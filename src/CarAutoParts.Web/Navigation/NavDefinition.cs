using CarAutoParts.Web.Models;

namespace CarAutoParts.Web.Navigation;

public sealed record NavDefItem(
    string TitleKeyOrLiteral,
    string Href,
    string Icon,
    string? Permission,
    string? ModuleKey,
    bool Exact = false,
    bool TitleIsLocaleKey = false);

public sealed record NavDefGroup(
    string Id,
    string TitleKeyOrLiteral,
    string Icon,
    IReadOnlyList<NavDefItem> Children,
    bool Leaf = false,
    bool TitleIsLocaleKey = false);

/// <summary>Central navigation definition with module keys for feature gating.</summary>
public static class NavDefinition
{
    public static IReadOnlyList<NavDefGroup> Groups { get; } =
    [
        new("dash", "Nav_Dashboard", "◉",
            [new("Nav_Dashboard", "", "◉", "dashboard.view", null, Exact: true, TitleIsLocaleKey: true)],
            Leaf: true, TitleIsLocaleKey: true),
        new("catalog", "Catalog", "▣",
        [
            new("Products", "products", "▣", "products.view", ModuleKeys.CatalogProducts),
            new("Categories", "categories", "▦", "categories.view", ModuleKeys.CatalogCategories),
            new("Brands", "brands", "◈", "brands.view", ModuleKeys.CatalogBrands),
            new("Kits", "kits", "🧩", "kits.manage", ModuleKeys.CatalogKits),
            new("Price Lists", "price-lists", "💲", "pricelists.manage", ModuleKeys.CatalogPriceLists),
            new("Barcodes", "barcodes", "║", "products.view", ModuleKeys.CatalogBarcodes),
        ]),
        new("inventory", "Inventory", "▤",
        [
            new("Stock", "inventory", "▤", "inventory.view", ModuleKeys.InvStock),
            new("Warehouses", "warehouses", "⌂", "warehouses.view", ModuleKeys.InvWarehouses),
            new("Movements", "movements", "⇄", "inventory.view", ModuleKeys.InvMovements),
            new("Serials", "serials", "#", "serialnumbers.view", ModuleKeys.InvSerials),
            new("Reservations", "reservations", "⏳", "inventory.adjust", ModuleKeys.InvReservations),
            new("Cycle Counts", "cycle-counts", "🔢", "cyclecount.manage", ModuleKeys.InvCycleCounts),
            new("Transfers", "transfers", "↔", "transfers.view", ModuleKeys.InvTransfers),
        ]),
        new("partners", "Partners", "◎",
        [
            new("Suppliers", "suppliers", "🚚", "suppliers.view", ModuleKeys.PartnersSuppliers),
            new("Customers", "customers", "◎", "customers.view", ModuleKeys.PartnersCustomers),
            new("Aging", "partner-aging", "⏰", "finance.view", ModuleKeys.PartnersAging),
            new("Receipts", "receipts", "💰", "finance.view", ModuleKeys.PartnersReceipts),
        ]),
        new("purchasing", "Purchasing", "↓",
        [
            new("Purchase Orders", "purchases", "↓", "purchases.view", ModuleKeys.PurchOrders),
            new("Requisitions", "requisitions", "☰", "purchases.requisition", ModuleKeys.PurchRequisitions),
            new("Reorder", "reorder", "↻", "purchases.requisition", ModuleKeys.PurchReorder),
            new("GRN", "grn", "📥", "grn.manage", ModuleKeys.PurchGrn),
            new("AP Invoices", "ap-invoices", "📄", "ap.invoice.manage", ModuleKeys.PurchAp),
            new("RFQ", "rfq", "📨", "purchases.view", null),
        ]),
        new("sales", "Sales", "💳",
        [
            new("Nav_POS", "pos", "💳", "pos.checkout", ModuleKeys.SalesPos, TitleIsLocaleKey: true),
            new("Quotations", "quotations", "✎", "quotations.manage", ModuleKeys.SalesQuotations),
            new("Sales Orders", "sales-orders", "≡", "sales.view", ModuleKeys.SalesOrders),
            new("Deliveries", "deliveries", "🚛", "deliveries.manage", ModuleKeys.SalesDeliveries),
            new("Invoices", "invoices", "🧾", "sales.view", ModuleKeys.SalesInvoices),
            new("Returns", "returns", "↩", "returns.manage", ModuleKeys.SalesReturns),
            new("FBR", "fbr", "☁", "pos.checkout", ModuleKeys.SalesFbr),
            new("Sales Targets", "sales-targets", "🎯", "sales.view", null),
        ]),
        new("crm", "CRM", "◎",
        [
            new("Leads", "crm/leads", "◎", "crm.view", ModuleKeys.SalesCrm),
            new("Tasks", "crm/tasks", "✓", "crm.view", ModuleKeys.SalesCrm),
            new("Pipeline", "crm/pipeline", "≡", "crm.view", ModuleKeys.SalesCrm),
            new("CRM Settings", "crm/settings", "⚙", "crm.manage", ModuleKeys.SalesCrm),
        ]),
        new("service", "Service", "🛠",
        [
            new("Tickets", "service/tickets", "🛠", "service.view", ModuleKeys.ServiceTickets),
            new("AMC Contracts", "service/amc", "📋", "service.view", ModuleKeys.ServiceTickets),
            new("Warranty claims", "service/warranty", "✅", "service.view", ModuleKeys.ServiceTickets),
            new("Knowledge Base", "service/kb", "📘", "service.view", ModuleKeys.ServiceTickets),
            new("SLA Policies", "service/sla", "⏱", "service.manage", ModuleKeys.ServiceTickets),
            new("SLA Breaches", "service/sla/breaches", "⚠", "service.view", ModuleKeys.ServiceTickets),
        ]),
        new("finance", "Finance", "📒",
        [
            new("Company", "company", "🏢", "platform.view", ModuleKeys.FinanceCompany),
            new("COA", "coa", "📒", "finance.view", ModuleKeys.FinanceCoa),
            new("Finance_Journals", "journals", "☰", "finance.view", ModuleKeys.FinanceJournals, TitleIsLocaleKey: true),
            new("Periods", "periods", "📅", "finance.post", ModuleKeys.FinancePeriods),
            new("Opening Balances", "opening-balances", "🏁", "finance.manage", ModuleKeys.FinanceOpening),
            new("Bank Recon", "bank-reconciliation", "🏦", "finance.view", ModuleKeys.FinanceBank),
            new("Budgets", "budgets", "📊", "finance.view", ModuleKeys.FinanceReports),
            new("Fin Reports", "financial-reports", "Σ", "finance.view", ModuleKeys.FinanceReports),
            new("Cash Flow", "cash-flow", "💵", "finance.view", ModuleKeys.FinanceReports),
            new("Mappings", "account-mappings", "🔗", "finance.view", ModuleKeys.FinanceMaps),
        ]),
        new("reports", "Nav_Reports", "▤",
        [
            new("Daily sales", "reports?type=daily-sales", "📅", "reports.export", ModuleKeys.InsightsReports, Exact: true),
            new("Z shifts", "reports?type=z-shifts", "Z", "reports.export", ModuleKeys.InsightsReports, Exact: true),
            new("Returns", "reports?type=sales-returns", "↩", "reports.export", ModuleKeys.InsightsReports, Exact: true),
            new("Nav_ReportSales", "reports?type=sales", "💳", "reports.export", ModuleKeys.InsightsReports, Exact: true, TitleIsLocaleKey: true),
            new("Nav_ReportStock", "reports?type=inventory", "▤", "reports.export", ModuleKeys.InsightsReports, Exact: true, TitleIsLocaleKey: true),
            new("Sales dim", "reports?type=sales-dim", "▣", "reports.export", ModuleKeys.InsightsReports, Exact: true),
            new("Staff sales", "reports?type=sales-staff", "☺", "reports.export", ModuleKeys.InsightsReports, Exact: true),
            new("Nav_ReportPurchases", "reports?type=purchases", "↓", "reports.export", ModuleKeys.InsightsReports, Exact: true, TitleIsLocaleKey: true),
            new("Nav_ReportProfit", "reports?type=profit", "Σ", "reports.export", ModuleKeys.InsightsReports, Exact: true, TitleIsLocaleKey: true),
            new("Movements", "reports?type=movements", "⇄", "reports.export", ModuleKeys.InsightsReports, Exact: true),
            new("PO / GRN", "reports?type=purchasing-pipeline", "📥", "reports.export", ModuleKeys.InsightsReports, Exact: true),
            new("AR/AP Excel", "reports?type=aging", "⏰", "reports.export", ModuleKeys.InsightsReports, Exact: true),
            new("Tax", "reports?type=tax", "%", "reports.export", ModuleKeys.InsightsReports, Exact: true),
            new("FBR register", "reports?type=fbr", "☁", "reports.export", ModuleKeys.SalesFbr, Exact: true),
            new("Stock age", "reports?type=stock-aging", "⏳", "reports.export", ModuleKeys.InsightsReports, Exact: true),
        ], TitleIsLocaleKey: true),
        new("insights", "Nav_Insights", "📈",
        [
            new("Nav_Analytics", "analytics", "📈", "analytics.view", ModuleKeys.InsightsAnalytics, TitleIsLocaleKey: true),
        ], TitleIsLocaleKey: true),
        new("system", "System", "⚙",
        [
            new("Nav_Users", "users", "☺", "users.view", null, TitleIsLocaleKey: true),
            new("Nav_Onboarding", "onboarding", "①", "settings.manage", null, TitleIsLocaleKey: true),
            new("Approvals", "approvals", "✓", "approvals.view", null),
            new("Mobile", "m", "📱", null, null, Exact: true),
            new("Nav_Audit", "audit", "⏱", "audit.view", null, TitleIsLocaleKey: true),
            new("MFA setup", "mfa-setup", "⚿", null, null),
            new("Nav_Settings", "settings", "⚙", "settings.view", null, TitleIsLocaleKey: true),
            new("Backup", "backup", "💾", "backup.view", null),
            new("Notifications", "notifications", "🔔", null, null),
        ]),
    ];

    public static string? ModuleForRoute(string relativePath)
    {
        var path = relativePath.TrimStart('/').Split('?', '#')[0];
        if (string.IsNullOrEmpty(path)) return null;

        // Phase 17 mobile light — stock inherits inventory.stock gate; hub/approvals ungated by module.
        if (path.Equals("m/stock", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("m/stock/", StringComparison.OrdinalIgnoreCase))
            return ModuleKeys.InvStock;
        if (path.Equals("m", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("m/approvals", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("m/approvals/", StringComparison.OrdinalIgnoreCase))
            return null;
        if (path.Equals("m/service", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("m/service/", StringComparison.OrdinalIgnoreCase))
            return ModuleKeys.ServiceTickets;

        foreach (var group in Groups)
        {
            foreach (var item in group.Children)
            {
                if (string.IsNullOrEmpty(item.Href)) continue;
                var itemPath = item.Href.Split('?', '#')[0];
                if (path.Equals(itemPath, StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith(itemPath + "/", StringComparison.OrdinalIgnoreCase))
                    return item.ModuleKey;
            }
        }
        return null;
    }
}
