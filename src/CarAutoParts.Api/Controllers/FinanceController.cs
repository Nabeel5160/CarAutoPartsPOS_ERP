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
    private readonly IPhase4FinanceService _phase4;

    public FinanceController(IMediator mediator, IPhase4FinanceService phase4)
    {
        _mediator = mediator;
        _phase4 = phase4;
    }

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

    [HttpGet("periods/{id:int}/close-checklist")]
    [Authorize(Policy = Permissions.FinanceView)]
    public async Task<ActionResult<PeriodCloseChecklistDto>> GetCloseChecklist(int id, CancellationToken ct) =>
        Ok(await _phase4.GetPeriodCloseChecklistAsync(id, ct));

    [HttpPost("periods/{id:int}/close")]
    [Authorize(Policy = Permissions.FinancePost)]
    public async Task<IActionResult> ClosePeriod(int id, [FromQuery] bool force = false, CancellationToken ct = default) =>
        FromResult(await _phase4.ClosePeriodWithChecklistAsync(id, force, ct));

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

    [HttpGet("opening-balances")]
    [Authorize(Policy = Permissions.FinanceView)]
    public async Task<IActionResult> GetOpeningBalances(CancellationToken ct) =>
        Ok(await _phase4.GetOpeningBalanceBatchesAsync(ct));

    [HttpPost("opening-balances")]
    [Authorize(Policy = Permissions.FinanceManage)]
    public async Task<IActionResult> PostOpeningBalances([FromBody] PostOpeningBalancesRequest request, CancellationToken ct) =>
        FromResult(await _phase4.PostOpeningBalancesAsync(request, ct));

    [HttpGet("bank-statements")]
    [Authorize(Policy = Permissions.FinanceView)]
    public async Task<IActionResult> GetBankStatements(CancellationToken ct) =>
        Ok(await _phase4.GetBankStatementsAsync(ct));

    [HttpPost("bank-statements")]
    [Authorize(Policy = Permissions.FinanceManage)]
    public async Task<IActionResult> CreateBankStatement([FromBody] CreateBankStatementRequest request, CancellationToken ct) =>
        FromResult(await _phase4.CreateBankStatementAsync(request, ct));

    [HttpPost("bank-statements/{id:int}/lines")]
    [Authorize(Policy = Permissions.FinanceManage)]
    public async Task<IActionResult> AddBankLine(int id, [FromBody] CreateBankStatementLineRequest request, CancellationToken ct) =>
        FromResult(await _phase4.AddBankStatementLineAsync(id, request, ct));

    [HttpPost("bank-statements/lines/{lineId:int}/match")]
    [Authorize(Policy = Permissions.FinancePost)]
    public async Task<IActionResult> MatchBankLine(int lineId, [FromQuery] int journalLineId, CancellationToken ct) =>
        FromResult(await _phase4.MatchBankLineAsync(lineId, journalLineId, ct));

    [HttpPost("bank-statements/lines/{lineId:int}/unclear")]
    [Authorize(Policy = Permissions.FinanceManage)]
    public async Task<IActionResult> UnclearBankLine(int lineId, CancellationToken ct) =>
        FromResult(await _phase4.UnclearBankLineAsync(lineId, ct));

    [HttpGet("bank-statements/uncleared-gl")]
    [Authorize(Policy = Permissions.FinanceView)]
    public async Task<IActionResult> UnclearedGl([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) =>
        Ok(await _phase4.GetUnclearedBankGlLinesAsync(from, to, ct));

    [HttpGet("bank-statements/{id:int}/report")]
    [Authorize(Policy = Permissions.FinanceView)]
    public async Task<IActionResult> BankReconReport(int id, CancellationToken ct) =>
        FromResult(await _phase4.GetBankReconReportAsync(id, ct));
}
