# Feature C — harmonogram auto-fill z ticketů implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Automaticky vyplnit `ProjektovyZaznamHarmonogram.SkutecnostDatum` z datumů vyjádření vytěžených ze ServiceDesk ticketů (binding `zaznam_harmonogram_vyjadreni_vazba` → krok harmonogramu). Přidat switch „Automaticky z SD ⇄ Ručně" per záznam, `SkutecnostZdrojEnum` pro audit zdroje, dropdown pro výběr alternativního datumu při 2+ kandidátech, kaskádové volání při změně bindingu.

**Architecture:**
1. **DB migrace** — nové sloupce `SkutecnostZdrojEnum` (byte) + `SkutecnostRezim` (byte: 0=Auto, 1=Manual) na `ProjektovyZaznamHarmonogram`.
2. **Service `HarmonogramSkutecnostResolver`** — pure funkce: pro každý řádek harmonogramu dle typu ticketu + bindings vypočte kandidáty (K3/K6/K4/K7/K10 datumy) + vybere MAX default.
3. **Service `HarmonogramSkutecnostSyncService`** — kaskádově volán při binding change nebo switch toggle; nepíše při `SkutecnostRezim = Manual`.
4. **UI** — switch v záložce Harmonogram (vpravo u sloupce Skutečnost), dropdown (chevron) u buňky Skutečnost při 2+ kandidátech, badge `SkutecnostZdrojEnum`.
5. **Trigger points** — (a) po save bindingu ve `VyjadreniVazbaService` / `BindingRebalanceService`, (b) po toggle switchu, (c) po Re-harvest batch.

**Tech Stack:** .NET 8, EF Core 8, existing binding infra (`BindingRebalanceService`, `VyjadreniVazby`), Razor partials, vanilla JS dropdown.

**Spec source:**
- [docs/specs/harmonogram-plan-vs-skutecnost.md](../../specs/harmonogram-plan-vs-skutecnost.md) §3-8
- [2026-04-21-servicedesk-vytezovani-vyjadreni-design.md §1.3-5](../specs/2026-04-21-servicedesk-vytezovani-vyjadreni-design.md) — matice NES/PMP/PNF
- Decision brief: [2026-04-24-sd-features-decision-brief.md](../specs/2026-04-24-sd-features-decision-brief.md) → C-Q1 až C-Q6

---

## File Structure

### Nové soubory
| Soubor | Odpovědnost |
|---|---|
| `db_upgrade_1_3_10_harmonogram_skutecnost_zdroj.sql` | DB migrace — 2 nové sloupce + SkutecnostZdrojEnum default Historicka pro existing řádky |
| `PmTracker.Web/Models/Entities/SkutecnostZdrojEnum.cs` | Enum: Neznámo/Automat/Manual/Historicka |
| `PmTracker.Web/Models/Entities/SkutecnostRezimEnum.cs` | Enum: Auto/Manual |
| `PmTracker.Web/Services/Harmonogram/HarmonogramSkutecnostResolver.cs` | Pure logic: binding + typ ticketu + krok → kandidátní datumy + MAX default |
| `PmTracker.Web/Services/Harmonogram/HarmonogramKrokDatumMapping.cs` | Hardcoded matice NES/PMP/PNF (dle C-Q6) |
| `PmTracker.Web/Services/Harmonogram/HarmonogramSkutecnostSyncService.cs` | Kaskádové volání + zápis do DB (respect SkutecnostRezim=Manual) |
| `PmTracker.Web/Models/ViewModels/Harmonogram/HarmonogramSkutecnostRowViewModel.cs` | VM per řádek: SkutecnostDatum + Zdroj + Kandidáti + CanOverride |
| `PmTracker.Web/wwwroot/js/components/harmonogram-skutecnost-switch/switch.js` | Switch Auto/Ručně toggle handler |
| `PmTracker.Web/wwwroot/js/components/harmonogram-skutecnost-dropdown/dropdown.js` | Chevron dropdown pro výběr alternativy |
| `PmTracker.Web/wwwroot/css/components/harmonogram-skutecnost.css` | Layout styling |
| `PmTracker.Tests.Unit/Services/Harmonogram/HarmonogramSkutecnostResolverTests.cs` | Unit: matice + MAX + kandidáti |
| `PmTracker.Tests.Unit/Services/Harmonogram/HarmonogramSkutecnostSyncServiceTests.cs` | Unit: manual override skip + cascade trigger |

