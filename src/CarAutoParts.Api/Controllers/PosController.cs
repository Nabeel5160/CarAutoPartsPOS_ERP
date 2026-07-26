using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Pos;
using CarAutoParts.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/pos")]
[Route("api/v1/pos")]
public class PosController : ApiControllerBase
{
    private readonly IPosCheckoutService _pos;
    private readonly IPosFloorService _floor;

    public PosController(IPosCheckoutService pos, IPosFloorService floor)
    {
        _pos = pos;
        _floor = floor;
    }

    [HttpGet("products")]
    [Authorize(Policy = Permissions.PosCheckout)]
    public async Task<IActionResult> GetProducts([FromQuery] string? search, CancellationToken ct)
        => Ok(await _pos.GetPosProductsAsync(search, ct));

    [HttpPost("checkout")]
    [Authorize(Policy = Permissions.PosCheckout)]
    public async Task<IActionResult> Checkout([FromBody] PosCheckoutDto dto, CancellationToken ct)
        => Ok(await _pos.CheckoutAsync(dto, ct));

    [HttpGet("receipts/{invoiceId:int}")]
    [Authorize(Policy = Permissions.PosCheckout)]
    public async Task<IActionResult> Receipt(int invoiceId, CancellationToken ct)
    {
        var html = await _pos.GetReceiptHtmlAsync(invoiceId, ct);
        return Content(html, "text/html");
    }

    [HttpPost("holds")]
    [Authorize(Policy = Permissions.PosHold)]
    public async Task<IActionResult> Hold([FromBody] HoldSaleRequestDto dto, CancellationToken ct)
        => FromResult(await _floor.HoldAsync(dto, ct));

    [HttpGet("holds")]
    [Authorize(Policy = Permissions.PosHold)]
    public async Task<IActionResult> ListHolds(CancellationToken ct)
        => Ok(await _floor.ListHeldAsync(ct));

    [HttpPost("holds/{id:int}/recall")]
    [Authorize(Policy = Permissions.PosHold)]
    public async Task<IActionResult> Recall(int id, CancellationToken ct)
        => FromResult(await _floor.RecallAsync(id, ct));

    [HttpPost("holds/{id:int}/discard")]
    [Authorize(Policy = Permissions.PosHold)]
    public async Task<IActionResult> Discard(int id, CancellationToken ct)
        => FromResult(await _floor.DiscardHeldAsync(id, ct));

    [HttpGet("shifts/current")]
    [Authorize(Policy = Permissions.PosShift)]
    public async Task<IActionResult> CurrentShift(CancellationToken ct)
        => Ok(await _floor.GetOpenShiftAsync(ct));

    [HttpPost("shifts/open")]
    [Authorize(Policy = Permissions.PosShift)]
    public async Task<IActionResult> OpenShift([FromBody] OpenShiftRequestDto dto, CancellationToken ct)
        => FromResult(await _floor.OpenShiftAsync(dto, ct));

    [HttpPost("shifts/{id:int}/close")]
    [Authorize(Policy = Permissions.PosShift)]
    public async Task<IActionResult> CloseShift(int id, [FromBody] CloseShiftRequestDto dto, CancellationToken ct)
        => FromResult(await _floor.CloseShiftAsync(id, dto, ct));

    [HttpGet("shifts/{id:int}/z-report")]
    [Authorize(Policy = Permissions.PosShift)]
    public async Task<IActionResult> ZReport(int id, CancellationToken ct)
        => FromResult(await _floor.GetZReportAsync(id, ct));
}
