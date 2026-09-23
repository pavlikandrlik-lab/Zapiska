# A4 — Doplnění breadcrumbs na stránky, kde chybí

**Datum:** 2026-07-08 · **Stav:** schváleno uživatelem (analýza 2026-07-08)

## Problém a kontext okolí

Drobečková lišta se renderuje z `ViewData["Breadcrumbs"]` (layout → `_BreadcrumbBar`); nastavuje ji `BaseController` helpery (`SetProjectBreadcrumbs`, `SetSectionRootBreadcrumb`, `SetSectionChildBreadcrumb` — [BaseController.Breadcrumbs.cs](../../../PmTracker.Web/Controllers/BaseController.Breadcrumbs.cs)). Audit všech controllerů (statický grep + live curl):

**Chybí (doplnit):**
| Akce | Stránka | Vzor |
|---|---|---|
| `NavrhyController.CreateRecordProposal` | Nový návrh záznamu (EditZaznamPage) | `SetProjectBreadcrumbs(..., currentText: "Nový návrh záznamu")` |
| `NavrhyController.CreateScheduleProposal` | Návrh změny harmonogramu | `currentText: "Návrh změny harmonogramu"` |
| `NavrhyController.ProposalDetail` | Detail/schválení návrhu (user report) | `currentText: "Schválení návrhu"`; pokud VM nese číslo návrhu, použít `$"Schválení návrhu #{číslo}"` |
| `NavrhyController.PrefillCreateProposal` | Převzetí návrhu | `currentText: "Převzetí návrhu"` |
| `SearchController.Index` | Výsledky hledání | `SetSectionRootBreadcrumb("Hledání")` (kořen sekce — hledání nemá rodiče) |
| `SDConnectorController.Index`, `.Inspect` | SD konektor (admin) | `SetSectionRootBreadcrumb("SD konektor")` / `SetSectionChildBreadcrumb("SD konektor", …, "Inspekce")` |

**Ověřeno OK (nedotýkat):** Jednání index i detail, Zaznamy Create/Edit, Projekty, Dashboard vč. podstránek, ProjectDashboard (fix 2026-07-07), Osoby, Číselníky, Nastavení, Profil, Dokumentace. Export šablony = tiskový layout, záměrně bez. Modaly breadcrumbs nemají (nejsou stránky).

Klíčový kontext: Navrhy akce vrací **stejný** `ZaznamEditViewModel` a stejnou view `EditZaznamPage.cshtml` jako `ZaznamyController.Create/Edit`, které breadcrumbs **mají** (ZaznamyController.cs:87, :113) — model nese `ProjektId/ProjektNazev/ProjektZkratka`. Jde o prosté doplnění stejného volání po `PrepareProposalEditorModel`.

`SetProjectBreadcrumbs` zachovává `?asUser` (dev impersonace) — nic dalšího netřeba.

## Řešení
Doplnit volání dle tabulky. Texty `currentText` finalizovat při implementaci podle terminologie UI (tlačítka: „Nový návrh záznamu", „Schválit návrh"…) — držet názvosloví, které user vidí na tlačítkách, jimiž na stránku přišel.

## Dotčené soubory
- `PmTracker.Web/Controllers/NavrhyController.cs` (4 akce)
- `PmTracker.Web/Controllers/SearchController.cs` (1)
- `PmTracker.Web/Controllers/SDConnectorController.cs` (2)

## Akceptační kritéria
- Všech 7 stránek renderuje `.app-breadcrumb-bar`; návrhové stránky ukazují `Projekty › [projekt | ZKRATKA] › [aktuální]` s funkčním ← i ✕.
- `?asUser` se propaguje v odkazech drobečků.

## Testy
- Api render testy: každá stránka obsahuje `app-breadcrumb-bar` + očekávaný current text (Navrhy stránky vyžadují seed návrhu pro ProposalDetail — pokud fixture neumí, pokrýt aspoň Create/Prefill + Search).
- Unit source-assertion: NavrhyController volá `SetProjectBreadcrumbs` ve 4 akcích.

## Mimo scope
HomeController.Error (chybová stránka bez navigace) — záměrně bez drobečků.
