using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-tabs-item title="Přehled" active="true">&lt;p&gt;Obsah&lt;/p&gt;</pm-tabs-item></pre>
///
/// Jednotlivá záložka uvnitř pm-tabs.
///
/// Dokumentace: docs/architecture/tabs.md
/// </summary>
[HtmlTargetElement("pm-tabs-item")]
public sealed class PmTabsItemTagHelper : TagHelper
{
    public string Title { get; set; } = "";

    /// <summary>Defaultně vybraná záložka.</summary>
    public bool Active { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-tabs-item";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("title", Title);
        if (Active)
            output.Attributes.SetAttribute("active", "active");

        var child = await output.GetChildContentAsync();
        output.Content.SetHtmlContent(child.GetContent());
    }
}
