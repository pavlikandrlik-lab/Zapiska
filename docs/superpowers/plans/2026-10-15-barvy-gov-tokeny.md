# Barvy podle gov tokenů (DS FIS v1.0.0) — implementační plán

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Všechny barvy aplikace (světlý i tmavý motiv) berou hodnoty ze sémantických tokenů gov DS 4.7.0, které přepíná sám gov podle `data-theme`. Aplikace přestane držet vlastní paletu a 109 ručních tmavých pravidel. Poznámku „Záznam je uzavřený…“ uvidí jen ten, kdo smí měnit stav.

**Architecture:**
- Jediný zdroj barev jsou tokeny gov v `wwwroot/assets/gov/styles/tokens.css`, tedy 1:1 kopie z `DesignSystem-FIS-v1.0.0/assets/gov`.
- Aplikační CSS odkazuje jen na ně, kromě několika aplikačních tokenů `--app-*` v `css/tokens.css`. Jde o barvy, pro které gov nemá ekvivalent: stíny, fialová kategorie Rozhodnutí a překryvy pruhů harmonogramu.
- Jen `css/tokens.css` smí obsahovat `data-theme="dark"`.
- Čtyři kontrolní unit testy hlídají, že se do CSS nevrátí natvrdo zapsaná barva, stará proměnná ani tmavé pravidlo na komponentě.
- E2E audit v prohlížeči hlídá paletu a kontrast na hlavních stránkách v obou motivech.

**Tech Stack:** CSS custom properties, gov DS 4.7.0 (Web Components + CSS), xUnit + FluentAssertions, Playwright .NET (E2E), ASP.NET Core Razor.

**Spec:** Specifikace se nesepisovala, zadání je v sekci „Zadání“ níže. Je převzaté z konverzace 2026-10-15, doslova i s rozhodnutími uživatele. Podkladem je audit 2026-10-15: memory `feedback_dark_mode_gov_tokens` a skript `scratchpad/dark-audit.cjs`, jehož vylepšená verze je v Tasku 6.

## Zadání (závazné)

Uživatel 2026-10-15:
1. „oprav deklarace na gov tokeny pokud jsou dané prvky gov a ne app“
2. „barvy dle designsystem fis v1.0.0, neměň ale barvu hlavičky, ta zůstane bílá jak je, stejně tak písmo v hlavičce“
3. „zbytek oprav, kdyby bylo něco sporného, zeptej se“
4. „záznam je uzavřený … by se to mohlo klidně schovat uživatelům kteří nemají právo“

Rozhodnutí uživatele na sporné body (2026-10-15):

| Bod | Rozhodnutí |
|---|---|
| Hlavička | **Beze změny v obou motivech.** Pozadí, písmo, logo a vyhledávací pole hlavičky se nemění. Ve světlém motivu zůstane bílá, v tmavém gov šedá. Jen spodní linka hlavičky přejde na gov token. |
| Pruhy harmonogramu | **Barvy z Excelu zůstávají** (`HarmonogramKroky.cs`) v obou motivech, text na nich zůstane tmavý. Na gov tokeny přejdou okolní prvky: dráha, osa, popisky DNES/TERMÍN, skluz a náskok. |
| Dashboard / Přehled | **Převést jen barvy.** Přechody se nahradí plochými gov plochami. Rozložení ani ECharts se nemění. |

Co znamená „DS FIS v1.0.0“:
- Kit = gov DS 4.7.0 + nadstavba `assets/ds-fis/ds-fis.css`.
- Nadstavba mění jen hlavičku (tmavě modrou) a aplikace ji záměrně nenačítá ([GovHeadLoadOrderTests](../../../PmTracker.Tests.Unit/Layout/GovHeadLoadOrderTests.cs) „nadstavba DS FIS se nepoužívá (bílá hlavička)“).
- Barvy podle DS FIS jsou tedy sémantické tokeny gov 4.7.0.
- Do `assets/gov/**` se nikdy nezasahuje (README kitu, pravidlo 1).

## Global Constraints

- **Soubory DS se needitují:** `wwwroot/assets/gov/**` ani nic v `DesignSystem-FIS-v1.0.0/`.
- **Hlavička je zmražená:** pravidla `.gov-header` (kromě `border-bottom`), `.app-logo`, `.app-title`, `.app-subtitle`, `.app-user-*` a pole vyhledávání v hlavičce (`.app-search`, ne `.app-search-dropdown`) se nemění. Výjimka `.app-logo { background: #ffffff }` je v kontrolním testu pojmenovaná.
- **Tisk je mimo rozsah:** `css/pdf-export.css` se nemění. Podbarvení tisku #EFF6FF a #FDF4E8 je dané specifikací 2026-09-05.
- **Barvy z Excelu** v `HarmonogramKroky.cs` a `utils.js` (volba tmavého nebo světlého textu na pruhu) se nemění.
- **ECharts (dashboard JS) se nemění** (memory `project_dashboard_rework_echarts`).
- **Nic, co nemusí běžet na Edge i15** (memory `feedback_i15_edge_css_compat`):
  - žádné nové `color-mix()`, `:has()` ani `@layer`;
  - stávající `color-mix()` se nahradí tokenem;
  - stačí `var()` a `color-scheme`.
- **Žádný záložní hex ve `var(--token, #hex)`:** gov tokeny jsou vždy definované a záložní hodnota by byla jen další natvrdo zapsaná barva.
- **Názvy aplikačních tokenů mají prefix `--app-`** a definují se jen v `css/tokens.css`.
- **Pracuje se ve worktree** `.claude/worktrees/barvy-gov-tokeny`, větev `feat/barvy-gov-tokeny`. Hotové a otestované se mergne `--no-ff` do lokálního `main`. Push jen na pokyn.
- **Odhady jsou v hodinách inline práce Opus.** Ruční ověření uživatelem na i15 se vykazuje zvlášť.

## Mapování (platí pro Tasky 3–5)

### Staré proměnné → gov token (podle vlastnosti)

Sloupce: `text` = `color`, `fill`, `stroke`, `caret-color`; `pozadí` = `background*`; `rámeček` = `border*`, `outline*`, `column-rule*`.

| Stará proměnná | text | pozadí | rámeček |
|---|---|---|---|
| `--gov-color-primary`, `--pm-color-primary` | `--text-primary-color` | `--background-primary` | `--border-primary` |
| `--gov-color-primary-strong`, `--pm-color-primary-strong` | `--text-primary-color` | `--button-solid-primary-hover` | `--border-primary` |
| `--gov-color-primary-bg` | `--text-primary-color` | `--background-primary-subtlest` | `--border-primary` |
| `--gov-color-surface`, `--pm-surface`, `--app-nav-active-bg` | `--text-primary` | `--background-block-primary` | `--border-subtle` |
| `--pm-surface-alt` | — | `--background-block-secondary` | — |
| `--gov-color-bg`, `--pm-color-background` | — | `--background-page` | — |
| `--app-nav-hover`, `--gov-color-neutral-bg-hover` | — | `--background-neutral-subtle` | — |
| `--gov-color-neutral-bg` | — | `--background-neutral-subtlest` | — |
| `--gov-color-border`, `--gov-color-neutral-border`, `--pm-border`, `--pm-color-border` | `--text-secondary` | `--background-neutral-subtle` | `--border-subtle` |
| `--app-text-primary`, `--gov-color-text`, `--pm-text`, `--pm-color-text` | `--text-primary` | — | `--border-neutral` |
| `--gov-color-muted`, `--app-muted`, `--gov-color-text-secondary`, `--gov-color-text-muted`, `--pm-text-muted`, `--pm-color-text-muted`, `--text-muted`, `--pm-color-neutral` | `--text-secondary` | `--background-neutral-disabled` | `--border-subtle` |
| `--gov-color-success`, `--pm-color-success` | `--text-status-success` | `--background-status-success` | `--border-success` |
| `--gov-color-warning`, `--pm-warning`, `--pm-color-warning` | `--text-status-warning` | `--background-status-warning` | `--border-warning` |
| `--gov-color-warning-strong`, `--pm-warning-strong`, `--text-warning-strong` | `--text-status-warning` | — | `--border-warning` |
| `--gov-color-warning-bg`, `--pm-warning-subtle`, `--background-warning-subtle` | — | `--background-status-warning-subtlest` | — |
| `--gov-color-accent`, `--gov-color-danger`, `--gov-color-error`, `--gov-color-error-500`, `--text-error`, `--text-error-strong` | `--text-status-error` | `--background-status-error` | `--border-error` |
| `--gov-color-error-bg`, `--gov-color-danger-bg` | — | `--background-status-error-subtlest` | — |
| `--color-focus` | `--status-focus` | `--status-focus` | `--status-focus` |
| `--gov-shadow-soft` | stín → `--app-shadow-soft` | | |
| `--record-cat-decision` | `--app-category-decision` | `--app-category-decision` | `--app-category-decision` |
| `--schedule-step-N`, `--schedule-delay` | `--app-schedule-step-N`, `--app-schedule-delay` (přejmenování, hodnota beze změny) | | |
| `--app-nav-active-border` | — | — | `--border-primary` |

Nedefinované proměnné bez řádku v tabulce rozhodne implementer podle významu a zapíše `Ruling:`. Týká se to `--pm-tab-active-bg`, `--pm-tab-inactive-bg`, `--pm-tab-page-bg`, `--schedule-track-color`, `--schedule-marker-today-color`, `--schedule-marker-deadline-color` a `--record-schedule-delay-color`. U poslední zůstává inline proměnná z `_ScheduleBlock.cshtml` a odstraní se jen záložní hodnota. Typicky platí: aktivní záložka → `--background-block-primary`, neaktivní → `--background-neutral-subtlest`, dráha → `--background-neutral-subtle`, dnes → `--background-neutral-secondary`, termín → `--background-primary`.

### Natvrdo zapsané barvy → token (podle role)

Skript v Tasku 4 obsahuje přesně tyto skupiny. Barva mimo skupiny se vypíše k ručnímu rozhodnutí.

