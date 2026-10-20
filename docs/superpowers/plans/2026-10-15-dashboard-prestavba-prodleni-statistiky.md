# Přestavba projektového dashboardu — implementační plán

> **Pro vykonavatele:** povinný sub-skill `superpowers:executing-plans`. Plán vykonávám sám inline, bez subagentů (paměť `feedback_inline_execution_no_subagents`). Kroky mají checkboxy (`- [ ]`).

**Revize 2026-10-19:** předávky a SLA hodiny se čtou přímo z databáze ServiceDesku (`ticket_external*`, spec §6). Textová rekonstrukce z vyjádření, diagnostický skript frází a konfigurace SLA hodin v `appsettings` odpadly.

**Cíl:** Záložku NES v prodlení pohání penalizační algoritmus z `Hotline.cs`. Dashboard dostane nový vzhled se souhrnným pásem a `pm-tabs`. Statistiky se stanou reportem pro projektového manažera nad ECharts.

**Architektura:** Čistý doménový engine (`PenaltyCalculator`, port 1:1) dostává předávky z tabulky `ticket_external_workflow` a SLA hodiny z `ticket_external_sla`, obojí přes druhý read-only kontext `ServiceDeskExternalReadOnlyDbContext`. Tickety a jejich příslušnost k IS dál dodává `TicketingReadOnlyDbContext` (`intranetNEW`). `ProdleniService` výsledky skládá a cachuje pro záložku NES, souhrnný pás, Statistiky a Excel. Statistiky rozšiřují existující Základní report (provider pattern, `ChartData` → ECharts) o nové providery a o slice dat ze ServiceDesku.

**Tech stack:** ASP.NET Core MVC (.NET), EF Core (PM DB + read-only `TicketingReadOnlyDbContext` + read-only `ServiceDeskExternalReadOnlyDbContext`), Razor partials, vanilla ESM JS, Apache ECharts (self-hosted), gov DS web komponenty, xUnit + FluentAssertions, Playwright (.NET bundled driver).

**Spec:** [docs/superpowers/specs/2026-10-15-dashboard-prestavba-prodleni-statistiky-design.md](../specs/2026-10-15-dashboard-prestavba-prodleni-statistiky-design.md)

**Referenční zdrojáky** (root repa, v `.gitignore`): `Hotline.cs` (výpočet penále), `Hotline (2).cs` (datová vrstva `DataLayer.Hotline`: `MOSD_External`, `TicketExternal_SlaCache`, modely), `Details.cshtml`.

**Úroveň detailu:** Plán uvádí soubory, přesná rozhraní, jmenované testy s konkrétními vstupy a očekávanými výstupy a příkazy. Implementace enginu je port 1:1 z `Hotline.cs`, takže referencí je zdroják (tabulka metod v Task 2) a plán jeho 640 řádků neopakuje. Plný kód je jen tam, kde je krátký a jednoznačný (svátky, ověření schématu).

## Global Constraints

- Barvy jen přes gov DS tokeny přes aliasy `--pm-*` v `:root` v `site.css`. Žádný hex v nových pravidlech a žádné nové `[data-theme="dark"]` pravidlo.
- Nosný layout jen flex/flex-wrap. `:has`/`max-content` smí být jen vylepšení, `:has` nevnořovat (Edge na i15).
- Nové statické CSS/JS se servírují s `charset=utf-8`. Ověř `Program.cs` StaticFiles `OnPrepareResponse`.
- Nový ESM modul s vedlejším efektem musí být explicitně importovaný v `wwwroot/js/modules/bootstrap.js`, jinak UI tiše selže.
- `gov-button`/`pm-button`: žádné `instanceof HTMLButtonElement`, žádné `.textContent` na hostu, stav řídit třídou na plain předkovi.
- Ticket bez `HOT_ZAZNAMY.id` je mimo rozsah. `typ_zaznamu` vždy `Trim().ToUpperInvariant()`.
- **Hodnotí se jen tickety s řádkem v `ticket_external`** (párování `external_id.Trim()` = `HOT_ZAZNAMY.id`, nečíselné `external_id` se ignoruje stejně jako v `MOSD_External.GetSyncedTickets`). Ostatní se neřeší a nikde nezobrazují.
- **„V prodlení" = ticket je právě u dodavatele** (poslední předávka 1/2 bez následné 3) **a má penalizované dny > 0.** Platí pro NES, PMP i PNF.
- SLA hodiny = přesná shoda `HOT_ZAZNAMY.dulezitost` s `ticket_external_sla.sla_name` (ordinal, bez ořezu), převod `Convert.ToInt32`. Chybějící řádek → „Nelze vyhodnotit", nikdy 0 h.
- Connection string `ServiceDeskExternalReadOnly` a čtecí právo zajišťuje správce. Kód se nesmí vázat na konkrétní server ani název databáze. Bez connection stringu běží `Disabled*` fallback.
- Akceptace kalkulace = zákazový seznam (helper v `SqlTicketingQueryService`), nikdy filtr na jednu hodnotu.
- Api testy kotvit na atributy a ASCII, ne na českou diakritiku.
- Dashboard projektu: `projektId` v route každého endpointu.
- Hotovou a otestovanou práci mergovat do lokálního `main`, push jen na pokyn.
- Uživateli psát česky, výstup testů hlásit česky.

## Review Focus

1. **Dvě předávky se stejným časem.** Pořadí musí být stabilní (`date`, pak `id` řádku workflow), jinak se obrátka zdvojí. Test v Task 2: `Stejny_cas_udalosti_razeni_podle_id`.
2. **`external_id` s mezerami nebo nečíselné.** Ticket se musí spárovat po `Trim()` a nečíselné hodnoty se tiše přeskočí, ne výjimka. Test v Task 4: `External_id_s_mezerami_a_nenumericke`.
3. **Projekt s IS, ale jedna z databází nedostupná nebo bez práva.** Záložka NES ukáže chybovou kartu a Statistiky ostatní sekce. Test v Task 5: `GetPrehled_pri_vyjimce_dotazu_vraci_stav_nedostupne`.
4. **Přepnutí tmavého režimu s otevřenými grafy.** Grafy se přebarví, bez reloadu. Ověří Playwright v Task 16 (snímek před a po přepnutí).
5. **Prázdné období ve Statistikách** (nový projekt, rok bez dat). Žádný graf nespadne na dělení nulou a karty ukážou `EmptyMessage`. Test v Task 12: `Vsechny_providery_na_prazdnem_datasetu_nespadnou`.

## Mapa souborů

**Nové**
- `PmTracker.ServiceDesk.Contracts/IProdleniQueryService.cs`, `Contracts/ProdleniTicketDto.cs`, `Contracts/WorkflowUdalostDto.cs`, `Contracts/SlaKatalogDto.cs`, `Contracts/AkceptovanaKalkulaceDto.cs`
- `PmTracker.ServiceDesk.Sql/ServiceDeskExternalReadOnlyDbContext.cs`, `Entities/TicketExternalEntity.cs`, `Entities/TicketExternalWorkflowEntity.cs`, `Entities/TicketExternalSlaEntity.cs`
- `PmTracker.ServiceDesk.Sql/SqlProdleniQueryService.cs`, `DisabledProdleniQueryService.cs`
- `scripts/dev/servicedesk_external_schema.sql` (lokální replika tří tabulek pro vývoj, jen schéma a smyšlená data)
- `PmTracker.Web/Services/ServiceDesk/Prodleni/`: `CeskeSvatky.cs`, `WorkflowUdalost.cs`, `PenaltyCalculator.cs`, `PenaltyResult.cs`, `PenaltyReason.cs`, `PenaltyReasonFormatter.cs`, `ProdleniService.cs`, `IProdleniService.cs`, `ProdleniPrehled.cs`
- `PmTracker.Web/Services/ProjectDashboard/DashboardSouhrnService.cs`
- `PmTracker.Web/Services/ProjectDashboard/Zakladni/ServiceDeskSlice.cs`
- `PmTracker.Web/Services/ProjectDashboard/Zakladni/Providers/`: `PrehledKpiProvider.cs`, `BurnUpProvider.cs`, `OpenTaskAgingProvider.cs`, `OwnerWorkloadProvider.cs`, `AttendancePerMonthProvider.cs`, `DeadlineCompliancePerSubsystemProvider.cs` (přepsaný na seskupený), `SdOpenTicketsByTypeProvider.cs`, `SdPenaltyBySupplierProvider.cs`, `SdPenaltyPerMonthProvider.cs`, `SdAcceptedCalculationsProvider.cs`
- `PmTracker.Web/Views/ProjectDashboard/_Souhrn.cshtml`, `_ProdleniTimeline.cshtml`, `_PanelError.cshtml`, `Zakladni/_ChartTable.cshtml`, `Zakladni/_Kpi.cshtml`
- `PmTracker.Web/wwwroot/css/components/project-dashboard.css`
- `PmTracker.Web/wwwroot/js/modules/dashboard/echartsTheme.js`, `dashboard/nesPanel.js`
- testy: `PmTracker.Tests.Unit/ServiceDesk/Prodleni/*`, `PmTracker.Tests.Unit/ServiceDesk/SqlProdleniQueryServiceTests.cs`, `PmTracker.Tests.Unit/Dashboard/Zakladni/*` (nové providery), `PmTracker.Tests.Api/ProjectDashboard/*`

