using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/reports")]
public class ReportsController : ApiControllerBase
{
    private readonly IReportService _reports;
    private readonly IPosFloorService _floor;
    private readonly PdfReportService _pdf;

    public ReportsController(IReportService reports, IPosFloorService floor, PdfReportService pdf)
    {
        _reports = reports;
        _floor = floor;
        _pdf = pdf;
    }

    [HttpGet("inventory")]
    [Authorize(Policy = Permissions.ReportsExport)]
    public async Task<IActionResult> Inventory(
        [FromQuery] string format = "xlsx",
        [FromQuery] int? branchId = null,
        CancellationToken ct = default)
    {
        if (IsPdf(format))
        {
            var pdf = await _pdf.GenerateInventoryReportAsync(branchId, ct);
            if (pdf is null)
                return BadRequest("Branch is not allowed for this user.");
            return File(pdf, "application/pdf", "inventory-report.pdf");
        }
        var bytes = await _reports.ExportInventoryReportAsync(format, branchId, ct);
        return File(bytes, ContentTypeFor(format), $"inventory-report.{ExtensionFor(format)}");
    }

    [HttpGet("sales")]
    [Authorize(Policy = Permissions.ReportsExport)]
    public async Task<IActionResult> Sales(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string period = "daily",
        [FromQuery] string format = "xlsx",
        [FromQuery] int? branchId = null,
        CancellationToken ct = default)
    {
        if (IsPdf(format))
        {
            var pdf = await _pdf.GenerateSalesReportAsync(from, to, branchId, ct);
            if (pdf is null)
                return BadRequest("Branch is not allowed for this user.");
            return File(pdf, "application/pdf", "sales-report.pdf");
        }
        var bytes = await _reports.ExportSalesReportAsync(from, to, period, format, branchId, ct);
        return File(bytes, ContentTypeFor(format), $"sales-report.{ExtensionFor(format)}");
    }

    [HttpGet("purchases")]
    [Authorize(Policy = Permissions.ReportsExport)]
    public async Task<IActionResult> Purchases(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string format = "xlsx",
        [FromQuery] int? branchId = null,
        CancellationToken ct = default)
    {
        if (IsPdf(format))
        {
            var pdf = await _pdf.GeneratePurchaseReportAsync(from, to, branchId, ct);
            if (pdf is null)
                return BadRequest("Branch is not allowed for this user.");
            return File(pdf, "application/pdf", "purchase-report.pdf");
        }
        var bytes = await _reports.ExportPurchaseReportAsync(from, to, format, branchId, ct);
        return File(bytes, ContentTypeFor(format), $"purchase-report.{ExtensionFor(format)}");
    }

    [HttpGet("profit")]
    [Authorize(Policy = Permissions.ReportsExport)]
    public async Task<IActionResult> Profit(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string format = "xlsx",
        [FromQuery] int? branchId = null,
        CancellationToken ct = default)
    {
        var bytes = await _reports.ExportProfitReportAsync(from, to, format, branchId, ct);
        return File(bytes, ContentTypeFor(format), $"profit-report.{ExtensionFor(format)}");
    }

    [HttpGet("daily-sales")]
    [Authorize(Policy = Permissions.ReportsExport)]
    public async Task<IActionResult> DailySales(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int? branchId = null,
        [FromQuery] string format = "json",
        CancellationToken ct = default)
    {
        if (IsXlsx(format))
        {
            var bytes = await _reports.ExportDailySalesSummaryAsync(from, to, branchId, ct);
            return File(bytes, XlsxType, "daily-sales.xlsx");
        }
        if (IsPdf(format))
        {
            var summary = await _reports.GetDailySalesSummaryAsync(from, to, branchId, ct);
            if (!summary.Succeeded || summary.Data is null)
                return BadRequest(summary.Error);
            var pdf = await _pdf.GenerateDailySalesReportAsync(summary.Data, ct);
            return File(pdf, "application/pdf", "daily-sales.pdf");
        }
        return FromResult(await _reports.GetDailySalesSummaryAsync(from, to, branchId, ct));
    }

    [HttpGet("sales-returns")]
    [Authorize(Policy = Permissions.ReportsExport)]
    public async Task<IActionResult> SalesReturns(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int? branchId = null,
        [FromQuery] string format = "json",
        CancellationToken ct = default)
    {
        if (IsXlsx(format))
            return File(await _reports.ExportSalesReturnsReportAsync(from, to, branchId, ct), XlsxType, "sales-returns.xlsx");
        return FromResult(await _reports.GetSalesReturnsReportAsync(from, to, branchId, ct));
    }

    [HttpGet("z-shifts")]
    [Authorize(Policy = Permissions.ReportsExport)]
    public async Task<IActionResult> ZShifts(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int? tillId = null,
        [FromQuery] int? branchId = null,
        [FromQuery] string format = "json",
        CancellationToken ct = default)
    {
        var list = await _floor.ListClosedShiftsAsync(from, to, tillId, branchId, ct);
        if (IsXlsx(format))
            return File(_reports.ExportClosedShiftsArchive(list), XlsxType, "z-shifts.xlsx");
        return Ok(list);
    }

    [HttpGet("sales-dim")]
    [Authorize(Policy = Permissions.ReportsExport)]
    public async Task<IActionResult> SalesDim(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string dimension = "product",
        [FromQuery] int? branchId = null,
        [FromQuery] string format = "json",
        CancellationToken ct = default)
    {
        if (IsXlsx(format))
            return File(await _reports.ExportSalesDimReportAsync(from, to, dimension, branchId, ct), XlsxType, "sales-dim.xlsx");
        return FromResult(await _reports.GetSalesDimReportAsync(from, to, dimension, branchId, ct));
    }

