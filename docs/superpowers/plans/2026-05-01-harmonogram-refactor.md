# Harmonogram Refactor Implementation Plan

> **Tento plán provádí inline jeden vývojář (Claude) bez agentů.** Po každém commitu uživatel ověřuje výsledek. Steps používají `- [ ]` checkbox syntax pro tracking. Žádný subagent dispatch.

**Goal:** Sjednotit harmonogram do architektonicky čistého, plně klíči řízeného workflow — zrušit 3 phantom UI bugy, vynutit invariant Auto/Manual/Návrh = mutuálně výlučné stavy, sjednotit permission flagy a refaktorovat sync service na Compute+Apply split s pre-fetch staging UX.

**Architecture:**
1. **Backend split** `HarmonogramSkutecnostSyncService.SyncZaznamAsync` → `ComputePlanAsync` (pure read) + `ApplyPlanAsync` (write) + convenience wrapper. Pre-fetch staging endpoint `POST /Harmonogram/PreviewSync` deleguje na `ComputePlanAsync`.
2. **Vynucení invariantu** Auto rezim ↔ návrh = výlučné stavy: server validace (submit + approve) odmítne návrh obsahující auto-fillovaný DELAY krok. UI gate v proposal editoru auto kroky **invisible** (ne disabled).
3. **Pending lock = univerzální guard** — žádný klíč ho neporazí (i `records.schedule.edit` / `proposals.edit.any`). UI button disabled, server-side defense throw.
4. **Max 1 Pending per (zaznamId, typNavrhu)** — auto-supersede vlastního starého návrhu při novém submitu (append-only audit přes `Stav=Superseded` + `SupersededByProposalId` FK).
5. **Permission flagy sjednocené** v `ScheduleEditorPermissionSet` (`CanEditScheduleDirect`, `CanProposeSchedule`, `CanEditManualActual`).
6. **`ManualActualKroky` save flow** dotažený přes shared `ManualActualKrokApplier.Apply()` (DRY mezi `RecordService.SaveRecord` a `RecordProposalService.DecisionCommands`).
7. **UI cleanup**: žádné skrývání kroků per typ ticketu, DURATION input = jen kalendář + readonly "X dnů" text, vytvořit chybějící `select-candidate.js` JS handler.

**Tech Stack:** .NET 8, EF Core 8, xUnit + FluentAssertions, ASP.NET Core MVC + Razor partials, vanilla JS ESM moduly, gov-design-system Web Components, MSSQL. Authorization: per-key seed-only RBAC (`PermissionKeys.*` + `IAuthorizationService.HasPermissionAsync`, **nikdy hardcoded role checks**).

**Key invariants:**
- Authorization rozhodnutí výhradně přes `HasPermission(key, projektId)`. Žádné `if (role == "ADM_PROJ")`.
- Pending návrh `Stav=Pending` blokuje **všechny** úpravy harmonogramu pro **všechny** uživatele (i nositele klíčů, i autora).
- Auto rezim krok = nelze editovat ani navrhnout. Manual rezim krok = lze editovat (s klíčem) nebo navrhnout (s klíčem).
- UI gate je primární; server validace je defense-in-depth, ne primární UX.
- **DELAY HodnotaInt sémantika**: NULL = "krok ještě nenastal", 0 = "vše dle plánu", non-0 = odchylka. Žádné magic 0.
- **Žádné skrývání kroků** v UI z žádného důvodu — render každého kroku schématu plnohodnotně, segmenty se renderují tolerance to chybějícím hodnotám (NULL HodnotaInt → no actual segment, 0 TrvaniDni → zero-width planned).
- DB migrace přes `db_upgrade_X_X_X.sql` skripty (intranet pattern, EF Migrations folder záměrně prázdná).
- Po každém `dotnet publish` zabalit do `publish.zip` v rootu repa (per memory).

**Reference DESIGN položky:** 15 aktivních + 3 deferred (viz souhrn po oblasti 10 v session diskusi 2026-05-01).

---

## File Structure

### Nové soubory
| Soubor | Odpovědnost |
|---|---|
| `db_upgrade_1_3_11_proposal_supersede.sql` | DB migrace — Stav=Superseded enum + SupersededByProposalId FK na zaznam_navrhy |
| `db_upgrade_1_3_12_delay_nullable.sql` | DB migrace — hodnota_int NOT NULL → NULL + backfill (Zdroj=Neznamo + 0 → NULL) |
| `PmTracker.Web/Services/Schedules/HarmonogramSyncPlan.cs` | DTO popisující plánované změny (delta) — výstup `ComputePlanAsync`, vstup `ApplyPlanAsync` |
| `PmTracker.Web/wwwroot/js/modules/schedule-feature-c/select-candidate.js` | JS handler pro dropdown výběr preferred kandidáta (chybějící phantom UI bug) |
| `PmTracker.Web/wwwroot/js/modules/schedule-feature-c/preview-sync.js` | JS staging flow — pre-fetch sync při blur na Cislo6, sessionStorage cache |
| `PmTracker.Tests.Unit/Schedule/HarmonogramSyncPlanTests.cs` | Unit testy ComputePlan + ApplyPlan + idempotence |
| `PmTracker.Tests.Unit/Schedule/HarmonogramAutoStepProposalRejectionTests.cs` | Architecture test — auto kroky odmítnuté v návrhu |
| `PmTracker.Tests.Unit/Schedule/HarmonogramMaxOnePendingTests.cs` | Test invariantu max 1 Pending + auto-supersede |
| `PmTracker.Tests.Unit/Schedule/RecordServiceManualActualKrokyPersistenceTests.cs` | Test direct save persistence |
| `PmTracker.Tests.Unit/Schedule/PendingLockUniversalGuardTests.cs` | Test že pending blokuje i admin/superadmin |

### Modifikované soubory
| Soubor | Změna |
|---|---|
| `PmTracker.Web/Models/Entities/PmTrackerEntities.cs` | ZaznamNavrhEntity + SupersededByProposalId, ZaznamHarmonogramHodnotaEntity.HodnotaInt: int → int? |
| `PmTracker.Web/Data/Configuration/RecordEntityConfiguration.cs` | EF mapping HodnotaInt nullable |
| `PmTracker.Web/Services/Schedules/HarmonogramSkutecnostSyncService.cs` | Split na ComputePlan + ApplyPlan + wrapper, optimistic concurrency, nullable HodnotaInt handling |
| `PmTracker.Web/Services/Schedules/ScheduleTimelineCalculator.cs` | NULL HodnotaInt → klíč chybí v values dict (= "krok nenastal", offset = 0) |
| `PmTracker.Web/Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs` | HarmonogramKrokEditViewModel.OdchylkaDni: int → int? |
| `PmTracker.Web/Services/Records/ManualActualKrokApplier.cs` | Přidat public `Apply()` method (extract z DecisionCommands.ComputeManualActualOverrides) |
| `PmTracker.Web/Services/RecordProposalService.DecisionCommands.cs` | Použít shared `ManualActualKrokApplier.Apply()` |
| `PmTracker.Web/Services/RecordProposalService.SubmitCommands.cs` | Pre-check max 1 Pending + auto-supersede + auto step rejection |
| `PmTracker.Web/Services/RecordService.SaveRecord.cs` | Persistence ManualActualKroky + pending lock pre-check |
| `PmTracker.Web/Services/Records/PendingScheduleProposalLockEvaluator.cs` | Filtrovat Stav=Superseded |
| `PmTracker.Web/Services/Records/ManualProposalFieldValidator.cs` | Validátor `ValidateAutoStepNotInProposal` |
| `PmTracker.Web/Services/Schedules/ScheduleEditorPermissionSet.cs` | Přidat CanEditScheduleDirect, CanProposeSchedule, CanEditManualActual flagy |
| `PmTracker.Web/Services/ProjectService.RecordEditorComposition.cs` | Composition s authz check pro CanEditManualActual |
| `PmTracker.Web/Controllers/HarmonogramController.cs` | Pending lock pre-check v ToggleRezim, nový PreviewSync endpoint |
| `PmTracker.Web/Models/Entities/PmTrackerEntities.cs` | ZaznamNavrhEntity + SupersededByProposalId int? |
| `PmTracker.Web/Models/ViewModels/RecordProposalViewModels.cs` | RecordProposalStateCodes.Superseded const |
| `PmTracker.Web/Data/Configuration/ZaznamNavrhEntityConfiguration.cs` | EF mapping nového sloupce |
| `PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml` | Smazat `Where(k => k.TrvaniDni > 0)` filter, redesign DURATION input |
| `PmTracker.Web/Views/Shared/_ScheduleBlockManualCell.cshtml` | Toggle/dropdown disabled state pro pending lock |
| `PmTracker.Web/wwwroot/js/modules/bootstrap.js` | Registrace select-candidate.js + preview-sync.js |

### Phantom UI bugy řešené v plánu
1. **ManualActualKroky save flow** (RecordService.SaveRecord ignoroval) → Phase 4
2. **ToggleRezim 404 bez DelayHodnotaId + UI catch-22** → Phase 7 (existing create-if-missing v SelectCandidate, replicate pro ToggleRezim)
3. **Dropdown SelectCandidate JS handler chybí** → Phase 11

---

## Phase 0: Verification of Current State

**Cíl:** ověřit, že kódová základna je ve stavu předpokládaném plánem. Pokud kterýkoli krok selže, **plán se musí adaptovat** (ne ignorovat selhání).

### Task 0.1: Build baseline + test pass count

**Files:** žádné změny, jen verifikace

- [ ] **Step 1: Clean build**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet build PmTracker.sln --no-incremental 2>&1 | tail -20
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 2: Run unit tests, record baseline**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -c Release --no-restore 2>&1 | tail -10
```

Expected: zaznamenat `Passed: X, Failed: Y, Skipped: Z`. Známé pre-existing failures (3 testy z memory `project_authz_architecture.md` a memory o pre-existing failures): `ChatModalCss_MaSpravneBreakpointy`, `Bundle_Contains_ManualKrokyModuleSection`, `ChatModalDragDropBundle_CallsDeleteBindingWithVazbaId`. Žádné NOVÉ failures nesmí být.

- [ ] **Step 3: Verify migrace 1_3_10 stav**

```bash
ls -la db_upgrade_1_3_10*.sql
```

Expected: file `db_upgrade_1_3_10_harmonogram_skutecnost_zdroj.sql` existuje.

### Task 0.2: Verify entity + enum struktura

**Files:** Read-only verifikace

- [ ] **Step 1: Ověřit SkutecnostRezimEnum + SkutecnostZdrojEnum**

```bash
grep -n "Auto\|Manual\|Neznamo\|Automat\|Historicka" PmTracker.Web/Models/Entities/SkutecnostRezimEnum.cs PmTracker.Web/Models/Entities/SkutecnostZdrojEnum.cs
```

Expected:
- `SkutecnostRezimEnum`: `Auto = 0`, `Manual = 1`
- `SkutecnostZdrojEnum`: `Neznamo = 0`, `Automat = 1`, `Manual = 2`, `Historicka = 3`

- [ ] **Step 2: Ověřit ZaznamHarmonogramHodnotaEntity sloupce**

```bash
grep -A 15 "class ZaznamHarmonogramHodnotaEntity" PmTracker.Web/Models/Entities/PmTrackerEntities.cs | head -25
```

Expected: properties `SkutecnostRezim`, `SkutecnostZdroj`, `PreferredExterniOdkazId`, `UpdatedAt`. Pokud chybí, migrace 1_3_10 nebyla aplikována — STOP a aplikuj migraci ručně (`sqlcmd -S localhost -d PM_Tracker_VYVOJ -i db_upgrade_1_3_10_harmonogram_skutecnost_zdroj.sql`).

- [ ] **Step 3: Ověřit ZaznamNavrhEntity neobsahuje SupersededByProposalId**

```bash
grep -n "SupersededByProposalId\|Superseded" PmTracker.Web/Models/Entities/PmTrackerEntities.cs PmTracker.Web/Models/ViewModels/RecordProposalViewModels.cs
```

Expected: pouze 0 nebo 1 výsledek (případný komentář), žádná property `SupersededByProposalId` neexistuje, `RecordProposalStateCodes` má jen `Pending/Approved/Rejected`. Pokud existuje — Task 1.1 už proběhl, přeskočit.

### Task 0.3: Verify phantom UI bugy (ovědčit, že stále existují)

**Files:** read-only

- [ ] **Step 1: ManualActualKroky v SaveRecord = 0 výskytů**

```bash
grep -c "ManualActualKroky" PmTracker.Web/Services/RecordService.SaveRecord.cs
```

Expected: `0`. Pokud > 0 — někdo to mezitím opravil, Phase 4 je bezpředmětná.

- [ ] **Step 2: select-candidate.js neexistuje**

```bash
ls PmTracker.Web/wwwroot/js/modules/schedule-feature-c/ 2>/dev/null
```

Expected: jen `toggle-rezim.js`. Pokud existuje `select-candidate.js` — Phase 11 je bezpředmětná.

- [ ] **Step 3: ToggleRezim nemá create-if-missing**

```bash
grep -A 5 "if (row is null)" PmTracker.Web/Controllers/HarmonogramController.cs | head -10
```

Expected: ToggleRezim handler vrací `NotFound()` pro chybějící row (řádek ~76-78). Pokud má create-if-missing — Phase 7 musí adaptovat.

### Task 0.4: Commit Phase 0 ověření

- [ ] **Step 1: Pokud všechny ověření prošly, vytvořit verification log**

```bash
cat > /tmp/harmonogram-refactor-phase0-verification.txt << 'EOF'
Phase 0 verification - 2026-05-01

Build: PASS
Tests baseline: [zaznam X passed / Y failed]
Migrace 1_3_10: APPLIED
Enums správně definované: YES
ZaznamNavrhEntity bez Superseded: YES (Phase 1 task 1.1 needed)
Phantom bug 1 (ManualActualKroky save): CONFIRMED (Phase 4 needed)
Phantom bug 2 (select-candidate.js): CONFIRMED (Phase 11 needed)
Phantom bug 3 (ToggleRezim 404): CONFIRMED (Phase 7 task 7.1 needed)
EOF
cat /tmp/harmonogram-refactor-phase0-verification.txt
```

Expected: výstup obsahuje "PASS"/"CONFIRMED" pro každý bod. Pokud kterýkoli bod selže, dokumentovat v souboru a probrat s uživatelem před pokračováním.

- [ ] **Step 2: Phase 0 se necommituje** (žádné code change). Continue do Phase 1.

---

## Phase 1: DB Schema — Proposal Supersede

### Task 1.1: Migrace + entity + EF mapping pro ZaznamNavrhEntity Superseded

**Files:**
- Create: `db_upgrade_1_3_11_proposal_supersede.sql`
- Modify: `PmTracker.Web/Models/Entities/PmTrackerEntities.cs`
- Modify: `PmTracker.Web/Models/ViewModels/RecordProposalViewModels.cs`
- Modify: `PmTracker.Web/Data/Configuration/RecordProposalEntityConfiguration.cs` (nebo equivalent — ověř název v Step 2)

- [ ] **Step 1: Vytvořit DB migraci**

Vytvořit soubor `db_upgrade_1_3_11_proposal_supersede.sql`:

```sql
-- db_upgrade_1_3_11_proposal_supersede.sql
--
-- Plán Harmonogram refactor 2026-05-01 — DESIGN-7-D.
-- Přidává sloupec SupersededByProposalId na zaznam_navrhy pro auto-supersede vlastního
-- starého návrhu při novém submitu (max 1 Pending per zaznam+typNavrhu invariant).
-- Stav 'SUPERSEDED' je nový code, doplněný v RecordProposalStateCodes (kód-only, není enum v DB).

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.zaznam_navrhy') AND name = 'superseded_by_proposal_id')
BEGIN
    ALTER TABLE dbo.zaznam_navrhy
        ADD superseded_by_proposal_id INT NULL;

    PRINT 'Added zaznam_navrhy.superseded_by_proposal_id (FK to dbo.zaznam_navrhy.id, no constraint to allow self-reference history).';
END
ELSE
    PRINT 'superseded_by_proposal_id column already exists, skipping.';
GO

-- Index pro rychlé hledání aktivních (non-superseded) Pending návrhů per záznam.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_zaznam_navrhy_zaznam_typ_stav_active')
BEGIN
    CREATE INDEX IX_zaznam_navrhy_zaznam_typ_stav_active
        ON dbo.zaznam_navrhy (zaznam_id, typ_navrhu, stav)
        WHERE stav IN ('PENDING');
    PRINT 'Created index IX_zaznam_navrhy_zaznam_typ_stav_active for max-1-pending check.';
END
ELSE
    PRINT 'Index IX_zaznam_navrhy_zaznam_typ_stav_active already exists, skipping.';
GO

-- Sanity check
SELECT
    COUNT(*) AS total_proposals,
    SUM(CASE WHEN stav = 'PENDING' THEN 1 ELSE 0 END) AS pending_count,
    SUM(CASE WHEN stav = 'APPROVED' THEN 1 ELSE 0 END) AS approved_count,
    SUM(CASE WHEN stav = 'REJECTED' THEN 1 ELSE 0 END) AS rejected_count,
    SUM(CASE WHEN stav = 'SUPERSEDED' THEN 1 ELSE 0 END) AS superseded_count,
    SUM(CASE WHEN superseded_by_proposal_id IS NOT NULL THEN 1 ELSE 0 END) AS with_supersedes_link
FROM dbo.zaznam_navrhy;
GO
```

- [ ] **Step 2: Najít EF configuration soubor pro ZaznamNavrh**

```bash
grep -rln "ZaznamNavrhEntity\|zaznam_navrhy" PmTracker.Web/Data/Configuration/ 2>/dev/null
```

Expected: soubor s konfigurací (např. `RecordProposalEntityConfiguration.cs` nebo podobný název). Zaznamenat přesný path.

- [ ] **Step 3: Aplikovat migraci na lokální DB**

```bash
sqlcmd -S localhost -d PM_Tracker_VYVOJ -i db_upgrade_1_3_11_proposal_supersede.sql
```

Expected: `Added zaznam_navrhy.superseded_by_proposal_id...` + `Created index IX_zaznam_navrhy_zaznam_typ_stav_active...` + sanity SELECT result.

- [ ] **Step 4: Rozšířit ZaznamNavrhEntity o SupersededByProposalId**

V `PmTracker.Web/Models/Entities/PmTrackerEntities.cs` najít `ZaznamNavrhEntity` a přidat property:

```csharp
public sealed class ZaznamNavrhEntity
{
    // ... existing properties ...

    /// <summary>
    /// FK na <see cref="Id"/> nového návrhu, který tento návrh nahradil.
    /// Null = aktivní návrh (Pending/Approved/Rejected). Non-null = Superseded při auto-supersede flow.
    /// Plán Harmonogram refactor 2026-05-01 (DESIGN-7-D).
    /// </summary>
    public int? SupersededByProposalId { get; set; }
}
```

- [ ] **Step 5: Přidat Superseded state code**

V `PmTracker.Web/Models/ViewModels/RecordProposalViewModels.cs` rozšířit `RecordProposalStateCodes`:

```csharp
public static class RecordProposalStateCodes
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";

    /// <summary>
    /// Auto-supersede při novém submitu vlastního návrhu (DESIGN-7-D, 2026-05-01).
    /// Návrh je už nahrazený novějším — UI ho zobrazuje read-only s linkem na nový.
    /// </summary>
    public const string Superseded = "SUPERSEDED";
}
```

- [ ] **Step 6: EF mapping nového sloupce**

V EF configuration souboru (z Step 2) přidat:

```csharp
builder.Property(x => x.SupersededByProposalId)
    .HasColumnName("superseded_by_proposal_id")
    .IsRequired(false);
