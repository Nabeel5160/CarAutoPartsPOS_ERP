using CarAutoParts.Application.Common;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Enterprise;

public sealed class FinancialReportService : IFinancialReportService
{
    private readonly IEnterpriseDb _db;
    private readonly ICurrentCompanyContext _company;

    public FinancialReportService(IEnterpriseDb db, ICurrentCompanyContext company)
    {
        _db = db;
        _company = company;
    }

    public async Task<Result<TrialBalanceReportDto>> TrialBalanceAsync(
        DateTime asOfDate,
        int? branchId = null,
        CancellationToken ct = default)
    {
        if (!EnsureCompany(out _, out var error))
            return Result<TrialBalanceReportDto>.Failure(error!);

        if (branchId is int bid && !_company.IsBranchAllowed(bid))
            return Result<TrialBalanceReportDto>.Failure("Branch is not allowed for this user.");

        var lines = await GetPostedLinesAsync(null, asOfDate, branchId, ct);
        var accounts = await _db.GlAccounts.AsNoTracking().ToDictionaryAsync(a => a.Id, ct);

        var grouped = lines
            .GroupBy(l => l.AccountId)
            .Select(g =>
            {
                accounts.TryGetValue(g.Key, out var account);
                var debit = g.Sum(l => l.Debit);
                var credit = g.Sum(l => l.Credit);
                return new TrialBalanceLineDto(
                    g.Key,
                    account?.Code ?? string.Empty,
                    account?.Name ?? string.Empty,
                    account?.AccountType ?? AccountType.Asset,
                    debit,
                    credit,
                    debit - credit);
            })
            .Where(l => l.Debit != 0 || l.Credit != 0)
            .OrderBy(l => l.Code)
            .ToList();

        return Result<TrialBalanceReportDto>.Success(new TrialBalanceReportDto(
            asOfDate,
            grouped.Sum(l => l.Debit),
            grouped.Sum(l => l.Credit),
            grouped));
    }

    public async Task<Result<ProfitAndLossReportDto>> ProfitAndLossAsync(
        DateTime fromDate,
        DateTime toDate,
        int? branchId = null,
        CancellationToken ct = default)
    {
        if (!EnsureCompany(out _, out var error))
            return Result<ProfitAndLossReportDto>.Failure(error!);

        if (branchId is int bid && !_company.IsBranchAllowed(bid))
            return Result<ProfitAndLossReportDto>.Failure("Branch is not allowed for this user.");

        var lines = await GetPostedLinesAsync(fromDate, toDate, branchId, ct);
        var accounts = await _db.GlAccounts.AsNoTracking().ToDictionaryAsync(a => a.Id, ct);

        var plTypes = new[] { AccountType.Revenue, AccountType.Expense, AccountType.CostOfGoods };
        var grouped = lines
            .Where(l => accounts.TryGetValue(l.AccountId, out var a) && plTypes.Contains(a.AccountType))
            .GroupBy(l => l.AccountId)
            .Select(g =>
            {
                var account = accounts[g.Key];
                var net = g.Sum(l => l.Credit - l.Debit);
                if (account.AccountType is AccountType.Expense or AccountType.CostOfGoods)
                    net = g.Sum(l => l.Debit - l.Credit);

                return new ProfitAndLossLineDto(g.Key, account.Code, account.Name, account.AccountType, net);
            })
            .Where(l => l.Amount != 0)
            .OrderBy(l => l.Code)
            .ToList();

        var revenue = grouped.Where(l => l.AccountType == AccountType.Revenue).Sum(l => l.Amount);
        var expenses = grouped.Where(l => l.AccountType is AccountType.Expense or AccountType.CostOfGoods).Sum(l => l.Amount);

        return Result<ProfitAndLossReportDto>.Success(new ProfitAndLossReportDto(
            fromDate,
            toDate,
            revenue,
            expenses,
            revenue - expenses,
            grouped));
    }

    public async Task<Result<BalanceSheetReportDto>> BalanceSheetAsync(DateTime asOfDate, CancellationToken ct = default)
    {
        if (!EnsureCompany(out _, out var error))
            return Result<BalanceSheetReportDto>.Failure(error!);

        var lines = await GetPostedLinesAsync(null, asOfDate, null, ct);
        var accounts = await _db.GlAccounts.AsNoTracking().ToDictionaryAsync(a => a.Id, ct);

        var bsTypes = new[] { AccountType.Asset, AccountType.Liability, AccountType.Equity };
        var grouped = lines
            .Where(l => accounts.TryGetValue(l.AccountId, out var a) && bsTypes.Contains(a.AccountType))
            .GroupBy(l => l.AccountId)
            .Select(g =>
            {
                var account = accounts[g.Key];
                var balance = account.AccountType == AccountType.Asset
                    ? g.Sum(l => l.Debit - l.Credit)
                    : g.Sum(l => l.Credit - l.Debit);

                return new BalanceSheetLineDto(g.Key, account.Code, account.Name, account.AccountType, balance);
            })
            .Where(l => l.Balance != 0)
            .OrderBy(l => l.Code)
            .ToList();

        return Result<BalanceSheetReportDto>.Success(new BalanceSheetReportDto(
            asOfDate,
            grouped.Where(l => l.AccountType == AccountType.Asset).Sum(l => l.Balance),
            grouped.Where(l => l.AccountType == AccountType.Liability).Sum(l => l.Balance),
            grouped.Where(l => l.AccountType == AccountType.Equity).Sum(l => l.Balance),
            grouped));
    }

