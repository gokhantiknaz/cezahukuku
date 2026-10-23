using System.Globalization;
using System.Text;

namespace CezaHukuku.Parsing.Text;

/// <summary>
/// Türkçe metin normalizasyonu. İndeksleme ve sorgu tarafında aynı fonksiyon kullanılmalıdır;
/// aksi halde "İHMAL" ile "ihmal" ya da "oldurme" ile "öldürme" eşleşmez.
/// </summary>
public static class TurkishText
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>Türkçe kurallarıyla küçük harfe çevirir (İ→i, I→ı) ve boşlukları sadeleştirir.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var lowered = text.Normalize(NormalizationForm.FormC).ToLower(Turkish);
        return CollapseWhitespace(lowered);
    }

    /// <summary>
    /// Normalize edilmiş metnin aksansız hâli ("öldürme" → "oldurme"). Kullanıcı Türkçe karakter
    /// kullanmadan yazdığında eşleşme sağlamak için ikinci bir indeks sütununda kullanılır.
    /// </summary>
    public static string Fold(string? text)
    {
        var normalized = Normalize(text);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            sb.Append(c switch
            {
                'ç' => 'c',
                'ğ' => 'g',
                'ı' => 'i',
                'ö' => 'o',
                'ş' => 's',
                'ü' => 'u',
                'â' => 'a',
                'î' => 'i',
                'û' => 'u',
                _ => c,
            });
        }
        return sb.ToString();
    }

    private static string CollapseWhitespace(string text)
    {
        var sb = new StringBuilder(text.Length);
        var previousWasSpace = false;
        foreach (var c in text.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                if (!previousWasSpace)
                    sb.Append(' ');
                previousWasSpace = true;
            }
            else
            {
                sb.Append(c);
                previousWasSpace = false;
            }
        }
        return sb.ToString();
    }
}
