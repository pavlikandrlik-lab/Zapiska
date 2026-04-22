# Authorization Unification — Fáze C, D, E+F Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dokončit autorizační refactor: doplnit chybějící permission keys + `READ_ALL` roli, zavřít 4 known security holes, zavést `IAuthorizationService` + `AuthorizationSnapshot`, zkonsolidovat ~60 ad-hoc auth checků do jediné fasády, zredukovat admin UI v Nastavení na read-only přehled + přiřazení globálních rolí.

**Architecture:**
- Fáze C: nové keys v seed + `READ_ALL` + opravit 4 bezpečnostní holes v controllerech + cleanup DB migrace
- Fáze D: `AuthorizationSnapshot` record + `IAuthorizationService` fasáda + ASP.NET Core policy handler + refactor 25 controllerů + 14 services na jedinou auth cestu
- Fáze E+F: smazat editační Nastavení UI + architecture testy (seed jediný zdroj pravdy) + docs

**Tech Stack:** ASP.NET Core 8, EF Core 8 (enumy + HasConversion z Fáze B0), ASP.NET Core Authorization (IAuthorizationHandler + policies), xUnit + FluentAssertions, SQL Server.

**Návaznost:**
- Fáze A ✅ (commity `da5e271` → `beaa1e4`): DB schéma + seed
- Fáze B0 ✅ (commity `2a09581` → `c8806ef`): enumy RoleScope / ScopeMode / PermissionScopeLevel
- Fáze B 🔴 detailní plán: [2026-04-22-authz-phase-b-resolver-swap.md](2026-04-22-authz-phase-b-resolver-swap.md) — resolver swap (4 tasky)
- Tento dokument: Fáze C (7 tasků), Fáze D (8 tasků), Fáze E+F (7 tasků)

---

## Master file impact matrix

### 🟢 Nové soubory (celkem ~22)

| Soubor | Fáze | Účel |
|---|---|---|
| `PmTracker.Web/Services/Security/AuthorizationSnapshot.cs` | D | In-memory record s `HasPermission()` |
| `PmTracker.Web/Services/Security/IAuthorizationService.cs` | D | Kanonická fasáda |
| `PmTracker.Web/Services/Security/AuthorizationService.cs` | D | Implementace fasády |
| `PmTracker.Web/Services/Security/AuthorizationSnapshotBuilder.cs` | D | Build snapshot z DB |
| `PmTracker.Web/Services/Security/PermissionAuthorizationHandler.cs` | D | Policy handler pro `[Authorize(Policy="...")]` |
| `PmTracker.Web/Services/Security/PermissionRequirement.cs` | D | Policy requirement |
| `PmTracker.Web/Extensions/AuthorizationPolicyExtensions.cs` | D | DI wiring + policy registrace |
| `PmTracker.Web/Services/Security/ForbiddenException.cs` | D | Exception pro service-layer guard |
| `db_upgrade_1_3_0_cleanup_orphaned_role_permissions.sql` | C | Cleanup DB |
| `docs/authorization.md` | F | Kompletní dokumentace |
| ~12 test souborů (authz, policy, architecture) | C, D, F | TDD coverage |

### 🟡 Upravené soubory (celkem ~60)

| Kategorie | Počet | Fáze |
|---|---|---|
| Controllery (Authorize policy attribute) | 25 | D |
| Services (nahradit IsSuperAdmin/Grants za IAuthorizationService) | 14 | D |
| PermissionKeys.cs | 1 | C |
| PermissionSeedConfiguration.cs | 1 | C |
| UserContextResolver.cs | 1 | D |
| UserAuthorizationSnapshotBuilder.cs | 1 | D (přepsat na snapshot) |
| SettingsAuthzQueries.cs + Commands.cs | 2 | E+F (editační metody pryč) |
| Views Nastaveni | 3 | E+F (Index, _DetailPanel, UserRolesModal) |
| SqlStartupValidatorHostedService.cs | 1 | C |
| Tests existing | ~15 | všechny fáze |

### 🔴 Smazané soubory (celkem ~10)

| Soubor | Fáze |
|---|---|
| `PmTracker.Web/Services/Common/ProjectRolePermissionGrantBuilder.cs` | B |
| `PmTracker.Web/Services/Common/SubsystemRolePermissionGrantBuilder.cs` | B |
| `PmTracker.Web/Views/Nastaveni/PermissionModal.cshtml` | E+F |
| `PmTracker.Web/Views/Nastaveni/RoleModal.cshtml` | E+F |
| `PmTracker.Web/Views/Nastaveni/RolePermissionModal.cshtml` | E+F |
| `PmTracker.Tests.Unit/Common/ProjectRolePermissionGrantBuilderTests.cs` | B |
| `PmTracker.Tests.Unit/Common/SubsystemRolePermissionGrantBuilderTests.cs` | B |
| Unused view models (PermissionModalViewModel, RoleModalViewModel, apod.) | E+F |

---

# Fáze C — Gap fixes: nové keys + READ_ALL + security holes + cleanup

**Cíl fáze:** Doplnit 8 chybějících permission keys. Přidat `READ_ALL` roli pro management visibility. Zavřít 4 known security holes (Export, ProjectDashboard, ZaznamyComments, SearchReindex). Vyčistit orphaned DB rows z éry manuálního UI kompozice + rozšířit startup validator.

**Odhad:** ~1 den. 7 tasků.

**Merge safety:** Každý task je samostatně zelený. Controllery po přidání permission check fungují buď stejně nebo přesněji (lepší než dnes).

**Předpoklad:** Fáze B hotová (resolver swap). Seed je jediný zdroj implicit grantů.

## Task C1: Přidání 8 nových permission keys do konstant + seed actions

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/SecurityViewModels.cs` (PermissionKeys třída — přidat konstanty + Definitions entry + ProjectReadGrantKeys pokud applicable)
- Modify: `PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs` (Actions list — přidat 8 rows)
- Test: `PmTracker.Tests.Unit/Security/PermissionKeysTests.cs` (rozšířit existing, pokud exists)
- Test: `PmTracker.Tests.Unit/Authorization/NewPermissionKeysSeedTests.cs` (new)

### Step 1 — Failing test

Vytvořit `PmTracker.Tests.Unit/Authorization/NewPermissionKeysSeedTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class NewPermissionKeysSeedTests
{
    [Theory]
    [InlineData("dashboard.view")]
    [InlineData("export.pdf")]
    [InlineData("export.word")]
    [InlineData("comments.add")]
    [InlineData("comments.edit.own")]
    [InlineData("comments.delete.own")]
    [InlineData("search.reindex")]
    [InlineData("projects.read.all")]
    public void NewKey_ShouldBeInPermissionKeysSupportedSet(string key)
    {
        PermissionKeys.IsSupported(key).Should().BeTrue(
            $"permission key '{key}' musí být registrovaný v PermissionKeys.Definitions");
    }

    [Theory]
    [InlineData("dashboard.view")]
    [InlineData("export.pdf")]
    [InlineData("export.word")]
    [InlineData("comments.add")]
    [InlineData("comments.edit.own")]
    [InlineData("comments.delete.own")]
    [InlineData("search.reindex")]
    [InlineData("projects.read.all")]
    public void NewKey_ShouldBeSeededInActionsList(string key)
    {
        PermissionSeedConfiguration.Actions
            .Select(a => a.Klic)
            .Should().Contain(key,
                $"permission key '{key}' musí být v seedu Actions");
    }

    [Fact]
    public void Export_AndComments_AndDashboard_ShouldBeProjectScopeLevel()
    {
        foreach (var key in new[] { "dashboard.view", "export.pdf", "export.word", "comments.add", "comments.edit.own", "comments.delete.own" })
        {
            var action = PermissionSeedConfiguration.Actions.First(a => a.Klic == key);
            action.ScopeLevel.Should().Be(PermissionScopeLevel.Project,
                $"{key} potřebuje projektový kontext");
        }
    }

    [Fact]
    public void SearchReindex_AndProjectsReadAll_ShouldBeGlobalScopeLevel()
    {
        foreach (var key in new[] { "search.reindex", "projects.read.all" })
        {
            var action = PermissionSeedConfiguration.Actions.First(a => a.Klic == key);
            action.ScopeLevel.Should().Be(PermissionScopeLevel.Global,
                $"{key} je globální — nepotřebuje projektId");
        }
    }
}
```

### Step 2 — Verify FAIL

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~NewPermissionKeysSeedTests" -v q
```

Expected: FAIL — keys ještě neexistují.

### Step 3 — Přidat konstanty do `PermissionKeys`

V `PmTracker.Web/Models/ViewModels/SecurityViewModels.cs`, po existujících konstantách (za `SettingsManage`), přidat:

```csharp
    public const string DashboardView = "dashboard.view";
    public const string ExportPdf = "export.pdf";
    public const string ExportWord = "export.word";
    public const string CommentsAdd = "comments.add";
    public const string CommentsEditOwn = "comments.edit.own";
    public const string CommentsDeleteOwn = "comments.delete.own";
    public const string SearchReindex = "search.reindex";
    public const string ProjectsReadAll = "projects.read.all";
```

Do `Definitions` array přidat 8 nových rows (za existující):

```csharp
        new(DashboardView, "Zobrazit projektový dashboard", "PROJECTS", "PROJECT", "Read-only přístup k projektovému dashboardu."),
        new(ExportPdf, "Exportovat projekt do PDF", "PROJECTS", "PROJECT", "Generování PDF exportu projektu."),
        new(ExportWord, "Exportovat projekt do Word", "PROJECTS", "PROJECT", "Generování Word exportu projektu."),
        new(CommentsAdd, "Přidávat komentáře", "RECORDS", "PROJECT", "Vkládání nových komentářů k záznamům."),
        new(CommentsEditOwn, "Upravovat vlastní komentáře", "RECORDS", "PROJECT", "Úprava komentářů, které osoba sama vložila."),
        new(CommentsDeleteOwn, "Mazat vlastní komentáře", "RECORDS", "PROJECT", "Smazání komentářů, které osoba sama vložila."),
        new(SearchReindex, "Spustit reindex vyhledávání", "SETTINGS", "GLOBAL", "Administrátorská akce: full reindex FTS."),
        new(ProjectsReadAll, "Číst všechny projekty", "PROJECTS", "GLOBAL", "Read-only přístup ke všem projektům v aplikaci (management visibility).")
```

`ProjectReadGrantKeys` hashset — přidat `DashboardView`, `ExportPdf`, `ExportWord`, `CommentsAdd`, `CommentsEditOwn`, `CommentsDeleteOwn` (všechny PROJECT scope read-granty):

```csharp
    private static readonly HashSet<string> ProjectReadGrantKeys = new(
    [
        ProjectsEdit,
        ProjectsDelete,
        RecordsEdit,
        RecordsScheduleAdd,
        RecordsScheduleEdit,
        RecordsCommentSubsystemLead,
        MeetingsCreate,
        MeetingsEdit,
        TeamManage,
        DashboardView,           // nové
        ExportPdf,               // nové
        ExportWord,              // nové
        CommentsAdd,             // nové
        CommentsEditOwn,         // nové
        CommentsDeleteOwn        // nové
    ],
        StringComparer.OrdinalIgnoreCase);
```

### Step 4 — Přidat do seed Actions

V `PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs`, v `Actions` list (za existujících 14 rows), přidat 8 nových:

```csharp
    // Fáze C — Task C1: nové permission keys
    new("dashboard.view", "Zobrazit projektový dashboard", "PROJECTS", PermissionScopeLevel.Project),
    new("export.pdf", "Exportovat projekt do PDF", "PROJECTS", PermissionScopeLevel.Project),
    new("export.word", "Exportovat projekt do Word", "PROJECTS", PermissionScopeLevel.Project),
    new("comments.add", "Přidávat komentáře", "RECORDS", PermissionScopeLevel.Project),
    new("comments.edit.own", "Upravovat vlastní komentáře", "RECORDS", PermissionScopeLevel.Project),
    new("comments.delete.own", "Mazat vlastní komentáře", "RECORDS", PermissionScopeLevel.Project),
    new("search.reindex", "Spustit reindex vyhledávání", "SETTINGS", PermissionScopeLevel.Global),
    new("projects.read.all", "Číst všechny projekty", "PROJECTS", PermissionScopeLevel.Global)
```

### Step 5 — PASS + full suite

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~NewPermissionKeysSeedTests" -v q
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -v q
```

Expected: target 12/12 PASS, full suite +12 (cca ~710 po Fázi B).

### Step 6 — Commit

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
git add PmTracker.Web/Models/ViewModels/SecurityViewModels.cs \
        PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs \
        PmTracker.Tests.Unit/Authorization/NewPermissionKeysSeedTests.cs
git commit -m "feat(authz): 8 nových permission keys (dashboard/export/comments/search/read-all)"
```

---

## Task C2: `READ_ALL` role + permission matice pro nové keys

**Files:**
- Modify: `PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs` (přidat READ_ALL role + nové mapping rows)
- Test: `PmTracker.Tests.Unit/Authorization/ReadAllRoleSeedTests.cs` (new)
- Test: `PmTracker.Tests.Unit/Authorization/ExtendedRoleMatrixTests.cs` (new)

### Step 1 — Failing test

Vytvořit `PmTracker.Tests.Unit/Authorization/ReadAllRoleSeedTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class ReadAllRoleSeedTests
{
    [Fact]
    public void READ_ALL_Role_ShouldBeSeeded()
    {
        PermissionSeedConfiguration.Roles
            .Should().ContainSingle(r => r.Kod == "READ_ALL");
    }

    [Fact]
    public void READ_ALL_Role_ShouldBeGlobalScope()
    {
        var role = PermissionSeedConfiguration.Roles.First(r => r.Kod == "READ_ALL");
        role.Scope.Should().Be(RoleScope.Global);
        role.IsSystem.Should().BeTrue();
    }

    [Fact]
    public void READ_ALL_Role_ShouldGrantReadingAcrossAllProjects()
    {
        var permissions = PermissionSeedConfiguration.RoleMappings
            .Where(m => m.RoleKod == "READ_ALL" && m.IsAllowed)
            .Select(m => m.ActionKlic)
            .OrderBy(x => x)
            .ToArray();

        permissions.Should().BeEquivalentTo(new[]
        {
            "dashboard.view",
            "export.pdf",
            "export.word",
            "projects.read.all"
        }, "READ_ALL má read + export práva ve všech projektech (management visibility)");
    }
}
```

Vytvořit `PmTracker.Tests.Unit/Authorization/ExtendedRoleMatrixTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class ExtendedRoleMatrixTests
{
    private static string[] PermissionsFor(string roleKod) =>
        PermissionSeedConfiguration.RoleMappings
            .Where(m => m.RoleKod == roleKod && m.IsAllowed)
            .Select(m => m.ActionKlic)
            .OrderBy(x => x)
            .ToArray();

    [Fact]
    public void SUPERADMIN_ShouldHaveAllNewKeys()
    {
        var perms = PermissionsFor("SUPERADMIN");
        perms.Should().Contain(new[]
        {
            "dashboard.view", "export.pdf", "export.word",
            "comments.add", "comments.edit.own", "comments.delete.own",
            "search.reindex", "projects.read.all"
        });
    }

    [Fact]
    public void APP_ADMIN_ShouldHaveSearchReindexAndReadAll_ButNoCommentsEdit()
    {
        var perms = PermissionsFor("APP_ADMIN");
        perms.Should().Contain(new[] { "search.reindex", "projects.read.all", "dashboard.view" });
        // APP_ADMIN sám nekomentuje ani needituje projekty — má jen administrativní práva + visibility
    }

    [Fact]
    public void VLASTNIK_PROJEKTU_ShouldHaveDashboardExportComments()
    {
        var perms = PermissionsFor("VLASTNIK_PROJEKTU");
        perms.Should().Contain(new[]
        {
            "dashboard.view", "export.pdf", "export.word",
            "comments.add", "comments.edit.own", "comments.delete.own"
        });
    }

    [Fact]
    public void ADM_PROJ_AND_PROJ_MAN_ShouldHaveDashboardExportComments_ButNotProjectsEdit()
    {
        foreach (var kod in new[] { "ADM_PROJ", "PROJ_MAN" })
        {
            var perms = PermissionsFor(kod);
            perms.Should().Contain(new[]
            {
                "dashboard.view", "export.pdf", "export.word",
                "comments.add", "comments.edit.own", "comments.delete.own"
            });
            perms.Should().NotContain("projects.edit");
        }
    }

    [Fact]
    public void HOST_ShouldHaveReadOnlyKeys_DashboardAndExport()
    {
        var perms = PermissionsFor("HOST");
        perms.Should().BeEquivalentTo(new[]
        {
            "dashboard.view",
            "export.pdf",
            "export.word"
        }, "HOST je read-only — vidí dashboard a exportuje, nic nepíše");
    }

    [Fact]
    public void GEST_ShouldHaveCommentsAndReadOnly()
    {
        var perms = PermissionsFor("GEST");
        perms.Should().BeEquivalentTo(new[]
        {
            "comments.add", "comments.edit.own", "comments.delete.own",
            "dashboard.view", "export.pdf", "export.word",
            "records.comment.subsystemlead"
        }, "GEST smí komentovat + read-only");
    }

    [Fact]
    public void METODIK_SUBSYSTEMU_ShouldHaveComments()
    {
        var perms = PermissionsFor("METODIK_SUBSYSTEMU");
        perms.Should().Contain(new[]
        {
            "comments.add", "comments.edit.own", "comments.delete.own"
        }, "METODIK smí komentovat své záznamy");
    }
}
```

### Step 2 — Verify FAIL

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ReadAllRoleSeedTests|FullyQualifiedName~ExtendedRoleMatrixTests" -v q
```

Expected: FAIL.

### Step 3 — Přidat READ_ALL do `Roles` list

V `PermissionSeedConfiguration.cs`, do `Roles` listu, za `APP_ADMIN` (stále ve Globálních rolích):

```csharp
    // Fáze C — Task C2: READ_ALL role pro management visibility
    new("READ_ALL", "Read-all (management visibility)", "Read-only přístup ke všem projektům a jejich datům.", true, RoleScope.Global),
```

### Step 4 — Přidat mapping rows pro READ_ALL

V `RoleMappings` listu, na konci SUPERADMIN bloku (nebo na začátku řady READ_ALL):

```csharp
    // Fáze C — Task C2: READ_ALL mappings
    new("READ_ALL", "projects.read.all", ScopeMode.All, true),
    new("READ_ALL", "dashboard.view", ScopeMode.All, true),
    new("READ_ALL", "export.pdf", ScopeMode.All, true),
    new("READ_ALL", "export.word", ScopeMode.All, true),
```

### Step 5 — Rozšířit SUPERADMIN + APP_ADMIN o nové keys

V SUPERADMIN bloku, za existujícími 14 rows, přidat 8 nových:

```csharp
    new("SUPERADMIN", "dashboard.view", ScopeMode.All, true),
    new("SUPERADMIN", "export.pdf", ScopeMode.All, true),
    new("SUPERADMIN", "export.word", ScopeMode.All, true),
    new("SUPERADMIN", "comments.add", ScopeMode.All, true),
    new("SUPERADMIN", "comments.edit.own", ScopeMode.All, true),
    new("SUPERADMIN", "comments.delete.own", ScopeMode.All, true),
    new("SUPERADMIN", "search.reindex", ScopeMode.All, true),
    new("SUPERADMIN", "projects.read.all", ScopeMode.All, true),
```

V APP_ADMIN bloku (APP_ADMIN má admin + visibility, ne editační projekt. práva — nemá comments):

```csharp
    new("APP_ADMIN", "dashboard.view", ScopeMode.All, true),
    new("APP_ADMIN", "search.reindex", ScopeMode.All, true),
    new("APP_ADMIN", "projects.read.all", ScopeMode.All, true),
```

### Step 6 — Rozšířit projektové role

VLASTNIK_PROJEKTU + ADM_PROJ + PROJ_MAN dostanou:
- dashboard.view (všichni tři)
- export.pdf, export.word (všichni tři)
- comments.add, comments.edit.own, comments.delete.own (všichni tři)

Přidat v každém projektovém bloku (za existujících keys):

```csharp
    // VLASTNIK_PROJEKTU pokračování (Fáze C):
    new("VLASTNIK_PROJEKTU", "dashboard.view", ScopeMode.All, true),
    new("VLASTNIK_PROJEKTU", "export.pdf", ScopeMode.All, true),
    new("VLASTNIK_PROJEKTU", "export.word", ScopeMode.All, true),
    new("VLASTNIK_PROJEKTU", "comments.add", ScopeMode.All, true),
    new("VLASTNIK_PROJEKTU", "comments.edit.own", ScopeMode.All, true),
    new("VLASTNIK_PROJEKTU", "comments.delete.own", ScopeMode.All, true),

    // ADM_PROJ pokračování:
    new("ADM_PROJ", "dashboard.view", ScopeMode.All, true),
    new("ADM_PROJ", "export.pdf", ScopeMode.All, true),
    new("ADM_PROJ", "export.word", ScopeMode.All, true),
    new("ADM_PROJ", "comments.add", ScopeMode.All, true),
    new("ADM_PROJ", "comments.edit.own", ScopeMode.All, true),
    new("ADM_PROJ", "comments.delete.own", ScopeMode.All, true),

    // PROJ_MAN pokračování:
    new("PROJ_MAN", "dashboard.view", ScopeMode.All, true),
    new("PROJ_MAN", "export.pdf", ScopeMode.All, true),
    new("PROJ_MAN", "export.word", ScopeMode.All, true),
    new("PROJ_MAN", "comments.add", ScopeMode.All, true),
    new("PROJ_MAN", "comments.edit.own", ScopeMode.All, true),
    new("PROJ_MAN", "comments.delete.own", ScopeMode.All, true),
```

HOST (read-only) dostane dashboard + export:

```csharp
    // HOST pokračování (Fáze C):
    new("HOST", "dashboard.view", ScopeMode.All, true),
    new("HOST", "export.pdf", ScopeMode.All, true),
    new("HOST", "export.word", ScopeMode.All, true),
```

GEST dostane comments + read:

```csharp
    // GEST pokračování (Fáze C):
    new("GEST", "dashboard.view", ScopeMode.All, true),
    new("GEST", "export.pdf", ScopeMode.All, true),
    new("GEST", "export.word", ScopeMode.All, true),
    new("GEST", "comments.add", ScopeMode.All, true),
    new("GEST", "comments.edit.own", ScopeMode.All, true),
    new("GEST", "comments.delete.own", ScopeMode.All, true),
