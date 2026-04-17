# Specifikace — uživatelské novinky na dashboardu (dashboard-user-news)

Popisuje požadované chování panelu **„Co je nového"** v pravém dolním rohu uživatelského dashboardu (`/` resp. `/dashboard`).

Cílem je, aby uživatel na první pohled viděl poslední relevantní dění k **jeho ticketům (projektovým záznamům)** — tedy jen to, co se ho opravdu týká.

---

## Zdroj dat

Novinky se skládají z posledních akcí zaznamenaných v auditu ([AuthzAuditLog](../../PmTracker.Web/Services/Audit/)).

Relevantní entity a akce:

| EntityType | Action | Význam |
| --- | --- | --- |
| `Record` (nebo legacy `projektove_zaznamy`) | `Create` / `Update` | Nový / změněný projektový záznam (ticket) |
| `Comment` (vyjádření) | `Create` / `Update` | Nové / změněné vyjádření na ticketu |
| `Meeting` (jednání) | `Create` / `Update` | Nové / změněné jednání |

Vyloučen je **sám uživatel** jako autor akce (`ActorOsobaId != currentUser.OsobaId`) — vlastní akce se v novinkách nezobrazují.

---

## Filtrování relevance („mé tickety")

Novinka se uživateli zobrazí jen tehdy, pokud má k ticketu (resp. jednání) vazbu. Vazba znamená jednu z následujících rolí:

1. **Vlastník záznamu** — `ProjektoveZaznamy.VlastnikId = OsobaId` uživatele
2. **Spolupráce na záznamu** — existuje řádek v `ZaznamSpoluprace` s `OsobaId` uživatele
3. **Vedoucí / zástupce vedoucího subsystému** — aktivní obsazení (`ObsazeniSubsystemuProjektu`) v roli `Lead` / `DeputyLead` na `ProjektSubsystem`, do kterého ticket spadá (pár `ProjektId:SubsystemId`)

Pro novinky typu **Vyjádření** a **Úkol** se relevance vyhodnocuje vůči ticketu, kterého se akce týká.

Pro novinky typu **Záznam jiné kategorie než úkol** (informace, rozhodnutí a podobné) platí volnější pravidlo: zobrazí se i bez přímé vazby, pokud má uživatel přístup do projektu — ale **pouze při vytvoření** (`Create`), ne při úpravě.

Pro novinky typu **Jednání** se požaduje, aby uživatel měl přístup do projektu jednání (viditelné projekty nebo globální `ProjectRead`).

---

## Prázdný stav

Pokud po aplikaci filtru není žádná relevantní novinka, panel zobrazí přesně:

> **Nic nového se neudálo**

(Aktuální text `Zatím nejsou k dispozici žádné nové relevantní změny.` má být nahrazen.)

---

## Počty a stránkování

| Místo | Počet položek |
| --- | --- |
| Panel na dashboardu (pravý dolní roh) | **5** |
| Stránka „Více" (`/dashboard/news`) | **50** (nejnovější) |

Stránka „Více" drží zbývající novinky za prvními 5 a umožňuje další dohrávání (viz níže).

---

## Lazy loading — panel na dashboardu

Panel se nevykresluje serverem v prvním requestu na `/` — render dashboardu vrátí jen **kostru panelu** s placeholderem (animovaný loading skeleton). Obsah dotahuje klient asynchronně.

Tok:

1. `GET /` vrátí `Views/Dashboard/Index.cshtml` s prázdným `data-dashboard-panel="news"` a URL pro fetch v `data-dashboard-panel-url`.
2. Skript [dashboard.js](../../PmTracker.Web/wwwroot/js/modules/dashboard.js) po `initDashboardShell()` pošle `GET /dashboard/news-panel` (`take=5`) a výsledný HTML fragment vloží do `[data-dashboard-panel-content]`.
3. Při chybě fetch se panel přepne do error stavu s textem **„Nepodařilo se načíst obsah panelu."** a tlačítkem pro retry (`data-dashboard-panel-retry`).
4. Retry znovu spustí `loadDashboardPanel(panel)` s původním URL.

