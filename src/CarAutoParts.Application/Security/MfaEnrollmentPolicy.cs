using CarAutoParts.Application.Constants;

namespace CarAutoParts.Application.Security;

/// <summary>
/// Counter-first MFA policy: cashiers with <c>pos.checkout</c> are not forced to enroll
/// unless <c>MfaEnforced</c> is set. Privileged admin/finance/platform users still enroll.
/// </summary>
public static class MfaEnrollmentPolicy
{
    /// <summary>True when permissions imply privileged admin/finance/platform access.</summary>
    public static bool IsPrivileged(IEnumerable<string> permissions)
    {
        foreach (var p in permissions)
        {
            if (p.Equals(Permissions.UsersManage, StringComparison.OrdinalIgnoreCase)
                || p.Equals(Permissions.FinanceManage, StringComparison.OrdinalIgnoreCase)
                || p.Equals(Permissions.FinancePost, StringComparison.OrdinalIgnoreCase)
                || p.Equals(Permissions.FinanceForceClose, StringComparison.OrdinalIgnoreCase)
                || p.Equals(Permissions.PlatformManage, StringComparison.OrdinalIgnoreCase)
                || p.StartsWith("finance.", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Counter path: has POS checkout and lacks users/finance/platform manage — sell without MFA enroll.
    /// </summary>
    public static bool IsCounterCashierPath(IEnumerable<string> permissions)
    {
        var list = permissions as IList<string> ?? permissions.ToList();
        var hasCheckout = list.Any(p => p.Equals(Permissions.PosCheckout, StringComparison.OrdinalIgnoreCase));
        if (!hasCheckout) return false;

        var hasManageGate = list.Any(p =>
            p.Equals(Permissions.UsersManage, StringComparison.OrdinalIgnoreCase)
            || p.Equals(Permissions.FinanceManage, StringComparison.OrdinalIgnoreCase)
            || p.Equals(Permissions.PlatformManage, StringComparison.OrdinalIgnoreCase));

        return !hasManageGate;
    }

    /// <summary>
    /// Force MFA enroll only when already enforced on the user, or privileged (and not counter-only cashier).
    /// </summary>
    public static bool MustEnroll(bool mfaEnabled, bool mfaEnforced, IEnumerable<string> permissions)
    {
        if (mfaEnabled) return false;
        if (mfaEnforced) return true;

        var list = permissions as IList<string> ?? permissions.ToList();
        if (IsCounterCashierPath(list))
            return false;

        return IsPrivileged(list);
    }

    /// <summary>Post-login home: counter cashiers → /pos; everyone else → /.</summary>
    public static string PostLoginHome(IEnumerable<string> permissions) =>
        IsCounterCashierPath(permissions) ? "/pos" : "/";
}
