# 14. Lokální konfigurace vývojového prostředí

## Princip: appsettings + User Secrets (ne plaintext v repu)

Repo obsahuje `PmTracker.Web/appsettings.json` s **placeholder / neutrálními** hodnotami
pouze jako **strukturu a default**. Skutečné credentials (connection stringy, API klíče,
hesla) se do gitu **nikdy** necommitují. Každý vývojář si je nastaví lokálně přes
**.NET User Secrets Manager**.

## Proč User Secrets

- Secrets leží v `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json` (macOS/Linux)
  nebo `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json` (Windows) — **mimo repo**
- ASP.NET Core je v `Development` prostředí automaticky načítá (order po `appsettings.Development.json`,
  ale PŘED environment variables)
- Žádný gitignore trik není potřeba — `appsettings.json` v repu má **kompletní strukturu**,
  User Secrets ho overridují jen v místech, kde potřebují
- Oproti přepisování `appsettings.json` lokálně: není riziko commitu skutečných credentials,
  schéma v repu se udržuje aktuální automaticky

## První setup — nový stroj / nový vývojář

1. Naklonuj repo, otevři kořenový adresář
2. Ověř, že `PmTracker.Web/PmTracker.Web.csproj` obsahuje `<UserSecretsId>` (je commitnuté v repu).
   Pokud ne (fresh repo), spusť:
   ```bash
   dotnet user-secrets init --project PmTracker.Web
   ```
3. Nastav svůj connection string (každý vývojář má svůj server):
   ```bash
   dotnet user-secrets set "ConnectionStrings:PmTrackerDb" \
     "Server=VLASTNI_SERVER;Database=PM_Tracker_VYVOJ;User Id=<ucet>;Password=<heslo>;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True" \
     --project PmTracker.Web
   ```
4. Ověř výpis (zobrazí se maskované):
   ```bash
   dotnet user-secrets list --project PmTracker.Web
   ```
5. Spusť aplikaci — měla by startovat proti tvému serveru bez dalšího zásahu.

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
