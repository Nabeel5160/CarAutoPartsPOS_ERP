using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarAutoParts.Infrastructure.Data.Seed;

public sealed class EnterprisePlatformSeeder
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<EnterprisePlatformSeeder> _logger;

    public EnterprisePlatformSeeder(ApplicationDbContext db, ILogger<EnterprisePlatformSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await _db.Companies.IgnoreQueryFilters().AnyAsync(ct))
        {
            await BackfillCompanyIdsAsync(ct);
            return;
        }

        _logger.LogInformation("Seeding enterprise platform + finance...");

        var company = new Company
        {
            Code = "CAP",
            Name = "Car Auto Parts",
            LegalName = "Car Auto Parts (Pvt) Ltd",
            Ntn = "0786909",
            City = "Karachi",
            CurrencyCode = "PKR",
            IsActive = true,
            CreatedBy = "system"
        };
        _db.Companies.Add(company);
        await _db.SaveChangesAsync(ct);

        var branch = new Branch
        {
            CompanyId = company.Id,
            Code = "HO",
            Name = "Head Office",
            City = "Karachi",
            IsDefault = true,
            IsActive = true,
            CreatedBy = "system"
        };
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync(ct);

        _db.CostCenters.Add(new CostCenter
        {
            CompanyId = company.Id,
            BranchId = branch.Id,
            Code = "CC-MAIN",
            Name = "Main Operations",
            CreatedBy = "system"
        });

        var yearStart = new DateTime(DateTime.UtcNow.Year, 7, 1);
        if (DateTime.UtcNow.Month < 7) yearStart = yearStart.AddYears(-1);
        var fy = new FiscalYear
        {
            CompanyId = company.Id,
            Name = $"FY{yearStart.Year}-{(yearStart.Year + 1) % 100:00}",
            StartDate = yearStart,
            EndDate = yearStart.AddYears(1).AddDays(-1),
            CreatedBy = "system"
        };
        _db.FiscalYears.Add(fy);
        await _db.SaveChangesAsync(ct);

        for (var i = 0; i < 12; i++)
        {
            var start = yearStart.AddMonths(i);
            _db.AccountingPeriods.Add(new AccountingPeriod
            {
                CompanyId = company.Id,
                FiscalYearId = fy.Id,
                PeriodNumber = i + 1,
                Name = start.ToString("MMM yyyy"),
                StartDate = start,
                EndDate = start.AddMonths(1).AddDays(-1),
                IsClosed = false,
                CreatedBy = "system"
            });
        }

        foreach (var (doc, prefix) in new[]
                 {
                     ("INV", "INV-"), ("PO", "PO-"), ("JV", "JV-"), ("GRN", "GRN-"),
                     ("SO", "SO-"), ("DN", "DN-"), ("QT", "QT-"), ("PI", "PI-"), ("CC", "CC-"),
                     ("REQ", "REQ-")
                 })
        {
            _db.NumberSequences.Add(new NumberSequence
            {
                CompanyId = company.Id,
                DocumentType = doc,
                Prefix = prefix,
                NextValue = 1,
                Padding = 6,
                Gapless = doc is "INV" or "JV",
                CreatedBy = "system"
            });
        }

        SeedChartOfAccounts(company.Id);
        await _db.SaveChangesAsync(ct);
        await BackfillCompanyIdsAsync(ct);
        _logger.LogInformation("Enterprise platform seed completed. CompanyId={CompanyId}", company.Id);
    }

    private void SeedChartOfAccounts(int companyId)
    {
        void Add(string code, string name, AccountType type, bool postable = true) =>
            _db.GlAccounts.Add(new GlAccount
            {
                CompanyId = companyId,
                Code = code,
                Name = name,
                AccountType = type,
                IsPostable = postable,
                IsActive = true,
                CreatedBy = "system"
            });

        Add("1000", "Assets", AccountType.Asset, false);
        Add("1100", "Cash in Hand", AccountType.Asset);
        Add("1110", "Bank", AccountType.Asset);
        Add("1200", "Accounts Receivable", AccountType.Asset);
        Add("1300", "Inventory Asset", AccountType.Asset);
        Add("1400", "GRN Clearing", AccountType.Liability);
        Add("2000", "Liabilities", AccountType.Liability, false);
        Add("2100", "Accounts Payable", AccountType.Liability);
        Add("2200", "Sales Tax Payable", AccountType.Liability);
        Add("3000", "Equity", AccountType.Equity, false);
        Add("3100", "Owner Equity", AccountType.Equity);
        Add("4000", "Revenue", AccountType.Revenue, false);
        Add("4100", "Sales Revenue", AccountType.Revenue);
        Add("5000", "Cost of Goods Sold", AccountType.CostOfGoods, false);
        Add("5100", "COGS", AccountType.CostOfGoods);
        Add("6000", "Expenses", AccountType.Expense, false);
        Add("6100", "Operating Expense", AccountType.Expense);

        // Mappings seeded after save with account IDs — done in second pass below via Save then map
    }

    private async Task BackfillCompanyIdsAsync(CancellationToken ct)
    {
        var companyId = await _db.Companies.IgnoreQueryFilters().Select(c => c.Id).FirstAsync(ct);

        foreach (var w in await _db.Warehouses.IgnoreQueryFilters().Where(x => x.CompanyId == 0).ToListAsync(ct))
            w.CompanyId = companyId;
        foreach (var p in await _db.Products.IgnoreQueryFilters().Where(x => x.CompanyId == 0).ToListAsync(ct))
            p.CompanyId = companyId;
        foreach (var b in await _db.Brands.IgnoreQueryFilters().Where(x => x.CompanyId == 0).ToListAsync(ct))
            b.CompanyId = companyId;
        foreach (var c in await _db.Categories.IgnoreQueryFilters().Where(x => x.CompanyId == 0).ToListAsync(ct))
            c.CompanyId = companyId;

        var branchId = await _db.Branches.IgnoreQueryFilters()
            .Where(b => b.CompanyId == companyId)
            .Select(b => b.Id)
            .FirstAsync(ct);
        foreach (var w in await _db.Warehouses.IgnoreQueryFilters().Where(x => x.BranchId == null).ToListAsync(ct))
            w.BranchId = branchId;

        if (!await _db.GlAccounts.IgnoreQueryFilters().AnyAsync(a => a.CompanyId == companyId && a.Code == "1400", ct))
        {
            _db.GlAccounts.Add(new GlAccount
            {
                CompanyId = companyId,
                Code = "1400",
                Name = "GRN Clearing",
                AccountType = AccountType.Liability,
                IsPostable = true,
                IsActive = true,
                CreatedBy = "system"
            });
            await _db.SaveChangesAsync(ct);
        }

        if (!await _db.NumberSequences.IgnoreQueryFilters()
                .AnyAsync(s => s.CompanyId == companyId && s.DocumentType == "REQ", ct))
        {
            _db.NumberSequences.Add(new NumberSequence
            {
                CompanyId = companyId,
                DocumentType = "REQ",
                Prefix = "REQ-",
                NextValue = 1,
                Padding = 6,
                Gapless = false,
                CreatedBy = "system"
            });
        }

        var accounts = await _db.GlAccounts.IgnoreQueryFilters()
            .Where(a => a.CompanyId == companyId)
            .ToDictionaryAsync(a => a.Code, ct);

        var existingMaps = await _db.AccountMappings.IgnoreQueryFilters()
            .Where(a => a.CompanyId == companyId)
            .Select(a => a.DocumentType + "|" + a.MappingKey)
            .ToListAsync(ct);
        var mapSet = existingMaps.ToHashSet(StringComparer.OrdinalIgnoreCase);

        void EnsureMap(string doc, string key, string code)
        {
            if (!accounts.TryGetValue(code, out var acc)) return;
            var token = doc + "|" + key;
            if (mapSet.Contains(token)) return;
            _db.AccountMappings.Add(new AccountMapping
            {
                CompanyId = companyId,
                DocumentType = doc,
                MappingKey = key,
                AccountId = acc.Id,
                CreatedBy = "system"
            });
            mapSet.Add(token);
        }

        EnsureMap("SalesInvoice", "Receivable", "1200");
        EnsureMap("SalesInvoice", "Revenue", "4100");
        EnsureMap("SalesInvoice", "Tax", "2200");
        EnsureMap("SalesInvoice", "Cogs", "5100");
        EnsureMap("SalesInvoice", "Inventory", "1300");
        EnsureMap("SalesInvoice", "Cash", "1100");
        EnsureMap("SalesInvoice", "Bank", "1110");
        EnsureMap("PurchaseInvoice", "Payable", "2100");
        EnsureMap("PurchaseInvoice", "Inventory", "1300");
        EnsureMap("PurchaseInvoice", "GrnClearing", "1400");
        EnsureMap("Grn", "Inventory", "1300");
        EnsureMap("Grn", "GrnClearing", "1400");
        EnsureMap("Payment", "Cash", "1100");
        EnsureMap("Payment", "Bank", "1110");
        EnsureMap("Payment", "Receivable", "1200");
        EnsureMap("Payment", "Payable", "2100");
        // Sales return reverses sales invoice economics
        EnsureMap("SalesReturn", "Receivable", "1200");
        EnsureMap("SalesReturn", "Revenue", "4100");
        EnsureMap("SalesReturn", "Tax", "2200");
        EnsureMap("SalesReturn", "Cogs", "5100");
        EnsureMap("SalesReturn", "Inventory", "1300");
        EnsureMap("SalesReturn", "Cash", "1100");
        EnsureMap("SalesReturn", "Bank", "1110");
        // Purchase return: reduce inventory + payable (and/or GRN clearing)
        EnsureMap("PurchaseReturn", "Inventory", "1300");
        EnsureMap("PurchaseReturn", "Payable", "2100");
        EnsureMap("PurchaseReturn", "GrnClearing", "1400");

        await _db.SaveChangesAsync(ct);

        // Loud guard: required money-path mappings must exist after seed.
        var required = new (string Doc, string Key)[]
        {
            ("SalesInvoice", "Cash"),
            ("SalesInvoice", "Bank"),
            ("SalesInvoice", "Receivable"),
            ("SalesInvoice", "Revenue"),
            ("SalesInvoice", "Tax"),
            ("SalesInvoice", "Cogs"),
            ("SalesInvoice", "Inventory"),
            ("SalesReturn", "Cash"),
            ("SalesReturn", "Receivable"),
            ("SalesReturn", "Revenue"),
            ("SalesReturn", "Tax"),
            ("SalesReturn", "Cogs"),
            ("SalesReturn", "Inventory"),
            ("Grn", "Inventory"),
            ("Grn", "GrnClearing"),
            ("PurchaseInvoice", "Payable"),
            ("PurchaseInvoice", "GrnClearing"),
            ("PurchaseReturn", "Inventory"),
            ("PurchaseReturn", "Payable"),
            ("Payment", "Cash"),
            ("Payment", "Bank"),
            ("Payment", "Receivable"),
            ("Payment", "Payable"),
        };

        var present = await _db.AccountMappings.IgnoreQueryFilters()
            .Where(a => a.CompanyId == companyId)
            .Select(a => a.DocumentType + "|" + a.MappingKey)
            .ToListAsync(ct);
        var presentSet = present.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = required
            .Where(r => !presentSet.Contains(r.Doc + "|" + r.Key))
            .Select(r => $"{r.Doc}.{r.Key}")
            .ToList();

        if (!await _db.GlAccounts.IgnoreQueryFilters()
                .AnyAsync(a => a.CompanyId == companyId && a.Code == "1400", ct))
            missing.Add("GlAccount.1400");

        if (missing.Count > 0)
        {
            var msg = "Enterprise seed missing required account mappings: " + string.Join(", ", missing);
            _logger.LogError(msg);
            throw new InvalidOperationException(msg);
        }
    }
}
