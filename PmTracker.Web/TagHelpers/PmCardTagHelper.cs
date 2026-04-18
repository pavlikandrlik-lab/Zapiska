using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-card headline="Projekt Alfa" href="/detail/1">Popis</pm-card></pre>
///
/// Thin wrapper nad gov-card. Headline se renderuje jako &lt;h3 slot="headline"&gt;,
/// dětský obsah jde do default slotu (tělo karty). Pokud je href, celá karta
/// je klikací (gov-card href).
///
/// Dokumentace: docs/architecture/cards.md
/// </summary>
[HtmlTargetElement("pm-card")]
public sealed class PmCardTagHelper : TagHelper
{
    private static readonly HtmlEncoder ContentEncoder =
        HtmlEncoder.Create(UnicodeRanges.All);

    public string Headline { get; set; } = "";

    /// <summary>Pokud je nastaveno, celá karta je klikací odkaz.</summary>
    public string? Href { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-card";
        output.TagMode = TagMode.StartTagAndEndTag;

        if (!string.IsNullOrEmpty(Href))
            output.Attributes.SetAttribute("href", Href);

        var child = await output.GetChildContentAsync();
        var bodyHtml = child.GetContent();

        var headlineHtml = string.IsNullOrEmpty(Headline)
            ? ""
            : $"<h3 slot=\"headline\">{ContentEncoder.Encode(Headline)}</h3>";

        output.Content.SetHtmlContent(headlineHtml + bodyHtml);
    }
}
