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
    private readonly IRepository<WarehouseLocation> _locations;
    private readonly IRepository<InventoryLocationBalance> _locationBalances;
    private readonly IRepository<CompanySettings> _settings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IValidator<StockAdjustmentDto> _adjustmentValidator;
    private readonly INotificationService _notifications;
    private readonly ICurrentCompanyContext _company;

    public InventoryService(
        IRepository<InventoryItem> inventory,
        IRepository<StockMovement> movements,
        IRepository<StockBatch> batches,
        IRepository<Product> products,
        IRepository<Warehouse> warehouses,
        IRepository<WarehouseLocation> locations,
        IRepository<InventoryLocationBalance> locationBalances,
        IRepository<CompanySettings> settings,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IValidator<StockAdjustmentDto> adjustmentValidator,
        INotificationService notifications,
        ICurrentCompanyContext company)
    {
        _inventory = inventory;
        _movements = movements;
        _batches = batches;
        _products = products;
        _warehouses = warehouses;
        _locations = locations;
        _locationBalances = locationBalances;
        _settings = settings;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _adjustmentValidator = adjustmentValidator;
        _notifications = notifications;
        _company = company;
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

        var deduct = await DeductStockAsync(
            dto.ProductId,
            dto.WarehouseId,
            Math.Abs(dto.QuantityDelta),
            "Adjustment",
            0,
            ct);
        return deduct.Succeeded ? Result.Success() : Result.Failure(deduct.Error ?? "Adjustment failed.");
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
        await EvaluateStockAlertsAsync(productId, warehouseId, ct);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<decimal>> DeductStockAsync(
        int productId,
        int warehouseId,
        decimal quantity,
        string referenceType,
        int referenceId,
        CancellationToken ct = default)
    {
        if (quantity <= 0)
            return Result<decimal>.Failure("Quantity must be positive.");

        var settings = await _settings.Query().AsNoTracking().FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        var allowNegative = settings?.AllowNegativeStock == true;

        var item = await _inventory.Query()
            .Include(i => i.Batches)
            .FirstOrDefaultAsync(i => i.ProductId == productId && i.WarehouseId == warehouseId && !i.IsDeleted, ct);

        if (item is null)
        {
            if (!allowNegative)
                return Result<decimal>.Failure("Insufficient stock.");
            item = await GetOrCreateInventoryItemAsync(productId, warehouseId, ct);
        }

        var available = item.QuantityOnHand - item.ReservedQuantity;
        if (!allowNegative && available < quantity)
            return Result<decimal>.Failure("Insufficient stock.");

        var remaining = quantity;
        decimal totalCost = 0;
        decimal unitCost;

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
                return Result<decimal>.Failure("Insufficient batch stock for FIFO deduction.");

            unitCost = quantity > 0 ? totalCost / quantity : item.AverageCost;
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
            unitCost = item.AverageCost;
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
        await EvaluateStockAlertsAsync(productId, warehouseId, ct);
        return Result<decimal>.Success(unitCost);
    }

    /// <inheritdoc />
    public async Task<Result<decimal>> TransferOutAsync(
        int productId,
        int warehouseId,
        decimal quantity,
        int transferId,
        int? fromLocationId = null,
        CancellationToken ct = default)
    {
        if (quantity <= 0)
            return Result<decimal>.Failure("Quantity must be positive.");

        var settings = await _settings.Query().AsNoTracking().FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        var allowNegative = settings?.AllowNegativeStock == true;

        var item = await _inventory.Query()
            .Include(i => i.Batches)
            .FirstOrDefaultAsync(i => i.ProductId == productId && i.WarehouseId == warehouseId && !i.IsDeleted, ct);

        if (item is null)
            return Result<decimal>.Failure("Insufficient stock.");

        var available = item.QuantityOnHand - item.ReservedQuantity;
        if (!allowNegative && available < quantity)
            return Result<decimal>.Failure("Insufficient stock.");

        decimal unitCost;
        if (item.ValuationMethod == ValuationMethod.Fifo && !allowNegative)
        {
            var remaining = quantity;
            decimal totalCost = 0;
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
                return Result<decimal>.Failure("Insufficient batch stock for FIFO deduction.");

            unitCost = quantity > 0 ? totalCost / quantity : item.AverageCost;
        }
        else
        {
            unitCost = item.AverageCost;
        }

        item.QuantityOnHand -= quantity;
        item.UpdatedAt = DateTime.UtcNow;
        _inventory.Update(item);

        _movements.Add(new StockMovement
        {
            InventoryItemId = item.Id,
            MovementType = StockMovementType.Transfer,
            Quantity = -quantity,
            UnitCost = unitCost,
            ReferenceType = nameof(InventoryTransfer),
            ReferenceId = transferId,
            MovementDate = DateTime.UtcNow
        });

        try
        {
            var companyId = _company.CompanyId ?? 0;
            var locId = await LocationBalanceSync.ResolvePickLocationIdAsync(
                _locations.Query(), l => _locations.Add(l), warehouseId, companyId, fromLocationId, ct);
            var locErr = await LocationBalanceSync.DecreaseAsync(
                _locationBalances.Query(), b => _locationBalances.Add(b), item.Id, locId, quantity, allowNegative, ct);
            if (locErr is not null)
                return Result<decimal>.Failure(locErr);
        }
        catch (InvalidOperationException ex)
        {
            return Result<decimal>.Failure(ex.Message);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result<decimal>.Success(unitCost);
    }

    /// <inheritdoc />
    public async Task<Result> TransferInAsync(
        int productId,
        int warehouseId,
        decimal quantity,
        decimal unitCost,
        int transferId,
        int? toLocationId = null,
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
                BatchNumber = $"TR-{transferId}-{DateTime.UtcNow:yyyyMMddHHmmss}",
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
            MovementType = StockMovementType.Transfer,
            Quantity = quantity,
            UnitCost = unitCost,
            ReferenceType = nameof(InventoryTransfer),
            ReferenceId = transferId,
            MovementDate = DateTime.UtcNow
        });

        try
        {
            var companyId = _company.CompanyId ?? 0;
            var locId = await LocationBalanceSync.ResolveReceivingLocationIdAsync(
                _locations.Query(), l => _locations.Add(l), warehouseId, companyId, toLocationId, ct);
            await LocationBalanceSync.IncreaseAsync(
                _locationBalances.Query(), b => _locationBalances.Add(b), item.Id, locId, quantity, ct);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
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
        var detail = await GetInventoryValueAsync("Average", null, null, ct);
        return detail.Value;
    }

    /// <inheritdoc />
    public async Task<InventoryValueDto> GetInventoryValueAsync(
        string? method,
        int? warehouseId,
        int? branchId,
        CancellationToken ct = default)
    {
        var useFifo = string.Equals(method, "Fifo", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "FIFO", StringComparison.OrdinalIgnoreCase);
        var resolvedMethod = useFifo ? "Fifo" : "Average";

        var q = _inventory.Query().Where(i => !i.IsDeleted);
        if (warehouseId.HasValue)
            q = q.Where(i => i.WarehouseId == warehouseId);
        else if (branchId.HasValue)
            q = q.Where(i => i.Warehouse != null && i.Warehouse.BranchId == branchId);

        decimal value;
        if (useFifo)
        {
            value = await q
                .SelectMany(i => i.Batches.Where(b => !b.IsDeleted))
                .SumAsync(b => b.QuantityRemaining * b.UnitCost, ct);

            // Items without batches still contribute via average for residual qty
            var residual = await q
                .Where(i => !i.Batches.Any(b => !b.IsDeleted && b.QuantityRemaining > 0))
                .SumAsync(i => i.QuantityOnHand * i.AverageCost, ct);
            value += residual;
        }
        else
        {
            value = await q.SumAsync(i => i.QuantityOnHand * i.AverageCost, ct);
        }

        return new InventoryValueDto(value, resolvedMethod, warehouseId, branchId);
    }

    private async Task EvaluateStockAlertsAsync(int productId, int warehouseId, CancellationToken ct)
    {
        var item = await _inventory.Query()
            .Include(i => i.Product)
            .Include(i => i.Warehouse)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.ProductId == productId && i.WarehouseId == warehouseId && !i.IsDeleted, ct);
        if (item?.Product is null) return;

        var onHand = item.QuantityOnHand;
        var reorder = item.Product.ReorderLevel;
        var overThreshold = item.Product.ReorderLevel > 0 ? item.Product.ReorderLevel * 3 : 0;

        if (reorder > 0 && onHand <= reorder)
        {
            await EnsureStockNotificationAsync(
                NotificationType.LowStock,
                productId,
                warehouseId,
                $"Low stock: {item.Product.Name}",
                $"{item.Product.Sku} at {item.Warehouse?.Name}: {onHand:0.##} (reorder {reorder})",
                ct);
        }

        if (overThreshold > 0 && onHand >= overThreshold)
        {
            await EnsureStockNotificationAsync(
                NotificationType.Overstock,
                productId,
                warehouseId,
                $"Overstock: {item.Product.Name}",
                $"{item.Product.Sku} at {item.Warehouse?.Name}: {onHand:0.##} (threshold {overThreshold})",
                ct);
        }
    }

    private async Task EnsureStockNotificationAsync(
        NotificationType type,
        int productId,
        int warehouseId,
        string title,
        string message,
        CancellationToken ct)
    {
        var keyedMessage = $"{message} [warehouseId={warehouseId}]";
        var existing = await _notifications.GetNotificationsAsync(unreadOnly: true, ct);
        var already = existing.Any(n =>
            n.Type == type
            && n.RelatedEntityId == productId
            && string.Equals(n.RelatedEntityType, "InventoryItem", StringComparison.OrdinalIgnoreCase)
            && n.Message != null
            && n.Message.Contains($"[warehouseId={warehouseId}]", StringComparison.OrdinalIgnoreCase));

        if (already) return;

        await _notifications.CreateNotificationAsync(
            type, title, keyedMessage, "InventoryItem", productId, ct);
    }

    private async Task<InventoryItem> GetOrCreateInventoryItemAsync(int productId, int warehouseId, CancellationToken ct)
    {
        var existing = await _inventory.Query()
            .FirstOrDefaultAsync(i => i.ProductId == productId && i.WarehouseId == warehouseId && !i.IsDeleted, ct);

        if (existing != null)
            return existing;

        if (!await _warehouses.ExistsAsync(w => w.Id == warehouseId && !w.IsDeleted, ct))
            throw new InvalidOperationException("Warehouse not found.");

        var settings = await _settings.Query().AsNoTracking().FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        var item = new InventoryItem
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            ValuationMethod = settings?.DefaultValuationMethod ?? ValuationMethod.Average
        };

        _inventory.Add(item);
        await _unitOfWork.SaveChangesAsync(ct);
        return item;
    }
}
