# Audit autorizace: projít celou aplikaci a ověřit soulad UI ↔ server-side

**Status:** Open, vznik 2026-04-23. Rozsah: **celá aplikace**. Potřebuje analýzu uživatele (byznysová rozhodnutí, které role co smí) a následnou implementaci.
**Kategorie:** Security / Architecture.
**Souvislost:** [docs/authorization.md](../authorization.md), [project_authz_architecture.md](../../memory/project_authz_architecture.md), seed-only RBAC refactor dokončený 2026-04-22.

## Zadání

**Projít všechny akce a zobrazení v celé aplikaci a ověřit každé z těchto tvrzení:**

1. **UI parity** — uživatel, který nemá právo na akci, **nevidí tlačítko / odkaz / položku menu**, která tu akci spouští.
2. **Server-side enforcement** — server **zamítne** volání, když volající nemá právo — a to nezávisle na tom, jestli UI tlačítko bylo zobrazeno, nebo ne (útočník může endpoint volat přímo).
3. **Single source of truth** — UI gating i server-side gating čtou ze stejného zdroje: `AuthorizationSnapshot.HasPermission(key, projektId?, subsystemId?)` driven [PermissionSeedConfiguration.cs](../../PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs). **Žádné hardkódované seznamy rolí** v service/controller/view vrstvě.
4. **Data scope** — u list/detail endpointů ověřit, že uživatel vidí **jen ta data, na která má právo** (ne jen že akce je chráněná, ale že i filtrace záznamů respektuje oprávnění / viditelnost projektů / subsystémů).

Audit je **blokován byznysovým rozhodnutím uživatele** – viz sekce „Analýza potřebuje" níže. Bez rozhodnutí o tom, které role mají co smět, nelze implementovat sjednocení.

## Rozsah auditu

Projít **všechny** níže uvedené vrstvy systematicky:

### 1. HTTP endpointy (controllery)

Pro **každou akci v každém controlleru** v [PmTracker.Web/Controllers/](../../PmTracker.Web/Controllers/) ověřit:

- Má mutující akce (`[HttpPost]`, `[HttpDelete]`, `[HttpPut]`) explicitní `[Authorize(Policy = "permission:…")]` nebo `[AllowAnonymous]`?
- Pokud používá imperativní `HasPermission` check v těle, je check na správném klíči a se správným projektId / subsystemId?
- U GET endpointů, které vracejí citlivá data (modaly, partials, export, panely): je chráněno policy nebo imperativně?
- Route parametry `projektId` / `projektSubsystemId` jsou konzistentně pojmenované (viz M4 fix v `PermissionAuthorizationHandler`).
- `ValidateAntiForgeryToken` na všech mutujících POSTech.

**Výstup auditu:** tabulka `Controller.Action → PermissionKey → ExpectedRoles` pro každou akci.

### 2. Views (UI gating)

Pro **každý soubor v** [PmTracker.Web/Views/](../../PmTracker.Web/Views/) ověřit:

- Každé `<button>`, `<a>`, `<form>` akce odkazující na chráněný endpoint je obaleno `@if (Model.CanXxx)` nebo ekvivalentem.
- Property `CanXxx` na ViewModelu je vypočtena z `AuthorizationSnapshot`, **nikoli** z hardkódovaných rolí (`activeRoleCodes.Contains("proj_man")` apod.).
- Záložky / taby / sekce nabídky respektují oprávnění (neukázat HOSTovi „Dashboard", pokud business nechce; neukázat záložku „Výzvy" rolím, které tam nemají co dělat).
- Menu a navigace (`_Layout.cshtml`, dashboard taby, nástrojové lišty).
- Modal triggery a akce uvnitř modálů.

**Výstup auditu:** každá UI gate má dokumentovaný odpovídající permission key.

### 3. ViewModel buildery

Pro každou `Can…` property na ViewModelu ověřit, že se počítá **výhradně** přes `CurrentUserContext.HasPermission(key, projektId)` nebo `IAuthorizationService.HasPermissionAsync(...)`. Místa k projití:

- [ProjektyController.cs](../../PmTracker.Web/Controllers/ProjektyController.cs) – `PrepareProjectDetailPresentationAsync`, `AttachCurrentUser`, `PrepareDashboardLinks`.
- [JednaniController.cs](../../PmTracker.Web/Controllers/JednaniController.cs) – `CanEditMeeting`, `CanEditRecords`, `HasSubsystemLeadPermission`, `CanDeleteMeetings`, `CanCreateMeetings`.
- [ZaznamyController](../../PmTracker.Web/Controllers/ZaznamyController.cs) – `CanEditRecord`, `CanEditSchedule*`, `CanCommentAsSubsystemLead`.
- [ProjectDashboardService.cs](../../PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs) – `CanUserEditProjectVyzvyAsync`, `CanAccessDashboardAsync` (dnes **hardkódované role**, kandidáti na refaktor).
- [VyzvyPanelBuilder](../../PmTracker.Web/Services/ProjectDashboard/VyzvyPanelBuilder.cs) – `MuzeEditovat`, `MuzeZaloztVyzvu`.
- `SettingsAuthzQueries`, `RecordProposalService.CanViewProposalTabAsync`, `CommentAuthorizationPolicy`.

### 4. Service vrstva

- Každé volání `IAuthorizationService.HasPermissionAsync` je na správném klíči.
- `CurrentUserContextViewModel.HasPermission` se volá, kde je kontext k dispozici.
- `VyzvaService.Assignment`, `RecordService.SaveRecord`, `ExterniOdkazController.Sync`, všechny `*Service` s mutacemi.
- **Žádný service-level check, který by se dal obejít** tím, že útočník zavolá jiný endpoint směřující na stejný mutator.

### 5. Data scoping (projections / queries)

- Kde se vrací **seznamy** projektů/záznamů/jednání: filtrace podle `CurrentUserContext.VisibleProjectIds` nebo `HasPermission("projects.read.all")`.
- Globální hledání (`SearchController`) – respektuje viditelnost?
- Export (`ExportController.*`) – pustí uživatele exportovat i záznamy, které normálně nevidí?
- Dashboard `RecordsPanel`, `StatisticsPanel`, `VyzvyPanel`, `NesPanel` – dotazy scopované na projekt, ale může uživatel bez `dashboard.view` získat data jinudy?

### 6. Architecture testy

Rozšířit existující `AuthorizationPolicyEnforcementTests` + `SeedSourceOfTruthTests` o:

- **Parity test**: každá `Can…` boolean property na jakémkoli ViewModelu musí být napojená na konkrétní permission key (whitelisted mapping enforced testem).
- **No-hardcoded-roles test**: zakázat přímé porovnání `role.Kod == "proj_man"` (a podobné) mimo seeder/linker — reflection scan.
- **UI-server mirror test**: pro každý endpoint chráněný policy X existuje aspoň jedna `Can…` property počítaná ze stejného klíče X (detekce endpointů, které UI nikdy neukazuje, a naopak tlačítek bez server enforcementu).
- **Data scope test**: integrační testy projdou `list` endpointy s různými rolemi a ověří, že počet vrácených položek odpovídá oprávněním.

## Dosavadní konkrétní nálezy (startovací body auditu)

Během předběžné inspekce 2026-04-23 identifikovány tyto tři rozpory. **Nejsou to jediné problémy** – slouží jako příklady vzorů, na které si dát pozor při průchodu zbytkem aplikace.

### Nález 1 — Projektový dashboard: tři nekonzistentní vrstvy

| Role | Vidí tlačítko „Dashboard" | Má `dashboard.view` (server pustí) |
|---|---|---|
| SUPERADMIN | ✓ | ✓ |
| APP_ADMIN | ✗ | ✓ |
| READ_ALL | ✗ | ✓ |
| VLASTNIK_PROJEKTU | ✗ | ✓ |
| PROJ_MAN | ✓ | ✓ |
| ADM_PROJ | ✓ | ✓ |
| GEST | ✓ | ✓ |
| HOST | ✗ | ✓ |

Tři nezávislá pravidla:
1. **Seed** ([PermissionSeedConfiguration.cs:134-225](../../PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs)) — `dashboard.view` mají všechny role v pravém sloupci.
2. **Endpoint** ([ProjectDashboardController.cs:87](../../PmTracker.Web/Controllers/ProjectDashboardController.cs)) — `[Authorize(Policy = "permission:dashboard.view")]`.
3. **Tlačítko** ([ProjektyController.cs:116-117](../../PmTracker.Web/Controllers/ProjektyController.cs)) → `CanAccessDashboardAsync` → **hardkódovaný whitelist** `{ PROJ_MAN, ADM_PROJ, GEST }` v [ProjectDashboardAuthorizationPolicy.cs](../../PmTracker.Web/Services/ProjectDashboard/ProjectDashboardAuthorizationPolicy.cs).

### Nález 2 — `VyzvyController`: mutující endpointy bez permission check

