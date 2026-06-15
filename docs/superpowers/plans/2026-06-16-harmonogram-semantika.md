# Harmonogram — sémantika Plán/Skutečnost + aktuální krok Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Zavést schválenou datum-model sémantiku harmonogramu (3 per-krok stavy, „aktuální krok", znaménkové Překročení, sjednocený stav v souhrnu, lišty táhnou skutečnost do dneška, pevné barvy + plán solid / skutečnost šrafa, chronologická validace) dle [docs/specs/harmonogram-plan-vs-skutecnost.md](../../specs/harmonogram-plan-vs-skutecnost.md).

**Architecture:** Čistá logika v `ScheduleDateCalculator` + `ScheduleBarLayoutCalculator` (unit-testovatelné, bez DB). `HarmonogramDateBlokBuilder` mapuje na ViewModely. Razor `_ScheduleBlock.cshtml` renderuje statická zobrazení ze server-vypočtených hodnot; `block.js` zrcadlí stejnou logiku jen pro editor live-preview. Chronologie validuje server (`ValidateScheduleValuesAsync`) + klient (`pm-date-field` min/max).

**Tech Stack:** .NET 8 / C# / EF Core, ASP.NET MVC Razor, xUnit + FluentAssertions (TDD), nativní JS ESM moduly + `<pm-date-field>` Custom Element, CSS (site.css).

**Prerekvizity / kontext (OVĚŘENO v kódu — nepřekvapí):**
- `BuildKroky` nemá `today` param → per-krok stav (Čeká vs V prodlení) vyžaduje jeho přidání.
- `HarmonogramKrokEditViewModel.SkutecneDatum` je **non-nullable** s fallbackem na `PlanEnd` ([HarmonogramDateBlokBuilder.cs:46](../../../PmTracker.Web/Services/Schedules/HarmonogramDateBlokBuilder.cs)); všechna jeho čtení v cshtml jsou guardovaná `OdchylkaDni.HasValue`, takže fallback se nikde nezobrazuje — odstranění je bezpečné, ale musí projít všemi čteními.
- **LATENT BUG (Phase 0):** editor zamyká VŠECHNY plán-date inputy (`locked="true"`), protože disabled logika v `_ScheduleBlock.cshtml` (ř. 269–285) gate-uje na mrtvém offset poli `krok.TrvaniTypId`/`ZpozdeniTypId`, které datum-model composition nikdy nenastaví → vždy 0 → vždy disabled. Ověřeno live (10× `locked="true"`).
- Overview actual segment má inline `background:@krok.BarvaHex`, který **přebíjí** CSS šrafu → overview skutečnost je teď plná, nerozliší se od plánu. Breakdown actual nemá inline background → CSS šrafa + delay barva (ne barva kroku).
- `pm-date-field` **nepodporuje** min/max (jen name/iso-value/display-value/locked/aria-label/container-css-class/clearable) → chronologie UI vyžaduje přidání.
- `Stihame` konzumenti (5): `_ScheduleBlock.cshtml:71-74`, `_ProjectScheduleTab.cshtml:64` (gov-tag), `ProjectService.ScheduleComposition.cs:86`, `ProjektHarmonogramUkolViewModel.Stihame`, `HarmonogramSouhrnViewModel.Stihame`.

---

## Soubory (mapa změn)

| Soubor | Zodpovědnost / změna |
|---|---|
| `PmTracker.Web/Services/Schedules/ScheduleDateCalculator.cs` | Nový `ScheduleDateSummary` shape (aktuální krok, znaménkové překročení, Dokonceno); `Summarize` přepočet; `ScheduleDateStepResult` + Compute: actual segment aktuálního kroku do dneška; per-step `Stav`. |
| `PmTracker.Web/Services/Schedules/HarmonogramKrokStav.cs` (NEW) | enum `HarmonogramKrokStav { Ceka, VProdleni, Splneno }`. |
| `PmTracker.Web/Services/Schedules/ScheduleBarLayoutCalculator.cs` | Segment aktuálního kroku: ActualWidth do dneška. |
| `PmTracker.Web/Services/Schedules/HarmonogramDateBlokBuilder.cs` | `BuildKroky(today)`, per-krok `Stav`, zrušit PlanEnd fallback, `BuildSouhrn` nové mapování. |
| `PmTracker.Web/Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs` | `HarmonogramKrokEditViewModel`: `Stav`, `SkutecneDatum` → `DateTime?`. `HarmonogramSouhrnViewModel`: nový shape. |
| `PmTracker.Web/Services/ProjectService.RecordEditorComposition.cs` | `today` do `BuildKroky`; Phase 0 disabled-logic fix; nový Souhrn. |
| `PmTracker.Web/Services/ProjectService.ScheduleComposition.cs` | `today` do `BuildKroky`; `Stihame` mapování z nového souhrnu. |
| `PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml` | Phase 0 fix; sjednocený souhrn (editor + mini); actual segment aktuálního kroku; breakdown stejně; pevná barva + šrafa přes CSS var; chronologie data-attrs. |
| `PmTracker.Web/wwwroot/css/site.css` | actual segment: barva kroku (`--seg-color`) + šrafa + nižší výška; plán solid. |
| `PmTracker.Web/wwwroot/js/modules/schedule/block.js` | `computeDateModel`/`renderSummary`/`renderOverview`/`renderBreakdown` na nový model + chronologie. |
| `PmTracker.Web/wwwroot/js/components/pm-date-field.js` | min/max atributy. |
| `PmTracker.Web/Services/RecordService.SaveRecord.cs` | `ValidateScheduleValuesAsync` chronologie. |
| Testy: `ScheduleDateCalculatorTests`, `HarmonogramDateBlokBuilderTests`, `ScheduleBarLayoutCalculatorTests`, `ScheduleBlockMarkupTests`, `ProjectHarmonogramRenderTests`, `HarmonogramUnifiedScenariosTests` (E2E) | aktualizace + nové. |

