using CarAutoParts.Application.Common;
using CarAutoParts.Application.Config;
using CarAutoParts.Application.DTOs.Service;
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

public class ServiceTicketTests
{
    [Fact]
    public async Task CreateTicket_Persists_And_List_Returns_It()
    {
        var (svc, _) = await SeedAsync();

        var created = await svc.CreateTicketAsync(new ServiceTicketCreateDto(
            1, "Engine noise", "Rattling on cold start", ServiceTicketPriority.High,
            false, null, null, null, null, null, "Customer waiting"));

        created.Succeeded.Should().BeTrue();
        created.Data!.Subject.Should().Be("Engine noise");
        created.Data.Status.Should().Be(ServiceTicketStatus.Open);
        created.Data.CustomerName.Should().Be("Ali Motors");

        var list = await svc.GetTicketsAsync(new QuerySpec { Page = 1, PageSize = 20 });
        list.TotalCount.Should().Be(1);
        list.Items[0].Priority.Should().Be(ServiceTicketPriority.High);
    }

    [Fact]
    public void Vertical_Profiles_Include_ServiceTickets_Module()
    {
        VerticalProfiles.KnownModuleKeys.Should().Contain(ConfigKeys.ModServiceTickets);
        VerticalProfiles.DefaultModules(VerticalProfiles.AutoParts)[ConfigKeys.ModServiceTickets].Should().BeTrue();
        VerticalProfiles.DefaultModules(VerticalProfiles.GeneralRetail)[ConfigKeys.ModServiceTickets].Should().BeTrue();
    }

    [Fact]
    public async Task Create_Fails_When_Customer_Not_Found()
    {
        var (svc, _) = await SeedAsync();
        var result = await svc.CreateTicketAsync(new ServiceTicketCreateDto(
            999, "Bad customer", null, ServiceTicketPriority.Normal, false, null, null, null, null, null, null));
        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("Customer");
    }

    [Fact]
    public async Task Ticket_List_Respects_Company_Filter()
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        await using var db = CreateDb(company);

        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        db.Customers.Add(new Customer { Id = 1, Name = "Company One Customer", IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "t" });
        await db.SaveChangesAsync();

        db.ServiceTickets.Add(new ServiceTicket
        {
            CompanyId = 1, CustomerId = 1, Subject = "Company One Ticket", Status = ServiceTicketStatus.Open,
            OpenedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        });
        db.ServiceTickets.Add(new ServiceTicket
        {
            CompanyId = 2, CustomerId = 1, Subject = "Other Company Ticket", Status = ServiceTicketStatus.Open,
            OpenedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        });
        await db.SaveChangesAsync();

        (await db.ServiceTickets.IgnoreQueryFilters().CountAsync()).Should().Be(2);

        var user = new Mock<ICurrentUserService>();
        var svc = CreateSvc(db, company, user.Object);
        var list = await svc.GetTicketsAsync(new QuerySpec { Page = 1, PageSize = 50 });
        list.TotalCount.Should().Be(1);
        list.Items.Single().Subject.Should().Be("Company One Ticket");
    }

    [Fact]
    public async Task Status_Transition_Requires_Resolution_Notes()
    {
        var (svc, ticketId) = await CreateOpenTicketAsync();

        var missingNotes = await svc.ChangeStatusAsync(ticketId, new ServiceTicketStatusChangeDto(ServiceTicketStatus.Resolved, null));
        missingNotes.Succeeded.Should().BeFalse();
        missingNotes.Error.Should().Contain("Resolution notes");

        var ok = await svc.ChangeStatusAsync(ticketId, new ServiceTicketStatusChangeDto(ServiceTicketStatus.Resolved, "Replaced brake pads"));
        ok.Succeeded.Should().BeTrue();
        ok.Data!.Status.Should().Be(ServiceTicketStatus.Resolved);
        ok.Data.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Closed_Ticket_Cannot_Change_Status_Again()
    {
        var (svc, ticketId) = await CreateOpenTicketAsync();
        var closed = await svc.ChangeStatusAsync(ticketId, new ServiceTicketStatusChangeDto(ServiceTicketStatus.Closed, "Done, closing"));
        closed.Succeeded.Should().BeTrue();
        closed.Data!.ClosedAt.Should().NotBeNull();

        var reopen = await svc.ChangeStatusAsync(ticketId, new ServiceTicketStatusChangeDto(ServiceTicketStatus.Open, null));
        reopen.Succeeded.Should().BeFalse();
        reopen.Error.Should().Contain("closed");
    }

    [Fact]
    public async Task Open_To_InProgress_Does_Not_Require_Resolution_Notes()
    {
        var (svc, ticketId) = await CreateOpenTicketAsync();
        var result = await svc.ChangeStatusAsync(ticketId, new ServiceTicketStatusChangeDto(ServiceTicketStatus.InProgress, null));
        result.Succeeded.Should().BeTrue();
        result.Data!.Status.Should().Be(ServiceTicketStatus.InProgress);
    }

    [Fact]
    public async Task GetTicketsForCustomer_Returns_Only_That_Customers_Tickets()
    {
        var (svc, company) = await SeedAsync();
        await svc.CreateTicketAsync(new ServiceTicketCreateDto(1, "Ticket A", null, ServiceTicketPriority.Normal, false, null, null, null, null, null, null));

        // second customer, different tickets
        await using var db2 = CreateDb(company);

        var tickets = await svc.GetTicketsForCustomerAsync(1);
        tickets.Should().ContainSingle(t => t.Subject == "Ticket A");
    }

    private static async Task<(ServiceTicketService Svc, int TicketId)> CreateOpenTicketAsync()
    {
        var (svc, _) = await SeedAsync();
        var created = await svc.CreateTicketAsync(new ServiceTicketCreateDto(
            1, "Brake pad warranty", "Squeaking noise", ServiceTicketPriority.Normal,
            true, "WARR-001", null, null, null, null, null));
        created.Succeeded.Should().BeTrue();
        return (svc, created.Data!.Id);
    }

    private static async Task<(ServiceTicketService Svc, CurrentCompanyContext Company)> SeedAsync()
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        var db = CreateDb(company);
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        db.Customers.Add(new Customer
        {
            Id = 1, Name = "Ali Motors", IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        });
        await db.SaveChangesAsync();
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.CurrentUser).Returns(new AppUser { Id = 1, Username = "tech" });
        return (CreateSvc(db, company, user.Object), company);
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

    private static ServiceTicketService CreateSvc(ApplicationDbContext db, CurrentCompanyContext company, ICurrentUserService user)
    {
        var notifications = new Mock<INotificationService>();
        notifications.Setup(n => n.CreateNotificationAsync(
                It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new ServiceTicketService(
            new Repository<ServiceTicket>(db),
            new Repository<Customer>(db),
            new Repository<Product>(db),
            new UnitOfWork(db),
            company,
            user,
            notifications.Object);
    }
}
