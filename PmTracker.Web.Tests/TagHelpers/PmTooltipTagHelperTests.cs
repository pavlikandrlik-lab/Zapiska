using System.Threading.Tasks;
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmTooltipTagHelperTests
{
    [Fact]
    public async Task Default_RendersGovTooltipWithContent()
    {
        var tagHelper = new PmTooltipTagHelper
        {
            Text = "Nápovědný text"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-tooltip", childContent: "Najeď myší");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("gov-tooltip", output.TagName);
        var html = output.Content.GetContent();
        Assert.Contains("Najeď myší", html);
        Assert.Contains("<gov-tooltip-content>Nápovědný text</gov-tooltip-content>", html);
    }

    [Fact]
    public async Task XssInText_IsEscaped()
    {
        var tagHelper = new PmTooltipTagHelper
        {
            Text = "<script>alert(1)</script>"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-tooltip", childContent: "T");
        await tagHelper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.DoesNotContain("<script>alert", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task TextWithDiacritics_PreservesUnicode()
    {
        var tagHelper = new PmTooltipTagHelper
        {
            Text = "Nápověda: ušetří čas"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-tooltip", childContent: "T");
        await tagHelper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("Nápověda: ušetří čas", html);
        Assert.DoesNotContain("&#", html);
    }
}
