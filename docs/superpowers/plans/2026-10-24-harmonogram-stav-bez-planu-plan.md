# Harmonogram: stav bez plánu a živý přepočet editoru — implementační plán

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Když aktuální krok harmonogramu nemá plán, stav se počítá k celkovému termínu záznamu a říká to textem; plán dokončení bez jediného plánu je „není zadán“; Rozpad u kroku bez plánu píše „bez plánu“; editor se po výběru data i po ✕ hned přepočítá a kalendář hlídá pořadí plánů stejně jako server.

**Architecture:** Rozhodnutí „vůči čemu se skluz počítá“ patří do výpočtu `ScheduleDateCalculator.Summarize` (nový výčet `HarmonogramStavVztah`); texty skládá jen `ScheduleStatusText`. Editor má JS zrcadlo obojího (`schedule/scheduleStatus.js`); parita C# ↔ JS je zamčená jednou sadou případů `status-cases.json`, kterou čte xUnit i node:test (vzor `axis-cases.json`). Živý přepočet = jeden delegovaný posluchač `change` na bloku harmonogramu; meze kalendáře = čistá funkce `planBounds` (zrcadlo `ScheduleChronologyValidator`).

**Tech Stack:** ASP.NET Core 8 MVC/Razor, xUnit + FluentAssertions (Unit, Api s `ApiSqlFixture`), `node --test`, Playwright .NET (E2E).

**Spec:** `docs/superpowers/specs/2026-10-24-harmonogram-stav-bez-planu-design.md` (R1–R11)

## Global Constraints

- `$WS` v příkazech = workspace plánu (`../subagent-driven-development/scripts/sdd-workspace <plán>`); dlouhé výstupy testů do něj, číst jen konec.
- Texty v UI česky přesně podle spec R3, R4, R6, R7, R8 (oddělovač „ · “ = mezera, U+00B7, mezera; pomlčka „–“ = U+2013 jen pro trvání bez plánu; záporná odchylka kroku má spojovník „-“ jako dnes).
- Skloňování ve stavu: 1 → „den“, 2–4 → „dny“, jinak „dní“; „zbývá 1 den“, „zbývají 2–4 dny“, „zbývá N dní“. Trvání a odchylka kroku: 1 → „den“, 2–4 → „dny“, jinak „dnů“ (`ScheduleDurationText.Format`).
- Barvy jen stávajícími třídami (`ok`/`late`, `late`/`ahead`); žádné nové barvy (gov tokeny, `AppCssColorTokenTests`).
- Žádná změna schématu DB. Dashboard (`ProjectDashboardService`, `PriorityMatrixRebuildService`) se nemění.
- Text „Aktuální krok:“ smí být jen v `ScheduleStatusText.cs` a `scheduleStatus.js` (ani v komentářích `_ScheduleBlock.cshtml` / `block.js` — hlídá to zdrojový test).
- Práce inline bez subagentů; větev `feat/harmonogram-stav-bez-planu`; hotové a otestované mergnout do lokálního `main`, nepushovat; doplnit `docs/k-otestovani.md`; ověřit v prohlížeči za všechny role.

## Review Focus

1. **Přechod letního/zimního času** (CZ 25.10.2026 a 29.3.2026): JS počítá rozdíl dnů z lokálních půlnocí (`diffCalendarDays`, zaokrouhlení), C# z `DateTime.Date`. Případy `bez-planu-zbyva-1-den` (24.→25.10.) a `aktualni-bez-planu-po-kroku-s-planem` (1.3.→30.6.) v `status-cases.json` přechod překračují — oba musí sedět na obou stranách.
2. **Detail návrhu se smazáním plánu aktuálního kroku:** server tam vykreslí souhrn současného záznamu, JS ho po načtení přepočítá z navržených hodnot → musí ukázat „plán není zadán · celkový termín…“. Ověřit v průchodu (Task 6).
3. **Změna termínu v editoru** (`TerminUkonceni`): u stavu k celkovému termínu se text musí přepočítat živě (posluchač `deadlineInput` už existuje) — ověřit v průchodu.
4. **Zamčený editor / role bez práva měnit plán:** meze (`min`/`max`) se nastaví i na zamčená pole — nesmí to nic rozbít ani odemknout; delegovaný posluchač nesmí reagovat na jiná pole (`name` končí `.PlanDatum`/`.SkutecnostDatum`).
5. **Záznam se starým plánem mimo pořadí:** meze se mohou překřížit (min > max) a kalendář kroku vypne všechny dny — přijatelné (server takový záznam neuloží ani dnes), ale nesmí spadnout JS. Ověřit ručně v průchodu na záznamu s přehozenými plány.

---

### Task 1: Výpočet — vůči čemu se počítá stav, plánové dokončení bez plánu

**Files:**
- Create: `PmTracker.Web/Services/Schedules/HarmonogramStavVztah.cs`
- Modify: `PmTracker.Web/Services/Schedules/ScheduleDateCalculator.cs:20-26` (record `ScheduleDateSummary`), `:96-111` (`Summarize`)
- Test: `PmTracker.Tests.Unit/Schedule/ScheduleDateCalculatorTests.cs`

**Interfaces:**
- Produces: `enum HarmonogramStavVztah { AktualniKrok, CelkovyTermin, Dokonceno }` (pořadí členů: `AktualniKrok` = 0 kvůli výchozí hodnotě view modelu); `ScheduleDateSummary(DateTime? PlanoveDokonceni, DateTime Termin, int? AktualniKrokPoradi, int PrekroceniDni, HarmonogramStavVztah Vztah)` s odvozeným `bool Dokonceno`.

- [ ] **Step 1: Napsat padající testy** — na konec třídy `ScheduleDateCalculatorTests` (před poslední `}`):

```csharp
    // --- Spec 2026-10-24: krok bez plánu (vztah stavu R2, plánové dokončení R6) ---

    [Fact]
    public void Summarize_aktualni_krok_bez_planu_pocita_skluz_k_terminu()
    {
        // Bez plánu, skutečnost kroků 1/3/4 (automat z externí vazby) → aktuální krok 5 bez plánu.
        var steps = new[]
        {
            Krok(1, null, new DateTime(2026, 1, 3)),
            Krok(3, null, new DateTime(2026, 1, 20)),
            Krok(4, null, new DateTime(2026, 2, 10)),
            Krok(5, null, null),
        };

        var s = ScheduleDateCalculator.Summarize(Start, steps, termin: new DateTime(2026, 3, 13), today: new DateTime(2026, 3, 1));

        s.AktualniKrokPoradi.Should().Be(5);
        s.Vztah.Should().Be(HarmonogramStavVztah.CelkovyTermin);
        s.PrekroceniDni.Should().Be(-12, "dnes − termín; dopočtený konec plánu (datum založení) by dal +59");
        s.Dokonceno.Should().BeFalse();
    }

    [Fact]
    public void Summarize_aktualni_krok_bez_planu_po_kroku_s_planem_pocita_k_terminu()
    {
        var steps = new[]
        {
            Krok(1, new DateTime(2026, 1, 20), new DateTime(2026, 1, 22)),
            Krok(2, null, null),
            Krok(3, new DateTime(2026, 4, 1), null),
        };

        var s = ScheduleDateCalculator.Summarize(Start, steps, termin: new DateTime(2026, 6, 30), today: new DateTime(2026, 3, 1));

        s.AktualniKrokPoradi.Should().Be(2);
        s.Vztah.Should().Be(HarmonogramStavVztah.CelkovyTermin, "ne k dopočtenému plánu kroku 1");
        s.PrekroceniDni.Should().Be(-121);
    }

    [Fact]
    public void Summarize_aktualni_krok_s_planem_pocita_k_planu_i_kdyz_drivejsi_kroky_plan_nemaji()
    {
        var steps = new[]
        {
            Krok(1, null, new DateTime(2026, 1, 3)),
            Krok(2, new DateTime(2026, 2, 20), null),
        };

        var s = ScheduleDateCalculator.Summarize(Start, steps, termin: new DateTime(2026, 6, 30), today: new DateTime(2026, 3, 1));

        s.Vztah.Should().Be(HarmonogramStavVztah.AktualniKrok);
        s.PrekroceniDni.Should().Be(9);
    }

    [Fact]
    public void Summarize_bez_jedineho_planu_nema_planove_dokonceni()
    {
        var steps = new[] { Krok(1, null, new DateTime(2026, 1, 3)), Krok(2, null, null) };

        var s = ScheduleDateCalculator.Summarize(Start, steps, termin: new DateTime(2026, 6, 30), today: new DateTime(2026, 3, 1));

        s.PlanoveDokonceni.Should().BeNull("dopočtené datum založení není plán");
    }

    [Fact]
    public void Summarize_vsechny_vyplnene_bez_planu_je_dokonceno_bez_planoveho_dokonceni()
    {
        var steps = Enumerable.Range(1, 10).Select(i => Krok(i, null, new DateTime(2026, 1, 1).AddDays(i))).ToList();

        var s = ScheduleDateCalculator.Summarize(Start, steps, termin: new DateTime(2026, 6, 30), today: new DateTime(2026, 3, 1));

        s.Vztah.Should().Be(HarmonogramStavVztah.Dokonceno);
        s.Dokonceno.Should().BeTrue();
        s.PrekroceniDni.Should().Be(0);
        s.PlanoveDokonceni.Should().BeNull();
    }
```

- [ ] **Step 2: Spustit — musí selhat**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ScheduleDateCalculatorTests" > $WS/t1.log 2>&1; tail -15 $WS/t1.log`
Expected: build error `CS0246: The type or namespace name 'HarmonogramStavVztah' could not be found` (a `'ScheduleDateSummary' does not contain a definition for 'Vztah'`).

- [ ] **Step 3: Implementace** — nový soubor `HarmonogramStavVztah.cs`:

```csharp
namespace PmTracker.Web.Services.Schedules;

/// <summary>
/// Spec 2026-10-24 R2: vůči čemu se počítá sjednocený stav harmonogramu (skluz / předstih).
/// Pořadí členů: <see cref="AktualniKrok"/> je výchozí hodnota (view model bez souhrnu).
/// </summary>
public enum HarmonogramStavVztah
{
    /// <summary>Aktuální krok má vlastní plán: překročení = dnes − plán aktuálního kroku.</summary>
    AktualniKrok,

    /// <summary>Aktuální krok plán nemá: překročení = dnes − celkový termín záznamu.</summary>
    CelkovyTermin,

    /// <summary>Všechny kroky mají skutečnost, aktuální krok není.</summary>
    Dokonceno
}
```

V `ScheduleDateCalculator.cs` nahradit record `ScheduleDateSummary` (ř. 20–26):

```csharp
/// <summary>Souhrn harmonogramu (datum-model, sjednocený stav). Viz spec harmonogram-plan-vs-skutecnost a 2026-10-24.</summary>
public sealed record ScheduleDateSummary(
    DateTime? PlanoveDokonceni,  // null = žádný krok nemá plán (spec 2026-10-24 R6)
    DateTime Termin,
    int? AktualniKrokPoradi,     // null = vše vyplněno (Dokončeno)
    int PrekroceniDni,           // dnes − plán(aktuální krok), resp. dnes − termín (Vztah = CelkovyTermin); 0 když Dokončeno
    HarmonogramStavVztah Vztah)
{
    public bool Dokonceno => Vztah == HarmonogramStavVztah.Dokonceno;
}
```

a metodu `Summarize` (ř. 96–111):

```csharp
    public static ScheduleDateSummary Summarize(
        DateTime start, IReadOnlyList<ScheduleDateStep> steps, DateTime termin, DateTime today)
    {
        var ordered = steps.OrderBy(x => x.Poradi).ToList();
        var normTermin = termin.Date;
        var computed = Compute(start, ordered, today);

        // Spec 2026-10-24 R6: plánové dokončení existuje, jen když aspoň jeden krok má vlastní plán —
        // jinak by to bylo dopočtené datum založení (konec plánu kroku bez plánu = jeho začátek).
        DateTime? planoveDokonceni = computed.Count > 0 && ordered.Any(x => x.PlanDatum.HasValue)
            ? computed[^1].PlanEnd
            : null;
        var aktualni = computed.FirstOrDefault(x => x.JeAktualniKrok);

        if (aktualni is null)   // vše vyplněno → Dokončeno
            return new ScheduleDateSummary(planoveDokonceni, normTermin, null, 0, HarmonogramStavVztah.Dokonceno);

        // Spec 2026-10-24 R2: bez vlastního plánu aktuálního kroku se skluz počítá k celkovému termínu.
        // Dopočtený konec plánu (konec předchozího plánu, nebo datum založení) by dával nesmysl.
        var maVlastniPlan = ordered.First(x => x.Poradi == aktualni.Poradi).PlanDatum.HasValue;
        return maVlastniPlan
            ? new ScheduleDateSummary(planoveDokonceni, normTermin, aktualni.Poradi,
                (today.Date - aktualni.PlanEnd.Date).Days, HarmonogramStavVztah.AktualniKrok)
            : new ScheduleDateSummary(planoveDokonceni, normTermin, aktualni.Poradi,
                (today.Date - normTermin).Days, HarmonogramStavVztah.CelkovyTermin);
    }
```

