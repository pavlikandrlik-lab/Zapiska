# C2 — Pravý sloup dashboardu: 50/50 strop + responzivní dlaždice jednání (implementační plán)

> **For agentic workers:** Inline exekuce (executing-plans) v hlavní session — bez subagentů (pravidlo uživatele). Kroky checkbox syntaxí.

**Goal:** Dlaždice jednání viditelné bez rolování (primární akceptace: 3 jednání + plné novinky → 3 dlaždice vidět); dělení sloupce 50/50 jako strop; dlaždice jednání = flex-wrap tok (1 řádka na širokém, přirozené zalomení na úzkém).

**Architecture:** CSS: obsahové rail panely `flex: 1 1 0` (rovný základ) + `max-height: max-content` (strop = vlastní obsah, slack přeteče sourozenci). Markup: `_DashboardMeetingsList.cshtml` dlaždice z 3 blokových řádků na jeden flex-wrap kontejner. Obsahová logika panelů beze změny.

**Tech Stack:** CSS (flexbox, :has — už v produkci), Razor, xUnit, Playwright.

## Global Constraints

- Commity DRŽET — uživatel finálně ověřuje ručně; layout finálně posoudí sám (screenshoty připravit).
- A2 typografické piny MUSÍ přežít: `.dashboard-meeting-title` → `var(--d-fs-base)`, `.dashboard-item-meta` → `var(--d-fs-label)` (DashboardTileTypographyTests).
- B1 piny MUSÍ přežít: scroll-snap + mask na listech, `scroll-snap-align` na dlaždicích.
- Duplicate-CSS memory: nové pravidlo pro `.dashboard-meeting-item` jako záměrný override ZA sdíleným blokem (~ř. 735) s komentářem; před claimem „hotovo" grep duplicitních selektorů.
- Focus a news dlaždice BEZE ZMĚNY.

---

### Task 1: CSS pin testy (RED)

**Files:**
- Modify: `PmTracker.Tests.Unit/Layout/DashboardTileTypographyTests.cs`

- [ ] **Step 1: Failing testy**

```csharp
[Fact]
public void RailContentPanels_SplitEquallyWithContentCap()
{
    var block = Block(@"\.dashboard-rail > \.dashboard-panel:has\(\.dashboard-meeting-list\)");
    block.Should().Contain("flex: 1 1 0", "C2: basis 0 = rovný 50/50 základ (auto vyhrával bohatší obsah)");
    block.Should().Contain("max-height: max-content", "C2: panel neroste nad vlastní obsah — slack přeteče sourozenci");
}

[Fact]
public void MeetingTile_FlowsAsFlexWrap()
{
    // Block() bere PRVNÍ blok selektoru — nový override blok umístit až za sdílený
    // border/padding blok, proto matchovat přes komentář-kotvu C2.
    var css = Css;
    var m = Regex.Match(css, @"/\* C2 [^*]*dlaždice jednání[^*]*\*/\s*\.dashboard-meeting-item\s*\{[^}]*\}", RegexOptions.Singleline);
    m.Success.Should().BeTrue("C2 override blok dlaždice jednání existuje");
    m.Value.Should().Contain("flex-wrap: wrap");
    m.Value.Should().Contain("display: flex");
}

[Fact]
public void FocusAndNewsTiles_KeepExistingLayout()
{
    Block(@"\.dashboard-focus-item").Should().Contain("display: grid", "focus dlaždice beze změny");
    Css.Should().NotContain(".dashboard-news-item {\n    display: flex", "news dlaždice beze změny");
}
```

- [ ] **Step 2: Run → FAIL** (`--filter "FullyQualifiedName~DashboardTileTypography"`)

### Task 2: CSS — rail split + tile flow (GREEN)

**Files:**
- Modify: `PmTracker.Web/wwwroot/css/site.css` (blok ~ř. 7543 a nový blok za ~ř. 750)

- [ ] **Step 1: Rail panely (stávající :has blok ~7543)**

```css
.dashboard-rail > .dashboard-panel:has(.dashboard-meeting-list),
.dashboard-rail > .dashboard-panel:has(.dashboard-meetings-list),
.dashboard-rail > .dashboard-panel:has(.dashboard-news-list) {
    /* C2 (2026-07-10): basis 0 = rovný 50/50 základ (dřívější `1 1 auto` = basis
       podle obsahu → bohatší novinky trvale vytlačovaly jednání, viz spec).
       max-content strop: panel neroste nad vlastní obsah — po zhuštění dlaždic
       si jednání vezmou jen kolik potřebují a zbytek dostanou novinky.
       Pozn.: min-height (floor) vítězí nad max-height dle CSS — panel s 1 dlaždicí
       drží --d-panel-min, to je záměr (viditelná přítomnost panelu). */
    flex: 1 1 0;
    max-height: max-content;
    min-height: var(--d-panel-min);
}
```

Empty-stav pravidlo (`:not(:has(...))` → `flex: 0 0 auto`) beze změny.

- [ ] **Step 2: Nový override blok dlaždice jednání — umístit HNED ZA sdílený border/padding blok (~ř. 750, za `.dashboard-meeting-item, .dashboard-news-item { ... }`)**

