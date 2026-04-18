using System.Threading.Tasks;
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmCardTagHelperTests
{
    [Fact]
    public async Task Default_RendersGovCardWithHeadlineSlot()
    {
        var tagHelper = new PmCardTagHelper
        {
            Headline = "Projekt Alfa"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-card", childContent: "<p>Popis projektu</p>");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("gov-card", output.TagName);
        var html = output.Content.GetContent();
        Assert.Contains("<h3 slot=\"headline\">Projekt Alfa</h3>", html);
        Assert.Contains("<p>Popis projektu</p>", html);
    }

    [Fact]
    public async Task ClickableHref_SetsHrefAttribute()
    {
        var tagHelper = new PmCardTagHelper
        {
            Headline = "Detail",
            Href = "/projekty/123"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-card", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("/projekty/123", output.Attributes["href"]?.Value?.ToString());
    }

    [Fact]
    public async Task XssInHeadline_IsEscaped()
    {
        var tagHelper = new PmCardTagHelper
        {
            Headline = "<script>alert(1)</script>"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-card", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.DoesNotContain("<script>alert", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task HeadlineWithDiacritics_PreservesUnicode()
    {
        var tagHelper = new PmCardTagHelper
        {
            Headline = "Žádost o vyjádření"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-card", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("Žádost o vyjádření", html);
        Assert.DoesNotContain("&#", html);
    }
}
