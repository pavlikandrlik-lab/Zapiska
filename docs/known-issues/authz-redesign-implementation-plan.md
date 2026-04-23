# Implementační plán: redesign permission modelu na per-action klíče

**Status:** Ready for implementation, 2026-04-23.
**Cíl:** postupný, testovatelný průchod redesignem seed-only RBAC. Každý krok je zvlášť commit v jednom PR, s gate testem.
**Vstupní dokumenty:**
- [authz-redesign-per-action-keys.md](authz-redesign-per-action-keys.md) — katalog 76 klíčů + mapovací tabulky per controller
- [authz-target-matrix.xlsx](authz-target-matrix.xlsx) — matice 11 rolí × 76 klíčů
- [authz-ui-serverside-mismatch.md](authz-ui-serverside-mismatch.md) — nálezy 1–12 (všechny budou v tomto redesignu uzavřeny)
- [meetings-endpoints-split-between-controllers.md](meetings-endpoints-split-between-controllers.md) — zohledněno (meetings přesun do `JednaniController`)

**PR strategie:** **jeden velký PR**, rozdělený do commitů = fází. Důvod: autorizace nesmí být nikdy ve „mezi-stavu", kde některá část kódu používá nové klíče a jiná staré. Review je per-commit, merge je najednou.

**Odhad rozsahu:** 1–2 týdny fokusované práce pro jednoho seniorního vývojáře. Infra + testy jsou 60 % času, samotný refaktor 40 %.

---

## Krok 0 — Přípravná baseline (před začátkem)

### Cíl

Zajistit, aby existující testy před refaktorem přecházely. Kdyby něco rozbilo, hned víme, že to je naše změna.

### Úkoly

1. **Aktualizovat `dev` branch** — pull latest, spustit `dotnet build` a `dotnet test` — vše musí projít (0 warnings / 0 failures).
2. **Vytvořit feature branch**: `git checkout -b feature/authz-redesign-per-action-keys`
3. **Sepsat snapshot aktuálních permission keys**:
   ```bash
   grep -rn 'permission:' PmTracker.Web/Controllers/ > /tmp/before-redesign.txt
   ```
   Uloží se pro porovnání na konci (očekáváme, že všechny staré klíče ze snapshotu pryč, nové tam budou).
4. **Projet `AuthorizationPolicyEnforcementTests`** — poznamenat, jaké endpointy jsou dnes allowlistnuté jako „bez policy, OK". Po refaktoru by allowlist měl být **prázdný**.

### Gate

`dotnet build --configuration Release` + `dotnet test` = 100 % zelené, žádné nové warnings.

---

## Fáze 1 — Seed + konstanty

### Cíl

Nahradit `PermissionSeedConfiguration.Actions` a `RoleMappings` novým katalogem 76 klíčů a 11 rolemi × klíč mappingem. Po commitu: `PermissionSeeder` při startu aplikace vytvoří v DB nové klíče, ale **kód nikde nové klíče ještě nepoužívá** — běh aplikace je zatím nerozbitý (stále běží na starých klíčích, i když v DB vedle sebe leží staré i nové).

### Soubory k úpravě

1. **[PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs](../../PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs)**

   Kompletně přepsat:

   - `Actions` — všech 76 klíčů z katalogu §1–§15 redesign dokumentu, každý s `PermissionScopeLevel` (Global / Project) a `categoryKod` (PROJECTS / RECORDS / …).
   - `RoleMappings` — ~400 řádků, podle matice v [authz-target-matrix.xlsx](authz-target-matrix.xlsx) „Role × Permission (target)".
   - `Categories` — ponechat pokud existuje struktura, případně rozšířit o nové kategorie (PROPOSALS, EXTERNI, DASHBOARD, EXPORT, SEARCH, SCHEDULE).

   **Staré klíče v seedu zatím nechat** (`records.schedule.add`, `records.comment.subsystemlead`, `comments.edit.own`, `comments.delete.own`, `team.manage`, `people.manage`, `ciselniky.edit`, `settings.manage`, `export.pdf`, `export.word`). Kód je dál používá. **Smazání starých klíčů** proběhne až v posledním commitu (Krok 10) — DB migrace.

