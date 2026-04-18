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
        string innerHtml = "Content",
        string? childContent = null)
    {
        var html = childContent ?? innerHtml;
        return new TagHelperOutput(
            tagName,
            attributes: new TagHelperAttributeList(),
            getChildContentAsync: (_, _) =>
            {
                var content = new DefaultTagHelperContent();
                content.SetHtmlContent(html);
                return Task.FromResult<TagHelperContent>(content);
            });
    }

    public static string Render(TagHelperOutput output)
    {
        using var sw = new StringWriter();
        output.WriteTo(sw, HtmlEncoder.Default);
        return sw.ToString();
    }

    /// <summary>
    /// Convenience overload: vytvoří kontext + output, zavolá ProcessAsync a vrátí HTML string.
    /// Vhodné pro tag helpery bez child content.
    /// </summary>
    public static string Render(TagHelper tagHelper, string tagName = "pm-tag")
    {
        var ctx = MakeContext(tagName);
        var output = MakeOutput(tagName, "");
        tagHelper.ProcessAsync(ctx, output).GetAwaiter().GetResult();
        return Render(output);
    }
}
