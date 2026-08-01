using CarAutoParts.Application.DTOs.Reports;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Services;
using CarAutoParts.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CarAutoParts.Infrastructure.Services;

/// <summary>QuestPDF exports with the same branch ACL / warehouse scoping as Excel paths.</summary>
public class PdfReportService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentCompanyContext _company;

    static PdfReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public PdfReportService(ApplicationDbContext db, ICurrentCompanyContext company)
    {
        _db = db;
        _company = company;
    }

    /// <summary>Returns null when the requested branch is not allowed.</summary>
    public async Task<byte[]?> GenerateInventoryReportAsync(int? branchId = null, CancellationToken ct = default)
    {
        if (ReportBranchScope.IsDenied(_company, branchId))
            return null;

        var whIds = ReportBranchScope.AllowedWarehouseIds(_db.Warehouses.AsNoTracking(), _company, branchId);
        var q = _db.InventoryItems
            .AsNoTracking()
            .Include(i => i.Product)
            .Include(i => i.Warehouse)
            .Where(i => !i.IsDeleted);
        if (whIds is not null)
            q = q.Where(i => whIds.Contains(i.WarehouseId));

        var items = await q
            .OrderBy(i => i.Product.Name)
            .Select(i => new
            {
                i.Product.Sku,
                i.Product.Name,
                Warehouse = i.Warehouse.Name,
                i.QuantityOnHand,
                i.AverageCost,
                Value = i.QuantityOnHand * i.AverageCost
            })
            .ToListAsync(ct);

        return BuildTableReport(
            "Inventory Report",
            ["SKU", "Product", "Warehouse", "Qty", "Avg Cost", "Value"],
            items.Select(i => new[]
            {
                i.Sku,
                i.Name,
                i.Warehouse,
                i.QuantityOnHand.ToString("N2"),
                i.AverageCost.ToString("N2"),
                i.Value.ToString("N2")
            }));
    }

    /// <summary>Returns null when the requested branch is not allowed.</summary>
    public async Task<byte[]?> GenerateSalesReportAsync(
        DateTime from, DateTime to, int? branchId = null, CancellationToken ct = default)
    {
        if (ReportBranchScope.IsDenied(_company, branchId))
            return null;

        var whIds = ReportBranchScope.AllowedWarehouseIds(_db.Warehouses.AsNoTracking(), _company, branchId);
        var q = _db.SalesInvoices
            .AsNoTracking()
            .Where(i => !i.IsDeleted && !i.IsVoided && i.InvoiceDate >= from && i.InvoiceDate <= to);
        if (whIds is not null)
            q = q.Where(i => i.WarehouseId != null && whIds.Contains(i.WarehouseId.Value));

        var invoices = await q
            .OrderByDescending(i => i.InvoiceDate)
            .Select(i => new
            {
                i.InvoiceNumber,
                i.InvoiceDate,
                i.GrandTotal,
                i.PaymentStatus
            })
            .ToListAsync(ct);

        return BuildTableReport(
            $"Sales Report ({from:dd-MMM-yyyy} – {to:dd-MMM-yyyy})",
            ["Invoice #", "Date", "Total", "Status"],
            invoices.Select(i => new[]
            {
                i.InvoiceNumber,
                i.InvoiceDate.ToString("dd-MMM-yyyy"),
                i.GrandTotal.ToString("N2"),
                i.PaymentStatus.ToString()
            }));
    }

    /// <summary>Returns null when the requested branch is not allowed.</summary>
    public async Task<byte[]?> GeneratePurchaseReportAsync(
        DateTime from, DateTime to, int? branchId = null, CancellationToken ct = default)
    {
        if (ReportBranchScope.IsDenied(_company, branchId))
            return null;

        var whIds = ReportBranchScope.AllowedWarehouseIds(_db.Warehouses.AsNoTracking(), _company, branchId);
        var q = _db.PurchaseOrders
            .AsNoTracking()
            .Include(o => o.Supplier)
            .Where(o => !o.IsDeleted && o.OrderDate >= from && o.OrderDate <= to);
        if (whIds is not null)
            q = q.Where(o => o.WarehouseId == null || whIds.Contains(o.WarehouseId.Value));

        var orders = await q
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new
            {
                o.OrderNumber,
                Supplier = o.Supplier.Name,
                o.OrderDate,
                o.GrandTotal,
                o.Status
            })
            .ToListAsync(ct);

        return BuildTableReport(
            $"Purchase Report ({from:dd-MMM-yyyy} – {to:dd-MMM-yyyy})",
            ["PO #", "Supplier", "Date", "Total", "Status"],
            orders.Select(o => new[]
            {
                o.OrderNumber,
                o.Supplier,
                o.OrderDate.ToString("dd-MMM-yyyy"),
                o.GrandTotal.ToString("N2"),
                o.Status.ToString()
            }));
    }

    public Task<byte[]> GenerateDailySalesReportAsync(DailySalesSummaryDto summary, CancellationToken ct = default)
    {
        var rows = new List<string[]>
        {
            new[] { "Invoices", summary.InvoiceCount.ToString() },
            new[] { "Returns", summary.ReturnCount.ToString() },
            new[] { "Sales", summary.SalesTotal.ToString("N2") },
            new[] { "Tax", summary.TaxAmount.ToString("N2") },
            new[] { "Returns total", summary.ReturnsTotal.ToString("N2") },
            new[] { "Net sales", summary.NetSales.ToString("N2") }
        };
        rows.AddRange(summary.Tenders.Select(t => new[] { $"Tender: {t.Method}", t.Amount.ToString("N2") }));
        rows.AddRange(summary.Days.Select(d => new[]
        {
            d.Date.ToString("dd-MMM-yyyy"),
            $"Inv {d.InvoiceCount} · Net {d.NetSales:N2}"
        }));

        var bytes = BuildTableReport(
            $"Daily Sales ({summary.From:dd-MMM-yyyy} – {summary.To:dd-MMM-yyyy})",
            new[] { "Metric / Day", "Value" },
            rows);
        return Task.FromResult(bytes);
    }

    private static byte[] BuildTableReport(string title, string[] headers, IEnumerable<string[]> rows)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.Header().Text(title).Bold().FontSize(16);
                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        for (var i = 0; i < headers.Length; i++)
                            cols.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        foreach (var h in headers)
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(h).Bold();
                    });

                    foreach (var row in rows)
                    {
                        foreach (var cell in row)
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(cell);
                    }
                });
                page.Footer().AlignRight().Text(t =>
                {
                    t.Span("Generated: ");
                    t.Span(DateTime.Now.ToString("dd-MMM-yyyy HH:mm"));
                });
            });
        }).GeneratePdf();
    }
}
