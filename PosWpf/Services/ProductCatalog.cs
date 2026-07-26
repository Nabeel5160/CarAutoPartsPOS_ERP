using PosWpf.Models;

namespace PosWpf.Services;

/// <summary>In-memory demo catalog. Swap for a DB / API later.</summary>
public static class ProductCatalog
{
    public static IReadOnlyList<Product> GetProducts() => new List<Product>
    {
        new() { Sku = "BVG-001", Name = "Mineral Water 1.5L", UnitPrice = 90m, TaxRatePercent = 18m, HsCode = "2201.9000", UoM = "Numbers, pieces, units" },
        new() { Sku = "BVG-002", Name = "Cola Can 250ml", UnitPrice = 80m, TaxRatePercent = 18m, HsCode = "2202.1000", UoM = "Numbers, pieces, units" },
        new() { Sku = "SNK-010", Name = "Potato Chips 40g", UnitPrice = 50m, TaxRatePercent = 18m, HsCode = "2005.2000", UoM = "Numbers, pieces, units" },
        new() { Sku = "SNK-011", Name = "Chocolate Bar", UnitPrice = 120m, TaxRatePercent = 18m, HsCode = "1806.3200", UoM = "Numbers, pieces, units" },
        new() { Sku = "GRO-020", Name = "Tea 200g Pack", UnitPrice = 350m, TaxRatePercent = 18m, HsCode = "0902.3000", UoM = "Numbers, pieces, units" },
        new() { Sku = "GRO-021", Name = "Sugar 1kg", UnitPrice = 180m, TaxRatePercent = 18m, HsCode = "1701.9900", UoM = "Kilogram" },
        new() { Sku = "GRO-022", Name = "Cooking Oil 1L", UnitPrice = 650m, TaxRatePercent = 18m, HsCode = "1511.9000", UoM = "Litre" },
        new() { Sku = "DRY-030", Name = "Milk 1L", UnitPrice = 230m, TaxRatePercent = 18m, HsCode = "0401.2000", UoM = "Litre" },
        new() { Sku = "BKR-040", Name = "Bread Loaf", UnitPrice = 160m, TaxRatePercent = 18m, HsCode = "1905.9000", UoM = "Numbers, pieces, units" },
        new() { Sku = "BKR-041", Name = "Eggs (Dozen)", UnitPrice = 320m, TaxRatePercent = 0m, HsCode = "0407.2100", UoM = "Dozen" },
        new() { Sku = "HHC-050", Name = "Soap Bar", UnitPrice = 140m, TaxRatePercent = 18m, HsCode = "3401.1100", UoM = "Numbers, pieces, units" },
        new() { Sku = "HHC-051", Name = "Shampoo 200ml", UnitPrice = 480m, TaxRatePercent = 18m, HsCode = "3305.1000", UoM = "Numbers, pieces, units" },
    };
}
