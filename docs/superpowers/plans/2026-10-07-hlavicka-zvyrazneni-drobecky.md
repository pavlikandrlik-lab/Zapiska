# Hlavní navigace bez podtržení, zvýrazněný Přehled, drobečky jen při zanoření — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Pozn. pro tento projekt:** uživatel chce **inline exekuci** v hlavní session (superpowers:executing-plans), subagenti se neosvědčili. Hotová a otestovaná práce končí v lokálním `main`, push jen na pokyn.

**Goal:** Vybraná položka hlavní navigace je zvýrazněná podbarvením bez podtržení. Zvýrazní se i Přehled. Drobečková lišta se ukáže jen na stránkách, které mají nadřazenou úroveň.

**Architecture:** Zvýraznění řeší jedno pravidlo v `site.css` nad `aria-current="page"`, které už `_Layout.cshtml` vypisuje; zruší se jen výjimka pro dashboard. O zobrazení drobečků rozhoduje vlastnost modelu `BreadcrumbTrail.IsNested` (aspoň dva drobečky), kterou čte jediný partial `_BreadcrumbBar`. Controllery dál deklarují své drobečky beze změny. Čára pod hlavičkou přechází z lišty na `gov-header`, aby zůstala i na stránkách bez lišty.

**Tech Stack:** .NET 8, ASP.NET Core MVC + Razor, DS gov.cz 4.7.0 (`gov-navigation`, tokeny), xUnit + FluentAssertions (Unit), Testcontainers SQL (Api), headless Chrome na snímky obrazovky.

**Spec:** samostatný spec není (ohraničená úprava). Návrh odsouhlasený uživatelem v chatu 2026-10-07:
1. Vybraná záložka (Přehled, Projekty, Osoby…) bez modrého podtržení se zaoblenými konci. Zvýrazněná zůstane podbarvením, o stupeň sytějším než hover (token stisknutého tlačítka DS).
2. Přehled se zvýrazní taky — na hlavní stránce i na stránkách „zobrazit více“.
3. Drobečky se nezobrazí při zanoření 0: Přehled, Projekty, Osoby, Nastavení, Můj profil, a stejným pravidlem Jednání, Číselníky, Hledání, strom Dokumentace, SD konektor. Zobrazí se všude, kde je rodič: konkrétní projekt, jednání, záznam, návrh, „zobrazit více“ v Přehledu, konkrétní číselník, stránka dokumentace.
4. Bez lišty se obsah nesmí nalepit na menu — čára pod menu zůstane.
5. Upravit testy, které hlídají opak, a odchylky č. 4 a 6 v `docs/known-issues/ds-fis-odchylky.md`.

## Global Constraints