`HarmonogramDateBlokBuilder.BuildSouhrn` se v tomto kroku jen přizpůsobí, aby se přeložil (plné mapování je Task 3) — ř. 129 a 131:

```csharp
            BaselineDokonceni = s.PlanoveDokonceni ?? start.Date,
            TerminUkolu = s.Termin,
            CelkoveTrvaniDni = s.PlanoveDokonceni.HasValue ? Math.Max(0, (s.PlanoveDokonceni.Value - start.Date).Days) : 0,
```

- [ ] **Step 4: Spustit — musí projít**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ScheduleDateCalculatorTests|FullyQualifiedName~HarmonogramDateBlokBuilderTests" > $WS/t1.log 2>&1; tail -5 $WS/t1.log`
Expected: `Passed!` — všechny testy obou tříd (staré beze změny: `Summarize_planove_dokonceni_je_plan_posledniho_kroku` dál vrací 25.01.2026).

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Schedules/HarmonogramStavVztah.cs PmTracker.Web/Services/Schedules/ScheduleDateCalculator.cs PmTracker.Web/Services/Schedules/HarmonogramDateBlokBuilder.cs PmTracker.Tests.Unit/Schedule/ScheduleDateCalculatorTests.cs
git commit -m "feat(harmonogram): stav aktuálního kroku bez plánu se počítá k celkovému termínu (spec 2026-10-24 R2, R6)"
```

---

### Task 2: Texty souhrnu — `ScheduleStatusText`

**Files:**
- Create: `PmTracker.Web/Services/Schedules/ScheduleStatusText.cs`
- Test: `PmTracker.Tests.Unit/Schedule/ScheduleStatusTextTests.cs`

**Interfaces:**
- Consumes: `HarmonogramStavVztah` (Task 1), `ScheduleDurationText.Format(int)`, `ScheduleDurationText.NoPlan`.
- Produces: `ScheduleStatusText.State(HarmonogramStavVztah vztah, string? krokNazev, int prekroceniDni) → string`, `PlanCompletion(DateTime? planoveDokonceni) → string`, `TotalDuration(DateTime? planoveDokonceni, int celkoveTrvaniDni) → string`, `StepOffset(DateTime? planDatum, int odchylkaDni) → string`, `Dni(int pocet) → string`; konstanty `Dokonceno = "Dokončeno"`, `PlanNeniZadan = "není zadán"`, `KrokBezPlanu = "bez plánu"`.

- [ ] **Step 1: Napsat padající testy** — `ScheduleStatusTextTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Tests.Unit.Schedule;

/// <summary>Spec 2026-10-24 R3, R4, R6, R7, R8: texty souhrnu harmonogramu a odchylky kroku.</summary>
public sealed class ScheduleStatusTextTests
{
    private const string Krok = "Aktuální krok: 5. vyporadani pripominek";

    [Theory]
    [InlineData(1, " · skluz +1 den")]
    [InlineData(3, " · skluz +3 dny")]
    [InlineData(4, " · skluz +4 dny")]
    [InlineData(5, " · skluz +5 dní")]
    [InlineData(22, " · skluz +22 dní")]
    [InlineData(-1, " · v předstihu 1 den")]
    [InlineData(-2, " · v předstihu 2 dny")]
    [InlineData(-7, " · v předstihu 7 dní")]
    [InlineData(0, " · dle plánu")]
    public void State_k_planu_aktualniho_kroku(int prekroceni, string konec)
        => ScheduleStatusText.State(HarmonogramStavVztah.AktualniKrok, "5. vyporadani pripominek", prekroceni)
            .Should().Be(Krok + konec);

    [Theory]
    [InlineData(-1, "zbývá 1 den")]
    [InlineData(-2, "zbývají 2 dny")]
    [InlineData(-4, "zbývají 4 dny")]
    [InlineData(-12, "zbývá 12 dní")]
    [InlineData(1, "skluz +1 den")]
    [InlineData(2, "skluz +2 dny")]
    [InlineData(10, "skluz +10 dní")]
    public void State_k_celkovemu_terminu(int prekroceni, string termin)
        => ScheduleStatusText.State(HarmonogramStavVztah.CelkovyTermin, "5. vyporadani pripominek", prekroceni)
            .Should().Be($"{Krok} · plán není zadán · celkový termín: {termin}");

    [Fact]
    public void State_k_celkovemu_terminu_v_den_terminu()
        => ScheduleStatusText.State(HarmonogramStavVztah.CelkovyTermin, "5. vyporadani pripominek", 0)
            .Should().Be($"{Krok} · plán není zadán · celkový termín je dnes");

    [Fact]
    public void State_dokonceno()
        => ScheduleStatusText.State(HarmonogramStavVztah.Dokonceno, null, 0).Should().Be("Dokončeno");

    [Fact]
    public void PlanCompletion_bez_planu_neni_zadan()
        => ScheduleStatusText.PlanCompletion(null).Should().Be("není zadán");

    [Fact]
    public void PlanCompletion_s_planem_je_datum()
        => ScheduleStatusText.PlanCompletion(new DateTime(2026, 2, 4)).Should().Be("04.02.2026");

    [Theory]
    [InlineData(1, "1 den")]
    [InlineData(3, "3 dny")]
    [InlineData(304, "304 dnů")]
    [InlineData(0, "0 dnů")]
    public void TotalDuration_s_planem_sklonuje(int dni, string text)
        => ScheduleStatusText.TotalDuration(new DateTime(2026, 2, 4), dni).Should().Be(text);

    [Fact]
    public void TotalDuration_bez_planu_je_pomlcka()
        => ScheduleStatusText.TotalDuration(null, 0).Should().Be("–");

    [Theory]
    [InlineData(1, "+1 den")]
    [InlineData(3, "+3 dny")]
    [InlineData(5, "+5 dnů")]
    [InlineData(-2, "-2 dny")]
    [InlineData(-5, "-5 dnů")]
    [InlineData(0, "0 dnů")]
    public void StepOffset_krok_s_planem_sklonuje(int odchylka, string text)
        => ScheduleStatusText.StepOffset(new DateTime(2026, 2, 4), odchylka).Should().Be(text);

    [Fact]
    public void StepOffset_krok_bez_planu()
        => ScheduleStatusText.StepOffset(null, 37).Should().Be("bez plánu");
}
```

- [ ] **Step 2: Spustit — musí selhat**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ScheduleStatusTextTests" > $WS/t2.log 2>&1; tail -8 $WS/t2.log`
Expected: build error `The name 'ScheduleStatusText' does not exist in the current context`.

- [ ] **Step 3: Implementace** — `ScheduleStatusText.cs`:

```csharp
using System.Globalization;

namespace PmTracker.Web.Services.Schedules;

/// <summary>
/// Spec 2026-10-24: texty souhrnu harmonogramu (stav, plán dokončení, trvání) a odchylky kroku
/// v Rozpadu. Jediné místo, kde se skládají — _ScheduleBlock.cshtml je jen volá. Zrcadlo v JS:
/// schedule/scheduleStatus.js (editor je přepočítává živě); parita je zamčená sdílenými případy
/// tests/js/schedule/__fixtures__/status-cases.json (ScheduleStatusGoldenVectorTests + scheduleStatus.test.js).
/// </summary>
public static class ScheduleStatusText
{
    public const string Dokonceno = "Dokončeno";
    public const string PlanNeniZadan = "není zadán";
    public const string KrokBezPlanu = "bez plánu";

    /// <summary>R4: „1 den“, „2–4 dny“, jinak „N dní“.</summary>
    public static string Dni(int pocet)
        => $"{pocet} {(pocet == 1 ? "den" : pocet is >= 2 and <= 4 ? "dny" : "dní")}";

    /// <summary>R3: sjednocený stav. <paramref name="prekroceniDni"/> = dnes − plán (resp. termín), znaménkově.</summary>
    public static string State(HarmonogramStavVztah vztah, string? krokNazev, int prekroceniDni)
    {
        if (vztah == HarmonogramStavVztah.Dokonceno)
        {
            return Dokonceno;
        }

        var krok = $"Aktuální krok: {krokNazev}";
        if (vztah == HarmonogramStavVztah.CelkovyTermin)
        {
            var termin = prekroceniDni > 0 ? $"celkový termín: skluz +{Dni(prekroceniDni)}"
                : prekroceniDni < 0 ? $"celkový termín: {Zbyva(-prekroceniDni)}"
                : "celkový termín je dnes";
            return $"{krok} · plán není zadán · {termin}";
        }

        return prekroceniDni > 0 ? $"{krok} · skluz +{Dni(prekroceniDni)}"
            : prekroceniDni < 0 ? $"{krok} · v předstihu {Dni(-prekroceniDni)}"
            : $"{krok} · dle plánu";
    }

    /// <summary>R6: „Plán dokončení“ / „Plán:“ — datum, nebo „není zadán“, když žádný krok nemá plán.</summary>
    public static string PlanCompletion(DateTime? planoveDokonceni)
        => planoveDokonceni?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) ?? PlanNeniZadan;

    /// <summary>R6: celkové trvání v editoru — „–“ bez plánu, jinak stejné tvary jako trvání kroku.</summary>
    public static string TotalDuration(DateTime? planoveDokonceni, int celkoveTrvaniDni)
        => planoveDokonceni.HasValue ? ScheduleDurationText.Format(celkoveTrvaniDni) : ScheduleDurationText.NoPlan;

    /// <summary>R7, R8: odchylka kroku v Rozpadu — „bez plánu“, když krok nemá vlastní plán.</summary>
    public static string StepOffset(DateTime? planDatum, int odchylkaDni)
        => !planDatum.HasValue ? KrokBezPlanu
            : odchylkaDni > 0 ? $"+{ScheduleDurationText.Format(odchylkaDni)}"
            : odchylkaDni < 0 ? $"-{ScheduleDurationText.Format(-odchylkaDni)}"
            : ScheduleDurationText.Format(0);

    private static string Zbyva(int pocet)
        => pocet is >= 2 and <= 4 ? $"zbývají {Dni(pocet)}" : $"zbývá {Dni(pocet)}";
}
```

- [ ] **Step 4: Spustit — musí projít**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ScheduleStatusTextTests" > $WS/t2.log 2>&1; tail -5 $WS/t2.log`
Expected: `Passed!  - Failed: 0, Passed: 32`.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Schedules/ScheduleStatusText.cs PmTracker.Tests.Unit/Schedule/ScheduleStatusTextTests.cs
git commit -m "feat(harmonogram): texty souhrnu a odchylky kroku v jednom místě, se skloňováním (spec 2026-10-24 R3, R4, R6–R8)"
```

---

### Task 3: Souhrn v šablonách — view model, `_ScheduleBlock`, render testy

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs:268-288` (`HarmonogramSouhrnViewModel`)
- Modify: `PmTracker.Web/Services/Schedules/HarmonogramDateBlokBuilder.cs:127-136` (`BuildSouhrn`)
- Modify: `PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml:1-2, 63-77, 169-179, 211-215`
- Test: `PmTracker.Tests.Unit/Schedule/HarmonogramDateBlokBuilderTests.cs`, `PmTracker.Tests.Unit/Architecture/ScheduleStatusTextSourceTests.cs` (nový), `PmTracker.Tests.Api/Controllers/ScheduleStatusRenderTests.cs` (nový)

**Interfaces:**
- Consumes: `ScheduleDateSummary.Vztah`, `.PlanoveDokonceni` (Task 1); `ScheduleStatusText.*` (Task 2).
- Produces: `HarmonogramSouhrnViewModel.BaselineDokonceni : DateTime?`, `.StavVztah : HarmonogramStavVztah`, `.Dokonceno` (odvozené, jen get), `.Stihame` (beze změny vzorce). Atributy v HTML beze změny: `data-schedule-summary-state`, `-baseline`, `-duration`, `data-schedule-offset`.

- [ ] **Step 1: Padající unit test builderu** — do `HarmonogramDateBlokBuilderTests` (před poslední `}`):

