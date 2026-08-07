namespace CarAutoParts.Application.Services;

/// <summary>Testable UTC clock for SLA elapsed calculations.</summary>
public interface ISlaClockTime
{
    DateTime UtcNow { get; }
}

public sealed class SystemSlaClockTime : ISlaClockTime
{
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>Mutable clock for unit tests.</summary>
public sealed class FakeSlaClockTime : ISlaClockTime
{
    public DateTime UtcNow { get; set; } = DateTime.UtcNow;
}
