# Skutečnost harmonogramu v jednom seznamu — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Tento projekt:** exekuce **inline v hlavní session**, bez subagentů (memory `feedback_inline_execution_no_subagents`).

**Goal:** Skutečnost všech kroků harmonogramu se z editoru i z návrhu posílá jediným seznamem `HarmonogramHodnoty[i]` a zapisuje jedinou komponentou, takže se ruční data už nemohou ztratit dírou v číslování, přepnutím režimu ani druhým uložením (vady D1–D6).

**Architecture:** `ManualActualKroky` zaniká; skutečnost jde v `HarmonogramHodnoty[i].SkutecnostDatum` + `SkutecnostDatumPuvodni` (hodnota při vykreslení), index `i` je pevný řádek kroku 0–9. Pravidla zápisu (režimy, kroky 2/5/8/9, mazání ručních dat při přepnutí na automatiku) drží jediná čistá komponenta `ScheduleActualApplier`, kterou volá uložení editoru, uložení záložky Harmonogram i schválení návrhu. Starý JSON návrhů převádí při čtení `RecordProposalPayloadJson`.

**Tech Stack:** ASP.NET Core 8 MVC (Razor), EF Core 8, SQL Server, vanilla ES moduly, xUnit + FluentAssertions, Api testy přes `WebApplicationFactory` (`ApiSqlFixture`), integrační testy přes `SqlIntegrationFixture`.

**Spec:** `docs/superpowers/specs/2026-10-14-harmonogram-skutecnost-jeden-seznam-design.md`

**Odhad:** ~1,7 h inline exekuce Opus 5 (bez ručního ověření uživatelem).

## Global Constraints

- Kroky **2/5/8/9 jsou vždy ruční** — automat ani přepínač „Automatické vyplňování harmonogramu“ je nikdy nemění (R3).
- Kroky **1/3/4/6/7/10** řídí přepínač; v automatickém režimu se změny jejich skutečnosti ignorují.
- **R2 (potvrzeno uživatelem 2026-10-14: „buď to dělám ručně, nebo to dělá automat, nic mezi“):** uložení s přepnutím na automatiku smaže skutečnost kroků 1/3/4/6/7/10, kterou **nedodal automat** — zdroj Ruční (2) **i Historická (3)**; zůstává jen zdroj Automat (1). Platí pro všechny kroky 1/3/4/6/7/10 záznamu, i když některý byl už předtím v režimu Auto (smíšený stav ze starých návrhů). Záznam, který už v automatickém režimu je, se při uložení nemění.
- Ve formuláři **nesmí zůstat řetězec `ManualActualKroky`** (výjimka: převod starého JSON v `RecordProposalPayloadJson.cs`).
- `manual-zdroj-toggle.js` se **nesmaže**, jen vyprázdní (R5 — `bootstrap.js` v mezipaměti prohlížeče ho importuje).
- Razor **nevynechá `data-*` atribut s null hodnotou** — podmíněný `data-*` skládat ručně přes `Html.Raw` (memory `feedback_razor_data_attributes_not_conditional`).
- Na český text v Api HTML testech se neassertuje (Razor ho kóduje na entity) — kotvit na atributy a ASCII (memory `feedback_razor_encodes_diacritics_in_html_tests`).
- Texty pro uživatele česky; komentáře v kódu česky, ve stylu okolního kódu.
- Žádná změna schématu DB, žádný `db_upgrade_*.sql`.

## Review Focus

1. **Souběh s automatem během editace:** editor otevřený v Auto režimu, automat mezitím změní krok 4, uživatel přepne na ruční a uloží bez sáhnutí na krok 4 → musí zůstat **novější** hodnota automatu (porovnání s `SkutecnostDatumPuvodni`, ne s DB). Test: Task 2 Step 1 `SaveRecord_NedotcenyKrok_NechaNovejsiHodnotuAutomatu`.
2. **Starý `bootstrap.js` v mezipaměti** importuje `manual-zdroj-toggle.js` → soubor musí existovat a být neškodný. Test: Task 5 Step 1 `ManualZdrojToggle_ZustavaPrazdnymModulem`.
3. **Stará nechronologická data od automatu v ručním režimu** nesmí zablokovat uložení, na které uživatel nesáhl. Test: Task 2 Step 1 (unit) `NedotceneNechronologickeHodnoty_NeblokujiUlozeni`.
4. **Přepnutí ruční → auto v editoru návrhu** vyčistí vstupy (JS) → vyčištění nesmí do payloadu projít jako „smazat“ u kroků 1/3/4/6/7/10. Test: Task 4 Step 1 `BuildSchedulePayload_CilAuto_VynechaZmenyAutoKroku`.
5. **Záznam bez řádků kroků** (nový úkol bez harmonogramu) přepnutý na ruční → řádky kroků 1/3/4/6/7/10 se založí v ručním režimu, kroky 2/5/8/9 se nezaloží. Test: Task 1 Step 1 `PrepnutiNaRucni_BezRadku_ZalozeRadkyAutoKroku`.

## File Structure

| Soubor | Akce | Odpovědnost |
|---|---|---|
| `PmTracker.Web/Services/Schedules/ScheduleActualApplier.cs` | Create | jediná pravidla zápisu skutečnosti |
| `PmTracker.Web/Services/Records/RecordProposalPayloadJson.cs` | Create | jediné čtení JSON návrhu + převod starého formátu |
| `PmTracker.Web/Models/ViewModels/Commands/RecordCommands.cs` | Modify | `SkutecnostDatumPuvodni`; později zrušit `ManualActualKroky` |
| `PmTracker.Web/Services/RecordService.SaveRecord.cs` | Modify | `PersistScheduleKrokyAsync` přes applier, stažení ze SD po přepnutí |
| `PmTracker.Web/Services/Schedules/ScheduleChronologyValidator.cs` | Modify | chronologie nad `HarmonogramHodnoty` (R7) |
| `PmTracker.Web/Views/Shared/_ScheduleTable.cshtml` | Modify | jedna větev pro existující záznam, index = řádek |
| `PmTracker.Web/Views/Shared/_ScheduleBlockManualCell.cshtml` | Modify | varianty Auto/Manual, bez mrtvé volby D5 |
| `PmTracker.Web/Views/Shared/_ScheduleBlockManualCellInput.cshtml` | Modify | editovatelný vstup + původní hodnota |
| `PmTracker.Web/Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs` | Modify | `ManualInputIndex` → `HodnotaIndex` |
| `PmTracker.Web/wwwroot/js/modules/schedule/block.js` | Modify | čtení skutečnosti pro graf |
| `PmTracker.Web/wwwroot/js/modules/harmonogram/manual-zdroj-toggle.js` | Modify | vyprázdnit (R5) |
| `PmTracker.Web/wwwroot/js/modules/bootstrap.js` | Modify | zrušit import toggle |
| `PmTracker.Web/wwwroot/js/modules/harmonogram/rezim-master-switch.js`, `manualKroky.js` | Modify | komentáře (D6) |
| `PmTracker.Web/Models/ViewModels/RecordProposalViewModels.cs` | Modify | zrušit `ManualActualKroky`, `ManualActualKrokDto` |
| `PmTracker.Web/Services/Records/RecordProposalPayloadMapper.cs` | Modify | payload skutečnosti = seznam změn |
| `PmTracker.Web/Services/RecordProposalService.SubmitCommands.cs` | Modify | zrušit staré validátory |
| `PmTracker.Web/Services/RecordProposalService.DecisionCommands.cs` | Modify | schválení přes applier |
| `PmTracker.Web/Services/RecordProposalService.Queries.cs` | Modify | infobar ze změn |
| `PmTracker.Web/Services/RecordProposalService.cs`, `Records/PendingScheduleProposalLockEvaluator.cs` | Modify | čtení JSON přes `RecordProposalPayloadJson` |
| `PmTracker.Web/Services/Records/ManualActualKrokApplier.cs` | Delete | nahrazeno applierem |
| `PmTracker.Web/Services/Records/ManualProposalFieldValidator.cs` | Modify | zrušit metody pro `ManualActualKroky` |
| `PmTracker.Web/Controllers/HarmonogramController.cs` | Modify | `SelectCandidate` odmítne kroky 2/5/8/9 |
| `docs/wiki/projekty/harmonogram/manualni-skutecnost.md` aj. | Modify | uživatelská dokumentace |

---

### Task 0: Výchozí stav

**Files:** žádné změny.

- [ ] **Step 1: Izolovaný worktree** (druhá session pracuje na `main` souběžně; memory `feedback_work_into_main` = na konci sloučit do lokálního `main`)

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
git worktree add "../PM Tracker-wt-skutecnost" -b fix/harmonogram-skutecnost-jeden-seznam main
cd "../PM Tracker-wt-skutecnost"
```

- [ ] **Step 2: Základní běh testů a zapsání selhání**

```bash
dotnet build PmTracker.sln -v q --nologo
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --nologo 2>&1 | grep -E '^  Failed |^Failed!|^Passed!'
dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --nologo 2>&1 | grep -E '^  Failed |^Failed!|^Passed!'
dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --nologo 2>&1 | grep -E '^  Failed |^Failed!|^Passed!'
```

Expected: Unit a Api zelené (memory `project_pre_existing_test_failures`); Integration nejvýš známé selhání `ProposalRejectAndTakeOverE2ETests.RejectAndTakeOver_ThenSaveWithModifiedData_CreatesRecord`. Jména všech selhání si zapiš — každé další selhání během plánu je regrese.

---

### Task 1: `ScheduleActualApplier` — jediná pravidla zápisu skutečnosti

**Files:**
- Create: `PmTracker.Web/Services/Schedules/ScheduleActualApplier.cs`
- Test: `PmTracker.Tests.Unit/Schedules/ScheduleActualApplierTests.cs`

**Interfaces:**
- Produces:
  - `ScheduleActualApplier.SkutecnostZmena(int Poradi, DateTime? Datum)` — explicitní změna; `Datum == null` = smazáno.
  - `ScheduleActualApplier.Vysledek(bool Zmeneno, bool PrepnutoNaAuto)`
  - `static Vysledek Apply(IReadOnlyCollection<ZaznamHarmonogramKrokEntity> existujici, Func<int, ZaznamHarmonogramKrokEntity> getOrCreate, IReadOnlyList<SkutecnostZmena> zmeny, string? cilovyRezim, DateTime nowUtc)`
  - `static bool JeZmena(DateTime? hodnota, DateTime? puvodni)` — porovnání po dnech.
  - `static byte VychoziRezim(int poradi)` — `Manual` pro 2/5/8/9, jinak `Auto` (pro zakládání řádků).

- [ ] **Step 1: Napiš padající testy**

```csharp
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Schedules;
using Xunit;

namespace PmTracker.Tests.Unit.Schedules;

