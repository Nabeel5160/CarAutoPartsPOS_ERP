using CarAutoParts.Application.Common;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Purchases;
using CarAutoParts.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

/// <summary>Purchasing RFQ: create → collect vendor quotes → compare → link/create PO (Program B — thin).</summary>
[Authorize]
[Route("api/rfq")]
[Route("api/v1/rfq")]
public class RfqController : ApiControllerBase
{
    private readonly IPurchaseRfqService _service;

    public RfqController(IPurchaseRfqService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = Permissions.PurchasesView)]
    public async Task<IActionResult> GetAll([FromQuery] QuerySpec query, CancellationToken ct) =>
        Ok(await _service.GetAllAsync(query, ct));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.PurchasesView)]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.RfqManage)]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseRfqRequest request, CancellationToken ct) =>
        FromResult(await _service.CreateAsync(request, ct));

    [HttpPost("{id:int}/send")]
    [Authorize(Policy = Permissions.RfqManage)]
    public async Task<IActionResult> Send(int id, CancellationToken ct) =>
        FromResult(await _service.SendAsync(id, ct));

    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = Permissions.RfqManage)]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct) =>
        FromResult(await _service.CancelAsync(id, ct));

    [HttpPost("{id:int}/quotes")]
    [Authorize(Policy = Permissions.RfqManage)]
    public async Task<IActionResult> AddQuote(int id, [FromBody] CreateVendorQuoteRequest request, CancellationToken ct) =>
        FromResult(await _service.AddVendorQuoteAsync(id, request, ct));

    [HttpPost("quotes/{quoteId:int}/select")]
    [Authorize(Policy = Permissions.RfqManage)]
    public async Task<IActionResult> SelectQuote(int quoteId, CancellationToken ct) =>
        FromResult(await _service.SelectVendorQuoteAsync(quoteId, ct));

    [HttpPost("quotes/{quoteId:int}/create-po")]
    [Authorize(Policy = Permissions.RfqManage)]
    public async Task<IActionResult> CreatePo(int quoteId, CancellationToken ct) =>
        FromResult(await _service.CreatePoFromQuoteAsync(quoteId, ct));
}
