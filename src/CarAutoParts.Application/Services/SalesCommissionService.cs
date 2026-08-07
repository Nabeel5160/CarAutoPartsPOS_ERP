using CarAutoParts.Application.Common;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

public record SalesCommissionDto(
    int Id,
    int SalesInvoiceId,
    string? InvoiceNumber,
    int CustomerId,
    string? CustomerName,
    int? UserId,
    string? UserName,
    decimal CommissionPercent,
    decimal CommissionAmount,
    decimal InvoiceAmount,
    DateTime InvoiceDate);

public interface ISalesCommissionService
{
    /// <summary>Records commission (and salesperson attribution) after invoice post. Idempotent per invoice.</summary>
    Task RecordForInvoiceAsync(SalesInvoice invoice, CancellationToken ct = default);
    Task<IReadOnlyList<SalesCommissionDto>> GetAsync(
        DateTime? from = null,
        DateTime? to = null,
        int? userId = null,
        CancellationToken ct = default);
}

public sealed class SalesCommissionService : ISalesCommissionService
{
    private readonly IEnterpriseDb _db;
    private readonly ICurrentCompanyContext _company;
    private readonly ICurrentUserService _user;

    public SalesCommissionService(
        IEnterpriseDb db,
        ICurrentCompanyContext company,
        ICurrentUserService user)
    {
        _db = db;
        _company = company;
        _user = user;
    }

    public async Task RecordForInvoiceAsync(SalesInvoice invoice, CancellationToken ct = default)
    {
        if (!_company.CompanyId.HasValue || invoice.CustomerId is not int customerId)
            return;

        var exists = await _db.SalesCommissions.AnyAsync(c => c.SalesInvoiceId == invoice.Id && !c.IsDeleted, ct);
        if (exists)
            return;

        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId && !c.IsDeleted, ct);
        if (customer is null)
            return;

        var percent = customer.CommissionPercent;
        var amount = Math.Round(invoice.GrandTotal * percent / 100m, 2);

        // Always record when there is a customer — enables target attainment even at 0% commission.
        _db.SalesCommissions.Add(new SalesCommission
        {
            CompanyId = _company.CompanyId.Value,
            SalesInvoiceId = invoice.Id,
            CustomerId = customerId,
            UserId = _user.CurrentUser?.Id,
            CommissionPercent = percent,
            CommissionAmount = amount,
            InvoiceAmount = invoice.GrandTotal,
            InvoiceDate = invoice.InvoiceDate.Date
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SalesCommissionDto>> GetAsync(
        DateTime? from = null,
        DateTime? to = null,
        int? userId = null,
        CancellationToken ct = default)
    {
        var q = _db.SalesCommissions.AsNoTracking()
            .Include(c => c.SalesInvoice)
            .Include(c => c.Customer)
            .Include(c => c.User)
            .Where(c => !c.IsDeleted);
        if (from is DateTime f) q = q.Where(c => c.InvoiceDate >= f.Date);
        if (to is DateTime t) q = q.Where(c => c.InvoiceDate <= t.Date);
        if (userId is int uid) q = q.Where(c => c.UserId == uid);

        var items = await q.OrderByDescending(c => c.InvoiceDate).ThenByDescending(c => c.Id).ToListAsync(ct);
        return items.Select(c => new SalesCommissionDto(
            c.Id, c.SalesInvoiceId, c.SalesInvoice?.InvoiceNumber, c.CustomerId, c.Customer?.Name,
            c.UserId, c.User?.Username, c.CommissionPercent, c.CommissionAmount, c.InvoiceAmount, c.InvoiceDate)).ToList();
    }
}
