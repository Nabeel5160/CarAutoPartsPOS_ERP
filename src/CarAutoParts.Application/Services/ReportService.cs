using CarAutoParts.Application.Common;
using CarAutoParts.Application.Config;
using CarAutoParts.Application.DTOs.Reports;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace CarAutoParts.Application.Services;

/// <summary>Operational reports: JSON aggregates + Excel/CSV exports with branch ACL.</summary>
public class ReportService : IReportService
{
    private readonly IRepository<InventoryItem> _inventory;
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<PurchaseOrder> _purchaseOrders;
    private readonly IRepository<Warehouse> _warehouses;
    private readonly IRepository<SalesReturn> _returns;
    private readonly IRepository<StockMovement> _movements;
    private readonly IRepository<StockBatch> _batches;
    private readonly IRepository<GoodsReceiptNote> _grns;
    private readonly IRepository<FbrSubmission> _fbr;
    private readonly IRepository<Product> _products;
    private readonly ICurrentCompanyContext _company;
    private readonly IFeatureGate _features;
    private readonly IAnalyticsService _analytics;
    private readonly IFinancialReportService _financial;

    public ReportService(
        IRepository<InventoryItem> inventory,
        IRepository<SalesInvoice> invoices,
        IRepository<PurchaseOrder> purchaseOrders,
        IRepository<Warehouse> warehouses,
        IRepository<SalesReturn> returns,
        IRepository<StockMovement> movements,
        IRepository<StockBatch> batches,
        IRepository<GoodsReceiptNote> grns,
        IRepository<FbrSubmission> fbr,
        IRepository<Product> products,
        ICurrentCompanyContext company,
        IFeatureGate features,
        IAnalyticsService analytics,
        IFinancialReportService financial)
    {
        _inventory = inventory;
        _invoices = invoices;
        _purchaseOrders = purchaseOrders;
        _warehouses = warehouses;
        _returns = returns;
        _movements = movements;
        _batches = batches;
        _grns = grns;
        _fbr = fbr;
        _products = products;
        _company = company;
        _features = features;
        _analytics = analytics;
        _financial = financial;
    }

    private Result DenyBranch(int? branchId)
    {
        if (ReportBranchScope.IsDenied(_company, branchId))
            return Result.Failure("Branch is not allowed for this user.");
        return Result.Success();
    }

    private HashSet<int>? AllowedWarehouseIds(int? branchId) =>
        ReportBranchScope.AllowedWarehouseIds(_warehouses.Query(), _company, branchId);

    private IQueryable<SalesInvoice> InvoicesInScope(DateTime from, DateTime to, int? branchId)
    {
        var whIds = AllowedWarehouseIds(branchId);
        var q = _invoices.Query()
            .AsNoTracking()
            .Where(i => !i.IsDeleted && !i.IsVoided && i.InvoiceDate >= from && i.InvoiceDate <= to);
        if (whIds is not null)
            q = q.Where(i => i.WarehouseId != null && whIds.Contains(i.WarehouseId.Value));
        return q;
    }

    private Result<(DateTime From, DateTime To)> RequireInteractiveRange(DateTime from, DateTime to)
        => ReportDateRange.ValidateInteractive(from, to);

    private Result<(DateTime From, DateTime To)> RequireExportRange(DateTime from, DateTime to)
        => ReportDateRange.ValidateExport(from, to);

    /// <inheritdoc />
    public async Task<byte[]> ExportInventoryReportAsync(string format, int? branchId = null, CancellationToken ct = default)
    {
        if (!DenyBranch(branchId).Succeeded) return Encoding.UTF8.GetBytes("Branch not allowed");

        var whIds = AllowedWarehouseIds(branchId);
        var q = _inventory.Query()
            .Include(i => i.Product)
            .Include(i => i.Warehouse)
            .Where(i => !i.IsDeleted);
        if (whIds is not null)
            q = q.Where(i => whIds.Contains(i.WarehouseId));

        var items = await q.OrderBy(i => i.Product.Name).ToListAsync(ct);

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var sb = new StringBuilder();
            sb.AppendLine("Product,SKU,Warehouse,Qty,Value");
            foreach (var i in items)
                sb.AppendLine($"{i.Product.Name},{i.Product.Sku},{i.Warehouse.Name},{i.QuantityOnHand},{i.QuantityOnHand * i.AverageCost}");
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        return ToWorkbook("Inventory",
            ["Product", "SKU", "Warehouse", "Quantity", "Value"],
            items.Select(i => new object[]
            {
                i.Product.Name, i.Product.Sku, i.Warehouse.Name, i.QuantityOnHand, i.QuantityOnHand * i.AverageCost
            }));
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportSalesReportAsync(DateTime from, DateTime to, string period, string format, int? branchId = null, CancellationToken ct = default)
    {
        if (!DenyBranch(branchId).Succeeded) return Encoding.UTF8.GetBytes("Branch not allowed");
        var range = RequireExportRange(from, to);
        if (!range.Succeeded) return Encoding.UTF8.GetBytes(range.Error!);
        (from, to) = range.Data!;

        var invoices = await InvoicesInScope(from, to, branchId)
            .OrderBy(i => i.InvoiceDate)
            .ToListAsync(ct);

        return ToWorkbook("Sales",
            ["Invoice", "Date", "SubTotal", "Tax", "Grand Total"],
            invoices.Select(i => new object[] { i.InvoiceNumber, i.InvoiceDate, i.SubTotal, i.TaxAmount, i.GrandTotal }));
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportPurchaseReportAsync(DateTime from, DateTime to, string format, int? branchId = null, CancellationToken ct = default)
    {
        if (!DenyBranch(branchId).Succeeded) return Encoding.UTF8.GetBytes("Branch not allowed");
        var range = RequireExportRange(from, to);
        if (!range.Succeeded) return Encoding.UTF8.GetBytes(range.Error!);
        (from, to) = range.Data!;

        var whIds = AllowedWarehouseIds(branchId);
        var q = _purchaseOrders.Query()
            .Include(o => o.Supplier)
            .Include(o => o.Warehouse)
            .Where(o => !o.IsDeleted && o.OrderDate >= from && o.OrderDate <= to);
        if (whIds is not null)
            q = q.Where(o => o.WarehouseId == null || whIds.Contains(o.WarehouseId.Value));

        var orders = await q.OrderBy(o => o.OrderDate).ToListAsync(ct);

        return ToWorkbook("Purchases",
            ["PO Number", "Supplier", "Date", "Status", "Grand Total"],
            orders.Select(o => new object[] { o.OrderNumber, o.Supplier.Name, o.OrderDate, o.Status.ToString(), o.GrandTotal }));
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportProfitReportAsync(DateTime from, DateTime to, string format, int? branchId = null, CancellationToken ct = default)
    {
        if (!DenyBranch(branchId).Succeeded) return Encoding.UTF8.GetBytes("Branch not allowed");
        var range = RequireExportRange(from, to);
        if (!range.Succeeded) return Encoding.UTF8.GetBytes(range.Error!);
        (from, to) = range.Data!;

        var lines = await InvoicesInScope(from, to, branchId)
            .SelectMany(i => i.Lines.Where(l => !l.IsDeleted))
            .GroupBy(l => new { l.ProductId, l.ProductName, l.Sku })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.ProductName,
                g.Key.Sku,
                Qty = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.LineTotal),
                Cost = g.Sum(x => x.Quantity * (x.UnitCost > 0
                    ? x.UnitCost
                    : (x.Product.CostPrice > 0 ? x.Product.CostPrice : x.Product.PurchasePrice)))
            })
            .ToListAsync(ct);

        return ToWorkbook("Profit",
            ["ProductId", "Product", "SKU", "Quantity", "Revenue", "COGS", "Profit", "GM%"],
            lines.Select(l =>
            {
                var profit = l.Revenue - l.Cost;
                return new object[]
                {
                    l.ProductId, l.ProductName, l.Sku, l.Qty, l.Revenue, l.Cost, profit,
                    l.Revenue > 0 ? profit / l.Revenue * 100m : 0
                };
            }));
    }

