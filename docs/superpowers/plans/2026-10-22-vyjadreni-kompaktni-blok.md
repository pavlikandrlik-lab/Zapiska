# Vyjádření v jednom kompaktním bloku — plán implementace

> **Pro agentní exekutory:** POVINNÝ SUB-SKILL: superpowers:executing-plans (inline, po úkolech, bez subagentů — uživatel 2026-10-12). Kroky jsou checkboxy (`- [ ]`). Commity do větve `feat/vyjadreni-kompaktni-blok`, na konci merge do lokálního `main`, bez pushe.

**Cíl:** Všechna vyjádření záznamu jsou v jednom ohraničeném bloku. Jednotlivá vyjádření odliší jen jemně šedé podbarvení řádku pod myší. Autor a datum jsou v bublině na štítku jednání. Upravit a Smazat se ukážou při najetí na řádek, ale nikdy u vyjádření k uzavřenému jednání. Cíl výšky: 5 jednořádkových vyjádření ≤ 200 px (dnes 600 px).

**Architektura:** Mění se jen sdílený partial `_ZaznamCommentsPartial.cshtml` (karta záznamu, detail jednání, stránka záznamu) a pravidla v `site.css`, omezená na `.record-comments`. Pravidla oprávnění, formuláře, JS háčky a server se nemění. Bublina používá existující vzor `.chip-wrap` + `.chip-tooltip` z karet externích vazeb.

**Tech Stack:** ASP.NET Core 8 MVC + Razor, CSS (gov DS tokeny), xUnit + FluentAssertions (Api `ApiSqlFixture`, Unit čtení zdrojů), E2E Playwright .NET, ruční průchod Playwright Node proti lokální app.

**Spec:** `docs/superpowers/specs/2026-10-22-vyjadreni-kompaktni-blok-design.md` (R1–R9, 5.4 „kdo uvidí ikony“, matice průchodu v §8).

## Global Constraints

- Pravidla, kdo smí vyjádření přidat, upravit a smazat, se **nemění**: `canEditThisComment = LzeUpravit && (CanEditRecord || (CanEditOwnAsSubsystemLeader && autor = já))`.
- U vyjádření k **uzavřenému jednání** (`MeetingStatePolicy.IsReadOnly`) se ikony nevykreslí a server úpravu i smazání odmítne (R8).
- Zachované háčky: `data-comment-list`, `data-comment-item`, `data-comment-id`, `data-comment-meeting`, `data-comment-date`, `data-comment-text`, `data-comment-edit-toggle`, `data-comment-edit-form`, `data-comment-edit-cancel`, `data-editing`. Pole formulářů úpravy a smazání, včetně `kontextJednaniId`, beze změny.
- CSS jen gov tokeny / `--app-*` (`AppCssColorTokenTests`), podbarvení `var(--background-neutral-subtle)`. Žádné `:has`, `max-content`, `[data-theme]`. Nová pravidla jen pod `.record-comments`, protože starý `Views/Jednani/_TaskItemPartial.cshtml` sdílí `.comment` a `.comment-list`. Žádná duplicitní pravidla.
- `.comment-actions` **zůstává**, používá ho formulář nového vyjádření (výběr jednání + Uložit).
- Razor: žádné `@* *@` uvnitř tagu s TagHelpery. Api testy kotví na atributy, český text jen po `WebUtility.HtmlDecode`.
- E2E: jednání s číslem pod 9820 a datem ne dnes.
- Po dokončení sekce 5 v `docs/k-otestovani.md` (stále ten jeden soubor).

## Review Focus

1. **Bublina u posledního řádku a u úzkého okna:** nesmí se ořezat blokem ani vyjet vodorovně mimo stránku. Ověří E2E (bublina viditelná u posledního řádku) a průchod 375 px.
2. **Ikony při najetí jsou dosažitelné klávesnicí.** Tab na tužku řádek „otevře“ (`:focus-within`), Enter otevře editor. Ověří E2E.
3. **Režim úpravy:** při `data-editing` zmizí štítek, bublina i ikony a editor má plnou šířku. Po Zrušit se vše vrátí. Ověří E2E.
4. **Uzavřené jednání:** žádné ikony ani při najetí, server odmítne podvrh. Ověří Api (Task 1 a 2) a E2E.
5. **Tmavý motiv:** šedé podbarvení je vidět (`neutral-900` na `neutral-950`). Ověří průchod se snímkem a vypočtenou barvou pozadí.

---

## Struktura souborů

| Soubor | Změna |
|---|---|
| `PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml` | nový markup seznamu a řádku |
| `PmTracker.Web/wwwroot/css/site.css` | pravidla bloku, řádku, ikon, `.chip-tooltip--end`; smazat `.comment-wrapper`, `.comment-meta-outer`, `.comment-author`, `.comment-sep`, `.comment-date`, `.comment-tile-side` |
| `PmTracker.Web/wwwroot/js/modules/comments.js` | jen komentář u řazení (logika beze změny) |
| `PmTracker.Tests.Api/Controllers/ClosedMeetingCommentChangesTests.cs` | **nový:** server odmítne úpravu i smazání u uzavřeného jednání |
| `PmTracker.Tests.Api/Controllers/CompactCommentsBlockRenderTests.cs` | **nový:** render bloku, bubliny, ikon podle role a stavu jednání |
| `PmTracker.Tests.Unit/Layout/CompactCommentsBlockCssTests.cs` | **nový:** pravidla CSS |
| `PmTracker.Tests.E2E/Scenarios/CompactCommentsBlockScenariosTests.cs` | **nový:** výška, najetí, klávesnice, úprava, řazení, uzavřené jednání |
| `docs/k-otestovani.md` | sekce 5 |

---

### Task 1: Zafixovat dnešní chování serveru u uzavřeného jednání

**Files:**
- Create: `PmTracker.Tests.Api/Controllers/ClosedMeetingCommentChangesTests.cs`

**Interfaces:**
- Produces: nic pro další tasky (pojistka chování R8 na serveru).

