# Authorization Unification — Fáze B0 → F Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dokončit autorizační sjednocení: zavést type-safe enumy, přehodit rezolvér na DB-driven data, vyplnit permission gaps, přidat `IAuthorizationService` fasádu, a **zrušit ruční UI kompozici rolí** — autoritativním zdrojem jsou seed konfigurace v kódu + `ObsazeniProjektu`/`ObsazeniSubsystemuProjektu` pro kontext.

**Architecture:** Seed-only RBAC — všechny role (GLOBAL/PROJECT/SUBSYSTEM) definované v `PermissionSeedConfiguration`, verzované v gitu, review v PR. Admin UI "Nastavení" je po Fázi F read-only (přehled rolí + přiřazení uživatelů do GLOBAL rolí). Bezpečnost vynucována na dvou vrstvách: ASP.NET Core Policy handler + service-layer `IAuthorizationService.RequirePermissionAsync`.

**Tech Stack:** ASP.NET Core 8, EF Core 8 (enumy přes `HasConversion<string>()`), xUnit + FluentAssertions, SQL Server 2019+ (idempotent `db_upgrade_*.sql`).

**Návaznost:**
- Spec: [docs/superpowers/specs/2026-04-21-authorization-unification-design.md](../specs/2026-04-21-authorization-unification-design.md)
- Předchozí plán: [docs/superpowers/plans/2026-04-21-authorization-unification.md](2026-04-21-authorization-unification.md) — Fáze A dokončena (commity `da5e271` → `beaa1e4`).

**Pivot oproti původnímu plánu (rozhodnuto 2026-04-22):**
1. **Type safety** — zavést enumy `RoleScope`, `ScopeMode`, `PermissionScopeLevel` + EF `HasConversion<string>()`. Stringy v DB zůstávají, ale C# kód pracuje s enumy. Fáze B0.
2. **Seed-only** — zrušit admin UI pro ruční kompozici rolí z permission keys. Všechny role = seed + PR review. Dopad: Fáze E+F se dramaticky zmenší, Fáze C přidá `READ_ALL` roli do seedu + cleanup orphaned DB rows.
3. **Glossary** — tři podobné pojmy (`Scope` na roli, `ScopeMode` na mappingu, `ScopeLevel` na keys) řádně dokumentované v XML docs + hlavičkovém komentáři seed souboru.

**Pořadí fází:** B0 → B → C → D → E+F. Každá fáze je merge-safe.

---

## Přehled fází a stav

| Fáze | Scope | Status | Detail |
|---|---|---|---|
| **A** | Infrastructure (DB migrace + seed) | ✅ Dokončeno (10 commitů) | [původní plán](2026-04-21-authorization-unification.md) |
| **B0** | Type safety — RoleScope/ScopeMode/ScopeLevel enumy + glossary | 🔴 Tento dokument, detail | §B0 |
| **B** | Resolver swap — DB-driven granty, smazat builders + hardcoded SUPERADMIN | 🔴 Tento dokument, detail | §B |
| **C** | Gap fixes — nové keys + READ_ALL + security holes + cleanup | 🔴 Tento dokument, sketch | §C — detail po B |
| **D** | IAuthorizationService + policy handler + service-layer guard | 🔴 Tento dokument, sketch | §D — detail po C |
| **E+F** | Seed-only UI Nastavení (read-only) + architecture tests + docs | 🔴 Tento dokument, sketch | §E+F — detail po D |

**Důvod postupného detailování:** po Fázi B bude jasnější, jak přesně dekomponovat Fáze C-F. Detail se píše po dokončení předchozí fáze, aby odpovídal aktuálnímu stavu kódu.

---

# Fáze B0 — Type safety + glossary (non-breaking)

**Cíl fáze:** Nahradit volné stringy (`"GLOBAL"`, `"ALL"`, `"PROJECT"`...) v C# kódu enumy s EF `HasConversion<string>()`. V DB zůstává všechno stejné (NVARCHAR sloupce, CHECK constraints), ale C# typo = compile error. Přidat XML docs a hlavičkový komentář ve `PermissionSeedConfiguration.cs` vysvětlující tři scope pojmy.

**Odhad:** ~3-4 h. 4 tasky. Každý task je komplexní refactor napříč několika soubory, ale mechanický.

**Merge safety:** Po každém tasku je aplikace plně funkční (enumy jsou drop-in replacement pro stringy).

**Předchozí stav (po Fázi A):**
- `AuthzRoleEntity.Scope` = `string` (dnes "GLOBAL" / "PROJECT" / "SUBSYSTEM")
- `AuthzRolePermissionEntity.ScopeMode` = `string` (dnes "ALL")
- `AuthzPermissionEntity.ScopeLevel` = `string` (dnes "GLOBAL" / "PROJECT")
- `RoleSeedItem.Scope` = `string`
- `RoleActionSeedItem.ScopeMode` = `string`
- `ActionSeedItem.ScopeLevel` = `string`

## Task B0-1: Enum `RoleScope` + refactor `AuthzRoleEntity.Scope`

