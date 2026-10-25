using CezaHukuku.Api.Library;
using CezaHukuku.Domain.Decisions;
using CezaHukuku.Domain.Laws;
using CezaHukuku.Infrastructure.Persistence;
using CezaHukuku.Parsing.Decisions;
using Microsoft.EntityFrameworkCore;

namespace CezaHukuku.Api.Tools;

/// <summary>
/// Metindeki karar künyelerini denetler (turk-hukuku.com'un künye doğrulama yöntemi):
/// <list type="number">
/// <item>Künye parçalarına ayrılır ve kendi içinde tutarlılığı denetlenir (<see cref="DecisionCitationRules"/>).</item>
/// <item>Kütüphanedeki kararlarda aranır. Esas ve karar numarası tutan karar bulunursa tarih ve daire karşılaştırılır;
/// yalnız biri tutuyorsa "benzer karar" olarak gösterilir (çoğu zaman numara yanlış yazılmıştır).</item>
/// <item>Bulunamayan künye "[doğrulanacak]" işaretlenir ve resmî karar bankasına yönlendirilir; hiçbir künye
/// doğrulanmış sayılmaz.</item>
/// <item>Bulunan kararın bağlı olduğu madde karardan sonra değiştiyse kararın güncelliği sorgulanır.</item>
/// </list>
/// </summary>
internal static class DecisionCitationCheck
{
    public static async Task<List<CheckedDecisionCitation>> CheckAsync(
        AppDbContext db, string text, DateOnly today, bool includePrivate, CancellationToken ct)
    {
        var found = DecisionCitationFinder.Find(text);
        if (found.Count == 0)
            return [];

        var numbers = found.SelectMany(c => new[] { c.CaseNumber, c.DecisionNumber }).OfType<string>().Distinct().ToList();
        var candidates = await db.CourtDecisions.AsNoTracking()
            .Where(d => (d.CaseNumber != null && numbers.Contains(d.CaseNumber.Replace(" ", "")))
                || (d.DecisionNumber != null && numbers.Contains(d.DecisionNumber.Replace(" ", ""))))
            .ToListAsync(ct);
        var context = await LoadContextAsync(db, candidates, includePrivate, ct);

        var result = new List<CheckedDecisionCitation>();
        foreach (var citation in found)
        {
            var court = ToCourt(citation.Court);
            var issues = DecisionCitationRules.Check(citation, today)
                .Select(f => new CitationIssue(f.Code, f.Severity, f.Message, null))
                .ToList();

            var (match, exact) = Match(citation, court, context.Decisions);
            LinkedDecisionRef? matchRef = null;
            if (match is not null)
            {
                var place = context.PlaceOf(match);
                matchRef = new LinkedDecisionRef(match.Id, match.Court, match.Chamber, match.Kind, match.CaseNumber,
                    match.DecisionNumber, match.DecisionDate, place?.Url, match.SourceUrl);
                if (exact)
                {
                    issues.Insert(0, new("bulundu", CheckSeverity.Ok, "Kütüphanede bulundu.", place?.Url ?? match.SourceUrl));
                    issues.AddRange(Compare(citation, match));
                    issues.AddRange(context.ChangesAfter(match));
                }
                else
                {
                    issues.Add(Similar(citation, match, place?.Url));
                }
            }
            else
            {
                issues.Add(new("dogrulanacak", CheckSeverity.Warning,
                    "[doğrulanacak] Kütüphanede bulunamadı; künyeyi resmî karar bankasından doğrulayın ve kararın tam metnini okuyun.",
                    OfficialSearchUrl(citation)));
            }

            result.Add(new CheckedDecisionCitation(citation.Start, citation.Length,
                $"{court}|{citation.Chamber}|{citation.CaseNumber}|{citation.DecisionNumber}",
                DecisionLabels.Citation(court, citation.Chamber, citation.Date, citation.DateText, citation.CaseNumber,
                    citation.DecisionNumber, citation.IsApplication),
                court, citation.Chamber, citation.CaseNumber, citation.DecisionNumber, citation.IsApplication, citation.Date,
                matchRef, issues.Max(i => i.Severity), issues));
        }
        return result;
    }

