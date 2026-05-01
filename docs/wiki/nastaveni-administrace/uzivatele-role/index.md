---
title: Uživatelé × role
description: Které role má konkrétní uživatel — globálně nebo na konkrétním projektu.
---

# Uživatelé × role

Sekce ukazuje **přiřazení rolí ke konkrétním uživatelům**. Pro každého
uživatele lze vidět:

- Jaké má **globální role** (platí napříč všemi projekty)
- Jaké má **per-projekt role** (platí jen na konkrétním projektu)
- Případný **organizační scope**

## Layout

### Hlavní view

Tabulka uživatelů s sloupci:

- Login (DOMÉNA\login)
- DisplayName (z AD)
- Organizační celek
- Počet rolí (globálních + per-projekt)
- Počet projektů (kolik projektů má přístup)
- Aktivní (`is_active=1`)

### Detail uživatele

Klik na řádek → detail:

```
Jan Novák (jan.novak@acr)
Org. celek: IT Oddělení / Sekce Aplikací

Globální role:
  ▸ HOST

Per-projekt role:
  ▸ FIS-EIS Modernizace          → VLASTNIK_PROJEKTU
  ▸ ISSP Refresh                 → PROJ_MAN
  ▸ R_DAN (subsystém v FIS-EIS)  → METODIK_SUBSYSTEMU

Efektivní permission keys:
  [Otevřít diagnostiku v Efektivních právech]
```

## Akce

### Přiřadit roli (per-projekt scope)

Tlačítko *Přiřadit roli* otevře modal:

- Vyber **roli** (z dropdownu)
- Vyber **scope**: globální / konkrétní projekt
- Volitelně subsystém (pokud role to podporuje)
- Ulož

Po uložení audit log + okamžitý effect (uživatel hned uvidí nové permissions).

### Odebrat roli

Tlačítko *Odebrat* u řádku přiřazení. Soft-delete v DB — historický audit
zůstává.

### Aktivace / deaktivace uživatele

Toggle *Aktivní*. Deaktivovaný user **se nemůže přihlásit**, ale data zůstávají.

## Editovatelné z UI

V této sekci lze:

- ✓ Přiřazovat / odebírat **role uživateli** (instance, ne definice)
- ✓ Aktivovat / deaktivovat usera
- ✗ **Definovat** novou roli (to se dělá v kódu)
- ✗ Měnit které klíče role obsahuje (kód)

## Permission keys pro tuto sekci

- `users.view` — vidět tabulku uživatelů
- `users.role.assign` — přiřazovat / odebírat role
- `users.deactivate` — deaktivovat usera

## Hromadné akce

V aktuální verzi UI jsou hromadné akce (multi-select) **omezené**. Typicky se
přiřazení dělají jeden po druhém. Pro masové změny (např. po reorganizaci
celého oddělení) se používá **migrační skript**.

## Audit

Každé přiřazení / odebrání role se loguje:

```
TYPE: USER_ROLE_ASSIGNED
USER: jan.novak
ROLE: VLASTNIK_PROJEKTU
SCOPE: project=FIS-EIS
ACTOR: admin (kdo to udělal)
TIMESTAMP: 2026-04-25T10:15:30Z
```

Lze zpětně dohledat "kdo, kdy, komu jakou roli dal".

## Synchronizace s `dbo.osoby_role`

Tabulka `osoby_role` v DB drží trojice **osoba × role × scope**. Aplikace
při kontrole oprávnění čte z této tabulky. Sekce *Uživatelé × role* je
read/write UI nad touto tabulkou.

## Pro koho

- **SuperAdmin** — full
- **AppAdmin** — typicky may, někdy s omezením (nemůže přiřadit SuperAdmin
  roli)

## Související

- [Role](../role/)
- [Efektivní práva](../efektivni-prava/) — diagnostika výsledku přiřazení
- [Osoby → Přiřazení k právům](../../osoby/prirazeni-prav.md)
