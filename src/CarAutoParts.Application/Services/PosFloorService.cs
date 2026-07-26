using CarAutoParts.Application.Common;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Pos;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

public sealed class PosFloorService : IPosFloorService
{
    private readonly IRepository<HeldSale> _held;
    private readonly IRepository<CashierShift> _shifts;
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<Payment> _payments;
    private readonly IRepository<SalesReturn> _returns;
    private readonly IRepository<Product> _products;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ICurrentCompanyContext _company;

    public PosFloorService(
        IRepository<HeldSale> held,
        IRepository<CashierShift> shifts,
        IRepository<SalesInvoice> invoices,
        IRepository<Payment> payments,
        IRepository<SalesReturn> returns,
        IRepository<Product> products,
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        ICurrentCompanyContext company)
    {
        _held = held;
        _shifts = shifts;
        _invoices = invoices;
        _payments = payments;
        _returns = returns;
        _products = products;
        _uow = uow;
        _currentUser = currentUser;
        _company = company;
    }

    public async Task<Result<HeldSaleDto>> HoldAsync(HoldSaleRequestDto dto, CancellationToken ct = default)
    {
        if (!_currentUser.HasPermission(Permissions.PosHold) && !_currentUser.HasPermission(Permissions.PosCheckout))
            return Result<HeldSaleDto>.Failure("Hold permission required.");
        if (dto.Lines.Count == 0)
            return Result<HeldSaleDto>.Failure("Cart is empty.");
        if (_currentUser.CurrentUser is null)
            return Result<HeldSaleDto>.Failure("Not authenticated.");

        var count = await _held.Query().CountAsync(ct);
        var entity = new HeldSale
        {
            HoldNumber = $"HLD-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D4}",
            UserId = _currentUser.CurrentUser.Id,
            UserName = _currentUser.CurrentUser.Username,
            WarehouseId = dto.WarehouseId,
            CustomerId = dto.CustomerId,
            BuyerName = dto.BuyerName,
            Notes = dto.Notes,
            Status = HeldSaleStatus.Held,
            HeldAt = DateTime.UtcNow
        };

        foreach (var line in dto.Lines)
        {
            var product = await _products.Query().FirstOrDefaultAsync(p => p.Id == line.ProductId && !p.IsDeleted, ct);
            if (product is null)
                return Result<HeldSaleDto>.Failure($"Product {line.ProductId} not found.");

            entity.Lines.Add(new HeldSaleLine
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = line.Quantity,
                UnitPriceOverride = line.UnitPriceOverride,
                DiscountAmount = line.DiscountAmount
            });
        }

