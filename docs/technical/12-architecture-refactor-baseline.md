# PM Tracker - Technická dokumentace 12: Baseline pro bezpečný architektonický refaktor

## 1. Účel
Tento dokument vytváří výchozí baseline před modulárním refaktorem. Cíl je popsat aktuální architekturu, hlavní hotspoty, testovací síť a checklist "no behavior change", aby další sprinty měnily strukturu bez změny funkčního chování aplikace.

## 2. Publikum a role
- Architekt: validace pořadí refaktoru a míry rizika.
- Vývojář: orientace v místech s nejvyšší vazbou a regresním dopadem.
- QA: kontrola, že každý sprint zachovává stávající kontrakty.
- Product owner / neprogramátor: čitelný podklad pro postup po sprintech.

## 3. Závislosti a předpoklady
- Cílová platforma: .NET 8, ASP.NET Core MVC + Razor Views.
- Zdroj pravdy: `Program.cs`, `DataStoreServiceCollectionExtensions`, controllery, služby, test projekty a skripty v `scripts/`.
- Refaktor v dalších sprintech nesmí bez explicitního souhlasu měnit URL routy, API kontrakty, DB schema ani autentizační model.
- Povinná minimální gate po každém sprintu je `./scripts/test-unit.sh`, `./scripts/test-api.sh`, `./scripts/test-integration.sh`.

## 4. Vstupy a výstupy
### Vstupy
- Runtime bootstrap v `PmTracker.Web/Program.cs`.
- Web vrstva v `PmTracker.Web/Controllers`, `PmTracker.Web/Middleware`, `PmTracker.Web/Filters`, `PmTracker.Web/Views`.
- Datová a servisní vrstva v `PmTracker.Web/Services` a `PmTracker.Web/Data`.
- Test projekty `PmTracker.Tests.Unit`, `PmTracker.Tests.Api`, `PmTracker.Tests.Integration`, `PmTracker.Tests.E2E`.

### Výstupy
- Snapshot aktuální architektury a hranic odpovědnosti.
- Seznam hotspotů a technických rizik.
- Checklist "no behavior change".
- Přesný plán sprintů 1-3.

## 5. Detailní postup
### 5.1 Snapshot aktuální architektury
| Vrstva / oblast | Aktuální stav | Poznámka pro refaktor |
| --- | --- | --- |
| Host a pipeline | `Program.cs` používá `WebApplication.CreateBuilder`, MVC controllery s views, IIS autentizaci, vlastní `X-Trace-Id` hlavičku, `AjaxResponseContractGuardMiddleware`, `MapControllers` a default route `{controller=Projekty}/{action=Index}/{id?}`. | Pořadí middleware a route kontrakty jsou stabilní povrch; v dalších sprintech je nesmíme měnit. |
| Presentation | Většina controllerů dědí z `BaseController`. Ten centralizuje `CurrentUserContext`, AJAX response helpery, validační a redirect utility. | `BaseController` je užitečný, ale koncentruje cross-cutting chování i přístup k datům. |
| Application/service kandidáti | Existují cílené služby `RecordsService`, `PeopleService`, `DictionariesService`, `SettingsService`, `MarkdownDocumentationService`, `OpenXmlWordExportService`. | Oddělení odpovědností je jen částečné; některé controllery stále pracují přímo s datastore. |
| Infrastructure / persistence | `PmTrackerDbContext` + monolitický `SqlServerDataStore` implementující `IPmTrackerDataStore`, doplněný o AD konektor a startup validator. | `SqlServerDataStore` je hlavní bod vazby a největší kandidát na interní rozpad po feature slice. |
| Testing | Samostatné projekty pro unit, API, integration a E2E testy. API testy používají `WebApplicationFactory<Program>`, integration testy spouští reálnou SQL databázi přes fixture. | Test síť je použitelná pro bezpečný refaktor, ale je silně navázaná na současný datastore a SQL infrastrukturu. |

### 5.2 Potvrzené hotspoty a vazby
| Hotspot | Důkaz v repozitáři | Riziko | Doporučení pro pořadí prací |
| --- | --- | --- | --- |
| `SqlServerDataStore` | cca 7 932 řádků v jednom souboru | Skryté coupling body, vysoká pravděpodobnost regresí při jednorázovém rozpadu | Nejprve rozdělovat interně po feature slices, ne měnit naráz externí kontrakt |
| `ProjektyController` | cca 633 řádků, přímá práce s `DataStore` | Míchání orchestrace, permission checků a datové kompozice | Přesouvat logiku až po zajištění testů pro projekty a detail projektu |
| `ZaznamyController` | cca 544 řádků, kombinace `IRecordsService` + `DataStore` | Hybridní boundary, vysoké riziko driftu AJAX kontraktu | Udržet jako pilotní slice, ale zachovat stávající payloady a redirecty |
| `NastaveniController` | cca 471 řádků | AuthZ a administrace bývají regresně citlivé | Refaktorovat až po stabilizaci feature služeb a permission testů |
| `BaseController` | cca 560 řádků | Sdílí validační, AJAX a časové utility napříč většinou UI | Nezužovat agresivně v prvních sprintech; nejdřív oddělit feature logiku |
| Test infrastruktura | `IntegrationTestHelper` vytváří přímo `SqlServerDataStore`; API fixture nahrazuje antiforgery a konfiguraci | Vnitřní změny konstruktorů/datastore rychle rozbijí testy | Zachovat kompatibilní kompozici, případné adaptéry doplnit až s testy |

