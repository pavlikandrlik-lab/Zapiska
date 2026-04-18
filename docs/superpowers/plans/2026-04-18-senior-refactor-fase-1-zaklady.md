# Senior refactor — Fáze 1: Architektonické základy — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Postavit v PM Tracker architektonickou kostru — design tokens, TagHelper vrstvu (`pm-button`, `pm-alert`, `pm-badge`, `pm-field`, `pm-icon`), JS event delegation s adaptérem `gov-click` → `click`, file-size warningy, living `/StyleGuide` stránku a 8 architektonických dokumentů — tak, aby následující fáze (tlačítka+responzivita, de-god-file, ticketing) měly čistou půdu bez improvizace.

**Architecture:** Thin wrapper pattern: Razor TagHelpers generují HTML s gov-design-system Web Components. Mapování `pm-*` atributů na gov atributy je centralizované v TagHelperech, dokumentováno v `docs/architecture/`. Vizuál aplikace zůstává beze změny — fáze 1 pouze instaluje infrastrukturu, existující views se nepřepisují.

**Tech Stack:** .NET 8, ASP.NET Core MVC, Razor TagHelpers, xUnit + FluentAssertions, gov-design-system 4.2.9 (Web Components), vanilla JS moduly + manuální bundle, CSS custom properties.

---

## File Structure

### Nové soubory

| Cesta | Odpovědnost |
|---|---|
| `PmTracker.Web/wwwroot/css/tokens.css` | Aplikační `--pm-*` CSS proměnné (aliasy gov tokenů + breakpointy + z-index) |
| `PmTracker.Web/TagHelpers/PmButtonTagHelper.cs` | `<pm-button>` → `<gov-button>` |
| `PmTracker.Web/TagHelpers/PmButtonVariant.cs` | Enum `Primary\|Secondary\|Destructive\|Ghost` |
| `PmTracker.Web/TagHelpers/PmComponentSize.cs` | Enum `Small\|Medium\|Large` — sdílený |
| `PmTracker.Web/TagHelpers/PmAlertTagHelper.cs` | `<pm-alert>` → `<gov-message>` |
| `PmTracker.Web/TagHelpers/PmAlertVariant.cs` | Enum `Info\|Success\|Warning\|Error` |
| `PmTracker.Web/TagHelpers/PmBadgeTagHelper.cs` | `<pm-badge>` → `<gov-tag>` |
| `PmTracker.Web/TagHelpers/PmBadgeVariant.cs` | Enum `Neutral\|Primary\|Success\|Warning\|Error` |
| `PmTracker.Web/TagHelpers/PmFieldTagHelper.cs` | `<pm-field>` → `<gov-form-control>` + label + input + message |
| `PmTracker.Web/TagHelpers/PmIconTagHelper.cs` | `<pm-icon>` → `<gov-icon>` |
| `PmTracker.Web/wwwroot/js/modules/eventBus.js` | `appEventBus` + `gov-click` → `click` adaptér |
| `PmTracker.Web/Controllers/StyleGuideController.cs` | `/StyleGuide` routing |
| `PmTracker.Web/Views/StyleGuide/Index.cshtml` | Living style guide stránka |
| `scripts/check-file-sizes.ps1` | Warning pro soubory nad limit |
| `scripts/check-file-sizes.sh` | Bash varianta pro CI na linuxu |
| `PmTracker.Web/wwwroot/.eslintrc.json` | JS `max-lines: 300` warning |
| `PmTracker.Tests.Unit/TagHelpers/PmButtonTagHelperTests.cs` | Unit testy |
| `PmTracker.Tests.Unit/TagHelpers/PmAlertTagHelperTests.cs` | Unit testy |
| `PmTracker.Tests.Unit/TagHelpers/PmBadgeTagHelperTests.cs` | Unit testy |
| `PmTracker.Tests.Unit/TagHelpers/PmFieldTagHelperTests.cs` | Unit testy |
| `PmTracker.Tests.Unit/TagHelpers/PmIconTagHelperTests.cs` | Unit testy |
| `PmTracker.Tests.Unit/Layout/TokensCssTests.cs` | Tokens.css přítomnost a integrace |
| `PmTracker.Tests.Unit/Layout/StyleGuidePageTests.cs` | Stránka se vykreslí + obsahuje komponenty |
| `PmTracker.Tests.E2E/Scenarios/StyleGuideRenderTests.cs` | Playwright ověření render a funkce |
| `docs/architecture/README.md` | Index |
| `docs/architecture/buttons.md` | pm-button API + mapping |
| `docs/architecture/alerts.md` | pm-alert API |
| `docs/architecture/badges.md` | pm-badge API |
| `docs/architecture/fields.md` | pm-field API |
| `docs/architecture/icons.md` | pm-icon API |
| `docs/architecture/tokens.md` | CSS tokens, breakpointy, z-index stack |
| `docs/architecture/js-modules.md` | JS modules pravidla + event bus |
| `docs/architecture/backend-layering.md` | Thin controller pravidlo |
| `docs/architecture/upgrade-gov-ds.md` | Postup upgrade gov DS verze |

### Modifikované soubory

| Cesta | Změna |
|---|---|
| `PmTracker.Web/Views/_ViewImports.cshtml` | `@addTagHelper *, PmTracker.Web` |
| `PmTracker.Web/Views/Shared/_Layout.cshtml` | Import `tokens.css`, script `eventBus.js` |
| `PmTracker.Web/wwwroot/js/site.bundle.js` | Přidat eventBus inline |

---

## Task 1: Setup větev, výchozí stav testů

**Files:**
- Modify: git working tree

- [ ] **Step 1: Ověřit větev `codex/senior-refactor-fase-1` aktivní**

Run: `cd "/Users/Pavel.Andrlik/Documents/PM Tracker" && git branch --show-current`
Expected: `codex/senior-refactor-fase-1`

- [ ] **Step 2: Zajistit čistý worktree kromě známých změn**

Run: `git status --short`
Expected: `M .claude/worktrees/pedantic-kowalevski` + `M .gitignore` (pre-existing, ignorovat), nic jiného. Pokud jiné změny → zastavit a rozhodnout.

- [ ] **Step 3: Zrefreshovat nuget + provést plný build**

Run: `dotnet restore && dotnet build PmTracker.sln --nologo`
Expected: `0 chyb, 0 upozornění`

- [ ] **Step 4: Zaznamenat výchozí počet testů**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --nologo 2>&1 | grep -E "Úspěšné:|Celkem:"`
Expected: Poznamenat číslo (očekáváme ~267). Toto je baseline.

---

## Task 2: `tokens.css` — design tokens

**Files:**
- Create: `PmTracker.Web/wwwroot/css/tokens.css`
- Modify: `PmTracker.Web/Views/Shared/_Layout.cshtml` (řádek po `core.min.css` link)
- Create: `PmTracker.Tests.Unit/Layout/TokensCssTests.cs`

- [ ] **Step 1: Napsat failing test**

Create `PmTracker.Tests.Unit/Layout/TokensCssTests.cs`:

```csharp
using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Layout;

