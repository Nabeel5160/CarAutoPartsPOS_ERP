namespace CarAutoParts.Infrastructure.Health;

/// <summary>Tracks last successful outbox batch for readiness probes.</summary>
public sealed class OutboxHeartbeat
{
    private long _lastSuccessUtcTicks;
    private int _lastPendingCount;

    public void RecordSuccess(int pendingCount)
    {
        Interlocked.Exchange(ref _lastSuccessUtcTicks, DateTime.UtcNow.Ticks);
        Interlocked.Exchange(ref _lastPendingCount, pendingCount);
    }

    public DateTime? LastSuccessUtc
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastSuccessUtcTicks);
            return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
        }
    }

    public int LastPendingCount => Interlocked.CompareExchange(ref _lastPendingCount, 0, 0);
}
