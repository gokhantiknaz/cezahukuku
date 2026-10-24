using CezaHukuku.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CezaHukuku.Infrastructure.Persistence.Configurations;

internal sealed class SearchDocConfiguration : IEntityTypeConfiguration<SearchDoc>
{
    // Kök bulan Türkçe sözlük (öldürmenin → öldürme) ve aksansız 'simple' sözlük ("oldurme") aynı vektörde; sorgu
    // her terimi ikisinde de arar. Metinler uygulamada küçültüldüğünden veritabanının (Türkçe olmayan) lower() kuralı
    // devreye girmez.
    private const string SearchVector = """
        setweight(to_tsvector('turkish'::regconfig, title_norm), 'A')
        || setweight(to_tsvector('turkish'::regconfig, body_norm), 'B')
        || setweight(to_tsvector('simple'::regconfig, title_fold), 'A')
        || setweight(to_tsvector('simple'::regconfig, body_fold), 'B')
        """;

    public void Configure(EntityTypeBuilder<SearchDoc> b)
    {
        b.Property(e => e.Kind).HasConversion<string>().HasMaxLength(16);
        b.Property(e => e.Visibility).HasConversion<string>().HasMaxLength(16);
        b.Property(e => e.Title).HasMaxLength(1000);
        b.Property(e => e.Context).HasMaxLength(500);
        b.Property(e => e.Url).HasMaxLength(500);
        b.Property(e => e.Search).HasComputedColumnSql(SearchVector, stored: true);

        b.HasIndex(e => new { e.Kind, e.SourceId }).IsUnique();
        b.HasIndex(e => e.OwnerId);
        b.HasIndex(e => e.Search).HasMethod("gin");
        b.HasIndex(e => e.LawIds).HasMethod("gin");
        b.HasIndex(e => e.NodeIds).HasMethod("gin");
    }
}