public sealed class TokensCssTests
{
    private static DirectoryInfo RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return dir!;
    }

    [Fact]
    public void TokensCss_Existuje()
    {
        var path = Path.Combine(RepoRoot().FullName, "PmTracker.Web", "wwwroot", "css", "tokens.css");
        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void TokensCss_ObsahujePmColorAliasy()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot().FullName, "PmTracker.Web", "wwwroot", "css", "tokens.css"));
        content.Should().Contain("--pm-color-primary");
        content.Should().Contain("var(--gov-color-");
    }

    [Fact]
    public void TokensCss_ObsahujeSpacingAliasy()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot().FullName, "PmTracker.Web", "wwwroot", "css", "tokens.css"));
        content.Should().Contain("--pm-spacing-s");
        content.Should().Contain("--pm-spacing-m");
        content.Should().Contain("--pm-spacing-l");
    }

    [Fact]
    public void TokensCss_ObsahujeBreakpointy()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot().FullName, "PmTracker.Web", "wwwroot", "css", "tokens.css"));
        content.Should().Contain("--pm-bp-sm");
        content.Should().Contain("--pm-bp-md");
        content.Should().Contain("--pm-bp-lg");
        content.Should().Contain("--pm-bp-xl");
        content.Should().Contain("--pm-bp-2xl");
    }

    [Fact]
    public void TokensCss_ObsahujeZIndexStack()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot().FullName, "PmTracker.Web", "wwwroot", "css", "tokens.css"));
        content.Should().Contain("--pm-z-header");
        content.Should().Contain("--pm-z-dropdown");
        content.Should().Contain("--pm-z-modal");
    }

    [Fact]
    public void Layout_ImportujeTokensCss()
    {
        var layoutPath = Path.Combine(RepoRoot().FullName, "PmTracker.Web", "Views", "Shared", "_Layout.cshtml");
        var layout = File.ReadAllText(layoutPath);
        layout.Should().Contain("~/css/tokens.css", "tokens.css musí být načteno v layoutu");
    }

    [Fact]
    public void Layout_TokensCss_JePoGovCssAPredSiteCss()
    {
        var layoutPath = Path.Combine(RepoRoot().FullName, "PmTracker.Web", "Views", "Shared", "_Layout.cshtml");
        var layout = File.ReadAllText(layoutPath);
        var tokensIndex = layout.IndexOf("~/css/tokens.css", StringComparison.Ordinal);
        var coreCssIndex = layout.IndexOf("core.min.css", StringComparison.Ordinal);
        var siteCssIndex = layout.IndexOf("~/css/site.css", StringComparison.Ordinal);
        tokensIndex.Should().BeGreaterThan(coreCssIndex, "tokens.css musí být po gov core.min.css (přepisuje gov tokeny)");
        tokensIndex.Should().BeLessThan(siteCssIndex, "tokens.css musí být před site.css (aby ji site.css viděla)");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~TokensCssTests" --nologo`
Expected: 7 testů, všechny FAIL (soubor neexistuje, layout neimportuje).

- [ ] **Step 3: Vytvořit `tokens.css`**

Create `PmTracker.Web/wwwroot/css/tokens.css`:

```css
/*
 * PM Tracker design tokens
 * ---------------------------------------------------------
 * Tato vrstva přemapovává gov-design-system tokeny na aplikační
 * semantika (--pm-*). Všechen CSS v site.css a komponentách má
 * používat --pm-* proměnné, ne přímé --gov-* ani hardcoded hodnoty.
 *
 * Zdroj gov tokenů: ~/lib/gov-design-system/styles/lib/tokens.min.css
 * Dokumentace: docs/architecture/tokens.md
 */

:root {
    /* Barvy — semantické aliasy */
    --pm-color-primary: var(--gov-color-primary-600, #005ea5);
    --pm-color-primary-strong: var(--gov-color-primary-700, #003d6b);
    --pm-color-primary-soft: var(--gov-color-primary-100, #e6f0f9);
    --pm-color-success: var(--gov-color-success-600, #00703c);
    --pm-color-warning: var(--gov-color-warning-600, #b25000);
    --pm-color-error: var(--gov-color-error-600, #d4351c);
    --pm-color-neutral: var(--gov-color-secondary-500, #626262);

    --pm-color-text: var(--gov-color-secondary-900, #1d1d1b);
    --pm-color-text-muted: var(--gov-color-secondary-600, #505050);
    --pm-color-background: var(--gov-color-base-white, #ffffff);
    --pm-color-surface: var(--gov-color-secondary-50, #f5f5f5);
    --pm-color-border: var(--gov-color-secondary-200, #d0d0d0);

    /* Spacing — 4pt grid vycházející z gov */
    --pm-spacing-3xs: var(--gov-spacing-3xs, 2px);
    --pm-spacing-2xs: var(--gov-spacing-2xs, 4px);
    --pm-spacing-xs: var(--gov-spacing-xs, 8px);
    --pm-spacing-s: var(--gov-spacing-s, 12px);
    --pm-spacing-m: var(--gov-spacing-m, 16px);
    --pm-spacing-l: var(--gov-spacing-l, 24px);
    --pm-spacing-xl: var(--gov-spacing-xl, 32px);
    --pm-spacing-2xl: var(--gov-spacing-2xl, 48px);
    --pm-spacing-3xl: var(--gov-spacing-3xl, 64px);

    /* Radius */
    --pm-radius-s: var(--gov-radius-s, 4px);
    --pm-radius-m: var(--gov-radius-m, 6px);
    --pm-radius-l: var(--gov-radius-l, 12px);
    --pm-radius-round: 9999px;

    /* Typografie */
    --pm-font-family-body: var(--gov-font-family-sans, system-ui, -apple-system, "Segoe UI", sans-serif);
    --pm-font-family-display: var(--gov-font-family-sans, system-ui, -apple-system, "Segoe UI", sans-serif);
    --pm-font-size-xs: var(--gov-font-size-xs, 12px);
    --pm-font-size-s: var(--gov-font-size-s, 14px);
    --pm-font-size-m: var(--gov-font-size-m, 16px);
    --pm-font-size-l: var(--gov-font-size-l, 18px);
    --pm-font-size-xl: var(--gov-font-size-xl, 24px);
    --pm-font-weight-normal: 400;
    --pm-font-weight-medium: 500;
    --pm-font-weight-semibold: 600;
    --pm-font-weight-bold: 700;

    /* Breakpointy (shodné s gov DS numericky) */
    --pm-bp-sm: 576px;   /* malé tablety */
    --pm-bp-md: 768px;   /* tablety */
    --pm-bp-lg: 992px;   /* malé desktopy */
    --pm-bp-xl: 1200px;  /* standardní desktopy */
    --pm-bp-2xl: 1400px; /* wide-screen 1440p+ */

    /* Z-index stack (semantic layers) */
    --pm-z-base: 1;
    --pm-z-elevated: 10;
    --pm-z-sticky: 50;
    --pm-z-header: 100;
    --pm-z-dropdown: 200;
    --pm-z-overlay: 500;
    --pm-z-modal: 1000;
    --pm-z-toast: 2000;
}
```

- [ ] **Step 4: Přidat `tokens.css` do `_Layout.cshtml`**

Read `PmTracker.Web/Views/Shared/_Layout.cshtml` line 20 (after `core.min.css` link). Edit:

Before:
```html
    <link rel="stylesheet" href="~/lib/gov-design-system/dist/core/core.min.css" asp-append-version="true" />
    <link rel="stylesheet" href="~/css/govcz.css" asp-append-version="true" />
```

After:
```html
    <link rel="stylesheet" href="~/lib/gov-design-system/dist/core/core.min.css" asp-append-version="true" />
    <link rel="stylesheet" href="~/css/tokens.css" asp-append-version="true" />
    <link rel="stylesheet" href="~/css/govcz.css" asp-append-version="true" />
```

- [ ] **Step 5: Run tests**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~TokensCssTests" --nologo`
Expected: 7 PASS.

- [ ] **Step 6: Ověřit full unit suite**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --nologo 2>&1 | tail -3`
Expected: baseline + 7 = ~274 passed.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/wwwroot/css/tokens.css \
  PmTracker.Web/Views/Shared/_Layout.cshtml \
  PmTracker.Tests.Unit/Layout/TokensCssTests.cs
git commit -m "$(cat <<'EOF'
feat(tokens): add --pm-* design token layer

Zavádí PmTracker.Web/wwwroot/css/tokens.css jako aplikační semantickou
vrstvu nad gov-design-system tokeny. Definuje:

- Barvy (--pm-color-*) — aliasy gov-color-*
- Spacing (--pm-spacing-*) — 4pt grid
- Radius, typografie, breakpointy (--pm-bp-sm/md/lg/xl/2xl)
- Z-index stack (--pm-z-*) pro konzistentní vrstvení

Layout načítá tokens.css mezi gov core.min.css a site.css.
Stávající CSS se bude postupně migrovat (fáze 2+).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: TagHelper infrastruktura — enums a ViewImports

**Files:**
- Create: `PmTracker.Web/TagHelpers/PmComponentSize.cs`
- Modify: `PmTracker.Web/Views/_ViewImports.cshtml`

- [ ] **Step 1: Vytvořit sdílený enum pro velikost**

Create `PmTracker.Web/TagHelpers/PmComponentSize.cs`:

```csharp
namespace PmTracker.Web.TagHelpers;

/// <summary>
/// Sdílený enum pro velikost pm-* komponent.
/// Mapuje se v každém TagHelperu na gov atribut size="s|m|l".
/// </summary>
public enum PmComponentSize
{
    Small,
    Medium,
    Large
}

internal static class PmComponentSizeExtensions
{
    public static string ToGovAttribute(this PmComponentSize size) => size switch
    {
        PmComponentSize.Small => "s",
        PmComponentSize.Large => "l",
        _ => "m"
    };
}
```

- [ ] **Step 2: Přidat `@addTagHelper` do ViewImports**

Read `PmTracker.Web/Views/_ViewImports.cshtml`. Edit to add line after existing `@addTagHelper`:

```razor
@using PmTracker.Web
@using PmTracker.Web.Models.ViewModels
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@addTagHelper *, PmTracker.Web
```

- [ ] **Step 3: Build**

Run: `dotnet build PmTracker.sln --nologo`
Expected: 0 chyb.

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/TagHelpers/PmComponentSize.cs PmTracker.Web/Views/_ViewImports.cshtml
git commit -m "feat(taghelpers): setup TagHelper infrastruktura (PmComponentSize, ViewImports)

Zavádí sdílený enum PmComponentSize (Small/Medium/Large) jako základ
pro všechny pm-* TagHelpers. Rozšiřuje _ViewImports.cshtml o registraci
PmTracker.Web assembly jako TagHelper zdroj."
```

---

## Task 4: `PmButtonTagHelper`

**Files:**
- Create: `PmTracker.Web/TagHelpers/PmButtonVariant.cs`
- Create: `PmTracker.Web/TagHelpers/PmButtonTagHelper.cs`
- Create: `PmTracker.Tests.Unit/TagHelpers/PmButtonTagHelperTests.cs`

- [ ] **Step 1: Napsat failing testy**

Create `PmTracker.Tests.Unit/TagHelpers/PmButtonTagHelperTests.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PmTracker.Web.TagHelpers;

namespace PmTracker.Tests.Unit.TagHelpers;

public sealed class PmButtonTagHelperTests
{
    private static async Task<TagHelperOutput> RenderAsync(PmButtonTagHelper helper, string innerText = "Akce")
    {
        var ctx = new TagHelperContext(
            new TagHelperAttributeList(),
            new Dictionary<object, object>(),
            "test");

        var output = new TagHelperOutput(
            "pm-button",
            new TagHelperAttributeList(),
            (useCached, encoder) =>
            {
                var content = new DefaultTagHelperContent();
                content.SetHtmlContent(innerText);
                return Task.FromResult<TagHelperContent>(content);
            });

        await helper.ProcessAsync(ctx, output);
        return output;
    }

    [Fact]
    public async Task Primary_Rendruje_GovButtonSolidPrimary()
    {
        var helper = new PmButtonTagHelper { Variant = PmButtonVariant.Primary };
        var output = await RenderAsync(helper, "Uložit");
        output.TagName.Should().Be("gov-button");
        output.Attributes["color"].Value.Should().Be("primary");
        output.Attributes["type"].Value.Should().Be("solid");
        output.Attributes["size"].Value.Should().Be("m");
        (await output.GetChildContentAsync()).GetContent().Should().Contain("Uložit");
    }

    [Fact]
    public async Task Secondary_Rendruje_GovButtonOutlinedPrimary()
    {
        var helper = new PmButtonTagHelper { Variant = PmButtonVariant.Secondary };
        var output = await RenderAsync(helper);
        output.Attributes["color"].Value.Should().Be("primary");
        output.Attributes["type"].Value.Should().Be("outlined");
    }

    [Fact]
    public async Task Destructive_Rendruje_GovButtonSolidError()
    {
        var helper = new PmButtonTagHelper { Variant = PmButtonVariant.Destructive };
        var output = await RenderAsync(helper);
        output.Attributes["color"].Value.Should().Be("error");
        output.Attributes["type"].Value.Should().Be("solid");
    }

    [Fact]
    public async Task Ghost_Rendruje_GovButtonBaseNeutral()
    {
        var helper = new PmButtonTagHelper { Variant = PmButtonVariant.Ghost };
        var output = await RenderAsync(helper);
        output.Attributes["color"].Value.Should().Be("neutral");
        output.Attributes["type"].Value.Should().Be("base");
    }

    [Fact]
    public async Task Small_Size_NastaviAtributSize()
    {
        var helper = new PmButtonTagHelper { Size = PmComponentSize.Small };
        var output = await RenderAsync(helper);
        output.Attributes["size"].Value.Should().Be("s");
    }

    [Fact]
    public async Task Large_Size_NastaviAtributSize()
    {
        var helper = new PmButtonTagHelper { Size = PmComponentSize.Large };
        var output = await RenderAsync(helper);
        output.Attributes["size"].Value.Should().Be("l");
    }

    [Fact]
    public async Task Disabled_NastaviDisabledAtribut()
    {
        var helper = new PmButtonTagHelper { Disabled = true };
        var output = await RenderAsync(helper);
        output.Attributes.ContainsName("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task Icon_Vlozi_GovIconDoSlotIconStart()
    {
        var helper = new PmButtonTagHelper { Icon = "save" };
        var output = await RenderAsync(helper, "Uložit");
        var html = output.PreContent.GetContent() + output.Content.GetContent() + output.PostContent.GetContent();
        // Icon je přidán jako pre-content
        output.PreContent.GetContent().Should().Contain("<gov-icon");
        output.PreContent.GetContent().Should().Contain("slot=\"icon-start\"");
        output.PreContent.GetContent().Should().Contain("name=\"save\"");
    }

    [Fact]
    public async Task IconPositionEnd_Vlozi_GovIconDoSlotIconEnd()
    {
        var helper = new PmButtonTagHelper { Icon = "arrow-right", IconPosition = "end" };
        var output = await RenderAsync(helper);
        output.PostContent.GetContent().Should().Contain("slot=\"icon-end\"");
        output.PostContent.GetContent().Should().Contain("name=\"arrow-right\"");
    }

    [Fact]
    public async Task Href_Pridana_RendrujeGovButtonJakoOdkaz()
    {
        var helper = new PmButtonTagHelper { Href = "/Projekty/Detail/5" };
        var output = await RenderAsync(helper);
        output.Attributes["href"].Value.Should().Be("/Projekty/Detail/5");
    }

    [Fact]
    public async Task Type_Submit_NastaviNativeButtonType()
    {
        var helper = new PmButtonTagHelper { NativeType = "submit" };
        var output = await RenderAsync(helper);
        output.Attributes["native-type"].Value.Should().Be("submit");
    }

    [Fact]
    public async Task DefaultVariant_JeSecondary()
    {
        var helper = new PmButtonTagHelper();
        var output = await RenderAsync(helper);
        output.Attributes["type"].Value.Should().Be("outlined");
    }
}
```

- [ ] **Step 2: Run test to verify failure**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~PmButtonTagHelperTests" --nologo`
Expected: compile error — `PmButtonTagHelper`, `PmButtonVariant` neexistují.

- [ ] **Step 3: Vytvořit `PmButtonVariant`**

Create `PmTracker.Web/TagHelpers/PmButtonVariant.cs`:

```csharp
namespace PmTracker.Web.TagHelpers;

/// <summary>
/// Semantické varianty pro pm-button — nezávislé na gov atributech.
/// Mapování na gov-button color/type je centralizované v PmButtonTagHelperu.
/// </summary>
public enum PmButtonVariant
{
    /// <summary>Primární akce (uložit, potvrdit, odeslat).</summary>
    Primary,
    /// <summary>Sekundární akce (zrušit, zpět).</summary>
    Secondary,
    /// <summary>Destruktivní akce (smazat, odstranit).</summary>
    Destructive,
    /// <summary>Tiché akce (toggle, link-like button).</summary>
    Ghost
}
```

- [ ] **Step 4: Vytvořit `PmButtonTagHelper`**

Create `PmTracker.Web/TagHelpers/PmButtonTagHelper.cs`:

```csharp
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pm-button variant="primary" size="m" icon="save">Uložit</pm-button>
///
/// Renderuje gov-button s centralizovaným mapováním semantických variant
/// (Primary/Secondary/Destructive/Ghost) na gov atributy color/type.
///
/// Dokumentace: docs/architecture/buttons.md
/// </summary>
[HtmlTargetElement("pm-button")]
public sealed class PmButtonTagHelper : TagHelper
{
    /// <summary>Semantická varianta tlačítka.</summary>
    public PmButtonVariant Variant { get; set; } = PmButtonVariant.Secondary;

    /// <summary>Velikost: Small/Medium/Large.</summary>
    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    /// <summary>Ikona ze sady gov-icon (např. "save", "arrow-right").</summary>
    public string? Icon { get; set; }

    /// <summary>Pozice ikony: "start" (default) nebo "end".</summary>
    public string IconPosition { get; set; } = "start";

    /// <summary>Nativní typ HTML tlačítka: button (default), submit, reset.</summary>
    [HtmlAttributeName("native-type")]
    public string NativeType { get; set; } = "button";

    /// <summary>Zakázané tlačítko.</summary>
    public bool Disabled { get; set; }

    /// <summary>Pokud je nastavené, renderuje se jako odkaz (gov-button href=...).</summary>
    public string? Href { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-button";
        output.TagMode = TagMode.StartTagAndEndTag;

        var (color, type) = Variant switch
        {
            PmButtonVariant.Primary => ("primary", "solid"),
            PmButtonVariant.Secondary => ("primary", "outlined"),
            PmButtonVariant.Destructive => ("error", "solid"),
            PmButtonVariant.Ghost => ("neutral", "base"),
            _ => ("primary", "outlined")
        };

        output.Attributes.SetAttribute("color", color);
        output.Attributes.SetAttribute("type", type);
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());

        if (Disabled)
            output.Attributes.SetAttribute("disabled", "disabled");

        if (!string.IsNullOrWhiteSpace(Href))
            output.Attributes.SetAttribute("href", Href);

        output.Attributes.SetAttribute("native-type", NativeType);

        if (!string.IsNullOrWhiteSpace(Icon))
        {
            var iconHtml = $"<gov-icon slot=\"icon-{(IconPosition == "end" ? "end" : "start")}\" name=\"{Icon}\" type=\"components\"></gov-icon>";
            if (IconPosition == "end")
                output.PostContent.AppendHtml(iconHtml);
            else
                output.PreContent.AppendHtml(iconHtml);
        }

        // Zajistit, že child content se propaguje (default content)
        if (output.Content.IsModified == false)
        {
            var child = await output.GetChildContentAsync();
            output.Content.SetHtmlContent(child);
        }
    }
}
```

- [ ] **Step 5: Run tests — musí projít všechny**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~PmButtonTagHelperTests" --nologo`
Expected: 12 PASS.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/TagHelpers/PmButtonVariant.cs \
  PmTracker.Web/TagHelpers/PmButtonTagHelper.cs \
  PmTracker.Tests.Unit/TagHelpers/PmButtonTagHelperTests.cs
git commit -m "feat(taghelpers): PmButtonTagHelper (pm-button → gov-button)

Semantické varianty Primary/Secondary/Destructive/Ghost s centralizovaným
mapováním na gov-button atributy. Podporuje size, icon (start/end),
disabled, href, native-type. 12 unit testů.

Dokumentace bude dodána v tasku 11 (docs/architecture/buttons.md)."
```

---

## Task 5: `PmAlertTagHelper`

**Files:**
- Create: `PmTracker.Web/TagHelpers/PmAlertVariant.cs`
- Create: `PmTracker.Web/TagHelpers/PmAlertTagHelper.cs`
- Create: `PmTracker.Tests.Unit/TagHelpers/PmAlertTagHelperTests.cs`

- [ ] **Step 1: Napsat failing testy**

Create `PmTracker.Tests.Unit/TagHelpers/PmAlertTagHelperTests.cs`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PmTracker.Web.TagHelpers;

namespace PmTracker.Tests.Unit.TagHelpers;

public sealed class PmAlertTagHelperTests
{
    private static async Task<TagHelperOutput> RenderAsync(PmAlertTagHelper helper, string text = "Zpráva")
    {
        var ctx = new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "test");
        var output = new TagHelperOutput(
            "pm-alert",
            new TagHelperAttributeList(),
            (_, _) =>
            {
                var c = new DefaultTagHelperContent();
                c.SetHtmlContent(text);
                return Task.FromResult<TagHelperContent>(c);
            });
        await helper.ProcessAsync(ctx, output);
        return output;
    }

    [Fact]
    public async Task Info_Rendruje_GovMessagePrimary()
    {
        var output = await RenderAsync(new PmAlertTagHelper { Variant = PmAlertVariant.Info });
        output.TagName.Should().Be("gov-message");
        output.Attributes["color"].Value.Should().Be("primary");
    }

    [Fact]
    public async Task Success_Rendruje_GovMessageSuccess()
    {
        var output = await RenderAsync(new PmAlertTagHelper { Variant = PmAlertVariant.Success });
        output.Attributes["color"].Value.Should().Be("success");
    }

    [Fact]
    public async Task Warning_Rendruje_GovMessageWarning()
    {
        var output = await RenderAsync(new PmAlertTagHelper { Variant = PmAlertVariant.Warning });
        output.Attributes["color"].Value.Should().Be("warning");
    }

    [Fact]
    public async Task Error_Rendruje_GovMessageError()
    {
        var output = await RenderAsync(new PmAlertTagHelper { Variant = PmAlertVariant.Error });
        output.Attributes["color"].Value.Should().Be("error");
    }

    [Fact]
    public async Task DefaultVariant_JeInfo()
    {
        var output = await RenderAsync(new PmAlertTagHelper());
        output.Attributes["color"].Value.Should().Be("primary");
    }

    [Fact]
    public async Task Content_SePropaguje()
    {
        var output = await RenderAsync(new PmAlertTagHelper(), "Něco se pokazilo");
        (await output.GetChildContentAsync()).GetContent().Should().Contain("Něco se pokazilo");
    }
}
```

- [ ] **Step 2: Run test to verify failure**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~PmAlertTagHelperTests" --nologo`
Expected: compile error.

- [ ] **Step 3: Vytvořit `PmAlertVariant`**

Create `PmTracker.Web/TagHelpers/PmAlertVariant.cs`:

```csharp
namespace PmTracker.Web.TagHelpers;

/// <summary>
/// Semantické varianty pro pm-alert.
/// </summary>
public enum PmAlertVariant
{
    Info,
    Success,
    Warning,
    Error
}
```

- [ ] **Step 4: Vytvořit `PmAlertTagHelper`**

Create `PmTracker.Web/TagHelpers/PmAlertTagHelper.cs`:

```csharp
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pm-alert variant="warning">Text zprávy</pm-alert>
///
/// Renderuje gov-message. Variant mapuje na gov color atribut.
/// Dokumentace: docs/architecture/alerts.md
/// </summary>
[HtmlTargetElement("pm-alert")]
public sealed class PmAlertTagHelper : TagHelper
{
    public PmAlertVariant Variant { get; set; } = PmAlertVariant.Info;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-message";
        output.TagMode = TagMode.StartTagAndEndTag;

        var color = Variant switch
        {
            PmAlertVariant.Success => "success",
            PmAlertVariant.Warning => "warning",
            PmAlertVariant.Error => "error",
            _ => "primary"
        };
        output.Attributes.SetAttribute("color", color);

        if (output.Content.IsModified == false)
        {
            var child = await output.GetChildContentAsync();
            output.Content.SetHtmlContent(child);
        }
    }
}
```

- [ ] **Step 5: Run tests**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~PmAlertTagHelperTests" --nologo`
Expected: 6 PASS.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/TagHelpers/PmAlertVariant.cs \
  PmTracker.Web/TagHelpers/PmAlertTagHelper.cs \
  PmTracker.Tests.Unit/TagHelpers/PmAlertTagHelperTests.cs
git commit -m "feat(taghelpers): PmAlertTagHelper (pm-alert → gov-message)

Info/Success/Warning/Error varianty. 6 unit testů."
```

---

## Task 6: `PmBadgeTagHelper`

**Files:**
- Create: `PmTracker.Web/TagHelpers/PmBadgeVariant.cs`
- Create: `PmTracker.Web/TagHelpers/PmBadgeTagHelper.cs`
- Create: `PmTracker.Tests.Unit/TagHelpers/PmBadgeTagHelperTests.cs`

- [ ] **Step 1: Napsat failing testy**

Create `PmTracker.Tests.Unit/TagHelpers/PmBadgeTagHelperTests.cs`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PmTracker.Web.TagHelpers;

namespace PmTracker.Tests.Unit.TagHelpers;

public sealed class PmBadgeTagHelperTests
{
    private static async Task<TagHelperOutput> RenderAsync(PmBadgeTagHelper helper, string text = "Nové")
    {
        var ctx = new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "test");
        var output = new TagHelperOutput(
            "pm-badge",
            new TagHelperAttributeList(),
            (_, _) =>
            {
                var c = new DefaultTagHelperContent();
                c.SetHtmlContent(text);
                return Task.FromResult<TagHelperContent>(c);
            });
        await helper.ProcessAsync(ctx, output);
        return output;
    }

    [Fact]
    public async Task Neutral_Rendruje_GovTagNeutral()
    {
        var output = await RenderAsync(new PmBadgeTagHelper { Variant = PmBadgeVariant.Neutral });
        output.TagName.Should().Be("gov-tag");
        output.Attributes["color"].Value.Should().Be("neutral");
    }

    [Fact]
    public async Task Primary_Rendruje_GovTagPrimary()
    {
        var output = await RenderAsync(new PmBadgeTagHelper { Variant = PmBadgeVariant.Primary });
        output.Attributes["color"].Value.Should().Be("primary");
    }

    [Fact]
    public async Task Success_Rendruje_GovTagSuccess()
    {
        var output = await RenderAsync(new PmBadgeTagHelper { Variant = PmBadgeVariant.Success });
        output.Attributes["color"].Value.Should().Be("success");
    }

    [Fact]
    public async Task Warning_Rendruje_GovTagWarning()
    {
        var output = await RenderAsync(new PmBadgeTagHelper { Variant = PmBadgeVariant.Warning });
        output.Attributes["color"].Value.Should().Be("warning");
    }

    [Fact]
    public async Task Error_Rendruje_GovTagError()
    {
        var output = await RenderAsync(new PmBadgeTagHelper { Variant = PmBadgeVariant.Error });
        output.Attributes["color"].Value.Should().Be("error");
    }

    [Fact]
    public async Task Size_Small_Rendruje_Size_s()
    {
        var output = await RenderAsync(new PmBadgeTagHelper { Size = PmComponentSize.Small });
        output.Attributes["size"].Value.Should().Be("s");
    }

    [Fact]
    public async Task DefaultVariant_JeNeutral()
    {
        var output = await RenderAsync(new PmBadgeTagHelper());
        output.Attributes["color"].Value.Should().Be("neutral");
    }
}
```

- [ ] **Step 2: Run test to verify failure**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~PmBadgeTagHelperTests" --nologo`

- [ ] **Step 3: Vytvořit `PmBadgeVariant`**

Create `PmTracker.Web/TagHelpers/PmBadgeVariant.cs`:

```csharp
namespace PmTracker.Web.TagHelpers;

public enum PmBadgeVariant
{
    Neutral,
    Primary,
    Success,
    Warning,
    Error
}
```

- [ ] **Step 4: Vytvořit `PmBadgeTagHelper`**

Create `PmTracker.Web/TagHelpers/PmBadgeTagHelper.cs`:

```csharp
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pm-badge variant="success" size="s">Hotovo</pm-badge>
///
/// Renderuje gov-tag.
/// Dokumentace: docs/architecture/badges.md
/// </summary>
[HtmlTargetElement("pm-badge")]
public sealed class PmBadgeTagHelper : TagHelper
{
    public PmBadgeVariant Variant { get; set; } = PmBadgeVariant.Neutral;
    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-tag";
        output.TagMode = TagMode.StartTagAndEndTag;

        var color = Variant switch
        {
            PmBadgeVariant.Primary => "primary",
            PmBadgeVariant.Success => "success",
            PmBadgeVariant.Warning => "warning",
            PmBadgeVariant.Error => "error",
            _ => "neutral"
        };
        output.Attributes.SetAttribute("color", color);
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());

        if (output.Content.IsModified == false)
        {
            var child = await output.GetChildContentAsync();
            output.Content.SetHtmlContent(child);
        }
    }
}
```

- [ ] **Step 5: Run tests**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~PmBadgeTagHelperTests" --nologo`
Expected: 7 PASS.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/TagHelpers/PmBadgeVariant.cs \
  PmTracker.Web/TagHelpers/PmBadgeTagHelper.cs \
  PmTracker.Tests.Unit/TagHelpers/PmBadgeTagHelperTests.cs
git commit -m "feat(taghelpers): PmBadgeTagHelper (pm-badge → gov-tag)"
```

---

## Task 7: `PmIconTagHelper`

**Files:**
- Create: `PmTracker.Web/TagHelpers/PmIconTagHelper.cs`
- Create: `PmTracker.Tests.Unit/TagHelpers/PmIconTagHelperTests.cs`

- [ ] **Step 1: Napsat failing testy**

Create `PmTracker.Tests.Unit/TagHelpers/PmIconTagHelperTests.cs`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PmTracker.Web.TagHelpers;

namespace PmTracker.Tests.Unit.TagHelpers;

public sealed class PmIconTagHelperTests
{
    private static async Task<TagHelperOutput> RenderAsync(PmIconTagHelper helper)
    {
        var ctx = new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "test");
        var output = new TagHelperOutput(
            "pm-icon",
            new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(ctx, output);
        return output;
    }

    [Fact]
    public async Task Rendruje_GovIconSeJmenem()
    {
        var output = await RenderAsync(new PmIconTagHelper { Name = "check" });
        output.TagName.Should().Be("gov-icon");
        output.Attributes["name"].Value.Should().Be("check");
        output.Attributes["type"].Value.Should().Be("components");
    }

    [Fact]
    public async Task Slot_SePropaguje()
    {
        var output = await RenderAsync(new PmIconTagHelper { Name = "x", Slot = "icon-end" });
        output.Attributes["slot"].Value.Should().Be("icon-end");
    }

    [Fact]
    public async Task AriaHidden_JeDefault_True()
    {
        var output = await RenderAsync(new PmIconTagHelper { Name = "check" });
        output.Attributes["aria-hidden"].Value.Should().Be("true");
    }

    [Fact]
    public async Task AriaLabel_DeaktivujeAriaHidden()
    {
        var output = await RenderAsync(new PmIconTagHelper { Name = "check", AriaLabel = "Hotovo" });
        output.Attributes["aria-label"].Value.Should().Be("Hotovo");
        output.Attributes.ContainsName("aria-hidden").Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run test to verify failure**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~PmIconTagHelperTests" --nologo`

- [ ] **Step 3: Vytvořit `PmIconTagHelper`**

Create `PmTracker.Web/TagHelpers/PmIconTagHelper.cs`:

```csharp
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pm-icon name="save" aria-label="Uložit" />
///
/// Renderuje gov-icon. Dle defaultu je dekorativní (aria-hidden=true);
/// pokud je nastaven aria-label, bere se jako funkční ikona.
/// Dokumentace: docs/architecture/icons.md
/// </summary>
[HtmlTargetElement("pm-icon")]
public sealed class PmIconTagHelper : TagHelper
{
    public string Name { get; set; } = "";
    public string? Slot { get; set; }

    [HtmlAttributeName("aria-label")]
    public string? AriaLabel { get; set; }

    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-icon";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("name", Name);
        output.Attributes.SetAttribute("type", "components");

        if (!string.IsNullOrEmpty(Slot))
            output.Attributes.SetAttribute("slot", Slot);

        if (!string.IsNullOrEmpty(AriaLabel))
            output.Attributes.SetAttribute("aria-label", AriaLabel);
        else
            output.Attributes.SetAttribute("aria-hidden", "true");

        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~PmIconTagHelperTests" --nologo`
Expected: 4 PASS.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/TagHelpers/PmIconTagHelper.cs \
  PmTracker.Tests.Unit/TagHelpers/PmIconTagHelperTests.cs
git commit -m "feat(taghelpers): PmIconTagHelper (pm-icon → gov-icon)

Defaultně dekorativní (aria-hidden=true); aria-label přepíná na funkční."
```

---

## Task 8: `PmFieldTagHelper`

**Files:**
- Create: `PmTracker.Web/TagHelpers/PmFieldTagHelper.cs`
- Create: `PmTracker.Tests.Unit/TagHelpers/PmFieldTagHelperTests.cs`

- [ ] **Step 1: Napsat failing testy**

Create `PmTracker.Tests.Unit/TagHelpers/PmFieldTagHelperTests.cs`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PmTracker.Web.TagHelpers;

namespace PmTracker.Tests.Unit.TagHelpers;

public sealed class PmFieldTagHelperTests
{
    private static async Task<TagHelperOutput> RenderAsync(PmFieldTagHelper helper)
    {
        var ctx = new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "test");
        var output = new TagHelperOutput(
            "pm-field",
            new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(ctx, output);
        return output;
    }

    [Fact]
    public async Task Rendruje_GovFormControl_SLabelAInput()
    {
        var helper = new PmFieldTagHelper
        {
            Name = "email",
            Label = "E-mail",
            InputType = "email"
        };
        var output = await RenderAsync(helper);
        output.TagName.Should().Be("gov-form-control");

        var html = output.PreContent.GetContent() + output.Content.GetContent() + output.PostContent.GetContent();
        html.Should().Contain("<gov-form-label");
        html.Should().Contain("E-mail");
        html.Should().Contain("<gov-form-input");
        html.Should().Contain("name=\"email\"");
    }

    [Fact]
    public async Task Required_NastaviPovinnyAtribut_VInputu()
    {
        var helper = new PmFieldTagHelper { Name = "x", Label = "X", Required = true };
        var output = await RenderAsync(helper);
        var html = output.PreContent.GetContent() + output.Content.GetContent() + output.PostContent.GetContent();
        html.Should().Contain("required");
    }

    [Fact]
    public async Task Error_VlozGovFormMessageError()
    {
        var helper = new PmFieldTagHelper { Name = "x", Label = "X", Error = "Povinné pole" };
        var output = await RenderAsync(helper);
        var html = output.PreContent.GetContent() + output.Content.GetContent() + output.PostContent.GetContent();
        html.Should().Contain("<gov-form-message");
        html.Should().Contain("variant=\"error\"");
        html.Should().Contain("Povinné pole");
    }

    [Fact]
    public async Task Help_VlozGovFormMessageDefault()
    {
        var helper = new PmFieldTagHelper { Name = "x", Label = "X", Help = "Zadejte e-mail" };
        var output = await RenderAsync(helper);
        var html = output.PreContent.GetContent() + output.Content.GetContent() + output.PostContent.GetContent();
        html.Should().Contain("<gov-form-message");
        html.Should().Contain("Zadejte e-mail");
    }

    [Fact]
    public async Task InputType_Default_JeText()
    {
        var helper = new PmFieldTagHelper { Name = "x", Label = "X" };
        var output = await RenderAsync(helper);
        var html = output.PreContent.GetContent() + output.Content.GetContent() + output.PostContent.GetContent();
        html.Should().Contain("type=\"text\"");
    }

    [Fact]
    public async Task Value_Propage_DoInputu()
    {
        var helper = new PmFieldTagHelper { Name = "email", Label = "E-mail", Value = "a@b.cz" };
        var output = await RenderAsync(helper);
        var html = output.PreContent.GetContent() + output.Content.GetContent() + output.PostContent.GetContent();
        html.Should().Contain("value=\"a@b.cz\"");
    }
}
```

- [ ] **Step 2: Run test to verify failure**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~PmFieldTagHelperTests" --nologo`

- [ ] **Step 3: Vytvořit `PmFieldTagHelper`**

Create `PmTracker.Web/TagHelpers/PmFieldTagHelper.cs`:

```csharp
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pm-field name="email" label="E-mail" input-type="email" required />
///
/// Renderuje kompletní formulářové pole: gov-form-control → gov-form-label,
/// gov-form-input (přes slot), volitelně gov-form-message (help nebo error).
///
/// Dokumentace: docs/architecture/fields.md
/// </summary>
[HtmlTargetElement("pm-field")]
public sealed class PmFieldTagHelper : TagHelper
{
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";

    [HtmlAttributeName("input-type")]
    public string InputType { get; set; } = "text";

    public string? Value { get; set; }
    public string? Placeholder { get; set; }
    public string? Help { get; set; }
    public string? Error { get; set; }
    public bool Required { get; set; }
    public bool Disabled { get; set; }
    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-form-control";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());
        if (!string.IsNullOrEmpty(Error))
            output.Attributes.SetAttribute("invalid", "invalid");

        var id = $"pm-field-{Name}";
        var labelHtml = $"<gov-form-label slot=\"top\" for=\"{WebUtility.HtmlEncode(id)}\" size=\"{Size.ToGovAttribute()}\">{WebUtility.HtmlEncode(Label)}{(Required ? " <span aria-hidden=\"true\">*</span>" : "")}</gov-form-label>";

        var attrs = new System.Text.StringBuilder();
        attrs.Append($" name=\"{WebUtility.HtmlEncode(Name)}\"");
        attrs.Append($" input-type=\"{WebUtility.HtmlEncode(InputType)}\"");
        attrs.Append($" size=\"{Size.ToGovAttribute()}\"");
        if (!string.IsNullOrEmpty(Placeholder))
            attrs.Append($" placeholder=\"{WebUtility.HtmlEncode(Placeholder)}\"");
        if (!string.IsNullOrEmpty(Value))
            attrs.Append($" value=\"{WebUtility.HtmlEncode(Value)}\"");
        if (Required) attrs.Append(" required");
        if (Disabled) attrs.Append(" disabled");

        var inputHtml = $"<gov-form-input{attrs}><span class=\"element\"><input id=\"{WebUtility.HtmlEncode(id)}\" name=\"{WebUtility.HtmlEncode(Name)}\" type=\"{WebUtility.HtmlEncode(InputType)}\"{(!string.IsNullOrEmpty(Placeholder) ? $" placeholder=\"{WebUtility.HtmlEncode(Placeholder)}\"" : "")}{(!string.IsNullOrEmpty(Value) ? $" value=\"{WebUtility.HtmlEncode(Value)}\"" : "")}{(Required ? " required" : "")}{(Disabled ? " disabled" : "")} /></span></gov-form-input>";

        var messageHtml = "";
        if (!string.IsNullOrEmpty(Error))
            messageHtml = $"<gov-form-message slot=\"bottom\" variant=\"error\">{WebUtility.HtmlEncode(Error)}</gov-form-message>";
        else if (!string.IsNullOrEmpty(Help))
            messageHtml = $"<gov-form-message slot=\"bottom\">{WebUtility.HtmlEncode(Help)}</gov-form-message>";

        output.Content.SetHtmlContent(labelHtml + inputHtml + messageHtml);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~PmFieldTagHelperTests" --nologo`
Expected: 6 PASS.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/TagHelpers/PmFieldTagHelper.cs \
  PmTracker.Tests.Unit/TagHelpers/PmFieldTagHelperTests.cs
git commit -m "feat(taghelpers): PmFieldTagHelper (pm-field → gov-form-control)

Kompletní formulářové pole: label + input + help/error message.
Podporuje name/label/input-type/value/placeholder/help/error/required/disabled/size.
6 unit testů."
```

---

## Task 9: JS event bus — gov-click → click adaptér

**Files:**
- Create: `PmTracker.Web/wwwroot/js/modules/eventBus.js`
- Modify: `PmTracker.Web/wwwroot/js/site.bundle.js` (prepend + export)
- Modify: `PmTracker.Web/Views/Shared/_Layout.cshtml` (načíst bundle jak je)
- Create: `PmTracker.Tests.E2E/Scenarios/EventBusAdapterTests.cs`

- [ ] **Step 1: Vytvořit `eventBus.js` modul**

Create `PmTracker.Web/wwwroot/js/modules/eventBus.js`:

```javascript
// PmTracker event bus a adaptér pro gov-design-system Web Components
//
// Problém: gov-button emituje CustomEvent "gov-click", ne nativní "click".
// Existující JS posluchače (modals.js, filters.js, bootstrap.js) nasazují
// document.addEventListener("click", ...). Bez adaptéru by přechod na
// gov-button rozbil všechny click handlery.
//
// Řešení: adaptér přeloží gov-click na nativní click na stejném targetu.
// Tím zůstávají existující JS handlery funkční beze změny.
//
// Dokumentace: docs/architecture/js-modules.md

const dispatched = new WeakSet();

export function installGovClickAdapter(root = document) {
    root.addEventListener("gov-click", (event) => {
        const target = event.target;
        if (!(target instanceof Element)) return;
        // Zabránit rekurzi: pokud tento gov-click už byl přemapován, ignorovat
        if (dispatched.has(event)) return;
        dispatched.add(event);

        const native = new MouseEvent("click", {
            bubbles: true,
            cancelable: true,
            composed: true,
            detail: 1
        });
        target.dispatchEvent(native);
    });
}

export const appEventBus = {
    on(selector, eventName, handler) {
        document.addEventListener(eventName, (event) => {
            const target = event.target instanceof Element
                ? event.target.closest(selector)
                : null;
            if (target) handler(event, target);
        });
    }
};

// Autoinstall při načtení, pokud je document ready
if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", () => installGovClickAdapter());
} else {
    installGovClickAdapter();
}
```

- [ ] **Step 2: Synchronizovat do `site.bundle.js`**

Read `PmTracker.Web/wwwroot/js/site.bundle.js` — najít začátek modulu `// PmTracker.Web/wwwroot/js/modules/` (místo pro vložení). Nejsnadnější je přidat jako **první modul** v bundlu.

