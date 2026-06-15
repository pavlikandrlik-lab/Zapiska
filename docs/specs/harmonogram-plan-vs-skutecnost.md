# Specifikace — harmonogram: Plán vs. Skutečnost (datum-model)

**Stav:** schváleno 2026-06-16
**Nahrazuje:** předchozí verzi (offset/tag/Hangfire model — zrušeno datum-migrací 2026-06)
**Souvisí:** [automat-vytezovani-vyjadreni.md](automat-vytezovani-vyjadreni.md)

## Kontext

Harmonogram má **10 pevných kroků** (kód `HarmonogramKroky.Vse`). Vše je v **absolutních
datech** — žádné offsety. Každý krok má:

- **Plán** — plánované datum konce kroku (vyplňuje/schvaluje PM). Plánové dokončení = plán kroku 10.
- **Skutečnost** — reálné datum konce kroku. Vzniká buď **automaticky** z vytěžování vyjádření
  ticketů ([automat-vytezovani-vyjadreni.md](automat-vytezovani-vyjadreni.md)), nebo **ručně**
  u kroků 2/5/8/9 (kam vyjádření neplyne). **Nevyplněno = `NULL`** (žádný fallback na plán).

Persistence: `zaznam_harmonogram_krok` (Poradi 1–10, PlanDatum, SkutecnostDatum, SkutecnostRezim,
SkutecnostZdroj, PreferredExterniOdkazId).

## Pojmy (autoritativní definice)

| Pojem | Význam |
|---|---|
| **Plán** | Per-krok plánovaná data. „Kdy to plánujeme." |
| **Plánové dokončení** | Plán kroku 10 (konec plánové lišty). |
| **Skutečnost** | Per-krok reálná data (auto/ruční). `NULL` = krok nemá skutečnost. |
| **Termín** | Deadline = `projektove_zaznamy.DatumUkonceni`. „Dokdy to musí být." Oddělený od plánového dokončení. |
| **Aktuální krok** | První **nevyplněný** krok **po posledním vyplněném** (s vyplněnou skutečností). |

> Příklad aktuálního kroku: vyplněné skutečnosti u kroků 4, 6, 9 → poslední vyplněný = 9 →
> **aktuální krok = 10**. Nevyplněné mezery před posledním vyplněným (5, 7, 8) se přeskakují —
> schéma už postoupilo přes krok 9.

## Per-krok stavy (3)

| Stav | Podmínka |
|---|---|
| **Čeká** | Skutečnost nevyplněna **a** plánované datum kroku je v budoucnu (≥ dnes). |
| **V prodlení** | Skutečnost nevyplněna **a** plánované datum kroku už ulpynulo (< dnes). |
| **Splněno** | Skutečnost vyplněna (auto nebo ruční). |

## Sjednocený stav (nahrazuje „Stíháme" + „Překročení" + „Skutečné dokončení")

Místo tří samostatných údajů **jeden** indikátor odvozený z aktuálního kroku:

```
Překročení = dnes − plán(aktuální krok)          (znaménkové, v kalendářních dnech)
```

- **> 0** → „skluz +X dní" (červeně) — aktuální krok je po svém plánu.
- **< 0** → „v předstihu X dní" (zeleně) — plán aktuálního kroku ještě nenastal.
- **= 0** → „dle plánu".
- **Všechny kroky vyplněné** (žádný aktuální krok) → **„Dokončeno"**, překročení 0.

Zobrazení v souhrnu: **„Aktuální krok: <název> · skluz +X dní"** (resp. „v předstihu", „Dokončeno").

**Rušíme** ze souhrnu: samostatné „Stíháme/Nestíháme", samostatné „Překročení: X dnů",
a řádek „Skutečné dokončení" (u rozpracovaného ukazoval matoucí „dnes").

Souhrn nově = **Termín** + **Sjednocený stav**.

## Pravidla pro datumy (chronologie)

Ruční vstupy musí být chronologické (**neklesající** — stejný den je OK). Vynuceno na
**dvou úrovních**: UI nedovolí zadat datum mimo pořadí (okamžitá zpětná vazba) **a** server
při Save tvrdě odmítne (`RecordValidationException`, chyba na konkrétním poli) jako pojistka.

- **Plán** — pro každý krok N platí `plán(N) ≥ plán(předchozího kroku s plánem)`. Neklesající
  napříč kroky 1–10.
- **Ruční skutečnost** (kroky 2/5/8/9) — zadané datum musí být `≥` skutečnost nejbližšího
  **předchozího** kroku se skutečností a `≤` skutečnost nejbližšího **následujícího** kroku se
  skutečností (pokud existují). Tedy zapadne chronologicky mezi známé skutečnosti.
- **Auto skutečnost** (harvest) — chronologii zajišťuje vytěžovací algoritmus
  ([automat-vytezovani-vyjadreni.md](automat-vytezovani-vyjadreni.md)); z automatu pomatené
  pořadí nepřijde, proto se zde **znovu nevaliduje** (kalkulátor jen defenzivně ořízne).

## Lišty (overview souhrn i Rozpad)

Dvě řady na společné časové ose:

- **Plán** — 10 segmentů vždy, pozice dle plánovaných dat (beze změny).
- **Skutečnost** — segmenty splněných kroků; **aktuální krok se kreslí od konce posledního
  splněného až po dnešek**. Ostatní kroky ve stavu „Čeká" (za aktuálním krokem) se nekreslí.
- Značky: **Termín** (deadline) a **Dnes**.

**Rozpad (breakdown)** — totéž per krok: každý krok ukazuje svůj plánový segment vs. segment
skutečnosti; aktuální krok táhne skutečnost do dneška.

### Barvy a styl (důležité)

- **Barva kroku je vždy pevná** — daná konfigurací kroku, **stejná v liště Plán, liště
  Skutečnost i v Rozpadu**. Stav kroku (Čeká / V prodlení / Splněno) se **NEpromítá do barvy**
  segmentů. (Konkrétní paleta se doladí později — není teď podstatná.)
- **Plán** = plná čára (solid styl).
- **Skutečnost** = **šrafovaný** styl a **o kousek nižší na výšku** než plán, aby bylo na první
  pohled vidět, kde je plán a kde skutečnost (překryv obou na stejné ose).
- **Prodlení/předstih se pozná délkou, ne barvou**: segment skutečnosti aktuálního kroku
  přesahuje za konec svého plánového segmentu (až po „Dnes") = vizuální skluz; končí dřív =
  předstih. Značka „Dnes" to potvrzuje.
- Stav (Čeká / V prodlení / Splněno) se komunikuje textově (sjednocený stav v souhrnu, tooltip
  kroku), nikoli barvou segmentu.

## Nevyplněná skutečnost

- View-model už **nepoužívá fallback na PlanEnd** pro nevyplněné kroky (dřív `SkutecneDatum =
  MaSkutecnost ? SkutecnostEnd : PlanEnd` → mohlo se tvářit „skutečnost = plán"). Nově nevyplněno
  = žádné datum, render dle 3-stavového modelu (Čeká / V prodlení).

## Hraniční případy

| Případ | Chování |
|---|---|
| Žádný krok vyplněn | Aktuální krok = 1; Překročení = dnes − plán(1) (znaménkově). |
| Všechny kroky vyplněny | „Dokončeno", Překročení 0. |
| Mezery (vyplněno 4,6,9) | Aktuální = 10 (po posledním vyplněném); mezery 5/7/8 přeskočeny. |
| Skutečnost před plánem (předstih) | Stav „Splněno"; sjednocený stav může být „v předstihu". |
| Plán aktuálního kroku v budoucnu | Stav aktuálního kroku = „Čeká"; Překročení záporné (předstih). |

## Dopad na kód (orientačně — detail řeší implementační plán)

- `ScheduleDateCalculator.Summarize` — překročení = `dnes − plán(aktuální krok)` (znaménkové),
  detekce aktuálního kroku, „Dokončeno" stav; zrušit `Stihame` jako separátní vs Termín.
- `HarmonogramDateBlokBuilder` — odstranit PlanEnd fallback pro nevyplněné; per-krok stav (3).
- `ScheduleBarLayoutCalculator` — segment skutečnosti aktuálního kroku táhnout do dneška.
- `_ScheduleBlock.cshtml` — souhrn (sjednocený stav, zrušit Skutečné dokončení/Stíháme/Překročení
  zvlášť), lišta skutečnosti + Rozpad render aktuálního kroku do dneška, oprava tooltipů.
- `ValidateScheduleValuesAsync` (server) — přidat chronologickou validaci plánu + ruční skutečnosti.
- `wwwroot/js/modules/schedule/block.js` + `pm-date-field` — UI bránění nechronologickému zadání
  (min/max dle sousedních kroků); editor live-preview musí zrcadlit stejnou logiku výpočtu.
- CSS (`site.css` / komponenty) — styl lišt: plán plný, skutečnost šrafovaná + nižší výška;
  barvy kroků pevné (jeden zdroj barvy pro plán/skutečnost/Rozpad).

## Mimo scope této specifikace

- Sémantika „hotovo" zaškrtnutím místo data — **není potřeba** (prázdné datum = neplní se, plné = splněno).
- Změna pravidel vytěžování / mapování predikátů (řeší automat-vytezovani-vyjadreni.md).
- Redukce počtu kroků (zůstává 10).

## Testy

- **Unit** `ScheduleDateCalculatorTests` — aktuální krok (vč. mezer 4/6/9→10), znaménkové
  překročení, „Dokončeno", hraniční případy (žádný/všechny vyplněné, předstih).
- **Unit** `HarmonogramDateBlokBuilderTests` — 3 stavy per krok, žádný PlanEnd fallback.
- **Unit** `ScheduleBarLayoutCalculatorTests` — segment aktuálního kroku do dneška.
- **Unit/Api** chronologie — server odmítne nechronologický plán i ruční skutečnost (chyba pole);
  validní (neklesající, stejný den) projde; auto skutečnost se nevaliduje.
- **Api render** — souhrn ukazuje sjednocený stav, neukazuje Skutečné dokončení; lišta skutečnosti
  je šrafovaná/nižší a barvy kroků se shodují s plánem.
