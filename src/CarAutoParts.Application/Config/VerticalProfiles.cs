using System.Text.Json;

namespace CarAutoParts.Application.Config;

public static class ConfigScopes
{
    public const string Module = "module";
    public const string Field = "field";
    public const string Behavior = "behavior";
    public const string Label = "label";
    public const string Brand = "brand";
}

public static class ConfigKeys
{
    // Modules
    public const string ModCatalogProducts = "catalog.products";
    public const string ModCatalogCategories = "catalog.categories";
    public const string ModCatalogBrands = "catalog.brands";
    public const string ModCatalogKits = "catalog.kits";
    public const string ModCatalogPriceLists = "catalog.priceLists";
    public const string ModCatalogBarcodes = "catalog.barcodes";
    public const string ModInvStock = "inventory.stock";
    public const string ModInvWarehouses = "inventory.warehouses";
    public const string ModInvMovements = "inventory.movements";
    public const string ModInvSerials = "inventory.serials";
    public const string ModInvReservations = "inventory.reservations";
    public const string ModInvCycleCounts = "inventory.cycleCounts";
    public const string ModInvTransfers = "inventory.transfers";
    public const string ModPartnersSuppliers = "partners.suppliers";
    public const string ModPartnersCustomers = "partners.customers";
    public const string ModPartnersAging = "partners.aging";
    public const string ModPartnersReceipts = "partners.receipts";
    public const string ModPurchOrders = "purchasing.orders";
    public const string ModPurchRequisitions = "purchasing.requisitions";
    public const string ModPurchReorder = "purchasing.reorder";
    public const string ModPurchGrn = "purchasing.grn";
    public const string ModPurchAp = "purchasing.apInvoices";
    public const string ModSalesPos = "sales.pos";
    public const string ModSalesQuotations = "sales.quotations";
    public const string ModSalesOrders = "sales.orders";
    public const string ModSalesDeliveries = "sales.deliveries";
    public const string ModSalesInvoices = "sales.invoices";
    public const string ModSalesReturns = "sales.returns";
    public const string ModSalesFbr = "sales.fbr";
    public const string ModFinanceCompany = "finance.company";
    public const string ModFinanceCoa = "finance.coa";
    public const string ModFinanceJournals = "finance.journals";
    public const string ModFinancePeriods = "finance.periods";
    public const string ModFinanceOpening = "finance.openingBalances";
    public const string ModFinanceBank = "finance.bankRecon";
    public const string ModFinanceReports = "finance.reports";
    public const string ModFinanceMaps = "finance.mappings";
    public const string ModInsightsReports = "insights.reports";
    public const string ModInsightsAnalytics = "insights.analytics";

    // Fields
    public const string FieldProductOem = "product.oem";
    public const string FieldProductPartNumber = "product.partNumber";
    public const string FieldProductHsCode = "product.hsCode";
    public const string FieldProductFitment = "product.fitment";
    public const string FieldCustomerNtn = "customer.ntn";

    // Behaviors
    public const string BehFbrEnabled = "fbr.enabled";
    public const string BehTaxEnabled = "tax.enabled";
    public const string BehFitmentSearch = "pos.fitmentSearch";
    public const string BehSupersession = "pos.supersession";
    public const string BehCurrency = "currency";
    public const string BehDecimals = "decimals";

    // Brand
    public const string BrandAppName = "appName";
    public const string BrandShortName = "shortName";
    public const string BrandAccentWord = "accentWord";
    public const string BrandLogoUrl = "logoUrl";
    public const string BrandTheme = "theme";
    public const string BrandAccent = "accent";
}

public sealed record FieldConfigDto(bool Visible, bool Required, string Label);

public sealed record BrandingDto(
    string AppName,
    string ShortName,
    string AccentWord,
    string? LogoUrl,
    string Theme,
    string Accent,
    string VerticalKey);

public sealed record AppConfigDto(
    string VerticalKey,
    BrandingDto Branding,
    IReadOnlyDictionary<string, bool> Modules,
    IReadOnlyDictionary<string, FieldConfigDto> Fields,
    IReadOnlyDictionary<string, string> Behaviors,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Labels,
    IReadOnlyList<VerticalPresetInfo> AvailableVerticals);

public sealed record VerticalPresetInfo(string Key, string DisplayName, string Description);

public sealed record PublicAppConfigDto(
    BrandingDto Branding,
    string DefaultCulture,
    IReadOnlyDictionary<string, string> Labels);

public sealed record AppConfigUpdateRequest(
    string? VerticalKey,
    bool ApplyPresetDefaults,
    IReadOnlyDictionary<string, bool>? Modules,
    IReadOnlyDictionary<string, FieldConfigDto>? Fields,
    IReadOnlyDictionary<string, string>? Behaviors,
    IReadOnlyDictionary<string, string>? Brand,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? Labels);

