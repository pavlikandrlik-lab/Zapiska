using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PmTracker.Web.TagHelpers;

namespace PmTracker.Tests.Unit.TagHelpers;

public sealed class PmButtonTagHelperTests
{
    private static async Task<TagHelperOutput> RenderAsync(PmButtonTagHelper helper, string innerText = "Akce")
    {
        var ctx = new TagHelperContext(
            new TagHelperAttributeList(),
            new Dictionary<object, object>(),
            "test");

        var output = new TagHelperOutput(
            "pm-button",
            new TagHelperAttributeList(),
            (useCached, encoder) =>
            {
                var content = new DefaultTagHelperContent();
                content.SetHtmlContent(innerText);
                return Task.FromResult<TagHelperContent>(content);
            });

        await helper.ProcessAsync(ctx, output);
        return output;
    }

    [Fact]
    public async Task Primary_Rendruje_GovButtonSolidPrimary()
    {
        var helper = new PmButtonTagHelper { Variant = PmButtonVariant.Primary };
        var output = await RenderAsync(helper, "Uložit");
        output.TagName.Should().Be("gov-button");
        output.Attributes["color"].Value.Should().Be("primary");
        output.Attributes["type"].Value.Should().Be("solid");
        output.Attributes["size"].Value.Should().Be("m");
        (await output.GetChildContentAsync()).GetContent().Should().Contain("Uložit");
    }

    [Fact]
    public async Task Secondary_Rendruje_GovButtonOutlinedPrimary()
    {
        var helper = new PmButtonTagHelper { Variant = PmButtonVariant.Secondary };
        var output = await RenderAsync(helper);
        output.Attributes["color"].Value.Should().Be("primary");
        output.Attributes["type"].Value.Should().Be("outlined");
    }

    [Fact]
    public async Task Destructive_Rendruje_GovButtonSolidError()
    {
        var helper = new PmButtonTagHelper { Variant = PmButtonVariant.Destructive };
        var output = await RenderAsync(helper);
        output.Attributes["color"].Value.Should().Be("error");
        output.Attributes["type"].Value.Should().Be("solid");
    }

    [Fact]
    public async Task Ghost_Rendruje_GovButtonBaseNeutral()
    {
        var helper = new PmButtonTagHelper { Variant = PmButtonVariant.Ghost };
        var output = await RenderAsync(helper);
        output.Attributes["color"].Value.Should().Be("neutral");
        output.Attributes["type"].Value.Should().Be("base");
    }

    [Fact]
    public async Task Small_Size_NastaviAtributSize()
    {
        var helper = new PmButtonTagHelper { Size = PmComponentSize.Small };
        var output = await RenderAsync(helper);
        output.Attributes["size"].Value.Should().Be("s");
    }

    [Fact]
    public async Task Large_Size_NastaviAtributSize()
    {
        var helper = new PmButtonTagHelper { Size = PmComponentSize.Large };
        var output = await RenderAsync(helper);
        output.Attributes["size"].Value.Should().Be("l");
    }

    [Fact]
    public async Task Disabled_NastaviDisabledAtribut()
    {
        var helper = new PmButtonTagHelper { Disabled = true };
        var output = await RenderAsync(helper);
        output.Attributes.ContainsName("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task Icon_Vlozi_GovIconDoSlotIconStart()
    {
        var helper = new PmButtonTagHelper { Icon = "save" };
        var output = await RenderAsync(helper, "Uložit");
        output.PreContent.GetContent().Should().Contain("<gov-icon");
        output.PreContent.GetContent().Should().Contain("slot=\"icon-start\"");
        output.PreContent.GetContent().Should().Contain("name=\"save\"");
    }

    [Fact]
    public async Task IconPositionEnd_Vlozi_GovIconDoSlotIconEnd()
    {
        var helper = new PmButtonTagHelper { Icon = "arrow-right", IconPosition = "end" };
        var output = await RenderAsync(helper);
        output.PostContent.GetContent().Should().Contain("slot=\"icon-end\"");
        output.PostContent.GetContent().Should().Contain("name=\"arrow-right\"");
    }

    [Fact]
    public async Task XssInIcon_JeEscapovanoVAtributu()
    {
        // Regrese: Icon byl dřív interpolován raw do HTML stringu.
        // Útok přes uzavření atributu event handlerem musí být zablokován.
        var helper = new PmButtonTagHelper { Icon = "\" onmouseover=\"alert(1)" };
        var output = await RenderAsync(helper);

        var html = output.PreContent.GetContent();
        html.Should().NotContain("onmouseover=\"alert");
        html.Should().Contain("&quot;");
    }

    [Fact]
    public async Task Href_Pridana_RendrujeGovButtonJakoOdkaz()
    {
        var helper = new PmButtonTagHelper { Href = "/Projekty/Detail/5" };
        var output = await RenderAsync(helper);
        output.Attributes["href"].Value.Should().Be("/Projekty/Detail/5");
    }

    [Fact]
    public async Task Type_Submit_NastaviNativeButtonType()
    {
        var helper = new PmButtonTagHelper { NativeType = "submit" };
        var output = await RenderAsync(helper);
        output.Attributes["native-type"].Value.Should().Be("submit");
    }

    [Fact]
    public async Task DefaultVariant_JeSecondary()
    {
        var helper = new PmButtonTagHelper();
        var output = await RenderAsync(helper);
        output.Attributes["type"].Value.Should().Be("outlined");
    }
}
