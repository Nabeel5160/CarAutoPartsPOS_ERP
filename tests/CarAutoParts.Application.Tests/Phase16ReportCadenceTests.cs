using CarAutoParts.Application.DTOs.Reports;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using CarAutoParts.Infrastructure.Repositories;
using CarAutoParts.Infrastructure.Services;
using ClosedXML.Excel;
using FluentAssertions;
using Moq;
using Xunit;

namespace CarAutoParts.Application.Tests;

public class Phase16ReportCadenceTests
{
    [Fact]
    public void ReportBranchScope_denies_disallowed_branch()
    {
        var company = new CurrentCompanyContext();
        company.Set(1, 1, [1]);
        ReportBranchScope.IsDenied(company, 99).Should().BeTrue();
        ReportBranchScope.IsDenied(company, 1).Should().BeFalse();
        ReportBranchScope.IsDenied(company, null).Should().BeFalse();
    }

    [Fact]
    public void ReportBranchScope_warehouse_ids_match_excel_acl()
    {
        using var db = TestDbContextFactory.Create();
        SeedTwoBranches(db);
        db.SaveChanges();

        var company = new CurrentCompanyContext();
        company.Set(1, 1, [1]);

        var branch1 = ReportBranchScope.AllowedWarehouseIds(db.Warehouses, company, branchId: 1);
        branch1.Should().BeEquivalentTo(new[] { 1 });

        var aclDefault = ReportBranchScope.AllowedWarehouseIds(db.Warehouses, company, branchId: null);
        aclDefault.Should().BeEquivalentTo(new[] { 1 });

        company.Set(1, null, []);
        ReportBranchScope.AllowedWarehouseIds(db.Warehouses, company, branchId: null).Should().BeNull();
    }

    [Fact]
    public async Task PdfSales_rejects_disallowed_branch_before_render()
    {
        await using var db = TestDbContextFactory.Create();
        SeedTwoBranches(db);
        await db.SaveChangesAsync();

        var company = new CurrentCompanyContext();
        company.Set(1, 1, [1]);
        var pdf = new PdfReportService(db, company);

        var result = await pdf.GenerateSalesReportAsync(
            DateTime.UtcNow.Date, DateTime.UtcNow.Date, branchId: 99);
        result.Should().BeNull();
    }

