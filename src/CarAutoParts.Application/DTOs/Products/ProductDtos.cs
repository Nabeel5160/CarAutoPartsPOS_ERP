namespace CarAutoParts.Application.DTOs.Products;

/// <summary>Product list row.</summary>
public record ProductListDto(
    int Id,
    string Name,
    string Sku,
    string? Barcode,
    string CategoryName,
    string BrandName,
    decimal SalePrice,
    decimal CostPrice,
    decimal TotalStock,
    int MinimumStock,
    bool IsActive);

/// <summary>Full product detail including compatibility and images.</summary>
public record ProductDetailDto(
    int Id,
    string Name,
    string Sku,
    string? Barcode,
    string? OemNumber,
    string? PartNumber,
    int CategoryId,
    string CategoryName,
    int BrandId,
    string BrandName,
    string Unit,
    decimal PurchasePrice,
    decimal SalePrice,
    decimal CostPrice,
    int MinimumStock,
    int ReorderLevel,
    int? MaximumStock,
    string? Description,
    string? HsCode,
    decimal TaxRatePercent,
    bool IsActive,
    bool TrackSerialNumbers,
    bool TrackBatches,
    IReadOnlyList<string> ImagePaths,
    IReadOnlyList<VehicleCompatibilityDto> VehicleCompatibilities);

/// <summary>Payload for creating or updating a product.</summary>
public record ProductCreateDto(
    string Name,
    string Sku,
    string? Barcode,
    string? OemNumber,
    string? PartNumber,
    int CategoryId,
    int BrandId,
    string Unit,
    decimal PurchasePrice,
    decimal SalePrice,
    int MinimumStock,
    int ReorderLevel,
    int? MaximumStock,
    string? Description,
    string? HsCode,
    decimal TaxRatePercent,
    bool IsActive,
    bool TrackSerialNumbers,
    bool TrackBatches,
    IReadOnlyList<VehicleCompatibilityDto>? VehicleCompatibilities);

/// <summary>Product search and filter criteria.</summary>
public class ProductQueryDto
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public int? BrandId { get; set; }
    public bool? IsActive { get; set; }
    public bool LowStockOnly { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
}

/// <summary>Category node for tree display.</summary>
public record CategoryDto(
    int Id,
    string Name,
    string? Description,
    string? Icon,
    int? ParentId,
    IReadOnlyList<CategoryDto> Children);

/// <summary>Brand summary.</summary>
public record BrandDto(int Id, string Name, string? LogoUrl, bool IsActive);

/// <summary>Warehouse summary.</summary>
public record WarehouseDto(
    int Id,
    string Name,
    string? Address,
    string? City,
    string? ContactPerson,
    string? PhoneNumber,
    bool IsDefault);

/// <summary>Vehicle compatibility for a product.</summary>
public record VehicleCompatibilityDto(
    int? Id,
    string Make,
    string Model,
    int? YearFrom,
    int? YearTo);
