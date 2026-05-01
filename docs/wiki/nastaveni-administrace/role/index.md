---
title: Role
description: Přehled rolí definovaných v PM Trackeru.
---

# Role

PM Tracker definuje **role v kódu aplikace** (`PermissionSeedConfiguration.cs`).
Role je sada permission keys (akcí) — uživatel přiřazený k roli získává
všechny její klíče.

## 11 systémových rolí

PM Tracker má 11 rolí ve třech kategoriích podle **RoleScope**:

### Globální (3) — `RoleScope.Global`

Přiřazují se přes `authz.user_roles` (přímo na osobu).

| Kód | Název | Účel |
|---|---|---|
| `SUPERADMIN` | Superadmin | Pevná role s plnými oprávněními |
| `APP_ADMIN` | Administrátor aplikace | Správa aplikace a základních entit |
| `READ_ALL` | Read-all (management visibility) | Read-only přístup ke všem projektům a jejich datům |

### Projektové (5) — `RoleScope.Project`

Přiřazují se přes `ObsazeniProjektu` (na konkrétní projekt). Použité v
`ciselnik_roli_projektu`.

| Kód | Název | Účel |
|---|---|---|
| `VLASTNIK_PROJEKTU` | Vlastník projektu | Plný vlastník projektu |
| `ADM_PROJ` | Projektový admin | Silný projektový admin bez práva měnit metadata |
| `PROJ_MAN` | Projektový manažer | Projektový manažer bez úprav metadat |
| `HOST` | Host | Read-only host projektu |
| `GEST` | Gestor | Gestor s komentovacími právy |

### Subsystémové (3) — `RoleScope.Subsystem`

Přiřazují se přes `ObsazeniSubsystemuProjektu` (na konkrétní subsystém uvnitř
projektu). Použité v `ciselnik_roli_subsystemu`.

| Kód | Název | Účel |
|---|---|---|
| `VEDOUCI_SUBSYSTEMU` | Vedoucí subsystému | Vedoucí subsystému projektu |
| `ZASTUPCE_VEDOUCIHO_SUBSYSTEMU` | Zástupce vedoucího subsystému | Zástupce vedoucího, stejná práva jako vedoucí |
| `METODIK_SUBSYSTEMU` | Metodik subsystému | Metodik s komentovacími právy |

## Sekce v UI

V Nastavení → Role uvidíš tabulku všech rolí s:

- **Kód role**
- **Popis** (z `Nazev` v seedu)
- **Aktivní** (toggle — neaktivní role se neaplikuje, ale data zůstávají)
- **Počet klíčů** v roli
- **Počet uživatelů** kteří roli mají

Klik na řádek → detail role:

- Plný seznam permission keys obsažených v roli (s `ScopeMode`)
- Seznam uživatelů s touto rolí (per-projekt scope vidět)
- Audit záznamy o změnách

## Read-only z UI

**Role definice (kód + klíče) se z UI nemění.** Důvod:

- Role mají **business význam** — jejich definice je součást aplikační logiky
- Změna z UI by způsobila **drift mezi DB a kódem** — aplikace by se chovala
  jinak ve dev a v produkci
- Audit / kontrola změn je čistší přes pull request + code review

Změna definice role:

1. Vývojář upraví `PermissionSeedConfiguration.cs`
2. Pull request + review (four-eyes)
3. Deploy nové verze
4. Při startu aplikace `PermissionSeeder.SeedAsync` aplikuje seed — DB se
   synchronizuje s kódem

## Co lze udělat z UI

**Přiřazení role uživateli** — viz [Uživatelé × role](../uzivatele-role/).
To je editovatelné (per-projekt scope, deaktivace přiřazení).

## Vztah role × subsystém

Subsystémové role (`VEDOUCI_SUBSYSTEMU`, `METODIK_SUBSYSTEMU`, atd.) jsou
**dál omezené na konkrétní subsystém uvnitř projektu**:

- Globální / per-projekt scope: role platí na celý projekt
- Per-subsystém scope: role platí jen na daný subsystém

Detail v [Efektivní práva](../efektivni-prava/).

## Pro koho

Admin / SuperAdmin. Pro běžného uživatele neviditelná sekce.

## Související

- [Akce / permissions](../akce-permissions/) — co je permission key (76 klíčů)
- [Role × akce matice](../role-akce-matice/) — která role co umí
- [Uživatelé × role](../uzivatele-role/) — přiřazování
- [Efektivní práva](../efektivni-prava/) — diagnostika
