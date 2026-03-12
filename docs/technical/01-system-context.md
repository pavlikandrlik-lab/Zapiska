# PM Tracker - Technická dokumentace 01: Systémový kontext

## 1. Účel
Tento dokument definuje systémový kontext aplikace PM Tracker (Zápiska) podle požadavků ISO 26514.
Cíl je sjednotit, co je součást systému, co je mimo systém, jaké jsou rozhraní a kdo je vlastník jednotlivých provozních odpovědností.
Rekurzivní strom všech oblastí a listových uzlů je veden v `docs/technical/00-documentation-tree.md`.

## 2. Publikum a role
| Role | Odpovědnost | Potřebný výstup |
| --- | --- | --- |
| Ops administrátor | Provoz IIS, monitoring, release cutover | Runbook pro nasazení a obnovu |
| DB administrátor | SQL bootstrap, patching, integrita dat | Ověřitelný SQL postup + rollback |
| Aplikační administrátor | Role, oprávnění, přístupy uživatelů | Postup správy authz modelu |
| Vývojář | Údržba aplikace a dokumentace | Přímé vazby docs -> source-of-truth |
| QA | Regresní a smoke validace | Test matrix a acceptance checklist |

## 3. Závislosti a předpoklady
- Aplikace běží jako ASP.NET Core 8 MVC monolit (`PmTracker.Web`) na IIS (in-process hosting).
- Datový provider je výhradně SQL Server (`PmTracker:Data:Provider=SqlServer`).
- Autentizace je navázána na Windows Identity a mapování uživatele přes `dbo.osoby.Guid_AD`.
- Výstup dokumentace je rozdělen na:
  - technickou dokumentaci (`docs/technical/*`),
  - uživatelskou příručku (`docs/user-guide.md`),
  - Q and A (`docs/qa.md`),
  - changelog (`docs/changelog/releases/*`, generovaný do `CHANGELOG.md`).

## 4. Vstupy a výstupy
### Vstupy
- Zdrojový kód: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web`.
- Databázové skripty: `/Users/Pavel.Andrlik/Documents/PM Tracker/PMTracker_insert_sql` a `db_upgrade_*.sql`.
- Runtime konfigurace: `PmTracker.Web/appsettings*.json`, IIS `web.config`.

### Výstupy
- Běžící aplikace na IIS.
- Inicializovaná databáze ve stavu odpovídajícím baseline.
- Auditovatelný záznam změn (release notes + provozní checklist).

## 5. Detailní postup
### 5.1 Kontextové hranice systému
- PM Tracker je interní webová aplikace pro:
  - evidenci projektů,
  - evidenci záznamů/úkolů,
  - evidenci jednání a účasti,
  - řízení oprávnění podle role + scope.
- Mimo systém jsou:
  - Active Directory infrastruktura,
  - SQL Server infrastruktura,
  - ServiceDesk (externí odkazy PMP/PNF/NES).

### 5.2 Integrace a jejich kontrakty
- IIS -> ASP.NET Core Module V2 (`publish/web.config`) spouští `dotnet .\\PmTracker.Web.dll`.
- Aplikace -> SQL Server přes `ConnectionStrings:PmTrackerDb`.
- Uživatel -> aplikace přes Windows Authentication; interní autorizace v tabulkách `authz.*`.
- AD -> aplikace: bez automatického sync procesu; vazba uživatele je explicitně přes ručně založené `dbo.osoby.Guid_AD`.

### 5.3 Provozní ownership
- Ops odpovídá za IIS hostování, certifikáty, environment variables a App Pool identitu.
- DBA odpovídá za bootstrap a SQL patching.
- Aplikační admin odpovídá za role/permissions/superadmins.
- Vývoj odpovídá za konzistenci `docs` se zdrojovým kódem.

### 5.4 Dokumentační mapování
- V aplikaci (`/Dokumentace`) jsou publikované markdowny z `DocsContent`.
- Source mapu drží `MarkdownDocumentationService`.
- Build/publish mapování markdownů do `DocsContent` drží `PmTracker.Web.csproj`.

## 6. Verifikace
- Ověř, že kontextové předpoklady odpovídají realitě:
  - provider je `SqlServer`,
  - app startuje přes IIS in-process,
  - autorizace běží přes `authz.*` tabulky,
  - dokumentace je dostupná přes `/Dokumentace/*` route.
- Ověř, že tato stránka je dostupná in-app a je uvedena v technické navigaci.

## 7. Rollback
- Pokud je dokumentační změna chybná:
  - vrať markdown soubor na předchozí git revizi,
  - znovu publikuj aplikaci,
  - ověř dostupnost `/Dokumentace/Technicka/Systemovy-kontext`.
- Pokud je chybná pouze navigace, rollbackni změny v `MarkdownDocumentationService` a `DokumentaceController`.

## 8. Troubleshooting
- Stránka dokumentace vrací 500:
  - ověř existenci souboru `DocsContent/technical/01-system-context.md` ve výstupu publish.
- Sidebar neobsahuje technické sekce:
  - ověř mapu zdrojů v `MarkdownDocumentationService`.
- Neodpovídá obsah realitě kódu:
  - proveď audit proti source-of-truth souborům v sekci 9.

## 9. Audit a traceability
Source-of-truth reference:
- Startup pipeline: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Program.cs`
- Dokumentační mapping: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Services/Documentation/MarkdownDocumentationService.cs`
- Dokumentační routing: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Controllers/DokumentaceController.cs`
- Publish mapování docs: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/PmTracker.Web.csproj`