**Upravené**
- `ProjectDashboardController.cs`, `ProjectDashboardService.cs` (+ interface), `ProjectDashboardViewModels.cs`
- `Views/ProjectDashboard/Index.cshtml`, `_RecordsPanel.cshtml`, `_NesPanel.cshtml`, `Zakladni/Report.cshtml`, `Zakladni/_Chart.cshtml`, `Zakladni/_ChartStatCards.cshtml`, `Zakladni/_ObdobiSelector.cshtml`
- `ChartData.cs`, `Obdobi.cs`, `ZakladniDataset.cs`, `ZakladniDatasetLoader.cs`, `ZakladniReportBuilder.cs`
- `echartsRender.js`, `zakladniReport.js`, `projectDashboard.js`, `bootstrap.js`
- `site.css` (`:root` grafové aliasy, smazání legacy `.dashboard-kpi-*`, `.dashboard-tab*`), `_Layout.cshtml` (link na nové CSS)
- `NesPanelExcelExportService.cs`, `DataStoreServiceCollectionExtensions.cs`, `ServiceDeskServiceCollectionExtensions.cs`, `appsettings.example.json`
- dokumentace: `docs/specs/dashboard-prodleni.md`, `docs/technical/12-servicedesk-schema-reference.md`, `docs/user-guide.md`, `install.md`

**Mazané**
- `Views/ProjectDashboard/_StatisticsPanel.cshtml`
- `ProjectDashboardService.BuildStatisticsPanelAsync` + `ComputeYearKpi` + `ComputeAttendanceRateAsync` + VM `ProjectDashboardStatisticsPanelViewModel`, `ProjectDashboardKpiViewModel`, `ProjectDashboardQuarterViewModel`, `ProjectDashboardSubsystemStatsViewModel`
- `Providers/DeadlineDisciplineStatsProvider.cs`, `VyjadreniStatsProvider.cs`, `DeadlineOriginalCompliancePerSubsystemProvider.cs` (sloučen)
- `IInformacniSystemQueryService.GetProdleneAsync` + `ProdlenyTicketDto` (jen když grep potvrdí, že jiný konzument není)
- `PmTracker.Tests.Unit/ProjectDashboard/ProjectDashboardStatisticsYearFilterTests.cs` a testy smazaných providerů

## Odhad pracnosti (hodiny inline exekuce Opus 5)

| Fáze | Tasky | Hodiny |
|---|---|---|
| 0 Spojovací sloupec | 0 | 0 |
| A Engine prodlení | 1–5 | 7–8,5 |
| B Shell, Záznamy, NES | 6–9 | 7–9 |
| C Statistiky | 10–14 | 8–10 |
| D Úklid, dokumentace, ověření | 15–16 | 2–3 |
| **Celkem** | | **25–30 h** |

Tvoje ruční ověření zvlášť: název spojovacího sloupce (~5 min), parita se ServiceDeskem (~1 h), závěrečný proklik (~0,5 h).

---

## Fáze 0 — spojovací sloupec

### Task 0: Název sloupce, který váže předávku na ticket

Jediná věc o databázi ServiceDesku, kterou z `Hotline (2).cs` nevyčtu: kód jde přes EF navigaci `ticket.ticket_external_workflow` (ř. 1069, 1218) a cizí klíč je schovaný v `.edmx`. Ostatní sloupce jsou v kódu vyjmenované (ř. 840–845, 1062–1115, 1216–1266).

- [ ] **Step 1:** Od tebe název sloupce v `ticket_external_workflow`, který ukazuje na `ticket_external.id` (SSMS: databáze → Tables → `ticket_external_workflow` → Keys / Columns). Zapíše se do spec §6.1.
- [ ] **Step 2: GATE pro Task 3.** Tasky 1–2 na tom nezávisí.

Typy číselných sloupců se nezjišťují: dotazy v Task 4 je převádějí `CAST(... AS int)` / `CAST(... AS float)`, takže na `tinyint`/`smallint`/`real` nezáleží.

---

## Fáze A — engine prodlení

### Task 1: České svátky

Port `MOSD_External.GetCzechHolidays` (`Hotline (2).cs` ř. 1137–1210), stejná pravidla i algoritmus Velikonoc.

**Files:**
- Create: `PmTracker.Web/Services/ServiceDesk/Prodleni/CeskeSvatky.cs`
- Modify: `PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs` (`services.AddSingleton<ICeskeSvatky, CeskeSvatky>();`)
- Test: `PmTracker.Tests.Unit/ServiceDesk/Prodleni/CeskeSvatkyTests.cs`

**Interfaces:**
- Produces:
  - `public interface ICeskeSvatky { bool JePracovniDen(DateTime den); }`
  - `public sealed class CeskeSvatky : ICeskeSvatky` (bezstavová, interně cache po rocích)

- [ ] **Step 1: Failing testy**

```csharp
public sealed class CeskeSvatkyTests
{
    private readonly CeskeSvatky _s = new();

    [Theory]
    [InlineData(2016, 3, 25)] [InlineData(2024, 3, 29)] [InlineData(2025, 4, 18)]
    [InlineData(2026, 4, 3)]  [InlineData(2027, 3, 26)] [InlineData(2030, 4, 19)]
    public void Velky_patek_od_2016_neni_pracovni(int y, int m, int d)
        => _s.JePracovniDen(new DateTime(y, m, d)).Should().BeFalse();

    [Fact] public void Velky_patek_2015_je_pracovni() // svátkem až od 2016
        => _s.JePracovniDen(new DateTime(2015, 4, 3)).Should().BeTrue();

    [Theory]
    [InlineData(2024, 4, 1)] [InlineData(2025, 4, 21)] [InlineData(2026, 4, 6)] [InlineData(2027, 3, 29)]
    public void Velikonocni_pondeli_neni_pracovni(int y, int m, int d)
        => _s.JePracovniDen(new DateTime(y, m, d)).Should().BeFalse();

    [Theory]
    [InlineData(1, 1)] [InlineData(5, 1)] [InlineData(5, 8)] [InlineData(7, 5)] [InlineData(7, 6)]
    [InlineData(9, 28)] [InlineData(10, 28)] [InlineData(11, 17)] [InlineData(12, 24)] [InlineData(12, 25)] [InlineData(12, 26)]
    public void Pevne_svatky_2026(int m, int d)
        => _s.JePracovniDen(new DateTime(2026, m, d)).Should().BeFalse();

    [Fact] public void Vikend_neni_pracovni()
        => _s.JePracovniDen(new DateTime(2026, 3, 7)).Should().BeFalse(); // sobota

    [Fact] public void Bezny_utery_je_pracovni()
        => _s.JePracovniDen(new DateTime(2026, 3, 3, 15, 30, 0)).Should().BeTrue(); // čas se ignoruje
}
```

- [ ] **Step 2: Spustit, ověřit FAIL**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~CeskeSvatky"`
Expected: FAIL kompilace (`CeskeSvatky` neexistuje)

- [ ] **Step 3: Implementace**

```csharp
namespace PmTracker.Web.Services.ServiceDesk.Prodleni;

/// <summary>Státní svátky ČR (zákon 245/2000 Sb.) — pevné + Velký pátek (od 2016) + Velikonoční pondělí.
/// Náhrada za DataLayer.Hotline.MOSD_External.GetCzechHolidaysForYears ze starého ServiceDesku.</summary>
public sealed class CeskeSvatky : ICeskeSvatky
{
    private static readonly (int M, int D)[] Pevne =
        [(1, 1), (5, 1), (5, 8), (7, 5), (7, 6), (9, 28), (10, 28), (11, 17), (12, 24), (12, 25), (12, 26)];

    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, HashSet<DateTime>> _cache = new();

    public bool JePracovniDen(DateTime den)
    {
        var d = den.Date;
        if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
        return !_cache.GetOrAdd(d.Year, Svatky).Contains(d);
    }

    private static HashSet<DateTime> Svatky(int rok)
    {
        var set = Pevne.Select(p => new DateTime(rok, p.M, p.D)).ToHashSet();
        var nedele = VelikonocniNedele(rok);
        set.Add(nedele.AddDays(1));                 // Velikonoční pondělí
        if (rok >= 2016) set.Add(nedele.AddDays(-2)); // Velký pátek
        return set;
    }

    // Anonymní gregoriánský algoritmus (Meeus/Jones/Butcher)
    private static DateTime VelikonocniNedele(int y)
    {
        int a = y % 19, b = y / 100, c = y % 100, d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30, i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451, month = (h + l - 7 * m + 114) / 31, day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateTime(y, month, day);
    }
}
```

- [ ] **Step 4: Spustit, ověřit PASS** (stejný příkaz). Expected: všechny testy zelené.
- [ ] **Step 5: Commit** `feat(prodleni): kalendář českých svátků (port MOSD_External.GetCzechHolidays)`

### Task 2: PenaltyCalculator — port 1:1

**Files:**
- Create: `Prodleni/WorkflowUdalost.cs`, `PenaltyReason.cs`, `PenaltyResult.cs`, `PenaltyCalculator.cs`, `PenaltyReasonFormatter.cs`
- Test: `PmTracker.Tests.Unit/ServiceDesk/Prodleni/PenaltyCalculatorTests.cs`, `PenaltyReasonFormatterTests.cs`

**Interfaces:**
- Consumes: `ICeskeSvatky` (Task 1), `TimeProvider`
- Produces:

```csharp
/// Kódy předávek 1:1 s PenaltyCalculation.TransferTypes v Hotline.cs. Hodnota zůstává int?,
/// protože ticket_external_workflow.transfertype může nést i jiné kódy, které originál ignoruje.
public static class TransferTyp { public const int U1 = 1, U2 = 2, U3 = 3, U5 = 5, U10 = 10, U11 = 11; }

public sealed record WorkflowUdalost(long Id, DateTime Datum, int? Typ, string? Dodavatel, DateTime? TerminParam);

/// SlaHodiny = ticket_external_sla.sla_hours pro dulezitost (null = řádek chybí). Potřebné jen pro NES.
public sealed record PenaltyInput(int TicketId, string TypZaznamu, string? Dulezitost, double? SlaHodiny,
    string? Dodavatel, IReadOnlyList<WorkflowUdalost> Udalosti);

public enum PenaltyReasonKod { T00, T01, T02, T03, T04, T05, T06, T07, T08, T11, T12, T13, T14 }
public sealed record PenaltyReason(PenaltyReasonKod Kod, DateTime? ObratkaOd = null, DateTime? ObratkaDo = null,
    DateTime? Deadline = null, DateTime? ProdlouzenoOd = null, DateTime? ProdlouzenoDo = null,
    int? PocetDni = null, double? ZbyvaHodin = null, double? SpotrebovanoHodin = null);

public enum PenaltyChyba { ZadneUdalosti, ChybiIniciacniUdalost, SlaChybiProDulezitost, NeznamyTyp }

public sealed record PenaltyResult(string? Dodavatel, IReadOnlyList<DateTime> PenalizovaneDny,
    IReadOnlyList<PenaltyReason> Duvody, PenaltyChyba? Chyba, bool PracovniDny)
{
    public int PocetDni => PenalizovaneDny.Count;
    public bool MaChybu => Chyba is not null;
}

public sealed class PenaltyCalculator
{
    public PenaltyCalculator(ICeskeSvatky svatky, TimeProvider time);
    public IReadOnlyList<PenaltyResult> Spocitej(PenaltyInput input);
    /// Pro „hrozí prodlení": kdy NES vyčerpá SLA (GetDeadline nad zbývajícími sekundami), resp. termín plnění PMP/PNF.
    public DateTime? AktualniDeadline(PenaltyInput input);
}

public static class PenaltyReasonFormatter { public static string Text(PenaltyReason r); } // česká souvětí pro UI/Excel
```

**Mapa portu (Hotline.cs → PmTracker):**

| Hotline.cs | Řádky | Nová metoda | Změna |
|---|---|---|---|
| `CalculatePenalty` | 1355–1393 | `Spocitej` | `TypZaznamu` Trim+Upper, chyby jako `PenaltyChyba` |
| `PrepareSortedWorkflowForMultipleSuppliers` | 1395–1418 | `RozdelPodleDodavatele` | beze změny |
| `PrepareCalculationResult` (NES/PMP/PNF) | 1420–1589 | `SpocitejNes`, `SpocitejSTerminem(pracovniDny)` | PMP a PNF sloučené (liší se jen `onlyWorkDays`). NES: `SlaHodiny == null` → `SlaChybiProDulezitost` místo `Convert.ToInt32(null) = 0` (spec §16 R6), jinak `Convert.ToInt32(SlaHodiny.Value)`. |
| `CheckProlongationPardon` | 1591–1605 | `ProminDoSchvalenehoTerminu` | — |
| `GetPenaltyDays` | 1616–1722 | `PenalizujObratku` | návrat `(List<DateTime>, PenaltyReason)` místo `ref` |
| `GetPenaltyDaysBetweenTwoDates` | 1740–1784 | `DnyMezi` | — |
| `GetProlongationSegment` | 1795–1832 | `NajdiProdlouzeni` | — |
| `GetDeadline` | 1841–1892 | `DeadlinePoSla` | — |
| `GetWorkflowTransferReturnDate` | 1894–1905 | `KonecObratky` | — |
| `GetWorkingSecondsBetweenDates` | 1907–1957 | `PracovniSekundy` | `DateTime.Now` → `TimeProvider` (lokální čas) |
| `IsWorkingDay` | 1959–1964 | `ICeskeSvatky.JePracovniDen` | — |

Řazení událostí: `OrderBy(Datum).ThenBy(Id)` (originál jen `OrderBy(Date)`, stabilní řazení nad pořadím z DB; `Id` to dělá deterministické).

- [ ] **Step 1: Failing golden testy.** Engine bere „teď" jako `time.GetLocalNow().DateTime`. Testy: `var t = new FakeTimeProvider(new DateTimeOffset(2026,3,25,12,0,0,TimeSpan.Zero)); t.SetLocalTimeZone(TimeZoneInfo.Utc);` (nezávislé na pásmu stroje; balíček `Microsoft.Extensions.TimeProvider.Testing` už Unit projekt má). NES II má `SlaHodiny = 16`. Helpery: `U(TransferTyp.U1, "2026-03-02 10:00", termin: "2026-03-05 23:59:59", dodavatel: "A")`, `Pmp(...)`, `Pnf(...)`, `Nes(...)`.

| Test | Vstup | Očekávání |
|---|---|---|
| `Pmp_pozde_kalendarni_dny_T05` | PMP: U1 02.03. 10:00 termín 05.03. 23:59:59; U3 09.03. 14:00 | 4 dny (06.–09.03.), důvod T05 |
| `Pnf_pozde_pracovni_dny_T05` | totéž jako PNF | 2 dny (06.03., 09.03.) |
| `Pnf_pres_velikonoce` | PNF: U1 01.04.2026 termín 02.04. 23:59:59; U3 08.04. | 2 dny (07.04., 08.04.) — 03.04. a 06.04. svátek |
| `Pmp_vcas_T03` | PMP: U1 02.03. termín 05.03. 23:59:59; U3 05.03. 18:00 | 0 dní, T03 |
| `Pmp_termin_o_pulnoci_00_00` | PMP: U1 02.03. termín 05.03. 00:00; U3 09.03. | 5 dní (05.–09.03.) — bez posunu o den |
| `Nes_v_ramci_sla_T01` | NES II: U1 02.03. 08:00; U3 02.03. 12:00 | 0 dní, T01 (zbývá 16 h, spotřebováno 4 h) |
| `Nes_prekroceni_sla_T05` | NES II: U1 03.03. 20:00; U3 05.03. 10:00 | deadline 04.03. 12:00, 2 dny (04.03., 05.03.), T05 |
| `Nes_po_vycerpani_dalsi_obratka_cela_T04` | předchozí + U2 09.03. 08:00; U3 11.03. 09:00 | celkem 5 dní (04., 05., 09., 10., 11.03.), druhá obrátka T04 |
| `Nes_sla_zaokrouhleni_convert_toint32` | NES, `SlaHodiny = 16.5`, jinak jako `Nes_prekroceni_sla_T05` | stejný výsledek jako pro 16 h (`Convert.ToInt32(16.5) == 16`) |
| `Nes_dulezitost_bez_sla_vraci_chybu_ne_vyjimku` | NES, `SlaHodiny = null` | `SlaChybiProDulezitost`, žádná výjimka, žádné dny |
| `Pmp_prodlouzeni_po_zadosti_vcas_T11_T14` | U1 02.03. termín 05.03. 23:59:59; U10 04.03.; U11 05.03. termín 12.03. 23:59:59; U3 10.03. | 0 dní, důvody [T11, T14] |
| `Pmp_otevreny_ticket_prominuti_T14` | U1 02.03. termín 05.03. 23:59:59; U11 06.03. termín 20.03. 23:59:59; bez U3; now 25.03. 12:00 | 5 dní (21.–25.03.), důvody [T02, T05, T14] |
| `Pmp_prodlouzeni_bez_zadosti_pozde_T07` | U1 02.03. termín 05.03. 23:59:59; U11 06.03. termín 08.03. 23:59:59; U3 10.03. | 2 dny (09.–10.03.), důvody [T07, T14] |
| `Pulnoc_23_59_59_pocita_od_dalsiho_dne` | PMP: termín 05.03. 23:59:59; U3 06.03. 00:30 | 1 den (06.03.) |
| `Den_se_nepenalizuje_dvakrat` | PMP: dvě obrátky překrývající stejný den po termínu | den v `PenalizovaneDny` jen jednou |
| `Vice_dodavatelu_T00` | U1 02.03. (A) termín 05.03. 23:59:59; U3 04.03.; U2 05.03. (B) termín 06.03. 23:59:59; U3 09.03. | 2 výsledky (A: 0 dní, B: 3 dny PMP 07.–09.03.), každý začíná T00 |
| `Stejny_cas_udalosti_razeni_podle_id` | U1 (id 10) a U3 (id 11) se stejným `Datum`, pak U2 | obrátky se vyhodnotí v pořadí id, výsledek nezávisí na pořadí ve vstupním seznamu |
| `Neznamy_kod_predavky_se_ignoruje` | PMP: U1, událost s `Typ = 4`, U3 | stejný výsledek jako bez události 4 |
| `Zadne_udalosti_chyba` | žádné události | 1 výsledek, `Chyba = ZadneUdalosti` |
| `Bez_U1_chyba` | jen U3 | `ChybiIniciacniUdalost` |
| `Typ_s_mezerami_char5` | `TypZaznamu = "PMP  "` | počítá jako PMP |
| `Neznamy_typ_chyba` | `TypZaznamu = "RU"` | `NeznamyTyp` |
| `AktualniDeadline_nes_u_dodavatele` | NES II: U1 23.03. 08:00, bez U3, now 25.03. 12:00 (spotřebováno 16+… h) | vrátí okamžik vyčerpání 16 h pracovního času |

Formatter: `PenaltyReasonFormatterTests` — pro každý kód jedna věta obsahující data ve formátu `dd.MM.yyyy HH:mm` a počet dní. T01 ukazuje hodiny na 2 desetinná místa (opravené dělení).

- [ ] **Step 2: Spustit, ověřit FAIL** — `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~PenaltyCalculator|FullyQualifiedName~PenaltyReasonFormatter"` → FAIL kompilace
- [ ] **Step 3: Implementace** podle mapy portu, metodu po metodě, se zdrojem otevřeným vedle. Každá větev `if` z originálu má odpovídající větev. Zakomentovaný blok T09/T10 (ř. 1690–1703) se **nepřenáší** a v kódu zůstane komentář s odkazem na spec §5.3.
- [ ] **Step 4: Spustit, ověřit PASS.** Když golden hodnota nesedí s ručním průchodem originálem, rozhoduje originál a test se opraví s komentářem „ověřeno průchodem Hotline.cs ř. …".
- [ ] **Step 5: Commit** `feat(prodleni): port penalizačního algoritmu ze ServiceDesku (NES/PMP/PNF)`

