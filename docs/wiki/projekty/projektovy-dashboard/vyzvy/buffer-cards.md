---
title: Buffer cards
description: Speciální karty pro nezadané výzvy — placeholdery v bufferu.
---

# Buffer cards

Buffer card je **vizuální placeholder karta** v panelu [Výzvy](index.md). Označuje
**slot pro budoucí výzvu**, která ještě nebyla zadaná.

## Účel

Bez buffer cards by panel zobrazoval jen reálné výzvy — ale pro plánování je
užitečné vidět **i prázdné sloty** ("víme že tady má být výzva, ale ještě
není zadaná").

Buffer cards slouží:

- **Vizuální indikace plánu** — kolik výzev má projekt mít
- **Drag-target pro budoucí workflow** (např. plánování, approval workflow)
- **Reminder** — leader vidí "tady ještě jedna chybí"

## Vzhled

Karta s **přerušovaným okrajem**, šedý / průhledný background, ikona "+":

```
┌─ ─ ─ ─ ─ ─ ─ ─ ┐
│                 │
│        +        │
│                 │
│   Slot pro      │
│  budoucí výzvu  │
│                 │
└─ ─ ─ ─ ─ ─ ─ ─ ┘
```

Vizuálně se odlišuje od reálných výzev (které mají **plný okraj** a obsah).

## Konfigurace bufferu

Počet buffer cards typicky **závisí na konfiguraci projektu**:

- **Default** — N buffer cards (např. 3)
- **Per typ záznamu** — pokud projekt plánuje X PMP záznamů, panel ukáže
  X-Y buffer cards (kde Y = počet již vytvořených výzev)
- **Manuální** — leader v editaci projektu řekne "potřebujeme buffer 5
  výzev"

Konkrétní logika je v aplikaci.

## Klik na buffer card

Klik = stejné jako klik na **+ Nová výzva** v hlavičce panelu — otevře modal
[Vytvořit výzvu](vytvorit-vyzvu.md).

Po uložení nové výzvy:

- Buffer card se "změní" v reálnou kartu (vizuálně buffer zmizí, na jeho
  místě je nová výzva)
- Pokud má projekt další buffer cards, zůstávají

## Drag-and-drop

V některých verzích UI lze **přetáhnout záznam projektu na buffer card**:

- User uchopí záznam (z panelu Záznamy přehled)
- Přetáhne na buffer card
- Otevře se modal *Vytvořit výzvu* s **předvyplněnými poli** podle záznamu
  (název, dodavatel)

To šetří čas — leader nemusí opisovat data.

## Permission

Aby user buffer card **viděl**, musí mít `dashboard.vyzvy.view` (jako pro
celý panel). Pro **klik a vytvoření výzvy** musí mít navíc `vyzvy.create`.

Pokud chybí `vyzvy.create`:

- Buffer cards se zobrazí (informační hodnota)
- Klik nic neudělá nebo zobrazí "nemáš oprávnění"

## Vztah ke statistikám projektu

Buffer cards **nezahrnujeme do KPI** — jsou to placeholdery, ne reálné
záznamy. Statistika "počet aktivních výzev" v panelu [Statistiky](../statistiky/)
buffer cards ignoruje.

## Skrytí bufferu

User může v některých verzích UI **schovat buffer cards** (např. když chce
čistý pohled na reálné výzvy bez slotů):

- Toggle *Zobrazit buffer* v hlavičce panelu
- Volba se ukládá do localStorage (per-user, per-projekt)

## Historický kontext

Buffer cards jsou **vizuální vzor** přebraný z agile / kanban metodologií,
kde "buffer slots" reprezentují plánovanou kapacitu. V PM Trackeru je to
adaptovaná verze pro **plánování výzev**.

## Související

- [Výzvy panel](index.md)
- [Vytvořit výzvu](vytvorit-vyzvu.md)
- [Statistiky panel](../statistiky/)
