using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Interfaces;
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

public class Phase12Stage0Tests
{
    [Fact]
    public async Task PosSearch_ExactSku_UsesHotPathWithoutContains()
    {
        await using var db = TestDbContextFactory.Create();
        db.Products.AddRange(
            new Product
            {
                Name = "Oil Filter A", Sku = "OF-100", Barcode = "6281001", OemNumber = "OEM-OF-100",
                CategoryId = 1, BrandId = 1, Unit = "PCS", PurchasePrice = 10, SalePrice = 20,
                TaxRatePercent = 18, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "t"
            },
            new Product
            {
                Name = "Oil Filter Soft", Sku = "OF-SOFT", Barcode = "6281999", OemNumber = "OTHER",
                CategoryId = 1, BrandId = 1, Unit = "PCS", PurchasePrice = 10, SalePrice = 22,
                TaxRatePercent = 18, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "t"
            });
        await db.SaveChangesAsync();

        var service = CreatePos(db);
        var hits = await service.GetPosProductsAsync("OF-100");
        hits.Should().ContainSingle(p => p.Sku == "OF-100");
    }

    [Fact]
    public async Task PosSearch_ExactOem_ReturnsMatch()
    {
        await using var db = TestDbContextFactory.Create();
        db.Products.Add(new Product
        {
            Name = "Brake Pad", Sku = "BP-1", OemNumber = "OEM-BP-99",
            CategoryId = 1, BrandId = 1, Unit = "PCS", PurchasePrice = 50, SalePrice = 90,
            TaxRatePercent = 18, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        });
        await db.SaveChangesAsync();

        var service = CreatePos(db);
        var hits = await service.GetPosProductsAsync("OEM-BP-99");
        hits.Should().ContainSingle();
        hits[0].OemNumber.Should().Be("OEM-BP-99");
    }

    [Fact]
    public async Task ReceiptHtml_IncludesFbrIrn_WhenPosted()
    {
        await using var db = TestDbContextFactory.Create();
        var inv = new SalesInvoice
        {
            InvoiceNumber = "INV-1",
            InvoiceDate = DateTime.UtcNow,
            GrandTotal = 100,
            TaxAmount = 15,
            SubTotal = 85,
            BuyerName = "Walk-in",
            PaymentStatus = PaymentStatus.Paid,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "t"
        };
        db.SalesInvoices.Add(inv);
        await db.SaveChangesAsync();
        db.SalesInvoiceLines.Add(new SalesInvoiceLine
        {
            SalesInvoiceId = inv.Id,
            ProductId = 1,
            ProductName = "Part",
            Quantity = 1,
            UnitPrice = 85,
            LineTotal = 85,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "t"
        });
        db.FbrSubmissions.Add(new FbrSubmission
        {
            SalesInvoiceId = inv.Id,
            FbrInvoiceNumber = "FBR-IRN-123",
            Status = FbrSubmissionStatus.Success,
            SubmittedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "t"
        });
        await db.SaveChangesAsync();

        var html = await CreatePos(db).GetReceiptHtmlAsync(inv.Id);
        html.Should().Contain("FBR IRN");
        html.Should().Contain("FBR-IRN-123");
        html.Should().Contain("FBR-IRN:FBR-IRN-123");
    }