### Modifikované soubory
| Soubor | Změna |
|---|---|
| `PmTracker.Web/Models/Entities/ProjektovyZaznamHarmonogram.cs` | `+ SkutecnostZdrojEnum SkutecnostZdroj` + `+ SkutecnostRezimEnum SkutecnostRezim` |
| `PmTracker.Web/Data/PmTrackerDbContext.cs` | EF mapping nových sloupců |
| `PmTracker.Web/Services/ServiceDesk/BindingRebalanceService.cs` | Po binding save → volat `HarmonogramSkutecnostSyncService` pro záznam |
| `PmTracker.Web/Services/ServiceDesk/VyjadreniHarvestService.cs` | Po re-harvest batch → reset user bindings + trigger sync |
| `PmTracker.Web/Controllers/???HarmonogramController.cs` | Přidat POST `ToggleRezim` + POST `SelectSkutecnostCandidate` endpointy |
| `PmTracker.Web/Views/???/_ScheduleBlock.cshtml` | Přidat switch + dropdown + badge |

---

## Tasks

### Task 1: DB migrace + enum + entity

**Files:**
- Create: `db_upgrade_1_3_10_harmonogram_skutecnost_zdroj.sql`
- Create: `PmTracker.Web/Models/Entities/SkutecnostZdrojEnum.cs`
- Create: `PmTracker.Web/Models/Entities/SkutecnostRezimEnum.cs`
- Modify: `PmTracker.Web/Models/Entities/ProjektovyZaznamHarmonogram.cs`
- Modify: `PmTracker.Web/Data/PmTrackerDbContext.cs`

- [ ] **Step 1: Enum definice**

```csharp
// PmTracker.Web/Models/Entities/SkutecnostZdrojEnum.cs
namespace PmTracker.Web.Models.Entities;

public enum SkutecnostZdrojEnum : byte
{
    Neznamo    = 0,  // default, prázdná skutečnost
    Automat    = 1,  // auto-fillovaný ze SD bindingu
    Manual     = 2,  // ručně vyplněný přes switch=Ručně + explicitní input
    Historicka = 3,  // migrovaná data před zavedením auto-fill (2026-04-24)
}
```

```csharp
// PmTracker.Web/Models/Entities/SkutecnostRezimEnum.cs
namespace PmTracker.Web.Models.Entities;

public enum SkutecnostRezimEnum : byte
{
    Auto   = 0,  // default — auto-fill ze SD je aktivní
    Manual = 1,  // user přepnul na ruční zápis, auto-fill ignoruje tento řádek
}
```

- [ ] **Step 2: DB migrace**

```sql
-- db_upgrade_1_3_10_harmonogram_skutecnost_zdroj.sql
-- Účel: Přidat SkutecnostZdrojEnum + SkutecnostRezimEnum na ProjektovyZaznamHarmonogram
-- pro Feature C (auto-fill z SD ticketů).

SET NOCOUNT ON;
GO

-- Idempotent guard
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.projektovy_zaznam_harmonogram') AND name = 'SkutecnostZdroj')
BEGIN
    ALTER TABLE dbo.projektovy_zaznam_harmonogram
        ADD SkutecnostZdroj TINYINT NOT NULL CONSTRAINT DF_pzh_skutecnost_zdroj DEFAULT (0);

    -- Existing záznamy se SkutecnostDatum nastaveným = Historicka (zdroj neznámý, ale data existují)
    UPDATE dbo.projektovy_zaznam_harmonogram
    SET SkutecnostZdroj = 3  -- Historicka
    WHERE SkutecnostDatum IS NOT NULL;

    PRINT 'Added SkutecnostZdroj column + backfilled Historicka for existing data.';
END
ELSE
    PRINT 'SkutecnostZdroj column already exists, skipping.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.projektovy_zaznam_harmonogram') AND name = 'SkutecnostRezim')
BEGIN
    ALTER TABLE dbo.projektovy_zaznam_harmonogram
        ADD SkutecnostRezim TINYINT NOT NULL CONSTRAINT DF_pzh_skutecnost_rezim DEFAULT (0);

    PRINT 'Added SkutecnostRezim column with default Auto.';
END
ELSE
    PRINT 'SkutecnostRezim column already exists, skipping.';
GO

-- Sanity check
SELECT
    COUNT(*) AS total_rows,
    SUM(CASE WHEN SkutecnostZdroj = 3 THEN 1 ELSE 0 END) AS historicka_count,
    SUM(CASE WHEN SkutecnostZdroj = 0 THEN 1 ELSE 0 END) AS neznamo_count,
    SUM(CASE WHEN SkutecnostRezim = 0 THEN 1 ELSE 0 END) AS rezim_auto,
    SUM(CASE WHEN SkutecnostRezim = 1 THEN 1 ELSE 0 END) AS rezim_manual
FROM dbo.projektovy_zaznam_harmonogram;
GO
```

- [ ] **Step 3: Rozšířit entity**

V `ProjektovyZaznamHarmonogram.cs` (nejspíš v `PmTracker.Web/Models/Entities/`) přidat:

```csharp
public SkutecnostZdrojEnum SkutecnostZdroj { get; set; } = SkutecnostZdrojEnum.Neznamo;
public SkutecnostRezimEnum SkutecnostRezim { get; set; } = SkutecnostRezimEnum.Auto;
```

- [ ] **Step 4: EF mapping**

V `PmTrackerDbContext.OnModelCreating` najdi `ProjektovyZaznamHarmonogram` konfiguraci a přidej:

```csharp
entity.Property(x => x.SkutecnostZdroj)
    .HasColumnName("SkutecnostZdroj")
    .HasConversion<byte>()
    .HasDefaultValue(SkutecnostZdrojEnum.Neznamo);

entity.Property(x => x.SkutecnostRezim)
    .HasColumnName("SkutecnostRezim")
    .HasConversion<byte>()
    .HasDefaultValue(SkutecnostRezimEnum.Auto);
```

- [ ] **Step 5: Spustit migraci lokálně + build**

```bash
sqlcmd -S localhost -d PM_Tracker_VYVOJ -i db_upgrade_1_3_10_harmonogram_skutecnost_zdroj.sql
dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore
```

- [ ] **Step 6: Commit**

```bash
git add db_upgrade_1_3_10_harmonogram_skutecnost_zdroj.sql \
        PmTracker.Web/Models/Entities/SkutecnostZdrojEnum.cs \
        PmTracker.Web/Models/Entities/SkutecnostRezimEnum.cs \
        PmTracker.Web/Models/Entities/ProjektovyZaznamHarmonogram.cs \
        PmTracker.Web/Data/PmTrackerDbContext.cs
git commit -m "feat(harmonogram): DB migrace SkutecnostZdroj + SkutecnostRezim enums

Nová sloupce pro audit zdroje SkutecnostDatum + switch per-záznam Auto/Manual.
Existing SkutecnostDatum rows dostávají Zdroj=Historicka (migrace)."
```

---

### Task 2: Krok↔datum matice (NES/PMP/PNF)

**Files:**
- Create: `PmTracker.Web/Services/Harmonogram/HarmonogramKrokDatumMapping.cs`
- Create: `PmTracker.Tests.Unit/Services/Harmonogram/HarmonogramKrokDatumMappingTests.cs`

- [ ] **Step 1: Matice jako static readonly dictionary**

```csharp
// PmTracker.Web/Services/Harmonogram/HarmonogramKrokDatumMapping.cs
namespace PmTracker.Web.Services.Harmonogram;

/// <summary>
/// Matice: (TypZaznamu, KrokPoradi) → PredikatKey, který indikuje které vyjádření má být použito jako zdroj SkutecnostDatum.
/// Dle matice v decision brief C-Q6 (2026-04-24):
///
///  | Typ | Krok 1 „Objednáno"       | Krok 3 „Dodáno" | Krok 5 „Archivováno" |
///  | NES | — (neplní)                | K4_K7           | K10                   |
///  | PMP | K3                        | K4_K7           | K10                   |
///  | PNF | K6 (kalkulace akceptována)| K4_K7           | K10                   |
///
/// Zdroj pravdy: 2026-04-21-servicedesk-vytezovani-vyjadreni-design.md §3-3.1
/// </summary>
public static class HarmonogramKrokDatumMapping
{
    // Klíč: TypZaznamu (NES/PMP/PNF), hodnota: mapa KrokPoradi → PredikatKey nebo null
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<int, string?>> Matrix =
        new Dictionary<string, IReadOnlyDictionary<int, string?>>
        {
            ["NES"] = new Dictionary<int, string?>
            {
                [1] = null,      // NES nemá objednávku
                [3] = "K4_K7",   // Dodání
                [5] = "K10",     // Archivace
            },
            ["PMP"] = new Dictionary<int, string?>
            {
                [1] = "K3",      // Odeslání dodavateli
                [3] = "K4_K7",   // Dodání
                [5] = "K10",     // Archivace
            },
            ["PNF"] = new Dictionary<int, string?>
            {
                [1] = "K6",      // Kalkulace akceptována
                [3] = "K4_K7",   // Dodání
                [5] = "K10",     // Archivace
            },
        };

    /// <summary>
    /// Vrátí PredikatKey pro daný typ ticketu + krok v harmonogramu.
    /// Vrací null pokud krok nemá mapování (např. NES krok 1).
    /// </summary>
    public static string? GetPredikatKey(string typZaznamu, int krokPoradi)
    {
        if (string.IsNullOrWhiteSpace(typZaznamu)) return null;
        if (!Matrix.TryGetValue(typZaznamu.Trim().ToUpperInvariant(), out var map)) return null;
        return map.TryGetValue(krokPoradi, out var key) ? key : null;
    }

    public static IReadOnlyCollection<string> SupportedTypes => Matrix.Keys;
}
```

- [ ] **Step 2: Unit testy**

