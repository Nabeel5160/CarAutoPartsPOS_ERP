using Blazored.LocalStorage;
using Microsoft.JSInterop;

namespace CarAutoParts.Web.Services;

/// <summary>Per-browser UI theme preference. Survives refresh and login until the user changes it.</summary>
public sealed class ThemeService
{
    public const string StorageKey = "cap.theme";

    private readonly ILocalStorageService _storage;
    private readonly IJSRuntime _js;
    private bool _initialized;

    public event Action? Changed;

    public string Mode { get; private set; } = "dark";
    public string Accent { get; private set; } = "amber";

    public ThemeService(ILocalStorageService storage, IJSRuntime js)
    {
        _storage = storage;
        _js = js;
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Prefer structured DTO; fall back to legacy "mode:accent" string.
            var pref = await _storage.GetItemAsync<ThemePreference>(StorageKey);
            if (pref is not null && !string.IsNullOrWhiteSpace(pref.Mode))
            {
                Mode = NormalizeMode(pref.Mode);
                Accent = NormalizeAccent(pref.Accent);
            }
            else
            {
                var saved = await _storage.GetItemAsStringAsync(StorageKey);
                if (!string.IsNullOrWhiteSpace(saved) && saved.Contains(':'))
                {
                    var parts = saved.Trim().Trim('"').Split(':', 2);
                    Mode = NormalizeMode(parts[0]);
                    Accent = parts.Length > 1 ? NormalizeAccent(parts[1]) : "amber";
                }
                else
                {
                    // Early-boot key written by index.html / JS (plain localStorage, not JSON).
                    try
                    {
                        var raw = await _js.InvokeAsync<string?>("capTheme.getStored");
                        if (!string.IsNullOrWhiteSpace(raw) && raw.Contains(':'))
                        {
                            var parts = raw.Split(':', 2);
                            Mode = NormalizeMode(parts[0]);
                            Accent = parts.Length > 1 ? NormalizeAccent(parts[1]) : "amber";
                        }
                    }
                    catch { /* JS not ready */ }
                }
            }
        }
        catch
        {
            // LocalStorage/JS may not be ready on first paint — keep defaults.
        }

        await PersistAndApplyAsync();
        _initialized = true;
    }

    public async Task SetAsync(string mode, string accent)
    {
        Mode = NormalizeMode(mode);
        Accent = NormalizeAccent(accent);
        await PersistAndApplyAsync();
        Changed?.Invoke();
    }

    /// <summary>Re-apply current preference to the DOM (e.g. after branding refresh).</summary>
    public async Task ReapplyAsync()
    {
        if (!_initialized)
            await InitializeAsync();
        else
            await ApplyAsync();
    }

    private async Task PersistAndApplyAsync()
    {
        try
        {
            await _storage.SetItemAsync(StorageKey, new ThemePreference { Mode = Mode, Accent = Accent });
        }
        catch { /* ignore */ }

        try
        {
            // Plain string for early HTML boot script (avoids JSON quotes).
            await _js.InvokeVoidAsync("capTheme.setStored", $"{Mode}:{Accent}");
        }
        catch { /* ignore */ }

        await ApplyAsync();
    }

    private async Task ApplyAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("capTheme.apply", Mode, Accent);
        }
        catch
        {
            // JS may not be ready yet during first render.
        }
    }

    private static string NormalizeMode(string? mode)
    {
        var m = (mode ?? "dark").Trim().ToLowerInvariant();
        return m is "light" or "dark" ? m : "dark";
    }

    private static string NormalizeAccent(string? accent)
    {
        var a = (accent ?? "amber").Trim().ToLowerInvariant();
        return a is "amber" or "cyan" or "emerald" or "rose" ? a : "amber";
    }

    private sealed class ThemePreference
    {
        public string Mode { get; set; } = "dark";
        public string Accent { get; set; } = "amber";
    }
}
