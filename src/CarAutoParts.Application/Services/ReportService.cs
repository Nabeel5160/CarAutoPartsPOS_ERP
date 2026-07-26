using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace CarAutoParts.Application.Services;

/// <summary>Exports operational reports to Excel or CSV.</summary>
public class ReportService : IReportService
{
    private readonly IRepository<InventoryItem> _inventory;
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<PurchaseOrder> _purchaseOrders;

    public ReportService(
        IRepository<InventoryItem> inventory,
        IRepository<SalesInvoice> invoices,
        IRepository<PurchaseOrder> purchaseOrders)
    {
        _inventory = inventory;
        _invoices = invoices;
        _purchaseOrders = purchaseOrders;
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportInventoryReportAsync(string format, CancellationToken ct = default)
    {
        var items = await _inventory.Query()
            .Include(i => i.Product)
            .Include(i => i.Warehouse)
            .Where(i => !i.IsDeleted)
            .OrderBy(i => i.Product.Name)
            .ToListAsync(ct);

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var sb = new StringBuilder();
            sb.AppendLine("Product,SKU,Warehouse,Qty,Value");
            foreach (var i in items)
                sb.AppendLine($"{i.Product.Name},{i.Product.Sku},{i.Warehouse.Name},{i.QuantityOnHand},{i.QuantityOnHand * i.AverageCost}");
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Inventory");
        sheet.Cell(1, 1).Value = "Product";
        sheet.Cell(1, 2).Value = "SKU";
        sheet.Cell(1, 3).Value = "Warehouse";
        sheet.Cell(1, 4).Value = "Quantity";
        sheet.Cell(1, 5).Value = "Value";

        var row = 2;
        foreach (var i in items)
        {
            sheet.Cell(row, 1).Value = i.Product.Name;
            sheet.Cell(row, 2).Value = i.Product.Sku;
            sheet.Cell(row, 3).Value = i.Warehouse.Name;
            sheet.Cell(row, 4).Value = i.QuantityOnHand;
            sheet.Cell(row, 5).Value = i.QuantityOnHand * i.AverageCost;
            row++;
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportSalesReportAsync(DateTime from, DateTime to, string period, string format, CancellationToken ct = default)
    {
        var invoices = await _invoices.Query()
            .Where(i => !i.IsDeleted && i.InvoiceDate >= from && i.InvoiceDate <= to)
            .OrderBy(i => i.InvoiceDate)
            .ToListAsync(ct);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sales");
        sheet.Cell(1, 1).Value = "Invoice";
        sheet.Cell(1, 2).Value = "Date";
        sheet.Cell(1, 3).Value = "SubTotal";
        sheet.Cell(1, 4).Value = "Tax";
        sheet.Cell(1, 5).Value = "Grand Total";

        var row = 2;
        foreach (var i in invoices)
        {
            sheet.Cell(row, 1).Value = i.InvoiceNumber;
            sheet.Cell(row, 2).Value = i.InvoiceDate;
            sheet.Cell(row, 3).Value = i.SubTotal;
            sheet.Cell(row, 4).Value = i.TaxAmount;
            sheet.Cell(row, 5).Value = i.GrandTotal;
            row++;
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportPurchaseReportAsync(DateTime from, DateTime to, string format, CancellationToken ct = default)
    {
        var orders = await _purchaseOrders.Query()
            .Include(o => o.Supplier)
            .Where(o => !o.IsDeleted && o.OrderDate >= from && o.OrderDate <= to)
            .OrderBy(o => o.OrderDate)
            .ToListAsync(ct);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Purchases");
        sheet.Cell(1, 1).Value = "PO Number";
        sheet.Cell(1, 2).Value = "Supplier";
        sheet.Cell(1, 3).Value = "Date";
        sheet.Cell(1, 4).Value = "Status";
        sheet.Cell(1, 5).Value = "Grand Total";

        var row = 2;
        foreach (var o in orders)
        {
            sheet.Cell(row, 1).Value = o.OrderNumber;
            sheet.Cell(row, 2).Value = o.Supplier.Name;
            sheet.Cell(row, 3).Value = o.OrderDate;
            sheet.Cell(row, 4).Value = o.Status.ToString();
            sheet.Cell(row, 5).Value = o.GrandTotal;
            row++;
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportProfitReportAsync(DateTime from, DateTime to, string format, CancellationToken ct = default)
    {
        var lines = await _invoices.Query()
            .Where(i => !i.IsDeleted && i.InvoiceDate >= from && i.InvoiceDate <= to)
            .SelectMany(i => i.Lines)
            .GroupBy(l => l.ProductId)
            .Select(g => new { ProductId = g.Key, Revenue = g.Sum(x => x.LineTotal), Qty = g.Sum(x => x.Quantity) })
            .ToListAsync(ct);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Profit");
        sheet.Cell(1, 1).Value = "ProductId";
        sheet.Cell(1, 2).Value = "Quantity";
        sheet.Cell(1, 3).Value = "Revenue";

        var row = 2;
        foreach (var l in lines)
        {
            sheet.Cell(row, 1).Value = l.ProductId;
            sheet.Cell(row, 2).Value = l.Qty;
            sheet.Cell(row, 3).Value = l.Revenue;
            row++;
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}
