namespace CezaHukuku.Domain.Laws;

public sealed record OutlineNode(
    LawNode Node,
    Visibility EffectiveVisibility,
    IReadOnlyList<OutlineNode> Children,
    IReadOnlyList<OutlineArticle> Articles);

public sealed record OutlineArticle(Article Article, Visibility EffectiveVisibility);

/// <summary>
/// Bir kanunun okuyucuya gösterilecek ağacı: düğümler, maddeler, okuma sırası ve her öğenin geçerli görünürlüğü.
/// Geçerli görünürlük kanundan aşağı doğru daralır (<see cref="VisibilityExtensions.Narrow"/>); private öğeler
/// <c>includePrivate</c> kapalıyken alt ağaçlarıyla birlikte çıkarılır.
/// Kanun ağaçları küçük olduğundan (TCK ≈ 350 madde) tamamı bellekte kurulur.
/// </summary>
public sealed class LawOutline
{
    private readonly Dictionary<Guid, LawNode> nodesById;
    private readonly Dictionary<Guid, Visibility> nodeVisibility = [];
    private readonly Visibility lawVisibility;
    private readonly List<OutlineArticle> readingOrder = [];
    private readonly Dictionary<Guid, int> readingIndex = [];

    public IReadOnlyList<OutlineNode> Roots { get; }

    /// <summary>Hiçbir düğüme bağlı olmayan, doğrudan kanunun altındaki maddeler.</summary>
    public IReadOnlyList<OutlineArticle> RootArticles { get; }

    /// <summary>Görünür maddeler kanundaki okuma sırasıyla (önceki/sonraki madde gezinmesi için).</summary>
    public IReadOnlyList<OutlineArticle> ReadingOrder => readingOrder;

    private LawOutline(Law law, IEnumerable<LawNode> nodes, IEnumerable<Article> articles, bool includePrivate)
    {
        lawVisibility = law.Visibility;
        nodesById = nodes.ToDictionary(n => n.Id);

        // Üst düğümü bulunamayan (silinmiş) düğüm ve maddeler köke düşer; okuyucudan kaybolmasın.
        var nodesByParent = nodesById.Values.ToLookup(n => KnownNodeOrNull(n.ParentId));
        var articlesByNode = articles.ToLookup(a => KnownNodeOrNull(a.NodeId));

        bool IsIncluded(Visibility visibility) => includePrivate || visibility == Visibility.Public;

        (List<OutlineNode> Nodes, List<OutlineArticle> Articles) Visit(Guid? parentId)
        {
            var childNodes = new List<OutlineNode>();
            var childArticles = new List<OutlineArticle>();

            // Düğüm ve maddeler aynı sıra uzayını paylaşır; okuma sırası ikisi birlikte sıralanarak çıkar.
            var entries = nodesByParent[parentId].Select(n => (Order: n.SortOrder, Node: (LawNode?)n, Article: (Article?)null))
                .Concat(articlesByNode[parentId].Select(a => (Order: a.SortOrder, Node: (LawNode?)null, Article: (Article?)a)))
                .OrderBy(e => e.Order)
                .ThenBy(e => e.Article is null ? 1 : 0);

            foreach (var entry in entries)
            {
                if (entry.Node is { } node)
                {
                    var visibility = EffectiveVisibilityOf(node);
                    if (!IsIncluded(visibility))
                        continue;
                    var (children, nodeArticles) = Visit(node.Id);
                    childNodes.Add(new OutlineNode(node, visibility, children, nodeArticles));
                }
                else if (entry.Article is { } article)
                {
                    var visibility = EffectiveVisibilityOf(article);
                    if (!IsIncluded(visibility))
                        continue;
                    var item = new OutlineArticle(article, visibility);
                    childArticles.Add(item);
                    readingIndex[article.Id] = readingOrder.Count;
                    readingOrder.Add(item);
                }
            }

            return (childNodes, childArticles);
        }

        (Roots, RootArticles) = IsIncluded(lawVisibility) ? Visit(null) : ([], []);
    }

    public static LawOutline Build(Law law, IEnumerable<LawNode> nodes, IEnumerable<Article> articles, bool includePrivate) =>
        new(law, nodes, articles, includePrivate);

    /// <summary>Görünür maddeyi kimliğiyle bulur; gizli ya da olmayan maddede null.</summary>
    public OutlineArticle? Find(Guid articleId) =>
        readingIndex.TryGetValue(articleId, out var index) ? readingOrder[index] : null;

    public OutlineArticle? Previous(Guid articleId) =>
        readingIndex.TryGetValue(articleId, out var index) && index > 0 ? readingOrder[index - 1] : null;

    public OutlineArticle? Next(Guid articleId) =>
        readingIndex.TryGetValue(articleId, out var index) && index < readingOrder.Count - 1 ? readingOrder[index + 1] : null;

    /// <summary>Kökten maddenin bağlı olduğu düğüme kadar düğümler (breadcrumb).</summary>
    public IReadOnlyList<LawNode> PathOf(Article article)
    {
        var path = new List<LawNode>();
        var seen = new HashSet<Guid>();
        var current = KnownNodeOrNull(article.NodeId);
        while (current is { } id && seen.Add(id))
        {
            var node = nodesById[id];
            path.Add(node);
            current = KnownNodeOrNull(node.ParentId);
        }
        path.Reverse();
        return path;
    }

    public Visibility EffectiveVisibilityOf(Article article) =>
        (KnownNodeOrNull(article.NodeId) is { } nodeId ? EffectiveVisibilityOf(nodesById[nodeId]) : lawVisibility)
        .Narrow(article.Visibility);

    public Visibility EffectiveVisibilityOf(LawNode node)
    {
        if (nodeVisibility.TryGetValue(node.Id, out var known))
            return known;

        // Köke (ya da hesaplanmış bir ataya) kadar çık, sonra aşağı doğru daraltarak her düğümü önbelleğe yaz.
        var chain = new List<LawNode>();
        var seen = new HashSet<Guid>();
        var inherited = lawVisibility;
        var current = node;
        while (true)
        {
            if (!seen.Add(current.Id))
            {
                // Döngü yazma tarafında engellenir; yine de oluşursa güvenli tarafta kal.
                inherited = Visibility.Private;
                break;
            }
            chain.Add(current);

            if (KnownNodeOrNull(current.ParentId) is not { } parentId)
                break;
            if (nodeVisibility.TryGetValue(parentId, out var parentVisibility))
            {
                inherited = parentVisibility;
                break;
            }
            current = nodesById[parentId];
        }

        for (var i = chain.Count - 1; i >= 0; i--)
        {
            inherited = inherited.Narrow(chain[i].Visibility);
            nodeVisibility[chain[i].Id] = inherited;
        }
        return nodeVisibility[node.Id];
    }

    private Guid? KnownNodeOrNull(Guid? nodeId) =>
        nodeId is { } id && nodesById.ContainsKey(id) ? id : null;
}
