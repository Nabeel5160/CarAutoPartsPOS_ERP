using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Finance;
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

public class Phase4FinanceTests
{
    private static async Task<(ApplicationDbContext Db, CurrentCompanyContext Company, Phase4FinanceService Phase4, IInventoryService Inventory, GlPostingService Gl)> CreateAsync()
    {
        var companyCtx = new CurrentCompanyContext();
        companyCtx.Set(1);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options, companyCtx);

        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        db.FiscalYears.Add(new FiscalYear
        {
            Id = 1, CompanyId = 1, Name = "FY26",
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31)
        });
        db.AccountingPeriods.Add(new AccountingPeriod
        {
            Id = 1, CompanyId = 1, FiscalYearId = 1, PeriodNumber = 7, Name = "Jul 2026",
            StartDate = new DateTime(2026, 7, 1), EndDate = new DateTime(2026, 7, 31), IsClosed = false
        });

        void Acc(int id, string code, AccountType type) =>
            db.GlAccounts.Add(new GlAccount
            {
                Id = id, CompanyId = 1, Code = code, Name = code, AccountType = type, IsPostable = true, IsActive = true
            });

        Acc(1, "1100", AccountType.Asset);
        Acc(2, "1110", AccountType.Asset);
        Acc(3, "1200", AccountType.Asset);
        Acc(4, "1300", AccountType.Asset);
        Acc(5, "2100", AccountType.Liability);
        Acc(6, "2200", AccountType.Liability);
        Acc(7, "3100", AccountType.Equity);
        Acc(8, "4100", AccountType.Revenue);

        void Map(string doc, string key, int accountId) =>
            db.AccountMappings.Add(new AccountMapping { CompanyId = 1, DocumentType = doc, MappingKey = key, AccountId = accountId });

        Map("PurchaseInvoice", "Inventory", 4);
        Map("PurchaseInvoice", "Payable", 5);
        Map("PurchaseInvoice", "Tax", 6);
        Map("SalesReturn", "Revenue", 8);
        Map("SalesReturn", "Tax", 6);
        Map("SalesReturn", "Receivable", 3);
        Map("SalesReturn", "Cash", 1);

        db.NumberSequences.Add(new NumberSequence { CompanyId = 1, DocumentType = "JV", Prefix = "JV-", NextValue = 1, Padding = 4 });
        db.NumberSequences.Add(new NumberSequence { CompanyId = 1, DocumentType = "OB", Prefix = "OB-", NextValue = 1, Padding = 4 });
        db.NumberSequences.Add(new NumberSequence { CompanyId = 1, DocumentType = "BS", Prefix = "BS-", NextValue = 1, Padding = 4 });

        db.Categories.Add(new Category { Id = 1, Name = "C", CreatedAt = DateTime.UtcNow });
        db.Brands.Add(new Brand { Id = 1, Name = "B", CreatedAt = DateTime.UtcNow });
        db.Warehouses.Add(new Warehouse { Id = 1, Name = "Main", CompanyId = 1, CreatedAt = DateTime.UtcNow });
        db.Products.Add(new Product
        {
            Id = 1, Name = "P", Sku = "P1", CategoryId = 1, BrandId = 1, Unit = "PCS",
            PurchasePrice = 10, CostPrice = 10, SalePrice = 20, IsActive = true, CompanyId = 1, CreatedAt = DateTime.UtcNow
        });
        db.Customers.Add(new Customer { Id = 1, Name = "Cust", IsActive = true, CreatedAt = DateTime.UtcNow });
        db.Suppliers.Add(new Supplier { Id = 1, Name = "Sup", IsActive = true, CreatedAt = DateTime.UtcNow });
        db.CompanySettings.Add(new CompanySettings { DefaultTaxRate = 18, DefaultValuationMethod = ValuationMethod.Average, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var enterprise = new EnterpriseDbAdapter(db);
        var uow = new UnitOfWork(db);
        var inventory = TestInventoryFactory.Create(db, company: companyCtx);

        var periods = new AccountingPeriodService(enterprise, companyCtx);
        var outbox = new Mock<IOutboxWriter>();
        var gl = new GlPostingService(enterprise, companyCtx, outbox.Object, periods);
        var reports = new FinancialReportService(enterprise, companyCtx);
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.HasPermission(It.IsAny<string>())).Returns(true);

        var approvals = new Mock<IApprovalWorkflowService>();
        approvals.Setup(a => a.EnsureApprovedOrQueueAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Application.Common.Result.Success());
        var phase4 = new Phase4FinanceService(enterprise, companyCtx, inventory, periods, reports, user.Object, approvals.Object);
        return (db, companyCtx, phase4, inventory, gl);
    }

    [Fact]
    public async Task Opening_balances_posts_balanced_journal_and_sets_cutover()
    {
        var (db, _, phase4, _, _) = await CreateAsync();
        var result = await phase4.PostOpeningBalancesAsync(new PostOpeningBalancesRequest(
            new DateTime(2026, 7, 15),
            "Go live",
            null,
            [new OpeningBalanceInventoryLineRequest(1, 1, 10, 50)],
            [new OpeningBalanceArLineRequest(1, 100)],
            [new OpeningBalanceApLineRequest(1, 80)]));

        result.Succeeded.Should().BeTrue(result.Error);
        var settings = await db.CompanySettings.SingleAsync();
        settings.OpeningBalanceDate.Should().Be(new DateTime(2026, 7, 15));
        var customer = await db.Customers.SingleAsync();
        customer.Balance.Should().Be(100);
        var supplier = await db.Suppliers.SingleAsync();
        supplier.Balance.Should().Be(80);
        var inv = await db.InventoryItems.SingleAsync();
        inv.QuantityOnHand.Should().Be(10);
        var journal = await db.JournalEntries.Include(j => j.Lines).SingleAsync();
        journal.Status.Should().Be(JournalStatus.Posted);
        journal.TotalDebit.Should().Be(journal.TotalCredit);
        journal.SourceDocumentType.Should().Be("OpeningBalance");
    }

    [Fact]
    public async Task Period_close_blocks_on_draft_journal()
    {
        var (db, _, phase4, _, _) = await CreateAsync();
        db.JournalEntries.Add(new JournalEntry
        {
            CompanyId = 1,
            JournalNumber = "JV-DRAFT",
            JournalDate = new DateTime(2026, 7, 10),
            Status = JournalStatus.Draft,
            Lines =
            {
                new JournalLine { CompanyId = 1, AccountId = 1, Debit = 10, Credit = 0 },
                new JournalLine { CompanyId = 1, AccountId = 7, Debit = 0, Credit = 10 }
            }
        });
        await db.SaveChangesAsync();

        var checklist = await phase4.GetPeriodCloseChecklistAsync(1);
        checklist.CanClose.Should().BeFalse();
        checklist.Items.Should().Contain(i => i.Code == "draft_journals" && i.Count == 1);

        var close = await phase4.ClosePeriodWithChecklistAsync(1, forceClose: false);
        close.Succeeded.Should().BeFalse();

        var forced = await phase4.ClosePeriodWithChecklistAsync(1, forceClose: true);
        forced.Succeeded.Should().BeTrue(forced.Error);
        (await db.AccountingPeriods.SingleAsync()).IsClosed.Should().BeTrue();
    }

    [Fact]
    public async Task Bank_recon_match_and_report()
    {
        var (db, _, phase4, _, _) = await CreateAsync();
        db.JournalEntries.Add(new JournalEntry
        {
            CompanyId = 1,
            JournalNumber = "JV-BANK",
            JournalDate = new DateTime(2026, 7, 12),
            Status = JournalStatus.Posted,
            AccountingPeriodId = 1,
            Lines =
            {
                new JournalLine { CompanyId = 1, AccountId = 2, Debit = 500, Credit = 0, Description = "Deposit" },
                new JournalLine { CompanyId = 1, AccountId = 8, Debit = 0, Credit = 500 }
            }
        });
        await db.SaveChangesAsync();
        var jlId = (await db.JournalLines.SingleAsync(l => l.AccountId == 2)).Id;

        var stmt = await phase4.CreateBankStatementAsync(new CreateBankStatementRequest(
            new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), 0, 500, null));
        stmt.Succeeded.Should().BeTrue(stmt.Error);

        var withLine = await phase4.AddBankStatementLineAsync(stmt.Data!.Id,
            new CreateBankStatementLineRequest(new DateTime(2026, 7, 12), 500, "DEP", "Deposit"));
        withLine.Succeeded.Should().BeTrue(withLine.Error);
        var lineId = withLine.Data!.Lines.Single().Id;

        (await phase4.MatchBankLineAsync(lineId, jlId)).Succeeded.Should().BeTrue();
        var report = await phase4.GetBankReconReportAsync(stmt.Data.Id);
        report.Succeeded.Should().BeTrue(report.Error);
        report.Data!.UnclearedStatementLines.Should().BeEmpty();
    }

    [Fact]
    public async Task Purchase_invoice_posts_tax_split()
    {
        var (db, company, _, _, gl) = await CreateAsync();
        var enterprise = new EnterpriseDbAdapter(db);
        var outbox = new Mock<IOutboxWriter>();
        var approvals = new Mock<IApprovalWorkflowService>();
        approvals.Setup(a => a.EnsureApprovedOrQueueAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Application.Common.Result.Success());
        var purchase = new EnterprisePurchaseService(enterprise, company, gl, outbox.Object, approvals.Object, OpsSlaTestDoubles.NoOp);

        db.PurchaseInvoices.Add(new PurchaseInvoice
        {
            CompanyId = 1,
            InvoiceNumber = "PI-1",
            SupplierId = 1,
            InvoiceDate = new DateTime(2026, 7, 20),
            Status = PurchaseInvoiceStatus.Draft,
            SubTotal = 100,
            TaxAmount = 18,
            GrandTotal = 118,
            ThreeWayMatched = true,
            Lines = { new PurchaseInvoiceLine { CompanyId = 1, ProductId = 1, Quantity = 1, UnitCost = 100, LineTotal = 100 } }
        });
        await db.SaveChangesAsync();
        var id = (await db.PurchaseInvoices.SingleAsync()).Id;

        var posted = await purchase.PostPurchaseInvoiceAsync(id);
        posted.Succeeded.Should().BeTrue(posted.Error);
        var journal = await db.JournalEntries.Include(j => j.Lines).OrderByDescending(j => j.Id).FirstAsync();
        journal.Lines.Should().Contain(l => l.AccountId == 6 && l.Debit == 18);
        journal.Lines.Should().Contain(l => l.AccountId == 5 && l.Credit == 118);
        journal.TotalDebit.Should().Be(journal.TotalCredit);
    }

    [Fact]
    public async Task Sales_credit_note_apply_to_invoice()
    {
        var (db, company, _, inventory, gl) = await CreateAsync();
        var uow = new UnitOfWork(db);
        var mapper = new AutoMapper.MapperConfiguration(c => c.AddProfile(new Application.Mapping.MappingProfile())).CreateMapper();
        var returns = new ReturnService(
            new Repository<SalesReturn>(db),
            new Repository<PurchaseReturn>(db),
            new Repository<SalesInvoice>(db),
            new Repository<PurchaseInvoice>(db),
            new Repository<CreditNoteApplication>(db),
            new Repository<PurchaseCreditNoteApplication>(db),
            new Repository<Supplier>(db),
            new Repository<Customer>(db),
            inventory,
            uow,
            mapper,
            gl,
            company);

        db.SalesInvoices.Add(new SalesInvoice
        {
            InvoiceNumber = "INV-1",
            CustomerId = 1,
            InvoiceDate = new DateTime(2026, 7, 18),
            SubTotal = 100,
            TaxAmount = 18,
            GrandTotal = 118,
            PaymentStatus = PaymentStatus.Pending,
            WarehouseId = 1,
            Lines =
            {
                new SalesInvoiceLine
                {
                    ProductId = 1, ProductName = "P", Sku = "P1", Quantity = 1, UnitPrice = 100,
                    TaxRate = 18, TaxAmount = 18, LineTotal = 118
                }
            }
        });
        await db.SaveChangesAsync();
        var invId = (await db.SalesInvoices.SingleAsync()).Id;

        var cn = await returns.CreateSalesReturnAsync(new Application.DTOs.Sales.SalesReturnCreateDto(
            invId, 1, ReturnType.Partial, "test", 1,
            [new Application.DTOs.Sales.SalesReturnLineDto(1, 1, 100, 18)],
            "DAMAGED",
            StockAffected: false));
        cn.Succeeded.Should().BeTrue(cn.Error);
        cn.Data!.CreditNoteNumber.Should().StartWith("CN-");
        cn.Data.TaxAmount.Should().Be(18);
        cn.Data.GrandTotal.Should().Be(118);

        var apply = await returns.ApplySalesCreditAsync(cn.Data.Id, new Application.DTOs.Sales.ApplyCreditNoteRequest(invId, 50));
        apply.Succeeded.Should().BeTrue(apply.Error);
        var loaded = await db.SalesReturns.SingleAsync();
        loaded.AppliedAmount.Should().Be(50);
        (await db.CreditNoteApplications.CountAsync()).Should().Be(1);
    }
}
