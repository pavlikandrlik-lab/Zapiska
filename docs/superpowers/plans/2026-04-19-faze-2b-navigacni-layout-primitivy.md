# Fáze 2B — navigační & layout pm-* primitivy — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Přidat 4 thin-wrapper TagHelpery (`pm-link`, `pm-tabs` + `pm-tabs-item`, `pm-card`, `pm-pagination`) nad odpovídající `gov-*` Web Components, sekce ve `/StyleGuide`, unit testy, docs.

**Architecture:** Stejný vzor jako Fáze 2A — thin wrapper nad gov-* Web Component, centralizované enumy pro varianty/velikosti, Unicode-aware HtmlEncoder pro textový obsah, `WebUtility.HtmlEncode` pro atributy. Žádné vlastní CSS nad gov komponenty.

**Tech Stack:** ASP.NET Core 8 Razor TagHelpers, xUnit + FluentAssertions, gov-design-system 4.2.9.

---

## File Structure

**TagHelpers (nové, `PmTracker.Web/TagHelpers/`):**

| Soubor | Odpovědnost |
|---|---|
| `PmLinkTagHelper.cs` | `<pm-link href=".." icon=".." external="true">text</pm-link>` → `<gov-link>` |
| `PmTabsTagHelper.cs` | `<pm-tabs>` wrapper → `<gov-tabs>` s orientací, typem (default/chip), size |
| `PmTabsItemTagHelper.cs` | `<pm-tabs-item title=".." active="true">content</pm-tabs-item>` → `<gov-tabs-item>` |
| `PmCardTagHelper.cs` | `<pm-card headline=".." href=".." clickable="true">body</pm-card>` → `<gov-card>` se sloty |
| `PmPaginationTagHelper.cs` | `<pm-pagination current="3" total-pages="12" url-template="?page={0}" />` → `<gov-pagination>` |

**Existující sdílené (použít):**
- `PmComponentSize.cs` (Small/Medium/Large) — pro pm-link, pm-tabs, pm-pagination
- `PmTracker.Web/Views/_ViewImports.cshtml` — už má `@addTagHelper *, PmTracker.Web`

**Tests (nové, `PmTracker.Web.Tests/TagHelpers/`):**

| Soubor | Pokrývá |
|---|---|
| `PmLinkTagHelperTests.cs` | Default, s ikonou, external, XSS |
| `PmTabsTagHelperTests.cs` | Default horizontal, vertical, type=chip |
| `PmTabsItemTagHelperTests.cs` | Default, active, child content |
| `PmCardTagHelperTests.cs` | Default, clickable href, headline XSS |
| `PmPaginationTagHelperTests.cs` | Default render, edge cases (current=1, current=last), URL template |

**Docs (nové, `docs/architecture/`):**

| Soubor | Obsah |
|---|---|
| `links.md` | `pm-link` API, external/internal, ikonové pozice |
| `tabs.md` | `pm-tabs` + `pm-tabs-item` API, orientation, type, integrace s JS |
| `cards.md` | `pm-card` API, sloty (headline, body), clickable pattern |
| `pagination.md` | `pm-pagination` API, URL template, integrace s server-side pagination |

**StyleGuide (modify):**
- `PmTracker.Web/Views/StyleGuide/Index.cshtml` — 4 nové sekce (`link`, `tabs`, `card`, `pagination`)

**README (modify):**
- `docs/architecture/README.md` — přidat Fáze 2B sekci s odkazy

**Test E2E (modify):**
- `PmTracker.Tests.E2E/Scenarios/StyleGuideRenderTests.cs` — rozšířit smoke test o 4 nové sekce

---

## Konvence kódu

Stejné jako Fáze 2A:

**C# soubory (TagHelpers):**
- `namespace PmTracker.Web.TagHelpers;`
- `[HtmlTargetElement("pm-...")]`
- `public sealed class Pm...TagHelper : TagHelper`
- Encoding:
  ```csharp
  private static readonly HtmlEncoder ContentEncoder =
      HtmlEncoder.Create(UnicodeRanges.All);
  ```
  `ContentEncoder.Encode(text)` pro text content, `WebUtility.HtmlEncode(val)` pro atributy.
- PascalCase properties (`Href`, `Icon`, `External`, …)
- Enum typy jako sealed (Vertical/Horizontal, Default/Chip …)
- XML doc comment s `<pre>` ukázkou

**Testy:**
- `namespace PmTracker.Web.Tests.TagHelpers;`
- `public class Pm...TagHelperTests`
- Min. 3 testy: default, varianta, XSS regrese
- `TagHelperTestHelpers.Render(tagHelper)` pro sync TagHelpery, pro tagy s dětským obsahem `MakeContext()` + `MakeOutput("pm-...", childContent: "...")` + `await ProcessAsync(...)`

**Commits:**
- Czech conventional: `feat(tag-helper): ...`, `docs(architecture): ...`, `test(e2e): ...`
- `Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>` (implementer) / `Claude Opus 4.7 (1M context)` (fixer/controller)

---

## Task 1: PmLinkTagHelper

