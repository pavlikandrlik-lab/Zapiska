# Zápiska – Instalační dokumentace (Windows Server + IIS + MS SQL)

Tento dokument je kompletní instalační postup pro ostrý provoz bez internetu.

---

## 1. Přehled nasazení

Architektura:

- ASP.NET Core MVC monolit (`PmTracker.Web`),
- provoz pod IIS (`No Managed Code` app pool),
- MS SQL Server jako jediný datový provider,
- autentizace přes Windows Authentication + mapování na `dbo.osoby.Guid_AD`.

Pořadí:

1. Build/publish na build stroji.
2. Přenos artefaktů na server.
3. Inicializace databáze jednotným SQL skriptem.
4. Konfigurace IIS + appsettings.
5. Nastavení prvního superadmina.
6. Smoke test.

---

## 2. Požadavky

### 2.1 Build stroj

- .NET SDK 8.x
- přístup k NuGet (jen na build stroji)
- přístup k repozitáři zdrojů

### 2.2 Cílový server

- Windows Server
- IIS (Web Server)
- ASP.NET Core Hosting Bundle 8.x (pokud nepoužiješ self-contained publish)
- MS SQL Server (lokální nebo vzdálený)

---

## 3. Build a publish

Pracovní složka:

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web"
```

### 3.1 Doporučený publish (framework-dependent)

```bash
dotnet restore
dotnet publish -c Release -o ../publish/fdd
```

### 3.2 Volitelný publish (self-contained)

```bash
dotnet publish -c Release -r win-x64 --self-contained true -o ../publish/win-x64
```

Přenést na server:

- obsah `publish/fdd` (nebo `publish/win-x64`),
- SQL skript `/Users/Pavel.Andrlik/Documents/PM Tracker/PMTracker_insert_sql`.
- při upgrade existující DB i `/Users/Pavel.Andrlik/Documents/PM Tracker/db_upgrade_1_1_0_signed_schedule_actual.sql`.

---

## 4. SQL instalace databáze

Použij `sqlcmd` (Windows auth):

```powershell
$SQL_INSTANCE = "localhost"   # např. SQL01\INST1
$DB_NAME = "PmTracker"
$DEPLOY_SQL_DIR = "C:\deploy\zapiska\sql"

sqlcmd -S $SQL_INSTANCE -E -Q "IF DB_ID('$DB_NAME') IS NULL CREATE DATABASE [$DB_NAME];"
sqlcmd -S $SQL_INSTANCE -E -d $DB_NAME -b -i "$DEPLOY_SQL_DIR\PMTracker_insert_sql"
```

### 4.1 Co skript vytvoří

- schéma `dbo` a `authz`,
- finální tabulky aplikace (včetně sloupců `ad_login`, `location_locked`, `autor_osoba_id`, harmonogramu a viditelných čísel záznamů),
- povinné stavy projektu `PLAN/RUN/DONE/DELETED`,
- základní seed role/permission,
- seed osobu `Pavel Admin` (bez demo projektu PMT).

### 4.2 Ověření po SQL instalaci

```powershell
sqlcmd -S $SQL_INSTANCE -E -d $DB_NAME -Q "SET NOCOUNT ON;
SELECT kod, nazev FROM dbo.ciselnik_stavu_projektu WHERE kod IN ('PLAN','RUN','DONE','DELETED') ORDER BY kod;
SELECT id, jmeno, prijmeni, email FROM dbo.osoby WHERE jmeno=N'Pavel' AND prijmeni=N'Admin';
SELECT COUNT(*) AS pmt_count FROM dbo.projekty WHERE zkratka='PMT';
SELECT COUNT(*) AS authz_roles FROM authz.roles;"
```

Očekávaný výsledek:

- 4 povinné stavy projektu existují,
- `Pavel Admin` existuje,
- `pmt_count = 0`,
- `authz.roles` má data.

### 4.3 Upgrade existující databáze na signed skutečnost harmonogramu

Pokud nenasazuješ čistou databázi, ale upgrade release `1.1.0`, spusť po standardním deployi i tento patch:

```powershell
sqlcmd -S $SQL_INSTANCE -E -d $DB_NAME -b -i "$DEPLOY_SQL_DIR\db_upgrade_1_1_0_signed_schedule_actual.sql"
```

Patch je idempotentní a jen odstraní legacy constraint `CK_zaznam_harmonogram_hodnoty_hodnota_nonnegative`, který by jinak blokoval záporné hodnoty harmonogramové skutečnosti.

---

## 5. Nastavení prvního superadmina

`authz.superadmins.osoba_id` je `INT` FK na `dbo.osoby.id`.

Do superadmin tabulky se **nevkládá** `acr\login`.

```powershell
sqlcmd -S $SQL_INSTANCE -E -d $DB_NAME -Q "SET NOCOUNT ON;
DECLARE @osoba_id int = (
    SELECT TOP (1) id
    FROM dbo.osoby
    WHERE email = 'andrlikp@your-domain.cz'
);
IF @osoba_id IS NULL
BEGIN
    RAISERROR('Osoba neexistuje v dbo.osoby. Nejdřív ji založte (AD sync/manual).', 16, 1);
    RETURN;
