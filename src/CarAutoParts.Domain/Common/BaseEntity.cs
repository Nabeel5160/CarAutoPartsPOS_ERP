namespace CarAutoParts.Domain.Common;

/// <summary>Marks an entity as owned by a company (multi-company filter).</summary>
public interface ICompanyOwned
{
    int CompanyId { get; set; }
}

/// <summary>Optimistic concurrency token.</summary>
public interface IHasRowVersion
{
    byte[] RowVersion { get; set; }
}

/// <summary>Base entity with audit, soft-delete, and concurrency.</summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>Company-scoped base entity.</summary>
public abstract class CompanyEntity : BaseEntity, ICompanyOwned
{
    public int CompanyId { get; set; }
}
