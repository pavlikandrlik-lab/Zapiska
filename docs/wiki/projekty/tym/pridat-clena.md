---
title: Přidat člena týmu
description: Postup přiřazení osoby do projektového týmu.
---

# Přidat člena týmu

## Předpoklady

- Permission `team.member.add` na projektu

## Postup

1. Detail projektu → tab **Tým** → tlačítko **Přidat člena**
2. Otevře se modal s [AD pickerem](../../integrace/active-directory/ad-picker.md)
3. Vyhledej osobu (jméno / login / email)
4. Vyber **roli** v rámci týmu (z dropdownu RoleProjektu číselníku)
5. Volitelně: vyplň **roli v jednání** (pokud projekt používá)
6. Ulož

## Co se stane na pozadí

### 1. Lookup v `dbo.osoby`

Aplikace najde osobu v `dbo.osoby` podle `Guid_AD` z AD pickeru:

- **Pokud existuje** → použije existující záznam
- **Pokud neexistuje** → **založí ji** podle GUID + atributů z AD (jméno,
  organizace, email)

To je hlavní cesta jak se osoby do `dbo.osoby` dostávají.

### 2. Vytvoří se přiřazení

V `osoby_role` se vytvoří záznam:

```
osoba_id    = (id z dbo.osoby)
role_kod    = (vybraná role)
projekt_id  = (aktuální projekt)
scope       = "project"
created_at  = (now)
created_by  = (admin který přidává)
```

Tím osoba získává **permission keys** dané role na tomto projektu.

### 3. Audit

Audit log: `TEAM_MEMBER_ADDED`:

```
ACTOR: admin.login
TARGET: jan.novak
PROJECT: 42 (FIS-EIS)
ROLE: PROJ_MAN
TIMESTAMP: ...
```

## Default role

Default role pro "běžného přidaného člena" je typicky **PROJ_MAN**.
Lze ji změnit na něco jiného z dropdownu (HOST, ADM_PROJ, VLASTNIK_PROJEKTU, atd.).

## Více rolí najednou

Pokud potřebuješ osobu přiřadit s **více rolemi** (např. PROJ_MAN +
ADM_PROJ), musíš to udělat **dvěma akcemi**:

1. Přidat člena s rolí PROJ_MAN (basic membership)
2. Přidat projektovou roli ADM_PROJ

Permission keys se sjednotí.

## Externí osoba (mimo organizaci)

V některých scenarios potřebuješ přiřadit **externí osobu** (klient,
dodavatel) která nemá AD účet:

- AD picker ji **nevrátí** — není v AD
- Aplikace ji **nemůže založit** v `dbo.osoby` (chybí Guid_AD)
- **Workaround**: vytvořit "fake" záznam v `dbo.osoby` přímo v DB s
  null `Guid_AD` a přiřadit ji rolí HOST

Tento workaround je **outside scope běžné UI** — dělá ho admin přes migraci
nebo SQL skript.

## Odebrat člena

V tabulce týmu → u řádku tlačítko **Odebrat z týmu**:

1. Confirm dialog
2. Soft-delete v `osoby_role` (záznam zůstane v DB jako historický)
3. Audit log: `TEAM_MEMBER_REMOVED`

Po odebrání osoba ztratí permission keys plynoucí z této role na tomto projektu.
Ostatní role (globální nebo jiné projekty) zůstávají.

## Není to to samé jako "přiřazení projektové role"

[Přidat člena](pridat-clena.md) = **basic membership** v týmu (typicky default
role).

[Projektové role](projektove-role.md) = **dodatečné role** s konkrétními
permission keys (VLASTNIK_PROJEKTU, ADM_PROJ, atd.).

User může být:

- Jen **člen** (basic, default PROJ_MAN)
- Člen **+ projektové role** (VLASTNIK_PROJEKTU)
- Člen **+ role v subsystému** (METODIK_SUBSYSTEMU pro R_DAN)
- Kombinace všech

## Permissions

- `team.view` — vidět tým
- `team.member.add` — přidat člena
- `team.member.remove` — odebrat člena

## Související

- [Projektové role](projektove-role.md)
- [Subsystémy](subsystemy.md)
- [Role v subsystému](role-v-subsystemu.md)
- [AD picker](../../integrace/active-directory/ad-picker.md)
