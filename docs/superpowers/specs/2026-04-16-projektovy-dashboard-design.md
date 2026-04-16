# Projektový dashboard — specifikace

## Účel

Projektový dashboard poskytuje projektovému manažerovi rychlou analytiku konkrétního projektu přímo v aplikaci, bez potřeby SQL nebo Power BI. Dashboard slouží jako operativní nástroj pro řízení projektu (záznamy v prodlení, NES) i jako podklad pro roční report (statistiky, KPI).

## Navigace a přístup

### Vstup do dashboardu

V detailu projektu přibude záložka **Dashboard**. Záložka je viditelná pouze pro uživatele s projektovou rolí:

- `PROJ_MAN` (Projektový manažer)
- `ADM_PROJ` (Administrátor projektu)
- `GEST` (Gestor FIS)

Kliknutí na záložku přesměruje na samostatnou stránku na URL `/projekty/{id}/dashboard`.

### Stránka dashboardu

- Hlavička: název projektu + zkratka (kompaktní styl shodný se stávajícím detailem projektu), tlačítko "Zpět na projekt".
- Pod hlavičkou: horizontální řada vnitřních záložek — **Záznamy**, **NES v prodlení**, **Statické informace**, **Výzvy**.
- Výchozí aktivní záložka: Záznamy.
- Každá záložka se načítá lazy přes AJAX (shodný pattern s uživatelským dashboardem — `data-dashboard-panel`).
- Layout dashboardu využívá celou šířku obrazovky (full-width), na rozdíl od stávajících stránek s omezenou `max-width`.

### Role GEST

Role `GEST` (Gestor FIS) existuje v databázi jako projektová role, ale kód s ní nepočítá. Je potřeba:

- Přidat konstantu `Gestor = "GEST"` do `ProjectRoleCodes`.
- Přidat `GEST` do pole `RequiredProjectRoleCodes` v `SqlStartupValidatorHostedService`.
- Ověřit, že role je v seedu databáze a je zamčená.

## Architektura

### Přístup: Samostatný ProjectDashboardController

Dashboard má vlastní controller, service a views — oddělený od `ProjektyController`. Důvody:

- Shodný vzor s existujícím `DashboardController` (uživatelský dashboard).
- Dashboard má odlišnou doménu (statistiky, grafy, výzvy) od správy záznamů.
- `ProjektyController` je již rozsáhlý.

### Nové soubory

- `ProjectDashboardController` — route `/projekty/{id}/dashboard`.
- `IProjectDashboardService` + `ProjectDashboardService`.
- Views v `Views/ProjectDashboard/` — `Index.cshtml` + partial views pro každou záložku.
- ViewModels pro dashboard.

### Úpravy stávajících souborů

- `ProjectRoleCodes` — přidat `Gestor = "GEST"`.
- `SqlStartupValidatorHostedService` — přidat `GEST` do validace.
- `Detail.cshtml` (Projekty) — přidat záložku Dashboard s autorizací na role PM/ADM/GEST.
- `ProjektDetailViewModel` — přidat příznak `CanViewDashboard`.
- CSS — nový full-width layout pro dashboard stránku.

## Záložka 1: Záznamy

### Účel

Přehled úkolů, které jsou v prodlení, čekají na skutečnost nebo se blíží jejich termín. Zobrazují se jen záznamy typu **úkol** (pouze ty mají harmonogram).

### Definice problematického záznamu — tři kategorie

1. **V prodlení (skutečnost za plánem):** Krok harmonogramu má vyplněnou skutečnost, ale je horší než plán (kladná odchylka v dnech).
2. **Čeká na skutečnost (plán vypršel, skutečnost chybí):** Krok harmonogramu má plánovaný termín v minulosti, ale skutečnost dosud nebyla zaznamenána. Indikátor, že se na výsledek čeká (např. dodávka od dodavatele).
3. **Blíží se termín:** Krok harmonogramu má plánovaný termín v blízké budoucnosti (výchozí práh: 7 kalendářních dní, konfigurovatelné v kódu) a skutečnost ještě není vyplněna.

### Filtry nad tabulkou

- Přepínač kategorie: **Vše** | **V prodlení** | **Čeká na skutečnost** | **Blíží se termín**.
- Filtr subsystémů (sekundární, nice-to-have).
- Výchozí: Vše, seřazené podle závažnosti.

