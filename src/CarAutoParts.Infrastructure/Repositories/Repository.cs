using System.Linq.Expressions;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly Data.ApplicationDbContext Db;
    protected readonly DbSet<T> Set;

    public Repository(Data.ApplicationDbContext db)
    {
        Db = db;
        Set = db.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
        => await Set.FindAsync([id], ct);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await Set.AsNoTracking().ToListAsync(ct);

    public virtual async Task<PagedResult<T>> GetPagedAsync(QuerySpec spec, CancellationToken ct = default)
    {
        var query = Set.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(spec.Search))
        {
            query = ApplySearch(query, spec.Search);
        }

        query = ApplySort(query, spec);

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, spec.Page);
        var pageSize = Math.Clamp(spec.PageSize, 1, QueryLimits.MaxPageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<T>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await Set.AnyAsync(predicate, ct);

    public virtual void Add(T entity) => Set.Add(entity);

    public virtual void Update(T entity) => Set.Update(entity);

    public virtual void Remove(T entity)
    {
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        Set.Update(entity);
    }

    public virtual IQueryable<T> Query() => Set.AsQueryable();

    protected virtual IQueryable<T> ApplySearch(IQueryable<T> query, string search) => query;

    protected virtual IQueryable<T> ApplySort(IQueryable<T> query, QuerySpec spec)
    {
        if (string.IsNullOrWhiteSpace(spec.SortBy))
            return query.OrderByDescending(e => e.Id);

        return spec.SortDescending
            ? query.OrderByDescending(e => EF.Property<object>(e, spec.SortBy))
            : query.OrderBy(e => EF.Property<object>(e, spec.SortBy));
    }
}
