using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Service;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>Program C1 — Service Light: thin ticket/warranty/AMC tracker (not full field service/SLA).</summary>
public interface IServiceTicketService
{
    Task<PagedResult<ServiceTicketDto>> GetTicketsAsync(QuerySpec query, CancellationToken ct = default);
    Task<ServiceTicketDto?> GetTicketByIdAsync(int id, CancellationToken ct = default);
    Task<Result<ServiceTicketDto>> CreateTicketAsync(ServiceTicketCreateDto dto, CancellationToken ct = default);
    Task<Result<ServiceTicketDto>> UpdateTicketAsync(int id, ServiceTicketUpdateDto dto, CancellationToken ct = default);
    Task<Result<ServiceTicketDto>> ChangeStatusAsync(int id, ServiceTicketStatusChangeDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<ServiceTicketDto>> GetTicketsForCustomerAsync(int customerId, CancellationToken ct = default);
}

public sealed class ServiceTicketService : IServiceTicketService
{
    private readonly IRepository<ServiceTicket> _tickets;
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<Product> _products;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentCompanyContext _company;
    private readonly ICurrentUserService _user;
    private readonly INotificationService _notifications;

    public ServiceTicketService(
        IRepository<ServiceTicket> tickets,
        IRepository<Customer> customers,
        IRepository<Product> products,
        IUnitOfWork uow,
        ICurrentCompanyContext company,
        ICurrentUserService user,
        INotificationService notifications)
    {
        _tickets = tickets;
        _customers = customers;
        _products = products;
        _uow = uow;
        _company = company;
        _user = user;
        _notifications = notifications;
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
            q = q.Where(x => x.AssignedToUserId == assignedId);

        q = q.OrderByDescending(x => x.CreatedAt);
        var paged = await q.ToPagedResultAsync(query.Page, query.PageSize, ct);

        var (customerNames, productNames) = await LookupNamesAsync(paged.Items, ct);
        return new PagedResult<ServiceTicketDto>
        {
            Items = paged.Items.Select(t => Map(t, customerNames.GetValueOrDefault(t.CustomerId), t.ProductId is int pid ? productNames.GetValueOrDefault(pid) : null)).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    public async Task<ServiceTicketDto?> GetTicketByIdAsync(int id, CancellationToken ct = default)
    {
        var entity = await _tickets.Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return null;
        var (customerNames, productNames) = await LookupNamesAsync([entity], ct);
        return Map(entity, customerNames.GetValueOrDefault(entity.CustomerId), entity.ProductId is int pid ? productNames.GetValueOrDefault(pid) : null);
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
            AmcReference = Norm(dto.AmcReference),
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

        if (entity.AssignedToUserId is int)
        {
            await _notifications.CreateNotificationAsync(
                NotificationType.Success,
                "Service ticket assigned",
                entity.Subject,
                "ServiceTicket",
                entity.Id,
                ct);
        }

        return Result<ServiceTicketDto>.Success(Map(entity, customer.Name, null));
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

        entity.Subject = dto.Subject.Trim();
        entity.Description = Norm(dto.Description);
        entity.Priority = dto.Priority;
        entity.IsWarrantyClaim = dto.IsWarrantyClaim;
        entity.WarrantyReference = Norm(dto.WarrantyReference);
        entity.AmcReference = Norm(dto.AmcReference);
        entity.ProductId = dto.ProductId;
        entity.AssignedToUserId = dto.AssignedToUserId;
        entity.DueAt = dto.DueAt;
        entity.Notes = Norm(dto.Notes);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _user.CurrentUser?.Username;

        await _uow.SaveChangesAsync(ct);
        var (customerNames, productNames) = await LookupNamesAsync([entity], ct);
        return Result<ServiceTicketDto>.Success(Map(entity, customerNames.GetValueOrDefault(entity.CustomerId), entity.ProductId is int p ? productNames.GetValueOrDefault(p) : null));
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
        var (customerNames, productNames) = await LookupNamesAsync([entity], ct);
        return Result<ServiceTicketDto>.Success(Map(entity, customerNames.GetValueOrDefault(entity.CustomerId), entity.ProductId is int p ? productNames.GetValueOrDefault(p) : null));
    }

    public async Task<IReadOnlyList<ServiceTicketDto>> GetTicketsForCustomerAsync(int customerId, CancellationToken ct = default)
    {
        var items = await _tickets.Query().AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .ToListAsync(ct);
        var (customerNames, productNames) = await LookupNamesAsync(items, ct);
        return items.Select(t => Map(t, customerNames.GetValueOrDefault(t.CustomerId), t.ProductId is int pid ? productNames.GetValueOrDefault(pid) : null)).ToList();
    }

    private async Task<(Dictionary<int, string> Customers, Dictionary<int, string> Products)> LookupNamesAsync(
        IReadOnlyCollection<ServiceTicket> items, CancellationToken ct)
    {
        var customerIds = items.Select(t => t.CustomerId).Distinct().ToList();
        var productIds = items.Where(t => t.ProductId is not null).Select(t => t.ProductId!.Value).Distinct().ToList();

        var customers = customerIds.Count == 0
            ? new Dictionary<int, string>()
            : await _customers.Query().AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var products = productIds.Count == 0
            ? new Dictionary<int, string>()
            : await _products.Query().AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        return (customers, products);
    }

    private static string? Norm(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static ServiceTicketDto Map(ServiceTicket t, string? customerName, string? productName) => new(
        t.Id, t.CustomerId, customerName, t.Subject, t.Description, t.Status, t.Priority,
        t.IsWarrantyClaim, t.WarrantyReference, t.AmcReference, t.ProductId, productName,
        t.AssignedToUserId, t.OpenedAt, t.DueAt, t.ResolvedAt, t.ClosedAt, t.Notes, t.ResolutionNotes, t.CreatedAt);
}
