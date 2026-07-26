using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Dashboard;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>Aggregates KPI and chart data for the dashboard.</summary>
public class DashboardService : IDashboardService
{
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<SalesInvoiceLine> _invoiceLines;
    private readonly IRepository<PurchaseOrder> _purchaseOrders;
    private readonly IRepository<InventoryItem> _inventory;
    private readonly IRepository<Product> _products;
    private readonly IRepository<InventoryTransfer> _transfers;
    private readonly INotificationService _notifications;

    public DashboardService(
        IRepository<SalesInvoice> invoices,
        IRepository<SalesInvoiceLine> invoiceLines,
        IRepository<PurchaseOrder> purchaseOrders,
        IRepository<InventoryItem> inventory,
        IRepository<Product> products,
        IRepository<InventoryTransfer> transfers,
        INotificationService notifications)
    {
        _invoices = invoices;
        _invoiceLines = invoiceLines;
        _purchaseOrders = purchaseOrders;
        _inventory = inventory;
        _products = products;
        _transfers = transfers;
        _notifications = notifications;
    }

    /// <inheritdoc />
    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var todaySales = await _invoices.Query()
            .Where(i => !i.IsDeleted && i.InvoiceDate >= today)
            .SumAsync(i => i.GrandTotal, ct);

        var monthSales = await _invoices.Query()
            .Where(i => !i.IsDeleted && i.InvoiceDate >= monthStart)
            .SumAsync(i => i.GrandTotal, ct);

        var monthPurchases = await _purchaseOrders.Query()
            .Where(p => !p.IsDeleted && p.OrderDate >= monthStart && p.Status != PurchaseOrderStatus.Cancelled)
            .SumAsync(p => p.GrandTotal, ct);

        var inventoryValue = await _inventory.Query()
            .Where(i => !i.IsDeleted)
            .SumAsync(i => i.QuantityOnHand * i.AverageCost, ct);

        var lowStockCount = await _products.Query()
            .Where(p => !p.IsDeleted && p.IsActive)
            .Select(p => new
            {
                p.MinimumStock,
                Quantity = p.InventoryItems.Where(i => !i.IsDeleted).Sum(i => i.QuantityOnHand)
            })
            .CountAsync(p => p.Quantity <= p.MinimumStock, ct);

        var pendingPo = await _purchaseOrders.Query()
            .CountAsync(p => !p.IsDeleted && (p.Status == PurchaseOrderStatus.Draft || p.Status == PurchaseOrderStatus.Approved || p.Status == PurchaseOrderStatus.PartiallyReceived), ct);

        var pendingTransfers = await _transfers.Query()
            .CountAsync(t => !t.IsDeleted && t.Status != TransferStatus.Completed && t.Status != TransferStatus.Cancelled, ct);

        var unread = await _notifications.GetUnreadCountAsync(ct);

        var sixMonthsAgo = monthStart.AddMonths(-5);
        var monthlySales = await _invoices.Query()
            .Where(i => !i.IsDeleted && i.InvoiceDate >= sixMonthsAgo)
            .GroupBy(i => new { i.InvoiceDate.Year, i.InvoiceDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Sales = g.Sum(x => x.GrandTotal) })
            .ToListAsync(ct);

        var monthlyPurchases = await _purchaseOrders.Query()
            .Where(p => !p.IsDeleted && p.OrderDate >= sixMonthsAgo && p.Status != PurchaseOrderStatus.Cancelled)
            .GroupBy(p => new { p.OrderDate.Year, p.OrderDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Purchases = g.Sum(x => x.GrandTotal) })
            .ToListAsync(ct);

        var monthlyPoints = Enumerable.Range(0, 6)
            .Select(i => monthStart.AddMonths(-i))
            .OrderBy(d => d)
            .Select(d =>
            {
                var sales = monthlySales.FirstOrDefault(m => m.Year == d.Year && m.Month == d.Month)?.Sales ?? 0;
                var purchases = monthlyPurchases.FirstOrDefault(m => m.Year == d.Year && m.Month == d.Month)?.Purchases ?? 0;
                return new MonthlySalesPoint(d.ToString("MMM yyyy"), sales, purchases);
            })
            .ToList();

        var topProductRows = await _invoiceLines.Query()
            .Where(l => !l.IsDeleted
                && !l.SalesInvoice.IsDeleted
                && l.SalesInvoice.InvoiceDate >= monthStart)
            .GroupBy(l => new { l.ProductId, l.ProductName, l.Sku })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.ProductName,
                g.Key.Sku,
                QuantitySold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.LineTotal)
            })
            .OrderByDescending(x => x.Revenue)
            .Take(10)
            .ToListAsync(ct);

        var topProducts = topProductRows
            .Select(x => new TopProductPoint(x.ProductId, x.ProductName, x.Sku, x.QuantitySold, x.Revenue))
            .ToList();

        var categoryDist = await _inventory.Query()
            .Where(i => !i.IsDeleted && !i.Product.IsDeleted && i.Product.IsActive)
            .GroupBy(i => new { i.Product.CategoryId, CategoryName = i.Product.Category.Name })
            .Select(g => new
            {
                g.Key.CategoryId,
                Name = g.Key.CategoryName,
                Value = g.Sum(x => x.QuantityOnHand * x.AverageCost)
            })
            .ToListAsync(ct);

        var totalCatValue = categoryDist.Sum(c => c.Value);
        var categoryPoints = categoryDist
            .OrderByDescending(c => c.Value)
            .Select(c => new CategoryDistributionPoint(
                c.CategoryId,
                c.Name,
                c.Value,
                totalCatValue > 0 ? Math.Round(c.Value / totalCatValue * 100, 2) : 0))
            .ToList();

        var inventoryTrendRows = await _inventory.Query()
            .Where(i => !i.IsDeleted)
            .GroupBy(i => new { i.CreatedAt.Year, i.CreatedAt.Month, i.CreatedAt.Day })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Day,
                Value = g.Sum(x => x.QuantityOnHand * x.AverageCost),
                Quantity = g.Sum(x => x.QuantityOnHand)
            })
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .ThenByDescending(x => x.Day)
            .Take(30)
            .ToListAsync(ct);

        var inventoryTrend = inventoryTrendRows
            .Select(x => new InventoryTrendPoint(
                new DateTime(x.Year, x.Month, x.Day, 0, 0, 0, DateTimeKind.Utc),
                x.Value,
                x.Quantity))
            .ToList();

        return new DashboardDto(
            todaySales,
            monthSales,
            monthPurchases,
            inventoryValue,
            lowStockCount,
            pendingPo,
            pendingTransfers,
            unread,
            monthlyPoints,
            inventoryTrend,
            topProducts,
            categoryPoints);
    }
}
