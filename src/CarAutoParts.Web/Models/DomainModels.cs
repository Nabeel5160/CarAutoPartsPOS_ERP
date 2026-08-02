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
    public List<VehicleCompatibilityDto> VehicleCompatibilities { get; set; } = [];
    public string? SupersedesSkus { get; set; }
    public string? SupersededBySku { get; set; }
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
    public List<VehicleCompatibilityDto>? VehicleCompatibilities { get; set; }
}

public sealed class VehicleCompatibilityDto
{
    public int? Id { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }
}

public sealed class OemFitmentImportResultDto
{
    public int Processed { get; set; }
    public int OemUpdated { get; set; }
    public int FitmentAdded { get; set; }
    public int Skipped { get; set; }
    public int ErrorCount { get; set; }
    public string? ErrorReportCsv { get; set; }
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
    public int? BranchId { get; set; }
}

public sealed class WarehouseLocationDto
{
    public int Id { get; set; }
    public int WarehouseId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsReceivingDefault { get; set; }
    public bool IsPickDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class UpsertWarehouseLocationDto
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsReceivingDefault { get; set; }
    public bool IsPickDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class InventoryLocationBalanceDto
{
    public int Id { get; set; }
    public int InventoryItemId { get; set; }
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public int WarehouseLocationId { get; set; }
    public string LocationCode { get; set; } = "";
    public string LocationName { get; set; } = "";
    public decimal QuantityOnHand { get; set; }
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
    public bool IsExactMatch { get; set; }
    public string? SupersedesSkus { get; set; }
    public string? SupersededBySku { get; set; }
    public string? FitmentSummary { get; set; }
}

public sealed class FitmentOptionsDto
{
    public List<string> Makes { get; set; } = [];
    public List<string> Models { get; set; } = [];
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
    public decimal ExpectedCash { get; set; }
    public decimal DeclaredClosingCash { get; set; }
    public decimal CashVariance { get; set; }
    public int? VarianceJournalEntryId { get; set; }
    public int? TillId { get; set; }
    public string? TillCode { get; set; }
    public decimal SafeDropsTotal { get; set; }
    public int? WarehouseId { get; set; }
}

public sealed class TillDto
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
    public int? WarehouseId { get; set; }
}

public sealed class SafeDropDto
{
    public int Id { get; set; }
    public int CashierShiftId { get; set; }
    public int TillId { get; set; }
    public decimal Amount { get; set; }
    public DateTime DroppedAt { get; set; }
    public string? Notes { get; set; }
}

