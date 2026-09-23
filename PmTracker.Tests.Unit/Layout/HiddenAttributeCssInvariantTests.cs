using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// DS gov 4.7.0 přidalo globální <c>[hidden]{display:none !important}</c>
/// (assets/gov/styles/templates.css) — 4.2.9 (core.min.css) tohle pravidlo nemělo.
/// Vzor „element má atribut hidden, viditelnost pak vrací CSS pravidlo přes stavovou
/// třídu na předkovi (např. <c>.open [hidden] { display: block }</c>), atribut hidden
/// se nikdy neodstraňuje" od 4.7.0 tiše prohraje — !important vždy vyhraje.
///
/// Investigace regrese ProjectMenuOverflowScenariosTests (fix round 1, 2026-09-23) tenhle
/// vzor v aplikaci nenašla: `.tab-secondary-group` [hidden] vůbec nepoužívá, viditelnost
/// sekundárních tabů řídí čistě `display:none/block` přes `.is-expanded` na předkovi
/// (žádný `[hidden]` atribut na cestě). Skutečná regrese testu byla timing (reload pod
/// větší CSS/JS zátěží DS 4.7.0 v plné sadě), opraveno v testu samotném (WaitForLoadStateAsync).
/// Tenhle test pinuje invariantu do budoucna — kdyby někdo vzor „hidden + CSS reveal“ zavedl,
/// spadne tady místo tichého UI bugu v produkci.
/// </summary>
public sealed class HiddenAttributeCssInvariantTests
{
    [Fact]
    public void ZadnePraviloNenastavujeHiddenSelektoruJinyDisplayNezNone()
    {
        var cssDir = ResolvePath("PmTracker.Web/wwwroot/css");
        var offenders = new List<string>();

        foreach (var file in Directory.GetFiles(cssDir, "*.css"))
        {
            var css = File.ReadAllText(file);

            // Najde nejvnitřnější pravidla selektor{tělo} — funguje i uvnitř @media bloků,
            // protože regex přeskočí vnější "@media (...) {" (obsahuje vnořenou {) a chytí
            // až konkrétní list pravidlo.
            foreach (Match m in Regex.Matches(css, @"([^{}]+)\{([^{}]*)\}"))
            {
                var selector = m.Groups[1].Value;
                var body = m.Groups[2].Value;
                if (!selector.Contains("[hidden]"))
                {
                    continue;
                }

                // Jediné bezpečné použití [hidden] v selektoru: pravidlo skrytý stav jen
                // potvrzuje/zpřesňuje (display: none). Cokoliv jiného by DS gov 4.7.0
                // [hidden]{display:none !important} (assets/gov/styles/templates.css)
                // přebilo — element by navzdory tomuto pravidlu zůstal neviditelný.
                if (Regex.IsMatch(body, @"display\s*:\s*(?!none\b)\S"))
                {
                    offenders.Add($"{Path.GetFileName(file)}: {selector.Trim()} {{ {body.Trim()} }}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "aplikační CSS nesmí přes [hidden] selektor nastavovat display jiný než none — " +
            "DS gov 4.7.0 [hidden]{display:none !important} (templates.css) by to přebilo a " +
            "element by zůstal skrytý i po odemčení/rozbalení");
    }
}
