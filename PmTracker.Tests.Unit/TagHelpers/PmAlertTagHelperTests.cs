using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PmTracker.Web.TagHelpers;

namespace PmTracker.Tests.Unit.TagHelpers;

public sealed class PmAlertTagHelperTests
{
    private static async Task<TagHelperOutput> RenderAsync(PmAlertTagHelper helper, string text = "Zpráva")
    {
        var ctx = new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "test");
        var output = new TagHelperOutput(
            "pm-alert",
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
    public async Task Info_Rendruje_GovMessagePrimary()
    {
        var output = await RenderAsync(new PmAlertTagHelper { Variant = PmAlertVariant.Info });
        output.TagName.Should().Be("gov-message");
        output.Attributes["color"].Value.Should().Be("primary");
    }

    [Fact]
    public async Task Success_Rendruje_GovMessageSuccess()
    {
        var output = await RenderAsync(new PmAlertTagHelper { Variant = PmAlertVariant.Success });
        output.Attributes["color"].Value.Should().Be("success");
    }

    [Fact]
    public async Task Warning_Rendruje_GovMessageWarning()
    {
        var output = await RenderAsync(new PmAlertTagHelper { Variant = PmAlertVariant.Warning });
        output.Attributes["color"].Value.Should().Be("warning");
    }

    [Fact]
    public async Task Error_Rendruje_GovMessageError()
    {
        var output = await RenderAsync(new PmAlertTagHelper { Variant = PmAlertVariant.Error });
        output.Attributes["color"].Value.Should().Be("error");
    }

    [Fact]
    public async Task DefaultVariant_JeInfo()
    {
        var output = await RenderAsync(new PmAlertTagHelper());
        output.Attributes["color"].Value.Should().Be("primary");
    }

    [Fact]
    public async Task Content_SePropaguje()
    {
        var output = await RenderAsync(new PmAlertTagHelper(), "Něco se pokazilo");
        (await output.GetChildContentAsync()).GetContent().Should().Contain("Něco se pokazilo");
    }
}
