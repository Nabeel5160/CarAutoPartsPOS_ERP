using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Auth;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Security;
using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>Authenticates users and establishes the session context.</summary>
public class AuthService : IAuthService
{
    private readonly IRepository<AppUser> _users;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public AuthService(
        IRepository<AppUser> users,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _users = users;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<LoginResultDto?> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var outcome = await TryLoginAsync(username, password, ct);
        return outcome.Login;
    }

    /// <inheritdoc />
    public async Task<AuthLoginOutcome> TryLoginAsync(string username, string password, CancellationToken ct = default)
    {
        var user = await _users.Query()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive && !u.IsDeleted, ct);

        if (user is null)
            return new AuthLoginOutcome(null, false, "Invalid username or password.");

        if (user.LockoutEndUtc.HasValue && user.LockoutEndUtc > DateTime.UtcNow)
            return new AuthLoginOutcome(null, true, "Account is temporarily locked.");

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= PasswordPolicy.MaxFailedAttempts)
            {
                user.LockoutEndUtc = DateTime.UtcNow.Add(PasswordPolicy.LockoutDuration);
                user.FailedLoginAttempts = 0;
            }
            _users.Update(user);
            await _unitOfWork.SaveChangesAsync(ct);
            return new AuthLoginOutcome(null, false, "Invalid username or password.");
        }

        user.FailedLoginAttempts = 0;
        user.LockoutEndUtc = null;
        user.LastLoginAt = DateTime.UtcNow;
        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);

        var roles = user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList();
        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _currentUser.SetUser(user, permissions);

        return new AuthLoginOutcome(
            new LoginResultDto(
                user.Id,
                user.Username,
                user.DisplayName,
                user.Email,
                roles,
                permissions,
                user.MustChangePassword),
            false,
            null);
    }

    /// <inheritdoc />
    public async Task<Result> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        if (!PasswordPolicy.IsValid(newPassword, out var passwordError))
            return Result.Failure(passwordError!);

        var user = await _users.Query().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);
        if (user is null)
            return Result.Failure("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            return Result.Failure("Current password is incorrect.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.FailedLoginAttempts = 0;
        user.LockoutEndUtc = null;
        user.MustChangePassword = false;
        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <inheritdoc />
    public Task LogoutAsync(CancellationToken ct = default)
    {
        _currentUser.Clear();
        return Task.CompletedTask;
    }
}
