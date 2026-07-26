using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CarAutoParts.Web.Models;

namespace CarAutoParts.Web.Services;

public sealed class ApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    private readonly HttpClient _http;

    public ApiClient(HttpClient http) => _http = http;

    public async Task<(string? Text, string? Error, int Status)> GetTextAsync(string url, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync(url, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                return (null, ExtractError(text), (int)response.StatusCode);
            return (text, null, (int)response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            ApiReachability.MarkDown(ex.Message);
            return (null, "API unreachable: " + ex.Message, 0);
        }
        catch (TaskCanceledException)
        {
            ApiReachability.MarkDown("timeout");
            return (null, "API request timed out.", 0);
        }
    }

    public async Task<(T? Data, string? Error, int Status)> GetAsync<T>(string url, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync(url, ct);
            ApiReachability.MarkUp();
            return await ReadAsync<T>(response, ct);
        }
        catch (HttpRequestException ex)
        {
            ApiReachability.MarkDown(ex.Message);
            return (default, "API unreachable: " + ex.Message, 0);
        }
        catch (TaskCanceledException)
        {
            ApiReachability.MarkDown("timeout");
            return (default, "API request timed out.", 0);
        }
    }

    public async Task<(T? Data, string? Error, int Status)> PostAsync<T>(string url, object? body, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(url, body, JsonOptions, ct);
            ApiReachability.MarkUp();
            return await ReadAsync<T>(response, ct);
        }
        catch (HttpRequestException ex)
        {
            ApiReachability.MarkDown(ex.Message);
            return (default, "API unreachable: " + ex.Message, 0);
        }
        catch (TaskCanceledException)
        {
            ApiReachability.MarkDown("timeout");
            return (default, "API request timed out.", 0);
        }
    }

    public async Task<(T? Data, string? Error, int Status)> PutAsync<T>(string url, object? body, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync(url, body, JsonOptions, ct);
        return await ReadAsync<T>(response, ct);
    }

    public async Task<(bool Ok, string? Error, int Status)> PostAsync(string url, object? body, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(url, body, JsonOptions, ct);
        if (response.IsSuccessStatusCode) return (true, null, (int)response.StatusCode);
        var err = await response.Content.ReadAsStringAsync(ct);
        return (false, ExtractError(err), (int)response.StatusCode);
    }

    public async Task<(bool Ok, string? Error, int Status)> PutAsync(string url, object? body, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync(url, body, JsonOptions, ct);
        if (response.IsSuccessStatusCode) return (true, null, (int)response.StatusCode);
        var err = await response.Content.ReadAsStringAsync(ct);
        return (false, ExtractError(err), (int)response.StatusCode);
    }

    public async Task<(bool Ok, string? Error, int Status)> DeleteAsync(string url, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync(url, ct);
        if (response.IsSuccessStatusCode) return (true, null, (int)response.StatusCode);
        var err = await response.Content.ReadAsStringAsync(ct);
        return (false, ExtractError(err), (int)response.StatusCode);
    }

    public async Task<(byte[]? Bytes, string? Error)> GetBytesAsync(string url, CancellationToken ct = default)
    {
        var response = await _http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            return (null, ExtractError(await response.Content.ReadAsStringAsync(ct)));
        return (await response.Content.ReadAsByteArrayAsync(ct), null);
    }

    public async Task<(T? Data, string? Error, int Status)> PostMultipartAsync<T>(string url, MultipartFormDataContent content, CancellationToken ct = default)
    {
        var response = await _http.PostAsync(url, content, ct);
        return await ReadAsync<T>(response, ct);
    }

    public async Task<(bool Ok, string? Error, int Status)> PostMultipartAsync(string url, MultipartFormDataContent content, CancellationToken ct = default)
    {
        var response = await _http.PostAsync(url, content, ct);
        if (response.IsSuccessStatusCode) return (true, null, (int)response.StatusCode);
        return (false, ExtractError(await response.Content.ReadAsStringAsync(ct)), (int)response.StatusCode);
    }

    public static string ToQuery(QuerySpec q)
    {
        var sb = new StringBuilder($"?page={q.Page}&pageSize={q.PageSize}");
        if (!string.IsNullOrWhiteSpace(q.Search))
            sb.Append($"&search={Uri.EscapeDataString(q.Search)}");
        return sb.ToString();
    }

    private static async Task<(T? Data, string? Error, int Status)> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var status = (int)response.StatusCode;
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return (default, ExtractError(text), status);

        if (string.IsNullOrWhiteSpace(text) || status == 204)
            return (default, null, status);

        var data = JsonSerializer.Deserialize<T>(text, JsonOptions);
        return (data, null, status);
    }

    private static string ExtractError(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
            {
                var d = detail.GetString();
                if (!string.IsNullOrWhiteSpace(d)) return d!;
            }
            if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
            {
                var t = title.GetString();
                if (!string.IsNullOrWhiteSpace(t)) return t!;
            }
            if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
            {
                var e = error.GetString();
                if (!string.IsNullOrWhiteSpace(e)) return e!;
            }
        }
        catch { /* ignore */ }

        try
        {
            var err = JsonSerializer.Deserialize<ApiError>(text, JsonOptions);
            if (!string.IsNullOrWhiteSpace(err?.Error)) return err.Error!;
        }
        catch { /* ignore */ }
        return string.IsNullOrWhiteSpace(text) ? "Request failed." : text;
    }
}
