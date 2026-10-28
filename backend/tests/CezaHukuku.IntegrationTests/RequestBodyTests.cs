using System.Text;
using System.Text.Json;

namespace CezaHukuku.IntegrationTests;

/// <summary>Okunamayan istek gövdesi: 500 değil, alan bazında Türkçe hatalı 400.</summary>
[Collection(ApiCollection.Name)]
public class RequestBodyTests(ApiFactory factory)
{
    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == 400, $"Beklenen 400, gelen {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement;
    }

    [Fact]
    public async Task InvalidDate_ReturnsFieldError()
    {
        // Tarayıcının tarih kutusu beş haneli yılı gönderebiliyor.
        var admin = await factory.CreateAdminClientAsync();
        var problem = await ProblemAsync(await admin.PostAsync($"/api/admin/laws/{Guid.NewGuid()}/import/preview",
            Json("""{"text":"Madde 1 - Deneme","joinWrappedLines":false,"validFrom":"20055-06-01","applyMetadata":false,"importFootnotes":false}""")));
        Assert.Equal("Geçerli bir tarih girin (gg.aa.yyyy).", problem.GetProperty("errors").GetProperty("validFrom")[0].GetString());

        var anonymous = factory.CreateAnonymousClient();
        problem = await ProblemAsync(await anonymous.PostAsync("/api/tools/citation-check",
            Json("""{"text":"TCK 81","referenceDate":"01.06.2005"}""")));
        Assert.True(problem.GetProperty("errors").TryGetProperty("referenceDate", out _));
    }

    [Fact]
    public async Task MalformedJson_ReturnsBadRequest()
    {
        var anonymous = factory.CreateAnonymousClient();
        var problem = await ProblemAsync(await anonymous.PostAsync("/api/tools/citation-check", Json("""{"text": """)));
        Assert.Equal("Gönderilen veri okunamadı.", problem.GetProperty("title").GetString());
    }
}
