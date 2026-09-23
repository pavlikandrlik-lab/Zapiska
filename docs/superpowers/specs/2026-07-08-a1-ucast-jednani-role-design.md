# A1 — Účast v jednání: konsolidace + role účastníka v projektu

**Datum:** 2026-07-08 · **Stav:** schváleno uživatelem (analýza 2026-07-08)

## Problém a kontext okolí

Detail jednání ([Views/Jednani/Detail.cshtml](../../../PmTracker.Web/Views/Jednani/Detail.cshtml), karta „Účast") zobrazuje tabulku pouze se sloupci **Osoba** (jméno + e-mail) a **Stav účasti** (select). Tabulka je jednosloupcová na plnou šířku karty — na velkém monitoru plýtvá místem (dlouhý svislý seznam), role účastníka v projektu není vidět vůbec.

Okolí:
- `UcastViewModel` (`Models/ViewModels/JednaniViewModels.cs:57`): `OsobaId, Osoba, Email, StavUcastiKod, StavUcasti` — **žádné role**.
- Server data o rolích **existují**: `MeetingService.DetailQueries.cs` → `BuildActiveProjectMembershipRowsAsync(projectId)` vrací per osoba `AktivniRole` (list labelů: projektová role názvem, subsystémová jako `„Role (SUBSYSTEM_KOD)"` přes `BuildSubsystemRoleLabel`). Volá se už dnes v témže flow (kandidáti modalu „Přidat osobu" + legacy attendance fallback).
- CSS: `.meeting-attendance-panel { display:grid; gap:12px }`, `.meeting-attendance-table td:last-child { width:240px }` (site.css ~5499–5516).
- Save flow: `SaveAttendance` čte `rows[i].OsobaId` + `rows[i].StavUcasti` z formuláře — indexy řádků musí zůstat souvislé i ve dvousloupcovém layoutu (jeden `<form>`, jedna posloupnost indexů).

## Řešení

1. **Data:** `UcastViewModel` + `IReadOnlyList<string> AktivniRole` (default `[]`). V `BuildMeetingAttendanceAsync` + `BuildLegacyMeetingAttendanceAsync` naplnit z `BuildActiveProjectMembershipRowsAsync` (join přes OsobaId; osoba mimo aktivní tým = prázdný list). Žádný nový SQL dotaz — membership rows se v `BuildJednaniDetailAsync` už stejně staví pro kandidáty; předat je dál (jedno zavolání, sdílený výsledek).
2. **UI:** role jako **šedý podtext pod jménem** (druhý řádek buňky Osoba, třída `muted`, labely oddělené `·`). Žádný nový sloupec — zachová šířku pro 13".
3. **Responzivní 2 sloupce:** `<table>` nahradit grid layoutem `.meeting-attendance-grid` s pravidlem `grid-template-columns: repeat(auto-fill, minmax(480px, 1fr))`. Každý řádek (osoba + role + stav-select) je jeden grid item („řádková karta"): na širokém monitoru se poskládají 2 vedle sebe, na 13" (1280/1440) zůstane 1 sloupec — bez media queries, řídí to šířka. Hlavičku tabulky nahradí struktura řádku (jméno tučně, select s `aria-label="Stav účasti"`).
   - Formulářové indexy `rows[@i]` zůstávají v pořadí renderu — souvislé, layout je čistě vizuální.
4. Souhrn v hlavičce karty (`attendanceSummaryText`) beze změny.

## Dotčené soubory
- `PmTracker.Web/Models/ViewModels/JednaniViewModels.cs` — `UcastViewModel.AktivniRole`
- `PmTracker.Web/Services/MeetingService.DetailQueries.cs` — naplnění rolí (sdílené membership rows)
- `PmTracker.Web/Views/Jednani/Detail.cshtml` — markup panelu účasti (grid řádků místo `<table>`)
- `PmTracker.Web/wwwroot/css/site.css` — `.meeting-attendance-grid` + breakpoint

## Akceptační kritéria
- U každého účastníka viditelné jeho aktivní role v projektu (projektové i subsystémové labely); bez rolí → jen jméno.
- Viewport 1440×900 (13"): 1 sloupec, nic nepřetéká; ≥ ~1900px: 2 sloupce vedle sebe.
- `SaveAttendance` funguje beze změny (indexy souvislé) — ověřit Api testem POST.
- Toggle „Zobrazit účast" (fix 2026-07-08) funguje dál.

## Testy
- Unit (source-assertion): Detail.cshtml renderuje `AktivniRole`; VM má pole.
- Api render: detail jednání obsahuje roli osoby (seed osoba s projektovou rolí).
- E2E/Playwright měření: 2 sloupce na 1920, 1 sloupec na 1280.

## Mimo scope
Editace rolí z tohoto panelu; změny modalu „Přidat osobu".
