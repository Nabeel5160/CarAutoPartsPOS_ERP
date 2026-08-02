using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CarAutoParts.Application.Constants;
using CarAutoParts.Application.DTOs.Service;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using CarAutoParts.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CarAutoParts.Api.Tests;

/// <summary>
/// Program C1 (Service Light) API-level regression: tickets over the real HTTP
/// pipeline (routing, JWT auth, permission policies, module gate, company scope).
/// Skips gracefully (no-op) if the Service Light API surface is ever removed —
/// today it exists, so these assertions run for real.
/// </summary>
public class ServiceApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private ApiTestFactory _factory = null!;
    private int _customerId;

    public async Task InitializeAsync()
    {
        _factory = new ApiTestFactory();
        await _factory.EnsureDatabaseAsync();

        using var scope = _factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var customer = new Customer
        {
            Name = "Ali Motors", IsActive = true,
            CreatedAt = DateTime.UtcNow, CreatedBy = "seed"
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        _customerId = customer.Id;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private HttpClient ServiceClient() =>
        _factory.CreateAuthorizedClient(permissions: [Permissions.ServiceView, Permissions.ServiceManage]);

    [Fact]
    public async Task Smoke_Returns_Ok_With_Ticket_Count()
    {
        using var client = ServiceClient();

        var response = await client.GetAsync("/api/service/smoke");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("ok").GetBoolean().Should().BeTrue();
        body.TryGetProperty("ticketCount", out _).Should().BeTrue();
    }

    [Fact]
    public async Task CreateTicket_Then_List_Returns_It()
    {
        using var client = ServiceClient();

        var create = await client.PostAsJsonAsync("/api/service/tickets",
            new ServiceTicketCreateDto(_customerId, "Engine noise", "Rattling on cold start",
                ServiceTicketPriority.High, false, null, null, null, null, null, "Customer waiting"));
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await create.Content.ReadFromJsonAsync<ServiceTicketDto>(Json);
        created!.Subject.Should().Be("Engine noise");
        created.Status.Should().Be(ServiceTicketStatus.Open);

        var list = await client.GetFromJsonAsync<JsonElement>("/api/service/tickets?page=1&pageSize=20", Json);
        list.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Ticket_List_Respects_Company_Filter_Over_Http()
    {
        using var client = ServiceClient();
        await client.PostAsJsonAsync("/api/service/tickets",
            new ServiceTicketCreateDto(_customerId, "Company One Ticket", null,
                ServiceTicketPriority.Normal, false, null, null, null, null, null, null));

        // Seed a ticket owned by a different company directly (bypasses HTTP auth).
        using (var scope = _factory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ServiceTickets.Add(new ServiceTicket
            {
                CompanyId = 2, CustomerId = _customerId, Subject = "Other Company Ticket",
                Status = ServiceTicketStatus.Open, OpenedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow, CreatedBy = "seed"
            });
            await db.SaveChangesAsync();
        }

        var list = await client.GetFromJsonAsync<JsonElement>("/api/service/tickets?page=1&pageSize=50", Json);
        list.GetProperty("totalCount").GetInt32().Should().Be(1);
        list.GetProperty("items")[0].GetProperty("subject").GetString().Should().Be("Company One Ticket");
    }

    [Fact]
    public async Task StatusChange_To_Resolved_Requires_Resolution_Notes()
    {
        using var client = ServiceClient();
        var create = await client.PostAsJsonAsync("/api/service/tickets",
            new ServiceTicketCreateDto(_customerId, "Brake pad warranty", "Squeaking",
                ServiceTicketPriority.Normal, true, "WARR-001", null, null, null, null, null));
        var ticket = await create.Content.ReadFromJsonAsync<ServiceTicketDto>(Json);

        var missing = await client.PostAsJsonAsync($"/api/service/tickets/{ticket!.Id}/status",
            new ServiceTicketStatusChangeDto(ServiceTicketStatus.Resolved, null));
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var ok = await client.PostAsJsonAsync($"/api/service/tickets/{ticket.Id}/status",
            new ServiceTicketStatusChangeDto(ServiceTicketStatus.Resolved, "Replaced brake pads"));
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Closed_Ticket_Cannot_Transition_Again_Over_Http()
    {
        using var client = ServiceClient();
        var create = await client.PostAsJsonAsync("/api/service/tickets",
            new ServiceTicketCreateDto(_customerId, "Squeaky brakes", null,
                ServiceTicketPriority.Normal, false, null, null, null, null, null, null));
        var ticket = await create.Content.ReadFromJsonAsync<ServiceTicketDto>(Json);

        var close = await client.PostAsJsonAsync($"/api/service/tickets/{ticket!.Id}/status",
            new ServiceTicketStatusChangeDto(ServiceTicketStatus.Closed, "Done, closing"));
        close.StatusCode.Should().Be(HttpStatusCode.OK);

        var reopen = await client.PostAsJsonAsync($"/api/service/tickets/{ticket.Id}/status",
            new ServiceTicketStatusChangeDto(ServiceTicketStatus.Open, null));
        reopen.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
