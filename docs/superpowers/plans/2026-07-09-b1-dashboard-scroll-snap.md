# B1 — Dashboard: scroll-snap + fade na panelech — Implementation Plan

> **For agentic workers:** Exekuce INLINE. **Commity DRŽET.** Pouštět POSLEDNÍ (vizuální ladění; závisí na naplněných panelech v dev DB — seed viz Task 2 Step 1).

**Goal:** Žádná dlaždice v panelech Přehledu není proříznutá hranou; spodní fade signalizuje pokračování.

**Architecture:** CSS-only na sdílených třídách: listy `scroll-snap-type: y proximity` + konstantní bottom fade `mask-image`; dlaždice `scroll-snap-align: start`.

## Global Constraints
- Žádné změny výšek/tokenů/markupu. Konstantní mask (žádný JS). Commity držené.

---

### Task 1: Source-assertion test + CSS

**Files:**
- Test: `PmTracker.Tests.Unit/Layout/DashboardTileTypographyTests.cs` (přidat 1 test — stejná doména)
- Modify: `PmTracker.Web/wwwroot/css/site.css` — blok listů (`.dashboard-focus-list, .dashboard-meeting-list, .dashboard-meetings-list, .dashboard-news-list`, ~ř. 7536) + blok dlaždic (`.dashboard-focus-item, .dashboard-meeting-item, .dashboard-news-item`, ~ř. 734)

- [ ] **Step 1: Failing test** (do DashboardTileTypographyTests):

```csharp
    [Fact]
    public void PanelLists_UseScrollSnapAndBottomFade()
    {
        var css = Css;
        var list = Block(@"\.dashboard-focus-list");
        list.Should().Contain("scroll-snap-type", "B1: snap drží celé dlaždice po doscrollování");
        list.Should().Contain("mask-image", "B1: fade signalizuje pokračování obsahu");
        Block(@"\.dashboard-focus-item").Should().Contain("scroll-snap-align");
    }
```
Pozn.: `Block(@"\.dashboard-focus-list")` matchne selektorovou skupinu listů (regex `[^{]*\{`); `.dashboard-focus-item` skupinu dlaždic.

- [ ] **Step 2: Run — FAIL.** `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~PanelLists_UseScrollSnap" --nologo -v q`

- [ ] **Step 3: CSS** — do bloku listů přidat:

```css
    /* B1 (2026-07-09): scroll-snap — po doscrollování jsou viditelné dlaždice celé
       (proximity: plynulý scroll bez lepení); fade dole signalizuje pokračování.
       Konstantní mask (bez JS) — fade je i na konci scrollu, přijatelný kompromis. */
    scroll-snap-type: y proximity;
    mask-image: linear-gradient(to bottom, black calc(100% - 28px), transparent);
```
a do bloku dlaždic (`.dashboard-focus-item, .dashboard-meeting-item, .dashboard-news-item`):
```css
    scroll-snap-align: start;
```

- [ ] **Step 4: Run — PASS** + grep duplicit (`grep -n "dashboard-focus-list" site.css` — jen jeden definiční blok).

### Task 2: Vizuální ověření (Playwright)

- [ ] **Step 1: Dev data** — panely musí scrollovat: focus 8+ úkolů (stav OPEN, vlastník 1) a 6+ jednání v projektu 1 existují z předchozích seedů; jednání doplnit:
```sql
INSERT INTO jednani (projekt_id, cislo_jednani, datum_planovane, cas_zacatek, misto, stav_jednani_id)
SELECT 1, 904+n, CAST(DATEADD(day, 8+n, GETDATE()) AS date), '09:00', N'Zasedačka B', 3
FROM (VALUES (0),(1),(2)) v(n)
WHERE NOT EXISTS (SELECT 1 FROM jednani WHERE projekt_id=1 AND cislo_jednani=904+n);
```
(sqlcmd s `-d PmTracker`; N-prefix na diakritiku.)

- [ ] **Step 2: Měření (1280×832):** pro focus i meetings list:
  - `list.scrollHeight > list.clientHeight` (scrolluje);
  - po `list.scrollTop = 0` + snap settle (300 ms): žádná dlaždice s `rect.top < listRect.bottom && rect.bottom > listRect.bottom` s překryvem > 2 px (tolerance sub-pixel);
  - `getComputedStyle(list).maskImage !== 'none'`;
  - scroll na konec: poslední dlaždice celá nad spodní hranou − fade (bottom ≤ listRect.bottom).
- [ ] **Step 3: Screenshoty 1280×832** (top + doscrollováno) do reportu — fade vzhled odsouhlasí user.
- [ ] **Step 4: Regrese A2/A3 čísel:** dlaždice ≤ 120 px, Přehled bez page-scrollu, gap 20 px k footeru (rychlé přeměření stávajícím skriptem `measure-a2.js`/`verify-a3-a9.js`).
