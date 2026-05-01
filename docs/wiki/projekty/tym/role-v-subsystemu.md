---
title: Role v subsystému
description: Role omezená na konkrétní subsystém (ne celý projekt).
---

# Role v subsystému

Velké projekty mají typicky **specialisty pro jednotlivé subsystémy**. Role
v subsystému umožňuje přiřadit osobě **role omezenou na konkrétní subsystém**
— má daná oprávnění jen na záznamy s tímto subsystémem.

## Proč to existuje

Bez per-subsystém role by:

- Specialista měl plný přístup k celému projektu (nadměrná oprávnění)
- Nebo by neměl žádný přístup (nedostatečná oprávnění)

Per-subsystém role je **kompromis** — má `records.edit` jen na záznamy se
zkratkou subsystému, na ostatní subsystémy projektu nemá editační práva.

## Příklad

```
Projekt:        FIS-EIS Modernizace
Subsystémy:     R_EIS, R_HFU, R_DAN

Marie Svobodová:
  Per-subsystem R_DAN: METODIK_SUBSYSTEMU
    → records.edit jen na záznamy s subsystem='R_DAN'
    → records.edit NEMÁ na záznamy s subsystem='R_HFU' nebo 'R_EIS'
    → records.view má na všechny (default PROJ_MAN basic)
```

## Vztah k projektovým rolím

| Role | Scope | Příklad |
|---|---|---|
| **Globální role** | Napříč všemi projekty | SuperAdmin |
| **Projektová role** | Celý projekt | VLASTNIK_PROJEKTU na FIS-EIS |
| **Role v subsystému** | Subsystém uvnitř projektu | METODIK_SUBSYSTEMU na FIS-EIS / R_DAN |

## Přiřazení role v subsystému

V detailu projektu → tab **Tým** → tlačítko **Přidat roli v subsystému**:

1. Otevře se modal
2. Vyber **subsystém** (z dostupných subsystémů přiřazených k projektu —
   viz [Subsystémy](subsystemy.md))
3. Vyber **osobu** ([AD picker](../../integrace/active-directory/ad-picker.md))
4. Vyber **roli** v subsystému (z dropdownu RoleSubsystemu číselníku)
5. Ulož

## Typické role v subsystému

Konkrétní seznam je v číselníku **RoleSubsystemu**. Orientačně:

| Role | Co umí |
|---|---|
| **METODIK_SUBSYSTEMU** | Read + komentování v subsystému |
| **VEDOUCI_SUBSYSTEMU** | Editace záznamů v subsystému |
| **SubsystemLeader** | Plný management záznamů v subsystému |

## Předpoklad — subsystém musí být přiřazený k projektu

**Než** přiřadíš osobě roli v subsystému, musí být subsystém **přiřazený
k projektu** (viz [Subsystémy](subsystemy.md)). Aplikace tě k tomu vede:

- Pokud projekt nemá žádné přiřazené subsystémy, dropdown subsystémů je
  prázdný
- Tlačítko *Přidat roli v subsystému* může být disabled

## Audit a revoke

Stejně jako u jiných rolí — audit log `SUBSYSTEM_ROLE_ASSIGNED`,
soft-delete při revoke (`SUBSYSTEM_ROLE_REMOVED`).

## Diagnostika

V [Nastavení → Efektivní práva](../../nastaveni-administrace/efektivni-prava/)
admin vybere uživatele × projekt. Pokud má per-subsystem role, zobrazí se:

```
Per-subsystem role:
  ▸ R_DAN (subsystém v FIS-EIS) → METODIK_SUBSYSTEMU

Efektivní permission keys (na záznamech s subsystem='R_DAN'):
  ✓ records.view
  ✓ records.edit
  ✓ comments.create
  ...

Efektivní permission keys (na záznamech bez subsystem='R_DAN'):
  ✓ records.view  (z basic PROJ_MAN role)
  ✗ records.edit  (per-subsystem role neaplikuje)
```

## Permissions

- `team.subsystem.role.assign` — přiřadit roli v subsystému
- `team.subsystem.role.unassign` — odebrat
- `team.view` — vidět (implicitní)

## Související

- [Subsystémy](subsystemy.md) — předpoklad
- [Projektové role](projektove-role.md) — širší scope
- [Efektivní práva](../../nastaveni-administrace/efektivni-prava/) — diagnostika
- [Role × akce matice](../../nastaveni-administrace/role-akce-matice/)
