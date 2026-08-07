using CarAutoParts.Application.Config;
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

        services.AddMemoryCache();
        services.AddScoped<IMoneyAuditService, MoneyAuditService>();
        services.AddScoped<IApprovalWorkflowService, ApprovalWorkflowService>();
        services.AddScoped<IMfaService, MfaService>();
        services.AddScoped<IDocumentVoidService, DocumentVoidService>();
        services.AddScoped<IOnboardingService, OnboardingService>();

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ICurrentCompanyContext, CurrentCompanyContext>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();

        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IBrandService, BrandService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IWarehouseLocationService, WarehouseLocationService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IAtpService, AtpService>();

        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ICrmService, CrmService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<IPurchaseRequisitionService, PurchaseRequisitionService>();
        services.AddScoped<IReorderService, ReorderService>();
        services.AddScoped<IPurchaseRfqService, PurchaseRfqService>();
        services.AddScoped<ISalesTargetService, SalesTargetService>();
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<ISalesCommissionService, SalesCommissionService>();
        services.AddScoped<IServiceTicketService, ServiceTicketService>();
        services.AddScoped<IAmcContractService, AmcContractService>();
        services.AddScoped<IServiceFieldService, ServiceFieldService>();
        services.AddSingleton<ISlaClockTime, SystemSlaClockTime>();
        services.AddScoped<ISlaPolicyService, SlaPolicyService>();
        services.AddScoped<ISlaClockService, SlaClockService>();
        services.AddScoped<IOpsSlaClockService, OpsSlaClockService>();
        services.AddScoped<ISlaMonitorService, SlaMonitorService>();
        services.AddScoped<ICrmActivityMonitorService, CrmActivityMonitorService>();
        services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();

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
        services.AddScoped<AppConfigService>();
        services.AddScoped<IAppConfigService>(sp => sp.GetRequiredService<AppConfigService>());
        services.AddScoped<IFeatureGate>(sp => sp.GetRequiredService<AppConfigService>());

        services.AddScoped<IEnterpriseInventoryService, EnterpriseInventoryService>();
        services.AddScoped<IEnterprisePurchaseService, EnterprisePurchaseService>();
        services.AddScoped<IEnterpriseSalesService, EnterpriseSalesService>();
        services.AddScoped<IMasterDataService, MasterDataService>();
        services.AddScoped<IAccountMappingService, AccountMappingService>();
        services.AddScoped<IFinancialReportService, FinancialReportService>();
        services.AddScoped<IGlPostingService, GlPostingService>();
        services.AddScoped<IAccountingPeriodService, AccountingPeriodService>();
        services.AddScoped<IPaymentPostingService, PaymentPostingService>();
        services.AddScoped<IPhase4FinanceService, Phase4FinanceService>();

        // IBarcodeService and IFbrService are registered in Infrastructure.

        return services;
    }
}
