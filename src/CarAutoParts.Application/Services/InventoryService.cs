using AutoMapper;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Inventory;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>Inventory operations with FIFO and weighted-average costing.</summary>
public class InventoryService : IInventoryService
{
    private readonly IRepository<InventoryItem> _inventory;
    private readonly IRepository<StockMovement> _movements;
    private readonly IRepository<StockBatch> _batches;
    private readonly IRepository<Product> _products;
    private readonly IRepository<Warehouse> _warehouses;
    private readonly IRepository<CompanySettings> _settings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IValidator<StockAdjustmentDto> _adjustmentValidator;

    public InventoryService(
        IRepository<InventoryItem> inventory,
        IRepository<StockMovement> movements,
        IRepository<StockBatch> batches,
        IRepository<Product> products,
        IRepository<Warehouse> warehouses,
        IRepository<CompanySettings> settings,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IValidator<StockAdjustmentDto> adjustmentValidator)
    {
        _inventory = inventory;
        _movements = movements;
        _batches = batches;
        _products = products;
        _warehouses = warehouses;
        _settings = settings;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _adjustmentValidator = adjustmentValidator;
    }

    /// <inheritdoc />
    public async Task<PagedResult<InventoryItemDto>> GetInventoryAsync(InventoryQueryDto query, CancellationToken ct = default)
    {
        var q = _inventory.Query()
            .Include(i => i.Product)
            .Include(i => i.Warehouse)
            .Where(i => !i.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(i => i.Product.Name.Contains(s) || i.Product.Sku.Contains(s));
        }

        if (query.ProductId.HasValue)
            q = q.Where(i => i.ProductId == query.ProductId);

        if (query.WarehouseId.HasValue)
            q = q.Where(i => i.WarehouseId == query.WarehouseId);

        if (query.LowStockOnly)
            q = q.Where(i => i.QuantityOnHand <= i.Product.MinimumStock);

        q = q.OrderBy(i => i.Product.Name);
        var paged = await q.ToPagedResultAsync(query.Page, query.PageSize, ct);

        return new PagedResult<InventoryItemDto>
        {
            Items = _mapper.Map<List<InventoryItemDto>>(paged.Items),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    /// <inheritdoc />
    public async Task<PagedResult<StockMovementDto>> GetMovementsAsync(StockMovementQueryDto query, CancellationToken ct = default)
    {
        var q = _movements.Query()
            .Include(m => m.InventoryItem).ThenInclude(i => i.Product)
            .Include(m => m.InventoryItem).ThenInclude(i => i.Warehouse)
            .Where(m => !m.IsDeleted);

        if (query.ProductId.HasValue)
            q = q.Where(m => m.InventoryItem.ProductId == query.ProductId);

        if (query.WarehouseId.HasValue)
            q = q.Where(m => m.InventoryItem.WarehouseId == query.WarehouseId);

        if (query.MovementType.HasValue)
            q = q.Where(m => m.MovementType == query.MovementType);

        if (query.FromDate.HasValue)
            q = q.Where(m => m.MovementDate >= query.FromDate);

        if (query.ToDate.HasValue)
            q = q.Where(m => m.MovementDate <= query.ToDate);

        q = q.OrderByDescending(m => m.MovementDate);
        var paged = await q.ToPagedResultAsync(query.Page, query.PageSize, ct);

        return new PagedResult<StockMovementDto>
        {
            Items = _mapper.Map<List<StockMovementDto>>(paged.Items),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    /// <inheritdoc />
    public async Task<Result> AdjustStockAsync(StockAdjustmentDto dto, CancellationToken ct = default)
    {
        var validation = await _adjustmentValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        if (dto.QuantityDelta > 0)
            return await ReceiveStockAsync(dto.ProductId, dto.WarehouseId, dto.QuantityDelta, 0, null, ct);

        return await DeductStockAsync(
            dto.ProductId,
            dto.WarehouseId,
            Math.Abs(dto.QuantityDelta),
            "Adjustment",
            0,
            ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LowStockAlertDto>> GetLowStockAlertsAsync(CancellationToken ct = default)
    {
        return await _inventory.Query()
            .Include(i => i.Product)
            .Include(i => i.Warehouse)
            .Where(i => !i.IsDeleted && i.QuantityOnHand <= i.Product.MinimumStock)
            .Select(i => new LowStockAlertDto(
                i.ProductId,
                i.Product.Name,
                i.Product.Sku,
                i.WarehouseId,
                i.Warehouse.Name,
                i.QuantityOnHand,
                i.Product.MinimumStock,
                i.Product.ReorderLevel,
                false))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LowStockAlertDto>> GetOverstockAlertsAsync(CancellationToken ct = default)
    {
        return await _inventory.Query()
            .Include(i => i.Product)
            .Include(i => i.Warehouse)
            .Where(i => !i.IsDeleted && i.Product.ReorderLevel > 0 && i.QuantityOnHand > i.Product.ReorderLevel * 3)
            .Select(i => new LowStockAlertDto(
                i.ProductId,
                i.Product.Name,
                i.Product.Sku,
                i.WarehouseId,
                i.Warehouse.Name,
                i.QuantityOnHand,
                i.Product.MinimumStock,
                i.Product.ReorderLevel,
                true))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Result> ReceiveStockAsync(
        int productId,
        int warehouseId,
        decimal quantity,
        decimal unitCost,
        string? batchNumber,
        CancellationToken ct = default)
    {
        if (quantity <= 0)
            return Result.Failure("Quantity must be positive.");

        var item = await GetOrCreateInventoryItemAsync(productId, warehouseId, ct);
        var product = await _products.GetByIdAsync(productId, ct);
        if (product is null || product.IsDeleted)
            return Result.Failure("Product not found.");

        if (item.ValuationMethod == ValuationMethod.Average)
        {
            var totalValue = item.QuantityOnHand * item.AverageCost + quantity * unitCost;
            var totalQty = item.QuantityOnHand + quantity;
            item.AverageCost = totalQty > 0 ? totalValue / totalQty : unitCost;
        }

        if (item.ValuationMethod == ValuationMethod.Fifo || product.TrackBatches)
        {
            _batches.Add(new StockBatch
            {
                InventoryItemId = item.Id,
                BatchNumber = string.IsNullOrWhiteSpace(batchNumber) ? $"B-{DateTime.UtcNow:yyyyMMddHHmmss}" : batchNumber,
                QuantityRemaining = quantity,
                UnitCost = unitCost,
                ReceivedDate = DateTime.UtcNow
            });
        }

        item.QuantityOnHand += quantity;
        item.UpdatedAt = DateTime.UtcNow;
        _inventory.Update(item);

        _movements.Add(new StockMovement
        {
            InventoryItemId = item.Id,
            MovementType = StockMovementType.Purchase,
            Quantity = quantity,
            UnitCost = unitCost,
            ReferenceType = "Receive",
            MovementDate = DateTime.UtcNow
        });

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> DeductStockAsync(
        int productId,
        int warehouseId,
        decimal quantity,
        string referenceType,
        int referenceId,
        CancellationToken ct = default)
    {
        if (quantity <= 0)
            return Result.Failure("Quantity must be positive.");

        var settings = await _settings.Query().AsNoTracking().FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        var allowNegative = settings?.AllowNegativeStock == true;

        var item = await _inventory.Query()
            .Include(i => i.Batches)
            .FirstOrDefaultAsync(i => i.ProductId == productId && i.WarehouseId == warehouseId && !i.IsDeleted, ct);

        if (item is null)
        {
            if (!allowNegative)
                return Result.Failure("Insufficient stock.");
            item = await GetOrCreateInventoryItemAsync(productId, warehouseId, ct);
        }

        var available = item.QuantityOnHand - item.ReservedQuantity;
        if (!allowNegative && available < quantity)
            return Result.Failure("Insufficient stock.");

        var remaining = quantity;
        decimal totalCost = 0;

        if (item.ValuationMethod == ValuationMethod.Fifo && !allowNegative)
        {
            var batches = item.Batches
                .Where(b => !b.IsDeleted && b.QuantityRemaining > 0)
                .OrderBy(b => b.ReceivedDate)
                .ThenBy(b => b.Id)
                .ToList();

            foreach (var batch in batches)
            {
                if (remaining <= 0) break;

                var take = Math.Min(remaining, batch.QuantityRemaining);
                batch.QuantityRemaining -= take;
                totalCost += take * batch.UnitCost;
                remaining -= take;
                _batches.Update(batch);
            }

            if (remaining > 0)
                return Result.Failure("Insufficient batch stock for FIFO deduction.");

            var unitCost = quantity > 0 ? totalCost / quantity : item.AverageCost;
            item.QuantityOnHand -= quantity;
            item.UpdatedAt = DateTime.UtcNow;
            _inventory.Update(item);

            _movements.Add(new StockMovement
            {
                InventoryItemId = item.Id,
                MovementType = StockMovementType.Sale,
                Quantity = -quantity,
                UnitCost = unitCost,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                MovementDate = DateTime.UtcNow
            });
        }
        else
        {
            var unitCost = item.AverageCost;
            item.QuantityOnHand -= quantity;
            item.UpdatedAt = DateTime.UtcNow;
            _inventory.Update(item);

            _movements.Add(new StockMovement
            {
                InventoryItemId = item.Id,
                MovementType = StockMovementType.Sale,
                Quantity = -quantity,
                UnitCost = unitCost,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                MovementDate = DateTime.UtcNow
            });
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> ReturnStockAsync(
        int productId,
        int warehouseId,
        decimal quantity,
        string referenceType,
        int referenceId,
        CancellationToken ct = default)
    {
        return await ReceiveStockAsync(productId, warehouseId, quantity, 0, null, ct);
    }

    /// <inheritdoc />
    public async Task<decimal> GetInventoryValueAsync(CancellationToken ct = default)
    {
        return await _inventory.Query()
            .Where(i => !i.IsDeleted)
            .SumAsync(i => i.QuantityOnHand * i.AverageCost, ct);
    }

    private async Task<InventoryItem> GetOrCreateInventoryItemAsync(int productId, int warehouseId, CancellationToken ct)
    {
        var existing = await _inventory.Query()
            .FirstOrDefaultAsync(i => i.ProductId == productId && i.WarehouseId == warehouseId && !i.IsDeleted, ct);

        if (existing != null)
            return existing;

        if (!await _warehouses.ExistsAsync(w => w.Id == warehouseId && !w.IsDeleted, ct))
            throw new InvalidOperationException("Warehouse not found.");

        var item = new InventoryItem
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            ValuationMethod = ValuationMethod.Average
        };

        _inventory.Add(item);
        await _unitOfWork.SaveChangesAsync(ct);
        return item;
    }
}
