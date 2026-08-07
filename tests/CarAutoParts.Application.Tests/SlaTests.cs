using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Service;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using CarAutoParts.Infrastructure.Data;
using CarAutoParts.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CarAutoParts.Application.Tests;

public class SlaPolicyTests
{
    [Fact]
    public async Task EnsureDefault_Creates_Policy_With_Targets()
    {
        var (policies, _) = await SeedAsync();
        var list = await policies.ListAsync();
        list.Should().ContainSingle(p => p.IsDefault);
        list[0].Targets.Should().HaveCount(8);
    }

    [Fact]
    public async Task Policies_Respect_Company_Filter()
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        await using var db = CreateDb(company);
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        await db.SaveChangesAsync();

        var svc1 = CreatePolicySvc(db, company);
        await svc1.EnsureDefaultPolicyAsync(1);

        company.Set(2);
        db.Companies.Add(new Company { Id = 2, Code = "U", Name = "Other", CurrencyCode = "PKR", IsActive = true });
        await db.SaveChangesAsync();
        var svc2 = CreatePolicySvc(db, company);
        await svc2.EnsureDefaultPolicyAsync(2);

        company.Set(1);
        var list = await CreatePolicySvc(db, company).ListAsync(SlaEntityType.ServiceTicket);
        list.Should().OnlyContain(p => p.Name.Length > 0);
        (await db.SlaPolicies.IgnoreQueryFilters()
            .CountAsync(p => p.AppliesToEntityType == SlaEntityType.ServiceTicket)).Should().Be(2);
        list.Count.Should().Be(1);
    }

    internal static async Task<(SlaPolicyService Svc, CurrentCompanyContext Company)> SeedAsync()
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        var db = CreateDb(company);
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        await db.SaveChangesAsync();
        return (CreatePolicySvc(db, company), company);
    }

    internal static ApplicationDbContext CreateDb(CurrentCompanyContext company)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options, company);
        db.Database.EnsureCreated();
        return db;
    }

    internal static SlaPolicyService CreatePolicySvc(ApplicationDbContext db, CurrentCompanyContext company)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.CurrentUser).Returns(new AppUser { Id = 1, Username = "admin" });
        return new SlaPolicyService(
            new Repository<SlaPolicy>(db),
            new Repository<SlaPolicyRule>(db),
            new Repository<SlaTarget>(db),
            new Repository<SlaTimer>(db),
            new Repository<BusinessCalendar>(db),
            new Repository<ServiceTicket>(db),
            new Repository<Customer>(db),
            new Repository<AppUser>(db),
            new UnitOfWork(db),
            company,
            user.Object);
    }
}

public class SlaClockTests
{
    [Fact]
    public async Task CreateTicket_Starts_Two_Running_Timers()
    {
        var (ticketSvc, clock, clockTime, db) = await SeedTicketStackAsync();
        var created = await ticketSvc.CreateTicketAsync(new ServiceTicketCreateDto(
            1, "High SLA", null, ServiceTicketPriority.High, false, null, null, null, null, null, null));
        created.Succeeded.Should().BeTrue();

        var sla = await clock.GetTicketSlaAsync(created.Data!.Id);
        sla.Should().NotBeNull();
        sla!.Timers.Should().HaveCount(2);
        sla.Timers.Should().OnlyContain(t => t.Status == SlaTimerStatus.Running);
        (await db.ServiceTickets.FirstAsync()).SlaPolicyId.Should().NotBeNull();
    }

    [Fact]
    public async Task Open_To_InProgress_Marks_FirstResponse_Met()
    {
        var (ticketSvc, clock, _, _) = await SeedTicketStackAsync();
        var created = await ticketSvc.CreateTicketAsync(new ServiceTicketCreateDto(
            1, "FR met", null, ServiceTicketPriority.Normal, false, null, null, null, null, null, null));
        await ticketSvc.ChangeStatusAsync(created.Data!.Id, new ServiceTicketStatusChangeDto(ServiceTicketStatus.InProgress, null));

        var sla = await clock.GetTicketSlaAsync(created.Data.Id);
        var fr = sla!.Timers.Single(t => t.Metric == SlaMetric.FirstResponse);
        fr.Status.Should().Be(SlaTimerStatus.Met);
        sla.Timers.Single(t => t.Metric == SlaMetric.Resolution).Status.Should().Be(SlaTimerStatus.Running);
    }

