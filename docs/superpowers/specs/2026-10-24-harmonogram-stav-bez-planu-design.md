# Harmonogram: stav a skluz u kroku bez plánu, živý přepočet editoru — specifikace

**Datum:** 2026-10-24
**Stav:** návrh k revizi; plán implementace `docs/superpowers/plans/2026-10-24-harmonogram-stav-bez-planu-plan.md`
**Navazuje na:** `docs/specs/harmonogram-plan-vs-skutecnost.md` (sjednocený stav, datum-model) — tato specifikace ho doplňuje o krok bez plánu
**Komponenty:** výpočet souhrnu (`Services/Schedules/ScheduleDateCalculator.cs`, `HarmonogramDateBlokBuilder.cs`), souhrn a Rozpad harmonogramu (`Views/Shared/_ScheduleBlock.cshtml`), editor harmonogramu (`wwwroot/js/modules/schedule/block.js`), štítek Stíháme/Nestíháme (karta záznamu, záložka Harmonogram projektu)

## 1. Zadání (uživatel, 2026-10-24)

> Nad harmonogramem mám nyní uvedené zpoždění ve dnech a vztahuje se to k aktuálnímu kroku. Problém ale nastává, když krok v plánu nemá datum, tedy je datum null, protože se počítá jako aktuální datum a naskakují nesmysly. Konkrétně když záznam nemá vyplněný plán, ale má navázaný externí záznam a z toho se automatem zjistí termíny pro skutečnost — když se takový termín skutečnosti doplní a plán neexistuje, píše to nesmyslná čísla. Byl bych raději, kdyby to psalo „plán není vyplněný“.

> A nebo kdyby to počítalo k celkovému termínu záznamu, ale jen když existuje skutečnost, která nemá do páru zadaný plán. Textem to ale musí být odlišeno, nyní to píše „Aktuální krok…“, tak když nebude plán, mohlo by to psát „Plán není zadán. Celkový termín…“.

Rozhodnutí z rozhovoru jsou v kapitole 3.

## 2. Současný stav (ověřeno 2026-10-24 v kódu a v prohlížeči)

### Výpočet
- **Aktuální krok** = první krok bez skutečnosti za posledním krokem se skutečností (spec harmonogram-plan-vs-skutecnost).
- **Skluz** = dnes − konec plánu aktuálního kroku (`ScheduleDateCalculator.Summarize`).
- **Konec plánu kroku bez plánu** se dopočítá: = konec plánu předchozího kroku, a když žádný předchozí krok plán nemá, = **datum založení záznamu** (`ScheduleDateCalculator.Compute`, ř. 59). Pro graf je to v pořádku (krok bez plánu má nulovou délku), ale pro skluz to dává nesmysl.
- **Plán dokončení** = konec plánu posledního kroku, takže u záznamu bez plánu = datum založení. **Trvání** = 0.

### Reprodukce v dev DB (editor záznamu 99009, žádný plán, žádná skutečnost, založen 08.07.2026, dnes 24.10.2026)
- Stav: **„Aktuální krok: 1. priprava zadani dodavateli · skluz +108 dní“** — 108 = dnes − datum založení.
- Plán dokončení: **08.07.2026** (datum založení), Trvání: **0 dnů**.
- Štítek u stejného případu by byl **„Nestíháme“** (štítek = skluz ≤ 0).
- Uživatelův případ (plán prázdný, skutečnost kroků 1/3/4 doplnil automat z externí vazby): aktuální krok je 5, jeho „plán“ = datum založení → skluz = dnes − datum založení.

### Kde se stav zobrazuje
| Místo | Co ukazuje | Kde se počítá |
|---|---|---|
| Editor záznamu, záložka Harmonogram | Termín, Plán dokončení, Trvání, Stav | server (`_ScheduleBlock.cshtml`), po načtení přepíše JS (`block.js renderSummary`) |
| Detail návrhu změny harmonogramu | totéž (editor v režimu jen pro čtení) | totéž |
| Karta záznamu → pohled Harmonogram, stránka záznamu | štítek Stíháme/Nestíháme, Plán, Termín, Stav, Rozpad s odchylkou kroku „+N dnů“ | server |
| Záložka Harmonogram projektu | štítek Stíháme/Nestíháme, Plán, Termín, Stav, Rozpad | server |

