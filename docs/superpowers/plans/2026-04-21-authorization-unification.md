# Authorization Unification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sjednotit dvě oddělené autorizační cesty (explicit DB grants + implicit code-driven builders) do **jediného DB-driven modelu**. Každá role — globální, projektová, subsystémová — má permissions uložené v `AuthzRolePermissions`; resolvér skládá výsledek z jednoho queries. Zavést `IAuthorizationService` s defense-in-depth service-layer guardem. Doplnit chybějící permission keys. Zachovat zpětnou kompatibilitu a všechny existující use-cases.

**Architecture:** Zachováváme `ObsazeniProjektu` / `ObsazeniSubsystemuProjektu` jako historii přiřazení, přidáváme FK `AuthzRoleId` na `CiselnikRoliProjektu` a `CiselnikRoliSubsystemu` (propojení na sjednocený permission katalog). Přidáváme `AuthzRole.Scope` (GLOBAL/PROJECT/SUBSYSTEM) jako discriminator. Rezolvér čte všechny granty z jednoho permission modelu přes FK → builder classes se smažou po přechodu. Authorization service `IAuthorizationService` jako kanonická fasáda (Policy handler + service-layer guard).

**Tech Stack:** ASP.NET Core 8, EF Core 8, xUnit + FluentAssertions, SQL Server.

**Vazba na spec:** [docs/superpowers/specs/2026-04-21-authorization-unification-design.md](../specs/2026-04-21-authorization-unification-design.md).

**Odhad:** 6 fází × 5–12 tasků = ~55 tasků, 4–6 dní full-time.

**Pořadí fází:** A → B → C → D → E → F. Každá fáze je samostatně merge-safe (aplikace po každé fázi stále funguje).

---

## Fáze A — Infrastructure (additive, non-breaking)

Rozšíříme schéma, ale existující kód tyto sloupce ještě nevyužívá. Resolvér stále používá builder classes.

### Task A1: EF Core migrace — `AuthzRole.Scope`

**Files:**
- Modify: [PmTracker.Web/Models/Entities/PmTrackerEntities.cs](../../PmTracker.Web/Models/Entities/PmTrackerEntities.cs) — přidat `Scope` property na entity class pro `AuthzRoles` tabulku
- Modify: `PmTracker.Web/Data/PmTrackerDbContext.cs` — konfigurace sloupce (CHECK constraint, default GLOBAL)
- New: `PmTracker.Web/Migrations/SqlUpgrades/1_2_0_AddRoleScope.sql` (nebo ekvivalent dle existujícího migračního patternu)
- Test: `PmTracker.Tests.Unit/Authorization/AuthzRoleScopeTests.cs`

**Steps:**
- [ ] Přidat failing test: `AuthzRole_ShouldHaveScopeColumn` — entity má property `Scope` typu string
- [ ] Přidat failing test: `AuthzRole_Scope_ShouldDefaultToGlobal` — při new instance default `GLOBAL`
- [ ] Přidat Scope property do entity + konfigurace v DbContext
- [ ] Napsat SQL migration skript (ALTER TABLE + UPDATE existing rows = 'GLOBAL' + CHECK constraint)
- [ ] Ověřit: build + unit testy
- [ ] Commit: `feat(authz): AuthzRole.Scope (GLOBAL|PROJECT|SUBSYSTEM) + migrace`

### Task A2: EF Core migrace — `CiselnikRoliProjektu.AuthzRoleId`

**Files:**
- Modify: `PmTracker.Web/Models/Entities/PmTrackerEntities.cs` — CiselnikRoliProjektuEntity
- Modify: `PmTracker.Web/Data/PmTrackerDbContext.cs`
- New: `PmTracker.Web/Migrations/SqlUpgrades/1_2_1_AddProjectRoleAuthzFk.sql`
- Test: `PmTracker.Tests.Unit/Authorization/ProjectRoleAuthzLinkTests.cs`

