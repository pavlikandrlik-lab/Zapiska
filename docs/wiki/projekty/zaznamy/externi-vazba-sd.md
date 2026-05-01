---
title: Externí vazba záznamu na SD ticket
description: Propojení PM Tracker záznamu se ServiceDesk ticketem (PMP/PNF/NES).
---

# Externí vazba na SD ticket

Editor záznamu má tab **Externí vazby**. Tady propojíš záznam PM Trackeru
s konkrétním ticketem ze ServiceDesku — odtamtud aplikace pak načítá
vyjádření, klasifikuje je a doplňuje skutečnost harmonogramu.

## Postup

1. Otevři editor záznamu → tab **Externí vazby**
2. Vyplň pole **Číslo ticketu** — 6-cifer (např. `363139`)
3. **Aplikace okamžitě (debounced ~300ms)** zavolá SD a ověří existenci
4. Výsledky:
   - **Ticket existuje** → vyplní se **typ záznamu** (NES / PMP / PNF) podle SD,
     povolí se tlačítko **Otevřít chat**
   - **Neexistuje** → inline chyba "Ticket #X v HOT_ZAZNAMY neexistuje", pole
     je validační error, záznam neuložíš

## Co vazba spustí na pozadí

### 1. Reaktivní harvest

Po uložení záznamu se sync služba spustí **okamžitě** pro tento jeden ticket:

- Načtěme všechna vyjádření z `HOT_VYJADRENI`
- Klasifikujeme harvest predikáty (K3 / K4_K7 / K6 / K10 / Plán dodání)
- Uložíme do `vyjadreni_vazby` a cache

### 2. Příprava chat modalu

Po harvestu obsahuje aplikace všechna vyjádření připravená k zobrazení v
[chat modalu](../../integrace/servicedesk/chat-vyjadreni.md).

### 3. Auto-fill harmonogramu

Pokud má krok harmonogramu nastavený **typ delay** odpovídající nějakému
predikátu, sync **přepočítá skutečnost** kroku z relevantních vyjádření.

Detail: [Auto-fill harmonogramu](../harmonogram/auto-fill-ze-sd.md).

## Memory rule — Ticket bez `id`

Z [memory `feedback_sd_ticket_id_required`](../../slovnicek.md):

> User zadává ticket výhradně přes 6-ciferné `HOT_ZAZNAMY.id`. Záznam bez
> `id` ignorovat (nikdy fallback na 0).

Praktické důsledky:

- **Pole `Číslo ticketu` má regex validaci** `^\d{6}$` — kratší / delší /
  non-numeric vstup selže
- **Aplikace nikdy nemapuje** záznam na fallback `id=0`
- Pokud SD má záznam **bez `id`** (vzácný edge case), PM Tracker ho **úplně
  ignoruje**

## Více vazeb na jeden záznam

Záznam může mít **N externích vazeb**:

- **Primární vazba** — typicky odpovídá typu záznamu (NES → NES ticket)
- **Další referenční** — pro křížové odkazy (např. PMP záznam s referencí
  na související NES ticket)

V editoru lze vazby **přidávat / odebírat**:

- *+ Přidat vazbu* → vyplnit nové 6-cif. číslo
- Tlačítko *Odebrat* u řádku vazby

Aplikace pro **každou vazbu** dělá vlastní harvest. Chat modal lze otevřít
pro každou separátně.

## Změna primární vazby

Pokud změníš `id` primární vazby:

1. Validace nového ticketu (existence v `HOT_ZAZNAMY`)
2. Po uložení **reaktivní harvest** pro nový ticket
3. **Stará vyjádření zůstávají v `vyjadreni_vazby`** (už nejsou aktivní)
4. **Skutečnost harmonogramu se přepočítá** ze vyjádření **nového** ticketu

Změna primární vazby je tedy **zásadní akce** — celý kontext SD se mění.

## URL na detail v SD

Po uložení vazby se v záznamu objeví **odkaz na detail v ServiceDesku**:

```
https://servicedesk.fis.acr/Hotline/Ticket/Details/{ticketId}
```

Klik otevře v novém okně / tabu.

## Validace pole

| Pravidlo | Chyba |
|---|---|
| Musí být přesně 6 cifer | "Ticket id musí být 6 cifer" |
| Musí existovat v `HOT_ZAZNAMY` | "Ticket #X neexistuje" |
| Nesmí duplicitní vazba na stejný ticket | "Tato vazba už existuje" |

## Co když SD je dolů

Pokud při uložení záznamu SD nedostupný:

- Validace `Ticket nenalezen` může selhat false-positive
- Reaktivní harvest se **odloží** (failed → retry queue)
- Záznam se uloží i bez aktivního harvestu
- Až SD bude dostupný, queue se dokončí

## Permissions

| Akce | Klíč |
|---|---|
| Vidět externí vazby | `records.view` (implicitní) |
| Přidat / změnit vazbu | `records.edit` |
| Spustit re-harvest | `vyjadreni.reharvest` (samostatné) |

## Diagnostika problémů

### "Ticket nenalezen" pro existující ticket

- Překlep v 6 cifer
- Ticket je v archivované DB (mimo aktivní `HOT_ZAZNAMY`)
- SD je nedostupný

Otevři `/SDConnector/Inspect?cislo=XXXXXX` pro detailní diagnostiku.

### Chat modal je prázdný

- Ticket existuje ale ještě nemá vyjádření
- Harvest neprošel — zkus *Re-harvest* z chat modalu

## Související

- [Chat vyjádření](../../integrace/servicedesk/chat-vyjadreni.md)
- [Tickety PMP/PNF/NES](../../integrace/servicedesk/tickety-pmp-pnf-nes.md)
- [Auto-fill harmonogramu](../harmonogram/auto-fill-ze-sd.md)
- [Řešení problémů SD](../../integrace/servicedesk/reseni-problemu-sd.md)
