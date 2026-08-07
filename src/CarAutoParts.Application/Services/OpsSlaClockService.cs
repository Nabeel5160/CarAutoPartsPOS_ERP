using CarAutoParts.Application.Common;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarAutoParts.Application.Services;

/// <summary>Thin ops SLA clocks (1A) — selected docs only; no POS-line / journal clocks.</summary>
public interface IOpsSlaClockService
{
    Task OnSalesOrderOpenedAsync(int orderId, int companyId, CancellationToken ct = default);
    Task OnSalesOrderClosedAsync(int orderId, CancellationToken ct = default);
    Task OnInvoiceUnpaidAsync(int invoiceId, int companyId, CancellationToken ct = default);
    Task OnInvoicePaidOrVoidedAsync(int invoiceId, CancellationToken ct = default);
    Task OnGrnOpenedAsync(int grnId, int companyId, CancellationToken ct = default);
    Task OnGrnClosedAsync(int grnId, CancellationToken ct = default);
    Task OnApInvoiceOpenedAsync(int invoiceId, int companyId, CancellationToken ct = default);
    Task OnApInvoiceClosedAsync(int invoiceId, CancellationToken ct = default);
    Task SyncLowStockAsync(int? companyId = null, CancellationToken ct = default);
}

public sealed class OpsSlaClockService : IOpsSlaClockService
{
    private readonly IRepository<SlaPolicy> _policies;
    private readonly IRepository<SlaTimer> _timers;
    private readonly IRepository<SlaEvent> _events;
    private readonly IRepository<InventoryItem> _inventory;
    private readonly IRepository<Product> _products;
    private readonly ISlaPolicyService _policyService;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentCompanyContext _company;
    private readonly ISlaClockTime _clock;
    private readonly ILogger<OpsSlaClockService> _logger;

    public OpsSlaClockService(
        IRepository<SlaPolicy> policies,
        IRepository<SlaTimer> timers,
        IRepository<SlaEvent> events,
        IRepository<InventoryItem> inventory,
        IRepository<Product> products,
        ISlaPolicyService policyService,
        IUnitOfWork uow,
        ICurrentCompanyContext company,
        ISlaClockTime clock,
        ILogger<OpsSlaClockService> logger)
    {
        _policies = policies;
        _timers = timers;
        _events = events;
        _inventory = inventory;
        _products = products;
        _policyService = policyService;
        _uow = uow;
        _company = company;
        _clock = clock;
        _logger = logger;
    }

    public Task OnSalesOrderOpenedAsync(int orderId, int companyId, CancellationToken ct = default) =>
        StartResolutionAsync(SlaEntityType.SalesOrder, orderId, companyId, ServiceTicketPriority.Normal, ct);

    public Task OnSalesOrderClosedAsync(int orderId, CancellationToken ct = default) =>
        CompleteAsync(SlaEntityType.SalesOrder, orderId, ct);

    public Task OnInvoiceUnpaidAsync(int invoiceId, int companyId, CancellationToken ct = default) =>
        StartResolutionAsync(SlaEntityType.SalesInvoice, invoiceId, companyId, ServiceTicketPriority.Normal, ct);

    public Task OnInvoicePaidOrVoidedAsync(int invoiceId, CancellationToken ct = default) =>
        CompleteAsync(SlaEntityType.SalesInvoice, invoiceId, ct);

    public Task OnGrnOpenedAsync(int grnId, int companyId, CancellationToken ct = default) =>
        StartResolutionAsync(SlaEntityType.GoodsReceiptNote, grnId, companyId, ServiceTicketPriority.Normal, ct);

    public Task OnGrnClosedAsync(int grnId, CancellationToken ct = default) =>
        CompleteAsync(SlaEntityType.GoodsReceiptNote, grnId, ct);

    public Task OnApInvoiceOpenedAsync(int invoiceId, int companyId, CancellationToken ct = default) =>
        StartResolutionAsync(SlaEntityType.PurchaseInvoice, invoiceId, companyId, ServiceTicketPriority.Normal, ct);

    public Task OnApInvoiceClosedAsync(int invoiceId, CancellationToken ct = default) =>
        CompleteAsync(SlaEntityType.PurchaseInvoice, invoiceId, ct);

