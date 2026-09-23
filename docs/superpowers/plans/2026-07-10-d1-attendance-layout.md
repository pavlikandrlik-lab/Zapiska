# D1 — Účast: inline řádky, column-major, číslování (implementační plán)

> **For agentic workers:** Inline exekuce (executing-plans) v hlavní session — bez subagentů. Kroky checkbox syntaxí.

**Goal:** Karta účastníka = jedna řádka `č. · jméno · role · e-mail` (zalomení až při nedostatku šířky), dvousloupec plněný shora dolů s deterministickým počtem řádků, decentní CSS číslo v kartě.

**Architecture:** Markup: divy → spany v pořadí jméno→role→e-mail; `--attendance-rows: ⌈n/2⌉` inline na gridu. CSS: flex-wrap uvnitř karty, `@media (min-width:1900px)` grid `grid-auto-flow: column` + `repeat(var(--attendance-rows), auto)`, číslo přes counter `::before`. Jen letité konstrukce (i15 lekce). Backend, form kontrakt `rows[i]`, B6 řazení beze změny.

**Tech Stack:** Razor, CSS (flex/grid/counters/media), xUnit, Playwright.

## Global Constraints
- Commity DRŽET; finální vizuální verdikt = uživatel na i15.
- Zákaz `:has`/`max-content`/multi-column v attendance bloku (pin testem).
- Mobilní fallback ≤640 px zachovat (select 100 %, wrap).

### Task 1: RED testy
**Files:** Modify `PmTracker.Tests.Api/Controllers/MeetingAttendanceRenderTests.cs`; Create `PmTracker.Tests.Unit/Layout/MeetingAttendanceLayoutTests.cs`
- [ ] Api: person blok = spany v pořadí strong→roles→email (regex `meeting-attendance-person[\s\S]{0,400}?<strong>[\s\S]{0,200}?<span class="muted meeting-attendance-roles"[\s\S]{0,300}?<span class="muted meeting-attendance-email"`), žádný `<div class="muted meeting-attendance-roles"`, grid nese `--attendance-rows: ⌈n/2⌉` (n = počet `meeting-attendance-row` v HTML).
- [ ] Unit piny: media 1900 blok s `grid-auto-flow: column` + `repeat(var(--attendance-rows`; `.meeting-attendance-person` flex-wrap; `::before` s `counter(attendance-row)`; attendance CSS úsek bez `:has(`/`max-content`.
- [ ] Run → FAIL.

### Task 2: Markup + CSS (GREEN)
**Files:** Modify `PmTracker.Web/Views/Jednani/Detail.cshtml` (~ř. 92–110), `PmTracker.Web/wwwroot/css/site.css` (~ř. 5550–5587)
- [ ] Grid: `style="--attendance-rows: @((Model.Ucast.Count + 1) / 2)"`; person div → spany jméno→role→e-mail (hidden input zůstává).
- [ ] CSS dle specu: counter-reset na gridu; row = flex baseline s `::before` číslem (min-width 1.6em, muted, 0.75rem, user-select none); person flex-wrap; select `margin-left:auto` 240px; media ≥1900px column-major; media ≤640px `flex-wrap: wrap` + select 100 %.
- [ ] Testy GREEN + `dotnet build`.

### Task 3: Živé ověření + regrese
- [ ] Dev seed: ucast řádky pro jednání 3 (5 osob — sloupce 3+2) přímým SQL (sloupce dle sys.columns).
- [ ] Playwright: rozbalit „Zobrazit účast"; 1470×956 → 1 sloupec, čísla 1..5 dolů, karty 1–2 řádky; 2560×1440 → 2 sloupce, vlevo 1–3, vpravo 4–5, jednořádkové; screenshoty pro uživatele.
- [ ] Regrese: Unit full, Api full (známé 4 gantt výjimky).

### Task 4: Držený commit
- [ ] `feat(jednani): D1 — účast inline tok, column-major sloupce, číslování řádků`
