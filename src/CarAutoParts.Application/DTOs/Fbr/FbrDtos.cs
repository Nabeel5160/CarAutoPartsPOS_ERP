using System.Text.Json.Serialization;

namespace CarAutoParts.Application.DTOs.Fbr;

public class FbrInvoiceRequestDto
{
    [JsonPropertyName("invoiceType")]
    public string InvoiceType { get; set; } = "Sale Invoice";

    [JsonPropertyName("invoiceDate")]
    public string InvoiceDate { get; set; } = string.Empty;

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
    public List<FbrInvoiceItemDto> Items { get; set; } = new();
}

public class FbrInvoiceItemDto
{
    [JsonPropertyName("hsCode")]
    public string HsCode { get; set; } = string.Empty;

    [JsonPropertyName("productDescription")]
    public string ProductDescription { get; set; } = string.Empty;

    [JsonPropertyName("rate")]
    public string Rate { get; set; } = string.Empty;

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

public class FbrPostResultDto
{
    public bool Success { get; init; }
    public string? InvoiceNumber { get; init; }
    public string? Message { get; init; }
    public bool WasStubbed { get; init; }
    public string? RequestJson { get; init; }
    public string? ResponseJson { get; init; }

    public static FbrPostResultDto Ok(string invoiceNumber, bool stubbed, string? reqJson, string? respJson, string? message = null)
        => new()
        {
            Success = true,
            InvoiceNumber = invoiceNumber,
            WasStubbed = stubbed,
            RequestJson = reqJson,
            ResponseJson = respJson,
            Message = message ?? (stubbed ? "Posted in OFFLINE/STUB mode (no FBR token configured)." : "Accepted by FBR.")
        };

    public static FbrPostResultDto Fail(string message, string? reqJson = null, string? respJson = null)
        => new() { Success = false, Message = message, RequestJson = reqJson, ResponseJson = respJson };
}

public class FbrSellerSettingsDto
{
    public string NTNCNIC { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string PosId { get; set; } = string.Empty;
}

public record FbrBuyerDetailsDto(
    string BuyerName,
    string? BuyerNtn,
    string BuyerRegistrationType,
    string BuyerProvince,
    string BuyerAddress,
    string? ScenarioId,
    string SroScheduleNo,
    string SroItemSerialNo,
    string SaleType)
{
    public bool IsRegistered =>
        string.Equals(BuyerRegistrationType, "Registered", StringComparison.OrdinalIgnoreCase);
}

public class FbrInvoiceLineDto
{
    public string HsCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal TaxRatePercent { get; set; }
    public string UnitOfMeasure { get; set; } = "PCS";
    public decimal Quantity { get; set; }
    public decimal LineSubtotal { get; set; }
    public decimal LineTax { get; set; }
    public decimal LineTotal => LineSubtotal + LineTax;
}
