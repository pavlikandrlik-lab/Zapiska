using System.Threading.Tasks;
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmTabsTagHelperTests
{
    [Fact]
    public async Task Default_RendersHorizontalDefaultTabs()
    {
        var tagHelper = new PmTabsTagHelper();
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-tabs", childContent: "<gov-tabs-item title=\"A\"></gov-tabs-item>");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("gov-tabs", output.TagName);
        Assert.Equal("horizontal", output.Attributes["orientation"]?.Value?.ToString());
        Assert.Equal("default", output.Attributes["type"]?.Value?.ToString());
    }

    [Fact]
    public async Task Vertical_SetsOrientation()
    {
        var tagHelper = new PmTabsTagHelper
        {
            Orientation = PmTabsOrientation.Vertical
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-tabs", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("vertical", output.Attributes["orientation"]?.Value?.ToString());
    }

    [Fact]
    public async Task Chip_SetsType()
    {
        var tagHelper = new PmTabsTagHelper
        {
            Type = PmTabsType.Chip
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-tabs", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("chip", output.Attributes["type"]?.Value?.ToString());
    }
}
