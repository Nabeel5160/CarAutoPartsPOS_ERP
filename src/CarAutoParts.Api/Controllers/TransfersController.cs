using CarAutoParts.Application.Common;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Transfers;
using CarAutoParts.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/transfers")]
public class TransfersController : ApiControllerBase
{
    private readonly ITransferService _transfers;

    public TransfersController(ITransferService transfers) => _transfers = transfers;

    [HttpGet]
    [Authorize(Policy = Permissions.TransfersView)]
    public async Task<IActionResult> GetAll([FromQuery] QuerySpec query, CancellationToken ct)
        => Ok(await _transfers.GetTransfersAsync(query, ct));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.TransfersView)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => NotFoundOrOk(await _transfers.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = Permissions.TransfersCreate)]
    public async Task<IActionResult> Create([FromBody] TransferCreateDto dto, CancellationToken ct)
        => FromResult(await _transfers.CreateAsync(dto, ct));

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = Permissions.TransfersApprove)]
    public async Task<IActionResult> Approve(int id, CancellationToken ct)
        => FromResult(await _transfers.ApproveAsync(id, ct));

    [HttpPost("{id:int}/ship")]
    [Authorize(Policy = Permissions.TransfersApprove)]
    public async Task<IActionResult> Ship(int id, CancellationToken ct)
        => FromResult(await _transfers.ShipAsync(id, ct));

    [HttpPost("{id:int}/complete")]
    [Authorize(Policy = Permissions.TransfersApprove)]
    public async Task<IActionResult> Complete(int id, CancellationToken ct)
        => FromResult(await _transfers.CompleteAsync(id, ct));
}
