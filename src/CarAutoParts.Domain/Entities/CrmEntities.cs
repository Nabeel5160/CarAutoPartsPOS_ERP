using CarAutoParts.Domain.Common;
using CarAutoParts.Domain.Enums;

namespace CarAutoParts.Domain.Entities;

/// <summary>Sales lead — converts to <see cref="Customer"/> in CRM W1.</summary>
public class Lead : CompanyEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Source { get; set; }
    public LeadStatus Status { get; set; } = LeadStatus.New;
    public string? Notes { get; set; }
    public string? LostReason { get; set; }
    public int? OwnerUserId { get; set; }
    public AppUser? OwnerUser { get; set; }
    public int? ConvertedCustomerId { get; set; }
    public Customer? ConvertedCustomer { get; set; }
    public ICollection<CrmActivity> Activities { get; set; } = new List<CrmActivity>();
    public ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
}

/// <summary>CRM follow-up / activity linked to a lead or customer.</summary>
public class CrmActivity : CompanyEntity
{
    public CrmActivityType Type { get; set; } = CrmActivityType.Task;
    public string Subject { get; set; } = string.Empty;
    public DateTime? DueAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? AssignedToUserId { get; set; }
    public AppUser? AssignedToUser { get; set; }
    public string? Notes { get; set; }
    public string? AttachmentPath { get; set; }
    public string? AttachmentName { get; set; }
    /// <summary>Set when CRM DueAt warn notification was sent (once).</summary>
    public DateTime? SlaWarnedAt { get; set; }
}

/// <summary>Light sales opportunity / pipeline deal (W3 links quotations).</summary>
public class Opportunity : CompanyEntity
{
    public string Name { get; set; } = string.Empty;
    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public OpportunityStage Stage { get; set; } = OpportunityStage.Prospect;
    public decimal Value { get; set; }
    public DateTime? ExpectedCloseDate { get; set; }
    public int? QuotationId { get; set; }
    public SalesQuotation? Quotation { get; set; }
    public string? LostReason { get; set; }
    public string? WinReason { get; set; }
    public int Probability { get; set; }
    public DateTime? StageChangedAt { get; set; }
    public ICollection<OpportunityStageHistory> StageHistory { get; set; } = new List<OpportunityStageHistory>();
}

/// <summary>Audit of opportunity stage changes (CRM W2).</summary>
public class OpportunityStageHistory : CompanyEntity
{
    public int OpportunityId { get; set; }
    public Opportunity Opportunity { get; set; } = null!;
    public OpportunityStage FromStage { get; set; }
    public OpportunityStage ToStage { get; set; }
    public string? ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
}

/// <summary>Light lead assignment rule (CRM W5).</summary>
public class CrmAssignmentRule : CompanyEntity
{
    public string? Source { get; set; }
    public int? OwnerUserId { get; set; }
    public AppUser? OwnerUser { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Email template stub (CRM W5) — copy in UI; SMTP send deferred.</summary>
public class CrmEmailTemplate : CompanyEntity
{
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
