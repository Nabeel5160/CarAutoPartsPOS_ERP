using System.Security.Claims;
using CarAutoParts.Api.Auth;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Api.Middleware;

/// <summary>Populates current user and company context from JWT / headers.</summary>
public sealed class CurrentUserMiddleware
{
    public const string CompanyHeader = "X-Company-Id";
    public const string BranchHeader = "X-Branch-Id";

    private readonly RequestDelegate _next;

    public CurrentUserMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentUserService currentUser,
        ICurrentCompanyContext companyContext,
        ApplicationDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var username = context.User.FindFirstValue(ClaimTypes.Name) ?? "unknown";
            var displayName = context.User.FindFirstValue("display_name") ?? username;
            var email = context.User.FindFirstValue(ClaimTypes.Email);

            _ = int.TryParse(userIdClaim, out var userId);

            var permissions = context.User.Claims
                .Where(c => c.Type == JwtTokenService.PermissionClaimType)
                .Select(c => c.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var mustChange = false;
            if (userId > 0)
            {
                mustChange = await db.Users.AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => u.MustChangePassword)
                    .FirstOrDefaultAsync(context.RequestAborted);
            }

            currentUser.SetUser(new AppUser
            {
                Id = userId,
                Username = username,
                DisplayName = displayName,
                Email = email,
                IsActive = true,
                MustChangePassword = mustChange
            }, permissions);

            var allowedBranchIds = context.User.Claims
                .Where(c => c.Type == JwtTokenService.BranchIdsClaimType)
                .Select(c => int.TryParse(c.Value, out var id) ? id : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            int? companyId = null;
            if (context.Request.Headers.TryGetValue(CompanyHeader, out var companyHeader) &&
                int.TryParse(companyHeader.FirstOrDefault(), out var headerCompany))
            {
                companyId = headerCompany;
            }
            else if (int.TryParse(context.User.FindFirstValue(JwtTokenService.CompanyClaimType), out var claimCompany))
            {
                companyId = claimCompany;
            }

            if (!companyId.HasValue)
            {
                companyId = await db.Companies.AsNoTracking()
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Id)
                    .Select(c => (int?)c.Id)
                    .FirstOrDefaultAsync(context.RequestAborted);
            }

            int? branchId = null;
            if (context.Request.Headers.TryGetValue(BranchHeader, out var branchHeader) &&
                int.TryParse(branchHeader.FirstOrDefault(), out var headerBranch))
            {
                if (allowedBranchIds.Count > 0 && !allowedBranchIds.Contains(headerBranch))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/problem+json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        type = "https://httpstatuses.com/403",
                        title = "Branch not allowed",
                        status = 403,
                        detail = $"Branch {headerBranch} is not in the allowed branch_ids set."
                    });
                    return;
                }

                branchId = headerBranch;
            }
            else if (int.TryParse(context.User.FindFirstValue(JwtTokenService.BranchClaimType), out var claimBranch))
            {
                branchId = claimBranch;
            }

            if (companyId.HasValue)
                companyContext.Set(companyId.Value, branchId, allowedBranchIds);
        }

        await _next(context);
    }
}

/// <summary>Adds correlation id to request/response for tracing.</summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing) &&
                            !string.IsNullOrWhiteSpace(existing)
            ? existing.ToString()
            : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        await _next(context);
    }
}
