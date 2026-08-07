using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Service;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

public interface ISlaPolicyService
{
    Task<IReadOnlyList<SlaPolicyDto>> ListAsync(SlaEntityType? entityType = null, CancellationToken ct = default);
    Task<Result<SlaPolicyDto>> UpsertAsync(SlaPolicyUpsertDto dto, CancellationToken ct = default);
    Task<Result> SetDefaultAsync(int policyId, CancellationToken ct = default);
    Task EnsureDefaultPolicyAsync(int companyId, CancellationToken ct = default);
    Task EnsureDefaultOpsPoliciesAsync(int companyId, CancellationToken ct = default);
    Task<IReadOnlyList<SlaPolicyRuleDto>> ListRulesAsync(int policyId, CancellationToken ct = default);
    Task<Result<SlaPolicyRuleDto>> UpsertRuleAsync(int policyId, SlaPolicyRuleUpsertDto dto, CancellationToken ct = default);
    Task<Result> DeleteRuleAsync(int policyId, int ruleId, CancellationToken ct = default);
    Task<BusinessCalendarDto?> GetCalendarAsync(CancellationToken ct = default);
    Task<Result<BusinessCalendarDto>> UpsertCalendarAsync(BusinessCalendarUpsertDto dto, CancellationToken ct = default);
    Task<SlaDashboardDto> GetDashboardAsync(SlaEntityType? entityType = null, CancellationToken ct = default);
    Task<IReadOnlyList<SlaBreachQueueItemDto>> GetBreachQueueAsync(SlaEntityType? entityType = null, int? policyId = null, CancellationToken ct = default);
    Task<IReadOnlyList<SlaEntityAlertDto>> GetOpenAlertsAsync(SlaEntityType entityType, CancellationToken ct = default);
}

public sealed class SlaPolicyService : ISlaPolicyService
{
    private readonly IRepository<SlaPolicy> _policies;
    private readonly IRepository<SlaPolicyRule> _rules;
    private readonly IRepository<SlaTarget> _targets;
    private readonly IRepository<SlaTimer> _timers;
    private readonly IRepository<BusinessCalendar> _calendars;
    private readonly IRepository<ServiceTicket> _tickets;
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<AppUser> _users;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentCompanyContext _company;
    private readonly ICurrentUserService _user;

    public SlaPolicyService(
        IRepository<SlaPolicy> policies,
        IRepository<SlaPolicyRule> rules,
        IRepository<SlaTarget> targets,
        IRepository<SlaTimer> timers,
        IRepository<BusinessCalendar> calendars,
        IRepository<ServiceTicket> tickets,
        IRepository<Customer> customers,
        IRepository<AppUser> users,
        IUnitOfWork uow,
        ICurrentCompanyContext company,
        ICurrentUserService user)
    {
        _policies = policies;
        _rules = rules;
        _targets = targets;
        _timers = timers;
        _calendars = calendars;
        _tickets = tickets;
        _customers = customers;
        _users = users;
        _uow = uow;
        _company = company;
        _user = user;
    }

    private int? TryCompanyId() => _company.CompanyId is int id && id > 0 ? id : null;

    public async Task<IReadOnlyList<SlaPolicyDto>> ListAsync(SlaEntityType? entityType = null, CancellationToken ct = default)
    {
        var companyId = TryCompanyId();
        if (companyId is int cid)
        {
            await EnsureDefaultPolicyAsync(cid, ct);
            await EnsureDefaultOpsPoliciesAsync(cid, ct);
        }

        var q = _policies.Query().AsNoTracking()
            .Include(p => p.Targets)
            .Include(p => p.Rules);
        var list = await (entityType is null ? q : q.Where(p => p.AppliesToEntityType == entityType.Value))
            .OrderByDescending(p => p.IsDefault)
            .ThenBy(p => p.AppliesToEntityType)
            .ThenBy(p => p.Name)
            .ToListAsync(ct);
        return list.Select(Map).ToList();
    }

