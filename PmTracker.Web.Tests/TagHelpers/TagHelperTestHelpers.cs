using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.Tests.TagHelpers;

/// <summary>
/// Pomocné metody pro testování TagHelperů bez nutnosti plného web stacku.
/// </summary>
internal static class TagHelperTestHelpers
{
    public static TagHelperContext MakeContext(string tagName = "pm-tag")
    {
        return new TagHelperContext(
            tagName: tagName,
            allAttributes: new TagHelperAttributeList(),
            items: new Dictionary<object, object?>(),
            uniqueId: "test");
    }

    public static TagHelperOutput MakeOutput(
        string tagName = "pm-tag",
        string innerHtml = "Content")
    {
        return new TagHelperOutput(
            tagName,
            attributes: new TagHelperAttributeList(),
            getChildContentAsync: (_, _) =>
            {
                var content = new DefaultTagHelperContent();
                content.SetHtmlContent(innerHtml);
                return Task.FromResult<TagHelperContent>(content);
            });
    }

    public static string Render(TagHelperOutput output)
    {
        using var sw = new StringWriter();
        output.WriteTo(sw, HtmlEncoder.Default);
        return sw.ToString();
    }
}
