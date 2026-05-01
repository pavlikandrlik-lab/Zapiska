---
title: Akce / permission keys
description: Granulární klíče oprávnění pro jednotlivé akce v aplikaci.
---

# Akce / permission keys

PM Tracker používá **per-action authorization model** (redesign 2026-04-23).
Každá mutující akce v aplikaci má vlastní stringový **permission key** —
granulární a čitelný identifikátor.

## Konvence pojmenování

```
<doména>.<entita>?.<akce>[.<scope>]
```

Příklady:

- `projects.edit` — `projects` doména, `edit` akce
- `team.subsystem.role.assign` — `team` doména, `subsystem` entita, `role`
  sub-entita, `assign` akce
- `comments.edit.own` — `comments` doména, `edit` akce, `own` scope
- `dashboard.nes.view` — `dashboard` doména, `nes` panel, `view` akce

## Kompletní katalog (76 klíčů ve 15 kategoriích)

Zdroj: `PermissionKeys.cs` + `PermissionSeedConfiguration.cs`.

### 1. Projekty (`PROJECTS`) — 4 klíče

| Klíč | ScopeLevel | Popis |
|---|---|---|
| `projects.read.all` | GLOBAL | Číst všechny projekty (management visibility) |
| `projects.create` | GLOBAL | Vytvářet projekty |
| `projects.edit` | PROJECT | Upravovat metadata a konfiguraci projektu |
| `projects.delete` | PROJECT | Mazat projekty (soft-delete; reverzibilní) |

### 2. Záznamy (`RECORDS`) — 5 klíčů

| Klíč | ScopeLevel | Popis |
|---|---|---|
| `records.create` | PROJECT | Založit nový projektový záznam |
| `records.edit` | PROJECT | Upravit metadata záznamu |
| `records.delete` | PROJECT | Smazat záznam |
| `records.schedule.edit` | PROJECT | Editace všech slotů harmonogramu úkolu |
| `records.assign.meeting` | PROJECT | Doplnit identifikátor jednání k záznamu |

### 3. Komentáře (`COMMENTS`) — 5 klíčů

| Klíč | ScopeLevel | Popis |
|---|---|---|
| `comments.add` | PROJECT | Vkládání nových komentářů k záznamům |
| `comments.edit.own` | PROJECT | Úprava komentářů, které osoba sama vložila |
| `comments.edit.any` | PROJECT | Admin úprava libovolného komentáře |
| `comments.delete.own` | PROJECT | Smazání vlastních komentářů |
| `comments.delete.any` | PROJECT | Admin smazání libovolného komentáře |

### 4. Jednání (`MEETINGS`) — 8 klíčů

| Klíč | ScopeLevel | Popis |
|---|---|---|
| `meetings.create` | PROJECT | Nové jednání v projektu |
| `meetings.edit` | PROJECT | Změna metadat jednání |
| `meetings.delete` | PROJECT | Smazat jednání |
| `meetings.status.change` | PROJECT | Změna stavu jednání (DRAFT/OPEN/CLOSED) |
| `meetings.notes.edit` | PROJECT | Editace poznámek k jednání |
| `meetings.notes.subsystemlead` | PROJECT | Přidat zápis/vyjádření za vedoucího subsystému |
| `meetings.attendance.edit` | PROJECT | Záznamy účasti na jednání |
| `meetings.participant.add` | PROJECT | Registrace účastníka jednání |

### 5. Návrhy (`PROPOSALS`) — 7 klíčů

| Klíč | ScopeLevel | Popis |
|---|---|---|
| `proposals.record.create` | PROJECT | Vytvoření návrhu nového záznamu |
| `proposals.schedule.create` | PROJECT | Vytvoření návrhu úpravy harmonogramu |
| `proposals.edit.own` | PROJECT | Úprava vlastního návrhu před rozhodnutím |
| `proposals.edit.any` | PROJECT | Admin úprava libovolného otevřeného návrhu |
| `proposals.accept` | PROJECT | Schválení návrhu |
| `proposals.reject` | PROJECT | Zamítnutí návrhu |
| `proposals.takeover` | PROJECT | Zamítnutí + převzetí vytvoření záznamu |

