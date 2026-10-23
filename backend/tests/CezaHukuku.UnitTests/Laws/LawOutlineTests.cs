using CezaHukuku.Domain.Laws;

namespace CezaHukuku.UnitTests.Laws;

public class LawOutlineTests
{
    // TCK kesiti: Birinci Kitap (boş) · İkinci Kitap › İkinci Kısım › Birinci Bölüm › md. 81, 82.
    private readonly Law law = new() { Slug = "tck", Number = "5237", Name = "Türk Ceza Kanunu" };
    private readonly LawNode genel;
    private readonly LawNode ozel;
    private readonly LawNode kisim;
    private readonly LawNode bolum;
    private readonly Article m81;
    private readonly Article m82;

    public LawOutlineTests()
    {
        genel = Node(null, "Birinci Kitap", 1);
        ozel = Node(null, "İkinci Kitap", 2);
        kisim = Node(ozel, "İkinci Kısım", 1);
        bolum = Node(kisim, "Birinci Bölüm", 1);
        m81 = Article(bolum, "81", 1);
        m82 = Article(bolum, "82", 2);
    }

    private LawOutline Build(bool includePrivate = false, params Article[] extra) =>
        LawOutline.Build(law, [genel, ozel, kisim, bolum], [m82, m81, .. extra], includePrivate);

    [Fact]
    public void Build_NestsNodesAndOrdersArticles()
    {
        var outline = Build();

        Assert.Equal(["Birinci Kitap", "İkinci Kitap"], outline.Roots.Select(n => n.Node.Number));
        var bolumNode = outline.Roots[1].Children.Single().Children.Single();
        Assert.Equal(["81", "82"], bolumNode.Articles.Select(a => a.Article.Number));
        Assert.Equal(["81", "82"], outline.ReadingOrder.Select(a => a.Article.Number));
    }

    [Fact]
    public void PathOf_ReturnsAncestorsFromRoot()
    {
        var outline = Build();

        Assert.Equal(["İkinci Kitap", "İkinci Kısım", "Birinci Bölüm"], outline.PathOf(m81).Select(n => n.Number));
    }

    [Fact]
    public void PreviousAndNext_FollowReadingOrder()
    {
        var outline = Build();

        Assert.Null(outline.Previous(m81.Id));
        Assert.Equal(m82.Id, outline.Next(m81.Id)?.Article.Id);
        Assert.Equal(m81.Id, outline.Previous(m82.Id)?.Article.Id);
        Assert.Null(outline.Next(m82.Id));
    }

    [Fact]
    public void ReadingOrder_InterleavesNodesAndArticlesBySortOrder()
    {
        // Kanunun doğrudan altında: md. 1 (sıra 0), Birinci Kitap (1), İkinci Kitap (2), md. 345 (3).
        var first = Article(null, "1", 0);
        var last = Article(null, "345", 3);

        var outline = Build(false, last, first);

        Assert.Equal(["1", "81", "82", "345"], outline.ReadingOrder.Select(a => a.Article.Number));
        Assert.Equal(["1", "345"], outline.RootArticles.Select(a => a.Article.Number));
    }

    [Fact]
    public void PrivateNode_HidesItsSubtreeFromAnonymousReaders()
    {
        kisim.Visibility = Visibility.Private;

        var outline = Build();

        Assert.Empty(outline.Roots[1].Children);
        Assert.Empty(outline.ReadingOrder);
        Assert.Null(outline.Find(m81.Id));
    }

    [Fact]
    public void PrivateNode_IsInheritedByDescendantsForSignedInReaders()
    {
        kisim.Visibility = Visibility.Private;

        var outline = Build(includePrivate: true);

        Assert.Equal(Visibility.Public, outline.EffectiveVisibilityOf(ozel));
        Assert.Equal(Visibility.Private, outline.EffectiveVisibilityOf(bolum));
        Assert.Equal(Visibility.Private, outline.Find(m81.Id)?.EffectiveVisibility);
    }

    [Fact]
    public void PublicChild_CannotOpenPrivateParent()
    {
        law.Visibility = Visibility.Private;
        m81.Visibility = Visibility.Public;

        Assert.Empty(Build().ReadingOrder);
        Assert.Equal(Visibility.Private, Build(includePrivate: true).EffectiveVisibilityOf(m81));
    }

    [Fact]
    public void PrivateArticle_HidesOnlyThatArticle()
    {
        m81.Visibility = Visibility.Private;

        var outline = Build();

        Assert.Equal(["82"], outline.ReadingOrder.Select(a => a.Article.Number));
        Assert.Null(outline.Previous(m82.Id));
    }

    [Fact]
    public void ArticleUnderMissingNode_FallsBackToRoot()
    {
        var orphan = Article(null, "99", 9);
        orphan.NodeId = Guid.CreateVersion7();

        var outline = Build(false, orphan);

        Assert.Contains(outline.RootArticles, a => a.Article.Id == orphan.Id);
        Assert.Empty(outline.PathOf(orphan));
    }

    [Fact]
    public void Cycle_IsTreatedAsPrivateAndUnreachable()
    {
        var a = Node(null, "A", 5);
        var b = Node(a, "B", 1);
        a.ParentId = b.Id;

        var outline = LawOutline.Build(law, [a, b], [], includePrivate: true);

        Assert.Empty(outline.Roots);
        Assert.Equal(Visibility.Private, outline.EffectiveVisibilityOf(a));
    }

    private LawNode Node(LawNode? parent, string number, int order) =>
        new() { LawId = law.Id, ParentId = parent?.Id, Number = number, Title = number, SortOrder = order };

    private Article Article(LawNode? node, string number, int order) =>
        new() { LawId = law.Id, NodeId = node?.Id, Number = number, Slug = "madde-" + number, SortOrder = order };
}
