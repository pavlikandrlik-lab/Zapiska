# Záznam: hlavička, detaily v tabulce, procházení šipkami, mazání data plánu — specifikace

**Datum:** 2026-10-22
**Stav:** schváleno 2026-10-22 (R1–R14); plán implementace `docs/superpowers/plans/2026-10-22-zaznam-hlavicka-detaily-sipky-plan.md`
**Komponenty:** karta záznamu (`Views/Projekty/_ZaznamPartial.cshtml`), detaily záznamu (`_ZaznamDetailPartial.cshtml`), stránka záznamu (`ZaznamDetailPage.cshtml`), detail jednání (záložka Záznamy v kontextu jednání), harmonogram v editoru a v návrhu změny (`Views/Shared/_ScheduleTable.cshtml`)

## 1. Zadání (uživatel, 2026-10-22)

> 1. Hlavičku záznamu zarovnat po levé straně, nyní není zarovnaná levá strana, je tam odskok.
> 2. Detaily záznamu zkusit dát do tabulky, aspoň dva sloupečky, cílem je konsolidace prostoru.
> 3. Přepínání mezi záznamy na celou obrazovku pomocí šipky, vstup z jednání z detailu úkolu. V normálních záznamech to může být také, ale v jednání to prochází všechny záznamy postupně od shora dolů, jak jsou seřazeny. V jednání chci ale vidět pouze záznamy, které do jednání patří, tzn. záznamy, které se objeví v zápisu. V záznamech jako hlavních záznamech: a) procházet jen aktivní záznamy, b) neumožnit procházení.
> 4. Nelze odstranit datum z plánu v harmonogramu.

Doplněno v rozhovoru: „chci, aby se mezi kartami dalo přecházet šipkami na klávesnici, šipkami vpravo a vlevo“.

Čtyři části (A–D) jsou na sobě nezávislé a jdou implementovat a otestovat každá zvlášť.

## 2. Současný stav (ověřeno 2026-10-22 v prohlížeči a v kódu)

### A. Hlavička karty
Karta záznamu (záložka Záznamy projektu i detail jednání — jedna sdílená komponenta) má vlevo v hlavičce indikátor rozbalení `›`/`˅` (`.record-expand-indicator`, ve flexu hlavičky). Kvůli němu štítky, nadpis, cíl a řádek „Termín · Založeno“ začínají na **x = 88 px**, kdežto detaily a vyjádření pod nimi na **x = 67 px** — odskok 21 px. Stránka záznamu (`/Zaznamy/Detail/{id}`) indikátor nemá a je zarovnaná správně (vše na x = 45 px).

### B. Detaily
`_ZaznamDetailPartial` vypisuje Vlastník, Termín, Subsystém a Typ úkolu **pod sebou, každý na vlastním řádku** (`.record-history > div`), hodnota hned za popiskem — hodnoty nejsou ve společném sloupci. Na kartě je navíc Termín dvakrát (řádek „Termín · Založeno“ v hlavičce i řádek v detailech). Stránka záznamu má „Založeno:“ jako samostatný řádek nad detaily.

### C. Procházení a záznamy jednání
- Šipkové procházení neexistuje. Na stránku záznamu vede jen menu **›** na kartě (vpravo nahoře, třetí tlačítko za tiskem a tužkou) → „Otevřít na nové kartě“ (vždy nová karta prohlížeče).
- Detail jednání (plán 2026-10-21) ukazuje **všechny záznamy projektu**; filtry a řazení běží v prohlížeči (karty se skrývají, skupiny subsystémů řadí JS).
- Které záznamy „patří do jednání“, už definuje **tisk zápisu** (`ExportRecordVisibilityEvaluator.IsVisibleForMeetingPrint`): záznam založený nejpozději v den jednání **a** ke dni jednání neuzavřený, **nebo** uzavřený mezi minulým a tímto jednáním (stav se počítá ke dni jednání z historie stavů, rozhoduje datum platnosti). Zápis řadí: skupiny subsystémů v pořadí projektu → kategorie (Informace, Rozhodnutí, Úkol, ostatní) → viditelné číslo (`RecordDisplayOrdering`).
- Záznam založený z jednání („Nový záznam“ v pruhu jednání) dostane datum založení = datum jednání (`RecordService.EditorQueries`), takže do zápisu patří.

