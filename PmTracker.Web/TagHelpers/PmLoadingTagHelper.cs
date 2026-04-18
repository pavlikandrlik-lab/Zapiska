using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-loading label="Načítám…" size="Medium" /></pre>
///
/// Thin wrapper nad gov-loading. Zobrazí animovaný spinner s volitelným
/// popiskem pod ním.
///
/// Dokumentace: docs/architecture/loadings.md
/// </summary>
[HtmlTargetElement("pm-loading")]
public sealed class PmLoadingTagHelper : TagHelper
{
    private static readonly HtmlEncoder ContentEncoder =
        HtmlEncoder.Create(UnicodeRanges.All);

    public string? Label { get; set; }
    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-loading";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());

        if (!string.IsNullOrEmpty(Label))
            output.Content.SetHtmlContent(ContentEncoder.Encode(Label));

        return Task.CompletedTask;
    }
}
