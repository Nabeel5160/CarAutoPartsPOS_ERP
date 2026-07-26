using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Purchases;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

public interface IPurchaseRequisitionService
{
    Task<IReadOnlyList<PurchaseRequisitionDto>> GetAllAsync(CancellationToken ct = default);
    Task<PurchaseRequisitionDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Result<PurchaseRequisitionDto>> CreateAsync(PurchaseRequisitionCreateDto dto, CancellationToken ct = default);
    Task<Result<PurchaseRequisitionDto>> SubmitAsync(int id, CancellationToken ct = default);
    Task<Result<PurchaseRequisitionDto>> ApproveAsync(int id, CancellationToken ct = default);
    Task<Result<PurchaseRequisitionDto>> RejectAsync(int id, string reason, CancellationToken ct = default);
    Task<Result<PurchaseOrderDetailDto>> ConvertToPoAsync(int id, CancellationToken ct = default);
}

public sealed class PurchaseRequisitionService : IPurchaseRequisitionService
{
    private readonly IEnterpriseDb _db;
    private readonly ICurrentCompanyContext _company;
    private readonly ICurrentUserService _user;
    private readonly IPurchaseOrderService _purchaseOrders;

    public PurchaseRequisitionService(
        IEnterpriseDb db,
        ICurrentCompanyContext company,
        ICurrentUserService user,
        IPurchaseOrderService purchaseOrders)
    {
        _db = db;
        _company = company;
        _user = user;
        _purchaseOrders = purchaseOrders;
    }

