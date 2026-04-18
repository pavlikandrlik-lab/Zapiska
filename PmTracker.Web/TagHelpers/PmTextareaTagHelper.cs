using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-textarea name="poznamka" label="Poznámka" rows="4" /></pre>
///
/// Renderuje gov-form-control → gov-form-input s vnitřním &lt;textarea slot="element"&gt;.
///
/// Dokumentace: docs/architecture/textareas.md
/// </summary>
[HtmlTargetElement("pm-textarea")]
public sealed class PmTextareaTagHelper : TagHelper
{
    // Encoding strategie:
    //   - ContentEncoder (Unicode-aware): pro text mezi tagy (Label, Help, Error, Value)
    //     zachovává diakritiku (é, í, á…), escapuje pouze <>&"' pro XSS ochranu.
    //   - WebUtility.HtmlEncode: pro atributy (name, id, placeholder) —
    //     entity pro non-ASCII v atributech jsou bezpečné a prohlížeč je korektně
    //     dekóduje při čtení hodnoty.
    // Rozdíl je záměrný: text potřebuje zachovat čitelnost, atributy potřebují strict escape.
    private static readonly HtmlEncoder ContentEncoder =
        HtmlEncoder.Create(UnicodeRanges.All);

    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Value { get; set; }
    public string? Placeholder { get; set; }
    public int Rows { get; set; } = 3;
    public bool Required { get; set; }
    public bool Disabled { get; set; }
    public string? Help { get; set; }
    public string? Error { get; set; }
    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-form-control";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());
        if (!string.IsNullOrEmpty(Error))
            output.Attributes.SetAttribute("invalid", "invalid");

        var id = $"pm-textarea-{Name}";
        var sizeAttr = Size.ToGovAttribute();
        var requiredMarker = Required ? " <span aria-hidden=\"true\">*</span>" : "";

        var labelHtml = $"<gov-form-label slot=\"top\" for=\"{WebUtility.HtmlEncode(id)}\" size=\"{sizeAttr}\">"
            + $"{ContentEncoder.Encode(Label)}{requiredMarker}</gov-form-label>";

        var wrapperAttrs = new StringBuilder();
        wrapperAttrs.Append($" name=\"{WebUtility.HtmlEncode(Name)}\"");
        wrapperAttrs.Append($" identifier=\"{WebUtility.HtmlEncode(id)}\"");
        wrapperAttrs.Append($" size=\"{sizeAttr}\"");

        var textareaAttrs = new StringBuilder();
        textareaAttrs.Append($" id=\"{WebUtility.HtmlEncode(id)}\"");
        textareaAttrs.Append($" name=\"{WebUtility.HtmlEncode(Name)}\"");
        textareaAttrs.Append($" rows=\"{Rows}\"");
        if (!string.IsNullOrEmpty(Placeholder))
            textareaAttrs.Append($" placeholder=\"{WebUtility.HtmlEncode(Placeholder)}\"");
        if (Required) textareaAttrs.Append(" required");
        if (Disabled) textareaAttrs.Append(" disabled");

        var valueContent = string.IsNullOrEmpty(Value) ? "" : ContentEncoder.Encode(Value);
        var textareaHtml = $"<textarea{textareaAttrs}>{valueContent}</textarea>";

        var inputHtml = $"<gov-form-input{wrapperAttrs} slot=\"input\">"
            + $"<span class=\"element\" slot=\"element\">{textareaHtml}</span></gov-form-input>";

        var messageHtml = "";
        if (!string.IsNullOrEmpty(Error))
            messageHtml = $"<gov-form-message slot=\"bottom\" variant=\"error\">{ContentEncoder.Encode(Error!)}</gov-form-message>";
        else if (!string.IsNullOrEmpty(Help))
            messageHtml = $"<gov-form-message slot=\"bottom\">{ContentEncoder.Encode(Help!)}</gov-form-message>";

        output.Content.SetHtmlContent(labelHtml + inputHtml + messageHtml);
        return Task.CompletedTask;
    }
}
