# NES odpojení + 4 datumy auto-fill + krok 1 MIN — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Odpojit NES vazby od harmonogramového stepperu, přidat auto-fill 4 datumů (`DatumObjednani`, `PlanDodani`, `DatumDodani`, `DatumPrevzeti`) na kartě externí vazby s rozdílnými pravidly per typ tiketu, a doplnit krok 1 PMP/PNF jako MIN agregaci `HOT_ZAZNAMY.datum`.

**Architecture:** Rozšíření existující harvest pipeline (`VyjadreniHarvestService`) o paralelní cestu „extract & write metadata" (sloupce už existují v DB, žádná migrace). NES vazby přeskakují stepper bindings. Synthetic K1 binding pro PMP/PNF z `HOT_ZAZNAMY.datum`. Resolver dostává hardcoded MIN výjimku pro krok 1.

**Tech Stack:** .NET 8, EF Core 8, xUnit + FluentAssertions, existing harvest pipeline + Razor partials.

**Spec source:** [docs/superpowers/specs/2026-04-28-nes-vyjadreni-a-4-datumy-design.md](../specs/2026-04-28-nes-vyjadreni-a-4-datumy-design.md)

---

## File Structure

### Nové soubory

| Soubor | Odpovědnost |
|---|---|
| `PmTracker.Web/Services/ServiceDesk/PerTicketMetadataExtractor.cs` | Pure logic: ze seznamu vyjádření tiketu + typu + sla_deadline → 4 datumy |
| `PmTracker.Web/Services/ServiceDesk/PerTicketMetadataSyncService.cs` | DB orchestrace: zapíše 4 datumy do `ZaznamExterniOdkazEntity` |
| `PmTracker.Tests.Unit/ServiceDesk/PerTicketMetadataExtractorTests.cs` | Unit testy (NES, PMP, PNF, dual-phrase, regex datumu, null tolerance) |
| `PmTracker.Tests.Unit/ServiceDesk/PerTicketMetadataSyncServiceTests.cs` | Unit testy (DB write, overwrite logika) |

### Modifikované soubory

| Soubor | Změna |
|---|---|
| `PmTracker.Web/Services/ServiceDesk/HarvestPredicates.cs` | Nové enum hodnoty `NES_DatumObjednani`, `NES_DatumDodani` + LIKE patterns |
| `PmTracker.Web/Services/Schedules/HarmonogramKrokDatumMapping.cs` | Přidat `[1] = PredikatK1` do `PMP` i `PNF` map |
| `PmTracker.Web/Services/Schedules/HarmonogramSkutecnostResolver.cs` | Hardcoded MIN výjimka pro `krokPoradi == 1` |
| `PmTracker.Web/Services/ServiceDesk/VyjadreniHarvestService.cs` | Volat `PerTicketMetadataSyncService` po harvestu, NES = skip stepper bindings, synthetic K1 binding pro PMP/PNF |
| `PmTracker.ServiceDesk.Sql/SqlVyjadreniQueryService.cs` | Nové query: dual-phrase PlanDodani validation |
| `PmTracker.ServiceDesk.Contracts/HotZaznamFingerprintDto.cs` | Přidat `SlaDeadline` field |
| Razor partial modalu „Vyjádření a termíny" | Conditional: skrýt stepper pro NES (`eo.TypZaznamu == "NES"`) |
| `PmTracker.Tests.Unit/ServiceDesk/HarvestPredicatesTests.cs` | Rozšířit o NES predikáty |
| `PmTracker.Tests.Unit/Schedule/HarmonogramKrokDatumMappingTests.cs` | Rozšířit o krok 1 PMP/PNF mapping |
| `PmTracker.Tests.Unit/Schedule/HarmonogramSkutecnostResolverTests.cs` | Test krok 1 MIN |

### Bez DB migrace

Sloupce `datum_objednani`, `plan_dodani`, `datum_dodani`, `datum_prevzeti` na `zaznam_externi_odkazy` již existují.

---

## Tasks

### Task 1: NES predikáty v `HarvestPredicates`

**Files:**
- Modify: `PmTracker.Web/Services/ServiceDesk/HarvestPredicates.cs`
- Modify: `PmTracker.Tests.Unit/ServiceDesk/HarvestPredicatesTests.cs`

- [ ] **Step 1: Přidat 2 nové enum hodnoty do `HarvestPredicateKind`**

V `HarvestPredicates.cs` na začátek za stávajících 5 hodnot:

```csharp
/// <summary>NES Datum objednání — „Záznam byl předán dodavateli k řešení." (kratší než K6, jen pro NES tickety).</summary>
NES_DatumObjednani,

/// <summary>NES Datum dodání — „Vazba na IMPLEMENTAČNÍ ZÁZNAM HOTLINE číslo …" (jen pro NES tickety).</summary>
NES_DatumDodani
```

- [ ] **Step 2: Přidat 2 nové fráze do private const sekce**

```csharp
private const string PhraseNesObjednani = "Záznam byl předán dodavateli k řešení.";
private const string PhraseNesDodani = "Vazba na IMPLEMENTAČNÍ ZÁZNAM HOTLINE číslo";
```

- [ ] **Step 3: Rozšířit `GetSqlLikePattern` switch**

```csharp
HarvestPredicateKind.NES_DatumObjednani => $"%{PhraseNesObjednani}%",
HarvestPredicateKind.NES_DatumDodani => $"%{PhraseNesDodani}%",
```

- [ ] **Step 4: Pozn. ke `ClassifyPopis`**

`ClassifyPopis` se používá pro klasifikaci na harmonogramové kroky (PMP/PNF). NES predikáty NEvedou na harmonogram krok — používají se přímo v `PerTicketMetadataExtractor`. Tj. `ClassifyPopis` switch **neměníme** — přidáváme novou metodu pro NES klasifikaci:

```csharp
/// <summary>
/// NES-specific klasifikace. Vrací jeden z NES_DatumObjednani / NES_DatumDodani / K10 / None.
/// K10 fráze se sdílí s PMP/PNF, ostatní jsou výhradně NES.
/// PlanDodani pro NES jde z HOT_ZAZNAMY.sla_deadline, ne z popisu — tady ho nedetekujeme.
/// </summary>
public static HarvestPredicateKind ClassifyPopisForNes(string? popis)
{
    if (string.IsNullOrWhiteSpace(popis)) return HarvestPredicateKind.None;

    if (Contains(popis, PhraseK10)) return HarvestPredicateKind.K10_NasazeniArchivace;
    if (Contains(popis, PhraseNesDodani)) return HarvestPredicateKind.NES_DatumDodani;
    if (Contains(popis, PhraseNesObjednani)) return HarvestPredicateKind.NES_DatumObjednani;

    return HarvestPredicateKind.None;
}
```

- [ ] **Step 5: Napsat failing tests**

