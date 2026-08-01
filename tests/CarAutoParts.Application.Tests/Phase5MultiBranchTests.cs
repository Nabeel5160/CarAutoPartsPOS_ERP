using AutoMapper;
using CarAutoParts.Application.DTOs.Pos;
using CarAutoParts.Application.DTOs.Transfers;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Mapping;
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

public class Phase5MultiBranchTests
{
    [Fact]
    public async Task ShipAsync_InterBranch_PostsGitGl_AndPreservesCost()
    {
        await using var db = await SeedTransferDbAsync(fromBranch: 1, toBranch: 2, unitCost: 12.5m);
        var gl = new Mock<IGlPostingService>();
        gl.Setup(g => g.PostDocumentAsync(
                "InventoryTransfer",
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<IReadOnlyList<GlPostingLineRequest>>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Application.Common.Result<GlJournalDraftDto>.Success(
                new GlJournalDraftDto(1, "JV-1", JournalStatus.Posted, 25m, 25m)));

        var service = CreateTransferService(db, gl.Object);
        var transferId = db.InventoryTransfers.Single().Id;

        (await service.ApproveAsync(transferId)).Succeeded.Should().BeTrue();
        (await service.ShipAsync(transferId)).Succeeded.Should().BeTrue();

        var line = await db.InventoryTransferLines.SingleAsync();
        line.ShippedUnitCost.Should().Be(12.5m);

        gl.Verify(g => g.PostDocumentAsync(
            "InventoryTransfer",
            It.IsAny<DateTime>(),
            It.IsAny<string>(),
            It.Is<string>(d => d.Contains("Ship")),
            transferId,
            It.Is<IReadOnlyList<GlPostingLineRequest>>(lines =>
                lines.Any(l => l.MappingKey == "GoodsInTransit" && l.IsDebit)
                && lines.Any(l => l.MappingKey == "Inventory" && !l.IsDebit)
                && lines.Sum(l => l.IsDebit ? l.Amount : 0) == 25m),
            true,
            It.IsAny<CancellationToken>()), Times.Once);

        (await service.CompleteAsync(transferId)).Succeeded.Should().BeTrue();

        gl.Verify(g => g.PostDocumentAsync(
            "InventoryTransfer",
            It.IsAny<DateTime>(),
            It.IsAny<string>(),
            It.Is<string>(d => d.Contains("Receive")),
            transferId,
            It.IsAny<IReadOnlyList<GlPostingLineRequest>>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);

        var dest = await db.InventoryItems.SingleAsync(i => i.WarehouseId == 2);
        dest.AverageCost.Should().Be(12.5m);
        dest.QuantityOnHand.Should().Be(2m);
    }

    [Fact]
    public async Task ShipAsync_SameBranch_SkipsGl()
    {
        await using var db = await SeedTransferDbAsync(fromBranch: 1, toBranch: 1, unitCost: 10m);
        var gl = new Mock<IGlPostingService>();
        var service = CreateTransferService(db, gl.Object);
        var transferId = db.InventoryTransfers.Single().Id;

        (await service.ApproveAsync(transferId)).Succeeded.Should().BeTrue();
        (await service.ShipAsync(transferId)).Succeeded.Should().BeTrue();
        (await service.CompleteAsync(transferId)).Succeeded.Should().BeTrue();

        gl.Verify(g => g.PostDocumentAsync(
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<IReadOnlyList<GlPostingLineRequest>>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CloseShift_WithCashShort_PostsOverShortGl()
    {
        await using var db = TestDbContextFactory.Create();
        var user = new AppUser { Id = 9, Username = "cashier", DisplayName = "C", IsActive = true };
        db.Users.Add(user);
        db.CashierShifts.Add(new CashierShift
        {
            Id = 1,
            ShiftNumber = "SHF-1",
            UserId = 9,
            UserName = "cashier",
            Status = CashierShiftStatus.Open,
            OpeningFloat = 100m,
            OpenedAt = DateTime.UtcNow.AddHours(-2),
            BranchId = 1,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var currentUser = new CurrentUserService();
        currentUser.SetUser(user, [Application.Constants.Permissions.PosShift]);
        var company = new CurrentCompanyContext();
        company.Set(1, 1, [1]);

        var gl = new Mock<IGlPostingService>();
        gl.Setup(g => g.PostDocumentAsync(
                "CashierShift",
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<IReadOnlyList<GlPostingLineRequest>>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Application.Common.Result<GlJournalDraftDto>.Success(
                new GlJournalDraftDto(42, "JV-VAR", JournalStatus.Posted, 10m, 10m)));

        var floor = new PosFloorService(
            new Repository<HeldSale>(db),
            new Repository<CashierShift>(db),
            new Repository<SalesInvoice>(db),
            new Repository<Payment>(db),
            new Repository<SalesReturn>(db),
            new Repository<Product>(db),
            new Repository<Warehouse>(db),
            new Repository<CostCenter>(db),
            new Repository<Till>(db),
            new Repository<SafeDrop>(db),
            new Repository<Branch>(db),
            new UnitOfWork(db),
            currentUser,
            company,
            gl.Object);

        // Expected = opening 100 + cash sales 0 = 100; declare 90 → short 10
        var result = await floor.CloseShiftAsync(1, new CloseShiftRequestDto(90m, null, 90m));
        result.Succeeded.Should().BeTrue();
        result.Data!.CashVariance.Should().Be(10m);
        result.Data.ExpectedCash.Should().Be(100m);
        result.Data.DeclaredClosingCash.Should().Be(90m);

        gl.Verify(g => g.PostDocumentAsync(
            "CashierShift",
            It.IsAny<DateTime>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            1,
            It.Is<IReadOnlyList<GlPostingLineRequest>>(lines =>
                lines.Any(l => l.MappingKey == "OverShort" && l.IsDebit && l.Amount == 10m)
                && lines.Any(l => l.MappingKey == "Cash" && !l.IsDebit)),
            true,
            It.IsAny<CancellationToken>()), Times.Once);

        var shift = await db.CashierShifts.SingleAsync();
        shift.VarianceJournalEntryId.Should().Be(42);
        shift.Status.Should().Be(CashierShiftStatus.Closed);
    }

    [Fact]
    public async Task Dashboard_FiltersSalesByBranchWarehouses()
    {
        await using var db = TestDbContextFactory.Create();
        db.Warehouses.AddRange(
            new Warehouse { Id = 1, Name = "A", BranchId = 1, CreatedAt = DateTime.UtcNow },
            new Warehouse { Id = 2, Name = "B", BranchId = 2, CreatedAt = DateTime.UtcNow });
        db.SalesInvoices.AddRange(
            new SalesInvoice
            {
                InvoiceNumber = "S1", InvoiceDate = DateTime.UtcNow, GrandTotal = 500, WarehouseId = 1,
                CreatedAt = DateTime.UtcNow, PaymentStatus = PaymentStatus.Paid
            },
            new SalesInvoice
            {
                InvoiceNumber = "S2", InvoiceDate = DateTime.UtcNow, GrandTotal = 200, WarehouseId = 2,
                CreatedAt = DateTime.UtcNow, PaymentStatus = PaymentStatus.Paid
            });
        await db.SaveChangesAsync();

        var company = new CurrentCompanyContext();
        company.Set(1, null, [1, 2]);
        var notifications = new Mock<INotificationService>();
        notifications.Setup(n => n.GetUnreadCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var dash = new DashboardService(
            new Repository<SalesInvoice>(db),
            new Repository<SalesInvoiceLine>(db),
            new Repository<PurchaseOrder>(db),
            new Repository<InventoryItem>(db),
            new Repository<Product>(db),
            new Repository<InventoryTransfer>(db),
            new Repository<Warehouse>(db),
            new Repository<CashierShift>(db),
            notifications.Object,
            company);

        var branch1 = await dash.GetDashboardAsync(1);
        branch1.TodaySales.Should().Be(500m);
        branch1.BranchId.Should().Be(1);

        var branch2 = await dash.GetDashboardAsync(2);
        branch2.TodaySales.Should().Be(200m);
        branch2.BranchId.Should().Be(2);
    }

    private static async Task<ApplicationDbContext> SeedTransferDbAsync(int fromBranch, int toBranch, decimal unitCost)
    {
        var db = TestDbContextFactory.Create();
        db.Categories.Add(new Category { Id = 1, Name = "C", CreatedAt = DateTime.UtcNow });
        db.Warehouses.AddRange(
            new Warehouse { Id = 1, Name = "From", BranchId = fromBranch, CompanyId = 1, CreatedAt = DateTime.UtcNow },
            new Warehouse { Id = 2, Name = "To", BranchId = toBranch, CompanyId = 1, CreatedAt = DateTime.UtcNow });
        db.Products.Add(new Product
        {
            Id = 1, Name = "P", Sku = "P1", CategoryId = 1, Unit = "PCS",
            PurchasePrice = unitCost, CostPrice = unitCost, SalePrice = unitCost * 2,
            IsActive = true, CompanyId = 1, CreatedAt = DateTime.UtcNow
        });
        db.CompanySettings.Add(new CompanySettings
        {
            DefaultValuationMethod = ValuationMethod.Average,
            AllowNegativeStock = false,
            CreatedAt = DateTime.UtcNow
        });
        db.InventoryItems.Add(new InventoryItem
        {
            ProductId = 1, WarehouseId = 1, QuantityOnHand = 10, AverageCost = unitCost,
            ValuationMethod = ValuationMethod.Average, CreatedAt = DateTime.UtcNow
        });
        db.InventoryTransfers.Add(new InventoryTransfer
        {
            TransferNumber = "TR-P5",
            FromWarehouseId = 1,
            ToWarehouseId = 2,
            Status = TransferStatus.Draft,
            TransferDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Lines =
            {
                new InventoryTransferLine { ProductId = 1, Quantity = 2, CreatedAt = DateTime.UtcNow }
            }
        });
        await db.SaveChangesAsync();
        return db;
    }

    private static TransferService CreateTransferService(ApplicationDbContext db, IGlPostingService gl)
    {
        var company = new CurrentCompanyContext();
        company.Set(1, 1, [1, 2]);
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>()).CreateMapper();
        var inventory = TestInventoryFactory.Create(db, mapper);
        return new TransferService(
            new Repository<InventoryTransfer>(db),
            new Repository<Warehouse>(db),
            new Repository<Product>(db),
            inventory,
            new CurrentUserService(),
            company,
            gl,
            new UnitOfWork(db),
            mapper,
            approvals: CreatePassThroughApprovals(),
            moneyAudit: Mock.Of<IMoneyAuditService>());
    }

    private static IApprovalWorkflowService CreatePassThroughApprovals()
    {
        var approvals = new Mock<IApprovalWorkflowService>();
        approvals.Setup(a => a.EnsureApprovedOrQueueAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Application.Common.Result.Success());
        return approvals.Object;
    }
}
