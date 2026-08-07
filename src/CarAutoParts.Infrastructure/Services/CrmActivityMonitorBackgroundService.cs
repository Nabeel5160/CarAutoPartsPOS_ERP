using CarAutoParts.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CarAutoParts.Infrastructure.Services;

/// <summary>Polls CRM activities for DueAt warn (SLA complete loop W3).</summary>
public sealed class CrmActivityMonitorBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CrmActivityMonitorBackgroundService> _logger;

    public CrmActivityMonitorBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<CrmActivityMonitorBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var monitor = scope.ServiceProvider.GetRequiredService<ICrmActivityMonitorService>();
                var raised = await monitor.SweepAsync(stoppingToken);
                if (raised > 0)
                    _logger.LogInformation("CRM activity monitor raised {Count} due warnings", raised);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "CRM activity monitor sweep failed");
            }

            try { await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
