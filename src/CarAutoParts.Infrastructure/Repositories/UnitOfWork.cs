using CarAutoParts.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly Data.ApplicationDbContext _db;

    public UnitOfWork(Data.ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        if (!_db.Database.IsRelational())
        {
            await action(ct);
            return;
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            await action(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
