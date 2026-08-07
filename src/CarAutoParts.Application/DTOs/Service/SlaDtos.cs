using CarAutoParts.Domain.Enums;

namespace CarAutoParts.Application.DTOs.Service;

public record SlaTargetDto(
    int Id,
    SlaMetric Metric,
    ServiceTicketPriority Priority,
    int TargetMinutes,
    int WarnAtPercent);

public record SlaPolicyRuleDto(
    int Id,
    int SlaPolicyId,
    ServiceTicketPriority? Priority,
    CustomerType? CustomerType,
    int? CustomerId,
    bool? IsWarrantyClaim,
    int SortOrder,
    bool IsActive);

public record SlaPolicyRuleUpsertDto(
    int? Id,
    ServiceTicketPriority? Priority,
    CustomerType? CustomerType,
    int? CustomerId,
    bool? IsWarrantyClaim,
    int SortOrder,
    bool IsActive = true);

public record SlaPolicyDto(
    int Id,
    string Name,
    bool IsDefault,
    bool IsActive,
    SlaCalendarMode CalendarMode,
    bool ApplyToWarrantyOnly,
    int? EscalateToUserId,
    SlaEntityType AppliesToEntityType,
    IReadOnlyList<SlaTargetDto> Targets,
    IReadOnlyList<SlaPolicyRuleDto> Rules);

public record SlaTargetUpsertDto(
    SlaMetric Metric,
    ServiceTicketPriority Priority,
    int TargetMinutes,
    int WarnAtPercent = 80);

public record SlaPolicyUpsertDto(
    int? Id,
    string Name,
    bool IsDefault,
    bool IsActive,
    SlaCalendarMode CalendarMode,
    bool ApplyToWarrantyOnly,
    int? EscalateToUserId,
    IReadOnlyList<SlaTargetUpsertDto> Targets,
    SlaEntityType AppliesToEntityType = SlaEntityType.ServiceTicket);

public record SlaTimerSummaryDto(
    int Id,
    SlaMetric Metric,
    SlaTimerStatus Status,
    int ElapsedSeconds,
    int TargetSeconds,
    int WarnSeconds,
    int RemainingSeconds,
    DateTime StartedAt,
    DateTime? WarnedAt,
    DateTime? BreachedAt,
    DateTime? CompletedAt,
    SlaPauseReason? PauseReason);

public record SlaEventDto(DateTime At, SlaEventKind Kind, string? Note);

public record SlaTicketSummaryDto(
    int ServiceTicketId,
    int? SlaPolicyId,
    string? PolicyName,
    SlaCalendarMode CalendarMode,
    IReadOnlyList<SlaTimerSummaryDto> Timers,
    IReadOnlyList<SlaEventDto> RecentEvents);

public record SlaPauseDto(SlaPauseReason Reason, string? Note);

public record SlaPolicyComplianceDto(
    int SlaPolicyId,
    string PolicyName,
    SlaEntityType AppliesToEntityType,
    double FirstResponseMetPercent,
    double ResolutionMetPercent,
    int FirstResponseTotalCount,
    int ResolutionTotalCount);

public record SlaDashboardDto(
    int OpenBreachCount,
    int OpenWarnCount,
    int FirstResponseMetCount,
    int FirstResponseTotalCount,
    int ResolutionMetCount,
    int ResolutionTotalCount,
    double FirstResponseMetPercent,
    double ResolutionMetPercent,
    IReadOnlyList<SlaPolicyComplianceDto> ByPolicy);

public record BusinessCalendarDto(
    int Id,
    string TimeZoneId,
    string WorkIntervalsJson,
    string HolidaysJson);

public record BusinessCalendarUpsertDto(
    string TimeZoneId,
    string WorkIntervalsJson,
    string HolidaysJson);

public record SlaBreachQueueItemDto(
    int? TicketId,
    string Subject,
    int? CustomerId,
    string? CustomerName,
    ServiceTicketStatus? TicketStatus,
    ServiceTicketPriority? Priority,
    SlaMetric Metric,
    DateTime BreachedAt,
    int ElapsedSeconds,
    int TargetSeconds,
    int? AssignedToUserId,
    SlaEntityType EntityType,
    int EntityId,
    int SlaPolicyId,
    string? PolicyName,
    string? DeepLink);

/// <summary>Thin warn/breach chip for ops list rows.</summary>
public record SlaEntityAlertDto(
    SlaEntityType EntityType,
    int EntityId,
    bool IsBreached,
    bool IsWarned);
