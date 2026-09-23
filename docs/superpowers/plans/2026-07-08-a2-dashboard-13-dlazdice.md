# A2 — Dashboard 13": kompaktní dlaždice — Implementation Plan

> **For agentic workers:** Exekuce INLINE. **Commity DRŽET.** Závislost: pouštět PO A3 (tokeny v :root — A2 na ně odkazuje ze subpages i Přehledu).

**Goal:** Dlaždice jednání ≤ 120 px na 1280×832 (3. řádek viditelný v panelu bez scrollu), poměr titulek/meta ≤ 1,25 u jednání i focus dlaždic.

**Architecture:** Per-dlaždicová typografie: titulky z `--d-fs-title` → `--d-fs-base`, meta → `--d-fs-label`, menší margins + vertikální padding. Globální tokeny se NEMĚNÍ (řídí i panely).

**Tech Stack:** CSS, xUnit source-assertion, Playwright měření (1280×832, 1440×900, 1920×1080).

## Global Constraints
- Žádná změna obsahu dlaždic; tokeny `--d-*` beze změny hodnot.
- Akceptace čísly, ne od oka; screenshoty pro finální ruční odsouhlasení userem.
- Commity držené.

---

### Task 1: Source-assertion test + CSS

**Files:**
- Test: `PmTracker.Tests.Unit/Layout/DashboardTileTypographyTests.cs` (create)
- Modify: `PmTracker.Web/wwwroot/css/site.css`:
  - blok `.dashboard-meeting-title, .dashboard-news-title` (~ř. 837–843)
  - blok `.dashboard-item-meta` (~ř. 820–826)
  - blok `.dashboard-focus-title` (grep `\.dashboard-focus-title` — analogicky)
  - blok `.dashboard-focus-item, .dashboard-meeting-item, .dashboard-news-item` (~ř. 734–751, padding)

- [ ] **Step 1: Failing test**

```csharp
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// A2 (2026-07-08): dlaždice dashboardu — titulek --d-fs-base (ne --d-fs-title),
/// meta --d-fs-label; těsnější svislé odsazení. Cíl: dlaždice jednání ≤120px na 13".
/// </summary>
public sealed class DashboardTileTypographyTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }
    private static string Css => File.ReadAllText(Path.Combine(RepoRoot(), "PmTracker.Web/wwwroot/css/site.css"));

    private static string Block(string selectorRegex)
    {
        var m = Regex.Match(Css, selectorRegex + @"[^{]*\{[^}]*\}", RegexOptions.Singleline);
        m.Success.Should().BeTrue(selectorRegex);
        return m.Value;
    }

    [Fact]
    public void TileTitles_UseBaseFont()
    {
        Block(@"\.dashboard-meeting-title").Should().Contain("var(--d-fs-base");
        Block(@"\.dashboard-focus-title").Should().Contain("var(--d-fs-base");
        Block(@"\.dashboard-meeting-title").Should().NotContain("--d-fs-title");
    }

    [Fact]
    public void TileMeta_UsesLabelFont()
    {
        Block(@"\.dashboard-item-meta").Should().Contain("var(--d-fs-label");
    }
}
```

- [ ] **Step 2: Run — FAIL** (`--filter "FullyQualifiedName~DashboardTileTypographyTests"`)

- [ ] **Step 3: CSS edity**

`.dashboard-meeting-title, .dashboard-news-title`:
```css
.dashboard-meeting-title,
.dashboard-news-title {
    margin: 0.15rem 0;
    /* A2 (2026-07-08): base místo title — 18px titulek na ~170px dlaždici tlačil 3. řádek
       mimo panel-min (150px na 13"); s base (~15.7px) + těsnějším spacingem se dlaždice vejde. */
    font-size: var(--d-fs-base, 0.95rem);
    font-weight: 700;
    color: var(--app-text-primary);
}
```

`.dashboard-focus-title` (v jeho stávajícím bloku vyměnit font-size):
```css
    font-size: var(--d-fs-base, 0.95rem);
```

`.dashboard-item-meta`:
```css
.dashboard-item-meta {
    display: flex;
    gap: 0.6rem;
    flex-wrap: wrap;
    margin-top: 0.2rem;
    color: var(--app-muted);
    font-size: var(--d-fs-label, 0.82rem);
}
```

Dlaždicový padding (blok `.dashboard-focus-item, .dashboard-meeting-item, .dashboard-news-item`, kde je `padding: var(--d-pad-card)` nebo ekvivalent — grep přesný řádek):
```css
    /* A2: svislý padding 70 % — šetří ~7px nahoře+dole na 13". */
    padding: calc(var(--d-pad-card, 0.8rem) * 0.7) var(--d-pad-card, 0.8rem);
```

- [ ] **Step 4: Run — PASS** + grep duplicit (`grep -n "dashboard-meeting-title" site.css` → 1 definice fontu; memory duplicate-rules).

### Task 2: Měření + iterace do akceptace

- [ ] **Step 1: Playwright skript** (scratchpad; seed 3 jednání v dev DB je z 2026-07-08): pro viewporty 1280×832 / 1440×900 / 1920×1080 změř: výšku `.dashboard-meeting-item` (první), font-size `.dashboard-meeting-title` vs `.dashboard-item-meta` (poměr), tile bottom vs panel viditelná oblast. Screenshoty všech tří.

- [ ] **Step 2: Akceptace:** 1280×832 → item ≤ **120 px**; poměr fontů ≤ **1,25**; 3. řádek uvnitř panelu (bez interního scrollu při prázdném focus panelu; s plným focus panelem tile ≤ `--d-panel-min` − padding: vizuální kontrola screenshotem).

- [ ] **Step 3: Pokud item > 120 px:** JEDNA povolená iterace — svislý padding faktor 0.7 → 0.55 a `margin-top` meta 0.2rem → 0.15rem; přeměřit. Pokud stále > 120 px → STOP, reportovat čísla userovi (další zásah = změna --d-panel-min, vyžaduje souhlas).

- [ ] **Step 4: Screenshoty 1280/1920 do reportu** pro ruční odsouhlasení userem (kritérium „na velkém monitoru ne přehnaně malé").
