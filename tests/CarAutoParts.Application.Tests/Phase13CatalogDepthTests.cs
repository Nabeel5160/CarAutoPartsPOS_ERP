using System.Text;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Infrastructure.Data;
using CarAutoParts.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CarAutoParts.Application.Tests;

public class Phase13CatalogDepthTests
{
    [Fact]
    public void CatalogSearchNormalizer_Trims_And_HandlesLeadingZeros()
    {
        var c = CatalogSearchNormalizer.BuildExactCandidates("  001234  ");
        c.Should().Contain("001234");
        c.Should().Contain("1234");
        c.Should().Contain("00001234");
        CatalogSearchNormalizer.NormalizePaste("  ABC  ").Should().Be("ABC");
    }

    [Fact]
    public async Task PosSearch_LeadingZeroBarcode_MatchesExact()
    {
        await using var db = TestDbContextFactory.Create();
        db.Products.Add(new Product
        {
            Name = "Pad", Sku = "PAD-1", Barcode = "0006281001",
            CategoryId = 1, BrandId = 1, Unit = "PCS", PurchasePrice = 10, SalePrice = 20,
            TaxRatePercent = 18, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        });
        await db.SaveChangesAsync();

        var hits = await CreatePos(db).GetPosProductsAsync("6281001");
        hits.Should().ContainSingle(p => p.Sku == "PAD-1");
        hits[0].IsExactMatch.Should().BeTrue();
    }

    [Fact]
    public async Task PosSearch_UniqueExactBarcode_ReturnsSingleExact()
    {
        await using var db = TestDbContextFactory.Create();
        db.Products.AddRange(
            new Product
            {
                Name = "Exact", Sku = "EX-1", Barcode = "8901234567004",
                CategoryId = 1, BrandId = 1, Unit = "PCS", PurchasePrice = 10, SalePrice = 20,
                IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "t"
            },
            new Product
            {
                Name = "Soft", Sku = "SOFT-890", Barcode = "999",
                CategoryId = 1, BrandId = 1, Unit = "PCS", PurchasePrice = 10, SalePrice = 22,
                IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "t"
            });
        await db.SaveChangesAsync();

        var hits = await CreatePos(db).GetPosProductsAsync("8901234567004");
        hits.Should().ContainSingle();
        hits[0].IsExactMatch.Should().BeTrue();
        hits[0].Sku.Should().Be("EX-1");
    }

    [Fact]
    public async Task PosSearch_FitmentMakeModelYear_Filters()
    {
        await using var db = TestDbContextFactory.Create();
        var a = new Product
        {
            Name = "Filter A", Sku = "FA", CategoryId = 1, BrandId = 1, Unit = "PCS",
            PurchasePrice = 1, SalePrice = 2, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        };
        var b = new Product
        {
            Name = "Filter B", Sku = "FB", CategoryId = 1, BrandId = 1, Unit = "PCS",
            PurchasePrice = 1, SalePrice = 2, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        };
        db.Products.AddRange(a, b);
        await db.SaveChangesAsync();
        db.ProductVehicleCompatibilities.AddRange(
            new ProductVehicleCompatibility
            {
                ProductId = a.Id, Make = "Toyota", Model = "Corolla", YearFrom = 2015, YearTo = 2020,
                CreatedAt = DateTime.UtcNow, CreatedBy = "t"
            },
            new ProductVehicleCompatibility
            {
                ProductId = b.Id, Make = "Honda", Model = "Civic", YearFrom = 2018, YearTo = 2022,
                CreatedAt = DateTime.UtcNow, CreatedBy = "t"
            });
        await db.SaveChangesAsync();

        var hits = await CreatePos(db).GetPosProductsAsync(make: "Toyota", model: "Corolla", year: 2018);
        hits.Should().ContainSingle(p => p.Sku == "FA");
        hits[0].FitmentSummary.Should().Contain("Toyota");
    }

    [Fact]
    public async Task PosSearch_SupersessionDisplay_WhenEnabled()
    {
        await using var db = TestDbContextFactory.Create();
        var oldP = new Product
        {
            Name = "Old Pad", Sku = "OLD-1", OemNumber = "OEM-OLD",
            CategoryId = 1, BrandId = 1, Unit = "PCS", PurchasePrice = 1, SalePrice = 2,
            IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        };
        var newP = new Product
        {
            Name = "New Pad", Sku = "NEW-1", OemNumber = "OEM-NEW",
            CategoryId = 1, BrandId = 1, Unit = "PCS", PurchasePrice = 1, SalePrice = 3,
            IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        };
        db.Products.AddRange(oldP, newP);
        await db.SaveChangesAsync();
        db.ProductSupersessions.Add(new ProductSupersession
        {
            OldProductId = oldP.Id, NewProductId = newP.Id, EffectiveFrom = DateTime.UtcNow.Date,
            CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        });
        await db.SaveChangesAsync();

        var hits = await CreatePos(db).GetPosProductsAsync("NEW-1");
        hits.Should().ContainSingle();
        hits[0].SupersedesSkus.Should().Contain("OLD-1");

        var soft = await CreatePos(db, supersession: false).GetPosProductsAsync("NEW-1");
        soft[0].SupersedesSkus.Should().BeNull();
    }

