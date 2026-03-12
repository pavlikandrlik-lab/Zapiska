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