### Kompaktní řádek tabulky

| Číslo | Název | Subsystém | Vlastník | Termín ukončení | Kategorie | Nejhorší odchylka (dní) |
|---|---|---|---|---|---|---|

- **Kategorie:** barevný štítek — *V prodlení* (červená) / *Čeká na skutečnost* (oranžová) / *Blíží se termín* (žlutá).
- **Nejhorší odchylka:** nejvyšší odchylka ze všech problematických kroků daného záznamu; hlavní řadící kritérium.
- Řazení výchozí: podle nejhorší odchylky sestupně (nejhorší nahoře).
- Řádek je klikatelný — rozbalí detail.

### Rozbalitelný detail řádku

- Seznam problematických kroků: název kroku, plánovaný termín, skutečný termín (nebo *"nevyplněno"*), odchylka v dnech.
- U kroků bez skutečnosti: kolik dní od vypršení plánu uplynulo.
- Tlačítko **"Přejít na harmonogram"** — odkaz na záložku Harmonogram v detailu projektu s předvybráním daného záznamu.

### Prázdný stav

Pokud projekt nemá žádné záznamy typu úkol, nebo žádný nesplňuje kritéria: *"Žádné záznamy k zobrazení — všechny úkoly jsou v pořádku."*

## Záložka 2: NES v prodlení

### Stav implementace

Závisí na integraci ServiceDesku, která zatím není naprogramovaná. Dokud nebude integrace hotová, záložka zobrazí informační hlášku: *"Napojení na ServiceDesk není k dispozici."*

### Účel

Přehled nesrovnalostí (NES) z externího ticketovacího systému, které jsou v prodlení podle interních SLA ServiceDesku. Slouží projektovému manažerovi mimo jiné pro vyměřování penále.

### Zdroj dat

- Projekt má v nastavení vazbu na informační systém v ServiceDesku (nový sloupec `ServiceDeskInfoSystemId` na tabulce projektu).
- Dashboard zobrazuje NES patřící pod tento informační systém a jeho podřízenou strukturu (subsystém, modul).
- Menší projekty bez přímé vazby na informační systém zobrazují jen NES navázané přes externí vazby na záznamech.

### Kompaktní řádek tabulky

| Číslo ticketu | Název/popis | Prodlení (SLA) | Navázaný záznam | Stav ticketu |
|---|---|---|---|---|

- **Prodlení (SLA):** hodnota přímo ze ServiceDesku (formát bude upřesněn při integraci).
- **Navázaný záznam:** pokud existuje externí vazba na záznam v PM Trackeru, zobrazí se číslo a odkaz; jinak prázdné.
- Řazení výchozí: podle prodlení sestupně.

### Export do Excelu

Tlačítko **"Export do Excelu"** s výběrem období (od-do). Generuje `.xlsx` se seznamem NES v prodlení za zvolené období pro účely vyměřování penále. Výchozí období = aktuální stav (k dnešnímu datu). Export se týká pouze této záložky.

### Prázdný stav

*"Žádné nesrovnalosti v prodlení."*

## Záložka 3: Statické informace

### Účel

Průběžná příprava na roční report projektu. Roční statistiky a KPI o průběhu záznamů/úkolů. V kontextu státní správy jsou klíčové zejména plnění harmonogramu a čerpání rozpočtu.

### Ovládání

Dropdown s výběrem roku nahoře. Výchozí: aktuální rok. Data se načtou lazy po změně roku.

### KPI karty — horní řada

Kompaktní karty s číslem, trendem oproti předchozímu roku (šipka nahoru/dolů) a popiskem:

**Plnění úkolů:**

- **Splněno** — počet úkolů uzavřených jako splněné v daném roce.
- **Zrušeno** — počet zrušených úkolů.
- **Přeneseno** — úkoly plánované v roce, ale nedokončené (carry-over do dalšího roku).
- **Včasnost plnění** — % úkolů dokončených do původního termínu (On-time Delivery Rate).

**Harmonogram:**

- **V prodlení** — počet úkolů, které byly během roku v prodlení.
- **Průměrné prodlení** — průměrný počet dní zpoždění u opožděných úkolů.
- **Prodlouženo** — počet úkolů s formálně posunutým termínem (deadline extension).