---

## Phase 0 — Latent bug: odemknout plán-date inputy v editoru

Bez tohoto je harmonogram needitovatelný a chronologie UI nemá smysl.

### Task 0.1: Migrace disabled logiky z TrvaniTypId na datum-model

**Files:**
- Modify: `PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml:266-330`
- Test: `PmTracker.Tests.Api/Controllers/ProjectHarmonogramRenderTests.cs`

- [ ] **Step 1: Failing test** — editor plán-date field NENÍ locked pro úkol s edit oprávněním.

```csharp
[Fact]
public async Task Edit_ScheduleTab_PlanDateFields_AreEditable_ForTaskWithEditPermission()
{
    // SeedDatumScheduleAsync založí úkol + krok rows (existující helper v tomto fixture).
    var (projectId, recordId) = await SeedDatumScheduleAsync();
    using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
    var html = await (await client.GetAsync($"/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}")).Content.ReadAsStringAsync();

    // Plán date pole (HarmonogramHodnoty[i].PlanDatum) musí být odemčená (locked="false").
    var planFields = Regex.Matches(html, "<pm-date-field\\b[^>]*HarmonogramHodnoty\\[\\d+\\]\\.PlanDatum[^>]*>");
    planFields.Count.Should().BeGreaterThan(0, "editor renderuje plán date pole");
    foreach (Match m in planFields)
        m.Value.Should().Contain("locked=\"false\"", "plán je v editoru editovatelný (regrese: TrvaniTypId gating)");
}
```

- [ ] **Step 2: Run → FAIL** (`locked="true"`).

Run: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "Edit_ScheduleTab_PlanDateFields_AreEditable"`
Expected: FAIL — pole jsou `locked="true"`.

- [ ] **Step 3: Implement** — v `_ScheduleBlock.cshtml` nahraď TrvaniTypId/ZpozdeniTypId gating datum-model permission logikou. Smaž řádky 269–275 (durationStaticDisabled/delayStaticDisabled/stepStaticDisabled/durationChanged/delayChanged/durationTooltip/delayTooltip — celé na TrvaniTypId/ZpozdeniTypId) a uprav canEdit bloky:

```csharp
// PŮVODNÍ (smazat): durationStaticDisabled, delayStaticDisabled, stepStaticDisabled,
// durationChanged, delayChanged, durationTooltip, delayTooltip — vše na TrvaniTypId/ZpozdeniTypId.
var canEditDuration = !Model.Permissions.IsPlanLocked
    && !Model.Permissions.IsScheduleLocked
    && Model.Permissions.CanEditDuration
    && (!Model.Permissions.IsAddOnlyMode || krok.TrvaniDni <= 0);
var dateDisabled = !Model.Permissions.IsTaskCategory || !canEditDuration;   // BEZ stepStaticDisabled
```

V `_AppDateField` volání (ř. ~314) `Locked = dateDisabled`. Odstraň `proposal-field-changed` třídy navázané na `durationChanged` (mrtvé) NEBO je nahraď `Model.EditorChangedTypeTooltips` lookupem dle `krok.KrokIndex`, pokud se changed-tracking používá (ověř: `EditorChangedTypeTooltips` klíč je teď `TrvaniTypId` — v datum-modelu nedává smysl; nastav `durationChanged = false`, tooltip null, dokud se proposal-diff nepředělá — mimo scope).

- [ ] **Step 4: Run → PASS.** Plus existující render testy zelené (`dotnet test ...ProjectHarmonogramRenderTests`).

- [ ] **Step 5: Commit** — `git commit -m "fix(harmonogram): editor plán date inputy odemčené (zrušit mrtvé TrvaniTypId gating)"`

---

## Phase 1 — Backend algoritmus (čistá logika, TDD)

### Task 1.1: enum HarmonogramKrokStav

**Files:** Create: `PmTracker.Web/Services/Schedules/HarmonogramKrokStav.cs`

- [ ] **Step 1:** napsat enum (žádný samostatný test — používá se v 1.3).

```csharp
namespace PmTracker.Web.Services.Schedules;

/// <summary>Per-krok stav harmonogramu (datum-model). Viz spec harmonogram-plan-vs-skutecnost.</summary>
public enum HarmonogramKrokStav
{
    /// <summary>Nevyplněno a plánované datum je v budoucnu (≥ dnes).</summary>
    Ceka = 0,
    /// <summary>Nevyplněno a plánované datum už ulpynulo (&lt; dnes).</summary>
    VProdleni = 1,
    /// <summary>Skutečnost vyplněna.</summary>
    Splneno = 2,
}
```

- [ ] **Step 2: Commit** — `git commit -m "feat(harmonogram): enum HarmonogramKrokStav (3 stavy)"`

### Task 1.2: ScheduleDateSummary nový shape + aktuální krok + znaménkové překročení

**Files:**
- Modify: `PmTracker.Web/Services/Schedules/ScheduleDateCalculator.cs`
- Test: `PmTracker.Tests.Unit/Schedule/ScheduleDateCalculatorTests.cs`

- [ ] **Step 1: Failing testy** (přidat; pokrývají hraniční případy ze specu):

```csharp
[Fact]
public void Summarize_aktualni_krok_je_prvni_nevyplneny_po_poslednim_vyplnenem()
{
    // vyplněno 1,2,3,4 (skutečnost), 5..10 prázdné → aktuální = 5
    var steps = Enumerable.Range(1, 10).Select(i => new ScheduleDateStep(
        i, new DateTime(2026, 1, 1).AddDays(i * 10),
        i <= 4 ? new DateTime(2026, 1, 1).AddDays(i * 10) : (DateTime?)null)).ToList();
    var s = ScheduleDateCalculator.Summarize(Start, steps, termin: new DateTime(2026,12,1), today: new DateTime(2026,3,1));
    s.AktualniKrokPoradi.Should().Be(5);
    s.Dokonceno.Should().BeFalse();
}