    public async Task<Result<DailySalesSummaryDto>> GetDailySalesSummaryAsync(
        DateTime from, DateTime to, int? branchId = null, CancellationToken ct = default)
    {
        return await GetDailySalesSummaryCoreAsync(from, to, branchId, interactive: true, ct);
    }

    private async Task<Result<DailySalesSummaryDto>> GetDailySalesSummaryCoreAsync(
        DateTime from, DateTime to, int? branchId, bool interactive, CancellationToken ct)
    {
        var deny = DenyBranch(branchId);
        if (!deny.Succeeded) return Result<DailySalesSummaryDto>.Failure(deny.Error!);

        var range = interactive ? RequireInteractiveRange(from, to) : RequireExportRange(from, to);
        if (!range.Succeeded) return Result<DailySalesSummaryDto>.Failure(range.Error!);
        (from, to) = range.Data!;

        var taxEnabled = await _features.BehaviorEnabledAsync(ConfigKeys.BehTaxEnabled, ct);
        var invQ = InvoicesInScope(from, to, branchId);

        // Server-side aggregates — avoid materializing full invoice + payment graphs.
        var invoiceCount = await invQ.CountAsync(ct);
        var subTotal = invoiceCount == 0 ? 0m : await invQ.SumAsync(i => i.SubTotal, ct);
        var taxAmount = invoiceCount == 0 || !taxEnabled ? 0m : await invQ.SumAsync(i => i.TaxAmount, ct);
        var discountAmount = invoiceCount == 0 ? 0m : await invQ.SumAsync(i => i.DiscountAmount, ct);
        var salesTotal = invoiceCount == 0 ? 0m : await invQ.SumAsync(i => i.GrandTotal, ct);

        var daySales = invoiceCount == 0
            ? new List<(DateTime Date, int Count, decimal Sales, decimal Tax)>()
            : (await invQ
                .GroupBy(i => new { i.InvoiceDate.Year, i.InvoiceDate.Month, i.InvoiceDate.Day })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    g.Key.Day,
                    Count = g.Count(),
                    Sales = g.Sum(x => x.GrandTotal),
                    Tax = g.Sum(x => x.TaxAmount)
                })
                .ToListAsync(ct))
            .Select(x => (
                Date: new DateTime(x.Year, x.Month, x.Day, 0, 0, 0, DateTimeKind.Utc),
                Count: x.Count,
                Sales: x.Sales,
                Tax: x.Tax))
            .ToList();

        var tenders = invoiceCount == 0
            ? new List<TenderTotalDto>()
            : (await invQ
                .SelectMany(i => i.Payments.Where(p => !p.IsDeleted))
                .GroupBy(p => p.PaymentMethod)
                .Select(g => new { Method = g.Key, Amount = g.Sum(x => x.Amount) })
                .ToListAsync(ct))
            .GroupBy(t => t.Method, StringComparer.OrdinalIgnoreCase)
            .Select(g => new TenderTotalDto(g.First().Method, g.Sum(x => x.Amount)))
            .OrderBy(t => t.Method)
            .ToList();

        var whIds = AllowedWarehouseIds(branchId);
        var returnsQ = _returns.Query()
            .AsNoTracking()
            .Where(r => !r.IsDeleted && r.ReturnDate >= from && r.ReturnDate <= to);
        if (whIds is not null)
            returnsQ = returnsQ.Where(r =>
                r.SalesInvoice != null &&
                r.SalesInvoice.WarehouseId != null &&
                whIds.Contains(r.SalesInvoice.WarehouseId.Value));

        var returnCount = await returnsQ.CountAsync(ct);
        var returnsTotal = returnCount == 0 ? 0m : await returnsQ.SumAsync(r => r.GrandTotal, ct);
        var dayReturns = returnCount == 0
            ? new Dictionary<DateTime, decimal>()
            : (await returnsQ
                .GroupBy(r => new { r.ReturnDate.Year, r.ReturnDate.Month, r.ReturnDate.Day })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    g.Key.Day,
                    Total = g.Sum(x => x.GrandTotal)
                })
                .ToListAsync(ct))
            .ToDictionary(
                x => new DateTime(x.Year, x.Month, x.Day, 0, 0, 0, DateTimeKind.Utc),
                x => x.Total);

        var dayKeys = daySales.Select(d => d.Item1)
            .Union(dayReturns.Keys)
            .OrderBy(d => d)
            .ToList();

        var days = dayKeys.Select(d =>
            {
                var sale = daySales.FirstOrDefault(x => x.Item1 == d);
                var sales = sale.Item3;
                var count = sale.Item2;
                var tax = taxEnabled ? sale.Item4 : 0m;
                dayReturns.TryGetValue(d, out var ret);
                return new DailySalesDayRowDto(d, count, sales, tax, ret, sales - ret);
            })
            .ToList();

        return Result<DailySalesSummaryDto>.Success(new DailySalesSummaryDto(
            from, to, branchId, taxEnabled,
            invoiceCount, returnCount,
            subTotal,
            taxAmount,
            discountAmount,
            salesTotal, returnsTotal, salesTotal - returnsTotal,
            tenders, days));
    }

    public async Task<byte[]> ExportDailySalesSummaryAsync(
        DateTime from, DateTime to, int? branchId = null, CancellationToken ct = default)
    {
        var result = await GetDailySalesSummaryCoreAsync(from, to, branchId, interactive: false, ct);
        if (!result.Succeeded || result.Data is null)
            return Encoding.UTF8.GetBytes(result.Error ?? "Error");

        var d = result.Data;
        using var wb = new XLWorkbook();
        var summary = wb.Worksheets.Add("Summary");
        summary.Cell(1, 1).Value = "Metric";
        summary.Cell(1, 2).Value = "Value";
        var rows = new (string, object)[]
        {
            ("From", d.From), ("To", d.To), ("Invoices", d.InvoiceCount), ("Returns", d.ReturnCount),
            ("SubTotal", d.SubTotal), ("Tax", d.TaxAmount), ("Discount", d.DiscountAmount),
            ("Sales", d.SalesTotal), ("Returns total", d.ReturnsTotal), ("Net sales", d.NetSales)
        };
        for (var i = 0; i < rows.Length; i++)
        {
            summary.Cell(i + 2, 1).Value = rows[i].Item1;
            summary.Cell(i + 2, 2).Value = rows[i].Item2 is DateTime dt ? dt : Convert.ToDouble(rows[i].Item2 is decimal m ? m : rows[i].Item2);
            if (rows[i].Item2 is DateTime date)
                summary.Cell(i + 2, 2).Value = date;
            else if (rows[i].Item2 is decimal dec)
                summary.Cell(i + 2, 2).Value = dec;
            else
                summary.Cell(i + 2, 2).Value = Convert.ToInt32(rows[i].Item2);
        }

        var tenderSheet = wb.Worksheets.Add("Tenders");
        WriteSheet(tenderSheet, ["Method", "Amount"], d.Tenders.Select(t => new object[] { t.Method, t.Amount }));

        var daySheet = wb.Worksheets.Add("By Day");
        WriteSheet(daySheet,
            ["Date", "Invoices", "Sales", "Tax", "Returns", "Net"],
            d.Days.Select(x => new object[] { x.Date, x.InvoiceCount, x.SalesTotal, x.TaxAmount, x.ReturnsTotal, x.NetSales }));

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<Result<SalesReturnsReportDto>> GetSalesReturnsReportAsync(
        DateTime from, DateTime to, int? branchId = null, CancellationToken ct = default)
    {
        var deny = DenyBranch(branchId);
        if (!deny.Succeeded) return Result<SalesReturnsReportDto>.Failure(deny.Error!);

        var range = RequireInteractiveRange(from, to);
        if (!range.Succeeded) return Result<SalesReturnsReportDto>.Failure(range.Error!);
        (from, to) = range.Data!;

        var whIds = AllowedWarehouseIds(branchId);
        var list = await _returns.Query()
            .AsNoTracking()
            .Include(r => r.Customer)
            .Include(r => r.SalesInvoice)!.ThenInclude(i => i!.Warehouse)
            .Where(r => !r.IsDeleted && r.ReturnDate >= from && r.ReturnDate <= to)
            .OrderByDescending(r => r.ReturnDate)
            .Take(QueryLimits.MaxMovementRows)
            .ToListAsync(ct);

        if (whIds is not null)
            list = list.Where(r => r.SalesInvoice?.WarehouseId is int wid && whIds.Contains(wid)).ToList();

        var rows = list.Select(r => new SalesReturnReportRowDto(
            r.Id, r.ReturnNumber, r.CreditNoteNumber, r.ReturnDate, r.Status.ToString(),
            r.SalesInvoice?.InvoiceNumber, r.Customer?.Name, r.GrandTotal, r.TaxAmount, r.ReasonCode)).ToList();

        return Result<SalesReturnsReportDto>.Success(new SalesReturnsReportDto(
            from, to, branchId, rows.Sum(x => x.GrandTotal), rows.Sum(x => x.TaxAmount), rows));
    }

    public async Task<byte[]> ExportSalesReturnsReportAsync(
        DateTime from, DateTime to, int? branchId = null, CancellationToken ct = default)
    {
        var result = await GetSalesReturnsReportAsync(from, to, branchId, ct);
        if (!result.Succeeded || result.Data is null)
            return Encoding.UTF8.GetBytes(result.Error ?? "Error");

        return ToWorkbook("Sales Returns",
            ["Return #", "Credit note", "Date", "Status", "Invoice", "Customer", "Total", "Tax", "Reason"],
            result.Data.Rows.Select(r => new object[]
            {
                r.ReturnNumber, r.CreditNoteNumber ?? "", r.ReturnDate, r.Status,
                r.InvoiceNumber ?? "", r.CustomerName ?? "", r.GrandTotal, r.TaxAmount, r.ReasonCode
            }));
    }

    public async Task<Result<SalesDimReportDto>> GetSalesDimReportAsync(
        DateTime from, DateTime to, string dimension, int? branchId = null, CancellationToken ct = default)
    {
        var deny = DenyBranch(branchId);
        if (!deny.Succeeded) return Result<SalesDimReportDto>.Failure(deny.Error!);

        var dim = (dimension ?? "product").Trim().ToLowerInvariant();
        var includeOem = (await _features.GetFieldAsync(ConfigKeys.FieldProductOem, ct)).Visible;

        var lines = await InvoicesInScope(from, to, branchId)
            .SelectMany(i => i.Lines.Where(l => !l.IsDeleted).Select(l => new
            {
                InvoiceId = i.Id,
                l.ProductId,
                l.ProductName,
                l.Sku,
                l.Quantity,
                l.LineTotal,
                l.TaxAmount,
                Oem = l.Product.OemNumber,
                Category = l.Product.Category.Name,
                CategoryId = l.Product.CategoryId,
                Brand = l.Product.Brand.Name,
                BrandId = l.Product.BrandId
            }))
            .ToListAsync(ct);

        IReadOnlyList<SalesDimRowDto> rows = dim switch
        {
            "category" => lines.GroupBy(l => new { l.CategoryId, l.Category })
                .Select(g => new SalesDimRowDto(
                    g.Key.CategoryId.ToString(), g.Key.Category, null, null,
                    g.Sum(x => x.Quantity), g.Sum(x => x.LineTotal), g.Sum(x => x.TaxAmount),
                    g.Select(x => x.InvoiceId).Distinct().Count()))
                .OrderByDescending(r => r.Revenue).ToList(),
            "brand" => lines.GroupBy(l => new { l.BrandId, l.Brand })
                .Select(g => new SalesDimRowDto(
                    g.Key.BrandId.ToString(), g.Key.Brand, null, null,
                    g.Sum(x => x.Quantity), g.Sum(x => x.LineTotal), g.Sum(x => x.TaxAmount),
                    g.Select(x => x.InvoiceId).Distinct().Count()))
                .OrderByDescending(r => r.Revenue).ToList(),
            _ => lines.GroupBy(l => new { l.ProductId, l.ProductName, l.Sku, l.Oem })
                .Select(g => new SalesDimRowDto(
                    g.Key.ProductId.ToString(), g.Key.ProductName, g.Key.Sku,
                    includeOem ? g.Key.Oem : null,
                    g.Sum(x => x.Quantity), g.Sum(x => x.LineTotal), g.Sum(x => x.TaxAmount),
                    g.Select(x => x.InvoiceId).Distinct().Count()))
                .OrderByDescending(r => r.Revenue).ToList()
        };

        return Result<SalesDimReportDto>.Success(new SalesDimReportDto(dim, from, to, branchId, includeOem, rows));
    }

    public async Task<byte[]> ExportSalesDimReportAsync(
        DateTime from, DateTime to, string dimension, int? branchId = null, CancellationToken ct = default)
    {
        var result = await GetSalesDimReportAsync(from, to, dimension, branchId, ct);
        if (!result.Succeeded || result.Data is null)
            return Encoding.UTF8.GetBytes(result.Error ?? "Error");

        var headers = result.Data.IncludeOem
            ? new[] { "Key", "Label", "SKU", "OEM", "Qty", "Revenue", "Tax", "Invoices" }
            : new[] { "Key", "Label", "SKU", "Qty", "Revenue", "Tax", "Invoices" };

        return ToWorkbook($"Sales by {result.Data.Dimension}", headers,
            result.Data.Rows.Select(r => result.Data.IncludeOem
                ? new object[] { r.Key, r.Label, r.Sku ?? "", r.OemNumber ?? "", r.Quantity, r.Revenue, r.TaxAmount, r.InvoiceCount }
                : new object[] { r.Key, r.Label, r.Sku ?? "", r.Quantity, r.Revenue, r.TaxAmount, r.InvoiceCount }));
    }

    public async Task<Result<SalesStaffReportDto>> GetSalesStaffReportAsync(
        DateTime from, DateTime to, string dimension, int? branchId = null, CancellationToken ct = default)
    {
        var deny = DenyBranch(branchId);
        if (!deny.Succeeded) return Result<SalesStaffReportDto>.Failure(deny.Error!);

        var dim = (dimension ?? "cashier").Trim().ToLowerInvariant();
        var invoices = await InvoicesInScope(from, to, branchId)
            .Include(i => i.Payments)
            .Include(i => i.CashierShift)
            .Include(i => i.Warehouse)
            .ToListAsync(ct);

        IReadOnlyList<SalesStaffRowDto> rows = dim switch
        {
            "till" => invoices.GroupBy(i => i.CashierShift?.TillId)
                .Select(g => BuildStaffRow(
                    (g.Key?.ToString() ?? "none"),
                    g.Key is int tid ? $"Till {tid}" : "No till",
                    g.ToList()))
                .OrderByDescending(r => r.SalesTotal).ToList(),
            "branch" => invoices.GroupBy(i => i.Warehouse?.BranchId ?? i.CashierShift?.BranchId)
                .Select(g => BuildStaffRow(
                    (g.Key?.ToString() ?? "none"),
                    g.Key is int bid ? $"Branch {bid}" : "No branch",
                    g.ToList()))
                .OrderByDescending(r => r.SalesTotal).ToList(),
            _ => invoices.GroupBy(i => i.CashierShift?.UserName ?? i.CreatedBy ?? "Unknown")
                .Select(g => BuildStaffRow(g.Key, g.Key, g.ToList()))
                .OrderByDescending(r => r.SalesTotal).ToList()
        };

        return Result<SalesStaffReportDto>.Success(new SalesStaffReportDto(dim, from, to, branchId, rows));
    }

    private static SalesStaffRowDto BuildStaffRow(string key, string label, List<SalesInvoice> invoices)
    {
        var payments = invoices.SelectMany(i => i.Payments.Where(p => !p.IsDeleted)).ToList();
        var cash = payments.Where(p => p.PaymentMethod.Contains("cash", StringComparison.OrdinalIgnoreCase)).Sum(p => p.Amount);
        var other = payments.Sum(p => p.Amount) - cash;
        return new SalesStaffRowDto(key, label, invoices.Count,
            invoices.Sum(i => i.GrandTotal), invoices.Sum(i => i.TaxAmount), cash, other);
    }

    public async Task<byte[]> ExportSalesStaffReportAsync(
        DateTime from, DateTime to, string dimension, int? branchId = null, CancellationToken ct = default)
    {
        var result = await GetSalesStaffReportAsync(from, to, dimension, branchId, ct);
        if (!result.Succeeded || result.Data is null)
            return Encoding.UTF8.GetBytes(result.Error ?? "Error");

        return ToWorkbook($"Sales by {result.Data.Dimension}",
            ["Key", "Label", "Invoices", "Sales", "Tax", "Cash", "Other tender"],
            result.Data.Rows.Select(r => new object[]
            {
                r.Key, r.Label, r.InvoiceCount, r.SalesTotal, r.TaxAmount, r.TenderCash, r.TenderOther
            }));
    }

    public async Task<Result<ProfitDimReportDto>> GetProfitDimReportAsync(
        DateTime from, DateTime to, string dimension, int? branchId = null, CancellationToken ct = default)
    {
        var deny = DenyBranch(branchId);
        if (!deny.Succeeded) return Result<ProfitDimReportDto>.Failure(deny.Error!);

        var dim = (dimension ?? "category").Trim().ToLowerInvariant();
        var lines = await InvoicesInScope(from, to, branchId)
            .SelectMany(i => i.Lines.Where(l => !l.IsDeleted).Select(l => new
            {
                BranchId = i.Warehouse != null ? i.Warehouse.BranchId : null,
                Category = l.Product.Category.Name,
                CategoryId = l.Product.CategoryId,
                l.Quantity,
                Revenue = l.LineTotal,
                Cost = l.Quantity * (l.UnitCost > 0
                    ? l.UnitCost
                    : (l.Product.CostPrice > 0 ? l.Product.CostPrice : l.Product.PurchasePrice))
            }))
            .ToListAsync(ct);

        IReadOnlyList<ProfitDimRowDto> rows = dim switch
        {
            "branch" => lines.GroupBy(l => l.BranchId)
                .Select(g => ToProfitRow(
                    g.Key?.ToString() ?? "none",
                    g.Key is int b ? $"Branch {b}" : "No branch",
                    g.Sum(x => x.Quantity), g.Sum(x => x.Revenue), g.Sum(x => x.Cost)))
                .OrderByDescending(r => r.Profit).ToList(),
            _ => lines.GroupBy(l => new { l.CategoryId, l.Category })
                .Select(g => ToProfitRow(
                    g.Key.CategoryId.ToString(), g.Key.Category,
                    g.Sum(x => x.Quantity), g.Sum(x => x.Revenue), g.Sum(x => x.Cost)))
                .OrderByDescending(r => r.Profit).ToList()
        };

        return Result<ProfitDimReportDto>.Success(new ProfitDimReportDto(dim, from, to, branchId, rows));
    }

    private static ProfitDimRowDto ToProfitRow(string key, string label, decimal qty, decimal revenue, decimal cost)
    {
        var profit = revenue - cost;
        return new ProfitDimRowDto(key, label, qty, revenue, cost, profit,
            revenue > 0 ? profit / revenue * 100m : 0);
    }

    public async Task<byte[]> ExportProfitDimReportAsync(
        DateTime from, DateTime to, string dimension, int? branchId = null, CancellationToken ct = default)
    {
        var result = await GetProfitDimReportAsync(from, to, dimension, branchId, ct);
        if (!result.Succeeded || result.Data is null)
            return Encoding.UTF8.GetBytes(result.Error ?? "Error");

        return ToWorkbook($"Profit by {result.Data.Dimension}",
            ["Key", "Label", "Qty", "Revenue", "COGS", "Profit", "GM%"],
            result.Data.Rows.Select(r => new object[]
            {
                r.Key, r.Label, r.Quantity, r.Revenue, r.Cost, r.Profit, r.GrossMarginPercent
            }));
    }

    public async Task<byte[]> ExportAnalyticsAsync(
        DateTime? from, DateTime? to, int? branchId, int deadStockDays = 90, CancellationToken ct = default)
    {
        if (!DenyBranch(branchId).Succeeded) return Encoding.UTF8.GetBytes("Branch not allowed");

        var dto = await _analytics.GetAnalyticsAsync(from, to, branchId, deadStockDays, ct);
        using var wb = new XLWorkbook();

        var kpi = wb.Worksheets.Add("KPIs");
        WriteSheet(kpi, ["Metric", "Value"],
        [
            new object[] { "Inventory value", dto.TotalInventoryValue },
            new object[] { "Turnover", dto.TurnoverRatio },
            new object[] { "Gross margin", dto.GrossMarginAmount },
            new object[] { "GM%", dto.GrossMarginPercent }
        ]);

        WriteSheet(wb.Worksheets.Add("Top selling"),
            ["Product", "SKU", "Qty", "Revenue", "Profit"],
            dto.TopSellingProducts.Select(p => new object[] { p.ProductName, p.Sku, p.QuantitySold, p.Revenue, p.Profit }));
        WriteSheet(wb.Worksheets.Add("Fast movers"),
            ["Product", "SKU", "Qty", "Revenue", "Profit"],
            dto.FastMovers.Select(p => new object[] { p.ProductName, p.Sku, p.QuantitySold, p.Revenue, p.Profit }));
        WriteSheet(wb.Worksheets.Add("Dead stock"),
            ["Product", "SKU", "Qty", "Value", "Days"],
            dto.DeadStock.Select(p => new object[] { p.ProductName, p.Sku, p.QuantityOnHand, p.StockValue, p.DaysSinceLastSale }));
        WriteSheet(wb.Worksheets.Add("Slow moving"),
            ["Product", "SKU", "Qty", "Days"],
            dto.SlowMovingProducts.Select(p => new object[] { p.ProductName, p.Sku, p.QuantityOnHand, p.DaysSinceLastSale }));
        WriteSheet(wb.Worksheets.Add("ABC"),
            ["Class", "Product", "SKU", "Usage value", "Cumulative %"],
            dto.AbcAnalysis.Select(p => new object[] { p.AbcClass.ToString(), p.ProductName, p.Sku, p.AnnualUsageValue, p.CumulativePercentage }));

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<Result<StockMovementReportDto>> GetStockMovementsReportAsync(
        DateTime from, DateTime to, int? warehouseId = null, string? movementType = null, CancellationToken ct = default)
    {
        var whIds = AllowedWarehouseIds(null);
        var q = _movements.Query()
            .Include(m => m.InventoryItem).ThenInclude(i => i.Product)
            .Include(m => m.InventoryItem).ThenInclude(i => i.Warehouse)
            .Where(m => !m.IsDeleted && m.MovementDate >= from && m.MovementDate <= to);

        if (warehouseId is int wid)
        {
            var wh = await _warehouses.Query().FirstOrDefaultAsync(w => w.Id == wid && !w.IsDeleted, ct);
            if (wh?.BranchId is int b && !_company.IsBranchAllowed(b))
                return Result<StockMovementReportDto>.Failure("Warehouse branch is not allowed.");
            q = q.Where(m => m.InventoryItem.WarehouseId == wid);
        }
        else if (whIds is not null)
            q = q.Where(m => whIds.Contains(m.InventoryItem.WarehouseId));

        if (!string.IsNullOrWhiteSpace(movementType) &&
            Enum.TryParse<StockMovementType>(movementType, true, out var mt))
            q = q.Where(m => m.MovementType == mt);

        var list = await q.OrderByDescending(m => m.MovementDate).Take(5000).ToListAsync(ct);
        var rows = list.Select(m => new StockMovementReportRowDto(
            m.Id, m.MovementDate, m.MovementType.ToString(),
            m.InventoryItem.Product.Sku, m.InventoryItem.Product.Name, m.InventoryItem.Warehouse.Name,
            m.Quantity, m.UnitCost, m.ReferenceType, m.ReferenceId, m.Notes)).ToList();

        return Result<StockMovementReportDto>.Success(
            new StockMovementReportDto(from, to, warehouseId, movementType, rows));
    }

    public async Task<byte[]> ExportStockMovementsReportAsync(
        DateTime from, DateTime to, int? warehouseId = null, string? movementType = null, CancellationToken ct = default)
    {
        var result = await GetStockMovementsReportAsync(from, to, warehouseId, movementType, ct);
        if (!result.Succeeded || result.Data is null)
            return Encoding.UTF8.GetBytes(result.Error ?? "Error");

        return ToWorkbook("Movements",
            ["Date", "Type", "SKU", "Product", "Warehouse", "Qty", "Unit cost", "Ref type", "Ref id", "Notes"],
            result.Data.Rows.Select(r => new object[]
            {
                r.MovementDate, r.MovementType, r.Sku, r.ProductName, r.WarehouseName,
                r.Quantity, r.UnitCost, r.ReferenceType ?? "", r.ReferenceId ?? 0, r.Notes ?? ""
            }));
    }

    public async Task<Result<PurchasingPipelineReportDto>> GetPurchasingPipelineAsync(
        int? branchId = null, CancellationToken ct = default)
    {
        var deny = DenyBranch(branchId);
        if (!deny.Succeeded) return Result<PurchasingPipelineReportDto>.Failure(deny.Error!);

        var whIds = AllowedWarehouseIds(branchId);
        var openStatuses = new[]
        {
            PurchaseOrderStatus.Draft, PurchaseOrderStatus.Approved, PurchaseOrderStatus.PartiallyReceived
        };

        var poQ = _purchaseOrders.Query()
            .Include(o => o.Supplier)
            .Include(o => o.Warehouse)
            .Include(o => o.Lines)
            .Where(o => !o.IsDeleted && openStatuses.Contains(o.Status));
        if (whIds is not null)
            poQ = poQ.Where(o => o.WarehouseId == null || whIds.Contains(o.WarehouseId.Value));

        var pos = await poQ.OrderBy(o => o.OrderDate).ToListAsync(ct);
        var openPos = pos.Select(o => new OpenPoRowDto(
            o.Id, o.OrderNumber, o.Supplier.Name, o.OrderDate, o.ExpectedDate, o.Status.ToString(),
            o.GrandTotal,
            o.Lines.Where(l => !l.IsDeleted).Sum(l => l.QuantityOrdered),
            o.Lines.Where(l => !l.IsDeleted).Sum(l => l.QuantityReceived),
            o.Warehouse?.Name, o.Warehouse?.BranchId)).ToList();

        var pendingStatuses = new[] { GrnStatus.Draft, GrnStatus.QcHold };
        var grnQ = _grns.Query()
            .Include(g => g.Warehouse)
            .Include(g => g.PurchaseOrder)
            .Include(g => g.Lines)
            .Where(g => !g.IsDeleted && pendingStatuses.Contains(g.Status));
        if (whIds is not null)
            grnQ = grnQ.Where(g => whIds.Contains(g.WarehouseId));

        var grns = await grnQ.OrderBy(g => g.ReceiptDate).ToListAsync(ct);
        var pending = grns.Select(g => new PendingGrnRowDto(
            g.Id, g.GrnNumber, g.PurchaseOrder?.OrderNumber, g.ReceiptDate, g.Status.ToString(),
            g.Warehouse.Name, g.Warehouse.BranchId,
            g.Lines.Where(l => !l.IsDeleted).Sum(l => l.Quantity))).ToList();

        return Result<PurchasingPipelineReportDto>.Success(new PurchasingPipelineReportDto(openPos, pending));
    }

    public async Task<byte[]> ExportPurchasingPipelineAsync(int? branchId = null, CancellationToken ct = default)
    {
        var result = await GetPurchasingPipelineAsync(branchId, ct);
        if (!result.Succeeded || result.Data is null)
            return Encoding.UTF8.GetBytes(result.Error ?? "Error");

        using var wb = new XLWorkbook();
        WriteSheet(wb.Worksheets.Add("Open POs"),
            ["PO #", "Supplier", "Date", "Expected", "Status", "Total", "Ordered", "Received", "Warehouse"],
            result.Data.OpenPurchaseOrders.Select(o => new object[]
            {
                o.OrderNumber, o.SupplierName, o.OrderDate, o.ExpectedDate ?? (object)"",
                o.Status, o.GrandTotal, o.QtyOrdered, o.QtyReceived, o.WarehouseName ?? ""
            }));
        WriteSheet(wb.Worksheets.Add("Pending GRN"),
            ["GRN #", "PO #", "Date", "Status", "Warehouse", "Qty"],
            result.Data.PendingGrns.Select(g => new object[]
            {
                g.GrnNumber, g.PoNumber ?? "", g.ReceiptDate, g.Status, g.WarehouseName, g.LineQty
            }));
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> ExportAgingReportAsync(string kind, DateTime? asOfDate = null, CancellationToken ct = default)
    {
        var isSupplier = kind.Equals("suppliers", StringComparison.OrdinalIgnoreCase) ||
                         kind.Equals("ap", StringComparison.OrdinalIgnoreCase);
        var result = isSupplier
            ? await _financial.SupplierAgingAsync(asOfDate, ct)
            : await _financial.CustomerAgingAsync(asOfDate, ct);
        if (!result.Succeeded || result.Data is null)
            return Encoding.UTF8.GetBytes(result.Error ?? "Error");

        return ToWorkbook(isSupplier ? "AP Aging" : "AR Aging",
            ["Partner", "Current", "30", "60", "90+", "Total"],
            result.Data.Lines.Select(l => new object[]
            {
                l.Name, l.Current, l.Days30, l.Days60, l.Days90, l.Total
            }));
    }

    public async Task<Result<TaxPeriodSummaryDto>> GetTaxPeriodSummaryAsync(
        DateTime from, DateTime to, int? branchId = null, CancellationToken ct = default)
    {
        var deny = DenyBranch(branchId);
        if (!deny.Succeeded) return Result<TaxPeriodSummaryDto>.Failure(deny.Error!);

        var taxEnabled = await _features.BehaviorEnabledAsync(ConfigKeys.BehTaxEnabled, ct);
        var includeHs = (await _features.GetFieldAsync(ConfigKeys.FieldProductHsCode, ct)).Visible;

        var lines = await InvoicesInScope(from, to, branchId)
            .SelectMany(i => i.Lines.Where(l => !l.IsDeleted))
            .Select(l => new { l.TaxRate, l.TaxAmount, Taxable = l.LineTotal - l.TaxAmount, l.HsCode, l.Quantity })
            .ToListAsync(ct);

        var whIds = AllowedWarehouseIds(branchId);
        var returns = await _returns.Query()
            .Include(r => r.SalesInvoice)
            .Include(r => r.Lines)
            .Where(r => !r.IsDeleted && r.ReturnDate >= from && r.ReturnDate <= to)
            .ToListAsync(ct);
        if (whIds is not null)
            returns = returns.Where(r => r.SalesInvoice?.WarehouseId is int wid && whIds.Contains(wid)).ToList();

        var returnTax = returns.Sum(r => r.TaxAmount);
        var byRate = lines.GroupBy(l => l.TaxRate)
            .Select(g => new TaxRateBucketDto(g.Key, g.Sum(x => x.Taxable), g.Sum(x => x.TaxAmount)))
            .OrderBy(b => b.TaxRate).ToList();

        IReadOnlyList<TaxHsRowDto> byHs = includeHs
            ? lines.Where(l => !string.IsNullOrWhiteSpace(l.HsCode))
                .GroupBy(l => l.HsCode!)
                .Select(g => new TaxHsRowDto(g.Key, g.Sum(x => x.Taxable), g.Sum(x => x.TaxAmount), g.Sum(x => x.Quantity)))
                .OrderByDescending(r => r.TaxAmount).ToList()
            : Array.Empty<TaxHsRowDto>();

        var outputTax = taxEnabled ? lines.Sum(l => l.TaxAmount) : 0;
        return Result<TaxPeriodSummaryDto>.Success(new TaxPeriodSummaryDto(
            from, to, branchId, taxEnabled, includeHs,
            taxEnabled ? lines.Sum(l => l.Taxable) : 0,
            outputTax, taxEnabled ? returnTax : 0, outputTax - (taxEnabled ? returnTax : 0),
            byRate, byHs));
    }

    public async Task<byte[]> ExportTaxPeriodSummaryAsync(
        DateTime from, DateTime to, int? branchId = null, CancellationToken ct = default)
    {
        var result = await GetTaxPeriodSummaryAsync(from, to, branchId, ct);
        if (!result.Succeeded || result.Data is null)
            return Encoding.UTF8.GetBytes(result.Error ?? "Error");

        var d = result.Data;
        using var wb = new XLWorkbook();
        WriteSheet(wb.Worksheets.Add("Summary"), ["Metric", "Value"],
        [
            new object[] { "Taxable sales", d.TaxableSales },
            new object[] { "Output tax", d.OutputTax },
            new object[] { "Return tax", d.ReturnTax },
            new object[] { "Net tax", d.NetTax }
        ]);
        WriteSheet(wb.Worksheets.Add("By rate"), ["Rate %", "Taxable", "Tax"],
            d.ByRate.Select(r => new object[] { r.TaxRate, r.TaxableAmount, r.TaxAmount }));
        if (d.IncludeHs)
            WriteSheet(wb.Worksheets.Add("By HS"), ["HS code", "Taxable", "Tax", "Qty"],
                d.ByHsCode.Select(r => new object[] { r.HsCode, r.TaxableAmount, r.TaxAmount, r.Quantity }));

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<Result<FbrRegisterReportDto>> GetFbrRegisterAsync(
        DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        if (!await _features.ModuleEnabledAsync(ConfigKeys.ModSalesFbr, ct) &&
            !await _features.BehaviorEnabledAsync(ConfigKeys.BehFbrEnabled, ct))
            return Result<FbrRegisterReportDto>.Failure("FBR module is not enabled.");

        var q = _fbr.Query().Include(f => f.SalesInvoice).Where(f => !f.IsDeleted);
        if (from is DateTime f0) q = q.Where(f => f.SubmittedAt >= f0);
        if (to is DateTime t0) q = q.Where(f => f.SubmittedAt <= t0.Date.AddDays(1).AddTicks(-1));

        var list = await q.OrderByDescending(f => f.SubmittedAt).Take(1000).ToListAsync(ct);
        var rows = list.Select(f => new FbrRegisterRowDto(
            f.Id, f.SalesInvoiceId, f.SalesInvoice?.InvoiceNumber, f.FbrInvoiceNumber,
            f.Status.ToString(), f.SubmittedAt, f.ErrorMessage)).ToList();

        return Result<FbrRegisterReportDto>.Success(new FbrRegisterReportDto(from, to, rows));
    }

    public async Task<byte[]> ExportFbrRegisterAsync(
        DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var result = await GetFbrRegisterAsync(from, to, ct);
        if (!result.Succeeded || result.Data is null)
            return Encoding.UTF8.GetBytes(result.Error ?? "Error");

        return ToWorkbook("FBR Register",
            ["Id", "Invoice Id", "Invoice #", "FBR #", "Status", "Submitted", "Error"],
            result.Data.Rows.Select(r => new object[]
            {
                r.Id, r.SalesInvoiceId, r.InvoiceNumber ?? "", r.FbrInvoiceNumber ?? "",
                r.Status, r.SubmittedAt, r.ErrorMessage ?? ""
            }));
    }

    public async Task<Result<StockAgingReportDto>> GetStockAgingAsync(
        DateTime? asOfDate = null, int? branchId = null, CancellationToken ct = default)
    {
        var deny = DenyBranch(branchId);
        if (!deny.Succeeded) return Result<StockAgingReportDto>.Failure(deny.Error!);

        var asOf = (asOfDate ?? DateTime.UtcNow).Date;
        var whIds = AllowedWarehouseIds(branchId);
        var note =
            "Best-effort aging from StockBatch.ReceivedDate when batches exist; otherwise last inbound movement or inventory CreatedAt.";

        var batches = await _batches.Query()
            .Include(b => b.InventoryItem).ThenInclude(i => i.Product)
            .Include(b => b.InventoryItem).ThenInclude(i => i.Warehouse)
            .Where(b => !b.IsDeleted && b.QuantityRemaining > 0)
            .ToListAsync(ct);

        if (whIds is not null)
            batches = batches.Where(b => whIds.Contains(b.InventoryItem.WarehouseId)).ToList();

        var rows = new List<StockAgingReportRowDto>();
        if (batches.Count > 0)
        {
            foreach (var b in batches)
            {
                var ageDate = b.ReceivedDate.Date;
                var days = Math.Max(0, (asOf - ageDate).Days);
                rows.Add(new StockAgingReportRowDto(
                    b.InventoryItem.ProductId, b.InventoryItem.Product.Sku, b.InventoryItem.Product.Name,
                    b.InventoryItem.Warehouse.Name, b.BatchNumber, ageDate, days,
                    b.QuantityRemaining, b.UnitCost, b.QuantityRemaining * b.UnitCost, AgeBucket(days)));
            }
        }
        else
        {
            var items = await _inventory.Query()
                .Include(i => i.Product)
                .Include(i => i.Warehouse)
                .Where(i => !i.IsDeleted && i.QuantityOnHand > 0)
                .ToListAsync(ct);
            if (whIds is not null)
                items = items.Where(i => whIds.Contains(i.WarehouseId)).ToList();

            var itemIds = items.Select(i => i.Id).ToList();
            var lastIn = await _movements.Query()
                .Where(m => !m.IsDeleted && itemIds.Contains(m.InventoryItemId) && m.Quantity > 0)
                .GroupBy(m => m.InventoryItemId)
                .Select(g => new { InventoryItemId = g.Key, Last = g.Max(x => x.MovementDate) })
                .ToListAsync(ct);
            var lastMap = lastIn.ToDictionary(x => x.InventoryItemId, x => x.Last);

            foreach (var i in items)
            {
                var ageDate = lastMap.TryGetValue(i.Id, out var d) ? d.Date : i.CreatedAt.Date;
                var days = Math.Max(0, (asOf - ageDate).Days);
                rows.Add(new StockAgingReportRowDto(
                    i.ProductId, i.Product.Sku, i.Product.Name, i.Warehouse.Name, null,
                    ageDate, days, i.QuantityOnHand, i.AverageCost,
                    i.QuantityOnHand * i.AverageCost, AgeBucket(days)));
            }
        }

        return Result<StockAgingReportDto>.Success(new StockAgingReportDto(
            asOf, note, rows.OrderByDescending(r => r.AgeDays).ToList()));
    }

    private static string AgeBucket(int days) => days switch
    {
        <= 30 => "0-30",
        <= 60 => "31-60",
        <= 90 => "61-90",
        <= 180 => "91-180",
        _ => "180+"
    };

    public async Task<byte[]> ExportStockAgingAsync(
        DateTime? asOfDate = null, int? branchId = null, CancellationToken ct = default)
    {
        var result = await GetStockAgingAsync(asOfDate, branchId, ct);
        if (!result.Succeeded || result.Data is null)
            return Encoding.UTF8.GetBytes(result.Error ?? "Error");

        return ToWorkbook("Stock Aging",
            ["SKU", "Product", "Warehouse", "Batch", "Age date", "Days", "Bucket", "Qty", "Unit cost", "Value"],
            result.Data.Rows.Select(r => new object[]
            {
                r.Sku, r.ProductName, r.WarehouseName, r.BatchNumber ?? "", r.AgeDate,
                r.AgeDays, r.Bucket, r.Quantity, r.UnitCost, r.Value
            }));
    }

    public async Task<Result<SkuMarginReportDto>> GetSkuMarginAsync(
        DateTime from, DateTime to, int? branchId = null, CancellationToken ct = default)
    {
        var deny = DenyBranch(branchId);
        if (!deny.Succeeded) return Result<SkuMarginReportDto>.Failure(deny.Error!);

        var lines = await InvoicesInScope(from, to, branchId)
            .SelectMany(i => i.Lines.Where(l => !l.IsDeleted))
            .GroupBy(l => new { l.ProductId, l.ProductName, l.Sku })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.ProductName,
                g.Key.Sku,
                Qty = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.LineTotal),
                Cost = g.Sum(x => x.Quantity * (x.UnitCost > 0
                    ? x.UnitCost
                    : (x.Product.CostPrice > 0 ? x.Product.CostPrice : x.Product.PurchasePrice)))
            })
            .ToListAsync(ct);

        var rows = lines.Select(l =>
        {
            var profit = l.Revenue - l.Cost;
            return new SkuMarginRowDto(l.ProductId, l.Sku, l.ProductName, l.Qty, l.Revenue, l.Cost, profit,
                l.Revenue > 0 ? profit / l.Revenue * 100m : 0);
        }).OrderByDescending(r => r.Profit).ToList();

        return Result<SkuMarginReportDto>.Success(new SkuMarginReportDto(from, to, branchId, rows));
    }

    public async Task<byte[]> ExportSkuMarginAsync(
        DateTime from, DateTime to, int? branchId = null, CancellationToken ct = default)
    {
        var result = await GetSkuMarginAsync(from, to, branchId, ct);
        if (!result.Succeeded || result.Data is null)
            return Encoding.UTF8.GetBytes(result.Error ?? "Error");

        return ToWorkbook("SKU Margin",
            ["SKU", "Product", "Qty", "Revenue", "COGS", "Profit", "GM%"],
            result.Data.Rows.Select(r => new object[]
            {
                r.Sku, r.ProductName, r.QtySold, r.Revenue, r.Cost, r.Profit, r.GrossMarginPercent
            }));
    }

    /// <inheritdoc />
    public byte[] ExportClosedShiftsArchive(IReadOnlyList<ClosedShiftListItemDto> shifts)
    {
        return ToWorkbook("Z Archive",
            ["Shift #", "Cashier", "BranchId", "Till", "Opened", "Closed", "Opening", "Closing", "Expected", "Variance", "Status"],
            shifts.Select(s => new object[]
            {
                s.ShiftNumber,
                s.UserName,
                s.BranchId?.ToString() ?? "",
                s.TillCode ?? s.TillId?.ToString() ?? "",
                s.OpenedAt,
                s.ClosedAt ?? (object)"",
                s.OpeningFloat,
                s.ClosingFloat,
                s.ExpectedCash,
                s.CashVariance,
                s.Status
            }));
    }

    private static byte[] ToWorkbook(string sheetName, string[] headers, IEnumerable<object[]> rows)
    {
        using var wb = new XLWorkbook();
        WriteSheet(wb.Worksheets.Add(sheetName), headers, rows);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void WriteSheet(IXLWorksheet sheet, string[] headers, IEnumerable<object[]> rows)
    {
        for (var c = 0; c < headers.Length; c++)
            sheet.Cell(1, c + 1).Value = headers[c];
        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Length; c++)
            {
                var v = row[c];
                if (v is null) sheet.Cell(r, c + 1).Value = "";
                else if (v is DateTime dt) sheet.Cell(r, c + 1).Value = dt;
                else if (v is decimal dec) sheet.Cell(r, c + 1).Value = dec;
                else if (v is int i) sheet.Cell(r, c + 1).Value = i;
                else if (v is double d) sheet.Cell(r, c + 1).Value = d;
                else sheet.Cell(r, c + 1).Value = v.ToString();
            }
            r++;
        }
    }
}
