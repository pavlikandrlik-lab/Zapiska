---
title: Přehled projektu (detail)
description: Detail projektu — top-level layout a navigace mezi taby.
---

# Přehled projektu (detail stránka)

Detail projektu je **hlavní pracovní stránka** uvnitř projektu. Všechny aktivity
(záznamy, jednání, tým, harmonogram, návrhy) se odehrávají tady, v
příslušných tabech.

## Layout stránky

### Hlavička

- **Název projektu** + zkratka
- **Stav** (badge — PLAN / BEZI / DOKONCEN / ARCHIV)
- **IS** (pokud je propojení FIS / ISSP)
- **Akce**: tlačítka *Upravit*, *Smazat*, *Tisk*

### Boční panel / horní link

- Odkaz na **Projektový dashboard** — samostatná stránka s metrikami a
  agregovanými panely

### Hlavní obsah — taby

Detail je rozdělen do **pěti tabů**:

| Tab | URL fragment | Obsah |
|---|---|---|
| **Záznamy** | `?tab=zaznamy` (default) | Seznam záznamů projektu |
| **Jednání** | `?tab=jednani` | Seznam jednání |
| **Tým** | `?tab=tym` | Členové, role, subsystémy |
| **Harmonogram** | `?tab=harmonogram` | Plán a skutečnost kroků |
| **Návrhy** | `?tab=navrhy` | Návrhy změn plánu |

## Persistence aktivního tabu

Když opustíš detail (např. otevřít editor záznamu) a vrátíš se, aplikace
**zapamatuje aktivní tab**:

- Otevřeš detail projektu na default tabu *Záznamy*
- Klikneš *Tab Harmonogram* → vidíš harmonogram
- Klikneš na editaci kroku → otevře se editor
- Po uložení se vrátíš → **zase tab Harmonogram** (ne default Záznamy)

Persistence je v `sessionStorage`, expiruje při zavření browseru.

## Sub-stránky této wiki sekce

- [Záznamy](zaznamy/) — úkoly, NES/PMP/PNF, evidence
- [Jednání](jednani/) — porady, zápisy, účastníci
- [Tým](tym/) — členové, role, subsystémy
- [Harmonogram](harmonogram/) — plán a skutečnost
- [Návrhy](navrhy/) — workflow změn plánu
- [Projektový dashboard](projektovy-dashboard/) — agregované metriky

## Permission gates per tab

Každý tab má vlastní permission key:

| Tab | Klíč pro vidět tab | Klíče pro akce |
|---|---|---|
| Záznamy | `records.view` | `records.create`, `records.edit`, `records.delete` |
| Jednání | `meetings.view` | `meetings.create`, `meetings.edit`, `meetings.reopen` |
| Tým | `team.view` | `team.member.add`, `team.role.assign`, `team.subsystem.create` |
| Harmonogram | `harmonogram.view` | `harmonogram.toggle-rezim`, `harmonogram.edit` |
| Návrhy | `proposals.view` | `proposals.create`, `proposals.approve` |

Pokud nemáš `view` klíč pro konkrétní tab, **tab se v UI vůbec nezobrazí**.

## Soft-deleted projekt

Pokud máš otevřený detail [smazaného projektu](smazat-projekt.md):

- Hlavička obsahuje badge "**SMAZÁNO**"
- Většina akcí je disabled (read-only)
- Tlačítko *Obnovit* (pokud máš permission)

## Pro koho

- **Člen projektu** (`PROJ_MAN`) — denní práce
- **Vedoucí projektu** (`VLASTNIK_PROJEKTU` / `ADM_PROJ`) — full management
- **HOST** — typicky vidí jen tab Záznamy + Jednání (pokud má `view` klíče)

## Související

- [Hlavní přehled Projektů](index.md)
- [Projektový dashboard](projektovy-dashboard/) — agregovaný pohled
- [Záznamy](zaznamy/), [Jednání](jednani/), [Tým](tym/), [Harmonogram](harmonogram/), [Návrhy](navrhy/)
