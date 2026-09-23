using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// FIX 2026-07-10: bez explicitního charset v Content-Type prohlížeč kódování CSS/JS
/// hádá — Edge na české Windows (i15) spadl na CP1250 a UTF-8 glyfy („›" breadcrumb
/// separátor) renderoval jako mojibake „â€ş". HTTP hlavička ukončuje hádání.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class StaticAssetCharsetTests
{
    private readonly ApiSqlFixture _fixture;
    public StaticAssetCharsetTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("/css/site.css", "text/css; charset=utf-8")]
    [InlineData("/js/site.js", "text/javascript; charset=utf-8")]
    public async Task TextAssets_DeclareUtf8Charset(string url, string expectedContentType)
    {
        using var client = _fixture.Factory.CreateClient();
        var response = await client.GetAsync(url);
        response.IsSuccessStatusCode.Should().BeTrue(url);
        response.Content.Headers.ContentType!.ToString().Should().Be(expectedContentType);
    }
}
