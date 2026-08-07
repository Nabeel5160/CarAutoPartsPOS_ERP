using CarAutoParts.Application.Enterprise;
using CarAutoParts.Application.Finance;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Common;
using CarAutoParts.Infrastructure.Data;
using CarAutoParts.Infrastructure.Data.Interceptors;
using CarAutoParts.Infrastructure.Data.Seed;
using CarAutoParts.Infrastructure.Fbr;
using CarAutoParts.Infrastructure.Logging;
using CarAutoParts.Infrastructure.Printing;
using CarAutoParts.Infrastructure.Repositories;
using CarAutoParts.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace CarAutoParts.Infrastructure;

public static class DependencyInjection
{
    public const string DefaultConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=CarAutoPartsDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Connection Timeout=60";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        SerilogConfiguration.ConfigureSerilog(configuration);
        services.AddLogging(logging => logging.AddSerilog(dispose: true));

        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? DefaultConnectionString;

        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure(3);
            });
            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IFinanceDb, FinanceDbAdapter>();
        services.AddScoped<IEnterpriseDb, EnterpriseDbAdapter>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<IFbrOutboxService, FbrOutboxService>();
        services.AddSingleton<CarAutoParts.Infrastructure.Health.OutboxHeartbeat>();

        services.Configure<FbrOptions>(configuration.GetSection(FbrOptions.SectionName));
        services.AddHttpClient<IFbrService, FbrService>();

        services.AddScoped<ExcelService>();
        services.AddScoped<PdfReportService>();
        services.AddScoped<ReceiptPrintService>();
        services.AddScoped<IBarcodeService, BarcodeService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<DataSeeder>();
        services.AddScoped<DemoDataSeeder>();
        services.AddScoped<EnterprisePlatformSeeder>();

        services.AddHostedService<BackupBackgroundService>();
        services.AddHostedService<OutboxProcessor>();
        services.AddHostedService<SlaMonitorBackgroundService>();
        services.AddHostedService<CrmActivityMonitorBackgroundService>();

        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        using var scope = serviceProvider.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInit");

        var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
        await seeder.SeedAsync(ct);
        var enterprise = scope.ServiceProvider.GetRequiredService<EnterprisePlatformSeeder>();
        await enterprise.SeedAsync(ct);

        // Production hard-blocks demo seed unless CAP_ALLOW_DEMO_SEED=true (explicit escape hatch).
        var allowDemoOverride = string.Equals(
            Environment.GetEnvironmentVariable("CAP_ALLOW_DEMO_SEED"), "true", StringComparison.OrdinalIgnoreCase);
        var seedDemo = configuration.GetValue("Seed:DemoData", !env.IsProduction());
        if (env.IsProduction() && !allowDemoOverride)
        {
            if (seedDemo)
                logger.LogWarning("Seed:DemoData was true but Production hard-blocks demo seed. Set CAP_ALLOW_DEMO_SEED=true to override.");
            seedDemo = false;
        }

        if (seedDemo)
        {
            logger.LogInformation("Seeding demo data (Seed:DemoData=true).");
            var demoSeeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
            await demoSeeder.SeedAsync(ct);
        }
        else
            logger.LogInformation("Skipping demo seed (Production guard or Seed:DemoData=false).");
    }
}
