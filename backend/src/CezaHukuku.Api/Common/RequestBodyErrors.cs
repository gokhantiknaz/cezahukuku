using System.Text.Json;

namespace CezaHukuku.Api.Common;

/// <summary>
/// İstek gövdesi okunamadığında (yanlış biçimde alan, bozuk JSON) dönen 400'e alan bazında Türkçe hata ekler:
/// panel formları hatayı ilgili alanın altında gösterir ("validFrom": "Geçerli bir tarih girin").
/// </summary>
public static class RequestBodyErrors
{
    public static void AddFieldErrors(ProblemDetailsContext context)
    {
        if (context.Exception is not BadHttpRequestException { InnerException: JsonException json })
            return;

        context.ProblemDetails.Title = "Gönderilen veri okunamadı.";
        context.ProblemDetails.Detail = null;
        if (FieldName(json.Path) is { } field)
            context.ProblemDetails.Extensions["errors"] = new Dictionary<string, string[]> { [field] = [Message(json)] };
    }

    /// <summary>"$.validFrom" → "validFrom", "$.paragraphs[0].text" → "paragraphs[0].text"; kök ya da yol yoksa null.</summary>
    public static string? FieldName(string? path) =>
        path is { Length: > 2 } && path.StartsWith("$.", StringComparison.Ordinal) ? path[2..] : null;

    private static string Message(JsonException json) =>
        json.InnerException is FormatException format && format.Message.Contains("Date", StringComparison.Ordinal)
            // Tarayıcının tarih kutusu yıla dört rakamdan fazlasını yazdırabiliyor (20055-06-01).
            ? "Geçerli bir tarih girin (gg.aa.yyyy)."
            : "Bu alanın değeri geçersiz.";
}
