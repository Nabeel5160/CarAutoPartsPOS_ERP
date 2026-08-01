using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;

namespace CarAutoParts.Application.Services;

/// <summary>Shared branch ACL / warehouse scoping for Excel and PDF report paths.</summary>
public static class ReportBranchScope
{
    public static bool IsDenied(ICurrentCompanyContext company, int? branchId) =>
        branchId is int b && !company.IsBranchAllowed(b);

    /// <summary>
    /// Warehouses in scope for reports. Null means unrestricted (no ACL list and no branch filter).
    /// </summary>
    public static HashSet<int>? AllowedWarehouseIds(
        IQueryable<Warehouse> warehouses,
        ICurrentCompanyContext company,
        int? branchId)
    {
        if (branchId is null && company.AllowedBranchIds.Count == 0)
            return null;

        var q = warehouses.Where(w => !w.IsDeleted);
        if (branchId is int b)
            q = q.Where(w => w.BranchId == b);
        else
            q = q.Where(w => w.BranchId == null || company.AllowedBranchIds.Contains(w.BranchId.Value));

        return q.Select(w => w.Id).ToHashSet();
    }
}
