using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Products;
using CarAutoParts.Application.DTOs.Transfers;
using CarAutoParts.Application.Enterprise;
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

public class Phase15WarehouseLocationsTests
{
    private sealed class Harness
    {
        public required ApplicationDbContext Db { get; init; }
        public required CurrentCompanyContext Company { get; init; }
        public required EnterpriseInventoryService Inventory { get; init; }
        public required WarehouseLocationService Locations { get; init; }
        public required TransferService Transfers { get; init; }
    }

    private static async Task<Harness> CreateAsync()
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        var db = TestDbContextFactory.Create();

        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        db.Branches.Add(new Branch { Id = 1, CompanyId = 1, Code = "HO", Name = "HO", IsDefault = true, IsActive = true });
        db.Warehouses.AddRange(
            new Warehouse { Id = 1, Name = "Main", CompanyId = 1, BranchId = 1, CreatedAt = DateTime.UtcNow, CreatedBy = "t" },
            new Warehouse { Id = 2, Name = "Branch2", CompanyId = 1, BranchId = 1, CreatedAt = DateTime.UtcNow, CreatedBy = "t" });
        db.WarehouseLocations.Add(new WarehouseLocation
        {
            Id = 1, WarehouseId = 1, CompanyId = 1, Code = "MAIN", Name = "Main",
            IsReceivingDefault = true, IsPickDefault = true, IsActive = true
        });
        db.WarehouseLocations.Add(new WarehouseLocation
        {
            Id = 2, WarehouseId = 1, CompanyId = 1, Code = "A-01", Name = "Aisle A",
            IsReceivingDefault = false, IsPickDefault = false, IsActive = true
        });
        db.WarehouseLocations.Add(new WarehouseLocation
        {
            Id = 3, WarehouseId = 2, CompanyId = 1, Code = "MAIN", Name = "Main",
            IsReceivingDefault = true, IsPickDefault = true, IsActive = true
        });
        db.Categories.Add(new Category { Id = 1, Name = "Cat", CreatedAt = DateTime.UtcNow, CreatedBy = "t" });
        db.Brands.Add(new Brand { Id = 1, Name = "Brand", CreatedAt = DateTime.UtcNow, CreatedBy = "t" });
        db.Products.Add(new Product
        {
            Id = 1, Name = "Pad", Sku = "BP-1", CategoryId = 1, BrandId = 1, Unit = "PCS",
            PurchasePrice = 100, CostPrice = 100, SalePrice = 250, IsActive = true, CompanyId = 1,
            CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        });
        db.NumberSequences.Add(new NumberSequence
        {
            CompanyId = 1, DocumentType = "GRN", Prefix = "GRN-", NextValue = 1, Padding = 4
        });
        db.NumberSequences.Add(new NumberSequence
        {
            CompanyId = 1, DocumentType = "CC", Prefix = "CC-", NextValue = 1, Padding = 4
        });
        await db.SaveChangesAsync();

        var gl = new Mock<IGlPostingService>();
        gl.Setup(g => g.PostDocumentAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<int?>(), It.IsAny<IReadOnlyList<GlPostingLineRequest>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GlJournalDraftDto>.Success(
                new GlJournalDraftDto(1, "JV-0001", JournalStatus.Posted, 100, 100)));

        var inventory = new EnterpriseInventoryService(new EnterpriseDbAdapter(db), company, gl.Object);
        var locations = new WarehouseLocationService(
            new Repository<Warehouse>(db),
            new Repository<WarehouseLocation>(db),
            new Repository<InventoryLocationBalance>(db),
            new UnitOfWork(db),
            company);

        var user = new CurrentUserService();
        var companyCtx = company;
        companyCtx.Set(1, 1, [1]);

