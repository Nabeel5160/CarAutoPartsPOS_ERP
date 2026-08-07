using CarAutoParts.Application.Common;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

public record SalesTargetDto(
    int Id,
    int UserId,
    string? UserName,
    int PeriodYear,
    int PeriodMonth,
    decimal TargetAmount,
    string? Notes,
    decimal ActualAmount = 0,
    decimal AttainmentPercent = 0);

public record SalesTargetUpsertRequest(
    int UserId,
    int PeriodYear,
    int PeriodMonth,
    decimal TargetAmount,
    string? Notes = null);

/// <summary>Thin CRUD for monthly salesperson targets (Program B — sales thin; C2 adds attainment).</summary>
public interface ISalesTargetService
{
    Task<IReadOnlyList<SalesTargetDto>> GetAllAsync(int? userId = null, int? year = null, CancellationToken ct = default);
    Task<Result<SalesTargetDto>> CreateAsync(SalesTargetUpsertRequest request, CancellationToken ct = default);
    Task<Result<SalesTargetDto>> UpdateAsync(int id, SalesTargetUpsertRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}

public sealed class SalesTargetService : ISalesTargetService
{
    private readonly IEnterpriseDb _db;
    private readonly ICurrentCompanyContext _company;

    public SalesTargetService(IEnterpriseDb db, ICurrentCompanyContext company)
    {
        _db = db;
        _company = company;
    }

    public async Task<IReadOnlyList<SalesTargetDto>> GetAllAsync(
        int? userId = null,
        int? year = null,
        CancellationToken ct = default)
    {
        var q = _db.SalesTargets.AsNoTracking().Include(t => t.User).Where(t => !t.IsDeleted);
        if (userId is int uid) q = q.Where(t => t.UserId == uid);
        if (year is int y) q = q.Where(t => t.PeriodYear == y);

        var items = await q
            .OrderByDescending(t => t.PeriodYear).ThenByDescending(t => t.PeriodMonth).ThenBy(t => t.UserId)
            .ToListAsync(ct);

        var result = new List<SalesTargetDto>();
        foreach (var t in items)
            result.Add(await MapWithAttainmentAsync(t, ct));
        return result;
    }

    public async Task<Result<SalesTargetDto>> CreateAsync(SalesTargetUpsertRequest request, CancellationToken ct = default)
    {
        if (!_company.CompanyId.HasValue)
            return Result<SalesTargetDto>.Failure("Company context is required.");

        var validation = Validate(request);
        if (validation is not null)
            return Result<SalesTargetDto>.Failure(validation);

        if (!await _db.Users.AnyAsync(u => u.Id == request.UserId && !u.IsDeleted, ct))
            return Result<SalesTargetDto>.Failure("User not found.");

        var exists = await _db.SalesTargets.AnyAsync(
            t => !t.IsDeleted && t.UserId == request.UserId
                 && t.PeriodYear == request.PeriodYear && t.PeriodMonth == request.PeriodMonth, ct);
        if (exists)
            return Result<SalesTargetDto>.Failure("A target already exists for this user and period.");

        var entity = new SalesTarget
        {
            CompanyId = _company.CompanyId.Value,
            UserId = request.UserId,
            PeriodYear = request.PeriodYear,
            PeriodMonth = request.PeriodMonth,
            TargetAmount = request.TargetAmount,
            Notes = request.Notes
        };
        _db.SalesTargets.Add(entity);
        await _db.SaveChangesAsync(ct);

        var loaded = await _db.SalesTargets.AsNoTracking().Include(t => t.User).FirstAsync(t => t.Id == entity.Id, ct);
        return Result<SalesTargetDto>.Success(await MapWithAttainmentAsync(loaded, ct));
    }

    public async Task<Result<SalesTargetDto>> UpdateAsync(int id, SalesTargetUpsertRequest request, CancellationToken ct = default)
    {
        var validation = Validate(request);
        if (validation is not null)
            return Result<SalesTargetDto>.Failure(validation);

        var entity = await _db.SalesTargets.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);
        if (entity is null)
            return Result<SalesTargetDto>.Failure("Sales target not found.");

        if (!await _db.Users.AnyAsync(u => u.Id == request.UserId && !u.IsDeleted, ct))
            return Result<SalesTargetDto>.Failure("User not found.");

        var duplicate = await _db.SalesTargets.AnyAsync(
            t => !t.IsDeleted && t.Id != id && t.UserId == request.UserId
                 && t.PeriodYear == request.PeriodYear && t.PeriodMonth == request.PeriodMonth, ct);
        if (duplicate)
            return Result<SalesTargetDto>.Failure("A target already exists for this user and period.");

        entity.UserId = request.UserId;
        entity.PeriodYear = request.PeriodYear;
        entity.PeriodMonth = request.PeriodMonth;
        entity.TargetAmount = request.TargetAmount;
        entity.Notes = request.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var loaded = await _db.SalesTargets.AsNoTracking().Include(t => t.User).FirstAsync(t => t.Id == id, ct);
        return Result<SalesTargetDto>.Success(await MapWithAttainmentAsync(loaded, ct));
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.SalesTargets.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);
        if (entity is null)
            return Result.Failure("Sales target not found.");

        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<SalesTargetDto> MapWithAttainmentAsync(SalesTarget t, CancellationToken ct)
    {
        var start = new DateTime(t.PeriodYear, t.PeriodMonth, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var actual = await _db.SalesCommissions.AsNoTracking()
            .Where(c => !c.IsDeleted && c.UserId == t.UserId
                        && c.InvoiceDate >= start && c.InvoiceDate <= end)
            .SumAsync(c => (decimal?)c.InvoiceAmount, ct) ?? 0m;
        var pct = t.TargetAmount == 0 ? 0m : Math.Round(actual / t.TargetAmount * 100m, 2);
        return new SalesTargetDto(
            t.Id, t.UserId, t.User?.Username, t.PeriodYear, t.PeriodMonth, t.TargetAmount, t.Notes, actual, pct);
    }

    private static string? Validate(SalesTargetUpsertRequest request)
    {
        if (request.PeriodMonth is < 1 or > 12)
            return "Period month must be between 1 and 12.";
        if (request.PeriodYear < 2000 || request.PeriodYear > 2100)
            return "Period year is out of range.";
        if (request.TargetAmount < 0)
            return "Target amount cannot be negative.";
        return null;
    }
}