Edit `site.bundle.js`:

Before (near the top of the file after initial imports/IIFE wrapper):
```javascript
// PmTracker.Web/wwwroot/js/modules/ajax.js
```

After (prepend adapter inline module before ajax.js):
```javascript
// PmTracker.Web/wwwroot/js/modules/eventBus.js
(function installGovClickAdapter() {
  const dispatched = new WeakSet();
  document.addEventListener("gov-click", (event) => {
    const target = event.target;
    if (!(target instanceof Element)) return;
    if (dispatched.has(event)) return;
    dispatched.add(event);
    const native = new MouseEvent("click", { bubbles: true, cancelable: true, composed: true, detail: 1 });
    target.dispatchEvent(native);
  });
})();

// PmTracker.Web/wwwroot/js/modules/ajax.js
```

**Postup vkládání:** použij Edit tool s jasným `old_string` první nalezený `// PmTracker.Web/wwwroot/js/modules/` komentář a vlož IIFE před něj.

- [ ] **Step 3: Napsat E2E test adaptéru**

Create `PmTracker.Tests.E2E/Scenarios/EventBusAdapterTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

[Collection(E2ECollection.CollectionName)]
public sealed class EventBusAdapterTests
{
    private readonly E2ETestFixture _fixture;

    public EventBusAdapterTests(E2ETestFixture fixture) { _fixture = fixture; }

    [Fact]
    public async Task GovClick_BubblesAs_NativeClick()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/");

        await page.EvaluateAsync(@"() => {
            window.__clickCount = 0;
            document.addEventListener('click', () => { window.__clickCount += 1; });

            const gb = document.createElement('gov-button');
            gb.setAttribute('data-test', 'eventbus-adapter');
            document.body.appendChild(gb);
            gb.dispatchEvent(new CustomEvent('gov-click', { bubbles: true, composed: true }));
        }");

        var count = await page.EvaluateAsync<int>("() => window.__clickCount");
        count.Should().Be(1, "adaptér přeloží gov-click na nativní click");

        await page.Context.CloseAsync();
    }
}
```

