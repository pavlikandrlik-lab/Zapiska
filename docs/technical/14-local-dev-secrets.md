# 14. Lokální konfigurace vývojového prostředí

## Princip: appsettings v .gitignore, v gitu jen schema template

- `PmTracker.Web/appsettings.json` je v `.gitignore` — **obsahuje reálné runtime
  credentials**. Lokálně na dev stroji (Mac s docker SQL) nebo na Windows VYVOJ stroji
  v něm jsou skutečné connection stringy. Při `dotnet publish` se bere tento lokální
  soubor a kopíruje do publish artefaktu → produkce/testing ho dostane s hotovými
  credentials
- `PmTracker.Web/appsettings.example.json` je v gitu — **schema template s placeholder
  hodnotami**. Slouží jako dokumentace struktury a jako fallback pro nového vývojáře,
  který repo naklonuje (zkopíruje si `.example.json` → `appsettings.json` a doplní
  credentials pro své prostředí)
- `PmTracker.Web/appsettings.Development.json` je také v `.gitignore` — lokální dev
  override (typicky docker localhost SQL credentials)
- Architecture test [AppSettingsCredentialLeakGuardTests](../../PmTracker.Tests.Unit/Architecture/AppSettingsCredentialLeakGuardTests.cs)
  hlídá **template soubor** — pokud někdo omylem zacommituje reálné credentials do
  `appsettings.example.json`, build padne

Alternativní `dotnet user-secrets` je také podporovaný (user secrets přepíší
`appsettings.json`) — ale jeho použití je **volitelné**. Oba pattern koexistují.

## Tři prostředí, tři způsoby

| Prostředí | Kde běží SQL Server | Konfigurace credentials |
|---|---|---|
| **Dev Mac (lokální editace a rychlé testy)** | Docker / Colima na `localhost:1433` | **Nic nenastavovat** — `appsettings.Development.json` obsahuje `sa / PmTracker!2026` jako docker default a je v .gitignore |
| **Windows VYVOJ server (za Citrix bránou)** | Nativní Windows SQL Server s produkčně podobnými daty | Přes RDP/Citrix session na ten Windows stroj: `dotnet user-secrets set "ConnectionStrings:PmTrackerDb" "..." --project PmTracker.Web` |
| **Produkce (IIS)** | Produkční SQL Server | Environment variables v IIS Web.config `<environmentVariables>` sekci, případně Azure Key Vault |

User Secrets jsou per-user per-stroj — každý vývojář si na svém stroji uloží credentials
jen pro prostředí, ke kterému se fyzicky dostane.

## Proč User Secrets

- Secrets leží v `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json` (macOS/Linux)
  nebo `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json` (Windows) — **mimo repo**
- ASP.NET Core je v `Development` prostředí automaticky načítá (order po `appsettings.Development.json`,
  ale PŘED environment variables)
- Žádný gitignore trik není potřeba — `appsettings.json` v repu má **kompletní strukturu**,
  User Secrets ho overridují jen v místech, kde potřebují
- Oproti přepisování `appsettings.json` lokálně: není riziko commitu skutečných credentials,
  schéma v repu se udržuje aktuální automaticky

## Nový stroj / nový vývojář — first-time setup

1. Naklonuj repo
2. Zkopíruj template:
   ```bash
   cp PmTracker.Web/appsettings.example.json PmTracker.Web/appsettings.json
   ```
3. Otevři `PmTracker.Web/appsettings.json` a vyplň `ConnectionStrings:PmTrackerDb`
   podle svého prostředí:
   - **Mac dev s docker/colima**: `Server=localhost,1433;Database=PmTracker;User Id=sa;Password=PmTracker!2026;TrustServerCertificate=True;MultipleActiveResultSets=True`
   - **Windows VYVOJ server (za Citrixem)**: `Server=HOSTNAME\INSTANCE;Database=PM_Tracker_VYVOJ;User Id=<ucet>;Password=<heslo>;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True`
