using CarAutoParts.Application.Common;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Pos;
using CarAutoParts.Application.DTOs.Reports;
using CarAutoParts.Application.Enterprise;
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
    private readonly IRepository<Warehouse> _warehouses;
    private readonly IRepository<CostCenter> _costCenters;
    private readonly IRepository<Till> _tills;
    private readonly IRepository<SafeDrop> _safeDrops;
    private readonly IRepository<Branch> _branches;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ICurrentCompanyContext _company;
    private readonly IGlPostingService _gl;

    public PosFloorService(
        IRepository<HeldSale> held,
        IRepository<CashierShift> shifts,
        IRepository<SalesInvoice> invoices,
        IRepository<Payment> payments,
        IRepository<SalesReturn> returns,
        IRepository<Product> products,
        IRepository<Warehouse> warehouses,
        IRepository<CostCenter> costCenters,
        IRepository<Till> tills,
        IRepository<SafeDrop> safeDrops,
        IRepository<Branch> branches,
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        ICurrentCompanyContext company,
        IGlPostingService gl)
    {
        _held = held;
        _shifts = shifts;
        _invoices = invoices;
        _payments = payments;
        _returns = returns;
        _products = products;
        _warehouses = warehouses;
        _costCenters = costCenters;
        _tills = tills;
        _safeDrops = safeDrops;
        _branches = branches;
        _uow = uow;
        _currentUser = currentUser;
        _company = company;
        _gl = gl;
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
        if (dto.TillId is null or <= 0)
            return Result<CashierShiftDto>.Failure("Till is required to open a shift.");

        var till = await _tills.Query()
            .FirstOrDefaultAsync(t => t.Id == dto.TillId && !t.IsDeleted && t.IsActive, ct);
        if (till is null)
            return Result<CashierShiftDto>.Failure("Till not found.");
        if (!_company.IsBranchAllowed(till.BranchId))
            return Result<CashierShiftDto>.Failure("Till branch is not allowed for this user.");

        var warehouseId = dto.WarehouseId ?? till.WarehouseId;
        if (warehouseId is int whId)
        {
            var wh = await _warehouses.Query().FirstOrDefaultAsync(w => w.Id == whId && !w.IsDeleted, ct);
            if (wh is null)
                return Result<CashierShiftDto>.Failure("Warehouse not found.");
            if (wh.BranchId is int b && !_company.IsBranchAllowed(b))
                return Result<CashierShiftDto>.Failure("Warehouse branch is not allowed for this user.");
        }

        var existing = await _shifts.Query()
            .FirstOrDefaultAsync(s => s.UserId == _currentUser.CurrentUser.Id && s.Status == CashierShiftStatus.Open && !s.IsDeleted, ct);
        if (existing is not null)
            return Result<CashierShiftDto>.Failure("You already have an open shift.");

        var tillBusy = await _shifts.Query()
            .AnyAsync(s => s.TillId == till.Id && s.Status == CashierShiftStatus.Open && !s.IsDeleted, ct);
        if (tillBusy)
            return Result<CashierShiftDto>.Failure("This till already has an open shift.");

        var count = await _shifts.Query().CountAsync(ct);
        var shift = new CashierShift
        {
            ShiftNumber = $"SHF-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D4}",
            UserId = _currentUser.CurrentUser.Id,
            UserName = _currentUser.CurrentUser.Username,
            BranchId = till.BranchId,
            TillId = till.Id,
            WarehouseId = warehouseId,
            OpeningFloat = dto.OpeningFloat,
            Notes = dto.Notes,
            Status = CashierShiftStatus.Open,
            OpenedAt = DateTime.UtcNow
        };
        _shifts.Add(shift);
        await _uow.SaveChangesAsync(ct);
        return Result<CashierShiftDto>.Success(await MapShiftAsync(shift, ct));
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

        var z = await GetZReportAsync(shiftId, ct);
        if (!z.Succeeded || z.Data is null)
            return Result<CashierShiftDto>.Failure(z.Error ?? "Could not compute expected cash.");

        var expected = z.Data.ExpectedCash;
        var declared = dto.DeclaredClosingCash ?? dto.ClosingFloat;
        var variance = expected - declared;

        shift.ClosingFloat = dto.ClosingFloat;
        shift.DeclaredClosingCash = declared;
        shift.ExpectedCash = expected;
        shift.CashVariance = variance;
        shift.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? shift.Notes : dto.Notes;
        shift.Status = CashierShiftStatus.Closed;
        shift.ClosedAt = DateTime.UtcNow;

        if (Math.Abs(variance) >= 0.01m && _company.CompanyId.HasValue)
        {
            var amount = Math.Abs(variance);
            int? costCenterId = null;
            if (shift.BranchId is int branchId)
            {
                costCenterId = await _costCenters.Query()
                    .Where(c => !c.IsDeleted && c.IsActive && c.BranchId == branchId)
                    .OrderBy(c => c.Id)
                    .Select(c => (int?)c.Id)
                    .FirstOrDefaultAsync(ct);
            }

            var glLines = variance > 0
                ? new List<GlPostingLineRequest>
                {
                    new("OverShort", amount, true, "Cash short", costCenterId),
                    new("Cash", amount, false, "Cash short", costCenterId)
                }
                : new List<GlPostingLineRequest>
                {
                    new("Cash", amount, true, "Cash over", costCenterId),
                    new("OverShort", amount, false, "Cash over", costCenterId)
                };

            var gl = await _gl.PostDocumentAsync(
                "CashierShift",
                DateTime.UtcNow.Date,
                shift.ShiftNumber,
                $"Shift variance {shift.ShiftNumber}",
                shift.Id,
                glLines,
                autoPost: true,
                ct);
            if (!gl.Succeeded)
                return Result<CashierShiftDto>.Failure(gl.Error ?? "Cash variance GL failed.");
            shift.VarianceJournalEntryId = gl.Data!.JournalId;
        }

        await _uow.SaveChangesAsync(ct);
        return Result<CashierShiftDto>.Success(await MapShiftAsync(shift, ct));
    }

    public async Task<CashierShiftDto?> GetOpenShiftAsync(CancellationToken ct = default)
    {
        var userId = _currentUser.CurrentUser?.Id ?? 0;
        var shift = await _shifts.Query()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == CashierShiftStatus.Open && !s.IsDeleted, ct);
        return shift is null ? null : await MapShiftAsync(shift, ct);
    }

    public async Task<Result<ShiftZReportDto>> GetXReportAsync(int? shiftId = null, CancellationToken ct = default)
    {
        CashierShift? shift;
        if (shiftId is int id)
        {
            shift = await _shifts.Query().FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);
        }
        else
        {
            var userId = _currentUser.CurrentUser?.Id ?? 0;
            shift = await _shifts.Query()
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == CashierShiftStatus.Open && !s.IsDeleted, ct);
        }

        if (shift is null)
            return Result<ShiftZReportDto>.Failure("Open shift not found.");
        if (shift.Status != CashierShiftStatus.Open)
            return Result<ShiftZReportDto>.Failure("X-report is only available for an open shift. Use Z archive for closed shifts.");
        if (shift.BranchId is int b && !_company.IsBranchAllowed(b))
            return Result<ShiftZReportDto>.Failure("Shift branch is not allowed for this user.");

        return await GetZReportAsync(shift.Id, ct);
    }

    public async Task<IReadOnlyList<ClosedShiftListItemDto>> ListClosedShiftsAsync(
        DateTime from, DateTime to, int? tillId = null, int? branchId = null, CancellationToken ct = default)
    {
        if (branchId is int bid && !_company.IsBranchAllowed(bid))
            return Array.Empty<ClosedShiftListItemDto>();

        var end = to.Date.AddDays(1).AddTicks(-1);
        var q = _shifts.Query()
            .Include(s => s.Till)
            .Where(s => !s.IsDeleted
                && s.Status == CashierShiftStatus.Closed
                && s.ClosedAt != null
                && s.ClosedAt >= from.Date
                && s.ClosedAt <= end);

        if (tillId is int tid)
            q = q.Where(s => s.TillId == tid);
        if (branchId is int b)
            q = q.Where(s => s.BranchId == b);
        else if (_company.AllowedBranchIds.Count > 0)
            q = q.Where(s => s.BranchId == null || _company.AllowedBranchIds.Contains(s.BranchId.Value));

        var list = await q.OrderByDescending(s => s.ClosedAt).Take(500).ToListAsync(ct);
        return list
            .Where(s => s.BranchId is not int sb || _company.IsBranchAllowed(sb))
            .Select(s => new ClosedShiftListItemDto(
                s.Id, s.ShiftNumber, s.UserName, s.BranchId, s.TillId, s.Till?.Code,
                s.OpenedAt, s.ClosedAt, s.OpeningFloat, s.ClosingFloat,
                s.ExpectedCash, s.CashVariance, s.Status.ToString()))
            .ToList();
    }

    public async Task<Result<ShiftZReportDto>> GetZReportAsync(int shiftId, CancellationToken ct = default)
    {
        var shift = await _shifts.Query().FirstOrDefaultAsync(s => s.Id == shiftId && !s.IsDeleted, ct);
        if (shift is null)
            return Result<ShiftZReportDto>.Failure("Shift not found.");
        if (shift.BranchId is int branchId && !_company.IsBranchAllowed(branchId))
            return Result<ShiftZReportDto>.Failure("Shift branch is not allowed for this user.");

        var end = shift.ClosedAt ?? DateTime.UtcNow;
        var invoices = await _invoices.Query()
            .Include(i => i.Payments)
            .Where(i => !i.IsDeleted && i.CashierShiftId == shiftId)
            .ToListAsync(ct);

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

        var safeDrops = await _safeDrops.Query()
            .Where(d => d.CashierShiftId == shiftId && !d.IsDeleted)
            .SumAsync(d => d.Amount, ct);

        var returns = await _returns.Query()
            .Where(r => !r.IsDeleted && r.ReturnDate >= shift.OpenedAt && r.ReturnDate <= end)
            .ToListAsync(ct);

        var holds = await _held.Query()
            .CountAsync(h => h.UserId == shift.UserId && h.HeldAt >= shift.OpenedAt && h.HeldAt <= end, ct);

        var expected = shift.OpeningFloat + cashSales - invoices.Sum(i => i.ChangeDue) - safeDrops;

        return Result<ShiftZReportDto>.Success(new ShiftZReportDto(
            shift.Id,
            shift.ShiftNumber,
            shift.OpenedAt,
            shift.ClosedAt,
            shift.OpeningFloat,
            shift.ClosingFloat,
            expected,
            tenderTotals,
            invoices.Count,
            returns.Count,
            holds,
            invoices.Sum(i => i.GrandTotal),
            returns.Sum(r => r.GrandTotal),
            safeDrops,
            shift.TillId));
    }

    public async Task<IReadOnlyList<TillDto>> ListTillsAsync(int? branchId = null, CancellationToken ct = default)
    {
        var q = _tills.Query().Where(t => !t.IsDeleted && t.IsActive);
        if (branchId.HasValue)
            q = q.Where(t => t.BranchId == branchId.Value);
        else if (_company.AllowedBranchIds.Count > 0)
            q = q.Where(t => _company.AllowedBranchIds.Contains(t.BranchId));

        var list = await q.OrderBy(t => t.Code).ToListAsync(ct);
        return list
            .Where(t => _company.IsBranchAllowed(t.BranchId))
            .Select(t => new TillDto(t.Id, t.BranchId, t.Code, t.Name, t.IsActive, t.WarehouseId))
            .ToList();
    }

    public async Task<Result<TillDto>> UpsertTillAsync(UpsertTillRequest dto, CancellationToken ct = default)
    {
        if (!_currentUser.HasPermission(Permissions.SettingsManage) && !_currentUser.HasPermission(Permissions.PlatformManage))
            return Result<TillDto>.Failure("Settings permission required to manage tills.");

        if (!_company.IsBranchAllowed(dto.BranchId))
            return Result<TillDto>.Failure("Branch is not allowed for this user.");

        if (!await _branches.ExistsAsync(b => b.Id == dto.BranchId && !b.IsDeleted, ct))
            return Result<TillDto>.Failure("Branch not found.");

        var code = dto.Code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
            return Result<TillDto>.Failure("Till code is required.");

        var existing = await _tills.Query()
            .FirstOrDefaultAsync(t => t.BranchId == dto.BranchId && t.Code == code && !t.IsDeleted, ct);

        if (existing is null)
        {
            existing = new Till
            {
                CompanyId = _company.CompanyId ?? 0,
                BranchId = dto.BranchId,
                Code = code,
                Name = dto.Name.Trim(),
                WarehouseId = dto.WarehouseId,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow
            };
            _tills.Add(existing);
        }
        else
        {
            existing.Name = dto.Name.Trim();
            existing.WarehouseId = dto.WarehouseId;
            existing.IsActive = dto.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
            _tills.Update(existing);
        }

        await _uow.SaveChangesAsync(ct);
        return Result<TillDto>.Success(new TillDto(existing.Id, existing.BranchId, existing.Code, existing.Name, existing.IsActive, existing.WarehouseId));
    }

    public async Task<Result<SafeDropDto>> RecordSafeDropAsync(int shiftId, SafeDropRequest dto, CancellationToken ct = default)
    {
        if (!_currentUser.HasPermission(Permissions.PosShift) && !_currentUser.HasPermission(Permissions.PosCheckout))
            return Result<SafeDropDto>.Failure("Shift permission required.");
        if (dto.Amount <= 0)
            return Result<SafeDropDto>.Failure("Safe drop amount must be positive.");

        var userId = _currentUser.CurrentUser?.Id ?? 0;
        var shift = await _shifts.Query().FirstOrDefaultAsync(s => s.Id == shiftId && !s.IsDeleted, ct);
        if (shift is null)
            return Result<SafeDropDto>.Failure("Shift not found.");
        if (shift.UserId != userId)
            return Result<SafeDropDto>.Failure("Shift belongs to another cashier.");
        if (shift.Status != CashierShiftStatus.Open)
            return Result<SafeDropDto>.Failure("Shift is closed.");
        if (shift.TillId is not int tillId)
            return Result<SafeDropDto>.Failure("Shift has no till.");

        var drop = new SafeDrop
        {
            CashierShiftId = shift.Id,
            TillId = tillId,
            Amount = dto.Amount,
            Notes = dto.Notes,
            DroppedAt = DateTime.UtcNow,
            CreatedByUserName = _currentUser.CurrentUser?.Username,
            CreatedAt = DateTime.UtcNow
        };
        _safeDrops.Add(drop);
        await _uow.SaveChangesAsync(ct);

        return Result<SafeDropDto>.Success(new SafeDropDto(drop.Id, drop.CashierShiftId, drop.TillId, drop.Amount, drop.DroppedAt, drop.Notes));
    }

    public async Task<IReadOnlyList<SafeDropDto>> ListSafeDropsAsync(int shiftId, CancellationToken ct = default)
    {
        var drops = await _safeDrops.Query()
            .Where(d => d.CashierShiftId == shiftId && !d.IsDeleted)
            .OrderByDescending(d => d.DroppedAt)
            .ToListAsync(ct);
        return drops.Select(d => new SafeDropDto(d.Id, d.CashierShiftId, d.TillId, d.Amount, d.DroppedAt, d.Notes)).ToList();
    }

    private async Task<CashierShiftDto> MapShiftAsync(CashierShift s, CancellationToken ct)
    {
        string? tillCode = null;
        if (s.TillId is int tid)
        {
            tillCode = await _tills.Query().Where(t => t.Id == tid).Select(t => t.Code).FirstOrDefaultAsync(ct);
        }

        var drops = await _safeDrops.Query()
            .Where(d => d.CashierShiftId == s.Id && !d.IsDeleted)
            .SumAsync(d => (decimal?)d.Amount, ct) ?? 0;

        return new CashierShiftDto(
            s.Id,
            s.ShiftNumber,
            s.UserId,
            s.UserName,
            s.WarehouseId,
            s.Status.ToString(),
            s.OpeningFloat,
            s.ClosingFloat,
            s.OpenedAt,
            s.ClosedAt,
            s.BranchId,
            s.ExpectedCash,
            s.DeclaredClosingCash,
            s.CashVariance,
            s.VarianceJournalEntryId,
            s.TillId,
            tillCode,
            drops);
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
}