### 6. Externí odkazy / vyjádření (`EXTERNI`) — 6 klíčů

| Klíč | ScopeLevel | Popis |
|---|---|---|
| `externiodkazy.sync` | PROJECT | Ruční synchronizace externího odkazu se SD |
| `vyjadreni.modal.open` | PROJECT | Otevření chat modalu s vyjádřeními |
| `vyjadreni.refresh` | PROJECT | Refresh vyjádření z externího zdroje |
| `vyjadreni.vazba.create` | PROJECT | Vytvoření vazby na krok harmonogramu |
| `vyjadreni.vazba.delete` | PROJECT | Smazání vazby na krok harmonogramu |
| `vyjadreni.reharvest` | PROJECT | Admin akce — znovunačíst vyjádření per ticket |

### 7. Tým (`TEAM`) — 10 klíčů

| Klíč | ScopeLevel | Popis |
|---|---|---|
| `team.member.add` | PROJECT | Přidání osoby do týmu projektu |
| `team.member.remove` | PROJECT | Odebrání osoby z týmu |
| `team.role.assign` | PROJECT | Přiřazení role na úrovni projektu |
| `team.role.deactivate` | PROJECT | Deaktivace role na úrovni projektu |
| `team.subsystem.create` | PROJECT | Přiřadit subsystém k projektu |
| `team.subsystem.reorder` | PROJECT | Změna pořadí subsystémů projektu |
| `team.subsystem.deactivate` | PROJECT | Deaktivace subsystému |
| `team.subsystem.role.assign` | PROJECT | Přiřazení role na úrovni subsystému |
| `team.subsystem.role.deactivate` | PROJECT | Deaktivace role subsystému |
| `team.candidates.search` | PROJECT | Fulltextové vyhledávání osob do týmu |

### 8. Osoby (`PEOPLE`) — 5 klíčů

| Klíč | ScopeLevel | Popis |
|---|---|---|
| `people.create` | GLOBAL | Založení nové osoby |
| `people.edit` | GLOBAL | Úprava osoby a jejích atributů |
| `people.delete` | GLOBAL | Odstranění osoby |
| `people.ad.search` | GLOBAL | Vyhledávání osob v AD |
| `people.ad.sync` | GLOBAL | Synchronizace osoby ze záznamu v AD |

### 9. Číselníky (`CISELNIKY`) — 2 klíče

| Klíč | ScopeLevel | Popis |
|---|---|---|
| `ciselniky.row.edit` | GLOBAL | Úprava / uložení řádku číselníku |
| `ciselniky.row.delete` | GLOBAL | Smazání řádku číselníku |

### 10. Výzvy (`VYZVY`) — 5 klíčů

| Klíč | ScopeLevel | Popis |
|---|---|---|
| `vyzvy.create` | PROJECT | Založení výzvy z bufferu |
| `vyzvy.state.change` | PROJECT | Změna stavu výzvy |
| `vyzvy.pnf.assign` | PROJECT | Zařadit / vyřadit PNF do bufferu |
| `vyzvy.pnf.reassign` | PROJECT | Přeřadit PNF mezi výzvami |
| `vyzvy.word.export` | PROJECT | Stáhnout Word export výzvy (budoucí feature) |

### 11. Dashboard (`DASHBOARD`) — 5 klíčů

| Klíč | ScopeLevel | Popis |
|---|---|---|
| `dashboard.view` | PROJECT | Vstup na dashboard projektu |
| `dashboard.records.view` | PROJECT | Panel Záznamy v dashboardu |
| `dashboard.nes.view` | PROJECT | Panel NES v prodlení |
| `dashboard.statistics.view` | PROJECT | Panel Statistiky |
| `dashboard.vyzvy.view` | PROJECT | Panel Výzvy v dashboardu |

### 12. Export (`EXPORT`) — 6 klíčů

| Klíč | ScopeLevel | Popis |
|---|---|---|
| `export.pdf.projekt` | PROJECT | Tisk projektu do PDF |
| `export.pdf.jednani` | PROJECT | Tisk jednání do PDF |
| `export.pdf.ukol` | PROJECT | Tisk úkolu do PDF |
| `export.word.projekt` | PROJECT | Word export projektu |
| `export.word.jednani` | PROJECT | Word export jednání |
| `export.word.ukol` | PROJECT | Word export úkolu |

