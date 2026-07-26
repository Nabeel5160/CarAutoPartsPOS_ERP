using CarAutoParts.Application.Common;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/audit-logs")]
public class AuditController : ApiControllerBase
{
    private readonly IAuditService _audit;

    public AuditController(IAuditService audit) => _audit = audit;

    [HttpGet]
    [Authorize(Policy = Permissions.AuditView)]
    public async Task<IActionResult> GetAll([FromQuery] QuerySpec query, CancellationToken ct)
        => Ok(await _audit.GetAuditLogsAsync(query, ct));
}
