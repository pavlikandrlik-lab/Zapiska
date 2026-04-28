# Vytěžování vyjádření — NES odpojení + 4 datumy na kartě externí vazby

**Stav:** schváleno k implementaci (2026-04-28)
**Závisí na:** [automat-vytezovani-vyjadreni.md](../../specs/automat-vytezovani-vyjadreni.md), [harmonogram-plan-vs-skutecnost.md](../../specs/harmonogram-plan-vs-skutecnost.md), [2026-04-21-servicedesk-vytezovani-vyjadreni-design.md](2026-04-21-servicedesk-vytezovani-vyjadreni-design.md), [2026-04-24-sd-features-decision-brief.md](2026-04-24-sd-features-decision-brief.md), [2026-04-24-sd-plan-4-c-harmonogram-auto-fill.md](../plans/2026-04-24-sd-plan-4-c-harmonogram-auto-fill.md)

## Goal

Dvě věcné změny v current logice vytěžování vyjádření z `HOT_VYJADRENI`:

1. **NES odpojen od harmonogramu** — externí vazba typu NES nepřispívá do harmonogramového stepperu žádného kroku, modal „Vyjádření a termíny" pro NES vazbu skryje stepper sekci.
2. **Auto-fill 4 datumů na kartě externí vazby** — sloupce `datum_objednani`, `plan_dodani`, `datum_dodani`, `datum_prevzeti` (existující, dnes vyplňované jen ručně) dostávají automatickou populaci z harvestu, s rozdílnými pravidly per typ tiketu (NES vs PMP/PNF).

Třetí drobnější změna:

3. **Krok 1 harmonogramu plněn z `HOT_ZAZNAMY.datum` agregovaně přes všechny PMP/PNF tickety záznamu** — agregace MIN (nejdřívější datum založení tiketu).

## Architecture

### §1 — Harmonogram matice

| # | Krok | NES | PMP | PNF | Zdroj datumu (auto-fill) |
|---|---|:-:|:-:|:-:|---|
| 1 | příprava zadání dodavateli | ❌ | ✓ | ✓ | `HOT_ZAZNAMY.datum` napříč všemi vazbami → **MIN** |
| 2 | konzultace termínů | — | dropdown | — | ruční |
| 3 | odeslání zadání dodavateli | — | ✓ | — | K3 fráze |
| 4 | dodání návrhu řešení | — | ✓ | — | K4 fráze (DESC poslední) |
| 5 | vypořádání připomínek | — | dropdown | — | ruční |
| 6 | odeslání požadavku na výrobu | — | — | ✓ | K6 fráze |
| 7 | dodání funkcionality | — | — | ✓ | K7 fráze (DESC poslední) |
| 8 | připomínkování | — | — | dropdown | ruční |
| 9 | testování | — | — | dropdown | ruční |
| 10 | nasazení do provozu | — | — | ✓ | K10 fráze |

**NES je úplně odpojen** — `HarmonogramKrokDatumMapping["NES"]` zůstává prázdné, NES vazby se nestávají kandidáty pro žádný krok.

PMP a PNF dostávají **navíc krok 1** (oproti dnešnímu stavu kódu, kde matice pro krok 1 prázdná). Krok 1 nepoužívá fráze v `HOT_VYJADRENI.popis` — místo toho přímo `HOT_ZAZNAMY.datum` (datum založení tiketu).

### §2 — Agregace per krok: MAX vs MIN

