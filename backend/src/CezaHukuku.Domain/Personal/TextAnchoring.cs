using CezaHukuku.Domain.Laws;

namespace CezaHukuku.Domain.Personal;

/// <summary>
/// Metindeki bir yerin kalıcı adresi (W3C Web Annotation: konum + alıntı seçicisi). Konum fıkra/bent kimliği ve
/// o metindeki başlangıç-uzunluk; alıntı ve çevresindeki metin (önek/sonek) metin değişince yeri yeniden bulmaya
/// yarar.
/// </summary>
/// <param name="ItemId">Bentteyse bendin kimliği; fıkranın kendi metnindeyse null.</param>
public sealed record TextAnchor(Guid ParagraphId, Guid? ItemId, int Start, int Length, string Quote, string Prefix, string Suffix);

/// <summary>Bir sürümün metnindeki konum.</summary>
public sealed record TextPosition(Guid ParagraphId, Guid? ItemId, int Start, int Length);

/// <summary>Sürüm metnindeki tek parça metin: fıkranın giriş metni ya da bir bent.</summary>
public sealed record TextBlock(Guid ParagraphId, Guid? ItemId, string Text);

public static class TextAnchoring
{
    /// <summary>Alıntının önünde ve arkasında saklanan metin uzunluğu.</summary>
    public const int ContextLength = 32;

    /// <summary>
    /// Alıntı başka bir fıkraya taşınmış bulunursa kabul edilmesi için ya çevresi tutmalı ya da alıntı en az bu
    /// uzunlukta olmalı ("hapis" gibi kısa bir sözcük metnin her yerinde geçer).
    /// </summary>
    private const int MinQuoteLengthWithoutContext = 20;

    public static IEnumerable<TextBlock> Blocks(IReadOnlyList<Paragraph> paragraphs) =>
        paragraphs.SelectMany(p =>
            new[] { new TextBlock(p.Id, null, p.Text) }.Concat(p.Items.Select(i => new TextBlock(p.Id, i.Id, i.Text))));

    /// <summary>Sürüm metnindeki konumdan ankraj oluşturur; konum metinde yoksa ya da boşluktan ibaretse null.</summary>
    public static TextAnchor? Create(IReadOnlyList<Paragraph> paragraphs, TextPosition position)
    {
        var block = Blocks(paragraphs).FirstOrDefault(b => b.ParagraphId == position.ParagraphId && b.ItemId == position.ItemId);
        if (block is null || position.Start < 0 || position.Length <= 0 || position.Start + position.Length > block.Text.Length)
            return null;

        var quote = block.Text.Substring(position.Start, position.Length);
        if (string.IsNullOrWhiteSpace(quote))
            return null;

        var prefixStart = Math.Max(0, position.Start - ContextLength);
        var end = position.Start + position.Length;
        return new TextAnchor(
            position.ParagraphId, position.ItemId, position.Start, position.Length, quote,
            block.Text[prefixStart..position.Start],
            block.Text.Substring(end, Math.Min(ContextLength, block.Text.Length - end)));
    }

    /// <summary>
    /// Ankrajı verilen sürümün metninde bulur. Önce kaydedildiği yer (aynı fıkra/bent, aynı konumda aynı alıntı);
    /// değilse alıntının metindeki geçişleri arasından çevresi (önek/sonek) en çok tutanı, eşitlikte aynı
    /// fıkra/bentte olanı ve eski konuma yakın olanı seçer. Alıntı metinden kalkmışsa null.
    /// </summary>
    public static TextPosition? Locate(TextAnchor anchor, IReadOnlyList<Paragraph> paragraphs)
    {
        var blocks = Blocks(paragraphs).ToList();
        var home = blocks.FirstOrDefault(b => b.ParagraphId == anchor.ParagraphId && b.ItemId == anchor.ItemId);
        if (home is not null
            && anchor.Start + anchor.Length <= home.Text.Length
            && string.CompareOrdinal(home.Text, anchor.Start, anchor.Quote, 0, anchor.Length) == 0)
        {
            return new TextPosition(home.ParagraphId, home.ItemId, anchor.Start, anchor.Length);
        }

        (TextPosition Position, int Context, bool SameBlock, int Distance)? best = null;
        foreach (var block in blocks)
        {
            var sameBlock = block == home;
            for (var index = block.Text.IndexOf(anchor.Quote, StringComparison.Ordinal);
                 index >= 0;
                 index = block.Text.IndexOf(anchor.Quote, index + 1, StringComparison.Ordinal))
            {
                var context = CommonSuffixLength(block.Text.AsSpan(0, index), anchor.Prefix)
                              + CommonPrefixLength(block.Text.AsSpan(index + anchor.Quote.Length), anchor.Suffix);
                if (!sameBlock && context == 0 && anchor.Quote.Length < MinQuoteLengthWithoutContext)
                    continue;

                var candidate = (new TextPosition(block.ParagraphId, block.ItemId, index, anchor.Quote.Length),
                    context, sameBlock, Math.Abs(index - anchor.Start));
                if (best is null || IsBetter(candidate, best.Value))
                    best = candidate;
            }
        }
        return best?.Position;
    }

    private static bool IsBetter(
        (TextPosition Position, int Context, bool SameBlock, int Distance) candidate,
        (TextPosition Position, int Context, bool SameBlock, int Distance) current)
    {
        if (candidate.Context != current.Context)
            return candidate.Context > current.Context;
        if (candidate.SameBlock != current.SameBlock)
            return candidate.SameBlock;
        return candidate.SameBlock && candidate.Distance < current.Distance;
    }

    private static int CommonSuffixLength(ReadOnlySpan<char> text, string suffix)
    {
        var length = 0;
        while (length < text.Length && length < suffix.Length && text[^(length + 1)] == suffix[^(length + 1)])
            length++;
        return length;
    }

    private static int CommonPrefixLength(ReadOnlySpan<char> text, string prefix)
    {
        var length = 0;
        while (length < text.Length && length < prefix.Length && text[length] == prefix[length])
            length++;
        return length;
    }
}