| Role | Hodnoty | text | pozadí | rámeček |
|---|---|---|---|---|
| hlavní text | `#0f172a #1f2937 #1e293b #111111 #0b0c0c` | `--text-primary` | `--background-neutral-secondary` | `--border-neutral` |
| vedlejší text | `#475467 #475569 #64748b #6b7280 #7f9bbf` | `--text-secondary` | `--background-neutral-subtle` | `--border-subtle` |
| tlumený text | `#94a3b8 #98a2b3` | `--text-tertiary` | `--background-neutral-disabled` | `--border-subtle` |
| zakázané | `#cbd5e1` | `--text-disabled` | `--background-neutral-disabled` | `--border-subtle` |
| bílá plocha | `#fff #ffffff white` a `rgba(255,255,255,≥0.7)`, `rgba(248,250,252,…)`, `rgba(248,251,255,…)` | `--text-white-fixed` | `--background-block-primary` | `--background-block-primary` |
| světle šedá plocha | `#f8fafc #f9fafb #fafafa #f1f5f9 #f2f4f7 #ededed` a `rgba(241,245,249,…)`, `rgba(0,43,73,≤0.04)`, `rgba(15,23,42,0.08)`, `rgba(148,163,184,≤0.08)` | — | `--background-neutral-subtlest` | `--border-subtlest` |
| šedá plocha | `#e2e8f0` | `--text-white-fixed` (jen `.sdc-inspect__raw-col pre`) | `--background-neutral-subtle` | `--border-subtlest` |
| rámeček | `#d0d5dd #e5e7eb #d1d5db #d8dbdd #e7edf7 #ccd7ea` a `rgba(148,163,184,…)`, `rgba(15,23,42,0.22)`, `rgba(0,43,73,0.1)`, `rgba(0,43,73,0.22)`, `rgba(29,78,216,0.14)` | — | `--border-subtle` | `--border-subtle` |
| modrá | `#1e3a8a #1e40af #1d4ed8 #0066cc #2362a2 #005ea5 #3568be #2563eb #bfdbfe` | `--text-primary-color` | `--background-primary` | `--border-primary` |
| světle modrá plocha | `#eff6ff #dbeafe #dfeaf5 #e9f5ff #edf3fb #dce6f5` a `rgba(37,99,235,…)`, `rgba(53,104,190,≤0.14)`, `rgba(29,78,216,≤0.12)`, `rgba(240,246,255,…)` | — | `--background-primary-subtlest` | `--border-primary` |
| modrá plná | `rgba(53,104,190,0.95)`, `rgba(32,76,145,0.95)` | — | `--background-primary` | — |
| červená | `#b91c1c #dc2626 #991b1b #b42318 #c53030 #c52a3a #ef4444 #6b211b #8a1c16 #9a3412 #d83b34 #f87171 #f4b6b0 #fecaca #fca5a5` | `--text-status-error` | `--background-status-error` | `--border-error` |
| světle červená | `#fef2f2 #fee2e2 #f9e1de` a `rgba(220,38,38,0.04)`, `rgba(248,113,113,0.08)`, `rgba(239,68,68,0.08)` | — | `--background-status-error-subtlest` | `--border-error` |
| zelená | `#166534 #15803d #16a34a #059669 #0f766e #22c55e` | `--text-status-success` | `--background-status-success` | `--border-success` |
| světle zelená | `#dcfce7` a `rgba(22,163,74,0.16)` | — | `--background-status-success-subtlest` | — |
| zelená zvýrazněná | `rgba(22,163,74,0.22)` | — | `--background-status-success-subtle` | — |
| oranžová | `#92400e #b45309 #f59e0b` | `--text-status-warning` | `--background-status-warning` | `--border-warning` |
| světle oranžová | `#fef3c7` a `rgba(245,158,11,0.16)` | — | `--background-status-warning-subtlest` | — |
| oranžová zvýrazněná | `rgba(245,158,11,0.22)` | — | `--background-status-warning-subtle` | — |
| zvýraznění hledání | `#fde68a` | — | `--background-secondary-subtle` | — |
| fialová Rozhodnutí | `#7c3aed` | `--app-category-decision` | `--app-category-decision-subtle` | `--app-category-decision` |
| překryv | `rgba(15,23,42,0.55)` | — | `--background-overlay-page` | — |
| průhledná | `rgba(255,255,255,0)`, `rgba(35,98,162,0)` | `transparent` | `transparent` | `transparent` |

Výjimky podle selektoru, které skript kontroluje dřív než skupiny:
- `.schedule-*segment*`: `rgba(255,255,255,0.55)` → `--app-schedule-stripe`, `rgba(255,255,255,0.35)` → `--app-schedule-segment-inner`, `rgba(15,23,42,0.42)` → `--app-schedule-segment-edge`. Leží na pruzích s barvami z Excelu, které jsou v obou motivech stejné, proto zůstávají jako aplikační tokeny se stejnou hodnotou.
- `.timeline-axis-tick`: `rgba(100,116,139,0.3)` → `--border-subtle`. `.timeline-axis-tick.edge`: `rgba(71,85,105,0.55)` → `--border-neutral`.
- `.schedule-layered-legend-swatch.today`: `#111111` → `--background-neutral-primary`. `.deadline`: `#1e293b` → `--background-neutral-secondary`.

**`box-shadow` a `text-shadow` se nahrazují celou hodnotou:**
- stín plochy, tj. posun nebo rozmazání a šedá nebo černá `rgba`: rozmazání ≤ 12 px → `var(--app-shadow-soft)`, jinak `var(--app-shadow-raised)`;
- fokusový prstenec (modrá `rgba`, tvar `0 0 0 Npx`) → `0 0 0 2px var(--status-focus)`;
- chybový prstenec (červená `rgba`) → `0 0 0 1px var(--border-error)`;
- `inset 0 0 0 1px rgba(0,94,165,0.25)` (fokus inputu) → `inset 0 0 0 1px var(--border-primary)`;
- klíčové snímky pulzu `#2362a2` → `var(--status-focus)`.

**Přechody** (`linear-gradient`, `radial-gradient`) s barvami v `.dashboard-*` a `.access-*` → jedna plocha (`--background-block-primary`, u zvýrazněných karet `--background-primary-subtlest`). Šrafování `repeating-linear-gradient` u `.dashboard-card-placeholder` → `var(--border-subtlest)` místo `rgba(…)`. Chevron nativního `<select>` (2× `linear-gradient`) → barva `var(--icon-neutral)`.

## Review Focus

1. **Text na barevné ploše po převodu ztratí kontrast.** Bílý text (`--text-white-fixed`) musí ležet na plné ploše (`--background-primary`, `--background-status-*`), ne na `*-subtle`. E2E audit (Task 6) kontrolu kontrastu dělá. Reviewer projde všechna místa `--text-white-fixed`.
2. **Hover a aktivní stav splynou s plochou.** V tmavém motivu je `--background-block-primary` = `--background-neutral-subtlest` (38,38,38). Hover karty proto musí být `--background-neutral-subtle` (59), jinak zmizí. Reviewer zkontroluje každý `:hover` nebo `.active`, kde pozadí přešlo na `*-subtlest`.
3. **Nativní ovládací prvky v tmavém motivu** (select, datum, čas, číslo, posuvníky, Quill). Bez `color-scheme: dark` zůstanou světlé. Pokrývá to Task 2 a Task 5 a ověřuje se ručně v prohlížeči.
4. **Přepsání selektorů, které jsou dnes jen v tmavém pravidle.** Když tmavé pravidlo nastavuje vlastnost, kterou základní pravidlo nemá, smazáním se ztratí. Typicky jde o Quill toolbar, `.badge`, `.pill` a `.record-delete-summary`. Skript v Tasku 5 tyto případy vypíše.
5. **Specificita.** Tmavá pravidla `:root[data-theme="dark"] .x` dnes vítězí nad základními. Po smazání může začít platit pravidlo, které bylo dosud přebité. Ověří se to porovnáním snímků obrazovky (Task 7).

---

### Task 1: Poznámku „Záznam je uzavřený…“ uvidí jen ten, kdo smí měnit stav

