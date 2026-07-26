using AutoMapper;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Sales;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>Sales invoice and order queries.</summary>
public class SalesService : ISalesService
{
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<SalesOrder> _orders;
    private readonly IMapper _mapper;

    public SalesService(
        IRepository<SalesInvoice> invoices,
        IRepository<SalesOrder> orders,
        IMapper mapper)
    {
        _invoices = invoices;
        _orders = orders;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<PagedResult<SalesInvoiceListDto>> GetInvoicesAsync(QuerySpec query, CancellationToken ct = default)
    {
        var q = _invoices.Query()
            .Include(i => i.Customer)
            .Include(i => i.FbrSubmission)
            .Where(i => !i.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(i => i.InvoiceNumber.Contains(s) || (i.Customer != null && i.Customer.Name.Contains(s)));
        }

        q = q.OrderByDescending(i => i.InvoiceDate);
        var paged = await q.ToPagedResultAsync(query.Page, query.PageSize, ct);

        return new PagedResult<SalesInvoiceListDto>
        {
            Items = _mapper.Map<List<SalesInvoiceListDto>>(paged.Items),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    /// <inheritdoc />
    public async Task<SalesInvoiceDetailDto?> GetInvoiceByIdAsync(int id, CancellationToken ct = default)
    {
        var invoice = await _invoices.Query()
            .Include(i => i.Customer)
            .Include(i => i.FbrSubmission)
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted, ct);

        return invoice is null ? null : _mapper.Map<SalesInvoiceDetailDto>(invoice);
    }

    /// <inheritdoc />
    public async Task<PagedResult<SalesOrderListDto>> GetOrdersAsync(QuerySpec query, CancellationToken ct = default)
    {
        var q = _orders.Query()
            .Include(o => o.Customer)
            .Where(o => !o.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(o => o.OrderNumber.Contains(s) || (o.Customer != null && o.Customer.Name.Contains(s)));
        }

        if (query.Filters.TryGetValue("Status", out var statusObj) && statusObj is SalesOrderStatus status)
            q = q.Where(o => o.Status == status);

        q = q.OrderByDescending(o => o.OrderDate);
        var paged = await q.ToPagedResultAsync(query.Page, query.PageSize, ct);

        return new PagedResult<SalesOrderListDto>
        {
            Items = _mapper.Map<List<SalesOrderListDto>>(paged.Items),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }
}