**Files:**
- Create: `PmTracker.Web/Services/Security/RoleScope.cs`
- Modify: `PmTracker.Web/Models/Entities/PmTrackerEntities.cs` (AuthzRoleEntity.Scope type)
- Modify: `PmTracker.Web/Data/Configuration/AuthorizationEntityConfiguration.cs` (AuthorizationRoleEntityConfiguration — add HasConversion)
- Modify: `PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs` (RoleSeedItem.Scope + all `new(...)` rows)
- Modify: `PmTracker.Web/Services/Security/PermissionSeeder.cs` (UpsertRolesAsync — no string comparison changes needed; assigning enum to property works)
- Modify: `PmTracker.Tests.Unit/Authorization/AuthzRoleScopeTests.cs` (assertions with enum)
- Modify: `PmTracker.Tests.Unit/Authorization/RoleSeedCoverageTests.cs` (assertions with enum)
- Modify: `PmTracker.Tests.Unit/Authorization/SeedEndToEndTests.cs` (assertions with enum)
- Modify: `PmTracker.Tests.Unit/Authorization/RoleCatalogLinkerTests.cs` (test data construction)

- [ ] **Step 1: Vytvořit enum `RoleScope`**

Soubor: `PmTracker.Web/Services/Security/RoleScope.cs`

```csharp
namespace PmTracker.Web.Services.Security;

/// <summary>
/// Úroveň role — určuje, jakým způsobem se role přiřazuje k osobě.
/// </summary>
/// <remarks>
/// Hodnoty se v DB uloží jako string přes EF <c>HasConversion&lt;string&gt;()</c>,
/// aby zůstala čitelnost + `CHECK` constraint (viz <c>db_upgrade_1_2_0_authz_role_scope.sql</c>).
///
/// Tři pojmy v autorizačním modelu, nezaměňovat:
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="RoleScope"/> (tento typ) — úroveň role. Určuje, JAK se role přiřazuje.
/// Příklad: <c>VLASTNIK_PROJEKTU</c> má <see cref="RoleScope.Project"/> → přiřazuje se přes
/// <c>ObsazeniProjektu</c> na konkrétní projekt.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="PermissionScopeLevel"/> — úroveň permission key. Určuje, zda key potřebuje
/// projektový kontext. Příklad: <c>projects.edit</c> je <see cref="PermissionScopeLevel.Project"/>,
/// <c>people.manage</c> je <see cref="PermissionScopeLevel.Global"/>.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="ScopeMode"/> — šířka grantu v konkrétním role→permission mappingu.
/// Příklad: <c>(ADM_PROJ, records.edit, ALL)</c> = ADM_PROJ smí editovat všechny záznamy
/// na projektech, kde má tuto roli.
/// </description>
/// </item>
/// </list>
/// </remarks>
public enum RoleScope
{
    /// <summary>Globální role — přiřazená osobě přímo v <c>authz.user_roles</c>. Platí všude.</summary>
    Global,

    /// <summary>Projektová role — přiřazená přes <c>ObsazeniProjektu</c>. Platí na jednom projektu.</summary>
    Project,

    /// <summary>Subsystémová role — přiřazená přes <c>ObsazeniSubsystemuProjektu</c>. Platí na jednom subsystému.</summary>
    Subsystem
}
```

- [ ] **Step 2: Přepsat failing test**

Přepsat `PmTracker.Tests.Unit/Authorization/AuthzRoleScopeTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class AuthzRoleScopeTests
{
    [Fact]
    public void AuthzRoleEntity_ShouldHaveScopeProperty()
    {
        var role = new AuthzRoleEntity
        {
            Kod = "TEST",
            Nazev = "Test",
            Scope = RoleScope.Project
        };

        role.Scope.Should().Be(RoleScope.Project);
    }

    [Fact]
    public void AuthzRoleEntity_Scope_ShouldDefaultToGlobal()
    {
        var role = new AuthzRoleEntity();
        role.Scope.Should().Be(RoleScope.Global);
    }
}
```

- [ ] **Step 3: Spustit test — FAIL (compile error)**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~AuthzRoleScopeTests" -v q
```

Expected: FAIL — `CS0029: Cannot implicitly convert type 'PmTracker.Web.Services.Security.RoleScope' to 'string'`.

- [ ] **Step 4: Změnit `AuthzRoleEntity.Scope` na `RoleScope`**

V [PmTracker.Web/Models/Entities/PmTrackerEntities.cs](../../PmTracker.Web/Models/Entities/PmTrackerEntities.cs) najít `AuthzRoleEntity` a změnit property. Také přidat `using`:

```csharp
using PmTracker.Web.Services.Security;

// ... uvnitř souboru ...

public sealed class AuthzRoleEntity
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Nazev { get; set; } = string.Empty;
    public string? Popis { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; }
    public RoleScope Scope { get; set; } = RoleScope.Global;   // změněno z string
}
```

- [ ] **Step 5: Přidat EF conversion**

V [PmTracker.Web/Data/Configuration/AuthorizationEntityConfiguration.cs](../../PmTracker.Web/Data/Configuration/AuthorizationEntityConfiguration.cs), v `AuthorizationRoleEntityConfiguration.Configure`, upravit mapping `Scope`:

```csharp
builder.Property(x => x.Scope)
    .HasColumnName("scope")
    .HasMaxLength(16)
    .IsRequired()
    .HasConversion<string>();   // přidáno: enum ↔ string v DB
```

- [ ] **Step 6: Aktualizovat `RoleSeedItem` record a seed rows**

V [PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs](../../PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs) změnit record signaturu + všechny `new(...)` rows:

```csharp
public sealed record RoleSeedItem(string Kod, string Nazev, string Popis, bool IsSystem, RoleScope Scope);

