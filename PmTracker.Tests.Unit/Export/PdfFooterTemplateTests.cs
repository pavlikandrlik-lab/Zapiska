using FluentAssertions;
using PmTracker.Web.Services.Export;
using Xunit;

namespace PmTracker.Tests.Unit.Export;

/// <summary>
/// Serverové PDF (2026-09-04): patička se sází v prohlížeči, čísla stránek se
/// nesmí vkládat z C# — v době skládání HTML ještě neznáme zalomení.
/// </summary>
public sealed class PdfFooterTemplateTests
{
    [Fact]
    public void Footer_UsesBrowserPageCounters()
    {
        PdfFooterTemplate.Html.Should().Contain("class=\"pageNumber\"");
        PdfFooterTemplate.Html.Should().Contain("class=\"totalPages\"");
    }

    [Fact]
    public void Footer_IsCentered()
    {
        PdfFooterTemplate.Html.Should().Contain("text-align:center",
            "zadání je číslování dole uprostřed");
    }

    [Fact]
    public void Footer_ReadsStranaXzY()
    {
        PdfFooterTemplate.Html.Should().MatchRegex(
            @"Strana\s*<span class=""pageNumber""></span>\s*z\s*<span class=""totalPages""></span>");
    }

    [Fact]
    public void Footer_HasExplicitFontSize()
    {
        // Bez vlastní velikosti sází Chromium patičku nečitelně malým písmem.
        PdfFooterTemplate.Html.Should().Contain("font-size:");
    }

    [Fact]
    public void Header_IsEmpty_SoBrowserDoesNotAddItsOwn()
    {
        PdfFooterTemplate.EmptyHeaderHtml.Should().Be("<div></div>",
            "prázdná hlavička potlačí výchozí název + URL od prohlížeče");
    }

    [Fact]
    public void RenderResult_DistinguishesSuccessFromFailure()
    {
        PdfRenderResult.Success([1, 2, 3]).Succeeded.Should().BeTrue();
        PdfRenderResult.Failure("bez prohlížeče").Succeeded.Should().BeFalse();
        PdfRenderResult.Failure("bez prohlížeče").FailureReason.Should().Be("bez prohlížeče");
    }
}
