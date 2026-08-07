using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Service;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using CarAutoParts.Infrastructure.Data;
using CarAutoParts.Infrastructure.Repositories;
using FluentAssertions;
using Moq;
using Xunit;

namespace CarAutoParts.Application.Tests;

/// <summary>Phase 8 service depth — AMC, visits, parts, warranty reject notes.</summary>
public class Phase8ServiceDepthTests
{
    private static async Task<(ApplicationDbContext Db, CurrentCompanyContext Company, CurrentUserService User)> SeedAsync()
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        var db = TestDbContextFactory.Create();
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        db.Users.Add(new AppUser
        {
            Id = 1, Username = "tech1", PasswordHash = "x", IsActive = true,
            CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        });
        db.Customers.Add(new Customer
        {
            Id = 1, Name = "Shop A", IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        });
        db.Customers.Add(new Customer
        {
            Id = 2, Name = "Shop B", IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        });
        db.Categories.Add(new Category { Id = 1, Name = "Cat", CreatedAt = DateTime.UtcNow, CreatedBy = "t" });
        db.Brands.Add(new Brand { Id = 1, Name = "Brand", CreatedAt = DateTime.UtcNow, CreatedBy = "t" });
        db.Products.Add(new Product
        {
            Id = 1, Name = "Pad", Sku = "P1", CategoryId = 1, BrandId = 1, Unit = "PCS",
            PurchasePrice = 10, CostPrice = 10, SalePrice = 20, IsActive = true,
            CompanyId = 1, CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        });
        db.Warehouses.Add(new Warehouse
        {
            Id = 1, Name = "Main", CompanyId = 1,
            CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        });
        db.InventoryItems.Add(new InventoryItem
        {
            ProductId = 1, WarehouseId = 1, QuantityOnHand = 50, AverageCost = 10,
            CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        });
        await db.SaveChangesAsync();
        var user = new CurrentUserService();
        user.SetUser(db.Users.First(), ["service.view", "service.manage"]);
        return (db, company, user);
    }

    [Fact]
    public async Task Amc_Link_Rejects_Wrong_Customer()
    {
        var (db, company, user) = await SeedAsync();
        var amc = new AmcContractService(
            new Repository<AmcContract>(db), new Repository<Customer>(db), new Repository<Product>(db),
            new UnitOfWork(db), company, user);
        var created = await amc.UpsertAsync(new AmcContractUpsertDto(
            null, "AMC-1", 1, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddYears(1),
            AmcContractStatus.Active, null, 1000m, null));
        created.Succeeded.Should().BeTrue();

        var tickets = await CreateTicketServiceAsync(db, company, user);
        var fail = await tickets.CreateTicketAsync(new ServiceTicketCreateDto(
            2, "Wrong customer AMC", null, ServiceTicketPriority.Normal, false, null, null, null, null, null, null,
            created.Data!.Id));
        fail.Succeeded.Should().BeFalse();
        fail.Error.Should().Contain("different customer");

        var ok = await tickets.CreateTicketAsync(new ServiceTicketCreateDto(
            1, "Linked AMC", null, ServiceTicketPriority.Normal, false, null, null, null, null, null, null,
            created.Data.Id));
        ok.Succeeded.Should().BeTrue();
        ok.Data!.AmcContractId.Should().Be(created.Data.Id);
        ok.Data.AmcReference.Should().Be("AMC-1");
    }

    [Fact]
    public async Task Visit_Schedule_And_Complete()
    {
        var (db, company, user) = await SeedAsync();
        var tickets = await CreateTicketServiceAsync(db, company, user);
        var ticket = await tickets.CreateTicketAsync(new ServiceTicketCreateDto(
            1, "Visit me", null, ServiceTicketPriority.Normal, false, null, null, null, null, null, null));
        ticket.Succeeded.Should().BeTrue();

        var field = CreateFieldService(db, company, user);
        var visit = await field.ScheduleVisitAsync(new ServiceVisitCreateDto(
            ticket.Data!.Id, 1, DateTime.UtcNow.AddHours(2), "Bring tools"));
        visit.Succeeded.Should().BeTrue();
        visit.Data!.Status.Should().Be(ServiceVisitStatus.Scheduled);

        var done = await field.ChangeVisitStatusAsync(visit.Data.Id, new ServiceVisitStatusDto(ServiceVisitStatus.Completed, null));
        done.Succeeded.Should().BeTrue();
        done.Data!.Status.Should().Be(ServiceVisitStatus.Completed);
        done.Data.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Consume_Part_Reduces_OnHand()
    {
        var (db, company, user) = await SeedAsync();
        var tickets = await CreateTicketServiceAsync(db, company, user);
        var ticket = await tickets.CreateTicketAsync(new ServiceTicketCreateDto(
            1, "Need parts", null, ServiceTicketPriority.Normal, false, null, null, null, null, null, null));
        var field = CreateFieldService(db, company, user);
        var part = await field.ConsumePartAsync(new ServiceTicketPartCreateDto(ticket.Data!.Id, 1, 1, 3));
        part.Succeeded.Should().BeTrue();
        var inv = db.InventoryItems.First(i => i.ProductId == 1 && i.WarehouseId == 1);
        inv.QuantityOnHand.Should().Be(47);
    }

    [Fact]
    public async Task Warranty_Reject_Requires_Notes()
    {
        var (db, company, user) = await SeedAsync();
        var tickets = await CreateTicketServiceAsync(db, company, user);
        var ticket = await tickets.CreateTicketAsync(new ServiceTicketCreateDto(
            1, "Warranty", null, ServiceTicketPriority.High, true, "WR-1", null, null, null, null, null));
        ticket.Succeeded.Should().BeTrue();

        var reject = await tickets.DecideWarrantyAsync(ticket.Data!.Id, new WarrantyClaimDecisionDto(WarrantyClaimStatus.Rejected, null));
        reject.Succeeded.Should().BeFalse();
        reject.Error.Should().Contain("Notes are required");

        var approve = await tickets.DecideWarrantyAsync(ticket.Data.Id, new WarrantyClaimDecisionDto(
            WarrantyClaimStatus.Approved, "OK", 1, 1));
        approve.Succeeded.Should().BeTrue();
        approve.Data!.ReplacementProductId.Should().Be(1);
        approve.Data.ReplacementQuantity.Should().Be(1);
    }

    private static async Task<ServiceTicketService> CreateTicketServiceAsync(
        ApplicationDbContext db, CurrentCompanyContext company, CurrentUserService user)
    {
        var notifications = new Mock<INotificationService>();
        notifications.Setup(n => n.CreateNotificationAsync(
                It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var clockTime = new Mock<ISlaClockTime>();
        clockTime.Setup(c => c.UtcNow).Returns(DateTime.UtcNow);
        var policySvc = new SlaPolicyService(
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
            user);
        await policySvc.EnsureDefaultPolicyAsync(1);
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
            clockTime.Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SlaClockService>.Instance);

        return new ServiceTicketService(
            new Repository<ServiceTicket>(db), new Repository<Customer>(db), new Repository<Product>(db),
            new Repository<AmcContract>(db), new Repository<SalesInvoice>(db), new Repository<SlaTimer>(db),
            new UnitOfWork(db), company, user, notifications.Object, clock);
    }

    private static ServiceFieldService CreateFieldService(
        ApplicationDbContext db, CurrentCompanyContext company, CurrentUserService user)
    {
        var notifications = new Mock<INotificationService>();
        notifications.Setup(n => n.CreateNotificationAsync(
                It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var inventory = new Mock<IInventoryService>();
        inventory.Setup(i => i.DeductStockAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int productId, int warehouseId, decimal qty, string _, int __, CancellationToken ___) =>
            {
                var item = db.InventoryItems.First(x => x.ProductId == productId && x.WarehouseId == warehouseId);
                if (item.QuantityOnHand < qty)
                    return Application.Common.Result<decimal>.Failure("Insufficient stock");
                item.QuantityOnHand -= qty;
                db.SaveChanges();
                return Application.Common.Result<decimal>.Success(item.AverageCost);
            });

        return new ServiceFieldService(
            new Repository<ServiceVisit>(db), new Repository<ServiceTicketPart>(db),
            new Repository<ServiceTicket>(db), new Repository<AppUser>(db),
            new Repository<Product>(db), new Repository<Warehouse>(db),
            inventory.Object, new UnitOfWork(db), company, user, notifications.Object);
    }
}
