using CarAutoParts.Application.Common;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

public record AtpLineDto(
    int ProductId,
    string Sku,
    string Name,
    int WarehouseId,
    string WarehouseName,
    decimal OnHand,
    decimal Reserved,
    decimal Available);

public interface IAtpService
{
    Task<decimal> GetAvailableAsync(int productId, int warehouseId, CancellationToken ct = default);
    Task<IReadOnlyList<AtpLineDto>> GetAsync(int? productId = null, int? warehouseId = null, CancellationToken ct = default);
    Task<Result> EnsureAvailableAsync(int productId, int warehouseId, decimal quantity, CancellationToken ct = default);
}

/// <summary>Available-to-promise = on-hand − reserved (respects AllowNegativeStock policy for ensure).</summary>
public sealed class AtpService : IAtpService
{
    private readonly IEnterpriseDb _db;

    public AtpService(IEnterpriseDb db) => _db = db;

    public async Task<decimal> GetAvailableAsync(int productId, int warehouseId, CancellationToken ct = default)
    {
        var item = await _db.InventoryItems.AsNoTracking()
            .FirstOrDefaultAsync(i => i.ProductId == productId && i.WarehouseId == warehouseId && !i.IsDeleted, ct);
        if (item is null) return 0;
        return item.QuantityOnHand - item.ReservedQuantity;
    }

    public async Task<IReadOnlyList<AtpLineDto>> GetAsync(int? productId = null, int? warehouseId = null, CancellationToken ct = default)
    {
        var q = _db.InventoryItems.AsNoTracking()
            .Include(i => i.Product)
            .Include(i => i.Warehouse)
            .Where(i => !i.IsDeleted && i.Product != null && !i.Product.IsDeleted);

        if (productId is int pid) q = q.Where(i => i.ProductId == pid);
        if (warehouseId is int wid) q = q.Where(i => i.WarehouseId == wid);

        var items = await q.OrderBy(i => i.Product!.Sku).ThenBy(i => i.WarehouseId).Take(500).ToListAsync(ct);
        return items.Select(i => new AtpLineDto(
            i.ProductId,
            i.Product!.Sku,
            i.Product.Name,
            i.WarehouseId,
            i.Warehouse?.Name ?? i.WarehouseId.ToString(),
            i.QuantityOnHand,
            i.ReservedQuantity,
            i.QuantityOnHand - i.ReservedQuantity)).ToList();
    }

    public async Task<Result> EnsureAvailableAsync(int productId, int warehouseId, decimal quantity, CancellationToken ct = default)
    {
        if (quantity <= 0) return Result.Success();
        var settings = await _db.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings?.AllowNegativeStock == true) return Result.Success();

        var available = await GetAvailableAsync(productId, warehouseId, ct);
        if (available < quantity)
            return Result.Failure($"Insufficient available stock for product {productId} at warehouse {warehouseId} (need {quantity}, ATP {available}).");
        return Result.Success();
    }
}