    /// <summary>
    /// Künyedeki numaraların hepsi tutuyorsa (künyede olmayan parça karşılaştırılmaz) kesin eşleşme; esas ve karardan
    /// yalnız biri tutuyor ama daire de aynıysa benzer karar. Mahkemesi farklı karar hiç eşleşmez.
    /// </summary>
    private static (CourtDecision? Decision, bool Exact) Match(DecisionCitation citation, Court? court, List<CourtDecision> decisions)
    {
        var pool = decisions.Where(d => court is null || d.Court == court).ToList();
        bool Same(string? stored, string? cited) =>
            stored is not null && cited is not null && DecisionCitationFinder.NormalizeNumber(stored) == cited;
        bool CaseOk(CourtDecision d) => citation.CaseNumber is null || Same(d.CaseNumber, citation.CaseNumber);
        bool DecisionOk(CourtDecision d) => citation.DecisionNumber is null || Same(d.DecisionNumber, citation.DecisionNumber);
        bool SameChamber(CourtDecision d) =>
            citation.Chamber is not null && DecisionCitationFinder.NormalizeChamber(d.Chamber) == citation.Chamber;

        var bothGiven = citation.CaseNumber is not null && citation.DecisionNumber is not null;
        var exact = pool
            .Where(d => CaseOk(d) && DecisionOk(d))
            // Tek numarayla (yalnız karar no) eşleşmede daire çelişmemeli: her dairenin "2019/567" sayılı kararı vardır.
            .Where(d => bothGiven || citation.Chamber is null || SameChamber(d))
            .OrderByDescending(SameChamber)
            .FirstOrDefault();
        if (exact is not null)
            return (exact, true);
        if (!bothGiven)
            return (null, false);
        var similar = pool.FirstOrDefault(d => SameChamber(d) && (CaseOk(d) || DecisionOk(d)));
        return (similar, false);
    }

    /// <summary>Kesin eşleşmede künye ile kayıt arasındaki farklar (bağlantı "bulundu" bulgusunda).</summary>
    private static IEnumerable<CitationIssue> Compare(DecisionCitation citation, CourtDecision match)
    {
        if (citation.Date is { } date && match.DecisionDate is { } stored && date != stored)
            yield return new("tarih_farkli", CheckSeverity.Warning,
                $"Kütüphanedeki kararın tarihi {Format(stored)}; künyede {citation.DateText}.", null);
        if (citation.Chamber is { } chamber && DecisionCitationFinder.NormalizeChamber(match.Chamber) is { } storedChamber
            && chamber != storedChamber)
            yield return new("daire_farkli", CheckSeverity.Warning,
                $"Kütüphanedeki karar {storedChamber} kararı; künyede {chamber}.", null);
    }

    private static CitationIssue Similar(DecisionCitation citation, CourtDecision match, string? url)
    {
        var differs = DecisionCitationFinder.NormalizeNumber(match.CaseNumber ?? "") != citation.CaseNumber
            ? $"esas numarası {match.CaseNumber}"
            : $"karar numarası {match.DecisionNumber}";
        return new("benzer", CheckSeverity.Warning,
            $"Kütüphanede numaralarından biri tutan bir karar var ({DecisionLabels.Citation(match)}); onda {differs}. "
            + "Künyedeki numarayı kontrol edin.", url ?? match.SourceUrl);
    }

    private static string OfficialSearchUrl(DecisionCitation citation) => citation.Court switch
    {
        CitedCourt.Yargitay => "https://karararama.yargitay.gov.tr/",
        CitedCourt.AnayasaMahkemesi when citation.IsApplication => "https://kararlarbilgibankasi.anayasa.gov.tr/",
        CitedCourt.AnayasaMahkemesi => "https://normkararlarbilgibankasi.anayasa.gov.tr/",
        CitedCourt.Danistay => "https://karararama.danistay.gov.tr/",
        _ => "https://emsal.uyap.gov.tr/",
    };

    private static Court? ToCourt(CitedCourt? court) => court switch
    {
        CitedCourt.Yargitay => Court.Yargitay,
        CitedCourt.AnayasaMahkemesi => Court.AnayasaMahkemesi,
        CitedCourt.Danistay => Court.Danistay,
        CitedCourt.BolgeAdliyeMahkemesi => Court.BolgeAdliyeMahkemesi,
        _ => null,
    };

    private static async Task<MatchContext> LoadContextAsync(
        AppDbContext db, List<CourtDecision> candidates, bool includePrivate, CancellationToken ct)
    {
        var ids = candidates.Select(d => d.Id).ToList();
        var links = await db.ArticleDecisions.AsNoTracking().Where(l => ids.Contains(l.DecisionId)).ToListAsync(ct);
        var articleIds = links.Select(l => l.ArticleId).Distinct().ToList();
        var articles = await db.Articles.AsNoTracking().Where(a => articleIds.Contains(a.Id)).ToListAsync(ct);
        var visible = await VisibleArticles.FilterAsync(db, articles, includePrivate);
        var visibleIds = visible.Keys.ToList();
        var versions = await db.ArticleVersions.AsNoTracking()
            .Where(v => visibleIds.Contains(v.ArticleId))
            .Select(v => new VersionStart(v.ArticleId, v.ValidFrom, v.AmendedBy, v.IsRepealed))
            .ToListAsync(ct);

        var linksByDecision = links.ToLookup(l => l.DecisionId, l => l.ArticleId);
        // Anonim kullanıcı yalnız herkese açık ve (bağlıysa) görünür bir maddeye bağlı kararı bulur.
        var decisions = candidates
            .Where(d => includePrivate
                || (d.Visibility == Visibility.Public
                    && (!linksByDecision[d.Id].Any() || linksByDecision[d.Id].Any(visible.ContainsKey))))
            .ToList();
        return new MatchContext(decisions, linksByDecision, visible, versions.ToLookup(v => v.ArticleId));
    }

