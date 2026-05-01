---
title: Číselníky
description: Referenční data aplikace — stavy, role, kategorie, číselníkové hodnoty.
---

# Číselníky

Číselníky drží **referenční hodnoty** používané napříč aplikací: stavy projektu,
typy záznamů, role, kategorie subsystémů, urgentnost, závažnost a další. Tyto
hodnoty se objevují v dropdownech, filtrech a validacích.

## Pro koho

| Persona | Co může |
|---|---|
| **Běžný uživatel** | Procházet (read-only) |
| **Admin / oprávněná role** | Procházet + editovat (záleží na konkrétním číselníku a permission `ciselniky.edit`) |

## Co je v této sekci

- [Procházení číselníků](prochazeni.md) — jak zobrazit obsah konkrétního číselníku
- [Editace](editace.md) — kdy lze a kdy ne (systémově uzamčené položky)

## Systémové uzamčení

Některé řádky jsou **systémově uzamčené** (`is_locked=1`). Aplikace na nich
spoléhá v business logice — jejich změna z UI by mohla rozbít aplikaci.
Editace takových řádků je možná jen přes deploy (úpravu v kódu / migraci).

Příklady systémově uzamčených hodnot:

- Stavy projektu: PLAN, BEZI, HOTOVO (změna by rozbila workflow)
- Typy záznamů: NES, PMP, PNF (mapuje se na ServiceDesk typy)
- Klíčové role v matici permission keys

## Co tady **nenajdeš**

- **Permission keys** (akce) — to nejsou číselníky v běžném smyslu, jsou
  v kódu. Viz [Akce / permissions](../nastaveni-administrace/akce-permissions/).
- **Role × akce mapování** — vidět v [Nastavení](../nastaveni-administrace/role-akce-matice/).

## Audit

Změny v číselnících se zapisují do **audit logu**. Lze tedy zpětně dohledat,
kdo a kdy hodnotu změnil.
