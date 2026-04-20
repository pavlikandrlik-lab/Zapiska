# Design — Širokoúhlý layout Fáze 1 + 2 (app-wide fluid container)

**Branch:** `codex/senior-refactor-fase-1`
**Datum:** 2026-04-20
**Návazné dokumenty:**
- Per-view audit: předchozí analýza v conversation (priority list views)
- Inbox úprav: `docs/superpowers/plans/2026-04-20-upravy-inbox.md` (úprava #4 "full-width app")

## Problém

Default `.app-main { max-width: 1280px }` plýtvá plochu na FHD (1920) monitoru o ~33 % a na 4K (3840) o ~67 %. Data-heavy workflow (Projekty/Detail, Jednani/Detail, grids, tables) tím ztrácejí density, nutí scroll, schovávají sloupce za ellipsis. User výslovně: "přijde mi škoda nevyužít 4k monitor celý."

Pixel-capped "wide tier" (1400/1600/1800 px) neřeší 4K — jen posouvá strop. Industry standard (GitHub, Linear, Vercel, Notion, Jira, Slack, ServiceNow) používá **fluid outer + self-constrained inner** pattern.

## Přístup

**2-tier width policy, content-driven internal widths:**

| Tier | BodyClass | Outer CSS | Pro jaké views |
|---|---|---|---|
| **Default (reading)** | (unset) | `max-width: 1280px; margin: 24px auto 48px; padding: 0 24px` | Text-heavy / form views (Profil, Dokumentace, StyleGuide) |
| **Fluid (data)** | `"dashboard-page"` | `max-width: none; padding: 0 clamp(16px, 2vw, 48px)` | Všechny data/workflow views |

**Padding `clamp(16px, 2vw, 48px)`:**
- Tablet ~768 px → 16 px (compact)
- FHD 1920 px → ~38 px
- 4K 3840 px → 48 px (cap, comfortable breathing room bez toho aby content byl zašitý u hrany)

**Žádný mid-tier (1600 px)** — zbytečně komplikuje bez výhody proti fluid.

### Inner constraints (per komponenta)

Stránky se složitým obsahem potřebují lokální width-policy, řešené **uvnitř markupu**, ne globálně:

| Komponenta | Recommended wrapper / rule |
|---|---|
| Text odstavec / intro | `.reading-block { max-width: 72ch; margin: 0 auto; }` — čitelný line-length |
| Form panel (např. Profil, modální form content) | `.form-panel { max-width: 900px; }` — pohodlná field-length |
| Card grid | `grid-template-columns: repeat(auto-fill, minmax(320px, 1fr))` — přirozené škálování |
| Tabulka | fluid 100 % — přirozený hit na víc sloupců |
| Sticky header / tabs row | fluid 100 % (přes celou stránku) |
| Master-detail (Nastaveni) | sidebar fixed (~280 px) + detail panel fluid zbytek |

**YAGNI:** neaplikujeme reading-block upfront napříč aplikací. Pouze **reactive** když konkrétní view potřebuje (např. Projekty/Detail header s title + subtitle může chtít constrain).

### CSS implementace

```css
/* Default (existing, zachovat) */
.app-main {
    max-width: 1280px;
    margin: 24px auto 48px;
    padding: 0 24px;
    width: 100%;
    flex: 1;
}

/* Fluid — nový padding (dnes padding: 0 byl naivní) */
.app-main--fluid {
    max-width: none;
    padding: 0 clamp(16px, 2vw, 48px);
    margin: 0; /* reset default margin */
}

/* Optional inner helper (YAGNI — přidat až potřeba) */
.reading-block { max-width: 72ch; margin: 0 auto; }
.form-panel { max-width: 900px; }
```

## Scope — Fáze 1

**3 views přechod na fluid:**

1. **`Views/Projekty/Detail.cshtml`** — projekt detail s 5 tabs (Záznamy, Harmonogram, Jednání, Tým, Návrhy)
   - Pouze `ViewData["BodyClass"] = "dashboard-page"`
   - Tabs row + tab-panels automaticky dostanou plnou šířku
   - Harmonogram/Gantt tab benefitne nejvíc — horizontal timeline plně roztažen
   - Per-tab internal width adjust: **YAGNI** — review po Fáze 1 a fixnout problémy case-by-case

2. **`Views/ProjectDashboard/Index.cshtml`** — per-project dashboard (zaznamy, NES, výzvy, stats panely)
   - `ViewData["BodyClass"] = "dashboard-page"`
   - Existing `.dashboard-fullwidth` class v markup — **ověřit redundanci**. Pokud duplicitní s novým `.app-main--fluid` paddingem, **odstranit** z markup (čistý opt-in přes BodyClass flag).

3. **`Views/Projekty/Index.cshtml`** — seznam projektů (card grid + filter panel + headers actions)
   - `ViewData["BodyClass"] = "dashboard-page"`
   - Card grid `.project-list-shell` ověřit že má `repeat(auto-fill, minmax(...))` → víc sloupců na větších monitorech

## Scope — Fáze 2

**3 views přechod na fluid:**

4. **`Views/Jednani/Detail.cshtml`** — detail jednání (zápis, účastníci, úkoly, attendance summary)
   - `ViewData["BodyClass"] = "dashboard-page"`
   - Sidebar-friendly layout: zápis (hlavní) + účastníci (sidebar) by mohli být side-by-side na FHD/4K — ale markup dnes stackuje. Po Fáze 2 review rozhodnout zda přeuspořádat.

5. **`Views/Search/Index.cshtml`** — global search results
   - `ViewData["BodyClass"] = "dashboard-page"`
   - Hit list víc sloupců / více metadata

6. **`Views/Dashboard/Focus.cshtml`** — full list záznamů ze "Zobrazit více"
   - `ViewData["BodyClass"] = "dashboard-page"`
   - Records list benefitne z šířky

**Ponechat narrow** (úmyslně `.dashboard-list-page-narrow` class):
- `Views/Dashboard/Meetings.cshtml` — chronological feed
- `Views/Dashboard/News.cshtml` — newsfeed reading UX

## Mimo scope (budoucí iterace)

- **Fáze 3:** Osoby/Index, Nastaveni/Index, Ciselniky/Detail, EditZaznamPage
- **Fáze 4:** Harmonogram tab special-case (pokud po Fáze 1 review zjistíme že Gantt chce breakout nebo internal structure change)
- **User-preference toggle** "Fixed vs Full width" (GitHub-style) — **YAGNI**
- **Reading-block helper** class adoption — postupně per potřeba

## Testing

**Architecture tests (nový soubor `PmTracker.Tests.Unit/Layout/LayoutWidthPolicyTests.cs`):**

1. `SiteCss_DefaultAppMain_Should1280pxMaxWidth` — regression guard
2. `SiteCss_AppMainFluid_ShouldUseClampPadding` — verify new padding
3. `SiteCss_AppMainFluid_ShouldRemoveMaxWidth` — verify no-cap
4. `Layout_BodyClassSwitch_ShouldSupport2Tiers` — `_Layout.cshtml` switch case "dashboard-page"
5. Per-view regression — each of 6 views (3 Fáze 1 + 3 Fáze 2) has `ViewData["BodyClass"] = "dashboard-page"`

**Manual verification (Citrix):**

Testovat per viewport × per page matice:
- Viewports: 1366×768 (HD), 1920×1080 (FHD), 2560×1440 (2K), 3840×2160 (4K)
- Pages: 6 views (Fáze 1 + 2) každá
- Kritéria: žádné hluché pásy > 48 px per strana, žádný horizontal scroll na <FHD, card grid reagoval na monitor (přibývají sloupce)

**Sandbox (SQL-free):** Playwright statický smoke — ověření CSS rules aplikované, `.app-main--fluid` class aktivní podle BodyClass.

## Risks

- **Harmonogram tab Gantt** — má internal horizontal scroll, fluid container by mohl odstranit potřebu scrollu, ale pokud Gantt má fixed-width rules, 4K by mohl ukázat nafouknutý layout. **Mitigation:** po Fáze 1 visual verification.
- **Existing CSS leaks** — některé stránky mají inline `max-width: 76rem` nebo `56rem` rules (viz `site.css:771, 775`). Tyto zůstávají jako inner constraints — **nedotkneme se**, protože jsou per-component (ne outer).
- **Projekt-detail header row** — `.page-header-compact` má vlastní padding/max-width chování. Ověřit že se nesmrští/nenafoukne.
- **Print CSS** — print média mají vlastní layout (pdf-export.css) — nedotkne se.

## Exit criteria

- 6 views přepnuto na fluid
- `.app-main--fluid` má `clamp(16px, 2vw, 48px)` padding
- Architecture tests pass
- Žádný horizontal scroll na FHD+ viewportech (manual smoke)
- Card grids / tabulky dynamicky využívají 4K
- Build 0 errors, tests 550 → ~555+

## Decision summary

**Rozhodnuto:** 2-tier (default 1280px + fluid), žádný mid-tier 1600px. Industry-standard pattern. YAGNI user-preference toggle. YAGNI reading-block helper.

**Důvod:** 4K reality. Pixel caps (1400/1600/1800) nefungují napříč monitor rozlišeními. Content-driven inner constraints (ne outer) je cleaner, škálovatelnější.