2. **[PmTracker.Web/Models/ViewModels/SecurityViewModels.cs](../../PmTracker.Web/Models/ViewModels/SecurityViewModels.cs)**

   - `PermissionKeys` konstanty — přidat 76 nových konstant s `PascalCase` jmény (např. `public const string ProjectsCreate = "projects.create";`).
   - **Ponechat** staré konstanty s `[Obsolete]` atributem, aby kompilátor hlásil varování na místech, kde se staré klíče používají (dává lepší discoverability pro Fáze 2–4).
   - `PermissionMetadata` — rozšířit o metadata pro nové klíče (popis v UI, kategorie).
   - `PermissionKeys.GrantsProjectRead` — nový klíč `projects.read.all` by měl padat pod tento helper.
   - `PermissionKeys.IsBlockedForDeletedProject` — aktualizovat seznam o mutující klíče nového modelu (records.*, meetings.*, comments.*, proposals.*, vyzvy.* — ne dashboard / export / read).

3. **DB sanity check**

   Po build + run aplikace v dev prostředí, zkontrolovat DB:
   ```sql
   SELECT COUNT(*) FROM authz.permissions WHERE klic LIKE 'projects.%'; -- očekáváme ≥ 4
   SELECT COUNT(*) FROM authz.permissions WHERE klic LIKE 'proposals.%'; -- očekáváme 7
   SELECT klic FROM authz.permissions ORDER BY klic; -- vizuálně projít
   ```

### Gate

- `SeedSourceOfTruthTests` — projít bez chyby (každý klíč z `PermissionKeys` je v seedu a naopak). **Test je třeba rozšířit**, aby akceptoval i obsolete klíče (nebudou v `PermissionKeys` jako konstanty s `[Obsolete]`, ale stále v seedu).
- `dotnet test` — zelené.
- Smoke test manuální: otevřít aplikaci, přihlásit se. Všechno stále funguje (kód používá staré klíče).

### Commit

`feat(authz): add per-action permission keys to seed (Phase 1 — seed only)`

---

## Fáze 2 — Controllers: přepsat `[Authorize(Policy)]` atributy

### Cíl

Na každé mutující akci v každém kontroléru přepsat `[Authorize(Policy = "permission:<starý>")]` na nový klíč. Po commitu: kód používá nové klíče ze seedu, staré klíče v seedu zůstávají (v DB), ale kontroléry je přestávají zmiňovat.

### Pracovní postup (one controller at a time)

Každý controller je **samostatný sub-commit** v téhle fázi, aby review mohl jít modulárně. Pořadí podle důležitosti / hustoty akcí:

#### 2.1 — `ProjektyController.cs` + partials

**Soubory:**
- `PmTracker.Web/Controllers/ProjektyController.cs`
- `PmTracker.Web/Controllers/ProjektyController.Commands.cs`
- `PmTracker.Web/Controllers/ProjektyController.MeetingModals.cs`
- `PmTracker.Web/Controllers/ProjektyController.ProjectModals.cs`
- `PmTracker.Web/Controllers/ProjektyController.TabPartials.cs`

**Změny** (viz mapovací tabulka v redesign dokumentu §ProjektyController):

- `NewProjectModal` → `projects.create` (beze změny, jen potvrdit)
- `EditProjectModal`, `DeleteProjectModal`, `DeleteProject` → `projects.edit`, `projects.delete` (beze změny)
- `SaveProject` → rozdělit větev nový/update:
  - `if (command.Id == 0)` → `HasPermission(projects.create)`
  - `else` → `HasPermission(projects.edit, command.Id)`
- **Přesun meetings akcí do `JednaniController`** (podle [meetings-endpoints-split](meetings-endpoints-split-between-controllers.md)) — přesunout, přejmenovat `SaveMeeting` → `Save`, `DeleteMeeting` → `Delete`, v `Jednani.Modals.cs` a `Jednani.Commands.cs`.
- `AddTeamMemberModal`, `SaveTeamMember` → `team.member.add`
- `RemoveTeamMember` → `team.member.remove`
- `AssignProjectRoleModal`, `AssignProjectRole` → `team.role.assign`
- `DeactivateProjectRole` → `team.role.deactivate`
- `AssignProjectSubsystemModal`, `AssignProjectSubsystem` → `team.subsystem.create`
- `ReorderProjectSubsystem` → `team.subsystem.reorder`
- `DeactivateProjectSubsystem` → `team.subsystem.deactivate`
- `AssignProjectSubsystemRoleModal`, `AssignProjectSubsystemRole` → `team.subsystem.role.assign`
- `DeactivateProjectSubsystemRole` → `team.subsystem.role.deactivate`
- `SearchProjectMemberCandidates` → `team.candidates.search`
- TabPartials (RecordsTabPartial, HarmonogramTabPartial, JednaniTabPartial, TymTabPartial, NavrhyTabPartial) — **zůstávají auth-only**, žádná policy.

**Gate:** `AuthorizationPolicyEnforcementTests` pro každou akci, vlastní integrační test (role × endpoint).

#### 2.2 — `JednaniController.cs`