```csharp
// PmTracker.Tests.Unit/Services/Harmonogram/HarmonogramKrokDatumMappingTests.cs
using FluentAssertions;
using PmTracker.Web.Services.Harmonogram;
using Xunit;

namespace PmTracker.Tests.Unit.Services.Harmonogram;

public sealed class HarmonogramKrokDatumMappingTests
{
    [Theory]
    [InlineData("NES", 1, null)]
    [InlineData("NES", 3, "K4_K7")]
    [InlineData("NES", 5, "K10")]
    [InlineData("PMP", 1, "K3")]
    [InlineData("PMP", 3, "K4_K7")]
    [InlineData("PMP", 5, "K10")]
    [InlineData("PNF", 1, "K6")]
    [InlineData("PNF", 3, "K4_K7")]
    [InlineData("PNF", 5, "K10")]
    public void GetPredikatKey_MapujeDleSpecMatice(string typ, int krok, string? expected)
    {
        HarmonogramKrokDatumMapping.GetPredikatKey(typ, krok).Should().Be(expected);
    }

    [Theory]
    [InlineData("NES", 2)]    // chybějící krok
    [InlineData("PMP", 99)]   // neexistující
    [InlineData("XYZ", 1)]    // neznámý typ
    [InlineData("", 1)]
    [InlineData(null, 1)]
    public void GetPredikatKey_NeznamyVstup_VraciNull(string? typ, int krok)
    {
        HarmonogramKrokDatumMapping.GetPredikatKey(typ!, krok).Should().BeNull();
    }

    [Fact]
    public void SupportedTypes_Obsahuje3Typy()
    {
        HarmonogramKrokDatumMapping.SupportedTypes.Should().BeEquivalentTo(new[] { "NES", "PMP", "PNF" });
    }
}
```

Run: `dotnet test PmTracker.Tests.Unit -c Release --filter "HarmonogramKrokDatumMapping" --no-restore`
Expected: PASS 15/15.

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Web/Services/Harmonogram/HarmonogramKrokDatumMapping.cs \
        PmTracker.Tests.Unit/Services/Harmonogram/HarmonogramKrokDatumMappingTests.cs
git commit -m "feat(harmonogram): krok↔datum matice NES/PMP/PNF (hardcoded) + testy"
```

---

### Task 3: `HarmonogramSkutecnostResolver` — pure logic

**Files:**
- Create: `PmTracker.Web/Services/Harmonogram/HarmonogramSkutecnostResolver.cs`
- Create: `PmTracker.Tests.Unit/Services/Harmonogram/HarmonogramSkutecnostResolverTests.cs`

- [ ] **Step 1: Input/output typy**

```csharp
// PmTracker.Web/Services/Harmonogram/HarmonogramSkutecnostResolver.cs
namespace PmTracker.Web.Services.Harmonogram;

public sealed record BindingKandidat(
    int ExterniOdkazId,
    string Cislo6,
    string TypZaznamu,
    string PredikatKey,
    DateTime Datum);

public sealed record ResolvedSkutecnost(
    DateTime? Datum,
    IReadOnlyList<BindingKandidat> Kandidati);
```

- [ ] **Step 2: Pure resolver funkce**

```csharp
public static class HarmonogramSkutecnostResolver
{
    /// <summary>
    /// Pro daný krok harmonogramu vybere kandidátní datumy ze všech napojených externích vazeb
    /// (přes bindings matching předepsaného PredikatKey pro typ ticketu) + vrátí default MAX.
    ///
    /// Logika (dle C-Q2):
    /// - Pro každou externí vazbu najdi binding s predikat-matching vyjádření.
    /// - Sestav seznam kandidátů (1 per externí vazba).
    /// - Výchozí datum = MAX(Datum) přes všechny kandidáty (nejpozdější).
    /// - Pokud 0 kandidátů → null.
    /// </summary>
    public static ResolvedSkutecnost Resolve(
        int krokPoradi,
        IReadOnlyList<BindingKandidat> allBindingsForZaznam)
    {
        var kandidati = allBindingsForZaznam
            .Where(b => HarmonogramKrokDatumMapping.GetPredikatKey(b.TypZaznamu, krokPoradi) == b.PredikatKey)
            .OrderByDescending(b => b.Datum)  // MAX first
            .ToList();

        return new ResolvedSkutecnost(
            Datum: kandidati.Count > 0 ? kandidati[0].Datum : null,
            Kandidati: kandidati);
    }
}
```

- [ ] **Step 3: Unit testy**

```csharp
// PmTracker.Tests.Unit/Services/Harmonogram/HarmonogramSkutecnostResolverTests.cs
using FluentAssertions;
using PmTracker.Web.Services.Harmonogram;
using Xunit;

namespace PmTracker.Tests.Unit.Services.Harmonogram;