/// <summary>
/// Spec 2026-10-14 §3 — pravidla zápisu skutečnosti harmonogramu. Jedna komponenta pro
/// uložení editoru i schválení návrhu, proto se testuje bez DB nad samotnými řádky kroků.
/// </summary>
public sealed class ScheduleActualApplierTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime A = new(2026, 10, 5);
    private static readonly DateTime B = new(2026, 10, 9);

    private static ZaznamHarmonogramKrokEntity Krok(int poradi, DateTime? datum, SkutecnostZdrojEnum zdroj, SkutecnostRezimEnum rezim)
        => new()
        {
            ZaznamId = 1,
            Poradi = (byte)poradi,
            SkutecnostDatum = datum,
            SkutecnostZdroj = (byte)zdroj,
            SkutecnostRezim = (byte)rezim
        };

    private static (List<ZaznamHarmonogramKrokEntity> Rows, Func<int, ZaznamHarmonogramKrokEntity> GetOrCreate) Radky(
        params ZaznamHarmonogramKrokEntity[] seed)
    {
        var rows = seed.ToList();
        ZaznamHarmonogramKrokEntity GetOrCreate(int poradi)
        {
            var row = rows.FirstOrDefault(x => x.Poradi == poradi);
            if (row is not null) return row;
            row = new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = 1,
                Poradi = (byte)poradi,
                SkutecnostRezim = ScheduleActualApplier.VychoziRezim(poradi),
                SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Neznamo
            };
            rows.Add(row);
            return row;
        }
        return (rows, GetOrCreate);
    }

    private static ScheduleActualApplier.Vysledek Apply(
        (List<ZaznamHarmonogramKrokEntity> Rows, Func<int, ZaznamHarmonogramKrokEntity> GetOrCreate) r,
        string? rezim,
        params ScheduleActualApplier.SkutecnostZmena[] zmeny)
        => ScheduleActualApplier.Apply(r.Rows.ToList(), r.GetOrCreate, zmeny, rezim, Now);

    [Fact]
    public void RucniKrok_Zmena_ZapiseDatumRucne()
    {
        var r = Radky();
        Apply(r, "Auto", new(5, A));
        var k5 = r.Rows.Single(x => x.Poradi == 5);
        k5.SkutecnostDatum.Should().Be(A);
        k5.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Manual);
        k5.SkutecnostRezim.Should().Be((byte)SkutecnostRezimEnum.Manual, "kroky 2/5/8/9 jsou vždy ruční (R3)");
    }

    [Fact]
    public void RucniKrok_Smazani_VyprazdniKrok()
    {
        var r = Radky(Krok(5, A, SkutecnostZdrojEnum.Manual, SkutecnostRezimEnum.Manual));
        Apply(r, "Auto", new(5, null));
        var k5 = r.Rows.Single(x => x.Poradi == 5);
        k5.SkutecnostDatum.Should().BeNull();
        k5.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Neznamo);
    }

    [Fact]
    public void AutoKrok_ZmenaVRucnimRezimu_Zapise()
    {
        var r = Radky();
        Apply(r, "Manual", new(3, B));
        var k3 = r.Rows.Single(x => x.Poradi == 3);
        k3.SkutecnostDatum.Should().Be(B);
        k3.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Manual);
    }

    [Fact]
    public void AutoKrok_ZmenaVAutoRezimu_Ignoruje()
    {
        var r = Radky(Krok(3, A, SkutecnostZdrojEnum.Automat, SkutecnostRezimEnum.Auto));
        Apply(r, "Auto", new(3, B));
        var k3 = r.Rows.Single(x => x.Poradi == 3);
        k3.SkutecnostDatum.Should().Be(A, "v automatickém režimu krok řídí automat");
        k3.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Automat);
    }

    /// <summary>D3 — vypnutí automatiky a uložení bez úprav nesmí smazat data automatu.</summary>
    [Fact]
    public void PrepnutiNaRucni_NedotcenyKrok_ZachovaHodnotuIZdroj()
    {
        var r = Radky(
            Krok(1, A, SkutecnostZdrojEnum.Automat, SkutecnostRezimEnum.Auto),
            Krok(3, A, SkutecnostZdrojEnum.Automat, SkutecnostRezimEnum.Auto));
        Apply(r, "Manual");
        foreach (var poradi in new[] { 1, 3 })
        {
            var k = r.Rows.Single(x => x.Poradi == poradi);
            k.SkutecnostDatum.Should().Be(A);
            k.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Automat);
            k.SkutecnostRezim.Should().Be((byte)SkutecnostRezimEnum.Manual);
        }
    }

    /// <summary>R2 — přepnutí na automatiku smaže vše, co nedodal automat (ruční i historická data).</summary>
    [Fact]
    public void PrepnutiNaAuto_SmazeRucniIHistorickaData_NechaAutomat()
    {
        var r = Radky(
            Krok(1, A, SkutecnostZdrojEnum.Historicka, SkutecnostRezimEnum.Manual),
            Krok(3, B, SkutecnostZdrojEnum.Manual, SkutecnostRezimEnum.Manual),
            Krok(4, A, SkutecnostZdrojEnum.Automat, SkutecnostRezimEnum.Manual));
        var vysledek = Apply(r, "Auto");

        vysledek.PrepnutoNaAuto.Should().BeTrue();
        foreach (var poradi in new[] { 1, 3 })
        {
            var k = r.Rows.Single(x => x.Poradi == poradi);
            k.SkutecnostDatum.Should().BeNull("v automatickém režimu nesmí zůstat datum, které nedodal automat");
            k.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Neznamo);
        }
        r.Rows.Single(x => x.Poradi == 4).SkutecnostDatum.Should().Be(A, "data automatu zůstávají");
        r.Rows.Where(x => x.Poradi is 1 or 3 or 4)
            .Should().OnlyContain(x => x.SkutecnostRezim == (byte)SkutecnostRezimEnum.Auto);
    }

    /// <summary>R2 — smíšený stav (část kroků už Auto s ručním datem) se přepnutím vyčistí celý.</summary>
    [Fact]
    public void PrepnutiNaAuto_ZeSmisenehoStavu_VycistiIKrokyUzVAuto()
    {
        var r = Radky(
            Krok(3, A, SkutecnostZdrojEnum.Manual, SkutecnostRezimEnum.Manual),
            Krok(6, B, SkutecnostZdrojEnum.Historicka, SkutecnostRezimEnum.Auto));
        Apply(r, "Auto").PrepnutoNaAuto.Should().BeTrue();
        r.Rows.Single(x => x.Poradi == 6).SkutecnostDatum.Should().BeNull("nic mezi — po zapnutí automatiky jen data automatu");
    }

    /// <summary>Záznam už v automatickém režimu: uložení s Auto není přepnutí, nic se nemaže ani nestahuje.</summary>
    [Fact]
    public void ZaznamUzVAuto_CilAuto_NicNemeni()
    {
        var r = Radky(Krok(6, B, SkutecnostZdrojEnum.Historicka, SkutecnostRezimEnum.Auto));
        var vysledek = Apply(r, "Auto");
        vysledek.Should().Be(new ScheduleActualApplier.Vysledek(false, false));
        r.Rows.Single(x => x.Poradi == 6).SkutecnostDatum.Should().Be(B);
    }

    /// <summary>R3 — přepínač nesmí sáhnout na kroky 2/5/8/9.</summary>
    [Theory]
    [InlineData("Auto")]
    [InlineData("Manual")]
    public void PrepnutiRezimu_NemeniKroky2589(string rezim)
    {
        var r = Radky(Krok(5, A, SkutecnostZdrojEnum.Manual, SkutecnostRezimEnum.Manual));
        Apply(r, rezim);
        var k5 = r.Rows.Single(x => x.Poradi == 5);
        k5.SkutecnostDatum.Should().Be(A);
        k5.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Manual);
        k5.SkutecnostRezim.Should().Be((byte)SkutecnostRezimEnum.Manual);
    }

    /// <summary>Review Focus 5 — úkol bez řádků kroků.</summary>
    [Fact]
    public void PrepnutiNaRucni_BezRadku_ZalozeRadkyAutoKroku()
    {
        var r = Radky();
        Apply(r, "Manual");
        r.Rows.Select(x => (int)x.Poradi).Should().BeEquivalentTo(new[] { 1, 3, 4, 6, 7, 10 });
        r.Rows.Should().OnlyContain(x => x.SkutecnostRezim == (byte)SkutecnostRezimEnum.Manual);
    }

    [Fact]
    public void BezCilovehoRezimu_RidiSeStavemZaznamu()
    {
        var rucni = Radky(Krok(1, null, SkutecnostZdrojEnum.Neznamo, SkutecnostRezimEnum.Manual));
        Apply(rucni, null, new(3, B));
        rucni.Rows.Single(x => x.Poradi == 3).SkutecnostDatum.Should().Be(B, "záznam je v ručním režimu");

        var auto = Radky(Krok(1, null, SkutecnostZdrojEnum.Neznamo, SkutecnostRezimEnum.Auto));
        Apply(auto, null, new(3, B));
        auto.Rows.Should().NotContain(x => x.Poradi == 3 && x.SkutecnostDatum == B, "záznam je v automatickém režimu");
    }

    [Fact]
    public void JeZmena_PorovnavaPoDnech()
    {
        ScheduleActualApplier.JeZmena(new DateTime(2026, 10, 5, 13, 0, 0), new DateTime(2026, 10, 5)).Should().BeFalse();
        ScheduleActualApplier.JeZmena(null, null).Should().BeFalse();
        ScheduleActualApplier.JeZmena(null, A).Should().BeTrue("smazání je změna");
        ScheduleActualApplier.JeZmena(A, null).Should().BeTrue();
    }
}
```

- [ ] **Step 2: Spusť testy, musí selhat na kompilaci**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ScheduleActualApplierTests" --nologo`
Expected: build error `The type or namespace name 'ScheduleActualApplier' could not be found`.

- [ ] **Step 3: Implementuj komponentu**

```csharp
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Schedules;

/// <summary>
/// Spec 2026-10-14 §3 — jediné místo s pravidly zápisu SKUTEČNOSTI harmonogramu. Volá ho
/// uložení editoru (RecordService.PersistScheduleKrokyAsync, včetně uložení jen záložky
/// Harmonogram) i schválení návrhu změny harmonogramu (DecisionCommands). Dřív měla každá
/// cesta vlastní pravidla a rozcházela se (vady D2–D4).
///
/// Pravidla:
///  - Kroky 2/5/8/9 jsou vždy ruční (R3): změna se zapíše vždy, přepínač je nemění.
///  - Kroky 1/3/4/6/7/10: změna se zapíše jen v ručním režimu (cílovém, jinak stávajícím);
///    v automatickém je řídí automat.
///  - Přepnutí na automatiku (R2) smaže jejich data, která nedodal automat (ruční i historická)
///    — uživatel 2026-10-14: „buď to dělám ručně, nebo to dělá automat, nic mezi“.
///  - Krok bez změny se nedotkne — zůstane hodnota i zdroj (oprava D3).
/// </summary>
public static class ScheduleActualApplier
{
    /// <summary>Explicitní změna skutečnosti jednoho kroku. <c>Datum == null</c> = uživatel datum smazal.</summary>
    public sealed record SkutecnostZmena(int Poradi, DateTime? Datum);

    public sealed record Vysledek(bool Zmeneno, bool PrepnutoNaAuto);

    private static readonly int[] AutoKroky = [1, 3, 4, 6, 7, 10];

    /// <summary>Režim, se kterým se zakládá chybějící řádek kroku.</summary>
    public static byte VychoziRezim(int poradi)
        => HarmonogramManualSteps.IsManual(poradi)
            ? (byte)SkutecnostRezimEnum.Manual
            : (byte)SkutecnostRezimEnum.Auto;

    /// <summary>Změna = jiný den (čas se ignoruje); null proti hodnotě je změna (smazání).</summary>
    public static bool JeZmena(DateTime? hodnota, DateTime? puvodni)
        => hodnota?.Date != puvodni?.Date;

    /// <param name="existujici">Řádky kroků záznamu v okamžiku volání (snímek).</param>
    /// <param name="getOrCreate">Vrátí řádek kroku; chybějící založí s <see cref="VychoziRezim"/>.</param>
    /// <param name="zmeny">Jen kroky, které uživatel opravdu změnil.</param>
    /// <param name="cilovyRezim"><c>Auto</c> / <c>Manual</c>, nebo null = režim se nemění.</param>
    public static Vysledek Apply(
        IReadOnlyCollection<ZaznamHarmonogramKrokEntity> existujici,
        Func<int, ZaznamHarmonogramKrokEntity> getOrCreate,
        IReadOnlyList<SkutecnostZmena> zmeny,
        string? cilovyRezim,
        DateTime nowUtc)
    {
        var zmeneno = false;
        var prepnutoNaAuto = false;
        var rezim = HarmonogramAutoFillRule.Normalize(cilovyRezim);
        var bylAuto = HarmonogramAutoFillRule.IsAutoFill(existujici.Select(k => ((int)k.Poradi, k.SkutecnostRezim)));
        var rucniRezim = rezim is not null
            ? rezim == HarmonogramAutoFillRule.Manual
            : !bylAuto;

        if (rezim == HarmonogramAutoFillRule.Manual)
        {
            foreach (var poradi in AutoKroky)
            {
                var row = getOrCreate(poradi);
                if (row.SkutecnostRezim == (byte)SkutecnostRezimEnum.Manual) continue;
                row.SkutecnostRezim = (byte)SkutecnostRezimEnum.Manual;
                row.UpdatedAt = nowUtc;
                zmeneno = true;
            }
        }
        else if (rezim == HarmonogramAutoFillRule.Auto && !bylAuto)
        {
            // R2 (uživatel 2026-10-14: „buď to dělám ručně, nebo to dělá automat, nic mezi“):
            // po zapnutí automatiky smí kroky 1/3/4/6/7/10 nést jen data automatu. Ruční
            // i historická data se mažou — i u kroku, který už Auto byl (smíšený stav ze starých
            // návrhů). Automat je hned doplní ze SD (volající plánuje stažení podle PrepnutoNaAuto).
            foreach (var row in existujici.Where(k => !HarmonogramManualSteps.IsManual(k.Poradi)))
            {
                var mazat = row.SkutecnostDatum is not null
                    && row.SkutecnostZdroj != (byte)SkutecnostZdrojEnum.Automat;
                if (row.SkutecnostRezim == (byte)SkutecnostRezimEnum.Auto && !mazat) continue;
                row.SkutecnostRezim = (byte)SkutecnostRezimEnum.Auto;
                if (mazat)
                {
                    row.SkutecnostDatum = null;
                    row.SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Neznamo;
                }
                row.UpdatedAt = nowUtc;
            }
            zmeneno = true;
            prepnutoNaAuto = true;
        }

        foreach (var zmena in zmeny.Where(z => z.Poradi is >= 1 and <= 10).GroupBy(z => z.Poradi).Select(g => g.Last()))
        {
            if (!HarmonogramManualSteps.IsManual(zmena.Poradi) && !rucniRezim)
            {
                continue;
            }
            var row = getOrCreate(zmena.Poradi);
            row.SkutecnostDatum = zmena.Datum?.Date;
            row.SkutecnostRezim = (byte)SkutecnostRezimEnum.Manual;
            row.SkutecnostZdroj = zmena.Datum.HasValue
                ? (byte)SkutecnostZdrojEnum.Manual
                : (byte)SkutecnostZdrojEnum.Neznamo;
            row.UpdatedAt = nowUtc;
            zmeneno = true;
        }

        return new Vysledek(zmeneno, prepnutoNaAuto);
    }
}
```

- [ ] **Step 4: Spusť testy, musí projít**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ScheduleActualApplierTests" --nologo`
Expected: `Passed! - Failed: 0, Passed: 13`

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Schedules/ScheduleActualApplier.cs PmTracker.Tests.Unit/Schedules/ScheduleActualApplierTests.cs
git commit -m "feat(harmonogram): ScheduleActualApplier — jediná pravidla zápisu skutečnosti"
```

---

### Task 2: Uložení editoru na nový kontrakt

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Commands/RecordCommands.cs:148-153`
- Modify: `PmTracker.Web/Services/RecordService.SaveRecord.cs` (`PersistScheduleKrokyAsync` ~ř. 771–892, volání ř. 250 a 1218, plánování stažení ze SD ~ř. 272–280)
- Modify: `PmTracker.Web/Services/Schedules/ScheduleChronologyValidator.cs:46-66`
- Modify: `PmTracker.Tests.Integration/TestInfrastructure/RecordConcurrencyScenario.cs` (`BuildManualActualCommand`)
- Delete: `PmTracker.Tests.Integration/DataStore/ManualActualAutoEligibleTests.cs`
- Create: `PmTracker.Tests.Integration/DataStore/ScheduleActualSaveTests.cs`
- Modify: `PmTracker.Tests.Unit/Schedules/ScheduleChronologyValidatorTests.cs`
- Modify: `PmTracker.Tests.Integration/DataStore/RecordProposalDataStoreTests.cs:169-172`

