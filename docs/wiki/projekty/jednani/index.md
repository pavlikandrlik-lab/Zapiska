---
title: Jednání
description: Porady projektu — zápisy, účastníci, identifikátory, uzavírání.
---

# Jednání

Jednání jsou **strukturované porady projektu**. Každá porada se eviduje jako
samostatný záznam s číslem, datem, místem, účastníky a zápisem (body programu /
relevantní záznamy projektu).

## Layout záložky Jednání

V detailu projektu → tab *Jednání*:

### Filtry

- **Stav** (otevřené / uzavřené / všechny)
- **Rok** (group-by rok pro starší jednání)
- **Skrýt smazané** (toggle)

### Seznam jednání

Default řazení = **nejnovější nahoře**. Sloupce:

- **Číslo jednání** (per-projekt unikátní)
- **Datum + čas**
- **Místo**
- **Stav** (otevřené / uzavřené)
- **Počet účastníků**
- **Počet záznamů z jednání**

Klik na řádek → detail jednání.

### Akce

- **Nové jednání** (pokud máš `meetings.create`)

## Detail jednání

Detail obsahuje:

- **Hlavička** — číslo, datum, čas, místo, stav
- **Účastníci** — pozváni / přítomni / omluveni
- **Záznamy z jednání** — seznam záznamů projektu, které byly diskutovány
- **Vyjádření** — komentáře z jednání
- **Akce**: *Upravit*, *Uzavřít*, *Tisk*

## Co je v této sekci wiki

- [Nové jednání](nove-jednani.md) — jak založit
- [Identifikátor jednání](identifikator-jednani.md) — formát čísla, toggle "8201"
- [Účastníci](ucastnici.md) — pozvání, evidence účasti
- [Uzavřené jednání](uzavrene-jednani.md) — read-only chování po uzavření

## Vztah k záznamům

Záznam projektu může být **přiřazený k jednání**. Vznikne to typicky:

- **Při zápisu z jednání** — během porady leader vytvoří nové záznamy a
  označí je vazbou na aktuální jednání
- **Manuálně** — později přiřadíš záznam k jednání ve kterém se diskutoval
  (i zpětně)

V detailu záznamu pak vidíš sekci *Vázáno na jednání: 8201/2026-04-25*.

## Komentáře / vyjádření

Komentář k záznamu může mít volitelně vazbu na konkrétní jednání. V exportu
zápisu z jednání pak komentáře vystoupí pod správnými body programu.

## Pro koho

- **Member** — vidí jednání, přidává záznamy z jednání
- **Leader** — vytváří, uzavírá jednání
- **Admin** — full

## Permissions

- `meetings.view` — vidět tab Jednání
- `meetings.create` — vytvořit nové
- `meetings.edit` — upravit
- `meetings.attendance.set` — nastavit svoji účast
- `meetings.reopen` — znovu otevřít uzavřené (typicky leader nebo admin)

## Související

- [Záznamy](../zaznamy/) — entity, které mohou být vázané na jednání
- [Tisk jednání](../../export/tisk-jednani.md)
