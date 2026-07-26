using AutoMapper;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Mapping;
using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using CarAutoParts.Infrastructure.Repositories;
using FluentAssertions;
using Moq;

namespace CarAutoParts.Application.Tests;

public class TransferServiceTests
{
    [Fact]
    public async Task ApproveAsync_OnNonDraftTransfer_ReturnsFailure()
    {
        await using var db = TestDbContextFactory.Create();
        var fromWarehouse = new Warehouse { Name = "Main", IsDefault = true, CreatedAt = DateTime.UtcNow, CreatedBy = "test" };
        var toWarehouse = new Warehouse { Name = "Branch", CreatedAt = DateTime.UtcNow, CreatedBy = "test" };
        db.Warehouses.AddRange(fromWarehouse, toWarehouse);
        await db.SaveChangesAsync();

        var transfer = new InventoryTransfer
        {
            TransferNumber = "TR-TEST-0001",
            FromWarehouseId = fromWarehouse.Id,
            ToWarehouseId = toWarehouse.Id,
            Status = TransferStatus.Approved,
            TransferDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        };
        db.InventoryTransfers.Add(transfer);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ApproveAsync(transfer.Id);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("draft");
    }

    [Fact]
    public async Task CreateAsync_WithSameWarehouses_ReturnsFailure()
    {
        await using var db = TestDbContextFactory.Create();
        var warehouse = new Warehouse { Name = "Main", IsDefault = true, CreatedAt = DateTime.UtcNow, CreatedBy = "test" };
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.CreateAsync(new Application.DTOs.Transfers.TransferCreateDto(
            warehouse.Id,
            warehouse.Id,
            null,
            [new Application.DTOs.Transfers.TransferLineDto(1, "Test", 1)]));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("differ");
    }

    [Fact]
    public async Task GetTransfersAsync_WithStatusFilter_ReturnsMatchingItems()
    {
        await using var db = TestDbContextFactory.Create();
        var fromWarehouse = new Warehouse { Name = "Main", IsDefault = true, CreatedAt = DateTime.UtcNow, CreatedBy = "test" };
        var toWarehouse = new Warehouse { Name = "Branch", CreatedAt = DateTime.UtcNow, CreatedBy = "test" };
        db.Warehouses.AddRange(fromWarehouse, toWarehouse);
        await db.SaveChangesAsync();
        db.InventoryTransfers.AddRange(
            new InventoryTransfer
            {
                TransferNumber = "TR-1",
                FromWarehouseId = fromWarehouse.Id,
                ToWarehouseId = toWarehouse.Id,
                Status = TransferStatus.Draft,
                TransferDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test"
            },
            new InventoryTransfer
            {
                TransferNumber = "TR-2",
                FromWarehouseId = fromWarehouse.Id,
                ToWarehouseId = toWarehouse.Id,
                Status = TransferStatus.Completed,
                TransferDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test"
            });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.GetTransfersAsync(new Application.Common.QuerySpec
        {
            Filters = { ["Status"] = TransferStatus.Draft }
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].TransferNumber.Should().Be("TR-1");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsDetailWithLines()
    {
        await using var db = TestDbContextFactory.Create();
        var fromWarehouse = new Warehouse { Name = "Main", IsDefault = true, CreatedAt = DateTime.UtcNow, CreatedBy = "test" };
        var toWarehouse = new Warehouse { Name = "Branch", CreatedAt = DateTime.UtcNow, CreatedBy = "test" };
        var product = new Product
        {
            Name = "Oil Filter",
            Sku = "OF-001",
            SalePrice = 10,
            PurchasePrice = 5,
            CategoryId = 1,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        };
        db.Warehouses.AddRange(fromWarehouse, toWarehouse);
        db.Categories.Add(new Category { Name = "Filters", CreatedAt = DateTime.UtcNow, CreatedBy = "test" });
        await db.SaveChangesAsync();
        product.CategoryId = db.Categories.First().Id;
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var transfer = new InventoryTransfer
        {
            TransferNumber = "TR-DETAIL",
            FromWarehouseId = fromWarehouse.Id,
            ToWarehouseId = toWarehouse.Id,
            Status = TransferStatus.Draft,
            TransferDate = DateTime.UtcNow,
            Notes = "Test",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
            Lines =
            {
                new InventoryTransferLine { ProductId = product.Id, Quantity = 3, CreatedAt = DateTime.UtcNow, CreatedBy = "test" }
            }
        };
        db.InventoryTransfers.Add(transfer);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var detail = await service.GetByIdAsync(transfer.Id);

        detail.Should().NotBeNull();
        detail!.TransferNumber.Should().Be("TR-DETAIL");
        detail.Lines.Should().HaveCount(1);
        detail.Lines[0].Quantity.Should().Be(3);
    }

    private static TransferService CreateService(Infrastructure.Data.ApplicationDbContext db)
    {
        var transfers = new Repository<InventoryTransfer>(db);
        var warehouses = new Repository<Warehouse>(db);
        var products = new Repository<Product>(db);
        var inventory = new Mock<IInventoryService>();
        var currentUser = new CurrentUserService();
        var unitOfWork = new UnitOfWork(db);
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>()).CreateMapper();
        return new TransferService(transfers, warehouses, products, inventory.Object, currentUser, unitOfWork, mapper);
    }
}