Charakterizační test: dnes projde, protože server odmítá už teď (`CommentService.EnsureMeetingAllowsCommentChangesAsync`). Aby bylo jisté, že test umí selhat, krok 3 dočasně vypne kontrolu a sleduje pád.

- [ ] **Step 1: Test**

```csharp
using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-10-22 R8: vyjádření k uzavřenému jednání nejde upravit ani smazat — ani PM,
/// ani podvrženým požadavkem. UI tlačítka nevykreslí (ClosedRecordCommentsRenderTests),
/// tady se hlídá server (CommentService.EnsureMeetingAllowsCommentChangesAsync).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ClosedMeetingCommentChangesTests
{
    private readonly ApiSqlFixture _fixture;

    public ClosedMeetingCommentChangesTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<(int ProjectId, int PmId, int RecordId, int CommentId)> SeedAsync(string marker, int meetingNumber)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var pmId = await _fixture.EnsurePersonAsync($"{marker}Pm");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        await _fixture.EnsureProjectRoleAssignmentAsync(projectId, pmId, ProjectRoleCodes.ProjectManager);
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} zaznam");
        var closedMeetingId = await _fixture.CreateMeetingAsync(projectId, "CLOSED", meetingNumber);
        await _fixture.AddCommentAsync(recordId, closedMeetingId, pmId, $"{marker} k uzavrenemu jednani");
        await using var db = _fixture.CreateDbContext();
        var commentId = await db.Vyjadreni.Where(x => x.ZaznamId == recordId).Select(x => x.Id).SingleAsync();
        return (projectId, pmId, recordId, commentId);
    }

    [Fact]
    public async Task UpdateComment_ClosedMeeting_IsRefused_TextUnchanged()
    {
        var s = await SeedAsync("ApiClsMtgUpd", 9611);
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/UpdateComment?asUser={s.PmId}",
            ApiTestHttpHelper.BuildForm(
                ("projektId", s.ProjectId.ToString()),
                ("uiContext", "project"),
                ("zaznamId", s.RecordId.ToString()),
                ("Id", s.CommentId.ToString()),
                ("Text", "<p>podvrh</p>")));

        var payload = await ApiTestHttpHelper.ReadModalResultAsync(await client.SendAsync(request));

        payload.Ok.Should().BeFalse("vyjádření k uzavřenému jednání nejde upravit");
        await using var db = _fixture.CreateDbContext();
        (await db.Vyjadreni.AsNoTracking().SingleAsync(x => x.Id == s.CommentId)).TextVyjadreni
            .Should().Be("ApiClsMtgUpd k uzavrenemu jednani");
    }

    [Fact]
    public async Task DeleteComment_ClosedMeeting_IsRefused_CommentKept()
    {
        var s = await SeedAsync("ApiClsMtgDel", 9613);
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/DeleteComment?asUser={s.PmId}",
            ApiTestHttpHelper.BuildForm(
                ("projektId", s.ProjectId.ToString()),
                ("uiContext", "project"),
                ("zaznamId", s.RecordId.ToString()),
                ("Id", s.CommentId.ToString())));

        var payload = await ApiTestHttpHelper.ReadModalResultAsync(await client.SendAsync(request));

        payload.Ok.Should().BeFalse("vyjádření k uzavřenému jednání nejde smazat");
        await using var db = _fixture.CreateDbContext();
        (await db.Vyjadreni.AnyAsync(x => x.Id == s.CommentId)).Should().BeTrue();
    }
}
```

- [ ] **Step 2: Spusť, dnes musí projít**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ClosedMeetingCommentChangesTests" --nologo`
Expected: PASS (2).

- [ ] **Step 3: Ověř, že test umí selhat (mutace, nevrací se do commitu)**

