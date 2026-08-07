using CarAutoParts.Domain.Common;

namespace CarAutoParts.Domain.Entities;

public enum BudgetStatus
{
    Draft = 0,
    Active = 1,
    Closed = 2
}

/// <summary>GL budget header for a fiscal year (Program C2 — finance depth).</summary>
public class Budget : CompanyEntity
{
    public string Name { get; set; } = string.Empty;
    public int FiscalYearId { get; set; }
    public FiscalYear FiscalYear { get; set; } = null!;
    public BudgetStatus Status { get; set; } = BudgetStatus.Draft;
    public string? Notes { get; set; }
    public ICollection<BudgetLine> Lines { get; set; } = new List<BudgetLine>();
}

/// <summary>Period × account (± cost center) budget amount.</summary>
public class BudgetLine : CompanyEntity
{
    public int BudgetId { get; set; }
    public Budget Budget { get; set; } = null!;
    public int GlAccountId { get; set; }
    public GlAccount GlAccount { get; set; } = null!;
    public int? CostCenterId { get; set; }
    public CostCenter? CostCenter { get; set; }
    public int AccountingPeriodId { get; set; }
    public AccountingPeriod AccountingPeriod { get; set; } = null!;
    public decimal Amount { get; set; }
}
