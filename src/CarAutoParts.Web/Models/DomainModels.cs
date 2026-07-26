namespace CarAutoParts.Web.Models;

public sealed class ProductListDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public decimal SalePrice { get; set; }
    public decimal CostPrice { get; set; }
    public decimal TotalStock { get; set; }
    public int MinimumStock { get; set; }
    public bool IsActive { get; set; }
}

public sealed class ProductDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string? OemNumber { get; set; }
    public string? PartNumber { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int BrandId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public string Unit { get; set; } = "PCS";
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal CostPrice { get; set; }
    public int MinimumStock { get; set; }
    public int ReorderLevel { get; set; }
    public int? MaximumStock { get; set; }
    public string? Description { get; set; }
    public string? HsCode { get; set; }
    public decimal TaxRatePercent { get; set; }
    public bool IsActive { get; set; } = true;
    public bool TrackSerialNumbers { get; set; }
    public bool TrackBatches { get; set; }
}

public sealed class ProductCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string? OemNumber { get; set; }
    public string? PartNumber { get; set; }
    public int CategoryId { get; set; }
    public int BrandId { get; set; }
    public string Unit { get; set; } = "PCS";
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public int MinimumStock { get; set; }
    public int ReorderLevel { get; set; }
    public int? MaximumStock { get; set; }
    public string? Description { get; set; }
    public string? HsCode { get; set; }
    public decimal TaxRatePercent { get; set; }
    public bool IsActive { get; set; } = true;
    public bool TrackSerialNumbers { get; set; }
    public bool TrackBatches { get; set; }
}

public sealed class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public int? ParentId { get; set; }
    public List<CategoryDto> Children { get; set; } = [];
}

public sealed class BrandDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class WarehouseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? ContactPerson { get; set; }
    public string? PhoneNumber { get; set; }
    public bool IsDefault { get; set; }
}

public sealed class InventoryItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal AvailableQuantity { get; set; }
    public decimal AverageCost { get; set; }
    public decimal StockValue { get; set; }
}

public sealed class StockMovementDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string MovementType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string? Notes { get; set; }
    public DateTime MovementDate { get; set; }
}

public sealed class StockAdjustmentDto
{
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public decimal QuantityDelta { get; set; }
    public string? Reason { get; set; }
}

public sealed class ReceiveStockRequest
{
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? BatchNumber { get; set; }
}

public sealed class DeductStockRequest
{
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public string ReferenceType { get; set; } = "Manual";
    public int ReferenceId { get; set; }
}

public sealed class LowStockAlertDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public int MinimumStock { get; set; }
    public int ReorderLevel { get; set; }
    public bool IsOverstock { get; set; }
}

