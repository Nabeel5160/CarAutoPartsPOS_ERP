namespace CarAutoParts.Web.Models;

public sealed class BrandingClientDto
{
    public string AppName { get; set; } = "Car Auto Parts";
    public string ShortName { get; set; } = "Car Auto";
    public string AccentWord { get; set; } = "Parts";
    public string? LogoUrl { get; set; }
    public string Theme { get; set; } = "dark";
    public string Accent { get; set; } = "amber";
    public string VerticalKey { get; set; } = "auto-parts";

    public static BrandingClientDto Default { get; } = new();
}

public sealed class FieldConfigClientDto
{
    public bool Visible { get; set; } = true;
    public bool Required { get; set; }
    public string Label { get; set; } = "";
}

public sealed class VerticalPresetClientDto
{
    public string Key { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
}

public sealed class AppConfigClientDto
{
    public string VerticalKey { get; set; } = "auto-parts";
    public BrandingClientDto Branding { get; set; } = BrandingClientDto.Default;
    public Dictionary<string, bool> Modules { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, FieldConfigClientDto> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Behaviors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Dictionary<string, string>> Labels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<VerticalPresetClientDto> AvailableVerticals { get; set; } = [];
}

public sealed class PublicAppConfigClientDto
{
    public BrandingClientDto Branding { get; set; } = BrandingClientDto.Default;
    public string DefaultCulture { get; set; } = "en";
    public Dictionary<string, string> Labels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class AppConfigUpdateClientRequest
{
    public string? VerticalKey { get; set; }
    public bool ApplyPresetDefaults { get; set; }
    public Dictionary<string, bool>? Modules { get; set; }
    public Dictionary<string, FieldConfigClientDto>? Fields { get; set; }
    public Dictionary<string, string>? Behaviors { get; set; }
    public Dictionary<string, string>? Brand { get; set; }
    public Dictionary<string, Dictionary<string, string>>? Labels { get; set; }
}

/// <summary>Module key constants mirrored from server ConfigKeys (nav gating).</summary>
public static class ModuleKeys
{
    public const string CatalogProducts = "catalog.products";
    public const string CatalogCategories = "catalog.categories";
    public const string CatalogBrands = "catalog.brands";
    public const string CatalogKits = "catalog.kits";
    public const string CatalogPriceLists = "catalog.priceLists";
    public const string CatalogBarcodes = "catalog.barcodes";
    public const string InvStock = "inventory.stock";
    public const string InvWarehouses = "inventory.warehouses";
    public const string InvMovements = "inventory.movements";
    public const string InvSerials = "inventory.serials";
    public const string InvReservations = "inventory.reservations";
    public const string InvCycleCounts = "inventory.cycleCounts";
    public const string InvTransfers = "inventory.transfers";
    public const string PartnersSuppliers = "partners.suppliers";
    public const string PartnersCustomers = "partners.customers";
    public const string PartnersAging = "partners.aging";
    public const string PartnersReceipts = "partners.receipts";
    public const string PurchOrders = "purchasing.orders";
    public const string PurchRequisitions = "purchasing.requisitions";
    public const string PurchReorder = "purchasing.reorder";
    public const string PurchGrn = "purchasing.grn";
    public const string PurchAp = "purchasing.apInvoices";
    public const string SalesPos = "sales.pos";
    public const string SalesQuotations = "sales.quotations";
    public const string SalesOrders = "sales.orders";
    public const string SalesDeliveries = "sales.deliveries";
    public const string SalesInvoices = "sales.invoices";
    public const string SalesReturns = "sales.returns";
    public const string SalesFbr = "sales.fbr";
    public const string SalesCrm = "sales.crm";
    public const string FinanceCompany = "finance.company";
    public const string FinanceCoa = "finance.coa";
    public const string FinanceJournals = "finance.journals";
    public const string FinancePeriods = "finance.periods";
    public const string FinanceOpening = "finance.openingBalances";
    public const string FinanceBank = "finance.bankRecon";
    public const string FinanceReports = "finance.reports";
    public const string FinanceMaps = "finance.mappings";
    public const string InsightsReports = "insights.reports";
    public const string InsightsAnalytics = "insights.analytics";
    public const string ServiceTickets = "service.tickets";

    public const string FieldProductOem = "product.oem";
    public const string FieldProductPartNumber = "product.partNumber";
    public const string FieldProductHsCode = "product.hsCode";
    public const string FieldProductFitment = "product.fitment";
}
