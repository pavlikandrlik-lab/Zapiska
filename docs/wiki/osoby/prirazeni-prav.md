---
title: Přiřazení osob k právům
description: Jak je osoba propojena s rolemi a projekty.
---

# Přiřazení osob k právům

PM Tracker pracuje s tříúrovňovou strukturou:

```
Osoba ─── má ───▶ Role ─── platí na ───▶ Projekt
                  (sada permission keys)   (scope)
```

## Vztah Osoba ↔ Role ↔ Projekt

### Osoba

Záznam v `dbo.osoby`, vázaný na AD `Guid_AD`. Identifikuje konkrétního uživatele.

### Role

Logická skupina permission keys (např. *Vlastník projektu*, *Host*). Definovaná
v kódu (`PermissionSeedConfiguration.cs`), aplikuje se při deploy.

### Projekt (jako scope)

Role lze přiřadit:

- **Globálně** — platí napříč všemi projekty
- **Per-projekt** — platí jen na konkrétním projektu

## Příklady přiřazení

### Globální admin

```
Osoba: Petr Novák
Role: SuperAdmin
Scope: globální (= bez omezení)
```

Petr má všechny permission keys napříč všemi projekty.

### Project leader na konkrétním projektu

```
Osoba: Marie Svobodová
Role: VLASTNIK_PROJEKTU
Scope: projekt FIS-EIS (id=42)
```

Marie má klíče pro management projektu (`projects.edit`, `team.member.add`, …),
ale **pouze** na projektu FIS-EIS. Na ostatních projektech ji to chování
neaplikuje.

### Member na všech projektech

```
Osoba: Jan Dvořák
Role: PROJ_MAN
Scope: globální
```

Jan má základní pracovní klíče (`records.create`, `records.edit`, `meetings.view`)
napříč všemi projekty. To je typicky pro běžné členy týmu.

### Member jen na vybraných projektech

```
Osoba: Eva Černá
Role 1: PROJ_MAN
Scope: FIS-EIS

Role 2: PROJ_MAN
Scope: ISSP-Refresh
```

Eva je member ve dvou projektech, na ostatních nemá přístup.

## Sloučení rolí

Pokud má osoba **více rolí**, jejich permission keys se **sjednocují**:

```
Osoba: Karel Bílý
Role 1: HOST (globální)        → records.view, projects.view
Role 2: PROJ_MAN (FIS-EIS)  → +records.edit, +records.create

Efektivní:
- na FIS-EIS: records.view, records.edit, records.create, projects.view
- na ostatních projektech: records.view, projects.view (jen z HOST)
```

## Kde se to nastavuje

| Akce | Kde | Kdo |
|---|---|---|
| Přiřadit osobě roli (per-projekt scope) | [Nastavení → Uživatelé × role](../nastaveni-administrace/uzivatele-role/) | Admin |
| Definovat role / klíče | `PermissionSeedConfiguration.cs` | Vývojář (pull request + deploy) |
| Diagnostika konkrétní osoby × projekt | [Nastavení → Efektivní práva](../nastaveni-administrace/efektivni-prava/) | Admin |
| Vidět vlastní práva | [Profil → Moje práva](../profil/moje-prava.md) | Uživatel sám |

## Read-only z UI

Aplikace přiřazení v UI **nedělá editovatelné** většinou — záleží na konkrétní
verzi. Default workflow:

1. Admin požádá vývojáře o úpravu seed
2. Vývojář otevře PR
3. Po review nasazení proběhne na produkci

## Kdy se osoba zakládá

Osoba se vytvoří v `dbo.osoby` v jednom z těchto okamžiků:

1. **Manuálně adminem** (přes DB nebo migrační skript)
2. **Při prvním přiřazení do týmu** přes [AD picker](../integrace/active-directory/ad-picker.md) —
   pokud osoba ještě v `osoby` není, aplikace ji založí podle GUID z AD

## Související

- [Profil → Moje práva](../profil/moje-prava.md)
- [Nastavení a administrace](../nastaveni-administrace/)
- [AD picker](../integrace/active-directory/ad-picker.md)