[Fact]
public void Summarize_aktualni_krok_preskoci_mezery_vyplneno_4_6_9_da_10()
{
    DateTime? F(int i) => (i is 4 or 6 or 9) ? new DateTime(2026, 1, 1).AddDays(i) : (DateTime?)null;
    var steps = Enumerable.Range(1, 10).Select(i => new ScheduleDateStep(
        i, new DateTime(2026, 1, 1).AddDays(i), F(i))).ToList();
    var s = ScheduleDateCalculator.Summarize(Start, steps, termin: new DateTime(2026,12,1), today: new DateTime(2026,3,1));
    s.AktualniKrokPoradi.Should().Be(10);
}

[Fact]
public void Summarize_prekroceni_je_dnes_minus_plan_aktualniho_kroku_znamenkove()
{
    // aktuální krok 5, plán(5) = 2026-02-10, dnes = 2026-02-15 → +5
    var steps = Enumerable.Range(1, 10).Select(i => new ScheduleDateStep(
        i, new DateTime(2026, 2, 1).AddDays(i), i <= 4 ? new DateTime(2026, 2, 1).AddDays(i) : (DateTime?)null)).ToList();
    var planP5 = new DateTime(2026,2,1).AddDays(5);
    var today = planP5.AddDays(5);
    var s = ScheduleDateCalculator.Summarize(Start, steps, termin: new DateTime(2026,12,1), today: today);
    s.PrekroceniDni.Should().Be(5);
}

[Fact]
public void Summarize_predstih_je_zaporne_prekroceni()
{
    var steps = Enumerable.Range(1, 10).Select(i => new ScheduleDateStep(
        i, new DateTime(2026, 2, 1).AddDays(i), i <= 4 ? new DateTime(2026, 2, 1).AddDays(i) : (DateTime?)null)).ToList();
    var planP5 = new DateTime(2026,2,1).AddDays(5);
    var s = ScheduleDateCalculator.Summarize(Start, steps, termin: new DateTime(2026,12,1), today: planP5.AddDays(-3));
    s.PrekroceniDni.Should().Be(-3);
}

[Fact]
public void Summarize_vsechny_vyplnene_je_dokonceno_a_prekroceni_nula()
{
    var steps = Enumerable.Range(1, 10).Select(i => new ScheduleDateStep(
        i, new DateTime(2026, 1, 1).AddDays(i), new DateTime(2026, 1, 1).AddDays(i))).ToList();
    var s = ScheduleDateCalculator.Summarize(Start, steps, termin: new DateTime(2026,12,1), today: new DateTime(2026,3,1));
    s.Dokonceno.Should().BeTrue();
    s.PrekroceniDni.Should().Be(0);
}

[Fact]
public void Summarize_zadny_vyplneny_aktualni_je_krok_1()
{
    var steps = Enumerable.Range(1, 10).Select(i => new ScheduleDateStep(
        i, new DateTime(2026, 1, 1).AddDays(i), (DateTime?)null)).ToList();
    var s = ScheduleDateCalculator.Summarize(Start, steps, termin: new DateTime(2026,12,1), today: new DateTime(2026,1,5));
    s.AktualniKrokPoradi.Should().Be(1);
}
```

- [ ] **Step 2: Run → FAIL** (členy `AktualniKrokPoradi`/`Dokonceno` neexistují, kompilace selže).

- [ ] **Step 3: Implement** — nový `ScheduleDateSummary` + `Summarize`:

```csharp
/// <summary>Souhrn harmonogramu (datum-model, sjednocený stav).</summary>
public sealed record ScheduleDateSummary(
    DateTime PlanoveDokonceni,
    DateTime Termin,
    int? AktualniKrokPoradi,   // null = vše vyplněno (Dokončeno)
    int PrekroceniDni,         // dnes − plán(aktuální krok), znaménkově; 0 když Dokončeno
    bool Dokonceno);