- **Soubory DS se nikdy needitují, bez výjimky:** `PmTracker.Web/wwwroot/assets/gov/**`. Mění se jen `site.css`, `_Layout.cshtml`, `_BreadcrumbBar.cshtml`, `Breadcrumbs.cs`, `BaseController.Breadcrumbs.cs`, testy a dokument odchylek.
- **Jen tokeny DS**, žádné natvrdo zapsané barvy v nových pravidlech navigace.
- **`site.css` nesmí obsahovat selektory staré hlavičky** (`.app-header`, `.app-nav`, … — hlídá `LayoutGovHeaderTests.SiteCss_NemaSelektoryStareHlavicky`). Nová pravidla jdou na `.gov-header` a `.app-main-nav`.
- **Razor kóduje diakritiku na entity** → Api testy kotvit na atributy a ASCII (`href="/"`, `class="app-breadcrumb-bar"`), český text přes `HtmlEncoder.Default.Encode(...)`.
- **Výstup testů je česky** („Úspěšné!“, „Neúspěšné!“) — grepovat podle toho.
- **Baseline Api:** 4 známá selhání harmonogramu, která s touto prací nesouvisí: `RecordEditorControllerTests.Edit_ShouldRenderScheduleMiniGantt_WithAlignedAxis_WithoutPerStepDuplicateBars` a 3× `ProjectHarmonogramRenderTests.Detail_*`. Jiné selhání je regrese.
- **Git:** práce na větvi `hlavicka-zvyrazneni-drobecky` z `main`, na konci merge do `main` („Merge hlavicka-zvyrazneni-drobecky into main“), větev smazat. Push jen na výslovný pokyn, force-push nikdy.
- **Commit trailer:** `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Najetí myší na vybranou položku** — DS má `.gov-navigation>ul>li>a:hover` se specifičností (0,2,3), vyšší než `.app-main-nav a[aria-current="page"]` (0,2,1). Bez vlastního `:hover` by vybraná položka pod myší zesvětlala na barvu hoveru. Pokrývá test v Tasku 1 (blok musí obsahovat i `:hover`).
2. **Rozbalené mobilní menu (< 48em)** — zvýraznění musí platit i tam, pravidlo proto nesmí být uvnitř `@media`. Pokrývá test v Tasku 1 (`Block` hledá selektor na začátku řádku, tj. mimo `@media`).
3. **Tmavý motiv** — podbarvení přes token, který DS přepíná s motivem (`--color-primary-200` světlý / `--color-primary-900` tmavý). Pokrývá test v Tasku 1 a snímky v Tasku 4.
4. **Podstránky Přehledu (`/Dashboard/Focus`)** — Přehled zvýrazněný a drobečky viditelné zároveň. Pokrývají testy v Taskách 2 a 3.
5. **Jeden drobeček s explicitním `BackUrl`** — lišta se neukáže a nezůstane ani osamocená šipka ←. Pokrývá unit test v Tasku 3.

---

## File Structure

| Soubor | Změna | Odpovědnost |
|---|---|---|
| `PmTracker.Web/wwwroot/css/site.css` | Modify ~377–409, ~587–597 | Vzhled vybrané položky; čára pod hlavičkou místo horní čáry lišty |
| `PmTracker.Web/Views/Shared/_Layout.cshtml` | Modify 76–81 | Zrušit výjimku, která nezvýrazňuje Přehled |
| `PmTracker.Web/Models/ViewModels/Breadcrumbs.cs` | Modify | `BreadcrumbTrail.IsNested` — pravidlo zanoření na jednom místě |
| `PmTracker.Web/Views/Shared/_BreadcrumbBar.cshtml` | Modify řádek 2 | Vykreslit lištu jen při `IsNested` |
| `PmTracker.Web/Controllers/BaseController.Breadcrumbs.cs` | Modify komentář `SetSectionRootBreadcrumb` | Dokumentace chování |
| `docs/known-issues/ds-fis-odchylky.md` | Modify odchylky č. 4 a 6 | Popis odchylky odpovídá kódu |
| `PmTracker.Tests.Unit/Layout/LayoutGovHeaderTests.cs` | Modify | CSS vybrané položky a čáry pod hlavičkou |
| `PmTracker.Tests.Unit/Layout/BreadcrumbTrailTests.cs` | Modify | `IsNested` |
| `PmTracker.Tests.Unit/Layout/BreadcrumbBarMarkupTests.cs` | Modify | Partial se řídí `IsNested` |
| `PmTracker.Tests.Api/Controllers/LayoutGovHeaderRenderTests.cs` | Modify | Přehled zvýrazněný |
| `PmTracker.Tests.Api/Controllers/BreadcrumbPhase2RenderTests.cs` | Modify | Kořeny sekcí bez lišty, podstránky s lištou |
| `PmTracker.Tests.Api/Controllers/BreadcrumbCoverageTests.cs` | Modify | Hledání bez lišty |

---

### Task 0: Větev a baseline

- [ ] **Step 1: Založit větev**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
git status --short          # musí být čisto
git checkout -b hlavicka-zvyrazneni-drobecky main
```

- [ ] **Step 2: Baseline testů**

```bash
dotnet test PmTracker.Tests.Unit 2>&1 | tail -1
dotnet test PmTracker.Tests.Api 2>&1 | grep -E "^\s+Neúspěšné |Úspěšné!|Neúspěšné!"
```

