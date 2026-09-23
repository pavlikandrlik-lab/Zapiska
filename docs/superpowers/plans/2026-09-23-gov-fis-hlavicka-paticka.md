# Standardní gov hlavička a patička (DS gov 4.7.0) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Pozn. pro tento projekt:** uživatel preferuje **inline exekuci** v hlavní session (superpowers:executing-plans), subagenti se neosvědčili. Po každém tasku vypiš stav a **počkej na rozhodnutí uživatele**.

**Goal:** Nahradit vlastní hlavičku a patičku standardními bílými komponentami DS gov.cz (`gov-header`, `gov-navigation`, `gov-footer`, `gov-skip-links`) a kvůli tomu povýšit jádro DS z 4.2.9 na 4.7.0.

**Architecture:** Předsestavený DS gov 4.7.0 se 1:1 zkopíruje z kitu do `wwwroot/assets/gov` a `_Layout.cshtml` ho načte v pořadí MANUALu bez `index.css`. Hlavička a patička přebírají strukturu `DesignSystem-FIS-v1.0.0/index.html` bez DS FIS specifik; obsah stránek, drobečky a vyhledávací dropdown zůstávají. Motiv obsluhuje nativní `gov-theme-switch`, aplikace k němu přidává jen roční cookie a výchozí stav podle systému.

**Tech Stack:** .NET 8, ASP.NET Core MVC + Razor, DS gov.cz 4.7.0 (Stencil Web Components + templates CSS/JS), vanilla JS ESM, xUnit + FluentAssertions, Testcontainers (Api), Playwright .NET (E2E).

**Spec:** `docs/superpowers/specs/2026-09-23-gov-fis-hlavicka-paticka-design.md` — **§12 má přednost** před starším textem specu.

## Global Constraints

