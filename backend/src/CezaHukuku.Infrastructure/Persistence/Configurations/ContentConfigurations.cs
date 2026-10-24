using CezaHukuku.Domain.Content;
using CezaHukuku.Domain.Laws;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CezaHukuku.Infrastructure.Persistence.Configurations;

internal sealed class NewsItemConfiguration : IEntityTypeConfiguration<NewsItem>
{
    public void Configure(EntityTypeBuilder<NewsItem> b)
    {
        b.HasQueryFilter(e => e.DeletedAt == null);
        b.Property(e => e.Title).HasMaxLength(NewsItem.MaxTitleLength);
        b.Property(e => e.Summary).HasMaxLength(NewsItem.MaxSummaryLength);
        b.Property(e => e.Url).HasMaxLength(500);
        b.Property(e => e.Visibility).HasConversion<string>().HasMaxLength(16);
        b.HasIndex(e => e.PublishedOn);
    }
}

internal sealed class QuizQuestionConfiguration : IEntityTypeConfiguration<QuizQuestion>
{
    public void Configure(EntityTypeBuilder<QuizQuestion> b)
    {
        b.HasQueryFilter(e => e.DeletedAt == null);
        b.Property(e => e.Text).HasMaxLength(2000);
        b.Property(e => e.Explanation).HasMaxLength(4000);
        b.Property(e => e.Visibility).HasConversion<string>().HasMaxLength(16);
        b.HasOne<Law>().WithMany().HasForeignKey(e => e.LawId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Article>().WithMany().HasForeignKey(e => e.ArticleId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => e.LawId);
    }
}
