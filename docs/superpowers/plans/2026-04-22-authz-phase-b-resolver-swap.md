# Authorization Unification — Fáze B: Resolver Swap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Přehodit `UserContextResolver` z hardcoded C# builderů na DB-driven granty čtené z `AuthzRolePermissions` přes FK `CiselnikRoli*.AuthzRoleId`. Smazat oba buildery a hardcoded `"SUPERADMIN"` fallback. Po Fázi B je seed single source of truth pro permission mapping.

**Architecture:** Dva paralelní kroky (B1, B2) přidají DB query pro projektové a subsystémové role s deduplicou oproti existujícímu builder výstupu. Migration safety test verifikuje že nová DB cesta produkuje superset builder výstupu. Task B3 buildery smaže. Task B4 smaže hardcoded SUPERADMIN fallback, aby `AuthzSuperadmins` byla jediný zdroj pravdy.

**Tech Stack:** ASP.NET Core 8, EF Core 8 (enumy + HasConversion z Fáze B0), xUnit + FluentAssertions, SQL Server.

**Návaznost:**
- Fáze A: ✅ (commity `da5e271` → `beaa1e4`) — DB schéma + seed
- Fáze B0: ✅ (commity `2a09581` → `c8806ef`) — type safety enumy
- Tato Fáze B navazuje. Detail pro C, D, E+F se napíše po dokončení B.

**Důležité kontextové fakty:**
1. Seed (po Fázi A) má pro projektové role: VLASTNIK_PROJEKTU (8 keys), ADM_PROJ (7 keys), PROJ_MAN (7 keys), GEST (1 key), HOST (0 keys).
2. Existing `ProjectRolePermissionGrantBuilder` dává jen ADM_PROJ a PROJ_MAN po **4 keys** (team.manage, records.edit, meetings.create, meetings.edit). VLASTNIK, HOST, GEST **dostávají 0 keys z builderu**.
3. Seed je tedy **SUPERSET builderu** — po přepnutí dostane VLASTNIK_PROJEKTU, GEST a plné ADM_PROJ/PROJ_MAN navíc keys ze seedu. To je opravená správná sémantika, ale **je to behaviorální změna** — spouští se při Fázi B3.
4. Subsystémový builder dává jen VEDOUCI_SUBSYSTEMU/ZASTUPCE_VEDOUCIHO_SUBSYSTEMU klíč `records.comment.subsystemlead`. Seed dává to samé (identický set).
5. `PermissionGrantViewModel.ScopeMode = "INCLUDE"` + `ProjectIds = [id]` je VIEW MODEL kontrakt pro "grant platí jen na tyto projekty". Seed má `ScopeMode.All` — resolver musí PŘELOŽIT: `(Scope=Project AND ScopeMode=All)` → emit grant s VM.ScopeMode="INCLUDE" + VM.ProjectIds=[projectIds z ObsazeniProjektu].
6. Downstream kód (authorization checks) respektuje jen VIEW MODEL — nesmí se změnit.

**Merge safety:** B1 a B2 běží **paralelně s existujícím builderem** (dedup zabraňuje duplikátům). B3 teprve smaže builder. Každý task je samostatně zelený — aplikace funguje po každém.

---

## Přehled tasků

| Task | Scope | Odhad |
|---|---|---|
| **B1** | DB-driven projektové granty paralelně s builderem + migration safety test | 1-1.5 h |
| **B2** | DB-driven subsystémové granty paralelně s builderem | 30-45 min |
| **B3** | Smazat oba buildery po důkazu ekvivalence | 45-60 min |
| **B4** | Smazat hardcoded SUPERADMIN fallback + test AuthzSuperadmins as source-of-truth | 30 min |

---

## Task B1: DB-driven projektové granty (paralelně s builderem)

**Cíl:** Přidat novou DB query, která načte projekt-role granty přes FK `CiselnikRoliProjektu.AuthzRoleId` → `AuthzRolePermissions`. Výsledné granty jsou emit ve VM formátu `ScopeMode="INCLUDE"` + `ProjectIds=[projId]`. Výstup se mergne s existujícím builder výstupem s deduplikací. Žádný behavior regress — jen se přidává (builder stále aktivní).

**Files:**
- Modify: `PmTracker.Web/Services/Security/UserContextResolver.cs` — add new method `LoadDbDrivenProjectRoleGrantsAsync` + wire it into `ResolveAsync` (dedupe logic)
- Test: `PmTracker.Tests.Unit/Authorization/DbDrivenProjectGrantsTests.cs` (new)

### Step 1 — Write failing test

