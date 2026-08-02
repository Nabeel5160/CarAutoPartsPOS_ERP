using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;

namespace CarAutoParts.Application.DTOs.Purchases;

/// <summary>Purchase order list row.</summary>
public record PurchaseOrderListDto(
    int Id,
    string OrderNumber,
    string SupplierName,
    PurchaseOrderStatus Status,
    DateTime OrderDate,
    DateTime? ExpectedDate,
    decimal GrandTotal);

/// <summary>Purchase order detail with lines.</summary>
public record PurchaseOrderDetailDto(
    int Id,
    string OrderNumber,
    int SupplierId,
    string SupplierName,
    PurchaseOrderStatus Status,
    DateTime OrderDate,
    DateTime? ExpectedDate,
    decimal SubTotal,
    decimal TaxAmount,
    decimal DiscountAmount,
    decimal GrandTotal,
    string? Notes,
    string? SupplierBackorderNotes,
    int? WarehouseId,
    string? WarehouseName,
    int? PurchaseRequisitionId,
    IReadOnlyList<PurchaseOrderLineDto> Lines);

/// <summary>Payload for creating or updating a purchase order.</summary>
public record PurchaseOrderCreateDto(
    int SupplierId,
    DateTime? ExpectedDate,
    int? WarehouseId,
    string? Notes,
    decimal DiscountAmount,
    IReadOnlyList<PurchaseOrderLineDto> Lines,
    string? SupplierBackorderNotes = null,
    int? PurchaseRequisitionId = null);

/// <summary>Purchase order line.</summary>
public record PurchaseOrderLineDto(
    int? Id,
    int ProductId,
    string? ProductName,
    decimal QuantityOrdered,
    decimal QuantityReceived,
    decimal UnitPrice,
    decimal TaxRate,
    decimal DiscountAmount,
    decimal LineTotal);

/// <summary>Goods receipt against a purchase order.</summary>
public record ReceivePurchaseOrderDto(
    IReadOnlyList<ReceivePurchaseOrderLineDto> Lines,
    string? Notes);

/// <summary>Line quantity to receive.</summary>
public record ReceivePurchaseOrderLineDto(int LineId, decimal QuantityReceived, string? BatchNumber);

/// <summary>Purchase return / supplier credit note row.</summary>
public record PurchaseReturnDto(
    int Id,
    string ReturnNumber,
    string? CreditNoteNumber,
    int SupplierId,
    string SupplierName,
    ReturnStatus Status,
    DateTime ReturnDate,
    decimal GrandTotal,
    decimal TaxAmount,
    decimal AppliedAmount,
    bool StockAffected,
    string? Notes,
    string ReasonCode,
    int? WarehouseId);

/// <summary>Payload for creating a purchase return / supplier credit note.</summary>
public record PurchaseReturnCreateDto(
    int SupplierId,
    int? PurchaseOrderId,
    int WarehouseId,
    string ReasonCode,
    string? Notes,
    IReadOnlyList<PurchaseReturnLineDto> Lines,
    bool StockAffected = true);

/// <summary>Purchase return line.</summary>
public record PurchaseReturnLineDto(int ProductId, decimal Quantity, decimal UnitPrice, decimal TaxRate = 0);

public record ApplyPurchaseCreditRequest(int PurchaseInvoiceId, decimal Amount, string? Notes = null);

public record PurchaseRequisitionLineDto(
    int Id,
    int ProductId,
    decimal Quantity,
    decimal? SuggestedUnitPrice,
    string? Notes);

public record PurchaseRequisitionDto(
    int Id,
    string RequisitionNumber,
    PurchaseRequisitionStatus Status,
    int? SupplierId,
    string? SupplierName,
    int? WarehouseId,
    DateTime RequestedAt,
    string? RequestedBy,
    string? Notes,
    string? ApprovedBy,
    DateTime? ApprovedAt,
    string? RejectionReason,
    int? ConvertedPurchaseOrderId,
    IReadOnlyList<PurchaseRequisitionLineDto> Lines);

public record PurchaseRequisitionLineCreateDto(
    int ProductId,
    decimal Quantity,
    decimal? SuggestedUnitPrice = null,
    string? Notes = null);

public record PurchaseRequisitionCreateDto(
    int? SupplierId,
    int? WarehouseId,
    string? Notes,
    IReadOnlyList<PurchaseRequisitionLineCreateDto> Lines);

public record ReorderSuggestionDto(
    int ProductId,
    string ProductName,
    string Sku,
    decimal AvailableQty,
    int MinimumStock,
    int ReorderLevel,
    int? MaximumStock,
    decimal SuggestedQty,
    decimal PurchasePrice);

public record ReorderSuggestionLineDto(int ProductId, decimal SuggestedQty, decimal? UnitPrice = null);

public record CreateReorderPrRequest(
    int? SupplierId,
    int? WarehouseId,
    IReadOnlyList<ReorderSuggestionLineDto> Lines);

// —— Purchase RFQ (Program B — thin RFQ → compare → PO) ——

public record CreatePurchaseRfqLineRequest(int ProductId, decimal Quantity, string? Notes = null);

public record CreatePurchaseRfqRequest(
    DateTime? ResponseDeadline,
    string? Notes,
    IReadOnlyList<CreatePurchaseRfqLineRequest> Lines);

public record PurchaseRfqLineDto(
    int Id,
    int ProductId,
    string? ProductName,
    string? Sku,
    decimal Quantity,
    string? Notes);

public record CreateVendorQuoteLineRequest(
    int ProductId,
    decimal Quantity,
    decimal UnitPrice,
    int? LeadTimeDays = null,
    string? Notes = null);

public record CreateVendorQuoteRequest(
    int SupplierId,
    DateTime? ValidUntil,
    string? Notes,
    IReadOnlyList<CreateVendorQuoteLineRequest> Lines);

public record VendorQuoteLineDto(
    int Id,
    int ProductId,
    string? ProductName,
    decimal Quantity,
    decimal UnitPrice,
    int? LeadTimeDays,
    string? Notes,
    decimal LineTotal);

public record VendorQuoteDto(
    int Id,
    int PurchaseRfqId,
    int SupplierId,
    string? SupplierName,
    VendorQuoteStatus Status,
    DateTime QuoteDate,
    DateTime? ValidUntil,
    string? Notes,
    decimal TotalAmount,
    IReadOnlyList<VendorQuoteLineDto> Lines);

public record PurchaseRfqDto(
    int Id,
    string RfqNumber,
    PurchaseRfqStatus Status,
    DateTime RfqDate,
    DateTime? ResponseDeadline,
    string? Notes,
    int? PurchaseOrderId,
    string? PurchaseOrderNumber,
    IReadOnlyList<PurchaseRfqLineDto> Lines,
    IReadOnlyList<VendorQuoteDto> VendorQuotes);
