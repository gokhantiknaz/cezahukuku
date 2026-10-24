using System.Net;
using System.Text;
using CezaHukuku.Infrastructure.Attachments;
using Microsoft.Extensions.Options;

namespace CezaHukuku.UnitTests.Content;

public class AttachmentProcessingTests
{
    [Fact]
    public void PdfText_JoinsHyphenatedLines_AndTrims()
    {
        Assert.Equal("Bir insanı kasten öldüren\nkişi cezalandırılır.",
            PdfTextExtractor.Normalize("  Bir insanı kasten öl-\r\ndüren  \n  kişi cezalandırılır.  \n"));
    }

    [Fact]
    public void PdfText_KeepsHyphenBeforeCapitalOrNumber()
    {
        Assert.Equal("Türk Ceza Kanunu -\nMadde 81", PdfTextExtractor.Normalize("Türk Ceza Kanunu -\nMadde 81"));
    }

    [Fact]
    public async Task Transcriber_SendsOpenAiRequest_AndReadsSegments()
    {
        var handler = new StubHandler("""
            {"text":"Kasten öldürme. Olası kast.","duration":7.5,
             "segments":[{"start":0.0,"text":" Kasten öldürme."},{"start":3.2,"text":" Olası kast."}]}
            """);
        var transcriber = new OpenAiCompatibleTranscriber(new HttpClient(handler),
            Options.Create(new TranscriptionOptions { BaseUrl = "http://whisper.local/v1/", ApiKey = "anahtar", Model = "whisper-1" }));

        var transcript = await transcriber.TranscribeAsync(new MemoryStream([1, 2, 3]), "not.webm", "audio/webm");

        Assert.Equal([new TranscriptSegment(0, "Kasten öldürme."), new TranscriptSegment(3.2, "Olası kast.")], transcript.Segments);
        Assert.Equal(7.5, transcript.DurationSeconds);
        Assert.Equal("http://whisper.local/v1/audio/transcriptions", handler.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer anahtar", handler.Request.Headers.Authorization!.ToString());
        Assert.Contains("name=language", handler.Body);
        Assert.Contains("verbose_json", handler.Body);
    }

    [Fact]
    public void Transcriber_IsDisabledWithoutBaseUrl() =>
        Assert.False(new OpenAiCompatibleTranscriber(new HttpClient(), Options.Create(new TranscriptionOptions())).IsEnabled);

    private sealed class StubHandler(string response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string Body { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }
}
