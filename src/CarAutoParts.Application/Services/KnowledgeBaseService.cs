using CarAutoParts.Application.Common;
using CarAutoParts.Application.DTOs.Service;
using CarAutoParts.Application.Interfaces;
using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarAutoParts.Application.Services;

public interface IKnowledgeBaseService
{
    Task<IReadOnlyList<KbArticleDto>> ListAsync(string? search = null, bool publishedOnly = false, CancellationToken ct = default);
    Task<KbArticleDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Result<KbArticleDto>> UpsertAsync(KbArticleUpsertDto dto, CancellationToken ct = default);
    Task<Result> SoftDeleteAsync(int id, CancellationToken ct = default);
}

/// <summary>Thin internal KB for Service Light — not a customer self-service portal.</summary>
public sealed class KnowledgeBaseService : IKnowledgeBaseService
{
    private readonly IRepository<KbArticle> _articles;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentCompanyContext _company;
    private readonly ICurrentUserService _user;

    public KnowledgeBaseService(
        IRepository<KbArticle> articles,
        IUnitOfWork uow,
        ICurrentCompanyContext company,
        ICurrentUserService user)
    {
        _articles = articles;
        _uow = uow;
        _company = company;
        _user = user;
    }

    private int? TryCompanyId() => _company.CompanyId is int id && id > 0 ? id : null;

    public async Task<IReadOnlyList<KbArticleDto>> ListAsync(
        string? search = null, bool publishedOnly = false, CancellationToken ct = default)
    {
        var q = _articles.Query().AsNoTracking();
        if (publishedOnly)
            q = q.Where(a => a.IsPublished);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(a =>
                a.Title.Contains(term) ||
                (a.Category != null && a.Category.Contains(term)) ||
                (a.Tags != null && a.Tags.Contains(term)) ||
                a.Body.Contains(term));
        }

        var list = await q.OrderBy(a => a.Title).Take(100).ToListAsync(ct);
        return list.Select(Map).ToList();
    }

    public async Task<KbArticleDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var a = await _articles.Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return a is null ? null : Map(a);
    }

    public async Task<Result<KbArticleDto>> UpsertAsync(KbArticleUpsertDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result<KbArticleDto>.Failure("Title is required.");
        if (string.IsNullOrWhiteSpace(dto.Body))
            return Result<KbArticleDto>.Failure("Body is required.");

        var companyId = TryCompanyId();
        if (companyId is null)
            return Result<KbArticleDto>.Failure("Company context is required.");

        KbArticle entity;
        if (dto.Id is int id)
        {
            var existing = await _articles.Query().FirstOrDefaultAsync(a => a.Id == id, ct);
            if (existing is null)
                return Result<KbArticleDto>.Failure("Article not found.");
            entity = existing;
            entity.Title = dto.Title.Trim();
            entity.Category = NullIfEmpty(dto.Category);
            entity.Body = dto.Body.Trim();
            entity.Tags = NullIfEmpty(dto.Tags);
            entity.IsPublished = dto.IsPublished;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = _user.CurrentUser?.Username;
        }
        else
        {
            entity = new KbArticle
            {
                CompanyId = companyId.Value,
                Title = dto.Title.Trim(),
                Category = NullIfEmpty(dto.Category),
                Body = dto.Body.Trim(),
                Tags = NullIfEmpty(dto.Tags),
                IsPublished = dto.IsPublished,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _user.CurrentUser?.Username ?? "system"
            };
            _articles.Add(entity);
        }

        await _uow.SaveChangesAsync(ct);
        return Result<KbArticleDto>.Success(Map(entity));
    }

    public async Task<Result> SoftDeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _articles.Query().FirstOrDefaultAsync(a => a.Id == id, ct);
        if (entity is null)
            return Result.Failure("Article not found.");
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = _user.CurrentUser?.Username;
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static KbArticleDto Map(KbArticle a) => new(
        a.Id, a.Title, a.Category, a.Body, a.Tags, a.IsPublished, a.CreatedAt, a.UpdatedAt);
}
