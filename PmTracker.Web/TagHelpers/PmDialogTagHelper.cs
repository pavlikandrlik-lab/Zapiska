using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-dialog id="confirm" title="Potvrzení">&lt;p&gt;Obsah&lt;/p&gt;</pm-dialog></pre>
///
/// Thin wrapper nad gov-dialog. Title se renderuje jako &lt;h3 slot="title"&gt;,
/// dětský obsah jde do default slotu. Otevření/zavření řídí gov-dialog JS
/// (atribut open nebo metoda .show()/.close() na elementu).
///
/// Dokumentace: docs/architecture/dialogs.md
/// </summary>
[HtmlTargetElement("pm-dialog")]
public sealed class PmDialogTagHelper : TagHelper
{
    private static readonly HtmlEncoder ContentEncoder =
        HtmlEncoder.Create(UnicodeRanges.All);

    public string Id { get; set; } = "";
    public string Title { get; set; } = "";

    /// <summary>Defaultně otevřený dialog.</summary>
    public bool Open { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-dialog";
        output.TagMode = TagMode.StartTagAndEndTag;
        if (!string.IsNullOrEmpty(Id))
            output.Attributes.SetAttribute("id", Id);
        if (Open)
            output.Attributes.SetAttribute("open", "true");

        var child = await output.GetChildContentAsync();
        var bodyHtml = child.GetContent();

        var titleHtml = string.IsNullOrEmpty(Title)
            ? ""
            : $"<h3 slot=\"title\">{ContentEncoder.Encode(Title)}</h3>";

        output.Content.SetHtmlContent(titleHtml + bodyHtml);
    }
}