    [Fact]
    public async Task Resolve_Within_Target_Marks_Resolution_Met()
    {
        var (ticketSvc, clock, _, _) = await SeedTicketStackAsync();
        var created = await ticketSvc.CreateTicketAsync(new ServiceTicketCreateDto(
            1, "Res met", null, ServiceTicketPriority.Urgent, false, null, null, null, null, null, null));
        await ticketSvc.ChangeStatusAsync(created.Data!.Id, new ServiceTicketStatusChangeDto(ServiceTicketStatus.InProgress, null));
        await ticketSvc.ChangeStatusAsync(created.Data.Id, new ServiceTicketStatusChangeDto(ServiceTicketStatus.Resolved, "Fixed"));

        var sla = await clock.GetTicketSlaAsync(created.Data.Id);
        sla!.Timers.Single(t => t.Metric == SlaMetric.Resolution).Status.Should().Be(SlaTimerStatus.Met);
    }

    [Fact]
    public async Task Warranty_Ticket_Prefers_ApplyToWarrantyOnly_Policy()
    {
        var (ticketSvc, clock, _, db) = await SeedTicketStackAsync();
        var company = new CurrentCompanyContext();
        company.Set(1);
        var policySvc = SlaPolicyTests.CreatePolicySvc(db, company);

        var warrantyPolicy = await policySvc.UpsertAsync(new SlaPolicyUpsertDto(
            null, "Warranty SLA", false, true, SlaCalendarMode.AlwaysOn, true, null,
            [
                new(SlaMetric.FirstResponse, ServiceTicketPriority.High, 15, 80),
                new(SlaMetric.Resolution, ServiceTicketPriority.High, 60, 80)
            ]));
        warrantyPolicy.Succeeded.Should().BeTrue();

        var created = await ticketSvc.CreateTicketAsync(new ServiceTicketCreateDto(
            1, "Warranty claim", null, ServiceTicketPriority.High, true, "W-1", null, null, null, null, null));
        created.Succeeded.Should().BeTrue();

        var sla = await clock.GetTicketSlaAsync(created.Data!.Id);
        sla!.SlaPolicyId.Should().Be(warrantyPolicy.Data!.Id);
        sla.PolicyName.Should().Be("Warranty SLA");
    }

    [Fact]
    public async Task Rule_Match_Warranty_And_High_Selects_Named_Policy()
    {
        var (ticketSvc, clock, _, db) = await SeedTicketStackAsync();
        var company = new CurrentCompanyContext();
        company.Set(1);
        var policySvc = SlaPolicyTests.CreatePolicySvc(db, company);
        await policySvc.EnsureDefaultPolicyAsync(1);

        var pipeline = await policySvc.UpsertAsync(new SlaPolicyUpsertDto(
            null, "Pipeline High Warranty", false, true, SlaCalendarMode.AlwaysOn, false, null,
            [
                new(SlaMetric.FirstResponse, ServiceTicketPriority.High, 10, 80),
                new(SlaMetric.Resolution, ServiceTicketPriority.High, 40, 80),
                new(SlaMetric.FirstResponse, ServiceTicketPriority.Normal, 60, 80),
                new(SlaMetric.Resolution, ServiceTicketPriority.Normal, 240, 80)
            ]));
        pipeline.Succeeded.Should().BeTrue();
        pipeline.Data!.IsDefault.Should().BeFalse();

        var rule = await policySvc.UpsertRuleAsync(pipeline.Data!.Id, new SlaPolicyRuleUpsertDto(
            null, ServiceTicketPriority.High, null, null, true, 1, true));
        rule.Succeeded.Should().BeTrue();

        var matched = await ticketSvc.CreateTicketAsync(new ServiceTicketCreateDto(
            1, "Rule match", null, ServiceTicketPriority.High, true, "W-2", null, null, null, null, null));
        matched.Succeeded.Should().BeTrue();
        (await clock.GetTicketSlaAsync(matched.Data!.Id))!.SlaPolicyId.Should().Be(pipeline.Data.Id);

        var unmatched = await ticketSvc.CreateTicketAsync(new ServiceTicketCreateDto(
            1, "Default path", null, ServiceTicketPriority.Normal, false, null, null, null, null, null, null));
        unmatched.Succeeded.Should().BeTrue();
        var defaultSla = await clock.GetTicketSlaAsync(unmatched.Data!.Id);
        defaultSla!.SlaPolicyId.Should().NotBe(pipeline.Data.Id);
        defaultSla.PolicyName.Should().NotBe("Pipeline High Warranty");
    }