```

METODIK_SUBSYSTEMU dostane comments (subsystem-scoped via ObsazeniSubsystemuProjektu):

```csharp
    // METODIK_SUBSYSTEMU pokračování (Fáze C):
    new("METODIK_SUBSYSTEMU", "comments.add", ScopeMode.All, true),
    new("METODIK_SUBSYSTEMU", "comments.edit.own", ScopeMode.All, true),
    new("METODIK_SUBSYSTEMU", "comments.delete.own", ScopeMode.All, true),
```

VEDOUCI_SUBSYSTEMU + ZASTUPCE_VEDOUCIHO_SUBSYSTEMU dostanou stejné comment keys + read:

```csharp
    // VEDOUCI_SUBSYSTEMU pokračování:
    new("VEDOUCI_SUBSYSTEMU", "comments.add", ScopeMode.All, true),
    new("VEDOUCI_SUBSYSTEMU", "comments.edit.own", ScopeMode.All, true),
    new("VEDOUCI_SUBSYSTEMU", "comments.delete.own", ScopeMode.All, true),

    // ZASTUPCE_VEDOUCIHO_SUBSYSTEMU pokračování:
    new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "comments.add", ScopeMode.All, true),
    new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "comments.edit.own", ScopeMode.All, true),
    new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "comments.delete.own", ScopeMode.All, true),
```

### Step 7 — Tests PASS

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -v q
```

Expected: ~724 PASS (710 + ~14 nových).

### Step 8 — Commit

```bash
git add PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs \
        PmTracker.Tests.Unit/Authorization/ReadAllRoleSeedTests.cs \
        PmTracker.Tests.Unit/Authorization/ExtendedRoleMatrixTests.cs
git commit -m "feat(authz): READ_ALL role + rozšířená permission matice pro nové keys"
```

---

## Task C3: `ExportController` — permission check pro PDF/Word export

**Files:**
- Modify: `PmTracker.Web/Controllers/ExportController.cs`
- Test: `PmTracker.Tests.Unit/Authorization/ExportAuthzTests.cs` (new)

**Poznámka:** v této fázi používáme ještě legacy pattern `CurrentUserContext.PermissionGrants.Any(...)` nebo `IsSuperAdmin` fallback — nahrazení za `IAuthorizationService.RequirePermissionAsync` přijde ve Fázi D (D5 refactoring všech controllerů). Cílem Task C3 je **zavřít bezpečnostní díru**, ne refactorovat pattern.

### Step 1 — Failing test

Vytvořit `PmTracker.Tests.Unit/Authorization/ExportAuthzTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class ExportAuthzTests
{
    [Fact]
    public void ExportController_Pdf_ShouldReferenceExportPdfPermissionKey()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ExportController.cs"));

        code.Should().Contain("PermissionKeys.ExportPdf",
            "ExportController.Pdf musí kontrolovat export.pdf permission key");
    }

    [Fact]
    public void ExportController_Word_ShouldReferenceExportWordPermissionKey()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ExportController.cs"));

        code.Should().Contain("PermissionKeys.ExportWord",
            "ExportController.Word musí kontrolovat export.word permission key");
    }

    [Fact]
    public void ExportController_ShouldNotBypassAuthOnIsSuperAdminAlone()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ExportController.cs"));

        // Bypass "CurrentUserContext.IsSuperAdmin" bez kontroly permission key je anti-pattern
        code.Should().NotMatchRegex(
            @"if\s*\(\s*!?\s*CurrentUserContext\.IsSuperAdmin\s*\)\s*{\s*return\s+Forbid",
            "nesmí existovat IsSuperAdmin-only bypass pro export — používej permission key check");
    }
}
```

### Step 2 — Verify FAIL

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ExportAuthzTests" -v q
```

Expected: FAIL — controller neobsahuje odkaz na nové keys.

### Step 3 — Přečíst existující `ExportController.cs`

Zjisti aktuální strukturu:

```bash
grep -n "public.*IActionResult\|Forbid\|CurrentUserContext" PmTracker.Web/Controllers/ExportController.cs
```

### Step 4 — Nahradit IsSuperAdmin check za permission key check

V každé export actions (Pdf, Word) najít blok ve tvaru:

```csharp
if (!CurrentUserContext.IsSuperAdmin) return Forbid();
```

Nahradit za helper metodu (přidat do `ExportController`):

```csharp
private bool HasPermission(string key, int projektId)
{
    if (CurrentUserContext.IsSuperAdmin) return true;
    return CurrentUserContext.PermissionGrants.Any(g =>
        string.Equals(g.PermissionKey, key, StringComparison.OrdinalIgnoreCase)
        && g.IsAllowed
        && (g.ScopeMode == "ALL" || g.ProjectIds.Contains(projektId)));
}
```

A actions:

```csharp
public async Task<IActionResult> Pdf(int projektId, CancellationToken ct)
{
    if (!HasPermission(PermissionKeys.ExportPdf, projektId)) return Forbid();
    // ... zbytek metody beze změny
}

public async Task<IActionResult> Word(int projektId, CancellationToken ct)
{
    if (!HasPermission(PermissionKeys.ExportWord, projektId)) return Forbid();
    // ... zbytek
}
```

### Step 5 — PASS + full suite

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -v q
```

### Step 6 — Commit

```bash
git add PmTracker.Web/Controllers/ExportController.cs \
        PmTracker.Tests.Unit/Authorization/ExportAuthzTests.cs
git commit -m "fix(authz): ExportController vyžaduje export.pdf/export.word permission"
```

---

## Task C4: `ProjectDashboardController` — dashboard.view permission

**Files:**
- Modify: `PmTracker.Web/Controllers/ProjectDashboardController.cs` (2 místa: line 34, 110)
- Test: `PmTracker.Tests.Unit/Authorization/ProjectDashboardAuthzTests.cs` (new)

### Step 1 — Failing test

```csharp
using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class ProjectDashboardAuthzTests
{
    [Fact]
    public void ProjectDashboardController_ShouldReferenceDashboardViewPermissionKey()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ProjectDashboardController.cs"));

        code.Should().Contain("PermissionKeys.DashboardView",
            "ProjectDashboardController musí kontrolovat dashboard.view permission key");
    }

    [Fact]
    public void ProjectDashboardController_ShouldNotRelyOnIsSuperAdminBypass()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ProjectDashboardController.cs"));

        code.Should().NotMatchRegex(
            @"if\s*\(\s*!\s*CurrentUserContext\.IsSuperAdmin\s*\s*&&\s*!.*HasProjectRole",
            "starý bypass pattern musí být nahrazen explicit permission check");
    }
}
```

### Step 2-6 — Opakovat pattern z C3

V ProjectDashboardController.cs line 34 a 110, nahradit IsSuperAdmin/HasProjectRole check za:

```csharp
if (!HasPermission(PermissionKeys.DashboardView, projektId)) return Forbid();
```

Commit:
```bash
git commit -m "fix(authz): ProjectDashboardController vyžaduje dashboard.view"
```

---

## Task C5: `ZaznamyController` comment actions — explicit comments permission

**Files:**
- Modify: `PmTracker.Web/Controllers/ZaznamyController.Commands.cs` (3 comment akce: AddComment, EditComment, DeleteComment)
- Test: `PmTracker.Tests.Unit/Authorization/CommentsAuthzTests.cs` (new)

### Step 1 — Failing test

```csharp
using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class CommentsAuthzTests
{
    [Fact]
    public void ZaznamyController_AddComment_ShouldRequireCommentsAddPermission()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ZaznamyController.Commands.cs"));

        code.Should().Contain("PermissionKeys.CommentsAdd",
            "AddComment action musí kontrolovat comments.add");
    }

    [Fact]
    public void ZaznamyController_EditComment_ShouldRequireCommentsEditOwnPermission()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ZaznamyController.Commands.cs"));

        code.Should().Contain("PermissionKeys.CommentsEditOwn",
            "EditComment action musí kontrolovat comments.edit.own");
    }

    [Fact]
    public void ZaznamyController_DeleteComment_ShouldRequireCommentsDeleteOwnPermission()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ZaznamyController.Commands.cs"));

        code.Should().Contain("PermissionKeys.CommentsDeleteOwn",
            "DeleteComment action musí kontrolovat comments.delete.own");
    }

    [Fact]
    public void ZaznamyController_ShouldNotContain_HasPermissionTrueStub()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ZaznamyController.Commands.cs"));

        code.Should().NotMatchRegex(
            @"hasPermission\s*:\s*\(\s*\)\s*=>\s*true",
            "'hasPermission: () => true' stub musí být nahrazen skutečným checkem permission key");
    }
}
```

### Step 2-6 — Implementace

V `ZaznamyController.Commands.cs` najít 3 comment akce (AddComment, EditComment, DeleteComment). Každá má dnes volání typu `CommentAuthorizationPolicy.CanXxx(...)` s `hasPermission: () => true` stubem. Nahradit:

```csharp
// AddComment:
if (!HasPermission(PermissionKeys.CommentsAdd, projektId)) return Forbid();

// EditComment:
if (!HasPermission(PermissionKeys.CommentsEditOwn, projektId)) return Forbid();
// Poznámka: "own" sémantika (kontrola, že komentář je vlastní) zůstává ve službě CommentService;
// permission key jen potvrzuje, že uživatel MŮŽE editovat vlastní komentáře.

// DeleteComment:
if (!HasPermission(PermissionKeys.CommentsDeleteOwn, projektId)) return Forbid();
```

`HasPermission` helper přidat do `BaseController.cs` (nebo do `ZaznamyController` pokud `BaseController` už nemá — zkontroluj).

### Step 7 — Commit

```bash
git commit -m "fix(authz): ZaznamyController comment actions vyžadují comments.* permissions"
```

---

## Task C6: `SearchController.Reindex` — search.reindex permission

**Files:**
- Modify: `PmTracker.Web/Controllers/SearchController.cs` (line 73 a 95 — dva IsSuperAdmin checky)
- Test: `PmTracker.Tests.Unit/Authorization/SearchReindexAuthzTests.cs` (new)

### Step 1-6 — Stejný pattern

Test asserty že kontroler referencuje `PermissionKeys.SearchReindex`. Dva call sites v kontroleru nahradit:

```csharp
if (!HasPermission(PermissionKeys.SearchReindex)) return Forbid();
// (SearchReindex je Global scope → projektId není potřeba)
```

Commit:
```bash
git commit -m "fix(authz): SearchController.Reindex vyžaduje search.reindex"
```

---

## Task C7: Cleanup migration + startup validator rozšíření

**Cíl:** Vyčistit DB řádky z éry manuální UI kompozice — pokud po Fázi F nepůjdou přes UI nově upravit, orphaned rows (role → permission mappings, které neodpovídají seedu) zůstanou v DB navždy. SQL skript smaže orphans. Startup validator ověří, že seed matches DB.

**Files:**
- Create: `db_upgrade_1_3_0_cleanup_orphaned_role_permissions.sql`
- Modify: `PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs`
- Test: `PmTracker.Tests.Unit/Authorization/SeedCleanupMigrationTests.cs` (new)

### Step 1 — Failing test

