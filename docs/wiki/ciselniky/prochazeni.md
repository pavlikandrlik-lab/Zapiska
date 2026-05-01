---
title: Procházení číselníků
description: Jak zobrazíš obsah konkrétního číselníku.
---

# Procházení číselníků

## Hlavní stránka číselníků

Hlavní stránka *Číselníky* zobrazí **seznam dostupných číselníků** v aplikaci.
Každý řádek je jeden číselník (např. *Stavy projektu*, *Typy záznamů*,
*Role projektu*, *Subsystémy*, *Urgentnosti*, …).

Klik na řádek → otevře se detail konkrétního číselníku.

## Detail číselníku

V detailu vidíš:

| Sloupec | Co znamená |
|---|---|
| **Kód** | Unikátní identifikátor hodnoty (např. `PLAN`, `BEZI`, `HOTOVO`) |
| **Popis** | Human-readable název (např. "Plánovaný", "Běží", "Hotovo") |
| **Pořadí** | Pořadí v UI dropdownech (sort key) |
| **Aktivní** | Je položka v UI vidět? Neaktivní zůstávají v DB ale skrývají se z dropdownů |
| **Uzamčený** | Systémově uzamčený řádek — viz [Editace](editace.md) |
| **Akce** | Tlačítko *Upravit* (pokud máš oprávnění) |

## Filtrace v číselníku

Některé číselníky mají filtr "Skrýt neaktivní" — default zapnutý. Pro
zobrazení neaktivních (např. historických hodnot, které se kdysi používaly)
ho vypni.

## Vyhledávání

Pokud má číselník hodně řádků, máš nahoře vyhledávací pole. Funguje na
**Kód** i **Popis**.

## Audit

Každý detail má sekci **Audit** s historií změn:

- Kdo a kdy upravil hodnotu
- Co konkrétně se změnilo (před / po)

Audit se loguje automaticky, nelze ho z UI vypnout.

## Read-only stav

Pokud nemáš `ciselniky.edit`, vidíš detail **read-only**:

- Tlačítka *Upravit* jsou skryté
- Hodnoty jsou jen ke čtení

Detail je informativní — uvidíš všechny hodnoty, ale nezměníš.

## Související

- [Editace](editace.md) — jak změnit hodnotu, kdy lze
