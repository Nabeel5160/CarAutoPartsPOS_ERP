using System.IO;
using System.Windows;
using System.Windows.Threading;
using CarAutoParts.Application;
using CarAutoParts.Infrastructure;
using CarAutoParts.Presentation.Services;
using CarAutoParts.Presentation.Selectors;
using CarAutoParts.Presentation.ViewModels;
using CarAutoParts.Presentation.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace CarAutoParts.Presentation;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private IServiceScope? _scope;

    public static IServiceProvider? Services { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Contains("--seed-demo", StringComparer.OrdinalIgnoreCase))
        {
            await RunSeedDemoOnlyAsync();
            Shutdown(0);
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            _host = Host.CreateDefaultBuilder()
                .UseSerilog((_, cfg) => cfg
                    .MinimumLevel.Information()
                    .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "logs", "app-.log"),
                        rollingInterval: RollingInterval.Day))
                .ConfigureAppConfiguration((_, config) =>
                {
                    config.SetBasePath(AppContext.BaseDirectory);
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
                .ConfigureServices((ctx, services) =>
                {
                    services.AddApplication();
                    services.AddInfrastructure(ctx.Configuration);
                    services.AddPresentation();
                })
                .Build();

            _scope = _host.Services.CreateScope();
            Services = _scope.ServiceProvider;

            Resources["ViewModelTemplateSelector"] = ViewModelTemplateSelector.CreateDefault();

            Services.GetRequiredService<IThemeService>().Initialize();

            try
            {
                await _host.Services.InitializeDatabaseAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Database initialization failed");
                MessageBox.Show(
                    $"Could not connect to the database.\n\n{ex.Message}\n\n" +
                    "Server: (localdb)\\MSSQLLocalDB\n" +
                    "Authentication: Windows Authentication\n\n" +
                    "Try in PowerShell:\n  sqllocaldb start MSSQLLocalDB",
                    "Car Auto Parts ERP",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            var loginVm = Services.GetRequiredService<LoginViewModel>();
            var loginWindow = new LoginWindow
            {
                DataContext = loginVm
            };

            loginWindow.ContentRendered += (_, _) => loginWindow.Activate();

            if (loginWindow.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            var shell = new ShellWindow
            {
                DataContext = Services.GetRequiredService<ShellViewModel>()
            };
            MainWindow = shell;

            shell.Loaded += async (_, _) =>
            {
                try
                {
                    if (shell.DataContext is ShellViewModel shellVm)
                        await shellVm.InitializeAsync();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to initialize main window");
                    Services?.GetService<IExceptionHandler>()?.Handle(ex, "Shell");
                }
            };

            shell.Show();
            shell.Activate();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }
        catch (Exception ex)
        {
            ex = ex.InnerException ?? ex;
            while (ex is System.Reflection.TargetInvocationException { InnerException: not null } tie)
                ex = tie.InnerException;

            Log.Error(ex, "Application startup failed");
            MessageBox.Show(
                $"Failed to start the application:\n\n{ex.Message}",
                "Car Auto Parts ERP",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static async Task RunSeedDemoOnlyAsync()
    {
        using var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((ctx, services) =>
            {
                services.AddApplication();
                services.AddInfrastructure(ctx.Configuration);
            })
            .Build();

        await host.Services.InitializeDatabaseAsync();

        MessageBox.Show(
            "Demo data seeding finished.\n\n" +
            "20 products, suppliers, customers, inventory, purchase orders,\n" +
            "sales history, transfers, and serial numbers were added.\n\n" +
            "Extra logins: manager/manager123, sales/sales123",
            "Car Auto Parts ERP",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
            await _host.StopAsync(TimeSpan.FromSeconds(3));

        _scope?.Dispose();
        _host?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Services?.GetService<IExceptionHandler>()?.Handle(e.Exception, "UI thread");
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            Services?.GetService<IExceptionHandler>()?.Handle(ex, "App domain");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Services?.GetService<IExceptionHandler>()?.Handle(e.Exception, "Background task");
        e.SetObserved();
    }
}
