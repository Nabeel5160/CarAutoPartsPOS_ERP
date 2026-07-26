using AutoMapper;
using CarAutoParts.Application.DTOs.Auth;
using CarAutoParts.Application.DTOs.Inventory;
using CarAutoParts.Application.DTOs.Partners;
using CarAutoParts.Application.DTOs.Products;
using CarAutoParts.Application.DTOs.Purchases;
using CarAutoParts.Application.DTOs.Sales;
using CarAutoParts.Application.DTOs.Settings;
using CarAutoParts.Application.DTOs.System;
using CarAutoParts.Application.DTOs.Transfers;
using CarAutoParts.Domain.Entities;

namespace CarAutoParts.Application.Mapping;

/// <summary>AutoMapper profiles for entity-to-DTO mappings.</summary>
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<AppUser, UserDto>()
            .ForCtorParam(nameof(UserDto.Roles), o => o.MapFrom(s => s.UserRoles.Select(ur => ur.Role.Name).ToList()));

        CreateMap<Role, RoleDto>()
            .ForCtorParam(nameof(RoleDto.PermissionCodes), o => o.MapFrom(s => s.RolePermissions.Select(rp => rp.Permission.Code).ToList()));

        CreateMap<Category, CategoryDto>()
            .ForCtorParam(nameof(CategoryDto.Children), o => o.MapFrom(s => s.Children));

        CreateMap<Brand, BrandDto>();
        CreateMap<Warehouse, WarehouseDto>();

        CreateMap<Product, ProductListDto>()
            .ForCtorParam(nameof(ProductListDto.CategoryName), o => o.MapFrom(s => s.Category.Name))
            .ForCtorParam(nameof(ProductListDto.BrandName), o => o.MapFrom(s => s.Brand.Name))
            .ForCtorParam(nameof(ProductListDto.TotalStock), o => o.MapFrom(s => s.InventoryItems.Sum(i => i.QuantityOnHand)));

        CreateMap<Product, ProductDetailDto>()
            .ForCtorParam(nameof(ProductDetailDto.CategoryName), o => o.MapFrom(s => s.Category.Name))
            .ForCtorParam(nameof(ProductDetailDto.BrandName), o => o.MapFrom(s => s.Brand.Name))
            .ForCtorParam(nameof(ProductDetailDto.ImagePaths), o => o.MapFrom(s => s.Images.OrderBy(i => i.SortOrder).Select(i => i.FilePath).ToList()))
            .ForCtorParam(nameof(ProductDetailDto.VehicleCompatibilities), o => o.MapFrom(s => s.VehicleCompatibilities));

        CreateMap<ProductVehicleCompatibility, VehicleCompatibilityDto>();
        CreateMap<VehicleCompatibilityDto, ProductVehicleCompatibility>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.ProductId, o => o.Ignore())
            .ForMember(d => d.Product, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.UpdatedAt, o => o.Ignore())
            .ForMember(d => d.CreatedBy, o => o.Ignore())
            .ForMember(d => d.UpdatedBy, o => o.Ignore())
            .ForMember(d => d.IsDeleted, o => o.Ignore())
            .ForMember(d => d.RowVersion, o => o.Ignore());

        CreateMap<InventoryItem, InventoryItemDto>()
            .ForCtorParam(nameof(InventoryItemDto.ProductName), o => o.MapFrom(s => s.Product.Name))
            .ForCtorParam(nameof(InventoryItemDto.Sku), o => o.MapFrom(s => s.Product.Sku))
            .ForCtorParam(nameof(InventoryItemDto.WarehouseName), o => o.MapFrom(s => s.Warehouse.Name))
            .ForCtorParam(nameof(InventoryItemDto.AvailableQuantity), o => o.MapFrom(s => s.QuantityOnHand - s.ReservedQuantity))
            .ForCtorParam(nameof(InventoryItemDto.StockValue), o => o.MapFrom(s => s.QuantityOnHand * s.AverageCost));

        CreateMap<StockMovement, StockMovementDto>()
            .ForCtorParam(nameof(StockMovementDto.ProductId), o => o.MapFrom(s => s.InventoryItem.ProductId))
            .ForCtorParam(nameof(StockMovementDto.ProductName), o => o.MapFrom(s => s.InventoryItem.Product.Name))
            .ForCtorParam(nameof(StockMovementDto.WarehouseId), o => o.MapFrom(s => s.InventoryItem.WarehouseId))
            .ForCtorParam(nameof(StockMovementDto.WarehouseName), o => o.MapFrom(s => s.InventoryItem.Warehouse.Name));

        CreateMap<SerialNumber, SerialNumberDto>()
            .ForCtorParam(nameof(SerialNumberDto.ProductName), o => o.MapFrom(s => s.Product.Name))
            .ForCtorParam(nameof(SerialNumberDto.CurrentWarehouseName), o => o.MapFrom(s => s.CurrentWarehouse != null ? s.CurrentWarehouse.Name : null));

        CreateMap<SerialNumberHistory, SerialNumberHistoryDto>();

        CreateMap<Supplier, SupplierDto>();
        CreateMap<Supplier, SupplierDetailDto>();
        CreateMap<SupplierDto, Supplier>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.Address, o => o.Ignore())
            .ForMember(d => d.Ntn, o => o.Ignore())
            .ForMember(d => d.Strn, o => o.Ignore())
            .ForMember(d => d.PurchaseOrders, o => o.Ignore())
            .ForMember(d => d.Payments, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.UpdatedAt, o => o.Ignore())
            .ForMember(d => d.CreatedBy, o => o.Ignore())
            .ForMember(d => d.UpdatedBy, o => o.Ignore())
            .ForMember(d => d.IsDeleted, o => o.Ignore())
            .ForMember(d => d.RowVersion, o => o.Ignore());

        CreateMap<Customer, CustomerDto>();
        CreateMap<Customer, CustomerDetailDto>();
        CreateMap<CustomerDto, Customer>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.Address, o => o.Ignore())
            .ForMember(d => d.NtnCnic, o => o.Ignore())
            .ForMember(d => d.Province, o => o.Ignore())
            .ForMember(d => d.SalesOrders, o => o.Ignore())
            .ForMember(d => d.SalesInvoices, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.UpdatedAt, o => o.Ignore())
            .ForMember(d => d.CreatedBy, o => o.Ignore())
            .ForMember(d => d.UpdatedBy, o => o.Ignore())
            .ForMember(d => d.IsDeleted, o => o.Ignore())
            .ForMember(d => d.RowVersion, o => o.Ignore());

        CreateMap<PurchaseOrder, PurchaseOrderListDto>()
            .ForCtorParam(nameof(PurchaseOrderListDto.SupplierName), o => o.MapFrom(s => s.Supplier.Name));

        CreateMap<PurchaseOrder, PurchaseOrderDetailDto>()
            .ForCtorParam(nameof(PurchaseOrderDetailDto.SupplierName), o => o.MapFrom(s => s.Supplier.Name))
            .ForCtorParam(nameof(PurchaseOrderDetailDto.WarehouseName), o => o.MapFrom(s => s.Warehouse != null ? s.Warehouse.Name : null))
            .ForCtorParam(nameof(PurchaseOrderDetailDto.Lines), o => o.MapFrom(s => s.Lines));

        CreateMap<PurchaseOrderLine, PurchaseOrderLineDto>()
            .ForCtorParam(nameof(PurchaseOrderLineDto.ProductName), o => o.MapFrom(s => s.Product.Name));

        CreateMap<PurchaseReturn, PurchaseReturnDto>()
            .ForCtorParam(nameof(PurchaseReturnDto.SupplierName), o => o.MapFrom(s => s.Supplier.Name));

        CreateMap<SalesInvoice, SalesInvoiceListDto>()
            .ForCtorParam(nameof(SalesInvoiceListDto.CustomerName), o => o.MapFrom(s => s.Customer != null ? s.Customer.Name : s.BuyerName))
            .ForCtorParam(nameof(SalesInvoiceListDto.FbrInvoiceNumber), o => o.MapFrom(s => s.FbrSubmission != null ? s.FbrSubmission.FbrInvoiceNumber : null));

        CreateMap<SalesInvoice, SalesInvoiceDetailDto>()
            .ForCtorParam(nameof(SalesInvoiceDetailDto.CustomerName), o => o.MapFrom(s => s.Customer != null ? s.Customer.Name : s.BuyerName))
            .ForCtorParam(nameof(SalesInvoiceDetailDto.FbrInvoiceNumber), o => o.MapFrom(s => s.FbrSubmission != null ? s.FbrSubmission.FbrInvoiceNumber : null))
            .ForCtorParam(nameof(SalesInvoiceDetailDto.FbrStatus), o => o.MapFrom(s => s.FbrSubmission != null ? (Domain.Enums.FbrSubmissionStatus?)s.FbrSubmission.Status : null))
            .ForCtorParam(nameof(SalesInvoiceDetailDto.Lines), o => o.MapFrom(s => s.Lines));

        CreateMap<SalesInvoiceLine, SalesInvoiceLineDto>();
        CreateMap<SalesOrder, SalesOrderListDto>()
            .ForCtorParam(nameof(SalesOrderListDto.CustomerName), o => o.MapFrom(s => s.Customer != null ? s.Customer.Name : null));

        CreateMap<SalesReturn, SalesReturnDto>()
            .ForCtorParam(nameof(SalesReturnDto.InvoiceNumber), o => o.MapFrom(s => s.SalesInvoice != null ? s.SalesInvoice.InvoiceNumber : null))
            .ForCtorParam(nameof(SalesReturnDto.CustomerName), o => o.MapFrom(s => s.Customer != null ? s.Customer.Name : null));

        CreateMap<InventoryTransfer, TransferListDto>()
            .ForCtorParam(nameof(TransferListDto.FromWarehouseName), o => o.MapFrom(s => s.FromWarehouse.Name))
            .ForCtorParam(nameof(TransferListDto.ToWarehouseName), o => o.MapFrom(s => s.ToWarehouse.Name));

        CreateMap<InventoryTransfer, TransferDetailDto>()
            .ForCtorParam(nameof(TransferDetailDto.FromWarehouseName), o => o.MapFrom(s => s.FromWarehouse.Name))
            .ForCtorParam(nameof(TransferDetailDto.ToWarehouseName), o => o.MapFrom(s => s.ToWarehouse.Name))
            .ForCtorParam(nameof(TransferDetailDto.Lines), o => o.MapFrom(s => s.Lines));

        CreateMap<InventoryTransferLine, TransferLineDto>()
            .ForCtorParam(nameof(TransferLineDto.ProductName), o => o.MapFrom(s => s.Product.Name));

        CreateMap<CompanySettings, CompanySettingsDto>();
        CreateMap<CompanySettingsDto, CompanySettings>()
            .ForMember(d => d.FbrBearerToken, o => o.Ignore())
            .ForMember(d => d.DatabaseConnectionString, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.UpdatedAt, o => o.Ignore())
            .ForMember(d => d.CreatedBy, o => o.Ignore())
            .ForMember(d => d.UpdatedBy, o => o.Ignore())
            .ForMember(d => d.IsDeleted, o => o.Ignore())
            .ForMember(d => d.RowVersion, o => o.Ignore());

        CreateMap<BackupHistory, BackupHistoryDto>()
            .ForCtorParam(nameof(BackupHistoryDto.BackupType), o => o.MapFrom(s => s.BackupType.ToString()));

        CreateMap<AppNotification, NotificationDto>();
        CreateMap<AuditLog, AuditLogDto>();
    }
}