Stav záznamu mění jen editor s `records.edit`. Cesta „jen harmonogram“ (`records.schedule.edit`, `SaveRecordScheduleOnlyAsync`) stav neukládá. `ZaznamCommentsPanelViewModel.CanEditRecord` už nese `records.edit` pro projekt a nastavuje ho [PrepareRecordCommentsPresentationAsync](../../../PmTracker.Web/Controllers/ZaznamyController.Partials.cs#L156) pro kartu i stránku záznamu.

**Files:**
- Modify: `PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml:169`
- Test: `PmTracker.Tests.Api/Controllers/ClosedRecordCommentsRenderTests.cs`

**Interfaces:**
- Consumes: `ZaznamCommentsPanelViewModel.IsRecordClosed`, `.CanEditRecord` (existující)
- Produces: nic

- [ ] **Step 1: Napsat padající test**

Do `ClosedRecordCommentsRenderTests` přidat `using PmTracker.Web.Models.ViewModels;` a test:

```csharp
    [Fact]
    public async Task ClosedRecord_ViewerWithoutRecordsEdit_SeesNoClosedNote()
    {
        var s = await SeedAsync("ApiCls5", 9989);
        var deputyId = await _fixture.EnsurePersonAsync("ApiCls5Deputy");
        await _fixture.EnsureSubsystemRoleAsync(s.ProjectId, s.SubsystemId, deputyId, SubsystemRoleCodes.DeputyLead);

        var html = await CommentsHtmlAsync(s.ProjectId, s.ClosedRecordId, deputyId);

        html.Should().NotContain("action=\"/Zaznamy/AddComment\"");
        html.Should().NotContain("data-record-closed-note",
            "výzva „znovu otevřete změnou stavu“ nemá smysl pro toho, kdo stav měnit nesmí");
    }
```

- [ ] **Step 2: Spustit a ověřit, že test padá**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ClosedRecordCommentsRenderTests" 2>&1 | tail -15`
Expected:
- `ClosedRecord_ViewerWithoutRecordsEdit_SeesNoClosedNote` FAIL, protože HTML obsahuje `data-record-closed-note`.
- Ostatní 4 PASS.
- Pokud test padne na 404 nebo 403, zástupce nemá přístup k projektu. Pak je chyba v seedu, ne v podmínce: dořešit seed a nedělat obejití.

- [ ] **Step 3: Implementace**

V `_ZaznamCommentsPartial.cshtml` změnit podmínku:

```cshtml
    else if (Model.IsRecordClosed && Model.CanEditRecord)
```

- [ ] **Step 4: Spustit a ověřit, že testy prochází**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ClosedRecordComments" 2>&1 | tail -5`
Expected: 5/5 PASS. `ClosedRecord_PmSeesNoFormAndNoMeetingSelect_ButClosedNote` dál prochází, protože PM má `records.edit`.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml PmTracker.Tests.Api/Controllers/ClosedRecordCommentsRenderTests.cs
git commit -m "feat(vyjadreni): poznámku o uzavřeném záznamu vidí jen ten, kdo smí měnit stav"
```

---

### Task 2: Aplikační tokeny a tmavý `color-scheme` v `css/tokens.css`

`css/tokens.css` se stane jediným souborem s aplikačními barvami a jediným místem s `data-theme`. Aliasy `--pm-color-*` z něj zmizí až v Tasku 3. Teď se jen přidávají nové tokeny.

**Files:**
- Modify: `PmTracker.Web/wwwroot/css/tokens.css`
- Test: `PmTracker.Tests.Unit/Layout/TokensCssTests.cs`

**Interfaces:**
- Produces, používají Tasky 3–5:
  - `--app-shadow-soft`, `--app-shadow-raised`
  - `--app-category-decision`, `--app-category-decision-subtle`
  - `--app-schedule-step-1` … `--app-schedule-step-11`, `--app-schedule-delay`
  - `--app-schedule-stripe`, `--app-schedule-segment-inner`, `--app-schedule-segment-edge`
  - `:root[data-theme="dark"] { color-scheme: dark; }`

- [ ] **Step 1: Napsat padající test**

Do `TokensCssTests` přidat:

```csharp
    [Theory]
    [InlineData("--app-shadow-soft")]
    [InlineData("--app-shadow-raised")]
    [InlineData("--app-category-decision")]
    [InlineData("--app-category-decision-subtle")]
    [InlineData("--app-schedule-step-1")]
    [InlineData("--app-schedule-step-11")]
    [InlineData("--app-schedule-delay")]
    [InlineData("--app-schedule-stripe")]
    [InlineData("--app-schedule-segment-inner")]
    [InlineData("--app-schedule-segment-edge")]
    public void TokensCss_DefinesAppToken(string token)
    {
        File.ReadAllText(TokensCssPath).Should().Contain($"{token}:", $"aplikační token {token} patří do tokens.css");
    }

    [Fact]
    public void TokensCss_DarkThemeSwitchesNativeControlsToDarkColorScheme()
    {
        var css = File.ReadAllText(TokensCssPath);
        Regex.IsMatch(css, @":root\[data-theme=""dark""\]\s*\{[^}]*color-scheme:\s*dark", RegexOptions.Singleline)
            .Should().BeTrue("bez color-scheme zůstanou nativní select/datum/posuvníky v tmavém motivu světlé");
    }
```

(Pokud `TokensCssTests` nemá `using System.Text.RegularExpressions;`, doplnit ho.)

- [ ] **Step 2: Spustit a ověřit, že test padá**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~TokensCssTests" 2>&1 | tail -8`
Expected: 11 nových testů FAIL (`Expected … to contain "--app-shadow-soft:"` atd.), původní PASS.

- [ ] **Step 3: Implementace — doplnit na konec `tokens.css`**

```css
/* ---------------------------------------------------------
 * Aplikační barvy (2026-10-15)
 * Barvy aplikace jsou sémantické tokeny gov DS 4.7.0 (--background-*, --text-*,
 * --border-*, --icon-*, --status-*), které přepíná gov podle data-theme.
 * Sem patří jen barvy, které gov nemá. Tohle je jediný soubor aplikace, který
 * smí obsahovat data-theme a barevné literály (hlídá AppCssColorTokenTests).
 * --------------------------------------------------------- */
:root {
    /* Stíny — gov DS nemá tokeny stínů. */
    --app-shadow-soft: 0 2px 8px rgba(11, 12, 12, 0.08);
    --app-shadow-raised: 0 8px 24px rgba(11, 12, 12, 0.14);

    /* Kategorie Rozhodnutí — fialová mimo gov paletu, odlišná od semaforu úkolů
       (zelená/oranžová/červená) a modré Informace. Viz docs/wiki/projekty/zaznamy/barvy-karet.md. */
    --app-category-decision: #7c3aed;
    --app-category-decision-subtle: #f3e8ff;

    /* Harmonogram — duhová paleta kroků a skluz (dříve --schedule-step-* v site.css). */
    --app-schedule-step-1: #ef4444;
    --app-schedule-step-2: #f97316;
    --app-schedule-step-3: #f59e0b;
    --app-schedule-step-4: #84cc16;
    --app-schedule-step-5: #22c55e;
    --app-schedule-step-6: #14b8a6;
    --app-schedule-step-7: #06b6d4;
    --app-schedule-step-8: #3b82f6;
    --app-schedule-step-9: #6366f1;
    --app-schedule-step-10: #8b5cf6;
    --app-schedule-step-11: #d946ef;
    --app-schedule-delay: #dc2626;

    /* Překryvy pruhů harmonogramu. Pruhy mají barvy z Excelu (HarmonogramKroky.cs),
       stejné v obou motivech — proto i tyto hodnoty jsou v obou motivech stejné. */
    --app-schedule-stripe: rgba(255, 255, 255, 0.55);
    --app-schedule-segment-inner: rgba(255, 255, 255, 0.35);
    --app-schedule-segment-edge: rgba(15, 23, 42, 0.42);
}

:root[data-theme="dark"] {
    /* Nativní ovládací prvky (select, datum, čas, posuvníky) v tmavém provedení. */
    color-scheme: dark;

    --app-shadow-soft: 0 4px 16px rgba(0, 0, 0, 0.28);
    --app-shadow-raised: 0 8px 24px rgba(0, 0, 0, 0.45);
    --app-category-decision: #c4b5fd;
    --app-category-decision-subtle: #2e1065;
}
```

- [ ] **Step 4: Spustit a ověřit, že testy prochází**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~TokensCssTests" 2>&1 | tail -5`
Expected: vše PASS.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/wwwroot/css/tokens.css PmTracker.Tests.Unit/Layout/TokensCssTests.cs
git commit -m "feat(barvy): aplikační tokeny a tmavý color-scheme v tokens.css"
```

---

### Task 3: Staré barevné proměnné → gov tokeny

**Files:**
- Create: `PmTracker.Tests.Unit/Architecture/AppCss.cs` (sdílený parser pro kontrolní testy)
- Create: `PmTracker.Tests.Unit/Architecture/AppCssColorTokenTests.cs`
- Modify: `PmTracker.Web/wwwroot/css/site.css`, `govcz.css`, `tokens.css`, `components/*.css`
- Modify: `PmTracker.Web/wwwroot/js/modules/schedule/block.js` (přejmenování `--schedule-step-*`, pokud ho používá)
- Modify: `PmTracker.Tests.Unit/Projects/ProposalCardCssTests.cs:24`, `PmTracker.Tests.Unit/Layout/TokensCssTests.cs:33-34`

**Interfaces:**
- Consumes: tokeny z Tasku 2
- Produces: `AppCss.Declarations(relPath)`, `AppCss.AppCssFiles()`, `AppCss.DefinedVariables()` (používají Tasky 4–5)

- [ ] **Step 1: Napsat sdílený parser**

`PmTracker.Tests.Unit/Architecture/AppCss.cs`:

```csharp
using System.Text.RegularExpressions;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Minimální čtečka aplikačního CSS pro kontrolní testy barev (2026-10-15). Rozumí pravidlům
/// i vnořeným @media (vnitřní pravidlo se najde samo, prelude @media se přeskočí).
/// </summary>
internal static class AppCss
{
    public sealed record Declaration(string File, int Line, string Selector, string Property, string Value);

    public static string RepoRoot { get; } = LocateRepositoryRoot();

    public static string CssDir => Path.Combine(RepoRoot, "PmTracker.Web", "wwwroot", "css");

    public static string GovStylesDir => Path.Combine(RepoRoot, "PmTracker.Web", "wwwroot", "assets", "gov", "styles");

    /// <summary>Aplikační CSS bez tisku (pdf-export.css je mimo rozsah — barvy tisku dává spec 2026-09-05).</summary>
    public static IReadOnlyList<string> AppCssFiles() =>
        Directory.GetFiles(CssDir, "*.css", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(CssDir, path).Replace('\\', '/'))
            .Where(path => path != "pdf-export.css")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    public static string StripComments(string css) =>
        Regex.Replace(css, @"/\*.*?\*/", m => new string('\n', m.Value.Count(c => c == '\n')), RegexOptions.Singleline);

    public static IEnumerable<Declaration> Declarations(string relPath)
    {
        var css = StripComments(File.ReadAllText(Path.Combine(CssDir, relPath)));
        var lineStarts = new List<int> { 0 };
        for (var i = 0; i < css.Length; i++)
        {
            if (css[i] == '\n') lineStarts.Add(i + 1);
        }

        int LineOf(int offset)
        {
            var index = lineStarts.BinarySearch(offset);
            return (index >= 0 ? index : ~index - 1) + 1;
        }

        foreach (Match rule in Regex.Matches(css, @"([^{}]+)\{([^{}]*)\}"))
        {
            var selector = Regex.Replace(rule.Groups[1].Value, @"\s+", " ").Trim();
            var body = rule.Groups[2];
            foreach (Match declaration in Regex.Matches(body.Value, @"(--[\w-]+|[a-zA-Z-]+)\s*:\s*([^;]+)"))
            {
                yield return new Declaration(
                    relPath,
                    LineOf(body.Index + declaration.Index),
                    selector,
                    declaration.Groups[1].Value.Trim().ToLowerInvariant(),
                    declaration.Groups[2].Value.Trim());
            }
        }
    }

    public static bool IsColorProperty(string property) =>
        property.StartsWith("--", StringComparison.Ordinal)
        || Regex.IsMatch(property,
            @"^(color|background(-color|-image)?|border(-(top|right|bottom|left|block|inline)(-(start|end))?)?(-color)?|outline(-color)?|box-shadow|text-shadow|fill|stroke|caret-color|accent-color|text-decoration(-color)?|column-rule(-color)?)$");

    /// <summary>Všechny custom properties definované v gov stylech a v aplikačním CSS.</summary>
    public static HashSet<string> DefinedVariables()
    {
        var defined = new HashSet<string>(StringComparer.Ordinal);
        var sources = Directory.GetFiles(GovStylesDir, "*.css")
            .Concat(AppCssFiles().Select(file => Path.Combine(CssDir, file)));
        foreach (var path in sources)
        {
            foreach (Match match in Regex.Matches(File.ReadAllText(path), @"(--[\w-]+)\s*:"))
            {
                defined.Add(match.Groups[1].Value);
            }
        }

        return defined;
    }

    private static string LocateRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře.");
    }
}
```

- [ ] **Step 2: Napsat padající testy**

`PmTracker.Tests.Unit/Architecture/AppCssColorTokenTests.cs`:

```csharp
using System.Text.RegularExpressions;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Barvy aplikace = sémantické tokeny gov DS 4.7.0 (2026-10-15, memory feedback_dark_mode_gov_tokens).
/// Tmavý motiv přepíná gov sám; aplikace nesmí držet vlastní paletu ani tmavá pravidla na komponentách.
/// </summary>
public sealed class AppCssColorTokenTests
{
    /// <summary>Proměnné nastavované až za běhu (inline style ve view / JS) — nejsou v žádném CSS.</summary>
    private static readonly HashSet<string> RuntimeVariables = new(StringComparer.Ordinal)
    {
        "--seg-color",                     // _ScheduleBlock.cshtml: barva kroku z Excelu
        "--record-schedule-delay-color",   // _ScheduleBlock.cshtml: barva skluzu
    };

