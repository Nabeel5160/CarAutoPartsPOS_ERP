using CarAutoParts.Application.DTOs.Fbr;
using CarAutoParts.Application.DTOs.Pos;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Finance;
using CarAutoParts.Application.Interfaces;
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

namespace CarAutoParts.Application.Tests;

public class DocumentPostingIntegrationTests
{
    private sealed class TestHarness
    {
        public required ApplicationDbContext Db { get; init; }
        public required CurrentCompanyContext Company { get; init; }
        public required GlPostingService Gl { get; init; }
        public required EnterpriseInventoryService Inventory { get; init; }
        public required EnterprisePurchaseService Purchase { get; init; }
        public required PosCheckoutService Pos { get; init; }
    }

    private static PosCheckoutDto CashCheckout(string? idem = null, decimal qty = 1) =>
        new(1, null, null, "Cash", 0, null, null, null,
            [new PosCheckoutLineDto(1, qty, null, 0)], idem);

    private static async Task<TestHarness> CreateAsync(bool periodClosed = false)
    {
        var companyCtx = new CurrentCompanyContext();
        companyCtx.Set(1);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options, companyCtx);

        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        db.Branches.Add(new Branch { Id = 1, CompanyId = 1, Code = "HO", Name = "HO", IsDefault = true, IsActive = true });
        db.FiscalYears.Add(new FiscalYear
        {
            Id = 1,
            CompanyId = 1,
            Name = "FY26",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31)
        });
        db.AccountingPeriods.Add(new AccountingPeriod
        {
            Id = 1,
            CompanyId = 1,
            FiscalYearId = 1,
            PeriodNumber = 1,
            Name = "Jul 2026",
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 31),
            IsClosed = periodClosed
        });

        void Acc(int id, string code, string name, AccountType type) =>
            db.GlAccounts.Add(new GlAccount
            {
                Id = id,
                CompanyId = 1,
                Code = code,
                Name = name,
                AccountType = type,
                IsPostable = true,
                IsActive = true
            });

        Acc(1, "1100", "Cash", AccountType.Asset);
        Acc(2, "1110", "Bank", AccountType.Asset);
        Acc(3, "1200", "AR", AccountType.Asset);
        Acc(4, "1300", "Inventory", AccountType.Asset);
        Acc(5, "1400", "GRNI", AccountType.Liability);
        Acc(6, "2100", "AP", AccountType.Liability);
        Acc(7, "2200", "Tax", AccountType.Liability);
        Acc(8, "4100", "Sales", AccountType.Revenue);
        Acc(9, "5100", "COGS", AccountType.CostOfGoods);

        void Map(string doc, string key, int accountId) =>
            db.AccountMappings.Add(new AccountMapping
            {
                CompanyId = 1,
                DocumentType = doc,
                MappingKey = key,
                AccountId = accountId
            });

        Map("SalesInvoice", "Cash", 1);
        Map("SalesInvoice", "Bank", 2);
        Map("SalesInvoice", "Receivable", 3);
        Map("SalesInvoice", "Inventory", 4);
        Map("SalesInvoice", "Tax", 7);
        Map("SalesInvoice", "Revenue", 8);
        Map("SalesInvoice", "Cogs", 9);
        Map("Grn", "Inventory", 4);
        Map("Grn", "GrnClearing", 5);
        Map("PurchaseInvoice", "Inventory", 4);
        Map("PurchaseInvoice", "GrnClearing", 5);
        Map("PurchaseInvoice", "Payable", 6);
        Map("PurchaseInvoice", "Tax", 7);
        Map("Payment", "Cash", 1);
        Map("Payment", "Bank", 2);
        Map("Payment", "Receivable", 3);
        Map("Payment", "Payable", 6);

        foreach (var doc in new[] { "JV", "GRN", "PI", "INV" })
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

        db.Categories.Add(new Category { Id = 1, Name = "Cat", CreatedAt = DateTime.UtcNow, CreatedBy = "t" });
        db.Brands.Add(new Brand { Id = 1, Name = "Brand", CreatedAt = DateTime.UtcNow, CreatedBy = "t" });
        db.Warehouses.Add(new Warehouse { Id = 1, Name = "Main", CompanyId = 1, BranchId = 1, CreatedAt = DateTime.UtcNow, CreatedBy = "t" });
        db.Products.Add(new Product
        {
            Id = 1,
            Name = "Filter",
            Sku = "F-1",
            CategoryId = 1,
            BrandId = 1,
            Unit = "PCS",
            PurchasePrice = 100,
            CostPrice = 100,
            SalePrice = 200,
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
        db.Suppliers.Add(new Supplier
        {
            Id = 1,
            Name = "Parts Co",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "t"
        });
        db.CompanySettings.Add(new CompanySettings { InvoicePrefix = "INV", CreatedAt = DateTime.UtcNow, CreatedBy = "t" });
        await db.SaveChangesAsync();

        var enterprise = new EnterpriseDbAdapter(db);
        var outbox = new Mock<IOutboxWriter>();
        var periods = new AccountingPeriodService(enterprise, companyCtx);
        var gl = new GlPostingService(enterprise, companyCtx, outbox.Object, periods);
        var inv = new EnterpriseInventoryService(enterprise, companyCtx, gl);
        var approvals = new Mock<IApprovalWorkflowService>();
        approvals.Setup(a => a.EnsureApprovedOrQueueAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Application.Common.Result.Success());
        var purchase = new EnterprisePurchaseService(enterprise, companyCtx, gl, outbox.Object, approvals.Object);

        var inventoryMock = new Mock<IInventoryService>();
        inventoryMock
            .Setup(i => i.DeductStockAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Application.Common.Result<decimal>.Success(10m));

        var fbrMock = new Mock<IFbrService>();
        fbrMock
            .Setup(f => f.PostInvoiceAsync(It.IsAny<FbrInvoiceRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FbrPostResultDto.Ok("FBR-OK", true, null, null));

        var fbrOutbox = new Mock<IFbrOutboxService>();
        var salesEnt = new Mock<IEnterpriseSalesService>();
        salesEnt.Setup(s => s.GetPriceForProductAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int pid, int? _, decimal __, CancellationToken ___) =>
                Application.Common.Result<PriceLookupResultDto>.Success(new PriceLookupResultDto(pid, 100, null, null)));
        salesEnt.Setup(s => s.CheckCreditLimitAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Application.Common.Result<CreditCheckResultDto>.Success(
                new CreditCheckResultDto(true, 10000, 0, 10000, null)));

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
            gl,
            fbrOutbox.Object,
            companyCtx,
            new CurrentUserService(),
            salesEnt.Object,
            new AtpService(enterprise),
            PosCheckoutServiceTests.CreateFeatureGate());

        return new TestHarness
        {
            Db = db,
            Company = companyCtx,
            Gl = gl,
            Inventory = inv,
            Purchase = purchase,
            Pos = pos
        };
    }

    [Fact]
    public async Task Pos_checkout_posts_stock_payment_and_journal()
    {
        var h = await CreateAsync();
        var result = await h.Pos.CheckoutAsync(CashCheckout("idem-1", 2), CancellationToken.None);

        result.SalesInvoiceId.Should().BeGreaterThan(0);
        (await h.Db.Payments.CountAsync()).Should().Be(1);
        var journal = await h.Db.JournalEntries.Include(j => j.Lines).SingleAsync();
        journal.Status.Should().Be(JournalStatus.Posted);
        journal.SourceDocumentType.Should().Be("SalesInvoice");
        journal.TotalDebit.Should().Be(journal.TotalCredit);
    }

    [Fact]
    public async Task Pos_idempotency_returns_same_invoice()
    {
        var h = await CreateAsync();
        var dto = CashCheckout("same-key");
        var a = await h.Pos.CheckoutAsync(dto, CancellationToken.None);
        var b = await h.Pos.CheckoutAsync(dto, CancellationToken.None);
        a.SalesInvoiceId.Should().Be(b.SalesInvoiceId);
        (await h.Db.SalesInvoices.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Closed_period_blocks_pos_checkout()
    {
        var h = await CreateAsync(periodClosed: true);
        var act = async () => await h.Pos.CheckoutAsync(CashCheckout(), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*period*");
    }

    [Fact]
    public async Task Grn_post_creates_inventory_and_posted_journal()
    {
        var h = await CreateAsync();
        var create = await h.Inventory.CreateGrnAsync(new CreateGrnRequest(
            1, null, new DateTime(2026, 7, 10), 50, null,
            [new CreateGrnLineRequest(1, 5, 100)]), CancellationToken.None);
        create.Succeeded.Should().BeTrue();

        var post = await h.Inventory.PostGrnAsync(create.Data!.Id, CancellationToken.None);
        post.Succeeded.Should().BeTrue();
        post.Data!.Status.Should().Be(GrnStatus.Posted);

        var item = await h.Db.InventoryItems.SingleAsync(i => i.ProductId == 1);
        item.QuantityOnHand.Should().Be(55);
        var jv = await h.Db.JournalEntries.SingleAsync(j => j.SourceDocumentType == "Grn");
        jv.Status.Should().Be(JournalStatus.Posted);
        jv.TotalDebit.Should().Be(550);
    }

    [Fact]
    public async Task Ap_match_failure_refuses_post()
    {
        var h = await CreateAsync();
        var inv = await h.Purchase.CreatePurchaseInvoiceAsync(new CreatePurchaseInvoiceRequest(
            1, null, null, new DateTime(2026, 7, 10), 0,
            [new CreatePurchaseInvoiceLineRequest(1, 2, 100)]), CancellationToken.None);
        inv.Succeeded.Should().BeTrue();

        var post = await h.Purchase.PostPurchaseInvoiceAsync(inv.Data!.Id, CancellationToken.None);
        post.Succeeded.Should().BeFalse();
        post.Error.Should().Contain("three-way");
    }

    [Fact]
    public async Task Ap_post_after_match_posts_journal_and_supplier_balance()
    {
        var h = await CreateAsync();
        var grn = await h.Inventory.CreateGrnAsync(new CreateGrnRequest(
            1, null, new DateTime(2026, 7, 10), 0, null,
            [new CreateGrnLineRequest(1, 2, 100)]), CancellationToken.None);
        await h.Inventory.PostGrnAsync(grn.Data!.Id, CancellationToken.None);

        var inv = await h.Purchase.CreatePurchaseInvoiceAsync(new CreatePurchaseInvoiceRequest(
            1, null, grn.Data.Id, new DateTime(2026, 7, 10), 0,
            [new CreatePurchaseInvoiceLineRequest(1, 2, 100)]), CancellationToken.None);
        var match = await h.Purchase.MatchThreeWayAsync(inv.Data!.Id, CancellationToken.None);
        match.Succeeded.Should().BeTrue();
        match.Data!.IsFullyMatched.Should().BeTrue();

        var post = await h.Purchase.PostPurchaseInvoiceAsync(inv.Data.Id, CancellationToken.None);
        post.Succeeded.Should().BeTrue();
        (await h.Db.Suppliers.SingleAsync()).Balance.Should().Be(200);
        var jv = await h.Db.JournalEntries.SingleAsync(j => j.SourceDocumentType == "PurchaseInvoice");
        jv.Status.Should().Be(JournalStatus.Posted);
    }

    [Fact]
    public async Task Fbr_failure_enqueues_outbox()
    {
        var h = await CreateAsync();
        var inventoryMock = new Mock<IInventoryService>();
        inventoryMock
            .Setup(i => i.DeductStockAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Application.Common.Result<decimal>.Success(10m));
        var fbrMock = new Mock<IFbrService>();
        fbrMock
            .Setup(f => f.PostInvoiceAsync(It.IsAny<FbrInvoiceRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FbrPostResultDto.Fail("FBR down"));
        var fbrOutbox = new Mock<IFbrOutboxService>();
        var salesEnt = new Mock<IEnterpriseSalesService>();
        salesEnt.Setup(s => s.GetPriceForProductAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int pid, int? _, decimal __, CancellationToken ___) =>
                Application.Common.Result<PriceLookupResultDto>.Success(new PriceLookupResultDto(pid, 100, null, null)));
        salesEnt.Setup(s => s.CheckCreditLimitAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Application.Common.Result<CreditCheckResultDto>.Success(
                new CreditCheckResultDto(true, 10000, 0, 10000, null)));
        var pos = new PosCheckoutService(
            new Repository<Product>(h.Db),
            new Repository<SalesInvoice>(h.Db),
            new Repository<FbrSubmission>(h.Db),
            new Repository<CompanySettings>(h.Db),
            new Repository<Payment>(h.Db),
            new Repository<Customer>(h.Db),
            new Repository<ProductKit>(h.Db),
            new Repository<CashierShift>(h.Db),
            new Repository<HeldSale>(h.Db),
            new Repository<ProductSupersession>(h.Db),
            new Repository<Warehouse>(h.Db),
            inventoryMock.Object,
            fbrMock.Object,
            new UnitOfWork(h.Db),
            new PosCheckoutValidator(),
            h.Gl,
            fbrOutbox.Object,
            h.Company,
            new CurrentUserService(),
            salesEnt.Object,
            new AtpService(new EnterpriseDbAdapter(h.Db)),
            PosCheckoutServiceTests.CreateFeatureGate());

        await pos.CheckoutAsync(CashCheckout(), CancellationToken.None);
        fbrOutbox.Verify(o => o.EnqueueFbrRetry(It.IsAny<int>(), It.IsAny<string?>()), Times.Once);
        (await h.Db.SalesInvoices.CountAsync()).Should().Be(1);
        (await h.Db.Payments.CountAsync()).Should().Be(1);
        (await h.Db.JournalEntries.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Fbr_throw_does_not_roll_back_sale()
    {
        var h = await CreateAsync();
        var inventoryMock = new Mock<IInventoryService>();
        inventoryMock
            .Setup(i => i.DeductStockAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Application.Common.Result<decimal>.Success(10m));
        var fbrMock = new Mock<IFbrService>();
        fbrMock
            .Setup(f => f.PostInvoiceAsync(It.IsAny<FbrInvoiceRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("FBR transport exploded"));
        var fbrOutbox = new Mock<IFbrOutboxService>();
        var salesEnt = new Mock<IEnterpriseSalesService>();
        salesEnt.Setup(s => s.GetPriceForProductAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int pid, int? _, decimal __, CancellationToken ___) =>
                Application.Common.Result<PriceLookupResultDto>.Success(new PriceLookupResultDto(pid, 100, null, null)));
        salesEnt.Setup(s => s.CheckCreditLimitAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Application.Common.Result<CreditCheckResultDto>.Success(
                new CreditCheckResultDto(true, 10000, 0, 10000, null)));
        var pos = new PosCheckoutService(
            new Repository<Product>(h.Db),
            new Repository<SalesInvoice>(h.Db),
            new Repository<FbrSubmission>(h.Db),
            new Repository<CompanySettings>(h.Db),
            new Repository<Payment>(h.Db),
            new Repository<Customer>(h.Db),
            new Repository<ProductKit>(h.Db),
            new Repository<CashierShift>(h.Db),
            new Repository<HeldSale>(h.Db),
            new Repository<ProductSupersession>(h.Db),
            new Repository<Warehouse>(h.Db),
            inventoryMock.Object,
            fbrMock.Object,
            new UnitOfWork(h.Db),
            new PosCheckoutValidator(),
            h.Gl,
            fbrOutbox.Object,
            h.Company,
            new CurrentUserService(),
            salesEnt.Object,
            new AtpService(new EnterpriseDbAdapter(h.Db)),
            PosCheckoutServiceTests.CreateFeatureGate());

        var result = await pos.CheckoutAsync(CashCheckout("idem-fbr-throw"), CancellationToken.None);
        result.FbrSuccess.Should().BeFalse();
        result.SalesInvoiceId.Should().BeGreaterThan(0);
        fbrOutbox.Verify(o => o.EnqueueFbrRetry(It.IsAny<int>(), It.IsAny<string?>()), Times.Once);
        (await h.Db.SalesInvoices.CountAsync()).Should().Be(1);
        (await h.Db.Payments.CountAsync()).Should().Be(1);
    }
}
