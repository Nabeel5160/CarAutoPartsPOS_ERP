using CarAutoParts.Application.DTOs.Analytics;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>Inventory and sales analytics including ABC, dead/fast stock, and margin.</summary>
public class AnalyticsService : IAnalyticsService
{
    private readonly IRepository<SalesInvoiceLine> _invoiceLines;
    private readonly IRepository<InventoryItem> _inventory;
    private readonly IRepository<Product> _products;
    private readonly IRepository<Warehouse> _warehouses;
    private readonly ICurrentCompanyContext _company;

    public AnalyticsService(
        IRepository<SalesInvoiceLine> invoiceLines,
        IRepository<InventoryItem> inventory,
        IRepository<Product> products,
        IRepository<Warehouse> warehouses,
        ICurrentCompanyContext company)
    {
        _invoiceLines = invoiceLines;
        _inventory = inventory;
        _products = products;
        _warehouses = warehouses;
        _company = company;
    }

    /// <inheritdoc />
    public async Task<AnalyticsDto> GetAnalyticsAsync(
        DateTime? from = null,
        DateTime? to = null,
        int? branchId = null,
        int deadStockDays = 90,
        CancellationToken ct = default)
    {
        if (deadStockDays < 1) deadStockDays = 90;
        var periodEnd = (to ?? DateTime.UtcNow).Date.AddDays(1).AddTicks(-1);
        var periodStart = from?.Date ?? new DateTime(periodEnd.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var effectiveBranch = branchId ?? _company.BranchId;
        var warehouseIds = await ResolveWarehouseIdsAsync(effectiveBranch, ct);

        var saleLines = _invoiceLines.Query()
            .Where(l => !l.IsDeleted && !l.SalesInvoice.IsDeleted && !l.SalesInvoice.IsVoided
                && l.SalesInvoice.InvoiceDate >= periodStart
                && l.SalesInvoice.InvoiceDate <= periodEnd);

        if (warehouseIds is not null)
            saleLines = saleLines.Where(l =>
                l.SalesInvoice.WarehouseId == null || warehouseIds.Contains(l.SalesInvoice.WarehouseId.Value));

        var topSellingRows = await saleLines
            .GroupBy(l => new { l.ProductId, l.ProductName, l.Sku })
            .Select(g => new
            {
                g.Key.ProductId,
                ProductName = g.Key.ProductName,
                g.Key.Sku,
                QuantitySold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.LineTotal),
                Cost = g.Sum(x => x.Quantity * (x.UnitCost > 0
                    ? x.UnitCost
                    : (x.Product.CostPrice > 0 ? x.Product.CostPrice : x.Product.PurchasePrice)))
            })
            .OrderByDescending(x => x.Revenue)
            .Take(20)
            .ToListAsync(ct);

        var topSelling = topSellingRows
            .Select(x => new TopProductAnalyticsDto(
                x.ProductId, x.ProductName, x.Sku, x.QuantitySold, x.Revenue, x.Revenue - x.Cost))
            .ToList();

        var fastMovers = topSellingRows
            .OrderByDescending(x => x.QuantitySold)
            .Take(20)
            .Select(x => new FastMoverItemDto(
                x.ProductId, x.ProductName, x.Sku, x.QuantitySold, x.Revenue, x.Revenue - x.Cost))
            .ToList();

        var lastSaleByProduct = await _invoiceLines.Query()
            .Where(l => !l.IsDeleted && !l.SalesInvoice.IsDeleted && !l.SalesInvoice.IsVoided)
            .GroupBy(l => l.ProductId)
            .Select(g => new { ProductId = g.Key, LastSale = g.Max(x => x.SalesInvoice.InvoiceDate) })
            .ToDictionaryAsync(x => x.ProductId, x => x.LastSale, ct);

        var invQuery = _inventory.Query().Where(i => !i.IsDeleted);
        if (warehouseIds is not null)
            invQuery = invQuery.Where(i => warehouseIds.Contains(i.WarehouseId));

        var stockByProduct = await invQuery
            .GroupBy(i => i.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Qty = g.Sum(i => i.QuantityOnHand),
                Value = g.Sum(i => i.QuantityOnHand * i.AverageCost)
            })
            .ToListAsync(ct);

        var productMeta = await _products.Query()
            .Where(p => !p.IsDeleted && p.IsActive)
            .Select(p => new { p.Id, p.Name, p.Sku })
            .ToDictionaryAsync(p => p.Id, p => p, ct);

        var slowProducts = stockByProduct
            .Where(s => s.Qty > 0 && productMeta.ContainsKey(s.ProductId))
            .Select(s =>
            {
                var p = productMeta[s.ProductId];
                var days = lastSaleByProduct.TryGetValue(s.ProductId, out var last)
                    ? (int)(DateTime.UtcNow - last).TotalDays
                    : 9999;
                return new SlowMovingProductDto(s.ProductId, p.Name, p.Sku, s.Qty, days);
            })
            .Where(p => p.DaysSinceLastSale >= 90)
            .OrderByDescending(p => p.DaysSinceLastSale)
            .Take(20)
            .ToList();

        var deadStock = stockByProduct
            .Where(s => s.Qty > 0 && productMeta.ContainsKey(s.ProductId))
            .Select(s =>
            {
                var p = productMeta[s.ProductId];
                var days = lastSaleByProduct.TryGetValue(s.ProductId, out var last)
                    ? (int)(DateTime.UtcNow - last).TotalDays
                    : 9999;
                return new DeadStockItemDto(s.ProductId, p.Name, p.Sku, s.Qty, s.Value, days);
            })
            .Where(p => p.DaysSinceLastSale >= deadStockDays)
            .OrderByDescending(p => p.StockValue)
            .Take(50)
            .ToList();

        var usageValues = await saleLines
            .GroupBy(l => new { l.ProductId, l.ProductName, l.Sku })
            .Select(g => new
            {
                g.Key.ProductId,
                Name = g.Key.ProductName,
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
                item.ProductId, item.Name, item.Sku, item.AnnualUsageValue, Math.Round(cumulative, 2), cls);
        }).ToList();

        var inventoryValue = stockByProduct.Sum(s => s.Value);

        var marginAgg = await saleLines
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Revenue = g.Sum(x => x.LineTotal),
                Cost = g.Sum(x => x.Quantity * (x.UnitCost > 0
                    ? x.UnitCost
                    : (x.Product.CostPrice > 0 ? x.Product.CostPrice : x.Product.PurchasePrice)))
            })
            .FirstOrDefaultAsync(ct);

        var revenue = marginAgg?.Revenue ?? 0;
        var cogs = marginAgg?.Cost ?? 0;
        var gm = revenue - cogs;
        var gmPct = revenue > 0 ? gm / revenue * 100m : 0;
        var turnover = inventoryValue > 0 ? cogs / inventoryValue : 0;

        return new AnalyticsDto(
            topSelling, slowProducts, abc, inventoryValue, turnover,
            deadStock, fastMovers, gm, gmPct, effectiveBranch);
    }

    private async Task<List<int>?> ResolveWarehouseIdsAsync(int? branchId, CancellationToken ct)
    {
        if (!branchId.HasValue) return null;
        return await _warehouses.Query()
            .Where(w => !w.IsDeleted && w.BranchId == branchId)
            .Select(w => w.Id)
            .ToListAsync(ct);
    }
}
