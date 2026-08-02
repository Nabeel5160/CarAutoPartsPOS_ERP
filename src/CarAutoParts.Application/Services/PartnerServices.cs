using AutoMapper;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Partners;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>Supplier master data and ledger.</summary>
public class SupplierService : ISupplierService
{
    private readonly IRepository<Supplier> _suppliers;
    private readonly IRepository<PurchaseOrder> _orders;
    private readonly IRepository<SupplierPayment> _payments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IValidator<SupplierDto> _validator;

    public SupplierService(
        IRepository<Supplier> suppliers,
        IRepository<PurchaseOrder> orders,
        IRepository<SupplierPayment> payments,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IValidator<SupplierDto> validator)
    {
        _suppliers = suppliers;
        _orders = orders;
        _payments = payments;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _validator = validator;
    }

    /// <inheritdoc />
    public async Task<PagedResult<SupplierDto>> GetSuppliersAsync(QuerySpec query, CancellationToken ct = default)
    {
        var q = _suppliers.Query().Where(s => !s.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(x => x.Name.Contains(s) || (x.Company != null && x.Company.Contains(s)));
        }

        q = q.OrderBy(x => x.Name);
        var paged = await q.ToPagedResultAsync(query.Page, query.PageSize, ct);

        return new PagedResult<SupplierDto>
        {
            Items = _mapper.Map<List<SupplierDto>>(paged.Items),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    /// <inheritdoc />
    public async Task<SupplierDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var entity = await _suppliers.Query()
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);
        return entity is null ? null : _mapper.Map<SupplierDetailDto>(entity);
    }

    /// <inheritdoc />
    public async Task<Result<SupplierDto>> CreateAsync(SupplierDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result<SupplierDto>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var entity = _mapper.Map<Supplier>(dto);
        _suppliers.Add(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<SupplierDto>.Success(_mapper.Map<SupplierDto>(entity));
    }

    /// <inheritdoc />
    public async Task<Result<SupplierDto>> UpdateAsync(int id, SupplierDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result<SupplierDto>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var entity = await _suppliers.GetByIdAsync(id, ct);
        if (entity is null || entity.IsDeleted)
            return Result<SupplierDto>.Failure("Supplier not found.");

        entity.Name = dto.Name.Trim();
        entity.Company = dto.Company;
        entity.City = dto.City;
        entity.Phone = dto.Phone;
        entity.Email = dto.Email;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        _suppliers.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<SupplierDto>.Success(_mapper.Map<SupplierDto>(entity));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _suppliers.GetByIdAsync(id, ct);
        if (entity is null || entity.IsDeleted)
            return Result.Failure("Supplier not found.");

        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        _suppliers.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SupplierLedgerEntryDto>> GetLedgerAsync(int supplierId, CancellationToken ct = default)
    {
        var orders = await _orders.Query()
            .Where(o => o.SupplierId == supplierId && !o.IsDeleted)
            .Select(o => new { o.OrderDate, o.OrderNumber, o.GrandTotal })
            .ToListAsync(ct);

        var payments = await _payments.Query()
            .Where(p => p.SupplierId == supplierId && !p.IsDeleted)
            .Select(p => new { p.PaymentDate, p.Reference, p.Amount })
            .ToListAsync(ct);

        var entries = orders
            .Select(o => new SupplierLedgerEntryDto(o.OrderDate, $"PO {o.OrderNumber}", o.OrderNumber, o.GrandTotal, 0, 0))
            .Concat(payments.Select(p => new SupplierLedgerEntryDto(p.PaymentDate, "Payment", p.Reference, 0, p.Amount, 0)))
            .OrderBy(e => e.Date)
            .ToList();

        decimal running = 0;
        var result = new List<SupplierLedgerEntryDto>();
        foreach (var e in entries)
        {
            running += e.Debit - e.Credit;
            result.Add(e with { Balance = running });
        }

        return result;
    }
}

/// <summary>Customer master data and ledger.</summary>
public class CustomerService : ICustomerService
{
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<Payment> _payments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IValidator<CustomerDto> _validator;

    public CustomerService(
        IRepository<Customer> customers,
        IRepository<SalesInvoice> invoices,
        IRepository<Payment> payments,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IValidator<CustomerDto> validator)
    {
        _customers = customers;
        _invoices = invoices;
        _payments = payments;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _validator = validator;
    }

    /// <inheritdoc />
    public async Task<PagedResult<CustomerDto>> GetCustomersAsync(QuerySpec query, CancellationToken ct = default)
    {
        var q = _customers.Query().Where(c => !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(x => x.Name.Contains(s) || (x.Phone != null && x.Phone.Contains(s)));
        }

        q = q.OrderBy(x => x.Name);
        var paged = await q.ToPagedResultAsync(query.Page, query.PageSize, ct);

        return new PagedResult<CustomerDto>
        {
            Items = _mapper.Map<List<CustomerDto>>(paged.Items),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    /// <inheritdoc />
    public async Task<CustomerDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var entity = await _customers.Query()
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        return entity is null ? null : _mapper.Map<CustomerDetailDto>(entity);
    }

    /// <inheritdoc />
    public async Task<Result<CustomerDto>> CreateAsync(CustomerDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result<CustomerDto>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var entity = _mapper.Map<Customer>(dto);
        _customers.Add(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<CustomerDto>.Success(_mapper.Map<CustomerDto>(entity));
    }

    /// <inheritdoc />
    public async Task<Result<CustomerDto>> UpdateAsync(int id, CustomerDto dto, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return Result<CustomerDto>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var entity = await _customers.GetByIdAsync(id, ct);
        if (entity is null || entity.IsDeleted)
            return Result<CustomerDto>.Failure("Customer not found.");

        entity.Name = dto.Name.Trim();
        entity.CustomerType = dto.CustomerType;
        entity.Phone = dto.Phone;
        entity.Email = dto.Email;
        entity.CreditLimit = dto.CreditLimit;
        entity.IsActive = dto.IsActive;
        entity.CommissionPercent = dto.CommissionPercent;
        entity.UpdatedAt = DateTime.UtcNow;
        _customers.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<CustomerDto>.Success(_mapper.Map<CustomerDto>(entity));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _customers.GetByIdAsync(id, ct);
        if (entity is null || entity.IsDeleted)
            return Result.Failure("Customer not found.");

        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        _customers.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CustomerLedgerEntryDto>> GetLedgerAsync(int customerId, CancellationToken ct = default)
    {
        var invoices = await _invoices.Query()
            .Where(i => i.CustomerId == customerId && !i.IsDeleted)
            .Select(i => new { i.InvoiceDate, i.InvoiceNumber, i.GrandTotal, i.Id })
            .ToListAsync(ct);

        var payments = await _payments.Query()
            .Where(p => !p.IsDeleted && invoices.Select(i => i.Id).Contains(p.SalesInvoiceId))
            .Select(p => new { p.PaymentDate, p.Reference, p.Amount, p.SalesInvoiceId })
            .ToListAsync(ct);

        var entries = invoices
            .Select(i => new CustomerLedgerEntryDto(i.InvoiceDate, $"Invoice {i.InvoiceNumber}", i.InvoiceNumber, i.GrandTotal, 0, 0))
            .Concat(payments.Select(p => new CustomerLedgerEntryDto(p.PaymentDate, "Payment", p.Reference, 0, p.Amount, 0)))
            .OrderBy(e => e.Date)
            .ToList();

        decimal running = 0;
        return entries.Select(e =>
        {
            running += e.Debit - e.Credit;
            return e with { Balance = running };
        }).ToList();
    }
}
