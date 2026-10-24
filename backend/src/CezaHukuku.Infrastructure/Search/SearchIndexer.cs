using System.Text;
using CezaHukuku.Domain.Decisions;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Domain.Personal;
using CezaHukuku.Infrastructure.Persistence;
using CezaHukuku.Parsing.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CezaHukuku.Infrastructure.Search;

/// <summary>Bir kayıt işleminde arama indeksini etkileyen kaynaklar.</summary>
public sealed class SearchIndexChanges
{
    public HashSet<Guid> Laws { get; } = [];
    public HashSet<Guid> Articles { get; } = [];
    public HashSet<Guid> Decisions { get; } = [];
    public HashSet<Guid> PersonalNotes { get; } = [];
    public HashSet<Guid> Highlights { get; } = [];

    public bool IsEmpty =>
        Laws.Count == 0 && Articles.Count == 0 && Decisions.Count == 0 && PersonalNotes.Count == 0 && Highlights.Count == 0;

    /// <summary>Kaydedilmek üzere olan (eklenen, değişen, silinen) varlıklardan etkilenen kaynakları çıkarır.</summary>
    public static SearchIndexChanges Collect(ChangeTracker tracker)
    {
        tracker.DetectChanges();
        var changes = new SearchIndexChanges();
        foreach (var entry in tracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;
            switch (entry.Entity)
            {
                // Kanun, düğüm ve madde değişince görünürlük, başlık ya da adres değişmiş olabilir: kanun baştan indekslenir.
                case Law law: changes.Laws.Add(law.Id); break;
                case LawNode node: changes.Laws.Add(node.LawId); break;
                case Article article: changes.Laws.Add(article.LawId); break;
                case ArticleVersion version: changes.Articles.Add(version.ArticleId); break;
                case EditorialNote note: changes.Laws.Add(note.LawId); break;
                case CourtDecision decision: changes.Decisions.Add(decision.Id); break;
                case ArticleDecision link: changes.Decisions.Add(link.DecisionId); break;
                case PersonalNote note: changes.PersonalNotes.Add(note.Id); break;
                case Highlight highlight: changes.Highlights.Add(highlight.Id); break;
            }
        }
        return changes;
    }
}

/// <summary>
/// Arama indeksini (search_docs) kaynaklardan üretir. AppDbContext her kayıttan sonra etkilenen kaynakları burada
/// yeniden indeksler: kanun değişikliği kanunun bütün madde ve notlarını, karar değişikliği kararı, kişisel kayıt
/// yalnız kendisini. Kanunlar küçük olduğundan (TCK ≈ 350 madde) kanunu baştan indekslemek yeterince hızlıdır.
/// </summary>
public static class SearchIndexer
{
    private static readonly SearchKind[] LawKinds =
        [SearchKind.Article, SearchKind.Gerekce, SearchKind.Aciklama, SearchKind.DersNotu, SearchKind.GenelGerekce];

    private static readonly SearchKind[] DecisionKinds = [SearchKind.Ictihat, SearchKind.NormDenetimi];

    public static async Task ApplyAsync(AppDbContext db, SearchIndexChanges changes, CancellationToken ct = default)
    {
        var lawIds = new HashSet<Guid>(changes.Laws);
        if (changes.Articles.Count > 0)
        {
            lawIds.UnionWith(await db.Articles.IgnoreQueryFilters()
                .Where(a => changes.Articles.Contains(a.Id)).Select(a => a.LawId).ToListAsync(ct));
        }
        var decisionIds = new HashSet<Guid>(changes.Decisions);
        var noteIds = new HashSet<Guid>(changes.PersonalNotes);
        var highlightIds = new HashSet<Guid>(changes.Highlights);

        var docs = new List<SearchDoc>();
        foreach (var lawId in lawIds)
        {
            await db.SearchDocs.Where(d => LawKinds.Contains(d.Kind) && d.LawIds.Contains(lawId)).ExecuteDeleteAsync(ct);
            docs.AddRange(await BuildLawAsync(db, lawId, ct));

            // Kanunun adresi ya da maddelerin görünürlüğü değişmiş olabilir: bağlı karar ve kişisel kayıtlar da yenilenir.
            var articleIds = db.Articles.IgnoreQueryFilters().Where(a => a.LawId == lawId).Select(a => a.Id);
            decisionIds.UnionWith(await db.ArticleDecisions.Where(l => articleIds.Contains(l.ArticleId)).Select(l => l.DecisionId).ToListAsync(ct));
            noteIds.UnionWith(await db.PersonalNotes.Where(n => articleIds.Contains(n.ArticleId)).Select(n => n.Id).ToListAsync(ct));
            highlightIds.UnionWith(await db.Highlights.Where(h => articleIds.Contains(h.ArticleId)).Select(h => h.Id).ToListAsync(ct));
        }

        if (decisionIds.Count > 0)
        {
            await db.SearchDocs.Where(d => DecisionKinds.Contains(d.Kind) && decisionIds.Contains(d.SourceId)).ExecuteDeleteAsync(ct);
            docs.AddRange(await BuildDecisionsAsync(db, decisionIds, ct));
        }
        if (noteIds.Count > 0 || highlightIds.Count > 0)
        {
            await db.SearchDocs.Where(d =>
                    (d.Kind == SearchKind.Note && noteIds.Contains(d.SourceId))
                    || (d.Kind == SearchKind.Highlight && highlightIds.Contains(d.SourceId)))
                .ExecuteDeleteAsync(ct);
            docs.AddRange(await BuildPersonalAsync(db, noteIds, highlightIds, ct));
        }

        if (docs.Count == 0)
            return;
        foreach (var doc in docs)
            Normalize(doc);
        db.SearchDocs.AddRange(docs);
        await db.SaveSearchDocsAsync(ct);
    }