Expected: Unit bez selhání; Api jen 4 baseline selhání z Global Constraints. Počty si zapiš.

---

### Task 1: Vybraná položka navigace — podbarvení bez podtržení

**Files:**
- Modify: `PmTracker.Web/wwwroot/css/site.css` (blok „Aktivní položka hlavní navigace“ až konec `@media (min-width: 48em)` odchylky č. 6, dnes ~377–409)
- Modify: `docs/known-issues/ds-fis-odchylky.md` (odchylky č. 4 a 6)
- Test: `PmTracker.Tests.Unit/Layout/LayoutGovHeaderTests.cs`

**Interfaces:**
- Consumes: `aria-current="page"` na odkazu v `<nav class="gov-navigation app-main-nav">` (už existuje v `_Layout.cshtml`).
- Produces: pravidlo `.app-main-nav a[aria-current="page"],\n.app-main-nav a[aria-current="page"]:hover` v `site.css`.

- [ ] **Step 1: Napsat padající test**

V `LayoutGovHeaderTests.cs` nahradit celý test `AktivniPolozkaNavigace_MaVTmavemMotivuKontrastniPodtrzeni` tímto:

```csharp
    [Fact]
    public void AktivniPolozkaNavigace_JePodbarvenaBezPodtrzeni()
    {
        // Odchylka č. 4 (uživatel 2026-10-07): bez podtržení, jen podbarvení tokenem stisknutého
        // tlačítka — o stupeň sytější než hover, aby šla odlišit od položky pod myší. Selektor
        // na začátku řádku = mimo @media, platí tedy i v rozbaleném mobilním menu. :hover drží
        // barvu, jinak by ji přebil hover DS (.gov-navigation>ul>li>a:hover má vyšší specifičnost).
        var css = SiteCssBezKomentaru();
        var block = Block(css,
            ".app-main-nav a[aria-current=\"page\"],\n.app-main-nav a[aria-current=\"page\"]:hover");
        block.Should().Contain("background-color: var(--button-outlined-primary-active)");
        css.Should().NotMatchRegex(@"aria-current=""page""\][^{]*\{[^}]*box-shadow",
            "podtržení vybrané položky je zrušené");
    }
```

- [ ] **Step 2: Spustit test, ověřit selhání**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~AktivniPolozkaNavigace_JePodbarvenaBezPodtrzeni"`
Expected: FAIL — „blok … má existovat“ (selektor se `:hover` zatím neexistuje).

- [ ] **Step 3: Upravit CSS**

V `site.css` nahradit celý úsek od komentáře `/* Aktivní položka hlavní navigace — odchylka č. 4` po konec bloku `@media (min-width: 48em) { … }` odchylky č. 6 (končí před komentářem `/* Kontakty v patičce`) tímto:

```css
/* Aktivní položka hlavní navigace — odchylka č. 4 (docs/known-issues/ds-fis-odchylky.md):
   vanilla gov-navigation aktuální stránku vizuálně neoznačuje. Podbarvení tokenem stisknutého
   tlačítka DS, o stupeň sytější než hover (--button-outlined-primary-hover), aby šla vybraná
   položka odlišit od položky pod myší. Bez podtržení (uživatel 2026-10-07). :hover drží barvu —
   hover DS (.gov-navigation>ul>li>a:hover) má vyšší specifičnost a vybranou položku by zesvětlil. */
.app-main-nav a[aria-current="page"],
.app-main-nav a[aria-current="page"]:hover {
    background-color: var(--button-outlined-primary-active);
}

/* Odchylka č. 6: nižší hlavní navigace (25 px místo 48 px). Výšku položek dává token
   --height-component-l, který sdílí všechny komponenty velikosti L — proto se přepisuje
   jen uvnitř navigace a jen na desktopu; v mobilním menu zůstávají dotykové cíle 48 px. */
@media (min-width: 48em) {
    .app-main-nav {
        --height-component-l: 25px;
    }
}
```

- [ ] **Step 4: Spustit test, ověřit průchod**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~LayoutGovHeaderTests"`
Expected: PASS všech testů třídy.

