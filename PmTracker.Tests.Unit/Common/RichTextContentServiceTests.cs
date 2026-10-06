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

    [Fact]
    public void ToSafeHtml_ShouldAllowLists_AndNormalizeQuillListMarkup()
    {
        const string input = "<ol><li data-list=\"bullet\">A</li><li data-list=\"ordered\">B</li></ol>";

        var result = _sut.ToSafeHtml(input);

        result.Should().Be("<ul><li>A</li></ul><ol><li>B</li></ol>");
    }

    [Fact]
    public void ToSafeHtml_ShouldPreserveAllowedListIndentClass_AndDropDisallowedClasses()
    {
        const string input = "<ul><li class=\"foo ql-indent-2 bar\">A</li><li class=\"ql-indent-9\">B</li></ul>";

        var result = _sut.ToSafeHtml(input);

        result.Should().Contain("<li class=\"ql-indent-2\">A</li>");
        result.Should().Contain("<li>B</li>");
        result.Should().NotContain("ql-indent-9");
        result.Should().NotContain("foo");
        result.Should().NotContain("bar");
    }

    [Fact]
    public void ToPlainText_ShouldPreserveLineBreaksBetweenListItems()
    {
        const string input = "<ul><li>První</li><li>Druhý</li></ul>";

        var result = _sut.ToPlainText(input);

        result.Should().Be("První\nDruhý");
    }

    // Ukládá se písmo tak, jak je: HtmlEncoder.Default dřív z „ř" dělal &#x159; a LIKE
    // v databázi pak slova s diakritikou nenašel (db_upgrade_1_4_6 data převádí zpět).
    [Fact]
    public void NormalizeForStorage_UkladaDiakritikuAOstatniZnakyBezEntit()
    {
        const string input = "<p>Řešení žádosti – „uvozovky“ 😀 Жук → ≥ ✓ ' +</p>";

        var result = _sut.NormalizeForStorage(input);

        result.Should().Be(input);
    }

    [Fact]
    public void NormalizeForStorage_KodujeJenZnakyNebezpecneVHtml()
    {
        const string input = "<p>a &lt; b &amp;&amp; c &gt; d &quot;e&quot;</p>";

        var result = _sut.NormalizeForStorage(input);

        result.Should().Be("<p>a &lt; b &amp;&amp; c &gt; d &quot;e&quot;</p>");
    }

    [Fact]
    public void NormalizeForStorage_OdkazSDiakritikou_KodujeJenAmpersandAUvozovky()
    {
        const string input = "<p><a href=\"https://example.cz/řešení?a=1&amp;b=2\">Řešení</a></p>";

        var result = _sut.NormalizeForStorage(input);

        result.Should().Be("<p><a href=\"https://example.cz/řešení?a=1&amp;b=2\">Řešení</a></p>");
    }

    [Fact]
    public void ToSafeHtml_PrevodHolehoTextu_ZachovaDiakritikuAKodujeZnacky()
    {
        var result = _sut.ToSafeHtml("Řádek 1\nŘádek <2> & víc");

        result.Should().Be("<p>Řádek 1<br>Řádek &lt;2&gt; &amp; víc</p>");
    }
}

