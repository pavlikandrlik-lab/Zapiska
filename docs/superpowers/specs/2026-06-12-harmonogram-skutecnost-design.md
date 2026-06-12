# Harmonogram — kompletní přepis na datum-model + 10 fixních kroků

**Datum:** 2026-06-12
**Stav:** Návrh k odsouhlasení (před implementací)
**Rozsah:** Velký — přepis backendu harmonogramu. Frontend zůstává vizuálně; mění se jen tenká vrstva JS↔server.

---

## 0. Rozhodnutí (potvrzeno uživatelem)

1. **Vše na datumy** — plán i skutečnost jako **absolutní datumy**, **žádné offsety nikde**.
2. **Zrušit šablony a verzování** — zahodit DB část pro šablony/typy, nahradit **setupem 10 pevných kroků**.
3. **Data jsou zahoditelná** (4 harmonogramy) → DROP & recreate, **žádný backfill**.
4. Cíl: čistá DB + přehlednější kód; opravit dotčený kód + DB konektory (EF mapping) na novou verzi.

---

## 1. Nový datový model

### 1.1 Pevných 10 kroků = konstanta v kódu (žádná DB)
Statická definice `HarmonogramKrokDefinice[10]`: `Poradi (1–10)`, `Kod`, `Nazev`, `BarvaHex`, `JeManualni` (kroky 2,5,8,9), `HarvestPredikat` (které vytěžení mapuje na krok). Nahrazuje dnešní `DefaultHarmonogramKroky` a celý číselník.

### 1.2 Nová tabulka `zaznam_harmonogram_krok` (1 řádek = 1 krok záznamu, 10/záznam)
| Sloupec | Typ | Význam |
|---|---|---|
| `id` | int PK | |
| `zaznam_id` | int FK→projektove_zaznamy (CASCADE) | |
| `poradi` | tinyint (1–10) | který krok |
| `plan_datum` | date NULL | plánové datum **konce** kroku |
| `skutecnost_datum` | date NULL | skutečné datum kroku (**NULL = nenastal**) |
| `skutecnost_zdroj` | tinyint | Neznamo/Automat/Manual/Historicka |
| `skutecnost_rezim` | tinyint | Auto/Manual (switch auto-fill) |
| `preferred_externi_odkaz_id` | int NULL | volba kandidáta při více vazbách |
| `updated_at` | datetime2 | concurrency stamp |
| **UQ** | (zaznam_id, poradi) | 1 řádek na krok |

### 1.3 `zaznam_harmonogram_vyjadreni_vazby` — ZŮSTÁVÁ (úložiště kandidátů)
- Tady žijí **kandidáti**: N vazeb na krok = N kandidátů (externí odkaz + jeho datum + zdroj). Multi-kandidát zůstává plně podporovaný.
- Jediná změna: `krok_key` (Guid) → **`poradi`** (tinyint 1–10). Zbytek beze změny.

### 1.4 Kandidáti a výběr (multi-vazba) — jak to funguje
- 2+ externí záznamy → 2+ vazeb (kandidátů) na krok.
- **Default výběr:** časově **nejzazší (MAX)** pro kroky 3,4,6,7,10; **MIN** pro K1 (synthetic). *(zachováno z dneška)*
- **Override:** `zaznam_harmonogram_krok.preferred_externi_odkaz_id` = kandidát zvolený uživatelem v dropdownu (přebíjí default).
- **`skutecnost_datum` na řádku kroku = vyřešené (cached) datum** zvoleného kandidáta. Udržuje ho harvest/sync při změně vazeb. Slouží přímému reportingu (datum ve sloupci, bez join+resolve).
- Dropdown kandidátů v editoru se plní z vazeb přes `HarmonogramSkutecnostResolver` (beze změny logiky, jen `Poradi` místo `KrokKey`).
- Manuální kroky (2,5,8,9) nemají vazby → `skutecnost_datum` se zadá ručně (`rezim=Manual`).

### 1.5 Zahodit (DROP)
- tabulka `harmonogram_sablony`
- tabulka `ciselnik_harmonogram_typu`
- tabulka `zaznam_harmonogram_hodnoty` (nahrazena `zaznam_harmonogram_krok`)
- sloupec `projektove_zaznamy.harmonogram_sablona_verze` (+ FK)

---

## 2. Nový výpočet (jen datumy, bez offsetů)

Vstup: záznam `DatumZalozeni` (start), `DatumUkonceni` (termín), 10 řádků kroků (plan_datum, skutecnost_datum).

- **Plán segment kroku k** = `[ plan_datum(k-1) , plan_datum(k) ]`, `plan_datum(0) = DatumZalozeni`.
- **Skutečnost segment** (jen vyplněné): krok k = `[ skutecnost_datum(předchozí vyplněný) , skutecnost_datum(k) ]`, první vyplněný od `DatumZalozeni`. Nevyplněné se nekreslí (pohltí je další vyplněný). *(pojistka: nezáporná šířka)*
- **Plánové dokončení** = `plan_datum(10)`.
- **Skutečné dokončení** = `skutecnost_datum(poslední vyplněný)`; pokud **krok 10 nevyplněn** → projekce `max(poslední vyplněný, dnes)`.
- **Termín** = `DatumUkonceni`.
- **Překročení** = `max(0, Skutečné dokončení − Termín)` (dny, odvozené — neukládá se).
- **Stíháme** = `Skutečné dokončení ≤ Termín`.

