using FluentAssertions;

namespace PmTracker.Tests.Api.TestInfrastructure;

/// <summary>
/// Výřezy panelu Výzev z vyrenderovaného HTML. Panely dlaždic jdou po sobě jako sekce
/// s data-vyzvy-pane, takže se dá dostat k obsahu jedné dlaždice bez HTML parseru.
/// </summary>
internal static class VyzvyPanelHtml
{
    public static string Pane(string html, string klic)
    {
        var start = html.IndexOf($"data-vyzvy-pane=\"{klic}\"", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, $"panel {klic} musí být vyrenderovaný");

        var next = html.IndexOf("data-vyzvy-pane=\"", start + 1, StringComparison.Ordinal);
        return next < 0 ? html[start..] : html[start..next];
    }

    public static string BufferPane(string html) => Pane(html, "buffer");

    public static string VyzvaPane(string html, int vyzvaId) => Pane(html, $"vyzva-{vyzvaId}");

    /// <summary>Rail končí tam, kde začíná obsah vybrané dlaždice.</summary>
    public static string Rail(string html)
    {
        var start = html.IndexOf("vyzvy-rail-list", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, "rail musí být vyrenderovaný");

        var end = html.IndexOf("vyzvy-content", start, StringComparison.Ordinal);
        return end < 0 ? html[start..] : html[start..end];
    }
}