### Podrobnější statistiky — pod kartami

**Plnění po čtvrtletích:**

Tabulka nebo sloupcový graf — plánovaná vs. skutečná dokončení úkolů po čtvrtletích. Ukazuje sezónní vzorce a bottlenecky.

**Přehled po subsystémech:**

Tabulka s rozpadem KPI karet na subsystém — kolik úkolů splněno, v prodlení, zrušeno atd. per subsystém. Pomáhá identifikovat problémové oblasti.

**ServiceDesk metriky** (závisí na integraci):

- **NES objem** — počet nesrovnalostí otevřených v roce, měsíční trend.
- **NES vyřešeno** — % uzavřených ze všech otevřených (backlog indikátor).
- **PMP/PNF objem** — počet schválených vs. zamítnutých požadavků.
- Dokud nebude integrace: *"Napojení na ServiceDesk není k dispozici."*

**Čerpání rozpočtu:**

- Horizontální bar graf: vyčerpáno / celková suma.
- Celková suma = ručně zadaná na projektu (nový sloupec `RozpocetCelkem` na tabulce projektu).
- Vyčerpáno = agregace z výzev a PNF.
- Závisí na integraci ServiceDesku. Bez integrace: *"Čerpání rozpočtu vyžaduje napojení na ServiceDesk."*
- Pokud projekt nemá zadanou celkovou sumu: *"Celková suma rozpočtu není nastavena."*

**Docházka na jednání:**

- Průměrná účast na jednáních projektu za rok (% skutečných účastníků z pozvaných).
- Data dostupná z existující tabulky `UcastEntity`.

### Prázdný stav

Pokud projekt nemá za vybraný rok žádná data, KPI karty zobrazí nuly: *"Za vybraný rok nejsou k dispozici žádné záznamy."*

## Záložka 4: Výzvy

### Stav implementace

Závisí na integraci ServiceDesku (výzva se skládá z PNF + navázaných záznamů). Konkrétní vizuální a obsahový vzhled bude dodán později. Specifikace popisuje rámcový cílový stav.

### Účel

Prohlížení existujících výzev a generování nových. Výzva = objednávka složená z PNF a k nim navázaných rozvojových úkolů/akcí (záznamů). Dosud se výzvy skládají ručně — dashboard toto automatizuje.

### Layout

- Seznam existujících výzev — tabulka s číslem, datem vytvoření, stavem, celkovou částkou.
- Tlačítko **"Nová výzva"** — workflow pro automatické sestavení výzvy z PNF a navázaných záznamů.
- U každé výzvy tlačítko **"Export do PDF"** — pro odeslání mailem.
- Detail výzvy po rozkliknutí: seznam PNF a navázaných záznamů, které výzvu tvoří.

### Rámcová poznámka

Konkrétní podoba formuláře pro generování výzvy, pravidla pro výběr PNF a formát PDF exportu budou dodány později.

### Prázdný stav

*"Žádné výzvy. Generování výzev vyžaduje napojení na ServiceDesk."*

## Databázové změny

### Tabulka `projekty` — nové sloupce

- `ServiceDeskInfoSystemId` (nullable) — vazba na informační systém v ServiceDesku. Jeden projekt = jeden informační systém.
- `RozpocetCelkem` (nullable decimal) — ručně zadávaná celková suma rozpočtu.

### Tabulka `ciselnik_roli_projektu`

- Ověřit, že role `GEST` (Gestor FIS) je v seedu a zamčená.

## Implementační fáze

### Fáze 1 — implementovatelné nyní

- Celá kostra dashboardu (controller, service, views, navigace, lazy loading, full-width layout).
- Záložka **Záznamy** — plně funkční z existujících dat.
- Záložka **Statické informace** — KPI karty z existujících dat (splněno, zrušeno, přeneseno, včasnost, prodlení, prodlouženo), plnění po čtvrtletích, přehled po subsystémech, docházka na jednání.
- Role `GEST` v kódu.
- DB sloupce na tabulce projektu.
- Placeholder záložky pro NES a Výzvy s informačními hláškami.

### Fáze 2 — po integraci ServiceDesku

- Záložka **NES v prodlení** — naplnění daty, export do Excelu.
- Záložka **Výzvy** — workflow, generování, PDF export.
- ServiceDesk metriky na Statických informacích (NES objem, PMP/PNF, čerpání rozpočtu).
- Naplnění sloupce `ServiceDeskInfoSystemId` hodnotami.