Přijímá přesunuté akce z ProjektyController (krok 2.1). Plus vlastní akce:

- `Save` (přesunuto) → `meetings.create` (new) / `meetings.edit` (update)
- `Delete` (přesunuto) → `meetings.delete`
- `AddMeetingParticipantModal`, `AddMeetingParticipant` → `meetings.participant.add`
- `SaveStatus` → `meetings.status.change`
- `SaveAttendance` → `meetings.attendance.edit`
- `SaveNotes` → `meetings.notes.edit`; **navíc** v service `MeetingService.SaveNotesAsync` kontrola: pokud uživatel nemá `meetings.notes.edit` ale má `meetings.notes.subsystemlead`, povolit jen subsystem lead notes (viz §4 v redesign dokumentu).
- `NewMeetingModal`, `EditMeetingModal` → `meetings.create`, `meetings.edit`
- `Index`, `Detail`, `TaskItemPartial` → auth-only

**Soubory:** `JednaniController.cs`, nové `JednaniController.Commands.cs`, `JednaniController.Modals.cs`.

#### 2.3 — `ZaznamyController.cs` + Commands + Modals + Partials

- `Create` → `records.create`
- `Edit` → `records.edit`
- `Save` → `records.create` (new) / `records.edit` (update) / `records.schedule.edit` (pokud changes only schedule)
- `DeleteRecordModal`, `DeleteRecord` → `records.delete`
- `AssignMeetingIdentifierModal`, `AssignMeetingIdentifier` → `records.assign.meeting`
- `AddComment` → `comments.add`
- `UpdateComment` → `comments.edit.own` (policy gate); service `CommentAuthorizationPolicy.CanModifyComment` testuje `comments.edit.any` pro cizí komentář
- `DeleteComment` → `comments.delete.own` (policy gate); service testuje `comments.delete.any`

#### 2.4 — `NavrhyController.cs`

- `CreateRecordProposal`, `SubmitCreateProposal` → `proposals.record.create`
- `CreateScheduleProposal`, `SubmitScheduleProposal` → `proposals.schedule.create`
- `PrefillCreateProposal` → `proposals.edit.own`
- **Smazat** `EditFromProposal` (akce i odkazy — viz §5 v redesign dokumentu).
- `ApproveProposal` → `proposals.accept`
- `RejectProposal` → `proposals.reject`
- `RejectAndTakeOverCreateProposal` → `proposals.takeover`
- `RejectAndEditProposal` → `proposals.reject` (policy gate) + service check `records.edit` před uložením
- `ProposalDetail` → auth-only

#### 2.5 — `VyjadreniModalController.cs` + `ExterniOdkazController.cs`

- `Modal` → `vyjadreni.modal.open` (nový klíč)
- `Refresh` → `vyjadreni.refresh`
- `CreateVazba` → `vyjadreni.vazba.create`
- `DeleteVazba` → `vyjadreni.vazba.delete`
- `ReHarvest` → `vyjadreni.reharvest`
- `ExterniOdkaz.Sync` → `externiodkazy.sync`

#### 2.6 — `VyzvyController.cs`

- `Zalozit` → `vyzvy.create`
- `ZmenitStav` → `vyzvy.state.change`
- `SetZaradid` → `vyzvy.pnf.assign`
- `Prerdit` → `vyzvy.pnf.reassign`
- `ReassignModal` → `vyzvy.pnf.reassign`
- (Word export endpoint pro budoucnost: `vyzvy.word.export` — klíč v seedu, akce zatím neexistuje.)

**Pozor:** request DTOs `ZaloztRequest`, `ZmenitStavRequest` atd. už mají `ProjektId` (ověřeno v bod 2.6 redesign dokumentu). `PermissionAuthorizationHandler` bere `projektId` z `RouteValues` — pokud je v body, musí se přidat jako route parameter, nebo udělat imperativní check v těle akce.

#### 2.7 — `ProjectDashboardController.cs`

- `Index` → `dashboard.view`
- `RecordsPanel` → `dashboard.records.view`
- `NesPanel` → `dashboard.nes.view`
- `StatisticsPanel` → `dashboard.statistics.view`
- `VyzvyPanel` → `dashboard.vyzvy.view`

#### 2.8 — `ExportController.cs`

- `ProjektTisk` → `export.pdf.projekt`
- `ProjektWord` → `export.word.projekt`
- `JednaniTisk` → `export.pdf.jednani`
- `JednaniWord` → `export.word.jednani`
- `UkolTisk` → `export.pdf.ukol`
- `UkolWord` → `export.word.ukol`
- `Pdf` (dialog wrapper) → `export.pdf.projekt`
- `Dialog` → auth-only

