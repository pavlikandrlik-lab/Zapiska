# PM Tracker — Technická dokumentace 13: Zapnutí ServiceDesk integrace

## 1. Účel

Postup, jak aktivovat ServiceDesk integraci v produkci (nebo vývoji) — tj. zapnout čtení z cizí MS SQL databáze `intranetNEW` pro:

- **Harvest vyjádření** (chat modal u externích záznamů).
- **Dashboard NES v prodlení** (projektový dashboard, záložka NES — aktivace v následném sprintu).
- **Budoucí features** (dropdown IS při nastavení projektu, rozpočet IS, výzvy).

Tento dokument **neobsahuje konkrétní heslo ani hostname** — to patří do `appsettings.json` lokálně nebo do secret store.

## 2. Předpoklady

- MS SQL Server 2012+ s databází `intranetNEW` (produkční ServiceDesk DB).
- Servisní účet s rolí `db_datareader` na `intranetNEW` (read-only). Tvůrce integrace by měl dále zvážit omezení práv **jen na schéma `dbo`** a **jen na tabulky `HOT_*`**, ale to je nad rámec PM Trackeru — je to security hardening v DB vrstvě.
- Síťová viditelnost z hostitele PM Tracker aplikace do SD SQL Serveru (firewall, SQL port 1433 nebo jiný custom).
- Šablona connection stringu je v `PmTracker.Web/appsettings.example.json`.

## 3. Konfigurace (krok za krokem)

### 3.1 Naplnit connection string

V `PmTracker.Web/appsettings.json` (produkce) nebo `PmTracker.Web/appsettings.Development.json` (dev) uprav sekci `ConnectionStrings.TicketingReadOnly`:

```jsonc
{
  "ConnectionStrings": {
    "PmTrackerDb": "...",
    "TicketingReadOnly": "Server=<SD_SQL_HOST>\\<INSTANCE>;Database=intranetNEW;User Id=<READONLY_USER>;Password=<PASSWORD>;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;ApplicationIntent=ReadOnly"
  }
}
```

**Doplň tyto hodnoty:**

| Placeholder | Co doplnit | Poznámka |
|---|---|---|
| `<SD_SQL_HOST>` | hostname nebo IP SQL Serveru (např. `SQLPROD01`) | Pokud SQL není default instance, přidej `\<INSTANCE>`. |
| `<INSTANCE>` | SQL instance (např. `VYVOJ`, `SQLEXPRESS`) | Pokud default instance, `<SD_SQL_HOST>` uveď bez `\\` a celého suffixu. |
| `<READONLY_USER>` | SQL login servisního účtu | Ideálně dedikovaný `pm_tracker_readonly` nebo podobný. |
| `<PASSWORD>` | heslo | Speciální znaky citlivé na JSON escape (`"` → `\"`), escapování se řeší při vyplňování. |

**Volitelné úpravy:**

- `Encrypt=True;TrustServerCertificate=True` — defaultní kombinace pro TLS s self-signed certifikátem. Pokud SD SQL Server má plně validní cert podepsaný CA, `TrustServerCertificate` vyraď.
- `ApplicationIntent=ReadOnly` — hint pro SQL Server (pokud AlwaysOn AG) preferovat read-only repliku. Bezpečný fallback na primárku při absenci repliky.
- `MultipleActiveResultSets=True` — EF Core s async dotazy to rád; nech.

### 3.2 Zapnout feature flag

V téže sekci `appsettings.json`:

```jsonc
{
  "Ticketing": {
    "Enabled": true,            // ← zapnuto (default: false)
    "ConnectionStringName": "TicketingReadOnly",
    "CommandTimeoutSeconds": 30
  }
}
```

`Enabled: true` způsobí, že DI kontejner zaregistruje:

- `ITicketingQueryService` → `CachingTicketingQueryService(SqlTicketingQueryService)`
- `IVyjadreniQueryService` → `SqlVyjadreniQueryService`
- `IInformacniSystemQueryService` → `SqlInformacniSystemQueryService`

Při `Enabled: false` se registrují `Disabled*QueryService` varianty, které vrací prázdné výsledky / `null`. Aplikace nespadne, jen SD funkce vrací „prázdno".

### 3.3 Ověření startu

Po nasazení zkontroluj aplikační log při startu:

1. Očekávaná úspěšná registrace: žádná `InvalidOperationException` s hláškou „ServiceDesk integrace je zapnutá (Ticketing:Enabled=true), ale ConnectionString 'TicketingReadOnly' je prázdný."
2. První běh `SdActivePeriodicSyncHostedService` (perioda 60 min; catch-up na startu) — měl by se objevit log `[sd.active] tick Auto`.
3. Chat modal u externího záznamu s napojeným SD ticketem — vyjádření se harvestují.

### 3.4 Smoke test v UI

1. Přihlas se jako uživatel s `PermissionKeys.RecordsEdit`.
2. Otevři záznam, který má externí vazbu s 6-ciferným SD ticket ID.
3. Klikni na chat bubble ikonu (modal otevře) → očekávaně se zobrazí vyjádření jako plain-text bubliny (po commit `98a020d` je HTML sanitizováno na plain text s newlines).
4. Po dokončení harvest batch se ve stavové řádce modalu objeví `Naposled syncnuto: <timestamp>`.

## 4. Rollback

Pokud se po nasazení objeví problémy (connectivity, perf, chyba v mapování):

1. V `appsettings.json` přepni `Ticketing.Enabled` na `false`.
2. Restartuj aplikaci.
3. Aplikace přejde do „degraded" režimu — chat modal zobrazí prázdný seznam vyjádření, NES panel placeholder, DropdownIS v editaci projektu vrátí prázdný seznam.

