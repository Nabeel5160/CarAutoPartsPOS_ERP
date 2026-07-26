using System.Text.Json.Serialization;

namespace PosWpf.Models.Fbr;

/// <summary>
/// A single line item in an FBR Digital Invoicing payload.
/// </summary>
public class FbrInvoiceItem
{
    [JsonPropertyName("hsCode")]
    public string HsCode { get; set; } = string.Empty;

    [JsonPropertyName("productDescription")]
    public string ProductDescription { get; set; } = string.Empty;

    [JsonPropertyName("rate")]
    public string Rate { get; set; } = string.Empty; // e.g. "18%"

    [JsonPropertyName("uoM")]
    public string UoM { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("totalValues")]
    public decimal TotalValues { get; set; }

    [JsonPropertyName("valueSalesExcludingST")]
    public decimal ValueSalesExcludingST { get; set; }

    [JsonPropertyName("fixedNotifiedValueOrRetailPrice")]
    public decimal FixedNotifiedValueOrRetailPrice { get; set; }

    [JsonPropertyName("salesTaxApplicable")]
    public decimal SalesTaxApplicable { get; set; }

    [JsonPropertyName("salesTaxWithheldAtSource")]
    public decimal SalesTaxWithheldAtSource { get; set; }

    [JsonPropertyName("extraTax")]
    public string ExtraTax { get; set; } = string.Empty;

    [JsonPropertyName("furtherTax")]
    public decimal FurtherTax { get; set; }

    [JsonPropertyName("sroScheduleNo")]
    public string SroScheduleNo { get; set; } = string.Empty;

    [JsonPropertyName("fedPayable")]
    public decimal FedPayable { get; set; }

    [JsonPropertyName("discount")]
    public decimal Discount { get; set; }

    [JsonPropertyName("saleType")]
    public string SaleType { get; set; } = "Goods at standard rate (default)";

    [JsonPropertyName("sroItemSerialNo")]
    public string SroItemSerialNo { get; set; } = string.Empty;
}
