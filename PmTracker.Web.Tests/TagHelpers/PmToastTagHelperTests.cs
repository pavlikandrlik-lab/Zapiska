using System.Threading.Tasks;
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmToastTagHelperTests
{
    [Fact]
    public async Task Default_RendersInfoTopRight()
    {
        var tagHelper = new PmToastTagHelper();
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-toast", childContent: "Zpráva");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("gov-toast", output.TagName);
        Assert.Equal("primary", output.Attributes["color"]?.Value?.ToString());
        Assert.Equal("bold", output.Attributes["type"]?.Value?.ToString());
        Assert.Equal("top", output.Attributes["gravity"]?.Value?.ToString());
        Assert.Equal("right", output.Attributes["position"]?.Value?.ToString());
    }

    [Fact]
    public async Task SuccessVariant_SetsColor()
    {
        var tagHelper = new PmToastTagHelper
        {
            Variant = PmToastVariant.Success
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-toast", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("success", output.Attributes["color"]?.Value?.ToString());
    }

    [Fact]
    public async Task ErrorVariant_SetsColor()
    {
        var tagHelper = new PmToastTagHelper
        {
            Variant = PmToastVariant.Error
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-toast", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("error", output.Attributes["color"]?.Value?.ToString());
    }

    [Fact]
    public async Task BottomLeftPosition_SetsBothAttributes()
    {
        var tagHelper = new PmToastTagHelper
        {
            Gravity = PmToastGravity.Bottom,
            Position = PmToastPosition.Left
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-toast", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("bottom", output.Attributes["gravity"]?.Value?.ToString());
        Assert.Equal("left", output.Attributes["position"]?.Value?.ToString());
    }
}
