# Authorization Unification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sjednotit dvě oddělené autorizační cesty (explicit DB grants + implicit code-driven builders) do **jediného DB-driven modelu**; zavést `IAuthorizationService` s defense-in-depth service-layer guardem; doplnit chybějící permission keys; zachovat zpětnou kompatibilitu a všechny existující use-cases.

**Architecture:** Propojíme projektové a subsystémové role se sjednoceným permission katalogem přes FK na `CiselnikRoliProjektu.AuthzRoleId` a `CiselnikRoliSubsystemu.AuthzRoleId`. Přidáme `AuthzRole.Scope` (GLOBAL/PROJECT/SUBSYSTEM) jako discriminator. Rezolvér čte všechno z jednoho DB queries, C# builders se smažou. Authorization service `IAuthorizationService` jako kanonická fasáda (ASP.NET Core Policy handler + service-layer `RequirePermissionAsync` guard).

**Tech Stack:** ASP.NET Core 8, EF Core 8 (fluent config v `Data/Configuration/*.cs`, snake_case columns, schema `authz` pro authz tabulky), xUnit + FluentAssertions, SQL Server 2019+ (idempotent `db_upgrade_X_Y_Z_*.sql` skripty v kořeni, ověřuje `SqlStartupValidatorHostedService`).

**Vazba na spec:** [docs/superpowers/specs/2026-04-21-authorization-unification-design.md](../specs/2026-04-21-authorization-unification-design.md).

**Pořadí fází:** A → B → C → D → E → F. Každá fáze je merge-safe (aplikace po každé funguje identicky nebo lépe).

---

## Přehled fází a stav

| Fáze | Scope | Status | Detailed plan |
|---|---|---|---|
| **A** | Infrastructure — DB migrace + seed (additive, non-breaking) | 🔴 Tento dokument obsahuje full detail | ✅ Níže |
| **B** | Resolver swap — čtení z DB, smazat builders, smazat hardcoded SUPERADMIN | 🔴 High-level níže | Napíše se po A |
| **C** | Gap fixes — Export auth, ProjectDashboard permission, comment checks, search.reindex | 🔴 High-level níže | Napíše se po B |
| **D** | `IAuthorizationService` + policy handler + service-layer guard + dedupe | 🔴 High-level níže | Napíše se po C |
| **E** | APP_ADMIN seed review + decision body | 🔴 High-level níže | Napíše se po D |
| **F** | UI Nastavení (Scope, source audit) + architecture tests + dokumentace | 🔴 High-level níže | Napíše se po E |

**Důvod postupného detailování:** každá fáze může ovlivnit dekompozici následující. Psát 55 detailních tasků naráz = 3000 řádků, které user nebude reálně reviewovat. Po fázi A máme konkrétní codebase context pro přesný detail B, atd.

---

# Fáze A — Infrastructure (non-breaking)

**Cíl fáze:** DB schéma rozšířené o discriminator `Scope` na `AuthzRole` a propojující FK `AuthzRoleId` na `CiselnikRoliProjektu` + `CiselnikRoliSubsystemu`. Seed vytvoří `AuthzRole` záznamy pro všechny projektové a subsystémové role codes. Data linker naplní FK dle match na `Kod`. Seed namapuje permissions podle role matice dle specu §3.7. **Aplikace po fázi A funguje identicky (existing builders stále aktivní).**

**Odhad:** ~1 den. 10 tasků, každý 15-45 min.

## Task A1: Entity property `AuthzRoleEntity.Scope`

**Files:**
- Modify: `PmTracker.Web/Models/Entities/PmTrackerEntities.cs:382-390` (add property to `AuthzRoleEntity`)
- Modify: `PmTracker.Web/Data/Configuration/AuthorizationEntityConfiguration.cs:50-63` (map column)
- Test: `PmTracker.Tests.Unit/Authorization/AuthzRoleScopeTests.cs` (new)

- [ ] **Step 1: Napsat failing test**

```csharp
// PmTracker.Tests.Unit/Authorization/AuthzRoleScopeTests.cs
using FluentAssertions;
using PmTracker.Web.Models.Entities;

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
            Scope = "PROJECT"
        };

        role.Scope.Should().Be("PROJECT");
    }

    [Fact]
    public void AuthzRoleEntity_Scope_ShouldDefaultToGlobal()
    {
        var role = new AuthzRoleEntity();
        role.Scope.Should().Be("GLOBAL");
    }
}
```

- [ ] **Step 2: Spustit test, ověřit FAIL**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~AuthzRoleScopeTests" -v q`

Expected: FAIL with `CS0117: 'AuthzRoleEntity' does not contain a definition for 'Scope'`

- [ ] **Step 3: Přidat property na entity**

V [PmTrackerEntities.cs:382](../../PmTracker.Web/Models/Entities/PmTrackerEntities.cs#L382) rozšířit `AuthzRoleEntity`:

```csharp
public sealed class AuthzRoleEntity
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Nazev { get; set; } = string.Empty;
    public string? Popis { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; }
    public string Scope { get; set; } = "GLOBAL";   // Nové: GLOBAL | PROJECT | SUBSYSTEM
}
```

- [ ] **Step 4: Přidat mapping v EF konfiguraci**

V [AuthorizationEntityConfiguration.cs:50-63](../../PmTracker.Web/Data/Configuration/AuthorizationEntityConfiguration.cs#L50-L63) doplnit property mapping:

```csharp
internal sealed class AuthorizationRoleEntityConfiguration : IEntityTypeConfiguration<AuthzRoleEntity>
{
    public void Configure(EntityTypeBuilder<AuthzRoleEntity> builder)
    {
        builder.ToTable("roles", "authz");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Kod).HasColumnName("kod");
        builder.Property(x => x.Nazev).HasColumnName("nazev");
        builder.Property(x => x.Popis).HasColumnName("popis");
        builder.Property(x => x.IsSystem).HasColumnName("is_system");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.Scope).HasColumnName("scope").HasMaxLength(16).IsRequired();
    }
}
```

- [ ] **Step 5: Spustit testy, ověřit PASS**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~AuthzRoleScopeTests" -v q`

Expected: PASS 2/2

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Models/Entities/PmTrackerEntities.cs \
        PmTracker.Web/Data/Configuration/AuthorizationEntityConfiguration.cs \
        PmTracker.Tests.Unit/Authorization/AuthzRoleScopeTests.cs
git commit -m "feat(authz): AuthzRoleEntity.Scope property (GLOBAL|PROJECT|SUBSYSTEM)"
```

---

## Task A2: SQL migrace pro `authz.roles.scope`

**Files:**
- Create: `db_upgrade_1_2_0_authz_role_scope.sql` (v kořeni repa)
- Modify: `PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs` (add validator)

- [ ] **Step 1: Vytvořit idempotent SQL upgrade skript**

Soubor: `/Users/Pavel.Andrlik/Documents/PM Tracker/db_upgrade_1_2_0_authz_role_scope.sql`

```sql
-- db_upgrade_1_2_0_authz_role_scope.sql
-- Authorization unification — Fáze A — Task A2
-- Spec: docs/superpowers/specs/2026-04-21-authorization-unification-design.md
-- Plan: docs/superpowers/plans/2026-04-21-authorization-unification.md

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1. Přidat sloupec scope do authz.roles (default 'GLOBAL' pro existing rows).
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('authz.roles') AND name = 'scope'
)
BEGIN
    ALTER TABLE authz.roles
        ADD scope NVARCHAR(16) NOT NULL CONSTRAINT df_authz_roles_scope DEFAULT 'GLOBAL';
END;

-- 2. CHECK constraint pro povolené hodnoty.
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID('authz.roles') AND name = 'ck_authz_roles_scope'
)
BEGIN
    ALTER TABLE authz.roles
        ADD CONSTRAINT ck_authz_roles_scope
            CHECK (scope IN ('GLOBAL','PROJECT','SUBSYSTEM'));
