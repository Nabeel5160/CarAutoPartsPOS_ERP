using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Interfaces;

namespace CarAutoParts.Infrastructure.Services;

public sealed class FbrOutboxService : IFbrOutboxService
{
    private readonly IOutboxWriter _outbox;

    public FbrOutboxService(IOutboxWriter outbox) => _outbox = outbox;

    public void EnqueueFbrRetry(int salesInvoiceId, string? requestJson = null)
    {
        _outbox.Enqueue("FbrSubmissionRequested", new FbrSubmissionRequested(salesInvoiceId, requestJson));
    }
}
