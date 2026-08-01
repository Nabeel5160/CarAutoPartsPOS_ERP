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
using AutoMapper;
using Xunit;

namespace CarAutoParts.Application.Tests;

public class Phase3InventoryTests
{
    [Fact]
    public async Task Atp_subtracts_reserved_from_on_hand()
    {
        await using var db = TestDbContextFactory.Create();
        db.Warehouses.Add(new Warehouse { Id = 1, Name = "Main", CreatedAt = DateTime.UtcNow });
        db.Categories.Add(new Category { Id = 1, Name = "C", CreatedAt = DateTime.UtcNow });
        db.Brands.Add(new Brand { Id = 1, Name = "B", CreatedAt = DateTime.UtcNow });
        db.Products.Add(new Product
        {
            Id = 1, Name = "Pad", Sku = "P1", CategoryId = 1, BrandId = 1, Unit = "PCS",
            IsActive = true, CreatedAt = DateTime.UtcNow
        });
        db.InventoryItems.Add(new InventoryItem
        {
            ProductId = 1, WarehouseId = 1, QuantityOnHand = 10, ReservedQuantity = 3, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var atp = new AtpService(new EnterpriseDbAdapter(db));
        (await atp.GetAvailableAsync(1, 1)).Should().Be(7);
        var ensure = await atp.EnsureAvailableAsync(1, 1, 8);
        ensure.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Transfer_ship_then_receive_moves_stock()
    {
        await using var db = TestDbContextFactory.Create();
        var from = new Warehouse { Name = "Main", CreatedAt = DateTime.UtcNow };
        var to = new Warehouse { Name = "Branch", CreatedAt = DateTime.UtcNow };
        db.Warehouses.AddRange(from, to);
        db.Categories.Add(new Category { Name = "C", CreatedAt = DateTime.UtcNow });
        db.Brands.Add(new Brand { Name = "B", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var product = new Product
        {
            Name = "Pad", Sku = "P1", CategoryId = db.Categories.First().Id, BrandId = db.Brands.First().Id,
            Unit = "PCS", IsActive = true, CreatedAt = DateTime.UtcNow
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        db.InventoryItems.Add(new InventoryItem
        {
            ProductId = product.Id, WarehouseId = from.Id, QuantityOnHand = 20, AverageCost = 10, CreatedAt = DateTime.UtcNow
        });
        db.CompanySettings.Add(new CompanySettings { AllowNegativeStock = false, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>()).CreateMapper();
        var inventory = TestInventoryFactory.Create(db, mapper);
        var approvals = new Mock<IApprovalWorkflowService>();
        approvals.Setup(a => a.EnsureApprovedOrQueueAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Application.Common.Result.Success());
        var transfers = new TransferService(
            new Repository<InventoryTransfer>(db),
            new Repository<Warehouse>(db),
            new Repository<Product>(db),
            inventory,
            new CurrentUserService(),
            new CurrentCompanyContext(),
            new Mock<IGlPostingService>().Object,
            new UnitOfWork(db),
            mapper,
            approvals.Object,
            Mock.Of<IMoneyAuditService>());

        var created = await transfers.CreateAsync(new TransferCreateDto(
            from.Id, to.Id, null,
            [new TransferLineDto(product.Id, product.Name, 5)]));
        created.Succeeded.Should().BeTrue();
        (await transfers.ApproveAsync(created.Data!.Id)).Succeeded.Should().BeTrue();
        (await transfers.ShipAsync(created.Data.Id)).Succeeded.Should().BeTrue();

        var midFrom = await db.InventoryItems.SingleAsync(i => i.WarehouseId == from.Id);
        midFrom.QuantityOnHand.Should().Be(15);
        (await db.InventoryItems.CountAsync(i => i.WarehouseId == to.Id)).Should().Be(0);

        (await transfers.CompleteAsync(created.Data.Id)).Succeeded.Should().BeTrue();
        var dest = await db.InventoryItems.SingleAsync(i => i.WarehouseId == to.Id);
        dest.QuantityOnHand.Should().Be(5);
    }

    [Fact]
    public async Task AllowNegativeStock_permits_over_deduct()
    {
        await using var db = TestDbContextFactory.Create();
        db.Warehouses.Add(new Warehouse { Id = 1, Name = "Main", CreatedAt = DateTime.UtcNow });
        db.Categories.Add(new Category { Id = 1, Name = "C", CreatedAt = DateTime.UtcNow });
        db.Brands.Add(new Brand { Id = 1, Name = "B", CreatedAt = DateTime.UtcNow });
        db.Products.Add(new Product
        {
            Id = 1, Name = "Pad", Sku = "P1", CategoryId = 1, BrandId = 1, Unit = "PCS",
            IsActive = true, CreatedAt = DateTime.UtcNow
        });
        db.InventoryItems.Add(new InventoryItem
        {
            ProductId = 1, WarehouseId = 1, QuantityOnHand = 2, AverageCost = 5, CreatedAt = DateTime.UtcNow
        });
        db.CompanySettings.Add(new CompanySettings { AllowNegativeStock = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>()).CreateMapper();
        var inventory = TestInventoryFactory.Create(db, mapper);

        var result = await inventory.DeductStockAsync(1, 1, 5, "Sale", 1);
        result.Succeeded.Should().BeTrue();
        (await db.InventoryItems.SingleAsync()).QuantityOnHand.Should().Be(-3);
    }
}