- [ ] **Step 4: Verifikovat bundle synchronizaci**

Run: `grep -c "installGovClickAdapter\|gov-click" PmTracker.Web/wwwroot/js/site.bundle.js`
Expected: >= 2 (adaptér a jeho event listener).

- [ ] **Step 5: Build + unit testy**

Run: `dotnet build PmTracker.sln --nologo && dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --nologo 2>&1 | tail -3`
Expected: 0 chyb, unit testy ≥ baseline + dosud přidané.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/wwwroot/js/modules/eventBus.js \
  PmTracker.Web/wwwroot/js/site.bundle.js \
  PmTracker.Tests.E2E/Scenarios/EventBusAdapterTests.cs
git commit -m "feat(js): event bus + gov-click → click adaptér

eventBus.js poskytuje appEventBus.on() delegation a automaticky instaluje
adaptér pro gov-click eventy z gov Web Components. Existující JS handlery
nativního click zůstávají funkční po přechodu na <gov-button>.

Synchronizováno do site.bundle.js. E2E test ověří propagaci."
```

---

## Task 10: File-size warningy

**Files:**
- Create: `scripts/check-file-sizes.sh`
- Create: `scripts/check-file-sizes.ps1`
- Create: `PmTracker.Web/wwwroot/.eslintrc.json`
- Create: `PmTracker.Tests.Unit/Architecture/FileSizePolicyTests.cs`

- [ ] **Step 1: Napsat policy test**

Create `PmTracker.Tests.Unit/Architecture/FileSizePolicyTests.cs`:

```csharp
using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Policy testy — neselhávají na existující god-files (fáze 3 je rozbije),
/// ale ověří, že scripty pro kontrolu existují a ESLint config je na místě.
/// </summary>
public sealed class FileSizePolicyTests
{
    private static DirectoryInfo RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return dir!;
    }

    [Fact]
    public void CheckFileSizesShellScript_Existuje()
    {
        var path = Path.Combine(RepoRoot().FullName, "scripts", "check-file-sizes.sh");
        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void CheckFileSizesPowershellScript_Existuje()
    {
        var path = Path.Combine(RepoRoot().FullName, "scripts", "check-file-sizes.ps1");
        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void EslintConfig_ExistujeVeWwwroot_SMaxLines()
    {
        var path = Path.Combine(RepoRoot().FullName, "PmTracker.Web", "wwwroot", ".eslintrc.json");
        File.Exists(path).Should().BeTrue();
        var content = File.ReadAllText(path);
        content.Should().Contain("max-lines");
        content.Should().Contain("300");
    }

    [Fact]
    public void DocsArchitectureModulesDok_MaLimitNaRadky()
    {
        // Tento test vyžaduje Task 11 (docs) — teď jen nastaví předpoklad.
        // V Tasku 11 bude docs/architecture/js-modules.md vytvořen.
        // Zde pouze ověříme, že .eslintrc limit souhlasí s dokumentací.
        var eslintPath = Path.Combine(RepoRoot().FullName, "PmTracker.Web", "wwwroot", ".eslintrc.json");
        var eslintContent = File.ReadAllText(eslintPath);
        eslintContent.Should().Contain("\"max\": 300");
    }
}
```

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~FileSizePolicyTests" --nologo`
Expected: 4 FAIL.

- [ ] **Step 3: Vytvořit `scripts/check-file-sizes.sh`**

Create `scripts/check-file-sizes.sh`:

```bash
#!/usr/bin/env bash
# PM Tracker — file size policy warning
# Popisuje: docs/architecture/backend-layering.md, docs/architecture/js-modules.md
set -euo pipefail

LIMIT_CS=500
LIMIT_JS=300
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
THRESHOLD_EXCEEDED=0

echo "== PM Tracker file-size policy =="
echo "   C# limit: $LIMIT_CS řádků, JS limit: $LIMIT_JS řádků"
echo ""

while IFS= read -r -d '' file; do
    lines=$(wc -l < "$file")
    if [ "$lines" -gt "$LIMIT_CS" ]; then
        echo "::warning file=${file#$ROOT/},line=1::C# soubor má $lines řádků (limit $LIMIT_CS) — zvažte rozdělení"
        THRESHOLD_EXCEEDED=$((THRESHOLD_EXCEEDED + 1))
    fi
done < <(find "$ROOT/PmTracker.Web" "$ROOT/PmTracker.Data" -name "*.cs" -not -path "*/bin/*" -not -path "*/obj/*" -print0 2>/dev/null)

while IFS= read -r -d '' file; do
    lines=$(wc -l < "$file")
    if [ "$lines" -gt "$LIMIT_JS" ]; then
        echo "::warning file=${file#$ROOT/},line=1::JS modul má $lines řádků (limit $LIMIT_JS) — zvažte rozdělení"
        THRESHOLD_EXCEEDED=$((THRESHOLD_EXCEEDED + 1))
    fi
done < <(find "$ROOT/PmTracker.Web/wwwroot/js/modules" -name "*.js" -print0 2>/dev/null)

if [ "$THRESHOLD_EXCEEDED" -gt 0 ]; then
    echo ""
    echo "⚠ Nalezeno $THRESHOLD_EXCEEDED souborů nad limit. Script nevrací chybový kód (warning only)."
else
    echo "✓ Všechny soubory v limitu."
fi
exit 0
```

Make executable:
```bash
chmod +x scripts/check-file-sizes.sh
```

- [ ] **Step 4: Vytvořit `scripts/check-file-sizes.ps1`**

Create `scripts/check-file-sizes.ps1`:

```powershell
# PM Tracker — file size policy warning (Windows / CI)
param(
    [int]$LimitCs = 500,
    [int]$LimitJs = 300
)

$root = Resolve-Path "$PSScriptRoot/.."
$exceeded = 0

Write-Host "== PM Tracker file-size policy =="
Write-Host "   C# limit: $LimitCs řádků, JS limit: $LimitJs řádků"
Write-Host ""

Get-ChildItem -Path "$root/PmTracker.Web","$root/PmTracker.Data" -Recurse -Include *.cs `
    | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } `
    | ForEach-Object {
        $lines = (Get-Content $_.FullName).Count
        if ($lines -gt $LimitCs) {
            Write-Warning "$($_.FullName): $lines řádků (limit $LimitCs) — zvažte rozdělení"
            $script:exceeded++
        }
    }

Get-ChildItem -Path "$root/PmTracker.Web/wwwroot/js/modules" -Recurse -Include *.js `
    | ForEach-Object {
        $lines = (Get-Content $_.FullName).Count
        if ($lines -gt $LimitJs) {
            Write-Warning "$($_.FullName): $lines řádků (limit $LimitJs) — zvažte rozdělení"
            $script:exceeded++
        }
    }