Text stavu je dnes zkopírovaný třikrát (dvakrát v `_ScheduleBlock.cshtml`, jednou v `block.js`).

### Editor se po změně data nepřepočítává (ověřeno v prohlížeči, záznam 300)
- Po výběru data v kalendáři i po ✕ zůstanou **Stav, Plán dokončení, Trvání i graf beze změny** až do uložení.
- Příčina: řádek editoru hledá pole selektorem `[data-schedule-duration-calendar]` (`block.js`, ř. 150). Ten atribut nese i obal `<pm-date-field>`, který je v DOM dřív než skryté pole uvnitř. Posluchač `change` se proto nepřipojí (obal není `<input>`).
- Stejnou příčinu mají **meze kalendáře** (Fáze 5, commit 0c1a4cb: kalendář měl vypnout dny mimo pořadí kroků) — `applyPlanChronologyBounds` čte `.value` z obalu, dostane `undefined`, a meze nenastaví nikdy. Na záznamu 300 nemá žádný krok `min`/`max`. Pořadí dnes hlídá jen server při uložení („Plán kroku 3 nesmí být dříve než plán kroku 2.“).

## 3. Rozhodnutí

| # | Rozhodnutí | Zdroj |
|---|---|---|
| R1 | **Aktuální krok** se určuje beze změny. | stávající spec |
| R2 | **Vůči čemu se počítá skluz:** má-li aktuální krok **vlastní** plánové datum, skluz = dnes − plán aktuálního kroku (beze změny). **Nemá-li ho, skluz = dnes − celkový termín záznamu** (`datum_ukonceni`, NOT NULL). Rozhoduje jen plán aktuálního kroku: když ho aktuální krok má, počítá se k němu, i kdyby dřívější kroky plán neměly. | uživatel (varianta „aktuální krok nemá plán“) |
| R3 | **Text stavu** (název kroku zůstává): <br>• k plánu kroku: „Aktuální krok: X · skluz +5 dní“ / „· v předstihu 3 dny“ / „· dle plánu“ <br>• k celkovému termínu: „Aktuální krok: X · plán není zadán · celkový termín: skluz +5 dní“ / „· celkový termín: zbývá 12 dní“ / „· celkový termín je dnes“ <br>• vše vyplněno: „Dokončeno“ | uživatel (varianta „I s názvem kroku“) |
| R4 | **Skloňování ve stavu:** 1 → „den“, 2–4 → „dny“, jinak „dní“ („skluz +1 den“, „v předstihu 3 dny“, „skluz +5 dní“). U „zbývá“ se shoduje sloveso: „zbývá 1 den“, „zbývají 3 dny“, „zbývá 5 dní“. Mění se tím i dnešní texty pro 1–4 dny. | uživatel |
| R5 | **Barva stavu a štítek Stíháme/Nestíháme** se řídí stejným číslem jako text: zeleně/„Stíháme“, když je hotovo nebo skluz ≤ 0; jinak červeně/„Nestíháme“. U záznamu bez plánu tedy podle celkového termínu. | uživatel |
| R6 | **Plán dokončení** (editor) a **Plán:** (karta, záložka): když **žádný** krok nemá plán, „není zadán“ a v editoru **Trvání: –**. Když má plán aspoň jeden krok, beze změny (poslední zadané plánové datum, trvání od založení). Trvání se skloňuje stejně jako trvání kroku („1 den“, „3 dny“, „5 dnů“). | uživatel |
| R7 | **Rozpad:** krok se skutečností, ale bez vlastního plánu, ukazuje místo „+N dnů“ neutrální štítek **„bez plánu“** (bez červené/zelené). | uživatel |
| R8 | **Odchylka kroku v Rozpadu se skloňuje** stejně jako trvání kroku: „+1 den“, „+3 dny“, „+5 dnů“, „-2 dny“, „0 dnů“ (dnes vždy „dnů“). Text se přesouvá do stejné formátovací funkce jako stav, tak ji nechávám mluvit správně. | doplněno — k potvrzení |
| R9 | **Živý přepočet editoru:** výběr data i ✕ u plánu **nebo** skutečnosti hned přepočítá Stav, Plán dokončení, Trvání i graf. | uživatel |
| R10 | **Meze kalendáře u plánu:** kalendář vypne dny **před plánem nejbližšího dřívějšího kroku s plánem** a **po plánu nejbližšího pozdějšího kroku s plánem**; kroky bez plánu se přeskakují; stejný den je povolený. Je to přesně pravidlo serveru (`ScheduleChronologyValidator`) — co kalendář dovolí, uložení přijme. Meze se přepočítají po každé změně. | uživatel |
| R11 | **Jedno pravidlo, dvě implementace, jedny testy:** rozhodnutí R2 a texty R3/R4/R6 jsou na serveru v jednom výpočtu a jedné formátovací funkci; editor má jejich JS zrcadlo. Obě strany ověřuje **tatáž sada případů** (jeden JSON soubor čtený xUnit i node:test), stejně jako dnes u osy harmonogramu (`axis-cases.json`). | uživatel (varianta A) |