    public async Task<Result<PartnerAgingReportDto>> CustomerAgingAsync(
        DateTime? asOfDate = null,
        CancellationToken ct = default)
    {
        if (!EnsureCompany(out _, out var error))
            return Result<PartnerAgingReportDto>.Failure(error!);

        var asOf = (asOfDate ?? DateTime.UtcNow).Date;
        var lines = new Dictionary<int, (string Name, decimal Current, decimal Days30, decimal Days60, decimal Days90)>();

        var invoices = await _db.SalesInvoices
            .AsNoTracking()
            .Include(i => i.Payments)
            .Include(i => i.Customer)
            .Where(i => i.PaymentStatus != PaymentStatus.Paid)
            .ToListAsync(ct);

        foreach (var invoice in invoices)
        {
            if (invoice.CustomerId is not int customerId)
                continue;

            var unpaid = invoice.GrandTotal - invoice.Payments.Sum(p => p.Amount);
            if (unpaid <= 0)
                continue;

            if (!lines.TryGetValue(customerId, out var line))
            {
                line = (invoice.Customer?.Name ?? $"Customer {customerId}", 0, 0, 0, 0);
            }

            AddToBucket(ref line, invoice.InvoiceDate, asOf, unpaid);
            lines[customerId] = line;
        }

        var customersWithBalance = await _db.Customers
            .AsNoTracking()
            .Where(c => !c.IsDeleted && c.Balance > 0)
            .ToListAsync(ct);

        foreach (var customer in customersWithBalance)
        {
            if (lines.ContainsKey(customer.Id))
                continue;

            lines[customer.Id] = (customer.Name, customer.Balance, 0, 0, 0);
        }

        return Result<PartnerAgingReportDto>.Success(BuildAgingReport(asOf, lines));
    }

    public async Task<Result<PartnerAgingReportDto>> SupplierAgingAsync(
        DateTime? asOfDate = null,
        CancellationToken ct = default)
    {
        if (!EnsureCompany(out _, out var error))
            return Result<PartnerAgingReportDto>.Failure(error!);

        var asOf = (asOfDate ?? DateTime.UtcNow).Date;
        var lines = new Dictionary<int, (string Name, decimal Current, decimal Days30, decimal Days60, decimal Days90)>();

        var invoices = await _db.PurchaseInvoices
            .AsNoTracking()
            .Include(i => i.Supplier)
            .Where(i => i.Status == PurchaseInvoiceStatus.Posted)
            .ToListAsync(ct);

        foreach (var invoice in invoices)
        {
            if (!lines.TryGetValue(invoice.SupplierId, out var line))
            {
                line = (invoice.Supplier?.Name ?? $"Supplier {invoice.SupplierId}", 0, 0, 0, 0);
            }

            AddToBucket(ref line, invoice.InvoiceDate, asOf, invoice.GrandTotal);
            lines[invoice.SupplierId] = line;
        }

        var suppliersWithBalance = await _db.Suppliers
            .AsNoTracking()
            .Where(s => !s.IsDeleted && s.Balance > 0)
            .ToListAsync(ct);

        foreach (var supplier in suppliersWithBalance)
        {
            if (lines.ContainsKey(supplier.Id))
                continue;

            lines[supplier.Id] = (supplier.Name, supplier.Balance, 0, 0, 0);
        }

        return Result<PartnerAgingReportDto>.Success(BuildAgingReport(asOf, lines));
    }