    private static readonly Regex LegacyColorVariable = new(
        @"--(gov-color-[\w-]+|app-text-primary|app-muted|app-nav-[\w-]+|app-bg-[\w-]+|pm-surface[\w-]*|pm-text[\w-]*|pm-border|pm-warning[\w-]*|pm-color-[\w-]+|record-cat-decision|schedule-step-\d+|schedule-delay|gov-shadow-soft)\b");

    [Fact]
    public void LegacyColorVariables_AreNeitherDefinedNorUsed()
    {
        var offenders = AppCss.AppCssFiles()
            .SelectMany(file => AppCss.StripComments(File.ReadAllText(Path.Combine(AppCss.CssDir, file)))
                .Split('\n')
                .Select((text, index) => (file, line: index + 1, text))
                .Where(x => LegacyColorVariable.IsMatch(x.text))
                .Select(x => $"{x.file}:{x.line}: {x.text.Trim()}"))
            .ToList();

        offenders.Should().BeEmpty("stará paleta aplikace (--gov-color-*, --app-*, --pm-*) je nahrazená gov tokeny");
    }

    [Fact]
    public void ColorProperties_ReferenceOnlyDefinedVariables()
    {
        var defined = AppCss.DefinedVariables();
        var offenders = AppCss.AppCssFiles()
            .SelectMany(AppCss.Declarations)
            .Where(d => AppCss.IsColorProperty(d.Property))
            .SelectMany(d => Regex.Matches(d.Value, @"var\((--[\w-]+)").Select(m => (d, name: m.Groups[1].Value)))
            .Where(x => !defined.Contains(x.name) && !RuntimeVariables.Contains(x.name))
            .Select(x => $"{x.d.File}:{x.d.Line} {x.d.Selector} {{ {x.d.Property}: {x.d.Value} }} → {x.name}")
            .ToList();

        offenders.Should().BeEmpty("nedefinovaná proměnná spadne na záložní (světlou) hodnotu i v tmavém motivu");
    }
}
```

- [ ] **Step 3: Spustit a ověřit, že testy padají**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~AppCssColorTokenTests" 2>&1 | tail -20`
Expected:
- oba FAIL;
- `LegacyColorVariables…` vypíše stovky řádků (např. `site.css:…: color: var(--gov-color-muted)`);
- `ColorProperties…` vypíše mimo jiné `--gov-color-text-secondary`, `--gov-color-neutral-bg` a `--pm-tab-active-bg`.

- [ ] **Step 4: Nahradit proměnné skriptem**

Skript se do repa necommituje. Uložit ho do scratchpadu jako `replace_legacy_vars.py` a spustit z kořene worktree.

```python
import re, glob, sys
CSS = "PmTracker.Web/wwwroot/css"
files = [f for f in glob.glob(f"{CSS}/**/*.css", recursive=True) if not f.endswith("pdf-export.css")]

T, B, R = "text", "bg", "border"
def m(text=None, bg=None, border=None): return {T: text, B: bg, R: border}
MAP = {}
def put(names, mapping):
    for n in names.split(): MAP[n] = mapping
put("--gov-color-primary --pm-color-primary", m("--text-primary-color", "--background-primary", "--border-primary"))
put("--gov-color-primary-strong --pm-color-primary-strong", m("--text-primary-color", "--button-solid-primary-hover", "--border-primary"))
put("--gov-color-primary-bg", m("--text-primary-color", "--background-primary-subtlest", "--border-primary"))
put("--gov-color-surface --pm-surface --app-nav-active-bg", m("--text-primary", "--background-block-primary", "--border-subtle"))
put("--pm-surface-alt", m(None, "--background-block-secondary", None))
put("--gov-color-bg --pm-color-background", m(None, "--background-page", None))
put("--app-nav-hover --gov-color-neutral-bg-hover", m(None, "--background-neutral-subtle", None))
put("--gov-color-neutral-bg", m(None, "--background-neutral-subtlest", None))
put("--gov-color-border --gov-color-neutral-border --pm-border --pm-color-border", m("--text-secondary", "--background-neutral-subtle", "--border-subtle"))
put("--app-text-primary --gov-color-text --pm-text --pm-color-text", m("--text-primary", None, "--border-neutral"))
put("--gov-color-muted --app-muted --gov-color-text-secondary --gov-color-text-muted --pm-text-muted --pm-color-text-muted --text-muted --pm-color-neutral",
    m("--text-secondary", "--background-neutral-disabled", "--border-subtle"))
put("--gov-color-success --pm-color-success", m("--text-status-success", "--background-status-success", "--border-success"))
put("--gov-color-warning --pm-warning --pm-color-warning", m("--text-status-warning", "--background-status-warning", "--border-warning"))
put("--gov-color-warning-strong --pm-warning-strong --text-warning-strong", m("--text-status-warning", None, "--border-warning"))
put("--gov-color-warning-bg --pm-warning-subtle --background-warning-subtle", m(None, "--background-status-warning-subtlest", None))
put("--gov-color-accent --gov-color-danger --gov-color-error --gov-color-error-500 --text-error --text-error-strong",
    m("--text-status-error", "--background-status-error", "--border-error"))
put("--gov-color-error-bg --gov-color-danger-bg", m(None, "--background-status-error-subtlest", None))
put("--color-focus", m("--status-focus", "--status-focus", "--status-focus"))
put("--app-nav-active-border", m(None, None, "--border-primary"))
RENAME = {"--gov-shadow-soft": "--app-shadow-soft", "--record-cat-decision": "--app-category-decision",
          "--schedule-delay": "--app-schedule-delay", **{f"--schedule-step-{i}": f"--app-schedule-step-{i}" for i in range(1, 12)}}

def kind(prop):
    p = prop.lower()
    if p.startswith("background"): return B
    if p.startswith(("border", "outline", "column-rule")): return R
    if p in ("color", "fill", "stroke", "caret-color", "text-decoration-color", "accent-color"): return T
    return None

manual = []
decl_re = re.compile(r'(?P<prop>--[\w-]+|[a-zA-Z-]+)(?P<sep>\s*:\s*)(?P<val>[^;{}]+)')
for f in files:
    src = open(f, encoding="utf-8").read()
    def fix(mt):
        prop, val = mt.group("prop"), mt.group("val")
        def sub(vm):
            name, rest = vm.group(1), vm.group(2) or ""
            if name in RENAME: return f"var({RENAME[name]}{rest})"
            if name not in MAP: return vm.group(0)
            k = kind(prop)
            target = MAP[name].get(k) if k else None
            if not target:
                manual.append(f"{f}: {prop}: {val.strip()}  [{name}]")
                return vm.group(0)
            return f"var({target})"  # záložní hodnota se zahazuje (Global Constraints)
        new_val = re.sub(r'var\((--[\w-]+)((?:,[^()]*(?:\([^()]*\))?[^()]*)?)\)', sub, val)
        return f"{prop}{mt.group('sep')}{new_val}"
    out = decl_re.sub(fix, src)
    if out != src: open(f, "w", encoding="utf-8").write(out)
print("\n".join(manual) if manual else "nic k ručnímu rozhodnutí")
```

Run: `python3 <scratchpad>/replace_legacy_vars.py`
Expected: seznam deklarací k ručnímu rozhodnutí. Typicky jde o:
- `box-shadow` s barevnou proměnnou;
- definice custom properties typu `--x: var(--gov-color-primary)`;
- `linear-gradient` v `background`.

Každou vyřešit podle tabulky Mapování podle významu. Zbylé starší vzory (záložní hodnoty u proměnných, které nejsou v `MAP`) dořeší Task 4.

- [ ] **Step 5: Smazat definice staré palety**

- **`govcz.css`:**
  - Z `:root` smazat všechny `--gov-color-*`, `--record-cat-decision`, `--gov-shadow-soft` a `--app-*`, ponechat jen `--gov-radius*` a `--gov-font`.
  - Celý blok `:root[data-theme="dark"]` smazat.
  - Na `body` vložit `color: var(--text-primary); background: var(--background-page);`.
- **`site.css` `:root` (ř. 9–49):**
  - smazat `--schedule-step-*` a `--schedule-delay` (přesunuté do tokens.css);
  - smazat celý blok `--pm-surface` … `--pm-warning-strong` i jeho komentáře;
  - ponechat `--app-picker-panel-width`, `--pm-font-mono` a `--app-sticky-top`.
- **`site.css` ř. ~319–327:** smazat blok `:root[data-theme="dark"] { --pm-surface: … }`.
- **`tokens.css`:** smazat sekci `/* Barvy — semantické aliasy */`, tj. `--pm-color-*`.
- **Kontrola mazání:** `grep -rn -- "--pm-color-\|--gov-color-\|--pm-surface\|--app-text-primary" PmTracker.Web/wwwroot PmTracker.Web/Views` nesmí nic najít. Pozor na view a JS.

- [ ] **Step 6: Opravit testy, které hlídají staré názvy**

- `ProposalCardCssTests.cs:24`: `var(--gov-color-border)` → `var(--border-subtle)`, důvod upravit na „rámeček karty návrhu je gov token“.
- `TokensCssTests.cs:29-35` (test na `--pm-color-primary`): přepsat na tvrzení, že `tokens.css` **neobsahuje** `--pm-color-` („barvy aplikace jsou přímo gov tokeny“).

- [ ] **Step 7: Spustit a ověřit, že testy prochází**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~AppCssColorTokenTests|FullyQualifiedName~ProposalCardCssTests|FullyQualifiedName~TokensCssTests" 2>&1 | tail -8`
Expected:
- `LegacyColorVariables_AreNeitherDefinedNorUsed` PASS;
- `ColorProperties_ReferenceOnlyDefinedVariables` PASS (pokud ne, doplnit podle tabulky a ledgerovat `Ruling:`);
- ostatní PASS.