// Dále ve `Roles` listu:
public static readonly IReadOnlyList<RoleSeedItem> Roles =
[
    // Globální role
    new("SUPERADMIN", "Superadmin", "Pevná role s plnými oprávněními.", true, RoleScope.Global),
    new("APP_ADMIN", "Administrátor aplikace", "Správa aplikace a základních entit.", true, RoleScope.Global),

    // Projektové role
    new("VLASTNIK_PROJEKTU", "Vlastník projektu", "Plný vlastník projektu.", true, RoleScope.Project),
    new("ADM_PROJ", "Projektový admin", "Silný projektový admin bez práva měnit metadata.", true, RoleScope.Project),
    new("PROJ_MAN", "Projektový manažer", "Projektový manažer bez úprav metadat.", true, RoleScope.Project),
    new("HOST", "Host", "Read-only host projektu.", true, RoleScope.Project),
    new("GEST", "Gestor", "Gestor s komentovacími právy.", true, RoleScope.Project),

    // Subsystémové role
    new("VEDOUCI_SUBSYSTEMU", "Vedoucí subsystému", "Vedoucí subsystému projektu.", true, RoleScope.Subsystem),
    new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "Zástupce vedoucího subsystému", "Zástupce vedoucího, stejná práva jako vedoucí.", true, RoleScope.Subsystem),
    new("METODIK_SUBSYSTEMU", "Metodik subsystému", "Metodik s komentovacími právy.", true, RoleScope.Subsystem)
];
```

- [ ] **Step 7: Aktualizovat stávající testy**

Najít všechna místa, kde testy porovnávají `Scope` se stringem, a přepsat na enum.

**`RoleSeedCoverageTests.cs`** — přepsat celý soubor:

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class RoleSeedCoverageTests
{
    [Fact]
    public void Roles_ShouldContainAllProjectScopeRoles()
    {
        var projectRoles = PermissionSeedConfiguration.Roles
            .Where(r => r.Scope == RoleScope.Project)
            .Select(r => r.Kod)
            .OrderBy(x => x)
            .ToList();

        projectRoles.Should().BeEquivalentTo(new[]
        {
            "ADM_PROJ", "GEST", "HOST", "PROJ_MAN", "VLASTNIK_PROJEKTU"
        });
    }

    [Fact]
    public void Roles_ShouldContainAllSubsystemScopeRoles()
    {
        var subsystemRoles = PermissionSeedConfiguration.Roles
            .Where(r => r.Scope == RoleScope.Subsystem)
            .Select(r => r.Kod)
            .OrderBy(x => x)
            .ToList();

        subsystemRoles.Should().BeEquivalentTo(new[]
        {
            "METODIK_SUBSYSTEMU", "VEDOUCI_SUBSYSTEMU", "ZASTUPCE_VEDOUCIHO_SUBSYSTEMU"
        });
    }

    [Fact]
    public void Roles_GlobalScope_ShouldContainSuperAdminAndAppAdmin()
    {
        var globalRoles = PermissionSeedConfiguration.Roles
            .Where(r => r.Scope == RoleScope.Global)
            .Select(r => r.Kod)
            .OrderBy(x => x)
            .ToList();

        globalRoles.Should().Contain(new[] { "APP_ADMIN", "SUPERADMIN" });
    }
}
```

**`SeedEndToEndTests.cs`** — v testu `FullSeed_ShouldCreateAllSystemRolesWithScope`, nahradit tři assertion řádky:

```csharp
// Před:
roles.First(r => r.Kod == "ADM_PROJ").Scope.Should().Be("PROJECT");
roles.First(r => r.Kod == "VEDOUCI_SUBSYSTEMU").Scope.Should().Be("SUBSYSTEM");
roles.First(r => r.Kod == "APP_ADMIN").Scope.Should().Be("GLOBAL");

// Po:
roles.First(r => r.Kod == "ADM_PROJ").Scope.Should().Be(RoleScope.Project);
roles.First(r => r.Kod == "VEDOUCI_SUBSYSTEMU").Scope.Should().Be(RoleScope.Subsystem);
roles.First(r => r.Kod == "APP_ADMIN").Scope.Should().Be(RoleScope.Global);
```

Přidat `using PmTracker.Web.Services.Security;` do souboru.

**`RoleCatalogLinkerTests.cs`** — 3 místa, kde testy vytváří `AuthzRoleEntity` se `Scope = "PROJECT"` / `"SUBSYSTEM"` — přepsat na `RoleScope.Project` / `RoleScope.Subsystem`. Přidat `using PmTracker.Web.Services.Security;`.

- [ ] **Step 8: Build + test**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet build PmTracker.Web/PmTracker.Web.csproj -v q
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -v q
```

Expected: build 0 errors, test suite 682/682 PASS (stejný počet jako po Fázi A, ale všechno porovnává enum).

- [ ] **Step 9: Commit**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
git add PmTracker.Web/Services/Security/RoleScope.cs \
        PmTracker.Web/Models/Entities/PmTrackerEntities.cs \
        PmTracker.Web/Data/Configuration/AuthorizationEntityConfiguration.cs \
        PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs \
        PmTracker.Tests.Unit/Authorization/AuthzRoleScopeTests.cs \
        PmTracker.Tests.Unit/Authorization/RoleSeedCoverageTests.cs \
        PmTracker.Tests.Unit/Authorization/SeedEndToEndTests.cs \
        PmTracker.Tests.Unit/Authorization/RoleCatalogLinkerTests.cs
git commit -m "refactor(authz): RoleScope enum + EF HasConversion"
```

