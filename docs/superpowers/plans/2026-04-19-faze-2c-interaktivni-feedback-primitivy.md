# Fáze 2C — interaktivní & feedback pm-* primitivy — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Přidat 5 thin-wrapper TagHelperů (`pm-dialog`, `pm-tooltip`, `pm-toast`, `pm-skeleton`, `pm-loading`) nad odpovídající `gov-*` Web Components, sekce ve `/StyleGuide`, unit testy, docs.

**Architecture:** Stejný vzor jako Fáze 2A/2B. Thin wrapper, centralizované enumy pro varianty, Unicode-aware `ContentEncoder` pro textový obsah, `WebUtility.HtmlEncode` pro atributy. **Bezpečnost**: všechny user-supplied stringy v ručně skládaném HTML procházejí `WebUtility.HtmlEncode` nebo `ContentEncoder.Encode` (poučení z 2B review `PmLinkTagHelper` blockera).

**Tech Stack:** ASP.NET Core 8 Razor TagHelpers, xUnit + FluentAssertions, gov-design-system 4.2.9.

---

## File Structure

**TagHelpers (nové, `PmTracker.Web/TagHelpers/`):**

| Soubor | Odpovědnost |
|---|---|
| `PmDialogTagHelper.cs` | `<pm-dialog id=".." title=".." open="true">content</pm-dialog>` → `<gov-dialog>` se slot title + body + volitelně icon |
| `PmTooltipTagHelper.cs` | `<pm-tooltip text="Nápověda">trigger</pm-tooltip>` → `<gov-tooltip>` + `<gov-tooltip-content>` |
| `PmToastTagHelper.cs` | `<pm-toast variant="Success" gravity="Top" position="Right">zpráva</pm-toast>` → `<gov-toast>` |
| `PmSkeletonTagHelper.cs` | `<pm-skeleton shape="Circle" size="Medium" />` → `<gov-skeleton>` placeholder |
| `PmLoadingTagHelper.cs` | `<pm-loading label="Načítám..." />` → `<gov-loading>` spinner |

**Enums (součástí odpovídajících souborů):**
- `PmToastVariant` (Info/Success/Warning/Error) → mapuje color
- `PmToastGravity` (Top/Bottom)
- `PmToastPosition` (Left/Center/Right)
- `PmSkeletonShape` (Default/Circle)

**Reuse:**
- `PmComponentSize` — pro pm-skeleton, pm-loading, pm-toast

**Tests (nové, `PmTracker.Web.Tests/TagHelpers/`):**

| Soubor | Pokrývá |
|---|---|
| `PmDialogTagHelperTests.cs` | Default, open state, XSS v title |
| `PmTooltipTagHelperTests.cs` | Default, text XSS, Unicode |
| `PmToastTagHelperTests.cs` | Default (Info), variants, gravity/position |
| `PmSkeletonTagHelperTests.cs` | Default, Circle shape, size |
| `PmLoadingTagHelperTests.cs` | Default, label Unicode, size |

**Docs (nové, `docs/architecture/`):**

| Soubor | Obsah |
|---|---|
| `dialogs.md` | `pm-dialog` API, open/close přes JS, sloty |
| `tooltips.md` | `pm-tooltip` API, trigger + content pattern |
| `toasts.md` | `pm-toast` API, gravity/position/variant, programové zobrazení |
| `skeletons.md` | `pm-skeleton` API, shape, animace |
| `loadings.md` | `pm-loading` API, size, kdy použít vs. skeleton |

**StyleGuide (modify):**
- `PmTracker.Web/Views/StyleGuide/Index.cshtml` — 5 nových sekcí: `dialog`, `tooltip`, `toast`, `skeleton`, `loading`

**README (modify):**
- `docs/architecture/README.md` — přidat Fáze 2C sekci

**Test E2E (modify):**
- `PmTracker.Tests.E2E/Scenarios/StyleGuideRenderTests.cs` — rozšířit smoke test o 5 nových sekcí

---

## Konvence kódu

Stejné jako Fáze 2A/2B:

**C# soubory (TagHelpers):**
- `namespace PmTracker.Web.TagHelpers;`
- `[HtmlTargetElement("pm-...")]`
- `public sealed class Pm...TagHelper : TagHelper`
- Encoding:
  ```csharp
  private static readonly HtmlEncoder ContentEncoder =
      HtmlEncoder.Create(UnicodeRanges.All);
  ```
  `ContentEncoder.Encode(text)` pro text content, `WebUtility.HtmlEncode(val)` pro atributy v ručně skládaném HTML.
- PascalCase properties, enum typy PascalCase
- XML doc comment s `<pre>` ukázkou
- **Důležité**: Jakákoliv user-supplied property, která jde do ručně skládaného HTML atributu MUSÍ projít `WebUtility.HtmlEncode`. Poučení z 2B review.

**Testy:**
- `namespace PmTracker.Web.Tests.TagHelpers;`
- Min. 3 testy: default, varianta, XSS nebo Unicode regrese
- `TagHelperTestHelpers.Render(tagHelper)` pro sync, `MakeContext()` + `MakeOutput("pm-...", childContent: "…")` pro async s child contentem

**Commits:**
- `feat(tag-helper): pm-... nad gov-... (Fáze 2C)`
- `Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>`

---

## Task 1: PmDialogTagHelper