**Interfaces:**
- Consumes: `ScheduleActualApplier.Apply/JeZmena/VychoziRezim` (Task 1).
- Produces: `SaveRecordHarmonogramValueCommand.SkutecnostDatumPuvodni` (`DateTime?`); `PersistScheduleKrokyAsync` vrací `ScheduleActualApplier.Vysledek`; `RecordConcurrencyScenario.BuildActualCommand(string? rezim, int poradi, DateTime? datum, DateTime? puvodni = null)`.

- [ ] **Step 1: Napiš padající integrační testy**

`PmTracker.Tests.Integration/DataStore/ScheduleActualSaveTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// Spec 2026-10-14 — uložení skutečnosti z editoru jediným seznamem HarmonogramHodnoty.
/// Nahrazuje ManualActualAutoEligibleTests (Fáze 1), které posílaly ManualActualKroky.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class ScheduleActualSaveTests
{
    private static readonly DateTime A = new(2026, 9, 10);
    private static readonly DateTime B = new(2026, 9, 20);
    private readonly SqlIntegrationFixture _fixture;

    public ScheduleActualSaveTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    private async Task<(PmTrackerDbContext Db, IntegrationTestDataStore Store, RecordConcurrencyScenario S)> SeedAsync(string dbName, string marker)
    {
        var db = await _fixture.CreateDatabaseAsync(dbName);
        var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, marker);
        return (dbContext, store, scenario);
    }

    private static Task<ZaznamHarmonogramKrokEntity> KrokAsync(PmTrackerDbContext db, int recordId, int poradi)
        => db.ZaznamHarmonogramKroky.AsNoTracking().SingleAsync(k => k.ZaznamId == recordId && k.Poradi == poradi);

    private static async Task SetKrokAsync(PmTrackerDbContext db, int recordId, int poradi, DateTime? datum, SkutecnostZdrojEnum zdroj, SkutecnostRezimEnum rezim)
    {
        var krok = await db.ZaznamHarmonogramKroky.SingleAsync(k => k.ZaznamId == recordId && k.Poradi == poradi);
        krok.SkutecnostDatum = datum;
        krok.SkutecnostZdroj = (byte)zdroj;
        krok.SkutecnostRezim = (byte)rezim;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task SaveRecord_AutoKrokVRucnimRezimu_UlozeniZmenaISmazani()
    {
        var (db, store, s) = await SeedAsync("sched_actual_auto_manual", "SAMAN");
        await using var _ = db;

        store.SaveRecord(s.BuildActualCommand("Manual", 3, A), s.CurrentUser);
        (await KrokAsync(db, s.RecordId, 3)).SkutecnostDatum.Should().Be(A);

        // D2: druhé uložení — krok má zdroj Ruční, změna se musí uložit.
        store.SaveRecord(s.BuildActualCommand("Manual", 3, B, puvodni: A), s.CurrentUser);
        (await KrokAsync(db, s.RecordId, 3)).SkutecnostDatum.Should().Be(B);

        // D2: smazání uloženého data.
        store.SaveRecord(s.BuildActualCommand("Manual", 3, null, puvodni: B), s.CurrentUser);
        var k3 = await KrokAsync(db, s.RecordId, 3);
        k3.SkutecnostDatum.Should().BeNull();
        k3.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Neznamo);
    }

    [Fact]
    public async Task SaveRecord_AutoKrokVAutoRezimu_ZmenuIgnoruje()
    {
        var (db, store, s) = await SeedAsync("sched_actual_auto_ignored", "SAIGN");
        await using var _ = db;

        store.SaveRecord(s.BuildActualCommand("Auto", 4, A), s.CurrentUser);

        (await KrokAsync(db, s.RecordId, 4)).SkutecnostDatum.Should().BeNull("v automatickém režimu krok řídí automat");
    }

    [Fact]
    public async Task SaveRecord_RucniKrok_UlozeVKazdemRezimu()
    {
        var (db, store, s) = await SeedAsync("sched_actual_manual_step", "SAMKR");
        await using var _ = db;

        store.SaveRecord(s.BuildActualCommand("Auto", 5, A), s.CurrentUser);

        (await KrokAsync(db, s.RecordId, 5)).SkutecnostDatum.Should().Be(A, "kroky 2/5/8/9 jsou vždy ruční");
    }

    /// <summary>D3 — vypnutí automatiky a uložení bez úprav nesmí smazat data automatu.</summary>
    [Fact]
    public async Task SaveRecord_PrepnutiNaRucniBezUprav_NechaDataAutomatu()
    {
        var (db, store, s) = await SeedAsync("sched_actual_keep_automat", "SAKEEP");
        await using var _ = db;
        await SetKrokAsync(db, s.RecordId, 1, A, SkutecnostZdrojEnum.Automat, SkutecnostRezimEnum.Auto);
        await SetKrokAsync(db, s.RecordId, 3, A, SkutecnostZdrojEnum.Automat, SkutecnostRezimEnum.Auto);

        // Editor předvyplní hodnotu automatu a stejnou pošle jako původní → žádná změna.
        var command = s.BuildActualCommand("Manual", 1, A, puvodni: A);
        var row3 = command.HarmonogramHodnoty.Single(x => x.Poradi == 3);
        row3.SkutecnostDatum = A;
        row3.SkutecnostDatumPuvodni = A;
        store.SaveRecord(command, s.CurrentUser);

        foreach (var poradi in new[] { 1, 3 })
        {
            var k = await KrokAsync(db, s.RecordId, poradi);
            k.SkutecnostDatum.Should().Be(A);
            k.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Automat);
            k.SkutecnostRezim.Should().Be((byte)SkutecnostRezimEnum.Manual);
        }
    }

    /// <summary>Review Focus 1 — automat změnil krok během editace; nedotčený krok nesmí vrátit starou hodnotu.</summary>
    [Fact]
    public async Task SaveRecord_NedotcenyKrok_NechaNovejsiHodnotuAutomatu()
    {
        var (db, store, s) = await SeedAsync("sched_actual_newer_automat", "SANEW");
        await using var _ = db;
        await SetKrokAsync(db, s.RecordId, 4, B, SkutecnostZdrojEnum.Automat, SkutecnostRezimEnum.Auto);

        // Editor se vykreslil, když měl krok 4 ještě hodnotu A.
        store.SaveRecord(s.BuildActualCommand("Manual", 4, A, puvodni: A), s.CurrentUser);

        (await KrokAsync(db, s.RecordId, 4)).SkutecnostDatum.Should().Be(B);
    }

    /// <summary>R2 + R3 — přepnutí na automatiku smaže ruční i historická data 1/3/4/6/7/10, kroky 2/5/8/9 nechá.</summary>
    [Fact]
    public async Task SaveRecord_PrepnutiNaAuto_SmazeNeautomatovaDataAutoKrokuANechaKroky2589()
    {
        var (db, store, s) = await SeedAsync("sched_actual_to_auto", "SATOAU");
        await using var _ = db;
        await SetKrokAsync(db, s.RecordId, 1, A, SkutecnostZdrojEnum.Historicka, SkutecnostRezimEnum.Manual);
        await SetKrokAsync(db, s.RecordId, 3, A, SkutecnostZdrojEnum.Manual, SkutecnostRezimEnum.Manual);
        await SetKrokAsync(db, s.RecordId, 5, B, SkutecnostZdrojEnum.Manual, SkutecnostRezimEnum.Manual);

        store.SaveRecord(s.BuildActualCommand("Auto", 3, null, puvodni: A), s.CurrentUser);

        (await KrokAsync(db, s.RecordId, 3)).SkutecnostDatum.Should().BeNull();
        (await KrokAsync(db, s.RecordId, 1)).SkutecnostDatum.Should().BeNull("nic mezi — historická data se mažou také");
        var k5 = await KrokAsync(db, s.RecordId, 5);
        k5.SkutecnostDatum.Should().Be(B);
        k5.SkutecnostRezim.Should().Be((byte)SkutecnostRezimEnum.Manual);
    }
}
```

`PmTracker.Tests.Integration/TestInfrastructure/RecordConcurrencyScenario.cs` — nahraď `BuildManualActualCommand` (celou metodu včetně komentáře):

```csharp
    /// <summary>
    /// Command s datem skutečnosti kroku, jak ho pošle editor: SkutecnostDatum = co uživatel
    /// nechal ve formuláři, SkutecnostDatumPuvodni = hodnota při vykreslení.
    /// </summary>
    public SaveRecordCommand BuildActualCommand(string? rezim, int poradi, DateTime? datum, DateTime? puvodni = null)
    {
        var command = BuildRenameCommand(OriginalNazev);
        command.HarmonogramRezim = rezim;
        var row = command.HarmonogramHodnoty.Single(x => x.Poradi == poradi);
        row.SkutecnostDatum = datum;
        row.SkutecnostDatumPuvodni = puvodni;
        return command;
    }
```

Smaž `PmTracker.Tests.Integration/DataStore/ManualActualAutoEligibleTests.cs` (jeho tři scénáře pokrývají první tři testy výše).

`PmTracker.Tests.Unit/Schedules/ScheduleChronologyValidatorTests.cs` — smaž helper `Actual(...)` a test `DescendingManualActual_ProducesActualChronologyIssue`; přidej:

```csharp
    private static SaveRecordHarmonogramValueCommand Skut(int poradi, DateTime datum, DateTime? puvodni = null)
        => new() { Poradi = poradi, SkutecnostDatum = datum, SkutecnostDatumPuvodni = puvodni };

    [Fact]
    public void ZmenenaKlesajiciSkutecnost_RucnichKroku_ProducesActualChronologyIssue()
    {
        var command = new SaveRecordCommand
        {
            HarmonogramHodnoty = [Skut(2, new DateTime(2026, 4, 20)), Skut(5, new DateTime(2026, 4, 5))]
        };
        var issues = Collect(command);
        issues.Should().ContainSingle();
        issues[0].Rule.Should().Be("schedule_actual_chronology");
        issues[0].FieldKey.Should().Be("HarmonogramHodnoty[1].SkutecnostDatum");
    }

    [Fact]
    public void AutoKrokyVAutomatickemRezimu_SeNekontroluji()
    {
        var command = new SaveRecordCommand
        {
            HarmonogramRezim = "Auto",
            HarmonogramHodnoty = [Skut(3, new DateTime(2026, 4, 20)), Skut(4, new DateTime(2026, 4, 5))]
        };
        Collect(command).Should().BeEmpty("kroky 1/3/4/6/7/10 v automatickém režimu řídí automat");
    }

    /// <summary>Review Focus 3 — staré nechronologické hodnoty, na které uživatel nesáhl, uložení neblokují.</summary>
    [Fact]
    public void NedotceneNechronologickeHodnoty_NeblokujiUlozeni()
    {
        var a = new DateTime(2026, 4, 20);
        var b = new DateTime(2026, 4, 5);
        var command = new SaveRecordCommand
        {
            HarmonogramRezim = "Manual",
            HarmonogramHodnoty = [Skut(3, a, puvodni: a), Skut(4, b, puvodni: b)]
        };
        Collect(command).Should().BeEmpty();
    }
```

`PmTracker.Tests.Integration/DataStore/RecordProposalDataStoreTests.cs:169-172` — nahraď přiřazení `saveCommand.ManualActualKroky = [...]` za:

```csharp
        saveCommand.HarmonogramHodnoty =
        [
            new SaveRecordHarmonogramValueCommand
            {
                Poradi = 2,
                SkutecnostDatum = originalDeadline.AddDays(-5),
                SkutecnostDatumPuvodni = originalSkutecnost
            }
        ];
```

- [ ] **Step 2: Spusť testy, musí selhat**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ScheduleChronologyValidatorTests" --nologo`
Expected: build error `'SaveRecordHarmonogramValueCommand' does not contain a definition for 'SkutecnostDatumPuvodni'`.

- [ ] **Step 3: Rozšiř příkaz**

`RecordCommands.cs` — nahraď třídu `SaveRecordHarmonogramValueCommand`:

```csharp
public sealed class SaveRecordHarmonogramValueCommand
{
    public int Poradi { get; set; }
    public DateTime? PlanDatum { get; set; }

    /// <summary>
    /// Spec 2026-10-14 — skutečnost kroku, jak ji uživatel nechal ve formuláři (prázdné = smazáno
    /// nebo nevyplněno). V payloadu návrhu (ActualHarmonogramHodnoty) jde jen u změněných kroků.
    /// </summary>
    public DateTime? SkutecnostDatum { get; set; }

    /// <summary>
    /// Spec 2026-10-14 R6 — skutečnost, se kterou se editor vykreslil. Server zapíše jen krok,
    /// kde se <see cref="SkutecnostDatum"/> od této hodnoty liší, takže nedotčený krok si nechá
    /// hodnotu i zdroj a novější hodnotu automatu z doby editace nepřepíše stará.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? SkutecnostDatumPuvodni { get; set; }
}
```

- [ ] **Step 4: Přepiš chronologii skutečnosti**

`ScheduleChronologyValidator.cs` — nahraď blok začínající komentářem `// Ruční skutečnost (kroky 2/5/8/9): neklesající mezi zadanými ručními datumy.` až po konec jeho `foreach` (před koncem metody `CollectIssues`):