        _held.Add(entity);
        await _uow.SaveChangesAsync(ct);
        return Result<HeldSaleDto>.Success(MapHeld(entity));
    }

    public async Task<IReadOnlyList<HeldSaleDto>> ListHeldAsync(CancellationToken ct = default)
    {
        var userId = _currentUser.CurrentUser?.Id ?? 0;
        var items = await _held.Query()
            .Include(h => h.Lines)
            .Where(h => !h.IsDeleted && h.Status == HeldSaleStatus.Held && h.UserId == userId)
            .OrderByDescending(h => h.HeldAt)
            .Take(50)
            .ToListAsync(ct);
        return items.Select(MapHeld).ToList();
    }

    public async Task<Result<HeldSaleDto>> RecallAsync(int heldSaleId, CancellationToken ct = default)
    {
        var userId = _currentUser.CurrentUser?.Id ?? 0;
        var entity = await _held.Query().Include(h => h.Lines)
            .FirstOrDefaultAsync(h => h.Id == heldSaleId && !h.IsDeleted, ct);
        if (entity is null)
            return Result<HeldSaleDto>.Failure("Held sale not found.");
        if (entity.UserId != userId)
            return Result<HeldSaleDto>.Failure("Held sale belongs to another cashier.");
        if (entity.Status != HeldSaleStatus.Held)
            return Result<HeldSaleDto>.Failure("Held sale is no longer available.");

        entity.Status = HeldSaleStatus.Recalled;
        entity.RecalledAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);
        return Result<HeldSaleDto>.Success(MapHeld(entity));
    }

    public async Task<Result> DiscardHeldAsync(int heldSaleId, CancellationToken ct = default)
    {
        var userId = _currentUser.CurrentUser?.Id ?? 0;
        var entity = await _held.Query().FirstOrDefaultAsync(h => h.Id == heldSaleId && !h.IsDeleted, ct);
        if (entity is null)
            return Result.Failure("Held sale not found.");
        if (entity.UserId != userId)
            return Result.Failure("Held sale belongs to another cashier.");

        entity.Status = HeldSaleStatus.Discarded;
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<CashierShiftDto>> OpenShiftAsync(OpenShiftRequestDto dto, CancellationToken ct = default)
    {
        if (!_currentUser.HasPermission(Permissions.PosShift) && !_currentUser.HasPermission(Permissions.PosCheckout))
            return Result<CashierShiftDto>.Failure("Shift permission required.");
        if (_currentUser.CurrentUser is null)
            return Result<CashierShiftDto>.Failure("Not authenticated.");

        var existing = await _shifts.Query()
            .FirstOrDefaultAsync(s => s.UserId == _currentUser.CurrentUser.Id && s.Status == CashierShiftStatus.Open && !s.IsDeleted, ct);
        if (existing is not null)
            return Result<CashierShiftDto>.Failure("You already have an open shift.");

        var count = await _shifts.Query().CountAsync(ct);
        var shift = new CashierShift
        {
            ShiftNumber = $"SHF-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D4}",
            UserId = _currentUser.CurrentUser.Id,
            UserName = _currentUser.CurrentUser.Username,
            BranchId = _company.BranchId,
            WarehouseId = dto.WarehouseId,
            OpeningFloat = dto.OpeningFloat,
            Notes = dto.Notes,
            Status = CashierShiftStatus.Open,
            OpenedAt = DateTime.UtcNow
        };
        _shifts.Add(shift);
        await _uow.SaveChangesAsync(ct);
        return Result<CashierShiftDto>.Success(MapShift(shift));
    }

    public async Task<Result<CashierShiftDto>> CloseShiftAsync(int shiftId, CloseShiftRequestDto dto, CancellationToken ct = default)
    {
        var userId = _currentUser.CurrentUser?.Id ?? 0;
        var shift = await _shifts.Query().FirstOrDefaultAsync(s => s.Id == shiftId && !s.IsDeleted, ct);
        if (shift is null)
            return Result<CashierShiftDto>.Failure("Shift not found.");
        if (shift.UserId != userId)
            return Result<CashierShiftDto>.Failure("Shift belongs to another cashier.");
        if (shift.Status != CashierShiftStatus.Open)
            return Result<CashierShiftDto>.Failure("Shift is already closed.");

        shift.ClosingFloat = dto.ClosingFloat;
        shift.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? shift.Notes : dto.Notes;
        shift.Status = CashierShiftStatus.Closed;
        shift.ClosedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);
        return Result<CashierShiftDto>.Success(MapShift(shift));
    }

    public async Task<CashierShiftDto?> GetOpenShiftAsync(CancellationToken ct = default)
    {
        var userId = _currentUser.CurrentUser?.Id ?? 0;
        var shift = await _shifts.Query()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == CashierShiftStatus.Open && !s.IsDeleted, ct);
        return shift is null ? null : MapShift(shift);
    }

    public async Task<Result<ShiftZReportDto>> GetZReportAsync(int shiftId, CancellationToken ct = default)
    {
        var shift = await _shifts.Query().FirstOrDefaultAsync(s => s.Id == shiftId && !s.IsDeleted, ct);
        if (shift is null)
            return Result<ShiftZReportDto>.Failure("Shift not found.");

        var end = shift.ClosedAt ?? DateTime.UtcNow;
        var invoices = await _invoices.Query()
            .Include(i => i.Payments)
            .Where(i => !i.IsDeleted && i.CashierShiftId == shiftId)
            .ToListAsync(ct);

        // Also include invoices in shift window for older rows without shift id
        if (invoices.Count == 0)
        {
            invoices = await _invoices.Query()
                .Include(i => i.Payments)
                .Where(i => !i.IsDeleted && i.InvoiceDate >= shift.OpenedAt && i.InvoiceDate <= end
                    && (i.CreatedBy == shift.UserName || i.CashierShiftId == null))
                .ToListAsync(ct);
            invoices = invoices.Where(i => i.CashierShiftId == null || i.CashierShiftId == shiftId).ToList();
        }

        var tenderTotals = invoices
            .SelectMany(i => i.Payments)
            .GroupBy(p => p.PaymentMethod, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ShiftTenderTotalDto(g.Key, g.Sum(x => x.Amount)))
            .OrderBy(t => t.Method)
            .ToList();

        var cashSales = tenderTotals
            .Where(t => t.Method.Contains("cash", StringComparison.OrdinalIgnoreCase))
            .Sum(t => t.Amount);

        var returns = await _returns.Query()
            .Where(r => !r.IsDeleted && r.ReturnDate >= shift.OpenedAt && r.ReturnDate <= end)
            .ToListAsync(ct);

        var holds = await _held.Query()
            .CountAsync(h => h.UserId == shift.UserId && h.HeldAt >= shift.OpenedAt && h.HeldAt <= end, ct);

        return Result<ShiftZReportDto>.Success(new ShiftZReportDto(
            shift.Id,
            shift.ShiftNumber,
            shift.OpenedAt,
            shift.ClosedAt,
            shift.OpeningFloat,
            shift.ClosingFloat,
            shift.OpeningFloat + cashSales - invoices.Sum(i => i.ChangeDue),
            tenderTotals,
            invoices.Count,
            returns.Count,
            holds,
            invoices.Sum(i => i.GrandTotal),
            returns.Sum(r => r.GrandTotal)));
    }

    private static HeldSaleDto MapHeld(HeldSale h) => new(
        h.Id,
        h.HoldNumber,
        h.WarehouseId,
        h.CustomerId,
        h.BuyerName,
        h.Notes,
        h.HeldAt,
        h.Lines.Select(l => new HeldSaleLineDto(l.ProductId, l.ProductName, l.Quantity, l.UnitPriceOverride, l.DiscountAmount)).ToList());

    private static CashierShiftDto MapShift(CashierShift s) => new(
        s.Id,
        s.ShiftNumber,
        s.UserId,
        s.UserName,
        s.WarehouseId,
        s.Status.ToString(),
        s.OpeningFloat,
        s.ClosingFloat,
        s.OpenedAt,
        s.ClosedAt);
}
