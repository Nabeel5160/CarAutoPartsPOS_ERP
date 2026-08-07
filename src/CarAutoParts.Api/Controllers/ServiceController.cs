using CarAutoParts.Api.Filters;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.Config;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Service;
using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

/// <summary>Program C1 Service Light + Program C2 SLA.</summary>
[Authorize]
[Route("api/service")]
[Route("api/v1/service")]
[RequireFeature(ConfigKeys.ModServiceTickets)]
public class ServiceController : ApiControllerBase
{
    private readonly IServiceTicketService _service;
    private readonly ISlaPolicyService _slaPolicies;
    private readonly ISlaClockService _slaClock;
    private readonly IKnowledgeBaseService _kb;
    private readonly IAmcContractService _amc;
    private readonly IServiceFieldService _field;

    public ServiceController(
        IServiceTicketService service,
        ISlaPolicyService slaPolicies,
        ISlaClockService slaClock,
        IKnowledgeBaseService kb,
        IAmcContractService amc,
        IServiceFieldService field)
    {
        _service = service;
        _slaPolicies = slaPolicies;
        _slaClock = slaClock;
        _kb = kb;
        _amc = amc;
        _field = field;
    }

    [HttpGet("tickets")]
    [Authorize(Policy = Permissions.ServiceView)]
    public async Task<IActionResult> GetTickets(
        [FromQuery] QuerySpec query,
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] int? customerId,
        [FromQuery] int? assignedToUserId,
        [FromQuery] bool? unassigned,
        [FromQuery] string? slaStatus,
        [FromQuery] bool? warrantyOnly,
        [FromQuery] string? warrantyClaimStatus,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(status)) query.Filters["status"] = status;
        if (!string.IsNullOrWhiteSpace(priority)) query.Filters["priority"] = priority;
        if (customerId is int cid) query.Filters["customerId"] = cid;
        if (assignedToUserId is int aid) query.Filters["assignedToUserId"] = aid;
        if (unassigned is true) query.Filters["unassigned"] = true;
        if (!string.IsNullOrWhiteSpace(slaStatus)) query.Filters["slaStatus"] = slaStatus;
        if (warrantyOnly is true) query.Filters["warrantyOnly"] = true;
        if (!string.IsNullOrWhiteSpace(warrantyClaimStatus)) query.Filters["warrantyClaimStatus"] = warrantyClaimStatus;
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

    [HttpPost("tickets/{id:int}/warranty-decision")]
    [Authorize(Policy = Permissions.ServiceManage)]
    public async Task<IActionResult> DecideWarranty(int id, [FromBody] WarrantyClaimDecisionDto dto, CancellationToken ct)
        => FromResult(await _service.DecideWarrantyAsync(id, dto, ct));

    [HttpGet("customers/{customerId:int}/tickets")]
    [Authorize(Policy = Permissions.ServiceView)]
    public async Task<IActionResult> CustomerTickets(int customerId, CancellationToken ct)
        => Ok(await _service.GetTicketsForCustomerAsync(customerId, ct));

    [HttpGet("tickets/{id:int}/sla")]
    [Authorize(Policy = Permissions.ServiceView)]
    public async Task<IActionResult> TicketSla(int id, CancellationToken ct)
        => NotFoundOrOk(await _slaClock.GetTicketSlaAsync(id, ct));

    [HttpPost("tickets/{id:int}/sla/pause")]
    [Authorize(Policy = Permissions.ServiceManage)]
    public async Task<IActionResult> PauseSla(int id, [FromBody] SlaPauseDto dto, CancellationToken ct)
        => FromResult(await _slaClock.PauseAsync(id, dto, ct));

    [HttpPost("tickets/{id:int}/sla/resume")]
    [Authorize(Policy = Permissions.ServiceManage)]
    public async Task<IActionResult> ResumeSla(int id, CancellationToken ct)
        => FromResult(await _slaClock.ResumeAsync(id, ct));

    [HttpGet("sla/policies")]
    [Authorize(Policy = Permissions.ServiceManage)]
    public async Task<IActionResult> ListPolicies([FromQuery] SlaEntityType? entityType, CancellationToken ct)
        => Ok(await _slaPolicies.ListAsync(entityType, ct));

    [HttpPost("sla/policies")]
    [Authorize(Policy = Permissions.ServiceManage)]
    public async Task<IActionResult> UpsertPolicy([FromBody] SlaPolicyUpsertDto dto, CancellationToken ct)
        => FromResult(await _slaPolicies.UpsertAsync(dto, ct));

    [HttpPut("sla/policies/{id:int}")]
    [Authorize(Policy = Permissions.ServiceManage)]
    public async Task<IActionResult> UpdatePolicy(int id, [FromBody] SlaPolicyUpsertDto dto, CancellationToken ct)
        => FromResult(await _slaPolicies.UpsertAsync(dto with { Id = id }, ct));

    [HttpPost("sla/policies/{id:int}/default")]
    [Authorize(Policy = Permissions.ServiceManage)]
    public async Task<IActionResult> SetDefaultPolicy(int id, CancellationToken ct)
        => FromResult(await _slaPolicies.SetDefaultAsync(id, ct));

    [HttpGet("sla/policies/{id:int}/rules")]
    [Authorize(Policy = Permissions.ServiceManage)]
    public async Task<IActionResult> ListRules(int id, CancellationToken ct)
        => Ok(await _slaPolicies.ListRulesAsync(id, ct));

    [HttpPost("sla/policies/{id:int}/rules")]
    [Authorize(Policy = Permissions.ServiceManage)]
    public async Task<IActionResult> UpsertRule(int id, [FromBody] SlaPolicyRuleUpsertDto dto, CancellationToken ct)
        => FromResult(await _slaPolicies.UpsertRuleAsync(id, dto, ct));

    [HttpPut("sla/policies/{policyId:int}/rules/{ruleId:int}")]
    [Authorize(Policy = Permissions.ServiceManage)]
    public async Task<IActionResult> UpdateRule(int policyId, int ruleId, [FromBody] SlaPolicyRuleUpsertDto dto, CancellationToken ct)
        => FromResult(await _slaPolicies.UpsertRuleAsync(policyId, dto with { Id = ruleId }, ct));

    [HttpDelete("sla/policies/{policyId:int}/rules/{ruleId:int}")]
    [Authorize(Policy = Permissions.ServiceManage)]
    public async Task<IActionResult> DeleteRule(int policyId, int ruleId, CancellationToken ct)
        => FromResult(await _slaPolicies.DeleteRuleAsync(policyId, ruleId, ct));

    [HttpGet("sla/calendar")]
    [Authorize(Policy = Permissions.ServiceManage)]
    public async Task<IActionResult> GetCalendar(CancellationToken ct)
        => NotFoundOrOk(await _slaPolicies.GetCalendarAsync(ct));

    [HttpPut("sla/calendar")]
    [Authorize(Policy = Permissions.ServiceManage)]
    public async Task<IActionResult> UpsertCalendar([FromBody] BusinessCalendarUpsertDto dto, CancellationToken ct)
        => FromResult(await _slaPolicies.UpsertCalendarAsync(dto, ct));

    [HttpGet("sla/dashboard")]
    [Authorize(Policy = Permissions.ServiceView)]
    public async Task<IActionResult> SlaDashboard([FromQuery] SlaEntityType? entityType, CancellationToken ct)
        => Ok(await _slaPolicies.GetDashboardAsync(entityType, ct));

    [HttpGet("sla/breaches")]
    [Authorize(Policy = Permissions.ServiceView)]
    public async Task<IActionResult> BreachQueue(
        [FromQuery] SlaEntityType? entityType,
        [FromQuery] int? policyId,
        CancellationToken ct)
        => Ok(await _slaPolicies.GetBreachQueueAsync(entityType, policyId, ct));

    [HttpGet("sla/alerts")]
    [Authorize(Policy = Permissions.ServiceView)]
    public async Task<IActionResult> OpenAlerts([FromQuery] SlaEntityType entityType, CancellationToken ct)
        => Ok(await _slaPolicies.GetOpenAlertsAsync(entityType, ct));

    /// <summary>Smoke: Service module + SLA default policy + optional timer attach.</summary>
    [HttpGet("smoke")]
    [Authorize(Policy = Permissions.ServiceView)]
    public async Task<IActionResult> Smoke(CancellationToken ct)
    {
        var tickets = await _service.GetTicketsAsync(new QuerySpec { Page = 1, PageSize = 1 }, ct);
        var policies = await _slaPolicies.ListAsync(ct: ct);
        return Ok(new
        {
            ok = true,
            ticketCount = tickets.TotalCount,
            slaPolicyCount = policies.Count,
            hasDefaultSla = policies.Any(p => p.IsDefault)
        });
    }

    [HttpGet("sla/smoke")]
    [Authorize(Policy = Permissions.ServiceView)]
    public async Task<IActionResult> SlaSmoke(CancellationToken ct)
    {
        var policies = await _slaPolicies.ListAsync(ct: ct);
        var dash = await _slaPolicies.GetDashboardAsync(ct: ct);
        var breaches = await _slaPolicies.GetBreachQueueAsync(ct: ct);
        return Ok(new
        {
            ok = policies.Count > 0 && policies.Any(p => p.IsDefault),
            policyCount = policies.Count,
            hasDefaultSla = policies.Any(p => p.IsDefault),
            breachesOk = true,
            breachCount = breaches.Count,
            dashboard = dash
        });
    }

    [HttpGet("kb")]
    [Authorize(Policy = Permissions.ServiceView)]
    public async Task<IActionResult> ListKb(
        [FromQuery] string? q,
        [FromQuery] bool publishedOnly = false,
        CancellationToken ct = default)
        => Ok(await _kb.ListAsync(q, publishedOnly, ct));

    [HttpGet("kb/{id:int}")]
    [Authorize(Policy = Permissions.ServiceView)]
    public async Task<IActionResult> GetKb(int id, CancellationToken ct)
        => NotFoundOrOk(await _kb.GetByIdAsync(id, ct));

    [HttpPost("kb")]
    [Authorize(Policy = Permissions.ServiceManage)]
    public async Task<IActionResult> UpsertKb([FromBody] KbArticleUpsertDto dto, CancellationToken ct)
        => FromResult(await _kb.UpsertAsync(dto, ct));

    [HttpPut("kb/{id:int}")]
    [Authorize(Policy = Permissions.ServiceManage)]
    public async Task<IActionResult> UpdateKb(int id, [FromBody] KbArticleUpsertDto dto, CancellationToken ct)
        => FromResult(await _kb.UpsertAsync(dto with { Id = id }, ct));

    [HttpDelete("kb/{id:int}")]
    [Authorize(Policy = Permissions.ServiceManage)]
    public async Task<IActionResult> DeleteKb(int id, CancellationToken ct)
        => FromResult(await _kb.SoftDeleteAsync(id, ct));

    // —— AMC contracts (Phase 8 Wave 1) ——
    [HttpGet("amc")]
    [Authorize(Policy = Permissions.ServiceView)]
    public async Task<IActionResult> ListAmc(
        [FromQuery] int? customerId,
        [FromQuery] AmcContractStatus? status,
        CancellationToken ct)
        => Ok(await _amc.ListAsync(customerId, status, ct));

    [HttpGet("amc/{id:int}")]
    [Authorize(Policy = Permissions.ServiceView)]
    public async Task<IActionResult> GetAmc(int id, CancellationToken ct)
        => NotFoundOrOk(await _amc.GetByIdAsync(id, ct));

    [HttpPost("amc")]
    [Authorize(Policy = Permissions.ServiceManage)]
    public async Task<IActionResult> UpsertAmc([FromBody] AmcContractUpsertDto dto, CancellationToken ct)
        => FromResult(await _amc.UpsertAsync(dto, ct));

    [HttpPut("amc/{id:int}")]
    [Authorize(Policy = Permissions.ServiceManage)]
    public async Task<IActionResult> UpdateAmc(int id, [FromBody] AmcContractUpsertDto dto, CancellationToken ct)
        => FromResult(await _amc.UpsertAsync(dto with { Id = id }, ct));

    [HttpDelete("amc/{id:int}")]
    [Authorize(Policy = Permissions.ServiceManage)]
    public async Task<IActionResult> DeleteAmc(int id, CancellationToken ct)
        => FromResult(await _amc.SoftDeleteAsync(id, ct));

    // —— Visits + parts (Phase 8 Wave 2) ——
    [HttpGet("tickets/{ticketId:int}/visits")]
    [Authorize(Policy = Permissions.ServiceView)]
    public async Task<IActionResult> TicketVisits(int ticketId, CancellationToken ct)
        => Ok(await _field.GetVisitsForTicketAsync(ticketId, ct));

    [HttpGet("visits/mine")]
    [Authorize(Policy = Permissions.ServiceView)]
    public async Task<IActionResult> MyVisits([FromQuery] DateTime? day, CancellationToken ct)
        => Ok(await _field.GetMyVisitsAsync(day, ct));

    [HttpPost("visits")]
    [Authorize(Policy = Permissions.ServiceManage)]
    public async Task<IActionResult> ScheduleVisit([FromBody] ServiceVisitCreateDto dto, CancellationToken ct)
        => FromResult(await _field.ScheduleVisitAsync(dto, ct));

    [HttpPost("visits/{id:int}/status")]
    [Authorize(Policy = Permissions.ServiceManage)]
    public async Task<IActionResult> ChangeVisitStatus(int id, [FromBody] ServiceVisitStatusDto dto, CancellationToken ct)
        => FromResult(await _field.ChangeVisitStatusAsync(id, dto, ct));

    [HttpGet("tickets/{ticketId:int}/parts")]
    [Authorize(Policy = Permissions.ServiceView)]
    public async Task<IActionResult> TicketParts(int ticketId, CancellationToken ct)
        => Ok(await _field.GetPartsForTicketAsync(ticketId, ct));

    [HttpPost("parts")]
    [Authorize(Policy = Permissions.ServiceManage)]
    public async Task<IActionResult> ConsumePart([FromBody] ServiceTicketPartCreateDto dto, CancellationToken ct)
        => FromResult(await _field.ConsumePartAsync(dto, ct));
}