```csharp
        // Spec 2026-10-14 R7: skutečnost se kontroluje jen mezi hodnotami, které po uložení patří
        // uživateli (kroky 2/5/8/9 vždy, 1/3/4/6/7/10 jen v ručním režimu), a jen u dvojic, kde se
        // aspoň jedna hodnota v tomto uložení změnila. Stará data (třeba převzatá od automatu),
        // na která uživatel nesáhl, uložení neblokují.
        var rucniRezim = HarmonogramAutoFillRule.Normalize(command.HarmonogramRezim) == HarmonogramAutoFillRule.Manual;
        var uzivatelske = command.HarmonogramHodnoty
            .Select((item, index) => (item, index))
            .Where(x => x.item.Poradi is >= 1 and <= 10 && x.item.SkutecnostDatum.HasValue)
            .Where(x => HarmonogramManualSteps.IsManual(x.item.Poradi) || rucniRezim)
            .OrderBy(x => x.item.Poradi)
            .ToList();
        for (var k = 1; k < uzivatelske.Count; k++)
        {
            var prev = uzivatelske[k - 1];
            var cur = uzivatelske[k];
            var curDate = cur.item.SkutecnostDatum!.Value.Date;
            var dotcena = ScheduleActualApplier.JeZmena(cur.item.SkutecnostDatum, cur.item.SkutecnostDatumPuvodni)
                || ScheduleActualApplier.JeZmena(prev.item.SkutecnostDatum, prev.item.SkutecnostDatumPuvodni);
            if (dotcena && curDate < prev.item.SkutecnostDatum!.Value.Date)
            {
                issues.Add(new RecordValidationIssue(
                    $"HarmonogramHodnoty[{cur.index}].SkutecnostDatum",
                    $"Skutečnost kroku {cur.item.Poradi} nesmí být dříve než skutečnost kroku {prev.item.Poradi}.",
                    "schedule",
                    "schedule_actual_chronology",
                    curDate.ToString("yyyy-MM-dd")));
            }
        }
```

`using PmTracker.Web.Models.ViewModels;` (kvůli `HarmonogramManualSteps`) i jmenný prostor `PmTracker.Web.Services.Schedules` (`ScheduleActualApplier`, `HarmonogramAutoFillRule`) soubor už má — nic nedoplňuj.

- [ ] **Step 5: Přepiš `PersistScheduleKrokyAsync` přes applier**

V `RecordService.SaveRecord.cs` nahraď celou metodu `PersistScheduleKrokyAsync` (od `private async Task<bool> PersistScheduleKrokyAsync(` po její závěrečné `return changed; }`):

```csharp
    private static readonly ScheduleActualApplier.Vysledek BezZmeny = new(false, false);

    private async Task<ScheduleActualApplier.Vysledek> PersistScheduleKrokyAsync(
        PmTracker.Web.Models.Entities.ProjektovyZaznamEntity entity,
        SaveRecordCommand command,
        bool isTaskCategory,
        bool scheduleLocked,
        CancellationToken ct)
    {
        // Pending návrh změny harmonogramu zamyká celý harmonogram (plán i skutečnost) –
        // analogicky k LocksTermDeadline u termínu. Přímý zápis kroků by kolidoval s návrhem,
        // který čeká na rozhodnutí, proto stávající krok řádky zachováme beze změny.
        if (scheduleLocked && isTaskCategory)
        {
            return BezZmeny;
        }

        if (!isTaskCategory)
        {
            var existingNonTask = await dbContext.ZaznamHarmonogramKroky
                .Where(k => k.ZaznamId == entity.Id)
                .ToListAsync(ct);
            if (existingNonTask.Count == 0)
            {
                return BezZmeny;
            }
            dbContext.ZaznamHarmonogramKroky.RemoveRange(existingNonTask);
            return new ScheduleActualApplier.Vysledek(true, false);
        }

        if (command.HarmonogramHodnoty.Count == 0)
        {
            return BezZmeny;
        }

        var nowUtc = DateTime.UtcNow;
        var existing = await dbContext.ZaznamHarmonogramKroky
            .Where(k => k.ZaznamId == entity.Id)
            .ToListAsync(ct);
        var byPoradi = existing
            .GroupBy(k => (int)k.Poradi)
            .ToDictionary(g => g.Key, g => g.First());

        PmTracker.Web.Models.Entities.ZaznamHarmonogramKrokEntity GetOrCreate(int poradi)
        {
            if (byPoradi.TryGetValue(poradi, out var r))
            {
                return r;
            }
            r = new PmTracker.Web.Models.Entities.ZaznamHarmonogramKrokEntity
            {
                ZaznamId = entity.Id,
                Poradi = (byte)poradi,
                // R3: kroky 2/5/8/9 vznikají jako ruční, ostatní jako automatické.
                SkutecnostRezim = ScheduleActualApplier.VychoziRezim(poradi),
                SkutecnostZdroj = (byte)PmTracker.Web.Models.Entities.SkutecnostZdrojEnum.Neznamo,
                UpdatedAt = nowUtc
            };
            dbContext.ZaznamHarmonogramKroky.Add(r);
            byPoradi[poradi] = r;
            return r;
        }

        // Plán se ukládá jako dřív. Skutečnost jde přes applier a jen u kroků, které uživatel
        // změnil (SkutecnostDatum ≠ SkutecnostDatumPuvodni) — spec 2026-10-14 R1/R6.
        var planZmenen = false;
        var zmeny = new List<ScheduleActualApplier.SkutecnostZmena>();
        foreach (var item in command.HarmonogramHodnoty)
        {
            if (item.Poradi is < 1 or > 10)
            {
                continue;
            }
            var row = GetOrCreate(item.Poradi);
            var plan = item.PlanDatum?.Date;
            if (row.PlanDatum != plan)
            {
                row.PlanDatum = plan;
                row.UpdatedAt = nowUtc;
                planZmenen = true;
            }
            if (ScheduleActualApplier.JeZmena(item.SkutecnostDatum, item.SkutecnostDatumPuvodni))
            {
                zmeny.Add(new ScheduleActualApplier.SkutecnostZmena(item.Poradi, item.SkutecnostDatum?.Date));
            }
        }

        var vysledek = ScheduleActualApplier.Apply(
            byPoradi.Values.ToList(), GetOrCreate, zmeny, command.HarmonogramRezim, nowUtc);
        return vysledek with { Zmeneno = vysledek.Zmeneno || planZmenen };
    }
```

Volání v `SaveRecordAsync` (ř. ~250) nahraď:

```csharp
            // Spec 2026-10-14: plán + skutečnost + režim přes ScheduleActualApplier.
            var harmonogram = await PersistScheduleKrokyAsync(entity, command, isTaskCategory, pendingScheduleProposalLock.LocksSchedule, innerCt);
```

a za smyčku `foreach (var link in addedExternalLinks) { ... }` (před `return entity.Id;`) přidej:

```csharp
            // R2: po přepnutí na automatiku doplní automat kroky 1/3/4/6/7/10 ze ServiceDesku hned,
            // ne až při příštím otevření editoru — stejně jako schválení návrhu (DecisionCommands).
            if (harmonogram.PrepnutoNaAuto)
            {
                await harvestScheduler.ScheduleHarvestForRecordAsync(
                    entity.Id, CancellationToken.None, PmTracker.Web.Services.ServiceDesk.SdReactiveSource.RecordSave);
            }
```

Ve větvi uložení jen záložky Harmonogram (ř. ~1218) nahraď `var changed = await PersistScheduleKrokyAsync(...); if (changed)` za:

```csharp
            var harmonogram = await PersistScheduleKrokyAsync(entity, command, isTaskCategory: true, scheduleLock.LocksSchedule, innerCt);
            if (harmonogram.Zmeneno)
```

a před `return entity.Id;` téže lambdy přidej stejný blok `if (harmonogram.PrepnutoNaAuto) { ... }` jako výše.

`using PmTracker.Web.Services.Schedules;` soubor má (přidán ve Fázi 1) — nic nedoplňuj.

- [ ] **Step 6: Spusť testy, musí projít**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ScheduleChronologyValidatorTests|FullyQualifiedName~ScheduleActualApplierTests" --nologo
dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~ScheduleActualSaveTests|FullyQualifiedName~RecordConcurrencyDataStoreTests|FullyQualifiedName~RecordProposalDataStoreTests" --nologo
```

Expected: vše zelené. `RecordConcurrencyDataStoreTests` musí projít beze změny (posílají plán bez skutečnosti → žádná změna skutečnosti).

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/Commands/RecordCommands.cs PmTracker.Web/Services/RecordService.SaveRecord.cs PmTracker.Web/Services/Schedules/ScheduleChronologyValidator.cs PmTracker.Tests.Integration PmTracker.Tests.Unit/Schedules/ScheduleChronologyValidatorTests.cs
git commit -m "fix(harmonogram): uložení skutečnosti z HarmonogramHodnoty přes applier (D2, D3, R2)"
```

---

### Task 3: Editor vykresluje skutečnost jedním seznamem (D1, D2, D5)

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs:247-262`
- Modify: `PmTracker.Web/Views/Shared/_ScheduleTable.cshtml` (ř. 1–9 a buňka skutečnosti ř. ~92–205)
- Modify: `PmTracker.Web/Views/Shared/_ScheduleBlockManualCell.cshtml` (ř. 6 a obsah `schedule-actual-cell__main`)
- Modify: `PmTracker.Web/Views/Shared/_ScheduleBlockManualCellInput.cshtml` (celý soubor)
- Modify: `PmTracker.Web/wwwroot/js/modules/schedule/block.js:633-656`
- Create: `PmTracker.Tests.Api/Controllers/ScheduleEditorContractTests.cs`
- Modify: `PmTracker.Tests.Unit/Harmonogram/ScheduleBlockPartialManualKrokRenderingTests.cs`
- Modify: `PmTracker.Tests.Unit/Architecture/HarmonogramPhantomUiFixesTests.cs` (`ScheduleBlockManualCell_ManualKrokInput_MaClearable`)

**Interfaces:**
- Consumes: `SkutecnostDatumPuvodni` (Task 2); uložení přes applier (Task 2).
- Produces: `ScheduleBlockManualCellViewModel.HodnotaIndex` (`int`, index řádku `HarmonogramHodnoty`).

- [ ] **Step 1: Napiš padající Api testy kontraktu (vykreslení → odeslání → DB)**

`PmTracker.Tests.Api/Controllers/ScheduleEditorContractTests.cs`:

```csharp
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-10-14 — kontrakt mezi tím, co editor vykreslí, a tím, co server uloží. Chyběl a
/// proto prošly vady D1–D3: testy posílaly příkaz rovnou službě. Tady se editor vykreslí, pole
/// harmonogramu se z HTML vytáhnou tak, jak by je odeslal prohlížeč (pm-date-field = skryté pole
/// s iso-value), a odešlou se na /Zaznamy/Save.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ScheduleEditorContractTests
{
    private static readonly DateTime A = new(2026, 10, 5);
    private readonly ApiSqlFixture _fixture;

    public ScheduleEditorContractTests(ApiSqlFixture fixture) => _fixture = fixture;

    /// <summary>Pole harmonogramu tak, jak je odešle prohlížeč.</summary>
    private static Dictionary<string, string> PoleHarmonogramu(string html)
    {
        var pole = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(html, @"<(input|pm-date-field)\b[^>]*>", RegexOptions.Singleline))
        {
            var tag = m.Value;
            var name = Regex.Match(tag, "name=\"([^\"]+)\"").Groups[1].Value;
            if (!name.StartsWith("HarmonogramHodnoty[", StringComparison.Ordinal) && name != "HarmonogramRezim")
            {
                continue;
            }
            var hodnota = m.Groups[1].Value == "pm-date-field"
                ? Regex.Match(tag, "iso-value=\"([^\"]*)\"").Groups[1].Value
                : Regex.Match(tag, "value=\"([^\"]*)\"").Groups[1].Value;
            pole.Add(name, hodnota); // Add: duplicitní pole = chyba kontraktu
        }
        return pole;
    }

    private async Task<(int RecordId, int ProjectId, string SubsystemCode, string StatusCode, int OwnerId)> ZalozUkolAsync(string marker)
    {
        var ownerId = await _fixture.EnsurePersonAsync(marker);
        var projectId = await _fixture.EnsureProjectAsync(marker);
        var subsystemId = await _fixture.EnsureSubsystemAsync(marker + "SUB", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", marker);
        await using var db = _fixture.CreateDbContext();
        for (var poradi = 1; poradi <= 10; poradi++)
        {
            db.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = recordId,
                Poradi = (byte)poradi,
                PlanDatum = new DateTime(2026, 10, 1).AddDays(poradi * 7),
                SkutecnostRezim = (byte)SkutecnostRezimEnum.Auto,
                SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Neznamo,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
        var subsystemCode = await db.Subsystemy.Where(x => x.Id == subsystemId).Select(x => x.Kod).SingleAsync();
        var statusCode = await db.CiselnikStavuUkolu.OrderBy(x => x.Id).Select(x => x.Kod).FirstAsync();
        return (recordId, projectId, subsystemCode, statusCode, ownerId);
    }

    private async Task NastavKrokAsync(int recordId, int poradi, DateTime? datum, SkutecnostZdrojEnum zdroj, SkutecnostRezimEnum rezim)
    {
        await using var db = _fixture.CreateDbContext();
        var k = await db.ZaznamHarmonogramKroky.SingleAsync(x => x.ZaznamId == recordId && x.Poradi == poradi);
        k.SkutecnostDatum = datum;
        k.SkutecnostZdroj = (byte)zdroj;
        k.SkutecnostRezim = (byte)rezim;
        await db.SaveChangesAsync();
    }

    private async Task<ZaznamHarmonogramKrokEntity> KrokAsync(int recordId, int poradi)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.ZaznamHarmonogramKroky.AsNoTracking().SingleAsync(x => x.ZaznamId == recordId && x.Poradi == poradi);
    }

    private async Task<string> EditorAsync(HttpClient client, int recordId)
        => await client.GetStringAsync($"/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}");

    private async Task UlozAsync(HttpClient client, (int RecordId, int ProjectId, string SubsystemCode, string StatusCode, int OwnerId) z,
        string html, Action<Dictionary<string, string>> uprav)
    {
        var pole = PoleHarmonogramu(html);
        uprav(pole);
        var recordVersion = Regex.Match(html, "name=\"RecordVersion\" value=\"([^\"]*)\"").Groups[1].Value;
        var form = new List<(string, string)>
        {
            ("Id", z.RecordId.ToString()), ("ProjektId", z.ProjectId.ToString()), ("Kategorie", "U"),
            ("Stav", z.StatusCode), ("Nazev", "Kontrakt harmonogramu"), ("VlastnikId", z.OwnerId.ToString()),
            ("DatumZalozeni", "2026-10-01"), ("TerminUkonceni", "2026-12-31"), ("Subsystem", z.SubsystemCode),
            ("EditorTab", "schedule"), ("RecordVersion", recordVersion)
        };
        form.AddRange(pole.Select(p => (p.Key, p.Value)));
        var response = await client.SendAsync(ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/Save?asUser={_fixture.AdminOsobaId}", ApiTestHttpHelper.BuildForm(form.ToArray())));
        var result = await ApiTestHttpHelper.ReadModalResultAsync(response);
        result.Ok.Should().BeTrue(result.Message);
    }

    [Fact]
    public async Task Editor_KazdyKrokMaJedinePoleSkutecnosti_BezManualActualKroky()
    {
        var z = await ZalozUkolAsync("APICONTR1");
        await NastavKrokAsync(z.RecordId, 3, A, SkutecnostZdrojEnum.Manual, SkutecnostRezimEnum.Manual);
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var html = await EditorAsync(client, z.RecordId);

        html.Should().NotContain("ManualActualKroky", "skutečnost jde jediným seznamem (R1)");
        html.Should().NotContain("data-manual-zdroj-dropdown", "volba Ručně / Z vyjádření je mrtvý kód (D5)");
        var pole = PoleHarmonogramu(html);
        for (var i = 0; i < 10; i++)
        {
            pole.Should().ContainKey($"HarmonogramHodnoty[{i}].Poradi");
            pole.Should().ContainKey($"HarmonogramHodnoty[{i}].SkutecnostDatum", $"krok na řádku {i} musí mít pole skutečnosti");
        }
        pole["HarmonogramHodnoty[2].SkutecnostDatum"].Should().Be("2026-10-05", "uložené ruční datum je předvyplněné (D2)");
        pole["HarmonogramHodnoty[2].SkutecnostDatumPuvodni"].Should().Be("2026-10-05");
    }

    /// <summary>D1 + D2 — po uloženém ručním datu jde změnit, smazat i vyplnit pozdější kroky.</summary>
    [Fact]
    public async Task Ulozeni_PoUlozenemRucnimDatu_ZmenaSmazaniADalsiKroky()
    {
        var z = await ZalozUkolAsync("APICONTR2");
        await NastavKrokAsync(z.RecordId, 3, A, SkutecnostZdrojEnum.Manual, SkutecnostRezimEnum.Manual);
        await NastavKrokAsync(z.RecordId, 7, A, SkutecnostZdrojEnum.Manual, SkutecnostRezimEnum.Manual);
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        await UlozAsync(client, z, await EditorAsync(client, z.RecordId), pole =>
        {
            pole["HarmonogramHodnoty[2].SkutecnostDatum"] = "";            // smazat krok 3
            pole["HarmonogramHodnoty[6].SkutecnostDatum"] = "2026-10-22";  // změnit krok 7
            pole["HarmonogramHodnoty[3].SkutecnostDatum"] = "2026-10-09";  // krok 4 za bývalou dírou
            pole["HarmonogramHodnoty[4].SkutecnostDatum"] = "2026-10-12";  // krok 5 (vždy ruční)
        });

        (await KrokAsync(z.RecordId, 3)).SkutecnostDatum.Should().BeNull();
        (await KrokAsync(z.RecordId, 7)).SkutecnostDatum.Should().Be(new DateTime(2026, 10, 22));
        (await KrokAsync(z.RecordId, 4)).SkutecnostDatum.Should().Be(new DateTime(2026, 10, 9));
        (await KrokAsync(z.RecordId, 5)).SkutecnostDatum.Should().Be(new DateTime(2026, 10, 12));
    }

    /// <summary>D3 — vypnutí automatiky a uložení bez úprav nesmaže data automatu.</summary>
    [Fact]
    public async Task Ulozeni_VypnutiAutomatikyBezUprav_NechaDataAutomatu()
    {
        var z = await ZalozUkolAsync("APICONTR3");
        await NastavKrokAsync(z.RecordId, 1, A, SkutecnostZdrojEnum.Automat, SkutecnostRezimEnum.Auto);
        await NastavKrokAsync(z.RecordId, 3, A, SkutecnostZdrojEnum.Automat, SkutecnostRezimEnum.Auto);
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        await UlozAsync(client, z, await EditorAsync(client, z.RecordId), pole => pole["HarmonogramRezim"] = "Manual");

        foreach (var poradi in new[] { 1, 3 })
        {
            var k = await KrokAsync(z.RecordId, poradi);
            k.SkutecnostDatum.Should().Be(A);
            k.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Automat);
            k.SkutecnostRezim.Should().Be((byte)SkutecnostRezimEnum.Manual);
        }
    }
}
```

- [ ] **Step 2: Spusť testy, musí selhat**

Run: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~ScheduleEditorContractTests" --nologo`
Expected: FAIL — `html` obsahuje `ManualActualKroky`; chybí `HarmonogramHodnoty[2].SkutecnostDatum` u kroků vykreslených ruční buňkou; `Ulozeni_*` neuloží (D1/D3).

