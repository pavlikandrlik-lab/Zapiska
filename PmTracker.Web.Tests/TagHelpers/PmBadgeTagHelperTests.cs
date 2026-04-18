using System.Threading.Tasks;
using PmTracker.Web.TagHelpers;

namespace PmTracker.Web.Tests.TagHelpers;

public sealed class PmBadgeTagHelperTests
{
    [Fact]
    public async Task DefaultType_RendersSubtle()
    {
        var helper = new PmBadgeTagHelper();
        var ctx = TagHelperTestHelpers.MakeContext("pm-badge");
        var output = TagHelperTestHelpers.MakeOutput("pm-badge", "Neutral");

        await helper.ProcessAsync(ctx, output);

        var html = TagHelperTestHelpers.Render(output);
        Assert.Contains("type=\"subtle\"", html);
        Assert.Equal("gov-tag", output.TagName);
    }

    [Fact]
    public async Task TypeBold_RendersBold()
    {
        var helper = new PmBadgeTagHelper { Type = PmBadgeType.Bold };
        var ctx = TagHelperTestHelpers.MakeContext("pm-badge");
        var output = TagHelperTestHelpers.MakeOutput("pm-badge", "Bold");

        await helper.ProcessAsync(ctx, output);

        var html = TagHelperTestHelpers.Render(output);
        Assert.Contains("type=\"bold\"", html);
        Assert.DoesNotContain("type=\"subtle\"", html);
    }

    [Fact]
    public async Task TypeSubtle_Explicit_RendersSubtle()
    {
        var helper = new PmBadgeTagHelper { Type = PmBadgeType.Subtle };
        var ctx = TagHelperTestHelpers.MakeContext("pm-badge");
        var output = TagHelperTestHelpers.MakeOutput("pm-badge", "Subtle");

        await helper.ProcessAsync(ctx, output);

        var html = TagHelperTestHelpers.Render(output);
        Assert.Contains("type=\"subtle\"", html);
        Assert.DoesNotContain("type=\"bold\"", html);
    }

    [Fact]
    public async Task VariantSuccess_RendersColorSuccess()
    {
        var helper = new PmBadgeTagHelper { Variant = PmBadgeVariant.Success };
        var ctx = TagHelperTestHelpers.MakeContext("pm-badge");
        var output = TagHelperTestHelpers.MakeOutput("pm-badge", "Success");

        await helper.ProcessAsync(ctx, output);

        var html = TagHelperTestHelpers.Render(output);
        Assert.Contains("color=\"success\"", html);
    }
}