public static class VerticalProfiles
{
    public const string AutoParts = "auto-parts";
    public const string BikeParts = "bike-parts";
    public const string GeneralRetail = "general-retail";

    public static IReadOnlyList<VerticalPresetInfo> All { get; } =
    [
        new(AutoParts, "Auto Parts", "Car / light-vehicle parts with OEM, fitment, and FBR"),
        new(BikeParts, "Bike Parts", "Motorcycle / bike parts with make-model-year fitment"),
        new(GeneralRetail, "General Retail", "Stationery and general POS — simplified catalog")
    ];

    public static bool IsKnown(string key) =>
        string.Equals(key, AutoParts, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, BikeParts, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, GeneralRetail, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string? key) =>
        IsKnown(key ?? "") ? key!.ToLowerInvariant() : AutoParts;

    public static IReadOnlySet<string> KnownModuleKeys { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ConfigKeys.ModCatalogProducts, ConfigKeys.ModCatalogCategories, ConfigKeys.ModCatalogBrands,
        ConfigKeys.ModCatalogKits, ConfigKeys.ModCatalogPriceLists, ConfigKeys.ModCatalogBarcodes,
        ConfigKeys.ModInvStock, ConfigKeys.ModInvWarehouses, ConfigKeys.ModInvMovements,
        ConfigKeys.ModInvSerials, ConfigKeys.ModInvReservations, ConfigKeys.ModInvCycleCounts, ConfigKeys.ModInvTransfers,
        ConfigKeys.ModPartnersSuppliers, ConfigKeys.ModPartnersCustomers, ConfigKeys.ModPartnersAging, ConfigKeys.ModPartnersReceipts,
        ConfigKeys.ModPurchOrders, ConfigKeys.ModPurchRequisitions, ConfigKeys.ModPurchReorder, ConfigKeys.ModPurchGrn, ConfigKeys.ModPurchAp,
        ConfigKeys.ModSalesPos, ConfigKeys.ModSalesQuotations, ConfigKeys.ModSalesOrders, ConfigKeys.ModSalesDeliveries,
        ConfigKeys.ModSalesInvoices, ConfigKeys.ModSalesReturns, ConfigKeys.ModSalesFbr,
        ConfigKeys.ModFinanceCompany, ConfigKeys.ModFinanceCoa, ConfigKeys.ModFinanceJournals, ConfigKeys.ModFinancePeriods,
        ConfigKeys.ModFinanceOpening, ConfigKeys.ModFinanceBank, ConfigKeys.ModFinanceReports, ConfigKeys.ModFinanceMaps,
        ConfigKeys.ModInsightsReports, ConfigKeys.ModInsightsAnalytics
    };

    public static IReadOnlySet<string> KnownFieldKeys { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ConfigKeys.FieldProductOem, ConfigKeys.FieldProductPartNumber, ConfigKeys.FieldProductHsCode,
        ConfigKeys.FieldProductFitment, ConfigKeys.FieldCustomerNtn
    };

    public static IReadOnlySet<string> KnownBehaviorKeys { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ConfigKeys.BehFbrEnabled, ConfigKeys.BehTaxEnabled, ConfigKeys.BehFitmentSearch,
        ConfigKeys.BehSupersession, ConfigKeys.BehCurrency, ConfigKeys.BehDecimals
    };

    public static IReadOnlySet<string> KnownBrandKeys { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ConfigKeys.BrandAppName, ConfigKeys.BrandShortName, ConfigKeys.BrandAccentWord,
        ConfigKeys.BrandLogoUrl, ConfigKeys.BrandTheme, ConfigKeys.BrandAccent
    };

    public static Dictionary<string, bool> DefaultModules(string vertical)
    {
        var all = KnownModuleKeys.ToDictionary(k => k, _ => true, StringComparer.OrdinalIgnoreCase);
        if (Normalize(vertical) == GeneralRetail)
        {
            all[ConfigKeys.ModCatalogKits] = false;
            all[ConfigKeys.ModSalesFbr] = false;
            all[ConfigKeys.ModInvSerials] = false;
            all[ConfigKeys.ModPurchRequisitions] = false;
        }
        return all;
    }