`ScheduleTimelineCalculator` se tím **výrazně zjednoduší** — mizí kumulativní offset aritmetika; pracuje s absolutními daty.

---

## 3. Dotčený kód — co a kde

### Vrstva DATA
- **Entity** (`PmTrackerEntities.cs`): smazat `HarmonogramSablonaEntity`, `HarmonogramTypEntity`; nahradit `ZaznamHarmonogramHodnotaEntity` → `ZaznamHarmonogramKrokEntity`; upravit `ZaznamHarmonogramVyjadreniVazbaEntity` (KrokKey→Poradi); smazat `ProjektovyZaznamEntity.HarmonogramSablonaVerze`.
- **EF config** (`LookupEntityConfiguration.cs`, `RecordEntityConfiguration.cs`): smazat mapování šablon/typů; nové mapování `zaznam_harmonogram_krok`; uprava vazby; `PmTrackerDbContext` DbSety.
- **SQL** (nový `db_upgrade_*.sql`): DROP staré tabulky+sloupec, CREATE `zaznam_harmonogram_krok`, ALTER vazby. Bez backfillu.

### Vrstva VÝPOČET
- **`ScheduleTimelineCalculator`**: přepsat na datum-model (segmenty z absolutních dat). Zmenší se.
- **`HarmonogramService`**: smazat verzování/načítání schématu/cache; 10 kroků = konstanta; `BuildHarmonogramVypocet*`/`BuildHarmonogramSouhrn*` na datech.
- **`HarmonogramCatalogService`**: **smazat celé** (clone/version/CUD).

### Vrstva ČTENÍ (UI compose)
- `ProjectService.ScheduleComposition` (karta), `RecordEditorComposition` (editor), `ScheduleBlockComposition`, `SchedulePreviewService` (/Recalc): číst plan/skutecnost datumy, bez schemaCache.

### Vrstva ZÁPIS
- `RecordService.SaveRecord`: persist plan + skutečnost jako datumy (manual override, auto/manual switch).
- `RecordProposalService.DecisionCommands` + `RecordProposalPayloadMapper`: payload už dnes nese `AbsolutniDatum` → sjednotit.

### Vrstva HARVEST
- `HarmonogramSkutecnostSyncService`, `HarmonogramSkutecnostResolver`, `HarmonogramKrokDatumMapping`, `VyjadreniHarvestService`, `ManualActualKrokApplier`: ukládat **resolved datum přímo** (smazat konverzi datum→offset); vazby přes `Poradi`.

### Vrstva REPORTING
- `ProjectDashboardService` (NES), `HomeDashboardService`, `DashboardPriorityScoringService`: číst datumy (reporting se zjednoduší — to byl hlavní důvod).

### Vrstva ADMIN
- `CiselnikyController` + `DictionaryService` (harmonogram-kroky) + partial: **odebrat** editaci kroků harmonogramu.

### Vrstva FRONTEND (vizuál zůstává)
- `block.js`: preview kontrakt `delayDays→datum`; bar render z datumů; editor už dnes jede přes kalendář.

---

## 4. Fázování (TDD, sekvenčně)

1. **Datový model + DB + EF** — nová entita/tabulka/mapping, DROP starého. *(základ)*
2. **Konstanta 10 kroků + výpočet** — `ScheduleTimelineCalculator` + `HarmonogramService` na datech. Testy.
3. **Čtení/UI** — compose (karta/editor/block/preview) render z datumů.
4. **Zápis** — save flow + proposal flow.
5. **Harvest** — resolvery/sync/vazby na datumy + Poradi.
6. **Reporting** — dashboardy.
7. **Úklid** — smazat catalog service, admin UI, verzování; JS preview kontrakt.

Každá fáze: build zelený + testy, ověřit v běžící appce.

---

## 5. Odhad a rizika

- **Velikost:** velký přepis napříč ~20 soubory + 3 vrstvy (DB/BE/JS), ale **net úbytek kódu** (mizí verzování + offset konverze + katalog).
- **De-risk:** zahoditelná data → migrace = DROP/CREATE bez backfillu; po nasazení znovu vytěžit.
- **Nejcitlivější:** (a) save/auto-fill flow, (b) harvest concurrency + vazby přepis na Poradi, (c) ruční SQL migrace (pořadí DROP kvůli FK).
- **Hrubě:** několik soustředěných pracovních dávek po fázích; těžiště ve fázích 4–5 (zápis + harvest).

---

## 6. Otevřené otázky k potvrzení

1. **Plán per krok jako absolutní datum** (plan_datum konce kroku, start derivován z předchozího) — OK? *(navrženo)*
2. **Vazby vytěžení** přejdou z `KrokKey` (Guid) na `Poradi` (1–10) — OK?
3. **Audit sloupce** (zdroj/režim/preferred_externi_odkaz) zůstávají per krok — OK?
4. **Trvání ve dnech** se nikde neukládá, jen se odvozuje pro zobrazení (`plan_datum(k) − plan_datum(k-1)`) — OK?