### D. Datum plánu
- Pole plánu (`pm-date-field`, `HarmonogramHodnoty[i].PlanDatum` v `_ScheduleTable.cshtml`) má text **jen pro čtení** (datum jde vybrat jen kalendářem) a **nemá tlačítko ✕** — `clearable` je volitelné a pole plánu ho nezapíná. Pole skutečnosti ho zapnuté má (`_ScheduleBlockManualCellInput`). Reprodukováno v editoru záznamu 99009: `clearable="false"`, žádné `[data-app-date-clear]`.
- Server prázdný plán uložit umí (`RecordService.SaveRecord`: `row.PlanDatum = null`), validátor chronologie prázdné kroky přeskakuje.
- **Návrh změny harmonogramu prázdná data zahazuje** (`RecordProposalPayloadMapper.BuildSchedulePayload`: `.Where(v => v.PlanDatum.HasValue)`) a přehled změn je nezobrazí (`RecordProposalService.Queries`: `if (!planned.PlanDatum.HasValue) continue`). Schválení prázdné datum zapsat umí (`DecisionCommands`: `row.PlanDatum = pv.PlanDatum?.Date`).

## 3. Rozhodnutí

| # | Rozhodnutí | Zdroj |
|---|---|---|
| R1 | Indikátor rozbalení karty se přesune **do levého okraje karty** (mezi červený pruh a obsah). Štítky, nadpis, cíl, detaily i vyjádření začínají na jedné svislé čáře. Šířka obsahu se nemění. | uživatel |
| R2 | Detaily jsou v **tabulce se dvěma dvojicemi popisek \| hodnota na řádek**: Vlastník \| Subsystém, Termín \| Typ úkolu, Založeno. | uživatel |
| R3 | **Bez duplicit:** na rozbalené kartě zmizí z hlavičky řádek „Termín · Založeno“ (je v tabulce); na sbalené kartě zůstane. Na stránce záznamu se samostatné „Založeno:“ přesune do tabulky. | uživatel |
| R4 | Historie (přeškrtnuté staré hodnoty vlastníka, termínu, subsystému, typu) zůstane v buňce hodnoty. Popis, štítky externích odkazů a Spolupráce zůstanou pod tabulkou beze změny. | doplněno |
| R5 | **Do jednání patří** přesně záznamy ze zápisu (pravidlo z 2.C). Pravidlo se vyčlení do **jedné sdílené části** pro tisk zápisu, seznam v detailu jednání i šipky — žádná druhá kopie. | uživatel |
| R6 | **Detail jednání** (otevřené / v přípravě) ukáže v záznamech **jen záznamy jednání**. Filtry dál fungují uvnitř nich. | uživatel |
| R7 | **Šipky v jednání:** vždy **všechny záznamy jednání v pořadí zápisu** (subsystém v pořadí projektu → kategorie → číslo), **bez ohledu na filtry** na stránce. | uživatel |
| R8 | **Šipky ze záložky Záznamy projektu:** jen **aktivní** záznamy (bez stavu, nebo stav není konečný — stejné pravidlo jako filtr „Aktivní“), stejné pořadí, filtry se neuplatní (konzistentně s R7). | uživatel (varianta a) |
| R9 | **Vstup:** v menu **›** na kartě přibude jako první položka **„Otevřít na celou obrazovku“** — stránka záznamu ve **stejné** kartě prohlížeče. „Otevřít na nové kartě“ zůstane. Obě nesou seznam pro šipky (R7/R8). | uživatel |
| R10 | **Ovládání:** na stránce záznamu „‹ Předchozí · 3 / 17 · Další ›“ a **klávesy ← a →** (hlavní ovládání). Klávesy nereagují při psaní do pole, výběru, editoru vyjádření ani v otevřeném okně. Na krajích seznamu je šipka neaktivní (nepřetáčí se). | uživatel |
| R11 | Seznam pro šipky **počítá server** z parametru v adrese — šipky fungují i v nové kartě, po obnovení stránky i z odkazu. Návrat (drobečková lišta) vede tam, odkud jsem přišel (jednání / záložka Záznamy) i po libovolném počtu kroků. | doplněno (plyne z R7) |
| R12 | Záznam, který do seznamu nepatří (např. uzavřený otevřený ze záložky Záznamy), šipky nedostane. | doplněno |
| R13 | Pole plánu dostane **✕ (smazat datum)**, pokud není zamčené — v editoru záznamu i v návrhu změny harmonogramu. | uživatel |
| R14 | **Návrh změny harmonogramu nese i odstranění plánu**; při rozhodování se ukáže „Plán: 13.07.2026 → bez plánu“, po schválení se plán smaže. | uživatel |

## 4. Návrh