- [ ] **Step 3: View model buňky**

`ProjektHarmonogramTabViewModels.cs` — v `ScheduleBlockManualCellViewModel` nahraď vlastnost `ManualInputIndex`:

```csharp
    /// <summary>
    /// Spec 2026-10-14 R1 — index řádku v <c>HarmonogramHodnoty[i]</c> (pevný 0–9 podle pořadí
    /// řádku tabulky). Skutečnost jde stejným indexem jako Poradi a PlanDatum, takže díra
    /// v číslování nemůže vzniknout.
    /// </summary>
    public required int HodnotaIndex { get; init; }
```

- [ ] **Step 4: Tabulka — jedna větev pro existující záznam**

`_ScheduleTable.cshtml` — v úvodním bloku (ř. 1–9) smaž řádky
```
    // Plán D Task 8: pořadí manual kroků v rámci ManualActualKroky[] binding.
    // Počítá se jen pro kroky, u kterých opravdu renderujeme manual input/read-only.
    var manualInputIndex = 0;
```

V buňce skutečnosti nahraď blok od `@{` s komentářem `// Plán D Task 8: Skutečnost sloupec — rozhodnutí mezi 4 scénáři:` až po konec větve `else if (useNoneCell) { ... }` (tj. vše před `else` se záložním polem `HarmonogramHodnoty[i].SkutecnostDatum`) tímto:

```razor
                                @{
                                    var isKrokLocked = krok.KrokIndex >= 1 && lockedManualKrokKeys.Contains(krok.KrokIndex);
                                    // Spec 2026-10-14 R1: skutečnost jde polem HarmonogramHodnoty[i].SkutecnostDatum
                                    // se stejným indexem i jako Poradi a PlanDatum výše → díra v číslování nevznikne.
                                    // U existujícího záznamu obsluhuje všechny kroky ruční buňka (varianty Auto/Manual
                                    // řídí přepínač). Záložní zamčené pole níže zůstává jen pro zakládání záznamu.
                                    var useManualCell = krok.IsManualKrok || Model.RecordId > 0;
                                }
                                @if (Model.HideActual)
                                {
                                    @* 7b: návrh založení vyplňuje pouze plán; skutečnost až po založení. *@
                                    <span class="muted schedule-actual-after-create" data-schedule-actual-hidden>Vyplní se po založení</span>
                                }
                                else if (useManualCell)
                                {
                                    @await Html.PartialAsync(
                                        "_ScheduleBlockManualCell",
                                        new ScheduleBlockManualCellViewModel
                                        {
                                            Krok = krok,
                                            HodnotaIndex = i,
                                            ZaznamId = Model.RecordId,
                                            CanEditManualActual = Model.CanEditManualActual,
                                            IsLocked = isKrokLocked
                                        })
                                }
```

V navazující větvi `else` (záložní pole) změň první řádek komentáře na `// Zakládání záznamu: kroky 1/3/4/6/7/10 ještě nemají skutečnost — zamčené pole.` Zbytek větve beze změny.

- [ ] **Step 5: Vstup skutečnosti**

`_ScheduleBlockManualCellInput.cshtml` — nahraď celý soubor:

```razor
@model ScheduleBlockManualCellViewModel
@*
    Spec 2026-10-14 R1/R6 — editovatelná skutečnost kroku. Pole HarmonogramHodnoty[i].SkutecnostDatum
    je předvyplněné AKTUÁLNÍ hodnotou bez ohledu na zdroj (automat / ruční / historická) a vedle něj
    jde SkutecnostDatumPuvodni s toutéž hodnotou. Server zapíše jen krok, kde se obě liší, takže
    nedotčený krok si nechá hodnotu i zdroj.
    U kroků 1/3/4/6/7/10 je vstup varianta „Manual“ (data-rezim-show-on); přepínač režimu
    (rezim-master-switch.js) ho při přepnutí na Auto vyčistí přes data-manual-krok-auto-eligible.
    Kroky 2/5/8/9 jsou vždy ruční → vstup je vidět v obou režimech.
*@
@{
    var krok = Model.Krok;
    var i = Model.HodnotaIndex;
    var autoEligible = !krok.IsManualKrok;
    var isoValue = krok.SkutecneDatum?.ToString("yyyy-MM-dd") ?? string.Empty;
    var displayValue = krok.SkutecneDatum?.ToString("dd.MM.yyyy") ?? string.Empty;
    var extraData = new Dictionary<string, object?>
    {
        ["data-manual-krok-input"] = "true",
        ["data-manual-krok-index"] = krok.KrokIndex.ToString(),
        ["data-manual-krok-key"] = krok.KrokIndex.ToString()
    };
    if (autoEligible)
    {
        extraData["data-manual-krok-auto-eligible"] = "true";
    }
    // Razor nevynechá data-* atribut s null hodnotou → skládá se ručně.
    var showOnAttr = autoEligible ? " data-rezim-show-on=\"Manual\"" : string.Empty;
    var cssClass = autoEligible
        ? "schedule-actual-manual schedule-actual-manual-editable schedule-actual-manual-auto-eligible"
        : "schedule-actual-manual schedule-actual-manual-editable";
}
<div class="@cssClass" data-schedule-actual-source="manual"@Html.Raw(showOnAttr)>
    <input type="hidden"
           name="HarmonogramHodnoty[@i].SkutecnostDatumPuvodni"
           value="@isoValue"
           data-schedule-actual-puvodni />
    @await Html.PartialAsync(
        "_AppDateField",
        new AppDateFieldViewModel
        {
            Name = $"HarmonogramHodnoty[{i}].SkutecnostDatum",
            IsoValue = isoValue,
            DisplayValue = displayValue,
            Locked = false,
            Clearable = true,
            AriaLabel = $"Skutečnost kroku {krok.Nazev}",
            ContainerCssClass = "schedule-actual-manual-date",
            ExtraDataAttributes = extraData
        })
</div>
```

- [ ] **Step 6: Buňka skutečnosti — varianty a odstranění D5**

`_ScheduleBlockManualCell.cshtml` ř. 6: `var manualInputIndex = Model.ManualInputIndex;` → `var hodnotaIndex = Model.HodnotaIndex;`.

Nahraď celý obsah elementu `<div class="schedule-actual-cell__main">` (od komentáře `FIX 2026-05-04: pro auto-eligible kroky (1/3/4/6/7/10, isManualKrok=false) renderuje cell` po konec poslední větve `else { ... }` s varováním; uzavírací `</div></div>` zůstávají):

