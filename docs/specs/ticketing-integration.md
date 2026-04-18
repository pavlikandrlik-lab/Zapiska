# Specifikace — integrace s ticketovacím systémem

**Stav:** rozpracováno (fáze 3 implementace)
**Závisí na:** fáze 1 (základy) dokončena

## Kontext

PM Tracker potřebuje ze stávajícího ticketovacího systému získávat data o ticketech pro:

1. **Automatické vytěžování stavů** — skutečnost harmonogramu
   (viz [automat-vytezovani-vyjadreni.md](automat-vytezovani-vyjadreni.md))
2. **Dashboard prodlení** — NES / PMP / PNF
   (viz [dashboard-prodleni.md](dashboard-prodleni.md))
3. **Zobrazení vyjádření ticketů** — modal „externí vazby" v editoru záznamu

Starý ticket systém je postaven na ASP (Classic ASP) a nelze jej rozšiřovat. Rozhraní je
proto **read-only čtení z DB přes servisní účet**. Nový systém plánovaný v řádu let —
integrace musí být **tolerantní k budoucí výměně zdroje**.

## Architektura

### Zdroj dat

- **Primární:** SQL Server databáze starého ticketing systému
- **Sekundární:** databáze „zobrazovače" (drží SLA termíny, zpoždění)
- **Přístup:** read-only servisní účet, samostatný connection string
  (`appsettings.json` → `ConnectionStrings:TicketingReadOnly`)

### Druhý DbContext

V `PmTracker.Data/TicketingReadOnlyDbContext.cs` (fáze 3):

```
public sealed class TicketingReadOnlyDbContext : DbContext
{
    public DbSet<TicketRow> Tickets { get; }
    public DbSet<TicketVyjadreniRow> Vyjadreni { get; }
    // ... podle schématu ticketingu
}
```

- **Read-only**: žádné `SaveChangesAsync`, `ChangeTracker.AutoDetectChangesEnabled = false`,
  query splitting `AsNoTracking()` by default
- **Servisní účet** s rolí `db_datareader`
- **Entity mapovány** do `PmTracker.Ticketing.Contracts` DTO — žádná přímá expozice
  ticketing schématu do doménové vrstvy (izolace proti změně schématu)

### Recurring job — Hangfire

PM Tracker používá **Hangfire** pro integrační joby. Důvod: enterprise standard, dashboard
`/hangfire` pro admina (retry, failed jobs, history), distribuovaný zámek (pokud web poběží
ve farmě), DB persistence konfigurace jobů.

**Job:** `TicketingDeltaSyncJob`

- Polling interval: **výchozí 5 min**, konfigurovatelné v UI `/Nastaveni/Integrace`
- Přečte **delta změny ve vyjádřeních** od posledního běhu (kolonka `LastSyncedAt`
  v tabulce `TicketingIntegrationState`)
- Pro každé nové vyjádření zavolá `IVytezovaniService.EvaluateNew(ticketId, vyjadreniId)`
  — viz [automat-vytezovani-vyjadreni.md](automat-vytezovani-vyjadreni.md)
- Poznačí `LastSyncedAt` až po **úspěšném** dokončení zpracování
- Failure → Hangfire retry (exponential backoff, 3 pokusy, poté alert)

**Jiné joby v budoucnu:**
- `TicketingArchivePurgeJob` (denně) — odstraní cache ticketů, které přešly do archivu
- `TicketingMetricsSyncJob` (hodinově) — přepočet SLA/prodlení ze zobrazovače

### Konfigurace

`appsettings.json`:
```json
{
  "ConnectionStrings": {
    "TicketingReadOnly": "Server=...;Database=...;User Id=pmtracker_ro;..."
  },
  "Ticketing": {
    "Enabled": true,
    "DefaultPollingIntervalSeconds": 300,
    "MaxBatchSize": 500,
    "FailureAlertEmail": "admin@acr.cz"
  },
  "Hangfire": {
    "StorageConnection": "... (pmtracker DB) ..."
  }
}
```

Runtime override (admin UI `/Nastaveni/Integrace`):
- Polling interval (30s – 24h)
- Job enabled/disabled toggle
- Max batch size (pro throttling)

Hangfire ukládá stav recurring jobů v PM Tracker DB — **jeden zdroj pravdy**, ne duplicita
mezi appsettings a DB.

### Monitoring

- **Hangfire dashboard** `/hangfire` — přístup jen pro `SuperAdmin` (authorization filter)
- **Log** přes `ILogger<TicketingDeltaSyncJob>` → standardní PM Tracker logovací pipeline
- **Alert** při 3× selhání v řadě → e-mail administrátorovi

## Datový model (PM Tracker strana)

Nové tabulky v PM Tracker DB (fáze 3 migrace):

### `TicketCache` (nepovinné)