END;

COMMIT TRANSACTION;
```

- [ ] **Step 2: Napsat failing test pro SqlStartupValidator**

V souboru `PmTracker.Tests.Unit/Data/SqlStartupValidatorTests.cs` (nebo vytvořit nový `PmTracker.Tests.Unit/Authorization/AuthzSchemaValidatorTests.cs`):

```csharp
// PmTracker.Tests.Unit/Authorization/AuthzSchemaValidatorTests.cs
using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class AuthzSchemaValidatorTests
{
    [Fact]
    public void SqlStartupValidator_ShouldReferenceAuthzRoleScopeUpgrade()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs"));

        code.Should().Contain("db_upgrade_1_2_0_authz_role_scope",
            "validátor musí uživatele informovat o potřebě spustit migraci, pokud chybí sloupec authz.roles.scope");
    }
}
```

- [ ] **Step 3: Spustit test, ověřit FAIL**

Run: `dotnet test --filter "AuthzSchemaValidatorTests"`

Expected: FAIL — referenční řetězec zatím v kódu není

- [ ] **Step 4: Rozšířit SqlStartupValidator o check `authz.roles.scope` sloupce**

V [SqlStartupValidatorHostedService.cs](../../PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs), za existující bloky přidat novou kontrolu (obvykle blízko ostatních column checks, např. po subsystem order check):

```csharp
// Authorization unification — Fáze A — migrace db_upgrade_1_2_0
var hasAuthzRoleScope = await connection.QueryFirstOrDefaultAsync<int?>(
    @"SELECT 1 FROM sys.columns
      WHERE object_id = OBJECT_ID('authz.roles') AND name = 'scope';",
    transaction: null);

if (hasAuthzRoleScope is null)
{
    throw new InvalidOperationException(
        "V DB chybí sloupec authz.roles.scope. Obnovte databázi přes PMTracker_insert_sql nebo spusťte db_upgrade_1_2_0_authz_role_scope.sql.");
}
```

(Přesná syntaxe — `QueryFirstOrDefaultAsync` nebo `ExecuteScalarAsync` — podle patternu ostatních checks v tom souboru; ADAPTUJ dle existing kódu.)

- [ ] **Step 5: Spustit SQL skript proti dev DB**

```bash
# Docker / colima SQL localhost
sqlcmd -S localhost -U sa -P 'PmTracker!2026' -d PmTracker \
  -i "/Users/Pavel.Andrlik/Documents/PM Tracker/db_upgrade_1_2_0_authz_role_scope.sql"
```

Expected: `Commands completed successfully.`

Ověř: `SELECT TOP 5 id, kod, scope FROM authz.roles;` — sloupec existuje, existing rows mají `scope='GLOBAL'`.

- [ ] **Step 6: Spustit dev server, ověřit startup nefalíruje**

```bash
cd PmTracker.Web && ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build --urls "http://localhost:5071;https://localhost:7071"
```

Expected log: `SQL startup validace proběhla úspěšně.`

- [ ] **Step 7: Spustit testy, všechny zelené**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -v q`

Expected: PASS (654+1 = 655)

- [ ] **Step 8: Commit**

```bash
git add db_upgrade_1_2_0_authz_role_scope.sql \
        PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs \
        PmTracker.Tests.Unit/Authorization/AuthzSchemaValidatorTests.cs
git commit -m "feat(authz): SQL migrace pro authz.roles.scope + validátor"
```

---

## Task A3: Entity property + EF mapping pro `CiselnikRoliProjektuEntity.AuthzRoleId`

**Files:**
- Modify: `PmTracker.Web/Models/Entities/PmTrackerEntities.cs:45-51` (add nullable FK)
- Modify: `PmTracker.Web/Data/Configuration/LookupEntityConfiguration.cs:79-90` (map column)
- Test: `PmTracker.Tests.Unit/Authorization/ProjectRoleAuthzLinkTests.cs`

- [ ] **Step 1: Failing test**

```csharp
// PmTracker.Tests.Unit/Authorization/ProjectRoleAuthzLinkTests.cs
using FluentAssertions;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class ProjectRoleAuthzLinkTests
{
    [Fact]
    public void CiselnikRoliProjektuEntity_ShouldHaveAuthzRoleIdProperty()
    {
        var role = new CiselnikRoliProjektuEntity
        {
            Kod = "ADM_PROJ",
            Nazev = "Projektový admin",
            AuthzRoleId = 42
        };

        role.AuthzRoleId.Should().Be(42);
    }

    [Fact]
    public void CiselnikRoliProjektuEntity_AuthzRoleId_ShouldBeNullable()
    {
        var role = new CiselnikRoliProjektuEntity { Kod = "X", Nazev = "Y" };
        role.AuthzRoleId.Should().BeNull();
    }
}
```

- [ ] **Step 2: Spustit test, FAIL**

Run: `dotnet test --filter "ProjectRoleAuthzLinkTests"`

Expected: FAIL — property neexistuje

- [ ] **Step 3: Přidat property**

