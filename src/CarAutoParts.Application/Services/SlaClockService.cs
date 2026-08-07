using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Service;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarAutoParts.Application.Services;

public interface ISlaClockService
{
    Task OnTicketCreatedAsync(ServiceTicket ticket, int? policyOverrideId = null, CancellationToken ct = default);
    Task OnTicketStatusChangedAsync(ServiceTicket ticket, ServiceTicketStatus from, ServiceTicketStatus to, CancellationToken ct = default);
    Task<SlaTicketSummaryDto?> GetTicketSlaAsync(int ticketId, CancellationToken ct = default);
    Task<Result> PauseAsync(int ticketId, SlaPauseDto dto, CancellationToken ct = default);
    Task<Result> ResumeAsync(int ticketId, CancellationToken ct = default);
}

public sealed class SlaClockService : ISlaClockService
{
    private readonly IRepository<SlaPolicy> _policies;
    private readonly IRepository<SlaPolicyRule> _rules;
    private readonly IRepository<SlaTimer> _timers;
    private readonly IRepository<SlaEvent> _events;
    private readonly IRepository<ServiceTicket> _tickets;
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<BusinessCalendar> _calendars;
    private readonly ISlaPolicyService _policyService;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentCompanyContext _company;
    private readonly ISlaClockTime _clock;
    private readonly ILogger<SlaClockService> _logger;

    public SlaClockService(
        IRepository<SlaPolicy> policies,
        IRepository<SlaPolicyRule> rules,
        IRepository<SlaTimer> timers,
        IRepository<SlaEvent> events,
        IRepository<ServiceTicket> tickets,
        IRepository<Customer> customers,
        IRepository<BusinessCalendar> calendars,
        ISlaPolicyService policyService,
        IUnitOfWork uow,
        ICurrentCompanyContext company,
        ISlaClockTime clock,
        ILogger<SlaClockService> logger)
    {
        _policies = policies;
        _rules = rules;
        _timers = timers;
        _events = events;
        _tickets = tickets;
        _customers = customers;
        _calendars = calendars;
        _policyService = policyService;
        _uow = uow;
        _company = company;
        _clock = clock;
        _logger = logger;
    }

    public async Task OnTicketCreatedAsync(ServiceTicket ticket, int? policyOverrideId = null, CancellationToken ct = default)
    {
        await _policyService.EnsureDefaultPolicyAsync(ticket.CompanyId, ct);

        var active = await _policies.Query()
            .Include(p => p.Targets)
            .Where(p => p.IsActive && p.AppliesToEntityType == SlaEntityType.ServiceTicket)
            .ToListAsync(ct);

        SlaPolicy? policy = null;
        if (policyOverrideId is int overrideId)
        {
            policy = active.FirstOrDefault(p => p.Id == overrideId);
            if (policy is null)
            {
                _logger.LogWarning("SLA policy override {PolicyId} not found/active; falling back to routing", overrideId);
            }
        }

        if (policy is null)
            policy = await ResolvePolicyByRulesAsync(ticket, active, ct);

        if (policy is null)
        {
            _logger.LogWarning("No SLA policy for company {CompanyId}; skipping timers for ticket {TicketId}", ticket.CompanyId, ticket.Id);
            return;
        }

        ticket.SlaPolicyId = policy.Id;
        var now = _clock.UtcNow;

        foreach (var metric in new[] { SlaMetric.FirstResponse, SlaMetric.Resolution })
        {
            var target = policy.Targets.FirstOrDefault(t => t.Metric == metric && t.Priority == ticket.Priority);
            if (target is null)
            {
                _logger.LogWarning("No SLA target for {Metric}/{Priority} on policy {PolicyId}", metric, ticket.Priority, policy.Id);
                continue;
            }

            var targetSeconds = target.TargetMinutes * 60;
            var warnSeconds = (int)Math.Floor(targetSeconds * (target.WarnAtPercent / 100.0));
            var timer = new SlaTimer
            {
                CompanyId = ticket.CompanyId,
                ServiceTicketId = ticket.Id,
                EntityType = SlaEntityType.ServiceTicket,
                EntityId = ticket.Id,
                Metric = metric,
                SlaPolicyId = policy.Id,
                SlaTargetId = target.Id,
                Status = SlaTimerStatus.Running,
                StartedAt = now,
                ActiveSince = now,
                ElapsedSeconds = 0,
                TargetSeconds = targetSeconds,
                WarnSeconds = warnSeconds,
                CreatedAt = now,
                CreatedBy = "system"
            };
            _timers.Add(timer);
            _events.Add(new SlaEvent
            {
                SlaTimer = timer,
                At = now,
                Kind = SlaEventKind.Started,
                CreatedAt = now,
                CreatedBy = "system"
            });
        }

        await _uow.SaveChangesAsync(ct);
    }