public sealed class ShiftZReportDto
{
    public int ShiftId { get; set; }
    public string ShiftNumber { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal OpeningFloat { get; set; }
    public decimal ClosingFloat { get; set; }
    public decimal ExpectedCash { get; set; }
    public List<ShiftTenderTotalDto> TenderTotals { get; set; } = [];
    public int InvoiceCount { get; set; }
    public int ReturnCount { get; set; }
    public int HoldCount { get; set; }
    public decimal SalesTotal { get; set; }
    public decimal ReturnsTotal { get; set; }
    public decimal SafeDropsTotal { get; set; }
    public int? TillId { get; set; }
}

public sealed class ClosedShiftListItemDto
{
    public int Id { get; set; }
    public string ShiftNumber { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public int? BranchId { get; set; }
    public int? TillId { get; set; }
    public string? TillCode { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal OpeningFloat { get; set; }
    public decimal ClosingFloat { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal CashVariance { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class DailySalesSummaryDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int? BranchId { get; set; }
    public bool TaxEnabled { get; set; }
    public int InvoiceCount { get; set; }
    public int ReturnCount { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal SalesTotal { get; set; }
    public decimal ReturnsTotal { get; set; }
    public decimal NetSales { get; set; }
    public List<TenderTotalDto> Tenders { get; set; } = [];
    public List<DailySalesDayRowDto> Days { get; set; } = [];
}

public sealed class DailySalesDayRowDto
{
    public DateTime Date { get; set; }
    public int InvoiceCount { get; set; }
    public decimal SalesTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ReturnsTotal { get; set; }
    public decimal NetSales { get; set; }
}

public sealed class TenderTotalDto
{
    public string Method { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public sealed class SalesReturnsReportDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int? BranchId { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalTax { get; set; }
    public List<SalesReturnReportRowDto> Rows { get; set; } = [];
}

public sealed class SalesReturnReportRowDto
{
    public int Id { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public string? CreditNoteNumber { get; set; }
    public DateTime ReturnDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? InvoiceNumber { get; set; }
    public string? CustomerName { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
}

public sealed class SalesDimReportDto
{
    public string Dimension { get; set; } = "product";
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int? BranchId { get; set; }
    public bool IncludeOem { get; set; }
    public List<SalesDimRowDto> Rows { get; set; } = [];
}

public sealed class SalesDimRowDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string? OemNumber { get; set; }
    public decimal Quantity { get; set; }
    public decimal Revenue { get; set; }
    public decimal TaxAmount { get; set; }
    public int InvoiceCount { get; set; }
}

public sealed class SalesStaffReportDto
{
    public string Dimension { get; set; } = "cashier";
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int? BranchId { get; set; }
    public List<SalesStaffRowDto> Rows { get; set; } = [];
}

public sealed class SalesStaffRowDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int InvoiceCount { get; set; }
    public decimal SalesTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TenderCash { get; set; }
    public decimal TenderOther { get; set; }
}

public sealed class ProfitDimReportDto
{
    public string Dimension { get; set; } = "category";
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int? BranchId { get; set; }
    public List<ProfitDimRowDto> Rows { get; set; } = [];
}

public sealed class ProfitDimRowDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit { get; set; }
    public decimal GrossMarginPercent { get; set; }
}

public sealed class StockMovementReportDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int? WarehouseId { get; set; }
    public string? MovementType { get; set; }
    public List<StockMovementReportRowDto> Rows { get; set; } = [];
}

public sealed class StockMovementReportRowDto
{
    public int Id { get; set; }
    public DateTime MovementDate { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string? Notes { get; set; }
}

public sealed class PurchasingPipelineReportDto
{
    public List<OpenPoRowDto> OpenPurchaseOrders { get; set; } = [];
    public List<PendingGrnRowDto> PendingGrns { get; set; } = [];
}

public sealed class OpenPoRowDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal GrandTotal { get; set; }
    public decimal QtyOrdered { get; set; }
    public decimal QtyReceived { get; set; }
    public string? WarehouseName { get; set; }
    public int? BranchId { get; set; }
}

public sealed class PendingGrnRowDto
{
    public int Id { get; set; }
    public string GrnNumber { get; set; } = string.Empty;
    public string? PoNumber { get; set; }
    public DateTime ReceiptDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public int? BranchId { get; set; }
    public decimal LineQty { get; set; }
}

public sealed class TaxPeriodSummaryDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int? BranchId { get; set; }
    public bool TaxEnabled { get; set; }
    public bool IncludeHs { get; set; }
    public decimal TaxableSales { get; set; }
    public decimal OutputTax { get; set; }
    public decimal ReturnTax { get; set; }
    public decimal NetTax { get; set; }
    public List<TaxRateBucketDto> ByRate { get; set; } = [];
    public List<TaxHsRowDto> ByHsCode { get; set; } = [];
}

public sealed class TaxRateBucketDto
{
    public decimal TaxRate { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
}

public sealed class TaxHsRowDto
{
    public string HsCode { get; set; } = string.Empty;
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Quantity { get; set; }
}

public sealed class FbrRegisterReportDto
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public List<FbrRegisterRowDto> Rows { get; set; } = [];
}

public sealed class FbrRegisterRowDto
{
    public int Id { get; set; }
    public int SalesInvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? FbrInvoiceNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class StockAgingReportDto
{
    public DateTime AsOfDate { get; set; }
    public string SourceNote { get; set; } = string.Empty;
    public List<StockAgingRowDto> Rows { get; set; } = [];
}

public sealed class StockAgingRowDto
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public string? BatchNumber { get; set; }
    public DateTime AgeDate { get; set; }
    public int AgeDays { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Value { get; set; }
    public string Bucket { get; set; } = string.Empty;
}

public sealed class SkuMarginReportDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int? BranchId { get; set; }
    public List<SkuMarginRowDto> Rows { get; set; } = [];
}

public sealed class SkuMarginRowDto
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal QtySold { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit { get; set; }
    public decimal GrossMarginPercent { get; set; }
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
    public string? CreditNoteNumber { get; set; }
    public int? SalesInvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ReturnType { get; set; }
    public DateTime ReturnDate { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal AppliedAmount { get; set; }
    public bool StockAffected { get; set; } = true;
    public string? Notes { get; set; }
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
    public bool StockAffected { get; set; } = true;
    public List<SalesReturnLineDto> Lines { get; set; } = [];
}

public sealed class SalesReturnLineDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
}

public sealed class PurchaseReturnCreateDto
{
    public int SupplierId { get; set; }
    public int? PurchaseOrderId { get; set; }
    public int WarehouseId { get; set; } = 1;
    public string ReasonCode { get; set; } = "";
    public string? Notes { get; set; }
    public bool StockAffected { get; set; } = true;
    public List<PurchaseReturnLineDto> Lines { get; set; } = [];
}

public sealed class PurchaseReturnLineDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
}

public sealed class TransferListDto
{
    public int Id { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public string? FromWarehouseName { get; set; }
    public string? ToWarehouseName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; }
    public bool IsInterBranch { get; set; }
    public bool AllLinesPicked { get; set; }
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
    public bool IsInterBranch { get; set; }
    public bool AllLinesPicked { get; set; }
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
    public decimal ShippedUnitCost { get; set; }
    public int? FromLocationId { get; set; }
    public int? ToLocationId { get; set; }
    public bool IsPicked { get; set; }
    public int? LineId { get; set; }
}

public sealed class ConfirmTransferPickRequest
{
    public List<ConfirmTransferPickLineRequest>? Lines { get; set; }
}

public sealed class ConfirmTransferPickLineRequest
{
    public int LineId { get; set; }
    public int? FromLocationId { get; set; }
    public int? ToLocationId { get; set; }
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
    public int? BranchId { get; set; }
    public decimal TodayCashVariance { get; set; }
    public int OpenShifts { get; set; }
    public List<MonthlySalesPointDto> MonthlySales { get; set; } = [];
    public List<InventoryTrendPointDto> InventoryTrend { get; set; } = [];
    public List<TopProductPointDto> TopProducts { get; set; } = [];
    public List<CategoryDistributionPointDto> CategoryDistribution { get; set; } = [];
}

public sealed class MonthlySalesPointDto
{
    public string Month { get; set; } = string.Empty;
    public decimal Sales { get; set; }
    public decimal Purchases { get; set; }
}

public sealed class InventoryTrendPointDto
{
    public DateTime Date { get; set; }
    public decimal Value { get; set; }
    public decimal Quantity { get; set; }
}

public sealed class TopProductPointDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal Revenue { get; set; }
}

public sealed class CategoryDistributionPointDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public decimal Percentage { get; set; }
}

public sealed class DashboardTimelineDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string Grain { get; set; } = "day";
    public string GroupBy { get; set; } = "category";
    public int? BranchId { get; set; }
    public List<string> SeriesKeys { get; set; } = [];
    public List<DashboardTimelineFrameDto> Frames { get; set; } = [];
    public List<string> Months { get; set; } = [];
    public List<DashboardBar3DPointDto> CategoryMonthSales { get; set; } = [];
}

public sealed class DashboardTimelineFrameDto
{
    public string Label { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public List<decimal> Values { get; set; } = [];
    public decimal Total { get; set; }
}

public sealed class DashboardBar3DPointDto
{
    public string Category { get; set; } = string.Empty;
    public string Month { get; set; } = string.Empty;
    public decimal Sales { get; set; }
}

public sealed class AnalyticsDto
{
    public List<TopProductAnalyticsDto> TopSellingProducts { get; set; } = [];
    public List<SlowMovingProductDto> SlowMovingProducts { get; set; } = [];
    public List<AbcAnalysisItemDto> AbcAnalysis { get; set; } = [];
    public decimal TotalInventoryValue { get; set; }
    public decimal TurnoverRatio { get; set; }
    public List<DeadStockItemDto> DeadStock { get; set; } = [];
    public List<FastMoverItemDto> FastMovers { get; set; } = [];
    public decimal GrossMarginAmount { get; set; }
    public decimal GrossMarginPercent { get; set; }
    public int? BranchId { get; set; }
}

public sealed class DeadStockItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public decimal StockValue { get; set; }
    public int DaysSinceLastSale { get; set; }
}

public sealed class FastMoverItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public decimal Profit { get; set; }
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
    public List<int> BranchIds { get; set; } = [];
    public int? DefaultBranchId { get; set; }
}