Vytvořit `PmTracker.Tests.Unit/Authorization/DbDrivenProjectGrantsTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

/// <summary>
/// Ověřuje, že UserContextResolver umí načíst projektové role granty
/// z DB cesty (CiselnikRoliProjektu.AuthzRoleId → AuthzRolePermissions),
/// nejen přes hardcoded ProjectRolePermissionGrantBuilder.
/// </summary>
public sealed class DbDrivenProjectGrantsTests
{
    private static PmTrackerDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PmTrackerDbContext(options);
    }

    [Fact]
    public async Task LoadDbDrivenProjectRoleGrantsAsync_ShouldReturnSeededGrantsForProjectRole()
    {
        await using var db = CreateInMemoryDb();

        // Arrange: vytvořit authz role ADM_PROJ + namapovat na něj permission records.edit
        db.AuthzRoles.Add(new AuthzRoleEntity
        {
            Id = 100, Kod = "ADM_PROJ", Nazev = "Projektový admin",
            IsActive = true, Scope = RoleScope.Project
        });
        db.AuthzPermissions.Add(new AuthzPermissionEntity
        {
            Id = 200, Klic = "records.edit", Nazev = "Editovat záznamy",
            CategoryId = 1, ScopeLevel = PermissionScopeLevel.Project,
            IsActive = true, IsSystem = true
        });
        db.AuthzRolePermissions.Add(new AuthzRolePermissionEntity
        {
            Id = 300, RoleId = 100, PermissionId = 200,
            ScopeMode = ScopeMode.All, IsAllowed = true
        });

        // Lookup role propojená s authz
        db.CiselnikRoliProjektu.Add(new CiselnikRoliProjektuEntity
        {
            Id = 1, Kod = "ADM_PROJ", Nazev = "Projektový admin",
            AuthzRoleId = 100
        });

        // Osoba + projekt + obsazení
        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "Test", Prijmeni = "User" });
        db.ObsazeniProjektu.Add(new ObsazeniProjektuEntity
        {
            Id = 1, OsobaId = 42, ProjektId = 777, RoleId = 1,
            DatumPrirazeni = DateTime.UtcNow, DatumOdebrani = null
        });

        await db.SaveChangesAsync();

        // Act
        var grants = await UserContextResolver.LoadDbDrivenProjectRoleGrantsAsync(db, 42, CancellationToken.None);

        // Assert: jeden grant s records.edit, ScopeMode=INCLUDE, ProjectIds=[777]
        grants.Should().ContainSingle();
        var grant = grants[0];
        grant.PermissionKey.Should().Be("records.edit");
        grant.ScopeMode.Should().Be("INCLUDE");
        grant.IsAllowed.Should().BeTrue();
        grant.ProjectIds.Should().ContainSingle().Which.Should().Be(777);
    }

    [Fact]
    public async Task LoadDbDrivenProjectRoleGrantsAsync_ShouldAggregateMultipleProjectsPerPermission()
    {
        await using var db = CreateInMemoryDb();

        // ADM_PROJ s records.edit, osoba má ADM_PROJ na projektech 777 + 888
        db.AuthzRoles.Add(new AuthzRoleEntity { Id = 100, Kod = "ADM_PROJ", Nazev = "Admin", IsActive = true, Scope = RoleScope.Project });
        db.AuthzPermissions.Add(new AuthzPermissionEntity { Id = 200, Klic = "records.edit", Nazev = "Edit", CategoryId = 1, ScopeLevel = PermissionScopeLevel.Project, IsActive = true, IsSystem = true });
        db.AuthzRolePermissions.Add(new AuthzRolePermissionEntity { Id = 300, RoleId = 100, PermissionId = 200, ScopeMode = ScopeMode.All, IsAllowed = true });
        db.CiselnikRoliProjektu.Add(new CiselnikRoliProjektuEntity { Id = 1, Kod = "ADM_PROJ", Nazev = "Admin", AuthzRoleId = 100 });
        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "T", Prijmeni = "U" });
        db.ObsazeniProjektu.AddRange(
            new ObsazeniProjektuEntity { Id = 10, OsobaId = 42, ProjektId = 777, RoleId = 1, DatumPrirazeni = DateTime.UtcNow },
            new ObsazeniProjektuEntity { Id = 11, OsobaId = 42, ProjektId = 888, RoleId = 1, DatumPrirazeni = DateTime.UtcNow });

        await db.SaveChangesAsync();

        var grants = await UserContextResolver.LoadDbDrivenProjectRoleGrantsAsync(db, 42, CancellationToken.None);

        grants.Should().ContainSingle("stejný permission klíč má být sloučený do jednoho grantu s více ProjectIds");
        grants[0].ProjectIds.Should().BeEquivalentTo(new[] { 777, 888 });
    }

    [Fact]
    public async Task LoadDbDrivenProjectRoleGrantsAsync_ShouldSkipExpiredAssignments()
    {
        await using var db = CreateInMemoryDb();

        db.AuthzRoles.Add(new AuthzRoleEntity { Id = 100, Kod = "ADM_PROJ", Nazev = "Admin", IsActive = true, Scope = RoleScope.Project });
        db.AuthzPermissions.Add(new AuthzPermissionEntity { Id = 200, Klic = "records.edit", Nazev = "Edit", CategoryId = 1, ScopeLevel = PermissionScopeLevel.Project, IsActive = true, IsSystem = true });
        db.AuthzRolePermissions.Add(new AuthzRolePermissionEntity { Id = 300, RoleId = 100, PermissionId = 200, ScopeMode = ScopeMode.All, IsAllowed = true });
        db.CiselnikRoliProjektu.Add(new CiselnikRoliProjektuEntity { Id = 1, Kod = "ADM_PROJ", Nazev = "Admin", AuthzRoleId = 100 });
        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "T", Prijmeni = "U" });
        db.ObsazeniProjektu.Add(new ObsazeniProjektuEntity
        {
            Id = 10, OsobaId = 42, ProjektId = 777, RoleId = 1,
            DatumPrirazeni = DateTime.UtcNow.AddDays(-30),
            DatumOdebrani = DateTime.UtcNow.AddDays(-1)   // expirované
        });

        await db.SaveChangesAsync();

        var grants = await UserContextResolver.LoadDbDrivenProjectRoleGrantsAsync(db, 42, CancellationToken.None);

        grants.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadDbDrivenProjectRoleGrantsAsync_ShouldSkipLookupRolesWithNullAuthzRoleId()
    {
        await using var db = CreateInMemoryDb();

        // Lookup role bez AuthzRoleId (custom admin-added, nelinkovaná)
        db.CiselnikRoliProjektu.Add(new CiselnikRoliProjektuEntity { Id = 1, Kod = "CUSTOM_ROLE", Nazev = "Vlastní", AuthzRoleId = null });
        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "T", Prijmeni = "U" });
        db.ObsazeniProjektu.Add(new ObsazeniProjektuEntity { Id = 10, OsobaId = 42, ProjektId = 777, RoleId = 1, DatumPrirazeni = DateTime.UtcNow });

        await db.SaveChangesAsync();

        var grants = await UserContextResolver.LoadDbDrivenProjectRoleGrantsAsync(db, 42, CancellationToken.None);

        grants.Should().BeEmpty("custom lookup role bez AuthzRoleId nemůže přinést žádné granty");
    }

    [Fact]
    public async Task LoadDbDrivenProjectRoleGrantsAsync_ShouldSkipInactivePermissions()
    {
        await using var db = CreateInMemoryDb();

        db.AuthzRoles.Add(new AuthzRoleEntity { Id = 100, Kod = "ADM_PROJ", Nazev = "A", IsActive = true, Scope = RoleScope.Project });
        db.AuthzPermissions.Add(new AuthzPermissionEntity { Id = 200, Klic = "deprecated.key", Nazev = "Old", CategoryId = 1, ScopeLevel = PermissionScopeLevel.Project, IsActive = false, IsSystem = true });
        db.AuthzRolePermissions.Add(new AuthzRolePermissionEntity { Id = 300, RoleId = 100, PermissionId = 200, ScopeMode = ScopeMode.All, IsAllowed = true });
        db.CiselnikRoliProjektu.Add(new CiselnikRoliProjektuEntity { Id = 1, Kod = "ADM_PROJ", Nazev = "A", AuthzRoleId = 100 });
        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "T", Prijmeni = "U" });
        db.ObsazeniProjektu.Add(new ObsazeniProjektuEntity { Id = 10, OsobaId = 42, ProjektId = 777, RoleId = 1, DatumPrirazeni = DateTime.UtcNow });

        await db.SaveChangesAsync();

        var grants = await UserContextResolver.LoadDbDrivenProjectRoleGrantsAsync(db, 42, CancellationToken.None);

        grants.Should().BeEmpty();
    }
}
```

