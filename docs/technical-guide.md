# Zápiska – Technická dokumentace

## 1. Účel

Tento dokument popisuje technické fungování aplikace, aby bylo možné bezpečně:

- provozovat aplikaci na IIS + MS SQL,
- rozšiřovat backend a UI bez regresí,
- orientovat se v doménovém modelu a oprávněních.

Instalační kroky jsou vedené samostatně v souboru:

- `/Users/Pavel.Andrlik/Documents/PM Tracker/install.md`

## 2. Architektura

- ASP.NET Core 8 MVC monolit (`PmTracker.Web`).
- Server-side Razor Views + vanilla JS.
- Datová vrstva: SQL Server (`SqlServerDataStore`) bez mock fallbacku v produkčním provideru.
- Autorizace: role/akce/scopy nad schématem `authz`.

## 3. Datový model

- Základní doména je ve schématu `dbo`.
- Oprávnění a role jsou ve schématu `authz`.
- Source of truth pro DB je sjednocený skript:
  - `/Users/Pavel.Andrlik/Documents/PM Tracker/PMTracker_insert_sql`

## 4. Oprávnění a bezpečnost

- Runtime model je `allow-only` (bez explicitních deny pravidel).
- Superadmin je určen tabulkou `authz.superadmins`.
- UI prvky pro editaci se řídí permission klíči i projektovým scope.
- AD/Windows identita se mapuje na osobu v `dbo.osoby`.

Podrobné provozní řízení:

- `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/admin-guide.md`
- `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/navod-administrace-akci-opravneni.md`

## 5. Testování a kvalita

- Test pipeline:
  - `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/testing-guide.md`
- Test coverage matrix:
  - `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/test-matrix.md`
- DRY/OOP refaktor poznámky:
  - `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/dry-oop-refactor.md`

## 6. Provozní checklist

Před go-live vždy zkontrolovat:

- DB bootstrap dokončen bez chyb,
- povinné číselníky existují,
- mapování identity funguje pro produkční účty,
- export (PDF/Word) funguje na reálných datech.

Detailní checklist:

- `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/production-readiness.md`

## 7. Konvence pro vývoj

- Preferovat sdílené partialy a view modely místo kopírování Razor bloků.
- Změny business pravidel držet v datové/službové vrstvě, ne v JavaScriptu.
- Všechny nové texty a štítky držet konzistentní s existující terminologií.
- Při změně datového modelu vždy upravit `PMTracker_insert_sql` + aktualizovat dokumentaci.
