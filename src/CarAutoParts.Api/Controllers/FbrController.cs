using CarAutoParts.Api.Filters;
using CarAutoParts.Application.Config;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Fbr;
using CarAutoParts.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/fbr")]
[RequireFeature(ConfigKeys.ModSalesFbr)]
public class FbrController : ApiControllerBase
{
    private readonly IFbrService _fbr;

    public FbrController(IFbrService fbr) => _fbr = fbr;

    [HttpPost("invoices")]
    [Authorize(Policy = Permissions.PosCheckout)]
    public async Task<IActionResult> PostInvoice([FromBody] FbrInvoiceRequestDto request, CancellationToken ct)
        => Ok(await _fbr.PostInvoiceAsync(request, ct));
}
