namespace CarAutoParts.Web.Models;

public sealed class CompanyDto
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Ntn { get; set; }
    public string CurrencyCode { get; set; } = "PKR";
    public bool IsActive { get; set; }
}

public sealed class BranchDto
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
}

public sealed class GlAccountDto
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int AccountType { get; set; }
    public int? ParentAccountId { get; set; }
    public bool IsPostable { get; set; }
    public bool IsActive { get; set; }
}

public sealed class AccountingPeriodDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int PeriodNumber { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
    public int FiscalYearId { get; set; }
}

public sealed class JournalLineDto
{
    public int AccountId { get; set; }
    public string? Description { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public int? CostCenterId { get; set; }
}

public sealed class JournalDto
{
    public int Id { get; set; }
    public string JournalNumber { get; set; } = "";
    public DateTime JournalDate { get; set; }
    public int Status { get; set; }
    public string? Reference { get; set; }
    public string? Description { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public List<JournalLineDto> Lines { get; set; } = [];
}

public sealed class TrialBalanceLineDto
{
    public int AccountId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public sealed class TrialBalanceReportDto
{
    public DateTime AsOfDate { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public List<TrialBalanceLineDto> Lines { get; set; } = [];
}

public sealed class ProfitAndLossLineDto
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int AccountType { get; set; }
    public decimal Amount { get; set; }
}

public sealed class ProfitAndLossReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetProfit { get; set; }
    public List<ProfitAndLossLineDto> Lines { get; set; } = [];
}

public sealed class BalanceSheetLineDto
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int AccountType { get; set; }
    public decimal Balance { get; set; }
}

public sealed class BalanceSheetReportDto
{
    public DateTime AsOfDate { get; set; }
    public decimal TotalAssets { get; set; }
    public decimal TotalLiabilities { get; set; }
    public decimal TotalEquity { get; set; }
    public List<BalanceSheetLineDto> Lines { get; set; } = [];
}
