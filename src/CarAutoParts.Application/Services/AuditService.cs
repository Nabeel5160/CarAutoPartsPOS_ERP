using AutoMapper;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.System;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>Audit log queries.</summary>
public class AuditService : IAuditService
{
    private readonly IRepository<AuditLog> _auditLogs;
    private readonly IMapper _mapper;

    public AuditService(IRepository<AuditLog> auditLogs, IMapper mapper)
    {
        _auditLogs = auditLogs;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(QuerySpec query, CancellationToken ct = default)
    {
        var q = _auditLogs.Query().Where(a => !a.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(a => a.EntityType.Contains(s) || (a.UserName != null && a.UserName.Contains(s)));
        }

        if (query.Filters.TryGetValue("FromDate", out var fromObj) && fromObj is DateTime from)
            q = q.Where(a => a.Timestamp >= from);

        if (query.Filters.TryGetValue("ToDate", out var toObj) && toObj is DateTime to)
            q = q.Where(a => a.Timestamp <= to);

        q = q.OrderByDescending(a => a.Timestamp);
        var paged = await q.ToPagedResultAsync(query.Page, query.PageSize, ct);

        return new PagedResult<AuditLogDto>
        {
            Items = _mapper.Map<List<AuditLogDto>>(paged.Items),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }
}
