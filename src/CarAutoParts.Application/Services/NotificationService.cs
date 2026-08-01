using AutoMapper;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.System;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using CarAutoParts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

/// <summary>In-app notification management.</summary>
public class NotificationService : INotificationService
{
    private readonly IRepository<AppNotification> _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public NotificationService(
        IRepository<AppNotification> notifications,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<PagedResult<NotificationDto>> GetNotificationsAsync(
        QuerySpec query,
        bool unreadOnly = false,
        CancellationToken ct = default)
    {
        var q = _notifications.Query().Where(n => !n.IsDeleted);
        if (unreadOnly)
            q = q.Where(n => !n.IsRead);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(n => n.Title.Contains(s) || n.Message.Contains(s));
        }

        q = q.OrderByDescending(n => n.CreatedAt);
        var paged = await q.ToPagedResultAsync(query.Page, query.PageSize, ct);
        return new PagedResult<NotificationDto>
        {
            Items = _mapper.Map<List<NotificationDto>>(paged.Items),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(bool unreadOnly = false, CancellationToken ct = default)
    {
        var paged = await GetNotificationsAsync(new QuerySpec { Page = 1, PageSize = 100 }, unreadOnly, ct);
        return paged.Items;
    }

    /// <inheritdoc />
    public async Task MarkAsReadAsync(int id, CancellationToken ct = default)
    {
        var notification = await _notifications.GetByIdAsync(id, ct);
        if (notification is null || notification.IsDeleted) return;

        notification.IsRead = true;
        notification.UpdatedAt = DateTime.UtcNow;
        _notifications.Update(notification);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task CreateNotificationAsync(
        NotificationType type,
        string title,
        string message,
        string? entityType = null,
        int? entityId = null,
        CancellationToken ct = default)
    {
        _notifications.Add(new AppNotification
        {
            Type = type,
            Title = title,
            Message = message,
            RelatedEntityType = entityType,
            RelatedEntityId = entityId,
            IsRead = false
        });
        await _unitOfWork.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> GetUnreadCountAsync(CancellationToken ct = default)
    {
        return await _notifications.Query()
            .CountAsync(n => !n.IsDeleted && !n.IsRead, ct);
    }
}
