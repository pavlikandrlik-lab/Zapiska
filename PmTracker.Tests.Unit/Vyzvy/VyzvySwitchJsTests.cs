using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

/// <summary>
/// Přepínač „Zařadit" u nové vazby (spec 2026-09-10 R0.3). Aplikace nemá JS test runner,
/// ověřuje se zdroj modulu.
/// </summary>
public sealed class VyzvySwitchJsTests
{
    private static string Js()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
        {
            dir = dir.Parent;
        }

        var root = dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
        return File.ReadAllText(Path.Combine(root, "PmTracker.Web/wwwroot/js/modules/vyzvy/switchController.js"));
    }

    [Fact]
    public void NovaVazba_PrepneJenSkrytePole_BezVolaniServeru()
    {
        var src = Js();

        var novaVetev = src.IndexOf("Po uložení půjde do bufferu", StringComparison.Ordinal);
        var volani = src.IndexOf("postForm(", StringComparison.Ordinal);

        novaVetev.Should().BeGreaterThan(-1, "nová vazba musí dostat vlastní větev");
        volani.Should().BeGreaterThan(novaVetev,
            "větev nové vazby skončí dřív, než se volá set-zaradid — vazba ještě nemá Id");
    }

    /// <summary>
    /// Nový řádek vazby vzniká klonem šablony (recordEditor/form.js) až po načtení stránky.
    /// Posluchač navázaný na jednotlivé přepínače při DOMContentLoaded ho mine a událost
    /// pm:record-editor-loaded nikdo neposílá — přepínač nové vazby by nereagoval a skryté
    /// pole by zůstalo false. Delegace z document pokryje i později přidané řádky.
    /// </summary>
    [Fact]
    public void Prepinac_PoslouchaDelegovane_PokryjeIPozdejiPridaneRadky()
    {
        var src = Js();

        src.Should().Contain("document.addEventListener('gov-change'",
            "jeden delegovaný posluchač pokryje i řádky naklonované ze šablony");
        src.Should().NotContain("pm:record-editor-loaded", "událost nikdo neposílá — mrtvý kód");
    }
}
