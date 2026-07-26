namespace CarAutoParts.Application.Interfaces;

/// <summary>No-op company context used when none is supplied (design-time / tests).</summary>
public sealed class NullCurrentCompanyContext : ICurrentCompanyContext
{
    public static readonly NullCurrentCompanyContext Instance = new();

    private NullCurrentCompanyContext() { }

    public int? CompanyId => null;
    public int? BranchId => null;
    public IReadOnlyList<int> AllowedBranchIds => Array.Empty<int>();
    public void Set(int companyId, int? branchId = null, IEnumerable<int>? allowedBranchIds = null) { }
    public void Clear() { }
    public bool IsBranchAllowed(int branchId) => true;
}
