namespace CarAutoParts.Application.DTOs.Pos;

public class ReceiptLineDto
{
    public required string Name { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal LineTax { get; init; }
    public decimal LineTotal { get; init; }
}

public class ReceiptDataDto
{
    public required Fbr.FbrSellerSettingsDto Seller { get; init; }
    public required string BuyerName { get; init; }
    public string? BuyerNtn { get; init; }
    public required string BuyerRegistrationType { get; init; }
    public required string PosRef { get; init; }
    public required string FbrInvoiceNumber { get; init; }
    public required string QrPayload { get; init; }
    public DateTime SaleDate { get; init; }
    public required IReadOnlyList<ReceiptLineDto> Lines { get; init; }
    public decimal Subtotal { get; init; }
    public decimal TaxTotal { get; init; }
    public decimal GrandTotal { get; init; }
    public string? ScenarioId { get; init; }
    public string? SroScheduleNo { get; init; }
    public string? SroItemSerialNo { get; init; }
    public bool WasStubbed { get; init; }
}
