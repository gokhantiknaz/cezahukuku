using System.Text.Json;
using CezaHukuku.Domain.Laws;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CezaHukuku.Infrastructure.Persistence.Configurations;

// Kayıtlar silinmez, DeletedAt ile işaretlenir; sorgular silinmişleri görmez ve tekil indeksler yalnız
// silinmemiş kayıtlarda geçerlidir (silinen kanunun kısa adı yeniden kullanılabilsin).
internal static class SoftDelete
{
    public const string ActiveFilter = "deleted_at IS NULL";
}

internal sealed class LawGroupConfiguration : IEntityTypeConfiguration<LawGroup>
{
    public void Configure(EntityTypeBuilder<LawGroup> b)
    {
        b.HasQueryFilter(e => e.DeletedAt == null);
        b.Property(e => e.Slug).HasMaxLength(100);
        b.Property(e => e.Title).HasMaxLength(200);
        b.HasIndex(e => e.Slug).IsUnique().HasFilter(SoftDelete.ActiveFilter);
    }
}

internal sealed class LawConfiguration : IEntityTypeConfiguration<Law>
{
    public void Configure(EntityTypeBuilder<Law> b)
    {
        b.HasQueryFilter(e => e.DeletedAt == null);
        b.Property(e => e.Slug).HasMaxLength(100);
        b.Property(e => e.Number).HasMaxLength(50);
        b.Property(e => e.ShortName).HasMaxLength(50);
        b.Property(e => e.Name).HasMaxLength(500);
        b.Property(e => e.GazetteNumber).HasMaxLength(50);
        b.Property(e => e.Visibility).HasConversion<string>().HasMaxLength(16);
        b.HasOne<LawGroup>().WithMany().HasForeignKey(e => e.GroupId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => e.Slug).IsUnique().HasFilter(SoftDelete.ActiveFilter);
    }
}

internal sealed class LawNodeConfiguration : IEntityTypeConfiguration<LawNode>
{
    public void Configure(EntityTypeBuilder<LawNode> b)
    {
        b.HasQueryFilter(e => e.DeletedAt == null);
        b.Property(e => e.Number).HasMaxLength(100);
        b.Property(e => e.Title).HasMaxLength(500);
        b.Property(e => e.Kind).HasConversion<string>().HasMaxLength(16);
        b.Property(e => e.Visibility).HasConversion<string>().HasMaxLength(16);
        b.HasOne<Law>().WithMany().HasForeignKey(e => e.LawId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LawNode>().WithMany().HasForeignKey(e => e.ParentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.LawId, e.ParentId, e.SortOrder });
    }
}

internal sealed class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> b)
    {
        b.HasQueryFilter(e => e.DeletedAt == null);
        b.Property(e => e.Number).HasMaxLength(50);
        b.Property(e => e.Slug).HasMaxLength(100);
        b.Property(e => e.Title).HasMaxLength(500);
        b.Property(e => e.Visibility).HasConversion<string>().HasMaxLength(16);
        b.HasOne<Law>().WithMany().HasForeignKey(e => e.LawId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LawNode>().WithMany().HasForeignKey(e => e.NodeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.LawId, e.Slug }).IsUnique().HasFilter(SoftDelete.ActiveFilter);
        b.HasIndex(e => e.NodeId);
    }
}

internal sealed class ArticleVersionConfiguration : IEntityTypeConfiguration<ArticleVersion>
{
    public void Configure(EntityTypeBuilder<ArticleVersion> b)
    {
        b.HasQueryFilter(e => e.DeletedAt == null);
        b.Property(e => e.AmendedBy).HasMaxLength(500);
        b.Property(e => e.GazetteNumber).HasMaxLength(50);
        b.HasOne<Article>().WithMany().HasForeignKey(e => e.ArticleId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(e => new { e.ArticleId, e.ValidFrom });

        // Fıkralar sürümle birlikte okunup yazılır, içlerinde sorgu yapılmaz (arama ayrı indeksten yapılacak);
        // bu yüzden tek jsonb sütunda tutulur.
        b.Property(e => e.Paragraphs)
            .HasColumnType("jsonb")
            .HasConversion(
                v => ParagraphJson.Serialize(v),
                v => ParagraphJson.Deserialize(v),
                new ValueComparer<IReadOnlyList<Paragraph>>(
                    (x, y) => ParagraphJson.Serialize(x) == ParagraphJson.Serialize(y),
                    v => ParagraphJson.Serialize(v).GetHashCode(),
                    v => ParagraphJson.Deserialize(ParagraphJson.Serialize(v))));
    }
}

internal static class ParagraphJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(IReadOnlyList<Paragraph>? paragraphs) =>
        JsonSerializer.Serialize(paragraphs ?? [], Options);

    public static IReadOnlyList<Paragraph> Deserialize(string json) =>
        JsonSerializer.Deserialize<List<Paragraph>>(json, Options) ?? [];
}