```css
/* C2 (2026-07-10): dlaždice jednání = flex-wrap tok — na širokém panelu 1 řádka
   (kód | badge | titulek | datum čas projekt), na úzkém přirozené zalomení.
   Záměrný override display:block ze sdíleného bloku výše; focus/news beze změny. */
.dashboard-meeting-item {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    column-gap: 0.6rem;
    row-gap: 0.25rem;
}

.dashboard-meeting-item .dashboard-meeting-title {
    margin: 0; /* blokový margin 0.15rem nemá v inline toku smysl */
}

.dashboard-meeting-item .dashboard-item-meta {
    margin-top: 0; /* baseline zarovnání v řádce; base pravidlo má 0.2rem */
    min-width: 0;
}
```

(`.dashboard-item-meta` base je už `display:flex; flex-wrap:wrap` — uvnitř
řádky se chová jako inline skupina, nic dalšího netřeba.)

- [ ] **Step 3: Unit testy zelené (celá DashboardTileTypographyTests vč. A2+B1 pinů)**

- [ ] **Step 4: Grep duplicit:** `grep -n "\.dashboard-meeting-item" site.css` — očekávané výskyty: sdílený blok (border/padding/snap), hover blok, nový C2 blok. Nic dalšího.

### Task 3: Markup dlaždice (RED → GREEN)

**Files:**
- Modify: `PmTracker.Web/Views/Dashboard/_DashboardMeetingsList.cshtml`
- Create: `PmTracker.Tests.Api/Controllers/DashboardMeetingTileRenderTests.cs`

**Interfaces:**
- Consumes: `ApiSqlFixture.EnsureMeetingAsync(...)` (budoucí datum, ať panel není prázdný), GET `/dashboard/meetings-panel?asUser=...`.

- [ ] **Step 1: Failing Api render test**

```csharp
[Fact]
public async Task MeetingTile_FlowsInlineWithoutTopWrapper()
{
    var html = await GetAsync($"/dashboard/meetings-panel?asUser={Fixture.AdminOsobaId}");
    html.Should().NotContain("dashboard-meeting-item-top", "C2: dlaždice jednání už nemá blokový top wrapper");
    // pořadí: kód → badge → titulek → meta (regex přes celou dlaždici)
    Regex.IsMatch(html,
        "dashboard-meeting-item[\\s\\S]*?dashboard-meeting-code[\\s\\S]*?badge[\\s\\S]*?dashboard-meeting-title[\\s\\S]*?dashboard-item-meta",
        RegexOptions.Singleline).Should().BeTrue();
}

[Fact]
public async Task MeetingTile_KeepsAllData()
{
    var html = WebUtility.HtmlDecode(await GetAsync($"/dashboard/meetings-panel?asUser={Fixture.AdminOsobaId}"));
    html.Should().Contain("Jednání č.");
    Regex.IsMatch(html, @"\d{2}\.\d{2}\.\d{4}").Should().BeTrue("datum zůstává");
    Regex.IsMatch(html, @"\d{2}:\d{2}").Should().BeTrue("čas zůstává");
}
```

- [ ] **Step 2: Run → FAIL (top wrapper dnes existuje)**

- [ ] **Step 3: Markup**

```razor
<a class="dashboard-meeting-item" href="@item.DetailUrl">
    <strong class="dashboard-meeting-code">@item.ProjectCode</strong>
    <span class="badge">@item.Meeting.Stav</span>
    <span class="dashboard-meeting-title">Jednání č. @item.Meeting.CisloJednani</span>
    <span class="dashboard-item-meta">
        <span>@item.Meeting.Datum.ToString("dd.MM.yyyy")</span>
        <span>@item.Meeting.CasZacatek.ToString("HH\\:mm")</span>
        <span>@item.ProjectName</span>
    </span>
</a>
```

(`.dashboard-meeting-code` je nová třída jen pro čitelnost markup — bez CSS;
`<strong>` dědí bold. Empty-stav `<p class="muted">` beze změny — na něm stojí
`:has` detekce.)

- [ ] **Step 4: Testy zelené; `dotnet build`**

### Task 4: Živé ověření (primární akceptace) + regrese

- [ ] **Step 1: Příprava dat — news nesmí být prázdné + přesně 3 budoucí jednání**

Dev DB: dočasně posunout jednání tak, ať budoucí jsou právě 3 (UPDATE
datum_planovane na minulost u přebytečných, vč. reprodukčního č. 907 —
to rovnou SMAZAT vč. účastí, viz úklid z analýzy); vložit 2 syntetické
audit řádky (actor 3, entity_type='zaznam' create/update na existující
záznam) pro plné novinky. Po ověření: audit řádky smazat, datumy vrátit.

- [ ] **Step 2: Playwright měření (headless, viewporty 2560×1440 a 1440×900)**

Asserty ve scriptu:
- všechny 3 dlaždice jednání plně viditelné bez scrollu v panelu
  (`tile.bottom <= list.bottom` pro každou),
- výška meetings panelu ≤ 52 % výšky railu, když news mají obsah,
- na 2560: výška dlaždice < 2× line-height (= 1 řádka); na 1440: ≤ 2 řádky,
- prázdné novinky (dočasně smazat syntetické řádky) → meetings panel > 60 % railu,
- screenshoty `c2-rail-27.png`, `c2-rail-13.png` pro finální posouzení uživatelem.

- [ ] **Step 3: Regrese — Unit Layout celé (A2/B1/B7 piny), Api Dashboard* + meetings-panel, E2E dashboard třídy jsou-li. Známé pre-existing výjimky platí.**

- [ ] **Step 4: Úklid dev dat ověřit (žádné syntetické audit řádky, jednání 907 pryč, datumy vrácené).**

### Task 5: Držený commit

- [ ] Commit message: `feat(dashboard): C2 — rail 50/50 strop + flex-wrap dlaždice jednání`