V [PmTrackerEntities.cs:45-51](../../PmTracker.Web/Models/Entities/PmTrackerEntities.cs#L45-L51):

```csharp
public sealed class CiselnikRoliProjektuEntity
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Nazev { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public int? AuthzRoleId { get; set; }   // Nové: FK na authz.roles(id), nullable během migrace
}
```

- [ ] **Step 4: EF mapping**

V [LookupEntityConfiguration.cs:79-90](../../PmTracker.Web/Data/Configuration/LookupEntityConfiguration.cs#L79-L90) doplnit:

```csharp
internal sealed class ProjectRoleLookupEntityConfiguration : IEntityTypeConfiguration<CiselnikRoliProjektuEntity>
{
    public void Configure(EntityTypeBuilder<CiselnikRoliProjektuEntity> builder)
    {
        builder.ToTable("ciselnik_roli_projektu");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Kod).HasColumnName("kod");
        builder.Property(x => x.Nazev).HasColumnName("nazev");
        builder.Property(x => x.IsLocked).HasColumnName("is_locked");
        builder.Property(x => x.AuthzRoleId).HasColumnName("authz_role_id");
    }
}
```

- [ ] **Step 5: Spustit testy, PASS**

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Models/Entities/PmTrackerEntities.cs \
        PmTracker.Web/Data/Configuration/LookupEntityConfiguration.cs \
        PmTracker.Tests.Unit/Authorization/ProjectRoleAuthzLinkTests.cs
git commit -m "feat(authz): CiselnikRoliProjektuEntity.AuthzRoleId nullable FK"
```

---

## Task A4: Entity property + EF mapping pro `CiselnikRoleSubsystemuEntity.AuthzRoleId`

**Files:**
- Modify: `PmTracker.Web/Models/Entities/PmTrackerEntities.cs:53-59`
- Modify: `PmTracker.Web/Data/Configuration/LookupEntityConfiguration.cs:92-103`
- Test: `PmTracker.Tests.Unit/Authorization/SubsystemRoleAuthzLinkTests.cs`

- [ ] **Step 1: Failing test**

```csharp
// PmTracker.Tests.Unit/Authorization/SubsystemRoleAuthzLinkTests.cs
using FluentAssertions;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class SubsystemRoleAuthzLinkTests
{
    [Fact]
    public void CiselnikRoleSubsystemuEntity_ShouldHaveAuthzRoleIdProperty()
    {
        var role = new CiselnikRoleSubsystemuEntity
        {
            Kod = "VEDOUCI_SUBSYSTEMU",
            Nazev = "Vedoucí subsystému",
            AuthzRoleId = 42
        };

        role.AuthzRoleId.Should().Be(42);
    }

    [Fact]
    public void CiselnikRoleSubsystemuEntity_AuthzRoleId_ShouldBeNullable()
    {
        var role = new CiselnikRoleSubsystemuEntity { Kod = "X", Nazev = "Y" };
        role.AuthzRoleId.Should().BeNull();
    }
}
```

- [ ] **Step 2: FAIL**

- [ ] **Step 3: Přidat property**

V [PmTrackerEntities.cs:53-59](../../PmTracker.Web/Models/Entities/PmTrackerEntities.cs#L53-L59):

```csharp
public sealed class CiselnikRoleSubsystemuEntity
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Nazev { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public int? AuthzRoleId { get; set; }
}
```

- [ ] **Step 4: EF mapping**

V [LookupEntityConfiguration.cs:92-103](../../PmTracker.Web/Data/Configuration/LookupEntityConfiguration.cs#L92-L103):

```csharp
internal sealed class SubsystemRoleLookupEntityConfiguration : IEntityTypeConfiguration<CiselnikRoleSubsystemuEntity>
{
    public void Configure(EntityTypeBuilder<CiselnikRoleSubsystemuEntity> builder)
    {
        builder.ToTable("ciselnik_roli_subsystemu");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Kod).HasColumnName("kod");
        builder.Property(x => x.Nazev).HasColumnName("nazev");
        builder.Property(x => x.IsLocked).HasColumnName("is_locked");
        builder.Property(x => x.AuthzRoleId).HasColumnName("authz_role_id");
    }
}
```

- [ ] **Step 5: PASS**

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Models/Entities/PmTrackerEntities.cs \
        PmTracker.Web/Data/Configuration/LookupEntityConfiguration.cs \
        PmTracker.Tests.Unit/Authorization/SubsystemRoleAuthzLinkTests.cs
git commit -m "feat(authz): CiselnikRoleSubsystemuEntity.AuthzRoleId nullable FK"
```

---

## Task A5: SQL migrace pro `ciselnik_roli_projektu.authz_role_id` + `ciselnik_roli_subsystemu.authz_role_id`

**Files:**
- Create: `db_upgrade_1_2_1_lookup_role_authz_fk.sql`
- Modify: `PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs` (add 2 checks)

- [ ] **Step 1: SQL skript**

Soubor: `/Users/Pavel.Andrlik/Documents/PM Tracker/db_upgrade_1_2_1_lookup_role_authz_fk.sql`

```sql
-- db_upgrade_1_2_1_lookup_role_authz_fk.sql
-- Authorization unification — Fáze A — Task A5
-- Propojení lookup rolí (projektové + subsystémové) s authz.roles přes FK.

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1. ciselnik_roli_projektu.authz_role_id (nullable během migrace)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.ciselnik_roli_projektu') AND name = 'authz_role_id'
)
BEGIN
    ALTER TABLE dbo.ciselnik_roli_projektu
        ADD authz_role_id INT NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID('dbo.ciselnik_roli_projektu')
      AND name = 'fk_ciselnik_roli_projektu_authz_role'
)
BEGIN
    ALTER TABLE dbo.ciselnik_roli_projektu
        ADD CONSTRAINT fk_ciselnik_roli_projektu_authz_role
            FOREIGN KEY (authz_role_id) REFERENCES authz.roles(id);
END;

-- 2. ciselnik_roli_subsystemu.authz_role_id
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.ciselnik_roli_subsystemu') AND name = 'authz_role_id'
)
BEGIN
    ALTER TABLE dbo.ciselnik_roli_subsystemu
        ADD authz_role_id INT NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID('dbo.ciselnik_roli_subsystemu')
      AND name = 'fk_ciselnik_roli_subsystemu_authz_role'
)
BEGIN
    ALTER TABLE dbo.ciselnik_roli_subsystemu
        ADD CONSTRAINT fk_ciselnik_roli_subsystemu_authz_role
            FOREIGN KEY (authz_role_id) REFERENCES authz.roles(id);
END;

COMMIT TRANSACTION;
```

- [ ] **Step 2: Failing test pro validátor**

Rozšířit `AuthzSchemaValidatorTests.cs` z Tasku A2:

```csharp
[Fact]
public void SqlStartupValidator_ShouldReferenceLookupRoleAuthzFkUpgrade()
{
    var code = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs"));

    code.Should().Contain("db_upgrade_1_2_1_lookup_role_authz_fk",
        "validátor musí uživatele informovat o potřebě spustit migraci, pokud chybí FK sloupce");
}
```

- [ ] **Step 3: FAIL**

- [ ] **Step 4: Rozšířit SqlStartupValidator**

V `SqlStartupValidatorHostedService.cs` přidat dva column checks (stejný pattern jako A2):

```csharp
// Authorization unification — Fáze A — migrace db_upgrade_1_2_1
var hasProjectRoleAuthzFk = await connection.ExecuteScalarAsync<int?>(
    "SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ciselnik_roli_projektu') AND name = 'authz_role_id';");
if (hasProjectRoleAuthzFk is null)
{
    throw new InvalidOperationException(
        "V DB chybí sloupec dbo.ciselnik_roli_projektu.authz_role_id. Spusťte db_upgrade_1_2_1_lookup_role_authz_fk.sql.");
}

var hasSubsystemRoleAuthzFk = await connection.ExecuteScalarAsync<int?>(
    "SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ciselnik_roli_subsystemu') AND name = 'authz_role_id';");
if (hasSubsystemRoleAuthzFk is null)
{
    throw new InvalidOperationException(
        "V DB chybí sloupec dbo.ciselnik_roli_subsystemu.authz_role_id. Spusťte db_upgrade_1_2_1_lookup_role_authz_fk.sql.");
}
```

(Přesná syntaxe `ExecuteScalarAsync<int?>` dle existing pattern v souboru.)

- [ ] **Step 5: Spustit SQL migraci**

```bash
sqlcmd -S localhost -U sa -P 'PmTracker!2026' -d PmTracker \
  -i "/Users/Pavel.Andrlik/Documents/PM Tracker/db_upgrade_1_2_1_lookup_role_authz_fk.sql"
```

Expected: `Commands completed successfully.`

Ověř: `SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ciselnik_roli_projektu';` — `authz_role_id` v seznamu.

- [ ] **Step 6: Spustit dev server, validátor projde**

- [ ] **Step 7: Full test suite, PASS**

- [ ] **Step 8: Commit**

```bash
git add db_upgrade_1_2_1_lookup_role_authz_fk.sql \
        PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs \
        PmTracker.Tests.Unit/Authorization/AuthzSchemaValidatorTests.cs
git commit -m "feat(authz): SQL migrace pro ciselnik_roli_*.authz_role_id FK + validátor"
```

---

## Task A6: Seed — rozšířit `RoleSeedItem` o `Scope` + přidat projektové/subsystémové role

**Files:**
- Modify: `PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs`
- Test: `PmTracker.Tests.Unit/Authorization/RoleSeedCoverageTests.cs`

- [ ] **Step 1: Failing test**

```csharp
// PmTracker.Tests.Unit/Authorization/RoleSeedCoverageTests.cs
using FluentAssertions;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class RoleSeedCoverageTests
{
    [Fact]
    public void Roles_ShouldContainAllProjectScopeRoles()
    {
        var projectRoles = PermissionSeedConfiguration.Roles
            .Where(r => r.Scope == "PROJECT")
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
            .Where(r => r.Scope == "SUBSYSTEM")
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
            .Where(r => r.Scope == "GLOBAL")
            .Select(r => r.Kod)
            .OrderBy(x => x)
            .ToList();

        globalRoles.Should().Contain(new[] { "APP_ADMIN", "SUPERADMIN" });
    }
}
```

- [ ] **Step 2: FAIL** (record nemá `Scope` property, seed nemá ty role)

- [ ] **Step 3: Rozšířit `RoleSeedItem` o `Scope`**

V [PermissionSeedConfiguration.cs:4](../../PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs#L4):

```csharp
public sealed record RoleSeedItem(string Kod, string Nazev, string Popis, bool IsSystem, string Scope);
```

- [ ] **Step 4: Přidat projektové + subsystémové role do `Roles` seznamu**

V [PermissionSeedConfiguration.cs:19-23](../../PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs#L19-L23):

```csharp
public static readonly IReadOnlyList<RoleSeedItem> Roles =
[
    // Globální role
    new("SUPERADMIN", "Superadmin", "Pevná role s plnými oprávněními.", true, "GLOBAL"),
    new("APP_ADMIN", "Administrátor aplikace", "Správa aplikace a základních entit.", true, "GLOBAL"),

    // Projektové role (použité v ciselnik_roli_projektu)
    new("VLASTNIK_PROJEKTU", "Vlastník projektu", "Plný vlastník projektu.", true, "PROJECT"),
    new("ADM_PROJ", "Projektový admin", "Silný projektový admin bez práva měnit metadata.", true, "PROJECT"),
    new("PROJ_MAN", "Projektový manažer", "Projektový manažer bez úprav metadat.", true, "PROJECT"),
    new("HOST", "Host", "Read-only host projektu.", true, "PROJECT"),
    new("GEST", "Gestor", "Gestor s komentovacími právy.", true, "PROJECT"),

    // Subsystémové role (použité v ciselnik_roli_subsystemu)
    new("VEDOUCI_SUBSYSTEMU", "Vedoucí subsystému", "Vedoucí subsystému projektu.", true, "SUBSYSTEM"),
    new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "Zástupce vedoucího subsystému", "Zástupce vedoucího, stejná práva jako vedoucí.", true, "SUBSYSTEM"),
    new("METODIK_SUBSYSTEMU", "Metodik subsystému", "Metodik s komentovacími právy.", true, "SUBSYSTEM")
];
```

- [ ] **Step 5: Rozšířit `PermissionSeeder` aby zapisoval `Scope`**

V [PermissionSeeder.cs](../../PmTracker.Web/Services/Security/PermissionSeeder.cs), najít blok který upsertuje role (typicky `UpsertRolesAsync` nebo podobná metoda). Přidat `Scope` do upsert logiky:

```csharp
// Uvnitř UpsertRolesAsync (pseudokód, adaptuj dle existing):
foreach (var seedItem in PermissionSeedConfiguration.Roles)
{
    var existing = await _db.AuthzRoles.FirstOrDefaultAsync(r => r.Kod == seedItem.Kod, ct);
    if (existing is null)
    {
        _db.AuthzRoles.Add(new AuthzRoleEntity
        {
            Kod = seedItem.Kod,
            Nazev = seedItem.Nazev,
            Popis = seedItem.Popis,
            IsSystem = seedItem.IsSystem,
            IsActive = true,
            Scope = seedItem.Scope       // Nové
        });
    }
    else
    {
        existing.Nazev = seedItem.Nazev;
        existing.Popis = seedItem.Popis;
        existing.IsSystem = seedItem.IsSystem;
        existing.Scope = seedItem.Scope; // Nové — přepíše pro system rows
    }
}
await _db.SaveChangesAsync(ct);
```

(Adaptuj přesnou strukturu podle existing `PermissionSeeder.cs` kódu — preserve any retry / concurrent-safe patterns.)

- [ ] **Step 6: PASS — unit testy**

Run: `dotnet test --filter "RoleSeedCoverageTests"`

Expected: PASS 3/3

- [ ] **Step 7: Spustit dev server, zkontrolovat, že se nové role zaseedovaly**

Po startu: `SELECT kod, scope FROM authz.roles ORDER BY kod;` — mělo by ukázat 10 systémových rolí.

- [ ] **Step 8: Commit**

```bash
git add PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs \
        PmTracker.Web/Services/Security/PermissionSeeder.cs \
        PmTracker.Tests.Unit/Authorization/RoleSeedCoverageTests.cs
git commit -m "feat(authz): seed projektových + subsystémových rolí s Scope discriminator"
```

---

## Task A7: Data linker — propojit `CiselnikRoliProjektu.AuthzRoleId` dle `Kod` match

**Files:**
- Create: `PmTracker.Web/Services/Security/RoleCatalogLinker.cs`
- Modify: `PmTracker.Web/Services/Security/PermissionSeeder.cs` (call linker po seed rolí)
- Test: `PmTracker.Tests.Unit/Authorization/RoleCatalogLinkerTests.cs`

- [ ] **Step 1: Failing test**

```csharp
// PmTracker.Tests.Unit/Authorization/RoleCatalogLinkerTests.cs
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class RoleCatalogLinkerTests
{
    private static PmTrackerDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PmTrackerDbContext(options);
    }

    [Fact]
    public async Task LinkAsync_ShouldFillAuthzRoleId_ForProjectRole_WhenAuthzRoleWithSameKodExists()
    {
        await using var db = CreateInMemoryDb();
        db.AuthzRoles.Add(new AuthzRoleEntity { Id = 10, Kod = "ADM_PROJ", Nazev = "Projektový admin", IsActive = true, Scope = "PROJECT" });
        db.CiselnikRoliProjektu.Add(new CiselnikRoliProjektuEntity { Id = 1, Kod = "ADM_PROJ", Nazev = "Projektový admin" });
        await db.SaveChangesAsync();

        var linker = new RoleCatalogLinker(db);
        await linker.LinkAsync(CancellationToken.None);

        var linked = await db.CiselnikRoliProjektu.FirstAsync(r => r.Id == 1);
        linked.AuthzRoleId.Should().Be(10);
    }

    [Fact]
    public async Task LinkAsync_ShouldFillAuthzRoleId_ForSubsystemRole()
    {
        await using var db = CreateInMemoryDb();
        db.AuthzRoles.Add(new AuthzRoleEntity { Id = 20, Kod = "VEDOUCI_SUBSYSTEMU", Nazev = "Vedoucí", IsActive = true, Scope = "SUBSYSTEM" });
        db.CiselnikRoliSubsystemu.Add(new CiselnikRoleSubsystemuEntity { Id = 2, Kod = "VEDOUCI_SUBSYSTEMU", Nazev = "Vedoucí" });
        await db.SaveChangesAsync();

        var linker = new RoleCatalogLinker(db);
        await linker.LinkAsync(CancellationToken.None);

        var linked = await db.CiselnikRoliSubsystemu.FirstAsync(r => r.Id == 2);
        linked.AuthzRoleId.Should().Be(20);
    }

    [Fact]
    public async Task LinkAsync_ShouldSkipRow_WhenAuthzRoleWithMatchingKodMissing()
    {
        await using var db = CreateInMemoryDb();
        db.CiselnikRoliProjektu.Add(new CiselnikRoliProjektuEntity { Id = 3, Kod = "UNKNOWN_ROLE", Nazev = "Custom" });
        await db.SaveChangesAsync();

        var linker = new RoleCatalogLinker(db);
        await linker.LinkAsync(CancellationToken.None);

        var unlinked = await db.CiselnikRoliProjektu.FirstAsync(r => r.Id == 3);
        unlinked.AuthzRoleId.Should().BeNull();
    }

    [Fact]
    public async Task LinkAsync_ShouldNotOverwriteExistingLink()
    {
        await using var db = CreateInMemoryDb();
        db.AuthzRoles.Add(new AuthzRoleEntity { Id = 30, Kod = "PROJ_MAN", Nazev = "PM", IsActive = true, Scope = "PROJECT" });
        db.CiselnikRoliProjektu.Add(new CiselnikRoliProjektuEntity { Id = 4, Kod = "PROJ_MAN", Nazev = "PM", AuthzRoleId = 99 });
        await db.SaveChangesAsync();

        var linker = new RoleCatalogLinker(db);
        await linker.LinkAsync(CancellationToken.None);

        var kept = await db.CiselnikRoliProjektu.FirstAsync(r => r.Id == 4);
        kept.AuthzRoleId.Should().Be(99); // Linker nepřepsal manuálně nastavený FK
    }
}
```

- [ ] **Step 2: FAIL** — `RoleCatalogLinker` class neexistuje

- [ ] **Step 3: Implementovat `RoleCatalogLinker`**

Soubor: `PmTracker.Web/Services/Security/RoleCatalogLinker.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;

namespace PmTracker.Web.Services.Security;

/// <summary>
/// Propojuje CiselnikRoliProjektu a CiselnikRoliSubsystemu s authz.roles přes FK AuthzRoleId
/// dle match na sloupci Kod. Volá se z PermissionSeeder po seed rolí. Idempotent —
/// nepřepisuje existující link, jen naplní prázdné FK.
/// Viz docs/superpowers/specs/2026-04-21-authorization-unification-design.md §3.2.
/// </summary>
public sealed class RoleCatalogLinker
{
    private readonly PmTrackerDbContext _db;

    public RoleCatalogLinker(PmTrackerDbContext db)
    {
        _db = db;
    }

    public async Task LinkAsync(CancellationToken cancellationToken)
    {
        var authzRolesByKod = await _db.AuthzRoles
            .AsNoTracking()
            .ToDictionaryAsync(r => r.Kod, r => r.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var projectRoles = await _db.CiselnikRoliProjektu
            .Where(r => r.AuthzRoleId == null)
            .ToListAsync(cancellationToken);

        foreach (var row in projectRoles)
        {
            if (authzRolesByKod.TryGetValue(row.Kod, out var authzRoleId))
            {
                row.AuthzRoleId = authzRoleId;
            }
        }

        var subsystemRoles = await _db.CiselnikRoliSubsystemu
            .Where(r => r.AuthzRoleId == null)
            .ToListAsync(cancellationToken);

        foreach (var row in subsystemRoles)
        {
            if (authzRolesByKod.TryGetValue(row.Kod, out var authzRoleId))
            {
                row.AuthzRoleId = authzRoleId;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 4: PASS** — unit testy

Run: `dotnet test --filter "RoleCatalogLinkerTests"`

Expected: PASS 4/4

- [ ] **Step 5: Zapojit linker do PermissionSeeder**

V `PermissionSeeder.SeedAsync` (na konci, po všech upsertech), přidat:

```csharp
public async Task SeedAsync(CancellationToken ct)
{
    await UpsertCategoriesAsync(ct);
    await UpsertActionsAsync(ct);       // nebo UpsertPermissionsAsync
    await UpsertRolesAsync(ct);
    await UpsertRoleMappingsAsync(ct);  // existing
    await UpsertSuperadminsAsync(ct);   // existing

    // Authorization unification — Fáze A — Task A7
    var linker = new RoleCatalogLinker(_db);
    await linker.LinkAsync(ct);
}
```

(Adaptuj dle existing PermissionSeeder struktury.)

- [ ] **Step 6: Startup aplikace → linker naplní FK v dev DB**

Spustit dev server. Po startu ověř:

```sql
SELECT c.kod, c.authz_role_id, r.kod as authz_role_kod, r.scope
FROM ciselnik_roli_projektu c
LEFT JOIN authz.roles r ON r.id = c.authz_role_id;
```

Expected: každá row s existing match Kod má `authz_role_id` naplněný a odpovídající `authz_role_kod` + `scope='PROJECT'`.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Services/Security/RoleCatalogLinker.cs \
        PmTracker.Web/Services/Security/PermissionSeeder.cs \
        PmTracker.Tests.Unit/Authorization/RoleCatalogLinkerTests.cs
git commit -m "feat(authz): RoleCatalogLinker — data migrace CiselnikRoli* → AuthzRole přes Kod"
```

---

## Task A8: Seed — permission mapping pro projektové role (matice dle specu §3.7)

**Files:**
- Modify: `PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs` (doplnit do `RoleMappings`)
- Test: `PmTracker.Tests.Unit/Authorization/ProjectRolePermissionMatrixTests.cs`

**Poznámka k permission keys:** Ve Fázi A používáme jen **existující** keys. Nové keys (`dashboard.view`, `export.*`, `comments.*`, `search.reindex`) se přidávají ve Fázi C Task C1 — namapování na role doplníme současně s přidáním. Teď mappujeme jen to, co aktuálně v `PermissionKeys` je.

- [ ] **Step 1: Failing test**

```csharp
// PmTracker.Tests.Unit/Authorization/ProjectRolePermissionMatrixTests.cs
using FluentAssertions;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class ProjectRolePermissionMatrixTests
{
    private static string[] PermissionsFor(string roleKod) =>
        PermissionSeedConfiguration.RoleMappings
            .Where(m => m.RoleKod == roleKod && m.IsAllowed)
            .Select(m => m.ActionKlic)
            .OrderBy(x => x)
            .ToArray();

    [Fact]
    public void VLASTNIK_PROJEKTU_ShouldHaveFullProjectPermissions()
    {
        var perms = PermissionsFor("VLASTNIK_PROJEKTU");

        perms.Should().BeEquivalentTo(new[]
        {
            "meetings.create",
            "meetings.edit",
            "projects.edit",
            "records.comment.subsystemlead",
            "records.edit",
            "records.schedule.add",
            "records.schedule.edit",
            "team.manage"
        });
    }

    [Fact]
    public void ADM_PROJ_ShouldHaveProjectAdminMinusProjectsEdit()
    {
        var perms = PermissionsFor("ADM_PROJ");

        perms.Should().BeEquivalentTo(new[]
        {
            "meetings.create",
            "meetings.edit",
            "records.comment.subsystemlead",
            "records.edit",
            "records.schedule.add",
            "records.schedule.edit",
            "team.manage"
        });
    }

    [Fact]
    public void PROJ_MAN_ShouldMatchAdmProj()
    {
        PermissionsFor("PROJ_MAN").Should().BeEquivalentTo(PermissionsFor("ADM_PROJ"));
    }

    [Fact]
    public void HOST_ShouldHaveNoWritePermissions()
    {
        // HOST je read-only; permission keys pro read access (dashboard.view, export.*) přijdou ve Fázi C.
        // Ve Fázi A má HOST explicitně zero mapping (přístup k projektu získá přes ObsazeniProjektu).
        PermissionsFor("HOST").Should().BeEmpty();
    }

    [Fact]
    public void GEST_ShouldHaveCommentsOnly()
    {
        // GEST = read + komentář. comments.* keys přijdou ve Fázi C. Ve Fázi A jen records.comment.subsystemlead.
        PermissionsFor("GEST").Should().BeEquivalentTo(new[]
        {
            "records.comment.subsystemlead"
        });
    }
}
```

- [ ] **Step 2: FAIL** — seed zatím neobsahuje tyto mapping rows

- [ ] **Step 3: Rozšířit `RoleMappings` v seedu**

V [PermissionSeedConfiguration.cs:43-72](../../PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs#L43-L72), před uzavírací `];` přidat:

```csharp
    // Authorization unification — Fáze A — Task A8
    // Projektové role mappings (existující permission keys; doplnění dashboard/export/comments ve Fázi C)
    new("VLASTNIK_PROJEKTU", "projects.edit", "ALL", true),
    new("VLASTNIK_PROJEKTU", "records.edit", "ALL", true),
    new("VLASTNIK_PROJEKTU", "records.schedule.add", "ALL", true),
    new("VLASTNIK_PROJEKTU", "records.schedule.edit", "ALL", true),
    new("VLASTNIK_PROJEKTU", "records.comment.subsystemlead", "ALL", true),
    new("VLASTNIK_PROJEKTU", "meetings.create", "ALL", true),
    new("VLASTNIK_PROJEKTU", "meetings.edit", "ALL", true),
    new("VLASTNIK_PROJEKTU", "team.manage", "ALL", true),

    new("ADM_PROJ", "records.edit", "ALL", true),
    new("ADM_PROJ", "records.schedule.add", "ALL", true),
    new("ADM_PROJ", "records.schedule.edit", "ALL", true),
    new("ADM_PROJ", "records.comment.subsystemlead", "ALL", true),
    new("ADM_PROJ", "meetings.create", "ALL", true),
    new("ADM_PROJ", "meetings.edit", "ALL", true),
    new("ADM_PROJ", "team.manage", "ALL", true),

    new("PROJ_MAN", "records.edit", "ALL", true),
    new("PROJ_MAN", "records.schedule.add", "ALL", true),
    new("PROJ_MAN", "records.schedule.edit", "ALL", true),
    new("PROJ_MAN", "records.comment.subsystemlead", "ALL", true),
    new("PROJ_MAN", "meetings.create", "ALL", true),
    new("PROJ_MAN", "meetings.edit", "ALL", true),
    new("PROJ_MAN", "team.manage", "ALL", true),

    new("GEST", "records.comment.subsystemlead", "ALL", true)

    // HOST — záměrně prázdný (read-only, dashboard/export keys ve Fázi C)
```

**Pozor na scope semantics:** tyto mappings mají `ScopeMode='ALL'`. Interpretace z specu §3.3 krok 8: pro role se `Scope=PROJECT` znamená `ALL` = „všechny projekty, kde má osoba tuto roli přes ObsazeniProjektu". Rezolvér ve Fázi B tuto interpretaci aplikuje.

- [ ] **Step 4: PASS** — unit testy

Run: `dotnet test --filter "ProjectRolePermissionMatrixTests"`

Expected: PASS 5/5

- [ ] **Step 5: Spustit server, ověřit seed zapíše do DB**

```sql
SELECT r.kod, p.klic, rp.scope_mode, rp.is_allowed
FROM authz.role_permissions rp
JOIN authz.roles r ON r.id = rp.role_id
JOIN authz.permissions p ON p.id = rp.permission_id
WHERE r.scope = 'PROJECT'
ORDER BY r.kod, p.klic;
```

Expected: rows dle matice výše.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs \
        PmTracker.Tests.Unit/Authorization/ProjectRolePermissionMatrixTests.cs
git commit -m "feat(authz): seed permission mappingu pro projektové role (existing keys)"
```

---

## Task A9: Seed — permission mapping pro subsystémové role

**Files:**
- Modify: `PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs`
- Test: `PmTracker.Tests.Unit/Authorization/SubsystemRolePermissionMatrixTests.cs`

- [ ] **Step 1: Failing test**

```csharp
// PmTracker.Tests.Unit/Authorization/SubsystemRolePermissionMatrixTests.cs
using FluentAssertions;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class SubsystemRolePermissionMatrixTests
{
    private static string[] PermissionsFor(string roleKod) =>
        PermissionSeedConfiguration.RoleMappings
            .Where(m => m.RoleKod == roleKod && m.IsAllowed)
            .Select(m => m.ActionKlic)
            .OrderBy(x => x)
            .ToArray();

    [Fact]
    public void VEDOUCI_SUBSYSTEMU_ShouldHaveSubsystemLeadPermission()
    {
        PermissionsFor("VEDOUCI_SUBSYSTEMU").Should().BeEquivalentTo(new[]
        {
            "records.comment.subsystemlead"
        });
    }

    [Fact]
    public void ZASTUPCE_VEDOUCIHO_SUBSYSTEMU_ShouldMatchVedouci()
    {
        PermissionsFor("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU")
            .Should().BeEquivalentTo(PermissionsFor("VEDOUCI_SUBSYSTEMU"));
    }

    [Fact]
    public void METODIK_SUBSYSTEMU_ShouldBeEmpty_ForNow()
    {
        // Metodik nemá ve Fázi A žádný existing permission key.
        // Ve Fázi C po přidání comments.* získá komentovací práva.
        PermissionsFor("METODIK_SUBSYSTEMU").Should().BeEmpty();
    }
}
```

- [ ] **Step 2: FAIL**

- [ ] **Step 3: Doplnit seed**

V `PermissionSeedConfiguration.cs` v `RoleMappings` přidat:

```csharp
    new("VEDOUCI_SUBSYSTEMU", "records.comment.subsystemlead", "ALL", true),
    new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "records.comment.subsystemlead", "ALL", true)

    // METODIK_SUBSYSTEMU — prázdný ve Fázi A (comments.* přijdou ve Fázi C)
```

- [ ] **Step 4: PASS**

Run: `dotnet test --filter "SubsystemRolePermissionMatrixTests"`

Expected: PASS 3/3

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs \
        PmTracker.Tests.Unit/Authorization/SubsystemRolePermissionMatrixTests.cs
git commit -m "feat(authz): seed permission mappingu pro subsystémové role"
```

---

## Task A10: End-to-end ověření — integration test, že seed + linker naplní DB správně

**Files:**
- Create: `PmTracker.Tests.Integration/Authorization/SeedIntegrationTests.cs` (pokud `PmTracker.Tests.Integration` projekt existuje; jinak unit test s InMemory DB)

- [ ] **Step 1: Zjistit, jestli existuje integration test projekt**

```bash
ls /Users/Pavel.Andrlik/Documents/PM\ Tracker/PmTracker.Tests.Integration/ 2>/dev/null
```

Pokud existuje, test bude tam. Jinak unit test v `PmTracker.Tests.Unit/Authorization/SeedEndToEndTests.cs` s InMemory DB.

- [ ] **Step 2: Napsat end-to-end seed test**

```csharp
// PmTracker.Tests.Unit/Authorization/SeedEndToEndTests.cs (fallback na InMemory)
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class SeedEndToEndTests
{
    private static PmTrackerDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PmTrackerDbContext(options);
    }

    [Fact]
    public async Task FullSeed_ShouldCreateAllSystemRolesWithScope()
    {
        await using var db = CreateInMemoryDb();
        var seeder = new PermissionSeeder(db, NullLogger<PermissionSeeder>.Instance); // adaptuj dle konstruktoru
        await seeder.SeedAsync(CancellationToken.None);

        var roles = await db.AuthzRoles.AsNoTracking().ToListAsync();
        roles.Select(r => r.Kod).Should().Contain(new[]
        {
            "SUPERADMIN", "APP_ADMIN",
            "VLASTNIK_PROJEKTU", "ADM_PROJ", "PROJ_MAN", "HOST", "GEST",
            "VEDOUCI_SUBSYSTEMU", "ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "METODIK_SUBSYSTEMU"
        });

        roles.First(r => r.Kod == "ADM_PROJ").Scope.Should().Be("PROJECT");
        roles.First(r => r.Kod == "VEDOUCI_SUBSYSTEMU").Scope.Should().Be("SUBSYSTEM");
        roles.First(r => r.Kod == "APP_ADMIN").Scope.Should().Be("GLOBAL");
    }

    [Fact]
    public async Task FullSeed_ShouldLinkLookupRolesToAuthzRoles()
    {
        await using var db = CreateInMemoryDb();

        // Arrange — existing lookup rows simulující stav po db_upgrade (prázdné AuthzRoleId)
        db.CiselnikRoliProjektu.AddRange(
            new CiselnikRoliProjektuEntity { Kod = "ADM_PROJ", Nazev = "Admin" },
            new CiselnikRoliProjektuEntity { Kod = "HOST", Nazev = "Host" });
        db.CiselnikRoliSubsystemu.Add(
            new CiselnikRoleSubsystemuEntity { Kod = "VEDOUCI_SUBSYSTEMU", Nazev = "Vedoucí" });
        await db.SaveChangesAsync();

        var seeder = new PermissionSeeder(db, NullLogger<PermissionSeeder>.Instance);
        await seeder.SeedAsync(CancellationToken.None);

        var admProj = await db.CiselnikRoliProjektu.FirstAsync(r => r.Kod == "ADM_PROJ");
        admProj.AuthzRoleId.Should().NotBeNull();

        var host = await db.CiselnikRoliProjektu.FirstAsync(r => r.Kod == "HOST");
        host.AuthzRoleId.Should().NotBeNull();

        var vedouci = await db.CiselnikRoliSubsystemu.FirstAsync(r => r.Kod == "VEDOUCI_SUBSYSTEMU");
        vedouci.AuthzRoleId.Should().NotBeNull();
    }

    [Fact]
    public async Task FullSeed_ShouldCreateRolePermissionsForProjectRoles()
    {
        await using var db = CreateInMemoryDb();
        var seeder = new PermissionSeeder(db, NullLogger<PermissionSeeder>.Instance);
        await seeder.SeedAsync(CancellationToken.None);

        var admProjRole = await db.AuthzRoles.FirstAsync(r => r.Kod == "ADM_PROJ");
        var admProjPerms = await (
            from rp in db.AuthzRolePermissions.AsNoTracking()
            join p in db.AuthzPermissions.AsNoTracking() on rp.PermissionId equals p.Id
            where rp.RoleId == admProjRole.Id && rp.IsAllowed
            select p.Klic).ToListAsync();

        admProjPerms.Should().Contain(new[] { "records.edit", "meetings.edit", "team.manage" });
        admProjPerms.Should().NotContain("projects.edit"); // ADM_PROJ úmyslně bez projects.edit
    }
}
```

(Pozn.: Přesný konstruktor `PermissionSeeder` adaptuj dle existing kódu. Pokud `PmTracker.Tests.Integration` projekt existuje, test patří tam s real SQL, ne InMemory — ale logika je stejná.)

- [ ] **Step 2: FAIL** (dokud se předchozí Tasky nespustí dohromady)

- [ ] **Step 3: Ověřit, že všechny Tasky A1-A9 jsou commited**

```bash
git log --oneline | head -15 | grep -E "A[0-9]+|authz" 
```

Expected: vidíš 9+ commitů s `feat(authz)` z Tasks A1-A9.

- [ ] **Step 4: PASS**

Run: `dotnet test --filter "SeedEndToEndTests"`

Expected: PASS 3/3

- [ ] **Step 5: Full test suite**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -v q`

Expected: PASS (cca 670 testů, +~16 z fáze A)

- [ ] **Step 6: Manual verification — dev server startup**

```bash
cd PmTracker.Web && ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build --urls "http://localhost:5071;https://localhost:7071"
```

Log musí obsahovat:
- `SQL startup validace proběhla úspěšně.`
- `PermissionSeeder: ... rolí zapsáno/aktualizováno` (podle existing logging)

DB dotazy:
```sql
-- 10 systémových rolí se scope:
SELECT kod, scope FROM authz.roles WHERE is_system = 1 ORDER BY scope, kod;
-- Očekávání: APP_ADMIN/GLOBAL, SUPERADMIN/GLOBAL, ADM_PROJ/PROJECT, GEST/PROJECT,
--           HOST/PROJECT, PROJ_MAN/PROJECT, VLASTNIK_PROJEKTU/PROJECT,
--           METODIK_SUBSYSTEMU/SUBSYSTEM, VEDOUCI_SUBSYSTEMU/SUBSYSTEM,
--           ZASTUPCE_VEDOUCIHO_SUBSYSTEMU/SUBSYSTEM

-- Lookup rows propojené s authz:
SELECT c.kod, r.scope
FROM ciselnik_roli_projektu c
JOIN authz.roles r ON r.id = c.authz_role_id;
-- Expected: všechny existing rows s match kod

-- Permission mapping pro ADM_PROJ:
SELECT p.klic
FROM authz.role_permissions rp
JOIN authz.roles r ON r.id = rp.role_id
JOIN authz.permissions p ON p.id = rp.permission_id
WHERE r.kod = 'ADM_PROJ' AND rp.is_allowed = 1
ORDER BY p.klic;
-- Expected: 7 rows (meetings.create/edit, records.comment.subsystemlead, records.edit,
--                   records.schedule.add, records.schedule.edit, team.manage)
```

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Tests.Unit/Authorization/SeedEndToEndTests.cs
git commit -m "test(authz): end-to-end seed integration test — Fáze A complete"
```

---

## Fáze A — Completion Checklist

Po Tasku A10 je Fáze A hotová. Ověř:

- [ ] Commit graph: 10 commitů `feat(authz):` a `test(authz):`
- [ ] Full test suite: 670+ testů zelených
- [ ] Dev server startup: SQL validace + seed run bez chyby
- [ ] DB stav:
  - `authz.roles` má 10 systémových rolí se správným `scope`
  - `ciselnik_roli_projektu.authz_role_id` naplněný pro existing rows
  - `ciselnik_roli_subsystemu.authz_role_id` naplněný pro existing rows
  - `authz.role_permissions` má rows pro projektové + subsystémové role
- [ ] **Aplikace funguje identicky jako před Fází A** — ProjectRolePermissionGrantBuilder a SubsystemRolePermissionGrantBuilder jsou stále aktivní (resolver je volá beze změny). Data-driven cesta je připravená, ale neaktivní.

**Po Fázi A následuje Fáze B** — resolver přepne na čtení z DB a builders se smažou. Detailní plán pro Fázi B se napíše po dokončení A (aby byl přesný vůči aktuálnímu stavu).

---

# Fáze B — Resolver swap (high-level)

**Cíl:** Rezolvér čte implicit granty (projektové + subsystémové role) z DB přes FK. Smazat `ProjectRolePermissionGrantBuilder`, `SubsystemRolePermissionGrantBuilder`. Smazat hardcoded `"SUPERADMIN"` fallback v `UserContextResolver.cs:242`. Po fázi B jsou oba code paths sjednocené.

**Tasky (vysoký level):**
- **B1**: `UserContextResolver` — načítá projektové role granty z DB (JOIN `ObsazeniProjektu` × `CiselnikRoliProjektu` × `AuthzRoles` × `AuthzRolePermissions`)
- **B2**: `UserContextResolver` — načítá subsystémové role granty z DB (analogicky)
- **B3**: Smazat `ProjectRolePermissionGrantBuilder` + `SubsystemRolePermissionGrantBuilder` + unit testy. Přepsat `UserAuthorizationSnapshotBuilder` stejným patternem.
- **B4**: Smazat hardcoded `"SUPERADMIN"` fallback na [UserContextResolver.cs:239-243](../../PmTracker.Web/Services/Security/UserContextResolver.cs#L239-L243). SuperAdmin pouze z `AuthzSuperadmins`.

**Detail B1-B4 se napíše po dokončení Fáze A** — bude obsahovat přesné code snippety pro upravený resolver + migration safety tests (ověření, že každý user má po swap stejné granty jako před, přes grant-level diff test).

**Odhad B:** ~1 den, 4 tasky.

---

# Fáze C — Gap fixes: chybějící permission keys + security holes (high-level)

**Cíl:** Doplnit 7 nových permission keys (`dashboard.view`, `export.pdf`, `export.word`, `comments.add`, `comments.edit.own`, `comments.delete.own`, `search.reindex`). Přidat je do seed mappingů (jak pro globální role, tak pro projektové/subsystémové role dle matice specu §3.7). Opravit 4 security gaps.

**Tasky:**
- **C1**: Definovat nové permission keys (konstanty + seed actions + kategorie)
- **C2**: Doplnit seed mappingy pro nové keys (SUPERADMIN + APP_ADMIN pro všechny; projektové role dostanou dashboard+export+comments dle matice; subsystémové role dostanou comments)
- **C3**: `ExportController.Pdf` — přidat permission check (`HasPermission(ExportPdf, projektId)`)
- **C4**: `ProjectDashboardController` — nahradit `IsSuperAdmin` bypass za `HasPermission(DashboardView, projektId)`
- **C5**: `ZaznamyController` comment actions — nahradit `hasPermission: () => true` explicitním `HasPermission(CommentsAdd/Edit/Delete, projektId)`
- **C6**: `SearchController.Reindex` — nahradit `IsSuperAdmin` za `HasPermission(SearchReindex)`

**Odhad C:** ~1 den, 6 tasků.

---

# Fáze D — `IAuthorizationService` + defense-in-depth (high-level)

**Cíl:** Vytvořit `IAuthorizationService` jako kanonickou fasádu. Zavést ASP.NET Core policy-based authorization (`[Authorize(Policy=...)]`). Přidat service-layer guard pro write commands. Dedupe existing controller+service duplicitních checků.

**Tasky:**
- **D1**: Vytvořit `IAuthorizationService` interface + `AuthorizationService` implementaci (HasPermission, CanAccessProject, RequirePermissionAsync). DI Scoped.
- **D2**: `PermissionRequirement` + `PermissionRequirementHandler` + `PermissionPolicies` konstanty. Registrace policies v `Program.cs` (1 policy per key).
- **D3**: Service-layer guard — injekt `IAuthorizationService` do top 5 write services (Projekt, Meeting, Record, People, Settings). Přidat `RequirePermissionAsync` na začátek každého write command. Test per service.
- **D4**: Migrovat 5 controllerů na `[Authorize(Policy=X)]` atributy (ProjektyController, JednaniController, ZaznamyController, OsobyController, NastaveniController).
- **D5**: Odstranit duplicitní controller checks kde existuje service-layer guard.
- **D6**: Zbytek controllerů (Ciselniky, Export, Profil, ProjectDashboard, Search) — per-controller commit.

**Odhad D:** ~1.5 dne, 6 tasků.

---

# Fáze E — APP_ADMIN seed finalizace (high-level)

**Cíl:** Dořešit rozhodnutí ohledně APP_ADMIN permissions v rámci nového modelu.

**Tasky:**
- **E1**: Přidat `settings.manage` do APP_ADMIN seedu (rationale: bez toho je APP_ADMIN nepraktický, SUPERADMIN má defense-in-depth přes `projects.delete`).
- **E2**: Rozhodnout `projects.delete` pro APP_ADMIN (spec defaultně doporučuje NE — ponechat defense-in-depth). User decision point.
- **E3**: Přidat `search.reindex` do APP_ADMIN seedu.

**Odhad E:** ~0.5 dne, 3 tasky.

---

# Fáze F — UI Nastavení + architecture tests + dokumentace (high-level)

**Cíl:** Nastavení UI dostává Scope dropdown pro role editor, permission picker filtruje podle scope, effective rights preview ukazuje source. Architecture tests zabrání regresi. Dokumentace aktualizovaná.

**Tasky:**
- **F1**: Role editor modal (`RoleModal.cshtml`) — přidat Scope dropdown (GLOBAL/PROJECT/SUBSYSTEM).
- **F2**: `RolePermissionModal` — filtr permissions dle Scope vybrané role.
- **F3**: Effective rights preview — rozšířit `EffectivePermissionRowViewModel` o SourceType, SourceRoleCode, SourceProjectName; UI zobrazí audit trail.
- **F4**: Architecture tests `AuthorizationArchitectureTests`:
  - `NoHardcodedRoleCodesInServices` (grep)
  - `AllPermissionKeysHaveSeededMapping`
  - `AllWriteControllerActionsHavePolicyOrServiceGuard` (reflection)
  - `AllWriteServicesHaveAuthzGuard` (reflection)
- **F5**: Update [docs/technical/07-security-authz.md](../../docs/technical/07-security-authz.md) + `docs/user-guide.md`.

**Odhad F:** ~1 den, 5 tasků.

---

# Celkové shrnutí

| Fáze | Tasků | Odhad | Detail stav |
|---|---|---|---|
| A | 10 | 1 den | ✅ Tento dokument (kompletní code-level detail) |
| B | 4 | 1 den | High-level; detail po A |
| C | 6 | 1 den | High-level; detail po B |
| D | 6 | 1.5 dne | High-level; detail po C |
| E | 3 | 0.5 dne | High-level; detail po D |
| F | 5 | 1 den | High-level; detail po E |
| **∑** | **34 tasků** (místo původně plánovaných 55 — sloučeno) | **~6 dní full-time** | |

## Rollback strategie per fáze

- **Fáze A:** ryze additive (nullable sloupce, nové seed rows). Rollback = `git revert` commity + drop nových sloupců (bezpečné, nepoužívají se aktivně).
- **Fáze B:** přepíná resolver. Rollback = `git revert` kódu (DB změny z A zůstávají, funkčně neutrální).
- **Fáze C:** doplňuje permission keys. Rollback = `git revert` + keys zůstávají v DB (nejsou volané, neškodí).
- **Fáze D:** zavádí `IAuthorizationService`. Rollback per-controller — D4+D5 jsou per-controller commity.
- **Fáze E:** seed changes. Idempotent, nepřepisuje custom data.
- **Fáze F:** UI a dokumentace. Rollback bez vlivu na funkci.

## Definition of Done (pro celou plán)

- Všech ~34 tasků checked-off ve všech fázích
- 100% unit testů zelených (odhad 700+ po všech fázích)
- Architecture tests F4 zelené
- Manual QA: super-admin, app-admin, project manager, subsystem lead, host — každý projde svým flow
- `ProjectRolePermissionGrantBuilder` a `SubsystemRolePermissionGrantBuilder` smazané
- Žádný hardcoded `"SUPERADMIN"` string v services/controllers (kromě seed configu)
- Dokumentace aktualizovaná
