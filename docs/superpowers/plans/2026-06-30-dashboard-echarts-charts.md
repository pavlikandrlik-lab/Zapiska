# Dashboard základní report — ECharts renderer + konkrétní grafy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Vyměnit ručně psaný SVG renderer základního reportu za Apache ECharts (self-hosted, offline), uklidit mrtvý SVG kód a doplnit konkrétní grafy základního reportu.

**Architecture:** Server dál vyrábí rendering-agnostický `ChartData` (kontrakt + providery beze změny). Renderer se ale posune ze server-SVG na klient-ECharts: `_Chart.cshtml` vykreslí pro grafové druhy kontejner s `ChartData` serializovaným do JSON; JS modul podle `ChartKind` sestaví ECharts `option` a vykreslí (SVG renderer ECharts = vektor pro tisk). StatCards zůstávají HTML bez knihovny.

**Tech Stack:** ASP.NET MVC Razor, vanilla ESM JS (bez build kroku), Apache ECharts (self-hosted ESM build), System.Text.Json, xUnit + FluentAssertions.

## Global Constraints

- Charting engine: **Apache ECharts**, **self-hosted / offline** (žádné CDN), licence Apache-2.0. Vendrovat do `PmTracker.Web/wwwroot/lib/echarts/` (precedent: `wwwroot/lib/quill/`).
- ECharts **SVG renderer** (`renderer: 'svg'`) — vektor, ostrý tisk/PDF.
- **Vanilla ESM**, bez build kroku — importovat vendrovaný ESM build ECharts.
- `ChartData` (kontrakt) a `IZakladniChartProvider` (kontrakt) se **NEMĚNÍ** — mění se jen renderer.
- Grafové druhy přes ECharts: **Bar, StackedBar, Pie, Line**. **StatCards zůstávají HTML** (`_ChartStatCards.cshtml`).
- Hlavička reportu = **jen název projektu** (žádná další identita/počty).
- Sekce: **tematické** — `zaznamy` (záznamy + stavy), `vyjadreni`, `terminy`.
- Termínová disciplína (#4) = **stat-karty + samostatný graf na metriku** (každá metrika vlastní osa Y → vlastní bar graf per subsystém).
- Stavové mapování (z spec, rozhodnuto): Hotovo = `IsFinal && Kod != "CANCEL"`; Zrušeno = `IsFinal && Kod == "CANCEL"`; Nezahájeno = `Kod == "NEW"` (konstanta `NEW`); Rozpracováno = zbytek. Pojmenované konstanty jen `NEW`/`CANCEL`.
- Ukotvení záznamu do období = „aktivní v období" (překryv) — už implementováno v loaderu (neměnit sémantiku).
- TDD: providery golden-vektor (bez DB); view/JS file-text testy; loader integračně (Testcontainers SQL).
- Uživatel commituje sám. `git add` jen konkrétní soubory (nikdy `-A`).
- Spec `docs/superpowers/specs/2026-06-28-projekt-dashboard-zakladni-report-design.md` rozhodnutí „ručně psané SVG" je tímto plánem **nahrazeno** (ECharts). Spec amend = Task 0.

---

## File Structure

**Nové:**
- `PmTracker.Web/wwwroot/lib/echarts/echarts.esm.min.js` — vendrovaný ECharts (offline).
- `PmTracker.Web/wwwroot/lib/echarts/LICENSE` — Apache-2.0 licence.
- `PmTracker.Web/wwwroot/js/modules/dashboard/echartsRender.js` — klient renderer: `[data-echart]` → ECharts option → init (SVG) + resize.
- `PmTracker.Web/Services/ProjectDashboard/Zakladni/ChartJson.cs` — `ChartData` → klient JSON (kind jako string, camelCase).
- `PmTracker.Web/Services/ProjectDashboard/Zakladni/ZakladniStatusMapper.cs` — stav → kýbl (Nezahájeno/Rozpracováno/Hotovo/Zrušeno).
- `PmTracker.Web/Services/ProjectDashboard/Zakladni/Providers/` — providery (Task 7–11).
- Testy: `ChartJsonTests.cs`, `EchartsRenderModuleTests.cs`, `ZakladniStatusMapperTests.cs`, `*ProviderTests.cs`.

**Měněné:**
- `PmTracker.Web/Views/ProjectDashboard/Zakladni/_Chart.cshtml` — dispatch na ECharts kontejner / StatCards.
- `PmTracker.Web/Views/ProjectDashboard/Zakladni/Report.cshtml` — titulek = název projektu.
- `PmTracker.Web/wwwroot/js/modules/dashboard/zakladniReport.js` — po reloadu reportu re-init ECharts.
- `PmTracker.Web/wwwroot/js/modules/bootstrap.js` — import + init echartsRender.
- `PmTracker.Web/wwwroot/css/components/zakladni-report.css` — smazat mrtvé `.zr-bar-*`, doplnit `.zr-echart`.
- `PmTracker.Web/Services/ProjectDashboard/Zakladni/ZakladniDataset.cs` + `ZakladniDatasetLoader.cs` + `ZakladniReportViewModel.cs` — název projektu + datum dokončení.
- `PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs` — DI registrace providerů.

**Mazané (mrtvý kód):**
- `PmTracker.Web/Views/ProjectDashboard/Zakladni/_ChartBar.cshtml` (ručně psané SVG).
- CSS pravidla `.zr-bar-rect`, `.zr-bar-value`, `.zr-bar-label`.
- `ChartRendererTests` aserce na SVG markup (přepsat na ECharts kontejner).

---

## Task 0: Amend spec (charting = ECharts)

**Files:**
- Modify: `docs/superpowers/specs/2026-06-28-projekt-dashboard-zakladni-report-design.md`

- [ ] **Step 1:** V sekci „Vyřešená rozhodnutí" změnit bod 5 z „ručně psané SVG komponenty" na: „**Charting = Apache ECharts, self-hosted/offline, SVG renderer** (vektor pro tisk). Důvod změny 2026-06-30: budoucí analytické grafy by ruční engine zbytečně nafoukly; ECharts je odladěný, rozšířený, zvládne i analytiku." Přidat: hlavička = jen název projektu; #4 = samostatný graf na metriku.
- [ ] **Step 2: Commit** `git add docs/superpowers/specs/2026-06-28-projekt-dashboard-zakladni-report-design.md && git commit -m "docs(dashboard): spec amend — charting = Apache ECharts (offline)"`

---

## Task 1: Vendrovat ECharts (offline)

**Files:**
- Create: `PmTracker.Web/wwwroot/lib/echarts/echarts.esm.min.js`, `PmTracker.Web/wwwroot/lib/echarts/LICENSE`
- Test: `PmTracker.Tests.Unit/Dashboard/Zakladni/EchartsAssetTests.cs`

**Pozn.:** Soubor `echarts.esm.min.js` stáhnout jednou tam, kde je internet (oficiální release ECharts, dist `echarts.esm.min.js`, verze ≥ 5.5). Produkce běží offline → musí být ve `wwwroot/lib`. Pokud build prostředí nemá internet, soubor dodá uživatel.

- [ ] **Step 1: Write the failing test**
```csharp
using System.IO;
using FluentAssertions;
namespace PmTracker.Tests.Unit.Dashboard.Zakladni;
public sealed class EchartsAssetTests
{
    private static string Root() { var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PmTracker.sln"))) d = d.Parent; return d!.FullName; }
    [Fact] public void Echarts_esm_build_is_vendored_offline()
    {
        var path = Path.Combine(Root(), "PmTracker.Web", "wwwroot", "lib", "echarts", "echarts.esm.min.js");
        File.Exists(path).Should().BeTrue("ECharts musí být self-hostovaný (offline), žádné CDN");
        new FileInfo(path).Length.Should().BeGreaterThan(100_000, "vendrovaný bundle ECharts");
    }
}
```
- [ ] **Step 2: Run → FAIL** (soubor neexistuje). `dotnet test --filter EchartsAssetTests`
- [ ] **Step 3:** Stáhnout `echarts.esm.min.js` (ECharts dist) → uložit do `wwwroot/lib/echarts/`; přidat `LICENSE` (Apache-2.0).
- [ ] **Step 4: Run → PASS**
- [ ] **Step 5: Commit** `git add PmTracker.Web/wwwroot/lib/echarts PmTracker.Tests.Unit/Dashboard/Zakladni/EchartsAssetTests.cs`

---

## Task 2: ChartData → klient JSON (server)

**Files:**
- Create: `PmTracker.Web/Services/ProjectDashboard/Zakladni/ChartJson.cs`
- Test: `PmTracker.Tests.Unit/Dashboard/Zakladni/ChartJsonTests.cs`

**Interfaces — Produces:** `static string ChartJson.Serialize(ChartData data)` → JSON s `kind` jako string (camelCase), čitelný v JS.

- [ ] **Step 1: Write the failing test**
```csharp
using FluentAssertions;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;
namespace PmTracker.Tests.Unit.Dashboard.Zakladni;
public sealed class ChartJsonTests
{
    [Fact] public void Serialize_emits_kind_as_string_and_series()
    {
        var data = ChartData.ForSeries("k", ChartKind.Bar, "T",
            new[] { "A", "B" }, new[] { new ChartSeries("Počet", new double[] { 1, 2 }) });
        var json = ChartJson.Serialize(data);
        json.Should().Contain("\"kind\":\"Bar\"");
        json.Should().Contain("\"categories\":[\"A\",\"B\"]");
        json.Should().Contain("\"values\":[1,2]");
    }
}
```
- [ ] **Step 2: Run → FAIL** (ChartJson neexistuje).
- [ ] **Step 3: Implement**
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;
public static class ChartJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }, // ChartKind jako string
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    public static string Serialize(ChartData data) => JsonSerializer.Serialize(data, Options);
}
```
- [ ] **Step 4: Run → PASS**
- [ ] **Step 5: Commit**

---

## Task 3: `_Chart.cshtml` — ECharts kontejner místo SVG

**Files:**
- Modify: `PmTracker.Web/Views/ProjectDashboard/Zakladni/_Chart.cshtml`
- Modify: `PmTracker.Web/wwwroot/css/components/zakladni-report.css` (přidat `.zr-echart`)
- Test: `PmTracker.Tests.Unit/Dashboard/Zakladni/ChartRendererTests.cs` (přepsat)

**Interfaces — Consumes:** `ChartJson.Serialize` (Task 2). **Produces:** `<div class="zr-echart" data-echart data-chart-json="…">` pro Bar/StackedBar/Pie/Line; `_ChartStatCards` pro StatCards.

- [ ] **Step 1: Přepsat test** (`ChartRendererTests`): místo aserce SVG `<svg class="zr-bar">` ověřit, že `_Chart.cshtml` pro grafové druhy renderuje `data-echart` kontejner s `data-chart-json`, a že StatCards jdou přes `_ChartStatCards`. `_ChartBar.cshtml` se v dispatch už nepoužívá.
```csharp
// v ChartRendererTests:
[Fact] public void Chart_dispatch_renders_echart_container_for_graphs_and_statcards_partial()
{
    var v = Read("PmTracker.Web/Views/ProjectDashboard/Zakladni/_Chart.cshtml");
    v.Should().Contain("data-echart");
    v.Should().Contain("data-chart-json");
    v.Should().Contain("ChartJson.Serialize");
    v.Should().Contain("_ChartStatCards.cshtml");
    v.Should().NotContain("_ChartBar.cshtml"); // SVG bar je mrtvý
}
```
- [ ] **Step 2: Run → FAIL**
- [ ] **Step 3: Implement** `_Chart.cshtml`:
```cshtml
@model PmTracker.Web.Services.ProjectDashboard.Zakladni.ChartData
@using PmTracker.Web.Services.ProjectDashboard.Zakladni
<figure class="zr-chart" data-chart-key="@Model.Key">
    <figcaption class="zr-chart-caption">
        <span class="zr-chart-title">@Model.Title</span>
        @if (!string.IsNullOrWhiteSpace(Model.Insight)) { <span class="zr-chart-insight">@Model.Insight</span> }
    </figcaption>
    @if (Model.Kind == ChartKind.StatCards)
    {
        <partial name="~/Views/ProjectDashboard/Zakladni/_ChartStatCards.cshtml" model="Model" />
    }
    else
    {
        @* Bar/StackedBar/Pie/Line → klient ECharts (echartsRender.js). Data v JSON atributu. *@
        <div class="zr-echart" data-echart data-chart-json="@ChartJson.Serialize(Model)" role="img" aria-label="@Model.Title"></div>
    }
    @if (!string.IsNullOrWhiteSpace(Model.Note)) { <figcaption class="zr-chart-note muted">@Model.Note</figcaption> }
