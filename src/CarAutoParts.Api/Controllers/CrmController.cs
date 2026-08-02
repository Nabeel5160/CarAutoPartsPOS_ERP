using CarAutoParts.Api.Filters;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.Config;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Crm;
using CarAutoParts.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/crm")]
[Route("api/v1/crm")]
[RequireFeature(ConfigKeys.ModSalesCrm)]
public class CrmController : ApiControllerBase
{
    private readonly ICrmService _crm;

    public CrmController(ICrmService crm) => _crm = crm;

    [HttpGet("leads")]
    [Authorize(Policy = Permissions.CrmView)]
    public async Task<IActionResult> GetLeads(
        [FromQuery] QuerySpec query,
        [FromQuery] string? status,
        [FromQuery] string? source,
        [FromQuery] int? ownerUserId,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(status)) query.Filters["status"] = status;
        if (!string.IsNullOrWhiteSpace(source)) query.Filters["source"] = source;
        if (ownerUserId is int oid) query.Filters["ownerUserId"] = oid;
        return Ok(await _crm.GetLeadsAsync(query, ct));
    }

    [HttpGet("leads/duplicates")]
    [Authorize(Policy = Permissions.CrmView)]
    public async Task<IActionResult> Duplicates([FromQuery] string? phone, [FromQuery] string? email, [FromQuery] string? name, [FromQuery] int? excludeLeadId, CancellationToken ct)
        => Ok(await _crm.FindDuplicatesAsync(phone, email, name, excludeLeadId, ct));

    [HttpGet("leads/{id:int}")]
    [Authorize(Policy = Permissions.CrmView)]
    public async Task<IActionResult> GetLead(int id, CancellationToken ct)
        => NotFoundOrOk(await _crm.GetLeadByIdAsync(id, ct));

    [HttpPost("leads")]
    [Authorize(Policy = Permissions.CrmLeads)]
    public async Task<IActionResult> CreateLead([FromBody] LeadCreateDto dto, CancellationToken ct)
        => FromResult(await _crm.CreateLeadAsync(dto, ct));

    [HttpPut("leads/{id:int}")]
    [Authorize(Policy = Permissions.CrmLeads)]
    public async Task<IActionResult> UpdateLead(int id, [FromBody] LeadUpdateDto dto, CancellationToken ct)
        => FromResult(await _crm.UpdateLeadAsync(id, dto, ct));

    [HttpPost("leads/{id:int}/convert-customer")]
    [Authorize(Policy = Permissions.CrmLeads)]
    public async Task<IActionResult> ConvertCustomer(int id, [FromBody] ConvertCustomerRequest? body, CancellationToken ct)
        => FromResult(await _crm.ConvertLeadToCustomerAsync(id, body?.ExistingCustomerId, ct));

    [HttpPost("leads/{id:int}/convert-opportunity")]
    [Authorize(Policy = Permissions.CrmLeads)]
    public async Task<IActionResult> ConvertOpportunity(int id, [FromBody] ConvertLeadToOpportunityDto dto, CancellationToken ct)
        => FromResult(await _crm.ConvertLeadToOpportunityAsync(id, dto, ct));

    [HttpGet("activities")]
    [Authorize(Policy = Permissions.CrmView)]
    public async Task<IActionResult> GetActivities(
        [FromQuery] QuerySpec query,
        [FromQuery] int? leadId,
        [FromQuery] int? customerId,
        [FromQuery] bool? myDay,
        [FromQuery] bool? overdue,
        CancellationToken ct)
    {
        if (leadId is int lid) query.Filters["leadId"] = lid;
        if (customerId is int cid) query.Filters["customerId"] = cid;
        if (myDay is true) query.Filters["myDay"] = true;
        if (overdue is true) query.Filters["overdue"] = true;
        return Ok(await _crm.GetActivitiesAsync(query, ct));
    }

    [HttpPost("activities")]
    [Authorize(Policy = Permissions.CrmActivities)]
    public async Task<IActionResult> CreateActivity([FromBody] CrmActivityCreateDto dto, CancellationToken ct)
        => FromResult(await _crm.CreateActivityAsync(dto, ct));

    [HttpPost("activities/{id:int}/complete")]
    [Authorize(Policy = Permissions.CrmActivities)]
    public async Task<IActionResult> CompleteActivity(int id, [FromBody] CompleteActivityRequest? body, CancellationToken ct)
        => FromResult(await _crm.CompleteActivityAsync(id, body?.CreateNext ?? false, body?.NextDueDays ?? 7, ct));

    [HttpDelete("activities/{id:int}")]
    [Authorize(Policy = Permissions.CrmActivities)]
    public async Task<IActionResult> DeleteActivity(int id, CancellationToken ct)
        => FromResult(await _crm.DeleteActivityAsync(id, ct));

    [HttpGet("opportunities")]
    [Authorize(Policy = Permissions.CrmView)]
    public async Task<IActionResult> GetOpportunities(
        [FromQuery] QuerySpec query,
        [FromQuery] string? stage,
        [FromQuery] int? leadId,
        [FromQuery] int? customerId,
        [FromQuery] decimal? minValue,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(stage)) query.Filters["stage"] = stage;
        if (leadId is int lid) query.Filters["leadId"] = lid;
        if (customerId is int cid) query.Filters["customerId"] = cid;
        if (minValue is decimal mv) query.Filters["minValue"] = mv;
        return Ok(await _crm.GetOpportunitiesAsync(query, ct));
    }

    [HttpGet("opportunities/{id:int}")]
    [Authorize(Policy = Permissions.CrmView)]
    public async Task<IActionResult> GetOpportunity(int id, CancellationToken ct)
        => NotFoundOrOk(await _crm.GetOpportunityByIdAsync(id, ct));

    [HttpPost("opportunities")]
    [Authorize(Policy = Permissions.CrmManage)]
    public async Task<IActionResult> CreateOpportunity([FromBody] OpportunityCreateDto dto, CancellationToken ct)
        => FromResult(await _crm.CreateOpportunityAsync(dto, ct));

    [HttpPut("opportunities/{id:int}")]
    [Authorize(Policy = Permissions.CrmManage)]
    public async Task<IActionResult> UpdateOpportunity(int id, [FromBody] OpportunityUpdateDto dto, CancellationToken ct)
        => FromResult(await _crm.UpdateOpportunityAsync(id, dto, ct));

    [HttpPost("opportunities/{id:int}/stage")]
    [Authorize(Policy = Permissions.CrmManage)]
    public async Task<IActionResult> ChangeStage(int id, [FromBody] OpportunityStageChangeDto dto, CancellationToken ct)
        => FromResult(await _crm.ChangeOpportunityStageAsync(id, dto, ct));

    [HttpPost("opportunities/{id:int}/link-quotation")]
    [Authorize(Policy = Permissions.CrmManage)]
    public async Task<IActionResult> LinkQuotation(int id, [FromBody] LinkQuoteRequest body, CancellationToken ct)
        => FromResult(await _crm.LinkQuotationAsync(id, body.QuotationId, ct));

    [HttpGet("opportunities/{id:int}/history")]
    [Authorize(Policy = Permissions.CrmView)]
    public async Task<IActionResult> StageHistory(int id, CancellationToken ct)
        => Ok(await _crm.GetStageHistoryAsync(id, ct));

    [HttpGet("pipeline/dashboard")]
    [Authorize(Policy = Permissions.CrmView)]
    public async Task<IActionResult> PipelineDashboard(CancellationToken ct)
        => Ok(await _crm.GetPipelineDashboardAsync(ct));

    /// <summary>Smoke: verifies CRM module + company-scoped lead list responds.</summary>
    [HttpGet("smoke")]
    [Authorize(Policy = Permissions.CrmView)]
    public async Task<IActionResult> Smoke(CancellationToken ct)
    {
        var leads = await _crm.GetLeadsAsync(new QuerySpec { Page = 1, PageSize = 1 }, ct);
        var dash = await _crm.GetPipelineDashboardAsync(ct);
        return Ok(new { ok = true, leadCount = leads.TotalCount, openDeals = dash.OpenCount });
    }

    [HttpGet("customers/{customerId:int}/360")]
    [Authorize(Policy = Permissions.CrmView)]
    public async Task<IActionResult> Customer360(int customerId, CancellationToken ct)
        => NotFoundOrOk(await _crm.GetCustomer360Async(customerId, ct));

    [HttpGet("assignment-rules")]
    [Authorize(Policy = Permissions.CrmManage)]
    public async Task<IActionResult> AssignmentRules(CancellationToken ct)
        => Ok(await _crm.GetAssignmentRulesAsync(ct));

    [HttpPost("assignment-rules")]
    [Authorize(Policy = Permissions.CrmManage)]
    public async Task<IActionResult> UpsertRule([FromBody] CrmAssignmentRuleDto dto, CancellationToken ct)
        => FromResult(await _crm.UpsertAssignmentRuleAsync(dto, ct));

    [HttpGet("email-templates")]
    [Authorize(Policy = Permissions.CrmView)]
    public async Task<IActionResult> EmailTemplates(CancellationToken ct)
        => Ok(await _crm.GetEmailTemplatesAsync(ct));

    [HttpPost("email-templates")]
    [Authorize(Policy = Permissions.CrmManage)]
    public async Task<IActionResult> UpsertTemplate([FromBody] CrmEmailTemplateDto dto, CancellationToken ct)
        => FromResult(await _crm.UpsertEmailTemplateAsync(dto, ct));

    public record ConvertCustomerRequest(int? ExistingCustomerId);
    public record CompleteActivityRequest(bool CreateNext, int NextDueDays = 7);
    public record LinkQuoteRequest(int QuotationId);
}