### 5.3 Stav aktuální test gate
| Vrstva | Spouštěč | Co dnes ověřuje | Poznámka |
| --- | --- | --- | --- |
| Unit | `./scripts/test-unit.sh` | Middleware, filtry, security pravidla, export, documentation service, schedule logiku | Dobrá ochrana pomocných a cross-cutting částí |
| API | `./scripts/test-api.sh` | `WebApplicationFactory`, controller kontrakty, AJAX endpointy, dokumentaci, export, profil | Pokrytí je nejsilnější u record editoru a souvisejících endpointů |
| Integration | `./scripts/test-integration.sh` | Reálné DB operace v datastore, permission a membership scénáře, ukládání záznamů | Kritická síť pro bezpečný rozpad datové vrstvy |
| E2E | `./scripts/test-e2e.sh` | UI/browser flow přes Playwright | Není povinné po každém sprintu, ale je povinné každé 3 sprinty nebo před merge |
| Prereq | `./scripts/test-prereq-check.sh` | Dostupnost `dotnet`, `docker` a výsledkových adresářů | Pro integrační/E2E běhy je to praktický předběžný check, i když není v minimální gate |

Klíčové existující xUnit kotvy pro byznys logiku:
- `PmTracker.Tests.Integration/DataStore/RecordSaveDataStoreTests.cs`
- `PmTracker.Tests.Integration/DataStore/ProjectMembershipDataStoreTests.cs`
- `PmTracker.Tests.Unit/Security/CurrentUserContextPermissionTests.cs`
- `PmTracker.Tests.Api/Controllers/RecordEditorControllerTests.cs`

### 5.4 Checklist "no behavior change"
- Zachovat `Program.cs` middleware order, výchozí route i IIS autentizační schéma.
- Neměnit názvy controllerů, action metod, view paths a redirect cíle.
- Neměnit HTTP status kódy, shape AJAX JSON odpovědí ani antiforgery chování.
- Neměnit `CurrentUserContext` resolution, permission key evaluaci a filtrování viditelných projektů.
- Neměnit connection string names, SQL provider validaci ani startup validator povinných číselníků.
- Neměnit kontrakt hlavičky `X-Trace-Id`.
- Nepřidávat DB schema změny, background joby ani nové externí knihovny bez explicitního důvodu.
- U časové logiky preferovat stávající `TimeProvider` injektovaný přes DI; nepřevádět datumové chování bez testu.
- Před přesunem logiky nejdřív identifikovat existující test kotvu; pokud chybí, doplnit test dřív než refaktor.
- Po každém sprintu projít minimální gate; po sprintu 3 navíc plný gate.

### 5.5 Registr rizik
| Riziko | Popis | Dopad | Mitigace |
| --- | --- | --- | --- |
| R1 Monolitický datastore | Většina business/datových operací je soustředěna v `SqlServerDataStore`. | Vysoký | Rozpad provádět interně po feature slice a držet stabilní veřejný kontrakt po mezikrocích |
| R2 Přímý přístup controller -> datastore | Některé controllery obcházejí cílené feature služby. | Vysoký | Zavádět aplikační služby po modulech a migrovat controllery postupně |
| R3 `BaseController` jako shared gravity point | Helpery pro AJAX, validaci a user context jsou sdílené napříč UI. | Střední až vysoký | V prvních sprintech neměnit jeho veřejné helpery ani semantiku |
| R4 Testy jsou infrastrukturně závislé na SQL | Integration/API vrstva předpokládá funkční SQL container a současnou kompozici služeb. | Střední | Používat stejné fixture kontrakty a případné změny DI provádět kompatibilně |
| R5 Nerovnoměrné pokrytí feature modulů | Záznamy a datastore mají silnější testovací síť než některé projekty/UI větve. | Střední | Před refaktorem projekty/jednání doplnit cílené API/unit testy na redirecty a permission flow |
| R6 AuthZ regrese | `Nastaveni`, práva a visibility pravidla jsou navázané na více tabulek a helperů. | Vysoký | Refaktor AuthZ dělat až po stabilizaci servisních hranic a s integračními testy jako gate |