## 4. Chování v příkladech (dnes = 24.10.2026)

| Situace | Stav | Plán dokončení / Plán | Štítek |
|---|---|---|---|
| Bez plánu, skutečnost kroků 1, 3, 4 (automat), termín 05.11.2026 | Aktuální krok: 5. vyporadani pripominek · plán není zadán · celkový termín: zbývá 12 dní | není zadán | Stíháme |
| Totéž, termín 14.10.2026 | … · plán není zadán · celkový termín: skluz +10 dní | není zadán | Nestíháme |
| Totéž, termín 24.10.2026 | … · plán není zadán · celkový termín je dnes | není zadán | Stíháme |
| Totéž, termín 27.10.2026 | … · celkový termín: zbývají 3 dny | není zadán | Stíháme |
| Bez plánu, bez skutečnosti (jen v editoru — karta ani záložka takový záznam neukazují) | Aktuální krok: 1. priprava zadani dodavateli · plán není zadán · celkový termín: … | není zadán, Trvání – | — |
| Krok 1 plán 01.10. + skutečnost, krok 2 bez plánu, krok 3 plán 01.11. | Aktuální krok: 2. … · plán není zadán · celkový termín: … | 01.11.2026 | podle termínu |
| Krok 1 skutečnost bez plánu, krok 2 plán 20.10. | Aktuální krok: 2. … · skluz +4 dny | 20.10.2026 | Nestíháme |
| Krok 5 plán 23.10., aktuální krok 5 | Aktuální krok: 5. … · skluz +1 den | podle plánu | Nestíháme |
| Všechny kroky se skutečností | Dokončeno | podle plánu | Stíháme |

## 5. Technický návrh (varianta A)