### Step 2 — Verify FAIL

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~DbDrivenProjectGrantsTests" -v q
```

Expected: FAIL — metoda `UserContextResolver.LoadDbDrivenProjectRoleGrantsAsync` neexistuje.

### Step 3 — Přidat statickou metodu do UserContextResolver

V `PmTracker.Web/Services/Security/UserContextResolver.cs` přidat novou **public static** metodu (aby byla testovatelná bez instance) — umístit ji bezprostředně za konstruktor:

```csharp
/// <summary>
/// Načte projektové-role granty pro danou osobu z DB cesty:
/// ObsazeniProjektu × CiselnikRoliProjektu (přes FK AuthzRoleId) × AuthzRolePermissions.
/// Fáze B náhrada za <c>ProjectRolePermissionGrantBuilder</c>.
/// </summary>
/// <remarks>
/// Sémantika: role s <see cref="RoleScope.Project"/> a mapping se <see cref="ScopeMode.All"/>
/// znamená "všechny projekty, kde osoba má tuto roli". Výsledný VM emituje
/// <c>ScopeMode="INCLUDE"</c> + <c>ProjectIds=[...]</c> (spec §3.3, VM kontrakt ze spec §3.1).
/// </remarks>
public static async Task<IReadOnlyList<PermissionGrantViewModel>> LoadDbDrivenProjectRoleGrantsAsync(
    PmTrackerDbContext dbContext,
    int osobaId,
    CancellationToken ct)
{
    ArgumentNullException.ThrowIfNull(dbContext);

    var rows = await (
            from assignment in dbContext.ObsazeniProjektu.AsNoTracking()
            where assignment.OsobaId == osobaId && !assignment.DatumOdebrani.HasValue
            join lookupRole in dbContext.CiselnikRoliProjektu.AsNoTracking()
                on assignment.RoleId equals lookupRole.Id
            where lookupRole.AuthzRoleId != null
            join authzRole in dbContext.AuthzRoles.AsNoTracking()
                on lookupRole.AuthzRoleId equals authzRole.Id
            where authzRole.IsActive
            join rp in dbContext.AuthzRolePermissions.AsNoTracking()
                on authzRole.Id equals rp.RoleId
            where rp.IsAllowed
            join permission in dbContext.AuthzPermissions.AsNoTracking()
                on rp.PermissionId equals permission.Id
            where permission.IsActive
            select new
            {
                permission.Klic,
                permission.ScopeLevel,
                assignment.ProjektId
            })
        .ToListAsync(ct);

    // Agregace: per permission key → set distinctních projectId
    return rows
        .GroupBy(r => r.Klic)
        .Select(group => new PermissionGrantViewModel
        {
            PermissionKey = group.Key,
            ScopeLevel = group.First().ScopeLevel.ToString().ToUpperInvariant(),
            ScopeMode = "INCLUDE",
            IsAllowed = true,
            ProjectIds = group
                .Select(r => r.ProjektId)
                .Distinct()
                .OrderBy(x => x)
                .ToList(),
            SourceType = "PROJECT_ROLE"
        })
        .ToList();
}
```

Poznámka: metoda je static — nečte žádné instance fieldy (`_dbContext`, `_textNormalizer`, ...). To umožňuje testy s injectnutým InMemory DbContext.

### Step 4 — Spustit nové testy, ověřit PASS

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~DbDrivenProjectGrantsTests" -v q
```

Expected: 5/5 PASS.

### Step 5 — Napojit novou metodu do ResolveAsync s deduplikací

V `UserContextResolver.ResolveAsync`, po řádku 306 (`grants.AddRange(implicitProjectRoleGrants);`) přidat:

```csharp
// Fáze B — Task B1: DB-driven granty jako doplněk builderu.
// Builder stále aktivní; deduplikace zajistí, že stejný grant (klíč + scope mode + projectIds)
// není přidán dvakrát. Po Fázi B3 se builder smaže a zůstane jen tato cesta.
var dbDrivenProjectGrants = await LoadDbDrivenProjectRoleGrantsAsync(_dbContext, osoba.Id, ct);
foreach (var dbGrant in dbDrivenProjectGrants)
{
    var alreadyPresent = grants.Any(existing =>
        string.Equals(existing.PermissionKey, dbGrant.PermissionKey, StringComparison.OrdinalIgnoreCase)
        && string.Equals(existing.ScopeMode, dbGrant.ScopeMode, StringComparison.OrdinalIgnoreCase)
        && existing.ProjectIds.OrderBy(x => x).SequenceEqual(dbGrant.ProjectIds.OrderBy(x => x)));

    if (!alreadyPresent)
    {
        grants.Add(dbGrant);
    }
}
```

### Step 6 — Full suite musí zůstat zelená

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -v q
dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj -v q
```

Expected:
- PmTracker.Tests.Unit: 689/689 PASS (684 + 5 nových)
- PmTracker.Web.Tests: pokud byly zelené před B1, musí zůstat. `HttpSmokeTests` 2 failures jsou pre-existing z DB schema issue, ne z našeho kódu.

**Pokud** se nějaký test rozbije v důsledku přidaných grantů (VLASTNIK_PROJEKTU, GEST teď dostanou keys, které dřív nedostávali), je to očekávaná behaviorální změna — oprav test aby očekával rozšířené granty, nikdy ne "zrušit nové granty".

### Step 7 — Commit

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
git add PmTracker.Web/Services/Security/UserContextResolver.cs \
        PmTracker.Tests.Unit/Authorization/DbDrivenProjectGrantsTests.cs
git commit -m "feat(authz): DB-driven projektové granty v UserContextResolver (paralelně s builderem)"
```

---

## Task B2: DB-driven subsystémové granty (paralelně s builderem)

**Cíl:** Stejný pattern jako B1 pro subsystémové role. Granty se načtou z `ObsazeniSubsystemuProjektu` × `CiselnikRoliSubsystemu` × `AuthzRoles` × `AuthzRolePermissions`. Emit VM s `ScopeMode="INCLUDE"` + `ProjectIds=[projektId z ProjektSubsystemy]`.