---

## Task B0-2: Enum `ScopeMode` + refactor `AuthzRolePermissionEntity.ScopeMode`

**Files:**
- Create: `PmTracker.Web/Services/Security/ScopeMode.cs`
- Modify: `PmTracker.Web/Models/Entities/PmTrackerEntities.cs` (AuthzRolePermissionEntity.ScopeMode type)
- Modify: `PmTracker.Web/Data/Configuration/AuthorizationEntityConfiguration.cs` (AuthorizationRolePermissionEntityConfiguration — add HasConversion)
- Modify: `PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs` (RoleActionSeedItem.ScopeMode + všechny `new(...)` rows v RoleMappings)
- Modify: `PmTracker.Web/Services/Security/PermissionSeeder.cs` (upsert `ScopeMode = mapping.ScopeMode` už funguje — enum se assignuje stejně)

- [ ] **Step 1: Vytvořit enum**

Soubor: `PmTracker.Web/Services/Security/ScopeMode.cs`

```csharp
namespace PmTracker.Web.Services.Security;

/// <summary>
/// Šířka grantu v jednotlivém role → permission mappingu. Určuje, na které entity se grant
/// rozšíří v rámci scope role. Pro GLOBAL role se typicky nepoužívá (nebo je <see cref="All"/>).
/// </summary>
/// <remarks>
/// Ukládá se do DB jako string přes EF <c>HasConversion&lt;string&gt;()</c>.
///
/// Viz glossary v <see cref="PermissionSeedConfiguration"/>.
/// </remarks>
public enum ScopeMode
{
    /// <summary>Platí pro všechny entity v rozsahu role (např. ADM_PROJ = všechny záznamy na projektech, kde má roli).</summary>
    All,

    /// <summary>Platí jen pro entity vlastněné osobou (např. komentáře autored vlastní osobou). Phase C.</summary>
    Own,

    /// <summary>Platí jen pro entity v subsystému (např. METODIK smí komentovat jen záznamy svého subsystému). Phase C.</summary>
    Subsystem
}
```

- [ ] **Step 2: Aktualizovat `AuthzRolePermissionEntity`**

V [PmTracker.Web/Models/Entities/PmTrackerEntities.cs](../../PmTracker.Web/Models/Entities/PmTrackerEntities.cs):

```csharp
public sealed class AuthzRolePermissionEntity
{
    public int Id { get; set; }
    public int RoleId { get; set; }
    public int PermissionId { get; set; }
    public ScopeMode ScopeMode { get; set; } = ScopeMode.All;   // změněno z string
    public bool IsAllowed { get; set; }
}
```

- [ ] **Step 3: EF conversion**

V `AuthorizationRolePermissionEntityConfiguration.Configure` přidat `.HasConversion<string>()` na `ScopeMode`:

```csharp
builder.Property(x => x.ScopeMode)
    .HasColumnName("scope_mode")
    .HasMaxLength(16)
    .IsRequired()
    .HasConversion<string>();
```

(Pokud tam nejsou `HasMaxLength`/`IsRequired` dnes, přidat je současně — sjednotí se s Scope mappingem z B0-1.)

- [ ] **Step 4: Aktualizovat `RoleActionSeedItem` + všechny rows**

V [PermissionSeedConfiguration.cs](../../PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs):

```csharp
public sealed record RoleActionSeedItem(string RoleKod, string ActionKlic, ScopeMode ScopeMode, bool IsAllowed);
```

Nahradit všechny `"ALL"` v `RoleMappings` listu za `ScopeMode.All`. Find-replace (regex):
- Pattern: `, "ALL", true)`
- Replacement: `, ScopeMode.All, true)`

Ověřit: všechny řádky v `RoleMappings` list mají stejný tvar a po replace vypadají takto:

```csharp
new("SUPERADMIN", "projects.create", ScopeMode.All, true),
new("SUPERADMIN", "projects.edit", ScopeMode.All, true),
// ... atd.
```

- [ ] **Step 5: Build + test**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet build PmTracker.Web/PmTracker.Web.csproj -v q
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -v q
```

Expected: build 0 errors, 682/682 PASS. `ProjectRolePermissionMatrixTests` a `SubsystemRolePermissionMatrixTests` nemusí být měněny — testují jen `ActionKlic`, ne `ScopeMode`.

- [ ] **Step 6: Commit**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
git add PmTracker.Web/Services/Security/ScopeMode.cs \
        PmTracker.Web/Models/Entities/PmTrackerEntities.cs \
        PmTracker.Web/Data/Configuration/AuthorizationEntityConfiguration.cs \
        PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs
git commit -m "refactor(authz): ScopeMode enum + EF HasConversion"
```

---

## Task B0-3: Enum `PermissionScopeLevel` + refactor `AuthzPermissionEntity.ScopeLevel`

**Files:**
- Create: `PmTracker.Web/Services/Security/PermissionScopeLevel.cs`
- Modify: `PmTracker.Web/Models/Entities/PmTrackerEntities.cs`
- Modify: `PmTracker.Web/Data/Configuration/AuthorizationEntityConfiguration.cs` (AuthorizationPermissionEntityConfiguration)
- Modify: `PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs` (ActionSeedItem + všechny `Actions` rows)

