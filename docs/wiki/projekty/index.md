---
title: Projekty
description: Centrální oblast aplikace — projektový management, záznamy, jednání, tým, harmonogram a projektový dashboard.
---

# Projekty

**Projekt je centrální entita PM Trackeru**. Všechny aktivity (záznamy, jednání,
úkoly, výzvy, dodávky) existují v kontextu konkrétního projektu. Když chceš
něco evidovat nebo plánovat, vždycky to děláš "v rámci projektu X".

## Přehled stránky **Projekty**

Hlavní seznam projektů s filtrací:

- **Skrýt dokončené** (toggle, default zapnutý) — projekty ve stavu *Hotovo*
  nebo *Archivováno* se nezobrazí
- **Skrýt smazané** (toggle, default zapnutý) — soft-deleted projekty se
  nezobrazí

Primární akce v přehledu:

- **Nový projekt** — viz [Nový projekt](novy-projekt.md)
- **Upravit** u řádku — viz [Upravit projekt](upravit-projekt.md)
- **Smazat** u řádku — viz [Smazat projekt](smazat-projekt.md)

Každý řádek je odkaz na **detail projektu**.

## Detail projektu

Detail je rozdělen do **pěti tabů**:

| Tab | Co obsahuje | Detail |
|---|---|---|
| **Záznamy** | Úkoly, NES/PMP/PNF, evidence | [Záznamy](zaznamy/) |
| **Jednání** | Porady, zápisy, účastníci | [Jednání](jednani/) |
| **Tým** | Členové, role, subsystémy | [Tým](tym/) |
| **Harmonogram** | Plán a skutečnost kroků | [Harmonogram](harmonogram/) |
| **Návrhy** | Návrhy změn plánu | [Návrhy](navrhy/) |

Vedle tabů je odkaz na **[Projektový dashboard](projektovy-dashboard/)** —
samostatná stránka s agregovanými metrikami.

## Persistence aktivního tabu

Pokud opustíš detail (např. otevřít editor záznamu) a vrátíš se, aplikace
**zapamatuje aktivní tab**. Default je *Záznamy*.

## Sekce wiki

- [Záznamy](zaznamy/) — úkoly a tickety
- [Jednání](jednani/) — porady a zápisy
- [Tým](tym/) — členové a role
- [Harmonogram](harmonogram/) — plán a skutečnost
- [Návrhy](navrhy/) — workflow změn plánu
- [Projektový dashboard](projektovy-dashboard/) — agregované metriky

## Stránky o projektu samotném

- [Nový projekt](novy-projekt.md)
- [Upravit projekt](upravit-projekt.md)
- [Smazat projekt](smazat-projekt.md)
- [Přehled projektu (detail)](prehled-projektu.md)

## Pro koho je sekce

- **Project leader** — celá sekce; hlavně harmonogram, jednání, výzvy
- **Project member** — Záznamy + Komentáře; čtení Týmu a Harmonogramu
- **Admin** — všechno + management projektu (vytvoření, smazání, propojení na IS)

## Související

- [Integrace → ServiceDesk](../integrace/servicedesk/) — propojení projektu na IS, externí vazby
- [Nastavení → Uživatelé × role](../nastaveni-administrace/uzivatele-role/) — kdo má přístup k projektům
