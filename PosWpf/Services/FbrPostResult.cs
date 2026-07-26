using PosWpf.Models.Fbr;

namespace PosWpf.Services;

/// <summary>Outcome of an attempt to post an invoice to FBR.</summary>
public class FbrPostResult
{
    public bool Success { get; init; }
    public string? InvoiceNumber { get; init; }
    public string? Message { get; init; }
    public bool WasStubbed { get; init; }
    public FbrInvoiceResponse? Raw { get; init; }
    public string? RequestJson { get; init; }
    public string? ResponseJson { get; init; }

    public static FbrPostResult Ok(string invoiceNumber, bool stubbed, FbrInvoiceResponse? raw, string? reqJson, string? respJson, string? message = null)
        => new()
        {
            Success = true,
            InvoiceNumber = invoiceNumber,
            WasStubbed = stubbed,
            Raw = raw,
            RequestJson = reqJson,
            ResponseJson = respJson,
            Message = message ?? (stubbed ? "Posted in OFFLINE/STUB mode (no FBR token configured)." : "Accepted by FBR.")
        };

    public static FbrPostResult Fail(string message, string? reqJson = null, string? respJson = null)
        => new() { Success = false, Message = message, RequestJson = reqJson, ResponseJson = respJson };
}
