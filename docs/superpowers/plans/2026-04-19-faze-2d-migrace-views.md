# Fáze 2D — Migrace Views na pm-* wrappery

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrovat existující Razor Views z ad-hoc `.btn`/`<input type="search">`/přímých `<gov-*>` tagů na unifikované `pm-*` TagHelper wrappery z Fáze 2 a opravit bugy v zobrazení jednání.

**Architecture:** Fáze 2D je čistě migrační — žádné nové TagHelpery, žádné nové CSS třídy. Jde o záměnu 1:1 na použití existujících `pm-button`, `pm-link`, `pm-search`/`gov-form-search` a opravu známých bugů v `_ProjectMeetingsTab.cshtml`. Modální systém (`modal-overlay` + AJAX) se **neřeší** — má vlastní coupling na `data-modal-*` + AJAX pipeline, což je samostatná fáze (2E).

**Tech Stack:** ASP.NET Core 8 MVC + Razor TagHelpers, gov-design-system 4.2.9, xUnit + FluentAssertions, Playwright E2E.

---

## Kontext před startem

### Co už máme (Fáze 1 + 2A/2B/2C)

21 `pm-*` TagHelperů v [PmTracker.Web/TagHelpers/](../../PmTracker.Web/TagHelpers/):
- Fáze 1: `pm-button`, `pm-field`, `pm-badge`, `pm-icon`
- Fáze 2A: `pm-select`, `pm-textarea`, `pm-checkbox`, `pm-radio` + `pm-radio-group`, `pm-switch`
- Fáze 2B: `pm-link`, `pm-tabs` + `pm-tabs-item`, `pm-card`, `pm-pagination`
- Fáze 2C: `pm-dialog`, `pm-tooltip`, `pm-toast`, `pm-skeleton`, `pm-loading`

Dokumentace: [docs/architecture/](../../architecture/) (20 souborů, README.md index).

### Scope 2D (co migrujeme)