```

```csharp
public static ScheduleDateSummary Summarize(
    DateTime start, IReadOnlyList<ScheduleDateStep> steps, DateTime termin, DateTime today)
{
    var ordered = steps.OrderBy(x => x.Poradi).ToList();
    var normTermin = termin.Date;
    var normToday = today.Date;
    var planoveDokonceni = ordered.Count == 0 ? start.Date : Compute(start, ordered)[^1].PlanEnd;

    // Aktuální krok = první nevyplněný PO posledním vyplněném.
    int? posledniVyplneny = null;
    for (var i = ordered.Count - 1; i >= 0; i--)
        if (ordered[i].SkutecnostDatum.HasValue) { posledniVyplneny = ordered[i].Poradi; break; }

    int? aktualni = null;
    foreach (var st in ordered)
        if (!st.SkutecnostDatum.HasValue && (posledniVyplneny is null || st.Poradi > posledniVyplneny))
        { aktualni = st.Poradi; break; }

    if (aktualni is null)   // vše vyplněno
        return new ScheduleDateSummary(planoveDokonceni, normTermin, null, 0, Dokonceno: true);

    // plán aktuálního kroku z Compute (PlanEnd), znaménkové překročení vůči dnešku
    var computed = Compute(start, ordered).First(x => x.Poradi == aktualni.Value);
    var prekroceni = (normToday - computed.PlanEnd.Date).Days;
    return new ScheduleDateSummary(planoveDokonceni, normTermin, aktualni, prekroceni, Dokonceno: false);
}
```

> **Pozn.:** `ScheduleDateSummary` ZTRÁCÍ `SkutecneDokonceni` a `Stihame`. Konzumenti (`HarmonogramDateBlokBuilder.BuildSouhrn`, `ScheduleComposition`) se upraví v Task 1.5 a Phase 2. Build bude dočasně rozbitý mezi 1.2 a 1.5 — proto je Phase 1 commitnutá až po 1.5 (jeden ucelený commit), NEBO Summarize dočasně vrací i staré členy. **Zvolená cesta:** ponech `Compute` beze změny, a uvnitř `BuildSouhrn` (1.5) si „skutečné dokončení" už nepočítej. Aby build prošel po každém tasku, proveď 1.2→1.5 jako jednu sekvenci a commitni až na konci 1.5.

- [ ] **Step 4: Run → PASS** (po 1.5, kdy build prochází).

### Task 1.3: Per-krok Stav v Compute/StepResult + actual segment aktuálního kroku do dneška

**Files:**
- Modify: `ScheduleDateCalculator.cs` (`ScheduleDateStepResult`, `Compute`)
- Test: `ScheduleDateCalculatorTests.cs`

- [ ] **Step 1: Failing testy:**

```csharp
[Fact]
public void Compute_stav_ceka_vprodleni_splneno()
{
    var steps = new[] {
        new ScheduleDateStep(1, new DateTime(2026,1,10), new DateTime(2026,1,11)), // Splneno
        new ScheduleDateStep(2, new DateTime(2026,1,20), null),                    // plán < dnes → VProdleni (pokud aktuální)
        new ScheduleDateStep(3, new DateTime(2026,3,20), null),                    // plán > dnes → Ceka
    };
    var res = ScheduleDateCalculator.Compute(Start, steps, today: new DateTime(2026,2,1));
    res[0].Stav.Should().Be(HarmonogramKrokStav.Splneno);
    res[1].Stav.Should().Be(HarmonogramKrokStav.VProdleni);
    res[2].Stav.Should().Be(HarmonogramKrokStav.Ceka);
}

[Fact]
public void Compute_aktualni_krok_ma_actual_segment_do_dneska()
{
    // vyplněno krok 1 do 2026-01-11; aktuální krok 2, dnes 2026-02-01
    var steps = new[] {
        new ScheduleDateStep(1, new DateTime(2026,1,10), new DateTime(2026,1,11)),
        new ScheduleDateStep(2, new DateTime(2026,1,20), null),
    };
    var res = ScheduleDateCalculator.Compute(Start, steps, today: new DateTime(2026,2,1));
    res[1].MaSkutecnost.Should().BeTrue("aktuální krok se kreslí jako rozpracovaný");
    res[1].JeAktualniKrok.Should().BeTrue();
    res[1].SkutecnostStart.Should().Be(new DateTime(2026,1,11)); // konec posledního vyplněného
    res[1].SkutecnostEnd.Should().Be(new DateTime(2026,2,1));     // dnešek
}
```

- [ ] **Step 2: Run → FAIL** (`Stav`/`JeAktualniKrok` + `today` param neexistují).

- [ ] **Step 3: Implement** — rozšířit `ScheduleDateStepResult` a `Compute`:

```csharp
public sealed record ScheduleDateStepResult(
    int Poradi, DateTime PlanStart, DateTime PlanEnd,
    bool MaSkutecnost, DateTime SkutecnostStart, DateTime SkutecnostEnd,
    HarmonogramKrokStav Stav, bool JeAktualniKrok);
