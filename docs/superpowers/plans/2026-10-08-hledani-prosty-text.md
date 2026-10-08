# Hledání nad čistým textem bez HTML — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Pozn. pro tento projekt:** uživatel chce **inline exekuci** v hlavní session (superpowers:executing-plans), subagenti se neosvědčili. Hotová a otestovaná práce jde rovnou do lokálního `main`, push jen na pokyn.

**Goal:** Fráze v uvozovkách najde text i přes formátování (`pes a <b>kočka</b> spali` ↔ `"pes a kočka spali"`). Hledání přestane nacházet samotné HTML značky („strong“, „span“) a zvýrazní frázi i přes tučné slovo.

**Architecture:** Popis záznamu, text vyjádření a text požadavku do výzvy dostanou sloupec s čistým textem: bez HTML, s rozbalenými entitami a s mezerami sjednocenými na jednu. Plní ho jediný háček v `PmTrackerDbContext.SaveChanges`, takže každé uložení přes EF ho drží aktuální. Stará data dopočítá jednorázově startovací služba. `RecordSearchService` hledá místo HTML nad čistými sloupci popisu a vyjádření. Sloupec požadavku zůstane zatím nevyužitý, připravený pro budoucí hledání ve výzvách. Zvýraznění v prohlížeči spojí text uvnitř jednoho bloku (odstavce, položky) přes inline formátování.

**Tech Stack:** .NET 8, EF Core (SQL Server, InMemory v unit testech), ruční SQL migrace `db_upgrade_*.sql`, vanilla JS ESM, xUnit + FluentAssertions, Testcontainers SQL (Api, Integration), Playwright .NET (E2E), `node --test` (JS).

**Spec:** samostatný spec není. Zadání uživatele z 2026-10-08 v konverzaci:
1. LIKE neumí přeskočit HTML značky, proto varianta A: ke každému formátovanému textu ukládat i čistý text.
2. Čistý text se musí plnit při **každém** uložení (nový záznam, úprava, nové i upravené vyjádření), ne jen jednorázově. Jednorázový dopočet je jen pro stará data.
3. Žádný databázový trigger: T-SQL neumí HTML spolehlivě odstranit a výsledek by se lišil od toho, co ukazuje aplikace.
4. Rozsah: hledat se bude v popisu a vyjádřeních. Text požadavku do výzvy dostane čistý text taky, aby bylo hledání ve výzvách připravené pro případné budoucí rozšíření.
5. Zvýraznění fráze přes formátování (tučné slovo) je součástí práce.
6. Související poznámky k opravám návrhů jsou v `docs/known-issues/2026-10-08-navrh-harmonogramu-opravy.md`. Tento plán je neřeší.

## Global Constraints

