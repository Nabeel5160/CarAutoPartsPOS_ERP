using CarAutoParts.Application.Common;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Products;
using CarAutoParts.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/warehouses")]
public class WarehousesController : ApiControllerBase
{
    private readonly IWarehouseService _warehouses;

    public WarehousesController(IWarehouseService warehouses) => _warehouses = warehouses;

    [HttpGet]
    [Authorize(Policy = Permissions.WarehousesView)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        if (page is null && pageSize is null && string.IsNullOrWhiteSpace(search))
            return Ok(await _warehouses.GetAllAsync(ct));

        return Ok(await _warehouses.GetPagedAsync(new QuerySpec
        {
            Page = page ?? 1,
            PageSize = pageSize ?? QueryLimits.DefaultPageSize,
            Search = search
        }, ct));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.WarehousesManage)]
    public async Task<IActionResult> Create([FromBody] WarehouseDto dto, CancellationToken ct)
        => FromResult(await _warehouses.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.WarehousesManage)]
    public async Task<IActionResult> Update(int id, [FromBody] WarehouseDto dto, CancellationToken ct)
        => FromResult(await _warehouses.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.WarehousesManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => FromResult(await _warehouses.DeleteAsync(id, ct));
}
