using System.Net;
using System.Text;
using CezaHukuku.Api.Common;
using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Infrastructure.Attachments;
using CezaHukuku.Infrastructure.Content;
using CezaHukuku.Infrastructure.Persistence;
using CezaHukuku.Parsing.Legislation;
using CezaHukuku.Parsing.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Admin;

/// <summary>
/// Kanunun tam metnini yapıştırarak içe aktarma ya da güncelleme. Önce önizleme, onaylanınca tek işlemde kayıt.
/// Önizleme ve kayıt aynı planı kullanır: plan yapılacak her değişikliği bir listeye ekler; önizleme listeyi
/// çalıştırmaz, içe aktarma çalıştırıp kaydeder. Böylece kaydedilen her zaman önizlemede görülendir.
/// <list type="bullet">
/// <item>Boş kanun: ağaç, maddeler ve ilk sürümler oluşturulur.</item>
/// <item>Dolu kanun (güncelleme): maddeler numarasıyla, düğümler ağaçtaki yeri (tür + sıra adı) ile eşleşir.
/// Değişmeyene dokunulmaz; değişen metin daha yeni bir tarih varsa yeni sürüm olur (eski metin önceki tarihler için
/// kalır), yoksa yürürlükteki sürümde düzeltilir. Metinde olmayan maddeler silinmez, listelenir.</item>
/// </list>
/// </summary>
internal static class AdminImportEndpoints
{
    public static void MapImportEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/laws/{lawId:guid}/import/preview", PreviewAsync).WithName("PreviewLawImport");
        group.MapPost("/laws/{lawId:guid}/import", ImportAsync).WithName("ImportLaw");
        group.MapPost("/import/extract-text", ExtractTextAsync).WithName("ExtractImportText")
            .DisableAntiforgery()
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(MaxImportFileBytes + 1024 * 1024));
    }

    private const long MaxImportFileBytes = 30 * 1024 * 1024;

    /// <summary>
    /// Kanun metnini dosyadan alır (Word .docx, PDF, düz metin); metin içe aktarma kutusuna konup aynı önizlemeyle
    /// içe aktarılır. PDF'te satırlar sayfa genişliğinde kırıldığından kırık satırları birleştirme önerilir.
    /// </summary>
    private static async Task<Results<Ok<ExtractedImportText>, ValidationProblem>> ExtractTextAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0 || file.Length > MaxImportFileBytes)
            return ValidationErrors.Single("file", "Dosya boş ya da çok büyük (en fazla 30 MB).");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        try
        {
            return extension switch
            {
                ".docx" => TypedResults.Ok(new ExtractedImportText(DocxTextExtractor.Extract(buffer), false)),
                ".pdf" => TypedResults.Ok(new ExtractedImportText(string.Join('\n', PdfTextExtractor.ExtractPages(buffer)), true)),
                ".txt" => TypedResults.Ok(new ExtractedImportText(Encoding.UTF8.GetString(buffer.ToArray()), false)),
                ".doc" => ValidationErrors.Single("file", "Eski Word (.doc) biçimi okunamıyor; belgeyi Word'de .docx olarak kaydedip yükleyin."),
                _ => ValidationErrors.Single("file", "Yalnız .docx, .pdf ve .txt dosyası yüklenebilir."),
            };
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException or UglyToad.PdfPig.Core.PdfDocumentFormatException)
        {
            return ValidationErrors.Single("file", "Dosya okunamadı; bozuk ya da parola korumalı olabilir.");
        }
    }

    private static async Task<Results<Ok<LawImportPreview>, NotFound>> PreviewAsync(
        Guid lawId, ImportLawTextRequest request, AppDbContext db)
    {
        var law = await db.Laws.FirstOrDefaultAsync(l => l.Id == lawId);
        return law is null ? TypedResults.NotFound() : TypedResults.Ok((await PlanAsync(law, request, db)).Preview);
    }

    private static async Task<Results<Ok<LawImportResult>, NotFound, ValidationProblem>> ImportAsync(
        Guid lawId, ImportLawTextRequest request, AppDbContext db)
    {
        var law = await db.Laws.FirstOrDefaultAsync(l => l.Id == lawId);
        if (law is null)
            return TypedResults.NotFound();

        var (preview, apply) = await PlanAsync(law, request, db);
        if (preview.Blockers.Count > 0)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["text"] = [.. preview.Blockers] });

        apply.ForEach(change => change());
        if (request.ApplyMetadata)
        {
            law.AcceptedOn ??= preview.Metadata.AcceptedOn;
            law.GazetteDate ??= preview.Metadata.GazetteDate;
            law.GazetteNumber ??= preview.Metadata.GazetteNumber;
        }
        await CitationIndex.ReplaceForPendingVersionsAsync(db);
        await db.SaveChangesAsync();

        return TypedResults.Ok(preview.Update is { } update
            ? new LawImportResult(true, update.NewNodeCount, update.Changes.Count, preview.Warnings)
            : new LawImportResult(false, preview.NodeCount, preview.ArticleCount, preview.Warnings));
    }

    private sealed record Plan(LawImportPreview Preview, List<Action> Apply);

    private static async Task<Plan> PlanAsync(Law law, ImportLawTextRequest request, AppDbContext db)
    {
        var parsed = LawTextParser.Parse(request.Text, new LawTextParserOptions { JoinWrappedLines = request.JoinWrappedLines });
        var warnings = parsed.Warnings.ToList();
        var blockers = new List<string>();
        var (nodes, articles, counts) = BuildTree(parsed, request, warnings, blockers);

        if (string.IsNullOrWhiteSpace(request.Text))
            blockers.Add("Kanun metnini yapıştırın.");
        else if (counts.Articles == 0)
            blockers.Add("Metinde madde bulunamadı. Maddeler “Madde 1-” biçiminde başlamalı.");

        if (parsed.Metadata.Number is { } metadataNumber && metadataNumber != law.Number)
        {
            warnings.Add($"Metindeki kanun numarası ({metadataNumber}) bu kanunun numarasıyla ({law.Number}) uyuşmuyor; "
                + "doğru kanuna yapıştırdığınızı kontrol edin.");
        }

        var isUpdate = await db.LawNodes.AnyAsync(n => n.LawId == law.Id) || await db.Articles.AnyAsync(a => a.LawId == law.Id);
        var apply = new List<Action>();
        LawUpdatePreview? update = null;
        if (isUpdate)
        {
            update = await PlanUpdateAsync(law, nodes, articles, apply, db);
        }
        else
        {
            PlanCreate(law, nodes, articles, apply, db);
            if (counts.Amended > 0)
            {
                warnings.Add($"{counts.Amended} maddede değişiklik notu var. Yapıştırılan metin kanunun güncel hâli olduğundan bu "
                    + "maddelerin metni nottaki en yeni tarihten itibaren geçerli sayılacak; daha önceki bir tarih sorulduğunda "
                    + "metin gösterilmez. Nottaki tarih değiştiren kanunun kabul tarihidir, yürürlük tarihi farklıysa maddeden "
                    + "düzeltin; eski metinleri maddeye ayrı sürüm olarak ekleyebilirsiniz.");
            }
        }

        if (request.ImportFootnotes)
            await PlanFootnoteNotesAsync(law, nodes, articles, apply, db);

        var metadata = new LawImportMetadata(parsed.Metadata.Number, parsed.Metadata.AcceptedOn,
            parsed.Metadata.GazetteDate, parsed.Metadata.GazetteNumber);
        var preview = new LawImportPreview(metadata, nodes, articles, counts.Nodes, counts.Articles, counts.Repealed,
            parsed.FootnoteCount, warnings, blockers, update);
        return new Plan(preview, apply);
    }

    private sealed record TreeCounts(int Nodes, int Articles, int Repealed, int Amended);

    /// <summary>Ayrıştırılan metni kaydedilecek biçime çevirir: madde adresleri, sıralar, ilk sürüm tarihleri.</summary>
    private static (List<ImportNodePreview> Nodes, List<ImportArticlePreview> Articles, TreeCounts Counts) BuildTree(
        ParsedLaw parsed, ImportLawTextRequest request, List<string> warnings, List<string> blockers)
    {
        var slugs = new HashSet<string>();
        int nodeCount = 0, articleCount = 0, repealedCount = 0, amendedCount = 0;

        ImportArticlePreview Article(ParsedArticle article)
        {
            articleCount++;
            if (article.IsRepealed)
                repealedCount++;

            // Aynı numara iki kez geçerse (ör. farklı kanunlarla eklenmiş iki "Geçici Madde 1") ikincisi ayırt edilir.
            var number = article.Number;
            var slug = Slugs.ForArticle(number);
            for (var copy = 2; !slugs.Add(slug); copy++)
            {
                number = $"{article.Number} ({copy})";
                slug = Slugs.ForArticle(number);
            }
            if (number != article.Number)
                warnings.Add($"Madde {article.Number} metinde birden fazla geçiyor; “{number}” olarak alınacak.");
            if (article.Title.Length > 500)
                blockers.Add($"Madde {article.Number}: başlık çok uzun; metin doğru ayrılamamış olabilir.");

            // Yapıştırılan metin güncel hâldir: değişmiş madde değişiklik notunun tarihinden önce bu metinle geçerli değildi.
            var validFrom = request.ValidFrom;
            if (article.LastAmendedOn is { } amendedOn && (validFrom is null || amendedOn > validFrom))
            {
                validFrom = amendedOn;
                amendedCount++;
            }
            var amendedBy = article.LastAmendment is { Length: > 500 } note ? note[..499] + "…" : article.LastAmendment;

            return new ImportArticlePreview(number, slug, article.Title, article.IsRepealed, article.Order + 1,
                validFrom, amendedBy,
                [.. article.Paragraphs.Select(p => new ParagraphDto(Guid.CreateVersion7(), p.Number, p.Text,
                    [.. p.Items.Select(i => new ParagraphItemDto(Guid.CreateVersion7(), i.Level, i.Label, i.Text))]))],
                [.. (article.Footnotes ?? []).Select(f => $"[{f.Number}] {f.Text}")]);
        }

        ImportNodePreview Node(ParsedNode node)
        {
            nodeCount++;
            if (node.Title.Length > 500 || node.Number.Length > 100)
                blockers.Add($"“{node.Number}” düğümünün adı ya da başlığı çok uzun; metin doğru ayrılamamış olabilir.");

            var kind = node.Kind switch
            {
                ParsedNodeKind.Kitap => LawNodeKind.Kitap,
                ParsedNodeKind.Kisim => LawNodeKind.Kisim,
                ParsedNodeKind.Bolum => LawNodeKind.Bolum,
                _ => LawNodeKind.Ayrim,
            };
            return new ImportNodePreview(kind, node.Number, node.Title, node.Order + 1,
                [.. node.Children.Select(Node)], [.. node.Articles.Select(Article)]);
        }

        var nodes = parsed.Nodes.Select(Node).ToList();
        var articles = parsed.Articles.Select(Article).ToList();
        return (nodes, articles, new TreeCounts(nodeCount, articleCount, repealedCount, amendedCount));
    }

    private static List<Paragraph> ToParagraphs(List<ParagraphDto> paragraphs) =>
        [.. paragraphs.Select(p => new Paragraph(p.Id, p.Number, p.Text,
            [.. p.Items.Select(i => new ParagraphItem(i.Id, i.Level, i.Label, i.Text))]))];

    private static (Article Article, ArticleVersion Version) NewArticle(Law law, ImportArticlePreview item, Guid? nodeId)
    {
        var article = new Article
        {
            LawId = law.Id,
            NodeId = nodeId,
            Number = item.Number,
            Slug = item.Slug,
            Title = item.Title,
            SortOrder = item.SortOrder,
        };
        var version = new ArticleVersion
        {
            ArticleId = article.Id,
            ValidFrom = item.ValidFrom,
            AmendedBy = item.AmendedBy,
            IsRepealed = item.IsRepealed,
            Paragraphs = ToParagraphs(item.Paragraphs),
        };
        return (article, version);
    }

    private static LawNode NewNode(Law law, ImportNodePreview item, Guid? parentId) => new()
    {
        LawId = law.Id,
        ParentId = parentId,
        Kind = item.Kind,
        Number = item.Number,
        Title = item.Title,
        SortOrder = item.SortOrder,
    };

    private static void PlanCreate(
        Law law, List<ImportNodePreview> nodes, List<ImportArticlePreview> articles, List<Action> apply, AppDbContext db)
    {
        void AddArticles(List<ImportArticlePreview> items, Guid? nodeId)
        {
            foreach (var item in items)
            {
                var (article, version) = NewArticle(law, item, nodeId);
                apply.Add(() =>
                {
                    db.Articles.Add(article);
                    db.ArticleVersions.Add(version);
                });
            }
        }

        void AddNodes(List<ImportNodePreview> items, Guid? parentId)
        {
            foreach (var item in items)
            {
                var node = NewNode(law, item, parentId);
                apply.Add(() => db.LawNodes.Add(node));
                AddNodes(item.Children, node.Id);
                AddArticles(item.Articles, node.Id);
            }
        }

        AddNodes(nodes, null);
        AddArticles(articles, null);
    }

    /// <summary>İçe aktarmanın oluşturduğu dipnot notunun başlığı; güncellemede bu başlıklı not yenilenir.</summary>
    public const string FootnoteNoteTitle = "Mevzuat dipnotları";

    /// <summary>
    /// Dipnotu olan her maddeye "Mevzuat dipnotları" açıklaması: yoksa eklenir, varsa ve dipnotlar değiştiyse güncellenir.
    /// Diğer planlanan değişikliklerden sonra çalışır (yeni maddeler o anda izleniyor olur).
    /// </summary>
    private static async Task PlanFootnoteNotesAsync(
        Law law, List<ImportNodePreview> nodes, List<ImportArticlePreview> articles, List<Action> apply, AppDbContext db)
    {
        IEnumerable<ImportArticlePreview> All(List<ImportNodePreview> items) =>
            items.SelectMany(n => n.Articles.Concat(All(n.Children)));
        var withFootnotes = All(nodes).Concat(articles).Where(a => a.Footnotes.Count > 0).ToList();
        if (withFootnotes.Count == 0)
            return;

        var existing = (await db.EditorialNotes
                .Where(n => n.LawId == law.Id && n.ArticleId != null && n.Kind == EditorialNoteKind.Aciklama && n.Title == FootnoteNoteTitle)
                .ToListAsync())
            .GroupBy(n => n.ArticleId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        apply.Add(() =>
        {
            var bySlug = db.Articles.Local.Where(a => a.LawId == law.Id && a.DeletedAt == null).ToDictionary(a => a.Slug);
            foreach (var item in withFootnotes)
            {
                if (!bySlug.TryGetValue(item.Slug, out var article))
                    continue;
                var html = string.Concat(item.Footnotes.Select(f => $"<p>{WebUtility.HtmlEncode(f)}</p>"));
                if (existing.TryGetValue(article.Id, out var note))
                {
                    if (note.Html != html)
                    {
                        note.Html = html;
                        note.PlainText = RichText.ToPlainText(html);
                    }
                    continue;
                }
                db.EditorialNotes.Add(new EditorialNote
                {
                    LawId = law.Id,
                    ArticleId = article.Id,
                    Kind = EditorialNoteKind.Aciklama,
                    Title = FootnoteNoteTitle,
                    Html = html,
                    PlainText = RichText.ToPlainText(html),
                    Visibility = Visibility.Public,
                    // Elle yazılan açıklamaların ardından gelsin.
                    SortOrder = 1000,
                });
            }
        });
    }

    private static async Task<LawUpdatePreview> PlanUpdateAsync(
        Law law, List<ImportNodePreview> nodes, List<ImportArticlePreview> articles, List<Action> apply, AppDbContext db)
    {
        var existingNodes = await db.LawNodes.Where(n => n.LawId == law.Id).ToListAsync();
        var existingArticles = await db.Articles.Where(a => a.LawId == law.Id).ToListAsync();
        var articleIds = existingArticles.Select(a => a.Id).ToList();
        var versionsByArticle = (await db.ArticleVersions.Where(v => articleIds.Contains(v.ArticleId)).ToListAsync())
            .ToLookup(v => v.ArticleId);

        // Düğüm, üst düğümü altında türü ve sıra adıyla tanınır ("Bölüm" + "birinci bölüm").
        static string NodeKey(LawNodeKind kind, string number) => $"{kind}:{TurkishText.Normalize(number)}";
        var nodesByParent = existingNodes.ToLookup(n => n.ParentId);
        var articlesBySlug = existingArticles.ToDictionary(a => a.Slug);
        var seenArticles = new HashSet<Guid>();
        var changes = new List<ArticleChange>();
        int unchanged = 0, newNodes = 0, renamedNodes = 0;

        void VisitArticles(List<ImportArticlePreview> items, Guid? nodeId)
        {
            foreach (var item in items)
            {
                if (!articlesBySlug.TryGetValue(item.Slug, out var article))
                {
                    var (created, version) = NewArticle(law, item, nodeId);
                    apply.Add(() =>
                    {
                        db.Articles.Add(created);
                        db.ArticleVersions.Add(version);
                    });
                    changes.Add(new ArticleChange(item.Number, item.Slug, item.Title, null, true, true, true, false,
                        item.ValidFrom, item.AmendedBy, [], item.Paragraphs));
                    continue;
                }

                seenArticles.Add(article.Id);
                var versions = versionsByArticle[article.Id].ToList();
                var current = ArticleVersion.Select(versions, null);
                var paragraphs = ToParagraphs(item.Paragraphs);
                var textChanged = current is null
                    || current.IsRepealed != item.IsRepealed
                    || !ParagraphIdentity.HaveSameText(current.Paragraphs, paragraphs);
                var titleChanged = article.Title != item.Title;
                var moved = article.NodeId != nodeId;

                var sortOrder = item.SortOrder;
                if (article.SortOrder != sortOrder)
                    apply.Add(() => article.SortOrder = sortOrder);
                if (!textChanged && !titleChanged && !moved)
                {
                    unchanged++;
                    continue;
                }

                if (titleChanged)
                    apply.Add(() => article.Title = item.Title);
                if (moved)
                    apply.Add(() => article.NodeId = nodeId);

                var asNewVersion = false;
                var validFrom = current?.ValidFrom;
                if (textChanged && current is null)
                {
                    asNewVersion = true;
                    validFrom = item.ValidFrom;
                    var version = new ArticleVersion
                    {
                        ArticleId = article.Id,
                        ValidFrom = item.ValidFrom,
                        AmendedBy = item.AmendedBy,
                        IsRepealed = item.IsRepealed,
                        Paragraphs = paragraphs,
                    };
                    apply.Add(() => db.ArticleVersions.Add(version));
                }
                else if (textChanged && current is not null)
                {
                    // Fıkra ve bent kimlikleri yürürlükteki metinden taşınır (vurgu ve notlar kopmasın).
                    var carried = ParagraphIdentity.CarryOver(current.Paragraphs, paragraphs);
                    if (item.ValidFrom is { } from && (current.ValidFrom is null || from > current.ValidFrom))
                    {
                        asNewVersion = true;
                        validFrom = from;
                        var version = new ArticleVersion
                        {
                            ArticleId = article.Id,
                            ValidFrom = from,
                            AmendedBy = item.AmendedBy,
                            IsRepealed = item.IsRepealed,
                            Paragraphs = carried,
                        };
                        apply.Add(() =>
                        {
                            if (current.ValidTo is null)
                                current.ValidTo = from.AddDays(-1);
                            db.ArticleVersions.Add(version);
                        });
                    }
                    else
                    {
                        apply.Add(() =>
                        {
                            current.Paragraphs = carried;
                            current.IsRepealed = item.IsRepealed;
                        });
                    }
                }

                changes.Add(new ArticleChange(item.Number, item.Slug, item.Title, titleChanged ? article.Title : null,
                    false, textChanged, asNewVersion, moved, validFrom, asNewVersion ? item.AmendedBy : current?.AmendedBy,
                    current is null ? [] : [.. current.Paragraphs.Select(p => p.ToDto())], item.Paragraphs));
            }
        }

        void VisitNodes(List<ImportNodePreview> items, Guid? parentId)
        {
            foreach (var item in items)
            {
                var existing = nodesByParent[parentId].FirstOrDefault(n => NodeKey(n.Kind, n.Number) == NodeKey(item.Kind, item.Number));
                Guid nodeId;
                if (existing is null)
                {
                    var node = NewNode(law, item, parentId);
                    nodeId = node.Id;
                    newNodes++;
                    apply.Add(() => db.LawNodes.Add(node));
                }
                else
                {
                    nodeId = existing.Id;
                    if (existing.Title != item.Title && item.Title.Length > 0)
                    {
                        renamedNodes++;
                        apply.Add(() => existing.Title = item.Title);
                    }
                    if (existing.SortOrder != item.SortOrder)
                        apply.Add(() => existing.SortOrder = item.SortOrder);
                }
                VisitNodes(item.Children, nodeId);
                VisitArticles(item.Articles, nodeId);
            }
        }

        VisitNodes(nodes, null);
        VisitArticles(articles, null);

        var notInText = existingArticles.Where(a => !seenArticles.Contains(a.Id))
            .OrderBy(a => a.SortOrder)
            .Select(a => a.Number)
            .ToList();
        return new LawUpdatePreview(changes, unchanged, newNodes, renamedNodes, notInText);
    }
}