- [ ] **Step 5: Přepsat odchylky č. 4 a 6**

V `docs/known-issues/ds-fis-odchylky.md` nahradit odstavec **Jak je odchylka provedena** u č. 4 tímto:

```markdown
**Jak je odchylka provedena:** `_Layout.cshtml` dává aktivnímu odkazu `aria-current="page"`
(přístupnost, v souladu s DS), a to i Přehledu. Vizuál řeší `.app-main-nav a[aria-current="page"]`
v `site.css` (třída `app-main-nav` na `<nav class="gov-navigation">`), jen tokeny DS: podbarvení
`--button-outlined-primary-active` (stisknuté tlačítko; světlý motiv `--color-primary-200`,
tmavý `--color-primary-900`), o stupeň sytější než hover, aby šla vybraná položka odlišit od
položky pod myší. Stejná barva platí i pro `:hover` vybrané položky. Podtržení bylo do
2026-10-07; uživatel ho nechce (zaoblené konce podle rohů odkazu), zvýraznění podbarvením ano.
```

U č. 6 smazat poslední odstavec (začíná „Řádek textu položky (27 px) je pak vyšší než položka.“) — podtržení už neexistuje.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/wwwroot/css/site.css PmTracker.Tests.Unit/Layout/LayoutGovHeaderTests.cs docs/known-issues/ds-fis-odchylky.md
git commit -m "$(cat <<'EOF'
style(navigace): vybraná položka podbarvená bez podtržení

Podtržení (box-shadow) kopírovalo zaoblené rohy odkazu. Vybraná položka
má teď jen podbarvení tokenem stisknutého tlačítka DS, o stupeň sytější
než hover; :hover barvu drží. Odchylky č. 4 a 6 přepsané.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Zvýraznit Přehled

**Files:**
- Modify: `PmTracker.Web/Views/Shared/_Layout.cshtml:76-81`
- Test: `PmTracker.Tests.Api/Controllers/LayoutGovHeaderRenderTests.cs`

**Interfaces:**
- Consumes: lokální funkce `NavCurrent(string controller)` v `_Layout.cshtml`; odkaz Přehledu `<a href="@Url.Action("Index", "Dashboard")" aria-current="@NavCurrent("dashboard")">`. `Url.Action("Index","Dashboard")` vrací `/` (výchozí route `{controller=Dashboard}/{action=Index}/{id?}`).
- Produces: nic nového.

- [ ] **Step 1: Napsat padající test**

V `LayoutGovHeaderRenderTests.cs` nahradit test `Dashboard_NezvyraznujeZadnouPolozku` tímto:

```csharp
    [Theory]
    [InlineData("/")]
    [InlineData("/Dashboard/Focus")]
    public async Task Prehled_JeZvyraznenyNaHlavniStranceIPodstrankach(string path)
    {
        var nav = NavSegment(await GetAsync(path, _fixture.AdminOsobaId));

        Regex.Matches(nav, "aria-current=\"page\"").Should().HaveCount(1);
        nav.Should().Contain("<a href=\"/\" aria-current=\"page\">",
            "Přehled se zvýrazňuje jako ostatní sekce (uživatel 2026-10-07)");
    }
```

- [ ] **Step 2: Spustit test, ověřit selhání**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~Prehled_JeZvyraznenyNaHlavniStranceIPodstrankach"`
Expected: FAIL 2× — nalezeno 0 výskytů `aria-current="page"`.

- [ ] **Step 3: Zrušit výjimku v layoutu**

V `_Layout.cshtml` nahradit:

```cshtml
        var suppressActiveNavigation = string.Equals(currentController, "dashboard", System.StringComparison.OrdinalIgnoreCase);
        // aria-current="page" pro položku aktuální sekce; null = Razor atribut nevypíše.
        // Na dashboardu se nezvýrazňuje nic (stejně jako dřív NavClass).
        string? NavCurrent(string controller) => !suppressActiveNavigation && currentController == controller ? "page" : null;
```

tímto:

```cshtml
        // aria-current="page" pro položku aktuální sekce, i pro Přehled (uživatel 2026-10-07);
        // null = Razor atribut nevypíše.
        string? NavCurrent(string controller) => currentController == controller ? "page" : null;
```

- [ ] **Step 4: Spustit testy, ověřit průchod**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~LayoutGovHeaderRenderTests"`
Expected: PASS všech testů třídy (včetně `AktivniPolozka_MaAriaCurrent_PraveJednou` na `/Projekty`).

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Views/Shared/_Layout.cshtml PmTracker.Tests.Api/Controllers/LayoutGovHeaderRenderTests.cs
git commit -m "$(cat <<'EOF'
feat(navigace): Přehled se zvýrazňuje jako ostatní sekce

Výjimka pro dashboard zrušena; Přehled má aria-current na hlavní
stránce i na podstránkách „zobrazit více“.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Drobečky jen při zanoření, čára pod hlavičkou

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Breadcrumbs.cs`
- Modify: `PmTracker.Web/Views/Shared/_BreadcrumbBar.cshtml:2`
- Modify: `PmTracker.Web/Controllers/BaseController.Breadcrumbs.cs` (komentář `SetSectionRootBreadcrumb`)
- Modify: `PmTracker.Web/wwwroot/css/site.css` (`.app-breadcrumb-bar`, nové `.gov-header`)
- Test: `PmTracker.Tests.Unit/Layout/BreadcrumbTrailTests.cs`, `PmTracker.Tests.Unit/Layout/BreadcrumbBarMarkupTests.cs`, `PmTracker.Tests.Unit/Layout/LayoutGovHeaderTests.cs`, `PmTracker.Tests.Api/Controllers/BreadcrumbPhase2RenderTests.cs`, `PmTracker.Tests.Api/Controllers/BreadcrumbCoverageTests.cs`

**Interfaces:**
- Consumes: `BreadcrumbTrail(IReadOnlyList<Breadcrumb> Items, string? BackUrl = null)`, `ParentUrl`.
- Produces: `public bool IsNested => Items.Count >= 2;` na `BreadcrumbTrail`.

- [ ] **Step 1: Napsat padající unit testy**

Do `BreadcrumbTrailTests.cs` přidat:

```csharp
    /// <summary>Uživatel 2026-10-07: lišta jen při zanoření — kořen sekce sám o sobě ne.</summary>
    [Fact]
    public void IsNested_False_ForSingleRoot()
    {
        new BreadcrumbTrail(new[] { new Breadcrumb("Osoby", null, null, false) })
            .IsNested.Should().BeFalse();
    }

    [Fact]
    public void IsNested_False_ForSingleRoot_EvenWithBackUrl()
    {
        // Jinak by zůstala osamocená šipka ← bez drobečků.
        new BreadcrumbTrail(new[] { new Breadcrumb("Osoby", null, null, false) }, "/Projekty")
            .IsNested.Should().BeFalse();
    }

    [Fact]
    public void IsNested_True_WhenParentExists()
    {
        new BreadcrumbTrail(new[]
        {
            new Breadcrumb("Přehled", "/", null, false),
            new Breadcrumb("Moje úkoly", null, null, true),
        }).IsNested.Should().BeTrue();
    }
```

V `BreadcrumbBarMarkupTests.cs` přidat:

```csharp
    [Fact]
    public void Partial_RendersOnlyNestedTrail()
    {
        var cshtml = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Shared/_BreadcrumbBar.cshtml"));
        cshtml.Should().Contain("@if (Model is { IsNested: true })",
            "zanoření 0 (kořen sekce) lištu nemá — uživatel 2026-10-07");
    }
```

V `LayoutGovHeaderTests.cs` přidat:

```csharp
    [Fact]
    public void CaraPodMenu_JeNaHlavicce_NeNaDrobeckoveListe()
    {
        // Kořeny sekcí nemají drobečkovou lištu (uživatel 2026-10-07); čára pod menu proto
        // patří hlavičce, jinak by obsah splynul s menu. Lišta si nechává jen spodní čáru.
        var css = SiteCssBezKomentaru();
        Block(css, ".gov-header").Should().Contain("border-bottom: 1px solid var(--pm-border)");
        Block(css, ".app-breadcrumb-bar").Should().NotContain("border-top");
    }