### 4.A Hlavička karty (R1)

- `.record-expand-indicator` přestane zabírat místo ve flexu hlavičky: absolutně umístěný v levém okraji `.record-content` (karta má vlevo od obsahu dost místa — dnes od červeného pruhu po obsah ~34 px), svisle u řádku se štítky. Klik na hlavičku rozbaluje beze změny (indikátor je dál uvnitř `[data-record-toggle]`).
- Výsledek: levý okraj `.record-title-wrap` = levý okraj `.record-history` / `.record-comments` (rozdíl ≤ 1 px), na šířce 1400 i 375 px.
- Jen CSS (`site.css`), bez `:has` a `max-content` (i15). Stránka záznamu se nemění.

### 4.B Detaily v tabulce (R2–R4)

Markup v `_ZaznamDetailPartial` (sdílí karta i stránka záznamu):

```html
<table class="record-facts">
  <tr>
    <th scope="row">Vlastník</th>  <td>~~stará~~ <strong>Pavel Admin &lt;…&gt;</strong></td>
    <th scope="row">Subsystém</th> <td><strong>Integrace</strong></td>
  </tr>
  <tr>
    <th scope="row">Termín</th>    <td>~~01.07.2026~~ <strong>13.07.2026</strong></td>
    <th scope="row">Typ úkolu</th> <td><strong>Akce</strong></td>        <!-- jen u úkolu -->
  </tr>
  <tr>
    <th scope="row">Založeno</th>  <td><strong>08.07.2026</strong></td>
  </tr>
</table>
```

- Skutečná `<table>` (spolehlivé zarovnání sloupců i v Edge na i15), bez rámečků, popisky stejným stylem jako dnešní `.label`. Na úzkém okně (< ~640 px) se buňky přeskládají do jedné dvojice na řádek (CSS `display: block` na řádcích / buňkách — letité vlastnosti).
- Záznam bez typu úkolu: druhá dvojice ve 2. řádku chybí (buňky se nevykreslí, ne prázdné „-“).
- Datum založení potřebuje `ZaznamCardDetailViewModel` (dnes ho má jen souhrn karty) — doplní se.
- Na kartě: řádek `.record-summary-meta` v hlavičce se skryje, když je karta rozbalená (`.record-card:not(.collapsed) …` — třída karty už existuje). Na stránce záznamu se samostatný řádek „Založeno:“ odstraní.

### 4.C Záznamy jednání a procházení (R5–R12)

**Sdílené pravidlo (R5).** Nová služba (pracovně `IRecordSequenceService`) s jedním zdrojem pravdy:
- `GetMeetingRecordIdsAsync(jednaniId)` → seřazená Id záznamů jednání (pravidlo zápisu + pořadí zápisu). Tisk zápisu (`ExportRecordProjectionBuilder`) přejde na ni — výběr záznamů v tisku se tím nezmění (pojistka testem: stejná sada a pořadí jako dnes).
- `GetActiveRecordIdsAsync(projektId)` → seřazená Id aktivních záznamů (R8), stejné pořadí.
- Pořadí: skupiny subsystémů podle pořadí v projektu (`ProjectSubsystemOrderingQuery`), subsystémy bez pořadí na konec abecedně; uvnitř `RecordDisplayOrdering` (kategorie → název kategorie → viditelné číslo A → B → číslo záznamu).

**Detail jednání (R6).** Záložka Záznamy v kontextu jednání (`JednaniController.Detail` i obnovení `ProjektyController.RecordsTabPartial` s `kontextJednaniId`) vykreslí jen karty se Id z `GetMeetingRecordIdsAsync`; prázdné skupiny subsystémů se nevykreslí. Bez záznamů: „Do tohoto jednání zatím nepatří žádný záznam.“ Uzavřené jednání se nemění (ukazuje zápis).

**Vstup (R9).** Menu › na kartě:
```
Otevřít na celou obrazovku        ← nové, stejná karta prohlížeče
Otevřít na nové kartě
Zobrazit na záložce Harmonogram   (jako dnes, jen u záznamů s harmonogramem)
Navrhnout změnu harmonogramu      (jako dnes)
```
Obě „Otevřít…“ vedou na `/Zaznamy/Detail/{id}?seznam=…&returnUrl=…`:
- z detailu jednání: `seznam=jednani-{jednaniId}`, `returnUrl` = detail jednání;
- ze záložky Záznamy projektu: `seznam=aktivni`, `returnUrl` = záložka Záznamy.

