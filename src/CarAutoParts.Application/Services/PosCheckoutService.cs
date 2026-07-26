using System.Text;
using System.Text.Json;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Pos;
using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>POS checkout with multi-tender, kits, price lists, shift gate, GL, and FBR.</summary>
public class PosCheckoutService : IPosCheckoutService
{
    private readonly IRepository<Product> _products;
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<FbrSubmission> _fbrSubmissions;
    private readonly IRepository<CompanySettings> _settings;
    private readonly IRepository<Payment> _payments;
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<ProductKit> _kits;
    private readonly IRepository<CashierShift> _shifts;
    private readonly IRepository<HeldSale> _held;
    private readonly IInventoryService _inventory;
    private readonly IFbrService _fbrService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<PosCheckoutDto> _validator;
    private readonly IGlPostingService _gl;
    private readonly IFbrOutboxService _fbrOutbox;
    private readonly ICurrentCompanyContext _company;
    private readonly ICurrentUserService _currentUser;
    private readonly IEnterpriseSalesService _salesEnterprise;

    public PosCheckoutService(
        IRepository<Product> products,
        IRepository<SalesInvoice> invoices,
        IRepository<FbrSubmission> fbrSubmissions,
        IRepository<CompanySettings> settings,
        IRepository<Payment> payments,
        IRepository<Customer> customers,
        IRepository<ProductKit> kits,
        IRepository<CashierShift> shifts,
        IRepository<HeldSale> held,
        IInventoryService inventory,
        IFbrService fbrService,
        IUnitOfWork unitOfWork,
        IValidator<PosCheckoutDto> validator,
        IGlPostingService gl,
        IFbrOutboxService fbrOutbox,
        ICurrentCompanyContext company,
        ICurrentUserService currentUser,
        IEnterpriseSalesService salesEnterprise)
    {
        _products = products;
        _invoices = invoices;
        _fbrSubmissions = fbrSubmissions;
        _settings = settings;
        _payments = payments;
        _customers = customers;
        _kits = kits;
        _shifts = shifts;
        _held = held;
        _inventory = inventory;
        _fbrService = fbrService;
        _unitOfWork = unitOfWork;
        _validator = validator;
        _gl = gl;
        _fbrOutbox = fbrOutbox;
        _company = company;
        _currentUser = currentUser;
        _salesEnterprise = salesEnterprise;
    }

    public async Task<IReadOnlyList<PosProductDto>> GetPosProductsAsync(string? search, CancellationToken ct = default)
    {
        var q = _products.Query()
            .Include(p => p.InventoryItems)
            .Include(p => p.VehicleCompatibilities)
            .Where(p => !p.IsDeleted && p.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(p =>
                p.Name.Contains(s) ||
                p.Sku.Contains(s) ||
                (p.Barcode != null && p.Barcode.Contains(s)) ||
                (p.OemNumber != null && p.OemNumber.Contains(s)) ||
                (p.PartNumber != null && p.PartNumber.Contains(s)) ||
                p.VehicleCompatibilities.Any(v => v.Make.Contains(s) || v.Model.Contains(s)));
        }

        var items = await q.OrderBy(p => p.Name).Take(100).ToListAsync(ct);
        return items.Select(p => new PosProductDto(
            p.Id, p.Name, p.Sku, p.Barcode, p.SalePrice, p.TaxRatePercent, p.Unit, p.HsCode,
            p.InventoryItems.Where(i => !i.IsDeleted).Sum(i => i.QuantityOnHand - i.ReservedQuantity),
            p.OemNumber, p.PartNumber)).ToList();
    }

