using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-field name="email" label="E-mail" input-type="email" required /></pre>
///
/// Renderuje kompletní formulářové pole: gov-form-control → gov-form-label,
/// gov-form-input (přes slot), volitelně gov-form-message (help nebo error).
///
/// Dokumentace: docs/architecture/fields.md
/// </summary>
[HtmlTargetElement("pm-field")]
public sealed class PmFieldTagHelper : TagHelper
{
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";

    [HtmlAttributeName("input-type")]
    public string InputType { get; set; } = "text";

    public string? Value { get; set; }
    public string? Placeholder { get; set; }
    public string? Help { get; set; }
    public string? Error { get; set; }
    public bool Required { get; set; }
    public bool Disabled { get; set; }
    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-form-control";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());
        if (!string.IsNullOrEmpty(Error))
            output.Attributes.SetAttribute("invalid", "invalid");

        var id = $"pm-field-{Name}";
        var sizeAttr = Size.ToGovAttribute();
        var requiredMarker = Required ? " <span aria-hidden=\"true\">*</span>" : "";

        var labelHtml = $"<gov-form-label slot=\"top\" for=\"{WebUtility.HtmlEncode(id)}\" size=\"{sizeAttr}\">"
            + $"{WebUtility.HtmlEncode(Label)}{requiredMarker}</gov-form-label>";

        // gov-form-input wrapper atributy (pro Web Component)
        var wrapperAttrs = new StringBuilder();
        wrapperAttrs.Append($" name=\"{WebUtility.HtmlEncode(Name)}\"");
        wrapperAttrs.Append($" input-type=\"{WebUtility.HtmlEncode(InputType)}\"");
        wrapperAttrs.Append($" size=\"{sizeAttr}\"");
        if (!string.IsNullOrEmpty(Placeholder))
            wrapperAttrs.Append($" placeholder=\"{WebUtility.HtmlEncode(Placeholder)}\"");
        if (!string.IsNullOrEmpty(Value))
            wrapperAttrs.Append($" value=\"{WebUtility.HtmlEncode(Value)}\"");
        if (Required) wrapperAttrs.Append(" required");
        if (Disabled) wrapperAttrs.Append(" disabled");

        // Vnitřní light-DOM <input> (slot="element") — gov-form-input očekává
        var innerInput = new StringBuilder();
        innerInput.Append($"<input id=\"{WebUtility.HtmlEncode(id)}\"");
        innerInput.Append($" name=\"{WebUtility.HtmlEncode(Name)}\"");
        innerInput.Append($" type=\"{WebUtility.HtmlEncode(InputType)}\"");
        if (!string.IsNullOrEmpty(Placeholder))
            innerInput.Append($" placeholder=\"{WebUtility.HtmlEncode(Placeholder)}\"");
        if (!string.IsNullOrEmpty(Value))
            innerInput.Append($" value=\"{WebUtility.HtmlEncode(Value)}\"");
        if (Required) innerInput.Append(" required");
        if (Disabled) innerInput.Append(" disabled");
        innerInput.Append(" />");

        var inputHtml = $"<gov-form-input{wrapperAttrs}><span class=\"element\">{innerInput}</span></gov-form-input>";

        var messageHtml = "";
        if (!string.IsNullOrEmpty(Error))
            messageHtml = $"<gov-form-message slot=\"bottom\" variant=\"error\">{Error}</gov-form-message>";
        else if (!string.IsNullOrEmpty(Help))
            messageHtml = $"<gov-form-message slot=\"bottom\">{Help}</gov-form-message>";

        output.Content.SetHtmlContent(labelHtml + inputHtml + messageHtml);
        return Task.CompletedTask;
    }
}
