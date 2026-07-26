using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs;
using CarAutoParts.Application.DTOs.Analytics;
using CarAutoParts.Application.DTOs.Auth;
using CarAutoParts.Application.DTOs.Dashboard;
using CarAutoParts.Application.DTOs.Fbr;
using CarAutoParts.Application.DTOs.Inventory;
using CarAutoParts.Application.DTOs.Partners;
using CarAutoParts.Application.DTOs.Pos;
using CarAutoParts.Application.DTOs.Products;
using CarAutoParts.Application.DTOs.Purchases;
using CarAutoParts.Application.DTOs.Sales;
using CarAutoParts.Application.DTOs.Settings;
using CarAutoParts.Application.DTOs.System;
using CarAutoParts.Application.DTOs.Transfers;
using CarAutoParts.Domain.Enums;

namespace CarAutoParts.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default);
}

public interface IProductService
{
    Task<PagedResult<ProductListDto>> GetProductsAsync(ProductQueryDto query, CancellationToken ct = default);
    Task<ProductDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Result<ProductDetailDto>> CreateAsync(ProductCreateDto dto, CancellationToken ct = default);
    Task<Result<ProductDetailDto>> UpdateAsync(int id, ProductCreateDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
    Task<Result<int>> ImportFromExcelAsync(Stream stream, CancellationToken ct = default);
    Task<byte[]> ExportToExcelAsync(ProductQueryDto query, CancellationToken ct = default);
}

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetTreeAsync(CancellationToken ct = default);
    Task<Result<CategoryDto>> CreateAsync(CategoryDto dto, CancellationToken ct = default);
    Task<Result<CategoryDto>> UpdateAsync(int id, CategoryDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}

public interface IBrandService
{
    Task<IReadOnlyList<BrandDto>> GetAllAsync(CancellationToken ct = default);
    Task<Result<BrandDto>> CreateAsync(BrandDto dto, CancellationToken ct = default);
    Task<Result<BrandDto>> UpdateAsync(int id, BrandDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}

public interface IWarehouseService
{
    Task<IReadOnlyList<WarehouseDto>> GetAllAsync(CancellationToken ct = default);
    Task<Result<WarehouseDto>> CreateAsync(WarehouseDto dto, CancellationToken ct = default);
    Task<Result<WarehouseDto>> UpdateAsync(int id, WarehouseDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}

public interface IInventoryService
{
    Task<PagedResult<InventoryItemDto>> GetInventoryAsync(InventoryQueryDto query, CancellationToken ct = default);
    Task<PagedResult<StockMovementDto>> GetMovementsAsync(StockMovementQueryDto query, CancellationToken ct = default);
    Task<Result> AdjustStockAsync(StockAdjustmentDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<LowStockAlertDto>> GetLowStockAlertsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LowStockAlertDto>> GetOverstockAlertsAsync(CancellationToken ct = default);
    Task<Result> ReceiveStockAsync(int productId, int warehouseId, decimal quantity, decimal unitCost, string? batchNumber, CancellationToken ct = default);
    Task<Result> DeductStockAsync(int productId, int warehouseId, decimal quantity, string referenceType, int referenceId, CancellationToken ct = default);
    Task<Result> ReturnStockAsync(int productId, int warehouseId, decimal quantity, string referenceType, int referenceId, CancellationToken ct = default);
    Task<decimal> GetInventoryValueAsync(CancellationToken ct = default);
}

public interface ISupplierService
{
    Task<PagedResult<SupplierDto>> GetSuppliersAsync(QuerySpec query, CancellationToken ct = default);
    Task<SupplierDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Result<SupplierDto>> CreateAsync(SupplierDto dto, CancellationToken ct = default);
    Task<Result<SupplierDto>> UpdateAsync(int id, SupplierDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<SupplierLedgerEntryDto>> GetLedgerAsync(int supplierId, CancellationToken ct = default);
}

public interface ICustomerService
{
    Task<PagedResult<CustomerDto>> GetCustomersAsync(QuerySpec query, CancellationToken ct = default);
    Task<CustomerDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Result<CustomerDto>> CreateAsync(CustomerDto dto, CancellationToken ct = default);
    Task<Result<CustomerDto>> UpdateAsync(int id, CustomerDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerLedgerEntryDto>> GetLedgerAsync(int customerId, CancellationToken ct = default);
}

public interface IPurchaseOrderService
{
    Task<PagedResult<PurchaseOrderListDto>> GetOrdersAsync(QuerySpec query, CancellationToken ct = default);
    Task<PurchaseOrderDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Result<PurchaseOrderDetailDto>> CreateAsync(PurchaseOrderCreateDto dto, CancellationToken ct = default);
    Task<Result<PurchaseOrderDetailDto>> UpdateAsync(int id, PurchaseOrderCreateDto dto, CancellationToken ct = default);
    Task<Result> ApproveAsync(int id, CancellationToken ct = default);
    Task<Result> CancelAsync(int id, CancellationToken ct = default);
    Task<Result> ReceiveAsync(int id, ReceivePurchaseOrderDto dto, CancellationToken ct = default);
}

public interface ISalesService
{
    Task<PagedResult<SalesInvoiceListDto>> GetInvoicesAsync(QuerySpec query, CancellationToken ct = default);
    Task<SalesInvoiceDetailDto?> GetInvoiceByIdAsync(int id, CancellationToken ct = default);
    Task<PagedResult<SalesOrderListDto>> GetOrdersAsync(QuerySpec query, CancellationToken ct = default);
}

public interface IPosCheckoutService
{
    Task<PosCheckoutResultDto> CheckoutAsync(PosCheckoutDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<PosProductDto>> GetPosProductsAsync(string? search, CancellationToken ct = default);
    Task<string> GetReceiptHtmlAsync(int salesInvoiceId, CancellationToken ct = default);
}

public interface IPosFloorService
{
    Task<Result<HeldSaleDto>> HoldAsync(HoldSaleRequestDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<HeldSaleDto>> ListHeldAsync(CancellationToken ct = default);
    Task<Result<HeldSaleDto>> RecallAsync(int heldSaleId, CancellationToken ct = default);
    Task<Result> DiscardHeldAsync(int heldSaleId, CancellationToken ct = default);
    Task<Result<CashierShiftDto>> OpenShiftAsync(OpenShiftRequestDto dto, CancellationToken ct = default);
    Task<Result<CashierShiftDto>> CloseShiftAsync(int shiftId, CloseShiftRequestDto dto, CancellationToken ct = default);
    Task<CashierShiftDto?> GetOpenShiftAsync(CancellationToken ct = default);
    Task<Result<ShiftZReportDto>> GetZReportAsync(int shiftId, CancellationToken ct = default);
}

public interface IReturnService
{
    Task<Result<SalesReturnDto>> CreateSalesReturnAsync(SalesReturnCreateDto dto, CancellationToken ct = default);
    Task<Result<PurchaseReturnDto>> CreatePurchaseReturnAsync(PurchaseReturnCreateDto dto, CancellationToken ct = default);
    Task<PagedResult<SalesReturnDto>> GetSalesReturnsAsync(QuerySpec query, CancellationToken ct = default);
}

public interface ITransferService
{
    Task<PagedResult<TransferListDto>> GetTransfersAsync(QuerySpec query, CancellationToken ct = default);
    Task<TransferDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Result<TransferDetailDto>> CreateAsync(TransferCreateDto dto, CancellationToken ct = default);
    Task<Result> ApproveAsync(int id, CancellationToken ct = default);
    Task<Result> CompleteAsync(int id, CancellationToken ct = default);
}

public interface IBarcodeService
{
    byte[] GenerateBarcodeImage(string code, int width = 300, int height = 100);
    Task PrintBarcodeAsync(string code, string label, CancellationToken ct = default);
}

public interface ISerialNumberService
{
    Task<PagedResult<SerialNumberDto>> GetSerialNumbersAsync(QuerySpec query, CancellationToken ct = default);
    Task<IReadOnlyList<SerialNumberHistoryDto>> GetHistoryAsync(int serialNumberId, CancellationToken ct = default);
    Task<Result> RegisterSerialAsync(int productId, string serial, int warehouseId, CancellationToken ct = default);
}

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(bool unreadOnly = false, CancellationToken ct = default);
    Task MarkAsReadAsync(int id, CancellationToken ct = default);
    Task CreateNotificationAsync(NotificationType type, string title, string message, string? entityType = null, int? entityId = null, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(CancellationToken ct = default);
}

public interface IReportService
{
    Task<byte[]> ExportInventoryReportAsync(string format, CancellationToken ct = default);
    Task<byte[]> ExportSalesReportAsync(DateTime from, DateTime to, string period, string format, CancellationToken ct = default);
    Task<byte[]> ExportPurchaseReportAsync(DateTime from, DateTime to, string format, CancellationToken ct = default);
    Task<byte[]> ExportProfitReportAsync(DateTime from, DateTime to, string format, CancellationToken ct = default);
}

public interface IAnalyticsService
{
    Task<AnalyticsDto> GetAnalyticsAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default);
}

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken ct = default);
    Task<Result<UserDto>> CreateAsync(UserCreateDto dto, CancellationToken ct = default);
    Task<Result<UserDto>> UpdateAsync(int id, UserCreateDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken ct = default);
}

public interface IAuditService
{
    Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(QuerySpec query, CancellationToken ct = default);
}

public interface ISettingsService
{
    Task<CompanySettingsDto> GetSettingsAsync(CancellationToken ct = default);
    Task<Result> UpdateSettingsAsync(CompanySettingsDto dto, CancellationToken ct = default);
}

public interface IBackupService
{
    Task<Result<string>> CreateBackupAsync(bool isAutomatic, CancellationToken ct = default);
    Task<Result> RestoreBackupAsync(string filePath, CancellationToken ct = default);
    Task<IReadOnlyList<BackupHistoryDto>> GetHistoryAsync(CancellationToken ct = default);
}

public interface IFbrService
{
    Task<FbrPostResultDto> PostInvoiceAsync(FbrInvoiceRequestDto request, CancellationToken ct = default);
}
