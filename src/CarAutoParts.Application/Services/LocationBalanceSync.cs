using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>
/// Keeps bin balances in sync with warehouse rollup.
/// ATP policy: available-to-promise stays warehouse-level (InventoryItem.QuantityOnHand − ReservedQuantity).
/// Location balances are the putaway/pick dimension and should sum to warehouse on-hand when bins are used.
/// </summary>
public static class LocationBalanceSync
{
    public const string DefaultCode = "MAIN";

    public static async Task<WarehouseLocation> EnsureDefaultLocationAsync(
        IQueryable<WarehouseLocation> locations,
        Action<WarehouseLocation> add,
        int warehouseId,
        int companyId,
        CancellationToken ct,
        bool receiving = true,
        bool pick = true)
    {
        var existing = await locations
            .FirstOrDefaultAsync(l =>
                l.WarehouseId == warehouseId &&
                l.Code == DefaultCode &&
                !l.IsDeleted, ct);

        if (existing is not null)
            return existing;

        var loc = new WarehouseLocation
        {
            WarehouseId = warehouseId,
            CompanyId = companyId,
            Code = DefaultCode,
            Name = "Main",
            IsReceivingDefault = receiving,
            IsPickDefault = pick,
            IsActive = true,
            SortOrder = 0
        };
        add(loc);
        return loc;
    }

    public static async Task<int> ResolveReceivingLocationIdAsync(
        IQueryable<WarehouseLocation> locations,
        Action<WarehouseLocation> add,
        int warehouseId,
        int companyId,
        int? preferredLocationId,
        CancellationToken ct)
    {
        if (preferredLocationId is int id)
        {
            var ok = await locations.AnyAsync(l =>
                l.Id == id && l.WarehouseId == warehouseId && l.IsActive && !l.IsDeleted, ct);
            if (!ok)
                throw new InvalidOperationException("Putaway location is invalid for this warehouse.");
            return id;
        }

        var receiving = await locations
            .Where(l => l.WarehouseId == warehouseId && l.IsActive && !l.IsDeleted)
            .OrderByDescending(l => l.IsReceivingDefault)
            .ThenBy(l => l.SortOrder)
            .ThenBy(l => l.Id)
            .FirstOrDefaultAsync(ct);

        if (receiving is not null)
            return receiving.Id;

        var created = await EnsureDefaultLocationAsync(locations, add, warehouseId, companyId, ct);
        return created.Id;
    }

    public static async Task<int> ResolvePickLocationIdAsync(
        IQueryable<WarehouseLocation> locations,
        Action<WarehouseLocation> add,
        int warehouseId,
        int companyId,
        int? preferredLocationId,
        CancellationToken ct)
    {
        if (preferredLocationId is int id)
        {
            var ok = await locations.AnyAsync(l =>
                l.Id == id && l.WarehouseId == warehouseId && l.IsActive && !l.IsDeleted, ct);
            if (!ok)
                throw new InvalidOperationException("Pick location is invalid for this warehouse.");
            return id;
        }

        var pick = await locations
            .Where(l => l.WarehouseId == warehouseId && l.IsActive && !l.IsDeleted)
            .OrderByDescending(l => l.IsPickDefault)
            .ThenBy(l => l.SortOrder)
            .ThenBy(l => l.Id)
            .FirstOrDefaultAsync(ct);

        if (pick is not null)
            return pick.Id;

        var created = await EnsureDefaultLocationAsync(locations, add, warehouseId, companyId, ct);
        return created.Id;
    }

    public static async Task IncreaseAsync(
        IQueryable<InventoryLocationBalance> balances,
        Action<InventoryLocationBalance> add,
        int inventoryItemId,
        int warehouseLocationId,
        decimal quantity,
        CancellationToken ct)
    {
        if (quantity == 0) return;

        var bal = await balances.FirstOrDefaultAsync(b =>
            b.InventoryItemId == inventoryItemId &&
            b.WarehouseLocationId == warehouseLocationId &&
            !b.IsDeleted, ct);

        if (bal is null)
        {
            add(new InventoryLocationBalance
            {
                InventoryItemId = inventoryItemId,
                WarehouseLocationId = warehouseLocationId,
                QuantityOnHand = quantity
            });
            return;
        }

        bal.QuantityOnHand += quantity;
        bal.UpdatedAt = DateTime.UtcNow;
    }

    public static async Task<string?> DecreaseAsync(
        IQueryable<InventoryLocationBalance> balances,
        Action<InventoryLocationBalance> add,
        int inventoryItemId,
        int warehouseLocationId,
        decimal quantity,
        bool allowNegative,
        CancellationToken ct)
    {
        if (quantity <= 0) return null;

        var bal = await balances.FirstOrDefaultAsync(b =>
            b.InventoryItemId == inventoryItemId &&
            b.WarehouseLocationId == warehouseLocationId &&
            !b.IsDeleted, ct);

        if (bal is null)
        {
            // Legacy / unallocated warehouse stock: first pick claims qty into this bin.
            add(new InventoryLocationBalance
            {
                InventoryItemId = inventoryItemId,
                WarehouseLocationId = warehouseLocationId,
                QuantityOnHand = allowNegative ? -quantity : 0
            });
            return null;
        }

        if (!allowNegative && bal.QuantityOnHand < quantity)
            return "Insufficient location stock.";

        bal.QuantityOnHand -= quantity;
        bal.UpdatedAt = DateTime.UtcNow;
        return null;
    }
}