```

`Compute(start, steps, today)` — přidat `DateTime today` param. Po stávajícím výpočtu plán/skutečnost cursorů: detekuj aktuální krok (první nevyplněný po posledním vyplněném) a:
- pro vyplněné kroky `Stav = Splneno`, `JeAktualniKrok = false`, segment beze změny;
- pro aktuální krok (nevyplněný): `MaSkutecnost = true`, `JeAktualniKrok = true`, `SkutecnostStart = skutecnostCursor` (konec posledního vyplněného), `SkutecnostEnd = max(skutecnostCursor, today.Date)`, `Stav = today.Date > PlanEnd ? VProdleni : Ceka`;
- ostatní nevyplněné: `MaSkutecnost = false`, `Stav = today.Date > PlanEnd ? VProdleni : Ceka`.

> **Pozor:** `today` přidává parametr do `Compute` → upravit VŠECHNY callery: `HarmonogramDateBlokBuilder.BuildKroky/BuildBarLayout/BuildSouhrn` (předávají dál) a všechny testy volající `Compute`. Stávající `Summarize` volá `Compute(start, ordered)` 2× — předat `normToday`.

- [ ] **Step 4: Run → PASS** (po dotažení callerů v 1.4/1.5).

### Task 1.4: ScheduleBarLayoutCalculator — segment aktuálního kroku do dneška

**Files:**
- Modify: `ScheduleBarLayoutCalculator.cs`
- Test: `PmTracker.Tests.Unit/Schedule/ScheduleBarLayoutCalculatorTests.cs`

- [ ] **Step 1: Failing test:**

```csharp
[Fact]
public void Compute_aktualni_krok_ma_actual_segment_az_po_dnes()
{
    var steps = new[] {
        new ScheduleDateStepResult(1, S(1,1), S(1,10), true, S(1,1), S(1,11), HarmonogramKrokStav.Splneno, false),
        new ScheduleDateStepResult(2, S(1,10), S(1,20), true, S(1,11), S(2,1), HarmonogramKrokStav.VProdleni, true),
    };
    var layout = ScheduleBarLayoutCalculator.Compute(S(1,1), deadline: S(3,1), today: S(2,1), steps);
    var seg2 = layout.Segments.First(s => s.Poradi == 2);
    seg2.HasActual.Should().BeTrue();
    seg2.ActualWidthPct.Should().BeGreaterThan(0);  // táhne se do dneška
}
// helper: static DateTime S(int m,int d) => new DateTime(2026,m,d);
```

- [ ] **Step 2: Run → FAIL.** (Aktuální krok je nevyplněný v reálu, ale `ScheduleDateStepResult.MaSkutecnost` už je true po 1.3 → segment by měl vzniknout. Pokud `ScheduleBarLayoutCalculator` jen čte `MaSkutecnost`, možná test prochází rovnou — pak ho ponech jako regresní guard a přejdi na Step 4.)

- [ ] **Step 3: Implement** — `ScheduleBarLayoutCalculator.Compute` čte `s.MaSkutecnost` (už true pro aktuální krok z 1.3), takže `ActualLeftPct/ActualWidthPct` se počítá z `SkutecnostStart..SkutecnostEnd` (= dnešek). Žádná změna logiky pravděpodobně netřeba — pouze ověř, že `Extend(s.SkutecnostEnd)` zahrne dnešek do osy. Pokud `axisEnd` nezahrnuje dnešek, segment by se ořízl — `Extend(today)` už tam je (ř. 39). OK.

- [ ] **Step 4: Run → PASS.**

### Task 1.5: HarmonogramKrokEditViewModel + HarmonogramSouhrnViewModel + BuildKroky/BuildSouhrn

**Files:**
- Modify: `ProjektHarmonogramTabViewModels.cs`, `HarmonogramDateBlokBuilder.cs`
- Test: `PmTracker.Tests.Unit/Schedule/HarmonogramDateBlokBuilderTests.cs`

- [ ] **Step 1: Failing testy:**

```csharp
[Fact]
public void BuildKroky_nevyplneny_krok_ma_null_SkutecneDatum_zadny_PlanEnd_fallback()
{
    var rows = new List<ZaznamHarmonogramKrokEntity> {
        new() { Poradi = 1, PlanDatum = new DateTime(2026,1,10), SkutecnostDatum = null }
    };
    var kroky = HarmonogramDateBlokBuilder.BuildKroky(new DateTime(2026,1,1), rows, today: new DateTime(2026,2,1));
    kroky[0].SkutecneDatum.Should().BeNull("zrušen PlanEnd fallback");
    kroky[0].Stav.Should().Be(HarmonogramKrokStav.VProdleni);
}