```csharp
using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class SeedCleanupMigrationTests
{
    [Fact]
    public void CleanupMigration_FileShouldExist()
    {
        var scriptPath = ResolvePath("db_upgrade_1_3_0_cleanup_orphaned_role_permissions.sql");
        File.Exists(scriptPath).Should().BeTrue();
    }

    [Fact]
    public void CleanupMigration_ShouldOnlyTouchRolePermissionsTables()
    {
        var script = File.ReadAllText(ResolvePath("db_upgrade_1_3_0_cleanup_orphaned_role_permissions.sql"));

        script.Should().Contain("authz.role_permissions");
        script.Should().Contain("SET XACT_ABORT ON");
        script.Should().Contain("BEGIN TRANSACTION");
        script.Should().Contain("COMMIT TRANSACTION");

        // Bezpečnostní pojistka: nesmí mazat role, permissions ani superadmins
        script.Should().NotMatchRegex(@"DELETE\s+FROM\s+authz\.roles\b");
        script.Should().NotMatchRegex(@"DELETE\s+FROM\s+authz\.permissions\b");
        script.Should().NotMatchRegex(@"DELETE\s+FROM\s+authz\.superadmins\b");
    }

    [Fact]
    public void SqlStartupValidator_ShouldReferenceCleanupMigration()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs"));

        code.Should().Contain("db_upgrade_1_3_0_cleanup_orphaned_role_permissions",
            "validator musí odkazovat na cleanup migraci v error message");
    }
}
```

### Step 2 — Verify FAIL

### Step 3 — Vytvořit SQL skript

`/Users/Pavel.Andrlik/Documents/PM Tracker/db_upgrade_1_3_0_cleanup_orphaned_role_permissions.sql`:

```sql
-- db_upgrade_1_3_0_cleanup_orphaned_role_permissions.sql
-- Authorization unification — Fáze C — Task C7
-- Cíl: smazat řádky v authz.role_permissions, které nevznikly přes seed
--      (pocházejí ze staré UI pro ruční kompozici, která je ve Fázi F zrušená).
--
-- Strategy: Seed je jediný zdroj pravdy. Každý deploy se seed upsertne do DB.
-- Tato migrace smaže JEN orphaned rows (role_id × permission_id kombinace,
-- které nejsou v seed matici). Po migraci bude DB přesně zrcadlem seedu.
--
-- POZOR: spustit AŽ PO nasazení kódu s novou seed matricí (Task C2).
-- Před spuštěním doporučuji backup.

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1. Vyčistit authz.role_permission_projects (INCLUDE scope mode per-project grants).
--    Tyto vznikly přes starou UI "přidej grant na konkrétní projekt".
--    Po Fázi F se spravují jen přes ObsazeniProjektu.
DELETE FROM authz.role_permission_projects
WHERE role_permission_id IN (
    SELECT rp.id
    FROM authz.role_permissions rp
    INNER JOIN authz.roles r ON r.id = rp.role_id
    WHERE r.is_system = 0   -- custom role (admin-created, není v seedu)
);

-- 2. Smazat mappings pro custom (non-system) role — ty už nejsou povolené.
DELETE FROM authz.role_permissions
WHERE role_id IN (
    SELECT id FROM authz.roles WHERE is_system = 0
);

-- 3. Deaktivovat custom role (nemažeme, zachováváme pro audit).
UPDATE authz.roles SET is_active = 0 WHERE is_system = 0;

-- 4. Log provedené migrace — pro audit.
PRINT '[db_upgrade_1_3_0] Cleanup orphaned role_permissions dokončen.';

COMMIT TRANSACTION;
```

### Step 4 — Rozšířit validator

V `SqlStartupValidatorHostedService.cs`, přidat na konec `StartAsync` (před `_logger.LogInformation("SQL startup validace proběhla úspěšně.");`):

```csharp
// Fáze C — Task C7: ověření cleanup migrace proběhlo.
// Pokud DB obsahuje custom (non-system) aktivní role, znamená to, že cleanup
// nebyl spuštěn → log warning (ne fatal; seed sám neohrozí fungování).
var customActiveRolesCount = await dbContext.AuthzRoles
    .AsNoTracking()
    .CountAsync(r => !r.IsSystem && r.IsActive, ct);

if (customActiveRolesCount > 0)
{
    _logger.LogWarning(
        "V DB je {Count} aktivních non-system rolí, které nejsou v seedu. " +
        "Očekávaná akce: spustit db_upgrade_1_3_0_cleanup_orphaned_role_permissions.sql. " +
        "Aplikace funguje, ale orphaned role mohou zmást audit.",
        customActiveRolesCount);
}
```

### Step 5 — PASS + full suite

### Step 6 — Commit

```bash
git add db_upgrade_1_3_0_cleanup_orphaned_role_permissions.sql \
        PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs \
        PmTracker.Tests.Unit/Authorization/SeedCleanupMigrationTests.cs
git commit -m "feat(authz): cleanup migrace orphaned role_permissions + warning validátor"
```

---

## Fáze C — Completion Checklist

- [ ] 7 commitů: C1-C7
- [ ] PermissionKeys.cs má 8 nových konstant + Definitions
- [ ] PermissionSeedConfiguration má 8 nových Actions + READ_ALL role + rozšířené matice
- [ ] 4 security holes uzavřené (Export, ProjectDashboard, Zaznamy comments, Search.Reindex)
- [ ] `db_upgrade_1_3_0_cleanup_orphaned_role_permissions.sql` v kořeni
- [ ] Validator loguje warning, pokud v DB custom role
- [ ] Test suite zelená (cca 724+ testů)
- [ ] Žádný controller/service nepoužívá `hasPermission: () => true` stub

---

# Fáze D — `IAuthorizationService` + `AuthorizationSnapshot` + policy handler

**Cíl fáze:** Zavést kanonickou fasádu pro autorizaci. `AuthorizationSnapshot` = in-memory per-request projekce. `IAuthorizationService` = jediný entry point pro `HasPermissionAsync` / `RequirePermissionAsync`. ASP.NET Core `IAuthorizationHandler` umožní `[Authorize(Policy="permission:xxx")]`. Refactor 25 controllerů + 14 services na jedinou cestu. Architecture test vynucující policy atribut na write actions.

**Odhad:** 1.5-2 dny. 8 tasků.

**Merge safety:** Tasky D1-D4 jsou infra (žádný dopad na chování). D5-D8 jsou refactoring — každý controller/service zvlášť nebo v malých dávkách. Deduplikace starých checků se děje postupně; aplikace se chová identicky během celé Fáze D.

## Task D1: `AuthorizationSnapshot` record + `HasPermission` metoda

**Files:**
- Create: `PmTracker.Web/Services/Security/AuthorizationSnapshot.cs`
- Test: `PmTracker.Tests.Unit/Authorization/AuthorizationSnapshotTests.cs` (new)

### Step 1 — Failing test

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class AuthorizationSnapshotTests
{
    [Fact]
    public void HasPermission_ShouldReturnTrue_WhenSuperAdmin()
    {
        var snapshot = new AuthorizationSnapshot(
            IsSuperAdmin: true,
            GlobalPermissions: new HashSet<string>(),
            PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>>(),
            PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>());

        snapshot.HasPermission("anything").Should().BeTrue();
        snapshot.HasPermission("records.edit", projektId: 999).Should().BeTrue();
    }

    [Fact]
    public void HasPermission_ShouldReturnTrue_WhenGlobalPermissionPresent()
    {
        var snapshot = new AuthorizationSnapshot(
            IsSuperAdmin: false,
            GlobalPermissions: new HashSet<string>(new[] { "people.manage" }, StringComparer.OrdinalIgnoreCase),
            PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>>(),
            PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>());

        snapshot.HasPermission("people.manage").Should().BeTrue();
        snapshot.HasPermission("People.Manage").Should().BeTrue(); // case-insensitive
    }

    [Fact]
    public void HasPermission_ShouldReturnTrue_WhenProjectPermissionMatches()
    {
        var perProject = new Dictionary<int, IReadOnlySet<string>>
        {
            [777] = new HashSet<string>(new[] { "records.edit" }, StringComparer.OrdinalIgnoreCase)
        };

        var snapshot = new AuthorizationSnapshot(
            IsSuperAdmin: false,
            GlobalPermissions: new HashSet<string>(),
            PerProjectPermissions: perProject,
            PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>());

        snapshot.HasPermission("records.edit", projektId: 777).Should().BeTrue();
        snapshot.HasPermission("records.edit", projektId: 888).Should().BeFalse();
        snapshot.HasPermission("records.edit").Should().BeFalse(); // chybí projektId
    }

    [Fact]
    public void HasPermission_ShouldReturnTrue_WhenSubsystemPermissionMatches()
    {
        var perSubsystem = new Dictionary<int, IReadOnlySet<string>>
        {
            [50] = new HashSet<string>(new[] { "records.comment.subsystemlead" }, StringComparer.OrdinalIgnoreCase)
        };

        var snapshot = new AuthorizationSnapshot(
            IsSuperAdmin: false,
            GlobalPermissions: new HashSet<string>(),
            PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>>(),
            PerSubsystemPermissions: perSubsystem);

        snapshot.HasPermission("records.comment.subsystemlead", subsystemId: 50).Should().BeTrue();
        snapshot.HasPermission("records.comment.subsystemlead", subsystemId: 51).Should().BeFalse();
    }

    [Fact]
    public void HasPermission_ShouldReturnFalse_ForUnknownKey()
    {
        var snapshot = new AuthorizationSnapshot(false,
            new HashSet<string>(), new Dictionary<int, IReadOnlySet<string>>(), new Dictionary<int, IReadOnlySet<string>>());

        snapshot.HasPermission("nonexistent.key").Should().BeFalse();
    }
}
```

### Step 2 — Verify FAIL (type doesn't exist)

### Step 3 — Implementace

`PmTracker.Web/Services/Security/AuthorizationSnapshot.cs`:

```csharp
namespace PmTracker.Web.Services.Security;

/// <summary>
/// In-memory per-request projekce autorizačních práv jedné osoby. Staví se
/// jednou za HTTP request (viz <see cref="IAuthorizationService"/>) a odpovídá
/// na volání <see cref="HasPermission(string, int?, int?)"/> bez dalších DB dotazů.
/// </summary>
/// <remarks>
/// Není uložen v DB — zdrojem pravdy je <c>authz.role_permissions</c> + seed.
/// Invalidace: žádná potřeba, protože je per-request.
/// </remarks>
public sealed record AuthorizationSnapshot(
    bool IsSuperAdmin,
    IReadOnlySet<string> GlobalPermissions,
    IReadOnlyDictionary<int, IReadOnlySet<string>> PerProjectPermissions,
    IReadOnlyDictionary<int, IReadOnlySet<string>> PerSubsystemPermissions)
{
    /// <summary>
    /// Vrátí true, pokud osoba má dané oprávnění v daném kontextu.
    /// </summary>
    /// <param name="key">Permission key (např. "records.edit").</param>
    /// <param name="projektId">Projekt kontext, pokud je key project-scope.</param>
    /// <param name="subsystemId">Subsystem kontext, pokud je key subsystem-scope.</param>
    public bool HasPermission(string key, int? projektId = null, int? subsystemId = null)
    {
        if (IsSuperAdmin)
        {
            return true;
        }

        if (GlobalPermissions.Contains(key))
        {
            return true;
        }

        if (projektId.HasValue
            && PerProjectPermissions.TryGetValue(projektId.Value, out var projectPerms)
            && projectPerms.Contains(key))
        {
            return true;
        }

        if (subsystemId.HasValue
            && PerSubsystemPermissions.TryGetValue(subsystemId.Value, out var subsystemPerms)
            && subsystemPerms.Contains(key))
        {
            return true;
        }

        return false;
    }
}
```

### Step 4 — Tests PASS

### Step 5 — Commit

```bash
git add PmTracker.Web/Services/Security/AuthorizationSnapshot.cs \
        PmTracker.Tests.Unit/Authorization/AuthorizationSnapshotTests.cs