Cache ticketů z ticketing systému pro rychlé čtení.

| Sloupec | Typ | Popis |
|---|---|---|
| `TicketId` | char(6) | PK, identifikátor z ticketing |
| `Typ` | varchar(10) | NES / PMP / PNF |
| `Nazev` | nvarchar(200) | Stručný název |
| `SubsystemKod` | varchar(50) | FK na subsystém |
| `ModulKod` | varchar(50) | Modul |
| `Verze` | varchar(20) | Verze modulu |
| `Zakladatel` | nvarchar(100) | Autor |
| `VytvorenoDne` | datetime2 | Datum založení |
| `JeArchivovan` | bit | Status z ticketing |
| `PosledniSync` | datetime2 | Kdy byl cache aktualizován |

### `TicketVyjadreniCache`

Cache vyjádření ticketů.

| Sloupec | Typ | Popis |
|---|---|---|
| `Id` | bigint | PK |
| `TicketId` | char(6) | FK |
| `VyjadreniId` | bigint | ID v ticketing DB |
| `Datum` | datetime2 | Datum vyjádření |
| `Autor` | nvarchar(100) | Autor vyjádření |
| `Text` | nvarchar(max) | Text (XML / richtext) |
| `Hash` | char(64) | SHA-256 textu pro detekci změn |

### `TicketVyjadreniTag`

Tagy přiřazené k vyjádřením ticketů (automat + manuál).

| Sloupec | Typ | Popis |
|---|---|---|
| `Id` | bigint | PK |
| `TicketVyjadreniCacheId` | bigint | FK |
| `TagKod` | varchar(50) | Kód stavu/kroku harmonogramu |
| `ZdrojEnum` | tinyint | 1=Automat, 2=Manual |
| `VytvorenoDne` | datetime2 | |
| `VytvorilOsobaId` | int nullable | NULL u automatu |

**Poznámka:** skutečnost harmonogramu se počítá z **existence tagů**, ne z přímé editace
v PM Tracker. Viz [harmonogram-plan-vs-skutecnost.md](harmonogram-plan-vs-skutecnost.md).

### `TicketingIntegrationState`

Stav posledního běhu sync jobu.

| Sloupec | Typ | Popis |
|---|---|---|
| `Id` | int | PK |
| `LastSyncedAt` | datetime2 | Kdy naposled úspěšně doběhl sync |
| `LastSuccessfulTicketId` | char(6) nullable | |
| `FailureCount` | int | Počet po sobě jdoucích selhání |
| `LastError` | nvarchar(max) nullable | Text poslední chyby |

## Bezpečnostní aspekty

1. **Read-only servisní účet** — PM Tracker nemá žádné UPDATE/INSERT/DELETE právo
   na ticketing DB (ani omylem)
2. **Connection string** v `appsettings` je **chráněn**: produkce používá `Secret Manager`
   nebo Azure Key Vault, **nikdy commit do repozitáře**
3. **ACL**: tickety jsou přístupné uživatelům podle mapování
   projekt → subsystém (stejný pattern jako stávající ACL v PM Tracker)
4. **Audit log**: každá manuální změna tagu (v PM Tracker) loguje
   `TicketVyjadreniTag.VytvorilOsobaId` + timestamp

## Testy (fáze 3)

- **Unit**: `TicketingDeltaSyncJobTests` — mocked `TicketingReadOnlyDbContext`, ověření delta logiky
- **Integration**: `TicketingIntegrationTests` — Testcontainers s SQL Server, seedovaná ticketing-like DB
- **Failure scenarios**: connection timeout, servisní účet bez oprávnění, malformed XML vyjádření

## Otevřené otázky

| # | Otázka | Kdo rozhodne | Deadline |
|---|---|---|---|
| T1 | **Přesné schéma ticketing DB** — tabulky, sloupce, vztahy | Ing. Andrlík (zpracování dnů) | T+7 dní |
| T2 | XML struktura vyjádření — DTD / XSD? | Ing. Andrlík | T+7 dní |
| T3 | Přístup k „zobrazovači" DB — schéma, connection string | Ing. Andrlík | T+14 dní |
| T4 | Polling interval výchozí — 5 min stačí, nebo kratší? | Vedení / uživatelé | Před nasazením |
| T5 | Alert email — adresát na selhání jobu | Admin | Před nasazením |
| T6 | Retention pro `TicketCache` — držet archivované tickety, nebo smazat? | Vedení | Před nasazením |

## Zodpovědnost

- **Technický návrh:** Claude + implementace dodavatel
- **Schéma ticketing DB:** Ing. Andrlík (odkaz na DBA ticketing systému)
- **Fráze pro vytěžování:** vedení (viz [automat-vytezovani-vyjadreni.md](automat-vytezovani-vyjadreni.md))
- **Přístup k servisnímu účtu:** IT oddělení
