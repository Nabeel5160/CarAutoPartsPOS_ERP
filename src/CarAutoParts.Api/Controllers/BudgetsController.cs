using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

/// <summary>GL budgets + vs-actual (Program C2 — finance depth).</summary>
[Authorize]
[Route("api/budgets")]
[Route("api/v1/budgets")]
public class BudgetsController : ApiControllerBase
{
    private readonly IBudgetService _service;

    public BudgetsController(IBudgetService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = Permissions.FinanceView)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await _service.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.FinanceView)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var dto = await _service.GetByIdAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.FinanceManage)]
    public async Task<IActionResult> Create([FromBody] BudgetCreateRequest request, CancellationToken ct) =>
        FromResult(await _service.CreateAsync(request, ct));

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.FinanceManage)]
    public async Task<IActionResult> Update(int id, [FromBody] BudgetUpdateRequest request, CancellationToken ct) =>
        FromResult(await _service.UpdateAsync(id, request, ct));

    [HttpPost("{id:int}/lines")]
    [Authorize(Policy = Permissions.FinanceManage)]
    public async Task<IActionResult> AddLine(int id, [FromBody] BudgetLineUpsertRequest request, CancellationToken ct) =>
        FromResult(await _service.AddLineAsync(id, request, ct));

    [HttpDelete("{id:int}/lines/{lineId:int}")]
    [Authorize(Policy = Permissions.FinanceManage)]
    public async Task<IActionResult> DeleteLine(int id, int lineId, CancellationToken ct) =>
        FromResult(await _service.DeleteLineAsync(id, lineId, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.FinanceManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        FromResult(await _service.DeleteAsync(id, ct));
}
