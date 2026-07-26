using CarAutoParts.Application.DTOs.Fbr;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Services;
using CarAutoParts.Application.Validators;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Infrastructure.Repositories;
using FluentAssertions;
using Moq;

namespace CarAutoParts.Application.Tests;

public class PosCheckoutServiceTests
{
    [Fact]
    public async Task GetPosProductsAsync_ReturnsActiveProductsWithStock()
    {
        await using var db = TestDbContextFactory.Create();
        var product = new Product
        {
            Name = "Spark Plug",
            Sku = "SP-001",
            CategoryId = 1,
            BrandId = 1,
            Unit = "PCS",
            PurchasePrice = 20,
            SalePrice = 35,
            TaxRatePercent = 18,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        };
        db.Products.Add(product);
        db.Warehouses.Add(new Warehouse { Name = "Main", CreatedAt = DateTime.UtcNow, CreatedBy = "test" });
        await db.SaveChangesAsync();

        db.InventoryItems.Add(new InventoryItem
        {
            ProductId = product.Id,
            WarehouseId = 1,
            QuantityOnHand = 25,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();

        var inventoryMock = new Mock<IInventoryService>();
        var fbrMock = new Mock<IFbrService>();
        fbrMock
            .Setup(f => f.PostInvoiceAsync(It.IsAny<FbrInvoiceRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FbrPostResultDto.Ok("FBR-001", true, null, null));

        var gl = new Mock<IGlPostingService>();
        var fbrOutbox = new Mock<IFbrOutboxService>();
        var company = new CurrentCompanyContext();
        var salesEnt = new Mock<IEnterpriseSalesService>();

        var service = new PosCheckoutService(
            new Repository<Product>(db),
            new Repository<SalesInvoice>(db),
            new Repository<FbrSubmission>(db),
            new Repository<CompanySettings>(db),
            new Repository<Payment>(db),
            new Repository<Customer>(db),
            new Repository<ProductKit>(db),
            new Repository<CashierShift>(db),
            new Repository<HeldSale>(db),
            inventoryMock.Object,
            fbrMock.Object,
            new UnitOfWork(db),
            new PosCheckoutValidator(),
            gl.Object,
            fbrOutbox.Object,
            company,
            new CurrentUserService(),
            salesEnt.Object);

        var products = await service.GetPosProductsAsync(null);

        products.Should().HaveCount(1);
        products[0].Name.Should().Be("Spark Plug");
        products[0].AvailableStock.Should().Be(25);
    }
}
