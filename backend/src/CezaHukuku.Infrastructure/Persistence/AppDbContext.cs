using CezaHukuku.Domain.Decisions;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Domain.Personal;
using CezaHukuku.Infrastructure.Identity;
using CezaHukuku.Infrastructure.Search;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<LawGroup> LawGroups => Set<LawGroup>();
    public DbSet<Law> Laws => Set<Law>();
    public DbSet<LawNode> LawNodes => Set<LawNode>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<ArticleVersion> ArticleVersions => Set<ArticleVersion>();
    public DbSet<EditorialNote> EditorialNotes => Set<EditorialNote>();
    public DbSet<ArticleLink> ArticleLinks => Set<ArticleLink>();
    public DbSet<ArticleCitation> ArticleCitations => Set<ArticleCitation>();
    public DbSet<CourtDecision> CourtDecisions => Set<CourtDecision>();
    public DbSet<ArticleDecision> ArticleDecisions => Set<ArticleDecision>();
    public DbSet<Highlight> Highlights => Set<Highlight>();
    public DbSet<PersonalNote> PersonalNotes => Set<PersonalNote>();
    public DbSet<SearchDoc> SearchDocs => Set<SearchDoc>();

    /// <summary>
    /// Kaydı yapar, ardından değişen içeriğin arama indeksini günceller (<see cref="SearchIndexer"/>): arama her
    /// zaman kaydedilmiş içeriği bulur, uç noktaların ayrıca indeksle uğraşması gerekmez.
    /// </summary>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var changes = SearchIndexChanges.Collect(ChangeTracker);
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        if (!changes.IsEmpty)
            await SearchIndexer.ApplyAsync(this, changes, cancellationToken);
        return result;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        SaveChangesAsync(acceptAllChangesOnSuccess).GetAwaiter().GetResult();

    /// <summary>
    /// Üretilen arama satırlarını yazar (yeniden indekslemeyi tetiklemeden) ve izlemeden çıkarır; yalnız
    /// <see cref="SearchIndexer"/> kullanır.
    /// </summary>
    internal async Task SaveSearchDocsAsync(CancellationToken cancellationToken)
    {
        await base.SaveChangesAsync(true, cancellationToken);
        foreach (var entry in ChangeTracker.Entries<SearchDoc>().ToList())
            entry.State = EntityState.Detached;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Identity tablolarına kısa, snake_case isimler (sütunlar naming convention ile snake_case olur).
        builder.Entity<ApplicationUser>(b =>
        {
            b.ToTable("users");
            b.Property(u => u.DisplayName).HasMaxLength(200);
        });
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
