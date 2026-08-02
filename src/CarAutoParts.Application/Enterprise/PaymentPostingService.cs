using CarAutoParts.Application.Common;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Enterprise;

public record PostCustomerReceiptRequest(
    int SalesInvoiceId,
    decimal Amount,
    string PaymentMethod,
    DateTime? PaymentDate = null,
    string? Reference = null);

public record PostSupplierPaymentRequest(
    int SupplierId,
    decimal Amount,
    string PaymentMethod,
    string? Reference = null,
    DateTime? PaymentDate = null,
    string? Notes = null,
    decimal WithholdingTaxRate = 0);

public interface IPaymentPostingService
{
    Task<Result<int>> PostCustomerReceiptAsync(PostCustomerReceiptRequest request, CancellationToken ct = default);
    Task<Result<int>> PostSupplierPaymentAsync(PostSupplierPaymentRequest request, CancellationToken ct = default);
}

public sealed class PaymentPostingService : IPaymentPostingService
{
    private readonly IEnterpriseDb _db;
    private readonly ICurrentCompanyContext _company;
    private readonly IGlPostingService _gl;

    public PaymentPostingService(IEnterpriseDb db, ICurrentCompanyContext company, IGlPostingService gl)
    {
        _db = db;
        _company = company;
        _gl = gl;
    }

    public async Task<Result<int>> PostCustomerReceiptAsync(PostCustomerReceiptRequest request, CancellationToken ct = default)
    {
        if (!_company.CompanyId.HasValue)
            return Result<int>.Failure("Company context is required.");
        if (request.Amount <= 0)
            return Result<int>.Failure("Amount must be positive.");

        var invoice = await _db.SalesInvoices
            .FirstOrDefaultAsync(i => i.Id == request.SalesInvoiceId && !i.IsDeleted, ct);
        if (invoice is null)
            return Result<int>.Failure("Sales invoice not found.");

        var cashKey = ResolveCashBankKey(request.PaymentMethod);
        var gl = await _gl.PostDocumentAsync(
            "Payment",
            request.PaymentDate ?? DateTime.UtcNow,
            invoice.InvoiceNumber,
            $"Customer receipt {invoice.InvoiceNumber}",
            invoice.Id,
            [
                new GlPostingLineRequest(cashKey, request.Amount, true, "Cash/Bank"),
                new GlPostingLineRequest("Receivable", request.Amount, false, "AR clearance")
            ],
            autoPost: true,
            ct);

        if (!gl.Succeeded)
            return Result<int>.Failure(gl.Error!);

        _db.Payments.Add(new Payment
        {
            SalesInvoiceId = invoice.Id,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            PaymentDate = request.PaymentDate ?? DateTime.UtcNow,
            Reference = request.Reference
        });

        if (invoice.CustomerId is int customerId)
        {
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId && !c.IsDeleted, ct);
            if (customer is not null)
                customer.Balance = Math.Max(0, customer.Balance - request.Amount);
        }

        await _db.SaveChangesAsync(ct);
        return Result<int>.Success(gl.Data!.JournalId);
    }

    public async Task<Result<int>> PostSupplierPaymentAsync(PostSupplierPaymentRequest request, CancellationToken ct = default)
    {
        if (!_company.CompanyId.HasValue)
            return Result<int>.Failure("Company context is required.");
        if (request.Amount <= 0)
            return Result<int>.Failure("Amount must be positive.");

        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == request.SupplierId && !s.IsDeleted, ct);
        if (supplier is null)
            return Result<int>.Failure("Supplier not found.");

        if (request.WithholdingTaxRate < 0 || request.WithholdingTaxRate > 100)
            return Result<int>.Failure("Withholding tax rate must be between 0 and 100.");

        var whtAmount = Math.Round(request.Amount * request.WithholdingTaxRate / 100m, 2);
        var netCash = request.Amount - whtAmount;
        if (netCash <= 0)
            return Result<int>.Failure("Net cash payment after withholding tax must be positive.");

        var cashKey = ResolveCashBankKey(request.PaymentMethod);
        var lines = new List<GlPostingLineRequest>
        {
            new("Payable", request.Amount, true, "AP clearance"),
            new(cashKey, netCash, false, "Cash/Bank")
        };
        if (whtAmount > 0)
            lines.Add(new GlPostingLineRequest("WithholdingTaxPayable", whtAmount, false, "Withholding tax withheld"));

        var gl = await _gl.PostDocumentAsync(
            "Payment",
            request.PaymentDate ?? DateTime.UtcNow,
            request.Reference,
            $"Supplier payment {supplier.Name}",
            supplier.Id,
            lines,
            autoPost: true,
            ct);

        if (!gl.Succeeded)
            return Result<int>.Failure(gl.Error!);

        _db.SupplierPayments.Add(new SupplierPayment
        {
            SupplierId = supplier.Id,
            Amount = request.Amount,
            PaymentDate = request.PaymentDate ?? DateTime.UtcNow,
            Reference = request.Reference,
            Notes = request.Notes,
            WithholdingTaxRate = request.WithholdingTaxRate,
            WithholdingTaxAmount = whtAmount
        });
        supplier.Balance = Math.Max(0, supplier.Balance - request.Amount);
        await _db.SaveChangesAsync(ct);
        return Result<int>.Success(gl.Data!.JournalId);
    }

    private static string ResolveCashBankKey(string? method)
    {
        var m = (method ?? "Cash").Trim();
        return m.Contains("bank", StringComparison.OrdinalIgnoreCase) ||
               m.Contains("card", StringComparison.OrdinalIgnoreCase) ||
               m.Contains("transfer", StringComparison.OrdinalIgnoreCase)
            ? "Bank"
            : "Cash";
    }
}
