---
title: Výzvy (panel)
description: Aktivní výzvy projektu — buffer cards, vytvoření výzvy.
---

# Výzvy (panel)

Výzva = **plánovaná akce / dodávka od dodavatele**. Panel **Výzvy** v
projektovém dashboardu ukazuje **aktivní výzvy projektu** jako karty.

## Co je výzva

Výzva je formální **objednávka / požadavek směrem k dodavateli**. Typicky:

- Vytvoření nové funkčnosti / modulu
- Větší úprava existujícího IS
- Externí dodávka

Workflow výzvy:

```
Vytvoření → Schválení → Odeslání dodavateli → Realizace → Akceptace
```

## Layout panelu

Karty (cards) zobrazené ve mřížce:

```
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│ VZ-001       │  │ VZ-002       │  │ + Nová výzva │
│ Daňový modul │  │ Reporting    │  │              │
│ Dodavatel: A │  │ Dodavatel: B │  │ (buffer)     │
│ Termín: 30.4 │  │ Termín: 15.5 │  │              │
│ Stav: Práce  │  │ Stav: Vytv.  │  │              │
└──────────────┘  └──────────────┘  └──────────────┘
```

## Typy karet

### Aktivní výzva

Reálná výzva s vlastními daty:

- **VZ-XXX** — interní ID výzvy
- **Název**
- **Dodavatel**
- **Termín**
- **Stav** (Vytvořena / Schválena / V realizaci / Dodáno / Akceptováno)

### Buffer card

Speciální placeholder karta — **slot pro výzvu která ještě nebyla zadána**.
Detail: [Buffer cards](buffer-cards.md).

## Akce

### + Nová výzva

Tlačítko otevře modal pro vytvoření nové výzvy. Detail:
[Vytvořit výzvu](vytvorit-vyzvu.md).

Permission `vyzvy.create`.

### Klik na kartu

Otevře **detail výzvy** — všechny atributy, dokumenty, změny stavu, audit.

## Vztah k projektu

Výzva je **per-projekt** — patří jednomu konkrétnímu projektu, není sdílená.
Pokud mají dva projekty podobné výzvy, jsou to dvě separátní instance.

## Vztah k záznamům

Výzva může být **vázaná na konkrétní záznam projektu**:

- Záznam = "co máme udělat"
- Výzva = "kdo to dodá a za kolik"

Vazba je obvykle **1:1** — jedna výzva odpovídá jednomu záznamu (typicky PMP).
Někdy 1:N (jedna výzva pokrývá víc záznamů s podobným scope).

## Generování výzvy

Aplikace umí **generovat textovou podobu výzvy** (Word / PDF) z dat:

- Hlavička: jméno projektu, místo plnění (`MistoPlneni`), číslo rámcové
  smlouvy (`CisloRamcoveSmlouvy`) — z metadat projektu
- Tělo: detail výzvy (název, popis, scope)
- Patička: kontakty, podpisové bloky

Generovaný dokument lze pak poslat dodavateli (mail / SD ticket).

## Permission

| Akce | Klíč |
|---|---|
| Vidět panel | `dashboard.vyzvy.view` |
| Vytvořit výzvu | `vyzvy.create` |
| Editovat výzvu | `vyzvy.edit` |
| Schválit výzvu | `vyzvy.approve` |
| Smazat výzvu | `vyzvy.delete` |

## Sub-stránky

- [Vytvořit výzvu](vytvorit-vyzvu.md)
- [Buffer cards](buffer-cards.md)

## Pro koho

- **Vedoucí projektu** (`VLASTNIK_PROJEKTU` / `ADM_PROJ`) — vytváří výzvy, schvaluje
- **Člen projektu** (`PROJ_MAN`) — vidí, případně edituje (dle role)
- **Sponsor** — read-only pohled
- **Dodavatel (externí)** — typicky nevidí PM Tracker, dostane jen vygenerovanou
  výzvu

## Lazy loading

Panel se načítá samostatně přes AJAX. Pokud generování karet selže, panel
zobrazí error placeholder s retry tlačítkem.

## Související

- [Vytvořit výzvu](vytvorit-vyzvu.md)
- [Buffer cards](buffer-cards.md)
- [Upravit projekt](../../upravit-projekt.md) — místo plnění a rámcová smlouva
- [Záznamy](../../zaznamy/) — záznamy vázané na výzvy
