using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Auth;
using CarAutoParts.Domain.Entities;

namespace CarAutoParts.Application.Interfaces;

public interface ICurrentUserService
{
    AppUser? CurrentUser { get; }
    IReadOnlyList<string> Permissions { get; }
    bool IsAuthenticated { get; }
    void SetUser(AppUser user, IReadOnlyList<string> permissions);
    void Clear();
    bool HasPermission(string permissionCode);
}

public interface IAuthService
{
    Task<LoginResultDto?> LoginAsync(string username, string password, CancellationToken ct = default);
    /// <summary>Returns null for invalid credentials; AccountLocked=true when lockout is active.</summary>
    Task<AuthLoginOutcome> TryLoginAsync(string username, string password, CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);
    Task<Result> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken ct = default);
}

public sealed record AuthLoginOutcome(LoginResultDto? Login, bool AccountLocked, string? Error);

public interface IAuthorizationService
{
    Task EnsurePermissionAsync(string permissionCode, CancellationToken ct = default);
    bool HasPermission(string permissionCode);
}