[VyzvyController.cs](../../PmTracker.Web/Controllers/VyzvyController.cs) má jen třídní `[Authorize]` (musí být přihlášen).

| Endpoint | Autorizace dnes | Očekávání |
|---|---|---|
| `POST /vyzvy/zalozit` | jen `CanAccessProject` | `vyzvy.create` (chybí v seedu) |
| `POST /vyzvy/zmenit-stav` | jen `CanAccessProject` | `vyzvy.manage` (chybí) |
| `POST /vyzvy/set-zaradid` | **žádný** project-level check | `records.edit` |
| `POST /vyzvy/prerdit` | **žádný** project-level check | `records.edit` |

### Nález 3 — `MuzeEditovat` na panelu Výzvy: hardkódované role mimo seed

[ProjectDashboardService.cs:339-350](../../PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs) testuje kódy rolí `"proj_man"` / `"adm_proj"` přímo proti DB, obchází seed. Důsledek: VLASTNIK_PROJEKTU nemá právo založit výzvu přes UI (pravděpodobně bug), APP_ADMIN dostane pass jen díky explicitní větvi `isSuperOrAppAdmin`.

### Nález 4 — `NavrhyController`: schvalování návrhů jen přes `CanAccessProject` (wave 2 audit, 2026-04-23)

[NavrhyController.cs](../../PmTracker.Web/Controllers/NavrhyController.cs) má 4 mutující akce, všechny chráněné jen přes `CurrentUserContext.CanAccessProject(projektId)` (read-access):

| Endpoint | Server-side check | Očekávání |
|---|---|---|
| `POST /Navrhy/ApproveProposal` (line 142) | `CanAccessProject` | `records.edit` nebo nový `proposals.approve` |
| `POST /Navrhy/RejectProposal` (line 156) | `CanAccessProject` | `records.edit` nebo `proposals.reject` |
| `POST /Navrhy/RejectAndTakeOverCreateProposal` (line 170) | `CanAccessProject` | dtto |
| `POST /Navrhy/RejectAndEditProposal` (line 192) | `CanAccessProject` | dtto |

**Důsledek**: Útočník s nejmenším project access levelem (např. HOST v seznamu účastníků) může schválit nebo zamítnout libovolný návrh v projektu. **Byznysové rozhodnutí potřeba**: vytvořit `proposals.*` keys, nebo přemapovat na existující `records.edit`?

### Nález 5 — `ScheduleController.Recalc`: bez permission policy (wave 2)

[ScheduleController.cs:17](../../PmTracker.Web/Controllers/ScheduleController.cs) má class-level `[Authorize]` (ale bez policy) — endpoint je chráněný na úrovni „musí být přihlášen", ale žádný permission-level check.

- Jakýkoli přihlášený uživatel (včetně HOST / GEST) ho může volat.
- Endpoint je čistá kalkulace (preview harmonogramu, bez DB mutace) + MaxSteps=500.
- Risk: mírný — user bez práv na editaci harmonogramu může zjistit kalkulační logiku; pro GEST/HOST je to technicky in scope „read" operation, ale nebyla explicitně povolena.

**Otázka k rozhodnutí**: má-li být chráněno `records.schedule.add` / `records.schedule.edit` (záleží na kontextu call-site — je Recalc součástí editor workflow?), nebo stačí aktuální autentizované `[Authorize]`?

### Nález 6 — `VyzvyController.SetZaradid` a `Prerdit`: self-identified missing ACL (wave 2 confirmation)

[VyzvyController.cs:78-79](../../PmTracker.Web/Controllers/VyzvyController.cs) sám obsahuje komentář:

> "Základní ACL přes CanAccessProject není triviální (vyžadovalo by načíst projektId přes vazbu), plný role check (proj_man/adm_proj) se provádí v UI (VM.MuzeEditovat) a na straně service."

Aktuální stav:

- `POST /vyzvy/set-zaradid` — server-side **neověřuje nic** o volajícím, jen deleguje na `VyzvaService.NastavitZaradidAsync`.
- `POST /vyzvy/prerdit` — stejně, žádný authz check v kontroléru.

Komentář dokonce říká, že plný check je „na straně service" — pokud ho tam user provedl, mělo by být uvedeno kde. Jinak je to stav, kdy UI gate (`VM.MuzeEditovat`) je jediná obrana. Útočník s přímým POST bypassuje.

**Již pokryto Nálezem 2** v tabulce výše; zde jen potvrzení, že kód to sám ví a je to v tech-debt konceptu.

