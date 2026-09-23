# Samostatná stránka záznamu (varianta 2) — design

**Datum:** 2026-07-14
**Stav:** návrh ke schválení
**Navazuje:** `2026-07-13-zaznam-card-harmonogram-toggle-design.md` (varianta 1 — toggle na kartě, hotová), C1 breadcrumb origin

## 1. Cíl

Samostatná stránka jednoho záznamu s **trvalou URL** (sdílitelný odkaz, více záznamů v záložkách prohlížeče). Rozložení: **bohatá hlavička na plnou šířku** + **dva sloupce** — vlevo vyjádření (jediná editovatelná část), vpravo harmonogram. Harmonogram má ve svém prostoru **přepínač Graf ⇄ Tabulka**; tabulková podoba je read-only varianta té z editoru harmonogramu.

Stránka je jinak **celá read-only** — jediná zapisující akce je přidání/úprava/smazání vyjádření (stávající oprávnění).

## 2. Rozhodnutí uživatele (z diskuse 2026-07-14)

| # | Rozhodnutí |
|---|---|
| U1 | Poměr sloupců **~40 % vyjádření / ~60 % harmonogram**; pod ~1100 px se sloupce **skládají pod sebe** (vyjádření nahoře, harmonogram dole). |
| U2 | Hlavička **bohatá** — název, cíl, popis, vlastník/termín/subsystém/typ **včetně historie** (škrtnuté starší hodnoty), chips externích odkazů, spolupráce. Stránka má být soběstačná pro sdílení odkazem. |
| U3 | Tabulková podoba = **3sloupcová tabulka Krok / Plán (datum) / Skutečnost (datum)** z editoru harmonogramu, celá zamčená. Bez přidaného sloupce odchylky (necháváme takto). |
| U4 | Přepínač Graf ⇄ Tabulka je **v prostoru harmonogramu** (horní část pravého sloupce), ne v hlavičce. |

## 3. Rozložení

### 3.1 Grafický režim (výchozí)

```
+-- <- Projekty > EIS > #901-1 Pripravit podklady...        [Tisk] [Upravit] --+
|  HLAVICKA (plna sirka, read-only)                                            |
|  [Ukol] [Akce]                                          Stav: Rozpracovano   |
|  #901-1  Pripravit podklady pro ridici vybor vcetne harmonogramu etap        |
|  Cil: Zanalyzovat soucasny stav a navrhnout nove reseni...                   |
|  Vlastnik: <s>Jana T.</s> Pavel Admin | Termin: <s>01.07.</s> 13.07.2026     |
|  Subsystem: Integrace | Typ ukolu: Akce                                      |
|  Popis: ...                                                                  |
|  [SD 123456 - Vyzva] [PMP 200034]        Spoluprace: [Jana T.] [Karel V.]    |
+-------------------------------+----------------------------------------------+
|  VYJADRENI          (~40 %)   |  HARMONOGRAM (~60 %)     [*Graf*| Tabulka]   |
|                               |  -- prepinac nahore v prostoru harmonogramu --|
|  Vyjadreni            [raz. v]|  Nestihame                                    |
|                               |  Plan       ####################              |
|  > 12.07  Pavel Admin         |  Skutecnost #####                             |
|    "Podklady pripraveny..."   |  07/26        ^DNES        08/26      09/26   |
|  > 10.07  Jana Testerova      |  Plan: 22.08.2026 | Termin: 13.07.2026        |
|    "Doplnit rozpocet."        |  Stav: aktualni krok 3 - skluz +5 dni         |
|  [ Nacist dalsi ]             |                                               |
|                               |  1. priprava zadani   ###        -16 dnu      |
|  +- Pridat vyjadreni -------+ |  2. konzultace        ###        -9 dnu       |
|  | Jednani: [ vyber v ]     | |  3. odeslani zadani   #####                   |
|  | +----------------------+ | |  4. dodani navrhu       ####                  |
|  | |  text (Quill)        | | |  5. vyporadani            #####               |
|  | +----------------------+ | |  6. ... az 10. nasazeni do provozu            |
|  |             [ Ulozit ]  | |                                               |
|  +--------------------------+ |                                               |
+-------------------------------+----------------------------------------------+

Poradi v levem sloupci = presne jak je dnes v panelu vyjadreni (seznam nahore,
formular pro pridani dole) - panel se pouziva beze zmeny.
```