- **Názvy sloupců:** `projektove_zaznamy.popis_prosty_text`, `vyjadreni.text_vyjadreni_prosty_text`, `zaznam_externi_odkazy.pozadavek_prosty_text`, vše `NVARCHAR(MAX) NULL`. Vlastnosti entit: `PopisProstyText`, `TextVyjadreniProstyText`, `PozadavekProstyText`.
- **Význam hodnoty:** `NULL` znamená, že HTML je `NULL` nebo čistý text ještě nebyl dopočten. Prázdný řetězec znamená, že HTML existuje, ale nemá viditelný text (`<p></p>`).
- **Převod je jediný:** `RichTextSearchText.FromHtml` nad `RichTextContentService.ToPlainText`. Nikde jinde se HTML na text pro hledání nepřevádí.
- **Migrační skript:** `db_upgrade_1_4_7_prosty_text_hledani.sql`, idempotentní, `SET XACT_ABORT ON` + transakce, kontroluje předchozí skript (`pozadavek` z 1_4_2). Data nemigruje, sloupce zůstanou `NULL` a dopočte je aplikace.
- **Registrace skriptu:** `docs/technical/06-database-bootstrap-migrations.md` (podle něj se zakládají i testovací DB), `db_check_applied_upgrades.sql` (pořadí 360), kontrola při startu v `SqlStartupValidatorHostedService` s hláškou, který skript spustit. `PMTracker_insert_sql` se nemění, nová instalace pouští upgrady po něm.
- **Soubory DS se nikdy needitují:** `PmTracker.Web/wwwroot/assets/gov/**`.
- **Lokální dev DB** (`pmtracker-sql`, DB `PmTracker`) upgraduju sám, se zálohou. Servery nasazuje uživatel.
- **Výstup testů je česky** („Úspěšné!“, „Neúspěšné!“).
- **Baseline Api:** 4 známá selhání harmonogramu, nesouvisí: `RecordEditorControllerTests.Edit_ShouldRenderScheduleMiniGantt_WithAlignedAxis_WithoutPerStepDuplicateBars` a 3× `ProjectHarmonogramRenderTests.Detail_*`. `RecordDetailPageRenderTests.EditorBreadcrumb_UsesProjectVisibleNumber_LikeDetailPage` padá jen ve filtrovaném běhu, v plném běhu projde.
- **Git:** commity rovnou do `main` (pokyn uživatele 2026-10-07). Trailer `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Push jen na pokyn.

## Review Focus

1. **Uložení mimo háček.** Hromadné `ExecuteUpdate` nebo přímé SQL by čistý text obešlo. Dnes žádné takové místo není (ověřeno grepem 2026-10-08). Task 3 to hlídá testem, který v `PmTracker.Web` hledá `ExecuteUpdate`/`ExecuteSql` nad třemi sloupci HTML.
2. **Dopočet se zacyklí na prázdném HTML.** `<p></p>` dá prázdný text. Kdyby se uložil jako `NULL`, dopočet by ten řádek vybíral pořád dokola. Task 1 vrací pro neprázdné HTML vždy řetězec a Task 4 to pokrývá testem.
3. **Pevná mezera a zalomení ve frázi.** `pes&nbsp;a<br>kočka` musí najít `"pes a kočka"`. Pokrývá to Task 1 (sjednocení mezer) a Task 5 (integrační test).
4. **Zvýraznění nesmí spojit oddělené bloky.** Popisek a hodnota (`Vlastník:` / `Pavel`) jsou různé elementy. Spojení přes ně by rozsvítilo falešnou frázi. Task 6 spojuje jen text uvnitř jednoho blokového elementu a testuje to.
5. **Hledání HTML značek.** Dotaz „strong“ dnes najde každý záznam s tučným textem. Po změně nesmí najít nic. Pokrývá to Task 5.

---

## File Structure

| Soubor | Změna | Odpovědnost |
|---|---|---|
| `PmTracker.Web/Services/Common/RichTextSearchText.cs` | Create | HTML → čistý text pro hledání |
| `db_upgrade_1_4_7_prosty_text_hledani.sql` | Create | 3 sloupce |
| `docs/technical/06-database-bootstrap-migrations.md` | Modify | registrace skriptu |
| `db_check_applied_upgrades.sql` | Modify | otisk 1_4_7 |
| `PmTracker.Web/Models/Entities/PmTrackerEntities.cs` | Modify | 3 vlastnosti |
| `PmTracker.Web/Data/Configuration/RecordEntityConfiguration.cs`, `MeetingEntityConfiguration.cs` | Modify | mapování |
| `PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs` | Modify | kontrola sloupců |
| `PmTracker.Web/Data/PmTrackerDbContext.cs` + `PmTracker.Web/Data/RichTextSearchTextSync.cs` | Modify/Create | háček při uložení |
| `PmTracker.Web/Services/Data/RichTextSearchTextBackfillHostedService.cs` | Create | dopočet starých dat |
| `PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs` | Modify | registrace hosted service |
| `PmTracker.Web/Services/Search/RecordSearchService.cs` | Modify | LIKE nad čistým textem |
| `PmTracker.Web/wwwroot/js/modules/searchHighlight.js` | Modify | zvýraznění přes inline formátování |
| `PmTracker.Tests.Integration/TestInfrastructure/SearchSeed.cs` | Modify | seed plní čistý text |
| testy | Create/Modify | viz tasky |
| `docs/wiki/uzivatelsky-dashboard/globalni-vyhledavani.md`, `docs/superpowers/specs/2026-09-17-vyhledavani-prestavba-design.md` | Modify | dokumentace |

---

### Task 0: Baseline

- [ ] **Step 1:** `git status --short` musí být čistý; jsi na `main`.
- [ ] **Step 2:** Spusť a zapiš si výsledky:

```bash
dotnet test PmTracker.Tests.Unit 2>&1 | tail -1
dotnet test PmTracker.Tests.Api 2>&1 | grep -E "^\s+Neúspěšné |Úspěšné!|Neúspěšné!"
npm test 2>&1 | grep -E "^ℹ (pass|fail)"
```

Expected: Unit bez selhání, Api jen 4 baseline selhání, JS bez selhání.

---

### Task 1: Převod HTML na čistý text pro hledání

**Files:**
- Create: `PmTracker.Web/Services/Common/RichTextSearchText.cs`
- Test: `PmTracker.Tests.Unit/Common/RichTextSearchTextTests.cs`

**Interfaces:**
- Produces: `public static string? RichTextSearchText.FromHtml(string? html)`. Vrací `null` pro `null`, jinak čistý text se sjednocenými mezerami, případně `""`.

- [ ] **Step 1: Failing test**

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Common;

namespace PmTracker.Tests.Unit.Common;

/// <summary>Uživatel 2026-10-08: fráze se hledá i přes formátování — v čistém textu bez HTML.</summary>
public sealed class RichTextSearchTextTests
{
    [Theory]
    [InlineData("<p>pes a <strong>kočka</strong> spali</p>", "pes a kočka spali")]
    [InlineData("<p>pes&nbsp;a<br>kočka</p><p>spali</p>", "pes a kočka spali")]
    [InlineData("<ul><li>jedna</li><li>dva</li></ul>", "jedna dva")]
    [InlineData("<p>A &amp; B &lt;tag&gt;</p>", "A & B <tag>")]
    [InlineData("<p><a href=\"https://x.cz\">odkaz</a> text</p>", "odkaz text")]
    [InlineData("prostý text bez značek", "prostý text bez značek")]
    public void FromHtml_VratiTextBezZnacekSJednouMezerou(string html, string expected)
    {
        RichTextSearchText.FromHtml(html).Should().Be(expected);
    }

    [Fact]
    public void FromHtml_NullZustaneNull()
    {
        RichTextSearchText.FromHtml(null).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("<p></p>")]
    [InlineData("<p><br></p>")]
    public void FromHtml_HtmlBezTextu_JePrazdnyRetezec_NeNull(string html)
    {
        // NULL znamená „nedopočteno“ — prázdné HTML by jinak dopočet vybíral pořád dokola.
        RichTextSearchText.FromHtml(html).Should().Be(string.Empty);
    }
}
```

- [ ] **Step 2:** `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~RichTextSearchTextTests"`. Expected: build error, `RichTextSearchText` neexistuje.

- [ ] **Step 3: Implementace**

```csharp
namespace PmTracker.Web.Services.Common;

/// <summary>
/// Čistý text formátovaného obsahu pro hledání (uživatel 2026-10-08): bez HTML značek,
/// s rozbalenými entitami a s mezerami, zalomeními a pevnými mezerami sjednocenými na jednu
/// mezeru. LIKE nad ním najde frázi i přes tučné slovo nebo zalomení řádku. Jediné místo
/// převodu — ukládá ho háček v PmTrackerDbContext a dopočet starých dat.
/// </summary>
public static class RichTextSearchText
{
    // Bezstavová služba (jen statická pravidla sanitizace), sdílená instance je bezpečná.
    private static readonly RichTextContentService RichText = new();

    /// <summary>
    /// <c>null</c> jen pro <c>null</c>. HTML bez viditelného textu dá prázdný řetězec, ne
    /// <c>null</c> — NULL ve sloupci znamená „ještě nedopočteno“.
    /// </summary>
    public static string? FromHtml(string? html)
    {
        if (html is null)
        {
            return null;
        }

        return string.Join(' ', RichText.ToPlainText(html)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
```

- [ ] **Step 4:** Spusť test z kroku 2. Expected: PASS. Pokud `ToPlainText` u řádku s `<a href>` nebo `&lt;tag&gt;` vrátí něco jiného, oprav očekávání podle skutečného chování `ToPlainText` (to je jediný zdroj pravdy). Odchylku zapiš do ledgeru jako Ruling.
- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Common/RichTextSearchText.cs PmTracker.Tests.Unit/Common/RichTextSearchTextTests.cs
git commit -m "feat(hledani): převod formátovaného textu na čistý text pro hledání

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Sloupce v DB, mapování a registrace skriptu

**Files:**
- Create: `db_upgrade_1_4_7_prosty_text_hledani.sql`
- Modify: `docs/technical/06-database-bootstrap-migrations.md` (seznam za 1_4_6), `db_check_applied_upgrades.sql`, `PmTracker.Web/Models/Entities/PmTrackerEntities.cs`, `PmTracker.Web/Data/Configuration/RecordEntityConfiguration.cs`, `PmTracker.Web/Data/Configuration/MeetingEntityConfiguration.cs`, `PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs`
- Test: `PmTracker.Tests.Unit/Common/ProstyTextMigrationRegistrationTests.cs`

