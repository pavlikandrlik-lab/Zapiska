# PM Tracker - Instalační dokument (vstupní)

Tento soubor je vstupní instalační dokument. Kompletní detail je rozdělen do technické dokumentace v `docs/technical/*`.

## 1) Rychlé odkazy
- Strom dokumentace a pokrytí uzlů: `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/technical/00-documentation-tree.md`
- Systémový kontext: `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/technical/01-system-context.md`
- Runtime konfigurace: `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/technical/03-runtime-configuration.md`
- Instalace a deployment (IIS): `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/technical/04-installation-deployment-iis.md`
- Konfigurace web serveru: `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/technical/05-web-server-iis-config.md`
- Databáze a migrace: `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/technical/06-database-bootstrap-migrations.md`
- Provozní runbooky: `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/technical/08-operations-runbooks.md`
- Troubleshooting a recovery: `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/technical/10-troubleshooting-recovery.md`

## 2) Instalační minimum (souhrn)
1. Build/publish aplikace (`dotnet publish`).
2. Přenos publish artefaktu na IIS server.
3. SQL bootstrap (`PMTracker_insert_sql`) a případné upgrade patch skripty.
4. Nastavení `appsettings.Production.json`.
5. IIS konfigurace (No Managed Code, Windows Authentication).
6. Restart IIS a smoke test.
7. Založení osob v `dbo.osoby` je ruční krok (AD sync není automatický).

## 3) Povinné artefakty pro deploy
- Publish výstup (`publish/fdd/*`).
- SQL baseline a upgrade skripty.
- `appsettings.Production.json` se správným `ConnectionStrings:PmTrackerDb`.

## 4) Ověření po nasazení
- Route: `/Projekty`, `/Jednani`, `/Ciselniky`, `/Nastaveni`.
- Přihlášení přes Windows Auth.
- Export PDF/Word.
- Kontrola práv na test účtu.

## 5) Poznámka ke změnám
Detailní SOP postupy jsou závazně vedené v `docs/technical/*` a in-app pod `/Dokumentace/Technicka/*`.