V `PmTracker.Web/Services/CommentService.cs` dočasně zakomentuj obě volání `await EnsureMeetingAllowsCommentChangesAsync(meeting, ct);` (v `UpdateCommentAsync` a `DeleteCommentAsync`).
Run: stejný příkaz jako v kroku 2. Expected: FAIL (2), `payload.Ok` je true.
Pak změnu vrať: `git checkout PmTracker.Web/Services/CommentService.cs`. Znovu spusť, Expected: PASS (2).

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Tests.Api/Controllers/ClosedMeetingCommentChangesTests.cs
git commit -m "test(vyjadreni): server odmítne úpravu i smazání vyjádření k uzavřenému jednání"
```

---

### Task 2: Markup — jeden blok, štítek s bublinou, ikony na řádku

**Files:**
- Create: `PmTracker.Tests.Api/Controllers/CompactCommentsBlockRenderTests.cs`
- Modify: `PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml` (seznam vyjádření, dnes ř. 29–112)

**Interfaces:**
- Produces (pro Task 3 a 4):
  - třídy `.comment-side`, `.comment-row-actions` s atributem `data-comment-row-actions`;
  - `.chip-wrap`, `.comment-meeting-chip` s `tabindex="0"` a `aria-describedby="vyjadreni-meta-{Id}"`;
  - `.chip-tooltip.chip-tooltip--end` s `id="vyjadreni-meta-{Id}"`, `role="tooltip"` a `data-comment-meta`.

- [ ] **Step 1: Padající Api testy**

```csharp
using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-10-22: vyjádření v jednom bloku, autor a datum jen v bublině štítku jednání,
/// Upravit / Smazat jen jako ikony řádku a nikdy u uzavřeného jednání (R8).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class CompactCommentsBlockRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public CompactCommentsBlockRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private sealed record Scenario(int ProjectId, int PmId, int HostId, int RecordId, int OpenCommentId, int ClosedCommentId);

    private async Task<Scenario> SeedAsync(string marker, int meetingBase)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var pmId = await _fixture.EnsurePersonAsync($"{marker}Pm", "Petr", "Manazer");
        var hostId = await _fixture.EnsurePersonAsync($"{marker}Host");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        await _fixture.EnsureProjectRoleAssignmentAsync(projectId, pmId, ProjectRoleCodes.ProjectManager);
        await _fixture.EnsureProjectRoleAssignmentAsync(projectId, hostId, ProjectRoleCodes.Host);
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} zaznam");
        var openMeetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", meetingBase);
        var closedMeetingId = await _fixture.CreateMeetingAsync(projectId, "CLOSED", meetingBase + 1);
        await _fixture.AddCommentAsync(recordId, openMeetingId, pmId, $"{marker} otevrene");
        await _fixture.AddCommentAsync(recordId, closedMeetingId, pmId, $"{marker} uzavrene");
        await using var db = _fixture.CreateDbContext();
        var ids = await db.Vyjadreni.Where(x => x.ZaznamId == recordId)
            .Select(x => new { x.Id, x.JednaniId }).ToListAsync();
        return new Scenario(projectId, pmId, hostId, recordId,
            ids.Single(x => x.JednaniId == openMeetingId).Id,
            ids.Single(x => x.JednaniId == closedMeetingId).Id);
    }

    private async Task<string> PanelHtmlAsync(Scenario s, int asUser)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/RecordCommentsPartial?projektId={s.ProjectId}&zaznamId={s.RecordId}&asUser={asUser}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return WebUtility.HtmlDecode(html);
    }

    /// <summary>Markup jednoho řádku: od jeho data-comment-item po další data-comment-item / konec seznamu.</summary>
    private static string RowHtml(string html, int commentId)
    {
        var match = Regex.Match(html,
            $@"data-comment-item[^>]*data-comment-id=""{commentId}"".*?(?=data-comment-item|</div>\s*</div>\s*(?:<div class=""comment-pagination|<form class=""comment-form|$))",
            RegexOptions.Singleline);
        match.Success.Should().BeTrue($"řádek vyjádření {commentId} je v panelu");
        return match.Value;
    }

    [Fact]
    public async Task Panel_IsOneBlock_WithoutPermanentAuthorLine()
    {
        var s = await SeedAsync("ApiCmpBlk", 9621);

        var html = await PanelHtmlAsync(s, s.PmId);

        Regex.Matches(html, "data-comment-list").Count.Should().Be(1);
        Regex.Matches(html, "data-comment-item").Count.Should().Be(2);
        html.Should().NotContain("comment-meta-outer", "autor a datum už nejsou trvale nad vyjádřením");
        html.Should().NotContain("data-comment-wrapper");
    }

    [Fact]
    public async Task EveryComment_HasFocusableMeetingChip_WithAuthorAndDateTooltip()
    {
        var s = await SeedAsync("ApiCmpTip", 9623);
        await using var db = _fixture.CreateDbContext();
        var dates = await db.Vyjadreni.AsNoTracking().Where(x => x.ZaznamId == s.RecordId)
            .ToDictionaryAsync(x => x.Id, x => x.DatumVyjadreni.ToString("dd.MM.yyyy"));

        var html = await PanelHtmlAsync(s, s.HostId);

        foreach (var commentId in new[] { s.OpenCommentId, s.ClosedCommentId })
        {
            var row = RowHtml(html, commentId);
            row.Should().MatchRegex($@"class=""comment-meeting-chip""[^>]*tabindex=""0""[^>]*aria-describedby=""vyjadreni-meta-{commentId}""");
            row.Should().MatchRegex($@"id=""vyjadreni-meta-{commentId}""[^>]*class=""chip-tooltip chip-tooltip--end""[^>]*role=""tooltip""");
            row.Should().Contain("Manazer", "bublina nese autora");
            row.Should().Contain(dates[commentId], "bublina nese datum zápisu");
        }
    }

    [Fact]
    public async Task ProjectManager_RowActionsOnlyOnOpenMeetingComment()
    {
        var s = await SeedAsync("ApiCmpAct", 9625);

        var html = await PanelHtmlAsync(s, s.PmId);

        Regex.Matches(html, "data-comment-row-actions").Count.Should().Be(1);
        RowHtml(html, s.OpenCommentId).Should().Contain("data-comment-row-actions");
        RowHtml(html, s.OpenCommentId).Should().Contain("data-comment-edit-toggle");
        RowHtml(html, s.ClosedCommentId).Should().NotContain("data-comment-row-actions", "u uzavřeného jednání žádné ikony (R8)");
        RowHtml(html, s.ClosedCommentId).Should().NotContain("/Zaznamy/DeleteComment");
    }

    [Fact]
    public async Task Host_NoRowActions_ButTooltips()
    {
        var s = await SeedAsync("ApiCmpHst", 9627);

        var html = await PanelHtmlAsync(s, s.HostId);

        html.Should().NotContain("data-comment-row-actions");
        Regex.Matches(html, @"role=""tooltip""[^>]*data-comment-meta|data-comment-meta[^>]*role=""tooltip""").Count.Should().Be(2);
    }
}
```

(Výchozí `RowHtml` končí na dalším `data-comment-item` nebo na konci seznamu. Pokud regex u posledního řádku nezabere konec seznamu, uprav lookahead podle skutečného markupu a zapiš ruling. Smysl testu se nemění: obsah řádku jednoho vyjádření.)

- [ ] **Step 2: Spusť, musí selhat**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~CompactCommentsBlockRenderTests" --nologo`
Expected: FAIL (4). Dnes je v markupu `comment-meta-outer` a chybí `tabindex`, `vyjadreni-meta-*` i `data-comment-row-actions`.

- [ ] **Step 3: Implementace — nahraď seznam v `_ZaznamCommentsPartial.cshtml`**

Blok `else { <div class="comment-list" data-comment-list> … </div> }` (od `@foreach (var vyjadreni in Model.Vyjadreni)` po konec `.comment-list`) nahraď:

```cshtml
        <div class="comment-list" data-comment-list>
            @foreach (var vyjadreni in Model.Vyjadreni)
            {
                var canEditThisComment = vyjadreni.LzeUpravit
                    && (Model.CanEditRecord
                        || (vyjadreni.CanEditOwnAsSubsystemLeader && vyjadreni.AutorOsobaId == Model.CurrentUserOsobaId));
                var metaId = $"vyjadreni-meta-{vyjadreni.Id}";
                var meetingLabel = vyjadreni.JednaniCislo.HasValue
                    ? (vyjadreni.JednaniDatum.HasValue
                        ? $"Jednání č. {vyjadreni.JednaniCislo} ({vyjadreni.JednaniDatum.Value:dd.MM.yyyy})"
                        : $"Jednání č. {vyjadreni.JednaniCislo}")
                    : "Jednání";
                @*
                    Spec 2026-10-22 (vyjádření v jednom bloku): řádek bez vlastního rámečku, vpravo float
                    se štítkem jednání; autor a datum jen v bublině štítku (hover / fokus), Upravit a Smazat
                    jen jako ikony řádku při :hover / :focus-within — a jen kde canEditThisComment
                    (LzeUpravit = jednání není uzavřené).
                *@
                <div class="comment"
                     data-comment-item
                     data-comment-meeting="@(vyjadreni.JednaniCislo ?? 0)"
                     data-comment-date="@vyjadreni.Datum.ToString("O")"
                     data-comment-id="@vyjadreni.Id">
                    <div class="comment-side">
                        @if (canEditThisComment)
                        {
                            <div class="comment-row-actions" data-comment-row-actions>
                                <pm-button variant="Secondary" size="Small" data-comment-edit-toggle="true" aria-label="Upravit vyjádření" title="Upravit vyjádření">
                                    <gov-icon size="s" name="pencil" type="components" aria-hidden="true"></gov-icon>
                                </pm-button>
                                <form class="comment-delete-form comment-delete-form-inline" asp-controller="Zaznamy" asp-action="DeleteComment" method="post" data-ajax-submit="true" onsubmit="return confirm('Opravdu smazat vyjádření?');">
                                    <input type="hidden" name="projektId" value="@Model.ProjektId" />
                                    <input type="hidden" name="uiContext" value="project" />
                                    <input type="hidden" name="zaznamId" value="@Model.ZaznamId" />
                                    <input type="hidden" name="Id" value="@vyjadreni.Id" />
                                    @if (Model.KontextJednaniId.HasValue)
                                    {
                                        <input type="hidden" name="kontextJednaniId" value="@Model.KontextJednaniId" />
                                    }
                                    <pm-button variant="Destructive" size="Small" native-type="submit" aria-label="Smazat vyjádření" title="Smazat vyjádření">
                                        <gov-icon size="s" name="trash3" type="components" aria-hidden="true"></gov-icon>
                                    </pm-button>
                                </form>
                            </div>
                        }
                        <span class="chip-wrap">
                            <span class="comment-meeting-chip" tabindex="0" aria-describedby="@metaId">@meetingLabel</span>
                            <span id="@metaId" class="chip-tooltip chip-tooltip--end" role="tooltip" data-comment-meta>
                                <span class="chip-tooltip-title">@vyjadreni.Autor</span>
                                <span>Zapsáno <time datetime="@vyjadreni.Datum.ToString("O")">@vyjadreni.Datum.ToString("dd.MM.yyyy")</time></span>
                            </span>
                        </span>
                    </div>
                    <div class="comment-text richtext-render" data-comment-text>@Html.Raw(RichTextContentService.ToSafeHtml(vyjadreni.Text))</div>
                    @if (canEditThisComment)
                    {
                        <form class="comment-edit-form" asp-controller="Zaznamy" asp-action="UpdateComment" method="post" data-ajax-submit="true" data-comment-edit-form hidden>
                            <input type="hidden" name="projektId" value="@Model.ProjektId" />
                            <input type="hidden" name="uiContext" value="project" />
                            <input type="hidden" name="zaznamId" value="@Model.ZaznamId" />
                            <input type="hidden" name="Id" value="@vyjadreni.Id" />
                            @if (Model.KontextJednaniId.HasValue)
                            {
                                <input type="hidden" name="kontextJednaniId" value="@Model.KontextJednaniId" />
                            }
                            <textarea rows="3"
                                      name="Text"
                                      required
                                      data-rich-text="true"
                                      data-rich-text-min-height="92">@RichTextContentService.ToSafeHtml(vyjadreni.Text)</textarea>
                            <div class="comment-edit-actions">
                                <pm-button variant="Secondary" size="Small" data-comment-edit-cancel="true">Zrušit úpravu</pm-button>
                                <pm-button variant="Primary" size="Small" native-type="submit">Uložit změnu</pm-button>
                            </div>
                        </form>
                    }
                </div>
            }
        </div>
```

(Formuláře úpravy a smazání jsou převzaté doslova ze současného souboru, včetně `kontextJednaniId`. Před nahrazením si je ještě jednou porovnej a odchylku zapiš jako ruling.)

