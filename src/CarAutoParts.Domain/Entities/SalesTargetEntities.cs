using CarAutoParts.Domain.Common;

namespace CarAutoParts.Domain.Entities;

/// <summary>Monthly sales target assigned to a salesperson (Program B — sales thin).</summary>
public class SalesTarget : CompanyEntity
{
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public decimal TargetAmount { get; set; }
    public string? Notes { get; set; }
}
