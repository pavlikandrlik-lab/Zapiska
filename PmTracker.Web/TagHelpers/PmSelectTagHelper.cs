using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-select name="stav" label="Stav" options="@options" required="true" /></pre>
///
/// Renderuje gov-form-control → gov-form-select s popiskem a options.
/// Options se předávají jako IList&lt;PmSelectOption&gt; přes property.
///
/// Dokumentace: docs/architecture/selects.md
/// </summary>
[HtmlTargetElement("pm-select")]
public sealed class PmSelectTagHelper : TagHelper
{
    // Encoding strategie:
    //   - ContentEncoder (Unicode-aware): pro text mezi tagy (Label, Text, Help, Error)
    //     zachovává diakritiku (é, í, á…), escapuje pouze <>&"' pro XSS ochranu.
    //   - WebUtility.HtmlEncode: pro atributy (name, id, value) —
    //     entity pro non-ASCII v atributech jsou bezpečné a prohlížeč je korektně
    //     dekóduje při čtení hodnoty.
    // Rozdíl je záměrný: text potřebuje zachovat čitelnost, atributy potřebují strict escape.
    private static readonly HtmlEncoder ContentEncoder =
        HtmlEncoder.Create(UnicodeRanges.All);

    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public IList<PmSelectOption> Options { get; set; } = new List<PmSelectOption>();
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

        var id = $"pm-select-{Name}";
        var sizeAttr = Size.ToGovAttribute();
        var requiredMarker = Required ? " <span aria-hidden=\"true\">*</span>" : "";

        var labelHtml = $"<gov-form-label slot=\"top\" for=\"{WebUtility.HtmlEncode(id)}\" size=\"{sizeAttr}\">"
            + $"{ContentEncoder.Encode(Label)}{requiredMarker}</gov-form-label>";

        var selectAttrs = new StringBuilder();
        selectAttrs.Append($" name=\"{WebUtility.HtmlEncode(Name)}\"");
        selectAttrs.Append($" identifier=\"{WebUtility.HtmlEncode(id)}\"");
        selectAttrs.Append($" size=\"{sizeAttr}\"");
        if (Required) selectAttrs.Append(" required");
        if (Disabled) selectAttrs.Append(" disabled");

        var optionsHtml = new StringBuilder();
        foreach (var opt in Options)
        {
            optionsHtml.Append($"<option value=\"{WebUtility.HtmlEncode(opt.Value)}\"");
            if (opt.selected) optionsHtml.Append(" selected");
            if (opt.disabled) optionsHtml.Append(" disabled");
            optionsHtml.Append($">{ContentEncoder.Encode(opt.Text)}</option>");
        }

        var selectHtml = $"<gov-form-select{selectAttrs}>{optionsHtml}</gov-form-select>";

        var messageHtml = "";
        if (!string.IsNullOrEmpty(Error))
            messageHtml = $"<gov-form-message slot=\"bottom\" variant=\"error\">{ContentEncoder.Encode(Error!)}</gov-form-message>";
        else if (!string.IsNullOrEmpty(Help))
            messageHtml = $"<gov-form-message slot=\"bottom\">{ContentEncoder.Encode(Help!)}</gov-form-message>";

        output.Content.SetHtmlContent(labelHtml + selectHtml + messageHtml);
        return Task.CompletedTask;
    }
}