- [ ] **Step 4: Spusť, musí projít + sousední testy panelu**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~CompactCommentsBlockRenderTests|FullyQualifiedName~ClosedRecordCommentsRenderTests|FullyQualifiedName~ClosedMeetingCommentChangesTests|FullyQualifiedName~MeetingContextCommentsTests|FullyQualifiedName~RecordCommentsFormAuthzRenderTests|FullyQualifiedName~RecordEditorControllerTests|FullyQualifiedName~MeetingDetailRecordsRenderTests" --nologo`
Expected: PASS. `CommentInClosedMeeting_HasNoEditOrDeleteControls` (1× `data-comment-edit-toggle`, 1× `DeleteComment`) beze změny.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml PmTracker.Tests.Api/Controllers/CompactCommentsBlockRenderTests.cs
git commit -m "feat(vyjadreni): jeden blok vyjádření, autor a datum v bublině štítku, ikony na řádku"
```

---

### Task 3: CSS — blok, šedé podbarvení, ikony při najetí, bublina vpravo

**Files:**
- Create: `PmTracker.Tests.Unit/Layout/CompactCommentsBlockCssTests.cs`
- Modify: `PmTracker.Web/wwwroot/css/site.css` (sekce „Layout vyjádření“ ř. ~4038–4135, `.chip-tooltip` ř. ~3953)
- Modify: `PmTracker.Web/wwwroot/js/modules/comments.js` (jen komentář u řazení, ř. ~93–99)

**Interfaces:**
- Consumes: třídy z Task 2.
- Produces: vzhled, na který se kotví E2E (Task 4): podbarvení `--background-neutral-subtle` a `opacity` ikon a bubliny.

- [ ] **Step 1: Padající unit test**

```csharp
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>Spec 2026-10-22 (vyjádření v jednom bloku) — pravidla v site.css.</summary>
public sealed class CompactCommentsBlockCssTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Css => File.ReadAllText(Path.Combine(RepoRoot(), "PmTracker.Web/wwwroot/css/site.css"));

    private static string Rule(string selector)
    {
        var match = Regex.Match(Css, Regex.Escape(selector) + @"\s*\{(?<body>[^}]*)\}");
        match.Success.Should().BeTrue($"pravidlo {selector} existuje");
        return match.Groups["body"].Value;
    }

    [Fact]
    public void Block_HasBorder_AndDoesNotClipTooltip()
    {
        var body = Rule(".record-comments .comment-list");
        body.Should().Contain("border: 1px solid var(--border-subtle)");
        body.Should().NotContain("overflow", "bublina u posledního řádku nesmí být oříznutá");
    }

    [Fact]
    public void Row_HasNoOwnBorder_AndGreyHover()
    {
        var row = Rule(".record-comments .comment");
        row.Should().Contain("border: 0");
        row.Should().Contain("display: flow-root");
        Regex.IsMatch(Css, @"\.record-comments \.comment:hover,\s*\.record-comments \.comment:focus-within\s*\{[^}]*background: var\(--background-neutral-subtle\)")
            .Should().BeTrue("jemně šedé podbarvení řádku pod myší i při fokusu (R2, R9)");
    }

    [Fact]
    public void RowActions_HiddenUntilRowHoverOrFocus()
    {
        var actions = Rule(".record-comments .comment-row-actions");
        actions.Should().Contain("opacity: 0");
        actions.Should().Contain("pointer-events: none");
        actions.Should().Contain("position: absolute", "ikony nesmí posunout text ani zvýšit řádek");
        Regex.IsMatch(Css, @"\.record-comments \.comment:hover \.comment-row-actions,\s*\.record-comments \.comment:focus-within \.comment-row-actions\s*\{[^}]*opacity: 1")
            .Should().BeTrue();
    }

    [Fact]
    public void Tooltip_EndVariant_AlignsToRightEdge()
    {
        Rule(".chip-tooltip--end").Should().Contain("right: 0");
    }

    [Fact]
    public void RemovedPermanentMetaLine_HasNoCss()
    {
        foreach (var dead in new[] { ".comment-wrapper", ".comment-meta-outer", ".comment-author", ".comment-sep", ".comment-date", ".comment-tile-side" })
        {
            Css.Should().NotContain(dead + " {", $"{dead} už markup nepoužívá");
        }
    }
}
```

- [ ] **Step 2: Spusť, musí selhat**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~CompactCommentsBlockCssTests" --nologo`
Expected: FAIL (5), pravidla neexistují a stará ano.

- [ ] **Step 3: Implementace CSS**

V `site.css` smaž pravidla `.comment-wrapper`, `.comment-meta-outer`, `.comment-author`, `.comment-sep`, `.comment-date`, `.comment-tile-side` a jejich komentář o „redesignu iterace 2“. Selektor `.comment[data-editing="true"] .comment-tile-side` nahraď za `.comment[data-editing="true"] .comment-side`. Před smazáním ověř grepem ve `Views/` a `wwwroot/js/`, že je mimo `_ZaznamCommentsPartial` nic nepoužívá. Pak za pravidlo `.comment-text` přidej:

```css
/* === Vyjádření v jednom bloku (spec 2026-10-22) ======================
   Blok = jeden rámeček; řádky bez vlastního rámečku, odliší je jen jemně šedé
   podbarvení pod myší / při fokusu. Vpravo float se štítkem jednání (autor a datum
   v bublině .chip-tooltip--end), Upravit / Smazat jako ikony řádku při najetí.
   Jen pod .record-comments — _TaskItemPartial (Jednani) sdílí .comment / .comment-list. */
.record-comments .comment-list {
    border: 1px solid var(--border-subtle);
    border-radius: var(--gov-radius);
    background: var(--background-block-primary);
    padding: 4px 0;
}

.record-comments .comment {
    border: 0;
    border-radius: 0;
    background: transparent;
    padding: 6px 12px;
    margin: 0;
    display: flow-root;
}

.record-comments .comment:hover,
.record-comments .comment:focus-within {
    background: var(--background-neutral-subtle);
}

.record-comments .comment[data-editing="true"] {
    background: var(--background-neutral-subtle);
}

.record-comments .comment-side {
    float: right;
    position: relative;
    display: flex;
    align-items: center;
    margin: 0 0 2px 12px;
    max-width: 60%;
}

.record-comments .comment-row-actions {
    position: absolute;
    right: calc(100% + 6px);
    top: 50%;
    transform: translateY(-50%);
    display: flex;
    gap: 4px;
    padding: 0 4px;
    border-radius: var(--gov-radius-sm);
    background: var(--background-neutral-subtle);
    opacity: 0;
    pointer-events: none;
}

.record-comments .comment:hover .comment-row-actions,
.record-comments .comment:focus-within .comment-row-actions {
    opacity: 1;
    pointer-events: auto;
}

.record-comments .comment-meeting-chip:focus-visible {
    outline: 2px solid var(--border-primary);
    outline-offset: 2px;
}

.record-comments .comment-text > :first-child {
    margin-top: 0;
}

.record-comments .comment-text > :last-child {
    margin-bottom: 0;
}
```

Za pravidlo `.chip-tooltip::before` přidej:

```css
/* Bublina u štítku při pravém okraji (vyjádření, spec 2026-10-22): zarovnaná vpravo, ať nevyjede z bloku. */
.chip-tooltip--end {
    left: auto;
    right: 0;
    min-width: 14rem;
    max-width: min(22rem, 80vw);
}

.chip-tooltip--end::before {
    left: auto;
    right: 14px;
}
```

V `comments.js` (řazení) nahraď komentář u `item.closest("[data-comment-wrapper]") || item` za:

```js
            // Panel vyjádření (spec 2026-10-22) i _TaskItemPartial řadí přímo řádky
            // [data-comment-item]; obal [data-comment-wrapper] (meta nad dlaždicí) už markup nemá.
```

(Logika řádku `const movable = …` zůstává, fallback na item je správně.)

- [ ] **Step 4: Spusť, musí projít + barvy a rozvržení**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~CompactCommentsBlockCssTests|FullyQualifiedName~AppCssColorTokenTests|FullyQualifiedName~Layout|FullyQualifiedName~Architecture" --nologo`
Expected: PASS.

- [ ] **Step 5: Rychlá kontrola výšky v prohlížeči** (před E2E, ať se ladí levně)

Lokální app (publikovaná do scratchpadu, viz memory „Průchod za všechny role“), 5 dočasných jednořádkových vyjádření na záznamu 99009, snímek panelu, `boundingBox` seznamu ≤ 200 px. Dočasná vyjádření pak smazat.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/wwwroot/css/site.css PmTracker.Web/wwwroot/js/modules/comments.js PmTracker.Tests.Unit/Layout/CompactCommentsBlockCssTests.cs
git commit -m "feat(vyjadreni): kompaktní blok — šedé podbarvení řádku, ikony při najetí, bublina vpravo"
```

---

### Task 4: E2E — výška, najetí, klávesnice, úprava, řazení, uzavřené jednání

**Files:**
- Create: `PmTracker.Tests.E2E/Scenarios/CompactCommentsBlockScenariosTests.cs`

**Interfaces:**
- Consumes: markup (Task 2) a CSS (Task 3).

- [ ] **Step 1: Scénář** (seed přes EF jako `MeetingDetailRecordsScenariosTests`, úklid ve `finally`)

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using static Microsoft.Playwright.Assertions;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Spec 2026-10-22: vyjádření v jednom bloku. 5 jednořádkových ≤ 200 px; bublina s autorem na štítku
/// (myš i Tab); ikony Upravit / Smazat při najetí na řádek, u uzavřeného jednání nikdy.
/// Jednání 9731–9736 (pod 9820), data v minulosti.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class CompactCommentsBlockScenariosTests
{
    private const int FirstMeetingNumber = 9731;
    private readonly E2ETestFixture _fixture;

    public CompactCommentsBlockScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private PmTrackerDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>().UseSqlServer(_fixture.Database.ConnectionString).Options);

    private sealed record Seed(int RecordId, IReadOnlyList<int> MeetingIds, IReadOnlyList<int> CommentIds, int ClosedCommentId);

    private async Task<Seed> SeedAsync(bool withClosedMeetingComment)
    {
        await using var db = CreateDbContext();
        var subsystemId = await db.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == _fixture.ProjectId && x.DatumOdebrani == null)
            .OrderBy(x => x.SubsystemId).Select(x => x.SubsystemId).FirstAsync();
        var categoryId = await db.CiselnikKategoriiZaznamu.Where(x => x.Kod == "U").Select(x => x.Id).FirstAsync();
        var taskStateId = await db.CiselnikStavuUkolu.Where(x => !x.IsFinal).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        var openStateId = await db.CiselnikStavuJednani.Where(x => x.Kod == "OPEN").Select(x => x.Id).FirstAsync();
        var closedStateId = await db.CiselnikStavuJednani.Where(x => x.Kod == "CLOSED").Select(x => x.Id).FirstAsync();
        var nextNumber = (await db.ProjektoveZaznamy.Where(x => x.ProjektId == _fixture.ProjectId)
            .Select(x => (int?)x.CisloZaznamu).MaxAsync() ?? 0) + 1;

        var record = new ProjektovyZaznamEntity
        {
            ProjektId = _fixture.ProjectId, KategorieId = categoryId, StavUkoluId = taskStateId,
            CisloZaznamu = nextNumber, Nazev = "E2E kompaktni vyjadreni", VlastnikId = _fixture.AdminOsobaId,
            DatumZalozeni = DateTime.Today.AddDays(-60), DatumUkonceni = DateTime.Today.AddDays(30), SubsystemId = subsystemId
        };
        db.ProjektoveZaznamy.Add(record);
        var meetings = Enumerable.Range(0, 6).Select(i => new JednaniEntity
        {
            ProjektId = _fixture.ProjectId, CisloJednani = FirstMeetingNumber + i,
            DatumPlanovane = DateTime.Today.AddDays(-30 + i), CasZacatek = new TimeOnly(9, 0),
            Misto = "E2E kompaktni", StavJednaniId = i == 5 ? closedStateId : openStateId
        }).ToList();
        db.Jednani.AddRange(meetings);
        await db.SaveChangesAsync();

        var comments = meetings.Take(5).Select((m, i) => new VyjadreniEntity
        {
            ZaznamId = record.Id, JednaniId = m.Id, AutorOsobaId = _fixture.AdminOsobaId,
            TextVyjadreni = $"<p>E2E jednoradkove vyjadreni {i + 1}</p>",
            TextVyjadreniProstyText = $"E2E jednoradkove vyjadreni {i + 1}",
            DatumVyjadreni = m.DatumPlanovane.AddHours(10)
        }).ToList();
        db.Vyjadreni.AddRange(comments);
        VyjadreniEntity? closedComment = null;
        if (withClosedMeetingComment)
        {
            closedComment = new VyjadreniEntity
            {
                ZaznamId = record.Id, JednaniId = meetings[5].Id, AutorOsobaId = _fixture.AdminOsobaId,
                TextVyjadreni = "<p>E2E k uzavrenemu jednani</p>", TextVyjadreniProstyText = "E2E k uzavrenemu jednani",
                DatumVyjadreni = meetings[5].DatumPlanovane.AddHours(10)
            };
            db.Vyjadreni.Add(closedComment);
        }
        await db.SaveChangesAsync();
        return new Seed(record.Id, meetings.Select(m => m.Id).ToList(), comments.Select(c => c.Id).ToList(), closedComment?.Id ?? 0);
    }

    private async Task CleanupAsync(Seed seed)
    {
        await using var db = CreateDbContext();
        await db.Vyjadreni.Where(x => x.ZaznamId == seed.RecordId).ExecuteDeleteAsync();
        await db.ProjektoveZaznamy.Where(x => x.Id == seed.RecordId).ExecuteDeleteAsync();
        await db.Jednani.Where(x => seed.MeetingIds.Contains(x.Id)).ExecuteDeleteAsync();
    }

    private async Task<ILocator> OpenPanelAsync(IPage page, Seed seed)
    {
        await page.GotoAsync($"{_fixture.BaseUrl}/Zaznamy/Detail/{seed.RecordId}?asUser={_fixture.AdminOsobaId}");
        var list = page.Locator("[data-comment-list]");
        await Expect(list).ToHaveCountAsync(1);
        return list;
    }

    [Fact]
    public async Task FiveOneLineComments_FitIn200px_AndTooltipShowsAuthorOnHoverAndFocus()
    {
        var seed = await SeedAsync(withClosedMeetingComment: false);
        var page = await _fixture.NewPageAsync();
        try
        {
            var list = await OpenPanelAsync(page, seed);
            var box = await list.BoundingBoxAsync();
            box.Should().NotBeNull();
            box!.Height.Should().BeLessThanOrEqualTo(200, "spec 2026-10-22: 5 jednořádkových vyjádření ≤ 200 px (dřív 600)");

            var lastRow = page.Locator($"[data-comment-item][data-comment-id='{seed.CommentIds[^1]}']");
            var tooltip = lastRow.Locator("[data-comment-meta]");
            (await tooltip.EvaluateAsync<string>("t => getComputedStyle(t).opacity")).Should().Be("0");
            await lastRow.Locator(".comment-meeting-chip").HoverAsync();
            await Expect(tooltip).ToHaveCSSAsync("opacity", "1");
            await Expect(tooltip).ToContainTextAsync(DateTime.Today.AddDays(-26).ToString("dd.MM.yyyy"));
            var tipBox = await tooltip.BoundingBoxAsync();
            var viewport = page.ViewportSize!;
            (tipBox!.X + tipBox.Width).Should().BeLessThanOrEqualTo(viewport.Width, "bublina nevyjede vpravo z okna");

            await page.Mouse.MoveAsync(5, 5);
            await Expect(tooltip).ToHaveCSSAsync("opacity", "0");
            await lastRow.Locator(".comment-meeting-chip").FocusAsync();
            await Expect(tooltip).ToHaveCSSAsync("opacity", "1");
        }
        finally
        {
            await page.Context.CloseAsync();
            await CleanupAsync(seed);
        }
    }

    [Fact]
    public async Task RowHover_ShowsGreyBackgroundAndActions_EditWorks_ClosedMeetingHasNone()
    {
        var seed = await SeedAsync(withClosedMeetingComment: true);
        var page = await _fixture.NewPageAsync();
        try
        {
            await OpenPanelAsync(page, seed);
            var row = page.Locator($"[data-comment-item][data-comment-id='{seed.CommentIds[0]}']");
            var actions = row.Locator("[data-comment-row-actions]");
            await Expect(actions).ToHaveCSSAsync("opacity", "0");
            var restingBg = await row.EvaluateAsync<string>("r => getComputedStyle(r).backgroundColor");

            await row.Locator("[data-comment-text]").HoverAsync();
            await Expect(actions).ToHaveCSSAsync("opacity", "1");
            var hoverBg = await row.EvaluateAsync<string>("r => getComputedStyle(r).backgroundColor");
            var expectedBg = await page.EvaluateAsync<string>(
                "() => { const d = document.createElement('div'); d.style.background = 'var(--background-neutral-subtle)'; document.body.appendChild(d); const c = getComputedStyle(d).backgroundColor; d.remove(); return c; }");
            hoverBg.Should().Be(expectedBg).And.NotBe(restingBg, "řádek pod myší je jemně šedý");

            await actions.Locator("[data-comment-edit-toggle]").DispatchEventAsync("click");
            await Expect(row.Locator("[data-comment-edit-form]")).Not.ToHaveAttributeAsync("hidden", "");
            await Expect(row.Locator(".comment-side")).ToBeHiddenAsync();
            await row.Locator("[data-comment-edit-cancel]").DispatchEventAsync("click");
            await Expect(row.Locator("[data-comment-edit-form]")).ToHaveAttributeAsync("hidden", "");

            var closedRow = page.Locator($"[data-comment-item][data-comment-id='{seed.ClosedCommentId}']");
            await closedRow.Locator("[data-comment-text]").HoverAsync();
            await Expect(closedRow.Locator("[data-comment-row-actions]")).ToHaveCountAsync(0);
            await Expect(closedRow.Locator("[data-comment-edit-toggle]")).ToHaveCountAsync(0);
        }
        finally
        {
            await page.Context.CloseAsync();
            await CleanupAsync(seed);
        }
    }

    [Fact]
    public async Task SortToggle_ReversesRowsInsideTheBlock()
    {
        var seed = await SeedAsync(withClosedMeetingComment: false);
        var page = await _fixture.NewPageAsync();
        try
        {
            await OpenPanelAsync(page, seed);
            var ids = page.Locator("[data-comment-list] > [data-comment-item]");
            var before = await ids.EvaluateAllAsync<string[]>("els => els.map(e => e.dataset.commentId)");
            await page.Locator("[data-comment-sort-toggle]").DispatchEventAsync("click");
            var after = await ids.EvaluateAllAsync<string[]>("els => els.map(e => e.dataset.commentId)");
            after.Should().Equal(before.Reverse(), "řazení přesouvá řádky uvnitř bloku");
        }
        finally
        {
            await page.Context.CloseAsync();
            await CleanupAsync(seed);
        }
    }
}
```

