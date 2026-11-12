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

internal sealed class LectureNoteConfiguration : IEntityTypeConfiguration<LectureNote>
{
    public void Configure(EntityTypeBuilder<LectureNote> b)
    {
        b.HasQueryFilter(e => e.DeletedAt == null);
        b.Property(e => e.Title).HasMaxLength(LectureNote.MaxTitleLength);
        b.Property(e => e.Slug).HasMaxLength(120);
        b.Property(e => e.Visibility).HasConversion<string>().HasMaxLength(16);
        b.HasOne<LawGroup>().WithMany().HasForeignKey(e => e.LawGroupId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Law>().WithMany().HasForeignKey(e => e.LawId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Article>().WithMany().HasForeignKey(e => e.ArticleId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => e.Slug).IsUnique().HasFilter(SoftDelete.ActiveFilter);
        b.HasIndex(e => e.LawGroupId);
        b.HasIndex(e => e.LawId);
        b.HasIndex(e => e.ArticleId);
        b.HasOne<LectureCourse>().WithMany().HasForeignKey(e => e.CourseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LectureNote>().WithMany().HasForeignKey(e => e.ParentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.CourseId, e.ParentId, e.SortOrder });
        b.HasIndex(e => e.ParentId);
    }
}

internal sealed class LectureCourseConfiguration : IEntityTypeConfiguration<LectureCourse>
{
    public void Configure(EntityTypeBuilder<LectureCourse> b)
    {
        b.HasQueryFilter(e => e.DeletedAt == null);
        b.Property(e => e.Title).HasMaxLength(LectureCourse.MaxTitleLength);
        b.Property(e => e.Slug).HasMaxLength(120);
        b.Property(e => e.Description).HasMaxLength(LectureCourse.MaxDescriptionLength);
        b.Property(e => e.Visibility).HasConversion<string>().HasMaxLength(16);
        b.HasIndex(e => e.Slug).IsUnique().HasFilter(SoftDelete.ActiveFilter);
    }
}

internal sealed class LectureNoteFileConfiguration : IEntityTypeConfiguration<LectureNoteFile>
{
    public void Configure(EntityTypeBuilder<LectureNoteFile> b)
    {
        b.HasQueryFilter(e => e.DeletedAt == null);
        b.Property(e => e.Title).HasMaxLength(LectureNoteFile.MaxTitleLength);
        b.Property(e => e.FileName).HasMaxLength(300);
        b.Property(e => e.ContentType).HasMaxLength(150);
        b.Property(e => e.StorageKey).HasMaxLength(300);
        b.HasOne<LectureNote>().WithMany().HasForeignKey(e => e.LectureNoteId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => e.LectureNoteId);
    }
}
