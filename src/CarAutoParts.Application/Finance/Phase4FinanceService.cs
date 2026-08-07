using CarAutoParts.Application.Common;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Finance;

public interface IPhase4FinanceService
{
    Task<Result<OpeningBalanceBatchDto>> PostOpeningBalancesAsync(PostOpeningBalancesRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<OpeningBalanceBatchDto>> GetOpeningBalanceBatchesAsync(CancellationToken ct = default);
    Task<PeriodCloseChecklistDto> GetPeriodCloseChecklistAsync(int periodId, CancellationToken ct = default);
    Task<Result> ClosePeriodWithChecklistAsync(int periodId, bool forceClose, CancellationToken ct = default);
    Task<Result<BankStatementDto>> CreateBankStatementAsync(CreateBankStatementRequest request, CancellationToken ct = default);
    Task<Result<BankStatementDto>> AddBankStatementLineAsync(int statementId, CreateBankStatementLineRequest request, CancellationToken ct = default);
    Task<Result> MatchBankLineAsync(int statementLineId, int journalLineId, CancellationToken ct = default);
    Task<Result> UnclearBankLineAsync(int statementLineId, CancellationToken ct = default);
    /// <summary>
    /// Suggest matches using amount ± tolerance, date window, optional reference contains.
    /// Thresholds are hardcoded (no rules entity yet — Program C2).
    /// </summary>
    Task<IReadOnlyList<BankMatchSuggestionDto>> SuggestBankMatchesAsync(
        int statementId,
        decimal amountTolerance = 0.01m,
        int dateWindowDays = 3,
        CancellationToken ct = default);
    /// <summary>Auto-clears each suggestion with a unique best journal line (score ≥ 50).</summary>
    Task<Result<BankAutoMatchResultDto>> AutoMatchBankAsync(
        int statementId,
        decimal amountTolerance = 0.01m,
        int dateWindowDays = 3,
        CancellationToken ct = default);
    Task<Result<BankReconReportDto>> GetBankReconReportAsync(int statementId, CancellationToken ct = default);
    Task<IReadOnlyList<BankStatementDto>> GetBankStatementsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<UnclearedBankGlLineDto>> GetUnclearedBankGlLinesAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
}

public record OpeningBalanceGlLineRequest(int AccountId, decimal Debit, decimal Credit, string? Description);
public record OpeningBalanceInventoryLineRequest(int ProductId, int WarehouseId, decimal Quantity, decimal UnitCost);
public record OpeningBalanceArLineRequest(int CustomerId, decimal Amount);
public record OpeningBalanceApLineRequest(int SupplierId, decimal Amount);

public record PostOpeningBalancesRequest(
    DateTime CutoverDate,
    string? Notes,
    IReadOnlyList<OpeningBalanceGlLineRequest>? GlLines,
    IReadOnlyList<OpeningBalanceInventoryLineRequest>? InventoryLines,
    IReadOnlyList<OpeningBalanceArLineRequest>? ArLines,
    IReadOnlyList<OpeningBalanceApLineRequest>? ApLines);

public record OpeningBalanceLineDto(
    int Id,
    string LineType,
    int? AccountId,
    int? ProductId,
    int? WarehouseId,
    decimal? Quantity,
    decimal? UnitCost,
    int? CustomerId,
    int? SupplierId,
    decimal Debit,
    decimal Credit,
    string? Description);

public record OpeningBalanceBatchDto(
    int Id,
    string BatchNumber,
    DateTime CutoverDate,
    string Status,
    int? JournalEntryId,
    string? Notes,
    IReadOnlyList<OpeningBalanceLineDto> Lines);

public record PeriodCloseChecklistItemDto(string Code, string Label, int Count, bool IsBlocker, string Severity);

public record PeriodCloseChecklistDto(
    int PeriodId,
    string PeriodName,
    bool CanClose,
    bool RequiresForceClose,
    IReadOnlyList<PeriodCloseChecklistItemDto> Items);

public record CreateBankStatementRequest(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal OpeningBalance,
    decimal ClosingBalance,
    string? Notes);

public record CreateBankStatementLineRequest(
    DateTime LineDate,
    decimal Amount,
    string? Reference,
    string? Description);

public record BankStatementLineDto(
    int Id,
    DateTime LineDate,
    decimal Amount,
    string? Reference,
    string? Description,
    bool IsCleared,
    int? MatchedJournalLineId);

public record BankStatementDto(
    int Id,
    string StatementNumber,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal OpeningBalance,
    decimal ClosingBalance,
    string Status,
    string? Notes,
    IReadOnlyList<BankStatementLineDto> Lines);

public record UnclearedBankGlLineDto(
    int JournalLineId,
    int JournalEntryId,
    string JournalNumber,
    DateTime JournalDate,
    decimal Debit,
    decimal Credit,
    string? Description);

/// <summary>Suggested bank line ↔ GL line pair with a 0–100 score.</summary>
public record BankMatchSuggestionDto(
    int StatementLineId,
    int JournalLineId,
    string JournalNumber,
    DateTime JournalDate,
    decimal StatementAmount,
    decimal GlNetAmount,
    int Score,
    string Reason);

public record BankAutoMatchResultDto(int MatchedCount, int SkippedCount, IReadOnlyList<BankMatchSuggestionDto> Applied);

public record BankReconReportDto(
    int StatementId,
    string StatementNumber,
    decimal StatementClosingBalance,
    decimal GlBankBalance,
    decimal UnclearedStatementTotal,
    decimal UnclearedGlTotal,
    decimal Difference,
    IReadOnlyList<BankStatementLineDto> UnclearedStatementLines,
    IReadOnlyList<UnclearedBankGlLineDto> UnclearedGlLines);

public sealed class Phase4FinanceService : IPhase4FinanceService
{
    private const string BankAccountCode = "1110";
    private const string InventoryCode = "1300";
    private const string ReceivableCode = "1200";
    private const string PayableCode = "2100";
    private const string EquityCode = "3100";