Run: `dotnet test PmTracker.Tests.Unit 2>&1 | tail -3`
Expected:
- vše zelené kromě testů, které hlídají tmavá pravidla: `DarkThemeInputScopingTests` a `GlobalSearchDarkModeCssTests`;
- ty se přepíšou v Tasku 5 a do té doby pořád prochází, protože tmavá pravidla ještě existují;
- jiné selhání je regrese.

- [ ] **Step 8: Commit**

```bash
git add -A PmTracker.Web/wwwroot/css PmTracker.Web/wwwroot/js PmTracker.Tests.Unit
git commit -m "refactor(barvy): staré barevné proměnné aplikace nahrazeny gov tokeny"
```

---

### Task 4: Natvrdo zapsané barvy → gov tokeny

**Files:**
- Modify: `PmTracker.Web/wwwroot/css/site.css`, `components/*.css`
- Test: `PmTracker.Tests.Unit/Architecture/AppCssColorTokenTests.cs`

**Interfaces:**
- Consumes: `AppCss` (Task 3), tokeny `--app-*` (Task 2)

- [ ] **Step 1: Napsat padající test**

Do `AppCssColorTokenTests` přidat:

```csharp
    private static readonly Regex ColorLiteral = new(@"#[0-9a-fA-F]{3,8}\b|\b(rgba?|hsla?)\(|\b(white|black)\b", RegexOptions.IgnoreCase);

    /// <summary>Pojmenované výjimky: hlavička je podle zadání 2026-10-15 zmražená.</summary>
    private static readonly HashSet<string> AllowedLiterals = new(StringComparer.Ordinal)
    {
        ".app-logo|background",   // rámeček loga v hlavičce zůstává bílý v obou motivech
    };

    public static IEnumerable<object[]> FilesWithoutColorLiterals() =>
        AppCss.AppCssFiles().Where(file => file != "tokens.css").Select(file => new object[] { file });

    [Theory]
    [MemberData(nameof(FilesWithoutColorLiterals))]
    public void ColorProperties_UseTokensNotLiterals(string file)
    {
        var offenders = AppCss.Declarations(file)
            .Where(d => AppCss.IsColorProperty(d.Property))
            .Where(d => ColorLiteral.IsMatch(d.Value))
            .Where(d => !AllowedLiterals.Contains($"{d.Selector}|{d.Property}"))
            .Select(d => $"{d.File}:{d.Line} {d.Selector} {{ {d.Property}: {d.Value} }}")
            .ToList();

        offenders.Should().BeEmpty("barva patří do gov tokenu (nebo aplikačního --app-* v tokens.css), jinak se v tmavém motivu nepřepne");
    }
```

- [ ] **Step 2: Spustit a ověřit, že test padá**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ColorProperties_UseTokensNotLiterals" 2>&1 | grep -E "Failed|Passed" | head -12`
Expected:
- FAIL pro `site.css` (stovky deklarací, včetně všech `:root[data-theme="dark"] …`) a pro `components/chat-modal.css`, `externi-odkaz-card.css`, `schedule-actual-cell-feature-c.css` a `sdconnector-inspect.css`;
- PASS pro `govcz.css`, `components/sdconnector-page.css`, `vyzvy-panel.css` a `zakladni-report.css`.

Tmavá pravidla v `site.css` se tu nepřevádějí, smaže je Task 5. Test pro `site.css` proto zezelená až po Tasku 5.

- [ ] **Step 3: Převést literály skriptem**

Skript uložit do scratchpadu jako `replace_color_literals.py`. Skupiny kopírují tabulku „Natvrdo zapsané barvy“.

```python
import re, glob
CSS = "PmTracker.Web/wwwroot/css"
files = [f for f in glob.glob(f"{CSS}/**/*.css", recursive=True)
         if not f.endswith(("pdf-export.css", "tokens.css"))]

def norm(v): return re.sub(r"\s+", "", v.lower())
G = {}
def group(values, text=None, bg=None, border=None):
    for v in values: G[norm(v)] = {"text": text, "bg": bg, "border": border}
group("#0f172a #1f2937 #1e293b #111111 #0b0c0c".split(), "--text-primary", "--background-neutral-secondary", "--border-neutral")
group("#475467 #475569 #64748b #6b7280 #7f9bbf".split(), "--text-secondary", "--background-neutral-subtle", "--border-subtle")
group("#94a3b8 #98a2b3".split(), "--text-tertiary", "--background-neutral-disabled", "--border-subtle")
group(["#cbd5e1"], "--text-disabled", "--background-neutral-disabled", "--border-subtle")
group(["#fff", "#ffffff", "white", "rgba(255,255,255,0.98)", "rgba(255,255,255,0.96)", "rgba(255,255,255,0.86)",
       "rgba(255,255,255,0.85)", "rgba(255,255,255,0.72)", "rgba(248,251,255,0.96)", "rgba(248,250,252,0.92)",
       "rgba(248,250,252,1)"], "--text-white-fixed", "--background-block-primary", "--background-block-primary")
group(["#f8fafc", "#f9fafb", "#fafafa", "#f1f5f9", "#f2f4f7", "#ededed", "rgba(241,245,249,0.9)", "rgba(0,43,73,0.02)",
       "rgba(0,43,73,0.04)", "rgba(15,23,42,0.08)", "rgba(148,163,184,0.07)", "rgba(148,163,184,0.08)"],
      None, "--background-neutral-subtlest", "--border-subtlest")
group(["#e2e8f0"], None, "--background-neutral-subtle", "--border-subtlest")
group(["#d0d5dd", "#e5e7eb", "#d1d5db", "#d8dbdd", "#e7edf7", "#ccd7ea", "rgba(148,163,184,0.35)", "rgba(148,163,184,0.24)",
       "rgba(15,23,42,0.22)", "rgba(0,43,73,0.1)", "rgba(0,43,73,0.22)", "rgba(29,78,216,0.14)"],
      None, "--border-subtle", "--border-subtle")
group("#1e3a8a #1e40af #1d4ed8 #0066cc #2362a2 #005ea5 #3568be #2563eb #bfdbfe".split(),
      "--text-primary-color", "--background-primary", "--border-primary")
group(["#eff6ff", "#dbeafe", "#dfeaf5", "#e9f5ff", "#edf3fb", "#dce6f5", "rgba(37,99,235,0.10)", "rgba(37,99,235,0.1)",
       "rgba(37,99,235,0.12)", "rgba(53,104,190,0.14)", "rgba(53,104,190,0.12)", "rgba(29,78,216,0.12)",
       "rgba(29,78,216,0.07)", "rgba(240,246,255,0.92)"], None, "--background-primary-subtlest", "--border-primary")
group(["rgba(53,104,190,0.95)", "rgba(32,76,145,0.95)"], None, "--background-primary", None)
group("#b91c1c #dc2626 #991b1b #b42318 #c53030 #c52a3a #ef4444 #6b211b #8a1c16 #9a3412 #d83b34 #f87171 #f4b6b0 #fecaca #fca5a5".split(),
      "--text-status-error", "--background-status-error", "--border-error")
group(["#fef2f2", "#fee2e2", "#f9e1de", "rgba(220,38,38,0.04)", "rgba(248,113,113,0.08)", "rgba(239,68,68,0.08)"],
      None, "--background-status-error-subtlest", "--border-error")
group("#166534 #15803d #16a34a #059669 #0f766e #22c55e".split(), "--text-status-success", "--background-status-success", "--border-success")
group(["#dcfce7", "rgba(22,163,74,0.16)"], None, "--background-status-success-subtlest", None)
group(["rgba(22,163,74,0.22)"], None, "--background-status-success-subtle", None)
group("#92400e #b45309 #f59e0b".split(), "--text-status-warning", "--background-status-warning", "--border-warning")
group(["#fef3c7", "rgba(245,158,11,0.16)"], None, "--background-status-warning-subtlest", None)
group(["rgba(245,158,11,0.22)"], None, "--background-status-warning-subtle", None)
group(["#fde68a"], None, "--background-secondary-subtle", None)
group(["#7c3aed"], "--app-category-decision", "--app-category-decision-subtle", "--app-category-decision")
group(["rgba(15,23,42,0.55)"], None, "--background-overlay-page", None)

SELECTOR_OVERRIDES = [  # (regex na selektor, literál, token) — mají přednost před skupinami
    (r"schedule.*segment|legend-swatch\.actual", "rgba(255,255,255,0.55)", "--app-schedule-stripe"),
    (r"schedule.*segment", "rgba(255,255,255,0.35)", "--app-schedule-segment-inner"),
    (r"schedule.*segment|legend-swatch", "rgba(15,23,42,0.42)", "--app-schedule-segment-edge"),
    (r"\.timeline-axis-tick\.edge", "rgba(71,85,105,0.55)", "--border-neutral"),
    (r"\.timeline-axis-tick", "rgba(100,116,139,0.3)", "--border-subtle"),
    (r"legend-swatch\.today", "#111111", "--background-neutral-primary"),
    (r"legend-swatch\.deadline", "#1e293b", "--background-neutral-secondary"),
    (r"\.sdc-inspect__raw-col pre", "#e2e8f0", "--text-white-fixed"),
]
TRANSPARENT = {norm("rgba(255,255,255,0)"), norm("rgba(35,98,162,0)")}
LIT = re.compile(r"#[0-9a-fA-F]{3,8}\b|rgba?\([^)]*\)|\bwhite\b", re.I)

def kind(prop):
    p = prop.lower()
    if p.startswith("background"): return "bg"
    if p.startswith(("border", "outline", "column-rule")): return "border"
    if p in ("color", "fill", "stroke", "caret-color", "text-decoration-color", "accent-color"): return "text"
    return None

manual = []
for f in files:
    src = open(f, encoding="utf-8").read()
    def fix_rule(rm):
        selector, body = rm.group(1), rm.group(2)
        if "data-theme" in selector: return rm.group(0)          # tmavá pravidla maže Task 5
        if re.search(r"\.app-logo\b", selector): return rm.group(0)  # hlavička zmražená
        def fix_decl(dm):
            prop, val = dm.group(1), dm.group(3)
            if prop.lower() in ("box-shadow", "text-shadow") or "gradient" in val:
                if LIT.search(val): manual.append(f"{f}: {selector.strip()[-70:]} {{ {prop}: {val.strip()} }}")
                return dm.group(0)
            k = kind(prop)
            def sub(lm):
                lit = norm(lm.group(0))
                if lit in TRANSPARENT: return "transparent"
                for pattern, literal, token in SELECTOR_OVERRIDES:
                    if lit == norm(literal) and re.search(pattern, selector): return f"var({token})"
                target = G.get(lit, {}).get(k) if k else None
                if target: return f"var({target})"
                manual.append(f"{f}: {selector.strip()[-70:]} {{ {prop}: {val.strip()} }}  [{lm.group(0)}]")
                return lm.group(0)
            # záložní hodnoty var(--x, #hex) → var(--x)
            val2 = re.sub(r"var\((--[\w-]+)\s*,\s*[^()]*(?:\([^()]*\))?[^()]*\)", r"var(\1)", val)
            return f"{prop}{dm.group(2)}{LIT.sub(sub, val2)}"
        return selector + "{" + re.sub(r"([a-zA-Z-]+|--[\w-]+)(\s*:\s*)([^;{}]+)", fix_decl, body) + "}"
    out = re.sub(r"([^{}]+)\{([^{}]*)\}", fix_rule, src)
    if out != src: open(f, "w", encoding="utf-8").write(out)
print("\n".join(manual) if manual else "nic k ručnímu rozhodnutí")
```

