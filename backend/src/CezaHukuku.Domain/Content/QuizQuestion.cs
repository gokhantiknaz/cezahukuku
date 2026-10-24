using CezaHukuku.Domain.Common;
using CezaHukuku.Domain.Laws;

namespace CezaHukuku.Domain.Content;

/// <summary>
/// "Kendini Test Et" sorusu: çoktan seçmeli, tek doğru. Bir kanuna, isteğe bağlı olarak bir maddeye bağlanır (yanıttan
/// sonra maddeye gidilebilsin). Maddenin görünürlüğü soruya da geçer.
/// </summary>
public sealed class QuizQuestion : Entity
{
    public const int MinOptions = 2;
    public const int MaxOptions = 6;

    public Guid LawId { get; set; }
    public Guid? ArticleId { get; set; }
    public string Text { get; set; } = "";
    public string[] Options { get; set; } = [];

    /// <summary>Doğru şıkkın sırası (0'dan).</summary>
    public int CorrectIndex { get; set; }

    /// <summary>Yanıttan sonra gösterilen açıklama.</summary>
    public string? Explanation { get; set; }

    public Visibility Visibility { get; set; }
}
