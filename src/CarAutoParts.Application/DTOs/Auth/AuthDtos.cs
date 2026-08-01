namespace CarAutoParts.Application.DTOs.Auth;

/// <summary>Result returned after a successful login (or MFA challenge prelude).</summary>
public record LoginResultDto(
    int UserId,
    string Username,
    string DisplayName,
    string? Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    bool MustChangePassword = false,
    bool MfaEnabled = false,
    bool MustEnrollMfa = false);

/// <summary>Application user summary.</summary>
public record UserDto(
    int Id,
    string Username,
    string DisplayName,
    string? Email,
    bool IsActive,
    DateTime? LastLoginAt,
    IReadOnlyList<string> Roles,
    IReadOnlyList<int> BranchIds,
    int? DefaultBranchId);

/// <summary>Payload for creating or updating a user.</summary>
public record UserCreateDto(
    string Username,
    string? Password,
    string DisplayName,
    string? Email,
    bool IsActive,
    IReadOnlyList<int> RoleIds,
    IReadOnlyList<int>? BranchIds = null,
    int? DefaultBranchId = null);

/// <summary>Role definition with optional permission codes.</summary>
public record RoleDto(
    int Id,
    string Name,
    string? Description,
    IReadOnlyList<string> PermissionCodes);
