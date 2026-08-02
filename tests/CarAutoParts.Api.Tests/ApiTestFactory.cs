using System.Linq;
using System.Net.Http.Headers;
using CarAutoParts.Api.Auth;
using CarAutoParts.Application.DTOs.Auth;
using CarAutoParts.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarAutoParts.Api.Tests;

/// <summary>
/// Boots the real API host (routing, auth, filters, DI) against an isolated
/// InMemory <see cref="ApplicationDbContext"/> instead of SQL Server, so CRM /
/// Service Light integration tests exercise real controllers + policies without
/// requiring a live SQL Server instance.
/// </summary>
public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"ApiTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Skip the SQL-only demo seeder path; tests seed exactly what they need.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:DemoData"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
    }

    /// <summary>Creates the InMemory database schema. Safe to call multiple times.</summary>
    public async Task EnsureDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    /// <summary>Opens a scope with a fresh <see cref="ApplicationDbContext"/> for direct seeding/assertions.
    /// The context has no company context set, so global company query filters are bypassed.</summary>
    public IServiceScope CreateScope() => Services.CreateScope();

    /// <summary>Builds an authenticated HttpClient carrying a real signed JWT with the given permission claims.</summary>
    public HttpClient CreateAuthorizedClient(
        int userId = 1,
        string username = "qa.tester",
        int? companyId = 1,
        IEnumerable<string>? permissions = null)
    {
        var client = CreateClient();
        var tokenService = Services.GetRequiredService<JwtTokenService>();
        var login = new LoginResultDto(
            userId,
            username,
            username,
            null,
            Array.Empty<string>(),
            (permissions ?? Array.Empty<string>()).ToArray());

        var (token, _) = tokenService.CreateToken(login, companyId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