**Files:**
- Modify: `PmTracker.Web/Services/Security/UserContextResolver.cs` — add `LoadDbDrivenSubsystemRoleGrantsAsync`
- Test: `PmTracker.Tests.Unit/Authorization/DbDrivenSubsystemGrantsTests.cs` (new)

### Step 1 — Failing test

Vytvořit `PmTracker.Tests.Unit/Authorization/DbDrivenSubsystemGrantsTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class DbDrivenSubsystemGrantsTests
{
    private static PmTrackerDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PmTrackerDbContext(options);
    }

    [Fact]
    public async Task LoadDbDrivenSubsystemRoleGrantsAsync_ShouldReturnSeededGrantsForSubsystemRole()
    {
        await using var db = CreateInMemoryDb();

        db.AuthzRoles.Add(new AuthzRoleEntity { Id = 100, Kod = "VEDOUCI_SUBSYSTEMU", Nazev = "Vedoucí", IsActive = true, Scope = RoleScope.Subsystem });
        db.AuthzPermissions.Add(new AuthzPermissionEntity { Id = 200, Klic = "records.comment.subsystemlead", Nazev = "Komentář", CategoryId = 1, ScopeLevel = PermissionScopeLevel.Project, IsActive = true, IsSystem = true });
        db.AuthzRolePermissions.Add(new AuthzRolePermissionEntity { Id = 300, RoleId = 100, PermissionId = 200, ScopeMode = ScopeMode.All, IsAllowed = true });

        db.CiselnikRoliSubsystemu.Add(new CiselnikRoleSubsystemuEntity { Id = 1, Kod = "VEDOUCI_SUBSYSTEMU", Nazev = "Vedoucí", AuthzRoleId = 100 });
        db.ProjektSubsystemy.Add(new ProjektSubsystemyEntity { Id = 50, ProjektId = 777, Kod = "SUB1", Nazev = "S1" });

        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "T", Prijmeni = "U" });
        db.ObsazeniSubsystemuProjektu.Add(new ObsazeniSubsystemuProjektuEntity
        {
            Id = 1, OsobaId = 42, ProjektSubsystemId = 50, RoleSubsystemuId = 1,
            DatumPrirazeni = DateTime.UtcNow, DatumOdebrani = null
        });

        await db.SaveChangesAsync();

        var grants = await UserContextResolver.LoadDbDrivenSubsystemRoleGrantsAsync(db, 42, CancellationToken.None);

        grants.Should().ContainSingle();
        grants[0].PermissionKey.Should().Be("records.comment.subsystemlead");
        grants[0].ScopeMode.Should().Be("INCLUDE");
        grants[0].ProjectIds.Should().ContainSingle().Which.Should().Be(777);
    }

    [Fact]
    public async Task LoadDbDrivenSubsystemRoleGrantsAsync_ShouldSkipExpiredAssignments()
    {
        await using var db = CreateInMemoryDb();

        db.AuthzRoles.Add(new AuthzRoleEntity { Id = 100, Kod = "VEDOUCI_SUBSYSTEMU", Nazev = "V", IsActive = true, Scope = RoleScope.Subsystem });
        db.AuthzPermissions.Add(new AuthzPermissionEntity { Id = 200, Klic = "records.comment.subsystemlead", Nazev = "K", CategoryId = 1, ScopeLevel = PermissionScopeLevel.Project, IsActive = true, IsSystem = true });
        db.AuthzRolePermissions.Add(new AuthzRolePermissionEntity { Id = 300, RoleId = 100, PermissionId = 200, ScopeMode = ScopeMode.All, IsAllowed = true });
        db.CiselnikRoliSubsystemu.Add(new CiselnikRoleSubsystemuEntity { Id = 1, Kod = "VEDOUCI_SUBSYSTEMU", Nazev = "V", AuthzRoleId = 100 });
        db.ProjektSubsystemy.Add(new ProjektSubsystemyEntity { Id = 50, ProjektId = 777, Kod = "S", Nazev = "S" });
        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "T", Prijmeni = "U" });
        db.ObsazeniSubsystemuProjektu.Add(new ObsazeniSubsystemuProjektuEntity
        {
            Id = 1, OsobaId = 42, ProjektSubsystemId = 50, RoleSubsystemuId = 1,
            DatumPrirazeni = DateTime.UtcNow.AddDays(-30),
            DatumOdebrani = DateTime.UtcNow.AddDays(-1)
        });

        await db.SaveChangesAsync();

        var grants = await UserContextResolver.LoadDbDrivenSubsystemRoleGrantsAsync(db, 42, CancellationToken.None);

        grants.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadDbDrivenSubsystemRoleGrantsAsync_ShouldSkipAssignmentsOnRemovedProjectSubsystem()
    {
        await using var db = CreateInMemoryDb();

        db.AuthzRoles.Add(new AuthzRoleEntity { Id = 100, Kod = "VEDOUCI_SUBSYSTEMU", Nazev = "V", IsActive = true, Scope = RoleScope.Subsystem });
        db.AuthzPermissions.Add(new AuthzPermissionEntity { Id = 200, Klic = "records.comment.subsystemlead", Nazev = "K", CategoryId = 1, ScopeLevel = PermissionScopeLevel.Project, IsActive = true, IsSystem = true });
        db.AuthzRolePermissions.Add(new AuthzRolePermissionEntity { Id = 300, RoleId = 100, PermissionId = 200, ScopeMode = ScopeMode.All, IsAllowed = true });
        db.CiselnikRoliSubsystemu.Add(new CiselnikRoleSubsystemuEntity { Id = 1, Kod = "VEDOUCI_SUBSYSTEMU", Nazev = "V", AuthzRoleId = 100 });
        db.ProjektSubsystemy.Add(new ProjektSubsystemyEntity
        {
            Id = 50, ProjektId = 777, Kod = "S", Nazev = "S",
            DatumOdebrani = DateTime.UtcNow.AddDays(-5)   // subsystém odstraněn
        });
        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "T", Prijmeni = "U" });
        db.ObsazeniSubsystemuProjektu.Add(new ObsazeniSubsystemuProjektuEntity
        {
            Id = 1, OsobaId = 42, ProjektSubsystemId = 50, RoleSubsystemuId = 1,
            DatumPrirazeni = DateTime.UtcNow, DatumOdebrani = null
        });

        await db.SaveChangesAsync();

        var grants = await UserContextResolver.LoadDbDrivenSubsystemRoleGrantsAsync(db, 42, CancellationToken.None);

        grants.Should().BeEmpty();
    }
}
```

