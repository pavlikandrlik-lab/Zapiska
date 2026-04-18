using System.Threading.Tasks;
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmRadioGroupTagHelperTests
{
    [Fact]
    public async Task Default_RendersVerticalGroup()
    {
        var tagHelper = new PmRadioGroupTagHelper
        {
            Legend = "Priorita"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-radio-group",
            childContent: "<gov-form-radio name=\"p\" value=\"a\">A</gov-form-radio>");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("gov-form-radio-group", output.TagName);
        Assert.Equal("vertical", output.Attributes["orientation"]?.Value?.ToString());
    }

    [Fact]
    public async Task Horizontal_SetsOrientation()
    {
        var tagHelper = new PmRadioGroupTagHelper
        {
            Legend = "P",
            Orientation = PmRadioOrientation.Horizontal
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-radio-group", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("horizontal", output.Attributes["orientation"]?.Value?.ToString());
    }

    [Fact]
    public async Task XssInLegend_IsEscaped()
    {
        var tagHelper = new PmRadioGroupTagHelper
        {
            Legend = "<script>alert('xss')</script>"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-radio-group", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.DoesNotContain("<script>alert", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task LegendWithDiacritics_PreservesUnicode()
    {
        var tagHelper = new PmRadioGroupTagHelper
        {
            Legend = "Priorita úkolu"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-radio-group", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("Priorita úkolu", html);
        Assert.DoesNotContain("&#", html);
    }
}
