using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-icon name="save" aria-label="Uložit" /></pre>
///
/// Renderuje gov-icon. Dle defaultu je dekorativní (aria-hidden=true);
/// pokud je nastaven aria-label, bere se jako funkční ikona.
/// Dokumentace: docs/architecture/icons.md
/// </summary>
[HtmlTargetElement("pm-icon")]
public sealed class PmIconTagHelper : TagHelper
{
    public string Name { get; set; } = "";
    public string? Slot { get; set; }

    [HtmlAttributeName("aria-label")]
    public string? AriaLabel { get; set; }

    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-icon";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("name", Name);
        output.Attributes.SetAttribute("type", "components");

        if (!string.IsNullOrEmpty(Slot))
            output.Attributes.SetAttribute("slot", Slot);

        if (!string.IsNullOrEmpty(AriaLabel))
            output.Attributes.SetAttribute("aria-label", AriaLabel);
        else
            output.Attributes.SetAttribute("aria-hidden", "true");

        return Task.CompletedTask;
    }
}