1. **Výpočet** (`ScheduleDateCalculator.Summarize`) vrací nově i **vůči čemu** se stav počítá — výčet `HarmonogramStavVztah { AktualniKrok, CelkovyTermin, Dokonceno }` — a `PlanoveDokonceni` jako `DateTime?` (null = žádný krok nemá plán). `Dokonceno` je odvozené z výčtu.
2. **Souhrn pro šablony** (`HarmonogramSouhrnViewModel`) nese `StavVztah`, `BaselineDokonceni` jako `DateTime?`; `Stihame` zůstává odvozené (hotovo nebo skluz ≤ 0), takže štítek ve službách záložky i karty převezme R2 bez úprav.
3. **Texty** skládá jediná třída `ScheduleStatusText` (stav, plán dokončení, trvání, odchylka kroku). `_ScheduleBlock.cshtml` ji jen volá — text v šabloně nezůstane.
4. **JS zrcadlo** `wwwroot/js/modules/schedule/scheduleStatus.js`: `summarizeSchedule` (výpočet = `Summarize`) a formátovací funkce (= `ScheduleStatusText`). Editor (`block.js renderSummary`) volá jen je.
5. **Sdílené případy** `tests/js/schedule/__fixtures__/status-cases.json` čte `ScheduleStatusGoldenVectorTests` (xUnit, přes `HarmonogramDateBlokBuilder.BuildSouhrn` = skutečná cesta šablon) i `scheduleStatus.test.js` (node:test). Rozejdou-li se server a prohlížeč, spadne test.
6. **Živý přepočet:** jeden delegovaný posluchač `change` na bloku harmonogramu pro pole `….PlanDatum` a `….SkutecnostDatum` (výběr v kalendáři i ✕ posílají `change` ze skrytého pole). Řádek editoru odkazuje přímo na skryté pole plánu. Odstraní se mrtvý kód, který odkazoval na obal (`renderEditorRows`, `setDateInputValue`, `recalcFromDate`, `recalcFromDelayDate`).
7. **Meze kalendáře:** čistá funkce `planBounds` (`wwwroot/js/modules/harmonogram/planChronology.js`) nad plány kroků v pořadí; `applyPlanChronologyBounds` z ní nastaví `min`/`max` na `<pm-date-field>`.
8. **Pojistka jednoho zdroje:** zdrojový test hlídá, že `_ScheduleBlock.cshtml` ani `block.js` neobsahují text „Aktuální krok:“ (text jen ve formátovacích funkcích).

## 6. Mimo rozsah

- **Dashboard a matice priorit** (`ProjectDashboardService`, `PriorityMatrixRebuildService.ResolveNearestFutureMilestoneDate`) používají stejné dopočtené konce plánu, takže krok bez plánu tam může vypadat jako „po termínu“. Podle dohody se dashboard neupravuje do jeho přepracování.
- **Graf** (pruhy plánu) se nemění — krok bez plánu dál nemá pruh.
- **Detail návrhu:** server tam vykreslí souhrn současného záznamu a JS ho po načtení přepočítá z navržených hodnot (tak je to i dnes). Po této změně JS spočítá správný text i pro navržené smazání plánu; serverový souhrn z navržených hodnot se nepřidává.
- **JS Rozpad** (`block.js renderBreakdown`) se v editoru nespouští (editor Rozpad nemá) — beze změny.

## 7. Testy

- **Unit (xUnit):** `ScheduleDateCalculatorTests` (vztah R2, plánové dokončení R6, včetně krajních případů z kapitoly 4), `HarmonogramDateBlokBuilderTests` (souhrn bez plánu), `ScheduleStatusTextTests` (všechny větve R3/R4/R6/R7/R8), `ScheduleStatusGoldenVectorTests` (sdílené případy), `ScheduleStatusTextSourceTests` (R11 pojistka).
- **JS (node:test):** `scheduleStatus.test.js` (sdílené případy), `planChronology.test.js` (R10).
- **Api (render):** stránka záznamu, editor a záložka Harmonogram u záznamu bez plánu se skutečností z automatu — text stavu, „není zadán“, „–“, „bez plánu“ v Rozpadu, štítek.
- **E2E (Playwright):** v editoru ✕ u plánu aktuálního kroku → stav se hned přepne na „plán není zadán · celkový termín…“; výběr data → zpět na skluz k plánu; kalendář kroku 3 má vypnuté dny před plánem kroku 1, když krok 2 plán nemá.
- **Prohlížeč za všechny role:** karta, stránka záznamu, záložka Harmonogram, editor (živý přepočet, meze), detail návrhu; světlý i tmavý motiv, 375 px.