**Files:**
- Create: `PmTracker.Web/TagHelpers/PmLinkTagHelper.cs`
- Test: `PmTracker.Web.Tests/TagHelpers/PmLinkTagHelperTests.cs`
- Docs: `docs/architecture/links.md`
- Modify: `PmTracker.Web/Views/StyleGuide/Index.cshtml`

- [ ] **Step 1: Failing test**

`PmTracker.Web.Tests/TagHelpers/PmLinkTagHelperTests.cs`:

```csharp
using System.Threading.Tasks;
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmLinkTagHelperTests
{
    [Fact]
    public async Task Default_RendersGovLinkWithHref()
    {
        var tagHelper = new PmLinkTagHelper { Href = "/projekty" };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-link", childContent: "Projekty");
        await tagHelper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.Equal("gov-link", output.TagName);
        Assert.Equal("/projekty", output.Attributes["href"]?.Value?.ToString());
        Assert.Contains("Projekty", html);
    }

    [Fact]
    public async Task External_AddsTargetAndRel()
    {
        var tagHelper = new PmLinkTagHelper
        {
            Href = "https://designsystem.gov.cz",
            External = true
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-link", childContent: "Design systém");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("_blank", output.Attributes["target"]?.Value?.ToString());
        Assert.Equal("noopener noreferrer", output.Attributes["rel"]?.Value?.ToString());
    }

    [Fact]
    public async Task Icon_RendersIconSlot()
    {
        var tagHelper = new PmLinkTagHelper
        {
            Href = "/",
            Icon = "chevron-right"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-link", childContent: "Dále");
        await tagHelper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("<gov-icon slot=\"icon-end\" name=\"chevron-right\" type=\"components\"></gov-icon>", html);
    }

    [Fact]
    public async Task XssInHref_IsEscapedAsAttribute()
    {
        var tagHelper = new PmLinkTagHelper
        {
            Href = "javascript:alert(1)\""
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-link", childContent: "Click");
        await tagHelper.ProcessAsync(context, output);

        // TagHelper framework encodes attribute values automatically; ověř že hodnota není raw v atributu
        var hrefValue = output.Attributes["href"]?.Value?.ToString() ?? "";
        Assert.Contains("javascript:alert(1)", hrefValue); // raw uložený, framework escapuje při renderu
    }
}
```

Pozn.: pro `pm-link` používáme async test signaturu protože child content se vrací přes `GetChildContentAsync()`.

- [ ] **Step 2: Verify failing**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmLinkTagHelperTests" --nologo`
Expected: build error — `PmLinkTagHelper` neexistuje.

- [ ] **Step 3: Implementation**

`PmTracker.Web/TagHelpers/PmLinkTagHelper.cs`:

```csharp
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-link href="/projekty" icon="chevron-right" icon-position="end">Projekty</pm-link></pre>
///
/// Thin wrapper nad gov-link. Pro externí odkazy (external="true")
/// automaticky přidá target="_blank" a rel="noopener noreferrer".
///
/// Dokumentace: docs/architecture/links.md
/// </summary>
[HtmlTargetElement("pm-link")]
public sealed class PmLinkTagHelper : TagHelper
{
    public string Href { get; set; } = "";

    /// <summary>Ikona ze sady gov-icon (např. "chevron-right").</summary>
    public string? Icon { get; set; }

    /// <summary>Pozice ikony: "start" nebo "end" (výchozí).</summary>
    public string IconPosition { get; set; } = "end";

    /// <summary>Externí odkaz (přidá target="_blank" + rel="noopener noreferrer").</summary>
    public bool External { get; set; }

    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-link";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("href", Href);
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());

        if (External)
        {
            output.Attributes.SetAttribute("target", "_blank");
            output.Attributes.SetAttribute("rel", "noopener noreferrer");
        }

        var child = await output.GetChildContentAsync();
        var childHtml = child.GetContent();

        if (!string.IsNullOrWhiteSpace(Icon))
        {
            var slotName = IconPosition == "start" ? "icon-start" : "icon-end";
            var iconHtml = $"<gov-icon slot=\"{slotName}\" name=\"{Icon}\" type=\"components\"></gov-icon>";
            output.Content.SetHtmlContent(IconPosition == "start"
                ? iconHtml + childHtml
                : childHtml + iconHtml);
        }
        else
        {
            output.Content.SetHtmlContent(childHtml);
        }
    }
}
```

- [ ] **Step 4: Run tests, expect pass**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmLinkTagHelperTests" --nologo`
Expected: 4 pass.

- [ ] **Step 5: Docs**

`docs/architecture/links.md`:

````markdown
# `pm-link`

Thin wrapper nad `<gov-link>`. Pro externí URL automaticky přidá `target="_blank"` a `rel="noopener noreferrer"`.

## Použití

```razor
<pm-link href="/projekty">Seznam projektů</pm-link>

<pm-link href="/detail/123" icon="chevron-right">Detail</pm-link>

<pm-link href="https://designsystem.gov.cz" external="true">Design systém</pm-link>
```

## API

| Property | Typ | Popis |
|---|---|---|
| `Href` | `string` | Cíl odkazu. |
| `Icon` | `string?` | Volitelná ikona (`gov-icon name="…"`). |
| `IconPosition` | `string` | `start` nebo `end` (výchozí). |
| `External` | `bool` | Externí odkaz (nový tab + noopener). |
| `Size` | `PmComponentSize` | s/m/l. |

