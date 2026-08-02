using CarAutoParts.Application.Common;
using CarAutoParts.Application.Config;
using CarAutoParts.Application.DTOs.Crm;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using CarAutoParts.Infrastructure.Data;
using CarAutoParts.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CarAutoParts.Application.Tests;

public class CrmFoundationW0Tests
{
    [Fact]
    public async Task CreateLead_Persists_And_List_Returns_It()
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        await using var db = CreateDb(company);

        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        await db.SaveChangesAsync();

        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.CurrentUser).Returns(new AppUser { Id = 1, Username = "sales" });

        var svc = CreateCrm(db, company, user.Object);
        var created = await svc.CreateLeadAsync(new LeadCreateDto("Ali Motors", "03001234567", null, "Walk-in", "Wants brake pads", null, true));
        created.Succeeded.Should().BeTrue();
        created.Data!.Name.Should().Be("Ali Motors");
        created.Data.Status.Should().Be(LeadStatus.New);

        var list = await svc.GetLeadsAsync(new QuerySpec { Page = 1, PageSize = 20 });
        list.TotalCount.Should().Be(1);
        list.Items[0].Phone.Should().Be("03001234567");
    }

    [Fact]
    public async Task Lead_List_Respects_Company_Filter()
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        await using var db = CreateDb(company);

        db.Leads.Add(new Lead
        {
            CompanyId = 1, Name = "Company One Lead", Status = LeadStatus.New, Source = "Walk-in",
            CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        });
        db.Leads.Add(new Lead
        {
            CompanyId = 2, Name = "Other Company Lead", Status = LeadStatus.New, Source = "Walk-in",
            CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        });
        await db.SaveChangesAsync();

        (await db.Leads.IgnoreQueryFilters().CountAsync()).Should().Be(2);

        var user = new Mock<ICurrentUserService>();
        var svc = CreateCrm(db, company, user.Object);
        var list = await svc.GetLeadsAsync(new QuerySpec { Page = 1, PageSize = 50 });
        list.TotalCount.Should().Be(1);
        list.Items.Single().Name.Should().Be("Company One Lead");
    }

    [Fact]
    public void Vertical_Profiles_Include_SalesCrm_Module()
    {
        VerticalProfiles.KnownModuleKeys.Should().Contain(ConfigKeys.ModSalesCrm);
        VerticalProfiles.DefaultModules(VerticalProfiles.AutoParts)[ConfigKeys.ModSalesCrm].Should().BeTrue();
        VerticalProfiles.DefaultModules(VerticalProfiles.GeneralRetail)[ConfigKeys.ModSalesCrm].Should().BeTrue();
    }

    [Fact]
    public async Task Convert_Customer_Once_Is_Idempotent()
    {
        var (svc, _) = await SeedLeadAsync();
        var first = await svc.ConvertLeadToCustomerAsync(1);
        first.Succeeded.Should().BeTrue();
        first.Data!.Status.Should().Be(LeadStatus.Converted);
        first.Data.ConvertedCustomerId.Should().NotBeNull();

        var second = await svc.ConvertLeadToCustomerAsync(1);
        second.Succeeded.Should().BeTrue();
        second.Data!.ConvertedCustomerId.Should().Be(first.Data.ConvertedCustomerId);
    }

    [Fact]
    public async Task Lost_Without_Reason_Fails()
    {
        var (svc, _) = await SeedLeadAsync();
        var result = await svc.UpdateLeadAsync(1, new LeadUpdateDto(
            "Ali Motors", "03001234567", null, "Walk-in", null, null, LeadStatus.Lost, null, true));
        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("Lost reason");
    }

    [Fact]
    public async Task Stage_Change_Writes_History_And_Validates_Won()
    {
        var (svc, _) = await SeedLeadAsync();
        await svc.ConvertLeadToCustomerAsync(1);
        var opp = await svc.ConvertLeadToOpportunityAsync(1, new ConvertLeadToOpportunityDto("Deal", 10000m, null));
        opp.Succeeded.Should().BeTrue();

        var bad = await svc.ChangeOpportunityStageAsync(opp.Data!.Id, new OpportunityStageChangeDto(OpportunityStage.Won, null, null, null));
        bad.Succeeded.Should().BeFalse();

        var ok = await svc.ChangeOpportunityStageAsync(opp.Data.Id, new OpportunityStageChangeDto(OpportunityStage.Won, null, "Best price", null));
        ok.Succeeded.Should().BeTrue();
        ok.Data!.Probability.Should().Be(100);

        var history = await svc.GetStageHistoryAsync(opp.Data.Id);
        history.Should().NotBeEmpty();
        history.Any(h => h.ToStage == OpportunityStage.Won).Should().BeTrue();
    }

    [Fact]
    public async Task Pipeline_Dashboard_Weighted_Revenue()
    {
        var (svc, company) = await SeedLeadAsync();
        await svc.CreateOpportunityAsync(new OpportunityCreateDto("A", null, null, 1000m, 50, null));
        await svc.CreateOpportunityAsync(new OpportunityCreateDto("B", null, null, 2000m, 25, null));
        var dash = await svc.GetPipelineDashboardAsync();
        dash.OpenCount.Should().Be(2);
        dash.OpenValue.Should().Be(3000m);
        dash.WeightedValue.Should().Be(1000m);
    }

    [Fact]
    public async Task Activity_Assign_Complete_And_MyDay()
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        await using var db = CreateDb(company);
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        await db.SaveChangesAsync();

        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.CurrentUser).Returns(new AppUser { Id = 9, Username = "sales" });
        var svc = CreateCrm(db, company, user.Object);

        var created = await svc.CreateActivityAsync(new CrmActivityCreateDto(
            CrmActivityType.Task, "Call back", DateTime.UtcNow.Date.AddHours(12), null, null, 9, null));
        created.Succeeded.Should().BeTrue();

        var myDay = await svc.GetActivitiesAsync(new QuerySpec
        {
            Page = 1, PageSize = 20,
            Filters = new Dictionary<string, object?> { ["myDay"] = true }
        });
        myDay.TotalCount.Should().Be(1);

        var done = await svc.CompleteActivityAsync(created.Data!.Id);
        done.Succeeded.Should().BeTrue();
        done.Data!.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Customer360_Includes_Converted_Lead()
    {
        var (svc, _) = await SeedLeadAsync();
        var conv = await svc.ConvertLeadToCustomerAsync(1);
        var cid = conv.Data!.ConvertedCustomerId!.Value;
        var dto = await svc.GetCustomer360Async(cid);
        dto.Should().NotBeNull();
        dto!.ConvertedLeads.Should().ContainSingle(l => l.Id == 1);
    }

    [Fact]
    public async Task Assignment_Rule_Applies_On_Create()
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        await using var db = CreateDb(company);
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        await db.SaveChangesAsync();
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.CurrentUser).Returns(new AppUser { Id = 1, Username = "sales" });
        var svc = CreateCrm(db, company, user.Object);

        await svc.UpsertAssignmentRuleAsync(new CrmAssignmentRuleDto(0, "Walk-in", 42, false, true));
        var lead = await svc.CreateLeadAsync(new LeadCreateDto("X", null, null, "Walk-in", null, null, true));
        lead.Succeeded.Should().BeTrue();
        lead.Data!.OwnerUserId.Should().Be(42);
    }

    [Fact]
    public async Task Convert_Creates_FollowUp_Task()
    {
        var (svc, company) = await SeedLeadAsync();
        await svc.ConvertLeadToCustomerAsync(1);
        var acts = await svc.GetActivitiesAsync(new QuerySpec
        {
            Page = 1, PageSize = 50,
            Filters = new Dictionary<string, object?> { ["leadId"] = 1 }
        });
        acts.Items.Any(a => a.Subject.Contains("conversion", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    private static async Task<(CrmService Svc, CurrentCompanyContext Company)> SeedLeadAsync()
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        var db = CreateDb(company);
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        db.Leads.Add(new Lead
        {
            Id = 1, CompanyId = 1, Name = "Ali Motors", Phone = "03001234567", Source = "Walk-in",
            Status = LeadStatus.New, CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        });
        await db.SaveChangesAsync();
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.CurrentUser).Returns(new AppUser { Id = 1, Username = "sales" });
        return (CreateCrm(db, company, user.Object), company);
    }

    private static ApplicationDbContext CreateDb(CurrentCompanyContext company)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options, company);
        db.Database.EnsureCreated();
        return db;
    }

    private static CrmService CreateCrm(ApplicationDbContext db, CurrentCompanyContext company, ICurrentUserService user)
    {
        var notifications = new Mock<INotificationService>();
        notifications.Setup(n => n.CreateNotificationAsync(
                It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new CrmService(
            new Repository<Lead>(db),
            new Repository<CrmActivity>(db),
            new Repository<Opportunity>(db),
            new Repository<OpportunityStageHistory>(db),
            new Repository<Customer>(db),
            new Repository<SalesInvoice>(db),
            new Repository<SalesOrder>(db),
            new Repository<SalesReturn>(db),
            new Repository<CrmAssignmentRule>(db),
            new Repository<CrmEmailTemplate>(db),
            new UnitOfWork(db),
            company,
            user,
            notifications.Object);
    }
}