```

- [ ] **Step 2: Spustit unit testy, ověřit selhání**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~BreadcrumbTrailTests|FullyQualifiedName~BreadcrumbBarMarkupTests|FullyQualifiedName~CaraPodMenu"`
Expected: build error `'BreadcrumbTrail' does not contain a definition for 'IsNested'` (RED pro `IsNested_*`). Že padají i `Partial_RendersOnlyNestedTrail` a `CaraPodMenu_…`, se ověří po kroku 3: po přidání modelu (krok 3) a před kroky 4–6 je spustit a očekávat FAIL obou („blok .gov-header má existovat“, chybějící `IsNested: true` v partialu).

- [ ] **Step 3: Model**

V `Breadcrumbs.cs` do `BreadcrumbTrail` za `ParentUrl` přidat:

```csharp
    /// <summary>Lišta se zobrazí jen při zanoření — aspoň dva drobečky, tedy stránka s rodičem.
    /// Kořen sekce (Přehled, Projekty, Osoby, Můj profil…) ji nemá (uživatel 2026-10-07).</summary>
    public bool IsNested => Items.Count >= 2;
```

- [ ] **Step 4: Partial**

V `_BreadcrumbBar.cshtml` nahradit řádek 2:

```cshtml
@if (Model is { Items.Count: > 0 })
```

tímto:

```cshtml
@if (Model is { IsNested: true })
```

- [ ] **Step 5: Komentář kořenového drobečku**

V `BaseController.Breadcrumbs.cs` nahradit:

```csharp
    /// <summary>Top-level seznam sekce (např. „Projekty") — jediný kořenový drobeček, bez ✕/←.</summary>
```

tímto:

```csharp
    /// <summary>Top-level seznam sekce (např. „Projekty") — jediný kořenový drobeček. Lišta se
    /// při zanoření 0 nevykreslí (<see cref="BreadcrumbTrail.IsNested"/>, uživatel 2026-10-07);
    /// volání zůstává, aby každá stránka deklarovala své místo v navigaci.</summary>
```

- [ ] **Step 6: Čára pod hlavičkou**

V `site.css` v bloku `.app-breadcrumb-bar { … }` smazat řádek `    border-top: 1px solid var(--pm-border);` a nad komentář `/* Drobečková lišta (frame bar pod hlavičkou)` vložit:

```css
/* Čára pod hlavní navigací. Dřív ji kreslila drobečková lišta (border-top); kořeny sekcí
   ale lištu nemají (uživatel 2026-10-07) a obsah by splynul s menu. */
.gov-header {
    border-bottom: 1px solid var(--pm-border);
}

```

- [ ] **Step 7: Spustit unit testy, ověřit průchod**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~Layout"`
Expected: PASS.

- [ ] **Step 8: Upravit Api testy (padající)**

V `BreadcrumbPhase2RenderTests.cs` nahradit test `SectionLanding_RendersRootBreadcrumb` a `CiselnikyIndex_RendersRootCrumb` tímto:

```csharp
    /// <summary>Uživatel 2026-10-07: kořen sekce (zanoření 0) drobečkovou lištu nemá.</summary>
    [Theory]
    [InlineData("/")]
    [InlineData("/Dashboard")]
    [InlineData("/Projekty")]
    [InlineData("/Osoby")]
    [InlineData("/Nastaveni")]
    [InlineData("/Profil")]
    [InlineData("/Jednani")]
    [InlineData("/Ciselniky")]
    [InlineData("/Dokumentace/Technicka/Strom-dokumentace")]
    public async Task SectionLanding_HasNoBreadcrumbBar(string path)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"{path}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().NotContain("class=\"app-breadcrumb-bar\"");
    }

    [Fact]
    public async Task CiselnikDetail_RendersUnderCiselniky()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Ciselniky/Detail/typy-ukolu?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("class=\"app-breadcrumb-bar\"");
        html.Should().Contain("app-breadcrumb-back");
        html.Should().Contain(HtmlEncoder.Default.Encode("Číselníky"));
    }
```

