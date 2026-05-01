using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>Orientace záložek.</summary>
public enum PmTabsOrientation
{
    /// <summary>Vodorovně (výchozí).</summary>
    Horizontal,
    /// <summary>Svisle (menu vlevo).</summary>
    Vertical
}

/// <summary>Vizuální typ záložek.</summary>
public enum PmTabsType
{
    /// <summary>Podtržené záložky (výchozí).</summary>
    Default,
    /// <summary>Chip (pilulkový) styl.</summary>
    Chip
}

/// <summary>
/// <pre>&lt;pm-tabs orientation="Horizontal" type="Default"&gt;
///   &lt;pm-tabs-item title="A" active="true"&gt;&lt;/pm-tabs-item&gt;
/// &lt;/pm-tabs&gt;</pre>
///
/// Thin wrapper nad gov-tabs. JS přepínání obsahu panelů řídí samotný
/// gov Web Component.
///
/// Pozor: existuje i druhá varianta &lt;pm-tabs&gt; — Light DOM Web Component
/// (definovaný v wwwroot/js/components/pmTabs.js) s lego architekturou
/// (pm-tab-left/pm-tab/pm-tab-right) + persist/sync-input atributy.
/// Tento TagHelper detekuje Web Component variantu přes přítomnost atributů
/// `persist`, `persist-key` nebo `sync-input` a v takovém případě ponechá
/// element nedotčený (žádný override na gov-tabs).
///
/// Dokumentace: docs/architecture/tabs.md
/// </summary>
[HtmlTargetElement("pm-tabs")]
public sealed class PmTabsTagHelper : TagHelper
{
    public PmTabsOrientation Orientation { get; set; } = PmTabsOrientation.Horizontal;
    public PmTabsType Type { get; set; } = PmTabsType.Default;
    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        // Web Component variant (light DOM, lego pm-tab-left/right) je rozeznána
        // přes persist/persist-key/sync-input atributy. V tom případě element
        // ponecháme nedotčený — JS Web Component (pmTabs.js) ho upgrade-uje.
        if (context.AllAttributes.ContainsName("persist")
            || context.AllAttributes.ContainsName("persist-key")
            || context.AllAttributes.ContainsName("sync-input"))
        {
            return;
        }

        output.TagName = "gov-tabs";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("orientation",
            Orientation == PmTabsOrientation.Vertical ? "vertical" : "horizontal");
        output.Attributes.SetAttribute("type",
            Type == PmTabsType.Chip ? "chip" : "default");
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());

        var child = await output.GetChildContentAsync();
        output.Content.SetHtmlContent(child.GetContent());
    }
}
