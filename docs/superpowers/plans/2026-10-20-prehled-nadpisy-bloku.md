# Přehled — nadpisy bloků — plán implementace

> **Pro agentní exekutory:** POVINNÝ SUB-SKILL: použij superpowers:executing-plans (inline, po úkolech, bez subagentů). Kroky používají checkboxy (`- [ ]`). Commity do větve `feat/prehled-nadpisy`, na konci merge do lokálního `main`, bez pushe.

**Cíl:** Tři bloky osobního Přehledu (`/Dashboard`) dostanou kompaktní nadpis (32 px), který nezmenší počet viditelných karet záznamů.

**Architektura:** Texty jsou v jedné konstantní třídě `DashboardBlockTitles` a používají je titulky detailních stránek (`DashboardService`) i nadpisy bloků. Nadpisy `h2` jsou v kostře stránky `Views/Dashboard/Index.cshtml`, mimo líně načítaný obsah. Blok na nadpis odkazuje přes `aria-labelledby`. Stránka dostane skrytý `h1`. Vzhled řídí jedno CSS pravidlo `.dashboard-panel-heading` s pevnou velikostí.

**Tech Stack:** ASP.NET Core MVC + Razor, CSS (gov DS tokeny), xUnit + FluentAssertions, Api testy (`ApiSqlFixture`), E2E Playwright .NET.

**Spec:** `docs/superpowers/specs/2026-10-20-prehled-nadpisy-bloku-design.md`

**Odchylky od specu (vědomé, schválí uživatel s plánem):**
- Spec §5 počítá s „existujícími E2E testy layoutu Přehledu“. Žádné takové testy nejsou (`grep dashboard-` v `PmTracker.Tests.E2E/Scenarios` je prázdný). Plán proto přidává nový E2E test kompaktnosti (Task 3).
- Spec §5/§7 navrhuje unit test `PageTitle`. Plán ho nahrazuje Api testem detailních stránek (Task 1), který ověří skutečně vykreslený titulek. Je silnější a nepotřebuje zakládat `DashboardService` s mocky.

## Global Constraints

- Texty přesně: `Na co se soustředit`, `Nejbližší jednání`, `Co je nového`. Skrytý `h1`: `Přehled`.
- Id nadpisů: `dashboard-focus-heading`, `dashboard-meetings-heading`, `dashboard-news-heading`.
- Nadpis bloku: `font-size: 0.875rem`, `line-height: 1.25rem`, `font-weight: 700`, `padding: 0.5rem var(--d-pad-card) 0.25rem`, `margin: 0`, `flex: 0 0 auto`, `color: var(--text-primary)`. Výška řádku nadpisu je nejvýš 33 px (spec §4.3, měřeno 32 px).
- Jen gov tokeny (`AppCssColorTokenTests`). Žádné `:has`, `max-content` ani nová pravidla `[data-theme="dark"]` (Edge na i15).
- Pravidla `.dashboard-rail` (jednání strop 50 %, novinky zbytek) se nemění.
- Api HTML testy kotví na atributy. Český text se ověřuje přes `WebUtility.HtmlDecode` (Razor kóduje diakritiku na entity).
- Razor: žádný `@* *@` uvnitř tagu se TagHelpery.
- Unit a Api sady zelené. E2E se posuzuje porovnáním jmen proti baseline, ne počtem.
- Po dokončení nová sekce v `docs/k-otestovani.md`.

## Review Focus

1. **Stav během načítání a chyba načtení obsahu bloku.** Nadpis musí být vidět i nad skeletonem a i když fetch panelu selže, protože je v kostře mimo `data-dashboard-panel-content`. Task 2 test ověří, že `h2` předchází placeholder i obsah v `Index` HTML.
2. **Úzký displej (blokové skládání).** Nadpis zůstává nad každým blokem a nepřetéká. Task 3 E2E při 375 px ověří, že `scrollWidth <= clientWidth`.
3. **Prázdný blok jednání.** Nadpis a hláška, novinky si vezmou zbytek a nic se neořízne. Kontroluje ruční průchod v Task 3.
4. **Dlouhý seznam záznamů.** Seznam dál roluje uvnitř bloku a nadpis zůstává nahoře. Task 3 E2E ověří, že nadpis je nad seznamem a seznam má `overflow-y: auto`.
5. **Tmavý motiv.** Nadpis používá `--text-primary` a sám se přebarví. Task 3 ruční snímek a `AppCssColorTokenTests`.

