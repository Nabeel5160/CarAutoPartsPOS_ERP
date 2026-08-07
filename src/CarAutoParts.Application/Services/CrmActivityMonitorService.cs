using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarAutoParts.Application.Services;

public interface ICrmActivityMonitorService
{
    /// <summary>Warn once for incomplete activities due soon or overdue. Returns notify count.</summary>
    Task<int> SweepAsync(CancellationToken ct = default);
}

/// <summary>Thin CRM DueAt warn — not a second SlaPolicy grid.</summary>
public sealed class CrmActivityMonitorService : ICrmActivityMonitorService
{
    public static readonly TimeSpan WarnWindow = TimeSpan.FromHours(2);

    private readonly IRepository<CrmActivity> _activities;
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notifications;
    private readonly ILogger<CrmActivityMonitorService> _logger;

    public CrmActivityMonitorService(
        IRepository<CrmActivity> activities,
        IUnitOfWork uow,
        INotificationService notifications,
        ILogger<CrmActivityMonitorService> logger)
    {
        _activities = activities;
        _uow = uow;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<int> SweepAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var warnUntil = now.Add(WarnWindow);

        var due = await _activities.Query().IgnoreQueryFilters()
            .Where(a => !a.IsDeleted
                        && a.CompletedAt == null
                        && a.DueAt != null
                        && a.SlaWarnedAt == null
                        && a.DueAt <= warnUntil)
            .OrderBy(a => a.DueAt)
            .Take(100)
            .ToListAsync(ct);

        if (due.Count == 0) return 0;

        var raised = 0;
        foreach (var activity in due)
        {
            var overdue = activity.DueAt!.Value < now;
            var title = overdue ? "CRM task overdue" : "CRM task due soon";
            var when = activity.DueAt.Value.ToString("u");
            await _notifications.CreateNotificationAsync(
                NotificationType.Error,
                title,
                $"{activity.Subject} (due {when})",
                "CrmActivity",
                activity.Id,
                ct);
            activity.SlaWarnedAt = now;
            activity.UpdatedAt = now;
            activity.UpdatedBy = "crm-activity-monitor";
            raised++;
        }

        await _uow.SaveChangesAsync(ct);
        _logger.LogDebug("CRM activity monitor warned {Count} tasks", raised);
        return raised;
    }
}
