using CarAutoParts.Application.Common;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using CarAutoParts.Infrastructure.Data;
using FluentAssertions;
using Moq;
using Xunit;

namespace CarAutoParts.Application.Tests;

public class Phase14WholesaleLoopTests
{
    private sealed class Harness
    {
        public required ApplicationDbContext Db { get; init; }
        public required CurrentCompanyContext Company { get; init; }
        public required EnterpriseSalesService Sales { get; init; }
        public required Mock<ICurrentUserService> User { get; init; }
        public required Mock<IGlPostingService> Gl { get; init; }
    }

    private static async Task<Harness> CreateAsync(decimal creditLimit = 100_000m, decimal balance = 0m)
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        var db = TestDbContextFactory.Create();

        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        db.Branches.Add(new Branch { Id = 1, CompanyId = 1, Code = "HO", Name = "HO", IsDefault = true, IsActive = true });
        db.Warehouses.Add(new Warehouse
        {
            Id = 1, Name = "Main", CompanyId = 1, BranchId = 1, CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        });
        db.Categories.Add(new Category { Id = 1, Name = "Cat", CreatedAt = DateTime.UtcNow, CreatedBy = "t" });
        db.Brands.Add(new Brand { Id = 1, Name = "Brand", CreatedAt = DateTime.UtcNow, CreatedBy = "t" });
        db.Products.Add(new Product
        {
            Id = 1,
            Name = "Brake Pad",
            Sku = "BP-1",
            CategoryId = 1,
            BrandId = 1,
            Unit = "PCS",
            PurchasePrice = 100,
            CostPrice = 100,
            SalePrice = 250,
            TaxRatePercent = 0,
            IsActive = true,
            CompanyId = 1,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "t"
        });
        db.InventoryItems.Add(new InventoryItem
        {
            ProductId = 1,
            WarehouseId = 1,
            QuantityOnHand = 50,
            AverageCost = 100,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "t"
        });
        db.Customers.Add(new Customer
        {
            Id = 1,
            Name = "Dealer Co",
            CreditLimit = creditLimit,
            Balance = balance,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "t"
        });

        foreach (var doc in new[] { "QT", "SO", "DN", "INV", "JV" })
        {
            db.NumberSequences.Add(new NumberSequence
            {
                CompanyId = 1,
                DocumentType = doc,
                Prefix = doc + "-",
                NextValue = 1,
                Padding = 4
            });
        }

        db.PriceLists.Add(new PriceList
        {
            CompanyId = 1,
            Name = "Dealer List",
            CurrencyCode = "PKR",
            ValidFrom = DateTime.UtcNow.Date.AddDays(-1),
            IsDefault = true,
            Items =
            {
                new PriceListItem { CompanyId = 1, ProductId = 1, MinQuantity = 1, UnitPrice = 200 }
            }
        });