Podle auditu ([grep výsledky](#audit-stávajícího-stavu)):

| Cíl migrace | Počet výskytů | Počet souborů | Poznámka |
|---|---|---|---|
| `.btn` → `pm-button` / `pm-link` | 103 | 27 | `<a class="btn">` → `pm-link`, `<button class="btn">` → `pm-button` |
| `<input type="search">` → `gov-form-search` + `gov-form-input` | 12 | 12 | Ponechat person-picker JS handler (`data-person-picker-input`) funkční |
| `<gov-button>` v `_Layout.cshtml` → `pm-button` | 2 | 1 | Pouze layout, StyleGuide necháváme (je to showcase) |
| Meetings display bug | — | 1 | [`_ProjectMeetingsTab.cshtml`](../../PmTracker.Web/Views/Projekty/_ProjectMeetingsTab.cshtml) + globální [`_DashboardMeetingsPanel.cshtml`](../../PmTracker.Web/Views/Dashboard/_DashboardMeetingsPanel.cshtml) |

### Scope 2E (odloženo, NE součást 2D)

- Migrace 33 souborů s `modal-overlay` na `pm-dialog`/`gov-dialog`. Důvod: tightly-coupled na AJAX pipeline (`data-ajax-submit`, `data-modal-url`, `data-modal-close`, `modal-floating-root` pro floating pickers) v [modals.js](../../PmTracker.Web/wwwroot/js/modules/modals.js). `gov-dialog` má jiný lifecycle (`.show()`/`.close()` metody na custom elementu), takže migrace vyžaduje refaktor AJAX modal stacku. Zdokumentovat v [docs/known-issues/modal-migration-to-pm-dialog.md](../../known-issues/) na konci 2D.

### Baseline před startem

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet build --nologo
dotnet test --nologo --filter "FullyQualifiedName!~E2E&FullyQualifiedName!~Integration" --verbosity quiet
```

Očekávané: build OK, všechny **Unit + Web.Tests** procházejí (Fáze 2 skončila na 417/417). E2E a Integration mají lokální SQL/Playwright závislosti a v CI prostředí mohou failovat — to není regrese z 2D.

---

## File Structure

### Nově vytvořené soubory

- `PmTracker.Web/TagHelpers/PmSearchTagHelper.cs` — wrapper nad `<gov-form-search>` + `<gov-form-input>` (odpovědnost: search input s tlačítkem, unifikovaný pattern pro Osoby/Ciselniky/Search/Team)
- `PmTracker.Web.Tests/TagHelpers/PmSearchTagHelperTests.cs` — unit testy
- `PmTracker.Tests.E2E/Scenarios/PhaseD_ViewsMigrationTests.cs` — E2E smoke test že migrované views fungují
- `docs/architecture/searches.md` — dokumentace pm-search
- `docs/known-issues/modal-migration-to-pm-dialog.md` — scope & blocker notes pro 2E

### Modifikované soubory

**ModalFormActions + shared partials** (1):
- `PmTracker.Web/Views/Shared/_ModalFormActions.cshtml` — migrace `<button class="btn">` na `<pm-button>`
- `PmTracker.Web/Views/Shared/_PageHeader.cshtml` — migrace `<a class="btn">` na `<pm-link variant="Button" ...>`

**Detail Jednání** (1):
- `PmTracker.Web/Views/Jednani/Detail.cshtml` — 10× `.btn`

**Projekty partials** (8):
- `_EditZaznamBasicPanel.cshtml`, `_EditZaznamForm.cshtml`, `_EditZaznamExternalPanel.cshtml`, `_ProjectRecordsTab.cshtml`, `_ProjectTeamTab.cshtml`, `_ProjectProposalsTab.cshtml`, `_ProjectScheduleTab.cshtml`, `_ProjectMeetingsTab.cshtml`, `_ZaznamCommentsPartial.cshtml`, `_ZaznamPartial.cshtml`, `Detail.cshtml`, `Index.cshtml`

**Dashboard** (4):
- `_DashboardNewsPanel.cshtml`, `_DashboardMeetingsPanel.cshtml`, `_DashboardFocusPanel.cshtml`, `News.cshtml`

**Nastaveni + Osoby + Ciselniky + Profil** (5):
- `_DetailPanel.cshtml`, `Osoby/Index.cshtml`, `Osoby/AdPersonModal.cshtml` (jen `.btn`, ne modal struktura), `_CiselnikDetail.cshtml`, `Profil/Index.cshtml`

**Layout + Project Dashboard** (2):
- `Shared/_Layout.cshtml` (2× `<gov-button>` → `<pm-button>`)
- `ProjectDashboard/Index.cshtml`, `ProjectDashboard/_RecordsPanel.cshtml`

**Search inputy** (12):
- všechny soubory ze seznamu v Task 5

**Meetings bug fix** (2):
- `_ProjectMeetingsTab.cshtml` (šipka + render logika)
- `_DashboardMeetingsPanel.cshtml` (globální záložka render logika)

---

## Task Decomposition Strategy

Úkoly jdou z nejmenšího rizika do největšího:

1. **Task 0** — baseline ověření (must-pass před startem)
2. **Task 1** — nový `pm-search` TagHelper (TDD, samostatná jednotka)
3. **Task 2** — migrace `_ModalFormActions` + `_PageHeader` (shared partials, ovlivní širokou plochu)
4. **Task 3** — migrace `.btn` v Dashboard views (čitelné, izolované)
5. **Task 4** — migrace `.btn` v Jednani/Detail, Projekty views (hlavní objem)
6. **Task 5** — migrace `<input type="search">` na `pm-search` (ponechat person-picker JS attrs)
7. **Task 6** — migrace `<gov-button>` v `_Layout.cshtml` na `pm-button`
8. **Task 7** — oprava meetings bug (šipka + render logika)
9. **Task 8** — E2E smoke test + závěrečný audit + dokumentace odložené 2E

---

## Task 0: Baseline ověření

**Files:**
- Pouze čtení, žádné změny.

- [ ] **Step 1: Ověřit čistý stav větve**

Run:
```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
git status --short
git log -1 --oneline
```

Expected: working tree clean, HEAD na posledním commitu z Fáze 2C review (`a8e9600 fix(tag-helper): pm-button XSS v Icon + doplnit XSS testy pm-field`).

- [ ] **Step 2: Baseline build**

Run: `dotnet build --nologo`

Expected: `0 chyb`, `0 upozornění`.

- [ ] **Step 3: Baseline unit testy (bez E2E/Integration)**

Run:
```bash
dotnet test --nologo --filter "FullyQualifiedName!~E2E&FullyQualifiedName!~Integration" --verbosity quiet 2>&1 | tail -10
```

Expected: `Úspěšné: 417, Neúspěšné: 0` (Web.Tests + Tests.Unit).

- [ ] **Step 4: Zaznamenat audit výchozího stavu do samostatného souboru**

Create: `docs/superpowers/plans/2026-04-19-faze-2d-baseline.md`

Obsah:
```markdown
# Fáze 2D — Baseline audit

**Datum:** 2026-04-19
**Commit:** `a8e9600`

## Počty výskytů před migrací

- `class="btn` v Views: 103 výskytů ve 27 souborech
- `<input type="search">` v Views: 12 výskytů ve 12 souborech
- `<gov-button>` v Views (mimo StyleGuide): 2 výskyty (_Layout.cshtml)
- `modal-overlay` v Views: 33 souborů (odloženo na 2E)

## Unit testy baseline

- PmTracker.Web.Tests: 95/95 pass
- PmTracker.Tests.Unit: 322/322 pass
- Celkem: 417/417 pass
```

- [ ] **Step 5: Commit baseline záznamu**

```bash
git add docs/superpowers/plans/2026-04-19-faze-2d-baseline.md
git commit -m "$(cat <<'EOF'
docs(2d): zaznamenat baseline před migrací Views

- 103 výskytů .btn, 12 input type=search, 2 gov-button mimo StyleGuide
- 417/417 unit testů pass

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 1: Nový `pm-search` TagHelper

**Files:**
- Create: `PmTracker.Web/TagHelpers/PmSearchTagHelper.cs`
- Test: `PmTracker.Web.Tests/TagHelpers/PmSearchTagHelperTests.cs`
- Create: `docs/architecture/searches.md`
- Modify: `docs/architecture/README.md` — přidat odkaz na searches.md

**Kontext:** Audit ukázal, že search inputy mají ~2 základní tvary:
1. Jednoduchý search bez submit tlačítka, jen s JS handlerem (`data-person-picker-input`, `data-collab-search`) — 10 výskytů
2. Search s submit tlačítkem (GET form) — 2 výskyty (Osoby/Index, Search/Index)

`pm-search` podpoří oba: default = jednoduchý input, s `submit="true"` se přidá gov slot pro tlačítko. `data-*` atributy se forwardnou přes standardní Razor asp-mechanismus (child content / additional attrs).

- [ ] **Step 1: Napsat první failing test (default render)**

Create file `PmTracker.Web.Tests/TagHelpers/PmSearchTagHelperTests.cs`:

```csharp
using System.Threading.Tasks;
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmSearchTagHelperTests
{
    [Fact]
    public async Task Default_RendersGovFormSearchWithInput()
    {
        var tagHelper = new PmSearchTagHelper
        {
            Name = "q",
            Placeholder = "Hledat…"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-search", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("gov-form-search", output.TagName);
        var html = TagHelperTestHelpers.Render(output);
        Assert.Contains("<gov-form-input", html);
        Assert.Contains("slot=\"input\"", html);
        Assert.Contains("name=\"q\"", html);
        Assert.Contains("placeholder=\"Hledat…\"", html);
        Assert.Contains("type=\"search\"", html);
    }
}
```

- [ ] **Step 2: Ověřit, že test selže (neexistuje PmSearchTagHelper)**

Run: `dotnet test --filter "FullyQualifiedName~PmSearchTagHelperTests" --nologo --verbosity quiet`

Expected: FAIL — `CS0246: The type or namespace name 'PmSearchTagHelper' could not be found`.

- [ ] **Step 3: Minimální implementace pm-search**

Create file `PmTracker.Web/TagHelpers/PmSearchTagHelper.cs`:

```csharp
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PmTracker.Web.TagHelpers;

/// <summary>
/// <pre><pm-search name="q" placeholder="Hledat…" value="@Model.Query" submit="true" /></pre>
///
/// Renderuje gov-form-search s vnořeným gov-form-input (slot="input")
/// a volitelným submit tlačítkem (slot="button").
///
/// Dokumentace: docs/architecture/searches.md
/// </summary>
[HtmlTargetElement("pm-search", TagStructure = TagStructure.NormalOrSelfClosing)]
public sealed class PmSearchTagHelper : TagHelper
{
    /// <summary>Atribut name (odesílá se jako GET parametr).</summary>
    public string Name { get; set; } = "q";

    /// <summary>Atribut placeholder.</summary>
    public string? Placeholder { get; set; }

    /// <summary>Aktuální hodnota (value).</summary>
    public string? Value { get; set; }

    /// <summary>Aria-label pro input (fallback když není label).</summary>
    [HtmlAttributeName("aria-label")]
    public string? AriaLabel { get; set; }

    /// <summary>Velikost: Small/Medium/Large.</summary>
    public PmComponentSize Size { get; set; } = PmComponentSize.Medium;

    /// <summary>Přidá submit tlačítko "Hledat" do slotu button.</summary>
    public bool Submit { get; set; }

    /// <summary>Pokud true, input dostane autofocus.</summary>
    public bool Autofocus { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "gov-form-search";
        output.TagMode = TagMode.StartTagAndEndTag;

        var size = Size.ToGovAttribute();
        output.Attributes.SetAttribute("color", "primary");
        output.Attributes.SetAttribute("size", size);

        // gov-form-input je generováno jako raw HTML s encodovanými atributy
        var safeName = WebUtility.HtmlEncode(Name);
        var safePlaceholder = WebUtility.HtmlEncode(Placeholder ?? string.Empty);
        var safeValue = WebUtility.HtmlEncode(Value ?? string.Empty);
        var safeAriaLabel = WebUtility.HtmlEncode(AriaLabel ?? Placeholder ?? "Hledat");
        var autofocusAttr = Autofocus ? " autofocus" : string.Empty;

        var inputHtml =
            $"<gov-form-input slot=\"input\" size=\"{size}\" name=\"{safeName}\" " +
            $"type=\"search\" placeholder=\"{safePlaceholder}\" value=\"{safeValue}\" " +
            $"aria-label=\"{safeAriaLabel}\"{autofocusAttr}></gov-form-input>";
        output.Content.AppendHtml(inputHtml);

        if (Submit)
        {
            var buttonHtml =
                $"<gov-button slot=\"button\" color=\"primary\" size=\"{size}\" type=\"solid\" native-type=\"submit\">Hledat</gov-button>";
            output.Content.AppendHtml(buttonHtml);
        }
    }
}
```

- [ ] **Step 4: Ověřit, že první test prochází**

Run: `dotnet test --filter "FullyQualifiedName~PmSearchTagHelperTests" --nologo --verbosity quiet`

Expected: PASS (1/1).

- [ ] **Step 5: Doplnit zbývající testy (submit, XSS, autofocus, velikosti)**

Replace `PmTracker.Web.Tests/TagHelpers/PmSearchTagHelperTests.cs` (celé tělo třídy):

```csharp
using System.Threading.Tasks;
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmSearchTagHelperTests
{
    [Fact]
    public async Task Default_RendersGovFormSearchWithInput()
    {
        var tagHelper = new PmSearchTagHelper
        {
            Name = "q",
            Placeholder = "Hledat…"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-search", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("gov-form-search", output.TagName);
        var html = TagHelperTestHelpers.Render(output);
        Assert.Contains("<gov-form-input", html);
        Assert.Contains("slot=\"input\"", html);
        Assert.Contains("name=\"q\"", html);
        Assert.Contains("placeholder=\"Hledat…\"", html);
        Assert.Contains("type=\"search\"", html);
    }

    [Fact]
    public async Task Default_DoesNotEmitSubmitButton()
    {
        var tagHelper = new PmSearchTagHelper { Name = "q" };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-search", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = TagHelperTestHelpers.Render(output);
        Assert.DoesNotContain("<gov-button", html);
    }

    [Fact]
    public async Task SubmitTrue_EmitsGovButtonWithSlotButton()
    {
        var tagHelper = new PmSearchTagHelper { Name = "q", Submit = true };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-search", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = TagHelperTestHelpers.Render(output);
        Assert.Contains("<gov-button", html);
        Assert.Contains("slot=\"button\"", html);
        Assert.Contains("native-type=\"submit\"", html);
        Assert.Contains(">Hledat</gov-button>", html);
    }

    [Fact]
    public async Task AutofocusTrue_EmitsAutofocusAttribute()
    {
        var tagHelper = new PmSearchTagHelper { Name = "q", Autofocus = true };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-search", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = TagHelperTestHelpers.Render(output);
        Assert.Contains("autofocus", html);
    }

    [Fact]
    public async Task LargeSize_SetsSizeL()
    {
        var tagHelper = new PmSearchTagHelper { Name = "q", Size = PmComponentSize.Large };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-search", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("l", output.Attributes["size"]?.Value?.ToString());
        var html = TagHelperTestHelpers.Render(output);
        Assert.Contains("size=\"l\"", html);
    }

    [Fact]
    public async Task XssInPlaceholder_IsEscaped()
    {
        var tagHelper = new PmSearchTagHelper
        {
            Name = "q",
            Placeholder = "\" onmouseover=\"alert(1)"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-search", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = TagHelperTestHelpers.Render(output);
        Assert.DoesNotContain("onmouseover=\"alert", html);
        Assert.Contains("&quot;", html);
    }

    [Fact]
    public async Task XssInValue_IsEscaped()
    {
        var tagHelper = new PmSearchTagHelper
        {
            Name = "q",
            Value = "<script>alert(1)</script>"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-search", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = TagHelperTestHelpers.Render(output);
        Assert.DoesNotContain("<script>alert", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task PlaceholderWithDiacritics_PreservesUnicode()
    {
        var tagHelper = new PmSearchTagHelper
        {
            Name = "q",
            Placeholder = "Jméno, příjmení…"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-search", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = TagHelperTestHelpers.Render(output);
        Assert.Contains("Jméno, příjmení", html);
        Assert.DoesNotContain("&#", html);
    }
}
```

- [ ] **Step 6: Všechny testy projít**

Run: `dotnet test --filter "FullyQualifiedName~PmSearchTagHelperTests" --nologo --verbosity quiet`

Expected: PASS (8/8).

- [ ] **Step 7: Dokumentace pm-search**

Create `docs/architecture/searches.md`:

```markdown
# `pm-search`

Thin wrapper nad `<gov-form-search>` + vnořený `<gov-form-input type="search" slot="input">`. Volitelně přidá submit tlačítko "Hledat" do slotu `button`.

## Použití

### Jednoduchý search (bez submit tlačítka, JS-driven)

```razor
<pm-search name="q" placeholder="Jméno…" aria-label="Hledaný výraz" />
```

### Search s submit tlačítkem (GET form)

```razor
<form asp-controller="Search" asp-action="Index" method="get">
    <pm-search name="q" value="@Model.Query" placeholder="Hledat…" submit="true" autofocus="true" />
</form>
```

## API

| Property | Typ | Default | Popis |
|---|---|---|---|
| `Name` | `string` | `"q"` | Atribut `name` inputu. |
| `Placeholder` | `string?` | `null` | Atribut `placeholder`. |
| `Value` | `string?` | `null` | Aktuální hodnota. |
| `AriaLabel` | `string?` | `Placeholder` nebo `"Hledat"` | Atribut `aria-label`. |
| `Size` | `PmComponentSize` | `Medium` | Velikost (s/m/l). |
| `Submit` | `bool` | `false` | Přidá `<gov-button slot="button">Hledat</gov-button>`. |
| `Autofocus` | `bool` | `false` | Přidá `autofocus` na input. |

## Mapování pm → gov

| pm atribut | gov atribut / slot |
|---|---|
| `name` / `placeholder` / `value` | na `<gov-form-input slot="input">` |
| `size` | `size` na `gov-form-search` i `gov-form-input` |
| `submit="true"` | přidá `<gov-button slot="button">` |
| `autofocus="true"` | atribut na `<gov-form-input>` |

## Poznámky

- Všechny user-supplied hodnoty (`Placeholder`, `Value`, `Name`, `AriaLabel`) jsou HTML-encodované pro ochranu před XSS při manuálním skládání atributů.
- Pro person-picker pattern (data-person-picker-input) zachovejte `<input type="search">` beze změny — pm-search se tam nehodí (gov-form-input by rozbil existující JS handler).

## Viz také
- [pm-button](./buttons.md)
- [pm-field](./fields.md)
```

- [ ] **Step 8: Registrovat dokumentaci v README**

Modify `docs/architecture/README.md`:

Najít sekci se seznamem form komponent a přidat řádek:
```markdown
- [pm-search](./searches.md) — search input s volitelným submit tlačítkem
```

- [ ] **Step 9: Celý build + všechny unit testy**

Run:
```bash
dotnet build --nologo
dotnet test --nologo --filter "FullyQualifiedName!~E2E&FullyQualifiedName!~Integration" --verbosity quiet 2>&1 | tail -5
```

Expected: build OK, `Úspěšné: 425` (417 + 8 nových).

- [ ] **Step 10: Commit**

```bash
git add PmTracker.Web/TagHelpers/PmSearchTagHelper.cs PmTracker.Web.Tests/TagHelpers/PmSearchTagHelperTests.cs docs/architecture/searches.md docs/architecture/README.md
git commit -m "$(cat <<'EOF'
feat(tag-helper): pridat pm-search wrapper nad gov-form-search

- pm-search renderuje gov-form-search + gov-form-input slot="input"
- volitelné submit tlačítko přes atribut submit="true"
- všechny user-supplied atributy HTML-encodované (XSS ochrana)
- 8 unit testů pokrývá default/submit/autofocus/size/XSS/diakritiku
- dokumentace v docs/architecture/searches.md

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: Migrace shared partials `_ModalFormActions` a `_PageHeader`

**Files:**
- Modify: `PmTracker.Web/Views/Shared/_ModalFormActions.cshtml`
- Modify: `PmTracker.Web/Views/Shared/_PageHeader.cshtml`

**Kontext:** Tyto partials jsou includované z mnoha views — migrace zde znamená, že další úkoly nemusí řešit submit/cancel tlačítka v modálech a page headerech. `_ModalFormActions` má `SubmitCssClass` property (např. `"btn primary"`, `"btn ghost danger"`) — tu zachováme (nechceme rozbít VM kontrakt), ale `btn` class se bude používat jen pro CSS obálku, která styly stejně nepoužije. Nicméně **ponecháme ji pro zpětnou kompatibilitu**: nemá smysl migrovat tlačítka, která jsou v modálech, které budou v 2E celé nahrazeny.

**Rozhodnutí scope**: `_ModalFormActions` migrujeme **JEN cancel button** (ten je jasně `class="btn"` bez varianty). Submit button má variabilní `SubmitCssClass` → necháme na 2E.

**`_PageHeader`**: dvě `<a class="btn small ghost">` tlačítka (Nastavení, Zpět) — čistá migrace na `<pm-link variant="Button" ButtonVariant="Ghost" Size="Small">`.

- [ ] **Step 1: Zkontrolovat, zda pm-link má variant="Button" + ButtonVariant**

Run:
```bash
cat PmTracker.Web/TagHelpers/PmLinkTagHelper.cs
```

**POZOR**: Pokud `PmLinkTagHelper` nepodporuje button-styled odkazy (jen obyčejný inline link), musíme použít `<pm-button Href="...">` místo `<pm-link>`. Z kódu Fáze 2B vyplývá, že pm-button podporuje `Href` property (rendruje se jako `<gov-button href="...">` — odkaz stylovaný jako button). Použijeme **pm-button s Href**.

- [ ] **Step 2: Migrovat `_PageHeader.cshtml`**

Read current file:
```bash
cat PmTracker.Web/Views/Shared/_PageHeader.cshtml
```

Replace content (očekávaný stav po migraci):

```razor
@model PageHeaderViewModel

<header class="page-header">
    <div class="page-header-main">
        <h1>@Model.Title</h1>
        @if (Model.ShowSettingsLink)
        {
            <pm-button variant="Ghost" size="Small" href="@Model.SettingsUrl">Nastavení</pm-button>
        }
        @if (!string.IsNullOrEmpty(Model.BackUrl))
        {
            <pm-button variant="Ghost" size="Small" href="@Model.BackUrl">@(Model.BackLabel ?? "Zpět")</pm-button>
        }
    </div>
    @if (!string.IsNullOrEmpty(Model.Subtitle))
    {
        <p class="page-header-subtitle">@Model.Subtitle</p>
    }
</header>
```

**Pozor:** Přesný obsah původního `_PageHeader.cshtml` si ověř při Step 2 — pokud má další logiku (např. breadcrumbs, další parametry), zachovej ji a migruj jen `<a class="btn small ghost">` řádky. Neodstraňuj nic, co jsi tam nevložil.

- [ ] **Step 3: Migrovat cancel button v `_ModalFormActions.cshtml`**

Read current file, pak modify:

```razor
@model ModalFormActionsViewModel

<div class="form-actions">
    <pm-button variant="Secondary" data-modal-close="true">@Model.CancelLabel</pm-button>
    <button class="@Model.SubmitCssClass" type="submit" disabled="@(Model.DisableSubmit ? "disabled" : null)">@Model.SubmitLabel</button>
</div>
```

**Poznámka:** Pm-button nemá `data-modal-close` jako strongly-typed property — forwarduje se jako HTML attribute přes Razor (TagHelper automaticky propouští neprefixované atributy). Pokud by to nefungovalo (což by bylo bug Fáze 1), musíme to řešit později — ale z implementace `PmButtonTagHelper.ProcessAsync` vyplývá, že `output.Attributes` zděděné z `context.AllAttributes` by se neměly ztrácet. Po Step 3 to ověřit v runtime (Task 8 E2E smoke).

- [ ] **Step 4: Build + unit testy**

Run:
```bash
dotnet build --nologo
dotnet test --nologo --filter "FullyQualifiedName!~E2E&FullyQualifiedName!~Integration" --verbosity quiet 2>&1 | tail -5
```

Expected: build OK, 425/425 pass (žádný unit test není na tyto views vázaný).

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Views/Shared/_PageHeader.cshtml PmTracker.Web/Views/Shared/_ModalFormActions.cshtml
git commit -m "$(cat <<'EOF'
refactor(views): migrovat shared partials na pm-button

- _PageHeader: a.btn → pm-button href (Nastavení + Zpět)
- _ModalFormActions: cancel button .btn → pm-button Secondary
- Submit button zůstává (variabilní CssClass, řeší se v 2E spolu s modály)

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: Migrace `.btn` v Dashboard views

**Files:**
- Modify: `PmTracker.Web/Views/Dashboard/_DashboardNewsPanel.cshtml`
- Modify: `PmTracker.Web/Views/Dashboard/_DashboardMeetingsPanel.cshtml`
- Modify: `PmTracker.Web/Views/Dashboard/_DashboardFocusPanel.cshtml`
- Modify: `PmTracker.Web/Views/Dashboard/News.cshtml`
- Modify: `PmTracker.Web/Views/Profil/Index.cshtml`

**Mapování .btn tříd → pm-button variant:**
- `.btn` (bez modifikátoru) → `variant="Secondary"` (outlined primary)
- `.btn primary` → `variant="Primary"` (solid primary)
- `.btn ghost` → `variant="Ghost"` (base neutral)
- `.btn danger` → `variant="Destructive"`
- `.btn ghost danger` → `variant="Destructive"` nebo speciální case — `SubmitCssClass` podobu neřešíme, ponecháme výjimku
- `.btn small` → `size="Small"`

**Pozor:** `<a class="btn">` → `<pm-button href="...">` (pm-button má Href property, rendruje gov-button jako odkaz).

- [ ] **Step 1: Číst a migrovat `_DashboardNewsPanel.cshtml`**

Read + modify: najít dvě výskyty `class="btn` (řádky 17, 23) a nahradit.

Původ:
```razor
<button class="btn small"
        data-dashboard-news-mark-all
        type="button">
    Označit vše jako přečtené
</button>
```

Nové:
```razor
<pm-button variant="Secondary" size="Small" data-dashboard-news-mark-all="true">
    Označit vše jako přečtené
</pm-button>
```

Původ (řádek 23):
```razor
<a class="btn ghost small" href="@Model.ListUrl">Zobrazit více</a>
```

Nové:
```razor
<pm-button variant="Ghost" size="Small" href="@Model.ListUrl">Zobrazit více</pm-button>
```

- [ ] **Step 2: Migrovat `_DashboardMeetingsPanel.cshtml`**

Jediný výskyt na řádku 15:
```razor
<a class="btn ghost small" href="@Model.ListUrl">Zobrazit více</a>
```

→

```razor
<pm-button variant="Ghost" size="Small" href="@Model.ListUrl">Zobrazit více</pm-button>
```

- [ ] **Step 3: Migrovat `_DashboardFocusPanel.cshtml`**

Řádek 15:
```razor
<a class="btn ghost small" href="@Model.ListUrl">Zobrazit vše</a>
```

→

```razor
<pm-button variant="Ghost" size="Small" href="@Model.ListUrl">Zobrazit vše</pm-button>
```

- [ ] **Step 4: Migrovat `News.cshtml`**

Řádek 20:
```razor
<a class="btn" href="@Model.LoadMoreUrl">Načíst další</a>
```

→

```razor
<pm-button variant="Secondary" href="@Model.LoadMoreUrl">Načíst další</pm-button>
```

- [ ] **Step 5: Migrovat `Profil/Index.cshtml`** (3× `.btn ghost`)

Původ (3×):
```razor
<button class="btn ghost" type="button" data-print-preference-reset>Zrušit uloženou volbu</button>
<button class="btn ghost" type="button" data-project-filter-preferences-reset>Smazat uložené projektové filtry</button>
<button class="btn ghost" type="button" data-record-editor-preference-reset>Zrušit uloženou volbu</button>
```

Nové (3×, zachovat data-* atributy pro JS):
```razor
<pm-button variant="Ghost" data-print-preference-reset="true">Zrušit uloženou volbu</pm-button>
<pm-button variant="Ghost" data-project-filter-preferences-reset="true">Smazat uložené projektové filtry</pm-button>
<pm-button variant="Ghost" data-record-editor-preference-reset="true">Zrušit uloženou volbu</pm-button>
```

- [ ] **Step 6: Build**

Run: `dotnet build --nologo`

Expected: `0 chyb`.

- [ ] **Step 7: Spustit aplikaci lokálně a zkontrolovat Dashboard + Profil manuálně**

Run:
```bash
dotnet run --project PmTracker.Web --no-build &
# počkat ~5s na start, otevřít v prohlížeči http://localhost:5000/
# zkontrolovat: dashboard widgets (News/Meetings/Focus) mají správně stylovaná tlačítka
# zkontrolovat: /Profil zobrazuje 3 ghost tlačítka, data-* handlery funkční
# ukončit: pkill -f "dotnet.*PmTracker.Web" nebo fg + Ctrl-C
```

Expected: Tlačítka renderovaná přes `<gov-button>`, stejný vizuální styl jako před migrací (gov-button ghost = base neutral).

**Pokud JS handler `data-dashboard-news-mark-all` nefunguje:** JS pravděpodobně hledá `<button data-dashboard-news-mark-all>`. Po migraci je to `<gov-button>` custom element — JS selektor by měl pořád fungovat (`[data-dashboard-news-mark-all]` target selektor). Pokud ne, zkontrolovat `PmTracker.Web/wwwroot/js/modules/ui.js` a opravit selektor (případně přes forwardnutí `native-type`).

- [ ] **Step 8: Commit**

```bash
git add PmTracker.Web/Views/Dashboard/ PmTracker.Web/Views/Profil/
git commit -m "$(cat <<'EOF'
refactor(views): migrovat Dashboard a Profil z .btn na pm-button

- _DashboardNewsPanel, _DashboardMeetingsPanel, _DashboardFocusPanel
- News (load-more link)
- Profil/Index (3× ghost tlačítka preference-reset)
- zachovány data-* atributy pro JS handlery

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: Migrace `.btn` v Jednani + Projekty views

**Files:**
- Modify: `PmTracker.Web/Views/Jednani/Detail.cshtml`
- Modify: `PmTracker.Web/Views/Jednani/_TaskItemPartial.cshtml`
- Modify: `PmTracker.Web/Views/Projekty/_EditZaznamBasicPanel.cshtml`
- Modify: `PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml`
- Modify: `PmTracker.Web/Views/Projekty/_EditZaznamExternalPanel.cshtml`
- Modify: `PmTracker.Web/Views/Projekty/_ProjectRecordsTab.cshtml`
- Modify: `PmTracker.Web/Views/Projekty/_ProjectTeamTab.cshtml`
- Modify: `PmTracker.Web/Views/Projekty/_ProjectProposalsTab.cshtml`
- Modify: `PmTracker.Web/Views/Projekty/_ProjectScheduleTab.cshtml`
- Modify: `PmTracker.Web/Views/Projekty/_ProjectMeetingsTab.cshtml`
- Modify: `PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml`
- Modify: `PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml`
- Modify: `PmTracker.Web/Views/Projekty/Detail.cshtml`
- Modify: `PmTracker.Web/Views/Projekty/Index.cshtml`
- Modify: `PmTracker.Web/Views/ProjectDashboard/Index.cshtml`
- Modify: `PmTracker.Web/Views/ProjectDashboard/_RecordsPanel.cshtml`
- Modify: `PmTracker.Web/Views/Osoby/Index.cshtml`
- Modify: `PmTracker.Web/Views/Nastaveni/_DetailPanel.cshtml`
- Modify: `PmTracker.Web/Views/Ciselniky/_CiselnikDetail.cshtml`

**Postup**: Po souborech, ne po třídě. Každý soubor: přečíst, provést záměny, grep kontrola, další soubor. Mapování `.btn` → pm-button variant je stejné jako v Task 3.

**Zvláštní případy:**
- Tlačítka uvnitř `<form>` s `type="submit"` → `<pm-button native-type="submit" variant="…">`.
- Tlačítka s `data-modal-url` (otevírají modal) → **neodstraňujeme** `data-modal-url`. Převádíme na `<pm-button data-modal-url="@Url.Action(...)">`.
- `.btn small ghost` → `variant="Ghost" size="Small"`.
- `.btn ghost danger` (např. `_CiselnikDetail.cshtml:97`) → `variant="Destructive"` (mapuje na solid error).

- [ ] **Step 1: Migrovat `Jednani/Detail.cshtml`** (10× `.btn`)

Read file first, then replace each occurrence. Přesné řádky podle grep:
- L47 `<a class="btn ghost" ...>` → `<pm-button variant="Ghost" href="...">`
- L59 `<button class="btn" type="submit">` → `<pm-button variant="Secondary" native-type="submit">`
- L70 `<button class="btn ghost" type="submit">Smazat</button>` → `<pm-button variant="Ghost" native-type="submit">Smazat</pm-button>`
- L86 `<button class="btn small" …>` → `<pm-button variant="Secondary" size="Small" …>`
- L92 `<button class="btn small ghost" …>` → `<pm-button variant="Ghost" size="Small" …>`
- L150 `<button class="btn small" type="submit">Uložit účast</button>` → `<pm-button variant="Secondary" size="Small" native-type="submit">Uložit účast</pm-button>`
- L189 `<button class="btn" type="submit">Uložit stav</button>` → `<pm-button variant="Secondary" native-type="submit">…</pm-button>`
- L203 `<button class="btn" type="submit">Otevřít jednání</button>` → `<pm-button variant="Secondary" native-type="submit">…</pm-button>`
- L216 `<button class="btn ghost" type="submit">Uzavřít jednání</button>` → `<pm-button variant="Ghost" native-type="submit">…</pm-button>`
- L230 `<button class="btn primary" …>` → `<pm-button variant="Primary" …>`

**Ověření**: po migraci `grep -n 'class="btn' PmTracker.Web/Views/Jednani/Detail.cshtml` nesmí mít žádný výsledek.

- [ ] **Step 2: Migrovat `_TaskItemPartial.cshtml`** (5× `.btn`)

Grep + replace all. Stejný pattern jako Step 1.

- [ ] **Step 3: Migrovat Projekty partials**

Pro každý soubor v pořadí:
- `_EditZaznamBasicPanel.cshtml` (1)
- `_EditZaznamForm.cshtml` (8)
- `_EditZaznamExternalPanel.cshtml` (3)
- `_ProjectRecordsTab.cshtml` (4)
- `_ProjectTeamTab.cshtml` (8)
- `_ProjectProposalsTab.cshtml` (10)
- `_ProjectScheduleTab.cshtml` (3)
- `_ProjectMeetingsTab.cshtml` (1) — **nesahat na šipky/render logiku, tu řeší Task 7**
- `_ZaznamCommentsPartial.cshtml` (5)
- `_ZaznamPartial.cshtml` (2)
- `Detail.cshtml` (1)
- `Index.cshtml` (3)

**Postup každého souboru:**
1. Read file
2. Pro každý výskyt `class="btn`:
   - Zjistit modifikátory (`primary`/`ghost`/`small`/`danger`)
   - Zjistit `<a>` vs `<button>`
   - Zjistit `type="submit"` pro buttony
   - Zjistit `data-*` atributy (zachovat všechny)
   - Nahradit na `<pm-button variant="…" size="…" native-type="…" data-…="…" href="…">`
3. Grep kontrola: `grep -c 'class="btn' <file>` musí vrátit 0 po migraci

- [ ] **Step 4: Migrovat `ProjectDashboard/Index.cshtml` a `_RecordsPanel.cshtml`**

Index: 1× `.btn` + `_RecordsPanel`: 5× `.btn`. Stejný postup.

- [ ] **Step 5: Migrovat `Osoby/Index.cshtml`, `Nastaveni/_DetailPanel.cshtml`, `Ciselniky/_CiselnikDetail.cshtml`**

- `Osoby/Index.cshtml` (4)
- `Nastaveni/_DetailPanel.cshtml` (12)
- `Ciselniky/_CiselnikDetail.cshtml` (4) — pozor na `.btn ghost` uvnitř delete formy

- [ ] **Step 6: Globální grep kontrola**

Run:
```bash
grep -r 'class="btn' PmTracker.Web/Views/ --include='*.cshtml' | grep -v 'StyleGuide/' | grep -v '_ModalFormActions\.cshtml'
```

Expected: **žádný výstup** (prázdný stdout). Výjimky: `_ModalFormActions.cshtml` Submit button (zachovaný) a `StyleGuide/Index.cshtml` (ukázky).

- [ ] **Step 7: Build + unit testy**

Run:
```bash
dotnet build --nologo
dotnet test --nologo --filter "FullyQualifiedName!~E2E&FullyQualifiedName!~Integration" --verbosity quiet 2>&1 | tail -5
```

Expected: build OK, 425/425 pass.

- [ ] **Step 8: Manuální smoke test v prohlížeči**

```bash
dotnet run --project PmTracker.Web --no-build &
# otevřít:
# - /Projekty → Index tlačítka
# - /Projekty/Detail/<id> → všechny záložky (Záznamy, Tým, Návrhy, Jednání, Harmonogram)
# - /Projekty/Zaznam/<id>/Edit → 4 panely formuláře + submit
# - /Jednani/Detail/<id> → všechny accordiony + submity
# - /Nastaveni/<key>/Detail
# - /Ciselniky/<key>
# - /Osoby
# ukončit běžící app
```

Expected: všechna tlačítka renderovaná, styly konzistentní s gov-button, submity, modal-url handlery fungují.

- [ ] **Step 9: Commit**

```bash
git add PmTracker.Web/Views/Jednani/ PmTracker.Web/Views/Projekty/ PmTracker.Web/Views/ProjectDashboard/ PmTracker.Web/Views/Osoby/ PmTracker.Web/Views/Nastaveni/ PmTracker.Web/Views/Ciselniky/
git commit -m "$(cat <<'EOF'
refactor(views): migrovat Jednani a Projekty views z .btn na pm-button

Přibližně 85 výskytů .btn nahrazeno pm-button wrapperem:
- Jednani/Detail (10)
- Projekty partials (~50 přes 12 souborů)
- ProjectDashboard (6)
- Osoby/Index, Nastaveni, Ciselniky (~20)

Zachovány všechny data-* atributy (modal-url, ajax-submit, …).
StyleGuide a _ModalFormActions submit button ponechány beze změny.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: Migrace `<input type="search">` na `pm-search` (selektivně)

**Files:**
- Modify: `PmTracker.Web/Views/Search/Index.cshtml`
- Modify: `PmTracker.Web/Views/Osoby/Index.cshtml`
- Modify: `PmTracker.Web/Views/Projekty/_ProjectTeamTab.cshtml`

**Kontext:** 12 výskytů `<input type="search">`. Z toho:
- **7 je person-picker** (atribut `data-person-picker-input`) — **nemigrujeme**, gov-form-input by rozbil existující floating autocomplete JS. Důvod: person-picker používá nativní `<input>` referenci, čte `.value`, poslouchá `input` event. Přesun do shadow DOM gov-form-input by vyžadoval kompletní refaktor picker modulu.
- **1 je data-collab-search** (`_EditZaznamCollaborationPanel.cshtml`) — JS handler podobný pickeru, **nemigrujeme**.
- **3-4 jsou čisté search formy** — migrujeme na `<pm-search>`:
  - `Search/Index.cshtml:51` (globální search, submit form)
  - `Osoby/Index.cshtml:30` (seznam osob, filter)
  - `Projekty/_ProjectTeamTab.cshtml:10` (filtrování týmu)

- [ ] **Step 1: Migrovat `Search/Index.cshtml`**

Read + modify. Najít řádek ~51:
```razor
<input type="search" name="q" value="@Model.Query" placeholder="Hledat…" aria-label="Hledaný výraz" autofocus />
```

Replace with:
```razor
<pm-search name="q" value="@Model.Query" placeholder="Hledat…" aria-label="Hledaný výraz" autofocus="true" submit="true" />
```

**Poznámka**: pokud je `<input>` uvnitř formy, která už má vlastní submit tlačítko, nechat `submit="false"` (default) a submit button si forma řeší separátně. Ověřit před úpravou.

- [ ] **Step 2: Migrovat `Osoby/Index.cshtml`**

Read file. Najít `<input type="search">` blok (kolem řádku 30). Zachovat všechny data-* atributy a submit button logiku.

Pokud je to filter pole s live handlerem (ne submit form), migrace bude:
```razor
<pm-search name="q" value="@Model.Query" placeholder="Jméno, příjmení…" />
```

Pokud je to GET form s submit tlačítkem oddělením:
```razor
<form method="get">
    <pm-search name="q" value="@Model.Query" submit="true" />
</form>
```

**Ověření**: otevřít v prohlížeči stránku, filtrování funguje jako dřív.

- [ ] **Step 3: Migrovat `_ProjectTeamTab.cshtml`**

Stejný pattern jako Osoby/Index.

- [ ] **Step 4: Build + unit testy**

Run:
```bash
dotnet build --nologo
dotnet test --nologo --filter "FullyQualifiedName!~E2E&FullyQualifiedName!~Integration" --verbosity quiet 2>&1 | tail -5
```

Expected: build OK, 425/425 pass.

- [ ] **Step 5: Manuální smoke test search**

```bash
dotnet run --project PmTracker.Web --no-build &
# otevřít: /Search?q=test, /Osoby, /Projekty/Detail/<id>?tab=tym
# ověřit: search input vypadá jako gov-form-search (se submit ikonou vpravo, pokud submit=true)
# zadat text, submit form, ověřit, že qs param je správně
# ukončit
```

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Views/Search/Index.cshtml PmTracker.Web/Views/Osoby/Index.cshtml PmTracker.Web/Views/Projekty/_ProjectTeamTab.cshtml
git commit -m "$(cat <<'EOF'
refactor(views): migrovat čisté search formy z input[type=search] na pm-search

Migrované:
- Search/Index (globální search, submit form)
- Osoby/Index (seznam osob)
- Projekty/_ProjectTeamTab (filter týmu)

Ponecháno beze změny (vyžaduje nativní input ref pro JS handlery):
- person-picker inputy (data-person-picker-input, 7 výskytů)
- data-collab-search (_EditZaznamCollaborationPanel)

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: Migrace `<gov-button>` v `_Layout.cshtml`

**Files:**
- Modify: `PmTracker.Web/Views/Shared/_Layout.cshtml`

**Kontext:** 2 výskyty `<gov-button>` uvnitř `<gov-form-search>` v layoutu (řádky 61, 64). Tyto jsou **slot children** uvnitř gov-form-search. Nemůžeme použít pm-button, protože gov-form-search vyžaduje konkrétní slot atributy (`slot="button-erase"`, `slot="button"`). pm-button slot neumí.

**Rozhodnutí:** **Neměnit**. Je to legitimní případ, kdy přímé gov tagy jsou správně. Dokumentovat jako akceptovaný pattern v `docs/architecture/layouts.md` (stub, pokud neexistuje) nebo v `_Layout.cshtml` komentáři.

Alternativa: pokud v budoucnu budeme mít `pm-search` s vlastní implementací erase/submit slotů, tento case vyřeší. Pro 2D **ponecháme** jako-je. V ideálním případě přepíšeme layout hledání na **`<pm-search submit="true">`** (Task 1 už to podporuje).

- [ ] **Step 1: Zkontrolovat, zda gov-form-search v layoutu může přejít na pm-search**

Read `PmTracker.Web/Views/Shared/_Layout.cshtml` (řádky 50-75 s gov-form-search blokem).

Pokud struktura je:
```razor
<form asp-controller="Search" asp-action="Index" method="get" class="global-search">
    <gov-form-search color="primary" size="m">
        <gov-form-input slot="input" size="m" name="q" placeholder="Hledání" type="search" />
        <gov-button slot="button-erase" size="s" color="primary" type="base">...</gov-button>
        <gov-button slot="button" color="primary" size="s" type="solid">Hledat</gov-button>
    </gov-form-search>
</form>
```

pak lze nahradit (pokud erase button není nutný = není v pm-search API):
```razor
<form asp-controller="Search" asp-action="Index" method="get" class="global-search">
    <pm-search name="q" placeholder="Hledání" submit="true" size="Medium" />
</form>
```

**POZOR**: erase button (clear query) je uživatelsky užitečný. Pokud pm-search erase nepodporuje (aktuálně ne), **nemigrujeme** a necháme gov-form-search přímo v layoutu.

**Rozhodnutí pro 2D**: **pokud erase-button existuje v původním layoutu**, necháme gov-form-search a přidáme komentář:
```razor
@* Globální hledání v layoutu — používá gov-form-search přímo kvůli slot="button-erase",
   který pm-search API (2D) nepodporuje. Přepsat na pm-search, až bude přidán erase support (tech debt). *@
```

Pokud erase-button v původu není (jen submit), **migrujeme** na `<pm-search submit="true">`.

- [ ] **Step 2: Provést rozhodnutí (ponechat / migrovat)**

Podle skutečného obsahu souboru: buď přidat komentář a nechat, nebo migrovat na pm-search.

- [ ] **Step 3: Build + manuální test**

```bash
dotnet build --nologo
dotnet run --project PmTracker.Web --no-build &
# otevřít libovolnou stránku, zadat query do globálního hledání, submit
# ověřit: redirect na /Search?q=…
# ukončit
```

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/Views/Shared/_Layout.cshtml
git commit -m "$(cat <<'EOF'
refactor(views): resit gov-button v layout hledani

[Podle rozhodnuti v Step 2:]
- Varianta A: migrovat na pm-search submit=true (pokud erase-button nebyl nutny)
- Varianta B: ponechat gov-form-search + pridat komentar o tech-debt
  (pm-search zatim nepodporuje slot=button-erase)

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

**Tech-debt záznam**: Pokud varianta B, vytvořit `docs/known-issues/pm-search-erase-slot.md` s TODO rozšířit pm-search o `Erasable` property.

---

## Task 7: Oprava meetings display bugs

**Files:**
- Modify: `PmTracker.Web/Views/Projekty/_ProjectMeetingsTab.cshtml`
- Modify: `PmTracker.Web/Views/Dashboard/_DashboardMeetingsPanel.cshtml`
- Create: `PmTracker.Tests.E2E/Scenarios/MeetingsDisplayTests.cs`

**Kontext z [docs/known-issues/meetings-display-bugs.md](../../known-issues/meetings-display-bugs.md):**

### Bug 1 — šipka ukazuje špatným směrem
- `state="collapsed"` → šipka dolů (rozbalit)
- `state="expanded"` → šipka nahoru (zabalit)
- Aktuálně prohozené.

### Bug 2 — globální záložka: render logika
**Očekávané:**
- Karta projektu → aktuální rok rozbalený s prvním řádkem jednání
- Vpravo horní roh karty jednání (aktuálního roku): šipka DOWN, POUZE pokud má 2+ řádky
- Vpravo horní roh karty PROJEKTU (nad aktuálním rokem): separátní šipka pro přepnutí starších let
- Starší roky: defaultně skryté; po kliknutí na horní šipku → zobrazit jako collapsed proužky

**Projektová záložka:** stejné, ale všechny roky viditelné od začátku (jako collapsed proužky).

- [ ] **Step 1: Přečíst aktuální `_ProjectMeetingsTab.cshtml`**

Run: `cat PmTracker.Web/Views/Projekty/_ProjectMeetingsTab.cshtml`

Identifikovat:
- Kde se rendruje `<gov-icon>` šipky s `data-meeting-year-state`
- JS handler přepínající state
- Logiku iterace přes roky

- [ ] **Step 2: Opravit orientaci šipky (Bug 1)**

V `_ProjectMeetingsTab.cshtml` najít `<gov-icon name="chevron-…">` v kontextu `data-meeting-year-state`. Opravit mapování:

```razor
@{
    var iconName = yearState == "collapsed" ? "chevron-down" : "chevron-up";
}
<gov-icon name="@iconName" ... />
```

(Konkrétní název z gov-icons: `chevron-up` / `chevron-down`. Pokud tam je něco jiného, analogicky.)

- [ ] **Step 3: Napsat E2E test pro Bug 1**

Create `PmTracker.Tests.E2E/Scenarios/MeetingsDisplayTests.cs`:

```csharp
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;
using PmTracker.Tests.E2E.Infrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

public class MeetingsDisplayTests : IClassFixture<PlaywrightFixture>
{
    private readonly PlaywrightFixture _fixture;

    public MeetingsDisplayTests(PlaywrightFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ProjectMeetingsTab_CollapsedYear_ShowsDownArrow()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/1?tab=jednani");

        // Najít první collapsed rok
        var collapsedYear = page.Locator("[data-meeting-year-state='collapsed']").First;
        await collapsedYear.WaitForAsync(new() { Timeout = 5000 });

        var arrow = collapsedYear.Locator("gov-icon").First;
        var iconName = await arrow.GetAttributeAsync("name");
        Assert.Equal("chevron-down", iconName);
    }

    [Fact]
    public async Task ProjectMeetingsTab_ExpandedYear_ShowsUpArrow()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/1?tab=jednani");

        var expandedYear = page.Locator("[data-meeting-year-state='expanded']").First;
        await expandedYear.WaitForAsync(new() { Timeout = 5000 });

        var arrow = expandedYear.Locator("gov-icon").First;
        var iconName = await arrow.GetAttributeAsync("name");
        Assert.Equal("chevron-up", iconName);
    }
}
```

**Pokud `PlaywrightFixture.BaseUrl` + seed data s projektem ID 1 neexistují**: použít konvenci podle ostatních E2E souborů v `PmTracker.Tests.E2E/Scenarios/`. Přečíst jeden z existujících testů (např. `StyleGuideRenderTests.cs`) a přizpůsobit.

- [ ] **Step 4: Spustit E2E (pokud je lokální SQL + Playwright dostupné)**

Run: `dotnet test --filter "FullyQualifiedName~MeetingsDisplayTests" --nologo --verbosity quiet`

Expected: 2/2 pass. Pokud fail kvůli test infrastruktuře (SQL nedostupný), přeskočit na manuální ověření.

- [ ] **Step 5: Refaktor render logiky (Bug 2) — projektová záložka**

V `_ProjectMeetingsTab.cshtml`:

```razor
@{
    var currentYear = DateTime.Now.Year;
    var yearsOrdered = Model.MeetingsByYear
        .OrderByDescending(g => g.Key)
        .ToList();
}

@foreach (var yearGroup in yearsOrdered)
{
    var isCurrentYear = yearGroup.Key == currentYear;
    var state = isCurrentYear ? "expanded" : "collapsed";
    var firstRow = yearGroup.Value.Take(ITEMS_PER_ROW).ToList();
    var hasMoreRows = yearGroup.Value.Count > ITEMS_PER_ROW;

    <div class="meeting-year-block" data-meeting-year="@yearGroup.Key" data-meeting-year-state="@state">
        <div class="meeting-year-header">
            <h3>@yearGroup.Key</h3>
            @if (isCurrentYear && hasMoreRows)
            {
                var iconName = state == "collapsed" ? "chevron-down" : "chevron-up";
                <button class="meeting-year-toggle" type="button" data-meeting-year-toggle aria-label="Přepnout zobrazení roku">
                    <gov-icon name="@iconName" type="components"></gov-icon>
                </button>
            }
        </div>

        @if (state == "expanded")
        {
            @foreach (var meeting in (hasMoreRows ? yearGroup.Value : yearGroup.Value))
            {
                @await Html.PartialAsync("_MeetingCard", meeting)
            }
        }
    </div>
}
```

**Konstanta `ITEMS_PER_ROW`** — záleží na layoutu (grid 3 karty na řádek = 3). Vytáhnout na vrch souboru:
```razor
@{
    const int ITEMS_PER_ROW = 3;
}
```

**Poznámka**: Přesný model (`Model.MeetingsByYear`, `_MeetingCard`) potřebuji ověřit proti aktuálnímu kódu. Při implementaci nejprve přečíst pořádně skutečnou strukturu souboru a model. Výše uvedený kód je **vzor**, ne doslovná šablona.

- [ ] **Step 6: Refaktor render logiky (Bug 2) — globální záložka v `_DashboardMeetingsPanel.cshtml`**

V `_DashboardMeetingsPanel.cshtml`:

```razor
@* Globální pohled: karta projektu, uvnitř aktuální rok rozbalený s 1. řádkem.
   Starší roky skryté, dostupné přes separátní horní šipku karty. *@

@foreach (var projectGroup in Model.ProjectsWithMeetings)
{
    <pm-card>
        <div class="project-card-header">
            <h3>@projectGroup.ProjectName</h3>
            @if (projectGroup.HasOlderYears)
            {
                <button type="button" class="project-older-years-toggle" data-project-older-years-toggle
                        aria-label="Zobrazit starší roky" aria-expanded="false">
                    <gov-icon name="chevron-down" type="components"></gov-icon>
                </button>
            }
        </div>

        @{
            var currentYearGroup = projectGroup.MeetingsByYear.FirstOrDefault(g => g.Year == DateTime.Now.Year);
            var olderYears = projectGroup.MeetingsByYear.Where(g => g.Year < DateTime.Now.Year).ToList();
        }

        @if (currentYearGroup != null)
        {
            var firstRowOnly = currentYearGroup.Meetings.Take(3).ToList();
            var hasMoreRows = currentYearGroup.Meetings.Count > 3;

            <div class="meeting-year-block" data-meeting-year="@currentYearGroup.Year" data-meeting-year-state="collapsed">
                <div class="meeting-year-header">
                    <h4>@currentYearGroup.Year</h4>
                    @if (hasMoreRows)
                    {
                        <button type="button" class="meeting-year-toggle" data-meeting-year-toggle aria-label="Rozbalit">
                            <gov-icon name="chevron-down" type="components"></gov-icon>
                        </button>
                    }
                </div>
                <div class="meeting-cards">
                    @foreach (var meeting in firstRowOnly)
                    {
                        @await Html.PartialAsync("_MeetingCard", meeting)
                    }
                </div>
            </div>
        }

        <div class="older-years-container" data-older-years-container style="display: none;">
            @foreach (var olderYear in olderYears)
            {
                <div class="meeting-year-block" data-meeting-year="@olderYear.Year" data-meeting-year-state="collapsed">
                    <div class="meeting-year-header">
                        <h4>@olderYear.Year</h4>
                        <button type="button" class="meeting-year-toggle" data-meeting-year-toggle aria-label="Rozbalit">
                            <gov-icon name="chevron-down" type="components"></gov-icon>
                        </button>
                    </div>
                </div>
            }
        </div>
    </pm-card>
}
```

**POZOR**: Tento kód předpokládá datový model `ProjectsWithMeetings` s `HasOlderYears` a `MeetingsByYear` — **reálný model v kódu je jiný**. Před implementací nejdřív přečíst aktuální ViewModel (hledat v `PmTracker.Web/Models/` nebo `PmTracker.Application/`) a přizpůsobit. Pokud model nemá tyto property, **buď je přidat** (pokud to dává doménový smysl), **nebo** je dopočítat v view pomocí `.GroupBy(m => m.Year)` atd.

- [ ] **Step 7: Doplnit / upravit JS handlery**

Najít v `PmTracker.Web/wwwroot/js/modules/ui.js` (nebo podobně) handler pro `data-meeting-year-toggle`. Ověřit, že:
- Přepíná `data-meeting-year-state` mezi `"collapsed"` / `"expanded"`
- Přepíná `gov-icon name` mezi `chevron-down` / `chevron-up`
- Pokud řeší `data-project-older-years-toggle`, přepíná `display: none` na `.older-years-container`

Pokud handler pro `data-project-older-years-toggle` neexistuje, dodat:

```javascript
// V ui.js
document.querySelectorAll('[data-project-older-years-toggle]').forEach(btn => {
    btn.addEventListener('click', () => {
        const card = btn.closest('pm-card, gov-card');
        const container = card?.querySelector('[data-older-years-container]');
        if (!container) return;

        const isHidden = container.style.display === 'none' || !container.style.display;
        container.style.display = isHidden ? '' : 'none';
        btn.setAttribute('aria-expanded', isHidden ? 'true' : 'false');

        const icon = btn.querySelector('gov-icon');
        if (icon) icon.setAttribute('name', isHidden ? 'chevron-up' : 'chevron-down');
    });
});
```

- [ ] **Step 8: Build + manuální test**

```bash
dotnet build --nologo
dotnet run --project PmTracker.Web --no-build &
# otevřít:
# /Projekty/Detail/<id>?tab=jednani → ověřit:
#   - aktuální rok rozbalený, ostatní roky jako collapsed proužky
#   - šipka u aktuálního roku míří dolů (pokud 2+ řádky), po kliku nahoru
#   - kliknutí na collapsed rok jej rozbalí
#
# /Dashboard → ověřit:
#   - projekty jako karty
#   - aktuální rok rozbalený s 1. řádkem jednání
#   - pokud 2+ řádky: šipka dolů u aktuálního roku
#   - starší roky defaultně skryté
#   - horní šipka na kartě projektu → zobrazí collapsed starší roky
# ukončit
```

- [ ] **Step 9: Commit**

```bash
git add PmTracker.Web/Views/Projekty/_ProjectMeetingsTab.cshtml PmTracker.Web/Views/Dashboard/_DashboardMeetingsPanel.cshtml PmTracker.Web/wwwroot/js/modules/ui.js PmTracker.Tests.E2E/Scenarios/MeetingsDisplayTests.cs
git commit -m "$(cat <<'EOF'
fix(meetings): oprava zobrazeni seznamu jednani (Bug 1 + Bug 2)

Bug 1: chevron u meeting-year-state byl obraceně
 - collapsed → chevron-down (rozbalit)
 - expanded → chevron-up (zabalit)

Bug 2: globalni založka renderovala vsechny roky namisto jen aktualniho
 - aktualni rok rozbaleny s 1. radkem (3 karet)
 - sipka u aktualniho roku jen pokud 2+ radky
 - starsi roky skryte, prepinani pres horni sipku karty projektu
 - projektova zalozka: vsechny roky viditelne (collapsed) od startu

Doplnen JS handler data-project-older-years-toggle.
Pridan E2E test MeetingsDisplayTests (2 testy).

Zdokumentovano v docs/known-issues/meetings-display-bugs.md (fixed).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 10: Označit known-issue jako vyřešené**

Modify `docs/known-issues/meetings-display-bugs.md`:

Přidat na začátek:
```markdown
> **Status:** ✅ VYŘEŠENO ve Fázi 2D (commit z Task 7, 2026-04-19).
> Popis zachován jako historický záznam.
```

Commit:
```bash
git add docs/known-issues/meetings-display-bugs.md
git commit -m "docs(known-issues): oznacit meetings-display-bugs jako vyreseno ve 2D

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 8: E2E smoke test + finální audit + dokumentace 2E

**Files:**
- Create: `PmTracker.Tests.E2E/Scenarios/PhaseD_ViewsMigrationTests.cs`
- Create: `docs/known-issues/modal-migration-to-pm-dialog.md`
- Modify: `docs/superpowers/plans/2026-04-19-faze-2d-migrace-views.md` (přidat závěr)

- [ ] **Step 1: E2E smoke test pro migrované views**

Create `PmTracker.Tests.E2E/Scenarios/PhaseD_ViewsMigrationTests.cs`:

```csharp
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;
using PmTracker.Tests.E2E.Infrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

public class PhaseD_ViewsMigrationTests : IClassFixture<PlaywrightFixture>
{
    private readonly PlaywrightFixture _fixture;

    public PhaseD_ViewsMigrationTests(PlaywrightFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Dashboard_RendersGovButtonsNotLegacyBtnClass()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/");

        // Žádná tlačítka s class="btn" (mimo _ModalFormActions submit)
        var legacyCount = await page.Locator(".btn:not([type='submit'])").CountAsync();
        Assert.Equal(0, legacyCount);

        // Alespoň jedno gov-button (z pm-button wrapperu)
        var govCount = await page.Locator("gov-button").CountAsync();
        Assert.True(govCount > 0, "Očekáváno alespoň jedno gov-button v Dashboardu");
    }

    [Fact]
    public async Task SearchIndex_RendersGovFormSearch()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Search");

        var govSearch = page.Locator("gov-form-search").First;
        await govSearch.WaitForAsync(new() { Timeout = 5000 });

        var govInput = govSearch.Locator("gov-form-input[slot='input']");
        Assert.Equal(1, await govInput.CountAsync());
    }

    [Fact]
    public async Task ProjectDetail_AllTabsRenderGovButtons()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/1");

        // Pro každou záložku (zaznamy, tym, navrhy, jednani, harmonogram)
        foreach (var tab in new[] { "zaznamy", "tym", "navrhy", "jednani", "harmonogram" })
        {
            await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/1?tab={tab}");
            var govButtons = await page.Locator("gov-button").CountAsync();
            Assert.True(govButtons > 0, $"Tab {tab} očekává alespoň jedno gov-button");
        }
    }
}
```

- [ ] **Step 2: Spustit E2E (pokud je dostupné)**

Run: `dotnet test --filter "FullyQualifiedName~PhaseD_ViewsMigrationTests" --nologo --verbosity quiet`

Expected: 3/3 pass (pokud lokální SQL + Playwright OK). Jinak manuální ověření.

- [ ] **Step 3: Závěrečný globální audit**

Run:
```bash
echo "=== .btn v produkčních Views ===" && grep -rc 'class="btn' PmTracker.Web/Views/ --include='*.cshtml' | grep -v ':0$' | grep -v StyleGuide | grep -v _ModalFormActions
echo "=== input type=search v produkčních Views ===" && grep -rc '<input.*type="search"' PmTracker.Web/Views/ --include='*.cshtml' | grep -v ':0$'
echo "=== gov-button v produkčních Views (mimo Layout gov-form-search slot) ===" && grep -rn '<gov-button' PmTracker.Web/Views/ --include='*.cshtml' | grep -v StyleGuide | grep -v _Layout
```

Expected:
- `.btn`: prázdný výstup nebo pouze `_ModalFormActions.cshtml` (submit) a `StyleGuide`
- `input[type=search]`: pouze person-picker výskyty (ty nemigrujeme)
- `gov-button` mimo StyleGuide/Layout: prázdný výstup

- [ ] **Step 4: Vytvořit known-issue pro 2E**

Create `docs/known-issues/modal-migration-to-pm-dialog.md`:

```markdown
# Migrace modálního systému na pm-dialog (2E scope)