    private readonly IEnterpriseDb _db;
    private readonly ICurrentCompanyContext _company;
    private readonly IInventoryService _inventory;
    private readonly IAccountingPeriodService _periods;
    private readonly IFinancialReportService _reports;
    private readonly ICurrentUserService _user;
    private readonly IApprovalWorkflowService _approvals;

    public Phase4FinanceService(
        IEnterpriseDb db,
        ICurrentCompanyContext company,
        IInventoryService inventory,
        IAccountingPeriodService periods,
        IFinancialReportService reports,
        ICurrentUserService user,
        IApprovalWorkflowService approvals)
    {
        _db = db;
        _company = company;
        _inventory = inventory;
        _periods = periods;
        _reports = reports;
        _user = user;
        _approvals = approvals;
    }

    public async Task<Result<OpeningBalanceBatchDto>> PostOpeningBalancesAsync(
        PostOpeningBalancesRequest request,
        CancellationToken ct = default)
    {
        if (!_company.CompanyId.HasValue)
            return Result<OpeningBalanceBatchDto>.Failure("Company context is required.");

        var companyId = _company.CompanyId.Value;
        var cutover = request.CutoverDate.Date;
        var periodResult = await _periods.EnsureOpenAsync(cutover, ct);
        if (!periodResult.Succeeded)
            return Result<OpeningBalanceBatchDto>.Failure(periodResult.Error ?? "No open period.");

        var accounts = await _db.GlAccounts.AsNoTracking()
            .Where(a => a.IsActive && !a.IsDeleted)
            .ToDictionaryAsync(a => a.Code, ct);

        if (!accounts.TryGetValue(EquityCode, out var equity)
            || !accounts.TryGetValue(InventoryCode, out var inventory)
            || !accounts.TryGetValue(ReceivableCode, out var receivable)
            || !accounts.TryGetValue(PayableCode, out var payable))
            return Result<OpeningBalanceBatchDto>.Failure("Required GL accounts 1200/1300/2100/3100 missing.");

        var batch = new OpeningBalanceBatch
        {
            CompanyId = companyId,
            BatchNumber = await AllocateAsync("OB", ct),
            CutoverDate = cutover,
            Status = OpeningBalanceStatus.Draft,
            Notes = request.Notes
        };

        var journal = new JournalEntry
        {
            CompanyId = companyId,
            JournalNumber = await AllocateAsync("JV", ct),
            JournalDate = cutover,
            Reference = batch.BatchNumber,
            Description = $"Opening balances {batch.BatchNumber}",
            Status = JournalStatus.Draft,
            SourceDocumentType = "OpeningBalance",
            SourceDocumentId = null
        };

        void AddGl(int accountId, decimal debit, decimal credit, string? desc)
        {
            if (debit == 0 && credit == 0) return;
            journal.Lines.Add(new JournalLine
            {
                CompanyId = companyId,
                AccountId = accountId,
                Debit = debit,
                Credit = credit,
                Description = desc
            });
        }

        foreach (var gl in request.GlLines ?? [])
        {
            if (gl.Debit < 0 || gl.Credit < 0)
                return Result<OpeningBalanceBatchDto>.Failure("GL line amounts cannot be negative.");
            AddGl(gl.AccountId, gl.Debit, gl.Credit, gl.Description);
            batch.Lines.Add(new OpeningBalanceLine
            {
                CompanyId = companyId,
                LineType = OpeningBalanceLineType.Gl,
                AccountId = gl.AccountId,
                Debit = gl.Debit,
                Credit = gl.Credit,
                Description = gl.Description
            });
        }

        foreach (var inv in request.InventoryLines ?? [])
        {
            if (inv.Quantity <= 0 || inv.UnitCost < 0)
                return Result<OpeningBalanceBatchDto>.Failure("Inventory OB requires positive qty and non-negative cost.");
            var amount = Math.Round(inv.Quantity * inv.UnitCost, 2);
            AddGl(inventory.Id, amount, 0, $"Inv OB product {inv.ProductId}");
            AddGl(equity.Id, 0, amount, "Equity offset inventory OB");
            batch.Lines.Add(new OpeningBalanceLine
            {
                CompanyId = companyId,
                LineType = OpeningBalanceLineType.Inventory,
                ProductId = inv.ProductId,
                WarehouseId = inv.WarehouseId,
                Quantity = inv.Quantity,
                UnitCost = inv.UnitCost,
                AccountId = inventory.Id,
                Debit = amount,
                Credit = 0,
                Description = $"Inventory OB"
            });
        }

        foreach (var ar in request.ArLines ?? [])
        {
            if (ar.Amount <= 0)
                return Result<OpeningBalanceBatchDto>.Failure("AR opening amount must be positive.");
            AddGl(receivable.Id, ar.Amount, 0, $"AR OB customer {ar.CustomerId}");
            AddGl(equity.Id, 0, ar.Amount, "Equity offset AR OB");
            batch.Lines.Add(new OpeningBalanceLine
            {
                CompanyId = companyId,
                LineType = OpeningBalanceLineType.Ar,
                CustomerId = ar.CustomerId,
                AccountId = receivable.Id,
                Debit = ar.Amount,
                Credit = 0,
                Description = "AR opening"
            });
        }

        foreach (var ap in request.ApLines ?? [])
        {
            if (ap.Amount <= 0)
                return Result<OpeningBalanceBatchDto>.Failure("AP opening amount must be positive.");
            AddGl(equity.Id, ap.Amount, 0, "Equity offset AP OB");
            AddGl(payable.Id, 0, ap.Amount, $"AP OB supplier {ap.SupplierId}");
            batch.Lines.Add(new OpeningBalanceLine
            {
                CompanyId = companyId,
                LineType = OpeningBalanceLineType.Ap,
                SupplierId = ap.SupplierId,
                AccountId = payable.Id,
                Debit = 0,
                Credit = ap.Amount,
                Description = "AP opening"
            });
        }

        if (journal.Lines.Count < 2)
            return Result<OpeningBalanceBatchDto>.Failure("Opening balance must produce at least two journal lines.");

        try { journal.EnsureBalanced(); }
        catch (Exception ex) { return Result<OpeningBalanceBatchDto>.Failure(ex.Message); }

        try { journal.Post(periodResult.Data!); }
        catch (Exception ex) { return Result<OpeningBalanceBatchDto>.Failure(ex.Message); }

        _db.JournalEntries.Add(journal);
        await _db.SaveChangesAsync(ct);

        batch.JournalEntryId = journal.Id;
        batch.Status = OpeningBalanceStatus.Posted;
        journal.SourceDocumentId = null;
        _db.OpeningBalanceBatches.Add(batch);
        await _db.SaveChangesAsync(ct);
        journal.SourceDocumentId = batch.Id;

        foreach (var inv in request.InventoryLines ?? [])
        {
            var recv = await _inventory.ReceiveStockAsync(
                inv.ProductId, inv.WarehouseId, inv.Quantity, inv.UnitCost, null, ct);
            if (!recv.Succeeded)
                return Result<OpeningBalanceBatchDto>.Failure(recv.Error ?? "Inventory receive failed.");
        }

        foreach (var ar in request.ArLines ?? [])
        {
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == ar.CustomerId && !c.IsDeleted, ct);
            if (customer is null)
                return Result<OpeningBalanceBatchDto>.Failure($"Customer {ar.CustomerId} not found.");
            customer.Balance += ar.Amount;
        }