```razor
@*
    Spec 2026-10-14: skutečnost kroku jde jediným polem HarmonogramHodnoty[i].SkutecnostDatum.
    Kroky 1/3/4/6/7/10 mají dvě varianty řízené atributem data-skutecnost-rezim na buňce
    (CSS components/schedule-actual-cell-feature-c.css): Auto = co vyplnil automat nebo co je
    uložené, Manual = editovatelný vstup. Kroky 2/5/8/9 jsou vždy ruční (R3) — automat je
    nevyplňuje (HarmonogramKrokDatumMapping je nemapuje), proto nemají variantu Auto.
*@
@{
    var skutecneDisplay = krok.SkutecneDatum?.ToString("dd.MM.yyyy");
    var readonlyTooltip = isLocked
        ? "Skutečnost tohoto kroku je uzamčená — čeká se na rozhodnutí návrhu."
        : "Nemáte oprávnění editovat skutečnost.";
    var vstupModel = new ScheduleBlockManualCellViewModel
    {
        Krok = krok,
        HodnotaIndex = hodnotaIndex,
        ZaznamId = Model.ZaznamId,
        CanEditManualActual = canEdit,
        IsLocked = isLocked
    };
}
@if (isManualKrok)
{
    if (canEdit)
    {
        <partial name="_ScheduleBlockManualCellInput" model="@vstupModel" />
    }
    else
    {
        <div class="schedule-actual-manual schedule-actual-manual-readonly" data-schedule-actual-source="manual">
            <span class="schedule-actual-datum"
                  data-manual-krok-readonly
                  data-manual-krok-locked="@(isLocked ? "true" : "false")"
                  title="@readonlyTooltip">@(skutecneDisplay ?? "—")</span>
        </div>
    }
}
else
{
    @* Varianta Auto *@
    if (zdroj == PmTracker.Web.Services.Records.ZdrojSkutecnosti.FromVyjadreni)
    {
        var tooltip = krok.SourceVyjadreniDatum is { } vyjDatum
            ? $"Z vyjádření {vyjDatum:dd.MM.yyyy}"
            : "Skutečnost pochází z navázaného vyjádření.";
        var visibleDatum = krok.SourceVyjadreniDatum?.ToString("dd.MM.yyyy") ?? skutecneDisplay ?? "—";
        <div class="schedule-actual-from-vyjadreni" data-schedule-actual-source="vyjadreni" data-rezim-show-on="Auto">
            <span class="schedule-actual-datum" data-manual-krok-readonly>@visibleDatum</span>
            @if (krok.SourceExterniOdkazId.HasValue && Model.ZaznamId > 0)
            {
                <pm-button variant="Ghost"
                           size="Small"
                           data-external-chat-open="true"
                           data-external-chat-mode="@(Model.VyjadreniOknoUpravy ? "edit" : "read")"
                           data-external-odkaz-id="@krok.SourceExterniOdkazId.Value"
                           data-external-zaznam-id="@Model.ZaznamId"
                           data-schedule-actual-from-vyjadreni
                           title="@tooltip"
                           aria-label="@tooltip">
                    <gov-icon size="s" name="chat" type="components" aria-hidden="true"></gov-icon>
                </pm-button>
            }
        </div>
    }
    else if (zdroj == PmTracker.Web.Services.Records.ZdrojSkutecnosti.Manual && skutecneDisplay is not null)
    {
        @* Ruční (✍️) nebo historické (📜) datum uložené na kroku. V Auto režimu se jen zobrazí —
           nová data tam nevzniknou (přepnutí na automatiku je maže, R2), zůstat mohou jen
           z migrace starých delay hodnot (1_3_10, Historická + Auto). *@
        <div class="schedule-actual-stored" data-schedule-actual-source="stored" data-rezim-show-on="Auto">
            <span class="schedule-actual-datum" data-manual-krok-readonly>@skutecneDisplay</span>
        </div>
    }
    else
    {
        <div class="schedule-actual-pending" data-schedule-actual-source="none" data-rezim-show-on="Auto">
            <span class="schedule-actual-datum" data-manual-krok-readonly>—</span>
            <span class="schedule-actual-warning"
                  data-manual-krok-warning
                  title="Automat zatím nenašel vhodné vyjádření.">
                <gov-icon size="s" name="exclamation-triangle-fill" type="components" aria-hidden="true"></gov-icon>
            </span>
        </div>
    }

    @* Varianta Manual *@
    if (canEdit)
    {
        <partial name="_ScheduleBlockManualCellInput" model="@vstupModel" />
    }
    else
    {
        <div class="schedule-actual-manual schedule-actual-manual-readonly" data-schedule-actual-source="manual" data-rezim-show-on="Manual">
            <span class="schedule-actual-datum"
                  data-manual-krok-readonly
                  data-manual-krok-locked="@(isLocked ? "true" : "false")"
                  title="@readonlyTooltip">@(skutecneDisplay ?? "—")</span>
        </div>
    }
}
```

Bublina `FromVyjadreni` výše odpovídá stavu souboru po commitu `ee433a7` (2026-10-13, `data-external-chat-mode`); mění se jen výpočet `tooltip`/`visibleDatum`, chování zůstává.

- [ ] **Step 7: Graf čte skutečnost z jediného pole**

`block.js` ~ř. 633 — komentář nahraď:
```js
    // Plán: [name$=".PlanDatum"]. Skutečnost: HarmonogramHodnoty[*].SkutecnostDatum (spec 2026-10-14),
    // prázdné pole (vyčištěné přepínačem) → uložená hodnota z data-step-actual-iso.
```
a v `computeDateModel` řádek `const actualIso = this.rowDateIso(row, "AbsolutniDatum")` s pokračováním nahraď:
```js
            const actualIso = this.rowDateIso(row, "SkutecnostDatum")
                || (String(row.dataset.stepActualIso || "").trim() || null);
```

- [ ] **Step 8: Uprav zdrojové testy šablon**

`ScheduleBlockPartialManualKrokRenderingTests.cs`:
- v `Partial_Renders_WarningIcon_For_None_State` smaž assert na `"ZdrojSkutecnosti.None"` (varování je teď výchozí větev; `data-manual-krok-warning` zůstává),
- `Partial_Renders_DateInput_For_Manual_State` nahraď:

```csharp
    [Fact]
    public void Partial_Renders_DateInput_For_Manual_State()
    {
        var text = LoadCombinedPartialText();
        text.Should().Contain("data-manual-krok-input",
            "JS modul manualKroky.js se váže přes data-manual-krok-input.");
        text.Should().Contain("HarmonogramHodnoty[",
            "Spec 2026-10-14 R1: skutečnost jde jediným seznamem HarmonogramHodnoty[i].");
        text.Should().Contain(".SkutecnostDatumPuvodni",
            "Spec 2026-10-14 R6: s vstupem jde hodnota při vykreslení.");
        text.Should().NotContain("ManualActualKroky",
            "druhý seznam s vlastním čítačem způsoboval díru v číslování (D1).");
        text.Should().NotContain("data-manual-zdroj-dropdown",
            "volba Ručně / Z vyjádření u kroků 2/5/8/9 byla mrtvý kód (D5).");
    }
```

`HarmonogramPhantomUiFixesTests.ScheduleBlockManualCell_ManualKrokInput_MaClearable` — čti `_ScheduleBlockManualCellInput.cshtml` místo `_ScheduleBlockManualCell.cshtml` (vstup se přesunul do sdíleného partialu).

- [ ] **Step 9: Spusť testy, musí projít**

```bash
dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~ScheduleEditorContractTests|FullyQualifiedName~RecordEditorControllerTests|FullyQualifiedName~ScheduleProposal" --nologo
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --nologo
```
Expected: zelené; Unit bez nových selhání proti Task 0.

- [ ] **Step 10: Commit**

```bash
git add PmTracker.Web/Views/Shared PmTracker.Web/Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs PmTracker.Web/wwwroot/js/modules/schedule/block.js PmTracker.Tests.Api/Controllers/ScheduleEditorContractTests.cs PmTracker.Tests.Unit/Harmonogram PmTracker.Tests.Unit/Architecture/HarmonogramPhantomUiFixesTests.cs
git commit -m "fix(harmonogram): editor posílá skutečnost jedním seznamem — bez díry v číslování (D1, D2, D5)"
```

---

### Task 4: Návrhy změny harmonogramu na nový kontrakt

**Files:**
- Create: `PmTracker.Web/Services/Records/RecordProposalPayloadJson.cs`
- Modify: `PmTracker.Web/Models/ViewModels/RecordProposalViewModels.cs` (vlastnosti `ManualActualKroky` u `CreateRecordProposalPayload` a `SchedulePlanProposalPayload`)
- Modify: `PmTracker.Web/Services/Records/RecordProposalPayloadMapper.cs`
- Modify: `PmTracker.Web/Services/RecordProposalService.SubmitCommands.cs` (ř. ~140, ~248, ~262, ~267–271, ~330–350)
- Modify: `PmTracker.Web/Services/RecordProposalService.DecisionCommands.cs` (ř. ~40–46, ~273–360, ~426–455, `GetOrCreateKrok`)
- Modify: `PmTracker.Web/Services/RecordProposalService.Queries.cs` (~ř. 256–275)
- Modify: `PmTracker.Web/Services/RecordProposalService.cs:86-94`
- Modify: `PmTracker.Web/Services/Records/PendingScheduleProposalLockEvaluator.cs` (~ř. 62, 84–90)
- Test: `PmTracker.Tests.Unit/Records/RecordProposalPayloadJsonTests.cs` (Create)
- Test: `PmTracker.Tests.Unit/Records/RecordProposalPayloadMapperScheduleTests.cs` (Modify)
- Test: `PmTracker.Tests.Integration/DataStore/ScheduleProposalRezimTests.cs` (testy 1 a 2), `RecordProposalDataStoreTests.cs:152-155`

**Interfaces:**
- Consumes: `ScheduleActualApplier` (Task 1), `SkutecnostDatumPuvodni` (Task 2), editor návrhu vykreslený přes `_ScheduleTable` (Task 3).
- Produces: `RecordProposalPayloadJson.Deserialize(string? json) : RecordProposalPayload`; `SchedulePlanProposalPayload.ActualHarmonogramHodnoty` = **seznam změn**.

- [ ] **Step 1: Napiš padající jednotkové testy**

`PmTracker.Tests.Unit/Records/RecordProposalPayloadJsonTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Records;
using Xunit;

namespace PmTracker.Tests.Unit.Records;

/// <summary>Spec 2026-10-14 R4 — starý JSON návrhu (ManualActualKroky) se čte převedený.</summary>
public sealed class RecordProposalPayloadJsonTests
{
    [Fact]
    public void StaryFormat_RucniDataSePrevedouNaZmenySkutecnosti()
    {
        const string json = """
            {"ProposalType":"SCHEDULE_PLAN_CHANGE","SchedulePlan":{"ZaznamId":7,
             "ActualHarmonogramHodnoty":[{"Poradi":4,"SkutecnostDatum":"2026-10-01T00:00:00"}],
             "ManualActualKroky":[{"Poradi":3,"AbsolutniDatum":"2026-10-05","PreferredZdroj":null},
                                  {"Poradi":5,"AbsolutniDatum":null},
                                  {"Poradi":4,"AbsolutniDatum":"2026-10-09"}]}}
            """;

        var plan = RecordProposalPayloadJson.Deserialize(json).SchedulePlan!;

        plan.ActualHarmonogramHodnoty.Select(x => (x.Poradi, x.SkutecnostDatum)).Should().BeEquivalentTo(new[]
        {
            (4, (DateTime?)new DateTime(2026, 10, 1)),   // existující položka vyhrává
            (3, (DateTime?)new DateTime(2026, 10, 5))    // prázdné AbsolutniDatum = „krok nenastal“, ne smazání
        });
    }

    [Fact]
    public void NovyFormat_CteSeBezeZmeny()
    {
        const string json = """
            {"ProposalType":"SCHEDULE_PLAN_CHANGE","SchedulePlan":{"ZaznamId":7,
             "ActualHarmonogramHodnoty":[{"Poradi":3,"SkutecnostDatum":null}],"ChangesScheduleActual":true}}
            """;

        var plan = RecordProposalPayloadJson.Deserialize(json).SchedulePlan!;

        plan.ActualHarmonogramHodnoty.Should().ContainSingle(x => x.Poradi == 3 && x.SkutecnostDatum == null,
            "null v seznamu změn znamená smazat");
    }

    [Fact]
    public void PrazdnyJson_VratiPrazdnyPayload()
        => RecordProposalPayloadJson.Deserialize("  ").SchedulePlan.Should().BeNull();
}
```

`RecordProposalPayloadMapperScheduleTests.cs` — přidej:

```csharp
    private static SaveRecordCommand CommandSeSkutecnosti(string rezim, params (int Poradi, DateTime? Datum, DateTime? Puvodni)[] kroky)
        => new()
        {
            TerminUkonceni = new DateTime(2026, 10, 31),
            HarmonogramRezim = rezim,
            HarmonogramHodnoty = kroky.Select(k => new SaveRecordHarmonogramValueCommand
            {
                Poradi = k.Poradi, SkutecnostDatum = k.Datum, SkutecnostDatumPuvodni = k.Puvodni
            }).ToList()
        };

    [Fact]
    public void BuildSchedulePayload_SkutecnostJeSeznamZmen()
    {
        var a = new DateTime(2026, 10, 5);
        var command = CommandSeSkutecnosti("Manual", (2, a, a), (3, null, a), (5, a, null));

        var plan = new RecordProposalPayloadMapper().BuildSchedulePayload(command, new DateTime(2026, 10, 31), currentAutoFill: false).SchedulePlan!;

        plan.ActualHarmonogramHodnoty.Select(x => (x.Poradi, x.SkutecnostDatum)).Should().BeEquivalentTo(new[]
        {
            (3, (DateTime?)null),   // smazáno
            (5, (DateTime?)a)       // nově vyplněno; krok 2 beze změny se nepošle
        });
        plan.ChangesScheduleActual.Should().BeTrue();
    }

    /// <summary>Review Focus 4 — přepnutí na automatiku v návrhu vyčistí vstupy; to nejsou změny.</summary>
    [Fact]
    public void BuildSchedulePayload_CilAuto_VynechaZmenyAutoKroku()
    {
        var a = new DateTime(2026, 10, 5);
        var command = CommandSeSkutecnosti("Auto", (3, null, a), (5, a, null));

        var plan = new RecordProposalPayloadMapper().BuildSchedulePayload(command, new DateTime(2026, 10, 31), currentAutoFill: false).SchedulePlan!;

        plan.ActualHarmonogramHodnoty.Should().ContainSingle(x => x.Poradi == 5,
            "v automatickém režimu se kroky 1/3/4/6/7/10 neposílají, kroky 2/5/8/9 ano");
    }
```

- [ ] **Step 2: Spusť testy, musí selhat**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~RecordProposalPayload" --nologo`
Expected: build error `RecordProposalPayloadJson` neexistuje.

- [ ] **Step 3: Čtení JSON s převodem starého formátu**