    private async Task<SlaPolicy?> ResolvePolicyByRulesAsync(ServiceTicket ticket, List<SlaPolicy> active, CancellationToken ct)
    {
        CustomerType? customerType = null;
        var customer = await _customers.Query().AsNoTracking().FirstOrDefaultAsync(c => c.Id == ticket.CustomerId, ct);
        if (customer is not null) customerType = customer.CustomerType;

        var policyIds = active.Select(p => p.Id).ToHashSet();
        var rules = await _rules.Query().AsNoTracking()
            .Where(r => r.IsActive && policyIds.Contains(r.SlaPolicyId))
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Id)
            .ToListAsync(ct);

        foreach (var rule in rules)
        {
            if (rule.Priority is ServiceTicketPriority rp && rp != ticket.Priority) continue;
            if (rule.CustomerType is CustomerType ctMatch && (customerType is null || ctMatch != customerType)) continue;
            if (rule.CustomerId is int cid && cid != ticket.CustomerId) continue;
            if (rule.IsWarrantyClaim is bool war && war != ticket.IsWarrantyClaim) continue;
            var matched = active.FirstOrDefault(p => p.Id == rule.SlaPolicyId);
            if (matched is not null) return matched;
        }

        if (ticket.IsWarrantyClaim)
        {
            return active.FirstOrDefault(p => p.ApplyToWarrantyOnly)
                   ?? active.FirstOrDefault(p => p.IsDefault && !p.ApplyToWarrantyOnly)
                   ?? active.FirstOrDefault(p => !p.ApplyToWarrantyOnly)
                   ?? active.FirstOrDefault();
        }