**Files:**
- Create: `PmTracker.Web/TagHelpers/PmDialogTagHelper.cs`
- Create: `PmTracker.Web.Tests/TagHelpers/PmDialogTagHelperTests.cs`
- Create: `docs/architecture/dialogs.md`
- Modify: `PmTracker.Web/Views/StyleGuide/Index.cshtml`

- [ ] **Step 1: Failing tests**

`PmTracker.Web.Tests/TagHelpers/PmDialogTagHelperTests.cs`:

```csharp
using System.Threading.Tasks;
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmDialogTagHelperTests
{
    [Fact]
    public async Task Default_RendersGovDialogWithTitleSlot()
    {
        var tagHelper = new PmDialogTagHelper
        {
            Id = "confirm-delete",
            Title = "Potvrdit smazání"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-dialog", childContent: "<p>Opravdu smazat?</p>");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("gov-dialog", output.TagName);
        Assert.Equal("confirm-delete", output.Attributes["id"]?.Value?.ToString());
        var html = output.Content.GetContent();
        Assert.Contains("<h3 slot=\"title\">Potvrdit smazání</h3>", html);
        Assert.Contains("<p>Opravdu smazat?</p>", html);
    }

    [Fact]
    public async Task Open_SetsOpenAttribute()
    {
        var tagHelper = new PmDialogTagHelper
        {
            Id = "d",
            Title = "T",
            Open = true
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-dialog", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("true", output.Attributes["open"]?.Value?.ToString());
    }

    [Fact]
    public async Task XssInTitle_IsEscaped()
    {
        var tagHelper = new PmDialogTagHelper
        {
            Id = "d",
            Title = "<script>alert(1)</script>"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-dialog", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.DoesNotContain("<script>alert", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task TitleWithDiacritics_PreservesUnicode()
    {
        var tagHelper = new PmDialogTagHelper
        {
            Id = "d",
            Title = "Potvrdit změnu"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-dialog", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("Potvrdit změnu", html);
        Assert.DoesNotContain("&#", html);
    }
}
```

- [ ] **Step 2: Verify failing**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmDialogTagHelperTests" --nologo`
Expected: build error.

- [ ] **Step 3: Implementation**

`PmTracker.Web/TagHelpers/PmDialogTagHelper.cs`:

```csharp
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-dialog id="confirm" title="Potvrzení">&lt;p&gt;Obsah&lt;/p&gt;</pm-dialog></pre>
///
/// Thin wrapper nad gov-dialog. Title se renderuje jako &lt;h3 slot="title"&gt;,
/// dětský obsah jde do default slotu. Otevření/zavření řídí gov-dialog JS
/// (atribut open nebo metoda .show()/.close() na elementu).
///
/// Dokumentace: docs/architecture/dialogs.md
/// </summary>
[HtmlTargetElement("pm-dialog")]
public sealed class PmDialogTagHelper : TagHelper
{
    private static readonly HtmlEncoder ContentEncoder =
        HtmlEncoder.Create(UnicodeRanges.All);

    public string Id { get; set; } = "";
    public string Title { get; set; } = "";

    /// <summary>Defaultně otevřený dialog.</summary>
    public bool Open { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-dialog";
        output.TagMode = TagMode.StartTagAndEndTag;
        if (!string.IsNullOrEmpty(Id))
            output.Attributes.SetAttribute("id", Id);
        if (Open)
            output.Attributes.SetAttribute("open", "true");

        var child = await output.GetChildContentAsync();
        var bodyHtml = child.GetContent();

        var titleHtml = string.IsNullOrEmpty(Title)
            ? ""
            : $"<h3 slot=\"title\">{ContentEncoder.Encode(Title)}</h3>";

        output.Content.SetHtmlContent(titleHtml + bodyHtml);
    }
}
```

- [ ] **Step 4: Verify pass**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmDialogTagHelperTests" --nologo`
Expected: 4 pass.

- [ ] **Step 5: Docs**

Create `docs/architecture/dialogs.md`. Use real triple backticks:

```markdown
# `pm-dialog`

Thin wrapper nad `<gov-dialog>`. Modální dialog s titulkem, tělem a volitelnými akcemi.

## Použití

(razor triple backticks)
<pm-button variant="Primary" onclick="document.getElementById('confirm-delete').show()">Smazat</pm-button>

<pm-dialog id="confirm-delete" title="Potvrdit smazání">
    <p>Opravdu chcete smazat tento záznam?</p>
    <pm-button variant="Destructive" onclick="document.getElementById('confirm-delete').close()">Smazat</pm-button>
    <pm-button variant="Secondary" onclick="document.getElementById('confirm-delete').close()">Zrušit</pm-button>
</pm-dialog>
(close code block)

## API

| Property | Typ | Popis |
|---|---|---|
| `Id` | `string` | HTML id (nutné pro otevření/zavření z jiných elementů). |
| `Title` | `string` | Titulek (renderuje se jako `<h3 slot="title">`). |
| `Open` | `bool` | Defaultně otevřený (přidá `open="true"`). |

Dětský obsah jde do default slotu (tělo dialogu).

## Otevření/zavření z JS

gov-dialog web komponenta má metody `.show()` a `.close()`:

(javascript triple backticks)
const dlg = document.getElementById('confirm-delete');
dlg.show();    // otevřít
dlg.close();   // zavřít
(close code block)

## Mapování pm → gov

| pm atribut | gov atribut / slot |
|---|---|
| `id` | HTML id |
| `title` | `<h3 slot="title">` |
| `open` | `open="true"` |
| child content | default slot |

## Viz také
- [pm-button](./buttons.md)
```