    [Fact]
    public async Task Policy_Override_On_Create_Bypasses_Rules()
    {
        var (ticketSvc, clock, _, db) = await SeedTicketStackAsync();
        var company = new CurrentCompanyContext();
        company.Set(1);
        var policySvc = SlaPolicyTests.CreatePolicySvc(db, company);

        var overridePol = await policySvc.UpsertAsync(new SlaPolicyUpsertDto(
            null, "Override Policy", false, true, SlaCalendarMode.AlwaysOn, false, null,
            [
                new(SlaMetric.FirstResponse, ServiceTicketPriority.Normal, 5, 80),
                new(SlaMetric.Resolution, ServiceTicketPriority.Normal, 20, 80)
            ]));
        overridePol.Succeeded.Should().BeTrue();

        var created = await ticketSvc.CreateTicketAsync(new ServiceTicketCreateDto(
            1, "Override", null, ServiceTicketPriority.Normal, false, null, null, null, null, null, null,
            null, null, null, overridePol.Data!.Id));
        created.Succeeded.Should().BeTrue();
        (await clock.GetTicketSlaAsync(created.Data!.Id))!.SlaPolicyId.Should().Be(overridePol.Data.Id);
    }

    [Fact]
    public async Task Pause_Freezes_Elapsed_Resume_Continues()
    {
        var (ticketSvc, clock, clockTime, _) = await SeedTicketStackAsync();
        var start = new DateTime(2026, 8, 7, 10, 0, 0, DateTimeKind.Utc);
        clockTime.UtcNow = start;

        var created = await ticketSvc.CreateTicketAsync(new ServiceTicketCreateDto(
            1, "Pause test", null, ServiceTicketPriority.High, false, null, null, null, null, null, null));

        clockTime.UtcNow = start.AddMinutes(10);
        await clock.PauseAsync(created.Data!.Id, new SlaPauseDto(SlaPauseReason.WaitingOnParts, "awaiting pads"));

        clockTime.UtcNow = start.AddMinutes(40);
        var mid = await clock.GetTicketSlaAsync(created.Data.Id);
        mid!.Timers.Should().OnlyContain(t => t.Status == SlaTimerStatus.Paused);
        var frozen = mid.Timers.First().ElapsedSeconds;
        frozen.Should().BeInRange(595, 605);

        await clock.ResumeAsync(created.Data.Id);
        clockTime.UtcNow = start.AddMinutes(50);
        var after = await clock.GetTicketSlaAsync(created.Data.Id);
        after!.Timers.First(t => t.Metric == SlaMetric.Resolution).ElapsedSeconds.Should().BeInRange(frozen + 595, frozen + 605);
    }

