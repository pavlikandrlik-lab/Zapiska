# Analýza bodů 8–12: Dashboard Záznamy / NES / Statistiky / Výzvy / Vyhledávání

**Datum:** 2026-06-25  
**Autor:** Claude (na základě zadání Ing. Andrlík)  
**Branch:** `codex/senior-refactor-fase-1`  
**Scope:** Hloubková analýza nalezených chyb a návrhů redesignu

---

## Obsah

1. [Bod 8 — Dashboard Záznamy](#bod-8--dashboard-záznamy)
2. [Bod 9 — NES v prodlení](#bod-9--nes-v-prodlení)
3. [Bod 10 — Statické informace](#bod-10--statické-informace)
4. [Bod 11 — Výzvy](#bod-11--výzvy)
5. [Bod 12 — Vyhledávání](#bod-12--vyhledávání)

---

## Bod 8 — Dashboard Záznamy

### 8.1 Aktuální stav

**Controller:** `ProjectDashboardController.RecordsPanel()` → [ProjectDashboardController.cs:54-66](PmTracker.Web/Controllers/ProjectDashboardController.cs#L54-L66)  
**Service:** `ProjectDashboardService.BuildRecordsPanelAsync()` → [ProjectDashboardService.cs:58-182](PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs#L58-L182)  
**View:** [_RecordsPanel.cshtml](PmTracker.Web/Views/ProjectDashboard/_RecordsPanel.cshtml)  
**JS:** [projectDashboard.js](PmTracker.Web/wwwroot/js/modules/projectDashboard.js) — řádky 108-166 (category filter), 143-166 (expand/collapse), 279-291 (click handler)  
**CSS:** [site.css:7069-7117](PmTracker.Web/wwwroot/css/site.css#L7069-L7117) — jen `.dashboard-expandable-row` a nepoužité třídy

#### Datový tok

```
RecordsPanel(projektId) 
  → BuildRecordsPanelAsync(projectId, localNow)
    → CiselnikKategoriiZaznamu.Where(Kod == "U" || Kod == "UKOL")    [taskCategoryIds]
    → CiselnikStavuUkolu.Where(IsFinal)                               [finalStateIds]
    → ProjektoveZaznamy JOIN Subsystemy JOIN Osoby
        WHERE ProjektId == projectId
          AND KategorieId IN taskCategoryIds
          AND (StavUkoluId IS NULL OR NOT IN finalStateIds)
        ORDER BY DatumZalozeni DESC
        TAKE 500
    → ZaznamHarmonogramKroky.Where(ZaznamId IN recordIds)
    → Per record: ScheduleDateCalculator.Compute() → DashboardRecordCategorizer.CategorizeRecord()
    → Filtr: jen záznamy s ProblematicSteps.Count > 0
    → Řazení: Delayed (severity 0) > AwaitingActual (1) > ApproachingDeadline (2), pak WorstOffsetDays DESC
  → PartialView("_RecordsPanel.cshtml", model)
```

#### Kategorizace kroků (DashboardRecordCategorizer.cs)

| Kategorie | Podmínka | Vizuál |
|---|---|---|
| **Delayed** | `OffsetDays > 0` (skutečnost je za plánem) | `gov-tag color="error"` |
| **AwaitingActual** | Plan expired (`PlanEnd < today`), skutečnost chybí | `gov-tag color="warning"` |
| **ApproachingDeadline** | Plan do 7 dní, skutečnost chybí | `gov-tag color="neutral"` |

### 8.2 Identifikované problémy

#### 8.2.1 KRITICKÉ: Chybějící CSS pro rozbalitelný detail

View definuje 7 CSS tříd, pro které **neexistuje žádné CSS pravidlo** v `site.css`:

| Třída v HTML | Kde v view | CSS existuje? |
|---|---|---|
| `dashboard-category-filter-row` | _RecordsPanel.cshtml:4 | **NE** (existuje jen nepoužitá `dashboard-category-filters`) |
| `dashboard-records-table-wrapper` | _RecordsPanel.cshtml:25 | **NE** |
| `dashboard-records-table` | _RecordsPanel.cshtml:26 | **NE** |
| `dashboard-expandable-detail` | _RecordsPanel.cshtml:84 | **NE** (existuje `dashboard-step-detail` ale ta není použita) |
| `dashboard-expandable-detail-body` | _RecordsPanel.cshtml:86 | **NE** |
| `dashboard-steps-table` | _RecordsPanel.cshtml:89 | **NE** |
| `dashboard-expandable-detail-actions` | _RecordsPanel.cshtml:133 | **NE** |

**Důsledek:** Vše je jednou barvou, rozbalený detail nemá vizuální odlišení od zbytku tabulky, tlačítko „Přejít na harmonogram" splývá s textem.

#### 8.2.2 STŘEDNÍ: Neexistující utility třída `text-danger`

`_RecordsPanel.cshtml:117` používá `<span class="text-danger">` pro zobrazení kladné odchylky ve dnech. V `site.css` žádný selektor `text-danger` neexistuje → červená barva se neaplikuje.

#### 8.2.3 STŘEDNÍ: Mismatch CSS tříd v site.css

`site.css:7077-7085` definuje `.dashboard-step-detail` / `.dashboard-step-detail.expanded` (display: none → table-row), ale view používá `.dashboard-expandable-detail` s atributem `hidden` (JS toggle). CSS pravidlo je mrtvý kód.

Stejně tak `site.css:7089-7117` definuje `.dashboard-category-filters` / `.dashboard-category-filter`, ale view používá `dashboard-category-filter-row` s `<pm-button>` komponentami. Opět mrtvý kód.

#### 8.2.4 NÍZKÁ: Ověření správnosti dat po rozkliknutí

Rozbalený detail zobrazuje `record.ProblematicSteps` — data jsou předpočtena na serveru (BuildRecordsPanelAsync), nikoli lazy-loaded. To je správně z hlediska UX (žádné další volání). Ale:

- **Guard pro `JeAktualniKrok`** ([ProjectDashboardService.cs:133](PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs#L133)): Dashboard filtruje projekce aktuálního kroku (vizuální prvek lišty) z kategorializace. Pokud by tento guard byl odstraněn, objevily by se falešné „v prodlení" záznamy.
- **TAKE 500** ([ProjectDashboardService.cs:95](PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs#L95)): Pro velmi velké projekty (>500 záznamů typu úkol) se některé záznamy nezobrazí. Žádné UI upozornění na truncation.

### 8.3 Návrh řešení

#### A. CSS stylování rozbalitelného detailu

Přidat do `site.css` (nebo lépe do `components/project-dashboard.css`):

```css
/* Zarovnání filtrů nad tabulkou */
.dashboard-category-filter-row {
    display: flex;
    flex-wrap: wrap;
    gap: 0.5rem;
    margin-bottom: 1rem;
}

/* Wrapper pro horizontální scroll na úzkých obrazovkách */
.dashboard-records-table-wrapper {
    overflow-x: auto;
}

/* Hlavní tabulka záznamů */
.dashboard-records-table {
    width: 100%;
    border-collapse: collapse;
}
.dashboard-records-table th,
.dashboard-records-table td {
    padding: 0.5rem 0.75rem;
    text-align: left;
    border-bottom: 1px solid var(--pm-border);
    font-size: var(--d-fs-base, 0.875rem);
}
.dashboard-records-table thead th {
    font-weight: 600;
    color: var(--pm-text-muted);
    font-size: var(--d-fs-label, 0.8125rem);
    text-transform: uppercase;
    letter-spacing: 0.03em;
}

/* Rozbalený detail — vizuálně odlišený */
.dashboard-expandable-detail td {
    padding: 0;
    border-bottom: 2px solid var(--pm-border);
}
.dashboard-expandable-detail-body {
    padding: 1rem 1.25rem;
    background: color-mix(in srgb, var(--gov-color-primary) 3%, var(--pm-surface));
    border-left: 3px solid var(--gov-color-primary);
}

/* Tabulka kroků uvnitř detailu */
.dashboard-steps-table {
    width: 100%;
    border-collapse: collapse;
    margin-bottom: 0.75rem;
}
.dashboard-steps-table th,
.dashboard-steps-table td {
    padding: 0.35rem 0.5rem;
    font-size: var(--d-fs-label, 0.8125rem);
}

/* Akční tlačítka v detailu — ghost buttons */
.dashboard-expandable-detail-actions {
    display: flex;
    gap: 0.5rem;
    padding-top: 0.75rem;
    border-top: 1px solid var(--pm-border);
}

/* Utility pro červenou odchylku */
.text-danger {
    color: #dc2626;
    font-weight: 600;
}
```

#### B. Smazat mrtvý CSS

Odstranit z `site.css`:
- `.dashboard-step-detail` / `.dashboard-step-detail.expanded` (řádky 7077-7085)
- `.dashboard-category-filters` / `.dashboard-category-filter` (řádky 7089-7117)

#### C. Edge cases

| Scénář | Aktuální chování | Doporučení |
|---|---|---|
| Projekt > 500 záznamů typu úkol | Truncation bez upozornění | Přidat banner „Zobrazeno prvních 500 záznamů" |
| Záznam bez harmonogramového kroku | `continue` v cyklu — přeskočen | OK, ale přidat empty-state „Žádné problematické záznamy" |
| Krok K1 s `JeAktualniKrok=true` | Vyfiltrován z kategorializace | OK — guard je správný |
| Dark mode | Třídy bez CSS → žádný dark override potřeba (zatím) | Při přidání CSS přidat `:root[data-theme="dark"]` varianty |

---

## Bod 9 — NES v prodlení

### 9.1 Aktuální stav

**Controller:** `ProjectDashboardController.NesPanel()` → [ProjectDashboardController.cs:82-94](PmTracker.Web/Controllers/ProjectDashboardController.cs#L82-L94)  
**Service:** `ProjectDashboardService.BuildNesPanelAsync()` → [ProjectDashboardService.cs:312-356](PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs#L312-L356)  
**Query:** `SqlInformacniSystemQueryService.GetProdleneAsync()` → [SqlInformacniSystemQueryService.cs:45-112](PmTracker.ServiceDesk.Sql/SqlInformacniSystemQueryService.cs#L45-L112)  
**View:** [_NesPanel.cshtml](PmTracker.Web/Views/ProjectDashboard/_NesPanel.cshtml)  
**CSS:** Žádné CSS pravidla pro `pm-nes-panel*` třídy v site.css

#### Datový tok

```
NesPanel(projektId)
  → BuildNesPanelAsync(projektId, reference)
    → Projekty.Where(Id == projektId).Select(ServiceDeskInfoSystemId)
    → pokud ServiceDeskInfoSystemId IS NULL → return IsServiceDeskIntegrated=false
    → SdInfoSystemy.ById(isId) → zkratka (FIS/ISSP)
    → _isQueryService.GetProdleneAsync(isId, reference)
      → HOT_MODULY.Where(IdIS == isId) → modulyProIs
      → HOT_ZAZNAMY
          WHERE TypZaznamu == "NES" AND Stav != "archiv" 
            AND Modul IN modulyProIs AND SlaDeadline < reference
        UNION
        HOT_ZAZNAMY
          WHERE TypZaznamu IN ("PMP","PNF") AND Stav != "archiv"
            AND Modul IN modulyProIs AND DatResT < reference
      → DATEDIFF(reference, termin)
    → Map to NesPanelItemViewModel[]
  → PartialView("_NesPanel.cshtml", model)
```

### 9.2 Identifikované problémy

#### 9.2.1 KRITICKÉ: Route parameter mismatch v NES panelu

**Soubor:** [_NesPanel.cshtml:28](PmTracker.Web/Views/ProjectDashboard/_NesPanel.cshtml#L28)

```csharp
var projektIdFromRoute = ViewContext.RouteData.Values.TryGetValue("id", out var rid) ? rid?.ToString() : null;
```

Controller route je `[Route("projekty/{projektId:int}/dashboard")]` — parametr se jmenuje `projektId`, NE `id`. View hledá `"id"` → **vždy vrátí null** → export tlačítko se nikdy nezobrazí i když existují položky.

**Dopad:** Excel export NES panelu je nefunkční (tlačítko je skryté, protože `projektIdFromRoute` je vždy `null`).

**Fix:** Změnit `"id"` na `"projektId"` v `TryGetValue`, a `asp-route-id` na `asp-route-projektId`.

#### 9.2.2 KRITICKÉ: Žádné CSS pro NES panel

View používá tyto třídy, pro které neexistuje CSS:

| Třída | Kde | Účel |
|---|---|---|
| `pm-nes-panel` | _NesPanel.cshtml:18 | Wrapper sekce |
| `pm-nes-panel__header` | _NesPanel.cshtml:19 | Header s KPI |
| `pm-nes-panel__kpi` | _NesPanel.cshtml:21 | KPI statistiky |
| `pm-nes-panel__export` | _NesPanel.cshtml:32 | Export odkaz |
| `pm-nes-panel__table` | _NesPanel.cshtml:49 | Tabulka ticketů |

**Důsledek:** NES panel vypadá jako nestylovaný HTML — žádné pozadí, ohraničení, odsazení tabulky.

#### 9.2.3 STŘEDNÍ: „Nic neukazuje" — prázdný seznam

Pokud `Items.Count == 0` a `IsServiceDeskIntegrated == true`, view zobrazí jen:
```html
<p class="muted">Žádné tickety v prodlení.</p>
```

Možné příčiny prázdného seznamu:
1. **Žádné moduly pod IS** — projekt má `ServiceDeskInfoSystemId` ale v `HOT_MODULY` neexistují moduly pro tento IS
2. **Všechny tickety jsou „archiv"** — filtr `Stav != "archiv"` odstraní vše
3. **Žádný ticket v prodlení** — SLA deadline / DatResT jsou v budoucnosti
4. **Chybné mapování IS** — projekt má nastavený špatný IS ID

#### 9.2.4 NÍZKÁ: ServiceDeskUrl hardcoded

[ServiceDeskUrlBuilder.cs](PmTracker.Web/Services/ServiceDesk/ServiceDeskUrlBuilder.cs) má hardcoded base URL. Na intranetu bez internetu to funguje, ale URL musí odpovídat aktuálnímu prostředí.

### 9.3 Návrh řešení

#### A. Fix route parameter

```diff
- var projektIdFromRoute = ViewContext.RouteData.Values.TryGetValue("id", out var rid) ? rid?.ToString() : null;
+ var projektIdFromRoute = ViewContext.RouteData.Values.TryGetValue("projektId", out var rid) ? rid?.ToString() : null;
```

A v `asp-route-*`:
```diff
- asp-route-id="@projektIdFromRoute"
+ asp-route-projektId="@projektIdFromRoute"
```

#### B. CSS stylování NES panelu

Doporučený styl: záznamy bílé na šedém pozadí s ohraničením (dle zadání):

```css
.pm-nes-panel {
    background: var(--pm-surface-muted, #f1f5f9);
    border-radius: var(--d-radius, 0.5rem);
    padding: 1rem 1.25rem;
}

.pm-nes-panel__header {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 1rem;
    margin-bottom: 1rem;
}

.pm-nes-panel__header h2 {
    margin: 0;
    font-size: 1.05rem;
    font-weight: 600;
}

.pm-nes-panel__kpi {
    display: flex;
    gap: 1.5rem;
    font-size: 0.875rem;
}

.pm-nes-panel__export {
    margin-left: auto;
    font-size: 0.8125rem;
    color: var(--gov-color-primary);
    text-decoration: underline;
}

.pm-nes-panel__table {
    width: 100%;
    border-collapse: separate;
    border-spacing: 0 0.35rem;
}

.pm-nes-panel__table thead th {
    font-size: 0.75rem;
    text-transform: uppercase;
    letter-spacing: 0.04em;
    color: var(--pm-text-muted);
    padding: 0.35rem 0.75rem;
}

.pm-nes-panel__table tbody tr {
    background: #fff;
    border: 1px solid var(--pm-border);
    border-radius: 0.375rem;
    box-shadow: 0 1px 2px rgba(0,0,0,0.04);
}

.pm-nes-panel__table tbody td {
    padding: 0.6rem 0.75rem;
    font-size: 0.875rem;
}

.pm-nes-panel__table tbody td:first-child {
    border-radius: 0.375rem 0 0 0.375rem;
}
.pm-nes-panel__table tbody td:last-child {
    border-radius: 0 0.375rem 0.375rem 0;
}
```

#### C. Diagnostika prázdného seznamu

Přidat do view podrobnější info při prázdném stavu:

```razor
@if (Model.Items.Count == 0 && Model.IsServiceDeskIntegrated)
{
    <p class="muted">
        Žádné tickety v prodlení pro IS @Model.IsZkratka.
        Zkontrolujte, zda existují moduly pod tímto informačním systémem 
        a zda tickety mají vyplněný SLA termín.
    </p>
}
```

---

## Bod 10 — Statické informace

### 10.1 Aktuální stav

**Controller:** `ProjectDashboardController.StatisticsPanel()` → [ProjectDashboardController.cs:68-80](PmTracker.Web/Controllers/ProjectDashboardController.cs#L68-L80)  
**Service:** `ProjectDashboardService.BuildStatisticsPanelAsync()` → [ProjectDashboardService.cs:184-310](PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs#L184-L310)  
**View:** [_StatisticsPanel.cshtml](PmTracker.Web/Views/ProjectDashboard/_StatisticsPanel.cshtml)  
**JS:** [projectDashboard.js:172-206](PmTracker.Web/wwwroot/js/modules/projectDashboard.js#L172-L206) — `reloadStatisticsPanel(shell, year)`, [projectDashboard.js:307-314](PmTracker.Web/wwwroot/js/modules/projectDashboard.js#L307-L314) — `handleProjectDashboardChange` (year select handler)

#### Datový tok pro year selector

```
User vybere rok v <select data-dashboard-year-select>
  → JS: handleProjectDashboardChange() zachytí "change" event
    → parseInt(yearSelect.value, 10) → year
    → reloadStatisticsPanel(shell, year)
      → baseUrl = panel.dataset.dashboardPanelUrl (= "/projekty/{id}/dashboard/statistics-panel")
      → fetch(baseUrl + "?year=" + year) → replace innerHTML
  → Server: StatisticsPanel(projektId, year)
    → BuildStatisticsPanelAsync(projectId, year)
      → rangeStart = yearStart.AddYears(-1)     // (year-1)-01-01
      → rangeEnd = yearEnd.AddYears(1).AddDays(-1)  // ← BUG
      → Query ProjektoveZaznamy WHERE DatumUkonceni BETWEEN rangeStart AND rangeEnd
      → Compute availableYears = DISTINCT years from all loaded records
      → Render new <select> s availableYears
```

### 10.2 Identifikované problémy

#### 10.2.1 KRITICKÉ: Bug ve výpočtu rozsahu dat — rok 2027 a zacyklení selectoru

**Soubor:** [ProjectDashboardService.cs:199-200](PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs#L199-L200)

```csharp
var rangeStart = yearStart.AddYears(-1);              // OK: (year-1)-01-01
var rangeEnd = yearEnd.AddYears(1).AddDays(-1);       // BUG!
```

**Problém:** Pro `year = 2025`:
- `yearEnd = new DateTime(2025, 12, 31)`
- `yearEnd.AddYears(1) = 2026-12-31`
- `.AddDays(-1) = 2026-12-30` ← chybí 31.12.2026!

**Skutečný dopad — zacyklení:** Mechanismus je horší než jen chybějící den:

1. `availableYears` se počítá z `allRecords` ([ProjectDashboardService.cs:272-277](PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs#L272-L277)):
   ```csharp
   var availableYears = allRecords
       .Select(r => r.DatumZalozeni.Year)
       .Concat(allRecords.Select(r => r.DatumUkonceni.Year))
       .Distinct()
       .OrderDescending()
       .ToList();
   ```

2. Při výběru roku 2025 se načtou záznamy z rozsahu `2024-01-01 .. 2026-12-30`. Z těchto záznamů se extrahují roky → `availableYears` obsahuje jen roky přítomné v datech.

3. Pokud záznam má `DatumUkonceni = 2027-xx-xx` (plán do budoucna), ten se nedostane do `allRecords` (je mimo range), ale jeho `DatumZalozeni` z roku 2025 se do range vejde → rok 2027 se může objevit v `availableYears` z `DatumZalozeni.Year`.

4. **Zacyklení:** Když user vybere rok 2023:
   - Range: `2022-01-01 .. 2024-12-30`
   - Pokud v range nejsou záznamy z 2024 nebo 2025, `availableYears` bude `[2023, 2022]` → nelze se vrátit zpět na 2025
   - Obnovení stránky (F5) dá default `year = currentYear = 2025`, ale pokud user přepne záložky, tab panel si drží stav (lazy-load, `dashboardPanelLoaded = "true"`)

**Root cause:** Range pro SQL dotaz závisí na vybraném roku, a `availableYears` je počítán z výsledku téhož dotazu. Kuřecí-vajíčko problém.

#### 10.2.2 STŘEDNÍ: `Zruseno` KPI vždy vrací 0

**Soubor:** [ProjectDashboardService.cs:426](PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs#L426)

```csharp
return new YearKpiSnapshot(splneno, 0 /* Zruseno */, preneseno, vcasnostPct, ...);
```

`Zruseno` je hardcoded na 0 — chybí logika pro detekci zrušených záznamů. ViewModel (`ProjectDashboardKpiViewModel`) nemá `ZrusenoPrevYear` — vždy `null`.

**Doplňky:** Subsystem breakdown má `VProdleni = 0` a `Zruseno = 0` (komentář „simplified for now", řádky 261-262).

#### 10.2.3 STŘEDNÍ: Chybí PDF/tisk export

Specifikace ([projektovy-dashboard-design.md § Záložka 3](docs/superpowers/specs/2026-04-16-projektovy-dashboard-design.md)) zmíňuje potřebu exportu. Aktuálně neexistuje žádný export endpoint ani tlačítko pro Statické informace.

### 10.3 Návrh řešení

#### A. Fix year range — oddělit availableYears od data query

Rozdělit na dva dotazy:

1. **Dotaz na dostupné roky** — nezávislý na vybraném roce:
```csharp
var allYears = await _dbContext.ProjektoveZaznamy.AsNoTracking()
    .Where(r => r.ProjektId == projectId && taskCategoryIds.Contains(r.KategorieId))
    .Select(r => r.DatumUkonceni.Year)
    .Distinct()
    .OrderByDescending(y => y)
    .ToListAsync(ct);

// Vždy zahrnout aktuální rok
if (!allYears.Contains(DateTime.Now.Year))
    allYears.Insert(0, DateTime.Now.Year);
```

2. **Datový dotaz** — pevný range pro vybraný rok ± 1 (pro YoY compare):
```csharp
var rangeStart = new DateTime(year - 1, 1, 1);
var rangeEnd = new DateTime(year + 1, 12, 31);  // FIX: celý rok+1 včetně 31.12.
```

Alternativně ještě jednodušeji: `new DateTime(year + 2, 1, 1).AddDays(-1)` vždy dá 31.12.

#### B. Implementace Zruseno logiky

Potřebuje definici „zrušeného" záznamu. Buď:
- **Stav úkolu** „Zrušeno" v `CiselnikStavuUkolu` (pokud IsFinal + Kod == "ZRUSENO" nebo podobně)
- **Nebo** nový příznak `IsZruseno` na `CiselnikStavuUkolu`

```csharp
var cancelledStateIds = await _dbContext.CiselnikStavuUkolu.AsNoTracking()
    .Where(s => s.Kod == "ZRUSENO" /* nebo jiný kód */)
    .Select(s => s.Id)
    .ToListAsync(ct);
var zruseno = yearRecords.Count(r => r.StavUkoluId.HasValue && cancelledStateIds.Contains(r.StavUkoluId.Value));
```

#### C. PDF export — architektura

Pro koncepci „PDF k vytisknutí":

| Komponenta | Technologie | Popis |
|---|---|---|
| Endpoint | `GET /projekty/{id}/dashboard/statistics-export?year=2025` | Nový controller action |
| Generátor | QuestPDF (MIT, .NET native) nebo server-side HTML→PDF (Playwright) | Preferuji QuestPDF pro offline |
| Obsah | Stejná data jako `BuildStatisticsPanelAsync()` | Reuse service metod |
| Layout | A4 portrait, hlavička s názvem projektu, KPI karty, tabulky | Fixní layout pro tisk |
| Trigger | Tlačítko „Stáhnout PDF" v panelu | `<pm-button variant="Ghost" href="...export?year=...">` |

#### D. Kompletní redesign statistik (dle zadání)

Požadavek „od znovu vymyslet všechny statistiky" vyžaduje nový spec. Základní kostru navrhuji takto:

**Navržené statistiky pro roční report:**

1. **Plnění úkolů** (stávající KPI — zachovat s opravami)
   - Splněno / Zrušeno / Přeneseno / Včasnost
   
2. **Harmonogram — prodlení** (stávající s doplněním)
   - V prodlení / Průměrné prodlení / Prodlouženo
   - **Nové:** Histogram prodlení (kolik záznamů 1-7d, 8-14d, 15-30d, 30+d)

3. **ServiceDesk metriky** (nové — vyžadují SD integraci)
   - NES: otevřeno / uzavřeno / průměrná doba řešení
   - PMP/PNF: schváleno / zamítnuto / průměrná doba akceptace
   - Čerpání rozpočtu (bar graf)

4. **Čtvrtletní přehled** (stávající — zachovat)
   
5. **Subsystémový breakdown** (stávající — doplnit o VProdleni + Zruseno)

6. **Účast na jednáních** (stávající — zachovat)

7. **Nové: Trendy** (line chart za posledních 6–12 měsíců)
   - Počet otevřených/uzavřených záznamů per měsíc
   - Kumulativní prodlení

---

## Bod 11 — Výzvy

### 11.1 Aktuální stav

**Controller:** [VyzvyController.cs](PmTracker.Web/Controllers/VyzvyController.cs) — 5 endpointů:
- `POST /vyzvy/zalozit` — založí výzvu z bufferu
- `POST /vyzvy/zmenit-stav` — změní stav výzvy
- `POST /vyzvy/set-zaradid` — toggle switch „Zařadit do další výzvy" na PNF
- `POST /vyzvy/prerdit` — přesun PNF mezi výzvami/bufferem (drag & drop)
- `GET /vyzvy/reassign-modal` — načte reassign modal HTML

**Service:** `VyzvaService` (partial class):
- [VyzvaService.Founding.cs](PmTracker.Web/Services/Vyzvy/VyzvaService.Founding.cs) — zakládání výzvy
- [VyzvaService.Assignment.cs](PmTracker.Web/Services/Vyzvy/VyzvaService.Assignment.cs) — přiřazování PNF
- [VyzvaService.Transitions.cs](PmTracker.Web/Services/Vyzvy/VyzvaService.Transitions.cs) — stavový automat
- [VyzvaService.Queries.cs](PmTracker.Web/Services/Vyzvy/VyzvaService.Queries.cs) — čtení dat

**Panel Builder:** [VyzvyPanelBuilder.cs](PmTracker.Web/Services/ProjectDashboard/VyzvyPanelBuilder.cs) — stavba dashboard panelu

**Views:**
- [_VyzvyPanel.cshtml](PmTracker.Web/Views/ProjectDashboard/_VyzvyPanel.cshtml) — hlavní wrapper
- [_VyzvyPanel.BufferCard.cshtml](PmTracker.Web/Views/ProjectDashboard/_VyzvyPanel.BufferCard.cshtml) — buffer karta
- [_VyzvyPanel.VyzvaCard.cshtml](PmTracker.Web/Views/ProjectDashboard/_VyzvyPanel.VyzvaCard.cshtml) — karta výzvy
- [ReassignModal.cshtml](PmTracker.Web/Views/Vyzvy/ReassignModal.cshtml) — drag & drop modal

**JS moduly:**
- [panelController.js](PmTracker.Web/wwwroot/js/modules/vyzvy/panelController.js) — akce panelu
- [switchController.js](PmTracker.Web/wwwroot/js/modules/vyzvy/switchController.js) — switch na externí vazbě
- [reassignModal.js](PmTracker.Web/wwwroot/js/modules/vyzvy/reassignModal.js) — drag & drop
- [index.js](PmTracker.Web/wwwroot/js/modules/vyzvy/index.js) — bootstrap

**CSS:** [components/vyzvy-panel.css](PmTracker.Web/wwwroot/css/components/vyzvy-panel.css) — kompletní styl

**DB schema:** [db_upgrade_1_1_8_vyzvy.sql](db_upgrade_1_1_8_vyzvy.sql)

#### Aktuální auto-číslování

[VyzvaCodeGenerator.cs](PmTracker.Web/Services/Vyzvy/VyzvaCodeGenerator.cs):
```csharp
Generuj(poradoveVRoce, rok) → "{poradoveVRoce}/{rok}"    // např. "1/2026"
DalsiPoradoveVRoce(existujici) → existujici.Max() + 1    // nebo 1 pokud prázdné
```

Scope unikátnosti: `(CisloRamcoveSmlouvySnapshot, Rok)` — čísla sdílí projekty se stejnou rámcovou smlouvou.

### 11.2 Požadované změny (dle zadání)

#### 11.2.1 Ruční číslování místo automatického

**Aktuální stav:**
- `VyzvaCodeGenerator.DalsiPoradoveVRoce()` počítá `MAX + 1` z existujících výzev se stejnou smlouvou a rokem
- Kód se automaticky generuje při `ZaloztVyzvuZBufferuAsync()`
- User nemá možnost vybrat číslo

**Požadovaný stav:**
- User zadává číslo ručně (po domluvě se SVA)
- Jediné ověření: duplicita čísla v rámci `CisloRamcoveSmlouvy`
- Číslo se zjišťuje externě u SVA nebo po domluvě

**Co je potřeba změnit:**

| Soubor | Změna | Detail |
|---|---|---|
| `VyzvaCodeGenerator.cs` | Smazat / ponechat jen `Generuj()` | `DalsiPoradoveVRoce()` odstranitq |
| `VyzvaService.Founding.cs:29-34` | Přijmout `poradoveVRoce` jako parametr | Místo auto-compute přijmout z UI |
| `IVyzvaService.cs` | Rozšířit `ZaloztVyzvuZBufferuAsync` o `poradoveVRoce` | Nový parametr |
| `VyzvyController.cs` | Přijmout `poradoveVRoce` z POST body | Nový request model |
| `_VyzvyPanel.BufferCard.cshtml` | Nahradit „Založit výzvu z bufferu" za „Nová výzva" → modal | Nové UI |
| Nový modal | Select neobsazených čísel + validace duplicity | Nové view |
| `VyzvaService.Founding.cs` | Přidat validaci duplicity | `Vyzvy.Any(v => v.CisloRamcoveSmlouvySnapshot == ... && v.Rok == ... && v.PoradoveVRoce == number)` |

**Validace duplicity — dotaz:**
```csharp
var isDuplicate = await _db.Vyzvy.AsNoTracking()
    .AnyAsync(v => v.CisloRamcoveSmlouvySnapshot == cisloSmlouvy 
                 && v.Rok == rok 
                 && v.PoradoveVRoce == zadanePoradove, ct);
```

Plus DB unique constraint `(CisloRamcoveSmlouvySnapshot, Rok, PoradoveVRoce)` jako bezpečnostní záchytná síť.

**Neobsazená čísla pro dropdown:**
```csharp
var obsazena = await _db.Vyzvy.AsNoTracking()
    .Where(v => v.CisloRamcoveSmlouvySnapshot == cisloSmlouvy && v.Rok == rok)
    .Select(v => v.PoradoveVRoce)
    .ToListAsync(ct);
var neobsazena = Enumerable.Range(1, 99)
    .Where(n => !obsazena.Contains(n))
    .Take(20)
    .ToList();
```

#### 11.2.2 Nový layout — 20/80 dvousloupcový

**Aktuální layout:** Vertikální stack (buffer card → výzva cards pod sebou)

**Požadovaný layout:**
```
┌──────────────┬──────────────────────────────────────────────────────┐
│ 20% LEVÝ     │ 80% PRAVÝ                                          │
│              │                                                     │
│ ┌──────────┐ │  Záznam #1 - Název záznamu                         │
│ │ Výzva    │ │    ├── PNF #123456 - Název PNF    [⋯ menu]         │
│ │ 1/2026   │ │    └── PNF #234567 - Název PNF    [⋯ menu]         │
│ │ Příprava │ │                                                     │
│ └──────────┘ │  Záznam #2 - Název záznamu                         │
│              │    ├── PNF #345678 - Název PNF    [⋯ menu]         │
│ ┌──────────┐ │    ├── PNF #456789 - Název PNF    [⋯ menu]         │
│ │ Výzva    │ │    └── PNF #567890 - Název PNF    [⋯ menu]         │
│ │ 2/2026   │ │                                                     │
│ │ Odesláno │ │  ...                                                │
│ └──────────┘ │                                                     │
│              │ [Nová výzva]                                        │
│ ┌──────────┐ │                                                     │
│ │ Buffer   │ │                                                     │
│ │ 3 PNF    │ │                                                     │
│ └──────────┘ │                                                     │
└──────────────┴──────────────────────────────────────────────────────┘
```

**Klíčové změny:**
1. `_VyzvyPanel.cshtml` → `display: grid; grid-template-columns: 1fr 4fr;`
2. Levý sloupek: dlaždice výzev (klikatelné, aktivní = zvýrazněná)
3. Pravý sloupek: záznamy s PNF, seskupené podle záznamu, pod nimi buffer
4. Každý PNF řádek má three-dot menu (`⋯`) s akcemi

#### 11.2.3 Three-dot menu pro PNF

**Akce v menu:**
- „Přesunout do bufferu" → `POST /vyzvy/prerdit` s `cilovaVyzvaId = null`
- „Přesunout do Výzvy N/YYYY" → `POST /vyzvy/prerdit` s konkrétní `cilovaVyzvaId`
- Menu zobrazuje jen neuzavřené/neodeslané výzvy jako cíle

**Implementace:**
```html
<div class="vyzvy-pnf-context-menu" data-vyzvy-context>
    <button type="button" class="vyzvy-pnf-dots" data-vyzvy-context-toggle>⋯</button>
    <ul class="vyzvy-pnf-context-list" hidden>
        <li><button data-vyzvy-action="presunout-buffer" data-externi-odkaz-id="@p.ExterniOdkazId">
            Přesunout do bufferu
        </button></li>
        @foreach (var v in Model.PripravaVyzvy.Where(v => v.Id != currentVyzvaId))
        {
            <li><button data-vyzvy-action="presunout-vyzva" 
                        data-externi-odkaz-id="@p.ExterniOdkazId"
                        data-cilova-vyzva-id="@v.Id">
                Přesunout do Výzvy @v.Kod
            </button></li>
        }
    </ul>
</div>
```

#### 11.2.4 Drag & drop podpora

Stávající drag & drop v `reassignModal.js` je funkční (native HTML5 DnD API). Rozšíření pro pravý sloupek:

- Draggable PNF řádky (`draggable="true"`)
- Drop targets: dlaždice výzev v levém sloupku
- Drop → volání `POST /vyzvy/prerdit` stejně jako v modal
- Vizuální feedback: `.drag-over` třída na cílovém sloupci

#### 11.2.5 Tlačítko „Nová výzva"

Nový modal pro založení výzvy:

```html
<dialog id="nova-vyzva-modal">
    <h2>Založit novou výzvu</h2>
    <form>
        <label>Číslo výzvy:
            <select name="poradoveVRoce">
                @foreach (var n in Model.NeobsazenaCisla)
                {
                    <option value="@n">@n/@Model.Rok</option>
                }
            </select>
        </label>
        <p class="muted">Číslo smlouvy: @Model.CisloRamcoveSmlouvy</p>
        <div class="modal-actions">
            <pm-button variant="Primary" type="submit">Založit</pm-button>
            <pm-button variant="Ghost" type="button" data-close-modal>Zrušit</pm-button>
        </div>
    </form>
</dialog>
```

### 11.3 Edge cases a bezpečnost

| Scénář | Aktuální řešení | Změna potřeba? |
|---|---|---|
| PNF ve více výzvách napříč projekty | Filtered unique index na `(cislo) WHERE vyzva_id IS NOT NULL` | NE — DB constraint platí |
| Smazání PNF z výzvy Odeslano | `VyzvaService.Assignment.cs:39-40` — kontrola stavu | NE — existující guard |
| Race condition při drag & drop | Žádná explicitní transakce | ANO — přidat `IsolationLevel.ReadCommitted` |
| Drag PNF do výzvy cizího projektu | `VyzvaService.Assignment.cs:86-98` — cross-project ACL check | NE — existující guard |
| Číslo smlouvy neexistuje na projektu | `VyzvaErrorCode.ProjectMissingCisloRamcoveSmlouvy` (error 3) | NE |
| Více Priprava výzev v projektu | Logika funguje, drag & drop modal ukazuje sloupce | NE |
| User zadá číslo > 99 | Aktuálně neřešeno | ANO — validace na int range (1-999) |
| User zadá číslo 0 nebo záporné | Aktuálně neřešeno | ANO — server-side validace `poradoveVRoce > 0` |

### 11.4 Kód k odstranění

| Soubor | Co smazat | Důvod |
|---|---|---|
| `VyzvaCodeGenerator.DalsiPoradoveVRoce()` | Celá metoda | Nahrazena ruční volbou |
| `VyzvaService.Founding.cs:29-34` | Auto-compute logika | Nahrazena parametrem z UI |
| `_VyzvyPanel.BufferCard.cshtml:46-51` | Tlačítko „Založit výzvu z bufferu" | Nahrazeno „Nová výzva" s modalem |
| `VyzvyPanelBuilder.cs` | Logika `MuzeZaloztVyzvu` / `DuvodBlokace` | Buffer neblokuje vytvoření — ruční číslo |

### 11.5 Dopad PNF ve více výzvách

Zadání specifikuje: „projektový záznam může mít více PNF a každé PNF bude moci být v jiné výzvě, může se jeden projektový záznam objevit ve více výzvách, ale PNF může být vždy pouze v jedné výzvě."

**Aktuální datový model to podporuje:**
- `ZaznamExterniOdkazEntity.VyzvaId` je nullable FK — 1 PNF → max 1 výzva
- Filtered unique index `(Cislo) WHERE VyzvaId IS NOT NULL` brání duplicitě
- 1 `ProjektovyZaznam` může mít N `ZaznamExterniOdkaz` řádků (každý pro jiný ticket)
- Každý řádek může být v jiné výzvě → záznam se zobrazí pod více výzvami

**Pravý sloupek musí tedy zohlednit:** Záznam #1 se může objevit pod Výzvou 1/2026 (s PNF #123456) i pod Výzvou 2/2026 (s PNF #234567).

---

## Bod 12 — Vyhledávání

### 12.1 Aktuální stav

**Controller:** [SearchController.cs](PmTracker.Web/Controllers/SearchController.cs) — 4 endpointy:
- `GET /Search/Index` — stránka výsledků (fulltext)
- `GET /Search/Suggest` — dropdown suggest (LIKE)
- `POST /Search/Reindex` — manuální reindex (admin)
- `GET /Search/Status` — stav FT indexu (admin)

**Dvě vrstvy vyhledávání:**

| Vrstva | Service | Zdroj | Vždy dostupná? |
|---|---|---|---|
| **DB Suggest** | `DbSuggestService` | EF Core `LIKE %q%` | ANO |
| **FullText** | `GlobalSearchService` | `SqlServerSearchClient` nebo `OpenSearchClient` | Jen pokud `Search:Enabled = true` |

**Konfigurace:** [SearchOptions.cs](PmTracker.Web/Services/Search/SearchOptions.cs) — `PmTracker:Search` sekce:
- `Enabled` (bool, default false)
- `Provider` ("SqlServer" nebo "OpenSearch")

#### Co se aktuálně prohledává

**DB Suggest (LIKE):**

| Entita | Prohledávané sloupce |
|---|---|
| `ProjektoveZaznamy` | `Nazev`, `Cil`, `Popis` |
| `Vyjadreni` (přes JOIN Jednani) | `TextVyjadreni` |

Limit: 10 celkem (split 50/50).

**FullText (Index):**

| Entita | Mapovací typ | Indexované sloupce | URL cíle |
|---|---|---|---|
| Projekty | `projekt` | `CelyNazev` (title), `Zkratka` (keywords) | `/Projekty/Detail/{id}` |
| Záznamy | `zaznam` | `Nazev` (title), `Cil`, `Popis` (body) | `/Projekty/Detail/{projektId}?recordId={id}` |
| Jednání | `jednani` | `Jednání #{CisloJednani}` (title), `Misto` (body) | `/Jednani/Detail/{id}` |
| Osoby | `osoba` | Jméno (title), `AdLogin`, `Email` (keywords) | `/Osoby/Index` (bez detailu) |
| Subsystémy | `subsystem` | `Nazev` (title), `Kod` (keywords) | `/Ciselniky/Detail/subsystemy` |
| Vyjádření | `vyjadreni` | `TextVyjadreni` (body) | `/Projekty/Detail/{projektId}?recordId={zaznamId}` |
| Návrhy | `zaznam_navrh` | `PayloadJson` (body), `TypNavrhu`, `Stav` (keywords) | `/Navrhy/ProposalDetail?...` |

Mapper: [EntityDocumentMapper.cs](PmTracker.Web/Services/Search/EntityDocumentMapper.cs)

#### ACL

- SuperAdmin: vidí vše
- Ostatní: `VisibleProjectIds` WHERE filtr v SQL (pre-filter) + `SearchAcl.IsAccessible()` post-filter
- Osoby a Subsystémy: vždy přístupné (nejsou project-scoped)

### 12.2 Identifikované problémy

#### 12.2.1 KRITICKÉ: Fulltext search nemusí být nainstalovaný

**Konfigurace:** `Search:Enabled` default = `false`. Pokud je `false`:
- `GET /Search/Index` vrátí view s hláškou „Globální vyhledávání je momentálně vypnuté"
- `GET /Search/Suggest` funguje vždy (LIKE based, nezávisí na `Enabled`)

**Pro SqlServer provider:**
- `SearchReindexHostedService` ([SearchReindexHostedService.cs:63-93](PmTracker.Web/Services/Search/SearchReindexHostedService.cs#L63-L93)) při startu:
  1. Vytvoří tabulku `SearchIndex` (pokud neexistuje)
  2. Vytvoří `FULLTEXT CATALOG SearchCatalog` (pokud neexistuje)
  3. Vytvoří `FULLTEXT INDEX ON SearchIndex(Title, Body, Keywords)` (pokud neexistuje)
- Pokud SQL Server **nemá nainstalovaný Full-Text Search feature**, `CREATE FULLTEXT CATALOG` selže
- Aplikace loguje chybu ale neskončí — suggest funguje dál

**Jak ověřit instalaci FTS na serveru:**
```sql
SELECT SERVERPROPERTY('IsFullTextInstalled');    -- 1 = nainstalováno
SELECT name FROM sys.fulltext_catalogs;          -- seznam katalogů
```

#### 12.2.2 STŘEDNÍ: Výsledky nejsou rozděleny do skupin v suggest dropdownu

Dropdown suggest (AJAX) vrací flat list s typem `"zaznam"` nebo `"vyjadreni"`. Uživatel vidí smíšený seznam bez vizuálního oddělení.

Fulltext search (stránka `/Search/Index`) **skupiny podporuje** — [Index.cshtml:63-80](PmTracker.Web/Views/Search/Index.cshtml#L63-L80) iteruje přes `Model.Result.Groups` (Dictionary<string, IReadOnlyList<SearchHit>>).

#### 12.2.3 STŘEDNÍ: Chybějící entity v Suggest

DB Suggest prohledává jen 2 entity (záznamy + vyjádření). Fulltext prohledává 7. Požadavek je rozdělit výsledky do skupin: **záznamy, osoby, role, harmonogram, vyjádření, jednání, návrhy**.

Aktuálně chybí v suggest:
- **Osoby** — žádný LIKE dotaz na `Osoby` tabulku
- **Role** — žádná tabulka pro prohledávání rolí (role jsou v `AuthzRoleEntity` — kódy, ne texty)
- **Harmonogram** — žádný dotaz na harmonogram kroky/šablony
- **Jednání** — žádný LIKE dotaz na `Jednani` tabulku
- **Návrhy** — žádný LIKE dotaz na `ZaznamNavrhy` tabulku

### 12.3 Návrh řešení

#### A. Ověření a aktivace Fulltext Search

**Krok 1 — Zjistit stav na produkčním serveru:**
```sql
SELECT SERVERPROPERTY('IsFullTextInstalled') AS FtsInstalled;
```

**Krok 2 — Pokud neinstalované:**
- Na SQL Serveru (on-prem): Server Features → Full-Text and Semantic Extractions for Search
- Po instalaci: restart SQL service
- Ověření: `SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'SearchCatalog'`

**Krok 3 — Aktivace v appsettings:**
```json
"PmTracker:Search": {
    "Enabled": true,
    "Provider": "SqlServer"
}
```

**Krok 4 — Inicální index:**
Aplikace při startu spustí `SearchReindexHostedService`, který vytvoří katalog a provede plný reindex.

#### B. Rozšíření Suggest o další entity

Rozšířit `DbSuggestService.SuggestAsync()` o nové dotazy. Navrhuji limit 15 celkem (3 per skupinu):

```csharp
public async Task<IReadOnlyList<SuggestHit>> SuggestAsync(string query, ...)
{
    var perGroup = Math.Max(1, limit / 5);
    
    var zaznamy   = await QueryZaznamy(likePattern, visibleIds, perGroup, ct);
    var vyjadreni = await QueryVyjadreni(likePattern, visibleIds, perGroup, ct);
    var osoby     = await QueryOsoby(likePattern, perGroup, ct);       // NOVÉ
    var jednani   = await QueryJednani(likePattern, visibleIds, perGroup, ct); // NOVÉ
    var navrhy    = await QueryNavrhy(likePattern, visibleIds, perGroup, ct);  // NOVÉ
    
    // Merge a vrátit
}
```

**QueryOsoby:**
```csharp
private async Task<List<SuggestHit>> QueryOsoby(string likePattern, int count, CancellationToken ct)
{
    var rows = await _db.Osoby.AsNoTracking()
        .Where(o => EF.Functions.Like(o.Jmeno + " " + o.Prijmeni, likePattern)
                  || (o.Email != null && EF.Functions.Like(o.Email, likePattern)))
        .Take(count)
        .Select(o => new { o.Id, o.Jmeno, o.Prijmeni, o.Email })
        .ToListAsync(ct);
    
    return rows.Select(r => new SuggestHit
    {
        Type = "osoba",
        Title = $"{r.Jmeno} {r.Prijmeni}",
        Snippet = r.Email ?? "",
        Url = "/Osoby/Index"
    }).ToList();
}
```

**QueryJednani:**
```csharp
private async Task<List<SuggestHit>> QueryJednani(string likePattern, IReadOnlyList<int>? visibleIds, int count, CancellationToken ct)
{
    var q = _db.Jednani.AsNoTracking()
        .Join(_db.Projekty.AsNoTracking(), j => j.ProjektId, p => p.Id, (j, p) => new { Jednani = j, Projekt = p })
        .Where(x => EF.Functions.Like(x.Jednani.Misto, likePattern)
                  || EF.Functions.Like(x.Jednani.CisloJednani.ToString(), likePattern));
    
    if (visibleIds != null)
        q = q.Where(x => visibleIds.Contains(x.Jednani.ProjektId));
    
    var rows = await q.OrderByDescending(x => x.Jednani.Id).Take(count)
        .Select(x => new { x.Jednani.Id, x.Jednani.CisloJednani, x.Jednani.Misto, ProjektNazev = x.Projekt.CelyNazev })
        .ToListAsync(ct);
    
    return rows.Select(r => new SuggestHit
    {
        Type = "jednani",
        Title = $"Jednání #{r.CisloJednani}",
        Snippet = r.Misto ?? "",
        Url = $"/Jednani/Detail/{r.Id}",
        ProjektNazev = r.ProjektNazev
    }).ToList();
}
```

**Role:** `AuthzRoleEntity` obsahuje jen kódy (`APP_ADMIN`, `PROJ_MAN`) a názvy. Prohledávání rolí pravděpodobně nemá smysl — uživatel hledá osoby, ne role. Doporučuji **vynechat** role z vyhledávání a místo toho přidat filtr osob dle role (jako sekundární feature).

**Harmonogram:** Harmonogram kroky (`ZaznamHarmonogramKroky`) nemají textová pole — jen datumy. Prohledávání harmonogramu by dávalo smysl jen pokud uživatel hledá záznam přes harmonogramový stav. Doporučuji **vynechat** z LIKE suggest a řešit přes filtr na fulltext stránce.

#### C. Skupinové zobrazení výsledků

**Suggest dropdown — seskupení v JS:**

```javascript
function renderGroupedHits(hits) {
    const groups = {};
    for (const hit of hits) {
        (groups[hit.type] ??= []).push(hit);
    }
    
    const typeLabels = {
        zaznam: "Záznamy",
        vyjadreni: "Vyjádření", 
        osoba: "Osoby",
        jednani: "Jednání",
        navrh: "Návrhy"
    };
    
    const ul = document.createElement("ul");
    ul.className = "global-search-list";
    
    for (const [type, items] of Object.entries(groups)) {
        const header = document.createElement("li");
        header.className = "global-search-group-header";
        header.textContent = typeLabels[type] ?? type;
        ul.appendChild(header);
        
        for (const hit of items) {
            ul.appendChild(renderHitItem(hit));
        }
    }
    return ul;
}
```

**Fulltext stránka** — již funguje (grouping přes `Model.Result.Groups`).

### 12.4 Edge cases

| Scénář | Aktuální chování | Doporučení |
|---|---|---|
| FTS není nainstalovaný na serveru | `SearchReindexHostedService` loguje error, app běží | Přidat admin banner na `/Search/Index` |
| `Search:Enabled = false` | Stránka zobrazí „vypnuté", suggest funguje | OK — suggest je nezávislý |
| Query kratší než 2 znaky | Vrátí prázdný result | OK |
| Query > 200 znaků | Truncation na 200 | OK |
| SQL injection přes LIKE pattern | `EscapeLikePattern()` escapuje `%`, `_`, `[` | OK — bezpečné |
| Uživatel bez přístupu k projektům | `VisibleProjectIds` filtr v WHERE | OK |
| Concurrent reindex + search | `SearchReindexHostedService` používá lock | OK |
| Hledání českých diakritik | SQL Server collation řeší | Ověřit collation na produkci |
| Osoby bez detail stránky | URL je `/Osoby/Index` (žádný detail) | Přijatelné pro suggest; zvážit detail stránku |

### 12.5 Požadavky na produkční prostředí

| Požadavek | Stav | Akce |
|---|---|---|
| SQL Server Full-Text Search feature | **NEZNÁMÝ** | Ověřit `SERVERPROPERTY('IsFullTextInstalled')` |
| `PmTracker:Search:Enabled` v appsettings | Pravděpodobně `false` | Nastavit na `true` po instalaci FTS |
| `SearchIndex` tabulka | Auto-vytvořena při startu | Žádná akce |
| `search_reindex_checkpoint` tabulka | Vytvořena migrací `db_upgrade_1_1_5` | Ověřit, že migrace proběhla |
| Collation pro diakritiku | Závisí na DB | Ověřit `Czech_CI_AS` nebo kompatibilní |

---

## Souhrnná tabulka všech nalezených issues

| # | Bod | Závažnost | Popis | Soubor | Řádky |
|---|---|---|---|---|---|
| 1 | 8 | KRITICKÉ | 7 CSS tříd bez definice — rozbalený detail nemá styl | site.css | chybí |
| 2 | 8 | STŘEDNÍ | `.text-danger` neexistuje — červená barva nefunguje | site.css | chybí |
| 3 | 8 | NÍZKÁ | Mrtvý CSS kód (`.dashboard-step-detail`, `.dashboard-category-filter*`) | site.css | 7077-7117 |
| 4 | 9 | KRITICKÉ | Route param `"id"` místo `"projektId"` — export nefunkční | _NesPanel.cshtml | 28, 34 |
| 5 | 9 | KRITICKÉ | Žádné CSS pro `pm-nes-panel*` | site.css | chybí |
| 6 | 9 | STŘEDNÍ | Prázdný seznam bez diagnostického info | _NesPanel.cshtml | 43-45 |
| 7 | 10 | KRITICKÉ | Year range bug → zacyklení selectoru, rok 2027 | ProjectDashboardService.cs | 199-200 |
| 8 | 10 | STŘEDNÍ | `Zruseno` KPI hardcoded na 0 | ProjectDashboardService.cs | 426 |
| 9 | 10 | STŘEDNÍ | Subsystem breakdown `VProdleni`/`Zruseno` = 0 | ProjectDashboardService.cs | 261-262 |
| 10 | 10 | STŘEDNÍ | Chybí PDF/tisk export | — | — |
| 11 | 11 | CELKOVÝ REDESIGN | Auto-číslování → ruční výběr čísla | VyzvaCodeGenerator.cs | celý |
| 12 | 11 | CELKOVÝ REDESIGN | Layout: stack → 20/80 dvousloupcový | _VyzvyPanel.cshtml | celý |
| 13 | 11 | NOVÝ FEATURE | Three-dot menu + drag & drop na PNF řádcích | — | — |
| 14 | 11 | NOVÝ FEATURE | Tlačítko „Nová výzva" s modal | — | — |
| 15 | 12 | STŘEDNÍ | FTS nemusí být nainstalovaný | — | — |
| 16 | 12 | STŘEDNÍ | Suggest jen 2 entity (záznamy + vyjádření) | DbSuggestService.cs | celý |
| 17 | 12 | NOVÝ FEATURE | Seskupení výsledků v suggest dropdownu | global-search.js | renderHits |

---

---

## Hloubková analýza rizik při implementaci oprav

Tato sekce identifikuje konkrétní rizika, co se může při opravách rozbít, skryté závislosti a edge cases na úrovni kódu.

### R1 — CSS: Kaskáda, dark mode, z-index kolize

#### R1.1 Load order komponentových CSS

`site.css` na řádcích 1-6 importuje:
```css
@import "components/vyzvy-panel.css";
@import "components/externi-odkaz-card.css";
@import "components/chat-modal.css";
@import "components/sdconnector-page.css";
@import "components/sdconnector-inspect.css";
@import "components/schedule-actual-cell-feature-c.css";
```

**Důsledek:** Komponenty se načítají PRVNÍ, main `site.css` pravidla přijdou PO nich. Jakékoliv nové dashboard CSS v `site.css` přepíše komponentové styly se stejnou specifitou.

**Riziko při opravě:** Pokud přidáme `.dashboard-records-table td { padding: 0.5rem }` do site.css a existující komponentní CSS (např. `externi-odkaz-card.css`) cílí na `td` uvnitř `.external-row`, nekonfliktuje to díky odlišnému selektoru. Ale obecné tabulkové selektory jako `.table td` (site.css:1663-1677, padding 10px) mohou ovlivnit dashboard tabulky, pokud na nich bude třída `.table`.

**Akce:** Nové dashboard CSS NESMÍ používat třídu `.table` — použít specifické selektory `.dashboard-records-table`, `.dashboard-steps-table`.

#### R1.2 Hardcoded barvy bez dark mode overrides

| Selektor | Soubor:řádek | Hodnota | Dark mode override? |
|---|---|---|---|
| `.dashboard-expandable-row:hover td` | site.css:7073-7075 | `rgba(0, 43, 73, 0.04)` | **NE** |
| `.kpi-trend-up` | site.css:7058 | `#16a34a` | **NE** |
| `.kpi-trend-down` | site.css:7061 | `#dc2626` | **NE** |
| `.vyzvy-card-vyzva[data-stav="Zruseno"]` | vyzvy-panel.css | `opacity: 0.75` | **NE** (nečitelné v dark) |

**Riziko:** Pokud přidáme CSS pro NES panel s barvami přes `--pm-*` tokeny, ale hover/trend zůstane hardcoded, bude vizuální nekonzistence v dark mode.

**Akce:** Při přidání CSS pro záznamy/NES panel přidat i dark mode overrides:
```css
:root[data-theme="dark"] .dashboard-expandable-row:hover td {
    background: rgba(255, 255, 255, 0.06);
}
```

#### R1.3 Z-index hierarchie pro dropdown/context menu

Při přidání three-dot menu na výzvy (bod 11) je kritická z-index hierarchie:

| Element | z-index | Poznámka |
|---|---|---|
| `.vyzvy-stav-menu-list` | 20 | Existující dropdown |
| `.user-menu-panel` | 20 | Uživatelské menu — **kolize!** |
| `.app-header` | 40 | Sticky header |
| `.app-search-dropdown` | 100 | Search dropdown |
| `.schedule-actual-cell__dropdown-list` | 1000 | Harmonogram cell dropdown |
| `.pm-modal` (reassign) | 2600 | Modal overlay |

**Riziko:** Three-dot menu na PNF řádcích bude potřebovat z-index. Pokud bude ≤ 20, překryje ho user menu. Pokud bude > 2600, bude nad modaly.

**Doporučení:** z-index: 30 (nad stav menu, pod header) + `position: absolute` na relativním rodiči.

#### R1.4 Container queries a responsive breakpoint

Dashboard používá `@container dashpanel (max-width: 520px)` (site.css:772-800) s container-type: inline-size na `.dashboard-panel-body`. Pod 1024px se layout přepne na stack (site.css:7310-7348).

**Riziko při výzvy redesignu:** Dvousloupcový 20/80 layout ve výzvách se na < 1024px rozpadne. Při přidání grid `grid-template-columns: 1fr 4fr` musí být responsive fallback:
```css
@media (max-width: 768px) {
    .vyzvy-panel { grid-template-columns: 1fr; }
}
```

### R2 — JS: Event chain, memory leaks, race conditions

#### R2.1 pm:panel-loaded event chain — KRITICKÁ ZÁVISLOST

```
dashboard.js:48 → content.dispatchEvent(new CustomEvent("pm:panel-loaded"))
  ↓ bubbles: true
vyzvy/index.js:26 → document.addEventListener("pm:panel-loaded")
  ↓ re-scan
vyzvy/panelController.js:bindPanel(panelElement)
  ↓ attach click listener
```

**Pokud se tato chain přeruší** (např. odebráním dispatche v dashboard.js), výzvy panel v project dashboardu přestane reagovat na klikání po lazy-loadu. Panel se zobrazí (HTML je v DOM), ale žádné tlačítko nebude fungovat.

**Test pro regresi:** Ověřit, že po přepnutí na záložku „Výzvy" funguje „Založit výzvu z bufferu".

#### R2.2 Race condition při reloadPanel

`panelController.js:22-35`:
```javascript
async function reloadPanel(panelElement) {
    const resp = await fetch(url);
    const html = await resp.text();
    panelElement.parentNode.replaceChild(newEl, panelElement);
    global.pmVyzvy.bootstrap(newEl);
}
```

**Problém:** Žádný AbortController. Pokud user klikne „Založit výzvu" 2× rychle za sebou, dva POST requesty se odešlou paralelně, oba dostanou success, a dva reloadPanel se zavolají. Druhý replaceChild selže — `panelElement.parentNode` je null (element už byl nahrazen prvním voláním).

**Dopad:** `TypeError: Cannot read properties of null (reading 'replaceChild')`. Panel se nenačte. Workaround: F5.

**Fix:** Přidat guard:
```javascript
if (!panelElement.parentNode) return; // Already replaced
```

#### R2.3 CSRF token refresh po session timeout

`panelController.js:4-7` čte CSRF token z DOM (`input[name="__RequestVerificationToken"]`). Token je v `_Layout.cshtml` a platí po dobu session. Pokud session expiruje (např. IIS recycle), POST requesty vrátí 400 (antiforgery failure).

**Dopad:** Vše ve výzvách přestane fungovat. showToast zobrazí chybu, ale user netuší proč.

**Akce:** Zvážit globální error handler pro 400 s `antiforgery` v těle → redirect na login.

#### R2.4 Memory leak při opakovaném reloadPanel

`panelController.js:94` přidá `addEventListener('click', ...)` na panelElement. Po `replaceChild` starý element je odstraněn z DOM, ale listener zůstane navázaný na starý element. GC by měl uvolnit (žádné external reference), ale:

**Riziko:** Pokud existuje closura drží reference na `panelElement` v outer scope (např. `reloadPanel(panelElement)` closure), starý element nebude GC-ován.

**Aktuální kód:** `reloadPanel` je volaný s `panelElement` argumentem z click handleru (closure přes `bindPanel`). Po `replaceChild` closure stále drží referenci na starý element → potenciální leak.

**Severity:** Nízká (při běžném používání max 5-10 reloadů za session).

#### R2.5 gov-change vs change event routing

`bootstrap.js:717` registruje `gov-change` handler, který volá `handleDocumentChange(target)`. Ten projde chain včetně `handleProjectDashboardChange()` (year selector). Ale year selector je `<select>` element — ten emituje nativní `change`, NE `gov-change`.

**Ověření:** `bootstrap.js:716` TAKÉ registruje `change` handler → year selector funguje přes nativní `change`. Gov-form-switch funguje přes `gov-change`. Oba vedou do `handleDocumentChange()`.

**Riziko:** Pokud by se přidal gov-form-select místo nativního `<select>` pro year picker, musí emitovat `gov-change` aby handler zachytil.

### R3 — Databázové constraints a migrace

#### R3.1 Filtered unique index na externi_odkazy

`db_upgrade_1_1_8_vyzvy.sql` definuje:
```sql
CREATE UNIQUE NONCLUSTERED INDEX UX_externi_vyzva_cislo 
ON zaznam_externi_odkazy(cislo) WHERE vyzva_id IS NOT NULL;
```

**Tento index brání duplicitě PNF ve výzvách.** Pokud by se měnilo chování přiřazování PNF (bod 11), index zůstane jako ochrana.

**Riziko při redesignu:** Pokud nový three-dot menu umožní přesun PNF do výzvy kde už jiný záznam má stejné `cislo`, DB hodí duplicate key violation. Service (`VyzvaService.Assignment.cs`) tento edge case NEOVĚŘUJE explicitně — spoléhá na DB constraint.

**Akce:** Při redesignu přidat pre-check:
```csharp
var conflict = await _db.ZaznamExterniOdkazy.AnyAsync(ev => 
    ev.Cislo == odkaz.Cislo && ev.VyzvaId == cilovaVyzvaId, ct);
if (conflict) return Fail<Unit>(VyzvaErrorCode.PnfAlreadyInAnotherVyzva, "...");
```

#### R3.2 SaveChangesAsync — dvě volání v ZaloztVyzvuZBufferuAsync

`VyzvaService.Founding.cs:49+64`:
```csharp
_db.Vyzvy.Add(vyzva);
await _db.SaveChangesAsync(ct);       // 1. uloží výzvu, získá ID

foreach (var p in polozky) p.VyzvaId = vyzva.Id;
_db.VyzvaHistorieStavu.Add(historie);
await _db.SaveChangesAsync(ct);       // 2. uloží přiřazení + historii
```

**Problém:** Pokud 2. SaveChangesAsync selže (např. filtered unique index violation na PNF), výzva je vytvořena ale PNF nejsou přiřazeny. Výzva je prázdná.

**Akce:** Obalit do transakce:
```csharp
using var tx = await _db.Database.BeginTransactionAsync(ct);
// ... oba SaveChanges ...
await tx.CommitAsync(ct);
```

#### R3.3 CiselnikTypuExternichOdkazu — hard FirstAsync

`VyzvaQueries.cs:28-32`:
```csharp
public static Task<int> GetPnfTypIdAsync(PmTrackerDbContext db, CancellationToken ct)
    => db.CiselnikTypuExternichOdkazu
        .Where(t => t.Kod == PnfKod)
        .Select(t => t.Id)
        .FirstAsync(ct);   // ← EXCEPTION pokud PNF typ neexistuje v DB
```

**Riziko:** Pokud `CiselnikTypuExternichOdkazu` neobsahuje záznam s `Kod = "PNF"`, celý výzvy panel i switch spadne s `InvalidOperationException`.

**Produkční check:** Ověřit `SELECT * FROM ciselnik_typu_externich_odkazu WHERE kod = 'PNF'`.

#### R3.4 TypZaznamu CHAR(5) — trim/normalizace v NES query

Dle MEMORY `feedback_typzaznamu_char5_normalization.md`: HOT_ZAZNAMY.typ_zaznamu je `CHAR(5)` s padding. `SqlInformacniSystemQueryService` filtruje `z.TypZaznamu == "NES"` a `"PMP"` a `"PNF"`.

**EF Core + SQL Server:** `CHAR(5)` comparison s `NVARCHAR` literálem funguje díky implicit conversion v SQL Serveru (trailing spaces ignorovány v porovnání). Ale pokud by se data filtrovala client-side (InMemory provider), `"NES  " != "NES"`.

**Riziko při testování:** InMemory testy pro NES panel MUSÍ seed data s trimmovanými hodnotami (`"NES"`, ne `"NES  "`). Produkce funguje díky SQL collation.

### R4 — Test coverage a regresní rizika

#### R4.1 Existující testy pro dotčené oblasti

| Soubor | Pokrytí | Dopad při změně |
|---|---|---|
| `ProjectDashboardStatisticsYearFilterTests.cs` | **Testuje buggy range** (komentář řádek 25: `[2023-01-01 .. 2025-12-30]`) | Po opravě year range bude test ČERVENÝ — nutné aktualizovat expected boundary |
| `ProjectDashboardRecordCategorizationTests.cs` | 6 testů: Delayed/Awaiting/Approaching/OnTrack/Record/Sort | Při CSS opravě NE-dotčeno; při změně ScheduleDateCalculator/Categorizer DOTČENO |
| `ProjectDashboardControllerBehaviorTests.cs` | Existence akce, Forbid/NotFound scénáře | Při přidání endpoint (výzvy modal) potřeba nový test |
| `ProjectDashboardServiceNesPanelTests.cs` | 3 testy: no-SD, empty list, 3 tickets | Při opravě route param NE-dotčeno (route je v controlleru, ne service) |

#### R4.2 Test pro year range bug dokumentuje STÁVAJÍCÍ (buggy) chování

`ProjectDashboardStatisticsYearFilterTests.cs:25`:
```csharp
// 3 outside (2021, 2022, 2027) — outside the [2023-01-01 .. 2025-12-30] buffer
```

Test PROJDE se stávajícím kódem (assert Preneseno=2). **Po opravě** (range → `2025-12-31`) test stále projde, protože záznam z 2027 je mimo i opravený range. Ale komentář bude nepřesný.

**Větší problém:** Test NEVERIFIKUJE `AvailableYears` — jen KPI. Chybí test pro:
```csharp
result.AvailableYears.Should().Contain(2024);
result.AvailableYears.Should().Contain(2025); // aktuální rok
```

#### R4.3 Chybějící testy pro NES route parameter

Neexistuje test, který by ověřil, že export tlačítko v NES panelu se zobrazí. View testy (`_NesPanel.cshtml` rendering) by musely ověřit `ViewContext.RouteData.Values["projektId"]` — aktuální test infrastructure to nepokrývá.

#### R4.4 Chybějící testy pro výzvy auto-číslování

`VyzvaCodeGenerator` nemá vlastní unit test soubor. Logika `DalsiPoradoveVRoce` je triviální (Max+1), ale po přechodu na ruční čísla je potřeba test pro validaci duplicity.

### R5 — Authorization a security edge cases

#### R5.1 PermissionAuthorizationHandler fallback

Handler čte `projektId` z route → query → form (v tomto pořadí). ProjectDashboardController route je `projekty/{projektId:int}/dashboard` → handler najde `projektId` v route values.

**Riziko:** Nový endpoint pro výzvy modal (`GET /vyzvy/reassign-modal?projektId=...`) čte `projektId` z query string. Handler ho NAJDE (fallback na query). Ale pokud by se přejmenovalo na `projId`, handler ho NENAJDE → permission check selže → 403.

**Akce:** Všechny nové endpointy musí pojmenovat parametr `projektId` (ne `id`, `projId` atd.).

#### R5.2 VyzvaService.Founding nevaliduje oprávnění

`ZaloztVyzvuZBufferuAsync` přijímá `zalozilOsobaId` jako parametr — neověřuje, zda volající má oprávnění. Controller (`VyzvyController.cs`) to ověřuje přes `[Authorize(Policy)]` a explicitní `HasPermission` check.

**Riziko:** Pokud by se service volal z jiného místa (např. background job, test helper) bez controller-level ověření, vzniká security gap.

**Akce:** Přidat komentář `// Caller must verify vyzvy.create permission` nebo přidat service-level check (jako má `NastavitZaradidAsync`).

#### R5.3 Cross-project PNF reassign bezpečnost

`VyzvaService.Assignment.cs:86-98` ověřuje oprávnění v cílovém projektu při cross-project přesunu. **Ale:** Three-dot menu (bod 11) by defaultně nabízelo jen výzvy aktuálního projektu. Cross-project scénář by nastával jen přes reassign modal.

**Akce:** V three-dot menu zobrazit jen výzvy aktuálního projektu (ne všech projektů).

### R6 — DI registrace a service lifetime

#### R6.1 Chybějící registrace pro nové služby

Při redesignu výzev budou potřeba nové služby:

| Služba | Potřebuje registraci? | Kam? |
|---|---|---|
| `VyzvaNumberValidatorService` (validace duplicity čísla) | Pokud je oddělená třída | `VyzvyServiceCollectionExtensions.cs` |
| Three-dot menu ViewModel builder | Pokud je oddělená třída | `DataStoreServiceCollectionExtensions.cs` |
| PDF export service (statistiky) | ANO | `DataStoreServiceCollectionExtensions.cs` — vedle `NesPanelExcelExportService` |

#### R6.2 Ticketing:Enabled=false a NES panel

Když `Ticketing:Enabled=false`:
- `IInformacniSystemQueryService` → `DisabledInformacniSystemQueryService` (vrací prázdné výsledky)
- `ProjectDashboardService.BuildNesPanelAsync()` → `GetProdleneAsync()` vrátí `[]` → NES panel: `PocetVProdleni = 0`
- Ale `ServiceDeskInfoSystemId` je na projektu vyplněné → `IsServiceDeskIntegrated = true` → panel se vykreslí s prázdným seznamem

**Vizuální dopad:** NES panel řekne „Žádné tickety v prodlení" i když ServiceDesk je celý vypnutý. Technicky správné (žádný ticket NENÍ v prodlení protože se žádný nevrátil), ale matoucí.

**Akce:** Zvážit: pokud `Ticketing:Enabled=false`, vrátit `IsServiceDeskIntegrated = false` nezávisle na `ServiceDeskInfoSystemId`.

### R7 — Harmonogram edge cases v dashboard záznamech

#### R7.1 Krok bez PlanDatum

`ScheduleDateCalculator.cs:59`:
```csharp
var planEnd = (step.PlanDatum?.Date) ?? planStart;
```

Pokud krok nemá `PlanDatum` (null), planEnd = planStart = předchozí krok. Dashboard categorizer pak vidí krok kde `PlanEndDate == ActualEndDate` a `OffsetDays == 0` → AwaitingActual (pokud planEnd < today).

**Dopad:** Krok bez plánu se může objevit jako „Čeká na skutečnost" na dashboardu. To je falešný pozitiv.

#### R7.2 Aktuální krok projekce

`ProjectDashboardService.cs:133`:
```csharp
var maRealnouSkutecnost = c.MaSkutecnost && !c.JeAktualniKrok;
```

Guard filtruje aktuální krok (vizuální projekce do dneška) z categorializace. Pokud by se guard odstranil, aktuální krok by se vždy zobrazil jako „V prodlení" (protože jeho skutečnost = dnes > plan).

#### R7.3 Záznam s více problematickými kroky

Jeden záznam může mít kroky v různých kategoriích — např. krok 3 Delayed, krok 6 AwaitingActual. Dashboard zobrazí nejhorší kategorii (Delayed) jako hlavní badge, ale v rozbalení ukáže oba kroky.

**Stávající chování:** Správné. Ale CSS pro rozbalení chybí → vizuálně se neliší delayed krok od awaiting kroku v detailu.

---

### R8 — Výzvy redesign: transakční integrita a DI

#### R8.1 Dvě SaveChangesAsync v ZaloztVyzvuZBufferuAsync (atomicita)

`VyzvaService.Founding.cs:49+64` — výzva se uloží, PNF se přiřadí v DRUHÉM SaveChanges. Pokud 2. save selže (filtered unique index `ux_zaznam_externi_odkazy_cislo_in_vyzve` — duplicitní PNF číslo), výzva zůstane v DB bez PNF.

**Při redesignu na ruční čísla:** Pokud user zadá číslo, které mezitím jiný user zabral (race condition), unique index `ux_vyzvy_smlouva_rok_poradove` hodí exception na PRVNÍM SaveChanges. To je OK — výzva se nevytvoří. Ale pokud obě SaveChanges projdou a pak se zjistí chyba, rollback není možný.

**Fix:** `BeginTransactionAsync` + `CommitAsync` obalení celého flow.

#### R8.2 VyzvaService závisí na ITicketingQueryService

`VyzvaService.cs:19`: `private readonly ITicketingQueryService _ticketing;`

Pokud `Ticketing:Enabled=false`, service `DisabledTicketingQueryService` vrací prázdné výsledky. `LoadHotZaznamyAsync()` vrátí prázdný dictionary → názvy PNF v panelu budou `"(bez názvu z HOT)"`. 

**Dopad na výzvy redesign:** Three-dot menu a pravý sloupek zobrazí PNF řádky s `StrucneNazev = null` → fallback text. Není to error, ale UX zhoršení. Dokumentovat v UI.

#### R8.3 Nové služby potřebují registraci

| Služba | Registrovat v | Lifetime |
|---|---|---|
| Modal pro „Nová výzva" → nový endpoint v `VyzvyController` | Existující controller, žádná nová registrace | — |
| Validátor duplicity čísla | Statická metoda v `VyzvaService.Founding.cs` | — |
| Query „neobsazená čísla" | Nová metoda v `IVyzvaService` | Existující registrace |
| PDF export statistik | Nový `IStatisticsPdfExportService` | Scoped, `DataStoreServiceCollectionExtensions.cs` |

#### R8.4 FK cascade chain při smazání záznamu

`db_upgrade_1_3_12_record_delete_cascade.sql`:
- `projektove_zaznamy` → `zaznam_externi_odkazy` CASCADE
- `zaznam_externi_odkazy` → `zaznam_harmonogram_vyjadreni_vazba` NO ACTION

Pokud se smaže záznam, jeho externí odkazy se cascade-smaží. Ale `vyjadreni_vazby` na těch odkazech mají NO ACTION FK → **smazání selže** pokud existují vyjadreni_vazby.

**Dopad:** RecordService musí pre-delete vyjadreni_vazby před mazáním záznamu. Při výzvy redesignu neměnit cascade chain.

### R9 — Search: fulltext instalace a rozšíření suggest

#### R9.1 Rozšíření DbSuggestService — výkonnostní dopad

Stávající suggest dělá 2 dotazy (záznamy + vyjádření). Rozšíření o osoby, jednání, návrhy přidá 3 další dotazy = 5 SQL dotazů per keystroke.

**Mitigace:** Debounce 300ms v `global-search.js:182` + AbortController (předchozí request se zruší). Ale pokud SQL dotazy trvají > 300ms, uživatel vidí staré výsledky.

**Doporučení:** Paralelní `Task.WhenAll()` pro všech 5 dotazů místo sekvenčního:
```csharp
var tasks = new[]
{
    QueryZaznamy(likePattern, visibleIds, perGroup, ct),
    QueryVyjadreni(likePattern, visibleIds, perGroup, ct),
    QueryOsoby(likePattern, perGroup, ct),
    QueryJednani(likePattern, visibleIds, perGroup, ct),
    QueryNavrhy(likePattern, visibleIds, perGroup, ct),
};
await Task.WhenAll(tasks);
```

#### R9.2 Osoby nemají detail stránku

`SearchController.cs:124`: `EntityDocumentMapper.TypeOsoba => "/Osoby/Index"` — URL je stránka seznamu, ne detail konkrétní osoby. Suggest hit pro osobu vede na seznam všech osob.

**Při seskupení suggest výsledků:** Skupina „Osoby" bude mít klikatelné položky, ale URL nenaviguje na konkrétní osobu. UX matoucí.

**Akce:** Buď přidat Osoby detail stránku, nebo v suggest zobrazit osobu s textem „Hledat v seznamu osob" místo přímého odkazu.

#### R9.3 Role vyhledávání — problém definice

Uživatel žádá skupinu „role" ve výsledcích. `AuthzRoleEntity` obsahuje:
- `Kod`: "APP_ADMIN", "PROJ_MAN", "GEST" (interní kódy)
- `Nazev`: "Správce aplikace", "Projektový manažer", "Gestor FIS" (české názvy)

**Otázka:** Hledá uživatel roli podle názvu, nebo osoby s danou rolí? Pravděpodobně druhé — „najdi lidi s rolí Gestor" je use case, ne „najdi roli Gestor".

**Doporučení:** Neprohledávat `AuthzRoleEntity` jako samostatnou entitu. Místo toho rozšířit osoby suggest o role metadata.

### R10 — NES panel: route mismatch a diagnostika

#### R10.1 Export route hlubší analýza

`_NesPanel.cshtml:28`: `ViewContext.RouteData.Values.TryGetValue("id", ...)` hledá `"id"`, ale controller route je `{projektId}`.

**Ale pozor:** `asp-route-id="@projektIdFromRoute"` generuje URL s `?id=...` query parametrem. Export endpoint (`NesPanelExport`) přijímá `int projektId` z ROUTE (ne query). Takže i kdyby `projektIdFromRoute` nebyl null, vygenerovaná URL by byla špatná — `asp-route-id` vytvoří `?id=X` místo `/projekty/X/dashboard/nes-panel/export`.

**Správný fix:**
```razor
<a asp-controller="ProjectDashboard" 
   asp-action="NesPanelExport" 
   asp-route-projektId="@projektIdFromRoute">
```

#### R10.2 Stav „archiv" normalizace

`SqlInformacniSystemQueryService.cs:14`: `private const string ArchivStav = "archiv";`
Filtr: `z.Stav != ArchivStav`

HOT_ZAZNAMY.stav může mít hodnoty jako "archiv", "dodavatel", "otevřeno" atd. **Ale:** `stav` sloupec v ServiceDesk DB má typ `VARCHAR(20)` nebo `NVARCHAR(20)`. Pokud by byly trailing spaces (jako u `typ_zaznamu` CHAR(5)), `"archiv" != "archiv "` v client-side comparison.

**Aktuální kód:** Filtr jde přes SQL (`EF.Functions.Like` se nepoužívá, jen `==`). SQL Server VARCHAR comparison ignoruje trailing spaces → OK. InMemory testy seed s přesnou hodnotou → OK.

**Ale:** Pokud by se přidalo client-side filtrování, musí se trimovat.

---

### Matice dopadů změn

| Změna | Dotčené soubory | Riziko regrese | Testy k aktualizaci |
|---|---|---|---|
| CSS záznamy panel | `site.css` (nové pravidla) | Nízké — žádné CSS existuje | Přidat architekturu test pro CSS třídy |
| CSS NES panel | `site.css` nebo nový `components/nes-panel.css` | Nízké — žádné CSS existuje | Přidat architekturu test |
| Fix NES route param | `_NesPanel.cshtml:28,34` | Nízké — 2 řádky | Přidat API test pro export endpoint |
| Fix year range | `ProjectDashboardService.cs:199-200` | **Střední** — test komentář stale | `ProjectDashboardStatisticsYearFilterTests` komentář update + nový test pro `AvailableYears` |
| Fix availableYears | `ProjectDashboardService.cs:272-283` | **Střední** — oddělit od data query | Nový test |
| Smazat auto-číslování | `VyzvaCodeGenerator.cs`, `VyzvaService.Founding.cs:29-34` | **Vysoké** — celý create flow | Přidat testy pro ruční čísla + validaci duplicity |
| Dvousloupcový layout výzev | `_VyzvyPanel*.cshtml`, `vyzvy-panel.css` | **Vysoké** — JS `data-*` atributy | `panelController.js` musí být aktualizován |
| Three-dot menu | Nový partial, nový CSS, nový JS handler | **Střední** — nový kód, testovat interakce | Nové testy |
| Rozšíření suggest | `DbSuggestService.cs` | **Střední** — výkonnost | `SuggestEndpointTests` rozšířit |
| Seskupení suggest | `global-search.js` | Nízké — klient-side | E2E test `GlobalSearchDynamicResultsTests` |
| PDF export statistik | Nový service + endpoint | Nízké — nový kód | Nový test |

### R11 — Statistiky: sémantická nesprávnost KPI výpočtů

#### R11.1 KRITICKÉ: `DatumUkonceni` je PLÁNOVANÝ termín, ne datum splnění

`ProjektovyZaznamEntity.DatumUkonceni` je **plánovaný termín ukončení** záznamu, NE datum skutečného dokončení. V DB neexistuje sloupec pro „skutečný datum dokončení".

**Dopad na statistiky:**
- **`Splneno`** — počítá záznamy kde `StavUkoluId` je v `IsFinal=true` stavech. **Ale:** zahrnuje i `CANCEL` (Zrušeno) → `Splneno` = DONE + CANCEL, ne jen DONE.
- **`ActualCompletions` per čtvrtletí** — řadí se do čtvrtletí podle `DatumUkonceni` (plánovaný termín), ne podle skutečného data dokončení. Záznam dokončený v Q4 ale s termínem v Q2 se objeví v Q2.
- **`VcasnostPlneniPct`** (on-time rate) — porovnává `DatumUkonceni` (aktuální plán) s původním termínem z `ZaznamHistorieTerminu`. Pokud se termín prodloužil a pak splnil „v novém termínu", je to stále „pozdě" (správné pokud se měří proti původnímu plánu).

**Kódy stavů z DB seed:**

| Kód | Název | IsFinal | Význam |
|---|---|---|---|
| `NEW` | Nezahájeno | 0 | Nový úkol |
| `OPEN` | Rozpracováno | 0 | Práce probíhá |
| `WAIT` | Čeká se | 0 | Čeká na externího actora |
| `DONE` | Hotovo | 1 | Úspěšně dokončeno |
| `CANCEL` | Zrušeno | 1 | Zrušeno |

**Fix pro Splneno vs Zruseno:**
```csharp
// Místo:
var completedRecords = yearRecords
    .Where(r => r.StavUkoluId.HasValue && finalStates.ContainsKey(r.StavUkoluId.Value))
    .ToList();
var splneno = completedRecords.Count;

// Správně:
var doneRecords = yearRecords
    .Where(r => r.StavUkoluId.HasValue 
        && finalStates.TryGetValue(r.StavUkoluId.Value, out var kod) 
        && kod == "DONE")
    .ToList();
var cancelRecords = yearRecords
    .Where(r => r.StavUkoluId.HasValue 
        && finalStates.TryGetValue(r.StavUkoluId.Value, out var kod) 
        && kod == "CANCEL")
    .ToList();
var splneno = doneRecords.Count;
var zruseno = cancelRecords.Count;
```

**Poznámka:** `TaskStateCodes` ([RecordCatalogKeys.cs](PmTracker.Web/Models/ViewModels/RecordCatalogKeys.cs)) neobsahuje konstanty pro `DONE` a `CANCEL` — potřeba přidat.

#### R11.2 Subsystem breakdown VProdleni vyžaduje harmonogram výpočet

`VProdleni` na úrovni subsystému (řádek 261) by vyžadoval pro každý záznam načíst `ZaznamHarmonogramKroky`, spustit `ScheduleDateCalculator.Compute()` a `DashboardRecordCategorizer.CategorizeRecord()` — stejnou logiku jako `BuildRecordsPanelAsync()`.

**Výkonnostní dopad:** Pro 500 záznamů × 10 kroků = 5000 řádků harmonogramu navíc. Reálně přijatelné (single DB query + in-memory compute).

**Implementační doporučení:** Extrahovat sdílenou metodu `CategorizeAllProjectRecords(projectId, referenceDate)` volanou z obou `BuildRecordsPanelAsync` a `BuildStatisticsPanelAsync`.

#### R11.3 `Prodlouzeno` vs `VProdleni` sémantický rozdíl

- **Prodlouzeno** = počet záznamů kde se ZMĚNIL termín (existuje záznam v `ZaznamHistorieTerminu`)
- **VProdleni** = počet záznamů kde harmonogram krok má kladnou odchylku (skutečnost za plánem)

Aktuálně `VProdleni` v KPI (řádek 414) měří záznamy kde NoveDatum > PuvodniDatum v historii → to je ve skutečnosti **prodloužení termínu**, ne prodlení harmonogramu. Metrika by se měla přejmenovat nebo přepočítat.

### R12 — Search: coverage gaps a badge rendering

#### R12.1 Suggest dropdown renderuje jen 2 typy badgů

[global-search.js:83-89](PmTracker.Web/wwwroot/js/global-search.js#L83-L89):
```javascript
badge.textContent = h.type === 'vyjadreni' ? 'Vyjádření' : 'Záznam';
```

Všechny entity kromě `vyjadreni` dostanou text „Záznam". Po rozšíření suggest o osoby/jednání budou osoby zobrazeny s badgem „Záznam" — matoucí.

**CSS badge třídy:** Existují jen `--zaznam` a `--vyjadreni`. Pro nové typy (osoba, jednani, navrh) potřeba přidat CSS třídy.

#### R12.2 Neindexované entity relevantní pro požadavek

Uživatel žádá skupiny: **záznamy, osoby, role, harmonogram, vyjádření, jednání, návrhy**.

| Skupina | Indexovaná? | V suggest? | V fulltext? | Poznámka |
|---|---|---|---|---|
| Záznamy | ✅ | ✅ | ✅ | Nazev, Cil, Popis |
| Osoby | ✅ | ❌ | ✅ | Jen jméno+email |
| Role | ❌ | ❌ | ❌ | AuthzRoleEntity nemá textový obsah |
| Harmonogram | ❌ | ❌ | ❌ | ZaznamHarmonogramKrokEntity nemá text |
| Vyjádření | ✅ | ✅ | ✅ | TextVyjadreni |
| Jednání | ✅ | ❌ | ✅ | Jen Misto (omezené) |
| Návrhy | ✅ | ❌ | ✅ | PayloadJson (raw JSON) |

**Role** a **Harmonogram** nelze rozumně prohledávat — nemají textová pole. Doporučuji:
- **Role:** Nahradit filtrací osob dle role (rozšířit osoby suggest o metadata rolí)
- **Harmonogram:** Prohledávat přes záznamy (název záznamu + subsystém), ne přes kroky

#### R12.3 Audit log mapping chybí pro subsystémy

`SearchReindexHostedService.MapAuditTypeToSearchType` mapuje 6 audit typů na search typy. **Subsystémy chybí** — editace subsystému v číselníku nespustí reindex. Subsystém se reindexuje jen při full reindex (startup/manual).

#### R12.4 ZaznamNavrh indexuje raw PayloadJson

`EntityDocumentMapper.MapZaznamNavrh()` indexuje `PayloadJson` jako body text. To je serializovaný JSON — fulltext ho matchne na hodnoty uvnitř, ale ranking bude nízký (JSON klíče jako „Nazev" se matchnou).

**Doporučení:** Deserializovat JSON a indexovat jen relevantní pole (Nazev, Popis) jako body.

### R13 — Wiki dokumentace je zastaralá

#### R13.1 Výzvy wiki popisuje jiný model

[docs/wiki/projekty/projektovy-dashboard/vyzvy/vytvorit-vyzvu.md](docs/wiki/projekty/projektovy-dashboard/vyzvy/vytvorit-vyzvu.md) popisuje:
- Modal s poli: Název, Popis, Dodavatel, Termín dodání, Vázaný záznam, Cena, Stav
- Interní ID „VZ-001, VZ-002"
- Schvalovací workflow (Vytvořena → Ke schválení → Schválena → ...)
- Permissions: `vyzvy.approve`, `vyzvy.edit`, `vyzvy.delete`

**Aktuální implementace:**
- Auto-create z bufferu (žádný modal s poli)
- Kód formát „1/2026" (číslo/rok)
- 3 stavy: Priprava → Odeslano → Zruseno
- Permissions: `vyzvy.create`, `vyzvy.state.change`, `vyzvy.pnf.assign`, `vyzvy.pnf.reassign`

**Wiki je KOMPLETNĚ ZASTARALÁ** pro výzvy — po redesignu (bod 11) bude potřeba přepsat.

#### R13.2 Buffer cards wiki popisuje vizuální placeholdery

[docs/wiki/projekty/projektovy-dashboard/vyzvy/buffer-cards.md](docs/wiki/projekty/projektovy-dashboard/vyzvy/buffer-cards.md) popisuje „buffer card" jako vizuální placeholder s přerušovaným okrajem a ikonou „+". Aktuální implementace: buffer = seznam PNF čekajících na zařazení do výzvy.

#### R13.3 NES wiki zmiňuje chat modal na řádku

[docs/wiki/projekty/projektovy-dashboard/nes-v-prodleni/index.md](docs/wiki/projekty/projektovy-dashboard/nes-v-prodleni/index.md) řádek 110-114: „Klik na řádek ticketu otevře chat modal". Aktuální implementace: řádky tabulky NEJSOU klikatelné, jen ticket ID je odkaz do ServiceDesku.

### R14 — NES export: Excel service omezení

#### R14.1 Formátování bez stylů

`NesPanelExcelExportService.cs` komentář (řádek 12): „Minimum viable OpenXML Spreadsheet writer: hlavička + data rows, bez stylů."

Wiki ([export-excel.md](docs/wiki/projekty/projektovy-dashboard/nes-v-prodleni/export-excel.md)) očekává: bold hlavičku, date format, number format, hyperlinky. Aktuální implementace: vše jako `CellValues.String`, žádné formátování, URL jako plain text (ne klikací hyperlink).

#### R14.2 Typ ticketu — NES vs PMP+PNF

NES panel zobrazuje **všechny typy** v prodlení (NES, PMP, PNF), ne jen NES. Sloupec `Typ` zobrazuje `item.TypZaznamu`. Export zahrnuje všechny typy. Ale název panelu je „NES v prodlení" → uživatelsky matoucí pokud se zobrazují i PMP/PNF tickety.

### R15 — Dashboard záznamy: data flow edge cases

#### R15.1 ScheduleUrl naviguje na špatnou záložku

[ProjectDashboardService.cs:169](PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs#L169):
```csharp
ScheduleUrl = $"/Projekty/Detail/{projectId}?tab=harmonogram&recordId={record.Id}"
```

[ProjektyController.cs:84](PmTracker.Web/Controllers/ProjektyController.cs#L84):
```csharp
model.TargetRecordId = requestedTab == RecordsTab ? recordId : null;
```

**Problém:** URL má `tab=harmonogram`, ale `TargetRecordId` se nastaví jen pro `tab=zaznamy`. Klik na „Přejít na harmonogram" otevře správnou záložku, ale `recordId` parametr se ignoruje → záznam se nevybere.

**Fix:** Změnit logiku v ProjektyController aby `TargetRecordId` fungoval i pro `tab=harmonogram`, nebo změnit URL na záložku záznamy s deep-linkem do harmonogramu.

#### R15.2 INNER JOIN na Subsystemy skrývá záznamy

[ProjectDashboardService.cs:77](PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs#L77): INNER JOIN `record.SubsystemId equals subsystem.Id`. Pokud záznam má SubsystemId na neexistující subsystém, celý záznam je tiše vyloučen z dashboardu.

**Doporučení:** LEFT JOIN (nebo `.DefaultIfEmpty()` v LINQ) s fallback `SubsystemKod = "?"`.

#### R15.3 TAKE(500) bez upozornění

Žádný indikátor pro uživatele, že výsledky jsou oříznuty. Na velkých projektech se záznamy za 500. prostě nezobrazí.

**Fix:** Přidat do ViewModelu `bool IsTruncated` a v UI banner „Zobrazeno prvních 500 záznamů."

#### R15.4 WorstOffsetDays sémantika pro AwaitingActual

Pro kategorii AwaitingActual je `WorstOffsetDays = (today - PlanEndDate).Days` — dny od vypršení plánu. V UI se zobrazí jako „+15 dní" → uživatel to může interpretovat jako 15 dní prodlení, ale ve skutečnosti to znamená 15 dní čekání na vyplnění skutečnosti.

### R16 — Výzvy: RecordService save flow přepisuje VyzvaId

#### R16.1 RecordService.SaveRecord.ReplaceRecordExternalLinksAsync

[RecordService.SaveRecord.cs:1214](PmTracker.Web/Services/RecordService.SaveRecord.cs#L1214): `existingEntity.VyzvaId = vyzvaId` — formulář záznamu může přepsat VyzvaId nastavenou výzvy switch/reassign flow.

**Scénář:**
1. User zapne switch na PNF → VyzvaService nastaví `VyzvaId = 5` (Výzva 1/2026)
2. User otevře editor záznamu — formulář obsahuje starý hidden field s `VyzvaId = null`
3. User uloží záznam → RecordService přepíše `VyzvaId = null` → PNF zmizí z výzvy

**Závažnost:** STŘEDNÍ — závisí na tom, zda formulář vůbec posílá VyzvaId hidden field.

#### R16.2 Chybějící validace v RecordService

RecordService.SaveRecord nepouží `VyzvaStateMachine.JePovolenyPrechod` — může přiřadit PNF do Odeslano výzvy přes formulář, přestože VyzvaService to blokuje.

**Fix:** Buď zabránit formuláři modifikovat VyzvaId (readonly/hidden bez serializace), nebo přidat service-level validaci.

### R17 — Výzvy: concurrency a race conditions

| Scénář | Důsledek | Mitigace |
|---|---|---|
| 2 users kliknou „Založit výzvu" současně | Oba dostanou stejné `PoradoveVRoce` → unique constraint exception na 2. | DB unique index `ux_vyzvy_smlouva_rok_poradove` chrání integritu |
| User togglene switch, jiný user smaže záznam | `NastavitZaradidAsync` načte odkaz (stále existuje) → SaveChanges uspěje → záznam smazán → CASCADE smaže odkaz → VyzvaId ztracen | Neškodné — výzva se reload |
| Drag & drop + současné smazání cílové výzvy | `PrerditPnfAsync` ověří existenci na řádku 114, ale výzva smazána mezi řádky 114 a 121 | FK constraint → SqlException → toast error |
| Buffer se vysype mezi panel load a Zalozit klik | `ZaloztVyzvuZBufferuAsync:26` vrátí `BufferEmpty` error | Akceptovatelná degradace |

### Kontrolní seznam před commitem

- [ ] `dotnet build` projde bez warningů
- [ ] Všechny existující testy projdou (`dotnet test`)
- [ ] CSS třídy v nových/upravených views mají definice v CSS
- [ ] Dark mode override pro nové barvy
- [ ] Žádné hardcoded RGB barvy — používat `var(--pm-*)` nebo `var(--gov-*)`
- [ ] Žádné nové z-index hodnoty bez kontroly hierarchie (tabulka v R1.3)
- [ ] `pm:panel-loaded` event stále dispatchovaný (R2.1)
- [ ] Route parametry pojmenovány `projektId` (ne `id`) (R5.1)
- [ ] Nové POST endpointy mají `[ValidateAntiForgeryToken]`
- [ ] Nové endpointy mají `[Authorize(Policy = "permission:...")]`
- [ ] Nové permission keys přidány do `PermissionSeedConfiguration`
- [ ] Nové tabulky/sloupce mají SQL upgrade skript (ne EF migration)
- [ ] `publish.zip` vytvořen po `dotnet publish`

---

## Doporučené pořadí implementace

### Fáze A — Quick fixes (1-2 dny)
1. **#4** NES export route fix (5 min)
2. **#7** Year range bug fix (15 min)
3. **#2** `.text-danger` CSS (5 min)
4. **#3** Smazat mrtvý CSS (10 min)

### Fáze B — CSS stylování (2-3 dny)
5. **#1** Dashboard záznamy CSS (expandable detail, tabulky, akce)
6. **#5** NES panel CSS (bílé řádky na šedém pozadí)

### Fáze C — Statistiky opravy (1-2 dny)
7. **#7** (pokud ještě neopraveno) Year selector fix + nezávislý availableYears dotaz
8. **#8** Zruseno KPI implementace
9. **#9** Subsystem breakdown doplnění

### Fáze D — Výzvy redesign (5-7 dní)
10. **#11** Smazat auto-číslování, přidat ruční volbu
11. **#14** Modal „Nová výzva" s výběrem čísla
12. **#12** Dvousloupcový layout 20/80
13. **#13** Three-dot menu + drag & drop

### Fáze E — Vyhledávání (3-5 dní)
14. **#15** Ověření FTS na serveru, aktivace
15. **#16** Rozšíření suggest o osoby, jednání, návrhy
16. **#17** Seskupení výsledků v dropdownu + nové badge CSS třídy
17. **#10** PDF export statistik (závisí na QuestPDF nebo jiné knihovně)

### Fáze F — Dokumentace (1 den)
18. Přepsat wiki výzvy (vytvorit-vyzvu.md, buffer-cards.md)
19. Aktualizovat NES wiki (odebrat zmínku o chat modalu na řádku)
20. Přidat NES export wiki — doplnit informace o chybějících stylech

---

## Rozšířená souhrnná tabulka issues (finální)

| # | Bod | Závažnost | Popis | Soubor | Řádky | Kategorie |
|---|---|---|---|---|---|---|
| 1 | 8 | KRITICKÉ | 7 CSS tříd bez definice — rozbalený detail nemá styl | site.css | chybí | CSS |
| 2 | 8 | STŘEDNÍ | `.text-danger` neexistuje | site.css | chybí | CSS |
| 3 | 8 | NÍZKÁ | Mrtvý CSS kód (`.dashboard-step-detail`, `.dashboard-category-filter*`) | site.css | 7077-7117 | CSS |
| 4 | 9 | KRITICKÉ | Route param `"id"` místo `"projektId"` — export nefunkční | _NesPanel.cshtml | 28, 34 | Funkční bug |
| 5 | 9 | KRITICKÉ | Žádné CSS pro `pm-nes-panel*` | site.css | chybí | CSS |
| 6 | 9 | STŘEDNÍ | Prázdný seznam bez diagnostického info | _NesPanel.cshtml | 43-45 | UX |
| 7 | 10 | KRITICKÉ | Year range bug → zacyklení selectoru | ProjectDashboardService.cs | 199-200 | Funkční bug |
| 8 | 10 | STŘEDNÍ | `Zruseno` KPI hardcoded na 0 | ProjectDashboardService.cs | 426 | Neúplná impl. |
| 9 | 10 | STŘEDNÍ | Subsystem breakdown `VProdleni`/`Zruseno` = 0 | ProjectDashboardService.cs | 261-262 | Neúplná impl. |
| 10 | 10 | STŘEDNÍ | Chybí PDF/tisk export | — | — | Chybějící feature |
| 11 | 11 | REDESIGN | Auto-číslování → ruční výběr čísla | VyzvaCodeGenerator.cs | celý | Redesign |
| 12 | 11 | REDESIGN | Layout: stack → 20/80 dvousloupcový | _VyzvyPanel.cshtml | celý | Redesign |
| 13 | 11 | NOVÝ | Three-dot menu + drag & drop na PNF | — | — | Nový feature |
| 14 | 11 | NOVÝ | Tlačítko „Nová výzva" s modal | — | — | Nový feature |
| 15 | 12 | STŘEDNÍ | FTS nemusí být nainstalovaný | — | — | Konfigurace |
| 16 | 12 | STŘEDNÍ | Suggest jen 2 entity (záznamy + vyjádření) | DbSuggestService.cs | celý | Neúplná impl. |
| 17 | 12 | NOVÝ | Seskupení výsledků v suggest dropdownu | global-search.js | renderHits | Nový feature |
| 18 | 10 | VYSOKÉ | `Splneno` zahrnuje CANCEL (chybí rozlišení DONE/CANCEL) | ProjectDashboardService.cs | 382-389 | Data bug |
| 19 | 10 | STŘEDNÍ | `DatumUkonceni` je plán, ne datum splnění — chybí DB sloupec | PmTrackerEntities.cs | 185 | Archit. omezení |
| 20 | 10 | STŘEDNÍ | On-time rate: records bez historie automaticky "včas" | ProjectDashboardService.cs | 398-406 | Edge case |
| 21 | 10 | STŘEDNÍ | `VProdleni` KPI ve skutečnosti měří prodloužení termínu | ProjectDashboardService.cs | 410-424 | Sémantika |
| 22 | 8 | STŘEDNÍ | Hardcoded hover barvy bez dark mode | site.css | 7073-7075 | Dark mode |
| 23 | 11 | STŘEDNÍ | `ZaloztVyzvuZBufferuAsync` dvě SaveChanges bez transakce | VyzvaService.Founding.cs | 49+64 | Integrita |
| 24 | 11 | STŘEDNÍ | Race condition v `reloadPanel` (double-click) | panelController.js | 22-35 | JS bug |
| 25 | 12 | NÍZKÁ | Suggest badge text jen 2 varianty, fallback na „Záznam" | global-search.js | 83-89 | UX |
| 26 | 12 | NÍZKÁ | ZaznamNavrh indexuje raw JSON (nízký ranking) | EntityDocumentMapper.cs | 104-116 | Search kvalita |
| 27 | 9 | NÍZKÁ | NES panel zobrazuje i PMP/PNF, ale název je „NES v prodlení" | _NesPanel.cshtml | 22 | UX nesoulad |
| 28 | wiki | STŘEDNÍ | Výzvy wiki kompletně zastaralá (jiný model) | docs/wiki/vyzvy/ | celý | Dokumentace |
| 29 | wiki | NÍZKÁ | NES wiki zmiňuje neexistující chat modal klik | docs/wiki/nes/ | 110-114 | Dokumentace |
| 30 | 9 | NÍZKÁ | NES export Excel bez formátování (bez bold, hyperlinků) | NesPanelExcelExportService.cs | 12 | UX |
| 31 | 8 | STŘEDNÍ | ScheduleUrl `tab=harmonogram` ignoruje `recordId` parametr | ProjectDashboardService.cs | 169 | Funkční bug |
| 32 | 8 | STŘEDNÍ | INNER JOIN na Subsystemy skrývá záznamy bez subsystému | ProjectDashboardService.cs | 77 | Data loss |
| 33 | 8 | NÍZKÁ | TAKE(500) bez UI upozornění o truncation | ProjectDashboardService.cs | 94 | UX |
| 34 | 11 | VYSOKÉ | RecordService.SaveRecord může přepsat VyzvaId z formuláře | RecordService.SaveRecord.cs | 1214 | Security |
| 35 | 11 | STŘEDNÍ | RecordService nevaliduje výzva stav při přiřazení | RecordService.SaveRecord.cs | 1201 | Security bypass |
| 36 | 11 | STŘEDNÍ | Concurrent Zalozit → unique constraint race (není graceful) | VyzvaService.Founding.cs | 29-34 | Concurrency |
| 37 | 10 | VYSOKÉ | Quarters ActualCompletions používá plánovaný termín místo skutečného | ProjectDashboardService.cs | 244-246 | Sémantika |
| 38 | 10 | STŘEDNÍ | VProdleni KPI ve skutečnosti = deadline extensions (ne harmonogram delay) | ProjectDashboardService.cs | 410 | Sémantika |

**Celkem: 38 identifikovaných issues** (6 kritických, 7 vysokých, 16 středních, 9 nízkých)
