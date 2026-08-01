using System.Net.Http.Json;
using System.Text.Json;
using Blazored.LocalStorage;
using CarAutoParts.Web.Models;
using Microsoft.JSInterop;

namespace CarAutoParts.Web.Services;

/// <summary>Client-side app config (branding, modules, fields, labels) with localStorage cache.</summary>
public sealed class WebAppConfigService
{
    private const string StorageKey = "cap.appconfig";
    private readonly HttpClient _http;
    private readonly ILocalStorageService _storage;
    private readonly IJSRuntime _js;
    private readonly LocaleService _locale;

    public AppConfigClientDto? Current { get; private set; }
    public BrandingClientDto Branding => Current?.Branding ?? BrandingClientDto.Default;
    public event Action? Changed;

    public WebAppConfigService(HttpClient http, ILocalStorageService storage, IJSRuntime js, LocaleService locale)
    {
        _http = http;
        _storage = storage;
        _js = js;
        _locale = locale;
    }

    public async Task InitializePublicAsync()
    {
        try
        {
            var cached = await _storage.GetItemAsync<AppConfigClientDto>(StorageKey);
            if (cached is not null)
            {
                Current = cached;
                ApplyLocaleOverrides();
                await ApplyDocumentBrandAsync();
            }
        }
        catch { /* ignore */ }

        try
        {
            var pub = await _http.GetFromJsonAsync<PublicAppConfigClientDto>("api/app-config/public");
            if (pub is not null)
            {
                Current ??= new AppConfigClientDto();
                Current.VerticalKey = pub.Branding.VerticalKey;
                Current.Branding = pub.Branding;
                if (pub.Labels.Count > 0)
                    Current.Labels["en"] = pub.Labels;
                await PersistAsync();
                ApplyLocaleOverrides();
                await ApplyDocumentBrandAsync();
                Changed?.Invoke();
            }
        }
        catch
        {
            Current ??= new AppConfigClientDto { Branding = BrandingClientDto.Default };
        }
    }

    public async Task RefreshFullAsync()
    {
        try
        {
            var full = await _http.GetFromJsonAsync<AppConfigClientDto>("api/app-config");
            if (full is not null)
            {
                Current = full;
                await PersistAsync();
                ApplyLocaleOverrides();
                await ApplyDocumentBrandAsync();
                Changed?.Invoke();
            }
        }
        catch { /* keep cached */ }
    }

    public bool ModuleEnabled(string key) =>
        Current?.Modules is null ||
        !Current.Modules.TryGetValue(key, out var enabled) ||
        enabled;

    public FieldConfigClientDto Field(string key) =>
        Current?.Fields is not null && Current.Fields.TryGetValue(key, out var f)
            ? f
            : new FieldConfigClientDto { Visible = true, Label = key };

    public bool BehaviorEnabled(string key) =>
        Current?.Behaviors is not null &&
        Current.Behaviors.TryGetValue(key, out var v) &&
        string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);

    public async Task<(AppConfigClientDto? Data, string? Error)> UpdateAsync(AppConfigUpdateClientRequest request)
    {
        var response = await _http.PutAsJsonAsync("api/app-config", request);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            return (null, err);
        }
        var data = await response.Content.ReadFromJsonAsync<AppConfigClientDto>();
        if (data is not null)
        {
            Current = data;
            await PersistAsync();
            ApplyLocaleOverrides();
            await ApplyDocumentBrandAsync();
            Changed?.Invoke();
        }
        return (data, null);
    }

    private void ApplyLocaleOverrides()
    {
        if (Current?.Labels is null) return;
        var culture = _locale.Culture;
        if (Current.Labels.TryGetValue(culture, out var map))
            _locale.SetOverrides(culture, map);
        else if (Current.Labels.TryGetValue("en", out var en))
            _locale.SetOverrides(culture, en);
    }

    private async Task PersistAsync()
    {
        if (Current is null) return;
        try { await _storage.SetItemAsync(StorageKey, Current); } catch { /* ignore */ }
    }

    private async Task ApplyDocumentBrandAsync()
    {
        try
        {
            // Theme/accent left null so company branding never overrides the user's saved UI theme.
            await _js.InvokeVoidAsync("capBrand.apply",
                Branding.AppName,
                Branding.ShortName,
                Branding.AccentWord,
                Branding.LogoUrl,
                null,
                null);
        }
        catch { /* JS may not be ready */ }
    }
}