if ($exceeded -gt 0) {
    Write-Host "`n⚠ Nalezeno $exceeded souborů nad limit."
} else {
    Write-Host "`n✓ Všechny soubory v limitu."
}
exit 0
```

- [ ] **Step 5: Vytvořit `.eslintrc.json`**

Create `PmTracker.Web/wwwroot/.eslintrc.json`:

```json
{
    "root": true,
    "parserOptions": {
        "ecmaVersion": "latest",
        "sourceType": "module"
    },
    "env": {
        "browser": true,
        "es2022": true
    },
    "rules": {
        "max-lines": ["warn", { "max": 300, "skipBlankLines": true, "skipComments": true }]
    },
    "ignorePatterns": [
        "site.bundle.js",
        "lib/"
    ]
}
```

- [ ] **Step 6: Run tests**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~FileSizePolicyTests" --nologo`
Expected: 4 PASS.

- [ ] **Step 7: Commit**

```bash
git add scripts/check-file-sizes.sh scripts/check-file-sizes.ps1 \
  PmTracker.Web/wwwroot/.eslintrc.json \
  PmTracker.Tests.Unit/Architecture/FileSizePolicyTests.cs
git commit -m "feat(architecture): file-size policy (JS 300 / C# 500 řádků warning)

check-file-sizes.sh a .ps1 varianty — CI / lokální kontrola. ESLint
max-lines warning pro JS moduly (bundle a knihovny jsou ignorované).
Stávající god-files zůstávají platné (fáze 3 je rozbije)."
```

