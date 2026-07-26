using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/reports")]
public class ReportsController : ApiControllerBase
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports) => _reports = reports;

    [HttpGet("inventory")]
    [Authorize(Policy = Permissions.ReportsExport)]
    public async Task<IActionResult> Inventory([FromQuery] string format = "xlsx", CancellationToken ct = default)
    {
        var bytes = await _reports.ExportInventoryReportAsync(format, ct);
        return File(bytes, ContentTypeFor(format), $"inventory-report.{ExtensionFor(format)}");
    }

    [HttpGet("sales")]
    [Authorize(Policy = Permissions.ReportsExport)]
    public async Task<IActionResult> Sales(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string period = "daily",
        [FromQuery] string format = "xlsx",
        CancellationToken ct = default)
    {
        var bytes = await _reports.ExportSalesReportAsync(from, to, period, format, ct);
        return File(bytes, ContentTypeFor(format), $"sales-report.{ExtensionFor(format)}");
    }

    [HttpGet("purchases")]
    [Authorize(Policy = Permissions.ReportsExport)]
    public async Task<IActionResult> Purchases(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string format = "xlsx",
        CancellationToken ct = default)
    {
        var bytes = await _reports.ExportPurchaseReportAsync(from, to, format, ct);
        return File(bytes, ContentTypeFor(format), $"purchase-report.{ExtensionFor(format)}");
    }

    [HttpGet("profit")]
    [Authorize(Policy = Permissions.ReportsExport)]
    public async Task<IActionResult> Profit(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string format = "xlsx",
        CancellationToken ct = default)
    {
        var bytes = await _reports.ExportProfitReportAsync(from, to, format, ct);
        return File(bytes, ContentTypeFor(format), $"profit-report.{ExtensionFor(format)}");
    }

    private static string ContentTypeFor(string format) =>
        format.Equals("pdf", StringComparison.OrdinalIgnoreCase)
            ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static string ExtensionFor(string format) =>
        format.Equals("pdf", StringComparison.OrdinalIgnoreCase) ? "pdf" : "xlsx";
}
