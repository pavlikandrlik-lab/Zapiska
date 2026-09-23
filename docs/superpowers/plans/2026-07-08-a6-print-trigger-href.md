# A6 — Tisk: odstranit href z gov-button print triggerů — Implementation Plan

> **For agentic workers:** Exekuce INLINE v hlavní session (user pravidlo — žádní subagenti). Kroky mají checkboxy pro tracking. **Commity DRŽET** — user commituje po vlastním ověření.

**Goal:** Klik na „Tisk projektu" / „Tisk" (detail jednání) otevře dialog volby formátu a NEotevře předčasně nový tab; zapamatování formátu funguje beze změny.

**Architecture:** Rozbité triggery jsou `pm-button` → `<gov-button href target=_blank>`; gov-button aktivuje interní anchor vlastní logikou, vnější `preventDefault` ho nezastaví. Fix = odebrat `href/target/rel` z obou pm-buttonů; URL zůstávají v `data-print-pdf-url`/`data-print-word-url`, které `resolvePrintUrl` už čte přednostně. Nativní `<a>` triggery (_MeetingCard, _ZaznamPartial) se nemění.

**Tech Stack:** Razor, xUnit source-assertion, Playwright ověření.

## Global Constraints
- Commity držené; user testuje ručně jako poslední krok.
- Nespouštět změny v `ui/print.js` — logika je správně.
- 3 pre-existing failing Api testy (gantt-axis) ignorovat.

---

### Task 1: Regresní test + odebrání href

