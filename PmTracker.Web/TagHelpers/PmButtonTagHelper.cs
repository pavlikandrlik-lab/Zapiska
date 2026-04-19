using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-button variant="Primary" size="Medium" icon="save">Uložit</pm-button></pre>
///
/// Renderuje gov-button s centralizovaným mapováním semantických variant
/// (Primary/Secondary/Destructive/Ghost) na gov atributy color/type.
///
/// Dokumentace: docs/architecture/buttons.md
/// </summary>
[HtmlTargetElement("pm-button")]
public sealed class PmButtonTagHelper : TagHelper
{
    /// <summary>Semantická varianta tlačítka.</summary>
    public PmButtonVariant Variant { get; set; } = PmButtonVariant.Secondary;

    /// <summary>Velikost: Small/Medium/Large.</summary>
    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    /// <summary>Ikona ze sady gov-icon (např. "save", "arrow-right").</summary>
    public string? Icon { get; set; }

    /// <summary>Pozice ikony: "start" (default) nebo "end".</summary>
    public string IconPosition { get; set; } = "start";

    /// <summary>Nativní typ HTML tlačítka: button (default), submit, reset.</summary>
    [HtmlAttributeName("native-type")]
    public string NativeType { get; set; } = "button";

    /// <summary>Zakázané tlačítko.</summary>
    public bool Disabled { get; set; }

    /// <summary>Pokud je nastavené, renderuje se jako odkaz (gov-button href=...).</summary>
    public string? Href { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-button";
        output.TagMode = TagMode.StartTagAndEndTag;

        var (color, type) = Variant switch
        {
            PmButtonVariant.Primary => ("primary", "solid"),
            PmButtonVariant.Secondary => ("primary", "outlined"),
            PmButtonVariant.Destructive => ("error", "solid"),
            // Ghost = outlined neutral (viditelný 1px šedý border, transparentní bg).
            // Po user testing 2026-04-19: původní "base" neutral neměla žádné ohraničení
            // a uživatel nepoznal že je to kliknutelné tlačítko. Viz docs/architecture/buttons.md.
            PmButtonVariant.Ghost => ("neutral", "outlined"),
            _ => ("primary", "outlined")
        };

        output.Attributes.SetAttribute("color", color);
        output.Attributes.SetAttribute("type", type);
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());

        if (Disabled)
            output.Attributes.SetAttribute("disabled", "disabled");

        if (!string.IsNullOrWhiteSpace(Href))
            output.Attributes.SetAttribute("href", Href);

        output.Attributes.SetAttribute("native-type", NativeType);

        if (!string.IsNullOrWhiteSpace(Icon))
        {
            // Icon + slotName pocházejí z user-supplied properties → encodujeme,
            // abychom zabránili XSS přes uzavření atributu (name="…" → event handler).
            var slotName = IconPosition == "end" ? "icon-end" : "icon-start";
            var safeSlot = WebUtility.HtmlEncode(slotName);
            var safeIcon = WebUtility.HtmlEncode(Icon);
            var iconHtml = $"<gov-icon slot=\"{safeSlot}\" name=\"{safeIcon}\" type=\"components\"></gov-icon>";
            if (IconPosition == "end")
                output.PostContent.AppendHtml(iconHtml);
            else
                output.PreContent.AppendHtml(iconHtml);
        }

        // Zajistit, že child content se propaguje (default content)
        if (!output.Content.IsModified)
        {
            var child = await output.GetChildContentAsync();
            output.Content.SetHtmlContent(child);
        }
    }
}