---

## Struktura souborů

| Soubor | Změna |
|---|---|
| `PmTracker.Web/Services/Dashboard/DashboardBlockTitles.cs` | nový: konstanty textů |
| `PmTracker.Web/Services/Dashboard/DashboardService.cs:87,100,116` | `PageTitle` z konstant |
| `PmTracker.Web/Views/Dashboard/Index.cshtml` | skrytý `h1`, tři `h2`, `aria-labelledby` |
| `PmTracker.Web/wwwroot/css/site.css:640-641,7040` | `.dashboard-panel-heading`, `.app-sr-only`, aktualizace komentářů |
| `PmTracker.Tests.Api/Controllers/DashboardBlockHeadingsRenderTests.cs` | nový: render testy |
| `PmTracker.Tests.E2E/Scenarios/DashboardHeadingsScenariosTests.cs` | nový: kompaktnost, rolování, úzký displej |
| `docs/k-otestovani.md` | sekce 2 |

---

### Task 1: Jeden zdroj textů bloků

**Files:**
- Create: `PmTracker.Web/Services/Dashboard/DashboardBlockTitles.cs`
- Modify: `PmTracker.Web/Services/Dashboard/DashboardService.cs` (`BuildFocusListPageAsync`, `BuildMeetingsListPageAsync`, `BuildNewsListPageAsync`)
- Test: `PmTracker.Tests.Api/Controllers/DashboardBlockHeadingsRenderTests.cs`

**Interfaces:**
- Produces: `public static class DashboardBlockTitles { public const string Focus = "Na co se soustředit"; public const string Meetings = "Nejbližší jednání"; public const string News = "Co je nového"; }` v namespace `PmTracker.Web.Services.Dashboard`.

- [ ] **Step 1: Padající test detailních stránek**

```csharp
using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Services.Dashboard;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-10-20 Přehled — nadpisy bloků. Texty z jednoho zdroje (DashboardBlockTitles),
/// nadpisy v kostře stránky. Český text přes HtmlDecode (Razor kóduje diakritiku).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class DashboardBlockHeadingsRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public DashboardBlockHeadingsRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<string> GetDecodedAsync(string path)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var separator = path.Contains('?') ? '&' : '?';
        var response = await client.GetAsync($"{path}{separator}asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return WebUtility.HtmlDecode(html);
    }

    [Theory]
    [InlineData("/dashboard/focus", DashboardBlockTitles.Focus)]
    [InlineData("/dashboard/meetings", DashboardBlockTitles.Meetings)]
    [InlineData("/dashboard/news", DashboardBlockTitles.News)]
    public async Task DetailPage_TitleComesFromSharedBlockTitles(string path, string expectedTitle)
    {
        var html = await GetDecodedAsync(path);

        html.Should().Contain($"<title>{expectedTitle} - Zápiska</title>", "titulek detailní stránky = název bloku na Přehledu");
    }
}
```

(Layout skládá titulek jako `<title>@ViewData["Title"] - Zápiska</title>`, `_Layout.cshtml:25`.)

- [ ] **Step 2: Spusť, musí selhat na kompilaci**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~DashboardBlockHeadingsRenderTests" --nologo`
Expected: FAIL: `The name 'DashboardBlockTitles' does not exist`.

- [ ] **Step 3: Implementace**

`PmTracker.Web/Services/Dashboard/DashboardBlockTitles.cs`:

```csharp
namespace PmTracker.Web.Services.Dashboard;

/// <summary>Názvy bloků Přehledu — sdílí nadpis bloku i titulek jeho detailní stránky.</summary>
public static class DashboardBlockTitles
{
    public const string Focus = "Na co se soustředit";
    public const string Meetings = "Nejbližší jednání";
    public const string News = "Co je nového";
}
```

V `DashboardService.cs` nahraď v `BuildFocusListPageAsync`, `BuildMeetingsListPageAsync` a `BuildNewsListPageAsync`:
- `PageTitle = "Na co se soustředit",` → `PageTitle = DashboardBlockTitles.Focus,`
- `PageTitle = "Nejbližší jednání",` → `PageTitle = DashboardBlockTitles.Meetings,`
- `PageTitle = "Co je nového",` → `PageTitle = DashboardBlockTitles.News,`

(`DashboardService` je ve stejném namespace, `using` není potřeba.)

- [ ] **Step 4: Spusť, musí projít**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~DashboardBlockHeadingsRenderTests" --nologo`
Expected: PASS (3 testy).

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Dashboard/DashboardBlockTitles.cs PmTracker.Web/Services/Dashboard/DashboardService.cs PmTracker.Tests.Api/Controllers/DashboardBlockHeadingsRenderTests.cs
git commit -m "feat(prehled): názvy bloků z jednoho zdroje (DashboardBlockTitles)"
```

---

### Task 2: Nadpisy v kostře Přehledu + CSS

**Files:**
- Modify: `PmTracker.Web/Views/Dashboard/Index.cshtml`
- Modify: `PmTracker.Web/wwwroot/css/site.css` (komentář ř. 640–641 a 7040, nové pravidlo za `.dashboard-panel-placeholder`)
- Test: `PmTracker.Tests.Api/Controllers/DashboardBlockHeadingsRenderTests.cs`

**Interfaces:**
- Consumes: `DashboardBlockTitles.Focus/Meetings/News` (Task 1)
- Produces: DOM kotvy `h1.app-sr-only`, `h2.dashboard-panel-heading#dashboard-{focus|meetings|news}-heading`, `section[aria-labelledby=…]` (pro Task 3)

- [ ] **Step 1: Padající render testy Přehledu** (do `DashboardBlockHeadingsRenderTests`)

```csharp
    [Theory]
    [InlineData("focus", DashboardBlockTitles.Focus)]
    [InlineData("meetings", DashboardBlockTitles.Meetings)]
    [InlineData("news", DashboardBlockTitles.News)]
    public async Task Index_BlockHasHeading_LabelledBy_AndHeadingPrecedesLazyContent(string block, string expectedTitle)
    {
        var html = await GetDecodedAsync("/dashboard");

        var headingId = $"dashboard-{block}-heading";
        html.Should().Contain($"<h2 class=\"dashboard-panel-heading\" id=\"{headingId}\">{expectedTitle}</h2>");

        var sectionStart = html.IndexOf($"data-dashboard-panel=\"{block}\"", StringComparison.Ordinal);
        sectionStart.Should().BeGreaterThan(0);
        var sectionTagStart = html.LastIndexOf("<section", sectionStart, StringComparison.Ordinal);
        var sectionTag = html[sectionTagStart..html.IndexOf('>', sectionStart)];
        sectionTag.Should().Contain($"aria-labelledby=\"{headingId}\"");

        var headingAt = html.IndexOf($"id=\"{headingId}\"", StringComparison.Ordinal);
        var placeholderAt = html.IndexOf("data-dashboard-panel-placeholder", sectionStart, StringComparison.Ordinal);
        var contentAt = html.IndexOf("data-dashboard-panel-content", sectionStart, StringComparison.Ordinal);
        headingAt.Should().BeGreaterThan(sectionStart).And.BeLessThan(placeholderAt,
            "nadpis je v kostře nad skeletonem — vidět i během načítání a při chybě načtení obsahu");
        placeholderAt.Should().BeLessThan(contentAt);
    }

    [Fact]
    public async Task Index_HasVisuallyHiddenH1()
    {
        var html = await GetDecodedAsync("/dashboard");

        html.Should().Contain("<h1 class=\"app-sr-only\">Přehled</h1>");
    }
```

