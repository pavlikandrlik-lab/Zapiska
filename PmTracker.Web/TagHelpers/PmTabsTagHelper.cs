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
