# Fáze 3E — CSS architectural decomposition — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Rozdělit monolitický `wwwroot/css/site.css` (6016 LOC) do feature-specific files podle CSS architecture conventions (ITCSS-inspired) — zachovat cascade order, specificity, a visual fidelity. Zero visual regression (verified Playwright screenshot diffing).

**Architecture:** ITCSS-inspired layered architecture:
```
wwwroot/css/
├── tokens.css           ← (existující, 81 LOC) — CSS custom properties (colors, spacing, radii, shadows)
├── govcz.css            ← (existující, 90 LOC) — gov-design-system overrides
├── pdf-export.css       ← (existující, 530 LOC) — print/pdf-only styles
├── site.css             ← (NEW, orchestrator) — @import only, zero rules
└── site/                ← (NEW) — feature-specific partials loaded via @import
    ├── _reset.css       ← CSS reset + base element defaults
    ├── _base.css        ← typography, link base, form element base
    ├── _layout.css      ← page layout, grid, container, header, footer, sidebar
    ├── _components.css  ← buttons, cards, tabs, breadcrumbs, badges, chips
    ├── _forms.css       ← form controls, inputs, selects, textarea, validation
    ├── _modals.css      ← gov-dialog + legacy modal styles
    ├── _tables.css      ← table layouts + data grids
    ├── _projekty.css    ← Projekty detail, tabs, cards feature-specific
    ├── _zaznamy.css     ← Záznam editor, timeline, comments feature-specific
    ├── _harmonogram.css ← Schedule + Gantt feature-specific
    ├── _navrhy.css      ← Návrhy (proposals) feature-specific
    ├── _pickers.css     ← Date/time/person picker dropdowns
    ├── _richtext.css    ← Quill editor + rich text display
    ├── _search.css      ← Global search UI + results
    ├── _dashboard.css   ← Priority dashboard + matrices
    ├── _ciselniky.css   ← Dictionary editor CRUD UI
    ├── _autentizace.css ← Login, permissions, user management
    ├── _utilities.css   ← Utility classes (text-*, bg-*, flex helpers, visibility)
    └── _print.css       ← Print media overrides pro non-PDF context
```

**Tech Stack:** CSS `@import`, Razor `<link rel="stylesheet">` reference zachován (site.css entry point), Playwright visual verification.

**Fundamental constraint:** CSS cascade order. `@import` preserves source order — if site.css imports `_components.css` before `_forms.css`, component rules cascade first. Must replicate original monolith's rule order within combined import sequence.

---

## File Structure Reorganization

### Task 1 — Analysis + mapping

- [ ] **Step 1.1:** Skenovat `site.css` pro logické bloky

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
grep -nE "^/\*[^/]|^/\* [A-Z]" wwwroot/css/site.css | head -40
grep -nE "^\.[a-z]|^#|^[a-z]" wwwroot/css/site.css | head -80
```

Identifikovat sekce podle CSS comment blocks (if any) nebo podle selector patterns. Mapovat LOC ranges.

- [ ] **Step 1.2:** Vytvořit mapping document `docs/superpowers/plans/3e-css-mapping.md`

Pro každý section (reset, base, layout, components, features, utilities):
- Line range v původním site.css
- Target file (_reset.css, _base.css, atd.)
- Sample selectors
- Cross-section dependencies (e.g., `.btn-primary` in components depends on `--color-primary` from tokens.css)

### Task 2 — Infrastructure

- [ ] **Step 2.1:** Create `wwwroot/css/site/` directory + empty partials

```bash
mkdir -p wwwroot/css/site
for f in _reset _base _layout _components _forms _modals _tables _projekty _zaznamy _harmonogram _navrhy _pickers _richtext _search _dashboard _ciselniky _autentizace _utilities _print; do
  touch "wwwroot/css/site/${f}.css"
done
```

- [ ] **Step 2.2:** Architecture tests FIRST

`PmTracker.Tests.Unit/Architecture/CssArchitectureTests.cs`:

```csharp
[Fact]
public void SiteCss_ShouldOnlyContainImportsAndComments()
{
    var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
    var nonImportLines = content.Split('\n')
        .Where(l => !string.IsNullOrWhiteSpace(l))
        .Where(l => !l.TrimStart().StartsWith("@import"))
        .Where(l => !l.TrimStart().StartsWith("/*") && !l.Contains("*/"));
    nonImportLines.Should().BeEmpty("site.css je orchestrator, ne rules container");
}

[Theory]
[InlineData("PmTracker.Web/wwwroot/css/site/_reset.css")]
// ... all new files
public void NewPartial_ShouldExistAndHaveContent(string relativePath)
{
    var full = ResolvePath(relativePath);
    File.Exists(full).Should().BeTrue();
    File.ReadAllText(full).Length.Should().BeGreaterThan(50, "each partial has meaningful content or is explicitly empty-with-comment");
}

