<div align="center">

<img src="PmTracker.Web/wwwroot/images/zapiska-logo.svg" alt="Zápiska logo" width="128" height="128" />

# Zápiska (PM Tracker)

**Interní webová aplikace pro evidenci projektů, záznamů, jednání a úkolů.**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![SQL Server](https://img.shields.io/badge/SQL%20Server-2019%2B-CC2927?logo=microsoftsqlserver&logoColor=white)](https://www.microsoft.com/sql-server)
[![IIS](https://img.shields.io/badge/IIS-in--process-0078D4?logo=microsoft&logoColor=white)](https://www.iis.net/)
[![Version](https://img.shields.io/badge/version-0.8-blue)](CHANGELOG.md)

</div>

---

## O projektu

Zápiska (interní kódové označení **PM Tracker**) je ASP.NET Core 8 MVC monolit provozovaný na IIS. Slouží k centrální evidenci projektového dění — projektů, jejich subsystémů, záznamů/úkolů, jednání a účasti, vyjádření a externích vazeb na okolní agendy (PMP, PNF, NES, ServiceDesk).

Aplikace je navržena pro **offline intranetové prostředí** bez internetové konektivity — autentizace přes Windows Authentication, AD mapování přes `dbo.osoby.Guid_AD`, migrace databáze ručními SQL skripty (`db_upgrade_*.sql`).

## Klíčové funkce

- **Projekty a subsystémy** — hierarchická evidence projektů s členy, rolemi a harmonogramem.
- **Záznamy a úkoly** — rich-text popis a vyjádření, priority, stavy, vlastníci i spolupracovníci.
- **Harmonogram** — plán vs. skutečnost, vizualizace kroků, filtrování podle subsystému.
- **Jednání** — evidence jednání, účasti, rozhodnutí a vazba záznamů na konkrétní jednání.
- **Autorizace** — role × scope model (`authz.*` tabulky), efektivní práva s přehledem zdrojů oprávnění.
- **Exporty** — PDF a WORD export záznamů a jednání se zachováním formátování i odkazů.
- **Globální vyhledávání** — volitelně přes OpenSearch (feature flag, post-filter přes oprávnění).
- **ServiceDesk integrace** — synchronizace stavů tiketů a externích odkazů (fáze 2, viz [docs/technical/13-servicedesk-connection-setup.md](docs/technical/13-servicedesk-connection-setup.md)).

Kompletní historii změn najdeš v [CHANGELOG.md](CHANGELOG.md).

## Technologický stack

| Vrstva | Technologie |
| --- | --- |
| Runtime | .NET 8, ASP.NET Core MVC |
| Hosting | IIS (in-process, ASP.NET Core Module V2) |
| Databáze | SQL Server (EF Core 8 + Microsoft.Data.SqlClient) |
| Autentizace | Windows Authentication |
| UI | Razor Views, [gov-design-system](https://gov-design-system.gov.cz/) web components |
| Vyhledávání (volitelné) | OpenSearch 2.x |
| Dokumenty | DocumentFormat.OpenXml (WORD), Markdig (in-app docs) |
| Testy | xUnit — Unit, Integration, Api, E2E, Web |

## Struktura repozitáře

```
PM Tracker/
├── PmTracker.Web/                  # Hlavní ASP.NET Core MVC aplikace
├── PmTracker.ServiceDesk.Contracts # Sdílené DTO pro ServiceDesk integraci
├── PmTracker.ServiceDesk.Sql/      # SQL přístupová vrstva pro ServiceDesk
├── PmTracker.Tests.Unit/           # Unit testy
├── PmTracker.Tests.Integration/    # Integrační testy (SQL, služby)
├── PmTracker.Tests.Api/            # Testy HTTP vrstvy
├── PmTracker.Tests.E2E/            # End-to-end testy
├── PmTracker.Web.Tests/            # Testy views a UI vrstvy
├── PMTracker_insert_sql            # SQL baseline (bootstrap)
├── db_upgrade_*.sql                # SQL migrační patch skripty
├── docs/                           # Dokumentace (technická + user guide)
│   ├── technical/                  # ISO 26514 technická dokumentace
│   ├── specs/                      # Specifikace fičur
│   ├── known-issues/               # Evidované známé problémy
│   └── changelog/releases/         # Podklady pro CHANGELOG
├── install.md                      # Vstupní instalační dokument
└── CHANGELOG.md                    # Vygenerovaný changelog
```

## Rychlý start

> **Pozn.:** Aplikace je navržená pro produkční provoz na IIS v offline síti. Lokální vývoj je primárně pro ladění kódu.

### Předpoklady

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server (LocalDB / Express / plná instance)
- Volitelně: Docker (pro lokální OpenSearch)

### Lokální build a spuštění

```bash
# 1) Obnovení balíčků a build
dotnet build PmTracker.sln

# 2) Bootstrap databáze
#    - aplikuj PMTracker_insert_sql (baseline)
#    - aplikuj všechny db_upgrade_*.sql v pořadí verzí

# 3) Lokální konfigurace
#    v PmTracker.Web/appsettings.Development.json nastav ConnectionStrings:PmTrackerDb

# 4) Spuštění
dotnet run --project PmTracker.Web
```

### Nasazení na IIS

Kompletní instalační postup (publish, SQL bootstrap, IIS konfigurace, smoke test) je v [install.md](install.md) a navazující technické dokumentaci v [docs/technical/04-installation-deployment-iis.md](docs/technical/04-installation-deployment-iis.md).

## Testy

```bash
# Všechny projekty
dotnet test PmTracker.sln

# Vybraný projekt
dotnet test PmTracker.Tests.Unit
```

Integrační a E2E testy vyžadují funkční SQL Server; detaily jsou v [docs/technical/09-testing-quality.md](docs/technical/09-testing-quality.md).

## Dokumentace

| Dokument | Obsah |
| --- | --- |
| [install.md](install.md) | Vstupní instalační dokument, rychlý přehled |
| [docs/technical/](docs/technical/) | Technická dokumentace (ISO 26514) — architektura, runtime, DB, provoz, troubleshooting |
| [docs/user-guide.md](docs/user-guide.md) | Uživatelská příručka |
| [docs/qa.md](docs/qa.md) | Q&A |
| [docs/authorization.md](docs/authorization.md) | Autorizační model |
| [CHANGELOG.md](CHANGELOG.md) | Historie verzí |

Dokumentace je zároveň publikovaná přímo v aplikaci pod routou `/Dokumentace/*`.

## Licence

Interní projekt bez veřejné licence. Pro využití mimo uvedený kontext kontaktuj autora.

## Autor

[@pavlikandrlik-lab](https://github.com/pavlikandrlik-lab)