</figure>
```
- [ ] **Step 4:** Do `zakladni-report.css` přidat `.zr-echart { width: 100%; height: 280px; }` (ECharts potřebuje výšku kontejneru).
- [ ] **Step 5: Run → PASS** + `dotnet build` (Razor se přeloží).
- [ ] **Step 6: Commit**

---

## Task 4: `echartsRender.js` — klient renderer + wiring

**Files:**
- Create: `PmTracker.Web/wwwroot/js/modules/dashboard/echartsRender.js`
- Modify: `PmTracker.Web/wwwroot/js/modules/bootstrap.js` (import + init)
- Modify: `PmTracker.Web/wwwroot/js/modules/dashboard/zakladniReport.js` (re-init po reloadu)
- Test: `PmTracker.Tests.Unit/Dashboard/Zakladni/EchartsRenderModuleTests.cs` (file-text kontrakt)

**Interfaces — Produces:** `export function initEchartsReport(scope)` — najde `[data-echart]`, přečte `data-chart-json`, podle `kind` sestaví option, `echarts.init(el, null, { renderer: 'svg' }).setOption(option)`; resize handler.

- [ ] **Step 1: Write the failing test (kontrakt)**
```csharp
using System.IO; using FluentAssertions;
namespace PmTracker.Tests.Unit.Dashboard.Zakladni;
public sealed class EchartsRenderModuleTests
{
    private static string Read(string rel) { var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PmTracker.sln"))) d = d.Parent;
        return File.ReadAllText(Path.Combine(d!.FullName, rel.Replace('/', Path.DirectorySeparatorChar))); }
    [Fact] public void Module_imports_vendored_echarts_and_renders_svg_by_kind()
    {
        var js = Read("PmTracker.Web/wwwroot/js/modules/dashboard/echartsRender.js");
        js.Should().Contain("../../lib/echarts/echarts.esm.min.js");
        js.Should().Contain("renderer: \"svg\"");
        js.Should().Contain("data-echart");
        foreach (var k in new[] { "Bar", "StackedBar", "Pie", "Line" }) js.Should().Contain(k);
    }
    [Fact] public void Bootstrap_wires_echarts_report()
        => Read("PmTracker.Web/wwwroot/js/modules/bootstrap.js").Should().Contain("initEchartsReport");
}
```
- [ ] **Step 2: Run → FAIL**
- [ ] **Step 3: Implement** `echartsRender.js`:
```javascript
// dashboard/echartsRender.js — vykreslí ChartData kontejnery přes Apache ECharts (SVG).
import * as echarts from "../../lib/echarts/echarts.esm.min.js";