#### 2.9 — `ScheduleController.cs`

- `Recalc` → **nová akce** s:
  - imperativním checkem na OR dvou klíčů: `schedule.preview` (dostatečný sám o sobě), kdokoli ho má.
  - `SchedulePreviewRequest` rozšířit o `ProjektId` (nové pole required > 0).
  - Metoda stane `async` kvůli `IAuthorizationService.HasPermissionAsync`.

#### 2.10 — `SearchController.cs`

- `Index`, `Suggest` → `search.index`
- `Reindex`, `Status` → `search.reindex`

#### 2.11 — `NastaveniController.cs` + `NastaveniSyncController.cs` + `SDConnectorController.cs`

- `Nastaveni.Index/Panel` → `settings.view`
- `UserRolesModal`, `SaveUserRole`, `SaveUserRolesForUser` → `settings.roles.assign`
- `NastaveniSync.Save` → `settings.sync.configure`
- `NastaveniSync.RunNow` → `settings.sync.run`
- `SDConnector.Index` → `settings.sd.view` (class-level `[Authorize]` odstranit, per-action)
- `SDConnector.ReHarvest` → `vyjadreni.reharvest` (sdílí s VyjadreniModal)

#### 2.12 — `OsobyController.cs`

- `Index` → `people.edit` (gate pro seznam)
- `AdPersonModal`, `SearchAd` → `people.ad.search`
- `SaveAd` → `people.create`
- `ManualPersonModal (new)`, `SaveManual (new)` → `people.create`
- `ManualPersonModal (edit)`, `SaveManual (update)` → `people.edit`
- `SyncFromAd` → `people.ad.sync`
- `Delete` → `people.delete`

#### 2.13 — `CiselnikyController.cs`

- `Index`, `Detail`, `Panel` → `ciselniky.row.edit` (gate pro listing)
- `EditRow`, `SaveRow` → `ciselniky.row.edit`
- `DeleteRow` → `ciselniky.row.delete`

### Gate po každém sub-commit Fáze 2

- `AuthorizationPolicyEnforcementTests` projde — kontroluje, že mutující akce má `[Authorize(Policy)]`.
- `dotnet test` zelené.
- **Manuální smoke test** pod PROJ_MAN: nejdůležitější akce v kontroléru musí fungovat.

### Commit (per sub-commit)

`feat(authz): migrate <ControllerName> to per-action policy keys`

---

## Fáze 3 — Service vrstva

### Cíl

Odstranit hardkódované role codes a backdoors. Každé rozhodnutí v service je založené na `HasPermission(...)`, nikoli na porovnání s `ProjectRoleCodes.*`.

### 3.1 — `CommentAuthorizationPolicy.cs`

**Změna:**
```csharp
public bool CanModifyComment(...)
{
    if (currentUser.HasPermission(PermissionKeys.CommentsEditAny, projektId))  // OR CommentsDeleteAny podle operace
        return true;
    // ... autor check zůstává
}
```

Zvažit rozdělit na `CanEditComment` / `CanDeleteComment`, aby bylo jasné, která `.any` varianta se kontroluje.

### 3.2 — `RecordProposalAuthorizationPolicy.cs`

**Změny:**

- `CanDecideProjectProposalAsync` — **smazat DB query na `ProjectRoleCodes`**, nahradit za:
  ```csharp
  return Task.FromResult(currentUser.HasPermission(PermissionKeys.ProposalsAccept, projectId));
  ```
  (nebo `proposals.edit.any` podle rozhodnutí — jednotné pro rozhodování i úpravy).
- `ResolveCreatableSubsystemIdsAsync` — **beze změny**, subsystémový filtr je legitimní business constraint, zůstává.
- `EvaluateProjectAccessAsync` — rozšířit o admin větev (kdo má `proposals.edit.any` dostane všechny subsystémy projektu).

### 3.3 — `RecordProposalService.SubmitCommands.cs`

**Kritické:** doplnit subsystémovou validaci v POST flow (IDOR fix — viz §5 redesign dokumentu).

