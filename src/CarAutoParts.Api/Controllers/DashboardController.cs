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
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await _dashboard.GetDashboardAsync(ct));
}