    [Fact]
    public async Task PosSearch_FitmentDisabled_IgnoresMakeFilter()
    {
        await using var db = TestDbContextFactory.Create();
        db.Products.Add(new Product
        {
            Name = "Any", Sku = "ANY", CategoryId = 1, BrandId = 1, Unit = "PCS",
            PurchasePrice = 1, SalePrice = 2, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        });
        await db.SaveChangesAsync();

        var hits = await CreatePos(db, fitment: false).GetPosProductsAsync(make: "Toyota");
        hits.Should().Contain(p => p.Sku == "ANY");
    }

    [Fact]
    public async Task OemFitmentCsv_UpsertsWithoutWiping_AndReportsErrors()
    {
        await using var db = TestDbContextFactory.Create();
        db.Categories.Add(new Category { Name = "Cat", CreatedAt = DateTime.UtcNow, CreatedBy = "t" });
        db.Brands.Add(new Brand { Name = "Brand", CreatedAt = DateTime.UtcNow, CreatedBy = "t" });
        await db.SaveChangesAsync();
        db.Products.Add(new Product
        {
            Name = "Part", Sku = "SKU-1", OemNumber = "OLD-OEM",
            CategoryId = 1, BrandId = 1, Unit = "PCS", PurchasePrice = 1, SalePrice = 2,
            IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        });
        await db.SaveChangesAsync();

        var csv = """
            Sku,OemNumber,PartNumber,Make,Model,YearFrom,YearTo
            SKU-1,NEW-OEM,PN-1,Toyota,Corolla,2015,2019
            SKU-1,,,Toyota,Corolla,2015,2019
            MISSING,,,Toyota,Camry,2010,2012
            SKU-1,,,Honda,,2018,2019
            """;

        var svc = CreateProducts(db);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await svc.ImportOemFitmentCsvAsync(stream);
        result.Succeeded.Should().BeTrue();
        result.Data!.OemUpdated.Should().Be(1);
        result.Data.FitmentAdded.Should().Be(1);
        result.Data.ErrorCount.Should().BeGreaterThanOrEqualTo(2);
        result.Data.ErrorReportCsv.Should().Contain("MISSING");
        result.Data.ErrorReportCsv.Should().Contain("Make and Model");

        var product = await db.Products.Include(p => p.VehicleCompatibilities).SingleAsync(p => p.Sku == "SKU-1");
        product.OemNumber.Should().Be("NEW-OEM");
        product.PartNumber.Should().Be("PN-1");
        product.VehicleCompatibilities.Should().ContainSingle(v => v.Make == "Toyota" && v.Model == "Corolla");
        (await db.Products.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetFitmentOptions_ReturnsMakesWhenEnabled()
    {
        await using var db = TestDbContextFactory.Create();
        var p = new Product
        {
            Name = "P", Sku = "P1", CategoryId = 1, BrandId = 1, Unit = "PCS",
            PurchasePrice = 1, SalePrice = 2, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        };
        db.Products.Add(p);
        await db.SaveChangesAsync();
        db.ProductVehicleCompatibilities.Add(new ProductVehicleCompatibility
        {
            ProductId = p.Id, Make = "Suzuki", Model = "Mehran", YearFrom = 2010, YearTo = 2015,
            CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        });
        await db.SaveChangesAsync();

        var opts = await CreatePos(db).GetFitmentOptionsAsync();
        opts.Makes.Should().Contain("Suzuki");
        var models = await CreatePos(db).GetFitmentOptionsAsync("Suzuki");
        models.Models.Should().Contain("Mehran");
    }

    private static PosCheckoutService CreatePos(ApplicationDbContext db, bool fitment = true, bool supersession = true)
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
            PosCheckoutServiceTests.CreateFeatureGate(fitment: fitment, supersession: supersession));
    }

    private static ProductService CreateProducts(ApplicationDbContext db)
    {
        var mapper = new AutoMapper.MapperConfiguration(c =>
            c.AddProfile(new Application.Mapping.MappingProfile())).CreateMapper();
        return new ProductService(
            new Repository<Product>(db),
            new Repository<Category>(db),
            new Repository<Brand>(db),
            new Repository<ProductSupersession>(db),
            new UnitOfWork(db),
            mapper,
            new Application.Validators.ProductValidator());
    }
}