### 3.2 Tabulkový režim (přepne se jen pravý sloupec)

Hlavička i levý sloupec zůstávají beze změny:

```
|  HARMONOGRAM (~60 %)                        [ Graf |*Tabulka*]|
|  Termin: 13.07.2026 | Plan dokonceni: 22.08.2026 | Trvani 70 d |
|  +--------------------+----------------+-----------------+     |
|  | Krok               | Plan (datum)   | Skutecnost      |     |
|  +--------------------+----------------+-----------------+     |
|  | 1. priprava zadani | 14.06.2026     | 16.06.2026      |     |
|  | 2. konzultace      | 21.06.2026     | 23.06.2026      |     |
|  | 3. odeslani zadani | 28.06.2026     | -               |     |
|  | ...                | ...            | ...             |     |
|  | 10. nasazeni       | 22.08.2026     | -               |     |
|  +--------------------+----------------+-----------------+     |
|  (vse read-only - hodnoty jako text, zadne editovatelne pole)  |
```

### 3.3 Úzká obrazovka (< 1100 px)

Jeden sloupec pod sebou: hlavička → vyjádření → harmonogram (přepínač zůstává nad harmonogramem).

## 4. Znovupoužité komponenty (co se NEpíše znovu)

| Část stránky | Zdroj | Poznámka |
|---|---|---|
| Grafický harmonogram | `_ScheduleBlock` mód `project-readonly` + `BreakdownExpanded = true` | Identické s pohledem na kartě (varianta 1) — pruhy Plán/Skutečnost, osa, souhrn, rozbalený rozpad. |
| Štítek Stíháme/Nestíháme | `_ZaznamSchedulePartial` (varianta 1) | Beze změny. |
| Tabulkový harmonogram | tabulková sekce `_ScheduleBlock` (dnes jen v módu `record-editor`) | Extrahuje se do samostatného partialu — viz §5.3. Zámek přes `ScheduleEditorPermissionSet.ForReadOnly()`, který zamkne všechna datumová pole (render jako text). |
| Vyjádření (seznam + formulář) | `_ZaznamCommentsPartial` | Beze změny, včetně řazení, načítání dalších a oprávnění. |
| Bohatá hlavička | `_ZaznamDetailPartial` (historie, chips, spolupráce) + souhrnná pole z `ZaznamCardSummaryViewModel` | Detail partial se použije uvnitř hlavičky. |
| Drobečky + návrat | `SetProjectBreadcrumbs(..., backUrl: returnUrl)` (C1) | Šipka ← vrací na místo původu. |

## 5. Server

### 5.1 Route a guard

`ZaznamyController` → nová akce:

```
[HttpGet] Detail(int id, string? returnUrl, CancellationToken ct)
```

URL `/Zaznamy/Detail/{id}` (+ volitelně `?returnUrl=`).

Guard (vzor z `Edit`, ale **read** místo edit — pořadí: nejdřív autorizace, pak těžké dotazy):
1. `projektId` lookup podle `id`; `null` → `NotFound()`.
2. `CurrentUserContext.CanAccessProject(projektId)` → jinak `NotFound()` (bez existence leaku).
3. Žádné další právo se nevyžaduje — stránka zobrazuje totéž, co uživatel vidí na kartě v záložce Záznamy.

Akční tlačítka v hlavičce se gatují stejně jako na kartě: **Tisk** vždy, **Upravit** jen při `RecordEditorAffordancePolicy.CanOpenEditor` (server i UI ze stejné funkce), **Navrhnout změnu harmonogramu** při `ProposalsScheduleCreate`.

### 5.2 View model a kompozice

Nový `ZaznamDetailPageViewModel` (Models/ViewModels/Projekty):