V témž souboru nahradit test `DocumentationTree_RendersRootCrumb_AndDropsLocalBreadcrumb` tímto:

```csharp
    [Fact]
    public async Task DocumentationTree_HasNoBreadcrumbBar_AndDropsLocalBreadcrumb()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Dokumentace/Technicka/Strom-dokumentace?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().NotContain("class=\"app-breadcrumb-bar\"", "strom dokumentace je kořen sekce");
        html.Should().NotContain("docs-breadcrumb");   // lokální breadcrumb nahrazen frame lištou
    }
```

Testy `DashboardSubpage_RendersUnderPrehled` a `DocumentationPage_RendersUnderDokumentace_AndDropsLocalBreadcrumb` zůstávají beze změny (lišta tam má být).

V `BreadcrumbCoverageTests.cs` nahradit `SearchPage_RendersBreadcrumbBar` tímto:

```csharp
    [Fact]
    public async Task SearchPage_HasNoBreadcrumbBar()
    {
        // Uživatel 2026-10-07: Hledání je zanoření 0, lištu nemá.
        var html = await GetOkAsync($"/Search?q=test&asUser={_fixture.AdminOsobaId}");
        html.Should().NotContain("class=\"app-breadcrumb-bar\"");
    }
```

- [ ] **Step 9: Ověřit, že Api testy chytí chybu**

Dočasně vrátit řádek 2 partialu na `@if (Model is { Items.Count: > 0 })`, spustit:

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~BreadcrumbPhase2RenderTests|FullyQualifiedName~BreadcrumbCoverageTests"`
Expected: FAIL `SectionLanding_HasNoBreadcrumbBar` (9×), `DocumentationTree_HasNoBreadcrumbBar_…`, `SearchPage_HasNoBreadcrumbBar`. Partial vrátit na `@if (Model is { IsNested: true })`.

- [ ] **Step 10: Spustit Api testy drobečků, ověřit průchod**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~Breadcrumb|FullyQualifiedName~DocumentationNavigationTests"`
Expected: PASS (včetně `BreadcrumbRenderTests`, `BreadcrumbBackNavigationRenderTests` — projekt, jednání, záznam mají lištu dál).

- [ ] **Step 11: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/Breadcrumbs.cs PmTracker.Web/Views/Shared/_BreadcrumbBar.cshtml PmTracker.Web/Controllers/BaseController.Breadcrumbs.cs PmTracker.Web/wwwroot/css/site.css PmTracker.Tests.Unit/Layout/BreadcrumbTrailTests.cs PmTracker.Tests.Unit/Layout/BreadcrumbBarMarkupTests.cs PmTracker.Tests.Unit/Layout/LayoutGovHeaderTests.cs PmTracker.Tests.Api/Controllers/BreadcrumbPhase2RenderTests.cs PmTracker.Tests.Api/Controllers/BreadcrumbCoverageTests.cs
git commit -m "$(cat <<'EOF'
feat(drobecky): lišta jen při zanoření, čára pod menu na hlavičce

Kořeny sekcí (Přehled, Projekty, Osoby, Nastavení, Můj profil, Jednání,
Číselníky, Hledání, strom Dokumentace, SD konektor) drobečkovou lištu
nemají; stránky s rodičem ano. Pravidlo je BreadcrumbTrail.IsNested,
čte ho jen _BreadcrumbBar. Čára pod menu přešla z lišty na gov-header,
aby obsah kořenových stránek nesplynul s menu.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Vizuální ověření, celé sady, merge do main

