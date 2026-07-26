using CarAutoParts.Domain.Common;

namespace CarAutoParts.Domain.Entities;

public enum AccountType
{
    Asset = 0,
    Liability = 1,
    Equity = 2,
    Revenue = 3,
    Expense = 4,
    CostOfGoods = 5
}

public enum JournalStatus
{
    Draft = 0,
    Posted = 1,
    Voided = 2
}

public class GlAccount : CompanyEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public int? ParentAccountId { get; set; }
    public GlAccount? ParentAccount { get; set; }
    public ICollection<GlAccount> Children { get; set; } = new List<GlAccount>();
    public bool IsPostable { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string? CurrencyCode { get; set; }
}

/// <summary>Maps operational document events to GL accounts.</summary>
public class AccountMapping : CompanyEntity
{
    public string DocumentType { get; set; } = string.Empty;
    public string MappingKey { get; set; } = string.Empty;
    public int AccountId { get; set; }
    public GlAccount Account { get; set; } = null!;
}

public class JournalEntry : AggregateRoot
{
    public string JournalNumber { get; set; } = string.Empty;
    public DateTime JournalDate { get; set; } = DateTime.UtcNow.Date;
    public int? AccountingPeriodId { get; set; }
    public AccountingPeriod? AccountingPeriod { get; set; }
    public string? Reference { get; set; }
    public string? Description { get; set; }
    public JournalStatus Status { get; set; } = JournalStatus.Draft;
    public string? SourceDocumentType { get; set; }
    public int? SourceDocumentId { get; set; }
    public ICollection<JournalLine> Lines { get; set; } = new List<JournalLine>();

    public decimal TotalDebit => Lines.Sum(l => l.Debit);
    public decimal TotalCredit => Lines.Sum(l => l.Credit);

    public void EnsureBalanced()
    {
        if (Lines.Count < 2)
            throw new InvalidOperationException("Journal must have at least two lines.");
        if (TotalDebit != TotalCredit)
            throw new InvalidOperationException($"Journal is unbalanced: Debit {TotalDebit} != Credit {TotalCredit}.");
        if (TotalDebit <= 0)
            throw new InvalidOperationException("Journal totals must be greater than zero.");
    }

    public void Post(AccountingPeriod period)
    {
        if (Status == JournalStatus.Posted)
            throw new InvalidOperationException("Journal already posted.");
        if (period.IsClosed || period.CompanyId != CompanyId)
            throw new InvalidOperationException("Accounting period is closed or invalid.");
        if (JournalDate.Date < period.StartDate.Date || JournalDate.Date > period.EndDate.Date)
            throw new InvalidOperationException("Journal date is outside the accounting period.");

        EnsureBalanced();
        AccountingPeriodId = period.Id;
        Status = JournalStatus.Posted;
        Raise(new JournalPostedEvent(CompanyId, Id, JournalNumber, TotalDebit));
    }
}

public class JournalLine : CompanyEntity
{
    public int JournalEntryId { get; set; }
    public JournalEntry JournalEntry { get; set; } = null!;
    public int AccountId { get; set; }
    public GlAccount Account { get; set; } = null!;
    public int? CostCenterId { get; set; }
    public CostCenter? CostCenter { get; set; }
    public string? Description { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public sealed class JournalPostedEvent : DomainEventBase
{
    public JournalPostedEvent(int companyId, int journalEntryId, string journalNumber, decimal amount)
    {
        CompanyId = companyId;
        JournalEntryId = journalEntryId;
        JournalNumber = journalNumber;
        Amount = amount;
    }

    public int CompanyId { get; }
    public int JournalEntryId { get; }
    public string JournalNumber { get; }
    public decimal Amount { get; }
}