### Step 2 — Verify FAIL

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~DbDrivenSubsystemGrantsTests" -v q
```

Expected: FAIL — metoda `LoadDbDrivenSubsystemRoleGrantsAsync` neexistuje.

### Step 3 — Přidat metodu do UserContextResolver

Přímo za metodu `LoadDbDrivenProjectRoleGrantsAsync` (z B1):

```csharp
/// <summary>
/// Načte subsystémové-role granty pro danou osobu z DB cesty:
/// ObsazeniSubsystemuProjektu × CiselnikRoliSubsystemu (přes FK AuthzRoleId)
/// × AuthzRolePermissions. Výsledný VM používá ProjektId ze spojeného ProjektSubsystemy řádku.
/// Fáze B náhrada za <c>SubsystemRolePermissionGrantBuilder</c>.
/// </summary>
public static async Task<IReadOnlyList<PermissionGrantViewModel>> LoadDbDrivenSubsystemRoleGrantsAsync(
    PmTrackerDbContext dbContext,
    int osobaId,
    CancellationToken ct)
{
    ArgumentNullException.ThrowIfNull(dbContext);

    var rows = await (
            from assignment in dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            where assignment.OsobaId == osobaId && !assignment.DatumOdebrani.HasValue
            join lookupRole in dbContext.CiselnikRoliSubsystemu.AsNoTracking()
                on assignment.RoleSubsystemuId equals lookupRole.Id
            where lookupRole.AuthzRoleId != null
            join projectSubsystem in dbContext.ProjektSubsystemy.AsNoTracking()
                on assignment.ProjektSubsystemId equals projectSubsystem.Id
            where !projectSubsystem.DatumOdebrani.HasValue
            join authzRole in dbContext.AuthzRoles.AsNoTracking()
                on lookupRole.AuthzRoleId equals authzRole.Id
            where authzRole.IsActive
            join rp in dbContext.AuthzRolePermissions.AsNoTracking()
                on authzRole.Id equals rp.RoleId
            where rp.IsAllowed
            join permission in dbContext.AuthzPermissions.AsNoTracking()
                on rp.PermissionId equals permission.Id
            where permission.IsActive
            select new
            {
                permission.Klic,
                permission.ScopeLevel,
                projectSubsystem.ProjektId
            })
        .ToListAsync(ct);

    return rows
        .GroupBy(r => r.Klic)
        .Select(group => new PermissionGrantViewModel
        {
            PermissionKey = group.Key,
            ScopeLevel = group.First().ScopeLevel.ToString().ToUpperInvariant(),
            ScopeMode = "INCLUDE",
            IsAllowed = true,
            ProjectIds = group
                .Select(r => r.ProjektId)
                .Distinct()
                .OrderBy(x => x)
                .ToList(),
            SourceType = "SUBSYSTEM_ROLE"
        })
        .ToList();
}
```

### Step 4 — Napojit do ResolveAsync s deduplikací

V `ResolveAsync`, za blok `grants.AddRange(implicitSubsystemRoleGrants);` (line ~322), přidat:

```csharp
// Fáze B — Task B2: DB-driven subsystémové granty jako doplněk builderu. Deduplikace stejná jako u projektových.
var dbDrivenSubsystemGrants = await LoadDbDrivenSubsystemRoleGrantsAsync(_dbContext, osoba.Id, ct);
foreach (var dbGrant in dbDrivenSubsystemGrants)
{
    var alreadyPresent = grants.Any(existing =>
        string.Equals(existing.PermissionKey, dbGrant.PermissionKey, StringComparison.OrdinalIgnoreCase)
        && string.Equals(existing.ScopeMode, dbGrant.ScopeMode, StringComparison.OrdinalIgnoreCase)
        && existing.ProjectIds.OrderBy(x => x).SequenceEqual(dbGrant.ProjectIds.OrderBy(x => x)));

    if (!alreadyPresent)
    {
        grants.Add(dbGrant);
    }
}
```

### Step 5 — Tests PASS

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~DbDrivenSubsystemGrantsTests" -v q
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -v q
```

Expected: target 3/3 PASS, full suite 692/692 (689 + 3 nových).

### Step 6 — Commit

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
git add PmTracker.Web/Services/Security/UserContextResolver.cs \
        PmTracker.Tests.Unit/Authorization/DbDrivenSubsystemGrantsTests.cs
git commit -m "feat(authz): DB-driven subsystémové granty v UserContextResolver"
```

---

## Task B3: Smazat buildery

**Cíl:** Odstranit `ProjectRolePermissionGrantBuilder` + `SubsystemRolePermissionGrantBuilder` + deduplikační kód z `ResolveAsync`. DB-driven cesta z B1+B2 se stane jediným zdrojem implicit grantů. Behaviorální změna: VLASTNIK_PROJEKTU, GEST a plný set ADM_PROJ/PROJ_MAN keys získají své seed granty (předtím je builder neprodukoval).

**Files:**
- Delete: `PmTracker.Web/Services/Common/ProjectRolePermissionGrantBuilder.cs`
- Delete: `PmTracker.Web/Services/Common/SubsystemRolePermissionGrantBuilder.cs`
- Delete (pokud existuje): unit testy `PmTracker.Tests.Unit/**/ProjectRolePermissionGrantBuilderTests.cs`, `SubsystemRolePermissionGrantBuilderTests.cs`
- Modify: `PmTracker.Web/Services/Security/UserContextResolver.cs` — smazat volání builderů + dedup logiku (DB cesta teď běží sama)
- Test: `PmTracker.Tests.Unit/Authorization/ResolverSwapSafetyTests.cs` (new) — asserts vlastník projektu dostane seed granty

### Step 1 — Najít existující builder testy

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
grep -rln "ProjectRolePermissionGrantBuilder\|SubsystemRolePermissionGrantBuilder" --include="*.cs" | grep -v -E "/(bin|obj|worktrees)/" | head
```

Expected output: UserContextResolver.cs + builder files + libovolné testy.

### Step 2 — Napsat regression/safety test PRED smazáním

