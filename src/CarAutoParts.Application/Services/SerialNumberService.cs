using AutoMapper;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Inventory;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>Serial number tracking and registration.</summary>
public class SerialNumberService : ISerialNumberService
{
    private readonly IRepository<SerialNumber> _serials;
    private readonly IRepository<SerialNumberHistory> _history;
    private readonly IRepository<Product> _products;
    private readonly IRepository<Warehouse> _warehouses;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public SerialNumberService(
        IRepository<SerialNumber> serials,
        IRepository<SerialNumberHistory> history,
        IRepository<Product> products,
        IRepository<Warehouse> warehouses,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _serials = serials;
        _history = history;
        _products = products;
        _warehouses = warehouses;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<PagedResult<SerialNumberDto>> GetSerialNumbersAsync(QuerySpec query, CancellationToken ct = default)
    {
        var q = _serials.Query()
            .Include(s => s.Product)
            .Include(s => s.CurrentWarehouse)
            .Where(s => !s.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(x => x.Serial.Contains(s) || x.Product.Name.Contains(s));
        }

        q = q.OrderByDescending(x => x.CreatedAt);
        var paged = await q.ToPagedResultAsync(query.Page, query.PageSize, ct);

        return new PagedResult<SerialNumberDto>
        {
            Items = _mapper.Map<List<SerialNumberDto>>(paged.Items),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SerialNumberHistoryDto>> GetHistoryAsync(int serialNumberId, CancellationToken ct = default)
    {
        var items = await _history.Query()
            .Where(h => h.SerialNumberId == serialNumberId && !h.IsDeleted)
            .OrderByDescending(h => h.ActionDate)
            .ToListAsync(ct);

        return _mapper.Map<List<SerialNumberHistoryDto>>(items);
    }

    /// <inheritdoc />
    public async Task<Result> RegisterSerialAsync(int productId, string serial, int warehouseId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(serial))
            return Result.Failure("Serial number is required.");

        if (!await _products.ExistsAsync(p => p.Id == productId && !p.IsDeleted, ct))
            return Result.Failure("Product not found.");

        if (!await _warehouses.ExistsAsync(w => w.Id == warehouseId && !w.IsDeleted, ct))
            return Result.Failure("Warehouse not found.");

        if (await _serials.ExistsAsync(s => s.Serial == serial && !s.IsDeleted, ct))
            return Result.Failure("Serial number already exists.");

        var entity = new SerialNumber
        {
            ProductId = productId,
            Serial = serial.Trim(),
            CurrentWarehouseId = warehouseId,
            Status = SerialNumberStatus.Available
        };

        entity.History.Add(new SerialNumberHistory
        {
            Action = "Registered",
            ReferenceType = nameof(Warehouse),
            ReferenceId = warehouseId,
            ActionDate = DateTime.UtcNow
        });

        _serials.Add(entity);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
