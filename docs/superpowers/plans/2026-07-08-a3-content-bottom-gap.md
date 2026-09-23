# A3 — Jednotné mezery (tokeny → :root + bottom gap) — Implementation Plan

> **For agentic workers:** Exekuce INLINE. **Commity DRŽET.**

**Goal:** Mezery mezi kartami na dashboard subpages fungují (token dostupný) a poslední obsah má všude 20px odstup od footeru — shodně se záznamy projektu.

**Architecture:** Root cause slepených karet = `--d-*` tokeny definované jen na `.dashboard-shell`; subpages token nevidí → `gap: var(--d-gap-item)` = nic. Fix: tokeny přesunout do `:root` (identické clamp hodnoty — viewport-based, kontext nehraje roli). Bottom gap sjednotit novým tokenem `--app-content-bottom-gap: 20px` aplikovaným na 4 místa.

**Tech Stack:** čisté CSS (site.css), xUnit source-assertion, Playwright měření.

## Global Constraints
- Referenční hodnoty (Záznamy projektu): 12px mezi kartami, 20px poslední karta→footer — NEMĚNIT.
- Přehled se musí dál vejít na 1 obrazovku 1280×832 (žádný scroll stránky).
- Duplicitní CSS pravidla ověřit grepem (memory `feedback_duplicate_css_rules_site_vs_component`).

---

### Task 1: Source-assertion test + přesun tokenů

**Files:**
- Test: `PmTracker.Tests.Unit/Layout/DashboardTokenScopeTests.cs` (create)
- Modify: `PmTracker.Web/wwwroot/css/site.css` — blok `.dashboard-shell` (~ř. 7418–7434) + nový `:root` blok těsně nad ním

- [ ] **Step 1: Failing test**

```csharp
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// A3 (2026-07-08): --d-* tokeny musí být v :root (subpages bez .dashboard-shell jinak
/// resolvnou var() na nic → slepené karty). Bottom gap sjednocen tokenem
/// --app-content-bottom-gap aplikovaným na dashboard i projektová jednání.
/// </summary>
public sealed class DashboardTokenScopeTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }
    private static string Css => File.ReadAllText(Path.Combine(RepoRoot(), "PmTracker.Web/wwwroot/css/site.css"));

    private static string BlockOf(string css, string selectorRegex)
    {
        var m = Regex.Match(css, selectorRegex + @"\s*\{[^}]*\}", RegexOptions.Singleline);
        m.Success.Should().BeTrue($"blok {selectorRegex} má existovat");
        return m.Value;
    }

    [Theory]
    [InlineData("--d-fs-label")]
    [InlineData("--d-fs-base")]
    [InlineData("--d-fs-title")]
    [InlineData("--d-gap-item")]
    [InlineData("--d-gap-panel")]
    [InlineData("--d-pad-card")]
    [InlineData("--d-pad-panel")]
    [InlineData("--d-radius")]
    [InlineData("--d-panel-min")]
    public void DashboardTokens_AreDefinedInRoot_NotInShell(string token)
    {
        var css = Css;
        // Definice tokenu (token + dvojtečka) musí být v :root bloku dashboard tokenů…
        Regex.IsMatch(css, @":root\s*\{[^}]*" + Regex.Escape(token) + @"\s*:", RegexOptions.Singleline)
            .Should().BeTrue($"{token} musí být definován v :root");
        // …a NESMÍ zůstat definice v .dashboard-shell (použití var(--d-…) tam smí).
        var shell = BlockOf(css, @"\.dashboard-shell");
        shell.Should().NotContain(token + ":", $"{token} nesmí být re-definován v .dashboard-shell (drift)");
    }

    [Fact]
    public void ContentBottomGap_TokenDefined_AndApplied()
    {
        var css = Css;
        css.Should().Contain("--app-content-bottom-gap: 20px");
        // 4 aplikace (Přehled, list-page, list-page-narrow řeší jeden selektor, jednání stack):
        css.Should().MatchRegex(@"\.app-main--fluid:has\(\.dashboard-shell\)\s*\{[^}]*padding-bottom:\s*var\(--app-content-bottom-gap\)");
        css.Should().MatchRegex(@"\.dashboard-list-page[^{]*\{[^}]*padding-bottom:\s*var\(--app-content-bottom-gap\)");
        css.Should().MatchRegex(@"\.meeting-year-stack\s*\{[^}]*margin-bottom:\s*var\(--app-content-bottom-gap\)");
    }
}
```