    public async Task SyncLowStockAsync(int? companyId = null, CancellationToken ct = default)
    {
        var cid = companyId ?? _company.CompanyId;
        if (cid is not int company) return;

        await _policyService.EnsureDefaultOpsPoliciesAsync(company, ct);

        var items = await _inventory.Query().ToListAsync(ct);
        if (items.Count == 0)
        {
            await ClearLowStockNotInSetAsync(company, [], ct);
            return;
        }

        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _products.Query()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var now = _clock.UtcNow;
        var lowKeys = new HashSet<int>();

        foreach (var item in items)
        {
            if (!products.TryGetValue(item.ProductId, out var product)) continue;
            var min = product.MinimumStock > 0 ? product.MinimumStock : product.ReorderLevel;
            if (min <= 0 || item.QuantityOnHand >= min) continue;

            var entityId = item.ProductId * 100_000 + item.WarehouseId;
            lowKeys.Add(entityId);
            await StartResolutionAsync(SlaEntityType.InventoryLowStock, entityId, company, ServiceTicketPriority.High, ct, save: false);
        }

        await ClearLowStockNotInSetAsync(company, lowKeys, ct);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task ClearLowStockNotInSetAsync(int companyId, HashSet<int> lowKeys, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var openLow = await _timers.Query()
            .Where(t => t.CompanyId == companyId
                        && t.EntityType == SlaEntityType.InventoryLowStock
                        && t.Status != SlaTimerStatus.Cancelled
                        && t.Status != SlaTimerStatus.Met
                        && t.CompletedAt == null)
            .ToListAsync(ct);

        foreach (var timer in openLow)
        {
            if (lowKeys.Contains(timer.EntityId)) continue;
            timer.Status = SlaTimerStatus.Met;
            timer.CompletedAt = now;
            timer.ActiveSince = null;
            _events.Add(new SlaEvent
            {
                SlaTimerId = timer.Id,
                At = now,
                Kind = SlaEventKind.Met,
                Note = "Stock replenished",
                CreatedAt = now,
                CreatedBy = "ops-sla"
            });
        }
    }

    private async Task StartResolutionAsync(
        SlaEntityType entityType, int entityId, int companyId, ServiceTicketPriority priority,
        CancellationToken ct, bool save = true)
    {
        await _policyService.EnsureDefaultOpsPoliciesAsync(companyId, ct);

        var existing = await _timers.Query()
            .AnyAsync(t => t.EntityType == entityType && t.EntityId == entityId
                           && t.Metric == SlaMetric.Resolution
                           && t.Status != SlaTimerStatus.Cancelled
                           && t.CompletedAt == null, ct);
        if (existing) return;

        var policy = await _policies.Query()
            .Include(p => p.Targets)
            .FirstOrDefaultAsync(p => p.IsActive && p.AppliesToEntityType == entityType, ct);
        if (policy is null)
        {
            _logger.LogDebug("No ops SLA policy for {EntityType}", entityType);
            return;
        }

        var target = policy.Targets.FirstOrDefault(t => t.Metric == SlaMetric.Resolution && t.Priority == priority)
                     ?? policy.Targets.FirstOrDefault(t => t.Metric == SlaMetric.Resolution);
        if (target is null) return;

        var now = _clock.UtcNow;
        var targetSeconds = target.TargetMinutes * 60;
        var warnSeconds = (int)Math.Floor(targetSeconds * (target.WarnAtPercent / 100.0));
        var timer = new SlaTimer
        {
            CompanyId = companyId,
            ServiceTicketId = null,
            EntityType = entityType,
            EntityId = entityId,
            Metric = SlaMetric.Resolution,
            SlaPolicyId = policy.Id,
            SlaTargetId = target.Id,
            Status = SlaTimerStatus.Running,
            StartedAt = now,
            ActiveSince = now,
            ElapsedSeconds = 0,
            TargetSeconds = targetSeconds,
            WarnSeconds = warnSeconds,
            CreatedAt = now,
            CreatedBy = "ops-sla"
        };
        _timers.Add(timer);
        _events.Add(new SlaEvent
        {
            SlaTimer = timer,
            At = now,
            Kind = SlaEventKind.Started,
            CreatedAt = now,
            CreatedBy = "ops-sla"
        });
        if (save) await _uow.SaveChangesAsync(ct);
    }

    private async Task CompleteAsync(SlaEntityType entityType, int entityId, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var timers = await _timers.Query()
            .Where(t => t.EntityType == entityType && t.EntityId == entityId
                        && t.CompletedAt == null
                        && t.Status != SlaTimerStatus.Cancelled)
            .ToListAsync(ct);
        if (timers.Count == 0) return;

        foreach (var timer in timers)
        {
            if (timer.Status == SlaTimerStatus.Breached || timer.BreachedAt is not null)
            {
                timer.Status = SlaTimerStatus.Breached;
            }
            else
            {
                timer.Status = SlaTimerStatus.Met;
            }
            timer.CompletedAt = now;
            timer.ActiveSince = null;
            _events.Add(new SlaEvent
            {
                SlaTimerId = timer.Id,
                At = now,
                Kind = timer.Status == SlaTimerStatus.Breached ? SlaEventKind.Breached : SlaEventKind.Met,
                Note = "Ops document cleared",
                CreatedAt = now,
                CreatedBy = "ops-sla"
            });
        }

        await _uow.SaveChangesAsync(ct);
    }
}
