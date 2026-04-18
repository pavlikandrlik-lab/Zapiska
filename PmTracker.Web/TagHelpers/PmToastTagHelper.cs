using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>Semantická varianta toastu (mapuje na gov color).</summary>
public enum PmToastVariant
{
    /// <summary>Informativní (primary, výchozí).</summary>
    Info,
    /// <summary>Úspěch (success).</summary>
    Success,
    /// <summary>Upozornění (warning).</summary>
    Warning,
    /// <summary>Chyba (error).</summary>
    Error
}

/// <summary>Svislá pozice toastu.</summary>
public enum PmToastGravity
{
    Top,
    Bottom
}

/// <summary>Vodorovná pozice toastu.</summary>
public enum PmToastPosition
{
    Left,
    Center,
    Right
}

/// <summary>
/// <pre><pm-toast variant="Success" gravity="Top" position="Right">Uloženo!</pm-toast></pre>
///
/// Thin wrapper nad gov-toast. Zobrazení bývá programové — aplikace
/// vytvoří element a volá .show() na něm.
///
/// Dokumentace: docs/architecture/toasts.md
/// </summary>
[HtmlTargetElement("pm-toast")]
public sealed class PmToastTagHelper : TagHelper
{
    public PmToastVariant Variant { get; set; } = PmToastVariant.Info;
    public PmToastGravity Gravity { get; set; } = PmToastGravity.Top;
    public PmToastPosition Position { get; set; } = PmToastPosition.Right;
    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-toast";
        output.TagMode = TagMode.StartTagAndEndTag;

        var color = Variant switch
        {
            PmToastVariant.Success => "success",
            PmToastVariant.Warning => "warning",
            PmToastVariant.Error => "error",
            _ => "primary"
        };
        output.Attributes.SetAttribute("color", color);
        output.Attributes.SetAttribute("type", "bold");
        output.Attributes.SetAttribute("gravity",
            Gravity == PmToastGravity.Bottom ? "bottom" : "top");
        output.Attributes.SetAttribute("position", Position switch
        {
            PmToastPosition.Left => "left",
            PmToastPosition.Center => "center",
            _ => "right"
        });
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());

        var child = await output.GetChildContentAsync();
        output.Content.SetHtmlContent(child.GetContent());
    }
}