**Interfaces:**
- Produces: vlastnosti `ProjektovyZaznamEntity.PopisProstyText`, `VyjadreniEntity.TextVyjadreniProstyText`, `ZaznamExterniOdkazEntity.PozadavekProstyText` (`string?`); `SqlStartupValidatorHostedService.ProstyTextColumns` (`internal static readonly (string Table, string Column)[]`).

- [ ] **Step 1: Failing test**

```csharp
using System.IO;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Data;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Common;

/// <summary>db_upgrade_1_4_7 — čistý text formátovaných polí pro hledání (2026-10-08).</summary>
public sealed class ProstyTextMigrationRegistrationTests
{
    private const string MigrationFile = "db_upgrade_1_4_7_prosty_text_hledani.sql";

    [Fact]
    public void Migrace_JeVBootstrapSeznamu_AVKontrolnimSkriptu()
    {
        File.ReadAllText(ResolvePath("docs/technical/06-database-bootstrap-migrations.md"))
            .Should().Contain(MigrationFile);
        File.ReadAllText(ResolvePath("db_check_applied_upgrades.sql"))
            .Should().Contain("db_upgrade_1_4_7_prosty_text_hledani");
    }

    [Fact]
    public void Migrace_PridaTriSloupce_VTransakci_AHlidaPredchoziSkript()
    {
        var sql = File.ReadAllText(ResolvePath(MigrationFile));

        sql.Should().Contain("SET XACT_ABORT ON").And.Contain("BEGIN TRANSACTION").And.Contain("COMMIT TRANSACTION");
        sql.Should().Contain("popis_prosty_text").And.Contain("text_vyjadreni_prosty_text").And.Contain("pozadavek_prosty_text");
        sql.Should().Contain("COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'pozadavek')",
            "pozadavek zakládá 1_4_2 — bez něj má skript skončit srozumitelnou chybou");
    }

    [Fact]
    public void StartovaciKontrola_VyzadujeVsechnySloupce()
    {
        SqlStartupValidatorHostedService.ProstyTextColumns.Should().BeEquivalentTo(new[]
        {
            ("dbo.projektove_zaznamy", "popis_prosty_text"),
            ("dbo.vyjadreni", "text_vyjadreni_prosty_text"),
            ("dbo.zaznam_externi_odkazy", "pozadavek_prosty_text"),
        });
    }

    [Theory]
    [InlineData(typeof(ProjektovyZaznamEntity), nameof(ProjektovyZaznamEntity.PopisProstyText), "popis_prosty_text")]
    [InlineData(typeof(VyjadreniEntity), nameof(VyjadreniEntity.TextVyjadreniProstyText), "text_vyjadreni_prosty_text")]
    [InlineData(typeof(ZaznamExterniOdkazEntity), nameof(ZaznamExterniOdkazEntity.PozadavekProstyText), "pozadavek_prosty_text")]
    public void Entity_JeNamapovanaNaSloupec(Type entity, string property, string column)
    {
        var opts = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var db = new PmTrackerDbContext(opts);

        var prop = db.Model.FindEntityType(entity)!.FindProperty(property);
        prop.Should().NotBeNull();
        prop!.GetColumnName().Should().Be(column);
    }
}
```

- [ ] **Step 2:** `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ProstyTextMigrationRegistrationTests"`. Expected: build error (chybí vlastnosti a `ProstyTextColumns`).

- [ ] **Step 3: SQL skript** `db_upgrade_1_4_7_prosty_text_hledani.sql`:

```sql
-- =============================================================================
-- db_upgrade_1_4_7_prosty_text_hledani.sql
--
-- Čistý text formátovaných polí pro hledání (uživatel 2026-10-08).
--
-- KONTEXT: popis záznamu, text vyjádření a text požadavku do výzvy jsou HTML z editoru.
-- LIKE nad HTML nenajde frázi přes formátování ("pes a <b>kočka</b>") a naopak najde
-- samotné značky ("strong"). Aplikace k nim nově ukládá čistý text a hledá nad ním.
--
-- ROZSAH MIGRACE:
--   * Přidá NVARCHAR(MAX) NULL sloupce:
--       dbo.projektove_zaznamy.popis_prosty_text
--       dbo.vyjadreni.text_vyjadreni_prosty_text
--       dbo.zaznam_externi_odkazy.pozadavek_prosty_text
--   * Data NEMIGRUJE. Čistý text dopočte aplikace při prvním startu (T-SQL neumí HTML
--     spolehlivě odstranit) a dál ho plní při každém uložení.
--   * NEDOTKNE se žádné jiné tabulky ani indexu.
--
-- Idempotence: skript lze spustit opakovaně.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

PRINT N'[1.4.7] Čistý text pro hledání — start';

IF COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'pozadavek') IS NULL
BEGIN
    RAISERROR(N'Chybí sloupec dbo.zaznam_externi_odkazy.pozadavek — nejdřív spusť db_upgrade_1_4_2_externi_odkaz_pozadavek.sql.', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.projektove_zaznamy', N'popis_prosty_text') IS NULL
BEGIN
    ALTER TABLE dbo.projektove_zaznamy ADD popis_prosty_text NVARCHAR(MAX) NULL;
    PRINT N'[1.4.7] dbo.projektove_zaznamy.popis_prosty_text přidán';
END

IF COL_LENGTH(N'dbo.vyjadreni', N'text_vyjadreni_prosty_text') IS NULL
BEGIN
    ALTER TABLE dbo.vyjadreni ADD text_vyjadreni_prosty_text NVARCHAR(MAX) NULL;
    PRINT N'[1.4.7] dbo.vyjadreni.text_vyjadreni_prosty_text přidán';
END

IF COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'pozadavek_prosty_text') IS NULL
BEGIN
    ALTER TABLE dbo.zaznam_externi_odkazy ADD pozadavek_prosty_text NVARCHAR(MAX) NULL;
    PRINT N'[1.4.7] dbo.zaznam_externi_odkazy.pozadavek_prosty_text přidán';
END

COMMIT TRANSACTION;

PRINT N'[1.4.7] hotovo — čistý text dopočte aplikace při startu';
```

- [ ] **Step 4: Registrace skriptu.**
  - `docs/technical/06-database-bootstrap-migrations.md`: za řádek `35. \`db_upgrade_1_4_6_richtext_unicode.sql\`` vlož `36. \`db_upgrade_1_4_7_prosty_text_hledani.sql\`` a seed přečísluj na `37.`.
  - `db_check_applied_upgrades.sql`: za blok `@richtextUnicode` přidej