```csharp
public async Task SubmitScheduleProposalAsync(...)
{
    var isAdmin = currentUser.HasPermission(PermissionKeys.ProposalsEditAny, projektId);
    if (!isAdmin)
    {
        var recordSubsystemId = await _db.ProjektoveZaznamy.AsNoTracking()
            .Where(z => z.Id == command.ZaznamId && z.ProjektId == projektId)
            .Select(z => (int?)z.SubsystemId)
            .FirstOrDefaultAsync(ct);
        if (recordSubsystemId is null) throw new ForbiddenException("Záznam nepatří do projektu.");

        var isLead = await _db.ObsazeniSubsystemuProjektu.AsNoTracking()
            .AnyAsync(o => o.OsobaId == currentUser.OsobaId
                && !o.DatumOdebrani.HasValue
                && o.ProjektSubsystem.ProjektId == projektId
                && o.ProjektSubsystem.SubsystemId == recordSubsystemId.Value
                && (o.RoleSubsystemu.Kod == SubsystemRoleCodes.Lead
                    || o.RoleSubsystemu.Kod == SubsystemRoleCodes.DeputyLead), ct);
        if (!isLead) throw new ForbiddenException("Nejste Lead/DeputyLead subsystému záznamu.");
    }
    // ... uložení návrhu
}
```

Stejnou validaci doplnit v `SubmitCreateRecordProposalAsync`.

### 3.4 — `RecordProposalService.Queries.cs`

- **Smazat** metodu `BuildEditableRecordEditorFromProposalAsync` (EditFromProposal bypass je pryč).
- Ověřit, že `model.CanPrefillProposalForm` se vypočítává z `proposals.edit.own` klíče (ne z `CanDecide…`).

### 3.5 — `ProjectDashboardService.cs`

- **Smazat** `CanUserEditProjectVyzvyAsync` — nahradit za `HasPermission(PermissionKeys.VyzvyCreate, projektId)`.
- **Smazat** `CanAccessDashboardAsync` — nahradit za `HasPermission(PermissionKeys.DashboardView, projektId)`.
- V metodě `BuildVyzvyPanelAsync` použít přímo `HasPermission` místo injected boolu.

### 3.6 — `ProjectDashboardAuthorizationPolicy.cs`

**Smazat celý soubor.** Jeho obsah (hardkódovaný whitelist rolí) byl nahrazen `HasPermission` v `ProjectDashboardService`.

### 3.7 — `VyzvaService.Assignment.cs`

Místo `records.edit` použít:
- `HasPermission(VyzvyPnfAssign, projektId)` pro `NastavitZaradidAsync`
- `HasPermission(VyzvyPnfReassign, projektId)` pro `PrerditPnfAsync`
- V `AssignToRecord` / `ReassignAcrossProjects` stejná logika — pokud sahá na záznam, `records.edit`.

### 3.8 — `RecordService.SaveRecord.cs`

- Odstranit větev `records.schedule.add` (ten klíč už neexistuje).
- Zachovat `records.edit` a `records.schedule.edit` logiku.

### 3.9 — `MeetingService.SaveNotes.cs`

Duální gate: pokud `meetings.notes.edit` ano, pokud `meetings.notes.subsystemlead` pak jen subsystem lead-specific pole. Logika v service.

### Gate po Fázi 3

- `dotnet test` zelené.
- Grep test: `grep -rn 'ProjectRoleCodes\.\|"proj_man"\|"adm_proj"' PmTracker.Web/Services` — **prázdný** (kromě allowed lokací: seeder, linker, IDENTIFIKAČNÍ mapování).
- Integrační test: HOST volá `POST /Navrhy/Approve/...` → 403.

### Commit

`feat(authz): remove hardcoded role codes and backdoors in service layer`

---

## Fáze 4 — ViewModels + Views

### 4.1 — ViewModel `Can…` properties

Pro každou `Can…` boolean property v ViewModelu:

- Přepsat, aby vypočítávala z `CurrentUserContextViewModel.HasPermission(newKey, projektId)`.
- **Smazat** propertyy, které jsou po redesignu nadbytečné: `CanAddSchedule`, `CanManageSchedule` (zjednodušit na `CanEditSchedule || CanEditRecord`).
- Zachovat `HasHostRole` / `HasNonHostProjectRole` (Nález 10 — legitimní UI klasifikace, ne authz gate).

**Soubory:**
- `PmTracker.Web/Models/ViewModels/SecurityViewModels.cs` — dispatch metody.
- `PmTracker.Web/Models/ViewModels/Projekty/*.cs` — všechny VMs s `Can…` properties.
- Computed properties v controllerech (`PrepareProjectDetailPresentationAsync` apod.) — přepsat na nové klíče.

### 4.2 — Views

Grep `@if (Model.Can` napříč `PmTracker.Web/Views/**/*.cshtml` + seznam odkazů z mapovací tabulky (redesign dokument):

- Každý `<button>`, `<a>`, `<form>` obalit správným guardem.
- **Smazat** tlačítko „Předvyplnit formulář" v `_EditZaznamForm.cshtml:191-199` (EditFromProposal bypass).
- Aktualizovat `_Layout.cshtml` menu — gate odkazy podle nových klíčů (např. Osoby odkaz pod `HasPermission(people.edit)`).

