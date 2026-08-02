using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

/// <summary>Monthly salesperson targets — thin CRUD (Program B — sales thin).</summary>
[Authorize]
[Route("api/sales-targets")]
[Route("api/v1/sales-targets")]
public class SalesTargetsController : ApiControllerBase
{
    private readonly ISalesTargetService _service;

    public SalesTargetsController(ISalesTargetService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = Permissions.SalesView)]
    public async Task<IActionResult> GetAll([FromQuery] int? userId, [FromQuery] int? year, CancellationToken ct) =>
        Ok(await _service.GetAllAsync(userId, year, ct));

    [HttpPost]
    [Authorize(Policy = Permissions.SalesTargetsManage)]
    public async Task<IActionResult> Create([FromBody] SalesTargetUpsertRequest request, CancellationToken ct) =>
        FromResult(await _service.CreateAsync(request, ct));

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.SalesTargetsManage)]
    public async Task<IActionResult> Update(int id, [FromBody] SalesTargetUpsertRequest request, CancellationToken ct) =>
        FromResult(await _service.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.SalesTargetsManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        FromResult(await _service.DeleteAsync(id, ct));
}
