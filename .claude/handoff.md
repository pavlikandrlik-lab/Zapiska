# Session Handoff

> Generated: 2026-04-30 | Branch: codex/senior-refactor-fase-1

## Completed (2026-04-29 → 2026-04-30)

### 1. pm-tabs lego architecture (custom Web Component)
- Round-out borders technique (CSS-tricks/Lea Verou) implementováno v light DOM Web Component
- 4 varianty: `pm-tab-left` / `pm-tab` / `pm-tab-right` / `pm-tab-last-in-row`
- CSS proměnné `--pm-tab-page-bg / --pm-tab-inactive-bg / --pm-tab-active-bg / --pm-tab-radius` v site.css s gov tokens fallbacky
- Panel rounding edge cases: pm-tab-left active → top-left=0; pm-tab-right active → top-right=0; single-tab edge case ošetřen přes `:has(pm-tab-left:only-child)`
- Active tab podtržení textu 3px modré (`var(--gov-color-primary)`, offset 4px)
- Files: [pmTabs.js](PmTracker.Web/wwwroot/js/components/pmTabs.js), [site.css:5400-5750](PmTracker.Web/wwwroot/css/site.css#L5400), [_SyncPanel.cshtml](PmTracker.Web/Views/Nastaveni/_SyncPanel.cshtml), [_EditZaznamForm.cshtml](PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml)

### 2. PmTabsTagHelper opt-in conflict fix
- `<pm-tabs>` tag helper přepisoval Web Component na `<gov-tabs>` vždy
- Opt-out: pokud je `persist`/`persist-key`/`sync-input` přítomen → tag ponechán nedotčený
- File: [PmTabsTagHelper.cs](PmTracker.Web/TagHelpers/PmTabsTagHelper.cs)
- Test: 3 [Theory] cases v [PmTabsTagHelperTests.cs](PmTracker.Web.Tests/TagHelpers/PmTabsTagHelperTests.cs)

### 3. Sync panel filter bug (CSS specificity)
- `pm-tab-panel.sync-tab-panel-content { display: grid }` (0,1,1) přebíjelo `pm-tab-panel:not([active]) { display: none }` (0,1,1) kvůli source order
- Fix: přidán `[active]` qualifier → 0,2,1 specificity, aplikuje se jen na aktivní panel
- File: [site.css:5728](PmTracker.Web/wwwroot/css/site.css#L5728)

### 4. Authz handler multi-source projektId lookup
- **Bug**: `PermissionAuthorizationHandler` četl projektId pouze z `RouteValues` → query-string (NavrhyController) i form-body (POST endpointy) selhaly → 403 pro user s validní project rolí
- **Druhý projev**: ProjectDashboardController měl route param `id` místo `projektId`
- Fix 1: Handler rozšířen o `Route → Query → Form` lookup s fail-closed na malformed
- Fix 2: ProjectDashboard route přejmenován `{id:int}` → `{projektId:int}` + všechny action params + Url.Action
- Files: [PermissionAuthorizationHandler.cs](PmTracker.Web/Services/Security/PermissionAuthorizationHandler.cs), [ProjectDashboardController.cs](PmTracker.Web/Controllers/ProjectDashboardController.cs), [Detail.cshtml:90](PmTracker.Web/Views/Projekty/Detail.cshtml#L90)
- Tests: 4 nové cases v [PermissionPolicyHandlerTests.cs](PmTracker.Tests.Unit/Authorization/PermissionPolicyHandlerTests.cs) — query, form, route precedence, malformed query

### 5. Settings/SaveAsync sync trigger (uživatel akceptoval, neopraveno)
- Po Uložit v sync settings hosted service spustí Manual run → IsRunning=true → "Spustit teď" disabled
- Root cause: `ManualSignal.Signal()` v [SyncJobAdminHandlerBase.SaveAsync:119](PmTracker.Web/Services/Sync/SyncJobAdminHandlerBase.cs#L119) — signal mechanism má jediný výklad: Manual run trigger
- Status: user řekl "nechci nic, dobrý" — záměrně necháno, není to fix candidate

## Pending

User signalizoval "další opravy" před compact — konkrétní task přijde po compactu. Žádné explicit pending work.

**Ne-případ k investigaci** (objevené během session, nebudu řešit bez explicit požadavku):
- Sync settings switch off symptom — root cause nejasný (DB stav neověřen, server-side flow čistý). Pokud se zopakuje, vyžádat browser DevTools network log + DB SELECT na `[Ad/Sd]SyncSettings.IsEnabled`.

## Context

- Branch: `codex/senior-refactor-fase-1`
- Last commit: `b38176d fix(modal-vyjadreni): zachovat scroll pozici po přiřazení kroku (anchor-based)`
- **Uncommitted changes**: ANO — všechna dnešní práce + pre-existing branch změny (modal-vyjadreni, RecordEditor refactor, atd.). Před commit nutné review/separation.
- Solution: `PmTracker.sln` (root)
- Publish: [publish.zip](publish.zip) (12 MB) aktuální s authz fixem (poslední rebuild 14:20)

## Doplňkové pravidla z této session (pro MEMORY.md)

Nově vznikla v learning-log.md (3 entries 2026-04-30):
1. PermissionAuthorizationHandler multi-source projektId lookup
2. CSS specificity rovnost `:not([attr])` vs `.class` → vždy `[active]` qualifier pro pm-tab-panel rules
3. TagHelper a stejnojmenný Web Component se přebíjí (opt-out marker attribute pattern)

Pokud se některý opakuje, povýšit do MEMORY.md jako preventive rule.