---

## Task 11: StyleGuide controller a stránka

**Files:**
- Create: `PmTracker.Web/Controllers/StyleGuideController.cs`
- Create: `PmTracker.Web/Views/StyleGuide/Index.cshtml`
- Create: `PmTracker.Tests.Unit/Layout/StyleGuidePageTests.cs`

- [ ] **Step 1: Napsat failing test**

Create `PmTracker.Tests.Unit/Layout/StyleGuidePageTests.cs`:

```csharp
using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Layout;

public sealed class StyleGuidePageTests
{
    private static DirectoryInfo RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return dir!;
    }

    [Fact]
    public void Controller_Existuje()
    {
        var path = Path.Combine(RepoRoot().FullName, "PmTracker.Web", "Controllers", "StyleGuideController.cs");
        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void IndexView_Existuje_A_ObsahujePmKomponenty()
    {
        var path = Path.Combine(RepoRoot().FullName, "PmTracker.Web", "Views", "StyleGuide", "Index.cshtml");
        File.Exists(path).Should().BeTrue();
        var content = File.ReadAllText(path);
        content.Should().Contain("<pm-button");
        content.Should().Contain("<pm-alert");
        content.Should().Contain("<pm-badge");
        content.Should().Contain("<pm-field");
        content.Should().Contain("<pm-icon");
    }

    [Fact]
    public void IndexView_MaSekciProKazdouKomponentu()
    {
        var path = Path.Combine(RepoRoot().FullName, "PmTracker.Web", "Views", "StyleGuide", "Index.cshtml");
        var content = File.ReadAllText(path);
        content.Should().Contain("Tlačítka");
        content.Should().Contain("Alerty");
        content.Should().Contain("Badge");
        content.Should().Contain("Pole");
        content.Should().Contain("Ikony");
    }
}
```

- [ ] **Step 2: Run test**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~StyleGuidePageTests" --nologo`
Expected: FAIL.

- [ ] **Step 3: Vytvořit controller**

Create `PmTracker.Web/Controllers/StyleGuideController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

/// <summary>
/// Living style guide — přehled pm-* TagHelper komponent pro vývojáře.
/// Přístup: kdokoli přihlášený (nejde o citlivá data).
/// </summary>
public sealed class StyleGuideController : BaseController
{
    public StyleGuideController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
    }

    [HttpGet]
    public IActionResult Index() => View();
}
```

- [ ] **Step 4: Vytvořit view**

Create `PmTracker.Web/Views/StyleGuide/Index.cshtml`:

```razor
@{
    ViewData["Title"] = "Style Guide — pm-* komponenty";
}

<section class="styleguide-page">
    <header>
        <h1>PM Tracker — Style Guide</h1>
        <p>Přehled aplikačních UI komponent (<code>pm-*</code>). Úplná dokumentace: <code>docs/architecture/</code>.</p>
    </header>

    <article data-styleguide-section="tlacitka">
        <h2>Tlačítka (<code>pm-button</code>)</h2>

        <h3>Varianty</h3>
        <div class="styleguide-row">
            <pm-button variant="Primary">Primary</pm-button>
            <pm-button variant="Secondary">Secondary</pm-button>
            <pm-button variant="Destructive">Destructive</pm-button>
            <pm-button variant="Ghost">Ghost</pm-button>
        </div>

        <h3>Velikosti</h3>
        <div class="styleguide-row">
            <pm-button variant="Primary" size="Small">Small</pm-button>
            <pm-button variant="Primary" size="Medium">Medium</pm-button>
            <pm-button variant="Primary" size="Large">Large</pm-button>
        </div>

        <h3>S ikonou</h3>
        <div class="styleguide-row">
            <pm-button variant="Primary" icon="check">Uložit</pm-button>
            <pm-button variant="Secondary" icon="arrow-right" icon-position="end">Dále</pm-button>
        </div>

        <h3>Disabled / Link</h3>
        <div class="styleguide-row">
            <pm-button variant="Primary" disabled="true">Disabled</pm-button>
            <pm-button variant="Ghost" href="/">Link k /</pm-button>
        </div>
    </article>

    <article data-styleguide-section="alerty">
        <h2>Alerty (<code>pm-alert</code>)</h2>
        <pm-alert variant="Info">Informační zpráva pro uživatele.</pm-alert>
        <pm-alert variant="Success">Akce proběhla úspěšně.</pm-alert>
        <pm-alert variant="Warning">Upozornění — zkontrolujte vstupy.</pm-alert>
        <pm-alert variant="Error">Chyba — akce se nezdařila.</pm-alert>
    </article>

    <article data-styleguide-section="badge">
        <h2>Badge (<code>pm-badge</code>)</h2>
        <div class="styleguide-row">
            <pm-badge variant="Neutral">Neutral</pm-badge>
            <pm-badge variant="Primary">Primary</pm-badge>
            <pm-badge variant="Success">Success</pm-badge>
            <pm-badge variant="Warning">Warning</pm-badge>
            <pm-badge variant="Error">Error</pm-badge>
        </div>
    </article>

    <article data-styleguide-section="pole">
        <h2>Pole (<code>pm-field</code>)</h2>
        <pm-field name="jmeno" label="Jméno" placeholder="Zadejte jméno" />
        <pm-field name="email" label="E-mail" input-type="email" required="true" help="Slouží k zasílání notifikací" />
        <pm-field name="chyba" label="S chybou" error="Pole je povinné" />
    </article>

    <article data-styleguide-section="ikony">
        <h2>Ikony (<code>pm-icon</code>)</h2>
        <div class="styleguide-row">
            <pm-icon name="check" aria-label="Hotovo" />
            <pm-icon name="x" aria-label="Zavřít" />
            <pm-icon name="arrow-right" aria-label="Dále" />
        </div>
    </article>
</section>

<style>
.styleguide-page article { margin-block: var(--pm-spacing-xl); padding-block-end: var(--pm-spacing-l); border-bottom: 1px solid var(--pm-color-border); }
.styleguide-page h2 { margin-block-end: var(--pm-spacing-m); }
.styleguide-page h3 { margin-block: var(--pm-spacing-m) var(--pm-spacing-xs); font-size: var(--pm-font-size-m); color: var(--pm-color-text-muted); }
.styleguide-row { display: flex; gap: var(--pm-spacing-s); flex-wrap: wrap; align-items: center; }
</style>
```

- [ ] **Step 5: Run tests + build**

Run: `dotnet build PmTracker.sln --nologo && dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~StyleGuidePageTests" --nologo`
Expected: 3 PASS.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Controllers/StyleGuideController.cs \
  PmTracker.Web/Views/StyleGuide/Index.cshtml \
  PmTracker.Tests.Unit/Layout/StyleGuidePageTests.cs
git commit -m "feat(styleguide): /StyleGuide stránka s ukázkami pm-* komponent

Living dokumentace pro vývojáře: pm-button / pm-alert / pm-badge / pm-field
/ pm-icon se všemi variantami. Přístupné pro přihlášené uživatele."
```

---

## Task 12: Architektonická dokumentace

**Files:**
- Create: 9 souborů v `docs/architecture/`

- [ ] **Step 1: Vytvořit `docs/architecture/README.md`**

Create `docs/architecture/README.md`:

```markdown
# PM Tracker — architektonická dokumentace

Stavbní pravidla aplikace. Závazné pro všechny vývojáře interní i externí.

## Komponentní vrstva (pm-*)

Aplikace používá thin-wrapper TagHelpery nad gov-design-system Web Components.
Jeden wrapper = jeden bod změny při upgradu gov DS verze.

- [pm-button — tlačítka](buttons.md)
- [pm-alert — alerty](alerts.md)
- [pm-badge — štítky](badges.md)
- [pm-field — formulářová pole](fields.md)
- [pm-icon — ikony](icons.md)

## Infrastruktura

- [Design tokens (CSS proměnné)](tokens.md)
- [JS moduly a event bus](js-modules.md)
- [Backend layering — thin controller, services, repositories](backend-layering.md)
- [Upgrade gov-design-system verze](upgrade-gov-ds.md)