```
int ProjektId; string ProjektZkratka; string ProjektNazev;
ZaznamCardSummaryViewModel Summary;        // hlavicka - cislo, nazev, cil, stav, kategorie, typ
ZaznamCardDetailViewModel Detail;          // historie, popis, chips, spoluprace
ZaznamCommentsPanelViewModel Comments;     // levy sloupec
ZaznamScheduleBlockViewModel? Schedule;    // graficky blok (null = zaznam nema harmonogram)
HarmonogramBlockViewModel? ScheduleTable;  // tabulkovy blok (null = totez)
bool CanEditRecord; bool CanCreateScheduleProposal;
string? EditUrl; string? ScheduleProposalUrl; string? PrintPdfUrl; string? PrintWordUrl;
string? BackUrl;
```

Composition metoda `BuildRecordDetailPageAsync(projektId, recordId, ct)` v `ProjectService` (nový partial `ProjectService.RecordDetailPage.cs`), delegace přes `IRecordService` — **stejný vzor jako `BuildRecordScheduleBlockAsync`** z varianty 1. Skládá existující buildery: `BuildRecordCardShellAsync`, `BuildRecordCardDetailAsync`, `BuildRecordCommentsPanelAsync`, `BuildRecordScheduleBlockAsync`.

`ScheduleTable` = tentýž `HarmonogramBlockViewModel` jako grafický, jen `with { Mode = "record-editor", Permissions = ScheduleEditorPermissionSet.ForReadOnly(), CanEditManualActual = false }`. `ForReadOnly()` má `IsTaskCategory = false`, což v tabulce zamkne plánová i skutečnostní datumová pole → renderují se jako text.

### 5.3 Extrakce tabulky (nutná dekompozice)

Tabulková sekce dnes žije uvnitř `_ScheduleBlock.cshtml` v `@if (isEditor)` větvi spolu s mini-ganttem a souhrnem. Stránka potřebuje **jen tabulku** (graf má vlastní režim).

**Řešení:** čistý přesun tabulkové sekce (`<div class="schedule-table-wrap">…`, včetně lokálního čítače `manualInputIndex` a všech větví manual/auto buněk) do nového partialu `Views/Shared/_ScheduleTable.cshtml` s modelem `HarmonogramBlockViewModel`. `_ScheduleBlock` ho v editor větvi zavolá; nová stránka ho volá přímo.

Výstup editoru se **nesmí změnit** — jde o přesun, ne úpravu. Pojistka: existující editor testy (mini-gantt, manual buňky, proposal diff) musí zůstat zelené + nový test, že `_ScheduleBlock` v editor módu tabulku pořád renderuje.

## 6. Přepínač Graf ⇄ Tabulka

- **Umístění:** pravý horní roh pravého sloupce, uvnitř hlavičky harmonogramu (`<h2>Harmonogram</h2>` + přepínač na stejném řádku).
- **Podoba:** dvoustavový segmentovaný přepínač (dvě tlačítka „Graf" / „Tabulka", aktivní zvýrazněné, `role="group"`, `aria-pressed`). **Ne `gov-form-switch`** — přepínáme mezi dvěma pojmenovanými pohledy, ne zapnuto/vypnuto; a vyhneme se známým gov-host omezením.
- **Mechanika:** obě podoby jsou v DOMu; přepínač jen mění třídu na kontejneru harmonogramu (`.record-page-schedule--table`), CSS skryje neaktivní. Žádný server round-trip, žádný lazy-load (stránka je serverem vyrenderovaná celá).
- **Kreslení os:** grafická podoba se vykresluje serverovými ticky (jako na kartě). Při prvním přepnutí zpět na graf se osy překreslí (`renderStaticTimelineAxes`), protože ve skrytém stavu mají nulovou šířku.
- **Zapamatování:** poloha se ukládá do `localStorage` (`pmtracker.recordPage.scheduleView` = `graph` | `table`) a aplikuje se při načtení stránky. Výchozí = graf.
- **Stav UI** řídí třída na kontejneru + `aria-pressed` na tlačítkách; rotace/zvýraznění čistě CSS (viz lekce `feedback_pm_button_strips_host_attributes`).

## 7. Vyjádření — vazba na obnovu (kritické)