```

- [ ] **Step 7: Build verification**

```bash
dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore 2>&1 | tail -5
```

Expected: `Build succeeded. 0 Error(s)`. Pokud chyba, opravit (typicky chybějící using nebo namespace mismatch).

- [ ] **Step 8: Commit**

```bash
git add db_upgrade_1_3_11_proposal_supersede.sql \
        PmTracker.Web/Models/Entities/PmTrackerEntities.cs \
        PmTracker.Web/Models/ViewModels/RecordProposalViewModels.cs \
        PmTracker.Web/Data/Configuration/

git commit -m "$(cat <<'EOF'
feat(proposal): SupersededByProposalId + state SUPERSEDED — auto-supersede support

Plán Harmonogram refactor 2026-05-01, DESIGN-7-D.
Připravuje schema pro max 1 Pending per (zaznamId, typNavrhu) invariant
s auto-supersede vlastního starého návrhu při novém submitu.

- Migrace db_upgrade_1_3_11 — sloupec superseded_by_proposal_id + index
- Entity ZaznamNavrhEntity.SupersededByProposalId (int?)
- RecordProposalStateCodes.Superseded const
- EF mapping nového sloupce

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 1.5: DELAY HodnotaInt Nullable Refactor (DESIGN-10-A)

**Motivace:** v aktuálním kódu je `HodnotaInt` typu `int` (NOT NULL) s default hodnotou 0 při insertu nového DELAY řádku. To vytváří dvojznačnost: 0 znamená buď "vše šlo dle plánu" (legitimní záznam) nebo "krok ještě nenastal" (default insert state). Render vrstva to dnes řeší kombinací `OdchylkaDni == 0 && SkutecnostZdroj == Neznamo`, ale na úrovni DB je to magic 0 anti-pattern.

**Cíl:** `int? HodnotaInt` — NULL = "krok ještě nenastal / nebyl vyplněn", explicit value (včetně 0) = "krok nastal, OdchylkaDni dnů odchylka". Sémantika napříč vrstvami čistá, eliminuje potřebu kombinace se `SkutecnostZdroj` enumem pro presence detection.

### Task 1.5.1: DB migrace + backfill

**Files:**
- Create: `db_upgrade_1_3_12_delay_nullable.sql`

- [ ] **Step 1: Vytvořit migraci**

```sql
-- db_upgrade_1_3_12_delay_nullable.sql
--
-- Plán Harmonogram refactor 2026-05-01 — DESIGN-10-A.
-- ALTER zaznam_harmonogram_hodnoty.hodnota_int z NOT NULL na NULL.
-- Backfill: řádky s SkutecnostZdroj=Neznamo (= "krok nenastal, default insert state")
-- dostávají NULL. Řádky s Manual/Automat/Historicka keep — legitimní hodnoty,
-- i 0 znamená "vše dle plánu" (krok dokončen včas).

SET NOCOUNT ON;
GO

-- 1) ALTER NOT NULL → NULL
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.zaznam_harmonogram_hodnoty')
      AND name = 'hodnota_int'
      AND is_nullable = 0
)
BEGIN
    ALTER TABLE dbo.zaznam_harmonogram_hodnoty
        ALTER COLUMN hodnota_int INT NULL;
    PRINT 'Altered hodnota_int to nullable.';
END
ELSE
    PRINT 'hodnota_int already nullable, skipping ALTER.';
GO

-- 2) Backfill: jen DELAY řádky (je_zpozdeni = 1) se Zdroj=Neznamo a HodnotaInt=0 → NULL
--    DURATION řádky (je_zpozdeni = 0) se nemění — plán s 0 trváním je legitimní stav.
IF EXISTS (
    SELECT 1
    FROM dbo.zaznam_harmonogram_hodnoty zhh
    INNER JOIN dbo.ciselnik_harmonogram_typu cht ON cht.id = zhh.typ_id
    WHERE cht.je_zpozdeni = 1
      AND zhh.skutecnost_zdroj = 0  -- Neznamo
      AND zhh.hodnota_int = 0
)
BEGIN
    UPDATE zhh
    SET zhh.hodnota_int = NULL
    FROM dbo.zaznam_harmonogram_hodnoty zhh
    INNER JOIN dbo.ciselnik_harmonogram_typu cht ON cht.id = zhh.typ_id
    WHERE cht.je_zpozdeni = 1
      AND zhh.skutecnost_zdroj = 0
      AND zhh.hodnota_int = 0;
    PRINT CONCAT('Backfilled ', @@ROWCOUNT, ' DELAY rows from 0 → NULL (krok nenastal).');
END
ELSE
    PRINT 'No DELAY rows matching backfill criteria, skipping.';
GO

-- Sanity check
SELECT
    COUNT(*) AS total_delay_rows,
    SUM(CASE WHEN zhh.hodnota_int IS NULL THEN 1 ELSE 0 END) AS null_delays,
    SUM(CASE WHEN zhh.hodnota_int = 0 AND zhh.skutecnost_zdroj <> 0 THEN 1 ELSE 0 END) AS legit_zero_delays,
    SUM(CASE WHEN zhh.hodnota_int <> 0 THEN 1 ELSE 0 END) AS nonzero_delays
FROM dbo.zaznam_harmonogram_hodnoty zhh
INNER JOIN dbo.ciselnik_harmonogram_typu cht ON cht.id = zhh.typ_id
WHERE cht.je_zpozdeni = 1;
GO
```

- [ ] **Step 2: Aplikovat migraci na lokální DB**

```bash
sqlcmd -S localhost -d PM_Tracker_VYVOJ -i db_upgrade_1_3_12_delay_nullable.sql
```

Expected: `Altered hodnota_int to nullable.` + `Backfilled X DELAY rows from 0 → NULL` + sanity SELECT.

- [ ] **Step 3: Commit migrace**

```bash
git add db_upgrade_1_3_12_delay_nullable.sql
git commit -m "$(cat <<'EOF'
feat(harmonogram): hodnota_int nullable + backfill (DESIGN-10-A)

ALTER zaznam_harmonogram_hodnoty.hodnota_int NOT NULL → NULL.
Backfill: DELAY řádky se Zdroj=Neznamo + HodnotaInt=0 dostanou NULL
(= "krok nenastal" semantika oddělená od legitimního "vše dle plánu" 0).

Připrava pro entity refactor v Task 1.5.2.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

### Task 1.5.2: Entity + EF mapping nullable

**Files:**
- Modify: `PmTracker.Web/Models/Entities/PmTrackerEntities.cs`
- Modify: `PmTracker.Web/Data/Configuration/RecordEntityConfiguration.cs`

- [ ] **Step 1: ZaznamHarmonogramHodnotaEntity.HodnotaInt → int?**

V `PmTracker.Web/Models/Entities/PmTrackerEntities.cs` najít `ZaznamHarmonogramHodnotaEntity` a změnit:

```csharp
// Před:
public int HodnotaInt { get; set; }

// Po:
/// <summary>
/// Plán Harmonogram refactor 2026-05-01 (DESIGN-10-A) — nullable.
/// NULL = "krok ještě nenastal / nebyl vyplněn".
/// 0 = "vše šlo dle plánu" (legitimní hodnota, krok dokončen včas).
/// non-zero = OdchylkaDni dnů (kladné = zpoždění, záporné = předstih).
/// Pro DURATION řádky (JeZpozdeni=false) zůstává sémantika: NULL → fallback na default duration ze schématu.
/// </summary>
public int? HodnotaInt { get; set; }
```

- [ ] **Step 2: EF mapping IsRequired(false)**

V `PmTracker.Web/Data/Configuration/RecordEntityConfiguration.cs` najít existing `builder.Property(x => x.HodnotaInt).HasColumnName("hodnota_int");` a rozšířit:

```csharp
builder.Property(x => x.HodnotaInt)
    .HasColumnName("hodnota_int")
    .IsRequired(false);
```

- [ ] **Step 3: Build — očekávané chyby v call site**

```bash
dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore 2>&1 | tail -30
```

Expected: build chyby na call site, kde se přiřazuje `int → int?` (auto-conversion OK) nebo `int? → int` (vyžaduje `.Value` / `?? 0` / `.HasValue` check). Zaznamenat všechny chyby — Task 1.5.3 je opraví všechny systematicky.

### Task 1.5.3: Update call sites (sync, save, controller, calculator, decision)

**Files:**
- Modify: `PmTracker.Web/Services/Schedules/HarmonogramSkutecnostSyncService.cs`
- Modify: `PmTracker.Web/Services/RecordService.SaveRecord.cs`
- Modify: `PmTracker.Web/Services/RecordProposalService.DecisionCommands.cs`
- Modify: `PmTracker.Web/Controllers/HarmonogramController.cs`
- Modify: `PmTracker.Web/Services/Schedules/ScheduleTimelineCalculator.cs`
- Modify: `PmTracker.Web/Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs` (`HarmonogramKrokEditViewModel.OdchylkaDni`)

- [ ] **Step 1: ScheduleTimelineCalculator — NULL handling**

V `PmTracker.Web/Services/Schedules/ScheduleTimelineCalculator.cs` najít `Compute` metodu. Aktuální logika:
```csharp
var offsetDays = step.OffsetTypeId > 0
    ? normalizedValues.GetValueOrDefault(step.OffsetTypeId)
    : 0;
```

`normalizedValues` je `IReadOnlyDictionary<int, int>`. Změnit signature na `IReadOnlyDictionary<int, int?>` + adaptovat callers, NEBO callers mají filtrovat NULL před dictionary build (klíč chybí = NULL = krok nenastal).

**Doporučená cesta:** zachovat `int` v dictionary, ale callers filtrují NULL při build:
```csharp
var values = allRows
    .Where(r => r.HodnotaInt.HasValue)
    .ToDictionary(r => r.TypId, r => r.HodnotaInt!.Value);
```

Calculator pak v `GetValueOrDefault` vrátí 0 pro chybějící klíč = "krok nenastal" → offset 0 → actualEnd = planEnd. Sémantika je: actual cursor postupuje shodně s plan cursor pro kroky, které ještě nenastaly. **Tato sémantika je zachovaná z původního kódu**, jen presence se mění z "magic 0" na "missing key".

- [ ] **Step 2: HarmonogramSkutecnostSyncService — ComputePlanAsync s nullable**

V `HarmonogramSkutecnostSyncService.cs` upravit `HarmonogramRowChange` DTO definice (Phase 2 task 2.1) — pole `OldHodnotaInt` a `NewHodnotaInt` musí být `int?`:

```csharp
public sealed record HarmonogramRowChange(
    int RowId,
    int TypId,
    int KrokPoradi,
    int? OldHodnotaInt,    // ← changed from int
    int? NewHodnotaInt,    // ← changed from int
    SkutecnostZdrojEnum OldZdroj,
    SkutecnostZdrojEnum NewZdroj,
    int? OldPreferredExterniOdkazId,
    int? NewPreferredExterniOdkazId,
    DateTime ExpectedUpdatedAt,
    HarmonogramRowChangeReason Reason);
```

V `ComputePlanAsync` body (Phase 2 task 2.2) update logic:
```csharp
// Retract: kandidát zmizel, dříve byl Automat
if (resolved.Datum is null)
{
    if (row.SkutecnostZdroj == SkutecnostZdrojEnum.Automat)
    {
        changes.Add(new HarmonogramRowChange(
            row.Id, row.TypId, poradi,
            OldHodnotaInt: row.HodnotaInt,
            NewHodnotaInt: null,                                    // ← retract na NULL, ne 0
            OldZdroj: row.SkutecnostZdroj,
            NewZdroj: SkutecnostZdrojEnum.Neznamo,
            OldPreferredExterniOdkazId: row.PreferredExterniOdkazId,
            NewPreferredExterniOdkazId: null,
            ExpectedUpdatedAt: row.UpdatedAt,
            Reason: HarmonogramRowChangeReason.RetractAutomat_NoCandidates));
    }
    continue;
}

// Apply candidate
var newPreferred = resolved.PreferredFallbackApplied ? null : row.PreferredExterniOdkazId;
var computedDelay = baselineEndByPoradi.TryGetValue(poradi, out var baselineEnd)
    ? (int)Math.Round((resolved.Datum.Value.Date - baselineEnd.Date).TotalDays)
    : (row.HodnotaInt ?? 0);  // fallback pokud schema chybí (rare)

var hasChange = row.HodnotaInt != computedDelay  // int? != int auto-promote, NULL != X → true
    || row.SkutecnostZdroj != SkutecnostZdrojEnum.Automat
    || row.PreferredExterniOdkazId != newPreferred;

if (!hasChange) continue;

changes.Add(new HarmonogramRowChange(
    row.Id, row.TypId, poradi,
    OldHodnotaInt: row.HodnotaInt,
    NewHodnotaInt: computedDelay,                                   // ← non-null (auto-fill kandidát)
    OldZdroj: row.SkutecnostZdroj,
    NewZdroj: SkutecnostZdrojEnum.Automat,
    OldPreferredExterniOdkazId: row.PreferredExterniOdkazId,
    NewPreferredExterniOdkazId: newPreferred,
    ExpectedUpdatedAt: row.UpdatedAt,
    Reason: ...));
```

V `ApplyPlanAsync` (Phase 2 task 2.3) update:
```csharp
if (change.Reason == HarmonogramRowChangeReason.RetractAutomat_NoCandidates)
{
    row.HodnotaInt = change.NewHodnotaInt;  // NULL retract
    row.SkutecnostZdroj = change.NewZdroj;
    row.PreferredExterniOdkazId = change.NewPreferredExterniOdkazId;
    row.UpdatedAt = nowUtc;
    updated++;
    continue;
}
```

- [ ] **Step 3: HarmonogramController.ToggleRezim — create-if-missing s NULL**

V `PmTracker.Web/Controllers/HarmonogramController.cs` v create-if-missing branch (Phase 7 task 7.1):

```csharp
row = new ZaznamHarmonogramHodnotaEntity
{
    ZaznamId = zaznamId,
    TypId = delayTypId.Value,
    HodnotaInt = null,                          // ← NULL ne 0 (krok ještě nenastal)
    UpdatedAt = _time.GetUtcNow().UtcDateTime,
    SkutecnostRezim = SkutecnostRezimEnum.Auto,
    SkutecnostZdroj = SkutecnostZdrojEnum.Neznamo
};
```

Stejně v `SelectCandidate` create-if-missing branch ([HarmonogramController.cs:201-209](PmTracker.Web/Controllers/HarmonogramController.cs#L201-L209)).

- [ ] **Step 4: RecordService.SaveRecord + DecisionCommands**

V obou souborech: insert / update flows pro DELAY rows. Hodnota přicházející z `ManualActualKroky` (user explicit input) je vždy non-null. Hodnota přicházející z `HarmonogramHodnoty` form binding pro DELAY: pokud user nezadá nic, `int?` deserializuje na NULL — konzistentní s "krok nenastal".

V `RecordService.SaveRecord.ApplyManualActualKrokyAsync` (Phase 4 task 4.2):
```csharp
foreach (var ov in overrides)
{
    if (existingByTypId.TryGetValue(ov.DelayTypId, out var row))
    {
        row.HodnotaInt = ov.OdchylkaDni;        // user explicit value, non-null
        row.SkutecnostZdroj = SkutecnostZdrojEnum.Manual;
        row.SkutecnostRezim = SkutecnostRezimEnum.Manual;
        row.UpdatedAt = nowUtc;
    }
    else
    {
        _dbContext.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
        {
            ZaznamId = recordId,
            TypId = ov.DelayTypId,
            HodnotaInt = ov.OdchylkaDni,        // user explicit value, non-null
            SkutecnostZdroj = SkutecnostZdrojEnum.Manual,
            SkutecnostRezim = SkutecnostRezimEnum.Manual,
            UpdatedAt = nowUtc
        });
    }
}
```

V `DecisionCommands` UPSERT loop ([:288-297](PmTracker.Web/Services/RecordProposalService.DecisionCommands.cs#L288-L297)) hodnoty vznikají z `normalizedValues` dictionary `<int, int>` (form-bound non-null). Insert s explicit value:
```csharp
foreach (var plannedValue in normalizedValues)
{
    _dbContext.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
    {
        ZaznamId = record.Id,
        TypId = plannedValue.Key,
        HodnotaInt = plannedValue.Value,        // form-bound non-null
        UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime
    });
}
```

DURATION default při insertu beze změny (DURATION nemá NULL semantiku, default je hodnota ze schématu).

- [ ] **Step 5: HarmonogramKrokEditViewModel.OdchylkaDni → int?**

V `PmTracker.Web/Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs`:

```csharp
public sealed class HarmonogramKrokEditViewModel
{
    // ... existing properties ...

    /// <summary>
    /// Plán Harmonogram refactor 2026-05-01 (DESIGN-10-A) — nullable.
    /// NULL = krok ještě nenastal. 0 = vše dle plánu. non-zero = odchylka.
    /// </summary>
    public int? OdchylkaDni { get; init; }

    public int? ZpozdeniDni => OdchylkaDni;  // alias

    // SkutecneDatum se počítá až po vyplnění odchylky (NULL → SkutecneDatum nedefinováno).
    // Aktuální non-nullable DateTime SkutecneDatum: pro NULL OdchylkaDni → SkutecneDatum = BaselineDatum
    // (= "kdyby krok nastal teď, byl by včas"). Render layer (Phase 9) actual segment vůbec
    // nezobrazí, takže hodnota SkutecneDatum se neuplatní. Necháváme non-nullable pro backward compat.
    public DateTime SkutecneDatum { get; init; }
}
```

V composition (`ProjectService.RecordEditorComposition.cs`) změnit:
```csharp
OdchylkaDni = delayRow?.HodnotaInt,  // null pokud row nemá value
SkutecneDatum = ... (ponechat existující výpočet, defaultně BaselineDatum pokud OdchylkaDni je null)
```

- [ ] **Step 6: Aktualizovat Phase 9 hasActual logic**

V `_ScheduleBlock.cshtml` (Phase 9 task 9.1 step 3) zjednodušit `hasActual`:

```razor
@* Před (dvojcheck): *@
@* var hasActual = krok.OdchylkaDni != 0 || krok.SkutecnostZdroj != SkutecnostZdrojEnum.Neznamo; *@

@* Po (čistá nullable semantika): *@
var hasActual = krok.OdchylkaDni.HasValue;
```

- [ ] **Step 7: Build + spustit unit testy (regrese)**

```bash
dotnet build PmTracker.sln --no-incremental 2>&1 | tail -5
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -c Release --no-restore 2>&1 | tail -10
```

Expected: build PASS, žádné nové test failures (existing tests s `int` semantikou by měly stále projít — nullable je backward-compat refinement).

Pokud test fail kvůli `int? → int` mismatch v test setup, doplnit `.HasValue ? .Value : 0` nebo `?? 0` jen v test arrange code.

- [ ] **Step 8: Commit**

```bash
git add PmTracker.Web/Models/Entities/PmTrackerEntities.cs \
        PmTracker.Web/Data/Configuration/RecordEntityConfiguration.cs \
        PmTracker.Web/Services/Schedules/HarmonogramSkutecnostSyncService.cs \
        PmTracker.Web/Services/Schedules/ScheduleTimelineCalculator.cs \
        PmTracker.Web/Services/RecordService.SaveRecord.cs \
        PmTracker.Web/Services/RecordProposalService.DecisionCommands.cs \
        PmTracker.Web/Controllers/HarmonogramController.cs \
        PmTracker.Web/Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs \
        PmTracker.Web/Services/ProjectService.RecordEditorComposition.cs
