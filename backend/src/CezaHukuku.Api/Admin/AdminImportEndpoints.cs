using CezaHukuku.Api.Common;
using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Infrastructure.Persistence;
using CezaHukuku.Parsing.Legislation;
using CezaHukuku.Parsing.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Admin;

/// <summary>
/// Kanunun tam metnini yapıştırarak içe aktarma: önce önizleme (ağaç, madde sayıları, uyarılar), onaylanınca
/// düğümler, maddeler ve ilk sürümler tek işlemde kaydedilir. Önizleme ve kayıt aynı planı kullanır; önizlemede
/// görülen yapı aynen kaydedilir. Şimdilik yalnız boş kanuna aktarılır (değişen maddeleri eşleştirip yeni sürüm
/// açmak ayrı bir adım).
/// </summary>
internal static class AdminImportEndpoints
{
    public static void MapImportEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/laws/{lawId:guid}/import/preview", PreviewAsync).WithName("PreviewLawImport");
        group.MapPost("/laws/{lawId:guid}/import", ImportAsync).WithName("ImportLaw");
    }

    private static async Task<Results<Ok<LawImportPreview>, NotFound>> PreviewAsync(
        Guid lawId, ImportLawTextRequest request, AppDbContext db)
    {
        var law = await db.Laws.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lawId);
        return law is null ? TypedResults.NotFound() : TypedResults.Ok(await PlanAsync(law, request, db));
    }

    private static async Task<Results<Ok<LawImportResult>, NotFound, ValidationProblem>> ImportAsync(
        Guid lawId, ImportLawTextRequest request, AppDbContext db)
    {
        var law = await db.Laws.FirstOrDefaultAsync(l => l.Id == lawId);
        if (law is null)
            return TypedResults.NotFound();

        var plan = await PlanAsync(law, request, db);
        if (plan.Blockers.Count > 0)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["text"] = [.. plan.Blockers] });

        void AddArticle(ImportArticlePreview item, Guid? nodeId)
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
            db.Articles.Add(article);
            db.ArticleVersions.Add(new ArticleVersion
            {
                ArticleId = article.Id,
                ValidFrom = item.ValidFrom,
                AmendedBy = item.AmendedBy,
                IsRepealed = item.IsRepealed,
                Paragraphs =
                [
                    .. item.Paragraphs.Select(p => new Paragraph(p.Id, p.Number, p.Text,
                        [.. p.Items.Select(i => new ParagraphItem(i.Id, i.Level, i.Label, i.Text))])),
                ],
            });
        }

        void AddNode(ImportNodePreview item, Guid? parentId)
        {
            var node = new LawNode
            {
                LawId = law.Id,
                ParentId = parentId,
                Kind = item.Kind,
                Number = item.Number,
                Title = item.Title,
                SortOrder = item.SortOrder,
            };
            db.LawNodes.Add(node);
            item.Children.ForEach(child => AddNode(child, node.Id));
            item.Articles.ForEach(article => AddArticle(article, node.Id));
        }

        plan.Nodes.ForEach(node => AddNode(node, null));
        plan.Articles.ForEach(article => AddArticle(article, null));

        if (request.ApplyMetadata)
        {
            law.AcceptedOn ??= plan.Metadata.AcceptedOn;
            law.GazetteDate ??= plan.Metadata.GazetteDate;
            law.GazetteNumber ??= plan.Metadata.GazetteNumber;
        }

        await db.SaveChangesAsync();
        return TypedResults.Ok(new LawImportResult(plan.NodeCount, plan.ArticleCount, plan.Warnings));
    }

    private static async Task<LawImportPreview> PlanAsync(Law law, ImportLawTextRequest request, AppDbContext db)
    {
        var parsed = LawTextParser.Parse(request.Text, new LawTextParserOptions { JoinWrappedLines = request.JoinWrappedLines });
        var warnings = parsed.Warnings.ToList();
        var blockers = new List<string>();
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
                    [.. p.Items.Select(i => new ParagraphItemDto(Guid.CreateVersion7(), i.Level, i.Label, i.Text))]))]);
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

        if (string.IsNullOrWhiteSpace(request.Text))
            blockers.Add("Kanun metnini yapıştırın.");
        else if (articleCount == 0)
            blockers.Add("Metinde madde bulunamadı. Maddeler “Madde 1-” biçiminde başlamalı.");

        var existingNodes = await db.LawNodes.CountAsync(n => n.LawId == law.Id);
        var existingArticles = await db.Articles.CountAsync(a => a.LawId == law.Id);
        if (existingNodes + existingArticles > 0)
        {
            blockers.Add($"Bu kanunda zaten {existingArticles} madde ve {existingNodes} düğüm var. "
                + "İçe aktarma şimdilik yalnız boş kanuna yapılabilir.");
        }

        if (amendedCount > 0)
        {
            warnings.Add($"{amendedCount} maddede değişiklik notu var. Yapıştırılan metin kanunun güncel hâli olduğundan bu "
                + "maddelerin metni nottaki en yeni tarihten itibaren geçerli sayılacak; daha önceki bir tarih sorulduğunda "
                + "metin gösterilmez. Nottaki tarih değiştiren kanunun kabul tarihidir, yürürlük tarihi farklıysa maddeden "
                + "düzeltin; eski metinleri maddeye ayrı sürüm olarak ekleyebilirsiniz.");
        }

        if (parsed.Metadata.Number is { } metadataNumber && metadataNumber != law.Number)
        {
            warnings.Add($"Metindeki kanun numarası ({metadataNumber}) bu kanunun numarasıyla ({law.Number}) uyuşmuyor; "
                + "doğru kanuna yapıştırdığınızı kontrol edin.");
        }

        var metadata = new LawImportMetadata(parsed.Metadata.Number, parsed.Metadata.AcceptedOn,
            parsed.Metadata.GazetteDate, parsed.Metadata.GazetteNumber);
        return new LawImportPreview(metadata, nodes, articles, nodeCount, articleCount, repealedCount,
            parsed.FootnoteCount, warnings, blockers);
    }
}
