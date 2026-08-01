using CarAutoParts.Application.Finance;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Infrastructure.Data;

/// <summary>Adapts ApplicationDbContext to finance MediatR handlers.</summary>
public sealed class FinanceDbAdapter : IFinanceDb
{
    private readonly ApplicationDbContext _db;

    public FinanceDbAdapter(ApplicationDbContext db) => _db = db;

    public DbSet<Company> Companies => _db.Companies;
    public DbSet<Branch> Branches => _db.Branches;
    public DbSet<GlAccount> GlAccounts => _db.GlAccounts;
    public DbSet<AccountingPeriod> AccountingPeriods => _db.AccountingPeriods;
    public DbSet<FiscalYear> FiscalYears => _db.FiscalYears;
    public DbSet<JournalEntry> JournalEntries => _db.JournalEntries;
    public DbSet<NumberSequence> NumberSequences => _db.NumberSequences;
    public DbSet<CompanySettings> CompanySettings => _db.CompanySettings;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