END
IF NOT EXISTS (SELECT 1 FROM authz.superadmins WHERE osoba_id = @osoba_id)
BEGIN
    INSERT INTO authz.superadmins (osoba_id, poznamka, created_at, created_by)
    VALUES (@osoba_id, N'První provozní superadmin', SYSUTCDATETIME(), N'install');
END;"
```

---

## 6. IIS konfigurace

### 6.1 Instalace IIS role

```powershell
Install-WindowsFeature Web-Server,Web-Mgmt-Console,Web-Windows-Auth
```

### 6.2 Hosting Bundle (pokud FDD publish)

```powershell
Start-Process -FilePath "C:\installs\dotnet-hosting-8.0.x-win.exe" -ArgumentList "/quiet /norestart" -Wait
iisreset
```

### 6.3 Deploy složka

```powershell
New-Item -ItemType Directory -Path "C:\apps\Zapiska.Web" -Force
```

Nakopírovat publish obsah do `C:\apps\Zapiska.Web`.

### 6.4 App Pool + Site

V IIS Manager:

- Application Pool:
  - Name: `ZapiskaPool`
  - .NET CLR: `No Managed Code`
  - Pipeline: `Integrated`
- Site:
  - Site name: `Zapiska`
  - App pool: `ZapiskaPool`
  - Physical path: `C:\apps\Zapiska.Web`
  - Binding: dle prostředí (HTTP/HTTPS, port, host header)

### 6.5 Oprávnění složky

- `Read & Execute` pro `IIS AppPool\ZapiskaPool`.

### 6.6 SQL přístup pro app pool účet

Pokud používáš `Trusted_Connection=True`, App Pool identita (nebo servisní účet) musí mít SQL login + odpovídající DB práva.

---

## 7. Konfigurace aplikace

Soubor:

`C:\apps\Zapiska.Web\appsettings.Production.json`

```json
{
  "ConnectionStrings": {
    "PmTrackerDb": "Server=SQLSERVER01;Database=PmTracker;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
  },
  "PmTracker": {
    "Data": {
      "Provider": "SqlServer",
      "SqlServer": {
        "ConnectionStringName": "PmTrackerDb",
        "CommandTimeoutSeconds": 30
      }
    },
    "ActiveDirectory": {
      "Domain": "acr",
      "MaxResults": 15,
      "QueryTimeoutSeconds": 8
    }
  }
}
```

### Kde se nastavuje connection string

- **Produkce:** `appsettings.Production.json` (`ConnectionStrings:PmTrackerDb`)
- **Vývoj:** `PmTracker.Web/appsettings.Development.json`

---

## 8. Autentizace v IIS

Produkce:

1. `Windows Authentication = Enabled`
2. `Anonymous Authentication = Disabled`

Aplikace pak páruje přihlášeného uživatele podle GUID claimu na `dbo.osoby.Guid_AD`.

---

## 9. Environment proměnné

V IIS nastav:

- `ASPNETCORE_ENVIRONMENT=Production`

Místo:

- Site -> Configuration Editor -> `system.webServer/aspNetCore` -> `environmentVariables`

---

## 10. Restart a smoke test

Restart:

```powershell
iisreset
```

Smoke test:

1. Otevřít `/Projekty`.
2. Ověřit název aplikace: `Zápiska`.
3. Otevřít `/Jednani`, `/Ciselniky`, `/Nastaveni`.
4. Ověřit, že přihlášený účet má očekávaná práva.
5. Ověřit exporty (projekt, jednání, úkol).

---

## 11. Nejčastější problémy

### 500.30 při startu

- chybné DB připojení,
- nespustil se `PMTracker_insert_sql`,
- chybí povinné číselníky/tabulky.

### 403 / nepřihlášený uživatel v app

- chybí záznam v `dbo.osoby`,
- není správně vyplněné `Guid_AD`,
- IIS auth není správně nastavená.

### SQL script fail

- DB není prázdná (skript je určený pro fresh install),
- chybí oprávnění pro CREATE/ALTER.

---

## 12. Související dokumenty

- Administrační dokumentace: `/Dokumentace/Administracni-prirucka`
- Uživatelská příručka: `/Dokumentace/Uzivatelska-prirucka`
- Q and A: `/Dokumentace/qa`
- Production checklist: `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/production-readiness.md`
