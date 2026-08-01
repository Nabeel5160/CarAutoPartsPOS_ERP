using AutoMapper;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.System;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
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
            AuditAction? actionFromSearch = null;
            if (Enum.TryParse<AuditAction>(s, ignoreCase: true, out var parsedAction))
                actionFromSearch = parsedAction;
            else if (int.TryParse(s, out var actionInt) && Enum.IsDefined(typeof(AuditAction), actionInt))
                actionFromSearch = (AuditAction)actionInt;

            q = q.Where(a =>
                a.EntityType.Contains(s)
                || (a.UserName != null && a.UserName.Contains(s))
                || (actionFromSearch != null && a.Action == actionFromSearch.Value));
        }

        if (TryGetFilter(query, "Action", out var actionRaw) && actionRaw is not null)
        {
            if (actionRaw is AuditAction ea)
                q = q.Where(a => a.Action == ea);
            else if (actionRaw is int ai && Enum.IsDefined(typeof(AuditAction), ai))
                q = q.Where(a => a.Action == (AuditAction)ai);
            else if (actionRaw is string asStr && Enum.TryParse<AuditAction>(asStr, ignoreCase: true, out var parsed))
                q = q.Where(a => a.Action == parsed);
        }

        if (TryGetFilter(query, "EntityType", out var entityRaw) && entityRaw is string entityType && !string.IsNullOrWhiteSpace(entityType))
        {
            var et = entityType.Trim();
            q = q.Where(a => a.EntityType.Contains(et));
        }

        if (TryGetDateFilter(query, "FromDate", out var from))
            q = q.Where(a => a.Timestamp >= from);

        if (TryGetDateFilter(query, "ToDate", out var to))
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

    private static bool TryGetFilter(QuerySpec query, string key, out object? value)
    {
        value = null;
        foreach (var kv in query.Filters)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kv.Value;
                return value is not null;
            }
        }
        return false;
    }

    private static bool TryGetDateFilter(QuerySpec query, string key, out DateTime date)
    {
        date = default;
        if (!TryGetFilter(query, key, out var raw) || raw is null)
            return false;
        if (raw is DateTime dt)
        {
            date = dt;
            return true;
        }
        if (raw is DateTimeOffset dto)
        {
            date = dto.UtcDateTime;
            return true;
        }
        if (raw is string s && DateTime.TryParse(s, out var parsed))
        {
            date = parsed;
            return true;
        }
        return false;
    }
}
