# PM Tracker - Technická dokumentace 03: Runtime konfigurace

## 1. Účel
Tento dokument popisuje runtime konfiguraci aplikace včetně všech povinných klíčů, jejich významu a bezpečného provozního nastavení.

## 2. Publikum a role
- Ops administrátor: konfigurace prostředí a deployment.
- Vývojář: lokální a testovací konfigurace.
- Bezpečnostní administrátor: validace přístupů a citlivých údajů.

## 3. Závislosti a předpoklady
- Konfigurace je načítána standardním .NET configuration pipeline.
- Aplikace očekává SQL provider a platný connection string.
- V produkci je očekáván `ASPNETCORE_ENVIRONMENT=Production`.

## 4. Vstupy a výstupy
### Vstupy
- `PmTracker.Web/appsettings.json`
- `PmTracker.Web/appsettings.Development.json`
- `PmTracker.Web/appsettings.Production.json`
- IIS environment variables

### Výstupy
- Deterministická konfigurace runtime bez fallbacku na neznámý provider.

## 5. Detailní postup
### 5.1 Povinné konfigurační klíče
```json
{
  "ConnectionStrings": {
    "PmTrackerDb": "Server=<SQL_SERVER>;Database=<DB_NAME>;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
  },
  "PmTracker": {
    "Data": {
      "Provider": "SqlServer",
      "SqlServer": {
        "ConnectionStringName": "PmTrackerDb",
        "CommandTimeoutSeconds": 60
      }
    },
    "ActiveDirectory": {
      "Domain": "<AD_DOMAIN>",
      "MaxResults": 15,
      "QueryTimeoutSeconds": 8
    }
  }
}
```

### 5.2 Environment-specific pravidla
- `Development`:
  - může používat SQL login pro lokální kontejner.
- `Production`:
  - preferuj `Trusted_Connection=True` se servisní identitou app poolu,
  - citlivé hodnoty neukládej do repozitáře.

### 5.3 IIS environment variables
Nastav minimálně:
- `ASPNETCORE_ENVIRONMENT=Production`

Volitelně pro diagnostiku při incidentu:
- dočasné zvýšení log levelu přes `appsettings.Production.json`.

### 5.4 Fail-fast očekávání
Aplikace má fail-fast režim při:
- nedostupném SQL serveru,
- chybějícím povinném seed baseline,
- nevalidní provider konfiguraci.

### 5.5 Active Directory režim (bez auto-sync)
- Sekce `PmTracker:ActiveDirectory` slouží pro doménový kontext a vyhledávání osob.
- Aplikace neprovádí automatickou synchronizaci AD účtů do `dbo.osoby`.
- Založení osoby pro přihlášení je řízený administrativní krok (ručně přes DB/provozní onboarding).
- Runtime endpoint pro vyhledání osob je `GET /Osoby/SearchAd` a slouží pouze k výběru osoby, ne k automatickému importu.

## 6. Verifikace
- Ověř parse a dostupnost `ConnectionStrings:PmTrackerDb`.
- Ověř, že provider je `SqlServer`.
- Ověř start aplikace po deployi bez runtime exception.
- Ověř, že AD konfigurace umožňuje vyhledávání osob v očekávaném doménovém kontextu.
- Ověř, že onboarding osoby proběhl ručně a existuje odpovídající `dbo.osoby.Guid_AD`.

## 7. Rollback
- Vrať `appsettings.Production.json` na poslední známou funkční verzi.
- Proveď recycle app poolu.
- Ověř dostupnost `/Projekty` a `/Dokumentace/Technicka/Runtime-konfigurace`.

## 8. Troubleshooting
- Chyba připojení k DB při startu:
  - ověř server, DB, credentials, firewall a práva app pool identity.
- AD lookup timeout:
  - sniž rozsah dotazu, ověř `Domain`, zvýš `QueryTimeoutSeconds` pouze po analýze.
- Neshoda konfigurace mezi prostředími:
  - porovnej jen klíče, ne tajné hodnoty; používej deployment checklist.

## 9. Audit a traceability
- Runtime bootstrap: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Program.cs`
- Konfigurace: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/appsettings*.json`
- Data provider registration: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs`
