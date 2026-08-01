using CarAutoParts.Application.Config;
using CarAutoParts.Application.DTOs.Fbr;
using CarAutoParts.Application.DTOs.Pos;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Services;
using CarAutoParts.Application.Validators;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Infrastructure.Data;
using CarAutoParts.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace CarAutoParts.Application.Tests;

public class Phase11ConfigurabilityTests
{
    [Fact]
    public async Task Preset_defaults_resolve_for_auto_parts()
    {
        await using var db = TestDbContextFactory.Create();
        db.CompanySettings.Add(new CompanySettings { CompanyName = "CAP", VerticalKey = "auto-parts", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var svc = CreateConfig(db);
        var cfg = await svc.GetAsync();
        cfg.VerticalKey.Should().Be("auto-parts");
        cfg.Modules[ConfigKeys.ModSalesFbr].Should().BeTrue();
        cfg.Behaviors[ConfigKeys.BehFbrEnabled].Should().Be("true");
        cfg.Fields[ConfigKeys.FieldProductOem].Visible.Should().BeTrue();
        cfg.Branding.AppName.Should().Be("CAP");
    }

    [Fact]
    public async Task Db_override_wins_over_preset()
    {
        await using var db = TestDbContextFactory.Create();
        db.CompanySettings.Add(new CompanySettings { CompanyName = "Shop", VerticalKey = "auto-parts", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var svc = CreateConfig(db);
        var result = await svc.UpdateAsync(new AppConfigUpdateRequest(
            null, false,
            Modules: new Dictionary<string, bool> { [ConfigKeys.ModSalesFbr] = false },
            Fields: null, Behaviors: null, Brand: null, Labels: null));
        result.Succeeded.Should().BeTrue();

        var cfg = await svc.GetAsync();
        cfg.Modules[ConfigKeys.ModSalesFbr].Should().BeFalse();
    }

    [Fact]
    public async Task General_retail_checkout_creates_no_FbrSubmission()
    {
        await using var db = TestDbContextFactory.Create();
        db.CompanySettings.Add(new CompanySettings
        {
            CompanyName = "Retail", VerticalKey = "general-retail", DefaultTaxRate = 0, CreatedAt = DateTime.UtcNow
        });
        db.Warehouses.Add(new Warehouse { Name = "Main", CreatedAt = DateTime.UtcNow });
        var product = new Product
        {
            Name = "Pen", Sku = "PEN-1", CategoryId = 1, BrandId = 1, Unit = "PCS",
            PurchasePrice = 5, SalePrice = 10, TaxRatePercent = 0, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        db.InventoryItems.Add(new InventoryItem
        {
            ProductId = product.Id, WarehouseId = 1, QuantityOnHand = 50, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var inventoryMock = new Mock<IInventoryService>();
        inventoryMock.Setup(i => i.DeductStockAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Application.Common.Result<decimal>.Success(5m));
        var fbrMock = new Mock<IFbrService>();
        var fbrOutbox = new Mock<IFbrOutboxService>();
        var gl = new Mock<IGlPostingService>();
        var salesEnt = new Mock<IEnterpriseSalesService>();
        salesEnt.Setup(s => s.GetPriceForProductAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int pid, int? _, decimal __, CancellationToken ___) =>
                Application.Common.Result<PriceLookupResultDto>.Success(new PriceLookupResultDto(pid, 10, null, null)));
        salesEnt.Setup(s => s.CheckCreditLimitAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Application.Common.Result<CreditCheckResultDto>.Success(
                new CreditCheckResultDto(true, 10000, 0, 10000, null)));

        var gate = PosCheckoutServiceTests.CreateFeatureGate(fbr: false, tax: true, fitment: false, supersession: false);
        var pos = new PosCheckoutService(
            new Repository<Product>(db),
            new Repository<SalesInvoice>(db),
            new Repository<FbrSubmission>(db),
            new Repository<CompanySettings>(db),
            new Repository<Payment>(db),
            new Repository<Customer>(db),
            new Repository<ProductKit>(db),
            new Repository<CashierShift>(db),
            new Repository<HeldSale>(db),
            new Repository<ProductSupersession>(db),
            new Repository<Warehouse>(db),
            inventoryMock.Object,
            fbrMock.Object,
            new UnitOfWork(db),
            new PosCheckoutValidator(),
            gl.Object,
            fbrOutbox.Object,
            new CurrentCompanyContext(),
            new CurrentUserService(),
            salesEnt.Object,
            new AtpService(new EnterpriseDbAdapter(db)),
            gate);

        var result = await pos.CheckoutAsync(new PosCheckoutDto(
            1, null, null, "Cash", 0, null, null, null,
            [new PosCheckoutLineDto(product.Id, 1, null, 0)]));

        result.SalesInvoiceId.Should().BeGreaterThan(0);
        (await db.FbrSubmissions.CountAsync()).Should().Be(0);
        fbrMock.Verify(f => f.PostInvoiceAsync(It.IsAny<FbrInvoiceRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Public_config_contains_no_secrets()
    {
        await using var db = TestDbContextFactory.Create();
        db.CompanySettings.Add(new CompanySettings
        {
            CompanyName = "CAP",
            VerticalKey = "auto-parts",
            FbrBearerToken = "SECRET-TOKEN",
            DatabaseConnectionString = "Server=secret",
            Ntn = "123",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var pub = await CreateConfig(db).GetPublicAsync();
        var json = System.Text.Json.JsonSerializer.Serialize(pub);
        json.Should().NotContain("SECRET-TOKEN");
        json.Should().NotContain("Server=secret");
        json.Should().NotContain("123"); // NTN must not leak on public endpoint
        pub.Branding.AppName.Should().Be("CAP");
    }

    [Fact]
    public async Task Unknown_config_key_rejected()
    {
        await using var db = TestDbContextFactory.Create();
        db.CompanySettings.Add(new CompanySettings { CompanyName = "CAP", VerticalKey = "auto-parts", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var result = await CreateConfig(db).UpdateAsync(new AppConfigUpdateRequest(
            null, false,
            Modules: new Dictionary<string, bool> { ["not.a.real.module"] = true },
            null, null, null, null));
        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("Unknown module");
    }

    [Fact]
    public async Task Apply_general_retail_preset_disables_fbr_and_fitment()
    {
        await using var db = TestDbContextFactory.Create();
        db.CompanySettings.Add(new CompanySettings { CompanyName = "Shop", VerticalKey = "auto-parts", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var svc = CreateConfig(db);
        var result = await svc.UpdateAsync(new AppConfigUpdateRequest("general-retail", true, null, null, null, null, null));
        result.Succeeded.Should().BeTrue();
        result.Data!.Modules[ConfigKeys.ModSalesFbr].Should().BeFalse();
        result.Data.Behaviors[ConfigKeys.BehFbrEnabled].Should().Be("false");
        result.Data.Fields[ConfigKeys.FieldProductOem].Visible.Should().BeFalse();
        result.Data.VerticalKey.Should().Be("general-retail");
    }

    private static AppConfigService CreateConfig(ApplicationDbContext db) =>
        new(
            new Repository<AppConfigEntry>(db),
            new Repository<CompanySettings>(db),
            new UnitOfWork(db),
            new MemoryCache(new MemoryCacheOptions()));
}
