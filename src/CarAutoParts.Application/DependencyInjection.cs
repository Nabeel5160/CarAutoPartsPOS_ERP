using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Finance;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Mapping;
using CarAutoParts.Application.Services;
using CarAutoParts.Application.Validators;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CarAutoParts.Application;

/// <summary>Registers application-layer services, validators, and AutoMapper.</summary>
public static class DependencyInjection
{
    /// <summary>Adds CarAutoParts application services to the DI container.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(typeof(MappingProfile));
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddValidatorsFromAssemblyContaining<ProductValidator>();

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ICurrentCompanyContext, CurrentCompanyContext>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();

        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IBrandService, BrandService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IInventoryService, InventoryService>();

        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<IPurchaseRequisitionService, PurchaseRequisitionService>();
        services.AddScoped<IReorderService, ReorderService>();

        services.AddScoped<ISalesService, SalesService>();
        services.AddScoped<IPosCheckoutService, PosCheckoutService>();
        services.AddScoped<IPosFloorService, PosFloorService>();
        services.AddScoped<IReturnService, ReturnService>();
        services.AddScoped<ITransferService, TransferService>();

        services.AddScoped<ISerialNumberService, SerialNumberService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ISettingsService, SettingsService>();

        services.AddScoped<IEnterpriseInventoryService, EnterpriseInventoryService>();
        services.AddScoped<IEnterprisePurchaseService, EnterprisePurchaseService>();
        services.AddScoped<IEnterpriseSalesService, EnterpriseSalesService>();
        services.AddScoped<IMasterDataService, MasterDataService>();
        services.AddScoped<IAccountMappingService, AccountMappingService>();
        services.AddScoped<IFinancialReportService, FinancialReportService>();
        services.AddScoped<IGlPostingService, GlPostingService>();
        services.AddScoped<IAccountingPeriodService, AccountingPeriodService>();
        services.AddScoped<IPaymentPostingService, PaymentPostingService>();

        // IBarcodeService and IFbrService are registered in Infrastructure.

        return services;
    }
}
