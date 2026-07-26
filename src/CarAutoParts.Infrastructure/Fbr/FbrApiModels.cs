using System.Text.Json.Serialization;

namespace CarAutoParts.Infrastructure.Fbr;

internal class FbrInvoiceResponse
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

internal class FbrValidationResponse
{
    [JsonPropertyName("statusCode")]
    public string? StatusCode { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
