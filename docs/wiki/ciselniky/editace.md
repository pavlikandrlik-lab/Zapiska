---
title: Editace číselníku
description: Jak změnit hodnotu — kdy lze a kdy ne.
---

# Editace číselníku

## Kdy můžeš editovat

Splnit obě podmínky:

1. Máš permission `ciselniky.edit` (typicky admin)
2. Konkrétní řádek **není systémově uzamčený** (`is_locked=0`)

Pokud chybí permission — **tlačítko *Upravit* je skryté**.
Pokud je řádek uzamčený — tlačítko *Upravit* je viditelné, ale po kliku
zobrazí informativní dialog "tato položka je systémově uzamčená".

## Co znamená "systémově uzamčený"

Hodnotu **používá business logika aplikace**. Příklady:

- **Stav projektu `PLAN`** — aplikace na něm spoléhá při výpočtu novinek
- **Typ záznamu `NES`** — mapuje se na ServiceDesk typ; změna by rozbila
  filtraci NES panelu
- **Role `SuperAdmin`** — má hard-coded význam v authz handler

Změna takových hodnot **z UI by aplikaci rozbila**. Proto jsou uzamčené.

Pokud potřebuješ skutečně změnit uzamčenou hodnotu, musí to udělat developer
přes:

1. Změna v `PermissionSeedConfiguration.cs` nebo migraci
2. Pull request + review
3. Deploy nové verze

## Postup editace (kde lze)

1. Otevři konkrétní číselník
2. Najdi řádek
3. Klik *Upravit* → otevře se modal
4. Změň hodnoty (typicky **Popis** nebo **Pořadí**)
5. Ulož

Po uložení:

- Změna se aplikuje okamžitě
- Audit log zapíše kdo, kdy, co
- Kde se hodnota používá (dropdowns), aktualizuje se při dalším load stránky

## Co lze obvykle měnit

- **Popis** (lokalizovaný název) — bez rizika
- **Pořadí** (sort) — bez rizika
- **Aktivní** (toggle) — pozor, deaktivace skryje hodnotu z dropdownů; pokud
  na ní záznam má vazbu, hodnota se v detailu stále zobrazí (jen ne v new dropdown)

## Co obvykle **NEjde** (i s permission)

- **Změnit Kód** — Kód je business identifier, na kterém spoléhají integrace
  a kód aplikace. Změna by způsobila orphan reference.
- **Smazat řádek** — soft-delete přes deaktivaci. Hard-delete jen přes DB.

## Přidávání nových položek

V aktuální verzi UI **většinou není dostupné**. Důvod: nové hodnoty často
vyžadují i kód změnu (nový case v switch/match). Proto se přidávají přes
deploy / migraci.

Výjimky: některé volné číselníky (např. *Subsystémy projektu* pokud admin
má `ciselniky.subsystem.create`) přidávání mají.

## Audit

Každá editace se zapíše:

- Kdo
- Kdy (timestamp)
- Před → po (které pole, jaká hodnota)

Lze zpětně zjistit "kdo přepsal popis stavu PLAN" — admin diagnostika.

## Související

- [Procházení](prochazeni.md)
- [Pomoc → Řešení problémů](../pomoc/reseni-problemu.md)
