using CarAutoParts.Application.DTOs.Analytics;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using CarAutoParts.Infrastructure.Repositories;
using FluentAssertions;
using Moq;
using Xunit;

namespace CarAutoParts.Application.Tests;

public class ClientReportsTests
{
    [Fact]
    public async Task DailySales_totals_invoices_tenders_and_returns()
    {
        await using var db = TestDbContextFactory.Create();
        SeedBranch(db, branchId: 1, warehouseId: 1);
        var invoice = new SalesInvoice
        {
            InvoiceNumber = "INV-1", InvoiceDate = DateTime.UtcNow.Date, WarehouseId = 1,
            SubTotal = 100, TaxAmount = 17, DiscountAmount = 0, GrandTotal = 117,
            PaymentStatus = PaymentStatus.Paid, CreatedAt = DateTime.UtcNow
        };
        invoice.Payments.Add(new Payment
        {
            Amount = 117, PaymentMethod = "Cash", PaymentDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow
        });
        db.SalesInvoices.Add(invoice);
        db.SalesReturns.Add(new SalesReturn
        {
            ReturnNumber = "SR-1", ReturnDate = DateTime.UtcNow.Date, GrandTotal = 20, TaxAmount = 0,
            Status = ReturnStatus.Completed, ReasonCode = "DEFECT", CreatedAt = DateTime.UtcNow,
            SalesInvoice = invoice
        });
        await db.SaveChangesAsync();

        var company = new CurrentCompanyContext();
        company.Set(1, 1, [1]);
        var svc = CreateReportService(db, company);

        var result = await svc.GetDailySalesSummaryAsync(DateTime.UtcNow.Date.AddDays(-1), DateTime.UtcNow.Date.AddDays(1), branchId: 1);
        result.Succeeded.Should().BeTrue();
        result.Data!.InvoiceCount.Should().Be(1);
        result.Data.SalesTotal.Should().Be(117);
        result.Data.ReturnsTotal.Should().Be(20);
        result.Data.NetSales.Should().Be(97);
        result.Data.Tenders.Should().Contain(t => t.Method == "Cash" && t.Amount == 117);
        result.Data.TaxEnabled.Should().BeTrue();
        result.Data.TaxAmount.Should().Be(17);
    }

    [Fact]
    public async Task DailySales_rejects_range_over_interactive_budget()
    {
        await using var db = TestDbContextFactory.Create();
        SeedBranch(db, branchId: 1, warehouseId: 1);
        await db.SaveChangesAsync();

        var company = new CurrentCompanyContext();
        company.Set(1, 1, [1]);
        var svc = CreateReportService(db, company);

        var from = DateTime.UtcNow.Date.AddDays(-120);
        var to = DateTime.UtcNow.Date;
        var result = await svc.GetDailySalesSummaryAsync(from, to, branchId: 1);
        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("93");
    }

    [Fact]
    public async Task DailySales_rejects_disallowed_branch()
    {
        await using var db = TestDbContextFactory.Create();
        SeedBranch(db, branchId: 1, warehouseId: 1);
        await db.SaveChangesAsync();

        var company = new CurrentCompanyContext();
        company.Set(1, 1, [1]);
        var svc = CreateReportService(db, company);

        var result = await svc.GetDailySalesSummaryAsync(DateTime.UtcNow.Date, DateTime.UtcNow.Date, branchId: 99);
        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("not allowed");
    }

    [Fact]
    public async Task SalesExport_filters_by_branch_acl()
    {
        await using var db = TestDbContextFactory.Create();
        SeedBranch(db, branchId: 1, warehouseId: 1);
        db.Branches.Add(new Branch { Id = 2, CompanyId = 1, Code = "B2", Name = "B2", IsActive = true });
        db.Warehouses.Add(new Warehouse { Id = 2, Name = "Other", BranchId = 2, IsDefault = false });
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

        var bytes = await svc.ExportSalesReportAsync(
            DateTime.UtcNow.Date.AddDays(-1), DateTime.UtcNow.Date.AddDays(1), "daily", "xlsx", branchId: 1);
        bytes.Length.Should().BeGreaterThan(100);
        // Branch 2 warehouse invoice must not appear when exporting branch 1 — spot-check via JSON aggregate
        var daily = await svc.GetDailySalesSummaryAsync(
            DateTime.UtcNow.Date.AddDays(-1), DateTime.UtcNow.Date.AddDays(1), branchId: 1);
        daily.Data!.InvoiceCount.Should().Be(1);
        daily.Data.SalesTotal.Should().Be(50);
    }

    private static void SeedBranch(Infrastructure.Data.ApplicationDbContext db, int branchId, int warehouseId)
    {
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "T", CurrencyCode = "PKR", IsActive = true });
        db.Branches.Add(new Branch
        {
            Id = branchId, CompanyId = 1, Code = "HQ", Name = "HQ", IsDefault = true, IsActive = true
        });
        db.Warehouses.Add(new Warehouse
        {
            Id = warehouseId, Name = "Main", BranchId = branchId, IsDefault = true, CompanyId = 1
        });
    }

    private static ReportService CreateReportService(
        Infrastructure.Data.ApplicationDbContext db,
        CurrentCompanyContext company)
    {
        var analytics = new Mock<IAnalyticsService>();
        analytics.Setup(a => a.GetAnalyticsAsync(
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnalyticsDto([], [], [], 0, 0, [], [], 0, 0, null));

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
