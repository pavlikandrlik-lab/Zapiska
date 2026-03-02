# Zápiska DB bootstrap

## Pořadí spuštění SQL skriptů

1. `PMTracker_insert_sql`

## Poznámky

- Backend očekává schéma `dbo` + `authz`.
- `dbo.osoby.Guid_AD` musí být `uniqueidentifier NULL`.
- `dbo.osoby.email` musí existovat jako `nvarchar(255) NULL`.
- `dbo.jednani.cas_zacatek` musí být `time(0) NOT NULL`.
- `dbo.vyjadreni.autor_osoba_id` musí existovat jako `int NOT NULL` (+ FK na `dbo.osoby.id`).
- `dbo.ciselnik_harmonogram_typu` a `dbo.zaznam_harmonogram_hodnoty` musí existovat (harmonogram úkolů).
- `dbo.projektove_zaznamy` musí mít `cislo_zaznamu` s unikátním indexem `(projekt_id, cislo_zaznamu)`.
- `dbo.projekty.pouzivat_ident_jednani` musí existovat (`bit not null`, default `0`).
- `dbo.projektove_zaznamy` musí mít `cislo_viditelne`, `cislo_viditelne_typ`, `cislo_viditelne_a`, `cislo_viditelne_b`, `cislo_jednani_zdroj_id`.
- V číselníku stavu projektu musí existovat minimálně kódy: `PLAN`, `RUN`, `DONE`, `DELETED`.
- V provozu musí být `IDENTITY_CACHE = OFF`, aby po restartu SQL nedocházelo ke skokům ID (např. +1000).
- Aplikace v režimu `SqlServer` startuje fail-fast: bez dostupné DB nebo bez povinných číselníků nespustí web.
- `Guid_AD` je technický identifikátor pro párování identity. V UI se nezobrazuje.

## Lokální připojení (macOS)

Výchozí `appsettings.Development.json` používá:

- `Server=localhost,1433`
- `Database=PmTracker`
- `User ID=sa`
- `Password=PmTracker!2026`

Pokud máte jiný SQL kontejner/instance, upravte `ConnectionStrings:PmTrackerDb`.

Pro AD vyhledávání osob je v `appsettings*.json` sekce `PmTracker:ActiveDirectory` (`Domain`, `MaxResults`, `QueryTimeoutSeconds`).
