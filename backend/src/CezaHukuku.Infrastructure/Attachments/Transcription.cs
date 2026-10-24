using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace CezaHukuku.Infrastructure.Attachments;

/// <summary>Yazıya dökülen sesin bir bölümü.</summary>
public sealed record TranscriptSegment(double StartSeconds, string Text);

public sealed record Transcript(IReadOnlyList<TranscriptSegment> Segments, double? DurationSeconds);

/// <summary>Sesli notu yazıya döker. Ayarlanmamışsa (<see cref="IsEnabled"/> false) sesli not metinsiz saklanır.</summary>
public interface ITranscriber
{
    bool IsEnabled { get; }

    Task<Transcript> TranscribeAsync(Stream audio, string fileName, string contentType, CancellationToken ct = default);
}

/// <summary>
/// Yazıya dökme ayarları. OpenAI'nin <c>/v1/audio/transcriptions</c> uç noktası ve onunla uyumlu sunucular
/// (whisper.cpp server, faster-whisper-server) desteklenir: bulut yerine kendi sunucumuzda yerel model çalıştırmak
/// (KVKK, ücretsiz) yalnız <see cref="BaseUrl"/> değiştirilerek seçilir.
/// </summary>
public sealed class TranscriptionOptions
{
    public const string Section = "Transcription";

    /// <summary>Boşsa yazıya dökme kapalı. Örn. "https://api.openai.com/v1" ya da "http://localhost:8000/v1".</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Bulut servisinde zorunlu; yerel sunucuda genelde boş.</summary>
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "whisper-1";

    public string Language { get; set; } = "tr";
}

public sealed class OpenAiCompatibleTranscriber(HttpClient http, IOptions<TranscriptionOptions> options) : ITranscriber
{
    private readonly TranscriptionOptions settings = options.Value;

    public bool IsEnabled => !string.IsNullOrWhiteSpace(settings.BaseUrl);

    public async Task<Transcript> TranscribeAsync(Stream audio, string fileName, string contentType, CancellationToken ct = default)
    {
        if (!IsEnabled)
            throw new InvalidOperationException("Yazıya dökme ayarlanmadı.");

        using var form = new MultipartFormDataContent();
        var file = new StreamContent(audio);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(file, "file", fileName);
        form.Add(new StringContent(settings.Model), "model");
        form.Add(new StringContent(settings.Language), "language");
        // verbose_json: bölümler zaman damgalarıyla gelir (nota tıklayınca sesin o yerine gidilebilsin).
        form.Add(new StringContent("verbose_json"), "response_format");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.BaseUrl!.TrimEnd('/')}/audio/transcriptions") { Content = form };
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Yazıya dökme servisi {(int)response.StatusCode} döndü.");

        var result = JsonSerializer.Deserialize<VerboseTranscription>(body)
            ?? throw new InvalidDataException("Yazıya dökme servisinin yanıtı okunamadı.");
        var segments = result.Segments is { Count: > 0 }
            ? result.Segments.Where(s => !string.IsNullOrWhiteSpace(s.Text)).Select(s => new TranscriptSegment(s.Start, s.Text.Trim())).ToList()
            : string.IsNullOrWhiteSpace(result.Text) ? [] : [new TranscriptSegment(0, result.Text.Trim())];
        return new Transcript(segments, result.Duration);
    }

    private sealed class VerboseTranscription
    {
        [JsonPropertyName("text")] public string? Text { get; set; }
        [JsonPropertyName("duration")] public double? Duration { get; set; }
        [JsonPropertyName("segments")] public List<VerboseSegment>? Segments { get; set; }
    }

    private sealed class VerboseSegment
    {
        [JsonPropertyName("start")] public double Start { get; set; }
        [JsonPropertyName("text")] public string Text { get; set; } = "";
    }
}
