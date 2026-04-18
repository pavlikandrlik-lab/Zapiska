using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PmTracker.Web.TagHelpers;

namespace PmTracker.Tests.Unit.TagHelpers;

public sealed class PmIconTagHelperTests
{
    private static async Task<TagHelperOutput> RenderAsync(PmIconTagHelper helper)
    {
        var ctx = new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "test");
        var output = new TagHelperOutput(
            "pm-icon",
            new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(ctx, output);
        return output;
    }

    [Fact]
    public async Task Rendruje_GovIconSeJmenem()
    {
        var output = await RenderAsync(new PmIconTagHelper { Name = "check" });
        output.TagName.Should().Be("gov-icon");
        output.Attributes["name"].Value.Should().Be("check");
        output.Attributes["type"].Value.Should().Be("components");
    }

    [Fact]
    public async Task Slot_SePropaguje()
    {
        var output = await RenderAsync(new PmIconTagHelper { Name = "x", Slot = "icon-end" });
        output.Attributes["slot"].Value.Should().Be("icon-end");
    }

    [Fact]
    public async Task AriaHidden_JeDefault_True()
    {
        var output = await RenderAsync(new PmIconTagHelper { Name = "check" });
        output.Attributes["aria-hidden"].Value.Should().Be("true");
    }

    [Fact]
    public async Task AriaLabel_DeaktivujeAriaHidden()
    {
        var output = await RenderAsync(new PmIconTagHelper { Name = "check", AriaLabel = "Hotovo" });
        output.Attributes["aria-label"].Value.Should().Be("Hotovo");
        output.Attributes.ContainsName("aria-hidden").Should().BeFalse();
    }
}
