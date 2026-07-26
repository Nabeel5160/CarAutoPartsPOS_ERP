using AutoMapper;
using CarAutoParts.Application;
using CarAutoParts.Application.DTOs.Inventory;
using CarAutoParts.Application.DTOs.Products;
using CarAutoParts.Application.DTOs.Purchases;
using CarAutoParts.Application.DTOs.Sales;
using CarAutoParts.Application.Mapping;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CarAutoParts.Application.Tests;

public class MappingProfileTests
{
    [Fact]
    public void MappingProfile_IsValid()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        config.AssertConfigurationIsValid();
    }

    [Fact]
    public void DiRegisteredMapper_MapsCoreModuleDtos()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        var mapper = services.BuildServiceProvider().GetRequiredService<IMapper>();

        var product = new Product
        {
            Id = 1,
            Name = "Oil Filter",
            Sku = "OF-001",
            CategoryId = 1,
            BrandId = 1,
            SalePrice = 100,
            CostPrice = 50,
            MinimumStock = 5,
            IsActive = true,
            Category = new Category { Name = "Filters" },
            Brand = new Brand { Name = "Toyota" },
            InventoryItems = { new InventoryItem { QuantityOnHand = 10 } }
        };

        mapper.Map<ProductListDto>(product).Name.Should().Be("Oil Filter");

        var movement = new StockMovement
        {
            Id = 1,
            MovementType = StockMovementType.Purchase,
            Quantity = 5,
            UnitCost = 50,
            MovementDate = DateTime.UtcNow,
            InventoryItem = new InventoryItem
            {
                ProductId = 1,
                WarehouseId = 1,
                Product = product,
                Warehouse = new Warehouse { Name = "Main" }
            }
        };

        mapper.Map<StockMovementDto>(movement).ProductName.Should().Be("Oil Filter");

        var po = new PurchaseOrder
        {
            Id = 1,
            OrderNumber = "PO-1",
            Status = PurchaseOrderStatus.Draft,
            OrderDate = DateTime.UtcNow,
            GrandTotal = 100,
            Supplier = new Supplier { Name = "Supplier A" }
        };

        mapper.Map<PurchaseOrderListDto>(po).SupplierName.Should().Be("Supplier A");

        var invoice = new SalesInvoice
        {
            Id = 1,
            InvoiceNumber = "INV-1",
            InvoiceDate = DateTime.UtcNow,
            GrandTotal = 200,
            PaymentStatus = PaymentStatus.Paid,
            Customer = new Customer { Name = "Ali" }
        };

        mapper.Map<SalesInvoiceListDto>(invoice).CustomerName.Should().Be("Ali");
    }
}
