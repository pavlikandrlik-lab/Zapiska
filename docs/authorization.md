# Autorizace v PM Trackeru

## Architektura

PM Tracker používá **seed-only RBAC** — role, permission klíče a jejich mapování jsou definovány výhradně v kódu (`PermissionSeedConfiguration`), verzovány v gitu a aplikovány při startu přes `PermissionSeeder`. Žádná runtime UI pro kompozici rolí neexistuje. Při každém HTTP requestu se z databáze sestaví `AuthorizationSnapshot` — kanonická in-memory projekce efektivních oprávnění dané osoby. Atribut `[Authorize(Policy = "permission:xxx")]` deleguje kontrolu na `PermissionAuthorizationHandler`, který čte snapshot přes `IAuthorizationService`. Pro programmatické checks v service vrstvě slouží `IAuthorizationService.HasPermissionAsync`.

---

## Klíčové typy

| Typ | Soubor | Účel |
|-----|--------|------|
| `PermissionKeys` | `Models/ViewModels/SecurityViewModels.cs` | Konstanty klíčů (kompilovně-bezpečně), `AllDefinitions` pro registraci policies |
| `AuthorizationSnapshot` | `Services/Security/AuthorizationSnapshot.cs` | Per-request projekce efektivních oprávnění (global + per-project + per-subsystem) |
| `IAuthorizationService` | `Services/Security/IAuthorizationService.cs` | Kanonická fasáda, per-request cached |
| `PermissionAuthorizationHandler` | `Services/Security/PermissionAuthorizationHandler.cs` | Spojení ASP.NET Policy s doménovým klíčem; čte `projektId`/`subsystemId` z route |
| `PermissionSeedConfiguration` | `Services/Security/PermissionSeedConfiguration.cs` | `Actions` + `Roles` + `RoleMappings` — jediný zdroj pravdy |
| `PermissionSeeder` | `Services/Security/PermissionSeeder.cs` | Aplikuje seed do DB při startu aplikace |
| `UserAuthorizationAuditSnapshot` | `Services/Settings/` | Audit view pro UI „Efektivní práva" |

---

## Jak přidat nový permission klíč

1. Přidat konstantu do `PermissionKeys` (např. `public const string FooBar = "foo.bar";`).
2. Přidat definici do `PermissionKeys` pole `Definitions` (soukromé pole, přes něj se exponuje `AllDefinitions`):
   ```csharp
   new(FooBar, "Popis akce", "CATEGORY_KOD", "PROJECT", "Detailní popis."),
   ```
3. Přidat do `PermissionSeedConfiguration.Actions`:
   ```csharp
   new("foo.bar", "Popis akce", "CATEGORY_KOD", PermissionScopeLevel.Project),
   ```
4. Přidat mapping do `PermissionSeedConfiguration.RoleMappings` pro příslušné role:
   ```csharp
   new("VLASTNIK_PROJEKTU", "foo.bar", ScopeMode.All, true),
   new("ADM_PROJ", "foo.bar", ScopeMode.All, true),
   ```
5. Použít v controlleru nebo service:
   ```csharp
   // Declarativně na action:
   [Authorize(Policy = "permission:foo.bar")]
   public IActionResult MyAction(int projektId) { ... }

   // Nebo programmaticky v service:
   if (!await _authzService.HasPermissionAsync(osobaId, PermissionKeys.FooBar, projektId, null, ct))
       throw new ForbiddenException();
   ```
6. Deploy — `PermissionSeeder` při startu synchronizuje seed do DB automaticky.

> **Architecture test:** `SeedSourceOfTruthTests.Every_PermissionKeys_Constant_MustBe_Seeded` selže, pokud krok 2 nebo 3 chybí.

---

## Jak přidat novou roli

1. Přidat do `PermissionSeedConfiguration.Roles`:
   ```csharp
   new("NOVA_ROLE", "Nová role", "Popis role.", true, RoleScope.Project),
   ```
2. Přidat mappings do `PermissionSeedConfiguration.RoleMappings`:
   ```csharp
   new("NOVA_ROLE", "records.edit", ScopeMode.All, true),
   new("NOVA_ROLE", "dashboard.view", ScopeMode.All, true),
   ```
3. Přiřazení uživateli závisí na `RoleScope`:
   - `Global` → admin přiřadí přes UI **Nastavení → Role uživatelů** (tabulka `authz.user_roles`)
   - `Project` → přes **Obsazení projektu** (tabulka `obsazeni_projektu`)
   - `Subsystem` → přes **Obsazení subsystému** (tabulka `obsazeni_subsystemu_projektu`)
4. `RoleCatalogLinker` propojí `CiselnikRoliProjektu` / `CiselnikRoliSubsystemu` s `AuthzRole.Id` při startu aplikace.

> **Architecture test:** `SeedSourceOfTruthTests.Every_Seed_Role_MustHave_At_Least_One_Mapping` selže, pokud krok 2 chybí.

---

## Policy konvence

Všechny policies mají formát `permission:{key}`, např. `permission:records.edit`.

Extension metoda `AddPermissionPolicies()` registruje při startu policy pro každý klíč z `PermissionKeys.AllDefinitions`:

```csharp
// Program.cs / ServiceCollectionExtensions
builder.Services.AddPermissionPolicies();
```

Handler `PermissionAuthorizationHandler` je registrován jako `IAuthorizationHandler` — přijme libovolný `PermissionRequirement` a vyřeší kontrolu přes `IAuthorizationService`.

---

## Jak funguje scope extraction v policy handleru

`PermissionAuthorizationHandler` čte `projektId` a `subsystemId` z `HttpContext.Request.RouteValues`:

```csharp
if (route.TryGetValue("projektId", out var p) && int.TryParse(p?.ToString(), out var pid))
    projektId = pid;
if (route.TryGetValue("subsystemId", out var s) && int.TryParse(s?.ToString(), out var sid))
    subsystemId = sid;
```

Pokud route **neobsahuje** `projektId` (např. `/nastaveni/reindex`, `/export/pdf` bez projektu), snapshot provede kontrolu na **globální úrovni** — grant musí existovat bez projektového kontextu.

---

## Residuální body-level checks

Některé akce jsou v `AuthorizationPolicyEnforcementTests` allowlistovány, protože standardní handler nestačí:

- **OR-composite logika** — akce vyžaduje jedno z více oprávnění (např. `ZaznamyController.Save` = `records.edit` OR `records.schedule.edit`).
- **`projektId` z form body** — `projektId` nepřichází v route, ale v těle příkazu (např. `DeleteRecordCommand.ProjektId`). Handler proto nemůže provést per-project check; kontrola se provádí manuálně v controlleru nebo service.

Viz komentáře přímo v `AuthorizationPolicyEnforcementTests.cs` (`PmTracker.Tests.Unit/Architecture/`).

---

## FAQ

**Proč neexistuje runtime UI pro vytváření rolí?**
Seed v gitu zajišťuje audit trail, four-eyes review v PR, reproducibilitu prostředí a guardrails při deployi. Runtime UI by umožnilo tiché drifty mezi prostředími bez záznamu v gitu.

**Jak zkontroluju, kdo má jaká práva?**
Nastavení → Efektivní práva. View používá `UserAuthorizationAuditSnapshot` (`Services/Settings/`), který agreguje všechny granty dané osoby včetně zdroje (globální role / projektová role / subsystémová role).

**Co s projektovými rolemi?**
Přiřazují se přes **Obsazení** (team management na projektu), ne přes Nastavení. Nastavení zobrazuje jen globální role (scope = `Global`). Projektové a subsystémové role spravuje vedoucí projektu v záložce Obsazení.
