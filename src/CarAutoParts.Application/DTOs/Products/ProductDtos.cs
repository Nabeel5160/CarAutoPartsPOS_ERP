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
    IReadOnlyList<VehicleCompatibilityDto> VehicleCompatibilities,
    string? SupersedesSkus = null,
    string? SupersededBySku = null);

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
    public string? FitmentMake { get; set; }
    public string? FitmentModel { get; set; }
    public int? FitmentYear { get; set; }
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
    bool IsDefault,
    int? BranchId = null);

/// <summary>Bin / location within a warehouse (Phase 15).</summary>
public record WarehouseLocationDto(
    int Id,
    int WarehouseId,
    string Code,
    string Name,
    bool IsReceivingDefault,
    bool IsPickDefault,
    bool IsActive,
    int SortOrder);

/// <summary>Create / update warehouse location.</summary>
public record UpsertWarehouseLocationDto(
    string Code,
    string Name,
    bool IsReceivingDefault = false,
    bool IsPickDefault = false,
    bool IsActive = true,
    int SortOrder = 0);

/// <summary>Location-level on-hand balance row.</summary>
public record InventoryLocationBalanceDto(
    int Id,
    int InventoryItemId,
    int ProductId,
    int WarehouseId,
    int WarehouseLocationId,
    string LocationCode,
    string LocationName,
    decimal QuantityOnHand);

/// <summary>Vehicle compatibility for a product.</summary>
public record VehicleCompatibilityDto(
    int? Id,
    string Make,
    string Model,
    int? YearFrom,
    int? YearTo);

/// <summary>Result of OEM/fitment CSV bulk import (upsert; does not wipe catalog).</summary>
public record OemFitmentImportResultDto(
    int Processed,
    int OemUpdated,
    int FitmentAdded,
    int Skipped,
    int ErrorCount,
    string? ErrorReportCsv);