[Fact]
public void BuildSouhrn_ma_aktualni_krok_a_znamenkove_prekroceni()
{
    var rows = new List<ZaznamHarmonogramKrokEntity> {
        new() { Poradi = 1, PlanDatum = new DateTime(2026,1,10), SkutecnostDatum = new DateTime(2026,1,11) },
        new() { Poradi = 2, PlanDatum = new DateTime(2026,1,20), SkutecnostDatum = null },
    };
    var s = HarmonogramDateBlokBuilder.BuildSouhrn(new DateTime(2026,1,1), rows, termin: new DateTime(2026,12,1), today: new DateTime(2026,2,1));
    s.AktualniKrokPoradi.Should().Be(2);
    s.PrekroceniDni.Should().Be((new DateTime(2026,2,1) - new DateTime(2026,1,20)).Days);
    s.Dokonceno.Should().BeFalse();
}
```

- [ ] **Step 2: Run → FAIL.**

- [ ] **Step 3: Implement:**

`HarmonogramKrokEditViewModel`:
```csharp
public DateTime? SkutecneDatum { get; init; }   // bylo DateTime (non-null)
public HarmonogramKrokStav Stav { get; init; } = HarmonogramKrokStav.Ceka;
public string? AktualniKrokNazev { get; init; } // volitelně; nebo přes Souhrn
```
Smaž alias `CeleZpozdeniDni`/`PosunuteDokonceni` pokud bez konzumenta (ověř grep). `OdchylkaDni`, `BaselineDatum`, `TrvaniDni` zůstávají.

`HarmonogramSouhrnViewModel` — nový shape:
```csharp
public sealed class HarmonogramSouhrnViewModel
{
    public DateTime BaselineDokonceni { get; init; }     // plánové dokončení (zůstává, zobrazuje editor)
    public DateTime TerminUkolu { get; init; }
    public int CelkoveTrvaniDni { get; init; }
    public int? AktualniKrokPoradi { get; init; }        // null = Dokončeno
    public string? AktualniKrokNazev { get; init; }
    public int PrekroceniDni { get; init; }              // znaménkové
    public bool Dokonceno { get; init; }
    public bool Stihame => Dokonceno || PrekroceniDni <= 0;  // odvozené pro gov-tag konzumenty
}
```

`HarmonogramDateBlokBuilder.BuildKroky` — přidat `DateTime today`, předat do `Compute`, mapovat `Stav = c.Stav`, `SkutecneDatum = c.MaSkutecnost && !c.JeAktualniKrok ? c.SkutecnostEnd : (c.JeAktualniKrok ? null : null)`.
> Přesněji: `SkutecneDatum` = reálné datum jen u **skutečně vyplněných** kroků (ne u projektovaného aktuálního). Použij vstupní `row?.SkutecnostDatum` přímo: `SkutecneDatum = row?.SkutecnostDatum?.Date`. `OdchylkaDni = row?.SkutecnostDatum.HasValue == true ? (row.SkutecnostDatum.Value.Date - c.PlanEnd).Days : null`.

`BuildSouhrn` — mapovat nový `ScheduleDateSummary` na nový `HarmonogramSouhrnViewModel` (`AktualniKrokPoradi`, `PrekroceniDni`, `Dokonceno`, `AktualniKrokNazev` z `HarmonogramKroky.Vse`).

- [ ] **Step 4: Run → PASS** (celá Phase 1 build green).

- [ ] **Step 5: Commit** — `git commit -m "feat(harmonogram): aktuální krok + znaménkové překročení + 3 stavy + zrušen PlanEnd fallback (backend)"`

---

## Phase 2 — Composition wiring

### Task 2.1: Thread `today` + nový souhrn do obou composition cest

**Files:**
- Modify: `ProjectService.RecordEditorComposition.cs` (BuildKroky volání — re-projekce ř. 149-203 už `today` má jako `todayDate`), `ProjectService.ScheduleComposition.cs`
- Test: `PmTracker.Tests.Api/Controllers/ProjectHarmonogramRenderTests.cs`

- [ ] **Step 1: Failing test** — souhrn na kartě ukazuje aktuální krok, ne „Skutečné dokončení".

```csharp
[Fact]
public async Task Detail_Souhrn_UkazujeAktualniKrok_NeSkutecneDokonceni()
{
    var (projectId, recordId) = await SeedDatumScheduleAsync(); // vyplněné 1..4, zbytek prázdný
    using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
    var html = await (await client.GetAsync($"/Projekty/Detail/{projectId}?tab=harmonogram&asUser={_fixture.AdminOsobaId}")).Content.ReadAsStringAsync();
    html.Should().NotContain("Skutečné dokončení");
    html.Should().MatchRegex("Aktuální krok");
}
```

- [ ] **Step 2: Run → FAIL.**

- [ ] **Step 3: Implement** — v `RecordEditorComposition` předat `todayDate` do `BuildKroky(record.DatumZalozeni, krokRows, todayDate)`. V `ScheduleComposition` najdi `BuildKroky`/`BuildSouhrn` volání a předej `today` (přidej `var today = timeProvider.GetUtcNow().UtcDateTime.Date;` pokud chybí). `ScheduleComposition.cs:86` `Stihame = souhrn.Stihame` zůstává (odvozená property). `ProjektHarmonogramUkolViewModel.Stihame` ponech (gov-tag konzument v `_ProjectScheduleTab.cshtml:64`).

- [ ] **Step 4: Run → PASS** (až po Phase 3 render). 
- [ ] **Step 5: Commit** — `git commit -m "feat(harmonogram): composition předává today + nový souhrn"`

---

## Phase 3 — Render (cshtml + CSS)

### Task 3.1: Sjednocený souhrn (editor + mini) — aktuální krok místo Stíháme/Překročení/Skutečné dokončení

**Files:** Modify: `_ScheduleBlock.cshtml:66-76` (editor) + `:165-170` (mini)
**Test:** `PmTracker.Tests.Unit/Schedule/ScheduleBlockMarkupTests.cs`

- [ ] **Step 1: Failing test** — markup obsahuje „Aktuální krok" a NEobsahuje „Skutečné dokončení"/„Stíháme". (Markup test renderuje partial přes `RenderPartialToString` helper, který v tomto projektu existuje — viz stávající `ScheduleBlockMarkupTests`.)

- [ ] **Step 2: Run → FAIL.**

- [ ] **Step 3: Implement** — nahraď editor summary blok (ř. 71-75) jedním sjednoceným:

```cshtml
<div class="schedule-summary-grid">
    <div><span class="label">Termín:</span> <strong data-schedule-summary-deadline>@Model.Souhrn.TerminUkolu.ToString("dd.MM.yyyy")</strong></div>
    <div><span class="label">Plán dokončení:</span> <strong data-schedule-summary-baseline>@Model.Souhrn.BaselineDokonceni.ToString("dd.MM.yyyy")</strong></div>
    @{
        var dokonceno = Model.Souhrn.Dokonceno;
        var skluz = Model.Souhrn.PrekroceniDni;
        var stavText = dokonceno ? "Dokončeno"
            : skluz > 0 ? $"Aktuální krok: {Model.Souhrn.AktualniKrokNazev} · skluz +{skluz} dní"
            : skluz < 0 ? $"Aktuální krok: {Model.Souhrn.AktualniKrokNazev} · v předstihu {-skluz} dní"
            : $"Aktuální krok: {Model.Souhrn.AktualniKrokNazev} · dle plánu";
        var stavOk = dokonceno || skluz <= 0;
    }
    <div class="schedule-status-line @(stavOk ? "ok" : "late")">
        <span class="label">Stav:</span>
        <strong data-schedule-summary-state>@stavText</strong>
    </div>