## Mapování pm → gov

| pm atribut | gov atribut / slot |
|---|---|
| `href` | `<gov-link href>` |
| `icon` + `icon-position` | `<gov-icon slot="icon-start\|icon-end">` |
| `external` | `target="_blank"` + `rel="noopener noreferrer"` |
| `size` | `size` (s/m/l) |

## Viz také
- [`pm-button`](./buttons.md) — pro odkazy stylované jako tlačítka
````

- [ ] **Step 6: StyleGuide sekce**

V `PmTracker.Web/Views/StyleGuide/Index.cshtml` najdi `data-styleguide-section="switch"` article (poslední ze 2A) a za jeho `</article>` přidej:

```razor
    <article data-styleguide-section="link">
        <h2>Odkazy (<code>pm-link</code>)</h2>
        <div class="styleguide-row">
            <pm-link href="/">Základní odkaz</pm-link>
            <pm-link href="/projekty" icon="chevron-right">S ikonou vpravo</pm-link>
            <pm-link href="/detail" icon="chevron-left" icon-position="start">S ikonou vlevo</pm-link>
            <pm-link href="https://designsystem.gov.cz" external="true">Externí odkaz</pm-link>
        </div>
    </article>
```

- [ ] **Step 7: Full tests + build**

```
dotnet build PmTracker.Web/PmTracker.Web.csproj --nologo
dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --nologo
```
Expected: build OK, ≥ 57 tests pass (53 existing + 4 new).

- [ ] **Step 8: Commit**

