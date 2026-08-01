using CarAutoParts.Application.Common;
using CarAutoParts.Application.Mapping;
using CarAutoParts.Application.Services;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using CarAutoParts.Infrastructure.Repositories;
using FluentAssertions;
using AutoMapper;
using Xunit;

namespace CarAutoParts.Application.Tests;

public class NotificationPagingTests
{
    [Fact]
    public async Task GetNotificationsAsync_pages_beyond_soft_cap()
    {
        await using var db = TestDbContextFactory.Create();
        for (var i = 0; i < 120; i++)
        {
            db.Notifications.Add(new AppNotification
            {
                Type = NotificationType.Success,
                Title = $"N{i}",
                Message = $"Message {i}",
                IsRead = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>()).CreateMapper();
        var svc = new NotificationService(new Repository<AppNotification>(db), uow, mapper);

        var page1 = await svc.GetNotificationsAsync(new QuerySpec { Page = 1, PageSize = 50 });
        page1.TotalCount.Should().Be(120);
        page1.Items.Should().HaveCount(50);
        page1.Page.Should().Be(1);
        page1.PageSize.Should().Be(50);

        var page3 = await svc.GetNotificationsAsync(new QuerySpec { Page = 3, PageSize = 50 });
        page3.Items.Should().HaveCount(20);
        page3.TotalCount.Should().Be(120);
    }

    [Fact]
    public async Task GetNotificationsAsync_unread_filter_respects_total()
    {
        await using var db = TestDbContextFactory.Create();
        db.Notifications.Add(new AppNotification
        {
            Type = NotificationType.LowStock, Title = "A", Message = "a", IsRead = false, CreatedAt = DateTime.UtcNow
        });
        db.Notifications.Add(new AppNotification
        {
            Type = NotificationType.LowStock, Title = "B", Message = "b", IsRead = true, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var uow = new UnitOfWork(db);
        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>()).CreateMapper();
        var svc = new NotificationService(new Repository<AppNotification>(db), uow, mapper);

        var unread = await svc.GetNotificationsAsync(new QuerySpec { Page = 1, PageSize = 25 }, unreadOnly: true);
        unread.TotalCount.Should().Be(1);
        unread.Items.Should().ContainSingle(n => n.Title == "A");
    }
}