**Steps:**
- [ ] Failing test: `CiselnikRoliProjektu_ShouldHaveAuthzRoleIdColumn`
- [ ] Přidat `AuthzRoleId INT NULL` property na entity + FK konfigurace
- [ ] SQL migration (nullable FK pro teď; enforce NOT NULL až po Fázi A5)
- [ ] Ověřit build + testy
- [ ] Commit: `feat(authz): CiselnikRoliProjektu.AuthzRoleId FK nullable`

### Task A3: EF Core migrace — `CiselnikRoliSubsystemu.AuthzRoleId`

Analogicky k A2 pro subsystémovou tabulku.

**Steps:**
- [ ] Failing test: `CiselnikRoliSubsystemu_ShouldHaveAuthzRoleIdColumn`
- [ ] Přidat property + konfigurace
- [ ] SQL migration
- [ ] Commit: `feat(authz): CiselnikRoliSubsystemu.AuthzRoleId FK nullable`

### Task A4: Seed — vytvořit AuthzRole záznamy pro projektové/subsystémové role

**Files:**
- Modify: [PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs](../../PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs)
- Modify: [PmTracker.Web/Services/Security/PermissionSeeder.cs](../../PmTracker.Web/Services/Security/PermissionSeeder.cs)
- Test: `PmTracker.Tests.Unit/Authorization/ProjectRoleSeedTests.cs`

**Steps:**
- [ ] Failing test: `PermissionSeedConfiguration_ShouldSeedProjectScopeRoles` — očekává role `VLASTNIK_PROJEKTU, ADM_PROJ, PROJ_MAN, HOST, GEST` se `Scope=PROJECT`
- [ ] Failing test: `PermissionSeedConfiguration_ShouldSeedSubsystemScopeRoles` — očekává `VEDOUCI_SUBSYSTEMU, ZASTUPCE_VEDOUCIHO_SUBSYSTEMU, METODIK_SUBSYSTEMU` se `Scope=SUBSYSTEM`
- [ ] Rozšířit `RoleSeedItem` record o `Scope` property
- [ ] Doplnit `Roles` seznam o projektové a subsystémové role (IsSystem=true, Scope=PROJECT/SUBSYSTEM)
- [ ] PermissionSeeder vytvoří AuthzRole záznamy s novým Scope polem
- [ ] Ověřit: seed vytvoří správné záznamy v DB (integration test)
- [ ] Commit: `feat(authz): seed AuthzRole pro projektové/subsystémové role`

### Task A5: Data migrace — propojit CiselnikRoliProjektu/Subsystemu s AuthzRole přes Kod match

**Files:**
- New: `PmTracker.Web/Services/Security/RoleCatalogLinker.cs` (nebo součást PermissionSeeder)
- Test: `PmTracker.Tests.Unit/Authorization/RoleCatalogLinkerTests.cs`

**Steps:**
- [ ] Failing test: `RoleCatalogLinker_ShouldLinkProjektRolesByKod` — pro každou row v CiselnikRoliProjektu s matching Kod v AuthzRoles nastaví AuthzRoleId
- [ ] Failing test: `RoleCatalogLinker_ShouldLinkSubsystemRolesByKod`
- [ ] Napsat linker jako post-seed step (volá se z PermissionSeeder)
- [ ] Ověřit na integration DB, že všechny existující rows dostaly FK
- [ ] Přidat SQL NOT NULL constraint (jen pokud test prošel — bezpečné)
- [ ] Commit: `feat(authz): data migrace — propojit CiselnikRoli* s AuthzRole přes Kod`

### Task A6: Seed permission mappingu pro projektové/subsystémové role

**Files:**
- Modify: `PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs`
- Test: `PmTracker.Tests.Unit/Authorization/RolePermissionSeedCoverageTests.cs`