Stávající resolver ([HarmonogramSkutecnostResolver.cs:60](../../../PmTracker.Web/Services/Schedules/HarmonogramSkutecnostResolver.cs#L60)) řadí kandidáty `OrderByDescending(b => b.Datum)` (= MAX, nejpozdější). Pro krok 1 chceme opačně.

**Pravidlo:**
- Krok 1 → **MIN** (ASC, nejdřívější datum)
- Všechny ostatní kroky (3, 4, 6, 7, 10) → **MAX** (DESC, nejpozdější)

Hardcoded výjimka v `HarmonogramSkutecnostResolver`:

```csharp
var ordered = krokPoradi == 1
    ? kandidati.OrderBy(b => b.Datum)              // MIN — nejdřívější založení
    : kandidati.OrderByDescending(b => b.Datum);   // MAX — výchozí (nejpozdější)
```

### §3 — Modal „Vyjádření a termíny" pro NES

Modal se otevírá z tlačítka „Vyjádření a termíny" u jednotlivé externí vazby (per-link), ne na úrovni projektového záznamu.

**Pro NES externí vazbu modal zobrazí:**
- ✅ Seznam vyjádření z `HOT_VYJADRENI` (chronologicky)
- ✅ Sekce 4 datumů (s NES-specific pravidly — viz §4.2)
- ❌ **Skrýt** stepper sekci s bublinami (žádné napojování vyjádření na harmonogramové kroky, žádný drag&drop, žádný stepper UI)

**Pro PMP/PNF externí vazbu** modal beze změny — stepper zůstává.

Implementačně: conditional rendering v Razor partialu na základě `eo.TypZaznamu == "NES"`.

### §4 — Per-ticket metadata (4 datumy)

DB sloupce již existují v tabulce `zaznam_externi_odkazy`:
- `datum_objednani` (date, NULL)
- `plan_dodani` (date, NULL)
- `datum_dodani` (date, NULL)
- `datum_prevzeti` (date, NULL)

Žádná DB migrace není potřeba. Auto-fill je nová funkcionalita; **přepisuje** dosavadní hodnoty (žádný respekt k ručnímu zápisu — Auto-fill mode je default).

#### §4.1 — PMP a PNF (společná pravidla)

| Pole | Predikát | SQL match (`HOT_VYJADRENI.popis LIKE`) | Pořadí | Validace |
|---|---|---|---|---|
| `DatumObjednani` | K6 | `N'%Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována.%'` | první (ASC) | full-string match |
| `PlanDodani` | PlanDodani + K6 dvojfráze | `N'%předal záznam dodavateli :%s termínem plnění dodavatele%'` + regex datumu | viz §4.3 | dvojfrázová validace, jinak null |
| `DatumDodani` | K4_K7 | `N'%Dodavatel přidal řešení%'` | **poslední (DESC)** | full-string match |
| `DatumPrevzeti` | K10 | `N'%Záznam byl převeden do archivu.%'` | jediný | full-string match |

Pro `DatumObjednani`, `DatumDodani`, `DatumPrevzeti` se používá `HOT_VYJADRENI.datum` toho vyjádření, které frázi obsahuje.

#### §4.2 — NES (jiná fráze + DB sloupec místo textu)

| Pole | Zdroj | Detail |
|---|---|---|
| `DatumObjednani` | NES fráze (≠ K6) | `popis LIKE N'%Záznam byl předán dodavateli k řešení.%'` — **kratší než K6**, full-string match. Rozlišení podle `HOT_ZAZNAMY.typ_zaznamu = 'NES'` (PMP/PNF tickety se hledají dle K6 plné fráze). |
| `PlanDodani` | **`HOT_ZAZNAMY.sla_deadline`** | DB sloupec (datetime, NULL), **ne text** v `HOT_VYJADRENI`. |
| `DatumDodani` | NES fráze | `popis LIKE N'%Vazba na IMPLEMENTAČNÍ ZÁZNAM HOTLINE číslo%'` — **poslední výskyt (DESC)**, číslo za frází se neextraktuje. |
| `DatumPrevzeti` | K10 fráze | identické s PMP/PNF: `N'%Záznam byl převeden do archivu.%'` |

#### §4.3 — PlanDodani PMP/PNF: dvojfrázová validace

Algoritmus pro vytěžení `PlanDodani` u PMP a PNF tiketů:

1. Najdi všechna vyjádření v daném tiketu, jejichž `popis` obsahuje frázi `předal záznam dodavateli :` AND `s termínem plnění dodavatele` (= predikát PlanDodani).
2. Pro každý takový kandidát `n`:
   1. Z konce textu `popis` extrahuj datum regexem `\b(\d{1,2})\.(\d{1,2})\.(\d{4})\b` (formát DD.MM.YYYY, akceptuje i jednomístný den/měsíc).
   2. **Validační podmínka**: V tom samém tiketu existuje vyjádření `n+1` nebo `n+2` (chronologicky podle `HOT_VYJADRENI.datum`, případně `id` při shodě), jehož `popis` obsahuje plnou K6 frázi „Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována.".
3. Pokud žádný kandidát validační podmínku nesplňuje → `PlanDodani = null`.
4. Pokud více kandidátů splňuje validaci → vzít poslední (DESC, nejpozdější vyjádření s validní dvojící).

#### §4.4 — Null tolerance

Každý ze 4 datumů může být `null` **nezávisle** na ostatních. Žádná posloupnost (např. „PlanDodani <= DatumDodani") není během auto-fillu vynucována. Validace v [RecordService.SaveRecord.cs:712-742](../../../PmTracker.Web/Services/RecordService.SaveRecord.cs#L712-L742) zůstává jen pro **ručně** zadané hodnoty (UI form input), auto-fill ji obchází (auto-fill jednotlivé datumy zapisuje atomicky).

### §5 — Switch Auto/Manual (beze změny)

Beze změny oproti decision brief C-Q1 až C-Q5:

- Switch Auto/Ručně per záznam, default Auto (C-Q1)
- Default datum z auto-fill = MAX (nejpozdější), dropdown chevron pro alternativy (C-Q2). **Výjimka**: krok 1 = MIN (viz §2).
- Enum 4 hodnoty: `Neznamo` / `Automat` / `Manual` / `Historicka` (C-Q3)
- Re-harvest zahazuje user manual bindings (C-Q4)
- Kaskádové volání binding → SkutecnostSync, bez periodic syncu (C-Q5)

## Tech Stack

.NET 8, EF Core 8, existující harvest pipeline (`VyjadreniHarvestService`, `SqlVyjadreniQueryService`, `HarmonogramSkutecnostResolver`, `HarmonogramSkutecnostSyncService`), Razor partials pro modal „Vyjádření a termíny".

## Implementační dopad (high-level)

### Nové soubory

| Soubor | Odpovědnost |
|---|---|
| `PmTracker.Web/Services/ServiceDesk/PerTicketMetadataExtractor.cs` | Pure logic: ze seznamu vyjádření tiketu + `HOT_ZAZNAMY.typ_zaznamu` + `HOT_ZAZNAMY.sla_deadline` vytvoří `PerTicketMetadata` (4 datumy). |
| `PmTracker.Web/Services/ServiceDesk/PerTicketMetadataSyncService.cs` | Orchestrace: po harvestu zapíše 4 datumy do `ZaznamExterniOdkazEntity`. |
| `PmTracker.Tests.Unit/ServiceDesk/PerTicketMetadataExtractorTests.cs` | Unit testy: NES texty, dvojfrázová validace PlanDodani, regex datumu, DESC poslední, null tolerance. |

### Modifikované soubory

| Soubor | Změna |
|---|---|
| `HarvestPredicates.cs` | Přidat enum hodnoty `NES_DatumObjednani`, `NES_DatumDodani` + odpovídající fráze. Stávající K3/K6/K10/K4_K7 beze změny. |
| `HarmonogramKrokDatumMapping.cs` | Přidat `[1] = PredikatK1` do `PMP` a `PNF` map (nový `PredikatK1` = `"K1_DatumZalozeni"`). |
| `HarmonogramSkutecnostResolver.cs` | Přidat MIN výjimku pro `krokPoradi == 1`. |
| `VyjadreniHarvestService.cs` | Po harvest batch volat `PerTicketMetadataSyncService.SyncTicketAsync`. Pro NES tickety **neukládat** žádné `VyjadreniVazby` (skip stepper bindings). Přidat synthetic K1 binding pro PMP/PNF (nese `HOT_ZAZNAMY.datum`). |
| `SqlVyjadreniQueryService.cs` | Nové query: (a) NES texty per typ tiketu, (b) dvojfrázová validace PlanDodani PMP/PNF, (c) `sla_deadline` z `HOT_ZAZNAMY` pro NES. |
| `HotZaznamFingerprintDto` (nebo ekvivalent) | Přidat `SlaDeadline` field — pro NES `PlanDodani`. |
| Razor partial modalu „Vyjádření a termíny" | Conditional rendering: skrýt stepper sekci pro NES (`@if (eo.TypZaznamu != "NES") { ... stepper ... }`). |

### Bez DB migrace

Sloupce `datum_objednani`, `plan_dodani`, `datum_dodani`, `datum_prevzeti` na `zaznam_externi_odkazy` již existují (`záznamy jednání-7.sql:186-189`).

## Testing

- **Unit `PerTicketMetadataExtractorTests`** — 5 typu tiketu × 4 pole + edge cases:
  - NES happy-path (4 datumy, jiné fráze)
  - PMP/PNF happy-path (4 datumy, K-fráze)
  - PlanDodani dvojfrázová validace: pass (n+1 K6), pass (n+2 K6), fail (n+3 K6), fail (žádné K6)
  - Regex datumu: `1.5.2026`, `01.05.2026`, `15.12.2026`, fail (žádný datum v textu)
  - Null tolerance: jen některé datumy nalezeny → ostatní null
  - DESC vs ASC pořadí: DatumDodani DESC latest, DatumObjednani jediný/první ASC
  - K1 MIN vs ostatní MAX
- **Unit `HarmonogramSkutecnostResolverTests`** (rozšíření existujícího) — krok 1 MIN test case
- **Unit `HarmonogramKrokDatumMappingTests`** (rozšíření) — PMP[1] a PNF[1] mapping
- **Integration** — end-to-end harvest s seedovaným `HOT_VYJADRENI` fixture (NES + PMP + PNF), assert 4 datumy v DB + harmonogram krok 1/3/4/6/7/10 fill stav
- **Regression** — ověřit, že existující ručně vyplněné hodnoty se přepíšou (= žádný respekt k manuálu)

## Otevřené otázky

Žádné. Všechny otázky brainstorming fáze (Q1-Q5) zodpovězeny:

| # | Otázka | Rozhodnutí |
|---|---|---|
| Q1 | PNF krok 10 auto vs ruční? | **Auto** (z K10 archivace) — finální stav |
| Q2 | NES UI scope? | Skrýt stepper sekci v modalu „Vyjádření a termíny" pro NES vazby |
| Q3 | PlanDodani dvojfráze sémantika? | n+1 nebo n+2 vyjádření v tom samém tiketu obsahuje K6 |
| Q4 | NES texty + číslo extrakce? | Pouze identifikace stringu, žádná extrakce čísla |
| Q5 | Existující ručně vyplněné datumy? | Auto-fill přepisuje |
| Q6 | Krok 1 agregace? | **MIN** (jediná výjimka, ostatní kroky MAX) |
| Q7 | NES `DatumDodani` pořadí? | **DESC poslední** (analogicky K4/K7) |

## Zodpovědnost

- **Implementace:** Claude (postupně, bez agentů — dle user pokynů z předchozí session)
- **Validace na produkčních datech:** Ing. Andrlík (po deploy)
- **Spec review:** Ing. Andrlík (před writing-plans)
