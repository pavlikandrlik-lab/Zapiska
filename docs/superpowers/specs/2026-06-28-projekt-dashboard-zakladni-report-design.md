# Projektový dashboard — redesign, sub-projekt 1: Základní report

## Kontext a rozsah

Stávající projektový dashboard (`ProjectDashboard`) se předělává od základu. Cílem je **exportovatelný reporting** (tisk/PDF) o projektu — nejen souhrn za celý projekt, ale i řezy **per subsystém**, výkonnost, termínová disciplína, náklady a ServiceDesk.

Celý redesign je velký → **dekomponujeme** na sub-projekty, každý vlastní spec → plán → implementace:

1. **Sub-projekt 1 (TENTO spec): Základní report** — rychlý jeden průchod DB, počty/stavy/vyjádření/termínová disciplína. Zakládá architekturu (provider pattern), kterou reuse-ují další.
2. Sub-projekt 2: Analytický report (distribuce, histogramy, mediány, korelace, trendy, outliery).
3. Sub-projekt 3+: tematické vrstvy podle záložek karty záznamu — Externí vazby, Spolupráce, Harmonogram — a ServiceDesk/NES jako celek.
4. Samostatně: PDF/tisk export.

Tento spec řeší **jen sub-projekt 1**. Ostatní jsou „mimo rozsah" (viz konec).

## Architektura (základ pro celý redesign)

### Dva reporty oddělené výpočetní cenou
- **Základní report** — jeden rychlý průchod DB (agregace zvládnutelné v pár dotazech). Renderuje se okamžitě.
- **Analytický report** — náročnější, opakované průchody + výpočty v paměti. Počítá se odděleně (on-demand/cache). *(Sub-projekt 2.)*

Dělící princip je **výpočetní cena**, ne téma.

### Graf = jedna OOP jednotka (DRY + OOP)
Každý graf vyrábí vlastní třída — „chart provider" — se single responsibility = **podklady pro jeden graf**. Společný kontrakt:

```csharp
public interface IZakladniChartProvider
{
    string Key { get; }                       // stabilní identifikátor grafu
    ChartData Build(ZakladniDataset dataset);  // čistá funkce: dataset → podklady grafu
}
```

`ChartData` je **rendering-agnostický** kontrakt nesoucí podklady + sdělení:

```csharp
public sealed record ChartData
{
    public required string Key { get; init; }
    public required ChartKind Kind { get; init; }     // Bar | StackedBar | Pie | StatCards | Line
    public required string Title { get; init; }
    public string? Insight { get; init; }             // hlavní sdělení „na první pohled"
    public string? Note { get; init; }                // metodická poznámka / caveat
    public IReadOnlyList<ChartSeries> Series { get; init; } = [];
    public IReadOnlyList<string> Categories { get; init; } = []; // osa X / popisky
}
public sealed record ChartSeries(string Label, IReadOnlyList<double> Values, string? ColorToken = null);
public sealed record StatCard(string Label, string Value, string? Trend = null, string? Note = null);
```

(StatCards graf nese `IReadOnlyList<StatCard>` přes dedikovanou property — `ChartData` má variantu pro stat-karty; detail dořeší plán.)

### Sdílený snapshot → mnoho grafů z jednoho průchodu
```
ReportContext (projektId, Obdobi)
  → ZakladniDatasetLoader (1 sada dotazů, ohraničená projektem + obdobím)
       → ZakladniDataset (záznamy, stavy-katalog, vyjádření, historie-termínů, subsystémy, obsazení)
  → každý registrovaný IZakladniChartProvider: Build(dataset) → ChartData
  → poskládat ChartData do sekcí → ZakladniReportViewModel → render (+ pozdější PDF)
```

Providery **nesahají do DB** — dostanou hotový `ZakladniDataset`. Výhody: rychlost (1 batch místo N round-tripů), konzistence (stejný okamžik), testovatelnost (golden-vektor bez DB, jako harmonogram osa).

### Generický renderer
Jeden renderer mapuje `ChartKind` → vizuální komponenta (Bar/Pie/StackedBar/Line/StatCards). Přidání grafu = nový provider + registrace do sekce; renderer se nemění. Data-viz best-practice (čitelnost, sdělení) je v defaultech komponent + v `Insight` headlinu.

