using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Analytics;
using CarAutoParts.Application.DTOs.Inventory;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Mapping;
using CarAutoParts.Application.Services;
using CarAutoParts.Application.Validators;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using CarAutoParts.Infrastructure.Data;
using CarAutoParts.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using AutoMapper;

namespace CarAutoParts.Application.Tests;

public class Phase7InsightsTests
{
    [Fact]
    public async Task Analytics_includes_dead_stock_and_gross_margin()
    {
        await using var db = TestDbContextFactory.Create();
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "T", CurrencyCode = "PKR", IsActive = true });
        db.Branches.Add(new Branch { Id = 1, CompanyId = 1, Code = "HQ", Name = "HQ", IsDefault = true, IsActive = true });
        db.Warehouses.Add(new Warehouse { Id = 1, Name = "Main", BranchId = 1, IsDefault = true });
        db.Products.Add(new Product
        {
            Id = 1, Sku = "DEAD-1", Name = "Dead Part", CostPrice = 10, PurchasePrice = 10, SalePrice = 20,
            IsActive = true, CreatedAt = DateTime.UtcNow
        });
        db.Products.Add(new Product
        {
            Id = 2, Sku = "FAST-1", Name = "Fast Part", CostPrice = 5, PurchasePrice = 5, SalePrice = 15,
            IsActive = true, CreatedAt = DateTime.UtcNow
        });
        db.InventoryItems.Add(new InventoryItem
        {
            ProductId = 1, WarehouseId = 1, QuantityOnHand = 10, AverageCost = 10, CreatedAt = DateTime.UtcNow
        });
        var invoice = new SalesInvoice
        {
            InvoiceNumber = "INV-1", InvoiceDate = DateTime.UtcNow.Date, WarehouseId = 1,
            SubTotal = 150, GrandTotal = 150, PaymentStatus = PaymentStatus.Paid, CreatedAt = DateTime.UtcNow
        };
        invoice.Lines.Add(new SalesInvoiceLine
        {
            ProductId = 2, ProductName = "Fast Part", Sku = "FAST-1", Quantity = 10,
            UnitPrice = 15, LineTotal = 150, UnitCost = 5, CreatedAt = DateTime.UtcNow
        });
        db.SalesInvoices.Add(invoice);
        await db.SaveChangesAsync();

        var company = new CurrentCompanyContext();
        company.Set(1, 1, [1]);
        var svc = new AnalyticsService(
            new Repository<SalesInvoiceLine>(db),
            new Repository<InventoryItem>(db),
            new Repository<Product>(db),
            new Repository<Warehouse>(db),
            company);

        var dto = await svc.GetAnalyticsAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow, branchId: 1, deadStockDays: 30);
        dto.DeadStock.Should().Contain(d => d.ProductId == 1 && d.StockValue == 100);
        dto.FastMovers.Should().Contain(f => f.ProductId == 2);
        dto.GrossMarginAmount.Should().Be(100);
        dto.GrossMarginPercent.Should().BeApproximately(66.67m, 0.1m);
    }

    [Fact]
    public async Task InventoryValue_Fifo_differs_from_Average_when_layers_differ()
    {
        await using var db = TestDbContextFactory.Create();
        db.Warehouses.Add(new Warehouse { Id = 1, Name = "Main", IsDefault = true });
        db.Products.Add(new Product { Id = 1, Sku = "P1", Name = "P1", IsActive = true, CreatedAt = DateTime.UtcNow });
        var item = new InventoryItem
        {
            ProductId = 1, WarehouseId = 1, QuantityOnHand = 10, AverageCost = 10,
            ValuationMethod = ValuationMethod.Fifo, CreatedAt = DateTime.UtcNow
        };
        db.InventoryItems.Add(item);
        await db.SaveChangesAsync();
        db.StockBatches.Add(new StockBatch
        {
            InventoryItemId = item.Id, BatchNumber = "A", QuantityRemaining = 10, UnitCost = 20,
            ReceivedDate = DateTime.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();

        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>()).CreateMapper();
        var inventory = TestInventoryFactory.Create(db, mapper);

        var avg = await inventory.GetInventoryValueAsync("Average", null, null);
        var fifo = await inventory.GetInventoryValueAsync("Fifo", null, null);
        avg.Value.Should().Be(100);
        fifo.Value.Should().Be(200);
        fifo.Method.Should().Be("Fifo");
    }

    [Fact]
    public async Task Deduct_creates_low_stock_notification()
    {
        await using var db = TestDbContextFactory.Create();
        db.Warehouses.Add(new Warehouse { Id = 1, Name = "Main", IsDefault = true });
        db.Products.Add(new Product
        {
            Id = 1, Sku = "L1", Name = "Low", ReorderLevel = 5, MinimumStock = 5,
            IsActive = true, CreatedAt = DateTime.UtcNow
        });
        db.InventoryItems.Add(new InventoryItem
        {
            ProductId = 1, WarehouseId = 1, QuantityOnHand = 6, AverageCost = 1, CreatedAt = DateTime.UtcNow
        });
        db.CompanySettings.Add(new CompanySettings { AllowNegativeStock = false, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var notifications = new Mock<INotificationService>();
        notifications.Setup(n => n.GetNotificationsAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Application.DTOs.System.NotificationDto>());
        notifications.Setup(n => n.CreateNotificationAsync(
                NotificationType.LowStock, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>()).CreateMapper();
        var inventory = TestInventoryFactory.Create(db, mapper, notifications: notifications.Object);

        var result = await inventory.DeductStockAsync(1, 1, 2, "Sale", 1);
        result.Succeeded.Should().BeTrue();
        result.Data.Should().Be(1);
        notifications.Verify(n => n.CreateNotificationAsync(
            NotificationType.LowStock, It.IsAny<string>(), It.Is<string>(m => m.Contains("[warehouseId=1]")),
            "InventoryItem", 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuditService_filters_by_Void_action()
    {
        await using var db = TestDbContextFactory.Create();
        db.AuditLogs.Add(new AuditLog
        {
            Action = AuditAction.Void, EntityType = "JournalEntry", EntityId = 1,
            UserName = "admin", Timestamp = DateTime.UtcNow, CreatedAt = DateTime.UtcNow
        });
        db.AuditLogs.Add(new AuditLog
        {
            Action = AuditAction.Update, EntityType = "Product", EntityId = 2,
            UserName = "admin", Timestamp = DateTime.UtcNow, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>()).CreateMapper();
        var svc = new AuditService(new Repository<AuditLog>(db), mapper);
        var result = await svc.GetAuditLogsAsync(new QuerySpec { Search = "Void", PageSize = 50 });
        result.Items.Should().HaveCount(1);
        result.Items[0].Action.Should().Be(AuditAction.Void);
    }

    [Fact]
    public void CashierShiftDto_includes_variance_journal_id()
    {
        var dto = new Application.DTOs.Pos.CashierShiftDto(
            1, "S1", 1, "admin", 1, "Closed", 100, 90, DateTime.UtcNow, DateTime.UtcNow,
            1, 100, 90, 10, 55);
        dto.VarianceJournalEntryId.Should().Be(55);
    }
}
