namespace PosWpf.Models;

/// <summary>Buyer and tax metadata sent to FBR with each invoice.</summary>
public class FbrBuyerDetails
{
    public string BuyerName { get; init; } = "Walk-in Customer";
    public string? BuyerNtn { get; init; }
    public string BuyerRegistrationType { get; init; } = "Unregistered";
    public string BuyerProvince { get; init; } = string.Empty;
    public string BuyerAddress { get; init; } = string.Empty;
    public string? ScenarioId { get; init; }
    public string SroScheduleNo { get; init; } = string.Empty;
    public string SroItemSerialNo { get; init; } = string.Empty;
    public string SaleType { get; init; } = "Goods at standard rate (default)";

    public bool IsRegistered =>
        string.Equals(BuyerRegistrationType, "Registered", StringComparison.OrdinalIgnoreCase);
}