public sealed class SerialNumberDto
{
    public int Id { get; set; }
    public string Serial { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? CurrentWarehouseId { get; set; }
    public string? CurrentWarehouseName { get; set; }
}

public sealed class SerialNumberHistoryDto
{
    public int Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string? Notes { get; set; }
    public DateTime ActionDate { get; set; }
}

public sealed class RegisterSerialRequest
{
    public int ProductId { get; set; }
    public string Serial { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
}

public sealed class SupplierDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public decimal Balance { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class CustomerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CustomerType { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public decimal Balance { get; set; }
    public decimal CreditLimit { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class LedgerEntryDto
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal Balance { get; set; }
}

public sealed class PurchaseOrderListDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public decimal GrandTotal { get; set; }
}

public sealed class PurchaseOrderDetailDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseOrderLineDto> Lines { get; set; } = [];
}

public sealed class PurchaseOrderLineDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public decimal Quantity { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TaxRate { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class PurchaseOrderCreateDto
{
    public int SupplierId { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseOrderLineCreateDto> Lines { get; set; } = [];
}

public sealed class PurchaseOrderLineCreateDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TaxRate { get; set; }
}

public sealed class ReceivePurchaseOrderDto
{
    public int? WarehouseId { get; set; }
    public List<ReceivePurchaseOrderLineDto> Lines { get; set; } = [];
}

public sealed class ReceivePurchaseOrderLineDto
{
    public int LineId { get; set; }
    public decimal QuantityReceived { get; set; }
    public string? BatchNumber { get; set; }
}

public sealed class SalesInvoiceListDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public DateTime InvoiceDate { get; set; }
    public decimal GrandTotal { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string? FbrInvoiceNumber { get; set; }
}

public sealed class SalesInvoiceDetailDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public DateTime InvoiceDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string? BuyerName { get; set; }
    public List<SalesInvoiceLineDto> Lines { get; set; } = [];
}

public sealed class SalesInvoiceLineDto
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class SalesOrderListDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public decimal GrandTotal { get; set; }
}

public sealed class PosProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal SalePrice { get; set; }
    public decimal TaxRatePercent { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? HsCode { get; set; }
    public decimal AvailableStock { get; set; }
    public string? OemNumber { get; set; }
    public string? PartNumber { get; set; }
}

public sealed class PosTenderDto
{
    public string Method { get; set; } = "Cash";
    public decimal Amount { get; set; }
}

public sealed class PosCheckoutDto
{
    public int WarehouseId { get; set; }
    public int? CustomerId { get; set; }
    public PosBuyerDto? Buyer { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public decimal DiscountAmount { get; set; }
    public string? Notes { get; set; }
    public string? ScenarioId { get; set; }
    public string? SaleType { get; set; } = "Local";
    public string? IdempotencyKey { get; set; }
    public List<PosCheckoutLineDto> Lines { get; set; } = [];
    public List<PosTenderDto>? Tenders { get; set; }
    public int? ShiftId { get; set; }
    public int? HeldSaleId { get; set; }
}

public sealed class PosCheckoutLineDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitPriceOverride { get; set; }
    public decimal DiscountAmount { get; set; }
}

public sealed class PosBuyerDto
{
    public string Name { get; set; } = "Walk-in Customer";
    public string? NtnCnic { get; set; }
    public string RegistrationType { get; set; } = "Unregistered";
    public string Province { get; set; } = "Sindh";
    public string Address { get; set; } = string.Empty;
    public string? SroScheduleNo { get; set; }
    public string? SroItemSerialNo { get; set; }
}

public sealed class PosCheckoutResultDto
{
    public int SalesInvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string? FbrInvoiceNumber { get; set; }
    public bool FbrSuccess { get; set; }
    public bool FbrWasStubbed { get; set; }
    public string? FbrMessage { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal SubTotal { get; set; }
    public decimal ChangeDue { get; set; }
    public string PaymentStatus { get; set; } = "Paid";
}

public sealed class HeldSaleDto
{
    public int Id { get; set; }
    public string HoldNumber { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
    public int? CustomerId { get; set; }
    public string? BuyerName { get; set; }
    public string? Notes { get; set; }
    public DateTime HeldAt { get; set; }
    public List<HeldSaleLineDto> Lines { get; set; } = [];
}

public sealed class HeldSaleLineDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal? UnitPriceOverride { get; set; }
    public decimal DiscountAmount { get; set; }
}

public sealed class CashierShiftDto
{
    public int Id { get; set; }
    public string ShiftNumber { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal OpeningFloat { get; set; }
    public decimal ClosingFloat { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}

public sealed class ShiftZReportDto
{
    public int ShiftId { get; set; }
    public string ShiftNumber { get; set; } = string.Empty;
    public decimal OpeningFloat { get; set; }
    public decimal ClosingFloat { get; set; }
    public decimal ExpectedCash { get; set; }
    public List<ShiftTenderTotalDto> TenderTotals { get; set; } = [];
    public int InvoiceCount { get; set; }
    public int ReturnCount { get; set; }
    public int HoldCount { get; set; }
    public decimal SalesTotal { get; set; }
    public decimal ReturnsTotal { get; set; }
}

public sealed class ShiftTenderTotalDto
{
    public string Method { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public sealed class SalesReturnDto
{
    public int Id { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public int? SalesInvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ReturnDate { get; set; }
    public decimal GrandTotal { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
}

public sealed class SalesReturnCreateDto
{
    public int? SalesInvoiceId { get; set; }
    public int? CustomerId { get; set; }
    public int ReturnType { get; set; } = 1;
    public string? Notes { get; set; }
    public int WarehouseId { get; set; } = 1;
    public string ReasonCode { get; set; } = "CUSTOMER";
    public List<SalesReturnLineDto> Lines { get; set; } = [];
}

public sealed class SalesReturnLineDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public sealed class PurchaseReturnCreateDto
{
    public int SupplierId { get; set; }
    public int? PurchaseOrderId { get; set; }
    public int WarehouseId { get; set; } = 1;
    public string ReasonCode { get; set; } = "";
    public string? Notes { get; set; }
    public List<PurchaseReturnLineDto> Lines { get; set; } = [];
}

public sealed class PurchaseReturnLineDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public sealed class TransferListDto
{
    public int Id { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public string? FromWarehouseName { get; set; }
    public string? ToWarehouseName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; }
}

public sealed class TransferDetailDto
{
    public int Id { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public int FromWarehouseId { get; set; }
    public int ToWarehouseId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; }
    public string? Notes { get; set; }
    public List<TransferLineDto> Lines { get; set; } = [];
}

public sealed class TransferCreateDto
{
    public int FromWarehouseId { get; set; }
    public int ToWarehouseId { get; set; }
    public string? Notes { get; set; }
    public List<TransferLineDto> Lines { get; set; } = [];
}

public sealed class TransferLineDto
{
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public decimal Quantity { get; set; }
}

public sealed class DashboardDto
{
    public decimal TodaySales { get; set; }
    public decimal MonthSales { get; set; }
    public decimal MonthPurchases { get; set; }
    public decimal InventoryValue { get; set; }
    public int LowStockCount { get; set; }
    public int PendingPurchaseOrders { get; set; }
    public int PendingTransfers { get; set; }
    public int UnreadNotifications { get; set; }
}

public sealed class AnalyticsDto
{
    public List<TopProductAnalyticsDto> TopSellingProducts { get; set; } = [];
    public List<SlowMovingProductDto> SlowMovingProducts { get; set; } = [];
    public List<AbcAnalysisItemDto> AbcAnalysis { get; set; } = [];
    public decimal TotalInventoryValue { get; set; }
    public decimal TurnoverRatio { get; set; }
}

public sealed class TopProductAnalyticsDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public decimal Profit { get; set; }
}

public sealed class SlowMovingProductDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public int DaysSinceLastSale { get; set; }
}

public sealed class AbcAnalysisItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal AnnualUsageValue { get; set; }
    public decimal CumulativePercentage { get; set; }
    public char AbcClass { get; set; }
}

public sealed class NotificationDto
{
    public int Id { get; set; }
    public int Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public List<string> Roles { get; set; } = [];
}

public sealed class UserCreateDto
{
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public List<int> RoleIds { get; set; } = [];
}

public sealed class RoleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> PermissionCodes { get; set; } = [];
}

public sealed class AuditLogDto
{
    public int Id { get; set; }
    public int Action { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public string? UserName { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public DateTime Timestamp { get; set; }
}

public sealed class CompanySettingsDto
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Ntn { get; set; }
    public string? Strn { get; set; }
    public string? PosId { get; set; }
    public decimal DefaultTaxRate { get; set; }
    public string? InvoicePrefix { get; set; }
    public string? InvoiceFooter { get; set; }
    public string? PrinterName { get; set; }
    public string Theme { get; set; } = "Dark";
    public bool AutoBackupEnabled { get; set; }
    public int AutoBackupIntervalHours { get; set; }
    public bool FbrUseSandbox { get; set; }
    public int FbrTimeoutSeconds { get; set; }
}

public sealed class BackupHistoryDto
{
    public int Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string BackupType { get; set; } = string.Empty;
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime BackupDate { get; set; }
}

public sealed class InventoryValueResponse
{
    public decimal Value { get; set; }
}

public sealed class UnreadCountResponse
{
    public int Count { get; set; }
}
