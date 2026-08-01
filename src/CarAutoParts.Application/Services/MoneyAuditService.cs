using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using System.Text.Json;

namespace CarAutoParts.Application.Services;

public interface IMoneyAuditService
{
    Task RecordAsync(
        AuditAction action,
        string entityType,
        int? entityId,
        string? summary,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken ct = default);
}

public sealed class MoneyAuditService : IMoneyAuditService
{
    private readonly IRepository<AuditLog> _logs;
    private readonly ICurrentUserService _user;
    private readonly IUnitOfWork _uow;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public MoneyAuditService(IRepository<AuditLog> logs, ICurrentUserService user, IUnitOfWork uow)
    {
        _logs = logs;
        _user = user;
        _uow = uow;
    }

    public async Task RecordAsync(
        AuditAction action,
        string entityType,
        int? entityId,
        string? summary,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken ct = default)
    {
        _logs.Add(new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            UserName = _user.CurrentUser?.Username ?? "system",
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues, JsonOpts),
            NewValues = newValues is null
                ? (summary is null ? null : JsonSerializer.Serialize(new { summary }, JsonOpts))
                : JsonSerializer.Serialize(newValues, JsonOpts),
            Timestamp = DateTime.UtcNow
        });
        await _uow.SaveChangesAsync(ct);
    }
}
