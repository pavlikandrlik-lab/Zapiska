# C3 — Schvalování návrhu: rezim switch read-only (implementační plán)

> **For agentic workers:** Inline exekuce (executing-plans) v hlavní session — bez subagentů (pravidlo uživatele). Kroky checkbox syntaxí.

**Goal:** Na detailu schvalování návrhu je switch „Automatické vyplňování harmonogramu" disabled; sweep potvrdil, že je to jediný editovatelný prvek mimo whitelist.

**Architecture:** Server-side `disabled` atribut podle `Model.IsProposalDecisionDetail` (stejný vzor jako B4 u collab/external panelů) + defensivní guard v JS modulu. Živý sweep (2026-07-10, proposal id 6) našel právě 1 prohřešek: `gov-form-switch[data-record-rezim-switch]` + jeho vnitřní nativní checkbox.

**Tech Stack:** Razor, gov-design-system 4.x (Stencil), xUnit + FluentAssertions, Playwright.

## Global Constraints

- Commity DRŽET — uživatel finálně ověřuje ručně (standing rule).
- Kvalita > zkratky; fix at source (sdílený partial `_EditZaznamForm.cshtml`).
- Razor komentáře NAD tag, nikdy mezi atributy (spolkly by následující atribut).
- Dev app: port 5071, `?asUser=1`; decision stránka: `/Navrhy/ProposalDetail?projektId=1&proposalId=6&asUser=1`.
- Whitelist editovatelných prvků na decision stránce: globální hledání + user menu (app chrome), tlačítka Schválit / Zamítnout / Zamítnout a převzít data / Zrušit a vrátit se, přepínání záložek.

---

### Task 1: Api render test (RED) + disabled atribut (GREEN)

**Files:**
- Modify: `PmTracker.Tests.Api/Controllers/ProposalDecisionReadonlyRenderTests.cs`
- Modify: `PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml` (řádky ~157–164)

**Interfaces:**
- Consumes: `ApiSqlFixture.EnsurePendingCreateProposalAsync(...)` (existuje z B-batche), `Model.IsProposalDecisionDetail`.
- Produces: markup `<gov-form-switch … data-record-rezim-switch … disabled>` na decision stránce.

- [ ] **Step 1: Failing testy do ProposalDecisionReadonlyRenderTests**

```csharp
[Fact]
public async Task ProposalDetail_RezimSwitch_IsDisabled()
{
    var html = await GetProposalDetailHtmlAsync(); // stejný helper/flow jako existující fakta třídy
    Regex.IsMatch(html, "<gov-form-switch[^>]*data-record-rezim-switch[^>]*disabled")
        .Should().BeTrue("C3: na schvalování je vše read-only, switch nesmí být editovatelný");
}

[Fact]
public async Task CreateProposalEditor_RezimSwitch_StaysEditable()
{
    var html = await GetCreateProposalEditorHtmlAsync(); // editor návrhu (guard-on stránka)
    var switchTag = Regex.Match(html, "<gov-form-switch[^>]*data-record-rezim-switch[^>]*>");
    switchTag.Success.Should().BeTrue("switch má být v editoru přítomný");
    switchTag.Value.Should().NotContain("disabled", "v editoru návrhu zůstává switch editovatelný");
}
```

Pozn.: pokud switch na create-proposal editoru není renderován (podmínka
`!HarmonogramBlok.HideActual`), druhý test upravit na negativní kontrolu
přes editor záznamu (`/Zaznamy/Edit/401`) — rozhodne skutečný render.

- [ ] **Step 2: Spustit — musí FAILNOUT na prvním faktu** (`disabled` chybí)

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ProposalDecisionReadonly"`

- [ ] **Step 3: Minimální implementace — disabled atribut**

V `_EditZaznamForm.cshtml` (komentář NAD tag, `disabled` mezi data atributy a `checked`):

```razor
@* C3 (2026-07-10): na schvalování návrhu (IsProposalDecisionDetail) je switch
   read-only — pravidlo „vše read-only, žádný editovatelný prvek". Stav zůstává
   viditelný (schvalovatel má vidět Auto/Manual režim). *@
<gov-form-switch size="m"
                 class="record-editor-tabs-rezim-switch"
                 data-record-rezim-switch
                 data-record-zaznam-id="@Model.Id"
                 data-record-projekt-id="@Model.ProjektId"
                 disabled="@(Model.IsProposalDecisionDetail ? "disabled" : null)"
                 @(Model.HarmonogramAutoFillSwitchOn ? "checked" : null)>
```

