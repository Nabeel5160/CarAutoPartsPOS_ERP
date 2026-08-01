using System.Globalization;
using System.Text.RegularExpressions;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Enterprise;

public sealed class EnterpriseSalesService : IEnterpriseSalesService
{
    private static readonly Regex SourceQuoteMarker = new(
        @"\[SourceQuote:(\d+):([^\]]+)\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IEnterpriseDb _db;
    private readonly ICurrentCompanyContext _company;
    private readonly IGlPostingService _gl;
    private readonly ICurrentUserService _currentUser;

    public EnterpriseSalesService(
        IEnterpriseDb db,
        ICurrentCompanyContext company,
        IGlPostingService gl,
        ICurrentUserService currentUser)
    {
        _db = db;
        _company = company;
        _gl = gl;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<SalesQuotationDto>> GetQuotationsAsync(QuerySpec? query = null, CancellationToken ct = default)
    {
        EnsureCompanyOrThrow();
        query ??= new QuerySpec();

        var baseQ = _db.SalesQuotations.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            baseQ = baseQ.Where(q => q.QuotationNumber.Contains(s));
        }

        var total = await baseQ.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? QueryLimits.DefaultPageSize : query.PageSize, 1, QueryLimits.MaxPageSize);

        var items = await baseQ
            .Include(q => q.Lines)
            .OrderByDescending(q => q.QuotationDate)
            .ThenByDescending(q => q.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            
            .ToListAsync(ct);

        var orders = await _db.SalesOrders
            .AsNoTracking()
            .Where(o => !o.IsDeleted && o.Notes != null && o.Notes.Contains("[SourceQuote:"))
            .Select(o => new { o.Id, o.OrderNumber, o.Notes })
            .ToListAsync(ct);

        var orderByQuoteId = new Dictionary<int, (int Id, string Number)>();
        foreach (var o in orders)
        {
            var parsed = TryParseSourceQuote(o.Notes);
            if (parsed is { } p && !orderByQuoteId.ContainsKey(p.QuotationId))
                orderByQuoteId[p.QuotationId] = (o.Id, o.OrderNumber);
        }

        var result = new List<SalesQuotationDto>(items.Count);
        foreach (var q in items)
        {
            orderByQuoteId.TryGetValue(q.Id, out var linked);
            var lines = new List<SalesQuotationLineDto>();
            foreach (var l in q.Lines)
            {
                var price = await ResolvePriceInternalAsync(l.ProductId, q.CustomerId, l.Quantity, ct);
                var (listName, source) = DescribePriceSource(l.UnitPrice, price);
                lines.Add(new SalesQuotationLineDto(l.Id, l.ProductId, l.Quantity, l.UnitPrice, l.LineTotal, listName, source));
            }

            result.Add(new SalesQuotationDto(
                q.Id, q.QuotationNumber, q.CustomerId, q.QuotationDate, q.ValidUntil, q.Status, q.GrandTotal, q.Notes,
                lines, linked.Id == 0 ? null : linked.Id, string.IsNullOrEmpty(linked.Number) ? null : linked.Number));
        }

        return new PagedResult<SalesQuotationDto>
        {
            Items = result,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<DeliveryNoteDto>> GetDeliveriesAsync(QuerySpec? query = null, CancellationToken ct = default)
    {
        EnsureCompanyOrThrow();
        query ??= new QuerySpec();

        var baseQ = _db.DeliveryNotes.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            baseQ = baseQ.Where(d => d.DeliveryNumber.Contains(s));
        }

        if (query.Filters.TryGetValue("salesOrderId", out var soObj) && soObj is int soFilter)
            baseQ = baseQ.Where(d => d.SalesOrderId == soFilter);

        var total = await baseQ.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? QueryLimits.DefaultPageSize : query.PageSize, 1, QueryLimits.MaxPageSize);

        var items = await baseQ
            .Include(d => d.Lines)
            .OrderByDescending(d => d.DeliveryDate)
            .ThenByDescending(d => d.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            
            .ToListAsync(ct);

        var soIds = items.Where(d => d.SalesOrderId.HasValue).Select(d => d.SalesOrderId!.Value).Distinct().ToList();
        var orders = await _db.SalesOrders.AsNoTracking()
            .Where(o => soIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.OrderNumber, ct);
        var invoiceRows = await _db.SalesInvoices.AsNoTracking()
            .Where(i => !i.IsDeleted && i.SalesOrderId != null && soIds.Contains(i.SalesOrderId.Value))
            .OrderByDescending(i => i.Id)
            .Select(i => new { i.SalesOrderId, i.Id, i.InvoiceNumber })
            .ToListAsync(ct);
        var invoiceBySo = invoiceRows
            .GroupBy(x => x.SalesOrderId!.Value)
            .ToDictionary(g => g.Key, g => (g.First().Id, g.First().InvoiceNumber));

        var mapped = items.Select(d =>
        {
            string? soNumber = null;
            int? invId = null;
            string? invNumber = null;
            if (d.SalesOrderId is int soId)
            {
                orders.TryGetValue(soId, out soNumber);
                if (invoiceBySo.TryGetValue(soId, out var inv))
                {
                    invId = inv.Id;
                    invNumber = inv.InvoiceNumber;
                }
            }

            return MapDeliveryNote(d, soNumber, invId, invNumber);
        }).ToList();

        return new PagedResult<DeliveryNoteDto>
        {
            Items = mapped,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<WholesaleSalesOrderDto>> GetWholesaleSalesOrdersAsync(QuerySpec? query = null, CancellationToken ct = default)
    {
        EnsureCompanyOrThrow();
        query ??= new QuerySpec();

        var baseQ = _db.SalesOrders.AsNoTracking().Where(o => !o.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            baseQ = baseQ.Where(o => o.OrderNumber.Contains(s)
                || (o.Customer != null && o.Customer.Name.Contains(s)));
        }

        var total = await baseQ.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? QueryLimits.DefaultPageSize : query.PageSize, 1, QueryLimits.MaxPageSize);

        var orders = await baseQ
            .Include(o => o.Customer)
            .Include(o => o.Lines)
            .OrderByDescending(o => o.OrderDate)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            
            .ToListAsync(ct);

        var soIds = orders.Select(o => o.Id).ToList();
        var deliveries = await _db.DeliveryNotes.AsNoTracking()
            .Where(d => d.SalesOrderId != null && soIds.Contains(d.SalesOrderId.Value))
            .OrderByDescending(d => d.Id)
            .ToListAsync(ct);
        var deliveryBySo = deliveries
            .GroupBy(d => d.SalesOrderId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var invoices = await _db.SalesInvoices.AsNoTracking()
            .Where(i => !i.IsDeleted && i.SalesOrderId != null && soIds.Contains(i.SalesOrderId.Value))
            .OrderByDescending(i => i.Id)
            .ToListAsync(ct);
        var invoiceBySo = invoices
            .GroupBy(i => i.SalesOrderId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var result = new List<WholesaleSalesOrderDto>(orders.Count);
        foreach (var o in orders)
        {
            var quote = TryParseSourceQuote(o.Notes);
            deliveryBySo.TryGetValue(o.Id, out var dn);
            invoiceBySo.TryGetValue(o.Id, out var inv);
            var lines = new List<WholesaleSalesOrderLineDto>();
            foreach (var l in o.Lines)
            {
                var price = await ResolvePriceInternalAsync(l.ProductId, o.CustomerId, l.Quantity, ct);
                var (listName, source) = DescribePriceSource(l.UnitPrice, price);
                lines.Add(new WholesaleSalesOrderLineDto(
                    l.Id, l.ProductId, l.Quantity, l.UnitPrice, l.LineTotal, listName, source));
            }

            result.Add(new WholesaleSalesOrderDto(
                o.Id,
                o.OrderNumber,
                o.CustomerId,
                o.Customer?.Name,
                o.Status,
                o.OrderDate,
                o.GrandTotal,
                quote?.QuotationId,
                quote?.QuotationNumber,
                dn?.Id,
                dn?.DeliveryNumber,
                dn?.Status,
                inv?.Id,
                inv?.InvoiceNumber,
                lines));
        }

        return new PagedResult<WholesaleSalesOrderDto>
        {
            Items = result,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<PriceListDto>> GetPriceListsAsync(QuerySpec? query = null, CancellationToken ct = default)
    {
        EnsureCompanyOrThrow();
        query ??= new QuerySpec();

        var baseQ = _db.PriceLists.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            baseQ = baseQ.Where(p => p.Name.Contains(s));
        }

        var total = await baseQ.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? QueryLimits.DefaultPageSize : query.PageSize, 1, QueryLimits.MaxPageSize);

        var items = await baseQ
            .Include(p => p.Items)
            .OrderByDescending(p => p.ValidFrom)
            .ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            
            .ToListAsync(ct);

        return new PagedResult<PriceListDto>
        {
            Items = items.Select(MapPriceList).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<FbrSubmissionDto>> GetFbrSubmissionsAsync(QuerySpec? query = null, CancellationToken ct = default)
    {
        EnsureCompanyOrThrow();
        query ??= new QuerySpec();

        var baseQ = _db.FbrSubmissions.AsNoTracking().AsQueryable();
        var total = await baseQ.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? QueryLimits.DefaultPageSize : query.PageSize, 1, QueryLimits.MaxPageSize);

        var items = await baseQ
            .Include(f => f.SalesInvoice)
            .OrderByDescending(f => f.SubmittedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            
            .ToListAsync(ct);

        return new PagedResult<FbrSubmissionDto>
        {
            Items = items.Select(MapFbrSubmission).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<FbrMetricsDto> GetFbrMetricsAsync(CancellationToken ct = default)
    {
        EnsureCompanyOrThrow();

        var rows = await _db.FbrSubmissions
            .AsNoTracking()
            .GroupBy(f => f.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int CountOf(FbrSubmissionStatus s) => rows.FirstOrDefault(r => r.Status == s)?.Count ?? 0;

        var success = CountOf(FbrSubmissionStatus.Success);
        var stub = CountOf(FbrSubmissionStatus.Stub);
        var failed = CountOf(FbrSubmissionStatus.Failed);
        var pending = CountOf(FbrSubmissionStatus.Pending);
        var total = success + stub + failed + pending;
        var posted = success + stub;
        var rate = total > 0 ? Math.Round(posted * 100m / total, 1) : 100m;
        var needsRetry = failed + pending;

        return new FbrMetricsDto(success, stub, failed, pending, total, rate, needsRetry);
    }

    public async Task<Result<SalesQuotationDto>> CreateQuotationAsync(
        CreateQuotationRequest request,
        CancellationToken ct = default)
    {
        if (!EnsureCompany(out var companyId, out var error))
            return Result<SalesQuotationDto>.Failure(error!);

        if (request.Lines.Count == 0)
            return Result<SalesQuotationDto>.Failure("Quotation must have at least one line.");

        var quotationNumber = await EnterpriseDocumentNumbers.AllocateAsync(_db, "QT", ct);
        decimal grandTotal = 0;
        var lineMeta = new List<(string? ListName, string Source)>();

        var quotation = new SalesQuotation
        {
            CompanyId = companyId,
            QuotationNumber = quotationNumber,
            CustomerId = request.CustomerId,
            QuotationDate = request.QuotationDate,
            ValidUntil = request.ValidUntil,
            Notes = request.Notes,
            Status = QuotationStatus.Draft
        };

        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0)
                return Result<SalesQuotationDto>.Failure("Line quantity must be positive.");

            var resolved = await ResolvePriceInternalAsync(line.ProductId, request.CustomerId, line.Quantity, ct);
            if (resolved is null)
                return Result<SalesQuotationDto>.Failure($"Product {line.ProductId} not found.");

            var unitPrice = line.UnitPrice;
            string source;
            string? listName = resolved.PriceListName;

            if (unitPrice <= 0)
            {
                unitPrice = resolved.UnitPrice;
                source = listName is null ? "Catalog" : "PriceList";
            }
            else if (Math.Abs(unitPrice - resolved.UnitPrice) > 0.0001m)
            {
                if (!CanOverridePrice())
                    return Result<SalesQuotationDto>.Failure(
                        "Price override requires sales.price.override (or pos.price.override) permission.");
                source = "Override";
            }
            else
            {
                source = listName is null ? "Catalog" : "PriceList";
            }

            var lineTotal = line.Quantity * unitPrice;
            grandTotal += lineTotal;
            quotation.Lines.Add(new SalesQuotationLine
            {
                CompanyId = companyId,
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitPrice = unitPrice,
                LineTotal = lineTotal
            });
            lineMeta.Add((listName, source));
        }

        quotation.GrandTotal = grandTotal;
        _db.SalesQuotations.Add(quotation);
        await _db.SaveChangesAsync(ct);

        var lines = quotation.Lines.Zip(lineMeta, (l, m) =>
            new SalesQuotationLineDto(l.Id, l.ProductId, l.Quantity, l.UnitPrice, l.LineTotal, m.ListName, m.Source)).ToList();

        return Result<SalesQuotationDto>.Success(new SalesQuotationDto(
            quotation.Id, quotation.QuotationNumber, quotation.CustomerId, quotation.QuotationDate,
            quotation.ValidUntil, quotation.Status, quotation.GrandTotal, quotation.Notes, lines, null, null));
    }

    public async Task<Result<ConvertQuotationResultDto>> ConvertQuotationToSalesOrderAsync(
        int quotationId,
        CancellationToken ct = default)
    {
        if (!EnsureCompany(out _, out var error))
            return Result<ConvertQuotationResultDto>.Failure(error!);

        var quotation = await _db.SalesQuotations
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == quotationId, ct);

        if (quotation is null)
            return Result<ConvertQuotationResultDto>.Failure("Quotation not found.");

        if (quotation.Status is not (QuotationStatus.Draft or QuotationStatus.Sent))
            return Result<ConvertQuotationResultDto>.Failure(
                $"Quotation cannot be converted (status {quotation.Status}).");

        if (quotation.CustomerId is int custId)
        {
            var credit = await CheckCreditLimitAsync(custId, quotation.GrandTotal, ct);
            if (!credit.Succeeded)
                return Result<ConvertQuotationResultDto>.Failure(credit.Error ?? "Credit check failed.");
            if (credit.Data is { Approved: false })
                return Result<ConvertQuotationResultDto>.Failure(
                    FormatCreditError(credit.Data));
        }

        var orderNumber = await EnterpriseDocumentNumbers.AllocateAsync(_db, "SO", ct);
        decimal subTotal = 0;
        var marker = BuildSourceQuoteMarker(quotation.Id, quotation.QuotationNumber);
        var notes = string.IsNullOrWhiteSpace(quotation.Notes)
            ? marker
            : marker + " " + quotation.Notes;

        var order = new SalesOrder
        {
            OrderNumber = orderNumber,
            CustomerId = quotation.CustomerId,
            OrderDate = DateTime.UtcNow,
            Status = SalesOrderStatus.Confirmed,
            Notes = notes
        };

        foreach (var line in quotation.Lines)
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == line.ProductId, ct);
            var taxRate = product?.TaxRatePercent ?? 0;
            var lineSub = line.LineTotal;
            var lineTax = lineSub * taxRate / 100m;

            subTotal += lineSub;
            order.Lines.Add(new SalesOrderLine
            {
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                TaxRate = taxRate,
                LineTotal = lineSub + lineTax
            });
        }

        order.SubTotal = subTotal;
        order.TaxAmount = order.Lines.Sum(l => l.LineTotal) - subTotal;
        order.GrandTotal = order.Lines.Sum(l => l.LineTotal);

        // Re-check credit against tax-inclusive SO total when customer present
        if (order.CustomerId is int cid2 && order.GrandTotal > quotation.GrandTotal)
        {
            var credit2 = await CheckCreditLimitAsync(cid2, order.GrandTotal, ct);
            if (!credit2.Succeeded)
                return Result<ConvertQuotationResultDto>.Failure(credit2.Error ?? "Credit check failed.");
            if (credit2.Data is { Approved: false })
                return Result<ConvertQuotationResultDto>.Failure(FormatCreditError(credit2.Data));
        }

        _db.SalesOrders.Add(order);
        quotation.Status = QuotationStatus.Accepted;
        quotation.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result<ConvertQuotationResultDto>.Success(
            new ConvertQuotationResultDto(order.Id, order.OrderNumber, quotation.Id, quotation.QuotationNumber));
    }

    public async Task<Result<DeliveryNoteDto>> CreateDeliveryNoteAsync(
        CreateDeliveryNoteRequest request,
        CancellationToken ct = default)
    {
        if (!EnsureCompany(out var companyId, out var error))
            return Result<DeliveryNoteDto>.Failure(error!);

        if (request.Lines.Count == 0)
            return Result<DeliveryNoteDto>.Failure("Delivery note must have at least one line.");

        string? soNumber = null;
        if (request.SalesOrderId is int soId)
        {
            var so = await _db.SalesOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == soId && !o.IsDeleted, ct);
            if (so is null)
                return Result<DeliveryNoteDto>.Failure("Sales order not found.");
            if (so.Status is SalesOrderStatus.Cancelled or SalesOrderStatus.Invoiced)
                return Result<DeliveryNoteDto>.Failure($"Cannot deliver sales order in status {so.Status}.");
            soNumber = so.OrderNumber;
        }

        var deliveryNumber = await EnterpriseDocumentNumbers.AllocateAsync(_db, "DN", ct);
        var note = new DeliveryNote
        {
            CompanyId = companyId,
            DeliveryNumber = deliveryNumber,
            SalesOrderId = request.SalesOrderId,
            WarehouseId = request.WarehouseId,
            DeliveryDate = request.DeliveryDate,
            Status = DeliveryStatus.Draft
        };

        foreach (var line in request.Lines)
        {
            note.Lines.Add(new DeliveryNoteLine
            {
                CompanyId = companyId,
                ProductId = line.ProductId,
                QuantityOrdered = line.QuantityOrdered,
                QuantityShipped = line.QuantityShipped,
                FromLocationId = line.FromLocationId
            });
        }

        _db.DeliveryNotes.Add(note);
        await _db.SaveChangesAsync(ct);
        return Result<DeliveryNoteDto>.Success(MapDeliveryNote(note, soNumber, null, null));
    }

    public async Task<Result<DeliveryNoteDto>> CreateDeliveryFromSalesOrderAsync(
        int salesOrderId,
        CreateDeliveryFromSalesOrderRequest request,
        CancellationToken ct = default)
    {
        if (!EnsureCompany(out _, out var error))
            return Result<DeliveryNoteDto>.Failure(error!);

        var order = await _db.SalesOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == salesOrderId && !o.IsDeleted, ct);

        if (order is null)
            return Result<DeliveryNoteDto>.Failure("Sales order not found.");

        if (order.Status is not SalesOrderStatus.Confirmed)
            return Result<DeliveryNoteDto>.Failure($"Sales order must be Confirmed to create delivery (status {order.Status}).");

        if (order.Lines.Count == 0)
            return Result<DeliveryNoteDto>.Failure("Sales order has no lines.");

        var existingDn = await _db.DeliveryNotes.AsNoTracking()
            .AnyAsync(d => d.SalesOrderId == salesOrderId && d.Status != DeliveryStatus.Cancelled, ct);
        if (existingDn)
            return Result<DeliveryNoteDto>.Failure("A delivery note already exists for this sales order.");

        var warehouse = await _db.Warehouses.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId && !w.IsDeleted, ct);
        if (warehouse is null)
            return Result<DeliveryNoteDto>.Failure("Warehouse not found.");

        var lines = order.Lines.Select(l => new CreateDeliveryNoteLineRequest(l.ProductId, l.Quantity, l.Quantity)).ToList();
        return await CreateDeliveryNoteAsync(new CreateDeliveryNoteRequest(
            salesOrderId,
            request.WarehouseId,
            request.DeliveryDate ?? DateTime.UtcNow,
            lines), ct);
    }

    public async Task<Result<DeliveryNoteDto>> ConfirmDeliveryPickAsync(
        int deliveryNoteId,
        ConfirmDeliveryPickRequest? request = null,
        CancellationToken ct = default)
    {
        if (!EnsureCompany(out var companyId, out var error))
            return Result<DeliveryNoteDto>.Failure(error!);

        var note = await _db.DeliveryNotes
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == deliveryNoteId, ct);

        if (note is null)
            return Result<DeliveryNoteDto>.Failure("Delivery note not found.");

        if (note.Status != DeliveryStatus.Draft)
            return Result<DeliveryNoteDto>.Failure("Only draft deliveries can be picked.");

        if (request?.Lines is { Count: > 0 })
        {
            foreach (var pick in request.Lines)
            {
                var line = note.Lines.FirstOrDefault(l => l.Id == pick.LineId);
                if (line is null)
                    return Result<DeliveryNoteDto>.Failure($"Delivery line {pick.LineId} not found.");
                if (pick.FromLocationId.HasValue)
                    line.FromLocationId = pick.FromLocationId;
                line.IsPicked = true;
                line.UpdatedAt = DateTime.UtcNow;
            }
        }
        else
        {
            foreach (var line in note.Lines)
            {
                if (line.FromLocationId is null)
                {
                    try
                    {
                        line.FromLocationId = await LocationBalanceSync.ResolvePickLocationIdAsync(
                            _db.WarehouseLocations,
                            l => _db.WarehouseLocations.Add(l),
                            note.WarehouseId,
                            companyId,
                            null,
                            ct);
                    }
                    catch (InvalidOperationException ex)
                    {
                        return Result<DeliveryNoteDto>.Failure(ex.Message);
                    }
                }

                line.IsPicked = true;
                line.UpdatedAt = DateTime.UtcNow;
            }
        }

        if (note.Lines.Any(l => !l.IsPicked))
            return Result<DeliveryNoteDto>.Failure("Not all delivery lines are picked.");

        note.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        string? soNumber = null;
        if (note.SalesOrderId is int soId)
        {
            soNumber = await _db.SalesOrders.AsNoTracking()
                .Where(o => o.Id == soId)
                .Select(o => o.OrderNumber)
                .FirstOrDefaultAsync(ct);
        }

        return Result<DeliveryNoteDto>.Success(MapDeliveryNote(note, soNumber, null, null));
    }

    public async Task<Result<DeliveryNoteDto>> ShipDeliveryAsync(int deliveryNoteId, CancellationToken ct = default)
    {
        if (!EnsureCompany(out var companyId, out var error))
            return Result<DeliveryNoteDto>.Failure(error!);

        var note = await _db.DeliveryNotes
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == deliveryNoteId, ct);

        if (note is null)
            return Result<DeliveryNoteDto>.Failure("Delivery note not found.");

        if (note.Status != DeliveryStatus.Draft)
            return Result<DeliveryNoteDto>.Failure("Delivery note is not in draft status.");

        if (note.Lines.Count > 0 && note.Lines.Any(l => !l.IsPicked))
            return Result<DeliveryNoteDto>.Failure("Confirm pick list before shipping delivery.");

        foreach (var line in note.Lines)
        {
            if (line.QuantityShipped <= 0)
                continue;

            var item = await _db.InventoryItems
                .FirstOrDefaultAsync(i =>
                    i.ProductId == line.ProductId &&
                    i.WarehouseId == note.WarehouseId &&
                    !i.IsDeleted, ct);

            if (item is null || item.QuantityOnHand < line.QuantityShipped)
                return Result<DeliveryNoteDto>.Failure($"Insufficient stock for product {line.ProductId}.");

            var unitCost = item.AverageCost;
            item.QuantityOnHand -= line.QuantityShipped;
            item.UpdatedAt = DateTime.UtcNow;

            _db.StockMovements.Add(new StockMovement
            {
                InventoryItemId = item.Id,
                MovementType = StockMovementType.Sale,
                Quantity = -line.QuantityShipped,
                UnitCost = unitCost,
                ReferenceType = nameof(DeliveryNote),
                ReferenceId = note.Id,
                MovementDate = DateTime.UtcNow
            });

            try
            {
                var locId = await LocationBalanceSync.ResolvePickLocationIdAsync(
                    _db.WarehouseLocations,
                    l => _db.WarehouseLocations.Add(l),
                    note.WarehouseId,
                    companyId,
                    line.FromLocationId,
                    ct);
                var locErr = await LocationBalanceSync.DecreaseAsync(
                    _db.InventoryLocationBalances,
                    b => _db.InventoryLocationBalances.Add(b),
                    item.Id,
                    locId,
                    line.QuantityShipped,
                    allowNegative: false,
                    ct);
                if (locErr is not null)
                    return Result<DeliveryNoteDto>.Failure(locErr);
            }
            catch (InvalidOperationException ex)
            {
                return Result<DeliveryNoteDto>.Failure(ex.Message);
            }
        }

        note.Status = DeliveryStatus.Shipped;
        note.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        string? soNumber = null;
        if (note.SalesOrderId is int soId)
        {
            soNumber = await _db.SalesOrders.AsNoTracking()
                .Where(o => o.Id == soId)
                .Select(o => o.OrderNumber)
                .FirstOrDefaultAsync(ct);
        }

        return Result<DeliveryNoteDto>.Success(MapDeliveryNote(note, soNumber, null, null));
    }