Žádná data v PmTracker DB se při rollbacku neztratí — harvest je read-only, zápisuje jen do vazebných tabulek (`zaznam_harmonogram_vyjadreni_vazba`). Ty zůstanou jako snapshot poslední úspěšné synchronizace.

## 5. Bezpečnostní pravidla

1. **Servisní účet má POUZE `db_datareader`** na `intranetNEW`. Nikdy `db_owner` ani write role.
2. **Žádný PII / reálná data** se z `intranetNEW` nepropisuje do logů PM Trackeru. Log line by měl obsahovat jen agregáty (`ticketsProcessed=12, bindingsUpdated=3`), nikdy e-maily, telefony, loginy, jména.
3. **Heslo v `appsettings.json`** — v produkci preferuj environment variables nebo secret store (Azure Key Vault, HashiCorp Vault). ASP.NET Core `IConfiguration` hierarchie: environment variable `ConnectionStrings__TicketingReadOnly` přepíše JSON.
4. **Read-only guard dvojitý:** `ApplicationIntent=ReadOnly` v connection stringu (SQL-side) + `TicketingReadOnlyDbContext.SaveChanges()` throw (app-side). DDL by stejně nepoškodil schéma, ale explicitně to blokujeme.
5. **Žádné konkrétní IDy / PIDy / částky** v git-tracked souborech nebo v chatech. Memory pravidlo `project_servicedesk_infosystem_binding.md` (Bezpečnostní pravidlo §1).

## 6. Troubleshooting

| Symptom | Pravděpodobná příčina | Řešení |
|---|---|---|
| Exception při startu: „ConnectionString 'TicketingReadOnly' je prázdný" | `Enabled: true` ale string je `""` | Vyplň connection string nebo přepni na `Enabled: false`. |
| `TimeoutException` při prvním dotazu | Firewall blokuje SQL port, nebo SQL Server neběží | Ověř `telnet <HOST> 1433`, zkontroluj service status SQL Serveru. |
| `LoginFailedException` | Špatný login/heslo, nebo chybí mapování na DB | V SSMS: `CREATE USER ... FOR LOGIN ...; EXEC sp_addrolemember 'db_datareader', '...'`. |
| `InvalidCastException` na sloupci `HOT_ZAZNAMY.splneno` | Někdo změnil DB schéma (smalldatetime → jiný typ) | Po opravě Bug #1 (commit `cd0442af`) je v entitě `DateTime?` + `HasColumnType("smalldatetime")`. Ověř, že DB skutečně má `smalldatetime`. |
| Chat modal zobrazí literal `<b>` místo tučného textu | `PopisPlainText` nezavolán, nebo view používá `@bubble.Popis` (starý escape) | Po commit `98a020d` je view `_ChatModal.cshtml:98` opravený na `@bubble.PopisPlainText`. Pokud je to jiná view, aplikuj stejný pattern. |
| Chat bubliny v různém pořadí mezi requesty | Pre-commit `52fee95` — `SqlVyjadreniQueryService` řazené jen dle `datum` | Po commit `52fee95` je `ThenBy(Id)` zajištěný tie-break. |
| „Číslo ticketu musí být přesně 6 cifer (např. 123456)." | User zadal do pole `ExterniVazby[N].Cislo` neplatný formát (<6 cifer, >6, písmena) | ServiceDesk HOT_ZAZNAMY.id je vždy 6-místný integer. Zkontroluj typo ve formuláři editoru záznamu. Memory: `feedback_sd_ticket_id_required.md`. |
| „Ticket #123456 v ServiceDesku neexistuje — externí vazbu nelze založit." | Zadané 6-ciferné číslo v HOT_ZAZNAMY skutečně není (typo, archivní tiket, chybné ID) | Ověř v SD UI (`https://servicedesk.fis.acr/Hotline/Ticket/Details/123456`), že ticket existuje. Pokud jde o archivní tiket, diskuze je mimo rozsah hard constraintu (viz Plán 3 Feature D). |
| „SD integrace je vypnutá — novou externí vazbu nelze založit." | `Ticketing:Enabled=false` v `appsettings.json` | Přepni na `true` a restartuj app pool; případně pracuj offline bez založení nových SD vazeb (stávající záznamy migrace neruší). |
| „ServiceDesk není dostupný — nelze ověřit existenci ticketu." | Network / SD SQL server down / timeout | Checkni connectivity (`telnet <HOST> 1433`) + SD SQL service status. Chyba je transientní; user zkusí save znovu za chvíli. Log obsahuje warning s kompletním exception stackem (viz `ExterniOdkazValidator`). |

## 7. Související dokumentace

- [docs/technical/12-servicedesk-schema-reference.md](12-servicedesk-schema-reference.md) — referenční DB schema ServiceDesku.
- [docs/superpowers/specs/2026-04-21-servicedesk-vytezovani-vyjadreni-design.md](../superpowers/specs/2026-04-21-servicedesk-vytezovani-vyjadreni-design.md) — spec vytěžování vyjádření (text matching predikátů).
- [docs/superpowers/specs/2026-04-22-sync-infra-and-ad-design.md](../superpowers/specs/2026-04-22-sync-infra-and-ad-design.md) — sdílená sync infra (`SyncHostedServiceBase`, `SdActivePeriodicSyncHostedService`).
- [docs/superpowers/plans/2026-04-23-sd-integrace-sprint-a-backend.md](../superpowers/plans/2026-04-23-sd-integrace-sprint-a-backend.md) — implementační plán Sprintu A (tento sprint).