**Stránka záznamu (R10–R12).**
```
Projekty › PMT › Jednání 906 › Záznam #901-1            ← drobečková lišta (návrat dle returnUrl)
[‹ Předchozí]  3 / 17  [Další ›]                         ← jen se seznamem a jen když záznam v něm je
[Úkol] (Akce)                          Rozpracováno [🖨] [Upravit]
#901-1 - Připravit podklady…
Vlastník  …   Subsystém  …
…
```
- `ZaznamyController.Detail(id, returnUrl, seznam)`: server ověří, že seznam patří k projektu záznamu (jednání téhož projektu; jinak se ignoruje), spočítá sekvenci, pozici záznamu a sousedy. Odkazy na sousedy nesou stejné `seznam` a `returnUrl`.
- Tlačítka mají v nápovědě (`title`) číslo a název sousedního záznamu. Na prvním / posledním je příslušné tlačítko neaktivní.
- Klávesy ← / → přejdou na předchozí / další (stejný odkaz jako tlačítka). Nereagují, když je fokus v `input`, `textarea`, `select`, `[contenteditable]` (editor vyjádření), v `[role=tablist]` nebo je otevřené okno (gov-dialog); ani s modifikátory (Alt+← je v prohlížeči „zpět“).
- Bez parametru `seznam` (staré odkazy, vyhledávání, přehled) stránka vypadá jako dnes — bez šipek.

### 4.D Mazání data plánu (R13, R14)

- `_ScheduleTable.cshtml`: pole plánu `Clearable = !dateDisabled`. Klik na ✕ vyprázdní datum. Požadavek: trvání kroku bez plánu se ukáže jako „–“ (ne „0 dnů“) a graf pro krok bez plánu nekreslí pruh — při implementaci ověřit dnešní chování u nevyplněného kroku a sjednotit.
- Editor záznamu: uloží se `PlanDatum = null` (server to umí).
- Návrh změny harmonogramu:
  - `BuildSchedulePayload` ponese i kroky s prázdným plánem, které plán v záznamu měly (odstranění), ne jen vyplněné;
  - přehled změn při rozhodování ukáže „Původní plán: 13.07.2026 | Navržený: bez plánu“;
  - schválení zapíše `null` (umí už dnes).
- Zamčené pole (návrh čeká na rozhodnutí, uzavřený záznam apod.) ✕ nemá.

## 5. Rozsah

**Mění se:** `_ZaznamPartial.cshtml` (menu, skrytí meta řádku), `_ZaznamDetailPartial.cshtml` (tabulka), `ZaznamDetailPage.cshtml` (šipky, Založeno do tabulky), `site.css`, nový JS modul pro klávesy (import v `bootstrap.js`), `ZaznamyController.Detail`, `BaseController.RecordsTab` (odkazy se `seznam`), `JednaniController.Detail` + `ProjektyController.RecordsTabPartial` (filtr záznamů jednání), nová služba sekvence + napojení tisku zápisu, `ZaznamCardDetailViewModel` (Založeno), `_ScheduleTable.cshtml`, `RecordProposalPayloadMapper`, `RecordProposalService.Queries` (text změny).

**Nemění se:** pravidla, kdo smí co vidět a upravovat; obsah a pořadí tisku zápisu (jen přejde na sdílenou službu); záložka Záznamy projektu (zobrazuje dál všechny záznamy); vyjádření (spec 2026-10-22 kompaktní blok); stránka záznamu bez parametru `seznam`; harmonogram mimo pole plánu.

## 6. Okrajové případy

| Případ | Chování |
|---|---|
| Záznam založený z jednání („Nový záznam“ v pruhu) | Datum založení = datum jednání → patří do jednání, objeví se v seznamu i v šipkách. |
| Záznam uzavřený před minulým jednáním | Do jednání nepatří (jako v zápisu). |
| První jednání projektu (bez minulého) | Uzavřené záznamy do něj nepatří. |
| Jednání v přípravě s datem v budoucnu | Stav „ke dni jednání“ = dnešní stav; patří aktivní a uzavřené od minulého jednání. |
| Ruční změna data založení záznamu po datu jednání | Záznam z jednání vypadne (stejně jako ze zápisu) — vědomé. |
| `seznam=jednani-X`, kde X je jednání jiného projektu nebo neexistuje | Parametr se ignoruje, stránka bez šipek. |
| Záznam mezitím z jednání vypadl (uzavřen, smazán) | Na jeho stránce nejsou šipky; ze sousedů se přeskočí. |
| Uzavřený záznam ze záložky Záznamy (`seznam=aktivni`) | Bez šipek (R12). |
| Jediný záznam v seznamu | „1 / 1“, obě šipky neaktivní. |
| Fokus v editoru vyjádření / poli / okně | ← → píší / ovládají pole, stránka se nemění. |
| Osoba bez přístupu k projektu | 404 jako dnes (šipky nic neprozradí). |
| Plán smazán u kroku, na který navazuje další krok | Trvání dalšího kroku se počítá od předchozího vyplněného (jako dnes u nevyplněného kroku); validátor chronologie prázdné kroky přeskakuje. |
| Návrh smaže plán, mezitím ho PM přímo změní | Rozhodování o návrhu se řídí dnešními pravidly konfliktů (beze změny). |

