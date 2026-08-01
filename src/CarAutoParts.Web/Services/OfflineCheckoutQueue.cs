using System.Text.Json;
using Microsoft.JSInterop;

namespace CarAutoParts.Web.Services;

/// <summary>Durable IndexedDB queue for POS checkouts during API outages (Phase 10).</summary>
public sealed class OfflineCheckoutQueue
{
    private readonly IJSRuntime _js;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public event Action? Changed;
    public int PendingCount { get; private set; }
    public bool IsDraining { get; private set; }

    public OfflineCheckoutQueue(IJSRuntime js) => _js = js;

    public async Task RefreshCountAsync()
    {
        try
        {
            PendingCount = await _js.InvokeAsync<int>("capOfflineOutbox.pendingCount");
            Changed?.Invoke();
        }
        catch
        {
            /* IndexedDB unavailable */
        }
    }

    public async Task EnqueueAsync(object checkoutPayload, string idempotencyKey, int? shiftId)
    {
        await _js.InvokeVoidAsync("capOfflineOutbox.enqueue", new
        {
            idempotencyKey,
            payload = checkoutPayload,
            shiftId,
            createdAt = DateTime.UtcNow.ToString("O")
        });
        await RefreshCountAsync();
    }

    public async Task<IReadOnlyList<OfflineQueueItem>> ListAsync()
    {
        try
        {
            var json = await _js.InvokeAsync<JsonElement>("capOfflineOutbox.list");
            if (json.ValueKind != JsonValueKind.Array)
                return [];

            var list = new List<OfflineQueueItem>();
            foreach (var el in json.EnumerateArray())
            {
                list.Add(new OfflineQueueItem(
                    el.GetProperty("idempotencyKey").GetString() ?? "",
                    el.TryGetProperty("status", out var st) ? st.GetString() ?? "Pending" : "Pending",
                    el.TryGetProperty("createdAt", out var ca) ? ca.GetString() : null,
                    el.TryGetProperty("lastError", out var le) && le.ValueKind != JsonValueKind.Null ? le.GetString() : null,
                    el.TryGetProperty("shiftId", out var sh) && sh.ValueKind == JsonValueKind.Number ? sh.GetInt32() : null,
                    el.TryGetProperty("payload", out var p) ? p : default));
            }
            return list;
        }
        catch
        {
            return [];
        }
    }

    public async Task MarkSyncedAsync(string idempotencyKey)
    {
        await _js.InvokeVoidAsync("capOfflineOutbox.remove", idempotencyKey);
        await RefreshCountAsync();
    }

    public async Task MarkFailedAsync(string idempotencyKey, string error)
    {
        await _js.InvokeVoidAsync("capOfflineOutbox.update", idempotencyKey, new
        {
            status = "Failed",
            lastError = error,
            retryCount = 1
        });
        await RefreshCountAsync();
    }

    public async Task MarkSyncingAsync(string idempotencyKey)
    {
        await _js.InvokeVoidAsync("capOfflineOutbox.update", idempotencyKey, new { status = "Syncing" });
    }

    public async Task DrainAsync(Func<object, Task<(bool Ok, string? Error)>> postCheckout)
    {
        if (IsDraining || ApiReachability.IsDown) return;
        IsDraining = true;
        Changed?.Invoke();
        try
        {
            var ready = await _js.InvokeAsync<JsonElement>("capOfflineOutbox.drainReady");
            if (ready.ValueKind != JsonValueKind.Array) return;

            foreach (var el in ready.EnumerateArray())
            {
                var key = el.GetProperty("idempotencyKey").GetString() ?? "";
                if (string.IsNullOrEmpty(key)) continue;
                await MarkSyncingAsync(key);

                object payload = el.TryGetProperty("payload", out var p)
                    ? JsonSerializer.Deserialize<object>(p.GetRawText())!
                    : new { };

                var (ok, error) = await postCheckout(payload);
                if (ok)
                    await MarkSyncedAsync(key);
                else
                    await MarkFailedAsync(key, error ?? "Sync failed");
            }
        }
        finally
        {
            IsDraining = false;
            await RefreshCountAsync();
        }
    }

    public record OfflineQueueItem(
        string IdempotencyKey,
        string Status,
        string? CreatedAt,
        string? LastError,
        int? ShiftId,
        JsonElement Payload);
}
