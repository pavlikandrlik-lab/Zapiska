# PM Tracker - Technická dokumentace 04: Instalace a deployment (IIS)

## 1. Účel
Tento runbook popisuje kompletní instalační a deployment postup pro produkční topologii Windows Server + IIS + SQL Server.

## 2. Publikum a role
- Ops administrátor: deployment aplikace, IIS konfigurace.
- DB administrátor: SQL bootstrap a upgrade.
- Aplikační administrátor: post-deploy validace práv.

## 3. Závislosti a předpoklady
- Build host s .NET SDK 8.
- Cílový server s IIS rolí.
- SQL Server instance dostupná ze serveru aplikace.
- Přístupová práva pro deploy a SQL změny.

## 4. Vstupy a výstupy
### Vstupy
- Zdrojový kód a projekt `PmTracker.Web.csproj`.
- SQL skripty: `PMTracker_insert_sql`, `db_upgrade_*.sql`.
- Runtime config: `appsettings.Production.json`.

### Výstupy
- Publikovaný obsah v cílové složce IIS webu.
- Inicializovaná nebo upgradovaná databáze.
- Validovaný smoke test po nasazení.

## 5. Detailní postup
### 5.1 Build a publish
```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web"
dotnet restore
dotnet publish -c Release -o ../publish/fdd
```

Artefakt pro deploy musí obsahovat:
- `publish/fdd/*`
- `/Users/Pavel.Andrlik/Documents/PM Tracker/PMTracker_insert_sql`
- relevantní `db_upgrade_*.sql`

### 5.2 SQL bootstrap (fresh install)
```powershell
$SQL_INSTANCE = "<SQL_SERVER>"
$DB_NAME = "PmTracker"
$DEPLOY_SQL_DIR = "C:\deploy\pmtracker\sql"

sqlcmd -S $SQL_INSTANCE -E -Q "IF DB_ID('$DB_NAME') IS NULL CREATE DATABASE [$DB_NAME];"
sqlcmd -S $SQL_INSTANCE -E -d $DB_NAME -b -i "$DEPLOY_SQL_DIR\PMTracker_insert_sql"
```

### 5.3 SQL upgrade (existující DB)
```powershell
sqlcmd -S $SQL_INSTANCE -E -d $DB_NAME -b -i "$DEPLOY_SQL_DIR\db_upgrade_1_1_0_signed_schedule_actual.sql"
sqlcmd -S $SQL_INSTANCE -E -d $DB_NAME -b -i "$DEPLOY_SQL_DIR\db_upgrade_1_1_1_external_link_estimated_price.sql"
```

### 5.4 Deploy na IIS server
```powershell
New-Item -ItemType Directory -Path "C:\apps\PmTracker.Web" -Force
# Zkopíruj publish obsah do C:\apps\PmTracker.Web
```

### 5.5 Konfigurace aplikace
V `C:\apps\PmTracker.Web\appsettings.Production.json` nastav:
- `ConnectionStrings:PmTrackerDb`
- `PmTracker:Data:Provider=SqlServer`
- `PmTracker:Data:SqlServer:ConnectionStringName=PmTrackerDb`
- `PmTracker:ActiveDirectory:Domain` pro správný doménový kontext vyhledávání osob

Poznámka:
- AD účty nejsou automaticky synchronizované do `dbo.osoby`.
- Pro přihlášení musí být osoba v DB založena ručně (včetně `Guid_AD`).

### 5.6 Finální restart
```powershell
iisreset
```

## 6. Verifikace
- Otevři `/Projekty`, `/Jednani`, `/Ciselniky`, `/Nastaveni`.
- Ověř funkční přihlášení přes Windows auth.
- Ověř export (PDF/Word).
- Ověř existence baseline dat (stavy projektu, role, authz tabulky).

## 7. Rollback
- Zastav site nebo proveď app_offline.
- Vrať předchozí publish balíček.
- Obnov předchozí `appsettings.Production.json`.
- Pokud byla aplikována nekompatibilní DB změna, proveď DBA-approved restore z backupu.

## 8. Troubleshooting
- 500.30 po nasazení:
  - ověř hosting bundle,
  - ověř connection string,
  - ověř seed baseline.
- 403 / nepřihlášený uživatel:
  - ověř IIS Windows Authentication,
  - ověř mapování osoby v `dbo.osoby`.

## 9. Audit a traceability
- Build/publish: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/PmTracker.Web.csproj`
- Hosting bootstrap: `/Users/Pavel.Andrlik/Documents/PM Tracker/publish/web.config`
- SQL baseline/patch: `/Users/Pavel.Andrlik/Documents/PM Tracker/PMTracker_insert_sql`, `/Users/Pavel.Andrlik/Documents/PM Tracker/db_upgrade_*.sql`
- Provozní checklist: `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/technical/08-operations-runbooks.md`
