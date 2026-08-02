namespace CarAutoParts.Web.Models;

public sealed class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public sealed class ApiError
{
    public string? Error { get; set; }
}

public sealed class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginUserDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public List<string> Roles { get; set; } = [];
    public List<string> Permissions { get; set; } = [];
    public bool MustChangePassword { get; set; }
    public bool MfaEnabled { get; set; }
    public bool MustEnrollMfa { get; set; }
}

public sealed class ApiLoginResponse
{
    public string? AccessToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public LoginUserDto? User { get; set; }
    public bool MfaRequired { get; set; }
    public string? MfaTicket { get; set; }
    public bool MustEnrollMfa { get; set; }
}

public sealed class MeDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool MustChangePassword { get; set; }
    public bool MfaEnabled { get; set; }
    public bool MustEnroll { get; set; }
    public int? CompanyId { get; set; }
    public int? BranchId { get; set; }
    public List<int> AllowedBranchIds { get; set; } = [];
    public List<string> Permissions { get; set; } = [];
    public List<string> Roles { get; set; } = [];
}

public sealed class QuerySpec
{
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? FitmentMake { get; set; }
    public string? FitmentModel { get; set; }
    public int? FitmentYear { get; set; }
}

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
    public const string SettingsView = "settings.view";
    public const string SettingsManage = "settings.manage";
    public const string BackupView = "backup.view";
    public const string BackupManage = "backup.manage";
    public const string PlatformView = "platform.view";
    public const string PlatformManage = "platform.manage";
    public const string FinanceView = "finance.view";
    public const string FinanceManage = "finance.manage";
    public const string FinancePost = "finance.post";
    public const string GrnManage = "grn.manage";
    public const string ApInvoiceManage = "ap.invoice.manage";
    public const string CycleCountManage = "cyclecount.manage";
    public const string QuotationsManage = "quotations.manage";
    public const string DeliveriesManage = "deliveries.manage";
    public const string PriceListsManage = "pricelists.manage";
    public const string SalesPriceOverride = "sales.price.override";
    public const string PosPriceOverride = "pos.price.override";
    public const string KitsManage = "kits.manage";
    public const string ServiceView = "service.view";
    public const string ServiceManage = "service.manage";
}
