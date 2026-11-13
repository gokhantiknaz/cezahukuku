using CezaHukuku.Domain.Laws;

namespace CezaHukuku.Domain.Content;

/// <summary>Ders ağacındaki bir başlık: numarası, düzeyi, etkin görünürlüğü ve görünür alt başlıkları.</summary>
/// <param name="Label">Gösterilen numara: birinci düzeyde "1. Bölüm", altında "1.5.1"; bölüm numaralı derste her düzeyde
/// numaranın kendisi ("2.1", "2.1.1").</param>
public sealed record LectureOutlineNode(
    LectureNote Note,
    string Number,
    string Label,
    int Depth,
    Visibility EffectiveVisibility,
    IReadOnlyList<LectureOutlineNode> Children);

/// <summary>
/// Bir dersin başlık ağacı (içindekiler): sıra, numaralar, etkin görünürlük, önceki/sonraki ve ders yolu.
/// <list type="bullet">
/// <item>Kardeşler <see cref="LectureNote.SortOrder"/>, sonra oluşturulma sırasıyla dizilir.</item>
/// <item>Dersin bölüm numarası (<see cref="LectureCourse.ChapterNumber"/>) varsa numaralar onunla başlar ("2.1.1").</item>
/// <item>Numara ağaçtaki yerden hesaplanır ve okuyucudan bağımsızdır: gizli bir başlık okuyucuda görünmese de
/// sonraki kardeşlerin numarası kaymaz (aynı numara herkes için aynı başlığı gösterir).</item>
/// <item>Etkin görünürlük dersten aşağı doğru daralır (<see cref="VisibilityExtensions.Narrow"/>): özel ders ya da
/// özel üst başlığın altındaki her şey özeldir. <c>includePrivate</c> kapalıyken özel başlıklar alt ağaçlarıyla
/// birlikte çıkarılır; önceki/sonraki gezinmesi de onları atlar.</item>
/// </list>
/// Dersler küçük olduğundan (≤ 300 başlık) ağacın tamamı bellekte kurulur.
/// </summary>
public sealed class LectureOutline
{
    private readonly Dictionary<Guid, LectureNote> nodesById;
    private readonly ILookup<Guid?, LectureNote> childrenOf;
    private readonly Dictionary<Guid, Visibility> visibilityCache = [];
    private readonly Dictionary<Guid, (string Number, int Depth)> numbers = [];
    private readonly List<LectureOutlineNode> tocOrder = [];
    private readonly Dictionary<Guid, int> tocIndex = [];

    public LectureCourse Course { get; }

    /// <summary>Görünür birinci düzey başlıklar.</summary>
    public IReadOnlyList<LectureOutlineNode> Roots { get; }

    /// <summary>Görünür başlıklar içindekilerdeki sırayla (önce başlık, sonra alt başlıkları).</summary>
    public IReadOnlyList<LectureOutlineNode> TocOrder => tocOrder;

    private LectureOutline(LectureCourse course, IEnumerable<LectureNote> nodes, bool includePrivate)
    {
        Course = course;
        nodesById = nodes.Where(n => n.CourseId == course.Id).ToDictionary(n => n.Id);
        // Üst başlığı bulunamayan (silinmiş ya da başka derste) başlık birinci düzeye düşer; kaybolmasın.
        childrenOf = nodesById.Values
            .OrderBy(n => n.SortOrder).ThenBy(n => n.CreatedAt).ThenBy(n => n.Id)
            .ToLookup(n => KnownOrNull(n.ParentId));

        Number(null, course.ChapterNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "", 0);

        bool IsIncluded(Visibility visibility) => includePrivate || visibility == Visibility.Public;

        List<LectureOutlineNode> Visit(Guid? parentId)
        {
            var result = new List<LectureOutlineNode>();
            foreach (var note in childrenOf[parentId])
            {
                var visibility = EffectiveVisibilityOf(note);
                if (!IsIncluded(visibility))
                    continue;
                var (number, depth) = numbers[note.Id];
                // Önce kendisi, sonra alt başlıkları: içindekilerdeki okuma sırası.
                var index = tocOrder.Count;
                tocIndex[note.Id] = index;
                tocOrder.Add(null!);
                var node = new LectureOutlineNode(note, number, LabelOf(number, depth, course.ChapterNumber is not null), depth,
                    visibility, Visit(note.Id));
                tocOrder[index] = node;
                result.Add(node);
            }
            return result;
        }

        Roots = IsIncluded(course.Visibility) ? Visit(null) : [];
    }

    /// <summary>Dersin ağacını kurar. <paramref name="nodes"/> dersin silinmemiş başlıklarıdır; başka derslerinkiler yok sayılır.</summary>
    public static LectureOutline Build(LectureCourse course, IEnumerable<LectureNote> nodes, bool includePrivate) =>
        new(course, nodes, includePrivate);

