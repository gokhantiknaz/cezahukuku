namespace CezaHukuku.Parsing.Search;

/// <summary>
/// Aranan sözcüğün kaba gövdesi (aksansız): sondaki türetme/çekim eklerinden biri atılır. PostgreSQL'in Türkçe kök
/// bulucusu isim eklerini atar ("hırsızlığın" → "hırsızlık") ama fiilden türeyenleri birleştirmez ("öldüren" ile
/// "öldürme" ayrı kalır); bu gövde önek olarak arandığında ("oldur:*") ikisi de bulunur.
/// Yalnız uzun sözcüklerde ve gövde en az <see cref="MinRootLength"/> harf kalacaksa uygulanır; kısa sözcükte önek
/// eşleşmesi alakasız sonuç getirir.
/// </summary>
public static class TurkishRoot
{
    private const int MinWordLength = 6;
    private const int MinRootLength = 5;

    // Uzundan kısaya: önce "-mesinin" denenir, sonra "-me". Aksansız yazılır (Fold sonrası).
    private static readonly string[] Suffixes =
    [
        "melerinin", "malarinin", "mesinin", "masinin", "melerin", "malarin", "meleri", "malari",
        "ilmesi", "ilmasi", "ulmesi", "ulmasi", "menin", "manin", "mesi", "masi", "meyi", "mayi", "mege", "maga",
        "erek", "arak", "ilir", "ulur", "ilen", "ulan", "mek", "mak", "dir", "dur", "me", "ma", "en", "an",
    ];

    public static string Guess(string folded)
    {
        if (folded.Length < MinWordLength || folded.Any(char.IsDigit))
            return folded;
        foreach (var suffix in Suffixes)
        {
            if (folded.EndsWith(suffix, StringComparison.Ordinal) && folded.Length - suffix.Length >= MinRootLength)
                return folded[..^suffix.Length];
        }
        return folded;
    }
}
