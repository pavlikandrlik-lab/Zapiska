using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-tooltip text="Nápověda">trigger element</pm-tooltip></pre>
///
/// Thin wrapper nad gov-tooltip. Child content je trigger (text/ikona),
/// Text property je obsah bubliny jako gov-tooltip-content.
///
/// Dokumentace: docs/architecture/tooltips.md
/// </summary>
[HtmlTargetElement("pm-tooltip")]
public sealed class PmTooltipTagHelper : TagHelper
{
    private static readonly HtmlEncoder ContentEncoder =
        HtmlEncoder.Create(UnicodeRanges.All);

    public string Text { get; set; } = "";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-tooltip";
        output.TagMode = TagMode.StartTagAndEndTag;

        var child = await output.GetChildContentAsync();
        var triggerHtml = child.GetContent();

        var contentHtml = string.IsNullOrEmpty(Text)
            ? ""
            : $"<gov-tooltip-content>{ContentEncoder.Encode(Text)}</gov-tooltip-content>";

        output.Content.SetHtmlContent(triggerHtml + contentHtml);
    }
}