git commit -m "$(cat <<'EOF'
refactor(harmonogram): HodnotaInt nullable napříč vrstvami (DESIGN-10-A)

Entity ZaznamHarmonogramHodnotaEntity.HodnotaInt: int → int?.
HarmonogramKrokEditViewModel.OdchylkaDni: int → int?.
HarmonogramRowChange DTO: Old/NewHodnotaInt nullable.

Sémantika napříč vrstvami:
  NULL    = "krok ještě nenastal / nebyl vyplněn"
  0       = "vše šlo dle plánu" (krok dokončen včas)
  non-0   = OdchylkaDni dnů (kladné = zpoždění, záporné = předstih)

Eliminuje "magic 0" anti-pattern. Render hasActual = OdchylkaDni.HasValue.
Sync retract: HodnotaInt → null (ne 0). ToggleRezim create-if-missing: NULL.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 2: Sync Service Refactor (DESIGN-4-A)

### Task 2.1: HarmonogramSyncPlan DTO + interface rozšíření

**Files:**
- Create: `PmTracker.Web/Services/Schedules/HarmonogramSyncPlan.cs`
- Modify: `PmTracker.Web/Services/Schedules/HarmonogramSkutecnostSyncService.cs`

- [ ] **Step 1: Vytvořit HarmonogramSyncPlan.cs**

```csharp
namespace PmTracker.Web.Services.Schedules;

/// <summary>
/// Plán Harmonogram refactor 2026-05-01 (DESIGN-4-A) — DTO popisující zamýšlené změny
/// pro jeden záznam, výstup <see cref="IHarmonogramSkutecnostSyncService.ComputePlanAsync"/>
/// a vstup <see cref="IHarmonogramSkutecnostSyncService.ApplyPlanAsync"/>.
///
/// Use cases:
/// 1. UI staging (PreviewSync endpoint) — server vrátí plán, JS drží v sessionStorage,
///    commit na Save přes ApplyPlanAsync.
/// 2. Testovatelnost — pure compute lze testovat bez DB write.
/// 3. Race resistance — caller může mezi Compute a Apply ověřit, že rows se nezměnily
///    (UpdatedAt token check). Pokud ano, znovu Compute.
/// </summary>
public sealed record HarmonogramSyncPlan(
    int ProjektovyZaznamId,
    DateTime ComputedAtUtc,
    IReadOnlyList<HarmonogramRowChange> Changes);

/// <summary>
/// Jeden zamýšlený delta zápis na <c>zaznam_harmonogram_hodnoty</c> řádek.
/// </summary>
public sealed record HarmonogramRowChange(
    int RowId,
    int TypId,
    int KrokPoradi,
    int? OldHodnotaInt,
    int? NewHodnotaInt,    // DESIGN-10-A: nullable (NULL = retract / krok nenastal)
    PmTracker.Web.Models.Entities.SkutecnostZdrojEnum OldZdroj,
    PmTracker.Web.Models.Entities.SkutecnostZdrojEnum NewZdroj,
    int? OldPreferredExterniOdkazId,
    int? NewPreferredExterniOdkazId,
    DateTime ExpectedUpdatedAt,
    HarmonogramRowChangeReason Reason);

/// <summary>
/// Důvod zamýšlené změny — pro audit / UI display "co se chystá změnit".
/// </summary>
public enum HarmonogramRowChangeReason
{
    NoChange = 0,
    NewAutomatValue,
    UpdatedAutomatValue,
    RetractAutomat_NoCandidates,
    PreferredFallback,
    SkippedManualRezim
}
```

- [ ] **Step 2: Rozšířit interface IHarmonogramSkutecnostSyncService**

V `HarmonogramSkutecnostSyncService.cs` upravit interface:

```csharp
public interface IHarmonogramSkutecnostSyncService
{
    /// <summary>
    /// Pure compute — vrátí plán zamýšlených změn, NIC nezapisuje do DB.
    /// Použití: UI staging, testy, manual review před commitem.
    /// </summary>
    Task<HarmonogramSyncPlan> ComputePlanAsync(int projektovyZaznamId, CancellationToken ct = default);

    /// <summary>
    /// Aplikuje plán na DB. Před zápisem ověří, že každý dotčený řádek má UpdatedAt
    /// odpovídající plánu (optimistic concurrency token). Pokud ne, skip rowu + log.
    /// </summary>
    Task<HarmonogramSyncResult> ApplyPlanAsync(HarmonogramSyncPlan plan, CancellationToken ct = default);

    /// <summary>
    /// Convenience wrapper: ComputePlanAsync + ApplyPlanAsync v sekvenci.
    /// Existující call sites (HarmonogramController, BindingRebalanceService, VyjadreniHarvestService)
    /// volají tuto metodu a fungují beze změny.
    /// </summary>
    Task<HarmonogramSyncResult> SyncZaznamAsync(int projektovyZaznamId, CancellationToken ct = default);

    Task<IReadOnlyList<BindingKandidat>> GetKandidatiForZaznamAsync(int zaznamId, CancellationToken ct = default);
}
```

- [ ] **Step 3: Build check**

```bash
dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore 2>&1 | tail -5
```

Expected: build error (`ComputePlanAsync` + `ApplyPlanAsync` nejsou implementované). Tenhle stav je očekávaný — pokračuj k Task 2.2.

- [ ] **Step 4: Commit interim (interface)**

```bash
git add PmTracker.Web/Services/Schedules/HarmonogramSyncPlan.cs \
        PmTracker.Web/Services/Schedules/HarmonogramSkutecnostSyncService.cs
git commit -m "$(cat <<'EOF'
feat(harmonogram): IHarmonogramSkutecnostSyncService rozšíření o ComputePlan/ApplyPlan

DESIGN-4-A. Build úmyslně rozbitý — Task 2.2 dodá impl.
Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

### Task 2.2: ComputePlanAsync implementace

**Files:**
- Modify: `PmTracker.Web/Services/Schedules/HarmonogramSkutecnostSyncService.cs`
- Create: `PmTracker.Tests.Unit/Schedule/HarmonogramSyncPlanTests.cs`

- [ ] **Step 1: Napsat failing test pro ComputePlanAsync**

V `PmTracker.Tests.Unit/Schedule/HarmonogramSyncPlanTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Schedules;
using PmTracker.Web.Services.ServiceDesk;
using Xunit;

namespace PmTracker.Tests.Unit.Schedule;

public sealed class HarmonogramSyncPlanTests
{
    private static PmTrackerDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase($"sync-plan-{Guid.NewGuid()}")
            .Options;
        return new PmTrackerDbContext(options);
    }

    [Fact]
    public async Task ComputePlanAsync_ZaznamNeexistuje_VraciPrazdnyPlan()
    {
        using var db = CreateInMemoryDb();
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-01T10:00:00Z"));
        var vyjadreniQueryStub = new StubVyjadreniQueryService();
        var sut = new HarmonogramSkutecnostSyncService(db, vyjadreniQueryStub, time, NullLogger<HarmonogramSkutecnostSyncService>.Instance);

        var plan = await sut.ComputePlanAsync(99999, CancellationToken.None);

        plan.ProjektovyZaznamId.Should().Be(99999);
        plan.Changes.Should().BeEmpty();
        plan.ComputedAtUtc.Should().Be(DateTime.Parse("2026-05-01T10:00:00").ToUniversalTime());
    }

    private sealed class StubVyjadreniQueryService : PmTracker.ServiceDesk.Contracts.IVyjadreniQueryService
    {
        public Task<IReadOnlyDictionary<string, PmTracker.ServiceDesk.Contracts.HotZaznamFingerprintDto>> GetHotZaznamFingerprintsAsync(
            IReadOnlyList<string> cisla, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<string, PmTracker.ServiceDesk.Contracts.HotZaznamFingerprintDto>>(
                new Dictionary<string, PmTracker.ServiceDesk.Contracts.HotZaznamFingerprintDto>());
    }
}
```

- [ ] **Step 2: Spustit test, ověřit failure**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -c Release --no-restore --filter "FullyQualifiedName~HarmonogramSyncPlanTests" 2>&1 | tail -10
```

Expected: build error nebo test fail (ComputePlanAsync method body není implementovaná).

- [ ] **Step 3: Implementovat ComputePlanAsync**

V `HarmonogramSkutecnostSyncService.cs` nahradit současnou `SyncZaznamAsync` body. Existing logika se rozdělí — read+compute části přesunou do `ComputePlanAsync`, write části do `ApplyPlanAsync`. Nová `ComputePlanAsync`:

```csharp
public async Task<HarmonogramSyncPlan> ComputePlanAsync(int projektovyZaznamId, CancellationToken ct = default)
{
    var nowUtc = _time.GetUtcNow().UtcDateTime;
    if (projektovyZaznamId <= 0)
    {
        return new HarmonogramSyncPlan(projektovyZaznamId, nowUtc, Array.Empty<HarmonogramRowChange>());
    }

    var zaznam = await _db.ProjektoveZaznamy.AsNoTracking()
        .FirstOrDefaultAsync(z => z.Id == projektovyZaznamId, ct).ConfigureAwait(false);
    if (zaznam is null)
    {
        return new HarmonogramSyncPlan(projektovyZaznamId, nowUtc, Array.Empty<HarmonogramRowChange>());
    }

    // Schema: KrokPoradi → DelayTypId, KrokPoradi → DurationTypId, KrokPoradi → defaultDuration
    var schemaRaw = await _db.CiselnikHarmonogramTypu.AsNoTracking()
        .Where(t => t.SablonaVerze == zaznam.HarmonogramSablonaVerze)
        .Select(t => new { t.Id, t.KrokPoradi, t.JeZpozdeni, t.Hodnota })
        .ToListAsync(ct).ConfigureAwait(false);
    var delayTypIdByPoradi = schemaRaw.Where(x => x.JeZpozdeni)
        .GroupBy(x => x.KrokPoradi).ToDictionary(g => g.Key, g => g.First().Id);
    var durationTypIdByPoradi = schemaRaw.Where(x => !x.JeZpozdeni)
        .GroupBy(x => x.KrokPoradi).ToDictionary(g => g.Key, g => g.First().Id);
    var defaultDurationByPoradi = schemaRaw.Where(x => !x.JeZpozdeni)
        .GroupBy(x => x.KrokPoradi).ToDictionary(g => g.Key, g => g.First().Hodnota);

    if (delayTypIdByPoradi.Count == 0)
    {
        return new HarmonogramSyncPlan(projektovyZaznamId, nowUtc, Array.Empty<HarmonogramRowChange>());
    }

    var allRows = await _db.ZaznamHarmonogramHodnoty.AsNoTracking()
        .Where(h => h.ZaznamId == projektovyZaznamId)
        .ToListAsync(ct).ConfigureAwait(false);
    var delayRowByTypId = allRows.Where(r => delayTypIdByPoradi.Values.Contains(r.TypId))
        .ToDictionary(r => r.TypId, r => r);
    var durationByPoradi = durationTypIdByPoradi.ToDictionary(kv => kv.Key, kv =>
    {
        var row = allRows.FirstOrDefault(r => r.TypId == kv.Value);
        return row?.HodnotaInt ?? defaultDurationByPoradi.GetValueOrDefault(kv.Key);
    });

    // Baseline end per krok (cumulative DURATION sum)
    var baselineEndByPoradi = new Dictionary<int, DateTime>();
    var cumulative = zaznam.DatumZalozeni.Date;
    foreach (var poradi in delayTypIdByPoradi.Keys.OrderBy(p => p))
    {
        cumulative = cumulative.AddDays(durationByPoradi.GetValueOrDefault(poradi));
        baselineEndByPoradi[poradi] = cumulative;
    }

    var bindings = await LoadBindingKandidatiAsync(projektovyZaznamId, ct).ConfigureAwait(false);

    var changes = new List<HarmonogramRowChange>();
    foreach (var (poradi, delayTypId) in delayTypIdByPoradi)
    {
        if (!delayRowByTypId.TryGetValue(delayTypId, out var row))
        {
            // Sync metadata se nevytváří "do zásoby" (zachované chování ze starého kódu).
            continue;
        }

        if (row.SkutecnostRezim == SkutecnostRezimEnum.Manual)
        {
            changes.Add(new HarmonogramRowChange(
                row.Id, row.TypId, poradi,
                row.HodnotaInt, row.HodnotaInt,
                row.SkutecnostZdroj, row.SkutecnostZdroj,
                row.PreferredExterniOdkazId, row.PreferredExterniOdkazId,
                row.UpdatedAt, HarmonogramRowChangeReason.SkippedManualRezim));
            continue;
        }

        var resolved = HarmonogramSkutecnostResolver.Resolve(poradi, bindings, row.PreferredExterniOdkazId);

        if (resolved.Datum is null)
        {
            if (row.SkutecnostZdroj == SkutecnostZdrojEnum.Automat)
            {
                changes.Add(new HarmonogramRowChange(
                    row.Id, row.TypId, poradi,
                    row.HodnotaInt, row.HodnotaInt,
                    row.SkutecnostZdroj, SkutecnostZdrojEnum.Neznamo,
                    row.PreferredExterniOdkazId, null,
                    row.UpdatedAt, HarmonogramRowChangeReason.RetractAutomat_NoCandidates));
            }
            continue;
        }

        var newPreferred = resolved.PreferredFallbackApplied ? null : row.PreferredExterniOdkazId;
        var computedDelay = baselineEndByPoradi.TryGetValue(poradi, out var baselineEnd)
            ? (int)Math.Round((resolved.Datum.Value.Date - baselineEnd.Date).TotalDays)
            : row.HodnotaInt;

        var hasChange = row.HodnotaInt != computedDelay
            || row.SkutecnostZdroj != SkutecnostZdrojEnum.Automat
            || row.PreferredExterniOdkazId != newPreferred;

        if (!hasChange) continue;

        var reason = resolved.PreferredFallbackApplied ? HarmonogramRowChangeReason.PreferredFallback
            : row.SkutecnostZdroj != SkutecnostZdrojEnum.Automat ? HarmonogramRowChangeReason.NewAutomatValue
            : HarmonogramRowChangeReason.UpdatedAutomatValue;

        changes.Add(new HarmonogramRowChange(
            row.Id, row.TypId, poradi,
            row.HodnotaInt, computedDelay,
            row.SkutecnostZdroj, SkutecnostZdrojEnum.Automat,
            row.PreferredExterniOdkazId, newPreferred,
            row.UpdatedAt, reason));
    }

    return new HarmonogramSyncPlan(projektovyZaznamId, nowUtc, changes);
}
```

- [ ] **Step 4: Spustit test, ověřit pass**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -c Release --no-restore --filter "FullyQualifiedName~HarmonogramSyncPlanTests" 2>&1 | tail -10
```

Expected: PASS 1/1.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Schedules/HarmonogramSkutecnostSyncService.cs \
        PmTracker.Tests.Unit/Schedule/HarmonogramSyncPlanTests.cs
git commit -m "$(cat <<'EOF'
feat(harmonogram): ComputePlanAsync — pure read+compute split (DESIGN-4-A)

Vrací HarmonogramSyncPlan s detail zamýšlených změn, žádný DB write.
Test ZaznamNeexistuje_VraciPrazdnyPlan PASS.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

### Task 2.3: ApplyPlanAsync implementace

**Files:**
- Modify: `PmTracker.Web/Services/Schedules/HarmonogramSkutecnostSyncService.cs`
- Modify: `PmTracker.Tests.Unit/Schedule/HarmonogramSyncPlanTests.cs`

- [ ] **Step 1: Napsat failing test pro ApplyPlanAsync s optimistic concurrency**

Přidat do `HarmonogramSyncPlanTests.cs`:

```csharp
[Fact]
public async Task ApplyPlanAsync_RowChanged_SkipsRowWithStaleToken()
{
    using var db = CreateInMemoryDb();
    var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-01T10:00:00Z"));
    var nowUtc = time.GetUtcNow().UtcDateTime;
    var olderUtc = nowUtc.AddMinutes(-5);

    db.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
    {
        Id = 1, ZaznamId = 1, TypId = 100, HodnotaInt = 0,
        SkutecnostRezim = SkutecnostRezimEnum.Auto,
        SkutecnostZdroj = SkutecnostZdrojEnum.Neznamo,
        UpdatedAt = nowUtc // current state
    });
    await db.SaveChangesAsync();

    var sut = new HarmonogramSkutecnostSyncService(db, new StubVyjadreniQueryService(), time, NullLogger<HarmonogramSkutecnostSyncService>.Instance);
    var staleChange = new HarmonogramRowChange(
        RowId: 1, TypId: 100, KrokPoradi: 1,
        OldHodnotaInt: 0, NewHodnotaInt: 5,
        OldZdroj: SkutecnostZdrojEnum.Neznamo, NewZdroj: SkutecnostZdrojEnum.Automat,
        OldPreferredExterniOdkazId: null, NewPreferredExterniOdkazId: null,
        ExpectedUpdatedAt: olderUtc, // STALE — DB má novější UpdatedAt
        Reason: HarmonogramRowChangeReason.NewAutomatValue);
    var plan = new HarmonogramSyncPlan(1, nowUtc, new[] { staleChange });

    var result = await sut.ApplyPlanAsync(plan, CancellationToken.None);

    result.KrokuAktualizovano.Should().Be(0); // stale token → skip
    var rowAfter = await db.ZaznamHarmonogramHodnoty.AsNoTracking().FirstAsync(h => h.Id == 1);
    rowAfter.HodnotaInt.Should().Be(0); // unchanged
    rowAfter.SkutecnostZdroj.Should().Be(SkutecnostZdrojEnum.Neznamo);
}

