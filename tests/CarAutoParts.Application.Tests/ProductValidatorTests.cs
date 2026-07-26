using CarAutoParts.Application.DTOs.Products;
using CarAutoParts.Application.Validators;
using FluentAssertions;

namespace CarAutoParts.Application.Tests;

public class ProductValidatorTests
{
    private readonly ProductValidator _validator = new();

    [Fact]
    public async Task Validate_WithValidProduct_Succeeds()
    {
        var dto = new ProductCreateDto(
            "Brake Pad", "BP-001", null, null, null, 1, 1, "PCS",
            100m, 150m, 5, 5, null, null, null, 18m, true, false, false, null);

        var result = await _validator.ValidateAsync(dto);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithEmptySku_Fails()
    {
        var dto = new ProductCreateDto(
            "Brake Pad", "", null, null, null, 1, 1, "PCS",
            100m, 150m, 5, 5, null, null, null, 18m, true, false, false, null);

        var result = await _validator.ValidateAsync(dto);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ProductCreateDto.Sku));
    }

    [Fact]
    public async Task Validate_WithNegativeSalePrice_Fails()
    {
        var dto = new ProductCreateDto(
            "Brake Pad", "BP-001", null, null, null, 1, 1, "PCS",
            100m, -1m, 5, 5, null, null, null, 18m, true, false, false, null);

        var result = await _validator.ValidateAsync(dto);

        result.IsValid.Should().BeFalse();
    }
}
