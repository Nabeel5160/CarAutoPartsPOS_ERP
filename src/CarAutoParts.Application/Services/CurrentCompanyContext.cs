using CarAutoParts.Application.Interfaces;

namespace CarAutoParts.Application.Services;

public sealed class CurrentCompanyContext : ICurrentCompanyContext
{
    private IReadOnlyList<int> _allowedBranchIds = Array.Empty<int>();

    public int? CompanyId { get; private set; }
    public int? BranchId { get; private set; }
    public IReadOnlyList<int> AllowedBranchIds => _allowedBranchIds;

    public void Set(int companyId, int? branchId = null, IEnumerable<int>? allowedBranchIds = null)
    {
        CompanyId = companyId;
        BranchId = branchId;
        _allowedBranchIds = allowedBranchIds?.Distinct().ToArray() ?? Array.Empty<int>();
    }

    public void Clear()
    {
        CompanyId = null;
        BranchId = null;
        _allowedBranchIds = Array.Empty<int>();
    }

    public bool IsBranchAllowed(int branchId) =>
        _allowedBranchIds.Count == 0 || _allowedBranchIds.Contains(branchId);
}
