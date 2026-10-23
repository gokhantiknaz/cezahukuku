using CezaHukuku.Domain.Decisions;
using CezaHukuku.Domain.Laws;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CezaHukuku.Infrastructure.Persistence.Configurations;

internal sealed class CourtDecisionConfiguration : IEntityTypeConfiguration<CourtDecision>
{
    public void Configure(EntityTypeBuilder<CourtDecision> b)
    {
        b.HasQueryFilter(e => e.DeletedAt == null);
        b.Property(e => e.Court).HasConversion<string>().HasMaxLength(32);
        b.Property(e => e.Kind).HasConversion<string>().HasMaxLength(20);
        b.Property(e => e.Outcome).HasConversion<string>().HasMaxLength(20);
        b.Property(e => e.Visibility).HasConversion<string>().HasMaxLength(16);
        b.Property(e => e.Chamber).HasMaxLength(200);
        b.Property(e => e.CaseNumber).HasMaxLength(50);
        b.Property(e => e.DecisionNumber).HasMaxLength(50);
        b.Property(e => e.GazetteNumber).HasMaxLength(50);
        b.Property(e => e.Summary).HasMaxLength(4000);
        b.Property(e => e.SourceUrl).HasMaxLength(1000);
        b.HasIndex(e => new { e.Court, e.DecisionDate });
    }
}

internal sealed class ArticleDecisionConfiguration : IEntityTypeConfiguration<ArticleDecision>
{
    public void Configure(EntityTypeBuilder<ArticleDecision> b)
    {
        b.HasKey(e => new { e.ArticleId, e.DecisionId });
        b.HasOne<Article>().WithMany().HasForeignKey(e => e.ArticleId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<CourtDecision>().WithMany().HasForeignKey(e => e.DecisionId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(e => e.DecisionId);
    }
}
