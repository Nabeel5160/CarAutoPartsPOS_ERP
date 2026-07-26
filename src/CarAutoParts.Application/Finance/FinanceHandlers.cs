using CarAutoParts.Application.Common;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Finance;

public interface IFinanceDb
{
    DbSet<Company> Companies { get; }
    DbSet<Branch> Branches { get; }
    DbSet<GlAccount> GlAccounts { get; }
    DbSet<AccountingPeriod> AccountingPeriods { get; }
    DbSet<FiscalYear> FiscalYears { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<NumberSequence> NumberSequences { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Finance handlers using injected DbContext via abstraction implemented in Infrastructure.</summary>
public sealed class FinanceHandlers :
    IRequestHandler<GetCompaniesQuery, IReadOnlyList<CompanyDto>>,
    IRequestHandler<GetBranchesQuery, IReadOnlyList<BranchDto>>,
    IRequestHandler<GetChartOfAccountsQuery, IReadOnlyList<GlAccountDto>>,
    IRequestHandler<GetOpenPeriodsQuery, IReadOnlyList<AccountingPeriodDto>>,
    IRequestHandler<GetJournalsQuery, PagedResult<JournalDto>>,
    IRequestHandler<CreateGlAccountCommand, Result<GlAccountDto>>,
    IRequestHandler<CreateJournalCommand, Result<JournalDto>>,
    IRequestHandler<PostJournalCommand, Result>,
    IRequestHandler<ClosePeriodCommand, Result>,
    IRequestHandler<ReopenPeriodCommand, Result>,
    IRequestHandler<GetNextDocumentNumberQuery, Result<string>>
{
    private readonly IFinanceDb _db;
    private readonly ICurrentCompanyContext _company;

    public FinanceHandlers(IFinanceDb db, ICurrentCompanyContext company)
    {
        _db = db;
        _company = company;
    }

    public async Task<IReadOnlyList<CompanyDto>> Handle(GetCompaniesQuery request, CancellationToken ct)
    {
        return await _db.Companies.AsNoTracking()
            .OrderBy(c => c.Code)
            .Select(c => new CompanyDto(c.Id, c.Code, c.Name, c.Ntn, c.CurrencyCode, c.IsActive))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BranchDto>> Handle(GetBranchesQuery request, CancellationToken ct)
    {
        return await _db.Branches.AsNoTracking()
            .Where(b => b.CompanyId == request.CompanyId)
            .OrderBy(b => b.Code)
            .Select(b => new BranchDto(b.Id, b.CompanyId, b.Code, b.Name, b.IsDefault, b.IsActive))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<GlAccountDto>> Handle(GetChartOfAccountsQuery request, CancellationToken ct)
    {
        EnsureCompany();
        return await _db.GlAccounts.AsNoTracking()
            .OrderBy(a => a.Code)
            .Select(a => new GlAccountDto(a.Id, a.Code, a.Name, a.AccountType, a.ParentAccountId, a.IsPostable, a.IsActive))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AccountingPeriodDto>> Handle(GetOpenPeriodsQuery request, CancellationToken ct)
    {
        EnsureCompany();
        return await _db.AccountingPeriods.AsNoTracking()
            .OrderByDescending(p => p.StartDate)
            .Select(p => new AccountingPeriodDto(p.Id, p.Name, p.PeriodNumber, p.StartDate, p.EndDate, p.IsClosed, p.FiscalYearId))
            .ToListAsync(ct);
    }

    public async Task<PagedResult<JournalDto>> Handle(GetJournalsQuery request, CancellationToken ct)
    {
        EnsureCompany();
        var q = _db.JournalEntries.AsNoTracking().Include(j => j.Lines).OrderByDescending(j => j.JournalDate);
        var total = await q.CountAsync(ct);
        var items = await q.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        return new PagedResult<JournalDto>
        {
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = total,
            Items = items.Select(MapJournal).ToList()
        };
    }

    public async Task<Result<GlAccountDto>> Handle(CreateGlAccountCommand request, CancellationToken ct)
    {
        EnsureCompany();
        var entity = new GlAccount
        {
            CompanyId = _company.CompanyId!.Value,
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            AccountType = request.AccountType,
            ParentAccountId = request.ParentAccountId,
            IsPostable = request.IsPostable,
            IsActive = true
        };
        _db.GlAccounts.Add(entity);
        await _db.SaveChangesAsync(ct);
        return Result<GlAccountDto>.Success(new GlAccountDto(entity.Id, entity.Code, entity.Name, entity.AccountType, entity.ParentAccountId, entity.IsPostable, entity.IsActive));
    }

    public async Task<Result<JournalDto>> Handle(CreateJournalCommand request, CancellationToken ct)
    {
        EnsureCompany();
        var companyId = _company.CompanyId!.Value;
        var number = await AllocateNumberAsync("JV", ct);
        var journal = new JournalEntry
        {
            CompanyId = companyId,
            JournalNumber = number,
            JournalDate = request.JournalDate.Date,
            Reference = request.Reference,
            Description = request.Description,
            Status = JournalStatus.Draft
        };
        foreach (var line in request.Lines)
        {
            journal.Lines.Add(new JournalLine
            {
                CompanyId = companyId,
                AccountId = line.AccountId,
                Description = line.Description,
                Debit = line.Debit,
                Credit = line.Credit,
                CostCenterId = line.CostCenterId
            });
        }

        try { journal.EnsureBalanced(); }
        catch (Exception ex) { return Result<JournalDto>.Failure(ex.Message); }

        _db.JournalEntries.Add(journal);
        await _db.SaveChangesAsync(ct);
        return Result<JournalDto>.Success(MapJournal(journal));
    }

    public async Task<Result> Handle(PostJournalCommand request, CancellationToken ct)
    {
        EnsureCompany();
        var journal = await _db.JournalEntries.Include(j => j.Lines)
            .FirstOrDefaultAsync(j => j.Id == request.JournalId, ct);
        if (journal is null) return Result.Failure("Journal not found.");

        var period = await _db.AccountingPeriods
            .FirstOrDefaultAsync(p =>
                !p.IsClosed &&
                journal.JournalDate >= p.StartDate &&
                journal.JournalDate <= p.EndDate, ct);
        if (period is null) return Result.Failure("No open accounting period for journal date.");

        try
        {
            journal.Post(period);
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> Handle(ClosePeriodCommand request, CancellationToken ct)
    {
        EnsureCompany();
        var period = await _db.AccountingPeriods.FirstOrDefaultAsync(p => p.Id == request.PeriodId, ct);
        if (period is null) return Result.Failure("Period not found.");
        period.IsClosed = true;
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> Handle(ReopenPeriodCommand request, CancellationToken ct)
    {
        EnsureCompany();
        var period = await _db.AccountingPeriods.FirstOrDefaultAsync(p => p.Id == request.PeriodId, ct);
        if (period is null) return Result.Failure("Period not found.");
        period.IsClosed = false;
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<string>> Handle(GetNextDocumentNumberQuery request, CancellationToken ct)
    {
        EnsureCompany();
        var value = await AllocateNumberAsync(request.DocumentType, ct);
        await _db.SaveChangesAsync(ct);
        return Result<string>.Success(value);
    }

    private async Task<string> AllocateNumberAsync(string documentType, CancellationToken ct)
    {
        var seq = await _db.NumberSequences
            .FirstOrDefaultAsync(s => s.DocumentType == documentType, ct)
            ?? throw new InvalidOperationException($"Number sequence '{documentType}' not configured.");
        return seq.AllocateNext();
    }

    private void EnsureCompany()
    {
        if (!_company.CompanyId.HasValue)
            throw new InvalidOperationException("Company context is required.");
    }

    private static JournalDto MapJournal(JournalEntry j) => new(
        j.Id, j.JournalNumber, j.JournalDate, j.Status, j.Reference, j.Description,
        j.TotalDebit, j.TotalCredit,
        j.Lines.Select(l => new JournalLineDto(l.AccountId, l.Description, l.Debit, l.Credit, l.CostCenterId)).ToList());
}
