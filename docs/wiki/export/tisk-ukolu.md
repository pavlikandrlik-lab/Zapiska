---
title: Tisk úkolu / záznamu
description: Detail jednoho záznamu (úkolu) jako PDF nebo Word.
---

# Tisk úkolu / záznamu

Tiskne **detail jednoho záznamu** — pro distribuci, archivaci, předání.

## Kde najít tlačítko

- **Detail záznamu** (modal nebo full-page editor) → tlačítko *Tisk*

## Co je v exportu

### Hlavička

- Typ záznamu (NES / PMP / PNF / úkol)
- Název
- Stav, urgentnost, kategorie
- Vlastník, collaboratoři
- Termíny: plán, dodaný, splněno
- Subsystém

### Externí vazby

Pokud je záznam propojený s SD ticketem:

- 6-ciferné `id` ticketu
- URL na detail v ServiceDesku
- Typ záznamu z SD

### Popis

Plný text popisu (rich-text se zachovaným formátováním).

### Komentáře / vyjádření

Chronologicky:

- Komentáře z PM Trackeru (autor, datum, text)
- Volitelně vyjádření ze SD ticketu (z chat modalu)

### Harmonogram

Pokud má záznam vlastní harmonogram, tabulka kroků s plánem a skutečností.

## Volby exportu

- **Formát** — PDF / Word
- **Včetně komentářů** — ano / ne
- **Včetně SD vyjádření** — ano / ne (může být dlouhé)
- **Včetně harmonogramu** — ano / ne

## Use cases

- Předání záznamu kolegovi mimo PM Tracker (mailem PDF)
- Archivace pro audit (PDF stabilně reprodukuje obsah)
- Editovatelná kopie (Word) pro přepracování do jiného dokumentu
- Vytisknout na papír pro schůzku (PDF)

## Permission

`records.view` na projektu, kde záznam je.

## Související

- [Upravit záznam](../projekty/zaznamy/upravit-zaznam.md)
- [Komentáře](../projekty/zaznamy/komentare.md)
- [Externí vazba na SD](../projekty/zaznamy/externi-vazba-sd.md)