Přidat na konec `HarvestPredicatesTests.cs`:

```csharp
[Fact]
public void ClassifyPopisForNes_NesObjednaniFraze_VraciNesDatumObjednani()
{
    var result = HarvestPredicates.ClassifyPopisForNes("Dnes 28.4.2026: Záznam byl předán dodavateli k řešení.");
    result.Should().Be(HarvestPredicateKind.NES_DatumObjednani);
}

[Fact]
public void ClassifyPopisForNes_NesDodaniFraze_VraciNesDatumDodani()
{
    var result = HarvestPredicates.ClassifyPopisForNes("Vazba na IMPLEMENTAČNÍ ZÁZNAM HOTLINE číslo 123456 byla vytvořena.");
    result.Should().Be(HarvestPredicateKind.NES_DatumDodani);
}

[Fact]
public void ClassifyPopisForNes_K10Fraze_VraciK10()
{
    var result = HarvestPredicates.ClassifyPopisForNes("Záznam byl převeden do archivu.");
    result.Should().Be(HarvestPredicateKind.K10_NasazeniArchivace);
}

[Fact]
public void ClassifyPopisForNes_PrazdnyPopis_VraciNone()
{
    HarvestPredicates.ClassifyPopisForNes("").Should().Be(HarvestPredicateKind.None);
    HarvestPredicates.ClassifyPopisForNes(null).Should().Be(HarvestPredicateKind.None);
    HarvestPredicates.ClassifyPopisForNes("Něco jiného.").Should().Be(HarvestPredicateKind.None);
}

[Fact]
public void GetSqlLikePattern_NesPredikaty_VraciKorektniLike()
{
    HarvestPredicates.GetSqlLikePattern(HarvestPredicateKind.NES_DatumObjednani)
        .Should().Be("%Záznam byl předán dodavateli k řešení.%");
    HarvestPredicates.GetSqlLikePattern(HarvestPredicateKind.NES_DatumDodani)
        .Should().Be("%Vazba na IMPLEMENTAČNÍ ZÁZNAM HOTLINE číslo%");
}
```

- [ ] **Step 6: Run tests — expect PASS**

```bash
dotnet test PmTracker.Tests.Unit -c Release --filter "HarvestPredicates" --no-restore
```

Expected: všechny existující testy + 5 nových PASS.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Services/ServiceDesk/HarvestPredicates.cs \
        PmTracker.Tests.Unit/ServiceDesk/HarvestPredicatesTests.cs
git commit -m "feat(harvest): NES predikáty (NES_DatumObjednani, NES_DatumDodani) + ClassifyPopisForNes"
```

---

### Task 2: `SlaDeadline` ve `HotZaznamFingerprintDto` + dual-phrase SQL query

**Files:**
- Modify: `PmTracker.ServiceDesk.Contracts/HotZaznamFingerprintDto.cs` (nebo kde je definice)
- Modify: `PmTracker.ServiceDesk.Sql/SqlVyjadreniQueryService.cs`
- Modify: `PmTracker.Tests.Unit/ServiceDesk/SqlVyjadreniQueryServiceTests.cs`

- [ ] **Step 1: Najít definici `HotZaznamFingerprintDto`**

```bash
grep -rn "record HotZaznamFingerprintDto\|class HotZaznamFingerprintDto" "/Users/Pavel.Andrlik/Documents/PM Tracker"
```

- [ ] **Step 2: Přidat `DateTime? SlaDeadline` do DTO**

Pokud DTO je `record`:

```csharp
public sealed record HotZaznamFingerprintDto(
    string Cislo,
    DateTime Datum,
    string? TypZaznamu,
    string? Stav,
    DateTime? SlaDeadline);   // NEW
```

Pokud existuje volání factory (např. `new HotZaznamFingerprintDto(cislo, datum, typ, stav)`), zachovat backward compat přidáním optional parameteru s default null.

- [ ] **Step 3: Rozšířit SQL query v `SqlVyjadreniQueryService.GetHotZaznamFingerprintsAsync`**

Najít metodu a v SELECT přidat sloupec:

```sql
SELECT id, datum, typ_zaznamu, stav, sla_deadline
FROM dbo.HOT_ZAZNAMY
WHERE id IN (...)
```

V mapping kódu `Read()`:

```csharp
SlaDeadline: reader.IsDBNull(4) ? null : reader.GetDateTime(4)
```

(Index dle pořadí sloupce.)

- [ ] **Step 4: Přidat novou query metodu `GetVyjadreniWithDualPhraseValidationAsync`**

V `IVyjadreniQueryService` interface:

```csharp
/// <summary>
/// Pro PMP/PNF tickety: vyhledá vyjádření obsahující PlanDodani frázi
/// (predal záznam dodavateli + s termínem plnění dodavatele) a páruje je s následnými
/// vyjádřeními n+1, n+2 obsahujícími K6 frázi (Kalkulace byla akceptována).
///
/// Vrací jen ty kandidáty, kde validace prošla — n+1 nebo n+2 obsahuje K6.
/// </summary>
Task<IReadOnlyList<HotVyjadreniDto>> GetPlanDodaniValidatedVyjadreniAsync(
    string cislo, CancellationToken ct);