```csharp
    [Fact]
    public void BuildSouhrn_bez_planu_se_skutecnosti_nema_plan_dokonceni_a_pocita_k_terminu()
    {
        // Spec 2026-10-24: skutečnost kroku 1 z automatu, žádný plán → aktuální krok 2 bez plánu.
        var rows = new[] { Row(1, null, new DateTime(2026, 1, 3)) };

        var souhrn = HarmonogramDateBlokBuilder.BuildSouhrn(
            Start, rows, termin: new DateTime(2026, 2, 13), today: Today);

        souhrn.BaselineDokonceni.Should().BeNull("bez plánu není plán dokončení (dřív datum založení)");
        souhrn.CelkoveTrvaniDni.Should().Be(0);
        souhrn.StavVztah.Should().Be(HarmonogramStavVztah.CelkovyTermin);
        souhrn.AktualniKrokPoradi.Should().Be(2);
        souhrn.PrekroceniDni.Should().Be(-12);
        souhrn.Stihame.Should().BeTrue("do celkového termínu zbývá 12 dní");
    }
```

- [ ] **Step 2: Padající zdrojový test** — `PmTracker.Tests.Unit/Architecture/ScheduleStatusTextSourceTests.cs`:

```csharp
using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Spec 2026-10-24 R11: text stavu harmonogramu skládá jen ScheduleStatusText (C#) a jeho JS
/// zrcadlo schedule/scheduleStatus.js. Kopie textu v šabloně nebo v editoru by se rozešla.
/// </summary>
public sealed class ScheduleStatusTextSourceTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře.");
    }

    [Theory]
    [InlineData("PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml")]
    public void StatusText_is_composed_only_by_the_formatter(string relativePath)
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

        content.Should().NotContain("Aktuální krok:", "text stavu skládá ScheduleStatusText / scheduleStatus.js");
        content.Should().NotContain("skluz +", "text stavu skládá ScheduleStatusText / scheduleStatus.js");
    }
}
```

- [ ] **Step 3: Padající Api render testy** — `PmTracker.Tests.Api/Controllers/ScheduleStatusRenderTests.cs`:

```csharp
using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-10-24: záznam bez plánu se skutečností z automatu — stav se počítá k celkovému
/// termínu a říká to („plán není zadán · celkový termín…“), plán dokončení „není zadán“, trvání „–“,
/// krok bez plánu v Rozpadu „bez plánu“, štítek podle termínu. Krok s plánem dál k plánu, se skloňováním.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ScheduleStatusRenderTests
{
    private const string Krok2 = "2. konzultace terminu s dodavatelem pred vytvorenim zadani";
    private readonly ApiSqlFixture _fixture;

    public ScheduleStatusRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    /// <summary>Úkol založený před 100 dny s termínem za <paramref name="terminZaDni"/> dní a zadanými kroky.</summary>
    private async Task<(int ProjectId, int RecordId, DateTime Zalozeni)> SeedAsync(
        string marker, int terminZaDni, Func<int, DateTime, (DateTime? Plan, DateTime? Skutecnost)> krok)
    {
        var leadId = await _fixture.EnsurePersonAsync($"{marker}Lead");
        var projectId = await _fixture.EnsureProjectAsync(marker);
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}_SUB", leadId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, leadId);
        await _fixture.EnsureSubsystemLeadAsync(projectId, subsystemId, leadId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, leadId, subsystemId, "U", marker);
        var zalozeni = DateTime.Today.AddDays(-100);
        await _fixture.SetRecordFoundingDateAsync(recordId, zalozeni);

        await using var db = _fixture.CreateDbContext();
        await db.ProjektoveZaznamy.Where(x => x.Id == recordId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DatumUkonceni, DateTime.Today.AddDays(terminZaDni)));
        for (var poradi = 1; poradi <= 10; poradi++)
        {
            var (plan, skutecnost) = krok(poradi, zalozeni);
            db.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = recordId,
                Poradi = (byte)poradi,
                PlanDatum = plan,
                SkutecnostDatum = skutecnost,
                SkutecnostRezim = HarmonogramManualSteps.IsManual(poradi)
                    ? (byte)SkutecnostRezimEnum.Manual
                    : (byte)SkutecnostRezimEnum.Auto,
                SkutecnostZdroj = skutecnost.HasValue
                    ? (byte)SkutecnostZdrojEnum.Automat
                    : (byte)SkutecnostZdrojEnum.Neznamo,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
        return (projectId, recordId, zalozeni);
    }

    /// <summary>Uživatelův případ: žádný plán, skutečnost kroků 1/3/4 z automatu → aktuální krok 5.</summary>
    private static (DateTime?, DateTime?) BezPlanuAutomat(int poradi, DateTime zalozeni) => poradi switch
    {
        1 => (null, zalozeni.AddDays(2)),
        3 => (null, zalozeni.AddDays(20)),
        4 => (null, zalozeni.AddDays(60)),
        _ => (null, null)
    };

    private async Task<string> GetHtmlAsync(string url)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return WebUtility.HtmlDecode(html);
    }

    private static string Text(string html, string attribute)
        => Regex.Match(html, $"{attribute}>([^<]*)<").Groups[1].Value.Trim();

    private static (string Color, string Text) Tag(string html)
    {
        var m = Regex.Match(html, "<gov-tag color=\"(\\w+)\" size=\"s\">(Stíháme|Nestíháme)</gov-tag>");
        return (m.Groups[1].Value, m.Groups[2].Value);
    }

    private static (string Css, string Text) Offset(string html, int poradi)
    {
        var m = Regex.Match(html,
            $"<div class=\"gantt-step-row\"[^>]*data-step-index=\"{poradi}\"[^>]*>[\\s\\S]*?<span class=\"gantt-step-offset\\s*([a-z]*)\\s*\" data-schedule-offset>([^<]*)</span>");
        m.Success.Should().BeTrue($"krok {poradi} má mít v Rozpadu odchylku");
        return (m.Groups[1].Value, m.Groups[2].Value.Trim());
    }

    [Fact]
    public async Task RecordPage_WithoutPlan_StateCountsToDeadline_PlanNotSet_StepWithoutPlan()
    {
        var s = await SeedAsync("APSTAV1", terminZaDni: 12, BezPlanuAutomat);

        var html = await GetHtmlAsync($"/Zaznamy/Detail/{s.RecordId}?asUser={_fixture.AdminOsobaId}");

        Text(html, "data-schedule-summary-state").Should()
            .Be("Aktuální krok: 5. vyporadani pripominek · plán není zadán · celkový termín: zbývá 12 dní");
        Text(html, "data-schedule-summary-baseline").Should().Be("není zadán");
        Offset(html, 1).Should().Be((string.Empty, "bez plánu"), "odchylka od dopočteného plánu by byla nesmysl");
        Tag(html).Should().Be(("success", "Stíháme"), "do celkového termínu zbývá 12 dní");
    }

    [Fact]
    public async Task Editor_WithoutPlan_PlanCompletionNotSet_DurationDash()
    {
        var s = await SeedAsync("APSTAV2", terminZaDni: 12, BezPlanuAutomat);

        var html = await GetHtmlAsync($"/Zaznamy/Edit?id={s.RecordId}&asUser={_fixture.AdminOsobaId}");

        Text(html, "data-schedule-summary-state").Should()
            .Be("Aktuální krok: 5. vyporadani pripominek · plán není zadán · celkový termín: zbývá 12 dní");
        Text(html, "data-schedule-summary-baseline").Should().Be("není zadán");
        Text(html, "data-schedule-summary-duration").Should().Be("–");
    }

    [Fact]
    public async Task ScheduleTab_WithoutPlan_PastDeadline_StateAndTagByDeadline()
    {
        var s = await SeedAsync("APSTAV3", terminZaDni: -10, BezPlanuAutomat);

        var html = await GetHtmlAsync($"/Projekty/HarmonogramTabPartial/{s.ProjectId}?asUser={_fixture.AdminOsobaId}");

        Text(html, "data-schedule-summary-state").Should()
            .Be("Aktuální krok: 5. vyporadani pripominek · plán není zadán · celkový termín: skluz +10 dní");
        Tag(html).Should().Be(("error", "Nestíháme"));
    }

    [Fact]
    public async Task RecordPage_CurrentStepWithPlan_CountsToPlan_CzechPlural()
    {
        // Krok 1 plán + skutečnost o den později; krok 2 (aktuální) plán včera.
        var s = await SeedAsync("APSTAV4", terminZaDni: 30, (poradi, zalozeni) => poradi switch
        {
            1 => (zalozeni.AddDays(10), zalozeni.AddDays(11)),
            2 => (DateTime.Today.AddDays(-1), null),
            _ => (null, null)
        });

        var html = await GetHtmlAsync($"/Zaznamy/Detail/{s.RecordId}?asUser={_fixture.AdminOsobaId}");

        Text(html, "data-schedule-summary-state").Should().Be($"Aktuální krok: {Krok2} · skluz +1 den");
        Text(html, "data-schedule-summary-baseline").Should().Be(DateTime.Today.AddDays(-1).ToString("dd.MM.yyyy"));
        Offset(html, 1).Should().Be(("late", "+1 den"));
        Tag(html).Should().Be(("error", "Nestíháme"));
    }
}
```

(Ověř před spuštěním, že `ApiSqlFixture` má `EnsurePersonAsync`, `EnsureProjectAsync`, `EnsureSubsystemAsync`, `EnsureProjectTeamMemberAsync`, `EnsureSubsystemLeadAsync`, `EnsureRecordAsync`, `SetRecordFoundingDateAsync`, `CreateDbContext`, `Factory`, `AdminOsobaId` — používá je `PlanDateClearRenderTests`.)

- [ ] **Step 4: Spustit — musí selhat**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~HarmonogramDateBlokBuilderTests|FullyQualifiedName~ScheduleStatusTextSourceTests" > $WS/t3u.log 2>&1; tail -12 $WS/t3u.log`
Expected: build error `'HarmonogramSouhrnViewModel' does not contain a definition for 'StavVztah'` (builder test). Po dočasném zakomentování nového testu builderu by zdrojový test selhal na `Aktuální krok:` — stačí vidět build error, zdrojový test se ověří v Step 6.

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ScheduleStatusRenderTests" > $WS/t3a.log 2>&1; tail -20 $WS/t3a.log`
Expected: 4 selhání — stav `…· skluz +100 dní` místo `…· plán není zadán · celkový termín: zbývá 12 dní` (dnešní nesmysl: dnes − založení), baseline datum založení místo `není zadán`, `+1 dnů` místo `+1 den`.

- [ ] **Step 5: Implementace**

`HarmonogramSouhrnViewModel` (nahradit celou třídu, ř. 268–288):

```csharp
public sealed class HarmonogramSouhrnViewModel
{
    /// <summary>Plánové dokončení = konec plánu posledního kroku; <c>null</c> = žádný krok nemá plán (spec 2026-10-24 R6).</summary>
    public DateTime? BaselineDokonceni { get; init; }
    public DateTime TerminUkolu { get; init; }

    /// <summary>Dny od založení do plánového dokončení; 0 bez plánu.</summary>
    public int CelkoveTrvaniDni { get; init; }

    /// <summary>Pořadí aktuálního (rozpracovaného) kroku; <c>null</c> = vše vyplněno (Dokončeno).</summary>
    public int? AktualniKrokPoradi { get; init; }

    /// <summary>Název aktuálního kroku pro sjednocený stav v souhrnu.</summary>
    public string? AktualniKrokNazev { get; init; }

    /// <summary>Znaménkové překročení = dnes − plán(aktuální krok), nebo dnes − termín, když aktuální krok
    /// plán nemá (<see cref="StavVztah"/>); 0 když Dokončeno.</summary>
    public int PrekroceniDni { get; init; }

    /// <summary>Vůči čemu se stav počítá (spec 2026-10-24 R2).</summary>
    public HarmonogramStavVztah StavVztah { get; init; }

    public bool Dokonceno => StavVztah == HarmonogramStavVztah.Dokonceno;

    /// <summary>Odvozené pro gov-tag konzumenty (ProjektHarmonogramUkolViewModel.Stihame, _ProjectScheduleTab).</summary>
    public bool Stihame => Dokonceno || PrekroceniDni <= 0;
}
```

`HarmonogramDateBlokBuilder.BuildSouhrn` — návratový objekt (nahradit ř. 127–136):

```csharp
        return new HarmonogramSouhrnViewModel
        {
            BaselineDokonceni = s.PlanoveDokonceni,
            TerminUkolu = s.Termin,
            CelkoveTrvaniDni = s.PlanoveDokonceni.HasValue ? Math.Max(0, (s.PlanoveDokonceni.Value - start.Date).Days) : 0,
            AktualniKrokPoradi = s.AktualniKrokPoradi,
            AktualniKrokNazev = aktualniNazev,
            PrekroceniDni = s.PrekroceniDni,
            StavVztah = s.Vztah,
        };
```

