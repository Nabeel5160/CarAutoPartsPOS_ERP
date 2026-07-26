using CarAutoParts.Application.Common;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Enterprise;

public sealed class EnterpriseSalesService : IEnterpriseSalesService
{
    private readonly IEnterpriseDb _db;
    private readonly ICurrentCompanyContext _company;

    public EnterpriseSalesService(IEnterpriseDb db, ICurrentCompanyContext company)
    {
        _db = db;
        _company = company;
    }

    public async Task<IReadOnlyList<SalesQuotationDto>> GetQuotationsAsync(CancellationToken ct = default)
    {
        EnsureCompanyOrThrow();

        var items = await _db.SalesQuotations
            .AsNoTracking()
            .Include(q => q.Lines)
            .OrderByDescending(q => q.QuotationDate)
            .ThenByDescending(q => q.Id)
            .ToListAsync(ct);

        return items.Select(MapQuotation).ToList();
    }

    public async Task<IReadOnlyList<DeliveryNoteDto>> GetDeliveriesAsync(CancellationToken ct = default)
    {
        EnsureCompanyOrThrow();

        var items = await _db.DeliveryNotes
            .AsNoTracking()
            .Include(d => d.Lines)
            .OrderByDescending(d => d.DeliveryDate)
            .ThenByDescending(d => d.Id)
            .ToListAsync(ct);

        return items.Select(MapDeliveryNote).ToList();
    }

    public async Task<IReadOnlyList<PriceListDto>> GetPriceListsAsync(CancellationToken ct = default)
    {
        EnsureCompanyOrThrow();

        var items = await _db.PriceLists
            .AsNoTracking()
            .Include(p => p.Items)
            .OrderByDescending(p => p.ValidFrom)
            .ThenByDescending(p => p.Id)
            .ToListAsync(ct);

        return items.Select(MapPriceList).ToList();
    }

    public async Task<IReadOnlyList<FbrSubmissionDto>> GetFbrSubmissionsAsync(CancellationToken ct = default)
    {
        EnsureCompanyOrThrow();

        var items = await _db.FbrSubmissions
            .AsNoTracking()
            .Include(f => f.SalesInvoice)
            .OrderByDescending(f => f.SubmittedAt)
            .Take(200)
            .ToListAsync(ct);

        return items.Select(MapFbrSubmission).ToList();
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
            var lineTotal = line.Quantity * line.UnitPrice;
            grandTotal += lineTotal;
            quotation.Lines.Add(new SalesQuotationLine
            {
                CompanyId = companyId,
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                LineTotal = lineTotal
            });
        }

        quotation.GrandTotal = grandTotal;
        _db.SalesQuotations.Add(quotation);
        await _db.SaveChangesAsync(ct);
        return Result<SalesQuotationDto>.Success(MapQuotation(quotation));
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

        if (quotation.Status is QuotationStatus.Cancelled or QuotationStatus.Expired)
            return Result<ConvertQuotationResultDto>.Failure("Quotation cannot be converted.");

        var orderNumber = await EnterpriseDocumentNumbers.AllocateAsync(_db, "SO", ct);
        decimal subTotal = 0;

        var order = new SalesOrder
        {
            OrderNumber = orderNumber,
            CustomerId = quotation.CustomerId,
            OrderDate = DateTime.UtcNow,
            Status = SalesOrderStatus.Confirmed,
            Notes = quotation.Notes
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

        _db.SalesOrders.Add(order);
        quotation.Status = QuotationStatus.Accepted;
        quotation.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result<ConvertQuotationResultDto>.Success(new ConvertQuotationResultDto(order.Id, order.OrderNumber, quotation.Id));
    }

    public async Task<Result<DeliveryNoteDto>> CreateDeliveryNoteAsync(
        CreateDeliveryNoteRequest request,
        CancellationToken ct = default)
    {
        if (!EnsureCompany(out var companyId, out var error))
            return Result<DeliveryNoteDto>.Failure(error!);

        if (request.Lines.Count == 0)
            return Result<DeliveryNoteDto>.Failure("Delivery note must have at least one line.");

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
                QuantityShipped = line.QuantityShipped
            });
        }

        _db.DeliveryNotes.Add(note);
        await _db.SaveChangesAsync(ct);
        return Result<DeliveryNoteDto>.Success(MapDeliveryNote(note));
    }

    public async Task<Result<DeliveryNoteDto>> ShipDeliveryAsync(int deliveryNoteId, CancellationToken ct = default)
    {
        if (!EnsureCompany(out _, out var error))
            return Result<DeliveryNoteDto>.Failure(error!);

        var note = await _db.DeliveryNotes
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == deliveryNoteId, ct);

        if (note is null)
            return Result<DeliveryNoteDto>.Failure("Delivery note not found.");

        if (note.Status != DeliveryStatus.Draft)
            return Result<DeliveryNoteDto>.Failure("Delivery note is not in draft status.");

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
        }

        note.Status = DeliveryStatus.Shipped;
        note.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result<DeliveryNoteDto>.Success(MapDeliveryNote(note));
    }

    public async Task<Result<PriceLookupResultDto>> GetPriceForProductAsync(
        int productId,
        int? customerId = null,
        decimal quantity = 1,
        CancellationToken ct = default)
    {
        if (!EnsureCompany(out _, out var error))
            return Result<PriceLookupResultDto>.Failure(error!);

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
                return Result<PriceLookupResultDto>.Success(new PriceLookupResultDto(
                    productId,
                    item.UnitPrice,
                    list.Id,
                    list.Name));
            }
        }

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted, ct);
        if (product is null)
            return Result<PriceLookupResultDto>.Failure("Product not found.");

        return Result<PriceLookupResultDto>.Success(new PriceLookupResultDto(
            productId,
            product.SalePrice,
            null,
            null));
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

    private static SalesQuotationDto MapQuotation(SalesQuotation q) => new(
        q.Id,
        q.QuotationNumber,
        q.CustomerId,
        q.QuotationDate,
        q.ValidUntil,
        q.Status,
        q.GrandTotal,
        q.Notes,
        q.Lines.Select(l => new SalesQuotationLineDto(l.Id, l.ProductId, l.Quantity, l.UnitPrice, l.LineTotal)).ToList());

    private static DeliveryNoteDto MapDeliveryNote(DeliveryNote d) => new(
        d.Id,
        d.DeliveryNumber,
        d.SalesOrderId,
        d.WarehouseId,
        d.DeliveryDate,
        d.Status,
        d.Lines.Select(l => new DeliveryNoteLineDto(l.Id, l.ProductId, l.QuantityOrdered, l.QuantityShipped)).ToList());

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