**Cílem je, aby error stav nastal jen při skutečné chybě sítě / serveru.** Aktuálně hlášená chyba v produkci při běžném načtení je bug (viz sekce „Známé problémy").

---

## Tlačítko „Více" uvnitř panelu

V zápatí panelu jsou dvě tlačítka:

- **„Načíst více"** — zobrazí se, pokud `LoadedCount < TotalCount`. Kliknutí nahradí obsah panelu fragmentem s větším `take` (krok `+5`). Implementováno přes `data-dashboard-news-load-more` URL.
- **„Zobrazit více"** — odkaz na plnou stránku `/dashboard/news` s 50 položkami a vlastním tlačítkem „Načíst další" pro dohrávání po 20.

Poznámka: Požadavek říká, že **panel má mít 5** a **„Více" vede na stránku s 50 novinkami**. Dosavadní inline tlačítko „Načíst více" uvnitř panelu je nadstavba a může zůstat, ale nesmí skrýt odkaz na plnou stránku.

---

## Řazení

Novinky jsou řazeny **sestupně podle `CreatedAt` auditního záznamu**, tie-break podle `AuditLogId` sestupně. Nejnovější je první.

---

## Otevření detailu

Kliknutí na položku otevře odpovídající detail:

| EntityType | Cíl |
| --- | --- |
| `Jednání` | `/Jednani/Detail/{meetingId}` (s `returnUrl=/dashboard/news`) |
| `Vyjádření` | `/Projekty/Detail/{projectId}?tab=zaznamy&recordId={recordId}&openComments=true` |
| `Záznam` | `/Projekty/Detail/{projectId}?tab=zaznamy&recordId={recordId}` |

URL sestavuje `DashboardController.PrepareNewsPresentation`.

---

## Dotčené soubory

- Controller: [PmTracker.Web/Controllers/DashboardController.cs](../../PmTracker.Web/Controllers/DashboardController.cs)
- Service: [PmTracker.Web/Services/Dashboard/DashboardService.cs](../../PmTracker.Web/Services/Dashboard/DashboardService.cs) — metody `BuildNewsPanelAsync`, `BuildNewsListPageAsync`, `BuildNewsItemsAsync`, `BuildRelevantRecordIdsForDashboardNewsAsync`
- View (panel): [PmTracker.Web/Views/Dashboard/_DashboardNewsPanel.cshtml](../../PmTracker.Web/Views/Dashboard/_DashboardNewsPanel.cshtml)
- View (seznam): [PmTracker.Web/Views/Dashboard/_DashboardNewsList.cshtml](../../PmTracker.Web/Views/Dashboard/_DashboardNewsList.cshtml)
- View (stránka): [PmTracker.Web/Views/Dashboard/News.cshtml](../../PmTracker.Web/Views/Dashboard/News.cshtml)
- Dashboard shell: [PmTracker.Web/Views/Dashboard/Index.cshtml](../../PmTracker.Web/Views/Dashboard/Index.cshtml)
- JS: [PmTracker.Web/wwwroot/js/modules/dashboard.js](../../PmTracker.Web/wwwroot/js/modules/dashboard.js)
- ViewModels: [PmTracker.Web/Models/ViewModels/HomeViewModels.cs](../../PmTracker.Web/Models/ViewModels/HomeViewModels.cs)

---

## Konstanty

V [DashboardController.cs](../../PmTracker.Web/Controllers/DashboardController.cs):

```csharp
private const int NewsHomepageLimit = 5;        // panel na dashboardu
private const int NewsListDefaultTake = 20;     // inkrementální načítání na stránce
```

Pro splnění požadavku „50 novinek na stránce Více" je cílová hodnota `NewsListDefaultTake = 50` (případně samostatná konstanta `NewsListPageInitialTake`). Inkrement lze nechat na `+20`.

---

## Historie oprav

### 2026-04-17 — oprava 500 při načítání panelu

**Root cause**: v `BuildNewsItemsAsync` byly tři lookup dotazy (`commentsById`, `recordsById`, `meetingsById`) postavené jako `(from … select new XxxAuditRow(…)).Where(item => ids.Contains(item.Xxx)).ToDictionaryAsync(…)`. Filtr přes `Contains` byl aplikovaný **až na projekci do privátního positional `record` typu**, což EF Core 8 na SQL Serveru nedokáže přeložit a na runtime vyhazuje `InvalidOperationException: "The LINQ expression '__ids_0 … .XxxId)' could not be translated."`. Controller vrátil HTTP 500, JS vykreslil error panel.

**Fix**: filtr přesunutý před projekci, tedy `from x in DbSet where ids.Contains(x.Id) join … select new XxxAuditRow(…)`. EF teď filtruje na primárním klíči entity (mapovaný sloupec) → přeloží se do `WHERE Id IN (…)`.

Pokryto regression testem [DashboardServiceNewsQueryShapeTests](../../PmTracker.Tests.Unit/Home/DashboardServiceNewsQueryShapeTests.cs) — pinuje jak správný tvar (translatable), tak původní broken tvar (netranslatable), aby reverze byla okamžitě viditelná.

Ve stejné změně:

- Prázdný stav přepsán na **„Nic nového se neudálo."** ([_DashboardNewsList.cshtml](../../PmTracker.Web/Views/Dashboard/_DashboardNewsList.cshtml)).
- `NewsListDefaultTake` zvýšen z 20 na **50** ([DashboardController.cs](../../PmTracker.Web/Controllers/DashboardController.cs)).

## Zbývající otevřené body

1. **Panel na dashboardu ukazuje `N / M` počet, ale požadavek počítá jen s počtem 5.** Zachovat ukazatel je OK, ale nesmí uživatele mást, když je `TotalCount` vysoké — „Zobrazit více" musí být viditelný.
2. **Tlačítko „Načíst více" uvnitř panelu** (inline pagination po 5) je nadstavba. Zvážit, jestli ho neskrýt a nechat jen odkaz na `/dashboard/news`.
