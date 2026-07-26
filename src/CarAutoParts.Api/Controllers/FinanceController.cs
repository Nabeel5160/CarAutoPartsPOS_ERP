using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Finance;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/finance")]
[Route("api/v1/finance")]
public class FinanceController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public FinanceController(IMediator mediator) => _mediator = mediator;

    [HttpGet("companies")]
    [Authorize(Policy = Permissions.PlatformView)]
    public async Task<ActionResult<IReadOnlyList<CompanyDto>>> GetCompanies(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetCompaniesQuery(), ct));

    [HttpGet("companies/{companyId:int}/branches")]
    [Authorize(Policy = Permissions.PlatformView)]
    public async Task<ActionResult<IReadOnlyList<BranchDto>>> GetBranches(int companyId, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetBranchesQuery(companyId), ct));

    [HttpGet("coa")]
    [Authorize(Policy = Permissions.FinanceView)]
    public async Task<ActionResult<IReadOnlyList<GlAccountDto>>> GetCoa(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetChartOfAccountsQuery(), ct));

    [HttpPost("coa")]
    [Authorize(Policy = Permissions.FinanceManage)]
    public async Task<ActionResult<GlAccountDto>> CreateAccount([FromBody] CreateGlAccountCommand command, CancellationToken ct) =>
        FromResult(await _mediator.Send(command, ct));

    [HttpGet("periods")]
    [Authorize(Policy = Permissions.FinanceView)]
    public async Task<ActionResult<IReadOnlyList<AccountingPeriodDto>>> GetPeriods(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetOpenPeriodsQuery(), ct));

    [HttpPost("periods/{id:int}/close")]
    [Authorize(Policy = Permissions.FinancePost)]
    public async Task<IActionResult> ClosePeriod(int id, CancellationToken ct) =>
        FromResult(await _mediator.Send(new ClosePeriodCommand(id), ct));

    [HttpPost("periods/{id:int}/reopen")]
    [Authorize(Policy = Permissions.FinancePost)]
    public async Task<IActionResult> ReopenPeriod(int id, CancellationToken ct) =>
        FromResult(await _mediator.Send(new ReopenPeriodCommand(id), ct));

    [HttpGet("journals")]
    [Authorize(Policy = Permissions.FinanceView)]
    public async Task<IActionResult> GetJournals([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        Ok(await _mediator.Send(new GetJournalsQuery(page, pageSize), ct));

    [HttpPost("journals")]
    [Authorize(Policy = Permissions.FinanceManage)]
    public async Task<IActionResult> CreateJournal([FromBody] CreateJournalCommand command, CancellationToken ct) =>
        FromResult(await _mediator.Send(command, ct));

    [HttpPost("journals/{id:int}/post")]
    [Authorize(Policy = Permissions.FinancePost)]
    public async Task<IActionResult> PostJournal(int id, CancellationToken ct) =>
        FromResult(await _mediator.Send(new PostJournalCommand(id), ct));

    [HttpGet("number-sequences/next")]
    [Authorize(Policy = Permissions.FinanceView)]
    public async Task<IActionResult> NextNumber([FromQuery] string documentType, CancellationToken ct) =>
        FromResult(await _mediator.Send(new GetNextDocumentNumberQuery(documentType), ct));
}
