using System.Text.Json.Serialization;

namespace PosWpf.Models.Fbr;

/// <summary>
/// Response returned by the FBR Digital Invoicing endpoint.
/// </summary>
public class FbrInvoiceResponse
{
    [JsonPropertyName("invoiceNumber")]
    public string? InvoiceNumber { get; set; }

    [JsonPropertyName("dated")]
    public string? Dated { get; set; }

    [JsonPropertyName("validationResponse")]
    public FbrValidationResponse? ValidationResponse { get; set; }

    [JsonIgnore]
    public bool IsValid =>
        string.Equals(ValidationResponse?.Status, "Valid", StringComparison.OrdinalIgnoreCase)
        || ValidationResponse?.StatusCode == "00";
}

public class FbrValidationResponse
{
    [JsonPropertyName("statusCode")]
    public string? StatusCode { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("invoiceStatuses")]
    public List<FbrItemStatus>? InvoiceStatuses { get; set; }
}

public class FbrItemStatus
{
    [JsonPropertyName("itemSNo")]
    public string? ItemSNo { get; set; }

    [JsonPropertyName("statusCode")]
    public string? StatusCode { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("invoiceNo")]
    public string? InvoiceNo { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