**Steps:**
- [ ] Failing test: `VLASTNIK_PROJEKTU_ShouldHave10Permissions` — verify complete list dle specu sekce 3.7
- [ ] Failing test: `ADM_PROJ_ShouldHave9Permissions` (bez projects.edit)
- [ ] Failing test: `PROJ_MAN_ShouldHave9Permissions`
- [ ] Failing test: `HOST_ShouldHaveReadOnlyPermissions` (dashboard.view, export.*)
- [ ] Failing test: `GEST_ShouldHaveReadOnlyPlusComments`
- [ ] Failing test: `VEDOUCI_SUBSYSTEMU_ShouldHaveSubsystemLeadPermissions`
- [ ] Failing test: `ZASTUPCE_VEDOUCIHO_SUBSYSTEMU_ShouldMatchVedouci`
- [ ] Failing test: `METODIK_SUBSYSTEMU_ShouldHaveSubsystemMemberPermissions`
- [ ] Doplnit RoleMappings seznam v seedu
- [ ] Ověřit: seed naplní `AuthzRolePermissions` správně
- [ ] Commit: `feat(authz): seed permission mappingů projektových/subsystémových rolí`

**Fáze A checkpoint:** DB má Scope + FK sloupce, seed naplnil AuthzRole pro všechny role codes, DB propojení funguje. Aplikace stále používá `ProjectRolePermissionGrantBuilder` a `SubsystemRolePermissionGrantBuilder` (ještě nezrušené). **Aplikace funguje beze změny chování.**

---

## Fáze B — Resolver swap

Resolvér začne číst implicit granty z DB přes nový FK. Stará builder infrastructure se smaže.

### Task B1: Resolver načítá project role grants z DB

**Files:**
- Modify: [PmTracker.Web/Services/Security/UserContextResolver.cs](../../PmTracker.Web/Services/Security/UserContextResolver.cs)
- Test: `PmTracker.Tests.Unit/Authorization/UserContextResolverProjectRoleTests.cs`

**Steps:**
- [ ] Failing test: `Resolver_ProjektRoleVlastnikProjektu_ShouldGetFullProjectPermissions` — vyseedovat user s VLASTNIK_PROJEKTU role na projekt 42, resolver vrátí grants pro `records.edit, meetings.*, team.manage, dashboard.view, export.*, comments.*` omezené na projekt 42
- [ ] Failing test: `Resolver_ProjektRoleWithoutAuthzRoleId_ShouldNotGrant` — pokud row nemá FK, nic negrantuje (tichá migrace safety)
- [ ] Přepsat resolvér: `ProjectRoleAssignments` query joinuje `CiselnikRoliProjektu` → `AuthzRoles` → `AuthzRolePermissions` → `AuthzPermissions`, vrací `PermissionGrantViewModel` list s SourceType=PROJECT_ROLE
- [ ] Scope aware: PROJECT role grants dostanou `ScopeMode=INCLUDE` s konkretním `ProjectIds=[projektId]`
- [ ] Ověřit testy zelené
- [ ] Commit: `refactor(authz): resolver čte projektové role grants z DB přes AuthzRoleId`

### Task B2: Resolver načítá subsystem role grants z DB

Analogicky k B1 pro subsystémové role.

**Steps:**
- [ ] Failing test: `Resolver_SubsystemRoleVedouci_ShouldGetComment+SubsystemLeadPermissions`
- [ ] Přepsat `SubsystemRoleAssignments` query
- [ ] Testy zelené
- [ ] Commit: `refactor(authz): resolver čte subsystem role grants z DB přes AuthzRoleId`

### Task B3: Odstranit ProjectRolePermissionGrantBuilder + SubsystemRolePermissionGrantBuilder

**Files:**
- Delete: [PmTracker.Web/Services/Common/ProjectRolePermissionGrantBuilder.cs](../../PmTracker.Web/Services/Common/ProjectRolePermissionGrantBuilder.cs)
- Delete: [PmTracker.Web/Services/Common/SubsystemRolePermissionGrantBuilder.cs](../../PmTracker.Web/Services/Common/SubsystemRolePermissionGrantBuilder.cs)
- Delete: related unit tests
- Modify: [PmTracker.Web/Services/Settings/UserAuthorizationSnapshotBuilder.cs](../../PmTracker.Web/Services/Settings/UserAuthorizationSnapshotBuilder.cs) — použít stejný DB-based path jako resolver

