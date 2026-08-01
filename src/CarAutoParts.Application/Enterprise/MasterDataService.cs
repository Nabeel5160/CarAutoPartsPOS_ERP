using CarAutoParts.Application.Common;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Enterprise;

public sealed class MasterDataService : IMasterDataService
{
    private readonly IEnterpriseDb _db;
    private readonly ICurrentCompanyContext _company;

    public MasterDataService(IEnterpriseDb db, ICurrentCompanyContext company)
    {
        _db = db;
        _company = company;
    }

    public async Task<Result<ProductKitDto>> UpsertKitAsync(UpsertKitRequest request, CancellationToken ct = default)
    {
        if (!EnsureCompany(out var companyId, out var error))
            return Result<ProductKitDto>.Failure(error!);

        if (request.Components.Count == 0)
            return Result<ProductKitDto>.Failure("Kit must have at least one component.");

        if (!await _db.Products.AnyAsync(p => p.Id == request.ParentProductId && !p.IsDeleted, ct))
            return Result<ProductKitDto>.Failure("Parent product not found.");

        ProductKit kit;
        if (request.Id.HasValue)
        {
            var existing = await _db.ProductKits
                .Include(k => k.Components)
                .FirstOrDefaultAsync(k => k.Id == request.Id.Value, ct);
            if (existing is null)
                return Result<ProductKitDto>.Failure("Kit not found.");
            kit = existing;

            foreach (var componentToRemove in kit.Components.ToList())
                _db.ProductKitComponents.Remove(componentToRemove);

            kit.ParentProductId = request.ParentProductId;
            kit.Name = request.Name.Trim();
            kit.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            kit = new ProductKit
            {
                CompanyId = companyId,
                ParentProductId = request.ParentProductId,
                Name = request.Name.Trim()
            };
            _db.ProductKits.Add(kit);
        }

        foreach (var component in request.Components)
        {
            kit.Components.Add(new ProductKitComponent
            {
                CompanyId = companyId,
                ComponentProductId = component.ComponentProductId,
                Quantity = component.Quantity
            });
        }

        await _db.SaveChangesAsync(ct);
        return Result<ProductKitDto>.Success(MapKit(kit));
    }

    public async Task<Result<ProductSupersessionDto>> UpsertSupersessionAsync(
        UpsertSupersessionRequest request,
        CancellationToken ct = default)
    {
        if (!EnsureCompany(out var companyId, out var error))
            return Result<ProductSupersessionDto>.Failure(error!);

        if (request.OldProductId == request.NewProductId)
            return Result<ProductSupersessionDto>.Failure("Old and new product must differ.");

        ProductSupersession entity;
        if (request.Id.HasValue)
        {
            var existing = await _db.ProductSupersessions.FirstOrDefaultAsync(s => s.Id == request.Id.Value, ct);
            if (existing is null)
                return Result<ProductSupersessionDto>.Failure("Supersession not found.");
            entity = existing;

            entity.OldProductId = request.OldProductId;
            entity.NewProductId = request.NewProductId;
            entity.EffectiveFrom = request.EffectiveFrom;
            entity.Notes = request.Notes;
            entity.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            entity = new ProductSupersession
            {
                CompanyId = companyId,
                OldProductId = request.OldProductId,
                NewProductId = request.NewProductId,
                EffectiveFrom = request.EffectiveFrom,
                Notes = request.Notes
            };
            _db.ProductSupersessions.Add(entity);
        }

        await _db.SaveChangesAsync(ct);
        return Result<ProductSupersessionDto>.Success(MapSupersession(entity));
    }

    public async Task<PagedResult<ProductKitDto>> GetKitsAsync(QuerySpec? query = null, int? parentProductId = null, CancellationToken ct = default)
    {
        EnsureCompanyOrThrow();
        query ??= new QuerySpec();

        var q = _db.ProductKits.Include(k => k.Components).AsNoTracking();
        if (parentProductId.HasValue)
            q = q.Where(k => k.ParentProductId == parentProductId.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(k => k.Name.Contains(s));
        }

        var ordered = q.OrderBy(k => k.Name);
        var paged = await ordered.ToPagedResultAsync(query.Page, query.PageSize, ct);
        return new PagedResult<ProductKitDto>
        {
            Items = paged.Items.Select(MapKit).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    public async Task<IReadOnlyList<ProductSupersessionDto>> GetSupersessionsAsync(
        int? productId = null,
        CancellationToken ct = default)
    {
        EnsureCompanyOrThrow();

        var q = _db.ProductSupersessions.AsNoTracking();
        if (productId.HasValue)
            q = q.Where(s => s.OldProductId == productId.Value || s.NewProductId == productId.Value);

        var items = await q
            .Include(s => s.OldProduct)
            .Include(s => s.NewProduct)
            .OrderByDescending(s => s.EffectiveFrom)
            .ToListAsync(ct);
        return items.Select(MapSupersession).ToList();
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

    private static ProductKitDto MapKit(ProductKit k) => new(
        k.Id,
        k.ParentProductId,
        k.Name,
        k.Components.Select(c => new ProductKitComponentDto(c.Id, c.ComponentProductId, c.Quantity)).ToList());

    private static ProductSupersessionDto MapSupersession(ProductSupersession s) => new(
        s.Id,
        s.OldProductId,
        s.NewProductId,
        s.EffectiveFrom,
        s.Notes,
        s.OldProduct?.Sku,
        s.NewProduct?.Sku);
}