`_ScheduleBlock.cshtml`:
- za ř. 1 (`@model HarmonogramBlockViewModel`) přidat `@using PmTracker.Web.Services.Schedules`;
- ř. 63–77 (souhrn editoru) nahradit:

```razor
        // Spec 2026-10-24: texty souhrnu skládá jen ScheduleStatusText (JS zrcadlo schedule/scheduleStatus.js).
        var stavText = ScheduleStatusText.State(Model.Souhrn.StavVztah, Model.Souhrn.AktualniKrokNazev, Model.Souhrn.PrekroceniDni);
        <div class="schedule-summary-grid">
            <div><span class="label">Termín:</span> <strong data-schedule-summary-deadline>@Model.Souhrn.TerminUkolu.ToString("dd.MM.yyyy")</strong></div>
            <div><span class="label">Plán dokončení:</span> <strong data-schedule-summary-baseline>@ScheduleStatusText.PlanCompletion(Model.Souhrn.BaselineDokonceni)</strong></div>
            <div><span class="label">Trvání:</span> <strong data-schedule-summary-duration>@ScheduleStatusText.TotalDuration(Model.Souhrn.BaselineDokonceni, Model.Souhrn.CelkoveTrvaniDni)</strong></div>
            <div class="schedule-status-line @(Model.Souhrn.Stihame ? "ok" : "late")">
                <span class="label">Stav:</span>
                <strong data-schedule-summary-state>@stavText</strong>
            </div>
        </div>
```

- ř. 169–179 (souhrn karty/záložky) nahradit:

```razor
        var miniStav = ScheduleStatusText.State(Model.Souhrn.StavVztah, Model.Souhrn.AktualniKrokNazev, Model.Souhrn.PrekroceniDni);
        <div class="schedule-layered-summary">
            <span>Plán: <strong data-schedule-summary-baseline>@ScheduleStatusText.PlanCompletion(Model.Souhrn.BaselineDokonceni)</strong></span>
            <span>Termín: <strong data-schedule-summary-deadline>@Model.Souhrn.TerminUkolu.ToString("dd.MM.yyyy")</strong></span>
            <span class="schedule-status-line @(Model.Souhrn.Stihame ? "ok" : "late")">Stav: <strong data-schedule-summary-state>@miniStav</strong></span>
        </div>
```

- ř. 211–215 (odchylka kroku v Rozpadu) nahradit:

```razor
                var odchylka = krok.OdchylkaDni ?? 0;
                // Spec 2026-10-24 R7, R8: krok bez vlastního plánu má „bez plánu“ (odchylka od dopočteného
                // konce plánu by byla nesmysl), jinak skloňovaná odchylka. Barva jen u kroku s plánem.
                var offsetLabel = ScheduleStatusText.StepOffset(krok.PlanDatum, odchylka);
                var offsetCssClass = krok.PlanDatum is null ? null : odchylka > 0 ? "late" : odchylka < 0 ? "ahead" : null;
```