### Task 3: Read-only kontext databáze ServiceDesku *(až po gate Task 0)*

**Files:**
- Create: `PmTracker.ServiceDesk.Sql/ServiceDeskExternalReadOnlyDbContext.cs`, `Entities/TicketExternalEntity.cs`, `Entities/TicketExternalWorkflowEntity.cs`, `Entities/TicketExternalSlaEntity.cs`
- Create: `scripts/dev/servicedesk_external_schema.sql` (lokální replika: `CREATE TABLE` se sloupci z `Hotline (2).cs` a spojovacím sloupcem z Task 0 + 3 smyšlené tickety a SLA katalog, idempotentní `IF OBJECT_ID(...) IS NULL`)
- Modify: `PmTracker.ServiceDesk.Sql/ServiceDeskServiceCollectionExtensions.cs`, `PmTracker.Web/appsettings.example.json`
- Test: `PmTracker.Tests.Unit/ServiceDesk/ServiceDeskExternalReadOnlyDbContextTests.cs`

**Interfaces:**
- Produces (sloupce známé z `Hotline (2).cs`, spojovací sloupec z Task 0; čtení přes projekci s `CAST`, takže na přesném číselném typu nezáleží):

```csharp
internal sealed class TicketExternalEntity        // ticket_external
{
    public int Id { get; set; }                   // id
    public string? ExternalId { get; set; }       // external_id (= HOT_ZAZNAMY.id jako text)
    public int? ExternalSyncId { get; set; }      // external_sync_id
    public string? SupplierTag { get; set; }      // supplier_tag
    public string? SlaName { get; set; }          // sla_name (SLA dodavatele, jen zobrazení)
    public List<TicketExternalWorkflowEntity> Workflow { get; set; } = [];
}
internal sealed class TicketExternalWorkflowEntity // ticket_external_workflow
{
    public long Id { get; set; }                  // PK (typ podle Task 0)
    public int TicketExternalId { get; set; }     // FK na ticket_external.id (název sloupce podle Task 0)
    public DateTime Date { get; set; }            // date
    public int? TransferType { get; set; }        // transfertype
    public string? Supplier { get; set; }         // supplier
    public DateTime? DtParam { get; set; }        // dt_param
}
internal sealed class TicketExternalSlaEntity     // ticket_external_sla
{
    public string SlaName { get; set; } = "";     // sla_name
    public double SlaHours { get; set; }          // sla_hours
}
public sealed class ServiceDeskExternalReadOnlyDbContext : DbContext  // SaveChanges* vyhodí InvalidOperationException (vzor TicketingReadOnlyDbContext)
```

Konfigurace: `Ticketing:ExternalConnectionStringName` (výchozí `ServiceDeskExternalReadOnly`). Kontext se registruje jen při `Ticketing:Enabled = true` **a** neprázdném connection stringu. Jinak se `IProdleniQueryService` registruje jako `DisabledProdleniQueryService`. Do `appsettings.example.json`:

```json
"ConnectionStrings": {
  "ServiceDeskExternalReadOnly": "Server=<SQL_HOST>\\<INSTANCE>;Database=<SERVICEDESK_DB>;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;ApplicationIntent=ReadOnly"
},
"Ticketing": {
  "ExternalConnectionStringName": "ServiceDeskExternalReadOnly",
  "_comment_external": "Databáze starého ServiceDesku (connection string ServiceDeskEntities v jeho Web.config). Čte se jen ticket_external, ticket_external_workflow, ticket_external_sla."
}
```

(Ukázka se zástupnými hodnotami je stejná konvence jako stávající `TicketingReadOnly` v example souboru. Skutečnou hodnotu vyplňuje správce.)

- [ ] **Step 1: Failing testy:** `SaveChanges_vyhodi`, `Mapovani_tabulek_a_sloupcu` (EF model metadata: `GetTableName()` a `GetColumnName()` podle `Hotline (2).cs` a Task 0), `Bez_connection_stringu_registruje_disabled_query_service`, `S_connection_stringem_registruje_sql_query_service`
- [ ] **Step 2: FAIL** — `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ServiceDeskExternalReadOnly"`
- [ ] **Step 3: Implementace + lokální replika** (spustit `scripts/dev/servicedesk_external_schema.sql` na lokální Colima DB, paměť `feedback_local_dev_db_upgrade_self`)
- [ ] **Step 4: PASS**
- [ ] **Step 5: Commit** `feat(prodleni): read-only kontext databáze ServiceDesku (ticket_external, workflow, SLA)`

### Task 4: Dotazová služba prodlení

**Files:**
- Create: `PmTracker.ServiceDesk.Contracts/IProdleniQueryService.cs`, `Contracts/ProdleniTicketDto.cs`, `Contracts/WorkflowUdalostDto.cs`, `Contracts/SlaKatalogDto.cs`, `Contracts/AkceptovanaKalkulaceDto.cs`
- Create: `PmTracker.ServiceDesk.Sql/SqlProdleniQueryService.cs`, `DisabledProdleniQueryService.cs`
- Modify: `PmTracker.ServiceDesk.Sql/ServiceDeskServiceCollectionExtensions.cs`
- Test: `PmTracker.Tests.Unit/ServiceDesk/SqlProdleniQueryServiceTests.cs` (InMemory `TicketingReadOnlyDbContext` + InMemory `ServiceDeskExternalReadOnlyDbContext`, vzor testů `SqlInformacniSystemQueryService`)

**Interfaces:**
- Consumes: oba read-only kontexty (Task 3)
- Produces:

```csharp
public sealed record ProdleniTicketDto(int Id, string Pid, string TypZaznamu, string? Strucne, string? Dulezitost,
    string? Modul, string? Dodavatel, string? Stav, DateTime? Datum, int TicketExternalId, string? SupplierTag);

public sealed record WorkflowUdalostDto(long Id, DateTime Datum, int? TransferType, string? Supplier, DateTime? DtParam);
public sealed record SlaKatalogDto(string SlaName, double SlaHours);
public sealed record AkceptovanaKalkulaceDto(int TicketId, DateTime DatumAkceptace, decimal Cena);

public interface IProdleniQueryService
{
    /// Tickety IS (přes HOT_MODULY) s řádkem v ticket_external. Vždy nearchivované; když aktivniOd != null,
    /// navíc archivované, které mají předávku s date ≥ aktivniOd (pro Statistiky za období).
    Task<IReadOnlyList<ProdleniTicketDto>> GetSynchronizovaneTicketyAsync(int isId, DateTime? aktivniOd, CancellationToken ct);
    /// Předávky podle ticket_external.id, chunk po 1 000 id.
    Task<IReadOnlyDictionary<int, IReadOnlyList<WorkflowUdalostDto>>> GetWorkflowAsync(IReadOnlyCollection<int> ticketExternalIds, CancellationToken ct);
    /// Celá tabulka ticket_external_sla.
    Task<IReadOnlyList<SlaKatalogDto>> GetSlaKatalogAsync(CancellationToken ct);
    /// Akceptované kalkulace ticketů IS s datum_akceptace v ⟨od, do⟩ (zákazový seznam akceptace), nejvyšší verze.
    Task<IReadOnlyList<AkceptovanaKalkulaceDto>> GetAkceptovaneKalkulaceAsync(int isId, DateTime od, DateTime @do, CancellationToken ct);
}
```

Párování přes dvě databáze: z `intranetNEW` množina `HOT_ZAZNAMY.id` tiketů IS → v DB ServiceDesku `ticket_external` s `external_id.Trim()` v této množině (chunk ≤ 1 000, string porovnání) → v paměti vyřadit nečíselné `external_id` (`^\d+$` po Trim, jako `GetSyncedTickets`) → spojit. Duplicitní `external_id` → první podle `id` (originál bere `FirstOrDefault()`).

- [ ] **Step 1: Failing testy:**
  - `Jen_tickety_is_se_zaznamem_v_ticket_external` (ticket IS bez `ticket_external` vyřazen, ticket jiného IS vyřazen)
  - `External_id_s_mezerami_a_nenumericke` (`" 123456 "` se spáruje, `"ABC"` se tiše přeskočí) — Review Focus 2
  - `Archivovane_jen_s_predavkou_od` (archivovaný s předávkou ≥ `aktivniOd` zahrnut, starší ne, bez parametru žádný)
  - `Ticket_bez_id_vyrazen`
  - `Workflow_chunkuje_nad_1000` (2 500 id → všechny předávky)
  - `Sla_katalog_cely`
  - `Kalkulace_zakazovy_seznam_a_nejvyssi_verze` (`Návrh`/`Neakceptováno`/`Akceptovat ?` vyloučené, dvě verze → vyšší)
  - `Disabled_vraci_prazdne`
- [ ] **Step 2: FAIL** — `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ProdleniQueryService"`
- [ ] **Step 3: Implementace.** Filtr modulů jako `GetProdleneAsync`. Akceptace přes existující helper (zviditelnit ho jako `internal static` sdílenou funkci, žádná kopie seznamu).
- [ ] **Step 4: PASS**
- [ ] **Step 5: Commit** `feat(prodleni): dotazy na synchronizované tickety, předávky, SLA katalog a kalkulace`

### Task 5: ProdleniService (skladba, klasifikace, cache)

**Files:**
- Create: `Prodleni/IProdleniService.cs`, `ProdleniService.cs`, `ProdleniPrehled.cs`
- Modify: `DataStoreServiceCollectionExtensions.cs` (registrace scoped + `IMemoryCache`)
- Test: `PmTracker.Tests.Unit/ServiceDesk/Prodleni/ProdleniServiceTests.cs` (fake `IProdleniQueryService`, `FakeTimeProvider`, `FakeLogger`)

**Interfaces:**
- Consumes: Task 2 (`PenaltyCalculator`), Task 4 (`IProdleniQueryService`)
- Produces:

```csharp
public enum ProdleniStavDat { Ok, BezNapojeni, Nedostupne }

public sealed record ProdleniPolozka(int TicketId, string Pid, string TypZaznamu, string? Dulezitost, string? Strucne,
    string? Dodavatel, bool ViceDodavatelu, DateTime UDodavateleOd, DateTime? Termin, DateTime? ProdlouzenoDo,
    int PenalizovaneDny, IReadOnlyList<DateTime> PenalizovaneDnyDatumy, bool PracovniDny,
    IReadOnlyList<WorkflowUdalost> Udalosti, IReadOnlyList<PenaltyReason> Duvody);

public sealed record HroziPolozka(int TicketId, string TypZaznamu, string? Strucne, string? Dodavatel, DateTime Termin, TimeSpan Zbyva);
public sealed record NelzeVyhodnotitPolozka(int TicketId, string TypZaznamu, string? Strucne, string? Dulezitost, PenaltyChyba Chyba);
public sealed record SynchronizovanyTicket(int TicketId, string TypZaznamu, bool UDodavatele);       // pro Statistiky (vizuál 16)
public sealed record PenalizaceVObdobi(int TicketId, string? Dodavatel, IReadOnlyList<DateTime> Dny); // pro Statistiky (17, 18)

public sealed record ProdleniPrehled(ProdleniStavDat Stav, int? IsId, string? IsZkratka,
    IReadOnlyList<ProdleniPolozka> VProdleni, IReadOnlyList<HroziPolozka> Hrozi,
    IReadOnlyList<NelzeVyhodnotitPolozka> NelzeVyhodnotit,
    IReadOnlyList<SynchronizovanyTicket> Otevrene, IReadOnlyList<PenalizaceVObdobi> Penalizace)
{
    public int SoucetDni => VProdleni.Sum(p => p.PenalizovaneDny);
}

public interface IProdleniService
{
    Task<ProdleniPrehled> GetPrehledAsync(int projektId, CancellationToken ct);
    Task<ProdleniPrehled> GetPrehledProObdobiAsync(int projektId, DateTime obdobiOd, CancellationToken ct);
}
```

Pravidla:
- **U dodavatele** (pro daného dodavatele) = po seřazení je poslední událost z {1, 2, 3} typu 1 nebo 2. `UDodavateleOd` = její datum.
- **V prodlení** = u dodavatele a `PocetDni > 0`. Ticket zpět u nás v seznamu není (spec §10).
- **Hrozí** = u dodavatele, `PocetDni == 0`, NES: `AktualniDeadline − teď ≤ 48 h`, PMP/PNF: `termín − teď ≤ 5 dní`. Konstanty `HroziNesHodin = 48`, `HroziTerminDni = 5`.
- **Nelze vyhodnotit** = u dodavatele a výsledek s chybou. Chyba u ticketu, který u dodavatele není, se nezobrazuje.
- `SlaHodiny` pro engine = `katalog.TryGetValue(dulezitost)` se `StringComparer.Ordinal`. Chybějící hodnota se zaloguje `LogWarning` jednou za načtení katalogu (seznam chybějících důležitostí).
- Více dodavatelů = položka na dodavatele s `ViceDodavatelu = true`.
- Cache: přehled `prodleni:{isId}:{den:yyyyMMdd}:{obdobiOd?}` 10 min, SLA katalog `prodleni:sla` 1 h.
- Výjimka z `IProdleniQueryService` → `LogWarning` s isId, výsledek `Stav = Nedostupne`, necachovat.

- [ ] **Step 1: Failing testy:**
  - `Bez_is_vraci_BezNapojeni`
  - `Rozdeli_na_vprodleni_hrozi_nelze` (5 tiketů u dodavatele: 2 s penále, 1 hrozí NES 30 h, 1 hrozí PMP 3 dny, 1 bez U1)
  - `Vraceny_ticket_s_penale_neni_v_prodleni` (poslední událost U3, dny > 0 → není ve `VProdleni`, je v `Penalizace`)
  - `Chyba_mimo_dodavatele_se_nezobrazi`
  - `Vice_dodavatelu_dve_polozky`
  - `Razeni_podle_dni_sestupne`
  - `Chybejici_sla_zaloguje_jednou` (2 tickety se stejnou chybějící důležitostí → 1 warning)
  - `Druhe_volani_z_cache` (fake query počítá volání = 1)
  - `GetPrehled_pri_vyjimce_dotazu_vraci_stav_nedostupne` (Review Focus 3)
- [ ] **Step 2: FAIL** — `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ProdleniService"`
- [ ] **Step 3: Implementace**
- [ ] **Step 4: PASS**
- [ ] **Step 5: Commit** `feat(prodleni): služba přehledu prodlení s cache a degradací`

---

## Fáze B — shell dashboardu, Záznamy, NES v prodlení

### Task 6: Shell — hlavička, pm-tabs, souhrnný pás, CSS základ

**Files:**
- Create: `Services/ProjectDashboard/DashboardSouhrnService.cs`, `Views/ProjectDashboard/_Souhrn.cshtml`, `Views/ProjectDashboard/_PanelError.cshtml`, `wwwroot/css/components/project-dashboard.css`
- Modify: `ProjectDashboardController.cs` (+ `GET souhrn`), `ProjectDashboardViewModels.cs` (+ `DashboardSouhrnViewModel`, page VM + `IsZkratka`, `SouhrnUrl`), `ProjectDashboardService.BuildDashboardPageAsync`, `Index.cshtml`, `projectDashboard.js`, `bootstrap.js`, `_Layout.cshtml` (link CSS), `site.css` (smazat `.dashboard-tab`, `.dashboard-tab-panel`, `.dashboard-kpi-*`, ř. ~7315–7400; před smazáním `grep -rn` na každý selektor)
- Test: `PmTracker.Tests.Unit/ProjectDashboard/DashboardSouhrnServiceTests.cs`, `PmTracker.Tests.Api/ProjectDashboard/DashboardShellRenderTests.cs`

**Interfaces:**
- Consumes: `IProdleniService.GetPrehledAsync`, `IProjectDashboardService.BuildRecordsPanelAsync`
- Produces:

```csharp
public sealed record SouhrnDlazdice(string Klic, int? Hodnota, string Popisek, string? Doplnek, SouhrnStav Stav, string CilovaZalozka, string? CilovyFiltr);
public enum SouhrnStav { Ok, Varovani, Kriticke, Nedostupne }
public sealed class DashboardSouhrnViewModel { public IReadOnlyList<SouhrnDlazdice> Dlazdice { get; init; } = []; }
// DashboardSouhrnService.BuildAsync(int projektId, UserContext user, DateTime now, CancellationToken ct)
// Klíče: "ukoly-po-terminu" (records.view), "ceka-na-skutecnost" (records.view), "tickety-v-prodleni" (nes.view + IS), "hrozi-prodleni" (nes.view + IS)
```

Markup (`Index.cshtml`, kostra):

```html
<section class="pd-shell" data-project-dashboard-shell data-project-id="@Model.Projekt.Id">
  <header class="pd-header">
    <div class="pd-header__title"><h1>…</h1><gov-tag size="s">@Model.Projekt.Stav</gov-tag> <span class="pd-chip">IS: @Model.IsZkratka</span></div>
    <pm-button variant="Ghost" size="Small" href="@Model.BackUrl">Zpět na projekt</pm-button>
  </header>
  <div class="pd-souhrn" data-dashboard-souhrn data-url="@Model.SouhrnUrl"><!-- skeleton 4 dlaždic --></div>
  <pm-tabs persist="storage" persist-key="dashboard-@Model.Projekt.Id" initial="@Model.ActiveTab" data-dashboard-tabs>
    <pm-tab-list>
      <pm-tab-left key="zaznamy">Záznamy</pm-tab-left><pm-tab key="nes">NES v prodlení</pm-tab><pm-tab-right key="statistiky">Statistiky</pm-tab-right>
    </pm-tab-list>
    <pm-tab-panel key="zaznamy" data-dashboard-panel data-dashboard-panel-url="@Model.RecordsPanelUrl">…</pm-tab-panel>
    …
  </pm-tabs>
</section>
```

(Pořadí tag-names `pm-tab-left/pm-tab/pm-tab-right` převzít z `_SyncPanel.cshtml` `TabTagFor`. `?dashTab=` přebije `initial` a uložený stav.)

Dlaždice (`_Souhrn.cshtml`): `<a class="pd-tile pd-tile--{stav}" href="#" data-souhrn-tab="nes" data-souhrn-filter="">` s velkým číslem, popiskem a `gov-icon` (`exclamation-triangle` / `check-circle`). Stav nese i text (`<span class="visually-hidden">Kritické</span>`). CSS: `.pd-souhrn { display:flex; flex-wrap:wrap; gap: var(--pm-spacing-m); } .pd-tile { flex: 1 1 200px; border-left: 4px solid var(--pm-color-border); … } .pd-tile--kriticke { border-left-color: var(--pm-color-error); }`

JS (`projectDashboard.js`): lazy-load panelu na `pm-tab-change` (logika `ensureProjectDashboardTabPanelLoaded` zůstává, mění se jen resolvování panelu na `pm-tab-panel[key]`). Načtení souhrnu po `DOMContentLoaded`. Klik na dlaždici volá `tabs.setActive(key)`, po načtení panelu nastaví filtr a dispatchne `pm:dashboard-filter` `{ panel, filter }`. Smazat localStorage logiku (`projectDashboardTabStorageKeyPrefix`), tu převezme `pm-tabs`.

- [ ] **Step 1: Failing testy:**
  - Unit: `Souhrn_bez_nes_opravneni_nema_ticketove_dlazdice`, `Souhrn_bez_is_nema_ticketove_dlazdice`, `Souhrn_nula_je_Ok_stav`, `Souhrn_nedostupny_sd_dava_Nedostupne_a_null_hodnotu`
  - Api: `Index_renderuje_pm_tabs_se_tremi_klici` (atributy `key="zaznamy|nes|statistiky"`), `Souhrn_endpoint_vraci_dlazdice_s_data_souhrn_tab`, `Souhrn_endpoint_bez_pristupu_k_projektu_403`