    public async Task<IReadOnlyList<PurchaseRequisitionDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await _db.PurchaseRequisitions
            .AsNoTracking()
            .Include(r => r.Lines)
            .Include(r => r.Supplier)
            .OrderByDescending(r => r.RequestedAt)
            .ThenByDescending(r => r.Id)
            .ToListAsync(ct);
        return items.Select(Map).ToList();
    }

    public async Task<PurchaseRequisitionDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var item = await _db.PurchaseRequisitions
            .AsNoTracking()
            .Include(r => r.Lines)
            .Include(r => r.Supplier)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct);
        return item is null ? null : Map(item);
    }

    public async Task<Result<PurchaseRequisitionDto>> CreateAsync(PurchaseRequisitionCreateDto dto, CancellationToken ct = default)
    {
        if (dto.Lines.Count == 0)
            return Result<PurchaseRequisitionDto>.Failure("Requisition must have at least one line.");

        if (dto.SupplierId is int sid && !await _db.Suppliers.AnyAsync(s => s.Id == sid && !s.IsDeleted, ct))
            return Result<PurchaseRequisitionDto>.Failure("Supplier not found.");

        if (dto.WarehouseId is int wid && !await _db.Warehouses.AnyAsync(w => w.Id == wid && !w.IsDeleted, ct))
            return Result<PurchaseRequisitionDto>.Failure("Warehouse not found.");

        string number;
        try
        {
            number = await EnterpriseDocumentNumbers.AllocateAsync(_db, "REQ", ct);
        }
        catch
        {
            var count = await _db.PurchaseRequisitions.CountAsync(ct);
            number = $"REQ-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D4}";
        }

        var entity = new PurchaseRequisition
        {
            RequisitionNumber = number,
            Status = PurchaseRequisitionStatus.Draft,
            SupplierId = dto.SupplierId,
            WarehouseId = dto.WarehouseId,
            Notes = dto.Notes,
            RequestedBy = _user.CurrentUser?.Username,
            RequestedAt = DateTime.UtcNow
        };

        foreach (var line in dto.Lines)
        {
            if (line.Quantity <= 0)
                return Result<PurchaseRequisitionDto>.Failure("Line quantity must be positive.");
            if (!await _db.Products.AnyAsync(p => p.Id == line.ProductId && !p.IsDeleted, ct))
                return Result<PurchaseRequisitionDto>.Failure($"Product {line.ProductId} not found.");

            entity.Lines.Add(new PurchaseRequisitionLine
            {
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                SuggestedUnitPrice = line.SuggestedUnitPrice,
                Notes = line.Notes
            });
        }

        _db.PurchaseRequisitions.Add(entity);
        await _db.SaveChangesAsync(ct);
        return Result<PurchaseRequisitionDto>.Success(Map(entity));
    }

    public async Task<Result<PurchaseRequisitionDto>> SubmitAsync(int id, CancellationToken ct = default)
    {
        var entity = await LoadAsync(id, ct);
        if (entity is null) return Result<PurchaseRequisitionDto>.Failure("Requisition not found.");
        if (entity.Status != PurchaseRequisitionStatus.Draft)
            return Result<PurchaseRequisitionDto>.Failure("Only draft requisitions can be submitted.");
        if (entity.Lines.Count == 0)
            return Result<PurchaseRequisitionDto>.Failure("Requisition has no lines.");

        entity.Status = PurchaseRequisitionStatus.Submitted;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result<PurchaseRequisitionDto>.Success(Map(entity));
    }

    public async Task<Result<PurchaseRequisitionDto>> ApproveAsync(int id, CancellationToken ct = default)
    {
        var entity = await LoadAsync(id, ct);
        if (entity is null) return Result<PurchaseRequisitionDto>.Failure("Requisition not found.");
        if (entity.Status != PurchaseRequisitionStatus.Submitted)
            return Result<PurchaseRequisitionDto>.Failure("Only submitted requisitions can be approved.");

        entity.Status = PurchaseRequisitionStatus.Approved;
        entity.ApprovedBy = _user.CurrentUser?.Username;
        entity.ApprovedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result<PurchaseRequisitionDto>.Success(Map(entity));
    }

    public async Task<Result<PurchaseRequisitionDto>> RejectAsync(int id, string reason, CancellationToken ct = default)
    {
        var entity = await LoadAsync(id, ct);
        if (entity is null) return Result<PurchaseRequisitionDto>.Failure("Requisition not found.");
        if (entity.Status != PurchaseRequisitionStatus.Submitted)
            return Result<PurchaseRequisitionDto>.Failure("Only submitted requisitions can be rejected.");

        entity.Status = PurchaseRequisitionStatus.Rejected;
        entity.RejectionReason = string.IsNullOrWhiteSpace(reason) ? "Rejected" : reason.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result<PurchaseRequisitionDto>.Success(Map(entity));
    }

    public async Task<Result<PurchaseOrderDetailDto>> ConvertToPoAsync(int id, CancellationToken ct = default)
    {
        var entity = await LoadAsync(id, ct);
        if (entity is null) return Result<PurchaseOrderDetailDto>.Failure("Requisition not found.");
        if (entity.Status != PurchaseRequisitionStatus.Approved)
            return Result<PurchaseOrderDetailDto>.Failure("Only approved requisitions can be converted.");
        if (entity.SupplierId is not int supplierId)
            return Result<PurchaseOrderDetailDto>.Failure("Supplier is required before converting to PO.");

        var productIds = entity.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        var lines = entity.Lines.Select(l =>
        {
            var price = l.SuggestedUnitPrice ?? (products.TryGetValue(l.ProductId, out var p) ? p.PurchasePrice : 0m);
            return new PurchaseOrderLineDto(null, l.ProductId, null, l.Quantity, 0, price, 0, 0, l.Quantity * price);
        }).ToList();

        var create = new PurchaseOrderCreateDto(
            supplierId,
            null,
            entity.WarehouseId,
            entity.Notes,
            0,
            lines,
            null,
            entity.Id);

        var result = await _purchaseOrders.CreateAsync(create, ct);
        if (!result.Succeeded)
            return Result<PurchaseOrderDetailDto>.Failure(result.Error!);

        entity.Status = PurchaseRequisitionStatus.Converted;
        entity.ConvertedPurchaseOrderId = result.Data!.Id;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return result;
    }

    private async Task<PurchaseRequisition?> LoadAsync(int id, CancellationToken ct) =>
        await _db.PurchaseRequisitions
            .Include(r => r.Lines)
            .Include(r => r.Supplier)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct);

    private static PurchaseRequisitionDto Map(PurchaseRequisition r) => new(
        r.Id,
        r.RequisitionNumber,
        r.Status,
        r.SupplierId,
        r.Supplier?.Name,
        r.WarehouseId,
        r.RequestedAt,
        r.RequestedBy,
        r.Notes,
        r.ApprovedBy,
        r.ApprovedAt,
        r.RejectionReason,
        r.ConvertedPurchaseOrderId,
        r.Lines.Select(l => new PurchaseRequisitionLineDto(l.Id, l.ProductId, l.Quantity, l.SuggestedUnitPrice, l.Notes)).ToList());
}