**Status:** Odloženo na Fázi 2E (po 2D). Dokumentováno 2026-04-19.

## Kontext

PM Tracker používá vlastní AJAX-based modální systém:

- `Views/Shared/_ModalLayout.cshtml` — overlay + container + floating-root
- `wwwroot/js/modules/modals.js` — event delegace, fetch modálního obsahu přes `data-modal-url`
- Atributy: `data-modal-url`, `data-modal-close`, `data-modal-container`, `data-ajax-submit`
- 33 views obsahuje `modal-` CSS třídy / data atributy

## Proč nebyla migrace v 2D

`pm-dialog` wrapper (Fáze 2C) je **thin wrapper nad `<gov-dialog>`** s API:
- `.show()` / `.close()` instance methods na custom elementu
- `title` slot
- default slot pro body

Migrace vyžaduje:
1. Přepsat `modals.js` aby pracoval s `gov-dialog.show()`/`close()` místo manipulace s `.modal-overlay` CSS třídami
2. Vyřešit AJAX fetch flow: `data-modal-url` → fetch HTML → vložit do gov-dialog body slot → call `.show()`
3. Refaktor `data-ajax-submit` forms uvnitř gov-dialog — validace, submit, error handling musí pracovat uvnitř shadow DOM gov-dialog
4. `modal-floating-root` (kontejner pro floating pickers nad modálem) — `gov-dialog` má vlastní z-index/positioning, floating-root musí být přemapovaný nebo nahrazený
5. Varianty modálu (`modal--wide`, `modal--record-editor`, `modal--overflow-visible`) — musí být převedené na gov-dialog size/variant atributy nebo řešené přes CSS scope

