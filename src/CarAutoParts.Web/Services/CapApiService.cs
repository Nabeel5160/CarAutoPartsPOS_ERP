using CarAutoParts.Web.Models;

namespace CarAutoParts.Web.Services;

/// <summary>Typed wrappers for every CarAutoParts API endpoint.</summary>
public sealed class CapApiService
{
    private readonly ApiClient _api;

    public CapApiService(ApiClient api) => _api = api;

    // Dashboard / Analytics
    public Task<(DashboardDto? Data, string? Error, int Status)> GetDashboardAsync() =>
        _api.GetAsync<DashboardDto>("/api/dashboard");

    public Task<(AnalyticsDto? Data, string? Error, int Status)> GetAnalyticsAsync(DateTime? from = null, DateTime? to = null)
    {
        var q = "?";
        if (from.HasValue) q += $"from={from:yyyy-MM-dd}&";
        if (to.HasValue) q += $"to={to:yyyy-MM-dd}&";
        return _api.GetAsync<AnalyticsDto>("/api/analytics" + q.TrimEnd('&', '?'));
    }

    // Products
    public Task<(PagedResult<ProductListDto>? Data, string? Error, int Status)> GetProductsAsync(QuerySpec q) =>
        _api.GetAsync<PagedResult<ProductListDto>>($"/api/products{ApiClient.ToQuery(q)}");

    public Task<(ProductDetailDto? Data, string? Error, int Status)> GetProductAsync(int id) =>
        _api.GetAsync<ProductDetailDto>($"/api/products/{id}");

    public Task<(ProductDetailDto? Data, string? Error, int Status)> CreateProductAsync(ProductCreateDto dto) =>
        _api.PostAsync<ProductDetailDto>("/api/products", dto);

    public Task<(ProductDetailDto? Data, string? Error, int Status)> UpdateProductAsync(int id, ProductCreateDto dto) =>
        _api.PutAsync<ProductDetailDto>($"/api/products/{id}", dto);

    public Task<(bool Ok, string? Error, int Status)> DeleteProductAsync(int id) =>
        _api.DeleteAsync($"/api/products/{id}");