Vytvořit `PmTracker.Tests.Unit/Authorization/ResolverSwapSafetyTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

/// <summary>
/// Ověřuje, že DB-driven cesta produkuje všechny granty, které dříve dával builder
/// (nic se neztratilo), a navíc přidává seed granty pro role, které builder opomíjel
/// (VLASTNIK_PROJEKTU, GEST, plný ADM_PROJ/PROJ_MAN matrix).
/// </summary>
public sealed class ResolverSwapSafetyTests
{
    private static PmTrackerDbContext CreateSeededDb()
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new PmTrackerDbContext(options);

        // Existing lookup rows MUSÍ být vloženy PŘED SeedAsync, aby je RoleCatalogLinker
        // (volaný na konci SeedAsync) dokázal propojit přes Kod match.
        db.CiselnikRoliProjektu.AddRange(
            new CiselnikRoliProjektuEntity { Kod = "VLASTNIK_PROJEKTU", Nazev = "Vlastník projektu" },
            new CiselnikRoliProjektuEntity { Kod = "ADM_PROJ", Nazev = "Projektový admin" },
            new CiselnikRoliProjektuEntity { Kod = "PROJ_MAN", Nazev = "Projektový manažer" },
            new CiselnikRoliProjektuEntity { Kod = "HOST", Nazev = "Host" },
            new CiselnikRoliProjektuEntity { Kod = "GEST", Nazev = "Gestor" });
        db.SaveChanges();

        // Full PermissionSeeder pipeline: vytvoří AuthzRoles + RolePermissions + RoleCatalogLinker
        // na konci propojí CiselnikRoliProjektu.AuthzRoleId.
        var seeder = new PermissionSeeder(db);
        seeder.SeedAsync(CancellationToken.None).GetAwaiter().GetResult();

        return db;
    }

    [Fact]
    public async Task DbDrivenProjectGrants_ShouldCoverVLASTNIK_PROJEKTU()
    {
        await using var db = CreateSeededDb();

        var vlastnikRole = await db.CiselnikRoliProjektu.FirstAsync(r => r.Kod == "VLASTNIK_PROJEKTU");

        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "V", Prijmeni = "L" });
        db.ObsazeniProjektu.Add(new ObsazeniProjektuEntity
        {
            OsobaId = 42, ProjektId = 777, RoleId = vlastnikRole.Id,
            DatumPrirazeni = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var grants = await UserContextResolver.LoadDbDrivenProjectRoleGrantsAsync(db, 42, CancellationToken.None);

        var grantKeys = grants.Select(g => g.PermissionKey).OrderBy(x => x).ToArray();
        grantKeys.Should().BeEquivalentTo(new[]
        {
            "meetings.create", "meetings.edit",
            "projects.edit",
            "records.comment.subsystemlead",
            "records.edit", "records.schedule.add", "records.schedule.edit",
            "team.manage"
        }, "VLASTNIK_PROJEKTU má dle seedu 8 permission keys — dřív builder nedal žádný");
    }

    [Fact]
    public async Task DbDrivenProjectGrants_ShouldCoverGEST()
    {
        await using var db = CreateSeededDb();

        var gestRole = await db.CiselnikRoliProjektu.FirstAsync(r => r.Kod == "GEST");

        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "G", Prijmeni = "E" });
        db.ObsazeniProjektu.Add(new ObsazeniProjektuEntity
        {
            OsobaId = 42, ProjektId = 777, RoleId = gestRole.Id,
            DatumPrirazeni = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var grants = await UserContextResolver.LoadDbDrivenProjectRoleGrantsAsync(db, 42, CancellationToken.None);

        grants.Should().ContainSingle();
        grants[0].PermissionKey.Should().Be("records.comment.subsystemlead");
    }

    [Fact]
    public async Task DbDrivenProjectGrants_ShouldGiveHOST_ZeroGrants_InPhaseB()
    {
        await using var db = CreateSeededDb();

        var hostRole = await db.CiselnikRoliProjektu.FirstAsync(r => r.Kod == "HOST");

        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "H", Prijmeni = "O" });
        db.ObsazeniProjektu.Add(new ObsazeniProjektuEntity
        {
            OsobaId = 42, ProjektId = 777, RoleId = hostRole.Id,
            DatumPrirazeni = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var grants = await UserContextResolver.LoadDbDrivenProjectRoleGrantsAsync(db, 42, CancellationToken.None);

        grants.Should().BeEmpty("HOST má read-only role — dashboard.view/export.* přijdou až ve Fázi C");
    }

    [Fact]
    public async Task DbDrivenProjectGrants_ShouldCoverFull_ADM_PROJ_Matrix()
    {
        await using var db = CreateSeededDb();

        var admRole = await db.CiselnikRoliProjektu.FirstAsync(r => r.Kod == "ADM_PROJ");

        db.Osoby.Add(new OsobaEntity { Id = 42, Jmeno = "A", Prijmeni = "P" });
        db.ObsazeniProjektu.Add(new ObsazeniProjektuEntity
        {
            OsobaId = 42, ProjektId = 777, RoleId = admRole.Id,
            DatumPrirazeni = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var grants = await UserContextResolver.LoadDbDrivenProjectRoleGrantsAsync(db, 42, CancellationToken.None);
        var grantKeys = grants.Select(g => g.PermissionKey).OrderBy(x => x).ToArray();

        grantKeys.Should().BeEquivalentTo(new[]
        {
            "meetings.create", "meetings.edit",
            "records.comment.subsystemlead",
            "records.edit", "records.schedule.add", "records.schedule.edit",
            "team.manage"
        }, "ADM_PROJ má dle seedu 7 keys — builder dával jen 4 (team.manage, records.edit, meetings.create, meetings.edit)");
    }
}
```

### Step 3 — Verify FAIL

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ResolverSwapSafetyTests" -v q
```

Expected: testy se mohou kompilovat (metody z B1 existují), ale mohou selhávat kvůli detailům seed dat. Pokud selžou, oprav testy — NE resolver. Cíl je ukázat, že nová cesta produkuje seed-driven granty.

Pokud všechny 4 testy projdou už teď (B1 jde), jdi na Step 4.

### Step 4 — Smazat buildery

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
git rm PmTracker.Web/Services/Common/ProjectRolePermissionGrantBuilder.cs
git rm PmTracker.Web/Services/Common/SubsystemRolePermissionGrantBuilder.cs
```

Pokud existují unit testy pro buildery (ověř přes `find PmTracker.Tests.Unit -name "*GrantBuilder*"`), smazat je také:

```bash
find PmTracker.Tests.Unit -name "ProjectRolePermissionGrantBuilderTests.cs" -delete 2>/dev/null
find PmTracker.Tests.Unit -name "SubsystemRolePermissionGrantBuilderTests.cs" -delete 2>/dev/null
```

