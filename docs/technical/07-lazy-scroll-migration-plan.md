# Vertical lazy-load „po dvojnásobku stránky" — migrační plán

**Status:** Infrastructure hotová ([`lazyScrollLoader.js`](../../PmTracker.Web/wwwroot/js/modules/lazyScrollLoader.js)),
konzumenti (konkrétní seznamy) čekají na per-list migraci. Architectural rozhodnutí
uživatele 2026-04-23: initial render 2× viewport + scroll-triggered další batch.

## Pattern

```
┌─────────────────────────┐
│  Initial server render  │  ← Razor vykreslí prvních N items (~2× viewport)
│  items[0..N-1]          │
└─────────────────────────┘
┌─────────────────────────┐
│  [sentinel div]         │  ← IntersectionObserver (rootMargin: 200% viewport)
└─────────────────────────┘
         │ scroll triggers
         ▼
┌─────────────────────────┐
│  fetch /list?offset=N   │  ← GET partial view s ?offset=&take=
│        &take=N          │
└─────────────────────────┘
         │
         ▼
┌─────────────────────────┐
│  append před sentinel   │
│  offset += result.items │
└─────────────────────────┘
```

## Čeká na migraci (prioritně)

| List | View | Controller action | Predpokládaný page size |
|---|---|---|---|
| Projekty — záznamy tab | [`_ProjectRecordsTab.cshtml`](../../PmTracker.Web/Views/Projekty/_ProjectRecordsTab.cshtml) | `ProjektyController.TabPartials` | 50 |
| Jednání — detail (účast) | [`Detail.cshtml`](../../PmTracker.Web/Views/Jednani/Detail.cshtml) | `JednaniController.Detail` | 100 |
| Projekty — historie vyjádření | `ProjectService.DetailQueries` | `ProjektyController.Detail` | 30 |
| Návrhy — seznam | `_ProjectProposalsTab.cshtml` | `NavrhyController` | 30 |
| Dashboard — RecordsPanel | `RecordsPanel.cshtml` | `ProjectDashboardController` | 50 |

## Per-list migrace (postup)

1. **Server-side**: Přidat `GetRecordsPageAsync(int projectId, int offset, int take)` do service vrstvy
   (nebo novou `GetProjectRecordsPageHandler` ve vertical-slice stylu — viz
   [`SaveProjectHandler`](../../PmTracker.Web/Services/Handlers/SaveProjectHandler.cs)).
   Endpoint vrací `{ items: [...], hasMore: bool }`.

2. **Controller**: Přidat `GET /Projekty/{id}/ZaznamyPage?offset=&take=` který render-uje **partial view**
   (jen items, bez wrapperu). Route zachovat konvenci: `TabPartials` collector.

3. **Razor view**: V tab view vykreslit prvních `take` items + `<div data-lazy-scroll-sentinel>`
   + `data-lazy-scroll-initial-offset="N"` na container.

4. **Frontend**: V odpovídajícím init modulu (např. `projectTabs.js`):

   ```js
   import { initLazyScrollLoader } from "./lazyScrollLoader.js";

   initLazyScrollLoader(tabEl, {
       fetchMore: async (offset, take, signal) => {
           const res = await fetch(`/Projekty/${projectId}/ZaznamyPage?offset=${offset}&take=${take}`, {
               signal, headers: { "Accept": "application/json" }
           });
           return res.json();
       },
       renderItem: (item) => item.html,   // server vrací už HTML segmenty
       itemsContainer: tabEl.querySelector("[data-records-list]"),
       sentinel: tabEl.querySelector("[data-lazy-scroll-sentinel]"),
       pageSize: 50
   });
   ```

5. **Tests**: `*ApiTests` přidat integration test na GET endpoint s offset validací (403 pro
   bez `projects.read`, 200 s correct `hasMore` pro happy path).

## Poznámka k authz

Endpoint **musí** respektovat stejný data-scope jako existující tab render (typicky
`CurrentUserContext.VisibleProjectIds.Contains(projectId)` + `HasPermission("records.read", projectId)`).
Lazy-load není bypass authz.

## Proč ne klasické stránkování

User rozhodnutí: doménově dělené taby (subsystem, kategorie) > umělé „stránka 1 z N" paginace.
Pokud tab přeroste tisíce items, přidat **uvnitř tabu** filter nebo druhou úroveň
lazy-load (po sub-kategoriích). Nikdy „pager" komponentu — stránkovaný pohled ztrácí kontext.
