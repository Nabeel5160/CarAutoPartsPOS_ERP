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
            ["Nav_Inventory"] = "Inventory",
            ["Nav_Purchases"] = "Purchases",
            ["Nav_Sales"] = "Sales",
            ["Nav_Partners"] = "Partners",
            ["Nav_Reports"] = "Reports",
            ["Nav_ReportSales"] = "Sales report",
            ["Nav_ReportStock"] = "Stock report",
            ["Nav_ReportProduct"] = "Product report",
            ["Nav_ReportPurchases"] = "Purchase report",
            ["Nav_ReportProfit"] = "Profit report",
            ["Nav_Insights"] = "Insights",
            ["Nav_Analytics"] = "Analytics",
            ["Nav_Admin"] = "Admin",
            ["Nav_Settings"] = "Settings",
            ["Nav_Users"] = "Users",
            ["Nav_Audit"] = "Audit",
            ["Nav_Onboarding"] = "Setup",
            ["Nav_Approvals"] = "Approvals",
            ["Toast_Saved"] = "Saved successfully",
            ["Toast_Error"] = "Something went wrong",
            ["Finance_Journals"] = "Journals",
            ["Finance_Periods"] = "Periods",
            ["Finance_OpeningBalances"] = "Opening balances",
            ["Finance_BankRecon"] = "Bank recon",
            ["Finance_TrialBalance"] = "Trial balance",
            ["POS_Checkout"] = "Checkout & send to FBR",
            ["POS_Hold"] = "Hold sale",
            ["POS_Shift"] = "Cashier shift",
            ["Login_Title"] = "Sign in",
            ["Login_Username"] = "Username",
            ["Login_Password"] = "Password",
            ["Login_Submit"] = "Sign in",
            ["Login_Mfa"] = "Authenticator code",
            ["Grn_Title"] = "Goods receipt",
            ["Ap_Title"] = "AP invoices",
            ["Onboarding_Title"] = "First-run setup",
            ["Common_Refresh"] = "Refresh",
            ["Common_Save"] = "Save",
            ["Common_Cancel"] = "Cancel",
            ["POS_SearchPlaceholder"] = "Barcode / SKU / OEM / make — Enter adds"
        },
        ["ur"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Nav_Dashboard"] = "ڈیش بورڈ",
            ["Nav_POS"] = "پوائنٹ آف سیل",
            ["Nav_Finance"] = "مالیات",
            ["Nav_Inventory"] = "انوینٹری",
            ["Nav_Purchases"] = "خریداری",
            ["Nav_Sales"] = "فروخت",
            ["Nav_Partners"] = "پارٹنرز",
            ["Nav_Reports"] = "رپورٹس",
            ["Nav_ReportSales"] = "فروخت رپورٹ",
            ["Nav_ReportStock"] = "اسٹاک رپورٹ",
            ["Nav_ReportProduct"] = "پروڈکٹ رپورٹ",
            ["Nav_ReportPurchases"] = "خریداری رپورٹ",
            ["Nav_ReportProfit"] = "منافع رپورٹ",
            ["Nav_Insights"] = "بصیرت",
            ["Nav_Analytics"] = "تجزیات",
            ["Nav_Admin"] = "ایڈمن",
            ["Nav_Settings"] = "ترتیبات",
            ["Nav_Users"] = "صارفین",
            ["Nav_Audit"] = "آڈٹ",
            ["Nav_Onboarding"] = "سیٹ اپ",
            ["Nav_Approvals"] = "منظوریاں",
            ["Toast_Saved"] = "کامیابی سے محفوظ ہو گیا",
            ["Toast_Error"] = "کچھ غلط ہو گیا",
            ["Finance_Journals"] = "جرنلز",
            ["Finance_Periods"] = "ادوار",
            ["Finance_OpeningBalances"] = "اوپننگ بیلنس",
            ["Finance_BankRecon"] = "بینک میل",
            ["Finance_TrialBalance"] = "ٹرائل بیلنس",
            ["POS_Checkout"] = "چیک آؤٹ اور ایف بی آر",
            ["POS_Hold"] = "ہولڈ سیل",
            ["POS_Shift"] = "کیشیئر شفٹ",
            ["Login_Title"] = "سائن ان",
            ["Login_Username"] = "صارف نام",
            ["Login_Password"] = "پاس ورڈ",
            ["Login_Submit"] = "سائن ان",
            ["Login_Mfa"] = "تصدیقی کوڈ",
            ["Grn_Title"] = "مال وصولی",
            ["Ap_Title"] = "قابل ادائیگی انوائسز",
            ["Onboarding_Title"] = "پہلی بار سیٹ اپ",
            ["Common_Refresh"] = "تازہ کریں",
            ["Common_Save"] = "محفوظ کریں",
            ["Common_Cancel"] = "منسوخ"
        }
    };

    public string Culture { get; private set; } = "en";
    public bool IsRtl => Culture.StartsWith("ur", StringComparison.OrdinalIgnoreCase);
    public event Action? Changed;

    private Dictionary<string, string>? _overrides;

    public LocaleService(ILocalStorageService storage, IJSRuntime js)
    {
        _storage = storage;
        _js = js;
    }

    public void SetOverrides(string culture, IReadOnlyDictionary<string, string> map)
    {
        _overrides = new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);
        Changed?.Invoke();
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

    public string this[string key]
    {
        get
        {
            if (_overrides is not null && _overrides.TryGetValue(key, out var over) && !string.IsNullOrWhiteSpace(over))
                return over;
            return _strings.TryGetValue(Culture, out var map) && map.TryGetValue(key, out var value)
                ? value
                : key;
        }
    }

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