Ukládání/úprava/smazání vyjádření běží přes AJAX, který po akci hledá `.record-card[data-record-id]` a v ní `[data-record-comments-shell]`. Aby ukládání fungovalo **beze změny obnovovacího mechanismu**, levý sloupec se obalí do:

```html
<article class="record-card record-card--page-column" data-record-id="@Model.Summary.Id"
         data-record-comments-url="..." data-record-comments-base-url="..."
         data-record-comments-loaded="true">
  <div class="record-body"> …_ZaznamCommentsPartial… </div>
</article>
```

Podmínky: wrapper **nesmí** mít třídu `collapsed` (skryla by tělo) a **nemá** `[data-record-toggle]` hlavičku (na stránce se nic nesbaluje). Vzhled karty resetuje `.record-card--page-column` (bez rámečku, bez barevného proužku, bez vlastního odsazení) — sloupec má vlastní kartu.

Refresh scope zůstává `record-comments` (nahrazuje jen obsah shellu, ne celou kartu) — tím se stránka nerozbije.

## 8. Vstupní bod

Do svislého menu akcí na kartě záznamu (varianta 1) se přidá položka **„Otevřít na nové kartě"** — odkaz na `/Zaznamy/Detail/{id}?returnUrl=<aktuální URL>` s `target="_blank" rel="noopener"`. Položka je viditelná vždy (stránka nevyžaduje zvláštní právo nad rámec přístupu k projektu).

## 9. Omezení a chyby

- **i15 pravidla** (`feedback_i15_edge_css_compat`): dvousloupec jen letitým flex/grid základem, žádné `:has`, žádné `max-content`/intrinsic keywords; skládání pod sebe přes `@media (max-width: 1100px)`.
- **Záznam bez harmonogramu** (žádná vyplněná hodnota kroku): pravý sloupec se nerenderuje vůbec, vyjádření zaberou plnou šířku; přepínač se nezobrazí.
- **Neexistující záznam / bez přístupu:** `NotFound()`.
- **Zastarání obsahu:** stránka je statický snapshot; po uložení vyjádření se obnoví jen panel vyjádření. Harmonogram se aktualizuje až reloadem — vědomé omezení (na stránce se harmonogram needituje).

## 10. Testy

- **Unit (markup/CSS piny):** dvousloupcový layout bez `:has`/intrinsic keywords; `@media (max-width: 1100px)` stohování; přepínač jako dvě tlačítka s `aria-pressed` (ne gov-form-switch); `.record-card--page-column` resetuje vzhled a nemá `collapsed`.
- **Api render:** stránka vrací 200 a obsahuje hlavičku (číslo, název, historii, chips), oba harmonogramové bloky (grafický `data-schedule-ticks` + tabulku `schedule-table-wrap`), panel vyjádření; tabulka je **read-only** (žádný `<input>`/`<select>` mimo formulář vyjádření); záznam bez harmonogramu nerenderuje pravý sloupec ani přepínač; bez přístupu → 404; „Upravit" se zobrazí jen s oprávněním.
- **Regrese extrakce tabulky:** editor záznamu (`EditZaznamPage`) i detail návrhu renderují tabulku beze změny (existující testy zelené + nový pin, že `_ScheduleBlock` v editor módu tabulku obsahuje).
- **E2E (Playwright):** otevření stránky z menu karty; přepnutí Graf → Tabulka a zpět (obě podoby, osy po návratu lícují); poloha přepínače přežije reload (localStorage); přidání vyjádření na stránce uloží a obnoví panel; šipka ← vrací na místo původu. Interakce dle `feedback_playwright_gov_component_interaction`.
- **Živě:** Playwright 1470×956 + 2560×1440 — poměr sloupců, stohování pod 1100 px, screenshoty pro ruční ověření na i15.

## 11. Mimo rozsah

- Editace harmonogramu ani metadat záznamu na této stránce (vede se přes „Upravit" do editoru).
- Sloupec odchylky/stavu kroku v tabulce (U3 — necháváme dnešní 3 sloupce).
- Živá aktualizace obsahu bez reloadu.
