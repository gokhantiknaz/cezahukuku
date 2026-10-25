using System.Text;
using CezaHukuku.Infrastructure.Attachments;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CezaHukuku.Api.Common;

/// <param name="IsPdf">Metin PDF'ten geldi: satırlar sayfa genişliğinde kırılmıştır.</param>
internal sealed record ExtractedFileText(string Text, bool IsPdf);

/// <summary>Yüklenen Word (.docx), PDF ya da düz metin dosyasının metni (dosya saklanmaz).</summary>
internal static class TextFiles
{
    /// <param name="pageSeparator">PDF sayfaları arasına konan ayraç.</param>
    public static async Task<Results<Ok<ExtractedFileText>, ValidationProblem>> ExtractAsync(
        IFormFile file, long maxBytes, string pageSeparator, CancellationToken ct)
    {
        if (file.Length == 0 || file.Length > maxBytes)
            return ValidationErrors.Single("file", $"Dosya boş ya da çok büyük (en fazla {maxBytes / 1024 / 1024} MB).");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        try
        {
            return extension switch
            {
                ".docx" => TypedResults.Ok(new ExtractedFileText(DocxTextExtractor.Extract(buffer), false)),
                ".pdf" => TypedResults.Ok(new ExtractedFileText(string.Join(pageSeparator, PdfTextExtractor.ExtractPages(buffer)), true)),
                ".txt" => TypedResults.Ok(new ExtractedFileText(Encoding.UTF8.GetString(buffer.ToArray()), false)),
                ".doc" => ValidationErrors.Single("file", "Eski Word (.doc) biçimi okunamıyor; belgeyi Word'de .docx olarak kaydedip yükleyin."),
                _ => ValidationErrors.Single("file", "Yalnız .docx, .pdf ve .txt dosyası yüklenebilir."),
            };
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException or UglyToad.PdfPig.Core.PdfDocumentFormatException)
        {
            return ValidationErrors.Single("file", "Dosya okunamadı; bozuk ya da parola korumalı olabilir.");
        }
    }
}
