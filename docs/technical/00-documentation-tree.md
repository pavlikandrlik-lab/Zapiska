# PM Tracker - Technická dokumentace 00: Strom dokumentace a pokrytí uzlů

## 1. Účel
Tento dokument je kořenový strom dokumentace vytvořený rekurzivním postupem podle ISO 26514 principů (účel, role, vstupy/výstupy, provozní použitelnost). Cíl je mít úplné pokrytí aplikace od nejvyšších oblastí po listové uzly, které se již dál smysluplně nedělí.

## 2. Publikum a role
- Software architekt: kontrola úplnosti dokumentačního stromu.
- Vývojář: mapování změny k přesnému uzlu stromu.
- Ops/DB/Security admin: rychlá orientace v provozních oblastech.
- QA: ověření, že každý kritický uzel má test/validaci.

## 3. Závislosti a předpoklady
- Zdroj pravdy je reálná struktura aplikace (`Controllers`, `Services`, `Program.cs`, `PMTracker_insert_sql`).
- Strom je udržovaný v repozitáři a publikovatelný in-app.
- Každý listový uzel musí mít konkrétní popis v některém dokumentu `docs/technical/*`.

## 4. Vstupy a výstupy
### Vstupy
- Aplikační vrstvy: `PmTracker.Web/Controllers`, `PmTracker.Web/Services`, `PmTracker.Web/Models`.
- Provozní artefakty: `appsettings*`, `publish/web.config`, SQL skripty, test skripty.

### Výstupy
- Deterministický strom dokumentace s uzly a listy.
- Mapování listů na konkrétní dokumentační soubory.

## 5. Detailní postup
### 5.1 Rekurzivní strom (od kořene po listy)
- `N0 PM Tracker (kořen)`
- `N1 Runtime platforma`
- `N1.1 Hosting pipeline (IIS + ASP.NET Core)`
- `N1.1.1 Middleware pořadí, route mapping, default route` (list)
- `N1.1.2 AJAX contract guard + antiforgery result contract` (list)
- `N1.2 Konfigurace prostředí`
- `N1.2.1 appsettings + environment variables` (list)
- `N1.2.2 SQL provider fail-fast validace` (list)
- `N1.3 Observabilita`
- `N1.3.1 X-Trace-Id hlavička a diagnostická stopa` (list)

- `N2 Aplikační moduly`
- `N2.1 Projekty`
- `N2.1.1 CRUD projektu` (list)
- `N2.1.2 Projektový tým a projektové role` (list)
- `N2.1.3 Subsystemy projektu a subsystemové role` (list)
- `N2.2 Záznamy`
- `N2.2.1 CRUD záznamu/úkolu` (list)
- `N2.2.2 Vyjádření a komentáře` (list)
- `N2.2.3 Vazba záznamu na identifikátor jednání` (list)
- `N2.2.4 Harmonogram a timeline výpočet` (list)
- `N2.3 Jednání`
- `N2.3.1 Stavový lifecycle jednání` (list)
- `N2.3.2 Účastníci a docházka` (list)
- `N2.3.3 Poznámky jednání` (list)
- `N2.4 Osoby`
- `N2.4.1 Ručně přidaná osoba (manual)` (list)
- `N2.4.2 AD vyhledání + řízené uložení osoby` (list)
- `N2.4.3 Mazání osoby a validační pravidla` (list)
- `N2.5 Číselníky`
- `N2.5.1 Bezpečnostní pravidla přístupu k číselníkům` (list)
- `N2.5.2 Úprava/smazání řádku včetně lock semantics` (list)
- `N2.6 Nastavení / AuthZ`
- `N2.6.1 Role` (list)
- `N2.6.2 Permission klíče` (list)
- `N2.6.3 Uživatel -> role` (list)
- `N2.6.4 Role -> permission (+ scope ALL/INCLUDE)` (list)
- `N2.6.5 Efektivní práva` (list)
- `N2.7 Exporty`
- `N2.7.1 Tisk PDF/HTML` (list)
- `N2.7.2 Word export` (list)
- `N2.8 Dokumentace a profil`
- `N2.8.1 In-app markdown dokumentace` (list)
- `N2.8.2 Profil uživatele a moje práva` (list)
- `N2.9 Historická route Obsazení`
- `N2.9.1 Redirect na Projekty/Index` (list)