`PmTracker.Web/Services/Records/RecordProposalPayloadJson.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records;

/// <summary>
/// Spec 2026-10-14 R4 — jediné místo, kde se čte JSON návrhu. Návrhy uložené před sloučením
/// skutečnosti do jednoho seznamu nesou ruční data v <c>ManualActualKroky</c>; převedou se na
/// <see cref="SchedulePlanProposalPayload.ActualHarmonogramHodnoty"/> (seznam změn). Prázdná
/// položka znamenala „krok nenastal“, ne smazání, proto se nepřevádí. Díky převodu se
/// rozpracované návrhy nemusí mazat a historie zůstane čitelná.
/// </summary>
public static class RecordProposalPayloadJson
{
    private const string LegacyManualKey = "ManualActualKroky";

    public static RecordProposalPayload Deserialize(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return new RecordProposalPayload();
        }

        var root = JsonNode.Parse(payloadJson);
        if (root is JsonObject obj)
        {
            if (obj["SchedulePlan"] is JsonObject plan)
            {
                PrevedStaraRucniData(plan);
            }
            // Návrh založení skutečnost nenese (HideActual) — starý klíč jen zahodit.
            (obj["CreateRecord"] as JsonObject)?.Remove(LegacyManualKey);
        }

        return root?.Deserialize<RecordProposalPayload>() ?? new RecordProposalPayload();
    }

    private static void PrevedStaraRucniData(JsonObject plan)
    {
        if (plan[LegacyManualKey] is not JsonArray legacy)
        {
            return;
        }
        plan.Remove(LegacyManualKey);

        if (plan["ActualHarmonogramHodnoty"] is not JsonArray actual)
        {
            actual = new JsonArray();
            plan["ActualHarmonogramHodnoty"] = actual;
        }

        var obsazene = actual.OfType<JsonObject>()
            .Select(a => a["Poradi"]?.GetValue<int>())
            .OfType<int>()
            .ToHashSet();
        foreach (var item in legacy.OfType<JsonObject>())
        {
            var poradi = item["Poradi"]?.GetValue<int>();
            var datum = item["AbsolutniDatum"]?.GetValue<string>();
            if (poradi is null || string.IsNullOrWhiteSpace(datum) || !obsazene.Add(poradi.Value))
            {
                continue;
            }
            actual.Add(new JsonObject
            {
                ["Poradi"] = poradi.Value,
                ["SkutecnostDatum"] = datum + "T00:00:00"
            });
        }
    }
}
```

`RecordProposalService.cs` — tělo `DeserializePayload` nahraď `=> RecordProposalPayloadJson.Deserialize(payloadJson);` (signatura beze změny).
`PendingScheduleProposalLockEvaluator.cs` ř. ~62 — `JsonSerializer.Deserialize<RecordProposalPayload>(proposal.PayloadJson)` → `RecordProposalPayloadJson.Deserialize(proposal.PayloadJson)`.

- [ ] **Step 4: Payload bez `ManualActualKroky`, skutečnost = seznam změn**

`RecordProposalViewModels.cs` — smaž vlastnost `ManualActualKroky` (včetně XML komentáře) v `CreateRecordProposalPayload` i `SchedulePlanProposalPayload`. U `SchedulePlanProposalPayload.ActualHarmonogramHodnoty` doplň komentář:

```csharp
    /// <summary>
    /// Spec 2026-10-14 — SEZNAM ZMĚN skutečnosti (ne stav všech kroků). <c>SkutecnostDatum == null</c>
    /// = smazat. Kroky 1/3/4/6/7/10 obsahuje jen u návrhu v ručním režimu.
    /// </summary>
```

`RecordProposalPayloadMapper.cs`:
- smaž helper `Manual(...)` a řádky `ManualActualKroky = ...` v `BuildCreatePayload`, `BuildSchedulePayload` i `BuildSaveCommand`,
- v `BuildSchedulePayload` nahraď výpočet `actual` a přiřazení `ChangesScheduleActual`:

```csharp
        // Spec 2026-10-14: skutečnost jde jako seznam ZMĚN (SkutecnostDatum ≠ SkutecnostDatumPuvodni).
        // V automatickém cílovém režimu se změny kroků 1/3/4/6/7/10 nepošlou — přepínač je při
        // přepnutí jen vyčistil a po schválení je stejně řídí automat.
        var rezim = HarmonogramAutoFillRule.Normalize(command.HarmonogramRezim);
        var cilAuto = rezim is null ? currentAutoFill : rezim == HarmonogramAutoFillRule.Auto;
        var actual = command.HarmonogramHodnoty
            .Where(v => v.Poradi is >= 1 and <= 10)
            .Where(v => ScheduleActualApplier.JeZmena(v.SkutecnostDatum, v.SkutecnostDatumPuvodni))
            .Where(v => HarmonogramManualSteps.IsManual(v.Poradi) || !cilAuto)
            .Select(v => new SaveRecordHarmonogramValueCommand { Poradi = v.Poradi, SkutecnostDatum = v.SkutecnostDatum?.Date })
            .ToList();
```
a `ChangesScheduleActual = actual.Count > 0,`. Původní řádek `var rezim = HarmonogramAutoFillRule.Normalize(command.HarmonogramRezim);` níže smaž (proměnná je teď výš).

- [ ] **Step 5: Odeslání a schválení návrhu**

`RecordProposalService.SubmitCommands.cs`:
- smaž volání `ValidateAutoStepsNotInScheduleProposal(command);` (ř. ~140) i metodu `ValidateAutoStepsNotInScheduleProposal` (ř. ~330–350) — mapper teď kroky 1/3/4/6/7/10 v automatickém režimu vynechá, odmítat je by zablokovalo návrh po přepnutí,
- smaž oba řádky `ValidateManualActualKroky(command.ManualActualKroky);` a soukromou metodu `ValidateManualActualKroky`.

`RecordProposalService.DecisionCommands.cs`:
- ř. ~44: podmínku `(createPayload.HarmonogramVazby.Count > 0 || createPayload.ManualActualKroky.Count > 0)` zkrať na `createPayload.HarmonogramVazby.Count > 0`,
- v `ApplyApprovedCreateProposalAuxiliariesAsync` smaž celý blok od komentáře `// ManualActualKroky → UPSERT krok skutečnost datumy (datum-model).` po jeho `await _dbContext.SaveChangesAsync(ct); }` (návrh založení skutečnost nenese),
- v lokální funkci `GetOrCreateKrok` (ř. ~252) nahraď `SkutecnostRezim = (byte)SkutecnostRezimEnum.Auto,` za `SkutecnostRezim = ScheduleActualApplier.VychoziRezim(poradi),`,
- ve schválení návrhu harmonogramu nahraď blok od `var targetRezim = schedulePayload.ChangesHarmonogramRezim` (ř. ~278) po konec cyklu `foreach (var applied in ManualActualKrokApplier.Compute(...)) { ... }` (ř. ~358) tímto; blok `if (schedulePayload.ChangesSchedulePlan) { ... }` z jeho středu přesuň beze změny PŘED něj. Proměnné `nowUtc` (ř. 246), `krokByPoradi` (ř. 250) a funkce `GetOrCreateKrok` (ř. 252) v metodě už jsou:

```csharp
        // Spec 2026-10-14: skutečnost a režim přes stejnou komponentu jako uložení editoru.
        // Režim se mění jen tehdy, když ho návrh opravdu mění (Fix round 1, 2026-10-12).
        var cilovyRezim = schedulePayload.ChangesHarmonogramRezim
            ? HarmonogramAutoFillRule.Normalize(schedulePayload.HarmonogramRezim)
            : null;
        IReadOnlyList<ScheduleActualApplier.SkutecnostZmena> zmeny = schedulePayload.ChangesScheduleActual
            ? schedulePayload.ActualHarmonogramHodnoty
                .Select(av => new ScheduleActualApplier.SkutecnostZmena(av.Poradi, av.SkutecnostDatum))
                .ToList()
            : [];
        var vysledek = ScheduleActualApplier.Apply(
            krokByPoradi.Values.ToList(), GetOrCreateKrok, zmeny, cilovyRezim, nowUtc);
        var switchedToAuto = vysledek.PrepnutoNaAuto;
```

(`return switchedToAuto;` na konci metody zůstává.)

`RecordProposalService.Queries.cs` ř. ~256–280 — nahraď komentář `// Review fix M3 …` i výpočet `autoEligibleKrokyZRucnichDat` s navazujícím `if` tímto. Infobar musí rozhodovat **stejným pravidlem jako applier** (režim celého záznamu), jinak by u smíšeného stavu kroků tvrdil opak toho, co schválení udělá:

```csharp
                // Spec 2026-10-14: návrh beze změny režimu nese změny skutečnosti. Kroky 1/3/4/6/7/10
                // se uplatní jen u záznamu v ručním režimu (ScheduleActualApplier — stejné pravidlo).
                // U záznamu v automatickém režimu to schvalovateli řekneme; reálně jen u návrhů ve
                // starém formátu (převod R4, dřív se takové kroky tiše přepnuly na ruční).
                var zaznamVAuto = HarmonogramAutoFillRule.IsAutoFill(
                    recordKrokRezimByPoradi.Select(kv => (kv.Key, (byte)kv.Value)));
                List<int> autoEligibleKrokyZRucnichDat = zaznamVAuto
                    ? schedulePayload.ActualHarmonogramHodnoty
                        .Where(av => av.SkutecnostDatum.HasValue)
                        .Select(av => av.Poradi)
                        .Where(poradi => !HarmonogramManualSteps.IsManual(poradi))
                        .Distinct()
                        .OrderBy(poradi => poradi)
                        .ToList()
                    : [];

                if (autoEligibleKrokyZRucnichDat.Count > 0)
                {
                    model.ProposalRezimChangeMessage =
                        $"Návrh obsahuje ruční data u kroků {FormatCzechPoradiList(autoEligibleKrokyZRucnichDat)}, " +
                        "které jsou na záznamu vyplňované automaticky. Po schválení se neuplatní.";
                }
```

Testy `StaryNavrhRucniData…` v `ScheduleProposalRezimTests` (hlídají „3“ a „vyplňované automaticky“, resp. žádný infobar u kroků 2/5/8/9) projdou beze změny.

`PendingScheduleProposalLockEvaluator.cs` ~ř. 84–90 — `schedulePayload.ManualActualKroky` → `schedulePayload.ActualHarmonogramHodnoty`.

- [ ] **Step 6: Uprav integrační testy návrhů**

`ScheduleProposalRezimTests.cs` test 1 (`NavrhAutoNaRucne_…`, ř. ~49–52) — nahraď `command.ManualActualKroky = [...]` za:

```csharp
        command.HarmonogramHodnoty =
        [
            new SaveRecordHarmonogramValueCommand { Poradi = 3, SkutecnostDatum = manualDatum }
        ];
```

Test 2 (`NavrhRucneNaAuto_…`, ř. ~116–119) — stejná náhrada se `skutecnostB` a `SkutecnostDatumPuvodni = skutecnostA`; poslední assert změň (R2):

```csharp
        krok3.SkutecnostDatum.Should().BeNull(
            "ruční datum B z návrhu se v Auto režimu neuplatní a ruční datum A se přepnutím na automatiku smaže (R2)");
```

`RecordProposalDataStoreTests.cs:152-155` — `scheduleProposalCommand.ManualActualKroky = [...]` nahraď:

```csharp
        scheduleProposalCommand.HarmonogramHodnoty =
        [
            new SaveRecordHarmonogramValueCommand
            {
                Poradi = 2,
                SkutecnostDatum = originalDeadline.AddDays(-15),
                SkutecnostDatumPuvodni = originalSkutecnost
            }
        ];
```

Testy se starým JSON (`StaryNavrh…`, `ScheduleProposalReasonTests`, Api `ScheduleProposalRezimRenderTests`) se **nemění** — ověřují převod R4.

- [ ] **Step 7: Spusť testy, musí projít**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~RecordProposalPayload" --nologo
dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~Proposal" --nologo
dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~Proposal|FullyQualifiedName~Navrh" --nologo
```
Expected: zelené (kromě selhání zapsaných v Task 0).

- [ ] **Step 8: Commit**

```bash
git add PmTracker.Web/Services PmTracker.Web/Models/ViewModels/RecordProposalViewModels.cs PmTracker.Tests.Unit/Records PmTracker.Tests.Integration/DataStore
git commit -m "fix(navrhy): skutečnost v návrhu jako seznam změn přes ScheduleActualApplier; převod starého JSON"
```

---

### Task 5: Úklid mrtvého kódu a pojistky (D5, D6, R3, R5)

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Commands/RecordCommands.cs` (smazat `ManualActualKroky`, opravit komentář `HarmonogramRezim`)
- Modify: `PmTracker.Web/Models/ViewModels/RecordProposalViewModels.cs` (smazat `ManualActualKrokDtoExtensions`, `ManualActualKrokDto`; komentář `HarmonogramManualSteps`)
- Delete: `PmTracker.Web/Services/Records/ManualActualKrokApplier.cs`
- Modify: `PmTracker.Web/Services/Records/ManualProposalFieldValidator.cs` (smazat `ValidateManualActualKroky`, `ValidateManualKrokKeysAreManualOnly`, `ValidateAutoStepNotInProposal`)
- Modify: `PmTracker.Web/wwwroot/js/modules/harmonogram/manual-zdroj-toggle.js`, `bootstrap.js`, `rezim-master-switch.js`, `manualKroky.js`
- Modify: `PmTracker.Web/Controllers/HarmonogramController.cs` (`SelectCandidate`)
- Create: `PmTracker.Tests.Unit/Architecture/ScheduleActualSingleListTests.cs`
- Modify: `PmTracker.Tests.Unit/Architecture/HarmonogramPhantomUiFixesTests.cs` (`RecordService_SaveRecord_PersistujeManualActualKroky`, `ManualProposalFieldValidator_ValidateAutoStepNotInProposal_Existuje`)
- Test: `PmTracker.Tests.Api/Controllers/ScheduleEditorContractTests.cs` (přidat test `SelectCandidate`)

- [ ] **Step 1: Napiš padající testy**