### Step 5 — Odstranit volání builderů z UserContextResolver

V `PmTracker.Web/Services/Security/UserContextResolver.cs`, **smazat 2 bloky kódu** (najdou se podle názvu třídy):

**Blok 1** (řádek ~294-306, volání projektového builderu):
```csharp
var activeProjectRoleAssignments = await (
        from assignment in _dbContext.ObsazeniProjektu.AsNoTracking()
        join role in _dbContext.CiselnikRoliProjektu.AsNoTracking() on assignment.RoleId equals role.Id
        where assignment.OsobaId == osoba.Id
            && !assignment.DatumOdebrani.HasValue
        select new ProjectRoleAssignmentGrantSource
        {
            RoleCode = role.Kod,
            ProjectId = assignment.ProjektId
        })
    .ToListAsync(ct);
var implicitProjectRoleGrants = ProjectRolePermissionGrantBuilder.BuildImplicitProjectRoleGrants(activeProjectRoleAssignments);
grants.AddRange(implicitProjectRoleGrants);
```

**Smazat celé.** Dedup blok z B1, který následoval, také smazat — není potřeba deduplikovat proti ničemu. Místo obou bloků zůstane jen:

```csharp
var dbDrivenProjectGrants = await LoadDbDrivenProjectRoleGrantsAsync(_dbContext, osoba.Id, ct);
grants.AddRange(dbDrivenProjectGrants);
```

**Blok 2** (řádek ~308-322, subsystémový builder):
```csharp
var activeSubsystemRoleAssignments = await (
        from assignment in _dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
        join role in _dbContext.CiselnikRoliSubsystemu.AsNoTracking() on assignment.RoleSubsystemuId equals role.Id
        join projectSubsystem in _dbContext.ProjektSubsystemy.AsNoTracking() on assignment.ProjektSubsystemId equals projectSubsystem.Id
        where assignment.OsobaId == osoba.Id
            && !assignment.DatumOdebrani.HasValue
            && !projectSubsystem.DatumOdebrani.HasValue
        select new SubsystemRoleAssignmentGrantSource
        {
            RoleCode = role.Kod,
            ProjectId = projectSubsystem.ProjektId
        })
    .ToListAsync(ct);
var implicitSubsystemRoleGrants = SubsystemRolePermissionGrantBuilder.BuildImplicitSubsystemRoleGrants(activeSubsystemRoleAssignments);
grants.AddRange(implicitSubsystemRoleGrants);
```

**Smazat celé.** Dedup blok z B2 také smazat. Místo obou zůstane:

```csharp
var dbDrivenSubsystemGrants = await LoadDbDrivenSubsystemRoleGrantsAsync(_dbContext, osoba.Id, ct);
grants.AddRange(dbDrivenSubsystemGrants);
```

### Step 6 — Zachovat `visibleProjectIds` výpočet

V `ResolveAsync` dál následují řádky (~324-338):

```csharp
var projectRoleProjectIds = activeProjectRoleAssignments
    .Select(x => x.ProjectId)
    .Distinct()
    .ToList();

var subsystemRoleProjectIds = activeSubsystemRoleAssignments
    .Select(x => x.ProjectId)
    .Distinct()
    .ToList();

var visibleProjectIds = projectRoleProjectIds
    .Concat(subsystemRoleProjectIds)
    .Distinct()
    .OrderBy(x => x)
    .ToList();
```

Tyto lokální proměnné `activeProjectRoleAssignments` a `activeSubsystemRoleAssignments` teď neexistují. **Nahradit celý blok** za jednodušší výpočet z DB-driven grants:

```csharp
// Fáze B Task B3: visibleProjectIds získáme z ProjectIds všech projektových/subsystémových grantů.
var visibleProjectIds = dbDrivenProjectGrants
    .SelectMany(g => g.ProjectIds)
    .Concat(dbDrivenSubsystemGrants.SelectMany(g => g.ProjectIds))
    .Distinct()
    .OrderBy(x => x)
    .ToList();
```

Pokud v kódu po `visibleProjectIds` jsou další reference na `activeProjectRoleAssignments` / `activeSubsystemRoleAssignments` (mělo by ne), oprav je jinak. Doporučené: najít všechny výskyty obou proměnných:

```bash
grep -n "activeProjectRoleAssignments\|activeSubsystemRoleAssignments" PmTracker.Web/Services/Security/UserContextResolver.cs
```

Expected po změnách: 0 výskytů.

### Step 7 — Odstranit unused `using` a records

`UserContextResolver.cs` používá třídy z `PmTracker.Web.Services.Common` (`ProjectRoleAssignmentGrantSource`, `SubsystemRoleAssignmentGrantSource`) — po smazání builderů tyto typy přestanou existovat.

Pokud compiler hlásí chybějící namespace/typy, odstraň:
- `using PmTracker.Web.Services.Common;` na začátku UserContextResolver.cs (pokud tam je a není potřeba pro nic jiného)

### Step 8 — Full build + test

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet build PmTracker.Web/PmTracker.Web.csproj -v q
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -v q
```

Expected:
- Build: 0 errors
- Test suite: ideálně 696/696 (692 + 4 ResolverSwapSafety tests). Pokud se nějaké existing testy rozbijí kvůli novým seed grantům (VLASTNIK_PROJEKTU teď dostane 8 keys místo 0, atd.), **NEODSTRAŇUJ nové granty** — oprav testy aby odpovídaly novému (správnému) stavu. Loguj každé takové úpravy v commit message.

### Step 9 — Commit

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
git add -A
git status  # ověř, že jsou smazané buildery + upravený resolver + nové testy
git commit -m "refactor(authz): smazat GrantBuildery, resolver pouze DB-driven"
```

Pokud `git status` ukáže nečekané změny (např. worktree), stagenout jen relevantní:

```bash
git add PmTracker.Web/Services/Common/ProjectRolePermissionGrantBuilder.cs \
        PmTracker.Web/Services/Common/SubsystemRolePermissionGrantBuilder.cs \
        PmTracker.Web/Services/Security/UserContextResolver.cs \
        PmTracker.Tests.Unit/Authorization/ResolverSwapSafetyTests.cs
# + případné upravené existing testy
git commit -m "refactor(authz): smazat GrantBuildery, resolver pouze DB-driven"
```

---

## Task B4: Smazat hardcoded `"SUPERADMIN"` fallback

