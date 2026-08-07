using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Service;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>Program C1 — Service Light tickets; Phase 8 AMC/warranty depth + SLA hooks.</summary>
public interface IServiceTicketService
{
    Task<PagedResult<ServiceTicketDto>> GetTicketsAsync(QuerySpec query, CancellationToken ct = default);
    Task<ServiceTicketDto?> GetTicketByIdAsync(int id, CancellationToken ct = default);
    Task<Result<ServiceTicketDto>> CreateTicketAsync(ServiceTicketCreateDto dto, CancellationToken ct = default);
    Task<Result<ServiceTicketDto>> UpdateTicketAsync(int id, ServiceTicketUpdateDto dto, CancellationToken ct = default);
    Task<Result<ServiceTicketDto>> ChangeStatusAsync(int id, ServiceTicketStatusChangeDto dto, CancellationToken ct = default);
    Task<Result<ServiceTicketDto>> DecideWarrantyAsync(int id, WarrantyClaimDecisionDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<ServiceTicketDto>> GetTicketsForCustomerAsync(int customerId, CancellationToken ct = default);
}

public sealed class ServiceTicketService : IServiceTicketService
{
    private readonly IRepository<ServiceTicket> _tickets;
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<Product> _products;
    private readonly IRepository<AmcContract> _amc;
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<SlaTimer> _slaTimers;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentCompanyContext _company;
    private readonly ICurrentUserService _user;
    private readonly INotificationService _notifications;
    private readonly ISlaClockService _slaClock;

    public ServiceTicketService(
        IRepository<ServiceTicket> tickets,
        IRepository<Customer> customers,
        IRepository<Product> products,
        IRepository<AmcContract> amc,
        IRepository<SalesInvoice> invoices,
        IRepository<SlaTimer> slaTimers,
        IUnitOfWork uow,
        ICurrentCompanyContext company,
        ICurrentUserService user,
        INotificationService notifications,
        ISlaClockService slaClock)
    {
        _tickets = tickets;
        _customers = customers;
        _products = products;
        _amc = amc;
        _invoices = invoices;
        _slaTimers = slaTimers;
        _uow = uow;
        _company = company;
        _user = user;
        _notifications = notifications;
        _slaClock = slaClock;
    }

    private int? TryCompanyId() => _company.CompanyId is int id && id > 0 ? id : null;

    public async Task<PagedResult<ServiceTicketDto>> GetTicketsAsync(QuerySpec query, CancellationToken ct = default)
    {
        var q = _tickets.Query().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(x => x.Subject.Contains(s) || (x.Description != null && x.Description.Contains(s)));
        }

        if (query.Filters.TryGetValue("status", out var statusObj) && statusObj is not null &&
            Enum.TryParse<ServiceTicketStatus>(statusObj.ToString(), true, out var status))
            q = q.Where(x => x.Status == status);

        if (query.Filters.TryGetValue("priority", out var prioObj) && prioObj is not null &&
            Enum.TryParse<ServiceTicketPriority>(prioObj.ToString(), true, out var priority))
            q = q.Where(x => x.Priority == priority);

        if (query.Filters.TryGetValue("customerId", out var custObj) && int.TryParse(custObj?.ToString(), out var customerId))
            q = q.Where(x => x.CustomerId == customerId);

        if (query.Filters.TryGetValue("assignedToUserId", out var assignedObj) && int.TryParse(assignedObj?.ToString(), out var assignedId))
        {
            if (assignedId < 0)
                q = q.Where(x => x.AssignedToUserId == null);
            else
                q = q.Where(x => x.AssignedToUserId == assignedId);
        }

        if (query.Filters.TryGetValue("unassigned", out var unObj) &&
            bool.TryParse(unObj?.ToString(), out var unassigned) && unassigned)
            q = q.Where(x => x.AssignedToUserId == null);

