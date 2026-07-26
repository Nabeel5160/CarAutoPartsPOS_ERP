using CarAutoParts.Infrastructure.Data;
using CarAutoParts.Infrastructure.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CarAutoParts.Api.Health;

public sealed class OutboxReadinessHealthCheck : IHealthCheck
{
    private readonly OutboxHeartbeat _heartbeat;
    private readonly TimeSpan _maxStale = TimeSpan.FromMinutes(5);

    public OutboxReadinessHealthCheck(OutboxHeartbeat heartbeat) => _heartbeat = heartbeat;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var last = _heartbeat.LastSuccessUtc;
        if (last is null)
            return Task.FromResult(HealthCheckResult.Degraded("Outbox processor has not completed a cycle yet."));

        if (DateTime.UtcNow - last.Value > _maxStale)
            return Task.FromResult(HealthCheckResult.Unhealthy($"Outbox heartbeat stale (last {last:O})."));

        if (_heartbeat.LastPendingCount > 500)
            return Task.FromResult(HealthCheckResult.Degraded($"Outbox backlog {_heartbeat.LastPendingCount}."));

        return Task.FromResult(HealthCheckResult.Healthy($"Outbox OK; pending≈{_heartbeat.LastPendingCount}"));
    }
}

public sealed class SqlReadinessHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _db;

    public SqlReadinessHealthCheck(ApplicationDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var ok = await _db.Database.CanConnectAsync(cancellationToken);
            return ok
                ? HealthCheckResult.Healthy("SQL connected")
                : HealthCheckResult.Unhealthy("SQL cannot connect");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQL check failed", ex);
        }
    }
}
