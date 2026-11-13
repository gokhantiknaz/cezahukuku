using CezaHukuku.Domain.Content;
using CezaHukuku.Domain.Laws;

namespace CezaHukuku.UnitTests.Content;

public class LectureOutlineTests
{
    private readonly LectureCourse course = new() { Title = "Ceza Hukuku", Slug = "ceza-hukuku" };
    private readonly List<LectureNote> nodes = [];
    private DateTimeOffset clock = DateTimeOffset.UnixEpoch;

    private LectureNote Node(LectureNote? parent, string title, int order = 0, Visibility visibility = Visibility.Public)
    {
        clock = clock.AddMinutes(1);
        var note = new LectureNote
        {
            Title = title,
            Slug = title.ToLowerInvariant().Replace(' ', '-'),
            CourseId = course.Id,
            ParentId = parent?.Id,
            SortOrder = order,
            Visibility = visibility,
            CreatedAt = clock,
        };
        nodes.Add(note);
        return note;
    }

    private LectureOutline Build(bool includePrivate = false) => LectureOutline.Build(course, nodes, includePrivate);

    [Fact]
    public void Numbers_FollowOrder_SixLevels()
    {
        var genel = Node(null, "Genel Hükümler", 1);
        var ozel = Node(null, "Özel Hükümler", 2);
        var level2 = Enumerable.Range(1, 5).Select(i => Node(genel, $"G{i}", i)).ToList();
        var l3 = Node(level2[4], "L3", 1);
        var l4 = Node(l3, "L4", 1);
        var l5a = Node(l4, "L5a", 1);
        Node(l4, "L5b", 2);
        var l5c = Node(l4, "L5c", 3);
        var l6 = Node(l5c, "L6", 1);

        var outline = Build();

        Assert.Equal(("1", 1), outline.NumberOf(genel.Id));
        Assert.Equal(("2", 1), outline.NumberOf(ozel.Id));
        Assert.Equal("1. Bölüm", outline.Find(genel.Id)?.Label);
        Assert.Equal("1.5", outline.Find(level2[4].Id)?.Label);
        Assert.Equal("1.5.1.1.1", outline.Find(l5a.Id)?.Label);
        Assert.Equal("1.5.1.1.3.1", outline.Find(l6.Id)?.Label);
        Assert.Equal(6, outline.Find(l6.Id)?.Depth);
        Assert.Equal(["Genel Hükümler", "Özel Hükümler"], outline.Roots.Select(r => r.Note.Title));
    }

    /// <summary>Spec 0004 AC-1: bölüm numaralı derste numaralar onunla başlar, birinci düzeyde "Bölüm" eklenmez.</summary>
    [Fact]
    public void ChapterNumber_PrefixesNumbers_AndLabels()
    {
        var unsurlar = Node(null, "Suçun Unsurları", 1);
        var sartlar = Node(null, "Cezalandırılabilirliğin Diğer Şartları", 2);
        var tipiklik = Node(unsurlar, "Tipiklik", 1);
        var deep = Node(Node(tipiklik, "Genel Olarak", 1), "Alt", 1);
        course.ChapterNumber = 2;

        var outline = Build();

        Assert.Equal(("2.1", 1), outline.NumberOf(unsurlar.Id));
        Assert.Equal(["2.1", "2.2", "2.1.1", "2.1.1.1.1"],
            new[] { unsurlar, sartlar, tipiklik, deep }.Select(n => outline.Find(n.Id)?.Label));
        Assert.Equal(4, outline.Find(deep.Id)?.Depth);

        // Numara kaldırılınca bugünkü biçim.
        course.ChapterNumber = null;
        outline = Build();
        Assert.Equal(["1. Bölüm", "2. Bölüm", "1.1", "1.1.1.1"],
            new[] { unsurlar, sartlar, tipiklik, deep }.Select(n => outline.Find(n.Id)?.Label));
    }

    [Fact]
    public void SameSortOrder_FallsBackToCreationOrder()
    {
        var first = Node(null, "Birinci");
        var second = Node(null, "İkinci");

        var outline = Build();

        Assert.Equal([first.Id, second.Id], outline.TocOrder.Select(n => n.Note.Id));
        Assert.Equal("2. Bölüm", outline.Find(second.Id)?.Label);
    }

    [Fact]
    public void Move_ChangesNumbersOfNodeAndSubtree()
    {
        var a = Node(null, "A", 1);
        var b = Node(null, "B", 2);
        var a1 = Node(a, "A1", 1);
        var a2 = Node(a, "A2", 2);
        var a21 = Node(a2, "A21", 1);

        // A2'yi B'nin altına taşı.
        a2.ParentId = b.Id;
        a2.SortOrder = 1;
        var outline = Build();

        Assert.Equal("2.1", outline.Find(a2.Id)?.Label);
        Assert.Equal("2.1.1", outline.Find(a21.Id)?.Label);
        Assert.Equal("1.1", outline.Find(a1.Id)?.Label);

        // Aynı düzeyde yukarı: B, A'nın önüne.
        b.SortOrder = 0;
        outline = Build();
        Assert.Equal("1. Bölüm", outline.Find(b.Id)?.Label);
        Assert.Equal("1.1.1", outline.Find(a21.Id)?.Label);
        Assert.Equal("2.1", outline.Find(a1.Id)?.Label);
    }