    private static async Task<(ServiceTicketService TicketSvc, SlaClockService Clock, FakeSlaClockTime Time, ApplicationDbContext Db)> SeedTicketStackAsync()
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        var db = SlaPolicyTests.CreateDb(company);
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        db.Customers.Add(new Customer { Id = 1, Name = "Ali Motors", IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "t" });
        await db.SaveChangesAsync();

        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.CurrentUser).Returns(new AppUser { Id = 1, Username = "tech" });
        var notifications = new Mock<INotificationService>();
        notifications.Setup(n => n.CreateNotificationAsync(
                It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var clockTime = new FakeSlaClockTime { UtcNow = DateTime.UtcNow };
        var policySvc = SlaPolicyTests.CreatePolicySvc(db, company);
        var clock = new SlaClockService(
            new Repository<SlaPolicy>(db),
            new Repository<SlaPolicyRule>(db),
            new Repository<SlaTimer>(db),
            new Repository<SlaEvent>(db),
            new Repository<ServiceTicket>(db),
            new Repository<Customer>(db),
            new Repository<BusinessCalendar>(db),
            policySvc,
            new UnitOfWork(db),
            company,
            clockTime,
            NullLogger<SlaClockService>.Instance);

        var ticketSvc = new ServiceTicketService(
            new Repository<ServiceTicket>(db),
            new Repository<Customer>(db),
            new Repository<Product>(db),
            new Repository<AmcContract>(db),
            new Repository<SalesInvoice>(db),
            new Repository<SlaTimer>(db),
            new UnitOfWork(db),
            company,
            user.Object,
            notifications.Object,
            clock);

        return (ticketSvc, clock, clockTime, db);
    }
}

public class SlaMonitorTests
{
    [Fact]
    public async Task Sweep_Warns_Once_Then_Breaches_Once()
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        var db = SlaPolicyTests.CreateDb(company);
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        db.Customers.Add(new Customer { Id = 1, Name = "Ali Motors", IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "t" });
        await db.SaveChangesAsync();

        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.CurrentUser).Returns(new AppUser { Id = 1, Username = "tech" });
        var notifyCount = 0;
        var notifications = new Mock<INotificationService>();
        notifications.Setup(n => n.CreateNotificationAsync(
                It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback(() => notifyCount++)
            .Returns(Task.CompletedTask);

        var clockTime = new FakeSlaClockTime { UtcNow = new DateTime(2026, 8, 7, 8, 0, 0, DateTimeKind.Utc) };
        var policySvc = SlaPolicyTests.CreatePolicySvc(db, company);
        await policySvc.EnsureDefaultPolicyAsync(1);

        // Tiny targets for test
        var policy = await db.SlaPolicies.Include(p => p.Targets).FirstAsync();
        foreach (var t in policy.Targets.Where(x => x.Priority == ServiceTicketPriority.Urgent))
        {
            t.TargetMinutes = 10;
            t.WarnAtPercent = 50;
        }
        await db.SaveChangesAsync();

        var clock = new SlaClockService(
            new Repository<SlaPolicy>(db),
            new Repository<SlaPolicyRule>(db),
            new Repository<SlaTimer>(db),
            new Repository<SlaEvent>(db),
            new Repository<ServiceTicket>(db),
            new Repository<Customer>(db),
            new Repository<BusinessCalendar>(db),
            policySvc,
            new UnitOfWork(db),
            company,
            clockTime,
            NullLogger<SlaClockService>.Instance);

        var ticketSvc = new ServiceTicketService(
            new Repository<ServiceTicket>(db),
            new Repository<Customer>(db),
            new Repository<Product>(db),
            new Repository<AmcContract>(db),
            new Repository<SalesInvoice>(db),
            new Repository<SlaTimer>(db),
            new UnitOfWork(db),
            company,
            user.Object,
            notifications.Object,
            clock);

        var created = await ticketSvc.CreateTicketAsync(new ServiceTicketCreateDto(
            1, "Breach me", null, ServiceTicketPriority.Urgent, false, null, null, null, null, null, null));

        var monitor = new SlaMonitorService(
            new Repository<SlaTimer>(db),
            new Repository<SlaPolicy>(db),
            new Repository<BusinessCalendar>(db),
            new Repository<ServiceTicket>(db),
            new UnitOfWork(db),
            notifications.Object,
            OpsSlaTestDoubles.NoOp,
            clockTime,
            NullLogger<SlaMonitorService>.Instance);

        clockTime.UtcNow = clockTime.UtcNow.AddMinutes(6); // past 50% of 10m
        var warnEvents = await monitor.SweepAsync();
        warnEvents.Should().BeGreaterThan(0);

        var afterWarn = await clock.GetTicketSlaAsync(created.Data!.Id);
        afterWarn!.Timers.Should().Contain(t => t.WarnedAt != null);

        var notifiesAfterWarn = notifyCount;
        await monitor.SweepAsync();
        notifyCount.Should().Be(notifiesAfterWarn); // idempotent warn

        clockTime.UtcNow = clockTime.UtcNow.AddMinutes(10);
        var breachEvents = await monitor.SweepAsync();
        breachEvents.Should().BeGreaterThan(0);

        var afterBreach = await clock.GetTicketSlaAsync(created.Data.Id);
        afterBreach!.Timers.Should().Contain(t => t.Status == SlaTimerStatus.Breached);

        var notifiesAfterBreach = notifyCount;
        await monitor.SweepAsync();
        notifyCount.Should().Be(notifiesAfterBreach);
    }
}