## ZakladniDataset (co se načte v jednom průchodu)

Ohraničeno `projektId` + `Obdobi`. Orientační obsah (přesné dotazy dořeší plán):
- **Záznamy** projektu: Id, SubsystemId, KategorieId, StavUkoluId, VlastnikId, DatumZalozeni, DatumUkonceni (= termín), CisloViditelne.
- **Katalog stavů** (CiselnikStavuUkolu): Id, Kod, Nazev, IsFinal — pro mapování na kýble.
- **Vyjádření**: ZaznamId, DatumVyjadreni — počty per záznam/období.
- **Historie termínů** (ZaznamHistorieTerminu): ZaznamId, starý/nový termín, datum změny — pro disciplínu.
- **Subsystémy** projektu: Id, Kod, Nazev (popisky os, řez).
- (Případně obsazení/vlastníci — dle potřeby grafů.)

## Období

- **Přednastavené**: rok, rok+kvartál.
- **Vlastní rozsah**: od–do s přepočítáním.
- Selektor je **jedna komponenta** nad oběma reporty (sdílená).
- **Ukotvení záznamu do období = „aktivní v období" (rozhodnuto):** záznam patří do období, pokud v něm „žil" — tj. překrývá interval ⟨start, end⟩. Sémantika: `DatumZalozeni ≤ end` A (záznam nebyl dokončen před `start`). „Datum dokončení" = kdy záznam vstoupil do koncového stavu (z historie stavů `ZaznamHistorieStavuZaznamu`); pokud nelze určit, bere se jako stále otevřený. *(Pro jediné vybrané období je to prostý filtr překryvu; multi-období trendy řeší analytický report.)*

## Obsah základního reportu — 5 jednotek

### 0. Hlavička reportu (ne graf)
Identita projektu (název, gestor, rámcová smlouva, IS napojení), období, počet subsystémů/lidí/aktivních záznamů. Kontext + „zdraví jedním pohledem".

### 1. Záznamy per subsystém — `records-per-subsystem`
- **Graf**: sloupec (subsystém × počet záznamů za období).
- **Sdělení**: kdo nese kolik práce.
- **Výpočet**: group-by SubsystemId nad záznamy v období; popisky z katalogu subsystémů.

