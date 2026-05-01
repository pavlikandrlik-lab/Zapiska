---
title: SD konektor — diagnostika
description: Admin stránka /SDConnector pro kontrolu harvestu a inspekci konkrétního ticketu.
---

# SD konektor — diagnostika

`/SDConnector` je **admin diagnostická stránka** pro kontrolu integrace s
ServiceDeskem. Slouží admin / ops účelům — běžný user ji nepotřebuje.

## Dostupnost

Permission `settings.sd.view`. Typicky SuperAdmin / App Admin.

URL: `/SDConnector` (relativní k base URL aplikace).

## Hlavní obrazovka — KPI

Nahoře 4 hlavní KPI tlačítka:

| KPI | Co znamená |
|---|---|
| **SD integrace** | Zapnutá / Vypnutá (`Ticketing.Enabled` flag z `appsettings.json`) |
| **Celkem externích vazeb** | Počet záznamů v `zaznam_externi_odkazy` |
| **Vytěženo za 24 h** | Kolik vazeb mělo úspěšný harvest v posledních 24 h |
| **Nikdy nevytěženo** | Vazby které ještě neměly žádný harvest |

## Tabulka externích vazeb

Kompletní seznam vazeb se sloupci:

- **ID externí vazby** (`zaznam_externi_odkazy.id`)
- **Číslo ticketu** (6-cifer)
- **Záznam ID** + odkaz na editor záznamu
- **Projekt ID** + odkaz na detail projektu
- **Vytěženo** (timestamp posledního úspěšného harvestu)
- **Vazby (A / M)** — počet aktivních / manuálních vazeb na harmonogram
- **Akce: Re-harvest**

### Filtrace

Default zobrazení = všechny vazby s vyplněným číslem (ignorují se prázdné).
Řazení = nejnovější vytěženo nahoře.

## Akce — Re-harvest

Tlačítko *Re-harvest* u řádku spustí sync **pro tento jediný ticket**:

1. Načte aktuální vyjádření z `HOT_VYJADRENI`
2. Klasifikuje je predikáty
3. Aktualizuje `vyjadreni_vazby`
4. Pokud má někoho v Auto režimu, přepočítá skutečnost

Po dokončení:

- **Toast** s výsledkem (kolik fetched / created / superseded / skipped)
- **Audit log** zapsaný (admin akce)
- Tabulka KPI se obnoví

Vyžaduje permission `vyjadreni.reharvest`.

### Diagnostika selhání

Pokud Re-harvest selže (např. SD nedostupné):

- Toast s chybou + Trace-Id
- Audit log s `reharvest.failed` (i selhání se loguje)
- Detaily v server-side logu (admin dive)

## Inspect podstránka

URL: `/SDConnector/Inspect[?cislo=XXXXXX]`

Slouží k **detailní diagnostice konkrétního ticketu** — co aplikace o něm
ví, jak ho klasifikuje, co by zobrazila v chat modalu.

### Co zobrazí

**Hlavička HOT_ZAZNAMY**:

- Číslo, pid, typ záznamu, stav
- Stručně, popis (raw HTML)
- Datum
- Externí vazba ID v PM Trackeru (pokud existuje)

**Bubliny vyjádření** (pro každé v `HOT_VYJADRENI`):

- HOT_VYJADRENI.id
- Typ, datum, login autora (raw + resolved displayName)
- Tým
- Plain text (HTML stripped)
- **Klasifikace** harvest predikátem (K3 / K4_K7 / K6 / K10 / PlanDodani / None)

### Use case

- "Proč auto-fill nedoplňuje krok HS04?" → otevři Inspect, ověř že vyjádření
  s klíčem K4_K7 existuje a je správně klasifikované
- "Proč chat modal je prázdný pro ticket X?" → Inspect ukáže, jestli ticket
  vůbec v `HOT_ZAZNAMY` existuje, jestli má vyjádření, jestli je harvest
  proběhlý
- "Schema mismatch?" → Inspect dělá full read na entity HotZaznam — pokud
  selže, dostaneš stack trace na `/SDConnector/Diag`

## Diagnostický endpoint `/Diag`

URL: `/SDConnector/Diag?cislo=XXXXXX`

**Plain-text** endpoint pro debug. Ne UI, ne JSON — čistý text response,
který otevřeš přímo v URL liště:

```
=== /SDConnector/Diag — plain-text diagnostic ===
Time:    2026-04-25T08:30:15Z
Cislo:   363139
User:    DOMÉNA\admin
Ticketing.Enabled: True
Ticketing.ConnectionStringName: TicketingReadOnly

[fingerprint] start...
[fingerprint] OK, fingerprints count=1
[fingerprint] fp.Datum=2026-04-24T16:41:00, fp.Stav=otevřeno, fp.TypZaznamu=PMP
[ticket-full] start (full HotZaznamEntity materialization)...
[ticket-full] OK, ticket=Id=363139, TypZaznamu=PMP, len(strucne)=58
[externi-odkaz] start...
[externi-odkaz] OK, externiOdkazId=42
[vyjadreni-fetch] start (HotVyjadreniEntity materialization)...
[vyjadreni-fetch] OK, count=12
[vyjadreni-classify] start...
[vyjadreni-classify] OK, classified=12

=== ALL STAGES PASSED ===
```

Pokud něco selže:

```
!!! EXCEPTION at stage [ticket-full] !!!

-- Level 0 --
Type:    System.InvalidCastException
Message: Unable to cast object of type 'System.Int32' to type 'System.Int64'.
Stack:
   at Microsoft.Data.SqlClient.SqlBuffer.get_Int64()
   ...
```

Účel: **bezúpadkový kanál pro získání exception**, žádný JS / browser dev tools.
Kdokoli s `settings.sd.view` může otevřít URL a poslat výsledek developerovi.

## Pro koho je tato stránka

- **App Admin** — denní diagnostika, Re-harvest po incidentu
- **SuperAdmin** — full management
- **DevOps / Vývojář** — když je nasazení nové verze a něco se rozbije

## Související

- [Řešení problémů SD](reseni-problemu-sd.md) — kompletní troubleshooting
- [Synchronizace → SD harvest](../../nastaveni-administrace/synchronizace/sd-harvest.md)
- [Auto-fill skutečnosti](auto-fill-skutecnosti.md)
