using CarAutoParts.Application.DTOs.Products;
using FluentValidation;

namespace CarAutoParts.Application.Validators;

/// <summary>Validates product create/update payloads.</summary>
public class ProductValidator : AbstractValidator<ProductCreateDto>
{
    public ProductValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(50);
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.BrandId).GreaterThan(0);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(20);
        RuleFor(x => x.PurchasePrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SalePrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TaxRatePercent).InclusiveBetween(0, 100);
        RuleFor(x => x.MinimumStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0);
    }
}

/// <summary>Validates category payloads.</summary>
public class CategoryValidator : AbstractValidator<CategoryDto>
{
    public CategoryValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

/// <summary>Validates brand payloads.</summary>
public class BrandValidator : AbstractValidator<BrandDto>
{
    public BrandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

/// <summary>Validates warehouse payloads.</summary>
public class WarehouseValidator : AbstractValidator<WarehouseDto>
{
    public WarehouseValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
