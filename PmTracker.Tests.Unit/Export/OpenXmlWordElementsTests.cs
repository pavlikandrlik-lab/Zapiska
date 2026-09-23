using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using PmTracker.Web.Services.Export;
using Xunit;

namespace PmTracker.Tests.Unit.Export;

/// <summary>
/// Vlastnosti běhu v pořadí podle schématu Office (CT_RPr): rFonts, b, i, strike, color, sz, u.
/// Word špatné pořadí toleruje, validátor ne — a sdílí ho export záznamu i výzvy.
/// </summary>
public sealed class OpenXmlWordElementsTests
{
    [Fact]
    public void CreateRunProperties_PoradiPrvkuPodleSchematu()
    {
        var rPr = OpenXmlWordElements.CreateRunProperties(
            24, bold: true, italic: true, strike: true, underline: true, colorHex: "FF0000");

        rPr.ChildElements.Select(e => e.GetType()).Should().Equal(
            typeof(RunFonts), typeof(Bold), typeof(Italic), typeof(Strike),
            typeof(Color), typeof(FontSize), typeof(Underline));
    }
}
