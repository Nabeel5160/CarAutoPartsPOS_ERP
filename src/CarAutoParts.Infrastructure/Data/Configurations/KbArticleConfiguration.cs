using CarAutoParts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarAutoParts.Infrastructure.Data.Configurations;

public class KbArticleConfiguration : IEntityTypeConfiguration<KbArticle>
{
    public void Configure(EntityTypeBuilder<KbArticle> builder)
    {
        builder.ToTable("KbArticles");
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(80);
        builder.Property(x => x.Body).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.Tags).HasMaxLength(200);
        builder.HasIndex(x => new { x.CompanyId, x.Title });
        builder.HasIndex(x => new { x.CompanyId, x.IsPublished });
    }
}