(Stránka záznamu (`/Zaznamy/Detail/{id}`) ukazuje panel rozbalený bez klikání. Pokud ho v E2E prostředí načítá líně, počkej na `[data-comment-list]` jako v `MeetingDetailRecordsScenariosTests`. Selektory převzaté z ověřených testů: `DispatchEventAsync("click")` na gov hostu, `ToHaveCSSAsync` pro opacity.)

- [ ] **Step 2: Spusť nové E2E**

Run: `dotnet test PmTracker.Tests.E2E --filter "FullyQualifiedName~CompactCommentsBlockScenariosTests|FullyQualifiedName~RecordRichTextScenariosTests|FullyQualifiedName~RichTextUnicodeScenariosTests|FullyQualifiedName~SearchResultTargetScenariosTests|FullyQualifiedName~MeetingDetailRecordsScenariosTests" --nologo`
Expected: nové 3 PASS. Stávající čtyři třídy mají stejný výsledek jako na `main`.

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Tests.E2E/Scenarios/CompactCommentsBlockScenariosTests.cs
git commit -m "test(vyjadreni): E2E kompaktní blok — výška, bublina, ikony při najetí, uzavřené jednání, řazení"
```

---

### Task 5: Plné sady, E2E proti baseline, průchod za všechny role, k-otestovani, merge

**Files:**
- Modify: `docs/k-otestovani.md`

- [ ] **Step 1: Plné sady**

Run: `dotnet test PmTracker.Tests.Unit --nologo`, `dotnet test PmTracker.Tests.Api --nologo`, `npm test`
Expected:
- Unit: 2229 + 5 PASS.
- Api: 506 + 6 PASS.
- JS: 45 PASS.

- [ ] **Step 2: E2E celá sada proti baseline `main`**

Dočasný worktree `main` ve scratchpadu, obě sady za sebou (ne souběžně), TRX do scratchpadu, porovnání jmen Pythonem. Expected: žádné nové jméno selhání (baseline 33), nové 3 testy prošly.

- [ ] **Step 3: Průchod v prohlížeči nad spuštěnou aplikací za všechny role** (spec §8, uživatel 2026-10-22)

1. Záloha dev DB (`BACKUP … COPY_ONLY`). Dočasné osoby „Průchod <ROLE>“ (11 rolí + bez přístupu) podle memory `reference_all_roles_browser_walkthrough`. Aplikace publikovaná do scratchpadu, port 5071.
2. Data na záznamu 99009 (subsystém 1):
   - vyjádření k jednání v přípravě 3: od PM-persony a od vedoucího;
   - vyjádření k otevřenému jednání 7: od PM-persony a od gestora;
   - vyjádření k uzavřenému jednání 1: od PM-persony.
   Vkládat přímo do `dbo.vyjadreni` s textem „Průchod …“.
3. Skript (Node, bundled Playwright, `context.route` doplní `asUser`) za každou personu a každé místo (záložka Záznamy s `recordId=99009&openComments=true`, detail jednání 7, stránka záznamu 99009) ověří podle matice spec §8:
   - blok je jeden a 404 pro osobu bez přístupu;
   - bublina na štítku s autorem a datem u každého vyjádření (najetí myší);
   - ikony při najetí přesně u očekávaných vyjádření: editory u přípravy a otevřeného, vedoucí a zástupce jen u vlastního v přípravě, ostatní nikde, **u uzavřeného nikdo**;
   - podbarvení řádku = vypočtená `--background-neutral-subtle`;
   - konzole bez chyb.
4. Akce:
   - PM-persona jedno vyjádření upraví a jedno smaže (najetí → ikona → uložit / potvrdit);
   - vedoucí upraví své vyjádření v přípravě;
   - podvržená úprava vyjádření k uzavřenému jednání za PM-personu skončí `ok=false`.
5. Vzhled: tmavý motiv (podbarvení viditelné, vypočtená barva ≠ pozadí bloku), 375 px bez vodorovného posuvníku, Tab na štítek ukáže bublinu, snímky před a po s výškou seznamu.
6. Úklid: dočasné osoby, vyjádření „Průchod …“ a jejich audit, zámky. Kontrolní dotaz = 0 řádků.

Expected: matice sedí na 100 %, 0 JS chyb. Odchylku řešit podle superpowers:systematic-debugging, ne úpravou očekávání.

- [ ] **Step 4: `docs/k-otestovani.md` sekce 5** (stejný soubor, formát jako sekce 4: co to dělá, ověřeno lokálně s maticí rolí, checklist k otestování, známá omezení včetně nesouladu Gestor / Metodik z spec §5.4)

- [ ] **Step 5: Revize celé větve, commit, merge**

Self-review podle code-reviewer.md (bez subagenta, uživatelské pravidlo). Pak:

```bash
git add docs/k-otestovani.md
git commit -m "docs(k-otestovani): vyjádření v jednom kompaktním bloku"
git checkout main
git merge --no-ff feat/vyjadreni-kompaktni-blok -m "Merge feat/vyjadreni-kompaktni-blok into main"
git branch -d feat/vyjadreni-kompaktni-blok
```

Doplnit hash merge do sekce 5 a commitnout na `main`. Push ne.

---

## Odhad (Opus inline)

| Task | Odhad |
|---|---|
| 1. Server, uzavřené jednání (charakterizace + mutace) | 0,25 h |
| 2. Markup + Api testy | 0,75 h |
| 3. CSS + unit testy + kontrola výšky | 1 h |
| 4. E2E | 0,75 h |
| 5. Sady, E2E baseline, průchod za všechny role, k-otestovani, merge | 2 h |
| **Celkem** | **≈ 4,75 h** |

Tvoje ruční ověření podle sekce 5 v `k-otestovani.md` je navíc.
