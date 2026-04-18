using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>Orientace skupiny radiobuttonů.</summary>
public enum PmRadioOrientation
{
    /// <summary>Pod sebou (výchozí).</summary>
    Vertical,
    /// <summary>Vedle sebe.</summary>
    Horizontal
}

/// <summary>
/// <pre>&lt;pm-radio-group legend="Priorita" orientation="Vertical"&gt;
///   &lt;pm-radio name="p" value="a" label="A" /&gt;
/// &lt;/pm-radio-group&gt;</pre>
///
/// Thin wrapper nad gov-form-radio-group. Dětští pm-radio se automaticky
/// zařadí do skupiny.
///
/// Dokumentace: docs/architecture/radios.md
/// </summary>
[HtmlTargetElement("pm-radio-group")]
public sealed class PmRadioGroupTagHelper : TagHelper
{
    private static readonly HtmlEncoder ContentEncoder =
        HtmlEncoder.Create(UnicodeRanges.All);

    public string Legend { get; set; } = "";
    public PmRadioOrientation Orientation { get; set; } = PmRadioOrientation.Vertical;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-form-radio-group";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("orientation",
            Orientation == PmRadioOrientation.Horizontal ? "horizontal" : "vertical");

        var child = await output.GetChildContentAsync();
        var childHtml = child.GetContent();

        var legendHtml = string.IsNullOrEmpty(Legend)
            ? ""
            : $"<gov-form-label slot=\"top\">{ContentEncoder.Encode(Legend)}</gov-form-label>";

        output.Content.SetHtmlContent(legendHtml + childHtml);
    }
}
