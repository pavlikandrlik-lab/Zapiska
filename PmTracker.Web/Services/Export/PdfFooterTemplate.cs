namespace PmTracker.Web.Services.Export;

/// <summary>
/// Patička tiskového PDF. Třídy <c>pageNumber</c> a <c>totalPages</c> plní sazeč
/// prohlížeče při lámání stránek. Spec 2026-09-04-serverove-pdf-tisk-design.md, §6.3.
/// </summary>
public static class PdfFooterTemplate
{
    public const string Html =
        "<div style=\"width:100%;font-family:Arial,sans-serif;font-size:9pt;color:#111;text-align:center;\">" +
        "Strana <span class=\"pageNumber\"></span> z <span class=\"totalPages\"></span>" +
        "</div>";

    /// <summary>Prázdná hlavička — bez ní vysází prohlížeč svou výchozí (název + URL).</summary>
    public const string EmptyHeaderHtml = "<div></div>";
}