    public static Dictionary<string, FieldConfigDto> DefaultFields(string vertical)
    {
        var v = Normalize(vertical);
        return v switch
        {
            BikeParts => new Dictionary<string, FieldConfigDto>(StringComparer.OrdinalIgnoreCase)
            {
                [ConfigKeys.FieldProductOem] = new(true, false, "OEM / Part ref"),
                [ConfigKeys.FieldProductPartNumber] = new(true, false, "Part number"),
                [ConfigKeys.FieldProductHsCode] = new(true, false, "HS code"),
                [ConfigKeys.FieldProductFitment] = new(true, false, "Bike fitment (make / model / year)"),
                [ConfigKeys.FieldCustomerNtn] = new(true, false, "NTN / CNIC"),
            },
            GeneralRetail => new Dictionary<string, FieldConfigDto>(StringComparer.OrdinalIgnoreCase)
            {
                [ConfigKeys.FieldProductOem] = new(false, false, "OEM"),
                [ConfigKeys.FieldProductPartNumber] = new(false, false, "Part number"),
                [ConfigKeys.FieldProductHsCode] = new(false, false, "HS code"),
                [ConfigKeys.FieldProductFitment] = new(false, false, "Fitment"),
                [ConfigKeys.FieldCustomerNtn] = new(false, false, "Tax ID"),
            },
            _ => new Dictionary<string, FieldConfigDto>(StringComparer.OrdinalIgnoreCase)
            {
                [ConfigKeys.FieldProductOem] = new(true, false, "OEM number"),
                [ConfigKeys.FieldProductPartNumber] = new(true, false, "Part number"),
                [ConfigKeys.FieldProductHsCode] = new(true, false, "HS code"),
                [ConfigKeys.FieldProductFitment] = new(true, false, "Vehicle fitment"),
                [ConfigKeys.FieldCustomerNtn] = new(true, false, "NTN / CNIC"),
            }
        };
    }

    public static Dictionary<string, string> DefaultBehaviors(string vertical)
    {
        var v = Normalize(vertical);
        var fbr = v != GeneralRetail;
        var fitment = v != GeneralRetail;
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ConfigKeys.BehFbrEnabled] = fbr ? "true" : "false",
            [ConfigKeys.BehTaxEnabled] = "true",
            [ConfigKeys.BehFitmentSearch] = fitment ? "true" : "false",
            [ConfigKeys.BehSupersession] = fitment ? "true" : "false",
            [ConfigKeys.BehCurrency] = "PKR",
            [ConfigKeys.BehDecimals] = "2"
        };
    }

    public static Dictionary<string, string> DefaultBrand(string vertical, string? companyName = null)
    {
        var v = Normalize(vertical);
        return v switch
        {
            BikeParts => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ConfigKeys.BrandAppName] = companyName ?? "Bike Auto Parts",
                [ConfigKeys.BrandShortName] = "Bike Auto",
                [ConfigKeys.BrandAccentWord] = "Parts",
                [ConfigKeys.BrandLogoUrl] = "",
                [ConfigKeys.BrandTheme] = "dark",
                [ConfigKeys.BrandAccent] = "cyan"
            },
            GeneralRetail => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ConfigKeys.BrandAppName] = companyName ?? "Retail POS",
                [ConfigKeys.BrandShortName] = "Retail",
                [ConfigKeys.BrandAccentWord] = "POS",
                [ConfigKeys.BrandLogoUrl] = "",
                [ConfigKeys.BrandTheme] = "light",
                [ConfigKeys.BrandAccent] = "emerald"
            },
            _ => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ConfigKeys.BrandAppName] = companyName ?? "Car Auto Parts",
                [ConfigKeys.BrandShortName] = "Car Auto",
                [ConfigKeys.BrandAccentWord] = "Parts",
                [ConfigKeys.BrandLogoUrl] = "",
                [ConfigKeys.BrandTheme] = "dark",
                [ConfigKeys.BrandAccent] = "amber"
            }
        };
    }

    public static Dictionary<string, Dictionary<string, string>> DefaultLabels(string vertical)
    {
        var v = Normalize(vertical);
        var en = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var ur = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (v == BikeParts)
        {
            en["POS_SearchPlaceholder"] = "Barcode / SKU / OEM / bike make — Enter adds";
            en["Field_Fitment"] = "Bike fitment";
        }
        else if (v == GeneralRetail)
        {
            en["POS_SearchPlaceholder"] = "Barcode / SKU / name — Enter adds";
            en["POS_Checkout"] = "Checkout";
            en["Nav_POS"] = "POS";
        }
        else
        {
            en["POS_SearchPlaceholder"] = "Barcode / SKU / OEM / make — Enter adds";
        }

        return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = en,
            ["ur"] = ur
        };
    }

    public static string SerializeField(FieldConfigDto f) =>
        JsonSerializer.Serialize(new { visible = f.Visible, required = f.Required, label = f.Label });

    public static FieldConfigDto DeserializeField(string json, FieldConfigDto fallback)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new FieldConfigDto(
                root.TryGetProperty("visible", out var vis) ? vis.GetBoolean() : fallback.Visible,
                root.TryGetProperty("required", out var req) ? req.GetBoolean() : fallback.Required,
                root.TryGetProperty("label", out var lab) ? lab.GetString() ?? fallback.Label : fallback.Label);
        }
        catch
        {
            return fallback;
        }
    }
}
