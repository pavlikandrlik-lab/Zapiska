---
title: Integrace
description: Propojení PM Trackeru s externími systémy — Active Directory, ServiceDesk.
---

# Integrace

PM Tracker není izolovaná aplikace. Integruje se se dvěma firemními systémy,
které poskytují **identitu** a **business data**:

## Active Directory (AD)

Slouží primárně k:

- **Přihlášení uživatele** — IIS Windows authentication. Aplikace nemá vlastní
  hesla, identitu přebírá z firemního účtu.
- **Vyhledávání osob** přes AD picker — když přiřazuješ osobu do týmu projektu
  nebo jako collaborator záznamu.
- **Synchronizaci atributů** — periodicky čte displayName, organizační celek,
  email a aktualizuje je v `dbo.osoby`.

Detaily: [Active Directory](active-directory/)

## ServiceDesk (SD)

Slouží primárně k:

- **Propojení projektu s informačním systémem** (FIS / ISSP). Toto určuje, které
  tickety se mají zobrazovat v dashboardu projektu.
- **Propojení záznamu se ServiceDesk ticketem** (PMP / PNF / NES). Vznikne
  externí vazba, na základě které pak aplikace načítá data z SD.
- **Načtení vyjádření z ticketu** do chat modalu PM Trackeru. Uživatel vidí
  celou historii komunikace SD ticketu na záznamu PM Trackeru.
- **Automatické doplnění skutečnosti harmonogramu** z dodaných vyjádření.
  Klasifikační pravidla rozeznají, které vyjádření odpovídá kterému kroku
  harmonogramu, a navrhnou skutečnost.
- **Zobrazení tiketů v prodlení** — projektový dashboard má panel **NES v prodlení**,
  který filtruje aktuální překročené NES tickety daného IS.

Detaily: [ServiceDesk](servicedesk/)

## Read-only filozofie

PM Tracker do externích systémů **nikdy nezapisuje**:

- AD — jen čte (lookup, sync atributů)
- SD — jen čte (přes dedikovaný read-only DB connection s `db_datareader` rolí)

Chyba v PM Trackeru tedy **nemůže poškodit** AD ani SD. Veškeré změny v SD
musíš dělat přímo ve ServiceDesku, PM Tracker je pak při dalším syncu nasaje.

## Konfigurace

Konfigurace připojení k AD a SD je **mimo scope této wiki**. Kompletní deploy
postup (connection strings, IIS app pool identity, AD oprávnění, …) najdeš v
`docs/technical/`:

- `13-servicedesk-connection-setup.md` — SD connection
- `04-installation-deployment-iis.md` — IIS deploy
- `15-infosystem-migration-manual.md` — migrace projektů na IS

## Co tady **nenajdeš**

- Schema reference cizí intranetNEW DB — `docs/technical/12-servicedesk-schema-reference.md`
- Konfigurace AD periodických jobů — `docs/technical/`
- Vývojářské patterny pro integraci — `docs/architecture/backend-layering.md`

## Pro koho

- **Uživatel** — přihlášení, AD picker, propojení záznamu s ticketem, chat modal
- **Admin** — synchronizace, diagnostika, řešení problémů