        if (query.Filters.TryGetValue("warrantyOnly", out var warObj) &&
            bool.TryParse(warObj?.ToString(), out var warrantyOnly) && warrantyOnly)
            q = q.Where(x => x.IsWarrantyClaim);

        if (query.Filters.TryGetValue("warrantyClaimStatus", out var wcsObj) && wcsObj is not null &&
            Enum.TryParse<WarrantyClaimStatus>(wcsObj.ToString(), true, out var wcs))
            q = q.Where(x => x.WarrantyClaimStatus == wcs);

        if (query.Filters.TryGetValue("slaStatus", out var slaObj) && slaObj is not null)
        {
            var sla = slaObj.ToString()?.Trim().ToLowerInvariant();
            if (sla == "breached")
            {
                var ids = _slaTimers.Query().AsNoTracking()
                    .Where(t => t.ServiceTicketId != null &&
                                (t.Status == SlaTimerStatus.Breached || t.BreachedAt != null))
                    .Select(t => t.ServiceTicketId!.Value);
                q = q.Where(x => ids.Contains(x.Id));
            }
            else if (sla is "warning" or "warn")
            {
                var ids = _slaTimers.Query().AsNoTracking()
                    .Where(t => t.ServiceTicketId != null &&
                                t.WarnedAt != null && t.BreachedAt == null && t.Status == SlaTimerStatus.Running)
                    .Select(t => t.ServiceTicketId!.Value);
                q = q.Where(x => ids.Contains(x.Id));
            }
        }

        q = q.OrderByDescending(x => x.CreatedAt);
        var paged = await q.ToPagedResultAsync(query.Page, query.PageSize, ct);

        return new PagedResult<ServiceTicketDto>
        {
            Items = await MapManyAsync(paged.Items, ct),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    public async Task<ServiceTicketDto?> GetTicketByIdAsync(int id, CancellationToken ct = default)
    {
        var entity = await _tickets.Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return null;
        var list = await MapManyAsync([entity], ct);
        return list[0];
    }

    public async Task<Result<ServiceTicketDto>> CreateTicketAsync(ServiceTicketCreateDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Subject))
            return Result<ServiceTicketDto>.Failure("Subject is required.");

        var companyId = TryCompanyId();
        if (companyId is null)
            return Result<ServiceTicketDto>.Failure("Company context is required.");

        var customer = await _customers.Query().AsNoTracking().FirstOrDefaultAsync(c => c.Id == dto.CustomerId, ct);
        if (customer is null)
            return Result<ServiceTicketDto>.Failure("Customer not found.");

        if (dto.ProductId is int pid && !await _products.Query().AsNoTracking().AnyAsync(p => p.Id == pid, ct))
            return Result<ServiceTicketDto>.Failure("Product not found.");

        var amcLink = await ResolveAmcAsync(dto.AmcContractId, dto.CustomerId, ct);
        if (amcLink.Error is not null)
            return Result<ServiceTicketDto>.Failure(amcLink.Error);

        if (dto.WarrantySalesInvoiceId is int invId &&
            !await _invoices.Query().AsNoTracking().AnyAsync(i => i.Id == invId && !i.IsDeleted, ct))
            return Result<ServiceTicketDto>.Failure("Warranty sales invoice not found.");

        var entity = new ServiceTicket
        {
            CompanyId = companyId.Value,
            CustomerId = dto.CustomerId,
            Subject = dto.Subject.Trim(),
            Description = Norm(dto.Description),
            Status = ServiceTicketStatus.Open,
            Priority = dto.Priority,
            IsWarrantyClaim = dto.IsWarrantyClaim,
            WarrantyReference = Norm(dto.WarrantyReference),
            WarrantyClaimStatus = dto.IsWarrantyClaim ? WarrantyClaimStatus.Submitted : WarrantyClaimStatus.None,
            WarrantySalesInvoiceId = dto.IsWarrantyClaim ? dto.WarrantySalesInvoiceId : null,
            WarrantyEvidenceNotes = dto.IsWarrantyClaim ? Norm(dto.WarrantyEvidenceNotes) : null,
            AmcReference = Norm(dto.AmcReference) ?? amcLink.Contract?.ContractNumber,
            AmcContractId = amcLink.Contract?.Id,
            ProductId = dto.ProductId,
            AssignedToUserId = dto.AssignedToUserId,
            OpenedAt = DateTime.UtcNow,
            DueAt = dto.DueAt,
            Notes = Norm(dto.Notes),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _user.CurrentUser?.Username ?? "system"
        };

