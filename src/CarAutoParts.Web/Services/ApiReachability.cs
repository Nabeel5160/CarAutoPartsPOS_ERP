namespace CarAutoParts.Web.Services;

/// <summary>Tracks whether the API host is reachable (Phase 1 degraded-mode signal).</summary>
public static class ApiReachability
{
    public static event Action? Changed;
    public static bool IsDown { get; private set; }
    public static string? LastError { get; private set; }

    public static void MarkDown(string? error = null)
    {
        IsDown = true;
        LastError = error;
        Changed?.Invoke();
    }

    public static void MarkUp()
    {
        if (!IsDown) return;
        IsDown = false;
        LastError = null;
        Changed?.Invoke();
    }
}
