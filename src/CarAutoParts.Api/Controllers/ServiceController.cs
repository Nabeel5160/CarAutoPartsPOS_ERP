using CarAutoParts.Api.Filters;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.Config;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Service;
using CarAutoParts.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

/// <summary>Program C1 — Service Light: thin ticket/warranty/AMC tracker.</summary>
[Authorize]
[Route("api/service")]
[Route("api/v1/service")]
[RequireFeature(ConfigKeys.ModServiceTickets)]
public class ServiceController : ApiControllerBase
{
    private readonly IServiceTicketService _service;

    public ServiceController(IServiceTicketService service) => _service = service;

    [HttpGet("tickets")]
    [Authorize(Policy = Permissions.ServiceView)]
    public async Task<IActionResult> GetTickets(
        [FromQuery] QuerySpec query,
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] int? customerId,
        [FromQuery] int? assignedToUserId,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(status)) query.Filters["status"] = status;
        if (!string.IsNullOrWhiteSpace(priority)) query.Filters["priority"] = priority;
        if (customerId is int cid) query.Filters["customerId"] = cid;
        if (assignedToUserId is int aid) query.Filters["assignedToUserId"] = aid;
        return Ok(await _service.GetTicketsAsync(query, ct));
    }

    [HttpGet("tickets/{id:int}")]
    [Authorize(Policy = Permissions.ServiceView)]
    public async Task<IActionResult> GetTicket(int id, CancellationToken ct)
        => NotFoundOrOk(await _service.GetTicketByIdAsync(id, ct));

    [HttpPost("tickets")]
    [Authorize(Policy = Permissions.ServiceManage)]
    public async Task<IActionResult> CreateTicket([FromBody] ServiceTicketCreateDto dto, CancellationToken ct)
        => FromResult(await _service.CreateTicketAsync(dto, ct));

    [HttpPut("tickets/{id:int}")]
    [Authorize(Policy = Permissions.ServiceManage)]
    public async Task<IActionResult> UpdateTicket(int id, [FromBody] ServiceTicketUpdateDto dto, CancellationToken ct)
        => FromResult(await _service.UpdateTicketAsync(id, dto, ct));

    [HttpPost("tickets/{id:int}/status")]
    [Authorize(Policy = Permissions.ServiceManage)]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] ServiceTicketStatusChangeDto dto, CancellationToken ct)
        => FromResult(await _service.ChangeStatusAsync(id, dto, ct));

    [HttpGet("customers/{customerId:int}/tickets")]
    [Authorize(Policy = Permissions.ServiceView)]
    public async Task<IActionResult> CustomerTickets(int customerId, CancellationToken ct)
        => Ok(await _service.GetTicketsForCustomerAsync(customerId, ct));

    /// <summary>Smoke: verifies Service module + company-scoped ticket list responds.</summary>
    [HttpGet("smoke")]
    [Authorize(Policy = Permissions.ServiceView)]
    public async Task<IActionResult> Smoke(CancellationToken ct)
    {
        var tickets = await _service.GetTicketsAsync(new QuerySpec { Page = 1, PageSize = 1 }, ct);
        return Ok(new { ok = true, ticketCount = tickets.TotalCount });
    }
}
