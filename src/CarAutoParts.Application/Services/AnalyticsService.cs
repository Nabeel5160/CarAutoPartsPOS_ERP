using CarAutoParts.Application.DTOs.Analytics;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>Inventory and sales analytics including ABC classification.</summary>
public class AnalyticsService : IAnalyticsService
{
    private readonly IRepository<SalesInvoiceLine> _invoiceLines;
    private readonly IRepository<InventoryItem> _inventory;
    private readonly IRepository<Product> _products;

    public AnalyticsService(
        IRepository<SalesInvoiceLine> invoiceLines,
        IRepository<InventoryItem> inventory,
        IRepository<Product> products)
    {
        _invoiceLines = invoiceLines;
        _inventory = inventory;
        _products = products;
    }

    /// <inheritdoc />
    public async Task<AnalyticsDto> GetAnalyticsAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var periodEnd = (to ?? DateTime.UtcNow).Date.AddDays(1).AddTicks(-1);
        var periodStart = from?.Date ?? new DateTime(periodEnd.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var topSellingRows = await _invoiceLines.Query()
            .Where(l => !l.IsDeleted && !l.SalesInvoice.IsDeleted
                && l.SalesInvoice.InvoiceDate >= periodStart
                && l.SalesInvoice.InvoiceDate <= periodEnd)
            .GroupBy(l => new { l.ProductId, l.Product.Name, l.Product.Sku, l.Product.CostPrice })
            .Select(g => new
            {
                g.Key.ProductId,
                ProductName = g.Key.Name,
                g.Key.Sku,
                QuantitySold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.LineTotal),
                Profit = g.Sum(x => x.LineTotal) - g.Sum(x => x.Quantity) * g.Key.CostPrice
            })
            .OrderByDescending(x => x.Revenue)
            .Take(20)
            .ToListAsync(ct);

        var topSelling = topSellingRows
            .Select(x => new TopProductAnalyticsDto(
                x.ProductId,
                x.ProductName,
                x.Sku,
                x.QuantitySold,
                x.Revenue,
                x.Profit))
            .ToList();

        var lastSaleByProduct = await _invoiceLines.Query()
            .Where(l => !l.IsDeleted && !l.SalesInvoice.IsDeleted)
            .GroupBy(l => l.ProductId)
            .Select(g => new { ProductId = g.Key, LastSale = g.Max(x => x.SalesInvoice.InvoiceDate) })
            .ToDictionaryAsync(x => x.ProductId, x => x.LastSale, ct);

        var slowMoving = await _products.Query()
            .Where(p => !p.IsDeleted && p.IsActive)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Sku,
                Qty = p.InventoryItems.Where(i => !i.IsDeleted).Sum(i => i.QuantityOnHand)
            })
            .ToListAsync(ct);

        var slowProducts = slowMoving
            .Select(p =>
            {
                var days = lastSaleByProduct.TryGetValue(p.Id, out var last)
                    ? (int)(DateTime.UtcNow - last).TotalDays
                    : 365;
                return new SlowMovingProductDto(p.Id, p.Name, p.Sku, p.Qty, days);
            })
            .Where(p => p.DaysSinceLastSale >= 90)
            .OrderByDescending(p => p.DaysSinceLastSale)
            .Take(20)
            .ToList();

        var usageValues = await _invoiceLines.Query()
            .Where(l => !l.IsDeleted && !l.SalesInvoice.IsDeleted
                && l.SalesInvoice.InvoiceDate >= periodStart
                && l.SalesInvoice.InvoiceDate <= periodEnd)
            .GroupBy(l => new { l.ProductId, l.Product.Name, l.Product.Sku })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.Name,
                g.Key.Sku,
                AnnualUsageValue = g.Sum(x => x.LineTotal)
            })
            .OrderByDescending(x => x.AnnualUsageValue)
            .ToListAsync(ct);

        var totalUsage = usageValues.Sum(x => x.AnnualUsageValue);
        decimal cumulative = 0;
        var abc = usageValues.Select(item =>
        {
            cumulative += totalUsage > 0 ? item.AnnualUsageValue / totalUsage * 100 : 0;
            var cls = cumulative <= 80 ? 'A' : cumulative <= 95 ? 'B' : 'C';
            return new AbcAnalysisItemDto(
                item.ProductId,
                item.Name,
                item.Sku,
                item.AnnualUsageValue,
                Math.Round(cumulative, 2),
                cls);
        }).ToList();

        var inventoryValue = await _inventory.Query()
            .Where(i => !i.IsDeleted)
            .SumAsync(i => i.QuantityOnHand * i.AverageCost, ct);

        var cogs = await _invoiceLines.Query()
            .Where(l => !l.IsDeleted && !l.SalesInvoice.IsDeleted
                && l.SalesInvoice.InvoiceDate >= periodStart
                && l.SalesInvoice.InvoiceDate <= periodEnd)
            .SumAsync(l => l.Quantity * l.Product.CostPrice, ct);

        var turnover = inventoryValue > 0 ? cogs / inventoryValue : 0;

        return new AnalyticsDto(topSelling, slowProducts, abc, inventoryValue, turnover);
    }
}
