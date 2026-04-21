# Fakturace Cleanup (krok #11) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Úplně odstranit krok harmonogramu #11 „fakturace" z aplikace i DB — seed, servisní konstanty, SQL upgrade skript pro existující instalace, aktualizace testů.

**Architecture:** Destruktivní in-place úprava. Jeden SQL upgrade skript (`db_upgrade_1_1_9_fakturace_cleanup.sql`) smaže data existujících instalací. Dva zdrojové soubory mění výchozí katalog kroků (`HarmonogramService.cs:51-64` + `HarmonogramCatalogService.cs:~521-534`). Dev seed (`db_seed_dev_admin.sql`) přestane vkládat HS11. Testy upravíme, aby očekávaly 10 kroků / 20 řádků místo 11 / 22. **Žádný API kontrakt se nemění**, jen data + defaults. Riziko: pending návrhy s referencí na HS11 — pokud existují, smažeme i je (uživatel potvrdil, že existující 1–2 návrhy lze zahodit).

**Tech Stack:** .NET 8 ASP.NET Core MVC, EF Core 8, SQL Server 2019+ (pmtracker-sql Docker container localhost:1433), xUnit + FluentAssertions, PowerShell/bash pro manuální ověření v Dev.

---

## File Structure

### Nové soubory
- `db_upgrade_1_1_9_fakturace_cleanup.sql` — destruktivní SQL skript pro existující DB (Dev i Prod).
- `PmTracker.Tests.Unit/Harmonogram/FakturaceCleanupTests.cs` — garantuje, že HS11 nikde v seedu / defaults nesedí.

### Modifikované soubory
- `PmTracker.Web/Services/Data/HarmonogramService.cs:51-64` — odstranit řádek HS11 z `DefaultHarmonogramKroky`.
- `PmTracker.Web/Services/Data/HarmonogramCatalogService.cs:~521-534` — odstranit řádek HS11 ze seznamu (v tomto souboru je `GetCanonicalHarmonogramSteps()` duplikát; odstranit tam taky).
- `db_seed_dev_admin.sql:156` a `db_seed_dev_admin.sql:167` — odstranit HS11_DURATION a HS11_DELAY INSERTy, opravit koncovou čárku na předchozím řádku.
- `PmTracker.Tests.Unit/Common/SeedBaselineDocumentationTests.cs:82` — nahradit `script.Should().Contain("N'HS11_DELAY'");` za `script.Should().NotContain("HS11");` (a `script.Should().Contain("N'HS10_DELAY'");` pro poslední krok).

### Soubory, které se záměrně NEMĚNÍ
- Stávající `db_upgrade_1_1_*.sql` — historie, žádné retrofitting.
- `docs/technical/06-database-bootstrap-migrations.md` — nezmíní konkrétní krok, popisuje jen tabulky.
- `.claude/worktrees/pedantic-kowalevski/db_seed_dev_admin.sql` — ignoruje se (je to worktree snapshot, ne aktivní kód).

---

## Pořadí úkolů

1. **Task 1** — Napsat regression guard test (očekává 10 kroků, ne 11) → musí failnout.
2. **Task 2** — Odstranit HS11 z `HarmonogramService.DefaultHarmonogramKroky`.
3. **Task 3** — Odstranit HS11 z `HarmonogramCatalogService` duplikátu.
4. **Task 4** — Upravit `SeedBaselineDocumentationTests.cs`.
5. **Task 5** — Odstranit HS11 z `db_seed_dev_admin.sql`.
6. **Task 6** — Napsat cleanup SQL skript `db_upgrade_1_1_9_fakturace_cleanup.sql`.
7. **Task 7** — Spustit cleanup skript proti Dev DB a ověřit.
8. **Task 8** — Build + full test pass + git status clean.

---

## Task 1: Regression guard test (failing)

**Files:**
- Create: `PmTracker.Tests.Unit/Harmonogram/FakturaceCleanupTests.cs`

- [ ] **Step 1: Napsat failující regression test**

Vytvoř soubor `PmTracker.Tests.Unit/Harmonogram/FakturaceCleanupTests.cs`:

```csharp
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Harmonogram;

public sealed class FakturaceCleanupTests
{
    private static string GetRepositoryRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
        {
            dir = dir.Parent;
        }
        dir.Should().NotBeNull("test must be executed from within repository tree");
        return dir!.FullName;
    }

    [Fact]
    public void HarmonogramService_DefaultHarmonogramKroky_ShouldNotContainFakturace()
    {
        var source = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(), "PmTracker.Web", "Services", "Data", "HarmonogramService.cs"));
        source.Should().NotContain("HS11_");
        source.Should().NotContain("fakturace");
    }

    [Fact]
    public void HarmonogramCatalogService_ShouldNotContainFakturace()
    {
        var source = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(), "PmTracker.Web", "Services", "Data", "HarmonogramCatalogService.cs"));
        source.Should().NotContain("HS11_");
        source.Should().NotContain("fakturace");
    }

    [Fact]
    public void DevSeedScript_ShouldNotContainFakturaceRows()
    {
        var seed = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db_seed_dev_admin.sql"));
        seed.Should().NotContain("HS11_DURATION");
        seed.Should().NotContain("HS11_DELAY");
        seed.Should().NotContain("fakturace");
    }

    [Fact]
    public void CleanupUpgradeScript_ShouldExistAndDeleteFakturaceRows()
    {
        var path = Path.Combine(GetRepositoryRoot(), "db_upgrade_1_1_9_fakturace_cleanup.sql");
        File.Exists(path).Should().BeTrue("upgrade script must exist");
        var script = File.ReadAllText(path);
        script.Should().Contain("DELETE FROM dbo.zaznam_harmonogram_hodnoty");
        script.Should().Contain("DELETE FROM dbo.ciselnik_harmonogram_typu");
        script.Should().Contain("HS11");
    }
}
```

- [ ] **Step 2: Spustit test (musí failnout — zatím existují reference)**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~FakturaceCleanupTests" --no-restore`

Expected: **4 failed** — všechny testy falí, protože HS11 je ještě všude a upgrade skript neexistuje.

- [ ] **Step 3: Commit testu**

```bash
git add PmTracker.Tests.Unit/Harmonogram/FakturaceCleanupTests.cs
git commit -m "test(fakturace-cleanup): regression guard — HS11 nesmí být v seedu/defaults (TDD red)"
```

---

## Task 2: Odstranit HS11 z HarmonogramService

**Files:**
- Modify: `PmTracker.Web/Services/Data/HarmonogramService.cs:51-64`

- [ ] **Step 1: Otevřít soubor a najít blok `DefaultHarmonogramKroky`**

Řádky 51–64 obsahují:

```csharp
private static readonly (int Poradi, string Kod, string Nazev, string BarvaHex)[] DefaultHarmonogramKroky =
[
    (1, "HS01_DURATION", "1. priprava zadani dodavateli", "#EF4444"),
    (2, "HS02_DURATION", "2. konzultace terminu s dodavatelem pred vytvorenim zadani", "#F97316"),
    (3, "HS03_DURATION", "3. odeslani zadani dodavateli", "#F59E0B"),
    (4, "HS04_DURATION", "4. dodani navrhu reseni", "#84CC16"),
    (5, "HS05_DURATION", "5. vyporadani pripominek", "#22C55E"),
    (6, "HS06_DURATION", "6. odeslani pozadavku na vyrobu", "#14B8A6"),
    (7, "HS07_DURATION", "7. dodani funkcionality dodavatelem", "#06B6D4"),
    (8, "HS08_DURATION", "8. pripominkovani", "#3B82F6"),
    (9, "HS09_DURATION", "9. testovani", "#6366F1"),
    (10, "HS10_DURATION", "10. nasazeni do provozu", "#8B5CF6"),
    (11, "HS11_DURATION", "11. fakturace", "#D946EF")
];
```

- [ ] **Step 2: Smazat poslední řádek a opravit čárku**

Nahraď blok za:

```csharp
private static readonly (int Poradi, string Kod, string Nazev, string BarvaHex)[] DefaultHarmonogramKroky =
[
    (1, "HS01_DURATION", "1. priprava zadani dodavateli", "#EF4444"),
    (2, "HS02_DURATION", "2. konzultace terminu s dodavatelem pred vytvorenim zadani", "#F97316"),
    (3, "HS03_DURATION", "3. odeslani zadani dodavateli", "#F59E0B"),
    (4, "HS04_DURATION", "4. dodani navrhu reseni", "#84CC16"),
    (5, "HS05_DURATION", "5. vyporadani pripominek", "#22C55E"),
    (6, "HS06_DURATION", "6. odeslani pozadavku na vyrobu", "#14B8A6"),
    (7, "HS07_DURATION", "7. dodani funkcionality dodavatelem", "#06B6D4"),
    (8, "HS08_DURATION", "8. pripominkovani", "#3B82F6"),
    (9, "HS09_DURATION", "9. testovani", "#6366F1"),
    (10, "HS10_DURATION", "10. nasazeni do provozu", "#8B5CF6")
];
```

