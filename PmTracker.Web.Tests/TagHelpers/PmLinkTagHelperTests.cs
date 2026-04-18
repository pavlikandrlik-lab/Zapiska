using System.Threading.Tasks;
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmLinkTagHelperTests
{
    [Fact]
    public async Task Default_RendersGovLinkWithHref()
    {
        var tagHelper = new PmLinkTagHelper { Href = "/projekty" };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-link", childContent: "Projekty");
        await tagHelper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.Equal("gov-link", output.TagName);
        Assert.Equal("/projekty", output.Attributes["href"]?.Value?.ToString());
        Assert.Contains("Projekty", html);
    }

    [Fact]
    public async Task External_AddsTargetAndRel()
    {
        var tagHelper = new PmLinkTagHelper
        {
            Href = "https://designsystem.gov.cz",
            External = true
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-link", childContent: "Design systém");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("_blank", output.Attributes["target"]?.Value?.ToString());
        Assert.Equal("noopener noreferrer", output.Attributes["rel"]?.Value?.ToString());
    }

    [Fact]
    public async Task Icon_RendersIconSlot()
    {
        var tagHelper = new PmLinkTagHelper
        {
            Href = "/",
            Icon = "chevron-right"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-link", childContent: "Dále");
        await tagHelper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("<gov-icon slot=\"icon-end\" name=\"chevron-right\" type=\"components\"></gov-icon>", html);
    }

    [Fact]
    public async Task IconStart_RendersIconBeforeText()
    {
        var tagHelper = new PmLinkTagHelper
        {
            Href = "/",
            Icon = "chevron-left",
            IconPosition = "start"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-link", childContent: "Zpět");
        await tagHelper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("<gov-icon slot=\"icon-start\"", html);
        // Icon comes before text
        var iconIndex = html.IndexOf("<gov-icon");
        var textIndex = html.IndexOf("Zpět");
        Assert.True(iconIndex < textIndex, "Icon should come before text for icon-position=start");
    }

    [Fact]
    public async Task XssInIcon_IsEscapedInAttribute()
    {
        var tagHelper = new PmLinkTagHelper
        {
            Href = "/",
            Icon = "\" onmouseover=\"alert(1)"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-link", childContent: "Click");
        await tagHelper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        // Uvozovky v Icon NESMÍ uzavřít name="…" a otevřít onmouseover event handler
        Assert.DoesNotContain("onmouseover=\"alert", html);
        // Raw uvozovky musí být escapovány na &quot;
        Assert.Contains("&quot;", html);
    }
}