- [ ] **Step 1: Spustit aplikaci**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
docker ps --format '{{.Names}} {{.Status}}' | grep pmtracker-sql   # SQL musí běžet (jinak: colima start)
dotnet run --project PmTracker.Web --launch-profile PmTracker.Web   # na pozadí; http://localhost:5071
```

Počkat, až `curl -s -o /dev/null -w '%{http_code}' 'http://localhost:5071/?asUser=1'` vrátí 200.

- [ ] **Step 2: Snímky obrazovky (světlý i tmavý motiv)**

```bash
CHROME="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
OUT="$SCRATCHPAD/hlavicka"; mkdir -p "$OUT"
PROJ=$(/opt/homebrew/bin/sqlcmd -S localhost,1433 -U sa -P 'PmTracker!2026' -Ndisable -d PmTracker -W -h -1 -Q "SET NOCOUNT ON; SELECT TOP 1 id FROM projekty ORDER BY id")
for p in "/|prehled" "/Dashboard/Focus|focus" "/Projekty|projekty" "/Projekty/Detail/$PROJ|projekt" "/Profil|profil" "/Osoby|osoby"; do
  url="${p%%|*}"; name="${p##*|}"
  "$CHROME" --headless=new --disable-gpu --hide-scrollbars --window-size=1400,420 \
    --screenshot="$OUT/$name-light.png" "http://localhost:5071$url?asUser=1"
  "$CHROME" --headless=new --disable-gpu --hide-scrollbars --window-size=1400,420 --force-dark-mode \
    --screenshot="$OUT/$name-dark.png" "http://localhost:5071$url?asUser=1"
done
```

(`$SCRATCHPAD` = scratchpad adresář session.) Snímky prohlédnout nástrojem Read. Kontrolní seznam:
- vybraná položka podbarvená, žádná linka pod ní, žádné zaoblené konce čáry;
- na `/` a `/Dashboard/Focus` je podbarvený Přehled;
- `/`, `/Projekty`, `/Profil`, `/Osoby`: bez drobečkové lišty, pod menu tenká čára, obsah nesplývá s menu;
- `/Dashboard/Focus`, `/Projekty/Detail/…`: lišta je, pod menu jedna čára (ne dvojitá);
- tmavý motiv: podbarvení viditelné, text čitelný. Motiv bez cookie řídí inline skript v `<head>` podle `prefers-color-scheme`. Pokud `--force-dark-mode` motiv nepřepne (tmavý snímek vyjde světlý), zopakovat tmavé snímky s `--blink-settings=preferredColorScheme=0` místo `--force-dark-mode`.

- [ ] **Step 3: Úzký displej**

Zopakovat snímek `/Projekty` s `--window-size=420,800` (světlý). Ověřit, že hlavička a hamburger vypadají jako dřív a pod hlavičkou je čára.

- [ ] **Step 4: Zastavit aplikaci**

Ukončit proces `dotnet run` z kroku 1.

- [ ] **Step 5: Celé sady testů na větvi**

```bash
dotnet test PmTracker.Tests.Unit 2>&1 | tail -1
dotnet test PmTracker.Tests.Api 2>&1 | grep -E "^\s+Neúspěšné |Úspěšné!|Neúspěšné!"
```

Expected: Unit bez selhání; Api jen 4 baseline selhání (jména z Global Constraints).

- [ ] **Step 6: Merge do main**

```bash
git checkout main
git merge --no-ff hlavicka-zvyrazneni-drobecky -m "Merge hlavicka-zvyrazneni-drobecky into main"
dotnet test PmTracker.Tests.Unit 2>&1 | tail -1
dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~Breadcrumb|FullyQualifiedName~LayoutGovHeader" 2>&1 | grep -E "Úspěšné!|Neúspěšné!"
git branch -d hlavicka-zvyrazneni-drobecky
git status -sb | head -1
```

Expected: testy zelené, `main` napřed před `origin/main`. Nepushovat.

- [ ] **Step 7: Report uživateli**

Česky: co se změnilo (podbarvení, Přehled, drobečky jen při zanoření, čára pod menu na hlavičce), výsledky testů s čísly, odkaz na snímky (případně je poslat), že `main` není pushnutý, a co ručně vyzkoušet (`/`, Projekty, detail projektu, „zobrazit více“ v Přehledu, Můj profil; světlý i tmavý motiv; Edge na i15).
