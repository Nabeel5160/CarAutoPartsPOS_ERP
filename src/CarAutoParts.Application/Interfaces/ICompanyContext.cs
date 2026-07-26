namespace CarAutoParts.Application.Interfaces;

public interface ICurrentCompanyContext
{
    int? CompanyId { get; }
    int? BranchId { get; }
    IReadOnlyList<int> AllowedBranchIds { get; }
    void Set(int companyId, int? branchId = null, IEnumerable<int>? allowedBranchIds = null);
    void Clear();
    bool IsBranchAllowed(int branchId);
}

public interface IOutboxWriter
{
    void Enqueue(string type, object payload);
}