## Testy

Testovací pokrytí rozšiřuje stávající strukturu projektu — unit testy s FluentAssertions a fake services, integrační testy proti databázi, E2E testy v prohlížeči.

### Unit testy — `PmTracker.Tests.Unit/ProjectDashboard/`

Nový adresář `ProjectDashboard/` po vzoru stávajícího `Home/` (uživatelský dashboard).

#### ProjectDashboardControllerBehaviorTests

Testy controlleru s fake service (vzor: `DashboardControllerBehaviorTests`):

- **Index_ShouldRenderDashboardShell** — ověří, že akce vrací `ViewResult` s `ProjectDashboardPageViewModel`.
- **Index_ShouldReturnNotFound_WhenUserLacksProjectAccess** — uživatel nemá projekt ve `VisibleProjectIds`.
- **Index_ShouldReturnForbid_WhenUserHasNoRequiredProjectRole** — uživatel má přístup k projektu, ale nemá roli PM/ADM/GEST.
- **Index_ShouldAllowAccess_ForProjectManager** — uživatel s rolí `PROJ_MAN` na projektu vidí dashboard.
- **Index_ShouldAllowAccess_ForGestor** — uživatel s rolí `GEST` na projektu vidí dashboard.
- **Index_ShouldAllowAccess_ForProjectAdmin** — uživatel s rolí `ADM_PROJ` na projektu vidí dashboard.
- **RecordsPanel_ShouldReturnPartialView** — akce pro lazy-loaded záložku Záznamy vrací `PartialViewResult`.
- **StatisticsPanel_ShouldReturnPartialView** — akce pro lazy-loaded záložku Statické informace vrací `PartialViewResult`.
- **NesPanel_ShouldReturnPlaceholder_WhenServiceDeskNotIntegrated** — záložka NES vrací placeholder hlášku.
- **VyzyyPanel_ShouldReturnPlaceholder_WhenServiceDeskNotIntegrated** — záložka Výzvy vrací placeholder hlášku.

#### ProjectDashboardServiceTests

Testy business logiky service (vzor: `HomeDashboardServiceTests`):

**Kategorizace záznamů:**

- **ShouldCategorizeRecord_AsDelayed_WhenStepHasPositiveOffset** — krok harmonogramu s vyplněnou skutečností za plánem → kategorie "V prodlení".
- **ShouldCategorizeRecord_AsAwaitingActual_WhenPlanExpiredAndActualMissing** — krok s plánovaným termínem v minulosti a bez skutečnosti → kategorie "Čeká na skutečnost".
- **ShouldCategorizeRecord_AsApproachingDeadline_WhenPlanWithinThreshold** — krok s plánovaným termínem do 7 dní a bez skutečnosti → kategorie "Blíží se termín".
- **ShouldNotIncludeRecord_WhenAllStepsOnTrack** — záznam kde všechny kroky jsou v pořádku se na dashboardu neobjeví.
- **ShouldNotIncludeRecord_WhenNotTaskType** — záznam jiného typu než úkol (nemá harmonogram) se nezobrazí.
- **ShouldCalculateWorstOffset_AcrossAllProblematicSteps** — nejhorší odchylka se počítá ze všech kroků, ne jen prvního.

**Řazení:**

- **ShouldSortRecords_ByWorstOffsetDescending** — záznamy s největší odchylkou jsou nahoře.
- **ShouldSortDelayedBeforeAwaiting_BeforeApproaching** — při stejné odchylce se řadí podle závažnosti kategorie.

**Filtrování:**

- **ShouldFilterByCategory_WhenCategoryFilterApplied** — filtr "V prodlení" zobrazí jen záznamy v prodlení.
- **ShouldFilterBySubsystem_WhenSubsystemFilterApplied** — filtr subsystému zobrazí jen záznamy daného subsystému.

**Statistiky (Statické informace):**

