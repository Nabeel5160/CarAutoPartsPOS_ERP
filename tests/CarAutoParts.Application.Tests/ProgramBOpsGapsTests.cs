using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Purchases;
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

/// <summary>
/// Program B (Ops gaps) regression: RFQ → vendor quote → compare → PO, sales
/// targets CRUD, and withholding tax fields on supplier payments. GL posting is
/// mocked so these stay independent of the (pre-existing) accounting-period
/// fixture issues affecting some Phase2/4/5 GL-integration tests.
/// </summary>
public class ProgramBOpsGapsTests
{
    // ---------- RFQ → vendor quote → compare → PO ----------

    private sealed class RfqHarness
    {
        public required ApplicationDbContext Db { get; init; }
        public required PurchaseRfqService Rfq { get; init; }
        public required Mock<IPurchaseOrderService> PurchaseOrders { get; init; }
        public int ProductId { get; init; }
        public int SupplierId { get; init; }
    }

    private static async Task<RfqHarness> CreateRfqHarnessAsync()
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        var db = TestDbContextFactory.Create();

        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        db.Categories.Add(new Category { Id = 1, Name = "Cat", CreatedAt = DateTime.UtcNow, CreatedBy = "t" });
        db.Brands.Add(new Brand { Id = 1, Name = "Brand", CreatedAt = DateTime.UtcNow, CreatedBy = "t" });
        db.Products.Add(new Product
        {
            Id = 1, Name = "Brake Pad", Sku = "BP-1", CategoryId = 1, BrandId = 1, Unit = "PCS",
            PurchasePrice = 100, CostPrice = 100, SalePrice = 250, TaxRatePercent = 0, IsActive = true,
            CompanyId = 1, CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        });
        db.Suppliers.Add(new Supplier
        {
            Id = 1, Name = "Acme Parts Supplier", IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "t"
        });
        db.NumberSequences.Add(new NumberSequence
        {
            CompanyId = 1, DocumentType = "RFQ", Prefix = "RFQ-", NextValue = 1, Padding = 4
        });
        await db.SaveChangesAsync();

        var enterprise = new EnterpriseDbAdapter(db);
        var purchaseOrders = new Mock<IPurchaseOrderService>();
        var rfq = new PurchaseRfqService(enterprise, company, purchaseOrders.Object);