### 5.6 Přesný plán sprintů 1-3
#### Sprint 1 - Stabilizace feature boundary bez změny kontraktů
- Scope: `Zaznamy`, `Osoby`, `Ciselniky`, `Nastaveni`, `Dokumentace`, `Export`.
- Cíl: dokončit a sjednotit feature-orientované služby tam, kde už jejich základy existují, a omezit přímé volání datastore v controllerech těchto oblastí.
- Povoleno: přesun orchestrace a validačních větví do služeb / use-case tříd, doplnění testů, úpravy DI.
- Zakázáno: změna route, payloadů, DB schema a view kontraktů.
- Gate: minimální gate.

#### Sprint 2 - Oddělení projekční a meeting logiky od controllerů
- Scope: `ProjektyController`, `JednaniController`, `HomeController`, `ProfilController`, `ObsazeniController`, sdílené části `BaseController`.
- Cíl: zavést služební hranice pro projekty a jednání tak, aby controller zůstal jen orchestrace + mapování.
- Povoleno: nové aplikační služby/fasády, doplnění API/unit testů pro projekty a meetingy, zúžení přímé datové závislosti controllerů.
- Zakázáno: změna default route, redirect flow, permission key semantics a HTML/AJAX kontraktů.
- Gate: minimální gate.

#### Sprint 3 - Interní rozpad infrastruktury a finální bezpečnostní síť
- Scope: `Services/Data`, zejména `SqlServerDataStore`, `IPmTrackerDataStore`, DI kompozice a test fixture kompatibilita.
- Cíl: rozdělit datovou vrstvu po modulech při zachování stejného chování aplikace navenek; připravit půdu pro další doménové či aplikační dělení.
- Povoleno: interní repository / feature store extrakce, přesun registrací do modulárních extension metod, doplnění integračních testů.
- Zakázáno: změna DB schema, SQL provideru, connection string názvů nebo test fixture contractu bez kompatibilní vrstvy.
- Gate: plný gate (`test-prereq-check`, `unit`, `api`, `integration`, `e2e`).

## 6. Verifikace
- Ověř, že baseline odkazuje na skutečné soubory v `PmTracker.Web`, `PmTracker.Tests.*` a `scripts/`.
- Ověř, že každý hotspot v části 5.2 má konkrétní důkaz a navrženou mitigaci.
- Ověř, že checklist v části 5.4 explicitně chrání routy, API kontrakty, DB schema, auth a middleware order.
- Ověř, že plán sprintů 1-3 navazuje od nejnižšího rizika k nejvyššímu a že sprint 3 používá plný gate.

## 7. Rollback
- Pokud baseline neodpovídá realitě repozitáře, vrať tento dokument a odpovídající řádky ve stromu dokumentace.
- Pokud se ukáže, že některý hotspot je chybně vyhodnocený, oprav pouze dokumentaci a znovu spusť minimální gate.
- Protože `SPRINT 0` nesmí měnit runtime kód, rollback nevyžaduje žádný zásah do `PmTracker.Web`.

## 8. Troubleshooting
- Integration/API testy padají kvůli infrastruktuře:
  - spusť `./scripts/test-prereq-check.sh`,
  - ověř dostupnost Docker/Colima,
  - teprve potom opakuj minimální gate.
- Při návrhu dalšího sprintu hrozí změna chování:
  - porovnej controller action signatures,
  - porovnej AJAX payload fields a status kódy,
  - zkontroluj, zda se nemění permission flow nebo view path.
- Objeví-li se potřeba změnit DB schema nebo routy:
  - zastav sprint,
  - vyčleň změnu do samostatného dedikovaného sprintu,
  - nepokračuj pod hlavičkou "no behavior change".

## 9. Audit a traceability
- Runtime bootstrap: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Program.cs`
- DI registrace a datastore composition root: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs`
- Shared controller behavior: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Controllers/BaseController.cs`
- Hlavní hotspot controllerů: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Controllers/ProjektyController.cs`
- Hlavní hotspot controllerů: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Controllers/ZaznamyController.cs`
- Hlavní hotspot persistence: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Services/Data/SqlServerDataStore.cs`
- API test host: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.Api/TestInfrastructure/PmTrackerWebAppFactory.cs`
- Integration test helper: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Tests.Integration/TestInfrastructure/IntegrationTestHelper.cs`
- Povinné gate skripty: `/Users/Pavel.Andrlik/Documents/PM Tracker/scripts/test-unit.sh`, `/Users/Pavel.Andrlik/Documents/PM Tracker/scripts/test-api.sh`, `/Users/Pavel.Andrlik/Documents/PM Tracker/scripts/test-integration.sh`
