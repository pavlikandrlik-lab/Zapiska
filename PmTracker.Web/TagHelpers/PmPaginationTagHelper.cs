using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-pagination current="3" total-pages="12" url-template="?page={0}" /></pre>
///
/// Thin wrapper nad gov-pagination. UrlTemplate musí obsahovat placeholder "{0}"
/// který gov-pagination nahrazuje číslem stránky.
///
/// Dokumentace: docs/architecture/pagination.md
/// </summary>
[HtmlTargetElement("pm-pagination")]
public sealed class PmPaginationTagHelper : TagHelper
{
    /// <summary>Aktuální stránka (1-based).</summary>
    public int Current { get; set; } = 1;

    /// <summary>Celkový počet stránek.</summary>
    [HtmlAttributeName("total-pages")]
    public int TotalPages { get; set; } = 1;

    /// <summary>URL šablona s "{0}" pro číslo stránky (např. "?page={0}").</summary>
    [HtmlAttributeName("url-template")]
    public string UrlTemplate { get; set; } = "?page={0}";

    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-pagination";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("current", Current.ToString());
        output.Attributes.SetAttribute("pages", TotalPages.ToString());
        output.Attributes.SetAttribute("href-template", UrlTemplate);
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());
        return Task.CompletedTask;
    }
}
