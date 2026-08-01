using CarAutoParts.Application.DTOs.Dashboard;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>Aggregates KPI and chart data for the dashboard (optional branch filter).</summary>
public class DashboardService : IDashboardService
{
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<SalesInvoiceLine> _invoiceLines;
    private readonly IRepository<PurchaseOrder> _purchaseOrders;
    private readonly IRepository<InventoryItem> _inventory;
    private readonly IRepository<Product> _products;
    private readonly IRepository<InventoryTransfer> _transfers;
    private readonly IRepository<Warehouse> _warehouses;
    private readonly IRepository<CashierShift> _shifts;
    private readonly INotificationService _notifications;
    private readonly ICurrentCompanyContext _company;

    public DashboardService(
        IRepository<SalesInvoice> invoices,
        IRepository<SalesInvoiceLine> invoiceLines,
        IRepository<PurchaseOrder> purchaseOrders,
        IRepository<InventoryItem> inventory,
        IRepository<Product> products,
        IRepository<InventoryTransfer> transfers,
        IRepository<Warehouse> warehouses,
        IRepository<CashierShift> shifts,
        INotificationService notifications,
        ICurrentCompanyContext company)
    {
        _invoices = invoices;
        _invoiceLines = invoiceLines;
        _purchaseOrders = purchaseOrders;
        _inventory = inventory;
        _products = products;
        _transfers = transfers;
        _warehouses = warehouses;
        _shifts = shifts;
        _notifications = notifications;
        _company = company;
    }