## Living style guide

`/StyleGuide` — běží v aplikaci. Ukazuje každou pm-* komponentu ve všech variantách.
```

- [ ] **Step 2: Vytvořit `docs/architecture/buttons.md`**

Create `docs/architecture/buttons.md`:

```markdown
# pm-button

Thin-wrapper nad `<gov-button>`. Jeden bod změny mapování při upgrade gov DS.

## Použití

```razor
<pm-button variant="Primary" size="Medium" icon="save">Uložit</pm-button>
<pm-button variant="Destructive" native-type="submit">Smazat</pm-button>
<pm-button variant="Ghost" href="/Projekty">Zpět na seznam</pm-button>
```

## Atributy

| Atribut | Typ | Default | Popis |
|---|---|---|---|
| `variant` | Primary\|Secondary\|Destructive\|Ghost | Secondary | Semantická varianta |
| `size` | Small\|Medium\|Large | Medium | Velikost |
| `icon` | string | — | Jméno ikony ze sady gov |
| `icon-position` | start\|end | start | Pozice ikony |
| `native-type` | button\|submit\|reset | button | HTML type atribut |
| `disabled` | bool | false | Zakázané |
| `href` | string | — | Pokud je, renderuje jako odkaz |

## Mapování na gov-button

| variant | gov color | gov type |
|---|---|---|
| Primary | primary | solid |
| Secondary | primary | outlined |
| Destructive | error | solid |
| Ghost | neutral | base |

## Kdy použít kterou variantu

- **Primary** — hlavní akce formu (Uložit, Potvrdit, Odeslat). **Max jeden primary per obrazovka.**
- **Secondary** — běžné akce (Zrušit, Zpět, Přidat). Několik na stránce je OK.
- **Destructive** — smazání, ztráta dat. Vždy s potvrzením.
- **Ghost** — tiché akce v tabulkách, inline (Rozbalit, Více).

## Přechod z .btn

| Starý markup | Nový markup |
|---|---|
| `<button class="btn primary">Uložit</button>` | `<pm-button variant="Primary" native-type="submit">Uložit</pm-button>` |
| `<button class="btn">Zrušit</button>` | `<pm-button variant="Secondary">Zrušit</pm-button>` |
| `<button class="btn danger">Smazat</button>` | `<pm-button variant="Destructive">Smazat</pm-button>` |
| `<button class="btn ghost small">Více</button>` | `<pm-button variant="Ghost" size="Small">Více</pm-button>` |
| `<a class="btn" href="...">Zpět</a>` | `<pm-button variant="Secondary" href="...">Zpět</pm-button>` |

## JS a eventy

`<gov-button>` emituje custom event `gov-click`. Aplikace má v `eventBus.js` adaptér,
který jej přemapuje na nativní `click`. Existující `addEventListener("click", ...)`
posluchače fungují beze změny.
```

- [ ] **Step 3: Vytvořit `docs/architecture/alerts.md`**

Create `docs/architecture/alerts.md`:

```markdown
# pm-alert

Wrapper nad `<gov-message>`.

## Použití

```razor
<pm-alert variant="Error">Něco se pokazilo.</pm-alert>
```

## Atributy

| Atribut | Typ | Default |
|---|---|---|
| `variant` | Info\|Success\|Warning\|Error | Info |

## Mapování

| variant | gov color |
|---|---|
| Info | primary |
| Success | success |
| Warning | warning |
| Error | error |

## Přechod

| Starý | Nový |
|---|---|
| `<div class="alert alert-error">X</div>` | `<pm-alert variant="Error">X</pm-alert>` |
| `<div class="alert warning">X</div>` | `<pm-alert variant="Warning">X</pm-alert>` |
```

- [ ] **Step 4: Vytvořit `docs/architecture/badges.md`**

Create `docs/architecture/badges.md`:

```markdown
# pm-badge

Wrapper nad `<gov-tag>`.

## Použití

```razor
<pm-badge variant="Success" size="Small">Hotovo</pm-badge>
```

## Atributy

| Atribut | Typ | Default |
|---|---|---|
| `variant` | Neutral\|Primary\|Success\|Warning\|Error | Neutral |
| `size` | Small\|Medium\|Large | Medium |

## Přechod

| Starý | Nový |
|---|---|
| `<span class="badge badge-success">OK</span>` | `<pm-badge variant="Success">OK</pm-badge>` |
| `<span class="badge badge-warning">Pozor</span>` | `<pm-badge variant="Warning">Pozor</pm-badge>` |
```

- [ ] **Step 5: Vytvořit `docs/architecture/fields.md`**

Create `docs/architecture/fields.md`:

```markdown
# pm-field

Kompletní formulářové pole: label + input + help/error message. Wrapper nad gov-form-control.

## Použití

```razor
<pm-field name="email" label="E-mail" input-type="email" required="true"
          help="Slouží k zasílání notifikací" />

<pm-field name="vek" label="Věk" input-type="number"
          error="@Model.Errors["vek"]" value="@Model.Vek" />
```

## Atributy

| Atribut | Typ | Default |
|---|---|---|
| `name` | string | — |
| `label` | string | — |
| `input-type` | text\|email\|number\|date\|... | text |
| `value` | string | — |
| `placeholder` | string | — |
| `help` | string | — (non-error pomoc) |
| `error` | string | — (pokud je, vykreslí error message + invalid) |
| `required` | bool | false |
| `disabled` | bool | false |
| `size` | Small\|Medium\|Large | Medium |

## Validace

Pokud Razor má `ModelState`, mapování chyb na `error` atribut:

```razor
<pm-field name="email" label="E-mail"
          error="@(ViewData.ModelState["email"]?.Errors.FirstOrDefault()?.ErrorMessage)" />
```

V budoucnu (fáze 2+) zvážíme model-binding variantu `<pm-field asp-for="Email" />`.
```

- [ ] **Step 6: Vytvořit `docs/architecture/icons.md`**

Create `docs/architecture/icons.md`:

```markdown
# pm-icon

Wrapper nad `<gov-icon>`. Automaticky dekorativní (aria-hidden); aria-label přepíná
na funkční.

## Použití

```razor
<pm-icon name="check" aria-label="Hotovo" />  <!-- funkční, předávána screen readerům -->
<pm-icon name="arrow-right" />                <!-- dekorativní, aria-hidden=true -->
<pm-icon name="x" slot="icon-end" aria-label="Zavřít" />
```

## Atributy

| Atribut | Typ | Default |
|---|---|---|
| `name` | string | — |
| `slot` | string | — (použij `icon-start`/`icon-end` pro sloty tlačítek) |
| `aria-label` | string | — (pokud nastaveno, aria-hidden se neaplikuje) |

## Dostupné ikony

Gov-design-system 4.2.9 poskytuje sadu `type="components"`. Názvy (výběr):
- `check`, `x`, `arrow-left`, `arrow-right`, `arrow-up`, `arrow-down`
- `search`, `pencil`, `trash`, `plus`, `minus`
- `info`, `warning`, `error`

Úplný seznam: https://designsystem.gov.cz/komponenty/ikony.html
```

- [ ] **Step 7: Vytvořit `docs/architecture/tokens.md`**

Create `docs/architecture/tokens.md`:

```markdown
# Design tokens

Aplikační CSS proměnné (`--pm-*`) jsou aliasy nad gov-design-system tokeny
(`--gov-*`). Zdroj: `PmTracker.Web/wwwroot/css/tokens.css`.

## Pravidla

1. **Žádné hardcoded hodnoty v CSS** — barva, spacing, radius, font-size → vždy token
2. **Používej --pm-*, ne --gov-*** v aplikačním CSS — `--pm-*` izoluje aplikaci od gov upgrade
3. **Breakpointy** v media queries jsou číselně shodné s gov DS
4. **Z-index** vždy z `--pm-z-*` stacku, nikdy ad-hoc číslo

## Kategorie tokenů

### Barvy

| Token | Význam |
|---|---|
| `--pm-color-primary` | Hlavní akční barva |
| `--pm-color-primary-strong` | Hover state |
| `--pm-color-primary-soft` | Pozadí primárních prvků |
| `--pm-color-success` | Úspěch / OK stav |
| `--pm-color-warning` | Upozornění |
| `--pm-color-error` | Chyba / destruktivní |
| `--pm-color-text` | Hlavní text |
| `--pm-color-text-muted` | Sekundární text |
| `--pm-color-background` | Pozadí stránky |
| `--pm-color-surface` | Pozadí karty / panelu |
| `--pm-color-border` | Hraniční linie |

### Spacing (4pt grid)

`--pm-spacing-3xs` (2px) až `--pm-spacing-3xl` (64px).

### Breakpointy

```css
@media (min-width: 576px) { /* --pm-bp-sm */ }
@media (min-width: 768px) { /* --pm-bp-md — tablety */ }
@media (min-width: 992px) { /* --pm-bp-lg — malé desktopy */ }
@media (min-width: 1200px) { /* --pm-bp-xl — standardní 1080p+ */ }
@media (min-width: 1400px) { /* --pm-bp-2xl — wide-screen 1440p+ */ }
```

### Z-index stack

| Token | Hodnota | Použití |
|---|---|---|
| `--pm-z-base` | 1 | default |
| `--pm-z-elevated` | 10 | karty, hover lift |
| `--pm-z-sticky` | 50 | sticky headery tabulky |
| `--pm-z-header` | 100 | app header |
| `--pm-z-dropdown` | 200 | dropdowny |
| `--pm-z-overlay` | 500 | overlay mimo modal |
| `--pm-z-modal` | 1000 | modaly |
| `--pm-z-toast` | 2000 | toasty nad vším |
```

- [ ] **Step 8: Vytvořit `docs/architecture/js-modules.md`**

Create `docs/architecture/js-modules.md`:

```markdown
# JS moduly — pravidla

## Limit 300 řádků / modul

Každý soubor v `PmTracker.Web/wwwroot/js/modules/*.js` má **měkký limit 300 řádků**
(skipBlankLines, skipComments). ESLint vypisuje warning, ne error.

Kontrola:
- Lokálně: ESLint s `PmTracker.Web/wwwroot/.eslintrc.json`
- CI: `scripts/check-file-sizes.sh`

## Struktura

Každý modul:

1. **Jedna zodpovědnost** — název souboru popisuje co dělá
2. **Explicit exports** — named exports, ne default
3. **Pure tam, kde možné** — helpery bez DOM/fetch isolovat
4. **Side-effectful moduly** jsou inicializované z `bootstrap.js`

## Bundle