### 2. Stavový rozpad — `record-status-breakdown`
- **Graf**: skládaný sloupec (per subsystém) nebo koláč (celek): Nezahájeno / Rozpracováno / Hotovo / Zrušeno.
- **Sdělení**: kolik je hotovo vs rozděláno vs nezačato.
- **Mapování stavů na kýble (rozhodnuto):**
  - `Hotovo` = IsFinal == true A Kod != „CANCEL".
  - `Zrušeno` = IsFinal == true A Kod == „CANCEL" (samostatný kýbl).
  - `Nezahájeno` = Kod == „NEW" (hardcoded konstanta — stavy jsou stabilní; izolovat do jedné konstanty `NotStartedStateCode`, ať je případná změna jednořádková).
  - `Rozpracováno` = ostatní non-final (vše IsFinal==false A Kod != „NEW").
  - Hotovo/Zrušeno se odvozuje z `IsFinal` (robustní); jen „NEW"/„CANCEL" jsou pojmenované konstanty.

### 3. Vyjádření — souhrn — `vyjadreni-summary`
- **Graf**: stat-karty (celkem vyjádření za období, ø na záznam) + sloupec per subsystém.
- **Sdělení**: kolik se komunikuje a kde.
- **Výpočet**: count vyjádření v období; ø = count / počet záznamů v období; group-by subsystém přes záznam.

### 4. Termínová disciplína — `deadline-discipline`
- **Graf**: stat-karty + volitelně sloupec per subsystém. **Sjednocená informace, NErozdělovat** (rozdělením se ztrácí výpověď) — ukazuje pohromadě:
  - **Dodržel původní termín** (počet/%) = záznam dokončen do *prvního* plánovaného termínu.
  - **Dodržel poslední platný termín** (počet/%) = dokončen do *aktuálního* (případně posunutého) termínu.
  - **Posuny**: u posunutých záznamů ø *kolikrát* posunul a ø *o kolik dní* (finální − původní termín).
- **Sdělení**: drží se termíny vůči původnímu plánu i vůči realitě, a jak moc se posouvalo.
- **Výpočet**: z `ZaznamHistorieTerminu` — původní termín = první záznam historie, poslední = aktuální `DatumUkonceni`; počet posunů = počet změn; dny = finální − původní; „dodržel" porovnává datum dokončení (vstup do koncového stavu) proti původnímu resp. poslednímu termínu.

## Soubory / struktura (DRY + OOP)

- `Services/ProjectDashboard/Zakladni/` — nová složka:
  - `IZakladniChartProvider.cs`, `ChartData.cs` (+ `ChartKind`, `ChartSeries`, `StatCard`)
  - `ZakladniDataset.cs`, `ZakladniDatasetLoader.cs`
  - `Providers/RecordsPerSubsystemProvider.cs`, `RecordStatusBreakdownProvider.cs`, `VyjadreniSummaryProvider.cs`, `DeadlineDisciplineProvider.cs`
  - `ZakladniReportBuilder.cs` (načte dataset, projede providery, poskládá sekce)
- Renderer + komponenty grafů na FE: **ručně psané SVG** komponenty (rozhodnuto) — jedna per `ChartKind` (Bar/StackedBar/Pie/Line/StatCards). Nula externích závislostí, perfektní tisk/PDF (vektor), plná kontrola stylu přes gov tokeny. Renderer mapuje `ChartData.Kind` → odpovídající SVG partial/komponentu.

## Testování

- Každý provider = **golden-vektor test** (vstup `ZakladniDataset` → očekávaný `ChartData`), bez DB — vzor jako `ScheduleBarLayoutCalculator`.
- `ZakladniDatasetLoader` = integrační test (SQL testcontainer) — ověří jeden průchod + správné ohraničení projektem/obdobím.
- Stavové mapování = jednotkový test nad konfiguračním mapperem (robustní vůči jiným kódům).

## Mimo rozsah (samostatné sub-projekty / specs)

- Analytický report (distribuce, histogramy, mediány, korelace, trendy, outliery).
- Tematické vrstvy: Externí vazby, Spolupráce, Harmonogram, ServiceDesk/NES.
- PDF/tisk export (zatím jen „ChartData + SVG je export-ready"; vlastní spec).

## Vyřešená rozhodnutí (zapracováno výše)

1. **Období** = „aktivní v období" (překryv rozsahu).
2. **Mapování stavů** = IsFinal pro Hotovo/Zrušeno (split dle `CANCEL`), konstanta `NEW` pro Nezahájeno, zbytek Rozpracováno.
3. **Zrušeno** = samostatný kýbl.
4. **Termínová disciplína** = stat-karty + **samostatný graf na metriku** (každá metrika má vlastní osu Y → vlastní sloupcový graf per subsystém: dodržení původního termínu, dodržení posledního termínu, ø dní posunu, ø počet posunů). *Revize 2026-06-30:* metriky mají nesouměřitelné jednotky (% vs dny vs počet), proto NEsdílejí jednu osu — každá vlastní graf.
5. **Charting** = **Apache ECharts, self-hosted/offline, SVG renderer** (vektor pro tisk/PDF). *Revize 2026-06-30:* původní rozhodnutí „ručně psané SVG" nahrazeno — budoucí analytické grafy (sub-projekt 2: distribuce, histogramy, mediány, korelace, trendy) by ruční engine zbytečně nafoukly; ECharts je odladěný, rozšířený, zvládne i analytiku. Server dál vyrábí rendering-agnostický `ChartData`, mění se jen renderer (server-SVG → klient-ECharts čtoucí `ChartData` JSON). Licence Apache-2.0, vendrováno do `wwwroot/lib/echarts/` (precedent: Quill).
6. **Hlavička reportu** = **jen název projektu** (revize 2026-06-30; bez gestora/rámcové smlouvy/počtů — ty se řeší v tematických sekcích níže).
