using CarAutoParts.Domain.Enums;

namespace CarAutoParts.Application.DTOs.Sales;

/// <summary>Sales invoice list row.</summary>
public record SalesInvoiceListDto(
    int Id,
    string InvoiceNumber,
    string? CustomerName,
    DateTime InvoiceDate,
    decimal GrandTotal,
    PaymentStatus PaymentStatus,
    string? FbrInvoiceNumber);

/// <summary>Sales invoice detail with lines and FBR status.</summary>
public record SalesInvoiceDetailDto(
    int Id,
    string InvoiceNumber,
    string? PosReference,
    int? CustomerId,
    string? CustomerName,
    DateTime InvoiceDate,
    decimal SubTotal,
    decimal TaxAmount,
    decimal DiscountAmount,
    decimal GrandTotal,
    PaymentStatus PaymentStatus,
    string? BuyerName,
    string? BuyerNtnCnic,
    string? BuyerProvince,
    string? FbrInvoiceNumber,
    FbrSubmissionStatus? FbrStatus,
    IReadOnlyList<SalesInvoiceLineDto> Lines);

/// <summary>Sales invoice line.</summary>
public record SalesInvoiceLineDto(
    int Id,
    int ProductId,
    string ProductName,
    string Sku,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRate,
    decimal TaxAmount,
    decimal LineTotal);

/// <summary>Sales order list row.</summary>
public record SalesOrderListDto(
    int Id,
    string OrderNumber,
    string? CustomerName,
    SalesOrderStatus Status,
    DateTime OrderDate,
    decimal GrandTotal);

/// <summary>Sales return / credit note row.</summary>
public record SalesReturnDto(
    int Id,
    string ReturnNumber,
    string? CreditNoteNumber,
    int? SalesInvoiceId,
    string? InvoiceNumber,
    int? CustomerId,
    string? CustomerName,
    ReturnStatus Status,
    ReturnType ReturnType,
    DateTime ReturnDate,
    decimal GrandTotal,
    decimal TaxAmount,
    decimal AppliedAmount,
    bool StockAffected,
    string? Notes,
    string ReasonCode = "");

/// <summary>Payload for creating a sales return / credit note.</summary>
public record SalesReturnCreateDto(
    int? SalesInvoiceId,
    int? CustomerId,
    ReturnType ReturnType,
    string? Notes,
    int WarehouseId,
    IReadOnlyList<SalesReturnLineDto> Lines,
    string ReasonCode = "",
    bool StockAffected = true);

/// <summary>Sales return line.</summary>
public record SalesReturnLineDto(
    int ProductId,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRate = 0);

public record ApplyCreditNoteRequest(int SalesInvoiceId, decimal Amount, string? Notes = null);