public class SlaEscalateValidationTests
{
    [Fact]
    public async Task Upsert_Rejects_Invalid_EscalateToUserId()
    {
        var (svc, _) = await SlaPolicyTests.SeedAsync();
        var targets = Enum.GetValues<ServiceTicketPriority>()
            .SelectMany(p => new[]
            {
                new SlaTargetUpsertDto(SlaMetric.FirstResponse, p, 60, 80),
                new SlaTargetUpsertDto(SlaMetric.Resolution, p, 240, 80)
            }).ToList();

        var bad = await svc.UpsertAsync(new SlaPolicyUpsertDto(
            null, "Bad escalate", false, true, SlaCalendarMode.AlwaysOn, false, 99999, targets));
        bad.Succeeded.Should().BeFalse();
        bad.Error.Should().Contain("Escalate-to user");
    }

    [Fact]
    public async Task Upsert_Accepts_Active_EscalateToUserId()
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        await using var db = SlaPolicyTests.CreateDb(company);
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        db.Users.Add(new AppUser
        {
            Id = 5,
            Username = "mgr",
            DisplayName = "Manager",
            PasswordHash = "x",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = SlaPolicyTests.CreatePolicySvc(db, company);
        var targets = Enum.GetValues<ServiceTicketPriority>()
            .SelectMany(p => new[]
            {
                new SlaTargetUpsertDto(SlaMetric.FirstResponse, p, 60, 80),
                new SlaTargetUpsertDto(SlaMetric.Resolution, p, 240, 80)
            }).ToList();

        var ok = await svc.UpsertAsync(new SlaPolicyUpsertDto(
            null, "With escalate", true, true, SlaCalendarMode.AlwaysOn, false, 5, targets));
        ok.Succeeded.Should().BeTrue();
        ok.Data!.EscalateToUserId.Should().Be(5);
    }
}

public class OpsSlaClockTests
{
    [Fact]
    public async Task Open_SalesOrder_Past_Target_Warns_Then_Breaches()
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        await using var db = SlaPolicyTests.CreateDb(company);
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        await db.SaveChangesAsync();

        var clockTime = new FakeSlaClockTime { UtcNow = new DateTime(2026, 8, 7, 10, 0, 0, DateTimeKind.Utc) };
        var policySvc = SlaPolicyTests.CreatePolicySvc(db, company);
        await policySvc.EnsureDefaultOpsPoliciesAsync(1);

        var soPolicy = await db.SlaPolicies.Include(p => p.Targets)
            .FirstAsync(p => p.AppliesToEntityType == SlaEntityType.SalesOrder);
        foreach (var t in soPolicy.Targets)
        {
            t.TargetMinutes = 10;
            t.WarnAtPercent = 50;
        }
        await db.SaveChangesAsync();

        var ops = new OpsSlaClockService(
            new Repository<SlaPolicy>(db),
            new Repository<SlaTimer>(db),
            new Repository<SlaEvent>(db),
            new Repository<InventoryItem>(db),
            new Repository<Product>(db),
            policySvc,
            new UnitOfWork(db),
            company,
            clockTime,
            NullLogger<OpsSlaClockService>.Instance);

