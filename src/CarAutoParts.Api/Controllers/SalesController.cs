using CarAutoParts.Application.Common;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/sales")]
public class SalesController : ApiControllerBase
{
    private readonly ISalesService _sales;

    public SalesController(ISalesService sales) => _sales = sales;

    [HttpGet("invoices")]
    [Authorize(Policy = Permissions.SalesView)]
    public async Task<IActionResult> GetInvoices([FromQuery] QuerySpec query, CancellationToken ct)
        => Ok(await _sales.GetInvoicesAsync(query, ct));

    [HttpGet("invoices/{id:int}")]
    [Authorize(Policy = Permissions.SalesView)]
    public async Task<IActionResult> GetInvoice(int id, CancellationToken ct)
        => NotFoundOrOk(await _sales.GetInvoiceByIdAsync(id, ct));

    [HttpGet("orders")]
    [Authorize(Policy = Permissions.SalesView)]
    public async Task<IActionResult> GetOrders([FromQuery] QuerySpec query, CancellationToken ct)
        => Ok(await _sales.GetOrdersAsync(query, ct));
}