- **Soubory DS se nikdy needitují** (README kitu, pravidlo 1, bez výjimky): `wwwroot/assets/gov/**` je 1:1 kopie `DesignSystem-FIS-v1.0.0/assets/gov` bez `icons/`. Co je potřeba změnit, patří do souborů aplikace s prefixem `app-` a do `docs/known-issues/ds-fis-odchylky.md`.
- **Načítá se 9 z 10 CSS DS v pořadí MANUAL B5, bez `index.css`**: tokens → templates-tokens → styles → layout → components → templates → animations → content → skip-links → `fonts/roboto.css`. Nenačítá se `index.css`, `components/core.css`, nic z `ds-fis/`, nic z `lib/gov-design-system`.
- **Aplikační CSS až po DS**: `~/css/tokens.css` → `~/css/govcz.css` → `~/lib/quill/quill.snow.css` → `~/css/site.css`.
- **`iconsPath` = `@Url.Content("~/assets/icons")`** (funguje i s PathBase). Ikony leží v `wwwroot/assets/icons/components/`.
- **Každý `<link>`/`<script>` na `~/assets/gov/` má `asp-append-version="true"`** — `/assets/gov/**` se cachuje natrvalo (`immutable`).
- **Offline**: žádný CDN (hlídá `OfflineAssetsTests`). Statické `.css`/`.js` vždy s `charset=utf-8` (Edge na i15).
- **Razor kóduje diakritiku na entity** → Api testy kotvit na atributy a ASCII (`href="/Osoby"`, `DS gov.cz 4.7.0`), ne na český text.
- **Razor komentář nikdy dovnitř TagHelper tagu** (`<form asp-*>`) — spolkne následující atribut; komentáře nad tag.
- **Motiv**: server vypíše `data-theme` jen pro cookie `pmtracker.theme.mode` = `dark`/`light`. Nikdy `data-theme="auto"`, nikdy `data-theme-mode`.
- **Git**: commitovat jen soubory tasku. Soubory s cizí rozdělanou prací (`Program.cs`, `wwwroot/js/modules/bootstrap.js`, `wwwroot/js/modules/pageSwitchers.js`, `PmTracker.Tests.Api/Controllers/DocumentationNavigationTests.cs`) se commitují **jen našimi hunky** postupem „Commit souboru s cizím WIP" níže. Necommitnuté cizí testy `PmTracker.Tests.Unit/Layout/BreadcrumbAndMenuLayoutTests.cs` a `BreadcrumbBarMarkupTests.cs` se upraví v pracovním stromu, ale **necommitují**.
- **Commit trailer**: `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- **Baseline selhání** (známá, nesouvisí): Api 4× harmonogram/gantt, Integration 1× `ProposalRejectAndTakeOver` (`proposals.accept`). Unit před začátkem spusť a zapiš si počet — počítá s necommitnutými cizími testy.

### Commit souboru s cizím WIP

```bash
F=PmTracker.Web/wwwroot/js/modules/bootstrap.js        # příklad
SCR=/private/tmp/claude-501/-Users-Pavel-Andrlik-Documents-PM-Tracker/f4036cf8-7b2c-4c5c-800a-82f6ab295290/scratchpad
git show HEAD:$F > "$SCR/head"
cp "$SCR/head" "$SCR/ours"
# Do "$SCR/ours" proveď JEN změny z tohoto tasku (stejné editace jako v pracovním stromu).
diff -u --label a/$F --label b/$F "$SCR/head" "$SCR/ours" > "$SCR/ours.patch"
git apply --cached "$SCR/ours.patch"
git diff --cached -- $F            # zkontroluj, že ve stage je jen naše změna
```

## Ověřená fakta (zkontrolováno při psaní plánu, ne odhad)

- Kit `DesignSystem-FIS-v1.0.0/` **není v gitu** (untracked). Testy se na něj proto neodkazují; seznam ikon kitu je v testu napevno.
- `gov-icon` 4.7.0 skládá URL `${iconsPath}/${type}/${name}.svg?v=4.7.0`, výchozí `iconsPath` je `/assets/icons`, výchozí `type` je `components`.
- Aplikace používá 22 ikon, které kit nemá; kit má 21 ikon, které aplikace nemá (mj. `person-fill`). Dnes nechybí žádná ikona použitá v `<gov-icon>`/`<pm-icon>` (`PmIconTagHelper` vykresluje `gov-icon type="components"`).
- 4.7.0 `styles.css` ≈ základ, který aplikace dnes dostává z 4.2.9 `core.min.css` (nadpisy, odkazy, odrážky `●`, utility `gov-text--*`). Nově jen `h1–h6, p { margin: 0 }` + drobnosti focusu. `index.css` je celý nový.
- Všech 56 gov tokenů, na které odkazuje aplikační CSS, v 4.7.0 existuje (žádný nezmizel).
- `gov-theme-switch` 4.7.0 ukládá volbu do **session** cookie `data-theme`, nastavuje `data-theme` + třídu na `<html>`, při startu čte cookie → atribut `<html data-theme>` → `auto`. Emituje `gov-change` s `detail = { component: "gov-theme-switch", state }`; Stencil `createEvent` má `bubbles: true, composed: true`. `gov-change` emitují i jiné komponenty (`gov-dropdown`, formuláře).
- gov 4.7.0 tokeny v režimu auto (`html:not([data-theme])`) přepínají podle `prefers-color-scheme` **jen gov tokeny**; `site.css` reaguje jen na `[data-theme="dark"]` (161×).
- `gov-button` s `href` vykreslí `<a class="element">`; klik jen `stopPropagation`, ne `preventDefault` → odkaz naviguje. `gov-link` má `external` (→ `target="_blank"`, `rel="noreferrer"`, ikona `box-arrow-up-right`).
- `gov-dropdown` zavírá seznam klikem mimo; odrážky v `gov-dropdown li` a `.gov-navigation ul li` potlačuje DS (`content: none`), patička používá `gov-list--plain` ze `styles.css`.
- Vanilla `templates.css`: `.gov-header__action { display:none }` pod 48 em, `.gov-header__mobile` jen pod 48 em; `gov-navigation` nemá styl aktivní položky.
- `components/core.css` 4.7.0 má 20× `@font-face` s `/playground/build/assets/fonts/…`; žádný z 9 načítaných CSS takový odkaz nemá. `fonts/roboto.css` odkazuje relativně na 20 `roboto-*.woff2`.
- Autentizace je IIS Windows Auth (`IISDefaults`), logout v aplikaci neexistuje.
- Kit nemá žádné `.mjs` soubory → mapování `.mjs` ze specu §4.4 se nepřidává (YAGNI); `.woff2` se mapuje explicitně.
- `site.css` má konce řádků LF; bloky `.gantt-picker {`, `.ciselniky-sidebar {`, `.settings-sidebar {`, `.app-breadcrumb-bar {` a `.docs-sidebar,\n.docs-toc {` jsou na začátku řádku každý jen jednou.
- `DokumentaceController` má jen `[Authorize]` → stránku dokumentace otevře i osoba bez rolí (`EnsurePersonAsync` + `asUser`).
- CSP: `script-src 'self' 'unsafe-inline'`, `font-src 'self' data:`, `connect-src 'self'` → inline skripty v `<head>`, lokální woff2 i `fetch` ikon projdou.

## Review Focus

1. **Tmavý systém, žádná uložená volba** → celá stránka tmavá hned při načtení, ne jen gov prvky. Test: Task 3, E2E `BezVolby_MotivPodleSystemu_APrepnutiSeUlozi`.
2. **Stará cookie `pmtracker.theme.mode=auto`** (z dnešní 3stavové logiky) → server nevypíše `data-theme`, rozhodne systém; nikdy `data-theme="auto"` (to by přepnulo jen gov tokeny). Test: Task 3, Api `CookieAuto_SeChovaJakoBezVolby`.
3. **Ikona použitá ve view/JS chybí v `/assets/icons`** → neviditelná ikona bez chyby. Test: Task 1, Unit `KazdaIkonaPouzitaVAplikaci_Existuje` (Task 4 přidá `person-fill` a test ho pohlídá).
4. **Uživatel bez oprávnění** nesmí v nové navigaci vidět Osoby / Číselníky / Nastavení. Test: Task 4, Api `Navigace_SkryvaSekceBezOpravneni`.
5. **Rozbalené menu uživatele pod drobečkovou lištou** (po zrušení sticky zůstal na liště `z-index`). Test: Task 4, Unit `DrobeckovaLista_NeniPrilepenaANeprekryvaHlavicku` + ruční kontrola.

## Pořadí a checkpointy

| Task | Výstup | Po tasku |
|---|---|---|
| 1 | Assety 4.7.0 + ikony + servírování | layout se nemění, nic vidět není |
| 2 | Jádro 4.7.0 v `<head>` | **checkpoint**: aplikace běží na 4.7.0 se starou hlavičkou → krátký ruční průchod komponent + E2E |
| 3 | Motiv — cookie most | přepínač motivu funguje po novu |
| 4 | Hlavička + navigace | nová bílá hlavička |
| 5 | Patička | nová patička |
| 6 | Úklid 4.2.9 + dokumentace | finální ruční checklist (spec §8) |

---

### Task 1: Assety DS gov 4.7.0, ikony a jejich servírování

**Files:**
- Create: `PmTracker.Web/wwwroot/assets/gov/{components,styles,fonts,templates}/**` (kopie kitu)
- Create: 21 ikon v `PmTracker.Web/wwwroot/assets/icons/components/` (kopie z kitu, jen chybějící)
- Create: `PmTracker.Web/Extensions/StaticAssetCachePolicy.cs`
- Modify: `PmTracker.Web/Program.cs:120-151` (blok statických souborů)
- Test: `PmTracker.Tests.Unit/Layout/GovAssets470Tests.cs`, `PmTracker.Tests.Unit/Layout/StaticAssetCachePolicyTests.cs`, `PmTracker.Tests.Api/Controllers/GovStaticAssetsServingTests.cs`

**Interfaces:**
- Produces: URL `/assets/gov/components/core.esm.js`, `/assets/gov/templates/scripts.js`, `/assets/gov/styles/*.css`, `/assets/gov/fonts/roboto.css`; ikona `/assets/icons/components/person-fill.svg`.
- Produces: `public static class StaticAssetCachePolicy` (namespace `PmTracker.Web.Extensions`) s konstantami `Immutable = "public, max-age=31536000, immutable"`, `NoCache = "no-cache, no-store, must-revalidate"` a metodou `string? Resolve(string requestPath, bool isDevelopment)`.

- [ ] **Step 1: Napiš failing testy na assety**

Vytvoř `PmTracker.Tests.Unit/Layout/GovAssets470Tests.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// DS gov 4.7.0 leží ve wwwroot/assets/gov jako kopie předsestaveného kitu
/// DesignSystem-FIS-v1.0.0/assets/gov beze změn (spec 2026-09-23 §4.1). Aplikace
/// běží offline, takže všechno, co layout načítá, musí ležet lokálně.
/// </summary>
public sealed class GovAssets470Tests
{
    private static string GovRoot => ResolvePath("PmTracker.Web/wwwroot/assets/gov");
    private static string AppIcons => ResolvePath("PmTracker.Web/wwwroot/assets/icons/components");

    // Sada ikon type="components" v kitu 4.7.0 (DesignSystem-FIS-v1.0.0/assets/gov/icons/components).
    // Kit není v gitu, proto seznam napevno.
    private static readonly string[] KitComponentIcons =
    {
        "arrow-down", "arrow-up", "book", "bookmarks", "box-arrow-up-right", "briefcase",
        "caret-right-fill", "check-circle-fill", "check-lg", "chevron-double-left",
        "chevron-double-right", "chevron-down", "chevron-left", "chevron-right", "chevron-up",
        "clock-history", "cookie", "copy", "dash-lg", "download", "envelope", "envelope-fill",
        "exclamation-lg", "exclamation-triangle-fill", "eye", "eye-slash", "facebook",
        "file-earmark", "filetype-jpg", "filetype-pdf", "filetype-png", "filetype-xls", "gear",
        "geo-alt-fill", "house-door-fill", "info", "info-circle", "info-circle-fill", "instagram",
        "lightbulb-fill", "link", "linkedin", "list", "loader", "moon", "person-fill", "plus-lg",
        "quote", "search", "star-fill", "sun", "telephone", "twitter-x", "upload", "x", "x-lg",
        "youtube",
    };

    [Fact]
    public void Loader_Chunky_A_SkriptySablon_JsouLokalne()
    {
        var components = Path.Combine(GovRoot, "components");
        File.Exists(Path.Combine(components, "core.esm.js")).Should().BeTrue("loader komponent");
        Directory.GetFiles(components, "p-*.js").Length.Should().BeGreaterThanOrEqualTo(100,
            "loader dynamicky importuje chunky; kit 4.7.0 jich má 141");
        File.Exists(Path.Combine(GovRoot, "templates", "scripts.js")).Should().BeTrue(
            "templates/scripts.js ovládá hamburger a přetékání hlavní navigace");
    }

    [Theory]
    [InlineData("tokens.css")]
    [InlineData("templates-tokens.css")]
    [InlineData("styles.css")]
    [InlineData("layout.css")]
    [InlineData("components.css")]
    [InlineData("templates.css")]
    [InlineData("animations.css")]
    [InlineData("content.css")]
    [InlineData("skip-links.css")]
    [InlineData("index.css")]
    public void StylyDs_JsouLokalne(string file)
    {
        File.Exists(Path.Combine(GovRoot, "styles", file)).Should().BeTrue(
            $"styles/{file} je součást kopie kitu (index.css se kopíruje, jen nenačítá)");
    }

    [Fact]
    public void RobotoCss_OdkazujeJenNaLokalniFonty()
    {
        var fonts = Path.Combine(GovRoot, "fonts");
        var css = File.ReadAllText(Path.Combine(fonts, "roboto.css"));
        var urls = Regex.Matches(css, @"url\('([^']+)'\)").Select(m => m.Groups[1].Value).ToList();

        urls.Should().NotBeEmpty();
        foreach (var url in urls)
        {
            url.Should().NotContain("/", "font se načítá vedle roboto.css, ne z CDN ani z absolutní cesty");
            File.Exists(Path.Combine(fonts, url)).Should().BeTrue($"font {url} musí ležet vedle roboto.css");
        }
    }

    [Fact]
    public void NadstavbaDsFis_AIkonyKitu_SeNekopiruji()
    {
        Directory.Exists(ResolvePath("PmTracker.Web/wwwroot/assets/ds-fis")).Should().BeFalse(
            "DS FIS nadstavba (tmavě modrá hlavička) se nepoužívá");
        Directory.Exists(Path.Combine(GovRoot, "icons")).Should().BeFalse(
            "ikony zůstávají v aplikačním /assets/icons (spec §12.2)");
    }

    [Fact]
    public void AplikacniIkony_JsouNadmnozinouKitu()
    {
        // iconsPath míří na aplikační strom a gov komponenty si z něj vnitřně berou
        // ikony kitové sady (chevron-down, x-lg, eye-slash…) — žádná nesmí chybět.
        KitComponentIcons.Where(name => !File.Exists(Path.Combine(AppIcons, name + ".svg")))
            .Should().BeEmpty();
    }

    [Fact]
    public void KazdaIkonaPouzitaVAplikaci_Existuje()
    {
        var sources = Directory.GetFiles(ResolvePath("PmTracker.Web/Views"), "*.cshtml", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(ResolvePath("PmTracker.Web/wwwroot/js"), "*.js", SearchOption.AllDirectories));

        var used = new SortedSet<string>();
        foreach (var file in sources)
        {
            // pm-icon je TagHelper, který vykreslí gov-icon type="components".
            foreach (Match tag in Regex.Matches(File.ReadAllText(file), @"<(?:gov-icon|pm-icon)\b[^>]*>"))
            {
                var name = Regex.Match(tag.Value, @"\bname=""([a-z0-9-]+)""");
                if (name.Success)
                {
                    used.Add(name.Groups[1].Value);
                }
            }
        }

        used.Should().NotBeEmpty();
        used.Where(name => !File.Exists(Path.Combine(AppIcons, name + ".svg")))
            .Should().BeEmpty("chybějící SVG = neviditelná ikona, gov-icon tiše selže");
    }
}
```

- [ ] **Step 2: Napiš failing testy na cache politiku**

Vytvoř `PmTracker.Tests.Unit/Layout/StaticAssetCachePolicyTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Extensions;

namespace PmTracker.Tests.Unit.Layout;

public sealed class StaticAssetCachePolicyTests
{
    [Theory]
    [InlineData("/assets/gov/components/core.esm.js", false)]
    [InlineData("/assets/gov/components/core.esm.js", true)]
    [InlineData("/assets/gov/fonts/roboto-regular.woff2", false)]
    [InlineData("/assets/gov/styles/templates.css", true)]
    public void GovAssety_SeCachujiNatrvalo(string path, bool isDevelopment)
    {
        StaticAssetCachePolicy.Resolve(path, isDevelopment).Should().Be(StaticAssetCachePolicy.Immutable);
    }

    [Theory]
    [InlineData("/js/site.js")]
    [InlineData("/css/site.css")]
    public void AplikacniJsACss_SeVeVyvojiNecachuji(string path)
    {
        // ESM sub-importy asp-append-version nevidí → bez no-cache by změny JS nedorazily.
        StaticAssetCachePolicy.Resolve(path, isDevelopment: true).Should().Be(StaticAssetCachePolicy.NoCache);
    }

    [Theory]
    [InlineData("/js/site.js", false)]
    [InlineData("/css/site.css", false)]
    [InlineData("/assets/icons/components/x.svg", false)]
    [InlineData("/assets/icons/components/x.svg", true)]
    [InlineData("/images/zapiska-logo.svg", false)]
    public void OstatniSoubory_BezZmenyHlavicky(string path, bool isDevelopment)
    {
        // Ikony nemají hash v názvu (gov je verzuje jen ?v=4.7.0) → nesmí být immutable.
        StaticAssetCachePolicy.Resolve(path, isDevelopment).Should().BeNull();
    }
}
```

- [ ] **Step 3: Napiš failing Api test na servírování**

Vytvoř `PmTracker.Tests.Api/Controllers/GovStaticAssetsServingTests.cs`:

```csharp
using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// DS gov 4.7.0 se servíruje z wwwroot/assets/gov se správným typem a natrvalo
/// cachovaný (spec 2026-09-23 §4.4). Factory běží v prostředí Development.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class GovStaticAssetsServingTests
{
    private readonly ApiSqlFixture _fixture;

    public GovStaticAssetsServingTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Font_MaTypWoff2ACacheImmutable()
    {
        using var client = _fixture.Factory.CreateClient();
        var response = await client.GetAsync("/assets/gov/fonts/roboto-regular.woff2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("font/woff2");
        response.Headers.CacheControl!.ToString().Should().Contain("immutable");
    }

    [Fact]
    public async Task LoaderKomponent_MaCharsetUtf8ACacheImmutable()
    {
        using var client = _fixture.Factory.CreateClient();
        var response = await client.GetAsync("/assets/gov/components/core.esm.js");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.ToString().Should().Contain("charset=utf-8");
        response.Headers.CacheControl!.ToString().Should().Contain("immutable");
    }

    [Fact]
    public async Task AplikacniJs_VeVyvoji_ZustavaNoCache()
    {
        using var client = _fixture.Factory.CreateClient();
        var response = await client.GetAsync("/js/global-search.js");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }
}
```

- [ ] **Step 4: Spusť testy — musí selhat**

```bash
dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~GovAssets470Tests|FullyQualifiedName~StaticAssetCachePolicyTests"
```

Expected: build FAIL (`StaticAssetCachePolicy` neexistuje). Po dočasném zakomentování `StaticAssetCachePolicyTests` by `GovAssets470Tests` selhaly na `Loader…`, `StylyDs…`, `RobotoCss…`, `AplikacniIkony…` (21 ikon chybí); `NadstavbaDsFis…` a `KazdaIkona…` už projdou — to je v pořádku, hlídají pozdější tasky.

- [ ] **Step 5: Zkopíruj DS a chybějící ikony**

```bash
KIT="DesignSystem-FIS-v1.0.0/assets/gov"
DEST="PmTracker.Web/wwwroot/assets/gov"
ICONS="PmTracker.Web/wwwroot/assets/icons/components"
mkdir -p "$DEST"
cp -R "$KIT/components" "$KIT/styles" "$KIT/fonts" "$KIT/templates" "$DEST/"
for f in "$KIT"/icons/components/*.svg; do
  n=$(basename "$f")
  [ -e "$ICONS/$n" ] || cp "$f" "$ICONS/$n"
done
# kontrola: kopie DS je bajtově shodná s kitem
for d in components styles fonts templates; do diff -rq "$KIT/$d" "$DEST/$d"; done
ls "$ICONS" | wc -l   # 58 dosavadních + 21 nových = 79
```

Společné ikony se **nepřepisují** (aplikační Bootstrap verze zůstává).

- [ ] **Step 6: Vytvoř cache politiku**

Vytvoř `PmTracker.Web/Extensions/StaticAssetCachePolicy.cs`:

```csharp
namespace PmTracker.Web.Extensions;

/// <summary>
/// Cache-Control pro statické soubory.
/// <para>
/// DS gov v <c>/assets/gov/</c> se mění jen výměnou celé složky při upgradu DS. Vstupní
/// soubory layout verzuje přes asp-append-version, chunky mají hash v názvu a fonty se
/// nemění → smí se cachovat natrvalo (MANUAL kitu, Část B).
/// </para>
/// <para>
/// Ve vývoji dostanou ostatní .js/.css no-cache: ESM moduly se importují relativním URL
/// bez verze (asp-append-version verzuje jen entry site.js), takže by změny v modulech
/// nedorazily do prohlížeče bez ručního vymazání cache. V produkci necháváme standardní
/// caching (deploy = plná výměna souborů + ohlášený hard-refresh).
/// </para>
/// </summary>
public static class StaticAssetCachePolicy
{
    public const string Immutable = "public, max-age=31536000, immutable";
    public const string NoCache = "no-cache, no-store, must-revalidate";

    /// <returns>Hodnota hlavičky Cache-Control, nebo null = hlavičku neměnit.</returns>
    public static string? Resolve(string requestPath, bool isDevelopment)
    {
        if (requestPath.StartsWith("/assets/gov/", StringComparison.OrdinalIgnoreCase))
        {
            return Immutable;
        }

        if (isDevelopment
            && (requestPath.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
                || requestPath.EndsWith(".css", StringComparison.OrdinalIgnoreCase)))
        {
            return NoCache;
        }

        return null;
    }
}
```

- [ ] **Step 7: Napoj politiku v Program.cs**

V `PmTracker.Web/Program.cs` nahraď celý blok od `var staticContentTypeProvider = …` po konec `if (app.Environment.IsDevelopment()) { … } else { … }` s `UseStaticFiles` (dnes ř. 120–151; komentář „FIX 2026-07-10" nad ním ponech) tímto:

```csharp
var staticContentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
staticContentTypeProvider.Mappings[".css"] = "text/css; charset=utf-8";
staticContentTypeProvider.Mappings[".js"] = "text/javascript; charset=utf-8";
// Písmo DS gov 4.7.0 (wwwroot/assets/gov/fonts) — explicitně, ať nezáleží na výchozí tabulce.
staticContentTypeProvider.Mappings[".woff2"] = "font/woff2";

// Cache-Control: DS gov natrvalo, ve vývoji aplikační .js/.css no-cache (viz StaticAssetCachePolicy).
var isDevelopment = app.Environment.IsDevelopment();
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = staticContentTypeProvider,
    OnPrepareResponse = ctx =>
    {
        var cacheControl = StaticAssetCachePolicy.Resolve(ctx.Context.Request.Path.Value ?? string.Empty, isDevelopment);
        if (cacheControl is not null)
        {
            ctx.Context.Response.Headers.CacheControl = cacheControl;
        }
    }
});
```

`using PmTracker.Web.Extensions;` v souboru už je (ř. 6).

- [ ] **Step 8: Spusť testy — musí projít**

```bash
dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~GovAssets470Tests|FullyQualifiedName~StaticAssetCachePolicyTests"
dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~GovStaticAssetsServingTests"
```

Expected: PASS. Pak celý Unit (`dotnet test PmTracker.Tests.Unit`) — počet selhání stejný jako baseline.

- [ ] **Step 9: Commit — nejdřív se zeptej**

`Program.cs` má **necommitnutý** blok „FIX 2026-07-10" (charset + `ContentTypeProvider` v obou větvích `UseStaticFiles`), na který Step 7 přímo navazuje — samotný náš hunk proti HEAD nejde použít. Zeptej se uživatele:
(a) commitnout jeho charset/static-files WIP zvlášť jako samostatný commit před naším, nebo
(b) `Program.cs` necommitovat a nechat ho v pracovním stromu.

Pak:

```bash
git add PmTracker.Web/wwwroot/assets/gov PmTracker.Web/Extensions/StaticAssetCachePolicy.cs \
        PmTracker.Tests.Unit/Layout/GovAssets470Tests.cs PmTracker.Tests.Unit/Layout/StaticAssetCachePolicyTests.cs \
        PmTracker.Tests.Api/Controllers/GovStaticAssetsServingTests.cs
# jen 21 nově zkopírovaných ikon (NE lock/unlock/three-dots-vertical — cizí WIP):
for n in arrow-down arrow-up briefcase check-circle-fill clock-history cookie envelope eye-slash facebook \
         file-earmark filetype-jpg filetype-pdf filetype-png filetype-xls instagram link linkedin person-fill \
         plus-lg quote youtube; do git add "PmTracker.Web/wwwroot/assets/icons/components/$n.svg"; done
# Program.cs dle volby (a)/(b)
git commit -m "feat(gov): assety DS gov 4.7.0 a jejich servírování" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 10: Vypiš stav a počkej na rozhodnutí uživatele.**

---

### Task 2: Jádro DS gov 4.7.0 v `<head>` (checkpoint)

Po tomto tasku aplikace běží na 4.7.0 **se starou hlavičkou** — regrese jádra se tak oddělí od změn hlavičky.

**Files:**
- Modify: `PmTracker.Web/Views/Shared/_Layout.cshtml:14-24` (head) a `:181` (starý loader)
- Modify: `PmTracker.Web/wwwroot/css/tokens.css:8` (komentář se zdrojem tokenů)
- Modify: `PmTracker.Tests.Unit/Layout/OfflineAssetsTests.cs`, `PmTracker.Tests.Unit/Layout/TokensCssTests.cs`, `PmTracker.Tests.Unit/Search/GlobalSearchMarkupTests.cs`, `PmTracker.Tests.Unit/Architecture/GovFontsOfflineTests.cs`
- Modify: `docs/known-issues/ds-fis-odchylky.md` (+ odchylky č. 2 a 3)
- Test: `PmTracker.Tests.Unit/Layout/GovHeadLoadOrderTests.cs`

**Interfaces:**
- Consumes: assety z Task 1.
- Produces: `window.GOV_DS_CONFIG` a `window.initTemplateScripts()` dostupné na každé stránce s `_Layout`; gov komponenty z 4.7.0.

- [ ] **Step 1: Napiš failing test pořadí načítání**

Vytvoř `PmTracker.Tests.Unit/Layout/GovHeadLoadOrderTests.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// &lt;head&gt; načítá DS gov 4.7.0 podle MANUAL B5 bez index.css (spec 2026-09-23 §12.1).
/// </summary>
public sealed class GovHeadLoadOrderTests
{
    private static string Layout() => File.ReadAllText(ResolvePath("PmTracker.Web/Views/Shared/_Layout.cshtml"));

    // Jen skutečně načítané URL (href/src v <link>/<script>) — komentáře v layoutu
    // zmiňují index.css, core.css i ds-fis a nesmí test shodit.
    private static List<string> LoadedAssets() =>
        Regex.Matches(Layout(), "<(?:link|script)[^>]*(?:href|src)=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToList();

    private static readonly string[] GovCssInOrder =
    {
        "~/assets/gov/styles/tokens.css",
        "~/assets/gov/styles/templates-tokens.css",
        "~/assets/gov/styles/styles.css",
        "~/assets/gov/styles/layout.css",
        "~/assets/gov/styles/components.css",
        "~/assets/gov/styles/templates.css",
        "~/assets/gov/styles/animations.css",
        "~/assets/gov/styles/content.css",
        "~/assets/gov/styles/skip-links.css",
        "~/assets/gov/fonts/roboto.css",
    };

    [Fact]
    public void GovCss_VPoradiManualu_APredAplikacnimCss()
    {
        var layout = Layout();
        var positions = GovCssInOrder
            .Select(href => layout.IndexOf($"href=\"{href}\"", StringComparison.Ordinal))
            .ToList();

        positions.Should().NotContain(-1, "všech 10 odkazů (9 CSS ze styles/ + roboto.css) musí být v layoutu");
        positions.Should().BeInAscendingOrder("pořadí dle MANUAL B5");
        layout.IndexOf("href=\"~/css/tokens.css\"", StringComparison.Ordinal)
            .Should().BeGreaterThan(positions.Last(), "aplikační CSS se načítá až po DS");
    }

    [Theory]
    [InlineData("index.css", "přestyluje main/section/fieldset v celé aplikaci (odchylka č. 2)")]
    [InlineData("components/core.css", "odkazuje na neexistující /playground fonty")]
    [InlineData("ds-fis", "nadstavba DS FIS se nepoužívá (bílá hlavička)")]
    [InlineData("lib/gov-design-system", "4.2.9 — dvě jádra by se přela o definice komponent")]
    public void Layout_NenacitaZakazaneAssety(string fragment, string because)
    {
        LoadedAssets().Should().NotContain(asset => asset.Contains(fragment), because);
    }

    [Fact]
    public void KonfiguraceDs_JePredLoaderem_AIkonyMiriNaAplikacniStrom()
    {
        var layout = Layout();
        var config = layout.IndexOf("window.GOV_DS_CONFIG", StringComparison.Ordinal);
        var loader = layout.IndexOf("~/assets/gov/components/core.esm.js", StringComparison.Ordinal);

        config.Should().BeGreaterThan(0);
        loader.Should().BeGreaterThan(config, "loader čte GOV_DS_CONFIG při startu");
        layout.Should().Contain("iconsPath: \"@Url.Content(\"~/assets/icons\")\"",
            "aplikační strom ikon (odchylka č. 3); Url.Content funguje i v podadresáři");
    }

    [Fact]
    public void SkriptySablon_SeInicializujiPoNacteniDom()
    {
        var layout = Layout();
        layout.Should().Contain("~/assets/gov/templates/scripts.js");
        layout.Should().Contain("window.initTemplateScripts()");
    }

    [Fact]
    public void GovAssety_SeVerzuji_ProtozeSeCachujiNatrvalo()
    {
        // /assets/gov/** má Cache-Control immutable — bez ?v= by upgrade DS se stejnými
        // názvy souborů zůstal rok v cache prohlížeče.
        var tags = Regex.Matches(Layout(), "<(?:link|script)[^>]*~/assets/gov/[^>]*>")
            .Select(m => m.Value)
            .ToList();

        tags.Should().HaveCount(12, "9 CSS ze styles/ + roboto.css + core.esm.js + scripts.js");
        tags.Should().OnlyContain(tag => tag.Contains("asp-append-version=\"true\""));
    }
}
```

- [ ] **Step 2: Spusť — musí selhat**

```bash
dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~GovHeadLoadOrderTests"
```

Expected: FAIL (layout načítá `lib/gov-design-system`, gov CSS chybí).

- [ ] **Step 3: Přepiš `<head>` v `_Layout.cshtml`**

Nahraď řádky 14–24 (`<head>` … `</head>`) tímto (horní blok s cookie motivu a tag `<html>` zatím beze změny — řeší Task 3):

```cshtml
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] - Zápiska</title>

    @* Konfigurace DS gov — musí být před loaderem komponent. iconsPath míří na aplikační
       strom ikon (nadmnožina sady kitu), ne na assets/gov/icons — odchylka č. 3
       v docs/known-issues/ds-fis-odchylky.md. *@
    <script>
        window.GOV_DS_CONFIG = { iconsPath: "@Url.Content("~/assets/icons")", canValidateWcagOnRender: false };
    </script>

    @* CSS DS gov 4.7.0 v pořadí MANUAL B5, BEZ index.css — ten by globálně přestyloval
       main, section a fieldset v celé aplikaci (odchylka č. 2). components/core.css se
       nenačítá: odkazuje na neexistující /playground fonty; písmo jde přes fonts/roboto.css. *@
    <link rel="stylesheet" href="~/assets/gov/styles/tokens.css" asp-append-version="true" />
    <link rel="stylesheet" href="~/assets/gov/styles/templates-tokens.css" asp-append-version="true" />
    <link rel="stylesheet" href="~/assets/gov/styles/styles.css" asp-append-version="true" />
    <link rel="stylesheet" href="~/assets/gov/styles/layout.css" asp-append-version="true" />
    <link rel="stylesheet" href="~/assets/gov/styles/components.css" asp-append-version="true" />
    <link rel="stylesheet" href="~/assets/gov/styles/templates.css" asp-append-version="true" />
    <link rel="stylesheet" href="~/assets/gov/styles/animations.css" asp-append-version="true" />
    <link rel="stylesheet" href="~/assets/gov/styles/content.css" asp-append-version="true" />
    <link rel="stylesheet" href="~/assets/gov/styles/skip-links.css" asp-append-version="true" />
    <link rel="stylesheet" href="~/assets/gov/fonts/roboto.css" asp-append-version="true" />

    @* Aplikační CSS až po DS. *@
    <link rel="stylesheet" href="~/css/tokens.css" asp-append-version="true" />
    <link rel="stylesheet" href="~/css/govcz.css" asp-append-version="true" />
    <link rel="stylesheet" href="~/lib/quill/quill.snow.css" asp-append-version="true" />
    <link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />

    <script type="module" src="~/assets/gov/components/core.esm.js" asp-append-version="true"></script>
    <script src="~/assets/gov/templates/scripts.js" asp-append-version="true" defer></script>
    <script>
        window.addEventListener("DOMContentLoaded", function () {
            if (window.initTemplateScripts) window.initTemplateScripts();
        });
    </script>
</head>
```

A smaž starý loader na konci `<body>` (ř. 181):

```cshtml
    <script type="module" src="~/lib/gov-design-system/dist/core/core.esm.min.js" asp-append-version="true"></script>
```

- [ ] **Step 4: Přesměruj existující testy na 4.7.0**

1. `PmTracker.Tests.Unit/Layout/OfflineAssetsTests.cs` — smaž metodu `GovDesignSystem_JeLokalneVRepozitari` (ř. 79–103); totéž pro 4.7.0 hlídá `GovAssets470Tests.Loader_Chunky_A_SkriptySablon_JsouLokalne`.
2. `PmTracker.Tests.Unit/Layout/TokensCssTests.cs`:
   - v `Layout_TokensCss_JePoGovCssAPredSiteCss` nahraď `layout.IndexOf("core.min.css", …)` za `layout.IndexOf("~/assets/gov/styles/components.css", StringComparison.Ordinal)` a text `because` na `"tokens.css musí být po gov components.css (přepisuje gov tokeny)"`;
   - v `TokensCss_ReferencovanyGovTokenExistuje` nahraď cestu `"lib", "gov-design-system", "styles", "lib", "tokens.min.css"` za `"assets", "gov", "styles", "tokens.css"`;
   - v komentáři `TokensCss_NeobsahujeNeexistujiciGovPrefix` `4.2.7` → `4.x`.
3. `PmTracker.Tests.Unit/Search/GlobalSearchMarkupTests.cs`:
   - doc komentář třídy: `~/lib/gov-design-system/` → `~/assets/gov/`;
   - `Layout_MaLocalScript_GovComponents`: očekávej `"~/assets/gov/components/core.esm.js"`;
   - `Layout_MaLocalCss_GovComponents`: očekávej `"~/assets/gov/styles/components.css"` a `"~/assets/gov/styles/tokens.css"`;
   - smaž `Layout_MaLocalniGovAssety_VRepozitari` (duplicita `GovAssets470Tests`).
4. `PmTracker.Tests.Unit/Architecture/GovFontsOfflineTests.cs` — nahraď celé tělo třídy (a doc komentář):

```csharp
using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Guard: aplikace musí fungovat offline. DS gov components/core.css odkazuje v @font-face
/// na /playground/build/assets/fonts/… — cesty, které v aplikaci neexistují (každá stránka
/// by házela 20× 404; v 4.2.9 se kvůli tomu upravoval soubor DS). Soubory DS se nově
/// needitují (README kitu, pravidlo 1): core.css se jen nenačítá a písmo jde přes
/// fonts/roboto.css s lokálními woff2 (GovAssets470Tests).
/// </summary>
public sealed class GovFontsOfflineTests
{
    [Fact]
    public void Layout_NenacitaCoreCssSPlaygroundFonty()
    {
        // Kontroluje jen href — komentář v layoutu core.css zmiňuje.
        var layout = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Shared/_Layout.cshtml"));
        layout.Should().NotMatchRegex("href=\"[^\"]*components/core\\.css");
        layout.Should().Contain("href=\"~/assets/gov/fonts/roboto.css\"", "písmo DS se načítá z lokálních woff2");
    }

    [Theory]
    [InlineData("tokens.css")]
    [InlineData("templates-tokens.css")]
    [InlineData("styles.css")]
    [InlineData("layout.css")]
    [InlineData("components.css")]
    [InlineData("templates.css")]
    [InlineData("animations.css")]
    [InlineData("content.css")]
    [InlineData("skip-links.css")]
    public void NacitaneCssDs_NeodkazujiNaExterniFonty(string file)
    {
        var css = File.ReadAllText(ResolvePath($"PmTracker.Web/wwwroot/assets/gov/styles/{file}"));
        css.Should().NotContain("/playground/build/assets/fonts/");
        css.Should().NotContain("fonts.gstatic.com", "gov fonty se nesmí stahovat z Google CDN");
    }
}
```

5. `PmTracker.Web/wwwroot/css/tokens.css:8` — `~/lib/gov-design-system/styles/lib/tokens.min.css` → `~/assets/gov/styles/tokens.css` a v ř. 11 `4.2.7` → `4.x`.

- [ ] **Step 5: Zapiš odchylky č. 2 a 3**

Na konec `docs/known-issues/ds-fis-odchylky.md` přidej:

```markdown
## 2. `index.css` DS gov se nenačítá (2026-09-23)

**Čeho se týká:** pořadí CSS v `<head>` podle MANUAL, Část B, B5.

**Co DS předepisuje:** načíst všech 10 souborů `assets/gov/styles/*.css` včetně `index.css`.

**Proč se odchylujeme:** `index.css` nese pravidla šablony stránky pro obsah uvnitř
`<gov-container id="main">`: `main { display:flex; flex-direction:column; gap }`,
`section > *:not(gov-layout-column) { margin-top }`, `section h2 + p { margin-top }`,
`fieldset`, `address`, `picture`. Aplikace obsah do `gov-container` nebalí (odchylka č. 5)
a má vlastní rozložení; tato pravidla by přestylovala celou aplikaci (79× `<section>`
ve view). Ostatních 9 souborů se načítá v pořadí MANUALu.

**Jak je odchylka provedena:** `index.css` se kopíruje s kitem beze změny, jen ho
`_Layout.cshtml` nenačítá. Z jeho pravidel patička potřebuje jen `address { font-style: normal }`
→ třída `app-address` v `site.css`. `[hidden]` a skip-links pokrývají `components.css`
a `skip-links.css`. Soubory `assets/gov/**` se needitují.

**Podklad pro centrální úpravu DS:** v balíčku `@gov-design-system-ce/styles` oddělit
pravidla šablony stránky (`main`, `section`) od pravidel potřebných pro hlavičku a patičku,
aby šlo DS nasadit do existující aplikace po částech. Patří do DS gov.

## 3. `iconsPath` míří na aplikační ikony (2026-09-23)

**Čeho se týká:** `window.GOV_DS_CONFIG.iconsPath` (MANUAL B: „musí ukazovat na `…/gov/icons`").

**Proč se odchylujeme:** aplikace používá 22 ikon `type="components"`, které sada kitu
(57 ikon) nemá — pencil, plus, trash, save, printer, lock… Do `assets/gov/**` se nesmí
nic přidávat (pravidlo 1) a `gov-icon` bere ikony z jediného kořene.

**Jak je odchylka provedena:** `iconsPath` = `~/assets/icons` (výchozí hodnota gov),
tj. aplikační `wwwroot/assets/icons/components/`. Obsahuje dosavadní ikony (Bootstrap
Icons 1.11.3, stahujeme je sami) + 21 ikon kitu, které aplikace neměla (mj. `person-fill`).
Společné ikony zůstávají v aplikační verzi (stejné glyfy). Složka `icons/` kitu se do
`wwwroot/assets/gov` nekopíruje. `GovAssets470Tests` hlídá, že strom je nadmnožinou sady
kitu a že existuje každá ikona použitá ve view/JS.

**Podklad pro centrální úpravu DS:** rozšířit `@gov-design-system-ce/icons` o běžné
akční ikony (pencil, plus, trash, save, printer, lock, unlock, calendar…), nebo umožnit
v `gov-icon` více kořenů ikon.
```

- [ ] **Step 6: Spusť testy**

```bash
dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~Layout|FullyQualifiedName~Search|FullyQualifiedName~GovFontsOffline"
dotnet test PmTracker.Tests.Unit
dotnet test PmTracker.Tests.Api
```

Expected: nový test PASS, Unit na baseline, Api na baseline (4 známá selhání).

- [ ] **Step 7: E2E regresní síť jádra**

```bash
dotnet test PmTracker.Tests.E2E
```

E2E sama spustí aplikaci (`dotnet run` na :5188) a potřebuje Docker + Playwright prohlížeče. Hlavně sleduj `ModalGovDialogSmokeTests`, `ModalCloseXScenariosTests`, `EventBusAdapterTests`, `GovComponentsRenderTests`, `StyleGuideRenderTests`. Selhání porovnej se stavem před taskem (`git stash` není potřeba — stačí výsledky reportovat uživateli).

- [ ] **Step 8: Checkpoint — ruční průchod uživatele (krátký)**

Předej uživateli k odkliknutí na 4.7.0 se starou hlavičkou:
- [ ] Modál (např. nový záznam): otevře se, zavře **křížkem**, klik mimo ho nezavře.
- [ ] `gov-button` reaguje (uložit záznam, akce na kartě), `gov-form-switch` přepíná (filtry projektů).
- [ ] Ikony se zobrazují (karty záznamů, drobečky, harmonogram) — v konzoli žádné 404.
- [ ] Písmo je Roboto (dřív jen, pokud ho měl systém).
- [ ] Nadpisy a odstavce: mezery vypadají rozumně (4.7.0 nuluje `margin` u `h1–h6, p`).
- [ ] Stránka výsledků hledání (`/Search/Index?q=…`): `gov-page-heading` a karty vypadají dobře (templates CSS je teď poprvé styluje).

- [ ] **Step 9: Commit**

```bash
git add PmTracker.Web/Views/Shared/_Layout.cshtml PmTracker.Web/wwwroot/css/tokens.css \
        PmTracker.Tests.Unit/Layout/GovHeadLoadOrderTests.cs PmTracker.Tests.Unit/Layout/OfflineAssetsTests.cs \
        PmTracker.Tests.Unit/Layout/TokensCssTests.cs PmTracker.Tests.Unit/Search/GlobalSearchMarkupTests.cs \
        PmTracker.Tests.Unit/Architecture/GovFontsOfflineTests.cs docs/known-issues/ds-fis-odchylky.md
git commit -m "feat(gov): jádro DS gov 4.7.0 v layoutu" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 10: Vypiš stav (vč. výsledků E2E) a počkej na rozhodnutí uživatele.**

---

### Task 3: Motiv — nativní `gov-theme-switch` + tenký cookie most

**Files:**
- Modify: `PmTracker.Web/Views/Shared/_Layout.cshtml:1-13` (blok motivu, `<html>`), `<head>` (inline skript před CSS)
- Rewrite: `PmTracker.Web/wwwroot/js/modules/theme.js`
- Modify: `PmTracker.Tests.Api/Controllers/DocumentationNavigationTests.cs` (`Layout_ShouldRenderServerThemeAttributesFromCookie`) — soubor má cizí WIP
- Modify: `PmTracker.Tests.Unit/Layout/GovComponentsReplacementTests.cs:33-36` (doc komentář)
- Modify: `PmTracker.Tests.E2E/Scenarios/GlobalSearchDynamicResultsTests.cs` (`DarkMode_MaJedinouLupu_VDom`)
- Modify: `docs/wiki/profil/nastaveni-uzivatele.md:20-27`
- Test: `PmTracker.Tests.Unit/Layout/ThemeBridgeTests.cs`, `PmTracker.Tests.Api/Controllers/ThemeServerRenderTests.cs`, `PmTracker.Tests.E2E/Scenarios/ThemeBridgeScenariosTests.cs`

**Interfaces:**
- Consumes: `gov-theme-switch` 4.7.0 (Task 2).
- Produces: `export function initTheme()` v `theme.js` (jméno beze změny — volá ho `bootstrap.js`); cookie `pmtracker.theme.mode` ∈ {`dark`,`light`}, `Path=/`, `Max-Age=31536000`, `SameSite=Lax`.

- [ ] **Step 1: Napiš failing unit test mostu**

Vytvoř `PmTracker.Tests.Unit/Layout/ThemeBridgeTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Motiv obsluhuje nativní gov-theme-switch; aplikace přidává jen roční cookie a výchozí
/// stav podle systému (spec 2026-09-23 §9.1, §12.4).
/// </summary>
public sealed class ThemeBridgeTests
{
    private static string ThemeJs() => File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/theme.js"));
    private static string Layout() => File.ReadAllText(ResolvePath("PmTracker.Web/Views/Shared/_Layout.cshtml"));

    [Fact]
    public void Most_PoslouchaGovChange_JenZPrepinaceMotivu()
    {
        var js = ThemeJs();
        js.Should().Contain("document.addEventListener(\"gov-change\"",
            "gov-change bublá; přepínač se může překreslit, delegace na document přežije");
        js.Should().Contain("detail.component !== govThemeSwitchComponent",
            "gov-change posílá i gov-dropdown a formuláře");
        js.Should().Contain("\"pmtracker.theme.mode\"");
    }

    [Fact]
    public void Most_NeobsahujeTristavovouLogiku()
    {
        var js = ThemeJs();
        js.Should().NotContain("localStorage");
        js.Should().NotContain("data-theme-mode");
        js.Should().NotContain("matchMedia", "výchozí stav podle systému řeší inline skript v <head>");
        js.Should().NotContain("[data-theme-switch]", "most nezávisí na obalu přepínače");
    }

    [Fact]
    public void Layout_DosazujeMotivPodleSystemu_PredPrvnimCss()
    {
        var layout = Layout();
        var resolver = layout.IndexOf("prefers-color-scheme: dark", StringComparison.Ordinal);
        var firstCss = layout.IndexOf("<link rel=\"stylesheet\"", StringComparison.Ordinal);

        resolver.Should().BeGreaterThan(0);
        resolver.Should().BeLessThan(firstCss, "motiv musí být na <html> dřív, než se vykreslí CSS");
        layout.Should().NotContain("data-theme-mode");
    }
}
```

- [ ] **Step 2: Napiš failing Api test vykreslení motivu**

Vytvoř `PmTracker.Tests.Api/Controllers/ThemeServerRenderTests.cs`:

```csharp
using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Server vypíše data-theme jen pro výslovnou volbu (cookie dark/light). Bez ní atribut
/// chybí a motiv dosadí podle systému inline skript v &lt;head&gt; (spec 2026-09-23 §12.4).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ThemeServerRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public ThemeServerRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<string> HtmlTagAsync(string? cookie)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        if (cookie is not null)
        {
            client.DefaultRequestHeaders.Add("Cookie", $"pmtracker.theme.mode={cookie}");
        }

        var response = await client.GetAsync($"/Projekty?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return Regex.Match(html, "<html[^>]*>").Value;
    }

    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public async Task VyslovnaVolba_SeVykresliNaServeru(string theme)
    {
        var tag = await HtmlTagAsync(theme);
        tag.Should().Contain($"data-theme=\"{theme}\"");
        tag.Should().NotContain("data-theme-mode");
    }

    [Fact]
    public async Task BezCookie_AtributChybi()
    {
        (await HtmlTagAsync(null)).Should().NotContain("data-theme");
    }

    [Fact]
    public async Task CookieAuto_SeChovaJakoBezVolby()
    {
        // Pozůstatek dnešní 3stavové logiky. data-theme="auto" by přepnul jen gov tokeny.
        (await HtmlTagAsync("auto")).Should().NotContain("data-theme");
    }
}
```

- [ ] **Step 3: Napiš failing E2E scénář**

Vytvoř `PmTracker.Tests.E2E/Scenarios/ThemeBridgeScenariosTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Bez uložené volby platí motiv systému pro celou stránku; přepnutí se uloží do roční
/// cookie pmtracker.theme.mode (spec 2026-09-23 §12.4).
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class ThemeBridgeScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public ThemeBridgeScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task BezVolby_MotivPodleSystemu_APrepnutiSeUlozi()
    {
        var page = await _fixture.NewPageAsync();
        await page.EmulateMediaAsync(new PageEmulateMediaOptions { ColorScheme = ColorScheme.Dark });

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty?asUser={_fixture.AdminOsobaId}");

        (await page.EvaluateAsync<string>("document.documentElement.getAttribute('data-theme')"))
            .Should().Be("dark", "bez cookie platí motiv systému — i pro site.css, ne jen gov tokeny");

        await page.WaitForFunctionAsync(
            "document.querySelector('header gov-theme-switch')?.classList.contains('hydrated') === true");
        await page.Locator("header gov-theme-switch button").ClickAsync();
        await page.WaitForFunctionAsync("document.documentElement.getAttribute('data-theme') === 'light'");

        var cookies = await page.Context.CookiesAsync();
        cookies.Should().Contain(c => c.Name == "pmtracker.theme.mode" && c.Value == "light");

        await page.Context.CloseAsync();
    }
}
```

- [ ] **Step 4: Spusť — musí selhat**

```bash
dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ThemeBridgeTests"
dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ThemeServerRenderTests"
```

Expected: `ThemeBridgeTests` FAIL (`theme.js` má `localStorage`/`matchMedia`, layout nemá inline skript a vypisuje `data-theme-mode`); `ThemeServerRenderTests.VyslovnaVolba_SeVykresliNaServeru` FAIL na `data-theme-mode`. `BezCookie_AtributChybi` a `CookieAuto_SeChovaJakoBezVolby` projdou už teď (Razor vynechá atribut s hodnotou null) — hlídají, aby to přestavba nerozbila.

- [ ] **Step 5: Uprav `_Layout.cshtml` — server a inline skript**

Horní blok (ř. 3–10) a `<html>` (ř. 13) nahraď:

```cshtml
@{
    // Motiv: server vypíše data-theme jen pro výslovnou volbu uživatele (cookie ukládá
    // most v theme.js). Bez volby atribut chybí a inline skript v <head> dosadí motiv
    // podle systému — hodnota "auto" se nikdy nevypisuje, přepnula by jen gov tokeny.
    var themeModeCookie = Context.Request.Cookies["pmtracker.theme.mode"];
    var serverTheme = string.Equals(themeModeCookie, "dark", System.StringComparison.OrdinalIgnoreCase)
        ? "dark"
        : string.Equals(themeModeCookie, "light", System.StringComparison.OrdinalIgnoreCase)
            ? "light"
            : null;
}

<!DOCTYPE html>
<html lang="cs" data-theme="@serverTheme">
```

V `<head>` hned za `<title>` (před `GOV_DS_CONFIG` a před prvním `<link>`) vlož:

```cshtml
    @* Bez uložené volby motiv podle systému — ještě před prvním vykreslením CSS, jinak by
       gov tokeny (html:not([data-theme]) sledují systém) a site.css ([data-theme="dark"])
       ukázaly napůl tmavou stránku. Změnu systému za běhu nesledujeme. *@
    <script>
        (function () {
            var root = document.documentElement;
            if (!root.hasAttribute("data-theme")) {
                var dark = window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches;
                root.setAttribute("data-theme", dark ? "dark" : "light");
            }
        })();
    </script>
```

Komentář u přepínače (ř. 75–77, „theme.js zachycuje event, řídí 3-stavový model…") nahraď:

```cshtml
                <!-- gov-theme-switch (DS gov 4.7.0) přepíná motiv sám; theme.js jen ukládá volbu
                     do roční cookie pmtracker.theme.mode pro vykreslení na serveru. -->
```

- [ ] **Step 6: Přepiš `theme.js`**

Celý obsah `PmTracker.Web/wwwroot/js/modules/theme.js`:

```js
// Tenký most mezi nativním gov-theme-switch a serverem (spec 2026-09-23 §9.1, §12.4).
//
// Přepínání obstarává gov-theme-switch sám: nastaví data-theme na <html> a volbu si drží
// v session cookie „data-theme". Aplikace přidává jen trvalou cookie pmtracker.theme.mode,
// ze které _Layout vykreslí data-theme už na serveru — i po zavření prohlížeče tak stránka
// naběhne rovnou ve zvoleném motivu, bez probliknutí. Bez uložené volby dosadí motiv podle
// systému inline skript v <head> _Layout.
const themeCookieName = "pmtracker.theme.mode";
const themeCookieMaxAgeSeconds = 60 * 60 * 24 * 365;
const govThemeSwitchComponent = "gov-theme-switch";

let bound = false;

function persistTheme(theme) {
    document.cookie = `${themeCookieName}=${theme}; Path=/; Max-Age=${themeCookieMaxAgeSeconds}; SameSite=Lax`;
}

export function initTheme() {
    if (bound) {
        return;
    }

    bound = true;

    // gov-change bublá (Stencil: bubbles + composed) a posílají ho i jiné gov komponenty
    // (gov-dropdown, formuláře) — proto filtr na detail.component.
    document.addEventListener("gov-change", (event) => {
        const detail = event.detail;
        if (!detail || detail.component !== govThemeSwitchComponent) {
            return;
        }

        if (detail.state === "dark" || detail.state === "light") {
            persistTheme(detail.state);
        }
    });
}
```

- [ ] **Step 7: Uprav dotčené testy a wiki**

1. `DocumentationNavigationTests.Layout_ShouldRenderServerThemeAttributesFromCookie` — smaž řádek `html.Should().Contain("data-theme-mode=\"dark\"");` (cizí WIP v souboru → commit postupem „Commit souboru s cizím WIP").
2. `GovComponentsReplacementTests.cs:33-36` — doc komentář „theme.js zachycuje gov-change event a řídí 3-stavový model (light/dark/auto)" nahraď „přepínání obstarává gov-theme-switch sám; theme.js jen ukládá volbu do cookie pmtracker.theme.mode".
3. `GlobalSearchDynamicResultsTests.DarkMode_MaJedinouLupu_VDom` — blok s `[data-theme-switch-input]` (neexistuje, test dnes tmavý režim vůbec nezapne) nahraď nastavením cookie před `GotoAsync`:

```csharp
        var baseUri = new Uri(_fixture.BaseUrl);
        await page.Context.AddCookiesAsync(new[]
        {
            new Cookie { Name = "pmtracker.theme.mode", Value = "dark", Domain = baseUri.Host, Path = "/" }
        });

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty?asUser={_fixture.AdminOsobaId}");
        (await page.EvaluateAsync<string>("document.documentElement.getAttribute('data-theme')"))
            .Should().Be("dark");
```

4. `docs/wiki/profil/nastaveni-uzivatele.md` ř. 20–27 (sekce „Téma") nahraď:

```markdown
### Téma

| Volba | Vzhled |
|---|---|
| **Světlé** | Bílé pozadí, tmavý text |
| **Tmavé** | Tmavé pozadí, světlý text — vhodné pro večerní práci |

Dokud si téma nezvolíš, řídí se **nastavením systému** (Windows / prohlížeče).
Volbu uděláš přepínačem v hlavičce; pamatuje se rok v tomto prohlížeči (cookie
`pmtracker.theme.mode`).
```

- [ ] **Step 8: Spusť testy**

```bash
dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ThemeBridgeTests|FullyQualifiedName~ThemeSwitchGovComponentTests|FullyQualifiedName~GovComponentsReplacementTests"
dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ThemeServerRenderTests|FullyQualifiedName~DocumentationNavigationTests"
dotnet test PmTracker.Tests.E2E --filter "FullyQualifiedName~ThemeBridgeScenariosTests|FullyQualifiedName~GovComponentsRenderTests|FullyQualifiedName~GlobalSearchDynamicResultsTests"
```

Expected: PASS. `ThemeSwitchGovComponentTests.Layout_ObsahujeDataThemeSwitchRoot` zatím projde (obal `data-theme-switch` odstraní až Task 4).

- [ ] **Step 9: Commit**

```bash
git add PmTracker.Web/Views/Shared/_Layout.cshtml PmTracker.Web/wwwroot/js/modules/theme.js \
        PmTracker.Tests.Unit/Layout/ThemeBridgeTests.cs PmTracker.Tests.Unit/Layout/GovComponentsReplacementTests.cs \
        PmTracker.Tests.Api/Controllers/ThemeServerRenderTests.cs \
        PmTracker.Tests.E2E/Scenarios/ThemeBridgeScenariosTests.cs PmTracker.Tests.E2E/Scenarios/GlobalSearchDynamicResultsTests.cs \
        docs/wiki/profil/nastaveni-uzivatele.md
# DocumentationNavigationTests.cs: jen náš hunk (postup „Commit souboru s cizím WIP")
git commit -m "feat(gov): motiv přes nativní gov-theme-switch a cookie most" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 10: Vypiš stav a počkej na rozhodnutí uživatele.**

---

### Task 4: Standardní gov hlavička a hlavní navigace

**Files:**
- Modify: `PmTracker.Web/Views/Shared/_Layout.cshtml` (tělo od `<body>` po konec drobečků)
- Modify: `PmTracker.Web/wwwroot/css/site.css` (odstranění staré hlavičky, `app-` pravidla, sticky offsety)
- Modify: `PmTracker.Web/wwwroot/css/govcz.css:22,54` (`--app-nav-bg`)
- Modify: `PmTracker.Web/wwwroot/js/modules/layout/header-height.js`
- Modify: `PmTracker.Web/wwwroot/js/modules/pageSwitchers.js:487-523`, `navigation.js:34`, `bootstrap.js:51,662` (cizí WIP v `pageSwitchers.js` a `bootstrap.js`)
- Modify: `PmTracker.Tests.Unit/Layout/ThemeSwitchGovComponentTests.cs`, `PmTracker.Tests.Api/Controllers/DocumentationNavigationTests.cs` (cizí WIP)
- Modify (jen pracovní strom, necommitovat): `PmTracker.Tests.Unit/Layout/BreadcrumbAndMenuLayoutTests.cs`, `BreadcrumbBarMarkupTests.cs`
- Modify: `docs/known-issues/ds-fis-odchylky.md` (+ odchylky č. 4 a 5)
- Test: `PmTracker.Tests.Api/Controllers/LayoutGovHeaderRenderTests.cs`, `PmTracker.Tests.Unit/Layout/LayoutGovHeaderTests.cs`

**Interfaces:**
- Consumes: `initTheme()` (Task 3), `window.initTemplateScripts` (Task 2), ikona `person-fill` (Task 1).
- Produces: `<header class="gov-header">`, `<nav class="gov-navigation app-main-nav" id="main-navigation">`, `<div class="gov-skip-links">`; CSS proměnná `--app-sticky-top` v `:root` `site.css`; `--app-header-h` = vzdálenost vršku stránky od `#main`.

- [ ] **Step 1: Napiš failing Api testy hlavičky**

Vytvoř `PmTracker.Tests.Api/Controllers/LayoutGovHeaderRenderTests.cs`:

```csharp
using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Hlavička je standardní gov-header se skip-links a gov-navigation (spec 2026-09-23 §4.3).
/// Kotví se na atributy a ASCII — Razor kóduje diakritiku na entity.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class LayoutGovHeaderRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public LayoutGovHeaderRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<string> GetAsync(string path, int osobaId)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var separator = path.Contains('?') ? '&' : '?';
        var response = await client.GetAsync($"{path}{separator}asUser={osobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return html;
    }

    private static string NavSegment(string html)
    {
        var start = html.IndexOf("id=\"main-navigation\"", StringComparison.Ordinal);
        start.Should().BeGreaterThan(0, "hlavní navigace musí mít id main-navigation (cíl skip-linku)");
        var end = html.IndexOf("</nav>", start, StringComparison.Ordinal);
        return html[start..end];
    }

    [Fact]
    public async Task Hlavicka_JeGovHeader_SeSkipLinkyANavigaci()
    {
        var html = await GetAsync("/Projekty", _fixture.AdminOsobaId);

        html.Should().Contain("<div class=\"gov-skip-links\">");
        html.Should().Contain("href=\"#main-navigation\"");
        html.Should().Contain("href=\"#main\"");
        html.Should().Contain("<header class=\"gov-header\">");
        html.Should().Contain("class=\"gov-navigation app-main-nav\"");
        html.Should().Contain("js-gov-header__navigation-trigger", "hamburger pro úzký displej");
        html.Should().Contain("class=\"gov-search gov-search--fixed-width app-search\"",
            "vyhledávání zůstává v hlavičce");

        html.Should().NotContain("app-header");
        html.Should().NotContain("app-topbar");
        html.Should().NotContain("class=\"app-nav");
        html.Should().NotContain("data-user-menu");
        html.Should().NotContain("class=\"skip-link\"");
    }

    [Fact]
    public async Task AktivniPolozka_MaAriaCurrent_PraveJednou()
    {
        var nav = NavSegment(await GetAsync("/Projekty", _fixture.AdminOsobaId));

        Regex.Matches(nav, "aria-current=\"page\"").Should().HaveCount(1);
        nav.Should().Contain("<a href=\"/Projekty\" aria-current=\"page\">");
    }

    [Fact]
    public async Task Dashboard_NezvyraznujeZadnouPolozku()
    {
        var nav = NavSegment(await GetAsync("/", _fixture.AdminOsobaId));
        nav.Should().NotContain("aria-current");
    }

    [Fact]
    public async Task Navigace_SkryvaSekceBezOpravneni()
    {
        var outsiderId = await _fixture.EnsurePersonAsync("ApiNavOutsider");

        // Dokumentace má jen [Authorize] → otevře ji i osoba bez rolí.
        var outsiderNav = NavSegment(await GetAsync("/Dokumentace/Uzivatelska-prirucka", outsiderId));
        outsiderNav.Should().Contain("href=\"/Projekty\"");
        outsiderNav.Should().Contain("href=\"/Jednani\"");
        outsiderNav.Should().NotContain("href=\"/Osoby\"");
        outsiderNav.Should().NotContain("href=\"/Ciselniky\"");
        outsiderNav.Should().NotContain("href=\"/Nastaveni\"");

        var adminNav = NavSegment(await GetAsync("/Dokumentace/Uzivatelska-prirucka", _fixture.AdminOsobaId));
        adminNav.Should().Contain("href=\"/Osoby\"");
        adminNav.Should().Contain("href=\"/Ciselniky\"");
        adminNav.Should().Contain("href=\"/Nastaveni\"");
    }

    [Fact]
    public async Task MenuUzivatele_JeGovDropdown_SOdkazyNaProfil()
    {
        var html = await GetAsync("/Projekty", _fixture.AdminOsobaId);

        html.Should().Contain("<gov-dropdown position=\"right\" class=\"app-user-menu\">");
        html.Should().Contain("name=\"person-fill\"");
        html.Should().Contain("href=\"/Profil\"");
        html.Should().Contain("href=\"/Profil#moje-prava\"");
    }
}
```

- [ ] **Step 2: Napiš failing unit testy úklidu a stackingu**

Vytvoř `PmTracker.Tests.Unit/Layout/LayoutGovHeaderTests.cs`:

```csharp
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Po přechodu na gov-header nezůstane mrtvé CSS/JS staré hlavičky, hlavička není přilepená
/// a nic po ní nepočítá s výškou přilepené hlavičky (spec 2026-09-23 §12.5).
/// </summary>
public sealed class LayoutGovHeaderTests
{
    private static string SiteCssBezKomentaru() =>
        Regex.Replace(File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css")), @"/\*.*?\*/", "",
            RegexOptions.Singleline);

    private static string Block(string css, string selector)
    {
        var match = Regex.Match(css, @"(^|\n)" + Regex.Escape(selector) + @"\s*\{[^}]*\}");
        match.Success.Should().BeTrue($"blok {selector} má existovat");
        return match.Value;
    }

    [Theory]
    [InlineData(@"\.app-header(?![\w-])")]
    [InlineData(@"\.app-topbar(?![\w-])")]
    [InlineData(@"\.app-brand(?![\w-])")]
    [InlineData(@"\.app-nav(?![\w-])")]
    [InlineData(@"\.app-nav-link(?![\w-])")]
    [InlineData(@"\.user-menu")]
    [InlineData(@"\.skip-link(?![\w-])")]
    [InlineData(@"\.app-user-tools(?![\w-])")]
    [InlineData(@"\.app-theme-switch(?![\w-])")]
    [InlineData(@"\[data-theme-switch\]")]
    public void SiteCss_NemaSelektoryStareHlavicky(string selectorPattern)
    {
        SiteCssBezKomentaru().Should().NotMatchRegex(selectorPattern);
    }

    [Fact]
    public void Js_NemaObsluhuStarehoMenuUzivatele()
    {
        var js = Directory.GetFiles(ResolvePath("PmTracker.Web/wwwroot/js"), "*.js", SearchOption.AllDirectories)
            .Select(File.ReadAllText);
        js.Should().NotContain(source => source.Contains("initUserMenu") || source.Contains("data-user-menu"));
    }

    [Fact]
    public void VyskaNadObsahem_SeMeriKMain()
    {
        var js = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/layout/header-height.js"));
        js.Should().Contain("document.getElementById(\"main\")");
        js.Should().NotContain(".app-header");
    }

    [Fact]
    public void DrobeckovaLista_NeniPrilepenaANeprekryvaHlavicku()
    {
        // Lišta leží pod gov-header. S position/z-index by překryla rozbalené menu uživatele.
        var block = Block(SiteCssBezKomentaru(), ".app-breadcrumb-bar");
        block.Should().NotContain("sticky");
        block.Should().NotContain("z-index");
    }

    [Theory]
    [InlineData(".docs-sidebar,\n.docs-toc")]
    [InlineData(".gantt-picker")]
    [InlineData(".ciselniky-sidebar")]
    [InlineData(".settings-sidebar")]
    public void PrilepenePanely_PouzivajiSpolecnyOdsazeni(string selector)
    {
        var block = Block(SiteCssBezKomentaru(), selector);
        block.Should().Contain("top: var(--app-sticky-top)",
            "hlavička už není přilepená — panely se lepí k hornímu okraji okna");
    }

    [Fact]
    public void Kotvy_NepocitajiSVyskouHlavicky()
    {
        var css = SiteCssBezKomentaru();
        css.Should().Contain("scroll-padding-top: var(--app-sticky-top)");
        css.Should().Contain("scroll-margin-top: var(--app-sticky-top)");
        css.Should().NotContain("scroll-padding-top: calc(var(--app-header-h");
        css.Should().NotContain("scroll-margin-top: calc(var(--app-header-h");
    }
}
```

- [ ] **Step 3: Spusť — musí selhat**

```bash
dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~LayoutGovHeaderTests"
dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~LayoutGovHeaderRenderTests"
```

Expected: FAIL (stará hlavička, sticky lišta, `initUserMenu`).

- [ ] **Step 4: Přepiš hlavičku v `_Layout.cshtml`**

Nahraď vše od `<body class="app-body">` (ř. 25) po konec bloku drobečků (`</header>` na ř. 132) tímto:

```cshtml
<body class="app-body">
    @{
        var currentController = (ViewContext.RouteData.Values["controller"]?.ToString() ?? string.Empty).ToLowerInvariant();
        var suppressActiveNavigation = string.Equals(currentController, "dashboard", System.StringComparison.OrdinalIgnoreCase);
        // aria-current="page" pro položku aktuální sekce; null = Razor atribut nevypíše.
        // Na dashboardu se nezvýrazňuje nic (stejně jako dřív NavClass).
        string? NavCurrent(string controller) => !suppressActiveNavigation && currentController == controller ? "page" : null;
        var nav = ViewData["NavPermissions"] as NavPermissionsViewModel;
        var roleList = nav?.CurrentUserRoles is { Count: > 0 } roles
            ? string.Join(", ", roles)
            : string.Empty;
        var profileUrl = Url.Action("Index", "Profil");
    }
    <div class="gov-skip-links">
        <a href="#main-navigation">Přeskočit na navigaci</a>
        <a href="#main">Přeskočit na obsah</a>
    </div>

    @* Hlavička a hlavní navigace: struktura z DesignSystem-FIS-v1.0.0/index.html bez DS FIS
       specifik (spec 2026-09-23 §4.3). Vanilla DS gov = bílá hlavička. Pod 48 em DS schová
       .gov-header__action (motiv, účet) a ukáže hamburger v .gov-header__mobile. *@
    <header class="gov-header">
        <div class="gov-header__divider">
            <div class="gov-header__content">
                <a class="gov-header__logo" href="@Url.Action("Index", "Dashboard")" aria-label="Zápiska – přejít na hlavní přehled">
                    <gov-flex align-items="center" gap="m" responsive="false">
                        <div class="app-logo" aria-hidden="true">
                            <img class="app-logo-image" src="~/images/zapiska-logo.svg" alt="" />
                        </div>
                        <div class="app-title">Zápiska</div>
                    </gov-flex>
                </a>

                @* Globální hledání v layoutu používá gov-form-search přímo (ne pm-search), protože
                   pm-search (Fáze 2D) zatím nepodporuje slot="button-erase" (clear query button)
                   ani atributy identifier + autocomplete na vnořeném gov-form-input.
                   Seznam výsledků kreslí global-search.js (odchylka č. 1); třídy gov-search dávají
                   šířku podle DS. *@
                <form data-global-search
                      method="get"
                      asp-controller="Search"
                      asp-action="Index"
                      class="gov-search gov-search--fixed-width app-search"
                      role="search">
                    <gov-form-search color="primary" size="m">
                        <gov-form-input slot="input" size="m" name="q" placeholder="Hledání"
                                        identifier="app-global-search-input"
                                        aria-label="Globální vyhledávání"
                                        autocomplete="off"></gov-form-input>
                        <gov-button slot="button-erase" size="s" color="primary" type="base"
                                    data-global-search-erase hidden aria-label="Smazat dotaz">
                            <gov-icon size="s" slot="icon-start" name="x" type="components"></gov-icon>
                        </gov-button>
                        <gov-button slot="button" color="primary" size="s" type="solid">
                            Hledat
                        </gov-button>
                    </gov-form-search>
                    <div data-global-search-dropdown class="app-search-dropdown" hidden></div>
                </form>

                <gov-flex class="gov-header__action" justify-content="flex-end" align-items="center" gap="m">
                    <!-- gov-theme-switch (DS gov 4.7.0) přepíná motiv sám; theme.js jen ukládá volbu
                         do roční cookie pmtracker.theme.mode pro vykreslení na serveru. -->
                    <gov-theme-switch size="m"
                                      aria-label-light="Přepnout na tmavý mód"
                                      aria-label-dark="Přepnout na světlý mód"
                                      label-light="Světlý mód"
                                      label-dark="Tmavý mód">
                    </gov-theme-switch>

                    <gov-dropdown position="right" class="app-user-menu">
                        <gov-button type="base" color="primary" size="m"
                                    aria-label="Účet uživatele @nav?.CurrentUserDisplayName">
                            <gov-icon slot="icon-start" name="person-fill" type="components"></gov-icon>
                            <span class="app-user-name">@nav?.CurrentUserDisplayName</span>
                            <gov-icon slot="icon-end" name="chevron-down" size="m" type="components"></gov-icon>
                        </gov-button>
                        <ul slot="list" role="menu">
                            <li role="presentation" class="app-user-menu__info">
                                <strong>@nav?.CurrentUserDisplayName</strong>
                                <span>@(string.IsNullOrWhiteSpace(nav?.CurrentUserEmail) ? "-" : nav?.CurrentUserEmail)</span>
                                <span>@nav?.CurrentUserOrg</span>
                                <span>@roleList</span>
                            </li>
                            <li role="separator"><hr class="app-user-menu__sep" /></li>
                            <li role="presentation">
                                <gov-button type="base" color="primary" size="m" expanded role="menuitem"
                                            href="@profileUrl">Můj profil</gov-button>
                            </li>
                            <li role="presentation">
                                <gov-button type="base" color="primary" size="m" expanded role="menuitem"
                                            href="@(profileUrl + "#moje-prava")">Moje práva</gov-button>
                            </li>
                        </ul>
                    </gov-dropdown>
                </gov-flex>

                <gov-flex class="gov-header__mobile" align-items="center" responsive="false">
                    <gov-button class="js-gov-header__navigation-trigger" type="base" color="primary" size="m"
                                aria-label="Zobrazit / skrýt menu" aria-expanded="false">
                        <gov-icon slot="icon-start" name="list" type="components"></gov-icon>
                    </gov-button>
                </gov-flex>
            </div>
        </div>

        <div class="gov-header__navigation js-gov-header__navigation"
             style="padding-block: var(--templates-margin-s); padding-inline: var(--templates-margin-l)">
            <nav class="gov-navigation app-main-nav" aria-label="Hlavní navigace" id="main-navigation">
                <ul>
                    <li><a href="@Url.Action("Index", "Dashboard")" aria-current="@NavCurrent("dashboard")">Přehled</a></li>
                    <li><a href="@Url.Action("Index", "Projekty")" aria-current="@NavCurrent("projekty")">Projekty</a></li>
                    @if (nav?.CanViewPeople == true)
                    {
                        <li><a href="@Url.Action("Index", "Osoby")" aria-current="@NavCurrent("osoby")">Osoby</a></li>
                    }
                    @if (nav?.CanViewCiselniky == true)
                    {
                        <li><a href="@Url.Action("Index", "Ciselniky")" aria-current="@NavCurrent("ciselniky")">Číselníky</a></li>
                    }
                    <li><a href="@Url.Action("Index", "Jednani")" aria-current="@NavCurrent("jednani")">Jednání</a></li>
                    @if (nav?.CanViewSettings == true)
                    {
                        <li><a href="@Url.Action("Index", "Nastaveni")" aria-current="@NavCurrent("nastaveni")">Nastavení</a></li>
                    }
                </ul>
            </nav>
        </div>
    </header>

    @{
        var breadcrumbTrail = ViewData["Breadcrumbs"] as PmTracker.Web.Models.ViewModels.BreadcrumbTrail;
    }
    @if (breadcrumbTrail is not null)
    {
        <partial name="_BreadcrumbBar" model="breadcrumbTrail" />
    }
```

Odkazy v navigaci jsou schválně prosté `<a href="@Url.Action(…)">` bez TagHelperu: u prostého elementu Razor atribut s hodnotou `null` spolehlivě vynechá. Zbytek layoutu (`<main>`, patička, skripty) zůstává.

- [ ] **Step 5: Uprav `site.css` — smaž starou hlavičku**

Smaž tyto bloky (čísla řádků podle stavu před taskem, mažeš-li odspodu, nesesunou se):
- ř. 56–69 `.skip-link`, `.skip-link:focus`
- ř. 71–78 `.app-header`; ř. 80–87 `.app-topbar`; ř. 89–106 `.app-brand`, `.app-brand:hover .app-title, .app-brand:focus-visible .app-title`, `.app-brand:focus-visible`
- ř. 138–141 `.app-user`; ř. 148–151 `.app-user-org`; ř. 153–157 `.app-user-tools` (**`.app-user-name` ponech** — používá ho tlačítko účtu)
- ř. 386–392 `[data-theme-switch]`, `.app-theme-switch`; ř. 394–483 `.user-menu` … `.user-menu-link-btn`
- ř. 650–675 `.app-nav`, `.app-nav-link`, `.app-nav-link:hover`, `.app-nav-link.active`
- ř. 6718–6728 `:root[data-theme="dark"] .skip-link`, `… .app-header`, `… .app-topbar`; ř. 6736–6738 `:root[data-theme="dark"] .user-menu-toggle:hover`
- v `@media (max-width: 900px)` (ř. 7295–7318): ze seznamu `.app-topbar, .app-nav, .app-main, .app-footer` vyhoď `.app-topbar` a `.app-nav`; smaž vnořené bloky `.app-topbar`, `.app-user-tools`, `.user-menu-toggle`
- ř. 7436–7443 celý `@media (max-width: 768px) { .app-search { … } }`

V `govcz.css` smaž `--app-nav-bg` (ř. 22 a 54). `--app-nav-active-border` **ponech** (používá ho i `site.css:2393`).

- [ ] **Step 6: Uprav `site.css` — vyhledávání, drobečky, sticky offsety**

1. `.app-search` (ř. 164–170) — šířku teď dává `.gov-search`:

```css
.app-search {
    position: relative;
    display: flex;
    align-items: stretch;
}
```

2. `.app-breadcrumb-bar` (ř. 678) — smaž `position: sticky;`, `top: 0;`, `z-index: 40;` a komentář nad blokem uprav na:

```css
/* Drobečková lišta (frame bar pod hlavičkou) — navigace zanoření. Hlavička není přilepená
   (spec 2026-09-23 §12.5), lišta odjíždí s ní; bez z-indexu, ať nepřekryje menu účtu.
   Modal se zavírá jen křížkem; tato lišta se zavírá ✕ = jdi na rodiče. */
```

3. Do prvního `:root` (ř. 9–46) za `--pm-font-mono` přidej:

```css
    /* Odstup přilepených postranních panelů a cílů kotev od horního okraje okna.
       Hlavička není přilepená (spec 2026-09-23 §12.5), proto nezávisí na její výšce. */
    --app-sticky-top: 16px;
```

4. `top` přilepených panelů → `top: var(--app-sticky-top);` v blocích `.docs-sidebar,\n.docs-toc` (dnes `94px`), `.gantt-picker` (`84px`), `.ciselniky-sidebar` (`74px`), `.settings-sidebar` (`74px`).
5. `.record-card[data-record-id], .schedule-card[data-schedule-record-id]` (ř. ~3741): `scroll-margin-top: var(--app-sticky-top);`, komentář nad ním:

```css
/* Scroll cíl překliku (crossTabNav scrollIntoView block:"start") — malá mezera od
   horního okraje. Hlavička není přilepená, s její výškou se nepočítá. */
```

6. `html { scroll-padding-top: … }` (ř. ~7445): `scroll-padding-top: var(--app-sticky-top);`, komentář „Odsazení kotev/scrollIntoView od horního okraje okna (hlavička není přilepená)."
7. Dashboard `.app-main--fluid:has(.dashboard-shell)` — `height: calc(100dvh - var(--app-header-h, 110px))`: nech výpočet, jen **změř** v prohlížeči na `/` (1920×1080) `document.getElementById('main').getBoundingClientRect().top` a dosaď naměřenou hodnotu místo `110px` (fallback pro první vykreslení před JS). Komentář nad blokem: „--app-header-h = vzdálenost od vršku stránky k #main (gov hlavička + navigace + drobečky), měří header-height.js".

- [ ] **Step 7: Přidej `app-` styly hlavičky**

Do `site.css` za blok `.app-search-dropdown` a související pravidla vyhledávání (konec sekce „End global search", ř. ~380) vlož:

```css
/* ── Hlavička gov (2026-09-23) ──────────────────────────────────────────── */

/* Menu účtu: řádek s identitou nad odkazy. Struktura podle index.html kitu
   (app-user-menu__info / __sep); DS FIS ji stylovala v ds-fis.css, který nenačítáme. */
.app-user-menu__info {
    display: grid;
    gap: 2px;
    max-width: 20rem;
    padding: var(--spacing-s) var(--spacing-m);
    white-space: normal;
}

.app-user-menu__info strong {
    font-size: 14px;
}

.app-user-menu__info span {
    font-size: 12px;
    color: var(--pm-text-muted);
}

.app-user-menu__sep {
    margin: var(--spacing-2xs) 0;
}

/* Aktivní položka hlavní navigace — odchylka č. 4 (docs/known-issues/ds-fis-odchylky.md):
   vanilla gov-navigation aktuální stránku vizuálně neoznačuje. Jen tokeny DS. */
.app-main-nav a[aria-current="page"] {
    background-color: var(--button-outlined-primary-hover);
    box-shadow: inset 0 -3px 0 var(--color-primary-600);
}
```

- [ ] **Step 8: Uprav JS**

1. `PmTracker.Web/wwwroot/js/modules/layout/header-height.js` — celý obsah:

```js
// Vystavuje --app-header-h = vzdálenost od vršku stránky k <main id="main"> (gov hlavička
// + hlavní navigace + případná drobečková lišta). Dashboard přehled ji používá pro
// height: calc(100dvh - var(--app-header-h)), aby se panely vešly na jednu obrazovku
// a patička spadla těsně pod fold. Hlavička není přilepená — jde o výšku nad obsahem
// při scrollu 0. Na úzkém viewportu se hlavička zalamuje → ResizeObserver + resize.
// CSS má fallback (var(--app-header-h, …)) pro první vykreslení / vypnutý JS.

function applyHeaderHeight(main) {
    const h = Math.round(main.getBoundingClientRect().top + window.scrollY);
    if (h > 0) {
        document.documentElement.style.setProperty("--app-header-h", `${h}px`);
    }
}

export function initHeaderHeightVar() {
    const main = document.getElementById("main");
    const header = document.querySelector(".gov-header");
    if (!main || !header) {
        return;
    }

    applyHeaderHeight(main);

    if (typeof ResizeObserver !== "undefined") {
        const observer = new ResizeObserver(() => applyHeaderHeight(main));
        observer.observe(header);
    }

    window.addEventListener("resize", () => applyHeaderHeight(main), { passive: true });
}

initHeaderHeightVar();
```

2. `pageSwitchers.js` — smaž celou funkci `export function initUserMenu() { … }` (ř. 487–523).
3. `navigation.js:34` — smaž `initUserMenu,` z re-exportu.
4. `bootstrap.js` — smaž `initUserMenu,` z importu (ř. 51) a `() => initUserMenu(),` z `runInitializers` (ř. 662). Komentář u importu `header-height.js` (ř. 14) nech.

- [ ] **Step 9: Uprav dotčené testy**

1. `ThemeSwitchGovComponentTests.cs` — smaž `Layout_ObsahujeDataThemeSwitchRoot` (obal `data-theme-switch` zanikl; most poslouchá na `document`).
2. `DocumentationNavigationTests.Layout_ShouldNotRenderThemeCycleMenuAction` — smaž `html.Should().Contain("data-theme-switch", …)` a komentář nad `<gov-theme-switch` aserci zkrať na „Hlavička renderuje nativní gov-theme-switch (DS gov 4.7.0).". Cizí WIP → commit jen našeho hunku.
3. Necommitnuté cizí testy (upravit, **necommitovat**):
   - `BreadcrumbAndMenuLayoutTests.cs` — smaž `UserMenuDropdown_StacksAbove_BreadcrumbBar` (`.user-menu-panel` zanikl; nový invariant hlídá `LayoutGovHeaderTests.DrobeckovaLista_NeniPrilepenaANeprekryvaHlavicku`) a z doc komentáře bod „1a)".
   - `BreadcrumbBarMarkupTests.cs` — `SiteCss_DefinesStickyBreadcrumbBar` přejmenuj na `SiteCss_DefinesBreadcrumbBar` a smaž aserci na `position:\s*sticky`.

- [ ] **Step 10: Zapiš odchylky č. 4 a 5**

Na konec `docs/known-issues/ds-fis-odchylky.md`:

```markdown
## 4. Zvýraznění aktivní položky hlavní navigace (2026-09-23)

**Čeho se týká:** `gov-navigation` v hlavičce.

**Co DS předepisuje:** `templates.css` nemá styl aktuální stránky — navigace ji vizuálně neoznačuje.

**Proč se odchylujeme:** aplikace má šest sekcí a uživatelé byli zvyklí vidět, kde jsou.

**Jak je odchylka provedena:** `_Layout.cshtml` dává aktivnímu odkazu `aria-current="page"`
(přístupnost, v souladu s DS). Vizuál řeší `.app-main-nav a[aria-current="page"]`
v `site.css` (třída `app-main-nav` na `<nav class="gov-navigation">`), jen tokeny DS.

**Podklad pro centrální úpravu DS:** doplnit do `templates.css` styl
`.gov-navigation a[aria-current="page"]`. Patří do DS gov.

## 5. Obsah stránek mimo `gov-container` a `gov-page-heading` (2026-09-23)

**Čeho se týká:** README kitu, pravidlo 2 („obsah se vkládá jen dovnitř `<gov-container id="main">`").

**Proč se odchylujeme:** rozsah přestavby je jen hlavička a patička (spec 2026-09-23 §2).
Stránky mají vlastní šířkové tiery (`app-main`, `app-main--fluid`) a drobečkovou lištu
`_BreadcrumbBar`; přestavba na `gov-container` + `gov-page-heading` by zasáhla všechny obrazovky.

**Jak je odchylka provedena:** obsah zůstává v `<main id="main" class="app-main">` (id `main`
je cíl skip-linku), drobečky v `_BreadcrumbBar` pod hlavičkou. Struktura hlavičky a patičky
odpovídá `index.html`.
```

- [ ] **Step 11: Spusť testy**

```bash
dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~Layout|FullyQualifiedName~Search|FullyQualifiedName~GovAssets470"
dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~LayoutGovHeaderRenderTests|FullyQualifiedName~DocumentationNavigationTests|FullyQualifiedName~Search"
dotnet test PmTracker.Tests.Unit
dotnet test PmTracker.Tests.Api
dotnet test PmTracker.Tests.E2E --filter "FullyQualifiedName~GovComponentsRenderTests|FullyQualifiedName~GlobalSearchDynamicResultsTests|FullyQualifiedName~Breadcrumb|FullyQualifiedName~ThemeBridge"
```

Expected: PASS, Unit/Api na baseline. `GovAssets470Tests.KazdaIkonaPouzitaVAplikaci_Existuje` teď kontroluje i `person-fill` a `list`.

- [ ] **Step 12: Ruční kontrola (předej uživateli)**

- [ ] Hlavička je bílá, logo + „Zápiska" vlevo, hledání uprostřed, motiv a účet vpravo; navigace pod ní.
- [ ] Aktivní sekce je zvýrazněná (Projekty, Osoby…), na Přehledu nic.
- [ ] Menu účtu: jméno, e-mail, organizace, role; Můj profil a Moje práva vedou na profil. Rozbalené menu **není schované** pod drobečkovou lištou ani obsahem (zkus na detailu projektu).
- [ ] Tab: první stisk ukáže skip-links „Přeskočit na navigaci / na obsah".
- [ ] Úzké okno (< 768 px): hamburger rozbalí navigaci.
- [ ] Vyhledávací dropdown se otevírá pod polem a nic ho nepřekrývá.
- [ ] Přehled (dashboard) vyplní obrazovku, patička je těsně pod ní; postranní panely Číselníků, Nastavení, dokumentace a výběr v Ganttu se lepí k hornímu okraji.

- [ ] **Step 13: Commit**

```bash
git add PmTracker.Web/Views/Shared/_Layout.cshtml PmTracker.Web/wwwroot/css/site.css PmTracker.Web/wwwroot/css/govcz.css \
        PmTracker.Web/wwwroot/js/modules/layout/header-height.js PmTracker.Web/wwwroot/js/modules/navigation.js \
        PmTracker.Tests.Unit/Layout/ThemeSwitchGovComponentTests.cs PmTracker.Tests.Unit/Layout/LayoutGovHeaderTests.cs \
        PmTracker.Tests.Api/Controllers/LayoutGovHeaderRenderTests.cs docs/known-issues/ds-fis-odchylky.md
# pageSwitchers.js, bootstrap.js, DocumentationNavigationTests.cs: jen naše hunky (postup „Commit souboru s cizím WIP")
# BreadcrumbAndMenuLayoutTests.cs, BreadcrumbBarMarkupTests.cs: NEcommitovat (cizí necommitnuté soubory)
git commit -m "feat(gov): standardní gov hlavička a hlavní navigace" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 14: Vypiš stav a počkej na rozhodnutí uživatele.**

---

### Task 5: Standardní gov patička

**Files:**
- Modify: `PmTracker.Web/Views/Shared/_Layout.cshtml` (`<footer class="app-footer">` … `</footer>`)
- Modify: `PmTracker.Web/wwwroot/css/site.css` (odstranění `.app-footer*`, + `.app-address`)
- Modify: `PmTracker.Web/wwwroot/css/govcz.css:28-32,60-64` (`--app-footer-*`)
- Test: `PmTracker.Tests.Api/Controllers/LayoutGovFooterRenderTests.cs`, doplnit `PmTracker.Tests.Unit/Layout/LayoutGovHeaderTests.cs`

**Interfaces:**
- Consumes: `IApplicationVersionProvider.DisplayVersion` (už injektovaný v layoutu).
- Produces: `<footer class="gov-footer">` se třemi sloupci a řádkem verze.

- [ ] **Step 1: Napiš failing Api test patičky**

Vytvoř `PmTracker.Tests.Api/Controllers/LayoutGovFooterRenderTests.cs`:

```csharp
using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Patička je standardní gov-footer se třemi sloupci jako dřív a řádkem verze
/// (spec 2026-09-23 §9.3, §12.7).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class LayoutGovFooterRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public LayoutGovFooterRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<string> FooterAsync()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        html.Should().NotContain("app-footer");
        var start = html.IndexOf("<footer class=\"gov-footer\">", StringComparison.Ordinal);
        start.Should().BeGreaterThan(0);
        return html[start..html.IndexOf("</footer>", start, StringComparison.Ordinal)];
    }

    [Fact]
    public async Task Paticka_MaOdkazyNaDokumentaci()
    {
        var footer = await FooterAsync();
        footer.Should().Contain("href=\"/Dokumentace/Uzivatelska-prirucka\"");
        footer.Should().Contain("href=\"/Dokumentace/Technicka/Strom-dokumentace\"");
        footer.Should().Contain("href=\"/Dokumentace/qa\"");
        footer.Should().Contain("href=\"/Dokumentace/Changelog\"");
    }

    [Fact]
    public async Task ExterniOdkazy_OteviraGovLinkExternal()
    {
        var footer = await FooterAsync();
        footer.Should().Contain("<gov-link href=\"https://servicedesk.fis.acr\" external size=\"s\">");
        footer.Should().Contain("href=\"https://www.fis.acr/fis\"");
        footer.Should().Contain("href=\"https://www.portalcechy.sis.acr\"");
    }

    [Fact]
    public async Task Paticka_MaKontaktyAVerzi()
    {
        var footer = await FooterAsync();
        footer.Should().Contain("<address class=\"app-address\">");
        footer.Should().Contain("973 200 840");
        footer.Should().Contain("DS gov.cz 4.7.0");
    }
}
```

Doplň do `PmTracker.Tests.Unit/Layout/LayoutGovHeaderTests.cs` do `[Theory]` `SiteCss_NemaSelektoryStareHlavicky`:

```csharp
    [InlineData(@"\.app-footer")]
```

- [ ] **Step 2: Spusť — musí selhat**

```bash
dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~LayoutGovFooterRenderTests"
dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~LayoutGovHeaderTests"
```

Expected: FAIL (`app-footer` v HTML i v CSS).

- [ ] **Step 3: Přepiš patičku v `_Layout.cshtml`**

Nahraď `<footer class="app-footer">` … `</footer>`:

```cshtml
    @* Patička: struktura z DesignSystem-FIS-v1.0.0/index.html (gov-footer), obsah jako dřív
       (spec 2026-09-23 §12.7). Copyright z kitu se nepřidává. *@
    <footer class="gov-footer">
        <gov-flex class="gov-footer__content" direction="column" justify-content="space-between" gap="3xl">
            <gov-flex responsive="false">
                <ul class="gov-footer__main gov-list--plain">
                    <li>
                        <nav aria-labelledby="footer-dokumenty">
                            <h5 id="footer-dokumenty">Dokumenty</h5>
                            <ul class="gov-list--plain">
                                <li><gov-link href="@Url.Action("UzivatelskaPrirucka", "Dokumentace")" size="s">Uživatelská příručka</gov-link></li>
                                <li><gov-link href="@Url.Action("StromDokumentace", "Dokumentace")" size="s">Technická dokumentace</gov-link></li>
                                <li><gov-link href="@Url.Action("Qa", "Dokumentace")" size="s">Q and A</gov-link></li>
                                <li><gov-link href="@Url.Action("Changelog", "Dokumentace")" size="s">Changelog verzí</gov-link></li>
                            </ul>
                        </nav>
                    </li>
                    <li>
                        <nav aria-labelledby="footer-informace">
                            <h5 id="footer-informace">Informace</h5>
                            <ul class="gov-list--plain">
                                <li><gov-link href="https://servicedesk.fis.acr" external size="s">Servicedesk</gov-link></li>
                                <li><gov-link href="https://www.fis.acr/fis" external size="s">Portál FIS</gov-link></li>
                                <li><gov-link href="https://www.portalcechy.sis.acr" external size="s">Portál ŠIS</gov-link></li>
                            </ul>
                        </nav>
                    </li>
                    <li>
                        <h5 id="footer-podpora">Podpora</h5>
                        @* app-address: index.css (nenačítá se, odchylka č. 2) by jinak zrušil kurzívu <address>. *@
                        <address class="app-address">
                            <ul class="gov-footer__address gov-list--plain">
                                <li>FIS: 973 200 840</li>
                                <li>ISSP: 973 225 500</li>
                                <li>ŠIS: 973 211 111</li>
                            </ul>
                        </address>
                    </li>
                </ul>
            </gov-flex>

            <gov-flex direction="column" gap="m-nudge">
                <hr />
                <gov-flex class="gov-footer-copy" justify-content="space-between" gap="m">
                    <span>Verze @ApplicationVersionProvider.DisplayVersion · DS gov.cz 4.7.0</span>
                </gov-flex>
            </gov-flex>
        </gov-flex>
    </footer>
```

Ověř v HTML, že `Url.Action(…)` dává přesně cesty z testu (atributové routy `DokumentaceController`) — test to pohlídá.

- [ ] **Step 4: Uprav CSS**

1. `site.css` — smaž bloky `.app-footer`, `.app-footer-grid`, `.app-footer h3`, `.app-footer a, .app-footer span`, `.app-footer a:hover`, `.app-footer a:last-child, .app-footer span:last-child`, `.app-footer-meta`, `.app-footer-version` (dnes ř. 1176–1229); v `@media (max-width: 900px)` vyhoď `.app-footer` ze seznamu (zbude `.app-main`); smaž `.app-footer-grid { gap: 10px; }` (ř. ~7432).
2. `site.css` — za blok `.app-main-nav a[aria-current="page"]` (Task 4) přidej:

```css
/* Kontakty v patičce: index.css DS se nenačítá (odchylka č. 2), <address> by zůstal kurzívou. */
.app-address {
    font-style: normal;
}
```

3. `govcz.css` — smaž `--app-footer-bg`, `--app-footer-text`, `--app-footer-title`, `--app-footer-meta`, `--app-footer-divider` (ř. 28–32 a 60–64).

Poznámka: `.app-footer { margin-top: auto }` tlačil patičku ke spodku; totéž zajišťuje `.app-main { flex: 1 }` v `.app-body` (flex sloupec). Na dashboardu má `main` pevnou výšku a patička jde hned pod něj — stejně jako dřív.

- [ ] **Step 5: Spusť testy**

```bash
dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~LayoutGovFooterRenderTests|FullyQualifiedName~DocumentationNavigationTests"
dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~Layout"
```

Expected: PASS (vč. `DocumentationNavigationTests.Layout_ShouldContainUpdatedTechnicalDocumentationLink`).

- [ ] **Step 6: Ruční kontrola (předej uživateli)**

- [ ] Patička: tři sloupce (Dokumenty / Informace / Podpora), externí odkazy otevírají novou kartu s ikonou, řádek „Verze … · DS gov.cz 4.7.0".
- [ ] Krátká stránka (např. prázdné hledání): patička drží spodní okraj okna.
- [ ] Tmavý motiv: patička i hlavička mají tmavou variantu DS.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Views/Shared/_Layout.cshtml PmTracker.Web/wwwroot/css/site.css PmTracker.Web/wwwroot/css/govcz.css \
        PmTracker.Tests.Api/Controllers/LayoutGovFooterRenderTests.cs PmTracker.Tests.Unit/Layout/LayoutGovHeaderTests.cs
git commit -m "feat(gov): standardní gov patička" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 8: Vypiš stav a počkej na rozhodnutí uživatele.**

---

### Task 6: Odstranění gov 4.2.9, dokumentace a závěrečné ověření

**Files:**
- Delete: `PmTracker.Web/wwwroot/lib/gov-design-system/` (224 souborů v gitu)
- Modify: `PmTracker.Web/wwwroot/pm-modal-harness.html:6,32`
- Modify: `PmTracker.Tests.Unit/Layout/GovDialogVariantWidthTests.cs` (komentáře s cestou 4.2.9)
- Modify: `docs/specs/offline-deployment.md:44-70`, `docs/specs/gov-design-system-integration.md:3-4`, `docs/architecture/upgrade-gov-ds.md`, `docs/architecture/icons.md:22-27`, `docs/architecture/dialogs.md:5`
- Test: `PmTracker.Tests.Unit/Layout/GovHeadLoadOrderTests.cs` (doplnit)

**Interfaces:**
- Consumes: vše z Task 1–5.
- Produces: jediné jádro DS v aplikaci = `wwwroot/assets/gov` (4.7.0).

- [ ] **Step 1: Napiš failing test — stará knihovna neexistuje**

Do `GovHeadLoadOrderTests.cs` přidej:

```csharp
    [Fact]
    public void StaraKnihovna429_UzNeexistuje()
    {
        Directory.Exists(ResolvePath("PmTracker.Web/wwwroot/lib/gov-design-system")).Should().BeFalse(
            "dvě jádra DS by se přela o definice komponent; jediné jádro je assets/gov (4.7.0)");

        var harness = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/pm-modal-harness.html"));
        harness.Should().NotContain("lib/gov-design-system");
    }
```

- [ ] **Step 2: Spusť — musí selhat**

```bash
dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~StaraKnihovna429_UzNeexistuje"
```

Expected: FAIL.

- [ ] **Step 3: Smaž 4.2.9 a přesměruj harness**

```bash
git rm -r -q PmTracker.Web/wwwroot/lib/gov-design-system
grep -rn "lib/gov-design-system" PmTracker.Web PmTracker.Tests.* --include='*.cs' --include='*.cshtml' --include='*.js' --include='*.css' --include='*.html' | grep -v "/bin/\|/obj/"
```

Druhý příkaz smí najít už jen `pm-modal-harness.html` a komentáře v `GovDialogVariantWidthTests.cs`.

V `pm-modal-harness.html` nahraď ř. 6:

```html
    <link rel="stylesheet" href="http://127.0.0.1:8766/assets/gov/styles/tokens.css">
    <link rel="stylesheet" href="http://127.0.0.1:8766/assets/gov/styles/styles.css">
    <link rel="stylesheet" href="http://127.0.0.1:8766/assets/gov/styles/components.css">
```

a ř. 32:

```html
    <script type="module" src="http://127.0.0.1:8766/assets/gov/components/core.esm.js"></script>
```

V `GovDialogVariantWidthTests.cs` v doc komentáři a `because` textech nahraď „gov-dialog 4.2.9 dist CSS" → „gov-dialog (DS gov 4.x, `assets/gov/styles/components.css`)" a „viz gov-design-system/styles/lib/html/components/gov-dialog.css" → „viz assets/gov/styles/components.css". Pravidlo `.gov-dialog__dialog { max-width: var(--max-width, 52.5rem); max-height: 75vh }` v 4.7.0 platí (ověřeno).

- [ ] **Step 4: Aktualizuj dokumentaci**

1. `docs/specs/offline-deployment.md`:
   - ř. 50: `~/lib/gov-design-system/` → `~/assets/gov/` (DS gov 4.7.0) a doplň odrážku „`GovAssets470Tests` — kopie DS, fonty a ikony leží lokálně";
   - řádek tabulky gov-design-system: `| gov-design-system | ~/assets/gov/ | 4.7.0 | předsestavený kit DesignSystem-FIS-v1.0.0/assets/gov (bez icons/, bez ds-fis/) |`;
   - ověřovací příkazy po publish:

```bash
ls publish/wwwroot/assets/gov/components/ | wc -l        # min 140 (core.esm.js + p-*.js)
ls publish/wwwroot/assets/gov/styles/components.css publish/wwwroot/assets/gov/fonts/roboto.css
ls publish/wwwroot/assets/icons/components/person-fill.svg
```

2. `docs/specs/gov-design-system-integration.md` ř. 3–4: „Gov Design System 4.7.0 je hostován lokálně v `PmTracker.Web/wwwroot/assets/gov/` (1:1 kopie předsestaveného kitu). Komponenty registruje `components/core.esm.js`, hlavičku a hlavní navigaci ovládá `templates/scripts.js` (`initTemplateScripts`)."
3. `docs/architecture/upgrade-gov-ds.md` — přepiš úvod a kroky 1–2:

```markdown
# Upgrade gov-design-system

Aplikace používá DS gov.cz lokálně v `PmTracker.Web/wwwroot/assets/gov/` jako **1:1 kopii
předsestaveného kitu** `DesignSystem-FIS-v1.0.0/assets/gov` (bez `icons/`). Soubory DS se
nikdy needitují (README kitu, pravidlo 1); odchylky jsou v `docs/known-issues/ds-fis-odchylky.md`.

Aktuální verze: `@gov-design-system-ce/{styles,templates,components,fonts}` **4.7.0**.

## Postup upgrade na novou verzi

### 1. Nahradit kopii DS

Sestav novou verzi kitu (`node build.mjs` v kitu) a přepiš složky
`components/`, `styles/`, `fonts/`, `templates/` v `wwwroot/assets/gov/` (celé, ne po souborech —
staré `p-*.js` smazat). `icons/` ani `ds-fis/` se nekopírují.

### 2. Ikony

Nové ikony sady kitu (`icons/components/`) doplň do `wwwroot/assets/icons/components/`,
**existující nepřepisuj**. `GovAssets470Tests.AplikacniIkony_JsouNadmnozinouKitu` obsahuje
seznam sady — aktualizuj ho.

### 3. Aktualizovat verze

- tento soubor, `docs/specs/offline-deployment.md` (tabulka + ověřovací příkazy),
- řádek verze v patičce `_Layout.cshtml` („DS gov.cz X.Y.Z") a `LayoutGovFooterRenderTests`.
```

   Sekce 3–6 původního souboru (breaking changes, testy, StyleGuide, commit) ponech a přečísluj na 4–7; v „Spustit testy" doplň `dotnet test PmTracker.Tests.Api`. Odstavec o `nomodule` ponech (platí dál).
4. `docs/architecture/icons.md` ř. 22–27 (sekce „Dostupné ikony"):

```markdown
## Dostupné ikony

`gov-icon type="components"` bere SVG z `wwwroot/assets/icons/components/{name}.svg`
(`window.GOV_DS_CONFIG.iconsPath` v `_Layout.cshtml`). Strom je aplikační: Bootstrap Icons
1.11.3, které stahujeme sami, + ikony sady DS gov 4.7.0, které jsme neměli (odchylka č. 3
v `docs/known-issues/ds-fis-odchylky.md`). Novou ikonu přidej jako SVG do této složky —
`GovAssets470Tests.KazdaIkonaPouzitaVAplikaci_Existuje` selže, když chybí.
```

5. `docs/architecture/dialogs.md` ř. 5: `gov-design-system 4.2.9` → `DS gov 4.7.0`.

- [ ] **Step 5: Plný běh testů**

```bash
dotnet build PmTracker.sln
dotnet test PmTracker.Tests.Unit
dotnet test PmTracker.Tests.Api
dotnet test PmTracker.Tests.Integration
dotnet test PmTracker.Tests.E2E
```

Expected: Unit na baseline, Api jen 4 známá selhání, Integration jen 1 známé, E2E porovnej s během z Task 2.

- [ ] **Step 6: Závěrečný ruční checklist (spec §8 — dělá uživatel)**

- [ ] Přehled (dashboard) — gov-button, gov-tag, gov-message, grafy ECharts.
- [ ] Detail projektu / záznamu — editor, gov-form-*, gov-form-switch, gov-stepper, harmonogram.
- [ ] Modály — gov-dialog zavírání křížkem, klik mimo nezavře.
- [ ] Formuláře — gov-form-input/select/group/control, validace.
- [ ] Ikony všude viditelné, v konzoli žádné 404.
- [ ] Tmavý motiv na 3 reprezentativních stránkách; bez cookie se řídí systémem.
- [ ] Hlavička, navigace, menu účtu, skip-links, hamburger, patička (Task 4–5).
- [ ] **Edge na i15**: rozložení hlavičky, charset (diakritika v CSS), písmo.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/wwwroot/pm-modal-harness.html PmTracker.Tests.Unit/Layout/GovHeadLoadOrderTests.cs \
        PmTracker.Tests.Unit/Layout/GovDialogVariantWidthTests.cs docs/specs/offline-deployment.md \
        docs/specs/gov-design-system-integration.md docs/architecture/upgrade-gov-ds.md \
        docs/architecture/icons.md docs/architecture/dialogs.md
# smazání lib/gov-design-system je už ve stage z `git rm`
git commit -m "chore(gov): odstranění gov 4.2.9 a aktualizace dokumentace" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 8: Vypiš souhrn celé přestavby a počkej na rozhodnutí uživatele** (publish jen na jeho pokyn; do `publish/` nikdy `.sql` ani `appsettings.json`).
