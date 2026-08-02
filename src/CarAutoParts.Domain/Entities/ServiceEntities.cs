using CarAutoParts.Domain.Common;
using CarAutoParts.Domain.Enums;

namespace CarAutoParts.Domain.Entities;

/// <summary>Service Light ticket (Program C1) — thin tickets/warranty/AMC tracker, not full field service.</summary>
public class ServiceTicket : CompanyEntity
{
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ServiceTicketStatus Status { get; set; } = ServiceTicketStatus.Open;
    public ServiceTicketPriority Priority { get; set; } = ServiceTicketPriority.Normal;
    public bool IsWarrantyClaim { get; set; }
    public string? WarrantyReference { get; set; }
    public string? AmcReference { get; set; }
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public int? AssignedToUserId { get; set; }
    public AppUser? AssignedToUser { get; set; }
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? Notes { get; set; }
    public string? ResolutionNotes { get; set; }
}