- [ ] **Step 1: Enum**

Soubor: `PmTracker.Web/Services/Security/PermissionScopeLevel.cs`

```csharp
namespace PmTracker.Web.Services.Security;

/// <summary>
/// Úroveň permission key. Určuje, zda key potřebuje projektový kontext při kontrole.
/// </summary>
/// <remarks>
/// Ukládá se v DB jako string přes <c>HasConversion&lt;string&gt;()</c>.
///
/// Viz glossary v <see cref="PermissionSeedConfiguration"/>.
/// </remarks>
public enum PermissionScopeLevel
{
    /// <summary>Key se kontroluje bez projektového kontextu (např. <c>people.manage</c>, <c>settings.view</c>).</summary>
    Global,

    /// <summary>Key potřebuje konkrétní projekt při kontrole (např. <c>projects.edit</c>, <c>records.edit</c>).</summary>
    Project
}
```

- [ ] **Step 2: Aktualizovat `AuthzPermissionEntity`**

V PmTrackerEntities.cs:

```csharp
public sealed class AuthzPermissionEntity
{
    public int Id { get; set; }
    public string Klic { get; set; } = string.Empty;
    public string Nazev { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public PermissionScopeLevel ScopeLevel { get; set; } = PermissionScopeLevel.Project;   // změněno z string
    public bool IsActive { get; set; }
    public bool IsSystem { get; set; }
}
```

- [ ] **Step 3: EF conversion**

V `AuthorizationPermissionEntityConfiguration.Configure` u `ScopeLevel`:

```csharp
builder.Property(x => x.ScopeLevel)
    .HasColumnName("scope_level")
    .HasMaxLength(16)
    .IsRequired()
    .HasConversion<string>();
```

- [ ] **Step 4: Aktualizovat `ActionSeedItem` + všechny rows**

```csharp
public sealed record ActionSeedItem(string Klic, string Nazev, string CategoryKod, PermissionScopeLevel ScopeLevel);

// V Actions listu (nahradit "GLOBAL"/"PROJECT" stringy enumy):
public static readonly IReadOnlyList<ActionSeedItem> Actions =
[
    new("projects.create", "Vytvářet projekty", "PROJECTS", PermissionScopeLevel.Project),
    new("projects.edit", "Upravovat projekty", "PROJECTS", PermissionScopeLevel.Project),
    new("projects.delete", "Mazat projekty (soft-delete)", "PROJECTS", PermissionScopeLevel.Project),
    new("records.edit", "Upravovat projektové záznamy", "RECORDS", PermissionScopeLevel.Project),
    new("records.schedule.add", "Doplňovat harmonogram úkolu", "RECORDS", PermissionScopeLevel.Project),
    new("records.schedule.edit", "Upravovat harmonogram úkolu", "RECORDS", PermissionScopeLevel.Project),
    new("records.comment.subsystemlead", "Přidávat vyjádření jako vedoucí subsystému", "RECORDS", PermissionScopeLevel.Project),
    new("meetings.create", "Zakládat jednání", "MEETINGS", PermissionScopeLevel.Project),
    new("meetings.edit", "Upravovat jednání", "MEETINGS", PermissionScopeLevel.Project),
    new("team.manage", "Správa týmu projektu", "PROJECTS", PermissionScopeLevel.Project),
    new("people.manage", "Správa osob", "MASTER", PermissionScopeLevel.Global),
    new("ciselniky.edit", "Editace číselníků", "MASTER", PermissionScopeLevel.Global),
    new("settings.view", "Zobrazit nastavení", "SETTINGS", PermissionScopeLevel.Global),
    new("settings.manage", "Správa nastavení", "SETTINGS", PermissionScopeLevel.Global)
];
```

- [ ] **Step 5: Build + test**

```bash
dotnet build PmTracker.Web/PmTracker.Web.csproj -v q
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -v q
```

Expected: 0 errors, 682/682 PASS.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/Security/PermissionScopeLevel.cs \
        PmTracker.Web/Models/Entities/PmTrackerEntities.cs \
        PmTracker.Web/Data/Configuration/AuthorizationEntityConfiguration.cs \
        PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs
git commit -m "refactor(authz): PermissionScopeLevel enum + EF HasConversion"
```

---

## Task B0-4: Glossary komentář v `PermissionSeedConfiguration.cs`

**Files:**
- Modify: `PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs` (add file-header comment)
- Create: `PmTracker.Tests.Unit/Authorization/PermissionSeedConfigurationGlossaryTests.cs` (validate glossary present)

- [ ] **Step 1: Failing test**

Soubor: `PmTracker.Tests.Unit/Authorization/PermissionSeedConfigurationGlossaryTests.cs`

```csharp
using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class PermissionSeedConfigurationGlossaryTests
{
    [Fact]
    public void PermissionSeedConfiguration_ShouldContainGlossaryHeader()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs"));

        code.Should().Contain("GLOSSARY",
            "soubor musí obsahovat glossary vysvětlující rozdíl mezi RoleScope, ScopeMode, PermissionScopeLevel");
        code.Should().Contain("RoleScope");
        code.Should().Contain("ScopeMode");
        code.Should().Contain("PermissionScopeLevel");
        code.Should().Contain("ObsazeniProjektu",
            "glossary musí zmínit, kde se projektové role přiřazují");
    }
}
```

- [ ] **Step 2: FAIL**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "PermissionSeedConfigurationGlossaryTests" -v q
```