[Fact]
public void OriginalMonolith_TotalLOC_ShouldMatchSumOfPartials()
{
    var originalLoc = File.ReadAllLines(ResolvePath("PmTracker.Web/wwwroot/css/site.css.bak")).Length;
    var partialsLoc = Directory.GetFiles(ResolvePath("PmTracker.Web/wwwroot/css/site"), "*.css").Sum(f => File.ReadAllLines(f).Length);
    // Tolerate ±200 lines for reorganization overhead (comments, whitespace)
    (partialsLoc - originalLoc).Should().BeInRange(-200, 500);
}
```

### Task 3 — Extract per-section

Incremental approach (**one section at a time** + verify Playwright screenshot between):

- [ ] **Step 3.1:** Backup `wwwroot/css/site.css` → `site.css.bak` (for comparison)

- [ ] **Step 3.2:** Extract `_reset.css` (first N lines — typically CSS reset + base html/body rules)

- [ ] **Step 3.3:** Update `site.css` to `@import url("site/_reset.css");` + rest of original

- [ ] **Step 3.4:** Playwright visual verification

Spustit headed harness: `wwwroot/pm-modal-harness.html` + capture screenshots of key pages (Projekty detail, záznam editor, dashboard). Compare s baseline (pre-3E screenshots).

- [ ] **Step 3.5 – 3.20:** Opakovat Step 3.2-3.4 pro každou další sekci

Order of extraction (cascade-safe):
1. `_reset.css` (first — base everything)
2. `_base.css` (element defaults)
3. `_layout.css` (structural)
4. `_components.css` (reusable UI)
5. `_forms.css`
6. `_modals.css`
7. `_tables.css`
8. Feature-specific: `_projekty`, `_zaznamy`, `_harmonogram`, `_navrhy`, `_pickers`, `_richtext`, `_search`, `_dashboard`, `_ciselniky`, `_autentizace`
9. `_utilities.css` (last — override-capable)
10. `_print.css` (media-scoped, stand-alone)

- [ ] **Step 3.21:** Final site.css is only `@import` statements + licence header comment

```css
/* PM Tracker site styles — orchestrator, see site/ subfolder for partials */

@import url("site/_reset.css");
@import url("site/_base.css");
@import url("site/_layout.css");
@import url("site/_components.css");
@import url("site/_forms.css");
@import url("site/_modals.css");
@import url("site/_tables.css");
@import url("site/_projekty.css");
@import url("site/_zaznamy.css");
@import url("site/_harmonogram.css");
@import url("site/_navrhy.css");
@import url("site/_pickers.css");
@import url("site/_richtext.css");
@import url("site/_search.css");
@import url("site/_dashboard.css");
@import url("site/_ciselniky.css");
@import url("site/_autentizace.css");
@import url("site/_utilities.css");
@import url("site/_print.css");
```

- [ ] **Step 3.22:** Delete `site.css.bak` (cleanup)

### Task 4 — Playwright visual regression

- [ ] **Step 4.1:** Full-page screenshot harness

Scripty pro screenshot capture napříč viewportech (desktop 1920x1080, tablet 768, mobile 375):
- `/Projekty/Index` (list)
- `/Projekty/Detail/{id}?tab=zaznamy`
- `/Projekty/Detail/{id}?tab=harmonogram`
- `/Projekty/Detail/{id}?tab=jednani`
- `/Projekty/Detail/{id}?tab=tym`
- `/Projekty/Detail/{id}?tab=navrhy`
- `/Zaznamy/Edit/{id}` modal
- Dashboard (priority matrix)
- `/Admin/Ciselniky`

- [ ] **Step 4.2:** Per-page pre/post screenshot diff

Snapshot per step v Task 3 → diff proti baseline před Task 3. Tolerate ≤5px per-pixel drift.

- [ ] **Step 4.3:** Sign-off criteria

Zero unintended visual changes. Intentional changes must be approved (v edge cases: minor rule reordering může mít small render diff).

### Task 5 — Wrap + commit

- [ ] **Step 5.1:** Build + test

```bash
dotnet build PmTracker.sln --nologo 2>&1 | tail -3
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -3
```

Expected: 523 + ~22 new CSS architecture tests ≈ 545 pass. 0 errors.

- [ ] **Step 5.2:** Commit

Per-section commit (nejen all-in-one) for bisectability pokud regression appears later:

```bash
# Per-section commit example
git add wwwroot/css/site/_reset.css wwwroot/css/site.css
git commit -m "refactor(css): extract _reset.css from site.css (Fáze 3E step 3.2)"

# ... repeat per section

# Final cleanup commit
git rm wwwroot/css/site.css.bak
git add PmTracker.Tests.Unit/Architecture/CssArchitectureTests.cs
git commit -m "chore(css): remove backup + architecture tests (Fáze 3E final)"
```

---

## Self-Review

**Spec coverage:**
- T1 Analysis ✓
- T2 Infrastructure ✓
- T3 Per-section extraction s visual verification ✓
- T4 Playwright regression testing ✓
- T5 Commit bisectable + tests ✓

**Risks:**
- **High** — CSS cascade order is fragile. Mis-ordered `@import` = subtle visual bugs (z-index, hover states, specific layouts).
- **Medium** — Playwright může nezachytit subtle font rendering differences (sub-pixel AA). Vyžaduje manual sanity check přes live app.
- **Low** — `site.css` jako orchestrator zachovává single-file reference v `_Layout.cshtml`, zero consumer changes.

**Exit criteria:**
- `site.css` je pouze `@import` statements
- 18 feature-specific partials v `wwwroot/css/site/` subfolder
- ≥22 architecture tests
- Playwright visual regression: 0 unintended diffs
- All existing unit tests pass

---

## Rozsahově rozumné? Ano / Ne

CSS rozebírání je **higher-risk** than backend refactor. Pokud během T3 vznikne ≥3 visual regressions, **PAUSE + discuss** před pokračováním.

**Alternative lightweight approach:** pokud pocit, že full ITCSS split je overkill:
1. Pouze 4-5 coarse-grained splits (`_layout`, `_components`, `_features`, `_utilities`)
2. Zachovat zbytek v `site.css`
3. Rehodnotit později

Doporučeno **starting s Alternative lightweight** a escalovat k full ITCSS pouze pokud potřebné.

---

## Next Phase 3 retrospective

Po 3E: celková retrospektiva Phase 3 refactoring (3A – 3E). Zhodnocení:
- LOC redistribution metrics
- Architecture test count delta
- Consumer churn metrics
- Time-to-completion per task
- Regression rate (ideally 0)
- Technical debt remaining
