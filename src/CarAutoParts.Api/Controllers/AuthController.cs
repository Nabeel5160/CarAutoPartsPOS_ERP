using CarAutoParts.Api.Auth;
using CarAutoParts.Api.Contracts;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Api.Controllers;

[Route("api/auth")]
[Route("api/v1/auth")]
public class AuthController : ApiControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUser;
    private readonly JwtTokenService _jwtTokenService;
    private readonly ApplicationDbContext _db;
    private readonly IMfaService _mfa;

    public AuthController(
        IAuthService authService,
        ICurrentUserService currentUser,
        JwtTokenService jwtTokenService,
        ApplicationDbContext db,
        IMfaService mfa)
    {
        _authService = authService;
        _currentUser = currentUser;
        _jwtTokenService = jwtTokenService;
        _db = db;
        _mfa = mfa;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation failed",
                Detail = "Username and password are required.",
                Status = StatusCodes.Status400BadRequest,
                Type = "https://httpstatuses.com/400"
            });
        }

        var outcome = await _authService.TryLoginAsync(request.Username.Trim(), request.Password, ct);
        if (outcome.AccountLocked)
        {
            var locked = new ProblemDetails
            {
                Title = "Account locked",
                Detail = outcome.Error ?? "Account is temporarily locked.",
                Status = StatusCodes.Status401Unauthorized,
                Type = "account_locked"
            };
            locked.Extensions["code"] = "account_locked";
            return Unauthorized(locked);
        }

        if (outcome.Login is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "Invalid username or password.",
                Status = StatusCodes.Status401Unauthorized,
                Type = "https://httpstatuses.com/401"
            });
        }

        var login = outcome.Login;
        if (login.MfaEnabled)
        {
            var ticket = _mfa.IssueMfaTicket(login.UserId);
            return Ok(new ApiLoginResponse(null, null, login, MfaRequired: true, MfaTicket: ticket));
        }

        return Ok(await IssueFullLoginAsync(login, ct));
    }

    public record MfaVerifyRequest(string MfaTicket, string Code);

    [AllowAnonymous]
    [HttpPost("mfa/verify")]
    public async Task<IActionResult> VerifyMfa([FromBody] MfaVerifyRequest request, CancellationToken ct)
    {
        if (!_mfa.TryConsumeMfaTicket(request.MfaTicket, out var userId))
            return Unauthorized(new ProblemDetails { Title = "MFA ticket expired", Status = 401 });

        var verified = await _mfa.VerifyLoginCodeAsync(userId, request.Code, ct);
        if (!verified.Succeeded)
            return Unauthorized(new ProblemDetails { Title = "Invalid MFA code", Detail = verified.Error, Status = 401 });

        var user = await _db.Users.AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);
        if (user is null)
            return Unauthorized();

        var roles = user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToList();
        var permissions = user.UserRoles.SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var login = new Application.DTOs.Auth.LoginResultDto(
            user.Id, user.Username, user.DisplayName, user.Email, roles, permissions,
            user.MustChangePassword, user.MfaEnabled, false);
        return Ok(await IssueFullLoginAsync(login, ct));
    }

    [Authorize]
    [HttpGet("mfa/status")]
    public async Task<IActionResult> MfaStatus(CancellationToken ct)
    {
        if (_currentUser.CurrentUser is null) return Unauthorized();
        return Ok(await _mfa.GetStatusAsync(_currentUser.CurrentUser.Id, ct));
    }

    [Authorize]
    [HttpPost("mfa/enroll/begin")]
    public async Task<IActionResult> BeginEnroll(CancellationToken ct)
    {
        if (_currentUser.CurrentUser is null) return Unauthorized();
        return FromResult(await _mfa.BeginEnrollAsync(_currentUser.CurrentUser.Id, ct));
    }

    public record MfaCodeRequest(string Code);

    [Authorize]
    [HttpPost("mfa/enroll/confirm")]
    public async Task<IActionResult> ConfirmEnroll([FromBody] MfaCodeRequest request, CancellationToken ct)
    {
        if (_currentUser.CurrentUser is null) return Unauthorized();
        return FromResult(await _mfa.ConfirmEnrollAsync(_currentUser.CurrentUser.Id, request.Code, ct));
    }

    public record MfaDisableRequest(string Password, string Code);

    [Authorize]
    [HttpPost("mfa/disable")]
    public async Task<IActionResult> DisableMfa([FromBody] MfaDisableRequest request, CancellationToken ct)
    {
        if (_currentUser.CurrentUser is null) return Unauthorized();
        return FromResult(await _mfa.DisableAsync(_currentUser.CurrentUser.Id, request.Password, request.Code, ct));
    }

    [Authorize(Policy = Application.Constants.Permissions.MfaManage)]
    [HttpPost("mfa/reset/{userId:int}")]
    public async Task<IActionResult> ResetMfa(int userId, CancellationToken ct)
        => FromResult(await _mfa.AdminResetAsync(userId, ct));

    public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        if (_currentUser.CurrentUser is null)
            return Unauthorized();

        var result = await _authService.ChangePasswordAsync(
            _currentUser.CurrentUser.Id,
            request.CurrentPassword,
            request.NewPassword,
            ct);

        if (!result.Succeeded)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Password change failed",
                Detail = result.Error,
                Status = StatusCodes.Status400BadRequest,
                Type = "https://httpstatuses.com/400"
            });
        }

        return Ok(new { message = "Password updated." });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await _authService.LogoutAsync(ct);
        return Ok(new { message = "Logged out. Discard the client token." });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(ICurrentCompanyContext company, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.CurrentUser is null)
            return Unauthorized();

        var user = _currentUser.CurrentUser;
        var mfa = await _mfa.GetStatusAsync(user.Id, ct);
        return Ok(new
        {
            user.Id,
            user.Username,
            user.DisplayName,
            user.Email,
            user.MustChangePassword,
            company.CompanyId,
            company.BranchId,
            AllowedBranchIds = company.AllowedBranchIds,
            Permissions = _currentUser.Permissions,
            mfa.MfaEnabled,
            mfa.MustEnroll
        });
    }

    private async Task<ApiLoginResponse> IssueFullLoginAsync(Application.DTOs.Auth.LoginResultDto login, CancellationToken ct)
    {
        var companyId = await _db.Companies.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Id)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(ct);

        var allBranchIds = companyId.HasValue
            ? await _db.Branches.AsNoTracking()
                .Where(b => b.CompanyId == companyId && b.IsActive)
                .Select(b => b.Id)
                .ToListAsync(ct)
            : new List<int>();

        var isAdmin = login.Roles.Any(r =>
            string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase))
            || login.Permissions.Any(p =>
                string.Equals(p, Application.Constants.Permissions.PlatformManage, StringComparison.OrdinalIgnoreCase));

        List<int> branchIds;
        int? preferredDefault = null;

        if (isAdmin || allBranchIds.Count == 0)
        {
            branchIds = allBranchIds;
        }
        else
        {
            var assigned = await _db.UserBranches.AsNoTracking()
                .Where(ub => ub.UserId == login.UserId && !ub.IsDeleted)
                .Select(ub => new { ub.BranchId, ub.IsDefault })
                .ToListAsync(ct);

            branchIds = assigned
                .Select(a => a.BranchId)
                .Where(id => allBranchIds.Contains(id))
                .Distinct()
                .ToList();

            preferredDefault = assigned.FirstOrDefault(a => a.IsDefault)?.BranchId;

            // No ACL rows yet: fall back to company default only (not all branches)
            if (branchIds.Count == 0)
            {
                var def = await _db.Branches.AsNoTracking()
                    .Where(b => b.CompanyId == companyId && b.IsDefault && b.IsActive)
                    .Select(b => (int?)b.Id)
                    .FirstOrDefaultAsync(ct);
                if (def.HasValue)
                    branchIds = [def.Value];
                else if (allBranchIds.Count > 0)
                    branchIds = [allBranchIds[0]];
            }
        }

        var branchId = preferredDefault is int pd && branchIds.Contains(pd)
            ? pd
            : (int?)null;

        if (!branchId.HasValue && companyId.HasValue)
        {
            branchId = await _db.Branches.AsNoTracking()
                .Where(b => b.CompanyId == companyId && b.IsDefault && branchIds.Contains(b.Id))
                .Select(b => (int?)b.Id)
                .FirstOrDefaultAsync(ct);
        }

        branchId ??= branchIds.FirstOrDefault();

        var (token, expiresAt) = _jwtTokenService.CreateToken(login, companyId, branchId, branchIds);
        return new ApiLoginResponse(token, expiresAt, login, MustEnrollMfa: login.MustEnrollMfa);
    }
}