    public async Task<Result<WholesaleInvoiceResultDto>> CreateInvoiceFromSalesOrderAsync(
        int salesOrderId,
        int? warehouseId = null,
        CancellationToken ct = default)
    {
        if (!EnsureCompany(out _, out var error))
            return Result<WholesaleInvoiceResultDto>.Failure(error!);

        var order = await _db.SalesOrders
            .Include(o => o.Lines)
            .ThenInclude(l => l.Product)
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == salesOrderId && !o.IsDeleted, ct);

        if (order is null)
            return Result<WholesaleInvoiceResultDto>.Failure("Sales order not found.");

        if (order.Status == SalesOrderStatus.Invoiced)
            return Result<WholesaleInvoiceResultDto>.Failure("Sales order is already invoiced.");

        if (order.Status == SalesOrderStatus.Cancelled)
            return Result<WholesaleInvoiceResultDto>.Failure("Cannot invoice a cancelled sales order.");

        if (order.Status != SalesOrderStatus.Confirmed)
            return Result<WholesaleInvoiceResultDto>.Failure($"Sales order must be Confirmed to invoice (status {order.Status}).");

        if (order.Lines.Count == 0)
            return Result<WholesaleInvoiceResultDto>.Failure("Sales order has no lines.");

        if (order.CustomerId is int custId)
        {
            var credit = await CheckCreditLimitAsync(custId, order.GrandTotal, ct);
            if (!credit.Succeeded)
                return Result<WholesaleInvoiceResultDto>.Failure(credit.Error ?? "Credit check failed.");
            if (credit.Data is { Approved: false })
                return Result<WholesaleInvoiceResultDto>.Failure(FormatCreditError(credit.Data));
        }

