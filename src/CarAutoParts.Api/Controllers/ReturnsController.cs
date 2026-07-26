using CarAutoParts.Application.Common;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Purchases;
using CarAutoParts.Application.DTOs.Sales;
using CarAutoParts.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/returns")]
public class ReturnsController : ApiControllerBase
{
    private readonly IReturnService _returns;

    public ReturnsController(IReturnService returns) => _returns = returns;

    [HttpGet("sales")]
    [Authorize(Policy = Permissions.ReturnsManage)]
    public async Task<IActionResult> GetSalesReturns([FromQuery] QuerySpec query, CancellationToken ct)
        => Ok(await _returns.GetSalesReturnsAsync(query, ct));

    [HttpPost("sales")]
    [Authorize(Policy = Permissions.ReturnsManage)]
    public async Task<IActionResult> CreateSalesReturn([FromBody] SalesReturnCreateDto dto, CancellationToken ct)
        => FromResult(await _returns.CreateSalesReturnAsync(dto, ct));

    [HttpPost("purchases")]
    [Authorize(Policy = Permissions.ReturnsManage)]
    public async Task<IActionResult> CreatePurchaseReturn([FromBody] PurchaseReturnCreateDto dto, CancellationToken ct)
        => FromResult(await _returns.CreatePurchaseReturnAsync(dto, ct));
}
