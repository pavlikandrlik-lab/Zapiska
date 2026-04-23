using FluentAssertions;
using PmTracker.Web.Services.ServiceDesk;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class VyjadreniHtmlTextTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void ToPlainText_PrazdnyVstup_VraciPrazdnyString(string? input, string expected)
    {
        VyjadreniHtmlText.ToPlainText(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("<b>tučný</b> text", "tučný text")]
    [InlineData("<B>VELKÉ</B> a <strong>taky velké</strong>", "VELKÉ a taky velké")]
    [InlineData("<i>kurzíva</i>", "kurzíva")]
    public void ToPlainText_TagyStripne(string input, string expected)
    {
        VyjadreniHtmlText.ToPlainText(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("první<br>druhá", "první\ndruhá")]
    [InlineData("a<br/>b<br />c<BR>d", "a\nb\nc\nd")]
    public void ToPlainText_BrSeStavaNewline(string input, string expected)
    {
        VyjadreniHtmlText.ToPlainText(input).Should().Be(expected);
    }

    [Fact]
    public void ToPlainText_LiSeStavaBulletNewline()
    {
        var result = VyjadreniHtmlText.ToPlainText("<li>první</li><li>druhá</li>");
        result.Should().Contain("• první").And.Contain("• druhá");
    }

    [Theory]
    [InlineData("&nbsp;pred text", "pred text")]   // nbsp → space → trim
    [InlineData("quoted &#34;text&#34;", "quoted \"text\"")]
    [InlineData("&lt;b&gt; literal &lt;/b&gt;", "<b> literal </b>")]
    [InlineData("a &amp; b", "a & b")]
    public void ToPlainText_HtmlEntityDekoduje(string input, string expected)
    {
        VyjadreniHtmlText.ToPlainText(input).Should().Be(expected);
    }

    [Fact]
    public void ToPlainText_OdkazNaTicket_NechaJenAnchorText()
    {
        // Realistický cross-ref pattern z HOT_VYJADRENI
        var input = "Viz <A HREF='./zobraz_zaznam.asp?pid=XYZ' target='_VAZBA_HOTLINE'>123456</A> detail.";
        var result = VyjadreniHtmlText.ToPlainText(input);
        result.Should().Be("Viz 123456 detail.");
    }

    [Fact]
    public void ToPlainText_PrilohaInlineLink_NechaJenNazevSouboru()
    {
        // Typ "P" — atachment reference v text form
        var input = "Příloha: <a href=./vyjadreni_prilohy/doc.docx target=_HOTLINE>doc.docx</a>";
        var result = VyjadreniHtmlText.ToPlainText(input);
        result.Should().Contain("doc.docx");
        result.Should().NotContain("<a");
    }

    [Fact]
    public void ToPlainText_MultiWhitespace_Normalizuje()
    {
        var input = "a    b\n\n\n\n\nc";
        var result = VyjadreniHtmlText.ToPlainText(input);
        result.Should().Be("a b\n\nc");
    }

    [Fact]
    public void ToPlainText_NbspPrefix_BezChybBezTrailing()
    {
        // Real pattern z K10 archivace: "&nbsp;Záznam byl převeden do archivu.<BR>"
        var input = "&nbsp;Záznam byl převeden do archivu.<BR>";
        VyjadreniHtmlText.ToPlainText(input).Should().Be("Záznam byl převeden do archivu.");
    }
}
