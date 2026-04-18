using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-radio name="priorita" value="vysoka" label="Vysoká" /></pre>
///
/// Thin wrapper nad gov-form-radio. Obvykle uvnitř pm-radio-group.
///
/// Dokumentace: docs/architecture/radios.md
/// </summary>
[HtmlTargetElement("pm-radio")]
public sealed class PmRadioTagHelper : TagHelper
{
    private static readonly HtmlEncoder ContentEncoder =
        HtmlEncoder.Create(UnicodeRanges.All);

    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
    public bool Checked { get; set; }
    public bool Disabled { get; set; }
    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-form-radio";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());
        output.Attributes.SetAttribute("name", Name);
        output.Attributes.SetAttribute("value", Value);
        if (Disabled)
            output.Attributes.SetAttribute("disabled", "disabled");

        var id = $"pm-radio-{Name}-{Value}";

        var inputAttrs = new StringBuilder();
        inputAttrs.Append($" id=\"{WebUtility.HtmlEncode(id)}\"");
        inputAttrs.Append($" name=\"{WebUtility.HtmlEncode(Name)}\"");
        inputAttrs.Append($" value=\"{WebUtility.HtmlEncode(Value)}\"");
        inputAttrs.Append(" type=\"radio\"");
        if (Checked) inputAttrs.Append(" checked");
        if (Disabled) inputAttrs.Append(" disabled");

        var inputHtml = $"<input{inputAttrs} />";
        var labelHtml = $"<gov-form-label slot=\"label\" for=\"{WebUtility.HtmlEncode(id)}\">{ContentEncoder.Encode(Label)}</gov-form-label>";

        output.Content.SetHtmlContent(inputHtml + labelHtml);
        return Task.CompletedTask;
    }
}
