using CarAutoParts.Application.Common;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Purchases;
using CarAutoParts.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/purchase-orders")]
public class PurchaseOrdersController : ApiControllerBase
{
    private readonly IPurchaseOrderService _orders;

    public PurchaseOrdersController(IPurchaseOrderService orders) => _orders = orders;

    [HttpGet]
    [Authorize(Policy = Permissions.PurchasesView)]
    public async Task<IActionResult> GetAll([FromQuery] QuerySpec query, CancellationToken ct)
        => Ok(await _orders.GetOrdersAsync(query, ct));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.PurchasesView)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => NotFoundOrOk(await _orders.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = Permissions.PurchasesCreate)]
    public async Task<IActionResult> Create([FromBody] PurchaseOrderCreateDto dto, CancellationToken ct)
        => FromResult(await _orders.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.PurchasesCreate)]
    public async Task<IActionResult> Update(int id, [FromBody] PurchaseOrderCreateDto dto, CancellationToken ct)
        => FromResult(await _orders.UpdateAsync(id, dto, ct));

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = Permissions.PurchasesApprove)]
    public async Task<IActionResult> Approve(int id, CancellationToken ct)
        => FromResult(await _orders.ApproveAsync(id, ct));

    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = Permissions.PurchasesCreate)]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
        => FromResult(await _orders.CancelAsync(id, ct));

    [HttpPost("{id:int}/receive")]
    [Authorize(Policy = Permissions.PurchasesReceive)]
    public async Task<IActionResult> Receive(int id, [FromBody] ReceivePurchaseOrderDto dto, CancellationToken ct)
        => FromResult(await _orders.ReceiveAsync(id, dto, ct));
}