**Files:**
- Test: `PmTracker.Tests.Unit/Layout/PrintTriggerMarkupTests.cs` (create)
- Modify: `PmTracker.Web/Views/Projekty/Detail.cshtml` (blok „Tisk projektu" v `.tabs-actions`)
- Modify: `PmTracker.Web/Views/Jednani/Detail.cshtml` (blok „Tisk" v `.meeting-detail-actions`)

**Interfaces:**
- Consumes: delegovaný click handler `bootstrap.js:177` (`[data-print-trigger]` → `handlePrintTriggerClick`), `resolvePrintUrl` fallback pořadí (data-print-pdf-url → href).
- Produces: pm-button print triggery bez `href` (gov-button s `native-type="button"`, žádný interní anchor).

- [ ] **Step 1: Failing test**

```csharp
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// A6 (2026-07-08): pm-button print trigger NESMÍ mít href — gov-button s href aktivuje
/// interní anchor vlastní logikou a otevře tab dřív, než uživatel zvolí formát v chooseru.
/// Nativní &lt;a&gt; triggery (karta jednání, řádek záznamu) href mít SMÍ (preventDefault funguje).
/// </summary>
public sealed class PrintTriggerMarkupTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }
    private static string Read(string rel) => File.ReadAllText(Path.Combine(RepoRoot(), rel));

    /// <summary>Vrátí všechny pm-button elementy obsahující data-print-trigger.</summary>
    private static IEnumerable<string> PmButtonPrintTriggers(string source)
        => Regex.Matches(source, @"<pm-button[^>]*data-print-trigger[^>]*>", RegexOptions.Singleline)
            .Select(m => m.Value);

    [Theory]
    [InlineData("PmTracker.Web/Views/Projekty/Detail.cshtml")]
    [InlineData("PmTracker.Web/Views/Jednani/Detail.cshtml")]
    public void PmButtonPrintTriggers_HaveNoHref_ButKeepDataUrls(string rel)
    {
        var src = Read(rel);
        var triggers = PmButtonPrintTriggers(src).ToList();
        triggers.Should().NotBeEmpty("stránka má mít pm-button print trigger");
        foreach (var t in triggers)
        {
            t.Should().NotContain("href=", "gov-button s href otevře tab dřív než chooser (A6)");
            t.Should().NotContain("target=");
            t.Should().Contain("data-print-pdf-url");
            t.Should().Contain("data-print-word-url");
        }
    }

    [Theory]
    [InlineData("PmTracker.Web/Views/Shared/_MeetingCard.cshtml")]
    [InlineData("PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml")]
    public void NativeAnchorPrintTriggers_KeepHrefFallback(string rel)
    {
        var src = Read(rel);
        var anchors = Regex.Matches(src, @"<a[^>]*data-print-trigger[^>]*>", RegexOptions.Singleline)
            .Select(m => m.Value).ToList();
        anchors.Should().NotBeEmpty();
        anchors.Should().OnlyContain(a => a.Contains("href="), "nativní <a> drží href fallback (middle-click)");
    }
}
```

Pozn.: `<pm-button ... href="...">` je víceřádkový — regex má `Singleline`; atributy v testu hledej v celém matchi.

- [ ] **Step 2: Run — expect FAIL**

`dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~PrintTriggerMarkupTests" -v q --nologo`
Očekávání: `PmButtonPrintTriggers_HaveNoHref…` FAIL na `href=` (oba soubory).

- [ ] **Step 3: Upravit Projekty/Detail.cshtml**

Nahradit:
```razor
        <pm-button variant="Ghost"
                   size="Small"
                   href="@Model.ZaznamyTab.ProjectPrintUrl"
                   target="_blank"
                   rel="noopener"
                   data-print-trigger="true"
                   data-project-print-trigger="true"
                   data-print-pdf-url="@Model.ZaznamyTab.ProjectPrintUrl"
                   data-print-word-url="@Model.ZaznamyTab.ProjectWordUrl"
                   data-print-label="Tisk projektu">
            Tisk projektu
        </pm-button>
```
za:
```razor
        @* A6 (2026-07-08): BEZ href — gov-button s href otevírá interní anchor dřív, než
           uživatel zvolí formát v chooseru. URL nese data-print-pdf/word-url (resolvePrintUrl). *@
        <pm-button variant="Ghost"
                   size="Small"
                   data-print-trigger="true"
                   data-project-print-trigger="true"
                   data-print-pdf-url="@Model.ZaznamyTab.ProjectPrintUrl"
                   data-print-word-url="@Model.ZaznamyTab.ProjectWordUrl"
                   data-print-label="Tisk projektu">
            Tisk projektu
        </pm-button>
```

- [ ] **Step 4: Upravit Jednani/Detail.cshtml**

Nahradit:
```razor
        <pm-button variant="Ghost"
                   href="@meetingPrintUrl"
                   target="_blank"
                   rel="noopener"
                   data-print-trigger="true"
                   data-print-pdf-url="@meetingPrintUrl"
                   data-print-word-url="@meetingWordUrl"
                   data-print-label="Tisk jednání č. @Model.Jednani.CisloJednani">
            Tisk
        </pm-button>
```
za:
```razor
        @* A6 (2026-07-08): BEZ href — viz Projekty/Detail.cshtml, stejný fix. *@
        <pm-button variant="Ghost"
                   data-print-trigger="true"
                   data-print-pdf-url="@meetingPrintUrl"
                   data-print-word-url="@meetingWordUrl"
                   data-print-label="Tisk jednání č. @Model.Jednani.CisloJednani">
            Tisk
        </pm-button>
```

- [ ] **Step 5: Run — expect PASS** (stejný příkaz jako Step 2; 4/4 zelené)

- [ ] **Step 6: Build + restart app**

`dotnet build PmTracker.Web/PmTracker.Web.csproj -c Debug -v q --nologo` → 0 chyb; restart přes standardní dev příkaz (viz memory `feedback_esm_module_cache_busting` — cshtml vyžaduje rebuild).

### Task 2: Ověření chování v prohlížeči (invarianty ze spec)

**Files:** scratchpad skript (Playwright přes playwright-skill runner), žádné produkční změny.

- [ ] **Step 1: Skript** — scénáře, každý loguje `newTabs` (`context.on('page')`) + `chooser` (`[data-print-popover]`):
  1. čistý localStorage, klik „Tisk projektu" → **newTabs=0, chooser=true**; klik PDF v chooseru → **newTabs=1**.
  2. `localStorage.setItem('pmtracker.print.preferredFormat','pdf')`, reload, klik → **newTabs=1, chooser=false**.
  3. detail jednání: totéž jako (1).
  4. nastavit filtr (chip „Pouze aktivní úkoly" je default aktivní) + klik „Tisk projektu" bez preference → scope-chooser (`Použít aktuální filtry?`); po volbě „Tisknout celý projekt" → formát chooser → PDF → newTabs=1.
  5. referenční nativní `<a>` (karta jednání) — beze změny: newTabs=0 + chooser.

- [ ] **Step 2: Spustit, výsledky vypsat do reportu.** Očekávání dle scénářů; jakákoli odchylka = STOP a systematic-debugging (žádné přihazování fixů).

- [ ] **Step 3: Doplnit checkpoint do reportu pro uživatele** (ruční test dělá user; commit držet).
