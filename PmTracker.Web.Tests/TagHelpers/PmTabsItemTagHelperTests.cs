using System.Threading.Tasks;
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmTabsItemTagHelperTests
{
    [Fact]
    public async Task Default_RendersGovTabsItem()
    {
        var tagHelper = new PmTabsItemTagHelper
        {
            Title = "Přehled"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-tabs-item", childContent: "<p>Obsah panelu</p>");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("gov-tabs-item", output.TagName);
        Assert.Equal("Přehled", output.Attributes["title"]?.Value?.ToString());
        var html = output.Content.GetContent();
        Assert.Contains("<p>Obsah panelu</p>", html);
    }

    [Fact]
    public async Task Active_SetsActiveAttribute()
    {
        var tagHelper = new PmTabsItemTagHelper
        {
            Title = "Jednání",
            Active = true
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-tabs-item", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("active", output.Attributes["active"]?.Value?.ToString());
    }

    [Fact]
    public async Task TitleWithDiacritics_IsStoredInAttribute()
    {
        var tagHelper = new PmTabsItemTagHelper
        {
            Title = "Jednání a dokumenty"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-tabs-item", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("Jednání a dokumenty", output.Attributes["title"]?.Value?.ToString());
    }
}
