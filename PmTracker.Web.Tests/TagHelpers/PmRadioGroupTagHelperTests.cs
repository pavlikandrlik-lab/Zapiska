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
}
