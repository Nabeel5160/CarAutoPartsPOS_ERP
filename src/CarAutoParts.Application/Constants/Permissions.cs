namespace CarAutoParts.Application.Constants;

/// <summary>Permission codes used for role-based authorization.</summary>
public static class Permissions
{
    public const string DashboardView = "dashboard.view";

    public const string ProductsView = "products.view";
    public const string ProductsCreate = "products.create";
    public const string ProductsUpdate = "products.update";
    public const string ProductsDelete = "products.delete";
    public const string ProductsImport = "products.import";
    public const string ProductsExport = "products.export";

    public const string CategoriesView = "categories.view";
    public const string CategoriesManage = "categories.manage";

    public const string BrandsView = "brands.view";
    public const string BrandsManage = "brands.manage";

    public const string WarehousesView = "warehouses.view";
    public const string WarehousesManage = "warehouses.manage";

    public const string InventoryView = "inventory.view";
    public const string InventoryAdjust = "inventory.adjust";
    public const string InventoryReceive = "inventory.receive";

    public const string SuppliersView = "suppliers.view";
    public const string SuppliersManage = "suppliers.manage";

    public const string CustomersView = "customers.view";
    public const string CustomersManage = "customers.manage";

    public const string PurchasesView = "purchases.view";
    public const string PurchasesCreate = "purchases.create";
    public const string PurchasesApprove = "purchases.approve";
    public const string PurchasesReceive = "purchases.receive";
    public const string PurchasesRequisition = "purchases.requisition";

    public const string SalesView = "sales.view";
    public const string PosCheckout = "pos.checkout";
    public const string PosPriceOverride = "pos.price.override";
    public const string PosHold = "pos.hold";
    public const string PosShift = "pos.shift";

    public const string ReturnsManage = "returns.manage";

    public const string TransfersView = "transfers.view";
    public const string TransfersCreate = "transfers.create";
    public const string TransfersApprove = "transfers.approve";

    public const string SerialNumbersView = "serialnumbers.view";
    public const string SerialNumbersManage = "serialnumbers.manage";

    public const string ReportsView = "reports.view";
    public const string ReportsExport = "reports.export";

    public const string AnalyticsView = "analytics.view";

    public const string UsersView = "users.view";
    public const string UsersManage = "users.manage";

    public const string AuditView = "audit.view";

    public const string ApprovalsView = "approvals.view";
    public const string ApprovalsDecide = "approvals.decide";
    public const string ApprovalsManage = "approvals.manage";
    public const string FinanceVoid = "finance.void";
    public const string MfaManage = "auth.mfa.manage";

    public const string SettingsView = "settings.view";
    public const string SettingsManage = "settings.manage";

    public const string BackupView = "backup.view";
    public const string BackupManage = "backup.manage";

    public const string PlatformView = "platform.view";
    public const string PlatformManage = "platform.manage";
    public const string FinanceView = "finance.view";
    public const string FinanceManage = "finance.manage";
    public const string FinancePost = "finance.post";
    public const string FinanceForceClose = "finance.force-close";
    public const string GrnManage = "grn.manage";
    public const string ApInvoiceManage = "ap.invoice.manage";
    public const string CycleCountManage = "cyclecount.manage";
    public const string QuotationsManage = "quotations.manage";
    public const string DeliveriesManage = "deliveries.manage";
    public const string PriceListsManage = "pricelists.manage";
    /// <summary>Override resolved price-list / catalog unit price on wholesale quote/SO lines.</summary>
    public const string SalesPriceOverride = "sales.price.override";
    public const string KitsManage = "kits.manage";

    public const string CrmView = "crm.view";
    public const string CrmManage = "crm.manage";
    public const string CrmLeads = "crm.leads";
    public const string CrmActivities = "crm.activities";

    /// <summary>Create/manage RFQs and vendor quotes; view uses PurchasesView.</summary>
    public const string RfqManage = "rfq.manage";

    /// <summary>Create/manage sales targets; view uses SalesView.</summary>
    public const string SalesTargetsManage = "salestargets.manage";

    /// <summary>View service tickets (Program C1 — Service Light).</summary>
    public const string ServiceView = "service.view";
    /// <summary>Create/manage service tickets and status transitions.</summary>
    public const string ServiceManage = "service.manage";

    /// <summary>All defined permission codes.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        DashboardView,
        ProductsView, ProductsCreate, ProductsUpdate, ProductsDelete, ProductsImport, ProductsExport,
        CategoriesView, CategoriesManage,
        BrandsView, BrandsManage,
        WarehousesView, WarehousesManage,
        InventoryView, InventoryAdjust, InventoryReceive,
        SuppliersView, SuppliersManage,
        CustomersView, CustomersManage,
        PurchasesView, PurchasesCreate, PurchasesApprove, PurchasesReceive, PurchasesRequisition,
        SalesView, PosCheckout, PosPriceOverride, PosHold, PosShift,
        ReturnsManage,
        TransfersView, TransfersCreate, TransfersApprove,
        SerialNumbersView, SerialNumbersManage,
        ReportsView, ReportsExport,
        AnalyticsView,
        UsersView, UsersManage,
        AuditView,
        ApprovalsView, ApprovalsDecide, ApprovalsManage, FinanceVoid, MfaManage,
        SettingsView, SettingsManage,
        BackupView, BackupManage,
        PlatformView, PlatformManage,
        FinanceView, FinanceManage, FinancePost, FinanceForceClose,
        GrnManage, ApInvoiceManage, CycleCountManage,
        QuotationsManage, DeliveriesManage, PriceListsManage, SalesPriceOverride, KitsManage,
        CrmView, CrmManage, CrmLeads, CrmActivities,
        RfqManage, SalesTargetsManage,
        ServiceView, ServiceManage
    ];
}
