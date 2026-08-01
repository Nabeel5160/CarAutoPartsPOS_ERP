using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/dashboard")]
public class DashboardController : ApiControllerBase
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    [HttpGet]
    [Authorize(Policy = Permissions.DashboardView)]
    public async Task<IActionResult> Get([FromQuery] int? branchId, CancellationToken ct)
        => Ok(await _dashboard.GetDashboardAsync(branchId, ct));

    [HttpGet("timeline")]
    [Authorize(Policy = Permissions.DashboardView)]
    public async Task<IActionResult> Timeline(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string grain = "day",
        [FromQuery] string groupBy = "category",
        [FromQuery] int? branchId = null,
        CancellationToken ct = default)
        => Ok(await _dashboard.GetTimelineAsync(from, to, grain, groupBy, branchId, ct));
}
