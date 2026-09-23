# Authz úklid: oprava hledání kandidátů + smazání schedule.preview + zapojení projects.read.all — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Opravit 403 při hledání osob pro projektové role (variantou „nedůvěřuj klientovi"), smazat mrtvý klíč `schedule.preview` a zapojit `projects.read.all` do read-grant sady.

**Architecture:** (1) `SearchProjectMemberCandidates` přejde z deklarativní policy (rozbité — `id` v route ≠ `projektId` klíč, který čte handler) na **imperativní body-check** `HasPermission(TeamCandidatesSearch, id)` — zavedený vzor „H-1 IDOR fix" z allowlistu `AuthorizationPolicyEnforcementTests`; URL ani view se nemění. (2) `schedule.preview` hlídal `ScheduleController.Recalc` smazaný ve Fázi 7a harmonogram migrace (live-preview je dnes client-side v block.js) — smaže se z kódu, seedu, testů, wiki a přes SQL skript z prod DB. (3) `ProjectsReadAll` se přidá do `ProjectReadGrantKeys`, čímž klíč začne dělat to, co jméno slibuje.

**Tech Stack:** ASP.NET Core MVC, EF Core, SQL Server. Testy: xUnit + FluentAssertions; Api testy přes `ApiSqlFixture` (reálná SQL DB, impersonace `?asUser={osobaId}`).

## Global Constraints

