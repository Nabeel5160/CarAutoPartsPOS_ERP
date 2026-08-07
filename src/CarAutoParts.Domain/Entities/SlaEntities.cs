using CarAutoParts.Domain.Common;
using CarAutoParts.Domain.Enums;

namespace CarAutoParts.Domain.Entities;

/// <summary>Company SLA policy (Program C2) — targets by priority/metric.</summary>
public class SlaPolicy : CompanyEntity
{
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public SlaCalendarMode CalendarMode { get; set; } = SlaCalendarMode.AlwaysOn;
    /// <summary>When true, this policy only attaches to warranty-claim tickets (legacy fallback after rules).</summary>
    public bool ApplyToWarrantyOnly { get; set; }
    /// <summary>Which entity type this policy applies to (ServiceTicket by default; ops clocks use other types).</summary>
    public SlaEntityType AppliesToEntityType { get; set; } = SlaEntityType.ServiceTicket;
    /// <summary>On SLA breach, optionally reassign the ticket to this user (stub escalation).</summary>
    public int? EscalateToUserId { get; set; }
    public AppUser? EscalateToUser { get; set; }
    public ICollection<SlaTarget> Targets { get; set; } = new List<SlaTarget>();
    public ICollection<SlaPolicyRule> Rules { get; set; } = new List<SlaPolicyRule>();
}

/// <summary>Routing rule: first matching active rule (by SortOrder) selects the policy on ticket open.</summary>
public class SlaPolicyRule : CompanyEntity
{
    public int SlaPolicyId { get; set; }
    public SlaPolicy? SlaPolicy { get; set; }
    public ServiceTicketPriority? Priority { get; set; }
    public CustomerType? CustomerType { get; set; }
    public int? CustomerId { get; set; }
    public bool? IsWarrantyClaim { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Target minutes for a metric + priority under a policy.</summary>
public class SlaTarget : CompanyEntity
{
    public int SlaPolicyId { get; set; }
    public SlaPolicy? SlaPolicy { get; set; }
    public SlaMetric Metric { get; set; }
    public ServiceTicketPriority Priority { get; set; }
    public int TargetMinutes { get; set; }
    public int WarnAtPercent { get; set; } = 80;
}

/// <summary>SLA clock instance — polymorphic via EntityType/EntityId (tickets + thin ops docs).</summary>
public class SlaTimer : CompanyEntity
{
    /// <summary>Legacy ticket FK; kept for ticket-specific joins. Prefer EntityType/EntityId.</summary>
    public int? ServiceTicketId { get; set; }
    public ServiceTicket? ServiceTicket { get; set; }
    public SlaEntityType EntityType { get; set; } = SlaEntityType.ServiceTicket;
    public int EntityId { get; set; }
    public SlaMetric Metric { get; set; }
    public int SlaPolicyId { get; set; }
    public SlaPolicy? SlaPolicy { get; set; }
    public int SlaTargetId { get; set; }
    public SlaTarget? SlaTarget { get; set; }
    public SlaTimerStatus Status { get; set; } = SlaTimerStatus.Running;
    public DateTime StartedAt { get; set; }
    public DateTime? PausedAt { get; set; }
    /// <summary>When the current Running segment began (null when Paused/terminal).</summary>
    public DateTime? ActiveSince { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ElapsedSeconds { get; set; }
    public int TargetSeconds { get; set; }
    public int WarnSeconds { get; set; }
    public DateTime? WarnedAt { get; set; }
    public DateTime? BreachedAt { get; set; }
    public SlaPauseReason? PauseReason { get; set; }
    public ICollection<SlaEvent> Events { get; set; } = new List<SlaEvent>();
}

/// <summary>Audit trail for an SLA timer.</summary>
public class SlaEvent : BaseEntity
{
    public int SlaTimerId { get; set; }
    public SlaTimer? SlaTimer { get; set; }
    public DateTime At { get; set; }
    public SlaEventKind Kind { get; set; }
    public string? Note { get; set; }
}

/// <summary>Company business hours calendar for SLA BusinessHours mode.</summary>
public class BusinessCalendar : CompanyEntity
{
    public string TimeZoneId { get; set; } = "Asia/Karachi";
    /// <summary>JSON array of work intervals, e.g. [{"dow":1,"start":"09:00","end":"18:00"},...].</summary>
    public string WorkIntervalsJson { get; set; } = "[]";
    /// <summary>JSON array of holiday dates yyyy-MM-dd.</summary>
    public string HolidaysJson { get; set; } = "[]";
}