### 13. Nastavení (`SETTINGS`) — 5 klíčů

| Klíč | ScopeLevel | Popis |
|---|---|---|
| `settings.view` | GLOBAL | Vstup do sekce Nastavení |
| `settings.roles.assign` | GLOBAL | Přiřazení globální role uživateli |
| `settings.sync.configure` | GLOBAL | Úprava konfigurace synchronizačního jobu |
| `settings.sync.run` | GLOBAL | Manuální spuštění synchronizačního jobu |
| `settings.sd.view` | GLOBAL | Otevřít SD konektor s admin přehledem |

### 14. Hledání (`SEARCH`) — 2 klíče

| Klíč | ScopeLevel | Popis |
|---|---|---|
| `search.index` | GLOBAL | Globální fulltext hledání |
| `search.reindex` | GLOBAL | Administrátorská akce: full reindex FTS |

### 15. Harmonogram preview (`SCHEDULE`) — 1 klíč

| Klíč | ScopeLevel | Popis |
|---|---|---|
| `schedule.preview` | PROJECT | Stateless kalkulace pro editor úkolu |

## Granularita

Příklad: pro **komentáře** máme **5 klíčů** rozdělené podle scope:

```
comments.add           — kdokoli s tímto klíčem může přidat komentář
comments.edit.own      — editovat jen svůj komentář
comments.edit.any      — editovat libovolný (admin)
comments.delete.own    — smazat svůj
comments.delete.any    — smazat libovolný (admin)
```

Tím lze role granulárně škálovat — `comments.add + comments.edit.own + comments.delete.own`
je default member; `comments.edit.any + comments.delete.any` je admin doplněk.

## ScopeMode v role × klíč mapping

Každé přiřazení role × klíč má `ScopeMode`:

| ScopeMode | Význam | Příklad |
|---|---|---|
| `All` | Všechny entity v rozsahu role (99 % mappingů) | `(ADM_PROJ, records.edit, All)` |
| `Include` | Jen vyjmenované entity | `(GEST, projects.edit, Include)` s konkrétními IDs |
| `Own` | Jen entity vlastněné osobou | `(PROJ_MAN, comments.edit.own, Own)` |
| `Subsystem` | Jen entity v subsystému osoby | `(VEDOUCI_SUBSYSTEMU, records.edit, Subsystem)` |

## PermissionScopeLevel — Global vs Project

Klíč má:

- **Global** — nepotřebuje projektId při kontrole. Příklad: `people.*`,
  `settings.*`, `ciselniky.*`
- **Project** — potřebuje projektId. Příklad: `projects.edit`, `records.edit`

## Aplikace klíče v controllerech

```csharp
[Authorize(Policy = "permission:projects.edit")]
public async Task<IActionResult> EditProject(int id) { ... }
```

`PermissionAuthorizationHandler` čte permission keys uživatele z
`AuthorizationSnapshot` a porovná je s policy.

## Per-projekt scope příklad

```
User:    Marie Svobodová
Role:    ADM_PROJ na projektu FIS-EIS
Klíč:    projects.edit (ScopeLevel=PROJECT, ScopeMode=All)

→ Marie má projects.edit JEN na FIS-EIS
→ Na ostatních projektech tlačítko *Upravit projekt* nevidí
```

## Read-only z UI

Klíče se z UI **nemění**. Změna definice klíče = pull request + deploy.

## Detail klíče v UI

V Nastavení → Akce → klik na řádek otevře detail:

- Popis klíče
- Které role ho obsahují (s ScopeMode)
- Kteří uživatelé ho efektivně mají
- Kde v aplikaci se kontroluje (controllery / akce)

## Pro koho

Admin / SuperAdmin. Vidět všechny klíče potřebuje typicky jen security audit.

## Související

- [Role](../role/) — 11 sad klíčů
- [Role × akce matice](../role-akce-matice/) — kompletní mapping
- [Efektivní práva](../efektivni-prava/) — diagnostika konkrétního usera
