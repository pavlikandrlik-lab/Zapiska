using FluentAssertions;
using PmTracker.Web.Services.Common;

namespace PmTracker.Tests.Unit.Common;

/// <summary>Uživatel 2026-10-08: fráze se hledá i přes formátování — v čistém textu bez HTML.</summary>
public sealed class RichTextSearchTextTests
{
    [Theory]
    [InlineData("<p>pes a <strong>kočka</strong> spali</p>", "pes a kočka spali")]
    [InlineData("<p>pes&nbsp;a<br>kočka</p><p>spali</p>", "pes a kočka spali")]
    [InlineData("<ul><li>jedna</li><li>dva</li></ul>", "jedna dva")]
    [InlineData("<p>A &amp; B &lt;tag&gt;</p>", "A & B <tag>")]
    [InlineData("<p><a href=\"https://x.cz\">odkaz</a> text</p>", "odkaz text")]
    [InlineData("prostý text bez značek", "prostý text bez značek")]
    public void FromHtml_VratiTextBezZnacekSJednouMezerou(string html, string expected)
    {
        RichTextSearchText.FromHtml(html).Should().Be(expected);
    }

    [Fact]
    public void FromHtml_NullZustaneNull()
    {
        RichTextSearchText.FromHtml(null).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("<p></p>")]
    [InlineData("<p><br></p>")]
    public void FromHtml_HtmlBezTextu_JePrazdnyRetezec_NeNull(string html)
    {
        // NULL znamená „nedopočteno" — prázdné HTML by jinak dopočet vybíral pořád dokola.
        RichTextSearchText.FromHtml(html).Should().Be(string.Empty);
    }
}
