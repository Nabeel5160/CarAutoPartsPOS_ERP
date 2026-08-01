using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/approvals")]
[Route("api/v1/approvals")]
public class ApprovalsController : ApiControllerBase
{
    private readonly IApprovalWorkflowService _approvals;
    private readonly IDocumentVoidService _voids;

    public ApprovalsController(IApprovalWorkflowService approvals, IDocumentVoidService voids)
    {
        _approvals = approvals;
        _voids = voids;
    }

    [HttpGet("pending")]
    [Authorize(Policy = Permissions.ApprovalsView)]
    public async Task<IActionResult> Pending(CancellationToken ct)
        => Ok(await _approvals.ListPendingAsync(ct));

    [HttpPost("pending/{id:int}/decide")]
    [Authorize(Policy = Permissions.ApprovalsDecide)]
    public async Task<IActionResult> Decide(int id, [FromBody] DecideRequest body, CancellationToken ct)
        => FromResult(await _approvals.DecideAsync(id, body.Approve, body.Notes, ct));

    [HttpGet("policies")]
    [Authorize(Policy = Permissions.ApprovalsManage)]
    public async Task<IActionResult> Policies(CancellationToken ct)
        => Ok(await _approvals.ListPoliciesAsync(ct));

    [HttpPost("policies")]
    [Authorize(Policy = Permissions.ApprovalsManage)]
    public async Task<IActionResult> CreatePolicy([FromBody] ApprovalPolicyUpsertDto dto, CancellationToken ct)
        => FromResult(await _approvals.UpsertPolicyAsync(null, dto, ct));

    [HttpPut("policies/{id:int}")]
    [Authorize(Policy = Permissions.ApprovalsManage)]
    public async Task<IActionResult> UpdatePolicy(int id, [FromBody] ApprovalPolicyUpsertDto dto, CancellationToken ct)
        => FromResult(await _approvals.UpsertPolicyAsync(id, dto, ct));

    [HttpDelete("policies/{id:int}")]
    [Authorize(Policy = Permissions.ApprovalsManage)]
    public async Task<IActionResult> DeletePolicy(int id, CancellationToken ct)
        => FromResult(await _approvals.DeletePolicyAsync(id, ct));

    [HttpPost("void/journals/{id:int}")]
    [Authorize(Policy = Permissions.FinanceVoid)]
    public async Task<IActionResult> VoidJournal(int id, [FromBody] VoidRequest body, CancellationToken ct)
        => FromResult(await _voids.VoidJournalAsync(id, body.Reason, ct));

    [HttpPost("void/sales-invoices/{id:int}")]
    [Authorize(Policy = Permissions.FinanceVoid)]
    public async Task<IActionResult> VoidSalesInvoice(int id, [FromBody] VoidRequest body, CancellationToken ct)
        => FromResult(await _voids.VoidSalesInvoiceAsync(id, body.Reason, ct));

    [HttpPost("void/purchase-invoices/{id:int}")]
    [Authorize(Policy = Permissions.FinanceVoid)]
    public async Task<IActionResult> VoidPurchaseInvoice(int id, [FromBody] VoidRequest body, CancellationToken ct)
        => FromResult(await _voids.VoidPurchaseInvoiceAsync(id, body.Reason, ct));

    public record DecideRequest(bool Approve, string? Notes);
    public record VoidRequest(string? Reason);
}