</div>
```

Mini souhrn (ř. 165-170) — nahradit `Skutečnost`/`Překročení` span za sjednocený stav span (`data-schedule-summary-state`), Termín + Plán ponech. Smaž `data-schedule-summary-shifted` (Skutečné dokončení) z obou míst.

> **block.js dependency:** block.js cílí na `data-schedule-summary-shifted`/`-overrun`/`-state` — Task 4.1 ho přepíše současně. Mezi 3.1 a 4.1 editor live-preview dočasně neaktualizuje souhrn (server-rendered hodnota zůstává). Akceptovatelné v rámci jedné PR; commitni 3.1+4.1 blízko.

- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5: Commit.**

### Task 3.2: Lišty + breakdown — actual segment aktuálního kroku + pevná barva + šrafa

**Files:** Modify: `_ScheduleBlock.cshtml` (overview ř. 104-141, breakdown ř. 190-250) + `site.css` (3020, 3111-3140)
**Test:** `ScheduleBlockMarkupTests` / `ProjectHarmonogramRenderTests`

- [ ] **Step 1: Failing test** — actual segment se renderuje i pro aktuální (nevyplněný) krok.

```csharp
// Render záznam s vyplněnými 1..4; krok 5 = aktuální. V overview/breakdown actual řadě
// musí být segment pro krok 5 (data-step-index="5", kind/segment="actual") s display != none.
```

- [ ] **Step 2: Run → FAIL** (dnes `isActualHidden = !OdchylkaDni.HasValue` → krok 5 skrytý).

- [ ] **Step 3: Implement:**
  - Overview (ř. 116, 124): `isActualHidden` a `hide` musí počítat s aktuálním krokem. Použij `seg.HasActual` (ze server layoutu, který je teď true i pro aktuální krok z 1.4) místo `!krok.OdchylkaDni.HasValue`. Tj. `var isActualHidden = row.Item1 == "actual" && (ovlSeg is null ? !krok.OdchylkaDni.HasValue : !seg.HasActual);` — ale `seg` je dostupný až v bloku; zjednodušit: pro actual řadu se viditelnost řídí výhradně `seg.HasActual`.
  - Overview segment background (ř. 139): nahraď `background:@krok.BarvaHex` za CSS var `--seg-color:@krok.BarvaHex;` (barva přes CSS, aby actual mohl mít šrafu nad barvou kroku):
    `style="--seg-color:@krok.BarvaHex;@positionStyle"`.
  - Breakdown actual (ř. 238-247): renderuj actual segment i pro aktuální krok (podmínka `hasActual` → `bseg?.HasActual == true`); přidej `style="--seg-color:@krok.BarvaHex;@actualPosStyle"`.
  - Breakdown planned (ř. 237): `style="--seg-color:@krok.BarvaHex;@plannedHiddenStyle"`.
  - Tooltip/aria u actual (ř. 111/114/242): `krok.SkutecneDatum` je teď nullable → `krok.SkutecneDatum?.ToString("dd.MM.yyyy") ?? "rozpracováno"`.

  CSS (`site.css`):
  ```css
  .schedule-overview-segment.planned,
  .schedule-layered-segment.planned { background: var(--seg-color); opacity: .94; box-shadow: inset 0 0 0 1px rgba(15,23,42,.18); }

  .schedule-overview-segment.actual,
  .schedule-layered-segment.actual {
    top: 50%; height: 8px; transform: translateY(-50%);
    background:
      repeating-linear-gradient(135deg, rgba(255,255,255,.55) 0, rgba(255,255,255,.55) 4px, transparent 4px, transparent 8px),
      var(--seg-color);
    border: 1px solid rgba(15,23,42,.42);
  }
  ```
  Smaž starou `var(--schedule-actual-color, ... delay)` referenci v `.actual` blocích (3020, 3118, 3138) + dark-mode (6547-6548) — nahraď `--seg-color`.

- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5: Commit** — `git commit -m "feat(harmonogram): lišty kreslí aktuální krok do dneška; pevná barva + šrafa skutečnosti"`

---

## Phase 4 — block.js editor live-preview mirror

### Task 4.1: Přepsat computeDateModel/renderSummary/renderOverview/renderBreakdown na nový model

**Files:** Modify: `PmTracker.Web/wwwroot/js/modules/schedule/block.js`
**Test:** `PmTracker.Tests.Unit/Architecture/ScheduleJsSplitTests.cs` (string-asserce na zdroj — vzor v repu)

- [ ] **Step 1: Failing test** — block.js neobsahuje starou „Stíháme"/„shifted" logiku, obsahuje aktuální-krok výpočet.

```csharp
[Fact]
public void BlockJs_RenderSummary_PouzivaAktualniKrok_NeStihameVsTermin()
{
    var src = ReadRepo("PmTracker.Web/wwwroot/js/modules/schedule/block.js");
    src.Should().NotContain("Stíháme", "souhrn už nepoužívá Stíháme/Nestíháme");
    src.Should().NotContain("summary-shifted", "Skutečné dokončení zrušeno");
    src.Should().Contain("aktualniKrok", "live-preview počítá aktuální krok");
}
```

- [ ] **Step 2: Run → FAIL.**

- [ ] **Step 3: Implement** — v `computeDateModel()` přidat detekci aktuálního kroku (první nevyplněný po posledním vyplněném, mirror 1.2) + znaménkové překročení (`dnes − plán(aktuální)`). `renderSummary` přepsat: cílit `[data-schedule-summary-state]` textem „Aktuální krok: <název> · skluz/předstih/Dokončeno"; odstranit `summary-shifted`/`summary-overrun`/„Stíháme" větve a `stihame` výpočet vs deadline (ř. 363-395). `renderOverview`/`renderBreakdown` — actual segment aktuálního kroku táhnout do dneška (segment width = dnes − konec posledního vyplněného), barva přes `--seg-color` (mirror 3.2). Zachovat existující selektory segmentů.

> Detaily názvů kroků: block.js má per-row data (`data-step-name`); aktuální krok název vezmi z `data-step-name` řádku s `data-step-index == aktualniKrokPoradi`.

- [ ] **Step 4: Run → PASS** + manuální ověření Playwrightem (editor: změna plán datumu posune lišty + přepočítá „Aktuální krok").
- [ ] **Step 5: Commit** — `git commit -m "feat(harmonogram): block.js editor live-preview na aktuální krok + sjednocený stav"`

---

## Phase 5 — Chronologie (server + UI)

### Task 5.1: Server validace chronologie v ValidateScheduleValuesAsync

**Files:** Modify: `RecordService.SaveRecord.cs:695-761`
**Test:** `PmTracker.Tests.Integration/DataStore/RecordSaveDataStoreTests.cs` (nebo Api `AjaxControllersTests`)

- [ ] **Step 1: Failing testy** — plán mimo pořadí → field error; validní (neklesající, stejný den) projde; ruční skutečnost mimo pořadí → field error.

```csharp
[Fact]
public void Save_NechronologickyPlan_VratiFieldError()
{
    // HarmonogramHodnoty: krok1 PlanDatum=2026-02-10, krok2 PlanDatum=2026-02-05 (klesá) → error na HarmonogramHodnoty[1].PlanDatum
}
[Fact]
public void Save_ChronologickyPlan_StejnyDen_Projde() { /* krok1=krok2 datum → OK */ }
```

- [ ] **Step 2: Run → FAIL.**

- [ ] **Step 3: Implement** — do `ValidateScheduleValuesAsync` (po existující Poradi/duplicita kontrole) přidat:

```csharp
// Chronologie plánu: seřaď podle Poradi, plán nesmí klesat.
var planByPoradi = command.HarmonogramHodnoty
    .Where(x => x.Poradi is >= 1 and <= 10 && x.PlanDatum.HasValue)
    .OrderBy(x => x.Poradi).ToList();