- [ ] **Step 2: FAIL** — `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~DashboardSouhrn"` a `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~DashboardShell"`
- [ ] **Step 3: Implementace**
- [ ] **Step 4: PASS** + `grep -rn "dashboard-tab\b\|dashboard-kpi-" PmTracker.Web --include='*.cshtml' --include='*.js' --include='*.css'` → 0 výsledků
- [ ] **Step 5: Commit** `feat(dashboard): nový shell — hlavička, souhrnný pás, systémové pm-tabs`

### Task 7: Záložka Záznamy — vzhled a počty

**Files:**
- Modify: `_RecordsPanel.cshtml`, `ProjectDashboardViewModels.cs` (`ProjectDashboardRecordsPanelViewModel` + `PocetPodleKategorie`), `projectDashboard.js` (filtr reaguje i na `pm:dashboard-filter`), `project-dashboard.css`
- Test: `PmTracker.Tests.Api/ProjectDashboard/RecordsPanelRenderTests.cs`, `PmTracker.Tests.Unit/ProjectDashboard/ProjectDashboardRecordCategorizationTests.cs` (počty)

**Interfaces:**
- Produces: `IReadOnlyDictionary<DashboardRecordCategory,int> PocetPodleKategorie` (počítá se z `Records`, žádný nový dotaz)

Markup: filtr chipy `<pm-button … data-dashboard-category-filter="delayed">V prodlení <span class="pd-count">5</span></pm-button>`, pro 0 navíc `disabled`. Tabulka `.pd-table` v `.pd-card`, řádek `class="pd-row pd-row--delayed"` (levý pruh přes `box-shadow: inset 3px 0 0 var(--pm-color-error)`), odchylka `<strong class="pd-offset pd-offset--delayed">+14 dní</strong>`. Detail obsahuje `<pm-button href="/Zaznamy/Detail/@id">Otevřít záznam</pm-button>`. Prázdný stav `.pd-empty` s `gov-icon name="check-circle"`.

- [ ] **Step 1: Failing testy:** `Chipy_maji_pocty` (atribut `data-count`), `Nulova_kategorie_disabled`, `Detail_ma_odkaz_na_zaznam`, `Prazdny_stav_ma_pd_empty`
- [ ] **Step 2: FAIL** — `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~RecordsPanelRender"`
- [ ] **Step 3: Implementace**
- [ ] **Step 4: PASS**
- [ ] **Step 5: Commit** `feat(dashboard): záložka Záznamy — chipy s počty, karta tabulky, odkaz na záznam`

### Task 8: Záložka NES v prodlení — nový obsah

**Files:**
- Create: `Views/ProjectDashboard/_ProdleniTimeline.cshtml`, `wwwroot/js/modules/dashboard/nesPanel.js`
- Modify: `_NesPanel.cshtml` (přepis), `ProjectDashboardViewModels.cs` (nový `ProjectDashboardNesPanelViewModel` nad `ProdleniPrehled` + 2× `ChartData`), `ProjectDashboardService.BuildNesPanelAsync` (volá `IProdleniService`), `bootstrap.js` (import `nesPanel.js`)
- Test: `PmTracker.Tests.Unit/ProjectDashboard/ProjectDashboardServiceNesPanelTests.cs` (přepsat), `PmTracker.Tests.Api/ProjectDashboard/NesPanelRenderTests.cs`

**Interfaces:**
- Consumes: `IProdleniService`, `ChartData` (Task 10 rozšíří kinds — tady použít `ChartKind.Bar`, po Task 10 přepnout na `HorizontalBar`; nebo pořadí Task 10 → Task 8, viz pozn. níže)
- Produces:

```csharp
public sealed class ProjectDashboardNesPanelViewModel
{
    public required ProdleniPrehled Prehled { get; init; }
    public required int ProjektId { get; init; }
    public ChartData? DnyPodleDodavatele { get; init; }
    public ChartData? PodleTypu { get; init; }
}
```

> **Pořadí:** Task 10 (ChartData + téma ECharts) udělat před Task 8, aby NES grafy rovnou použily `HorizontalBar` a gov téma. Číslování nechávám kvůli čitelnosti fází.

Markup: KPI řada (`.pd-kpis` se 4 `.pd-kpi`), dva grafy (`<partial name="Zakladni/_Chart" model="…">`), filtrační řada (`data-nes-filter-typ="NES|PMP|PNF|"`), tabulka s řádky `data-typ` (v seznamu jsou jen tickety u dodavatele, filtr „běží" odpadl), rozbalovací `<tr hidden data-nes-detail>` s `_ProdleniTimeline` (SVG osa: obrátky jako úsečky, U11 jako kosočtverec, dnešek čárkovaně; pozice v % počítá server, stejný princip jako `data-schedule-ticks` z paměti `feedback_static_schedule_axis_needs_server_ticks`) a `<ol>` odůvodnění přes `PenaltyReasonFormatter.Text`. Pod tabulkou `<details>` „Hrozí prodlení (n)" a „Nelze vyhodnotit (n)". Stav `Nedostupne` = `_PanelError` s tlačítkem `data-dashboard-panel-retry`.

JS `nesPanel.js`: delegovaný `click`/`gov-change` z `document` (paměť `feedback_cloned_rows_need_delegation`), filtr typu, toggle detailu přes třídu `pd-row--open` na `<tr>`, poslech `pm:dashboard-filter`.

- [ ] **Step 1: Failing testy:**
  - Unit: `NesPanel_mapuje_prehled_a_grafy`, `NesPanel_graf_dodavatelu_serazeny_sestupne`, `NesPanel_nedostupne_bez_grafu`
  - Api: `Tabulka_ma_data_typ_a_u_dodavatele_od`, `Detail_obsahuje_timeline_svg`, `Nelze_vyhodnotit_sekce_s_poctem`, `Bez_is_placeholder_s_odkazem`, `Nedostupne_ma_retry`
- [ ] **Step 2: FAIL** — `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~NesPanel"` + `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~NesPanelRender"`
- [ ] **Step 3: Implementace**
- [ ] **Step 4: PASS**
- [ ] **Step 5: Commit** `feat(dashboard): NES v prodlení poháněné penalizačním algoritmem — KPI, grafy, časová osa, odůvodnění`

### Task 9: Excel export — penalizační sloupce

**Files:**
- Modify: `Services/Export/NesPanelExcelExportService.cs`, `ProjectDashboardController.NesPanelExport`
- Test: `PmTracker.Tests.Unit/Export/NesPanelExcelExportServiceTests.cs`

**Interfaces:**
- Consumes: `ProjectDashboardNesPanelViewModel` (Task 8)
- Produces: list „V prodlení" se sloupci: Ticket, Typ, Důležitost, Stručně, Dodavatel (+ „*" u více dodavatelů), U dodavatele od, Termín, Prodlouženo do, Penalizované dny, Typ dní, Odůvodnění (text + komentář buňky), Penalizované dny – data (komentář), Odkaz ServiceDesk. Druhý list „Nelze vyhodnotit" (Ticket, Typ, Důvod).

- [ ] **Step 1: Failing testy:** `Export_ma_sloupec_penalizovane_dny`, `Export_odůvodneni_v_komentari`, `Export_vice_dodavatelu_hvezdicka`, `Export_list_nelze_vyhodnotit`
- [ ] **Step 2: FAIL** — `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~NesPanelExcelExport"`
- [ ] **Step 3: Implementace**
- [ ] **Step 4: PASS**
- [ ] **Step 5: Commit** `feat(export): Excel NES v prodlení s penalizovanými dny a odůvodněním`

---

## Fáze C — Statistiky

### Task 10: ChartData rozšíření + gov téma ECharts + tabulkový pohled

**Files:**
- Create: `wwwroot/js/modules/dashboard/echartsTheme.js`, `Views/ProjectDashboard/Zakladni/_ChartTable.cshtml`, `Zakladni/_Kpi.cshtml`
- Modify: `ChartData.cs`, `ChartJson.cs`, `_Chart.cshtml`, `_ChartStatCards.cshtml`, `echartsRender.js`, `site.css` (`:root` aliasy), `project-dashboard.css` (`.zr-*` → karty), smazat `wwwroot/css/components/zakladni-report.css` (pravidla přesunout)
- Test: `PmTracker.Tests.Unit/Dashboard/Zakladni/ChartDataTests.cs`, `ChartJsonTests.cs`, `EchartsRenderModuleTests.cs` (rozšířit), `PmTracker.Tests.Api/ProjectDashboard/ChartTableRenderTests.cs`

**Interfaces:**
- Produces:

```csharp
public enum ChartKind { Bar, StackedBar, HorizontalBar, StackedHorizontalBar, Pie, Donut, Line, StatCards, Kpi }
public enum ChartSpan { Half, Full }
public enum TrendPolarity { HigherIsBetter, LowerIsBetter }
public enum CardStatus { Neutral, Good, Warning, Critical }
public sealed record CardTrend(double Delta, string Text, TrendPolarity GoodWhen) { public bool JeLepsi => GoodWhen == TrendPolarity.HigherIsBetter ? Delta > 0 : Delta < 0; }
public sealed record StatCard(string Label, string Value, string? Note = null, CardTrend? Trend = null, CardStatus Status = CardStatus.Neutral, double? Progress = null);
// ChartData + : public ChartSpan Span { get; init; } = ChartSpan.Half; public string? Unit { get; init; }; public string? EmptyMessage { get; init; }
//              public bool IsEmpty => Kind is ChartKind.StatCards or ChartKind.Kpi ? Stats.Count == 0 : Series.All(s => s.Values.All(v => v == 0));
```

Tokeny (`site.css` `:root`, hodnoty finálně podle validátoru):

