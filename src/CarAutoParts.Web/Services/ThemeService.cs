using Blazored.LocalStorage;
using Microsoft.JSInterop;

namespace CarAutoParts.Web.Services;

public sealed class ThemeService
{
    public const string StorageKey = "cap.theme";

    private readonly ILocalStorageService _storage;
    private readonly IJSRuntime _js;

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
        var saved = await _storage.GetItemAsStringAsync(StorageKey);
        if (!string.IsNullOrWhiteSpace(saved) && saved.Contains(':'))
        {
            var parts = saved.Trim('"').Split(':');
            Mode = parts[0];
            Accent = parts.Length > 1 ? parts[1] : "amber";
        }

        await ApplyAsync();
    }

    public async Task SetAsync(string mode, string accent)
    {
        Mode = mode;
        Accent = accent;
        await _storage.SetItemAsStringAsync(StorageKey, $"{mode}:{accent}");
        await ApplyAsync();
        Changed?.Invoke();
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
}
