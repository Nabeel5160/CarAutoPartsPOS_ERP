using CarAutoParts.Application.Finance;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CarAutoParts.Application.Tests;

public class PeriodLockIntegrationTests
{
    private static (ApplicationDbContext Db, CurrentCompanyContext Company, FinanceHandlers Handlers) CreateSut()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var company = new CurrentCompanyContext();
        company.Set(1);
        var db = new ApplicationDbContext(options, company);
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        db.AccountingPeriods.Add(new AccountingPeriod
        {
            Id = 1,
            CompanyId = 1,
            FiscalYearId = 1,
            Name = "Jan",
            PeriodNumber = 1,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 1, 31),
            IsClosed = true
        });
        db.GlAccounts.AddRange(
            new GlAccount { Id = 1, CompanyId = 1, Code = "1100", Name = "Cash", AccountType = AccountType.Asset, IsPostable = true },
            new GlAccount { Id = 2, CompanyId = 1, Code = "4100", Name = "Sales", AccountType = AccountType.Revenue, IsPostable = true });
        db.NumberSequences.Add(new NumberSequence
        {
            CompanyId = 1,
            DocumentType = "JV",
            Prefix = "JV-",
            NextValue = 1,
            Padding = 4
        });
        db.SaveChanges();

        var handlers = new FinanceHandlers(new FinanceDbAdapter(db), company);
        return (db, company, handlers);
    }

    [Fact]
    public async Task PostJournal_fails_when_period_closed()
    {
        var (db, _, handlers) = CreateSut();
        var create = await handlers.Handle(new CreateJournalCommand(
            new DateTime(2026, 1, 10),
            "REF",
            "Test",
            [
                new JournalLineDto(1, "Dr", 50, 0, null),
                new JournalLineDto(2, "Cr", 0, 50, null)
            ]), CancellationToken.None);

        create.Succeeded.Should().BeTrue();
        var journalId = create.Data!.Id;

        var post = await handlers.Handle(new PostJournalCommand(journalId), CancellationToken.None);
        post.Succeeded.Should().BeFalse();
        post.Error.Should().Contain("open accounting period");
    }

    [Fact]
    public async Task Company_filter_isolates_accounts()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var company = new CurrentCompanyContext();
        company.Set(1);
        await using var db = new ApplicationDbContext(options, company);
        db.GlAccounts.Add(new GlAccount { CompanyId = 1, Code = "A1", Name = "C1", AccountType = AccountType.Asset });
        db.GlAccounts.Add(new GlAccount { CompanyId = 2, Code = "A2", Name = "C2", AccountType = AccountType.Asset });
        await db.SaveChangesAsync();

        var visible = await db.GlAccounts.Select(a => a.Code).ToListAsync();
        visible.Should().ContainSingle().Which.Should().Be("A1");
    }
}
