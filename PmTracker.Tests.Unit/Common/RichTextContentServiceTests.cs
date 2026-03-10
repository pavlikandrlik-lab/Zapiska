using System.Net;
using System.Text;
using FluentAssertions;
using PmTracker.Web.Services.Common;

namespace PmTracker.Tests.Unit.Common;

public sealed class RichTextContentServiceTests
{
    private readonly RichTextContentService _sut = new();

    [Fact]
    public void NormalizeForStorage_ShouldSanitizeHtml_AndRemoveUnsafeScriptAndHref()
    {
        const string input = "<p><strong>Safe</strong><script>alert('x')</script><a href=\"javascript:alert('x')\">bad</a><a href=\"https://example.com\">ok</a></p>";

        var result = _sut.NormalizeForStorage(input);

        result.Should().Contain("<strong>Safe</strong>");
        result.Should().Contain("https://example.com");
        result.Should().NotContain("<script");
        result.Should().NotContain("javascript:");
    }

    [Fact]
    public void ToSafeHtml_ShouldConvertLegacyPlainText_AndPreserveLineBreaks()
    {
        const string input = "Prvni radek\nDruhy radek";

        var result = _sut.ToSafeHtml(input);

        result.Should().Contain("<p>");
        result.Should().Contain("Prvni radek<br>Druhy radek");
    }

    [Theory]
    [InlineData("<p><br></p>")]
    [InlineData("<p>   </p>")]
    [InlineData("   ")]
    [InlineData("")]
    public void HasVisibleText_ShouldReturnFalse_ForEmptyRichText(string input)
    {
        var hasVisibleText = _sut.HasVisibleText(input);

        hasVisibleText.Should().BeFalse();
    }

    [Fact]
    public void ToSafeHtml_ShouldAllowHttpHttpsAndMailtoOnly()
    {
        const string input = "<p><a href=\"https://example.com\">https</a> <a href=\"mailto:test@example.com\">mail</a> <a href=\"ftp://example.com/file\">ftp</a></p>";

        var result = _sut.ToSafeHtml(input);

        result.Should().Contain("https://example.com");
        result.Should().Contain("mailto:test@example.com");
        result.Should().NotContain("ftp://example.com/file");
    }

    [Fact]
    public void ToSafeHtml_ShouldPreserveCzechDiacritics()
    {
        const string input = "<p><strong>Tučné vyjádření</strong></p>";

        var result = _sut.ToSafeHtml(input);
        var decoded = WebUtility.HtmlDecode(result).Normalize(NormalizationForm.FormC);

        decoded.Should().Contain("Tučné vyjádření");
    }
}
