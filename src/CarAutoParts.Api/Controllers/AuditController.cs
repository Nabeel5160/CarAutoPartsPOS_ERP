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
    public async Task<IActionResult> GetAll(
        [FromQuery] QuerySpec query,
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(action))
            query.Filters["Action"] = action;
        if (!string.IsNullOrWhiteSpace(entityType))
            query.Filters["EntityType"] = entityType;
        if (fromDate.HasValue)
            query.Filters["FromDate"] = fromDate.Value;
        if (toDate.HasValue)
            query.Filters["ToDate"] = toDate.Value;
        return Ok(await _audit.GetAuditLogsAsync(query, ct));
    }
}