        _tickets.Add(entity);
        await _uow.SaveChangesAsync(ct);
        await _slaClock.OnTicketCreatedAsync(entity, dto.SlaPolicyId, ct);

        if (entity.AssignedToUserId is int)
        {
            await _notifications.CreateNotificationAsync(
                NotificationType.Success,
                "Service ticket assigned to you",
                $"'{entity.Subject}' was assigned (#{entity.Id}).",
                "ServiceTicket",
                entity.Id,
                ct);
        }

        var mapped = await MapManyAsync([entity], ct);
        return Result<ServiceTicketDto>.Success(mapped[0]);
    }

    public async Task<Result<ServiceTicketDto>> UpdateTicketAsync(int id, ServiceTicketUpdateDto dto, CancellationToken ct = default)
    {
        var entity = await _tickets.Query().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return Result<ServiceTicketDto>.Failure("Ticket not found.");
        if (entity.Status == ServiceTicketStatus.Closed)
            return Result<ServiceTicketDto>.Failure("Closed tickets cannot be edited.");
        if (string.IsNullOrWhiteSpace(dto.Subject))
            return Result<ServiceTicketDto>.Failure("Subject is required.");

        if (dto.ProductId is int pid && !await _products.Query().AsNoTracking().AnyAsync(p => p.Id == pid, ct))
            return Result<ServiceTicketDto>.Failure("Product not found.");

        var amcLink = await ResolveAmcAsync(dto.AmcContractId, entity.CustomerId, ct);
        if (amcLink.Error is not null)
            return Result<ServiceTicketDto>.Failure(amcLink.Error);

        if (dto.WarrantySalesInvoiceId is int invId &&
            !await _invoices.Query().AsNoTracking().AnyAsync(i => i.Id == invId && !i.IsDeleted, ct))
            return Result<ServiceTicketDto>.Failure("Warranty sales invoice not found.");

        var previousAssignee = entity.AssignedToUserId;
        var wasWarranty = entity.IsWarrantyClaim;

        entity.Subject = dto.Subject.Trim();
        entity.Description = Norm(dto.Description);
        entity.Priority = dto.Priority;
        entity.IsWarrantyClaim = dto.IsWarrantyClaim;
        entity.WarrantyReference = Norm(dto.WarrantyReference);
        entity.WarrantyEvidenceNotes = dto.IsWarrantyClaim ? Norm(dto.WarrantyEvidenceNotes) : null;
        entity.WarrantySalesInvoiceId = dto.IsWarrantyClaim ? dto.WarrantySalesInvoiceId : null;
        if (dto.IsWarrantyClaim && !wasWarranty && entity.WarrantyClaimStatus == WarrantyClaimStatus.None)
            entity.WarrantyClaimStatus = WarrantyClaimStatus.Submitted;
        if (!dto.IsWarrantyClaim)
        {
            entity.WarrantyClaimStatus = WarrantyClaimStatus.None;
            entity.WarrantyDecisionNotes = null;
            entity.WarrantyDecidedAt = null;
            entity.WarrantyDecidedBy = null;
            entity.ReplacementProductId = null;
            entity.ReplacementQuantity = 0;
            entity.WarrantyEvidenceNotes = null;
            entity.WarrantySalesInvoiceId = null;
        }
        entity.AmcContractId = amcLink.Contract?.Id;
        entity.AmcReference = Norm(dto.AmcReference) ?? amcLink.Contract?.ContractNumber;
        entity.ProductId = dto.ProductId;
        entity.AssignedToUserId = dto.AssignedToUserId;
        entity.DueAt = dto.DueAt;
        entity.Notes = Norm(dto.Notes);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _user.CurrentUser?.Username;

        await _uow.SaveChangesAsync(ct);

        if (entity.AssignedToUserId is int newAssignee && newAssignee != previousAssignee)
        {
            await _notifications.CreateNotificationAsync(
                NotificationType.Success,
                "Service ticket assigned to you",
                $"'{entity.Subject}' was assigned (#{entity.Id}).",
                "ServiceTicket",
                entity.Id,
                ct);
        }

        var mapped = await MapManyAsync([entity], ct);
        return Result<ServiceTicketDto>.Success(mapped[0]);
    }

