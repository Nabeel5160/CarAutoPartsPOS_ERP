using CarAutoParts.Api.Auth;
using CarAutoParts.Api.Contracts;
using CarAutoParts.Application.Interfaces;
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

    public AuthController(
        IAuthService authService,
        ICurrentUserService currentUser,
        JwtTokenService jwtTokenService,
        ApplicationDbContext db)
    {
        _authService = authService;
        _currentUser = currentUser;
        _jwtTokenService = jwtTokenService;
        _db = db;
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
        var companyId = await _db.Companies.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Id)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(ct);

        var branchIds = companyId.HasValue
            ? await _db.Branches.AsNoTracking()
                .Where(b => b.CompanyId == companyId && b.IsActive)
                .Select(b => b.Id)
                .ToListAsync(ct)
            : [];

        var branchId = companyId.HasValue
            ? await _db.Branches.AsNoTracking()
                .Where(b => b.CompanyId == companyId && b.IsDefault)
                .Select(b => (int?)b.Id)
                .FirstOrDefaultAsync(ct)
            : null;
        branchId ??= branchIds.FirstOrDefault();

        var (token, expiresAt) = _jwtTokenService.CreateToken(login, companyId, branchId, branchIds);
        return Ok(new ApiLoginResponse(token, expiresAt, login));
    }

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
    public IActionResult Me(ICurrentCompanyContext company)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.CurrentUser is null)
            return Unauthorized();

        var user = _currentUser.CurrentUser;
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
            Permissions = _currentUser.Permissions
        });
    }
}