function optionFor(d) {
    const cats = d.categories || [];
    const series = d.series || [];
    switch (d.kind) {
        case "Bar":
            return { tooltip: {}, xAxis: { type: "category", data: cats }, yAxis: { type: "value" },
                series: series.map(s => ({ name: s.label, type: "bar", data: s.values })) };
        case "StackedBar":
            return { tooltip: {}, legend: {}, xAxis: { type: "category", data: cats }, yAxis: { type: "value" },
                series: series.map(s => ({ name: s.label, type: "bar", stack: "total", data: s.values })) };
        case "Line":
            return { tooltip: {}, xAxis: { type: "category", data: cats }, yAxis: { type: "value" },
                series: series.map(s => ({ name: s.label, type: "line", data: s.values })) };
        case "Pie": {
            const s0 = series[0] || { values: [] };
            return { tooltip: { trigger: "item" }, legend: {},
                series: [{ type: "pie", radius: "65%", data: cats.map((c, i) => ({ name: c, value: s0.values[i] ?? 0 })) }] };
        }
        default: return null;
    }
}

function renderOne(el) {
    if (el.dataset.echartReady === "true") return;
    let data; try { data = JSON.parse(el.dataset.chartJson || "{}"); } catch { return; }
    const option = optionFor(data);
    if (!option) return;
    if (el.clientWidth === 0) { window.requestAnimationFrame(() => renderOne(el)); return; } // lazy/hidden panel
    const chart = echarts.init(el, null, { renderer: "svg" });
    chart.setOption(option);
    el.dataset.echartReady = "true";
    el._echart = chart;
}