## Postupná migrace (doporučený plán pro 2E)

**Task 1:** Vytvořit `pm-modal` wrapper (ne `pm-dialog` — ten už existuje) se stejným API jako stávající `modal-overlay` ale internally používající `gov-dialog`. Adapter vrstva.

**Task 2:** Refaktor `modals.js` použít nové API, zachovat `data-modal-url` / `data-ajax-submit` kontrakty.

**Task 3:** Postupně migrovat 33 views z `Layout = "_ModalLayout"` na `Layout = "_PmModalLayout"` (nový). Po jednom view, smoke test každého.

**Task 4:** Odstranit starý `modal-*` CSS po dokončení.

## Zasažené views (33)

- Shared: `_ModalLayout`, `_ModalFormActions`
- Projekty: všechny `*Modal.cshtml` (8 souborů)
- Nastaveni: `*Modal.cshtml` (4 soubory)
- Osoby: `AdPersonModal`, `ManualPersonModal`
- Ciselniky: `EditRow`
- Jednani: `AddMeetingParticipantModal`
- a všechny partials, které mají `data-modal-close` (řeší se v adaptéru)
```

- [ ] **Step 5: Aktualizovat plán s výsledky 2D**

Modify `docs/superpowers/plans/2026-04-19-faze-2d-migrace-views.md`:

Přidat na konec:

```markdown
---