```sql
DECLARE @prostyText INT = CASE
    WHEN COL_LENGTH(N'dbo.projektove_zaznamy', N'popis_prosty_text') IS NOT NULL
     AND COL_LENGTH(N'dbo.vyjadreni', N'text_vyjadreni_prosty_text') IS NOT NULL
     AND COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'pozadavek_prosty_text') IS NOT NULL
    THEN 1 ELSE 0 END;
    -- 1_4_7: čistý text formátovaných polí pro hledání
```

    a za řádek 350 (`db_upgrade_1_4_6_richtext_unicode`) přidej

```sql
INSERT @r VALUES (360, N'db_upgrade_1_4_7_prosty_text_hledani',
    CASE WHEN @prostyText = 1 THEN N'APLIKOVÁN' ELSE N'CHYBÍ' END,
    N'sloupce popis_prosty_text, text_vyjadreni_prosty_text a pozadavek_prosty_text (bez nich aplikace nenastartuje)');
```

- [ ] **Step 5: Entity a mapování.**
  - `PmTrackerEntities.cs`: v `ProjektovyZaznamEntity` za `public string? Popis { get; set; }` přidej:

```csharp
    /// <summary>Čistý text popisu pro hledání (RichTextSearchText); plní PmTrackerDbContext při uložení.</summary>
    public string? PopisProstyText { get; set; }
```

    Ve `VyjadreniEntity` za `TextVyjadreni` přidej `TextVyjadreniProstyText` se stejným komentářem a v `ZaznamExterniOdkazEntity` za `Pozadavek` přidej `PozadavekProstyText`. U požadavku komentář doplň větou: „Zatím se nehledá — připraveno pro hledání ve výzvách (uživatel 2026-10-08).“
  - `RecordEntityConfiguration.cs`: za `builder.Property(x => x.Popis).HasColumnName("popis");` přidej `builder.Property(x => x.PopisProstyText).HasColumnName("popis_prosty_text");` a za `Pozadavek` přidej `builder.Property(x => x.PozadavekProstyText).HasColumnName("pozadavek_prosty_text");`
  - `MeetingEntityConfiguration.cs`: za `TextVyjadreni` přidej `builder.Property(x => x.TextVyjadreniProstyText).HasColumnName("text_vyjadreni_prosty_text");`

- [ ] **Step 6: Kontrola při startu.** V `SqlStartupValidatorHostedService` přidej pole

```csharp
    /// <summary>
    /// Sloupce čistého textu pro hledání (db_upgrade_1_4_7). Bez nich spadne každé uložení
    /// záznamu nebo vyjádření, proto je start musí ohlásit i s názvem skriptu.
    /// </summary>
    internal static readonly (string Table, string Column)[] ProstyTextColumns =
    {
        ("dbo.projektove_zaznamy", "popis_prosty_text"),
        ("dbo.vyjadreni", "text_vyjadreni_prosty_text"),
        ("dbo.zaznam_externi_odkazy", "pozadavek_prosty_text"),
    };
```

    a za kontrolu `dbo.zaznam_edit_zamek` vlož

```csharp
        foreach (var (table, column) in ProstyTextColumns)
        {
            if (!await HasColumnAsync(dbContext, table, column, ct))
            {
                throw new InvalidOperationException(
                    $"V DB chybí sloupec {table}.{column}. Spusťte db_upgrade_1_4_7_prosty_text_hledani.sql.");
            }
        }
```

- [ ] **Step 7:** Spusť test z kroku 2. Expected: PASS.
- [ ] **Step 8:** Ověř, že testovací DB se zakládají se skriptem: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~LayoutGovHeaderRenderTests"`. Expected: PASS (fixture spustí nový skript, aplikace nastartuje).
- [ ] **Step 9: Commit** (všechny soubory tasku): `feat(db): sloupce s čistým textem popisu, vyjádření a požadavku (db_upgrade_1_4_7)`.

---

### Task 3: Háček při uložení

**Files:**
- Create: `PmTracker.Web/Data/RichTextSearchTextSync.cs`
- Modify: `PmTracker.Web/Data/PmTrackerDbContext.cs`
- Test: `PmTracker.Tests.Unit/Data/RichTextSearchTextSyncTests.cs`

**Interfaces:**
- Consumes: `RichTextSearchText.FromHtml` (Task 1), vlastnosti z Tasku 2.
- Produces: `internal static void RichTextSearchTextSync.Apply(ChangeTracker tracker)`.

- [ ] **Step 1: Failing test**

```csharp
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Data;

/// <summary>Uživatel 2026-10-08: čistý text se plní při každém uložení, ne jen jednorázově.</summary>
public sealed class RichTextSearchTextSyncTests
{
    private static PmTrackerDbContext Db() => new(new DbContextOptionsBuilder<PmTrackerDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task NovyZaznam_AUpravaPopisu_PlniCistyText()
    {
        await using var db = Db();
        var zaznam = new ProjektovyZaznamEntity { Id = 1, Nazev = "N", Popis = "<p>pes a <b>kočka</b></p>" };
        db.ProjektoveZaznamy.Add(zaznam);
        await db.SaveChangesAsync();
        zaznam.PopisProstyText.Should().Be("pes a kočka");

        zaznam.Popis = "<p>jen <i>pes</i></p>";
        await db.SaveChangesAsync();
        zaznam.PopisProstyText.Should().Be("jen pes");

        zaznam.Popis = null;
        db.SaveChanges();
        zaznam.PopisProstyText.Should().BeNull();
    }

    [Fact]
    public async Task Vyjadreni_APozadavek_PlniCistyText()
    {
        await using var db = Db();
        var vyjadreni = new VyjadreniEntity { Id = 1, ZaznamId = 1, JednaniId = 1, TextVyjadreni = "<p>A&nbsp;<strong>B</strong></p>" };
        var odkaz = new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 1, TypOdkazuId = 1, Cislo = "1", Pozadavek = "<p>text <u>výzvy</u></p>" };
        db.Vyjadreni.Add(vyjadreni);
        db.ZaznamExterniOdkazy.Add(odkaz);
        await db.SaveChangesAsync();

        vyjadreni.TextVyjadreniProstyText.Should().Be("A B");
        odkaz.PozadavekProstyText.Should().Be("text výzvy");

        vyjadreni.TextVyjadreni = "<p>nové</p>";
        await db.SaveChangesAsync();
        vyjadreni.TextVyjadreniProstyText.Should().Be("nové");
    }

    [Fact]
    public async Task UlozeniBezZmenyHtml_CistyTextNeprepisuje()
    {
        await using var db = Db();
        var zaznam = new ProjektovyZaznamEntity { Id = 1, Nazev = "N", Popis = "<p>pes</p>" };
        db.ProjektoveZaznamy.Add(zaznam);
        await db.SaveChangesAsync();

        zaznam.PopisProstyText = "ručně";   // simulace: jiný zdroj; háček ho bez změny HTML nemá přepsat
        zaznam.Nazev = "Jiný název";
        await db.SaveChangesAsync();

        zaznam.PopisProstyText.Should().Be("ručně");
    }

    [Fact]
    public void ZadnyZapisNeobchaziHacek()
    {
        // ExecuteUpdate / přímé SQL by čistý text obešlo — nad HTML sloupci se nesmí použít.
        var zdroje = Directory.GetFiles(ResolvePath("PmTracker.Web"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(File.ReadAllText);
        zdroje.Should().NotContain(s => Regex.IsMatch(s,
            @"ExecuteUpdate[^;]*(Popis|TextVyjadreni|Pozadavek)\b|UPDATE\s+dbo\.(projektove_zaznamy|vyjadreni|zaznam_externi_odkazy)\s+SET[^;]*(popis|text_vyjadreni|pozadavek)\s*=",
            RegexOptions.IgnoreCase));
    }
}
```