    public async Task<Result<ServiceTicketDto>> DecideWarrantyAsync(int id, WarrantyClaimDecisionDto dto, CancellationToken ct = default)
    {
        if (dto.Decision is not WarrantyClaimStatus.Approved and not WarrantyClaimStatus.Rejected)
            return Result<ServiceTicketDto>.Failure("Decision must be Approved or Rejected.");

        if (dto.Decision == WarrantyClaimStatus.Rejected && string.IsNullOrWhiteSpace(dto.Notes))
            return Result<ServiceTicketDto>.Failure("Notes are required when rejecting a warranty claim.");

        var entity = await _tickets.Query().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return Result<ServiceTicketDto>.Failure("Ticket not found.");
        if (!entity.IsWarrantyClaim)
            return Result<ServiceTicketDto>.Failure("Ticket is not a warranty claim.");
        if (entity.Status == ServiceTicketStatus.Closed)
            return Result<ServiceTicketDto>.Failure("Closed tickets cannot change warranty decision.");
        if (entity.WarrantyClaimStatus is WarrantyClaimStatus.Approved or WarrantyClaimStatus.Rejected)
            return Result<ServiceTicketDto>.Failure("Warranty claim already decided.");

        if (dto.Decision == WarrantyClaimStatus.Approved && dto.ReplacementProductId is int rpId)
        {
            if (!await _products.Query().AsNoTracking().AnyAsync(p => p.Id == rpId, ct))
                return Result<ServiceTicketDto>.Failure("Replacement product not found.");
            if (dto.ReplacementQuantity < 0)
                return Result<ServiceTicketDto>.Failure("Replacement quantity cannot be negative.");
            entity.ReplacementProductId = rpId;
            entity.ReplacementQuantity = dto.ReplacementQuantity;
        }

        entity.WarrantyClaimStatus = dto.Decision;
        entity.WarrantyDecisionNotes = Norm(dto.Notes);
        entity.WarrantyDecidedAt = DateTime.UtcNow;
        entity.WarrantyDecidedBy = _user.CurrentUser?.Username ?? "system";
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = entity.WarrantyDecidedBy;

        await _uow.SaveChangesAsync(ct);

        await _notifications.CreateNotificationAsync(
            NotificationType.Success,
            dto.Decision == WarrantyClaimStatus.Approved ? "Warranty claim approved" : "Warranty claim rejected",
            $"'{entity.Subject}' · {dto.Decision}" + (string.IsNullOrWhiteSpace(dto.Notes) ? "" : $": {dto.Notes}"),
            "ServiceTicket",
            entity.Id,
            ct);

        var mapped = await MapManyAsync([entity], ct);
        return Result<ServiceTicketDto>.Success(mapped[0]);
    }

