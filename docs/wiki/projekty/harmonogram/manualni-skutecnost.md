---
title: Manuální skutečnost a režim Auto/Ručně
description: Jak ručně zadat skutečnost kroku, co dělá přepínač Automatické vyplňování harmonogramu a které kroky jsou vždy ruční.
---

# Manuální skutečnost — automatické a ruční vyplňování

Skutečnost kroku harmonogramu je datum, kdy krok opravdu skončil. Vyplňuje ji buď
**automat** z vyjádření v ServiceDesku, nebo **uživatel**. Nic mezi: když je automatika
zapnutá, vyplňuje kroky automat; když je vypnutá, vyplňujete je vy.

## Kroky, které jsou vždy ruční — 2, 5, 8 a 9

Kroky **2, 5, 8 a 9** v ServiceDesku žádné vyjádření nemají, automat je proto nikdy
nevyplňuje. Jejich skutečnost zadáváte vždy ručně, ať je přepínač v jakékoli poloze.
Přepínač je nemění.

## Přepínač „Automatické vyplňování harmonogramu“

Přepínač je v editoru záznamu vpravo v řádku záložek a platí pro **celý záznam** —
pro kroky **1, 3, 4, 6, 7 a 10**. Změna se projeví až uložením záznamu.

### Zapnuto — vyplňuje automat (výchozí stav)

- Automat bere skutečnost z vyjádření navázaných na externí vazby záznamu a přepisuje ji,
  když dorazí novější vyjádření.
- U kroku je bublina s datem vyjádření a ikonou, která otevře okno tiketu.
- Když automat zatím vhodné vyjádření nenašel, je v buňce „—“ a varovná ikona.

### Vypnutí — vyplňujete vy

- Kroky 1, 3, 4, 6, 7 a 10 si po uložení **ponechají stávající data** (i ta od automatu)
  a jdou upravit. Nic se nesmaže.
- Automat je od té chvíle nepřepisuje.

### Zapnutí — data převezme automat

- Uložením se **smažou všechna data kroků 1, 3, 4, 6, 7 a 10, která nedodal automat**
  — ručně zadaná (✍️) i historická (📜).
- Automat je hned doplní z vyjádření, která už jsou na záznamu navázaná, a dál z nových.
- Krok, ke kterému žádné vyjádření není, zůstane prázdný.

## Zadání, změna a smazání data

V ručním režimu (a u kroků 2, 5, 8 a 9 vždy) je ve sloupci Skutečnost pole s kalendářem.

- Pole je předvyplněné aktuální skutečností, ať pochází odkudkoli.
- Datum jde **kdykoli změnit i smazat** (křížek u pole). Uloží se jen kroky, na které jste
  sáhli — ostatní si ponechají hodnotu i zdroj.
- Skutečnost kroku nesmí být dříve než skutečnost předchozího kroku. Kontrolují se jen
  hodnoty, které jste v tomto uložení změnili, takže stará data uložení neblokují.

## Zdroj skutečnosti

Ikona vedle hodnoty říká, odkud datum pochází:

| Ikona | Zdroj |
|---|---|
| 🤖 | Automat — z vyjádření v ServiceDesku |
| ✍️ | Ručně zadané uživatelem |
| 📜 | Historická data převedená před zavedením automatu |
| — | Bez hodnoty |

## Výběr jiného vyjádření (dropdown ▼)

Když má krok 1, 3, 4, 6, 7 nebo 10 v automatickém režimu **víc vhodných vyjádření**,
nabídne buňka šipku ▼ se seznamem kandidátů. Výběrem určíte, ze kterého vyjádření
automat datum bere — krok zůstává v automatickém režimu. U kroků 2, 5, 8 a 9 ani
v ručním režimu se dropdown nenabízí.

## Vztah k návrhům

Čeká-li na záznamu [návrh změny termínu a harmonogramu](../navrhy/navrhnout-zmenu-planu.md),
je harmonogram do rozhodnutí uzamčený — přepínač ani skutečnost nejdou měnit přímo.
Schválení zapíše skutečnost stejnými pravidly jako uložení editoru.

## Oprávnění

| Akce | Klíč |
|---|---|
| Upravit skutečnost a přepínač při úpravě záznamu | `records.edit` |
| Upravit jen harmonogram (záložka Harmonogram) | `records.schedule.edit` |
| Vybrat jiné vyjádření z dropdownu | `records.schedule.edit` |

## Související

- [Auto-fill ze SD](auto-fill-ze-sd.md)
- [Plán versus skutečnost](plan-vs-skutecnost.md)
- [Návrhy](../navrhy/)
- [Chat vyjádření](../../integrace/servicedesk/chat-vyjadreni.md) — odkud vyjádření přicházejí