    [Fact]
    public async Task FbrMetrics_ComputesSuccessRate_AndNeedsRetry()
    {
        await using var db = TestDbContextFactory.Create();
        var company = new CurrentCompanyContext();
        company.Set(1);

        // Seed invoices for FK if needed — FbrSubmission only needs SalesInvoiceId
        for (var i = 0; i < 4; i++)
        {
            var inv = new SalesInvoice
            {
                InvoiceNumber = $"INV-M{i}",
                InvoiceDate = DateTime.UtcNow,
                GrandTotal = 10,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "t"
            };
            db.SalesInvoices.Add(inv);
            await db.SaveChangesAsync();
            db.FbrSubmissions.Add(new FbrSubmission
            {
                SalesInvoiceId = inv.Id,
                Status = i switch
                {
                    0 => FbrSubmissionStatus.Success,
                    1 => FbrSubmissionStatus.Stub,
                    2 => FbrSubmissionStatus.Failed,
                    _ => FbrSubmissionStatus.Pending
                },
                FbrInvoiceNumber = i < 2 ? $"N{i}" : null,
                SubmittedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "t"
            });
        }
        await db.SaveChangesAsync();

        var sales = new EnterpriseSalesService(
            new EnterpriseDbAdapter(db),
            company,
            Mock.Of<IGlPostingService>(),
            Mock.Of<ICurrentUserService>());
        var m = await sales.GetFbrMetricsAsync();
        m.SuccessCount.Should().Be(1);
        m.StubCount.Should().Be(1);
        m.FailedCount.Should().Be(1);
        m.PendingCount.Should().Be(1);
        m.TotalCount.Should().Be(4);
        m.SuccessRatePercent.Should().Be(50m);
        m.NeedsRetryCount.Should().Be(2);
    }

    [Fact]
    public async Task Onboarding_Complete_CreatesFirstTill_WhenMissing()
    {
        await using var db = TestDbContextFactory.Create();
        db.Companies.Add(new Company { Code = "C1", Name = "Co", CurrencyCode = "PKR", IsActive = true, CreatedBy = "t" });
        await db.SaveChangesAsync();
        db.Branches.Add(new Branch
        {
            CompanyId = 1, Code = "HO", Name = "Head", IsDefault = true, IsActive = true, CreatedBy = "t"
        });
        db.Warehouses.Add(new Warehouse { Name = "Main", BranchId = 1, CreatedAt = DateTime.UtcNow, CreatedBy = "t" });
        db.CompanySettings.Add(new CompanySettings { CompanyName = "Old", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var user = new CurrentUserService();
        user.SetUser(new AppUser { Id = 1, Username = "admin", DisplayName = "Admin" },
            [Application.Constants.Permissions.SettingsManage]);
        var company = new CurrentCompanyContext();
        company.Set(1, 1);

        var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var appConfig = new AppConfigService(
            new Repository<AppConfigEntry>(db),
            new Repository<CompanySettings>(db),
            new UnitOfWork(db),
            cache);

        var svc = new OnboardingService(
            new Repository<CompanySettings>(db),
            new Repository<Warehouse>(db),
            new Repository<FiscalYear>(db),
            new Repository<AccountMapping>(db),
            new Repository<AppUser>(db),
            new Repository<Till>(db),
            new Repository<Branch>(db),
            new UnitOfWork(db),
            user,
            PosCheckoutServiceTests.CreateFeatureGate(),
            appConfig,
            company);

        (await db.Tills.CountAsync()).Should().Be(0);
        var result = await svc.CompleteAsync(new CompleteOnboardingDto(
            "Pilot Parts", "0786909", null, "Karachi", "Shop", null, null, "1001", 18m, true, "Fifo", "auto-parts"));
        result.Succeeded.Should().BeTrue();
        var till = await db.Tills.SingleAsync();
        till.Code.Should().Be("TILL-01");
        till.BranchId.Should().Be(1);
    }

    private static PosCheckoutService CreatePos(ApplicationDbContext db)
    {
        var inventoryMock = new Mock<IInventoryService>();
        var fbrMock = new Mock<IFbrService>();
        var gl = new Mock<IGlPostingService>();
        var fbrOutbox = new Mock<IFbrOutboxService>();
        var salesEnt = new Mock<IEnterpriseSalesService>();
        return new PosCheckoutService(
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
            new Application.Validators.PosCheckoutValidator(),
            gl.Object,
            fbrOutbox.Object,
            new CurrentCompanyContext(),
            new CurrentUserService(),
            salesEnt.Object,
            new AtpService(new EnterpriseDbAdapter(db)),
            PosCheckoutServiceTests.CreateFeatureGate());
    }
}
