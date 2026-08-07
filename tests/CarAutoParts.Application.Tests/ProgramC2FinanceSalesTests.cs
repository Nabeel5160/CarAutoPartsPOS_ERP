using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Finance;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Infrastructure.Data;
using FluentAssertions;
using Moq;
using Xunit;

namespace CarAutoParts.Application.Tests;

/// <summary>Program C2 — budgets vs actual, bank auto-match scoring, sales commission + target attainment.</summary>
public class ProgramC2FinanceSalesTests
{
    private static async Task<(ApplicationDbContext Db, CurrentCompanyContext Company, EnterpriseDbAdapter Ent)> SeedFinanceAsync()
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        var db = TestDbContextFactory.Create();
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        var fy = new FiscalYear
        {
            Id = 1, CompanyId = 1, Name = "FY2026",
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31)
        };
        db.FiscalYears.Add(fy);
        db.AccountingPeriods.Add(new AccountingPeriod
        {
            Id = 1, CompanyId = 1, FiscalYearId = 1, Name = "Jan 2026", PeriodNumber = 1,
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 1, 31)
        });
        db.GlAccounts.Add(new GlAccount
        {
            Id = 1, CompanyId = 1, Code = "5100", Name = "Rent Expense",
            AccountType = AccountType.Expense, IsPostable = true, IsActive = true
        });
        db.GlAccounts.Add(new GlAccount
        {
            Id = 2, CompanyId = 1, Code = "1110", Name = "Bank",
            AccountType = AccountType.Asset, IsPostable = true, IsActive = true
        });
        await db.SaveChangesAsync();
        return (db, company, new EnterpriseDbAdapter(db));
    }

    [Fact]
    public async Task Budget_VsActual_Uses_Posted_Journal_Lines()
    {
        var (db, company, ent) = await SeedFinanceAsync();
        var budgets = new BudgetService(ent, company);

        var created = await budgets.CreateAsync(new BudgetCreateRequest(
            "OpEx 2026", 1, null,
            [new BudgetLineUpsertRequest(1, 1, 10_000m)]));
        created.Succeeded.Should().BeTrue();
        created.Data!.Lines[0].ActualAmount.Should().Be(0);

        var journal = new JournalEntry
        {
            CompanyId = 1,
            JournalNumber = "JV-1",
            JournalDate = new DateTime(2026, 1, 15),
            Status = JournalStatus.Posted,
            AccountingPeriodId = 1
        };
        journal.Lines.Add(new JournalLine
        {
            CompanyId = 1, AccountId = 1, Debit = 4_000m, Credit = 0
        });
        journal.Lines.Add(new JournalLine
        {
            CompanyId = 1, AccountId = 2, Debit = 0, Credit = 4_000m
        });
        db.JournalEntries.Add(journal);
        await db.SaveChangesAsync();

        var detail = await budgets.GetByIdAsync(created.Data.Id);
        detail.Should().NotBeNull();
        detail!.Lines[0].ActualAmount.Should().Be(4_000m);
        detail.Lines[0].Variance.Should().Be(6_000m);
    }

    [Fact]
    public async Task Bank_SuggestMatches_Scores_Amount_And_Date()
    {
        var (db, company, ent) = await SeedFinanceAsync();
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.HasPermission(It.IsAny<string>())).Returns(true);
        var inventory = new Mock<IInventoryService>();
        var periods = new Mock<IAccountingPeriodService>();
        var reports = new Mock<IFinancialReportService>();
        var approvals = new Mock<IApprovalWorkflowService>();
        var phase4 = new Phase4FinanceService(
            ent, company, inventory.Object, periods.Object, reports.Object, user.Object, approvals.Object);

        var stmt = await phase4.CreateBankStatementAsync(new CreateBankStatementRequest(
            new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), 0, 100, null));
        stmt.Succeeded.Should().BeTrue();

        var line = await phase4.AddBankStatementLineAsync(stmt.Data!.Id, new CreateBankStatementLineRequest(
            new DateTime(2026, 1, 10), 500m, "REF1", "Deposit"));
        line.Succeeded.Should().BeTrue();

        var journal = new JournalEntry
        {
            CompanyId = 1,
            JournalNumber = "JV-BANK",
            JournalDate = new DateTime(2026, 1, 11),
            Status = JournalStatus.Posted
        };
        journal.Lines.Add(new JournalLine { CompanyId = 1, AccountId = 2, Debit = 500m, Credit = 0, Description = "REF1 deposit" });
        journal.Lines.Add(new JournalLine { CompanyId = 1, AccountId = 1, Debit = 0, Credit = 500m });
        db.JournalEntries.Add(journal);
        await db.SaveChangesAsync();

        var suggestions = await phase4.SuggestBankMatchesAsync(stmt.Data.Id);
        suggestions.Should().HaveCount(1);
        suggestions[0].Score.Should().BeGreaterThanOrEqualTo(70);
        suggestions[0].Reason.Should().Contain("amount");

        var auto = await phase4.AutoMatchBankAsync(stmt.Data.Id);
        auto.Succeeded.Should().BeTrue();
        auto.Data!.MatchedCount.Should().Be(1);
    }

    [Fact]
    public async Task SalesCommission_And_Target_Attainment()
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        var db = TestDbContextFactory.Create();
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        db.Users.Add(new AppUser
        {
            Id = 1, Username = "sales1", PasswordHash = "x", IsActive = true,
            CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        });
        db.Customers.Add(new Customer
        {
            Id = 1, Name = "Dealer", IsActive = true, CommissionPercent = 5m,
            CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        });
        await db.SaveChangesAsync();

        var ent = new EnterpriseDbAdapter(db);
        var currentUser = new CurrentUserService();
        currentUser.SetUser(db.Users.First(), ["sales.view"]);
        var commissions = new SalesCommissionService(ent, company, currentUser);
        var targets = new SalesTargetService(ent, company);

        var invoice = new SalesInvoice
        {
            InvoiceNumber = "INV-1",
            CustomerId = 1,
            InvoiceDate = new DateTime(2026, 3, 15),
            GrandTotal = 1_000m,
            SubTotal = 1_000m
        };
        db.SalesInvoices.Add(invoice);
        await db.SaveChangesAsync();

        await commissions.RecordForInvoiceAsync(invoice);
        var rows = await commissions.GetAsync();
        rows.Should().HaveCount(1);
        rows[0].CommissionAmount.Should().Be(50m);
        rows[0].UserId.Should().Be(1);

        var target = await targets.CreateAsync(new SalesTargetUpsertRequest(1, 2026, 3, 2_000m));
        target.Succeeded.Should().BeTrue();
        target.Data!.ActualAmount.Should().Be(1_000m);
        target.Data.AttainmentPercent.Should().Be(50m);
    }
}
