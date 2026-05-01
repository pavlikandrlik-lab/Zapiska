---
title: SD harvest
description: Vytěžování vyjádření z HOT_VYJADRENI a aktualizace harmonogramu.
---

# SD harvest

PM Tracker má **dva harvest jobs** pro načítání dat ze ServiceDesku — aktivní
a archivní. Oba čtou `HOT_VYJADRENI` a aktualizují interní data
(klasifikaci, vazby na harmonogram, skutečnost).

Pro detailní popis logiky harvestu viz
[Auto-fill skutečnosti](../../integrace/servicedesk/auto-fill-skutecnosti.md).
Tato stránka popisuje **admin UI pro správu jobů**.

## Dva joby — proč rozdělené

### Aktivní harvest (`SdActivePeriodicSyncHostedService`)

- **Cíl**: vazby na *otevřené* / aktivní tickety
- **Frekvence**: častá (default 30 minut)
- **Velikost dávky**: malá (typicky desítky-stovky vazeb)
- **Kritičnost**: vysoká — uživatelé chtějí vidět aktuální data

### Archivní harvest (`SdArchivePeriodicSyncHostedService`)

- **Cíl**: vazby na *uzavřené* / staré tickety
- **Frekvence**: řidší (default 6 hodin)
- **Velikost dávky**: větší (může být tisíce vazeb)
- **Kritičnost**: nižší — historická data se moc nemění

Rozdělení je optimalizace — staré tickety nepotřebují stejnou frekvenci
jako live workflow.

## Co stránka ukazuje

### Per job (aktivní + archivní)

```
Aktivní harvest:
  Poslední běh:    2026-04-25 09:30:00 UTC
  Doba trvání:     2 minuty 15 sekund
  Stav:            ✓ úspěšný
  Zpracováno vazeb: 156
    - Fetched (nově načteno):    156
    - Created (nové vazby):      12
    - Superseded (přepsané):     8
    - Skipped (změna nebyla):    136
  Chyby:           0

Archivní harvest:
  Poslední běh:    2026-04-25 06:00:00 UTC
  ...
```

### Posledních N běhů

Tabulka historie pro každý job (aktivní / archivní separátně).

## Akce

### Spustit teď

Pro každý job tlačítko *Spustit teď*:

- Confirm dialog
- Spustí ad-hoc běh, vrátí výsledek
- Po dokončení update stránky

⚠️ **Pozor na archivní harvest** — má větší dávku, manuální spuštění může
trvat **desítky minut**. Spouštěj jen pokud opravdu potřebuješ čerstvá
archivní data.

### Re-harvest jednoho ticketu

Pro **konkrétní vazbu** (jen jeden ticket) použij `/SDConnector` →
*Re-harvest* u řádku. Detail:
[SD konektor — diagnostika](../../integrace/servicedesk/sd-konektor-diagnostika.md).

### Reaktivní vs periodický

PM Tracker má i **reaktivní sync** (`SdReactiveSyncConsumer`) — když user
v UI změní vazbu (drag & drop v chat modalu, propojení nového ticketu),
sync se spustí **okamžitě jen pro ten jeden ticket**. Periodické joby pak
slouží jako safety net.

Reaktivní sync nemá vlastní UI — běží automaticky.

## Konfigurace

```json
"PmTracker": {
  "Sync": {
    "SdActiveHarvestIntervalMinutes": 30,
    "SdArchiveHarvestIntervalMinutes": 360,
    "SdHarvestQuietHourStart": "22:00",
    "SdHarvestQuietHourEnd": "06:00"
  }
}
```

Plus konfigurace v `Ticketing`:

```json
"Ticketing": {
  "Enabled": true,
  "ConnectionStringName": "TicketingReadOnly",
  "CommandTimeoutSeconds": 30
}
```

`CommandTimeoutSeconds` je timeout SQL dotazu na SD. Pokud SD je pomalý,
zvýšit (např. 60s).

## Diagnostika problémů

### Harvest selhává s connection error

- SD SQL je nedostupný — viz
  [Řešení problémů SD](../../integrace/servicedesk/reseni-problemu-sd.md)
- Connection string je špatný (`appsettings.json`)
- Login nemá `db_datareader`

### Harvest běží pomalu (timeout)

- Velký počet vazeb — zkrátit interval, snížit batch size, zvýšit
  `CommandTimeoutSeconds`
- SD SQL je pomalý (jiná zátěž) — řešit s SD adminem
- Network latency mezi PM Tracker a SD serverem

### Harvest neaktualizuje data ani když SD má nové vyjádření

- Reaktivní sync neproběhl (chyba v queue / consumer)
- Tichá doba — kontroluj `SdHarvestQuietHourStart` / `End`
- Cache TTL — některé výsledky jsou cachované, refresh trvá

### Harvest aktualizuje data ale auto-fill nedoplňuje skutečnost

Není to chyba harvestu, ale chyba auto-fill logiky. Viz
[Auto-fill skutečnosti](../../integrace/servicedesk/auto-fill-skutecnosti.md).

## Pro koho

- **Admin** — monitoring, manuální trigger
- **DevOps** — performance tuning konfigurace

## Související

- [SD harvest — princip (integrace)](../../integrace/servicedesk/auto-fill-skutecnosti.md)
- [SD konektor — diagnostika](../../integrace/servicedesk/sd-konektor-diagnostika.md)
- [Synchronizace](index.md)