    public async Task<Result<CashFlowReportDto>> CashFlowAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken ct = default)
    {
        if (!EnsureCompany(out _, out var error))
            return Result<CashFlowReportDto>.Failure(error!);

        var from = fromDate.Date;
        var to = toDate.Date;

        var accounts = await _db.GlAccounts.AsNoTracking().ToDictionaryAsync(a => a.Id, ct);
        var cashCodes = new[] { "1100", "1110" };
        var cashAccountIds = accounts.Values.Where(a => cashCodes.Contains(a.Code)).Select(a => a.Id).ToHashSet();
        if (cashAccountIds.Count == 0)
            return Result<CashFlowReportDto>.Failure("Cash/Bank GL accounts (1100/1110) are not configured.");

        var openingCash = await _db.JournalLines.AsNoTracking()
            .Where(l => cashAccountIds.Contains(l.AccountId)
                        && l.JournalEntry.Status == JournalStatus.Posted
                        && l.JournalEntry.JournalDate < from)
            .SumAsync(l => l.Debit - l.Credit, ct);

        var periodLines = await _db.JournalLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.Status == JournalStatus.Posted
                        && l.JournalEntry.JournalDate >= from
                        && l.JournalEntry.JournalDate <= to)
            .ToListAsync(ct);

        decimal operating = 0, investing = 0, financing = 0;
        var lines = new List<CashFlowLineDto>();

        foreach (var grp in periodLines.GroupBy(l => l.JournalEntryId))
        {
            var cashLines = grp.Where(l => cashAccountIds.Contains(l.AccountId)).ToList();
            if (cashLines.Count == 0)
                continue;

            var netCash = cashLines.Sum(l => l.Debit - l.Credit);
            if (netCash == 0)
                continue;

            var counterparts = grp.Where(l => !cashAccountIds.Contains(l.AccountId)).ToList();
            var category = ClassifyCashFlow(counterparts, accounts);

            switch (category)
            {
                case "Financing": financing += netCash; break;
                case "Investing": investing += netCash; break;
                default: operating += netCash; break;
            }

            var je = grp.First().JournalEntry;
            lines.Add(new CashFlowLineDto(je.Id, je.JournalNumber, je.JournalDate, category, netCash, je.Description));
        }

        var netChange = operating + investing + financing;

        return Result<CashFlowReportDto>.Success(new CashFlowReportDto(
            from,
            to,
            openingCash,
            operating,
            investing,
            financing,
            netChange,
            openingCash + netChange,
            lines.OrderBy(l => l.JournalDate).ThenBy(l => l.JournalNumber).ToList()));
    }

    private static string ClassifyCashFlow(List<JournalLine> counterparts, Dictionary<int, GlAccount> accounts)
    {
        if (counterparts.Any(l => accounts.TryGetValue(l.AccountId, out var a) && a.AccountType == AccountType.Equity))
            return "Financing";

        if (counterparts.Any(l => accounts.TryGetValue(l.AccountId, out var a) && IsFixedAssetCode(a.Code)))
            return "Investing";

        return "Operating";
    }

    private static bool IsFixedAssetCode(string code) =>
        code.Length >= 2 && code[0] == '1' && code[1] is >= '5' and <= '9';

    private static void AddToBucket(
        ref (string Name, decimal Current, decimal Days30, decimal Days60, decimal Days90) line,
        DateTime invoiceDate,
        DateTime asOf,
        decimal amount)
    {
        var days = (asOf - invoiceDate.Date).Days;
        if (days <= 30)
            line.Current += amount;
        else if (days <= 60)
            line.Days30 += amount;
        else if (days <= 90)
            line.Days60 += amount;
        else
            line.Days90 += amount;
    }

    private static PartnerAgingReportDto BuildAgingReport(
        DateTime asOf,
        Dictionary<int, (string Name, decimal Current, decimal Days30, decimal Days60, decimal Days90)> lines)
    {
        var reportLines = lines
            .Select(kvp =>
            {
                var total = kvp.Value.Current + kvp.Value.Days30 + kvp.Value.Days60 + kvp.Value.Days90;
                return new PartnerAgingLineDto(
                    kvp.Key,
                    kvp.Value.Name,
                    kvp.Value.Current,
                    kvp.Value.Days30,
                    kvp.Value.Days60,
                    kvp.Value.Days90,
                    total);
            })
            .Where(l => l.Total > 0)
            .OrderBy(l => l.Name)
            .ToList();

        return new PartnerAgingReportDto(asOf, reportLines);
    }

    private async Task<List<JournalLine>> GetPostedLinesAsync(
        DateTime? fromDate,
        DateTime toDate,
        int? branchId,
        CancellationToken ct)
    {
        var q = _db.JournalLines
            .AsNoTracking()
            .Include(l => l.JournalEntry)
            .Include(l => l.CostCenter)
            .Where(l => l.JournalEntry.Status == JournalStatus.Posted &&
                        l.JournalEntry.JournalDate <= toDate.Date);

        if (fromDate.HasValue)
            q = q.Where(l => l.JournalEntry.JournalDate >= fromDate.Value.Date);

        if (branchId.HasValue)
            q = q.Where(l => l.CostCenter != null && l.CostCenter.BranchId == branchId.Value);

        return await q.ToListAsync(ct);
    }

    private bool EnsureCompany(out int companyId, out string? error)
    {
        if (_company.CompanyId.HasValue)
        {
            companyId = _company.CompanyId.Value;
            error = null;
            return true;
        }

        companyId = 0;
        error = "Company context is required.";
        return false;
    }
}
