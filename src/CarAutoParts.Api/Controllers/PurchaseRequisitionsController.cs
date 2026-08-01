using CarAutoParts.Application.Common;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Purchases;
using CarAutoParts.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/purchase-requisitions")]
[Route("api/v1/purchase-requisitions")]
public class PurchaseRequisitionsController : ApiControllerBase
{
    private readonly IPurchaseRequisitionService _service;

    public PurchaseRequisitionsController(IPurchaseRequisitionService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = Permissions.PurchasesRequisition)]
    public async Task<IActionResult> GetAll([FromQuery] QuerySpec query, CancellationToken ct) =>
        Ok(await _service.GetAllAsync(query, ct));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.PurchasesRequisition)]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.PurchasesRequisition)]
    public async Task<IActionResult> Create([FromBody] PurchaseRequisitionCreateDto dto, CancellationToken ct) =>
        FromResult(await _service.CreateAsync(dto, ct));

    [HttpPost("{id:int}/submit")]
    [Authorize(Policy = Permissions.PurchasesRequisition)]
    public async Task<IActionResult> Submit(int id, CancellationToken ct) =>
        FromResult(await _service.SubmitAsync(id, ct));

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = Permissions.PurchasesApprove)]
    public async Task<IActionResult> Approve(int id, CancellationToken ct) =>
        FromResult(await _service.ApproveAsync(id, ct));

    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = Permissions.PurchasesApprove)]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectRequisitionRequest? body, CancellationToken ct) =>
        FromResult(await _service.RejectAsync(id, body?.Reason ?? "Rejected", ct));

    [HttpPost("{id:int}/convert-to-po")]
    [Authorize(Policy = Permissions.PurchasesCreate)]
    public async Task<IActionResult> Convert(int id, CancellationToken ct) =>
        FromResult(await _service.ConvertToPoAsync(id, ct));
}

public sealed class RejectRequisitionRequest
{
    public string? Reason { get; set; }
}

[Authorize]
[Route("api/reorder")]
[Route("api/v1/reorder")]
public class ReorderController : ApiControllerBase
{
    private readonly IReorderService _reorder;

    public ReorderController(IReorderService reorder) => _reorder = reorder;

    [HttpGet("suggestions")]
    [Authorize(Policy = Permissions.PurchasesRequisition)]
    public async Task<IActionResult> Suggestions([FromQuery] int? warehouseId, CancellationToken ct) =>
        Ok(await _reorder.SuggestAsync(warehouseId, ct));

    [HttpPost("create-pr")]
    [Authorize(Policy = Permissions.PurchasesRequisition)]
    public async Task<IActionResult> CreatePr([FromBody] CreateReorderPrRequest request, CancellationToken ct) =>
        FromResult(await _reorder.CreateDraftPrFromSuggestionsAsync(
            request.Lines, request.SupplierId, request.WarehouseId, ct));
}
