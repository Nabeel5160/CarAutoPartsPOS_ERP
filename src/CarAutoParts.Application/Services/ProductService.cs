using AutoMapper;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Products;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using ClosedXML.Excel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>Product catalog CRUD and import/export.</summary>
public class ProductService : IProductService
{
    private readonly IRepository<Product> _products;
    private readonly IRepository<Category> _categories;
    private readonly IRepository<Brand> _brands;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IValidator<ProductCreateDto> _validator;

    public ProductService(
        IRepository<Product> products,
        IRepository<Category> categories,
        IRepository<Brand> brands,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IValidator<ProductCreateDto> validator)
    {
        _products = products;
        _categories = categories;
        _brands = brands;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _validator = validator;
    }

    /// <inheritdoc />
    public async Task<PagedResult<ProductListDto>> GetProductsAsync(ProductQueryDto query, CancellationToken ct = default)
    {
        var q = _products.Query()
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.InventoryItems)
            .Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(p => p.Name.Contains(s) || p.Sku.Contains(s) || (p.Barcode != null && p.Barcode.Contains(s)));
        }

        if (query.CategoryId.HasValue)
            q = q.Where(p => p.CategoryId == query.CategoryId);

        if (query.BrandId.HasValue)
            q = q.Where(p => p.BrandId == query.BrandId);

        if (query.IsActive.HasValue)
            q = q.Where(p => p.IsActive == query.IsActive);

        if (query.LowStockOnly)
            q = q.Where(p => p.InventoryItems.Sum(i => i.QuantityOnHand) <= p.MinimumStock);

        q = query.SortBy?.ToLowerInvariant() switch
        {
            "name" => query.SortDescending ? q.OrderByDescending(p => p.Name) : q.OrderBy(p => p.Name),
            "sku" => query.SortDescending ? q.OrderByDescending(p => p.Sku) : q.OrderBy(p => p.Sku),
            "price" => query.SortDescending ? q.OrderByDescending(p => p.SalePrice) : q.OrderBy(p => p.SalePrice),
            _ => q.OrderBy(p => p.Name)
        };

        var paged = await q.ToPagedResultAsync(query.Page, query.PageSize, ct);
        return new PagedResult<ProductListDto>
        {
            Items = _mapper.Map<List<ProductListDto>>(paged.Items),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    /// <inheritdoc />
    public async Task<ProductDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var product = await _products.Query()
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Images)
            .Include(p => p.VehicleCompatibilities)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);

        return product is null ? null : _mapper.Map<ProductDetailDto>(product);
    }

    /// <inheritdoc />
    public async Task<Result<ProductDetailDto>> CreateAsync(ProductCreateDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result<ProductDetailDto>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        if (await _products.ExistsAsync(p => p.Sku == dto.Sku && !p.IsDeleted, ct))
            return Result<ProductDetailDto>.Failure("SKU already exists.");

        if (!await _categories.ExistsAsync(c => c.Id == dto.CategoryId && !c.IsDeleted, ct))
            return Result<ProductDetailDto>.Failure("Category not found.");

        if (!await _brands.ExistsAsync(b => b.Id == dto.BrandId && !b.IsDeleted, ct))
            return Result<ProductDetailDto>.Failure("Brand not found.");

        var entity = new Product
        {
            Name = dto.Name.Trim(),
            Sku = dto.Sku.Trim(),
            Barcode = dto.Barcode,
            OemNumber = dto.OemNumber,
            PartNumber = dto.PartNumber,
            CategoryId = dto.CategoryId,
            BrandId = dto.BrandId,
            Unit = dto.Unit,
            PurchasePrice = dto.PurchasePrice,
            SalePrice = dto.SalePrice,
            CostPrice = dto.PurchasePrice,
            MinimumStock = dto.MinimumStock,
            ReorderLevel = dto.ReorderLevel,
            MaximumStock = dto.MaximumStock,
            Description = dto.Description,
            HsCode = dto.HsCode,
            TaxRatePercent = dto.TaxRatePercent,
            IsActive = dto.IsActive,
            TrackSerialNumbers = dto.TrackSerialNumbers,
            TrackBatches = dto.TrackBatches
        };

        if (dto.VehicleCompatibilities != null)
        {
            foreach (var vc in dto.VehicleCompatibilities)
            {
                entity.VehicleCompatibilities.Add(new ProductVehicleCompatibility
                {
                    Make = vc.Make,
                    Model = vc.Model,
                    YearFrom = vc.YearFrom,
                    YearTo = vc.YearTo
                });
            }
        }

        _products.Add(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<ProductDetailDto>.Success((await GetByIdAsync(entity.Id, ct))!);
    }

    /// <inheritdoc />
    public async Task<Result<ProductDetailDto>> UpdateAsync(int id, ProductCreateDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result<ProductDetailDto>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var entity = await _products.Query()
            .Include(p => p.VehicleCompatibilities)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);

        if (entity is null)
            return Result<ProductDetailDto>.Failure("Product not found.");

        if (await _products.ExistsAsync(p => p.Sku == dto.Sku && p.Id != id && !p.IsDeleted, ct))
            return Result<ProductDetailDto>.Failure("SKU already exists.");

        entity.Name = dto.Name.Trim();
        entity.Sku = dto.Sku.Trim();
        entity.Barcode = dto.Barcode;
        entity.OemNumber = dto.OemNumber;
        entity.PartNumber = dto.PartNumber;
        entity.CategoryId = dto.CategoryId;
        entity.BrandId = dto.BrandId;
        entity.Unit = dto.Unit;
        entity.PurchasePrice = dto.PurchasePrice;
        entity.SalePrice = dto.SalePrice;
        entity.MinimumStock = dto.MinimumStock;
        entity.ReorderLevel = dto.ReorderLevel;
        entity.MaximumStock = dto.MaximumStock;
        entity.Description = dto.Description;
        entity.HsCode = dto.HsCode;
        entity.TaxRatePercent = dto.TaxRatePercent;
        entity.IsActive = dto.IsActive;
        entity.TrackSerialNumbers = dto.TrackSerialNumbers;
        entity.TrackBatches = dto.TrackBatches;
        entity.UpdatedAt = DateTime.UtcNow;

        entity.VehicleCompatibilities.Clear();
        if (dto.VehicleCompatibilities != null)
        {
            foreach (var vc in dto.VehicleCompatibilities)
            {
                entity.VehicleCompatibilities.Add(new ProductVehicleCompatibility
                {
                    Make = vc.Make,
                    Model = vc.Model,
                    YearFrom = vc.YearFrom,
                    YearTo = vc.YearTo
                });
            }
        }

        _products.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<ProductDetailDto>.Success((await GetByIdAsync(id, ct))!);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _products.GetByIdAsync(id, ct);
        if (entity is null || entity.IsDeleted)
            return Result.Failure("Product not found.");

        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        _products.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<int>> ImportFromExcelAsync(Stream stream, CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();
        var imported = 0;

        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            var sku = row.Cell(1).GetString().Trim();
            if (string.IsNullOrEmpty(sku)) continue;

            if (await _products.ExistsAsync(p => p.Sku == sku && !p.IsDeleted, ct))
                continue;

            var name = row.Cell(2).GetString().Trim();
            if (string.IsNullOrEmpty(name)) continue;

            var categoryName = row.Cell(3).GetString().Trim();
            var brandName = row.Cell(4).GetString().Trim();
            var category = await _categories.Query().FirstOrDefaultAsync(c => c.Name == categoryName && !c.IsDeleted, ct);
            var brand = await _brands.Query().FirstOrDefaultAsync(b => b.Name == brandName && !b.IsDeleted, ct);
            if (category is null || brand is null) continue;

            _products.Add(new Product
            {
                Sku = sku,
                Name = name,
                CategoryId = category.Id,
                BrandId = brand.Id,
                SalePrice = row.Cell(5).TryGetValue(out decimal sp) ? sp : 0,
                PurchasePrice = row.Cell(6).TryGetValue(out decimal pp) ? pp : 0,
                CostPrice = row.Cell(6).TryGetValue(out decimal cp) ? cp : 0,
                IsActive = true
            });
            imported++;
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result<int>.Success(imported);
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportToExcelAsync(ProductQueryDto query, CancellationToken ct = default)
    {
        query.PageSize = 10_000;
        var result = await GetProductsAsync(query, ct);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Products");
        sheet.Cell(1, 1).Value = "SKU";
        sheet.Cell(1, 2).Value = "Name";
        sheet.Cell(1, 3).Value = "Category";
        sheet.Cell(1, 4).Value = "Brand";
        sheet.Cell(1, 5).Value = "Sale Price";
        sheet.Cell(1, 6).Value = "Cost";
        sheet.Cell(1, 7).Value = "Stock";
        sheet.Cell(1, 8).Value = "Active";

        var row = 2;
        foreach (var p in result.Items)
        {
            sheet.Cell(row, 1).Value = p.Sku;
            sheet.Cell(row, 2).Value = p.Name;
            sheet.Cell(row, 3).Value = p.CategoryName;
            sheet.Cell(row, 4).Value = p.BrandName;
            sheet.Cell(row, 5).Value = p.SalePrice;
            sheet.Cell(row, 6).Value = p.CostPrice;
            sheet.Cell(row, 7).Value = p.TotalStock;
            sheet.Cell(row, 8).Value = p.IsActive;
            row++;
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}
