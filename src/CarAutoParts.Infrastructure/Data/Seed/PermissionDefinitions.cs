using CarAutoParts.Application.Constants;

namespace CarAutoParts.Infrastructure.Data.Seed;

public static class PermissionDefinitions
{
    public static readonly (string Code, string Name, string Module)[] All =
    [
        (Permissions.DashboardView, "View Dashboard", "Dashboard"),
        (Permissions.ProductsView, "View Products", "Products"),
        (Permissions.ProductsCreate, "Create Products", "Products"),
        (Permissions.ProductsUpdate, "Edit Products", "Products"),
        (Permissions.ProductsDelete, "Delete Products", "Products"),
        (Permissions.ProductsImport, "Import Products", "Products"),
        (Permissions.ProductsExport, "Export Products", "Products"),
        (Permissions.CategoriesView, "View Categories", "Products"),
        (Permissions.CategoriesManage, "Manage Categories", "Products"),
        (Permissions.BrandsView, "View Brands", "Products"),
        (Permissions.BrandsManage, "Manage Brands", "Products"),
        (Permissions.WarehousesView, "View Warehouses", "Inventory"),
        (Permissions.WarehousesManage, "Manage Warehouses", "Inventory"),
        (Permissions.InventoryView, "View Inventory", "Inventory"),
        (Permissions.InventoryAdjust, "Adjust Stock", "Inventory"),
        (Permissions.InventoryReceive, "Receive Stock", "Inventory"),
        (Permissions.SuppliersView, "View Suppliers", "Partners"),
        (Permissions.SuppliersManage, "Manage Suppliers", "Partners"),
        (Permissions.CustomersView, "View Customers", "Partners"),
        (Permissions.CustomersManage, "Manage Customers", "Partners"),
        (Permissions.PurchasesView, "View Purchases", "Purchases"),
        (Permissions.PurchasesCreate, "Create Purchases", "Purchases"),
        (Permissions.PurchasesApprove, "Approve Purchases", "Purchases"),
        (Permissions.PurchasesReceive, "Receive Purchases", "Purchases"),
        (Permissions.PurchasesRequisition, "Purchase Requisitions", "Purchases"),
        (Permissions.SalesView, "View Sales", "Sales"),
        (Permissions.PosCheckout, "POS Checkout", "POS"),
        (Permissions.PosPriceOverride, "POS Price Override", "POS"),
        (Permissions.PosHold, "POS Hold/Recall", "POS"),
        (Permissions.PosShift, "POS Cashier Shift", "POS"),
        (Permissions.ReturnsManage, "Manage Returns", "Sales"),
        (Permissions.TransfersView, "View Transfers", "Inventory"),
        (Permissions.TransfersCreate, "Create Transfers", "Inventory"),
        (Permissions.TransfersApprove, "Approve Transfers", "Inventory"),
        (Permissions.SerialNumbersView, "View Serial Numbers", "Inventory"),
        (Permissions.SerialNumbersManage, "Manage Serial Numbers", "Inventory"),
        (Permissions.ReportsView, "View Reports", "Reports"),
        (Permissions.ReportsExport, "Export Reports", "Reports"),
        (Permissions.AnalyticsView, "View Analytics", "Analytics"),
        (Permissions.UsersView, "View Users", "Users"),
        (Permissions.UsersManage, "Manage Users", "Users"),
        (Permissions.AuditView, "View Audit Logs", "Audit"),
        (Permissions.ApprovalsView, "View Approvals", "Governance"),
        (Permissions.ApprovalsDecide, "Decide Approvals", "Governance"),
        (Permissions.ApprovalsManage, "Manage Approval Policies", "Governance"),
        (Permissions.FinanceVoid, "Void Posted Documents", "Finance"),
        (Permissions.MfaManage, "Manage MFA / Reset", "Users"),
        (Permissions.SettingsView, "View Settings", "Settings"),
        (Permissions.SettingsManage, "Manage Settings", "Settings"),
        (Permissions.BackupView, "View Backups", "Backups"),
        (Permissions.BackupManage, "Manage Backups", "Backups"),
        (Permissions.PlatformView, "View Platform", "Platform"),
        (Permissions.PlatformManage, "Manage Platform", "Platform"),
        (Permissions.FinanceView, "View Finance", "Finance"),
        (Permissions.FinanceManage, "Manage Finance", "Finance"),
        (Permissions.FinancePost, "Post Journals", "Finance"),
        (Permissions.FinanceForceClose, "Force Close Period", "Finance"),
        (Permissions.GrnManage, "Manage GRN", "Purchases"),
        (Permissions.ApInvoiceManage, "Manage AP Invoices", "Purchases"),
        (Permissions.CycleCountManage, "Manage Cycle Counts", "Inventory"),
        (Permissions.QuotationsManage, "Manage Quotations", "Sales"),
        (Permissions.DeliveriesManage, "Manage Deliveries", "Sales"),
        (Permissions.PriceListsManage, "Manage Price Lists", "Sales"),
        (Permissions.SalesPriceOverride, "Override Wholesale Prices", "Sales"),
        (Permissions.KitsManage, "Manage Kits", "Products"),
        (Permissions.CrmView, "View CRM", "CRM"),
        (Permissions.CrmManage, "Manage CRM", "CRM"),
        (Permissions.CrmLeads, "Manage CRM Leads", "CRM"),
        (Permissions.CrmActivities, "Manage CRM Activities", "CRM"),
        (Permissions.RfqManage, "Manage RFQs & Vendor Quotes", "Purchases"),
        (Permissions.SalesTargetsManage, "Manage Sales Targets", "Sales"),
        (Permissions.ServiceView, "View Service Tickets", "Service"),
        (Permissions.ServiceManage, "Manage Service Tickets", "Service")
    ];

