using AutoMapper;
using CarAutoParts.Application.Mapping;
using CarAutoParts.Application.Services;
using CarAutoParts.Application.Validators;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using CarAutoParts.Infrastructure.Repositories;
using FluentAssertions;
using FluentValidation;

namespace CarAutoParts.Application.Tests;

public class FifoCostingTests
{
    [Fact]
    public async Task DeductStockAsync_UsesOldestBatchFirst_ForFifoValuation()
    {
        await using var db = TestDbContextFactory.Create();

        var product = new Product
        {
            Name = "Oil Filter",
            Sku = "OF-001",
            CategoryId = 1,
            BrandId = 1,
            Unit = "PCS",
            PurchasePrice = 50,
            SalePrice = 80,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        };
        db.Products.Add(product);
        db.Warehouses.Add(new Warehouse { Name = "Main", CreatedAt = DateTime.UtcNow, CreatedBy = "test" });
        await db.SaveChangesAsync();

        var inventoryItem = new InventoryItem
        {
            ProductId = product.Id,
            WarehouseId = 1,
            QuantityOnHand = 0,
            ValuationMethod = ValuationMethod.Fifo,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        };
        db.InventoryItems.Add(inventoryItem);
        await db.SaveChangesAsync();

        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>()).CreateMapper();
        var service = new InventoryService(
            new Repository<InventoryItem>(db),
            new Repository<StockMovement>(db),
            new Repository<StockBatch>(db),
            new Repository<Product>(db),
            new Repository<Warehouse>(db),
            new UnitOfWork(db),
            mapper,
            new StockAdjustmentValidator());

        await service.ReceiveStockAsync(product.Id, 1, 10, 50m, "BATCH-A");
        await service.ReceiveStockAsync(product.Id, 1, 10, 60m, "BATCH-B");

        var deduct = await service.DeductStockAsync(product.Id, 1, 15, "Sale", 1);

        deduct.Succeeded.Should().BeTrue();

        var batches = db.StockBatches.OrderBy(b => b.ReceivedDate).ToList();
        batches[0].QuantityRemaining.Should().Be(0);
        batches[1].QuantityRemaining.Should().Be(5);

        var item = await db.InventoryItems.FindAsync(inventoryItem.Id);
        item!.QuantityOnHand.Should().Be(5);
    }
}
