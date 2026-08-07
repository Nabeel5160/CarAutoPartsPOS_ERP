using CarAutoParts.Domain.Enums;

namespace CarAutoParts.Application.DTOs.Service;

public record ServiceTicketDto(
    int Id,
    int CustomerId,
    string? CustomerName,
    string Subject,
    string? Description,
    ServiceTicketStatus Status,
    ServiceTicketPriority Priority,
    bool IsWarrantyClaim,
    string? WarrantyReference,
    WarrantyClaimStatus WarrantyClaimStatus,
    string? WarrantyDecisionNotes,
    DateTime? WarrantyDecidedAt,
    string? WarrantyDecidedBy,
    string? AmcReference,
    int? ProductId,
    string? ProductName,
    int? AssignedToUserId,
    DateTime OpenedAt,
    DateTime? DueAt,
    DateTime? ResolvedAt,
    DateTime? ClosedAt,
    string? Notes,
    string? ResolutionNotes,
    DateTime CreatedAt,
    int? AmcContractId = null,
    string? AmcContractNumber = null,
    int? WarrantySalesInvoiceId = null,
    int? ReplacementProductId = null,
    string? ReplacementProductName = null,
    decimal ReplacementQuantity = 0,
    string? WarrantyEvidenceNotes = null);

public record ServiceTicketCreateDto(
    int CustomerId,
    string Subject,
    string? Description,
    ServiceTicketPriority Priority,
    bool IsWarrantyClaim,
    string? WarrantyReference,
    string? AmcReference,
    int? ProductId,
    int? AssignedToUserId,
    DateTime? DueAt,
    string? Notes,
    int? AmcContractId = null,
    int? WarrantySalesInvoiceId = null,
    string? WarrantyEvidenceNotes = null,
    int? SlaPolicyId = null);

public record ServiceTicketUpdateDto(
    string Subject,
    string? Description,
    ServiceTicketPriority Priority,
    bool IsWarrantyClaim,
    string? WarrantyReference,
    string? AmcReference,
    int? ProductId,
    int? AssignedToUserId,
    DateTime? DueAt,
    string? Notes,
    int? AmcContractId = null,
    int? WarrantySalesInvoiceId = null,
    string? WarrantyEvidenceNotes = null);

public record ServiceTicketStatusChangeDto(ServiceTicketStatus Status, string? ResolutionNotes);

public record WarrantyClaimDecisionDto(
    WarrantyClaimStatus Decision,
    string? Notes,
    int? ReplacementProductId = null,
    decimal ReplacementQuantity = 0);

public record AmcContractDto(
    int Id,
    string ContractNumber,
    int CustomerId,
    string? CustomerName,
    DateTime StartDate,
    DateTime EndDate,
    AmcContractStatus Status,
    string? CoverageNotes,
    decimal? AnnualAmount,
    int? ProductId,
    string? ProductName);

public record AmcContractUpsertDto(
    int? Id,
    string ContractNumber,
    int CustomerId,
    DateTime StartDate,
    DateTime EndDate,
    AmcContractStatus Status,
    string? CoverageNotes,
    decimal? AnnualAmount,
    int? ProductId);

public record ServiceVisitDto(
    int Id,
    int ServiceTicketId,
    string? TicketSubject,
    int AssignedToUserId,
    string? AssignedToUserName,
    DateTime ScheduledAt,
    DateTime? CompletedAt,
    ServiceVisitStatus Status,
    string? Notes);

public record ServiceVisitCreateDto(
    int ServiceTicketId,
    int AssignedToUserId,
    DateTime ScheduledAt,
    string? Notes);

public record ServiceVisitStatusDto(ServiceVisitStatus Status, string? Notes);

public record ServiceTicketPartDto(
    int Id,
    int ServiceTicketId,
    int ProductId,
    string? ProductName,
    int WarehouseId,
    string? WarehouseName,
    decimal Quantity,
    decimal? UnitCost,
    DateTime ConsumedAt);

public record ServiceTicketPartCreateDto(
    int ServiceTicketId,
    int ProductId,
    int WarehouseId,
    decimal Quantity);
