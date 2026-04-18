using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-checkbox name="souhlas" label="Souhlasím" value="1" checked="true" /></pre>
///
/// Renderuje gov-form-checkbox s natívním input[type=checkbox] uvnitř.
///
/// Dokumentace: docs/architecture/checkboxes.md
/// </summary>
[HtmlTargetElement("pm-checkbox")]
public sealed class PmCheckboxTagHelper : TagHelper
{
    private static readonly HtmlEncoder ContentEncoder =
        HtmlEncoder.Create(UnicodeRanges.All);

    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public string Value { get; set; } = "true";
    public bool Checked { get; set; }
    public bool Disabled { get; set; }
    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-form-checkbox";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());
        output.Attributes.SetAttribute("name", Name);
        if (Disabled)
            output.Attributes.SetAttribute("disabled", "disabled");

        var id = $"pm-checkbox-{Name}";

        var inputAttrs = new StringBuilder();
        inputAttrs.Append($" id=\"{WebUtility.HtmlEncode(id)}\"");
        inputAttrs.Append($" name=\"{WebUtility.HtmlEncode(Name)}\"");
        inputAttrs.Append($" value=\"{WebUtility.HtmlEncode(Value)}\"");
        inputAttrs.Append(" type=\"checkbox\"");
        if (Checked) inputAttrs.Append(" checked");
        if (Disabled) inputAttrs.Append(" disabled");

        var inputHtml = $"<input{inputAttrs} />";
        var labelHtml = $"<gov-form-label slot=\"label\" for=\"{WebUtility.HtmlEncode(id)}\">{ContentEncoder.Encode(Label)}</gov-form-label>";

        output.Content.SetHtmlContent(inputHtml + labelHtml);
        return Task.CompletedTask;
    }
}
