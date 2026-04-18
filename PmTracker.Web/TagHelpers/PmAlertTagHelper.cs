using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-alert variant="Warning">Text zprávy</pm-alert></pre>
///
/// Renderuje gov-message. Variant mapuje na gov color atribut.
/// Dokumentace: docs/architecture/alerts.md
/// </summary>
[HtmlTargetElement("pm-alert")]
public sealed class PmAlertTagHelper : TagHelper
{
    public PmAlertVariant Variant { get; set; } = PmAlertVariant.Info;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-message";
        output.TagMode = TagMode.StartTagAndEndTag;

        var color = Variant switch
        {
            PmAlertVariant.Success => "success",
            PmAlertVariant.Warning => "warning",
            PmAlertVariant.Error => "error",
            _ => "primary"
        };
        output.Attributes.SetAttribute("color", color);

        if (!output.Content.IsModified)
        {
            var child = await output.GetChildContentAsync();
            output.Content.SetHtmlContent(child);
        }
    }
}