git commit -m "feat(authz): AuthorizationSnapshot record + HasPermission metoda"
```

---

## Task D2: `IAuthorizationService` + `AuthorizationService` implementace

**Files:**
- Create: `PmTracker.Web/Services/Security/IAuthorizationService.cs`
- Create: `PmTracker.Web/Services/Security/AuthorizationService.cs`
- Create: `PmTracker.Web/Services/Security/AuthorizationSnapshotBuilder.cs`
- Create: `PmTracker.Web/Services/Security/ForbiddenException.cs`
- Test: `PmTracker.Tests.Unit/Authorization/AuthorizationServiceTests.cs` (new)

### Step 1 — Failing tests

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class AuthorizationServiceTests
{
    private static PmTrackerDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PmTrackerDbContext(options);
    }

    [Fact]
    public async Task BuildSnapshotAsync_ShouldReturnEmptySnapshot_ForUnknownPerson()
    {
        await using var db = CreateInMemoryDb();
        var service = new AuthorizationService(db);

        var snapshot = await service.BuildSnapshotAsync(osobaId: 999, CancellationToken.None);

        snapshot.IsSuperAdmin.Should().BeFalse();
        snapshot.GlobalPermissions.Should().BeEmpty();
        snapshot.PerProjectPermissions.Should().BeEmpty();
        snapshot.PerSubsystemPermissions.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildSnapshotAsync_ShouldSetIsSuperAdmin_WhenInAuthzSuperadmins()
    {
        await using var db = CreateInMemoryDb();
        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "T", Prijmeni = "U" });
        db.AuthzSuperadmins.Add(new AuthzSuperadminEntity { OsobaId = 42, IsActive = true });
        await db.SaveChangesAsync();

        var service = new AuthorizationService(db);
        var snapshot = await service.BuildSnapshotAsync(42, CancellationToken.None);

        snapshot.IsSuperAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_ShouldReturnTrue_WhenSuperAdmin()
    {
        await using var db = CreateInMemoryDb();
        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "T", Prijmeni = "U" });
        db.AuthzSuperadmins.Add(new AuthzSuperadminEntity { OsobaId = 42, IsActive = true });
        await db.SaveChangesAsync();

        var service = new AuthorizationService(db);

        (await service.HasPermissionAsync(42, "anything")).Should().BeTrue();
    }

    [Fact]
    public async Task RequirePermissionAsync_ShouldThrowForbidden_WhenMissing()
    {
        await using var db = CreateInMemoryDb();
        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "T", Prijmeni = "U" });
        await db.SaveChangesAsync();

        var service = new AuthorizationService(db);

        await FluentActions
            .Invoking(() => service.RequirePermissionAsync(42, "records.edit", projektId: 777))
            .Should()
            .ThrowAsync<ForbiddenException>()
            .WithMessage("*records.edit*");
    }
}
```

### Step 2 — Verify FAIL

### Step 3 — Vytvořit `ForbiddenException`

`PmTracker.Web/Services/Security/ForbiddenException.cs`:

```csharp
namespace PmTracker.Web.Services.Security;

/// <summary>
/// Výjimka pro autorizační odmítnutí ve service vrstvě. Global exception handler
/// ji mapuje na HTTP 403 Forbidden.
/// </summary>
public sealed class ForbiddenException : Exception
{
    public string PermissionKey { get; }
    public int? ProjektId { get; }
    public int? SubsystemId { get; }

    public ForbiddenException(string permissionKey, int? projektId = null, int? subsystemId = null)
        : base(BuildMessage(permissionKey, projektId, subsystemId))
    {
        PermissionKey = permissionKey;
        ProjektId = projektId;
        SubsystemId = subsystemId;
    }

    private static string BuildMessage(string key, int? projektId, int? subsystemId)
    {
        var context = (projektId, subsystemId) switch
        {
            (int p, _) => $" na projektu {p}",
            (_, int s) => $" na subsystému {s}",
            _ => string.Empty
        };
        return $"Permission '{key}' odmítnuto{context}.";
    }
}
```

### Step 4 — Interface

`PmTracker.Web/Services/Security/IAuthorizationService.cs`:

```csharp
namespace PmTracker.Web.Services.Security;

/// <summary>
/// Kanonická fasáda pro autorizační rozhodování. Jediný entry point pro:
/// - výstavbu per-request snapshot (viz <see cref="BuildSnapshotAsync"/>)
/// - kontrolu oprávnění (<see cref="HasPermissionAsync"/>)
/// - vynucení oprávnění (<see cref="RequirePermissionAsync"/>)
/// </summary>
/// <remarks>
/// Všechny autorizační kontroly v aplikaci (controllery, services) musí jít přes
/// tento interface nebo přes ASP.NET Core <c>[Authorize(Policy="permission:xxx")]</c>
/// (který interně volá totéž). Žádné ad-hoc IsSuperAdmin + Grants.Any().
/// </remarks>
public interface IAuthorizationService
{
    /// <summary>Postaví snapshot pro osobu (načte z DB). Cacheable per-request.</summary>
    Task<AuthorizationSnapshot> BuildSnapshotAsync(int osobaId, CancellationToken ct = default);

    /// <summary>Vrátí true, pokud osoba má oprávnění (s volitelným projekt/subsystem kontextem).</summary>
    Task<bool> HasPermissionAsync(int osobaId, string permissionKey, int? projektId = null, int? subsystemId = null, CancellationToken ct = default);

    /// <summary>Vyhodí <see cref="ForbiddenException"/>, pokud osoba nemá oprávnění.</summary>
    Task RequirePermissionAsync(int osobaId, string permissionKey, int? projektId = null, int? subsystemId = null, CancellationToken ct = default);
}
```

### Step 5 — `AuthorizationSnapshotBuilder`

`PmTracker.Web/Services/Security/AuthorizationSnapshotBuilder.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;

namespace PmTracker.Web.Services.Security;

/// <summary>
/// Staví AuthorizationSnapshot z DB. Používá se z AuthorizationService;
/// výsledek je cacheovatelný (per-request nebo IMemoryCache).
/// </summary>
internal sealed class AuthorizationSnapshotBuilder(PmTrackerDbContext db)
{
    public async Task<AuthorizationSnapshot> BuildAsync(int osobaId, CancellationToken ct)
    {
        // 1. SuperAdmin status (AuthzSuperadmins tabulka)
        var isSuperAdmin = await db.AuthzSuperadmins
            .AsNoTracking()
            .AnyAsync(s => s.OsobaId == osobaId && s.IsActive, ct);

        // 2. Global permissions (AuthzUserRoles — přímo přiřazené globální role + jejich role_permissions)
        var globalPerms = await (
                from ur in db.AuthzUserRoles.AsNoTracking()
                where ur.OsobaId == osobaId && ur.IsActive
                join r in db.AuthzRoles.AsNoTracking() on ur.RoleId equals r.Id
                where r.IsActive && r.Scope == RoleScope.Global
                join rp in db.AuthzRolePermissions.AsNoTracking() on r.Id equals rp.RoleId
                where rp.IsAllowed
                join p in db.AuthzPermissions.AsNoTracking() on rp.PermissionId equals p.Id
                where p.IsActive
                select p.Klic)
            .Distinct()
            .ToListAsync(ct);

        // 3. Per-project permissions (via ObsazeniProjektu × CiselnikRoliProjektu.AuthzRoleId)
        var projectRows = await (
                from a in db.ObsazeniProjektu.AsNoTracking()
                where a.OsobaId == osobaId && !a.DatumOdebrani.HasValue
                join lr in db.CiselnikRoliProjektu.AsNoTracking() on a.RoleId equals lr.Id
                where lr.AuthzRoleId != null
                join r in db.AuthzRoles.AsNoTracking() on lr.AuthzRoleId equals r.Id
                where r.IsActive
                join rp in db.AuthzRolePermissions.AsNoTracking() on r.Id equals rp.RoleId
                where rp.IsAllowed
                join p in db.AuthzPermissions.AsNoTracking() on rp.PermissionId equals p.Id
                where p.IsActive
                select new { a.ProjektId, p.Klic })
            .ToListAsync(ct);

        var perProject = projectRows
            .GroupBy(x => x.ProjektId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlySet<string>)new HashSet<string>(g.Select(x => x.Klic), StringComparer.OrdinalIgnoreCase));

        // 4. Per-subsystem permissions (via ObsazeniSubsystemuProjektu × CiselnikRoliSubsystemu.AuthzRoleId)
        var subsystemRows = await (
                from a in db.ObsazeniSubsystemuProjektu.AsNoTracking()
                where a.OsobaId == osobaId && !a.DatumOdebrani.HasValue
                join ps in db.ProjektSubsystemy.AsNoTracking() on a.ProjektSubsystemId equals ps.Id
                where !ps.DatumOdebrani.HasValue
                join lr in db.CiselnikRoliSubsystemu.AsNoTracking() on a.RoleSubsystemuId equals lr.Id
                where lr.AuthzRoleId != null
                join r in db.AuthzRoles.AsNoTracking() on lr.AuthzRoleId equals r.Id
                where r.IsActive
                join rp in db.AuthzRolePermissions.AsNoTracking() on r.Id equals rp.RoleId
                where rp.IsAllowed
                join p in db.AuthzPermissions.AsNoTracking() on rp.PermissionId equals p.Id
                where p.IsActive
                select new { a.ProjektSubsystemId, p.Klic })
            .ToListAsync(ct);

        var perSubsystem = subsystemRows
            .GroupBy(x => x.ProjektSubsystemId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlySet<string>)new HashSet<string>(g.Select(x => x.Klic), StringComparer.OrdinalIgnoreCase));

        return new AuthorizationSnapshot(
            IsSuperAdmin: isSuperAdmin,
            GlobalPermissions: new HashSet<string>(globalPerms, StringComparer.OrdinalIgnoreCase),
            PerProjectPermissions: perProject,
            PerSubsystemPermissions: perSubsystem);
    }
}
```

### Step 6 — `AuthorizationService`

`PmTracker.Web/Services/Security/AuthorizationService.cs`:

```csharp
using PmTracker.Web.Data;

namespace PmTracker.Web.Services.Security;

public sealed class AuthorizationService(PmTrackerDbContext db) : IAuthorizationService
{
    private readonly AuthorizationSnapshotBuilder _builder = new(db);

    public async Task<AuthorizationSnapshot> BuildSnapshotAsync(int osobaId, CancellationToken ct = default)
        => await _builder.BuildAsync(osobaId, ct);

    public async Task<bool> HasPermissionAsync(int osobaId, string permissionKey, int? projektId = null, int? subsystemId = null, CancellationToken ct = default)
    {
        var snapshot = await BuildSnapshotAsync(osobaId, ct);
        return snapshot.HasPermission(permissionKey, projektId, subsystemId);
    }

    public async Task RequirePermissionAsync(int osobaId, string permissionKey, int? projektId = null, int? subsystemId = null, CancellationToken ct = default)
    {
        if (!await HasPermissionAsync(osobaId, permissionKey, projektId, subsystemId, ct))
        {
            throw new ForbiddenException(permissionKey, projektId, subsystemId);
        }
    }
}
```