    [Fact]
    public async Task SalesExport_and_scope_helper_agree_on_branch_filter()
    {
        await using var db = TestDbContextFactory.Create();
        SeedTwoBranches(db);
        db.SalesInvoices.Add(new SalesInvoice
        {
            InvoiceNumber = "A", InvoiceDate = DateTime.UtcNow.Date, WarehouseId = 1,
            SubTotal = 50, GrandTotal = 50, PaymentStatus = PaymentStatus.Paid, CreatedAt = DateTime.UtcNow
        });
        db.SalesInvoices.Add(new SalesInvoice
        {
            InvoiceNumber = "B", InvoiceDate = DateTime.UtcNow.Date, WarehouseId = 2,
            SubTotal = 80, GrandTotal = 80, PaymentStatus = PaymentStatus.Paid, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var company = new CurrentCompanyContext();
        company.Set(1, 1, [1]);
        var svc = CreateReportService(db, company);

        var whIds = ReportBranchScope.AllowedWarehouseIds(db.Warehouses, company, 1);
        whIds.Should().Contain(1).And.NotContain(2);

        var daily = await svc.GetDailySalesSummaryAsync(
            DateTime.UtcNow.Date.AddDays(-1), DateTime.UtcNow.Date.AddDays(1), branchId: 1);
        daily.Data!.InvoiceCount.Should().Be(1);
        daily.Data.SalesTotal.Should().Be(50);
    }

    [Fact]
    public async Task InventoryExport_scopes_to_allowed_branch_warehouses()
    {
        await using var db = TestDbContextFactory.Create();
        SeedTwoBranches(db);
        db.Categories.Add(new Category { Id = 1, Name = "Cat", CreatedAt = DateTime.UtcNow });
        db.Brands.Add(new Brand { Id = 1, Name = "Brand", CreatedAt = DateTime.UtcNow });
        var product = new Product
        {
            Sku = "SKU-1", Name = "Part", SalePrice = 10, CostPrice = 5, PurchasePrice = 5,
            CategoryId = 1, BrandId = 1, Unit = "PCS", IsActive = true, CompanyId = 1,
            CreatedAt = DateTime.UtcNow
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        db.InventoryItems.Add(new InventoryItem
        {
            ProductId = product.Id, WarehouseId = 1, QuantityOnHand = 3, AverageCost = 5,
            CreatedAt = DateTime.UtcNow
        });
        db.InventoryItems.Add(new InventoryItem
        {
            ProductId = product.Id, WarehouseId = 2, QuantityOnHand = 9, AverageCost = 5,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var company = new CurrentCompanyContext();
        company.Set(1, 1, [1]);
        var excelSvc = CreateReportService(db, company);

        ReportBranchScope.AllowedWarehouseIds(db.Warehouses, company, 1)
            .Should().BeEquivalentTo(new[] { 1 });

        var xlsx = await excelSvc.ExportInventoryReportAsync("xlsx", branchId: 1);
        using var wb = new XLWorkbook(new MemoryStream(xlsx));
        var sheet = wb.Worksheet(1);
        sheet.LastRowUsed()!.RowNumber().Should().Be(2);
        sheet.Cell(2, 4).GetValue<decimal>().Should().Be(3);
    }

    [Fact]
    public void ZArchiveExcel_exports_closed_shift_rows()
    {
        using var db = TestDbContextFactory.Create();
        var company = new CurrentCompanyContext();
        company.Set(1, 1, [1]);
        var svc = CreateReportService(db, company);

        var rows = new List<ClosedShiftListItemDto>
        {
            new(1, "S-1", "cashier", 1, 10, "TILL-01",
                DateTime.UtcNow.Date.AddHours(8), DateTime.UtcNow.Date.AddHours(17),
                1000, 2500, 2400, 100, "Closed")
        };

        var bytes = svc.ExportClosedShiftsArchive(rows);
        bytes.Length.Should().BeGreaterThan(100);

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var sheet = wb.Worksheet("Z Archive");
        sheet.Cell(2, 1).GetString().Should().Be("S-1");
        sheet.Cell(2, 2).GetString().Should().Be("cashier");
        sheet.Cell(2, 10).GetValue<decimal>().Should().Be(100);
    }

    private static void SeedTwoBranches(Infrastructure.Data.ApplicationDbContext db)
    {
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "T", CurrencyCode = "PKR", IsActive = true });
        db.Branches.Add(new Branch { Id = 1, CompanyId = 1, Code = "HQ", Name = "HQ", IsDefault = true, IsActive = true });
        db.Branches.Add(new Branch { Id = 2, CompanyId = 1, Code = "B2", Name = "B2", IsActive = true });
        db.Warehouses.Add(new Warehouse { Id = 1, Name = "Main", BranchId = 1, IsDefault = true, CompanyId = 1 });
        db.Warehouses.Add(new Warehouse { Id = 2, Name = "Other", BranchId = 2, IsDefault = false, CompanyId = 1 });
    }

    private static ReportService CreateReportService(
        Infrastructure.Data.ApplicationDbContext db,
        CurrentCompanyContext company)
    {
        var analytics = new Mock<IAnalyticsService>();
        analytics.Setup(a => a.GetAnalyticsAsync(
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DTOs.Analytics.AnalyticsDto([], [], [], 0, 0, [], [], 0, 0, null));

        var financial = new Mock<IFinancialReportService>();

        return new ReportService(
            new Repository<InventoryItem>(db),
            new Repository<SalesInvoice>(db),
            new Repository<PurchaseOrder>(db),
            new Repository<Warehouse>(db),
            new Repository<SalesReturn>(db),
            new Repository<StockMovement>(db),
            new Repository<StockBatch>(db),
            new Repository<GoodsReceiptNote>(db),
            new Repository<FbrSubmission>(db),
            new Repository<Product>(db),
            company,
            PosCheckoutServiceTests.CreateFeatureGate(),
            analytics.Object,
            financial.Object);
    }
}
