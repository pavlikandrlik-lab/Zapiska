using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;
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

    /// <summary>
    /// Web Component varianta (light DOM lego pm-tab-left/right) je rozeznána přes
    /// persist/persist-key/sync-input atributy. TagHelper musí element ponechat
    /// nedotčený — jinak by se prepsal na gov-tabs a JS Web Component (pmTabs.js)
    /// by ho neupgradoval. Použito v _SyncPanel.cshtml a _EditZaznamForm.cshtml.
    /// </summary>
    [Theory]
    [InlineData("persist")]
    [InlineData("persist-key")]
    [InlineData("sync-input")]
    public async Task WebComponentVariant_LeavesTagUntouched(string markerAttr)
    {
        var tagHelper = new PmTabsTagHelper();
        var context = new TagHelperContext(
            tagName: "pm-tabs",
            allAttributes: new TagHelperAttributeList { new TagHelperAttribute(markerAttr, "value") },
            items: new Dictionary<object, object?>(),
            uniqueId: "test");
        var output = TagHelperTestHelpers.MakeOutput("pm-tabs", childContent: "");

        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("pm-tabs", output.TagName);
        Assert.Null(output.Attributes["orientation"]);
        Assert.Null(output.Attributes["type"]);
    }
}