### Nález 7 — `JednaniController.SaveNotes`: mismatch key (wave 2)

[JednaniController.cs:302-316](../../PmTracker.Web/Controllers/JednaniController.cs) — uložení poznámek jednání je chráněno `PermissionKeys.RecordsEdit`, ale logicky jde o meeting operations (ne record mutation).

**Otázka**: Má být `MeetingsEdit` nebo `RecordsEdit`? Záleží na tom, jestli „poznámky jednání" jsou považovány za součást záznamu, nebo za součást jednání. Vyžaduje byznysové rozhodnutí.

### Nález 8 — `ProjektyController.Commands.SaveProject`: chybí `[Authorize(Policy)]`, jen imperativní (wave 2 observation)

[ProjektyController.Commands.cs:10-32](../../PmTracker.Web/Controllers/ProjektyController.Commands.cs) — akce má imperativní `HasPermission(isCreate ? ProjectsCreate : ProjectsEdit(id))` uvnitř `ExecuteValidatedCommandAsync`. To je **korektní** z pohledu enforcement, ale **nekonzistentní** s ostatními akcemi, které mají explicitní atribut.

**Argument pro imperativní check**: podmíněná authz (create vs edit dle `Id.HasValue`) nelze triviálně vyjádřit jedinou policy; vyžadovalo by custom `IAuthorizationRequirement`.

