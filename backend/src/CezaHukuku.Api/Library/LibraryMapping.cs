using CezaHukuku.Domain.Laws;

namespace CezaHukuku.Api.Library;

internal static class LibraryMapping
{
    public static LawGroupSummary ToSummary(this LawGroup g) => new(g.Id, g.Slug, g.Title, g.SortOrder);

    public static LawSummary ToSummary(this Law l) =>
        new(l.Id, l.GroupId, l.Slug, l.Number, l.ShortName, l.Name, l.AcceptedOn, l.GazetteDate, l.GazetteNumber,
            l.Visibility, l.SortOrder);

    public static LawNodeSummary ToSummary(this LawNode n) =>
        new(n.Id, n.LawId, n.ParentId, n.Kind, n.Number, n.Title, n.SortOrder, n.Visibility);

    public static ArticleSummary ToSummary(this Article a, Visibility effectiveVisibility) =>
        new(a.Id, a.NodeId, a.Slug, a.Number, a.Title, a.SortOrder, a.Visibility, effectiveVisibility);

    public static ArticleSummary ToSummary(this OutlineArticle a) => a.Article.ToSummary(a.EffectiveVisibility);

    public static LawNodeTree ToTree(this OutlineNode n) =>
        new(n.Node.Id, n.Node.ParentId, n.Node.Kind, n.Node.Number, n.Node.Title, n.Node.SortOrder, n.Node.Visibility,
            n.EffectiveVisibility, [.. n.Children.Select(ToTree)], [.. n.Articles.Select(ToSummary)]);

    public static LawNodeRef ToRef(this LawNode n) => new(n.Id, n.Kind, n.Number, n.Title);

    public static ArticleRef ToRef(this OutlineArticle a) => new(a.Article.Slug, a.Article.Number, a.Article.Title);

    public static ArticleVersionSummary ToSummary(this ArticleVersion v) =>
        new(v.Id, v.ValidFrom, v.ValidTo, v.AmendedBy, v.IsRepealed);

    public static ArticleVersionDetail ToDetail(this ArticleVersion v) =>
        new(v.Id, v.ValidFrom, v.ValidTo, v.AmendedBy, v.GazetteDate, v.GazetteNumber, v.IsRepealed,
            [.. v.Paragraphs.Select(ToDto)]);

    public static EditorialNoteDto ToDto(this EditorialNote n, Visibility effectiveVisibility) =>
        new(n.Id, n.Kind, n.Title, n.Html, n.Visibility, effectiveVisibility, n.SortOrder, n.UpdatedAt);

    public static ParagraphDto ToDto(this Paragraph p) =>
        new(p.Id, p.Number, p.Text, [.. p.Items.Select(i => new ParagraphItemDto(i.Id, i.Level, i.Label, i.Text))]);
}
