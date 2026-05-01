---
title: Projektový dashboard
description: Agregovaný pohled na projekt — KPI, statistiky, NES v prodlení, výzvy.
---

# Projektový dashboard

**Samostatná stránka** přístupná z detailu projektu. Ukazuje **agregované
metriky** napříč záznamy, harmonogramem, výzvami. Slouží pro:

- **Project leader** — denní pohled na stav projektu
- **Sponsor / management** — zhuštěný report bez nutnosti procházet detaily
- **Audit / kontrola** — přehled tiketů v prodlení, statistik

## Vztah k tabu *Záznamy*

| Co | Záložka *Záznamy* | Projektový dashboard |
|---|---|---|
| Účel | Pracovní seznam | Agregovaný report |
| Editace | Ano (otevřít editor) | Ne (read-only metriky) |
| Granularita | Per řádek | Skupiny / KPI |
| Filtry | Detailní (typ, stav, vlastník) | Vysoké úrovně (rok, kvartal) |

## Layout dashboard stránky

Dashboard má **4 hlavní panely** v dvousloupcovém / responsivním layoutu:

```
┌─────────────────────────┬─────────────────────────┐
│ Záznamy přehled         │ Statistiky              │
│ (klíčové záznamy podle  │ (KPI: počty po          │
│ priority / stavu)       │ kvartálech, subsystémy) │
├─────────────────────────┼─────────────────────────┤
│ NES v prodlení          │ Výzvy                   │
│ (tickety NES s          │ (aktivní výzvy + buffer │
│ překročeným termínem)   │ cards)                  │
└─────────────────────────┴─────────────────────────┘
```

## Sub-stránky této sekce

- [Záznamy přehled](zaznamy-prehled/) — `_RecordsPanel.cshtml`
- [Statistiky](statistiky/) — `_StatisticsPanel.cshtml` (KPI, kvartály, subsystémy)
- [NES v prodlení](nes-v-prodleni/) — `_NesPanel.cshtml` (SD-závislé)
- [Výzvy](vyzvy/) — `_VyzvyPanel.cshtml`

## Per-permission gating

Každý panel má **vlastní permission key**:

| Panel | Klíč pro zobrazení |
|---|---|
| Celá stránka | `dashboard.view` |
| Záznamy panel | `dashboard.records.view` |
| Statistiky panel | `dashboard.statistics.view` |
| NES panel | `dashboard.nes.view` |
| Výzvy panel | `dashboard.vyzvy.view` |

Pokud uživatel nemá konkrétní klíč, **panel se vůbec nerenderuje**. Layout se
přizpůsobí (zbylé panely roztáhnou prostor).

Příklad: user s `dashboard.view + dashboard.records.view + dashboard.statistics.view`
ale bez NES a Vyzvy klíčů uvidí jen levou stranu (Záznamy + Statistiky).

## Lazy loading panelů

Každý panel se **načítá samostatně** přes AJAX po načtení hlavní stránky.
Důvod:

- **Rychlejší TTFP** (time to first paint) — uživatel hned vidí layout
- **Nezávislost selhání** — pokud SD nefunguje, NES panel selže ale ostatní
  fungují
- **Per-panel retry** — když některý panel selže, user může klikem retry
  bez reloadu celé stránky

## Vztah k SD

**NES panel** vyžaduje **propojení projektu na IS** (FIS / ISSP):

- Pokud projekt **nemá vazbu**, panel zobrazí *graceful state* s odkazem
  do edit projektu
- Pokud projekt **má vazbu**, panel ukazuje skutečné NES tickety filtrované
  na daný IS

Detail: [Propojení projektu na IS](../../integrace/servicedesk/propojeni-projekt-is.md).

## Cache a refresh

Dashboard data se **cachují** pro performance:

- **KPI** — recomputed periodicky (typicky nightly + reaktivně po změnách)
- **NES tickety** — z poslední SD harvest sync
- **Záznamy panel** — z aktuální DB

Klikem na ikonku *Refresh* (pokud existuje) lze vyvolat ad-hoc refresh.

## Pro koho

- **Member** — vidí panely podle permission keys
- **Leader / Admin** — typicky všechno

## Související

- [Hlavní detail projektu](../prehled-projektu.md)
- [Záznamy](../zaznamy/) — pracovní seznam (komplementární k panelu)
- [Harmonogram](../harmonogram/) — detailní pohled na plán
- [Integrace → ServiceDesk](../../integrace/servicedesk/) — backend pro NES panel