- [ ] **Step 6: StyleGuide**

V `PmTracker.Web/Views/StyleGuide/Index.cshtml` najdi `<article data-styleguide-section="pagination">` (poslední z 2B). Vlož AFTER jeho `</article>`:

```razor
    <article data-styleguide-section="dialog">
        <h2>Dialog (<code>pm-dialog</code>)</h2>
        <p>
            Klikni na tlačítko — otevře se modální dialog. Gov-dialog JS řídí
            otevření/zavření přes <code>.show()</code> / <code>.close()</code>.
        </p>
        <pm-button variant="Primary" onclick="document.getElementById('styleguide-demo-dialog').show()">Otevřít dialog</pm-button>

        <pm-dialog id="styleguide-demo-dialog" title="Ukázkový dialog">
            <p>Toto je demonstrace <code>pm-dialog</code> komponenty.</p>
            <pm-button variant="Primary" onclick="document.getElementById('styleguide-demo-dialog').close()">Zavřít</pm-button>
        </pm-dialog>
    </article>
```

- [ ] **Step 7: Build + tests**

```
dotnet build PmTracker.Web/PmTracker.Web.csproj --nologo
dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --nologo
```
Expected: 76+ pass (72 + 4).

- [ ] **Step 8: Commit**

```bash
git add PmTracker.Web/TagHelpers/PmDialogTagHelper.cs \
        PmTracker.Web.Tests/TagHelpers/PmDialogTagHelperTests.cs \
        docs/architecture/dialogs.md \
        PmTracker.Web/Views/StyleGuide/Index.cshtml
git commit -m "$(cat <<'EOF'
feat(tag-helper): pm-dialog nad gov-dialog (Fáze 2C)

Title → <h3 slot="title"> s Unicode-aware encoding. Otevírání/zavírání
řídí gov-dialog JS přes .show()/.close(). Default open stav přes
atribut open="true".

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: PmTooltipTagHelper

**Files:**
- Create: `PmTracker.Web/TagHelpers/PmTooltipTagHelper.cs`
- Create: `PmTracker.Web.Tests/TagHelpers/PmTooltipTagHelperTests.cs`
- Create: `docs/architecture/tooltips.md`
- Modify: `PmTracker.Web/Views/StyleGuide/Index.cshtml`

Pozn.: `gov-tooltip` obaluje **trigger element** (text/button/icon) a zobrazuje `gov-tooltip-content` jako bublinu. Náš pm-tooltip přijme `Text` property (obsah bubliny) a child content (trigger).

- [ ] **Step 1: Failing tests**

`PmTracker.Web.Tests/TagHelpers/PmTooltipTagHelperTests.cs`:

```csharp
using System.Threading.Tasks;
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmTooltipTagHelperTests
{
    [Fact]
    public async Task Default_RendersGovTooltipWithContent()
    {
        var tagHelper = new PmTooltipTagHelper
        {
            Text = "Nápovědný text"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-tooltip", childContent: "Najeď myší");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("gov-tooltip", output.TagName);
        var html = output.Content.GetContent();
        Assert.Contains("Najeď myší", html);
        Assert.Contains("<gov-tooltip-content>Nápovědný text</gov-tooltip-content>", html);
    }

    [Fact]
    public async Task XssInText_IsEscaped()
    {
        var tagHelper = new PmTooltipTagHelper
        {
            Text = "<script>alert(1)</script>"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-tooltip", childContent: "T");
        await tagHelper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.DoesNotContain("<script>alert", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task TextWithDiacritics_PreservesUnicode()
    {
        var tagHelper = new PmTooltipTagHelper
        {
            Text = "Nápověda: ušetří čas"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-tooltip", childContent: "T");
        await tagHelper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("Nápověda: ušetří čas", html);
        Assert.DoesNotContain("&#", html);
    }
}
```

- [ ] **Step 2: Verify failing**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmTooltipTagHelperTests" --nologo`
Expected: build error.

- [ ] **Step 3: Implementation**

`PmTracker.Web/TagHelpers/PmTooltipTagHelper.cs`:

```csharp
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-tooltip text="Nápověda">trigger element</pm-tooltip></pre>
///
/// Thin wrapper nad gov-tooltip. Child content je trigger (text/ikona),
/// Text property je obsah bubliny jako gov-tooltip-content.
///
/// Dokumentace: docs/architecture/tooltips.md
/// </summary>
[HtmlTargetElement("pm-tooltip")]
public sealed class PmTooltipTagHelper : TagHelper
{
    private static readonly HtmlEncoder ContentEncoder =
        HtmlEncoder.Create(UnicodeRanges.All);

    public string Text { get; set; } = "";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-tooltip";
        output.TagMode = TagMode.StartTagAndEndTag;

        var child = await output.GetChildContentAsync();
        var triggerHtml = child.GetContent();

        var contentHtml = string.IsNullOrEmpty(Text)
            ? ""
            : $"<gov-tooltip-content>{ContentEncoder.Encode(Text)}</gov-tooltip-content>";

        output.Content.SetHtmlContent(triggerHtml + contentHtml);
    }
}
```

- [ ] **Step 4: Verify pass**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmTooltipTagHelperTests" --nologo`
Expected: 3 pass.

- [ ] **Step 5: Docs**

`docs/architecture/tooltips.md`:

```markdown
# `pm-tooltip`

Thin wrapper nad `<gov-tooltip>` + `<gov-tooltip-content>`. Child content = trigger, `Text` property = obsah bubliny.

## Použití

(razor triple backticks)
<pm-tooltip text="Klikněte pro uložení záznamu">
    <pm-button variant="Primary">Uložit</pm-button>
</pm-tooltip>

<pm-tooltip text="Stav projektu">
    <pm-badge variant="Success">Aktivní</pm-badge>
</pm-tooltip>
(close code block)

## API

| Property | Typ | Popis |
|---|---|---|
| `Text` | `string` | Obsah bubliny. |

Dětský obsah je trigger (prvek, na který uživatel najede myší nebo fokusem).

## Mapování pm → gov

| pm atribut | gov prvek |
|---|---|
| child content | trigger (před `gov-tooltip-content`) |
| `text` | `<gov-tooltip-content>` |

## Viz také
- [pm-dialog](./dialogs.md) — pro delší obsah / modální
```

- [ ] **Step 6: StyleGuide**

Za dialog article:

```razor
    <article data-styleguide-section="tooltip">
        <h2>Tooltip (<code>pm-tooltip</code>)</h2>
        <p>Najeď myší na prvek, uvidíš bublinu.</p>
        <div class="styleguide-row">
            <pm-tooltip text="Klikněte pro uložení záznamu">
                <pm-button variant="Primary">Uložit</pm-button>
            </pm-tooltip>
            <pm-tooltip text="Stav projektu">
                <pm-badge variant="Success">Aktivní</pm-badge>
            </pm-tooltip>
        </div>
    </article>
```

- [ ] **Step 7: Build + tests**

```
dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --nologo
```
Expected: ≥ 79 pass.

- [ ] **Step 8: Commit**

```bash
git add PmTracker.Web/TagHelpers/PmTooltipTagHelper.cs \
        PmTracker.Web.Tests/TagHelpers/PmTooltipTagHelperTests.cs \
        docs/architecture/tooltips.md \
        PmTracker.Web/Views/StyleGuide/Index.cshtml
git commit -m "$(cat <<'EOF'
feat(tag-helper): pm-tooltip nad gov-tooltip (Fáze 2C)

Child content = trigger, Text property = bublina (gov-tooltip-content).
ContentEncoder pro Text, triggerHtml je bezpečný (GetChildContentAsync).

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: PmToastTagHelper

**Files:**
- Create: `PmTracker.Web/TagHelpers/PmToastTagHelper.cs`
- Create: `PmTracker.Web.Tests/TagHelpers/PmToastTagHelperTests.cs`
- Create: `docs/architecture/toasts.md`
- Modify: `PmTracker.Web/Views/StyleGuide/Index.cshtml`

- [ ] **Step 1: Failing tests**

`PmTracker.Web.Tests/TagHelpers/PmToastTagHelperTests.cs`:

```csharp
using System.Threading.Tasks;
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmToastTagHelperTests
{
    [Fact]
    public async Task Default_RendersInfoTopRight()
    {
        var tagHelper = new PmToastTagHelper();
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-toast", childContent: "Zpráva");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("gov-toast", output.TagName);
        Assert.Equal("primary", output.Attributes["color"]?.Value?.ToString());
        Assert.Equal("bold", output.Attributes["type"]?.Value?.ToString());
        Assert.Equal("top", output.Attributes["gravity"]?.Value?.ToString());
        Assert.Equal("right", output.Attributes["position"]?.Value?.ToString());
    }

    [Fact]
    public async Task SuccessVariant_SetsColor()
    {
        var tagHelper = new PmToastTagHelper
        {
            Variant = PmToastVariant.Success
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-toast", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("success", output.Attributes["color"]?.Value?.ToString());
    }

    [Fact]
    public async Task ErrorVariant_SetsColor()
    {
        var tagHelper = new PmToastTagHelper
        {
            Variant = PmToastVariant.Error
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-toast", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("error", output.Attributes["color"]?.Value?.ToString());
    }

    [Fact]
    public async Task BottomLeftPosition_SetsBothAttributes()
    {
        var tagHelper = new PmToastTagHelper
        {
            Gravity = PmToastGravity.Bottom,
            Position = PmToastPosition.Left
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-toast", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("bottom", output.Attributes["gravity"]?.Value?.ToString());
        Assert.Equal("left", output.Attributes["position"]?.Value?.ToString());
    }
}
```

- [ ] **Step 2: Verify failing**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmToastTagHelperTests" --nologo`
Expected: build error.

- [ ] **Step 3: Implementation**

`PmTracker.Web/TagHelpers/PmToastTagHelper.cs`:

```csharp
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>Semantická varianta toastu (mapuje na gov color).</summary>
public enum PmToastVariant
{
    /// <summary>Informativní (primary, výchozí).</summary>
    Info,
    /// <summary>Úspěch (success).</summary>
    Success,
    /// <summary>Upozornění (warning).</summary>
    Warning,
    /// <summary>Chyba (error).</summary>
    Error
}

/// <summary>Svislá pozice toastu.</summary>
public enum PmToastGravity
{
    Top,
    Bottom
}

/// <summary>Vodorovná pozice toastu.</summary>
public enum PmToastPosition
{
    Left,
    Center,
    Right
}

/// <summary>
/// <pre><pm-toast variant="Success" gravity="Top" position="Right">Uloženo!</pm-toast></pre>
///
/// Thin wrapper nad gov-toast. Zobrazení bývá programové — aplikace
/// vytvoří element a volá .show() na něm.
///
/// Dokumentace: docs/architecture/toasts.md
/// </summary>
[HtmlTargetElement("pm-toast")]
public sealed class PmToastTagHelper : TagHelper
{
    public PmToastVariant Variant { get; set; } = PmToastVariant.Info;
    public PmToastGravity Gravity { get; set; } = PmToastGravity.Top;
    public PmToastPosition Position { get; set; } = PmToastPosition.Right;
    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-toast";
        output.TagMode = TagMode.StartTagAndEndTag;

        var color = Variant switch
        {
            PmToastVariant.Success => "success",
            PmToastVariant.Warning => "warning",
            PmToastVariant.Error => "error",
            _ => "primary"
        };
        output.Attributes.SetAttribute("color", color);
        output.Attributes.SetAttribute("type", "bold");
        output.Attributes.SetAttribute("gravity",
            Gravity == PmToastGravity.Bottom ? "bottom" : "top");
        output.Attributes.SetAttribute("position", Position switch
        {
            PmToastPosition.Left => "left",
            PmToastPosition.Center => "center",
            _ => "right"
        });
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());

        var child = await output.GetChildContentAsync();
        output.Content.SetHtmlContent(child.GetContent());
    }
}
```

- [ ] **Step 4: Verify pass**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmToastTagHelperTests" --nologo`
Expected: 4 pass.

- [ ] **Step 5: Docs**

`docs/architecture/toasts.md`:

```markdown
# `pm-toast`

Thin wrapper nad `<gov-toast>`. Krátká notifikace u okraje obrazovky.

## Použití (deklarativně v Razoru)

(razor triple backticks)
<pm-toast variant="Success" gravity="Top" position="Right">
    Záznam byl uložen.
</pm-toast>
(close code block)

## Programové zobrazení (JS)

V praxi se toast vytváří za běhu aplikace:

(javascript triple backticks)
const toast = document.createElement('gov-toast');
toast.setAttribute('color', 'success');
toast.setAttribute('type', 'bold');
toast.setAttribute('gravity', 'top');
toast.setAttribute('position', 'right');
toast.textContent = 'Záznam byl uložen.';
document.body.appendChild(toast);
toast.show();  // případně auto-dismiss přes timeout
(close code block)

## API

| Property | Typ | Výchozí | Popis |
|---|---|---|---|
| `Variant` | `PmToastVariant` | `Info` | Info/Success/Warning/Error → color. |
| `Gravity` | `PmToastGravity` | `Top` | Svislá pozice. |
| `Position` | `PmToastPosition` | `Right` | Vodorovná pozice. |
| `Size` | `PmComponentSize` | `Medium` | s/m/l. |

## Mapování pm → gov

| pm | gov |
|---|---|
| `variant=Info` | `color="primary"` |
| `variant=Success` | `color="success"` |
| `variant=Warning` | `color="warning"` |
| `variant=Error` | `color="error"` |
| `gravity` | `gravity="top\|bottom"` |
| `position` | `position="left\|center\|right"` |

Vždy renderujeme `type="bold"` (filled barva pro kontrast).

## Viz také
- [pm-alert](./alerts.md) — inline oznámení (ne u okraje)
- [pm-dialog](./dialogs.md) — modální dialog
```

- [ ] **Step 6: StyleGuide**

Za tooltip article:

```razor
    <article data-styleguide-section="toast">
        <h2>Toast (<code>pm-toast</code>)</h2>
        <p>
            Toast se obvykle vytváří za běhu aplikace přes JS
            (<code>document.createElement</code> + <code>.show()</code>).
            Zde jen ukázka statického markupu variant:
        </p>
        <div class="styleguide-row" style="flex-direction: column; align-items: flex-start;">
            <pm-toast variant="Info" gravity="Top" position="Right">Informativní zpráva.</pm-toast>
            <pm-toast variant="Success" gravity="Top" position="Right">Úspěšně uloženo.</pm-toast>
            <pm-toast variant="Warning" gravity="Top" position="Right">Upozornění — zkontrolujte vstup.</pm-toast>
            <pm-toast variant="Error" gravity="Top" position="Right">Chyba — akce selhala.</pm-toast>
        </div>
    </article>
```

- [ ] **Step 7: Build + tests**

```
dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --nologo
```
Expected: ≥ 83 pass.

- [ ] **Step 8: Commit**

```bash
git add PmTracker.Web/TagHelpers/PmToastTagHelper.cs \
        PmTracker.Web.Tests/TagHelpers/PmToastTagHelperTests.cs \
        docs/architecture/toasts.md \
        PmTracker.Web/Views/StyleGuide/Index.cshtml
git commit -m "$(cat <<'EOF'
feat(tag-helper): pm-toast nad gov-toast (Fáze 2C)

PmToastVariant (Info/Success/Warning/Error → color), PmToastGravity
(Top/Bottom), PmToastPosition (Left/Center/Right). Vždy type=bold.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: PmSkeletonTagHelper

**Files:**
- Create: `PmTracker.Web/TagHelpers/PmSkeletonTagHelper.cs`
- Create: `PmTracker.Web.Tests/TagHelpers/PmSkeletonTagHelperTests.cs`
- Create: `docs/architecture/skeletons.md`
- Modify: `PmTracker.Web/Views/StyleGuide/Index.cshtml`

- [ ] **Step 1: Failing tests**

`PmTracker.Web.Tests/TagHelpers/PmSkeletonTagHelperTests.cs`:

```csharp
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmSkeletonTagHelperTests
{
    [Fact]
    public void Default_RendersGovSkeletonMedium()
    {
        var tagHelper = new PmSkeletonTagHelper();

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("<gov-skeleton", html);
        Assert.Contains("size=\"m\"", html);
    }

    [Fact]
    public void CircleShape_SetsShapeAttribute()
    {
        var tagHelper = new PmSkeletonTagHelper
        {
            Shape = PmSkeletonShape.Circle
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("shape=\"circle\"", html);
    }

    [Fact]
    public void LargeSize_SetsSizeAttribute()
    {
        var tagHelper = new PmSkeletonTagHelper
        {
            Size = PmComponentSize.Large
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("size=\"l\"", html);
    }
}
```

- [ ] **Step 2: Verify failing**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmSkeletonTagHelperTests" --nologo`
Expected: build error.

- [ ] **Step 3: Implementation**

`PmTracker.Web/TagHelpers/PmSkeletonTagHelper.cs`:

```csharp
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>Tvar skeletonu.</summary>
public enum PmSkeletonShape
{
    /// <summary>Obdélník (výchozí).</summary>
    Default,
    /// <summary>Kruh (pro avatary apod.).</summary>
    Circle
}

/// <summary>
/// <pre><pm-skeleton shape="Circle" size="Medium" /></pre>
///
/// Thin wrapper nad gov-skeleton. Placeholder prvek zobrazovaný během
/// načítání obsahu — animovaný prázdný obdélník nebo kruh.
///
/// Dokumentace: docs/architecture/skeletons.md
/// </summary>
[HtmlTargetElement("pm-skeleton")]
public sealed class PmSkeletonTagHelper : TagHelper
{
    public PmSkeletonShape Shape { get; set; } = PmSkeletonShape.Default;
    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-skeleton";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());
        if (Shape == PmSkeletonShape.Circle)
            output.Attributes.SetAttribute("shape", "circle");
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Verify pass**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmSkeletonTagHelperTests" --nologo`
Expected: 3 pass.

- [ ] **Step 5: Docs**

`docs/architecture/skeletons.md`:

```markdown
# `pm-skeleton`

Thin wrapper nad `<gov-skeleton>`. Placeholder zobrazovaný během načítání dat.

## Použití

(razor triple backticks)
@if (Model.IsLoading)
{
    <pm-skeleton size="Medium" />
    <pm-skeleton shape="Circle" size="Large" />
}
else
{
    <div>@Model.Content</div>
}
(close code block)

## API

| Property | Typ | Popis |
|---|---|---|
| `Shape` | `PmSkeletonShape` | Default (obdélník) / Circle. |
| `Size` | `PmComponentSize` | s/m/l. |

## Kdy použít skeleton vs. loading

- **Skeleton**: obsah načítá se z dat, chceme ukázat strukturu (karty, text, avatar)
- **Loading**: spinner pro neurčitou dobu čekání (submit formuláře, globální akce)

## Viz také
- [pm-loading](./loadings.md)
```

- [ ] **Step 6: StyleGuide**

Za toast article:

```razor
    <article data-styleguide-section="skeleton">
        <h2>Skeleton (<code>pm-skeleton</code>)</h2>
        <p>Placeholder během načítání.</p>

        <h3>Velikosti</h3>
        <div class="styleguide-row" style="flex-direction: column; align-items: flex-start; gap: var(--pm-spacing-xs);">
            <pm-skeleton size="Small" />
            <pm-skeleton size="Medium" />
            <pm-skeleton size="Large" />
        </div>

        <h3>Kruh (avatar)</h3>
        <div class="styleguide-row">
            <pm-skeleton shape="Circle" size="Medium" />
            <pm-skeleton shape="Circle" size="Large" />
        </div>
    </article>
```

- [ ] **Step 7: Build + tests**

```
dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --nologo
```
Expected: ≥ 86 pass.

- [ ] **Step 8: Commit**

```bash
git add PmTracker.Web/TagHelpers/PmSkeletonTagHelper.cs \
        PmTracker.Web.Tests/TagHelpers/PmSkeletonTagHelperTests.cs \
        docs/architecture/skeletons.md \
        PmTracker.Web/Views/StyleGuide/Index.cshtml
git commit -m "$(cat <<'EOF'
feat(tag-helper): pm-skeleton nad gov-skeleton (Fáze 2C)

PmSkeletonShape (Default/Circle). Size přes PmComponentSize.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: PmLoadingTagHelper

**Files:**
- Create: `PmTracker.Web/TagHelpers/PmLoadingTagHelper.cs`
- Create: `PmTracker.Web.Tests/TagHelpers/PmLoadingTagHelperTests.cs`
- Create: `docs/architecture/loadings.md`
- Modify: `PmTracker.Web/Views/StyleGuide/Index.cshtml`

- [ ] **Step 1: Failing tests**

`PmTracker.Web.Tests/TagHelpers/PmLoadingTagHelperTests.cs`:

```csharp
using System.Threading.Tasks;
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmLoadingTagHelperTests
{
    [Fact]
    public async Task Default_RendersGovLoadingMedium()
    {
        var tagHelper = new PmLoadingTagHelper();
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-loading", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("gov-loading", output.TagName);
        Assert.Equal("m", output.Attributes["size"]?.Value?.ToString());
    }

    [Fact]
    public async Task Label_RendersAsTextContent()
    {
        var tagHelper = new PmLoadingTagHelper
        {
            Label = "Načítám data…"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-loading", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("Načítám data…", html);
    }

    [Fact]
    public async Task XssInLabel_IsEscaped()
    {
        var tagHelper = new PmLoadingTagHelper
        {
            Label = "<script>alert(1)</script>"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-loading", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.DoesNotContain("<script>alert", html);
        Assert.Contains("&lt;script&gt;", html);
    }
}
```

- [ ] **Step 2: Verify failing**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmLoadingTagHelperTests" --nologo`
Expected: build error.

- [ ] **Step 3: Implementation**

`PmTracker.Web/TagHelpers/PmLoadingTagHelper.cs`:

```csharp
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-loading label="Načítám…" size="Medium" /></pre>
///
/// Thin wrapper nad gov-loading. Zobrazí animovaný spinner s volitelným
/// popiskem pod ním.
///
/// Dokumentace: docs/architecture/loadings.md
/// </summary>
[HtmlTargetElement("pm-loading")]
public sealed class PmLoadingTagHelper : TagHelper
{
    private static readonly HtmlEncoder ContentEncoder =
        HtmlEncoder.Create(UnicodeRanges.All);

    public string? Label { get; set; }
    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-loading";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());

        if (!string.IsNullOrEmpty(Label))
            output.Content.SetHtmlContent(ContentEncoder.Encode(Label));

        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Verify pass**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmLoadingTagHelperTests" --nologo`
Expected: 3 pass.

- [ ] **Step 5: Docs**

`docs/architecture/loadings.md`:

```markdown
# `pm-loading`

Thin wrapper nad `<gov-loading>`. Animovaný spinner, volitelně s popiskem.

## Použití

(razor triple backticks)
<pm-loading label="Načítám data…" size="Medium" />

<!-- jen spinner -->
<pm-loading size="Small" />
(close code block)

## API

| Property | Typ | Popis |
|---|---|---|
| `Label` | `string?` | Volitelný text pod/vedle spinneru. |
| `Size` | `PmComponentSize` | s/m/l. |

## Kdy použít loading vs. skeleton

- **Loading**: neurčitá čekací doba (submit, dlouhá operace) — spinner
- **Skeleton**: zobrazujeme strukturu budoucího obsahu — placeholder tvary

## Viz také
- [pm-skeleton](./skeletons.md)
```

- [ ] **Step 6: StyleGuide**

Za skeleton article:

```razor
    <article data-styleguide-section="loading">
        <h2>Loading (<code>pm-loading</code>)</h2>
        <p>Spinner pro neurčitou dobu čekání.</p>
        <div class="styleguide-row">
            <pm-loading size="Small" label="Načítám…" />
            <pm-loading size="Medium" label="Zpracovávám data…" />
            <pm-loading size="Large" label="Probíhá import…" />
        </div>
    </article>
```

- [ ] **Step 7: Build + tests**

```
dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --nologo
```
Expected: ≥ 89 pass.

- [ ] **Step 8: Commit**

```bash
git add PmTracker.Web/TagHelpers/PmLoadingTagHelper.cs \
        PmTracker.Web.Tests/TagHelpers/PmLoadingTagHelperTests.cs \
        docs/architecture/loadings.md \
        PmTracker.Web/Views/StyleGuide/Index.cshtml
git commit -m "$(cat <<'EOF'
feat(tag-helper): pm-loading nad gov-loading (Fáze 2C)

Volitelný Label (Unicode-aware encoding), Size přes PmComponentSize.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: Konsolidace — E2E smoke test + README update

**Files:**
- Modify: `PmTracker.Tests.E2E/Scenarios/StyleGuideRenderTests.cs`
- Modify: `docs/architecture/README.md`

- [ ] **Step 1: Rozšířit E2E test**

V `PmTracker.Tests.E2E/Scenarios/StyleGuideRenderTests.cs` najdi metodu `StyleGuide_ObsahujeSekceFaze2B`. Přidej ZA ni:

```csharp
    [Fact]
    public async Task StyleGuide_ObsahujeSekceFaze2C()
    {
        var page = await _fixture.NewPageAsync();

        var response = await page.GotoAsync($"{_fixture.BaseUrl}/StyleGuide");
        response!.Status.Should().Be(200);

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        foreach (var section in new[] { "dialog", "tooltip", "toast", "skeleton", "loading" })
        {
            var count = await page.Locator($"[data-styleguide-section=\"{section}\"]").CountAsync();
            count.Should().Be(1, $"Sekce {section} musí být přesně jednou ve StyleGuide (Fáze 2C)");
        }

        // Sanity: gov komponenty odpovídající novým pm-* wrapperům jsou přítomné
        (await page.Locator("gov-dialog").CountAsync())
            .Should().BeGreaterThanOrEqualTo(1, "1 pm-dialog ukázka");
        (await page.Locator("gov-tooltip").CountAsync())
            .Should().BeGreaterThanOrEqualTo(2, "2 pm-tooltip ukázky");
        (await page.Locator("gov-toast").CountAsync())
            .Should().BeGreaterThanOrEqualTo(4, "4 pm-toast varianty (Info/Success/Warning/Error)");
        (await page.Locator("gov-skeleton").CountAsync())
            .Should().BeGreaterThanOrEqualTo(5, "≥5 pm-skeleton ukázek (3 velikosti + 2 kruhy)");
        (await page.Locator("gov-loading").CountAsync())
            .Should().BeGreaterThanOrEqualTo(3, "3 pm-loading velikosti");

        await page.Context.CloseAsync();
    }
```

- [ ] **Step 2: Build E2E projekt**

Run: `dotnet build PmTracker.Tests.E2E/PmTracker.Tests.E2E.csproj --nologo`
Expected: 0 errors.

- [ ] **Step 3: README update**

V `docs/architecture/README.md` najdi sekci `### Fáze 2B — navigační & layout primitivy`. Za její poslední položku (`pm-pagination`) přidej:

```markdown

### Fáze 2C — interaktivní & feedback primitivy (hotovo 2026-04-19)
- [pm-dialog — modální dialogy](dialogs.md)
- [pm-tooltip — bubliny nápovědy](tooltips.md)
- [pm-toast — notifikace u okraje](toasts.md)
- [pm-skeleton — placeholdery při načítání](skeletons.md)
- [pm-loading — spinner](loadings.md)
```

- [ ] **Step 4: Full unit test run + publish**

```
dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --nologo
dotnet publish PmTracker.Web/PmTracker.Web.csproj -c Release -o ./publish --nologo 2>&1 | tail -3
```
Expected: 89 unit tests pass, publish OK.

- [ ] **Step 5: Final commit**

```bash
git add PmTracker.Tests.E2E/Scenarios/StyleGuideRenderTests.cs \
        docs/architecture/README.md
git commit -m "$(cat <<'EOF'
test(e2e) + docs: uzavřít Fázi 2C

- StyleGuideRenderTests: test StyleGuide_ObsahujeSekceFaze2C ověří
  5 nových sekcí (dialog, tooltip, toast, skeleton, loading) +
  minimální počty gov instancí.
- docs/architecture/README.md: sekce Fáze 2C s odkazy na 5 nových
  komponentních dokumentů.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Po všech tascích

- [ ] **Finální check**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --nologo 2>&1 | tail -3
dotnet publish PmTracker.Web/PmTracker.Web.csproj -c Release -o ./publish --nologo 2>&1 | tail -3
```

Expected:
- `PmTracker.Web.Tests` ≥ 89 tests pass (72 po 2B + 17 nových z 2C: 4 + 3 + 4 + 3 + 3)
- Publish build bez chyb

---

## Self-Review

**Spec coverage:**
- ✅ `pm-dialog` → Task 1
- ✅ `pm-tooltip` → Task 2
- ✅ `pm-toast` → Task 3
- ✅ `pm-skeleton` → Task 4
- ✅ `pm-loading` → Task 5
- ✅ E2E + README → Task 6
- ✅ Unit testy (min 3 + XSS/Unicode) → každý task Step 1
- ✅ StyleGuide sekce → Tasks 1-5 Step 6
- ✅ Konvence (PascalCase, Encoders, size, doc comments)

**Placeholder scan:** Žádné "TBD" / "implement later". Všechny code blocks kompletní.

**Type consistency:**
- `PmToastVariant` (Info/Success/Warning/Error), `PmToastGravity` (Top/Bottom), `PmToastPosition` (Left/Center/Right) — definovány v Task 3, použity v tomtéž tasku
- `PmSkeletonShape` (Default/Circle) — definován v Task 4
- `PmComponentSize.ToGovAttribute()` používán ve všech (dialog kromě toho nemá size, tooltip také ne)
- `ContentEncoder` pattern — použit v dialog, tooltip, loading (kde je user-supplied text); toast a skeleton ho nepotřebují (toast má jen child content přes GetChildContentAsync, skeleton nemá žádný user text)
- Commit model: `Claude Sonnet 4.6` pro implementer tasky 1-5, `Claude Opus 4.7` pro Task 6

**Bezpečnostní check (poučení z 2B):**
- Tasks 1, 2, 5 mají XSS regresní test pro user-supplied text
- Task 3, 4 nepřidávají žádný ručně skládaný atribut z user inputu — všechny atributy jdou přes `output.Attributes.SetAttribute(key, enumMappedValue)` což framework sám encoduje

**CSS scope:**
- Fáze 2C nepřidává formulářové prvky ani globální CSS — žádný scope fix nepotřebný (všechny dosavadní CSS scope fixy z Fáze 1+2A pokrývají input/select/textarea).
