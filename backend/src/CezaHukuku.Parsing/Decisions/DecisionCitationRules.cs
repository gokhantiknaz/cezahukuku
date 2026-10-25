namespace CezaHukuku.Parsing.Decisions;

/// <summary>Denetim bulgusunun ağırlığı; sıralama önemlidir (en ağır olan atıfın durumunu belirler).</summary>
public enum CheckSeverity
{
    Ok,
    Info,
    Warning,
    Error,
}

public sealed record CheckFinding(string Code, CheckSeverity Severity, string Message);

/// <summary>
/// Künyenin kendi içinde tutarlılığı (kaynağa bakmadan): eksik parça, geçersiz ya da ileri tarih, karar numarasının
/// yılı ile karar tarihinin yılı (karar numarası kararın verildiği yılın sırasıdır), esas yılının karar yılından
/// sonra olması (dava karardan önce açılır), Yargıtay'da olmayan daire numarası. Uydurma ya da yanlış yazılmış
/// künyelerin çoğu bu denetimlerde yakalanır.
/// </summary>
public static class DecisionCitationRules
{
    /// <summary>Yargıtay'da en çok 23 ceza ve 23 hukuk dairesi olmuştur.</summary>
    private const int MaxYargitayChamber = 23;

    public static IReadOnlyList<CheckFinding> Check(DecisionCitation citation, DateOnly today)
    {
        var findings = new List<CheckFinding>();

        var missing = new List<string>();
        if (citation.Court is null)
            missing.Add("mahkeme");
        if (citation.Court is CitedCourt.Yargitay or CitedCourt.BolgeAdliyeMahkemesi && citation.Chamber is null)
            missing.Add("daire");
        if (citation.CaseNumber is null)
            missing.Add("esas no");
        if (citation.DecisionNumber is null && !citation.IsApplication)
            missing.Add("karar no");
        if (citation.DateText is null)
            missing.Add("tarih");
        if (missing.Count > 0)
            findings.Add(new("eksik", CheckSeverity.Warning, $"Künyede eksik: {string.Join(", ", missing)}."));

        if (citation.DateText is not null && citation.Date is null)
            findings.Add(new("gecersiz_tarih", CheckSeverity.Error, $"“{citation.DateText}” geçerli bir tarih değil."));
        if (citation.Date > today)
            findings.Add(new("ileri_tarih", CheckSeverity.Error, $"Karar tarihi ({Format(citation.Date.Value)}) bugünden sonra."));

        var caseYear = YearOf(citation.CaseNumber);
        var decisionYear = YearOf(citation.DecisionNumber);
        if (decisionYear is { } k && citation.Date is { } date && k != date.Year)
            findings.Add(new("karar_yili", CheckSeverity.Error,
                $"Karar numarasının yılı ({k}) karar tarihinin yılıyla ({date.Year}) uyuşmuyor; karar numarası kararın verildiği yılda alınır."));
        if (caseYear is { } e && (decisionYear ?? citation.Date?.Year) is { } upper && e > upper)
            findings.Add(citation.IsApplication
                ? new("esas_yili", CheckSeverity.Error, $"Başvuru yılı ({e}) karar yılından ({upper}) sonra olamaz.")
                : new("esas_yili", CheckSeverity.Error, $"Esas yılı ({e}) karar yılından ({upper}) sonra olamaz; dava karardan önce açılır."));
        foreach (var year in (int?[])[caseYear, decisionYear])
        {
            if (year is < 1900)
                findings.Add(new("yil", CheckSeverity.Error, $"Numaradaki yıl ({year}) geçersiz görünüyor."));
        }

        if (citation.Court == CitedCourt.Yargitay && ChamberNumber(citation.Chamber) is > MaxYargitayChamber and var chamber)
            findings.Add(new("daire_no", CheckSeverity.Warning, $"Yargıtay'da {chamber}. daire olmamıştır (en çok {MaxYargitayChamber})."));

        return findings;
    }

    private static int? YearOf(string? number) =>
        number is { Length: >= 4 } && int.TryParse(number.AsSpan(0, 4), out var year) ? year : null;

    private static int? ChamberNumber(string? chamber)
    {
        if (chamber is null)
            return null;
        var dot = chamber.IndexOf('.');
        return dot > 0 && int.TryParse(chamber.AsSpan(0, dot), out var number) ? number : null;
    }

    private static string Format(DateOnly date) => date.ToString("dd.MM.yyyy");
}