    /// <summary>
    /// Birinci düzeyde "1. Bölüm", altında numaranın kendisi. Bölüm numaralı derste (<paramref name="chaptered"/>)
    /// numara zaten bölümle başladığından ("2.1") her düzeyde numaranın kendisi.
    /// </summary>
    public static string LabelOf(string number, int depth, bool chaptered) =>
        depth == 1 && !chaptered ? $"{number}. Bölüm" : number;

    /// <summary>Görünür başlığı kimliğiyle bulur; gizli ya da olmayan başlıkta null.</summary>
    public LectureOutlineNode? Find(Guid noteId) =>
        tocIndex.TryGetValue(noteId, out var index) ? tocOrder[index] : null;

    public LectureOutlineNode? Previous(Guid noteId) =>
        tocIndex.TryGetValue(noteId, out var index) && index > 0 ? tocOrder[index - 1] : null;

    public LectureOutlineNode? Next(Guid noteId) =>
        tocIndex.TryGetValue(noteId, out var index) && index < tocOrder.Count - 1 ? tocOrder[index + 1] : null;

    /// <summary>Birinci düzeyden başlığın üst başlığına kadar görünür üst başlıklar (ders yolu). Başlığın kendisi dahil değil.</summary>
    public IReadOnlyList<LectureOutlineNode> PathOf(Guid noteId)
    {
        var path = new List<LectureOutlineNode>();
        if (!nodesById.TryGetValue(noteId, out var note))
            return path;
        var seen = new HashSet<Guid> { noteId };
        var current = KnownOrNull(note.ParentId);
        while (current is { } id && seen.Add(id))
        {
            if (Find(id) is { } node)
                path.Add(node);
            current = KnownOrNull(nodesById[id].ParentId);
        }
        path.Reverse();
        return path;
    }

    /// <summary>Başlığın numarası ve düzeyi (görünürlükten bağımsız); bu dersin başlığı değilse null.</summary>
    public (string Number, int Depth)? NumberOf(Guid noteId) =>
        numbers.TryGetValue(noteId, out var value) ? value : null;

    /// <summary>Ders → üst başlıklar → kendisi zincirinde daralan görünürlük. Döngüdeki başlık özeldir.</summary>
    public Visibility EffectiveVisibilityOf(LectureNote note)
    {
        if (visibilityCache.TryGetValue(note.Id, out var known))
            return known;

        var chain = new List<LectureNote>();
        var seen = new HashSet<Guid>();
        var inherited = Course.Visibility;
        var current = note;
        while (true)
        {
            if (!seen.Add(current.Id))
            {
                // Döngü yazma tarafında engellenir (WouldCreateCycle); yine de oluşursa güvenli tarafta kal.
                inherited = Visibility.Private;
                break;
            }
            chain.Add(current);
            if (KnownOrNull(current.ParentId) is not { } parentId)
                break;
            if (visibilityCache.TryGetValue(parentId, out var parentVisibility))
            {
                inherited = parentVisibility;
                break;
            }
            current = nodesById[parentId];
        }

        for (var i = chain.Count - 1; i >= 0; i--)
        {
            inherited = inherited.Narrow(chain[i].Visibility);
            visibilityCache[chain[i].Id] = inherited;
        }
        return visibilityCache[note.Id];
    }

    /// <summary>
    /// <paramref name="noteId"/> başlığını <paramref name="newParentId"/> altına taşımak döngü yaratır mı: yeni üst
    /// başlık kendisi ya da alt başlıklarından biriyse evet.
    /// </summary>
    public static bool WouldCreateCycle(IEnumerable<LectureNote> courseNodes, Guid noteId, Guid? newParentId)
    {
        if (newParentId is not { } target)
            return false;
        var parentOf = courseNodes.ToDictionary(n => n.Id, n => n.ParentId);
        var seen = new HashSet<Guid>();
        Guid? current = target;
        while (current is { } id && seen.Add(id))
        {
            if (id == noteId)
                return true;
            current = parentOf.TryGetValue(id, out var parent) ? parent : null;
        }
        return false;
    }

    private void Number(Guid? parentId, string prefix, int depth)
    {
        var position = 0;
        foreach (var note in childrenOf[parentId])
        {
            if (numbers.ContainsKey(note.Id))
                continue;
            position++;
            var number = prefix.Length == 0 ? position.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : $"{prefix}.{position}";
            numbers[note.Id] = (number, depth + 1);
            Number(note.Id, number, depth + 1);
        }
    }

    private Guid? KnownOrNull(Guid? noteId) =>
        noteId is { } id && nodesById.ContainsKey(id) ? id : null;
}
