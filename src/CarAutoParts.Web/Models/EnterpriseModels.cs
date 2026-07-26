namespace CarAutoParts.Web.Models;

// —— GRN ——

public sealed class CreateGrnLineRequest
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public int? PurchaseOrderLineId { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public List<string>? SerialNumbers { get; set; }
}

public sealed class CreateGrnLandedCostLineRequest
{
    public string CostType { get; set; } = "Other";
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

public sealed class CreateGrnRequest
{
    public int WarehouseId { get; set; }
    public int? PurchaseOrderId { get; set; }
    public DateTime ReceiptDate { get; set; } = DateTime.UtcNow;
    public decimal LandedCostAmount { get; set; }
    public string? Notes { get; set; }
    public bool HoldForQc { get; set; }
    public List<CreateGrnLineRequest> Lines { get; set; } = [];
    public List<CreateGrnLandedCostLineRequest> LandedCostLines { get; set; } = [];
}

public sealed class GoodsReceiptLineDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public int? PurchaseOrderLineId { get; set; }
    public List<string>? SerialNumbers { get; set; }
}

public sealed class GrnLandedCostLineDto
{
    public int Id { get; set; }
    public string CostType { get; set; } = "";
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

public sealed class GoodsReceiptNoteDto
{
    public int Id { get; set; }
    public string GrnNumber { get; set; } = "";
    public int? PurchaseOrderId { get; set; }
    public int WarehouseId { get; set; }
    public DateTime ReceiptDate { get; set; }
    public int Status { get; set; }
    public decimal LandedCostAmount { get; set; }
    public string? Notes { get; set; }
    public List<GoodsReceiptLineDto> Lines { get; set; } = [];
    public List<GrnLandedCostLineDto>? LandedCostLines { get; set; }
}

// —— AP Invoices ——

public sealed class CreatePurchaseInvoiceLineRequest
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

public sealed class CreatePurchaseInvoiceRequest
{
    public int SupplierId { get; set; }
    public int? PurchaseOrderId { get; set; }
    public int? GoodsReceiptNoteId { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public decimal TaxAmount { get; set; }
    public List<CreatePurchaseInvoiceLineRequest> Lines { get; set; } = [];
}

public sealed class PurchaseInvoiceLineDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class PurchaseInvoiceDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public int SupplierId { get; set; }
    public int? PurchaseOrderId { get; set; }
    public int? GoodsReceiptNoteId { get; set; }
    public DateTime InvoiceDate { get; set; }
    public int Status { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public bool ThreeWayMatched { get; set; }
    public string? MatchNotes { get; set; }
    public List<PurchaseInvoiceLineDto> Lines { get; set; } = [];
}

public sealed class ThreeWayMatchLineResult
{
    public int ProductId { get; set; }
    public decimal PoQuantity { get; set; }
    public decimal GrnQuantity { get; set; }
    public decimal InvoiceQuantity { get; set; }
    public bool IsMatched { get; set; }
}

public sealed class ThreeWayMatchResultDto
{
    public int PurchaseInvoiceId { get; set; }
    public bool IsFullyMatched { get; set; }
    public string Summary { get; set; } = "";
    public List<ThreeWayMatchLineResult> Lines { get; set; } = [];
}

// —— Quotations ——

public sealed class CreateQuotationLineRequest
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public sealed class CreateQuotationRequest
{
    public int? CustomerId { get; set; }
    public DateTime QuotationDate { get; set; } = DateTime.UtcNow;
    public DateTime? ValidUntil { get; set; }
    public string? Notes { get; set; }
    public List<CreateQuotationLineRequest> Lines { get; set; } = [];
}

public sealed class SalesQuotationLineDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class SalesQuotationDto
{
    public int Id { get; set; }
    public string QuotationNumber { get; set; } = "";
    public int? CustomerId { get; set; }
    public DateTime QuotationDate { get; set; }
    public DateTime? ValidUntil { get; set; }
    public int Status { get; set; }
    public decimal GrandTotal { get; set; }
    public string? Notes { get; set; }
    public List<SalesQuotationLineDto> Lines { get; set; } = [];
}

public sealed class ConvertQuotationResultDto
{
    public int SalesOrderId { get; set; }
    public string OrderNumber { get; set; } = "";
    public int QuotationId { get; set; }
}

// —— Deliveries ——

public sealed class CreateDeliveryNoteLineRequest
{
    public int ProductId { get; set; }
    public decimal QuantityOrdered { get; set; }
    public decimal QuantityShipped { get; set; }
}

public sealed class CreateDeliveryNoteRequest
{
    public int? SalesOrderId { get; set; }
    public int WarehouseId { get; set; }
    public DateTime DeliveryDate { get; set; } = DateTime.UtcNow;
    public List<CreateDeliveryNoteLineRequest> Lines { get; set; } = [];
}

public sealed class DeliveryNoteLineDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public decimal QuantityOrdered { get; set; }
    public decimal QuantityShipped { get; set; }
}

public sealed class DeliveryNoteDto
{
    public int Id { get; set; }
    public string DeliveryNumber { get; set; } = "";
    public int? SalesOrderId { get; set; }
    public int WarehouseId { get; set; }
    public DateTime DeliveryDate { get; set; }
    public int Status { get; set; }
    public List<DeliveryNoteLineDto> Lines { get; set; } = [];
}

// —— Account mappings ——

public sealed class AccountMappingDto
{
    public int Id { get; set; }
    public string DocumentType { get; set; } = "";
    public string MappingKey { get; set; } = "";
    public int AccountId { get; set; }
    public string? AccountCode { get; set; }
    public string? AccountName { get; set; }
}

public sealed class CreateAccountMappingRequest
{
    public string DocumentType { get; set; } = "";
    public string MappingKey { get; set; } = "";
    public int AccountId { get; set; }
}

public sealed class UpdateAccountMappingRequest
{
    public string DocumentType { get; set; } = "";
    public string MappingKey { get; set; } = "";
    public int AccountId { get; set; }
}

// —— Reservations ——

public sealed class ReserveStockRequest
{
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public string ReferenceType { get; set; } = "Manual";
    public int ReferenceId { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public sealed class StockReservationDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public string ReferenceType { get; set; } = "";
    public int ReferenceId { get; set; }
    public int Status { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

// —— Cycle counts ——

public sealed class CreateCycleCountLineRequest
{
    public int ProductId { get; set; }
    public decimal CountedQuantity { get; set; }
}

public sealed class CreateCycleCountRequest
{
    public int WarehouseId { get; set; }
    public DateTime CountDate { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public List<CreateCycleCountLineRequest>? Lines { get; set; }
}

public sealed class CycleCountLineDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public decimal SystemQuantity { get; set; }
    public decimal CountedQuantity { get; set; }
    public decimal Variance { get; set; }
}

public sealed class CycleCountDto
{
    public int Id { get; set; }
    public string CountNumber { get; set; } = "";
    public int WarehouseId { get; set; }
    public DateTime CountDate { get; set; }
    public int Status { get; set; }
    public string? Notes { get; set; }
    public List<CycleCountLineDto> Lines { get; set; } = [];
}

// —— Kits ——

public sealed class ProductKitComponentRequest
{
    public int ComponentProductId { get; set; }
    public decimal Quantity { get; set; }
}

public sealed class UpsertKitRequest
{
    public int? Id { get; set; }
    public int ParentProductId { get; set; }
    public string Name { get; set; } = "";
    public List<ProductKitComponentRequest> Components { get; set; } = [];
}

public sealed class ProductKitComponentDto
{
    public int Id { get; set; }
    public int ComponentProductId { get; set; }
    public decimal Quantity { get; set; }
}

public sealed class ProductKitDto
{
    public int Id { get; set; }
    public int ParentProductId { get; set; }
    public string Name { get; set; } = "";
    public List<ProductKitComponentDto> Components { get; set; } = [];
}

// —— Price lists ——

public sealed class PriceListItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public decimal MinQuantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public sealed class PriceListDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string CurrencyCode { get; set; } = "PKR";
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsDefault { get; set; }
    public int? CustomerId { get; set; }
    public List<PriceListItemDto> Items { get; set; } = [];
}

public sealed class CreatePriceListRequest
{
    public string Name { get; set; } = "";
    public string CurrencyCode { get; set; } = "PKR";
    public DateTime ValidFrom { get; set; } = DateTime.UtcNow.Date;
    public DateTime? ValidTo { get; set; }
    public bool IsDefault { get; set; }
    public int? CustomerId { get; set; }
}

public sealed class UpsertPriceListItemRequest
{
    public int ProductId { get; set; }
    public decimal MinQuantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
}

public sealed class UpdatePriceListItemsRequest
{
    public List<UpsertPriceListItemRequest> Items { get; set; } = [];
}

// —— FBR submissions ——

public sealed class FbrSubmissionDto
{
    public int Id { get; set; }
    public int SalesInvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? FbrInvoiceNumber { get; set; }
    public int Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime SubmittedAt { get; set; }
}

// —— Aging ——

public sealed class PartnerAgingLineDto
{
    public int PartnerId { get; set; }
    public string Name { get; set; } = "";
    public decimal Current { get; set; }
    public decimal Days30 { get; set; }
    public decimal Days60 { get; set; }
    public decimal Days90 { get; set; }
    public decimal Total { get; set; }
}

public sealed class PartnerAgingReportDto
{
    public DateTime AsOfDate { get; set; }
    public List<PartnerAgingLineDto> Lines { get; set; } = [];
}

// —— Payments ——

public sealed class PostCustomerReceiptRequest
{
    public int SalesInvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public DateTime? PaymentDate { get; set; }
    public string? Reference { get; set; }
}

public sealed class PostSupplierPaymentRequest
{
    public int SupplierId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string? Reference { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? Notes { get; set; }
}

// —— Requisitions / Reorder ——

public sealed class PurchaseRequisitionLineDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? SuggestedUnitPrice { get; set; }
    public string? Notes { get; set; }
}

public sealed class PurchaseRequisitionDto
{
    public int Id { get; set; }
    public string RequisitionNumber { get; set; } = "";
    public int Status { get; set; }
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public int? WarehouseId { get; set; }
    public DateTime RequestedAt { get; set; }
    public string? RequestedBy { get; set; }
    public string? Notes { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public int? ConvertedPurchaseOrderId { get; set; }
    public List<PurchaseRequisitionLineDto> Lines { get; set; } = [];
}

public sealed class PurchaseRequisitionLineCreateDto
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? SuggestedUnitPrice { get; set; }
    public string? Notes { get; set; }
}

public sealed class PurchaseRequisitionCreateDto
{
    public int? SupplierId { get; set; }
    public int? WarehouseId { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseRequisitionLineCreateDto> Lines { get; set; } = [];
}

public sealed class ReorderSuggestionDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string Sku { get; set; } = "";
    public decimal AvailableQty { get; set; }
    public int MinimumStock { get; set; }
    public int ReorderLevel { get; set; }
    public int? MaximumStock { get; set; }
    public decimal SuggestedQty { get; set; }
    public decimal PurchasePrice { get; set; }
    public bool Selected { get; set; } = true;
}

public sealed class ReorderSuggestionLineDto
{
    public int ProductId { get; set; }
    public decimal SuggestedQty { get; set; }
    public decimal? UnitPrice { get; set; }
}

public sealed class CreateReorderPrRequest
{
    public int? SupplierId { get; set; }
    public int? WarehouseId { get; set; }
    public List<ReorderSuggestionLineDto> Lines { get; set; } = [];
}