- [ ] **Step 2:** `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~RichTextSearchTextSyncTests"`. Expected: první dva a třetí test FAIL (čistý text `null`), čtvrtý PASS (pojistka). Pokud InMemory odmítne entitu kvůli povinným vlastnostem, doplň do testovacích entit jen ty, které hlásí.

- [ ] **Step 3: Implementace** `PmTracker.Web/Data/RichTextSearchTextSync.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Data;

/// <summary>
/// Drží čistý text formátovaných polí v souladu s HTML (uživatel 2026-10-08). Volá ho
/// PmTrackerDbContext před každým uložením, takže pokryje všechna místa zápisu: založení
/// a úpravu záznamu (i ze schváleného návrhu), přidání a úpravu vyjádření, externí vazby.
/// Přepočítává jen při založení nebo změně HTML.
/// </summary>
internal static class RichTextSearchTextSync
{
    public static void Apply(ChangeTracker tracker)
    {
        tracker.DetectChanges();

        foreach (var entry in tracker.Entries<ProjektovyZaznamEntity>())
        {
            if (HtmlChanged(entry, nameof(ProjektovyZaznamEntity.Popis)))
            {
                entry.Entity.PopisProstyText = RichTextSearchText.FromHtml(entry.Entity.Popis);
            }
        }

        foreach (var entry in tracker.Entries<VyjadreniEntity>())
        {
            if (HtmlChanged(entry, nameof(VyjadreniEntity.TextVyjadreni)))
            {
                entry.Entity.TextVyjadreniProstyText = RichTextSearchText.FromHtml(entry.Entity.TextVyjadreni);
            }
        }

        foreach (var entry in tracker.Entries<ZaznamExterniOdkazEntity>())
        {
            if (HtmlChanged(entry, nameof(ZaznamExterniOdkazEntity.Pozadavek)))
            {
                entry.Entity.PozadavekProstyText = RichTextSearchText.FromHtml(entry.Entity.Pozadavek);
            }
        }
    }

    private static bool HtmlChanged<T>(EntityEntry<T> entry, string htmlProperty) where T : class
        => entry.State == EntityState.Added
           || (entry.State == EntityState.Modified && entry.Property(htmlProperty).IsModified);
}
```

  V `PmTrackerDbContext.cs` za konstruktor přidej:

```csharp
    // Čistý text formátovaných polí pro hledání — viz RichTextSearchTextSync. Ostatní
    // přetížení SaveChanges/SaveChangesAsync volají tato dvě.
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RichTextSearchTextSync.Apply(ChangeTracker);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RichTextSearchTextSync.Apply(ChangeTracker);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
```

- [ ] **Step 4:** Spusť test z kroku 2. Expected: PASS 4/4.
- [ ] **Step 5:** `dotnet test PmTracker.Tests.Unit`. Expected: bez selhání (háček nesmí rozbít žádné jiné ukládání).
- [ ] **Step 6: Commit:** `feat(hledani): čistý text se plní při každém uložení (háček v PmTrackerDbContext)`.

---

### Task 4: Dopočet starých dat při startu

**Files:**
- Create: `PmTracker.Web/Services/Data/RichTextSearchTextBackfillHostedService.cs`
- Modify: `PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs:221` (registrace hned za `SqlStartupValidatorHostedService`)
- Modify: `PmTracker.Tests.Integration/TestInfrastructure/SearchSeed.cs`
- Test: `PmTracker.Tests.Integration/DataStore/RichTextSearchTextBackfillTests.cs`

**Interfaces:**
- Consumes: Task 1–3.
- Produces: `public static Task<int> RichTextSearchTextBackfillHostedService.BackfillAsync(PmTrackerDbContext db, CancellationToken ct)` — vrací počet doplněných řádků.

- [ ] **Step 1: Seed plní čistý text jako aplikace.** `SearchSeed` vkládá přímým SQL, takže háček z Tasku 3 neproběhne. V `AddRecordAsync` přidej do INSERTu sloupec `popis_prosty_text` s hodnotou `withPlainText ? RichTextSearchText.FromHtml(popis) : null` (nový volitelný parametr `bool withPlainText = true`). Stejně `AddStatementAsync` → `text_vyjadreni_prosty_text`. Komentář: „Jako aplikace: čistý text plní PmTrackerDbContext; přímý INSERT ho musí doplnit sám. withPlainText: false = data z doby před 1_4_7.“

- [ ] **Step 2: Failing test** (Integration, reálná DB, data vložená přímým SQL bez čistého textu jako stará data). Podle vzoru `RichTextUnicodeMigrationTests` a `RecordSearchServiceTests` použij `_fixture.CreateDatabaseAsync` a `SearchSeed`:

```csharp
[Fact]
public async Task Backfill_DoplniJenChybejici_ANezacykliSeNaPrazdnemHtml()
{
    var db = await _fixture.CreateDatabaseAsync("backfill_prosty_text");
    var seed = await SearchSeed.CreateAsync(db.ConnectionString);
    var zaznamId = await seed.AddRecordAsync(seed.ProjektId, "Starý", popis: "<p>pes a <b>kočka</b></p>", withPlainText: false);
    var prazdnyId = await seed.AddRecordAsync(seed.ProjektId, "Prázdný", popis: "<p></p>", withPlainText: false);
    var jednaniId = await seed.AddMeetingAsync(seed.ProjektId, cisloJednani: 1);
    var vyjadreniId = await seed.AddStatementAsync(zaznamId, jednaniId, "<p>A&nbsp;<i>B</i></p>", withPlainText: false);

    await using var ctx = CreateDbContext(db.ConnectionString);
    var doplneno = await RichTextSearchTextBackfillHostedService.BackfillAsync(ctx, default);

    doplneno.Should().Be(3);
    (await ctx.ProjektoveZaznamy.FindAsync(zaznamId))!.PopisProstyText.Should().Be("pes a kočka");
    (await ctx.ProjektoveZaznamy.FindAsync(prazdnyId))!.PopisProstyText.Should().Be(string.Empty);
    (await ctx.Vyjadreni.FindAsync(vyjadreniId))!.TextVyjadreniProstyText.Should().Be("A B");

    (await RichTextSearchTextBackfillHostedService.BackfillAsync(ctx, default))
        .Should().Be(0, "druhý běh nemá co dělat — prázdné HTML dalo \"\", ne NULL");
}
```

  `CreateDbContext` je helper testovací třídy:
  `new PmTrackerDbContext(new DbContextOptionsBuilder<PmTrackerDbContext>().UseSqlServer(cs).Options)`.

- [ ] **Step 3:** `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~RichTextSearchTextBackfillTests"`. Expected: build error (služba neexistuje).

- [ ] **Step 4: Implementace**

```csharp
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services.Data;

/// <summary>
/// Jednorázový dopočet čistého textu pro data uložená před db_upgrade_1_4_7 (uživatel
/// 2026-10-08). Běží při startu hned po kontrole schématu a doplní jen řádky, kde HTML je
/// a čistý text chybí (NULL). Po prvním běhu nemá co dělat. Nové a upravené texty plní
/// PmTrackerDbContext při uložení.
/// </summary>
public sealed class RichTextSearchTextBackfillHostedService : IHostedService
{
    private const int Davka = 200;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RichTextSearchTextBackfillHostedService> _logger;

    public RichTextSearchTextBackfillHostedService(
        IServiceScopeFactory scopeFactory, ILogger<RichTextSearchTextBackfillHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
        var doplneno = await BackfillAsync(db, ct);
        if (doplneno > 0)
        {
            _logger.LogInformation("Čistý text pro hledání doplněn u {Pocet} řádků.", doplneno);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    public static async Task<int> BackfillAsync(PmTrackerDbContext db, CancellationToken ct)
    {
        var celkem = 0;

        celkem += await DoplnAsync(
            db.ProjektoveZaznamy.Where(z => z.Popis != null && z.PopisProstyText == null),
            z => z.PopisProstyText = RichTextSearchText.FromHtml(z.Popis), db, ct);
        celkem += await DoplnAsync(
            db.Vyjadreni.Where(v => v.TextVyjadreni != null && v.TextVyjadreniProstyText == null),
            v => v.TextVyjadreniProstyText = RichTextSearchText.FromHtml(v.TextVyjadreni), db, ct);
        celkem += await DoplnAsync(
            db.ZaznamExterniOdkazy.Where(o => o.Pozadavek != null && o.PozadavekProstyText == null),
            o => o.PozadavekProstyText = RichTextSearchText.FromHtml(o.Pozadavek), db, ct);

        return celkem;
    }

    // Vybírá vždy znovu první dávku chybějících: doplněné řádky z dotazu vypadnou, protože
    // FromHtml pro neprázdné HTML vrátí řetězec (i prázdný), nikdy NULL.
    private static async Task<int> DoplnAsync<T>(
        IQueryable<T> chybejici, Action<T> dopln, PmTrackerDbContext db, CancellationToken ct)
        where T : class
    {
        var pocet = 0;
        while (true)
        {
            var davka = await chybejici.Take(Davka).ToListAsync(ct);
            if (davka.Count == 0)
            {
                return pocet;
            }

            davka.ForEach(dopln);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            pocet += davka.Count;
        }
    }
}
```

  Registrace v `DataStoreServiceCollectionExtensions.cs` hned za `services.AddHostedService<SqlStartupValidatorHostedService>();`:

```csharp
        // Až po kontrole schématu — bez sloupců 1_4_7 by dopočet spadl na SQL chybě
        // místo srozumitelné hlášky validátoru. Hosted services startují v pořadí registrace.
        services.AddHostedService<RichTextSearchTextBackfillHostedService>();
```

- [ ] **Step 5:** Spusť test z kroku 3. Expected: PASS.
- [ ] **Step 6:** `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~RecordSearchServiceTests"`. Expected: PASS (seed s čistým textem nic nerozbil; hledání zatím běží nad HTML).
- [ ] **Step 7: Commit:** `feat(hledani): jednorázový dopočet čistého textu pro stará data`.

---

### Task 5: Hledání nad čistým textem

**Files:**
- Modify: `PmTracker.Web/Services/Search/RecordSearchService.cs`
- Modify: `PmTracker.Tests.Integration/DataStore/RecordSearchServiceTests.cs`

**Interfaces:**
- Consumes: Task 1–3. `RecordSearchService` přestane potřebovat `IRichTextContentService` — odeber ji z konstruktoru a uprav `CreateService` v `RecordSearchServiceTests` (řádek s `new RecordSearchService(db, new ProjectVisibilityResolver(), new RichTextContentService(), …)`).

- [ ] **Step 1:** `SearchSeed` už čistý text plní (Task 4, krok 1).
- [ ] **Step 2: Failing testy** v `RecordSearchServiceTests`:

```csharp
/// <summary>Uživatel 2026-10-08: fráze se najde i přes formátování a zalomení.</summary>
[Fact]
public async Task Hledani_FrazePresFormatovani_SeNajde()
{
    var db = await _fixture.CreateDatabaseAsync("search_fraze_format");
    var seed = await SearchSeed.CreateAsync(db.ConnectionString);
    var zaznamId = await seed.AddRecordAsync(seed.ProjektId, "Nesouvisející",
        popis: "<p>Dnes pes a <strong>kočka</strong><br>spali</p>");
    var jednaniId = await seed.AddMeetingAsync(seed.ProjektId, cisloJednani: 3);
    var druhyId = await seed.AddRecordAsync(seed.ProjektId, "Jiný");
    await seed.AddStatementAsync(druhyId, jednaniId, "<p>Rozhodnuto o <em>řešení</em> zálohy</p>");

    var service = CreateService(db.ConnectionString);

    var popis = await service.SearchAsync("\"pes a kočka spali\"", User(isSuperAdmin: true), 7, default);
    var item = popis.Categories.Single().Items.Single();
    item.ZaznamId.Should().Be(zaznamId);
    item.Snippet!.Match.Should().Be("pes a kočka spali");

    var vyjadreni = await service.SearchAsync("\"o řešení zálohy\"", User(isSuperAdmin: true), 7, default);
    vyjadreni.Categories.Single().Items.Single().ZaznamId.Should().Be(druhyId);
}

/// <summary>Hledání nesmí najít samotné HTML značky (dřív „strong“ našlo vše tučné).</summary>
[Theory]
[InlineData("strong")]
[InlineData("href")]
public async Task Hledani_NenajdeHtmlZnacky(string dotaz)
{
    var db = await _fixture.CreateDatabaseAsync("search_html_znacky_" + dotaz);
    var seed = await SearchSeed.CreateAsync(db.ConnectionString);
    await seed.AddRecordAsync(seed.ProjektId, "Záznam",
        popis: "<p>text <strong>tučně</strong> <a href=\"https://x.cz\">odkaz</a></p>");

    var result = await CreateService(db.ConnectionString)
        .SearchAsync(dotaz, User(isSuperAdmin: true), 7, default);

    result.TotalCount.Should().Be(0);
}
```

- [ ] **Step 3:** `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~RecordSearchServiceTests"`. Expected: oba nové testy FAIL (fráze nenalezena; „strong“ nalezeno), ostatní PASS.
- [ ] **Step 4: Implementace** v `RecordSearchService`:
  - v podmínce hledání nahraď `(z.Popis != null && EF.Functions.Like(EF.Functions.Collate(z.Popis, coll), pattern))` za `(z.PopisProstyText != null && EF.Functions.Like(EF.Functions.Collate(z.PopisProstyText, coll), pattern))` a u vyjádření `v.TextVyjadreni` za `v.TextVyjadreniProstyText` (s `!= null`);
  - v projekci nahraď `z.Popis` za `z.PopisProstyText` a `v.TextVyjadreni` za `v.TextVyjadreniProstyText`;
  - náhled: `SearchQueryText.BuildSnippet(row.PopisProstyText, terms)` a `SearchQueryText.BuildSnippet(v.TextVyjadreniProstyText, terms)`;
  - smaž metodu `PlainText` a pole `_richText` i parametr konstruktoru; komentář nad podmínkou: „Hledá se v čistém textu (bez HTML) — fráze najde text i přes formátování a značky samotné se nenajdou (uživatel 2026-10-08).“
- [ ] **Step 5:** Spusť test z kroku 3. Expected: PASS všech testů třídy.
- [ ] **Step 6:** `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~Search"`. Expected: PASS (Api seeduje přes EF, háček čistý text doplní).
- [ ] **Step 7: Commit:** `feat(hledani): hledání v popisu a vyjádřeních nad čistým textem`.

---

### Task 6: Zvýraznění fráze přes formátování

**Files:**
- Modify: `PmTracker.Web/wwwroot/js/modules/searchHighlight.js`
- Modify: `tests/js/dom/searchHighlightTerms.test.js`
- Modify: `PmTracker.Tests.E2E/Scenarios/SearchResultTargetScenariosTests.cs`

**Interfaces:**
- Consumes: `buildHighlightPattern(terms)` (existuje).
- Produces: `export function findHighlightRanges(texts, pattern)` → `Array<{ index, start, end }>`.

- [ ] **Step 1: Failing JS test** (přidat do `searchHighlightTerms.test.js`, import rozšířit o `findHighlightRanges`):

```js
test("fráze přes tučné slovo — úseky po textových uzlech", () => {
    const texts = ["Rozhodnuto o ", "řešení", " zálohy"];
    assert.deepEqual(
        findHighlightRanges(texts, buildHighlightPattern(["o řešení zálohy"])),
        [{ index: 0, start: 11, end: 13 }, { index: 1, start: 0, end: 6 }, { index: 2, start: 0, end: 7 }]);
});

test("slova v různých uzlech zůstanou samostatné shody", () => {
    assert.deepEqual(
        findHighlightRanges(["Rozhodnuto o ", "řešení", " zálohy"], buildHighlightPattern(["řešení", "zálohy"])),
        [{ index: 1, start: 0, end: 6 }, { index: 2, start: 1, end: 7 }]);
});
```

- [ ] **Step 2:** `node --test tests/js/dom/searchHighlightTerms.test.js`. Expected: `does not provide an export named 'findHighlightRanges'`.
- [ ] **Step 3: Failing E2E test** v `SearchResultTargetScenariosTests` (seed už má `Rozhodnuto o <strong>řešení</strong> zálohy`):

```csharp
/// <summary>Uživatel 2026-10-08: fráze se podsvítí i přes tučné slovo.</summary>
[Fact]
public async Task OdkazSFraziPresFormatovani_PodsvitiCelouFrazi()
{
    var (recordId, targetCommentId) = await CreateRecordWithCommentsAsync();
    var page = await _fixture.NewPageAsync();
    try
    {
        await page.GotoAsync(RecordUrl(recordId, $"&vyjadreniId={targetCommentId}&hl={Uri.EscapeDataString("\"o řešení zálohy\"")}"));
        var target = page.Locator($".record-card[data-record-id='{recordId}'] [data-comment-id='{targetCommentId}']");
        var marks = target.Locator("[data-comment-text] mark.app-search-flash");
        // Tři úseky (před, uvnitř a za <strong>); Playwright text porovnává bez krajních mezer.
        await Expect(marks).ToHaveTextAsync(new[] { "o", "řešení", "zálohy" });
    }
    finally
    {
        await page.Context.CloseAsync();
        await DeleteRecordAsync(recordId);
    }
}
```

  Spusť ho: `dotnet test PmTracker.Tests.E2E --filter "FullyQualifiedName~OdkazSFraziPresFormatovani"`. Expected: FAIL (`[]`).

- [ ] **Step 4: Implementace** v `searchHighlight.js`:
  - přidej konstantu a pure funkci:

```js
// Text uvnitř jednoho z těchto elementů je souvislý (tučné slovo, odkaz, kurzíva jsou jen
// inline). Mezi nimi se nespojuje — popisek a hodnota nesmí dát falešnou frázi.
const BLOCK_SELECTOR = "p, li, dd, dt, td, th, h1, h2, h3, h4, h5, h6, blockquote, pre, div, section, article, header, footer";

/**
 * Shody vzoru v souvislém textu bloku rozloženém do více textových uzlů. Vrací úseky po
 * uzlech: index uzlu a rozsah v jeho textu. Jedna fráze přes tučné slovo = víc úseků.
 */
export function findHighlightRanges(texts, pattern) {
    const joined = texts.join("");
    const ranges = [];
    for (const match of joined.matchAll(pattern)) {
        if (match[0].length === 0) {
            continue;
        }
        const matchStart = match.index;
        const matchEnd = match.index + match[0].length;
        let offset = 0;
        texts.forEach((text, index) => {
            const start = Math.max(matchStart, offset);
            const end = Math.min(matchEnd, offset + text.length);
            if (start < end) {
                ranges.push({ index, start: start - offset, end: end - offset });
            }
            offset += text.length;
        });
    }
    return ranges;
}
```

  - v `highlightSearchTerms` nahraď sběr a obalování: uzly, které projdou stávajícím filtrem (`SKIP_SELECTOR`, `isInsideCustomElement`), seskup podle `parent.closest(BLOCK_SELECTOR) ?? root` (Map v pořadí dokumentu). Pro každou skupinu `findHighlightRanges(nodes.map((n) => n.data), pattern)`, úseky rozděl podle `index` a každý uzel s úseky nahraď fragmentem: text před úsekem, `<mark class="app-search-flash">` s textem úseku, text za posledním úsekem. Pole `marks` a časovač mizení zůstávají beze změny. Předběžný test `pattern.test(node.data)` jednotlivých uzlů odstraň, protože fráze přes uzly by v něm neprošla.
- [ ] **Step 5:** `node --test tests/js/dom/searchHighlightTerms.test.js` a `npm test`. Expected: PASS.
- [ ] **Step 6:** `dotnet test PmTracker.Tests.E2E --filter "FullyQualifiedName~SearchResultTargetScenariosTests"`. Expected: PASS všech tří testů, tedy i stávajících se slovy a s frází v názvu.
- [ ] **Step 7: Commit:** `feat(hledani): podsvícení fráze i přes formátování textu`.

---

### Task 7: Lokální DB, ověření a dokumentace

- [ ] **Step 1: Upgrade lokální dev DB** (se zálohou):

```bash
/opt/homebrew/bin/sqlcmd -S localhost,1433 -U sa -P 'PmTracker!2026' -Ndisable -d master -Q "BACKUP DATABASE [PmTracker] TO DISK = N'/var/opt/mssql/data/PmTracker_pred_1_4_7_$(date +%Y%m%d).bak' WITH INIT, COPY_ONLY"
/opt/homebrew/bin/sqlcmd -S localhost,1433 -U sa -P 'PmTracker!2026' -Ndisable -d PmTracker -b -i db_upgrade_1_4_7_prosty_text_hledani.sql
/opt/homebrew/bin/sqlcmd -S localhost,1433 -U sa -P 'PmTracker!2026' -Ndisable -d PmTracker -W -s '|' -i db_check_applied_upgrades.sql | grep 1_4_7
```

Expected: `APLIKOVÁN`.

- [ ] **Step 2: Spuštění aplikace** (dopočet proběhne při startu):

```bash
ASPNETCORE_ENVIRONMENT=Development ConnectionStrings__PmTrackerDb='Server=localhost,1433;Database=PmTracker;User Id=sa;Password=PmTracker!2026;TrustServerCertificate=True;Encrypt=True' PmTracker__Data__Provider=SqlServer dotnet run --project PmTracker.Web --urls http://localhost:5071 --no-launch-profile
```

  Ověř v logu „Čistý text pro hledání doplněn u N řádků“ a v DB `SELECT COUNT(*) FROM projektove_zaznamy WHERE popis IS NOT NULL AND popis_prosty_text IS NULL` = 0 (totéž pro `vyjadreni`). Restart aplikace už nic nedoplní.
- [ ] **Step 3: Ruční kontrola:** v editoru záznamu napiš do popisu „test <tučně>fráze</tučně> hledání“. Uložení musí doplnit čistý text (SQL dotaz). `/Search/Suggest?q=%22test%20fráze%20hledání%22&asUser=1` vrátí záznam a `/Search/Suggest?q=strong&asUser=1` ne. Zastav aplikaci.
- [ ] **Step 4: Dokumentace.**
  - `docs/wiki/uzivatelsky-dashboard/globalni-vyhledavani.md`: odrážku **Fráze přes formátování** v Limitech nahraď textem „Fráze v uvozovkách se najde i přes formátování (tučné slovo, odkaz) a zalomení řádku. Hledá se v textu bez HTML, takže hledání slova jako „strong“ nenajde formátovací značky.“
  - `docs/superpowers/specs/2026-09-17-vyhledavani-prestavba-design.md`: odrážku o frázích doplň: „Od 2026-10-08 se hledá nad sloupci čistého textu (`popis_prosty_text`, `text_vyjadreni_prosty_text`; `pozadavek_prosty_text` připravený pro výzvy), plněnými háčkem `RichTextSearchTextSync` při uložení a jednorázovým dopočtem při startu. Omezení „fráze přes formátování“ odpadá.“
  - `docs/technical/06-database-bootstrap-migrations.md` už skript obsahuje (Task 2).
- [ ] **Step 5: Celé sady:**

```bash
dotnet test PmTracker.Tests.Unit 2>&1 | tail -1
npm test 2>&1 | grep -E "^ℹ (pass|fail)"
dotnet test PmTracker.Tests.Api 2>&1 | grep -E "^\s+Neúspěšné |Úspěšné!|Neúspěšné!"
dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~Search|FullyQualifiedName~RichText" 2>&1 | grep -E "^\s+Neúspěšné |Úspěšné!|Neúspěšné!"
dotnet test PmTracker.Tests.E2E --filter "FullyQualifiedName~Search|FullyQualifiedName~Header" 2>&1 | grep -E "^\s+Neúspěšné |Úspěšné!|Neúspěšné!"
```

  Expected: bez nových selhání oproti baseline (Task 0).
- [ ] **Step 6: Commit** dokumentace: `docs(hledani): fráze přes formátování, čistý text v popisu a vyjádřeních`.
- [ ] **Step 7: Report uživateli** česky. Uveď, co se změnilo, výsledky testů s čísly a poznámku k nasazení: spustit `db_upgrade_1_4_7_prosty_text_hledani.sql` na obou DB, první start pak doplní čistý text. Dál uveď, že `main` není pushnutý, a co ručně vyzkoušet (fráze přes tučné slovo v popisu i ve vyjádření, zvýraznění na detailu, Edge na i15).
