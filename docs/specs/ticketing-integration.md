# Specifikace — integrace s ticketovacím systémem

**Stav:** ⚠️ **ZASTARALÝ** k 2026-04-23 — viz banner níže. Ponechán jako audit trail původního návrhu.
**Závisí na:** fáze 1 (základy) dokončena
**Nahrazuje / doplňuje:** [2026-04-22-sync-infra-and-ad-design.md](../superpowers/specs/2026-04-22-sync-infra-and-ad-design.md), [2026-04-21-servicedesk-vytezovani-vyjadreni-design.md](../superpowers/specs/2026-04-21-servicedesk-vytezovani-vyjadreni-design.md), [2026-04-22-sd-sync-revise.md](../superpowers/plans/2026-04-22-sd-sync-revise.md)

> ## ⚠️ ZASTARALÝ — NEPOUŽÍVAT JAKO ZDROJ PRAVDY
>
> **Tento spec je z velké části neaktuální** (aktualizováno 2026-04-23 po auditu stavu kódu).
> Značná část architektury je **již implementovaná v kódu**, jinde a jinak, než spec předpokládá.
> Pro jakoukoli novou práci nejdřív viz **Source of truth** níže.
>
> ### Co je už v kódu hotové
>
> Projekty **`PmTracker.ServiceDesk.Sql/`** a **`PmTracker.ServiceDesk.Contracts/`** (ne `PmTracker.Data/` jak spec tvrdí):
>
> - `TicketingReadOnlyDbContext` — read-only DbContext s hard guardem na `SaveChanges*` a DI toggle `Ticketing:Enabled`
> - `SqlTicketingQueryService`, `SqlVyjadreniQueryService` — EF Core queries nad `HOT_*` tabulkami
> - `CachingTicketingQueryService` — in-memory dekorátor (ne Hangfire!)
> - `DisabledTicketingQueryService`, `DisabledVyjadreniQueryService` — fallbacky když je integrace vypnutá
> - Entity: `HotZaznamEntity` (10 z 39 sloupců), `HotVyjadreniEntity`, `HotKalkulaceEntity`
> - DTO: `HotZaznamDto`, `HotVyjadreniDto`, `HotKalkulaceDto`
> - DI registrace: `AddServiceDeskIntegration(IConfiguration)` v `ServiceDeskServiceCollectionExtensions`
> - Konfigurace: `TicketingOptions` (pattern `Ticketing:ConnectionStringName`, default `TicketingReadOnly`)
>
> ### Které otevřené otázky jsou zodpovězené
>
> - **T1 (schéma ticketing DB)** — **zodpovězeno**. DB = `intranetNEW` (MS SQL), tabulky `dbo.HOT_*`. Kompletní inventář v [SD_servicedesk/hotline.txt](../../SD_servicedesk/hotline.txt). Referenční BusinessLayer implementace z původní aplikace v [SD_servicedesk/Hotline.cs](../../SD_servicedesk/Hotline.cs) (2089 řádků, penalizační logika, workflow eventy U1–U11).
> - **T3 (zdroj SLA termínu)** — **zodpovězeno**. Žádná samostatná DB "zobrazovače" neexistuje. `sla_deadline` je sloupec přímo na `HOT_ZAZNAMY`. To mění doporučení v sekci "Datový zdroj" (Varianta A/B neplatí).
>
> ### Co ze specu **přestalo platit**
>
> - **Hangfire** — **NEPOUŽÍVAT**. Spec [2026-04-22-sync-infra-and-ad-design.md](../superpowers/specs/2026-04-22-sync-infra-and-ad-design.md) zavádí vlastní infrastrukturu `PmTracker.Web.Services.Sync` (`SyncHostedServiceBase<T>`, `IReactiveSyncQueue<T>`, `ReactiveSyncConsumerBase<T>`) nad `BackgroundService` + `Channel<T>` + `TimeProvider`. Všechny Hangfire zmínky v tomto specu (sekce "Recurring job", `TicketingDeltaSyncJob`, `TicketingArchivePurgeJob`, `TicketingMetricsSyncJob`, Hangfire dashboard `/hangfire`) jsou **nahrazeny** touto infrastrukturou.
> - **Umístění v `PmTracker.Data`** — kód je v `PmTracker.ServiceDesk.Sql`, izolovaný od hlavní DB vrstvy (správné rozhodnutí).
> - **Hangfire admin UI na `/hangfire`** — nahrazuje admin UI `/Nastaveni?section=synchronizace` ze sync-infra specu.
>
> ### Co ze specu **stále platí jako návrh**
>
> - Princip **read-only servisního účtu** s rolí `db_datareader`
> - **Separátní DbContext** a izolace přes DTO (implementováno přesně takto)
> - **DI toggle** `Ticketing:Enabled` (implementováno)
> - **Datový model** `TicketVyjadreniTag` (tagování pro vytěžování) — platí jako koncept, ale implementace patří do specu [2026-04-21-servicedesk-vytezovani-vyjadreni-design.md](../superpowers/specs/2026-04-21-servicedesk-vytezovani-vyjadreni-design.md)
> - **Bezpečnostní principy** (connection string v secret manageru, ACL přes mapování projekt→subsystém, audit log manuálních tagů)
>
> ### Co v kódu **stále chybí**
>
> Oblasti na které spec upozorňuje a kde je potřeba další práce:
>
> - Entity pro: `HOT_IS`, `HOT_SUBSYSTEM`, `HOT_MODULY`, `HOT_PID`, `HOT_DODAVATEL`, `HOT_VYJADRENI_TEXT`, `HOT_TYMY`, `HOT_IS_LIMIT`
> - Většina sloupců `HOT_ZAZNAMY` (29 z 39): `subsystem`, `modul`, `term_pl`, `rok`, `dulezitost`, `zavaznost`, `dodavatel`, `dat_res_t`, `dat_dod`, `priznak_zamceni`, `priznak_gdpr`, `utvar`, `zpracoval`, `schvalil`, `uzivatel`, `email`, atd.
> - Service metody pro IS hierarchii (IS → subsystémy → moduly) a NES filtraci pro dashboard
> - Workflow/transfer event tabulka (U1–U11) — v inventáři [hotline.txt](../../SD_servicedesk/hotline.txt) není, existence a umístění se musí ověřit
> - Sync job (bude postaven nad `SyncHostedServiceBase<T>` ze sync-infra specu)
>
> ### Source of truth
>
> - **Schéma ticketing DB (referenční):** [docs/technical/12-servicedesk-schema-reference.md](../technical/12-servicedesk-schema-reference.md) ⭐ — datové typy, enumové kategorie, join řetězce, pasti, definice prodlení per typ, architektura PM Tracker strany
> - **Kód:** [`PmTracker.ServiceDesk.Sql/`](../../PmTracker.ServiceDesk.Sql/), [`PmTracker.ServiceDesk.Contracts/`](../../PmTracker.ServiceDesk.Contracts/)
> - **DB schéma (inventář + reference app):** [SD_servicedesk/hotline.txt](../../SD_servicedesk/hotline.txt) + [SD_servicedesk/Hotline.cs](../../SD_servicedesk/Hotline.cs)
> - **Nová sync architektura:** [docs/superpowers/specs/2026-04-22-sync-infra-and-ad-design.md](../superpowers/specs/2026-04-22-sync-infra-and-ad-design.md)
> - **Vytěžování vyjádření:** [docs/superpowers/specs/2026-04-21-servicedesk-vytezovani-vyjadreni-design.md](../superpowers/specs/2026-04-21-servicedesk-vytezovani-vyjadreni-design.md)
> - **Implementační plán SD sync:** [docs/superpowers/plans/2026-04-22-sd-sync-revise.md](../superpowers/plans/2026-04-22-sd-sync-revise.md)
> - **Implementační plán Sprint A (backend pro dashboard):** [docs/superpowers/plans/2026-04-23-sd-integrace-sprint-a-backend.md](../superpowers/plans/2026-04-23-sd-integrace-sprint-a-backend.md)

---

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