```

V `SqlVyjadreniQueryService` implementace:

```csharp
public async Task<IReadOnlyList<HotVyjadreniDto>> GetPlanDodaniValidatedVyjadreniAsync(
    string cislo, CancellationToken ct)
{
    // Načti všechna vyjádření tiketu chronologicky.
    var sql = @"
        SELECT id, pid, datum, popis
        FROM dbo.HOT_VYJADRENI
        WHERE pid = (SELECT pid FROM dbo.HOT_ZAZNAMY WHERE id = @cislo)
        ORDER BY datum ASC, id ASC";

    var all = await ExecuteVyjadreniQueryAsync(sql, new[] { ("@cislo", (object)cislo) }, ct)
        .ConfigureAwait(false);

    if (all.Count == 0) return Array.Empty<HotVyjadreniDto>();

    var result = new List<HotVyjadreniDto>();
    for (int i = 0; i < all.Count; i++)
    {
        var v = all[i];
        if (v.Popis is null) continue;
        if (!v.Popis.Contains("předal záznam dodavateli :", StringComparison.OrdinalIgnoreCase)
            || !v.Popis.Contains("s termínem plnění dodavatele", StringComparison.OrdinalIgnoreCase))
            continue;

        // n+1 nebo n+2 musí obsahovat K6
        bool validated = false;
        for (int j = i + 1; j <= i + 2 && j < all.Count; j++)
        {
            if (all[j].Popis?.Contains(
                    "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována.",
                    StringComparison.OrdinalIgnoreCase) == true)
            {
                validated = true;
                break;
            }
        }

        if (validated) result.Add(v);
    }

    return result;
}
```

(Pokud `ExecuteVyjadreniQueryAsync` neexistuje, použít EF Core / ADO.NET vzor existující v souboru.)

Také aktualizovat `DisabledVyjadreniQueryService` a `CachingVyjadreniQueryService` (no-op pro disabled, no-cache pro caching).

- [ ] **Step 5: Napsat unit test pro dual-phrase logiku**

V `SqlVyjadreniQueryServiceTests.cs` přidat test (viz pattern v souboru — pravděpodobně mockuje `IDbConnection` nebo používá in-memory):

```csharp
[Fact]
public async Task GetPlanDodaniValidatedVyjadreniAsync_KalkulaceVNplus1_ValidaceProjde()
{
    // Pseudo-test concept (přesný pattern dle existujícího souboru):
    // Seed 3 vyjádření v 1 tiketu:
    //   v0 (datum=1.4): popis = "PM předal záznam dodavateli : XYZ s termínem plnění dodavatele 15.4.2026"
    //   v1 (datum=2.4): popis = "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována."
    //   v2 (datum=3.4): popis = "Něco jiného"
    // Expected: vrátí v0 (validace prošla v n+1 = v1)
    // ...
}
```

**Pozn.:** Přesné pattern testu (Testcontainers / in-memory / mock) zjistit z existujícího `SqlVyjadreniQueryServiceTests.cs` — replikovat. Pokud test přesahuje rozsah nebo vyžaduje fyzickou DB, **přesunout** logiku dual-phrase validace do pure-logic třídy (`PlanDodaniDualPhraseValidator`) v `PmTracker.Web/Services/ServiceDesk/` a testovat ji izolovaně. Doporučená cesta: pure-logic + DB query vrací jen seznam vyjádření, validace v memory.

- [ ] **Step 6: Lepší alternativa — pure logic + jednoduchý DB fetch**

Místo komplikované SQL query oddělit:

```csharp
// V SqlVyjadreniQueryService — jen primitive fetch, žádná dual-phrase logika v DB:
public async Task<IReadOnlyList<HotVyjadreniDto>> GetAllVyjadreniForTicketAsync(
    string cislo, CancellationToken ct);

// V PmTracker.Web/Services/ServiceDesk/PlanDodaniDualPhraseValidator.cs:
public static class PlanDodaniDualPhraseValidator
{
    public static IReadOnlyList<HotVyjadreniDto> FilterValidated(IReadOnlyList<HotVyjadreniDto> chronological)
    {
        // Same logic as Step 4 above, ale pure-in-memory.
    }
}
```

Tím se test pure logiky stane triviální xUnit test bez DB.

**Doporučení**: jít touto cestou (Step 6). Testy v Step 5 pak přesunout do nového `PlanDodaniDualPhraseValidatorTests.cs`.

- [ ] **Step 7: Build + run tests**

```bash
dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore
dotnet test PmTracker.Tests.Unit -c Release --filter "PlanDodani|HotZaznamFingerprint" --no-restore
```

- [ ] **Step 8: Commit**

```bash
git add PmTracker.ServiceDesk.Contracts \
        PmTracker.ServiceDesk.Sql/SqlVyjadreniQueryService.cs \
        PmTracker.Web/Services/ServiceDesk/PlanDodaniDualPhraseValidator.cs \
        PmTracker.Tests.Unit/ServiceDesk
git commit -m "feat(harvest): dual-phrase PlanDodani validátor + SlaDeadline na fingerprintu"
```

---

### Task 3: `PerTicketMetadataExtractor` (pure logic)

**Files:**
- Create: `PmTracker.Web/Services/ServiceDesk/PerTicketMetadataExtractor.cs`
- Create: `PmTracker.Tests.Unit/ServiceDesk/PerTicketMetadataExtractorTests.cs`

- [ ] **Step 1: Definice typu `PerTicketMetadata`**

```csharp
namespace PmTracker.Web.Services.ServiceDesk;

public sealed record PerTicketMetadata(
    DateTime? DatumObjednani,
    DateTime? PlanDodani,
    DateTime? DatumDodani,
    DateTime? DatumPrevzeti);
```

- [ ] **Step 2: Implementace extractoru — common helpers**

```csharp
using System.Text.RegularExpressions;
using PmTracker.ServiceDesk.Contracts;

namespace PmTracker.Web.Services.ServiceDesk;

public static class PerTicketMetadataExtractor
{
    private static readonly Regex DatumRegex = new(
        @"\b(\d{1,2})\.(\d{1,2})\.(\d{4})\b",
        RegexOptions.Compiled);

    public static PerTicketMetadata Extract(
        string typZaznamu,
        DateTime? slaDeadline,
        IReadOnlyList<HotVyjadreniDto> chronologicalVyjadreni)
    {
        var typ = (typZaznamu ?? string.Empty).Trim().ToUpperInvariant();
        return typ switch
        {
            "NES" => ExtractNes(slaDeadline, chronologicalVyjadreni),
            "PMP" or "PNF" => ExtractPmpPnf(chronologicalVyjadreni),
            _ => new PerTicketMetadata(null, null, null, null),
        };
    }

    private static PerTicketMetadata ExtractNes(
        DateTime? slaDeadline,
        IReadOnlyList<HotVyjadreniDto> all)
    {
        DateTime? datumObjednani = null;
        DateTime? datumDodani = null;
        DateTime? datumPrevzeti = null;

        foreach (var v in all)
        {
            var kind = HarvestPredicates.ClassifyPopisForNes(v.Popis);
            switch (kind)
            {
                case HarvestPredicateKind.NES_DatumObjednani:
                    datumObjednani ??= v.Datum;  // první výskyt (ASC chronological)
                    break;
                case HarvestPredicateKind.NES_DatumDodani:
                    datumDodani = v.Datum;       // poslední výskyt (DESC last)
                    break;
                case HarvestPredicateKind.K10_NasazeniArchivace:
                    datumPrevzeti = v.Datum;     // jediný (poslední vyhrává)
                    break;
            }
        }

        // PlanDodani pro NES = HOT_ZAZNAMY.sla_deadline (DateTime? → date)
        var planDodani = slaDeadline?.Date;
        return new PerTicketMetadata(datumObjednani, planDodani, datumDodani, datumPrevzeti);
    }