        var approvals = new Mock<IApprovalWorkflowService>();
        approvals.Setup(a => a.EnsureApprovedOrQueueAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var moneyAudit = new Mock<IMoneyAuditService>();
        moneyAudit.Setup(m => m.RecordAsync(
                It.IsAny<AuditAction>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mapper = new AutoMapper.MapperConfiguration(cfg =>
            cfg.AddProfile<CarAutoParts.Application.Mapping.MappingProfile>()).CreateMapper();

        var invSvc = TestInventoryFactory.Create(db, mapper, company);

        var transfers = new TransferService(
            new Repository<InventoryTransfer>(db),
            new Repository<Warehouse>(db),
            new Repository<Product>(db),
            invSvc,
            user,
            company,
            gl.Object,
            new UnitOfWork(db),
            mapper,
            approvals.Object,
            moneyAudit.Object);

        return new Harness
        {
            Db = db,
            Company = company,
            Inventory = inventory,
            Locations = locations,
            Transfers = transfers
        };
    }

    [Fact]
    public async Task LocationCrud_CreatesAndListsBins()
    {
        var h = await CreateAsync();
        var created = await h.Locations.CreateAsync(1, new UpsertWarehouseLocationDto("B-02", "Bay 2", IsPickDefault: true));
        created.Succeeded.Should().BeTrue();
        created.Data!.Code.Should().Be("B-02");

        var list = await h.Locations.GetByWarehouseAsync(1);
        list.Should().Contain(l => l.Code == "B-02");
        list.Single(l => l.Code == "B-02").IsPickDefault.Should().BeTrue();
        list.Count(l => l.IsPickDefault).Should().Be(1);
    }

    [Fact]
    public async Task GrnPost_AssignsPutawayBin_AndUpdatesLocationBalance()
    {
        var h = await CreateAsync();
        var grn = await h.Inventory.CreateGrnAsync(new CreateGrnRequest(
            1, null, DateTime.UtcNow, 0, null,
            [new CreateGrnLineRequest(1, 10, 50, WarehouseLocationId: 2)]));
        grn.Succeeded.Should().BeTrue();

        var posted = await h.Inventory.PostGrnAsync(grn.Data!.Id);
        posted.Succeeded.Should().BeTrue();

        var item = h.Db.InventoryItems.Single(i => i.ProductId == 1 && i.WarehouseId == 1);
        item.QuantityOnHand.Should().Be(10);

        var bal = h.Db.InventoryLocationBalances.Single(b => b.WarehouseLocationId == 2);
        bal.QuantityOnHand.Should().Be(10);
        bal.InventoryItemId.Should().Be(item.Id);
    }

    [Fact]
    public async Task GrnPost_WithoutBin_UsesReceivingDefault()
    {
        var h = await CreateAsync();
        var grn = await h.Inventory.CreateGrnAsync(new CreateGrnRequest(
            1, null, DateTime.UtcNow, 0, null,
            [new CreateGrnLineRequest(1, 5, 40)]));
        grn.Succeeded.Should().BeTrue();
        var posted = await h.Inventory.PostGrnAsync(grn.Data!.Id);
        posted.Succeeded.Should().BeTrue();

        h.Db.InventoryLocationBalances.Single(b => b.WarehouseLocationId == 1).QuantityOnHand.Should().Be(5);
    }

    [Fact]
    public async Task CycleCount_ByBin_PostsVarianceToLocation()
    {
        var h = await CreateAsync();
        var grn = await h.Inventory.CreateGrnAsync(new CreateGrnRequest(
            1, null, DateTime.UtcNow, 0, null,
            [new CreateGrnLineRequest(1, 10, 50, WarehouseLocationId: 2)]));
        await h.Inventory.PostGrnAsync(grn.Data!.Id);

        var cc = await h.Inventory.CreateCycleCountAsync(new CreateCycleCountRequest(
            1, DateTime.UtcNow, null,
            [new CreateCycleCountLineRequest(1, 8, 2)],
            WarehouseLocationId: 2));
        cc.Succeeded.Should().BeTrue();
        cc.Data!.Lines[0].SystemQuantity.Should().Be(10);
        cc.Data.WarehouseLocationId.Should().Be(2);

        var done = await h.Inventory.CompleteCycleCountAsync(cc.Data.Id);
        done.Succeeded.Should().BeTrue();

        h.Db.InventoryItems.Single().QuantityOnHand.Should().Be(8);
        h.Db.InventoryLocationBalances.Single(b => b.WarehouseLocationId == 2).QuantityOnHand.Should().Be(8);
    }

    [Fact]
    public async Task Transfer_RequiresPick_BeforeShip_AndMovesBinStock()
    {
        var h = await CreateAsync();
        var grn = await h.Inventory.CreateGrnAsync(new CreateGrnRequest(
            1, null, DateTime.UtcNow, 0, null,
            [new CreateGrnLineRequest(1, 10, 50, WarehouseLocationId: 2)]));
        await h.Inventory.PostGrnAsync(grn.Data!.Id);

        var created = await h.Transfers.CreateAsync(new TransferCreateDto(
            1, 2, null,
            [new TransferLineDto(1, null, 4, FromLocationId: 2, ToLocationId: 3)]));
        created.Succeeded.Should().BeTrue();

        var approve = await h.Transfers.ApproveAsync(created.Data!.Id);
        approve.Succeeded.Should().BeTrue();

        var shipBlocked = await h.Transfers.ShipAsync(created.Data.Id);
        shipBlocked.Succeeded.Should().BeFalse();
        shipBlocked.Error.Should().Contain("pick");

        var pick = await h.Transfers.ConfirmPickAsync(created.Data.Id);
        pick.Succeeded.Should().BeTrue();
        pick.Data!.AllLinesPicked.Should().BeTrue();

        var ship = await h.Transfers.ShipAsync(created.Data.Id);
        ship.Succeeded.Should().BeTrue();

        h.Db.InventoryItems.Single(i => i.WarehouseId == 1).QuantityOnHand.Should().Be(6);
        h.Db.InventoryLocationBalances.Single(b => b.WarehouseLocationId == 2).QuantityOnHand.Should().Be(6);

        var recv = await h.Transfers.CompleteAsync(created.Data.Id);
        recv.Succeeded.Should().BeTrue();
        h.Db.InventoryItems.Single(i => i.WarehouseId == 2).QuantityOnHand.Should().Be(4);
        h.Db.InventoryLocationBalances.Single(b => b.WarehouseLocationId == 3).QuantityOnHand.Should().Be(4);
    }

    [Fact]
    public void AtpPolicy_IsWarehouseLevel_Documented()
    {
        // ATP = InventoryItem.QuantityOnHand − ReservedQuantity (warehouse rollup).
        // Location balances are putaway/pick dimensions kept in sync; POS/ATP do not require a bin.
        LocationBalanceSync.DefaultCode.Should().Be("MAIN");
    }
}