Expected: FAIL — soubor neobsahuje `GLOSSARY`.

- [ ] **Step 3: Přidat glossary header**

Na začátek `PermissionSeedConfiguration.cs` (před `namespace`), vložit:

```csharp
// =============================================================================
// GLOSSARY — autorizační model PM Trackeru
// =============================================================================
//
// Tento soubor je JEDNIM ze DVOU autoritativních zdrojů autorizace:
//   (1) PermissionSeedConfiguration (tento soubor) — katalog rolí, permission
//       keys, a mappingů mezi nimi. Verzovaný v gitu, review v PR.
//   (2) ObsazeniProjektu + ObsazeniSubsystemuProjektu — DB tabulky, kam admin
//       v UI přiřazuje konkrétní osoby do projektových/subsystémových rolí.
//
// Manuální UI kompozice rolí z permission keys (dříve tab "Akce/Role/Uživatel-Role"
// v Nastavení) byla zrušena — role jsou nyní definované POUZE v seedu tohoto
// souboru. Důvod: audit trail v gitu, review four-eyes, žádné drift mezi
// seedem a runtime. Viz spec §3 a plán 2026-04-22-authz-phases-b-to-f.md.
//
// TŘI PODOBNÉ POJMY — NEZAMĚŇOVAT:
//
//   RoleScope            — úroveň ROLE. Určuje, JAK se role přiřazuje:
//                           - Global     → přes authz.user_roles (přímo)
//                           - Project    → přes ObsazeniProjektu (na projekt)
//                           - Subsystem  → přes ObsazeniSubsystemuProjektu (na subsystém)
//                         Příklad: VLASTNIK_PROJEKTU má RoleScope.Project.
//
//   PermissionScopeLevel — úroveň permission KEY. Určuje, zda key potřebuje
//                          projektový kontext při kontrole:
//                           - Global   → nepotřebuje projektId (např. people.manage)
//                           - Project  → potřebuje projektId (např. projects.edit)
//                         Příklad: records.edit má PermissionScopeLevel.Project.
//
//   ScopeMode            — šířka grantu v role→permission MAPPINGU:
//                           - All       → všechny entity v rozsahu role (99% mappingů)
//                           - Own       → jen entity vlastněné osobou (Phase C, comments)
//                           - Subsystem → jen entity v subsystému osoby (Phase C)
//                         Příklad: (ADM_PROJ, records.edit, All) = smí editovat
//                         VŠECHNY záznamy na projektech, kde má ADM_PROJ.
//
// Matice platných kombinací (zjednodušeně):
//
//   RoleScope  │ PermissionScopeLevel │ ScopeMode typicky
//   ───────────┼──────────────────────┼──────────────────
//   Global     │ Global, Project      │ All
//   Project    │ Project              │ All, Own
//   Subsystem  │ Project              │ All, Subsystem, Own
//
// Resolver (UserContextResolver) interpretuje kombinaci Scope × ScopeMode tak,
// aby vrátil správnou množinu projektId/subsystemId, kde grant platí.
// =============================================================================
```

Přidat `using PmTracker.Web.Services.Security;` pokud už není (kvůli enumům).

- [ ] **Step 4: PASS**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "PermissionSeedConfigurationGlossaryTests" -v q
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -v q
```

Expected: glossary test PASS, full suite 683/683.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs \
        PmTracker.Tests.Unit/Authorization/PermissionSeedConfigurationGlossaryTests.cs
git commit -m "docs(authz): glossary komentář v PermissionSeedConfiguration"
```

---

## Fáze B0 — Completion Checklist

- [ ] 4 commity: B0-1 (RoleScope), B0-2 (ScopeMode), B0-3 (PermissionScopeLevel), B0-4 (glossary)
- [ ] 3 enumy v `PmTracker.Web/Services/Security/`, každý s XML docs
- [ ] EF `HasConversion<string>()` na všech třech mapping pozicích
- [ ] Seed rows + testy používají enumy, ne stringy
- [ ] Glossary header na začátku `PermissionSeedConfiguration.cs`
- [ ] Full test suite zelená (683 testů)
- [ ] Aplikace se chová identicky — žádný funkční dopad, jen type safety

---

# Fáze B — Resolver swap (DB-driven)