4. Ujisti se, že na Macu běží docker/colima SQL Server. Pokud ne:
   ```bash
   colima start
   docker run --name pmtracker-mssql -e 'ACCEPT_EULA=Y' -e 'SA_PASSWORD=PmTracker!2026' \
     -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
   ```
5. Spusť aplikaci:
   ```bash
   dotnet run --project PmTracker.Web
   ```

`appsettings.json` je v `.gitignore` → git ti ho neuvidí, tvé credentials nikdy
nezacommituješ omylem.

## Windows VYVOJ server — setup credentials

Když se přihlásíš přes RDP / Citrix session na Windows server s aplikací a potřebuješ
propojit s lokálním Windows SQL Serverem:

1. Otevři PowerShell / CMD v kořeni projektu
2. Ověř, že `.csproj` obsahuje `<UserSecretsId>` (je commitnutý — stejný pro všechny stroje):
   ```powershell
   dotnet user-secrets list --project PmTracker.Web
   ```
3. Nastav connection string **svého** Windows SQL Serveru:
   ```powershell
   dotnet user-secrets set "ConnectionStrings:PmTrackerDb" `
     "Server=HOSTNAME\INSTANCE;Database=PM_Tracker_VYVOJ;User Id=<ucet>;Password=<heslo>;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True" `
     --project PmTracker.Web
   ```
4. User secrets se uloží do `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`
   na tomhle stroji — žádný jiný stroj je nevidí, v repu nejsou.
5. Spusť aplikaci (přes `dotnet run` nebo deployuj do IIS) — přihlásí se na Windows
   SQL Server pod těmito credentials.

## Další secrets, které se běžně nastavují lokálně

| Klíč v User Secrets | Účel |
|---|---|
| `ConnectionStrings:PmTrackerDb` | Hlavní DB aplikace |
| `ConnectionStrings:TicketingReadOnly` | ServiceDesk read-only DB (až bude napojená) |
| `PmTracker:ActiveDirectory:Password` | AD service account (pokud LDAP vyžaduje) |
| `PmTracker:Search:Password` | OpenSearch basic auth (jen pokud Provider=OpenSearch) |

Každý klíč odpovídá cestě v `appsettings.json` oddělené dvojtečkou (`:`).

## Bezpečnostní pravidla

- **Nikdy** nezapisuj skutečná hesla do `appsettings.json` ani `appsettings.Development.json`
  — jsou v gitu nebo se do něj můžou omylem dostat.
- `appsettings.Development.json` je v `.gitignore` (respektive `git check-ignore` ho blokuje
  od tracked stavu) a obsahuje docker/localhost defaulty. Neupravuj ho pro svůj server
  — používej User Secrets.
- Pokud omylem zacommituješ heslo: **okamžitě rotuj**, pak `git filter-repo`/`BFG` na
  přepsání historie a force-push po konzultaci s týmem.

## Ochrana v CI: architecture test

Repo obsahuje [`AppSettingsCredentialLeakGuardTests`](../../PmTracker.Tests.Unit/Infrastructure/AppSettingsCredentialLeakGuardTests.cs)
který na každý build kontroluje, že `appsettings.json` neobsahuje:

- Neprázdné heslo v connection stringu (`Password=` následované jinou než whitespace hodnotou)
- Neprázdný `User Id=` s non-placeholder hodnotou
- Neprázdné API klíče v sekcích jako `Search.Embeddings.ApiKey`

Pokud někdo omylem zacommituje secret, build padne a PR se nedostane do main.

## Produkční nasazení

V produkci se secrets spravují přes:
- **Environment variables** (`ConnectionStrings__PmTrackerDb=...` — double underscore místo
  dvojtečky pro prostředí, která nepodporují speciální znaky v env names)
- **IIS Web.config** `<environmentVariables>` sekce (pro Windows IIS)
- **Azure Key Vault** / **AWS Secrets Manager** (pokud se projde k cloud deploymentu)

User Secrets jsou **pouze pro Development** prostředí — v produkci je ASP.NET Core
automaticky ignoruje.