## Výsledky Fáze 2D

**Dokončeno:** 2026-04-19 (doplnit skutečný datum)

### Metriky před / po

| Metrika | Před 2D | Po 2D |
|---|---|---|
| `.btn` v produkčních Views | 103 | 0 (mimo _ModalFormActions submit) |
| `<input type="search">` | 12 | 7 (jen person-picker) |
| `pm-*` TagHelpery | 21 | 22 (+pm-search) |
| Unit testy (Web.Tests + Tests.Unit) | 417 | 425+ |
| E2E smoke scenarios | 3 | 6 (+MeetingsDisplay, +PhaseD_ViewsMigration) |

### Opraveno

- **Bug: meetings šipka obráceně** — Task 7 Step 2
- **Bug: globální záložka render logika** — Task 7 Step 6

### Odloženo na 2E

- Modální systém (`modal-overlay` → `pm-dialog`): 33 views — [docs/known-issues/modal-migration-to-pm-dialog.md](../../known-issues/modal-migration-to-pm-dialog.md)

### Tech debt pro 2E+

- `_ModalFormActions.SubmitCssClass` — migrovat spolu s modály
- Layout globální hledání `slot="button-erase"` — pm-search nepodporuje erase (volitelně rozšířit)
- DRY label/help/error pattern (PmFieldTagHelper/PmSelectTagHelper/PmTextareaTagHelper) — fáze 3+
- Rename `PmSkeletonShape.Default` → `Rectangle`, `PmTabsType.Default` → `Underline` (breaking change)
```

- [ ] **Step 6: Final build + all unit testy**

Run:
```bash
dotnet build --nologo
dotnet test --nologo --filter "FullyQualifiedName!~E2E&FullyQualifiedName!~Integration" --verbosity quiet 2>&1 | tail -5
```

Expected: build OK, všechny unit testy pass (nemělo by to být méně než 425).

- [ ] **Step 7: Final commit**

```bash
git add PmTracker.Tests.E2E/Scenarios/PhaseD_ViewsMigrationTests.cs docs/known-issues/modal-migration-to-pm-dialog.md docs/superpowers/plans/2026-04-19-faze-2d-migrace-views.md
git commit -m "$(cat <<'EOF'
chore(2d): zaverecny audit, E2E smoke a dokumentace 2E scope