### Step 7 — Tests PASS

### Step 8 — Commit

```bash
git add PmTracker.Web/Services/Security/AuthorizationSnapshot.cs \
        PmTracker.Web/Services/Security/IAuthorizationService.cs \
        PmTracker.Web/Services/Security/AuthorizationService.cs \
        PmTracker.Web/Services/Security/AuthorizationSnapshotBuilder.cs \
        PmTracker.Web/Services/Security/ForbiddenException.cs \
        PmTracker.Tests.Unit/Authorization/AuthorizationServiceTests.cs
git commit -m "feat(authz): IAuthorizationService + AuthorizationService + ForbiddenException"
```

---

## Task D3: ASP.NET Core Policy handler + DI wiring

**Cíl:** Umožnit `[Authorize(Policy="permission:records.edit")]` na controllerech. Policy handler load snapshotu z `IAuthorizationService` a ověří key.

**Files:**
- Create: `PmTracker.Web/Services/Security/PermissionRequirement.cs`
- Create: `PmTracker.Web/Services/Security/PermissionAuthorizationHandler.cs`
- Create: `PmTracker.Web/Extensions/AuthorizationPolicyExtensions.cs`
- Modify: `PmTracker.Web/Program.cs` (DI + policy registration)
- Test: `PmTracker.Tests.Unit/Authorization/PermissionPolicyHandlerTests.cs` (new)