        await ops.OnSalesOrderOpenedAsync(42, 1);
        var timer = await db.SlaTimers.SingleAsync(t => t.EntityType == SlaEntityType.SalesOrder && t.EntityId == 42);
        timer.Status.Should().Be(SlaTimerStatus.Running);
        timer.TargetSeconds.Should().Be(600);
        timer.WarnSeconds.Should().Be(300);

        var notifications = new Mock<INotificationService>();
        notifications.Setup(n => n.CreateNotificationAsync(
                It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var monitor = new SlaMonitorService(
            new Repository<SlaTimer>(db),
            new Repository<SlaPolicy>(db),
            new Repository<BusinessCalendar>(db),
            new Repository<ServiceTicket>(db),
            new UnitOfWork(db),
            notifications.Object,
            ops,
            clockTime,
            NullLogger<SlaMonitorService>.Instance);

        clockTime.UtcNow = clockTime.UtcNow.AddMinutes(6);
        await monitor.SweepAsync();
        timer = await db.SlaTimers.SingleAsync(t => t.Id == timer.Id);
        timer.WarnedAt.Should().NotBeNull();
        timer.Status.Should().Be(SlaTimerStatus.Running);

        clockTime.UtcNow = clockTime.UtcNow.AddMinutes(6);
        await monitor.SweepAsync();
        timer = await db.SlaTimers.SingleAsync(t => t.Id == timer.Id);
        timer.Status.Should().Be(SlaTimerStatus.Breached);
        timer.BreachedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Pay_Invoice_Completes_Ops_Timer()
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        await using var db = SlaPolicyTests.CreateDb(company);
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        await db.SaveChangesAsync();

        var clockTime = new FakeSlaClockTime { UtcNow = DateTime.UtcNow };
        var policySvc = SlaPolicyTests.CreatePolicySvc(db, company);
        var ops = new OpsSlaClockService(
            new Repository<SlaPolicy>(db),
            new Repository<SlaTimer>(db),
            new Repository<SlaEvent>(db),
            new Repository<InventoryItem>(db),
            new Repository<Product>(db),
            policySvc,
            new UnitOfWork(db),
            company,
            clockTime,
            NullLogger<OpsSlaClockService>.Instance);

        await ops.OnInvoiceUnpaidAsync(7, 1);
        (await db.SlaTimers.CountAsync(t => t.EntityType == SlaEntityType.SalesInvoice && t.EntityId == 7
                                            && t.Status == SlaTimerStatus.Running)).Should().Be(1);

        await ops.OnInvoicePaidOrVoidedAsync(7);
        var timer = await db.SlaTimers.SingleAsync(t => t.EntityType == SlaEntityType.SalesInvoice && t.EntityId == 7);
        timer.Status.Should().Be(SlaTimerStatus.Met);
        timer.CompletedAt.Should().NotBeNull();
    }
}

public class CrmActivityMonitorTests
{
    [Fact]
    public async Task Sweep_Warns_Once_For_Overdue_Activity()
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        await using var db = SlaPolicyTests.CreateDb(company);
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        db.CrmActivities.Add(new CrmActivity
        {
            CompanyId = 1,
            Subject = "Call back",
            Type = CrmActivityType.Task,
            DueAt = DateTime.UtcNow.AddHours(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();

        var notifyCount = 0;
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(n => n.CreateNotificationAsync(
                It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback(() => notifyCount++)
            .Returns(Task.CompletedTask);

        var monitor = new CrmActivityMonitorService(
            new Repository<CrmActivity>(db),
            new UnitOfWork(db),
            notifications.Object,
            NullLogger<CrmActivityMonitorService>.Instance);

        var first = await monitor.SweepAsync();
        first.Should().Be(1);
        notifyCount.Should().Be(1);
        (await db.CrmActivities.SingleAsync()).SlaWarnedAt.Should().NotBeNull();

        var second = await monitor.SweepAsync();
        second.Should().Be(0);
        notifyCount.Should().Be(1);
    }
}