public sealed class HarmonogramSkutecnostResolverTests
{
    private static readonly DateTime D1 = new(2026, 3, 1);
    private static readonly DateTime D2 = new(2026, 3, 15);
    private static readonly DateTime D3 = new(2026, 3, 28);

    [Fact]
    public void Resolve_ZadneKandidati_VraciDatumNull()
    {
        var r = HarmonogramSkutecnostResolver.Resolve(3, Array.Empty<BindingKandidat>());
        r.Datum.Should().BeNull();
        r.Kandidati.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_JedenKandidat_VraciJeho()
    {
        var b = new BindingKandidat(100, "111111", "PMP", "K4_K7", D2);
        var r = HarmonogramSkutecnostResolver.Resolve(3, new[] { b });
        r.Datum.Should().Be(D2);
        r.Kandidati.Should().HaveCount(1);
    }

    [Fact]
    public void Resolve_DvaKandidati_VraciMax()
    {
        var b1 = new BindingKandidat(100, "111111", "PMP", "K4_K7", D2);
        var b2 = new BindingKandidat(101, "222222", "PMP", "K4_K7", D3);
        var r = HarmonogramSkutecnostResolver.Resolve(3, new[] { b1, b2 });
        r.Datum.Should().Be(D3);  // MAX
        r.Kandidati.Should().HaveCount(2);
        r.Kandidati[0].ExterniOdkazId.Should().Be(101);  // MAX first (OrderByDescending)
    }

    [Fact]
    public void Resolve_Binding_SPredikatemNesedicimNaKrok_Ignoruje()
    {
        // NES krok 3 = K4_K7. Binding s K10 je pro jiný krok → neměl by patřit do kandidátů krok 3.
        var b = new BindingKandidat(100, "111111", "NES", "K10", D1);
        var r = HarmonogramSkutecnostResolver.Resolve(3, new[] { b });
        r.Datum.Should().BeNull();
        r.Kandidati.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_NESKrok1_NemaMapovani_ZadnyKandidat()
    {
        // NES krok 1 = null v matici → nic neplní
        var b = new BindingKandidat(100, "111111", "NES", "K3", D1);
        var r = HarmonogramSkutecnostResolver.Resolve(1, new[] { b });
        r.Datum.Should().BeNull();
    }

    [Fact]
    public void Resolve_MixTypu_PouzeMatchingPredikat()
    {
        // Záznam má 2 externí vazby: 1× PMP + 1× PNF. Pro krok 1: PMP→K3, PNF→K6. Každá vrací jiný predikat.
        var pmpBinding = new BindingKandidat(100, "111111", "PMP", "K3", D1);
        var pnfBinding = new BindingKandidat(101, "222222", "PNF", "K6", D2);
        var r = HarmonogramSkutecnostResolver.Resolve(1, new[] { pmpBinding, pnfBinding });
        r.Kandidati.Should().HaveCount(2);
        r.Datum.Should().Be(D2);  // PNF D2 je max
    }
}
```

Run: `dotnet test PmTracker.Tests.Unit -c Release --filter "HarmonogramSkutecnostResolver" --no-restore`
Expected: PASS 6/6.

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/Services/Harmonogram/HarmonogramSkutecnostResolver.cs \
        PmTracker.Tests.Unit/Services/Harmonogram/HarmonogramSkutecnostResolverTests.cs
git commit -m "feat(harmonogram): SkutecnostResolver — MAX default + kandidáti"
```

---

### Task 4: `HarmonogramSkutecnostSyncService` — orchestrace + zápis do DB

**Files:**
- Create: `PmTracker.Web/Services/Harmonogram/HarmonogramSkutecnostSyncService.cs`
- Create: `PmTracker.Tests.Unit/Services/Harmonogram/HarmonogramSkutecnostSyncServiceTests.cs`

- [ ] **Step 1: Service interface + impl**

```csharp
// PmTracker.Web/Services/Harmonogram/HarmonogramSkutecnostSyncService.cs
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.Harmonogram;

public interface IHarmonogramSkutecnostSyncService
{
    /// <summary>
    /// Kaskádově přepočítá SkutecnostDatum pro všechny kroky harmonogramu daného záznamu.
    /// Respektuje SkutecnostRezim — řádky s Manual skip.
    /// </summary>
    Task<HarmonogramSyncResult> SyncZaznamAsync(int projektovyZaznamId, CancellationToken ct);
}

public sealed record HarmonogramSyncResult(
    int ProjektovyZaznamId,
    int KrokuAktualizovano,
    int KrokuPreskoceno_Manual,
    int KrokuBezKandidatu);

public sealed class HarmonogramSkutecnostSyncService : IHarmonogramSkutecnostSyncService
{
    private readonly PmTrackerDbContext _db;
    private readonly ILogger<HarmonogramSkutecnostSyncService> _logger;

    public HarmonogramSkutecnostSyncService(
        PmTrackerDbContext db,
        ILogger<HarmonogramSkutecnostSyncService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<HarmonogramSyncResult> SyncZaznamAsync(int projektovyZaznamId, CancellationToken ct)
    {
        // 1) Načíst harmonogram kroky
        var kroky = await _db.ProjektoveZaznamyHarmonogram
            .Where(h => h.ZaznamId == projektovyZaznamId)
            .ToListAsync(ct).ConfigureAwait(false);

        if (kroky.Count == 0)
        {
            return new HarmonogramSyncResult(projektovyZaznamId, 0, 0, 0);
        }

        // 2) Načíst všechny active bindings + datumy vyjádření pro tento záznam
        var bindings = await LoadBindingKandidatiAsync(projektovyZaznamId, ct).ConfigureAwait(false);

        // 3) Per krok: resolve + (pokud Rezim=Auto) zapsat
        int updated = 0, skipManual = 0, noKandidat = 0;
        foreach (var krok in kroky)
        {
            if (krok.SkutecnostRezim == SkutecnostRezimEnum.Manual)
            {
                skipManual++;
                continue;
            }

            var resolved = HarmonogramSkutecnostResolver.Resolve(krok.Poradi, bindings);
            if (resolved.Datum is null)
            {
                // Žádný kandidát → pokud byl Zdroj=Automat, null-uj (data se zrušila), jinak nech
                if (krok.SkutecnostZdroj == SkutecnostZdrojEnum.Automat && krok.SkutecnostDatum != null)
                {
                    krok.SkutecnostDatum = null;
                    krok.SkutecnostZdroj = SkutecnostZdrojEnum.Neznamo;
                    updated++;
                }
                else
                {
                    noKandidat++;
                }
                continue;
            }

            // Zapiš default (MAX) — user může později přes dropdown zvolit jinou variantu (separátní flow)
            if (krok.SkutecnostDatum != resolved.Datum || krok.SkutecnostZdroj != SkutecnostZdrojEnum.Automat)
            {
                krok.SkutecnostDatum = resolved.Datum;
                krok.SkutecnostZdroj = SkutecnostZdrojEnum.Automat;
                updated++;
            }
        }

        if (updated > 0)
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Harmonogram sync pro záznam #{Id}: updated={U} skipManual={SM} noKandidat={NK}",
            projektovyZaznamId, updated, skipManual, noKandidat);

        return new HarmonogramSyncResult(projektovyZaznamId, updated, skipManual, noKandidat);
    }

    private async Task<IReadOnlyList<BindingKandidat>> LoadBindingKandidatiAsync(int zaznamId, CancellationToken ct)
    {
        // Join: vyjadreni_vazba (Active) → externi_odkaz → HotVyjadreni (předpokládá lokální snapshot)
        // Přesné názvy tabulek + polí je nutno sladit s existujícími entitama.
        // Tato implementace předpokládá, že `vyjadreni_vazba` má `HotVyjadreniId`, `PredikatKey`, `ExterniOdkazId`
        // a že join na HOT_VYJADRENI pro datum je přes pid (stejný pattern jako SqlVyjadreniQueryService).

        var raw = await (
            from v in _db.VyjadreniVazby.AsNoTracking()
            where v.Stav == (byte)VazbaStav.Active
            join eo in _db.ZaznamExterniOdkazy.AsNoTracking() on v.ExterniOdkazId equals eo.Id
            where eo.ZaznamId == zaznamId && eo.Cislo != null
            select new
            {
                ExterniOdkazId = eo.Id,
                eo.Cislo,
                PredikatKey = v.PredikatKey,
                DatumVyjadreni = v.DatumVyjadreni,
                TypZaznamu = eo.TypZaznamu // předpoklad: cache typZaznamu na externi_odkaz; jinak join na fingerprint
            })
            .ToListAsync(ct).ConfigureAwait(false);

        return raw.Select(x => new BindingKandidat(
            x.ExterniOdkazId, x.Cislo!, x.TypZaznamu ?? "", x.PredikatKey ?? "", x.DatumVyjadreni))
            .Where(x => !string.IsNullOrEmpty(x.TypZaznamu) && !string.IsNullOrEmpty(x.PredikatKey))
            .ToList();
    }
}
```

Poznámka: SQL field names v `LoadBindingKandidatiAsync` jsou educated guesses. Engineer musí cross-check s reálnými entity (`VyjadreniVazbaEntity`, `ZaznamExterniOdkazEntity`) a upravit dle skutečných property names. Pokud `TypZaznamu` není cachováno na externí odkaz, musí join na `HotZaznamFingerprint` nebo se přidá cache column.

- [ ] **Step 2: Unit testy (in-memory DB)**

```csharp
// PmTracker.Tests.Unit/Services/Harmonogram/HarmonogramSkutecnostSyncServiceTests.cs
// Používá InMemory EF provider + seed representative data
// Testuje:
//   1) Manual rezim skip — SkutecnostRezim=Manual neaktualizuje řádek
//   2) Auto rezim + 1 kandidát → zapíše + flipne Zdroj na Automat
//   3) Auto rezim + žádný kandidát po dříve auto-filled → null-uje (retracts)
//   4) Auto rezim + 2 kandidáti → MAX
//   5) Historicka rows zůstávají (Rezim=Auto ale Zdroj=Historicka) — přepíše se

// Pro stručnost plánu: plná implementace následuje existing pattern v PmTracker.Tests.Unit/Services/*
// Cíl: 5 test cases odpovídajících result summary.
```

- [ ] **Step 3: DI registrace**

```csharp
// PmTracker.Web/Program.cs (nebo DependencyInjection extension)
services.AddScoped<IHarmonogramSkutecnostSyncService, HarmonogramSkutecnostSyncService>();
```

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/Services/Harmonogram/HarmonogramSkutecnostSyncService.cs \
        PmTracker.Tests.Unit/Services/Harmonogram/HarmonogramSkutecnostSyncServiceTests.cs \
        PmTracker.Web/Program.cs
git commit -m "feat(harmonogram): SkutecnostSyncService — kaskádové volání binding→skutečnost"
```

---

### Task 5: Trigger points — volání z BindingRebalanceService + VyjadreniHarvestService

**Files:**
- Modify: `PmTracker.Web/Services/ServiceDesk/BindingRebalanceService.cs`
- Modify: `PmTracker.Web/Services/ServiceDesk/VyjadreniHarvestService.cs`

- [ ] **Step 1: Inject sync service do BindingRebalanceService**

Přidat `IHarmonogramSkutecnostSyncService` do constructor. Po každém commit bindingu volat `SyncZaznamAsync(zaznamId, ct)`.

```csharp
// Na konci metody která commits binding changes (SaveChangesAsync):
await _harmonogramSyncService.SyncZaznamAsync(zaznamId, ct).ConfigureAwait(false);
```

Konkrétní místo: typicky po `_db.SaveChangesAsync()` v metodách `AssignManualBinding`, `AcceptAutoBinding`, `RebalanceAsync`, atd.

- [ ] **Step 2: Trigger po harvest batch ve `VyjadreniHarvestService`**

Po re-harvest batch:
1. Zrušit všechny user manual bindings (Source=Manual) — dle C-Q4 „Re-harvest zahazuje user bindings"
2. Vytvořit nové auto bindings z freshly harvested vyjádření
3. Volat `SyncZaznamAsync` pro každý dotčený záznam

- [ ] **Step 3: Build + tests — PASS**

```bash
dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore
dotnet test PmTracker.Tests.Unit -c Release --filter "HarmonogramSkutecnost" --no-restore
```

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/Services/ServiceDesk/BindingRebalanceService.cs \
        PmTracker.Web/Services/ServiceDesk/VyjadreniHarvestService.cs
git commit -m "feat(harmonogram): trigger SkutecnostSync po binding save + re-harvest"
```

---

### Task 6: UI — switch Auto/Ručně + dropdown + badge

**Files:**
- Create: `PmTracker.Web/wwwroot/js/components/harmonogram-skutecnost-switch/switch.js`
- Create: `PmTracker.Web/wwwroot/js/components/harmonogram-skutecnost-dropdown/dropdown.js`
- Create: `PmTracker.Web/wwwroot/css/components/harmonogram-skutecnost.css`
- Modify: existing `_ScheduleBlock.cshtml` (nebo ekvivalent)
- Modify: harmonogram controller (přidat POST endpoints)
- Create: `PmTracker.Web/Models/ViewModels/Harmonogram/HarmonogramSkutecnostRowViewModel.cs`

Vzhledem k velikosti je Task 6 rozdělen na 3 sub-parts:

#### Task 6a: ViewModel + Razor renderování

- [ ] Rozšířit VM na row, přidat properties `SkutecnostRezim`, `SkutecnostZdroj`, `KandidatiJson` (pro dropdown)
- [ ] V `_ScheduleBlock.cshtml` přidat hlavičku sloupce „Skutečnost" se switch komponentou vpravo (viz CSS layout níže)
- [ ] Per-row: badge ikonka (🤖 Automat / ✍️ Manual / 📜 Historicka / — Neznamo) + datepicker (pokud rezim=Manual) nebo read-only s dropdown chevronem (pokud rezim=Auto + 2+ kandidáti)

#### Task 6b: Switch komponenta

```javascript
// harmonogram-skutecnost-switch/switch.js
// Toggle button group: [Auto] [Ručně]
// Na click posílá POST /Harmonogram/ToggleRezim { zaznamId, rezim } → server sync
```

#### Task 6c: Dropdown komponenta

```javascript
// harmonogram-skutecnost-dropdown/dropdown.js
// Chevron vedle datumu. Klik → popup se seznamem kandidátů (datum + ticket ID).
// Klik na kandidáta → POST /Harmonogram/SelectCandidate { krokId, externiOdkazId } → server update + redraw.
// Chevron hidden když Kandidati.Count ≤ 1.
```

#### CSS (shared pro 6a, 6b, 6c)

```css
/* harmonogram-skutecnost.css */
.harmonogram-skutecnost__header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 0.5rem;
    background: var(--gov-color-neutral-bg, #f2f4f7);
}

.harmonogram-skutecnost__switch {
    display: inline-flex;
    border: 1px solid var(--gov-color-neutral-border, #d0d5dd);
    border-radius: 0.375rem;
    overflow: hidden;
    font-size: 0.85rem;
}

.harmonogram-skutecnost__switch button {
    padding: 0.3rem 0.75rem;
    background: #fff;
    border: none;
    cursor: pointer;
}

.harmonogram-skutecnost__switch button[aria-pressed="true"] {
    background: var(--gov-color-primary, #1e40af);
    color: #fff;
}

.harmonogram-skutecnost__cell {
    display: inline-flex;
    align-items: center;
    gap: 0.5rem;
}

.harmonogram-skutecnost__badge {
    font-size: 0.85rem;
}

.harmonogram-skutecnost__chevron {
    cursor: pointer;
    user-select: none;
    color: var(--gov-color-text-secondary, #475467);
}

.harmonogram-skutecnost__chevron[aria-hidden="true"] { display: none; }

.harmonogram-skutecnost__dropdown {
    position: absolute;
    background: #fff;
    border: 1px solid var(--gov-color-neutral-border);
    border-radius: 0.375rem;
    box-shadow: 0 4px 12px rgba(0,0,0,0.1);
    padding: 0.5rem;
    z-index: 100;
    min-width: 16rem;
}
```

- [ ] **Step (6-final): Commit všech 3 částí**

```bash
git add ... # všechny soubory z 6a/6b/6c
git commit -m "feat(harmonogram): UI switch Auto/Ručně + dropdown kandidátů + badge zdroje"
```

---

### Task 7: Controller endpoints + audit

**Files:**
- Modify: harmonogram controller

- [ ] POST `/Harmonogram/ToggleRezim` — body `{ zaznamId, rezim }` → update `SkutecnostRezim` + volá `SyncZaznamAsync` (v Auto mode)
- [ ] POST `/Harmonogram/SelectCandidate` — body `{ krokId, externiOdkazId }` → override MAX default pro konkrétní krok; Zdroj zůstává Automat (je to volba z auto kandidátů, ne manual datum), ale zapíše se `PreferredExterniOdkazId` cache (nová column nebo ad-hoc logika — TODO zvolit)
- [ ] Autorizace: `projects.edit` (existing)
- [ ] Audit log obou akcí

- [ ] **Commit**

```bash
git commit -m "feat(harmonogram): POST endpoints ToggleRezim + SelectCandidate + audit"
```

---

### Task 8: Final validace

- [ ] Full build + test
- [ ] Manuální smoke — jdi na záznam s napojeným SD ticketem, projdi:
  1. Harmonogram má všechny tři sloupce (Plán, Skutečnost, badge)
  2. Switch defaultně „Automaticky z SD"
  3. Skutečnost je auto-filled dle matice + MAX
  4. Dropdown chevron viditelný jen u kroků s 2+ kandidáty
  5. Přepnutí na „Ručně" umožní zapsat vlastní datum → Zdroj=Manual
  6. Re-harvest zruší user manual bindings, ale NE manual datumy (Rezim=Manual)
  7. Po novém vyjádření ze SD (nebo simulaci přes manual binding) se Skutečnost updateruje kaskádově bez reload stránky (pokud je AJAX flow; jinak po refreshi)

- [ ] Final commit

---

## Deliverable

- ✅ DB migrace 1_3_10 — `SkutecnostZdroj` + `SkutecnostRezim`
- ✅ Matice NES/PMP/PNF (hardcoded, testovaná 15 test cases)
- ✅ `HarmonogramSkutecnostResolver` (pure, 6 test cases)
- ✅ `HarmonogramSkutecnostSyncService` (orchestrace + DB write, 5+ test cases)
- ✅ Trigger points: BindingRebalanceService + VyjadreniHarvestService
- ✅ UI: switch Auto/Ručně (per záznam), dropdown kandidátů (chevron hidden při ≤1), badge zdroje
- ✅ Controller POST endpoints: ToggleRezim + SelectCandidate + audit
- ✅ Respekt user intent: Rezim=Manual skip; Re-harvest zahazuje bindings (ne manual datumy)
