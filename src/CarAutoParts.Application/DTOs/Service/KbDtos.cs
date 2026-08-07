namespace CarAutoParts.Application.DTOs.Service;

public record KbArticleDto(
    int Id,
    string Title,
    string? Category,
    string Body,
    string? Tags,
    bool IsPublished,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record KbArticleUpsertDto(
    int? Id,
    string Title,
    string? Category,
    string Body,
    string? Tags,
    bool IsPublished);
