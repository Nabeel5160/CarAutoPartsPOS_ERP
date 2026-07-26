namespace PosWpf.Models;

public class FbrSettings
{
    public bool UseSandbox { get; set; } = true;
    public string PostInvoiceUrlSandbox { get; set; } = string.Empty;
    public string PostInvoiceUrlProduction { get; set; } = string.Empty;
    public string ValidateInvoiceUrlSandbox { get; set; } = string.Empty;
    public string ValidateInvoiceUrlProduction { get; set; } = string.Empty;
    public string BearerToken { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;

    public string PostInvoiceUrl => UseSandbox ? PostInvoiceUrlSandbox : PostInvoiceUrlProduction;
    public bool HasToken => !string.IsNullOrWhiteSpace(BearerToken);
}

public class SellerSettings
{
    public string NTNCNIC { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string PosId { get; set; } = string.Empty;
}
