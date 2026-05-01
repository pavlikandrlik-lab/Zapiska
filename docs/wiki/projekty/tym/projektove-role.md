---
title: Projektové role
description: Role v rámci celého projektu — leader, member, reviewer, atd.
---

# Projektové role

Projektová role je **přiřazená osobě v kontextu konkrétního projektu** a dává
sadu permission keys na tomto projektu. Liší se od:

- **Globální role** — platí napříč všemi projekty
- **Role v subsystému** — omezená na subsystém uvnitř projektu

## Projektové role v PM Trackeru

Konkrétní seznam je v `ciselnik_roli_projektu` (seedovaný z
`PermissionSeedConfiguration.cs`):

| Kód role | Název | Co umí |
|---|---|---|
| `VLASTNIK_PROJEKTU` | Vlastník projektu | Plný vlastník — full management projektu |
| `ADM_PROJ` | Projektový admin | Silný projektový admin bez práva měnit metadata |
| `PROJ_MAN` | Projektový manažer | Projektový manažer bez úprav metadat |
| `HOST` | Host | Read-only host projektu |
| `GEST` | Gestor | Gestor s komentovacími právy |

Specifický mapping role → permission keys vidíš v
[Role × akce matice](../../nastaveni-administrace/role-akce-matice/).

## Přiřazení projektové role

V detailu projektu → tab **Tým** → tlačítko **Přidat projektovou roli**:

1. Otevře se modal s [AD pickerem](../../integrace/active-directory/ad-picker.md)
2. Vyhledej osobu
3. Vyber roli z dropdownu (RoleProjektu číselníku)
4. Ulož

Permission `team.role.assign`.

## Více rolí na osobu

Osoba **může mít víc rolí** na stejném projektu. Permission keys se
**sjednocují**:

```
Marie Svobodová na projektu FIS-EIS:
  - PROJ_MAN (přidaná na začátku)
  - ADM_PROJ (přidaná po promotion)
  - HOST (na ostatních projektech, kde není v týmu)

Efektivní permission keys na FIS-EIS = sjednocení PROJ_MAN + ADM_PROJ
```

V UI vidíš všechny role jako badges u jejího jména.

## Odebrat roli

U řádku osoby → konkrétní role → tlačítko **Odebrat**:

1. Confirm dialog
2. Soft-delete v `osoby_role`
3. Audit log: `PROJECT_ROLE_REMOVED`

Permission `team.role.unassign`.

## Vztah k basic membership

[Přidat člena](pridat-clena.md) = basic membership s default rolí (typicky
PROJ_MAN).

[Přidat projektovou roli](projektove-role.md) = **dodatečná role** k existující
membership, nebo přiřazení role osobě která ještě v týmu není.

Workflow má dvě varianty:

### Varianta 1: nejdřív basic, pak dodatečné

1. Přidat Jana jako PROJ_MAN (basic)
2. Přidat Janovi další roli `ADM_PROJ` (dodatečná, dává víc oprávnění)

### Varianta 2: rovnou role assign

1. Přiřadit Janovi roli `VLASTNIK_PROJEKTU`
2. Aplikace **automaticky vytvoří basic membership** s default rolí
3. Plus přiřadí `VLASTNIK_PROJEKTU`

Druhá varianta je rychlejší — není nutné dvě akce.

## Per-projekt scope

Role jsou **per-projekt**. Stejná osoba může mít různé role na různých
projektech:

```
Honza Novák:
  Projekt FIS-EIS:    VLASTNIK_PROJEKTU
  Projekt ISSP:       PROJ_MAN
  Projekt PNF-Tools:  HOST
```

To je řízeno scope sloupcem v `osoby_role`.

## Vztah ke globálním rolím

Globální role (např. SuperAdmin) **nemusí být přiřazená per-projekt** — platí
napříč všemi. Některé role mohou existovat jen jako globální (admin role
nemá smysl per-projekt).

Detail: [Role a oprávnění](../../zacatek/role-a-prava-prehled.md).

## Permissions

- `team.role.assign` — přiřadit projektovou roli
- `team.role.unassign` — odebrat
- `team.view` — vidět seznam (implicitní)

## Související

- [Přidat člena](pridat-clena.md) — basic membership
- [Subsystémy](subsystemy.md) — ještě užší scope
- [Role v subsystému](role-v-subsystemu.md)
- [Role × akce matice](../../nastaveni-administrace/role-akce-matice/)