### Gate

- `dotnet build` bez warnings (všechny obsolete klíče přejmenované nebo smazané).
- **Manuální smoke test UI** pod 6 rolemi: SUPERADMIN, APP_ADMIN, PROJ_MAN, GEST, HOST, VEDOUCI_SUBSYSTEMU. Pro každou:
  - Hlavní menu — vidí jen položky, na které má klíč.
  - Detail projektu — tlačítka upravit/smazat/exportovat podle matice.
  - Dashboard — všech 5 panelů (u rolí s klíči).

### Commit

`feat(authz): migrate ViewModels and Views to per-action permission gates`

---

## Fáze 5 — JavaScript

### 5.1 — `ScheduleController.Recalc` client

[block.js:22-48](../../PmTracker.Web/wwwroot/js/modules/schedule/block.js) — `fetchSchedulePreview`:

```js
async function fetchSchedulePreview(projektId, startDate, deadlineDate, steps, antiForgeryToken) {
    const payload = {
        projektId,  // NOVÉ — nutné pro server-side policy check
        recordId: 0,
        startDate: formatIsoDate(startDate),
        // ... zbytek beze změny
    };
    // ... fetch
}
```

Volače `fetchSchedulePreview` — předávají `projektId` ze state editoru. State už projektId má (grep `data-projekt-id`).

### 5.2 — Client-side gating (pokud existuje)

Grep `data-permission` / `data-can-*` v `PmTracker.Web/wwwroot/js/`. Aktualizovat názvy atributů na nové klíče.

### Gate

- Manuální test editoru úkolu pod PROJ_MAN — preview timeline funguje.
- Manuální test pod HOST — AJAX `403` v konzoli, timeline se nepřepočítá (degradovaný stav, ne crash).

### Commit

`feat(authz): update JS client for schedule preview authz + gating`

---

## Fáze 6 — Architecture + integrační testy

### 6.1 — Rozšířit `AuthorizationPolicyEnforcementTests`

- **Pravidlo 1:** každý mutující endpoint (POST/DELETE/PUT) má `[Authorize(Policy = "permission:…")]` nebo `[AllowAnonymous]`. **Smazat allowlist** — po redesignu by neměl být potřeba.
- **Pravidlo 2 (nové):** každý policy klíč je použit právě na 1 endpointu (detekce kopírování klíčů). Výjimky: `vyjadreni.reharvest` (sdílený mezi VyjadreniModal + SDConnector), `proposals.record.create` (CreateRecordProposal GET i SubmitCreateProposal POST), analogicky pro schedule. Allowlist s rationale.

### 6.2 — Nový test `NoHardcodedRolesTest`

Reflection scan:
- Prohledej `PmTracker.Web/Services/**/*.cs` a `PmTracker.Web/Controllers/**/*.cs`.
- Detekuj syntaxe: `ProjectRoleCodes.ProjectAdmin`, `ProjectRoleCodes.ProjectManager`, `"proj_man"`, `"adm_proj"`, `"VEDOUCI_SUBSYSTEMU"` atd.
- Povolené výjimky: `PermissionSeedConfiguration.cs`, `PermissionSeeder.cs`, `RoleCatalogLinker.cs`, `UserContextResolver.cs` (kde se role codes načítají z DB jako data).
- Fail pokud najde hit mimo povolená místa.

### 6.3 — Rozšířit `SeedSourceOfTruthTests`

- Bijekce: každý `PermissionKeys.*` konstanta ↔ seed Action.
- Každá role v `Roles` má aspoň jeden mapping v `RoleMappings`.
- Každý mapping odkazuje na existující role kod + existující action klic.

### 6.4 — Integrační testy per-role × per-endpoint

Sampling:
- **SUPERADMIN** — smí všechno (smoke test na každé mutating akci, 200).
- **APP_ADMIN** — smí všechno (= SUPERADMIN).
- **PROJ_MAN** (přiřazen k projektu X):
  - Smí projektové akce na projektu X (200).
  - Nesmí `projects.edit` / `projects.delete` (403 i na svém projektu).
  - Nesmí `people.edit` (403).
  - Nesmí globální `settings.roles.assign` (403).
- **HOST** (přiřazen k projektu X):
  - Smí `dashboard.*` (200).
  - Smí `export.*` (200).
  - Nesmí `comments.add` (403).
  - Nesmí `records.edit` (403).
  - Nesmí `proposals.*` (403).
