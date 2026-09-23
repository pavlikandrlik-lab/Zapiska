# B5 — Spolupráce: kompaktní jednořádkový výběr — Implementation Plan

> **For agentic workers:** Exekuce INLINE. **Commity DRŽET.** Pouštět PO B3+B4 (panel se v B4 dotýká — disabled atributy; tento plán pracuje s výslednou podobou).

**Goal:** Řádek osoby = checkbox + jméno + org. celek (vpravo, tlumeně); email z výpisu pryč, hledání podle emailu funguje dál.

**Architecture:** Markup `.collab-option` z 3 stacked spanů na flex řádek; `data-collab-label` (searchable) beze změny. CSS blok collab-option přepsat na flex.

## Global Constraints
- `data-collab-label` musí dál obsahovat email + organizaci (search).
- B4 `collabDisabled` disabled atributy zachovat. Commity držené.

---

### Task 1: Render test + markup + CSS

**Files:**
- Test: `PmTracker.Tests.Api/Controllers/CollabPanelRenderTests.cs` (create)
- Modify: `PmTracker.Web/Views/Projekty/_EditZaznamCollaborationPanel.cshtml` (ř. ~15–26)
- Modify: `PmTracker.Web/wwwroot/css/site.css` (blok ~4588–4620)

- [ ] **Step 1: Failing Api test**

```csharp
using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>B5 (2026-07-09): collab řádek = jméno + org celek; email jen v searchable labelu.</summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class CollabPanelRenderTests
{
    private readonly ApiSqlFixture _fixture;
    public CollabPanelRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task RecordEditor_CollabRows_ShowNameAndOrgUnit_NoEmailSpan()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiCollabOwner");
        var projectId = await _fixture.EnsureProjectAsync("APICOLLAB");
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Create?projektId={projectId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        html.Should().Contain("collab-option-org", "org celek se zobrazuje");
        html.Should().NotContain("collab-option-email", "email z výpisu zmizel");
        html.Should().NotContain("collab-option-origin", "stará organizace/celek dvojice zmizela");
        // searchable label dál nese email (hledání podle emailu):
        html.Should().MatchRegex(@"data-collab-label=""[^""]*@[^""]*""");
    }
}
```

- [ ] **Step 2: Run — FAIL** (`collab-option-org` neexistuje).

- [ ] **Step 3: Markup** — nahradit spany (ř. 24–26, po B4 s disabled checkboxy):

```razor
                    <span class="collab-option-name">@osoba.Osoba</span>
                    <span class="collab-option-org">@(osoba.OrganizacniCelek ?? "-")</span>
```
(řádky `collab-option-email` a `collab-option-origin` smazat; `searchable` proměnná ř. 14 beze změny — email/organizace v ní zůstávají).

- [ ] **Step 4: CSS** — přepsat blok:

```css
/* B5 (2026-07-09): jednořádkový výběr — checkbox | jméno | org celek vpravo.
   Email z výpisu pryč (zůstává v data-collab-label pro hledání). */
.collab-option {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 6px 10px;
    border-bottom: 1px solid var(--gov-color-border);
}

.collab-option:last-child {
    border-bottom: none;
}

.collab-option input[type="checkbox"] {
    width: auto;
    margin: 0;
    flex: 0 0 auto;
}

.collab-option-name {
    font-size: 14px;
    font-weight: 600;
    line-height: 1.2;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.collab-option-org {
    margin-left: auto;
    flex: 0 0 auto;
    color: var(--pm-text-muted);
    font-size: 13px;
}
```
Smazat stará pravidla `.collab-option-email` / `.collab-option-origin` / grid pravidla bloku (grep výskyty vč. dark-mode sekcí — memory duplicate-rules; `grep -n "collab-option" site.css` a projít všechny).

- [ ] **Step 5: Run — PASS** + build + restart.

- [ ] **Step 6: Ověření (Playwright, dev):** editor záznamu → tab Spolupráce: výška `.collab-option` ≤ 36 px; napsat do search část emailu (`@test`/`test.local`) → filtruje správné řádky; screenshot do reportu. B4 decision stránka: disabled vzhled na novém layoutu (screenshot).