    public async Task<Result<ServiceTicketDto>> ChangeStatusAsync(int id, ServiceTicketStatusChangeDto dto, CancellationToken ct = default)
    {
        var entity = await _tickets.Query().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return Result<ServiceTicketDto>.Failure("Ticket not found.");

        if (entity.Status == ServiceTicketStatus.Closed)
            return Result<ServiceTicketDto>.Failure("Ticket is already closed; open a new ticket instead.");

        var needsResolution = dto.Status is ServiceTicketStatus.Resolved or ServiceTicketStatus.Closed;
        if (needsResolution && string.IsNullOrWhiteSpace(dto.ResolutionNotes) && string.IsNullOrWhiteSpace(entity.ResolutionNotes))
            return Result<ServiceTicketDto>.Failure("Resolution notes are required to resolve or close a ticket.");

        var from = entity.Status;
        entity.Status = dto.Status;
        if (!string.IsNullOrWhiteSpace(dto.ResolutionNotes))
            entity.ResolutionNotes = dto.ResolutionNotes.Trim();
        if (dto.Status == ServiceTicketStatus.Resolved)
            entity.ResolvedAt = DateTime.UtcNow;
        if (dto.Status == ServiceTicketStatus.Closed)
            entity.ClosedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _user.CurrentUser?.Username;

        await _uow.SaveChangesAsync(ct);
        await _slaClock.OnTicketStatusChangedAsync(entity, from, dto.Status, ct);

        var mapped = await MapManyAsync([entity], ct);
        return Result<ServiceTicketDto>.Success(mapped[0]);
    }

    public async Task<IReadOnlyList<ServiceTicketDto>> GetTicketsForCustomerAsync(int customerId, CancellationToken ct = default)
    {
        var items = await _tickets.Query().AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .ToListAsync(ct);
        return await MapManyAsync(items, ct);
    }

    private async Task<(AmcContract? Contract, string? Error)> ResolveAmcAsync(
        int? amcContractId, int customerId, CancellationToken ct)
    {
        if (amcContractId is null) return (null, null);
        var contract = await _amc.Query().AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == amcContractId.Value, ct);
        if (contract is null)
            return (null, "AMC contract not found.");
        if (contract.CustomerId != customerId)
            return (null, "AMC contract belongs to a different customer.");
        return (contract, null);
    }

    private async Task<List<ServiceTicketDto>> MapManyAsync(IReadOnlyCollection<ServiceTicket> items, CancellationToken ct)
    {
        var customerIds = items.Select(t => t.CustomerId).Distinct().ToList();
        var productIds = items
            .SelectMany(t => new int?[] { t.ProductId, t.ReplacementProductId })
            .Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        var amcIds = items.Where(t => t.AmcContractId is not null).Select(t => t.AmcContractId!.Value).Distinct().ToList();

        var customers = customerIds.Count == 0
            ? new Dictionary<int, string>()
            : await _customers.Query().AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var products = productIds.Count == 0
            ? new Dictionary<int, string>()
            : await _products.Query().AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var amcs = amcIds.Count == 0
            ? new Dictionary<int, string>()
            : await _amc.Query().AsNoTracking().Where(a => amcIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.ContractNumber, ct);

        return items.Select(t => new ServiceTicketDto(
            t.Id, t.CustomerId, customers.GetValueOrDefault(t.CustomerId), t.Subject, t.Description, t.Status, t.Priority,
            t.IsWarrantyClaim, t.WarrantyReference, t.WarrantyClaimStatus, t.WarrantyDecisionNotes,
            t.WarrantyDecidedAt, t.WarrantyDecidedBy, t.AmcReference, t.ProductId,
            t.ProductId is int pid ? products.GetValueOrDefault(pid) : null,
            t.AssignedToUserId, t.OpenedAt, t.DueAt, t.ResolvedAt, t.ClosedAt, t.Notes, t.ResolutionNotes, t.CreatedAt,
            t.AmcContractId, t.AmcContractId is int aid ? amcs.GetValueOrDefault(aid) : null,
            t.WarrantySalesInvoiceId, t.ReplacementProductId,
            t.ReplacementProductId is int rpid ? products.GetValueOrDefault(rpid) : null,
            t.ReplacementQuantity, t.WarrantyEvidenceNotes)).ToList();
    }

    private static string? Norm(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
