# Authorization Unification — Design Spec

**Datum:** 2026-04-21
**Status:** Draft
**Vazba:** Inbox [#3](../plans/2026-04-20-upravy-inbox.md) — authorization audit
**Autor:** architectural review (2026-04-21) + implementation spec

## 1. Proč tento spec existuje

Aplikace PM Tracker má funkční autorizační systém, ale má **dvě oddělené cesty**, jak uživatel získá permission:

1. **Explicit grants (data-driven):** `AuthzUserRoles` → `AuthzRolePermissions` → `AuthzPermissions`. Editovatelné v UI (Nastavení). Pokrývá globální role (SUPERADMIN, APP_ADMIN).
2. **Implicit grants (code-driven):** `ProjectRolePermissionGrantBuilder` a `SubsystemRolePermissionGrantBuilder` **hardcoded v C#** mapují projektové/subsystémové role na pevné permission seznamy. Pokrývá `ADM_PROJ, PROJ_MAN, VEDOUCI_SUBSYSTEMU, ZASTUPCE_VEDOUCIHO_SUBSYSTEMU`.

Druhá cesta vznikla, když vývojář potřeboval automatické přiřazování permissions projektovým/subsystémovým rolím. V ten moment bylo jednodušší si napsat pár řádek kódu než rozšířit DB model — a ten kompromis zůstal.

### Důsledky bordelu

- **Dva zdroje pravdy.** Přidání nové projektové role znamená zásah do C#, ne jen DB.
- **Neviditelné granty.** UI Nastavení → Nastavení efektivních práv zobrazuje jen explicit granty. Projektový admin nemůže v Nastavení vidět/editovat, co přesně může `PROJ_MAN`.
- **Hardcoded `"SUPERADMIN"` v resolveru.** [UserContextResolver.cs:242](../../PmTracker.Web/Services/Security/UserContextResolver.cs#L242) porovnává role kód jako string — super-admin je jak bit flag v tabulce `AuthzSuperadmins`, tak "fallback" po role kódu. Duplicitní, matoucí.
- **Controller + Service duplicitní auth.** Většina write commands dělá permission check 2×: jednou v controlleru, jednou v service. Divergence = bug.
- **Bezpečnostní gapy:** `ExportController.Pdf` bez autorizace vůbec; comment actions v `ZaznamyController` mají `hasPermission: () => true` (delegace na service, ale bez propagace rozhodnutí zpět); `ProjectDashboardController` bypassuje permission systém přes `IsSuperAdmin`.
- **Inkonzistentní APP_ADMIN.** Nemá `projects.delete` ani `settings.manage` — bez zdokumentovaného důvodu. V praxi to znamená, že jedinou osobou schopnou smazat projekt nebo spravovat role je super-admin, což je provozně nepraktické.
- **Tři vzory pro project access check** (`CanAccessProject`, `EnsureProjectReadableAsync`, inline grant check) napříč různými controllery.

Cíl tohoto specu: **vrátit jediný jasný model**, který pokryje 95 % use-cases přes projektové a subsystémové role (user request), zachová super-admin jako privilegovaný flag, umožní občasný ad-hoc per-user grant, a odstraní duplikáty.

## 2. Zachovávané use-cases (z user input 2026-04-21)

- **Projektové role** (VLASTNIK_PROJEKTU, ADM_PROJ, PROJ_MAN, HOST, GEST) — 95 % běžného nasazení.
- **Subsystémové role** (VEDOUCI_SUBSYSTEMU, ZASTUPCE, METODIK_SUBSYSTEMU) — účast v subsystému projektu.
- **APP_ADMIN** — standardní role, „leze všude" (má globální záběr, CRUD na administrativních entitách).
- **SUPERADMIN** — speciální privilegovaný login, má navíc zámky číselníků (lock/unlock).
- **Ad-hoc grant** — občasné udělení specifické akce konkrétnímu uživateli v konkrétním projektu. Pokud by dělal problém, **uživatel je ochoten to obětovat** (low priority).

## 3. Best-practice model (jak by to dělala velká firma)

### 3.1 Principles

1. **Single source of truth pro permission mapping = databáze.** C# konstanty drží jen klíče (identifiery). Žádné implicit mapping v kódu.
2. **Role má scope** (`GLOBAL | PROJECT | SUBSYSTEM`). Scope určuje, kde a jak se role přiřazuje uživateli a kde se její permissions aplikují.
3. **Unified permission resolution.** Jediná komponenta (`UserContextResolver`) skládá všechny granty z DB z jednoho modelu. Žádné code builders.
4. **Explicit permission enforcement dvoustupňový:** `[Authorize(Policy=...)]` atribut na controlleru (primary gate, deny unauthenticated) + `IAuthorizationService.RequirePermissionAsync(...)` v service layer (secondary gate, scope-aware, defense-in-depth).
5. **Super-admin = výhradně DB flag.** `AuthzSuperadmins.OsobaId`. Žádný fallback přes role kód.
6. **Keep 14 existing permission keys + doplnit 5 chybějících** pro gaps (dashboard.view, export.*, comments.*, search.reindex).
7. **Project/subsystem role = první class AuthzRole se Scope=PROJECT/SUBSYSTEM.** Permission mapping sdílí stejnou tabulku `AuthzRolePermissions`. Přiřazení uživateli má per-role-scope kontext (ProjektId pro PROJECT role, ProjektSubsystemId pro SUBSYSTEM role).

### 3.2 Target data model

**Stávající tabulky, které zůstávají:** `AuthzPermissions`, `AuthzRolePermissions`, `AuthzRolePermissionProjects`, `AuthzSuperadmins`, `AuthzPermissionCategories`, `ObsazeniProjektu`, `ObsazeniSubsystemuProjektu`.

**Změny:**

```
AuthzRole (upravit):
  + Scope NVARCHAR(16) NOT NULL DEFAULT 'GLOBAL'
    CHECK (Scope IN ('GLOBAL','PROJECT','SUBSYSTEM'))

CiselnikRoliProjektu (upravit):
  + AuthzRoleId INT NULL FK → AuthzRoles(Id)
  Constraint: každý aktivní záznam musí mít AuthzRoleId NOT NULL
    (po data migraci)

CiselnikRoliSubsystemu (upravit):
  + AuthzRoleId INT NULL FK → AuthzRoles(Id)
  Constraint stejný jako výše
```

**Proč neudělat B-alternativu (jedna sjednocená `AuthzUserRoleAssignment` tabulka):**
- Existují tabulky `ObsazeniProjektu` a `ObsazeniSubsystemuProjektu` s history (DatumPrirazeni, DatumOdebrani) a řada dependent views/queries.
- Migrace na jednu tabulku = velká, riziková, s nutností přepsat team management API.
- Benefit je malý — existující model funguje pro use-cases, jen mu chybí propojení s permission systémem.
- **Kompromis:** nechat projektové/subsystémové role v jejich vlastních tabulkách, ale připojit každou přes FK na `AuthzRoles` pro permission mapping.

### 3.3 Resolver flow (cílový)

```
UserContextResolver.ResolveAsync(osobaId):

1. Osoba exists? → else Forbidden
2. IsSuperAdmin = await AuthzSuperadmins.AnyAsync(x => x.OsobaId == osobaId)
   [NO fallback přes role kód — smažeme řádky 240-243]

3. GlobalRoleIds = await AuthzUserRoles × AuthzRoles
   where IsActive AND Scope='GLOBAL'
   → list of AuthzRole.Id

4. ProjectRoleAssignments = await ObsazeniProjektu × CiselnikRoliProjektu
   where not-ended AND CiselnikRoliProjektu.AuthzRoleId IS NOT NULL
   → list of (AuthzRoleId, ProjektId)

5. SubsystemRoleAssignments = await ObsazeniSubsystemuProjektu × CiselnikRoliSubsystemu × ProjektSubsystemy
   where not-ended AND CiselnikRoliSubsystemu.AuthzRoleId IS NOT NULL
   → list of (AuthzRoleId, ProjektId)

6. AllRoleIds = GlobalRoleIds ∪ ProjectRoleAssignments.RoleIds ∪ SubsystemRoleAssignments.RoleIds

7. Explicit grants = await AuthzRolePermissions × AuthzPermissions × AuthzRolePermissionProjects
   where RoleId ∈ AllRoleIds AND Permission.IsActive
   → grouped to PermissionGrantViewModel list

8. Scope-awareness:
   - GLOBAL role grants → aplikují se všude (ALL scope)
   - PROJECT role grants → restrikce na ProjektId z ProjectRoleAssignments pro daný RoleId
   - SUBSYSTEM role grants → restrikce na ProjektId z SubsystemRoleAssignments pro daný RoleId
   (Pro PROJECT/SUBSYSTEM role se `ScopeMode='ALL'` interpretuje jako „ALL v rámci projektů, kde osoba má danou roli")

9. VisibleProjectIds = ProjectRoleAssignments.ProjectIds ∪ SubsystemRoleAssignments.ProjectIds
   (IsSuperAdmin vidí všechny → viditelnost se počítá fallback v CanAccessProject)

10. DeletedProjectIds = ProjectAuthorizationQueryHelper (beze změny)

11. Return CurrentUserContextViewModel s PermissionGrants obsahující Source metadata (SourceType, SourceRoleCode, SourceProjectId) pro UI audit trail.
```

**Smažeme:** `ProjectRolePermissionGrantBuilder`, `SubsystemRolePermissionGrantBuilder`. Permission mapping se migruje do `AuthzRolePermissions` jako seed.

### 3.4 Authorization service

```csharp
public interface IAuthorizationService
{
    bool HasPermission(string permissionKey, int? projektId = null);
    bool CanAccessProject(int projektId);
    Task RequirePermissionAsync(string permissionKey, int? projektId = null, CancellationToken ct = default);
    Task RequireProjectAccessAsync(int projektId, CancellationToken ct = default);
}
```

`HasPermission` a `CanAccessProject` jsou synchronní wrappery nad `CurrentUserContext` (pro kompatibilitu).
`RequirePermissionAsync` vyhazuje `UnauthorizedAccessException` / `PermissionDeniedException`, kterou globální filter vrací 403 s JSON payloadem.

Injectuje se do:
- **Controllerů** (namísto inline `CurrentUserContext.HasPermission(...)`) — pomáhá testovatelnosti a konzistenci.
- **Write services** — defense-in-depth. Pokud controller check selže kvůli chybě, service guard stále chrání.

### 3.5 Controller-level policies (ASP.NET Core)

Existujíci `[Authorize(Policy=...)]` framework. Registrujeme policies pro každý permission key:

```csharp
// Startup / Program.cs
options.AddPolicy(PermissionPolicies.RecordsEdit, p => p.Requirements.Add(new PermissionRequirement("records.edit")));
// ... pro všechny keys
```

```csharp
public sealed class PermissionRequirementHandler : AuthorizationHandler<PermissionRequirement>
{
    // Přečte CurrentUserContext, rozhodne HasPermission(key, projektId)
    // ProjektId čte z RouteValues ("projektId" nebo "id" podle konvence)
}
```

Atribut na controller action:
```csharp
[HttpPost]
[Authorize(Policy = PermissionPolicies.RecordsEdit)]
public async Task<IActionResult> Save(...) {
    // action body; service ještě udělá RequirePermissionAsync v write commandu
}
```

**Proč obojí (policy + service guard):** policy běží před action → ušetří SQL roundtrips pro zjevně zakázané requesty. Service guard je defensive — dokonce i nevědomá mistake v controlleru (vynechat atribut) nedostane permission past service layer.

### 3.6 Chybějící permission keys (gaps)

| Key | Popis | Scope |
|---|---|---|
| `dashboard.view` | Přístup na Project Dashboard | PROJECT |
| `export.pdf` | PDF export projekt/jednání/úkol | PROJECT |
| `export.word` | Word export | PROJECT |
| `comments.add` | Přidat vyjádření k záznamu | PROJECT |
| `comments.edit.own` | Upravit vlastní vyjádření | PROJECT |
| `comments.delete.own` | Smazat vlastní vyjádření | PROJECT |
| `search.reindex` | Manuální reindex vyhledávání | GLOBAL |

`records.comment.subsystemlead` zůstává jako specific grant pro subsystem lead (doplněk k `comments.add`).

### 3.7 Role → permission mapping (cílový seed)

**SUPERADMIN** (GLOBAL): všechna práva, všechny scope ALL.

**APP_ADMIN** (GLOBAL): všechna práva kromě — rozhodně se musí prodiskutovat s uživatelem. **Současný stav** vyjmul `projects.delete` a `settings.manage`. Můj **návrh default**: APP_ADMIN dostane všechno včetně `settings.manage` (jinak je nepraktický), ale `projects.delete` zůstává jen u SUPERADMINa (destruktivní operace, dobrá obrana v depth). Doplnit `search.reindex` pro APP_ADMIN.

**VLASTNIK_PROJEKTU** (PROJECT): `projects.edit`, `records.edit`, `records.schedule.*`, `meetings.create`, `meetings.edit`, `team.manage`, `dashboard.view`, `export.*`, `comments.add`, `comments.edit.own`, `comments.delete.own`. Plný majitel.

**ADM_PROJ** (PROJECT): stejné jako VLASTNIK_PROJEKTU kromě `projects.edit`. Silný projekt admin bez práva měnit metadata projektu.

**PROJ_MAN** (PROJECT): `records.edit`, `records.schedule.*`, `meetings.create`, `meetings.edit`, `team.manage`, `dashboard.view`, `export.*`, `comments.add`, `comments.edit.own`, `comments.delete.own`. Projekt manažer bez práva editovat metadata projektu.

**HOST** (PROJECT): `dashboard.view`, `export.pdf`, `export.word`. Read-only přístup k projektu.

**GEST** (PROJECT): jako HOST + `comments.add`, `comments.edit.own`, `comments.delete.own`. Gestor může komentovat.

**VEDOUCI_SUBSYSTEMU** (SUBSYSTEM): `records.comment.subsystemlead`, `comments.add`, `comments.edit.own`, `comments.delete.own`, `dashboard.view`, `export.pdf`. Vedoucí subsystému.

**ZASTUPCE_VEDOUCIHO_SUBSYSTEMU** (SUBSYSTEM): stejné jako VEDOUCI_SUBSYSTEMU.

**METODIK_SUBSYSTEMU** (SUBSYSTEM): `comments.add`, `comments.edit.own`, `comments.delete.own`, `dashboard.view`, `export.pdf`. Nižší než vedoucí, ale má komentovací práva.

### 3.8 Super-admin speciální privilegium — zámky číselníků

Číselníky (`CiselnikyController.SaveRow` / `DeleteRow`) mají policy přes `DictionarySecurityPolicy.CanModifyRow(IsLocked, IsSuperAdmin)`. Locked rows může editovat jen super-admin.

**Ponecháváme** — je to semantika „super-admin je jediný, kdo může modifikovat globálně zamčená referenční data". Není to permission, je to role-gated feature.

### 3.9 Ad-hoc per-user grant

Zachováváme existující `AuthzRolePermissionProjects` + `AuthzUserRoles` infrastruktura:
- Admin v Nastavení vytvoří custom role s `Scope=GLOBAL` (např. „CUSTOM_PROJ123_DOC_EDIT")
- Přiřadí jí jediné permission (např. `records.edit`) se `ScopeMode=INCLUDE` a `ProjectIds=[123]`
- Přiřadí tu roli konkrétnímu uživateli
- Effekt: user dostává `records.edit` jen pro projekt 123

**User ví, že tohle je workaround pro 5 % use-cases.** Pokud bude dělat maintenance problém (rozplizlé custom role), akceptujeme odstranění této možnosti (explicit deny všech ne-system rolí kromě projektových/subsystémových definovaných v číselnících).

## 4. Design decisions v detailu

### 4.1 Proč ne jedna velká `AuthzUserRoleAssignment` tabulka?

Kompromis pro zachování:
- Existujících API (`/Projekty/AssignProjectRole`, `/Projekty/DeactivateProjectRole` atd.)
- Existujících queries v `UserContextResolver`, `JednaniController`, `SettingsService`
- Historie (`DatumPrirazeni`, `DatumOdebrani`) na `ObsazeniProjektu` a `ObsazeniSubsystemuProjektu`

Volba: zachovat 3 assignment tabulky (`AuthzUserRoles`, `ObsazeniProjektu`, `ObsazeniSubsystemuProjektu`), propojit role přes FK na `AuthzRoles`.

### 4.2 Proč `AuthzRole.Scope`?

Je to discriminator. UI Nastavení → Role editor může filtrovat permissions podle scope (PROJECT role nemůže mít `settings.manage`, která je GLOBAL). Rezolvér může snadno rozhodnout, z kterých assignment tabulek načíst role podle scope. Self-documenting.

### 4.3 Proč explicit deny (IsAllowed=false) zachováváme?

Model ho podporuje, ale seed ho nepoužívá. Zachováváme schéma pro budoucí případy („tato role všechno kromě X") — stálé pro flexibilitu, nulový runtime cost.

### 4.4 Backward compatibility

- Existující data v `ObsazeniProjektu` a `ObsazeniSubsystemuProjektu` zůstávají — nová FK `AuthzRoleId` se naplní v migraci dle `Kod` match.
- Existující `AuthzUserRoles` pro SUPERADMIN/APP_ADMIN zůstávají beze změny.
- `ProjectRolePermissionGrantBuilder` + `SubsystemRolePermissionGrantBuilder` se po migraci smažou — ale až **poté**, co rezolvér už čte z DB (za běhu paralelní run pro verifikaci).

## 5. Risks + mitigations

| Risk | Mitigation |
|---|---|
| Data migrace naplní AuthzRoleId špatně → někdo ztratí přístup | Migrace dělaná jako **additive** (přidá FK), verifikovaná dotazem před smazáním builderů. Feature flag, ať se dá rychle vrátit. |
| Performance regression (víc JOINů v resolveru) | Resolvér už dělá 6-7 queries. Nový model přidá 1-2. Mírné zhoršení acceptable. Možno cachovat per-request. |
| Controller-level Policy nedostane ProjektId z URL (chybí v route) | PermissionRequirementHandler má fallback na route values ("projektId", "id"). Action, které nemají projektId, používají null (GLOBAL permission check). |
| Service-level `RequirePermissionAsync` vyvolá exception tam, kde controller check OK → broken flow | Fáze D zavedena po Fázi A+B+C. Integration testy ověří každý write flow. |
| APP_ADMIN změna seedu (přidat `settings.manage`) rozbije produkci | Seed je idempotent — pokud v DB už je custom APP_ADMIN bez `settings.manage`, seed nepřepíše. Rozhodnutí, jestli dopracovat existing instance, zůstává na ops. |

## 6. Nechat vs. odstranit

**Nechat (ošetřeno / zamýšlené):**
- `DictionarySecurityPolicy.CanModifyRow` — zamykání řádků pro super-admina
- `AuthzSuperadmins` jako separátní tabulka (not merged do AuthzUserRoles)
- `ObsazeniProjektu` / `ObsazeniSubsystemuProjektu` jako historie přiřazení

**Odstranit:**
- `ProjectRolePermissionGrantBuilder` + unit testy
- `SubsystemRolePermissionGrantBuilder` + unit testy
- Hardcoded `"SUPERADMIN"` check v `UserContextResolver.cs:242`
- `hasPermission: () => true` v comment actions
- Tři různé vzory project access — jednotit na `IAuthorizationService.CanAccessProject()`

## 7. Metriky úspěchu

- 0× hardcoded role code mapping v C# (grep test)
- 100% permission keys má alespoň jeden mapping v seed (DB query test)
- 100% write controller actions má buď `[Authorize(Policy=...)]` nebo `IAuthorizationService.RequirePermissionAsync` v action body (architecture test)
- 100% write services má `IAuthorizationService.RequirePermissionAsync` guard (architecture test)
- Žádný `CurrentUserContext.IsSuperAdmin` bypass v actions kromě `CiselnikyController.SaveRow/DeleteRow` (locked rows) a `ProfilController` (vlastní profil — ale ani tam není potřeba)

## 8. Implementační plán

Viz [2026-04-21-authorization-unification.md](../plans/2026-04-21-authorization-unification.md). Rozdělen na 6 fází (A–F), každá samostatně reviewovatelná a rollback-safe. Celkový odhad 4–6 dní full-time práce.