**Otázka**: má-li se přesto přidat `[Authorize]` (bez policy, jen „musí být přihlášen") jako defense-in-depth, nebo ponechat imperativní design?

**Další akce v ProjektyController.Commands.cs se stejným patternem** (imperativní + bez `[Authorize(Policy)]`): line 54, 128, 140, 149, 161, 173, 185, 197, 209, 221. Všechny mají `ExecuteValidatedCommandAsync(hasPermission: ...)` inline. Pokud je imperativní pattern akceptovaný, pak je to OK; jen se hodí zdokumentovat jako převažující styl pro controllery.

### Nález 9 — `RecordProposalAuthorizationPolicy`: hardkódované role codes v authz policy (wave 2 — KRITICKÉ)

[RecordProposalAuthorizationPolicy.cs:80-82](../../PmTracker.Web/Services/Records/RecordProposalAuthorizationPolicy.cs):

```csharp
return activeProjectRoleCodes.Any(code =>
    string.Equals(code, ProjectRoleCodes.ProjectAdmin, StringComparison.OrdinalIgnoreCase) ||
    string.Equals(code, ProjectRoleCodes.ProjectManager, StringComparison.OrdinalIgnoreCase));
```

Tohle je přesně anti-pattern, který seed-only authz refactor (2026-04-22) měl odstranit. Policy porovnává **kódy rolí** (string literals) místo `AuthorizationSnapshot.HasPermission(key, projectId)`. Důsledek: změna role × permission matice v seedu **nemá efekt** na tuto policy — uživatel s nastaveným `records.edit` permissionem, ale bez role „proj_man"/"adm_proj" na projektu, přes tuto policy neprojde.

**Návrh fixu**: vytvořit `records.proposal.approve` permission key, seednout na odpovídající role, přepsat policy na `HasPermission(RecordProposalApprove, projectId)`.

**Pozor**: tato policy ovlivňuje i [NavrhyController akce](#nález-4--navrhycontroller-schvalování-návrhů-jen-přes-canaccessproject-wave-2-audit-2026-04-23) (Nález 4). Oba nálezy se doplňují — controller má slabou authz a deleguje na policy, která zase má hardcoded roles.

### Nález 10 — VM compute properties `HasHostRole` / `HasNonHostProjectRole` (wave 2 — informativní)

[ProjectService.TeamComposition.cs:107-108](../../PmTracker.Web/Services/ProjectService.TeamComposition.cs) a [MeetingService.DetailQueries.cs:390](../../PmTracker.Web/Services/MeetingService.DetailQueries.cs) vypočítávají ViewModel property přes porovnání s `ProjectRoleCodes.Host`. To **není** authz decision (nepředevídá, jestli uživatel smí co dělat) — jen **klasifikace UI elementu pro display** (barevné odlišení řádku, ikona). Je to legitimní use, ale **hodí se zmínit v auditu**, aby byly potvrzeny jako „ne-authz use" a ne omylem refaktorovány na HasPermission.

### Nález 11 — `MeetingService.WriteCommands.cs:414`: filtr `!= ProjectRoleCodes.Host` v select (wave 2)

[MeetingService.WriteCommands.cs:414](../../PmTracker.Web/Services/MeetingService.WriteCommands.cs) — při sestavení seznamu účastníků jednání se filtrují host-role osoby přímo přes `.Where(x => !Ci.Equals(x.Kod, ProjectRoleCodes.Host))`. Otázka: **má HOST být vyloučen z meeting attendance?** Pokud ano, je to byznysové pravidlo, které by mělo existovat buď jako permission key (`meetings.attend` seednutý bez HOST role), nebo explicitně dokumentované.

### Nález 12 — Komentáře: chybí `comments.edit.any` / `comments.delete.any`, „any" chování je skryté pod `records.edit`

[CommentAuthorizationPolicy.cs:32-43](../../PmTracker.Web/Services/Common/CommentAuthorizationPolicy.cs) realizuje úpravu a smazání komentáře dvouvrstvě:

1. **Policy gate na endpointu** ([ZaznamyController.Commands.cs:104](../../PmTracker.Web/Controllers/ZaznamyController.Commands.cs)): `[Authorize(Policy = "permission:comments.edit.own")]` / `comments.delete.own`.
2. **Service check** (`CommentAuthorizationPolicy.CanModifyComment`):
   ```csharp
   if (HasPermission(records.edit, projektId)) return true;   // ← implicitní "any"
   // jinak jen autor + draft jednání + subsystem lead
   ```

Důsledky:

- Klíč `comments.edit.any` / `comments.delete.any` v seedu **neexistuje**. „Any" oprávnění je implicitně navázané na `records.edit` — což je matoucí sémantika (klíč `records.edit` sémanticky znamená „upravit projektový záznam", ne „upravit cizí komentář").
- Název `comments.edit.own` **lže o sémantice**: prochází policy atribut i pro cizí komentář. Vlastnictví se testuje až ve službě.
- **Anomálie APP_ADMIN**: má `records.edit` (service ho pustí na úpravu čehokoliv), ale **nemá `comments.edit.own`** v seedu → policy gate ho zařízne dřív, než se service vůbec zavolá. Výsledek: APP_ADMIN nemůže upravit ani cizí, ani svůj komentář. Nebyla to evidentní záměrná volba.

**Aktuální výsledná matice (kdo může upravit CIZÍ komentář):** SUPERADMIN, VLASTNIK_PROJEKTU, ADM_PROJ, PROJ_MAN.

**Varianty fixu (k rozhodnutí):**

- **Varianta A — přidat nové klíče** `comments.edit.any` / `comments.delete.any`. Service volá `HasPermission(comments.edit.any, projektId)` místo `records.edit`. Mapping v seedu: SUPERADMIN, APP_ADMIN, VLASTNIK_PROJEKTU, ADM_PROJ, PROJ_MAN. Čisté, jasná sémantika, ale přibudou 2 klíče.
- **Varianta B — přejmenovat existující klíč** z `comments.edit.own` na neutrální `comments.modify` (policy-only gate, službě se ponechá existující logika vlastnictví + `records.edit` backdoor). Méně invazivní, ale zůstane implicitní vazba na `records.edit`.

Doporučení: **Varianta A** kvůli čistotě modelu a správnosti principu single-source-of-truth (každý klíč přesně jedna sémantika).

## Analýza potřebuje (byznysové rozhodnutí uživatele)

Před auditem / implementací musí uživatel rozhodnout role × permission matrix. Otázky k zodpovězení — seznam bude nutně rozšířen, až projdeme celou aplikaci:

### A) Dashboard a jeho záložky

- Kdo vidí tlačítko „Dashboard" v detailu projektu?
- Kdo vidí záložku Záznamy / NES / Statistiky / Výzvy uvnitř dashboardu — jedna policy pro všechny záložky, nebo každá záložka jiná?
- Mají HOST / READ_ALL / VLASTNIK_PROJEKTU / APP_ADMIN vidět dashboard, nebo jen přímým URL?

### B) Výzvy

- Kdo smí **založit** výzvu (`POST /vyzvy/zalozit`)?
- Kdo smí **změnit stav** výzvy?
- Kdo smí **přeřadit / zařadit/vyřadit PNF** (buffer management)?
- Má VEDOUCI_SUBSYSTEMU vidět a spravovat výzvy na svém subsystému?
- Dostatečné rozlišení: `vyzvy.create` vs `vyzvy.manage` vs `vyzvy.assign`, nebo stačí jeden klíč?

### C) Projekty, záznamy, jednání

- Je aktuální sada permission keys (`projects.*`, `records.*`, `meetings.*`, `team.manage`, `people.manage`, …) byznysově kompletní?
- Chybí nějaký key (např. `projects.archive`, `records.reopen`, `meetings.close`, `team.viewroles`)?
- `records.schedule.add` vs `records.schedule.edit` — rozlišení dává smysl? Dnes se na ně spoléhá i UI.

### D) Komentáře a návrhy

- `comments.add` / `comments.edit.own` / `comments.delete.own` — dostatečné? Potřebujeme `comments.edit.any` / `comments.delete.any` pro admina?
- Akce schvalování návrhů (`NavrhyController.ApproveProposal`, `RejectProposal`, `RejectAndTakeOverCreateProposal`, `RejectAndEditProposal`) — které role smí schvalovat návrhy? Dnes nejsou chráněné `[Authorize(Policy)]`.

### E) Export a reporty

- `export.pdf` / `export.word` — má HOST právo exportovat? GEST? READ_ALL?
- Filtrace dat v exportu respektuje oprávnění volajícího?

### F) Nastavení

- `settings.view` vs `settings.manage` — rozdělení je OK? Audit tab („Efektivní práva") má vlastní imperativní check; má dostat vlastní key?
- SD konektor — je OK, že je celý za `settings.manage`?

### G) Data scoping

- Seznam projektů — dnes přes `VisibleProjectIds`. Co přesně do toho set padá? Je to vypočteno konzistentně pro menu / list / search / export?
- Mají subsystémové role vidět seznam projektů, kde nemají žádnou vazbu na svůj subsystém?
- `projects.read.all` — má existovat jen pro READ_ALL, nebo i pro PMO role?

### H) Obecné principy

- Má existovat **read-only varianta** panelů (HOST vidí dashboard bez akcí), nebo úplný cut (HOST nesmí)?
- Deleted/archived projekty — `IsProjectReadOnly`, `PermissionKeys.IsBlockedForDeletedProject` — je seznam zablokovaných klíčů kompletní?

## Postup auditu (navrhovaný)

1. **Fáze 1 — inventura** (bez rozhodnutí uživatele):
   - Vygenerovat tabulku všech `HttpPost/HttpGet/HttpDelete/HttpPut` akcí × jejich aktuální autorizace.
   - Vygenerovat tabulku všech `Can…` properties × jejich výpočet.
   - Identifikovat všechny hardkódované role (grep `proj_man`, `adm_proj`, `ProjectRoleCodes.*` v service vrstvě).
   - Identifikovat všechny UI gate bez odpovídajícího server-side klíče a naopak.
2. **Fáze 2 — byznysové rozhodnutí** (workshop s uživatelem):
   - Projít otázky A–H výše, výsledky zapsat do `docs/authorization.md` jako matici role × permission.
   - Odsouhlasit sadu nových permission keys a jejich mappingy k rolím.
3. **Fáze 3 — implementace**:
   - Přidat nové klíče do [PermissionSeedConfiguration.cs](../../PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs) a `PermissionKeys` konstant.
   - Nahradit hardkódované whitelists za `HasPermission`.
   - Doplnit `[Authorize(Policy)]` atributy.
   - Sjednotit ViewModel `Can…` properties.
   - Rozšířit architecture testy (viz sekce „Architecture testy").
4. **Fáze 4 — verifikace**:
   - Integrační testy pro každou roli × akci (happy + forbidden path).
   - Manuální průchod UI pod různými rolemi.
   - Zkontrolovat, že data scoping respektuje oprávnění.
5. **Fáze 5 — dokumentace**:
   - Aktualizovat [docs/authorization.md](../authorization.md) s výslednou maticí.
   - Přidat sekci „jak přidat nový endpoint" do vývojářské dokumentace.

## Poznámka k rizikům

- **Offline deployment** ([project_offline_deployment_sql_migrations.md](../../memory/project_offline_deployment_sql_migrations.md)): nové permission keys / role přiřazení půjdou přes `db_upgrade_*.sql` skripty plus seeder v kódu. Žádné Aspire / online sync.
- **Zpětná kompatibilita**: po zpřísnění autorizace může část uživatelů ztratit přístup k akcím, které dosud (byť neprávem) měli. Před deployem připravit komunikaci + seznam změn per-role.
- **Architecture tests** musí přibýt **dřív**, než se spustí implementace, aby zajistily, že refaktor nesklouzne zpět k hardkódovaným rolím.
