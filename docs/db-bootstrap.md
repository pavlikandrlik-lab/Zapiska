# Zápiska DB bootstrap

## Pořadí spuštění SQL skriptů

1. `PMTracker_insert_sql`
2. volitelně pro lokální/dev/test: `db_seed_dev_admin.sql`

## Poznámky

- Backend očekává schéma `dbo` + `authz`.
- `PMTracker_insert_sql` je produkční baseline seed. Obsahuje:
  - technicky povinné stavy projektu,
  - fixní projektové a subsystemové role,
  - bootstrap organizaci `MO` pro ruční založení první osoby,
  - katalog oprávnění a systémové authz role,
  - minimální provozní business baseline pro plnou funkčnost UI po fresh installu:
    - kategorii záznamu `U / Úkol`,
    - minimální stavy úkolů,
    - minimální stavy jednání,
    - minimální stavy účasti,
    - typy úkolů `mp / MiniProjekt`, `A / Akce`, `P / Projekt`, `RU / Hlavní úkol rozvoje`,
    - typy externích odkazů `PMP / Požadavek metodické podpory`, `PNF / Požadavek nové funkcionality`, `NES / Nesrovnalost`,
    - výchozí aktivní harmonogramovou šablonu a její kroky.
- `db_seed_dev_admin.sql` je lokální/dev/test vrstva navíc. Obsahuje demo data, lokální osobu `Pavel Admin`, další business číselníky a navázání prvního administrátora pro neprodukční provoz.
- Produkční baseline záměrně nevytváří žádnou osobu, projekt ani superadmin účet.
- Pro upgrade existující DB na release `1.1.0` spusťte navíc `db_upgrade_1_1_0_signed_schedule_actual.sql`.
- `dbo.osoby.Guid_AD` musí být `uniqueidentifier NULL`.
- `dbo.osoby.email` musí existovat jako `nvarchar(255) NULL`.
- `dbo.jednani.cas_zacatek` musí být `time(0) NOT NULL`.
- `dbo.vyjadreni.autor_osoba_id` musí existovat jako `int NOT NULL` (+ FK na `dbo.osoby.id`).
- `dbo.ciselnik_harmonogram_typu` a `dbo.zaznam_harmonogram_hodnoty` musí existovat (harmonogram úkolů).
- `dbo.zaznam_harmonogram_hodnoty.hodnota_int` musí umožnit záporné hodnoty pro harmonogramovou skutečnost; legacy constraint `CK_zaznam_harmonogram_hodnoty_hodnota_nonnegative` už nesmí být nasazen.
- `dbo.projektove_zaznamy` musí mít `cislo_zaznamu` s unikátním indexem `(projekt_id, cislo_zaznamu)`.
- `dbo.projekty.pouzivat_ident_jednani` musí existovat (`bit not null`, default `0`).
- `dbo.projektove_zaznamy` musí mít `cislo_viditelne`, `cislo_viditelne_typ`, `cislo_viditelne_a`, `cislo_viditelne_b`, `cislo_jednani_zdroj_id`.
- V číselníku stavu projektu musí existovat minimálně kódy: `PLAN`, `RUN`, `DONE`, `DELETED`.
- V provozu musí být `IDENTITY_CACHE = OFF`, aby po restartu SQL nedocházelo ke skokům ID (např. +1000).
- Aplikace v režimu `SqlServer` startuje fail-fast: bez dostupné DB nebo bez povinných číselníků nespustí web.
- `Guid_AD` je technický identifikátor pro párování identity. V UI se nezobrazuje.
- První provozní superadmin se zakládá ručně do DB:
  - `dbo.osoby.organizace_id` musí ukazovat na bootstrap organizaci `MO`,
  - `dbo.osoby.organizacni_celek_id` může být `NULL`,
  - následně se `osoba_id` vloží do `authz.superadmins`.

## Lokální připojení (macOS)

Výchozí `appsettings.Development.json` používá:

- `Server=localhost,1433`
- `Database=PmTracker`
- `User ID=sa`
- `Password=PmTracker!2026`

Pokud máte jiný SQL kontejner/instance, upravte `ConnectionStrings:PmTrackerDb`.

Pro AD vyhledávání osob je v `appsettings*.json` sekce `PmTracker:ActiveDirectory` (`Domain`, `MaxResults`, `QueryTimeoutSeconds`).
