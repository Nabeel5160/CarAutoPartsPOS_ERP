using CarAutoParts.Application.DTOs.Purchases;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Finance;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using CarAutoParts.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CarAutoParts.Application.Tests;

public class Phase2ProcurementTests
{
    private static async Task<(ApplicationDbContext Db, EnterpriseDbAdapter Ent, EnterpriseInventoryService Inv, EnterprisePurchaseService Purchase, CurrentCompanyContext Company)> CreateAsync()
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
            Id = 1, CompanyId = 1, Name = "FY26",
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31)
        });
        db.AccountingPeriods.Add(new AccountingPeriod
        {
            Id = 1, CompanyId = 1, FiscalYearId = 1, PeriodNumber = 1, Name = "Jul 2026",
            StartDate = new DateTime(2026, 7, 1), EndDate = new DateTime(2026, 7, 31), IsClosed = false
        });

        void Acc(int id, string code, string name, AccountType type) =>
            db.GlAccounts.Add(new GlAccount
            {
                Id = id, CompanyId = 1, Code = code, Name = name, AccountType = type, IsPostable = true, IsActive = true
            });

        Acc(4, "1300", "Inventory", AccountType.Asset);
        Acc(5, "1400", "GRNI", AccountType.Liability);
        Acc(6, "2100", "AP", AccountType.Liability);

        void Map(string doc, string key, int accountId) =>
            db.AccountMappings.Add(new AccountMapping
            {
                CompanyId = 1, DocumentType = doc, MappingKey = key, AccountId = accountId
            });

        Map("Grn", "Inventory", 4);
        Map("Grn", "GrnClearing", 5);
        Map("PurchaseInvoice", "GrnClearing", 5);
        Map("PurchaseInvoice", "Payable", 6);
        Map("PurchaseReturn", "Inventory", 4);
        Map("PurchaseReturn", "Payable", 6);

        foreach (var doc in new[] { "GRN", "PI", "REQ", "PO", "JV" })
            db.NumberSequences.Add(new NumberSequence
            {
                CompanyId = 1, DocumentType = doc, Prefix = doc + "-", NextValue = 1, Padding = 4
            });

        db.Categories.Add(new Category { Id = 1, Name = "Cat", CreatedAt = DateTime.UtcNow });
        db.Brands.Add(new Brand { Id = 1, Name = "Brand", CreatedAt = DateTime.UtcNow });
        db.Warehouses.Add(new Warehouse { Id = 1, Name = "Main", CompanyId = 1, BranchId = 1, CreatedAt = DateTime.UtcNow });
        db.Products.Add(new Product
        {
            Id = 1, Name = "Filter", Sku = "F-1", CategoryId = 1, BrandId = 1, Unit = "PCS",
            PurchasePrice = 100, CostPrice = 100, SalePrice = 200, IsActive = true, CompanyId = 1,
            MinimumStock = 5, ReorderLevel = 5, MaximumStock = 20, CreatedAt = DateTime.UtcNow
        });
        db.InventoryItems.Add(new InventoryItem
        {
            ProductId = 1, WarehouseId = 1, QuantityOnHand = 2, AverageCost = 100, CreatedAt = DateTime.UtcNow
        });
        db.Suppliers.Add(new Supplier { Id = 1, Name = "Parts Co", IsActive = true, Balance = 500, CreatedAt = DateTime.UtcNow });
        db.CompanySettings.Add(new CompanySettings
        {
            InvoicePrefix = "INV",
            GrnOverReceivePercent = 0,
            GrnUnderReceiveAllowed = true,
            ThreeWayQtyTolerancePercent = 5,
            ThreeWayPriceTolerancePercent = 2,
            CreatedAt = DateTime.UtcNow
        });
        db.PurchaseOrders.Add(new PurchaseOrder
        {
            Id = 1, OrderNumber = "PO-1", SupplierId = 1, WarehouseId = 1,
            Status = PurchaseOrderStatus.Approved, OrderDate = DateTime.UtcNow,
            SubTotal = 1000, GrandTotal = 1000, CreatedAt = DateTime.UtcNow,
            Lines =
            {
                new PurchaseOrderLine
                {
                    Id = 1, ProductId = 1, QuantityOrdered = 10, QuantityReceived = 0,
                    UnitPrice = 100, LineTotal = 1000
                }
            }
        });
        await db.SaveChangesAsync();

        var enterprise = new EnterpriseDbAdapter(db);
        var outbox = new Mock<IOutboxWriter>();
        var periods = new AccountingPeriodService(enterprise, companyCtx);
        var gl = new GlPostingService(enterprise, companyCtx, outbox.Object, periods);
        var inv = new EnterpriseInventoryService(enterprise, companyCtx, gl, OpsSlaTestDoubles.NoOp);
        var approvals = new Mock<IApprovalWorkflowService>();
        approvals.Setup(a => a.EnsureApprovedOrQueueAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Application.Common.Result.Success());
        var purchase = new EnterprisePurchaseService(enterprise, companyCtx, gl, outbox.Object, approvals.Object, OpsSlaTestDoubles.NoOp);
        return (db, enterprise, inv, purchase, companyCtx);
    }

    [Fact]
    public async Task Over_receive_is_blocked_when_percent_is_zero()
    {
        var (db, _, inv, _, _) = await CreateAsync();
        var result = await inv.CreateGrnAsync(new CreateGrnRequest(
            1, 1, DateTime.UtcNow, 0, null,
            [new CreateGrnLineRequest(1, 11, 100, 1)]));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("Over-receive");
    }

    [Fact]
    public async Task Three_way_match_allows_qty_within_tolerance()
    {
        var (db, _, inv, purchase, _) = await CreateAsync();

        var grn = await inv.CreateGrnAsync(new CreateGrnRequest(
            1, 1, DateTime.UtcNow, 0, null,
            [new CreateGrnLineRequest(1, 10, 100, 1)]));
        grn.Succeeded.Should().BeTrue();
        (await inv.PostGrnAsync(grn.Data!.Id)).Succeeded.Should().BeTrue();

        var pi = await purchase.CreatePurchaseInvoiceAsync(new CreatePurchaseInvoiceRequest(
            1, 1, grn.Data.Id, DateTime.UtcNow, 0,
            [new CreatePurchaseInvoiceLineRequest(1, 10.4m, 100)]));
        pi.Succeeded.Should().BeTrue();

        var match = await purchase.MatchThreeWayAsync(pi.Data!.Id);
        match.Succeeded.Should().BeTrue();
        match.Data!.IsFullyMatched.Should().BeTrue();
    }

    [Fact]
    public async Task Reorder_suggest_creates_draft_pr()
    {
        var (db, ent, _, _, company) = await CreateAsync();
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.CurrentUser).Returns(new AppUser { Username = "tester" });
        var po = new Mock<IPurchaseOrderService>();
        var req = new PurchaseRequisitionService(ent, company, user.Object, po.Object);
        var reorder = new ReorderService(ent, req);

        var suggestions = await reorder.SuggestAsync(1);
        suggestions.Should().NotBeEmpty();
        suggestions[0].SuggestedQty.Should().Be(18); // max 20 - available 2

        var draft = await reorder.CreateDraftPrFromSuggestionsAsync(
            [new ReorderSuggestionLineDto(1, suggestions[0].SuggestedQty, 100)],
            1, 1);
        draft.Succeeded.Should().BeTrue();
        draft.Data!.Status.Should().Be(PurchaseRequisitionStatus.Draft);
    }

    [Fact]
    public async Task Qc_hold_blocks_three_way_match_until_release()
    {
        var (db, _, inv, purchase, _) = await CreateAsync();
        var create = await inv.CreateGrnAsync(new CreateGrnRequest(
            1, 1, DateTime.UtcNow, 0, null,
            [new CreateGrnLineRequest(1, 10, 100, 1)],
            null,
            HoldForQc: true));
        create.Succeeded.Should().BeTrue();
        var posted = await inv.PostGrnAsync(create.Data!.Id);
        posted.Succeeded.Should().BeTrue();
        posted.Data!.Status.Should().Be(GrnStatus.QcHold);

        var pi = await purchase.CreatePurchaseInvoiceAsync(new CreatePurchaseInvoiceRequest(
            1, 1, create.Data.Id, DateTime.UtcNow, 0,
            [new CreatePurchaseInvoiceLineRequest(1, 10, 100)]));
        pi.Succeeded.Should().BeTrue();

        var blocked = await purchase.MatchThreeWayAsync(pi.Data!.Id);
        blocked.Succeeded.Should().BeFalse();
        blocked.Error.Should().Contain("QC");

        (await inv.ReleaseQcAsync(create.Data.Id)).Succeeded.Should().BeTrue();
        var matched = await purchase.MatchThreeWayAsync(pi.Data.Id);
        matched.Succeeded.Should().BeTrue();
        matched.Data!.IsFullyMatched.Should().BeTrue();
    }
}
