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
    private static async Task<TagHelperOutput> RenderAsync(
        PmButtonTagHelper helper,
        string innerText = "Akce",
        TagHelperAttributeList? extraAttributes = null)
    {
        var attrs = extraAttributes ?? new TagHelperAttributeList();
        var ctx = new TagHelperContext(
            attrs,
            new Dictionary<object, object>(),
            "test");

        // Razor MVC v reálném runtimu propaguje "unknown" input attributy do output.Attributes
        // PŘED voláním ProcessAsync. Reprodukujeme to v testovací helper, abychom ověřili
        // že náš ProcessAsync je nezahodí.
        var outputAttrs = new TagHelperAttributeList();
        foreach (var a in attrs)
        {
            outputAttrs.Add(a);
        }
        var output = new TagHelperOutput(
            "pm-button",
            outputAttrs,
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
    public async Task DataAtributy_NaInputu_SePropagujiNaGovButtonOutput()
    {
        // Regrese: <pm-button data-modal-url="..."> musí propagovat data-modal-url
        // na výsledný <gov-button>, jinak JS delegated handler v modules/modals.js
        // (target.closest("[data-modal-url]")) nikdy nematchuje a tlačítko "nedělá nic".
        var helper = new PmButtonTagHelper { Variant = PmButtonVariant.Primary };
        var extra = new TagHelperAttributeList
        {
            { "data-modal-url", "/Projekty/NewProjectModal" },
            { "data-record-editor-url", "/Zaznamy/Editor?id=42" }
        };

        var output = await RenderAsync(helper, "Nový projekt", extra);

        output.TagName.Should().Be("gov-button");
        output.Attributes.ContainsName("data-modal-url").Should().BeTrue("data-modal-url musi byt na vystupu, jinak JS handler nematchuje");
        output.Attributes["data-modal-url"].Value.Should().Be("/Projekty/NewProjectModal");
        output.Attributes.ContainsName("data-record-editor-url").Should().BeTrue();
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
        // Ghost = base neutral (bez ohraničení, transparentní bg) — tichá akce.
        // User rozhodnutí 2026-04-19: původně dočasně přepnuto na outlined kvůli
        // viditelnosti, ale kde je potřeba výraznost, view přešly na Secondary.
        // Ghost je rezervovaný pro decorative/tichou akci (Zpět, Zobrazit více,
        // filter chips, dashboard panel odkazy). Viz docs/architecture/buttons.md.
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
