using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Products;
using CarAutoParts.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/warehouses/{warehouseId:int}/locations")]
public class WarehouseLocationsController : ApiControllerBase
{
    private readonly IWarehouseLocationService _locations;

    public WarehouseLocationsController(IWarehouseLocationService locations) => _locations = locations;

    [HttpGet]
    [Authorize(Policy = Permissions.WarehousesView)]
    public async Task<IActionResult> GetAll(int warehouseId, CancellationToken ct)
        => Ok(await _locations.GetByWarehouseAsync(warehouseId, ct));

    [HttpGet("balances")]
    [Authorize(Policy = Permissions.WarehousesView)]
    public async Task<IActionResult> GetBalances(int warehouseId, [FromQuery] int? locationId, CancellationToken ct)
        => Ok(await _locations.GetBalancesAsync(warehouseId, locationId, ct));

    [HttpPost]
    [Authorize(Policy = Permissions.WarehousesManage)]
    public async Task<IActionResult> Create(int warehouseId, [FromBody] UpsertWarehouseLocationDto dto, CancellationToken ct)
        => FromResult(await _locations.CreateAsync(warehouseId, dto, ct));

    [HttpPut("{locationId:int}")]
    [Authorize(Policy = Permissions.WarehousesManage)]
    public async Task<IActionResult> Update(int warehouseId, int locationId, [FromBody] UpsertWarehouseLocationDto dto, CancellationToken ct)
        => FromResult(await _locations.UpdateAsync(locationId, dto, ct));

    [HttpDelete("{locationId:int}")]
    [Authorize(Policy = Permissions.WarehousesManage)]
    public async Task<IActionResult> Delete(int warehouseId, int locationId, CancellationToken ct)
        => FromResult(await _locations.DeleteAsync(locationId, ct));
}
