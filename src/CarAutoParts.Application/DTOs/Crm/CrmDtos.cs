using CarAutoParts.Domain.Enums;

namespace CarAutoParts.Application.DTOs.Crm;

public record LeadDto(
    int Id,
    string Name,
    string? Phone,
    string? Email,
    string? Source,
    LeadStatus Status,
    string? Notes,
    string? LostReason,
    int? OwnerUserId,
    int? ConvertedCustomerId,
    DateTime CreatedAt,
    int Score = 0);

public record LeadCreateDto(
    string Name,
    string? Phone,
    string? Email,
    string? Source,
    string? Notes,
    int? OwnerUserId,
    bool ConfirmDuplicate = false);

public record LeadUpdateDto(
    string Name,
    string? Phone,
    string? Email,
    string? Source,
    string? Notes,
    int? OwnerUserId,
    LeadStatus Status,
    string? LostReason,
    bool ConfirmDuplicate = false);

public record LeadDuplicateDto(int Id, string Name, string? Phone, string? Email, LeadStatus Status, string Kind);

public record ConvertLeadToOpportunityDto(string? Name, decimal Value, DateTime? ExpectedCloseDate);

public record CrmActivityDto(
    int Id,
    CrmActivityType Type,
    string Subject,
    DateTime? DueAt,
    DateTime? CompletedAt,
    int? LeadId,
    int? CustomerId,
    int? AssignedToUserId,
    string? Notes,
    string? AttachmentPath,
    string? AttachmentName);

public record CrmActivityCreateDto(
    CrmActivityType Type,
    string Subject,
    DateTime? DueAt,
    int? LeadId,
    int? CustomerId,
    int? AssignedToUserId,
    string? Notes,
    bool CreateNextOnComplete = false,
    int? NextDueDays = null);

public record OpportunityDto(
    int Id,
    string Name,
    int? LeadId,
    int? CustomerId,
    OpportunityStage Stage,
    decimal Value,
    int Probability,
    decimal WeightedValue,
    DateTime? ExpectedCloseDate,
    int? QuotationId,
    string? LostReason,
    string? WinReason,
    DateTime? StageChangedAt);

public record OpportunityCreateDto(
    string Name,
    int? LeadId,
    int? CustomerId,
    decimal Value,
    int Probability,
    DateTime? ExpectedCloseDate);

public record OpportunityUpdateDto(
    string Name,
    int? LeadId,
    int? CustomerId,
    decimal Value,
    int Probability,
    DateTime? ExpectedCloseDate);

public record OpportunityStageChangeDto(
    OpportunityStage Stage,
    string? LostReason,
    string? WinReason,
    string? Note);

public record OpportunityStageHistoryDto(
    int Id,
    OpportunityStage FromStage,
    OpportunityStage ToStage,
    string? ChangedBy,
    DateTime ChangedAt,
    string? Note);

public record CrmPipelineDashboardDto(
    decimal OpenValue,
    decimal WeightedValue,
    int OpenCount,
    int WonCount,
    int LostCount,
    double WinRate,
    IReadOnlyList<CrmStageBucketDto> ByStage);

public record CrmStageBucketDto(OpportunityStage Stage, int Count, decimal Value, decimal WeightedValue);

public record Customer360Dto(
    int CustomerId,
    string Name,
    string? Phone,
    string? Email,
    decimal CreditLimit,
    decimal Balance,
    decimal Outstanding,
    decimal Profitability,
    IReadOnlyList<LeadDto> ConvertedLeads,
    IReadOnlyList<CrmActivityDto> Activities,
    IReadOnlyList<OpportunityDto> Opportunities,
    IReadOnlyList<Customer360DocDto> Invoices,
    IReadOnlyList<Customer360DocDto> Orders,
    IReadOnlyList<Customer360DocDto> Returns);

public record Customer360DocDto(int Id, string Number, DateTime Date, decimal Amount, string Status);

public record CrmAssignmentRuleDto(int Id, string? Source, int? OwnerUserId, bool IsDefault, bool IsActive);

public record CrmEmailTemplateDto(int Id, string Name, string Subject, string Body, bool IsActive);
