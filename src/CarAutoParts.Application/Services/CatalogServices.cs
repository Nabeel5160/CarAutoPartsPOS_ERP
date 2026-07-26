using AutoMapper;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Products;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>Category tree management.</summary>
public class CategoryService : ICategoryService
{
    private readonly IRepository<Category> _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IValidator<CategoryDto> _validator;

    public CategoryService(
        IRepository<Category> categories,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IValidator<CategoryDto> validator)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _validator = validator;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CategoryDto>> GetTreeAsync(CancellationToken ct = default)
    {
        var all = await _categories.Query()
            .Where(c => !c.IsDeleted)
            .Include(c => c.Children.Where(ch => !ch.IsDeleted))
            .Where(c => c.ParentId == null)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        return _mapper.Map<List<CategoryDto>>(all);
    }

    /// <inheritdoc />
    public async Task<Result<CategoryDto>> CreateAsync(CategoryDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result<CategoryDto>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var entity = new Category
        {
            Name = dto.Name.Trim(),
            Description = dto.Description,
            Icon = dto.Icon,
            ParentId = dto.ParentId
        };

        _categories.Add(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<CategoryDto>.Success(_mapper.Map<CategoryDto>(entity));
    }

    /// <inheritdoc />
    public async Task<Result<CategoryDto>> UpdateAsync(int id, CategoryDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result<CategoryDto>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var entity = await _categories.GetByIdAsync(id, ct);
        if (entity is null || entity.IsDeleted)
            return Result<CategoryDto>.Failure("Category not found.");

        entity.Name = dto.Name.Trim();
        entity.Description = dto.Description;
        entity.Icon = dto.Icon;
        entity.ParentId = dto.ParentId;
        entity.UpdatedAt = DateTime.UtcNow;
        _categories.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<CategoryDto>.Success(_mapper.Map<CategoryDto>(entity));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _categories.Query()
            .Include(c => c.Children)
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);

        if (entity is null)
            return Result.Failure("Category not found.");

        if (entity.Children.Any(c => !c.IsDeleted) || entity.Products.Any(p => !p.IsDeleted))
            return Result.Failure("Category has children or products.");

        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        _categories.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Brand master data management.</summary>
public class BrandService : IBrandService
{
    private readonly IRepository<Brand> _brands;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IValidator<BrandDto> _validator;

    public BrandService(
        IRepository<Brand> brands,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IValidator<BrandDto> validator)
    {
        _brands = brands;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _validator = validator;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BrandDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await _brands.Query()
            .Where(b => !b.IsDeleted)
            .OrderBy(b => b.Name)
            .ToListAsync(ct);
        return _mapper.Map<List<BrandDto>>(items);
    }

    /// <inheritdoc />
    public async Task<Result<BrandDto>> CreateAsync(BrandDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result<BrandDto>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var entity = new Brand { Name = dto.Name.Trim(), LogoUrl = dto.LogoUrl, IsActive = dto.IsActive };
        _brands.Add(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<BrandDto>.Success(_mapper.Map<BrandDto>(entity));
    }

    /// <inheritdoc />
    public async Task<Result<BrandDto>> UpdateAsync(int id, BrandDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result<BrandDto>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var entity = await _brands.GetByIdAsync(id, ct);
        if (entity is null || entity.IsDeleted)
            return Result<BrandDto>.Failure("Brand not found.");

        entity.Name = dto.Name.Trim();
        entity.LogoUrl = dto.LogoUrl;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        _brands.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<BrandDto>.Success(_mapper.Map<BrandDto>(entity));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _brands.Query()
            .Include(b => b.Products)
            .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted, ct);

        if (entity is null)
            return Result.Failure("Brand not found.");

        if (entity.Products.Any(p => !p.IsDeleted))
            return Result.Failure("Brand has products.");

        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        _brands.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Warehouse master data management.</summary>
public class WarehouseService : IWarehouseService
{
    private readonly IRepository<Warehouse> _warehouses;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IValidator<WarehouseDto> _validator;
    private readonly ICurrentCompanyContext _company;

    public WarehouseService(
        IRepository<Warehouse> warehouses,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IValidator<WarehouseDto> validator,
        ICurrentCompanyContext company)
    {
        _warehouses = warehouses;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _validator = validator;
        _company = company;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WarehouseDto>> GetAllAsync(CancellationToken ct = default)
    {
        var q = _warehouses.Query().Where(w => !w.IsDeleted);

        if (_company.BranchId.HasValue)
        {
            var branchId = _company.BranchId.Value;
            q = q.Where(w => w.BranchId == null || w.BranchId == branchId);
        }

        var items = await q
            .OrderByDescending(w => w.IsDefault)
            .ThenBy(w => w.Name)
            .ToListAsync(ct);
        return _mapper.Map<List<WarehouseDto>>(items);
    }

    /// <inheritdoc />
    public async Task<Result<WarehouseDto>> CreateAsync(WarehouseDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result<WarehouseDto>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        if (dto.IsDefault)
            await ClearDefaultAsync(ct);

        var entity = new Warehouse
        {
            Name = dto.Name.Trim(),
            Address = dto.Address,
            City = dto.City,
            ContactPerson = dto.ContactPerson,
            PhoneNumber = dto.PhoneNumber,
            IsDefault = dto.IsDefault
        };

        _warehouses.Add(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<WarehouseDto>.Success(_mapper.Map<WarehouseDto>(entity));
    }

    /// <inheritdoc />
    public async Task<Result<WarehouseDto>> UpdateAsync(int id, WarehouseDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result<WarehouseDto>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var entity = await _warehouses.GetByIdAsync(id, ct);
        if (entity is null || entity.IsDeleted)
            return Result<WarehouseDto>.Failure("Warehouse not found.");

        if (dto.IsDefault)
            await ClearDefaultAsync(ct);

        entity.Name = dto.Name.Trim();
        entity.Address = dto.Address;
        entity.City = dto.City;
        entity.ContactPerson = dto.ContactPerson;
        entity.PhoneNumber = dto.PhoneNumber;
        entity.IsDefault = dto.IsDefault;
        entity.UpdatedAt = DateTime.UtcNow;
        _warehouses.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<WarehouseDto>.Success(_mapper.Map<WarehouseDto>(entity));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _warehouses.GetByIdAsync(id, ct);
        if (entity is null || entity.IsDeleted)
            return Result.Failure("Warehouse not found.");

        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        _warehouses.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task ClearDefaultAsync(CancellationToken ct)
    {
        var defaults = await _warehouses.Query().Where(w => w.IsDefault && !w.IsDeleted).ToListAsync(ct);
        foreach (var w in defaults)
        {
            w.IsDefault = false;
            _warehouses.Update(w);
        }
    }
}
