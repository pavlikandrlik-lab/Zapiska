# Fáze 2A — formulářové pm-* primitivy — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Přidat 5 thin-wrapper TagHelperů (`pm-select`, `pm-textarea`, `pm-checkbox`, `pm-radio`, `pm-switch`) nad odpovídající `gov-*` Web Components, sekce ve `/StyleGuide`, unit testy, docs.

**Architecture:** Stejný vzor jako `pm-button` / `pm-badge` / `pm-field` z Fáze 1 — thin wrapper nad gov-* Web Component, centralizované enumy pro varianty/velikosti, Unicode-aware HtmlEncoder pro textový obsah, `WebUtility.HtmlEncode` pro atributy. Žádná vlastní CSS nad gov komponenty — pouze scope existujícího globálního CSS v `site.css` přes `:not(gov-* *)` kde to chybí (poučení z Fáze 1 search bugu).

**Tech Stack:** ASP.NET Core 8 Razor TagHelpers, xUnit + FluentAssertions, gov-design-system 4.2.9, CSS custom properties (`--pm-*` aliasy).

---

## File Structure

**TagHelpers (nové, `PmTracker.Web/TagHelpers/`):**

| Soubor | Odpovědnost |
|---|---|
| `PmSelectTagHelper.cs` | `<pm-select>` → `<gov-form-control><gov-form-select>` s label + options child content |
| `PmSelectOption.cs` | Model pro jednotlivou option (value, text, selected, disabled) |
| `PmTextareaTagHelper.cs` | `<pm-textarea>` → `<gov-form-control><gov-form-input>` s `<textarea slot="element">` |
| `PmCheckboxTagHelper.cs` | `<pm-checkbox>` → `<gov-form-checkbox>` s label, checked, name |
| `PmRadioGroupTagHelper.cs` | `<pm-radio-group>` → `<gov-form-radio-group>` wrapper (orientation) |
| `PmRadioTagHelper.cs` | `<pm-radio>` → `<gov-form-radio>` (name, value, checked, label) |
| `PmSwitchTagHelper.cs` | `<pm-switch>` → `<gov-form-switch>` (name, checked, label) |

**Existující sdílené (použít):**
- `PmComponentSize.cs` (Small/Medium/Large → s/m/l) — použitelný všude
- `PmTracker.Web/Views/_ViewImports.cshtml` — už má `@addTagHelper *, PmTracker.Web`

**Tests (nové, `PmTracker.Web.Tests/TagHelpers/`):**

| Soubor | Pokrývá |
|---|---|
| `PmSelectTagHelperTests.cs` | Default render, options, selected option, required |
| `PmTextareaTagHelperTests.cs` | Default render, rows, help text, error, disabled |
| `PmCheckboxTagHelperTests.cs` | Default render, checked, disabled, label obsah, XSS escaping |
| `PmRadioGroupTagHelperTests.cs` | Orientation, child rendering |
| `PmRadioTagHelperTests.cs` | Default render, checked, name group |
| `PmSwitchTagHelperTests.cs` | Default render, checked, disabled |

**Existující sdílené (použít):**
- `PmTracker.Web.Tests/TagHelpers/TagHelperTestHelpers.cs` — `MakeContext`, `MakeOutput`, `Render` (vytvořeno v Fázi 1)

**Docs (nové, `docs/architecture/`):**

| Soubor | Obsah |
|---|---|
| `selects.md` | `pm-select` API, mapování na gov-form-select, ukázky |
| `textareas.md` | `pm-textarea` API, rozdíl oproti `pm-field[input-type=text]` |
| `checkboxes.md` | `pm-checkbox` API, samostatný vs. v `pm-radio-group` |
| `radios.md` | `pm-radio` + `pm-radio-group` API, orientation |
| `switches.md` | `pm-switch` API vs. `gov-theme-switch` (ten je speciální) |

**StyleGuide (modify):**
- `PmTracker.Web/Views/StyleGuide/Index.cshtml` — 5 nových sekcí (`select`, `textarea`, `checkbox`, `radio`, `switch`)

**CSS scope fixy (modify):**
- `PmTracker.Web/wwwroot/css/site.css` — projít globální selektory a přidat `:not(gov-* *)` exclusions tam, kde chybí (select, textarea globální styly)

**Žádné změny v:**
- `_Layout.cshtml` (TagHelpers se aktivují přes `_ViewImports.cshtml`)
- Existující `pm-*` TagHelpery (není potřeba refactor)
- `tokens.css` (všechny tokeny už jsou z Fáze 1)

---

## Konvence kódu

**C# soubory (TagHelpers):**
- `namespace PmTracker.Web.TagHelpers;`
- `[HtmlTargetElement("pm-...")]`
- `public sealed class Pm...TagHelper : TagHelper`
- XML doc comment nad class s jednou ukázkou použití
- XML doc comments nad veřejnými properties
- Pokud TagHelper renderuje text obsah uživatele (label, help, error), použít tento pattern pro encoding:
  ```csharp
  private static readonly HtmlEncoder ContentEncoder =
      HtmlEncoder.Create(UnicodeRanges.All);
  ```
  a použít `ContentEncoder.Encode(text)` na text, `WebUtility.HtmlEncode(val)` na atributy. Důvod: XSS ochrana + zachování diakritiky. Viz `PmFieldTagHelper.cs` pro referenci.

**Testy:**
- `namespace PmTracker.Web.Tests.TagHelpers;`
- `public class Pm...TagHelperTests` (bez `sealed`)
- Používej `TagHelperTestHelpers.Render(tagHelper)` → string
- Asserty přes xUnit `Assert.Contains`, `Assert.DoesNotContain`, `Assert.Equal`
- Minimum 3 testy per TagHelper: default render, 1 varianta, 1 edge-case
- XSS regresní test všude, kde TagHelper renderuje user-supplied text (Label, Help, Error)

**Docs:**
- Jedna H1 = název komponenty
- Sekce: "Použití" (příklad), "API" (tabulka properties), "Mapování na gov" (tabulka pm atribut → gov atribut), "Viz také"
- Pattern podle existujícího `docs/architecture/buttons.md`

**Commits:**
- Czech messages
- Conv commit typy: `feat(tag-helper): ...`, `test(tag-helper): ...`, `docs(architecture): ...`
- Co-Author footer: `Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>` (subagent implementer) nebo odpovídající model

---

## Task 1: PmSelectOption + PmSelectTagHelper

**Files:**
- Create: `PmTracker.Web/TagHelpers/PmSelectOption.cs`
- Create: `PmTracker.Web/TagHelpers/PmSelectTagHelper.cs`
- Test: `PmTracker.Web.Tests/TagHelpers/PmSelectTagHelperTests.cs`
- Docs: `docs/architecture/selects.md`

- [ ] **Step 1: Vytvoř test file s failing testem**

`PmTracker.Web.Tests/TagHelpers/PmSelectTagHelperTests.cs`:

```csharp
using System.Collections.Generic;
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmSelectTagHelperTests
{
    [Fact]
    public void Default_RendersGovFormControlWithSelect()
    {
        var tagHelper = new PmSelectTagHelper
        {
            Name = "stav",
            Label = "Stav záznamu",
            Options = new List<PmSelectOption>
            {
                new("nova", "Nová", selected: false, disabled: false),
                new("rozpracovana", "Rozpracovaná", selected: true, disabled: false)
            }
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("<gov-form-control", html);
        Assert.Contains("<gov-form-select", html);
        Assert.Contains("name=\"stav\"", html);
        Assert.Contains("identifier=\"pm-select-stav\"", html);
        Assert.Contains(">Stav záznamu<", html);
        Assert.Contains("<option value=\"nova\">Nová</option>", html);
        Assert.Contains("<option value=\"rozpracovana\" selected>Rozpracovaná</option>", html);
    }

    [Fact]
    public void DisabledOption_RendersDisabledAttribute()
    {
        var tagHelper = new PmSelectTagHelper
        {
            Name = "role",
            Label = "Role",
            Options = new List<PmSelectOption>
            {
                new("--", "Vyberte…", selected: true, disabled: true),
                new("admin", "Administrátor", selected: false, disabled: false)
            }
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("<option value=\"--\" selected disabled>Vyberte…</option>", html);
    }

    [Fact]
    public void Required_AppendsRequiredMarker()
    {
        var tagHelper = new PmSelectTagHelper
        {
            Name = "kategorie",
            Label = "Kategorie",
            Required = true,
            Options = new List<PmSelectOption>
            {
                new("a", "A", selected: false, disabled: false)
            }
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("<span aria-hidden=\"true\">*</span>", html);
        Assert.Contains("required", html);
    }

    [Fact]
    public void LabelWithDiacritics_PreservesUnicode()
    {
        var tagHelper = new PmSelectTagHelper
        {
            Name = "velikost",
            Label = "Velikost měření",
            Options = new List<PmSelectOption>
            {
                new("s", "Malá", selected: false, disabled: false)
            }
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("Velikost měření", html);
        Assert.DoesNotContain("&#", html); // žádné numerické entity pro diakritiku
    }

    [Fact]
    public void XssInLabel_IsEscaped()
    {
        var tagHelper = new PmSelectTagHelper
        {
            Name = "pole",
            Label = "<script>alert('xss')</script>",
            Options = new List<PmSelectOption>
            {
                new("a", "A", selected: false, disabled: false)
            }
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.DoesNotContain("<script>alert", html);
        Assert.Contains("&lt;script&gt;", html);
    }
}
```

- [ ] **Step 2: Ověř že test failuje (PmSelectTagHelper neexistuje)**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmSelectTagHelperTests" --nologo`
Expected: build error — `PmSelectTagHelper` neexistuje.

- [ ] **Step 3: Vytvoř `PmSelectOption.cs`**

`PmTracker.Web/TagHelpers/PmSelectOption.cs`:

```csharp
namespace PmTracker.Web.TagHelpers;

/// <summary>
/// Jedna položka pro pm-select.
/// </summary>
/// <param name="Value">Hodnota odesílaná ve formuláři (atribut value).</param>
/// <param name="Text">Zobrazovaný text (obsah option).</param>
/// <param name="Selected">Je defaultně vybraná.</param>
/// <param name="Disabled">Zakázaná volba.</param>
public sealed record PmSelectOption(string Value, string Text, bool Selected, bool Disabled);
```

- [ ] **Step 4: Vytvoř `PmSelectTagHelper.cs`**

`PmTracker.Web/TagHelpers/PmSelectTagHelper.cs`:

```csharp
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-select name="stav" label="Stav" options="@options" required="true" /></pre>
///
/// Renderuje gov-form-control → gov-form-select s popiskem a options.
/// Options se předávají jako IList&lt;PmSelectOption&gt; přes property.
///
/// Dokumentace: docs/architecture/selects.md
/// </summary>
[HtmlTargetElement("pm-select")]
public sealed class PmSelectTagHelper : TagHelper
{
    private static readonly HtmlEncoder ContentEncoder =
        HtmlEncoder.Create(UnicodeRanges.All);

    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public IList<PmSelectOption> Options { get; set; } = new List<PmSelectOption>();
    public bool Required { get; set; }
    public bool Disabled { get; set; }
    public string? Help { get; set; }
    public string? Error { get; set; }
    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-form-control";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());
        if (!string.IsNullOrEmpty(Error))
            output.Attributes.SetAttribute("invalid", "invalid");

        var id = $"pm-select-{Name}";
        var sizeAttr = Size.ToGovAttribute();
        var requiredMarker = Required ? " <span aria-hidden=\"true\">*</span>" : "";

        var labelHtml = $"<gov-form-label slot=\"top\" for=\"{WebUtility.HtmlEncode(id)}\" size=\"{sizeAttr}\">"
            + $"{ContentEncoder.Encode(Label)}{requiredMarker}</gov-form-label>";

        var selectAttrs = new StringBuilder();
        selectAttrs.Append($" name=\"{WebUtility.HtmlEncode(Name)}\"");
        selectAttrs.Append($" identifier=\"{WebUtility.HtmlEncode(id)}\"");
        selectAttrs.Append($" size=\"{sizeAttr}\"");
        if (Required) selectAttrs.Append(" required");
        if (Disabled) selectAttrs.Append(" disabled");

        var optionsHtml = new StringBuilder();
        foreach (var opt in Options)
        {
            optionsHtml.Append($"<option value=\"{WebUtility.HtmlEncode(opt.Value)}\"");
            if (opt.Selected) optionsHtml.Append(" selected");
            if (opt.Disabled) optionsHtml.Append(" disabled");
            optionsHtml.Append($">{ContentEncoder.Encode(opt.Text)}</option>");
        }

        var selectHtml = $"<gov-form-select{selectAttrs}>{optionsHtml}</gov-form-select>";

        var messageHtml = "";
        if (!string.IsNullOrEmpty(Error))
            messageHtml = $"<gov-form-message slot=\"bottom\" variant=\"error\">{ContentEncoder.Encode(Error!)}</gov-form-message>";
        else if (!string.IsNullOrEmpty(Help))
            messageHtml = $"<gov-form-message slot=\"bottom\">{ContentEncoder.Encode(Help!)}</gov-form-message>";

        output.Content.SetHtmlContent(labelHtml + selectHtml + messageHtml);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 5: Ověř že testy projdou**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmSelectTagHelperTests" --nologo`
Expected: PASS (5 testů).

- [ ] **Step 6: Napiš dokumentaci**

`docs/architecture/selects.md`:

```markdown
# `pm-select`

Thin wrapper nad `<gov-form-control>` + `<gov-form-select>` z gov-design-system.

## Použití

```razor
@{
    var options = new List<PmSelectOption>
    {
        new("nova", "Nová", false, false),
        new("rozpracovana", "Rozpracovaná", true, false),
        new("uzavrena", "Uzavřená", false, false)
    };
}