- [ ] **Step 6: Spustit — musí projít**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~Schedule" > $WS/t3u.log 2>&1; tail -5 $WS/t3u.log`
Expected: `Passed!` (včetně `ScheduleStatusTextSourceTests`, `HarmonogramDateBlokBuilderTests`, `ScheduleDateCalculatorTests`).

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ScheduleStatusRenderTests|FullyQualifiedName~PlanDateClearRenderTests|FullyQualifiedName~ScheduleProposalPlanRemovalRenderTests|FullyQualifiedName~RecordDetailPageRenderTests" > $WS/t3a.log 2>&1; tail -5 $WS/t3a.log`
Expected: `Passed!` — nové 4 i stávající render testy harmonogramu.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs PmTracker.Web/Services/Schedules/HarmonogramDateBlokBuilder.cs PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml PmTracker.Tests.Unit/Schedule/HarmonogramDateBlokBuilderTests.cs PmTracker.Tests.Unit/Architecture/ScheduleStatusTextSourceTests.cs PmTracker.Tests.Api/Controllers/ScheduleStatusRenderTests.cs
git commit -m "feat(harmonogram): souhrn bez plánu — „plán není zadán · celkový termín“, plán dokončení „není zadán“, krok „bez plánu“ (spec 2026-10-24 R3–R8)"
```

---

### Task 4: JS zrcadlo a sdílené případy (golden vectors)

**Files:**
- Create: `tests/js/schedule/__fixtures__/status-cases.json`
- Create: `PmTracker.Tests.Unit/Schedule/ScheduleStatusGoldenVectorTests.cs`
- Create: `PmTracker.Web/wwwroot/js/modules/schedule/scheduleStatus.js`
- Create: `tests/js/schedule/scheduleStatus.test.js`

**Interfaces:**
- Consumes: `HarmonogramDateBlokBuilder.BuildSouhrn`, `ScheduleStatusText.*` (Task 1–3); JS `parseIsoDate`, `diffCalendarDays`, `formatIsoDate`, `formatDisplayDate` (`../utils.js`), `formatDuration`, `NO_PLAN_TEXT` (`../harmonogram/planDuration.js`).
- Produces (JS): `summarizeSchedule({ startIso, terminIso, todayIso, steps: [{ poradi, nazev, planIso, skutecnostIso }] }) → { vztah, aktualniKrokPoradi, aktualniKrokNazev, prekroceniDni, planoveDokonceniIso, celkoveTrvaniDni, dokonceno, stihame }`; `formatScheduleState(souhrn)`, `formatPlanCompletion(planoveDokonceniIso)`, `formatTotalDuration(planoveDokonceniIso, celkoveTrvaniDni)`, `formatDni(pocet)`; `VZTAH`, `PLAN_NENI_ZADAN`.

Pozn.: C# strana sdílených případů projde hned (Task 1–3 ji už udělaly test-first) — zamyká očekávané hodnoty. RED je JS test (modul neexistuje).

- [ ] **Step 1: Sdílené případy** — `tests/js/schedule/__fixtures__/status-cases.json`:

```json
{
  "kroky": [
    { "poradi": 1, "nazev": "1. priprava zadani dodavateli" },
    { "poradi": 2, "nazev": "2. konzultace terminu s dodavatelem pred vytvorenim zadani" },
    { "poradi": 3, "nazev": "3. odeslani zadani dodavateli" },
    { "poradi": 4, "nazev": "4. dodani navrhu reseni" },
    { "poradi": 5, "nazev": "5. vyporadani pripominek" },
    { "poradi": 6, "nazev": "6. odeslani pozadavku na vyrobu" },
    { "poradi": 7, "nazev": "7. dodani funkcionality dodavatelem" },
    { "poradi": 8, "nazev": "8. pripominkovani" },
    { "poradi": 9, "nazev": "9. testovani" },
    { "poradi": 10, "nazev": "10. nasazeni do provozu" }
  ],
  "cases": [
    {
      "name": "k-planu-skluz-5-dni",
      "input": { "start": "2026-01-01", "termin": "2026-06-30", "today": "2026-03-01",
        "steps": [ { "poradi": 1, "plan": "2026-01-20", "skutecnost": "2026-01-22" }, { "poradi": 2, "plan": "2026-02-24" } ] },
      "expected": { "vztah": "AktualniKrok", "aktualniKrokPoradi": 2, "prekroceniDni": 5, "planoveDokonceni": "2026-02-24",
        "celkoveTrvaniDni": 54, "stihame": false,
        "stavText": "Aktuální krok: 2. konzultace terminu s dodavatelem pred vytvorenim zadani · skluz +5 dní",
        "planDokonceniText": "24.02.2026", "trvaniText": "54 dnů" }
    },
    {
      "name": "k-planu-skluz-1-den",
      "input": { "start": "2026-01-01", "termin": "2026-06-30", "today": "2026-02-25",
        "steps": [ { "poradi": 1, "plan": "2026-01-20", "skutecnost": "2026-01-22" }, { "poradi": 2, "plan": "2026-02-24" } ] },
      "expected": { "vztah": "AktualniKrok", "aktualniKrokPoradi": 2, "prekroceniDni": 1, "planoveDokonceni": "2026-02-24",
        "celkoveTrvaniDni": 54, "stihame": false,
        "stavText": "Aktuální krok: 2. konzultace terminu s dodavatelem pred vytvorenim zadani · skluz +1 den",
        "planDokonceniText": "24.02.2026", "trvaniText": "54 dnů" }
    },
    {
      "name": "k-planu-predstih-3-dny",
      "input": { "start": "2026-01-01", "termin": "2026-06-30", "today": "2026-02-21",
        "steps": [ { "poradi": 1, "plan": "2026-01-20", "skutecnost": "2026-01-22" }, { "poradi": 2, "plan": "2026-02-24" } ] },
      "expected": { "vztah": "AktualniKrok", "aktualniKrokPoradi": 2, "prekroceniDni": -3, "planoveDokonceni": "2026-02-24",
        "celkoveTrvaniDni": 54, "stihame": true,
        "stavText": "Aktuální krok: 2. konzultace terminu s dodavatelem pred vytvorenim zadani · v předstihu 3 dny",
        "planDokonceniText": "24.02.2026", "trvaniText": "54 dnů" }
    },
    {
      "name": "k-planu-predstih-1-den",
      "input": { "start": "2026-01-01", "termin": "2026-06-30", "today": "2026-02-23",
        "steps": [ { "poradi": 1, "plan": "2026-01-20", "skutecnost": "2026-01-22" }, { "poradi": 2, "plan": "2026-02-24" } ] },
      "expected": { "vztah": "AktualniKrok", "aktualniKrokPoradi": 2, "prekroceniDni": -1, "planoveDokonceni": "2026-02-24",
        "celkoveTrvaniDni": 54, "stihame": true,
        "stavText": "Aktuální krok: 2. konzultace terminu s dodavatelem pred vytvorenim zadani · v předstihu 1 den",
        "planDokonceniText": "24.02.2026", "trvaniText": "54 dnů" }
    },
    {
      "name": "k-planu-dle-planu",
      "input": { "start": "2026-01-01", "termin": "2026-06-30", "today": "2026-02-24",
        "steps": [ { "poradi": 1, "plan": "2026-01-20", "skutecnost": "2026-01-22" }, { "poradi": 2, "plan": "2026-02-24" } ] },
      "expected": { "vztah": "AktualniKrok", "aktualniKrokPoradi": 2, "prekroceniDni": 0, "planoveDokonceni": "2026-02-24",
        "celkoveTrvaniDni": 54, "stihame": true,
        "stavText": "Aktuální krok: 2. konzultace terminu s dodavatelem pred vytvorenim zadani · dle plánu",
        "planDokonceniText": "24.02.2026", "trvaniText": "54 dnů" }
    },
    {
      "name": "bez-planu-automat-zbyva-12-dni",
      "input": { "start": "2026-07-08", "termin": "2026-11-05", "today": "2026-10-24",
        "steps": [ { "poradi": 1, "skutecnost": "2026-07-10" }, { "poradi": 3, "skutecnost": "2026-08-01" }, { "poradi": 4, "skutecnost": "2026-09-15" } ] },
      "expected": { "vztah": "CelkovyTermin", "aktualniKrokPoradi": 5, "prekroceniDni": -12, "planoveDokonceni": null,
        "celkoveTrvaniDni": 0, "stihame": true,
        "stavText": "Aktuální krok: 5. vyporadani pripominek · plán není zadán · celkový termín: zbývá 12 dní",
        "planDokonceniText": "není zadán", "trvaniText": "–" }
    },
    {
      "name": "bez-planu-po-terminu-skluz-10-dni",
      "input": { "start": "2026-07-08", "termin": "2026-10-14", "today": "2026-10-24",
        "steps": [ { "poradi": 1, "skutecnost": "2026-07-10" }, { "poradi": 3, "skutecnost": "2026-08-01" }, { "poradi": 4, "skutecnost": "2026-09-15" } ] },
      "expected": { "vztah": "CelkovyTermin", "aktualniKrokPoradi": 5, "prekroceniDni": 10, "planoveDokonceni": null,
        "celkoveTrvaniDni": 0, "stihame": false,
        "stavText": "Aktuální krok: 5. vyporadani pripominek · plán není zadán · celkový termín: skluz +10 dní",
        "planDokonceniText": "není zadán", "trvaniText": "–" }
    },
    {
      "name": "bez-planu-skluz-2-dny",
      "input": { "start": "2026-07-08", "termin": "2026-10-22", "today": "2026-10-24",
        "steps": [ { "poradi": 1, "skutecnost": "2026-07-10" }, { "poradi": 3, "skutecnost": "2026-08-01" }, { "poradi": 4, "skutecnost": "2026-09-15" } ] },
      "expected": { "vztah": "CelkovyTermin", "aktualniKrokPoradi": 5, "prekroceniDni": 2, "planoveDokonceni": null,
        "celkoveTrvaniDni": 0, "stihame": false,
        "stavText": "Aktuální krok: 5. vyporadani pripominek · plán není zadán · celkový termín: skluz +2 dny",
        "planDokonceniText": "není zadán", "trvaniText": "–" }
    },
    {
      "name": "bez-planu-termin-je-dnes",
      "input": { "start": "2026-07-08", "termin": "2026-10-24", "today": "2026-10-24",
        "steps": [ { "poradi": 1, "skutecnost": "2026-07-10" }, { "poradi": 3, "skutecnost": "2026-08-01" }, { "poradi": 4, "skutecnost": "2026-09-15" } ] },
      "expected": { "vztah": "CelkovyTermin", "aktualniKrokPoradi": 5, "prekroceniDni": 0, "planoveDokonceni": null,
        "celkoveTrvaniDni": 0, "stihame": true,
        "stavText": "Aktuální krok: 5. vyporadani pripominek · plán není zadán · celkový termín je dnes",
        "planDokonceniText": "není zadán", "trvaniText": "–" }
    },
    {
      "name": "bez-planu-zbyva-1-den",
      "input": { "start": "2026-07-08", "termin": "2026-10-25", "today": "2026-10-24",
        "steps": [ { "poradi": 1, "skutecnost": "2026-07-10" }, { "poradi": 3, "skutecnost": "2026-08-01" }, { "poradi": 4, "skutecnost": "2026-09-15" } ] },
      "expected": { "vztah": "CelkovyTermin", "aktualniKrokPoradi": 5, "prekroceniDni": -1, "planoveDokonceni": null,
        "celkoveTrvaniDni": 0, "stihame": true,
        "stavText": "Aktuální krok: 5. vyporadani pripominek · plán není zadán · celkový termín: zbývá 1 den",
        "planDokonceniText": "není zadán", "trvaniText": "–" }
    },
    {
      "name": "bez-planu-zbyvaji-3-dny",
      "input": { "start": "2026-07-08", "termin": "2026-10-27", "today": "2026-10-24",
        "steps": [ { "poradi": 1, "skutecnost": "2026-07-10" }, { "poradi": 3, "skutecnost": "2026-08-01" }, { "poradi": 4, "skutecnost": "2026-09-15" } ] },
      "expected": { "vztah": "CelkovyTermin", "aktualniKrokPoradi": 5, "prekroceniDni": -3, "planoveDokonceni": null,
        "celkoveTrvaniDni": 0, "stihame": true,
        "stavText": "Aktuální krok: 5. vyporadani pripominek · plán není zadán · celkový termín: zbývají 3 dny",
        "planDokonceniText": "není zadán", "trvaniText": "–" }
    },
    {
      "name": "bez-planu-zbyva-22-dni",
      "input": { "start": "2026-07-08", "termin": "2026-11-15", "today": "2026-10-24",
        "steps": [ { "poradi": 1, "skutecnost": "2026-07-10" }, { "poradi": 3, "skutecnost": "2026-08-01" }, { "poradi": 4, "skutecnost": "2026-09-15" } ] },
      "expected": { "vztah": "CelkovyTermin", "aktualniKrokPoradi": 5, "prekroceniDni": -22, "planoveDokonceni": null,
        "celkoveTrvaniDni": 0, "stihame": true,
        "stavText": "Aktuální krok: 5. vyporadani pripominek · plán není zadán · celkový termín: zbývá 22 dní",
        "planDokonceniText": "není zadán", "trvaniText": "–" }
    },
    {
      "name": "bez-planu-bez-skutecnosti",
      "input": { "start": "2026-07-08", "termin": "2026-11-05", "today": "2026-10-24", "steps": [] },
      "expected": { "vztah": "CelkovyTermin", "aktualniKrokPoradi": 1, "prekroceniDni": -12, "planoveDokonceni": null,
        "celkoveTrvaniDni": 0, "stihame": true,
        "stavText": "Aktuální krok: 1. priprava zadani dodavateli · plán není zadán · celkový termín: zbývá 12 dní",
        "planDokonceniText": "není zadán", "trvaniText": "–" }
    },
    {
      "name": "aktualni-bez-planu-po-kroku-s-planem",
      "input": { "start": "2026-01-01", "termin": "2026-06-30", "today": "2026-03-01",
        "steps": [ { "poradi": 1, "plan": "2026-01-20", "skutecnost": "2026-01-22" }, { "poradi": 3, "plan": "2026-04-01" } ] },
      "expected": { "vztah": "CelkovyTermin", "aktualniKrokPoradi": 2, "prekroceniDni": -121, "planoveDokonceni": "2026-04-01",
        "celkoveTrvaniDni": 90, "stihame": true,
        "stavText": "Aktuální krok: 2. konzultace terminu s dodavatelem pred vytvorenim zadani · plán není zadán · celkový termín: zbývá 121 dní",
        "planDokonceniText": "01.04.2026", "trvaniText": "90 dnů" }
    },
    {
      "name": "aktualni-s-planem-drivejsi-bez-planu",
      "input": { "start": "2026-07-08", "termin": "2026-11-05", "today": "2026-10-24",
        "steps": [ { "poradi": 1, "skutecnost": "2026-07-10" }, { "poradi": 2, "plan": "2026-10-20" } ] },
      "expected": { "vztah": "AktualniKrok", "aktualniKrokPoradi": 2, "prekroceniDni": 4, "planoveDokonceni": "2026-10-20",
        "celkoveTrvaniDni": 104, "stihame": false,
        "stavText": "Aktuální krok: 2. konzultace terminu s dodavatelem pred vytvorenim zadani · skluz +4 dny",
        "planDokonceniText": "20.10.2026", "trvaniText": "104 dnů" }
    },
    {
      "name": "plan-pred-predchozim-se-orizne",
      "input": { "start": "2026-01-01", "termin": "2026-06-30", "today": "2026-03-01",
        "steps": [ { "poradi": 1, "plan": "2026-02-10", "skutecnost": "2026-02-11" }, { "poradi": 2, "plan": "2026-02-05" } ] },
      "expected": { "vztah": "AktualniKrok", "aktualniKrokPoradi": 2, "prekroceniDni": 19, "planoveDokonceni": "2026-02-10",
        "celkoveTrvaniDni": 40, "stihame": false,
        "stavText": "Aktuální krok: 2. konzultace terminu s dodavatelem pred vytvorenim zadani · skluz +19 dní",
        "planDokonceniText": "10.02.2026", "trvaniText": "40 dnů" }
    },
    {
      "name": "trvani-1-den",
      "input": { "start": "2026-01-01", "termin": "2026-06-30", "today": "2026-01-02",
        "steps": [ { "poradi": 1, "plan": "2026-01-02" } ] },
      "expected": { "vztah": "AktualniKrok", "aktualniKrokPoradi": 1, "prekroceniDni": 0, "planoveDokonceni": "2026-01-02",
        "celkoveTrvaniDni": 1, "stihame": true,
        "stavText": "Aktuální krok: 1. priprava zadani dodavateli · dle plánu",
        "planDokonceniText": "02.01.2026", "trvaniText": "1 den" }
    },
    {
      "name": "dokonceno",
      "input": { "start": "2026-01-01", "termin": "2026-06-30", "today": "2026-03-01",
        "steps": [
          { "poradi": 1, "plan": "2026-01-11", "skutecnost": "2026-01-11" }, { "poradi": 2, "plan": "2026-01-12", "skutecnost": "2026-01-12" },
          { "poradi": 3, "plan": "2026-01-13", "skutecnost": "2026-01-13" }, { "poradi": 4, "plan": "2026-01-14", "skutecnost": "2026-01-14" },
          { "poradi": 5, "plan": "2026-01-15", "skutecnost": "2026-01-15" }, { "poradi": 6, "plan": "2026-01-16", "skutecnost": "2026-01-16" },
          { "poradi": 7, "plan": "2026-01-17", "skutecnost": "2026-01-17" }, { "poradi": 8, "plan": "2026-01-18", "skutecnost": "2026-01-18" },
          { "poradi": 9, "plan": "2026-01-19", "skutecnost": "2026-01-19" }, { "poradi": 10, "plan": "2026-01-20", "skutecnost": "2026-01-20" } ] },
      "expected": { "vztah": "Dokonceno", "aktualniKrokPoradi": null, "prekroceniDni": 0, "planoveDokonceni": "2026-01-20",
        "celkoveTrvaniDni": 19, "stihame": true, "stavText": "Dokončeno",
        "planDokonceniText": "20.01.2026", "trvaniText": "19 dnů" }
    },
    {
      "name": "dokonceno-bez-planu",
      "input": { "start": "2026-01-01", "termin": "2026-06-30", "today": "2026-03-01",
        "steps": [
          { "poradi": 1, "skutecnost": "2026-01-11" }, { "poradi": 2, "skutecnost": "2026-01-12" }, { "poradi": 3, "skutecnost": "2026-01-13" },
          { "poradi": 4, "skutecnost": "2026-01-14" }, { "poradi": 5, "skutecnost": "2026-01-15" }, { "poradi": 6, "skutecnost": "2026-01-16" },
          { "poradi": 7, "skutecnost": "2026-01-17" }, { "poradi": 8, "skutecnost": "2026-01-18" }, { "poradi": 9, "skutecnost": "2026-01-19" },
          { "poradi": 10, "skutecnost": "2026-01-20" } ] },
      "expected": { "vztah": "Dokonceno", "aktualniKrokPoradi": null, "prekroceniDni": 0, "planoveDokonceni": null,
        "celkoveTrvaniDni": 0, "stihame": true, "stavText": "Dokončeno",
        "planDokonceniText": "není zadán", "trvaniText": "–" }
    }
  ]
}
```

- [ ] **Step 2: C# strana sdílených případů** — `ScheduleStatusGoldenVectorTests.cs`:

```csharp
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Schedules;
using Xunit;

namespace PmTracker.Tests.Unit.Schedule;

/// <summary>
/// Spec 2026-10-24 R11: golden-vector parita stavu harmonogramu. Server (BuildSouhrn + ScheduleStatusText,
/// tedy skutečná cesta šablon) a JS zrcadlo (schedule/scheduleStatus.js) musí nad TÝMIŽ případy
/// (tests/js/schedule/__fixtures__/status-cases.json) dát stejný vztah, čísla i texty.
/// </summary>
public sealed class ScheduleStatusGoldenVectorTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře.");
    }

    private static JsonDocument LoadFixture()
    {
        var path = Path.Combine(RepoRoot(), "tests", "js", "schedule", "__fixtures__", "status-cases.json");
        File.Exists(path).Should().BeTrue($"sdílené případy musí existovat: {path}");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static DateTime? OptionalDate(JsonElement step, string property)
        => step.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? DateTime.Parse(value.GetString()!)
            : null;

    [Fact]
    public void Fixture_step_names_match_HarmonogramKroky()
    {
        using var doc = LoadFixture();
        var names = doc.RootElement.GetProperty("kroky").EnumerateArray()
            .Select(k => (k.GetProperty("poradi").GetInt32(), k.GetProperty("nazev").GetString()))
            .ToList();

        names.Should().Equal(HarmonogramKroky.Vse.Select(k => (k.Poradi, (string?)k.Nazev)),
            "JS dostává názvy kroků z řádků editoru (krok.Nazev) — případy musí mluvit stejně");
    }

    [Fact]
    public void BuildSouhrn_and_texts_match_golden_vectors()
    {
        using var doc = LoadFixture();
        foreach (var c in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            var name = c.GetProperty("name").GetString();
            var input = c.GetProperty("input");
            var rows = input.GetProperty("steps").EnumerateArray().Select(s => new ZaznamHarmonogramKrokEntity
            {
                Poradi = (byte)s.GetProperty("poradi").GetInt32(),
                PlanDatum = OptionalDate(s, "plan"),
                SkutecnostDatum = OptionalDate(s, "skutecnost")
            }).ToList();

            var souhrn = HarmonogramDateBlokBuilder.BuildSouhrn(
                DateTime.Parse(input.GetProperty("start").GetString()!),
                rows,
                DateTime.Parse(input.GetProperty("termin").GetString()!),
                DateTime.Parse(input.GetProperty("today").GetString()!));

            var e = c.GetProperty("expected");
            souhrn.StavVztah.ToString().Should().Be(e.GetProperty("vztah").GetString(), $"případ {name}: vztah");
            var aktualni = e.GetProperty("aktualniKrokPoradi");
            souhrn.AktualniKrokPoradi.Should().Be(
                aktualni.ValueKind == JsonValueKind.Null ? (int?)null : aktualni.GetInt32(), $"případ {name}: aktuální krok");
            souhrn.PrekroceniDni.Should().Be(e.GetProperty("prekroceniDni").GetInt32(), $"případ {name}: překročení");
            // Nejdřív do proměnné: „x?.ToString().Should()“ by při null přeskočilo i samotnou kontrolu.
            // JsonElement.GetString() vrací pro JSON null hodnotu null — porovnání tak hlídá obě strany.
            var planoveDokonceni = souhrn.BaselineDokonceni?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            planoveDokonceni.Should().Be(e.GetProperty("planoveDokonceni").GetString(), $"případ {name}: plánové dokončení");
            souhrn.CelkoveTrvaniDni.Should().Be(e.GetProperty("celkoveTrvaniDni").GetInt32(), $"případ {name}: trvání");
            souhrn.Stihame.Should().Be(e.GetProperty("stihame").GetBoolean(), $"případ {name}: štítek");
            ScheduleStatusText.State(souhrn.StavVztah, souhrn.AktualniKrokNazev, souhrn.PrekroceniDni)
                .Should().Be(e.GetProperty("stavText").GetString(), $"případ {name}: text stavu");
            ScheduleStatusText.PlanCompletion(souhrn.BaselineDokonceni)
                .Should().Be(e.GetProperty("planDokonceniText").GetString(), $"případ {name}: plán dokončení");
            ScheduleStatusText.TotalDuration(souhrn.BaselineDokonceni, souhrn.CelkoveTrvaniDni)
                .Should().Be(e.GetProperty("trvaniText").GetString(), $"případ {name}: trvání text");
        }
    }
}
```

- [ ] **Step 3: JS test sdílených případů** — `tests/js/schedule/scheduleStatus.test.js`:

```js
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import {
    formatPlanCompletion,
    formatScheduleState,
    formatTotalDuration,
    summarizeSchedule
} from "../../../PmTracker.Web/wwwroot/js/modules/schedule/scheduleStatus.js";