    /// <summary>İndeksi baştan kurar (ilk kurulum, yönetim panelindeki "Arama indeksini yenile").</summary>
    public static async Task<int> RebuildAllAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.SearchDocs.ExecuteDeleteAsync(ct);
        var changes = new SearchIndexChanges();
        changes.Laws.UnionWith(await db.Laws.Select(l => l.Id).ToListAsync(ct));
        changes.Decisions.UnionWith(await db.CourtDecisions.Select(d => d.Id).ToListAsync(ct));
        changes.PersonalNotes.UnionWith(await db.PersonalNotes.Select(n => n.Id).ToListAsync(ct));
        changes.Highlights.UnionWith(await db.Highlights.Select(h => h.Id).ToListAsync(ct));
        await ApplyAsync(db, changes, ct);
        return await db.SearchDocs.CountAsync(ct);
    }

    private static async Task<List<SearchDoc>> BuildLawAsync(AppDbContext db, Guid lawId, CancellationToken ct)
    {
        var law = await db.Laws.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lawId, ct);
        if (law is null)
            return []; // silinmiş kanun: satırları silindi, yenisi üretilmez

        var nodes = await db.LawNodes.AsNoTracking().Where(n => n.LawId == lawId).ToListAsync(ct);
        var articles = await db.Articles.AsNoTracking().Where(a => a.LawId == lawId).ToListAsync(ct);
        var versions = (await db.ArticleVersions.AsNoTracking()
                .Where(v => db.Articles.Any(a => a.Id == v.ArticleId && a.LawId == lawId))
                .ToListAsync(ct))
            .ToLookup(v => v.ArticleId);
        var notes = await db.EditorialNotes.AsNoTracking().Where(n => n.LawId == lawId).ToListAsync(ct);
        var outline = LawOutline.Build(law, nodes, articles, includePrivate: true);
        var label = LawLabel(law);

        var docs = new List<SearchDoc>();
        foreach (var item in outline.ReadingOrder)
        {
            var article = item.Article;
            var version = ArticleVersion.Select(versions[article.Id], null);
            docs.Add(new SearchDoc
            {
                Kind = SearchKind.Article,
                SourceId = article.Id,
                Visibility = item.EffectiveVisibility,
                LawIds = [law.Id],
                NodeIds = [.. outline.PathOf(article).Select(n => n.Id)],
                Title = ArticleTitle(article),
                Context = label,
                Body = version is null ? "" : ArticleText(version),
                Url = ArticleUrl(law, article),
                Date = version?.ValidFrom,
            });
        }

        var visibleArticles = outline.ReadingOrder.ToDictionary(a => a.Article.Id);
        foreach (var note in notes)
        {
            var kind = note.Kind switch
            {
                EditorialNoteKind.Gerekce => SearchKind.Gerekce,
                EditorialNoteKind.Aciklama => SearchKind.Aciklama,
                EditorialNoteKind.DersNotu => SearchKind.DersNotu,
                _ => SearchKind.GenelGerekce,
            };
            if (note.ArticleId is { } articleId)
            {
                if (!visibleArticles.TryGetValue(articleId, out var item))
                    continue; // silinmiş maddenin notu
                docs.Add(new SearchDoc
                {
                    Kind = kind,
                    SourceId = note.Id,
                    Visibility = item.EffectiveVisibility.Narrow(note.Visibility),
                    LawIds = [law.Id],
                    NodeIds = [.. outline.PathOf(item.Article).Select(n => n.Id)],
                    Title = note.Title ?? $"{KindLabel(kind)} — Madde {item.Article.Number}",
                    Context = label,
                    Body = note.PlainText,
                    Url = $"{ArticleUrl(law, item.Article)}?sekme={(kind == SearchKind.Gerekce ? "gerekce" : "aciklama")}",
                });
            }
            else
            {
                docs.Add(new SearchDoc
                {
                    Kind = kind,
                    SourceId = note.Id,
                    Visibility = law.Visibility.Narrow(note.Visibility),
                    LawIds = [law.Id],
                    Title = note.Title ?? KindLabel(kind),
                    Context = label,
                    Body = note.PlainText,
                    Url = $"/{law.Slug}/{(kind == SearchKind.GenelGerekce ? "genel-gerekce" : "ders-notlari")}",
                });
            }
        }
        return docs;
    }

    private static async Task<List<SearchDoc>> BuildDecisionsAsync(AppDbContext db, HashSet<Guid> ids, CancellationToken ct)
    {
        var decisions = await db.CourtDecisions.AsNoTracking().Where(d => ids.Contains(d.Id)).ToListAsync(ct);
        var links = (await db.ArticleDecisions.AsNoTracking().Where(l => ids.Contains(l.DecisionId)).ToListAsync(ct))
            .ToLookup(l => l.DecisionId);
        var articleIds = links.SelectMany(g => g.Select(l => l.ArticleId)).Distinct().ToList();
        var articles = await db.Articles.AsNoTracking().Where(a => articleIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        var lawIds = articles.Values.Select(a => a.LawId).Distinct().ToList();
        var laws = await db.Laws.AsNoTracking().Where(l => lawIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var nodes = (await db.LawNodes.AsNoTracking().Where(n => lawIds.Contains(n.LawId)).ToListAsync(ct)).ToLookup(n => n.LawId);
        var outlines = laws.Values.ToDictionary(l => l.Id, l => LawOutline.Build(l, nodes[l.Id], [], includePrivate: true));

        var docs = new List<SearchDoc>();
        foreach (var decision in decisions)
        {
            var targets = links[decision.Id]
                .Select(l => articles.GetValueOrDefault(l.ArticleId))
                .Where(a => a is not null && laws.ContainsKey(a.LawId))
                .Select(a => (Article: a!, Law: laws[a!.LawId], Visibility: outlines[a!.LawId].EffectiveVisibilityOf(a)))
                .OrderBy(t => t.Law.SortOrder).ThenBy(t => t.Article.SortOrder)
                .ToList();
            if (targets.Count == 0)
                continue; // hiçbir maddeye bağlı değil: gösterilecek sayfası yok

            // Anonim kullanıcı kararı ancak herkese açık bir maddede görebilir; bağlantı o maddeye verilir.
            var publicIndex = targets.FindIndex(t => t.Visibility == Visibility.Public);
            var target = targets[Math.Max(publicIndex, 0)];
            var kind = decision.Kind == DecisionKind.NormDenetimi ? SearchKind.NormDenetimi : SearchKind.Ictihat;
            docs.Add(new SearchDoc
            {
                Kind = kind,
                SourceId = decision.Id,
                Visibility = decision.Visibility == Visibility.Public && publicIndex >= 0 ? Visibility.Public : Visibility.Private,
                LawIds = [.. targets.Select(t => t.Law.Id).Distinct()],
                NodeIds = [.. targets.SelectMany(t => outlines[t.Law.Id].PathOf(t.Article).Select(n => n.Id)).Distinct()],
                Title = DecisionCitation(decision),
                Context = string.Join(", ", targets.Take(3).Select(t => $"{LawLabel(t.Law)} {t.Article.Number}")),
                Body = string.Join("\n", new[] { decision.Summary, decision.PlainText }.Where(s => !string.IsNullOrWhiteSpace(s))),
                Url = $"{ArticleUrl(target.Law, target.Article)}?sekme={(kind == SearchKind.NormDenetimi ? "norm" : "ictihatlar")}",
                Date = decision.DecisionDate,
            });
        }
        return docs;
    }

    private static async Task<List<SearchDoc>> BuildPersonalAsync(
        AppDbContext db, HashSet<Guid> noteIds, HashSet<Guid> highlightIds, CancellationToken ct)
    {
        var notes = await db.PersonalNotes.AsNoTracking().Where(n => noteIds.Contains(n.Id)).ToListAsync(ct);
        var highlights = await db.Highlights.AsNoTracking().Where(h => highlightIds.Contains(h.Id)).ToListAsync(ct);
        var articleIds = notes.Select(n => n.ArticleId).Concat(highlights.Select(h => h.ArticleId)).Distinct().ToList();
        var articles = await db.Articles.AsNoTracking().Where(a => articleIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        var lawIds = articles.Values.Select(a => a.LawId).Distinct().ToList();
        var laws = await db.Laws.AsNoTracking().Where(l => lawIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);

        SearchDoc? Doc(SearchKind kind, Guid sourceId, Guid ownerId, Guid articleId, string body)
        {
            if (!articles.TryGetValue(articleId, out var article) || !laws.TryGetValue(article.LawId, out var law))
                return null;
            return new SearchDoc
            {
                Kind = kind,
                SourceId = sourceId,
                OwnerId = ownerId,
                Visibility = Visibility.Private,
                LawIds = [law.Id],
                Title = ArticleTitle(article),
                Context = LawLabel(law),
                Body = body,
                Url = ArticleUrl(law, article),
            };
        }

        return
        [
            .. notes.Select(n => Doc(SearchKind.Note, n.Id, n.OwnerId, n.ArticleId, n.Text)).OfType<SearchDoc>(),
            .. highlights.Select(h => Doc(SearchKind.Highlight, h.Id, h.OwnerId, h.ArticleId,
                string.Join(" … ", h.Segments.Select(s => s.Quote)))).OfType<SearchDoc>(),
        ];
    }

    /// <summary>Aranacak metinlerin Türkçe küçük harfli ve aksansız biçimleri (tsvector bunlardan üretilir).</summary>
    private static void Normalize(SearchDoc doc)
    {
        doc.TitleNorm = TurkishText.Normalize(doc.Title);
        doc.BodyNorm = TurkishText.Normalize(doc.Body);
        doc.TitleFold = TurkishText.Fold(doc.Title);
        doc.BodyFold = TurkishText.Fold(doc.Body);
    }

    private static string LawLabel(Law law) => law.ShortName ?? law.Number;

    private static string ArticleUrl(Law law, Article article) => $"/{law.Slug}/{article.Slug}";

    private static string ArticleTitle(Article article) =>
        string.IsNullOrWhiteSpace(article.Title) ? $"Madde {article.Number}" : $"Madde {article.Number} — {article.Title}";

    /// <summary>Madde metni mevzuat biçiminde: "(1) …", "a) …".</summary>
    private static string ArticleText(ArticleVersion version)
    {
        if (version.Paragraphs.Count == 0)
            return version.IsRepealed ? "Mülga" : "";
        var text = new StringBuilder();
        foreach (var paragraph in version.Paragraphs)
        {
            text.AppendLine(paragraph.Number is { } n ? $"({n}) {paragraph.Text}" : paragraph.Text);
            foreach (var item in paragraph.Items)
                text.AppendLine(item.Label is null ? item.Text : $"{item.Label} {item.Text}");
        }
        return text.ToString().TrimEnd();
    }

    private static string KindLabel(SearchKind kind) => kind switch
    {
        SearchKind.Gerekce => "Gerekçe",
        SearchKind.Aciklama => "Açıklama",
        SearchKind.DersNotu => "Ders notu",
        SearchKind.GenelGerekce => "Genel gerekçe",
        _ => kind.ToString(),
    };

    private static readonly Dictionary<Court, string> CourtLabels = new()
    {
        [Court.Yargitay] = "Yargıtay",
        [Court.AnayasaMahkemesi] = "Anayasa Mahkemesi",
        [Court.Aihm] = "Avrupa İnsan Hakları Mahkemesi",
        [Court.Danistay] = "Danıştay",
        [Court.BolgeAdliyeMahkemesi] = "Bölge Adliye Mahkemesi",
        [Court.UyusmazlikMahkemesi] = "Uyuşmazlık Mahkemesi",
        [Court.Diger] = "Diğer",
    };

    /// <summary>Künye: "Yargıtay Ceza Genel Kurulu, 12.03.2020, E. 2019/1-100, K. 2020/1" (web'deki decisionCitation ile aynı).</summary>
    private static string DecisionCitation(CourtDecision d) =>
        string.Join(", ", new[]
        {
            string.Join(" ", new[] { CourtLabels[d.Court], d.Chamber }.Where(s => !string.IsNullOrWhiteSpace(s))),
            d.DecisionDate?.ToString("dd.MM.yyyy"),
            d.CaseNumber is null ? null : $"E. {d.CaseNumber}",
            d.DecisionNumber is null ? null : $"K. {d.DecisionNumber}",
        }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
