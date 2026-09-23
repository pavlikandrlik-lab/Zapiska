# A7 — Křížek modalu: odstranit block-close — Implementation Plan

> **For agentic workers:** Exekuce INLINE v hlavní session (user pravidlo). **Commity DRŽET.**

**Goal:** Křížek (X) zavírá všechny gov-dialog modaly; backdrop-close zůstává zakázaný; Escape zavírá (schváleno userem).

**Architecture:** Aktualizovaná gov verze renderuje X jako `disabled: this.blockClose` → náš plošný `block-close="true"` v `_ModalLayout` X umrtvil všude. Odebrat `block-close` (X pak emituje `gov-close` + gov sám `hideDialog()`); náš `handleGovCloseEvent` → `closeModal()` dál dělá aplikační úklid (modal-root, floating-root, focus) a je vůči už-skrytému dialogu idempotentní (ověřeno čtením `closeModal`: defensivní `removeAttribute("open")` + try/catch `close()`). `block-backdrop-close="true"` zůstává.

**Tech Stack:** Razor, JS, xUnit, Playwright E2E.

## Global Constraints
- Backdrop klik NESMÍ zavírat modal (memory pravidlo, drag-select ochrana).
- Escape smí zavírat (user potvrdil).
- Commity držené.

---

### Task 1: Source-assertion testy + odebrání atributu

**Files:**
- Test: `PmTracker.Tests.Unit/Modals/ModalBlockCloseTests.cs` (create)
- Modify: `PmTracker.Web/Views/Shared/_ModalLayout.cshtml` (řádek `block-close="true"` v `<gov-dialog>`)
- Modify: `PmTracker.Web/wwwroot/js/modules/modals.js:237` (error-dialog šablona)

**Interfaces:**
- Consumes: `handleGovCloseEvent` (bootstrap.js:646) — beze změny; `closeModal()` (modals.js:186) — beze změny.
- Produces: gov-dialog bez `block-close` → X enabled, klik → `gov-close` event + gov self-close.

- [ ] **Step 1: Failing test**

```csharp
using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Modals;

/// <summary>
/// A7 (2026-07-08): gov-design-system nově renderuje X jako disabled=blockClose →
/// block-close="true" umrtvil křížek ve VŠECH modalech. Atribut nesmí existovat;
/// block-backdrop-close (žádný backdrop-close, drag-select ochrana) zůstává povinný.
/// </summary>
public sealed class ModalBlockCloseTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }
    private static string Read(string rel) => File.ReadAllText(Path.Combine(RepoRoot(), rel));

    [Theory]
    [InlineData("PmTracker.Web/Views/Shared/_ModalLayout.cshtml")]
    [InlineData("PmTracker.Web/wwwroot/js/modules/modals.js")]
    public void GovDialog_HasNoBlockClose_ButKeepsBackdropBlock(string rel)
    {
        var src = Read(rel);
        src.Should().NotContain("block-close=\"true\"",
            "block-close disabluje X v nové gov verzi (disabled: this.blockClose)");
        src.Should().Contain("block-backdrop-close=\"true\"",
            "backdrop-close zůstává zakázaný (modal se zavírá jen X/Escape)");
    }
}
```

- [ ] **Step 2: Run — expect FAIL** (`--filter "FullyQualifiedName~ModalBlockCloseTests"`; oba soubory dnes `block-close="true"` obsahují)

- [ ] **Step 3: _ModalLayout.cshtml** — z `<gov-dialog open="true" block-close="true" block-backdrop-close="true" ...>` smazat `block-close="true"` a aktualizovat komentář nad tím (ř. ~35): nahradit větu o block-close textem:

```
    - block-close se NEPOUŽÍVÁ (A7 2026-07-08): nová gov verze renderuje X jako
      disabled=blockClose → X by byl mrtvý. X → gov-close event + gov self-close;
      handleGovCloseEvent (bootstrap) dodělá aplikační úklid přes closeModal().
      block-backdrop-close="true" zůstává — backdrop klik nesmí zavírat (drag-select).
```

- [ ] **Step 4: modals.js:237** — v error šabloně smazat `block-close="true"` (ponechat `block-backdrop-close="true"`).

- [ ] **Step 5: Run — expect PASS**; poté `dotnet build PmTracker.Web -c Debug -v q` (0 chyb) + restart app.

### Task 2: E2E scénář (X zavírá, backdrop ne, žádné JS chyby)

**Files:**
- Test: `PmTracker.Tests.E2E/Scenarios/ModalCloseXScenariosTests.cs` (create)

**Interfaces:**
- Consumes: `E2ETestFixture` (`ProjectId`, `AdminOsobaId`, `BaseUrl`, `NewPageAsync`); modal „Přidat projektovou roli" (`[data-modal-url*='AssignProjectRoleModal']` na tabu tym); gov-dialog X = `.gov-dialog__close` (light DOM, gov je scoped — Playwright na něj klikne přímo).

- [ ] **Step 1: Test**

```csharp
using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// A7 (2026-07-08): X zavírá modal (block-close odstraněn), backdrop klik NEzavírá,
/// žádná JS chyba z konvergence gov self-close + closeModal().
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class ModalCloseXScenariosTests
{
    private readonly E2ETestFixture _fixture;
    public ModalCloseXScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private static ILocatorAssertions Expect(ILocator l) => Assertions.Expect(l);

    [Fact]
    public async Task CloseX_ClosesModal_BackdropDoesNot()
    {
        var page = await _fixture.NewPageAsync();
        var jsErrors = new List<string>();
        page.PageError += (_, e) => jsErrors.Add(e);

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=tym&asUser={_fixture.AdminOsobaId}");
        await page.Locator("[data-modal-url*='AssignProjectRoleModal']").First.ClickAsync();
        await Expect(page.Locator("gov-dialog[data-modal-container]")).ToHaveCountAsync(1);

        // Backdrop klik (roh stránky mimo dialog) NEzavírá.
        await page.Mouse.ClickAsync(5, 5);
        await page.WaitForTimeoutAsync(300);
        await Expect(page.Locator("gov-dialog[data-modal-container]")).ToHaveCountAsync(1);

        // X zavírá.
        await page.Locator("gov-dialog .gov-dialog__close").ClickAsync();
        await Expect(page.Locator("gov-dialog[data-modal-container]")).ToHaveCountAsync(0);

        jsErrors.Should().BeEmpty("dvojitá close cesta (gov self-close + closeModal) nesmí házet");
        await page.Context.CloseAsync();
    }
}
```

- [ ] **Step 2: Run** `dotnet test PmTracker.Tests.E2E --filter "FullyQualifiedName~ModalCloseXScenariosTests" -v q --nologo` → PASS. Pokud X klik selže na „element intercepted", je to nový nález → systematic-debugging (NEobcházet force-click).

- [ ] **Step 3: Ruční sanity dalších modalů** (Playwright skript, ne durable): Nové jednání, Přidat osobu (jednání detail), AD search — X zavírá, backdrop ne, floating-root vrácený (`#floating-panel-root` je child `<body>`). Výstup do reportu.