// Spec 2026-10-24 R11: tytéž případy čte ScheduleStatusGoldenVectorTests (xUnit) — parita se serverem.
const fixture = JSON.parse(readFileSync(fileURLToPath(new URL("./__fixtures__/status-cases.json", import.meta.url))));

/** Editor posílá vždy všech 10 kroků (řádky tabulky); případy uvádějí jen kroky s hodnotou. */
function expandSteps(steps) {
    const byPoradi = new Map(steps.map((step) => [step.poradi, step]));
    return fixture.kroky.map((krok) => ({
        poradi: krok.poradi,
        nazev: krok.nazev,
        planIso: byPoradi.get(krok.poradi)?.plan ?? null,
        skutecnostIso: byPoradi.get(krok.poradi)?.skutecnost ?? null
    }));
}

for (const c of fixture.cases) {
    test(`parita stavu: ${c.name}`, () => {
        const souhrn = summarizeSchedule({
            startIso: c.input.start,
            terminIso: c.input.termin,
            todayIso: c.input.today,
            steps: expandSteps(c.input.steps)
        });
        const e = c.expected;
        assert.equal(souhrn.vztah, e.vztah, "vztah");
        assert.equal(souhrn.aktualniKrokPoradi, e.aktualniKrokPoradi, "aktuální krok");
        assert.equal(souhrn.prekroceniDni, e.prekroceniDni, "překročení");
        assert.equal(souhrn.planoveDokonceniIso, e.planoveDokonceni, "plánové dokončení");
        assert.equal(souhrn.celkoveTrvaniDni, e.celkoveTrvaniDni, "trvání");
        assert.equal(souhrn.stihame, e.stihame, "štítek");
        assert.equal(formatScheduleState(souhrn), e.stavText, "text stavu");
        assert.equal(formatPlanCompletion(souhrn.planoveDokonceniIso), e.planDokonceniText, "plán dokončení");
        assert.equal(formatTotalDuration(souhrn.planoveDokonceniIso, souhrn.celkoveTrvaniDni), e.trvaniText, "trvání text");
    });
}
```

- [ ] **Step 4: Spustit — C# projde, JS musí selhat**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ScheduleStatusGoldenVectorTests" > $WS/t4c.log 2>&1; tail -5 $WS/t4c.log`
Expected: `Passed!  - Failed: 0, Passed: 2` (C# už je hotové; případ, který neprojde, je chyba ve fixture nebo v Task 1–3 — dohledat, ne upravit očekávání naslepo).

Run: `node --test tests/js/schedule/scheduleStatus.test.js > $WS/t4j.log 2>&1; tail -8 $WS/t4j.log`
Expected: FAIL — `ERR_MODULE_NOT_FOUND … scheduleStatus.js`.

- [ ] **Step 5: Implementace** — `PmTracker.Web/wwwroot/js/modules/schedule/scheduleStatus.js`:

```js
/**
 * schedule/scheduleStatus.js — JS dvojče serverového ScheduleDateCalculator.Summarize
 * a ScheduleStatusText (spec 2026-10-24). Editor harmonogramu (block.js) jím živě přepočítává
 * Stav, Plán dokončení a Trvání. Parita s C# je zamčená sdílenými případy
 * tests/js/schedule/__fixtures__/status-cases.json (node:test + xUnit nad týmiž daty).
 */

import { diffCalendarDays, formatDisplayDate, formatIsoDate, parseIsoDate } from "../utils.js";
import { formatDuration, NO_PLAN_TEXT } from "../harmonogram/planDuration.js";

/** Vůči čemu se stav počítá (C# HarmonogramStavVztah). */
export const VZTAH = Object.freeze({
    AKTUALNI_KROK: "AktualniKrok",
    CELKOVY_TERMIN: "CelkovyTermin",
    DOKONCENO: "Dokonceno"
});

export const PLAN_NENI_ZADAN = "není zadán";

/**
 * Souhrn harmonogramu z datumů kroků (mirror ScheduleDateCalculator.Compute + Summarize).
 * @param {{startIso: string, terminIso: string, todayIso: string,
 *          steps: Array<{poradi: number, nazev: string, planIso: string|null, skutecnostIso: string|null}>}} input
 */
export function summarizeSchedule({ startIso, terminIso, todayIso, steps }) {
    const start = parseIsoDate(startIso);
    const termin = parseIsoDate(terminIso);
    const today = parseIsoDate(todayIso);
    const ordered = [...steps].sort((left, right) => left.poradi - right.poradi);

    // Konec plánu kroku = jeho plán; bez plánu (nebo plán před koncem předchozího) = konec předchozího.
    let planCursor = start;
    const planEnds = ordered.map((step) => {
        const plan = parseIsoDate(step.planIso);
        if (plan && diffCalendarDays(plan, planCursor) > 0) {
            planCursor = plan;
        }
        return planCursor;
    });

    // Aktuální krok = první bez skutečnosti po posledním kroku se skutečností.
    let posledniVyplneny = -1;
    ordered.forEach((step, index) => {
        if (parseIsoDate(step.skutecnostIso)) {
            posledniVyplneny = index;
        }
    });
    const aktualniIndex = ordered.findIndex((step, index) => index > posledniVyplneny && !parseIsoDate(step.skutecnostIso));

    // R6: plánové dokončení jen tehdy, když aspoň jeden krok má vlastní plán.
    const maPlan = ordered.some((step) => parseIsoDate(step.planIso));
    const planoveDokonceni = maPlan && planEnds.length > 0 ? planEnds[planEnds.length - 1] : null;
    const zaklad = {
        planoveDokonceniIso: planoveDokonceni ? formatIsoDate(planoveDokonceni) : null,
        celkoveTrvaniDni: planoveDokonceni ? Math.max(0, diffCalendarDays(planoveDokonceni, start)) : 0
    };

    if (aktualniIndex < 0) {
        return {
            ...zaklad,
            vztah: VZTAH.DOKONCENO,
            aktualniKrokPoradi: null,
            aktualniKrokNazev: null,
            prekroceniDni: 0,
            dokonceno: true,
            stihame: true
        };
    }

    // R2: bez vlastního plánu aktuálního kroku se skluz počítá k celkovému termínu.
    const aktualni = ordered[aktualniIndex];
    const vlastniPlan = Boolean(parseIsoDate(aktualni.planIso));
    const prekroceniDni = vlastniPlan
        ? diffCalendarDays(today, planEnds[aktualniIndex])
        : diffCalendarDays(today, termin);
    return {
        ...zaklad,
        vztah: vlastniPlan ? VZTAH.AKTUALNI_KROK : VZTAH.CELKOVY_TERMIN,
        aktualniKrokPoradi: aktualni.poradi,
        aktualniKrokNazev: aktualni.nazev,
        prekroceniDni,
        dokonceno: false,
        stihame: prekroceniDni <= 0
    };
}

/** R4: „1 den“, „2–4 dny“, jinak „N dní“. */
export function formatDni(pocet) {
    const unit = pocet === 1 ? "den" : pocet >= 2 && pocet <= 4 ? "dny" : "dní";
    return `${pocet} ${unit}`;
}

function formatZbyva(pocet) {
    return pocet >= 2 && pocet <= 4 ? `zbývají ${formatDni(pocet)}` : `zbývá ${formatDni(pocet)}`;
}

/** R3: sjednocený stav (mirror ScheduleStatusText.State). */
export function formatScheduleState({ vztah, aktualniKrokNazev, prekroceniDni }) {
    if (vztah === VZTAH.DOKONCENO) {
        return "Dokončeno";
    }
    const krok = `Aktuální krok: ${aktualniKrokNazev}`;
    if (vztah === VZTAH.CELKOVY_TERMIN) {
        const termin = prekroceniDni > 0 ? `celkový termín: skluz +${formatDni(prekroceniDni)}`
            : prekroceniDni < 0 ? `celkový termín: ${formatZbyva(-prekroceniDni)}`
            : "celkový termín je dnes";
        return `${krok} · plán není zadán · ${termin}`;
    }
    return prekroceniDni > 0 ? `${krok} · skluz +${formatDni(prekroceniDni)}`
        : prekroceniDni < 0 ? `${krok} · v předstihu ${formatDni(-prekroceniDni)}`
        : `${krok} · dle plánu`;
}

/** R6: „Plán dokončení“ — datum, nebo „není zadán“ (mirror ScheduleStatusText.PlanCompletion). */
export function formatPlanCompletion(planoveDokonceniIso) {
    const date = parseIsoDate(planoveDokonceniIso);
    return date ? formatDisplayDate(date) : PLAN_NENI_ZADAN;
}

/** R6: celkové trvání — „–“ bez plánu (mirror ScheduleStatusText.TotalDuration). */
export function formatTotalDuration(planoveDokonceniIso, celkoveTrvaniDni) {
    return planoveDokonceniIso ? formatDuration(celkoveTrvaniDni) : NO_PLAN_TEXT;
}
```

- [ ] **Step 6: Spustit — musí projít**

Run: `node --test tests/js/schedule/scheduleStatus.test.js > $WS/t4j.log 2>&1; tail -8 $WS/t4j.log`
Expected: `# pass 19`, `# fail 0`.

Run: `npm test > $WS/t4all.log 2>&1; tail -8 $WS/t4all.log`
Expected: `# fail 0` (56 stávajících + 19 nových).

- [ ] **Step 7: Commit**

```bash
git add tests/js/schedule/__fixtures__/status-cases.json tests/js/schedule/scheduleStatus.test.js PmTracker.Tests.Unit/Schedule/ScheduleStatusGoldenVectorTests.cs PmTracker.Web/wwwroot/js/modules/schedule/scheduleStatus.js
git commit -m "feat(harmonogram): JS zrcadlo stavu se sdílenými případy pro server i prohlížeč (spec 2026-10-24 R11)"
```

---

### Task 5: Editor — živý přepočet a meze kalendáře

**Files:**
- Create: `PmTracker.Web/wwwroot/js/modules/harmonogram/planChronology.js`
- Create: `tests/js/schedule/planChronology.test.js`
- Modify: `PmTracker.Web/wwwroot/js/modules/schedule/block.js` (importy ř. 8–17; řádky editoru ř. 146–156; `setDateInputValue` ř. 205–220; `recalcFromDate`/`recalcFromDelayDate` ř. 310–316; `renderSummary` ř. 368–405; `renderEditorRows` ř. 536–558; `recalcAll` ř. 591–602; `applyPlanChronologyBounds` ř. 604–620; `computeDateModel` ř. 635–699; `bind` ř. 826–831)
- Modify: `PmTracker.Tests.Unit/Architecture/ScheduleStatusTextSourceTests.cs` (+ `block.js`)
- Test: `PmTracker.Tests.E2E/Scenarios/ScheduleLiveRecalcScenariosTests.cs` (nový)

**Interfaces:**
- Consumes: `summarizeSchedule`, `formatScheduleState`, `formatPlanCompletion`, `formatTotalDuration` (Task 4).
- Produces: `planBounds(plans: Array<string|null>) → Array<{min: string|null, max: string|null}>`; řádek editoru `entry.planInput` (skryté `input[name$=".PlanDatum"]`).

- [ ] **Step 1: Padající JS test mezí** — `tests/js/schedule/planChronology.test.js`:

```js
import { test } from "node:test";
import assert from "node:assert/strict";
import { planBounds } from "../../../PmTracker.Web/wwwroot/js/modules/harmonogram/planChronology.js";

// Spec 2026-10-24 R10: meze = nejbližší dřívější / pozdější krok s plánem (pravidlo serveru).
test("meze podle sousedů, když všechny kroky plán mají", () => {
    assert.deepEqual(planBounds(["2026-03-01", "2026-03-10", "2026-03-20"]), [
        { min: null, max: "2026-03-10" },
        { min: "2026-03-01", max: "2026-03-20" },
        { min: "2026-03-10", max: null }
    ]);
});

test("krok bez plánu se přeskakuje", () => {
    const bounds = planBounds(["2026-03-01", "", "2026-04-01", null, "2026-05-01"]);
    assert.deepEqual(bounds[2], { min: "2026-03-01", max: "2026-05-01" });
    assert.deepEqual(bounds[0], { min: null, max: "2026-04-01" });
    assert.deepEqual(bounds[1], { min: "2026-03-01", max: "2026-04-01" }, "prázdný krok má meze taky");
});

test("bez plánu žádné meze", () => {
    assert.deepEqual(planBounds(["", "", ""]), [
        { min: null, max: null }, { min: null, max: null }, { min: null, max: null }
    ]);
});
```

- [ ] **Step 2: Padající E2E** — `PmTracker.Tests.E2E/Scenarios/ScheduleLiveRecalcScenariosTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using static Microsoft.Playwright.Assertions;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Spec 2026-10-24 R9, R10: editor harmonogramu po ✕ i po výběru data hned přepočítá stav
/// (u aktuálního kroku bez plánu k celkovému termínu) a kalendář plánu vypne dny mimo pořadí
/// podle nejbližšího kroku s plánem.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class ScheduleLiveRecalcScenariosTests
{
    private const string Krok1 = "Aktuální krok: 1. priprava zadani dodavateli";
    private readonly E2ETestFixture _fixture;

    public ScheduleLiveRecalcScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private PmTrackerDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>().UseSqlServer(_fixture.Database.ConnectionString).Options);

    /// <summary>Úkol založený před 40 dny, termín za 60 dní, plán kroku k = založení + 7·k, krok <paramref name="bezPlanu"/> bez plánu.</summary>
    private async Task<(int RecordId, DateTime Zalozeni)> SeedAsync(int? bezPlanu = null)
    {
        await using var db = CreateDbContext();
        var subsystemId = await db.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == _fixture.ProjectId && x.DatumOdebrani == null)
            .OrderBy(x => x.SubsystemId).Select(x => x.SubsystemId).FirstAsync();
        var categoryId = await db.CiselnikKategoriiZaznamu.Where(x => x.Kod == "U").Select(x => x.Id).FirstAsync();
        var stateId = await db.CiselnikStavuUkolu.Where(x => !x.IsFinal).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        var number = (await db.ProjektoveZaznamy.Where(x => x.ProjektId == _fixture.ProjectId)
            .Select(x => (int?)x.CisloZaznamu).MaxAsync() ?? 0) + 1;
        var zalozeni = DateTime.Today.AddDays(-40);
        var record = new ProjektovyZaznamEntity
        {
            ProjektId = _fixture.ProjectId, KategorieId = categoryId, StavUkoluId = stateId, CisloZaznamu = number,
            Nazev = "E2E zivy prepocet", Cil = "E2E zivy prepocet", Popis = "E2E zivy prepocet",
            VlastnikId = _fixture.AdminOsobaId, DatumZalozeni = zalozeni, DatumUkonceni = DateTime.Today.AddDays(60),
            SubsystemId = subsystemId
        };
        db.ProjektoveZaznamy.Add(record);
        await db.SaveChangesAsync();
        for (var poradi = 1; poradi <= 10; poradi++)
        {
            db.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = record.Id,
                Poradi = (byte)poradi,
                PlanDatum = poradi == bezPlanu ? null : zalozeni.AddDays(7 * poradi),
                SkutecnostRezim = HarmonogramManualSteps.IsManual(poradi)
                    ? (byte)SkutecnostRezimEnum.Manual
                    : (byte)SkutecnostRezimEnum.Auto,
                SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Neznamo,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
        return (record.Id, zalozeni);
    }

    private async Task CleanupAsync(int recordId)
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.zaznam_edit_zamek WHERE zaznam_id = {recordId}");
        await db.ZaznamHarmonogramKroky.Where(x => x.ZaznamId == recordId).ExecuteDeleteAsync();
        await db.ProjektoveZaznamy.Where(x => x.Id == recordId).ExecuteDeleteAsync();
    }

    private async Task<IPage> OpenEditorScheduleAsync(int recordId)
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}");
        await page.Locator("pm-tabs [key='schedule']").First.ClickAsync();
        return page;
    }

    private static ILocator PlanField(IPage page, int poradi)
        => page.Locator($"tr[data-schedule-step-row][data-step-index='{poradi}'] pm-date-field[data-schedule-plan-step]");

    /// <summary>Otevře kalendář pole a šipkami ‹ › přejde na měsíc daného dne; vrátí tlačítko dne.</summary>
    private static async Task<ILocator> OpenCalendarAtAsync(IPage page, ILocator dateField, DateTime day)
    {
        await dateField.Locator("[data-app-date-open]").DispatchEventAsync("click");
        var panel = page.Locator("[data-app-date-panel]:not([hidden])");
        await Expect(panel).ToHaveCountAsync(1);
        var shownMonth = int.Parse(await panel.Locator("[data-app-date-month]").InputValueAsync());
        var shownYear = int.Parse(await panel.Locator("[data-app-date-year]").InputValueAsync());
        var diff = (day.Year * 12 + day.Month - 1) - (shownYear * 12 + shownMonth);
        for (var i = 0; i < Math.Abs(diff); i++)
        {
            await panel.Locator(diff > 0 ? "[data-app-date-next]" : "[data-app-date-prev]").DispatchEventAsync("click");
        }
        return panel.Locator($"button.app-date-day[data-iso='{day:yyyy-MM-dd}']");
    }

    [Fact]
    public async Task Editor_ClearCurrentStepPlan_StateCountsToDeadline_PickDate_BackToPlan()
    {
        var (recordId, zalozeni) = await SeedAsync();
        var page = await OpenEditorScheduleAsync(recordId);
        try
        {
            var state = page.Locator("[data-schedule-block][data-schedule-mode='record-editor'] [data-schedule-summary-state]");
            var statusLine = page.Locator("[data-schedule-block][data-schedule-mode='record-editor'] .schedule-status-line");
            await Expect(state).ToHaveTextAsync($"{Krok1} · skluz +33 dní");

            await PlanField(page, 1).Locator("[data-app-date-clear]").DispatchEventAsync("click");

            await Expect(state).ToHaveTextAsync($"{Krok1} · plán není zadán · celkový termín: zbývá 60 dní");
            await Expect(statusLine).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("\\bok\\b"));

            var day = await OpenCalendarAtAsync(page, PlanField(page, 1), zalozeni.AddDays(10));
            await Expect(day).ToBeEnabledAsync();
            await day.DispatchEventAsync("click");

            await Expect(PlanField(page, 1).Locator("input[data-app-date-value]")).ToHaveValueAsync($"{zalozeni.AddDays(10):yyyy-MM-dd}");
            await Expect(state).ToHaveTextAsync($"{Krok1} · skluz +30 dní");
            await Expect(statusLine).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("\\blate\\b"));
        }
        finally
        {
            await page.Context.CloseAsync();
            await CleanupAsync(recordId);
        }
    }

    [Fact]
    public async Task Editor_PlanCalendar_BoundsSkipStepWithoutPlan_AndFollowChanges()
    {
        var (recordId, zalozeni) = await SeedAsync(bezPlanu: 2);
        var page = await OpenEditorScheduleAsync(recordId);
        try
        {
            var plan3 = PlanField(page, 3);
            await Expect(plan3).ToHaveAttributeAsync("min", $"{zalozeni.AddDays(7):yyyy-MM-dd}");
            await Expect(plan3).ToHaveAttributeAsync("max", $"{zalozeni.AddDays(28):yyyy-MM-dd}");
            (await PlanField(page, 1).GetAttributeAsync("min")).Should().BeNull("krok 1 nemá dřívější krok");

            var beforePlan1 = await OpenCalendarAtAsync(page, plan3, zalozeni.AddDays(6));
            await Expect(beforePlan1).ToBeDisabledAsync();
            await Expect(page.Locator($"[data-app-date-panel]:not([hidden]) button.app-date-day[data-iso='{zalozeni.AddDays(7):yyyy-MM-dd}']"))
                .ToBeEnabledAsync();
            await page.Keyboard.PressAsync("Escape");

            await PlanField(page, 4).Locator("[data-app-date-clear]").DispatchEventAsync("click");

            await Expect(plan3).ToHaveAttributeAsync("max", $"{zalozeni.AddDays(35):yyyy-MM-dd}");
        }
        finally
        {
            await page.Context.CloseAsync();
            await CleanupAsync(recordId);
        }
    }
}
```

Doplnit do `ScheduleStatusTextSourceTests` druhý řádek dat:

```csharp
    [InlineData("PmTracker.Web/wwwroot/js/modules/schedule/block.js")]
```

- [ ] **Step 3: Spustit — musí selhat**

Run: `node --test tests/js/schedule/planChronology.test.js > $WS/t5j.log 2>&1; tail -6 $WS/t5j.log`
Expected: FAIL — `ERR_MODULE_NOT_FOUND … planChronology.js`.

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ScheduleStatusTextSourceTests" > $WS/t5s.log 2>&1; tail -6 $WS/t5s.log`
Expected: 1 selhání (`block.js` obsahuje `Aktuální krok:` a `skluz +`).

Run: `dotnet build PmTracker.Tests.E2E > $WS/t5b.log 2>&1 && dotnet test PmTracker.Tests.E2E --no-build --filter "FullyQualifiedName~ScheduleLiveRecalcScenariosTests" > $WS/t5e.log 2>&1; tail -25 $WS/t5e.log`
Expected: 2 selhání — stav zůstane `…· skluz +33 dní` po ✕ (přepočet nefunguje); `plan3` nemá atribut `min`.

- [ ] **Step 4: Implementace — `planChronology.js`**

```js
/**
 * Spec 2026-10-24 R10: meze kalendáře u plánu kroku (datum-model). Zrcadlo serverového
 * ScheduleChronologyValidator: plán kroku ≥ plán nejbližšího dřívějšího kroku s plánem;
 * shora ho omezuje plán nejbližšího pozdějšího kroku s plánem. Kroky bez plánu se přeskakují,
 * stejný den je povolený (date.js vypíná jen dny ostře mimo meze).
 */

/**
 * @param {Array<string|null>} plans plán kroků v pořadí (yyyy-MM-dd; "" nebo null = bez plánu)
 * @returns {Array<{min: string|null, max: string|null}>}
 */
export function planBounds(plans) {
    return plans.map((_, index) => ({
        min: lastFilled(plans.slice(0, index)),
        max: firstFilled(plans.slice(index + 1))
    }));
}

function firstFilled(values) {
    return values.find((value) => Boolean(value)) || null;
}

function lastFilled(values) {
    return firstFilled([...values].reverse());
}
```

- [ ] **Step 5: Implementace — `block.js`**

Importy (za ř. 17):

```js
import { formatPlanCompletion, formatScheduleState, formatTotalDuration, summarizeSchedule } from "./scheduleStatus.js";
import { planBounds } from "../harmonogram/planChronology.js";
```

Řádek editoru — v mapě `editorRows` nahradit ř. 150–151 (`dateInput`, `delayDateInput`):

```js
                    // Spec 2026-10-24: skryté pole plánu uvnitř <pm-date-field>. Selektor
                    // [data-schedule-duration-calendar] trefil obal (nese stejný atribut a je v DOM
                    // dřív), takže meze kalendáře ani přepočet po změně data nikdy nefungovaly.
                    planInput: row.querySelector('input[name$=".PlanDatum"]'),
```

Smazat metody `setDateInputValue` (ř. 205–220), `recalcFromDate` a `recalcFromDelayDate` (ř. 310–316) a `renderEditorRows` (ř. 536–558) — jediné, co je volalo, byly odstraněné posluchače / nikdo; odkazovaly na obal pole.

`renderSummary` (ř. 368–405) nahradit:

```js
    renderSummary(state, startDate, deadlineDate) {
        // Spec 2026-10-24: Stav, Plán dokončení i Trvání ze sdíleného výpočtu scheduleStatus.js —
        // zrcadlo serverového ScheduleDateCalculator.Summarize + ScheduleStatusText (golden vektory).
        const souhrn = summarizeSchedule({
            startIso: formatIsoDate(startDate),
            terminIso: formatIsoDate(deadlineDate),
            todayIso: formatIsoDate(resolveToday(this.root)),
            steps: state.map((item) => ({
                poradi: item.stepIndex,
                nazev: item.name,
                planIso: item.planIso,
                skutecnostIso: item.actualIso
            }))
        });

        if (this.summaryDeadline instanceof HTMLElement) {
            this.summaryDeadline.textContent = formatDisplayDate(deadlineDate);
        }
        if (this.summaryBaseline instanceof HTMLElement) {
            this.summaryBaseline.textContent = formatPlanCompletion(souhrn.planoveDokonceniIso);
        }
        if (this.summaryDuration instanceof HTMLElement) {
            this.summaryDuration.textContent = formatTotalDuration(souhrn.planoveDokonceniIso, souhrn.celkoveTrvaniDni);
        }
        if (this.summaryState instanceof HTMLElement) {
            this.summaryState.textContent = formatScheduleState(souhrn);
        }
        if (this.statusLine instanceof HTMLElement) {
            this.statusLine.classList.toggle("ok", souhrn.stihame);
            this.statusLine.classList.toggle("late", !souhrn.stihame);
        }
    }
```

`recalcAll` (ř. 591–602):

```js
    recalcAll() {
        const { plan, actual, state, startDate } = this.computeDateModel();
        if (state.length === 0) {
            return;
        }
        const deadlineDate = this.getDeadlineDate(startDate);
        this.renderSummary(state, startDate, deadlineDate);
        this.renderOverview(plan, actual, state, startDate, deadlineDate);
        this.renderBreakdown(plan, actual, state, startDate, deadlineDate);
        this.applyPlanChronologyBounds();
        queueRainbowSegmentRender(this.root);
    }
```

`applyPlanChronologyBounds` (ř. 604–620):

```js
    // Spec 2026-10-24 R10: meze kalendáře plánu = nejbližší kroky s plánem (pravidlo serveru
    // ScheduleChronologyValidator; kroky bez plánu se přeskakují). Nastaví min/max na host
    // <pm-date-field>, kalendář dny mimo meze vypne (date.js).
    applyPlanChronologyBounds() {
        const plans = this.editorRows.map((entry) =>
            entry.planInput instanceof HTMLInputElement ? entry.planInput.value.trim() : "");
        const bounds = planBounds(plans);
        this.editorRows.forEach((entry, index) => {
            const host = entry.planInput instanceof HTMLInputElement ? entry.planInput.closest("pm-date-field") : null;
            if (!host) {
                return;
            }
            const { min, max } = bounds[index];
            if (min) host.setAttribute("min", min); else host.removeAttribute("min");
            if (max) host.setAttribute("max", max); else host.removeAttribute("max");
        });
    }
```

`computeDateModel` — ve `forEach` nahradit `const planEndRaw = parseIsoDate(this.rowDateIso(row, "PlanDatum"));` za

```js
            const planIso = this.rowDateIso(row, "PlanDatum");
            const planEndRaw = parseIsoDate(planIso);
```

do `state.push({ … })` přidat

```js
                planIso,
                actualIso: actualDate ? actualIso : null,
```

a návrat změnit na `return { plan, actual, state, startDate };` (aktuální krok si souhrn určí sám; `aktualniIndex` zůstává jen lokálně pro pruh do dneška).

`bind` — smazat blok ř. 826–831 (posluchače `dateInput` / `delayDateInput`) a za `this.editorRows.forEach(…)` přidat:

```js
        // Spec 2026-10-24 R9: výběr data i ✕ u plánu nebo skutečnosti přepočítá souhrn a graf hned.
        // date.js posílá „change“ ze skrytého pole uvnitř <pm-date-field>; delegace z bloku ho chytí
        // u všech řádků (posluchač na obalu pole se nikdy nespustil).
        this.root.addEventListener("change", (event) => {
            const target = event.target;
            if (target instanceof HTMLInputElement && /\.(PlanDatum|SkutecnostDatum)$/.test(target.name)) {
                this.recalcAll();
            }
        });
```

Grep kontrola: `grep -n "dateInput\|delayDateInput\|setDateInputValue\|renderEditorRows\|recalcFromDate\|aktualniIndex)" PmTracker.Web/wwwroot/js/modules/schedule/block.js` → zbývá jen lokální `aktualniIndex` v `computeDateModel`; `grep -rn "renderEditorRows\|recalcFromDate" PmTracker.Web/wwwroot/js` → jen komentář v `recordEditor/draft.js` (historický, beze změny).

- [ ] **Step 6: Spustit — musí projít**

Run: `npm test > $WS/t5j.log 2>&1; tail -6 $WS/t5j.log`
Expected: `# fail 0`.

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ScheduleStatusTextSourceTests|FullyQualifiedName~ScheduleJsSplitTests" > $WS/t5s.log 2>&1; tail -5 $WS/t5s.log`
Expected: `Passed!`.

Run: `dotnet build PmTracker.Tests.E2E > $WS/t5b.log 2>&1 && dotnet test PmTracker.Tests.E2E --no-build --filter "FullyQualifiedName~ScheduleLiveRecalcScenariosTests|FullyQualifiedName~PlanDateClearScenariosTests" > $WS/t5e.log 2>&1; tail -6 $WS/t5e.log`
Expected: `Passed!  - Failed: 0, Passed: 3`.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/wwwroot/js/modules/harmonogram/planChronology.js PmTracker.Web/wwwroot/js/modules/schedule/block.js tests/js/schedule/planChronology.test.js PmTracker.Tests.Unit/Architecture/ScheduleStatusTextSourceTests.cs PmTracker.Tests.E2E/Scenarios/ScheduleLiveRecalcScenariosTests.cs
git commit -m "fix(harmonogram): editor se po výběru data i ✕ hned přepočítá, kalendář hlídá pořadí plánů jako server (spec 2026-10-24 R9, R10)"
```

---

### Task 6: Dokumentace, plné sady, průchod v prohlížeči za všechny role, merge

**Files:**
- Modify: `docs/specs/harmonogram-plan-vs-skutecnost.md` (kapitola „Sjednocený stav“, „Pravidla pro datumy“, „Hraniční případy“)
- Modify: `docs/k-otestovani.md` (nová sekce 7)
- Modify: `docs/superpowers/specs/2026-10-24-harmonogram-stav-bez-planu-design.md` (řádek Stav)

- [ ] **Step 1: Spec harmonogramu** — v kapitole „Sjednocený stav“ za odrážku „**Všechny kroky vyplněné** …“ přidat:

```markdown
- **Aktuální krok bez vlastního plánu** (spec 2026-10-24) → překročení = **dnes − celkový termín
  záznamu** a text to řekne: „Aktuální krok: <název> · plán není zadán · celkový termín: zbývá X dní“
  (resp. „skluz +X dní“, „je dnes“). Dopočtený konec plánu (konec předchozího plánu nebo datum
  založení) se pro skluz nepoužívá. Štítek Stíháme/Nestíháme a barva se řídí stejným číslem.
- Počet dní se skloňuje: 1 den, 2–4 dny, 5 a víc dní.
- **Plán dokončení** bez jediného plánovaného kroku = „není zadán“, trvání „–“.
```

V „Pravidla pro datumy“ za odrážku **Plán** přidat: „UI: kalendář vypne dny před plánem nejbližšího dřívějšího a po plánu nejbližšího pozdějšího kroku s plánem (`harmonogram/planChronology.js`).“ V tabulce „Hraniční případy“ změnit řádek „Žádný krok vyplněn“ na „Aktuální krok = 1; překročení = dnes − plán(1), bez plánu kroku 1 dnes − termín.“ a přidat řádek „Aktuální krok bez plánu | Překročení = dnes − termín; „plán není zadán · celkový termín…“.“

- [ ] **Step 2: Plné sady**

Run: `dotnet test PmTracker.Tests.Unit > $WS/full-unit.log 2>&1; tail -3 $WS/full-unit.log` — Expected: `Failed: 0` (2272 + nové).
Run: `dotnet test PmTracker.Tests.Api > $WS/full-api.log 2>&1; tail -3 $WS/full-api.log` — Expected: `Failed: 0` (532 + 4).
Run: `npm test > $WS/full-js.log 2>&1; tail -4 $WS/full-js.log` — Expected: `# fail 0` (56 + 22).
Run: `dotnet test PmTracker.Tests.Integration > $WS/full-int.log 2>&1; tail -3 $WS/full-int.log` — Expected: jediné selhání `ProposalRejectAndTakeOver` (známé).
Run: celé E2E (`dotnet test PmTracker.Tests.E2E --logger "trx;LogFileName=$WS/e2e-branch.trx"`) a porovnat jména selhání Pythonem proti 32 baseline (`project_e2e_baseline_failures`) — Expected: žádné nové jméno, 2 nové testy prošly.

- [ ] **Step 3: Průchod v prohlížeči za všechny role** (lokální publish větve na :5071, dev DB se zálohou `BACKUP DATABASE … pmtracker_pre_pruchod_stav_20261024.bak`):
  - Dočasné osoby 13 rolí podle `reference_all_roles_browser_walkthrough` (seed/cleanup jako `pruchod3-seed.sql` / `pruchod3-cleanup.sql` ve scratchpadu; bez plánu 99009 — nechat NULL).
  - Uživatelův případ na 99009 (projekt 1, bez plánu): dočasně skutečnost kroků 1/3/4 (`skutecnost_zdroj = 1`, Automat) a termín +12 dní; po průchodu vrátit (NULL, původní termín).
  - Každá role: stránka záznamu 99009 (stav „… · plán není zadán · celkový termín: zbývá 12 dní“, Plán „není zadán“, Rozpad krok 1 „bez plánu“, štítek Stíháme), záložka Harmonogram projektu 1; editor (role s právem): stav, „není zadán“, „–“, ✕/výběr data → živý přepočet, meze (krok 2 bez plánu přeskočen), změna termínu → text se přepočítá; detail návrhu od vedoucího se smazaným plánem aktuálního kroku → „plán není zadán“; role bez práva: editor odepřen (dev 500 Forbid), čtení ano; bez role 404.
  - Záznam 300 (plány i skutečnosti): stav k plánu, skloňování; ručně přehodit plán (Review Focus 5) jen v editoru bez uložení — JS bez chyby.
  - Světlý i tmavý motiv, 1400 a 375 px; konzole bez chyb. Snímky do scratchpadu.
  - Úklid podle reference (včetně `zaznam_priority_uzivatelu` a FK na `osoby`), ověřit dotazy, že stav DB = před průchodem.

- [ ] **Step 4: `docs/k-otestovani.md`** — sekce „## 7. Harmonogram — stav u kroku bez plánu, živý přepočet editoru“: co je hotové (R2–R10 česky), ověření (matice rolí z průchodu), ruční checklist (uživatelův případ: záznam bez plánu s externí vazbou → stav k celkovému termínu; ✕ u plánu aktuálního kroku v editoru → text hned; kalendář nedovolí plán mimo pořadí; skloňování 1/3/5 dní; štítek), známá omezení (dashboard počítá postaru; detail návrhu ze serveru ukazuje souhrn záznamu, JS ho přepočítá).

- [ ] **Step 5: Commit, merge, úklid**

```bash
git add docs/specs/harmonogram-plan-vs-skutecnost.md docs/k-otestovani.md docs/superpowers/specs/2026-10-24-harmonogram-stav-bez-planu-design.md
git commit -m "docs(k-otestovani,spec): harmonogram — stav u kroku bez plánu, živý přepočet editoru"
git checkout main && git merge --no-ff feat/harmonogram-stav-bez-planu -m "Merge feat/harmonogram-stav-bez-planu into main"
```

Doplnit merge hash do sekce 7 (`docs(k-otestovani): merge hash — …`), smazat větev, aktualizovat paměť `project_pre_existing_test_failures` (nové počty), zastavit dev aplikace, smazat workspace plánu. Nepushovat.
