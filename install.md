# PM Tracker - Instalační dokument (vstupní)

Tento soubor je vstupní instalační dokument. Kompletní detail je rozdělen do technické dokumentace v `docs/technical/*`.

## 1) Rychlé odkazy
- Strom dokumentace a pokrytí uzlů: `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/technical/00-documentation-tree.md`
- Systémový kontext: `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/technical/01-system-context.md`
- Runtime konfigurace: `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/technical/03-runtime-configuration.md`
- Instalace a deployment (IIS): `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/technical/04-installation-deployment-iis.md`
- Konfigurace web serveru: `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/technical/05-web-server-iis-config.md`
- Databáze a migrace: `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/technical/06-database-bootstrap-migrations.md`
- Provozní runbooky: `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/technical/08-operations-runbooks.md`
- Troubleshooting a recovery: `/Users/Pavel.Andrlik/Documents/PM Tracker/docs/technical/10-troubleshooting-recovery.md`

## 2) Instalační minimum (souhrn)
1. Build/publish aplikace (`dotnet publish`).
2. Přenos publish artefaktu na IIS server.
3. SQL bootstrap (`PMTracker_insert_sql`) a případné upgrade patch skripty.
4. Nastavení `appsettings.Production.json`.
5. IIS konfigurace (No Managed Code, Windows Authentication).
6. Restart IIS a smoke test.
7. Založení osob v `dbo.osoby` je ruční krok (AD sync není automatický).

## 3) Povinné artefakty pro deploy
- Publish výstup (`publish/fdd/*`).
- SQL baseline a upgrade skripty.
- `appsettings.Production.json` se správným `ConnectionStrings:PmTrackerDb`.

## 4) Ověření po nasazení
- Route: `/Projekty`, `/Jednani`, `/Ciselniky`, `/Nastaveni`.
- Přihlášení přes Windows Auth.
- Export PDF/Word.
- Kontrola práv na test účtu.

## 5) Poznámka ke změnám
Detailní SOP postupy jsou závazně vedené v `docs/technical/*` a in-app pod `/Dokumentace/Technicka/*`.

## 6) Globální vyhledávání (OpenSearch)

Od verze 1.1.5 je v aplikaci připraveno globální vyhledávání nad projekty, záznamy, jednáními, osobami, subsystémy, vyjádřeními a návrhy záznamů. Ve výchozím stavu je **vypnuté** a aktivuje se přes konfiguraci.

### 6.1) Předpoklady
- Běžící OpenSearch 2.x (single-node stačí).
- SQL migrace `db_upgrade_1_1_5_search_checkpoint.sql` aplikovaná v DB.

### 6.2) Lokální OpenSearch pro vývoj
```
docker run -d --name pmtracker-opensearch -p 9200:9200 \
  -e discovery.type=single-node \
  -e DISABLE_SECURITY_PLUGIN=true \
  opensearchproject/opensearch:2.15.0
```

### 6.3) Konfigurace v `appsettings.Production.json`
```json
"PmTracker": {
  "Search": {
    "Enabled": true,
    "Provider": "OpenSearch",
    "Uri": "http://opensearch-host:9200",
    "Username": "",
    "Password": "",
    "IndexName": "pmtracker-search-v1",
    "ReindexIntervalSeconds": 30,
    "PostFilterCandidateMultiplier": 3,
    "Embeddings": {
      "Enabled": false,
      "Provider": "None",
      "Endpoint": "",
      "ApiKey": "",
      "Model": "text-embedding-3-small",
      "Dimensions": 384
    }
  }
}
```
- `Enabled=false` → UI prvek se skryje a všechny služby jsou no-op.
- `Embeddings.Enabled=true` vyžaduje Azure OpenAI endpoint + klíč (provider `AzureOpenAI`).

### 6.4) První naplnění indexu
Po zapnutí feature flagu spusť jako superadmin:
```
POST /Search/Reindex
```
Endpoint iteruje všechny entity a nahraje je do OpenSearch v dávkách po 500. Inkrementální aktualizace pak běží automaticky každých `ReindexIntervalSeconds` přes audit log.

### 6.5) Ověření
- `GET http://opensearch-host:9200/pmtracker-search-v1/_count` – počet dokumentů.
- V navbaru aplikace se objeví vyhledávací pole; stránka `/Search?q=...` vrací výsledky seskupené podle typu entity.
- Výsledky jsou filtrovány post-filter přes `IPermissionEvaluationService` – uživatel nikdy neuvidí entitu mimo svůj scope.
