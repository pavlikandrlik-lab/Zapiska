# PM Tracker - Technická dokumentace 07: Bezpečnost a autorizace

## 1. Účel
Dokument definuje bezpečnostní model aplikace: autentizaci, autorizaci, správu rolí a provozní bezpečnostní pravidla.

## 2. Publikum a role
- Security admin: governance přístupového modelu.
- Aplikační admin: role, akce, scope, superadmin.
- Vývojář: implementace a validace permission pravidel.

## 3. Závislosti a předpoklady
- IIS musí mít Windows Authentication zapnutou.
- Uživatel musí mít odpovídající záznam v `dbo.osoby`.
- `Guid_AD` je technický identifikátor mapování identity.
- AD účty nejsou automaticky synchronizované do `dbo.osoby`; onboarding je explicitně řízen administrací.

## 4. Vstupy a výstupy
### Vstupy
- Tabulky `authz.*`, `dbo.osoby`.
- Permission katalog a authz view modely.

### Výstupy
- Deterministické vyhodnocení práv v runtime.

## 5. Detailní postup
### 5.1 Autentizace
- ASP.NET Core používá `IISDefaults.AuthenticationScheme`.
- Runtime očekává přihlášeného Windows uživatele.
- Uživatel je mapován přes `Guid_AD`.
- Neexistuje automatický sync job, který by AD uživatele zakládal do aplikační DB.
- Pokud `Guid_AD` mapování chybí, autentizovaný AD uživatel nemá v aplikaci identitu ani oprávnění.

### 5.2 Autorizace
- Permission keys jsou serverový source-of-truth.
- Scope model:
  - `GLOBAL` / `PROJECT` na úrovni akce,
  - `ALL` / `INCLUDE` na úrovni role->akce mapování.
- Efektivní práva jsou výsledkem všech aktivních rolí uživatele.

### 5.3 Podporované klíče (per-action redesign 2026-04-23)
Autoritativní zdroj: `PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs` (76 klíčů + seed rolí). Kompletní roadmapa redesignu viz `docs/known-issues/authz-redesign-per-action-keys.md`.

Hlavní kategorie (reprezentativní výběr):
- **Projekty:** `projects.read.all`, `projects.create`, `projects.edit`, `projects.delete`
- **Záznamy:** `records.create`, `records.edit`, `records.delete`, `records.schedule.edit`, `records.assign.meeting`
- **Komentáře / vyjádření:** `comments.add`, `comments.edit.own`, `comments.edit.any`, `comments.delete.own`, `comments.delete.any`, `vyjadreni.modal.open`, `vyjadreni.refresh`, `vyjadreni.vazba.create/delete`, `vyjadreni.reharvest`
- **Jednání:** `meetings.create`, `meetings.edit`, `meetings.delete`, `meetings.status.change`, `meetings.notes.edit`, `meetings.notes.subsystemlead`, `meetings.attendance.edit`, `meetings.participant.add`
- **Návrhy:** `proposals.record.create`, `proposals.schedule.create`, `proposals.edit.own`, `proposals.edit.any`, `proposals.accept`, `proposals.reject`, `proposals.takeover`
- **Tým:** `team.member.add/.remove`, `team.role.assign/.deactivate`, `team.subsystem.create/.deactivate/.reorder`, `team.subsystem.role.assign/.deactivate`, `team.candidates.search`
- **Osoby:** `people.create`, `people.edit`, `people.delete`, `people.ad.search`, `people.ad.sync`
- **Výzvy:** `vyzvy.create`, `vyzvy.state.change`, `vyzvy.pnf.assign`, `vyzvy.pnf.reassign`, `vyzvy.word.export`
- **Dashboard:** `dashboard.view`, `dashboard.records.view`, `dashboard.nes.view`, `dashboard.statistics.view`, `dashboard.vyzvy.view`
- **Export:** `export.pdf.projekt/.jednani/.ukol`, `export.word.projekt/.jednani/.ukol`
- **Nastavení:** `settings.roles.assign`, `settings.sync.configure`, `settings.sync.run`, `settings.sd.view`
- **Číselníky:** `ciselniky.row.edit`, `ciselniky.row.delete`
- **Ostatní:** `search.index`, `search.reindex`, `schedule.preview`, `externiodkazy.sync`

Pre-redesign klíče (`records.schedule.add`, `records.comment.subsystemlead`, `team.manage`, `people.manage`, `ciselniky.edit`, `settings.manage`, `export.pdf`, `export.word`) byly smazány migrací `db_upgrade_1_3_8_authz_per_action_redesign.sql`.

### 5.4 Provozní postup změny oprávnění
1. Založ/aktualizuj akci v `Nastavení -> Akce`.
2. Namapuj akci na roli (`Nastavení -> Role -> Akce`).
3. Přiřaď roli uživateli (`Nastavení -> Uživatel -> Role`).
4. Ověř `Efektivní práva` pro konkrétního uživatele a projekt.
5. Proveď funkční test cílového use-case.

### 5.5 Superadmin governance
- Minimálně 2 aktivní superadmin účty.
- Změny superadminů zapisovat do provozního audit logu.
- Nepoužívat sdílené účty.

## 6. Verifikace
- Ověř route-level i action-level autorizaci na minimálně 2 test účtech.
- Ověř, že systémové role/akce (`is_system=1`) nejsou nechtěně modifikovány.
- Ověř, že uživatel bez role nevidí editční prvky.

## 7. Rollback
- Pokud změna práv rozbije provoz:
  - vrať role/permissions mapování na poslední validní stav,
  - ověř efekt v `Efektivní práva` a reálném UI scénáři.
- Kritický incident: dočasně použij superadmin účet k obnově mapování.

## 8. Troubleshooting
- Uživatel má roli, ale nemá právo:
  - zkontroluj aktivitu role, aktivitu akce, scope mód a include projekty.
- Uživatel se nepřihlásí:
  - zkontroluj `Guid_AD`, IIS auth a doménovou identitu.
  - zkontroluj, že osoba byla v DB skutečně založena (AD samo zápis neprovede).
- Nastavení stránka není vidět:
  - ověř `settings.view` a případně superadmin status.

## 9. Audit a traceability
- Auth bootstrap: `/Users/Pavel.Andrlik/Documents/PM Tracker/PMTracker_insert_sql`
- Permission katalog: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Models/ViewModels/SecurityViewModels.cs`
- Authz persistence: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Services/Data/SqlServerDataStore.cs`
- Nastavení controller: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Controllers/NastaveniController.cs`