    /// <inheritdoc />
    public Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default)
        => GetDashboardAsync(null, ct);

    public async Task<DashboardDto> GetDashboardAsync(int? branchId, CancellationToken ct = default)
    {
        var effectiveBranch = branchId ?? _company.BranchId;
        if (effectiveBranch is int b && !_company.IsBranchAllowed(b))
            effectiveBranch = _company.BranchId;

        List<int>? warehouseIds = null;
        if (effectiveBranch is int branch)
        {
            warehouseIds = await _warehouses.Query()
                .Where(w => !w.IsDeleted && w.BranchId == branch)
                .Select(w => w.Id)
                .ToListAsync(ct);
        }

        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var invoiceQ = _invoices.Query().AsNoTracking().Where(i => !i.IsDeleted);
        if (warehouseIds is not null)
            invoiceQ = invoiceQ.Where(i => i.WarehouseId != null && warehouseIds.Contains(i.WarehouseId.Value));

        var todaySales = await invoiceQ.Where(i => i.InvoiceDate >= today).SumAsync(i => i.GrandTotal, ct);
        var monthSales = await invoiceQ.Where(i => i.InvoiceDate >= monthStart).SumAsync(i => i.GrandTotal, ct);

        var monthPurchases = await _purchaseOrders.Query()
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.OrderDate >= monthStart && p.Status != PurchaseOrderStatus.Cancelled)
            .SumAsync(p => p.GrandTotal, ct);

        var invQ = _inventory.Query().AsNoTracking().Where(i => !i.IsDeleted);
        if (warehouseIds is not null)
            invQ = invQ.Where(i => warehouseIds.Contains(i.WarehouseId));

        var inventoryValue = await invQ.SumAsync(i => i.QuantityOnHand * i.AverageCost, ct);

        var lowStockCount = await _products.Query()
            .Where(p => !p.IsDeleted && p.IsActive)
            .Select(p => new
            {
                p.MinimumStock,
                Quantity = p.InventoryItems
                    .Where(i => !i.IsDeleted && (warehouseIds == null || warehouseIds.Contains(i.WarehouseId)))
                    .Sum(i => i.QuantityOnHand)
            })
            .CountAsync(p => p.Quantity <= p.MinimumStock, ct);

        var pendingPo = await _purchaseOrders.Query()
            .CountAsync(p => !p.IsDeleted && (p.Status == PurchaseOrderStatus.Draft || p.Status == PurchaseOrderStatus.Approved || p.Status == PurchaseOrderStatus.PartiallyReceived), ct);

        var transferQ = _transfers.Query()
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .Where(t => !t.IsDeleted && t.Status != TransferStatus.Completed && t.Status != TransferStatus.Cancelled);
        if (warehouseIds is not null)
            transferQ = transferQ.Where(t =>
                warehouseIds.Contains(t.FromWarehouseId) || warehouseIds.Contains(t.ToWarehouseId));
        var pendingTransfers = await transferQ.CountAsync(ct);

        var unread = await _notifications.GetUnreadCountAsync(ct);

        var shiftQ = _shifts.Query().Where(s => !s.IsDeleted);
        if (effectiveBranch is int sb)
            shiftQ = shiftQ.Where(s => s.BranchId == sb);
        var openShifts = await shiftQ.CountAsync(s => s.Status == CashierShiftStatus.Open, ct);
        var todayCashVariance = await shiftQ
            .Where(s => s.Status == CashierShiftStatus.Closed && s.ClosedAt >= today)
            .SumAsync(s => s.CashVariance, ct);

        var sixMonthsAgo = monthStart.AddMonths(-5);
        var monthlySales = await invoiceQ
            .Where(i => i.InvoiceDate >= sixMonthsAgo)
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

        var lineQ = _invoiceLines.Query()
            .Where(l => !l.IsDeleted && !l.SalesInvoice.IsDeleted && l.SalesInvoice.InvoiceDate >= monthStart);
        if (warehouseIds is not null)
            lineQ = lineQ.Where(l => l.SalesInvoice.WarehouseId != null && warehouseIds.Contains(l.SalesInvoice.WarehouseId.Value));

        var topProductRows = await lineQ
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

        var catQ = invQ.Where(i => !i.Product.IsDeleted && i.Product.IsActive);
        var categoryDist = await catQ
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

        var inventoryTrendRows = await invQ
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
            categoryPoints,
            effectiveBranch,
            todayCashVariance,
            openShifts);
    }

    public async Task<DashboardTimelineDto> GetTimelineAsync(
        DateTime? from = null,
        DateTime? to = null,
        string grain = "day",
        string groupBy = "category",
        int? branchId = null,
        CancellationToken ct = default)
    {
        var effectiveBranch = branchId ?? _company.BranchId;
        if (effectiveBranch is int b && !_company.IsBranchAllowed(b))
            effectiveBranch = _company.BranchId;

        List<int>? warehouseIds = null;
        if (effectiveBranch is int branch)
        {
            warehouseIds = await _warehouses.Query()
                .Where(w => !w.IsDeleted && w.BranchId == branch)
                .Select(w => w.Id)
                .ToListAsync(ct);
        }

        var toDate = (to ?? DateTime.UtcNow).Date;
        var fromDate = (from ?? toDate.AddDays(-29)).Date;
        if (fromDate > toDate) (fromDate, toDate) = (toDate, fromDate);

        grain = string.Equals(grain, "week", StringComparison.OrdinalIgnoreCase) ? "week" : "day";
        groupBy = string.Equals(groupBy, "branch", StringComparison.OrdinalIgnoreCase) ? "branch" : "category";

        var lineQ = _invoiceLines.Query()
            .AsNoTracking()
            .Where(l => !l.IsDeleted
                        && !l.SalesInvoice.IsDeleted
                        && !l.SalesInvoice.IsVoided
                        && l.SalesInvoice.InvoiceDate >= fromDate
                        && l.SalesInvoice.InvoiceDate < toDate.AddDays(1));
        if (warehouseIds is not null)
            lineQ = lineQ.Where(l => l.SalesInvoice.WarehouseId != null && warehouseIds.Contains(l.SalesInvoice.WarehouseId.Value));

        var raw = await lineQ
            .Select(l => new
            {
                l.SalesInvoice.InvoiceDate,
                l.LineTotal,
                CategoryName = l.Product.Category.Name,
                BranchId = l.SalesInvoice.Warehouse != null ? l.SalesInvoice.Warehouse.BranchId : null,
                BranchName = l.SalesInvoice.Warehouse != null && l.SalesInvoice.Warehouse.Branch != null
                    ? l.SalesInvoice.Warehouse.Branch.Name
                    : "Unassigned"
            })
            .ToListAsync(ct);

        static DateTime PeriodStart(DateTime d, string g)
        {
            var day = d.Date;
            if (g != "week") return day;
            var diff = ((int)day.DayOfWeek + 6) % 7; // Monday-based
            return day.AddDays(-diff);
        }

        static string PeriodLabel(DateTime start, string g)
            => g == "week" ? $"W/c {start:dd MMM}" : start.ToString("dd MMM");

        string SeriesOf(string categoryName, string branchName) =>
            groupBy == "branch" ? branchName : categoryName;

        var seriesKeys = raw
            .Select(r => SeriesOf(r.CategoryName, r.BranchName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .Take(12)
            .ToList();

        if (seriesKeys.Count == 0)
            seriesKeys = ["No data"];

        var periodStarts = new List<DateTime>();
        if (grain == "week")
        {
            for (var start = PeriodStart(fromDate, grain); start <= toDate; start = start.AddDays(7))
                periodStarts.Add(start);
        }
        else
        {
            for (var d = fromDate; d <= toDate; d = d.AddDays(1))
                periodStarts.Add(d);
        }

        var frames = periodStarts.Select(start =>
        {
            var slice = raw.Where(r => PeriodStart(r.InvoiceDate, grain) == start).ToList();
            var values = seriesKeys
                .Select(key => slice
                    .Where(r => string.Equals(SeriesOf(r.CategoryName, r.BranchName), key, StringComparison.OrdinalIgnoreCase))
                    .Sum(r => r.LineTotal))
                .ToList();
            return new DashboardTimelineFrameDto(
                PeriodLabel(start, grain),
                start,
                values,
                values.Sum());
        }).ToList();

        var monthStart = new DateTime(toDate.Year, toDate.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);
        var monthRows = raw
            .Where(r => r.InvoiceDate >= monthStart)
            .GroupBy(r => new
            {
                Month = new DateTime(r.InvoiceDate.Year, r.InvoiceDate.Month, 1),
                Category = r.CategoryName
            })
            .Select(g => new DashboardBar3DPointDto(
                g.Key.Category,
                g.Key.Month.ToString("MMM yyyy"),
                g.Sum(x => x.LineTotal)))
            .ToList();

        // Prefer top categories for 3D readability
        var topCats = monthRows
            .GroupBy(x => x.Category)
            .OrderByDescending(g => g.Sum(x => x.Sales))
            .Take(8)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        monthRows = monthRows.Where(r => topCats.Contains(r.Category)).ToList();

        var months = Enumerable.Range(0, 6)
            .Select(i => monthStart.AddMonths(i).ToString("MMM yyyy"))
            .ToList();

        return new DashboardTimelineDto(
            fromDate,
            toDate,
            grain,
            groupBy,
            effectiveBranch,
            seriesKeys,
            frames,
            months,
            monthRows);
    }
}