    [Fact]
    public void MoveUnderOwnDescendant_IsRejected()
    {
        var a = Node(null, "A");
        var b = Node(a, "B");
        var c = Node(b, "C");
        var other = Node(null, "Diğer");

        Assert.True(LectureOutline.WouldCreateCycle(nodes, a.Id, c.Id));
        Assert.True(LectureOutline.WouldCreateCycle(nodes, a.Id, b.Id));
        Assert.True(LectureOutline.WouldCreateCycle(nodes, a.Id, a.Id));
        Assert.False(LectureOutline.WouldCreateCycle(nodes, c.Id, a.Id));
        Assert.False(LectureOutline.WouldCreateCycle(nodes, a.Id, other.Id));
        Assert.False(LectureOutline.WouldCreateCycle(nodes, c.Id, null));
    }

    [Fact]
    public void PreviousNext_FollowTocOrder_SkipHidden()
    {
        var a = Node(null, "A", 1);
        var a1 = Node(a, "A1", 1);
        var a2 = Node(a, "A2", 2, Visibility.Private);
        Node(a2, "A21", 1);
        var b = Node(null, "B", 2);

        var anonymous = Build();
        Assert.Equal([a.Id, a1.Id, b.Id], anonymous.TocOrder.Select(n => n.Note.Id));
        Assert.Null(anonymous.Previous(a.Id));
        Assert.Equal(a1.Id, anonymous.Next(a.Id)?.Note.Id);
        Assert.Equal(b.Id, anonymous.Next(a1.Id)?.Note.Id);
        Assert.Equal(a1.Id, anonymous.Previous(b.Id)?.Note.Id);
        Assert.Null(anonymous.Next(b.Id));
        Assert.Null(anonymous.Previous(a2.Id));

        var member = Build(includePrivate: true);
        Assert.Equal(["A", "A1", "A2", "A21", "B"], member.TocOrder.Select(n => n.Note.Title));
        Assert.Equal(b.Id, member.Next(member.TocOrder[3].Note.Id)?.Note.Id);
    }

    [Fact]
    public void HiddenNode_DoesNotShiftNumbersOfSiblings()
    {
        var a = Node(null, "A", 1, Visibility.Private);
        var b = Node(null, "B", 2);

        var outline = Build();

        Assert.Null(outline.Find(a.Id));
        Assert.Equal("2. Bölüm", outline.Find(b.Id)?.Label);
    }

    [Fact]
    public void EffectiveVisibility_NarrowsDownTheTree()
    {
        var a = Node(null, "A", 1, Visibility.Private);
        var a1 = Node(a, "A1", 1);
        var a11 = Node(a1, "A11", 1);
        var b = Node(null, "B", 2);
        var b1 = Node(b, "B1", 1, Visibility.Private);
        var b2 = Node(b, "B2", 2);

        var outline = Build();
        Assert.Equal(Visibility.Private, outline.EffectiveVisibilityOf(a11));
        Assert.Equal(Visibility.Private, outline.EffectiveVisibilityOf(a1));
        Assert.Equal(Visibility.Private, outline.EffectiveVisibilityOf(b1));
        Assert.Equal(Visibility.Public, outline.EffectiveVisibilityOf(b2));
        Assert.Equal([b.Id, b2.Id], outline.TocOrder.Select(n => n.Note.Id));

        var member = Build(includePrivate: true);
        Assert.Equal(Visibility.Private, member.Find(a11.Id)?.EffectiveVisibility);
        Assert.Equal(Visibility.Public, member.Find(b2.Id)?.EffectiveVisibility);

        // Özel ders: başlıkların kendi ayarı açık olsa da hepsi özel, anonimde ağaç boş.
        course.Visibility = Visibility.Private;
        var privateCourse = Build();
        Assert.Empty(privateCourse.Roots);
        Assert.Empty(privateCourse.TocOrder);
        Assert.Equal(Visibility.Private, privateCourse.EffectiveVisibilityOf(b2));
        Assert.All(Build(includePrivate: true).TocOrder, n => Assert.Equal(Visibility.Private, n.EffectiveVisibility));
    }

    [Fact]
    public void PathOf_ReturnsVisibleAncestorsFromRoot()
    {
        var a = Node(null, "A");
        var a1 = Node(a, "A1");
        var a11 = Node(a1, "A11");

        var outline = Build();

        Assert.Equal(["A", "A1"], outline.PathOf(a11.Id).Select(n => n.Note.Title));
        Assert.Empty(outline.PathOf(a.Id));
        Assert.Empty(outline.PathOf(Guid.NewGuid()));
    }

    [Fact]
    public void NodesOfOtherCourses_AndOrphans_AreHandled()
    {
        var a = Node(null, "A", 1);
        var foreign = new LectureNote { Title = "Başka ders", CourseId = Guid.NewGuid() };
        var orphan = Node(null, "Yetim", 2);
        orphan.ParentId = Guid.NewGuid(); // üst başlığı silinmiş

        var outline = LectureOutline.Build(course, [.. nodes, foreign], includePrivate: false);

        Assert.Null(outline.Find(foreign.Id));
        Assert.Equal(["A", "Yetim"], outline.Roots.Select(r => r.Note.Title));
        Assert.Equal("2. Bölüm", outline.Find(orphan.Id)?.Label);
        Assert.Equal(a.Id, outline.Previous(orphan.Id)?.Note.Id);
    }

    [Fact]
    public void CycleInData_IsTreatedAsPrivateAndUnreachable()
    {
        var a = Node(null, "A");
        var x = Node(null, "X");
        var y = Node(x, "Y");
        x.ParentId = y.Id; // veri bozulmuş: X ↔ Y

        var outline = Build(includePrivate: true);

        Assert.Equal([a.Id], outline.TocOrder.Select(n => n.Note.Id));
        Assert.Equal(Visibility.Private, outline.EffectiveVisibilityOf(x));
        Assert.Null(outline.NumberOf(y.Id));
    }
}
