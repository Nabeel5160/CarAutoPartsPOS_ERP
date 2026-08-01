using CarAutoParts.Api.Contracts;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Inventory;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/inventory")]
public class InventoryController : ApiControllerBase
{
    private readonly IInventoryService _inventory;
    private readonly IAtpService _atp;

    public InventoryController(IInventoryService inventory, IAtpService atp)
    {
        _inventory = inventory;
        _atp = atp;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.InventoryView)]
    public async Task<IActionResult> GetInventory([FromQuery] InventoryQueryDto query, CancellationToken ct)
        => Ok(await _inventory.GetInventoryAsync(query, ct));

    [HttpGet("movements")]
    [Authorize(Policy = Permissions.InventoryView)]
    public async Task<IActionResult> GetMovements([FromQuery] StockMovementQueryDto query, CancellationToken ct)
        => Ok(await _inventory.GetMovementsAsync(query, ct));

    [HttpPost("adjust")]
    [Authorize(Policy = Permissions.InventoryAdjust)]
    public async Task<IActionResult> Adjust([FromBody] StockAdjustmentDto dto, CancellationToken ct)
        => FromResult(await _inventory.AdjustStockAsync(dto, ct));

    [HttpPost("receive")]
    [Authorize(Policy = Permissions.InventoryReceive)]
    public async Task<IActionResult> Receive([FromBody] ReceiveStockRequest request, CancellationToken ct)
        => FromResult(await _inventory.ReceiveStockAsync(
            request.ProductId, request.WarehouseId, request.Quantity, request.UnitCost, request.BatchNumber, ct));

    [HttpPost("deduct")]
    [Authorize(Policy = Permissions.InventoryAdjust)]
    public async Task<IActionResult> Deduct([FromBody] DeductStockRequest request, CancellationToken ct)
        => FromResult(await _inventory.DeductStockAsync(
            request.ProductId, request.WarehouseId, request.Quantity, request.ReferenceType, request.ReferenceId, ct));

    [HttpPost("return-stock")]
    [Authorize(Policy = Permissions.InventoryAdjust)]
    public async Task<IActionResult> ReturnStock([FromBody] ReturnStockRequest request, CancellationToken ct)
        => FromResult(await _inventory.ReturnStockAsync(
            request.ProductId, request.WarehouseId, request.Quantity, request.ReferenceType, request.ReferenceId, ct));

    [HttpGet("alerts/low-stock")]
    [Authorize(Policy = Permissions.InventoryView)]
    public async Task<IActionResult> LowStock(CancellationToken ct)
        => Ok(await _inventory.GetLowStockAlertsAsync(ct));

    [HttpGet("alerts/overstock")]
    [Authorize(Policy = Permissions.InventoryView)]
    public async Task<IActionResult> Overstock(CancellationToken ct)
        => Ok(await _inventory.GetOverstockAlertsAsync(ct));

    [HttpGet("value")]
    [Authorize(Policy = Permissions.InventoryView)]
    public async Task<IActionResult> Value(
        [FromQuery] string? method,
        [FromQuery] int? warehouseId,
        [FromQuery] int? branchId,
        CancellationToken ct)
        => Ok(await _inventory.GetInventoryValueAsync(method, warehouseId, branchId, ct));

    [HttpGet("atp")]
    [Authorize(Policy = Permissions.InventoryView)]
    public async Task<IActionResult> Atp([FromQuery] int? productId, [FromQuery] int? warehouseId, CancellationToken ct)
        => Ok(await _atp.GetAsync(productId, warehouseId, ct));
}
