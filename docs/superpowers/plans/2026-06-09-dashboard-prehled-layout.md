# Dashboard přehled — fluid + adaptivní layout — Implementation Plan

> **For agentic workers:** Spec: `docs/superpowers/specs/2026-06-09-dashboard-prehled-layout-design.md`. Verifikace je vizuální (Playwright screenshoty per scénář) + `dotnet build`. CSS nemá unit testy → red-green je „před/po" screenshot.

**Goal:** Hlavní stránka (přehled) využije prostor hustěji, plynule škáluje 13"→28" a Novinky už neodjedou pod fold.

**Architecture:** Čistě prezentační vrstva. Flex-fill výška (bez měření headeru JSkem), fluid `clamp()` tokeny na `.dashboard-shell`, adaptivní hustota přes container queries scoped na `.dashboard-panel-body`. Konsolidace duplicitního dashboard CSS do jednoho regionu. Žádná změna controlleru/viewmodelů/služeb.

**Tech Stack:** CSS (custom props, clamp, container queries, :has), Razor partial (1 třída), ASP.NET Core MVC.

---

## File Structure

- `PmTracker.Web/wwwroot/css/site.css` — konsolidace + tokeny + flex-fill + container queries (hlavní práce).
- `PmTracker.Web/Views/Dashboard/_DashboardMeetingsList.cshtml` — sjednotit třídu listu (bugfix).
- (read-only ověření) `_Layout.cshtml`, partials focus/news list — beze změn dat.

Žádné JS změny (JS používá jen `data-*` atributy → nedotčeno).

---

## Task 1: Bugfix — meetings list scroll (class mismatch)

**Files:** Modify `PmTracker.Web/Views/Dashboard/_DashboardMeetingsList.cshtml`

- [ ] Změnit `class="dashboard-meeting-list"` → `class="dashboard-meeting-list dashboard-meetings-list"` (drží zpětně i staré CSS) — nebo sjednotit na jeden název v CSS (preferováno v Tasku 2, kde se třída zařadí do scroll pravidla). Konkrétně: ponechat singulár v partialu a v Tasku 2 zařadit singulár do overflow pravidla.

(Reálná oprava je v Tasku 2 — zde jen poznámka, partial necháme `dashboard-meeting-list`.)

## Task 2: Konsolidace + flex-fill výška + scroll chain

**Files:** Modify `PmTracker.Web/wwwroot/css/site.css` (region ~6996–7079)

- [ ] Nahradit blok `.dashboard-shell … @media(max-width:1024px)` konsolidovaným blokem:
  - tokeny na `.dashboard-shell` (viz spec sekce B)
  - `.app-main--fluid:has(.dashboard-shell){ display:flex; flex-direction:column; }` (scope flex-fill jen na dashboard)
  - `.dashboard-shell{ flex:1 1 auto; min-height:0; display:flex; flex-direction:column; }` (zrušit 100vh)
  - `.dashboard-body{ flex:1; grid 2fr/1fr; rows minmax(--d-panel-min,1fr)×2; gap/padding z tokenů; min-height:0 }`
  - sekce focus/meetings/news placement (beze změny)
  - `.dashboard-panel{ display:flex; flex-direction:column; min-height:0; overflow:hidden }`
  - `.dashboard-panel > [data-dashboard-panel-content]{ flex:1 1 auto; min-height:0; display:flex; flex-direction:column }`
  - `.dashboard-panel-body{ flex:1 1 auto; min-height:0; container-type:inline-size; container-name:dashpanel; gap/padding z tokenů }`
  - footer z tokenů
  - listy: `.dashboard-focus-list, .dashboard-meeting-list, .dashboard-meetings-list, .dashboard-news-list { flex:1 1 auto; min-height:0; overflow-y:auto; gap z tokenu }` ← **singulár i plurál = bugfix**
  - `@media(max-width:1024px)` stack fallback (shell height:auto, page scroll)
- [ ] Build: `dotnet build PmTracker.Web` → exit 0.

## Task 3: Tokenizace položek + container-query hustota

**Files:** Modify `PmTracker.Web/wwwroot/css/site.css` (region ~630–793)

- [ ] Odstranit duplicitní `.dashboard-panel-body` (630), `.dashboard-panel-footer` (645), listy (652) — žijí už v konsolidovaném bloku (Task 2).
- [ ] Items (`.dashboard-*-item`, title, meta, description, identity) přepsat na fluid tokeny (`--d-fs-*`, `--d-pad-card`, `--d-radius`, `--d-gap-item`).
- [ ] Nahradit `@media(max-width:899px)` za `@container dashpanel (max-width: 480px)` s kompaktní variantou focus karty (stack, meta 2-col, description 1 řádek).
- [ ] Build → exit 0.

## Task 4: Vizuální verifikace (Playwright) + commit

- [ ] Spustit appku, projít scénáře (13"/FHD/28" viewporty, prázdné panely) → screenshoty.
- [ ] Ověřit: Novinky vždy viditelné (interní scroll), žádný fold; hustota OK; dark mode OK.
- [ ] Commit izolovaně (oddělené commity bugfix / layout / tokenizace).

---

## Self-review
- Spec coverage: A(výška)→T2, B(tokeny)→T2/T3, C(hustota)→T3, D(scénáře)→T4, bugfixy(scroll/100vh)→T2. ✓
- Sdílené focus třídy (Focus.cshtml/Profil): density scoped na `@container dashpanel` → list-page nedotčen. ✓
- Duplicita: konsolidace v T2/T3. ✓
</content>
</invoke>
