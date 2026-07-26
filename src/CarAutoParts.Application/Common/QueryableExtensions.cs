using CarAutoParts.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Common;

/// <summary>EF Core query helpers for paged results.</summary>
public static class QueryableExtensions
{
    /// <summary>Executes a paged query and returns a <see cref="PagedResult{T}"/>.</summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var total = await query.CountAsync(ct);
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
}
