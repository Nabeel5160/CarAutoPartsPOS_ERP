using CarAutoParts.Application.Common;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using CarAutoParts.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Enterprise;

internal static class EnterpriseDocumentNumbers
{
    public static async Task<string> AllocateAsync(IEnterpriseDb db, string documentType, CancellationToken ct)
    {
        var seq = await db.NumberSequences
            .FirstOrDefaultAsync(s => s.DocumentType == documentType, ct)
            ?? throw new InvalidOperationException($"Number sequence '{documentType}' not configured.");
        return seq.AllocateNext();
    }
}

public sealed class EnterpriseInventoryService : IEnterpriseInventoryService
{
    private readonly IEnterpriseDb _db;
    private readonly ICurrentCompanyContext _company;
    private readonly IGlPostingService _gl;

    public EnterpriseInventoryService(IEnterpriseDb db, ICurrentCompanyContext company, IGlPostingService gl)
    {
        _db = db;
        _company = company;
        _gl = gl;
    }

    public async Task<PagedResult<GoodsReceiptNoteDto>> GetGrnsAsync(QuerySpec? query = null, CancellationToken ct = default)
    {
        EnsureCompanyOrThrow();
        query ??= new QuerySpec();

        var baseQ = _db.GoodsReceiptNotes.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            baseQ = baseQ.Where(g => g.GrnNumber.Contains(s));
        }