- [ ] **Step 4: Testy zelené** (stejný filtr, celá třída musí projít)

### Task 2: Defensivní guard v JS + unit pin

**Files:**
- Modify: `PmTracker.Web/wwwroot/js/modules/harmonogram/rezim-master-switch.js`
- Modify: `PmTracker.Tests.Unit/Layout/RecordEditorHistoryTrapTests.cs` (nebo nový `RezimSwitchGuardTests` tamtéž ve stylu source-pin testů)

- [ ] **Step 1: Failing source-pin test**

```csharp
[Fact]
public void RezimMasterSwitch_SkipsWiring_WhenHostDisabled()
{
    var js = File.ReadAllText(Path.Combine(RepoRoot(), "PmTracker.Web/wwwroot/js/modules/harmonogram/rezim-master-switch.js"));
    js.Should().Contain("hasAttribute(\"disabled\")",
        "C3: disabled host se nesmí zapojit — gov event by jinak mohl přepnout hidden input");
}
```

- [ ] **Step 2: Přečíst `rezim-master-switch.js` init a doplnit early-return guard**

Do funkce, která hledá `[data-record-rezim-switch]` a věší listener (přesné
místo podle čtení souboru — modul je malý):

```javascript
if (host.hasAttribute("disabled")) {
    return; // C3: read-only stránka (schvalování návrhu) — switch se nezapojuje
}
```

- [ ] **Step 3: Unit test zelený + `dotnet build` čistý**

### Task 3: E2E sweep regrese + živé ověření

**Files:**
- Modify: `PmTracker.Tests.E2E/Scenarios/ProposalDecisionScenariosTests.cs`

- [ ] **Step 1: Nový E2E fakt — sweep decision stránky**

```csharp
[Fact]
public async Task ProposalDecisionPage_HasNoEditableControls_OutsideWhitelist()
{
    var page = await Fixture.NewPageAsync();
    await page.GotoAsync($"{Fixture.BaseUrl}/Navrhy/ProposalDetail?projektId={Fixture.ProjectId}&proposalId={ProposalId}&asUser={Fixture.AdminOsobaId}");
    // rezim switch: host má disabled a klik nepřepne checked
    var sw = page.Locator("gov-form-switch[data-record-rezim-switch]");
    await Assertions.Expect(sw).ToHaveCountAsync(1);
    await Assertions.Expect(sw).ToHaveAttributeAsync("disabled", new Regex(".*"));
    var before = await sw.GetAttributeAsync("checked");
    await sw.ClickAsync(new() { Force = true });
    (await sw.GetAttributeAsync("checked")).Should().Be(before);
    // sweep: žádný enabled form control mimo whitelist (name=q je globální hledání)
    var offenders = await page.EvaluateAsync<string[]>(
        @"() => [...document.querySelectorAll('input:not([type=hidden]):not([disabled]):not([readonly]), select:not([disabled]), textarea:not([disabled])')]
            .filter(el => el.getBoundingClientRect().height > 0 && !el.closest('[hidden]'))
            .filter(el => el.name !== 'q' && !el.classList.contains('gov-form-switch__input') || !el.closest('gov-form-switch[disabled]'))
            .map(el => el.name || el.id || el.className)");
    offenders.Should().BeEmpty("na schvalování nesmí být editovatelný žádný datový prvek");
}
```

(Vnitřní `gov-form-switch__input` checkbox: pokud gov komponenta
nepropaguje `disabled` na nativní input, filtr přes
`closest('gov-form-switch[disabled]')` ho vyloučí — přepnutí stejně
blokuje host disabled + JS guard; ověří klik-assert výše.)

- [ ] **Step 2: E2E třída zelená** (`dotnet test PmTracker.Tests.E2E --filter "FullyQualifiedName~ProposalDecision"`)

- [ ] **Step 3: Živý Playwright re-sweep (scratchpad/c3-decision-sweep.js) — očekávaný výstup: rezim switch už NENÍ v editovatelných; počet editovatelných mimo chrome+akce = 0**

- [ ] **Step 4: Regrese — Api ProposalDecisionReadonly + guard-on editor E2E (RecordEditorHistoryBackScenariosTests) beze změny chování**

### Task 4: Držený commit (až po ručním ověření uživatelem)

- [ ] Připravit `git add` seznam; commit message: `fix(navrhy): C3 — rezim switch read-only na schvalování + JS guard`
