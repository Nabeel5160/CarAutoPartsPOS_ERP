namespace CarAutoParts.Application.DTOs.Pos;

/// <summary>Product available at POS with stock and pricing.</summary>
public record PosProductDto(
    int Id,
    string Name,
    string Sku,
    string? Barcode,
    decimal SalePrice,
    decimal TaxRatePercent,
    string Unit,
    string? HsCode,
    decimal AvailableStock,
    string? OemNumber = null,
    string? PartNumber = null,
    bool IsExactMatch = false,
    string? SupersedesSkus = null,
    string? SupersededBySku = null,
    string? FitmentSummary = null);

/// <summary>POS catalog search with optional fitment filters.</summary>
public record PosProductSearchQuery(
    string? Search = null,
    string? Make = null,
    string? Model = null,
    int? Year = null);

/// <summary>Distinct make/model options for fitment picker.</summary>
public record FitmentOptionsDto(
    IReadOnlyList<string> Makes,
    IReadOnlyList<string> Models);

/// <summary>Single tender line on checkout.</summary>
public record PosTenderDto(string Method, decimal Amount);

/// <summary>POS checkout request.</summary>
public record PosCheckoutDto(
    int WarehouseId,
    int? CustomerId,
    PosBuyerDto? Buyer,
    string PaymentMethod,
    decimal DiscountAmount,
    string? Notes,
    string? ScenarioId,
    string? SaleType,
    IReadOnlyList<PosCheckoutLineDto> Lines,
    string? IdempotencyKey = null,
    IReadOnlyList<PosTenderDto>? Tenders = null,
    int? ShiftId = null,
    int? HeldSaleId = null);

/// <summary>POS checkout line.</summary>
public record PosCheckoutLineDto(
    int ProductId,
    decimal Quantity,
    decimal? UnitPriceOverride,
    decimal DiscountAmount);

/// <summary>POS checkout result including FBR reference.</summary>
public record PosCheckoutResultDto(
    int SalesInvoiceId,
    string InvoiceNumber,
    string? FbrInvoiceNumber,
    bool FbrSuccess,
    bool FbrWasStubbed,
    string? FbrMessage,
    decimal GrandTotal,
    decimal TaxAmount,
    decimal SubTotal,
    decimal ChangeDue = 0,
    string PaymentStatus = "Paid");

/// <summary>Buyer details for POS and FBR.</summary>
public record PosBuyerDto(
    string Name,
    string? NtnCnic,
    string RegistrationType,
    string Province,
    string Address,
    string? SroScheduleNo,
    string? SroItemSerialNo);

public record HoldSaleRequestDto(
    int WarehouseId,
    int? CustomerId,
    string? BuyerName,
    string? Notes,
    IReadOnlyList<PosCheckoutLineDto> Lines);

public record HeldSaleDto(
    int Id,
    string HoldNumber,
    int WarehouseId,
    int? CustomerId,
    string? BuyerName,
    string? Notes,
    DateTime HeldAt,
    IReadOnlyList<HeldSaleLineDto> Lines);

public record HeldSaleLineDto(
    int ProductId,
    string ProductName,
    decimal Quantity,
    decimal? UnitPriceOverride,
    decimal DiscountAmount);

public record OpenShiftRequestDto(decimal OpeningFloat, int? WarehouseId, string? Notes, int? TillId = null);
public record CloseShiftRequestDto(decimal ClosingFloat, string? Notes, decimal? DeclaredClosingCash = null);

public record CashierShiftDto(
    int Id,
    string ShiftNumber,
    int UserId,
    string UserName,
    int? WarehouseId,
    string Status,
    decimal OpeningFloat,
    decimal ClosingFloat,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    int? BranchId = null,
    decimal ExpectedCash = 0,
    decimal DeclaredClosingCash = 0,
    decimal CashVariance = 0,
    int? VarianceJournalEntryId = null,
    int? TillId = null,
    string? TillCode = null,
    decimal SafeDropsTotal = 0);

public record ShiftZReportDto(
    int ShiftId,
    string ShiftNumber,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    decimal OpeningFloat,
    decimal ClosingFloat,
    decimal ExpectedCash,
    IReadOnlyList<ShiftTenderTotalDto> TenderTotals,
    int InvoiceCount,
    int ReturnCount,
    int HoldCount,
    decimal SalesTotal,
    decimal ReturnsTotal,
    decimal SafeDropsTotal = 0,
    int? TillId = null);

public record ShiftTenderTotalDto(string Method, decimal Amount);

public record TillDto(int Id, int BranchId, string Code, string Name, bool IsActive, int? WarehouseId);
public record UpsertTillRequest(int BranchId, string Code, string Name, int? WarehouseId = null, bool IsActive = true);
public record SafeDropRequest(decimal Amount, string? Notes = null);
public record SafeDropDto(int Id, int CashierShiftId, int TillId, decimal Amount, DateTime DroppedAt, string? Notes);