DateTime? prevPlan = null; int prevPoradi = 0;
foreach (var item in planByPoradi)
{
    if (prevPlan.HasValue && item.PlanDatum!.Value.Date < prevPlan.Value.Date)
        AddRecordValidationIssue(issues,
            $"HarmonogramHodnoty[{command.HarmonogramHodnoty.IndexOf(item)}].PlanDatum",
            $"Plán kroku {item.Poradi} nesmí být dříve než plán kroku {prevPoradi}.",
            "schedule", "schedule_plan_chronology", item.PlanDatum.Value.ToString("yyyy-MM-dd"));
    prevPlan = item.PlanDatum!.Value.Date; prevPoradi = item.Poradi;
}
// Chronologie ruční skutečnosti (ManualActualKroky 2/5/8/9): obdobně vůči sousedním známým skutečnostem.
```

> **Pozn.:** `IndexOf` na řádku najde správný form index pro field key. Pro ruční skutečnost ověř sousedy mezi všemi známými skutečnostmi (plán i existující krok rows) — viz spec „zapadne mezi známé skutečnosti". Auto skutečnost se NEvaliduje.

- [ ] **Step 4: Run → PASS.**
- [ ] **Step 5: Commit.**

### Task 5.2: pm-date-field min/max + UI chronologie v block.js

**Files:** Modify: `pm-date-field.js`, `block.js`, `_ScheduleBlock.cshtml` (data-attrs)
**Test:** `PmTracker.Tests.Unit/Architecture/HarmonogramPhantomUiFixesTests.cs` (string-asserce)

- [ ] **Step 1: Failing test** — `pm-date-field.js` má `min`/`max` v observedAttributes a propisuje je do hidden inputu.

```csharp
[Fact]
public void PmDateField_PodporujeMinMax()
{
    var src = ReadRepo("PmTracker.Web/wwwroot/js/components/pm-date-field.js");
    src.Should().Contain("\"min\"").And.Contain("\"max\"");
}
```

- [ ] **Step 2: Run → FAIL.**

- [ ] **Step 3: Implement** — `pm-date-field.js`: přidat `ATTR_MIN="min"`, `ATTR_MAX="max"` do observedAttributes; v render/attributeChanged propsat na nativní `<input type="date" min max>` (pokud komponenta používá nativní date input) NEBO validovat v `block.js` při `change`. block.js: při editaci plán datumu kroku N nastav `min` = plán(N-1), `max` = plán(N+1) na sousedních polích a zabraň uložení mimo rozsah (soft — zobraz hlášku, server stejně validuje). Razor: žádné nové server data-attrs nutné (sousedi se čtou z DOM pořadí).

- [ ] **Step 4: Run → PASS** + Playwright ověření (nelze zadat datum mimo pořadí).
- [ ] **Step 5: Commit** — `git commit -m "feat(harmonogram): chronologie datumů — server validace + UI min/max"`

---

## Phase 6 — Test sweep + dokumentace

### Task 6.1: Aktualizovat existující testy + E2E

**Files:** `ProjectHarmonogramRenderTests.cs`, `HarmonogramUnifiedScenariosTests.cs` (E2E), `ScheduleBlockViewModelManualKrokShapeTests.cs`, `ScheduleBlockPartialManualKrokRenderingTests.cs`

- [ ] **Step 1:** Projít a opravit asserce odkazující na `SkutecneDokonceni`/`Stihame`/`SkutecneDatum` (non-null)/„Skutečné dokončení"/„Stíháme". Aktualizovat E2E scénáře na sjednocený stav.
- [ ] **Step 2:** `dotnet test` všechny 3 suites — zelená kromě known-pre-existing (8 Unit CSS/bundle + 7 Api authz + 5 Integration authz; viz [[project_pre_existing_test_failures]]).
- [ ] **Step 3: Commit.**

### Task 6.2: Ověřit dokumentaci

- [ ] Spec [docs/specs/harmonogram-plan-vs-skutecnost.md](../../specs/harmonogram-plan-vs-skutecnost.md) je aktuální (sepsána v brainstormingu). Zkontrolovat, že implementace nezavedla odchylku; pokud ano, dopsat.

---

## Pořadí a závislosti

```
Phase 0 (odemknout editor) ──► Phase 5.2 (UI chronologie potřebuje editovatelné inputy)
Phase 1 (backend) ──► Phase 2 (composition) ──► Phase 3 (render) ──► Phase 4 (block.js mirror)
Phase 1 ──► Phase 5.1 (server chronologie)
Phase 3 + 4 ──► Phase 6 (test sweep)
```

Doporučené pořadí commitů: 0 → 1 → 2 → 3 → 4 → 5 → 6.
