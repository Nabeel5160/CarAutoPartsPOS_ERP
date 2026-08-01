using CarAutoParts.Api.Contracts;
using CarAutoParts.Api.Filters;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.Config;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/serial-numbers")]
[RequireFeature(ConfigKeys.ModInvSerials)]
public class SerialNumbersController : ApiControllerBase
{
    private readonly ISerialNumberService _serials;

    public SerialNumbersController(ISerialNumberService serials) => _serials = serials;

    [HttpGet]
    [Authorize(Policy = Permissions.SerialNumbersView)]
    public async Task<IActionResult> GetAll([FromQuery] QuerySpec query, CancellationToken ct)
        => Ok(await _serials.GetSerialNumbersAsync(query, ct));

    [HttpGet("{id:int}/history")]
    [Authorize(Policy = Permissions.SerialNumbersView)]
    public async Task<IActionResult> GetHistory(int id, CancellationToken ct)
        => Ok(await _serials.GetHistoryAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = Permissions.SerialNumbersManage)]
    public async Task<IActionResult> Register([FromBody] RegisterSerialRequest request, CancellationToken ct)
        => FromResult(await _serials.RegisterSerialAsync(request.ProductId, request.Serial, request.WarehouseId, ct));
}
