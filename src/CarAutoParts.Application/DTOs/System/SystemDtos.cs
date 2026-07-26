using CarAutoParts.Domain.Enums;

namespace CarAutoParts.Application.DTOs.System;

/// <summary>In-app notification.</summary>
public record NotificationDto(
    int Id,
    NotificationType Type,
    string Title,
    string Message,
    bool IsRead,
    string? RelatedEntityType,
    int? RelatedEntityId,
    DateTime CreatedAt);

/// <summary>Audit log entry.</summary>
public record AuditLogDto(
    int Id,
    AuditAction Action,
    string EntityType,
    int? EntityId,
    string? UserName,
    string? OldValues,
    string? NewValues,
    DateTime Timestamp,
    string? IpAddress);