    public async Task<(int? Count, string? Error)> ImportProductsAsync(Stream file, string fileName)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(file), "file", fileName);
        var (data, error, _) = await _api.PostMultipartAsync<int?>("/api/products/import", content);
        return (data, error);
    }

    public Task<(byte[]? Bytes, string? Error)> ExportProductsAsync() =>
        _api.GetBytesAsync("/api/products/export");

    // Categories / Brands / Warehouses
    public Task<(List<CategoryDto>? Data, string? Error, int Status)> GetCategoriesAsync() =>
        _api.GetAsync<List<CategoryDto>>("/api/categories");

    public Task<(CategoryDto? Data, string? Error, int Status)> CreateCategoryAsync(CategoryDto dto) =>
        _api.PostAsync<CategoryDto>("/api/categories", dto);

    public Task<(CategoryDto? Data, string? Error, int Status)> UpdateCategoryAsync(int id, CategoryDto dto) =>
        _api.PutAsync<CategoryDto>($"/api/categories/{id}", dto);

    public Task<(bool Ok, string? Error, int Status)> DeleteCategoryAsync(int id) =>
        _api.DeleteAsync($"/api/categories/{id}");

    public Task<(List<BrandDto>? Data, string? Error, int Status)> GetBrandsAsync() =>
        _api.GetAsync<List<BrandDto>>("/api/brands");

    public Task<(BrandDto? Data, string? Error, int Status)> CreateBrandAsync(BrandDto dto) =>
        _api.PostAsync<BrandDto>("/api/brands", dto);

    public Task<(BrandDto? Data, string? Error, int Status)> UpdateBrandAsync(int id, BrandDto dto) =>
        _api.PutAsync<BrandDto>($"/api/brands/{id}", dto);

    public Task<(bool Ok, string? Error, int Status)> DeleteBrandAsync(int id) =>
        _api.DeleteAsync($"/api/brands/{id}");

    public Task<(List<WarehouseDto>? Data, string? Error, int Status)> GetWarehousesAsync() =>
        _api.GetAsync<List<WarehouseDto>>("/api/warehouses");

    public Task<(WarehouseDto? Data, string? Error, int Status)> CreateWarehouseAsync(WarehouseDto dto) =>
        _api.PostAsync<WarehouseDto>("/api/warehouses", dto);

    public Task<(WarehouseDto? Data, string? Error, int Status)> UpdateWarehouseAsync(int id, WarehouseDto dto) =>
        _api.PutAsync<WarehouseDto>($"/api/warehouses/{id}", dto);

    public Task<(bool Ok, string? Error, int Status)> DeleteWarehouseAsync(int id) =>
        _api.DeleteAsync($"/api/warehouses/{id}");

    // Inventory
    public Task<(PagedResult<InventoryItemDto>? Data, string? Error, int Status)> GetInventoryAsync(int page = 1, string? search = null) =>
        _api.GetAsync<PagedResult<InventoryItemDto>>($"/api/inventory?page={page}&pageSize=50&search={Uri.EscapeDataString(search ?? "")}");

    public Task<(PagedResult<StockMovementDto>? Data, string? Error, int Status)> GetMovementsAsync(int page = 1) =>
        _api.GetAsync<PagedResult<StockMovementDto>>($"/api/inventory/movements?page={page}&pageSize=50");

    public Task<(bool Ok, string? Error, int Status)> AdjustStockAsync(StockAdjustmentDto dto) =>
        _api.PostAsync("/api/inventory/adjust", dto);

    public Task<(bool Ok, string? Error, int Status)> ReceiveStockAsync(ReceiveStockRequest dto) =>
        _api.PostAsync("/api/inventory/receive", dto);

    public Task<(bool Ok, string? Error, int Status)> DeductStockAsync(DeductStockRequest dto) =>
        _api.PostAsync("/api/inventory/deduct", dto);

    public Task<(bool Ok, string? Error, int Status)> ReturnStockAsync(DeductStockRequest dto) =>
        _api.PostAsync("/api/inventory/return-stock", dto);

    public Task<(List<LowStockAlertDto>? Data, string? Error, int Status)> GetLowStockAsync() =>
        _api.GetAsync<List<LowStockAlertDto>>("/api/inventory/alerts/low-stock");

    public Task<(List<LowStockAlertDto>? Data, string? Error, int Status)> GetOverstockAsync() =>
        _api.GetAsync<List<LowStockAlertDto>>("/api/inventory/alerts/overstock");

    public Task<(InventoryValueResponse? Data, string? Error, int Status)> GetInventoryValueAsync() =>
        _api.GetAsync<InventoryValueResponse>("/api/inventory/value");

    // Serials
    public Task<(PagedResult<SerialNumberDto>? Data, string? Error, int Status)> GetSerialsAsync(QuerySpec q) =>
        _api.GetAsync<PagedResult<SerialNumberDto>>($"/api/serial-numbers{ApiClient.ToQuery(q)}");

    public Task<(List<SerialNumberHistoryDto>? Data, string? Error, int Status)> GetSerialHistoryAsync(int id) =>
        _api.GetAsync<List<SerialNumberHistoryDto>>($"/api/serial-numbers/{id}/history");

    public Task<(bool Ok, string? Error, int Status)> RegisterSerialAsync(RegisterSerialRequest dto) =>
        _api.PostAsync("/api/serial-numbers", dto);

    // Partners
    public Task<(PagedResult<SupplierDto>? Data, string? Error, int Status)> GetSuppliersAsync(QuerySpec q) =>
        _api.GetAsync<PagedResult<SupplierDto>>($"/api/suppliers{ApiClient.ToQuery(q)}");

    public Task<(SupplierDto? Data, string? Error, int Status)> GetSupplierAsync(int id) =>
        _api.GetAsync<SupplierDto>($"/api/suppliers/{id}");

    public Task<(SupplierDto? Data, string? Error, int Status)> CreateSupplierAsync(SupplierDto dto) =>
        _api.PostAsync<SupplierDto>("/api/suppliers", dto);

    public Task<(SupplierDto? Data, string? Error, int Status)> UpdateSupplierAsync(int id, SupplierDto dto) =>
        _api.PutAsync<SupplierDto>($"/api/suppliers/{id}", dto);

    public Task<(bool Ok, string? Error, int Status)> DeleteSupplierAsync(int id) =>
        _api.DeleteAsync($"/api/suppliers/{id}");

    public Task<(List<LedgerEntryDto>? Data, string? Error, int Status)> GetSupplierLedgerAsync(int id) =>
        _api.GetAsync<List<LedgerEntryDto>>($"/api/suppliers/{id}/ledger");

    public Task<(PagedResult<CustomerDto>? Data, string? Error, int Status)> GetCustomersAsync(QuerySpec q) =>
        _api.GetAsync<PagedResult<CustomerDto>>($"/api/customers{ApiClient.ToQuery(q)}");

    public Task<(CustomerDto? Data, string? Error, int Status)> GetCustomerAsync(int id) =>
        _api.GetAsync<CustomerDto>($"/api/customers/{id}");

    public Task<(CustomerDto? Data, string? Error, int Status)> CreateCustomerAsync(CustomerDto dto) =>
        _api.PostAsync<CustomerDto>("/api/customers", dto);

    public Task<(CustomerDto? Data, string? Error, int Status)> UpdateCustomerAsync(int id, CustomerDto dto) =>
        _api.PutAsync<CustomerDto>($"/api/customers/{id}", dto);

    public Task<(bool Ok, string? Error, int Status)> DeleteCustomerAsync(int id) =>
        _api.DeleteAsync($"/api/customers/{id}");

    public Task<(List<LedgerEntryDto>? Data, string? Error, int Status)> GetCustomerLedgerAsync(int id) =>
        _api.GetAsync<List<LedgerEntryDto>>($"/api/customers/{id}/ledger");

    // Purchases / Sales / POS / Returns / Transfers
    public Task<(PagedResult<PurchaseOrderListDto>? Data, string? Error, int Status)> GetPurchaseOrdersAsync(QuerySpec q) =>
        _api.GetAsync<PagedResult<PurchaseOrderListDto>>($"/api/purchase-orders{ApiClient.ToQuery(q)}");

    public Task<(PurchaseOrderDetailDto? Data, string? Error, int Status)> GetPurchaseOrderAsync(int id) =>
        _api.GetAsync<PurchaseOrderDetailDto>($"/api/purchase-orders/{id}");

    public Task<(PurchaseOrderDetailDto? Data, string? Error, int Status)> CreatePurchaseOrderAsync(PurchaseOrderCreateDto dto) =>
        _api.PostAsync<PurchaseOrderDetailDto>("/api/purchase-orders", dto);

    public Task<(PurchaseOrderDetailDto? Data, string? Error, int Status)> UpdatePurchaseOrderAsync(int id, PurchaseOrderCreateDto dto) =>
        _api.PutAsync<PurchaseOrderDetailDto>($"/api/purchase-orders/{id}", dto);

    public Task<(bool Ok, string? Error, int Status)> ApprovePurchaseOrderAsync(int id) =>
        _api.PostAsync($"/api/purchase-orders/{id}/approve", new { });

    public Task<(bool Ok, string? Error, int Status)> CancelPurchaseOrderAsync(int id) =>
        _api.PostAsync($"/api/purchase-orders/{id}/cancel", new { });

    public Task<(bool Ok, string? Error, int Status)> ReceivePurchaseOrderAsync(int id, ReceivePurchaseOrderDto dto) =>
        _api.PostAsync($"/api/purchase-orders/{id}/receive", dto);

    public Task<(PagedResult<SalesInvoiceListDto>? Data, string? Error, int Status)> GetInvoicesAsync(QuerySpec q) =>
        _api.GetAsync<PagedResult<SalesInvoiceListDto>>($"/api/sales/invoices{ApiClient.ToQuery(q)}");

    public Task<(SalesInvoiceDetailDto? Data, string? Error, int Status)> GetInvoiceAsync(int id) =>
        _api.GetAsync<SalesInvoiceDetailDto>($"/api/sales/invoices/{id}");

    public Task<(PagedResult<SalesOrderListDto>? Data, string? Error, int Status)> GetSalesOrdersAsync(QuerySpec q) =>
        _api.GetAsync<PagedResult<SalesOrderListDto>>($"/api/sales/orders{ApiClient.ToQuery(q)}");

    public Task<(List<PosProductDto>? Data, string? Error, int Status)> GetPosProductsAsync(string? search = null) =>
        _api.GetAsync<List<PosProductDto>>($"/api/pos/products?search={Uri.EscapeDataString(search ?? "")}");

    public Task<(PosCheckoutResultDto? Data, string? Error, int Status)> CheckoutAsync(PosCheckoutDto dto) =>
        _api.PostAsync<PosCheckoutResultDto>("/api/pos/checkout", dto);

    public Task<(string? Data, string? Error, int Status)> GetReceiptHtmlAsync(int invoiceId) =>
        _api.GetTextAsync($"/api/pos/receipts/{invoiceId}");

    public Task<(HeldSaleDto? Data, string? Error, int Status)> HoldSaleAsync(object dto) =>
        _api.PostAsync<HeldSaleDto>("/api/pos/holds", dto);

    public Task<(List<HeldSaleDto>? Data, string? Error, int Status)> ListHeldSalesAsync() =>
        _api.GetAsync<List<HeldSaleDto>>("/api/pos/holds");

    public Task<(HeldSaleDto? Data, string? Error, int Status)> RecallHeldSaleAsync(int id) =>
        _api.PostAsync<HeldSaleDto>($"/api/pos/holds/{id}/recall", new { });

    public Task<(bool Ok, string? Error, int Status)> DiscardHeldSaleAsync(int id) =>
        _api.PostAsync($"/api/pos/holds/{id}/discard", new { });

    public Task<(CashierShiftDto? Data, string? Error, int Status)> GetOpenShiftAsync() =>
        _api.GetAsync<CashierShiftDto>("/api/pos/shifts/current");

    public Task<(CashierShiftDto? Data, string? Error, int Status)> OpenShiftAsync(decimal openingFloat, int? warehouseId = null) =>
        _api.PostAsync<CashierShiftDto>("/api/pos/shifts/open", new { OpeningFloat = openingFloat, WarehouseId = warehouseId });

    public Task<(CashierShiftDto? Data, string? Error, int Status)> CloseShiftAsync(int id, decimal closingFloat) =>
        _api.PostAsync<CashierShiftDto>($"/api/pos/shifts/{id}/close", new { ClosingFloat = closingFloat });

    public Task<(ShiftZReportDto? Data, string? Error, int Status)> GetZReportAsync(int shiftId) =>
        _api.GetAsync<ShiftZReportDto>($"/api/pos/shifts/{shiftId}/z-report");

    public Task<(PagedResult<SalesReturnDto>? Data, string? Error, int Status)> GetSalesReturnsAsync(QuerySpec q) =>
        _api.GetAsync<PagedResult<SalesReturnDto>>($"/api/returns/sales{ApiClient.ToQuery(q)}");

    public Task<(SalesReturnDto? Data, string? Error, int Status)> CreateSalesReturnAsync(SalesReturnCreateDto dto) =>
        _api.PostAsync<SalesReturnDto>("/api/returns/sales", dto);

    public Task<(bool Ok, string? Error, int Status)> CreatePurchaseReturnAsync(PurchaseReturnCreateDto dto) =>
        _api.PostAsync("/api/returns/purchases", dto);

    public Task<(PagedResult<TransferListDto>? Data, string? Error, int Status)> GetTransfersAsync(QuerySpec q) =>
        _api.GetAsync<PagedResult<TransferListDto>>($"/api/transfers{ApiClient.ToQuery(q)}");

    public Task<(TransferDetailDto? Data, string? Error, int Status)> GetTransferAsync(int id) =>
        _api.GetAsync<TransferDetailDto>($"/api/transfers/{id}");

    public Task<(TransferDetailDto? Data, string? Error, int Status)> CreateTransferAsync(TransferCreateDto dto) =>
        _api.PostAsync<TransferDetailDto>("/api/transfers", dto);

    public Task<(bool Ok, string? Error, int Status)> ApproveTransferAsync(int id) =>
        _api.PostAsync($"/api/transfers/{id}/approve", new { });

    public Task<(bool Ok, string? Error, int Status)> CompleteTransferAsync(int id) =>
        _api.PostAsync($"/api/transfers/{id}/complete", new { });

    // Admin
    public Task<(byte[]? Bytes, string? Error)> ExportReportAsync(string kind, string query) =>
        _api.GetBytesAsync($"/api/reports/{kind}{query}");

    public Task<(List<NotificationDto>? Data, string? Error, int Status)> GetNotificationsAsync(bool unreadOnly = false) =>
        _api.GetAsync<List<NotificationDto>>($"/api/notifications?unreadOnly={unreadOnly}");

    public Task<(UnreadCountResponse? Data, string? Error, int Status)> GetUnreadCountAsync() =>
        _api.GetAsync<UnreadCountResponse>("/api/notifications/unread-count");

    public Task<(bool Ok, string? Error, int Status)> MarkNotificationReadAsync(int id) =>
        _api.PostAsync($"/api/notifications/{id}/read", new { });

    public Task<(bool Ok, string? Error, int Status)> CreateNotificationAsync(object dto) =>
        _api.PostAsync("/api/notifications", dto);

    public Task<(List<UserDto>? Data, string? Error, int Status)> GetUsersAsync() =>
        _api.GetAsync<List<UserDto>>("/api/users");

    public Task<(UserDto? Data, string? Error, int Status)> CreateUserAsync(UserCreateDto dto) =>
        _api.PostAsync<UserDto>("/api/users", dto);

    public Task<(UserDto? Data, string? Error, int Status)> UpdateUserAsync(int id, UserCreateDto dto) =>
        _api.PutAsync<UserDto>($"/api/users/{id}", dto);

    public Task<(bool Ok, string? Error, int Status)> DeleteUserAsync(int id) =>
        _api.DeleteAsync($"/api/users/{id}");

    public Task<(List<RoleDto>? Data, string? Error, int Status)> GetRolesAsync() =>
        _api.GetAsync<List<RoleDto>>("/api/roles");

    public Task<(PagedResult<AuditLogDto>? Data, string? Error, int Status)> GetAuditLogsAsync(QuerySpec q) =>
        _api.GetAsync<PagedResult<AuditLogDto>>($"/api/audit-logs{ApiClient.ToQuery(q)}");

    public Task<(CompanySettingsDto? Data, string? Error, int Status)> GetSettingsAsync() =>
        _api.GetAsync<CompanySettingsDto>("/api/settings");

    public Task<(bool Ok, string? Error, int Status)> UpdateSettingsAsync(CompanySettingsDto dto) =>
        _api.PutAsync("/api/settings", dto);

    public Task<(List<BackupHistoryDto>? Data, string? Error, int Status)> GetBackupsAsync() =>
        _api.GetAsync<List<BackupHistoryDto>>("/api/backups");

    public Task<(string? Data, string? Error, int Status)> CreateBackupAsync(bool isAutomatic = false) =>
        _api.PostAsync<string>("/api/backups", new { isAutomatic });

    public async Task<(bool Ok, string? Error)> RestoreBackupAsync(Stream file, string fileName)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(file), "file", fileName);
        var (ok, error, _) = await _api.PostMultipartAsync("/api/backups/restore", content);
        return (ok, error);
    }

    public Task<(object? Data, string? Error, int Status)> PostFbrInvoiceAsync(object dto) =>
        _api.PostAsync<object>("/api/fbr/invoices", dto);

    public Task<(byte[]? Bytes, string? Error)> GetBarcodeAsync(string code) =>
        _api.GetBytesAsync($"/api/barcodes/{Uri.EscapeDataString(code)}");

    // Finance / Platform
    public Task<(List<CompanyDto>? Data, string? Error, int Status)> GetCompaniesAsync() =>
        _api.GetAsync<List<CompanyDto>>("/api/v1/finance/companies");

    public Task<(List<BranchDto>? Data, string? Error, int Status)> GetBranchesAsync(int companyId) =>
        _api.GetAsync<List<BranchDto>>($"/api/v1/finance/companies/{companyId}/branches");

    public Task<(List<GlAccountDto>? Data, string? Error, int Status)> GetCoaAsync() =>
        _api.GetAsync<List<GlAccountDto>>("/api/v1/finance/coa");

    public Task<(GlAccountDto? Data, string? Error, int Status)> CreateGlAccountAsync(object body) =>
        _api.PostAsync<GlAccountDto>("/api/v1/finance/coa", body);

    public Task<(List<AccountingPeriodDto>? Data, string? Error, int Status)> GetPeriodsAsync() =>
        _api.GetAsync<List<AccountingPeriodDto>>("/api/v1/finance/periods");

    public Task<(bool Ok, string? Error, int Status)> ClosePeriodAsync(int id) =>
        _api.PostAsync($"/api/v1/finance/periods/{id}/close", null);

    public Task<(bool Ok, string? Error, int Status)> ReopenPeriodAsync(int id) =>
        _api.PostAsync($"/api/v1/finance/periods/{id}/reopen", null);

    public Task<(PagedResult<JournalDto>? Data, string? Error, int Status)> GetJournalsAsync(int page = 1, int pageSize = 50) =>
        _api.GetAsync<PagedResult<JournalDto>>($"/api/v1/finance/journals?page={page}&pageSize={pageSize}");

    public Task<(JournalDto? Data, string? Error, int Status)> CreateJournalAsync(object body) =>
        _api.PostAsync<JournalDto>("/api/v1/finance/journals", body);

    public Task<(bool Ok, string? Error, int Status)> PostJournalAsync(int id) =>
        _api.PostAsync($"/api/v1/finance/journals/{id}/post", null);

    public Task<(TrialBalanceReportDto? Data, string? Error, int Status)> GetTrialBalanceAsync(DateTime? asOf = null) =>
        _api.GetAsync<TrialBalanceReportDto>($"/api/v1/enterprise/reports/trial-balance{(asOf is null ? "" : $"?asOf={asOf:yyyy-MM-dd}")}");

    public Task<(ProfitAndLossReportDto? Data, string? Error, int Status)> GetProfitLossAsync(DateTime from, DateTime to) =>
        _api.GetAsync<ProfitAndLossReportDto>($"/api/v1/enterprise/reports/profit-loss?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

    public Task<(BalanceSheetReportDto? Data, string? Error, int Status)> GetBalanceSheetAsync(DateTime? asOf = null) =>
        _api.GetAsync<BalanceSheetReportDto>($"/api/v1/enterprise/reports/balance-sheet{(asOf is null ? "" : $"?asOf={asOf:yyyy-MM-dd}")}");

    public Task<(bool Ok, string? Error, int Status)> RetryFbrAsync(int invoiceId) =>
        _api.PostAsync($"/api/v1/enterprise/fbr/retry/{invoiceId}", null);

    // Enterprise — inventory / P2P / O2C
    private const string Ent = "/api/v1/enterprise";

    public Task<(List<GoodsReceiptNoteDto>? Data, string? Error, int Status)> GetGrnsAsync() =>
        _api.GetAsync<List<GoodsReceiptNoteDto>>($"{Ent}/grn");

    public Task<(GoodsReceiptNoteDto? Data, string? Error, int Status)> CreateGrnAsync(CreateGrnRequest dto) =>
        _api.PostAsync<GoodsReceiptNoteDto>($"{Ent}/grn", dto);

    public Task<(GoodsReceiptNoteDto? Data, string? Error, int Status)> PostGrnAsync(int id) =>
        _api.PostAsync<GoodsReceiptNoteDto>($"{Ent}/grn/{id}/post", null);

    public Task<(GoodsReceiptNoteDto? Data, string? Error, int Status)> ReleaseQcAsync(int id) =>
        _api.PostAsync<GoodsReceiptNoteDto>($"{Ent}/grn/{id}/release-qc", null);

    public Task<(List<PurchaseRequisitionDto>? Data, string? Error, int Status)> GetRequisitionsAsync() =>
        _api.GetAsync<List<PurchaseRequisitionDto>>("/api/v1/purchase-requisitions");

    public Task<(PurchaseRequisitionDto? Data, string? Error, int Status)> CreateRequisitionAsync(PurchaseRequisitionCreateDto dto) =>
        _api.PostAsync<PurchaseRequisitionDto>("/api/v1/purchase-requisitions", dto);

    public Task<(PurchaseRequisitionDto? Data, string? Error, int Status)> SubmitRequisitionAsync(int id) =>
        _api.PostAsync<PurchaseRequisitionDto>($"/api/v1/purchase-requisitions/{id}/submit", null);

    public Task<(PurchaseRequisitionDto? Data, string? Error, int Status)> ApproveRequisitionAsync(int id) =>
        _api.PostAsync<PurchaseRequisitionDto>($"/api/v1/purchase-requisitions/{id}/approve", null);

    public Task<(PurchaseRequisitionDto? Data, string? Error, int Status)> RejectRequisitionAsync(int id, string reason) =>
        _api.PostAsync<PurchaseRequisitionDto>($"/api/v1/purchase-requisitions/{id}/reject", new { Reason = reason });

    public Task<(PurchaseOrderDetailDto? Data, string? Error, int Status)> ConvertRequisitionToPoAsync(int id) =>
        _api.PostAsync<PurchaseOrderDetailDto>($"/api/v1/purchase-requisitions/{id}/convert-to-po", null);

    public Task<(List<ReorderSuggestionDto>? Data, string? Error, int Status)> GetReorderSuggestionsAsync(int? warehouseId = null) =>
        _api.GetAsync<List<ReorderSuggestionDto>>($"/api/v1/reorder/suggestions{(warehouseId is int w ? $"?warehouseId={w}" : "")}");

    public Task<(PurchaseRequisitionDto? Data, string? Error, int Status)> CreateReorderPrAsync(CreateReorderPrRequest dto) =>
        _api.PostAsync<PurchaseRequisitionDto>("/api/v1/reorder/create-pr", dto);

    public Task<(List<PurchaseInvoiceDto>? Data, string? Error, int Status)> GetApInvoicesAsync() =>
        _api.GetAsync<List<PurchaseInvoiceDto>>($"{Ent}/ap-invoices");

    public Task<(PurchaseInvoiceDto? Data, string? Error, int Status)> CreateApInvoiceAsync(CreatePurchaseInvoiceRequest dto) =>
        _api.PostAsync<PurchaseInvoiceDto>($"{Ent}/ap-invoices", dto);

    public Task<(ThreeWayMatchResultDto? Data, string? Error, int Status)> MatchApInvoiceAsync(int id) =>
        _api.PostAsync<ThreeWayMatchResultDto>($"{Ent}/ap-invoices/{id}/match", null);

    public Task<(PurchaseInvoiceDto? Data, string? Error, int Status)> PostApInvoiceAsync(int id) =>
        _api.PostAsync<PurchaseInvoiceDto>($"{Ent}/ap-invoices/{id}/post", null);

    public Task<(List<SalesQuotationDto>? Data, string? Error, int Status)> GetQuotationsAsync() =>
        _api.GetAsync<List<SalesQuotationDto>>($"{Ent}/quotations");

    public Task<(SalesQuotationDto? Data, string? Error, int Status)> CreateQuotationAsync(CreateQuotationRequest dto) =>
        _api.PostAsync<SalesQuotationDto>($"{Ent}/quotations", dto);

    public Task<(ConvertQuotationResultDto? Data, string? Error, int Status)> ConvertQuotationAsync(int id) =>
        _api.PostAsync<ConvertQuotationResultDto>($"{Ent}/quotations/{id}/convert", null);

    public Task<(List<DeliveryNoteDto>? Data, string? Error, int Status)> GetDeliveriesAsync() =>
        _api.GetAsync<List<DeliveryNoteDto>>($"{Ent}/deliveries");

    public Task<(DeliveryNoteDto? Data, string? Error, int Status)> CreateDeliveryAsync(CreateDeliveryNoteRequest dto) =>
        _api.PostAsync<DeliveryNoteDto>($"{Ent}/deliveries", dto);

    public Task<(DeliveryNoteDto? Data, string? Error, int Status)> ShipDeliveryAsync(int id) =>
        _api.PostAsync<DeliveryNoteDto>($"{Ent}/deliveries/{id}/ship", null);

    public Task<(List<AccountMappingDto>? Data, string? Error, int Status)> GetAccountMappingsAsync() =>
        _api.GetAsync<List<AccountMappingDto>>($"{Ent}/account-mappings");

    public Task<(AccountMappingDto? Data, string? Error, int Status)> CreateAccountMappingAsync(CreateAccountMappingRequest dto) =>
        _api.PostAsync<AccountMappingDto>($"{Ent}/account-mappings", dto);

    public Task<(AccountMappingDto? Data, string? Error, int Status)> UpdateAccountMappingAsync(int id, UpdateAccountMappingRequest dto) =>
        _api.PutAsync<AccountMappingDto>($"{Ent}/account-mappings/{id}", dto);

    public Task<(bool Ok, string? Error, int Status)> DeleteAccountMappingAsync(int id) =>
        _api.DeleteAsync($"{Ent}/account-mappings/{id}");

    public Task<(List<StockReservationDto>? Data, string? Error, int Status)> GetReservationsAsync() =>
        _api.GetAsync<List<StockReservationDto>>($"{Ent}/reservations");

    public Task<(StockReservationDto? Data, string? Error, int Status)> CreateReservationAsync(ReserveStockRequest dto) =>
        _api.PostAsync<StockReservationDto>($"{Ent}/reservations", dto);

    public Task<(bool Ok, string? Error, int Status)> ReleaseReservationAsync(int id) =>
        _api.PostAsync($"{Ent}/reservations/{id}/release", null);

    public Task<(List<CycleCountDto>? Data, string? Error, int Status)> GetCycleCountsAsync() =>
        _api.GetAsync<List<CycleCountDto>>($"{Ent}/cycle-counts");

    public Task<(CycleCountDto? Data, string? Error, int Status)> CreateCycleCountAsync(CreateCycleCountRequest dto) =>
        _api.PostAsync<CycleCountDto>($"{Ent}/cycle-counts", dto);

    public Task<(CycleCountDto? Data, string? Error, int Status)> CompleteCycleCountAsync(int id) =>
        _api.PostAsync<CycleCountDto>($"{Ent}/cycle-counts/{id}/complete", null);

    public Task<(List<ProductKitDto>? Data, string? Error, int Status)> GetKitsAsync() =>
        _api.GetAsync<List<ProductKitDto>>($"{Ent}/kits");

    public Task<(ProductKitDto? Data, string? Error, int Status)> UpsertKitAsync(UpsertKitRequest dto) =>
        _api.PostAsync<ProductKitDto>($"{Ent}/kits", dto);

    public Task<(List<PriceListDto>? Data, string? Error, int Status)> GetPriceListsAsync() =>
        _api.GetAsync<List<PriceListDto>>($"{Ent}/price-lists");

    public Task<(PriceListDto? Data, string? Error, int Status)> CreatePriceListAsync(CreatePriceListRequest dto) =>
        _api.PostAsync<PriceListDto>($"{Ent}/price-lists", dto);

    public Task<(PriceListDto? Data, string? Error, int Status)> UpdatePriceListItemsAsync(int id, UpdatePriceListItemsRequest dto) =>
        _api.PutAsync<PriceListDto>($"{Ent}/price-lists/{id}/items", dto);

    public Task<(List<FbrSubmissionDto>? Data, string? Error, int Status)> GetFbrSubmissionsAsync() =>
        _api.GetAsync<List<FbrSubmissionDto>>($"{Ent}/fbr/submissions");

    public Task<(PartnerAgingReportDto? Data, string? Error, int Status)> GetCustomerAgingAsync(DateTime? asOf = null) =>
        _api.GetAsync<PartnerAgingReportDto>($"{Ent}/aging/customers{(asOf is null ? "" : $"?asOf={asOf:yyyy-MM-dd}")}");

    public Task<(PartnerAgingReportDto? Data, string? Error, int Status)> GetSupplierAgingAsync(DateTime? asOf = null) =>
        _api.GetAsync<PartnerAgingReportDto>($"{Ent}/aging/suppliers{(asOf is null ? "" : $"?asOf={asOf:yyyy-MM-dd}")}");

    public Task<(int Data, string? Error, int Status)> PostCustomerReceiptAsync(PostCustomerReceiptRequest dto) =>
        _api.PostAsync<int>($"{Ent}/payments/customer-receipt", dto);

    public Task<(int Data, string? Error, int Status)> PostSupplierPaymentAsync(PostSupplierPaymentRequest dto) =>
        _api.PostAsync<int>($"{Ent}/payments/supplier-payment", dto);
}
