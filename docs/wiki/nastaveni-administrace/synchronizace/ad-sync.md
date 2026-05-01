---
title: AD sync
description: Synchronizace atributů osob z Active Directory do dbo.osoby.
---

# AD sync

`AdPeriodicSyncHostedService` periodicky čte z Active Directory atributy osob
v `dbo.osoby` a aktualizuje je. Pro **podrobný popis principu** viz
[Synchronizace osob — Integrace](../../integrace/active-directory/synchronizace-osob.md).

Tato stránka popisuje **jak job spravovat z admin UI**.

## Co stránka ukazuje

### Stav posledního běhu

```
Poslední běh:    2026-04-25 06:00:12 UTC
Doba trvání:     12.3 sekundy
Stav:            ✓ úspěšný
Zpracováno:      342 osob
Aktualizováno:   8 (atribut se změnil)
Bez změny:       320 (atribut se neměnil)
Přeskočeno:      14 (lookup selhal)
Chyby:           0
```

### Příští běh

```
Plánováno na:   2026-04-25 12:00:00 UTC
(za 4 hodiny 23 minut)
```

### Posledních N běhů

Tabulka historie:

| Čas | Trvání | Stav | Zpracováno | Chyby |
|---|---|---|---|---|
| 2026-04-25 06:00 | 12s | ✓ | 342 | 0 |
| 2026-04-25 00:00 | 11s | ✓ | 342 | 0 |
| 2026-04-24 18:00 | 13s | ✓ | 342 | 0 |
| 2026-04-24 12:00 | 8s | ⚠️ | 342 | 2 |
| ... | | | | |

## Akce

### Spustit teď

Tlačítko **Spustit teď** triggers immediate run:

1. Confirm dialog ("Opravdu spustit ad-hoc?")
2. Po potvrzení start běhu
3. Spinner během běhu (typicky < minuta)
4. Po dokončení refresh stránky s novými statistikami

### Zobrazit detail běhu

Klik na řádek v historii → modal s detaily:

- Plný log běhu
- Seznam zpracovaných osob (login + status)
- Seznam chyb (pokud nějaké)

### Stop / pauza

V aktuální verzi UI **nelze trvale zastavit** AD sync. Pokud potřebuješ
("AD je v údržbě, nech sync vypnutý"), musí se to udělat:

- Změnou intervalu na velkou hodnotu (např. 99999 minut) v `appsettings.json`
- Restart app pool

## Konfigurace

```json
"PmTracker": {
  "Sync": {
    "AdSyncIntervalMinutes": 360,
    "AdSyncQuietHourStart": "22:00",
    "AdSyncQuietHourEnd": "06:00"
  }
}
```

| Klíč | Default | Účel |
|---|---|---|
| `AdSyncIntervalMinutes` | 360 (6 hodin) | Periodicita běhu |
| `AdSyncQuietHourStart` | 22:00 | Začátek "tiché doby" (nespouštět) |
| `AdSyncQuietHourEnd` | 06:00 | Konec tiché doby |

Tichá doba je užitečná pro:

- AD backup window (nespouštět zátěž v noci)
- Maintenance windows

## Diagnostika problémů

### Sync nikdy neproběhl

- App pool je restartovaný (timer reset)
- Konfigurace `AdSyncIntervalMinutes` je extrémně velká
- HostedService selhal při startu — viz aplikační log

### Sync běží ale neaktualizuje atributy

- AD je nedostupný, lookup vrací prázdné výsledky
- `Guid_AD` v `dbo.osoby` neodpovídá aktuálnímu AD účtu — sync nemůže najít
- Lookup permission — IIS app pool identity nemá AD search rights

### Sync padá na chybu

- Connection k AD timeout — zvýšit `QueryTimeoutSeconds`
- AD vrací neočekávaný atribut — bug v aplikaci, nahlas vývojářům s logem

## Pro koho

- **Admin** — denní monitoring, manuální trigger
- **DevOps** — diagnostika sync jobů

## Související

- [Active Directory → Synchronizace osob](../../integrace/active-directory/synchronizace-osob.md)
- [Synchronizace](index.md)