    public async Task<Result<SlaPolicyDto>> UpsertAsync(SlaPolicyUpsertDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<SlaPolicyDto>.Failure("Name is required.");
        if (dto.Targets is null || dto.Targets.Count == 0)
            return Result<SlaPolicyDto>.Failure("At least one target is required.");

        var companyId = TryCompanyId();
        if (companyId is null)
            return Result<SlaPolicyDto>.Failure("Company context is required.");

        foreach (var t in dto.Targets)
        {
            if (t.TargetMinutes <= 0)
                return Result<SlaPolicyDto>.Failure("Target minutes must be positive.");
            if (t.WarnAtPercent is < 1 or > 100)
                return Result<SlaPolicyDto>.Failure("Warn percent must be 1–100.");
        }

        if (dto.EscalateToUserId is > 0)
        {
            var escalateOk = await _users.Query().AsNoTracking()
                .AnyAsync(u => u.Id == dto.EscalateToUserId && u.IsActive && !u.IsDeleted, ct);
            if (!escalateOk)
                return Result<SlaPolicyDto>.Failure("Escalate-to user must be an active user.");
        }

        SlaPolicy entity;
        if (dto.Id is int id)
        {
            var existing = await _policies.Query().Include(p => p.Targets).FirstOrDefaultAsync(p => p.Id == id, ct);
            if (existing is null)
                return Result<SlaPolicyDto>.Failure("Policy not found.");
            entity = existing;
            entity.Name = dto.Name.Trim();
            entity.IsActive = dto.IsActive;
            entity.CalendarMode = dto.CalendarMode;
            entity.ApplyToWarrantyOnly = dto.ApplyToWarrantyOnly;
            entity.AppliesToEntityType = dto.AppliesToEntityType;
            entity.EscalateToUserId = dto.EscalateToUserId is > 0 ? dto.EscalateToUserId : null;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = _user.CurrentUser?.Username;

            foreach (var old in entity.Targets.ToList())
                _targets.Remove(old);
            entity.Targets.Clear();
        }
        else
        {
            entity = new SlaPolicy
            {
                CompanyId = companyId.Value,
                Name = dto.Name.Trim(),
                IsActive = dto.IsActive,
                CalendarMode = dto.CalendarMode,
                ApplyToWarrantyOnly = dto.ApplyToWarrantyOnly,
                AppliesToEntityType = dto.AppliesToEntityType,
                EscalateToUserId = dto.EscalateToUserId is > 0 ? dto.EscalateToUserId : null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _user.CurrentUser?.Username ?? "system"
            };
            _policies.Add(entity);
        }

        if (dto.IsDefault && dto.AppliesToEntityType == SlaEntityType.ServiceTicket)
        {
            var others = await _policies.Query()
                .Where(p => p.IsDefault && p.AppliesToEntityType == SlaEntityType.ServiceTicket && (dto.Id == null || p.Id != dto.Id))
                .ToListAsync(ct);
            foreach (var o in others) o.IsDefault = false;
            entity.IsDefault = true;
        }
        else if (dto.Id is null && dto.AppliesToEntityType == SlaEntityType.ServiceTicket)
        {
            entity.IsDefault = !await _policies.Query().AnyAsync(p => p.IsDefault && p.AppliesToEntityType == SlaEntityType.ServiceTicket, ct);
        }
        else if (dto.AppliesToEntityType != SlaEntityType.ServiceTicket)
        {
            entity.IsDefault = false;
        }

        foreach (var t in dto.Targets)
        {
            entity.Targets.Add(new SlaTarget
            {
                CompanyId = companyId.Value,
                Metric = t.Metric,
                Priority = t.Priority,
                TargetMinutes = t.TargetMinutes,
                WarnAtPercent = t.WarnAtPercent,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _user.CurrentUser?.Username ?? "system"
            });
        }

        await _uow.SaveChangesAsync(ct);
        var loaded = await _policies.Query().AsNoTracking()
            .Include(p => p.Targets).Include(p => p.Rules)
            .FirstAsync(p => p.Id == entity.Id, ct);
        return Result<SlaPolicyDto>.Success(Map(loaded));
    }

