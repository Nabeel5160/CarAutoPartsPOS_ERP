namespace PosWpf.Models;

/// <summary>
/// A sellable product in the catalog. HsCode / TaxRate / UoM are required by FBR DI.
/// </summary>
public class Product
{
    public required string Sku { get; init; }
    public required string Name { get; init; }
    public decimal UnitPrice { get; init; }

    /// <summary>Sales tax rate as a percentage, e.g. 18 for 18%.</summary>
    public decimal TaxRatePercent { get; init; } = 18m;

    /// <summary>FBR Harmonized System code.</summary>
    public string HsCode { get; init; } = "0000.0000";

    /// <summary>Unit of measure, e.g. "Numbers, pieces, units".</summary>
    public string UoM { get; init; } = "Numbers, pieces, units";
}
