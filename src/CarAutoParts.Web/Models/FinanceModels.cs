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

public sealed class CashFlowLineDto
{
    public int JournalEntryId { get; set; }
    public string JournalNumber { get; set; } = "";
    public DateTime JournalDate { get; set; }
    public string Category { get; set; } = "";
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}

public sealed class CashFlowReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal OpeningCash { get; set; }
    public decimal OperatingActivities { get; set; }
    public decimal InvestingActivities { get; set; }
    public decimal FinancingActivities { get; set; }
    public decimal NetChangeInCash { get; set; }
    public decimal ClosingCash { get; set; }
    public List<CashFlowLineDto> Lines { get; set; } = [];
}

public sealed class PeriodCloseChecklistItemDto
{
    public string Code { get; set; } = "";
    public string Label { get; set; } = "";
    public int Count { get; set; }
    public bool IsBlocker { get; set; }
    public string Severity { get; set; } = "";
}

public sealed class PeriodCloseChecklistDto
{
    public int PeriodId { get; set; }
    public string PeriodName { get; set; } = "";
    public bool CanClose { get; set; }
    public bool RequiresForceClose { get; set; }
    public List<PeriodCloseChecklistItemDto> Items { get; set; } = [];
}

public sealed class OpeningBalanceBatchDto
{
    public int Id { get; set; }
    public string BatchNumber { get; set; } = "";
    public DateTime CutoverDate { get; set; }
    public string Status { get; set; } = "";
    public int? JournalEntryId { get; set; }
    public string? Notes { get; set; }
}

public sealed class BankStatementLineDto
{
    public int Id { get; set; }
    public DateTime LineDate { get; set; }
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
    public string? Description { get; set; }
    public bool IsCleared { get; set; }
    public int? MatchedJournalLineId { get; set; }
}

public sealed class BankStatementDto
{
    public int Id { get; set; }
    public string StatementNumber { get; set; } = "";
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public string Status { get; set; } = "";
    public string? Notes { get; set; }
    public List<BankStatementLineDto> Lines { get; set; } = [];
}

public sealed class UnclearedBankGlLineDto
{
    public int JournalLineId { get; set; }
    public int JournalEntryId { get; set; }
    public string JournalNumber { get; set; } = "";
    public DateTime JournalDate { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Description { get; set; }
}

public sealed class BankReconReportDto
{
    public int StatementId { get; set; }
    public string StatementNumber { get; set; } = "";
    public decimal StatementClosingBalance { get; set; }
    public decimal GlBankBalance { get; set; }
    public decimal UnclearedStatementTotal { get; set; }
    public decimal UnclearedGlTotal { get; set; }
    public decimal Difference { get; set; }
    public List<BankStatementLineDto> UnclearedStatementLines { get; set; } = [];
    public List<UnclearedBankGlLineDto> UnclearedGlLines { get; set; } = [];
}