- [ ] **Step 2: Run — FAIL** (`--filter "FullyQualifiedName~DashboardTokenScopeTests"`)

- [ ] **Step 3: CSS — tokeny do :root.** Těsně NAD blok `.dashboard-shell` vložit:

```css
/* A3 (2026-07-08): dashboard fluid tokeny v :root — subpages (Dashboard/Focus|Meetings|News,
   .dashboard-list-page bez .dashboard-shell) jinak var(--d-…) resolvnou na NIC → gap 0,
   slepené karty (stejná třída chyb jako --pm-* 2026-06). Hodnoty beze změny. */
:root {
    --d-fs-label: clamp(0.68rem, 0.60rem + 0.25vw, 0.80rem);
    --d-fs-base:  clamp(0.80rem, 0.70rem + 0.35vw, 0.98rem);
    --d-fs-title: clamp(0.92rem, 0.78rem + 0.45vw, 1.18rem);
    --d-gap-item:  clamp(0.35rem, 0.15rem + 0.45vw, 0.85rem);
    --d-gap-panel: clamp(0.5rem,  0.30rem + 0.5vw,  0.85rem);
    --d-pad-card:  clamp(0.5rem,  0.30rem + 0.5vw,  1.05rem);
    --d-pad-panel: clamp(0.55rem, 0.30rem + 0.6vw,  1.2rem);
    --d-radius:    clamp(0.45rem, 0.30rem + 0.3vw,  0.85rem);
    --d-panel-min: clamp(140px, 18vh, 240px);
    /* Jednotný spodní odstup obsahu od footeru (reference: Záznamy projektu = 20px). */
    --app-content-bottom-gap: 20px;
}
```
Z bloku `.dashboard-shell` smazat všech 9 `--d-*` řádků (ostatní vlastnosti bloku zůstávají).

- [ ] **Step 4: CSS — aplikace bottom gapu** (3 edity):
  - `.app-main--fluid:has(.dashboard-shell)` blok: přidat `padding-bottom: var(--app-content-bottom-gap);` a změnit `height: calc(100dvh - var(--app-header-h, 110px));` → beze změny (padding jde dovnitř výšky, panely mají interní scroll).
  - Blok `.dashboard-list-page` (a `-narrow` sdílí přes nový společný řádek): přidat pravidlo
    ```css
    .dashboard-list-page,
    .dashboard-list-page-narrow {
        padding-bottom: var(--app-content-bottom-gap);
    }
    ```
    (nový blok hned za existující definice max-width, ať last-wins nic nepřebije).
  - `.meeting-year-stack` (grep přesnou pozici; pokud blok neexistuje, vytvořit): `margin-bottom: var(--app-content-bottom-gap);`

- [ ] **Step 5: Duplicity check** — `grep -n "\-\-d-gap-item:\|--app-content-bottom-gap:" site.css` → každý token právě 1 definice.

- [ ] **Step 6: Run test — PASS.**

### Task 2: Playwright měření (akceptační čísla ze spec)

- [ ] **Step 1: Skript** (scratchpad; seed data z 2026-07-08 v dev DB zůstávají): měř
  - `/Dashboard/Meetings?asUser=1`: itemGaps mezi `.dashboard-meeting-item` **> 0** (~10px na 1440) a `lastToFooter ≥ 20`;
  - `/Dashboard?asUser=1` (1280×832): `footer.top − shell.bottom == 20` ± 1 a `document.scrollingElement.scrollHeight ≤ viewport + footer` (Přehled bez scrollu nad fold — přesně: shell se vejde, scrollTop==0 a shell.bottom ≤ 832);
  - `/Projekty/Detail/1?tab=jednani`: `footer.top − meetingYearStack.bottom ≥ 20`;
  - reference `/Projekty/Detail/1?tab=zaznamy`: itemGaps 12, lastToFooter 20 (beze změny).
- [ ] **Step 2: Spustit + čísla do reportu; screenshoty subpage + Přehled.** Odchylka → systematic-debugging.
