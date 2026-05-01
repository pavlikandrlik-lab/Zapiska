---
title: Export
description: Tisk a exporty — projekty, jednání, úkoly, NES panel. Formáty PDF, Word, Excel.
---

# Export

PM Tracker umí vytisknout / vyexportovat několik typů obsahu. Generuje se
**na vyžádání** (klikem na tlačítko *Tisk* / *Export*) — žádný export běh
na pozadí.

## Co lze exportovat

| Obsah | Formáty | Detail |
|---|---|---|
| **Projekt** (souhrn) | PDF, HTML, Word | [Tisk projektu](tisk-projektu.md) |
| **Jednání** (zápis) | PDF, Word | [Tisk jednání](tisk-jednani.md) |
| **Úkol / záznam** (detail) | PDF, Word | [Tisk úkolu](tisk-ukolu.md) |
| **NES panel** (tickety v prodlení) | Excel (.xlsx) | [Projektový dashboard → NES → Export](../projekty/projektovy-dashboard/nes-v-prodleni/export-excel.md) |

## Volba formátu

V profilu si můžeš nastavit **výchozí formát** pro tlačítka *Tisk*. Pak nemusíš
pokaždé vybírat. Detaily formátů: [PDF versus Word](pdf-vs-word.md).

## Tisk je read-only generování

Export **vytvoří dočasný soubor** a stáhne ti ho. Žádná část obsahu se nemění.
Aplikace si soubor neukládá — je to jen výstup pro tebe.

## Tisk vs. screenshot

Tisk používej pro:

- Distribuci stejného obsahu mezi více lidí (mail, sdílený disk)
- Archivaci pro audit / kontrolu (PDF stabilně reprodukuje obsah)
- Editovatelné kopie (Word — můžeš dál upravit, dopsat podpisy)

Screenshot používej pro:

- Rychlou ilustraci v emailu
- Dokumentaci bug reportu

## Permissions

Tisk obecně **nemá vlastní permission key**. Pokud uvidíš obsah na obrazovce,
můžeš si ho i vytisknout. Výjimka: NES panel Excel export vyžaduje aby měl
projekt propojení na IS (jinak je tlačítko skryté).

## Co tady **nenajdeš**

- **Hromadný export více projektů najednou** — aktuálně není podporovaný.
  Tisk se dělá per-projekt, per-jednání, per-záznam.
- **Naplánovaný export** (denní emailem, atd.) — aplikace nemá schedulery
  pro export.

## Související

- [Profil → Uživatelské nastavení](../profil/nastaveni-uzivatele.md) — výchozí formát tisku