- **`SearchProjectMemberCandidates` je `[HttpGet]`** → architektonický test `AuthorizationPolicyEnforcementTests` pokrývá jen mutující akce (POST/PUT/DELETE); odebrání policy atributu z GET nic neporuší. Class-level `[Authorize]` na `ProjektyController` dál vynucuje autentizaci.
- **Body-check pattern:** imperativní `CurrentUserContext.HasPermission(klíč, projektId)` je zavedená konvence pro akce, kde projektId nejde route klíčem `projektId` (viz allowlist komentář „H-1 IDOR fix" v `AuthorizationPolicyEnforcementTests.cs:71-81`). `PolicyAttributeMigrationTests` hlídá jen ExportController/ProjectDashboard/ZaznamyCommands/Search — TabPartials se netýká.
- **Seeder nikdy nemaže** — odebrání klíče z `PermissionSeedConfiguration` NEodstraní řádky z prod DB; nutný ruční SQL skript (offline deployment, vzor `db_upgrade_1_3_8_authz_per_action_redesign.sql`). Další volné číslo: **1_4_1** (poslední je `db_upgrade_1_4_0_harmonogram_datum_model.sql`).
- **DB tabulky/sloupce (ověřeno v `AuthorizationEntityConfiguration.cs`):** `authz.permissions(id, klic, category_id, …)`, `authz.role_permissions(id, role_id, permission_id, …)`, `authz.role_permission_projects(role_permission_id, …)`, `authz.permission_categories(id, kod, …)`.
- **Kategorie `SCHEDULE`** má jediný klíč (`schedule.preview`) → maže se i kategorie (config + DB, s guardem na cizí reference).
- **Baseline:** dle `memory/project_pre_existing_test_failures.md` existují orthogonální pre-existing selhání (F4 authz / meetings / SD) — neřešit, porovnávat proti baseline.
- **Route hledání:** `/Projekty/SearchProjectMemberCandidates/{id}?q=…` (default pattern `{controller}/{action}/{id?}`); assertované v `ProjectsModalsControllerTests` na 3 místech — **beze změny** (fix nemění URL).

---

### Task 1: Oprava hledání kandidátů — imperativní per-projekt check (varianta 2)

Bug: `[Authorize(Policy = "permission:team.candidates.search")]` + `id` v route → handler nenajde `projektId` → globální check → per-projektové role (VLASTNIK_PROJEKTU, ADM_PROJ, PROJ_MAN) dostanou 403 v person-pickeru team modalů.

**Files:**
- Modify: `PmTracker.Tests.Api/TestInfrastructure/ApiSqlFixture.cs` (nový helper na konec třídy)
- Modify: `PmTracker.Web/Controllers/ProjektyController.TabPartials.cs:40-54`
- Test: `PmTracker.Tests.Api/Controllers/ProjectsModalsControllerTests.cs` (2 nové `[Fact]`)

**Interfaces:**
- Consumes: `IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync` vzor (CiselnikRoliProjektu dle Kod → ObsazeniProjektu); `ProjectRoleCodes.ProjectOwner = "VLASTNIK_PROJEKTU"` (namespace `PmTracker.Web.Models.ViewModels`); `_fixture.EnsureSubsystemLeadAsync(projectId, subsystemId, osobaId)`; `PermissionKeys.TeamCandidatesSearch`.
- Produces: `ApiSqlFixture.EnsureProjectRoleAssignmentAsync(int projectId, int osobaId, string roleCode)` — používá ho Task 1 test; akce `SearchProjectMemberCandidates` bez policy atributu s body-checkem.

- [ ] **Step 1: Přidat fixture helper pro přiřazení projektové role dle kódu**

Do `ApiSqlFixture.cs` za metodu `EnsureProjectTeamMemberAsync` (konec třídy):

```csharp
    /// <summary>
    /// Authz úklid (2026-07-14): přiřadí osobě konkrétní projektovou roli dle Kod
    /// (CiselnikRoliProjektu → ObsazeniProjektu). Na rozdíl od EnsureProjectTeamMemberAsync
    /// (první role v číselníku) je deterministická — pro testy per-projektových authz grantů.
    /// Vzor: IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync.
    /// </summary>
    public async Task EnsureProjectRoleAssignmentAsync(int projectId, int osobaId, string roleCode)
    {
        await using var dbContext = CreateDbContext();

        var roleId = await dbContext.CiselnikRoliProjektu
            .Where(x => x.Kod == roleCode)
            .Select(x => x.Id)
            .FirstAsync();

        var exists = await dbContext.ObsazeniProjektu.AnyAsync(x =>
            x.ProjektId == projectId &&
            x.OsobaId == osobaId &&
            x.RoleId == roleId &&
            x.DatumOdebrani == null);

        if (exists)
        {
            return;
        }

        dbContext.ObsazeniProjektu.Add(new ObsazeniProjektuEntity
        {
            ProjektId = projectId,
            OsobaId = osobaId,
            RoleId = roleId,
            DatumPrirazeni = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }
```

- [ ] **Step 2: Napsat failing test (vlastník projektu smí hledat) + guard test (subsystémový vedoucí ne)**

Do `ProjectsModalsControllerTests.cs` (na konec třídy, před závěrečnou `}`):

```csharp
    [Fact]
    public async Task SearchProjectMemberCandidates_ShouldSucceed_ForProjectScopedRole()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiCandSearchOwner");
        var projectId = await _fixture.EnsureProjectAsync("APICANDSRCH");
        await _fixture.EnsureProjectRoleAssignmentAsync(
            projectId, ownerId, PmTracker.Web.Models.ViewModels.ProjectRoleCodes.ProjectOwner);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(
            $"/Projekty/SearchProjectMemberCandidates/{projectId}?q=jan&asUser={ownerId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "VLASTNIK_PROJEKTU drží team.candidates.search per-projekt — nesmí dostat 403 z globálního checku");
    }

    [Fact]
    public async Task SearchProjectMemberCandidates_ShouldReturnForbidden_ForSubsystemLeadWithoutTeamKey()
    {
        var leadId = await _fixture.EnsurePersonAsync("ApiCandSearchLead");
        var projectId = await _fixture.EnsureProjectAsync("APICANDLEAD");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APICANDLEADS", leadId);
        await _fixture.EnsureSubsystemLeadAsync(projectId, subsystemId, leadId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(
            $"/Projekty/SearchProjectMemberCandidates/{projectId}?q=jan&asUser={leadId}");

        // Vedoucí subsystému projekt číst smí (CanAccessProject), ale team.candidates.search nedrží.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
```

- [ ] **Step 3: Spustit testy — první musí selhat (403), druhý projde (guard)**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ProjectsModalsControllerTests.SearchProjectMemberCandidates"`
Expected: `ShouldSucceed_ForProjectScopedRole` FAIL (dnes 403 z policy global-checku); `ShouldReturnForbidden_ForSubsystemLeadWithoutTeamKey` PASS.

- [ ] **Step 4: Implementovat fix — odebrat policy atribut, přidat body-check**

V `ProjektyController.TabPartials.cs` nahradit:

```csharp
    [HttpGet]
    [Authorize(Policy = "permission:team.candidates.search")]
    public async Task<IActionResult> SearchProjectMemberCandidates(int id, [FromQuery(Name = "q")] string? query, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(id))
        {
            return NotFound();
        }

        var results = await _projectService.SearchProjectMemberCandidatesAsync(query ?? string.Empty, ct);
```

za:

```csharp
    [HttpGet]
    public async Task<IActionResult> SearchProjectMemberCandidates(int id, [FromQuery(Name = "q")] string? query, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(id))
        {
            return NotFound();
        }

        // Authz úklid 2026-07-14: dříve [Authorize(Policy="permission:team.candidates.search")],
        // ale route nese "id" (ne "projektId") → PermissionAuthorizationHandler degradoval na
        // global-only check a 403 pro per-projektové role (VLASTNIK/ADM_PROJ/PROJ_MAN).
        // Body-check proti témuž id, které akce používá — vzor H-1 IDOR fix
        // (viz allowlist v AuthorizationPolicyEnforcementTests).
        if (!CurrentUserContext.HasPermission(PermissionKeys.TeamCandidatesSearch, id))
        {
            return Forbid();
        }

        var results = await _projectService.SearchProjectMemberCandidatesAsync(query ?? string.Empty, ct);
```

Poznámka: `using Microsoft.AspNetCore.Authorization;` v souboru zůstává (žádný jiný atribut tam není — pokud po odebrání kompilátor hlásí unused using, smazat ho).

- [ ] **Step 5: Spustit testy — oba musí projít + okolní sady**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ProjectsModalsControllerTests"`
Expected: PASS (včetně 3 existujících assertů na search-url — URL se neměnila).
Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ProjektyControllerBehaviorTests|FullyQualifiedName~ProjektyControllerTeamAuthzTests|FullyQualifiedName~AuthorizationPolicyEnforcementTests|FullyQualifiedName~PolicyAttributeMigrationTests"`
Expected: PASS (behavior test staví superadmin context → body-check projde).

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Controllers/ProjektyController.TabPartials.cs PmTracker.Tests.Api/TestInfrastructure/ApiSqlFixture.cs PmTracker.Tests.Api/Controllers/ProjectsModalsControllerTests.cs
git commit -m "fix(authz): hledání kandidátů týmu funguje per-projektovým rolím (body-check místo rozbité policy)"
```

---

### Task 2: Smazání mrtvého klíče `schedule.preview` (kód + seed + testy + wiki + SQL)

Klíč hlídal `ScheduleController.Recalc` (stateless přepočet harmonogramu), smazaný ve Fázi 7a harmonogram datum-model migrace (`52d6607`). Live-preview je dnes client-side (block.js); serverové endpointy kontrolují `records.schedule.edit`. Všech 7 rolí, které klíč drží, má jiné read-grant klíče → žádná změna viditelnosti projektů.

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/SecurityViewModels.cs` (const :129, definice :243, `ProjectReadGrantKeys` :318)
- Modify: `PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs` (kategorie SCHEDULE :91, definice :223, 7 role řádků, počty v komentářích)
- Modify: `PmTracker.Tests.Unit/Authorization/ProjectRolePermissionMatrixTests.cs:41`, `PmTracker.Tests.Unit/Authorization/SubsystemRolePermissionMatrixTests.cs:31`, `PmTracker.Tests.Unit/Authorization/ResolverSwapSafetyTests.cs:160`
- Modify: `PmTracker.Tests.Unit/Architecture/AuthorizationPolicyEnforcementTests.cs:152-157` (stale allowlist záznam smazaného `ScheduleController.Recalc`)
- Modify: `docs/wiki/nastaveni-administrace/akce-permissions/index.md:179` (řádek tabulky)
- Create: `db_upgrade_1_4_1_drop_schedule_preview.sql` (root repa)

**Interfaces:**
- Consumes: seed record `RoleActionSeedItem(RoleKod, ActionKlic, ScopeMode, IsAllowed)`; kategorie record tvar `new("SCHEDULE", "Harmonogram preview", 85)`.
- Produces: katalog bez `schedule.preview`/`SchedulePreview`/kategorie SCHEDULE; SQL skript pro prod.

- [ ] **Step 1: RED — odebrat klíč z očekávaných polí maticových testů**

`SubsystemRolePermissionMatrixTests.cs` — z pole `SubsystemLeadTargetKeys` smazat řádek:

```csharp
        "schedule.preview",
```

`ProjectRolePermissionMatrixTests.cs` — z pole `ProjectExecutiveTargetKeys` (řádek 41) smazat:

```csharp
        "schedule.preview",
```

`ResolverSwapSafetyTests.cs` — z pole klíčů (řádek 160) smazat:

```csharp
        "schedule.preview",
```

- [ ] **Step 2: Spustit — musí selhat (seed klíč pořád má)**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~SubsystemRolePermissionMatrixTests|FullyQualifiedName~ProjectRolePermissionMatrixTests|FullyQualifiedName~ResolverSwapSafetyTests"`
Expected: FAIL (očekávaná pole už klíč nemají, `RoleMappings` ano).

- [ ] **Step 3: Odebrat klíč ze seedu (7 řádků + definice + kategorie + počty)**

V `PermissionSeedConfiguration.cs` smazat těchto 7 řádků (každý je unikátní):

```csharp
        new("SUPERADMIN", "schedule.preview", ScopeMode.All, true),
        new("APP_ADMIN", "schedule.preview", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "schedule.preview", ScopeMode.All, true),
        new("ADM_PROJ", "schedule.preview", ScopeMode.All, true),
        new("PROJ_MAN", "schedule.preview", ScopeMode.All, true),
        new("VEDOUCI_SUBSYSTEMU", "schedule.preview", ScopeMode.All, true),
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "schedule.preview", ScopeMode.All, true),
```

Smazat definici (řádek 223) — POZOR: je to poslední položka pole, předchozímu řádku smazat čárku:

```csharp
        new("schedule.preview", "Náhledový přepočet harmonogramu", "SCHEDULE", PermissionScopeLevel.Project)
```

Smazat kategorii (řádek 91):

```csharp
        new("SCHEDULE", "Harmonogram preview", 85),
```

Upravit počty v komentářích bloků:
- `// --- VEDOUCI_SUBSYSTEMU (20 klíčů — subsystémový lead + export/tisk) ---` → `(19 klíčů`
- `// --- ZASTUPCE_VEDOUCIHO_SUBSYSTEMU (20 klíčů — = VEDOUCI) ---` → `(19 klíčů`
- `// --- VLASTNIK_PROJEKTU (59 klíčů — …) ---` → `(58 klíčů`
- `// --- ADM_PROJ (59 klíčů — …) ---` → `(58 klíčů`
- `// --- PROJ_MAN (59 klíčů — …) ---` → `(58 klíčů`

Ověřit grep: `grep -n "schedule.preview" PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs` → prázdné.

- [ ] **Step 4: Odebrat z SecurityViewModels.cs (const + definice + read-grant sada)**

Smazat konstantu (řádek 129):

```csharp
    public const string SchedulePreview = "schedule.preview";
```

Smazat definici (řádek 243):

```csharp
        new(SchedulePreview, "Náhledový přepočet harmonogramu", "SCHEDULE", "PROJECT", "Stateless kalkulace pro editor úkolu."),
```

Z `ProjectReadGrantKeys` smazat poslední položku (řádek 318) — předchozímu řádku (`ExportWordUkol,`) smazat čárku:

```csharp
        SchedulePreview
```

Ověřit grep: `grep -rn "SchedulePreview" --include="*.cs" PmTracker.Web` → prázdné.

- [ ] **Step 5: Smazat stale allowlist záznam + wiki řádek**

V `AuthorizationPolicyEnforcementTests.cs` smazat blok (ScheduleController smazán ve Fázi 7a — FQN nikdy nematchne):

```csharp
            // ---- ScheduleController ----

            // Recalc: kalkulační endpoint pro preview harmonogramu (stateless výpočet).
            // Nepotřebuje per-project autorizaci — pracuje jen se vstupy bez DB operace.
            // Třídy kontroleru nemá [Authorize], žádná policy nemůže být aplikována.
            "PmTracker.Web.Controllers.ScheduleController.Recalc",
```

V `docs/wiki/nastaveni-administrace/akce-permissions/index.md` smazat řádek tabulky:

```markdown
| `schedule.preview` | PROJECT | Stateless kalkulace pro editor úkolu |
```

- [ ] **Step 6: Build + spustit — musí projít**

Run: `dotnet build PmTracker.Web` → 0 errors (kdyby kompilátor našel zapomenutou referenci `SchedulePreview`, spadne tady).
Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~Authorization"`
Expected: PASS (celá authz sada, včetně upravených matic).

- [ ] **Step 7: Vytvořit SQL skript pro prod DB**

Create `db_upgrade_1_4_1_drop_schedule_preview.sql`:

```sql
-- =============================================================================
-- db_upgrade_1_4_1_drop_schedule_preview.sql
--
-- Authz úklid 2026-07-14 — smaže z authz.* tabulek mrtvý klíč `schedule.preview`
-- a jeho (jednoúčelovou) kategorii SCHEDULE.
--
-- KONTEXT: Klíč hlídal ScheduleController.Recalc (stateless přepočet harmonogramu),
-- který byl smazán při harmonogram datum-model migraci (Fáze 7a, commit 52d6607).
-- Live-preview editoru je client-side (block.js); serverové schedule endpointy
-- (HarmonogramController.SelectCandidate/PreviewSync) kontrolují records.schedule.edit.
-- Klíč není referencován žádným [Authorize(Policy)] ani HasPermission checkem.
--
-- ROZSAH MIGRACE:
--   * Smaže záznamy POUZE z:
--       - authz.role_permission_projects  (scope INCLUDE vazby)
--       - authz.role_permissions          (vazby role -> permission)
--       - authz.permissions               (katalog: klic = 'schedule.preview')
--       - authz.permission_categories     (kod = 'SCHEDULE', jen pokud osiřelá)
--   * NEDOTKNE se: authz.user_roles, authz.roles, žádných business tabulek.
--
-- Idempotence: skript lze spustit opakovaně.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

PRINT N'[1.4.1] Drop schedule.preview — start';

-- Audit pre-state: které role klíč drží (očekávané: 7 seed rolí; případné CUSTOM
-- role zkontrolovat ručně — po smazání ztratí tento grant, což je záměr, klíč nic nehlídá).
SELECT r.kod AS role_kod, p.klic
FROM authz.role_permissions rp
INNER JOIN authz.roles r ON r.id = rp.role_id
INNER JOIN authz.permissions p ON p.id = rp.permission_id
WHERE p.klic = N'schedule.preview';

-- 1) role_permission_projects — první kvůli FK na role_permissions.
DELETE rpp
FROM authz.role_permission_projects rpp
INNER JOIN authz.role_permissions rp ON rp.id = rpp.role_permission_id
INNER JOIN authz.permissions p ON p.id = rp.permission_id
WHERE p.klic = N'schedule.preview';

PRINT N'[1.4.1] Smazáno z authz.role_permission_projects: ' + CAST(@@ROWCOUNT AS NVARCHAR(16));

-- 2) role_permissions.
DELETE rp
FROM authz.role_permissions rp
INNER JOIN authz.permissions p ON p.id = rp.permission_id
WHERE p.klic = N'schedule.preview';

PRINT N'[1.4.1] Smazáno z authz.role_permissions: ' + CAST(@@ROWCOUNT AS NVARCHAR(16));

-- 3) permissions.
DELETE FROM authz.permissions WHERE klic = N'schedule.preview';

PRINT N'[1.4.1] Smazáno z authz.permissions: ' + CAST(@@ROWCOUNT AS NVARCHAR(16));

-- 4) Kategorie SCHEDULE — jen pokud na ni už nic neukazuje (guard proti cizím klíčům).
DELETE c
FROM authz.permission_categories c
WHERE c.kod = N'SCHEDULE'
  AND NOT EXISTS (SELECT 1 FROM authz.permissions p WHERE p.category_id = c.id);

PRINT N'[1.4.1] Smazáno z authz.permission_categories: ' + CAST(@@ROWCOUNT AS NVARCHAR(16));

-- Sanity check.
IF EXISTS (SELECT 1 FROM authz.permissions WHERE klic = N'schedule.preview')
BEGIN
    ROLLBACK TRANSACTION;
    RAISERROR(N'[1.4.1] KONZISTENCE: schedule.preview stále existuje — migrace přerušena.', 16, 1);
    RETURN;
END

PRINT N'[1.4.1] Drop schedule.preview — dokončeno.';

COMMIT TRANSACTION;

-- ROLLBACK: nejprve vrátit C# kód (revert commitu, který klíč odstranil z
-- PermissionSeedConfiguration) — PermissionSeeder pak klíč i granty při startu
-- aplikace znovu doseeduje (idempotentní UPSERT). Samotný INSERT zpět bez C#
-- reverту nedává smysl.
```

- [ ] **Step 8: Zapsat skript do db_check přehledu (pokud existuje)**

Run: `grep -n "1_4_0" db_check_applied_upgrades.sql 2>/dev/null || echo "SKIP — přehledový skript neexistuje nebo neregistruje verze"`
Pokud `db_check_applied_upgrades.sql` obsahuje seznam verzí, přidat řádek pro `1_4_1` stejným vzorem jako `1_4_0`. Pokud ne, krok přeskočit.

- [ ] **Step 9: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/SecurityViewModels.cs PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs PmTracker.Tests.Unit/Authorization/ProjectRolePermissionMatrixTests.cs PmTracker.Tests.Unit/Authorization/SubsystemRolePermissionMatrixTests.cs PmTracker.Tests.Unit/Authorization/ResolverSwapSafetyTests.cs PmTracker.Tests.Unit/Architecture/AuthorizationPolicyEnforcementTests.cs docs/wiki/nastaveni-administrace/akce-permissions/index.md db_upgrade_1_4_1_drop_schedule_preview.sql
git commit -m "refactor(authz): smazat mrtvý klíč schedule.preview (endpoint zanikl ve Fázi 7a)"
```

(Pokud Step 8 upravil `db_check_applied_upgrades.sql`, přidat ho do `git add`.)

---

### Task 3: Zapojit `projects.read.all` do read-grant sady

Klíč je sémanticky kanonický pro „čtení všech projektů" (drží ho READ_ALL/SUPERADMIN/APP_ADMIN), ale nikdy nebyl zapojen do `ProjectReadGrantKeys` → custom role jen s tímto klíčem by neviděla nic. READ_ALL dnes funguje jen díky dashboard/export klíčům.

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/SecurityViewModels.cs` (`ProjectReadGrantKeys`, začátek pole)
- Test: `PmTracker.Tests.Unit/Security/CurrentUserContextPermissionTests.cs` (nový `[Fact]`)

**Interfaces:**
- Consumes: `BuildUser(bool isSuperAdmin, IReadOnlyList<int>? visibleProjectIds = null, IReadOnlyList<int>? deletedProjectIds = null, params PermissionGrantViewModel[] grants)` helper v témže test souboru; `PermissionKeys.ProjectsReadAll = "projects.read.all"`.
- Produces: `PermissionKeys.GrantsProjectRead("projects.read.all") == true`.

- [ ] **Step 1: Napsat failing test**

Do `CurrentUserContextPermissionTests.cs` za test `CanAccessProject_ShouldIgnoreProjectsCreateGrant`:

```csharp
    [Fact]
    public void CanAccessProject_ShouldGrantAllProjects_ForGlobalProjectsReadAll()
    {
        // projects.read.all je kanonický klíč management visibility (role READ_ALL).
        // Musí být v ProjectReadGrantKeys — jinak custom role jen s tímto klíčem nevidí nic
        // a READ_ALL funguje pouze díky vedlejším dashboard/export klíčům.
        var user = BuildUser(
            isSuperAdmin: false,
            grants: new PermissionGrantViewModel
            {
                PermissionKey = PermissionKeys.ProjectsReadAll,
                ScopeLevel = "GLOBAL",
                ScopeMode = "ALL",
                IsAllowed = true,
                ProjectIds = Array.Empty<int>()
            });

        user.CanAccessProject(3).Should().BeTrue();
        user.CanAccessProject(999).Should().BeTrue();
        PermissionKeys.GrantsProjectRead(PermissionKeys.ProjectsReadAll).Should().BeTrue();
    }
```

- [ ] **Step 2: Spustit — musí selhat**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~CurrentUserContextPermissionTests.CanAccessProject_ShouldGrantAllProjects_ForGlobalProjectsReadAll"`
Expected: FAIL — `CanAccessProject(3)` vrací false (klíč není v `ProjectReadGrantKeys`).

- [ ] **Step 3: Přidat klíč do ProjectReadGrantKeys**

V `SecurityViewModels.cs` v poli `ProjectReadGrantKeys` nahradit:

```csharp
        // Nový model — read-grantující klíče (pokud osoba má kterýkoli z nich,
        // smí vidět projekt).
        ProjectsEdit,
        ProjectsDelete,
```

za:

```csharp
        // Nový model — read-grantující klíče (pokud osoba má kterýkoli z nich,
        // smí vidět projekt).
        ProjectsReadAll,
        ProjectsEdit,
        ProjectsDelete,
```

- [ ] **Step 4: Spustit — musí projít + celá authz/security sada**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~CurrentUserContextPermissionTests|FullyQualifiedName~Authorization"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/SecurityViewModels.cs PmTracker.Tests.Unit/Security/CurrentUserContextPermissionTests.cs
git commit -m "fix(authz): projects.read.all reálně grantuje čtení všech projektů (zapojení do read-grant sady)"
```

---

### Task 4: Závěrečné regresní ověření

**Files:** žádné změny (jen ověření; případné srovnání počtových testů).

- [ ] **Step 1: Build celé solution**

Run: `dotnet build`
Expected: 0 errors.

- [ ] **Step 2: Celé Unit sady authz + security + architecture**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~Authorization|FullyQualifiedName~Security|FullyQualifiedName~Architecture"`
Expected: PASS. Pokud selže test počítající celkový počet klíčů/mapování (o −1 definici / −7 mapování z Task 2), srovnat očekávané počty; NEupravovat sémantické asserty.

- [ ] **Step 3: Dotčené Api sady**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ProjectsModalsControllerTests|FullyQualifiedName~ExportControllerTests|FullyQualifiedName~SettingsAuthzAdminControllerTests"`
Expected: PASS.

- [ ] **Step 4: Porovnat zbylá selhání proti baseline**

Jakékoli červené testy porovnat s `memory/project_pre_existing_test_failures.md` (F4 authz / meetings / SD) — pre-existing nechat, nové vyšetřit před dokončením.

- [ ] **Step 5: Commit (jen pokud Step 2 vyžadoval srovnání počtů)**

```bash
git add PmTracker.Tests.Unit/
git commit -m "test(authz): srovnat počty po odstranění schedule.preview"
```

---

## Self-Review

**Spec coverage:** oprava hledání variantou 2 (nedůvěřuj klientovi, body-check) → Task 1 ✓ · smazání schedule.preview vč. prod DB → Task 2 ✓ · zapojení projects.read.all → Task 3 ✓ · latentní rizika a výzvy vědomě mimo scope (rozhodnutí uživatele) ✓.

**Placeholder scan:** žádné TBD/TODO; každý krok má konkrétní kód/příkaz/expected. Step 8 Task 2 má explicitní SKIP větev, ne placeholder. ✓

**Type consistency:** `EnsureProjectRoleAssignmentAsync(int, int, string)` definován v Task 1 Step 1 a použit v Step 2 · `ProjectRoleCodes.ProjectOwner = "VLASTNIK_PROJEKTU"` ověřen v `RoleCatalogKeys.cs` · `BuildUser` signatura ověřena proti `CurrentUserContextPermissionTests.cs:347` · SQL sloupce (`klic`, `category_id`, `kod`, `role_permission_id`) ověřeny proti `AuthorizationEntityConfiguration.cs` a vzoru 1_3_8 · `PermissionKeys.TeamCandidatesSearch` existuje (používán v seedu i testech). ✓