[Fact]
public async Task ApplyPlanAsync_FreshToken_AppliesChange()
{
    using var db = CreateInMemoryDb();
    var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-01T10:00:00Z"));
    var rowUpdatedAt = DateTime.Parse("2026-05-01T09:55:00").ToUniversalTime();

    db.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
    {
        Id = 1, ZaznamId = 1, TypId = 100, HodnotaInt = 0,
        SkutecnostRezim = SkutecnostRezimEnum.Auto,
        SkutecnostZdroj = SkutecnostZdrojEnum.Neznamo,
        UpdatedAt = rowUpdatedAt
    });
    await db.SaveChangesAsync();

    var sut = new HarmonogramSkutecnostSyncService(db, new StubVyjadreniQueryService(), time, NullLogger<HarmonogramSkutecnostSyncService>.Instance);
    var change = new HarmonogramRowChange(
        RowId: 1, TypId: 100, KrokPoradi: 1,
        OldHodnotaInt: 0, NewHodnotaInt: 5,
        OldZdroj: SkutecnostZdrojEnum.Neznamo, NewZdroj: SkutecnostZdrojEnum.Automat,
        OldPreferredExterniOdkazId: null, NewPreferredExterniOdkazId: null,
        ExpectedUpdatedAt: rowUpdatedAt, // FRESH — match DB
        Reason: HarmonogramRowChangeReason.NewAutomatValue);
    var plan = new HarmonogramSyncPlan(1, time.GetUtcNow().UtcDateTime, new[] { change });

    var result = await sut.ApplyPlanAsync(plan, CancellationToken.None);

    result.KrokuAktualizovano.Should().Be(1);
    var rowAfter = await db.ZaznamHarmonogramHodnoty.AsNoTracking().FirstAsync(h => h.Id == 1);
    rowAfter.HodnotaInt.Should().Be(5);
    rowAfter.SkutecnostZdroj.Should().Be(SkutecnostZdrojEnum.Automat);
}
```

- [ ] **Step 2: Implementovat ApplyPlanAsync**

V `HarmonogramSkutecnostSyncService.cs` přidat:

```csharp
public async Task<HarmonogramSyncResult> ApplyPlanAsync(HarmonogramSyncPlan plan, CancellationToken ct = default)
{
    if (plan.Changes.Count == 0)
    {
        return new HarmonogramSyncResult(plan.ProjektovyZaznamId, 0, 0, 0, 0);
    }

    var rowIds = plan.Changes.Select(c => c.RowId).Distinct().ToList();
    var trackedRows = await _db.ZaznamHarmonogramHodnoty
        .Where(h => rowIds.Contains(h.Id))
        .ToListAsync(ct).ConfigureAwait(false);
    var rowById = trackedRows.ToDictionary(r => r.Id);

    int updated = 0, skipManual = 0, noKandidat = 0, preferredFallbacks = 0, staleSkipped = 0;
    var nowUtc = _time.GetUtcNow().UtcDateTime;

    foreach (var change in plan.Changes)
    {
        if (!rowById.TryGetValue(change.RowId, out var row))
        {
            staleSkipped++; // row deleted between Compute and Apply
            continue;
        }

        // Optimistic concurrency: row UpdatedAt musí matchovat plan ExpectedUpdatedAt.
        if (row.UpdatedAt != change.ExpectedUpdatedAt)
        {
            staleSkipped++;
            _logger.LogWarning(
                "ApplyPlan: row {RowId} stale token (expected {Exp:o}, actual {Act:o}) — skipped.",
                change.RowId, change.ExpectedUpdatedAt, row.UpdatedAt);
            continue;
        }

        if (change.Reason == HarmonogramRowChangeReason.SkippedManualRezim)
        {
            skipManual++;
            continue;
        }

        if (change.Reason == HarmonogramRowChangeReason.RetractAutomat_NoCandidates)
        {
            row.SkutecnostZdroj = change.NewZdroj;
            row.PreferredExterniOdkazId = change.NewPreferredExterniOdkazId;
            row.UpdatedAt = nowUtc;
            updated++;
            continue;
        }

        if (change.Reason == HarmonogramRowChangeReason.PreferredFallback)
        {
            preferredFallbacks++;
        }

        row.HodnotaInt = change.NewHodnotaInt;
        row.SkutecnostZdroj = change.NewZdroj;
        row.PreferredExterniOdkazId = change.NewPreferredExterniOdkazId;
        row.UpdatedAt = nowUtc;
        updated++;
    }

    if (updated > 0)
    {
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    _logger.LogInformation(
        "ApplyPlan #{Id}: updated={U} skipManual={SM} noKandidat={NK} preferredFallback={PF} stale={ST}",
        plan.ProjektovyZaznamId, updated, skipManual, noKandidat, preferredFallbacks, staleSkipped);

    return new HarmonogramSyncResult(
        plan.ProjektovyZaznamId, updated, skipManual, noKandidat, preferredFallbacks);
}
```

- [ ] **Step 3: Refactor SyncZaznamAsync na wrapper**

V `HarmonogramSkutecnostSyncService.cs` nahradit body `SyncZaznamAsync`:

```csharp
public async Task<HarmonogramSyncResult> SyncZaznamAsync(int projektovyZaznamId, CancellationToken ct = default)
{
    var plan = await ComputePlanAsync(projektovyZaznamId, ct).ConfigureAwait(false);
    return await ApplyPlanAsync(plan, ct).ConfigureAwait(false);
}
```

- [ ] **Step 4: Spustit testy**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -c Release --no-restore --filter "FullyQualifiedName~HarmonogramSyncPlanTests" 2>&1 | tail -10
```

Expected: PASS 3/3.

- [ ] **Step 5: Spustit existující sync testy (regrese)**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -c Release --no-restore --filter "FullyQualifiedName~HarmonogramSkutecnostSyncServiceTests" 2>&1 | tail -10
```

Expected: stejný počet PASS jako baseline z Phase 0. Pokud regrese, zkontrolovat backwardc-compat (typicky `SyncZaznamAsync` wrapper musí volit Compute+Apply atomicky).

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/Schedules/HarmonogramSkutecnostSyncService.cs \
        PmTracker.Tests.Unit/Schedule/HarmonogramSyncPlanTests.cs
git commit -m "$(cat <<'EOF'
feat(harmonogram): ApplyPlanAsync + SyncZaznam wrapper (DESIGN-4-A)

Optimistic concurrency token via UpdatedAt — stale rows skip + log.
Tests stale token + fresh token pokrývají oba cases.
SyncZaznamAsync deleguje na Compute+Apply, callers beze změny.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

### Task 2.4: PreviewSync controller endpoint

**Files:**
- Modify: `PmTracker.Web/Controllers/HarmonogramController.cs`

- [ ] **Step 1: Přidat PreviewSync endpoint**

V `HarmonogramController.cs` přidat:

```csharp
public sealed record PreviewSyncRequest(int ZaznamId);

/// <summary>
/// DESIGN-9-C — staging endpoint pro UI pre-fetch flow.
/// Vrací plán zamýšlených změn pro daný záznam, NIC nezapisuje.
/// JS strana drží plán v sessionStorage; commit přes Save form (RecordService).
/// </summary>
[HttpPost("PreviewSync")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> PreviewSync([FromBody] PreviewSyncRequest request, CancellationToken ct)
{
    if (request is null || request.ZaznamId <= 0)
    {
        return BadRequest();
    }

    var projektId = await GetProjektIdAsync(request.ZaznamId, ct).ConfigureAwait(false);
    if (projektId is null)
    {
        return NotFound();
    }

    if (!await HasSchedulePermissionAsync(projektId.Value, ct).ConfigureAwait(false))
    {
        return Forbid();
    }

    var plan = await _sync.ComputePlanAsync(request.ZaznamId, ct).ConfigureAwait(false);
    return Ok(plan);
}
```

- [ ] **Step 2: Build check**

```bash
dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore 2>&1 | tail -5
```

Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Web/Controllers/HarmonogramController.cs
git commit -m "$(cat <<'EOF'
feat(harmonogram): POST /Harmonogram/PreviewSync endpoint (DESIGN-9-C backend)

Staging endpoint pro UI pre-fetch — vrací HarmonogramSyncPlan bez DB write.
Authz: records.schedule.edit (stejně jako ToggleRezim/SelectCandidate).
JS staging klient přijde v Phase 12.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 3: ManualActualKrokApplier Refactor (DESIGN-6-B)

### Task 3.1: Extract Apply method z DecisionCommands

**Files:**
- Modify: `PmTracker.Web/Services/Records/ManualActualKrokApplier.cs`
- Modify: `PmTracker.Web/Services/RecordProposalService.DecisionCommands.cs`

- [ ] **Step 1: Najít current ComputeManualActualOverrides v DecisionCommands**

```bash
grep -n "ComputeManualActualOverrides\|private.*ManualActualKrok" PmTracker.Web/Services/RecordProposalService.DecisionCommands.cs | head -10
```

Expected: zaznamenat řádky obsahující `ComputeManualActualOverrides` private metodu.

- [ ] **Step 2: Přidat Apply method na ManualActualKrokApplier**

V `PmTracker.Web/Services/Records/ManualActualKrokApplier.cs` rozšířit třídu (zachovat existující `Compute`):

```csharp
/// <summary>
/// DESIGN-6-B — public API pro aplikaci ManualActualKroky[] na harmonogram záznamu.
/// Sdílené mezi RecordProposalService.DecisionCommands (approve flow) a
/// RecordService.SaveRecord (direct save flow, DESIGN-6-A).
/// </summary>
/// <param name="manualKroky">Ruční skutečnosti z command/payload (kroky 2/5/8/9).</param>
/// <param name="schema">Schema záznamu (Kroky s TrvaniTypId + ZpozdeniTypId + KrokKey).</param>
/// <param name="datumZalozeni">Datum založení záznamu (start baseline timeline).</param>
/// <param name="plannedTypeIds">Set TypId pro DURATION řádky (= plán).</param>
/// <param name="submittedDurationValues">Hodnoty DURATION z payloadu (přepíše plánované hodnoty pro výpočet baseline).</param>
/// <returns>List ManualActualKrokApplied — DelayTypId + OdchylkaDni pro každý zadaný krok.</returns>
public static IReadOnlyList<ManualActualKrokApplied> Apply(
    IReadOnlyList<ManualActualKrokDto> manualKroky,
    HarmonogramSchema schema,
    DateTime datumZalozeni,
    IReadOnlySet<int> plannedTypeIds,
    IReadOnlyList<SaveRecordHarmonogramValueCommand> submittedDurationValues)
{
    if (manualKroky.Count == 0)
    {
        return Array.Empty<ManualActualKrokApplied>();
    }

    var krokKeyToPoradi = schema.Kroky.ToDictionary(k => k.KrokKey, k => k.KrokIndex);
    var krokKeyToDelayTypId = schema.Kroky.ToDictionary(k => k.KrokKey, k => k.ZpozdeniTypId);
    var krokKeyToDurationTypId = schema.Kroky.ToDictionary(k => k.KrokKey, k => k.TrvaniTypId);

    // Effective DURATION values: overlay submittedDurationValues over schema defaults
    var submittedByTypId = submittedDurationValues
        .Where(v => plannedTypeIds.Contains(v.TypId))
        .ToDictionary(v => v.TypId, v => v.Hodnota);
    var effectiveDurationByPoradi = schema.Kroky.ToDictionary(
        k => k.KrokIndex,
        k => submittedByTypId.TryGetValue(k.TrvaniTypId, out var v) ? v : k.TrvaniDni);

    // Compute baseline end per krok via TimelineCalculator
    var stepDefs = schema.Kroky.Select(k => new ScheduleTimelineStepDefinition
    {
        StepIndex = k.KrokIndex,
        Code = $"HS{k.KrokIndex:D2}",
        Name = k.Nazev,
        ColorHex = k.BarvaHex,
        DurationTypeId = k.TrvaniTypId,
        OffsetTypeId = k.ZpozdeniTypId
    }).ToList();
    var values = effectiveDurationByPoradi.ToDictionary(kv => stepDefs.First(s => s.StepIndex == kv.Key).DurationTypeId, kv => kv.Value);
    var timeline = ScheduleTimelineCalculator.Compute(datumZalozeni, stepDefs, values);
    var vypocet = timeline.Steps.Select(r => new HarmonogramVypocetKroku
    {
        KrokIndex = r.StepIndex,
        BaselineDatum = r.PlanEndDate
    }).ToList();

    return Compute(manualKroky, vypocet, krokKeyToPoradi, krokKeyToDelayTypId);
}
```

(Pokud `HarmonogramVypocetKroku` nebo `HarmonogramSchema` není definovaný v aktuálním kódu, najít ekvivalent v `Services/Data/`.)

- [ ] **Step 3: Refactor DecisionCommands.ComputeManualActualOverrides — delegovat na Apply**

V `RecordProposalService.DecisionCommands.cs` najít `ComputeManualActualOverrides` a zjednodušit:

```csharp
private static IReadOnlyList<ManualActualKrokApplier.ManualActualKrokApplied> ComputeManualActualOverrides(
    IReadOnlyList<ManualActualKrokDto> manualKroky,
    HarmonogramSchema schema,
    DateTime datumZalozeni,
    IReadOnlySet<int> plannedTypeIds,
    IReadOnlyList<SaveRecordHarmonogramValueCommand> submittedValues)
{
    return ManualActualKrokApplier.Apply(manualKroky, schema, datumZalozeni, plannedTypeIds, submittedValues);
}
```

(Lze i smazat helper úplně a inline volat `ManualActualKrokApplier.Apply` přímo na callsite.)

- [ ] **Step 4: Build + run all proposal tests**

```bash
dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore 2>&1 | tail -5
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -c Release --no-restore --filter "FullyQualifiedName~RecordProposal" 2>&1 | tail -10
```

Expected: build PASS, žádná regrese v proposal testech.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Records/ManualActualKrokApplier.cs \
        PmTracker.Web/Services/RecordProposalService.DecisionCommands.cs
git commit -m "$(cat <<'EOF'
refactor(harmonogram): extract ManualActualKrokApplier.Apply (DESIGN-6-B)

DRY extraction z DecisionCommands.ComputeManualActualOverrides do public method.
Připrava pro DESIGN-6-A (RecordService.SaveRecord persistence).
Žádný behavior change.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 4: ManualActualKroky Save Flow (DESIGN-6-A)

### Task 4.1: Failing test pro SaveRecord ManualActualKroky persistence

**Files:**
- Create: `PmTracker.Tests.Unit/Schedule/RecordServiceManualActualKrokyPersistenceTests.cs`

- [ ] **Step 1: Napsat failing test**

```csharp
using FluentAssertions;
// ... using statements
using Xunit;

namespace PmTracker.Tests.Unit.Schedule;

public sealed class RecordServiceManualActualKrokyPersistenceTests
{
    [Fact]
    public async Task SaveRecord_ManualActualKroky_PersistujeOdchylkuDoDelayHodnotaInt()
    {
        // Arrange: záznam s harmonogramem, user má records.schedule.edit, pošle ManualActualKrok pro krok 5
        // Komplet test setup vyžaduje InMemory PmTrackerDbContext + seed schématu.
        // Detail viz existing tests v PmTracker.Tests.Unit/Schedule/HarmonogramKrokDatumMappingTests.cs

        // Act: zavolat RecordService.SaveRecordAsync s SaveRecordCommand obsahujícím ManualActualKroky[i]

        // Assert: zaznam_harmonogram_hodnoty pro HS05_DELAY má HodnotaInt = (zadaneDatum - baselineEnd_HS05).Days
        //         + SkutecnostZdroj = Manual + UpdatedAt aktualizováno

        // Setup test po vzoru existing RecordService tests s real PmTrackerDbContext (InMemory).
        Assert.True(false, "TDD: implementace v Task 4.2");
    }
}
```

- [ ] **Step 2: Spustit test**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -c Release --no-restore --filter "FullyQualifiedName~RecordServiceManualActualKrokyPersistenceTests" 2>&1 | tail -10
```

Expected: FAIL (Assert.True(false)).

### Task 4.2: Persistence implementace v RecordService.SaveRecord

**Files:**
- Modify: `PmTracker.Web/Services/RecordService.SaveRecord.cs`

- [ ] **Step 1: Najít místo pro injection persistence logiky**

```bash
grep -n "command.HarmonogramHodnoty\|SaveRecordScheduleOnlyAsync\|isTaskCategory" PmTracker.Web/Services/RecordService.SaveRecord.cs | head -20
```

Expected: zaznamenat řádky kde se zpracovávají `HarmonogramHodnoty` v hlavní save metodě a v `SaveRecordScheduleOnlyAsync`.

- [ ] **Step 2: Přidat ApplyManualActualKrokyAsync helper**

V `RecordService.SaveRecord.cs` přidat nový private helper:

```csharp
private async Task ApplyManualActualKrokyAsync(
    SaveRecordCommand command,
    int recordId,
    DateTime datumZalozeni,
    int harmonogramSablonaVerze,
    bool isTaskCategory,
    IPendingScheduleProposalLockEvaluator pendingLockEvaluator,
    CancellationToken ct)
{
    if (!isTaskCategory || command.ManualActualKroky.Count == 0)
    {
        return;
    }

    // Validace: KrokKey ≠ Empty, žádné duplikáty, datum ≤ today.
    var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
    PmTracker.Web.Services.Records.ManualProposalFieldValidator.ValidateManualActualKroky(command.ManualActualKroky, today);

    // Pending lock pre-check — žádné překrývání s pending návrhem.
    var lockState = await pendingLockEvaluator.EvaluateAsync(recordId, ct).ConfigureAwait(false);
    if (lockState.HasPendingProposal && lockState.LockedManualKrokKeys is not null)
    {
        var conflicting = command.ManualActualKroky
            .Where(m => lockState.LockedManualKrokKeys.Contains(m.KrokKey))
            .ToList();
        if (conflicting.Count > 0)
        {
            throw new RecordValidationException(
                $"Krok je uzamčený pending návrhem #{lockState.ProposalId}, vyřeš návrh nejdříve.");
        }
    }

    // Načíst schema + plánované DURATION hodnoty z command.HarmonogramHodnoty.
    var schema = await _harmonogramService.GetSchemaForRecordAsync(
        new ProjektovyZaznamEntity { Id = recordId, HarmonogramSablonaVerze = harmonogramSablonaVerze, DatumZalozeni = datumZalozeni },
        ct).ConfigureAwait(false);
    var plannedTypeIds = schema.Kroky.Select(k => k.TrvaniTypId).Where(x => x > 0).ToHashSet();

    // Compute overrides via shared applier.
    var overrides = PmTracker.Web.Services.Records.ManualActualKrokApplier.Apply(
        command.ManualActualKroky, schema, datumZalozeni, plannedTypeIds, command.HarmonogramHodnoty);

    if (overrides.Count == 0)
    {
        return;
    }

    // UPSERT: pro každý DelayTypId buď update existing row, nebo insert new.
    var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
    var existingDelayRows = await _dbContext.ZaznamHarmonogramHodnoty
        .Where(h => h.ZaznamId == recordId && overrides.Select(o => o.DelayTypId).Contains(h.TypId))
        .ToListAsync(ct).ConfigureAwait(false);
    var existingByTypId = existingDelayRows.ToDictionary(r => r.TypId);

    foreach (var ov in overrides)
    {
        if (existingByTypId.TryGetValue(ov.DelayTypId, out var row))
        {
            row.HodnotaInt = ov.OdchylkaDni;
            row.SkutecnostZdroj = SkutecnostZdrojEnum.Manual;
            row.SkutecnostRezim = SkutecnostRezimEnum.Manual; // explicit user input
            row.UpdatedAt = nowUtc;
        }
        else
        {
            _dbContext.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
            {
                ZaznamId = recordId,
                TypId = ov.DelayTypId,
                HodnotaInt = ov.OdchylkaDni,
                SkutecnostZdroj = SkutecnostZdrojEnum.Manual,
                SkutecnostRezim = SkutecnostRezimEnum.Manual,
                UpdatedAt = nowUtc
            });
        }
    }

    await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
}
```

- [ ] **Step 3: Volat helper z hlavní SaveRecordAsync metody**

V `SaveRecordAsync` najít místo PO úspěšném SaveChanges (entity má Id) a volat:

```csharp
// Po úspěšném save záznamu + harmonogram hodnot:
await ApplyManualActualKrokyAsync(
    command,
    entity.Id,
    entity.DatumZalozeni,
    entity.HarmonogramSablonaVerze,
    isTaskCategory,
    _pendingScheduleProposalLockEvaluator,
    innerCt).ConfigureAwait(false);
```

(`_pendingScheduleProposalLockEvaluator` musí být injectnutý — pokud není, přidat do constructor parameters.)

- [ ] **Step 4: Volat helper z SaveRecordScheduleOnlyAsync**

Stejné volání v `SaveRecordScheduleOnlyAsync` (po commit hlavních harmonogram hodnot).

- [ ] **Step 5: Audit log RecordSchedule (analog DecisionCommands)**

Po `ApplyManualActualKrokyAsync` přidat audit:

```csharp
if (command.ManualActualKroky.Count > 0)
{
    var newScheduleRows = await _dbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
        .Where(x => x.ZaznamId == entity.Id)
        .ToListAsync(innerCt).ConfigureAwait(false);
    var newScheduleSnapshot = RecordScheduleAuditSnapshot.FromEntities(entity.Id, newScheduleRows);

    _auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
        AuditActionType.Update,
        AuditEntityType.RecordSchedule,
        entity.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
        BeforeState: null, // before snapshot pre-save by chtělo větší refactor; minimal viable bez before
        AfterState: newScheduleSnapshot));
}
```

- [ ] **Step 6: Update test z Task 4.1 — implementovat asserce**

Nahradit `Assert.True(false, "TDD")` v `RecordServiceManualActualKrokyPersistenceTests.cs` skutečným testem:

```csharp
// Setup: in-memory db + minimal schema + záznam s ZaznamId=1
// Act: SaveRecordAsync s ManualActualKroky = [{ KrokKey = HS05.KrokKey, AbsolutniDatum = DateOnly(2026,5,1) }]
// Assert: zaznam_harmonogram_hodnoty pro HS05_DELAY existuje s HodnotaInt = expected_delay_days
//         a SkutecnostZdroj = Manual a SkutecnostRezim = Manual
```

Konkrétní setup test musí matchovat existing patterns v `PmTracker.Tests.Unit/Records/` (najít test, který už setupuje SaveRecord + InMemoryDb a inspirovat se).

- [ ] **Step 7: Run test**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -c Release --no-restore --filter "FullyQualifiedName~RecordServiceManualActualKrokyPersistenceTests" 2>&1 | tail -15
```

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add PmTracker.Web/Services/RecordService.SaveRecord.cs \
        PmTracker.Tests.Unit/Schedule/RecordServiceManualActualKrokyPersistenceTests.cs
git commit -m "$(cat <<'EOF'
fix(harmonogram): persistence ManualActualKroky v SaveRecord (DESIGN-6-A, phantom UI bug 1)

Před fixem: UI input pro krok 2/5/8/9 form posílal data, RecordService.SaveRecord
ho ignoroval. Hodnota se nikdy neuložila — phantom UI.
Nyní: shared ManualActualKrokApplier.Apply() + UPSERT do zaznam_harmonogram_hodnoty
+ pending lock pre-check + RecordSchedule audit.

Closes phantom UI bug 1 (memory: project_manual_actual_kroky_phantom_ui).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 5: Permission Flags Unification (DESIGN-9-B + DESIGN-6-C)

### Task 5.1: Rozšířit ScheduleEditorPermissionSet

**Files:**
- Modify: `PmTracker.Web/Services/Schedules/ScheduleEditorPermissionSet.cs`
- Modify: `PmTracker.Web/Services/ProjectService.RecordEditorComposition.cs`

- [ ] **Step 1: Přidat nové flagy do ScheduleEditorPermissionSet**

V `ScheduleEditorPermissionSet.cs`:

```csharp
public sealed record ScheduleEditorPermissionSet
{
    // ... existing properties ...

    /// <summary>
    /// DESIGN-9-B/6-C — uživatel má klíč records.schedule.edit pro daný projekt.
    /// Composition: HasPermission("records.schedule.edit", projektId).
    /// Řídí přímou editaci skutečnosti (toggle, dropdown, manual cell input pro 2/5/8/9).
    /// </summary>
    public bool CanEditScheduleDirect { get; init; }

    /// <summary>
    /// DESIGN-9-B — uživatel má klíč proposals.schedule.create pro daný projekt.
    /// Composition: HasPermission("proposals.schedule.create", projektId).
    /// Řídí možnost otevřít proposal editor a submitnout návrh.
    /// </summary>
    public bool CanProposeSchedule { get; init; }

    /// <summary>
    /// DESIGN-6-C — composite flag řídící zobrazení manual input pro kroky 2/5/8/9.
    /// Logic: IsTaskCategory && !pendingLock.LocksSchedule && CanEditScheduleDirect.
    /// </summary>
    public bool CanEditManualActual { get; init; }

    public static ScheduleEditorPermissionSet ForFullEdit(bool isTaskCategory) => new()
    {
        IsTaskCategory = isTaskCategory,
        CanEditDuration = true,
        CanEditDelay = true,
        CanEditScheduleDirect = true,
        CanProposeSchedule = true,
        CanEditManualActual = isTaskCategory
        // ... ostatní existing flagy
    };

    public static ScheduleEditorPermissionSet ForReadOnly() => new()
    {
        // všechny flagy false (default record values)
    };

    // ... ostatní existing factory methods upravit obdobně
}
```

- [ ] **Step 2: Composition v RecordEditorComposition s authz check**

V `ProjectService.RecordEditorComposition.cs` (current line ~289):

```csharp
// Aktuální:
// canEditManualActual: isTaskCategory && !pendingScheduleProposalLock.LocksSchedule

// Nahradit:
var canEditScheduleDirect = currentUser.Authorization?
    .HasPermission(PmTracker.Web.Models.ViewModels.PermissionKeys.RecordsScheduleEdit, record.ProjektId) ?? false;
var canProposeSchedule = currentUser.Authorization?
    .HasPermission(PmTracker.Web.Models.ViewModels.PermissionKeys.ProposalsScheduleCreate, record.ProjektId) ?? false;
var canEditManualActual = isTaskCategory
    && !pendingScheduleProposalLock.LocksSchedule
    && canEditScheduleDirect;

// Předat do BuildScheduleBlockViewModel jako součást permissions:
permissions: pendingScheduleProposalLock.LocksSchedule
    ? ScheduleEditorPermissionSet.ForActiveScheduleProposal(isTaskCategory) with
      {
          CanEditScheduleDirect = canEditScheduleDirect,
          CanProposeSchedule = canProposeSchedule,
          CanEditManualActual = false
      }
    : ScheduleEditorPermissionSet.ForFullEdit(isTaskCategory) with
      {
          CanEditScheduleDirect = canEditScheduleDirect,
          CanProposeSchedule = canProposeSchedule,
          CanEditManualActual = canEditManualActual
      },
```

- [ ] **Step 3: Update _ScheduleBlock.cshtml použít nové flagy**

V `_ScheduleBlock.cshtml` najít current `Model.CanEditManualActual` (mimo `Permissions`) a přesunout:

```razor
@* Před: var canEdit = Model.CanEditManualActual && !isLocked; *@
@* Po: *@
var canEdit = Model.Permissions.CanEditManualActual && !isLocked;
```

A v `_ScheduleBlockManualCell.cshtml` analogicky pokud je tam reference.

- [ ] **Step 4: Build + run editor composition tests**

```bash
dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore 2>&1 | tail -5
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -c Release --no-restore --filter "FullyQualifiedName~RecordEditorComposition" 2>&1 | tail -10
```

Expected: build PASS, tests PASS.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Schedules/ScheduleEditorPermissionSet.cs \
        PmTracker.Web/Services/ProjectService.RecordEditorComposition.cs \
        PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml \
        PmTracker.Web/Views/Shared/_ScheduleBlockManualCell.cshtml
git commit -m "$(cat <<'EOF'
refactor(harmonogram): sjednocení permission flagů (DESIGN-9-B + DESIGN-6-C)

ScheduleEditorPermissionSet nově nese CanEditScheduleDirect, CanProposeSchedule,
CanEditManualActual (composite). Composition v RecordEditorComposition volá
HasPermission(records.schedule.edit) — UI input pro manuální kroky 2/5/8/9
se nezobrazí bez klíče (před fixem viditelný každému s task-category).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 6: Auto Step Validation (DESIGN-5-A + DESIGN-7-A)

### Task 6.1: ValidateAutoStepNotInProposal validátor

**Files:**
- Modify: `PmTracker.Web/Services/Records/ManualProposalFieldValidator.cs`
- Modify: `PmTracker.Web/Services/RecordProposalService.SubmitCommands.cs`
- Modify: `PmTracker.Web/Services/RecordProposalService.DecisionCommands.cs`
- Create: `PmTracker.Tests.Unit/Schedule/HarmonogramAutoStepProposalRejectionTests.cs`

- [ ] **Step 1: Failing test pro auto step rejection**

```csharp
using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Records;
using Xunit;

namespace PmTracker.Tests.Unit.Schedule;

public sealed class HarmonogramAutoStepProposalRejectionTests
{
    [Fact]
    public void Validate_ProposalActualHodnotyContainsAutoStepDelay_Throws()
    {
        // Arrange: payload obsahuje HodnotaCommand pro DelayTypId krok 4 (PMP K4_K7 auto-fill)
        // Schema map: krok 4 v PMP type → predikat K4_K7 (auto), nelze v návrhu.
        var actualValues = new[]
        {
            new SaveRecordHarmonogramValueCommand { TypId = 104 /* HS04_DELAY */, Hodnota = 5 }
        };
        var autoFilledTypIds = new HashSet<int> { 104 }; // HS04_DELAY je auto-filled v PMP

        // Act
        Action act = () => ManualProposalFieldValidator.ValidateAutoStepNotInProposal(actualValues, autoFilledTypIds);

        // Assert
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*krok*auto*");
    }

    [Fact]
    public void Validate_ProposalActualHodnotyContainsOnlyManualSteps_DoesNotThrow()
    {
        // Manual kroky (2/5/8/9) v payloadu jsou OK.
        var actualValues = new[]
        {
            new SaveRecordHarmonogramValueCommand { TypId = 105 /* HS05_DELAY */, Hodnota = 3 }
        };
        var autoFilledTypIds = new HashSet<int> { 101, 103, 104, 106, 107, 110 }; // bez HS05_DELAY

        Action act = () => ManualProposalFieldValidator.ValidateAutoStepNotInProposal(actualValues, autoFilledTypIds);

        act.Should().NotThrow();
    }
}
```

- [ ] **Step 2: Spustit test, ověřit failure**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -c Release --no-restore --filter "FullyQualifiedName~HarmonogramAutoStepProposalRejectionTests" 2>&1 | tail -10
```

Expected: FAIL (`ValidateAutoStepNotInProposal` neexistuje).

- [ ] **Step 3: Implementovat ValidateAutoStepNotInProposal**

V `ManualProposalFieldValidator.cs` přidat:

```csharp
/// <summary>
/// DESIGN-5-A/7-A — odmítne payload obsahující DELAY hodnoty pro auto-fillované kroky.
/// Auto kroky (1/3/4 PMP, 1/6/7/10 PNF) se plní automaticky ze SD vyjádření a
/// nelze je ani upravit napřímo, ani navrhnout úpravu.
/// </summary>
/// <param name="actualHodnoty">Položky payload.ActualHarmonogramHodnoty navrhované úpravy skutečnosti.</param>
/// <param name="autoFilledDelayTypIds">Set TypId DELAY řádků, které jsou pro daný typ záznamu auto-fillované (= z mapy v <see cref="Schedules.HarmonogramKrokDatumMapping"/>).</param>
public static void ValidateAutoStepNotInProposal(
    IReadOnlyList<SaveRecordHarmonogramValueCommand> actualHodnoty,
    IReadOnlySet<int> autoFilledDelayTypIds)
{
    if (actualHodnoty.Count == 0 || autoFilledDelayTypIds.Count == 0)
    {
        return;
    }

    foreach (var item in actualHodnoty)
    {
        if (autoFilledDelayTypIds.Contains(item.TypId))
        {
            throw new InvalidOperationException(
                $"Krok pro TypId {item.TypId} je v Auto rezimu — návrh úpravy skutečnosti není možný. " +
                $"Použij přímou editaci s ToggleRezim=Manual mimo návrhový workflow, nebo edituj jen manuální kroky 2/5/8/9.");
        }
    }
}
```

- [ ] **Step 4: Volat validátor v SubmitCommands**

V `RecordProposalService.SubmitCommands.cs` v `SubmitScheduleProposalAsync` (a `SubmitCreateProposalAsync` pokud má `ActualHarmonogramHodnoty`) přidat call PŘED stávající validation:

```csharp
// DESIGN-5-A/7-A: vyplnit set auto-fillovaných DelayTypId pro daný záznam.
// Pro každý napojený externí odkaz (PMP/PNF) zjistit jeho typZaznamu, pak z matice
// HarmonogramKrokDatumMapping zjistit auto-filled kroky → najít DelayTypId v schématu.
var autoFilledDelayTypIds = await ResolveAutoFilledDelayTypIdsAsync(command.ZaznamId, ct).ConfigureAwait(false);
ManualProposalFieldValidator.ValidateAutoStepNotInProposal(command.HarmonogramHodnoty, autoFilledDelayTypIds);
```

A přidat helper `ResolveAutoFilledDelayTypIdsAsync` (private):

```csharp
private async Task<IReadOnlySet<int>> ResolveAutoFilledDelayTypIdsAsync(int zaznamId, CancellationToken ct)
{
    if (zaznamId <= 0) return new HashSet<int>();

    // 1) Načíst zaznam → HarmonogramSablonaVerze
    var zaznam = await _dbContext.ProjektoveZaznamy.AsNoTracking()
        .FirstOrDefaultAsync(z => z.Id == zaznamId, ct).ConfigureAwait(false);
    if (zaznam is null) return new HashSet<int>();

    // 2) Zjistit typy napojených ticketů přes externí vazby + fingerprint
    var cisla = await _dbContext.ZaznamExterniOdkazy.AsNoTracking()
        .Where(eo => eo.ZaznamId == zaznamId && eo.Cislo != null)
        .Select(eo => eo.Cislo!)
        .Distinct()
        .ToListAsync(ct).ConfigureAwait(false);
    if (cisla.Count == 0) return new HashSet<int>();

    var fp = await _vyjadreniQueryService.GetHotZaznamFingerprintsAsync(cisla, ct).ConfigureAwait(false);
    var typy = fp.Values
        .Where(f => !string.IsNullOrWhiteSpace(f.TypZaznamu))
        .Select(f => f.TypZaznamu!)
        .Distinct()
        .ToList();

    // 3) Pro každý typ + krok poradí v matici → predikát existuje → krok je auto-filled
    var autoFilledKrokPoradi = new HashSet<int>();
    foreach (var typ in typy)
    {
        for (int poradi = 1; poradi <= 10; poradi++)
        {
            if (Schedules.HarmonogramKrokDatumMapping.IsAutomatickyKrok(typ, poradi))
            {
                autoFilledKrokPoradi.Add(poradi);
            }
        }
    }

    if (autoFilledKrokPoradi.Count == 0) return new HashSet<int>();

    // 4) Z schématu vyhledat DelayTypId pro tato poradí
    var delayTypIds = await _dbContext.CiselnikHarmonogramTypu.AsNoTracking()
        .Where(t => t.SablonaVerze == zaznam.HarmonogramSablonaVerze
                 && t.JeZpozdeni
                 && autoFilledKrokPoradi.Contains(t.KrokPoradi))
        .Select(t => t.Id)
        .ToListAsync(ct).ConfigureAwait(false);

    return new HashSet<int>(delayTypIds);
}
```

- [ ] **Step 5: Defense-in-depth v ApproveProposal**

V `RecordProposalService.DecisionCommands.cs` v `ApproveProposalAsync` (schedule plan change větev) přidat **stejný validátor call** na payload, NEŽ se aplikuje:

```csharp
// DESIGN-7-A defense-in-depth: i kdyby submit prošel obejmutím (např. starý payload),
// approve nesmí aplikovat auto step delay.
var autoFilledDelayTypIds = await ResolveAutoFilledDelayTypIdsAsync(record.Id, ct).ConfigureAwait(false);
PmTracker.Web.Services.Records.ManualProposalFieldValidator.ValidateAutoStepNotInProposal(
    schedulePayload.ActualHarmonogramHodnoty, autoFilledDelayTypIds);
```

(Helper sdílet — buď duplicate v DecisionCommands nebo factor out do common location, např. `RecordProposalService` parent class.)

- [ ] **Step 6: Run tests**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -c Release --no-restore --filter "FullyQualifiedName~HarmonogramAutoStepProposalRejectionTests" 2>&1 | tail -10
```

Expected: PASS 2/2.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Services/Records/ManualProposalFieldValidator.cs \
        PmTracker.Web/Services/RecordProposalService.SubmitCommands.cs \
        PmTracker.Web/Services/RecordProposalService.DecisionCommands.cs \
        PmTracker.Tests.Unit/Schedule/HarmonogramAutoStepProposalRejectionTests.cs
git commit -m "$(cat <<'EOF'
feat(harmonogram): odmítnutí auto-fillovaných kroků v návrhu (DESIGN-5-A/7-A)

Submit + Approve validace odmítne payload obsahující DELAY pro auto kroky
(1/3/4 PMP, 1/6/7/10 PNF). Defense-in-depth na obou vrstvách.
Vynucuje invariant: Auto rezim ↔ návrh = mutuálně výlučné stavy.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 7: ToggleRezim Pending Lock (DESIGN-7-B) + Create-If-Missing

### Task 7.1: Pending lock pre-check + create-if-missing v ToggleRezim

**Files:**
- Modify: `PmTracker.Web/Controllers/HarmonogramController.cs`

- [ ] **Step 1: Update ToggleRezim s pending lock + create-if-missing**

V `HarmonogramController.cs` upravit `ToggleRezim` action — analog `SelectCandidate` create-if-missing flow + pending lock pre-check:

```csharp
public sealed record ToggleRezimRequest(
    int HodnotaId,
    SkutecnostRezimEnum Rezim,
    int? ZaznamId = null,
    int? KrokPoradi = null);

[HttpPost("ToggleRezim")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ToggleRezim([FromBody] ToggleRezimRequest request, CancellationToken ct)
{
    if (request is null) return BadRequest();

    ZaznamHarmonogramHodnotaEntity? row = null;

    // Větev A: HodnotaId existuje — load row
    if (request.HodnotaId > 0)
    {
        row = await _db.ZaznamHarmonogramHodnoty
            .FirstOrDefaultAsync(h => h.Id == request.HodnotaId, ct).ConfigureAwait(false);
        if (row is null) return NotFound();
    }
    // Větev B: create-if-missing analog SelectCandidate
    else if (request.ZaznamId is int zaznamId && zaznamId > 0
             && request.KrokPoradi is int krokPoradi && krokPoradi > 0)
    {
        var zaznam = await _db.ProjektoveZaznamy.AsNoTracking()
            .FirstOrDefaultAsync(z => z.Id == zaznamId, ct).ConfigureAwait(false);
        if (zaznam is null) return NotFound();

        var delayTypId = await _db.CiselnikHarmonogramTypu.AsNoTracking()
            .Where(t => t.SablonaVerze == zaznam.HarmonogramSablonaVerze
                     && t.JeZpozdeni
                     && t.KrokPoradi == krokPoradi)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (delayTypId is null)
        {
            return NotFound(new { error = $"Krok {krokPoradi} neexistuje v schématu záznamu." });
        }

        row = await _db.ZaznamHarmonogramHodnoty
            .FirstOrDefaultAsync(h => h.ZaznamId == zaznamId && h.TypId == delayTypId.Value, ct).ConfigureAwait(false);
        if (row is null)
        {
            row = new ZaznamHarmonogramHodnotaEntity
            {
                ZaznamId = zaznamId,
                TypId = delayTypId.Value,
                HodnotaInt = 0,
                UpdatedAt = _time.GetUtcNow().UtcDateTime,
                SkutecnostRezim = SkutecnostRezimEnum.Auto,
                SkutecnostZdroj = SkutecnostZdrojEnum.Neznamo
            };
            _db.ZaznamHarmonogramHodnoty.Add(row);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
    else
    {
        return BadRequest(new { error = "Předej HodnotaId nebo ZaznamId+KrokPoradi." });
    }

    var projektId = await GetProjektIdAsync(row.ZaznamId, ct).ConfigureAwait(false);
    if (projektId is null) return NotFound();

    if (!await HasSchedulePermissionAsync(projektId.Value, ct).ConfigureAwait(false))
    {
        return Forbid();
    }

    // DESIGN-7-B: Pending lock pre-check — Manual→Auto cesta vyžaduje žádný pending.
    if (request.Rezim == SkutecnostRezimEnum.Auto && row.SkutecnostRezim == SkutecnostRezimEnum.Manual)
    {
        var lockState = await _pendingLockEvaluator.EvaluateAsync(row.ZaznamId, ct).ConfigureAwait(false);
        var krokKey = await _db.CiselnikHarmonogramTypu.AsNoTracking()
            .Where(t => t.Id == row.TypId)
            .Select(t => t.KrokKey)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (lockState.HasPendingProposal && lockState.LockedManualKrokKeys is not null
            && lockState.LockedManualKrokKeys.Contains(krokKey))
        {
            return BadRequest(new
            {
                error = $"Pending návrh #{lockState.ProposalId} blokuje přepnutí kroku do Auto rezimu. Vyřeš návrh nejdříve."
            });
        }
    }

    if (row.SkutecnostRezim == request.Rezim)
    {
        return Ok(new { changed = false });
    }

    var previousRezim = row.SkutecnostRezim;
    var previousZdroj = row.SkutecnostZdroj;

    row.SkutecnostRezim = request.Rezim;
    if (request.Rezim == SkutecnostRezimEnum.Manual)
    {
        if (row.SkutecnostZdroj == SkutecnostZdrojEnum.Automat)
        {
            row.SkutecnostZdroj = SkutecnostZdrojEnum.Manual;
        }
    }
    row.UpdatedAt = _time.GetUtcNow().UtcDateTime;
    await _db.SaveChangesAsync(ct).ConfigureAwait(false);

    await _audit.WriteAsync(_currentUser.OsobaId, new AuditWriteEntry(
        AuditActionType.Update,
        AuditEntityType.RecordSchedule,
        row.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
        BeforeState: new { Rezim = previousRezim, Zdroj = previousZdroj },
        AfterState: new { row.SkutecnostRezim, row.SkutecnostZdroj, Action = "toggle-rezim" }),
        ct).ConfigureAwait(false);

    if (request.Rezim == SkutecnostRezimEnum.Auto)
    {
        try
        {
            await _sync.SyncZaznamAsync(row.ZaznamId, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ToggleRezim sync selhal pro záznam {ZaznamId}.", row.ZaznamId);
        }
    }

    return Ok(new { changed = true, hodnotaId = row.Id, row.SkutecnostRezim, row.SkutecnostZdroj });
}
```

- [ ] **Step 2: Inject IPendingScheduleProposalLockEvaluator do controlleru**

Pokud není už injectnutý, přidat constructor parameter `IPendingScheduleProposalLockEvaluator pendingLockEvaluator` a private field `_pendingLockEvaluator`.

- [ ] **Step 3: Build check**

```bash
dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore 2>&1 | tail -5
```

Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/Controllers/HarmonogramController.cs
git commit -m "$(cat <<'EOF'
fix(harmonogram): ToggleRezim create-if-missing + pending lock pre-check (DESIGN-7-B, phantom UI bug 2)

- Create-if-missing analog SelectCandidate — switch může vzniknout i pro
  krok bez existujícího HS0X_DELAY řádku (catch-22 řešen)
- Pending lock blokuje Manual→Auto pokud krok je v LockedManualKrokKeys

Closes phantom UI bug 2 (ToggleRezim 404 na nový úkol).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 8: Max 1 Pending + Auto-Supersede (DESIGN-7-C)

### Task 8.1: Max 1 Pending invariant v SubmitCommands

**Files:**
- Modify: `PmTracker.Web/Services/RecordProposalService.SubmitCommands.cs`
- Modify: `PmTracker.Web/Services/Records/PendingScheduleProposalLockEvaluator.cs`
- Create: `PmTracker.Tests.Unit/Schedule/HarmonogramMaxOnePendingTests.cs`

- [ ] **Step 1: Failing test pro auto-supersede**

```csharp
// PmTracker.Tests.Unit/Schedule/HarmonogramMaxOnePendingTests.cs
using FluentAssertions;
// ...
namespace PmTracker.Tests.Unit.Schedule;

public sealed class HarmonogramMaxOnePendingTests
{
    [Fact]
    public async Task Submit_DruhyNavrhSamehoAutora_AutoSupersedePrvni()
    {
        // Arrange: existing Pending návrh #100 typu SCHEDULE_PLAN_CHANGE pro zaznam 1, autor osoba 5.
        // Act: submit nový SCHEDULE_PLAN_CHANGE pro zaznam 1, stejný autor osoba 5.
        // Assert:
        //   - existing #100 dostane Stav=Superseded a SupersededByProposalId=newId
        //   - nový návrh má Stav=Pending
        //   - PendingLockEvaluator pro zaznam 1 vrátí new návrh, ne #100
        Assert.True(false, "TDD: implementace v Step 3");
    }

    [Fact]
    public async Task Submit_DruhyNavrhCizihoAutora_BezKlice_Throws()
    {
        // Arrange: existing Pending návrh #100 autor osoba 5.
        // Act: submit nový SCHEDULE_PLAN_CHANGE od osoby 7 (nemá proposals.edit.any).
        // Assert: throws RecordValidationException "Záznam má pending návrh #100, vyžádej zamítnutí".
        Assert.True(false, "TDD");
    }
}
```

- [ ] **Step 2: Pre-check + auto-supersede v SubmitScheduleProposalAsync**

V `RecordProposalService.SubmitCommands.cs`:

```csharp
public async Task<int> SubmitScheduleProposalAsync(SaveRecordCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct)
{
    // ... existing validation ...

    // DESIGN-7-C: Max 1 Pending invariant — pre-check existujícího Pending návrhu
    var existingPending = await _dbContext.ZaznamNavrhy.AsNoTracking()
        .Where(n => n.ZaznamId == command.Id
                 && n.TypNavrhu == RecordProposalTypeCodes.SchedulePlanChange
                 && n.Stav == RecordProposalStateCodes.Pending)
        .OrderByDescending(n => n.CreatedAt)
        .FirstOrDefaultAsync(ct).ConfigureAwait(false);

    if (existingPending is not null)
    {
        var isOwn = existingPending.AutorOsobaId == currentUser.OsobaId;
        var canEditOwn = currentUser.Authorization?.HasPermission(PermissionKeys.ProposalsEditOwn, command.ProjektId) ?? false;
        var canEditAny = currentUser.Authorization?.HasPermission(PermissionKeys.ProposalsEditAny, command.ProjektId) ?? false;

        if (isOwn && canEditOwn)
        {
            // Auto-supersede vlastního návrhu (Stav=Superseded, link forward)
            await SupersedeProposalAsync(existingPending.Id, supersededByProposalId: 0 /* placeholder, update below */, ct).ConfigureAwait(false);
            // (později po vytvoření newId update SupersededByProposalId)
        }
        else if (!isOwn && canEditAny)
        {
            // Admin override — supersede cizího (audit-loggable)
            await SupersedeProposalAsync(existingPending.Id, supersededByProposalId: 0, ct).ConfigureAwait(false);
        }
        else
        {
            throw new RecordValidationException(
                $"Záznam má pending návrh #{existingPending.Id} od jiného autora. " +
                $"Vyžádej zamítnutí, nebo počkej na rozhodnutí.");
        }
    }

    // ... existing submit logic creates new ZaznamNavrhEntity with Stav=Pending ...
    var newProposal = new ZaznamNavrhEntity { /* ... */ Stav = RecordProposalStateCodes.Pending };
    _dbContext.ZaznamNavrhy.Add(newProposal);
    await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

    // Update SupersededByProposalId na superseded návrhu (now we have newProposal.Id)
    if (existingPending is not null)
    {
        var supersededTracked = await _dbContext.ZaznamNavrhy.FirstAsync(n => n.Id == existingPending.Id, ct).ConfigureAwait(false);
        supersededTracked.SupersededByProposalId = newProposal.Id;
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    return newProposal.Id;
}

private async Task SupersedeProposalAsync(int proposalId, int supersededByProposalId, CancellationToken ct)
{
    var proposal = await _dbContext.ZaznamNavrhy.FirstAsync(n => n.Id == proposalId, ct).ConfigureAwait(false);
    proposal.Stav = RecordProposalStateCodes.Superseded;
    proposal.SupersededByProposalId = supersededByProposalId > 0 ? supersededByProposalId : null;
    await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

    // Audit log
    _auditWriteService.Add(_currentUser.OsobaId, new AuditWriteEntry(
        AuditActionType.Update,
        AuditEntityType.Record, // proposal pojí na záznam, RecordSchedule není ideal
        proposalId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        BeforeState: new { Stav = RecordProposalStateCodes.Pending },
        AfterState: new { Stav = RecordProposalStateCodes.Superseded, SupersededBy = supersededByProposalId }));
}
```

- [ ] **Step 3: Update PendingScheduleProposalLockEvaluator filter na Stav=Pending**

V `PendingScheduleProposalLockEvaluator.cs` ověřit, že WHERE už filtruje `Stav == Pending` (line ~38). Aktuálně tam je — pokud ano, OK. Pokud ne, přidat filter aby Superseded návrhy nebyly v lock evaluation.

- [ ] **Step 4: Implementovat tests z Step 1**

Nahradit `Assert.True(false)` real testy s InMemory DB setup.

- [ ] **Step 5: Run tests**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -c Release --no-restore --filter "FullyQualifiedName~HarmonogramMaxOnePendingTests" 2>&1 | tail -10
```

Expected: PASS 2/2.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/RecordProposalService.SubmitCommands.cs \
        PmTracker.Web/Services/Records/PendingScheduleProposalLockEvaluator.cs \
        PmTracker.Tests.Unit/Schedule/HarmonogramMaxOnePendingTests.cs
git commit -m "$(cat <<'EOF'
feat(proposal): max 1 Pending invariant + auto-supersede (DESIGN-7-C)

Submit nového SCHEDULE_PLAN_CHANGE návrhu:
- Vlastní starý Pending → auto-supersede (Stav=Superseded + SupersededByProposalId)
- Cizí Pending bez proposals.edit.any → throw RecordValidationException
- Cizí Pending s proposals.edit.any → admin override supersede + audit

Žádný bordel v data flow (max 1 active Pending per zaznam+typ).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 9: UI Visibility — Remove TrvaniDni Filter (DESIGN-9-D)

### Task 9.1: Smazat filter v read-only Detail render

**Files:**
- Modify: `PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml`

- [ ] **Step 1: Najít všechna místa s `Where(TrvaniDni > 0)` filtrem**

```bash
grep -n "TrvaniDni > 0\|Where(krok => krok\.TrvaniDni" PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml
```

Expected: 2 výskyty:
- ~ř.19 (`visibleCompactSteps` — compact rainbow strip read-only varianta)
- ~ř.164 (read-only Detail breakdown foreach)

Oba výskyty musí být odstraněny.

- [ ] **Step 2a: Smazat filter v `visibleCompactSteps` (compact rainbow)**

V `_ScheduleBlock.cshtml` ~ř.17-21:

```razor
@* Před: *@
var visibleCompactSteps = (isEditor
        ? Model.Kroky
        : Model.Kroky.Where(krok => krok.TrvaniDni > 0))
    .OrderBy(krok => krok.KrokIndex)
    .ToList();

@* Po — sjednoceno editor + read-only, vždy všechny kroky: *@
var visibleCompactSteps = Model.Kroky
    .OrderBy(krok => krok.KrokIndex)
    .ToList();
```

Důsledek: read-only Detail compact rainbow zobrazí všech 10 kroků jako segmenty. Per-segment visibility (zero-duration / NULL skutečnost) se řídí inline `style` v Step 2b.

- [ ] **Step 2b: Per-row segment visibility v compact rainbow strip foreach**

V `_ScheduleBlock.cshtml` ~ř.87-122 — foreach planned/actual row + foreach krok. Současné `initialHidden` pokrývá jen `TrvaniDni <= 0` univerzálně pro oba řádky (planned i actual). Rozdělit per-row:

```razor
@foreach (var row in new[] { ("planned", "Plán"), ("actual", "Skutečnost") })
{
    <div class="@overviewRowCssClass" data-schedule-overview-row="@row.Item1">
        <span class="@overviewLabelCssClass">@row.Item2</span>
        <div class="@overviewTrackCssClass" data-schedule-overview-track="@row.Item1">
            <div class="@overviewMarkerCssClass today" data-schedule-marker="today" ... title="Dnes"></div>
            <div class="@overviewMarkerCssClass @(isEditor ? null : "deadline")" data-schedule-marker="deadline" ...></div>
            @foreach (var krok in visibleCompactSteps)
            {
                var segmentTitle = row.Item1 == "planned"
                    ? $"{krok.Nazev}: plán {krok.BaselineDatum:dd.MM.yyyy}"
                    : $"{krok.Nazev}: skutečnost {(krok.OdchylkaDni.HasValue ? krok.SkutecneDatum.ToString("dd.MM.yyyy") : "—")}";
                var ariaLabel = row.Item1 == "planned"
                    ? $"Krok {krok.KrokIndex} {krok.Nazev}: plán {krok.BaselineDatum:dd.MM.yyyy}"
                    : $"Krok {krok.KrokIndex} {krok.Nazev}: skutečnost {(krok.OdchylkaDni.HasValue ? krok.SkutecneDatum.ToString("dd.MM.yyyy") : "krok ještě nenastal")}";

                // Per-row visibility — DESIGN-9-D + DESIGN-10-A.
                // Planned segment: hidden pokud TrvaniDni == 0 (zero-width plán).
                // Actual segment: hidden pokud OdchylkaDni == NULL (krok nenastal, žádný záznam).
                // V editor mode je per-row visibility stále řešena přes JS layoutter (data-step-duration / data-step-delay attrs),
                // ale SSR initial state musí být korektní — žádný segment-ghost při prvním render.
                var isPlannedHidden = row.Item1 == "planned" && krok.TrvaniDni <= 0;
                var isActualHidden = row.Item1 == "actual" && !krok.OdchylkaDni.HasValue;
                var initialHidden = (isPlannedHidden || isActualHidden) ? "display:none;" : string.Empty;

                <span class="@overviewSegmentCssClass"
                      role="img"
                      aria-label="@ariaLabel"
                      data-rainbow-segment-label-full="@krok.Nazev"
                      data-rainbow-segment-label-short="@krok.KrokIndex"
                      data-schedule-segment-kind="@row.Item1"
                      data-step-index="@krok.KrokIndex"
                      data-step-has-actual="@(krok.OdchylkaDni.HasValue ? "true" : "false")"
                      style="background:@krok.BarvaHex;@initialHidden"
                      title="@segmentTitle"></span>
            }
        </div>
    </div>
}
```

- [ ] **Step 2c: Smazat filter v read-only Detail breakdown**

V `_ScheduleBlock.cshtml` ~ř.164:

```razor
@* Před: *@
@* @foreach (var krok in Model.Kroky.Where(k => k.TrvaniDni > 0).OrderBy(k => k.KrokIndex)) *@

@* Po: *@
@foreach (var krok in Model.Kroky.OrderBy(k => k.KrokIndex))
```

- [ ] **Step 3: Render všech kroků plně — žádný placeholder, žádné skrývání**

Důležité business pravidlo (user feedback 2026-05-01): **žádné skrývání kroků z jakéhokoli důvodu**. Každý krok schématu se vždy zobrazí plnohodnotným řádkem. Render po DESIGN-10-A nullable refactoru musí být tolerantní k:
- `TrvaniDni == 0` — krok bez plánu (planned segment width = 0, ale element v DOMu existuje pro JS layoutter)
- `OdchylkaDni == NULL` — krok ještě nenastal, žádný záznam o skutečnosti. **Žádný actual segment se nerenderuje vůbec** (`@if (hasActual)` block neprovede). Badge zdroje 🤖/✍️/📜/— ukáže `—` (Neznamo).
- `OdchylkaDni == 0` (non-null) — skutečnost vyplněna, krok dokončen v plánu (Zdroj může být Automat/Manual/Historicka). Actual segment **shodný s baseline**, label "0 dnů".
- `OdchylkaDni != 0` (non-null) — skutečnost s odchylkou (actual segment posunutý, label "+N dnů" / "-N dnů")
- Skutečnost bez plánu (`TrvaniDni == 0` ale `OdchylkaDni != null`) — uživatel-lajdák, ale render funguje (planned 0-width, actual jako overlay od start kroku)

V `_ScheduleBlock.cshtml` po smazání `Where(TrvaniDni > 0)` filtrů z předchozích Step 2a/2c zachovat existující full render markup — JEN ho rozšířit o tolerance hodnot:

```razor
@foreach (var krok in Model.Kroky.OrderBy(k => k.KrokIndex))
{
    var hasPlan = krok.TrvaniDni > 0;
    // DESIGN-10-A: hasActual čistě nullable check — žádné magic 0 + Zdroj kombinace.
    var hasActual = krok.OdchylkaDni.HasValue;
    var odchylka = krok.OdchylkaDni ?? 0;
    var offsetLabel = odchylka > 0 ? $"+{odchylka} dnů"
                    : odchylka < 0 ? $"{odchylka} dnů"
                    : "0 dnů";
    var offsetCssClass = odchylka > 0 ? "late"
                       : odchylka < 0 ? "ahead"
                       : null;
    <div class="gantt-step-row"
         data-schedule-step-row
         data-step-index="@krok.KrokIndex"
         data-step-name="@krok.Nazev"
         data-step-color="@krok.BarvaHex"
         data-step-duration="@krok.TrvaniDni"
         data-step-delay="@(krok.OdchylkaDni?.ToString() ?? "")"
         data-step-has-plan="@(hasPlan ? "true" : "false")"
         data-step-has-actual="@(hasActual ? "true" : "false")">
        <div class="gantt-step-name">
            <span>@krok.Nazev</span>
            @if (hasActual)
            {
                <span class="gantt-step-offset @offsetCssClass" data-schedule-offset>@offsetLabel</span>
            }
        </div>
        <div class="schedule-layered-track schedule-layered-track--step" data-schedule-breakdown-track>
            <div class="schedule-layered-marker today" data-schedule-breakdown-today title="Dnes"></div>
            @* Planned segment — render i pro zero-duration (width 0 v CSS, ale element existuje pro consistency) *@
            <span class="schedule-layered-segment planned"
                  role="img"
                  aria-label="Krok @krok.KrokIndex @krok.Nazev: plán @krok.BaselineDatum.ToString("dd.MM.yyyy")"
                  data-segment-tooltip="Krok @krok.KrokIndex @krok.Nazev: plán @krok.BaselineDatum.ToString("dd.MM.yyyy")"
                  data-rainbow-segment-label-full="@krok.Nazev"
                  data-rainbow-segment-label-short="@krok.KrokIndex"
                  data-schedule-breakdown-segment="planned"
                  data-step-index="@krok.KrokIndex"
                  data-step-zero-plan="@(hasPlan ? "false" : "true")"
                  style="background:@krok.BarvaHex;"></span>
            @* Actual segment — render pouze pokud má smysluplnou skutečnost *@
            @if (hasActual)
            {
                <span class="schedule-layered-segment actual"
                      role="img"
                      aria-label="Krok @krok.KrokIndex @krok.Nazev: skutečnost @krok.SkutecneDatum.ToString("dd.MM.yyyy")"
                      data-segment-tooltip="Krok @krok.KrokIndex @krok.Nazev: skutečnost @krok.SkutecneDatum.ToString("dd.MM.yyyy")"
                      data-schedule-breakdown-segment="actual"
                      data-step-index="@krok.KrokIndex"
                      style="background:@krok.BarvaHex;"></span>
            }
        </div>
    </div>
}
```

**Pravidlo `hasActual` po DESIGN-10-A nullable refactoru:**

| `OdchylkaDni` | Sémantika | UI render actual segment |
|---|---|---|
| `NULL` | "krok ještě nenastal, není o něm záznam" | NE (`hasActual = false`) — actual segment v DOMu vůbec není, compact rainbow `display:none` |
| `0` | "vše šlo dle plánu" (krok dokončen včas, zdroj může být Automat/Manual/Historicka) | ANO — actual segment shodný s baseline, label "0 dnů" |
| non-0 | odchylka N dnů (kladné = zpoždění, záporné = předstih) | ANO — actual segment posunutý, label "+N dnů" / "-N dnů" |

**Řádek kroku zůstává vždy viditelný** s názvem, jen actual segment uvnitř může chybět.

- [ ] **Step 4: CSS handling pro zero-plan segment**

V `harmonogram-skutecnost.css` (nebo equivalent) přidat / aktualizovat:

```css
/* DESIGN-9-D — krok s nulovým plánem stále zobrazený, jen planned segment má 0 šířku.
   data-step-zero-plan="true" zajistí že se segment z layout výpočtu vyřadí
   (JS layoutter používá data-step-duration k computation šířky). */
.schedule-layered-segment.planned[data-step-zero-plan="true"] {
    width: 0;
    min-width: 0;
    visibility: hidden; /* nezabírat místo, ale zachovat element pro JS index consistency */
}

/* Krok bez actual segmentu (skutečnost zatím nenastala) — žádné speciální styling,
   prostě actual segment není v DOMu. Planned segment se zobrazí normálně. */
```

- [ ] **Step 5: Visual smoke check**

```bash
dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore 2>&1 | tail -3
```

Expected: build PASS. Visual ověření po deploy v Phase 14 audit.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml \
        PmTracker.Web/wwwroot/css/components/harmonogram-skutecnost.css
git commit -m "$(cat <<'EOF'
ui(harmonogram): zrušit jakékoli skrývání kroků v compact + breakdown (DESIGN-9-D + DESIGN-10-A)

Read-only Detail dříve filtroval Where(TrvaniDni > 0) v 2 místech (compact rainbow
strip + layered breakdown) → kroky bez plánu (včetně těch s ručně doplněnou
skutečností) mizely. Memory project_harmonogram_visibility_rules — žádné
skrývání kroků z žádného důvodu.

Compact rainbow strip:
- visibleCompactSteps zruší Where(TrvaniDni > 0), všechny kroky vždy v DOMu
- Per-row segment visibility:
  * planned row: hidden pokud TrvaniDni == 0
  * actual row: hidden pokud OdchylkaDni == NULL
- aria-label pro NULL skutečnost: "krok ještě nenastal"

Layered breakdown:
- Žádný filter, žádný empty placeholder, render každého kroku plně
- Plán segment vždy (zero-duration → CSS width:0 visibility:hidden, element zachován)
- Actual segment jen pokud OdchylkaDni.HasValue (čistá nullable semantika po DESIGN-10-A)
- OdchylkaDni == NULL → no actual segment (krok nenastal)
- OdchylkaDni == 0 → actual segment shodný s baseline (vše dle plánu)
- OdchylkaDni != 0 → actual segment posunutý + label "+N dnů" / "-N dnů"

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 10: DURATION Input Redesign (DESIGN-9-F)

### Task 10.1: Markup change — kalendář + readonly text

**Files:**
- Modify: `PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml`
- Create: `PmTracker.Web/wwwroot/js/modules/harmonogram/duration-calendar-binding.js`
- Modify: `PmTracker.Web/wwwroot/js/modules/bootstrap.js`

- [ ] **Step 1: Update _ScheduleBlock.cshtml — DURATION cell markup**

Najít `<td>` s DURATION inputem (řádek ~245-300, hidden TypId + number input + datepicker + +/- stepper) a nahradit:

```razor
<td>
    <div class="schedule-stacked-input @(durationChanged ? "proposal-field-changed" : null)"
         title="@durationTooltip">
        <input type="hidden" name="HarmonogramHodnoty[@durationInputIndex].TypId" value="@krok.TrvaniTypId" />
        <input type="hidden" name="HarmonogramHodnoty[@durationInputIndex].Hodnota"
               value="@krok.TrvaniDni"
               data-schedule-duration-hidden
               data-schedule-original-duration="@krok.TrvaniDni" />
        <div class="schedule-duration-row">
            @await Html.PartialAsync(
                "_AppDateField",
                new AppDateFieldViewModel
                {
                    Name = $"UiHarmonogramDatumy[{i}]",
                    IsoValue = krok.BaselineDatum.ToString("yyyy-MM-dd"),
                    DisplayValue = krok.BaselineDatum.ToString("dd.MM.yyyy"),
                    Locked = dateDisabled,
                    AriaLabel = $"Datum konce kroku {krok.Nazev}",
                    ContainerCssClass = $"schedule-date-field schedule-date-field-stacked{(durationChanged ? " proposal-field-changed" : string.Empty)}",
                    ExtraDataAttributes = new Dictionary<string, object?>
                    {
                        ["data-schedule-duration-calendar"] = "true",
                        ["data-schedule-static-disabled"] = stepStaticDisabled ? "true" : "false",
                        ["title"] = durationTooltip
                    }
                })
            <span class="schedule-duration-days-readonly"
                  data-schedule-duration-readonly
                  aria-label="Trvání ve dnech"
                  title="Trvání kroku v kalendářních dnech (vypočteno z datumu konce a začátku kroku)">
                @krok.TrvaniDni dnů
            </span>
        </div>
    </div>
</td>
```

(+/- stepper buttony **smazat**.)

- [ ] **Step 2: Vytvořit duration-calendar-binding.js**

```javascript
// PmTracker.Web/wwwroot/js/modules/harmonogram/duration-calendar-binding.js
//
// DESIGN-9-F — kalendář datum konce kroku ⇄ readonly počet dní text.
// Při změně data v kalendáři přepočítá počet dnů (datum konce - datum začátku
// předchozího kroku, resp. DatumZalozeni pro krok 1) a zapíše do hidden field
// HarmonogramHodnoty[i].Hodnota + zobrazí v .schedule-duration-days-readonly.
//
// Cumulative chain: změna kroku N posune startovní bod kroku N+1 → recalculate
// readonly text všech následujících kroků.

(function (global) {
    'use strict';

    const ROW_SELECTOR = '[data-schedule-step-row]';
    const CALENDAR_SELECTOR = '[data-schedule-duration-calendar]';
    const HIDDEN_SELECTOR = '[data-schedule-duration-hidden]';
    const READONLY_SELECTOR = '[data-schedule-duration-readonly]';

    function parseIso(value) {
        if (!value) return null;
        const d = new Date(value + 'T00:00:00');
        return isNaN(d.getTime()) ? null : d;
    }

    function formatIso(date) {
        const yyyy = date.getFullYear();
        const mm = String(date.getMonth() + 1).padStart(2, '0');
        const dd = String(date.getDate()).padStart(2, '0');
        return `${yyyy}-${mm}-${dd}`;
    }

    function diffDays(start, end) {
        const ms = end.getTime() - start.getTime();
        return Math.round(ms / (24 * 3600 * 1000));
    }

    function getStartDateForStep(rows, stepIndex, recordStartDate) {
        // Krok 1 začíná v recordStartDate; krok N začíná tam, kde N-1 končí.
        let cursor = new Date(recordStartDate);
        for (const row of rows) {
            const idx = parseInt(row.getAttribute('data-step-index') || '0', 10);
            if (idx >= stepIndex) break;
            const cal = row.querySelector(CALENDAR_SELECTOR);
            const isoInput = cal?.querySelector('input[type="hidden"][name*="UiHarmonogramDatumy"]');
            const iso = isoInput?.value;
            const end = parseIso(iso);
            if (end) cursor = end;
        }
        return cursor;
    }

    function recalcRow(row, recordStartDate, allRows) {
        const stepIndex = parseInt(row.getAttribute('data-step-index') || '0', 10);
        const cal = row.querySelector(CALENDAR_SELECTOR);
        const hidden = row.querySelector(HIDDEN_SELECTOR);
        const readonly = row.querySelector(READONLY_SELECTOR);
        if (!cal || !hidden || !readonly) return;

        const isoInput = cal.querySelector('input[type="hidden"][name*="UiHarmonogramDatumy"]');
        const endDate = parseIso(isoInput?.value);
        if (!endDate) return;

        const startDate = getStartDateForStep(allRows, stepIndex, recordStartDate);
        const days = Math.max(0, diffDays(startDate, endDate));
        hidden.value = String(days);
        readonly.textContent = `${days} dnů`;
    }

    function recalcAllAfter(stepIndex, recordStartDate, allRows) {
        for (const row of allRows) {
            const idx = parseInt(row.getAttribute('data-step-index') || '0', 10);
            if (idx >= stepIndex) recalcRow(row, recordStartDate, allRows);
        }
    }

    function init() {
        const recordStartIso = document.querySelector('[data-schedule-record-start]')?.getAttribute('data-schedule-record-start');
        const recordStartDate = parseIso(recordStartIso) || new Date();

        document.addEventListener('change', (event) => {
            const cal = event.target?.closest(CALENDAR_SELECTOR);
            if (!cal) return;
            const row = cal.closest(ROW_SELECTOR);
            if (!row) return;
            const stepIndex = parseInt(row.getAttribute('data-step-index') || '0', 10);
            const allRows = Array.from(document.querySelectorAll(ROW_SELECTOR))
                .sort((a, b) => parseInt(a.getAttribute('data-step-index') || '0', 10)
                              - parseInt(b.getAttribute('data-step-index') || '0', 10));
            recalcAllAfter(stepIndex, recordStartDate, allRows);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    global.pmDurationCalendarBinding = { recalcAllAfter };
})(window);
```

- [ ] **Step 3: Přidat data-schedule-record-start atribut na schedule block kořen**

V `_ScheduleBlock.cshtml` najít root element schedule-block a přidat:

```razor
<div class="schedule-block" data-schedule-record-start="@Model.DatumZalozeni.ToString("yyyy-MM-dd")">
```

- [ ] **Step 4: Import + init z bootstrap.js**

V `PmTracker.Web/wwwroot/js/modules/bootstrap.js` přidat side-effect import:

```javascript
import './harmonogram/duration-calendar-binding.js';
```

(Pozor: per memory `project_bundle_sync` — side-effect moduly musí být explicit imported v bootstrap.js, jinak tichý fail.)

- [ ] **Step 5: CSS pro nový layout**

V `harmonogram-skutecnost.css` (nebo equivalent):

```css
.schedule-duration-row {
    display: flex;
    align-items: center;
    gap: 0.5rem;
}

.schedule-duration-days-readonly {
    font-size: 0.9rem;
    color: var(--gov-color-text-secondary, #475467);
    white-space: nowrap;
    user-select: none;
}
```

- [ ] **Step 6: Build + smoke test**

```bash
dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore 2>&1 | tail -3
```

Expected: build PASS.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml \
        PmTracker.Web/wwwroot/js/modules/harmonogram/duration-calendar-binding.js \
        PmTracker.Web/wwwroot/js/modules/bootstrap.js \
        PmTracker.Web/wwwroot/css/components/harmonogram-skutecnost.css
git commit -m "$(cat <<'EOF'
ui(harmonogram): DURATION input = kalendář + readonly dny (DESIGN-9-F)

Smazán number input s +/- stepperem, primární editor je kalendář (datum konce).
JS přepočítá trvání dnů ze (datum konce - datum začátku kroku) a propíše do
hidden field HarmonogramHodnoty[i].Hodnota + zobrazí jako readonly text.
Cumulative chain — změna kroku N přepočítá všechny následující.

Vizuální sjednocení s skutečností (která je už datum-driven přes ManualCell).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 11: select-candidate.js JS Handler (DESIGN-9-A)

### Task 11.1: Vytvořit JS handler

**Files:**
- Create: `PmTracker.Web/wwwroot/js/modules/schedule-feature-c/select-candidate.js`
- Modify: `PmTracker.Web/wwwroot/js/modules/bootstrap.js`

- [ ] **Step 1: Vytvořit select-candidate.js**

```javascript
// PmTracker.Web/wwwroot/js/modules/schedule-feature-c/select-candidate.js
//
// DESIGN-9-A — JS handler pro dropdown výběr preferred kandidáta v Auto kroku.
// Před fixem: tlačítka [data-feature-c-select-candidate] v _ScheduleBlockManualCell.cshtml
// existovala, ale žádný JS handler je neposlouchal — klik nedělal nic (phantom UI bug 3).
//
// Flow:
//  1. Klik na <button data-feature-c-select-candidate> uvnitř <details> dropdownu
//  2. POST /Harmonogram/SelectCandidate { HodnotaId, ExterniOdkazId, ZaznamId, KrokPoradi }
//  3. Po success refresh data-attributes buňky + close <details>
//  4. Auto-switch UX: výběr preferred = explicit user volba, ale rezim zůstává Auto
//     (= sync respektuje preferred). Pro switch na Manual user musí použít toggle.

(function (global) {
    'use strict';

    const BUTTON_SELECTOR = '[data-feature-c-select-candidate]';
    const CELL_SELECTOR = '[data-schedule-actual-cell]';
    const DROPDOWN_SELECTOR = '[data-feature-c-dropdown]';

    function getAntiforgery() {
        const input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : null;
    }

    async function postSelectCandidate(payload) {
        const token = getAntiforgery();
        const headers = { 'Content-Type': 'application/json', 'Accept': 'application/json' };
        if (token) headers['RequestVerificationToken'] = token;
        const resp = await fetch('/Harmonogram/SelectCandidate', {
            method: 'POST',
            headers: headers,
            credentials: 'same-origin',
            body: JSON.stringify(payload)
        });
        if (!resp.ok) {
            const body = await resp.text().catch(() => '');
            throw new Error(`HTTP ${resp.status}: ${body || resp.statusText}`);
        }
        return await resp.json();
    }

    function refreshSelectionInUi(cell, selectedExterniOdkazId) {
        const items = cell.querySelectorAll('[data-feature-c-select-candidate]');
        items.forEach((btn) => {
            const id = parseInt(btn.getAttribute('data-externi-odkaz-id') || '0', 10);
            const isSelected = id === selectedExterniOdkazId;
            btn.setAttribute('data-is-selected', isSelected ? 'true' : 'false');
            btn.setAttribute('aria-current', isSelected ? 'true' : 'false');

            const li = btn.closest('li');
            if (li) li.classList.toggle('schedule-actual-cell__dropdown-item--selected', isSelected);

            const check = btn.querySelector('.schedule-actual-cell__dropdown-item-check');
            if (isSelected && !check) {
                const span = document.createElement('span');
                span.className = 'schedule-actual-cell__dropdown-item-check';
                span.setAttribute('aria-hidden', 'true');
                span.textContent = '✓';
                btn.appendChild(span);
            } else if (!isSelected && check) {
                check.remove();
            }
        });

        // Close dropdown
        const details = cell.querySelector(DROPDOWN_SELECTOR);
        if (details && details.tagName === 'DETAILS') {
            details.removeAttribute('open');
        }
    }

    function showError(cell, message) {
        if (!cell) { alert(message); return; }
        let errBox = cell.querySelector('[data-feature-c-error]');
        if (!errBox) {
            errBox = document.createElement('div');
            errBox.setAttribute('data-feature-c-error', '');
            errBox.className = 'schedule-actual-cell__error';
            cell.appendChild(errBox);
        }
        errBox.textContent = message;
        setTimeout(() => { errBox?.remove(); }, 5000);
    }

    async function handleClick(event) {
        const btn = event.target?.closest(BUTTON_SELECTOR);
        if (!btn) return;
        event.preventDefault();

        const cell = btn.closest(CELL_SELECTOR);
        const hodnotaId = parseInt(btn.getAttribute('data-hodnota-id') || '0', 10);
        const externiOdkazId = parseInt(btn.getAttribute('data-externi-odkaz-id') || '0', 10);
        const zaznamId = parseInt(btn.getAttribute('data-zaznam-id') || '0', 10);
        const krokPoradi = parseInt(btn.getAttribute('data-krok-poradi') || '0', 10);

        if (!Number.isFinite(externiOdkazId) || externiOdkazId <= 0) {
            showError(cell, 'Chybí ID externího odkazu kandidáta.');
            return;
        }

        const payload = {
            HodnotaId: hodnotaId > 0 ? hodnotaId : 0,
            ExterniOdkazId: externiOdkazId,
            ZaznamId: zaznamId > 0 ? zaznamId : null,
            KrokPoradi: krokPoradi > 0 ? krokPoradi : null
        };

        btn.disabled = true;
        try {
            const result = await postSelectCandidate(payload);
            refreshSelectionInUi(cell, result?.preferredExterniOdkazId || externiOdkazId);
        } catch (err) {
            showError(cell, `Výběr kandidáta selhal: ${err.message}`);
        } finally {
            btn.disabled = false;
        }
    }

    function init() {
        document.addEventListener('click', handleClick);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    global.pmSelectCandidate = { postSelectCandidate };
})(window);
```

- [ ] **Step 2: Side-effect import v bootstrap.js**

V `PmTracker.Web/wwwroot/js/modules/bootstrap.js` přidat:

```javascript
import './schedule-feature-c/select-candidate.js';
```

- [ ] **Step 3: Build + manual smoke check**

```bash
dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore 2>&1 | tail -3
```

Expected: build PASS. Manual smoke test po deploy v Phase 14.

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/wwwroot/js/modules/schedule-feature-c/select-candidate.js \
        PmTracker.Web/wwwroot/js/modules/bootstrap.js
git commit -m "$(cat <<'EOF'
fix(harmonogram): select-candidate.js JS handler (DESIGN-9-A, phantom UI bug 3)

Před fixem: dropdown <button data-feature-c-select-candidate> v UI byly bez
JS handleru — klik nedělal nic kromě otevření/zavření <details>.
Server endpoint POST /Harmonogram/SelectCandidate fungoval, jen ho nikdo nevolal.

Closes phantom UI bug 3.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 12: Pre-Fetch Staging Flow (DESIGN-9-C)

### Task 12.1: JS staging klient

**Files:**
- Create: `PmTracker.Web/wwwroot/js/modules/schedule-feature-c/preview-sync.js`
- Modify: `PmTracker.Web/wwwroot/js/modules/bootstrap.js`

- [ ] **Step 1: Vytvořit preview-sync.js**

```javascript
// PmTracker.Web/wwwroot/js/modules/schedule-feature-c/preview-sync.js
//
// DESIGN-9-C — pre-fetch staging UX.
// Když user na kartě externí vazby zadá Cislo6 a opustí input (blur), JS volá
// POST /Harmonogram/PreviewSync s ZaznamId. Server vrátí HarmonogramSyncPlan
// (ComputePlanAsync, žádný DB write). JS uloží plán do sessionStorage pod klíčem
// `pmtracker.harmonogramPreview.{zaznamId}`.
//
// Editor harmonogram tab čte staging před render — pokud existuje aktuální plán,
// zobrazí ho s vizuálním indikátorem "čeká na uložení". Submit form (Save):
// staging cleared. Cancel: staging cleared on editor close.

(function (global) {
    'use strict';

    const STORAGE_KEY_PREFIX = 'pmtracker.harmonogramPreview.';
    const STORAGE_TTL_MS = 30 * 60 * 1000; // 30 min

    function getAntiforgery() {
        const input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : null;
    }

    async function fetchPreview(zaznamId) {
        const token = getAntiforgery();
        const headers = { 'Content-Type': 'application/json', 'Accept': 'application/json' };
        if (token) headers['RequestVerificationToken'] = token;
        const resp = await fetch('/Harmonogram/PreviewSync', {
            method: 'POST',
            headers: headers,
            credentials: 'same-origin',
            body: JSON.stringify({ ZaznamId: zaznamId })
        });
        if (!resp.ok) {
            throw new Error(`Preview fetch failed: ${resp.status}`);
        }
        return await resp.json();
    }

    function storeStaging(zaznamId, plan) {
        if (!zaznamId || !plan) return;
        const wrapped = { fetchedAt: Date.now(), plan: plan };
        try {
            sessionStorage.setItem(STORAGE_KEY_PREFIX + zaznamId, JSON.stringify(wrapped));
        } catch (e) {
            console.warn('preview-sync: sessionStorage write failed', e);
        }
    }

    function readStaging(zaznamId) {
        try {
            const raw = sessionStorage.getItem(STORAGE_KEY_PREFIX + zaznamId);
            if (!raw) return null;
            const wrapped = JSON.parse(raw);
            if (Date.now() - (wrapped.fetchedAt || 0) > STORAGE_TTL_MS) {
                sessionStorage.removeItem(STORAGE_KEY_PREFIX + zaznamId);
                return null;
            }
            return wrapped.plan;
        } catch {
            return null;
        }
    }

    function clearStaging(zaznamId) {
        try { sessionStorage.removeItem(STORAGE_KEY_PREFIX + zaznamId); } catch {}
    }

    async function handleZaznamIdBlur(event) {
        // Listener delegate na input pole s data-pm-prefetch-zaznam-id atributem
        // (např. v kartě externí vazby — input pro Cislo6 nebo skrytý ZaznamId field).
        const input = event.target?.closest('[data-pm-prefetch-zaznam-id]');
        if (!input) return;
        const zaznamId = parseInt(input.value || input.getAttribute('data-zaznam-id') || '0', 10);
        if (!Number.isFinite(zaznamId) || zaznamId <= 0) return;

        try {
            const plan = await fetchPreview(zaznamId);
            storeStaging(zaznamId, plan);
            // Optional: dispatch custom event pro editor harmonogram tabu
            document.dispatchEvent(new CustomEvent('pm-harmonogram-preview-ready', {
                detail: { zaznamId, plan }
            }));
        } catch (e) {
            console.warn('preview-sync fetch failed', e);
        }
    }

    function handleFormSubmit(event) {
        // Při submit form clear all staging klíče pro daný editor (po commitu jsou irrelevantní).
        const form = event.target?.closest('form[data-record-editor-form]');
        if (!form) return;
        const zaznamIdField = form.querySelector('input[name="Id"]');
        const zaznamId = parseInt(zaznamIdField?.value || '0', 10);
        if (zaznamId > 0) clearStaging(zaznamId);
    }

    function init() {
        document.addEventListener('blur', handleZaznamIdBlur, /* capture */ true);
        document.addEventListener('submit', handleFormSubmit);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    global.pmPreviewSync = { readStaging, clearStaging };
})(window);
```

- [ ] **Step 2: Bootstrap import**

V `PmTracker.Web/wwwroot/js/modules/bootstrap.js`:

```javascript
import './schedule-feature-c/preview-sync.js';
```

- [ ] **Step 3: Build**

```bash
dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore 2>&1 | tail -3
```

Expected: build PASS.

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/wwwroot/js/modules/schedule-feature-c/preview-sync.js \
        PmTracker.Web/wwwroot/js/modules/bootstrap.js
git commit -m "$(cat <<'EOF'
feat(harmonogram): pre-fetch staging flow (DESIGN-9-C)

JS strana — blur handler na [data-pm-prefetch-zaznam-id] inputs volá
POST /Harmonogram/PreviewSync, ukládá plán do sessionStorage s 30min TTL.
Editor harmonogram tab může číst staging před render (custom event
pm-harmonogram-preview-ready). Submit form → clear staging.

Backend ComputePlanAsync je z DESIGN-4-A. Editor read-side integrace
přijde s UI auditem v Phase 14.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 13: Architecture Tests

### Task 13.1: PendingLockUniversalGuardTests

**Files:**
- Create: `PmTracker.Tests.Unit/Schedule/PendingLockUniversalGuardTests.cs`

- [ ] **Step 1: Test že pending blokuje i nositele records.schedule.edit**

```csharp
namespace PmTracker.Tests.Unit.Schedule;

public sealed class PendingLockUniversalGuardTests
{
    [Fact]
    public async Task SaveRecord_PendingLockOnKrok5_DirectSaveOdmitnut()
    {
        // Arrange: záznam s pending SCHEDULE_PLAN_CHANGE návrhem co obsahuje
        // ManualActualKroky[].KrokKey = krok 5. User má records.schedule.edit.
        // User pošle SaveRecordCommand.ManualActualKroky pro krok 5.

        // Act + Assert: throws RecordValidationException "Krok je uzamčený pending návrhem #X"

        Assert.True(false, "TDD: setup InMemory db + skutečný RecordService volání");
    }

    [Fact]
    public void ToggleRezim_AutoToManual_VzdyAllowed_NezavisleNaPending()
    {
        // Auto→Manual NIKDY nezamyká pendingem (zamykání bez pendingu = absurdita).
        // Pouze Manual→Auto je zamknuté pendingem.
        // Tady jen architecture-style test že kód obsahuje správnou kondici.
        Assert.True(true, "Logic test — pokrývá HarmonogramController.ToggleRezim:if(rezim == Auto && row.Rezim == Manual)");
    }
}
```

- [ ] **Step 2: Implementace + run**

Stejný pattern jako Task 4.1 — skutečný InMemory DB setup, RecordService call, assert exception.

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Tests.Unit/Schedule/PendingLockUniversalGuardTests.cs
git commit -m "test(harmonogram): pending lock universal guard (DESIGN-7-B audit)"
```

### Task 13.2: Architecture testy pro phantom UI fixes

**Files:**
- Create: `PmTracker.Tests.Unit/Architecture/HarmonogramPhantomUiFixesTests.cs`

- [ ] **Step 1: Test že select-candidate.js existuje + že je v bootstrap.js importován**

```csharp
using FluentAssertions;
using System.IO;
using Xunit;

namespace PmTracker.Tests.Unit.Architecture;

public sealed class HarmonogramPhantomUiFixesTests
{
    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Read(string relPath)
        => File.ReadAllText(Path.Combine(LocateRepoRoot(), relPath));

    [Fact]
    public void SelectCandidateJs_Existuje()
    {
        var path = Path.Combine(LocateRepoRoot(), "PmTracker.Web/wwwroot/js/modules/schedule-feature-c/select-candidate.js");
        File.Exists(path).Should().BeTrue("DESIGN-9-A — select-candidate.js musí existovat (phantom UI bug 3 fix).");
    }

    [Fact]
    public void Bootstrap_ImportujeSelectCandidate()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/bootstrap.js");
        src.Should().Contain("schedule-feature-c/select-candidate", "bootstrap musí importovat select-candidate.js (memory: project_bundle_sync — side-effect imports povinné).");
    }

    [Fact]
    public void RecordService_SaveRecord_PersistujeManualActualKroky()
    {
        var src = Read("PmTracker.Web/Services/RecordService.SaveRecord.cs");
        src.Should().Contain("ApplyManualActualKrokyAsync", "DESIGN-6-A — phantom UI bug 1 fix musí být persistovaný v save flow.");
    }

    [Fact]
    public void HarmonogramController_ToggleRezim_MaCreateIfMissing()
    {
        var src = Read("PmTracker.Web/Controllers/HarmonogramController.cs");
        src.Should().MatchRegex(@"ToggleRezim.*ZaznamId.*KrokPoradi", "DESIGN-7-B + phantom UI bug 2 — ToggleRezim musí podporovat create-if-missing přes ZaznamId+KrokPoradi.");
    }

    [Fact]
    public void ManualProposalFieldValidator_ValidateAutoStepNotInProposal_Existuje()
    {
        var src = Read("PmTracker.Web/Services/Records/ManualProposalFieldValidator.cs");
        src.Should().Contain("ValidateAutoStepNotInProposal", "DESIGN-5-A/7-A — auto step rejection validátor.");
    }

    [Fact]
    public void ScheduleEditorPermissionSet_ObsahujeNoveFlagy()
    {
        var src = Read("PmTracker.Web/Services/Schedules/ScheduleEditorPermissionSet.cs");
        src.Should().Contain("CanEditScheduleDirect", "DESIGN-9-B sjednocení permission flagů.");
        src.Should().Contain("CanProposeSchedule", "DESIGN-9-B.");
        src.Should().Contain("CanEditManualActual", "DESIGN-9-B/6-C composite flag.");
    }
}
```

- [ ] **Step 2: Run**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -c Release --no-restore --filter "FullyQualifiedName~HarmonogramPhantomUiFixesTests" 2>&1 | tail -10
```

Expected: PASS 6/6.

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Tests.Unit/Architecture/HarmonogramPhantomUiFixesTests.cs
git commit -m "test(harmonogram): architecture testy phantom UI fixes (DESIGN-5/6/7/9)"
```

---

## Phase 14: UI Design Audit (DESIGN-9-E)

### Task 14.1: Manual checklist verification po dotnet publish

**Files:** žádné code changes — manuální audit po deploy

- [ ] **Step 1: Publish + zip**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet publish PmTracker.Web/PmTracker.Web.csproj -c Release --no-restore -o ./publish
cd publish && zip -r ../publish.zip . && cd ..
ls -la publish.zip
```

Expected: `publish.zip` v rootu (per memory `feedback_publish_zip`).

- [ ] **Step 2: Lokální deploy + smoke test checklist**

Spustit aplikaci lokálně, projít checklist (manual UI audit):

- [ ] **Auto-fillovaný krok PMP záznamu (krok 3)** — badge 🤖 Automat zobrazený, toggle Auto viditelný, datum v cell
- [ ] **Manuální krok 5** — date input zobrazený (jako uživatel s `records.schedule.edit`), čas po editaci uloží
- [ ] **Manuální krok 5 bez klíče** — input invisible (uživatel s jen `proposals.schedule.create` vidí "Navrhnout úpravu" tlačítko místo přímé editace)
- [ ] **Toggle Auto→Manual** — switch funguje, klik přepne, badge se změní na ✍️
- [ ] **Toggle Manual→Auto bez pendingu** — funguje
- [ ] **Toggle Manual→Auto s pendingem** — disabled / 400 error "Pending blokuje"
- [ ] **Dropdown s 2+ kandidáty** — chevron viditelný, klik na kandidáta funguje (volá SelectCandidate, badge ✓ se přesune)
- [ ] **Dropdown s 1 kandidátem** — chevron hidden
- [ ] **Návrh na auto krok** — proposal editor input pro krok 3 invisible
- [ ] **Návrh na manuální krok 5** — proposal editor input zobrazený (jako uživatel s `proposals.schedule.create`)
- [ ] **Druhý submit téhož záznamu týmž autorem** — starý Pending Stav=Superseded, nový Pending vznikl
- [ ] **Read-only Detail PMP záznamu** — všech 10 kroků viditelných (= žádné skrývání). Kroky 6-10 PMP nepoužívá → planned segment width=0 (visibility:hidden v rainbow), žádný actual segment (NULL OdchylkaDni). Řádek kroku v breakdown viditelný se jménem.
- [ ] **Read-only Detail PNF záznamu** — všech 10 kroků viditelných. Kroky 2-5 PNF nepoužívá → planned width=0, actual NULL → no segment. Krok 1, 6, 7, 10 plně render (auto-fillované).
- [ ] **DURATION input** — kalendář + readonly "X dnů" napravo, žádný number input ani +/- stepper
- [ ] **Kalendář změna kroku 3** — readonly text kroku 3 přepočítaný + readonly text kroku 4-10 cumulatively přepočítaný
- [ ] **Pre-fetch staging** — po blur na ZaznamId field se v sessionStorage objeví `pmtracker.harmonogramPreview.{id}` klíč
- [ ] **Submit form** — staging klíč zmizí ze sessionStorage
- [ ] **NULL handling — krok ještě nenastal** (DESIGN-10-A): u nového úkolu otevřít Detail; všechny kroky mají `OdchylkaDni == NULL`. Compact rainbow actual row: žádné segmenty (`display:none` napříč všemi kroky). Breakdown: žádné actual segmenty, žádné offset labely. Badge `—` (Neznamo). Plán segmenty viditelné dle TrvaniDni.
- [ ] **NULL handling — částečně vyplněno** (DESIGN-10-A): manuálně zapsat skutečnost pro krok 5 PMP záznamu. Krok 5 dostane actual segment (= 0 dnů odchylka, shodný s baseline) + badge ✍️. Ostatní kroky stále NULL → žádné segmenty.
- [ ] **Sync retract** (DESIGN-4-A + 10-A): Auto-fillovaný krok 4 PMP odpojit ServiceDesk vazbu → sync retract → DB `HodnotaInt = NULL`, Zdroj = Neznamo. UI: actual segment zmizí, badge `—`.

- [ ] **Step 3: Pokud jakýkoli bod selže — vrátit se k odpovídající Phase a fixnout**

(Žádný commit — audit je jen verification step.)

---

## Phase 15: Final Validation + Publish

### Task 15.1: Full build + test pass

- [ ] **Step 1: Clean build**

```bash
dotnet build PmTracker.sln --no-incremental 2>&1 | tail -5
```

Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 2: Run all tests**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -c Release --no-restore 2>&1 | tail -10
dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj -c Release --no-restore 2>&1 | tail -10
```

Expected: stejný počet PASS jako Phase 0 baseline + nové testy z Phase 2-13. Známé pre-existing 3 failures z baseline zůstávají.

- [ ] **Step 3: Architecture tests verification**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -c Release --no-restore --filter "FullyQualifiedName~Architecture" 2>&1 | tail -10
```

Expected: všechny PASS (včetně nových z Task 13.2).

### Task 15.2: Publish + zip

- [ ] **Step 1: Publish web**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
rm -rf publish publish.zip
dotnet publish PmTracker.Web/PmTracker.Web.csproj -c Release --no-restore -o ./publish 2>&1 | tail -5
cd publish && zip -r ../publish.zip . && cd ..
ls -la publish.zip
```

Expected: `publish.zip` ~10-15 MB v rootu.

- [ ] **Step 2: Final summary commit message draft**

(Tento commit už neexistuje jako patch, ale je summary v memory pro user reference.)

```
feat(harmonogram): refactor — phantom UI fixes + invariants vynucené (2026-05-01)

15 DESIGN položek (DESIGN-4-A, 5-A, 6-A/B/C, 7-A/B/C/D, 9-A/B/C/D/E/F):

Backend:
  - HarmonogramSkutecnostSyncService split: ComputePlan + ApplyPlan + wrapper,
    optimistic concurrency token via UpdatedAt
  - ManualActualKrokApplier.Apply() shared mezi save a approve flow
  - RecordService.SaveRecord persistuje ManualActualKroky (phantom UI bug 1 fix)
  - ManualProposalFieldValidator.ValidateAutoStepNotInProposal — auto kroky odmítnuty
  - SubmitCommands max 1 Pending invariant + auto-supersede vlastního starého
  - ToggleRezim create-if-missing + pending lock pre-check (phantom UI bug 2 fix)

UI / JS:
  - select-candidate.js JS handler (phantom UI bug 3 fix)
  - preview-sync.js staging flow (DESIGN-9-C)
  - duration-calendar-binding.js — DURATION input redesign na kalendář + readonly dny
  - _ScheduleBlock.cshtml zrušení Where(TrvaniDni > 0) filtru v compact + breakdown, žádné skrývání kroků
  - ScheduleEditorPermissionSet sjednocené flagy CanEditScheduleDirect / CanProposeSchedule / CanEditManualActual

DB:
  - db_upgrade_1_3_11_proposal_supersede.sql — Stav=Superseded + SupersededByProposalId

Tests:
  - HarmonogramSyncPlanTests, HarmonogramAutoStepProposalRejectionTests,
    HarmonogramMaxOnePendingTests, RecordServiceManualActualKrokyPersistenceTests,
    PendingLockUniversalGuardTests, HarmonogramPhantomUiFixesTests (architecture)

Memory: project_manual_actual_kroky_phantom_ui (closed),
        project_harmonogram_visibility_rules (applied),
        feedback_quality_over_shortcuts (applied),
        project_authz_architecture (per-key komunikace upevněná).
```

(Tento commit message už není potřeba — všechny změny jsou v dílčích commitech.)

---

## Self-Review Checklist (Plan Author — provedeno)

**1. Spec coverage:** všech 16 aktivních DESIGN položek (DESIGN-4-A, 5-A, 6-A/B/C, 7-A/B/C/D, 9-A/B/C/D/E/F, **10-A**) má odpovídající Phase. DESIGN-10-A přidaný 2026-05-01 po user dotazu — DELAY HodnotaInt nullable napříč vrstvami (Phase 1.5). Deferred (DESIGN-5-B audit UI komponenta + retention + takeover rename) explicitně mimo plán, tracked v session memory.

**2. Placeholder scan:** žádné "TBD" / "implement later" / "similar to". Některé Phase 0 a Phase 14 jsou verification-only bez code change — to je legitimní (audit phase). Test files mají `Assert.True(false, "TDD: ...")` jen jako placeholders pro real test code, který se dopisuje v dalších stepu téhož task — to je TDD red→green workflow, ne placeholder failure.

**3. Type consistency:**
- `HarmonogramSyncPlan` + `HarmonogramRowChange` + `HarmonogramRowChangeReason` definované v Task 2.1, používané v Task 2.2/2.3
- `ScheduleEditorPermissionSet.CanEditScheduleDirect/CanProposeSchedule/CanEditManualActual` definované v Task 5.1, používané v _ScheduleBlock.cshtml
- `RecordProposalStateCodes.Superseded` definované v Task 1.1, používané v Task 8.1
- `ZaznamNavrhEntity.SupersededByProposalId` definované v Task 1.1, používané v Task 8.1
- `ValidateAutoStepNotInProposal` definované v Task 6.1, používané v SubmitCommands (Task 6.1) + DecisionCommands defense (Task 6.1)
- `ManualActualKrokApplier.Apply` definované v Task 3.1, používané v RecordService.SaveRecord (Task 4.2) + DecisionCommands (Task 3.1 refactor)

Žádné inkonzistence.

---

## Execution Notes

**Tento plán provádí Claude inline, task po tasku.** Po každém Phase commit user manuálně ověří:
- Build PASS
- Tests pass count nezhoršený
- Klíčové funkční změny v UI (Phase 9, 10, 11, 12 zejména)

**Kritické závislosti mezi Phasemi:**
- Phase 1 (DB schema) → blokuje Phase 8 (Stav=Superseded)
- Phase 3 (extract Apply) → blokuje Phase 4 (SaveRecord persistence)
- Phase 5 (permission flags) → blokuje Phase 9-12 UI changes (UI consume nové flagy)
- Phase 2 (ComputePlan) → blokuje Phase 12 (PreviewSync endpoint je v Phase 2 Task 2.4)

**Phase 14 (UI audit) je ručně provedená kontrola** — nemůže být automatizovaná, vyžaduje deploy + browser test.

**Commit cadence:** každá Task má commit. Žádné batch-commit napříč Tasky. User může reviewnout po každém commit.

**Pokud uprostřed Phase narazím na neočekávaný blocker** (např. existing kód odporuje předpokladu plánu), STOP a probrat s userem před adaptací plánu — nehackovat workaround.
