# PM Tracker - Technická dokumentace 02: Architektura

## 1. Účel
Dokument popisuje interní architekturu aplikace tak, aby změny byly proveditelné bez regresí a s jasným oddělením odpovědností.

## 2. Publikum a role
- Vývojář: orientace v architektuře a v odpovědnostech vrstev.
- Architekt: validace konzistence návrhu.
- Ops/QA: pochopení provozního dopadu změn.

## 3. Závislosti a předpoklady
- Cílová platforma: .NET 8.
- Prezentace: ASP.NET Core MVC + Razor Views.
- Persistence: SQL Server provider přes datovou službu.
- Dokumentace in-app je markdown render přes Markdig.

## 4. Vstupy a výstupy
### Vstupy
- Kód controllerů, služeb a view modelů.
- Datová vrstva `SqlServerDataStore`.
- Dokumentační vrstva `IDocumentationService`.

### Výstupy
- Schéma odpovědností a pravidel pro rozšiřování systému.

## 5. Detailní postup
### 5.1 Hlavní komponenty
- `Controllers`: orchestrace HTTP požadavků, validace vstupů, volání služeb.
- `Services/Data`: business + persistence operace nad SQL.
- `Services/Common`: sdílené policy/helper služby (normalizace, permission evaluation, authorization policy).
- `Models/ViewModels`: kontrakty mezi controllerem a view.
- `Views`: Razor render; business rozhodování je mimo view.

### 5.2 Datový a autentizační model
- Doménová data: schéma `dbo`.
- Oprávnění: schéma `authz`.
- Superadmini: `authz.superadmins`.
- Efektivní práva: kombinace user roles, role permissions, scope režimů a projektových include map.
- AD identity je používána pro vyhledání a mapování (`Guid_AD`), ale bez automatické synchronizace uživatelů do DB.

### 5.3 Aplikační moduly (rekurzivní listy)
- Projekty: CRUD projektu, tým, projektové role, subsystemy a subsystemové role.
- Záznamy: CRUD záznamu, komentáře/vyjádření, meeting identifier, harmonogram/timeline.
- Jednání: detail jednání, status, účast, poznámky, přidání účastníků.
- Osoby: manuální osoba, AD vyhledání + uložená AD osoba, mazání osoby.
- Číselníky: dashboard/detail/panel, edit/smazání řádku, lock a bezpečnostní pravidla.
- Nastavení (authz): role, permission klíče, user-role, role-permission, efektivní práva.
- Export: tisk projektu/jednání/úkolu + Word export.
- Dokumentace/profil: markdown dokumentace, profil a zobrazení práv.
- Obsazení: kompatibilní route, která redirectuje na `Projekty/Index`.

### 5.4 Pravidla návrhu změn
- Business pravidla držet ve službách, ne v JS/UI.
- Nové permission key vždy zavést server-first (katalog podporovaných klíčů).
- Změnu datového modelu vždy doprovodit:
  - SQL baseline/upgrade skriptem,
  - aktualizací dokumentace,
  - testem minimálně na unit + integration vrstvě.

### 5.5 Dokumentační architektura
- Markdown soubory jsou publishované do `DocsContent`.
- `MarkdownDocumentationService` mapuje key -> route -> markdown source.
- `DokumentaceController` poskytuje route endpointy.
- Rekurzivní strom dokumentace a mapa listů je veden v `docs/technical/00-documentation-tree.md`.

### 5.6 Přímé mapování na runtime pipeline
- Startup registrace služeb je centralizovaná v `DataStoreServiceCollectionExtensions`.
- Runtime middleware řetězec je v `Program.cs`:
  - `UseHttpsRedirection` -> `UseStaticFiles` -> `UseRouting` -> `AjaxResponseContractGuardMiddleware` -> auth middleware -> controller routes.
- `X-Trace-Id` je přidáván na každou odpověď a je použitelný pro audit incidentů.

### 5.7 AD a osoby (architektonické omezení)
- AD integrace je search-only + mapování identity.
- Aplikace nemá background job pro automatický import AD účtů.
- `OsobyController` + `PeopleService` implementují explicitní onboarding osoby do `dbo.osoby`.

## 6. Verifikace
- Ověř, že architektonické vrstvy nejsou porušeny (business logika nevzniká ve view/controlleru).
- Ověř, že každá nová funkcionalita má odpovídající testy a dokumentaci.
- Ověř, že documentation route mapa odpovídá fyzickým markdown souborům.