- `N3 Identita a bezpečnost`
- `N3.1 Windows Authentication` (list)
- `N3.2 Uživatelský kontext a permission evaluation` (list)
- `N3.3 AD integrace`
- `N3.3.1 AD vyhledávání osob (query)` (list)
- `N3.3.2 Bez automatické AD synchronizace do dbo.osoby` (list)

- `N4 Data a DB lifecycle`
- `N4.1 SQL baseline bootstrap` (list)
- `N4.2 Upgrade patching` (list)
- `N4.3 Startup validator povinných číselníků` (list)

- `N5 Provoz, kvalita, recovery`
- `N5.1 Instalace a deployment IIS` (list)
- `N5.2 Provozní runbooky` (list)
- `N5.3 Testování a quality gates` (list)
- `N5.3.1 Visual/Playwright smoke governance` (list)
- `N5.4 Troubleshooting a recovery` (list)
- `N6 Řízená evoluce architektury`
- `N6.1 Baseline a sprint plán bezpečného refaktoru` (list)

### 5.2 Mapa listových uzlů -> dokumenty
| Uzel | Popis listu | Dokument |
| --- | --- | --- |
| N1.1.1 | Hosting pipeline a route | `docs/technical/02-architecture.md`, `docs/technical/05-web-server-iis-config.md` |
| N1.1.2 | AJAX kontrakt + antiforgery | `docs/technical/02-architecture.md` |
| N1.2.1 | Runtime config | `docs/technical/03-runtime-configuration.md` |
| N1.2.2 | SQL provider fail-fast | `docs/technical/03-runtime-configuration.md`, `docs/technical/06-database-bootstrap-migrations.md` |
| N1.3.1 | Traceability | `docs/technical/10-troubleshooting-recovery.md` |
| N2.1.* | Projekty/tým/subsystemy | `docs/technical/02-architecture.md`, `docs/technical/07-security-authz.md` |
| N2.2.* | Záznamy/komentáře/harmonogram | `docs/technical/02-architecture.md`, `docs/technical/07-security-authz.md` |
| N2.3.* | Jednání/účast/poznámky | `docs/technical/02-architecture.md`, `docs/technical/08-operations-runbooks.md` |
| N2.4.* | Osoby manual/AD | `docs/technical/07-security-authz.md`, `docs/technical/08-operations-runbooks.md` |
| N2.5.* | Číselníky | `docs/technical/07-security-authz.md` |
| N2.6.* | Role/permissions/effective rights | `docs/technical/07-security-authz.md` |
| N2.7.* | Export PDF/Word | `docs/technical/02-architecture.md`, `docs/technical/08-operations-runbooks.md` |
| N2.8.* | In-app docs/profil | `docs/technical/02-architecture.md` |
| N2.9.1 | Obsazení redirect | `docs/technical/02-architecture.md` |
| N3.1 | Windows Authentication | `docs/technical/05-web-server-iis-config.md`, `docs/technical/07-security-authz.md` |
| N3.2 | User context + permissions | `docs/technical/07-security-authz.md` |
| N3.3.1 | AD search | `docs/technical/03-runtime-configuration.md`, `docs/technical/07-security-authz.md` |
| N3.3.2 | AD bez auto-sync | `docs/technical/03-runtime-configuration.md`, `docs/technical/07-security-authz.md`, `docs/qa.md` |
| N4.1 | Baseline | `docs/technical/06-database-bootstrap-migrations.md` |
| N4.2 | Upgrade | `docs/technical/06-database-bootstrap-migrations.md` |
| N4.3 | Startup validator | `docs/technical/06-database-bootstrap-migrations.md` |
| N5.1 | Deployment | `docs/technical/04-installation-deployment-iis.md`, `docs/technical/05-web-server-iis-config.md` |
| N5.2 | Runbooky | `docs/technical/08-operations-runbooks.md` |
| N5.3 | Testy + docs gate | `docs/technical/09-testing-quality.md` |
| N5.3.1 | Visual/Playwright smoke governance | `docs/technical/11-visual-testing-governance.md` |
| N5.4 | Recovery | `docs/technical/10-troubleshooting-recovery.md` |
| N6.1 | Baseline refaktoru + sprint plán | `docs/technical/11-architecture-refactor-prompt-sequence.md`, `docs/technical/12-architecture-refactor-baseline.md` |

