using CarAutoParts.Application.Common;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Enterprise;

public interface IEnterpriseDb
{
    DbSet<StockReservation> StockReservations { get; }
    DbSet<GoodsReceiptNote> GoodsReceiptNotes { get; }
    DbSet<GoodsReceiptLine> GoodsReceiptLines { get; }
    DbSet<GrnLandedCostLine> GrnLandedCostLines { get; }
    DbSet<PurchaseRequisition> PurchaseRequisitions { get; }
    DbSet<PurchaseRequisitionLine> PurchaseRequisitionLines { get; }
    DbSet<CompanySettings> CompanySettings { get; }
    DbSet<SerialNumber> SerialNumbers { get; }
    DbSet<SerialNumberHistory> SerialNumberHistories { get; }
    DbSet<CycleCount> CycleCounts { get; }
    DbSet<CycleCountLine> CycleCountLines { get; }
    DbSet<PurchaseInvoice> PurchaseInvoices { get; }
    DbSet<PurchaseInvoiceLine> PurchaseInvoiceLines { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderLine> PurchaseOrderLines { get; }
    DbSet<SalesQuotation> SalesQuotations { get; }
    DbSet<SalesQuotationLine> SalesQuotationLines { get; }
    DbSet<SalesOrder> SalesOrders { get; }
    DbSet<SalesOrderLine> SalesOrderLines { get; }
    DbSet<DeliveryNote> DeliveryNotes { get; }
    DbSet<DeliveryNoteLine> DeliveryNoteLines { get; }
    DbSet<PriceList> PriceLists { get; }
    DbSet<PriceListItem> PriceListItems { get; }
    DbSet<ProductKit> ProductKits { get; }
    DbSet<ProductKitComponent> ProductKitComponents { get; }
    DbSet<ProductSupersession> ProductSupersessions { get; }
    DbSet<InventoryItem> InventoryItems { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<Product> Products { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<NumberSequence> NumberSequences { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<JournalLine> JournalLines { get; }
    DbSet<GlAccount> GlAccounts { get; }
    DbSet<AccountMapping> AccountMappings { get; }
    DbSet<AccountingPeriod> AccountingPeriods { get; }
    DbSet<SalesInvoice> SalesInvoices { get; }
    DbSet<Payment> Payments { get; }
    DbSet<SupplierPayment> SupplierPayments { get; }
    DbSet<FbrSubmission> FbrSubmissions { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

// —— Inventory DTOs ——

public record ReserveStockRequest(
    int ProductId,
    int WarehouseId,
    decimal Quantity,
    string ReferenceType,
    int ReferenceId,
    DateTime? ExpiresAt = null);

public record StockReservationDto(
    int Id,
    int ProductId,
    int WarehouseId,
    decimal Quantity,
    string ReferenceType,
    int ReferenceId,
    ReservationStatus Status,
    DateTime? ExpiresAt);

public record CreateGrnLandedCostLineRequest(string CostType, decimal Amount, string? Notes = null);

public record CreateGrnLineRequest(
    int ProductId,
    decimal Quantity,
    decimal UnitCost,
    int? PurchaseOrderLineId = null,
    string? BatchNumber = null,
    DateTime? ExpiryDate = null,
    IReadOnlyList<string>? SerialNumbers = null);

public record CreateGrnRequest(
    int WarehouseId,
    int? PurchaseOrderId,
    DateTime ReceiptDate,
    decimal LandedCostAmount,
    string? Notes,
    IReadOnlyList<CreateGrnLineRequest> Lines,
    IReadOnlyList<CreateGrnLandedCostLineRequest>? LandedCostLines = null,
    bool HoldForQc = false);

public record GoodsReceiptLineDto(
    int Id,
    int ProductId,
    decimal Quantity,
    decimal UnitCost,
    int? PurchaseOrderLineId,
    IReadOnlyList<string>? SerialNumbers = null);

public record GrnLandedCostLineDto(int Id, string CostType, decimal Amount, string? Notes);

public record GoodsReceiptNoteDto(
    int Id,
    string GrnNumber,
    int? PurchaseOrderId,
    int WarehouseId,
    DateTime ReceiptDate,
    GrnStatus Status,
    decimal LandedCostAmount,
    string? Notes,
    IReadOnlyList<GoodsReceiptLineDto> Lines,
    IReadOnlyList<GrnLandedCostLineDto>? LandedCostLines = null);

public record CreateCycleCountLineRequest(int ProductId, decimal CountedQuantity);

public record CreateCycleCountRequest(
    int WarehouseId,
    DateTime CountDate,
    string? Notes,
    IReadOnlyList<CreateCycleCountLineRequest>? Lines = null);

public record CycleCountLineDto(
    int Id,
    int ProductId,
    decimal SystemQuantity,
    decimal CountedQuantity,
    decimal Variance);

public record CycleCountDto(
    int Id,
    string CountNumber,
    int WarehouseId,
    DateTime CountDate,
    CycleCountStatus Status,
    string? Notes,
    IReadOnlyList<CycleCountLineDto> Lines);

// —— Purchase DTOs ——

public record CreatePurchaseInvoiceLineRequest(
    int ProductId,
    decimal Quantity,
    decimal UnitCost);

public record CreatePurchaseInvoiceRequest(
    int SupplierId,
    int? PurchaseOrderId,
    int? GoodsReceiptNoteId,
    DateTime InvoiceDate,
    decimal TaxAmount,
    IReadOnlyList<CreatePurchaseInvoiceLineRequest> Lines);

public record PurchaseInvoiceLineDto(
    int Id,
    int ProductId,
    decimal Quantity,
    decimal UnitCost,
    decimal LineTotal);

public record PurchaseInvoiceDto(
    int Id,
    string InvoiceNumber,
    int SupplierId,
    int? PurchaseOrderId,
    int? GoodsReceiptNoteId,
    DateTime InvoiceDate,
    PurchaseInvoiceStatus Status,
    decimal SubTotal,
    decimal TaxAmount,
    decimal GrandTotal,
    bool ThreeWayMatched,
    string? MatchNotes,
    IReadOnlyList<PurchaseInvoiceLineDto> Lines);

public record ThreeWayMatchLineResult(
    int ProductId,
    decimal PoQuantity,
    decimal GrnQuantity,
    decimal InvoiceQuantity,
    bool IsMatched);

public record ThreeWayMatchResultDto(
    int PurchaseInvoiceId,
    bool IsFullyMatched,
    string Summary,
    IReadOnlyList<ThreeWayMatchLineResult> Lines);

// —— Sales DTOs ——

public record CreateQuotationLineRequest(
    int ProductId,
    decimal Quantity,
    decimal UnitPrice);

public record CreateQuotationRequest(
    int? CustomerId,
    DateTime QuotationDate,
    DateTime? ValidUntil,
    string? Notes,
    IReadOnlyList<CreateQuotationLineRequest> Lines);

public record SalesQuotationLineDto(
    int Id,
    int ProductId,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public record SalesQuotationDto(
    int Id,
    string QuotationNumber,
    int? CustomerId,
    DateTime QuotationDate,
    DateTime? ValidUntil,
    QuotationStatus Status,
    decimal GrandTotal,
    string? Notes,
    IReadOnlyList<SalesQuotationLineDto> Lines);

public record ConvertQuotationResultDto(int SalesOrderId, string OrderNumber, int QuotationId);

public record CreateDeliveryNoteLineRequest(
    int ProductId,
    decimal QuantityOrdered,
    decimal QuantityShipped);

public record CreateDeliveryNoteRequest(
    int? SalesOrderId,
    int WarehouseId,
    DateTime DeliveryDate,
    IReadOnlyList<CreateDeliveryNoteLineRequest> Lines);

public record DeliveryNoteLineDto(
    int Id,
    int ProductId,
    decimal QuantityOrdered,
    decimal QuantityShipped);

public record DeliveryNoteDto(
    int Id,
    string DeliveryNumber,
    int? SalesOrderId,
    int WarehouseId,
    DateTime DeliveryDate,
    DeliveryStatus Status,
    IReadOnlyList<DeliveryNoteLineDto> Lines);

public record PriceLookupResultDto(
    int ProductId,
    decimal UnitPrice,
    int? PriceListId,
    string? PriceListName);

public record CreditCheckResultDto(
    bool Approved,
    decimal CreditLimit,
    decimal CurrentBalance,
    decimal AvailableCredit,
    string? Message);

public record PriceListItemDto(
    int Id,
    int ProductId,
    decimal MinQuantity,
    decimal UnitPrice);

public record PriceListDto(
    int Id,
    string Name,
    string CurrencyCode,
    DateTime ValidFrom,
    DateTime? ValidTo,
    bool IsDefault,
    int? CustomerId,
    IReadOnlyList<PriceListItemDto> Items);

public record CreatePriceListRequest(
    string Name,
    string CurrencyCode,
    DateTime ValidFrom,
    DateTime? ValidTo,
    bool IsDefault,
    int? CustomerId);

public record UpsertPriceListItemRequest(
    int ProductId,
    decimal MinQuantity,
    decimal UnitPrice);

public record UpdatePriceListItemsRequest(
    IReadOnlyList<UpsertPriceListItemRequest> Items);

public record FbrSubmissionDto(
    int Id,
    int SalesInvoiceId,
    string? InvoiceNumber,
    string? FbrInvoiceNumber,
    FbrSubmissionStatus Status,
    string? ErrorMessage,
    DateTime SubmittedAt);

// —— Account mapping DTOs ——

public record AccountMappingDto(
    int Id,
    string DocumentType,
    string MappingKey,
    int AccountId,
    string? AccountCode,
    string? AccountName);

public record CreateAccountMappingRequest(
    string DocumentType,
    string MappingKey,
    int AccountId);

public record UpdateAccountMappingRequest(
    string DocumentType,
    string MappingKey,
    int AccountId);

// —— Aging DTOs ——

public record PartnerAgingLineDto(
    int PartnerId,
    string Name,
    decimal Current,
    decimal Days30,
    decimal Days60,
    decimal Days90,
    decimal Total);

public record PartnerAgingReportDto(
    DateTime AsOfDate,
    IReadOnlyList<PartnerAgingLineDto> Lines);

// —— Master data DTOs ——

public record ProductKitComponentRequest(int ComponentProductId, decimal Quantity);

public record UpsertKitRequest(
    int? Id,
    int ParentProductId,
    string Name,
    IReadOnlyList<ProductKitComponentRequest> Components);

public record ProductKitComponentDto(int Id, int ComponentProductId, decimal Quantity);

public record ProductKitDto(
    int Id,
    int ParentProductId,
    string Name,
    IReadOnlyList<ProductKitComponentDto> Components);

public record UpsertSupersessionRequest(
    int? Id,
    int OldProductId,
    int NewProductId,
    DateTime EffectiveFrom,
    string? Notes);

public record ProductSupersessionDto(
    int Id,
    int OldProductId,
    int NewProductId,
    DateTime EffectiveFrom,
    string? Notes);

// —— Financial report DTOs ——

public record TrialBalanceLineDto(
    int AccountId,
    string Code,
    string Name,
    AccountType AccountType,
    decimal Debit,
    decimal Credit,
    decimal Balance);

public record TrialBalanceReportDto(
    DateTime AsOfDate,
    decimal TotalDebit,
    decimal TotalCredit,
    IReadOnlyList<TrialBalanceLineDto> Lines);

public record ProfitAndLossLineDto(
    int AccountId,
    string Code,
    string Name,
    AccountType AccountType,
    decimal Amount);

public record ProfitAndLossReportDto(
    DateTime FromDate,
    DateTime ToDate,
    decimal TotalRevenue,
    decimal TotalExpenses,
    decimal NetProfit,
    IReadOnlyList<ProfitAndLossLineDto> Lines);

public record BalanceSheetLineDto(
    int AccountId,
    string Code,
    string Name,
    AccountType AccountType,
    decimal Balance);

public record BalanceSheetReportDto(
    DateTime AsOfDate,
    decimal TotalAssets,
    decimal TotalLiabilities,
    decimal TotalEquity,
    IReadOnlyList<BalanceSheetLineDto> Lines);

// —— GL posting DTOs ——

public record GlPostingLineRequest(
    string MappingKey,
    decimal Amount,
    bool IsDebit,
    string? Description = null);

public record GlJournalDraftDto(
    int JournalId,
    string JournalNumber,
    JournalStatus Status,
    decimal TotalDebit,
    decimal TotalCredit);

public record FbrSubmissionRequested(int SalesInvoiceId, string? RequestJson);

// —— Service interfaces ——

public interface IEnterpriseInventoryService
{
    Task<IReadOnlyList<GoodsReceiptNoteDto>> GetGrnsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<StockReservationDto>> GetReservationsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CycleCountDto>> GetCycleCountsAsync(CancellationToken ct = default);
    Task<Result<StockReservationDto>> ReserveStockAsync(ReserveStockRequest request, CancellationToken ct = default);
    Task<Result> ReleaseReservationAsync(int reservationId, CancellationToken ct = default);
    Task<Result<GoodsReceiptNoteDto>> CreateGrnAsync(CreateGrnRequest request, CancellationToken ct = default);
    Task<Result<GoodsReceiptNoteDto>> PostGrnAsync(int grnId, CancellationToken ct = default);
    Task<Result<GoodsReceiptNoteDto>> ReleaseQcAsync(int grnId, CancellationToken ct = default);
    Task<Result<CycleCountDto>> CreateCycleCountAsync(CreateCycleCountRequest request, CancellationToken ct = default);
    Task<Result<CycleCountDto>> CompleteCycleCountAsync(int cycleCountId, CancellationToken ct = default);
}

public interface IEnterprisePurchaseService
{
    Task<IReadOnlyList<PurchaseInvoiceDto>> GetPurchaseInvoicesAsync(CancellationToken ct = default);
    Task<Result<PurchaseInvoiceDto>> CreatePurchaseInvoiceAsync(CreatePurchaseInvoiceRequest request, CancellationToken ct = default);
    Task<Result<ThreeWayMatchResultDto>> MatchThreeWayAsync(int purchaseInvoiceId, CancellationToken ct = default);
    Task<Result<PurchaseInvoiceDto>> PostPurchaseInvoiceAsync(int purchaseInvoiceId, CancellationToken ct = default);
}

public interface IEnterpriseSalesService
{
    Task<IReadOnlyList<SalesQuotationDto>> GetQuotationsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DeliveryNoteDto>> GetDeliveriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PriceListDto>> GetPriceListsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<FbrSubmissionDto>> GetFbrSubmissionsAsync(CancellationToken ct = default);
    Task<Result<SalesQuotationDto>> CreateQuotationAsync(CreateQuotationRequest request, CancellationToken ct = default);
    Task<Result<ConvertQuotationResultDto>> ConvertQuotationToSalesOrderAsync(int quotationId, CancellationToken ct = default);
    Task<Result<DeliveryNoteDto>> CreateDeliveryNoteAsync(CreateDeliveryNoteRequest request, CancellationToken ct = default);
    Task<Result<DeliveryNoteDto>> ShipDeliveryAsync(int deliveryNoteId, CancellationToken ct = default);
    Task<Result<PriceListDto>> CreatePriceListAsync(CreatePriceListRequest request, CancellationToken ct = default);
    Task<Result<PriceListDto>> UpdatePriceListItemsAsync(int priceListId, UpdatePriceListItemsRequest request, CancellationToken ct = default);
    Task<Result<PriceLookupResultDto>> GetPriceForProductAsync(int productId, int? customerId = null, decimal quantity = 1, CancellationToken ct = default);
    Task<Result<CreditCheckResultDto>> CheckCreditLimitAsync(int customerId, decimal additionalAmount, CancellationToken ct = default);
}

public interface IMasterDataService
{
    Task<Result<ProductKitDto>> UpsertKitAsync(UpsertKitRequest request, CancellationToken ct = default);
    Task<Result<ProductSupersessionDto>> UpsertSupersessionAsync(UpsertSupersessionRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ProductKitDto>> GetKitsAsync(int? parentProductId = null, CancellationToken ct = default);
    Task<IReadOnlyList<ProductSupersessionDto>> GetSupersessionsAsync(int? productId = null, CancellationToken ct = default);
}

public interface IFinancialReportService
{
    Task<Result<TrialBalanceReportDto>> TrialBalanceAsync(DateTime asOfDate, CancellationToken ct = default);
    Task<Result<ProfitAndLossReportDto>> ProfitAndLossAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<Result<BalanceSheetReportDto>> BalanceSheetAsync(DateTime asOfDate, CancellationToken ct = default);
    Task<Result<PartnerAgingReportDto>> CustomerAgingAsync(DateTime? asOfDate = null, CancellationToken ct = default);
    Task<Result<PartnerAgingReportDto>> SupplierAgingAsync(DateTime? asOfDate = null, CancellationToken ct = default);
}

public interface IAccountMappingService
{
    Task<IReadOnlyList<AccountMappingDto>> GetAccountMappingsAsync(CancellationToken ct = default);
    Task<Result<AccountMappingDto>> CreateAccountMappingAsync(CreateAccountMappingRequest request, CancellationToken ct = default);
    Task<Result<AccountMappingDto>> UpdateAccountMappingAsync(int id, UpdateAccountMappingRequest request, CancellationToken ct = default);
    Task<Result> DeleteAccountMappingAsync(int id, CancellationToken ct = default);
}

public interface IGlPostingService
{
    Task<Result<GlJournalDraftDto>> CreateBalancedJournalAsync(
        string documentType,
        DateTime journalDate,
        string? reference,
        string? description,
        int? sourceDocumentId,
        IReadOnlyList<GlPostingLineRequest> lines,
        CancellationToken ct = default);

    Task<Result<GlJournalDraftDto>> PostDocumentAsync(
        string documentType,
        DateTime journalDate,
        string? reference,
        string? description,
        int? sourceDocumentId,
        IReadOnlyList<GlPostingLineRequest> lines,
        bool autoPost = true,
        CancellationToken ct = default);
}

public interface IFbrOutboxService
{
    void EnqueueFbrRetry(int salesInvoiceId, string? requestJson = null);
}