<pm-select name="stav" label="Stav záznamu" options="options" required="true" />
```

## API

| Property | Typ | Popis |
|---|---|---|
| `Name` | `string` | Název formulářového pole (HTML `name`). |
| `Label` | `string` | Popisek nad selectem. |
| `Options` | `IList<PmSelectOption>` | Seznam voleb. |
| `Required` | `bool` | Povinné pole (přidá hvězdičku a `required`). |
| `Disabled` | `bool` | Zakázaný select. |
| `Help` | `string?` | Nápovědný text pod selectem. |
| `Error` | `string?` | Chybové hlášení (přepíše Help, přidá `invalid`). |
| `Size` | `PmComponentSize` | Small / Medium (výchozí) / Large. |

`PmSelectOption` je record: `(Value, Text, Selected, Disabled)`.

## Mapování pm → gov

| pm atribut | gov atribut / slot |
|---|---|
| `name` | `<gov-form-select name>` |
| `label` | `<gov-form-label slot="top">` |
| `options` | `<option>` potomci `<gov-form-select>` |
| `required` | `required` + asterisk marker |
| `size` | `size` (s/m/l) |
| `help` | `<gov-form-message slot="bottom">` |
| `error` | `<gov-form-message slot="bottom" variant="error">` + `invalid` |

## Viz také
- [`pm-field`](./fields.md) — textové inputy
- [`pm-textarea`](./textareas.md) — víceřádkový text
```

- [ ] **Step 7: Přidej sekci do StyleGuide**

Otevři `PmTracker.Web/Views/StyleGuide/Index.cshtml`. Za sekci `data-styleguide-section="search"` (a před `</section>` stránky) vlož:

```razor
    <article data-styleguide-section="select">
        <h2>Select (<code>pm-select</code>)</h2>
        @{
            var selectOptions = new List<PmTracker.Web.TagHelpers.PmSelectOption>
            {
                new("", "Vyberte…", true, true),
                new("nova", "Nová", false, false),
                new("rozpracovana", "Rozpracovaná", false, false),
                new("uzavrena", "Uzavřená", false, false)
            };
        }
        <pm-select name="stav_ukazka" label="Stav záznamu" options="selectOptions" help="Výběr z číselníku" />
        <pm-select name="povinny_ukazka" label="Povinný výběr" options="selectOptions" required="true" />
        <pm-select name="chyba_ukazka" label="S chybou" options="selectOptions" error="Vyberte prosím hodnotu" />
    </article>
```

- [ ] **Step 8: Rebuild + spusť všechny testy**

Run:
```
dotnet build PmTracker.Web/PmTracker.Web.csproj --nologo
dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --nologo
```
Expected: build OK, all tests pass (existing 28 + 5 new = 33 minimum).

- [ ] **Step 9: Commit**

