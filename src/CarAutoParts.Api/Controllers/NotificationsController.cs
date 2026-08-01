using CarAutoParts.Api.Contracts;
using CarAutoParts.Application.Common;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarAutoParts.Api.Controllers;

[Authorize]
[Route("api/notifications")]
public class NotificationsController : ApiControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications) => _notifications = notifications;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] QuerySpec query,
        [FromQuery] bool unreadOnly = false,
        CancellationToken ct = default)
        => Ok(await _notifications.GetNotificationsAsync(query, unreadOnly, ct));

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
        => Ok(new { count = await _notifications.GetUnreadCountAsync(ct) });

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct)
    {
        await _notifications.MarkAsReadAsync(id, ct);
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateNotificationRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<NotificationType>(request.Type, ignoreCase: true, out var type))
            return BadRequest(new { error = "Invalid notification type." });

        await _notifications.CreateNotificationAsync(
            type, request.Title, request.Message, request.EntityType, request.EntityId, ct);
        return Ok();
    }
}
