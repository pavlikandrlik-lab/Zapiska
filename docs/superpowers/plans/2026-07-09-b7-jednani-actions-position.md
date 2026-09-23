# B7 — /Jednani: tlačítka sidebar drží u obsahu — Implementation Plan

> **For agentic workers:** Exekuce INLINE. **Commity DRŽET.**

**Goal:** „Detail projektu / Nové jednání" zůstávají pod statistikami i při rozbalení mnoha let.

**Architecture:** Jednořádkový CSS fix: `.meeting-project-overview__actions` `margin-top: auto` → `var(--pm-spacing-s)`.

## Global Constraints
- Mobilní breakpoint beze změny. Commity držené.

---

### Task 1: Test + fix + měření

**Files:**
- Test: `PmTracker.Tests.Unit/Layout/DashboardTokenScopeTests.cs` → NE; nový soubor `PmTracker.Tests.Unit/Layout/MeetingOverviewActionsTests.cs` (create)
- Modify: `PmTracker.Web/wwwroot/css/site.css:5209-5215`

- [ ] **Step 1: Failing test**

```csharp
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>B7 (2026-07-09): margin-top:auto odvezl akce na dno natažené karty (rozbalené roky).</summary>
public sealed class MeetingOverviewActionsTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    [Fact]
    public void OverviewActions_DoNotUseAutoTopMargin()
    {
        var css = File.ReadAllText(Path.Combine(RepoRoot(), "PmTracker.Web/wwwroot/css/site.css"));
        var block = Regex.Match(css, @"\.meeting-project-overview__actions\s*\{[^}]*\}", RegexOptions.Singleline);
        block.Success.Should().BeTrue();
        block.Value.Should().NotContain("margin-top: auto",
            "auto-margin posílá tlačítka na dno karty natažené rozbalenými roky");
    }
}
```

- [ ] **Step 2: Run — FAIL.** `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~MeetingOverviewActionsTests" --nologo -v q`

- [ ] **Step 3: CSS fix**

```css
.meeting-project-overview__actions {
    /* B7 (2026-07-09): NE margin-top:auto — sidebar se natahuje s výškou karty
       (rozbalené roky) a auto-margin odvezl tlačítka na dno mimo dohled. */
    margin-top: var(--pm-spacing-s);
    padding-top: var(--pm-spacing-s);
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: var(--pm-spacing-2xs);
}
```

- [ ] **Step 4: Run — PASS.**

- [ ] **Step 5: Playwright měření** (dev app, /Jednani?asUser=1): rozbalit aktuální rok (`[data-meeting-year-toggle]` click) + historii (`[data-project-history-toggle]` click, existuje-li) → změřit `actions.getBoundingClientRect().top − stats.getBoundingClientRect().bottom` ≤ 24 px; screenshot rozbaleného stavu do reportu.