public interface IReorderService
{
    Task<IReadOnlyList<ReorderSuggestionDto>> SuggestAsync(int? warehouseId = null, CancellationToken ct = default);
    Task<Result<PurchaseRequisitionDto>> CreateDraftPrFromSuggestionsAsync(
        IReadOnlyList<ReorderSuggestionLineDto> lines,
        int? supplierId,
        int? warehouseId,
        CancellationToken ct = default);
}

public sealed class ReorderService : IReorderService
{
    private readonly IEnterpriseDb _db;
    private readonly IPurchaseRequisitionService _requisitions;

    public ReorderService(IEnterpriseDb db, IPurchaseRequisitionService requisitions)
    {
        _db = db;
        _requisitions = requisitions;
    }

    public async Task<IReadOnlyList<ReorderSuggestionDto>> SuggestAsync(int? warehouseId = null, CancellationToken ct = default)
    {
        var products = await _db.Products.AsNoTracking()
            .Where(p => p.IsActive && !p.IsDeleted)
            .ToListAsync(ct);

        var invQuery = _db.InventoryItems.AsNoTracking().Where(i => !i.IsDeleted);
        if (warehouseId is int wid)
            invQuery = invQuery.Where(i => i.WarehouseId == wid);

        var stock = await invQuery
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Available = g.Sum(i => i.QuantityOnHand - i.ReservedQuantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Available, ct);

        var suggestions = new List<ReorderSuggestionDto>();
        foreach (var p in products)
        {
            var threshold = Math.Max(p.ReorderLevel, p.MinimumStock);
            stock.TryGetValue(p.Id, out var available);
            if (available > threshold) continue;

            var target = p.MaximumStock ?? (p.ReorderLevel > 0 ? p.ReorderLevel * 2 : p.MinimumStock * 2);
            if (target <= 0) target = Math.Max(threshold, 1) * 2;
            var qty = Math.Max(0, target - available);
            if (qty <= 0) continue;

            suggestions.Add(new ReorderSuggestionDto(
                p.Id,
                p.Name,
                p.Sku,
                available,
                p.MinimumStock,
                p.ReorderLevel,
                p.MaximumStock,
                qty,
                p.PurchasePrice));
        }

        return suggestions.OrderBy(s => s.Sku).ToList();
    }

    public async Task<Result<PurchaseRequisitionDto>> CreateDraftPrFromSuggestionsAsync(
        IReadOnlyList<ReorderSuggestionLineDto> lines,
        int? supplierId,
        int? warehouseId,
        CancellationToken ct = default)
    {
        if (lines.Count == 0)
            return Result<PurchaseRequisitionDto>.Failure("No suggestion lines selected.");

        return await _requisitions.CreateAsync(new PurchaseRequisitionCreateDto(
            supplierId,
            warehouseId,
            "Created from reorder suggestions",
            lines.Select(l => new PurchaseRequisitionLineCreateDto(l.ProductId, l.SuggestedQty, l.UnitPrice, null)).ToList()), ct);
    }
}
