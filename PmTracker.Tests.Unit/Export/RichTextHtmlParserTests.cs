using FluentAssertions;
using PmTracker.Web.Services.Export;
using Xunit;

namespace PmTracker.Tests.Unit.Export;

/// <summary>
/// Parser rich textu vytažený 2026-09-08 z exportu záznamu, aby ho mohla použít i výzva.
/// Chování musí zůstat stejné — proto tyhle testy popisují dnešní stav, ne nový.
/// </summary>
public sealed class RichTextHtmlParserTests
{
    [Fact]
    public void Parse_OdstavceATucnyText()
    {
        var odstavce = RichTextHtmlParser.Parse("<p>Ahoj <strong>svete</strong></p>");

        var odstavec = odstavce.Should().ContainSingle().Which;
        odstavec.Tokens.Should().HaveCount(2);
        odstavec.Tokens[0].Text.Should().Be("Ahoj ");
        odstavec.Tokens[0].Bold.Should().BeFalse();
        odstavec.Tokens[1].Text.Should().Be("svete");
        odstavec.Tokens[1].Bold.Should().BeTrue();
    }

    [Fact]
    public void Parse_SeznamDaOdsazeni()
    {
        var odstavce = RichTextHtmlParser.Parse("<ul><li>prvni</li><li>druhy</li></ul>");

        odstavce.Should().HaveCount(2);
        odstavce.Should().OnlyContain(p => p.Tokens.Count > 0, "položky seznamu nesou text");
        odstavce.Should().OnlyContain(p => p.Tokens[0].Text == "• ",
            "odrážka se sází jako první běh textu");
    }

    [Fact]
    public void Parse_Odkaz_SiNeseCil()
    {
        var odstavce = RichTextHtmlParser.Parse("<p><a href=\"https://example.org\">web</a></p>");

        odstavce.Should().ContainSingle()
            .Which.Tokens.Should().ContainSingle()
            .Which.LinkHref.Should().Be("https://example.org");
    }

    [Fact]
    public void Parse_PrazdnyVstup_VraciPrazdno()
    {
        RichTextHtmlParser.Parse("").Should().BeEmpty();
        RichTextHtmlParser.Parse("<p><br></p>").Should()
            .OnlyContain(p => p.Tokens.All(t => t.IsLineBreak || string.IsNullOrEmpty(t.Text)),
                "prázdný odstavec z Quillu nesmí vyrobit viditelný text");
    }

    /// <summary>
    /// Odsazení nese Quill třídou ql-indent-N; bez ní by se vnořené seznamy ve Wordu
    /// slily do jedné úrovně.
    /// </summary>
    [Fact]
    public void Parse_OdsazenyOdstavec_MaUroven()
    {
        var odstavce = RichTextHtmlParser.Parse("<p class=\"ql-indent-2\">text</p>");

        odstavce.Should().ContainSingle().Which.IndentLevel.Should().Be(2);
    }

    [Fact]
    public void Parse_Odstavec_NeniSeznam()
    {
        RichTextHtmlParser.Parse("<p>text</p>").Should().ContainSingle()
            .Which.ListKind.Should().Be(RichTextListKind.None);
    }

    /// <summary>
    /// U položky seznamu je první token vždy značka. Word výzvy ji přeskočí a kreslí seznam
    /// sám; export záznamu ji sází dál. Tahle smlouva drží oba konzumenty pohromadě.
    /// </summary>
    [Fact]
    public void Parse_Odrazky_MajiDruhBulletSpolecneIdAZnackuVPrvnimTokenu()
    {
        var odstavce = RichTextHtmlParser.Parse("<ul><li>prvni</li><li>druhy</li></ul>");

        odstavce.Should().HaveCount(2);
        odstavce.Should().OnlyContain(p => p.ListKind == RichTextListKind.Bullet);
        odstavce.Select(p => p.ListId).Distinct().Should().ContainSingle("obě položky jsou jeden seznam");
        odstavce[0].Tokens[0].Text.Should().Be("• ");
        odstavce[0].Tokens[1].Text.Should().Be("prvni");
    }

    [Fact]
    public void Parse_CislovanySeznam_MaDruhOrdered()
    {
        var odstavce = RichTextHtmlParser.Parse("<ol><li>a</li><li>b</li></ol>");

        odstavce.Should().OnlyContain(p => p.ListKind == RichTextListKind.Ordered);
        odstavce[1].Tokens[0].Text.Should().Be("2. ", "značka pro export záznamu se nemění");
    }

    [Fact]
    public void Parse_DvaSeznamyZaSebou_MajiRuzneId()
    {
        var odstavce = RichTextHtmlParser.Parse("<ol><li>a</li></ol><p>mezi</p><ol><li>b</li></ol>");

        var id = odstavce.Where(p => p.ListKind == RichTextListKind.Ordered).Select(p => p.ListId).ToArray();
        id.Should().HaveCount(2);
        id[0].Should().NotBe(id[1], "každý číslovaný seznam ve Wordu začíná od 1.");
    }

    /// <summary>
    /// Quill 2 dává odrážky i čísla do jednoho &lt;ol&gt; a liší je atributem data-list.
    /// Změna druhu uvnitř elementu je pro Word nový seznam.
    /// </summary>
    [Fact]
    public void Parse_Quill2SmisenySeznam_ZmenaDruhuZacinaNovySeznam()
    {
        var odstavce = RichTextHtmlParser.Parse(
            "<ol><li data-list=\"bullet\">a</li><li data-list=\"ordered\">b</li></ol>");

        odstavce.Select(p => p.ListKind).Should().Equal(RichTextListKind.Bullet, RichTextListKind.Ordered);
        odstavce[0].ListId.Should().NotBe(odstavce[1].ListId);
    }
}
