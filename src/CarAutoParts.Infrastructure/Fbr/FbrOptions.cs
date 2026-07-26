namespace CarAutoParts.Infrastructure.Fbr;

public class FbrOptions
{
    public const string SectionName = "Fbr";

    public bool UseSandbox { get; set; } = true;
    public string PostInvoiceUrlSandbox { get; set; } = "https://gw.fbr.gov.pk/di_data/v1/di/postinvoicedata_sb";
    public string PostInvoiceUrlProduction { get; set; } = "https://gw.fbr.gov.pk/di_data/v1/di/postinvoicedata";
    public string BearerToken { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 60;

    public string PostInvoiceUrl => UseSandbox ? PostInvoiceUrlSandbox : PostInvoiceUrlProduction;
    public bool HasToken => !string.IsNullOrWhiteSpace(BearerToken);
}
