using CarAutoParts.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CarAutoParts.Infrastructure.Services;

public class PdfReportService
{
    private readonly ApplicationDbContext _db;

    static PdfReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public PdfReportService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<byte[]> GenerateInventoryReportAsync(CancellationToken ct = default)
    {
        var items = await _db.InventoryItems
            .AsNoTracking()
            .Include(i => i.Product)
            .Include(i => i.Warehouse)
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

    public async Task<byte[]> GenerateSalesReportAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var invoices = await _db.SalesInvoices
            .AsNoTracking()
            .Where(i => i.InvoiceDate >= from && i.InvoiceDate <= to)
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

    public async Task<byte[]> GeneratePurchaseReportAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var orders = await _db.PurchaseOrders
            .AsNoTracking()
            .Include(o => o.Supplier)
            .Where(o => o.OrderDate >= from && o.OrderDate <= to)
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
