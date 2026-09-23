# B6 — Účast jednání: řazení podle rolí ve skupinách, uvnitř příjmení+jméno

**Datum:** 2026-07-09 · **Stav:** schváleno uživatelem (analýza 2026-07-09)

## Problém a kontext okolí
Uživatel chce pořadí účastníků podle projektových rolí; dosavadní řazení (naposledy příjmení+jméno z 2026-07-09 ráno) nahrazuje toto zadání. Zdroj dat: `MeetingService.DetailQueries.BuildActiveProjectMembershipRowsAsync` — projektová větev dotazu už nese `RoleKod` (`ciselnik_roli_projektu`: `VLASTNIK_PROJEKTU`, `GEST`, `PROJ_MAN`, `ADM_PROJ`, `HOST`, `ANALYTIK`, `DEV`), subsystémová větev role kódy subsystémové.

## Řešení

**Priorita skupin** (menší = dřív):
| Priorita | Skupina | Podmínka |
|---|---|---|
| 1 | Vlastník projektu | projektová role kód `VLASTNIK_PROJEKTU` |
| 2 | Gestor | `GEST` |
| 3 | Projektový manažer | `PROJ_MAN` |
| 4 | Administrátor projektu | `ADM_PROJ` |
| 5 | Zbytek | vše ostatní (HOST/ANALYTIK/DEV, čistě subsystémové role, osoby mimo aktivní tým s explicitní účastí) |

- Osoba s více rolemi → **minimum** (nejvyšší skupina).
- Uvnitř skupiny: **Prijmeni → Jmeno** (CurrentCulture, jako dosud); dál nerozhodujeme (user: „je to jedno").
- **Jen pořadí, žádné vizuální nadpisy skupin** (doporučení z analýzy; user nerozporoval — snadno rozšiřitelné později).

**Implementace:** `ActiveProjectMembershipRow` + `int GroupPriority` (spočtené při group-by z projektových RoleKod fragmentů; subsystem-only fragmenty nepřispívají → 5). Aplikace na VŠECH místech řazení osob jednání (sourozenci):
1. legacy fallback účasti (`BuildLegacyMeetingAttendance`),
2. normální účast (explicitní Ucast řádky — priorita z membership lookup, fallback 5 pro osoby mimo rows),
3. kandidáti modalu „Přidat osobu" (obě overloady konzumují rows → pořadí rows).
Řazení rows: `GroupPriority → Prijmeni → Jmeno` (jediné místo — rows; ucast normal path řadí vlastním klíčem se stejnou trojicí).

## Dotčené soubory
- `PmTracker.Web/Services/MeetingService.DetailQueries.cs`

## Akceptační kritéria (Integration)
Seed: osoba A=ADM_PROJ (příjmení „Bílý"), B=VLASTNIK_PROJEKTU („Žlutý"), C=PROJ_MAN („Adamec"), D=jen subsystémová role („Aaron"), E=ADM_PROJ („Adam") ⇒ pořadí účasti i kandidátů: **Žlutý(1), Adamec(3), Adam(4), Bílý(4), Aaron(5)** — skupina přebíjí příjmení, uvnitř skupiny 4 řadí příjmení.
Nahrazuje assert z `BuildJednaniDetail_OrdersUcastAndCandidates_BySurnameThenGivenName` (test se přepíše na skupinové zadání).

## Testy
- Integration: scénář výše (účast legacy + kandidáti); + explicitní Ucast řádky varianta.
- Živě: dev seed (Jana=PM+lead, Karel=DEV, Marie=ANALYTIK, Pavel=PM) ⇒ Jana/ Pavel (PM, dle příjmení Admin<Testerová → Pavel, Jana), pak Karel+Marie dle příjmení.

## Mimo scope
Vizuální group headery; řazení jinde v aplikaci (osoby, tým).
