using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PmTracker.Web.TagHelpers;

namespace PmTracker.Tests.Unit.TagHelpers;

public sealed class PmBadgeTagHelperTests
{
    private static async Task<TagHelperOutput> RenderAsync(PmBadgeTagHelper helper, string text = "Nové")
    {
        var ctx = new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "test");
        var output = new TagHelperOutput(
            "pm-badge",
            new TagHelperAttributeList(),
            (_, _) =>
            {
                var c = new DefaultTagHelperContent();
                c.SetHtmlContent(text);
                return Task.FromResult<TagHelperContent>(c);
            });
        await helper.ProcessAsync(ctx, output);
        return output;
    }

    [Fact]
    public async Task Neutral_Rendruje_GovTagNeutral()
    {
        var output = await RenderAsync(new PmBadgeTagHelper { Variant = PmBadgeVariant.Neutral });
        output.TagName.Should().Be("gov-tag");
        output.Attributes["color"].Value.Should().Be("neutral");
    }

    [Fact]
    public async Task Primary_Rendruje_GovTagPrimary()
    {
        var output = await RenderAsync(new PmBadgeTagHelper { Variant = PmBadgeVariant.Primary });
        output.Attributes["color"].Value.Should().Be("primary");
    }

    [Fact]
    public async Task Success_Rendruje_GovTagSuccess()
    {
        var output = await RenderAsync(new PmBadgeTagHelper { Variant = PmBadgeVariant.Success });
        output.Attributes["color"].Value.Should().Be("success");
    }

    [Fact]
    public async Task Warning_Rendruje_GovTagWarning()
    {
        var output = await RenderAsync(new PmBadgeTagHelper { Variant = PmBadgeVariant.Warning });
        output.Attributes["color"].Value.Should().Be("warning");
    }

    [Fact]
    public async Task Error_Rendruje_GovTagError()
    {
        var output = await RenderAsync(new PmBadgeTagHelper { Variant = PmBadgeVariant.Error });
        output.Attributes["color"].Value.Should().Be("error");
    }

    [Fact]
    public async Task Size_Small_Rendruje_Size_s()
    {
        var output = await RenderAsync(new PmBadgeTagHelper { Size = PmComponentSize.Small });
        output.Attributes["size"].Value.Should().Be("s");
    }

    [Fact]
    public async Task DefaultVariant_JeNeutral()
    {
        var output = await RenderAsync(new PmBadgeTagHelper());
        output.Attributes["color"].Value.Should().Be("neutral");
    }
}