`PmTracker.Tests.Unit/Architecture/ScheduleActualSingleListTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>Spec 2026-10-14 — pojistky proti návratu druhého seznamu skutečnosti a mrtvého kódu.</summary>
public sealed class ScheduleActualSingleListTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    [Fact]
    public void WebNepouzivaManualActualKroky_KromePrevoduStarehoJson()
    {
        var web = Path.Combine(Root(), "PmTracker.Web");
        var vyskyty = Directory.EnumerateFiles(web, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs") || f.EndsWith(".cshtml") || f.EndsWith(".js"))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}"))
            .Where(f => !f.EndsWith("RecordProposalPayloadJson.cs"))
            .Where(f => File.ReadAllText(f).Contains("ManualActualKrok"))
            .Select(f => Path.GetRelativePath(web, f))
            .ToList();

        vyskyty.Should().BeEmpty("druhý seznam s vlastním čítačem způsoboval díru v číslování (D1)");
    }

    /// <summary>Review Focus 2 — bootstrap.js z mezipaměti prohlížeče modul importuje.</summary>
    [Fact]
    public void ManualZdrojToggle_ZustavaPrazdnymModulem()
    {
        var path = Path.Combine(Root(), "PmTracker.Web/wwwroot/js/modules/harmonogram/manual-zdroj-toggle.js");
        File.Exists(path).Should().BeTrue("chybějící modul by shodil celý ESM graf u uživatelů se starým bootstrap.js");
        File.ReadAllText(path).Should().NotContain("addEventListener", "modul nesmí nic dělat (D5 mrtvý kód)");
        File.ReadAllText(Path.Combine(Root(), "PmTracker.Web/wwwroot/js/modules/bootstrap.js"))
            .Should().NotContain("manual-zdroj-toggle", "nová verze ho už neimportuje");
    }
}
```

V `ScheduleEditorContractTests` přidej:

```csharp
    /// <summary>R3 — kroky 2/5/8/9 automat nevyplňuje, výběr kandidáta pro ně nemá smysl.</summary>
    [Fact]
    public async Task SelectCandidate_OdmitneRucniKrok()
    {
        var z = await ZalozUkolAsync("APICONTR4");
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.SendAsync(ApiTestHttpHelper.BuildAjaxPost(
            $"/Harmonogram/SelectCandidate?asUser={_fixture.AdminOsobaId}",
            new StringContent($"{{\"zaznamId\":{z.RecordId},\"poradi\":5,\"externiOdkazId\":null}}",
                System.Text.Encoding.UTF8, "application/json")));

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        (await KrokAsync(z.RecordId, 5)).SkutecnostRezim.Should().Be((byte)SkutecnostRezimEnum.Auto,
            "řádek kroku 5 se nesmí dotknout (založený seedem jako Auto)");
    }
```

V `HarmonogramPhantomUiFixesTests`:
- `RecordService_SaveRecord_PersistujeManualActualKroky` přejmenuj na `RecordService_SaveRecord_PersistujeSkutecnostPresApplier` a asserty nahraď: `src.Should().Contain("ScheduleActualApplier.Apply", "Spec 2026-10-14: skutečnost se zapisuje jedinou komponentou.");` a `src.Should().NotContain("ManualActualKroky");`,
- `ManualProposalFieldValidator_ValidateAutoStepNotInProposal_Existuje` smaž (validátor zrušen; kroky 1/3/4/6/7/10 v automatickém režimu vynechává mapper — test `BuildSchedulePayload_CilAuto_VynechaZmenyAutoKroku`).

- [ ] **Step 2: Spusť testy, musí selhat**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ScheduleActualSingleListTests" --nologo`
Expected: FAIL — výskyty `ManualActualKrok` v `RecordCommands.cs`, `RecordProposalViewModels.cs`, `ManualActualKrokApplier.cs`, `ManualProposalFieldValidator.cs`, JS; bootstrap importuje toggle.

- [ ] **Step 3: Smaž mrtvý kód**

- `RecordCommands.cs`: smaž vlastnost `ManualActualKroky` s komentářem; v komentáři `HarmonogramRezim` nahraď větu o `ManualActualKroky` a „serverside spustí sync“ textem: „V ručním režimu jdou změny skutečnosti kroků 1/3/4/6/7/10 v HarmonogramHodnoty; v automatickém se ignorují a přepnutí na automatiku smaže jejich data, která nedodal automat (spec 2026-10-14 R2).“
- `RecordProposalViewModels.cs`: smaž `ManualActualKrokDtoExtensions` a `ManualActualKrokDto`; v komentáři `HarmonogramManualSteps` větu „Skutečnost u těchto kroků se zadává ručně (ManualActualKrokDto).“ nahraď „Skutečnost těchto kroků zadává vždy uživatel — automat ani přepínač režimu je nemění.“
- `git rm PmTracker.Web/Services/Records/ManualActualKrokApplier.cs`
- `ManualProposalFieldValidator.cs`: smaž metody `ValidateManualActualKroky`, `ValidateManualKrokKeysAreManualOnly`, `ValidateAutoStepNotInProposal` (zůstává `ValidateHarmonogramVazby`).

- [ ] **Step 4: JS — vyprázdnit toggle, odpojit import, opravit komentáře**

`manual-zdroj-toggle.js` — nahraď celý obsah:

```js
/**
 * Spec 2026-10-14 (R5): volba „Ručně / Z vyjádření“ u kroků 2/5/8/9 byla mrtvý kód — automat
 * tyto kroky nevyplňuje (HarmonogramKrokDatumMapping je nemapuje), takže se nikdy nevykreslila.
 *
 * Soubor zůstává PRÁZDNÝ jen proto, že bootstrap.js z předchozí verze v mezipaměti prohlížeče
 * ho importuje a chybějící modul by shodil celý ESM graf (memory project_bundle_sync).
 * Smazat ve verzi po příští.
 */
```

`bootstrap.js` — smaž řádek `import "./harmonogram/manual-zdroj-toggle.js"; // FIX 2026-05-04 — chevron toggle 2/5/8/9 (Ručně ⇄ Z vyjádření).`

`rezim-master-switch.js` — v hlavičkovém komentáři nahraď odstavec „Žádný server call. … re-fill ze ServiceDesk vyjádření.“ textem:
```
 * Žádný server call. Režim i data jdou až při uložení v jednom POST /Zaznamy/Save.
 * Server (ScheduleActualApplier, spec 2026-10-14) při přepnutí na Auto smaže ruční data
 * kroků 1/3/4/6/7/10 a naplánuje stažení ze ServiceDesku; při přepnutí na ruční nechá
 * hodnoty i zdroje beze změny (vstupy jsou předvyplněné aktuální hodnotou).
```

`manualKroky.js` — v hlavičce řádky o `ManualActualKroky[i].KrokKey` / `AbsolutniDatum` nahraď: ` *  - Vedle něj je skryté pole name="HarmonogramHodnoty[i].SkutecnostDatum" (pm-date-field)` a řádek o `ManualActualKrokApplier cascade` nahraď ` *    přes gov-message (inline), ale submit se nezamyká — chronologii hlídá server`.

- [ ] **Step 5: `SelectCandidate` odmítne kroky 2/5/8/9 (R3)**

`HarmonogramController.cs` — v `SelectCandidate` hned za kontrolu `request is null || ...` přidej:

```csharp
        if (HarmonogramManualSteps.IsManual(request.Poradi))
        {
            // R3 (spec 2026-10-14): kroky 2/5/8/9 automat nevyplňuje, kandidát pro ně nemá smysl
            // a založený řádek by vznikl v režimu Auto.
            return BadRequest(new { error = $"Krok {request.Poradi} je ruční — automat ho nevyplňuje." });
        }
```
(`using PmTracker.Web.Models.ViewModels;` controller už má).

- [ ] **Step 6: Spusť testy, musí projít**

```bash
dotnet build PmTracker.sln -v q --nologo
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --nologo
dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~ScheduleEditorContractTests" --nologo
```
Expected: build bez chyb a varování z dotčených souborů; Unit zelené; kontraktové testy zelené.

- [ ] **Step 7: Commit**

```bash
git add -A PmTracker.Web PmTracker.Tests.Unit PmTracker.Tests.Api/Controllers/ScheduleEditorContractTests.cs
git commit -m "refactor(harmonogram): odstranění ManualActualKroky a mrtvé volby zdroje u kroků 2/5/8/9 (D5, D6, R3)"
```

---

### Task 6: Dokumentace, plné sady, ověření v prohlížeči, sloučení

**Files:**
- Modify: `docs/wiki/projekty/harmonogram/manualni-skutecnost.md` (sekce „Přepnutí na režim Ručně“, „Manuální zadání hodnoty“, „Dropdown alternativních kandidátů“, „Vrátit zpět na Auto“)
- Modify: `docs/wiki/projekty/navrhy/navrhnout-zmenu-planu.md` (odstavec o režimu ~ř. 82–84)
- Modify: `docs/wiki/integrace/servicedesk/auto-fill-skutecnosti.md` (~ř. 137, 199–206)
- Create: `docs/changelog/releases/0.11.md`; regenerovat `CHANGELOG.md`
- Modify: `docs/superpowers/specs/2026-10-14-harmonogram-skutecnost-jeden-seznam-design.md` (stav)

- [ ] **Step 1: Wiki**

`manualni-skutecnost.md` — přepiš uvedené sekce tak, aby říkaly přesně toto:
- Režim se přepíná **jedním přepínačem** „Automatické vyplňování harmonogramu“ pro celý záznam (per-krok tlačítko neexistuje).
- **Vypnutí:** kroky 1, 3, 4, 6, 7 a 10 zůstanou se stávajícími daty a jdou upravit; nic se nesmaže.
- **Zapnutí:** ručně zadaná data kroků 1, 3, 4, 6, 7 a 10 se uložením smažou a automat je doplní ze ServiceDesku; historická data zůstanou.
- Kroky **2, 5, 8 a 9 jsou vždy ruční**; přepínač je nemění, automat je nevyplňuje.
- Uložené ruční datum jde kdykoli změnit i smazat (křížek u pole).
- Dropdown kandidátů platí jen pro kroky 1, 3, 4, 6, 7 a 10 v automatickém režimu.

`navrhnout-zmenu-planu.md` — doplň větu: „Návrh nese jen kroky, které jste změnili. Když návrh přepíná na automatické vyplňování, ruční data kroků 1, 3, 4, 6, 7 a 10 se po schválení smažou.“

`auto-fill-skutecnosti.md` ř. ~199–206 — nahraď popis „přepnout na Ručně a vybrat preferred kandidáta“ textem odpovídajícímu master přepínači a dropdownu kandidátů v automatickém režimu.

- [ ] **Step 2: Release notes**

`docs/changelog/releases/0.11.md`:

```markdown
## 0.11 - 2026-10-14

### Opraveno
- **Ruční skutečnost harmonogramu se už neztrácí.** Datum zadané ručně u kroků 1, 3, 4, 6, 7 a 10 šlo dřív uložit jen jednou — změna ani smazání se pak tiše neuložily a navíc mohly strhnout ruční data dalších kroků. Teď jde datum měnit i mazat kdykoli.
- **Vypnutí automatického vyplňování už nemaže data ze ServiceDesku.** Uložení po vypnutí přepínače dřív vyprázdnilo kroky, které vyplnil automat. Teď zůstanou a jdou upravit.
- **Zapnutí automatického vyplňování dělá, co slibuje:** ručně zadaná i historická data kroků 1, 3, 4, 6, 7 a 10 se smažou a automat je hned doplní ze ServiceDesku. Buď vyplňuje uživatel, nebo automat — nic mezi. Kroky 2, 5, 8 a 9 zůstávají vždy ruční.
- Návrh změny harmonogramu nese jen kroky, které autor opravdu změnil.
```

```bash
bash scripts/generate-changelog.sh
```

- [ ] **Step 3: Plné sady**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --nologo 2>&1 | grep -E '^  Failed |^Failed!|^Passed!'
dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --nologo 2>&1 | grep -E '^  Failed |^Failed!|^Passed!'
dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --nologo 2>&1 | grep -E '^  Failed |^Failed!|^Passed!'
```
Expected: jen selhání zapsaná v Task 0. E2E harmonogramu/editoru: `dotnet test PmTracker.Tests.E2E/PmTracker.Tests.E2E.csproj --filter "FullyQualifiedName~Harmonogram|FullyQualifiedName~Schedule|FullyQualifiedName~RecordEdit" --nologo` — jména selhání porovnat s baseline (memory `project_e2e_baseline_failures`).

- [ ] **Step 4: Ověření v prohlížeči** (lokální DB, vlastní instance na portu 5072 z worktree, Playwright skripty ve scratchpadu — memory `feedback_local_dev_db_upgrade_self`)

Projdi znovu scénáře, které 2026-10-13/14 selhaly, na testovacích záznamech ZTST1 (id 3):
1. T2: krok 3 a 7 ručně → uložit → znovu otevřít → změnit krok 7 → uložit → smazat krok 3 → uložit. Očekávání: DB odpovídá po každém kroku.
2. Díra: záznam s ručním krokem 3 → vyplnit krok 4 a 5 → uložit. Očekávání: oba uložené.
3. D3: záznam s kroky 1 a 3 od automatu → vypnout automatiku → uložit. Očekávání: data i zdroj 🤖 zůstanou.
4. R2: záznam v ručním režimu s ručním krokem 3 a ručním krokem 5 → zapnout automatiku → uložit. Očekávání: krok 3 prázdný (nebo s datem automatu 🤖), krok 5 beze změny.
5. Editor návrhu: totéž co bod 1 přes „Navrhnout změnu harmonogramu“ → schválit. Očekávání: změny uplatněné.

- [ ] **Step 5: Spec do stavu „implementováno“, rebase, sloučení do `main`**

V hlavičce specu změň `**Stav:**` na `implementováno 2026-10-14`. Pak:

```bash
git add docs CHANGELOG.md
git commit -m "docs(harmonogram): ruční skutečnost a přepínač režimu po sloučení do jednoho seznamu"
git fetch . main:main 2>/dev/null; git rebase main
dotnet build PmTracker.sln -v q --nologo && dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --nologo 2>&1 | grep -E '^Failed!|^Passed!'
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
git merge --ff-only fix/harmonogram-skutecnost-jeden-seznam
git worktree remove "../PM Tracker-wt-skutecnost"
```
Push jen na pokyn uživatele.
