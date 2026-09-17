# Přestavba vyhledávání — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Pozn. pro tento projekt:** uživatel preferuje **inline exekuci** v hlavní session (superpowers:executing-plans), subagenti se neosvědčili.

**Goal:** Nahradit dvě paralelní vyhledávání jedním záznamocentrickým hledáním nad ostrými tabulkami, s autorizací vynucenou v dotazu.

**Architecture:** Jedna služba sestaví EF Core dotaz nad `projektove_zaznamy` s `EXISTS` podmínkami na `vyjadreni` a `zaznam_externi_odkazy`. Autorizace je `WHERE projekt_id IN (…)` ve stejném dotazu — post-filtr neexistuje. Indexová tabulka, reindex služba a fulltext se ruší bez náhrady.

**Tech Stack:** .NET 8, EF Core 8 (SQL Server), ASP.NET Core MVC + Razor, vanilla JS ESM, gov-design-system 4.2.9 Web Components, xUnit + FluentAssertions, Testcontainers.MsSql (Azure SQL Edge).

**Spec:** `docs/superpowers/specs/2026-09-17-vyhledavani-prestavba-design.md`

## Global Constraints

- **Minimální délka dotazu: 3 znaky.** Kratší dotaz vrací prázdný výsledek bez dotazu do DB.
- **Maximálně 6 slov** z dotazu se bere v potaz.
- **Collation `Czech_CI_AI`** u každého `LIKE`. Databáze má `Czech_CI_AS` (akcent-citlivou), bez vynucení by „zalohovani" nenašlo „Zálohování".
- **Escapování LIKE hranatými závorkami**, ne `ESCAPE` klauzulí — projekt to tak dělá v `DbSuggestService`: `raw.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]")`.
- **7 výsledků v dropdownu.** Řazení **vzestupně**, texty i čísla. Žádné skórování relevance.
- **Autorizace jedním pravidlem:** uživatel vidí záznam právě tehdy, když vidí projekt v plném rozsahu `CanAccessProject`. `null` = bez omezení (superadmin / globální read klíč), **prázdná množina = žádné výsledky** (ne „žádný filtr").
- **Nezasahovat do `DesignSystem-FIS-v1.0.0/assets/gov/**` ani `assets/ds-fis/*`** — pravidlo 1 Design systému, bez výjimky.
- **Nové `db_upgrade_*.sql` zaregistrovat** do `docs/technical/06-database-bootstrap-migrations.md` (sekce 5.1, číslovaný seznam — parsuje ho `RepositoryPaths.GetBootstrapScripts` pro Testcontainers) a do `db_check_applied_upgrades.sql`.
- **Migrace musí projít na Azure SQL Edge** — testovací kontejner nepodporuje fulltext ani vše, co MSSQL 2022. Nepodporovanou věc přeskočit s `RAISERROR(...,10,1)`, nikdy `16`.

**Ověřená API (nevymyšlená, zkontrolováno kompilací proti EF Core 8):**
- `EF.Functions.Like(EF.Functions.Collate(sloupec, "Czech_CI_AI"), vzor)` — překládá se.
- `CompareInfo.IndexOf(ReadOnlySpan<char>, ReadOnlySpan<char>, CompareOptions, out int matchLength)` — pro `cs-CZ` s `IgnoreCase | IgnoreNonSpace` vrací `"zaloha"` → `"Záloha"` index 0, matchLength 6; bez shody index −1, matchLength 0.

**Ověřená fakta o schématu:**
- `projektove_zaznamy.popis` a `vyjadreni.text_vyjadreni` jsou typu **`text`** (legacy LOB). `COLLATE` na nich funguje přímo, `CAST` není potřeba.
- `projektove_zaznamy.subsystem_id` → **`dbo.subsystemy(id)`**, NE `projekt_subsystemy`. Zkratka je `SubsystemEntity.Kod`.
- `SUPERADMIN`, `APP_ADMIN` i `READ_ALL` drží globálně `projects.read.all`, což je project-read klíč → dostanou `null` filtr.
- `search.reindex` drží **jen** SUPERADMIN a APP_ADMIN. `search.index` drží **všech 12 rolí** a **zůstává** — kategorie `SEARCH` se proto NESMÍ mazat.

---

### Task 1: Migrace 1_4_4 — úklid po zrušené indexové vrstvě

Nahrazuje dosavadní `db_upgrade_1_4_4_search_index.sql`, který zakládal `dbo.SearchIndex`. Ten nebyl nikdy nasazen, takže se číslo recykluje.

**Files:**
- Delete: `db_upgrade_1_4_4_search_index.sql`
- Create: `db_upgrade_1_4_4_search_cleanup.sql`
- Modify: `docs/technical/06-database-bootstrap-migrations.md` (řádek 33 seznamu v sekci 5.1)
- Modify: `db_check_applied_upgrades.sql`
- Test: `PmTracker.Tests.Unit/Search/SearchMigrationTests.cs`

**Interfaces:**
- Produces: tabulka `dbo.search_reindex_checkpoint` a `dbo.SearchIndex` po migraci neexistují; klíč `search.reindex` není v `authz.permissions`.

- [ ] **Step 1: Napiš failing test**

Vytvoř `PmTracker.Tests.Unit/Search/SearchMigrationTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Search;

/// <summary>
/// Migrace 1_4_4 uklízí po zrušené indexové vrstvě vyhledávání (spec 2026-09-17 §5).
/// Původní 1_4_4 tabulku SearchIndex naopak zakládala — nebyla nikdy nasazena,
/// proto se číslo recykluje místo zavedení 1_4_5.
/// </summary>
public sealed class SearchMigrationTests
{
    private const string MigrationFile = "db_upgrade_1_4_4_search_cleanup.sql";

    private static string Sql() => File.ReadAllText(ResolvePath(MigrationFile));

    [Fact]
    public void StaraMigrace_ZakladajiciSearchIndex_JizNeexistuje()
    {
        File.Exists(ResolvePath("db_upgrade_1_4_4_search_index.sql")).Should().BeFalse(
            "indexová tabulka se ruší, zakládací skript nesmí zůstat v řadě migrací");
    }

    [Fact]
    public void Migrace_RusiObeTabulkyIndexoveVrstvy()
    {
        var sql = Sql();

        sql.Should().Contain("DROP TABLE dbo.search_reindex_checkpoint");
        sql.Should().Contain("DROP TABLE dbo.SearchIndex");
    }

    [Fact]
    public void Migrace_RusiKlicSearchReindex_AleNechavaSearchIndex()
    {
        var sql = Sql();

        sql.Should().Contain("search.reindex", "klíč hlídal endpoint, který zaniká");
        sql.Should().NotContain("'search.index'",
            "search.index drží všech 12 rolí a vyhledávání zůstává — klíč se NESMÍ mazat");
        sql.Should().NotContain("permission_categories",
            "kategorie SEARCH neosiřela (drží ji search.index), na rozdíl od vzoru 1_4_1");
    }

    [Fact]
    public void Migrace_JeIdempotentni()
    {
        var sql = Sql();

        sql.Should().Contain("OBJECT_ID(N'dbo.search_reindex_checkpoint'");
        sql.Should().Contain("OBJECT_ID(N'dbo.SearchIndex'");
    }

    [Fact]
    public void Migrace_JeZaregistrovanaVBootstrapSeznamu()
    {
        // RepositoryPaths.GetBootstrapScripts parsuje tenhle seznam pro Testcontainers.
        // Bez zápisu spadnou Integration i Api testy na chybějící/přebývající schéma.
        var doc = File.ReadAllText(ResolvePath("docs/technical/06-database-bootstrap-migrations.md"));

        doc.Should().Contain(MigrationFile);
        doc.Should().NotContain("db_upgrade_1_4_4_search_index.sql");
    }

    [Fact]
    public void Migrace_JeVKontrolnimSkriptu()
    {
        var check = File.ReadAllText(ResolvePath("db_check_applied_upgrades.sql"));

        check.Should().Contain("db_upgrade_1_4_4_search_cleanup");
    }
}
```

- [ ] **Step 2: Spusť test, ověř že padá**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~SearchMigrationTests" -v q --nologo
```
Očekávej: 6 FAIL (soubor migrace neexistuje).

- [ ] **Step 3: Smaž starou migraci a napiš novou**

```bash
rm -f db_upgrade_1_4_4_search_index.sql
```

Vytvoř `db_upgrade_1_4_4_search_cleanup.sql`:

```sql
-- =============================================================================
-- db_upgrade_1_4_4_search_cleanup.sql
--
-- Úklid po zrušené indexové vrstvě vyhledávání (spec 2026-09-17).
--
-- KONTEXT: Vyhledávání stálo na denormalizované tabulce dbo.SearchIndex plněné
--   na pozadí a na fulltextovém indexu. Produkční SQL Server nemá komponentu
--   Full-Text Search (IsFullTextInstalled = 0) a mít ji nebude; index navíc mohl
--   zestárnout, což se projevovalo jako „záznam nejde najít". Nové vyhledávání
--   čte rovnou z ostrých tabulek, takže obě tabulky i checkpoint jsou mrtvé.
--
-- ROZSAH MIGRACE:
--   * DROP dbo.SearchIndex — pokud existuje (na dev strojích ji mohla založit
--     stará runtime cesta; v produkci nevznikla, chybělo právo CREATE TABLE).
--   * DROP dbo.search_reindex_checkpoint — zavedena v db_upgrade_1_1_5.
--   * Smaže klíč `search.reindex` z authz (endpoint POST /Search/Reindex zaniká).
--     Klíč `search.index` ZŮSTÁVÁ — drží ho všech 12 rolí a vyhledávání funguje dál.
--     Kategorie SEARCH se proto NEMAŽE (na rozdíl od vzoru 1_4_1, kde osiřela).
--   * Žádná business data se nemažou.
--
-- SPUŠTĚNÍ: pod účtem s db_owner na cílové databázi.
--   sqlcmd -S <SQL_HOST>\<INSTANCE> -E -d PM_Tracker -b -i db_upgrade_1_4_4_search_cleanup.sql
--
-- Idempotence: skript lze spustit opakovaně.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

PRINT N'[1.4.4] Úklid vyhledávání — start';

-- -----------------------------------------------------------------------------
-- 1. Tabulky indexové vrstvy
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.SearchIndex', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.SearchIndex;
    PRINT N'[1.4.4] dbo.SearchIndex zrušena';
END
ELSE
    PRINT N'[1.4.4] dbo.SearchIndex neexistuje — přeskakuji';

IF OBJECT_ID(N'dbo.search_reindex_checkpoint', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.search_reindex_checkpoint;
    PRINT N'[1.4.4] dbo.search_reindex_checkpoint zrušena';
END
ELSE
    PRINT N'[1.4.4] dbo.search_reindex_checkpoint neexistuje — přeskakuji';

-- -----------------------------------------------------------------------------
-- 2. Authz úklid — klíč search.reindex
-- -----------------------------------------------------------------------------
DECLARE @permId INT = (SELECT id FROM authz.permissions WHERE klic = N'search.reindex');

IF @permId IS NULL
    PRINT N'[1.4.4] klíč search.reindex v authz není — přeskakuji';
ELSE
BEGIN
    -- Audit pre-state: které role klíč drží (očekávané: SUPERADMIN, APP_ADMIN).
    SELECT DrziKlic = r.kod
    FROM authz.role_permissions rp
    JOIN authz.roles r ON r.id = rp.role_id
    WHERE rp.permission_id = @permId;

    DELETE FROM authz.role_permission_projects
    WHERE role_permission_id IN (SELECT id FROM authz.role_permissions WHERE permission_id = @permId);

    DELETE FROM authz.role_permissions WHERE permission_id = @permId;
    DELETE FROM authz.permissions     WHERE id = @permId;

    PRINT N'[1.4.4] klíč search.reindex smazán';
END

-- Kategorie SEARCH se NEMAŽE — drží ji search.index, který zůstává.

-- -----------------------------------------------------------------------------
-- 3. Kontrola výsledku
-- -----------------------------------------------------------------------------
SELECT
    SearchIndexZrusena     = CASE WHEN OBJECT_ID(N'dbo.SearchIndex', N'U') IS NULL THEN N'ANO' ELSE N'NE' END,
    CheckpointZrusen       = CASE WHEN OBJECT_ID(N'dbo.search_reindex_checkpoint', N'U') IS NULL THEN N'ANO' ELSE N'NE' END,
    SearchReindexZrusen    = CASE WHEN NOT EXISTS (SELECT 1 FROM authz.permissions WHERE klic = N'search.reindex') THEN N'ANO' ELSE N'NE' END,
    SearchIndexKlicZustava = CASE WHEN EXISTS (SELECT 1 FROM authz.permissions WHERE klic = N'search.index') THEN N'ANO' ELSE N'CHYBA' END;

COMMIT TRANSACTION;

PRINT N'[1.4.4] hotovo';
```

- [ ] **Step 4: Zaregistruj migraci**

V `docs/technical/06-database-bootstrap-migrations.md`, sekce 5.1, nahraď řádek 33:

```
33. `db_upgrade_1_4_4_search_cleanup.sql`
```

V `db_check_applied_upgrades.sql` nahraď dosavadní blok 1_4_4 (sonda i `INSERT @r`):

```sql
DECLARE @searchCleanup INT = CASE
    WHEN OBJECT_ID(N'dbo.SearchIndex', N'U') IS NULL
     AND OBJECT_ID(N'dbo.search_reindex_checkpoint', N'U') IS NULL
     AND NOT EXISTS (SELECT 1 FROM authz.permissions WHERE klic = N'search.reindex')
    THEN 1 ELSE 0 END;
    -- 1_4_4: úklid po zrušené indexové vrstvě vyhledávání
```

```sql
INSERT @r VALUES (330, N'db_upgrade_1_4_4_search_cleanup',
    CASE WHEN @searchCleanup = 1 THEN N'APLIKOVÁN' ELSE N'CHYBÍ' END,
    N'zrušené tabulky SearchIndex + search_reindex_checkpoint a klíč search.reindex');
```

- [ ] **Step 5: Spusť test, ověř že prochází**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~SearchMigrationTests" -v q --nologo
```
Očekávej: 6 PASS.

- [ ] **Step 6: Ověř migraci naostro proti reálné DB, dvakrát**

Skript musí projít i na Azure SQL Edge (Testcontainers) a být idempotentní. Pusť ho dvakrát na čisté databázi a zkontroluj, že druhý běh nic nerozbije a kontrolní `SELECT` vrátí samá `ANO`.

- [ ] **Step 7: Commit**

```bash
git add db_upgrade_1_4_4_search_cleanup.sql db_check_applied_upgrades.sql \
        docs/technical/06-database-bootstrap-migrations.md \
        PmTracker.Tests.Unit/Search/SearchMigrationTests.cs
git rm --cached db_upgrade_1_4_4_search_index.sql 2>/dev/null || true
git commit -m "$(cat <<'EOF'
feat(search): migrace 1_4_4 uklízí indexovou vrstvu vyhledávání

Nahrazuje nenasazenou 1_4_4, která SearchIndex naopak zakládala.
Ruší dbo.SearchIndex, dbo.search_reindex_checkpoint a klíč search.reindex.
search.index zůstává (drží ho všech 12 rolí), kategorie SEARCH se nemaže.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Čistá logika dotazu — slova, escapování, výřez

Bez databáze, bez EF. Všechno testovatelné unit testy.

**Files:**
- Create: `PmTracker.Web/Services/Search/SearchQueryText.cs`
- Test: `PmTracker.Tests.Unit/Search/SearchQueryTextTests.cs`

**Interfaces:**
- Produces:
  - `SearchQueryText.MinQueryLength` = 3, `MaxTerms` = 6, `AccentInsensitiveCollation` = `"Czech_CI_AI"`
  - `IReadOnlyList<string> SearchQueryText.SplitTerms(string? query)`
  - `string SearchQueryText.EscapeLikePattern(string raw)`
  - `string SearchQueryText.ToContainsPattern(string term)`
  - `SearchSnippet? SearchQueryText.BuildSnippet(string? haystack, IReadOnlyList<string> terms, int wordsAround = 2)`
  - `sealed record SearchSnippet(string Before, string Match, string After)`

- [ ] **Step 1: Napiš failing test**

Vytvoř `PmTracker.Tests.Unit/Search/SearchQueryTextTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Search;

namespace PmTracker.Tests.Unit.Search;

public sealed class SearchQueryTextTests
{
    [Theory]
    [InlineData("  zálohování   serveru  ", new[] { "zálohování", "serveru" })]
    [InlineData("jedno", new[] { "jedno" })]
    public void SplitTerms_OrezeMezeryAZahodiPrazdna(string input, string[] expected)
    {
        SearchQueryText.SplitTerms(input).Should().Equal(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    public void SplitTerms_PodPrahemVraciPrazdno(string? input)
    {
        // Práh 3 znaky se vyhodnocuje nad celým dotazem, ne nad jednotlivými slovy.
        SearchQueryText.SplitTerms(input).Should().BeEmpty();
    }

    [Fact]
    public void SplitTerms_OmezujePocetSlov()
    {
        SearchQueryText.SplitTerms("a b c d e f g h i")
            .Should().HaveCount(SearchQueryText.MaxTerms);
    }

    [Theory]
    [InlineData("50 %", "50 [%]")]
    [InlineData("a_b", "a[_]b")]
    [InlineData("[abc]", "[[]abc]")]
    [InlineData("běžný dotaz", "běžný dotaz")]
    public void EscapeLikePattern_ZneskodniZastupneZnaky(string input, string expected)
    {
        SearchQueryText.EscapeLikePattern(input).Should().Be(expected);
    }

    [Fact]
    public void EscapeLikePattern_ZavorkuEscapujeJakoPrvni()
    {
        // Kdyby se '[' escapovala až po '%', vznikl by z "[%]" nesmysl.
        SearchQueryText.EscapeLikePattern("[%]").Should().Be("[[][%]]");
    }

    [Fact]
    public void ToContainsPattern_ObaliProcenty()
    {
        SearchQueryText.ToContainsPattern("záloha").Should().Be("%záloha%");
    }

    [Fact]
    public void BuildSnippet_VratiDveSlovaPredAPo()
    {
        var snippet = SearchQueryText.BuildSnippet(
            "První druhé třetí záloha čtvrté páté šesté",
            new[] { "záloha" });

        snippet.Should().NotBeNull();
        snippet!.Before.Should().Be("druhé třetí ");
        snippet.Match.Should().Be("záloha");
        snippet.After.Should().Be(" čtvrté páté");
    }

    [Fact]
    public void BuildSnippet_IgnorujeDiakritikuAleVratiOriginalniText()
    {
        // Uživatel píše bez diakritiky, zvýraznit se musí text tak, jak je v datech.
        var snippet = SearchQueryText.BuildSnippet("Dnes proběhlo Zálohování dat", new[] { "zalohovani" });

        snippet.Should().NotBeNull();
        snippet!.Match.Should().Be("Zálohování");
    }

    [Fact]
    public void BuildSnippet_BezShodyVraciNull()
    {
        SearchQueryText.BuildSnippet("Text bez shody", new[] { "xyz" }).Should().BeNull();
    }

    [Fact]
    public void BuildSnippet_PrazdnyHaystackVraciNull()
    {
        SearchQueryText.BuildSnippet(null, new[] { "záloha" }).Should().BeNull();
        SearchQueryText.BuildSnippet("", new[] { "záloha" }).Should().BeNull();
    }

    [Fact]
    public void BuildSnippet_NaZacatkuTextuNepadá()
    {
        var snippet = SearchQueryText.BuildSnippet("Záloha proběhla dnes večer", new[] { "záloha" });

        snippet.Should().NotBeNull();
        snippet!.Before.Should().BeEmpty();
        snippet.Match.Should().Be("Záloha");
        snippet.After.Should().Be(" proběhla dnes");
    }

    [Fact]
    public void BuildSnippet_PouzijePrvniSlovoKtereSePotka()
    {
        var snippet = SearchQueryText.BuildSnippet("alfa beta gama", new[] { "nenajde", "gama" });

        snippet.Should().NotBeNull();
        snippet!.Match.Should().Be("gama");
    }
}
```

- [ ] **Step 2: Spusť test, ověř že padá**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~SearchQueryTextTests" -v q --nologo
```
Očekávej: chybu překladu — `SearchQueryText` neexistuje.

- [ ] **Step 3: Napiš implementaci**

Vytvoř `PmTracker.Web/Services/Search/SearchQueryText.cs`:

```csharp
using System.Globalization;

namespace PmTracker.Web.Services.Search;

/// <summary>Výřez textu kolem nalezené shody. Zvýraznění vykresluje UI, ne tahle třída.</summary>
public sealed record SearchSnippet(string Before, string Match, string After);

/// <summary>
/// Čistá textová logika vyhledávání — rozklad dotazu, escapování a výřez okolí shody.
/// Bez databáze a bez EF, aby šla celá otestovat unit testy.
/// </summary>
public static class SearchQueryText
{
    /// <summary>Kratší dotaz se do databáze vůbec neposílá.</summary>
    public const int MinQueryLength = 3;

    /// <summary>Strop počtu slov, aby dotaz nerostl bez hranic.</summary>
    public const int MaxTerms = 6;

    /// <summary>
    /// Databáze má Czech_CI_AS (rozlišuje diakritiku). Bez vynucení téhle collation
    /// by „zalohovani" nenašlo „Zálohování".
    /// </summary>
    public const string AccentInsensitiveCollation = "Czech_CI_AI";

    private static readonly CompareInfo Czech = CultureInfo.GetCultureInfo("cs-CZ").CompareInfo;

    private const CompareOptions AccentInsensitive =
        CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    public static IReadOnlyList<string> SplitTerms(string? query)
    {
        var trimmed = query?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length < MinQueryLength)
        {
            return Array.Empty<string>();
        }

        return trimmed
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Take(MaxTerms)
            .ToList();
    }

    /// <summary>
    /// Escapuje zástupné znaky LIKE hranatými závorkami — stejně jako zbytek projektu.
    /// Pořadí je podstatné: '[' musí jít první, jinak by se rozbily závorky vložené poté.
    /// </summary>
    public static string EscapeLikePattern(string raw) => raw
        .Replace("[", "[[]")
        .Replace("%", "[%]")
        .Replace("_", "[_]");

    public static string ToContainsPattern(string term) => $"%{EscapeLikePattern(term)}%";

    /// <summary>
    /// Najde první slovo, které se v textu vyskytuje, a vrátí <paramref name="wordsAround"/>
    /// slov před ním a za ním. Hledá bez ohledu na diakritiku a velikost písmen, ale
    /// <see cref="SearchSnippet.Match"/> nese text tak, jak je v datech — zvýrazní se
    /// tedy „Zálohování", i když uživatel napsal „zalohovani".
    /// </summary>
    public static SearchSnippet? BuildSnippet(string? haystack, IReadOnlyList<string> terms, int wordsAround = 2)
    {
        if (string.IsNullOrEmpty(haystack) || terms.Count == 0)
        {
            return null;
        }

        foreach (var term in terms)
        {
            if (string.IsNullOrEmpty(term))
            {
                continue;
            }

            var index = Czech.IndexOf(haystack.AsSpan(), term.AsSpan(), AccentInsensitive, out var matchLength);
            if (index < 0 || matchLength <= 0)
            {
                continue;
            }

            var before = TakeLastWords(haystack[..index], wordsAround);
            var after = TakeFirstWords(haystack[(index + matchLength)..], wordsAround);

            return new SearchSnippet(before, haystack.Substring(index, matchLength), after);
        }

        return null;
    }

    private static string TakeLastWords(string text, int count)
    {
        if (string.IsNullOrEmpty(text) || count <= 0)
        {
            return string.Empty;
        }

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return string.Empty;
        }

        var taken = string.Join(' ', words.TakeLast(count));
        return taken + " ";
    }

    private static string TakeFirstWords(string text, int count)
    {
        if (string.IsNullOrEmpty(text) || count <= 0)
        {
            return string.Empty;
        }

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return string.Empty;
        }

        return " " + string.Join(' ', words.Take(count));
    }
}
```

- [ ] **Step 4: Spusť test, ověř že prochází**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~SearchQueryTextTests" -v q --nologo
```
Očekávej: všechny PASS. Pokud `BuildSnippet_VratiDveSlovaPredAPo` selže na mezerách, zkontroluj `TakeLastWords`/`TakeFirstWords` — mezera patří dovnitř `Before`/`After`, ne do `Match`.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Search/SearchQueryText.cs PmTracker.Tests.Unit/Search/SearchQueryTextTests.cs
git commit -m "$(cat <<'EOF'
feat(search): čistá textová logika dotazu — slova, escapování, výřez

Práh 3 znaky, max 6 slov, escapování hranatými závorkami jako zbytek projektu.
Výřez kolem shody hledá bez diakritiky, ale vrací text tak, jak je v datech.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Model výsledku — kategorie jako objekt

Dnes existuje jediná kategorie (*Záznamy*). Model se přesto staví jako kolekce kategorií, aby přidání další bylo doplněním, ne přepisem.

**Files:**
- Create: `PmTracker.Web/Services/Search/SearchResultModels.cs`
- Test: `PmTracker.Tests.Unit/Search/SearchResultModelsTests.cs`

**Interfaces:**
- Consumes: `SearchSnippet` z Tasku 2.
- Produces:
  - `enum SearchMatchKind { Nazev, Popis, Vyjadreni, ExterniOdkaz }`
  - `sealed record SearchResultItem(int ZaznamId, int ProjektId, string Nazev, string? CisloViditelne, string SubsystemKod, SearchMatchKind MatchKind, SearchSnippet? Snippet, int? CisloJednani, string DetailUrl)`
  - `sealed record SearchResultCategory(string Key, string Nazev, IReadOnlyList<SearchResultItem> Items)`
  - `sealed record SearchResult(string Query, IReadOnlyList<SearchResultCategory> Categories)` s `TotalCount` a `SearchResult.Empty(string query)`
  - `SearchCategoryKeys.Zaznamy` = `"zaznamy"`

- [ ] **Step 1: Napiš failing test**

Vytvoř `PmTracker.Tests.Unit/Search/SearchResultModelsTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Search;

namespace PmTracker.Tests.Unit.Search;

public sealed class SearchResultModelsTests
{
    private static SearchResultItem Item(int id) => new(
        ZaznamId: id,
        ProjektId: 10,
        Nazev: $"Záznam {id}",
        CisloViditelne: $"RU {id}",
        SubsystemKod: "R_EIS",
        MatchKind: SearchMatchKind.Nazev,
        Snippet: null,
        CisloJednani: null,
        DetailUrl: $"/Projekty/Detail/10?recordId={id}");

    [Fact]
    public void Empty_NemaZadneKategorieAniVysledky()
    {
        var result = SearchResult.Empty("zal");

        result.Query.Should().Be("zal");
        result.Categories.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public void TotalCount_SectePolozkyPresVsechnyKategorie()
    {
        var result = new SearchResult("zal",
        [
            new SearchResultCategory(SearchCategoryKeys.Zaznamy, "Záznamy", [Item(1), Item(2)]),
            new SearchResultCategory("budouci", "Budoucí kategorie", [Item(3)])
        ]);

        result.TotalCount.Should().Be(3,
            "počet se musí odvozovat z kategorií, aby přidání další nevyžadovalo zásah");
    }

    [Fact]
    public void KlicKategorieZaznamy_JeStabilni()
    {
        // Klíč jde do JSON pro dropdown i do markupu stránky — nesmí se měnit náhodou.
        SearchCategoryKeys.Zaznamy.Should().Be("zaznamy");
    }
}
```

- [ ] **Step 2: Spusť test, ověř že padá**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~SearchResultModelsTests" -v q --nologo
```
Očekávej: chybu překladu.

- [ ] **Step 3: Napiš implementaci**

Vytvoř `PmTracker.Web/Services/Search/SearchResultModels.cs`:

```csharp
namespace PmTracker.Web.Services.Search;

/// <summary>Proč se záznam našel. Určuje, co se vykreslí na druhém řádku dropdownu.</summary>
public enum SearchMatchKind
{
    /// <summary>Shoda v názvu záznamu (nebo v jeho čísle).</summary>
    Nazev,

    /// <summary>Shoda v popisu nebo cíli záznamu.</summary>
    Popis,

    /// <summary>Shoda v textu navázaného vyjádření.</summary>
    Vyjadreni,

    /// <summary>Shoda v čísle navázaného externího odkazu.</summary>
    ExterniOdkaz
}

/// <summary>Stabilní klíče kategorií. Jdou do JSON i do markupu — neměnit.</summary>
public static class SearchCategoryKeys
{
    public const string Zaznamy = "zaznamy";
}

/// <summary>Jeden výsledek. Jednotkou je vždy záznam, i když shoda padla ve vyjádření.</summary>
public sealed record SearchResultItem(
    int ZaznamId,
    int ProjektId,
    string Nazev,
    string? CisloViditelne,
    string SubsystemKod,
    SearchMatchKind MatchKind,
    SearchSnippet? Snippet,
    int? CisloJednani,
    string DetailUrl);

/// <summary>
/// Kategorie výsledků. Dnes je jediná (Záznamy), model ji přesto drží jako kolekci,
/// aby přidání další kategorie bylo doplněním implementace, ne přepisem zobrazení.
/// </summary>
public sealed record SearchResultCategory(
    string Key,
    string Nazev,
    IReadOnlyList<SearchResultItem> Items);

public sealed record SearchResult(
    string Query,
    IReadOnlyList<SearchResultCategory> Categories)
{
    public int TotalCount => Categories.Sum(c => c.Items.Count);

    public static SearchResult Empty(string query) =>
        new(query, Array.Empty<SearchResultCategory>());
}
```

- [ ] **Step 4: Spusť test, ověř že prochází**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~SearchResultModelsTests" -v q --nologo
```
Očekávej: 3 PASS.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Search/SearchResultModels.cs PmTracker.Tests.Unit/Search/SearchResultModelsTests.cs
git commit -m "$(cat <<'EOF'
feat(search): model výsledku s kategoriemi jako objektem

Jednotka výsledku je vždy záznam. Kategorie je kolekce, i když je zatím
jediná — přidání další nemá vyžadovat přepis zobrazení.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Viditelnost projektů — jediné autorizační pravidlo

Tady se opravuje vada V1 ze specifikace. Dnešní kód pre-filtruje jen podle `VisibleProjectIds`, takže držitel role bez obsazení v projektu nevidí nic.

**Files:**
- Create: `PmTracker.Web/Services/Search/ProjectVisibilityResolver.cs`
- Test: `PmTracker.Tests.Unit/Search/ProjectVisibilityResolverTests.cs`

**Interfaces:**
- Consumes: `CurrentUserContextViewModel`, `AuthorizationSnapshot`, `PermissionKeys.GrantsProjectRead` (vše z `PmTracker.Web.Models.ViewModels`).
- Produces:
  - `interface IProjectVisibilityResolver { IReadOnlyList<int>? Resolve(CurrentUserContextViewModel user); }`
  - `sealed class ProjectVisibilityResolver : IProjectVisibilityResolver`
  - Kontrakt návratové hodnoty: **`null` = bez omezení**, **prázdný seznam = žádné výsledky**, jinak seznam id projektů.

- [ ] **Step 1: Napiš failing test**

Vytvoř `PmTracker.Tests.Unit/Search/ProjectVisibilityResolverTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Search;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Search;

/// <summary>
/// Spec 2026-09-17 §2.3. Vada V1: dnešní vyhledávání pre-filtruje jen podle
/// VisibleProjectIds, takže kdo má přístup přes ROLI a ne přes obsazení projektu,
/// nevidí nic. Uživatel navíc výslovně požadoval, aby nové chování „prázdná množina
/// = žádné výsledky" nerozbilo app-admina a superadmina.
/// </summary>
public sealed class ProjectVisibilityResolverTests
{
    private static CurrentUserContextViewModel User(
        bool isSuperAdmin = false,
        int[]? visibleProjectIds = null,
        string[]? globalPermissions = null,
        Dictionary<int, IReadOnlySet<string>>? perProject = null) => new()
    {
        OsobaId = 1,
        Jmeno = "Jan",
        Prijmeni = "Novák",
        DisplayName = "Jan Novák",
        Email = "jan.novak@example.cz",
        OrganizacniCelek = "MO",
        IsSuperAdmin = isSuperAdmin,
        RoleKody = Array.Empty<string>(),
        VisibleProjectIds = visibleProjectIds ?? Array.Empty<int>(),
        DeletedProjectIds = Array.Empty<int>(),
        Authorization = new AuthorizationSnapshot(
            IsSuperAdmin: isSuperAdmin,
            GlobalPermissions: new HashSet<string>(globalPermissions ?? Array.Empty<string>()),
            PerProjectPermissions: perProject ?? new Dictionary<int, IReadOnlySet<string>>(),
            PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>())
    };

    private readonly ProjectVisibilityResolver _resolver = new();

    [Fact]
    public void Superadmin_NemaOmezeni()
    {
        _resolver.Resolve(User(isSuperAdmin: true)).Should().BeNull(
            "null znamená bez omezení — superadmin nesmí spadnout do prázdné množiny");
    }

    [Fact]
    public void AppAdmin_SGlobalnimReadAll_NemaOmezeni()
    {
        // APP_ADMIN i READ_ALL drží globálně projects.read.all.
        var user = User(globalPermissions: [PermissionKeys.ProjectsReadAll]);

        _resolver.Resolve(user).Should().BeNull();
    }

    [Fact]
    public void ClenProjektu_VidiSveProjekty()
    {
        var user = User(visibleProjectIds: [10, 20]);

        _resolver.Resolve(user).Should().BeEquivalentTo([10, 20]);
    }

    [Fact]
    public void DrzitelProjektoveRole_BezObsazeni_VidiSvujProjekt()
    {
        // Tohle je vada V1 — dnes by takový uživatel nedostal nic.
        var user = User(perProject: new Dictionary<int, IReadOnlySet<string>>
        {
            [42] = new HashSet<string> { PermissionKeys.RecordsEdit }
        });

        _resolver.Resolve(user).Should().BeEquivalentTo([42]);
    }

    [Fact]
    public void Obsazeni_ASnapshot_SeSlucujiBezDuplicit()
    {
        var user = User(
            visibleProjectIds: [10, 20],
            perProject: new Dictionary<int, IReadOnlySet<string>>
            {
                [20] = new HashSet<string> { PermissionKeys.RecordsEdit },
                [30] = new HashSet<string> { PermissionKeys.RecordsEdit }
            });

        _resolver.Resolve(user).Should().BeEquivalentTo([10, 20, 30]);
    }

    [Fact]
    public void PerProjektovyKlicBezPravaCist_ProjektNepridava()
    {
        var user = User(perProject: new Dictionary<int, IReadOnlySet<string>>
        {
            [42] = new HashSet<string> { "search.index" }
        });

        _resolver.Resolve(user).Should().BeEmpty(
            "search.index není project-read klíč, sám o sobě projekt nezpřístupní");
    }

    [Fact]
    public void UzivatelBezProjektu_VraciPrazdnoNeNull()
    {
        // Rozdíl, na kterém záleží: prázdná množina = žádné výsledky.
        // null by znamenalo „bez omezení", tedy únik všech záznamů.
        var result = _resolver.Resolve(User());

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ChybejiciSnapshot_NevraciNull()
    {
        var user = new CurrentUserContextViewModel
        {
            OsobaId = 1,
            Jmeno = "Jan",
            Prijmeni = "Novák",
            DisplayName = "Jan Novák",
            Email = "jan.novak@example.cz",
            OrganizacniCelek = "MO",
            IsSuperAdmin = false,
            RoleKody = Array.Empty<string>(),
            VisibleProjectIds = Array.Empty<int>(),
            DeletedProjectIds = Array.Empty<int>(),
            Authorization = null
        };

        _resolver.Resolve(user).Should().BeEmpty("bez snapshotu se nesmí povolit všechno");
    }
}
```

- [ ] **Step 2: Spusť test, ověř že padá**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ProjectVisibilityResolverTests" -v q --nologo
```
Očekávej: chybu překladu — `ProjectVisibilityResolver` neexistuje.

- [ ] **Step 3: Napiš implementaci**

Vytvoř `PmTracker.Web/Services/Search/ProjectVisibilityResolver.cs`:

```csharp
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Search;

/// <summary>
/// Spočítá množinu projektů, jejichž záznamy smí uživatel ve vyhledávání vidět.
/// </summary>
public interface IProjectVisibilityResolver
{
    /// <summary>
    /// <c>null</c> = bez omezení (superadmin nebo globální právo číst projekty).
    /// Prázdný seznam = uživatel nevidí žádný projekt, tedy žádné výsledky.
    /// </summary>
    IReadOnlyList<int>? Resolve(CurrentUserContextViewModel user);
}

/// <summary>
/// Jediné autorizační pravidlo vyhledávání: záznam je vidět tehdy, když je vidět
/// jeho projekt — ve stejném rozsahu jako <see cref="CurrentUserContextViewModel.CanAccessProject"/>.
///
/// Dřívější vyhledávání používalo jen <c>VisibleProjectIds</c> (plněno z ObsazeniProjektu),
/// takže držitel projektové role bez obsazení nedostal nic. Stejná třída vady jako
/// commit 55625b2 u projects.read.all.
/// </summary>
public sealed class ProjectVisibilityResolver : IProjectVisibilityResolver
{
    public IReadOnlyList<int>? Resolve(CurrentUserContextViewModel user)
    {
        if (user.IsSuperAdmin)
        {
            return null;
        }

        var authz = user.Authorization;

        // Globální read klíč (projects.read.all u SUPERADMIN, APP_ADMIN, READ_ALL)
        // otevírá všechny projekty — omezovat výčtem by bylo zbytečné i pomalé.
        if (authz is not null && authz.GlobalPermissions.Any(PermissionKeys.GrantsProjectRead))
        {
            return null;
        }

        var fromSnapshot = authz is null
            ? Enumerable.Empty<int>()
            : authz.PerProjectPermissions
                .Where(kvp => kvp.Value.Any(PermissionKeys.GrantsProjectRead))
                .Select(kvp => kvp.Key);

        return user.VisibleProjectIds
            .Concat(fromSnapshot)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
    }
}
```

- [ ] **Step 4: Spusť test, ověř že prochází**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ProjectVisibilityResolverTests" -v q --nologo
```
Očekávej: 8 PASS.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Search/ProjectVisibilityResolver.cs PmTracker.Tests.Unit/Search/ProjectVisibilityResolverTests.cs
git commit -m "$(cat <<'EOF'
feat(search): jediné autorizační pravidlo pro viditelnost projektů

null = bez omezení (superadmin, globální read klíč), prázdná množina =
žádné výsledky. Opravuje vadu V1: držitel projektové role bez obsazení
v projektu dosud ve vyhledávání nedostal nic.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: Vyhledávací služba — EF dotaz

Jádro. Testuje se integračně proti reálné databázi, protože `COLLATE` ani `EXISTS` nejde ověřit in-memory.

**Files:**
- Create: `PmTracker.Web/Services/Search/IRecordSearchService.cs`
- Create: `PmTracker.Web/Services/Search/RecordSearchService.cs`
- Test: `PmTracker.Tests.Integration/DataStore/RecordSearchServiceTests.cs`

**Interfaces:**
- Consumes: `SearchQueryText`, `SearchSnippet` (Task 2); `SearchResult`, `SearchResultItem`, `SearchResultCategory`, `SearchMatchKind`, `SearchCategoryKeys` (Task 3); `IProjectVisibilityResolver` (Task 4); `PmTrackerDbContext`.
- Produces: `interface IRecordSearchService { Task<SearchResult> SearchAsync(string? query, CurrentUserContextViewModel user, int limit, CancellationToken ct); }`
- Konstanta `RecordSearchService.DropdownLimit` = 7.

- [ ] **Step 1: Napiš failing test**

Vytvoř `PmTracker.Tests.Integration/DataStore/RecordSearchServiceTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Search;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// Vyhledávání naostro proti reálné databázi. Collation ani EXISTS nejde ověřit
/// in-memory, takže tohle je jediné místo, kde se dotaz opravdu testuje.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class RecordSearchServiceTests
{
    private readonly SqlIntegrationFixture _fixture;

    public RecordSearchServiceTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    private static CurrentUserContextViewModel User(
        bool isSuperAdmin = false,
        int[]? visibleProjectIds = null,
        string[]? globalPermissions = null) => new()
    {
        OsobaId = 1,
        Jmeno = "Jan",
        Prijmeni = "Novák",
        DisplayName = "Jan Novák",
        Email = "jan.novak@example.cz",
        OrganizacniCelek = "MO",
        IsSuperAdmin = isSuperAdmin,
        RoleKody = Array.Empty<string>(),
        VisibleProjectIds = visibleProjectIds ?? Array.Empty<int>(),
        DeletedProjectIds = Array.Empty<int>(),
        Authorization = new AuthorizationSnapshot(
            IsSuperAdmin: isSuperAdmin,
            GlobalPermissions: new HashSet<string>(globalPermissions ?? Array.Empty<string>()),
            PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>>(),
            PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>())
    };

    private static RecordSearchService CreateService(string connectionString)
    {
        var db = IntegrationTestHelper.CreateDbContext(connectionString);
        return new RecordSearchService(db, new ProjectVisibilityResolver(),
            NullLogger<RecordSearchService>.Instance);
    }

    /// <summary>
    /// Bez tohohle by test diakritiky mohl projít i kdyby COLLATE v dotazu chybělo.
    /// Produkce má Czech_CI_AS, kontejner SQL_Latin1_General_CP1_CI_AS — obojí _AS.
    /// </summary>
    private static async Task AssertAccentSensitiveCollationAsync(string connectionString)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT CONVERT(nvarchar(128), DATABASEPROPERTYEX(DB_NAME(), 'Collation'));";
        ((string)(await cmd.ExecuteScalarAsync())!).Should().EndWith("_AS");
    }

    [Fact]
    public async Task Hledani_IgnorujeDiakritiku()
    {
        var db = await _fixture.CreateDatabaseAsync("search_diakritika");
        await AssertAccentSensitiveCollationAsync(db.ConnectionString);
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        await seed.AddRecordAsync(projektId: seed.ProjektId, nazev: "Zálohování serveru");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("zalohovani", User(isSuperAdmin: true), 7, default);

        result.TotalCount.Should().Be(1);
        result.Categories.Should().ContainSingle()
            .Which.Items[0].Nazev.Should().Be("Zálohování serveru");
    }

    [Fact]
    public async Task Hledani_PodPrahemTriZnaku_NehledaVubec()
    {
        var db = await _fixture.CreateDatabaseAsync("search_prah");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        await seed.AddRecordAsync(seed.ProjektId, "Záloha");

        var service = CreateService(db.ConnectionString);

        (await service.SearchAsync("za", User(isSuperAdmin: true), 7, default))
            .TotalCount.Should().Be(0);
        (await service.SearchAsync("zal", User(isSuperAdmin: true), 7, default))
            .TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Hledani_VyzadujeVsechnaSlova()
    {
        var db = await _fixture.CreateDatabaseAsync("search_vsechna_slova");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        await seed.AddRecordAsync(seed.ProjektId, "Zálohování serveru");
        await seed.AddRecordAsync(seed.ProjektId, "Zálohování databáze");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("zálohování serveru", User(isSuperAdmin: true), 7, default);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Hledani_ZastupneZnakyJsouDoslovne()
    {
        var db = await _fixture.CreateDatabaseAsync("search_zastupne");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        await seed.AddRecordAsync(seed.ProjektId, "Čerpání 50 % rozpočtu");
        await seed.AddRecordAsync(seed.ProjektId, "Úplně nesouvisející");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("50 %", User(isSuperAdmin: true), 7, default);

        result.TotalCount.Should().Be(1, "neescapované % by vrátilo obě položky");
    }

    [Fact]
    public async Task Hledani_NajdeZaznamPodleTextuVyjadreni_AVratiCisloJednani()
    {
        var db = await _fixture.CreateDatabaseAsync("search_vyjadreni");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        var zaznamId = await seed.AddRecordAsync(seed.ProjektId, "Nesouvisející název");
        var jednaniId = await seed.AddMeetingAsync(seed.ProjektId, cisloJednani: 8201);
        await seed.AddStatementAsync(zaznamId, jednaniId, "Dnes proběhla záloha dat a byla ověřena");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("záloha", User(isSuperAdmin: true), 7, default);

        var item = result.Categories.Single().Items.Single();
        item.ZaznamId.Should().Be(zaznamId, "jednotkou výsledku je záznam, ne vyjádření");
        item.MatchKind.Should().Be(SearchMatchKind.Vyjadreni);
        item.CisloJednani.Should().Be(8201);
        item.Snippet!.Match.Should().Be("záloha");
        item.Snippet.Before.Should().Be("Dnes proběhla ");
        item.Snippet.After.Should().Be(" dat a");
    }

    [Fact]
    public async Task Hledani_NajdeZaznamPodleCislaExterniVazby()
    {
        var db = await _fixture.CreateDatabaseAsync("search_externi");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        var zaznamId = await seed.AddRecordAsync(seed.ProjektId, "Nesouvisející název");
        await seed.AddExternalLinkAsync(zaznamId, cislo: "123456");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("123456", User(isSuperAdmin: true), 7, default);

        var item = result.Categories.Single().Items.Single();
        item.ZaznamId.Should().Be(zaznamId);
        item.MatchKind.Should().Be(SearchMatchKind.ExterniOdkaz);
        item.Snippet!.Match.Should().Be("123456");
    }

    [Fact]
    public async Task Hledani_NajdeZaznamPodleCislaZaznamu()
    {
        var db = await _fixture.CreateDatabaseAsync("search_cislo_zaznamu");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        await seed.AddRecordAsync(seed.ProjektId, "Nesouvisející název", cisloViditelne: "RU 123");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("RU 123", User(isSuperAdmin: true), 7, default);

        result.Categories.Single().Items.Single().MatchKind.Should().Be(SearchMatchKind.Nazev);
    }

    [Fact]
    public async Task Hledani_VraciZkratkuSubsystemu()
    {
        var db = await _fixture.CreateDatabaseAsync("search_subsystem");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        await seed.AddRecordAsync(seed.ProjektId, "Zálohování serveru");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("zálohování", User(isSuperAdmin: true), 7, default);

        result.Categories.Single().Items.Single().SubsystemKod.Should().Be(seed.SubsystemKod);
    }

    // ---- Autorizace: spec §2.3 --------------------------------------------

    [Fact]
    public async Task Autorizace_ClenProjektu_VidiJenSvujProjekt()
    {
        var db = await _fixture.CreateDatabaseAsync("search_authz_clen");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        var druhyProjekt = await seed.AddProjectAsync("DRUHY");
        await seed.AddRecordAsync(seed.ProjektId, "Záloha v mém projektu");
        await seed.AddRecordAsync(druhyProjekt, "Záloha v cizím projektu");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("záloha", User(visibleProjectIds: [seed.ProjektId]), 7, default);

        result.Categories.Single().Items.Should().ContainSingle()
            .Which.Nazev.Should().Be("Záloha v mém projektu");
    }

    [Fact]
    public async Task Autorizace_UzivatelBezProjektu_NevidiNic()
    {
        var db = await _fixture.CreateDatabaseAsync("search_authz_bez_projektu");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        await seed.AddRecordAsync(seed.ProjektId, "Záloha serveru");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("záloha", User(), 7, default);

        result.TotalCount.Should().Be(0,
            "prázdná množina projektů musí znamenat žádné výsledky, ne žádný filtr");
    }

    [Fact]
    public async Task Autorizace_Superadmin_VidiVse()
    {
        var db = await _fixture.CreateDatabaseAsync("search_authz_superadmin");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        var druhyProjekt = await seed.AddProjectAsync("DRUHY");
        await seed.AddRecordAsync(seed.ProjektId, "Záloha jedna");
        await seed.AddRecordAsync(druhyProjekt, "Záloha dvě");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("záloha", User(isSuperAdmin: true), 7, default);

        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Autorizace_AppAdminSGlobalnimReadAll_VidiVse()
    {
        // Uživatel výslovně žádal, aby nové chování nerozbilo app-admina.
        var db = await _fixture.CreateDatabaseAsync("search_authz_appadmin");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        var druhyProjekt = await seed.AddProjectAsync("DRUHY");
        await seed.AddRecordAsync(seed.ProjektId, "Záloha jedna");
        await seed.AddRecordAsync(druhyProjekt, "Záloha dvě");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("záloha",
            User(globalPermissions: [PermissionKeys.ProjectsReadAll]), 7, default);

        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Autorizace_VyjadreniZCizihoProjektu_ZaznamNeodhali()
    {
        // Shoda padne ve vyjádření záznamu z projektu, který uživatel nevidí.
        var db = await _fixture.CreateDatabaseAsync("search_authz_vyjadreni");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        var cizi = await seed.AddProjectAsync("CIZI");
        var cizinZaznam = await seed.AddRecordAsync(cizi, "Cizí záznam");
        var jednani = await seed.AddMeetingAsync(cizi, 9001);
        await seed.AddStatementAsync(cizinZaznam, jednani, "Tady je tajná záloha");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("záloha", User(visibleProjectIds: [seed.ProjektId]), 7, default);

        result.TotalCount.Should().Be(0);
    }

    // ---- Limit a řazení ----------------------------------------------------

    [Fact]
    public async Task Hledani_RespektujeLimit()
    {
        var db = await _fixture.CreateDatabaseAsync("search_limit");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        for (var i = 1; i <= 10; i++)
        {
            await seed.AddRecordAsync(seed.ProjektId, $"Záloha {i:D2}");
        }

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("záloha", User(isSuperAdmin: true), 7, default);

        result.TotalCount.Should().Be(7);
    }

    [Fact]
    public async Task Hledani_RadiVzestupnePodleNazvu()
    {
        var db = await _fixture.CreateDatabaseAsync("search_razeni");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        await seed.AddRecordAsync(seed.ProjektId, "Záloha C");
        await seed.AddRecordAsync(seed.ProjektId, "Záloha A");
        await seed.AddRecordAsync(seed.ProjektId, "Záloha B");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("záloha", User(isSuperAdmin: true), 7, default);

        result.Categories.Single().Items.Select(i => i.Nazev)
            .Should().Equal("Záloha A", "Záloha B", "Záloha C");
    }
}
```

- [ ] **Step 2: Vytvoř seed helper**

Testy výše potřebují `SearchSeed`. Vytvoř `PmTracker.Tests.Integration/TestInfrastructure/SearchSeed.cs`:

```csharp
using Microsoft.Data.SqlClient;

namespace PmTracker.Tests.Integration.TestInfrastructure;

/// <summary>
/// Minimální seed pro testy vyhledávání. Píše se přímo SQL, protože přes aplikační
/// službu by to táhlo validace, které s vyhledáváním nesouvisí.
/// </summary>
public sealed class SearchSeed
{
    private readonly string _connectionString;

    public int ProjektId { get; private init; }
    public int SubsystemId { get; private init; }
    public string SubsystemKod { get; private init; } = "R_EIS";
    public int OsobaId { get; private init; }
    public int KategorieId { get; private init; }

    private SearchSeed(string connectionString) => _connectionString = connectionString;

    private async Task<int> ScalarAsync(string sql, params (string Name, object Value)[] ps)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in ps)
        {
            cmd.Parameters.AddWithValue(name, value);
        }
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public static async Task<SearchSeed> CreateAsync(string connectionString)
    {
        var tmp = new SearchSeed(connectionString);

        var osobaId = await tmp.ScalarAsync("""
            INSERT INTO dbo.osoby (jmeno, prijmeni, email)
            OUTPUT INSERTED.id VALUES (N'Jan', N'Novák', N'jan.novak@example.cz');
            """);

        var subsystemId = await tmp.ScalarAsync("""
            INSERT INTO dbo.subsystemy (kod, nazev)
            OUTPUT INSERTED.id VALUES (N'R_EIS', N'Registr EIS');
            """);

        var kategorieId = await tmp.ScalarAsync("""
            SELECT TOP (1) id FROM dbo.ciselnik_kategorii_zaznamu ORDER BY id;
            """);

        var seed = new SearchSeed(connectionString)
        {
            OsobaId = osobaId,
            SubsystemId = subsystemId,
            KategorieId = kategorieId
        };

        var projektId = await seed.AddProjectAsync("PRVNI");
        return new SearchSeed(connectionString)
        {
            OsobaId = osobaId,
            SubsystemId = subsystemId,
            KategorieId = kategorieId,
            ProjektId = projektId
        };
    }

    public Task<int> AddProjectAsync(string zkratka) => ScalarAsync("""
        INSERT INTO dbo.projekty (zkratka, cely_nazev)
        OUTPUT INSERTED.id VALUES (@zkratka, @nazev);
        """, ("@zkratka", zkratka), ("@nazev", $"Projekt {zkratka}"));

    public Task<int> AddRecordAsync(int projektId, string nazev, string? popis = null,
        string? cisloViditelne = null) => ScalarAsync("""
        INSERT INTO dbo.projektove_zaznamy
            (projekt_id, kategorie_id, cislo_zaznamu, cislo_viditelne, nazev, popis,
             vlastnik_id, datum_zalozeni, datum_ukonceni, subsystem_id)
        OUTPUT INSERTED.id
        VALUES (@projekt, @kategorie, 1, @cisloViditelne, @nazev, @popis,
                @vlastnik, SYSUTCDATETIME(), SYSUTCDATETIME(), @subsystem);
        """,
        ("@projekt", projektId), ("@kategorie", KategorieId),
        ("@cisloViditelne", (object?)cisloViditelne ?? DBNull.Value),
        ("@nazev", nazev), ("@popis", (object?)popis ?? DBNull.Value),
        ("@vlastnik", OsobaId), ("@subsystem", SubsystemId));

    public Task<int> AddMeetingAsync(int projektId, int cisloJednani) => ScalarAsync("""
        INSERT INTO dbo.jednani (projekt_id, cislo_jednani, datum_planovane, stav_jednani_id)
        OUTPUT INSERTED.id
        VALUES (@projekt, @cislo, SYSUTCDATETIME(),
                (SELECT TOP (1) id FROM dbo.ciselnik_stavu_jednani ORDER BY id));
        """, ("@projekt", projektId), ("@cislo", cisloJednani));

    public Task<int> AddStatementAsync(int zaznamId, int jednaniId, string text) => ScalarAsync("""
        INSERT INTO dbo.vyjadreni (zaznam_id, jednani_id, autor_osoba_id, text_vyjadreni, datum_vyjadreni)
        OUTPUT INSERTED.id VALUES (@zaznam, @jednani, @autor, @text, SYSUTCDATETIME());
        """, ("@zaznam", zaznamId), ("@jednani", jednaniId), ("@autor", OsobaId), ("@text", text));

    public Task<int> AddExternalLinkAsync(int zaznamId, string cislo) => ScalarAsync("""
        INSERT INTO dbo.zaznam_externi_odkazy (zaznam_id, typ_odkazu_id, cislo)
        OUTPUT INSERTED.id
        VALUES (@zaznam, (SELECT TOP (1) id FROM dbo.ciselnik_typu_externich_odkazu ORDER BY id), @cislo);
        """, ("@zaznam", zaznamId), ("@cislo", cislo));
}
```

**Pozn.:** sloupce v `INSERT` ověř proti `PMTracker_insert_sql` — legacy tabulky mají snake_case a některé sloupce diakritiku. Pokud `INSERT` spadne na „Invalid column name", podívej se do `sys.columns`, nedomýšlej názvy.

- [ ] **Step 3: Spusť test, ověř že padá**

```bash
dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~RecordSearchServiceTests" -v q --nologo
```
Očekávej: chybu překladu — `RecordSearchService` neexistuje.

- [ ] **Step 4: Napiš rozhraní**

Vytvoř `PmTracker.Web/Services/Search/IRecordSearchService.cs`:

```csharp
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Search;

public interface IRecordSearchService
{
    /// <summary>
    /// Najde záznamy, které uživatel smí vidět a v nichž (nebo v jejich vyjádřeních
    /// či externích odkazech) se vyskytují všechna slova dotazu.
    /// Dotaz kratší než <see cref="SearchQueryText.MinQueryLength"/> vrací prázdný výsledek.
    /// </summary>
    Task<SearchResult> SearchAsync(
        string? query,
        CurrentUserContextViewModel user,
        int limit,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 5: Napiš implementaci**

Vytvoř `PmTracker.Web/Services/Search/RecordSearchService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Search;

/// <summary>
/// Vyhledávání nad ostrými tabulkami. Žádná indexová vrstva — výsledky jsou vždy
/// čerstvé a odpadá celá reindex mašinerie (spec 2026-09-17 §2.4).
///
/// Autorizace je součástí dotazu, ne post-filtr: záznam, na který uživatel nemá
/// právo, se z databáze vůbec nevrátí.
/// </summary>
public sealed class RecordSearchService : IRecordSearchService
{
    /// <summary>Kolik výsledků se vejde do dropdownu.</summary>
    public const int DropdownLimit = 7;

    private readonly PmTrackerDbContext _db;
    private readonly IProjectVisibilityResolver _visibility;
    private readonly ILogger<RecordSearchService> _logger;

    public RecordSearchService(
        PmTrackerDbContext db,
        IProjectVisibilityResolver visibility,
        ILogger<RecordSearchService> logger)
    {
        _db = db;
        _visibility = visibility;
        _logger = logger;
    }

    public async Task<SearchResult> SearchAsync(
        string? query,
        CurrentUserContextViewModel user,
        int limit,
        CancellationToken cancellationToken)
    {
        var trimmed = query?.Trim() ?? string.Empty;
        var terms = SearchQueryText.SplitTerms(trimmed);
        if (terms.Count == 0)
        {
            return SearchResult.Empty(trimmed);
        }

        var visibleProjectIds = _visibility.Resolve(user);
        if (visibleProjectIds is { Count: 0 })
        {
            // Prázdná množina znamená žádné výsledky. Kdyby se tady jen „neaplikoval
            // filtr", uživatel bez projektů by uviděl všechno.
            return SearchResult.Empty(trimmed);
        }

        var q = _db.ProjektoveZaznamy.AsNoTracking();

        if (visibleProjectIds is not null)
        {
            q = q.Where(z => visibleProjectIds.Contains(z.ProjektId));
        }

        const string coll = SearchQueryText.AccentInsensitiveCollation;

        foreach (var term in terms)
        {
            var pattern = SearchQueryText.ToContainsPattern(term);

            q = q.Where(z =>
                EF.Functions.Like(EF.Functions.Collate(z.Nazev, coll), pattern)
                || (z.CisloViditelne != null
                    && EF.Functions.Like(EF.Functions.Collate(z.CisloViditelne, coll), pattern))
                || (z.Cil != null && EF.Functions.Like(EF.Functions.Collate(z.Cil, coll), pattern))
                || (z.Popis != null && EF.Functions.Like(EF.Functions.Collate(z.Popis, coll), pattern))
                || _db.Vyjadreni.Any(v => v.ZaznamId == z.Id
                    && EF.Functions.Like(EF.Functions.Collate(v.TextVyjadreni, coll), pattern))
                || _db.ZaznamExterniOdkazy.Any(o => o.ZaznamId == z.Id
                    && EF.Functions.Like(EF.Functions.Collate(o.Cislo, coll), pattern)));
        }

        var rows = await q
            .OrderBy(z => z.Nazev)
            .ThenBy(z => z.Id)
            .Take(limit)
            .Select(z => new
            {
                z.Id,
                z.ProjektId,
                z.Nazev,
                z.CisloViditelne,
                z.Cil,
                z.Popis,
                SubsystemKod = _db.Subsystemy
                    .Where(s => s.Id == z.SubsystemId)
                    .Select(s => s.Kod)
                    .FirstOrDefault(),
                Vyjadreni = _db.Vyjadreni
                    .Where(v => v.ZaznamId == z.Id)
                    .OrderBy(v => v.Id)
                    .Select(v => new
                    {
                        v.TextVyjadreni,
                        CisloJednani = _db.Jednani
                            .Where(j => j.Id == v.JednaniId)
                            .Select(j => (int?)j.CisloJednani)
                            .FirstOrDefault()
                    })
                    .ToList(),
                ExterniCisla = _db.ZaznamExterniOdkazy
                    .Where(o => o.ZaznamId == z.Id)
                    .OrderBy(o => o.Id)
                    .Select(o => o.Cislo)
                    .ToList()
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = new List<SearchResultItem>(rows.Count);

        foreach (var row in rows)
        {
            // Pořadí rozhoduje, co se ukáže na druhém řádku: nejdřív to, co uživatel
            // vidí na prvním (název, číslo), pak popis, pak vyjádření, nakonec odkaz.
            var kind = SearchMatchKind.Nazev;
            SearchSnippet? snippet =
                SearchQueryText.BuildSnippet(row.Nazev, terms)
                ?? SearchQueryText.BuildSnippet(row.CisloViditelne, terms);

            if (snippet is null)
            {
                snippet = SearchQueryText.BuildSnippet(row.Popis, terms)
                          ?? SearchQueryText.BuildSnippet(row.Cil, terms);
                if (snippet is not null)
                {
                    kind = SearchMatchKind.Popis;
                }
            }

            int? cisloJednani = null;

            if (snippet is null)
            {
                foreach (var v in row.Vyjadreni)
                {
                    snippet = SearchQueryText.BuildSnippet(v.TextVyjadreni, terms);
                    if (snippet is not null)
                    {
                        kind = SearchMatchKind.Vyjadreni;
                        cisloJednani = v.CisloJednani;
                        break;
                    }
                }
            }

            if (snippet is null)
            {
                foreach (var cislo in row.ExterniCisla)
                {
                    snippet = SearchQueryText.BuildSnippet(cislo, terms);
                    if (snippet is not null)
                    {
                        kind = SearchMatchKind.ExterniOdkaz;
                        break;
                    }
                }
            }

            items.Add(new SearchResultItem(
                ZaznamId: row.Id,
                ProjektId: row.ProjektId,
                Nazev: row.Nazev,
                CisloViditelne: row.CisloViditelne,
                SubsystemKod: row.SubsystemKod ?? string.Empty,
                MatchKind: kind,
                Snippet: snippet,
                CisloJednani: cisloJednani,
                DetailUrl: $"/Projekty/Detail/{row.ProjektId}?recordId={row.Id}"));
        }

        _logger.LogDebug("Vyhledávání '{Query}': {Count} výsledků.", trimmed, items.Count);

        return new SearchResult(trimmed,
        [
            new SearchResultCategory(SearchCategoryKeys.Zaznamy, "Záznamy", items)
        ]);
    }
}
```

- [ ] **Step 6: Spusť test, ověř že prochází**

```bash
dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~RecordSearchServiceTests" -v q --nologo
```
Očekávej: 15 PASS.

Pokud `EF.Functions.Collate` spadne na „could not be translated", zkontroluj, že `coll` je `const string` — EF vyžaduje konstantu, ne proměnnou.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Services/Search/IRecordSearchService.cs \
        PmTracker.Web/Services/Search/RecordSearchService.cs \
        PmTracker.Tests.Integration/DataStore/RecordSearchServiceTests.cs \
        PmTracker.Tests.Integration/TestInfrastructure/SearchSeed.cs
git commit -m "$(cat <<'EOF'
feat(search): vyhledávací služba nad ostrými tabulkami

Jeden EF dotaz s EXISTS na vyjádření a externí odkazy, autorizace jako
WHERE ve stejném dotazu. Bez indexové vrstvy, výsledky vždy čerstvé.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: Endpointy — dropdown JSON a stránka

**Files:**
- Modify: `PmTracker.Web/Controllers/SearchController.cs` (nahradit obsah)
- Create: `PmTracker.Web/Models/ViewModels/Search/SearchPageViewModel.cs`
- Modify: `PmTracker.Web/Services/Search/SearchServiceCollectionExtensions.cs`
- Test: `PmTracker.Tests.Api/Controllers/SearchEndpointsTests.cs`

**Interfaces:**
- Consumes: `IRecordSearchService`, `RecordSearchService.DropdownLimit`, `SearchResult` (Tasky 3 a 5).
- Produces:
  - `GET /Search/Suggest?q=…` → JSON `{ query, totalCount, categories: [{ key, nazev, items: [{ zaznamId, nazev, cisloViditelne, subsystemKod, matchKind, snippet: { before, match, after }, cisloJednani, detailUrl }] }] }`
  - `GET /Search/Index?q=…` → stránka
  - `SearchPageViewModel { string Query; SearchResult Result; }`

- [ ] **Step 1: Napiš failing test**

Vytvoř `PmTracker.Tests.Api/Controllers/SearchEndpointsTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

public sealed class SearchEndpointsTests : IClassFixture<PmTrackerWebAppFactory>
{
    private readonly PmTrackerWebAppFactory _factory;

    public SearchEndpointsTests(PmTrackerWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Suggest_PodPrahemTriZnaku_VraciPrazdnoBezChyby()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Search/Suggest?q=za");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Suggest_VraciOcekavanyTvarJson()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Search/Suggest?q=zal");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.TryGetProperty("query", out _).Should().BeTrue();
        json.TryGetProperty("totalCount", out _).Should().BeTrue();
        json.TryGetProperty("categories", out var categories).Should().BeTrue();
        categories.ValueKind.Should().Be(JsonValueKind.Array,
            "kategorie jsou pole, aby šlo přidat další bez změny kontraktu");
    }

    [Fact]
    public async Task Index_SeVykresli()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Search/Index?q=zal");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reindex_UzNeexistuje()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/Search/Reindex", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "endpoint zanikl spolu s indexovou vrstvou");
    }

    [Fact]
    public async Task Status_UzNeexistuje()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Search/Status");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

- [ ] **Step 2: Spusť test, ověř že padá**

```bash
dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~SearchEndpointsTests" -v q --nologo
```
Očekávej: `Reindex_UzNeexistuje` a `Status_UzNeexistuje` FAIL (endpointy zatím existují), tvar JSON FAIL.

- [ ] **Step 3: Vytvoř view model**

Vytvoř `PmTracker.Web/Models/ViewModels/Search/SearchPageViewModel.cs`:

```csharp
using PmTracker.Web.Services.Search;

namespace PmTracker.Web.Models.ViewModels.Search;

/// <summary>
/// Dědí <see cref="BaseViewModel"/> — bez toho neprojde generická podmínka
/// <c>AttachCurrentUser&lt;T&gt; where T : BaseViewModel</c> v BaseController.
/// </summary>
public sealed class SearchPageViewModel : BaseViewModel
{
    public required string Query { get; init; }
    public required SearchResult Result { get; init; }
}
```

- [ ] **Step 4: Přepiš controller**

Nahraď celý obsah `PmTracker.Web/Controllers/SearchController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PmTracker.Web.Models.ViewModels.Search;
using PmTracker.Web.Services.Search;

namespace PmTracker.Web.Controllers;

[Authorize]
public sealed class SearchController : BaseController
{
    private readonly IRecordSearchService _search;

    public SearchController(IRecordSearchService search) => _search = search;

    /// <summary>Data pro dropdown pod vyhledávacím polem.</summary>
    [HttpGet]
    [EnableRateLimiting("search")]
    public async Task<IActionResult> Suggest(string? q, CancellationToken cancellationToken)
    {
        var result = await _search.SearchAsync(
            q, CurrentUserContext, RecordSearchService.DropdownLimit, cancellationToken);

        return Json(new
        {
            query = result.Query,
            totalCount = result.TotalCount,
            categories = result.Categories.Select(c => new
            {
                key = c.Key,
                nazev = c.Nazev,
                items = c.Items.Select(i => new
                {
                    zaznamId = i.ZaznamId,
                    nazev = i.Nazev,
                    cisloViditelne = i.CisloViditelne,
                    subsystemKod = i.SubsystemKod,
                    matchKind = i.MatchKind.ToString(),
                    snippet = i.Snippet is null ? null : new
                    {
                        before = i.Snippet.Before,
                        match = i.Snippet.Match,
                        after = i.Snippet.After
                    },
                    cisloJednani = i.CisloJednani,
                    detailUrl = i.DetailUrl
                })
            })
        });
    }

    /// <summary>Stránka výsledků.</summary>
    [HttpGet]
    [EnableRateLimiting("search")]
    public async Task<IActionResult> Index(string? q, CancellationToken cancellationToken)
    {
        var result = await _search.SearchAsync(
            q, CurrentUserContext, PageLimit, cancellationToken);

        SetSectionRootBreadcrumb("Hledání");

        return View(AttachCurrentUser(new SearchPageViewModel
        {
            Query = result.Query,
            Result = result
        }));
    }

    /// <summary>Kolik výsledků se načte na stránce výsledků.</summary>
    private const int PageLimit = 40;
}
```

**Ověřené členy `BaseController`** (nepřepisovat podle paměti):
- `protected CurrentUserContextViewModel CurrentUserContext { get; }` — `BaseController.cs:19`
- `protected T AttachCurrentUser<T>(T model) where T : BaseViewModel` — `BaseController.cs:45`. Podmínka je důvod, proč `SearchPageViewModel` dědí `BaseViewModel`.
- `protected void SetSectionRootBreadcrumb(string text)` — `BaseController.Breadcrumbs.cs:17` (partial, ne hlavní soubor).

- [ ] **Step 5: Zaregistruj služby**

V `PmTracker.Web/Services/Search/SearchServiceCollectionExtensions.cs` nahraď registrace vyhledávání:

```csharp
services.AddScoped<IProjectVisibilityResolver, ProjectVisibilityResolver>();
services.AddScoped<IRecordSearchService, RecordSearchService>();
```

Odstraň registrace `ISearchClient`, `ISearchIndexer`, `IGlobalSearchService`, `IDbSuggestService`, `IEmbeddingService` a `AddHostedService<SearchReindexHostedService>()`.

- [ ] **Step 6: Spusť test, ověř že prochází**

```bash
dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~SearchEndpointsTests" -v q --nologo
```
Očekávej: 5 PASS.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Controllers/SearchController.cs \
        PmTracker.Web/Models/ViewModels/Search/SearchPageViewModel.cs \
        PmTracker.Web/Services/Search/SearchServiceCollectionExtensions.cs \
        PmTracker.Tests.Api/Controllers/SearchEndpointsTests.cs
git commit -m "$(cat <<'EOF'
feat(search): endpointy Suggest a Index nad novou službou

Reindex a Status zanikají spolu s indexovou vrstvou. JSON nese kategorie
jako pole, aby přidání další nezměnilo kontrakt.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 7: Dropdown — dvouřádkové položky

Dokumentovaná lokální odchylka od Design systému (spec §3.1): pole zůstává gov, seznam si kreslí aplikace.

**Files:**
- Modify: `PmTracker.Web/wwwroot/js/global-search.js` (přepsat vykreslování)
- Modify: `PmTracker.Web/wwwroot/css/site.css` (sekce `.app-search-dropdown`)
- Create: `docs/known-issues/ds-fis-odchylky.md`
- Test: `PmTracker.Tests.Unit/Search/SearchDropdownMarkupTests.cs`

**Interfaces:**
- Consumes: JSON z `GET /Search/Suggest` (Task 6).
- Produces: položka dropdownu s třídami `app-search-item`, `app-search-item__title`, `app-search-item__meta`, `app-search-item__snippet`, `app-search-item__badge`, `app-search-hl`.

- [ ] **Step 1: Napiš failing test**

Vytvoř `PmTracker.Tests.Unit/Search/SearchDropdownMarkupTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Search;

public sealed class SearchDropdownMarkupTests
{
    private static string Js() =>
        File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/global-search.js"));

    private static string Css() =>
        File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));

    [Fact]
    public void Dropdown_HledaAzOdTriZnaku()
    {
        Js().Should().MatchRegex(@"length\s*<\s*3",
            "práh je 3 znaky — musí sedět se serverem (SearchQueryText.MinQueryLength)");
    }

    [Fact]
    public void Dropdown_MaDebounceAAbortController()
    {
        var js = Js();

        js.Should().Contain("AbortController", "předchozí dotaz se musí rušit");
        js.Should().MatchRegex(@"setTimeout\([^)]*,\s*(150|200)\s*\)",
            "debounce 150–200 ms; bez něj by 10 úhozů poslalo 10 dotazů a narazilo na rate limit");
    }

    [Fact]
    public void Dropdown_VykresliDvaRadky()
    {
        var js = Js();

        js.Should().Contain("app-search-item__title");
        js.Should().Contain("app-search-item__snippet");
        js.Should().Contain("app-search-item__meta", "zkratka subsystému a číslo jednání jdou vpravo");
    }

    [Fact]
    public void Dropdown_ZvyrazniShoduBezVkladaniHtmlZeServeru()
    {
        var js = Js();

        js.Should().Contain("app-search-hl", "zvýraznění má vlastní třídu, ne inline styl");
        js.Should().NotContain("innerHTML",
            "text ze serveru se nesmí vkládat jako HTML — skládej uzly přes textContent");
    }

    [Fact]
    public void Dropdown_MaCssProDvousloupcovyLayoutAZvyrazneni()
    {
        var css = Css();

        css.Should().Contain(".app-search-item");
        css.Should().Contain(".app-search-hl");
    }

    [Fact]
    public void Odchylka_JeZdokumentovana()
    {
        // Pravidlo 4 Design systému: jednorázová odchylka se musí poznamenat.
        var doc = File.ReadAllText(ResolvePath("docs/known-issues/ds-fis-odchylky.md"));

        doc.Should().Contain("gov-form-autocomplete");
        doc.Should().Contain("app-search-dropdown");
    }
}
```

- [ ] **Step 2: Spusť test, ověř že padá**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~SearchDropdownMarkupTests" -v q --nologo
```
Očekávej: 6 FAIL.

- [ ] **Step 3: Přepiš vykreslování v `global-search.js`**

Nahraď funkci, která staví položky dropdownu (dnes `renderHits`), tímto. Zbytek souboru — obsluha kláves, zavírání, `AbortController` — se nemění, jen práh a debounce:

```javascript
/** Popisek druhého řádku podle toho, proč se záznam našel. */
var MATCH_BADGE = {
    Nazev: '',
    Popis: 'popis',
    Vyjadreni: '',
    ExterniOdkaz: 'Ext. záz.'
};

function buildSnippetNode(item) {
    var wrap = document.createElement('span');
    wrap.className = 'app-search-item__snippet';

    var snippet = item.snippet;
    if (!snippet) {
        wrap.textContent = item.nazev;
        return wrap;
    }

    // Skládá se z uzlů, ne z innerHTML — text pochází z databáze a nesmí se
    // interpretovat jako HTML.
    wrap.appendChild(document.createTextNode(snippet.before || ''));

    var hl = document.createElement('mark');
    hl.className = 'app-search-hl';
    hl.textContent = snippet.match || '';
    wrap.appendChild(hl);

    wrap.appendChild(document.createTextNode(snippet.after || ''));
    return wrap;
}

function buildMetaText(item) {
    if (item.matchKind === 'Vyjadreni' && item.cisloJednani) {
        return String(item.cisloJednani);
    }
    return MATCH_BADGE[item.matchKind] || '';
}

function renderItem(item) {
    var li = document.createElement('li');
    li.className = 'app-search-item';
    li.setAttribute('role', 'option');
    li.dataset.url = item.detailUrl;

    // První řádek: název záznamu vlevo, zkratka subsystému vpravo.
    var rowTitle = document.createElement('span');
    rowTitle.className = 'app-search-item__row';

    var title = document.createElement('span');
    title.className = 'app-search-item__title';
    title.textContent = item.cisloViditelne
        ? item.cisloViditelne + ' — ' + item.nazev
        : item.nazev;
    rowTitle.appendChild(title);

    var subsystem = document.createElement('span');
    subsystem.className = 'app-search-item__meta';
    subsystem.textContent = item.subsystemKod || '';
    rowTitle.appendChild(subsystem);

    // Druhý řádek: proč se záznam našel.
    var rowSnippet = document.createElement('span');
    rowSnippet.className = 'app-search-item__row';
    rowSnippet.appendChild(buildSnippetNode(item));

    var badge = document.createElement('span');
    badge.className = 'app-search-item__badge';
    badge.textContent = buildMetaText(item);
    rowSnippet.appendChild(badge);

    li.appendChild(rowTitle);
    li.appendChild(rowSnippet);
    return li;
}

function renderHits(payload) {
    var list = document.createElement('ul');
    list.className = 'app-search-list';

    (payload.categories || []).forEach(function (category) {
        (category.items || []).forEach(function (item) {
            list.appendChild(renderItem(item));
        });
    });

    return list;
}
```

Dále v témže souboru změň práh a debounce:

```javascript
if (q.length < 3) { clearDropdown(); return; }
timer = setTimeout(function () { suggest(q); }, 200);
```

- [ ] **Step 4: Doplň CSS**

Do `PmTracker.Web/wwwroot/css/site.css`, k dosavadní sekci `.app-search-dropdown`:

```css
/* Dropdown vyhledávání — lokální odchylka od DS FIS, viz docs/known-issues/ds-fis-odchylky.md.
   gov-form-autocomplete umí jen jednořádkový text, proto si seznam kreslíme sami. */
.app-search-list {
    list-style: none;
    margin: 0;
    padding: 0;
}

.app-search-item {
    display: flex;
    flex-direction: column;
    gap: 0.125rem;
    padding: 0.5rem 0.75rem;
    cursor: pointer;
}

.app-search-item:hover,
.app-search-item:focus,
.app-search-item[aria-selected="true"] {
    background: var(--pm-surface);
}

/* 80 / 20 — text vlevo, metadata vpravo. */
.app-search-item__row {
    display: flex;
    align-items: baseline;
    gap: 0.5rem;
}

.app-search-item__title,
.app-search-item__snippet {
    flex: 1 1 80%;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.app-search-item__meta,
.app-search-item__badge {
    flex: 0 0 20%;
    text-align: right;
    font-size: 0.8125rem;
    color: var(--pm-text-muted);
    white-space: nowrap;
}

.app-search-item__snippet {
    font-size: 0.8125rem;
    color: var(--pm-text-muted);
}

.app-search-hl {
    background: #fde68a;
    color: inherit;
    padding: 0 0.0625rem;
    border-radius: 2px;
}
```

**Pozor:** `--pm-*` proměnné jsou aliasované na gov tokeny v `site.css :root`. Před přidáním pravidla ověř grepem, že stejný selektor není definovaný i v `wwwroot/css/components/` — `site.css` se načítá později a přebil by ho.

- [ ] **Step 5: Zdokumentuj odchylku**

Vytvoř `docs/known-issues/ds-fis-odchylky.md`:

```markdown
# Lokální odchylky od Design systému FIS

Evidence podle pravidla 4 v `DesignSystem-FIS-v1.0.0/README.md`: odchylka se
nedělá v souborech Design systému, ale v souborech aplikace s prefixem `app-`,
a poznamená se sem.

## 1. Dropdown vyhledávání (2026-09-17)

**Čeho se týká:** `gov-form-autocomplete` v hlavičce.

**Co DS předepisuje:** vyhledávací pole s komponentou `gov-form-autocomplete`,
která si seznam výsledků vykresluje sama. Plní se přes property `options`
plochými řetězci (`[{ name: "text" }]`).

**Proč se odchylujeme:** komponenta nemá API pro vlastní vykreslení položky.
Zadání vyžaduje dvouřádkovou položku se zkratkou subsystému a číslem jednání
zarovnanými vpravo a se zvýrazněnou shodou — to se do plochého řetězce nevejde.

**Jak je odchylka provedena:** vyhledávací **pole** zůstává gov
(`gov-form-search` + `gov-form-input`). **Seznam výsledků** kreslí aplikace
v `.app-search-dropdown` / `.app-search-item*` (soubory `wwwroot/js/global-search.js`
a `wwwroot/css/site.css`). Soubory `assets/gov/**` ani `assets/ds-fis/*` se needitují.

**Podklad pro případnou centrální úpravu DS:** `gov-form-autocomplete` by
potřebovala slot nebo callback pro vykreslení položky (obdoba `renderOption`),
aby šlo zobrazit víceřádkovou položku s metadaty. Patřilo by to do DS gov
(komponenta), ne do nadstavby DS FIS.
```

- [ ] **Step 6: Spusť test, ověř že prochází**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~SearchDropdownMarkupTests" -v q --nologo
```
Očekávej: 6 PASS.

- [ ] **Step 7: Ověř v prohlížeči**

Spusť aplikaci a napiš do vyhledávání výraz, který se vyskytuje v názvu, v popisu, ve vyjádření a v čísle externího odkazu. U každého typu ověř, že druhý řádek ukazuje to správné a že vpravo je u vyjádření číslo jednání.

**Pozn. k JS:** `asp-append-version` verzuje jen vstupní soubor, ne ESM sub-importy. Když se změna neprojeví ani po hard-refresh, je to cache, ne chyba v kódu.

- [ ] **Step 8: Commit**

```bash
git add PmTracker.Web/wwwroot/js/global-search.js PmTracker.Web/wwwroot/css/site.css \
        docs/known-issues/ds-fis-odchylky.md \
        PmTracker.Tests.Unit/Search/SearchDropdownMarkupTests.cs
git commit -m "$(cat <<'EOF'
feat(search): dvouřádkový dropdown se zvýrazněním shody

Lokální odchylka od DS FIS podle pravidla 4 — gov-form-autocomplete neumí
víceřádkové položky s metadaty. Pole zůstává gov, seznam kreslí aplikace.
Odchylka zdokumentována v docs/known-issues/ds-fis-odchylky.md.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 8: Stránka výsledků

**Files:**
- Replace: `PmTracker.Web/Views/Search/Index.cshtml`
- Create: `PmTracker.Web/Views/Search/_SearchResultCategory.cshtml`
- Test: `PmTracker.Tests.Api/Controllers/SearchPageRenderTests.cs`

**Interfaces:**
- Consumes: `SearchPageViewModel` (Task 6), `SearchResultCategory`, `SearchResultItem` (Task 3).

- [ ] **Step 1: Napiš failing test**

Vytvoř `PmTracker.Tests.Api/Controllers/SearchPageRenderTests.cs`:

```csharp
using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

public sealed class SearchPageRenderTests : IClassFixture<PmTrackerWebAppFactory>
{
    private readonly PmTrackerWebAppFactory _factory;

    public SearchPageRenderTests(PmTrackerWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Stranka_PouzivaGovKostruSablony()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Search/Index?q=zal");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();

        // Kostra podle gov šablony „Výsledky vyhledávání".
        html.Should().Contain("gov-page-heading");
        html.Should().Contain("gov-card");
    }

    [Fact]
    public async Task Stranka_RenderujeKategorieJakoSekce()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Search/Index?q=zal");
        var html = await response.Content.ReadAsStringAsync();

        // Kategorie je objekt — markup na ni musí být připravený i při jediné.
        html.Should().Contain("data-search-category");
    }

    [Fact]
    public async Task Stranka_MaZvyrazneniShody()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Search/Index?q=zal");
        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("app-search-hl", "zvýraznění je stejné jako v dropdownu");
    }
}
```

**Pozn.:** Razor kóduje diakritiku na HTML entity, takže na český text v HTML testech se nekotvi — testuj atributy a ASCII třídy.

- [ ] **Step 2: Spusť test, ověř že padá**

```bash
dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~SearchPageRenderTests" -v q --nologo
```

- [ ] **Step 3: Napiš partial kategorie**

Vytvoř `PmTracker.Web/Views/Search/_SearchResultCategory.cshtml`:

```razor
@using PmTracker.Web.Services.Search
@model SearchResultCategory

@* Kategorie výsledků. Dnes jediná (Záznamy), partial je ale samostatný, aby
   přidání další znamenalo jen další položku v kolekci, ne zásah do stránky. *@
<section class="app-search-results" data-search-category="@Model.Key">
    <h2 class="gov-text--body-l">@Model.Nazev <span class="gov-color--text-secondary">(@Model.Items.Count)</span></h2>

    <gov-card-grid>
        @foreach (var item in Model.Items)
        {
            <article>
                <gov-card direction="horizontal" href="@item.DetailUrl">
                    <gov-flex gap="s" direction="column">
                        <h3 class="gov-card__headline">
                            @if (!string.IsNullOrEmpty(item.CisloViditelne))
                            {
                                <span class="gov-color--text-secondary">@item.CisloViditelne</span>
                                <text> — </text>
                            }
                            @item.Nazev
                            <span class="app-search-item__meta">@item.SubsystemKod</span>
                        </h3>

                        @if (item.Snippet is not null)
                        {
                            <p class="app-search-item__snippet">
                                @item.Snippet.Before<mark class="app-search-hl">@item.Snippet.Match</mark>@item.Snippet.After
                                @if (item.MatchKind == SearchMatchKind.Vyjadreni && item.CisloJednani.HasValue)
                                {
                                    <span class="app-search-item__badge">@item.CisloJednani</span>
                                }
                                else if (item.MatchKind == SearchMatchKind.Popis)
                                {
                                    <span class="app-search-item__badge">popis</span>
                                }
                                else if (item.MatchKind == SearchMatchKind.ExterniOdkaz)
                                {
                                    <span class="app-search-item__badge">Ext. záz.</span>
                                }
                            </p>
                        }
                    </gov-flex>
                </gov-card>
            </article>
        }
    </gov-card-grid>
</section>
```

- [ ] **Step 4: Přepiš stránku**

Nahraď celý obsah `PmTracker.Web/Views/Search/Index.cshtml`:

```razor
@using PmTracker.Web.Models.ViewModels.Search
@model SearchPageViewModel
@{
    ViewData["Title"] = "Výsledky vyhledávání";
}

@* Kostra podle gov šablony „Výsledky vyhledávání".
   Místo pro filtry vlevo zůstává prázdné — dokud je kategorie jediná,
   není podle čeho filtrovat (spec 2026-09-17 §4). *@
<header class="gov-page-heading">
    <h1>
        Vyhledávání „<b>@Model.Query</b>"
    </h1>
    <span class="gov-color--text-secondary">@Model.Result.TotalCount výsledků vyhledávání</span>
</header>

@if (Model.Result.TotalCount == 0)
{
    <p class="gov-text--body-m" data-search-empty>
        Nic nenalezeno. Zkuste jiný výraz — hledá se od tří znaků a v názvu,
        popisu, vyjádřeních a číslech externích záznamů.
    </p>
}
else
{
    foreach (var category in Model.Result.Categories)
    {
        <partial name="_SearchResultCategory" model="category" />
    }
}
```

- [ ] **Step 5: Spusť test, ověř že prochází**

```bash
dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~SearchPageRenderTests" -v q --nologo
```
Očekávej: 3 PASS.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Views/Search/ PmTracker.Tests.Api/Controllers/SearchPageRenderTests.cs
git commit -m "$(cat <<'EOF'
feat(search): stránka výsledků podle gov šablony

Kategorie je samostatný partial — přidání další kategorie nevyžaduje
zásah do stránky. Místo pro filtry vlevo zatím zůstává prázdné.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 9: Odstranění staré vrstvy

Až teď, když nová cesta funguje end-to-end.

**Files:**
- Delete: `PmTracker.Web/Services/Search/` — `AzureOpenAIEmbeddingService.cs`, `DbSuggestService.cs`, `EntityDocumentMapper.cs`, `GlobalSearchModels.cs`, `GlobalSearchService.cs`, `IDbSuggestService.cs`, `IEmbeddingService.cs`, `IGlobalSearchService.cs`, `ISearchClient.cs`, `ISearchIndexer.cs`, `NullEmbeddingService.cs`, `OpenSearchClient.cs`, `OpenSearchIndexSettings.cs`, `SearchAcl.cs`, `SearchDocument.cs`, `SearchIndexer.cs`, `SearchProvisioningRemedy.cs`, `SearchQueryContracts.cs`, `SearchReindexHostedService.cs`, `SearchViewModels.cs`, `SqlServerSearchClient.cs`
- Delete: `PmTracker.Tests.Unit/Search/` — `SearchBootstrapTests.cs`, `SearchProvisioningTests.cs`, `GlobalSearchServiceTests.cs`, `SearchAclTests.cs`, `GlobalSearchMarkupTests.cs` (jen pokud testuje zaniklý markup)
- Delete: `PmTracker.Tests.Integration/DataStore/SearchLikeQueryTests.cs`
- Modify: `PmTracker.Web/Services/Search/SearchOptions.cs` (zúžit)
- Modify: `PmTracker.Web/Data/PmTrackerDbContext.cs` (odebrat `SearchReindexCheckpoint`)
- Delete: `SearchReindexCheckpointEntity` z `PmTracker.Web/Models/Entities/`
- Modify: `PmTracker.Web/Views/Profil/Index.cshtml` (odebrat admin kartu)
- Modify: `PmTracker.Web/Models/ViewModels/SecurityViewModels.cs`, `PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs` (odebrat `search.reindex`)
- Modify: `PmTracker.Web/appsettings.example.json`
- Modify: `docs/specs/global-search.md` (přepsat na nový stav)

- [ ] **Step 1: Napiš failing test**

Přidej do `PmTracker.Tests.Unit/Search/SearchProvisioningTests.cs` — nebo vytvoř nový `SearchLegacyRemovedTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Search;

/// <summary>
/// Indexová vrstva se ruší bez náhrady (spec 2026-09-17 §5). Test drží úklid,
/// aby se nevrátila zadními vrátky.
/// </summary>
public sealed class SearchLegacyRemovedTests
{
    [Theory]
    [InlineData("SqlServerSearchClient.cs")]
    [InlineData("OpenSearchClient.cs")]
    [InlineData("ISearchClient.cs")]
    [InlineData("SearchIndexer.cs")]
    [InlineData("SearchReindexHostedService.cs")]
    [InlineData("EntityDocumentMapper.cs")]
    [InlineData("SearchAcl.cs")]
    [InlineData("DbSuggestService.cs")]
    [InlineData("IEmbeddingService.cs")]
    public void ZanikleSoubory_JizNeexistuji(string fileName)
    {
        File.Exists(ResolvePath($"PmTracker.Web/Services/Search/{fileName}"))
            .Should().BeFalse($"{fileName} patřil ke zrušené indexové vrstvě");
    }

    [Fact]
    public void Seed_JizNeobsahujeKlicSearchReindex()
    {
        var seed = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs"));

        seed.Should().NotContain("search.reindex");
        seed.Should().Contain("search.index", "vyhledávání zůstává, klíč pro jeho použití taky");
    }

    [Fact]
    public void DbContext_JizNemaCheckpointTabulku()
    {
        var ctx = File.ReadAllText(ResolvePath("PmTracker.Web/Data/PmTrackerDbContext.cs"));

        ctx.Should().NotContain("SearchReindexCheckpoint");
    }

    [Fact]
    public void ProfilIndex_JizNemaAdminKartuVyhledavani()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Profil/Index.cshtml"));

        view.Should().NotContain("data-search-admin-card");
        view.Should().NotContain("data-search-reindex-trigger");
    }
}
```

- [ ] **Step 2: Spusť test, ověř že padá**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~SearchLegacyRemovedTests" -v q --nologo
```

- [ ] **Step 3: Smaž zaniklé soubory**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
git rm PmTracker.Web/Services/Search/{AzureOpenAIEmbeddingService,DbSuggestService,EntityDocumentMapper,GlobalSearchModels,GlobalSearchService,IDbSuggestService,IEmbeddingService,IGlobalSearchService,ISearchClient,ISearchIndexer,NullEmbeddingService,OpenSearchClient,OpenSearchIndexSettings,SearchAcl,SearchDocument,SearchIndexer,SearchProvisioningRemedy,SearchQueryContracts,SearchReindexHostedService,SearchViewModels,SqlServerSearchClient}.cs
git rm PmTracker.Tests.Unit/Search/{SearchBootstrapTests,SearchProvisioningTests,GlobalSearchServiceTests,SearchAclTests}.cs
git rm PmTracker.Tests.Integration/DataStore/SearchLikeQueryTests.cs
```

- [ ] **Step 4: Ukliď zbylé odkazy**

Projdi kompilační chyby a odstraň:
- `SearchReindexCheckpointEntity` a její `DbSet` v `PmTrackerDbContext`
- `search.reindex` ze `SecurityViewModels.cs` (konstanta + definice) a z `PermissionSeedConfiguration.cs` (akce + dvě role)
- admin kartu vyhledávání z `Views/Profil/Index.cshtml`
- sekci `PmTracker:Search` v `appsettings.example.json` zúžit na to, co zbylo
- `SearchOptions.cs` zúžit nebo smazat, podle toho, co ještě někdo čte

```bash
dotnet build PmTracker.sln -v q --nologo
```

Opakuj, dokud build neprojde. **Nepřidávej** zpět nic, co jen „aby to šlo přeložit" — pokud něco drží mrtvý odkaz, smaž i to.

- [ ] **Step 5: Přepiš dokumentaci**

`docs/specs/global-search.md` popisuje dvojí implementaci, OpenSearch a `SearchIndex`. Přepiš ho na nový stav, nebo ho nahraď odkazem na spec a wiki. Zkontroluj taky `docs/wiki/uzivatelsky-dashboard/globalni-vyhledavani.md`, kde je popis LIKE chování — sedí, ale zmiňuje tabulku `dbo.SearchIndex`, která zaniká.

- [ ] **Step 6: Spusť všechny suity**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -v q --nologo
dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj -v q --nologo
dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj -v q --nologo
```

Známá **pre-existing** selhání, která s vyhledáváním nesouvisí a nemají se v rámci tohoto úkolu řešit:
- Api: 4× harmonogram/gantt (`ProjectHarmonogramRenderTests`, `RecordEditorControllerTests`)
- Integration: 1× `ProposalRejectAndTakeOverE2ETests`

Cokoli nad rámec tohoto seznamu je regrese z této práce.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "$(cat <<'EOF'
refactor(search): odstranění indexové vrstvy

Smazáno 21 souborů služeb, checkpoint entita, admin karta a klíč
search.reindex. Vyhledávání čte rovnou z ostrých tabulek.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

## Self-review plánu

**Pokrytí specifikace**

| Sekce specu | Task |
|---|---|
| §2.1 jednotka výsledku = záznam | 3, 5 |
| §2.2 rozsah polí | 5 |
| §2.3 autorizace | 4, 5 |
| §2.4 ostré tabulky | 5 |
| §2.5 diakritika a escapování | 2, 5 |
| §2.6 řazení vzestupně | 5 |
| §3.1 odchylka od DS | 7 |
| §3.2 rozvržení dropdownu | 7 |
| §3.3 chování dropdownu | 7 |
| §4 stránka výsledků | 6, 8 |
| §5 co se ruší | 1, 9 |
| §6 testy | v každém tasku |

**Konzistence názvů napříč tasky** — `SearchQueryText.SplitTerms`/`EscapeLikePattern`/`ToContainsPattern`/`BuildSnippet`, `SearchSnippet(Before, Match, After)`, `SearchMatchKind`, `SearchResultItem`, `SearchResultCategory`, `SearchResult.Empty`, `SearchCategoryKeys.Zaznamy`, `IProjectVisibilityResolver.Resolve`, `IRecordSearchService.SearchAsync`, `RecordSearchService.DropdownLimit` — použité tvary se shodují s definicemi.

**Rizika, na která narazí implementátor**

1. `EF.Functions.Collate` vyžaduje **konstantu** collation, ne proměnnou. V Tasku 5 je proto `const string coll`.
2. Legacy tabulky mají snake_case a místy diakritiku ve sloupcích. `SearchSeed` v Tasku 5 je nejpravděpodobnější místo, kde to spadne — ověřuj proti `sys.columns`, nedomýšlej.
3. Testovací kontejner je **Azure SQL Edge**, ne MSSQL 2022. Cokoli, co Edge neumí, shodí bootstrap databáze a s ním všechny Integration i Api testy.
4. Duplicitní CSS selektory: `site.css` se načítá po `components/` a přebíjí je. Před přidáním pravidla grepni, jestli stejný selektor neexistuje jinde.
5. `asp-append-version` neverzuje ESM sub-importy — JS změny se nemusí projevit ani po hard-refresh.

## Otevřené body ze specifikace

Tyto tři body spec nechal otevřené a plán pro ně zvolil pracovní řešení. Potvrdit při implementaci:

1. **Text při nulovém výsledku** — Task 8 používá „Nic nenalezeno. Zkuste jiný výraz — hledá se od tří znaků a v názvu, popisu, vyjádřeních a číslech externích záznamů."
2. **Přechod z dropdownu na stránku** — plán zatím ponechává stávající chování (odeslání formuláře tlačítkem Hledat). Položka „zobrazit všechny výsledky" v dropdownu není součástí žádného tasku.
3. **Stránkování** — Task 6 načítá pevně 40 výsledků, `gov-pagination` ani „Načíst dalších N" zatím nejsou implementované.
