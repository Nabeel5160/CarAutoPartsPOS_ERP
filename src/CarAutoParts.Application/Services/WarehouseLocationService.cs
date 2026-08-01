using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Products;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

public sealed class WarehouseLocationService : IWarehouseLocationService
{
    private readonly IRepository<Warehouse> _warehouses;
    private readonly IRepository<WarehouseLocation> _locations;
    private readonly IRepository<InventoryLocationBalance> _balances;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentCompanyContext _company;

    public WarehouseLocationService(
        IRepository<Warehouse> warehouses,
        IRepository<WarehouseLocation> locations,
        IRepository<InventoryLocationBalance> balances,
        IUnitOfWork unitOfWork,
        ICurrentCompanyContext company)
    {
        _warehouses = warehouses;
        _locations = locations;
        _balances = balances;
        _unitOfWork = unitOfWork;
        _company = company;
    }

    public async Task<IReadOnlyList<WarehouseLocationDto>> GetByWarehouseAsync(int warehouseId, CancellationToken ct = default)
    {
        var items = await _locations.Query()
            .Where(l => l.WarehouseId == warehouseId && !l.IsDeleted)
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.Code)
            .ToListAsync(ct);
        return items.Select(Map).ToList();
    }

    public async Task<Result<WarehouseLocationDto>> CreateAsync(int warehouseId, UpsertWarehouseLocationDto dto, CancellationToken ct = default)
    {
        var wh = await _warehouses.Query().FirstOrDefaultAsync(w => w.Id == warehouseId && !w.IsDeleted, ct);
        if (wh is null)
            return Result<WarehouseLocationDto>.Failure("Warehouse not found.");

        if (wh.BranchId is int branchId && !_company.IsBranchAllowed(branchId))
            return Result<WarehouseLocationDto>.Failure("Warehouse branch is not allowed for this user.");

        var code = (dto.Code ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
            return Result<WarehouseLocationDto>.Failure("Location code is required.");
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<WarehouseLocationDto>.Failure("Location name is required.");

        if (await _locations.Query().AnyAsync(l => l.WarehouseId == warehouseId && l.Code == code && !l.IsDeleted, ct))
            return Result<WarehouseLocationDto>.Failure($"Location code '{code}' already exists in this warehouse.");

        if (dto.IsReceivingDefault)
            await ClearReceivingDefaultAsync(warehouseId, ct);
        if (dto.IsPickDefault)
            await ClearPickDefaultAsync(warehouseId, ct);

        var entity = new WarehouseLocation
        {
            WarehouseId = warehouseId,
            CompanyId = wh.CompanyId,
            Code = code,
            Name = dto.Name.Trim(),
            IsReceivingDefault = dto.IsReceivingDefault,
            IsPickDefault = dto.IsPickDefault,
            IsActive = dto.IsActive,
            SortOrder = dto.SortOrder
        };
        _locations.Add(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<WarehouseLocationDto>.Success(Map(entity));
    }

    public async Task<Result<WarehouseLocationDto>> UpdateAsync(int locationId, UpsertWarehouseLocationDto dto, CancellationToken ct = default)
    {
        var entity = await _locations.Query().FirstOrDefaultAsync(l => l.Id == locationId && !l.IsDeleted, ct);
        if (entity is null)
            return Result<WarehouseLocationDto>.Failure("Location not found.");

        var code = (dto.Code ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
            return Result<WarehouseLocationDto>.Failure("Location code is required.");
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<WarehouseLocationDto>.Failure("Location name is required.");

        if (await _locations.Query().AnyAsync(l =>
                l.WarehouseId == entity.WarehouseId && l.Code == code && l.Id != locationId && !l.IsDeleted, ct))
            return Result<WarehouseLocationDto>.Failure($"Location code '{code}' already exists in this warehouse.");

        if (dto.IsReceivingDefault)
            await ClearReceivingDefaultAsync(entity.WarehouseId, ct, exceptId: locationId);
        if (dto.IsPickDefault)
            await ClearPickDefaultAsync(entity.WarehouseId, ct, exceptId: locationId);

        entity.Code = code;
        entity.Name = dto.Name.Trim();
        entity.IsReceivingDefault = dto.IsReceivingDefault;
        entity.IsPickDefault = dto.IsPickDefault;
        entity.IsActive = dto.IsActive;
        entity.SortOrder = dto.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;
        _locations.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<WarehouseLocationDto>.Success(Map(entity));
    }

    public async Task<Result> DeleteAsync(int locationId, CancellationToken ct = default)
    {
        var entity = await _locations.Query().FirstOrDefaultAsync(l => l.Id == locationId && !l.IsDeleted, ct);
        if (entity is null)
            return Result.Failure("Location not found.");

        var hasQty = await _balances.Query().AnyAsync(b =>
            b.WarehouseLocationId == locationId && !b.IsDeleted && b.QuantityOnHand != 0, ct);
        if (hasQty)
            return Result.Failure("Cannot delete a location that still has stock balances.");

        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        _locations.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<IReadOnlyList<InventoryLocationBalanceDto>> GetBalancesAsync(
        int warehouseId,
        int? locationId = null,
        CancellationToken ct = default)
    {
        var q = _balances.Query()
            .Include(b => b.InventoryItem)
            .Include(b => b.WarehouseLocation)
            .Where(b => !b.IsDeleted && b.InventoryItem.WarehouseId == warehouseId && !b.InventoryItem.IsDeleted);

        if (locationId is int loc)
            q = q.Where(b => b.WarehouseLocationId == loc);

        var rows = await q.OrderBy(b => b.WarehouseLocation.Code).ThenBy(b => b.InventoryItem.ProductId).ToListAsync(ct);
        return rows.Select(b => new InventoryLocationBalanceDto(
            b.Id,
            b.InventoryItemId,
            b.InventoryItem.ProductId,
            b.InventoryItem.WarehouseId,
            b.WarehouseLocationId,
            b.WarehouseLocation.Code,
            b.WarehouseLocation.Name,
            b.QuantityOnHand)).ToList();
    }

    private async Task ClearReceivingDefaultAsync(int warehouseId, CancellationToken ct, int? exceptId = null)
    {
        var rows = await _locations.Query()
            .Where(l => l.WarehouseId == warehouseId && l.IsReceivingDefault && !l.IsDeleted)
            .ToListAsync(ct);
        foreach (var r in rows.Where(r => exceptId is null || r.Id != exceptId))
        {
            r.IsReceivingDefault = false;
            r.UpdatedAt = DateTime.UtcNow;
            _locations.Update(r);
        }
    }

    private async Task ClearPickDefaultAsync(int warehouseId, CancellationToken ct, int? exceptId = null)
    {
        var rows = await _locations.Query()
            .Where(l => l.WarehouseId == warehouseId && l.IsPickDefault && !l.IsDeleted)
            .ToListAsync(ct);
        foreach (var r in rows.Where(r => exceptId is null || r.Id != exceptId))
        {
            r.IsPickDefault = false;
            r.UpdatedAt = DateTime.UtcNow;
            _locations.Update(r);
        }
    }

    private static WarehouseLocationDto Map(WarehouseLocation l) => new(
        l.Id, l.WarehouseId, l.Code, l.Name, l.IsReceivingDefault, l.IsPickDefault, l.IsActive, l.SortOrder);
}