    public static readonly string[] Admin = Permissions.All.ToArray();

    public static readonly string[] Manager =
    [
        Permissions.DashboardView,
        Permissions.ProductsView, Permissions.ProductsCreate, Permissions.ProductsUpdate, Permissions.ProductsDelete,
        Permissions.ProductsImport, Permissions.ProductsExport,
        Permissions.CategoriesView, Permissions.CategoriesManage,
        Permissions.BrandsView, Permissions.BrandsManage,
        Permissions.WarehousesView, Permissions.WarehousesManage,
        Permissions.InventoryView, Permissions.InventoryAdjust, Permissions.InventoryReceive,
        Permissions.SuppliersView, Permissions.SuppliersManage,
        Permissions.CustomersView, Permissions.CustomersManage,
        Permissions.PurchasesView, Permissions.PurchasesCreate, Permissions.PurchasesApprove, Permissions.PurchasesReceive, Permissions.PurchasesRequisition,
        Permissions.SalesView, Permissions.PosCheckout, Permissions.PosPriceOverride, Permissions.PosHold, Permissions.PosShift, Permissions.ReturnsManage,
        Permissions.TransfersView, Permissions.TransfersCreate, Permissions.TransfersApprove,
        Permissions.SerialNumbersView, Permissions.SerialNumbersManage,
        Permissions.ReportsView, Permissions.ReportsExport,
        Permissions.AnalyticsView,
        Permissions.AuditView,
        Permissions.ApprovalsView, Permissions.ApprovalsDecide, Permissions.ApprovalsManage, Permissions.FinanceVoid,
        Permissions.SettingsView, Permissions.SettingsManage,
        Permissions.BackupView,
        Permissions.PlatformView,
        Permissions.FinanceView, Permissions.FinanceManage, Permissions.FinancePost, Permissions.FinanceForceClose,
        Permissions.GrnManage, Permissions.ApInvoiceManage, Permissions.CycleCountManage,
        Permissions.QuotationsManage, Permissions.DeliveriesManage, Permissions.PriceListsManage,
        Permissions.SalesPriceOverride, Permissions.KitsManage,
        Permissions.CrmView, Permissions.CrmManage, Permissions.CrmLeads, Permissions.CrmActivities,
        Permissions.RfqManage, Permissions.SalesTargetsManage,
        Permissions.ServiceView, Permissions.ServiceManage
    ];

    public static readonly string[] SalesUser =
    [
        Permissions.DashboardView,
        Permissions.ProductsView,
        Permissions.CustomersView,
        Permissions.SalesView,
        Permissions.PosCheckout,
        Permissions.PosPriceOverride,
        Permissions.PosHold,
        Permissions.PosShift,
        Permissions.ReturnsManage,
        Permissions.ReportsView,
        Permissions.QuotationsManage, Permissions.DeliveriesManage, Permissions.SalesPriceOverride,
        Permissions.CrmView, Permissions.CrmLeads, Permissions.CrmActivities,
        Permissions.ServiceView, Permissions.ServiceManage
    ];

    public static readonly string[] InventoryUser =
    [
        Permissions.DashboardView,
        Permissions.ProductsView, Permissions.ProductsCreate, Permissions.ProductsUpdate,
        Permissions.CategoriesView, Permissions.CategoriesManage,
        Permissions.BrandsView, Permissions.BrandsManage,
        Permissions.WarehousesView,
        Permissions.InventoryView, Permissions.InventoryAdjust, Permissions.InventoryReceive,
        Permissions.PurchasesView, Permissions.PurchasesReceive, Permissions.PurchasesRequisition,
        Permissions.TransfersView, Permissions.TransfersCreate,
        Permissions.SerialNumbersView, Permissions.SerialNumbersManage,
        Permissions.ReportsView,
        Permissions.GrnManage, Permissions.CycleCountManage, Permissions.KitsManage,
        Permissions.RfqManage
    ];

    /// <summary>Counter cashier — POS + shifts; no price override.</summary>
    public static readonly string[] Cashier =
    [
        Permissions.DashboardView,
        Permissions.ProductsView,
        Permissions.CustomersView,
        Permissions.SalesView,
        Permissions.PosCheckout,
        Permissions.PosHold,
        Permissions.PosShift,
        Permissions.ReturnsManage
    ];

    /// <summary>Back-office accountant — finance/reports without POS checkout.</summary>
    public static readonly string[] Accountant =
    [
        Permissions.DashboardView,
        Permissions.ProductsView,
        Permissions.SuppliersView,
        Permissions.CustomersView,
        Permissions.PurchasesView,
        Permissions.SalesView,
        Permissions.ReportsView, Permissions.ReportsExport,
        Permissions.AnalyticsView,
        Permissions.AuditView,
        Permissions.ApprovalsView, Permissions.ApprovalsDecide,
        Permissions.FinanceView, Permissions.FinanceManage, Permissions.FinancePost, Permissions.FinanceVoid,
        Permissions.ApInvoiceManage,
        Permissions.SettingsView
    ];

    public static IReadOnlyDictionary<string, string[]> RoleTemplates { get; } =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Admin"] = Admin,
            ["Manager"] = Manager,
            ["SalesUser"] = SalesUser,
            ["InventoryUser"] = InventoryUser,
            ["Cashier"] = Cashier,
            ["Accountant"] = Accountant
        };
}
