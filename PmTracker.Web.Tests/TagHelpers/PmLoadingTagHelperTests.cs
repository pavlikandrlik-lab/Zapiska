using System.Threading.Tasks;
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmLoadingTagHelperTests
{
    [Fact]
    public async Task Default_RendersGovLoadingMedium()
    {
        var tagHelper = new PmLoadingTagHelper();
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-loading", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("gov-loading", output.TagName);
        Assert.Equal("m", output.Attributes["size"]?.Value?.ToString());
    }

    [Fact]
    public async Task Label_RendersAsTextContent()
    {
        var tagHelper = new PmLoadingTagHelper
        {
            Label = "Načítám data…"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-loading", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("Načítám data…", html);
    }

    [Fact]
    public async Task XssInLabel_IsEscaped()
    {
        var tagHelper = new PmLoadingTagHelper
        {
            Label = "<script>alert(1)</script>"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-loading", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.DoesNotContain("<script>alert", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task LabelWithDiacritics_PreservesUnicode()
    {
        var tagHelper = new PmLoadingTagHelper
        {
            Label = "Načítám záznamy…"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-loading", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("Načítám záznamy", html);
        Assert.DoesNotContain("&#", html);
    }
}