- **VEDOUCI_SUBSYSTEMU** (přiřazen k subsystému S projektu X):
  - Smí `proposals.record.create` pro záznam v S (200).
  - Nesmí `proposals.record.create` pro záznam mimo S (403 — IDOR test).
  - Nesmí `proposals.accept` (403).
- **GEST** (přiřazen k projektu X):
  - Smí `comments.add`, `comments.edit.own`, `comments.delete.own` (200).
  - Nesmí `comments.edit.any` (403 na cizím komentáři).
  - Smí `dashboard.*` + `export.*` (200).
- **READ_ALL** (globální):
  - Smí `projects.read.all` (200 na seznamu všech projektů).
  - Smí `dashboard.*` + `export.*` (200).
  - Nesmí mutace (403).

**Počet testů:** cca 60–80 kombinací. Lze generovat přes `[Theory]` + `MemberData`.

### Gate

- `dotnet test` zelené, včetně nových testů.
- `NoHardcodedRolesTest` projde.

### Commit

`test(authz): add architecture tests + per-role integration coverage`

---

## Fáze 7 — DB migrace (odstranění starých klíčů)

### Cíl

Vyčistit DB od starých klíčů, které už kód nepoužívá.

### Soubor

`db_upgrade_X_Y_Z_authz_per_action_redesign.sql`:

```sql
-- Odstranit staré (pre-redesign) permission keys, které byly sjednoceny / rozděleny / přejmenovány.
DELETE FROM authz.role_permissions
WHERE permission_id IN (
  SELECT id FROM authz.permissions
  WHERE klic IN (
    'records.schedule.add',
    'records.comment.subsystemlead',
    'team.manage',
    'people.manage',
    'ciselniky.edit',
    'settings.manage',
    'export.pdf',
    'export.word'
  )
);

DELETE FROM authz.permissions
WHERE klic IN (
  'records.schedule.add',
  'records.comment.subsystemlead',
  'team.manage',
  'people.manage',
  'ciselniky.edit',
  'settings.manage',
  'export.pdf',
  'export.word'
);

-- Verifikace — po spuštění by tyto klíče v DB neměly existovat:
-- SELECT COUNT(*) FROM authz.permissions WHERE klic IN ('records.schedule.add', ...);  -- 0
```

### Poznámka k offline deploymentu

Projekt neběží na internetu — DB skripty se aplikují ručně přes DBA. Skript poslat spolu s release notes. **Před produkcí** otestovat v dev + staging prostředí.

### Také

- Odstranit `[Obsolete]` konstanty z `SecurityViewModels.cs` (byly tam dočasně pro Fázi 2).
- Odstranit staré klíče ze seedu `PermissionSeedConfiguration.Actions` (v Fázi 1 zůstaly pro kompat).

### Gate

- Po aplikaci DB skriptu, spustit `SeedSourceOfTruthTests` — musí projít.
- Manuální DB check: `SELECT klic FROM authz.permissions ORDER BY klic` — 76 klíčů, žádný starý.

### Commit

`feat(authz): remove obsolete permission keys from seed and DB`

---

## Fáze 8 — Finální verifikace a merge

### 8.1 — Manuální smoke test

Pod každou rolí projít klíčové workflow:

1. **SUPERADMIN** — založit projekt, záznam, jednání, vytvořit výzvu, schválit návrh, smazat komentář.
2. **APP_ADMIN** — stejné (ověřit že = SUPERADMIN).
3. **PROJ_MAN** — na svém projektu: přidat člena týmu, vytvořit záznam, založit výzvu. Na cizím projektu: 403.
4. **HOST** — otevřít dashboard (všech 5 panelů), export PDF + Word. Pokus o komentář → 403.
5. **VEDOUCI_SUBSYSTEMU** — založit návrh záznamu ve svém subsystému. Pokus o návrh mimo subsystém → 403. Otevřít dashboard.
6. **GEST** — komentovat, upravit svůj komentář, smazat svůj komentář. Pokus o editaci cizího → 403.

### 8.2 — Code review checklist

