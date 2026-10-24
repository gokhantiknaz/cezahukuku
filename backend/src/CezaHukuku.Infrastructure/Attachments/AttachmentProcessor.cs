using CezaHukuku.Domain.Personal;
using CezaHukuku.Infrastructure.Files;
using CezaHukuku.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CezaHukuku.Infrastructure.Attachments;

/// <summary>Yeni ek yüklenince işleyiciyi uyandırır (beklemeden işlensin).</summary>
public sealed class AttachmentQueue
{
    private readonly SemaphoreSlim signal = new(0);

    public void Notify() => signal.Release();

    internal Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct) => signal.WaitAsync(timeout, ct);
}

/// <summary>
/// Bekleyen ekleri sırayla işler: PDF'in sayfa metinlerini çıkarır, sesli notu (ayarlıysa) yazıya döker. Durum
/// veritabanında tutulduğundan uygulama yeniden başlasa da yarım kalan iş sürer. Kayıt arama indeksini de günceller.
/// </summary>
public sealed class AttachmentProcessor(IServiceScopeFactory scopes, AttachmentQueue queue, ILogger<AttachmentProcessor> logger)
    : BackgroundService
{
    // Bildirim kaçarsa (başka sunucudan yükleme) bekleyenler en geç bu aralıkla yoklanır.
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                while (await ProcessNextAsync(stoppingToken))
                {
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Ek işlenirken beklenmeyen hata.");
            }

            try
            {
                await queue.WaitAsync(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Sıradaki bekleyen eki işler; bekleyen yoksa false.</summary>
    private async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var attachment = await db.Attachments
            .Where(a => a.Status == AttachmentStatus.Pending)
            .OrderBy(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (attachment is null)
            return false;

        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var transcriber = scope.ServiceProvider.GetRequiredService<ITranscriber>();
        List<AttachmentSegment> segments = [];
        try
        {
            await using var stream = await storage.OpenReadAsync(attachment.StorageKey, ct)
                ?? throw new FileNotFoundException("Dosya depoda bulunamadı.");
            segments = attachment.Kind == AttachmentKind.Pdf
                ? await ProcessPdfAsync(attachment, stream)
                : await ProcessAudioAsync(attachment, stream, transcriber, ct);
            attachment.Status = AttachmentStatus.Ready;
            attachment.Error = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Ek işlenemedi: {AttachmentId}", attachment.Id);
            attachment.Status = AttachmentStatus.Failed;
            attachment.Error = attachment.Kind == AttachmentKind.Pdf
                ? "PDF okunamadı; dosya bozuk ya da parola korumalı olabilir."
                : "Sesli not yazıya dökülemedi; daha sonra yeniden deneyebilirsiniz.";
        }

        await db.AttachmentSegments.Where(s => s.AttachmentId == attachment.Id).ExecuteDeleteAsync(ct);
        db.AttachmentSegments.AddRange(segments);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static async Task<List<AttachmentSegment>> ProcessPdfAsync(Attachment attachment, Stream stream)
    {
        // PdfPig konumlanabilir akış ister; depo uzak olabileceğinden bellekte okunur (en fazla MaxPdfBytes).
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        buffer.Position = 0;
        var pages = PdfTextExtractor.ExtractPages(buffer);

        attachment.PageCount = pages.Count;
        attachment.ScannedPageCount = pages.Count(p => p.Length == 0);
        return
        [
            .. pages.Select((text, i) => (text, i))
                .Where(p => p.text.Length > 0)
                .Select((p, index) => new AttachmentSegment { AttachmentId = attachment.Id, Index = index, Page = p.i + 1, Text = p.text }),
        ];
    }

    private static async Task<List<AttachmentSegment>> ProcessAudioAsync(
        Attachment attachment, Stream stream, ITranscriber transcriber, CancellationToken ct)
    {
        if (!transcriber.IsEnabled)
        {
            attachment.HasTranscript = false;
            return [];
        }
        var transcript = await transcriber.TranscribeAsync(stream, attachment.FileName, attachment.ContentType, ct);
        attachment.DurationSeconds = transcript.DurationSeconds;
        attachment.HasTranscript = transcript.Segments.Count > 0;
        return
        [
            .. transcript.Segments.Select((s, index) => new AttachmentSegment
            {
                AttachmentId = attachment.Id, Index = index, StartSeconds = s.StartSeconds, Text = s.Text,
            }),
        ];
    }
}
