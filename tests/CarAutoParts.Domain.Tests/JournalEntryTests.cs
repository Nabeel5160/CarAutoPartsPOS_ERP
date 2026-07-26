using CarAutoParts.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace CarAutoParts.Domain.Tests;

public class JournalEntryTests
{
    [Fact]
    public void EnsureBalanced_rejects_unbalanced_journal()
    {
        var journal = new JournalEntry
        {
            CompanyId = 1,
            JournalNumber = "JV-1",
            Lines =
            {
                new JournalLine { CompanyId = 1, AccountId = 1, Debit = 100, Credit = 0 },
                new JournalLine { CompanyId = 1, AccountId = 2, Debit = 0, Credit = 50 }
            }
        };

        var act = () => journal.EnsureBalanced();
        act.Should().Throw<InvalidOperationException>().WithMessage("*unbalanced*");
    }

    [Fact]
    public void Post_rejects_closed_period()
    {
        var journal = new JournalEntry
        {
            CompanyId = 1,
            JournalNumber = "JV-2",
            JournalDate = new DateTime(2026, 1, 15),
            Lines =
            {
                new JournalLine { CompanyId = 1, AccountId = 1, Debit = 100, Credit = 0 },
                new JournalLine { CompanyId = 1, AccountId = 2, Debit = 0, Credit = 100 }
            }
        };
        var period = new AccountingPeriod
        {
            Id = 1,
            CompanyId = 1,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 31),
            IsClosed = true
        };

        var act = () => journal.Post(period);
        act.Should().Throw<InvalidOperationException>().WithMessage("*closed*");
    }

    [Fact]
    public void Post_succeeds_for_open_balanced_period()
    {
        var journal = new JournalEntry
        {
            CompanyId = 1,
            JournalNumber = "JV-3",
            JournalDate = new DateTime(2026, 1, 15),
            Lines =
            {
                new JournalLine { CompanyId = 1, AccountId = 1, Debit = 100, Credit = 0 },
                new JournalLine { CompanyId = 1, AccountId = 2, Debit = 0, Credit = 100 }
            }
        };
        var period = new AccountingPeriod
        {
            Id = 9,
            CompanyId = 1,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 31),
            IsClosed = false
        };

        journal.Post(period);
        journal.Status.Should().Be(JournalStatus.Posted);
        journal.AccountingPeriodId.Should().Be(9);
        journal.DomainEvents.Should().ContainSingle(e => e is JournalPostedEvent);
    }
}