- PhaseD_ViewsMigrationTests: 3 E2E smoke testy (Dashboard, Search, ProjectDetail tabs)
- docs/known-issues/modal-migration-to-pm-dialog.md — blocker pro 2E
- Aktualizovana metrika a vysledky ve 2D planu

Faze 2D dokoncena. Faze 2E: migrace modalneho systemu.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Audit stávajícího stavu

Pro referenci: baseline před startem Fáze 2D (výsledky grepu 2026-04-19).

**Rozložení `.btn` po souborech (27 souborů, 103 výskytů):**

```
PmTracker.Web/Views/Jednani/Detail.cshtml: 10
PmTracker.Web/Views/Nastaveni/_DetailPanel.cshtml: 12
PmTracker.Web/Views/Projekty/_ProjectProposalsTab.cshtml: 10
PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml: 8
PmTracker.Web/Views/Projekty/_ProjectTeamTab.cshtml: 8
PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml: 5
PmTracker.Web/Views/Jednani/_TaskItemPartial.cshtml: 5
PmTracker.Web/Views/Projekty/_ProjectRecordsTab.cshtml: 4
PmTracker.Web/Views/Osoby/Index.cshtml: 4
PmTracker.Web/Views/Ciselniky/_CiselnikDetail.cshtml: 4
PmTracker.Web/Views/Profil/Index.cshtml: 3
PmTracker.Web/Views/Projekty/Index.cshtml: 3
PmTracker.Web/Views/Projekty/_ProjectScheduleTab.cshtml: 3
PmTracker.Web/Views/Projekty/_EditZaznamExternalPanel.cshtml: 3
PmTracker.Web/Views/ProjectDashboard/_RecordsPanel.cshtml: 5
PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml: 2
PmTracker.Web/Views/Shared/_PageHeader.cshtml: 2
PmTracker.Web/Views/Dashboard/_DashboardNewsPanel.cshtml: 2
PmTracker.Web/Views/Osoby/AdPersonModal.cshtml: 2
PmTracker.Web/Views/Dashboard/_DashboardMeetingsPanel.cshtml: 1
PmTracker.Web/Views/Dashboard/News.cshtml: 1
PmTracker.Web/Views/Dashboard/_DashboardFocusPanel.cshtml: 1
PmTracker.Web/Views/ProjectDashboard/Index.cshtml: 1
PmTracker.Web/Views/Projekty/_EditZaznamBasicPanel.cshtml: 1
PmTracker.Web/Views/Projekty/Detail.cshtml: 1
PmTracker.Web/Views/Projekty/_ProjectMeetingsTab.cshtml: 1
PmTracker.Web/Views/Shared/_ModalFormActions.cshtml: 1 (ponecháno — Submit button)
```