- **ShouldCalculateCompletedCount_ForSelectedYear** — počet splněných úkolů odpovídá vybranému roku.
- **ShouldCalculateCancelledCount_ForSelectedYear** — počet zrušených úkolů.
- **ShouldCalculateCarryOverCount_ForSelectedYear** — úkoly plánované v roce, ale nedokončené.
- **ShouldCalculateOnTimeRate_ForSelectedYear** — % včas dokončených úkolů.
- **ShouldCalculateDelayedCount_ForSelectedYear** — počet opožděných úkolů.
- **ShouldCalculateAverageDelay_ForSelectedYear** — průměrný počet dní zpoždění.
- **ShouldCalculateExtendedCount_ForSelectedYear** — počet úkolů s posunutým termínem.
- **ShouldCalculateQuarterlyBreakdown_ForSelectedYear** — plánovaná vs. skutečná dokončení po čtvrtletích.
- **ShouldCalculateSubsystemBreakdown_ForSelectedYear** — rozpad KPI per subsystém.
- **ShouldCalculateAttendanceRate_ForSelectedYear** — průměrná účast na jednáních.
- **ShouldReturnZeros_WhenNoDataForYear** — prázdný rok vrátí nulové hodnoty.

**Prázdné stavy:**

- **ShouldReturnEmptyMessage_WhenProjectHasNoTasks** — projekt bez úkolů vrátí prázdný stav.
- **ShouldReturnEmptyMessage_WhenAllTasksOnTrack** — všechny úkoly v pořádku vrátí prázdný stav.

### Unit testy — `PmTracker.Tests.Unit/Security/`

Rozšíření stávajících permission testů:

#### ProjectRoleCodesTests

- **GestorConstant_ShouldEqual_GEST** — konstanta `ProjectRoleCodes.Gestor` má hodnotu `"GEST"`.

#### CurrentUserContextPermissionTests (rozšíření)

- **HasProjectRole_ShouldReturnTrue_WhenUserHasGestorRole** — metoda pro kontrolu projektové role rozpozná `GEST`.
- **CanViewDashboard_ShouldBeTrue_WhenUserHasProjectManagerRole** — `CanViewDashboard` je true pro PM.
- **CanViewDashboard_ShouldBeTrue_WhenUserHasProjectAdminRole** — `CanViewDashboard` je true pro admin.
- **CanViewDashboard_ShouldBeTrue_WhenUserHasGestorRole** — `CanViewDashboard` je true pro gestora.
- **CanViewDashboard_ShouldBeFalse_WhenUserHasNoRequiredRole** — `CanViewDashboard` je false pro běžného uživatele.

### Integrační testy — `PmTracker.Tests.Integration/Projects/`

Rozšíření stávající integrační testovací infrastruktury:

- **ProjectDashboard_ShouldLoadRecordsTab_WithDelayedTasks** — ověří, že se z databáze správně načtou a kategorizují opožděné úkoly.
- **ProjectDashboard_ShouldLoadStatistics_ForYear** — ověří agregaci ročních KPI z reálných dat.
- **ProjectDashboard_ShouldRespectProjectRoleAccess** — ověří, že uživatel bez požadované role nevidí dashboard data.

### E2E testy — `PmTracker.Tests.E2E/Scenarios/`

Nové scénáře v prohlížeči:

#### ProjectDashboardScenariosTests

- **DashboardTab_ShouldBeVisible_ForProjectManager** — záložka Dashboard se zobrazí pro PM.
- **DashboardTab_ShouldNotBeVisible_ForRegularUser** — záložka se nezobrazí pro běžného uživatele.
- **DashboardTab_ShouldNavigateToFullPage** — kliknutí na záložku přesměruje na `/projekty/{id}/dashboard`.
- **RecordsTab_ShouldLoadLazy_AndShowTable** — záložka Záznamy se načte lazy a zobrazí tabulku.
- **RecordsTab_ShouldExpandRow_OnClick** — kliknutí na řádek rozbalí detail s problematickými kroky.
- **RecordsTab_ShouldFilterByCategory** — přepínač kategorie filtruje záznamy.
- **StatisticsTab_ShouldLoadLazy_AndShowKpiCards** — záložka Statické informace zobrazí KPI karty.
- **StatisticsTab_ShouldSwitchYear** — změna roku přenačte data.
- **NesTab_ShouldShowPlaceholder** — záložka NES zobrazí placeholder.
- **VyzyyTab_ShouldShowPlaceholder** — záložka Výzvy zobrazí placeholder.
- **DashboardLayout_ShouldUseFullWidth** — dashboard využívá celou šířku obrazovky.
