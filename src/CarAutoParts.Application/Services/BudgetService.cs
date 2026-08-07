using CarAutoParts.Application.Common;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

public record BudgetLineDto(
    int Id,
    int GlAccountId,
    string? AccountCode,
    string? AccountName,
    int? CostCenterId,
    string? CostCenterCode,
    int AccountingPeriodId,
    string? PeriodName,
    decimal Amount,
    decimal ActualAmount,
    decimal Variance,
    decimal? VariancePercent);

public record BudgetDto(
    int Id,
    string Name,
    int FiscalYearId,
    string? FiscalYearName,
    string Status,
    string? Notes,
    IReadOnlyList<BudgetLineDto> Lines);

public record BudgetLineUpsertRequest(
    int GlAccountId,
    int AccountingPeriodId,
    decimal Amount,
    int? CostCenterId = null);

public record BudgetCreateRequest(
    string Name,
    int FiscalYearId,
    string? Notes = null,
    IReadOnlyList<BudgetLineUpsertRequest>? Lines = null);

public record BudgetUpdateRequest(
    string Name,
    string Status,
    string? Notes = null);

public interface IBudgetService
{
    Task<IReadOnlyList<BudgetDto>> GetAllAsync(CancellationToken ct = default);
    Task<BudgetDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Result<BudgetDto>> CreateAsync(BudgetCreateRequest request, CancellationToken ct = default);
    Task<Result<BudgetDto>> UpdateAsync(int id, BudgetUpdateRequest request, CancellationToken ct = default);
    Task<Result<BudgetDto>> AddLineAsync(int budgetId, BudgetLineUpsertRequest request, CancellationToken ct = default);
    Task<Result> DeleteLineAsync(int budgetId, int lineId, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}

public sealed class BudgetService : IBudgetService
{
    private readonly IEnterpriseDb _db;
    private readonly ICurrentCompanyContext _company;

    public BudgetService(IEnterpriseDb db, ICurrentCompanyContext company)
    {
        _db = db;
        _company = company;
    }

    public async Task<IReadOnlyList<BudgetDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await _db.Budgets.AsNoTracking()
            .Include(b => b.FiscalYear)
            .Include(b => b.Lines).ThenInclude(l => l.GlAccount)
            .Include(b => b.Lines).ThenInclude(l => l.CostCenter)
            .Include(b => b.Lines).ThenInclude(l => l.AccountingPeriod)
            .Where(b => !b.IsDeleted)
            .OrderByDescending(b => b.Id)
            .ToListAsync(ct);

        var result = new List<BudgetDto>();
        foreach (var b in items)
            result.Add(await MapWithActualsAsync(b, ct));
        return result;
    }

