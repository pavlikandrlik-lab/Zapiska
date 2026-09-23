# Přihlašování uživatele a Active Directory

> **Stav: 🟢 mapa harvestu hotová; kód vložen do P1 (identita) a využit v P2.**

## Co modul v Zápisce dělá

- Autentizace přes **Windows Authentication** na IIS (`IISDefaults.AuthenticationScheme`).
- Přihlášeného uživatele mapuje na osobu v aplikační databázi přes **technický identifikátor
  z AD** (`Guid_AD`). Bez tohoto mapování je uživatel autentizovaný, ale bez identity a práv.
- Onboarding uživatelů je **explicitně řízený administrací** — AD účty se nezakládají samy.
- Nad rámec přihlášení modul umí:
  - **vyhledávání osob v AD** (pro zakládání a doplňování osob),
  - **čtení atributů z AD** (jméno, příjmení, e-mail, útvar, telefon…),
  - **periodickou synchronizaci** na pozadí,
  - **reaktivní synchronizaci** při události,
  - **administraci synchronizačního jobu** s nastavením v databázi.

## Zdroje k harvestu ze Zápisky

| Soubor | Obsah |
|---|---|
| `PmTracker.Web/Services/ActiveDirectory/ADConnector.cs` | Nízkoúrovňové připojení k AD |
| `PmTracker.Web/Services/ActiveDirectory/ActiveDirectoryService.cs` | Vyhledávání a čtení atributů |
| `PmTracker.Web/Services/ActiveDirectory/ActiveDirectoryModels.cs` | Tvar dat vracených z AD |
| `PmTracker.Web/Services/ActiveDirectory/ActiveDirectoryOptions.cs` | Konfigurace připojení |
| `PmTracker.Web/Services/ActiveDirectory/AdSyncService.cs` | Vlastní synchronizace |
| `PmTracker.Web/Services/ActiveDirectory/AdPeriodicSyncHostedService.cs` | Běh na pozadí |
| `PmTracker.Web/Services/ActiveDirectory/AdReactiveSyncConsumer.cs` | Synchronizace na událost |
| `PmTracker.Web/Services/ActiveDirectory/AdSyncJobAdminHandler.cs` | Administrace jobu |
| `PmTracker.Web/Middleware/UserContextMiddleware.cs` | Naplnění identity do requestu |
| `PmTracker.Web/Services/Security/UserContextResolver.cs` | Překlad AD identity na osobu |
| `PmTracker.Web/Services/Security/CurrentUserAccessor.cs` | Přístup k aktuálnímu uživateli |
| `db_upgrade_1_3_4_ad_sync_settings.sql` | Tabulky nastavení synchronizace |
| `docs/wiki/integrace/active-directory/` | Uživatelská dokumentace modulu |
| `PmTracker.Tests.Unit/ActiveDirectory/` | Testy modulu |

## Co je nutné rozhodnout, než se kód vloží

- **A6** — zůstává Windows Authentication, nebo se přechází na jiný způsob přihlášení?
- **A3** — poběží aplikace na IIS? Windows Auth mimo IIS/Windows hosting nefunguje stejně.
- Jak se identita předá do React frontendu (A2) — v Razoru se čte serverově.
