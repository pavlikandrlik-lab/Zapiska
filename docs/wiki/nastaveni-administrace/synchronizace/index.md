---
title: Synchronizace
description: Stav AD a SD sync jobů, manuální spuštění, diagnostika.
---

# Synchronizace

Sekce *Synchronizace* spravuje **periodické joby** které běží na pozadí
PM Trackeru. Aplikace má dva základní typy:

| Job | Co dělá | Frekvence |
|---|---|---|
| **AD sync** | Čte atributy osob z Active Directory | Typicky 6 h |
| **SD harvest (aktivní)** | Čte vyjádření aktivních ticketů ze SD | Typicky desítky minut |
| **SD harvest (archivní)** | Čte vyjádření starých ticketů ze SD | Typicky hodiny |

## Co stránka ukazuje

Pro každý job:

- **Stav posledního běhu** — kdy proběhl, doba trvání
- **Počet zpracovaných položek** (osob / ticketů)
- **Počet úspěšných / chyb / přeskočených**
- **Poslední chyba** (pokud nějaká byla)
- **Příští plánovaný běh**
- Tlačítko **Spustit teď** (manuální trigger, vyžaduje permission)

## Sub-stránky

- [AD sync](ad-sync.md) — synchronizace osob z Active Directory
- [SD harvest](sd-harvest.md) — vytěžování vyjádření ze ServiceDesku

## Kdy spustit manuálně

Manuální *Spustit teď* dává smysl:

- **Po deploy** nové verze, abys hned ověřil že job funguje
- **Po incidentu** (AD nebo SD bylo dlouho dolů a teď zase běží — chceš data
  honem aktualizovat)
- **Pro debug** — hned vidíš výsledek + chybu (pokud je)
- **Pro testovací data** — admin přidá testovacího usera do AD a chce ho
  hned vidět v `dbo.osoby`

## Audit

Každý běh sync se zapíše:

- Type: `AD_SYNC_START`, `AD_SYNC_END_SUCCESS`, `AD_SYNC_END_ERROR`
- Stejně pro SD harvest

Z audit logu lze sledovat **historii běhů** + reagovat na opakované selhání
("posledních 5 běhů AD sync skončilo chybou").

## Permission keys

- `settings.view` — vidět tuto sekci
- `sync.trigger` — manuálně spustit job (typicky jen Admin)

## Pro koho

Admin / SuperAdmin / DevOps.

## Konfigurace

Periodicita sync jobů je v `appsettings.json`:

```json
"PmTracker": {
  "Sync": {
    "AdSyncIntervalMinutes": 360,
    "SdActiveHarvestIntervalMinutes": 30,
    "SdArchiveHarvestIntervalMinutes": 360
  }
}
```

Změna intervalu vyžaduje **restart app pool** (config se čte při startu).
Detail v `docs/technical/`.

## Související

- [Active Directory — synchronizace osob](../../integrace/active-directory/synchronizace-osob.md)
- [Auto-fill skutečnosti](../../integrace/servicedesk/auto-fill-skutecnosti.md)
- [SD konektor — diagnostika](../../integrace/servicedesk/sd-konektor-diagnostika.md)
