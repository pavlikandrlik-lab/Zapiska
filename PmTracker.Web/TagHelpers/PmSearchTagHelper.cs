using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-search name="q" placeholder="Hledat…" value="@Model.Query" submit="true" /></pre>
///
/// Renderuje gov-form-search s vnořeným gov-form-input (slot="input")
/// a volitelným submit tlačítkem (slot="button").
///
/// Dokumentace: docs/architecture/searches.md
/// </summary>
[HtmlTargetElement("pm-search", TagStructure = TagStructure.NormalOrSelfClosing)]
public sealed class PmSearchTagHelper : TagHelper
{
    // HtmlEncoder.Create(UnicodeRanges.All) encodes pouze HTML nebezpečné znaky (<, >, ", &, ')
    // a zachovává diakritiku (é, í, á, ě, …). Bezpečné pro XSS ochranu i Unicode vstup.
    private static readonly HtmlEncoder AttributeEncoder = HtmlEncoder.Create(UnicodeRanges.All);

    /// <summary>Atribut name (odesílá se jako GET parametr).</summary>
    public string Name { get; set; } = "q";

    /// <summary>Atribut placeholder.</summary>
    public string? Placeholder { get; set; }

    /// <summary>Aktuální hodnota (value).</summary>
    public string? Value { get; set; }

    /// <summary>Aria-label pro input (fallback když není label).</summary>
    [HtmlAttributeName("aria-label")]
    public string? AriaLabel { get; set; }

    /// <summary>Velikost: Small/Medium/Large.</summary>
    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    /// <summary>Přidá submit tlačítko "Hledat" do slotu button.</summary>
    public bool Submit { get; set; }

    /// <summary>Pokud true, input dostane autofocus.</summary>
    public bool Autofocus { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-form-search";
        output.TagMode = TagMode.StartTagAndEndTag;

        var size = Size.ToGovAttribute();
        output.Attributes.SetAttribute("color", "primary");
        output.Attributes.SetAttribute("size", size);

        var safeName = AttributeEncoder.Encode(Name);
        var safePlaceholder = AttributeEncoder.Encode(Placeholder ?? string.Empty);
        var safeValue = AttributeEncoder.Encode(Value ?? string.Empty);
        var safeAriaLabel = AttributeEncoder.Encode(AriaLabel ?? Placeholder ?? "Hledat");
        var autofocusAttr = Autofocus ? " autofocus" : string.Empty;

        var inputHtml =
            $"<gov-form-input slot=\"input\" size=\"{size}\" name=\"{safeName}\" " +
            $"type=\"search\" placeholder=\"{safePlaceholder}\" value=\"{safeValue}\" " +
            $"aria-label=\"{safeAriaLabel}\"{autofocusAttr}></gov-form-input>";
        output.Content.AppendHtml(inputHtml);

        if (Submit)
        {
            var buttonHtml =
                $"<gov-button slot=\"button\" color=\"primary\" size=\"{size}\" type=\"solid\" native-type=\"submit\">Hledat</gov-button>";
            output.Content.AppendHtml(buttonHtml);
        }
    }
}