        foreach (var ap in request.ApLines ?? [])
        {
            var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == ap.SupplierId && !s.IsDeleted, ct);
            if (supplier is null)
                return Result<OpeningBalanceBatchDto>.Failure($"Supplier {ap.SupplierId} not found.");
            supplier.Balance += ap.Amount;
        }

        var settings = await _db.CompanySettings.FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        if (settings is not null)
            settings.OpeningBalanceDate = cutover;

        await _db.SaveChangesAsync(ct);

        return Result<OpeningBalanceBatchDto>.Success(MapBatch(batch));
    }

    public async Task<IReadOnlyList<OpeningBalanceBatchDto>> GetOpeningBalanceBatchesAsync(CancellationToken ct = default)
    {
        var items = await _db.OpeningBalanceBatches.AsNoTracking()
            .Include(b => b.Lines)
            .OrderByDescending(b => b.CutoverDate)
            .ToListAsync(ct);
        return items.Select(MapBatch).ToList();
    }

    public async Task<PeriodCloseChecklistDto> GetPeriodCloseChecklistAsync(int periodId, CancellationToken ct = default)
    {
        var period = await _db.AccountingPeriods.AsNoTracking().FirstOrDefaultAsync(p => p.Id == periodId, ct)
            ?? throw new InvalidOperationException("Period not found.");

        var draftJournals = await _db.JournalEntries.CountAsync(
            j => j.Status == JournalStatus.Draft
                 && j.JournalDate >= period.StartDate
                 && j.JournalDate <= period.EndDate, ct);

        var tb = await _reports.TrialBalanceAsync(period.EndDate, ct: ct);
        var tbImbalance = !tb.Succeeded
            || Math.Abs(tb.Data!.TotalDebit - tb.Data.TotalCredit) > 0.01m;

        var openGrn = await _db.GoodsReceiptNotes.CountAsync(
            g => g.Status == GrnStatus.Draft || g.Status == GrnStatus.QcHold, ct);

        var openAp = await _db.PurchaseInvoices.CountAsync(
            i => i.Status == PurchaseInvoiceStatus.Draft, ct);

        var fbrFailed = await _db.FbrSubmissions.CountAsync(
            f => f.Status == FbrSubmissionStatus.Failed || f.Status == FbrSubmissionStatus.Pending, ct);

        var unclearedBank = await _db.BankStatementLines.CountAsync(
            l => !l.IsCleared
                 && l.LineDate >= period.StartDate
                 && l.LineDate <= period.EndDate, ct);

        var items = new List<PeriodCloseChecklistItemDto>
        {
            new("draft_journals", "Draft journals in period", draftJournals, IsBlocker: true, draftJournals > 0 ? "Block" : "Ok"),
            new("tb_imbalance", "Trial balance imbalance", tbImbalance ? 1 : 0, IsBlocker: true, tbImbalance ? "Block" : "Ok"),
            new("open_grn", "Open GRN drafts / QC hold", openGrn, IsBlocker: false, openGrn > 0 ? "Warn" : "Ok"),
            new("open_ap", "Draft purchase invoices", openAp, IsBlocker: false, openAp > 0 ? "Warn" : "Ok"),
            new("fbr_backlog", "FBR failed/pending", fbrFailed, IsBlocker: false, fbrFailed > 0 ? "Warn" : "Ok"),
            new("uncleared_bank", "Uncleared bank statement lines", unclearedBank, IsBlocker: false, unclearedBank > 0 ? "Warn" : "Ok"),
        };

        var blockers = items.Where(i => i.IsBlocker && i.Count > 0).ToList();
        return new PeriodCloseChecklistDto(
            period.Id,
            period.Name,
            CanClose: blockers.Count == 0,
            RequiresForceClose: blockers.Count > 0,
            items);
    }

    public async Task<Result> ClosePeriodWithChecklistAsync(int periodId, bool forceClose, CancellationToken ct = default)
    {
        var checklist = await GetPeriodCloseChecklistAsync(periodId, ct);
        var blockers = checklist.Items.Where(i => i.IsBlocker && i.Count > 0).ToList();
        if (blockers.Count > 0)
        {
            if (!forceClose)
                return Result.Failure($"Close blocked: {string.Join("; ", blockers.Select(b => $"{b.Label}={b.Count}"))}. Use force-close with permission.");
            if (!_user.HasPermission(Permissions.FinanceForceClose))
                return Result.Failure("finance.force-close permission required to override blockers.");
        }
        else
        {
            var warnings = checklist.Items.Where(i => !i.IsBlocker && i.Count > 0).ToList();
            if (warnings.Count > 0 && forceClose && !_user.HasPermission(Permissions.FinanceForceClose)
                && !_user.HasPermission(Permissions.FinancePost))
            {
                // force not required for warnings when FinancePost; allow close
            }
        }

        var period = await _db.AccountingPeriods.FirstOrDefaultAsync(p => p.Id == periodId, ct);
        if (period is null) return Result.Failure("Period not found.");
        if (period.IsClosed) return Result.Failure("Period already closed.");

        var gate = await _approvals.EnsureApprovedOrQueueAsync(
            "PeriodClose", period.Id, period.Name, 0, ct);
        if (!gate.Succeeded)
            return gate;

        period.IsClosed = true;
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<BankStatementDto>> CreateBankStatementAsync(
        CreateBankStatementRequest request,
        CancellationToken ct = default)
    {
        if (!_company.CompanyId.HasValue)
            return Result<BankStatementDto>.Failure("Company context is required.");

        var entity = new BankStatement
        {
            CompanyId = _company.CompanyId.Value,
            StatementNumber = await AllocateAsync("BS", ct),
            PeriodStart = request.PeriodStart.Date,
            PeriodEnd = request.PeriodEnd.Date,
            OpeningBalance = request.OpeningBalance,
            ClosingBalance = request.ClosingBalance,
            Notes = request.Notes,
            Status = BankStatementStatus.Draft
        };
        _db.BankStatements.Add(entity);
        await _db.SaveChangesAsync(ct);
        return Result<BankStatementDto>.Success(MapStatement(entity));
    }

    public async Task<Result<BankStatementDto>> AddBankStatementLineAsync(
        int statementId,
        CreateBankStatementLineRequest request,
        CancellationToken ct = default)
    {
        var stmt = await _db.BankStatements.Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.Id == statementId, ct);
        if (stmt is null) return Result<BankStatementDto>.Failure("Statement not found.");
        if (stmt.Status == BankStatementStatus.Finalized)
            return Result<BankStatementDto>.Failure("Statement is finalized.");

        stmt.Lines.Add(new BankStatementLine
        {
            CompanyId = stmt.CompanyId,
            LineDate = request.LineDate.Date,
            Amount = request.Amount,
            Reference = request.Reference,
            Description = request.Description
        });
        await _db.SaveChangesAsync(ct);
        return Result<BankStatementDto>.Success(MapStatement(stmt));
    }

    public async Task<Result> MatchBankLineAsync(int statementLineId, int journalLineId, CancellationToken ct = default)
    {
        var line = await _db.BankStatementLines.FirstOrDefaultAsync(l => l.Id == statementLineId, ct);
        if (line is null) return Result.Failure("Statement line not found.");
        var jl = await _db.JournalLines.Include(j => j.JournalEntry)
            .FirstOrDefaultAsync(j => j.Id == journalLineId, ct);
        if (jl is null) return Result.Failure("Journal line not found.");
        if (jl.JournalEntry.Status != JournalStatus.Posted)
            return Result.Failure("Matched journal must be posted.");

        var bank = await _db.GlAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Code == BankAccountCode, ct);
        if (bank is null || jl.AccountId != bank.Id)
            return Result.Failure("Journal line must be on GL 1110 Bank.");

        line.MatchedJournalLineId = journalLineId;
        line.IsCleared = true;
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> UnclearBankLineAsync(int statementLineId, CancellationToken ct = default)
    {
        if (!_user.HasPermission(Permissions.FinanceManage) && !_user.HasPermission(Permissions.FinanceForceClose))
            return Result.Failure("Permission required to unclear bank lines.");

        var line = await _db.BankStatementLines.FirstOrDefaultAsync(l => l.Id == statementLineId, ct);
        if (line is null) return Result.Failure("Statement line not found.");
        line.IsCleared = false;
        line.MatchedJournalLineId = null;
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<IReadOnlyList<BankMatchSuggestionDto>> SuggestBankMatchesAsync(
        int statementId,
        decimal amountTolerance = 0.01m,
        int dateWindowDays = 3,
        CancellationToken ct = default)
    {
        // Hardcoded scoring (Program C2): amount match +40, date within window +30, reference contains +30.
        var stmt = await _db.BankStatements.AsNoTracking().Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.Id == statementId, ct);
        if (stmt is null) return [];

        var bank = await _db.GlAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Code == BankAccountCode, ct);
        if (bank is null) return [];

        var clearedJlIds = stmt.Lines.Where(l => l.MatchedJournalLineId.HasValue)
            .Select(l => l.MatchedJournalLineId!.Value).ToHashSet();
        var from = stmt.PeriodStart.AddDays(-dateWindowDays);
        var to = stmt.PeriodEnd.AddDays(dateWindowDays);
        var unclearedGl = await GetUnclearedBankGlLinesCoreAsync(bank.Id, from, to, clearedJlIds, ct);
        var usedJl = new HashSet<int>();
        var suggestions = new List<BankMatchSuggestionDto>();

        foreach (var line in stmt.Lines.Where(l => !l.IsCleared).OrderBy(l => l.LineDate))
        {
            BankMatchSuggestionDto? best = null;
            foreach (var gl in unclearedGl.Where(g => !usedJl.Contains(g.JournalLineId)))
            {
                var glNet = gl.Debit - gl.Credit;
                var score = 0;
                var reasons = new List<string>();

                if (Math.Abs(glNet - line.Amount) <= amountTolerance)
                {
                    score += 40;
                    reasons.Add("amount");
                }
                else
                    continue;

                if (Math.Abs((gl.JournalDate.Date - line.LineDate.Date).TotalDays) <= dateWindowDays)
                {
                    score += 30;
                    reasons.Add("date");
                }

                if (!string.IsNullOrWhiteSpace(line.Reference) &&
                    ((!string.IsNullOrWhiteSpace(gl.Description) &&
                      gl.Description.Contains(line.Reference, StringComparison.OrdinalIgnoreCase)) ||
                     gl.JournalNumber.Contains(line.Reference, StringComparison.OrdinalIgnoreCase)))
                {
                    score += 30;
                    reasons.Add("ref");
                }

                if (best is null || score > best.Score)
                {
                    best = new BankMatchSuggestionDto(
                        line.Id, gl.JournalLineId, gl.JournalNumber, gl.JournalDate,
                        line.Amount, glNet, score, string.Join("+", reasons));
                }
            }

            if (best is not null)
            {
                usedJl.Add(best.JournalLineId);
                suggestions.Add(best);
            }
        }

        return suggestions.OrderByDescending(s => s.Score).ToList();
    }

    public async Task<Result<BankAutoMatchResultDto>> AutoMatchBankAsync(
        int statementId,
        decimal amountTolerance = 0.01m,
        int dateWindowDays = 3,
        CancellationToken ct = default)
    {
        var suggestions = await SuggestBankMatchesAsync(statementId, amountTolerance, dateWindowDays, ct);
        var applied = new List<BankMatchSuggestionDto>();
        var skipped = 0;
        foreach (var s in suggestions)
        {
            if (s.Score < 50)
            {
                skipped++;
                continue;
            }

            var match = await MatchBankLineAsync(s.StatementLineId, s.JournalLineId, ct);
            if (match.Succeeded) applied.Add(s);
            else skipped++;
        }

        return Result<BankAutoMatchResultDto>.Success(new BankAutoMatchResultDto(applied.Count, skipped, applied));
    }

    public async Task<Result<BankReconReportDto>> GetBankReconReportAsync(int statementId, CancellationToken ct = default)
    {
        var stmt = await _db.BankStatements.AsNoTracking().Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.Id == statementId, ct);
        if (stmt is null) return Result<BankReconReportDto>.Failure("Statement not found.");

        var bank = await _db.GlAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Code == BankAccountCode, ct);
        if (bank is null) return Result<BankReconReportDto>.Failure("Bank account 1110 not found.");

        var glBalance = await _db.JournalLines
            .Where(l => l.AccountId == bank.Id && l.JournalEntry.Status == JournalStatus.Posted
                        && l.JournalEntry.JournalDate <= stmt.PeriodEnd)
            .SumAsync(l => l.Debit - l.Credit, ct);

        var unclearedStmt = stmt.Lines.Where(l => !l.IsCleared).ToList();
        var clearedJlIds = stmt.Lines.Where(l => l.MatchedJournalLineId.HasValue)
            .Select(l => l.MatchedJournalLineId!.Value).ToHashSet();

        var unclearedGl = await GetUnclearedBankGlLinesCoreAsync(bank.Id, stmt.PeriodStart, stmt.PeriodEnd, clearedJlIds, ct);
        var unclearedStmtTotal = unclearedStmt.Sum(l => l.Amount);
        var unclearedGlTotal = unclearedGl.Sum(l => l.Debit - l.Credit);
        var difference = stmt.ClosingBalance - (glBalance - unclearedGlTotal + unclearedStmtTotal);

        return Result<BankReconReportDto>.Success(new BankReconReportDto(
            stmt.Id,
            stmt.StatementNumber,
            stmt.ClosingBalance,
            glBalance,
            unclearedStmtTotal,
            unclearedGlTotal,
            difference,
            unclearedStmt.Select(MapStmtLine).ToList(),
            unclearedGl));
    }

    public async Task<IReadOnlyList<BankStatementDto>> GetBankStatementsAsync(CancellationToken ct = default)
    {
        var items = await _db.BankStatements.AsNoTracking()
            .Include(s => s.Lines)
            .OrderByDescending(s => s.PeriodEnd)
            .ToListAsync(ct);
        return items.Select(MapStatement).ToList();
    }

    public async Task<IReadOnlyList<UnclearedBankGlLineDto>> GetUnclearedBankGlLinesAsync(
        DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var bank = await _db.GlAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Code == BankAccountCode, ct);
        if (bank is null) return [];
        var cleared = await _db.BankStatementLines.AsNoTracking()
            .Where(l => l.MatchedJournalLineId != null)
            .Select(l => l.MatchedJournalLineId!.Value)
            .ToListAsync(ct);
        return await GetUnclearedBankGlLinesCoreAsync(
            bank.Id,
            from ?? DateTime.MinValue,
            to ?? DateTime.UtcNow.Date,
            cleared.ToHashSet(),
            ct);
    }

    private async Task<IReadOnlyList<UnclearedBankGlLineDto>> GetUnclearedBankGlLinesCoreAsync(
        int bankAccountId,
        DateTime from,
        DateTime to,
        HashSet<int> clearedJournalLineIds,
        CancellationToken ct)
    {
        var lines = await _db.JournalLines.AsNoTracking()
            .Include(l => l.JournalEntry)
            .Where(l => l.AccountId == bankAccountId
                        && l.JournalEntry.Status == JournalStatus.Posted
                        && l.JournalEntry.JournalDate >= from
                        && l.JournalEntry.JournalDate <= to)
            .ToListAsync(ct);

        return lines
            .Where(l => !clearedJournalLineIds.Contains(l.Id))
            .Select(l => new UnclearedBankGlLineDto(
                l.Id, l.JournalEntryId, l.JournalEntry.JournalNumber, l.JournalEntry.JournalDate,
                l.Debit, l.Credit, l.Description))
            .ToList();
    }

    private async Task<string> AllocateAsync(string documentType, CancellationToken ct)
    {
        var seq = await _db.NumberSequences.FirstOrDefaultAsync(s => s.DocumentType == documentType, ct);
        if (seq is null)
        {
            seq = new NumberSequence
            {
                CompanyId = _company.CompanyId!.Value,
                DocumentType = documentType,
                Prefix = documentType + "-",
                NextValue = 1,
                Padding = 5,
                Gapless = true
            };
            _db.NumberSequences.Add(seq);
            await _db.SaveChangesAsync(ct);
        }
        return seq.AllocateNext();
    }

    private static OpeningBalanceBatchDto MapBatch(OpeningBalanceBatch b) => new(
        b.Id, b.BatchNumber, b.CutoverDate, b.Status.ToString(), b.JournalEntryId, b.Notes,
        b.Lines.Select(l => new OpeningBalanceLineDto(
            l.Id, l.LineType.ToString(), l.AccountId, l.ProductId, l.WarehouseId,
            l.Quantity, l.UnitCost, l.CustomerId, l.SupplierId, l.Debit, l.Credit, l.Description)).ToList());

    private static BankStatementDto MapStatement(BankStatement s) => new(
        s.Id, s.StatementNumber, s.PeriodStart, s.PeriodEnd, s.OpeningBalance, s.ClosingBalance,
        s.Status.ToString(), s.Notes, s.Lines.Select(MapStmtLine).ToList());

    private static BankStatementLineDto MapStmtLine(BankStatementLine l) =>
        new(l.Id, l.LineDate, l.Amount, l.Reference, l.Description, l.IsCleared, l.MatchedJournalLineId);
}