- [ ] **Step 2: Spusť, musí selhat**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~DashboardBlockHeadingsRenderTests" --nologo`
Expected: FAIL u 4 nových testů (`h2` / `h1` chybí). 3 testy z Task 1 PASS.

- [ ] **Step 3: Markup**

`Views/Dashboard/Index.cshtml`: na začátek souboru pod `@model` přidej `@using PmTracker.Web.Services.Dashboard`. Pak:
- hned za `<section class="dashboard-shell" data-dashboard-shell>` vlož `<h1 class="app-sr-only">Přehled</h1>`
- každá `<section class="dashboard-panel …">` dostane atribut `aria-labelledby` a jako první potomek `h2`:

```cshtml
        <section class="dashboard-panel dashboard-section--focus"
                 data-dashboard-panel="focus"
                 data-dashboard-panel-url="@Model.FocusPanelUrl"
                 aria-labelledby="dashboard-focus-heading">
            <h2 class="dashboard-panel-heading" id="dashboard-focus-heading">@DashboardBlockTitles.Focus</h2>
            <div class="dashboard-panel-placeholder" data-dashboard-panel-placeholder>
```

```cshtml
            <section class="dashboard-panel dashboard-section--meetings"
                     data-dashboard-panel="meetings"
                     data-dashboard-panel-url="@Model.MeetingsPanelUrl"
                     aria-labelledby="dashboard-meetings-heading">
                <h2 class="dashboard-panel-heading" id="dashboard-meetings-heading">@DashboardBlockTitles.Meetings</h2>
                <div class="dashboard-panel-placeholder" data-dashboard-panel-placeholder>
```

```cshtml
            <section class="dashboard-panel dashboard-section--news"
                     data-dashboard-panel="news"
                     data-dashboard-panel-url="@Model.NewsPanelUrl"
                     aria-labelledby="dashboard-news-heading">
                <h2 class="dashboard-panel-heading" id="dashboard-news-heading">@DashboardBlockTitles.News</h2>
                <div class="dashboard-panel-placeholder" data-dashboard-panel-placeholder>
```

JS se nemění: `wwwroot/js/modules/dashboard.js` (`loadDashboardPanel`) plní jen `content.innerHTML` (`[data-dashboard-panel-content]`) a přepíná placeholder. Nadpis jako sourozenec před nimi tedy po načtení i při chybě zůstává.

- [ ] **Step 4: CSS**

`site.css` ř. 640–641 nahraď komentář:

```css
/* .dashboard-panel-header (+ h2) a .dashboard-panel-count → odstraněno 2026-06-08
   (úspora místa). Od 2026-10-20 jen kompaktní nadpis .dashboard-panel-heading (spec
   2026-10-20 Přehled — nadpisy bloků): 32 px, počet viditelných karet se nemění. */
.dashboard-panel-heading {
    flex: 0 0 auto;
    margin: 0;
    padding: 0.5rem var(--d-pad-card) 0.25rem;
    font-size: 0.875rem;
    line-height: 1.25rem;
    font-weight: 700;
    color: var(--text-primary);
}

/* Jen pro čtečky obrazovky (skrytý h1 stránky). */
.app-sr-only {
    position: absolute;
    width: 1px;
    height: 1px;
    margin: -1px;
    padding: 0;
    overflow: hidden;
    clip: rect(0 0 0 0);
    white-space: nowrap;
    border: 0;
}
```

`site.css` ř. 7040 komentář `/* .dashboard-panel-header → odstraněno 2026-06-08. */` uprav na `/* .dashboard-panel-header → odstraněno 2026-06-08; nadpis bloku viz .dashboard-panel-heading. */`.

Pozn.: `--d-pad-card` je definovaný na `.dashboard-shell` (ř. ~6931), nadpis je jeho potomek, takže proměnná platí.