**Steps:**
- [ ] Ověřit žádné další callery: `grep -rn "ProjectRolePermissionGrantBuilder\|SubsystemRolePermissionGrantBuilder"`
- [ ] Smazat classes + testy
- [ ] `UserAuthorizationSnapshotBuilder` přepsat stejným patternem jako Resolver (JOIN přes FK)
- [ ] Build + testy
- [ ] Commit: `refactor(authz): smazat builder classes — mapping v DB`

### Task B4: Smazat hardcoded "SUPERADMIN" check v resolveru

**Files:**
- Modify: [UserContextResolver.cs:239-243](../../PmTracker.Web/Services/Security/UserContextResolver.cs#L239-L243)
- Test: `PmTracker.Tests.Unit/Authorization/SuperAdminBitFlagTests.cs`

**Steps:**
- [ ] Failing test: `Resolver_UserWithSuperAdminRoleButNotInAuthzSuperadmins_ShouldNotBeSuperAdmin` — user v tabulce AuthzUserRoles s role.Kod="SUPERADMIN", ale NENÍ v AuthzSuperadmins → `IsSuperAdmin=false`
- [ ] Failing test: `Resolver_UserInAuthzSuperadminsOnly_ShouldBeSuperAdmin`
- [ ] Smazat řádky 239-243 (zachovat jen `isSuperAdmin = osoba.IsSuperAdmin;` ze řádku 211 AuthzSuperadmins check)
- [ ] Ověřit integration test (seed SUPERADMIN user bez AuthzSuperadmins row → nemá super-admin práva)
- [ ] Commit: `fix(authz): super-admin pouze z AuthzSuperadmins (smazán role code fallback)`

**Fáze B checkpoint:** Všechny implicit granty proudí z DB. Builder classes pryč. SuperAdmin bit flag jediný zdroj. **Aplikace funguje identicky jako předtím (testováno přes před/po comparison).**

---

## Fáze C — Gap fixes: chybějící permissions + bezpečnostní díry

### Task C1: Přidat nové permission keys

**Files:**
- Modify: [PmTracker.Web/Models/ViewModels/SecurityViewModels.cs](../../PmTracker.Web/Models/ViewModels/SecurityViewModels.cs) — PermissionKeys constants
- Modify: `PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs`

**Steps:**
- [ ] Failing test: `PermissionKeys_ShouldContainNewKeys` — `dashboard.view, export.pdf, export.word, comments.add, comments.edit.own, comments.delete.own, search.reindex`
- [ ] Přidat constants
- [ ] Přidat do Actions seed seznam (category: RECORDS pro comments, PROJECTS pro dashboard + export, SETTINGS pro search.reindex)
- [ ] Přidat mappings dle specu 3.7 (SUPERADMIN + APP_ADMIN all, role-specific dle tabulky)
- [ ] Testy zelené
- [ ] Commit: `feat(authz): nové permission keys (dashboard, export, comments, search.reindex)`

### Task C2: ExportController — ochránit Pdf action

**Files:**
- Modify: [PmTracker.Web/Controllers/ExportController.cs](../../PmTracker.Web/Controllers/ExportController.cs)
- Test: `PmTracker.Tests.Unit/Authorization/ExportControllerAuthTests.cs`

**Steps:**
- [ ] Failing test: `Pdf_WithoutPermission_ShouldReturnForbid`
- [ ] Failing test: `Pdf_WithExportPdfPermission_ShouldAllow`
- [ ] Přidat permission check (zatím inline `CurrentUserContext.HasPermission(PermissionKeys.ExportPdf, projektId)`)
- [ ] Commit: `fix(authz): Pdf export — ochranu permission key`

### Task C3: ProjectDashboardController — přepnout na `dashboard.view` permission

**Files:**
- Modify: `PmTracker.Web/Controllers/ProjectDashboardController.cs`
- Test: `PmTracker.Tests.Unit/Authorization/ProjectDashboardAuthTests.cs`

**Steps:**
- [ ] Failing test: `Index_WithDashboardViewPermission_ShouldAllow_NonSuperAdmin` — user s PROJ_MAN role (který v Fázi A6 dostal dashboard.view) může vidět dashboard
- [ ] Failing test: `Index_WithoutPermission_ShouldForbid`
- [ ] Nahradit `if (!IsSuperAdmin) return Forbid()` s `if (!HasPermission(dashboard.view, projektId)) return Forbid()`
- [ ] Commit: `fix(authz): ProjectDashboard — permission key místo IsSuperAdmin bypass`

### Task C4: ZaznamyController comments — nahradit `hasPermission: () => true`

**Files:**
- Modify: [PmTracker.Web/Controllers/ZaznamyController.Commands.cs](../../PmTracker.Web/Controllers/ZaznamyController.Commands.cs)
- Test: `PmTracker.Tests.Unit/Authorization/CommentAuthorizationTests.cs`

**Steps:**
- [ ] Failing test: `AddComment_WithoutCommentsAddPermission_ShouldReturnForbid`
- [ ] Failing test: `UpdateComment_OwnerWithEditOwnPermission_ShouldAllow`
- [ ] Failing test: `DeleteComment_NonOwnerWithoutAdmin_ShouldForbid`
- [ ] Přepsat: `hasPermission: () => HasPermission(PermissionKeys.CommentsAdd, projektId)` atd.
- [ ] Zachovat existující service-layer CommentAuthorizationPolicy jako second guard (Fáze D)
- [ ] Commit: `fix(authz): comment actions — explicit permission checks v controlleru`

### Task C5: SearchController.Reindex — permission místo IsSuperAdmin

**Files:**
- Modify: [PmTracker.Web/Controllers/SearchController.cs](../../PmTracker.Web/Controllers/SearchController.cs)

**Steps:**
- [ ] Failing test: `Reindex_AppAdmin_ShouldAllow` (po Fázi C1 má APP_ADMIN `search.reindex`)
- [ ] Přepsat na `HasPermission(PermissionKeys.SearchReindex)`
- [ ] Commit: `fix(authz): Search.Reindex — permission místo IsSuperAdmin`

**Fáze C checkpoint:** Hlavní bezpečnostní gapy zavřené. Permission systém je kompletní ve smyslu pokrytí.

---

## Fáze D — `IAuthorizationService` + defense-in-depth

### Task D1: Vytvořit `IAuthorizationService` interface + implementaci

**Files:**
- New: `PmTracker.Web/Services/Security/IAuthorizationService.cs`
- New: `PmTracker.Web/Services/Security/AuthorizationService.cs`
- Test: `PmTracker.Tests.Unit/Authorization/AuthorizationServiceTests.cs`

**Steps:**
- [ ] Failing test: `HasPermission_DelegatesToCurrentUserContext`
- [ ] Failing test: `RequirePermissionAsync_WhenDenied_Throws`
- [ ] Failing test: `CanAccessProject_DelegatesToCurrentUserContext`
- [ ] Implementace (thin wrapper kolem ICurrentUserContextAccessor)
- [ ] DI registrace: Scoped
- [ ] Commit: `feat(authz): IAuthorizationService + implementation`

### Task D2: Controller policy handler

**Files:**
- New: `PmTracker.Web/Services/Security/PermissionRequirement.cs`
- New: `PmTracker.Web/Services/Security/PermissionRequirementHandler.cs`
- New: `PmTracker.Web/Services/Security/PermissionPolicies.cs` (konstanty)
- Modify: `PmTracker.Web/Program.cs` — registrace policies pro všechny PermissionKeys

**Steps:**
- [ ] Failing test: `PermissionRequirementHandler_ReadsProjektIdFromRoute`
- [ ] Failing test: `PermissionRequirementHandler_AllowsWhenHasPermission`
- [ ] Failing test: `PermissionRequirementHandler_DeniesWhenMissing`
- [ ] Implementace handleru (ASP.NET Core IAuthorizationHandler)
- [ ] Registrace policies (1 policy per permission key)
- [ ] Commit: `feat(authz): policy-based authorization framework`

### Task D3: Service-layer guard — refactor top 5 nejdůležitějších write services

**Files:**
- Modify: 5 write services (ProjektService, MeetingService, RecordService, PeopleService, SettingsService)
- Test: integration test per service

**Steps:**
- [ ] Failing test pro každou service: `SaveX_WithoutPermission_ThrowsPermissionDenied`
- [ ] Injekt `IAuthorizationService` do services
- [ ] Přidat `await _authz.RequirePermissionAsync(PermissionKeys.X, projektId);` na začátek každého write command
- [ ] Zachovat existující checks v controlleru (pro teď — odstraníme v D5 po verify)
- [ ] Commit: `feat(authz): service-layer guards pro top 5 write services`

### Task D4: Migrovat 5 controllerů na `[Authorize(Policy=...)]` atributy

**Files:**
- Modify: 5 controllerů (ProjektyController, JednaniController, ZaznamyController, OsobyController, NastaveniController)

**Steps:**
- [ ] Per controller: nahradit inline `HasPermission(...)` check s `[Authorize(Policy=PermissionPolicies.X)]` atributem (kde to jde — scope přes route je jednoduchý)
- [ ] Kde potřebuje project-scope context, zachovat inline check přes `IAuthorizationService.CanAccessProject()` nebo `RequirePermissionAsync(..., projektId)` v action body
- [ ] Zjednodušit `ExecuteXValidatedActionAsync` helpery (volat IAuthorizationService)
- [ ] Commit: `refactor(authz): migrate 5 controllers to policy-based authorization`

### Task D5: Odstranit duplicitní controller checks kde existuje service-guard

**Files:**
- Modify: stejné controllery jako D4

**Steps:**
- [ ] Identifikovat: pro každou action, kde je **oba** controller-level check a service-level guard, smazat controller-level check
- [ ] Primární gate = ASP.NET Core policy ([Authorize]). Scope gate = service guard. Duplicita pryč.
- [ ] Integration testy ověří, že stále blokují unauthorized requests
- [ ] Commit: `refactor(authz): dedupe controller+service checks — ponechat service guard`

### Task D6: Zbytek controllerů (Ciselniky, Export, Profil, Projekty.Dashboard, Search)

**Steps:**
- [ ] Analogicky per controller podle vzoru D4+D5
- [ ] Commit per controller

**Fáze D checkpoint:** Všechny controller actions mají buď `[Authorize(Policy=X)]` nebo explicit `_authz.RequirePermissionAsync`. Všechny write services mají guard. Duplicitní checks odstraněny.

---

## Fáze E — APP_ADMIN review + seed finalizace

### Task E1: Přidat `settings.manage` do APP_ADMIN seedu

**Steps:**
- [ ] Rozhodnutí: podle specu APP_ADMIN dostane `settings.manage` (jinak je nepraktický — nikdo jiný než SUPERADMIN nemůže editovat role)
- [ ] Upravit `PermissionSeedConfiguration.RoleMappings` (řádky 60-71)
- [ ] Test: `AppAdmin_ShouldHaveSettingsManage`
- [ ] Commit: `feat(authz): APP_ADMIN dostane settings.manage`

### Task E2: Rozhodnout `projects.delete` pro APP_ADMIN

**Steps:**
- [ ] **User decision point:** ponechat jen SUPERADMINa (current), nebo rozšířit na APP_ADMIN?
- [ ] Spec doporučuje ponechat defense-in-depth (jen SUPERADMIN), ale s dokumentací
- [ ] Commit: (podle rozhodnutí) `docs(authz): APP_ADMIN projects.delete policy` nebo `feat(authz): APP_ADMIN projects.delete grant`

### Task E3: Kompletní `search.reindex` distribuce

**Steps:**
- [ ] Ověřit, že permission key z C1 je správně mapován pro SUPERADMIN a APP_ADMIN
- [ ] Integration test: APP_ADMIN může triggrnout `/Search/Reindex`

---

## Fáze F — UI Nastavení + Audit

### Task F1: Role editor — přidat Scope dropdown

**Files:**
- Modify: `PmTracker.Web/Views/Nastaveni/RoleModal.cshtml`
- Modify: `PmTracker.Web/Models/ViewModels/NastaveniViewModels.cs` — AuthzRoleModalViewModel
- Modify: `PmTracker.Web/Services/Settings/SettingsService.cs`

**Steps:**
- [ ] Failing test: `RoleModal_ShouldContainScopeDropdown`
- [ ] Přidat Scope dropdown (GLOBAL | PROJECT | SUBSYSTEM) do modalu
- [ ] Service ukládá Scope při Create/Edit role
- [ ] Commit: `feat(authz): UI — Scope dropdown v role editoru`

### Task F2: Permission picker filtr dle scope

**Steps:**
- [ ] Failing test: `RolePermissionModal_ProjectRole_ShouldOfferOnlyProjectAndGlobalPermissions`
- [ ] RolePermissionModal filtruje dostupné permissions dle Scope vybrané role
- [ ] PROJECT role může dostat PROJECT/GLOBAL permissions, ale ne SUBSYSTEM-specific (pokud nějaké přibudou)
- [ ] Commit: `feat(authz): filtr permissions dle scope v UI`

### Task F3: Effective rights preview — enriched source info

**Files:**
- Modify: Nastavení → Efektivní práva (SectionKey="efektivni-prava")
- Modify: `EffectivePermissionRowViewModel` — doplnit SourceType, SourceRoleCode, SourceProjectName

**Steps:**
- [ ] Failing test: `EffectivePermissions_ShouldExposeSource`
- [ ] UI ukazuje, odkud permission pochází (app-level role vs. projektová role + projekt)
- [ ] Commit: `feat(authz): UI — source trail v efektivních právech`

### Task F4: Architecture tests — anti-regression

**Files:**
- New: `PmTracker.Tests.Unit/Authorization/AuthorizationArchitectureTests.cs`

**Steps:**
- [ ] `NoHardcodedRoleCodesInServices` — grep všechny .cs v Services, řádky obsahující string "SUPERADMIN"/"APP_ADMIN"/"PROJ_MAN" atd. (kromě role-code konstant a seed configu)
- [ ] `AllPermissionKeysHaveSeededMapping` — každý PermissionKeys konstanta má alespoň jedno mapping v seedu
- [ ] `AllWriteControllerActionsHavePolicyOrServiceGuard` — reflection nad controllery: každá [HttpPost]/[HttpPut]/[HttpDelete] action má buď `[Authorize(Policy=...)]` nebo inline `IAuthorizationService` volání
- [ ] `AllWriteServicesHaveAuthzGuard` — reflection nad services: každá `SaveX`, `CreateX`, `DeleteX`, `UpdateX`, `ApproveX` metoda má IAuthorizationService volání
- [ ] Commit: `test(authz): architecture guardrails proti regrese`

### Task F5: Documentation + user guide update

**Files:**
- Modify: [docs/technical/07-security-authz.md](../../docs/technical/07-security-authz.md)
- Modify: `docs/user-guide.md` — sekce „Role a oprávnění"

**Steps:**
- [ ] Popsat nový model (Scope, propojení přes FK, IAuthorizationService)
- [ ] Update user-guide: „jak vytvořit vlastní projektovou roli v Nastavení"
- [ ] Commit: `docs(authz): update security dokumentace + user guide`

---

## Rollback & risk strategie

Každá fáze je merge-safe — aplikace funguje po každé. Specificky:

- **Fáze A je ryze additive** (nové sloupce nullable, builders stále aktivní). Rollback = revert migrací.
- **Fáze B přepíná resolver.** Rollback = git revert (DB změny zůstávají, funkčně neutrální).
- **Fáze C doplňuje permissions.** Rollback = git revert, permission keys zůstávají v DB ale nejsou použity.
- **Fáze D zavádí `IAuthorizationService`.** Rollback per-controller = git revert individual commits (D4+D5 jsou per-controller).
- **Fáze E jsou seed změny.** Idempotent, nepřepisuje custom data.
- **Fáze F je UI only.** Rollback bez vlivu na funkci.

## Definition of Done

- Všech ~55 tasků checked-off
- 100% unit testů zelených
- Integration test suite passed
- Architecture tests (F4) zelené
- Manual QA (2026-04-2X): super-admin, app-admin, project manager, subsystem lead, host — každý projde svým flow
- Dokumentace aktualizovaná