    private sealed record VersionStart(Guid ArticleId, DateOnly? ValidFrom, string? AmendedBy, bool IsRepealed);

    private sealed record MatchContext(
        List<CourtDecision> Decisions,
        ILookup<Guid, Guid> ArticlesOf,
        Dictionary<Guid, (Article Article, Law Law)> VisibleArticles,
        ILookup<Guid, VersionStart> Versions)
    {
        public (string Url, string Label)? PlaceOf(CourtDecision decision)
        {
            foreach (var articleId in ArticlesOf[decision.Id])
            {
                if (VisibleArticles.TryGetValue(articleId, out var visible))
                {
                    var tab = decision.Kind == DecisionKind.NormDenetimi ? "norm" : "ictihatlar";
                    return ($"/{visible.Law.Slug}/{visible.Article.Slug}?sekme={tab}", ArticleLabel(visible));
                }
            }
            return null;
        }

        /// <summary>Kararın bağlı olduğu maddelerin karardan sonraki son değişikliği (madde başına bir bulgu).</summary>
        public IEnumerable<CitationIssue> ChangesAfter(CourtDecision decision)
        {
            if (decision.DecisionDate is not { } date)
                yield break;
            foreach (var articleId in ArticlesOf[decision.Id])
            {
                if (!VisibleArticles.TryGetValue(articleId, out var visible))
                    continue;
                var latest = Versions[articleId].Where(v => v.ValidFrom > date).MaxBy(v => v.ValidFrom);
                if (latest is null)
                    continue;
                var by = latest.IsRepealed ? "mülga" : latest.AmendedBy is { Length: > 0 } amended ? amended : "değişiklik";
                yield return new("karardan_sonra_degisti", CheckSeverity.Info,
                    $"Karardan sonra {ArticleLabel(visible)} değişti ({by}, {Format(latest.ValidFrom!.Value)}); "
                    + "kararın hâlâ geçerli olup olmadığını kontrol edin.",
                    $"/{visible.Law.Slug}/{visible.Article.Slug}?sekme=degisiklikler");
            }
        }

        private static string ArticleLabel((Article Article, Law Law) visible) =>
            $"{visible.Law.ShortName ?? visible.Law.Number} m. {visible.Article.Number}";
    }

    private static string Format(DateOnly date) => date.ToString("dd.MM.yyyy");
}

/// <summary>Karar künyesinin tek biçimli yazımı: "Yargıtay 1. Ceza Dairesi, 12.03.2019, E. 2018/1234, K. 2019/567".</summary>
internal static class DecisionLabels
{
    public static string Citation(CourtDecision d) =>
        Citation(d.Court, d.Chamber, d.DecisionDate, null, d.CaseNumber, d.DecisionNumber, isApplication: false);

    public static string Citation(
        Court? court, string? chamber, DateOnly? date, string? dateText, string? caseNumber, string? decisionNumber, bool isApplication)
    {
        var parts = new[]
        {
            string.Join(' ', new[] { court is { } c ? CourtName(c) : null, chamber }.OfType<string>()),
            date?.ToString("dd.MM.yyyy") ?? dateText,
            caseNumber is null ? null : isApplication ? $"B. No: {caseNumber}" : $"E. {caseNumber}",
            decisionNumber is null ? null : $"K. {decisionNumber}",
        };
        return string.Join(", ", parts.Where(p => !string.IsNullOrEmpty(p)));
    }

    private static string CourtName(Court court) => court switch
    {
        Court.Yargitay => "Yargıtay",
        Court.AnayasaMahkemesi => "Anayasa Mahkemesi",
        Court.Aihm => "AİHM",
        Court.Danistay => "Danıştay",
        Court.BolgeAdliyeMahkemesi => "Bölge Adliye Mahkemesi",
        Court.UyusmazlikMahkemesi => "Uyuşmazlık Mahkemesi",
        _ => "",
    };
}