```css
--pm-chart-1: var(--color-primary-600);
--pm-chart-2: var(--color-secondary-700);
--pm-chart-3: var(--color-primary-300);
--pm-chart-neutral: var(--color-neutral-400);
--pm-chart-good: var(--color-success-600);
--pm-chart-warning: var(--color-warning-600);
--pm-chart-critical: var(--color-error-600);
--pm-chart-grid: var(--border-subtle);
--pm-chart-axis-text: var(--text-secondary);
--pm-chart-surface: var(--background-page);
```

`echartsTheme.js`: `export function readTheme()` čte `getComputedStyle(document.documentElement)` → objekt ECharts tématu (`color: [chart-1..3]`, `textStyle.color`, osy, mřížka, `tooltip.backgroundColor = surface`). `ColorToken` série → `var(--…)` se resolvuje stejně. `echartsRender.js`: `optionFor` dostane `theme` a mapuje nové kinds (Donut `radius: ["55%","75%"]` + `graphic` text součtu uprostřed; HorizontalBar = prohozené osy, `inverse` kategorie; bary `itemStyle.borderRadius: [4,4,0,0]` resp. `[0,4,4,0]`, `borderColor: surface`, `borderWidth: 2`; čáry `lineStyle.width: 2`, `symbolSize: 8`; legenda jen při ≥ 2 sériích; `tooltip.valueFormatter` s `Unit` a `cs-CZ` formátem). `MutationObserver` na `document.documentElement` atribut `data-theme` → `chart.dispose(); renderOne(el)` pro všechny `[data-echart]`.

`_Chart.cshtml`: `<figure class="pd-card pd-card--@(span)">`, `IsEmpty` → `.pd-empty` s `EmptyMessage`, jinak graf + `<details class="pd-data"><summary>Zobrazit data</summary><partial _ChartTable/></details>`.

- [ ] **Step 1: Failing testy:** `ChartJson_serializuje_span_unit_trend`, `IsEmpty_pro_nulove_serie`, `CardTrend_JeLepsi_podle_polarity`, `ChartTable_renderuje_kategorie_a_serie` (Api, atributy `data-chart-table`), `EchartsRender_mapuje_vsechny_kinds` (stávající test čte JS jako text a hlídá větve — rozšířit o nové kinds a `MutationObserver`)
- [ ] **Step 2: FAIL** — `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~Dashboard.Zakladni"`
- [ ] **Step 3: Implementace**
- [ ] **Step 4: Validace palety**

Run: načíst skill `dataviz` a z jeho base directory spustit `node scripts/validate_palette.js "<hex chart-1>,<hex chart-2>,<hex chart-3>" --mode light`, totéž `--mode dark` s povrchem tmavého režimu (hex přečíst z vyřešených gov tokenů v prohlížeči přes `getComputedStyle`).
Expected: PASS (CVD ΔE ≥ 8 sousedních párů, normal-vision ≥ 15). Při FAIL posunout krok rampy (např. `--color-secondary-600/800`) a spustit znovu. Výsledek zapsat komentářem nad tokeny.

- [ ] **Step 5: PASS testů**
- [ ] **Step 6: Commit** `feat(statistiky): ChartData rozšíření, gov téma ECharts s tmavým režimem, tabulkový pohled grafů`

### Task 11: Rozšíření datasetu reportu

**Files:**
- Create: `Services/ProjectDashboard/Zakladni/ServiceDeskSlice.cs`
- Modify: `ZakladniDataset.cs`, `ZakladniDatasetLoader.cs`, `Obdobi.cs`
- Test: `PmTracker.Tests.Integration/DataStore/ZakladniDatasetLoaderTests.cs` (rozšířit), `PmTracker.Tests.Unit/Dashboard/Zakladni/ObdobiTests.cs`

**Interfaces:**
- Consumes: `IProdleniService.GetPrehledProObdobiAsync`, `IProdleniQueryService.GetAkceptovaneKalkulaceAsync`, `IInformacniSystemQueryService.GetRozpocetAsync`
- Produces:

```csharp
public sealed record DatasetRecord(int Id, int SubsystemId, int? StavId, DateTime DatumZalozeni, DateTime DatumUkonceni,
    DateTime? DatumDokonceni = null, int KategorieId = 0, int VlastnikId = 0);
public sealed record DatasetOsoba(int Id, string Jmeno);
public sealed record DatasetJednani(int Id, DateTime Datum);
public sealed record DatasetUcast(int JednaniId, bool Pritomen);
public sealed record ServiceDeskSlice(ProdleniPrehled Prodleni, IsRozpocetDto? Rozpocet, IReadOnlyList<AkceptovanaKalkulaceDto> Kalkulace);
// ZakladniDataset + : DateTime Dnes; IReadOnlyCollection<int> TaskCategoryIds; IReadOnlyList<DatasetOsoba> Vlastnici;
//                    IReadOnlyList<DatasetJednani> Jednani; IReadOnlyList<DatasetUcast> Ucasti; ServiceDeskSlice? ServiceDesk;
//                    IEnumerable<DatasetRecord> Ukoly => Records.Where(r => TaskCategoryIds.Contains(r.KategorieId));
// Obdobi + : public Obdobi Predchozi();
```

`Obdobi.Predchozi()`: rok → předchozí rok, kvartál → předchozí kvartál (Q1 → Q4 předchozího roku), rozsah → `[Start − délka, Start − 1 s]`. Rozlišení podle nové property `ObdobiDruh Druh`.

Loader: `Dnes` z `TimeProvider` (lokálně). Jednání projektu v období + předchozím, účast stav `PRESENT` → `Pritomen`. SD slice jen když projekt má `ServiceDeskInfoSystemId` a `Prodleni.Stav != BezNapojeni`. Při `Nedostupne` se slice vytvoří se stavem, aby sekce mohla ukázat chybovou kartu.

- [ ] **Step 1: Failing testy:**
  - Unit: `Predchozi_rok`, `Predchozi_Q1_je_Q4_minuleho_roku`, `Predchozi_rozsah_stejne_delky`
  - Integration: `Loader_nacte_kategorii_a_vlastnika`, `Loader_jednani_a_ucasti_v_obdobi`, `Loader_bez_is_ServiceDesk_null`
- [ ] **Step 2: FAIL** — `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ObdobiTests"` + `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~ZakladniDatasetLoader"`
- [ ] **Step 3: Implementace** (stávající providery přepnout z `Records` na `Ukoly`, golden testy upravit, protože filtr na úkoly je vědomá změna čísel)
- [ ] **Step 4: PASS**
- [ ] **Step 5: Commit** `feat(statistiky): dataset reportu — kategorie, vlastníci, jednání, slice ServiceDesku, předchozí období`

### Task 12: Nové providery — Přehled, Průběh, Termíny, Lidé

**Files:**
- Create: `Providers/PrehledKpiProvider.cs`, `BurnUpProvider.cs`, `OpenTaskAgingProvider.cs`, `OwnerWorkloadProvider.cs`, `AttendancePerMonthProvider.cs`
- Modify: `Providers/DeadlineCompliancePerSubsystemProvider.cs` (seskupený: 2 série „Původní termín", „Poslední termín", `Unit = "%"`), sekce a pořadí všech providerů (`SectionKey`: `prehled`, `prubeh`, `terminy`, `lide`, `servicedesk`), `Span` u stacked/owners = `Full`
- Delete: `DeadlineDisciplineStatsProvider.cs`, `VyjadreniStatsProvider.cs`, `DeadlineOriginalCompliancePerSubsystemProvider.cs` + jejich testy, registrace v `DataStoreServiceCollectionExtensions.cs`
- Test: `PmTracker.Tests.Unit/Dashboard/Zakladni/` — soubor na provider + `AllProvidersEmptyDatasetTests.cs`

**Interfaces:**
- Consumes: `ZakladniDataset` (Task 11), `ChartData`/`StatCard` (Task 10), `DeadlineDiscipline`
- Produces: `IZakladniChartProvider` implementace s klíči `prehled-kpi`, `burn-up`, `open-task-aging`, `owner-workload`, `attendance-per-month`, `deadline-compliance-per-subsystem`

Golden testy (dataset Q1 2026, `Dnes = 2026-03-31`):

