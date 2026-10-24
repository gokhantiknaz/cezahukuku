using System.Text.Json;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Domain.Personal;
using CezaHukuku.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CezaHukuku.Infrastructure.Persistence.Configurations;

internal sealed class HighlightConfiguration : IEntityTypeConfiguration<Highlight>
{
    public void Configure(EntityTypeBuilder<Highlight> b)
    {
        b.HasQueryFilter(e => e.DeletedAt == null);
        b.Property(e => e.Color).HasConversion<string>().HasMaxLength(16);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.OwnerId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Article>().WithMany().HasForeignKey(e => e.ArticleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ArticleVersion>().WithMany().HasForeignKey(e => e.ArticleVersionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.OwnerId, e.ArticleId });

        // Parçalar vurguyla birlikte okunup yazılır; fıkralar gibi tek jsonb sütunda.
        b.Property(e => e.Segments)
            .HasColumnType("jsonb")
            .HasConversion(
                v => AnchorJson.Serialize(v),
                v => AnchorJson.Deserialize(v),
                new ValueComparer<IReadOnlyList<TextAnchor>>(
                    (x, y) => AnchorJson.Serialize(x) == AnchorJson.Serialize(y),
                    v => AnchorJson.Serialize(v).GetHashCode(),
                    v => AnchorJson.Deserialize(AnchorJson.Serialize(v))));
    }
}

internal sealed class PersonalNoteConfiguration : IEntityTypeConfiguration<PersonalNote>
{
    public void Configure(EntityTypeBuilder<PersonalNote> b)
    {
        b.HasQueryFilter(e => e.DeletedAt == null);
        b.Property(e => e.Text).HasMaxLength(PersonalNote.MaxTextLength);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.OwnerId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Article>().WithMany().HasForeignKey(e => e.ArticleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Highlight>().WithMany().HasForeignKey(e => e.HighlightId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.OwnerId, e.ArticleId });
        b.HasIndex(e => e.HighlightId).IsUnique().HasFilter(SoftDelete.ActiveFilter);
    }
}

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> b)
    {
        b.HasQueryFilter(e => e.DeletedAt == null);
        b.Property(e => e.Kind).HasConversion<string>().HasMaxLength(16);
        b.Property(e => e.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(e => e.Title).HasMaxLength(Attachment.MaxTitleLength);
        b.Property(e => e.FileName).HasMaxLength(300);
        b.Property(e => e.ContentType).HasMaxLength(100);
        b.Property(e => e.StorageKey).HasMaxLength(300);
        b.Property(e => e.Error).HasMaxLength(500);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.OwnerId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Article>().WithMany().HasForeignKey(e => e.ArticleId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.OwnerId, e.ArticleId });
        b.HasIndex(e => e.Status);
    }
}

internal sealed class AttachmentSegmentConfiguration : IEntityTypeConfiguration<AttachmentSegment>
{
    public void Configure(EntityTypeBuilder<AttachmentSegment> b)
    {
        b.HasOne<Attachment>().WithMany().HasForeignKey(e => e.AttachmentId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(e => new { e.AttachmentId, e.Index });
    }
}

internal static class AnchorJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(IReadOnlyList<TextAnchor>? anchors) => JsonSerializer.Serialize(anchors ?? [], Options);

    public static IReadOnlyList<TextAnchor> Deserialize(string json) =>
        JsonSerializer.Deserialize<List<TextAnchor>>(json, Options) ?? [];
}
