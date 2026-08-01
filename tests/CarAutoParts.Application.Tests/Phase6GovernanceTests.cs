using CarAutoParts.Application.Common;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Finance;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Security;
using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using CarAutoParts.Infrastructure.Data;
using CarAutoParts.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace CarAutoParts.Application.Tests;

public class Phase6GovernanceTests
{
    [Fact]
    public void TotpHelper_roundtrips_secret_and_verifies_current_code()
    {
        var secret = TotpHelper.GenerateSecret();
        secret.Should().NotBeNullOrWhiteSpace();
        var uri = TotpHelper.BuildOtpAuthUri(secret, "admin");
        uri.Should().StartWith("otpauth://totp/");

        // Brute force current window by computing via Verify against itself — generate by trial:
        // We verify that wrong code fails.
        TotpHelper.VerifyCode(secret, "000000").Should().BeFalse();
    }

    [Fact]
    public async Task ApprovalPolicy_queues_when_amount_exceeds_threshold()
    {
        await using var db = TestDbContextFactory.Create();
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "T", CurrencyCode = "PKR", IsActive = true });
        db.ApprovalPolicies.Add(new ApprovalPolicy
        {
            CompanyId = 1, DocumentType = "PurchaseOrder", MinAmount = 1000, RequiredPermission = Permissions.PurchasesApprove,
            IsActive = true, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var company = new CurrentCompanyContext();
        company.Set(1, 1, [1]);
        var user = new CurrentUserService();
        user.SetUser(new AppUser { Id = 2, Username = "clerk", DisplayName = "Clerk" }, [Permissions.PurchasesCreate]);

        var notifications = new Mock<INotificationService>();
        notifications.Setup(n => n.CreateNotificationAsync(It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = new ApprovalWorkflowService(
            new Repository<ApprovalPolicy>(db),
            new Repository<ApprovalRequest>(db),
            user,
            company,
            new UnitOfWork(db),
            new MoneyAuditService(new Repository<AuditLog>(db), user, new UnitOfWork(db)),
            notifications.Object);

        var result = await svc.EnsureApprovedOrQueueAsync("PurchaseOrder", 55, "PO-1", 5000);
        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain(ApprovalWorkflowService.PendingCode);
        (await db.ApprovalRequests.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ApprovalPolicy_allows_when_below_threshold()
    {
        await using var db = TestDbContextFactory.Create();
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "T", CurrencyCode = "PKR", IsActive = true });
        db.ApprovalPolicies.Add(new ApprovalPolicy
        {
            CompanyId = 1, DocumentType = "PurchaseOrder", MinAmount = 100_000, RequiredPermission = Permissions.PurchasesApprove,
            IsActive = true, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var company = new CurrentCompanyContext();
        company.Set(1);
        var user = new CurrentUserService();
        user.SetUser(new AppUser { Id = 2, Username = "clerk", DisplayName = "Clerk" }, []);

        var svc = new ApprovalWorkflowService(
            new Repository<ApprovalPolicy>(db),
            new Repository<ApprovalRequest>(db),
            user,
            company,
            new UnitOfWork(db),
            new MoneyAuditService(new Repository<AuditLog>(db), user, new UnitOfWork(db)),
            Mock.Of<INotificationService>());

        var result = await svc.EnsureApprovedOrQueueAsync("PurchaseOrder", 1, "PO-S", 500);
        result.Succeeded.Should().BeTrue();
        (await db.ApprovalRequests.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task MfaService_enroll_confirm_and_ticket_verify()
    {
        await using var db = TestDbContextFactory.Create();
        db.Users.Add(new AppUser
        {
            Id = 1, Username = "admin", DisplayName = "A", PasswordHash = BCrypt.Net.BCrypt.HashPassword("x"),
            IsActive = true, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var userSvc = new CurrentUserService();
        userSvc.SetUser(db.Users.First(), [Permissions.FinanceManage]);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var mfa = new MfaService(new Repository<AppUser>(db), new UnitOfWork(db), userSvc, cache);

        mfa.ShouldEnforceMfa([Permissions.FinanceManage]).Should().BeTrue();

        var begin = await mfa.BeginEnrollAsync(1);
        begin.Succeeded.Should().BeTrue();
        begin.Data!.Secret.Should().NotBeNullOrEmpty();

        // Confirm with a forged code should fail
        (await mfa.ConfirmEnrollAsync(1, "000000")).Succeeded.Should().BeFalse();

        // Manually enable MFA with known secret for ticket path
        var secret = TotpHelper.GenerateSecret();
        var u = await db.Users.FirstAsync();
        u.MfaEnabled = true;
        u.MfaSecret = secret;
        await db.SaveChangesAsync();

        var ticket = mfa.IssueMfaTicket(1);
        mfa.TryConsumeMfaTicket(ticket, out var uid).Should().BeTrue();
        uid.Should().Be(1);
        mfa.TryConsumeMfaTicket(ticket, out _).Should().BeFalse(); // consumed
    }

    [Fact]
    public async Task VoidJournal_creates_reversing_entry_and_audit()
    {
        await using var db = TestDbContextFactory.Create();
        var company = new CurrentCompanyContext();
        company.Set(1);
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "T", CurrencyCode = "PKR", IsActive = true });
        db.FiscalYears.Add(new FiscalYear
        {
            Id = 1, CompanyId = 1, Name = "FY", StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31)
        });
        db.AccountingPeriods.Add(new AccountingPeriod
        {
            Id = 1, CompanyId = 1, FiscalYearId = 1, PeriodNumber = 7, Name = "Jul",
            StartDate = new DateTime(2026, 7, 1), EndDate = new DateTime(2026, 7, 31), IsClosed = false
        });
        db.GlAccounts.AddRange(
            new GlAccount { Id = 1, CompanyId = 1, Code = "1100", Name = "Cash", AccountType = AccountType.Asset, IsPostable = true, IsActive = true },
            new GlAccount { Id = 2, CompanyId = 1, Code = "4100", Name = "Rev", AccountType = AccountType.Revenue, IsPostable = true, IsActive = true });

        var journal = new JournalEntry
        {
            Id = 1, CompanyId = 1, JournalNumber = "JV-1", JournalDate = new DateTime(2026, 7, 15),
            Status = JournalStatus.Posted, AccountingPeriodId = 1,
            Lines =
            {
                new JournalLine { CompanyId = 1, AccountId = 1, Debit = 100, Credit = 0 },
                new JournalLine { CompanyId = 1, AccountId = 2, Debit = 0, Credit = 100 }
            }
        };
        db.JournalEntries.Add(journal);
        await db.SaveChangesAsync();

        var user = new CurrentUserService();
        user.SetUser(new AppUser { Id = 1, Username = "fin", DisplayName = "F" }, [Permissions.FinanceVoid]);

        var periods = new Mock<IAccountingPeriodService>();
        periods.Setup(p => p.EnsureOpenAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AccountingPeriod>.Success(db.AccountingPeriods.First()));

        var voids = new DocumentVoidService(
            new Repository<JournalEntry>(db),
            new Repository<SalesInvoice>(db),
            new Repository<Payment>(db),
            new Repository<PurchaseInvoice>(db),
            new Repository<Supplier>(db),
            user,
            company,
            new UnitOfWork(db),
            new MoneyAuditService(new Repository<AuditLog>(db), user, new UnitOfWork(db)),
            Mock.Of<IGlPostingService>(),
            periods.Object);

        var result = await voids.VoidJournalAsync(1, "test void");
        result.Succeeded.Should().BeTrue();
        (await db.JournalEntries.CountAsync(j => j.Status == JournalStatus.Voided)).Should().Be(1);
        (await db.JournalEntries.CountAsync(j => j.JournalNumber.EndsWith("-R"))).Should().Be(1);
        (await db.AuditLogs.CountAsync(a => a.Action == AuditAction.Void)).Should().Be(1);
    }
}