- [ ] `PermissionSeedConfiguration.cs` má 76 Actions + ~400 RoleMappings.
- [ ] `PermissionKeys` konstanty (76) — každá použita alespoň na jednom místě.
- [ ] Žádný `[Obsolete]` atribut zůstává.
- [ ] `grep -rn 'ProjectRoleCodes\.' PmTracker.Web/Services PmTracker.Web/Controllers` — prázdný (mimo allowlistované cesty).
- [ ] `grep -rn 'permission:records\.schedule\.add\|permission:comments\.edit\.own\|permission:team\.manage' PmTracker.Web` — prázdný (staré klíče pryč z policy atributů).
- [ ] `AuthorizationPolicyEnforcementTests` allowlist **prázdný**.
- [ ] Všechny nálezy 1–12 v [authz-ui-serverside-mismatch.md](authz-ui-serverside-mismatch.md) jsou uzavřené (u každého připsat „Uzavřeno v PR #XXX").
- [ ] [authz-target-matrix.xlsx](authz-target-matrix.xlsx) odpovídá kódu.
- [ ] [docs/authorization.md](../authorization.md) aktualizován s novou maticí rolí × klíčů.

### 8.3 — Release notes

V `docs/changelog/releases/` založit nový soubor s notkou:

```md
## Authz redesign — per-action permission keys

**Backward-incompatible change.** Permission model aplikace byl přepsán z hrubých klíčů
(`records.edit`, `team.manage`, `people.manage` apod.) na per-action klíče (`records.create`,
`records.edit`, `records.delete`, `team.member.add`, ...).

### Dopad na uživatele

Role byly přemapovány podle nové matice (viz docs/authorization.md). Žádná role by
neměla ztratit dosavadní schopnosti, ale **některé dosavadní nezamýšlené privilegium
je pryč** (např. APP_ADMIN už nemá tichý přístup ke kontentu projektů, PROJ_MAN už
nesmí měnit metadata projektu — to dělá jen admin).

### Dopad na integrace

- **Zrušené endpointy:** `Navrhy.EditFromProposal` (bypass workflow — uzavřený návrh).
- **Přesunuté endpointy:** meeting akce z `/Projekty/...` na `/Jednani/...`.
- **Zpětná kompatibilita URL:** není implementována — volači musí aktualizovat.

### Pro admina

DB skript `db_upgrade_X_Y_Z_authz_per_action_redesign.sql` musí být aplikován
**po nasazení nové verze aplikace**.
```

### Merge

Merge do `main`. Tag `vX.Y.Z-authz-redesign`.

---

## Rollback plán

Pokud po nasazení zjištěn kritický problém:

1. **Rollback aplikace:** vrátit předchozí verzi (deployment příkaz).
2. **Rollback DB:** DBA zpátky nahraje staré klíče přes reversní SQL skript:
   ```sql
   -- Skript připravit dopředu během Fáze 7, uložit vedle forward-migration skriptu.
   INSERT INTO authz.permissions (klic, ...) VALUES ('records.schedule.add', ...);
   -- atd. pro všech 8 starých klíčů
   -- + INSERT INTO authz.role_permissions ...
   ```

Pozor: pokud mezi nasazením a rollbackem někdo přidal nové role v UI, ty se mohou ztratit. Rollback **není bez rizika** — dokumentovat v release notes.

---

## Časový odhad

| Fáze | Čas | Poznámka |
|---|---|---|
| 0 | 30 min | Baseline |
| 1 | 4 h | Seed + konstanty — hodně dat, ale přímočaré |
| 2 | 2 d | 13 kontrolérů × průměrně 1–2 h |
| 3 | 1 d | Service vrstva + IDOR fixy |
| 4 | 1 d | ViewModels + Views — hodně Views k projetí |
| 5 | 2 h | JS + smoke test |
| 6 | 1 d | Architecture testy + integrační testy |
| 7 | 2 h | DB skript + cleanup |
| 8 | 1 d | Manuální smoke + review + release notes |
| **Celkem** | **~8 pracovních dnů** | fokusovaný vývojář |

---

## Commit graf (cílový)

```
feature/authz-redesign-per-action-keys
├── feat(authz): add per-action permission keys to seed (Phase 1)
├── feat(authz): migrate ProjektyController to per-action policy keys
├── feat(authz): migrate JednaniController (+ meetings endpoints split)
├── feat(authz): migrate ZaznamyController
├── feat(authz): migrate NavrhyController (+ remove EditFromProposal bypass)
├── feat(authz): migrate VyjadreniModalController + ExterniOdkazController
├── feat(authz): migrate VyzvyController
├── feat(authz): migrate ProjectDashboardController
├── feat(authz): migrate ExportController + ScheduleController
├── feat(authz): migrate SearchController + NastaveniController + SDConnector
├── feat(authz): migrate OsobyController + CiselnikyController
├── feat(authz): remove hardcoded role codes in service layer
├── feat(authz): migrate ViewModels and Views
├── feat(authz): update JS client for schedule preview
├── test(authz): architecture tests + per-role integration coverage
├── feat(authz): remove obsolete permission keys from seed and DB
└── docs(authz): update authorization.md with new matrix
```

**Review:** per-commit (17 commitů). Merge squash **NEdoporučuji** — historie per-commit zůstane užitečná pro git bisect, pokud se v budoucnu objeví regrese.
