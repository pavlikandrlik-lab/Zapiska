---
title: Tým
description: Členové projektu, projektové role, subsystémy a role v subsystémech.
---

# Tým projektu

Tým spravuje **kdo má přístup k projektu** a **v jaké roli**. PM Tracker má
**tříúrovňovou** strukturu týmu, abychom dokázali popsat i složité projekty
s více subsystémy a specializovanými reviewer.

## Tři úrovně přiřazení

```
1. Členství v týmu (basic)
   ↓
2. Projektové role (napříč celým projektem)
   ↓
3. Role v subsystému (omezené na konkrétní subsystém)
```

### 1. Členství v týmu (basic)

Osoba je **přidaná do projektu** s default rolí (typicky *PROJ_MAN*).
Tím získává základní přístup — vidět projekt, číst záznamy.

### 2. Projektové role

Osoba má **konkrétní roli na celém projektu** (např. *VLASTNIK_PROJEKTU*). Role
dává sadu permission keys, které platí napříč všemi subsystémy a záznamy
projektu.

### 3. Role v subsystému

Osoba má roli **omezenou jen na konkrétní subsystém**. Příklad: specialista
daní má roli *METODIK_SUBSYSTEMU* na subsystému R_DAN — může editovat záznamy
s `subsystem='R_DAN'`, ale ne s `subsystem='R_HFU'`.

## Layout záložky Tým

V detailu projektu → tab *Tým*:

### Sekce "Členové týmu"

Tabulka osob s:

- DisplayName (z AD)
- Login
- Organizační celek
- Role (badges)
- Akce: *Upravit roli*, *Odebrat z týmu*

### Sekce "Projektové role"

Detail rolí přiřazených na projektu (kdo má roli VLASTNIK_PROJEKTU, kdo Reader,
atd.).

### Sekce "Subsystémy"

Subsystémy projektu s přiřazením jejich vlastníků / reviewerů.

### Tlačítka

- **Přidat člena** (`team.member.add`)
- **Přidat projektovou roli** (`team.role.assign`)
- **Přiřadit subsystém** (`team.subsystem.create`)
- **Přidat roli v subsystému** (`team.subsystem.role.assign`)

## Co je v této sekci wiki

- [Přidat člena](pridat-clena.md) — basic přidání osoby
- [Projektové role](projektove-role.md) — role na celém projektu
- [Subsystémy](subsystemy.md) — rozdělení projektu na podčásti
- [Role v subsystému](role-v-subsystemu.md) — specialisté pro subsystém

## AD picker

Při přidávání člena nebo přiřazování role se používá [AD picker](../../integrace/active-directory/ad-picker.md) —
vyhledá osobu z firemního Active Directory podle jména / loginu / emailu.

Pokud osoba ještě v `dbo.osoby` není, **založí se automaticky** podle GUID z AD.

## Vztah k oprávněním

Tým **definuje permissions na projektu**. Pokud user není v týmu, default
nemá k projektu žádný přístup. Výjimky:

- **SuperAdmin** — má všechno napříč projekty bez nutnosti přiřazení do týmů
- **Globální HOST / READ_ALL** — má `projects.read.all` napříč všemi projekty bez týmu
- Některé organizační scope role

Detail: [Role a oprávnění](../../zacatek/role-a-prava-prehled.md).

## Pro koho

- **Member** — vidí složení týmu (read-only)
- **Leader** — všechny editační akce na svém projektu (per-projekt scope)
- **Admin** — full

## Vazba na ostatní oblasti

- **AD picker** — viz [Active Directory](../../integrace/active-directory/)
- **Role / Permission keys** — viz [Nastavení → Role](../../nastaveni-administrace/role/)
- **Subsystémy** — vázané na číselník subsystémů (případně na `HOT_SUBSYSTEM` v SD)

## Permissions overview

| Akce | Klíč |
|---|---|
| Vidět tým | `team.view` |
| Přidat člena | `team.member.add` |
| Odebrat člena | `team.member.remove` |
| Přiřadit projektovou roli | `team.role.assign` |
| Odebrat projektovou roli | `team.role.unassign` |
| Přiřadit subsystém k projektu | `team.subsystem.create` |
| Odebrat subsystém | `team.subsystem.remove` |
| Přiřadit roli v subsystému | `team.subsystem.role.assign` |
| Odebrat roli v subsystému | `team.subsystem.role.unassign` |

## Související

- [AD picker](../../integrace/active-directory/ad-picker.md)
- [Profil → Moje práva](../../profil/moje-prava.md)
- [Nastavení → Uživatelé × role](../../nastaveni-administrace/uzivatele-role/)