        return new RfqHarness { Db = db, Rfq = rfq, PurchaseOrders = purchaseOrders, ProductId = 1, SupplierId = 1 };
    }

    [Fact]
    public async Task CreateRfq_Requires_At_Least_One_Line()
    {
        var h = await CreateRfqHarnessAsync();
        var result = await h.Rfq.CreateAsync(new CreatePurchaseRfqRequest(null, "No lines", []));
        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("at least one line");
    }

    [Fact]
    public async Task CreateRfq_Then_Send_Then_AddQuote_Moves_To_QuotesReceived()
    {
        var h = await CreateRfqHarnessAsync();

        var created = await h.Rfq.CreateAsync(new CreatePurchaseRfqRequest(
            DateTime.UtcNow.AddDays(7), "Quarterly restock",
            [new CreatePurchaseRfqLineRequest(h.ProductId, 50, null)]));
        created.Succeeded.Should().BeTrue();
        created.Data!.Status.Should().Be(PurchaseRfqStatus.Draft);

        var sent = await h.Rfq.SendAsync(created.Data.Id);
        sent.Succeeded.Should().BeTrue();
        sent.Data!.Status.Should().Be(PurchaseRfqStatus.Sent);

        var quote = await h.Rfq.AddVendorQuoteAsync(created.Data.Id, new CreateVendorQuoteRequest(
            h.SupplierId, DateTime.UtcNow.AddDays(14), null,
            [new CreateVendorQuoteLineRequest(h.ProductId, 50, 95m, 5, null)]));
        quote.Succeeded.Should().BeTrue();
        quote.Data!.TotalAmount.Should().Be(50 * 95m);

        var refreshed = await h.Rfq.GetByIdAsync(created.Data.Id);
        refreshed!.Status.Should().Be(PurchaseRfqStatus.QuotesReceived);
    }

    [Fact]
    public async Task SelectVendorQuote_Deselects_Previously_Selected_Sibling()
    {
        var h = await CreateRfqHarnessAsync();
        AddSecondSupplier(h.Db);

        var created = await h.Rfq.CreateAsync(new CreatePurchaseRfqRequest(
            null, null, [new CreatePurchaseRfqLineRequest(h.ProductId, 10, null)]));
        await h.Rfq.SendAsync(created.Data!.Id);

        var quoteA = await h.Rfq.AddVendorQuoteAsync(created.Data.Id, new CreateVendorQuoteRequest(
            1, null, null, [new CreateVendorQuoteLineRequest(h.ProductId, 10, 100m, null, null)]));
        var quoteB = await h.Rfq.AddVendorQuoteAsync(created.Data.Id, new CreateVendorQuoteRequest(
            2, null, null, [new CreateVendorQuoteLineRequest(h.ProductId, 10, 90m, null, null)]));

        var selectA = await h.Rfq.SelectVendorQuoteAsync(quoteA.Data!.Id);
        selectA.Succeeded.Should().BeTrue();
        selectA.Data!.Status.Should().Be(VendorQuoteStatus.Selected);

        // Cheaper quote B wins later — A should flip back to Received.
        var selectB = await h.Rfq.SelectVendorQuoteAsync(quoteB.Data!.Id);
        selectB.Succeeded.Should().BeTrue();

        var refreshedRfq = await h.Rfq.GetByIdAsync(created.Data.Id);
        refreshedRfq!.VendorQuotes.Single(q => q.Id == quoteA.Data.Id).Status.Should().Be(VendorQuoteStatus.Received);
        refreshedRfq.VendorQuotes.Single(q => q.Id == quoteB.Data.Id).Status.Should().Be(VendorQuoteStatus.Selected);
    }

    [Fact]
    public async Task CreatePoFromQuote_Closes_Rfq_And_Links_PurchaseOrder()
    {
        var h = await CreateRfqHarnessAsync();

        var created = await h.Rfq.CreateAsync(new CreatePurchaseRfqRequest(
            null, null, [new CreatePurchaseRfqLineRequest(h.ProductId, 20, null)]));
        await h.Rfq.SendAsync(created.Data!.Id);
        var quote = await h.Rfq.AddVendorQuoteAsync(created.Data.Id, new CreateVendorQuoteRequest(
            h.SupplierId, null, null, [new CreateVendorQuoteLineRequest(h.ProductId, 20, 80m, null, null)]));

        h.PurchaseOrders
            .Setup(p => p.CreateAsync(It.IsAny<PurchaseOrderCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PurchaseOrderDetailDto>.Success(new PurchaseOrderDetailDto(
                77, "PO-0001", h.SupplierId, "Acme Parts Supplier", PurchaseOrderStatus.Draft,
                DateTime.UtcNow, null, 1600, 0, 0, 1600, null, null, null, null, null, [])));

        var po = await h.Rfq.CreatePoFromQuoteAsync(quote.Data!.Id);
        po.Succeeded.Should().BeTrue();
        po.Data!.Id.Should().Be(77);

        var refreshedRfq = await h.Rfq.GetByIdAsync(created.Data.Id);
        refreshedRfq!.Status.Should().Be(PurchaseRfqStatus.Closed);
        refreshedRfq.PurchaseOrderId.Should().Be(77);
    }

    private static void AddSecondSupplier(ApplicationDbContext db)
    {
        db.Suppliers.Add(new Supplier { Id = 2, Name = "Second Supplier", IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "t" });
        db.SaveChanges();
    }

    // ---------- Sales targets ----------

    private static (SalesTargetService Svc, ApplicationDbContext Db) CreateSalesTargetHarness()
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        var db = TestDbContextFactory.Create();
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        db.Users.Add(new AppUser { Id = 1, Username = "sales1", PasswordHash = "x", IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "t" });
        db.SaveChanges();

        var enterprise = new EnterpriseDbAdapter(db);
        return (new SalesTargetService(enterprise, company), db);
    }

    [Fact]
    public async Task CreateSalesTarget_Then_Duplicate_Period_Is_Rejected()
    {
        var (svc, _) = CreateSalesTargetHarness();

        var first = await svc.CreateAsync(new SalesTargetUpsertRequest(1, 2026, 8, 500_000m, "Q3 push"));
        first.Succeeded.Should().BeTrue();
        first.Data!.TargetAmount.Should().Be(500_000m);

        var dup = await svc.CreateAsync(new SalesTargetUpsertRequest(1, 2026, 8, 600_000m, null));
        dup.Succeeded.Should().BeFalse();
        dup.Error.Should().Contain("already exists");
    }

    [Fact]
    public async Task SalesTarget_Rejects_Invalid_Month_And_Negative_Amount()
    {
        var (svc, _) = CreateSalesTargetHarness();

        (await svc.CreateAsync(new SalesTargetUpsertRequest(1, 2026, 13, 100m))).Succeeded.Should().BeFalse();
        (await svc.CreateAsync(new SalesTargetUpsertRequest(1, 2026, 1, -1m))).Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteSalesTarget_SoftDeletes_And_Excludes_From_List()
    {
        var (svc, _) = CreateSalesTargetHarness();
        var created = await svc.CreateAsync(new SalesTargetUpsertRequest(1, 2026, 9, 250_000m));

        var deleted = await svc.DeleteAsync(created.Data!.Id);
        deleted.Succeeded.Should().BeTrue();

        var list = await svc.GetAllAsync();
        list.Should().BeEmpty();
    }

    // ---------- Withholding tax on supplier payments ----------

    private static (PaymentPostingService Svc, Mock<IGlPostingService> Gl, ApplicationDbContext Db) CreatePaymentHarness()
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        var db = TestDbContextFactory.Create();
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        db.Suppliers.Add(new Supplier { Id = 1, Name = "WHT Supplier", Balance = 100_000m, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "t" });
        db.SaveChanges();

        var gl = new Mock<IGlPostingService>();
        gl.Setup(g => g.PostDocumentAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<int?>(), It.IsAny<IReadOnlyList<GlPostingLineRequest>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GlJournalDraftDto>.Success(new GlJournalDraftDto(1, "JV-0001", JournalStatus.Posted, 0, 0)));

        var enterprise = new EnterpriseDbAdapter(db);
        return (new PaymentPostingService(enterprise, company, gl.Object, OpsSlaTestDoubles.NoOp), gl, db);
    }

    [Fact]
    public async Task SupplierPayment_With_Wht_Posts_Net_Cash_And_WithholdingLine()
    {
        var (svc, gl, db) = CreatePaymentHarness();
        IReadOnlyList<GlPostingLineRequest>? capturedLines = null;
        gl.Setup(g => g.PostDocumentAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<int?>(), It.IsAny<IReadOnlyList<GlPostingLineRequest>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<string, DateTime, string?, string?, int?, IReadOnlyList<GlPostingLineRequest>, bool, CancellationToken>(
                (_, _, _, _, _, lines, _, _) => capturedLines = lines)
            .ReturnsAsync(Result<GlJournalDraftDto>.Success(new GlJournalDraftDto(1, "JV-0001", JournalStatus.Posted, 10_000m, 10_000m)));

        var result = await svc.PostSupplierPaymentAsync(new PostSupplierPaymentRequest(
            1, 10_000m, "Bank", "REF-1", null, "WHT on services", WithholdingTaxRate: 10));

        result.Succeeded.Should().BeTrue();
        capturedLines.Should().Contain(l => l.MappingKey == "WithholdingTaxPayable" && l.Amount == 1_000m);
        capturedLines.Should().Contain(l => l.MappingKey == "Bank" && l.Amount == 9_000m);

        var payment = db.SupplierPayments.Single();
        payment.WithholdingTaxRate.Should().Be(10);
        payment.WithholdingTaxAmount.Should().Be(1_000m);
    }

    [Fact]
    public async Task SupplierPayment_Rejects_Wht_Rate_Out_Of_Range()
    {
        var (svc, _, _) = CreatePaymentHarness();
        var result = await svc.PostSupplierPaymentAsync(new PostSupplierPaymentRequest(
            1, 1_000m, "Cash", WithholdingTaxRate: 150));
        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("Withholding tax rate");
    }

    [Fact]
    public async Task SupplierPayment_Rejects_When_Wht_Consumes_Entire_Amount()
    {
        var (svc, _, _) = CreatePaymentHarness();
        var result = await svc.PostSupplierPaymentAsync(new PostSupplierPaymentRequest(
            1, 1_000m, "Cash", WithholdingTaxRate: 100));
        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("Net cash payment");
    }
}