        return active.FirstOrDefault(p => p.IsDefault && !p.ApplyToWarrantyOnly)
               ?? active.FirstOrDefault(p => !p.ApplyToWarrantyOnly)
               ?? active.FirstOrDefault(p => p.IsDefault);
    }

    public async Task OnTicketStatusChangedAsync(ServiceTicket ticket, ServiceTicketStatus from, ServiceTicketStatus to, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var calendar = await GetCalendarAsync(ticket.CompanyId, ct);
        var policyMode = await GetPolicyModeAsync(ticket.SlaPolicyId, ct);

        var timers = await _timers.Query()
            .Where(t => t.ServiceTicketId == ticket.Id &&
                        t.Status != SlaTimerStatus.Cancelled &&
                        t.Status != SlaTimerStatus.Met)
            .ToListAsync(ct);

        if (from == ServiceTicketStatus.Open && to == ServiceTicketStatus.InProgress)
        {
            var fr = timers.FirstOrDefault(t => t.Metric == SlaMetric.FirstResponse &&
                                                t.Status is SlaTimerStatus.Running or SlaTimerStatus.Paused);
            if (fr is not null)
                CompleteTimer(fr, now, policyMode, calendar, forceBreachedKeep: false);
        }

        if (to is ServiceTicketStatus.Resolved or ServiceTicketStatus.Closed)
        {
            foreach (var timer in timers.Where(t => t.Metric == SlaMetric.FirstResponse &&
                                                     t.Status is SlaTimerStatus.Running or SlaTimerStatus.Paused))
            {
                timer.Status = SlaTimerStatus.Cancelled;
                timer.CompletedAt = now;
                timer.ActiveSince = null;
                AddEvent(timer, now, SlaEventKind.Cancelled, "Ticket resolved/closed without first response");
            }

            var res = timers.FirstOrDefault(t => t.Metric == SlaMetric.Resolution &&
                                                 (t.Status is SlaTimerStatus.Running or SlaTimerStatus.Paused or SlaTimerStatus.Breached));
            if (res is not null && res.CompletedAt is null)
                CompleteTimer(res, now, policyMode, calendar, forceBreachedKeep: res.Status == SlaTimerStatus.Breached || res.BreachedAt is not null);
        }

        await _uow.SaveChangesAsync(ct);
    }

    public async Task<SlaTicketSummaryDto?> GetTicketSlaAsync(int ticketId, CancellationToken ct = default)
    {
        var ticket = await _tickets.Query().AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null) return null;

        var policy = ticket.SlaPolicyId is int pid
            ? await _policies.Query().AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid, ct)
            : null;
        var calendar = await GetCalendarAsync(ticket.CompanyId, ct);
        var mode = policy?.CalendarMode ?? SlaCalendarMode.AlwaysOn;
        var now = _clock.UtcNow;

        var timers = await _timers.Query().AsNoTracking()
            .Where(t => t.ServiceTicketId == ticketId && t.Status != SlaTimerStatus.Cancelled)
            .OrderBy(t => t.Metric)
            .ToListAsync(ct);

        var events = await _events.Query().AsNoTracking()
            .Where(e => timers.Select(t => t.Id).Contains(e.SlaTimerId))
            .OrderByDescending(e => e.At)
            .Take(30)
            .ToListAsync(ct);

        return new SlaTicketSummaryDto(
            ticketId,
            ticket.SlaPolicyId,
            policy?.Name,
            mode,
            timers.Select(t =>
            {
                var elapsed = SlaBusinessHoursCalculator.LiveElapsedSeconds(t, now, mode, calendar);
                var remaining = Math.Max(0, t.TargetSeconds - elapsed);
                return new SlaTimerSummaryDto(
                    t.Id, t.Metric, t.Status, elapsed, t.TargetSeconds, t.WarnSeconds, remaining,
                    t.StartedAt, t.WarnedAt, t.BreachedAt, t.CompletedAt, t.PauseReason);
            }).ToList(),
            events.Select(e => new SlaEventDto(e.At, e.Kind, e.Note)).ToList());
    }

    public async Task<Result> PauseAsync(int ticketId, SlaPauseDto dto, CancellationToken ct = default)
    {
        var ticket = await _tickets.Query().FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null) return Result.Failure("Ticket not found.");
        if (ticket.Status is ServiceTicketStatus.Resolved or ServiceTicketStatus.Closed)
            return Result.Failure("Cannot pause SLA on a resolved/closed ticket.");

        var now = _clock.UtcNow;
        var calendar = await GetCalendarAsync(ticket.CompanyId, ct);
        var mode = await GetPolicyModeAsync(ticket.SlaPolicyId, ct);

        var timers = await _timers.Query()
            .Where(t => t.ServiceTicketId == ticketId && t.Status == SlaTimerStatus.Running)
            .ToListAsync(ct);
        if (timers.Count == 0)
            return Result.Failure("No running SLA timers to pause.");

        foreach (var timer in timers)
        {
            FreezeElapsed(timer, now, mode, calendar);
            timer.Status = SlaTimerStatus.Paused;
            timer.PausedAt = now;
            timer.ActiveSince = null;
            timer.PauseReason = dto.Reason;
            AddEvent(timer, now, SlaEventKind.Paused, dto.Note ?? dto.Reason.ToString());
        }

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ResumeAsync(int ticketId, CancellationToken ct = default)
    {
        var ticket = await _tickets.Query().FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null) return Result.Failure("Ticket not found.");

        var now = _clock.UtcNow;
        var timers = await _timers.Query()
            .Where(t => t.ServiceTicketId == ticketId && t.Status == SlaTimerStatus.Paused)
            .ToListAsync(ct);
        if (timers.Count == 0)
            return Result.Failure("No paused SLA timers to resume.");

        foreach (var timer in timers)
        {
            timer.Status = SlaTimerStatus.Running;
            timer.PausedAt = null;
            timer.ActiveSince = now;
            timer.PauseReason = null;
            AddEvent(timer, now, SlaEventKind.Resumed, null);
        }

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private void CompleteTimer(SlaTimer timer, DateTime now, SlaCalendarMode mode, BusinessCalendar? calendar, bool forceBreachedKeep)
    {
        FreezeElapsed(timer, now, mode, calendar);
        timer.ActiveSince = null;
        timer.CompletedAt = now;
        timer.PausedAt = null;

        if (forceBreachedKeep || timer.Status == SlaTimerStatus.Breached || timer.BreachedAt is not null ||
            timer.ElapsedSeconds > timer.TargetSeconds)
        {
            timer.Status = SlaTimerStatus.Breached;
            timer.BreachedAt ??= now;
            AddEvent(timer, now, SlaEventKind.Breached, "Completed after breach");
        }
        else
        {
            timer.Status = SlaTimerStatus.Met;
            AddEvent(timer, now, SlaEventKind.Met, null);
        }
    }

    private static void FreezeElapsed(SlaTimer timer, DateTime now, SlaCalendarMode mode, BusinessCalendar? calendar)
    {
        timer.ElapsedSeconds = SlaBusinessHoursCalculator.LiveElapsedSeconds(timer, now, mode, calendar);
    }

    private static void AddEvent(SlaTimer timer, DateTime at, SlaEventKind kind, string? note)
    {
        timer.Events.Add(new SlaEvent
        {
            At = at,
            Kind = kind,
            Note = note,
            CreatedAt = at,
            CreatedBy = "system"
        });
    }

    private async Task<BusinessCalendar?> GetCalendarAsync(int companyId, CancellationToken ct) =>
        await _calendars.Query().AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == companyId, ct)
        ?? await _calendars.Query().AsNoTracking().FirstOrDefaultAsync(ct);

    private async Task<SlaCalendarMode> GetPolicyModeAsync(int? policyId, CancellationToken ct)
    {
        if (policyId is not int id) return SlaCalendarMode.AlwaysOn;
        var p = await _policies.Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return p?.CalendarMode ?? SlaCalendarMode.AlwaysOn;
    }
}
