using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Guard: aplikace musí fungovat offline. Gov-design-system core CSS referencoval
/// fonty přes `url("/playground/build/assets/fonts/...")` — cesty které v PM Tracker
/// neexistují → každé načtení stránky generovalo 20× HTTP 404 na fonty a
/// (v corporate prostředí bez Roboto nainstalovaným) fallback na sans-serif.
///
/// User rozhodnutí 2026-04-19: strip url() parts z @font-face, zůstává jen
/// src: local(...) — pokud má klient Roboto nainstalovaný, použije se; jinak
/// CSS stack fallback (font-family: Roboto, sans-serif) → sans-serif bez 404.
/// </summary>
public sealed class GovFontsOfflineTests
{
    private static string LocateRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře.");
        }

        return directory.FullName;
    }

    [Theory]
    [InlineData("PmTracker.Web/wwwroot/lib/gov-design-system/dist/core/core.css")]
    [InlineData("PmTracker.Web/wwwroot/lib/gov-design-system/dist/core/core.min.css")]
    public void GovCoreCss_MustNotReferenceExternalFontUrls(string relativePath)
    {
        var path = Path.Combine(LocateRepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(path).Should().BeTrue($"gov core CSS musí existovat: {path}");

        var source = File.ReadAllText(path);

        source.Should().NotContain(
            "/playground/build/assets/fonts/",
            "gov core CSS nesmí odkazovat na '/playground/build/assets/fonts/' — tyto cesty v PM Tracker " +
            "neexistují a každé načtení stránky generuje HTTP 404 na 20 font souborů. " +
            "Použijte pouze src: local(...) – když klient nemá Roboto, CSS stack fallback " +
            "(Roboto, sans-serif) zajistí zobrazení bez síťového dotazu.");

        source.Should().NotContain(
            "fonts.gstatic.com",
            "aplikace musí běžet offline, gov fonty se nesmí stahovat z Google CDN.");
    }
}
