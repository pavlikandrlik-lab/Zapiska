using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-link href="/projekty" icon="chevron-right" icon-position="end">Projekty</pm-link></pre>
///
/// Thin wrapper nad gov-link. Pro externí odkazy (external="true")
/// automaticky přidá target="_blank" a rel="noopener noreferrer".
///
/// Dokumentace: docs/architecture/links.md
/// </summary>
[HtmlTargetElement("pm-link")]
public sealed class PmLinkTagHelper : TagHelper
{
    public string Href { get; set; } = "";

    /// <summary>Ikona ze sady gov-icon (např. "chevron-right").</summary>
    public string? Icon { get; set; }

    /// <summary>Pozice ikony: "start" nebo "end" (výchozí).</summary>
    public string IconPosition { get; set; } = "end";

    /// <summary>Externí odkaz (přidá target="_blank" + rel="noopener noreferrer").</summary>
    public bool External { get; set; }

    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-link";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("href", Href);
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());

        if (External)
        {
            output.Attributes.SetAttribute("target", "_blank");
            output.Attributes.SetAttribute("rel", "noopener noreferrer");
        }

        var child = await output.GetChildContentAsync();
        var childHtml = child.GetContent();

        if (!string.IsNullOrWhiteSpace(Icon))
        {
            var slotName = IconPosition == "start" ? "icon-start" : "icon-end";
            var iconHtml = $"<gov-icon slot=\"{slotName}\" name=\"{Icon}\" type=\"components\"></gov-icon>";
            output.Content.SetHtmlContent(IconPosition == "start"
                ? iconHtml + childHtml
                : childHtml + iconHtml);
        }
        else
        {
            output.Content.SetHtmlContent(childHtml);
        }
    }
}
