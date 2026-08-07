using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Service;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

public interface IServiceFieldService
{
    Task<IReadOnlyList<ServiceVisitDto>> GetVisitsForTicketAsync(int ticketId, CancellationToken ct = default);
    Task<IReadOnlyList<ServiceVisitDto>> GetMyVisitsAsync(DateTime? day = null, CancellationToken ct = default);
    Task<Result<ServiceVisitDto>> ScheduleVisitAsync(ServiceVisitCreateDto dto, CancellationToken ct = default);
    Task<Result<ServiceVisitDto>> ChangeVisitStatusAsync(int visitId, ServiceVisitStatusDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<ServiceTicketPartDto>> GetPartsForTicketAsync(int ticketId, CancellationToken ct = default);
    Task<Result<ServiceTicketPartDto>> ConsumePartAsync(ServiceTicketPartCreateDto dto, CancellationToken ct = default);
}

public sealed class ServiceFieldService : IServiceFieldService
{
    private readonly IRepository<ServiceVisit> _visits;
    private readonly IRepository<ServiceTicketPart> _parts;
    private readonly IRepository<ServiceTicket> _tickets;
    private readonly IRepository<AppUser> _users;
    private readonly IRepository<Product> _products;
    private readonly IRepository<Warehouse> _warehouses;
    private readonly IInventoryService _inventory;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentCompanyContext _company;
    private readonly ICurrentUserService _user;
    private readonly INotificationService _notifications;

    public ServiceFieldService(
        IRepository<ServiceVisit> visits,
        IRepository<ServiceTicketPart> parts,
        IRepository<ServiceTicket> tickets,
        IRepository<AppUser> users,
        IRepository<Product> products,
        IRepository<Warehouse> warehouses,
        IInventoryService inventory,
        IUnitOfWork uow,
        ICurrentCompanyContext company,
        ICurrentUserService user,
        INotificationService notifications)
    {
        _visits = visits;
        _parts = parts;
        _tickets = tickets;
        _users = users;
        _products = products;
        _warehouses = warehouses;
        _inventory = inventory;
        _uow = uow;
        _company = company;
        _user = user;
        _notifications = notifications;
    }

    private int? TryCompanyId() => _company.CompanyId is int id && id > 0 ? id : null;

    public async Task<IReadOnlyList<ServiceVisitDto>> GetVisitsForTicketAsync(int ticketId, CancellationToken ct = default)
    {
        var list = await _visits.Query().AsNoTracking()
            .Include(v => v.AssignedToUser)
            .Include(v => v.ServiceTicket)
            .Where(v => v.ServiceTicketId == ticketId)
            .OrderBy(v => v.ScheduledAt)
            .ToListAsync(ct);
        return list.Select(MapVisit).ToList();
    }

    public async Task<IReadOnlyList<ServiceVisitDto>> GetMyVisitsAsync(DateTime? day = null, CancellationToken ct = default)
    {
        var userId = _user.CurrentUser?.Id;
        if (userId is null) return [];
        var d = (day ?? DateTime.UtcNow).Date;
        var next = d.AddDays(1);
        var list = await _visits.Query().AsNoTracking()
            .Include(v => v.AssignedToUser)
            .Include(v => v.ServiceTicket)
            .Where(v => v.AssignedToUserId == userId
                        && v.ScheduledAt >= d && v.ScheduledAt < next
                        && v.Status != ServiceVisitStatus.Cancelled)
            .OrderBy(v => v.ScheduledAt)
            .ToListAsync(ct);
        return list.Select(MapVisit).ToList();
    }

    public async Task<Result<ServiceVisitDto>> ScheduleVisitAsync(ServiceVisitCreateDto dto, CancellationToken ct = default)
    {
        var companyId = TryCompanyId();
        if (companyId is null)
            return Result<ServiceVisitDto>.Failure("Company context is required.");

        var ticket = await _tickets.Query().FirstOrDefaultAsync(t => t.Id == dto.ServiceTicketId, ct);
        if (ticket is null)
            return Result<ServiceVisitDto>.Failure("Ticket not found.");
        if (ticket.Status == ServiceTicketStatus.Closed)
            return Result<ServiceVisitDto>.Failure("Cannot schedule visits on a closed ticket.");
        if (!await _users.Query().AsNoTracking().AnyAsync(u => u.Id == dto.AssignedToUserId && !u.IsDeleted, ct))
            return Result<ServiceVisitDto>.Failure("Assignee user not found.");

        var visit = new ServiceVisit
        {
            CompanyId = companyId.Value,
            ServiceTicketId = dto.ServiceTicketId,
            AssignedToUserId = dto.AssignedToUserId,
            ScheduledAt = dto.ScheduledAt,
            Status = ServiceVisitStatus.Scheduled,
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _user.CurrentUser?.Username ?? "system"
        };
        _visits.Add(visit);

        // Stamp ticket assignee from visit if ticket unassigned
        if (ticket.AssignedToUserId is null)
            ticket.AssignedToUserId = dto.AssignedToUserId;

        await _uow.SaveChangesAsync(ct);

        await _notifications.CreateNotificationAsync(
            NotificationType.Success,
            "Service visit scheduled",
            $"Visit for '{ticket.Subject}' at {visit.ScheduledAt:g} (ticket #{ticket.Id}).",
            "ServiceTicket",
            ticket.Id,
            ct);

        var loaded = await _visits.Query().AsNoTracking()
            .Include(v => v.AssignedToUser).Include(v => v.ServiceTicket)
            .FirstAsync(v => v.Id == visit.Id, ct);
        return Result<ServiceVisitDto>.Success(MapVisit(loaded));
    }