        await db.SaveChangesAsync();

        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.HasPermission(It.IsAny<string>())).Returns(true);

        var gl = new Mock<IGlPostingService>();
        gl.Setup(g => g.PostDocumentAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<int?>(), It.IsAny<IReadOnlyList<GlPostingLineRequest>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GlJournalDraftDto>.Success(
                new GlJournalDraftDto(1, "JV-0001", JournalStatus.Posted, 100, 100)));

        var sales = new EnterpriseSalesService(new EnterpriseDbAdapter(db), company, gl.Object, user.Object, Mock.Of<ISalesCommissionService>(), OpsSlaTestDoubles.NoOp);
        return new Harness { Db = db, Company = company, Sales = sales, User = user, Gl = gl };
    }

    [Fact]
    public async Task ConvertQuotation_CreatesSo_AndBlocksWhenCreditExceeded()
    {
        var ok = await CreateAsync(creditLimit: 10_000, balance: 0);
        var quote = await ok.Sales.CreateQuotationAsync(new CreateQuotationRequest(
            1, DateTime.UtcNow, null, null,
            [new CreateQuotationLineRequest(1, 2, 0)]));
        quote.Succeeded.Should().BeTrue();
        quote.Data!.Lines[0].UnitPrice.Should().Be(200);
        quote.Data.Lines[0].PriceSource.Should().Be("PriceList");
        quote.Data.Lines[0].PriceListName.Should().Be("Dealer List");

        var converted = await ok.Sales.ConvertQuotationToSalesOrderAsync(quote.Data.Id);
        converted.Succeeded.Should().BeTrue();
        converted.Data!.OrderNumber.Should().StartWith("SO-");
        converted.Data.QuotationNumber.Should().StartWith("QT-");

        var blocked = await CreateAsync(creditLimit: 100, balance: 50);
        var big = await blocked.Sales.CreateQuotationAsync(new CreateQuotationRequest(
            1, DateTime.UtcNow, null, null,
            [new CreateQuotationLineRequest(1, 10, 200)]));
        big.Succeeded.Should().BeTrue();
        var fail = await blocked.Sales.ConvertQuotationToSalesOrderAsync(big.Data!.Id);
        fail.Succeeded.Should().BeFalse();
        fail.Error.Should().Contain("Credit limit exceeded");
    }

    [Fact]
    public async Task HappyPath_Quote_So_Delivery_Ship_Invoice_EnforcesChain()
    {
        var h = await CreateAsync();
        var quote = await h.Sales.CreateQuotationAsync(new CreateQuotationRequest(
            1, DateTime.UtcNow, null, null,
            [new CreateQuotationLineRequest(1, 3, 0)]));
        quote.Succeeded.Should().BeTrue();

        var so = await h.Sales.ConvertQuotationToSalesOrderAsync(quote.Data!.Id);
        so.Succeeded.Should().BeTrue();

        var again = await h.Sales.ConvertQuotationToSalesOrderAsync(quote.Data.Id);
        again.Succeeded.Should().BeFalse();

        var dn = await h.Sales.CreateDeliveryFromSalesOrderAsync(
            so.Data!.SalesOrderId,
            new CreateDeliveryFromSalesOrderRequest(1, DateTime.UtcNow));
        dn.Succeeded.Should().BeTrue();
        dn.Data!.SalesOrderNumber.Should().Be(so.Data.OrderNumber);
        dn.Data.Lines.Should().HaveCount(1);
        dn.Data.Lines[0].QuantityShipped.Should().Be(3);

        var pick = await h.Sales.ConfirmDeliveryPickAsync(dn.Data.Id);
        pick.Succeeded.Should().BeTrue();

        var ship = await h.Sales.ShipDeliveryAsync(dn.Data.Id);
        ship.Succeeded.Should().BeTrue();
        ship.Data!.Status.Should().Be(DeliveryStatus.Shipped);
        h.Db.InventoryItems.First().QuantityOnHand.Should().Be(47);

        var inv = await h.Sales.CreateInvoiceFromDeliveryAsync(dn.Data.Id);
        inv.Succeeded.Should().BeTrue();
        inv.Data!.InvoiceNumber.Should().StartWith("INV-");
        inv.Data.OrderNumber.Should().Be(so.Data.OrderNumber);
        inv.Data.DeliveryNumber.Should().Be(dn.Data.DeliveryNumber);

        var order = h.Db.SalesOrders.First(o => o.Id == so.Data.SalesOrderId);
        order.Status.Should().Be(SalesOrderStatus.Invoiced);
        h.Db.Customers.First(c => c.Id == 1).Balance.Should().Be(order.GrandTotal);

        var dup = await h.Sales.CreateInvoiceFromSalesOrderAsync(so.Data.SalesOrderId);
        dup.Succeeded.Should().BeFalse();

        var chain = await h.Sales.GetWholesaleSalesOrdersAsync();
        var row = chain.Items.Single(o => o.Id == so.Data.SalesOrderId);
        row.QuotationNumber.Should().Be(quote.Data.QuotationNumber);
        row.DeliveryNumber.Should().Be(dn.Data.DeliveryNumber);
        row.InvoiceNumber.Should().Be(inv.Data!.InvoiceNumber);
    }

    [Fact]
    public async Task CreateInvoiceFromSalesOrder_BlocksWhenCreditExceeded()
    {
        var h = await CreateAsync(creditLimit: 100, balance: 90);
        var quote = await h.Sales.CreateQuotationAsync(new CreateQuotationRequest(
            1, DateTime.UtcNow, null, null,
            [new CreateQuotationLineRequest(1, 1, 50)]));
        // convert with remaining credit 10 — wait, grand total 50 exceeds available 10
        // so convert itself should fail; use higher limit for convert then lower for invoice path

        h = await CreateAsync(creditLimit: 10_000, balance: 0);
        quote = await h.Sales.CreateQuotationAsync(new CreateQuotationRequest(
            1, DateTime.UtcNow, null, null,
            [new CreateQuotationLineRequest(1, 1, 500)]));
        var so = await h.Sales.ConvertQuotationToSalesOrderAsync(quote.Data!.Id);
        so.Succeeded.Should().BeTrue();

        // exhaust credit after SO confirmed
        var cust = h.Db.Customers.First(c => c.Id == 1);
        cust.CreditLimit = 100;
        cust.Balance = 50;
        await h.Db.SaveChangesAsync();

        var inv = await h.Sales.CreateInvoiceFromSalesOrderAsync(so.Data!.SalesOrderId, 1);
        inv.Succeeded.Should().BeFalse();
        inv.Error.Should().Contain("Credit limit exceeded");
    }

    [Fact]
    public async Task PriceOverride_RequiresPermission()
    {
        var h = await CreateAsync();
        h.User.Setup(u => u.HasPermission(Permissions.SalesPriceOverride)).Returns(false);
        h.User.Setup(u => u.HasPermission(Permissions.PosPriceOverride)).Returns(false);

        var fail = await h.Sales.CreateQuotationAsync(new CreateQuotationRequest(
            1, DateTime.UtcNow, null, null,
            [new CreateQuotationLineRequest(1, 1, 999)]));
        fail.Succeeded.Should().BeFalse();
        fail.Error.Should().Contain("Price override");

        h.User.Setup(u => u.HasPermission(Permissions.SalesPriceOverride)).Returns(true);
        var ok = await h.Sales.CreateQuotationAsync(new CreateQuotationRequest(
            1, DateTime.UtcNow, null, null,
            [new CreateQuotationLineRequest(1, 1, 999)]));
        ok.Succeeded.Should().BeTrue();
        ok.Data!.Lines[0].PriceSource.Should().Be("Override");
    }

    [Fact]
    public async Task CreateDeliveryFromSalesOrder_RejectsDuplicate()
    {
        var h = await CreateAsync();
        var quote = await h.Sales.CreateQuotationAsync(new CreateQuotationRequest(
            1, DateTime.UtcNow, null, null,
            [new CreateQuotationLineRequest(1, 1, 200)]));
        var so = await h.Sales.ConvertQuotationToSalesOrderAsync(quote.Data!.Id);
        so.Succeeded.Should().BeTrue();

        var first = await h.Sales.CreateDeliveryFromSalesOrderAsync(
            so.Data!.SalesOrderId, new CreateDeliveryFromSalesOrderRequest(1, null));
        first.Succeeded.Should().BeTrue();

        var dup = await h.Sales.CreateDeliveryFromSalesOrderAsync(
            so.Data.SalesOrderId, new CreateDeliveryFromSalesOrderRequest(1, null));
        dup.Succeeded.Should().BeFalse();
        dup.Error.Should().Contain("already exists");
    }

    [Fact]
    public async Task CreateInvoiceFromSalesOrder_DirectPath_IssuesStock()
    {
        var h = await CreateAsync();
        var quote = await h.Sales.CreateQuotationAsync(new CreateQuotationRequest(
            1, DateTime.UtcNow, null, null,
            [new CreateQuotationLineRequest(1, 2, 200)]));
        var so = await h.Sales.ConvertQuotationToSalesOrderAsync(quote.Data!.Id);
        so.Succeeded.Should().BeTrue();

        var inv = await h.Sales.CreateInvoiceFromSalesOrderAsync(so.Data!.SalesOrderId, 1);
        inv.Succeeded.Should().BeTrue();

        await h.Db.Entry(h.Db.InventoryItems.First()).ReloadAsync();
        h.Db.InventoryItems.First().QuantityOnHand.Should().Be(48);
        h.Db.SalesOrders.First(o => o.Id == so.Data.SalesOrderId).Status.Should().Be(SalesOrderStatus.Invoiced);
    }
}
