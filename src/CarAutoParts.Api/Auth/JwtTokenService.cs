using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CarAutoParts.Application.DTOs.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CarAutoParts.Api.Auth;

public class JwtTokenService
{
    public const string PermissionClaimType = "permission";
    public const string CompanyClaimType = "company_id";
    public const string BranchClaimType = "branch_id";
    public const string BranchIdsClaimType = "branch_ids";

    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options) => _options = options.Value;

    public (string Token, DateTime ExpiresAt) CreateToken(
        LoginResultDto login,
        int? companyId = null,
        int? branchId = null,
        IEnumerable<int>? allowedBranchIds = null)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, login.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, login.UserId.ToString()),
            new(ClaimTypes.Name, login.Username),
            new("display_name", login.DisplayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrWhiteSpace(login.Email))
            claims.Add(new Claim(ClaimTypes.Email, login.Email));

        if (companyId.HasValue)
            claims.Add(new Claim(CompanyClaimType, companyId.Value.ToString()));
        if (branchId.HasValue)
            claims.Add(new Claim(BranchClaimType, branchId.Value.ToString()));

        foreach (var id in allowedBranchIds?.Distinct() ?? Array.Empty<int>())
            claims.Add(new Claim(BranchIdsClaimType, id.ToString()));

        foreach (var role in login.Roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        foreach (var permission in login.Permissions)
            claims.Add(new Claim(PermissionClaimType, permission));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