## 7. Rollback
- Při regresi po refaktoru:
  - rollbackni dotčenou službu/controller,
  - vrať dokumentační změny na poslední validní verzi,
  - spusť regresní testy (`unit`, `integration`, `api`).

## 8. Troubleshooting
- Vývojové změny rozbíjí autorizaci:
  - ověř konzistenci permission key katalogu, authz tabulek a UI mapování.
- Dokumentace ukazuje zastaralou architekturu:
  - aktualizuj tento soubor při každé významné architektonické změně.

## 9. Audit a traceability
- Program bootstrap: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Program.cs`
- Data store registration: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs`
- Authz model a view modely: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Models/ViewModels/SecurityViewModels.cs`
- SQL baseline: `/Users/Pavel.Andrlik/Documents/PM Tracker/PMTracker_insert_sql`

## 10. Souběžná editace záznamu

Návrh a rozhodnutí: `docs/superpowers/specs/2026-09-17-record-edit-concurrency-design.md`.

Dvě vrstvy, každá řeší jiného protivníka:

1. **Zámek karty (prevence, člověk × člověk).** `dbo.zaznam_edit_zamek`, jeden řádek na
   záznam. `GET /Zaznamy/Edit` ho po autorizaci a před načtením modelu získává atomickým
   `MERGE`; cizí živý zámek vrátí stránku „upravuje jiný uživatel" a editor se nevykreslí.
   Vlastní zámek je re-entrantní (dva taby téhož uživatele). TTL 15 minut bez heartbeatu,
   heartbeat veze existující keep-alive (`/App/KeepAlive?zaznamId=…`), uvolnění při odchodu
   jde `sendBeacon` na `/Zaznamy/ReleaseEditLock`. Služba: `RecordEditLockService`.

2. **Record guard (detekce, zbytek).** Editor nese `RecordVersion` = id posledního
   auditního zápisu nad záznamem (`RecordVersionQuery`). Při neshodě se uložení odmítne
   kódem `RECORD_STALE` a hláška pojmenuje autora z auditu. Pokrývá i to, na co zámek
   nedosáhne: schválení návrhu harmonogramu, vypršelý zámek, dva taby.

**Invariant, na kterém to stojí:** verzi posouvá jen uživatelský zápis, který by uložení
editoru mohlo přepsat. Audit s entitou `zaznam` píší výhradně `SaveRecord`, `DeleteRecord`,
`MeetingIdentifier` a rozhodnutí o návrhu; automatika (harvest, rebalance, sync harmonogramu)
neaudituje nic, takže její zásahy do kroků nikoho neblokují. Hlídá to test
`AutomatScheduleWrite_ShouldNotChangeRecordVersion`.

**Výjimka `assign`:** doplnění identifikátoru z jednání verzi neposouvá. Mění jen číslo
záznamu, které editor nepřepíše, a tlačítko je uvnitř editoru, který se po akci nepřenačte —
posunutá verze by uživatele zablokovala jeho vlastní akcí. Vyřadit se smí jen akce, jejíž zápis
uložení editoru prokazatelně nepřepíše; hlídá to `SaveRecord_ShouldSucceed_AfterMeetingIdentifierWasAssignedFromOpenEditor`
(ověřuje i to, že identifikátor uložení přežije).

Dřívější kontrola `ScheduleVersion` (skalární `MAX(UpdatedAt)` nad kroky) byla zrušena —
blokovala uložení kvůli automatice a před kolizí uživatel × automat nechránila, protože
do skutečnosti auto-eligible kroků se uživatel nedostane.

**Dvě místa, kde na pořadí záleží:**

- Ve `ValidateSaveRecordAsync` stojí kontrola verze **až za** kontrolou
  `record_project_mismatch`. Ta je jediná vazba mezi `Id` záznamu z formuláře a `ProjektId`,
  proti kterému controller ověřoval oprávnění; kdyby ji guard v `if/else` řetězu přeskočil,
  právo `records.edit` na jednom projektu by otevřelo záznamy všech ostatních.
- `RecordLastWriterQuery` musí číst **tentýž auditní řádek**, který `RecordVersionQuery`
  považuje za verzi. Obě místa proto berou řádky ze sdíleného `RecordVersionQuery.VersionRows`
  (poslední podle `id`) — jinak hláška pojmenuje někoho, kdo s aktuální verzí nemá nic společného.

Tabulku zámku vyžaduje `SqlStartupValidatorHostedService`: nasazení bez
`db_upgrade_1_4_5_record_edit_lock.sql` selže při startu a hláška ten skript jmenuje.