    public async Task<Result> SetDefaultAsync(int policyId, CancellationToken ct = default)
    {
        var entity = await _policies.Query().FirstOrDefaultAsync(p => p.Id == policyId, ct);
        if (entity is null) return Result.Failure("Policy not found.");

        var others = await _policies.Query().Where(p => p.IsDefault && p.Id != policyId).ToListAsync(ct);
        foreach (var o in others) o.IsDefault = false;
        entity.IsDefault = true;
        entity.IsActive = true;
        entity.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task EnsureDefaultPolicyAsync(int companyId, CancellationToken ct = default)
    {
        if (await _policies.Query().IgnoreQueryFilters().AnyAsync(
                p => p.CompanyId == companyId && !p.IsDeleted && p.AppliesToEntityType == SlaEntityType.ServiceTicket, ct))
            return;

        var policy = new SlaPolicy
        {
            CompanyId = companyId,
            Name = "Default Service SLA",
            IsDefault = true,
            IsActive = true,
            AppliesToEntityType = SlaEntityType.ServiceTicket,
            CalendarMode = SlaCalendarMode.AlwaysOn,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };

        // AlwaysOn wall-clock: treat "business day" as 8 hours until BusinessHours mode.
        void Add(SlaMetric metric, ServiceTicketPriority priority, int minutes) =>
            policy.Targets.Add(new SlaTarget
            {
                CompanyId = companyId,
                Metric = metric,
                Priority = priority,
                TargetMinutes = minutes,
                WarnAtPercent = 80,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            });

        Add(SlaMetric.FirstResponse, ServiceTicketPriority.Urgent, 30);
        Add(SlaMetric.Resolution, ServiceTicketPriority.Urgent, 4 * 60);
        Add(SlaMetric.FirstResponse, ServiceTicketPriority.High, 2 * 60);
        Add(SlaMetric.Resolution, ServiceTicketPriority.High, 8 * 60);
        Add(SlaMetric.FirstResponse, ServiceTicketPriority.Normal, 4 * 60);
        Add(SlaMetric.Resolution, ServiceTicketPriority.Normal, 3 * 8 * 60);
        Add(SlaMetric.FirstResponse, ServiceTicketPriority.Low, 8 * 60);
        Add(SlaMetric.Resolution, ServiceTicketPriority.Low, 5 * 8 * 60);

        _policies.Add(policy);

        if (!await _calendars.Query().IgnoreQueryFilters().AnyAsync(c => c.CompanyId == companyId && !c.IsDeleted, ct))
        {
            _calendars.Add(new BusinessCalendar
            {
                CompanyId = companyId,
                TimeZoneId = "Asia/Karachi",
                WorkIntervalsJson = SlaBusinessHoursCalculator.DefaultMonSatJson(),
                HolidaysJson = "[]",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            });
        }

        await _uow.SaveChangesAsync(ct);
    }

    public async Task EnsureDefaultOpsPoliciesAsync(int companyId, CancellationToken ct = default)
    {
        async Task EnsureOne(SlaEntityType type, string name, int resolutionMinutes)
        {
            if (await _policies.Query().IgnoreQueryFilters().AnyAsync(
                    p => p.CompanyId == companyId && !p.IsDeleted && p.AppliesToEntityType == type, ct))
                return;

            var policy = new SlaPolicy
            {
                CompanyId = companyId,
                Name = name,
                IsDefault = false,
                IsActive = true,
                AppliesToEntityType = type,
                CalendarMode = SlaCalendarMode.AlwaysOn,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            };
            foreach (var priority in Enum.GetValues<ServiceTicketPriority>())
            {
                policy.Targets.Add(new SlaTarget
                {
                    CompanyId = companyId,
                    Metric = SlaMetric.Resolution,
                    Priority = priority,
                    TargetMinutes = resolutionMinutes,
                    WarnAtPercent = 80,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "system"
                });
            }
            _policies.Add(policy);
        }

        await EnsureOne(SlaEntityType.SalesOrder, "Open sales order age", 3 * 24 * 60);
        await EnsureOne(SlaEntityType.SalesInvoice, "Unpaid invoice age", 7 * 24 * 60);
        await EnsureOne(SlaEntityType.GoodsReceiptNote, "Stuck GRN age", 2 * 24 * 60);
        await EnsureOne(SlaEntityType.PurchaseInvoice, "Stuck AP invoice age", 5 * 24 * 60);
        await EnsureOne(SlaEntityType.InventoryLowStock, "Low stock reorder", 2 * 24 * 60);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SlaPolicyRuleDto>> ListRulesAsync(int policyId, CancellationToken ct = default)
    {
        var rules = await _rules.Query().AsNoTracking()
            .Where(r => r.SlaPolicyId == policyId)
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Id)
            .ToListAsync(ct);
        return rules.Select(MapRule).ToList();
    }

    public async Task<Result<SlaPolicyRuleDto>> UpsertRuleAsync(int policyId, SlaPolicyRuleUpsertDto dto, CancellationToken ct = default)
    {
        var companyId = TryCompanyId();
        if (companyId is null)
            return Result<SlaPolicyRuleDto>.Failure("Company context is required.");

        var policy = await _policies.Query().FirstOrDefaultAsync(p => p.Id == policyId, ct);
        if (policy is null) return Result<SlaPolicyRuleDto>.Failure("Policy not found.");
        if (policy.AppliesToEntityType != SlaEntityType.ServiceTicket)
            return Result<SlaPolicyRuleDto>.Failure("Routing rules apply only to ServiceTicket policies.");

        SlaPolicyRule entity;
        if (dto.Id is int id)
        {
            var existing = await _rules.Query().FirstOrDefaultAsync(r => r.Id == id && r.SlaPolicyId == policyId, ct);
            if (existing is null) return Result<SlaPolicyRuleDto>.Failure("Rule not found.");
            entity = existing;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = _user.CurrentUser?.Username;
        }
        else
        {
            entity = new SlaPolicyRule
            {
                CompanyId = companyId.Value,
                SlaPolicyId = policyId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _user.CurrentUser?.Username ?? "system"
            };
            _rules.Add(entity);
        }

        entity.Priority = dto.Priority;
        entity.CustomerType = dto.CustomerType;
        entity.CustomerId = dto.CustomerId is > 0 ? dto.CustomerId : null;
        entity.IsWarrantyClaim = dto.IsWarrantyClaim;
        entity.SortOrder = dto.SortOrder;
        entity.IsActive = dto.IsActive;
        await _uow.SaveChangesAsync(ct);
        return Result<SlaPolicyRuleDto>.Success(MapRule(entity));
    }

    public async Task<Result> DeleteRuleAsync(int policyId, int ruleId, CancellationToken ct = default)
    {
        var entity = await _rules.Query().FirstOrDefaultAsync(r => r.Id == ruleId && r.SlaPolicyId == policyId, ct);
        if (entity is null) return Result.Failure("Rule not found.");
        _rules.Remove(entity);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<BusinessCalendarDto?> GetCalendarAsync(CancellationToken ct = default)
    {
        var companyId = TryCompanyId();
        if (companyId is int cid)
            await EnsureDefaultPolicyAsync(cid, ct);

        var cal = await _calendars.Query().AsNoTracking().FirstOrDefaultAsync(ct);
        return cal is null ? null : new BusinessCalendarDto(cal.Id, cal.TimeZoneId, cal.WorkIntervalsJson, cal.HolidaysJson);
    }

    public async Task<Result<BusinessCalendarDto>> UpsertCalendarAsync(BusinessCalendarUpsertDto dto, CancellationToken ct = default)
    {
        var companyId = TryCompanyId();
        if (companyId is null)
            return Result<BusinessCalendarDto>.Failure("Company context is required.");
        if (string.IsNullOrWhiteSpace(dto.TimeZoneId))
            return Result<BusinessCalendarDto>.Failure("Time zone is required.");

        var entity = await _calendars.Query().FirstOrDefaultAsync(ct);
        if (entity is null)
        {
            entity = new BusinessCalendar
            {
                CompanyId = companyId.Value,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _user.CurrentUser?.Username ?? "system"
            };
            _calendars.Add(entity);
        }

        entity.TimeZoneId = dto.TimeZoneId.Trim();
        entity.WorkIntervalsJson = string.IsNullOrWhiteSpace(dto.WorkIntervalsJson)
            ? SlaBusinessHoursCalculator.DefaultMonSatJson()
            : dto.WorkIntervalsJson;
        entity.HolidaysJson = string.IsNullOrWhiteSpace(dto.HolidaysJson) ? "[]" : dto.HolidaysJson;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _user.CurrentUser?.Username;
        await _uow.SaveChangesAsync(ct);
        return Result<BusinessCalendarDto>.Success(new BusinessCalendarDto(entity.Id, entity.TimeZoneId, entity.WorkIntervalsJson, entity.HolidaysJson));
    }

    public async Task<SlaDashboardDto> GetDashboardAsync(SlaEntityType? entityType = null, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddDays(-30);
        var q = _timers.Query().AsNoTracking().Where(t => t.Status != SlaTimerStatus.Cancelled);
        if (entityType is not null)
            q = q.Where(t => t.EntityType == entityType.Value);
        var timers = await q.ToListAsync(ct);

        var openBreach = timers.Count(t => t.Status == SlaTimerStatus.Breached && t.CompletedAt is null);
        var openWarn = timers.Count(t => t.WarnedAt is not null && t.Status == SlaTimerStatus.Running && t.BreachedAt is null);

        var frDone = timers.Where(t => t.Metric == SlaMetric.FirstResponse && t.CompletedAt is not null && t.CompletedAt >= since).ToList();
        var resDone = timers.Where(t => t.Metric == SlaMetric.Resolution && t.CompletedAt is not null && t.CompletedAt >= since).ToList();

        int frMet = frDone.Count(t => t.Status == SlaTimerStatus.Met);
        int resMet = resDone.Count(t => t.Status == SlaTimerStatus.Met);

        var policyIds = timers.Select(t => t.SlaPolicyId).Distinct().ToList();
        var policies = await _policies.Query().AsNoTracking()
            .Where(p => policyIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var byPolicy = policyIds.Select(pid =>
        {
            policies.TryGetValue(pid, out var pol);
            var fr = frDone.Where(t => t.SlaPolicyId == pid).ToList();
            var res = resDone.Where(t => t.SlaPolicyId == pid).ToList();
            var frM = fr.Count(t => t.Status == SlaTimerStatus.Met);
            var resM = res.Count(t => t.Status == SlaTimerStatus.Met);
            return new SlaPolicyComplianceDto(
                pid,
                pol?.Name ?? $"#{pid}",
                pol?.AppliesToEntityType ?? SlaEntityType.ServiceTicket,
                fr.Count == 0 ? 0 : Math.Round(100.0 * frM / fr.Count, 1),
                res.Count == 0 ? 0 : Math.Round(100.0 * resM / res.Count, 1),
                fr.Count,
                res.Count);
        }).OrderBy(x => x.PolicyName).ToList();

        return new SlaDashboardDto(
            openBreach,
            openWarn,
            frMet,
            frDone.Count,
            resMet,
            resDone.Count,
            frDone.Count == 0 ? 0 : Math.Round(100.0 * frMet / frDone.Count, 1),
            resDone.Count == 0 ? 0 : Math.Round(100.0 * resMet / resDone.Count, 1),
            byPolicy);
    }

    public async Task<IReadOnlyList<SlaBreachQueueItemDto>> GetBreachQueueAsync(
        SlaEntityType? entityType = null, int? policyId = null, CancellationToken ct = default)
    {
        var q = _timers.Query().AsNoTracking()
            .Where(t => t.Status == SlaTimerStatus.Breached && t.CompletedAt == null);
        if (entityType is not null) q = q.Where(t => t.EntityType == entityType.Value);
        if (policyId is not null) q = q.Where(t => t.SlaPolicyId == policyId.Value);

        var breached = await q.OrderByDescending(t => t.BreachedAt).Take(100).ToListAsync(ct);
        if (breached.Count == 0) return [];

        var policyIds = breached.Select(t => t.SlaPolicyId).Distinct().ToList();
        var policies = await _policies.Query().AsNoTracking()
            .Where(p => policyIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var ticketIds = breached.Where(t => t.EntityType == SlaEntityType.ServiceTicket)
            .Select(t => t.EntityId).Distinct().ToList();
        var tickets = ticketIds.Count == 0
            ? new Dictionary<int, ServiceTicket>()
            : await _tickets.Query().AsNoTracking()
                .Where(t => ticketIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, ct);
        var customerIds = tickets.Values.Select(t => t.CustomerId).Distinct().ToList();
        var customers = customerIds.Count == 0
            ? new Dictionary<int, string>()
            : await _customers.Query().AsNoTracking()
                .Where(c => customerIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        return breached
            .Where(t => t.BreachedAt is not null)
            .Select(t =>
            {
                policies.TryGetValue(t.SlaPolicyId, out var pol);
                tickets.TryGetValue(t.EntityId, out var ticket);
                var (subject, deepLink) = t.EntityType switch
                {
                    SlaEntityType.ServiceTicket => (ticket?.Subject ?? $"Ticket #{t.EntityId}", $"service/tickets/{t.EntityId}"),
                    SlaEntityType.SalesOrder => ($"Sales order #{t.EntityId}", $"sales-orders?highlight={t.EntityId}"),
                    SlaEntityType.SalesInvoice => ($"Invoice #{t.EntityId}", $"invoices?search={t.EntityId}"),
                    SlaEntityType.GoodsReceiptNote => ($"GRN #{t.EntityId}", "grn"),
                    SlaEntityType.PurchaseInvoice => ($"AP invoice #{t.EntityId}", "ap-invoices"),
                    SlaEntityType.InventoryLowStock => ($"Low stock #{t.EntityId}", "inventory"),
                    _ => ($"{t.EntityType} #{t.EntityId}", "/")
                };
                return new SlaBreachQueueItemDto(
                    t.EntityType == SlaEntityType.ServiceTicket ? t.EntityId : null,
                    subject,
                    ticket?.CustomerId,
                    ticket is null ? null : customers.GetValueOrDefault(ticket.CustomerId),
                    ticket?.Status,
                    ticket?.Priority,
                    t.Metric,
                    t.BreachedAt!.Value,
                    t.ElapsedSeconds,
                    t.TargetSeconds,
                    ticket?.AssignedToUserId,
                    t.EntityType,
                    t.EntityId,
                    t.SlaPolicyId,
                    pol?.Name,
                    deepLink);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<SlaEntityAlertDto>> GetOpenAlertsAsync(SlaEntityType entityType, CancellationToken ct = default)
    {
        var timers = await _timers.Query().AsNoTracking()
            .Where(t => t.EntityType == entityType &&
                        t.CompletedAt == null &&
                        (t.Status == SlaTimerStatus.Breached ||
                         (t.Status == SlaTimerStatus.Running && t.WarnedAt != null)))
            .ToListAsync(ct);

        return timers
            .GroupBy(t => t.EntityId)
            .Select(g => new SlaEntityAlertDto(
                entityType,
                g.Key,
                g.Any(t => t.Status == SlaTimerStatus.Breached || t.BreachedAt != null),
                g.Any(t => t.WarnedAt != null && t.Status == SlaTimerStatus.Running && t.BreachedAt == null)))
            .ToList();
    }

    private static SlaPolicyRuleDto MapRule(SlaPolicyRule r) => new(
        r.Id, r.SlaPolicyId, r.Priority, r.CustomerType, r.CustomerId, r.IsWarrantyClaim, r.SortOrder, r.IsActive);

    private static SlaPolicyDto Map(SlaPolicy p) => new(
        p.Id,
        p.Name,
        p.IsDefault,
        p.IsActive,
        p.CalendarMode,
        p.ApplyToWarrantyOnly,
        p.EscalateToUserId,
        p.AppliesToEntityType,
        p.Targets.OrderBy(t => t.Priority).ThenBy(t => t.Metric)
            .Select(t => new SlaTargetDto(t.Id, t.Metric, t.Priority, t.TargetMinutes, t.WarnAtPercent))
            .ToList(),
        (p.Rules ?? []).OrderBy(r => r.SortOrder).ThenBy(r => r.Id).Select(MapRule).ToList());
}