### 5.3 Kritérium „listového uzlu"
Uzel je list, pokud:
- má jednotný účel,
- není smysluplné ho dále dělit bez ztráty čitelnosti,
- lze ho popsat jedním stabilním kontraktem/procedurou,
- má přiřazenou odpovědnost (owner) a verifikační krok.

### 5.4 Mapa controllerů -> uzly stromu
| Controller | Primární uzly |
| --- | --- |
| `ProjektyController` | N2.1.1, N2.1.2, N2.1.3, N2.3.1 |
| `ZaznamyController` | N2.2.1, N2.2.2, N2.2.3, N2.2.4 |
| `JednaniController` | N2.3.1, N2.3.2, N2.3.3 |
| `OsobyController` | N2.4.1, N2.4.2, N2.4.3, N3.3.1, N3.3.2 |
| `CiselnikyController` | N2.5.1, N2.5.2 |
| `NastaveniController` | N2.6.1, N2.6.2, N2.6.3, N2.6.4, N2.6.5 |
| `ExportController` | N2.7.1, N2.7.2 |
| `DokumentaceController` | N2.8.1 |
| `ProfilController` | N2.8.2 |
| `ObsazeniController` | N2.9.1 |
| `HomeController` | N1.1.1 |

### 5.5 Mapa služeb -> uzly stromu
| Service | Primární uzly |
| --- | --- |
| `SqlServerDataStore` / `IPmTrackerDataStore` | N2.*, N4.* |
| `SqlStartupValidatorHostedService` | N1.2.2, N4.3 |
| `UserContextResolver` | N3.2 |
| `PermissionEvaluationService` | N2.6.*, N3.2 |
| `CommentAuthorizationPolicy` | N2.2.2, N3.2 |
| `ActiveDirectoryService` / `ADConnector` | N2.4.2, N3.3.1, N3.3.2 |
| `DictionariesService` + `DictionarySecurityPolicy` | N2.5.1, N2.5.2 |
| `SettingsService` | N2.6.* |
| `PeopleService` | N2.4.* |
| `RecordsService` | N2.2.* |
| `OpenXmlWordExportService` | N2.7.2 |
| `MarkdownDocumentationService` | N2.8.1 |
| `AjaxResponseContractGuardMiddleware` + `AjaxAntiforgeryResultFilter` | N1.1.2 |

## 6. Verifikace
- Ověř, že každý list z části 5.1 je uveden v mapě 5.2.
- Ověř, že každý controller z části 5.4 má mapování na minimálně jeden list.
- Ověř, že každá kritická služba z části 5.5 má mapování na list.
- Ověř, že každé kritické téma má konkrétní cílový dokument.
- Ověř, že uzel `N3.3.2` explicitně uvádí, že AD není automaticky synchronizováno.

## 7. Rollback
- Pokud strom neodpovídá aplikaci, vrať dokument na poslední validní revizi.
- Po rollbacku proveď audit pokrytí listů a oprav pouze chybějící větve.

## 8. Troubleshooting
- Chybí dokumentace pro listový uzel:
  - vytvoř/rozšiř cílový dokument,
  - doplň řádek do mapy 5.2,
  - ověř quality gate.
- Duplicitní listy v různých dokumentech:
  - ponech jednu primární lokaci a ostatní dokumenty nech jen jako referenci.

## 9. Audit a traceability
- Controllers source: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Controllers`
- Services source: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Services`
- Runtime bootstrap: `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Program.cs`
- Docs quality gate: `/Users/Pavel.Andrlik/Documents/PM Tracker/scripts/check-docs-quality.sh`
