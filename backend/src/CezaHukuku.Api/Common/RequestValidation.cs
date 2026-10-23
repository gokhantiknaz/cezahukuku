using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CezaHukuku.Api.Common;

/// <summary>
/// İstek doğrulama hatalarını alan adına göre toplar ve ValidationProblem (400) olarak döner.
/// Alan adları JSON'daki camelCase adlardır; web formu hatayı ilgili alanın altında gösterebilsin.
/// </summary>
internal sealed class ValidationErrors
{
    private readonly Dictionary<string, List<string>> errors = [];

    public bool HasErrors => errors.Count > 0;

    public ValidationErrors Add(string field, string message)
    {
        if (!errors.TryGetValue(field, out var messages))
            errors[field] = messages = [];
        messages.Add(message);
        return this;
    }

    public ValidationErrors Required(string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Add(field, "Bu alan zorunludur.");
        return MaxLength(field, value, maxLength);
    }

    public ValidationErrors MaxLength(string field, string? value, int maxLength)
    {
        if (value is not null && value.Trim().Length > maxLength)
            Add(field, $"En fazla {maxLength} karakter olabilir.");
        return this;
    }

    public ValidationProblem ToProblem() =>
        TypedResults.ValidationProblem(errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));

    public static ValidationProblem Single(string field, string message) =>
        new ValidationErrors().Add(field, message).ToProblem();
}

internal static class InputText
{
    /// <summary>
    /// Kayda girecek metni sadeleştirir: Unicode NFC (kopyala-yapıştırla gelen ayrık aksanlar arama ve
    /// karşılaştırmada sorun çıkarmasın) ve baştaki/sondaki boşluklar. Boş metin null olur.
    /// </summary>
    public static string? Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        return text.Normalize(NormalizationForm.FormC).Trim();
    }

    public static string CleanRequired(string? text) => Clean(text) ?? "";
}