**Cíl:** V `UserContextResolver.ResolveAsync` (line ~239-243) je fallback: pokud `osoba.IsSuperAdmin == false`, tak se zkontroluje, zda má osoba Kod="SUPERADMIN" v rolích — a pokud ano, označí se jako super-admin. Tato pojistka byla z éry, kdy super-admin status nebyl v `AuthzSuperadmins` tabulce. Po Fázi A+B je `AuthzSuperadmins` jediný zdroj pravdy, fallback je overhead + bezpečnostní riziko (někdo přidá roli SUPERADMIN přes UI a eskaluje práva).

**Files:**
- Modify: `PmTracker.Web/Services/Security/UserContextResolver.cs:239-243` — smazat if-blok
- Test: `PmTracker.Tests.Unit/Authorization/SuperAdminSourceTests.cs` (new)

### Step 1 — Failing test

Vytvořit `PmTracker.Tests.Unit/Authorization/SuperAdminSourceTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

/// <summary>
/// Fáze B Task B4: SuperAdmin status určuje pouze AuthzSuperadmins tabulka
/// (reprezentovaná přes <c>osoba.IsSuperAdmin</c> v ResolvedPersonRow).
/// Hardcoded fallback 'roleCodes obsahuje SUPERADMIN' je odstraněn.
/// </summary>
public sealed class SuperAdminSourceTests
{
    [Fact]
    public void UserContextResolver_ShouldNotContainHardcodedSuperadminFallback()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Security/UserContextResolver.cs"));

        // Assertion: nikde v kódu se nezkoumá, zda roleCodes obsahuje "SUPERADMIN" jako fallback.
        code.Should().NotMatchRegex(
            @"roleCodes\.Any\s*\(\s*code\s*=>\s*string\.Equals\s*\(\s*code\s*,\s*""SUPERADMIN""",
            "hardcoded 'SUPERADMIN' fallback musí být odstraněn; zdrojem pravdy je jen AuthzSuperadmins → osoba.IsSuperAdmin");
    }

    [Fact]
    public void UserContextResolver_ShouldStillSetIsSuperAdminFromOsoba()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Security/UserContextResolver.cs"));

        code.Should().Contain("var isSuperAdmin = osoba.IsSuperAdmin;",
            "osoba.IsSuperAdmin z ResolvedPersonRow (napojené na AuthzSuperadmins) musí zůstat jediným zdrojem");
    }
}
```

### Step 2 — FAIL

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~SuperAdminSourceTests" -v q
```

Expected: první test FAIL (regex matchne existing fallback), druhý PASS.

### Step 3 — Smazat fallback

V `PmTracker.Web/Services/Security/UserContextResolver.cs` najdi blok:

```csharp
var isSuperAdmin = osoba.IsSuperAdmin;
if (!isSuperAdmin)
{
    isSuperAdmin = roleCodes.Any(code => string.Equals(code, "SUPERADMIN", StringComparison.OrdinalIgnoreCase));
}
```

Nahraď za:

```csharp
var isSuperAdmin = osoba.IsSuperAdmin;
```

### Step 4 — PASS + full suite

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~SuperAdminSourceTests" -v q
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -v q
```

Expected: target 2/2 PASS, full suite 698/698 (696 + 2 nových).

**Pokud nějaký existing test rozbije:** např. test co vytvoří osobu s AuthzUserRole="SUPERADMIN" bez záznamu v AuthzSuperadmins, a očekává `IsSuperAdmin=true` — to byl pre-existing nebezpečný pattern. Oprav test aby pridal osobu do `AuthzSuperadmins`, ne aby vracel fallback.

### Step 5 — Commit

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
git add PmTracker.Web/Services/Security/UserContextResolver.cs \
        PmTracker.Tests.Unit/Authorization/SuperAdminSourceTests.cs
git commit -m "refactor(authz): smazat hardcoded SUPERADMIN fallback, AuthzSuperadmins jediný zdroj"
```

---

## Fáze B — Completion Checklist

- [ ] 4 commity: B1, B2, B3, B4
- [ ] `ProjectRolePermissionGrantBuilder.cs` a `SubsystemRolePermissionGrantBuilder.cs` smazané
- [ ] Hardcoded `"SUPERADMIN"` fallback smazaný
- [ ] DB-driven `LoadDbDrivenProjectRoleGrantsAsync` + `LoadDbDrivenSubsystemRoleGrantsAsync` = jediné zdroje implicit projektových/subsystémových grantů
- [ ] Full test suite zelená (cca 698+ testů)
- [ ] VLASTNIK_PROJEKTU, GEST, plný ADM_PROJ matrix teď dostanou všechny seed keys (opravený bug z éry builderu)
- [ ] HOST zůstává bez grantů (dashboard.view/export přijdou ve Fázi C)
- [ ] Osoba se `"SUPERADMIN"` rolí ale bez záznamu v `AuthzSuperadmins` NENÍ super-admin
- [ ] Aplikace se chová identicky pro běžné uživatele (nebo lépe — opravený matrix pro VLASTNIK apod.)

---

## Známá rizika a mitigace

| Riziko | Mitigace |
|---|---|
| VLASTNIK_PROJEKTU teď dostane 8 keys místo 0 — někde v UI/controlleru se může nepříjemně odhalit dřívější "VLASTNIK nemá permissions, ale vidí UI" (nestandardní check) | B3 full suite test odhalí. Pokud nějaký test spoléhal na chybějící permissions, oprav ho. Pokud production UI spoléhá — vítejte na opravené authz. |
| Uživatel se `"SUPERADMIN"` rolí bez záznamu v `AuthzSuperadmins` ztratí super-admin status | B4 test to potvrdí. V produkci: zkontrolovat `SELECT o.jmeno FROM osoby o JOIN authz.user_roles ur ON ... JOIN authz.roles r ON ... WHERE r.kod='SUPERADMIN' AND NOT EXISTS (SELECT 1 FROM authz.superadmins s WHERE s.osoba_id = o.id)` — pokud někdo, přidej ho do AuthzSuperadmins PRED deploy. |
| EF InMemory provider může se chovat jinak než SQL Server u složitých JOINů | Testy pokrývají klíčové scénáře. Pokud jiný provider v produkci selže, dotáhneme to ve Fázi D s integration testy. |
| Nová DB cesta načítá více dat než starý builder (více keys × více projektů) | Query má AsNoTracking + projection do anonymního typu = minimální overhead. Pokud profilování ukáže problém, doplníme cache ve Fázi D. |
