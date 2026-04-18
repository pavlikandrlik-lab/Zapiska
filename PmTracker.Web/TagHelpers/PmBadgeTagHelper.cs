using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-badge variant="Success" size="Small">Hotovo</pm-badge></pre>
///
/// Renderuje gov-tag.
/// Dokumentace: docs/architecture/badges.md
/// </summary>
[HtmlTargetElement("pm-badge")]
public sealed class PmBadgeTagHelper : TagHelper
{
    public PmBadgeVariant Variant { get; set; } = PmBadgeVariant.Neutral;
    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    /// <summary>Vizuální typ: Subtle (výchozí) / Bold.</summary>
    public PmBadgeType Type { get; set; } = PmBadgeType.Subtle;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-tag";
        output.TagMode = TagMode.StartTagAndEndTag;

        var color = Variant switch
        {
            PmBadgeVariant.Primary => "primary",
            PmBadgeVariant.Success => "success",
            PmBadgeVariant.Warning => "warning",
            PmBadgeVariant.Error => "error",
            _ => "neutral"
        };
        output.Attributes.SetAttribute("color", color);
        output.Attributes.SetAttribute("type", Type == PmBadgeType.Bold ? "bold" : "subtle");
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());

        if (!output.Content.IsModified)
        {
            var child = await output.GetChildContentAsync();
            output.Content.SetHtmlContent(child);
        }
    }
}