Aplikace v prohlížeči načítá **jen `site.bundle.js`**. Moduly v `/modules/` jsou
pro vývoj / testování. Při změně v modulu synchronizuj do bundlu — viz
[synchronizace bundlu](../specs/modal-close-guard.md#synchronizace-bundle).

## Event bus

`eventBus.js` poskytuje:

- `appEventBus.on(selector, event, handler)` — delegation helper
- Automatický adaptér: gov-click → nativní click

Existující `addEventListener("click", ...)` posluchače fungují s `<gov-button>`
beze změny díky adaptéru.

## Testování

- Čistě funkční helpery (`utils.js`, `snippets.js`) — unit test přes node
- DOM/fetch — Playwright E2E
- Event handling — Playwright (spuštěný live)
```

- [ ] **Step 9: Vytvořit `docs/architecture/backend-layering.md`**

Create `docs/architecture/backend-layering.md`:

```markdown
# Backend layering — thin controller

## Pravidla

1. **Controller ≤ 200 řádků** — jen routing, validace, mapping na view model
2. **Service layer** — business logika, DI lifetime: Scoped
3. **Repository layer** — EF Core queries; závislost jen na `DbContext`
4. **Soubor ≤ 500 řádků** — měkký limit, kontrola přes `scripts/check-file-sizes.sh`

## Controller anti-patterns

- ❌ EF Core query přímo v akci controlleru
- ❌ Business pravidlo (if-else nad doménou) v akci
- ❌ Přímé volání `DbContext.SaveChanges()` bez service
- ❌ Mapping entit na view model v controlleru (→ AutoMapper / explicitní mapper)

## Správná delegace

```csharp
public async Task<IActionResult> Update(ProjektUpdateCommand command, CancellationToken ct)
{
    if (!ModelState.IsValid)
        return View(command);

    var result = await _projektService.UpdateAsync(command, CurrentUserContext, ct);
    if (result.IsFailure)
    {
        ModelState.AddModelError("", result.Error);
        return View(command);
    }
    return RedirectToAction("Detail", new { id = result.Value.Id });
}
```

## Existující god-files

Fáze 1 NErozbíjí existující 500+ řádkové soubory. Fáze 3 je systematicky rozdělí
podle zodpovědnosti. Do té doby:

- Nové funkce v existujících velkých souborech pouze **když není alternativa**
- Preferuj rozdělení při dotyku (boy-scout rule)
- Nový kód **nesmí** mít >500 řádků souboru
```

- [ ] **Step 10: Vytvořit `docs/architecture/upgrade-gov-ds.md`**

Create `docs/architecture/upgrade-gov-ds.md`:

```markdown
# Upgrade gov-design-system

Aplikace používá gov-design-system lokálně hostované v `PmTracker.Web/wwwroot/lib/gov-design-system/`.

Aktuální verze:
- `@gov-design-system-ce/components` **4.2.9**
- `@gov-design-system-ce/styles` **4.2.7**

## Postup upgrade na novou verzi

### 1. Stáhnout nové soubory

```bash
# V adresáři PmTracker.Web/wwwroot/lib/gov-design-system/dist/core/
# smaž staré soubory a stáhni všechny p-*.js + core.esm.min.js + core.min.css
# (viz docs/specs/offline-deployment.md)
```

### 2. Aktualizovat verze v kódu

- `docs/specs/offline-deployment.md` — tabulka aktuálních knihoven
- `docs/architecture/upgrade-gov-ds.md` — tento soubor
- `docs/specs/global-search.md` — verzní tabulka

### 3. Prověřit breaking changes

Otevři `CHANGELOG.md` gov DS (https://github.com/gov-design-system-ce/...).

Pro `pm-*` TagHelpery stačí zkontrolovat:
- **gov-button** atributy (color, type, size) — pokud se změní enum, upravit `PmButtonTagHelper`
- **gov-message** (variant/color) — upravit `PmAlertTagHelper`
- **gov-tag** — `PmBadgeTagHelper`
- **gov-form-control/input/message** — `PmFieldTagHelper`

Jeden zdroj pravdy pro každý mapping → jeden soubor k revizi.

### 4. Spustit testy

```bash
dotnet test PmTracker.Tests.Unit
dotnet test PmTracker.Tests.E2E
```

Vizuální regression: snapshot testy TagHelperu odhalí změnu generovaného HTML.

### 5. Ověřit `/StyleGuide`

Otevři stránku, projdi všechny sekce. Vizuál musí být stále konzistentní.

### 6. Commit

```
chore(gov-ds): upgrade na verzi X.Y.Z

- components 4.2.9 → X.Y.Z
- styles 4.2.7 → X.Y.Z
- Updates v PmButtonTagHelper / PmAlertTagHelper (pokud potřeba)
```
```

- [ ] **Step 11: Build a commit**

Run: `dotnet build PmTracker.sln --nologo`

```bash
git add docs/architecture/
git commit -m "docs(architecture): 9 dokumentů pro pm-* komponenty + infrastrukturu

- README index
- buttons / alerts / badges / fields / icons — API, mapping, přechod z .btn
- tokens — CSS proměnné, breakpointy, z-index stack
- js-modules — pravidlo 300 řádků, event bus
- backend-layering — thin controller, 500 řádků limit
- upgrade-gov-ds — postup upgrade gov-design-system

Závazné pro interní i externí vývojáře."
```

---

## Task 13: E2E test /StyleGuide

**Files:**
- Create: `PmTracker.Tests.E2E/Scenarios/StyleGuideRenderTests.cs`

- [ ] **Step 1: Napsat E2E test**

Create `PmTracker.Tests.E2E/Scenarios/StyleGuideRenderTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

[Collection(E2ECollection.CollectionName)]
public sealed class StyleGuideRenderTests
{
    private readonly E2ETestFixture _fixture;

    public StyleGuideRenderTests(E2ETestFixture fixture) { _fixture = fixture; }

    [Fact]
    public async Task StyleGuide_VraciHttp200_AOnsahujePmKomponenty()
    {
        var page = await _fixture.NewPageAsync();

        var response = await page.GotoAsync($"{_fixture.BaseUrl}/StyleGuide");
        response!.Status.Should().Be(200);

        // Po hydrataci gov Web Components
        await page.WaitForSelectorAsync("gov-button", new() { Timeout = 5000 });

        var buttonCount = await page.Locator("gov-button").CountAsync();
        buttonCount.Should().BeGreaterThan(4, "StyleGuide zobrazuje alespoň 4 varianty pm-button");

        var alertCount = await page.Locator("gov-message").CountAsync();
        alertCount.Should().Be(4, "StyleGuide zobrazuje 4 pm-alert varianty");

        var badgeCount = await page.Locator("gov-tag").CountAsync();
        badgeCount.Should().Be(5, "StyleGuide zobrazuje 5 pm-badge variant");

        var fieldCount = await page.Locator("gov-form-control").CountAsync();
        fieldCount.Should().BeGreaterThanOrEqualTo(3, "StyleGuide má alespoň 3 pm-field ukázky");

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task StyleGuide_PmButton_Klikatelne_PropagujeClick()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/StyleGuide");
        await page.WaitForSelectorAsync("gov-button", new() { Timeout = 5000 });

        await page.EvaluateAsync(@"() => {
            window.__clickCount = 0;
            document.addEventListener('click', () => { window.__clickCount += 1; }, true);
        }");

        var firstButton = page.Locator("gov-button").First;
        await firstButton.ClickAsync();

        var count = await page.EvaluateAsync<int>("() => window.__clickCount");
        count.Should().BeGreaterThan(0, "gov-click adaptér propaguje jako nativní click");

        await page.Context.CloseAsync();
    }
}
```

- [ ] **Step 2: Run E2E test**

Run: `dotnet test PmTracker.Tests.E2E/PmTracker.Tests.E2E.csproj --filter "FullyQualifiedName~StyleGuideRenderTests" --nologo`
Expected: 2 PASS (fixture startuje app + SQL Server container).

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Tests.E2E/Scenarios/StyleGuideRenderTests.cs
git commit -m "test(e2e): /StyleGuide render + pm-button click propagation"
```

---

## Task 14: Finální ověření + rebuild publish

**Files:**
- None (verifikace)

- [ ] **Step 1: Spustit celou unit testovou suite**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --nologo 2>&1 | tail -5`
Expected: **Všechny** testy passed. Nové minimální číslo: baseline + (7 + 12 + 6 + 7 + 4 + 6 + 4 + 3) = baseline + 49.

- [ ] **Step 2: Spustit offline assets test (regresní)**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~OfflineAssets" --nologo`
Expected: 5/5 PASS — ověří, že fáze 1 nezavedla žádný CDN odkaz.

- [ ] **Step 3: Ověřit vizuální beze změny (grep kontrola)**

Run: `grep -rn "class=\"btn\|class=\"alert\|class=\"badge" PmTracker.Web/Views/ | wc -l`
Expected: **stejné číslo jako před fází 1** — fáze 1 neměnila existující markup.

- [ ] **Step 4: Spustit file-size policy script**

Run: `bash scripts/check-file-sizes.sh`
Expected: vypíše warning pro existující god-files, ale exit code 0.

- [ ] **Step 5: Rebuild publish**

Run: `rm -rf publish && dotnet publish PmTracker.Web/PmTracker.Web.csproj -c Release -o ./publish 2>&1 | tail -3`
Expected: 0 chyb.

- [ ] **Step 6: Ověřit publish obsahuje tokens.css a TagHelper assembly**

Run: `ls publish/wwwroot/css/tokens.css && ls publish/PmTracker.Web.dll`
Expected: oba existují.

- [ ] **Step 7: Sync bundle identity kontrola**

Run: `diff PmTracker.Web/wwwroot/js/site.bundle.js publish/wwwroot/js/site.bundle.js && echo IDENTICAL`
Expected: `IDENTICAL`.

- [ ] **Step 8: Aktualizovat spec fáze 1 — označit akceptační kritéria checked**

Read and edit `docs/superpowers/specs/2026-04-18-senior-refactor-fase-1-zaklady.md` sekce "Akceptační kritéria fáze 1" — nahradit `- [ ]` za `- [x]` u splněných bodů:

- tokens.css existuje ✓
- PmButtonTagHelper ✓
- PmAlert/Badge/Field/Icon ✓
- eventBus ✓
- /StyleGuide ✓
- 10 dokumentů v docs/architecture/ (9 + README = 10) ✓
- dotnet build 0 chyb ✓
- dotnet test baseline + 49 ✓
- OfflineAssetsTests pass ✓
- Vizuál beze změny ✓ (existující views nepoužívají pm-*)
- Screenshot diff — nelze v auto mode, **odloženo na uživatelskou verifikaci**

- [ ] **Step 9: Final commit (spec update)**

```bash
git add docs/superpowers/specs/2026-04-18-senior-refactor-fase-1-zaklady.md
git commit -m "docs(fase-1): checkmark akceptačních kritérií fáze 1"
```

- [ ] **Step 10: Push na remote**

Run: `git push origin codex/senior-refactor-fase-1`
Expected: push úspěšný, vytvoří novou remote větev.

---

## Akceptační check (souhrn)

| # | Kritérium | Ověřeno |
|---|---|---|
| 1 | `tokens.css` existuje a je v layoutu | Task 2 |
| 2 | 5 TagHelperů + enums | Tasky 4-8 |
| 3 | Unit testy pro každý TagHelper (≥4) | Tasky 4-8 |
| 4 | `eventBus.js` + adaptér + E2E test | Task 9 |
| 5 | File-size skripty + ESLint | Task 10 |
| 6 | `/StyleGuide` stránka + testy | Tasky 11, 13 |
| 7 | 10 dokumentů v `docs/architecture/` | Task 12 |
| 8 | `OfflineAssetsTests` pass (regresní) | Task 14 |
| 9 | Vizuál beze změny (existující views nezměněny) | Task 14 |
| 10 | Publish rebuild OK | Task 14 |
| 11 | Push na `codex/senior-refactor-fase-1` | Task 14 |

---

## Handoff pro fázi 2

Po akceptaci fáze 1 začne brainstorming fáze 2:
- Konverze 113 `.btn` výskytů na `<pm-button>`
- Konverze `.badge`/`.alert` výskytů
- Responzivita: grid refactor, breakpointy, wide-screen
- Merge `codex/senior-refactor-fase-1` → `codex/refactor_sprint_0` (nebo `main`, dle tvé volby)
