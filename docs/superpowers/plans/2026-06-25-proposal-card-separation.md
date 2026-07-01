# Vizuální oddělení návrhů — sub-karty Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Přidat CSS definice pro `.proposal-section-grid`, `.proposal-list`, `.proposal-card`, `.proposal-card-header` a `.proposal-card-actions` — třídy existují v HTML, ale nemají žádné CSS, takže se návrhy vizuálně slévají.

**Architecture:** Čistě CSS změna v `site.css` (5 nových pravidel). Razor view `_ProjectProposalsTab.cshtml` se nemění — HTML markup s těmito třídami tam už je. Guard test ověří přítomnost klíčových CSS selektorů.

**Tech Stack:** CSS, xUnit + FluentAssertions (guard test)

## Global Constraints

- Žádné změny v Razor views — HTML třídy už existují
- Dark mode přes gov tokeny (`var(--gov-color-border)`, `var(--gov-radius)`) — žádné explicitní `:root[data-theme="dark"]` pravidla
- CSS vkládat za blok `.record-summary-meta` (řádek ~3592 v site.css), kde žijí příbuzné record-* třídy
- Vzor guard testu: `ScheduleMarkerCssTests.cs` / `NavrhyRedesignTests.cs` (file-text assertion nad site.css)

---

### Task 1: Guard test + CSS implementace sub-karet

**Files:**
- Create: `PmTracker.Tests.Unit/Projects/ProposalCardCssTests.cs`
- Modify: `PmTracker.Web/wwwroot/css/site.css:~3593` (za `.record-summary-meta`)

**Interfaces:**
- Consumes: nic (čistě nový CSS + nový test soubor)
- Produces: CSS pravidla pro `.proposal-card`, `.proposal-list`, `.proposal-section-grid`, `.proposal-card-header`, `.proposal-card-actions`

- [ ] **Step 1: Write the failing test**

Soubor `PmTracker.Tests.Unit/Projects/ProposalCardCssTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Projects;

public sealed class ProposalCardCssTests
{
    private static string Css()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return File.ReadAllText(
            Path.Combine(dir!.FullName, "PmTracker.Web", "wwwroot", "css", "site.css"));
    }

    [Fact]
    public void ProposalCard_HasBorderAndPadding()
    {
        var css = Css();
        css.Should().Contain(".proposal-card {",
            "každý návrh potřebuje vlastní sub-kartu s orámováním");
        css.Should().Contain("var(--gov-color-border)",
            "border barva musí používat gov token pro dark-mode kompatibilitu");
    }

    [Fact]
    public void ProposalList_HasGap()
    {
        Css().Should().Contain(".proposal-list {",
            "kontejner návrhů potřebuje gap mezi sub-kartami");
    }

    [Fact]
    public void ProposalCardHeader_IsFlexRow()
    {
        var css = Css();
        css.Should().Contain(".proposal-card-header {",
            "header návrhu (název + badge) musí být flex row");
        css.Should().Contain("justify-content: space-between",
            "badge stavu zarovnaný vpravo");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PmTracker.Tests.Unit --filter "ProposalCardCssTests" --no-build 2>&1 | tail -20`

(Pokud build cache chybí: `dotnet build PmTracker.Tests.Unit && dotnet test PmTracker.Tests.Unit --filter "ProposalCardCssTests" --no-build`)

Expected: FAIL — 3 testy selžou, protože `.proposal-card {`, `.proposal-list {`, `.proposal-card-header {` v site.css neexistují.

- [ ] **Step 3: Add CSS rules to site.css**

Vložit za uzavírací `}` bloku `.record-summary-meta` (řádek ~3593), před `.record-collab-inline`:

```css
.proposal-section-grid {
    display: flex;
    flex-direction: column;
    gap: 16px;
}

.proposal-list {
    display: flex;
    flex-direction: column;
    gap: 12px;
}

.proposal-card {
    border: 1px solid var(--gov-color-border);
    border-radius: var(--gov-radius);
    padding: 12px;
}

.proposal-card-header {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    gap: 12px;
}

.proposal-card-actions {
    display: flex;
    gap: 8px;
    margin-top: 8px;
}
```

**Kontext rizik:**
- `.proposal-card` uvnitř `.card` = zamýšlený double-border (sub-karta v kartě) — obě používají `var(--gov-color-border)`, konzistentní
- `.record-summary-meta` uvnitř `.proposal-card` má `margin-top: 8px` — funguje správně s proposal-card padding 12px (celkový vizuální prostor nahoře 12+8=20px, dole 12px)
- `.badge` (`display: inline-flex; padding: 4px 10px`) funguje v flex containeru bez úprav — `align-items: flex-start` na headeru zabrání roztažení badge do plné výšky
- Dark mode: `var(--gov-color-border)` a `var(--gov-radius)` se přepínají přes `:root[data-theme="dark"]` automaticky — ověřeno na existujícím `.card` pravidle (řádek 1402)

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PmTracker.Tests.Unit --filter "ProposalCardCssTests" --no-build`

Expected: PASS — všechny 3 testy zelené.

- [ ] **Step 5: Run full test suite to check for regressions**

Run: `dotnet test PmTracker.Tests.Unit --no-build 2>&1 | tail -5`

Expected: žádné nové selhání (existující pre-existing failures z `project_pre_existing_test_failures.md` zůstanou — 8 CSS/architektonických testů).

- [ ] **Step 6: Visual verification**

Spustit `dotnet run --project PmTracker.Web`, otevřít projekt s alespoň 2 návrhy v jedné sekci, hard-refresh (Ctrl+Shift+R), ověřit:
1. Každý návrh má viditelný border a padding — jasně oddělený od ostatních
2. Header: název vlevo, badge stavu vpravo
3. Tlačítka v akční řadě mají mezery
4. Dark mode: přepnout téma, ověřit že border barva se přepne
5. Prázdná sekce (0 návrhů): zobrazí se text "V této části zatím nejsou žádné návrhy." bez artefaktů
