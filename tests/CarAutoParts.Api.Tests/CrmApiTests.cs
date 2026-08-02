using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CarAutoParts.Application.Config;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Crm;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CarAutoParts.Api.Tests;

/// <summary>
/// Program A (Light CRM) API-level regression: exercises the real HTTP pipeline
/// (routing, JWT auth, permission policies, module gate) against an InMemory store.
/// </summary>
public class CrmApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private ApiTestFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiTestFactory();
        await _factory.EnsureDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private HttpClient CrmClient(IEnumerable<string>? extraPermissions = null) =>
        _factory.CreateAuthorizedClient(permissions:
        [
            Permissions.CrmView, Permissions.CrmLeads, Permissions.CrmManage, Permissions.CrmActivities,
            .. extraPermissions ?? Array.Empty<string>()
        ]);

    [Fact]
    public async Task Smoke_Returns_Ok_With_Lead_Count_And_Open_Deals()
    {
        using var client = CrmClient();

        var response = await client.GetAsync("/api/crm/smoke");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("ok").GetBoolean().Should().BeTrue();
        body.TryGetProperty("leadCount", out _).Should().BeTrue();
        body.TryGetProperty("openDeals", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Smoke_Without_Token_Returns_Unauthorized()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/crm/smoke");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Smoke_Without_CrmView_Permission_Returns_Forbidden()
    {
        using var client = _factory.CreateAuthorizedClient(permissions: []);
        var response = await client.GetAsync("/api/crm/smoke");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateLead_Then_List_Returns_It()
    {
        using var client = CrmClient();

        var create = await client.PostAsJsonAsync("/api/crm/leads",
            new LeadCreateDto("Bilal Auto", "03011112222", null, "Walk-in", "Needs shocks", null, true));
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await create.Content.ReadFromJsonAsync<LeadDto>(Json);
        created!.Name.Should().Be("Bilal Auto");

        var list = await client.GetFromJsonAsync<JsonElement>("/api/crm/leads?page=1&pageSize=20", Json);
        list.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task ConvertLeadToCustomer_Twice_Is_Idempotent_Over_Http()
    {
        using var client = CrmClient();

        var create = await client.PostAsJsonAsync("/api/crm/leads",
            new LeadCreateDto("Idempotent Motors", "03033334444", null, "Referral", null, null, true));
        var lead = await create.Content.ReadFromJsonAsync<LeadDto>(Json);

        var first = await client.PostAsJsonAsync($"/api/crm/leads/{lead!.Id}/convert-customer", new { });
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstLead = await first.Content.ReadFromJsonAsync<LeadDto>(Json);
        firstLead!.ConvertedCustomerId.Should().NotBeNull();

        var second = await client.PostAsJsonAsync($"/api/crm/leads/{lead.Id}/convert-customer", new { });
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondLead = await second.Content.ReadFromJsonAsync<LeadDto>(Json);
        secondLead!.ConvertedCustomerId.Should().Be(firstLead.ConvertedCustomerId);
    }

    [Fact]
    public async Task PipelineDashboard_Reports_Weighted_Revenue()
    {
        using var client = CrmClient();

        await client.PostAsJsonAsync("/api/crm/opportunities",
            new OpportunityCreateDto("Deal A", null, null, 1000m, 50, null));
        await client.PostAsJsonAsync("/api/crm/opportunities",
            new OpportunityCreateDto("Deal B", null, null, 2000m, 25, null));

        var dash = await client.GetFromJsonAsync<CrmPipelineDashboardDto>("/api/crm/pipeline/dashboard", Json);
        dash!.OpenCount.Should().Be(2);
        dash.OpenValue.Should().Be(3000m);
        dash.WeightedValue.Should().Be(1000m);
    }

    [Fact]
    public async Task Lost_Without_Reason_Returns_BadRequest_Over_Http()
    {
        using var client = CrmClient();

        var create = await client.PostAsJsonAsync("/api/crm/leads",
            new LeadCreateDto("Lossy Motors", null, null, "Walk-in", null, null, true));
        var lead = await create.Content.ReadFromJsonAsync<LeadDto>(Json);

        var update = await client.PutAsJsonAsync($"/api/crm/leads/{lead!.Id}",
            new LeadUpdateDto("Lossy Motors", null, null, "Walk-in", null, null,
                CarAutoParts.Domain.Enums.LeadStatus.Lost, null, true));
        update.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Module_Disabled_Returns_404_For_Crm_Routes()
    {
        // Fresh factory: the app-config cache is per-process, so the override
        // must be seeded before the first request populates the resolved config.
        using var factory = new ApiTestFactory();
        await factory.EnsureDatabaseAsync();

        using (var scope = factory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.AppConfigEntries.Add(new AppConfigEntry
            {
                Scope = ConfigScopes.Module,
                Key = ConfigKeys.ModSalesCrm,
                Value = "false",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateAuthorizedClient(permissions:
            [Permissions.CrmView, Permissions.CrmLeads, Permissions.CrmManage]);
        var response = await client.GetAsync("/api/crm/smoke");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