        // Only shipped/delivered notes count as stock already issued
        var linkedDelivery = await _db.DeliveryNotes
            .Where(d => d.SalesOrderId == salesOrderId
                        && (d.Status == DeliveryStatus.Shipped || d.Status == DeliveryStatus.Delivered))
            .OrderByDescending(d => d.Id)
            .FirstOrDefaultAsync(ct);

        warehouseId ??= linkedDelivery?.WarehouseId;

        return await PostWholesaleInvoiceAsync(order, warehouseId, linkedDelivery, ct);
    }

    public async Task<Result<WholesaleInvoiceResultDto>> CreateInvoiceFromDeliveryAsync(
        int deliveryNoteId,
        CancellationToken ct = default)
    {
        if (!EnsureCompany(out _, out var error))
            return Result<WholesaleInvoiceResultDto>.Failure(error!);

        var note = await _db.DeliveryNotes
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == deliveryNoteId, ct);

        if (note is null)
            return Result<WholesaleInvoiceResultDto>.Failure("Delivery note not found.");

        if (note.Status != DeliveryStatus.Shipped && note.Status != DeliveryStatus.Delivered)
            return Result<WholesaleInvoiceResultDto>.Failure("Delivery must be shipped before invoicing.");

        if (note.SalesOrderId is not int soId)
            return Result<WholesaleInvoiceResultDto>.Failure("Delivery is not linked to a sales order.");

        return await CreateInvoiceFromSalesOrderAsync(soId, note.WarehouseId, ct);
    }

    public async Task<Result<PriceLookupResultDto>> GetPriceForProductAsync(
        int productId,
        int? customerId = null,
        decimal quantity = 1,
        CancellationToken ct = default)
    {
        if (!EnsureCompany(out _, out var error))
            return Result<PriceLookupResultDto>.Failure(error!);

        var resolved = await ResolvePriceInternalAsync(productId, customerId, quantity, ct);
        if (resolved is null)
            return Result<PriceLookupResultDto>.Failure("Product not found.");

        return Result<PriceLookupResultDto>.Success(resolved);
    }

    public async Task<Result<CreditCheckResultDto>> CheckCreditLimitAsync(
        int customerId,
        decimal additionalAmount,
        CancellationToken ct = default)
    {
        if (!EnsureCompany(out _, out var error))
            return Result<CreditCheckResultDto>.Failure(error!);

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId && !c.IsDeleted, ct);
        if (customer is null)
            return Result<CreditCheckResultDto>.Failure("Customer not found.");

        var available = customer.CreditLimit - customer.Balance;
        var approved = customer.CreditLimit <= 0 || available >= additionalAmount;

        return Result<CreditCheckResultDto>.Success(new CreditCheckResultDto(
            approved,
            customer.CreditLimit,
            customer.Balance,
            Math.Max(0, available),
            approved ? null : "Credit limit exceeded."));
    }

    public async Task<Result<PriceListDto>> CreatePriceListAsync(
        CreatePriceListRequest request,
        CancellationToken ct = default)
    {
        if (!EnsureCompany(out var companyId, out var error))
            return Result<PriceListDto>.Failure(error!);

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<PriceListDto>.Failure("Price list name is required.");

        if (request.IsDefault)
        {
            var existingDefaults = await _db.PriceLists.Where(p => p.IsDefault).ToListAsync(ct);
            foreach (var existing in existingDefaults)
                existing.IsDefault = false;
        }

        var priceList = new PriceList
        {
            CompanyId = companyId,
            Name = request.Name.Trim(),
            CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? "PKR" : request.CurrencyCode.Trim(),
            ValidFrom = request.ValidFrom.Date,
            ValidTo = request.ValidTo?.Date,
            IsDefault = request.IsDefault,
            CustomerId = request.CustomerId
        };

        _db.PriceLists.Add(priceList);
        await _db.SaveChangesAsync(ct);
        return Result<PriceListDto>.Success(MapPriceList(priceList));
    }

    public async Task<Result<PriceListDto>> UpdatePriceListItemsAsync(
        int priceListId,
        UpdatePriceListItemsRequest request,
        CancellationToken ct = default)
    {
        if (!EnsureCompany(out var companyId, out var error))
            return Result<PriceListDto>.Failure(error!);

        var priceList = await _db.PriceLists
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == priceListId, ct);

        if (priceList is null)
            return Result<PriceListDto>.Failure("Price list not found.");

        foreach (var item in priceList.Items.ToList())
            _db.PriceListItems.Remove(item);

        foreach (var item in request.Items)
        {
            priceList.Items.Add(new PriceListItem
            {
                CompanyId = companyId,
                ProductId = item.ProductId,
                MinQuantity = item.MinQuantity,
                UnitPrice = item.UnitPrice
            });
        }

        priceList.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result<PriceListDto>.Success(MapPriceList(priceList));
    }

    private async Task<Result<WholesaleInvoiceResultDto>> PostWholesaleInvoiceAsync(
        SalesOrder order,
        int? warehouseId,
        DeliveryNote? linkedDelivery,
        CancellationToken ct)
    {
        var existing = await _db.SalesInvoices.AsNoTracking()
            .AnyAsync(i => !i.IsDeleted && i.SalesOrderId == order.Id, ct);
        if (existing)
            return Result<WholesaleInvoiceResultDto>.Failure("An invoice already exists for this sales order.");

        var invoiceNumber = await EnterpriseDocumentNumbers.AllocateAsync(_db, "INV", ct);
        var invoice = new SalesInvoice
        {
            InvoiceNumber = invoiceNumber,
            CustomerId = order.CustomerId,
            SalesOrderId = order.Id,
            InvoiceDate = DateTime.UtcNow,
            SubTotal = order.SubTotal,
            TaxAmount = order.TaxAmount,
            DiscountAmount = order.DiscountAmount,
            GrandTotal = order.GrandTotal,
            PaymentStatus = PaymentStatus.Pending,
            BuyerName = order.Customer?.Name,
            WarehouseId = warehouseId
        };

        // Stock already issued on ship; SO→Invoice without prior ship depletes here
        var issueStock = linkedDelivery is null && warehouseId is int;
        var pendingMovements = new List<StockMovement>();

        decimal cogsTotal = 0;
        foreach (var line in order.Lines)
        {
            var unitCost = 0m;
            if (warehouseId is int wh)
            {
                var invItem = issueStock
                    ? await _db.InventoryItems
                        .FirstOrDefaultAsync(i => i.ProductId == line.ProductId && i.WarehouseId == wh && !i.IsDeleted, ct)
                    : await _db.InventoryItems.AsNoTracking()
                        .FirstOrDefaultAsync(i => i.ProductId == line.ProductId && i.WarehouseId == wh && !i.IsDeleted, ct);

                unitCost = invItem?.AverageCost ?? line.Product?.CostPrice ?? 0;

                if (issueStock)
                {
                    if (invItem is null || invItem.QuantityOnHand < line.Quantity)
                        return Result<WholesaleInvoiceResultDto>.Failure(
                            $"Insufficient stock for product {line.ProductId}.");

                    invItem.QuantityOnHand -= line.Quantity;
                    invItem.UpdatedAt = DateTime.UtcNow;
                    var mv = new StockMovement
                    {
                        InventoryItemId = invItem.Id,
                        MovementType = StockMovementType.Sale,
                        Quantity = -line.Quantity,
                        UnitCost = unitCost,
                        ReferenceType = nameof(SalesInvoice),
                        ReferenceId = 0,
                        MovementDate = DateTime.UtcNow
                    };
                    pendingMovements.Add(mv);
                    _db.StockMovements.Add(mv);
                }
            }
            else
            {
                unitCost = line.Product?.CostPrice ?? 0;
            }

            var lineTax = line.LineTotal - (line.Quantity * line.UnitPrice);
            if (lineTax < 0) lineTax = line.Quantity * line.UnitPrice * line.TaxRate / 100m;
            cogsTotal += unitCost * line.Quantity;

            invoice.Lines.Add(new SalesInvoiceLine
            {
                ProductId = line.ProductId,
                ProductName = line.Product?.Name ?? $"Product {line.ProductId}",
                Sku = line.Product?.Sku ?? "",
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                TaxRate = line.TaxRate,
                TaxAmount = Math.Round(lineTax, 4),
                LineTotal = line.LineTotal,
                UnitCost = unitCost,
                HsCode = line.Product?.HsCode,
                UnitOfMeasure = line.Product?.Unit
            });
        }

        _db.SalesInvoices.Add(invoice);

        if (order.CustomerId is int cid)
        {
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == cid && !c.IsDeleted, ct);
            if (customer is not null)
                customer.Balance += order.GrandTotal;
        }

        order.Status = SalesOrderStatus.Invoiced;
        order.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        if (pendingMovements.Count > 0)
        {
            foreach (var mv in pendingMovements)
                mv.ReferenceId = invoice.Id;
            await _db.SaveChangesAsync(ct);
        }

        var glLines = new List<GlPostingLineRequest>
        {
            new("Receivable", invoice.GrandTotal, true, $"AR {invoice.InvoiceNumber}")
        };
        var netRevenue = Math.Max(0, invoice.SubTotal - invoice.DiscountAmount);
        if (netRevenue > 0)
            glLines.Add(new("Revenue", netRevenue, false, $"Sales {invoice.InvoiceNumber}"));
        if (invoice.TaxAmount > 0)
            glLines.Add(new("Tax", invoice.TaxAmount, false, $"Tax {invoice.InvoiceNumber}"));

        var creditSum = glLines.Where(l => !l.IsDebit).Sum(l => l.Amount);
        if (creditSum + 0.01m < invoice.GrandTotal)
            glLines.Add(new("Revenue", invoice.GrandTotal - creditSum, false, "Rounding"));

        // COGS when stock was issued (prior ship or SO→Invoice path)
        if (cogsTotal > 0 && (linkedDelivery is not null || issueStock))
        {
            glLines.Add(new("Cogs", cogsTotal, true, "COGS"));
            glLines.Add(new("Inventory", cogsTotal, false, "Inventory"));
        }

        var gl = await _gl.PostDocumentAsync(
            "SalesInvoice",
            invoice.InvoiceDate,
            invoice.InvoiceNumber,
            $"Wholesale {invoice.InvoiceNumber}",
            invoice.Id,
            glLines,
            autoPost: true,
            ct);

        if (!gl.Succeeded)
            return Result<WholesaleInvoiceResultDto>.Failure(gl.Error ?? "GL posting failed.");

        return Result<WholesaleInvoiceResultDto>.Success(new WholesaleInvoiceResultDto(
            invoice.Id,
            invoice.InvoiceNumber,
            order.Id,
            order.OrderNumber,
            linkedDelivery?.Id,
            linkedDelivery?.DeliveryNumber));
    }

    private async Task<PriceLookupResultDto?> ResolvePriceInternalAsync(
        int productId,
        int? customerId,
        decimal quantity,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow.Date;
        var priceLists = await _db.PriceLists
            .Include(p => p.Items)
            .Where(p =>
                p.ValidFrom <= now &&
                (p.ValidTo == null || p.ValidTo >= now))
            .OrderByDescending(p => p.IsDefault)
            .ThenByDescending(p => p.CustomerId == customerId)
            .ToListAsync(ct);

        foreach (var list in priceLists)
        {
            if (list.CustomerId.HasValue && list.CustomerId != customerId)
                continue;

            var item = list.Items
                .Where(i => i.ProductId == productId && i.MinQuantity <= quantity)
                .OrderByDescending(i => i.MinQuantity)
                .FirstOrDefault();

            if (item is not null)
            {
                return new PriceLookupResultDto(
                    productId,
                    item.UnitPrice,
                    list.Id,
                    list.Name);
            }
        }

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted, ct);
        if (product is null)
            return null;

        return new PriceLookupResultDto(productId, product.SalePrice, null, null);
    }

    private bool CanOverridePrice() =>
        _currentUser.HasPermission(Permissions.SalesPriceOverride)
        || _currentUser.HasPermission(Permissions.PosPriceOverride);

    private static string FormatCreditError(CreditCheckResultDto c) =>
        string.Create(CultureInfo.InvariantCulture,
            $"Credit limit exceeded. Limit {c.CreditLimit:N2}, balance {c.CurrentBalance:N2}, available {c.AvailableCredit:N2}.");

    private static string BuildSourceQuoteMarker(int quotationId, string quotationNumber) =>
        $"[SourceQuote:{quotationId}:{quotationNumber}]";

    private static (int QuotationId, string QuotationNumber)? TryParseSourceQuote(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return null;
        var m = SourceQuoteMarker.Match(notes);
        if (!m.Success)
            return null;
        return (int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), m.Groups[2].Value);
    }

    private static (string? ListName, string Source) DescribePriceSource(decimal unitPrice, PriceLookupResultDto? resolved)
    {
        if (resolved is null)
            return (null, "Manual");
        if (Math.Abs(unitPrice - resolved.UnitPrice) > 0.0001m)
            return (resolved.PriceListName, "Override");
        return (resolved.PriceListName, resolved.PriceListName is null ? "Catalog" : "PriceList");
    }

    private bool EnsureCompany(out int companyId, out string? error)
    {
        if (_company.CompanyId.HasValue)
        {
            companyId = _company.CompanyId.Value;
            error = null;
            return true;
        }

        companyId = 0;
        error = "Company context is required.";
        return false;
    }

    private void EnsureCompanyOrThrow()
    {
        if (!_company.CompanyId.HasValue)
            throw new InvalidOperationException("Company context is required.");
    }

    private static DeliveryNoteDto MapDeliveryNote(
        DeliveryNote d,
        string? salesOrderNumber,
        int? invoiceId,
        string? invoiceNumber) => new(
        d.Id,
        d.DeliveryNumber,
        d.SalesOrderId,
        salesOrderNumber,
        d.WarehouseId,
        d.DeliveryDate,
        d.Status,
        d.Lines.Select(l => new DeliveryNoteLineDto(
            l.Id, l.ProductId, l.QuantityOrdered, l.QuantityShipped, l.FromLocationId, l.IsPicked)).ToList(),
        invoiceId,
        invoiceNumber,
        d.Lines.Count > 0 && d.Lines.All(l => l.IsPicked));

    private static PriceListDto MapPriceList(PriceList p) => new(
        p.Id,
        p.Name,
        p.CurrencyCode,
        p.ValidFrom,
        p.ValidTo,
        p.IsDefault,
        p.CustomerId,
        p.Items.Select(i => new PriceListItemDto(i.Id, i.ProductId, i.MinQuantity, i.UnitPrice)).ToList());

    private static FbrSubmissionDto MapFbrSubmission(FbrSubmission f) => new(
        f.Id,
        f.SalesInvoiceId,
        f.SalesInvoice?.InvoiceNumber,
        f.FbrInvoiceNumber,
        f.Status,
        f.ErrorMessage,
        f.SubmittedAt);
}