    [HttpGet("sales-staff")]
    [Authorize(Policy = Permissions.ReportsExport)]
    public async Task<IActionResult> SalesStaff(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string dimension = "cashier",
        [FromQuery] int? branchId = null,
        [FromQuery] string format = "json",
        CancellationToken ct = default)
    {
        if (IsXlsx(format))
            return File(await _reports.ExportSalesStaffReportAsync(from, to, dimension, branchId, ct), XlsxType, "sales-staff.xlsx");
        return FromResult(await _reports.GetSalesStaffReportAsync(from, to, dimension, branchId, ct));
    }

    [HttpGet("profit-dim")]
    [Authorize(Policy = Permissions.ReportsExport)]
    public async Task<IActionResult> ProfitDim(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string dimension = "category",
        [FromQuery] int? branchId = null,
        [FromQuery] string format = "json",
        CancellationToken ct = default)
    {
        if (IsXlsx(format))
            return File(await _reports.ExportProfitDimReportAsync(from, to, dimension, branchId, ct), XlsxType, "profit-dim.xlsx");
        return FromResult(await _reports.GetProfitDimReportAsync(from, to, dimension, branchId, ct));
    }

    [HttpGet("analytics-export")]
    [Authorize(Policy = Permissions.ReportsExport)]
    public async Task<IActionResult> AnalyticsExport(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int? branchId = null,
        [FromQuery] int deadStockDays = 90,
        CancellationToken ct = default)
    {
        var bytes = await _reports.ExportAnalyticsAsync(from, to, branchId, deadStockDays, ct);
        return File(bytes, XlsxType, "analytics.xlsx");
    }

    [HttpGet("movements")]
    [Authorize(Policy = Permissions.ReportsExport)]
    public async Task<IActionResult> Movements(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int? warehouseId = null,
        [FromQuery] string? movementType = null,
        [FromQuery] string format = "json",
        CancellationToken ct = default)
    {
        if (IsXlsx(format))
            return File(await _reports.ExportStockMovementsReportAsync(from, to, warehouseId, movementType, ct), XlsxType, "movements.xlsx");
        return FromResult(await _reports.GetStockMovementsReportAsync(from, to, warehouseId, movementType, ct));
    }

    [HttpGet("purchasing-pipeline")]
    [Authorize(Policy = Permissions.ReportsExport)]
    public async Task<IActionResult> PurchasingPipeline(
        [FromQuery] int? branchId = null,
        [FromQuery] string format = "json",
        CancellationToken ct = default)
    {
        if (IsXlsx(format))
            return File(await _reports.ExportPurchasingPipelineAsync(branchId, ct), XlsxType, "purchasing-pipeline.xlsx");
        return FromResult(await _reports.GetPurchasingPipelineAsync(branchId, ct));
    }

    [HttpGet("aging")]
    [Authorize(Policy = Permissions.ReportsExport)]
    public async Task<IActionResult> Aging(
        [FromQuery] string kind = "customers",
        [FromQuery] DateTime? asOf = null,
        CancellationToken ct = default)
    {
        var bytes = await _reports.ExportAgingReportAsync(kind, asOf, ct);
        return File(bytes, XlsxType, $"aging-{kind}.xlsx");
    }

    [HttpGet("tax")]
    [Authorize(Policy = Permissions.ReportsExport)]
    public async Task<IActionResult> Tax(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int? branchId = null,
        [FromQuery] string format = "json",
        CancellationToken ct = default)
    {
        if (IsXlsx(format))
            return File(await _reports.ExportTaxPeriodSummaryAsync(from, to, branchId, ct), XlsxType, "tax-summary.xlsx");
        return FromResult(await _reports.GetTaxPeriodSummaryAsync(from, to, branchId, ct));
    }

    [HttpGet("fbr")]
    [Authorize(Policy = Permissions.ReportsExport)]
    public async Task<IActionResult> Fbr(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string format = "json",
        CancellationToken ct = default)
    {
        if (IsXlsx(format))
            return File(await _reports.ExportFbrRegisterAsync(from, to, ct), XlsxType, "fbr-register.xlsx");
        return FromResult(await _reports.GetFbrRegisterAsync(from, to, ct));
    }

    [HttpGet("stock-aging")]
    [Authorize(Policy = Permissions.ReportsExport)]
    public async Task<IActionResult> StockAging(
        [FromQuery] DateTime? asOf = null,
        [FromQuery] int? branchId = null,
        [FromQuery] string format = "json",
        CancellationToken ct = default)
    {
        if (IsXlsx(format))
            return File(await _reports.ExportStockAgingAsync(asOf, branchId, ct), XlsxType, "stock-aging.xlsx");
        return FromResult(await _reports.GetStockAgingAsync(asOf, branchId, ct));
    }

    [HttpGet("sku-margin")]
    [Authorize(Policy = Permissions.ReportsExport)]
    public async Task<IActionResult> SkuMargin(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int? branchId = null,
        [FromQuery] string format = "json",
        CancellationToken ct = default)
    {
        if (IsXlsx(format))
            return File(await _reports.ExportSkuMarginAsync(from, to, branchId, ct), XlsxType, "sku-margin.xlsx");
        return FromResult(await _reports.GetSkuMarginAsync(from, to, branchId, ct));
    }

    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static bool IsPdf(string format) =>
        format.Equals("pdf", StringComparison.OrdinalIgnoreCase);

    private static bool IsXlsx(string format) =>
        format.Equals("xlsx", StringComparison.OrdinalIgnoreCase) ||
        format.Equals("excel", StringComparison.OrdinalIgnoreCase);

    private static string ContentTypeFor(string format) =>
        IsPdf(format) ? "application/pdf" : XlsxType;

    private static string ExtensionFor(string format) =>
        IsPdf(format) ? "pdf" : "xlsx";
}
