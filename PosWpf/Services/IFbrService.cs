using PosWpf.Models.Fbr;

namespace PosWpf.Services;

public interface IFbrService
{
    Task<FbrPostResult> PostInvoiceAsync(FbrInvoiceRequest request, CancellationToken cancellationToken = default);
}