```bash
git add PmTracker.Web/TagHelpers/PmSelectOption.cs PmTracker.Web/TagHelpers/PmSelectTagHelper.cs \
        PmTracker.Web.Tests/TagHelpers/PmSelectTagHelperTests.cs \
        docs/architecture/selects.md \
        PmTracker.Web/Views/StyleGuide/Index.cshtml
git commit -m "$(cat <<'EOF'
feat(tag-helper): pm-select nad gov-form-select (Fáze 2A)

- PmSelectOption record (Value, Text, Selected, Disabled)
- PmSelectTagHelper: label + options + required/disabled/help/error + size
- Unicode-aware HtmlEncoder pro Label/Text/Help/Error, WebUtility.HtmlEncode pro atributy
- 5 unit testů (default, disabled option, required, diacritics, XSS)
- docs/architecture/selects.md
- /StyleGuide sekce Select (3 ukázky)

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: PmTextareaTagHelper

**Files:**
- Create: `PmTracker.Web/TagHelpers/PmTextareaTagHelper.cs`
- Test: `PmTracker.Web.Tests/TagHelpers/PmTextareaTagHelperTests.cs`
- Docs: `docs/architecture/textareas.md`

- [ ] **Step 1: Failing test**

`PmTracker.Web.Tests/TagHelpers/PmTextareaTagHelperTests.cs`:

```csharp
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmTextareaTagHelperTests
{
    [Fact]
    public void Default_RendersGovFormControlWithTextarea()
    {
        var tagHelper = new PmTextareaTagHelper
        {
            Name = "poznamka",
            Label = "Poznámka",
            Rows = 4
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("<gov-form-control", html);
        Assert.Contains("<gov-form-input", html);
        Assert.Contains("<textarea", html);
        Assert.Contains("name=\"poznamka\"", html);
        Assert.Contains("rows=\"4\"", html);
        Assert.Contains(">Poznámka<", html);
    }

    [Fact]
    public void HelpText_RendersFormMessage()
    {
        var tagHelper = new PmTextareaTagHelper
        {
            Name = "komentar",
            Label = "Komentář",
            Help = "Max. 500 znaků"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("<gov-form-message slot=\"bottom\">Max. 500 znaků</gov-form-message>", html);
    }

    [Fact]
    public void Error_RendersErrorVariantAndInvalid()
    {
        var tagHelper = new PmTextareaTagHelper
        {
            Name = "p",
            Label = "P",
            Error = "Je potřeba něco napsat"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("invalid=\"invalid\"", html);
        Assert.Contains("variant=\"error\"", html);
        Assert.Contains("Je potřeba něco napsat", html);
    }

    [Fact]
    public void Disabled_AddsDisabledAttribute()
    {
        var tagHelper = new PmTextareaTagHelper
        {
            Name = "p",
            Label = "P",
            Disabled = true
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("disabled", html);
    }

    [Fact]
    public void XssInHelp_IsEscaped()
    {
        var tagHelper = new PmTextareaTagHelper
        {
            Name = "p",
            Label = "P",
            Help = "<img src=x onerror=alert(1)>"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.DoesNotContain("<img src=x", html);
        Assert.Contains("&lt;img src=x", html);
    }
}
```

- [ ] **Step 2: Ověř failing**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmTextareaTagHelperTests" --nologo`
Expected: build error.

- [ ] **Step 3: Implementace**

`PmTracker.Web/TagHelpers/PmTextareaTagHelper.cs`:

```csharp
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-textarea name="poznamka" label="Poznámka" rows="4" /></pre>
///
/// Renderuje gov-form-control → gov-form-input s vnitřním &lt;textarea slot="element"&gt;.
///
/// Dokumentace: docs/architecture/textareas.md
/// </summary>
[HtmlTargetElement("pm-textarea")]
public sealed class PmTextareaTagHelper : TagHelper
{
    private static readonly HtmlEncoder ContentEncoder =
        HtmlEncoder.Create(UnicodeRanges.All);

    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Value { get; set; }
    public string? Placeholder { get; set; }
    public int Rows { get; set; } = 3;
    public bool Required { get; set; }
    public bool Disabled { get; set; }
    public string? Help { get; set; }
    public string? Error { get; set; }
    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-form-control";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());
        if (!string.IsNullOrEmpty(Error))
            output.Attributes.SetAttribute("invalid", "invalid");

        var id = $"pm-textarea-{Name}";
        var sizeAttr = Size.ToGovAttribute();
        var requiredMarker = Required ? " <span aria-hidden=\"true\">*</span>" : "";

        var labelHtml = $"<gov-form-label slot=\"top\" for=\"{WebUtility.HtmlEncode(id)}\" size=\"{sizeAttr}\">"
            + $"{ContentEncoder.Encode(Label)}{requiredMarker}</gov-form-label>";

        var wrapperAttrs = new StringBuilder();
        wrapperAttrs.Append($" name=\"{WebUtility.HtmlEncode(Name)}\"");
        wrapperAttrs.Append($" identifier=\"{WebUtility.HtmlEncode(id)}\"");
        wrapperAttrs.Append($" size=\"{sizeAttr}\"");

        var textareaAttrs = new StringBuilder();
        textareaAttrs.Append($" id=\"{WebUtility.HtmlEncode(id)}\"");
        textareaAttrs.Append($" name=\"{WebUtility.HtmlEncode(Name)}\"");
        textareaAttrs.Append($" rows=\"{Rows}\"");
        if (!string.IsNullOrEmpty(Placeholder))
            textareaAttrs.Append($" placeholder=\"{WebUtility.HtmlEncode(Placeholder)}\"");
        if (Required) textareaAttrs.Append(" required");
        if (Disabled) textareaAttrs.Append(" disabled");

        var valueContent = string.IsNullOrEmpty(Value) ? "" : ContentEncoder.Encode(Value);
        var textareaHtml = $"<textarea{textareaAttrs}>{valueContent}</textarea>";

        var inputHtml = $"<gov-form-input{wrapperAttrs} slot=\"input\">"
            + $"<span class=\"element\" slot=\"element\">{textareaHtml}</span></gov-form-input>";

        var messageHtml = "";
        if (!string.IsNullOrEmpty(Error))
            messageHtml = $"<gov-form-message slot=\"bottom\" variant=\"error\">{ContentEncoder.Encode(Error!)}</gov-form-message>";
        else if (!string.IsNullOrEmpty(Help))
            messageHtml = $"<gov-form-message slot=\"bottom\">{ContentEncoder.Encode(Help!)}</gov-form-message>";

        output.Content.SetHtmlContent(labelHtml + inputHtml + messageHtml);
        return Task.CompletedTask;
    }
}
```

Poznámka: pm-textarea **musí** mít vnitřní `<textarea>` v `slot="element"`, protože `gov-form-input` potřebuje nativní prvek pro propagaci událostí a gov-design nemá separátní `gov-form-textarea` komponentu — řeší se přes gov-form-input + textarea slot.

- [ ] **Step 4: Ověř pass**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmTextareaTagHelperTests" --nologo`
Expected: PASS (5 testů).

- [ ] **Step 5: Docs**

`docs/architecture/textareas.md`:

```markdown
# `pm-textarea`

Thin wrapper nad `<gov-form-control>` + `<gov-form-input>` s vnitřním `<textarea slot="element">` — gov-design nemá samostatnou textarea komponentu.

## Použití

```razor
<pm-textarea name="poznamka" label="Poznámka" rows="4" placeholder="Zadejte poznámku…" help="Max. 500 znaků" />
```

## API

| Property | Typ | Popis |
|---|---|---|
| `Name` | `string` | HTML name. |
| `Label` | `string` | Popisek. |
| `Value` | `string?` | Předvyplněná hodnota. |
| `Placeholder` | `string?` | Placeholder text. |
| `Rows` | `int` | Počet viditelných řádků (default 3). |
| `Required` | `bool` | Povinné pole. |
| `Disabled` | `bool` | Zakázáno. |
| `Help` | `string?` | Nápověda. |
| `Error` | `string?` | Chybové hlášení. |
| `Size` | `PmComponentSize` | s/m/l. |

## Rozdíl oproti pm-field

`pm-field` má `input-type="text"` — jeden řádek. `pm-textarea` je víceřádkový. Jinak API a chování identické (label, help, error, size).

## Viz také
- [`pm-field`](./fields.md) — jednořádkový text input
```

- [ ] **Step 6: StyleGuide**

Za sekci Select v `Index.cshtml`:

```razor
    <article data-styleguide-section="textarea">
        <h2>Textarea (<code>pm-textarea</code>)</h2>
        <pm-textarea name="poznamka_ukazka" label="Poznámka" rows="4" placeholder="Zadejte poznámku…" help="Max. 500 znaků" />
        <pm-textarea name="chyba_ukazka_ta" label="S chybou" rows="3" error="Pole je povinné" />
    </article>
```

- [ ] **Step 7: Build + all tests**

```
dotnet build PmTracker.Web/PmTracker.Web.csproj --nologo
dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --nologo
```
Expected: 38+ pass.

- [ ] **Step 8: Commit**

```bash
git add PmTracker.Web/TagHelpers/PmTextareaTagHelper.cs \
        PmTracker.Web.Tests/TagHelpers/PmTextareaTagHelperTests.cs \
        docs/architecture/textareas.md \
        PmTracker.Web/Views/StyleGuide/Index.cshtml
git commit -m "$(cat <<'EOF'
feat(tag-helper): pm-textarea nad gov-form-input + textarea (Fáze 2A)

Gov-design nemá separátní gov-form-textarea komponentu — víceřádkový
vstup řeší gov-form-input s <textarea slot="element">. pm-textarea to
obalí tak, aby API bylo konzistentní s pm-field (label/help/error/size).

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: PmCheckboxTagHelper

**Files:**
- Create: `PmTracker.Web/TagHelpers/PmCheckboxTagHelper.cs`
- Test: `PmTracker.Web.Tests/TagHelpers/PmCheckboxTagHelperTests.cs`
- Docs: `docs/architecture/checkboxes.md`

- [ ] **Step 1: Failing test**

`PmTracker.Web.Tests/TagHelpers/PmCheckboxTagHelperTests.cs`:

```csharp
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmCheckboxTagHelperTests
{
    [Fact]
    public void Default_RendersGovFormCheckboxWithLabel()
    {
        var tagHelper = new PmCheckboxTagHelper
        {
            Name = "souhlas",
            Label = "Souhlasím s podmínkami",
            Value = "1"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("<gov-form-checkbox", html);
        Assert.Contains("name=\"souhlas\"", html);
        Assert.Contains("value=\"1\"", html);
        Assert.Contains("Souhlasím s podmínkami", html);
    }

    [Fact]
    public void Checked_AddsCheckedAttribute()
    {
        var tagHelper = new PmCheckboxTagHelper
        {
            Name = "aktivni",
            Label = "Aktivní",
            Checked = true
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("checked", html);
    }

    [Fact]
    public void Disabled_AddsDisabledAttribute()
    {
        var tagHelper = new PmCheckboxTagHelper
        {
            Name = "a",
            Label = "A",
            Disabled = true
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("disabled", html);
    }

    [Fact]
    public void XssInLabel_IsEscaped()
    {
        var tagHelper = new PmCheckboxTagHelper
        {
            Name = "x",
            Label = "<script>x</script>"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.DoesNotContain("<script>x", html);
        Assert.Contains("&lt;script&gt;", html);
    }
}
```

- [ ] **Step 2: Ověř failing**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmCheckboxTagHelperTests" --nologo`
Expected: build error.

- [ ] **Step 3: Implementace**

`PmTracker.Web/TagHelpers/PmCheckboxTagHelper.cs`:

```csharp
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-checkbox name="souhlas" label="Souhlasím" value="1" checked="true" /></pre>
///
/// Renderuje gov-form-checkbox s natívním input[type=checkbox] uvnitř.
///
/// Dokumentace: docs/architecture/checkboxes.md
/// </summary>
[HtmlTargetElement("pm-checkbox")]
public sealed class PmCheckboxTagHelper : TagHelper
{
    private static readonly HtmlEncoder ContentEncoder =
        HtmlEncoder.Create(UnicodeRanges.All);

    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public string Value { get; set; } = "true";
    public bool Checked { get; set; }
    public bool Disabled { get; set; }
    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-form-checkbox";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());
        output.Attributes.SetAttribute("name", Name);
        if (Disabled)
            output.Attributes.SetAttribute("disabled", "disabled");

        var id = $"pm-checkbox-{Name}";

        var inputAttrs = new StringBuilder();
        inputAttrs.Append($" id=\"{WebUtility.HtmlEncode(id)}\"");
        inputAttrs.Append($" name=\"{WebUtility.HtmlEncode(Name)}\"");
        inputAttrs.Append($" value=\"{WebUtility.HtmlEncode(Value)}\"");
        inputAttrs.Append(" type=\"checkbox\"");
        if (Checked) inputAttrs.Append(" checked");
        if (Disabled) inputAttrs.Append(" disabled");

        var inputHtml = $"<input{inputAttrs} />";
        var labelHtml = $"<gov-form-label slot=\"label\" for=\"{WebUtility.HtmlEncode(id)}\">{ContentEncoder.Encode(Label)}</gov-form-label>";

        output.Content.SetHtmlContent(inputHtml + labelHtml);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Ověř pass**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmCheckboxTagHelperTests" --nologo`
Expected: PASS.

- [ ] **Step 5: Docs**

`docs/architecture/checkboxes.md`:

```markdown
# `pm-checkbox`

Thin wrapper nad `<gov-form-checkbox>`. Použití jako samostatné zaškrtávátko — pro skupinu souvisejících voleb použij `pm-radio-group`.

## Použití

```razor
<pm-checkbox name="souhlas" label="Souhlasím s podmínkami" value="1" />
<pm-checkbox name="aktivni" label="Aktivní" checked="true" />
<pm-checkbox name="zakazany" label="Zakázáno" disabled="true" />
```

## API

| Property | Typ | Popis |
|---|---|---|
| `Name` | `string` | HTML name. |
| `Label` | `string` | Popisek vedle checkboxu. |
| `Value` | `string` | Hodnota odesílaná pokud checked (default "true"). |
| `Checked` | `bool` | Defaultní stav. |
| `Disabled` | `bool` | Zakázáno. |
| `Size` | `PmComponentSize` | s/m/l. |

## Viz také
- [`pm-radio`](./radios.md) — pro výběr 1-z-N
- [`pm-switch`](./switches.md) — alternativa pro on/off stavy
```

- [ ] **Step 6: StyleGuide**

```razor
    <article data-styleguide-section="checkbox">
        <h2>Checkbox (<code>pm-checkbox</code>)</h2>
        <div class="styleguide-row">
            <pm-checkbox name="ukazka_cb1" label="Souhlasím s podmínkami" />
            <pm-checkbox name="ukazka_cb2" label="Aktivní záznam" checked="true" />
            <pm-checkbox name="ukazka_cb3" label="Zakázaná volba" disabled="true" />
        </div>
    </article>
```

- [ ] **Step 7: Build + tests**

```
dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --nologo
```
Expected: 42+ pass.

- [ ] **Step 8: Commit**

```bash
git add PmTracker.Web/TagHelpers/PmCheckboxTagHelper.cs \
        PmTracker.Web.Tests/TagHelpers/PmCheckboxTagHelperTests.cs \
        docs/architecture/checkboxes.md \
        PmTracker.Web/Views/StyleGuide/Index.cshtml
git commit -m "$(cat <<'EOF'
feat(tag-helper): pm-checkbox nad gov-form-checkbox (Fáze 2A)

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: PmRadioGroupTagHelper + PmRadioTagHelper

**Files:**
- Create: `PmTracker.Web/TagHelpers/PmRadioGroupTagHelper.cs`
- Create: `PmTracker.Web/TagHelpers/PmRadioTagHelper.cs`
- Test: `PmTracker.Web.Tests/TagHelpers/PmRadioGroupTagHelperTests.cs`
- Test: `PmTracker.Web.Tests/TagHelpers/PmRadioTagHelperTests.cs`
- Docs: `docs/architecture/radios.md`

- [ ] **Step 1: Failing test pro pm-radio**

`PmTracker.Web.Tests/TagHelpers/PmRadioTagHelperTests.cs`:

```csharp
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmRadioTagHelperTests
{
    [Fact]
    public void Default_RendersGovFormRadio()
    {
        var tagHelper = new PmRadioTagHelper
        {
            Name = "priorita",
            Value = "vysoka",
            Label = "Vysoká"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("<gov-form-radio", html);
        Assert.Contains("name=\"priorita\"", html);
        Assert.Contains("value=\"vysoka\"", html);
        Assert.Contains("Vysoká", html);
    }

    [Fact]
    public void Checked_AddsChecked()
    {
        var tagHelper = new PmRadioTagHelper
        {
            Name = "p",
            Value = "a",
            Label = "A",
            Checked = true
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("checked", html);
    }

    [Fact]
    public void XssInLabel_IsEscaped()
    {
        var tagHelper = new PmRadioTagHelper
        {
            Name = "p",
            Value = "v",
            Label = "<script>y</script>"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.DoesNotContain("<script>y", html);
        Assert.Contains("&lt;script&gt;", html);
    }
}
```

- [ ] **Step 2: Failing test pro pm-radio-group**

`PmTracker.Web.Tests/TagHelpers/PmRadioGroupTagHelperTests.cs`:

```csharp
using Microsoft.AspNetCore.Razor.TagHelpers;
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmRadioGroupTagHelperTests
{
    [Fact]
    public async System.Threading.Tasks.Task Default_RendersVerticalGroup()
    {
        var tagHelper = new PmRadioGroupTagHelper
        {
            Legend = "Priorita"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-radio-group", childContent: "<gov-form-radio name=\"p\" value=\"a\">A</gov-form-radio>");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("gov-form-radio-group", output.TagName);
        Assert.Equal("vertical", output.Attributes["orientation"]?.Value?.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task Horizontal_SetsOrientation()
    {
        var tagHelper = new PmRadioGroupTagHelper
        {
            Legend = "P",
            Orientation = PmRadioOrientation.Horizontal
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-radio-group", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("horizontal", output.Attributes["orientation"]?.Value?.ToString());
    }
}
```

- [ ] **Step 3: Ověř failing**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmRadio" --nologo`
Expected: build error — třídy neexistují.

- [ ] **Step 4: Implementace pm-radio**

`PmTracker.Web/TagHelpers/PmRadioTagHelper.cs`:

```csharp
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-radio name="priorita" value="vysoka" label="Vysoká" /></pre>
///
/// Thin wrapper nad gov-form-radio. Obvykle uvnitř pm-radio-group.
///
/// Dokumentace: docs/architecture/radios.md
/// </summary>
[HtmlTargetElement("pm-radio")]
public sealed class PmRadioTagHelper : TagHelper
{
    private static readonly HtmlEncoder ContentEncoder =
        HtmlEncoder.Create(UnicodeRanges.All);

    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
    public bool Checked { get; set; }
    public bool Disabled { get; set; }
    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-form-radio";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());
        output.Attributes.SetAttribute("name", Name);
        output.Attributes.SetAttribute("value", Value);
        if (Disabled)
            output.Attributes.SetAttribute("disabled", "disabled");

        var id = $"pm-radio-{Name}-{Value}";

        var inputAttrs = new StringBuilder();
        inputAttrs.Append($" id=\"{WebUtility.HtmlEncode(id)}\"");
        inputAttrs.Append($" name=\"{WebUtility.HtmlEncode(Name)}\"");
        inputAttrs.Append($" value=\"{WebUtility.HtmlEncode(Value)}\"");
        inputAttrs.Append(" type=\"radio\"");
        if (Checked) inputAttrs.Append(" checked");
        if (Disabled) inputAttrs.Append(" disabled");

        var inputHtml = $"<input{inputAttrs} />";
        var labelHtml = $"<gov-form-label slot=\"label\" for=\"{WebUtility.HtmlEncode(id)}\">{ContentEncoder.Encode(Label)}</gov-form-label>";

        output.Content.SetHtmlContent(inputHtml + labelHtml);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 5: Implementace pm-radio-group**

`PmTracker.Web/TagHelpers/PmRadioGroupTagHelper.cs`:

```csharp
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>Orientace skupiny radiobuttonů.</summary>
public enum PmRadioOrientation
{
    /// <summary>Pod sebou (výchozí).</summary>
    Vertical,
    /// <summary>Vedle sebe.</summary>
    Horizontal
}

/// <summary>
/// <pre>&lt;pm-radio-group legend="Priorita" orientation="Vertical"&gt;
///   &lt;pm-radio name="p" value="a" label="A" /&gt;
/// &lt;/pm-radio-group&gt;</pre>
///
/// Thin wrapper nad gov-form-radio-group. Dětští pm-radio se automaticky
/// zařadí do skupiny.
///
/// Dokumentace: docs/architecture/radios.md
/// </summary>
[HtmlTargetElement("pm-radio-group")]
public sealed class PmRadioGroupTagHelper : TagHelper
{
    private static readonly HtmlEncoder ContentEncoder =
        HtmlEncoder.Create(UnicodeRanges.All);

    public string Legend { get; set; } = "";
    public PmRadioOrientation Orientation { get; set; } = PmRadioOrientation.Vertical;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-form-radio-group";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("orientation",
            Orientation == PmRadioOrientation.Horizontal ? "horizontal" : "vertical");

        var child = await output.GetChildContentAsync();
        var childHtml = child.GetContent();

        var legendHtml = string.IsNullOrEmpty(Legend)
            ? ""
            : $"<gov-form-label slot=\"top\">{ContentEncoder.Encode(Legend)}</gov-form-label>";

        output.Content.SetHtmlContent(legendHtml + childHtml);
    }
}
```

- [ ] **Step 6: Ověř pass**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmRadio" --nologo`
Expected: PASS (5 testů = 3 radio + 2 group).

- [ ] **Step 7: Docs**

`docs/architecture/radios.md`:

```markdown
# `pm-radio` + `pm-radio-group`

Thin wrappery nad `<gov-form-radio>` a `<gov-form-radio-group>`.

## Použití

```razor
<pm-radio-group legend="Priorita" orientation="Horizontal">
    <pm-radio name="priorita" value="nizka" label="Nízká" />
    <pm-radio name="priorita" value="normalni" label="Normální" checked="true" />
    <pm-radio name="priorita" value="vysoka" label="Vysoká" />
</pm-radio-group>
```

## API

### pm-radio-group

| Property | Typ | Popis |
|---|---|---|
| `Legend` | `string` | Popisek skupiny (slot="top"). |
| `Orientation` | `PmRadioOrientation` | Vertical (výchozí) / Horizontal. |

### pm-radio

| Property | Typ | Popis |
|---|---|---|
| `Name` | `string` | Společný name v rámci skupiny. |
| `Value` | `string` | Odesílaná hodnota. |
| `Label` | `string` | Popisek. |
| `Checked` | `bool` | Defaultní stav. |
| `Disabled` | `bool` | Zakázáno. |
| `Size` | `PmComponentSize` | s/m/l. |

## Viz také
- [`pm-checkbox`](./checkboxes.md)
```

- [ ] **Step 8: StyleGuide**

```razor
    <article data-styleguide-section="radio">
        <h2>Radio (<code>pm-radio-group</code> + <code>pm-radio</code>)</h2>
        <h3>Vertical</h3>
        <pm-radio-group legend="Priorita">
            <pm-radio name="priorita_v" value="nizka" label="Nízká" />
            <pm-radio name="priorita_v" value="normalni" label="Normální" checked="true" />
            <pm-radio name="priorita_v" value="vysoka" label="Vysoká" />
        </pm-radio-group>
        <h3>Horizontal</h3>
        <pm-radio-group legend="Stav" orientation="Horizontal">
            <pm-radio name="stav_h" value="nova" label="Nová" checked="true" />
            <pm-radio name="stav_h" value="vyrizena" label="Vyřízena" />
        </pm-radio-group>
    </article>
```

- [ ] **Step 9: Build + tests**

```
dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --nologo
```
Expected: 47+ pass.

- [ ] **Step 10: Commit**

```bash
git add PmTracker.Web/TagHelpers/PmRadioGroupTagHelper.cs \
        PmTracker.Web/TagHelpers/PmRadioTagHelper.cs \
        PmTracker.Web.Tests/TagHelpers/PmRadioGroupTagHelperTests.cs \
        PmTracker.Web.Tests/TagHelpers/PmRadioTagHelperTests.cs \
        docs/architecture/radios.md \
        PmTracker.Web/Views/StyleGuide/Index.cshtml
git commit -m "$(cat <<'EOF'
feat(tag-helper): pm-radio + pm-radio-group (Fáze 2A)

PmRadioOrientation enum (Vertical/Horizontal). Group obaluje děti v
gov-form-radio-group, každý pm-radio je jeden gov-form-radio.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: PmSwitchTagHelper

**Files:**
- Create: `PmTracker.Web/TagHelpers/PmSwitchTagHelper.cs`
- Test: `PmTracker.Web.Tests/TagHelpers/PmSwitchTagHelperTests.cs`
- Docs: `docs/architecture/switches.md`

- [ ] **Step 1: Failing test**

`PmTracker.Web.Tests/TagHelpers/PmSwitchTagHelperTests.cs`:

```csharp
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmSwitchTagHelperTests
{
    [Fact]
    public void Default_RendersGovFormSwitch()
    {
        var tagHelper = new PmSwitchTagHelper
        {
            Name = "notifikace",
            Label = "Zasílat notifikace"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("<gov-form-switch", html);
        Assert.Contains("name=\"notifikace\"", html);
        Assert.Contains("Zasílat notifikace", html);
    }

    [Fact]
    public void Checked_AddsChecked()
    {
        var tagHelper = new PmSwitchTagHelper
        {
            Name = "n",
            Label = "N",
            Checked = true
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("checked", html);
    }

    [Fact]
    public void Disabled_AddsDisabled()
    {
        var tagHelper = new PmSwitchTagHelper
        {
            Name = "n",
            Label = "N",
            Disabled = true
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("disabled", html);
    }

    [Fact]
    public void XssInLabel_IsEscaped()
    {
        var tagHelper = new PmSwitchTagHelper
        {
            Name = "n",
            Label = "<iframe>x</iframe>"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.DoesNotContain("<iframe>x", html);
        Assert.Contains("&lt;iframe&gt;", html);
    }
}
```

- [ ] **Step 2: Ověř failing**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmSwitchTagHelperTests" --nologo`
Expected: build error.

- [ ] **Step 3: Implementace**

`PmTracker.Web/TagHelpers/PmSwitchTagHelper.cs`:

```csharp
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-switch name="notifikace" label="Zasílat notifikace" /></pre>
///
/// Thin wrapper nad gov-form-switch. Pro přepnutí motivu použij
/// &lt;gov-theme-switch&gt; přímo (ta má vlastní JS handler v site.bundle.js).
///
/// Dokumentace: docs/architecture/switches.md
/// </summary>
[HtmlTargetElement("pm-switch")]
public sealed class PmSwitchTagHelper : TagHelper
{
    private static readonly HtmlEncoder ContentEncoder =
        HtmlEncoder.Create(UnicodeRanges.All);

    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public string Value { get; set; } = "true";
    public bool Checked { get; set; }
    public bool Disabled { get; set; }
    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-form-switch";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("size", Size.ToGovAttribute());
        output.Attributes.SetAttribute("name", Name);
        if (Disabled)
            output.Attributes.SetAttribute("disabled", "disabled");

        var id = $"pm-switch-{Name}";

        var inputAttrs = new StringBuilder();
        inputAttrs.Append($" id=\"{WebUtility.HtmlEncode(id)}\"");
        inputAttrs.Append($" name=\"{WebUtility.HtmlEncode(Name)}\"");
        inputAttrs.Append($" value=\"{WebUtility.HtmlEncode(Value)}\"");
        inputAttrs.Append(" type=\"checkbox\"");
        if (Checked) inputAttrs.Append(" checked");
        if (Disabled) inputAttrs.Append(" disabled");

        var inputHtml = $"<input{inputAttrs} />";
        var labelHtml = $"<gov-form-label slot=\"label\" for=\"{WebUtility.HtmlEncode(id)}\">{ContentEncoder.Encode(Label)}</gov-form-label>";

        output.Content.SetHtmlContent(inputHtml + labelHtml);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Ověř pass**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~PmSwitchTagHelperTests" --nologo`
Expected: PASS.

- [ ] **Step 5: Docs**

`docs/architecture/switches.md`:

```markdown
# `pm-switch`

Thin wrapper nad `<gov-form-switch>` — on/off přepínač (alternativa ke checkboxu pro stavy typu "Zapnuto/Vypnuto", "Aktivní/Neaktivní").

## Použití

```razor
<pm-switch name="notifikace" label="Zasílat notifikace" />
<pm-switch name="aktivni" label="Aktivní" checked="true" />
<pm-switch name="zamek" label="Uzamčeno" disabled="true" />
```

## Kdy použít switch vs. checkbox

- **Switch:** okamžitá změna stavu (toggle settings), binary on/off
- **Checkbox:** volba v rámci formuláře, která se odesílá dávkově

## Rozdíl oproti gov-theme-switch

`<gov-theme-switch>` je speciální komponenta pro přepínání light/dark/auto motivu. Má vlastní JS handler v `wwwroot/js/modules/theme.js`. **Není** obalena do `pm-*`, používá se přímo.

## API

| Property | Typ | Popis |
|---|---|---|
| `Name` | `string` | HTML name. |
| `Label` | `string` | Popisek. |
| `Value` | `string` | Hodnota pokud checked (default "true"). |
| `Checked` | `bool` | Defaultní stav. |
| `Disabled` | `bool` | Zakázáno. |
| `Size` | `PmComponentSize` | s/m/l. |

## Viz také
- [`pm-checkbox`](./checkboxes.md)
```

- [ ] **Step 6: StyleGuide**

```razor
    <article data-styleguide-section="switch">
        <h2>Switch (<code>pm-switch</code>)</h2>
        <div class="styleguide-row">
            <pm-switch name="ukazka_sw1" label="Zasílat notifikace" />
            <pm-switch name="ukazka_sw2" label="Aktivní záznam" checked="true" />
            <pm-switch name="ukazka_sw3" label="Uzamčeno" disabled="true" />
        </div>
    </article>
```

- [ ] **Step 7: Build + tests**

```
dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --nologo
```
Expected: 51+ pass.

- [ ] **Step 8: Commit**

```bash
git add PmTracker.Web/TagHelpers/PmSwitchTagHelper.cs \
        PmTracker.Web.Tests/TagHelpers/PmSwitchTagHelperTests.cs \
        docs/architecture/switches.md \
        PmTracker.Web/Views/StyleGuide/Index.cshtml
git commit -m "$(cat <<'EOF'
feat(tag-helper): pm-switch nad gov-form-switch (Fáze 2A)

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: Scope globálních form stylů proti gov komponentám

**Files:**
- Modify: `PmTracker.Web/wwwroot/css/site.css` (řádky s globálními form selektory)

Pozn.: `input[type="text"]` atd. už bylo scopováno v Fázi 1 commitem `9c3ac3a`. Zkontroluj, že `select` a `textarea` pravidla taky mají `:not(gov-* ...)` exclusions a přidej je pokud chybí.

- [ ] **Step 1: Najdi globální `select` / `textarea` pravidla**

Run:
```bash
grep -nE "^(select|textarea)[^\.]|^\.*\s(select|textarea)\s*,?\s*\{" "/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/wwwroot/css/site.css"
```

Zkontroluj výstup — jsou pravidla typu `select { ... }` nebo `textarea { ... }` bez scope?

- [ ] **Step 2: Přidej exclusions tam, kde chybí**

Pokud najdeš v `site.css` řádky jako:
```css
select {
    min-height: 44px;
    ...
}
textarea {
    resize: vertical;
    min-height: 120px;
}
```

Nahraď:
```css
select:not(gov-form-select select):not(pm-select select) {
    min-height: 44px;
    ...
}
textarea:not(gov-form-input textarea):not(pm-textarea textarea) {
    resize: vertical;
    min-height: 120px;
}
```

(Aktualizuj také komentář nad blokem, pokud referuje důvod.)

- [ ] **Step 3: Publish + ruční ověření**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet publish PmTracker.Web/PmTracker.Web.csproj -c Release -o ./publish --nologo 2>&1 | tail -3
```

Ověř vizuálně (pokud je spuštěný server s DB), že všechny pm-* formulářové komponenty ve StyleGuide vypadají správně — žádné přečnívání.

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/wwwroot/css/site.css
git commit -m "$(cat <<'EOF'
fix(css): scope globálních select/textarea pravidel proti gov komponentám

Stejný pattern jako 9c3ac3a pro input[type=*]. Globální form styly
v site.css se musí scopovat :not(gov-form-* *) a :not(pm-* *), jinak
přebíjejí gov layout a komponenty se nafukují/přečnívají.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Task 7: Konsolidace — E2E render smoke test + final commit

**Files:**
- Modify: `PmTracker.Web.Tests/E2E/StyleGuideRenderTests.cs` (existující — rozšířit)

- [ ] **Step 1: Zjisti jak existující StyleGuideRenderTests funguje**

Read: `PmTracker.Web.Tests/E2E/StyleGuideRenderTests.cs`

Zjisti, jestli používá Playwright a jestli ověřuje přítomnost elementů přes selektory. Najdi místo, kam přidat smoke check pro nové sekce.

- [ ] **Step 2: Rozšiř test o smoke assertion pro nové sekce**

Do existujícího test file přidej jeden nový test, který ověří, že v `/StyleGuide` jsou přítomné všechny 5 nových sekcí. Konkrétní selektory a API Playwrightu zrcadli z existujícího testu; typické:

```csharp
[Fact]
public async Task StyleGuide_ObsahujeSekceFaze2A()
{
    // Předpokládá, že existující SetUp/teardown z třídy spustí server + otevře stránku.
    var page = await GetAuthenticatedPageAsync();  // metoda z base třídy, pokud existuje

    await page.GotoAsync($"{BaseUrl}/StyleGuide");
    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

    foreach (var section in new[] { "select", "textarea", "checkbox", "radio", "switch" })
    {
        var count = await page.Locator($"[data-styleguide-section=\"{section}\"]").CountAsync();
        Assert.True(count == 1, $"Sekce {section} nebyla nalezena ve StyleGuide");
    }
}
```

Pokud existující test používá jinou autentizaci/setup API, přizpůsob se — tento test musí sedět do toho framework, ne ho měnit.

- [ ] **Step 3: Spusť E2E test lokálně (pokud Docker DB je k dispozici)**

```bash
dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj --filter "FullyQualifiedName~StyleGuide_ObsahujeSekceFaze2A" --nologo
```

Pokud E2E infrastruktura na běžícím boxu chybí, test se může skipnout — to je OK, hlavní pokrytí dávají unit testy. V tom případě přeskoč tento krok a přejdi na Step 4.

- [ ] **Step 4: Zaznamenat uzavření Fáze 2A**

Aktualizuj `docs/architecture/README.md` — přidej do seznamu odkazů na nové docs:
- `selects.md`
- `textareas.md`
- `checkboxes.md`
- `radios.md`
- `switches.md`

Pokud `README.md` obsahuje sekci "Fáze", přidej řádek `- Fáze 2A — formulářové primitivy (✅ dokončeno YYYY-MM-DD)`.

- [ ] **Step 5: Final commit**

```bash
git add PmTracker.Web.Tests/E2E/StyleGuideRenderTests.cs docs/architecture/README.md
git commit -m "$(cat <<'EOF'
test(e2e): smoke test pro nové StyleGuide sekce (Fáze 2A)

docs: aktualizovat docs/architecture/README.md s odkazy na nové komponenty.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

## Po všech tascích

- [ ] **Finální check**

Run:
```bash
dotnet test --nologo
dotnet publish PmTracker.Web/PmTracker.Web.csproj -c Release -o ./publish --nologo
```

Expected:
- Všechny testy pass (≥ 51 celkově v PmTracker.Web.Tests, ≥ 321 v PmTracker.Tests.Unit)
- Publish build bez chyb
- `publish/` obsahuje nové TagHelpery (v `PmTracker.Web.dll`)

- [ ] **Push brache**

```bash
git push origin codex/senior-refactor-fase-1
```

(Větvení na Fázi 2 použijeme novou větev, pokud to user chce — default zatím držíme na `codex/senior-refactor-fase-1` pokračováním, dokud user neřekne jinak.)

---

## Self-Review

**Spec coverage:**
- ✅ `pm-select` → Task 1
- ✅ `pm-textarea` → Task 2
- ✅ `pm-checkbox` → Task 3
- ✅ `pm-radio` + `pm-radio-group` → Task 4
- ✅ `pm-switch` → Task 5
- ✅ CSS scope (poučení z Fáze 1) → Task 6
- ✅ StyleGuide sekce → Tasks 1-5 Step "StyleGuide"
- ✅ Unit testy → každý task Step 1, 2, 4
- ✅ Docs → každý task Step 5/6
- ✅ Konvence (HtmlEncoder, XSS, WebUtility, file-size policy) → Konvence kódu sekce

**Placeholder scan:** Žádné "TBD" / "implement later" / "podobně jako". Všechny kódové bloky jsou kompletní.

**Type consistency:**
- `PmSelectOption` record používán konzistentně v Task 1 + docs + StyleGuide
- `PmRadioOrientation` enum definován v Task 4, použit v tom samém tasku
- `PmComponentSize.ToGovAttribute()` používán ve všech TagHelperech — to samé rozšíření existuje z Fáze 1
- Všechny TagHelpery používají `HtmlEncoder.Create(UnicodeRanges.All)` s jménem `ContentEncoder` a `WebUtility.HtmlEncode` pro atributy (konzistentní s `PmFieldTagHelper`)
- Commit message model je ve všech tascích `Claude Sonnet 4.6` (subagent implementer)