    public async Task<BudgetDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var b = await LoadBudgetAsync(id, ct);
        return b is null ? null : await MapWithActualsAsync(b, ct);
    }

    public async Task<Result<BudgetDto>> CreateAsync(BudgetCreateRequest request, CancellationToken ct = default)
    {
        if (!_company.CompanyId.HasValue)
            return Result<BudgetDto>.Failure("Company context is required.");
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<BudgetDto>.Failure("Name is required.");

        var fy = await _db.FiscalYears.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == request.FiscalYearId && !f.IsDeleted, ct);
        if (fy is null)
            return Result<BudgetDto>.Failure("Fiscal year not found.");

        var entity = new Budget
        {
            CompanyId = _company.CompanyId.Value,
            Name = request.Name.Trim(),
            FiscalYearId = request.FiscalYearId,
            Status = BudgetStatus.Draft,
            Notes = request.Notes
        };

        if (request.Lines is { Count: > 0 })
        {
            foreach (var line in request.Lines)
            {
                var err = await ValidateLineAsync(line, request.FiscalYearId, ct);
                if (err is not null)
                    return Result<BudgetDto>.Failure(err);
                entity.Lines.Add(new BudgetLine
                {
                    CompanyId = _company.CompanyId.Value,
                    GlAccountId = line.GlAccountId,
                    CostCenterId = line.CostCenterId,
                    AccountingPeriodId = line.AccountingPeriodId,
                    Amount = line.Amount
                });
            }
        }

        _db.Budgets.Add(entity);
        await _db.SaveChangesAsync(ct);

        var loaded = await LoadBudgetAsync(entity.Id, ct);
        return Result<BudgetDto>.Success(await MapWithActualsAsync(loaded!, ct));
    }

    public async Task<Result<BudgetDto>> UpdateAsync(int id, BudgetUpdateRequest request, CancellationToken ct = default)
    {
        var entity = await _db.Budgets.FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted, ct);
        if (entity is null)
            return Result<BudgetDto>.Failure("Budget not found.");
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<BudgetDto>.Failure("Name is required.");
        if (!Enum.TryParse<BudgetStatus>(request.Status, ignoreCase: true, out var status))
            return Result<BudgetDto>.Failure("Invalid status. Use Draft, Active, or Closed.");

        entity.Name = request.Name.Trim();
        entity.Status = status;
        entity.Notes = request.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var loaded = await LoadBudgetAsync(id, ct);
        return Result<BudgetDto>.Success(await MapWithActualsAsync(loaded!, ct));
    }

    public async Task<Result<BudgetDto>> AddLineAsync(int budgetId, BudgetLineUpsertRequest request, CancellationToken ct = default)
    {
        var budget = await _db.Budgets.FirstOrDefaultAsync(b => b.Id == budgetId && !b.IsDeleted, ct);
        if (budget is null)
            return Result<BudgetDto>.Failure("Budget not found.");
        if (budget.Status == BudgetStatus.Closed)
            return Result<BudgetDto>.Failure("Cannot add lines to a closed budget.");

        var err = await ValidateLineAsync(request, budget.FiscalYearId, ct);
        if (err is not null)
            return Result<BudgetDto>.Failure(err);

        _db.BudgetLines.Add(new BudgetLine
        {
            CompanyId = budget.CompanyId,
            BudgetId = budgetId,
            GlAccountId = request.GlAccountId,
            CostCenterId = request.CostCenterId,
            AccountingPeriodId = request.AccountingPeriodId,
            Amount = request.Amount
        });
        await _db.SaveChangesAsync(ct);

        var loaded = await LoadBudgetAsync(budgetId, ct);
        return Result<BudgetDto>.Success(await MapWithActualsAsync(loaded!, ct));
    }

    public async Task<Result> DeleteLineAsync(int budgetId, int lineId, CancellationToken ct = default)
    {
        var line = await _db.BudgetLines.FirstOrDefaultAsync(
            l => l.Id == lineId && l.BudgetId == budgetId && !l.IsDeleted, ct);
        if (line is null)
            return Result.Failure("Budget line not found.");

        line.IsDeleted = true;
        line.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.Budgets.Include(b => b.Lines)
            .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted, ct);
        if (entity is null)
            return Result.Failure("Budget not found.");

        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        foreach (var line in entity.Lines.Where(l => !l.IsDeleted))
        {
            line.IsDeleted = true;
            line.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<string?> ValidateLineAsync(BudgetLineUpsertRequest request, int fiscalYearId, CancellationToken ct)
    {
        if (request.Amount < 0)
            return "Budget amount cannot be negative.";
        if (!await _db.GlAccounts.AnyAsync(a => a.Id == request.GlAccountId && !a.IsDeleted, ct))
            return "GL account not found.";
        var period = await _db.AccountingPeriods.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.AccountingPeriodId && !p.IsDeleted, ct);
        if (period is null)
            return "Accounting period not found.";
        if (period.FiscalYearId != fiscalYearId)
            return "Accounting period does not belong to the budget fiscal year.";
        if (request.CostCenterId is int ccId &&
            !await _db.CostCenters.AnyAsync(c => c.Id == ccId && !c.IsDeleted, ct))
            return "Cost center not found.";
        return null;
    }

    private async Task<Budget?> LoadBudgetAsync(int id, CancellationToken ct) =>
        await _db.Budgets.AsNoTracking()
            .Include(b => b.FiscalYear)
            .Include(b => b.Lines.Where(l => !l.IsDeleted)).ThenInclude(l => l.GlAccount)
            .Include(b => b.Lines).ThenInclude(l => l.CostCenter)
            .Include(b => b.Lines).ThenInclude(l => l.AccountingPeriod)
            .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted, ct);

    private async Task<BudgetDto> MapWithActualsAsync(Budget b, CancellationToken ct)
    {
        var lineDtos = new List<BudgetLineDto>();
        foreach (var line in b.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.AccountingPeriodId).ThenBy(l => l.GlAccountId))
        {
            var actual = await SumActualAsync(line, ct);
            var variance = line.Amount - actual;
            decimal? pct = line.Amount == 0 ? null : Math.Round(variance / line.Amount * 100m, 2);
            lineDtos.Add(new BudgetLineDto(
                line.Id,
                line.GlAccountId,
                line.GlAccount?.Code,
                line.GlAccount?.Name,
                line.CostCenterId,
                line.CostCenter?.Code,
                line.AccountingPeriodId,
                line.AccountingPeriod?.Name,
                line.Amount,
                actual,
                variance,
                pct));
        }

        return new BudgetDto(
            b.Id, b.Name, b.FiscalYearId, b.FiscalYear?.Name,
            b.Status.ToString(), b.Notes, lineDtos);
    }

    /// <summary>
    /// Actual = net activity on the account in the period (expense/COGS: Debit−Credit; revenue: Credit−Debit; else Debit−Credit).
    /// </summary>
    private async Task<decimal> SumActualAsync(BudgetLine line, CancellationToken ct)
    {
        var period = line.AccountingPeriod
            ?? await _db.AccountingPeriods.AsNoTracking().FirstAsync(p => p.Id == line.AccountingPeriodId, ct);
        var account = line.GlAccount
            ?? await _db.GlAccounts.AsNoTracking().FirstAsync(a => a.Id == line.GlAccountId, ct);

        var q = _db.JournalLines.AsNoTracking()
            .Where(jl => jl.AccountId == line.GlAccountId
                         && jl.JournalEntry.Status == JournalStatus.Posted
                         && jl.JournalEntry.JournalDate >= period.StartDate
                         && jl.JournalEntry.JournalDate <= period.EndDate);
        if (line.CostCenterId is int cc)
            q = q.Where(jl => jl.CostCenterId == cc);

        var debit = await q.SumAsync(jl => jl.Debit, ct);
        var credit = await q.SumAsync(jl => jl.Credit, ct);

        return account.AccountType is AccountType.Revenue
            ? credit - debit
            : debit - credit;
    }
}
