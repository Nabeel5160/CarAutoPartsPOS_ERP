using CarAutoParts.Domain.Common;

namespace CarAutoParts.Domain.Entities;

public enum ApprovalRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3
}

/// <summary>Company rule: documents at or above MinAmount need a second approver with RequiredPermission.</summary>
public class ApprovalPolicy : CompanyEntity
{
    public string DocumentType { get; set; } = string.Empty;
    public decimal MinAmount { get; set; }
    public string RequiredPermission { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}

public class ApprovalRequest : CompanyEntity
{
    public string DocumentType { get; set; } = string.Empty;
    public int DocumentId { get; set; }
    public string? DocumentNumber { get; set; }
    public decimal Amount { get; set; }
    public ApprovalRequestStatus Status { get; set; } = ApprovalRequestStatus.Pending;
    public int RequestedByUserId { get; set; }
    public string RequestedByUserName { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public int? DecidedByUserId { get; set; }
    public string? DecidedByUserName { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNotes { get; set; }
    public int? PolicyId { get; set; }
    public ApprovalPolicy? Policy { get; set; }
}