    public async Task<Result<ServiceVisitDto>> ChangeVisitStatusAsync(int visitId, ServiceVisitStatusDto dto, CancellationToken ct = default)
    {
        var visit = await _visits.Query().Include(v => v.ServiceTicket)
            .FirstOrDefaultAsync(v => v.Id == visitId, ct);
        if (visit is null)
            return Result<ServiceVisitDto>.Failure("Visit not found.");
        if (visit.Status == ServiceVisitStatus.Cancelled && dto.Status != ServiceVisitStatus.Cancelled)
            return Result<ServiceVisitDto>.Failure("Cancelled visits cannot be restarted.");

        visit.Status = dto.Status;
        if (!string.IsNullOrWhiteSpace(dto.Notes))
            visit.Notes = dto.Notes.Trim();
        if (dto.Status == ServiceVisitStatus.Completed)
            visit.CompletedAt = DateTime.UtcNow;
        if (dto.Status == ServiceVisitStatus.InProgress && visit.ServiceTicket.Status == ServiceTicketStatus.Open)
            visit.ServiceTicket.Status = ServiceTicketStatus.InProgress;
        visit.UpdatedAt = DateTime.UtcNow;
        visit.UpdatedBy = _user.CurrentUser?.Username;
        await _uow.SaveChangesAsync(ct);

        var loaded = await _visits.Query().AsNoTracking()
            .Include(v => v.AssignedToUser).Include(v => v.ServiceTicket)
            .FirstAsync(v => v.Id == visitId, ct);
        return Result<ServiceVisitDto>.Success(MapVisit(loaded));
    }

    public async Task<IReadOnlyList<ServiceTicketPartDto>> GetPartsForTicketAsync(int ticketId, CancellationToken ct = default)
    {
        var list = await _parts.Query().AsNoTracking()
            .Include(p => p.Product).Include(p => p.Warehouse)
            .Where(p => p.ServiceTicketId == ticketId)
            .OrderByDescending(p => p.ConsumedAt)
            .ToListAsync(ct);
        return list.Select(MapPart).ToList();
    }

    public async Task<Result<ServiceTicketPartDto>> ConsumePartAsync(ServiceTicketPartCreateDto dto, CancellationToken ct = default)
    {
        var companyId = TryCompanyId();
        if (companyId is null)
            return Result<ServiceTicketPartDto>.Failure("Company context is required.");
        if (dto.Quantity <= 0)
            return Result<ServiceTicketPartDto>.Failure("Quantity must be positive.");

        var ticket = await _tickets.Query().FirstOrDefaultAsync(t => t.Id == dto.ServiceTicketId, ct);
        if (ticket is null)
            return Result<ServiceTicketPartDto>.Failure("Ticket not found.");
        if (ticket.Status == ServiceTicketStatus.Closed)
            return Result<ServiceTicketPartDto>.Failure("Cannot consume parts on a closed ticket.");

        var product = await _products.Query().AsNoTracking().FirstOrDefaultAsync(p => p.Id == dto.ProductId, ct);
        if (product is null)
            return Result<ServiceTicketPartDto>.Failure("Product not found.");
        if (!await _warehouses.Query().AsNoTracking().AnyAsync(w => w.Id == dto.WarehouseId, ct))
            return Result<ServiceTicketPartDto>.Failure("Warehouse not found.");

        var deduct = await _inventory.DeductStockAsync(
            dto.ProductId, dto.WarehouseId, dto.Quantity,
            nameof(ServiceTicketPart), dto.ServiceTicketId, ct);
        if (!deduct.Succeeded)
            return Result<ServiceTicketPartDto>.Failure(deduct.Error ?? "Stock deduction failed.");

        var part = new ServiceTicketPart
        {
            CompanyId = companyId.Value,
            ServiceTicketId = dto.ServiceTicketId,
            ProductId = dto.ProductId,
            WarehouseId = dto.WarehouseId,
            Quantity = dto.Quantity,
            UnitCost = deduct.Data,
            ConsumedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _user.CurrentUser?.Username ?? "system"
        };
        _parts.Add(part);
        await _uow.SaveChangesAsync(ct);

        var loaded = await _parts.Query().AsNoTracking()
            .Include(p => p.Product).Include(p => p.Warehouse)
            .FirstAsync(p => p.Id == part.Id, ct);
        return Result<ServiceTicketPartDto>.Success(MapPart(loaded));
    }

    private static ServiceVisitDto MapVisit(ServiceVisit v) => new(
        v.Id, v.ServiceTicketId, v.ServiceTicket?.Subject,
        v.AssignedToUserId, v.AssignedToUser?.Username,
        v.ScheduledAt, v.CompletedAt, v.Status, v.Notes);

    private static ServiceTicketPartDto MapPart(ServiceTicketPart p) => new(
        p.Id, p.ServiceTicketId, p.ProductId, p.Product?.Name,
        p.WarehouseId, p.Warehouse?.Name, p.Quantity, p.UnitCost, p.ConsumedAt);
}