**Cíl fáze:** `UserContextResolver` přestane volat `ProjectRolePermissionGrantBuilder` + `SubsystemRolePermissionGrantBuilder` (C# code-driven) a místo toho načítá implicit granty JOIN-em přes FK `CiselnikRoli*.AuthzRoleId` → `AuthzRolePermission`. Smazat oba buildery a hardcoded `"SUPERADMIN"` fallback na řádku 242. Po Fázi B je seed-driven cesta SINGLE SOURCE OF TRUTH.

**Odhad:** 1 den. 4 tasky.

**Předpoklad:** Fáze A + B0 hotové. `RoleCatalogLinker` už naplnil `CiselnikRoliProjektu.AuthzRoleId` / `CiselnikRoliSubsystemu.AuthzRoleId` FK.

**Merge safety:** Každý task udržuje aplikaci funkční. Migration safety test v B3 vynucuje grant-level ekvivalenci před/po.

## Task B1: DB-driven projektové granty v `UserContextResolver`

**Files:**
- Modify: `PmTracker.Web/Services/Security/UserContextResolver.cs` — nahradit volání `ProjectRolePermissionGrantBuilder.BuildImplicitProjectRoleGrants` za DB query
- Modify nebo create: JOIN query vrací `List<PermissionGrant>` kompatibilní s existing signaturou
- Test: `PmTracker.Tests.Unit/Authorization/DbDrivenProjectGrantsTests.cs` (integration-style s InMemory DB)

**Detail:** Napíše se po dokončení B0 (potřebuje přečíst současný stav UserContextResolver s enumy).

**Přístup (sketch):**
```csharp
// Místo:
var implicitProjectRoleGrants = ProjectRolePermissionGrantBuilder.BuildImplicitProjectRoleGrants(activeProjectRoleAssignments);

// Bude:
var implicitProjectRoleGrants = await (
    from assignment in _dbContext.ObsazeniProjektu.AsNoTracking()
    join lookupRole in _dbContext.CiselnikRoliProjektu.AsNoTracking()
        on assignment.CiselnikRoliProjektuId equals lookupRole.Id
    where lookupRole.AuthzRoleId != null
          && assignment.OsobaId == osobaId
          && assignment.DatumOdebrani == null
    join authzRole in _dbContext.AuthzRoles.AsNoTracking()
        on lookupRole.AuthzRoleId equals authzRole.Id
    where authzRole.IsActive
    join rp in _dbContext.AuthzRolePermissions.AsNoTracking()
        on authzRole.Id equals rp.RoleId
    where rp.IsAllowed
    join permission in _dbContext.AuthzPermissions.AsNoTracking()
        on rp.PermissionId equals permission.Id
    where permission.IsActive
    select new PermissionGrant
    {
        PermissionKey = permission.Klic,
        ProjektId = assignment.ProjektId,
        Source = "PROJECT_ROLE:" + lookupRole.Kod
    }).ToListAsync(ct);
```

**Migration safety test**: test vytvoří stav DB, spustí starou cestu (builder) a novou cestu (DB query), porovná grant sety — musí být identické (jen `Source` se liší v prefixu). Po tomto testu můžeme smazat builder v B3.

## Task B2: DB-driven subsystémové granty

**Files:**
- Modify: `UserContextResolver.cs` — analogicky B1 pro subsystémové role
- Test: `PmTracker.Tests.Unit/Authorization/DbDrivenSubsystemGrantsTests.cs`

**Detail:** Psané po B1. Stejný pattern JOINu přes `ObsazeniSubsystemuProjektu` × `CiselnikRoliSubsystemu` × `AuthzRoles` × `AuthzRolePermissions`.

## Task B3: Smazat buildery + migration safety verification

**Files:**
- Delete: `PmTracker.Web/Services/Common/ProjectRolePermissionGrantBuilder.cs`
- Delete: `PmTracker.Web/Services/Common/SubsystemRolePermissionGrantBuilder.cs`
- Delete: existing unit testy pro oba buildery (pokud jsou)
- Modify: Jakékoliv další call sites (hledat `GrantBuilder.Build*`)
- Test: ponechat migration safety testy z B1+B2 jako regression guard

**Detail:** Po ověření, že B1+B2 produkují identické granty, smazat buildery. Run full suite.

## Task B4: Smazat hardcoded `"SUPERADMIN"` fallback

**Files:**
- Modify: `PmTracker.Web/Services/Security/UserContextResolver.cs:242` — smazat `roleCodes.Any(code => string.Equals(code, "SUPERADMIN"...))`. SuperAdmin status pouze z `AuthzSuperadmins` tabulky (už tam je).
- Test: `PmTracker.Tests.Unit/Authorization/SuperAdminSourceTests.cs` — ověří, že osoba s kodem "SUPERADMIN" v AuthzUserRoles ale NEPŘÍTOMNÁ v AuthzSuperadmins není uznána jako super-admin.

**Detail:** Přečíst kontext řádku 242 v UserContextResolver, upravit logiku.

---

# Fáze C — Gap fixes: nové keys + READ_ALL + security holes + cleanup (sketch)

**Cíl fáze:** Doplnit 8 nových permission keys. Přidat `READ_ALL` seed roli. Opravit 4 security gaps (Export, ProjectDashboard, comments, search.reindex). Vyčistit orphaned `authz.role_permissions` rows z éry manuální UI kompozice.

**Odhad:** ~1 den. 7 tasků.

**Tasky:**
- **C1**: Nové permission keys (konstanty + seed actions + kategorie):
  - `dashboard.view` (Project)
  - `export.pdf` (Project)
  - `export.word` (Project)
  - `comments.add` (Project)
  - `comments.edit.own` (Project)
  - `comments.delete.own` (Project)
  - `search.reindex` (Global)
  - `projects.read.all` (Global, pro READ_ALL roli)
- **C2**: Seed mappingy pro nové keys (SUPERADMIN všechno; APP_ADMIN co dává smysl; projektové/subsystémové role dle matice specu §3.7)
- **C3**: `READ_ALL` seed role (Scope=Global) s `projects.read.all` + `dashboard.view` + `export.pdf` + `export.word`
- **C4**: `ExportController.Pdf` — přidat permission check `HasPermission(ExportPdf, projektId)`
- **C5**: `ProjectDashboardController` — nahradit `IsSuperAdmin` bypass za `HasPermission(DashboardView, projektId)`; SuperAdmin + READ_ALL projde přes grant matici, ne přes hardcoded check
- **C6**: `ZaznamyController` comment actions — nahradit `hasPermission: () => true` explicitním `HasPermission(CommentsAdd/EditOwn/DeleteOwn, projektId)`
- **C7**: `SearchController.Reindex` — nahradit `IsSuperAdmin` za `HasPermission(SearchReindex)`
- **C8**: Cleanup migration: SQL skript `db_upgrade_1_3_0_cleanup_orphaned_role_permissions.sql` — smaže `authz.role_permissions` rows, které po nové seed nevrátí match. Startup validátor ověří "seed matches DB".

**Detail C1-C8** se napíše po dokončení Fáze B.

---

# Fáze D — `IAuthorizationService` + policy handler + service-layer guard (sketch)

**Cíl fáze:** Dát aplikaci kanonickou fasádu pro autorizaci. ASP.NET Core Policy handler (`[Authorize(Policy="...")]` na controllerech) a service-layer guard `IAuthorizationService.RequirePermissionAsync(user, key, projektId?)` pro ochranu service methods (defense-in-depth). Dedupe: nahradit existing místa, kde se ručně dotazuje na granty.

**Odhad:** 1 den. 5 tasků.

**Tasky:**
- **D1**: Definovat `IAuthorizationService` interface + `AuthorizationService` implementace (load user context cache-aware, check against PermissionGrant set)
- **D2**: ASP.NET Core `IAuthorizationHandler` + `PermissionRequirement` policy (napojit na `[Authorize(Policy="permission:records.edit")]` pattern)
- **D3**: Service-layer `RequirePermissionAsync` — throws `ForbiddenException` pokud grant chybí
- **D4**: Dedupe: najít a nahradit v controllerech/services všechny ad-hoc kontroly (cca 30 míst dle §4 specu)
- **D5**: Architecture test: jakýkoliv `[HttpPost]` / `[HttpDelete]` / `[HttpPut]` action musí mít buď `[Authorize(Policy=...)]` nebo explicit `RequirePermissionAsync` volání

**Detail** se napíše po dokončení Fáze C.

---

# Fáze E+F — Seed-only UI Nastavení + architecture tests + docs (sketch)

**Cíl fáze:** Dokončit autorizaci přechodem UI "Nastavení" do seed-only režimu. Smazat editační UI pro role/keys. Ponechat jen read-only přehledy + global role assignment. Přidat architecture testy vynucující seed-only model. Napsat docs.

**Odhad:** 1-2 dny. 7 tasků (spojené E+F oproti původnímu odhadu ~4 dnů samostatných fází).

**Tasky:**
- **EF1**: Smazat starou editační UI — controllery, view models, views pro "Akce", "Role (editační)", "Role→Akce", custom "Uživatel→Role" flow
- **EF2**: Nová karta "Role" (read-only) — seznam všech seed rolí, rozbalovatelný seznam keys + popis každého Scope
- **EF3**: Nová karta "Permission keys" (read-only) — seznam všech keys s popisem + odkazem na commit/PR, kde byl key přidán
- **EF4**: Přejmenovat/zjednodušit "Uživatelé a globální role" — UI pro přiřazení osob do `SUPERADMIN`/`APP_ADMIN`/`READ_ALL` (stále editační, ale jen pro GLOBAL scope role)
- **EF5**: Architecture test: zakázat nové entity pro "custom composed role" storage (žádné nové tabulky `authz.custom_*`). Test seedu = `authz.role_permissions` po `PermissionSeeder.SeedAsync` obsahuje PRÁVĚ rows dle matice seed.
- **EF6**: Architecture test: každý `PermissionKeys.*` const je seedován v `Actions` list
- **EF7**: Dokumentace — nová `docs/authorization.md` (nebo update existing CLAUDE.md) s:
  - Jak přidat novou permission key (3 řádky kódu + migration)
  - Jak přidat novou global roli
  - Jak přidat novou project/subsystem roli
  - Matice scopů
  - Data flow diagram (user → authz → resolver → grants → check)

**Detail** se napíše po dokončení Fáze D.

---

# Závěr — Definition of Done pro celý autorizační refactor

Po dokončení Fáze F je:
- [ ] Všechny autorizační rozhodování jdou přes `IAuthorizationService` nebo `[Authorize(Policy=...)]`
- [ ] Žádný hardcoded string `"SUPERADMIN"` v kódu (jen const `PermissionKeys.*`)
- [ ] Žádný `ProjectRolePermissionGrantBuilder` / `SubsystemRolePermissionGrantBuilder`
- [ ] Seed je jediný zdroj pravdy pro role + keys (žádná admin UI, která by je vytvářela)
- [ ] DB cleanup hotový — orphaned rows smazány
- [ ] Tři enumy (`RoleScope`, `ScopeMode`, `PermissionScopeLevel`) místo stringů v C#
- [ ] Glossary + dokumentace
- [ ] Architecture testy vynucující všechna výše uvedená pravidla
- [ ] Full test suite ~700+ zelených
- [ ] Žádná regrese stávajícího chování pro běžné uživatele (migration safety testy v Fázi B)
