using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>Tvar skeletonu.</summary>
public enum PmSkeletonShape
{
    /// <summary>Obdélník (výchozí).</summary>
    Default,
    /// <summary>Kruh (pro avatary apod.).</summary>
    Circle
}

/// <summary>
/// <pre><pm-skeleton shape="Circle" size="Medium" /></pre>
///
/// Thin wrapper nad gov-skeleton. Placeholder prvek zobrazovaný během
/// načítání obsahu — animovaný prázdný obdélník nebo kruh.
///
/// Dokumentace: docs/architecture/skeletons.md
/// </summary>
[HtmlTargetElement("pm-skeleton")]
public sealed class PmSkeletonTagHelper : TagHelper
{
    public PmSkeletonShape Shape { get; set; } = PmSkeletonShape.Default;
    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-skeleton";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());
        if (Shape == PmSkeletonShape.Circle)
            output.Attributes.SetAttribute("shape", "circle");
        return Task.CompletedTask;
    }
}