(poslední řádek bez trailing čárky — C# collection expression zvládá oba tvary)

- [ ] **Step 3: Build**

Run: `dotnet build PmTracker.Web --no-restore`

Expected: 0 errors, 0 warnings.

- [ ] **Step 4: Spustit první test izolovaně (musí projít)**

Run: `dotnet test PmTracker.Tests.Unit --filter "HarmonogramService_DefaultHarmonogramKroky_ShouldNotContainFakturace" --no-restore`

Expected: 1 passed. Ostatní 3 testy ještě falí.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Data/HarmonogramService.cs
git commit -m "refactor(harmonogram): odstranit HS11 fakturace z HarmonogramService defaults"
```

---

## Task 3: Odstranit HS11 z HarmonogramCatalogService

**Files:**
- Modify: `PmTracker.Web/Services/Data/HarmonogramCatalogService.cs:~521-534`

- [ ] **Step 1: Najít přesné řádky**

Run: `grep -n "HS11\|fakturace" PmTracker.Web/Services/Data/HarmonogramCatalogService.cs`

Expected: jeden hit kolem řádku 533 (`(11, "HS11_DURATION", "11. fakturace", "#D946EF")` nebo podobně).

- [ ] **Step 2: Přečíst kontext**

Run: `sed -n '520,540p' PmTracker.Web/Services/Data/HarmonogramCatalogService.cs`

Zjistíš strukturu seznamu (podobná tuple/collection expression jako v Task 2).

- [ ] **Step 3: Smazat HS11 řádek**

Použij Edit:

```
old_string: (11, "HS11_DURATION", "11. fakturace", "#D946EF")
new_string: (10, "HS10_DURATION", "10. nasazeni do provozu", "#8B5CF6")
```

**POZOR:** pokud předchozí řádek (HS10) končí čárkou, musíš ji smazat u HS10 i HS11. Přesný edit záleží na výsledku Step 2. Pokud existuje trailing čárka na předposledním řádku, edituj:

```
old_string:         (10, "HS10_DURATION", "10. nasazeni do provozu", "#8B5CF6"),
        (11, "HS11_DURATION", "11. fakturace", "#D946EF")
new_string:         (10, "HS10_DURATION", "10. nasazeni do provozu", "#8B5CF6")
```

- [ ] **Step 4: Build**

Run: `dotnet build PmTracker.Web --no-restore`

Expected: 0 errors.

- [ ] **Step 5: Spustit druhý test izolovaně**

Run: `dotnet test PmTracker.Tests.Unit --filter "HarmonogramCatalogService_ShouldNotContainFakturace" --no-restore`

Expected: 1 passed.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/Data/HarmonogramCatalogService.cs
git commit -m "refactor(harmonogram): odstranit HS11 fakturace z HarmonogramCatalogService"
```

---

## Task 4: Upravit SeedBaselineDocumentationTests

**Files:**
- Modify: `PmTracker.Tests.Unit/Common/SeedBaselineDocumentationTests.cs:82`

- [ ] **Step 1: Přečíst řádky kolem 82**

Run: `sed -n '78,90p' PmTracker.Tests.Unit/Common/SeedBaselineDocumentationTests.cs`

Expected: vidíš řádek `script.Should().Contain("N'HS11_DELAY'");`

- [ ] **Step 2: Nahradit check**

Použij Edit:

```
old_string:         script.Should().Contain("N'HS01_DURATION'");
        script.Should().Contain("N'HS11_DELAY'");
new_string:         script.Should().Contain("N'HS01_DURATION'");
        script.Should().Contain("N'HS10_DELAY'");
        script.Should().NotContain("HS11");
```

- [ ] **Step 3: Spustit test (musí failnout — seed ještě obsahuje HS11)**

Run: `dotnet test PmTracker.Tests.Unit --filter "SeedBaselineDocumentationTests" --no-restore`

Expected: FAIL. Test teď říká „seed NESMÍ obsahovat HS11", ale seed ho ještě obsahuje. Task 5 to spraví.

- [ ] **Step 4: Commit změny v testu**

```bash
git add PmTracker.Tests.Unit/Common/SeedBaselineDocumentationTests.cs
git commit -m "test(seed-baseline): očekávat HS10 jako poslední krok místo HS11"
```

---

## Task 5: Odstranit HS11 z db_seed_dev_admin.sql

**Files:**
- Modify: `db_seed_dev_admin.sql:155-167`

- [ ] **Step 1: Přečíst oblast INSERTů (řádky 145–170)**

Run: `sed -n '145,170p' db_seed_dev_admin.sql`

Expected: vidíš 11 řádků DURATION (1–11) a 11 řádků DELAY (1–11).

- [ ] **Step 2: Smazat HS11_DURATION řádek (155–156)**

Použij Edit:

```
old_string:             (N'HS10_DURATION', N'10. nasazení do provozu', 10, 10, 0, N'#8B5CF6', CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8310')),
            (N'HS11_DURATION', N'11. fakturace', 11, 11, 0, N'#D946EF', CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8311')),
            (N'HS01_DELAY', N'1. příprava zadání dodavateli - zpoždění', 101, 1, 1, NULL, CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8301')),
new_string:             (N'HS10_DURATION', N'10. nasazení do provozu', 10, 10, 0, N'#8B5CF6', CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8310')),
            (N'HS01_DELAY', N'1. příprava zadání dodavateli - zpoždění', 101, 1, 1, NULL, CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8301')),
```

(Poslední DURATION řádek získá trailing čárku, která přemostí na HS01_DELAY — to je validní SQL.)

- [ ] **Step 3: Smazat HS11_DELAY řádek (166–167)**

Použij Edit:

```
old_string:             (N'HS10_DELAY', N'10. nasazení do provozu - zpoždění', 110, 10, 1, NULL, CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8310')),
            (N'HS11_DELAY', N'11. fakturace - zpoždění', 111, 11, 1, NULL, CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8311'))
new_string:             (N'HS10_DELAY', N'10. nasazení do provozu - zpoždění', 110, 10, 1, NULL, CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8310'))
```

(HS10_DELAY je teď poslední, ztratí trailing čárku.)

- [ ] **Step 4: Verifikace — žádný HS11 nezbývá**

Run: `grep -n "HS11\|fakturace" db_seed_dev_admin.sql || echo "CLEAN"`

Expected: `CLEAN`.

- [ ] **Step 5: Spustit testy**

Run: `dotnet test PmTracker.Tests.Unit --filter "DevSeedScript_ShouldNotContainFakturaceRows|SeedBaselineDocumentationTests" --no-restore`

Expected: oba passed.

- [ ] **Step 6: Commit**

```bash
git add db_seed_dev_admin.sql
git commit -m "chore(seed): odstranit HS11 fakturace řádky z dev seed scriptu"
```

---

## Task 6: Napsat db_upgrade_1_1_9_fakturace_cleanup.sql

**Files:**
- Create: `db_upgrade_1_1_9_fakturace_cleanup.sql`

- [ ] **Step 1: Vytvořit skript**

Vytvoř soubor `db_upgrade_1_1_9_fakturace_cleanup.sql` s obsahem:

```sql
-- =============================================================================
-- db_upgrade_1_1_9_fakturace_cleanup.sql
-- Odstraňuje krok harmonogramu #11 „fakturace" z existujících instalací.
-- Spec: docs/superpowers/specs/2026-04-21-servicedesk-vytezovani-vyjadreni-design.md §11
-- Destruktivní skript — spouštět s vědomím, že se smažou data.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

-- 1. Zjistit všechny Id řádků označených jako fakturace
DECLARE @FakturaceTypy TABLE (Id INT NOT NULL PRIMARY KEY);

INSERT @FakturaceTypy (Id)
SELECT Id
FROM dbo.ciselnik_harmonogram_typu
WHERE kod LIKE N'HS11[_]%'
   OR nazev LIKE N'%fakturace%';

-- 2. Report před smazáním
DECLARE @TypyCount INT = (SELECT COUNT(*) FROM @FakturaceTypy);
DECLARE @HodnotyCount INT = (
    SELECT COUNT(*)
    FROM dbo.zaznam_harmonogram_hodnoty
    WHERE typ_id IN (SELECT Id FROM @FakturaceTypy)
);

PRINT N'Fakturace typy ke smazání: ' + CAST(@TypyCount AS nvarchar(10));
PRINT N'Fakturace hodnoty ke smazání: ' + CAST(@HodnotyCount AS nvarchar(10));

-- 3. Smazat hodnoty (skutečnost + plán) pro fakturační typy
DELETE FROM dbo.zaznam_harmonogram_hodnoty
WHERE typ_id IN (SELECT Id FROM @FakturaceTypy);

-- 4. Smazat pending návrhy s referencí na HS11 (edge case)
--    payload_json je nvarchar(max), obsahuje TypId nebo kód kroku
DECLARE @NavrhyCount INT;
SELECT @NavrhyCount = COUNT(*)
FROM dbo.zaznam_navrhy
WHERE stav = N'PENDING'
  AND (payload_json LIKE N'%HS11[_]%' OR payload_json LIKE N'%fakturace%');

IF @NavrhyCount > 0
BEGIN
    PRINT N'Pending návrhy s referencí na fakturaci: ' + CAST(@NavrhyCount AS nvarchar(10));
    DELETE FROM dbo.zaznam_navrhy
    WHERE stav = N'PENDING'
      AND (payload_json LIKE N'%HS11[_]%' OR payload_json LIKE N'%fakturace%');
END;

-- 5. Smazat řádky ze číselníku
DELETE FROM dbo.ciselnik_harmonogram_typu
WHERE Id IN (SELECT Id FROM @FakturaceTypy);

-- 6. Verifikace
DECLARE @ZbyvajiTypy INT = (
    SELECT COUNT(*)
    FROM dbo.ciselnik_harmonogram_typu
    WHERE kod LIKE N'HS11[_]%' OR nazev LIKE N'%fakturace%'
);
DECLARE @ZbyvajiHodnoty INT = (
    SELECT COUNT(*)
    FROM dbo.zaznam_harmonogram_hodnoty zhh
    INNER JOIN dbo.ciselnik_harmonogram_typu cht ON cht.Id = zhh.typ_id
    WHERE cht.kod LIKE N'HS11[_]%'
);

IF @ZbyvajiTypy > 0 OR @ZbyvajiHodnoty > 0
BEGIN
    RAISERROR(N'Fakturace cleanup selhal: typy=%d, hodnoty=%d', 16, 1, @ZbyvajiTypy, @ZbyvajiHodnoty);
    ROLLBACK TRANSACTION;
    RETURN;
END;

PRINT N'Fakturace cleanup OK. Typy smazáno: ' + CAST(@TypyCount AS nvarchar(10))
    + N', hodnoty smazáno: ' + CAST(@HodnotyCount AS nvarchar(10))
    + N', návrhy smazáno: ' + CAST(@NavrhyCount AS nvarchar(10));

COMMIT TRANSACTION;
GO
```

- [ ] **Step 2: Spustit test pro upgrade skript**

Run: `dotnet test PmTracker.Tests.Unit --filter "CleanupUpgradeScript_ShouldExistAndDeleteFakturaceRows" --no-restore`

Expected: passed. Všechny 4 testy z Task 1 teď procházejí.

- [ ] **Step 3: Commit**

```bash
git add db_upgrade_1_1_9_fakturace_cleanup.sql
git commit -m "feat(db): db_upgrade_1_1_9 — cleanup fakturace (#11) pro existující instalace"
```

---

## Task 7: Spustit cleanup skript proti Dev DB

**Files:**
- (spouští skript, žádná úprava)

- [ ] **Step 1: Ověřit, že Dev SQL container běží**

Run: `docker ps | grep pmtracker-sql`

Expected: jeden běžící řádek s `pmtracker-sql` + port `1433`.

Pokud neběží: `docker start pmtracker-sql` → čekat 5s → ověřit znovu.

- [ ] **Step 2: Vytvořit PRE snapshot**

Vytvoř `/tmp/pre_snapshot.sql` s obsahem:

```sql
SELECT 'typy' AS Entity, COUNT(*) AS Count_
FROM dbo.ciselnik_harmonogram_typu
WHERE kod LIKE 'HS11%' OR nazev LIKE N'%fakturace%';

SELECT 'hodnoty' AS Entity, COUNT(*) AS Count_
FROM dbo.zaznam_harmonogram_hodnoty zhh
INNER JOIN dbo.ciselnik_harmonogram_typu cht ON cht.Id = zhh.typ_id
WHERE cht.kod LIKE 'HS11%';
```

Spusť:

```bash
cd /tmp/run-sql && dotnet run /tmp/pre_snapshot.sql
```

Expected: vidíš aktuální počty řádků — typy by měly být 2 (HS11_DURATION + HS11_DELAY), hodnoty = kolik máš testovacích záznamů.

- [ ] **Step 3: Spustit cleanup skript**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker" && cd /tmp/run-sql && dotnet run "/Users/Pavel.Andrlik/Documents/PM Tracker/db_upgrade_1_1_9_fakturace_cleanup.sql"
```

Expected: `PRINT` výstup „Fakturace cleanup OK. Typy smazáno: 2, hodnoty smazáno: N, návrhy smazáno: N", žádný RAISERROR.

- [ ] **Step 4: POST verifikace**

Spusť `pre_snapshot.sql` znovu:

```bash
cd /tmp/run-sql && dotnet run /tmp/pre_snapshot.sql
```

Expected: `typy = 0`, `hodnoty = 0`. Pokud cokoliv zůstalo, skript selhal (ale rollback by měl zabránit cokoliv smazat — takže spíš ladit než panikařit).

- [ ] **Step 5: Aplikace startne bez fakturace**

Ověřit, že aplikace nespadne po cleanupu:

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web" && ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build 2>&1 | head -40
```

Expected: běží, `Now listening on: http://localhost:5071` nebo podobně. Žádná fatální výjimka o chybějícím HS11.

Ukončit: `Ctrl+C` nebo `kill $(pgrep -f "PmTracker.Web/bin")`.

- [ ] **Step 6: Žádný commit v tomto kroku** (jen manuální validace).

---

## Task 8: Full test pass + finální commit pro plán

**Files:**
- (žádná úprava, jen verifikace + volitelný commit audit log)

- [ ] **Step 1: Kompletní build**

Run: `dotnet build --no-restore -c Debug`

Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Step 2: Full test run**

Run: `dotnet test PmTracker.Tests.Unit --no-restore --no-build`

Expected: všechny testy passed (číslo záleží na aktuálním počtu; před plánem bylo ~611, teď je +4 nové z Task 1 = ~615).

Pokud někde selže test odkazující na HS11 nebo fakturaci, který jsme nenašli v grepu — dopsat fix:

```bash
grep -rn "HS11\|fakturace" PmTracker.Web PmTracker.Tests.Unit 2>/dev/null
```

Pokud non-empty, najít + opravit, znovu test.

- [ ] **Step 3: Git status clean**

Run: `git status`

Expected: `nothing to commit, working tree clean`.

- [ ] **Step 4: Git log sanity check**

Run: `git log --oneline | head -8`

Expected: posledních 7 commitů z tohoto plánu:
1. `test(fakturace-cleanup): regression guard` (Task 1)
2. `refactor(harmonogram): odstranit HS11 z HarmonogramService` (Task 2)
3. `refactor(harmonogram): odstranit HS11 z HarmonogramCatalogService` (Task 3)
4. `test(seed-baseline): HS10 místo HS11` (Task 4)
5. `chore(seed): HS11 řádky pryč z dev seedu` (Task 5)
6. `feat(db): db_upgrade_1_1_9 cleanup fakturace` (Task 6)
7. (Task 7 manuální, bez commitu)

- [ ] **Step 5: Bez dalšího commitu — plán hotov**

Pokud všechno čisté, plán skončil. Žádné další tasky, žádný summary commit.

---

## Hotovo — Plán A

Po dokončení máš:
- ✅ Krok #11 pryč z `DefaultHarmonogramKroky` v obou servisních souborech.
- ✅ Dev seed skript čistý.
- ✅ Upgrade skript `db_upgrade_1_1_9_fakturace_cleanup.sql` pro existující Dev i Prod instalace.
- ✅ Regression test `FakturaceCleanupTests` hlídá, že se HS11 nikdy vrátit nemůže.
- ✅ `SeedBaselineDocumentationTests` očekává HS10 jako poslední.
- ✅ Dev DB prohlédnutá a ověřená po cleanupu.

### Mimo scope (dělá se v navazujících plánech)
- **Plán B** — karta externí vazby v2 + auto-sync ServiceDesk.
- **Plán C** — chat modal + automat vytěžování.
- **Plán D** — integrace s návrhovou vrstvou.

### Nasazení do produkce
Upgrade skript `db_upgrade_1_1_9_fakturace_cleanup.sql` **spustit na Prod** jen po domluvě — destruktivní. Dev ověřen v Task 7.
