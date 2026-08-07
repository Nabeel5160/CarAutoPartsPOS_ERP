using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarAutoParts.Application.Services;

public interface ISlaMonitorService
{
    /// <summary>Sweep running timers for warn/breach. Returns number of events raised.</summary>
    Task<int> SweepAsync(CancellationToken ct = default);
}

public sealed class SlaMonitorService : ISlaMonitorService
{
    private readonly IRepository<SlaTimer> _timers;
    private readonly IRepository<SlaPolicy> _policies;
    private readonly IRepository<BusinessCalendar> _calendars;
    private readonly IRepository<ServiceTicket> _tickets;
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notifications;
    private readonly IOpsSlaClockService _opsSla;
    private readonly ISlaClockTime _clock;
    private readonly ILogger<SlaMonitorService> _logger;

    public SlaMonitorService(
        IRepository<SlaTimer> timers,
        IRepository<SlaPolicy> policies,
        IRepository<BusinessCalendar> calendars,
        IRepository<ServiceTicket> tickets,
        IUnitOfWork uow,
        INotificationService notifications,
        IOpsSlaClockService opsSla,
        ISlaClockTime clock,
        ILogger<SlaMonitorService> logger)
    {
        _timers = timers;
        _policies = policies;
        _calendars = calendars;
        _tickets = tickets;
        _uow = uow;
        _notifications = notifications;
        _opsSla = opsSla;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> SweepAsync(CancellationToken ct = default)
    {
        try { await _opsSla.SyncLowStockAsync(ct: ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Ops low-stock SLA sync failed"); }

        var now = _clock.UtcNow;
        var running = await _timers.Query().IgnoreQueryFilters()
            .Where(t => !t.IsDeleted && t.Status == SlaTimerStatus.Running)
            .ToListAsync(ct);

        if (running.Count == 0) return 0;

        var policyIds = running.Select(t => t.SlaPolicyId).Distinct().ToList();
        var policies = await _policies.Query().IgnoreQueryFilters()
            .Where(p => policyIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var companyIds = running.Select(t => t.CompanyId).Distinct().ToList();
        var calendars = await _calendars.Query().IgnoreQueryFilters()
            .Where(c => !c.IsDeleted && companyIds.Contains(c.CompanyId))
            .ToDictionaryAsync(c => c.CompanyId, ct);

        var ticketIds = running
            .Where(t => t.ServiceTicketId is int)
            .Select(t => t.ServiceTicketId!.Value)
            .Distinct()
            .ToList();
        var tickets = ticketIds.Count == 0
            ? new Dictionary<int, ServiceTicket>()
            : await _tickets.Query().IgnoreQueryFilters()
                .Where(t => ticketIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, ct);

        var events = 0;
        foreach (var timer in running)
        {
            policies.TryGetValue(timer.SlaPolicyId, out var policy);
            var mode = policy?.CalendarMode ?? SlaCalendarMode.AlwaysOn;
            calendars.TryGetValue(timer.CompanyId, out var calendar);
            ServiceTicket? ticket = null;
            if (timer.ServiceTicketId is int tid)
                tickets.TryGetValue(tid, out ticket);

            var elapsed = SlaBusinessHoursCalculator.LiveElapsedSeconds(timer, now, mode, calendar);
            timer.ElapsedSeconds = elapsed;

            if (timer.WarnedAt is null && elapsed >= timer.WarnSeconds && timer.WarnSeconds > 0)
            {
                timer.WarnedAt = now;
                timer.Events.Add(new SlaEvent
                {
                    At = now,
                    Kind = SlaEventKind.Warned,
                    Note = $"Elapsed {elapsed}s / warn {timer.WarnSeconds}s",
                    CreatedAt = now,
                    CreatedBy = "sla-monitor"
                });
                await NotifyAsync(NotificationType.Error, "SLA warning", ticket, timer, ct);
                events++;
            }

            if (timer.BreachedAt is null && elapsed >= timer.TargetSeconds)
            {
                timer.Status = SlaTimerStatus.Breached;
                timer.BreachedAt = now;
                timer.ActiveSince = null;
                timer.Events.Add(new SlaEvent
                {
                    At = now,
                    Kind = SlaEventKind.Breached,
                    Note = $"Elapsed {elapsed}s / target {timer.TargetSeconds}s",
                    CreatedAt = now,
                    CreatedBy = "sla-monitor"
                });
                await NotifyAsync(NotificationType.Error, "SLA breached", ticket, timer, ct);
                if (policy?.EscalateToUserId is int escalateTo && ticket is not null &&
                    ticket.AssignedToUserId != escalateTo &&
                    ticket.Status is not ServiceTicketStatus.Resolved and not ServiceTicketStatus.Closed)
                {
                    ticket.AssignedToUserId = escalateTo;
                    ticket.UpdatedAt = now;
                    ticket.UpdatedBy = "sla-monitor";
                    timer.Events.Add(new SlaEvent
                    {
                        At = now,
                        Kind = SlaEventKind.Breached,
                        Note = $"Escalated assignee → user {escalateTo}",
                        CreatedAt = now,
                        CreatedBy = "sla-monitor"
                    });
                    await _notifications.CreateNotificationAsync(
                        NotificationType.Error,
                        "SLA escalated — open ticket",
                        $"SLA breach: '{ticket.Subject}' reassigned to escalatee (user #{escalateTo}). Open the ticket to act.",
                        "ServiceTicket",
                        ticket.Id,
                        ct);
                }
                events++;
            }
            else if (timer.Status == SlaTimerStatus.Running && timer.ActiveSince is not null)
            {
                // Keep ActiveSince; ElapsedSeconds already includes live segment —
                // reset ActiveSince and store frozen elapsed so next sweep doesn't double-count.
                timer.ElapsedSeconds = elapsed;
                timer.ActiveSince = now;
            }
        }

        if (events > 0 || running.Count > 0)
            await _uow.SaveChangesAsync(ct);

        _logger.LogDebug("SLA sweep processed {Count} timers, raised {Events} events", running.Count, events);
        return events;
    }

    private async Task NotifyAsync(NotificationType type, string title, ServiceTicket? ticket, SlaTimer timer, CancellationToken ct)
    {
        var subject = ticket?.Subject
                      ?? (timer.EntityType == SlaEntityType.ServiceTicket
                          ? $"Ticket #{timer.EntityId}"
                          : $"{timer.EntityType} #{timer.EntityId}");
        var metric = timer.Metric == SlaMetric.FirstResponse ? "First response" : "Resolution";
        var entityType = timer.EntityType == SlaEntityType.ServiceTicket ? "ServiceTicket" : timer.EntityType.ToString();
        await _notifications.CreateNotificationAsync(
            type,
            title,
            $"{metric}: {subject}",
            entityType,
            timer.EntityId,
            ct);
    }
}