public sealed class UserCreateDto
{
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public List<int> RoleIds { get; set; } = [];
    public List<int> BranchIds { get; set; } = [];
    public int? DefaultBranchId { get; set; }
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

public sealed class ApprovalRequestWebDto
{
    public int Id { get; set; }
    public string DocumentType { get; set; } = "";
    public int DocumentId { get; set; }
    public string? DocumentNumber { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
    public string RequestedByUserName { get; set; } = "";
    public DateTime RequestedAt { get; set; }
    public string? DecidedByUserName { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNotes { get; set; }
}

public sealed class ApprovalPolicyWebDto
{
    public int Id { get; set; }
    public string DocumentType { get; set; } = "";
    public decimal MinAmount { get; set; }
    public string RequiredPermission { get; set; } = "";
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
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
    public decimal GrnOverReceivePercent { get; set; }
    public bool GrnUnderReceiveAllowed { get; set; } = true;
    public decimal ThreeWayQtyTolerancePercent { get; set; }
    public decimal ThreeWayPriceTolerancePercent { get; set; }
    public bool AllowNegativeStock { get; set; }
    public string DefaultValuationMethod { get; set; } = "Average";
    public DateTime? OpeningBalanceDate { get; set; }
    public DateTime? SetupCompletedAt { get; set; }
    public string VerticalKey { get; set; } = "auto-parts";
    public string? LogoUrl { get; set; }
}

public sealed class LogoUploadResultDto
{
    public string? LogoUrl { get; set; }
    public string? LogoPath { get; set; }
}

public sealed class OnboardingStatusDto
{
    public bool IsComplete { get; set; }
    public DateTime? SetupCompletedAt { get; set; }
    public List<OnboardingStepDto> Steps { get; set; } = [];
}

public sealed class OnboardingStepDto
{
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public bool Done { get; set; }
    public string? Href { get; set; }
    public string? Hint { get; set; }
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
    public string Method { get; set; } = "Average";
    public int? WarehouseId { get; set; }
    public int? BranchId { get; set; }
}

public sealed class UnreadCountResponse
{
    public int Count { get; set; }
}
