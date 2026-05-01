---
title: Upravit projekt
description: Změna metadat projektu — název, stav, IS, místo plnění.
---

# Upravit projekt

## Předpoklady

- Permission `projects.edit` na konkrétním projektu (per-projekt scope)

## Postup

Dvě cesty k editoru:

### Z přehledu projektů

Hlavní stránka *Projekty* → u řádku tlačítko **Upravit** → otevře se modal.

### Z detailu projektu

Detail projektu → tlačítko **Upravit** v hlavičce → modal.

## Pole formuláře

Stejná jako u [Nový projekt](novy-projekt.md). Plus:

- **Informační systém** — můžeš:
  - **Přidat** propojení (změnit z *— bez napojení —* na FIS / ISSP)
  - **Změnit** propojení (FIS ↔ ISSP)
  - **Odstranit** propojení (vrátit na *— bez napojení —*)

## Co po uložení nevyžaduje akci

- **Změna názvu / zkratky** — propíše se okamžitě napříč UI
- **Změna stavu** — funkční chování projektu se nemění (např. změna na
  `DOKONCEN` pouze přesune projekt do archivního filtru, nezavře záznamy)

## Co změna IS **NEpřepočítává automaticky**

⚠️ **Důležité — změna IS je pasivní:**

- Existující **externí vazby na záznamech** zůstávají na původních ticketech
  (i kdyby byly z původního IS, ne z nového)
- Existující **harmonogram** (skutečnost) se nepřepočítá — auto-fill bude
  postupně doplňovat nové hodnoty z nového IS, staré zůstanou
- **NES panel projektu** se okamžitě přepíná na tickety nového IS

Pokud chceš úplnou re-sync s novým IS:

1. Změň propojení
2. Spusť ručně **SD harvest** (Nastavení → Synchronizace)
3. Případně otevři chat modaly relevantních záznamů a zkontroluj že vyjádření
   sedí

Pro většinu use-case ale stačí "změnit IS a počkat na další periodický sync".

## Audit

Změna se zaloguje:

```
TYPE: PROJECT_UPDATED
PROJECT: 42
BEFORE: { "ServiceDeskInfoSystemId": null, ... }
AFTER:  { "ServiceDeskInfoSystemId": 1, ... }
ACTOR: jan.novak
```

## Co tady **NEzměníš**

- **Vlastníka projektu / leadera** — to se nastavuje v záložce *Tým*
- **Subsystémy** — záložka *Tým*
- **Členy projektu** — záložka *Tým*
- **Harmonogram** — záložka *Harmonogram*
- **Záznamy** — záložka *Záznamy*

Modal *Upravit projekt* je čistě pro **metadata projektu** — ne pro jeho
obsah.

## Související

- [Nový projekt](novy-projekt.md)
- [Propojení projektu na IS](../integrace/servicedesk/propojeni-projekt-is.md)
- [Tým](tym/)
