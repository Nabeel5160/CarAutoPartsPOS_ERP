using System.Text.Json.Serialization;

namespace PosWpf.Models.Fbr;

/// <summary>
/// Payload posted to the FBR Digital Invoicing endpoint
/// (di/postinvoicedata). Property names match the FBR JSON schema.
/// </summary>
public class FbrInvoiceRequest
{
    [JsonPropertyName("invoiceType")]
    public string InvoiceType { get; set; } = "Sale Invoice";

    [JsonPropertyName("invoiceDate")]
    public string InvoiceDate { get; set; } = string.Empty; // yyyy-MM-dd

    [JsonPropertyName("sellerNTNCNIC")]
    public string SellerNTNCNIC { get; set; } = string.Empty;

    [JsonPropertyName("sellerBusinessName")]
    public string SellerBusinessName { get; set; } = string.Empty;

    [JsonPropertyName("sellerProvince")]
    public string SellerProvince { get; set; } = string.Empty;

    [JsonPropertyName("sellerAddress")]
    public string SellerAddress { get; set; } = string.Empty;

    [JsonPropertyName("buyerNTNCNIC")]
    public string? BuyerNTNCNIC { get; set; }

    [JsonPropertyName("buyerBusinessName")]
    public string BuyerBusinessName { get; set; } = "Walk-in Customer";

    [JsonPropertyName("buyerProvince")]
    public string BuyerProvince { get; set; } = string.Empty;

    [JsonPropertyName("buyerAddress")]
    public string BuyerAddress { get; set; } = string.Empty;

    [JsonPropertyName("buyerRegistrationType")]
    public string BuyerRegistrationType { get; set; } = "Unregistered";

    [JsonPropertyName("invoiceRefNo")]
    public string InvoiceRefNo { get; set; } = string.Empty;

    [JsonPropertyName("scenarioId")]
    public string? ScenarioId { get; set; }

    [JsonPropertyName("items")]
    public List<FbrInvoiceItem> Items { get; set; } = new();
}