- [ ] **Step 5: Spusť render testy + CSS architekturu**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~DashboardBlockHeadingsRenderTests|FullyQualifiedName~DashboardMeetingTileRenderTests|FullyQualifiedName~HomeControllerTests" --nologo`
Expected: PASS.

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~Architecture" --nologo`
Expected: PASS (`AppCssColorTokenTests` atd.).

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Views/Dashboard/Index.cshtml PmTracker.Web/wwwroot/css/site.css PmTracker.Tests.Api/Controllers/DashboardBlockHeadingsRenderTests.cs
git commit -m "feat(prehled): kompaktní nadpisy bloků v kostře stránky + skrytý h1"
```

---

### Task 3: E2E kompaktnost, ruční průchod, k-otestovani, merge

**Files:**
- Test: `PmTracker.Tests.E2E/Scenarios/DashboardHeadingsScenariosTests.cs`
- Modify: `docs/k-otestovani.md`

**Interfaces:**
- Consumes: DOM kotvy z Task 2; `E2ETestFixture.BaseUrl`, `AdminOsobaId`, `NewPageAsync()`

- [ ] **Step 1: E2E test**

```csharp
using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Spec 2026-10-20 Přehled — nadpisy bloků: kompaktní (≤ 33 px), nad seznamem,
/// seznam dál roluje uvnitř bloku, na úzkém displeji nic nepřetéká.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class DashboardHeadingsScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public DashboardHeadingsScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private sealed record HeadingBox(string Block, double Height, double Bottom, double ContentTop);

    [Fact]
    public async Task Widescreen_NadpisyKompaktni_NadObsahem_SeznamRoluje()
    {
        var page = await _fixture.NewPageAsync();
        await page.SetViewportSizeAsync(1400, 900);
        await page.GotoAsync($"{_fixture.BaseUrl}/Dashboard?asUser={_fixture.AdminOsobaId}");
        await Assertions.Expect(page.Locator("[data-dashboard-panel='focus'] [data-dashboard-panel-content] .dashboard-panel-body"))
            .ToHaveCountAsync(1);

        var boxes = await page.EvaluateAsync<HeadingBox[]>(@"() =>
            ['focus', 'meetings', 'news'].map(block => {
                const section = document.querySelector(`[data-dashboard-panel='${block}']`);
                const h = section.querySelector('h2.dashboard-panel-heading').getBoundingClientRect();
                const c = section.querySelector('[data-dashboard-panel-content]').getBoundingClientRect();
                return { block, height: h.height, bottom: h.bottom, contentTop: c.top };
            })");

        foreach (var box in boxes)
        {
            box.Height.Should().BeLessThanOrEqualTo(33, $"nadpis bloku {box.Block} má zabrat nejvýš jeden kompaktní řádek");
            box.Bottom.Should().BeLessThanOrEqualTo(box.ContentTop + 0.5, $"nadpis bloku {box.Block} je nad obsahem");
        }

        var focusListOverflow = await page.EvaluateAsync<string?>(
            "() => { const l = document.querySelector('.dashboard-focus-list'); return l ? getComputedStyle(l).overflowY : null; }");
        if (focusListOverflow is not null)
        {
            focusListOverflow.Should().BeOneOf(new[] { "auto", "scroll" }, "seznam záznamů dál roluje uvnitř bloku");
        }

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task UzkyDisplej_NadpisyBezVodorovnehoPretekani()
    {
        var page = await _fixture.NewPageAsync();
        await page.SetViewportSizeAsync(375, 800);
        await page.GotoAsync($"{_fixture.BaseUrl}/Dashboard?asUser={_fixture.AdminOsobaId}");
        await Assertions.Expect(page.Locator("h2.dashboard-panel-heading")).ToHaveCountAsync(3);

        var overflow = await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth > document.documentElement.clientWidth");
        overflow.Should().BeFalse();

        await page.Context.CloseAsync();
    }
}
```

Pozn.: `.dashboard-panel-body` se vykreslí vždy, i bez položek. `.dashboard-focus-list` jen když má admin v E2E seedu fokusové záznamy. Proto: pokud `document.querySelector('.dashboard-focus-list')` vrátí `null`, test kontrolu rolování vynechá (`if (focusListOverflow is not null)`), zapíše se ruling a rolování ověří ruční průchod (Step 4). Výška a pořadí nadpisů se testují vždy.

- [ ] **Step 2: Spusť E2E (RED proti main by šel jen bez Task 2; tady stačí GREEN)**

Run: `dotnet test PmTracker.Tests.E2E --filter "FullyQualifiedName~DashboardHeadingsScenariosTests" --nologo`
Expected: PASS (2 testy). Že test umí selhat, ověř mutací: dočasně změň v `site.css` `font-size: 0.875rem` na `1.5rem`. Test `Widescreen_…` musí spadnout na výšce. Pak vrať.

- [ ] **Step 3: Celá E2E sada proti baseline**

Run: celá `dotnet test PmTracker.Tests.E2E` na větvi a v throwaway worktree na `main`, porovnání jmen selhání Pythonem z TRX (vzor z plánu Pohled jako, Task 8).
Expected: žádné nové jméno.

- [ ] **Step 4: Ruční průchod (Playwright skript proti lokální app 5071, `?asUser=1`)**

- 1400×900 a 1920×1080: počet celých karet v levém bloku = 5 / 6 (spec §4.4), snímky,
- tmavý motiv: nadpisy čitelné,
- 375 px: snímek, nadpisy nad bloky,
- skeleton: zpomal síť (`page.route` na `/dashboard/*-panel` se zpožděním 2 s), snímek: nadpis nad skeletonem,
- prázdná jednání (pokud lokální data jednání mají, ověř aspoň vizuálně rozložení jednání/novinky beze změny proti snímku před změnou).

- [ ] **Step 5: Plné sady**

Run: `dotnet test PmTracker.Tests.Unit --nologo && dotnet test PmTracker.Tests.Api --nologo`
Expected: PASS (Unit ≥ 2223, Api ≥ 473 + nové).

- [ ] **Step 6: Zápis do `docs/k-otestovani.md`** (nová sekce 2)

```markdown
## 2. Přehled — nadpisy bloků

**Hotovo 2026-10-20:** spec `docs/superpowers/specs/2026-10-20-prehled-nadpisy-bloku-design.md`, plán `docs/superpowers/plans/2026-10-20-prehled-nadpisy-bloku.md`.

**Co to dělá:** tři bloky Přehledu mají kompaktní nadpis — „Na co se soustředit“, „Nejbližší jednání“, „Co je nového“ (stejné texty jako titulky stránek „Zobrazit více“). Nadpis zabere 32 px; počet viditelných karet záznamů se nezměnil.

**Otestovat:**
- [ ] Přehled na i15 / Edge: tři nadpisy, malé tučné písmo, nezabírají víc než jeden řádek.
- [ ] Levý blok: vejde se stejný počet karet záznamů jako dřív; seznam roluje, nadpis zůstává nahoře.
- [ ] Pravý sloupec: jednání a novinky se dělí výškou jako dřív, nic se neořezává.
- [ ] Při načítání stránky je nadpis vidět už nad šedým skeletonem.
- [ ] Tmavý motiv a úzké okno: nadpisy čitelné, nic nepřetéká.
- [ ] „Zobrazit více“ u bloku → titulek stránky i drobečková navigace mají stejný název jako nadpis bloku.
```

- [ ] **Step 7: Commit + merge**

```bash
git add PmTracker.Tests.E2E/Scenarios/DashboardHeadingsScenariosTests.cs docs/k-otestovani.md
git commit -m "test(prehled): E2E kompaktnost nadpisů bloků; k-otestovani"
git checkout main
git merge --no-ff feat/prehled-nadpisy -m "Merge feat/prehled-nadpisy into main"
git branch -d feat/prehled-nadpisy
```
