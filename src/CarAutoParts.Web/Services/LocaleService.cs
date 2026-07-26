using System.Globalization;
using Blazored.LocalStorage;
using Microsoft.JSInterop;

namespace CarAutoParts.Web.Services;

public sealed class LocaleService
{
    private const string StorageKey = "cap.culture";
    private readonly ILocalStorageService _storage;
    private readonly IJSRuntime _js;
    private readonly Dictionary<string, Dictionary<string, string>> _strings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Nav_Dashboard"] = "Dashboard",
            ["Nav_POS"] = "POS",
            ["Nav_Finance"] = "Finance",
            ["Toast_Saved"] = "Saved successfully",
            ["Toast_Error"] = "Something went wrong",
            ["Finance_Journals"] = "Journals",
            ["POS_Checkout"] = "Checkout & send to FBR"
        },
        ["ur"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Nav_Dashboard"] = "ڈیش بورڈ",
            ["Nav_POS"] = "پوائنٹ آف سیل",
            ["Nav_Finance"] = "مالیات",
            ["Toast_Saved"] = "کامیابی سے محفوظ ہو گیا",
            ["Toast_Error"] = "کچھ غلط ہو گیا",
            ["Finance_Journals"] = "جرنلز",
            ["POS_Checkout"] = "چیک آؤٹ اور ایف بی آر"
        }
    };

    public string Culture { get; private set; } = "en";
    public bool IsRtl => Culture.StartsWith("ur", StringComparison.OrdinalIgnoreCase);
    public event Action? Changed;

    public LocaleService(ILocalStorageService storage, IJSRuntime js)
    {
        _storage = storage;
        _js = js;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var saved = await _storage.GetItemAsStringAsync(StorageKey);
            if (!string.IsNullOrWhiteSpace(saved))
                await SetCultureAsync(saved.Trim('"'));
            else
                await ApplyDocumentAsync();
        }
        catch
        {
            await ApplyDocumentAsync();
        }
    }

    public async Task SetCultureAsync(string culture)
    {
        Culture = culture.StartsWith("ur", StringComparison.OrdinalIgnoreCase) ? "ur" : "en";
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo(Culture == "ur" ? "ur-PK" : "en-US");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.DefaultThreadCurrentCulture;
        await _storage.SetItemAsStringAsync(StorageKey, Culture);
        await ApplyDocumentAsync();
        Changed?.Invoke();
    }

    public string this[string key] =>
        _strings.TryGetValue(Culture, out var map) && map.TryGetValue(key, out var value)
            ? value
            : key;

    private async Task ApplyDocumentAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("capSetLocale", Culture, IsRtl ? "rtl" : "ltr");
        }
        catch
        {
            // JS may not be ready during prerender
        }
    }
}
