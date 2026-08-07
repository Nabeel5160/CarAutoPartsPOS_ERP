using CarAutoParts.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CarAutoParts.Infrastructure.Services;

/// <summary>Polls SLA timers for warn/breach (Program C2).</summary>
public sealed class SlaMonitorBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SlaMonitorBackgroundService> _logger;

    public SlaMonitorBackgroundService(IServiceScopeFactory scopeFactory, ILogger<SlaMonitorBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Stagger first run slightly after boot
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var monitor = scope.ServiceProvider.GetRequiredService<ISlaMonitorService>();
                var raised = await monitor.SweepAsync(stoppingToken);
                if (raised > 0)
                    _logger.LogInformation("SLA monitor raised {Count} warn/breach events", raised);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "SLA monitor sweep failed");
            }

            try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
