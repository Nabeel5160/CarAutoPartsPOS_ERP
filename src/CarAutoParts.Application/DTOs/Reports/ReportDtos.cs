namespace CarAutoParts.Application.DTOs.Reports;

public record DailySalesSummaryDto(
    DateTime From,
    DateTime To,
    int? BranchId,
    bool TaxEnabled,
    int InvoiceCount,
    int ReturnCount,
    decimal SubTotal,
    decimal TaxAmount,
    decimal DiscountAmount,
    decimal SalesTotal,
    decimal ReturnsTotal,
    decimal NetSales,
    IReadOnlyList<TenderTotalDto> Tenders,
    IReadOnlyList<DailySalesDayRowDto> Days);

public record DailySalesDayRowDto(
    DateTime Date,
    int InvoiceCount,
    decimal SalesTotal,
    decimal TaxAmount,
    decimal ReturnsTotal,
    decimal NetSales);

public record TenderTotalDto(string Method, decimal Amount);

public record SalesReturnsReportDto(
    DateTime From,
    DateTime To,
    int? BranchId,
    decimal TotalAmount,
    decimal TotalTax,
    IReadOnlyList<SalesReturnReportRowDto> Rows);

public record SalesReturnReportRowDto(
    int Id,
    string ReturnNumber,
    string? CreditNoteNumber,
    DateTime ReturnDate,
    string Status,
    string? InvoiceNumber,
    string? CustomerName,
    decimal GrandTotal,
    decimal TaxAmount,
    string ReasonCode);

public record ClosedShiftListItemDto(
    int Id,
    string ShiftNumber,
    string UserName,
    int? BranchId,
    int? TillId,
    string? TillCode,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    decimal OpeningFloat,
    decimal ClosingFloat,
    decimal ExpectedCash,
    decimal CashVariance,
    string Status);

public record SalesDimReportDto(
    string Dimension,
    DateTime From,
    DateTime To,
    int? BranchId,
    bool IncludeOem,
    IReadOnlyList<SalesDimRowDto> Rows);

public record SalesDimRowDto(
    string Key,
    string Label,
    string? Sku,
    string? OemNumber,
    decimal Quantity,
    decimal Revenue,
    decimal TaxAmount,
    int InvoiceCount);

public record SalesStaffReportDto(
    string Dimension,
    DateTime From,
    DateTime To,
    int? BranchId,
    IReadOnlyList<SalesStaffRowDto> Rows);

public record SalesStaffRowDto(
    string Key,
    string Label,
    int InvoiceCount,
    decimal SalesTotal,
    decimal TaxAmount,
    decimal TenderCash,
    decimal TenderOther);

public record ProfitDimReportDto(
    string Dimension,
    DateTime From,
    DateTime To,
    int? BranchId,
    IReadOnlyList<ProfitDimRowDto> Rows);

public record ProfitDimRowDto(
    string Key,
    string Label,
    decimal Quantity,
    decimal Revenue,
    decimal Cost,
    decimal Profit,
    decimal GrossMarginPercent);

public record StockMovementReportDto(
    DateTime From,
    DateTime To,
    int? WarehouseId,
    string? MovementType,
    IReadOnlyList<StockMovementReportRowDto> Rows);

public record StockMovementReportRowDto(
    int Id,
    DateTime MovementDate,
    string MovementType,
    string Sku,
    string ProductName,
    string WarehouseName,
    decimal Quantity,
    decimal UnitCost,
    string? ReferenceType,
    int? ReferenceId,
    string? Notes);

public record PurchasingPipelineReportDto(
    IReadOnlyList<OpenPoRowDto> OpenPurchaseOrders,
    IReadOnlyList<PendingGrnRowDto> PendingGrns);

public record OpenPoRowDto(
    int Id,
    string OrderNumber,
    string SupplierName,
    DateTime OrderDate,
    DateTime? ExpectedDate,
    string Status,
    decimal GrandTotal,
    decimal QtyOrdered,
    decimal QtyReceived,
    string? WarehouseName,
    int? BranchId);

public record PendingGrnRowDto(
    int Id,
    string GrnNumber,
    string? PoNumber,
    DateTime ReceiptDate,
    string Status,
    string WarehouseName,
    int? BranchId,
    decimal LineQty);

public record TaxPeriodSummaryDto(
    DateTime From,
    DateTime To,
    int? BranchId,
    bool TaxEnabled,
    bool IncludeHs,
    decimal TaxableSales,
    decimal OutputTax,
    decimal ReturnTax,
    decimal NetTax,
    IReadOnlyList<TaxRateBucketDto> ByRate,
    IReadOnlyList<TaxHsRowDto> ByHsCode);

public record TaxRateBucketDto(decimal TaxRate, decimal TaxableAmount, decimal TaxAmount);

public record TaxHsRowDto(string HsCode, decimal TaxableAmount, decimal TaxAmount, decimal Quantity);

public record FbrRegisterReportDto(
    DateTime? From,
    DateTime? To,
    IReadOnlyList<FbrRegisterRowDto> Rows);

public record FbrRegisterRowDto(
    int Id,
    int SalesInvoiceId,
    string? InvoiceNumber,
    string? FbrInvoiceNumber,
    string Status,
    DateTime SubmittedAt,
    string? ErrorMessage);

public record StockAgingReportDto(
    DateTime AsOfDate,
    string SourceNote,
    IReadOnlyList<StockAgingReportRowDto> Rows);

public record StockAgingReportRowDto(
    int ProductId,
    string Sku,
    string ProductName,
    string WarehouseName,
    string? BatchNumber,
    DateTime AgeDate,
    int AgeDays,
    decimal Quantity,
    decimal UnitCost,
    decimal Value,
    string Bucket);

public record SkuMarginReportDto(
    DateTime From,
    DateTime To,
    int? BranchId,
    IReadOnlyList<SkuMarginRowDto> Rows);

public record SkuMarginRowDto(
    int ProductId,
    string Sku,
    string ProductName,
    decimal QtySold,
    decimal Revenue,
    decimal Cost,
    decimal Profit,
    decimal GrossMarginPercent);