| Test | Vstup | Očekávání |
|---|---|---|
| `PrehledKpi_otevrene_a_po_terminu` | 5 úkolů non-final, 2 s `DatumUkonceni < Dnes`; 1 informace non-final | karta „Otevřené úkoly" `Value="5"`, `Note="z toho 2 po termínu"`, `Status=Warning` |
| `PrehledKpi_dokonceno_trend` | 6 dokončeno v Q1, 4 v Q4 2025 | `Value="6"`, `Trend.Delta=+2`, `HigherIsBetter`, `JeLepsi` |
| `PrehledKpi_vcasnost_trend_dolu` | Q1 3/4 původní, Q4 4/4 | `"75,0 %"`, `Delta=-25`, `JeLepsi=false` |
| `PrehledKpi_bez_sd_jen_ctyri_karty` | `ServiceDesk = null` | 4 karty |
| `PrehledKpi_rozpocet_progress` | limit 1 000 000, čerpání 640 000 | `"64 %"`, `Progress=0.64` |
| `BurnUp_kumulativni_po_mesicich` | založeno: led 3, úno 2, bře 1; dokončeno: led 1, úno 2, bře 2 | kategorie `["01/2026","02/2026","03/2026"]`, série založeno `[3,5,6]`, dokončeno `[1,3,5]`, `Kind=Line` |
| `Aging_kose` | non-final úkoly stáří 10, 45, 120, 200, 400 dní | `[1,1,1,1,1]` v pořadí 0–30, 31–90, 91–180, 181–365, >365 |
| `OwnerWorkload_top10_a_ostatni` | 12 vlastníků | 11 kategorií (10 + „Ostatní"), série „V termínu"/„Po termínu", seřazeno podle celku sestupně, `Kind=StackedHorizontalBar` |
| `Attendance_procenta_po_mesicich` | led: 8/10 přítomno, úno: žádné jednání | `[80, 0]`, `Unit="%"`, `Note` vysvětlí 0 = bez jednání |
| `Compliance_seskupeny_dve_serie` | subsystém A: 2/4 původní, 3/4 poslední | série `[50]`, `[75]`, `Kind=Bar` |
| `Vsechny_providery_na_prazdnem_datasetu_nespadnou` | prázdný dataset, všechny zaregistrované providery | žádná výjimka, `IsEmpty || Stats.Count > 0`, `EmptyMessage` nastaven (Review Focus 5) |

- [ ] **Step 1: Failing testy** (tabulka výše)
- [ ] **Step 2: FAIL** — `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~Dashboard.Zakladni"`
- [ ] **Step 3: Implementace** (formát čísel `cs-CZ`, procenta `0,0 %`)
- [ ] **Step 4: PASS**
- [ ] **Step 5: Commit** `feat(statistiky): KPI přehled, burn-up, stáří úkolů, vytížení vlastníků, účast na jednáních`

### Task 13: Providery sekce ServiceDesk a dodavatelé

**Files:**
- Create: `Providers/SdOpenTicketsByTypeProvider.cs`, `SdPenaltyBySupplierProvider.cs`, `SdPenaltyPerMonthProvider.cs`, `SdAcceptedCalculationsProvider.cs`
- Modify: `ZakladniReportBuilder.cs` (sekce `servicedesk` při `ServiceDesk == null` → místo grafů jedna `ChartData` `StatCards` s `EmptyMessage` „Projekt není napojen na IS" a odkazem; při `Nedostupne` → „ServiceDesk teď neodpovídá")
- Test: `PmTracker.Tests.Unit/Dashboard/Zakladni/SdProvidersTests.cs`, `ZakladniReportBuilderTests.cs` (rozšířit)

**Interfaces:**
- Consumes: `ServiceDeskSlice` (Task 11), `ProdleniPrehled`/`ProdleniPolozka` (Task 5)
- Produces: klíče `sd-open-by-type`, `sd-penalty-by-supplier`, `sd-penalty-per-month`, `sd-accepted-calculations`

| Test | Očekávání |
|---|---|
| `OpenByType_stack_u_dodavatele_u_nas` | z `ProdleniPrehled.Otevrene` (jen synchronizované): kategorie `NES,PMP,PNF`, série „U dodavatele"/„U nás", `StackedBar` |
| `PenaltyBySupplier_jen_dny_v_obdobi_serazeno` | z `ProdleniPrehled.Penalizace` (i tickety už vrácené nebo archivované), dny mimo období se nezapočtou, sestupně, `HorizontalBar`, `Unit="dní"` |
| `PenaltyPerMonth_datovane_dny` | dny 28.02. a 02.03.–04.03. → únor 1, březen 3 |
| `AcceptedCalculations_soucet_po_mesicich_kc` | `Unit="Kč"`, součty po měsících |
| `Builder_bez_sd_jedna_informacni_karta` | sekce `servicedesk` má 1 chart s `EmptyMessage` |

- [ ] **Step 1–4:** failing → `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~SdProviders|FullyQualifiedName~ZakladniReportBuilder"` → implementace → PASS
- [ ] **Step 5: Commit** `feat(statistiky): sekce ServiceDesk a dodavatelé — tickety, penalizované dny, kalkulace`

### Task 14: Zapojení Statistik a smazání legacy

**Files:**
- Modify: `ProjectDashboardController.cs` (`StatisticsPanel` přijme `rok`, `kvartal`, `od`, `doDate` a vrátí `Zakladni/Report.cshtml`; endpoint `zakladni-report` smazat), `Zakladni/Report.cshtml` (sekce s nadpisy `h2` + popisky, flex-wrap mřížka karet), `_ObdobiSelector.cshtml` (URL → `statistics-panel`, popisek „Srovnání s: …" z `Obdobi.Predchozi().Label`, kvartál `selected`), `zakladniReport.js` (po výměně HTML `initEchartsReport`), `IProjectDashboardService` + `ProjectDashboardService` (smazat `BuildStatisticsPanelAsync` a pomocné typy), `ProjectDashboardViewModels.cs` (smazat statistické VM), `IInformacniSystemQueryService` (smazat `GetProdleneAsync` + `ProdlenyTicketDto`, pokud grep nenajde jiné použití)
- Delete: `_StatisticsPanel.cshtml`, `ProjectDashboardStatisticsYearFilterTests.cs`, testy `GetProdleneAsync`, CSS `.dashboard-year-select-label`, `.dashboard-quarters-table`, `.dashboard-subsystem-table`, `.dashboard-attendance-rate`
- Test: `PmTracker.Tests.Unit/ProjectDashboard/ProjectDashboardControllerBehaviorTests.cs`, `Dashboard/Zakladni/ZakladniReportWiringTests.cs`, `PmTracker.Tests.Api/ProjectDashboard/StatisticsRenderTests.cs`

Sekce (nadpis + jednovětý popis):
- „Přehled": hlavní čísla za období a srovnání s předchozím
- „Průběh práce": stíháme dokončovat, co přibývá
- „Termíny": plní se plán a kde se posouvá
- „Lidé a spolupráce": vytížení, účast, komunikace
- „ServiceDesk a dodavatelé": tickety, prodlení dodavatelů, objednávky

- [ ] **Step 1: Failing testy:** `StatisticsPanel_vraci_report_se_sekcemi` (Api: `data-section="prehled|prubeh|terminy|lide|servicedesk"`), `StatisticsPanel_kvartal_parametr`, `Zakladni_report_endpoint_neexistuje` (404), `Grep_zadny_StatisticsPanelViewModel` (wiring test čte zdroják)
- [ ] **Step 2: FAIL** — `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~StatisticsRender"` + `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ProjectDashboard|FullyQualifiedName~ZakladniReportWiring"`
- [ ] **Step 3: Implementace + mazání.** Před smazáním každého typu `grep -rn "<Typ>" --include='*.cs' --include='*.cshtml' .`. Do `project-dashboard.css` přidat tisk: `@media print { .pd-souhrn, pm-tab-list, .zr-obdobi, .pd-data { display: none; } .pd-card { break-inside: avoid; } pm-tab-panel:not([active]) { display: none; } }` (atribut aktivního panelu ověřit v `pmTabs.js`).
- [ ] **Step 4: PASS + celé Unit a Api suity** — `dotnet test PmTracker.Tests.Unit && dotnet test PmTracker.Tests.Api`. Expected: zelené (od 2026-10-08 je každé selhání regrese, paměť `project_pre_existing_test_failures`).
- [ ] **Step 5: Commit** `feat(statistiky): záložka Statistiky = nový report, odstranění legacy statistik a naivního prodlení`

---

## Fáze D — dokumentace a ověření

### Task 15: Dokumentace

**Files:**
- Modify: `docs/specs/dashboard-prodleni.md` (banner „Nahrazeno specem 2026-10-15" nahoře, definice prodlení přeškrtnout odkazem), `docs/technical/12-servicedesk-schema-reference.md` (§5.5 nová definice, nová sekce „Databáze ServiceDesku: ticket_external*" se schématem z Task 0, §7 „Penalizace U1–U11" → „Penále v Kč"), `docs/user-guide.md` (sekce Dashboard projektu: souhrnný pás, jak číst prodlení a odůvodnění, Statistiky), `docs/superpowers/specs/2026-10-15-…-design.md` (§6.1 doplnit schéma z Task 0), `install.md` / deploy checklist (connection string `ServiceDeskExternalReadOnly`, `Ticketing:ExternalConnectionStringName`, čtecí právo na `ticket_external`, `ticket_external_workflow`, `ticket_external_sla`)
- [ ] **Step 1:** úpravy
- [ ] **Step 2: Commit** `docs(dashboard): prodlení podle penalizačního algoritmu, Statistiky, konfigurace SLA`

### Task 16: Ověření v reálném prohlížeči + parita

- [ ] **Step 1: Celé suity** — `dotnet test PmTracker.Tests.Unit`, `dotnet test PmTracker.Tests.Api`, `dotnet test PmTracker.Tests.Integration`, E2E: porovnat jména selhání proti baseline worktree (paměť `project_e2e_baseline_failures`), ne počet.
- [ ] **Step 2: Lokální app** s env connection stringy (PM DB + lokální `intranetNEW` + lokální replika ze `scripts/dev/servicedesk_external_schema.sql`), dev DB upgradovaná. Playwright skript (bundled driver z E2E bin) projde: dashboard projektu s IS i bez IS; každou dlaždici souhrnu (přepne záložku a filtr); Záznamy (filtr, rozbalení, odkaz); NES (filtry, rozbalení timeline, Hrozí, Nelze vyhodnotit, export stáhne `.xlsx`); Statistiky (rok, kvartál, „Zobrazit data"); přepnutí tmavého režimu s otevřenými grafy (Review Focus 4); šířka 390 px bez horizontálního scrollu. Snímky světlý i tmavý do scratchpadu a poslat ti je.
- [ ] **Step 3: Parita se ServiceDeskem (s tebou).** Vyexportuješ „Dní penále" ze starého ServiceDesku pro 20–30 ticketů. Lokální skript porovná ticket × dodavatel × dny s výstupem `ProdleniService` (reálná data nejdou do gitu). Engine čte stejná data jako ServiceDesk, takže každý rozdíl je chyba portu: rozebrat po kódech T a opravit. Výjimkou jsou jen tickety s důležitostí bez SLA řádku, kde se záměrně liší (spec §16 R6).
- [ ] **Step 4: Merge do `main`** po zelených testech a tvém odsouhlasení paritního výsledku. Před nasazením zabalit `publish.zip`.