### Step 1 — Failing tests (integration-style)

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PmTracker.Web.Services.Security;
using System.Security.Claims;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class PermissionPolicyHandlerTests
{
    [Fact]
    public async Task Handler_ShouldSucceed_WhenPermissionGranted()
    {
        var authzService = new Mock<IAuthorizationService>();
        authzService
            .Setup(s => s.HasPermissionAsync(42, "records.edit", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.OsobaId).Returns(42);

        var handler = new PermissionAuthorizationHandler(authzService.Object, currentUser.Object);
        var requirement = new PermissionRequirement("records.edit");
        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            new ClaimsPrincipal(),
            null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handler_ShouldFail_WhenPermissionDenied()
    {
        var authzService = new Mock<IAuthorizationService>();
        authzService
            .Setup(s => s.HasPermissionAsync(42, "records.edit", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.OsobaId).Returns(42);

        var handler = new PermissionAuthorizationHandler(authzService.Object, currentUser.Object);
        var requirement = new PermissionRequirement("records.edit");
        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            new ClaimsPrincipal(),
            null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }
}
```

### Step 2 — `PermissionRequirement`

```csharp
using Microsoft.AspNetCore.Authorization;

namespace PmTracker.Web.Services.Security;

public sealed class PermissionRequirement(string permissionKey) : IAuthorizationRequirement
{
    public string PermissionKey { get; } = permissionKey;
}
```

### Step 3 — Policy handler

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace PmTracker.Web.Services.Security;

public sealed class PermissionAuthorizationHandler(
    IAuthorizationService authzService,
    ICurrentUserAccessor currentUser,
    IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var osobaId = currentUser.OsobaId;
        if (osobaId is null)
        {
            return; // anonymní → fail (Fail() by rovnou ukončilo; nezavoláním Succeed policies selže)
        }

        // Kontext projekt/subsystem z route parametru (pokud je)
        int? projektId = null;
        int? subsystemId = null;
        var route = httpContextAccessor.HttpContext?.Request.RouteValues;
        if (route != null)
        {
            if (route.TryGetValue("projektId", out var p) && int.TryParse(p?.ToString(), out var pid)) projektId = pid;
            if (route.TryGetValue("subsystemId", out var s) && int.TryParse(s?.ToString(), out var sid)) subsystemId = sid;
        }

        var ct = httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        if (await authzService.HasPermissionAsync(osobaId.Value, requirement.PermissionKey, projektId, subsystemId, ct))
        {
            context.Succeed(requirement);
        }
    }
}
```

### Step 4 — `ICurrentUserAccessor` interface (if not exists)

Over current UserContextResolver. Přidat interface pro čtení `osobaId` z request kontextu:

```csharp
// PmTracker.Web/Services/Security/ICurrentUserAccessor.cs
namespace PmTracker.Web.Services.Security;

public interface ICurrentUserAccessor
{
    int? OsobaId { get; }
    bool IsSuperAdmin { get; }
}
```

Implementace: `CurrentUserAccessor` načítá z `HttpContext.Items["authz.snapshot"]` (viz D4).

### Step 5 — Extensions + Program.cs wiring

`PmTracker.Web/Extensions/AuthorizationPolicyExtensions.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Extensions;

public static class AuthorizationPolicyExtensions
{
    public static void AddPermissionPolicies(this AuthorizationOptions options)
    {
        // Registrovat policy per každý známý permission key jako "permission:xxx".
        // Policy handler (PermissionAuthorizationHandler) zajistí match na snapshot.
        foreach (var definition in PermissionKeys.AllDefinitions)
        {
            options.AddPolicy(
                $"permission:{definition.Key}",
                policy => policy.Requirements.Add(new PermissionRequirement(definition.Key)));
        }
    }
}
```

V `Program.cs` za existující `AddAuthorization`:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPermissionPolicies();
});
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationService, AuthorizationService>();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
```

### Step 6 — Tests PASS

### Step 7 — Commit

```bash
git commit -m "feat(authz): policy handler + DI wiring pro [Authorize(Policy=...)]"
```

---

## Task D4: `UserContextResolver` emituje snapshot vedle legacy VM

**Cíl:** Postupná migrace — staré `CurrentUserContextViewModel.PermissionGrants` zůstává (controllery ho ještě používají), ale přidá se `Authorization: AuthorizationSnapshot` field. Nové controllery/services používají snapshot; staré VM zůstanou do refactoru v D5/D6.

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/SecurityViewModels.cs` (CurrentUserContextViewModel + přidat `Authorization` field)
- Modify: `PmTracker.Web/Services/Security/UserContextResolver.cs` (volat `AuthorizationSnapshotBuilder` + naplnit field)
- Test: `PmTracker.Tests.Unit/Authorization/UserContextSnapshotIntegrationTests.cs` (new)

### Step 1-6 — Detail

Přidat do `CurrentUserContextViewModel`:

```csharp
public AuthorizationSnapshot? Authorization { get; init; }
```

V `UserContextResolver.ResolveAsync`, před vytvořením `CurrentUserContextViewModel`:

```csharp
var authzBuilder = new AuthorizationSnapshotBuilder(_dbContext);
var authzSnapshot = await authzBuilder.BuildAsync(osoba.Id, ct);
```

A do VM construction:

```csharp
var context = new CurrentUserContextViewModel
{
    // ... existing fields ...
    Authorization = authzSnapshot
};
```

Test ověří, že po `ResolveAsync` je `Authorization` field naplněný a odpovídá seedu.

Commit:
```bash
git commit -m "feat(authz): UserContextResolver emit AuthorizationSnapshot vedle legacy VM"
```

---

## Task D5: Refactor 25 controllerů — `[Authorize(Policy="permission:...")]` attribute

**Cíl:** Přidat Policy atribut na všechny endpointy s autorizačním checkem. Starý ad-hoc check v controlleru (`if (!CurrentUserContext.IsSuperAdmin)`) se smaže; policy handler to dělá transparentně.

**Files:** 25 controllerů, viz inventory:
- `AppController.cs`, `BaseController.cs`, `CiselnikyController.cs`, `DashboardController.cs`, `DokumentaceController.cs`
- `ExportController.cs`, `HomeController.cs`, `JednaniController.cs`, `NastaveniController.cs`, `NavrhyController.cs`
- `ObsazeniController.cs`, `OsobyController.cs`, `ProfilController.cs`, `ProjectDashboardController.cs`
- `ProjektyController.cs` + Commands/MeetingModals/ProjectModals/TabPartials partials
- `SearchController.cs`, `StyleGuideController.cs`, `VyzvyController.cs`
- `ZaznamyController.cs` + Commands/Modals/Partials partials

**Přístup:** jeden controller per task-mini (aby každý commit byl atomický), ale v subagent-driven flow lze seskupit 3-5 podobných controllerů do jednoho dispatch.

### Šablona pro každý controller

```csharp
// Před:
public async Task<IActionResult> EditRecord(int projektId, ...)
{
    if (!CurrentUserContext.IsSuperAdmin && !CurrentUserContext.PermissionGrants.Any(...)) return Forbid();
    // ...
}

// Po:
[Authorize(Policy = "permission:records.edit")]
public async Task<IActionResult> EditRecord(int projektId, ...)
{
    // Policy handler se postará — ad-hoc check smazán.
    // ...
}
```

### Per-controller kroky (pattern)

Pro každý controller:

1. **Failing test** (`XxxControllerAuthzTests.cs`) — že action má `[Authorize(Policy=...)]`:
   ```csharp
   [Fact]
   public void ExportController_Pdf_ShouldHaveExportPdfPolicy()
   {
       var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ExportController.cs"));
       code.Should().Contain("[Authorize(Policy = \"permission:export.pdf\")]");
   }
   ```

2. **Add attribute** na action metodu

3. **Smazat ad-hoc check** v těle metody (pokud `PermissionGrants.Any` nebo `IsSuperAdmin && HasPermission...`)

4. **Test PASS**

5. **Commit** (pattern: jeden commit per controller nebo per tematickou skupinu — např. `ZaznamyController.*` partials dohromady)

### Per-controller finish checklist

- [ ] AppController
- [ ] CiselnikyController (11× IsSuperAdmin — přepsat na policy "permission:ciselniky.edit")
- [ ] DashboardController
- [ ] DokumentaceController
- [ ] ExportController (policy "permission:export.pdf"/export.word — už upraveno v C3, jen přidat atribut)
- [ ] HomeController
- [ ] JednaniController (HasGlobalMeetingOverviewAccess → policy)
- [ ] NastaveniController (← velký zásah, 14 actions: SettingsView/SettingsManage)
- [ ] NavrhyController
- [ ] ObsazeniController
- [ ] OsobyController (policy "permission:people.manage")
- [ ] ProfilController
- [ ] ProjectDashboardController (policy "permission:dashboard.view" — už C4)
- [ ] ProjektyController (a partials)
- [ ] SearchController (policy "permission:search.reindex" — už C6)
- [ ] VyzvyController
- [ ] ZaznamyController (a partials)

### Commit pattern

Cílit 3-5 commitů dohromady pro malé controllery, 1 commit per velký:

```bash
git commit -m "refactor(authz): AppController + HomeController + StyleGuideController na policy"
git commit -m "refactor(authz): CiselnikyController — nahradit 11 IsSuperAdmin za policy"
git commit -m "refactor(authz): ZaznamyController + partials na policy"
# ...atd
```

### Odhad

~10 commitů, každý 15-30 min. Total: 3-5 hodin.

---

## Task D6: Refactor 14 services — použít `IAuthorizationService`

**Files:** 14 services, viz inventory:
- `PmTracker.Web/Services/Common/CommentAuthorizationPolicy.cs`
- `PmTracker.Web/Services/Common/PermissionEvaluationService.cs`
- `PmTracker.Web/Services/Dashboard/DashboardService.cs`
- `PmTracker.Web/Services/Data/HarmonogramCatalogService.cs`
- `PmTracker.Web/Services/Dictionaries/DictionarySecurityPolicy.cs`
- `PmTracker.Web/Services/Dictionaries/DictionaryService.Commands.cs`
- `PmTracker.Web/Services/Dictionaries/DictionaryService.Queries.cs`
- `PmTracker.Web/Services/Profile/ProfileService.PageQueries.cs`
- `PmTracker.Web/Services/RecordService.SaveRecord.cs`
- `PmTracker.Web/Services/Search/DbSuggestService.cs`
- `PmTracker.Web/Services/Search/GlobalSearchService.cs`
- `PmTracker.Web/Services/Search/SearchAcl.cs` (ponechat — custom ACL)
- `PmTracker.Web/Services/Settings/SettingsAuthzQueries.cs`
- `PmTracker.Web/Services/Settings/UserAuthorizationSnapshotBuilder.cs` (→ nahradit za AuthorizationSnapshotBuilder z D2)

### Přístup

Pro každou service:
1. Injectnout `IAuthorizationService` do konstruktoru
2. Najít `IsSuperAdmin` / `Grants.Any` / `PermissionGrants` checky
3. Nahradit za `await _authz.HasPermissionAsync(osobaId, key, projektId, ct)` nebo `RequirePermissionAsync`
4. Commit per service

### UserAuthorizationSnapshotBuilder.cs — speciální případ

Tato třída v `Services/Settings/` staví **jiný** snapshot (audit log detail pro UI). Přejmenovat aby nesplývala s novým `AuthorizationSnapshot`:
- Přejmenovat `UserAuthorizationSnapshotBuilder` → `UserAuthorizationAuditSnapshotBuilder`
- Přejmenovat `UserAuthorizationSnapshot` → `UserAuthorizationAuditSnapshot`

### Odhad

~14 commitů, total ~4-5 hodin.

---

## Task D7: `BaseController` helper + unused helpers cleanup

**Cíl:** V `BaseController.cs` smazat ad-hoc `HasPermission` helper (pokud existuje) — nahradí ho policy handler. Ponechat jen `CurrentUserContext` accessor.

**Files:**
- Modify: `PmTracker.Web/Controllers/BaseController.cs`
- Delete: `PmTracker.Web/Services/Common/PermissionEvaluationService.cs` + interface (pokud po D6 je unused)
- Delete: `PmTracker.Web/Services/Common/CommentAuthorizationPolicy.cs` + interface (pokud po D6 je unused)

### Kroky

1. Test — `BaseController.cs` neobsahuje `HasPermission` helper (jen `CurrentUserContext` property + policy handler volaný přes atribut)
2. Smazat nepotřebné helpery
3. Smazat `PermissionEvaluationService` pokud žádný consumer nezůstane
4. Grep: `grep -rln "IPermissionEvaluationService\|ICommentAuthorizationPolicy" PmTracker.Web/` — pokud 0, smazat
5. Commit

```bash
git commit -m "refactor(authz): smazat zastaralý PermissionEvaluationService + CommentAuthorizationPolicy"
```

---

## Task D8: Architecture test — policy atribut na každé mutating action

**Cíl:** Vynucovat, že žádný `[HttpPost]` / `[HttpPut]` / `[HttpDelete]` action nechybí `[Authorize(Policy=...)]`. Deny by default.

**Files:**
- Create: `PmTracker.Tests.Unit/Architecture/AuthorizationPolicyEnforcementTests.cs`

### Step 1 — Test

```csharp
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PmTracker.Tests.Unit.Architecture;

public sealed class AuthorizationPolicyEnforcementTests
{
    [Fact]
    public void Every_Mutating_Action_MustHave_AuthorizePolicy()
    {
        var assembly = typeof(PmTracker.Web.Program).Assembly;
        var controllerTypes = assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        var violations = new List<string>();
        foreach (var controller in controllerTypes)
        {
            var methods = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                var isMutating =
                    method.GetCustomAttribute<HttpPostAttribute>() != null
                    || method.GetCustomAttribute<HttpPutAttribute>() != null
                    || method.GetCustomAttribute<HttpDeleteAttribute>() != null;

                if (!isMutating) continue;

                var hasAllowAnon = method.GetCustomAttribute<AllowAnonymousAttribute>() != null
                    || controller.GetCustomAttribute<AllowAnonymousAttribute>() != null;

                if (hasAllowAnon) continue;

                var hasPolicy = method.GetCustomAttributes<AuthorizeAttribute>()
                    .Any(a => !string.IsNullOrWhiteSpace(a.Policy));

                if (!hasPolicy)
                {
                    violations.Add($"{controller.FullName}.{method.Name}");
                }
            }
        }

        violations.Should().BeEmpty(
            "každá POST/PUT/DELETE action musí mít [Authorize(Policy = \"permission:...\")] " +
            "nebo [AllowAnonymous]. Seznam chybějících: " + string.Join(", ", violations));
    }
}
```

### Step 2-5 — Verify, Commit

Commit:
```bash
git commit -m "test(authz): architecture test vynucující Authorize(Policy) na mutating actions"
```

---

## Fáze D — Completion Checklist

- [ ] 8+ commitů (D1-D8, některé dohromady)
- [ ] `AuthorizationSnapshot` + `IAuthorizationService` + policy handler + DI wiring
- [ ] 25 controllerů má `[Authorize(Policy=...)]` na všech mutating actions
- [ ] 14 services používá `IAuthorizationService` místo ad-hoc checků
- [ ] Architecture test vynucuje policy atribut
- [ ] Staré helpery smazané (PermissionEvaluationService, CommentAuthorizationPolicy, UserAuthorizationSnapshot*Builder přejmenované)
- [ ] Test suite zelená (~750+ testů)
- [ ] Žádný `IsSuperAdmin` check mimo `AuthorizationSnapshot.HasPermission`

---

# Fáze E+F — Seed-only UI Nastavení + architecture tests + docs

**Cíl fáze:** Smazat editační UI karty v Nastavení (Akce, Role, Role-Akce, Uživatel-Role pro projekt/subsystem scope). Ponechat read-only přehled + přiřazení globálních rolí. Přidat architecture testy vynucující seed-only. Napsat `docs/authorization.md`.

**Odhad:** 1-1.5 dne. 7 tasků.

## Task EF1: Smazat editační actions v `NastaveniController`

**Files:**
- Modify: `PmTracker.Web/Controllers/NastaveniController.cs` — smazat CRUD actions pro Permissions a Roles
- Modify: `PmTracker.Web/Services/Settings/SettingsAuthzCommands.cs` — smazat editační metody, zachovat jen `AssignGlobalRoleAsync` + `RemoveGlobalRoleAsync`
- Test: `PmTracker.Tests.Unit/Authorization/NastaveniReadOnlyTests.cs` (new) — policy tests

### Step 1 — Failing test

```csharp
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class NastaveniReadOnlyTests
{
    [Theory]
    [InlineData("CreatePermission")]
    [InlineData("EditPermission")]
    [InlineData("DeletePermission")]
    [InlineData("CreateRole")]
    [InlineData("EditRole")]
    [InlineData("DeleteRole")]
    [InlineData("SaveRolePermission")]
    [InlineData("RemoveRolePermission")]
    public void NastaveniController_ShouldNotHaveEditingAction(string actionName)
    {
        var controller = typeof(PmTracker.Web.Controllers.NastaveniController);
        controller.GetMethod(actionName)
            .Should().BeNull($"{actionName} musí být smazaná — role/permissions se spravují přes seed v kódu");
    }

    [Fact]
    public void NastaveniController_ShouldKeepGlobalRoleAssignmentAction()
    {
        var controller = typeof(PmTracker.Web.Controllers.NastaveniController);
        controller.GetMethod("AssignGlobalRole")
            .Should().NotBeNull("přiřazení osoby do globální role (APP_ADMIN, READ_ALL, SUPERADMIN) zůstává");
    }
}
```

### Step 2-6 — Provést smazání

V `NastaveniController.cs` smazat metody:
- `CreatePermission`, `EditPermission`, `DeletePermission` (pokud existují)
- `CreateRole`, `EditRole`, `DeleteRole`
- `SaveRolePermission`, `RemoveRolePermission`
- `AssignRoleProjects` (INCLUDE scope mode setup — už nepotřeba)

Ponechat:
- `Index` (listing)
- `AssignGlobalRole` / `RemoveGlobalRole` (pro APP_ADMIN, READ_ALL, SUPERADMIN přiřazení)
- `AssignSuperadmin` / `RemoveSuperadmin` (ObligatoryAuthzSuperadmins — zůstává)

V `SettingsAuthzCommands.cs` smazat odpovídající metody. Test suite musí být zelený (policy tests v D8 zachytí i chybějící actions).

Commit:
```bash
git commit -m "refactor(authz): smazat editační NastaveniController actions (seed-only)"
```

---

## Task EF2: Smazat modal views + view models

**Files:**
- Delete: `PmTracker.Web/Views/Nastaveni/PermissionModal.cshtml`
- Delete: `PmTracker.Web/Views/Nastaveni/RoleModal.cshtml`
- Delete: `PmTracker.Web/Views/Nastaveni/RolePermissionModal.cshtml`
- Modify: `PmTracker.Web/Views/Nastaveni/UserRolesModal.cshtml` (omezit jen na globální role)
- Delete (nebo cleanup): view models v `PmTracker.Web/Models/ViewModels/Settings/*ModalViewModel.cs` (pokud existují)

### Kroky

1. `git rm` views, které jsou zbytečné
2. V `UserRolesModal.cshtml` odstranit dropdown pro project/subsystem scope; ponechat jen Global role dropdown
3. Grep nepoužitých view models: `grep -rln "PermissionModalViewModel\|RoleModalViewModel" PmTracker.Web/`
4. Pokud 0 usages → smazat
5. Commit

```bash
git commit -m "refactor(authz): smazat editační modal views (Nastavení read-only)"
```

---

## Task EF3: `Nastaveni/Index.cshtml` a `_DetailPanel.cshtml` na read-only

**Files:**
- Modify: `PmTracker.Web/Views/Nastaveni/Index.cshtml`
- Modify: `PmTracker.Web/Views/Nastaveni/_DetailPanel.cshtml`

### Kroky

1. V Index.cshtml smazat:
   - Tlačítka "Přidat akci", "Přidat roli", "Upravit", "Smazat"
   - JavaScript handlers pro modaly které už neexistují
2. Přidat banner:
   ```html
   <div class="pm-info-banner">
     <strong>Read-only pohled:</strong> Role a permission keys jsou definované v kódu aplikace
     (<code>PermissionSeedConfiguration.cs</code>). Přidání nové role/key vyžaduje PR a nasazení.
     <a href="/docs/authorization.md" target="_blank">Dokumentace</a>.
   </div>
   ```
3. V `_DetailPanel.cshtml` smazat akční tlačítka; ponechat jen čtení.

Commit:
```bash
git commit -m "ui(authz): Nastavení Index a DetailPanel na read-only s info bannerem"
```

---

## Task EF4: `UserRolesModal.cshtml` — jen globální role

**Files:**
- Modify: `PmTracker.Web/Views/Nastaveni/UserRolesModal.cshtml`
- Modify: `PmTracker.Web/Services/Settings/SettingsAuthzQueries.cs` — vrátit jen Global role v seznamu pro UI

### Kroky

1. Filtrovat role v query: `role.Scope == RoleScope.Global` (předtím všechny)
2. V UI skrýt `Scope` dropdown (všechno Global)
3. Test, že seznam rolí v modalu obsahuje jen SUPERADMIN, APP_ADMIN, READ_ALL

Commit:
```bash
git commit -m "ui(authz): UserRolesModal jen pro globální role"
```

---

## Task EF5: Architecture testy — seed je zdroj pravdy

**Files:**
- Create: `PmTracker.Tests.Unit/Architecture/SeedSourceOfTruthTests.cs`

### Testy

```csharp
using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Architecture;

public sealed class SeedSourceOfTruthTests
{
    [Fact]
    public void Every_PermissionKeys_Constant_MustBe_Seeded()
    {
        var keysFromConstants = PermissionKeys.AllDefinitions.Select(d => d.Key).OrderBy(x => x).ToArray();
        var keysFromSeed = PermissionSeedConfiguration.Actions.Select(a => a.Klic).OrderBy(x => x).ToArray();

        keysFromConstants.Should().BeEquivalentTo(keysFromSeed,
            "každá konstanta v PermissionKeys musí být v seed Actions — a naopak");
    }

    [Fact]
    public void Every_Seed_Role_MustHave_At_Least_One_Mapping_Or_BeDocumented()
    {
        var rolesInSeed = PermissionSeedConfiguration.Roles.Select(r => r.Kod).ToHashSet();
        var rolesInMappings = PermissionSeedConfiguration.RoleMappings.Select(m => m.RoleKod).ToHashSet();

        var rolesWithoutMappings = rolesInSeed.Except(rolesInMappings).ToArray();

        // Role bez mappingů = musí být dokumentovány jako záměr (HOST, METODIK do C2 neměl; po C2 už mají)
        rolesWithoutMappings.Should().BeEmpty(
            "každá seed role musí mít alespoň jeden permission mapping. Pokud má být prázdná, zdokumentuj v glossary.");
    }

    [Fact]
    public void No_Controller_Should_Compose_Role_In_Runtime()
    {
        // Vyhledat vzor "new AuthzRoleEntity" v controllerech — zakázáno po E+F
        var controllerSources = Directory.GetFiles(
            ResolveProjectRoot() + "/PmTracker.Web/Controllers",
            "*.cs",
            SearchOption.AllDirectories);

        foreach (var file in controllerSources)
        {
            var code = File.ReadAllText(file);
            code.Should().NotContain("new AuthzRoleEntity",
                $"{file}: role se vytváří jen přes seed, ne přes controller");
            code.Should().NotContain("new AuthzRolePermissionEntity",
                $"{file}: permission mapping se vytváří jen přes seed, ne přes controller");
        }
    }

    private static string ResolveProjectRoot()
    {
        var current = new DirectoryInfo(Environment.CurrentDirectory);
        while (current != null && !current.GetDirectories("PmTracker.Web").Any())
            current = current.Parent;
        return current!.FullName;
    }
}
```

Commit:
```bash
git commit -m "test(authz): architecture testy vynucující seed jako zdroj pravdy"
```

---

## Task EF6: `SettingsAuthzCommands` cleanup — jen global role assignment

**Files:**
- Modify: `PmTracker.Web/Services/Settings/SettingsAuthzCommands.cs`

### Kroky

Smazat metody:
- `CreatePermissionAsync`, `UpdatePermissionAsync`, `DeletePermissionAsync`
- `CreateRoleAsync`, `UpdateRoleAsync`, `DeleteRoleAsync`
- `SaveRolePermissionAsync`, `RemoveRolePermissionAsync`
- `AssignRoleProjectsAsync`

Ponechat:
- `AssignUserRoleAsync` (upravit: jen pro Global scope role)
- `RemoveUserRoleAsync`

Commit:
```bash
git commit -m "refactor(authz): SettingsAuthzCommands — jen global role assignment"
```

---

## Task EF7: Dokumentace `docs/authorization.md`

**Files:**
- Create: `docs/authorization.md`

### Obsah

```markdown
# PM Tracker — Autorizační systém

Autorizace je řízena **seed-only RBAC** modelem: všechny role a permission keys
jsou definované v kódu, admin UI je read-only + přiřazení globálních rolí.

## Datový model

(diagram: AuthzRoles × AuthzPermissions × AuthzRolePermissions + scope discriminator)

## Tři pojmy, nezaměňovat

- `RoleScope`: úroveň role (Global/Project/Subsystem)
- `PermissionScopeLevel`: úroveň key (Global/Project)
- `ScopeMode`: šířka grantu (All/Include/Own/Subsystem)

## Jak přidat nový permission key

1. Přidat konstantu do `PermissionKeys.cs`
2. Přidat do `Definitions` array
3. Přidat do `PermissionSeedConfiguration.Actions`
4. Namapovat na role v `PermissionSeedConfiguration.RoleMappings`
5. Použít v kódu přes `[Authorize(Policy = "permission:xxx.yyy")]`
6. PR + review + deploy → seed upsertne do DB

## Jak přidat novou roli

1. Přidat do `PermissionSeedConfiguration.Roles` s správným `RoleScope`
2. Přidat mappingy do `RoleMappings`
3. (Project/Subsystem scope) přidat i do `CiselnikRoliProjektu` nebo `CiselnikRoliSubsystemu` lookup tabulky (prod DB seed data)
4. PR + review + deploy

## Jak zkontrolovat oprávnění v kódu

```csharp
// Controller
[HttpPost]
[Authorize(Policy = "permission:records.edit")]
public async Task<IActionResult> Edit(int projektId, ...) { ... }

// Service
public async Task Save(int projektId, ...) {
    await _authz.RequirePermissionAsync(userId, PermissionKeys.RecordsEdit, projektId);
    // ...
}
```

## Rozhodovací tok

```
HTTP request → ASP.NET Authorization middleware
  → PermissionAuthorizationHandler
  → IAuthorizationService.HasPermissionAsync
  → AuthorizationSnapshot.HasPermission (in-memory)
    → SuperAdmin? → true
    → GlobalPermissions contains key? → true
    → PerProjectPermissions[projektId] contains key? → true
    → PerSubsystemPermissions[subsystemId] contains key? → true
    → false
```

## FAQ

### Jak smažu roli?

Smažeš ji ze seedu (PR) a deploy — `authz.roles.is_active=0` + cleanup migrace
odstraní orphaned mappings.

### Jak udělám custom roli pro jediný projekt?

Nelze — role jsou globální pattern. Místo toho:
1. Přidej novou "šablonu" role (např. `AUDITOR_PROJEKTU`) přes PR
2. V UI (Projekt → Tým) přiřaď osobu do této role na konkrétní projekt

### Co když potřebuji rychlý hotfix produkce?

Nouzová obtížka je přidat osobu do `AuthzSuperadmins` — jediný "bypass" v systému.
Nepoužívej jako trvalé řešení; normalizuj přes PR jakmile je čas.
```

Commit:
```bash
git commit -m "docs(authz): kompletní dokumentace autorizačního systému"
```

---

## Fáze E+F — Completion Checklist

- [ ] 7 commitů (EF1-EF7)
- [ ] Editační NastaveniController actions smazané
- [ ] 3 modal views smazané (Permission, Role, RolePermission)
- [ ] UserRolesModal omezený na Global scope
- [ ] Index.cshtml read-only s info bannerem
- [ ] SettingsAuthzCommands has only global role assignment
- [ ] Architecture testy (SeedSourceOfTruthTests)
- [ ] docs/authorization.md komplet
- [ ] Full test suite zelená (~760+ testů)

---

# Celkový Definition of Done — po Fázi F

- [ ] `AuthorizationSnapshot` + `IAuthorizationService` jediná cesta pro auth rozhodnutí
- [ ] 25 controllerů používá `[Authorize(Policy="permission:...")]`
- [ ] 14 services používá `IAuthorizationService`
- [ ] Seed (PermissionSeedConfiguration) = jediný zdroj pravdy pro role + keys + mappingy
- [ ] 22 permission keys (14 původních + 8 nových)
- [ ] 11 system rolí (SUPERADMIN, APP_ADMIN, READ_ALL, 5 projektových, 3 subsystémové)
- [ ] 4 security holes zavřené (Export, Dashboard, Comments, Search.Reindex)
- [ ] Všechny ad-hoc `IsSuperAdmin` checky mimo snapshot smazané
- [ ] `ProjectRolePermissionGrantBuilder` + `SubsystemRolePermissionGrantBuilder` smazané
- [ ] Editační UI smazané; admin má jen read-only přehled + global role assignment
- [ ] 3 DB migrace zelené (`db_upgrade_1_2_0`, `1_2_1`, `1_3_0`)
- [ ] Architecture testy vynucující policy atribut + seed jako zdroj pravdy
- [ ] `docs/authorization.md` dokumentuje celý systém
- [ ] Full test suite ~760+ zelených
- [ ] Žádná regrese pro běžné uživatele (nebo naopak opravené behavior — VLASTNIK má konečně plné granty)

---

# Přechodová risk-log

| Risk | Pravděpodobnost | Dopad | Mitigace |
|---|---|---|---|
| Uživatel se `"SUPERADMIN"` rolí bez záznamu v `AuthzSuperadmins` ztratí super-admin status | Střední | Vysoký | Před deploy B4 vyběhnout SQL: `SELECT o.jmeno, o.prijmeni FROM osoby o WHERE EXISTS (role s Kod=SUPERADMIN) AND NOT EXISTS (AuthzSuperadmins záznam)` — manuálně přidat do AuthzSuperadmins |
| VLASTNIK_PROJEKTU / GEST / ADM_PROJ / PROJ_MAN dostanou nově granty, což odhalí ztajené business rules v UI | Střední | Střední | Ve Fázi B3 safety testy potvrdí; pokud UI bug = oprava v UI, ne regrese granty |
| Custom role v produkci před Fázi F budou po cleanup migraci `is_active=0` | Vysoká (pokud admin vytvářel) | Nízký | User řekl, že používá jen APP_ADMIN + READ_ALL (oboje v seedu); ověřit SQL před deploy |
| 25 controller refactorů = velké surface area pro regrese | Vysoká | Střední | Architecture test (D8) + průběžné full test suite runs po každém commitu; subagent-driven workflow s reviewy |
| InMemory EF provider se chová jinak než SQL Server pro LINQ | Nízká | Střední | Integration tests s real SQL ve Fázi D (pokud detekujeme problém) |
| Policy handler nečte project/subsystem ID z route, pokud není v URL | Střední | Střední | Explicit dokumentace + route convention (všechny project actions mají `projektId` v URL); service-layer `RequirePermissionAsync` pro non-route scénáře |

---

# Rozhodovací strom pro executing worker

Po Fázi B je hotovo → chceš pokračovat?

- **Ano, pokračujeme** → Task C1 (nové permission keys)
- **Review prvně** → spustit full test suite, prohlédnout Fázi B commity (4 ks), diff oproti `fd95ead`
- **Pauza** → commity připravené, plán pokračuje od C1 kdykoliv

Po Fázi C → chceš pokračovat?

- Stejná otázka, stejné možnosti

Každá fáze je merge-safe — aplikace po každé funguje buď identicky, nebo lépe. Fázi lze pauzovat na hranici fáze (ne uprostřed).
