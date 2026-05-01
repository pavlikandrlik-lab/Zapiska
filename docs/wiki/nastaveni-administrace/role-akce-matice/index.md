---
title: Role × akce matice
description: Která role může provést kterou akci. Read-only přehled.
---

# Role × akce — matice

Přehledová tabulka, která vidí na první pohled **kdo může co**. Role v řádcích,
permission keys ve sloupcích, ✓ v buňce = role má klíč.

## K čemu je matice

- **Audit security** — security review může rychle ověřit, že žádná role
  nemá nadbytečná oprávnění
- **Návrh nové role** — admin vidí co existující role obsahují a může
  zformulovat "potřebuju roli mezi HOST a PROJ_MAN"
- **Troubleshooting** — odpovědi na otázky:
  - "Která role má klíč `vyjadreni.reharvest`?"
  - "Existuje role s `dashboard.nes.view` ale bez `dashboard.statistics.view`?"
  - "Co všechno SuperAdmin reálně může?"

## Layout matice

```
                  | projects.view | projects.edit | projects.create | projects.delete | ...
SuperAdmin        |      ✓        |       ✓       |        ✓        |        ✓        | ...
AppAdmin          |      ✓        |       ✓       |        ✓        |        ✓        | ...
VLASTNIK_PROJEKTU     |      ✓        |       ✓       |                 |                 | ...
PROJ_MAN     |      ✓        |               |                 |                 | ...
HOST            |      ✓        |               |                 |                 | ...
```

(Konkrétní obsah vychází z `PermissionSeedConfiguration.cs`.)

## Filtry

Matice je velká (až ~60 klíčů × 6+ rolí). Filtry:

- **Per oblast** — zobrazit jen klíče `projects.*`, jen `dashboard.*`, atd.
- **Per role** — zobrazit jen jednu nebo dvě role (užitečné pro porovnání)
- **Jen rozdíly** — skryje klíče, které mají všechny role stejně (např. všechny
  `view` klíče mají všichni)

## Read-only

Matice **se nemění z UI**. Důvod: matice = `Role × Permission` mapping je
v `PermissionSeedConfiguration.cs`. Změna se dělá:

1. Vývojář upraví seed soubor
2. Pull request s **before / after diff** matice (typicky review by se měl
   dívat na rozdíl)
3. Code review
4. Deploy

Pro audit / kontrolu pomáhá generovat **diff matice** mezi dvěma verzemi —
v repu jsou nástroje pro export do Excelu (`docs/known-issues/authz-target-matrix.xlsx`).

## Vztah k seed konfiguraci

V `PermissionSeedConfiguration.cs`:

```csharp
SeedRole("VLASTNIK_PROJEKTU", new[] {
  "projects.view",
  "projects.edit",
  "records.view",
  "records.edit",
  "records.create",
  "team.member.add",
  // ...
});
```

Při deploy se aplikace:

1. Načte seed
2. Porovná s aktuálním stavem v DB
3. Aplikuje rozdíly (přidá / odebere přiřazení)
4. Audit záznamy o změnách

Po deploy je matice v UI = matice v `PermissionSeedConfiguration.cs`.

## Pro koho

- **Admin** — quick lookup
- **Security auditor** — review oprávnění
- **Architect / lead developer** — návrh nových rolí

## Související

- [Role](../role/) — definice rolí
- [Akce / permissions](../akce-permissions/) — klíče
- [Efektivní práva](../efektivni-prava/) — pro konkrétního usera × projekt