## 7. Testy a ověření

- **Unit:** sekvence (pravidlo jednání: založení po datu, uzavřen před / po minulém jednání, první jednání; aktivní; pořadí subsystém → kategorie → číslo); CSS hlavičky (indikátor mimo flex, `.record-facts` bez `max-content`/`:has`); klávesový modul (čisté funkce: kdy ← → reagují).
- **Api (render):** detail jednání vykreslí jen karty jednání (a obnovení záložky totéž); odkazy „Otevřít…“ nesou `seznam` a `returnUrl`; stránka záznamu se `seznam` má správnou pozici a sousedy, bez `seznam` / s cizím jednáním šipky nemá; tabulka detailů (Vlastník, Subsystém, Termín, Typ úkolu, Založeno; bez typu u ne-úkolu); pole plánu má `clearable="true"` jen když není zamčené.
- **Api (uložení):** editor uloží prázdný plán; návrh nese odstranění, přehled ho ukáže, schválení plán smaže.
- **Pojistka tisku:** zápis jednání po přechodu na sdílenou službu obsahuje stejné záznamy ve stejném pořadí (charakterizační test před refaktorem).
- **E2E:** odskok hlavičky = 0 px (1400 i 375 px); z detailu jednání menu › → „Otevřít na celou obrazovku“ → ← → projde záznamy jednání a drobečková lišta vrátí do jednání; klávesy v editoru vyjádření stránku nepřepnou; ✕ u plánu → uložit → plán prázdný.
- **Průchod v prohlížeči nad spuštěnou aplikací za všechny role** (stálé pravidlo): 11 rolí + PM s vedoucím + bez přístupu; detail jednání (otevřené, v přípravě), záložka Záznamy, stránka záznamu.

  | Role | Seznam v jednání = záznamy zápisu | Menu „Otevřít na celou obrazovku“ + šipky + klávesy | ✕ u plánu |
  |---|---|---|---|
  | Superadmin, Admin aplikace, Vlastník, Admin projektu, PM, PM + vedoucí | ano | ano | v editoru ano (není-li zamčeno) |
  | Vedoucí, Zástupce vedoucího | ano | ano | v návrhu změny harmonogramu ano |
  | Gestor, Metodik, Host, Read-all | ano | ano | nemají editor ani návrh |
  | Bez role v projektu | 404 | 404 | – |

  Plus: tmavý motiv, 375 px bez vodorovného posuvníku (kromě známého posuvníku hlavičky stránky záznamu), konzole bez chyb, tisk zápisu beze změny.

## 8. Rizika

- **Detail jednání ukáže méně záznamů než dnes** — záměr (R6). Kdo chce všechny, má záložku Záznamy projektu. V `k-otestovani.md` výslovně.
- **Šipky neodpovídají filtrům na stránce** — vědomá volba (R7/R8). Počítadlo „3 / 17“ ukazuje velikost celého seznamu, ne filtrovaného.
- **Refaktor výběru záznamů tisku** — riziko změny zápisu; kryje charakterizační test před změnou.
- **Klávesy ← →** můžou kolidovat s budoucími ovládacími prvky; omezení v R10 drží kolize na minimu.

## 9. Odhad (Opus inline)

| Část | Odhad |
|---|---|
| A. Hlavička karty | 0,5 h |
| B. Detaily v tabulce | 1,25 h |
| C. Záznamy jednání + šipky + klávesy + vstup z menu | 4 h |
| D. Mazání data plánu (editor + návrh) | 1,5 h |
| Ověření: sady, E2E proti baseline, průchod za všechny role, k-otestovani | 2 h |
| **Celkem** | **≈ 9,25 h** |

Ruční ověření uživatelem je navíc. Doporučené pořadí: D (chyba) → A → B → C.
