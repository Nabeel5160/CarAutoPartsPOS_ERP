using CarAutoParts.Application.DTOs.Service;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using CarAutoParts.Infrastructure.Repositories;
using FluentAssertions;
using Moq;
using Xunit;

namespace CarAutoParts.Application.Tests;

public class WarrantyAndAssignTests
{
    [Fact]
    public async Task Create_Warranty_Starts_Submitted_And_Notify_On_Assign()
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        await using var db = SlaPolicyTests.CreateDb(company);
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        db.Customers.Add(new Customer { Id = 1, Name = "C", IsActive = true, CreatedAt = DateTime.UtcNow });
        db.Users.Add(new AppUser { Id = 9, Username = "tech", DisplayName = "Tech", PasswordHash = "x", IsActive = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var notifies = new List<(string Title, string? Entity)>();
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(n => n.CreateNotificationAsync(
                It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationType, string, string, string?, int?, CancellationToken>((_, t, _, e, _, _) => notifies.Add((t, e)))
            .Returns(Task.CompletedTask);

        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.CurrentUser).Returns(new AppUser { Id = 1, Username = "admin" });
        var svc = ServiceTicketTests.CreateSvc(db, company, user.Object, notifications.Object);

        var created = await svc.CreateTicketAsync(new ServiceTicketCreateDto(
            1, "Pad warranty", null, ServiceTicketPriority.High, true, "WR-1", null, null, 9, null, null));
        created.Succeeded.Should().BeTrue();
        created.Data!.IsWarrantyClaim.Should().BeTrue();
        created.Data.WarrantyClaimStatus.Should().Be(WarrantyClaimStatus.Submitted);
        notifies.Should().Contain(n => n.Title.Contains("assigned") && n.Entity == "ServiceTicket");

        var decided = await svc.DecideWarrantyAsync(created.Data.Id, new WarrantyClaimDecisionDto(WarrantyClaimStatus.Approved, "OK"));
        decided.Succeeded.Should().BeTrue();
        decided.Data!.WarrantyClaimStatus.Should().Be(WarrantyClaimStatus.Approved);

        var again = await svc.DecideWarrantyAsync(created.Data.Id, new WarrantyClaimDecisionDto(WarrantyClaimStatus.Rejected, "nope"));
        again.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Update_Notifies_When_Assignee_Changes()
    {
        var company = new CurrentCompanyContext();
        company.Set(1);
        await using var db = SlaPolicyTests.CreateDb(company);
        db.Companies.Add(new Company { Id = 1, Code = "T", Name = "Test", CurrencyCode = "PKR", IsActive = true });
        db.Customers.Add(new Customer { Id = 1, Name = "C", IsActive = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var notifyCount = 0;
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(n => n.CreateNotificationAsync(
                It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback(() => notifyCount++)
            .Returns(Task.CompletedTask);

        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.CurrentUser).Returns(new AppUser { Id = 1, Username = "admin" });
        var svc = ServiceTicketTests.CreateSvc(db, company, user.Object, notifications.Object);

        var created = await svc.CreateTicketAsync(new ServiceTicketCreateDto(
            1, "Unassigned", null, ServiceTicketPriority.Normal, false, null, null, null, null, null, null));
        notifyCount.Should().Be(0);

        var updated = await svc.UpdateTicketAsync(created.Data!.Id, new ServiceTicketUpdateDto(
            "Unassigned", null, ServiceTicketPriority.Normal, false, null, null, null, 5, null, null));
        updated.Succeeded.Should().BeTrue();
        notifyCount.Should().Be(1);
    }
}