export function initEchartsReport(scope) {
    const root = scope instanceof Element ? scope : document;
    root.querySelectorAll("[data-echart]").forEach(renderOne);
}

let resizeBound = false;
export function bindEchartsResize() {
    if (resizeBound) return; resizeBound = true;
    window.addEventListener("resize", () => {
        document.querySelectorAll("[data-echart]").forEach(el => el._echart && el._echart.resize());
    });
}
```
- [ ] **Step 4:** `bootstrap.js` — přidat `import { bindEchartsResize, initEchartsReport } from "./dashboard/echartsRender.js";` a do `runInitializers`: `() => initEchartsReport(document), () => bindEchartsResize(),`.
- [ ] **Step 5:** `zakladniReport.js` — po `report.outerHTML = html;` zavolat re-init: importovat `initEchartsReport` a po reloadu `initEchartsReport(document.querySelector("[data-zakladni-report]"))`.
- [ ] **Step 6: Run → PASS** (oba testy).
- [ ] **Step 7: Commit**

---

## Task 5: Smazat mrtvý SVG kód

**Files:**
- Delete: `PmTracker.Web/Views/ProjectDashboard/Zakladni/_ChartBar.cshtml`
- Modify: `PmTracker.Web/wwwroot/css/components/zakladni-report.css` (smazat `.zr-bar-rect/.zr-bar-value/.zr-bar-label`)
- Test: `PmTracker.Tests.Unit/Dashboard/Zakladni/DeadSvgRemovedTests.cs`

- [ ] **Step 1: Write the failing test**
```csharp
using System.IO; using FluentAssertions;
namespace PmTracker.Tests.Unit.Dashboard.Zakladni;
public sealed class DeadSvgRemovedTests
{
    private static string Root() { var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "PmTracker.sln"))) d = d.Parent; return d!.FullName; }
    [Fact] public void Svg_bar_partial_is_gone()
        => File.Exists(Path.Combine(Root(), "PmTracker.Web/Views/ProjectDashboard/Zakladni/_ChartBar.cshtml".Replace('/', Path.DirectorySeparatorChar)))
            .Should().BeFalse("ruční SVG bar nahradil ECharts");
    [Fact] public void Dead_svg_bar_css_is_gone()
    {
        var css = File.ReadAllText(Path.Combine(Root(), "PmTracker.Web/wwwroot/css/components/zakladni-report.css".Replace('/', Path.DirectorySeparatorChar)));
        css.Should().NotContain(".zr-bar-rect");
    }
}
```
- [ ] **Step 2: Run → FAIL**
- [ ] **Step 3:** Smazat `_ChartBar.cshtml`; smazat z CSS řádky `.zr-bar-rect/.zr-bar-value/.zr-bar-label`.
- [ ] **Step 4: Run → PASS** + full unit suite (žádná regrese).
- [ ] **Step 5: Commit**

---

## Task 6: Hlavička = název projektu

**Files:**
- Modify: `ZakladniDataset.cs` (přidat `string ProjektNazev`), `ZakladniDatasetLoader.cs` (načíst `CelyNazev`), `ZakladniReportViewModel.cs` (nést název), `Report.cshtml` (titulek)
- Test: `ZakladniDatasetLoaderTests` (integrační — název) + `ChartRendererTests`/nový file-text na Report.cshtml

- [ ] **Step 1:** Test (file-text) že `Report.cshtml` renderuje `Model.ProjektNazev` jako nadpis `<h2 class="zr-report-title">`.
- [ ] **Step 2: FAIL**
- [ ] **Step 3:** `ZakladniDataset` + VM o `ProjektNazev`; loader načte `projekty.CelyNazev` (1 dotaz dle projektId); `Report.cshtml` přidá `<h2 class="zr-report-title">@Model.ProjektNazev</h2>` nad sekce.
- [ ] **Step 4: PASS** + integrační test loaderu (název neprázdný).
- [ ] **Step 5: Commit**

---

## Task 7: Provider `records-per-subsystem` (Bar)

**Files:**
- Create: `Services/ProjectDashboard/Zakladni/Providers/RecordsPerSubsystemProvider.cs`
- Modify: `DataStoreServiceCollectionExtensions.cs` (DI)
- Test: `RecordsPerSubsystemProviderTests.cs`

**Interfaces — Produces:** `IZakladniChartProvider` Key=`records-per-subsystem`, SectionKey=`zaznamy`, Order=10, Bar.

- [ ] **Step 1: Golden-vektor test**
```csharp
using FluentAssertions; using PmTracker.Web.Services.ProjectDashboard.Zakladni;
using PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;
namespace PmTracker.Tests.Unit.Dashboard.Zakladni;
public sealed class RecordsPerSubsystemProviderTests
{
    [Fact] public void Counts_records_per_subsystem()
    {
        var ds = new ZakladniDataset {
            Obdobi = Obdobi.Rok(2026),
            Subsystemy = new[] { new DatasetSubsystem(1, "A", "Subsystém A"), new DatasetSubsystem(2, "B", "Subsystém B") },
            Records = new[] {
                new DatasetRecord(1, 1, null, new DateTime(2026,1,1), new DateTime(2026,2,1)),
                new DatasetRecord(2, 1, null, new DateTime(2026,1,1), new DateTime(2026,2,1)),
                new DatasetRecord(3, 2, null, new DateTime(2026,1,1), new DateTime(2026,2,1)),
            }
        };
        var data = new RecordsPerSubsystemProvider().Build(ds);
        data.Kind.Should().Be(ChartKind.Bar);
        data.Categories.Should().Equal("Subsystém A", "Subsystém B");
        data.Series[0].Values.Should().Equal(2d, 1d);
    }
}
```
- [ ] **Step 2: FAIL** (provider neexistuje)
- [ ] **Step 3: Implement**
```csharp
namespace PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;
public sealed class RecordsPerSubsystemProvider : IZakladniChartProvider
{
    public string Key => "records-per-subsystem";
    public string SectionKey => "zaznamy";
    public int Order => 10;
    public ChartData Build(ZakladniDataset ds)
    {
        var countById = ds.Records.GroupBy(r => r.SubsystemId).ToDictionary(g => g.Key, g => (double)g.Count());
        var cats = ds.Subsystemy.Select(s => s.Nazev).ToList();
        var vals = ds.Subsystemy.Select(s => countById.GetValueOrDefault(s.Id)).ToList();
        return ChartData.ForSeries(Key, ChartKind.Bar, "Záznamy per subsystém", cats,
            new[] { new ChartSeries("Počet", vals) },
            insight: $"Celkem {ds.Records.Count} záznamů za období.");
    }
}
```
- [ ] **Step 4: PASS** + registrovat v DI: `services.AddScoped<IZakladniChartProvider, RecordsPerSubsystemProvider>();`
- [ ] **Step 5: Commit**

---

## Task 8: Stavový rozpad — mapper + Pie + StackedBar

**Files:**
- Create: `ZakladniStatusMapper.cs`, `Providers/RecordStatusPieProvider.cs`, `Providers/RecordStatusBreakdownProvider.cs`
- Modify: `DataStoreServiceCollectionExtensions.cs`
- Test: `ZakladniStatusMapperTests.cs`, `RecordStatusPieProviderTests.cs`, `RecordStatusBreakdownProviderTests.cs`

**Interfaces — Produces:** `enum StatusBucket { Nezahajeno, Rozpracovano, Hotovo, Zruseno }`; `static StatusBucket ZakladniStatusMapper.Bucket(DatasetState? state)`. Providery Key=`record-status-pie` (Pie, SectionKey=`zaznamy`, Order=20) a `record-status-breakdown` (StackedBar, Order=30).

- [ ] **Step 1: Mapper golden-vektor test** (4 kýble dle pravidel: IsFinal+CANCEL→Zruseno, IsFinal→Hotovo, Kod==NEW→Nezahajeno, jinak Rozpracovano; null state→Nezahajeno).
- [ ] **Step 2: FAIL**
- [ ] **Step 3: Implement** `ZakladniStatusMapper`:
```csharp
namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;
public enum StatusBucket { Nezahajeno, Rozpracovano, Hotovo, Zruseno }
public static class ZakladniStatusMapper
{
    public const string NotStartedStateCode = "NEW";
    public const string CancelStateCode = "CANCEL";
    public static StatusBucket Bucket(DatasetState? s)
    {
        if (s is null) return StatusBucket.Nezahajeno;
        if (s.IsFinal) return string.Equals(s.Kod, CancelStateCode, StringComparison.OrdinalIgnoreCase) ? StatusBucket.Zruseno : StatusBucket.Hotovo;
        return string.Equals(s.Kod, NotStartedStateCode, StringComparison.OrdinalIgnoreCase) ? StatusBucket.Nezahajeno : StatusBucket.Rozpracovano;
    }
}
```
- [ ] **Step 4: PASS**
- [ ] **Step 5: Pie provider test + impl** — celý projekt, 4 segmenty (Categories = názvy kýblů, Series[0].Values = počty). Kind=Pie.
- [ ] **Step 6: StackedBar provider test + impl** — Categories = subsystémy, Series = 4 (Nezahájeno/Rozpracováno/Hotovo/Zrušeno), každý Values per subsystém. Kind=StackedBar. (Stav záznamu = lookup `Stavy` dle `record.StavId`.)
- [ ] **Step 7: DI registrace obou** + PASS
- [ ] **Step 8: Commit**

---

## Task 9: Vyjádření — stats + per subsystém

**Files:**
- Create: `Providers/VyjadreniStatsProvider.cs` (StatCards), `Providers/VyjadreniPerSubsystemProvider.cs` (Bar)
- Modify: DI
- Test: `VyjadreniStatsProviderTests.cs`, `VyjadreniPerSubsystemProviderTests.cs`

**Produces:** Key=`vyjadreni-stats` (StatCards, SectionKey=`vyjadreni`, Order=10): „Celkem vyjádření", „ø na záznam". Key=`vyjadreni-per-subsystem` (Bar, Order=20): počet vyjádření per subsystém (vyjádření→záznam→subsystém).

- [ ] **Step 1–5:** Golden-vektory (ø = count / počet záznamů, ošetřit dělení nulou → 0), impl, DI, PASS, commit.

---

## Task 10: Dataset — datum dokončení (pro disciplínu)

**Files:**
- Modify: `ZakladniDataset.cs` (přidat `DatumDokonceni` per záznam — z historie stavů, vstup do koncového stavu), `ZakladniDatasetLoader.cs` (načíst z `ZaznamHistorieStavuZaznamu`)
- Test: `ZakladniDatasetLoaderTests` (integrační)

**Pozn.:** `DatasetRecord.DatumUkonceni` = TERMÍN (deadline), ne datum dokončení. Disciplína „dodržel" potřebuje datum, kdy záznam vstoupil do koncového stavu. Přidat `DateTime? DatumDokonceni` (null = stále otevřený) — z `ZaznamHistorieStavuZaznamu` (poslední přechod do stavu, jehož `IsFinal == true`).

- [ ] **Step 1: Integrační test** — seed záznam + historie stavů s přechodem do final → loader naplní `DatumDokonceni`.
- [ ] **Step 2: FAIL**
- [ ] **Step 3:** Rozšířit `DatasetRecord`/loader; dotaz na historii stavů ohraničený `taskRecordIds`.
- [ ] **Step 4: PASS** (Colima SQL).
- [ ] **Step 5: Commit**

---

## Task 11: Termínová disciplína — stats + grafy per metrika

**Files:**
- Create: `Providers/DeadlineDisciplineStatsProvider.cs` (StatCards) + `Providers/DeadlineCompliancePerSubsystemProvider.cs` (% dodržení posledního termínu, Bar) + `Providers/DeadlineOriginalCompliancePerSubsystemProvider.cs` (% dodržení původního, Bar) + `Providers/DeadlineAvgShiftDaysPerSubsystemProvider.cs` (ø dní posunu, Bar)
- Modify: DI
- Test: golden-vektory per provider

**Produces:** SectionKey=`terminy`. Disciplína se počítá z `TerminChanges` (původní = první `PuvodniTermin`; poslední = `record.DatumUkonceni`; počet posunů = počet změn; dny = poslední − původní) + `record.DatumDokonceni` („dodržel" = DatumDokonceni ≤ termín; null = nedokončeno → nepočítá se do „dodržel"). Každá metrika **vlastní graf** (vlastní osa Y).

- [ ] **Step 1–5:** Golden-vektory (přesné vektory pro: dodržel původní %, dodržel poslední %, ø dní, ø počet posunů), impl providerů, DI, PASS, commit. Order: stats=10, compliance-posledni=20, compliance-puvodni=30, avg-shift=40.

---

## Self-Review

**1. Spec coverage:**
- Hlavička (#0) → Task 6 (jen název). ✓
- Záznamy per subsystém (#1) → Task 7. ✓
- Stavový rozpad (#2, oboje pie+stacked) → Task 8. ✓
- Vyjádření (#3, stats+bar) → Task 9. ✓
- Termínová disciplína (#4, per-metrika grafy) → Task 10 (data) + Task 11. ✓
- Renderer = ECharts offline → Task 1–4. ✓ Dead-code → Task 5. ✓

**2. Placeholder scan:** Task 8/9/11 mají kroky „1–5/8" shrnuté — při exekuci rozepsat na bite-sized RED/GREEN dle vzoru Task 7 (golden-vektor → provider → DI → commit). Vzor je v Task 7 kompletní; ostatní providery ho kopírují s jinou agregací.

**3. Type consistency:** `ChartData.ForSeries/ForStatCards`, `IZakladniChartProvider {Key, SectionKey, Order, Build}`, `DatasetRecord/DatasetSubsystem/DatasetState/DatasetVyjadreni/DatasetTerminChange` — odpovídá existujícím kontraktům. Nové: `ChartJson.Serialize`, `ZakladniStatusMapper.Bucket`, `DatasetRecord.DatumDokonceni`, `ZakladniDataset.ProjektNazev`.

**Pořadí exekuce:** 0 → 1 → 2 → 3 → 4 → 5 (renderer + cleanup hotové a ověřitelné prázdným reportem), pak 6 → 7 → 8 → 9 → 10 → 11 (obsah). Po Task 4/5 jede ECharts s 0 providery (prázdný report); každý další provider přidá graf.