Run: `python3 <scratchpad>/replace_color_literals.py > <scratchpad>/manual-literals.txt; wc -l <scratchpad>/manual-literals.txt`
Expected:
- seznam stínů, přechodů a literálů, které nejsou ve skupinách;
- odhadem 40–70 řádků.

- [ ] **Step 4: Rozhodnout zbytek ručně podle Mapování**

Pro každý řádek `manual-literals.txt`:
- **Stíny a prstence:** podle pravidel v Mapování („box-shadow a text-shadow se nahrazují celou hodnotou“).
- **Přechody na `.dashboard-*` a `.access-*`:** plocha podle Mapování.
- **Chevron `<select>`:** `linear-gradient(45deg, transparent 50%, var(--icon-neutral) 50%), linear-gradient(135deg, var(--icon-neutral) 50%, transparent 50%)`.
- **Bílý text na barevném tlačítku nebo štítku** (`.dashboard-category-filter.active`, `.modal-submit-action-btn--primary:hover`, `.timeline-axis-event-label`):
  - text `--text-white-fixed`;
  - ověřit, že pozadí je plné (`--background-primary`, `--background-status-*`), viz Review Focus 1.
- **Hover a aktivní stavy**, kde vyšlo `--background-neutral-subtlest` na ploše `--background-block-primary`: změnit na `--background-neutral-subtle` (Review Focus 2).
- **Literál, který nesedí na žádné pravidlo:** zvolit nejbližší sémantický token a zapsat do ledgeru `Ruling: <selektor> <literál> → <token> — <proč>`.

- [ ] **Step 5: Nativní formulářové prvky a Quill dostanou tokeny v základním pravidle**

V `site.css` najít základní pravidlo nativních inputů (`input[type="text"]:not(gov-form-input input)…`, kolem ř. 1960–1990). Doplnit nebo upravit ho tak, aby měl **stejné vyloučení gov komponent** jako dnešní tmavé pravidlo (memory `feedback_gov_web_components_css_scoping`):

```css
input[type="text"]:not(gov-form-input input):not(gov-form-search input):not([role="search"] input),
input[type="email"]:not(gov-form-input input):not(gov-form-search input):not([role="search"] input),
input[type="date"]:not(gov-form-input input):not(gov-form-search input):not([role="search"] input),
input[type="time"]:not(gov-form-input input):not(gov-form-search input):not([role="search"] input),
input[type="datetime-local"]:not(gov-form-input input):not(gov-form-search input):not([role="search"] input),
input[type="number"]:not(gov-form-input input):not(gov-form-search input):not([role="search"] input),
input[type="search"]:not(gov-form-input input):not(gov-form-search input):not([role="search"] input),
select:not(gov-form-select select):not(pm-select select),
textarea:not(gov-form-input textarea):not(pm-textarea textarea) {
    /* Stejně jako gov-form-input: plocha bloku, jemný rámeček (gov components.css .gov-form-input>.element). */
    background-color: var(--background-block-primary);
    border-color: var(--border-subtle);
    color: var(--text-primary);
}
```

Použít `background-color`, ne zkratku `background`. Zkratka by smazala chevron selectu (komentář „Bug fix 2026-04-29“).

Quill (`.richtext-host`) má v knihovně natvrdo `#444` a `#ccc`. Doplnit základní pravidla:

```css
.richtext-host .ql-toolbar.ql-snow { background: var(--background-block-secondary); border-color: var(--border-subtle); }
.richtext-host .ql-container.ql-snow { background: var(--background-block-primary); border-color: var(--border-subtle); color: var(--text-primary); }
.richtext-host .ql-snow .ql-stroke { stroke: var(--icon-default); }
.richtext-host .ql-snow .ql-fill { fill: var(--icon-default); }
.richtext-host .ql-snow .ql-picker { color: var(--text-primary); }
.richtext-host .ql-editor.ql-blank::before { color: var(--text-placeholder); }
```

Ostatní vlastnosti, které dnes nastavují jen tmavá pravidla, vypíše Task 5 Step 3 a doplní se tam.

- [ ] **Step 6: Spustit a ověřit, že testy prochází**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ColorProperties_UseTokensNotLiterals" 2>&1 | grep -E "Failed|Passed|Total"`
Expected:
- PASS pro všechny soubory kromě `site.css`;
- u `site.css` už zbývají jen deklarace se selektorem `:root[data-theme="dark"] …`;
- ověřit: `dotnet test … 2>&1 | grep "site.css:" | grep -vc 'data-theme="dark"'` = `0`.

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~AppCssColorTokenTests" 2>&1 | tail -3`
Expected: dva testy z Tasku 3 dál PASS.

- [ ] **Step 7: Commit**

```bash
git add -A PmTracker.Web/wwwroot/css PmTracker.Tests.Unit
git commit -m "refactor(barvy): natvrdo zapsané barvy nahrazeny gov tokeny"
```

---

### Task 5: Smazat tmavá pravidla — tmavý motiv řídí jen tokeny

**Files:**
- Modify: `PmTracker.Web/wwwroot/css/site.css`
- Modify (přepsat): `PmTracker.Tests.Unit/Architecture/DarkThemeInputScopingTests.cs`, `PmTracker.Tests.Unit/Search/GlobalSearchDarkModeCssTests.cs`
- Test: `PmTracker.Tests.Unit/Architecture/AppCssColorTokenTests.cs`

**Interfaces:**
- Consumes: `AppCss` (Task 3)

- [ ] **Step 1: Napsat padající test**

```csharp
    [Fact]
    public void DarkThemeSelectors_OnlyInTokensCss()
    {
        var offenders = AppCss.AppCssFiles()
            .Where(file => file != "tokens.css")
            .SelectMany(AppCss.Declarations)
            .Where(d => d.Selector.Contains("data-theme", StringComparison.Ordinal))
            .Select(d => $"{d.File}:{d.Line} {d.Selector}")
            .Distinct()
            .ToList();

        offenders.Should().BeEmpty("tmavý motiv přepínají gov tokeny; pravidlo :root[data-theme=\"dark\"] .x je ruční paleta navíc");
    }

    [Fact]
    public void TokensCss_DarkBlockOnlyDefinesTokensAndColorScheme()
    {
        var offenders = AppCss.Declarations("tokens.css")
            .Where(d => d.Selector.Contains("data-theme", StringComparison.Ordinal))
            .Where(d => !d.Property.StartsWith("--", StringComparison.Ordinal) && d.Property != "color-scheme")
            .Select(d => $"tokens.css:{d.Line} {d.Selector} {{ {d.Property} }}")
            .ToList();

        offenders.Should().BeEmpty();
    }
```

- [ ] **Step 2: Spustit a ověřit, že test padá**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~DarkThemeSelectors_OnlyInTokensCss" 2>&1 | tail -15`
Expected:
- FAIL;
- vypíše asi 150 selektorů ze `site.css` (ř. ~331–346, 2078–2082, 2785–2790, 3024–3032, 3217, 4608, 6603–7122);
- jeden selektor z `components/chat-modal.css`.

- [ ] **Step 3: Vypsat, co tmavá pravidla nastavují navíc oproti základním**

Skript `dark_rules_report.py` do scratchpadu:

```python
import re
src = open("PmTracker.Web/wwwroot/css/site.css", encoding="utf-8").read()
src_nc = re.sub(r"/\*.*?\*/", "", src, flags=re.S)
base = {}
for m in re.finditer(r"([^{}]+)\{([^{}]*)\}", src_nc):
    for sel in m.group(1).split(","):
        sel = " ".join(sel.split())
        if "data-theme" in sel: continue
        props = {p.strip().lower() for p in re.findall(r"([\w-]+)\s*:", m.group(2))}
        base.setdefault(sel, set()).update(props)
for m in re.finditer(r"([^{}]+)\{([^{}]*)\}", src_nc):
    if "data-theme" not in m.group(1): continue
    props = {p.strip().lower() for p in re.findall(r"([\w-]+)\s*:", m.group(2))}
    for sel in m.group(1).split(","):
        plain = " ".join(re.sub(r':root\[data-theme="dark"\]\s*|html\[data-theme="dark"\]\s*', "", sel).split())
        missing = props - base.get(plain, set())
        if missing:
            print(f"{plain}: tmavé pravidlo nastavuje {sorted(missing)}, základní pravidlo ne")
```

Run: `python3 <scratchpad>/dark_rules_report.py`
Expected: seznam selektorů. Pro každý doplnit **základní** pravidlo s gov tokenem podle Mapování, typicky `.badge`, `.pill`, `.record-delete-summary`, `.dashboard-*` a `.richtext-host …`. Doplňky typu filtr ikony výběru data (`::-webkit-calendar-picker-indicator { filter: invert(0.9) }`) se nepřevádějí, protože je řeší `color-scheme: dark` z Tasku 2. Každé rozhodnutí „nepřevádí se“ zapsat jako `Ruling:`.

- [ ] **Step 4: Smazat tmavá pravidla**

- **V `site.css`** smazat každé pravidlo, jehož selektor obsahuje `data-theme`. Tím odpadnou tmavé override inputů (ř. ~6705–6734), dlaždic hledání (ř. ~331–346) i blok 6603–7122.
- **Komentáře** nad smazanými pravidly smazat taky. Ponechat jen komentář vyhledávacího pole (ř. 145–148), který říká, že gov komponenty se nestylují.
- **V `components/chat-modal.css`** smazat jediné tmavé pravidlo.
- **Kontrola:** `grep -c 'data-theme' PmTracker.Web/wwwroot/css/site.css PmTracker.Web/wwwroot/css/components/*.css` = 0 všude.

- [ ] **Step 5: Přepsat testy, které hlídaly tmavá pravidla**

