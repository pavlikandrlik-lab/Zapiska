# Fáze 2D — Baseline audit

**Datum:** 2026-04-19
**Větev:** `codex/senior-refactor-fase-1`
**HEAD před startem 2D:** `7d46c94` (docs plánu 2D), předchozí implementační commit `a8e9600`.

## Stav testů na `main` (baseline referenční)

- `PmTracker.Tests.Api`: **54/54 pass** (jen stávající minimum)
- `PmTracker.Tests.Unit`, `PmTracker.Web.Tests`: neexistují na main (feature větev je přidala)

## Stav testů na feature větvi (před 2D)

| Projekt | Pass | Fail | Pozn. |
|---|---|---|---|
| `PmTracker.Web.Tests` | 89 | 0 | pm-* TagHelper unit testy |
| `PmTracker.Tests.Unit` | 322 | 0 | zahrnuje taghelper + MVC unit testy |
| `PmTracker.Tests.Api` | 276 | **11** | integrační testy controllerů — 11 je **pre-existing red** |
| `PmTracker.Tests.E2E` | — | — | Playwright, vyžaduje browser |
| `PmTracker.Tests.Integration` | — | — | vyžaduje SQL Server |

**Celkem unit + api (bez E2E, bez Integration): 687 pass / 11 fail.**

## 11 pre-existing Api failů (nejsou regrese Fáze 2)

Fáze 2 (A/B/C) tyto testy neupravovala ani nerozbila. Failují kvůli nesouladu mezi napsanými expecty a reálným HTML výstupem současného `_Layout.cshtml` / controllers:

1. `GlobalSearchLayoutTests.Layout_ShouldRenderSearchSubmitButton` — očekává `class="gov-button"` a `type="submit"` v light DOM, gov 4.2.9 Web Components je rendruje v shadow DOM
2. `GlobalSearchLayoutTests.Layout_ShouldRenderSearchInputWithAccessibleLabel` — očekává `gov-form-input__input` BEM třídu + `aria-label="Globální vyhledávání"`; layout má `placeholder="Hledání"` bez aria-label
3. `DocumentationNavigationTests.Layout_ShouldNotRenderThemeCycleMenuAction` — neznámá příčina (pravděpodobně layout menu diff)
4. `CiselnikyControllerTests.SaveRow_ShouldRedirectWithTempData_WhenModelIsInvalid_ForNonAjaxRequest`
5. `CiselnikyControllerTests.DeleteRow_ShouldRedirectWithTempData_WhenRowDoesNotExist_ForNonAjaxRequest`
6. `PeopleControllerTests.Delete_ShouldRedirectAndShowTempDataError_WhenRequestIsNotAjaxAndModelIsInvalid`
7. `PeopleControllerTests.SaveAd_ShouldRedirectAndShowTempDataError_WhenRequestIsNotAjaxAndModelIsInvalid`
8. `SettingsAuthzAdminControllerTests.SaveRole_ShouldRedirectWithTempDataError_WhenRequestIsNotAjaxAndModelIsInvalid`
9. `SettingsAuthzAdminControllerTests.SaveRolePermission_ShouldRedirectWithTempDataError_WhenRequestIsNotAjaxAndMappingIsDuplicate`
10. `SuggestEndpointTests.Suggest_HitMaSpravnePoloZky`
11. `SuggestEndpointTests.Suggest_VyhledaZaznam_DleNazvu`

### Princip během 2D

- **Baseline** = 11 failů (tyto)
- **Akceptační kritérium** = po každém commitu 2D NESMÍ být víc než 11 failů v Api; pokud se některý test *změní z red na red s jiným výstupem*, zkontrolovat
- **GlobalSearchLayoutTests** pozor: Task 6 (Layout) a Task 5 (search) mohou výstup změnit. Po dokončení přehodnotit. Pokud 2D konečně layout dotáhne, některé testy můžou zezelenat (bonus), některé nové fails (musí se zdůvodnit).

## Počty výskytů v Views před migrací

- `class="btn` v produkčních Views (mimo StyleGuide): 103 výskytů ve 27 souborech
- `<input type="search">`: 12 výskytů ve 12 souborech (9 je person-picker → NEmigrujeme)
- `<gov-button>` v produkčních Views (mimo StyleGuide): 2 výskyty (_Layout.cshtml, slot children v gov-form-search)
- `modal-*` CSS / `data-modal-*`: 33 souborů (→ odloženo na 2E)

## pm-* TagHelpery před 2D (21)

Fáze 1: pm-button, pm-field, pm-badge, pm-icon
Fáze 2A: pm-select, pm-textarea, pm-checkbox, pm-radio + pm-radio-group, pm-switch
Fáze 2B: pm-link, pm-tabs + pm-tabs-item, pm-card, pm-pagination
Fáze 2C: pm-dialog, pm-tooltip, pm-toast, pm-skeleton, pm-loading

## Známé odchylky od plánu

- **PmTracker.Web.Tests**: 89 testů, ne 95 jak uvádí pre-compact summary (drobná nepřesnost v plánu — realita vítězí, nepřepisovat plán)
- **Tests.Unit**: 322 (sedí s plánem)
