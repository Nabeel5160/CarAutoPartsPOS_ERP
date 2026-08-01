using AutoMapper;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Mapping;
using CarAutoParts.Application.Services;
using CarAutoParts.Application.Validators;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Infrastructure.Data;
using CarAutoParts.Infrastructure.Repositories;
using FluentValidation;
using Moq;

namespace CarAutoParts.Application.Tests;

/// <summary>Shared constructors for tests after Phase 15 location-balance ctor changes.</summary>
internal static class TestInventoryFactory
{
    public static InventoryService Create(
        ApplicationDbContext db,
        IMapper? mapper = null,
        ICurrentCompanyContext? company = null,
        INotificationService? notifications = null)
    {
        mapper ??= new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>()).CreateMapper();
        company ??= new CurrentCompanyContext();
        if (company is CurrentCompanyContext ctx && ctx.CompanyId is null)
            ctx.Set(1, 1, [1]);

        return new InventoryService(
            new Repository<InventoryItem>(db),
            new Repository<StockMovement>(db),
            new Repository<StockBatch>(db),
            new Repository<Product>(db),
            new Repository<Warehouse>(db),
            new Repository<WarehouseLocation>(db),
            new Repository<InventoryLocationBalance>(db),
            new Repository<CompanySettings>(db),
            new UnitOfWork(db),
            mapper,
            new StockAdjustmentValidator(),
            notifications ?? Mock.Of<INotificationService>(),
            company);
    }
}