    public async Task<PosCheckoutResultDto> CheckoutAsync(PosCheckoutDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            throw new InvalidOperationException(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        // Authenticated cashiers must have an open shift; in-process tests without user skip the gate.
        CashierShift? shift = null;
        if (_currentUser.CurrentUser is not null)
        {
            shift = await ResolveOpenShiftAsync(dto.ShiftId, ct)
                ?? throw new InvalidOperationException("Open a cashier shift before checkout.");
        }

        if (!string.IsNullOrWhiteSpace(dto.IdempotencyKey))
        {
            var existing = await _invoices.Query()
                .Include(i => i.FbrSubmission)
                .FirstOrDefaultAsync(i => i.IdempotencyKey == dto.IdempotencyKey && !i.IsDeleted, ct);
            if (existing is not null)
            {
                return new PosCheckoutResultDto(
                    existing.Id, existing.InvoiceNumber, existing.FbrSubmission?.FbrInvoiceNumber,
                    existing.FbrSubmission?.Status is FbrSubmissionStatus.Success or FbrSubmissionStatus.Stub,
                    existing.FbrSubmission?.Status == FbrSubmissionStatus.Stub,
                    existing.FbrSubmission?.ErrorMessage,
                    existing.GrandTotal, existing.TaxAmount, existing.SubTotal,
                    existing.ChangeDue, existing.PaymentStatus.ToString());
            }
        }

        var settings = await _settings.Query().FirstOrDefaultAsync(s => !s.IsDeleted, ct) ?? new CompanySettings();
        var invoiceNumber = await GenerateInvoiceNumberAsync(settings, ct);
        SalesInvoice? invoice = null;

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            invoice = new SalesInvoice
            {
                InvoiceNumber = invoiceNumber,
                PosReference = $"POS-{DateTime.UtcNow:yyyyMMddHHmmss}",
                IdempotencyKey = string.IsNullOrWhiteSpace(dto.IdempotencyKey) ? null : dto.IdempotencyKey.Trim(),
                CustomerId = dto.CustomerId,
                InvoiceDate = DateTime.UtcNow,
                WarehouseId = dto.WarehouseId,
                PaymentStatus = PaymentStatus.Paid,
                BuyerName = dto.Buyer?.Name ?? "Walk-in Customer",
                BuyerNtnCnic = dto.Buyer?.NtnCnic,
                BuyerProvince = dto.Buyer?.Province,
                BuyerAddress = dto.Buyer?.Address,
                BuyerRegistrationType = dto.Buyer?.RegistrationType ?? "Unregistered",
                DiscountAmount = dto.DiscountAmount,
                CashierShiftId = shift?.Id
            };

            decimal subTotal = 0, taxTotal = 0, cogsTotal = 0;
            var lineItems = new List<(Product Product, PosCheckoutLineDto Line, decimal UnitPrice, decimal LineTax, decimal LineTotal)>();

            foreach (var line in dto.Lines)
            {
                var product = await _products.Query()
                    .Include(p => p.InventoryItems)
                    .FirstOrDefaultAsync(p => p.Id == line.ProductId && !p.IsDeleted && p.IsActive, innerCt)
                    ?? throw new InvalidOperationException($"Product {line.ProductId} is not available.");

                var unitPrice = await ResolveUnitPriceAsync(product, line, dto.CustomerId, innerCt);
                await EnsureStockForSaleAsync(product, dto.WarehouseId, line.Quantity, innerCt);

                var lineSub = unitPrice * line.Quantity - line.DiscountAmount;
                var lineTax = lineSub * product.TaxRatePercent / 100m;
                subTotal += lineSub;
                taxTotal += lineTax;
                lineItems.Add((product, line, unitPrice, lineTax, lineSub + lineTax));
            }

            invoice.SubTotal = subTotal;
            invoice.TaxAmount = taxTotal;
            invoice.GrandTotal = subTotal + taxTotal - dto.DiscountAmount;

            foreach (var (product, line, unitPrice, lineTax, lineTotal) in lineItems)
            {
                invoice.Lines.Add(new SalesInvoiceLine
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Sku = product.Sku,
                    Quantity = line.Quantity,
                    UnitPrice = unitPrice,
                    TaxRate = product.TaxRatePercent,
                    TaxAmount = lineTax,
                    LineTotal = lineTotal,
                    HsCode = product.HsCode,
                    UnitOfMeasure = product.Unit
                });
            }

            _invoices.Add(invoice);
            await _unitOfWork.SaveChangesAsync(innerCt);

            foreach (var (product, line, _, _, _) in lineItems)
                cogsTotal += await DeductForSaleAsync(product, dto.WarehouseId, line.Quantity, invoice.Id, innerCt);

            var tenders = BuildTenders(dto, invoice.GrandTotal);
            var (recorded, changeDue, creditAmount, status) = await ApplyTendersAsync(invoice, tenders, innerCt);
            invoice.ChangeDue = changeDue;
            invoice.PaymentStatus = status;
            await _unitOfWork.SaveChangesAsync(innerCt);

            if (_company.CompanyId.HasValue && invoice.GrandTotal > 0)
                await PostSalesGlAsync(invoice, recorded, creditAmount, cogsTotal, innerCt);

            if (dto.HeldSaleId is int heldId)
            {
                var held = await _held.Query().FirstOrDefaultAsync(h => h.Id == heldId && !h.IsDeleted, innerCt);
                if (held is not null)
                {
                    held.Status = HeldSaleStatus.Recalled;
                    held.RecalledAt = DateTime.UtcNow;
                    await _unitOfWork.SaveChangesAsync(innerCt);
                }
            }
        }, ct);

        if (invoice is null)
            throw new InvalidOperationException("Checkout produced no invoice.");

        var fbrRequest = FbrInvoiceBuilder.Build(invoice, invoice.Lines.ToList(), settings, dto.Buyer, dto.ScenarioId, dto.SaleType);
        var fbrResult = await _fbrService.PostInvoiceAsync(fbrRequest, ct);
        var submission = new FbrSubmission
        {
            SalesInvoiceId = invoice.Id,
            FbrInvoiceNumber = fbrResult.InvoiceNumber,
            Status = fbrResult.Success
                ? (fbrResult.WasStubbed ? FbrSubmissionStatus.Stub : FbrSubmissionStatus.Success)
                : FbrSubmissionStatus.Failed,
            RequestJson = fbrResult.RequestJson ?? JsonSerializer.Serialize(fbrRequest),
            ResponseJson = fbrResult.ResponseJson,
            ErrorMessage = fbrResult.Success ? null : fbrResult.Message,
            SubmittedAt = DateTime.UtcNow
        };
        _fbrSubmissions.Add(submission);
        await _unitOfWork.SaveChangesAsync(ct);
        if (!fbrResult.Success)
            _fbrOutbox.EnqueueFbrRetry(invoice.Id, submission.RequestJson);

        return new PosCheckoutResultDto(
            invoice.Id, invoice.InvoiceNumber, fbrResult.InvoiceNumber,
            fbrResult.Success, fbrResult.WasStubbed, fbrResult.Message,
            invoice.GrandTotal, invoice.TaxAmount, invoice.SubTotal,
            invoice.ChangeDue, invoice.PaymentStatus.ToString());
    }

    public async Task<string> GetReceiptHtmlAsync(int salesInvoiceId, CancellationToken ct = default)
    {
        var invoice = await _invoices.Query()
            .Include(i => i.Lines).Include(i => i.Payments).Include(i => i.Customer)
            .FirstOrDefaultAsync(i => i.Id == salesInvoiceId && !i.IsDeleted, ct)
            ?? throw new InvalidOperationException("Invoice not found.");

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'><title>").Append(invoice.InvoiceNumber)
            .Append("</title><style>body{font-family:Segoe UI,Arial,sans-serif;max-width:320px;margin:1rem auto;font-size:13px}h1{font-size:16px;margin:0 0 .5rem}table{width:100%;border-collapse:collapse}td{padding:.2rem 0}.r{text-align:right}.muted{color:#666;font-size:11px}</style></head><body>");
        sb.Append("<h1>Car Auto Parts</h1><div class='muted'>").Append(invoice.InvoiceNumber).Append(" · ")
            .Append(invoice.InvoiceDate.ToLocalTime().ToString("g")).Append("</div><div>")
            .Append(System.Net.WebUtility.HtmlEncode(invoice.BuyerName ?? invoice.Customer?.Name ?? "Walk-in"))
            .Append("</div><hr/><table>");
        foreach (var line in invoice.Lines)
        {
            sb.Append("<tr><td>").Append(System.Net.WebUtility.HtmlEncode(line.ProductName))
                .Append("<div class='muted'>").Append(line.Quantity.ToString("0.##")).Append(" × ").Append(line.UnitPrice.ToString("N2"))
                .Append("</div></td><td class='r'>").Append(line.LineTotal.ToString("N2")).Append("</td></tr>");
        }
        sb.Append("</table><hr/><table><tr><td>Subtotal</td><td class='r'>").Append(invoice.SubTotal.ToString("N2")).Append("</td></tr>");
        sb.Append("<tr><td>Tax</td><td class='r'>").Append(invoice.TaxAmount.ToString("N2")).Append("</td></tr>");
        sb.Append("<tr><td><strong>Total</strong></td><td class='r'><strong>").Append(invoice.GrandTotal.ToString("N2")).Append("</strong></td></tr>");
        foreach (var p in invoice.Payments)
            sb.Append("<tr><td>").Append(System.Net.WebUtility.HtmlEncode(p.PaymentMethod)).Append("</td><td class='r'>").Append(p.Amount.ToString("N2")).Append("</td></tr>");
        if (invoice.ChangeDue > 0)
            sb.Append("<tr><td>Change</td><td class='r'>").Append(invoice.ChangeDue.ToString("N2")).Append("</td></tr>");
        sb.Append("</table><p class='muted'>Thank you</p><script>window.onload=()=>window.print()</script></body></html>");
        return sb.ToString();
    }

    private static List<PosTenderDto> BuildTenders(PosCheckoutDto dto, decimal grandTotal)
    {
        if (dto.Tenders is { Count: > 0 })
            return dto.Tenders.Where(t => t.Amount > 0).Select(t => new PosTenderDto(t.Method.Trim(), t.Amount)).ToList();

        var method = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? "Cash" : dto.PaymentMethod.Trim();
        return [new PosTenderDto(method, grandTotal)];
    }

    private async Task<(List<PosTenderDto> Recorded, decimal ChangeDue, decimal CreditAmount, PaymentStatus Status)> ApplyTendersAsync(
        SalesInvoice invoice, List<PosTenderDto> tenders, CancellationToken ct)
    {
        var credit = tenders.Where(t => IsCredit(t.Method)).Sum(t => t.Amount);
        var nonCredit = tenders.Where(t => !IsCredit(t.Method)).ToList();
        var nonCreditSum = nonCredit.Sum(t => t.Amount);

        if (credit > 0 && invoice.CustomerId is null)
            throw new InvalidOperationException("Customer is required for credit tender.");

        if (credit > 0 && invoice.CustomerId is int custId)
        {
            var check = await _salesEnterprise.CheckCreditLimitAsync(custId, credit, ct);
            if (!check.Succeeded || check.Data is null || !check.Data.Approved)
                throw new InvalidOperationException(check.Data?.Message ?? check.Error ?? "Credit limit exceeded.");
        }

        if (nonCreditSum + credit + 0.01m < invoice.GrandTotal)
            throw new InvalidOperationException($"Tenders do not cover total {invoice.GrandTotal:N2}.");

        var dueFromNonCredit = Math.Max(0, invoice.GrandTotal - credit);
        var changeDue = Math.Max(0, nonCreditSum - dueFromNonCredit);

        var recorded = new List<PosTenderDto>();
        var remainingChange = changeDue;
        foreach (var t in nonCredit)
        {
            var amount = t.Amount;
            if (remainingChange > 0 && t.Method.Contains("cash", StringComparison.OrdinalIgnoreCase))
            {
                var reduce = Math.Min(remainingChange, amount);
                amount -= reduce;
                remainingChange -= reduce;
            }
            if (amount > 0)
            {
                recorded.Add(new PosTenderDto(t.Method, amount));
                _payments.Add(new Payment
                {
                    SalesInvoiceId = invoice.Id,
                    Amount = amount,
                    PaymentMethod = t.Method,
                    PaymentDate = DateTime.UtcNow
                });
            }
        }

        var ar = Math.Min(credit, invoice.GrandTotal - recorded.Sum(r => r.Amount));
        if (ar > 0 && invoice.CustomerId is int cid)
        {
            var customer = await _customers.Query().FirstOrDefaultAsync(c => c.Id == cid && !c.IsDeleted, ct);
            if (customer is not null)
                customer.Balance += ar;
        }

        var status = ar <= 0 ? PaymentStatus.Paid
            : recorded.Sum(r => r.Amount) > 0 ? PaymentStatus.Partial
            : PaymentStatus.Pending;

        return (recorded, changeDue, ar, status);
    }

    private async Task PostSalesGlAsync(SalesInvoice invoice, List<PosTenderDto> recorded, decimal creditAmount, decimal cogsTotal, CancellationToken ct)
    {
        var netRevenue = Math.Max(0, invoice.SubTotal - invoice.DiscountAmount);
        var glLines = new List<GlPostingLineRequest>();

        foreach (var t in recorded)
        {
            var key = t.Method.Contains("bank", StringComparison.OrdinalIgnoreCase) ||
                      t.Method.Contains("card", StringComparison.OrdinalIgnoreCase) ||
                      t.Method.Contains("transfer", StringComparison.OrdinalIgnoreCase)
                ? "Bank" : "Cash";
            glLines.Add(new GlPostingLineRequest(key, t.Amount, true, t.Method));
        }

        if (creditAmount > 0)
            glLines.Add(new GlPostingLineRequest("Receivable", creditAmount, true, "AR"));

        var debitSum = glLines.Where(l => l.IsDebit).Sum(l => l.Amount);
        if (debitSum + 0.01m < invoice.GrandTotal)
            glLines.Add(new GlPostingLineRequest("Cash", invoice.GrandTotal - debitSum, true, "Rounding"));

        if (netRevenue > 0)
            glLines.Add(new GlPostingLineRequest("Revenue", netRevenue, false, "Sales"));
        if (invoice.TaxAmount > 0)
            glLines.Add(new GlPostingLineRequest("Tax", invoice.TaxAmount, false, "Sales tax"));

        var creditSum = glLines.Where(l => !l.IsDebit).Sum(l => l.Amount);
        if (creditSum < invoice.GrandTotal)
            glLines.Add(new GlPostingLineRequest("Revenue", invoice.GrandTotal - creditSum, false, "Rounding"));

        if (cogsTotal > 0)
        {
            glLines.Add(new GlPostingLineRequest("Cogs", cogsTotal, true, "COGS"));
            glLines.Add(new GlPostingLineRequest("Inventory", cogsTotal, false, "Inventory"));
        }

        var gl = await _gl.PostDocumentAsync(
            "SalesInvoice", invoice.InvoiceDate, invoice.InvoiceNumber, $"POS {invoice.InvoiceNumber}",
            invoice.Id, glLines, autoPost: true, ct);
        if (!gl.Succeeded)
            throw new InvalidOperationException(gl.Error ?? "GL posting failed.");
    }

    private async Task<CashierShift?> ResolveOpenShiftAsync(int? shiftId, CancellationToken ct)
    {
        var userId = _currentUser.CurrentUser!.Id;
        if (shiftId is int id)
            return await _shifts.Query().FirstOrDefaultAsync(
                s => s.Id == id && s.UserId == userId && s.Status == CashierShiftStatus.Open && !s.IsDeleted, ct);
        return await _shifts.Query().FirstOrDefaultAsync(
            s => s.UserId == userId && s.Status == CashierShiftStatus.Open && !s.IsDeleted, ct);
    }

    private async Task<decimal> ResolveUnitPriceAsync(Product product, PosCheckoutLineDto line, int? customerId, CancellationToken ct)
    {
        if (line.UnitPriceOverride is decimal ov)
        {
            if (_currentUser.CurrentUser is not null && !_currentUser.HasPermission(Permissions.PosPriceOverride))
                throw new InvalidOperationException("Price override permission required.");
            return ov;
        }

        var lookup = await _salesEnterprise.GetPriceForProductAsync(product.Id, customerId, line.Quantity, ct);
        if (lookup.Succeeded && lookup.Data is not null)
            return lookup.Data.UnitPrice;
        return product.SalePrice;
    }

    private async Task EnsureStockForSaleAsync(Product product, int warehouseId, decimal qty, CancellationToken ct)
    {
        var kit = await _kits.Query().Include(k => k.Components)
            .FirstOrDefaultAsync(k => k.ParentProductId == product.Id && !k.IsDeleted, ct);
        if (kit is { Components.Count: > 0 })
        {
            foreach (var c in kit.Components.Where(x => !x.IsDeleted))
            {
                var need = c.Quantity * qty;
                var available = await _products.Query().Where(p => p.Id == c.ComponentProductId)
                    .SelectMany(p => p.InventoryItems)
                    .Where(i => !i.IsDeleted && i.WarehouseId == warehouseId)
                    .SumAsync(i => i.QuantityOnHand - i.ReservedQuantity, ct);
                if (available < need)
                    throw new InvalidOperationException($"Insufficient kit component stock (product {c.ComponentProductId}).");
            }
            return;
        }

        var onHand = product.InventoryItems.Where(i => !i.IsDeleted && i.WarehouseId == warehouseId)
            .Sum(i => i.QuantityOnHand - i.ReservedQuantity);
        if (onHand < qty)
            throw new InvalidOperationException($"Insufficient stock for {product.Name}.");
    }

    private async Task<decimal> DeductForSaleAsync(Product product, int warehouseId, decimal qty, int invoiceId, CancellationToken ct)
    {
        var kit = await _kits.Query().Include(k => k.Components)
            .FirstOrDefaultAsync(k => k.ParentProductId == product.Id && !k.IsDeleted, ct);
        if (kit is { Components.Count: > 0 })
        {
            decimal cogs = 0;
            foreach (var c in kit.Components.Where(x => !x.IsDeleted))
            {
                var need = c.Quantity * qty;
                var comp = await _products.Query().FirstAsync(p => p.Id == c.ComponentProductId, ct);
                var deduct = await _inventory.DeductStockAsync(comp.Id, warehouseId, need, nameof(SalesInvoice), invoiceId, ct);
                if (!deduct.Succeeded)
                    throw new InvalidOperationException(deduct.Error ?? "Kit component stock deduction failed.");
                cogs += (comp.CostPrice > 0 ? comp.CostPrice : comp.PurchasePrice) * need;
            }
            return cogs;
        }

        var result = await _inventory.DeductStockAsync(product.Id, warehouseId, qty, nameof(SalesInvoice), invoiceId, ct);
        if (!result.Succeeded)
            throw new InvalidOperationException(result.Error ?? "Stock deduction failed.");
        return (product.CostPrice > 0 ? product.CostPrice : product.PurchasePrice) * qty;
    }

    private static bool IsCredit(string method) =>
        method.Contains("credit", StringComparison.OrdinalIgnoreCase) ||
        method.Contains("account", StringComparison.OrdinalIgnoreCase) ||
        method.Equals("AR", StringComparison.OrdinalIgnoreCase);

    private async Task<string> GenerateInvoiceNumberAsync(CompanySettings settings, CancellationToken ct)
    {
        var prefix = settings.InvoicePrefix ?? "INV";
        var count = await _invoices.Query().CountAsync(ct);
        return $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D5}";
    }
}
