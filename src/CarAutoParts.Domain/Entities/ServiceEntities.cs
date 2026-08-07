using CarAutoParts.Domain.Common;
using CarAutoParts.Domain.Enums;

namespace CarAutoParts.Domain.Entities;

/// <summary>Service Light ticket (Program C1+) — tickets, warranty, AMC, SLA; visits/parts in Phase 8 depth.</summary>
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
    public WarrantyClaimStatus WarrantyClaimStatus { get; set; } = WarrantyClaimStatus.None;
    public string? WarrantyDecisionNotes { get; set; }
    public DateTime? WarrantyDecidedAt { get; set; }
    public string? WarrantyDecidedBy { get; set; }
    /// <summary>Optional invoice the warranty claim refers to (Phase 8 depth).</summary>
    public int? WarrantySalesInvoiceId { get; set; }
    public SalesInvoice? WarrantySalesInvoice { get; set; }
    public int? ReplacementProductId { get; set; }
    public Product? ReplacementProduct { get; set; }
    public decimal ReplacementQuantity { get; set; }
    public string? WarrantyEvidenceNotes { get; set; }
    public string? AmcReference { get; set; }
    public int? AmcContractId { get; set; }
    public AmcContract? AmcContract { get; set; }
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
    /// <summary>Policy snapshot at open (stable if policy later changes).</summary>
    public int? SlaPolicyId { get; set; }
    public SlaPolicy? SlaPolicy { get; set; }
    public ICollection<ServiceVisit> Visits { get; set; } = new List<ServiceVisit>();
    public ICollection<ServiceTicketPart> Parts { get; set; } = new List<ServiceTicketPart>();
}

/// <summary>AMC / annual maintenance contract (Phase 8 Wave 1).</summary>
public class AmcContract : CompanyEntity
{
    public string ContractNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public AmcContractStatus Status { get; set; } = AmcContractStatus.Draft;
    public string? CoverageNotes { get; set; }
    public decimal? AnnualAmount { get; set; }
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
}

/// <summary>Technician visit scheduled against a service ticket (Phase 8 Wave 2).</summary>
public class ServiceVisit : CompanyEntity
{
    public int ServiceTicketId { get; set; }
    public ServiceTicket ServiceTicket { get; set; } = null!;
    public int AssignedToUserId { get; set; }
    public AppUser AssignedToUser { get; set; } = null!;
    public DateTime ScheduledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public ServiceVisitStatus Status { get; set; } = ServiceVisitStatus.Scheduled;
    public string? Notes { get; set; }
}

/// <summary>Parts consumed on a service ticket (Phase 8 Wave 2 — thin inventory deduct).</summary>
public class ServiceTicketPart : CompanyEntity
{
    public int ServiceTicketId { get; set; }
    public ServiceTicket ServiceTicket { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public DateTime ConsumedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Service knowledge base article stub — internal staff articles, not a customer portal.</summary>
public class KbArticle : CompanyEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? Tags { get; set; }
    public bool IsPublished { get; set; } = true;
}
