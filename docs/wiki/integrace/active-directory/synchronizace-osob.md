---
title: Synchronizace osob z AD
description: Periodické čtení atributů (displayName, OU, email) z AD do aplikace.
---

# Synchronizace osob z AD

Hosted service `AdPeriodicSyncHostedService` pravidelně **čte atributy osob
z AD a aktualizuje je v `dbo.osoby`**. Cíl: aby aplikace měla aktuální
displayName a organizační celek bez ruční intervence.

## Co sync dělá

Pro každou osobu v `dbo.osoby` (kde `Guid_AD` je vyplněný):

1. Zavolá AD lookup podle `Guid_AD`
2. Pokud osoba v AD existuje:
   - Aktualizuje `displayName`, `organizační_celek`, `email`
   - Pokud se atribut změnil oproti DB, zapíše audit záznam
3. Pokud osoba v AD **už neexistuje**:
   - **Logicky deaktivuje** záznam (`is_active = 0`)
   - Zapíše audit záznam typu `PERSON_DEACTIVATED_NOT_IN_AD`
4. Pokud lookup selže (timeout, AD nedostupné):
   - Záznam **přeskočí** (nezasahuje do dat)
   - Zapíše chybu do logu

## Co sync **NEdělá**

### Nezakládá nové osoby

Kdyby nový zaměstnanec přibyl do AD, sync ho **neprodloží** do `dbo.osoby`.
Důvod: aplikace by se zaplnila tisíci osob které nejsou jejími uživateli.

Nové osoby se zakládají **lazy**: až když je někdo přiřadí do projektového
týmu přes [AD picker](ad-picker.md).

### Nemaže osoby

Deaktivace = `is_active=0`, ne fyzické DELETE. Důvod:

- Audit log a historické záznamy by jinak ztratily reference na autora
- Reaktivace osoby (kdyby se vrátila do AD) je triviální

### Nemění role / oprávnění

Role spravuje admin v [Nastavení](../../nastaveni-administrace/), ne sync.
AD obsahuje *kdo je člověk*, ne *co může v PM Trackeru*.

## Periodicita

Konfigurace v `appsettings.json`:

```json
"PmTracker": {
  "Sync": {
    "AdSyncIntervalMinutes": 360,    // 6 hodin
    "AdSyncQuietHourStart": "22:00",
    "AdSyncQuietHourEnd": "06:00"
  }
}
```

Default každých 6 hodin. Možnost nastavit "tichá hodina" — během noci sync
neběží (úspora zátěže AD při backup window).

## Reaktivní sync

Vedle periodického jobu existuje **reaktivní sync** — pokud admin udělá akci
která vyžaduje aktuální AD data (např. *Refresh osoby* v UI), sync se
spustí okamžitě pro tu jednu osobu.

## Manuální spuštění

V [Nastavení → Synchronizace → AD sync](../../nastaveni-administrace/synchronizace/ad-sync.md)
je tlačítko **Spustit teď**. Spustí jeden běh okamžitě (out-of-schedule).

## Stav posledního běhu

Admin sekce ukazuje:

- Datum / čas posledního běhu
- Doba trvání
- Počet zpracovaných osob
- Počet úspěšných / chyb / přeskočených
- Poslední chyba (pokud nějaká byla)

## Audit

Každá změna atributu se zapíše do audit logu:

```
TYPE: PERSON_ATTRIBUTE_UPDATED
PERSON: jan.novak
FIELD: displayName
BEFORE: "Jan Novák"
AFTER: "Mgr. Jan Novák"
TIMESTAMP: 2026-04-25 06:00:12 UTC
```

To umožní zpětně dohledat "kdy se změnilo" — a pochopit, že to udělal
automatický sync, ne admin.

## Limity

### Velký AD = pomalý sync

Pokud `dbo.osoby` má tisíce záznamů, sync může trvat desítky minut. Aplikace
to ošetřuje **batchováním** — N osob v jedné dávce, mezi dávkami pauza.

### Network glitch během sync

Pokud spadne během běhu připojení k AD, sync zapíše error a ukončí běh.
Nezpracované osoby zůstanou s nezměněnými atributy (last-known-good).

### Obrana proti AD downtime

Pokud AD není dostupný delší dobu, sync **nepřepíše atributy na null** ani
neoznačí osoby jako neaktivní. Vyžaduje úspěšný lookup, jinak osoba zůstane
beze změny.

## Pro koho je tato stránka

Admin / SuperAdmin. Běžný user toto nepotřebuje vědět — pro něj jen sync
"funguje na pozadí".

## Související

- [AD picker](ad-picker.md) — kde se osoby do `dbo.osoby` zakládají
- [Nastavení → Synchronizace → AD sync](../../nastaveni-administrace/synchronizace/ad-sync.md) — admin trigger
- [Osoby](../../osoby/) — co s atributy aplikace dělá
