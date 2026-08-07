using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/sales-commissions")]
[Route("api/v1/sales-commissions")]
public class SalesCommissionsController : ApiControllerBase
{
    private readonly ISalesCommissionService _service;

    public SalesCommissionsController(ISalesCommissionService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = Permissions.SalesView)]
    public async Task<IActionResult> Get(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? userId,
        CancellationToken ct) =>
        Ok(await _service.GetAsync(from, to, userId, ct));
}
