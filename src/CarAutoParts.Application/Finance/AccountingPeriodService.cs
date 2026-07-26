using CarAutoParts.Application.Common;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Finance;

public interface IAccountingPeriodService
{
    Task<Result<AccountingPeriod>> EnsureOpenAsync(DateTime documentDate, CancellationToken ct = default);
}

public sealed class AccountingPeriodService : IAccountingPeriodService
{
    private readonly IEnterpriseDb _db;
    private readonly ICurrentCompanyContext _company;

    public AccountingPeriodService(IEnterpriseDb db, ICurrentCompanyContext company)
    {
        _db = db;
        _company = company;
    }

    public async Task<Result<AccountingPeriod>> EnsureOpenAsync(DateTime documentDate, CancellationToken ct = default)
    {
        if (!_company.CompanyId.HasValue)
            return Result<AccountingPeriod>.Failure("Company context is required.");

        var date = documentDate.Date;
        var period = await _db.AccountingPeriods
            .FirstOrDefaultAsync(p =>
                !p.IsClosed &&
                date >= p.StartDate &&
                date <= p.EndDate, ct);

        if (period is null)
            return Result<AccountingPeriod>.Failure("No open accounting period for document date.");

        return Result<AccountingPeriod>.Success(period);
    }
}