**`<input type="search">` po souborech (12):**

- Migrujeme (3): `Search/Index.cshtml`, `Osoby/Index.cshtml`, `Projekty/_ProjectTeamTab.cshtml`
- Ponecháme (9 — person-picker + collab-search): `Ciselniky/_CiselnikDetail.cshtml`, `Ciselniky/EditRow.cshtml`, `Jednani/AddMeetingParticipantModal.cshtml`, `Osoby/AdPersonModal.cshtml`, `Projekty/_EditZaznamBasicPanel.cshtml`, `Projekty/AssignProjectSubsystemRoleModal.cshtml`, `Projekty/AddTeamMemberModal.cshtml`, `Projekty/AssignProjectRoleModal.cshtml`, `Projekty/_EditZaznamCollaborationPanel.cshtml`

---

## Self-Review výsledky

**Spec coverage:** všechny požadavky (migrace `.btn`, migrace search, meetings bugy, dokumentace 2E) mají úkol. ✅

**Placeholder scan:** Task 6 má variantu "A/B" rozhodnutí (podle skutečného obsahu layoutu). Není to placeholder — je to podmíněná implementace na fakt, který si implementer ověří v Step 1. Akceptovatelné, ale označit rozhodnutí jako Step 2. Task 7 Step 5-6 má "před implementací ověřit reálný ViewModel" — také není placeholder, je to nutná kontrola cesty.

**Type consistency:** `PmSearchTagHelper` property `Submit` (bool, Task 1) konzistentně použitá v Task 5 jako `submit="true"` (Razor camelCase konvence). `variant="Secondary"` / `variant="Primary"` / `variant="Ghost"` / `variant="Destructive"` konzistentně mapované po celém plánu. `size="Small"/"Medium"/"Large"` (PmComponentSize enum) též konzistentní.

Plán vypadá v pořádku.