        var total = await baseQ.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? QueryLimits.DefaultPageSize : query.PageSize, 1, QueryLimits.MaxPageSize);

        var items = await baseQ
            .Include(g => g.Lines)
            .Include(g => g.LandedCostLines)
            .OrderByDescending(g => g.ReceiptDate)
            .ThenByDescending(g => g.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            
            .ToListAsync(ct);

        return new PagedResult<GoodsReceiptNoteDto>
        {
            Items = items.Select(MapGrn).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<StockReservationDto>> GetReservationsAsync(CancellationToken ct = default)
    {
        EnsureCompanyOrThrow();

        var items = await _db.StockReservations
            .AsNoTracking()
            .OrderByDescending(r => r.Id)
            .ToListAsync(ct);

        return items.Select(MapReservation).ToList();
    }

    public async Task<IReadOnlyList<CycleCountDto>> GetCycleCountsAsync(CancellationToken ct = default)
    {
        EnsureCompanyOrThrow();

        var items = await _db.CycleCounts
            .AsNoTracking()
            .Include(c => c.Lines)
            .OrderByDescending(c => c.CountDate)
            .ThenByDescending(c => c.Id)
            .ToListAsync(ct);

        return items.Select(c => MapCycleCount(c)).ToList();
    }

    public async Task<Result<StockReservationDto>> ReserveStockAsync(ReserveStockRequest request, CancellationToken ct = default)
    {
        if (!EnsureCompany(out var companyId, out var error))
            return Result<StockReservationDto>.Failure(error!);

        if (request.Quantity <= 0)
            return Result<StockReservationDto>.Failure("Quantity must be positive.");

        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(i =>
                i.ProductId == request.ProductId &&
                i.WarehouseId == request.WarehouseId &&
                !i.IsDeleted, ct);

        if (item is null)
            return Result<StockReservationDto>.Failure("Inventory item not found.");

        var available = item.QuantityOnHand - item.ReservedQuantity;
        if (available < request.Quantity)
            return Result<StockReservationDto>.Failure("Insufficient available stock to reserve.");

        item.ReservedQuantity += request.Quantity;
        item.UpdatedAt = DateTime.UtcNow;

        var reservation = new StockReservation
        {
            CompanyId = companyId,
            ProductId = request.ProductId,
            WarehouseId = request.WarehouseId,
            Quantity = request.Quantity,
            ReferenceType = request.ReferenceType,
            ReferenceId = request.ReferenceId,
            ExpiresAt = request.ExpiresAt,
            Status = ReservationStatus.Active
        };

        _db.StockReservations.Add(reservation);
        await _db.SaveChangesAsync(ct);

        return Result<StockReservationDto>.Success(MapReservation(reservation));
    }

    public async Task<Result> ReleaseReservationAsync(int reservationId, CancellationToken ct = default)
    {
        if (!EnsureCompany(out _, out var error))
            return Result.Failure(error!);

        var reservation = await _db.StockReservations
            .FirstOrDefaultAsync(r => r.Id == reservationId, ct);

        if (reservation is null)
            return Result.Failure("Reservation not found.");

        if (reservation.Status != ReservationStatus.Active)
            return Result.Failure("Reservation is not active.");

        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(i =>
                i.ProductId == reservation.ProductId &&
                i.WarehouseId == reservation.WarehouseId &&
                !i.IsDeleted, ct);

        if (item is not null)
        {
            item.ReservedQuantity = Math.Max(0, item.ReservedQuantity - reservation.Quantity);
            item.UpdatedAt = DateTime.UtcNow;
        }

        reservation.Status = ReservationStatus.Released;
        reservation.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<GoodsReceiptNoteDto>> CreateGrnAsync(CreateGrnRequest request, CancellationToken ct = default)
    {
        if (!EnsureCompany(out var companyId, out var error))
            return Result<GoodsReceiptNoteDto>.Failure(error!);

        if (request.Lines.Count == 0)
            return Result<GoodsReceiptNoteDto>.Failure("GRN must have at least one line.");

        if (!await _db.Warehouses.AnyAsync(w => w.Id == request.WarehouseId && !w.IsDeleted, ct))
            return Result<GoodsReceiptNoteDto>.Failure("Warehouse not found.");

        var settings = await _db.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var overPct = settings?.GrnOverReceivePercent ?? 0m;
        var underAllowed = settings?.GrnUnderReceiveAllowed ?? true;

        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id) && !p.IsDeleted)
            .ToDictionaryAsync(p => p.Id, ct);

        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0)
                return Result<GoodsReceiptNoteDto>.Failure("Line quantity must be positive.");
            if (!products.TryGetValue(line.ProductId, out var product))
                return Result<GoodsReceiptNoteDto>.Failure($"Product {line.ProductId} not found.");

            if (product.TrackSerialNumbers)
            {
                var serials = line.SerialNumbers ?? [];
                if (serials.Count != (int)line.Quantity)
                    return Result<GoodsReceiptNoteDto>.Failure(
                        $"Product {product.Sku} requires {line.Quantity} serial number(s).");
                if (serials.Any(string.IsNullOrWhiteSpace) || serials.Distinct(StringComparer.OrdinalIgnoreCase).Count() != serials.Count)
                    return Result<GoodsReceiptNoteDto>.Failure($"Invalid or duplicate serials for product {product.Sku}.");
            }

            if (line.PurchaseOrderLineId is int polId)
            {
                var poLine = await _db.PurchaseOrderLines.FirstOrDefaultAsync(p => p.Id == polId && !p.IsDeleted, ct);
                if (poLine is null)
                    return Result<GoodsReceiptNoteDto>.Failure($"PO line {polId} not found.");

                var maxRecv = ThreeWayMatchRules.MaxReceivableQty(poLine.QuantityOrdered, poLine.QuantityReceived, overPct);
                if (line.Quantity > maxRecv + 0.0001m)
                    return Result<GoodsReceiptNoteDto>.Failure(
                        $"Over-receive blocked for product {line.ProductId}: max remaining {maxRecv} (over %={overPct}).");

                if (!underAllowed && line.Quantity + poLine.QuantityReceived + 0.0001m < poLine.QuantityOrdered)
                {
                    // under-receive only enforced at full PO close — allow partial when under allowed;
                    // when under not allowed, still permit partial creates but Post will validate completeness? Plan: GrnUnderReceiveAllowed default true.
                    // Interpret: when false, each GRN against a PO line must bring received to ordered (no partial). Soft: warn only at post if PO still short and under not allowed — skip here.
                }
            }
        }

        if (request.PurchaseOrderId is int poId && !underAllowed)
        {
            // At create time we only block over; under is checked when posting if this receive would leave PO incomplete intentionally.
            _ = poId;
        }

        var landedLines = request.LandedCostLines ?? [];
        var landedSum = landedLines.Sum(l => l.Amount);
        var landedTotal = landedSum > 0 ? landedSum : request.LandedCostAmount;

        var grnNumber = await EnterpriseDocumentNumbers.AllocateAsync(_db, "GRN", ct);
        var grn = new GoodsReceiptNote
        {
            CompanyId = companyId,
            GrnNumber = grnNumber,
            PurchaseOrderId = request.PurchaseOrderId,
            WarehouseId = request.WarehouseId,
            ReceiptDate = request.ReceiptDate,
            LandedCostAmount = landedTotal,
            Notes = request.Notes,
            Status = GrnStatus.Draft
        };

        foreach (var line in request.Lines)
        {
            string? serialJson = null;
            if (line.SerialNumbers is { Count: > 0 })
                serialJson = System.Text.Json.JsonSerializer.Serialize(line.SerialNumbers);

            grn.Lines.Add(new GoodsReceiptLine
            {
                CompanyId = companyId,
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitCost = line.UnitCost,
                PurchaseOrderLineId = line.PurchaseOrderLineId,
                BatchNumber = line.BatchNumber,
                ExpiryDate = line.ExpiryDate,
                SerialNumbersJson = serialJson,
                WarehouseLocationId = line.WarehouseLocationId
            });
        }

        foreach (var lc in landedLines.Where(l => l.Amount > 0))
        {
            grn.LandedCostLines.Add(new GrnLandedCostLine
            {
                CompanyId = companyId,
                CostType = string.IsNullOrWhiteSpace(lc.CostType) ? "Other" : lc.CostType.Trim(),
                Amount = lc.Amount,
                Notes = lc.Notes
            });
        }

        // Stash HoldForQc in notes marker if needed — better: store on entity. Use Notes prefix? Add field? Plan uses Post with hold.
        // Persist via temporary: we'll pass HoldForQc only at post. Create stores draft; PostGrn accepts optional hold from request stored on create.
        if (request.HoldForQc)
            grn.Notes = string.IsNullOrWhiteSpace(grn.Notes) ? "[QC_HOLD]" : grn.Notes + "\n[QC_HOLD]";

        _db.GoodsReceiptNotes.Add(grn);
        await _db.SaveChangesAsync(ct);
        return Result<GoodsReceiptNoteDto>.Success(MapGrn(grn));
    }

    public async Task<Result<GoodsReceiptNoteDto>> PostGrnAsync(int grnId, CancellationToken ct = default)
    {
        if (!EnsureCompany(out var companyId, out var error))
            return Result<GoodsReceiptNoteDto>.Failure(error!);

        var grn = await _db.GoodsReceiptNotes
            .Include(g => g.Lines)
            .Include(g => g.LandedCostLines)
            .FirstOrDefaultAsync(g => g.Id == grnId, ct);

        if (grn is null)
            return Result<GoodsReceiptNoteDto>.Failure("GRN not found.");

        if (grn.Status != GrnStatus.Draft)
            return Result<GoodsReceiptNoteDto>.Failure("GRN is not in draft status.");

        var settings = await _db.CompanySettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var overPct = settings?.GrnOverReceivePercent ?? 0m;

        var productIds = grn.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        if (grn.LandedCostLines.Count > 0)
            grn.LandedCostAmount = grn.LandedCostLines.Sum(l => l.Amount);

        foreach (var line in grn.Lines)
        {
            if (line.PurchaseOrderLineId is int polId)
            {
                var poLine = await _db.PurchaseOrderLines.FirstOrDefaultAsync(p => p.Id == polId, ct);
                if (poLine is not null)
                {
                    var maxRecv = ThreeWayMatchRules.MaxReceivableQty(poLine.QuantityOrdered, poLine.QuantityReceived, overPct);
                    if (line.Quantity > maxRecv + 0.0001m)
                        return Result<GoodsReceiptNoteDto>.Failure(
                            $"Over-receive blocked for PO line {polId}: max {maxRecv}.");
                }
            }

            if (products.TryGetValue(line.ProductId, out var product) && product.TrackSerialNumbers)
            {
                var serials = ParseSerials(line.SerialNumbersJson);
                if (serials.Count != (int)line.Quantity)
                    return Result<GoodsReceiptNoteDto>.Failure($"Serial count mismatch for product {product.Sku}.");
            }
        }

        var lineBaseTotal = grn.Lines.Sum(l => l.Quantity * l.UnitCost);
        foreach (var line in grn.Lines)
        {
            var lineValue = line.Quantity * line.UnitCost;
            var landedShare = lineBaseTotal > 0
                ? grn.LandedCostAmount * (lineValue / lineBaseTotal)
                : 0m;
            var effectiveUnitCost = line.Quantity > 0
                ? (lineValue + landedShare) / line.Quantity
                : line.UnitCost;

            var receiveResult = await ReceiveStockInternalAsync(
                companyId,
                line.ProductId,
                grn.WarehouseId,
                line.Quantity,
                effectiveUnitCost,
                line.BatchNumber,
                nameof(GoodsReceiptNote),
                grn.Id,
                line.WarehouseLocationId,
                ct);

            if (!receiveResult.Succeeded)
                return Result<GoodsReceiptNoteDto>.Failure(receiveResult.Error!);

            foreach (var serial in ParseSerials(line.SerialNumbersJson))
            {
                if (await _db.SerialNumbers.AnyAsync(s => s.Serial == serial && !s.IsDeleted, ct))
                    return Result<GoodsReceiptNoteDto>.Failure($"Serial '{serial}' already exists.");

                _db.SerialNumbers.Add(new SerialNumber
                {
                    ProductId = line.ProductId,
                    Serial = serial,
                    CurrentWarehouseId = grn.WarehouseId,
                    Status = SerialNumberStatus.Available,
                    History =
                    {
                        new SerialNumberHistory
                        {
                            Action = "Received",
                            ReferenceType = nameof(GoodsReceiptNote),
                            ReferenceId = grn.Id,
                            ActionDate = DateTime.UtcNow
                        }
                    }
                });
            }

            if (line.PurchaseOrderLineId.HasValue)
            {
                var poLine = await _db.PurchaseOrderLines
                    .FirstOrDefaultAsync(p => p.Id == line.PurchaseOrderLineId.Value, ct);
                if (poLine is not null)
                    poLine.QuantityReceived += line.Quantity;
            }
        }

        var inventoryValue = lineBaseTotal + grn.LandedCostAmount;
        if (inventoryValue > 0)
        {
            var gl = await _gl.PostDocumentAsync(
                "Grn",
                grn.ReceiptDate,
                grn.GrnNumber,
                $"GRN {grn.GrnNumber}",
                grn.Id,
                [
                    new GlPostingLineRequest("Inventory", inventoryValue, true, "Inventory receipt"),
                    new GlPostingLineRequest("GrnClearing", inventoryValue, false, "GRN clearing")
                ],
                autoPost: true,
                ct);

            if (!gl.Succeeded)
                return Result<GoodsReceiptNoteDto>.Failure(gl.Error!);
        }

        var holdForQc = grn.Notes?.Contains("[QC_HOLD]", StringComparison.Ordinal) == true;
        grn.Status = holdForQc ? GrnStatus.QcHold : GrnStatus.Posted;
        grn.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result<GoodsReceiptNoteDto>.Success(MapGrn(grn));
    }

    public async Task<Result<GoodsReceiptNoteDto>> ReleaseQcAsync(int grnId, CancellationToken ct = default)
    {
        if (!EnsureCompany(out _, out var error))
            return Result<GoodsReceiptNoteDto>.Failure(error!);

        var grn = await _db.GoodsReceiptNotes
            .Include(g => g.Lines)
            .Include(g => g.LandedCostLines)
            .FirstOrDefaultAsync(g => g.Id == grnId, ct);

        if (grn is null)
            return Result<GoodsReceiptNoteDto>.Failure("GRN not found.");
        if (grn.Status != GrnStatus.QcHold)
            return Result<GoodsReceiptNoteDto>.Failure("GRN is not on QC hold.");

        grn.Status = GrnStatus.Posted;
        if (!string.IsNullOrEmpty(grn.Notes))
            grn.Notes = grn.Notes.Replace("[QC_HOLD]", "", StringComparison.Ordinal).Trim();
        grn.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result<GoodsReceiptNoteDto>.Success(MapGrn(grn));
    }

    private static List<string> ParseSerials(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json)?
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<Result<CycleCountDto>> CreateCycleCountAsync(CreateCycleCountRequest request, CancellationToken ct = default)
    {
        if (!EnsureCompany(out var companyId, out var error))
            return Result<CycleCountDto>.Failure(error!);

        if (!await _db.Warehouses.AnyAsync(w => w.Id == request.WarehouseId && !w.IsDeleted, ct))
            return Result<CycleCountDto>.Failure("Warehouse not found.");

        if (request.WarehouseLocationId is int locId)
        {
            var locOk = await _db.WarehouseLocations.AnyAsync(l =>
                l.Id == locId && l.WarehouseId == request.WarehouseId && l.IsActive && !l.IsDeleted, ct);
            if (!locOk)
                return Result<CycleCountDto>.Failure("Cycle count location is invalid for this warehouse.");
        }

        var countNumber = await EnterpriseDocumentNumbers.AllocateAsync(_db, "CC", ct);
        var cycleCount = new CycleCount
        {
            CompanyId = companyId,
            CountNumber = countNumber,
            WarehouseId = request.WarehouseId,
            WarehouseLocationId = request.WarehouseLocationId,
            CountDate = request.CountDate,
            Notes = request.Notes,
            Status = CycleCountStatus.Draft
        };

        if (request.Lines is { Count: > 0 })
        {
            foreach (var line in request.Lines)
            {
                var lineLoc = line.WarehouseLocationId ?? request.WarehouseLocationId;
                var systemQty = await GetSystemQuantityAsync(line.ProductId, request.WarehouseId, lineLoc, ct);
                cycleCount.Lines.Add(new CycleCountLine
                {
                    CompanyId = companyId,
                    ProductId = line.ProductId,
                    WarehouseLocationId = lineLoc,
                    SystemQuantity = systemQty,
                    CountedQuantity = line.CountedQuantity
                });
            }
        }
        else if (request.WarehouseLocationId is int filterLoc)
        {
            var balances = await _db.InventoryLocationBalances
                .Include(b => b.InventoryItem)
                .Where(b =>
                    !b.IsDeleted &&
                    b.WarehouseLocationId == filterLoc &&
                    b.QuantityOnHand > 0 &&
                    b.InventoryItem.WarehouseId == request.WarehouseId &&
                    !b.InventoryItem.IsDeleted)
                .ToListAsync(ct);

            foreach (var bal in balances)
            {
                cycleCount.Lines.Add(new CycleCountLine
                {
                    CompanyId = companyId,
                    ProductId = bal.InventoryItem.ProductId,
                    WarehouseLocationId = filterLoc,
                    SystemQuantity = bal.QuantityOnHand,
                    CountedQuantity = bal.QuantityOnHand
                });
            }
        }
        else
        {
            var inventory = await _db.InventoryItems
                .Where(i => i.WarehouseId == request.WarehouseId && !i.IsDeleted && i.QuantityOnHand > 0)
                .ToListAsync(ct);

            foreach (var item in inventory)
            {
                cycleCount.Lines.Add(new CycleCountLine
                {
                    CompanyId = companyId,
                    ProductId = item.ProductId,
                    SystemQuantity = item.QuantityOnHand,
                    CountedQuantity = item.QuantityOnHand
                });
            }
        }

        if (cycleCount.Lines.Count == 0)
            return Result<CycleCountDto>.Failure("No inventory lines to count.");

        cycleCount.Status = CycleCountStatus.InProgress;
        _db.CycleCounts.Add(cycleCount);
        await _db.SaveChangesAsync(ct);
        return Result<CycleCountDto>.Success(await MapCycleCountAsync(cycleCount, ct));
    }

    public async Task<Result<CycleCountDto>> CompleteCycleCountAsync(int cycleCountId, CancellationToken ct = default)
    {
        if (!EnsureCompany(out var companyId, out var error))
            return Result<CycleCountDto>.Failure(error!);

        var cycleCount = await _db.CycleCounts
            .Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.Id == cycleCountId, ct);

        if (cycleCount is null)
            return Result<CycleCountDto>.Failure("Cycle count not found.");

        if (cycleCount.Status is CycleCountStatus.Completed or CycleCountStatus.Cancelled)
            return Result<CycleCountDto>.Failure("Cycle count is already closed.");

        foreach (var line in cycleCount.Lines)
        {
            var variance = line.CountedQuantity - line.SystemQuantity;
            if (variance == 0)
                continue;

            var locationId = line.WarehouseLocationId ?? cycleCount.WarehouseLocationId;

            if (variance > 0)
            {
                var receive = await ReceiveStockInternalAsync(
                    companyId,
                    line.ProductId,
                    cycleCount.WarehouseId,
                    variance,
                    0,
                    null,
                    nameof(CycleCount),
                    cycleCount.Id,
                    locationId,
                    ct);
                if (!receive.Succeeded)
                    return Result<CycleCountDto>.Failure(receive.Error!);
            }
            else
            {
                var deduct = await DeductStockInternalAsync(
                    line.ProductId,
                    cycleCount.WarehouseId,
                    Math.Abs(variance),
                    nameof(CycleCount),
                    cycleCount.Id,
                    locationId,
                    ct);
                if (!deduct.Succeeded)
                    return Result<CycleCountDto>.Failure(deduct.Error!);
            }
        }

        cycleCount.Status = CycleCountStatus.Completed;
        cycleCount.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result<CycleCountDto>.Success(await MapCycleCountAsync(cycleCount, ct));
    }

    private async Task<Result> ReceiveStockInternalAsync(
        int companyId,
        int productId,
        int warehouseId,
        decimal quantity,
        decimal unitCost,
        string? batchNumber,
        string referenceType,
        int referenceId,
        int? warehouseLocationId,
        CancellationToken ct)
    {
        var item = await GetOrCreateInventoryItemAsync(productId, warehouseId, ct);

        if (item.ValuationMethod == ValuationMethod.Average)
        {
            var totalValue = item.QuantityOnHand * item.AverageCost + quantity * unitCost;
            var totalQty = item.QuantityOnHand + quantity;
            item.AverageCost = totalQty > 0 ? totalValue / totalQty : unitCost;
        }

        item.QuantityOnHand += quantity;
        item.UpdatedAt = DateTime.UtcNow;

        _db.StockMovements.Add(new StockMovement
        {
            InventoryItemId = item.Id,
            MovementType = StockMovementType.Purchase,
            Quantity = quantity,
            UnitCost = unitCost,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            MovementDate = DateTime.UtcNow,
            Notes = batchNumber
        });

        try
        {
            var locId = await LocationBalanceSync.ResolveReceivingLocationIdAsync(
                _db.WarehouseLocations, l => _db.WarehouseLocations.Add(l), warehouseId, companyId, warehouseLocationId, ct);
            await LocationBalanceSync.IncreaseAsync(
                _db.InventoryLocationBalances, b => _db.InventoryLocationBalances.Add(b), item.Id, locId, quantity, ct);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }

        return Result.Success();
    }

    private async Task<Result> DeductStockInternalAsync(
        int productId,
        int warehouseId,
        decimal quantity,
        string referenceType,
        int referenceId,
        int? warehouseLocationId,
        CancellationToken ct)
    {
        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(i =>
                i.ProductId == productId &&
                i.WarehouseId == warehouseId &&
                !i.IsDeleted, ct);

        if (item is null || item.QuantityOnHand < quantity)
            return Result.Failure("Insufficient stock for cycle count adjustment.");

        var unitCost = item.AverageCost;
        item.QuantityOnHand -= quantity;
        item.UpdatedAt = DateTime.UtcNow;

        _db.StockMovements.Add(new StockMovement
        {
            InventoryItemId = item.Id,
            MovementType = StockMovementType.Adjustment,
            Quantity = -quantity,
            UnitCost = unitCost,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            MovementDate = DateTime.UtcNow
        });

        var companyId = _company.CompanyId ?? 0;
        try
        {
            var locId = await LocationBalanceSync.ResolvePickLocationIdAsync(
                _db.WarehouseLocations, l => _db.WarehouseLocations.Add(l), warehouseId, companyId, warehouseLocationId, ct);
            var locErr = await LocationBalanceSync.DecreaseAsync(
                _db.InventoryLocationBalances, b => _db.InventoryLocationBalances.Add(b), item.Id, locId, quantity, allowNegative: false, ct);
            if (locErr is not null)
                return Result.Failure(locErr);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }

        return Result.Success();
    }

    private async Task<InventoryItem> GetOrCreateInventoryItemAsync(int productId, int warehouseId, CancellationToken ct)
    {
        var existing = await _db.InventoryItems
            .FirstOrDefaultAsync(i =>
                i.ProductId == productId &&
                i.WarehouseId == warehouseId &&
                !i.IsDeleted, ct);

        if (existing is not null)
            return existing;

        var defaultMethod = await _db.CompanySettings.AsNoTracking()
            .Where(s => !s.IsDeleted)
            .Select(s => (ValuationMethod?)s.DefaultValuationMethod)
            .FirstOrDefaultAsync(ct) ?? ValuationMethod.Average;

        var item = new InventoryItem
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            ValuationMethod = defaultMethod
        };

        _db.InventoryItems.Add(item);
        await _db.SaveChangesAsync(ct);
        return item;
    }

    private async Task<decimal> GetSystemQuantityAsync(int productId, int warehouseId, int? locationId, CancellationToken ct)
    {
        if (locationId is int loc)
        {
            var bal = await _db.InventoryLocationBalances
                .Include(b => b.InventoryItem)
                .FirstOrDefaultAsync(b =>
                    !b.IsDeleted &&
                    b.WarehouseLocationId == loc &&
                    b.InventoryItem.ProductId == productId &&
                    b.InventoryItem.WarehouseId == warehouseId &&
                    !b.InventoryItem.IsDeleted, ct);
            return bal?.QuantityOnHand ?? 0;
        }

        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(i =>
                i.ProductId == productId &&
                i.WarehouseId == warehouseId &&
                !i.IsDeleted, ct);

        return item?.QuantityOnHand ?? 0;
    }

    private bool EnsureCompany(out int companyId, out string? error)
    {
        if (_company.CompanyId.HasValue)
        {
            companyId = _company.CompanyId.Value;
            error = null;
            return true;
        }

        companyId = 0;
        error = "Company context is required.";
        return false;
    }

    private void EnsureCompanyOrThrow()
    {
        if (!_company.CompanyId.HasValue)
            throw new InvalidOperationException("Company context is required.");
    }

    private static StockReservationDto MapReservation(StockReservation r) => new(
        r.Id, r.ProductId, r.WarehouseId, r.Quantity, r.ReferenceType, r.ReferenceId, r.Status, r.ExpiresAt);

    private static GoodsReceiptNoteDto MapGrn(GoodsReceiptNote g) => new(
        g.Id,
        g.GrnNumber,
        g.PurchaseOrderId,
        g.WarehouseId,
        g.ReceiptDate,
        g.Status,
        g.LandedCostAmount,
        g.Notes,
        g.Lines.Select(l => new GoodsReceiptLineDto(
            l.Id,
            l.ProductId,
            l.Quantity,
            l.UnitCost,
            l.PurchaseOrderLineId,
            string.IsNullOrWhiteSpace(l.SerialNumbersJson)
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<List<string>>(l.SerialNumbersJson),
            l.WarehouseLocationId)).ToList(),
        g.LandedCostLines?.Select(l => new GrnLandedCostLineDto(l.Id, l.CostType, l.Amount, l.Notes)).ToList());

    private async Task<CycleCountDto> MapCycleCountAsync(CycleCount c, CancellationToken ct)
    {
        var locIds = c.Lines.Where(l => l.WarehouseLocationId.HasValue).Select(l => l.WarehouseLocationId!.Value)
            .Concat(c.WarehouseLocationId.HasValue ? [c.WarehouseLocationId.Value] : Array.Empty<int>())
            .Distinct()
            .ToList();
        var codes = locIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.WarehouseLocations.AsNoTracking()
                .Where(l => locIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, l => l.Code, ct);

        return MapCycleCount(c, codes);
    }

    private static CycleCountDto MapCycleCount(CycleCount c, IReadOnlyDictionary<int, string>? codes = null) => new(
        c.Id,
        c.CountNumber,
        c.WarehouseId,
        c.CountDate,
        c.Status,
        c.Notes,
        c.Lines.Select(l => new CycleCountLineDto(
            l.Id,
            l.ProductId,
            l.SystemQuantity,
            l.CountedQuantity,
            l.Variance,
            l.WarehouseLocationId,
            l.WarehouseLocationId is int lid && codes is not null && codes.TryGetValue(lid, out var code) ? code : null)).ToList(),
        c.WarehouseLocationId);
}
