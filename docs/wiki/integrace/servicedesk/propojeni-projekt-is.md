---
title: Propojení projektu na IS
description: Jak se projekt PM Trackeru přiřadí k informačnímu systému ve ServiceDesku (FIS / ISSP).
---

# Propojení projektu na informační systém (IS)

Projekt v PM Trackeru se může (volitelně) přiřadit k jednomu **informačnímu
systému ve ServiceDesku**. Toto propojení **nemění data v SD**, pouze říká
PM Trackeru "tento projekt patří k tomu IS, filtruj data odpovídajícím způsobem".

## Proč to existuje

Bez propojení je projekt v PM Trackeru izolovaný — neví, že má vůbec něco
společného s konkrétním IS v SD. Některé funkce pak nemůžou fungovat:

- **NES panel projektového dashboardu** — bez propojení neví, které tickety
  zobrazit (graceful state)
- **Cílení automatického harvestu** — bez propojení aplikace neví, které pole
  v `HOT_ZAZNAMY` filtrovat
- **Subsystém-stats** — vazba na `HOT_SUBSYSTEM` se opírá o IS

S propojením všechny tyto funkce začnou fungovat automaticky.

## Kde se propojení nastaví

Detail projektu → tlačítko *Upravit* → **Informační systém** dropdown.

Hodnoty:

- **— bez napojení —** (default pro nový projekt)
- **FIS — Finanční informační systém** (`HOT_IS.ID = 1`)
- **ISSP — Informační systém služebních poměrů** (`HOT_IS.ID = 2`)

Po uložení se hodnota propíše do `dbo.projekty.ServiceDeskInfoSystemId`.

## Hardcoded katalog vs. dynamický

PM Tracker má **hardcoded katalog dvou IS** (FIS a ISSP). Hodnoty `ID = 1`
a `ID = 2` jsou **přesně shodné** s `HOT_IS.ID` v intranetNEW DB.

V SD reálně existuje i třetí IS (X_FIS), ale ten je **mimo scope** PM Trackeru.

### Proč hardcoded?

- IS je **stabilní enum** — nepřibývá nový IS každý měsíc
- Dynamické čtení by vyžadovalo dotaz na SD při každém renderu edit modalu
- Selhání connection by způsobilo, že editace projektu nepojede

Trade-off: pokud SD přidá nové IS (např. nový pro nějaký useful systém),
musí to do `SdInfoSystemy.cs` zapsat vývojář a deploynout.

### Verifikace při deploy

V `SdInfoSystemy.cs` je TODO komentář pro admina, aby při deploy ověřil:

```sql
SELECT ID, zkratka, nazev FROM intranetNEW.dbo.HOT_IS
WHERE zkratka IN ('FIS', 'ISSP') ORDER BY ID;
```

Pokud ID v produkci není 1/2, musí se konstanty `FisId` / `IsspId` upravit.

## Co propojení **mění**

Po nastavení propojení se okamžitě:

- **NES panel** v projektovém dashboardu začne ukazovat tickety daného IS
  v prodlení (pokud existují)
- **Filtrace záznamů** v projektovém dashboardu se může opírat o subsystémy
  daného IS
- **Auto-fill harmonogramu** ze SD vyjádření začne fungovat (pokud má záznam
  vazbu na ticket)

## Co propojení **NEmění**

- **Existující externí vazby na záznamech** — zůstávají
- **Ostatní projekty** — propojení je per-projekt, ne globální
- **SD samotný** — PM Tracker do SD nezapisuje, propojení je čistě interní

## Co se stane, když propojení odstraním

Změníš dropdown na "— bez napojení —" a uložíš:

- NES panel přejde do **graceful state** ("Projekt nemá napojení na IS …")
- Auto-fill harmonogramu se zastaví (pro nové vyjádření, existující hodnoty
  zůstanou)
- Ostatní funkce (záznamy, jednání, tým, návrhy) zůstávají nedotčené

Propojení **nelze nastavit dvakrát** (projekt má 1:0..1 vztah na IS).

## Memory rule

Z memory `project_servicedesk_infosystem_binding`:

> projekt → IS je 1:0..1 (`ServiceDeskInfoSystemId`), řídí filtraci NES /
> Výzvy / SD metrik v projektovém dashboardu.

## Permissions

Změnu propojení může udělat user s **`projects.edit`** na daném projektu.
Žádný separátní permission key (jako `projects.edit.servicedesk`) zatím není.

## Související

- [Projekty → Upravit projekt](../../projekty/upravit-projekt.md) — UI dropdown
- [Projektový dashboard → NES v prodlení](../../projekty/projektovy-dashboard/nes-v-prodleni/)
- [Nastavení → Synchronizace → SD harvest](../../nastaveni-administrace/synchronizace/sd-harvest.md)