`DarkThemeInputScopingTests` se přejmenuje na `NativeInputTokenScopingTests` a zachová smysl: **základní** pravidlo nativních inputů s tokeny musí mít vyloučení gov komponent. Pro každý `[InlineData]` typ:

```csharp
        var pattern = new Regex(
            $@"input\[type=""{Regex.Escape(type)}""\]:not\(gov-form-input input\):not\(gov-form-search input\):not\(\[role=""search""\] input\)[^{{]*\{{[^}}]*background-color:\s*var\(--background-block-primary\)",
            RegexOptions.Singleline);
        pattern.IsMatch(source).Should().BeTrue(
            $"základní pravidlo input[type={type}] s gov tokeny musí vynechat vnitřní input gov-form-input/gov-form-search (bug 2026-04-19)");
```

Úvodní komentář třídy doplnit o větu: „Od 2026-10-15 nejsou tmavá pravidla; motiv přepínají tokeny, scoping se proto hlídá na základním pravidle.“

`GlobalSearchDarkModeCssTests`:
- testy `…NotContain("[data-theme=\"dark\"] .gov-form-search"` a `.gov-form-input__input` ponechat, protože pořád platí;
- testy `Contain("[data-theme=\"dark\"] .app-search-dropdown")` a `…badge--zaznam` přepsat na tvrzení, že `.app-search-dropdown` má `background: var(--background-block-primary)` a `.global-search-item__badge--zaznam` má barvu z gov tokenu, tedy obsahuje `var(--`;
- název třídy a souhrn upravit („…v obou motivech přes gov tokeny“).

- [ ] **Step 6: Spustit a ověřit, že testy prochází**

Run: `dotnet test PmTracker.Tests.Unit 2>&1 | tail -3`
Expected:
- celá Unit sada zelená, včetně `ColorProperties_UseTokensNotLiterals(site.css)`, `DarkThemeSelectors_OnlyInTokensCss` a `TokensCss_DarkBlockOnlyDefinesTokensAndColorScheme`;
- počet testů = 2133 + nové;
- žádné jiné selhání (memory `project_pre_existing_test_failures`).

- [ ] **Step 7: Commit**

```bash
git add -A PmTracker.Web/wwwroot/css PmTracker.Tests.Unit
git commit -m "refactor(barvy): tmavý motiv řídí jen gov tokeny — ruční tmavá pravidla smazána"
```

---

### Task 6: E2E audit palety a kontrastu v obou motivech

Test se píše až teď, protože prochází až po Tascích 2–5. Že umí selhat, se dokáže během na baseline worktree `main` (Step 3).

**Files:**
- Create: `PmTracker.Tests.E2E/TestInfrastructure/ColorPaletteAudit.cs`
- Create: `PmTracker.Tests.E2E/Scenarios/ThemePaletteAuditTests.cs`

**Interfaces:**
- Consumes: `E2ETestFixture.NewPageAsync()`, `.BaseUrl`, `.AdminOsobaId` (existující)

- [ ] **Step 1: Napsat audit**

`ColorPaletteAudit.cs`:

```csharp
namespace PmTracker.Tests.E2E.TestInfrastructure;

/// <summary>
/// Audit barev na vykreslené stránce (2026-10-15): každá barva aplikačního prvku musí být
/// hodnota gov tokenu aktivního motivu nebo aplikačního --app-* tokenu a text musí splnit
/// WCAG kontrast. Přeskakuje vnitřek gov-* komponent (stylují se samy), zmraženou hlavičku
/// a pruhy harmonogramu s barvami z Excelu (inline --seg-color).
/// </summary>
public static class ColorPaletteAudit
{
    public const string Script = """
() => {
  const probe = document.createElement("div");
  document.body.appendChild(probe);
  const norm = (v) => { probe.style.color = ""; probe.style.color = v; return getComputedStyle(probe).color; };
  const parse = (c) => {
    const m = /rgba?\(([^)]+)\)/.exec(c || "");
    if (!m) return null;
    const p = m[1].split(/[ ,/]+/).filter(Boolean).map(Number);
    return { r: p[0], g: p[1], b: p[2], a: p.length > 3 ? p[3] : 1 };
  };
  const rootStyle = getComputedStyle(document.documentElement);
  const palette = [];
  const addToken = (name) => {
    const raw = rootStyle.getPropertyValue(name).trim();
    if (!raw || /^-?[\d.]+(px|rem|em|%|ms|s)?$/.test(raw)) return;
    const c = parse(norm(raw));
    if (c) palette.push(c);
  };
  for (const sheet of document.styleSheets) {
    let rules; try { rules = sheet.cssRules; } catch { continue; }
    const isGov = (sheet.href || "").includes("/assets/gov/styles/");
    const isApp = (sheet.href || "").includes("/css/tokens.css");
    if (!isGov && !isApp) continue;
    const walk = (list) => {
      for (const rule of list) {
        if (rule.cssRules) walk(rule.cssRules);
        if (!rule.style) continue;
        for (let i = 0; i < rule.style.length; i++) {
          const n = rule.style[i];
          if (n.startsWith("--") && (isGov || n.startsWith("--app-"))) addToken(n);
        }
      }
    };
    walk(rules);
  }
  const inPalette = (c) => {
    const p = parse(c);
    if (!p || p.a === 0) return true;
    return palette.some((q) => Math.abs(q.r - p.r) <= 3 && Math.abs(q.g - p.g) <= 3 && Math.abs(q.b - p.b) <= 3 && Math.abs(q.a - p.a) <= 0.02);
  };
  const lum = ({ r, g, b }) => {
    const f = (x) => { x /= 255; return x <= 0.03928 ? x / 12.92 : Math.pow((x + 0.055) / 1.055, 2.4); };
    return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
  };
  const blend = (t, b) => ({ r: t.r * t.a + b.r * (1 - t.a), g: t.g * t.a + b.g * (1 - t.a), b: t.b * t.a + b.b * (1 - t.a), a: 1 });
  const effectiveBg = (el) => {
    const layers = [];
    for (let e = el; e; e = e.parentElement) {
      const p = parse(getComputedStyle(e).backgroundColor);
      if (p && p.a > 0) { layers.push(p); if (p.a >= 0.99) break; }
    }
    let c = parse(getComputedStyle(document.body).backgroundColor) || { r: 255, g: 255, b: 255, a: 1 };
    for (let i = layers.length - 1; i >= 0; i--) c = blend(layers[i], c);
    return c;
  };
  const skipped = (el) => {
    for (let e = el; e; e = e.parentElement) {
      if (e.tagName.startsWith("GOV-") || e.matches("header.gov-header, .gov-header, [style*='--seg-color']")) return true;
    }
    return false;
  };
  const visible = (el) => {
    const r = el.getBoundingClientRect();
    if (r.width < 2 || r.height < 2) return false;
    const cs = getComputedStyle(el);
    return cs.visibility !== "hidden" && cs.display !== "none" && Number(cs.opacity) > 0.05;
  };
  const describe = (el) => el.tagName.toLowerCase() + [...el.classList].slice(0, 2).map((c) => "." + c).join("");
  const off = new Set(), low = new Set();
  for (const el of document.body.querySelectorAll("*")) {
    if (el === probe || ["SCRIPT", "STYLE", "BR", "svg", "path"].includes(el.tagName) || skipped(el) || !visible(el)) continue;
    const cs = getComputedStyle(el);
    const ownText = [...el.childNodes].filter((n) => n.nodeType === 3).map((n) => n.textContent.trim()).join(" ").trim();
    if (!inPalette(cs.backgroundColor)) off.add(`${describe(el)} background ${cs.backgroundColor}`);
    if (ownText && !inPalette(cs.color)) off.add(`${describe(el)} color ${cs.color} „${ownText.slice(0, 30)}“`);
    for (const side of ["Top", "Right", "Bottom", "Left"]) {
      if (parseFloat(cs[`border${side}Width`]) > 0 && cs[`border${side}Style`] !== "none" && !inPalette(cs[`border${side}Color`])) {
        off.add(`${describe(el)} border ${cs[`border${side}Color`]}`);
        break;
      }
    }
    if (ownText && !el.matches(":disabled, [aria-disabled='true'], .is-disabled")) {
      const fg = parse(cs.color), bg = effectiveBg(el);
      const fgc = fg.a < 1 ? blend(fg, bg) : fg;
      const l1 = lum(fgc), l2 = lum(bg);
      const ratio = (Math.max(l1, l2) + 0.05) / (Math.min(l1, l2) + 0.05);
      const size = parseFloat(cs.fontSize), bold = Number(cs.fontWeight) >= 700;
      const need = size >= 24 || (bold && size >= 18.66) ? 3 : 4.5;
      if (ratio < need) low.add(`${describe(el)} ${ratio.toFixed(2)}:1 (${cs.color} na rgb(${bg.r | 0}, ${bg.g | 0}, ${bg.b | 0})) „${ownText.slice(0, 30)}“`);
    }
  }
  probe.remove();
  return { paletteSize: palette.length, off: [...off], low: [...low] };
}
""";

    /// <summary>Třída s vlastnostmi (ne positional record) — tak ji Playwright .NET spolehlivě naplní z JSON.</summary>
    public sealed class Result
    {
        public int PaletteSize { get; set; }
        public string[] Off { get; set; } = [];
        public string[] Low { get; set; } = [];
    }
}
```

`ThemePaletteAuditTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Barvy aplikace = gov tokeny v obou motivech (2026-10-15). Hlavní stránky nesmí mít barvu mimo
/// paletu gov + --app-* a žádný text pod WCAG kontrastem.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class ThemePaletteAuditTests
{
    private readonly E2ETestFixture _fixture;

    public ThemePaletteAuditTests(E2ETestFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task HlavniStranky_BarvyZGovPalety_AKontrastVPoradku(string theme)
    {
        var page = await _fixture.NewPageAsync();
        await page.Context.AddCookiesAsync([new Cookie { Name = "pmtracker.theme.mode", Value = theme, Url = _fixture.BaseUrl }]);
        var asUser = $"asUser={_fixture.AdminOsobaId}";

        var urls = new List<string> { "/", "/Projekty", "/Nastaveni", "/Search?q=a" };
        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty?{asUser}");
        var projectHref = await page.Locator("a[href*='/Projekty/Detail/']").First.GetAttributeAsync("href");
        if (projectHref is not null)
        {
            var projectPath = projectHref.Split('?')[0];
            urls.Add($"{projectPath}?tab=zaznamy");
            urls.Add($"{projectPath}?tab=jednani");
            urls.Add($"{projectPath}?tab=harmonogram");
            await page.GotoAsync($"{_fixture.BaseUrl}{projectPath}?tab=zaznamy&{asUser}", new() { WaitUntil = WaitUntilState.NetworkIdle });
            foreach (var selector in new[] { "a[href*='/Zaznamy/Detail/']", "a[href*='/Zaznamy/Edit']", "a[href*='/Jednani/Detail/']" })
            {
                var href = await page.EvaluateAsync<string?>($"document.querySelector(\"{selector}\")?.getAttribute('href') ?? null");
                if (href is not null)
                {
                    urls.Add(href); // query (např. Edit?id=…) zůstává; asUser se připojí níže
                }
            }
        }

        var findings = new List<string>();
        foreach (var url in urls.Distinct())
        {
            var separator = url.Contains('?') ? '&' : '?';
            await page.GotoAsync($"{_fixture.BaseUrl}{url}{separator}{asUser}", new() { WaitUntil = WaitUntilState.NetworkIdle });
            await page.WaitForTimeoutAsync(500);
            (await page.EvaluateAsync<string>("document.documentElement.getAttribute('data-theme')")).Should().Be(theme);
            var result = await page.EvaluateAsync<ColorPaletteAudit.Result>(ColorPaletteAudit.Script);
            result.PaletteSize.Should().BeGreaterThan(50, "paleta se musí načíst z gov tokens.css");
            findings.AddRange(result.Off.Select(x => $"{url} mimo paletu: {x}"));
            findings.AddRange(result.Low.Select(x => $"{url} kontrast: {x}"));
        }

        findings.Should().BeEmpty($"motiv {theme}: barvy patří do gov tokenů a text musí být čitelný");
        await page.Context.CloseAsync();
    }
}
```

Implementer si ověří, jak `E2ETestFixture` čeká na hydrataci, a sjednotí to s existujícími testy. Odkazy v seedu E2E mohou obsahovat `asUser`, takže před připojením zkontrolovat, že se neobjeví dvakrát. Zjednodušit sběr URL je povolené, pokud se zachová seznam stránek: Přehled, Projekty, 3 záložky projektu, detail záznamu, editace záznamu, detail jednání, Nastavení a Hledání.

- [ ] **Step 2: Build**

Run: `dotnet build PmTracker.Tests.E2E 2>&1 | tail -3`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 3: Doložit, že test umí selhat (baseline `main`)**

Run:

```bash
git worktree add ../pm-baseline-barvy main
cp PmTracker.Tests.E2E/TestInfrastructure/ColorPaletteAudit.cs ../pm-baseline-barvy/PmTracker.Tests.E2E/TestInfrastructure/
cp PmTracker.Tests.E2E/Scenarios/ThemePaletteAuditTests.cs ../pm-baseline-barvy/PmTracker.Tests.E2E/Scenarios/
(cd ../pm-baseline-barvy && dotnet test PmTracker.Tests.E2E --filter "FullyQualifiedName~ThemePaletteAuditTests" 2>&1 | tail -30)
```

Expected:
- oba případy FAIL;
- `dark` vypíše mimo jiné `article.record-card … background rgb(27, 33, 40)` a `kontrast: strong 1.10:1`;
- `light` vypíše například `color rgb(0, 94, 165)`.

Výstup uložit do ledgeru jako důkaz RED. Pak baseline worktree odstranit: `git worktree remove ../pm-baseline-barvy`.

- [ ] **Step 4: Spustit na větvi**

Run: `dotnet test PmTracker.Tests.E2E --filter "FullyQualifiedName~ThemePaletteAuditTests" 2>&1 | tail -30`
Expected: 2/2 PASS.

Pokud test něco najde:
- **barva mimo paletu:** opravit v CSS podle Mapování, každý nález RED→GREEN;
- **kontrast:** opravit tokenem;
- **nález v gov komponentě** (audit ji neměl vidět): opravit filtr `skipped` a zapsat `Ruling:`;
- **nesahat na gov soubory.**

Po opravách znovu Unit sada (`dotnet test PmTracker.Tests.Unit`), musí být zelená.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Tests.E2E/TestInfrastructure/ColorPaletteAudit.cs PmTracker.Tests.E2E/Scenarios/ThemePaletteAuditTests.cs PmTracker.Web/wwwroot/css
git commit -m "test(barvy): E2E audit palety gov a kontrastu v obou motivech"
```

---

### Task 7: Ověření v prohlížeči, celé sady, dokumentace

**Files:**
- Modify: `docs/architecture/tokens.md`, `docs/wiki/projekty/zaznamy/barvy-karet.md:43-50` (sekce Tmavý režim)
- Modify: `docs/changelog/releases/0.11.md`
- Modify: memory `feedback_dark_mode_gov_tokens.md`, `feedback_pm_tokens_aliased_to_gov.md`

- [ ] **Step 1: Ruční průchod v reálném prohlížeči** (memory `feedback_verify_ui_in_real_browser`)

- Spustit app proti lokální DB (příkaz v memory `feedback_esm_module_cache_busting`).
- Spustit vylepšený `scratchpad/dark-audit.cjs` se skriptem `ColorPaletteAudit.Script` v obou motivech přes cookie `pmtracker.theme.mode` na 10 stránkách:
  - Přehled, Projekty, záložky Záznamy / Jednání / Harmonogram;
  - detail záznamu TEST-300 (uzavřený) a 901-17;
  - editace 99022, detail jednání 7, Nastavení, Hledání, StyleGuide.
- Pořídit full-page snímky do scratchpadu a projít je očima (Review Focus 1–5):
  - hlavička v obou motivech stejná jako na `main` (porovnat se snímkem z baseline);
  - karty, inputy, select s chevronem, datum, Quill toolbar, rozbalená karta záznamu, menu akcí, modal, toast, harmonogram (dráha, osa, DNES/TERMÍN, skluz), dashboard dlaždice;
  - hover karty a záložky v tmavém motivu je vidět.
- U uzavřeného TEST-300 jako `asUser=1` (PM) je poznámka vidět. Jako zástupce vedoucího subsystému bez `records.edit` vidět není.

- [ ] **Step 2: Celé sady**

Run: `dotnet test PmTracker.Tests.Unit 2>&1 | tail -2; dotnet test PmTracker.Tests.Api 2>&1 | tail -2; (cd tests/js && npm test 2>&1 | tail -3)`
Expected: Unit vše zelené, Api 453/453 (452 + Task 1), JS 41/41.

Run: `dotnet test PmTracker.Tests.E2E 2>&1 > <scratchpad>/e2e.txt; tail -3 <scratchpad>/e2e.txt`
Expected:
- 33 selhání se **stejnými jmény** jako baseline (memory `project_e2e_baseline_failures`, porovnat Pythonem);
- `ThemePaletteAuditTests` 2/2 PASS;
- nové jméno v selháních = regrese, opravit.

Integration sada se nespouští, protože CSS a view se jí netýkají.

- [ ] **Step 3: Dokumentace**

- **`docs/architecture/tokens.md`:**
  - přepsat úvod: barvy jsou sémantické gov tokeny 4.7.0, aplikační jen `--app-*` v `css/tokens.css`;
  - přidat tabulku „Kdy který token“ (zkrácené Mapování: plocha stránky / bloku, text hlavní / vedlejší / tlumený / odkaz, rámeček, stavy);
  - popsat 4 kontrolní testy a E2E audit;
  - smazat zmínky o `--pm-color-*` a `#111b2d`.
- **`barvy-karet.md` sekce Tmavý režim:** „Barvy používají sémantické tokeny gov DS (`--text-status-*`, `--background-primary` …), které se v tmavém režimu přepnou samy. Fialová Rozhodnutí je aplikační token `--app-category-decision`.“
- **`0.11.md`**, sekce `### Změněno`, kterou je potřeba založit:
  - **Barvy aplikace podle Design systému gov.cz (DS FIS v1.0.0).** Plochy, texty, rámečky a stavové barvy berou hodnoty z gov tokenů. Tmavý režim je tak sladěný s gov komponentami a čitelný: dřív byly například hodnoty Vlastník a Termín v hlavičce záznamu tmavé na tmavém. Ve světlém režimu se mírně změní odstín modré. Hlavička zůstává beze změny.
  - **Poznámku „Záznam je uzavřený…“ uvidí jen ten, kdo smí měnit stav záznamu.**
  - Pokud je 0.11 už nasazená, založit `0.12.md`. Zapsat `Ruling:` s ověřením podle `git tag` nebo podle data posledního `publish.zip`.

- [ ] **Step 4: Memory**

- **`feedback_dark_mode_gov_tokens.md`:**
  - doplnit „Stav 2026-10-15: převedeno (commit …)“;
  - doplnit, kde jsou kontrolní testy (`AppCssColorTokenTests`, `ThemePaletteAuditTests`);
  - zapsat pravidlo „nová barva = gov token; co gov nemá, `--app-*` v tokens.css“.
- **`feedback_pm_tokens_aliased_to_gov.md`:** je zastaralá, protože `--pm-*` barvy už neexistují. Přepsat na odkaz `[[feedback_dark_mode_gov_tokens]]` nebo smazat a upravit řádek v `MEMORY.md`.
- **`MEMORY.md`:** aktualizovat řádek „Tmavý režim = gov DS tokeny“ o stav převodu.

- [ ] **Step 5: Commit**

```bash
git add docs/architecture/tokens.md docs/wiki/projekty/zaznamy/barvy-karet.md docs/changelog/releases/
git commit -m "docs(barvy): tokeny gov jako jediný zdroj barev, changelog"
```

- [ ] **Step 6: Merge do main** (memory `feedback_work_into_main`)

```bash
git -C "/Users/Pavel.Andrlik/Documents/PM Tracker" merge --no-ff feat/barvy-gov-tokeny -m "Merge feat/barvy-gov-tokeny into main"
git worktree remove .claude/worktrees/barvy-gov-tokeny && git branch -d feat/barvy-gov-tokeny
```

Push jen na pokyn uživatele. Finální ruční kontrolu na i15 (Edge) dělá uživatel.

---

## Odhad (hodiny inline Opus 5)

| Task | Odhad |
|---|---|
| 1 Poznámka u uzavřeného záznamu | 0,3 h |
| 2 Aplikační tokeny | 0,3 h |
| 3 Staré proměnné → gov | 1 h |
| 4 Natvrdo zapsané barvy → gov | 2–2,5 h |
| 5 Smazání tmavých pravidel | 1 h |
| 6 E2E audit | 1 h |
| 7 Prohlížeč, sady, dokumentace | 1,5 h |
| **Celkem** | **7–7,5 h** |

Ruční ověření uživatelem na i15 ve světlém i tmavém motivu: zhruba 30 minut.
