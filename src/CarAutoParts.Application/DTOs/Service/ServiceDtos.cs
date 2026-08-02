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
    DateTime CreatedAt);

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
    string? Notes);

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
    string? Notes);

public record ServiceTicketStatusChangeDto(ServiceTicketStatus Status, string? ResolutionNotes);