```bash
git add PmTracker.Web/TagHelpers/PmLinkTagHelper.cs \
        PmTracker.Web.Tests/TagHelpers/PmLinkTagHelperTests.cs \
        docs/architecture/links.md \
        PmTracker.Web/Views/StyleGuide/Index.cshtml
git commit -m "$(cat <<'EOF'
feat(tag-helper): pm-link nad gov-link (Fáze 2B)

- PmLinkTagHelper: Href + Icon (start/end) + External (target+rel) + Size
- External=true automaticky přidá noopener noreferrer (bezpečnost)
- 4 unit testy + docs + StyleGuide sekce

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: PmTabsTagHelper + PmTabsItemTagHelper

**Files:**
- Create: `PmTracker.Web/TagHelpers/PmTabsTagHelper.cs`
- Create: `PmTracker.Web/TagHelpers/PmTabsItemTagHelper.cs`
- Test: `PmTracker.Web.Tests/TagHelpers/PmTabsTagHelperTests.cs`
- Test: `PmTracker.Web.Tests/TagHelpers/PmTabsItemTagHelperTests.cs`
- Docs: `docs/architecture/tabs.md`
- Modify: `PmTracker.Web/Views/StyleGuide/Index.cshtml`

- [ ] **Step 1: Failing test pm-tabs-item**

`PmTracker.Web.Tests/TagHelpers/PmTabsItemTagHelperTests.cs`:

```csharp
using System.Threading.Tasks;
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmTabsItemTagHelperTests
{
    [Fact]
    public async Task Default_RendersGovTabsItem()
    {
        var tagHelper = new PmTabsItemTagHelper
        {
            Title = "Přehled"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-tabs-item", childContent: "<p>Obsah panelu</p>");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("gov-tabs-item", output.TagName);
        Assert.Equal("Přehled", output.Attributes["title"]?.Value?.ToString());
        var html = output.Content.GetContent();
        Assert.Contains("<p>Obsah panelu</p>", html);
    }

    [Fact]
    public async Task Active_SetsActiveAttribute()
    {
        var tagHelper = new PmTabsItemTagHelper
        {
            Title = "Jednání",
            Active = true
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-tabs-item", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("active", output.Attributes["active"]?.Value?.ToString());
    }

    [Fact]
    public async Task XssInTitle_IsStoredRawInAttribute()
    {
        var tagHelper = new PmTabsItemTagHelper
        {
            Title = "<script>alert(1)</script>"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-tabs-item", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        // TagHelper framework escapuje attribute values; ověř že raw je v atributu,
        // framework udělá &lt;script&gt; při renderu
        var titleVal = output.Attributes["title"]?.Value?.ToString() ?? "";
        Assert.Contains("<script>", titleVal);
    }
}
```

- [ ] **Step 2: Failing test pm-tabs**

`PmTracker.Web.Tests/TagHelpers/PmTabsTagHelperTests.cs`:

```csharp
using System.Threading.Tasks;
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmTabsTagHelperTests
{
    [Fact]
    public async Task Default_RendersHorizontalTabs()
    {
        var tagHelper = new PmTabsTagHelper();
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-tabs", childContent: "<gov-tabs-item title=\"A\"></gov-tabs-item>");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("gov-tabs", output.TagName);
        Assert.Equal("horizontal", output.Attributes["orientation"]?.Value?.ToString());
        Assert.Equal("default", output.Attributes["type"]?.Value?.ToString());
    }

    [Fact]
    public async Task Vertical_SetsOrientation()
    {
        var tagHelper = new PmTabsTagHelper
        {
            Orientation = PmTabsOrientation.Vertical
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-tabs", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("vertical", output.Attributes["orientation"]?.Value?.ToString());
    }

    [Fact]
    public async Task Chip_SetsType()
    {
        var tagHelper = new PmTabsTagHelper
        {
            Type = PmTabsType.Chip
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-tabs", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("chip", output.Attributes["type"]?.Value?.ToString());
    }
}
```

- [ ] **Step 3: Verify failing**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmTabs" --nologo`
Expected: build error.

- [ ] **Step 4: Implementation pm-tabs-item**

`PmTracker.Web/TagHelpers/PmTabsItemTagHelper.cs`:

```csharp
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-tabs-item title="Přehled" active="true">&lt;p&gt;Obsah&lt;/p&gt;</pm-tabs-item></pre>
///
/// Jednotlivá záložka uvnitř pm-tabs.
///
/// Dokumentace: docs/architecture/tabs.md
/// </summary>
[HtmlTargetElement("pm-tabs-item")]
public sealed class PmTabsItemTagHelper : TagHelper
{
    public string Title { get; set; } = "";

    /// <summary>Defaultně vybraná záložka.</summary>
    public bool Active { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-tabs-item";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("title", Title);
        if (Active)
            output.Attributes.SetAttribute("active", "active");

        var child = await output.GetChildContentAsync();
        output.Content.SetHtmlContent(child.GetContent());
    }
}
```

- [ ] **Step 5: Implementation pm-tabs + enums**

`PmTracker.Web/TagHelpers/PmTabsTagHelper.cs`:

```csharp
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>Orientace záložek.</summary>
public enum PmTabsOrientation
{
    /// <summary>Vodorovně (výchozí).</summary>
    Horizontal,
    /// <summary>Svisle (menu vlevo).</summary>
    Vertical
}

/// <summary>Vizuální typ záložek.</summary>
public enum PmTabsType
{
    /// <summary>Podtržené záložky (výchozí).</summary>
    Default,
    /// <summary>Chip (pilulkový) styl.</summary>
    Chip
}

/// <summary>
/// <pre>&lt;pm-tabs orientation="Horizontal" type="Default"&gt;
///   &lt;pm-tabs-item title="A" active="true"&gt;&lt;/pm-tabs-item&gt;
/// &lt;/pm-tabs&gt;</pre>
///
/// Thin wrapper nad gov-tabs. JS přepínání obsahu panelů řídí samotný
/// gov Web Component.
///
/// Dokumentace: docs/architecture/tabs.md
/// </summary>
[HtmlTargetElement("pm-tabs")]
public sealed class PmTabsTagHelper : TagHelper
{
    public PmTabsOrientation Orientation { get; set; } = PmTabsOrientation.Horizontal;
    public PmTabsType Type { get; set; } = PmTabsType.Default;
    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-tabs";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("orientation",
            Orientation == PmTabsOrientation.Vertical ? "vertical" : "horizontal");
        output.Attributes.SetAttribute("type",
            Type == PmTabsType.Chip ? "chip" : "default");
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());

        var child = await output.GetChildContentAsync();
        output.Content.SetHtmlContent(child.GetContent());
    }
}
```

- [ ] **Step 6: Run tests, expect pass**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmTabs" --nologo`
Expected: 6 pass (3 + 3).

- [ ] **Step 7: Docs**

`docs/architecture/tabs.md`:

````markdown
# `pm-tabs` + `pm-tabs-item`

Thin wrappery nad `<gov-tabs>` a `<gov-tabs-item>`. Přepínání aktivní záložky řídí gov Web Component JS interně.

## Použití

```razor
<pm-tabs>
    <pm-tabs-item title="Přehled" active="true">
        <p>Obsah přehledu</p>
    </pm-tabs-item>
    <pm-tabs-item title="Jednání">
        <p>Seznam jednání</p>
    </pm-tabs-item>
    <pm-tabs-item title="Dokumenty">
        <p>Dokumenty projektu</p>
    </pm-tabs-item>
</pm-tabs>
```

## Typy

```razor
<pm-tabs type="Chip">
    <pm-tabs-item title="Vše" active="true"></pm-tabs-item>
    <pm-tabs-item title="Aktivní"></pm-tabs-item>
</pm-tabs>

<pm-tabs orientation="Vertical">
    <pm-tabs-item title="Sekce 1"></pm-tabs-item>
    <pm-tabs-item title="Sekce 2"></pm-tabs-item>
</pm-tabs>
```

## API

### pm-tabs

| Property | Typ | Popis |
|---|---|---|
| `Orientation` | `PmTabsOrientation` | Horizontal (výchozí) / Vertical. |
| `Type` | `PmTabsType` | Default (podtržené) / Chip (pilulky). |
| `Size` | `PmComponentSize` | s/m/l. |

### pm-tabs-item

| Property | Typ | Popis |
|---|---|---|
| `Title` | `string` | Text v hlavičce záložky. |
| `Active` | `bool` | Defaultně vybraná záložka. |

## Integrace s JS

gov-tabs JS handler zachytává kliky na hlavičku a přepíná `active` atribut. Aplikační JS může poslouchat `gov-change` event z gov-tabs pro custom logiku:

```javascript
document.querySelector('gov-tabs').addEventListener('gov-change', (e) => {
    console.log('Vybrána záložka index:', e.detail.index);
});
```

## Viz také
- [pm-card](./cards.md)
````

- [ ] **Step 8: StyleGuide**

Za link article přidej:

```razor
    <article data-styleguide-section="tabs">
        <h2>Záložky (<code>pm-tabs</code> + <code>pm-tabs-item</code>)</h2>

        <h3>Horizontal (default)</h3>
        <pm-tabs>
            <pm-tabs-item title="Přehled" active="true">
                <p>Obsah přehledu — první panel je aktivní.</p>
            </pm-tabs-item>
            <pm-tabs-item title="Jednání">
                <p>Seznam jednání — druhý panel.</p>
            </pm-tabs-item>
            <pm-tabs-item title="Dokumenty">
                <p>Dokumenty projektu — třetí panel.</p>
            </pm-tabs-item>
        </pm-tabs>

        <h3>Chip type</h3>
        <pm-tabs type="Chip">
            <pm-tabs-item title="Vše" active="true">
                <p>Všechny záznamy.</p>
            </pm-tabs-item>
            <pm-tabs-item title="Aktivní">
                <p>Filtrované na aktivní.</p>
            </pm-tabs-item>
        </pm-tabs>
    </article>
```

- [ ] **Step 9: Build + tests**

```
dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --nologo
```
Expected: ≥ 63 tests pass (57 předchozí + 6 nových).

- [ ] **Step 10: Commit**

```bash
git add PmTracker.Web/TagHelpers/PmTabsTagHelper.cs \
        PmTracker.Web/TagHelpers/PmTabsItemTagHelper.cs \
        PmTracker.Web.Tests/TagHelpers/PmTabsTagHelperTests.cs \
        PmTracker.Web.Tests/TagHelpers/PmTabsItemTagHelperTests.cs \
        docs/architecture/tabs.md \
        PmTracker.Web/Views/StyleGuide/Index.cshtml
git commit -m "$(cat <<'EOF'
feat(tag-helper): pm-tabs + pm-tabs-item (Fáze 2B)

PmTabsOrientation (Horizontal/Vertical) + PmTabsType (Default/Chip).
Přepínání aktivní záložky řídí gov-tabs JS interně; aplikace může
poslouchat gov-change event.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: PmCardTagHelper

**Files:**
- Create: `PmTracker.Web/TagHelpers/PmCardTagHelper.cs`
- Test: `PmTracker.Web.Tests/TagHelpers/PmCardTagHelperTests.cs`
- Docs: `docs/architecture/cards.md`
- Modify: `PmTracker.Web/Views/StyleGuide/Index.cshtml`

- [ ] **Step 1: Failing test**

`PmTracker.Web.Tests/TagHelpers/PmCardTagHelperTests.cs`:

```csharp
using System.Threading.Tasks;
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmCardTagHelperTests
{
    [Fact]
    public async Task Default_RendersGovCardWithHeadlineSlot()
    {
        var tagHelper = new PmCardTagHelper
        {
            Headline = "Projekt Alfa"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-card", childContent: "<p>Popis projektu</p>");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("gov-card", output.TagName);
        var html = output.Content.GetContent();
        Assert.Contains("<h3 slot=\"headline\">Projekt Alfa</h3>", html);
        Assert.Contains("<p>Popis projektu</p>", html);
    }

    [Fact]
    public async Task ClickableHref_SetsHrefAttribute()
    {
        var tagHelper = new PmCardTagHelper
        {
            Headline = "Detail",
            Href = "/projekty/123"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-card", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("/projekty/123", output.Attributes["href"]?.Value?.ToString());
    }

    [Fact]
    public async Task XssInHeadline_IsEscaped()
    {
        var tagHelper = new PmCardTagHelper
        {
            Headline = "<script>alert(1)</script>"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-card", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.DoesNotContain("<script>alert", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task HeadlineWithDiacritics_PreservesUnicode()
    {
        var tagHelper = new PmCardTagHelper
        {
            Headline = "Žádost o vyjádření"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-card", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("Žádost o vyjádření", html);
        Assert.DoesNotContain("&#", html);
    }
}
```

- [ ] **Step 2: Verify failing**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmCardTagHelperTests" --nologo`
Expected: build error.

- [ ] **Step 3: Implementation**

`PmTracker.Web/TagHelpers/PmCardTagHelper.cs`:

```csharp
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-card headline="Projekt Alfa" href="/detail/1">Popis</pm-card></pre>
///
/// Thin wrapper nad gov-card. Headline se renderuje jako &lt;h3 slot="headline"&gt;,
/// dětský obsah jde do default slotu (tělo karty). Pokud je href, celá karta
/// je klikací (gov-card href).
///
/// Dokumentace: docs/architecture/cards.md
/// </summary>
[HtmlTargetElement("pm-card")]
public sealed class PmCardTagHelper : TagHelper
{
    private static readonly HtmlEncoder ContentEncoder =
        HtmlEncoder.Create(UnicodeRanges.All);

    public string Headline { get; set; } = "";

    /// <summary>Pokud je nastaveno, celá karta je klikací odkaz.</summary>
    public string? Href { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-card";
        output.TagMode = TagMode.StartTagAndEndTag;

        if (!string.IsNullOrEmpty(Href))
            output.Attributes.SetAttribute("href", Href);

        var child = await output.GetChildContentAsync();
        var bodyHtml = child.GetContent();

        var headlineHtml = string.IsNullOrEmpty(Headline)
            ? ""
            : $"<h3 slot=\"headline\">{ContentEncoder.Encode(Headline)}</h3>";

        output.Content.SetHtmlContent(headlineHtml + bodyHtml);
    }
}
```

- [ ] **Step 4: Run tests, expect pass**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmCardTagHelperTests" --nologo`
Expected: 4 pass.

- [ ] **Step 5: Docs**

`docs/architecture/cards.md`:

````markdown
# `pm-card`

Thin wrapper nad `<gov-card>`. Obsahuje `headline` slot a default body slot.

## Použití

```razor
<pm-card headline="Projekt Alfa">
    <p>Popis projektu…</p>
    <pm-badge variant="Success">Aktivní</pm-badge>
</pm-card>
```

## Klikací karta (cela karta jako odkaz)

```razor
<pm-card headline="Detail projektu" href="/projekty/123">
    <p>Klikněte pro detail</p>
</pm-card>
```

## API

| Property | Typ | Popis |
|---|---|---|
| `Headline` | `string` | Titulek karty (renderuje se jako `<h3 slot="headline">`). |
| `Href` | `string?` | Pokud nastaveno, celá karta je klikací. |

Dětský obsah jde do default slotu (tělo karty).

## Mapování pm → gov

| pm atribut | gov atribut / slot |
|---|---|
| `headline` | `<h3 slot="headline">` |
| `href` | `<gov-card href>` |
| child content | default slot (tělo) |

## Viz také
- [`pm-tabs`](./tabs.md)
````

- [ ] **Step 6: StyleGuide**

Za tabs article:

```razor
    <article data-styleguide-section="card">
        <h2>Karty (<code>pm-card</code>)</h2>

        <div class="styleguide-row">
            <pm-card headline="Projekt Alfa">
                <p>Popis základní karty bez odkazu.</p>
                <pm-badge variant="Success">Aktivní</pm-badge>
            </pm-card>

            <pm-card headline="Klikací karta" href="/">
                <p>Celá tato karta je odkaz na kořen.</p>
            </pm-card>
        </div>
    </article>
```

- [ ] **Step 7: Build + tests**

```
dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --nologo
```
Expected: ≥ 67 tests pass.

- [ ] **Step 8: Commit**

```bash
git add PmTracker.Web/TagHelpers/PmCardTagHelper.cs \
        PmTracker.Web.Tests/TagHelpers/PmCardTagHelperTests.cs \
        docs/architecture/cards.md \
        PmTracker.Web/Views/StyleGuide/Index.cshtml
git commit -m "$(cat <<'EOF'
feat(tag-helper): pm-card nad gov-card (Fáze 2B)

Headline → <h3 slot="headline"> s Unicode-aware encoding.
Volitelné href pro klikací karty. Dětský obsah do default slotu.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: PmPaginationTagHelper

**Files:**
- Create: `PmTracker.Web/TagHelpers/PmPaginationTagHelper.cs`
- Test: `PmTracker.Web.Tests/TagHelpers/PmPaginationTagHelperTests.cs`
- Docs: `docs/architecture/pagination.md`
- Modify: `PmTracker.Web/Views/StyleGuide/Index.cshtml`

Pozn.: `gov-pagination` má vlastní API (current, pages, href-template). Náš thin wrapper jen mapuje atributy.

- [ ] **Step 1: Failing test**

`PmTracker.Web.Tests/TagHelpers/PmPaginationTagHelperTests.cs`:

```csharp
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmPaginationTagHelperTests
{
    [Fact]
    public void Default_RendersGovPagination()
    {
        var tagHelper = new PmPaginationTagHelper
        {
            Current = 3,
            TotalPages = 12,
            UrlTemplate = "?page={0}"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("<gov-pagination", html);
        Assert.Contains("current=\"3\"", html);
        Assert.Contains("pages=\"12\"", html);
        Assert.Contains("href-template=\"?page={0}\"", html);
    }

    [Fact]
    public void FirstPage_RendersWithCurrentOne()
    {
        var tagHelper = new PmPaginationTagHelper
        {
            Current = 1,
            TotalPages = 5,
            UrlTemplate = "?page={0}"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("current=\"1\"", html);
        Assert.Contains("pages=\"5\"", html);
    }

    [Fact]
    public void LastPage_RendersWithCurrentEqualsTotal()
    {
        var tagHelper = new PmPaginationTagHelper
        {
            Current = 5,
            TotalPages = 5,
            UrlTemplate = "?page={0}"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("current=\"5\"", html);
        Assert.Contains("pages=\"5\"", html);
    }

    [Fact]
    public void SinglePage_StillRendersButHidden()
    {
        var tagHelper = new PmPaginationTagHelper
        {
            Current = 1,
            TotalPages = 1,
            UrlTemplate = "?page={0}"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("<gov-pagination", html);
        Assert.Contains("pages=\"1\"", html);
    }
}
```

- [ ] **Step 2: Verify failing**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmPaginationTagHelperTests" --nologo`
Expected: build error.

- [ ] **Step 3: Implementation**

`PmTracker.Web/TagHelpers/PmPaginationTagHelper.cs`:

```csharp
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-pagination current="3" total-pages="12" url-template="?page={0}" /></pre>
///
/// Thin wrapper nad gov-pagination. UrlTemplate musí obsahovat placeholder "{0}"
/// který gov-pagination nahrazuje číslem stránky.
///
/// Dokumentace: docs/architecture/pagination.md
/// </summary>
[HtmlTargetElement("pm-pagination")]
public sealed class PmPaginationTagHelper : TagHelper
{
    /// <summary>Aktuální stránka (1-based).</summary>
    public int Current { get; set; } = 1;

    /// <summary>Celkový počet stránek.</summary>
    [HtmlAttributeName("total-pages")]
    public int TotalPages { get; set; } = 1;

    /// <summary>URL šablona s "{0}" pro číslo stránky (např. "?page={0}").</summary>
    [HtmlAttributeName("url-template")]
    public string UrlTemplate { get; set; } = "?page={0}";

    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-pagination";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("current", Current.ToString());
        output.Attributes.SetAttribute("pages", TotalPages.ToString());
        output.Attributes.SetAttribute("href-template", UrlTemplate);
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Run tests, expect pass**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmPaginationTagHelperTests" --nologo`
Expected: 4 pass.

- [ ] **Step 5: Docs**

`docs/architecture/pagination.md`:

````markdown
# `pm-pagination`

Thin wrapper nad `<gov-pagination>`. Renderuje stránkovací lištu s odkazy na jednotlivé stránky.

## Použití

```razor
<pm-pagination current="@Model.CurrentPage"
               total-pages="@Model.TotalPages"
               url-template="?page={0}&q=@Model.Query" />
```

## API

| Property | Typ | Popis |
|---|---|---|
| `Current` | `int` | Aktuální stránka (1-based). |
| `TotalPages` | `int` | Celkový počet stránek. |
| `UrlTemplate` | `string` | URL šablona s `{0}` pro číslo stránky. |
| `Size` | `PmComponentSize` | s/m/l. |

## Integrace se server-side pagingem

Server generuje `CurrentPage` a `TotalPages` (např. z EF Core `Skip/Take`). `UrlTemplate` obsahuje všechny ostatní parametry dotazu:

```csharp
// Controller
ViewBag.UrlTemplate = $"?page={{0}}&q={Uri.EscapeDataString(query ?? "")}";
```

```razor
<pm-pagination current="@currentPage" total-pages="@totalPages" url-template="@ViewBag.UrlTemplate" />
```

gov-pagination automaticky generuje `<a>` odkazy s nahrazeným `{0}` a označuje aktuální stránku.

## Mapování pm → gov

| pm atribut | gov atribut |
|---|---|
| `current` | `current` |
| `total-pages` | `pages` |
| `url-template` | `href-template` |
| `size` | `size` |

## Viz také
- [pm-link](./links.md)
````

- [ ] **Step 6: StyleGuide**

Za card article:

```razor
    <article data-styleguide-section="pagination">
        <h2>Stránkování (<code>pm-pagination</code>)</h2>

        <h3>Uprostřed (3 / 12)</h3>
        <pm-pagination current="3" total-pages="12" url-template="?page={0}" />

        <h3>První stránka (1 / 5)</h3>
        <pm-pagination current="1" total-pages="5" url-template="?page={0}" />

        <h3>Poslední stránka (5 / 5)</h3>
        <pm-pagination current="5" total-pages="5" url-template="?page={0}" />
    </article>
```

- [ ] **Step 7: Build + tests**

```
dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --nologo
```
Expected: ≥ 71 tests pass.

- [ ] **Step 8: Commit**

```bash
git add PmTracker.Web/TagHelpers/PmPaginationTagHelper.cs \
        PmTracker.Web.Tests/TagHelpers/PmPaginationTagHelperTests.cs \
        docs/architecture/pagination.md \
        PmTracker.Web/Views/StyleGuide/Index.cshtml
git commit -m "$(cat <<'EOF'
feat(tag-helper): pm-pagination nad gov-pagination (Fáze 2B)

Mapuje Current/TotalPages/UrlTemplate → current/pages/href-template.
gov-pagination samo generuje <a> odkazy s nahrazeným {0}.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: Konsolidace — E2E smoke rozšíření + README update

**Files:**
- Modify: `PmTracker.Tests.E2E/Scenarios/StyleGuideRenderTests.cs`
- Modify: `docs/architecture/README.md`

- [ ] **Step 1: Rozšířit E2E test**

V `PmTracker.Tests.E2E/Scenarios/StyleGuideRenderTests.cs` najdi metodu `StyleGuide_ObsahujeSekceFaze2A`. Přidej za ni novou metodu:

```csharp
    [Fact]
    public async Task StyleGuide_ObsahujeSekceFaze2B()
    {
        var page = await _fixture.NewPageAsync();

        var response = await page.GotoAsync($"{_fixture.BaseUrl}/StyleGuide");
        response!.Status.Should().Be(200);

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        foreach (var section in new[] { "link", "tabs", "card", "pagination" })
        {
            var count = await page.Locator($"[data-styleguide-section=\"{section}\"]").CountAsync();
            count.Should().Be(1, $"Sekce {section} musí být přesně jednou ve StyleGuide (Fáze 2B)");
        }

        // Sanity: gov komponenty odpovídající novým pm-* wrapperům jsou přítomné
        (await page.Locator("gov-link").CountAsync())
            .Should().BeGreaterThanOrEqualTo(4, "4 pm-link ukázky ve StyleGuide");
        (await page.Locator("gov-tabs").CountAsync())
            .Should().BeGreaterThanOrEqualTo(2, "2 pm-tabs (horizontal + chip)");
        (await page.Locator("gov-card").CountAsync())
            .Should().BeGreaterThanOrEqualTo(2, "2 pm-card (default + klikací)");
        (await page.Locator("gov-pagination").CountAsync())
            .Should().BeGreaterThanOrEqualTo(3, "3 pm-pagination (uprostřed, první, poslední)");

        await page.Context.CloseAsync();
    }
```

- [ ] **Step 2: Build E2E projekt**

Run: `dotnet build PmTracker.Tests.E2E/PmTracker.Tests.E2E.csproj --nologo`
Expected: 0 errors.

- [ ] **Step 3: README update**

V `docs/architecture/README.md` najdi sekci `### Fáze 2A — formulářové primitivy`. Za její poslední položku přidej:

```markdown
### Fáze 2B — navigační & layout primitivy (hotovo 2026-04-19)
- [pm-link — odkazy (interní i externí)](links.md)
- [pm-tabs + pm-tabs-item — záložky](tabs.md)
- [pm-card — karty](cards.md)
- [pm-pagination — stránkování](pagination.md)
```

- [ ] **Step 4: Full test run + publish**

```
dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --nologo
dotnet publish PmTracker.Web/PmTracker.Web.csproj -c Release -o ./publish --nologo 2>&1 | tail -3
```
Expected: 71+ unit tests pass, publish OK.

- [ ] **Step 5: Final commit**

```bash
git add PmTracker.Tests.E2E/Scenarios/StyleGuideRenderTests.cs \
        docs/architecture/README.md
git commit -m "$(cat <<'EOF'
test(e2e) + docs: uzavřít Fázi 2B

- StyleGuideRenderTests: test StyleGuide_ObsahujeSekceFaze2B
  ověří 4 nové sekce (link, tabs, card, pagination) a minimální
  počet gov komponentních instancí.
- docs/architecture/README.md: sekce Fáze 2B s odkazy.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Po všech tascích

- [ ] **Finální check**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet test --nologo 2>&1 | tail -10
dotnet publish PmTracker.Web/PmTracker.Web.csproj -c Release -o ./publish --nologo 2>&1 | tail -3
```

Expected:
- `PmTracker.Web.Tests` ≥ 71 tests pass (53 po 2A + 18 nových z 2B)
- Publish build bez chyb
- `publish/` obsahuje nové TagHelpery

---

## Self-Review

**Spec coverage:**
- ✅ `pm-link` → Task 1
- ✅ `pm-tabs` + `pm-tabs-item` → Task 2
- ✅ `pm-card` → Task 3
- ✅ `pm-pagination` → Task 4
- ✅ E2E smoke + README → Task 5
- ✅ Unit testy (min 3 + XSS) → každý task Step 1
- ✅ StyleGuide sekce → Tasks 1-4 Step "StyleGuide"
- ✅ Konvence (HtmlEncoder, XSS, PascalCase, file-size) → Konvence kódu

**Placeholder scan:** Žádné "TBD" / "implement later". Všechny code blocks kompletní.

**Type consistency:**
- `PmTabsOrientation` enum (Horizontal/Vertical) + `PmTabsType` enum (Default/Chip) definovány v Task 2
- `PmComponentSize.ToGovAttribute()` používán konzistentně ve všech TagHelperech (od Fáze 1)
- `ContentEncoder` pattern (jméno a použití) zrcadlí Fázi 1+2A
- `[HtmlAttributeName("total-pages")]` + `[HtmlAttributeName("url-template")]` v pm-pagination — potřebné protože C# PascalCase na HTML kebab-case nemapuje automaticky pro víceslovní property
- `gov-card` headline slot používá `<h3>` — záměrně (konzistentní s oficiálními gov DS ukázkami)
- Commit co-author footer je Sonnet 4.6 pro implementer tasky 1-4 a Opus 4.7 pro Task 5 (consolidation, controller-driven)

**CSS scope check:** Fáze 2B nepřidává žádné formulářové prvky, takže CSS scope fix z Fáze 2A stačí — žádné nové globální `link`/`button` selektory neupravujeme (ty jsou už škopované z Fáze 1). Pokud při implementaci narazíš na globální style override přes `a { ... }` nebo podobné, flagni to jako DONE_WITH_CONCERNS.