    private static PerTicketMetadata ExtractPmpPnf(IReadOnlyList<HotVyjadreniDto> all)
    {
        DateTime? datumObjednani = null;
        DateTime? datumDodani = null;
        DateTime? datumPrevzeti = null;

        foreach (var v in all)
        {
            var kind = HarvestPredicates.ClassifyPopis(v.Popis);
            switch (kind)
            {
                case HarvestPredicateKind.K6_OdeslaniPozadavku:
                    datumObjednani ??= v.Datum;  // první výskyt
                    break;
                case HarvestPredicateKind.K4_K7_DodaniReseni:
                    datumDodani = v.Datum;       // poslední výskyt
                    break;
                case HarvestPredicateKind.K10_NasazeniArchivace:
                    datumPrevzeti = v.Datum;
                    break;
            }
        }

        // PlanDodani — dual-phrase validace + regex z textu
        var planDodaniValidated = PlanDodaniDualPhraseValidator.FilterValidated(all);
        DateTime? planDodani = null;
        foreach (var v in planDodaniValidated)
        {
            var match = DatumRegex.Match(v.Popis ?? string.Empty);
            if (!match.Success) continue;
            if (int.TryParse(match.Groups[1].Value, out var d)
                && int.TryParse(match.Groups[2].Value, out var m)
                && int.TryParse(match.Groups[3].Value, out var y))
            {
                try
                {
                    var candidate = new DateTime(y, m, d);
                    planDodani = candidate;  // poslední validovaný vyhrává (validatedList je chronologically ASC)
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Skip invalid date (např. 32.13.2026)
                }
            }
        }

        return new PerTicketMetadata(datumObjednani, planDodani, datumDodani, datumPrevzeti);
    }
}
```

- [ ] **Step 3: Napsat tests (failing first)**

```csharp
using FluentAssertions;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Services.ServiceDesk;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class PerTicketMetadataExtractorTests
{
    private static HotVyjadreniDto V(long id, string datum, string popis)
        => new(id, "PID-1", DateTime.Parse(datum), popis, "25");

    [Fact]
    public void Extract_Nes_HappyPath_VratiVsechny4Datumy()
    {
        var all = new[]
        {
            V(1, "2026-01-01", "Záznam byl předán dodavateli k řešení."),
            V(2, "2026-01-15", "Nějaké zpracování."),
            V(3, "2026-02-01", "Vazba na IMPLEMENTAČNÍ ZÁZNAM HOTLINE číslo 123456 byla vytvořena."),
            V(4, "2026-03-01", "Záznam byl převeden do archivu."),
        };

        var sla = new DateTime(2026, 2, 28);
        var meta = PerTicketMetadataExtractor.Extract("NES", sla, all);

        meta.DatumObjednani.Should().Be(new DateTime(2026, 1, 1));
        meta.PlanDodani.Should().Be(new DateTime(2026, 2, 28));
        meta.DatumDodani.Should().Be(new DateTime(2026, 2, 1));
        meta.DatumPrevzeti.Should().Be(new DateTime(2026, 3, 1));
    }

    [Fact]
    public void Extract_Nes_VicNesDodani_VratiPosledni()
    {
        var all = new[]
        {
            V(1, "2026-01-01", "Vazba na IMPLEMENTAČNÍ ZÁZNAM HOTLINE číslo 100001 vytvořena."),
            V(2, "2026-02-01", "Vazba na IMPLEMENTAČNÍ ZÁZNAM HOTLINE číslo 100002 vytvořena."),
        };

        var meta = PerTicketMetadataExtractor.Extract("NES", null, all);
        meta.DatumDodani.Should().Be(new DateTime(2026, 2, 1));  // poslední (DESC)
        meta.PlanDodani.Should().BeNull();  // sla_deadline je null
    }

    [Fact]
    public void Extract_Nes_BezSlaDeadline_PlanDodaniNull()
    {
        var meta = PerTicketMetadataExtractor.Extract("NES", null, Array.Empty<HotVyjadreniDto>());
        meta.PlanDodani.Should().BeNull();
        meta.DatumObjednani.Should().BeNull();
        meta.DatumDodani.Should().BeNull();
        meta.DatumPrevzeti.Should().BeNull();
    }

    [Fact]
    public void Extract_Pmp_HappyPath_VratiVsechny4Datumy()
    {
        var all = new[]
        {
            V(1, "2026-01-01", "Předal záznam dodavateli : ABC s termínem plnění dodavatele 15.4.2026"),
            V(2, "2026-01-02", "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována."),
            V(3, "2026-02-01", "Dodavatel přidal řešení."),
            V(4, "2026-02-15", "Dodavatel přidal řešení."),  // poslední dodání vyhrává
            V(5, "2026-03-01", "Záznam byl převeden do archivu."),
        };

        var meta = PerTicketMetadataExtractor.Extract("PMP", null, all);

        meta.DatumObjednani.Should().Be(new DateTime(2026, 1, 2));   // K6 první výskyt
        meta.PlanDodani.Should().Be(new DateTime(2026, 4, 15));      // regex z PlanDodani textu
        meta.DatumDodani.Should().Be(new DateTime(2026, 2, 15));     // K4 poslední (DESC)
        meta.DatumPrevzeti.Should().Be(new DateTime(2026, 3, 1));    // K10 jediný
    }

    [Fact]
    public void Extract_Pmp_PlanDodaniBezKalkulace_VratiNull()
    {
        var all = new[]
        {
            V(1, "2026-01-01", "Předal záznam dodavateli : ABC s termínem plnění dodavatele 15.4.2026"),
            V(2, "2026-01-02", "Něco úplně jiného."),
            V(3, "2026-01-03", "A další něco jiného."),
            V(4, "2026-01-04", "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována."),  // n+3 — moc daleko
        };

        var meta = PerTicketMetadataExtractor.Extract("PMP", null, all);
        meta.PlanDodani.Should().BeNull();  // K6 je v n+3, validace selhala
    }

    [Fact]
    public void Extract_Pmp_PlanDodaniKalkulaceVNplus2_ValidaceProjde()
    {
        var all = new[]
        {
            V(1, "2026-01-01", "Předal záznam dodavateli : ABC s termínem plnění dodavatele 15.4.2026"),
            V(2, "2026-01-02", "Mezikrok."),
            V(3, "2026-01-03", "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována."),  // n+2
        };

        var meta = PerTicketMetadataExtractor.Extract("PMP", null, all);
        meta.PlanDodani.Should().Be(new DateTime(2026, 4, 15));
    }

    [Fact]
    public void Extract_Pnf_HappyPath_StejneJakoPmp()
    {
        // PNF používá stejné fráze pro 4 datumy jako PMP — jen typ rozhoduje (matrix kroku)
        var all = new[]
        {
            V(1, "2026-01-01", "Předal záznam dodavateli : ABC s termínem plnění dodavatele 30.5.2026"),
            V(2, "2026-01-02", "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována."),
            V(3, "2026-04-01", "Dodavatel přidal řešení."),
        };

        var meta = PerTicketMetadataExtractor.Extract("PNF", null, all);
        meta.DatumObjednani.Should().Be(new DateTime(2026, 1, 2));
        meta.PlanDodani.Should().Be(new DateTime(2026, 5, 30));
        meta.DatumDodani.Should().Be(new DateTime(2026, 4, 1));
        meta.DatumPrevzeti.Should().BeNull();
    }

    [Fact]
    public void Extract_NeznamyTyp_VratiVsechnyNull()
    {
        var meta = PerTicketMetadataExtractor.Extract("XYZ", null, Array.Empty<HotVyjadreniDto>());
        meta.DatumObjednani.Should().BeNull();
        meta.PlanDodani.Should().BeNull();
        meta.DatumDodani.Should().BeNull();
        meta.DatumPrevzeti.Should().BeNull();
    }

    [Fact]
    public void Extract_NullToleranceJednotlivychPoli()
    {
        var all = new[]
        {
            V(1, "2026-01-01", "Dodavatel přidal řešení."),  // jen K4
        };

        var meta = PerTicketMetadataExtractor.Extract("PMP", null, all);
        meta.DatumObjednani.Should().BeNull();
        meta.PlanDodani.Should().BeNull();
        meta.DatumDodani.Should().Be(new DateTime(2026, 1, 1));
        meta.DatumPrevzeti.Should().BeNull();
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

```bash
dotnet test PmTracker.Tests.Unit -c Release --filter "PerTicketMetadataExtractor" --no-restore
```

Expected: 9/9 PASS.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/ServiceDesk/PerTicketMetadataExtractor.cs \
        PmTracker.Tests.Unit/ServiceDesk/PerTicketMetadataExtractorTests.cs
git commit -m "feat(harvest): PerTicketMetadataExtractor pure-logic 4 datumy per typ"
```

---

### Task 4: `PerTicketMetadataSyncService` (DB write)

**Files:**
- Create: `PmTracker.Web/Services/ServiceDesk/PerTicketMetadataSyncService.cs`
- Create: `PmTracker.Tests.Unit/ServiceDesk/PerTicketMetadataSyncServiceTests.cs`
- Modify: `PmTracker.Web/Program.cs` (DI registration)

- [ ] **Step 1: Interface + impl**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.ServiceDesk;

public interface IPerTicketMetadataSyncService
{
    /// <summary>
    /// Pro daný externí odkaz: vyextrahuje 4 datumy a zapíše je do ZaznamExterniOdkazEntity.
    /// Přepíše dosavadní hodnoty (auto-fill je default, nerespektuje manuální zápis).
    /// </summary>
    Task SyncTicketAsync(int externiOdkazId, CancellationToken ct);
}

public sealed class PerTicketMetadataSyncService : IPerTicketMetadataSyncService
{
    private readonly PmTrackerDbContext _db;
    private readonly IVyjadreniQueryService _vyjadreni;
    private readonly ILogger<PerTicketMetadataSyncService> _logger;

    public PerTicketMetadataSyncService(
        PmTrackerDbContext db,
        IVyjadreniQueryService vyjadreni,
        ILogger<PerTicketMetadataSyncService> logger)
    {
        _db = db;
        _vyjadreni = vyjadreni;
        _logger = logger;
    }

    public async Task SyncTicketAsync(int externiOdkazId, CancellationToken ct)
    {
        var eo = await _db.ZaznamExterniOdkazy
            .FirstOrDefaultAsync(x => x.Id == externiOdkazId, ct).ConfigureAwait(false);
        if (eo is null || string.IsNullOrWhiteSpace(eo.Cislo)) return;

        // Get fingerprint pro typ a sla_deadline
        var fps = await _vyjadreni.GetHotZaznamFingerprintsAsync(new[] { eo.Cislo! }, ct).ConfigureAwait(false);
        if (!fps.TryGetValue(eo.Cislo!, out var fp)) return;

        var typ = fp.TypZaznamu ?? string.Empty;
        var sla = fp.SlaDeadline;

        // Fetch all vyjadreni chronologically
        var all = await _vyjadreni.GetVyjadreniForTicketAsync(eo.Cislo!, since: null, ct).ConfigureAwait(false);

        var meta = PerTicketMetadataExtractor.Extract(typ, sla, all);

        bool changed = false;
        if (eo.DatumObjednani != meta.DatumObjednani) { eo.DatumObjednani = meta.DatumObjednani; changed = true; }
        if (eo.PlanDodani != meta.PlanDodani) { eo.PlanDodani = meta.PlanDodani; changed = true; }
        if (eo.DatumDodani != meta.DatumDodani) { eo.DatumDodani = meta.DatumDodani; changed = true; }
        if (eo.DatumPrevzeti != meta.DatumPrevzeti) { eo.DatumPrevzeti = meta.DatumPrevzeti; changed = true; }

        if (changed)
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            _logger.LogInformation(
                "PerTicketMetadataSync: externí odkaz #{Id} (cislo={Cislo}, typ={Typ}) — 4 datumy aktualizovány.",
                externiOdkazId, eo.Cislo, typ);
        }
    }
}
```

- [ ] **Step 2: DI registrace**

V `PmTracker.Web/Program.cs` najít sekci s `AddScoped<IVyjadreniHarvestService, VyjadreniHarvestService>` a přidat:

```csharp
services.AddScoped<IPerTicketMetadataSyncService, PerTicketMetadataSyncService>();
```

- [ ] **Step 3: Unit testy (in-memory DB pattern z existujícího kódu)**

Replikovat pattern z existujícího `VyjadreniHarvestServiceTests.cs`. Test cases:

```csharp
[Fact]
public async Task SyncTicketAsync_NesTicket_ZapiseSlaDeadlineDoPlanDodani()
{
    // Arrange: in-memory db s ZaznamExterniOdkazEntity (Cislo=123456, ZaznamId=1, žádné datumy)
    // Mock IVyjadreniQueryService:
    //   - GetHotZaznamFingerprintsAsync vrátí typ=NES, sla_deadline=2026-04-30
    //   - GetVyjadreniForTicketAsync vrátí 1 vyjádření s "Záznam byl předán dodavateli k řešení."
    // Act: SyncTicketAsync(eo.Id, default)
    // Assert: eo.PlanDodani == 2026-04-30, eo.DatumObjednani == datum vyjádření
}

[Fact]
public async Task SyncTicketAsync_PrepiseExistujiciHodnoty()
{
    // Arrange: eo s DatumObjednani = pre-existing 2025-12-31
    // Auto-fill najde 2026-01-01.
    // Assert: eo.DatumObjednani == 2026-01-01 (overwrite).
}

[Fact]
public async Task SyncTicketAsync_ZadnaZmena_NevolaSaveChanges()
{
    // Arrange: eo už má correct datumy.
    // Act: SyncTicketAsync.
    // Assert: žádný DbUpdate (lze ověřit přes DB.ChangeTracker.Entries().Count() == 0).
}
```

(Přesný setup mock services dle pattern v `VyjadreniHarvestServiceTests.cs`.)

- [ ] **Step 4: Run tests**

```bash
dotnet test PmTracker.Tests.Unit -c Release --filter "PerTicketMetadataSyncService" --no-restore
```

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/ServiceDesk/PerTicketMetadataSyncService.cs \
        PmTracker.Tests.Unit/ServiceDesk/PerTicketMetadataSyncServiceTests.cs \
        PmTracker.Web/Program.cs
git commit -m "feat(harvest): PerTicketMetadataSyncService — DB write 4 datumů"
```

---

### Task 5: `HarmonogramKrokDatumMapping` — krok 1 PMP/PNF

**Files:**
- Modify: `PmTracker.Web/Services/Schedules/HarmonogramKrokDatumMapping.cs`
- Modify: `PmTracker.Tests.Unit/Schedule/HarmonogramKrokDatumMappingTests.cs`

- [ ] **Step 1: Přidat konstantu `PredikatK1` + mapping krok 1**

V `HarmonogramKrokDatumMapping.cs`:

```csharp
/// <summary>PredikatKey „K1" — datum založení tiketu (z HOT_ZAZNAMY.datum, ne z popisu vyjádření).</summary>
public const string PredikatK1 = "K1";
```

A v Matrix:

```csharp
["PMP"] = new Dictionary<int, string>
{
    [1] = PredikatK1,    // NEW
    [3] = PredikatK3,
    [4] = PredikatK4K7,
},
["PNF"] = new Dictionary<int, string>
{
    [1] = PredikatK1,    // NEW
    [6] = PredikatK6,
    [7] = PredikatK4K7,
    [10] = PredikatK10,
},
```

- [ ] **Step 2: Aktualizovat existující testy + přidat nové**

V `HarmonogramKrokDatumMappingTests.cs`:

```csharp
[Theory]
[InlineData("PMP", 1, "K1")]
[InlineData("PNF", 1, "K1")]
[InlineData("NES", 1, null)]   // NES je nadále prázdné
public void GetPredikatKey_Krok1_MapujeNaK1ProPmpPnf(string typ, int krok, string? expected)
{
    HarmonogramKrokDatumMapping.GetPredikatKey(typ, krok).Should().Be(expected);
}
```

- [ ] **Step 3: Run tests**

```bash
dotnet test PmTracker.Tests.Unit -c Release --filter "HarmonogramKrokDatumMapping" --no-restore
```

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/Services/Schedules/HarmonogramKrokDatumMapping.cs \
        PmTracker.Tests.Unit/Schedule/HarmonogramKrokDatumMappingTests.cs
git commit -m "feat(harmonogram): krok 1 PMP/PNF mapped na K1 (datum založení tiketu)"
```

---

### Task 6: `HarmonogramSkutecnostResolver` — MIN výjimka pro krok 1

**Files:**
- Modify: `PmTracker.Web/Services/Schedules/HarmonogramSkutecnostResolver.cs`
- Modify: `PmTracker.Tests.Unit/Schedule/HarmonogramSkutecnostResolverTests.cs`

- [ ] **Step 1: Přidat MIN/MAX rozdíl podle krokPoradi**

V `Resolve` metodě nahradit:

```csharp
var kandidati = allBindingsForZaznam
    .Where(b => HarmonogramKrokDatumMapping.GetPredikatKey(b.TypZaznamu, krokPoradi) == b.PredikatKey)
    .OrderByDescending(b => b.Datum)
    .ThenBy(b => b.ExterniOdkazId)
    .ToList();
```

za:

```csharp
var matched = allBindingsForZaznam
    .Where(b => HarmonogramKrokDatumMapping.GetPredikatKey(b.TypZaznamu, krokPoradi) == b.PredikatKey);

// Krok 1 = MIN agregace (nejdřívější datum založení napříč ticketu).
// Ostatní kroky = MAX (nejpozdější datum vyjádření).
var kandidati = (krokPoradi == 1
        ? matched.OrderBy(b => b.Datum)
        : matched.OrderByDescending(b => b.Datum))
    .ThenBy(b => b.ExterniOdkazId)
    .ToList();
```

- [ ] **Step 2: Přidat test pro krok 1 MIN**

```csharp
[Fact]
public void Resolve_Krok1_DvaKandidati_VratiMin()
{
    var b1 = new BindingKandidat(100, "111111", "PMP", "K1", new DateTime(2026, 1, 1));
    var b2 = new BindingKandidat(101, "222222", "PMP", "K1", new DateTime(2026, 2, 15));
    var r = HarmonogramSkutecnostResolver.Resolve(1, new[] { b1, b2 }, preferredExterniOdkazId: null);

    r.Datum.Should().Be(new DateTime(2026, 1, 1));  // MIN
    r.Kandidati[0].ExterniOdkazId.Should().Be(100); // MIN first
}

[Fact]
public void Resolve_Krok3_DvaKandidati_VratiMax_BezeZmeny()
{
    var b1 = new BindingKandidat(100, "111111", "PMP", "K3", new DateTime(2026, 1, 1));
    var b2 = new BindingKandidat(101, "222222", "PMP", "K3", new DateTime(2026, 2, 15));
    var r = HarmonogramSkutecnostResolver.Resolve(3, new[] { b1, b2 }, preferredExterniOdkazId: null);

    r.Datum.Should().Be(new DateTime(2026, 2, 15));  // MAX (default)
}
```

- [ ] **Step 3: Run tests**

```bash
dotnet test PmTracker.Tests.Unit -c Release --filter "HarmonogramSkutecnostResolver" --no-restore
```

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/Services/Schedules/HarmonogramSkutecnostResolver.cs \
        PmTracker.Tests.Unit/Schedule/HarmonogramSkutecnostResolverTests.cs
git commit -m "feat(harmonogram): krok 1 = MIN agregace, ostatní MAX (default)"
```

---

### Task 7: `VyjadreniHarvestService` — integrace metadata sync + NES skip + synthetic K1

**Files:**
- Modify: `PmTracker.Web/Services/ServiceDesk/VyjadreniHarvestService.cs`
- Modify: `PmTracker.Tests.Unit/ServiceDesk/VyjadreniHarvestServiceTests.cs`

- [ ] **Step 1: Inject `IPerTicketMetadataSyncService` do constructoru**

```csharp
private readonly IPerTicketMetadataSyncService? _metadataSync;

public VyjadreniHarvestService(
    PmTrackerDbContext db,
    IVyjadreniQueryService vyjadreni,
    TimeProvider time,
    IPerExterniOdkazLockRegistry lockRegistry,
    ILogger<VyjadreniHarvestService> logger,
    IHarmonogramSkutecnostSyncService? skutecnostSync = null,
    IPerTicketMetadataSyncService? metadataSync = null)   // NEW
{
    // ...
    _metadataSync = metadataSync;
}
```

- [ ] **Step 2: V `HarvestTicketCoreAsync` — NES skip stepper, PMP/PNF synthetic K1**

Najít sekci `if (list.Count > 0)` a před foreach loop přidat NES branch:

```csharp
var typZaznamu = typZaznamuHint ?? NormalizeTypZaznamu(zaznam, eo);

// NES tickety NEPRO HARVESTUJÍ stepper bindings (žádný harmonogram krok auto-fill).
// Pouze metadata sync (volá se na konci metody).
bool isNes = string.Equals(typZaznamu, "NES", StringComparison.OrdinalIgnoreCase);

if (list.Count > 0 && !isNes)
{
    var krokKeyByPoradi = await LoadKrokKeyByPoradiAsync(zaznam.HarmonogramSablonaVerze, ct).ConfigureAwait(false);
    
    // ... existing foreach loop with bindings ...
}

// Synthetic K1 binding pro PMP/PNF — z HOT_ZAZNAMY.datum.
// Reprezentuje krok 1 „příprava zadání" jako virtuální vyjádření s ID = 0
// (SQL FK na hot_vyjadreni neexistuje, takže ID 0 je bezpečné).
if (!isNes)
{
    // Vytvořit synthetic binding pokud ještě nemáme aktivní binding pro K1 v tomto ticketu.
    // (Použít primary fingerprint datum přes typZaznamuHint cesta — tady ho nemáme,
    //  raději volat _vyjadreni.GetHotZaznamFingerprintsAsync inline.)
    var fp = await _vyjadreni.GetHotZaznamFingerprintsAsync(new[] { eo.Cislo! }, ct).ConfigureAwait(false);
    if (fp.TryGetValue(eo.Cislo!, out var primary))
    {
        await UpsertSyntheticK1BindingAsync(zaznam, eo, primary.Datum, ct).ConfigureAwait(false);
    }
}
```

A pomocná metoda:

```csharp
private async Task UpsertSyntheticK1BindingAsync(
    ProjektovyZaznamEntity zaznam,
    ZaznamExterniOdkazEntity eo,
    DateTime hotZaznamDatum,
    CancellationToken ct)
{
    // K1 binding patří na krok 1 — najdi KrokKey
    var krokKeyByPoradi = await LoadKrokKeyByPoradiAsync(zaznam.HarmonogramSablonaVerze, ct).ConfigureAwait(false);
    if (!krokKeyByPoradi.TryGetValue(1, out var krokKey)) return;

    // PredikatKey = "K1" (per HarmonogramKrokDatumMapping.PredikatK1)
    // HotVyjadreniId = 0 (synthetic — sloupec nemá FK)
    var existing = await _db.VyjadreniVazby
        .Where(x => x.ZaznamId == zaznam.Id
                 && x.ExterniOdkazId == eo.Id
                 && x.KrokKey == krokKey
                 && x.Stav == (byte)VazbaStav.Active
                 && x.HotVyjadreniId == 0L)
        .FirstOrDefaultAsync(ct).ConfigureAwait(false);

    if (existing is null)
    {
        // Označit Superseded existující jiné active K1 bindings na této vazbě (defensive)
        var others = await _db.VyjadreniVazby
            .Where(x => x.ZaznamId == zaznam.Id
                     && x.ExterniOdkazId == eo.Id
                     && x.KrokKey == krokKey
                     && x.Stav == (byte)VazbaStav.Active)
            .ToListAsync(ct).ConfigureAwait(false);
        var nowUtc = _time.GetUtcNow().UtcDateTime;
        foreach (var o in others)
        {
            o.Stav = (byte)VazbaStav.Superseded;
        }

        _db.VyjadreniVazby.Add(new ZaznamHarmonogramVyjadreniVazbaEntity
        {
            ZaznamId = zaznam.Id,
            KrokKey = krokKey,
            ExterniOdkazId = eo.Id,
            HotVyjadreniId = 0L,
            DatumVyjadreni = hotZaznamDatum,
            Source = (byte)VazbaSource.Auto,
            Stav = (byte)VazbaStav.Active,
            CreatedAt = nowUtc
        });
    }
    else if (existing.DatumVyjadreni != hotZaznamDatum)
    {
        // Datum se změnilo (re-harvest) — supersede a vytvoř nový.
        existing.Stav = (byte)VazbaStav.Superseded;
        var nowUtc = _time.GetUtcNow().UtcDateTime;
        _db.VyjadreniVazby.Add(new ZaznamHarmonogramVyjadreniVazbaEntity
        {
            ZaznamId = zaznam.Id,
            KrokKey = krokKey,
            ExterniOdkazId = eo.Id,
            HotVyjadreniId = 0L,
            DatumVyjadreni = hotZaznamDatum,
            Source = (byte)VazbaSource.Auto,
            Stav = (byte)VazbaStav.Active,
            CreatedAt = nowUtc
        });
    }
}
```

**Pozn.:** Také upravit `PerTicketMetadataSyncService.SyncTicketAsync` aby se volala _i pro NES tickety_ (NES bindings se neukládají, ale 4 datumy se vyextrahují přes `PerTicketMetadataExtractor.Extract`).

- [ ] **Step 3: Volání metadata sync na konci `HarvestTicketCoreAsync`**

Najít existující volání `_skutecnostSync.SyncZaznamAsync(zaznam.Id, ct)` a před něj nebo paralelně s ním přidat:

```csharp
if (_metadataSync is not null)
{
    try
    {
        await _metadataSync.SyncTicketAsync(eo.Id, ct).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex,
            "VyjadreniHarvestService: PerTicketMetadataSync selhal pro externí odkaz {Id} (cislo={Cislo}).",
            eo.Id, eo.Cislo);
    }
}
```

(Best-effort: chyba v metadata syncu nezasahuje výsledek harvestu.)

- [ ] **Step 4: Pro NES tickety — přidat metadata sync volání i když je `list.Count == 0` nebo skip stepper logiky**

Aktuální struktura:

```csharp
if (list.Count > 0) { /* harvest bindings */ }
// ... fingerprint update ...
await _db.SaveChangesAsync(ct).ConfigureAwait(false);
// SkutecnostSync ...
```

Pro NES list může být i 0 a stejně chceme metadata sync. `_metadataSync` volá `_vyjadreni.GetVyjadreniForTicketAsync` zvlášť → bude fungovat i když harvest list je prázdný.

- [ ] **Step 5: Run existing tests + nové integration test cases**

```bash
dotnet test PmTracker.Tests.Unit -c Release --filter "VyjadreniHarvest" --no-restore
```

Některé existující testy mohou selhat kvůli nutnosti mockovat nový `IPerTicketMetadataSyncService` parametr — projít a opravit (nullable parameter má default null, takže optional).

Nové testy:

```csharp
[Fact]
public async Task HarvestTicketAsync_NesTiket_NenechaStepperBindings()
{
    // Arrange: mock _vyjadreni vrátí typ=NES
    // Act: HarvestTicketAsync
    // Assert: VyjadreniVazby.Count() == 0 pro toto eo
    //         _metadataSync.SyncTicketAsync byl volán
}

[Fact]
public async Task HarvestTicketAsync_PmpTiket_VytvoriSyntheticK1Binding()
{
    // Arrange: mock _vyjadreni vrátí typ=PMP, hot_zaznam.datum=2026-01-15
    // Act: HarvestTicketAsync
    // Assert: VyjadreniVazby obsahuje K1 binding s HotVyjadreniId=0, DatumVyjadreni=2026-01-15
}
```

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/ServiceDesk/VyjadreniHarvestService.cs \
        PmTracker.Tests.Unit/ServiceDesk/VyjadreniHarvestServiceTests.cs
git commit -m "feat(harvest): NES skip stepper, PMP/PNF synthetic K1, metadata sync invocation"
```

---

### Task 8: UI modal „Vyjádření a termíny" — skrýt stepper pro NES

**Files:**
- Modify: Razor partial modalu „Vyjádření a termíny" (přesný soubor zjistit grep-em)

- [ ] **Step 1: Najít soubor modalu**

```bash
grep -rln "Vyjádření a termíny\|chat-stepper\|pm-chat-stepper" "/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Views/" "/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/wwwroot/" 2>/dev/null
```

Pravděpodobní kandidáti dle git status: `_EditZaznamExternalPanel.cshtml`, `_ZaznamPartial.cshtml`, nebo modal partial.

- [ ] **Step 2: Identifikovat sekci stepperu**

Otevřít kandidáta a najít blok s `<pm-chat-stepper>` nebo s renderováním napojení vyjádření na harmonogramové kroky.

- [ ] **Step 3: Conditional rendering**

Obalit blok `if (eo.TypZaznamu != "NES")`:

```cshtml
@if (!string.Equals(Model.TypZaznamu, "NES", StringComparison.OrdinalIgnoreCase))
{
    <!-- existující stepper sekce -->
    <pm-chat-stepper ...></pm-chat-stepper>
    <!-- ... -->
}
```

- [ ] **Step 4: Pokud existuje JS module který se snaží vyhledat stepper element**

Najít odpovídající JS:

```bash
grep -rn "pm-chat-stepper\|chat-stepper" "/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/wwwroot/js/" 2>/dev/null
```

Ujistit se, že JS čeká na existenci elementu (defensive: `if (!stepper) return;`). Pokud ano, pro NES stepper neexistuje a JS bezpečně přeskočí.

- [ ] **Step 5: Smoke test (manuální)**

Spustit dev server, otevřít projektový záznam s NES externí vazbou + projektový záznam s PMP/PNF externí vazbou. Otevřít modal „Vyjádření a termíny" pro každý — pro NES nesmí být stepper viditelný, pro PMP/PNF musí.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Views/...  # přesný soubor
git commit -m "feat(modal): skrýt stepper sekci v 'Vyjádření a termíny' pro NES vazby"
```

---

### Task 9: Final build + run all tests + smoke

- [ ] **Step 1: Full build**

```bash
dotnet build PmTracker.sln -c Release --no-restore 2>&1 | tail -50
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 2: Full unit tests**

```bash
dotnet test PmTracker.Tests.Unit -c Release --no-restore 2>&1 | tail -20
```

Expected: všechny testy PASS (kromě 2 pre-existing failů v ChatModalDragDropBundle a ManualKrokyJsModuleTests, dle predchozí session log).

- [ ] **Step 3: Bun bundle**

Pokud se používá bun pro frontend bundle:

```bash
cd PmTracker.Web && bun run build 2>&1 | tail -10
```

- [ ] **Step 4: Spustit dev server a smoke test**

```bash
dotnet run --project PmTracker.Web --no-build &
# počkat ~10s na startup
# Otevřít browser na http://localhost:5000
```

Smoke kontroly:
1. Projekt s NES externí vazbou: harmonogram tab funguje (PMP/PNF kroky vidím), modal „Vyjádření a termíny" pro NES bez stepperu, ale 4 datumy v UI viditelné a auto-filled.
2. Projekt s PMP externí vazbou: harmonogram krok 1 = MIN datum napříč všemi PMP/PNF tickety, krok 3 = K3 datum, krok 4 = K4 DESC last datum.
3. Projekt s PNF externí vazbou: krok 1 = MIN, krok 6 = K6, krok 7 = K7 DESC, krok 10 = K10.
4. Harvest re-trigger (`/SDConnector` admin) přepíše ručně zadané hodnoty 4 datumů.

- [ ] **Step 5: Commit final (pokud potřeba — měly by být všechny commits z předchozích tasků)**

```bash
# Pokud byly úpravy ve smoke testech, commit:
git status
git log --oneline | head -20
```

---

## Self-Review

**1. Spec coverage:**
- §1 NES odpojen → Task 7 (NES skip stepper) ✓
- §1 Krok 1 PMP/PNF → Task 5 + Task 7 (synthetic K1) ✓
- §2 MIN/MAX agregace → Task 6 ✓
- §3 NES UI skip stepper → Task 8 ✓
- §4.1 PMP/PNF 4 datumy → Task 3 (extractor) + Task 4 (sync) ✓
- §4.2 NES 4 datumy → Task 1 (NES predikáty) + Task 2 (sla_deadline DTO) + Task 3 (extractor) + Task 4 (sync) ✓
- §4.3 PlanDodani dvojfráze → Task 2 (validator) + Task 3 (extractor) ✓
- §4.4 Null tolerance → Task 3 test cases ✓
- §5 Switch / cascade → beze změny (existující kód) ✓

**2. Placeholder scan:** projeto, bez TBD/TODO. Step 4 v Tasku 7 odkazuje na další Task — ok, je to pokyn pro engineera, ne placeholder.

**3. Type consistency:**
- `PerTicketMetadata` (Task 3) ↔ použito v `PerTicketMetadataSyncService` (Task 4) ✓
- `PredikatK1` const (Task 5) ↔ porovnání v `Resolver` (Task 6) přes `b.PredikatKey == "K1"` — pozor, resolver porovnává `b.PredikatKey` proti vrácené hodnotě z `GetPredikatKey(typ, krok)`, takže shoda je `K1 == K1` přes string. ✓
- `HarvestPredicateKind.NES_DatumObjednani / NES_DatumDodani` (Task 1) ↔ použito v `ClassifyPopisForNes` (Task 1) + `PerTicketMetadataExtractor.ExtractNes` (Task 3) ✓
- `IPerTicketMetadataSyncService` (Task 4) ↔ DI registration (Task 4 Step 2) + injekce do `VyjadreniHarvestService` (Task 7 Step 1) ✓

Plan complete and saved to `docs/superpowers/plans/2026-04-28-nes-vyjadreni-a-4-datumy.md`.
