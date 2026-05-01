---
title: NES v prodlení (panel)
description: Tickety NES s překročeným termínem ze ServiceDesku, filtrované na IS projektu.
---

# NES v prodlení (panel)

Panel **NES v prodlení** ukazuje **NES tickety ze ServiceDesku, které mají
překročený termín dodání**, filtrované na informační systém daného projektu.
Slouží:

- **Vedoucí projektu** (`VLASTNIK_PROJEKTU` / `ADM_PROJ`) — denní přehled "co je urgentní u dodavatele"
- **Sponsor** — reporting kvality dodávek
- **Auditor** — kontrola SLA

## Předpoklad — projekt musí mít propojení na IS

Panel vyžaduje **propojení projektu na IS** (FIS / ISSP). Pokud projekt nemá
vazbu na IS, panel zobrazí **graceful state**:

```
ℹ️ Projekt nemá napojení na informační systém ve ServiceDesku.

Pro zobrazení NES tiketů v prodlení nastavte propojení v
[Editaci projektu] na FIS nebo ISSP.
```

Detail: [Propojení projektu na IS](../../../integrace/servicedesk/propojeni-projekt-is.md).

## Co panel ukazuje

### KPI v hlavičce

```
V prodlení: 5 ticketů
Průměr dnů: 12.3
```

### Tabulka ticketů

Pro každý NES ticket v prodlení daného IS:

| Sloupec | Popis |
|---|---|
| **Ticket ID** | 6-ciferné `id` z `HOT_ZAZNAMY` (klikací odkaz do SD) |
| **Pid** | Pid záznamu (např. `A111111`) |
| **Typ** | Vždy NES |
| **Stručně** | Krátký popis ticketu |
| **Dodavatel** | Z `HOT_ZAZNAMY.dodavatel` |
| **Termín** | Plánovaný termín dodání |
| **Dní v prodlení** | Počet dní od překročení termínu |
| **Stav** | Aktuální SD stav (otevřeno, dodavatel, atd.) |

Default řazení: **nejdéle prodlené nahoře**.

### Akce v hlavičce

- **Export do Excelu** — `.xlsx` se všemi řádky panelu

## Filtrace

Default filtry — viz [Filtrace](filtrace.md):

- IS = projektový IS (z `dbo.projekty.ServiceDeskInfoSystemId`)
- Typ = NES
- Stav = aktivní (ne uzavřené / archivované)
- Termín = překročený proti dnešku

User v aktuální verzi **nemá další filtrace** — pro detail je nutné jít do
ServiceDesku přímo.

## Lazy loading

Panel se načítá samostatně přes AJAX. Pokud SD je nedostupný, panel
zobrazí:

```
⚠️ Nepodařilo se načíst data ze ServiceDesku.
[Zkusit znovu]
```

Klik *Zkusit znovu* spustí re-load.

## Cache a freshness

- Data **z poslední SD harvest sync**
- Periodický harvest: typicky každých 30 minut (aktivní)
- Manuální refresh přes admin operaci v [/SDConnector](../../../integrace/servicedesk/sd-konektor-diagnostika.md)

Pokud user chce **opravdu live data**, musí jít do ServiceDesku přímo.
PM Tracker zobrazuje **mírně zpožděný snímek**.

## Sub-stránky

- [Filtrace](filtrace.md) — co určuje které tickety se zobrazí
- [Export do Excelu](export-excel.md) — `.xlsx` výstup

## Permission

`dashboard.nes.view`. Bez toho se panel **nerenderuje**.

## Pro koho

- **Vedoucí projektu** (`VLASTNIK_PROJEKTU` / `ADM_PROJ`) — primární uživatel
- **Sponsor / Management** — reporting
- **Auditor** — kontrola SLA dodavatelů

## Vazba na chat modal

Klik na řádek ticketu **otevře chat modal** v PM Trackeru:

- Vidíš historii vyjádření z SD
- Můžeš [propojit vyjádření s krokem harmonogramu](../../../integrace/servicedesk/chat-vyjadreni.md)

Pro plný kontext ticketu (vlastní formy, přílohy, atd.) klikni na **Ticket ID**
— odkaz vede do **detailu v ServiceDesku** v novém tabu.

## Související

- [Propojení projektu na IS](../../../integrace/servicedesk/propojeni-projekt-is.md)
- [Tickety PMP/PNF/NES](../../../integrace/servicedesk/tickety-pmp-pnf-nes.md)
- [Chat vyjádření](../../../integrace/servicedesk/chat-vyjadreni.md)
- [SD harvest sync](../../../nastaveni-administrace/synchronizace/sd-harvest.md)
