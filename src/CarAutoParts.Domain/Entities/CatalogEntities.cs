using CarAutoParts.Domain.Common;

namespace CarAutoParts.Domain.Entities;

public class Category : CompanyEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public int? ParentId { get; set; }
    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Brand : CompanyEntity
{
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Warehouse : CompanyEntity
{
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? ContactPerson { get; set; }
    public string? PhoneNumber { get; set; }
    public bool IsDefault { get; set; }
    public ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();
    public ICollection<WarehouseLocation> Locations { get; set; } = new List<WarehouseLocation>();
}

/// <summary>Bin / aisle location within a warehouse (Phase 15).</summary>
public class WarehouseLocation : CompanyEntity
{
    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsReceivingDefault { get; set; }
    public bool IsPickDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public ICollection<InventoryLocationBalance> Balances { get; set; } = new List<InventoryLocationBalance>();
}

public class Product : CompanyEntity
{
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string? OemNumber { get; set; }
    public string? PartNumber { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public int BrandId { get; set; }
    public Brand Brand { get; set; } = null!;
    public string Unit { get; set; } = "PCS";
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal CostPrice { get; set; }
    public int MinimumStock { get; set; }
    public int ReorderLevel { get; set; }
    public int? MaximumStock { get; set; }
    public string? Description { get; set; }
    public string? HsCode { get; set; }
    public decimal TaxRatePercent { get; set; }
    public bool IsActive { get; set; } = true;
    public bool TrackSerialNumbers { get; set; }
    public bool TrackBatches { get; set; }
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductVehicleCompatibility> VehicleCompatibilities { get; set; } = new List<ProductVehicleCompatibility>();
    public ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();
}

public class ProductImage : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string FilePath { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }
}

public class ProductVehicleCompatibility : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }
}
