# A1 — Účast v jednání: role účastníka + responzivní 2 sloupce — Implementation Plan

> **For agentic workers:** Exekuce INLINE. **Commity DRŽET.**

**Goal:** Panel Účast ukazuje u každé osoby její aktivní role v projektu (podtext pod jménem) a na širokém monitoru se řádky skládají do 2 sloupců; SaveAttendance beze změny.

**Architecture:** Server role už má (`ActiveProjectMembershipRow.AktivniRole`); membership rows se v `BuildJednaniDetailAsync` načtou JEDNOU a sdílí pro účast, legacy fallback i kandidáty modalu (dnes se dotazují 2×). `<table>` v panelu se nahradí grid „řádkovými kartami" (`repeat(auto-fill, minmax(480px, 1fr))`) — 2 sloupce vyplynou z šířky, bez media queries. Form kontrakt `rows[@i].OsobaId/StavUcasti` zůstává.

**Tech Stack:** C# služba + VM, Razor, CSS, Integration + Api + E2E testy.

## Global Constraints
- `SaveAttendance` POST formát beze změny (souvislé indexy `rows[0..n]`).
- Toggle `data-meeting-attendance-toggle` + label span (fix 2026-07-08) zachovat.
- Commity držené.

---

### Task 1: VM + služba (role do účasti, sdílené membership rows)

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/JednaniViewModels.cs:57` (UcastViewModel)
- Modify: `PmTracker.Web/Services/MeetingService.DetailQueries.cs` (BuildJednaniDetailAsync ~ř. 23–85; BuildMeetingAttendanceAsync ~ř. 168; BuildLegacyMeetingAttendanceAsync ~ř. 213; BuildMeetingParticipantCandidatesAsync ~ř. 309)
- Test: `PmTracker.Tests.Integration/DataStore/MeetingAttendanceRolesDataStoreTests.cs` (create)

**Interfaces:**
- Consumes: `BuildActiveProjectMembershipRowsAsync(int projectId, CancellationToken)` → `List<ActiveProjectMembershipRow>` (`OsobaId, Osoba, Email, HasNonHostProjectRole, HasSubsystemRole, AktivniRole`).
- Produces: `UcastViewModel.AktivniRole : IReadOnlyList<string>`; privátní signatury: `BuildMeetingAttendanceAsync(int meetingId, IReadOnlyList<ActiveProjectMembershipRow> membershipRows, CancellationToken ct)`, `BuildMeetingParticipantCandidatesAsync(int projectId, int meetingId, bool includeAlreadyPresent, IReadOnlyList<ActiveProjectMembershipRow> membershipRows, CancellationToken ct)`.

- [ ] **Step 1: Failing Integration test**

```csharp
using FluentAssertions;
using PmTracker.Tests.Integration.TestInfrastructure;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>A1 (2026-07-08): detail jednání nese u účastníků jejich aktivní role v projektu.</summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class MeetingAttendanceRolesDataStoreTests
{
    private readonly SqlIntegrationFixture _fixture;
    public MeetingAttendanceRolesDataStoreTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task BuildJednaniDetail_UcastCarriesActiveProjectRoles()
    {
        var db = await _fixture.CreateDatabaseAsync("meeting_attendance_roles");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "AttRoleAdmin");
        var memberId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "AttRoleMember");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "ATTROLE");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "ATTROLE_SYS", adminId);

        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, memberId, ProjectRoleCodes.ProjectOwner);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, memberId);

        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9720);
        var detail = store.BuildJednaniDetail(meetingId);

        // Bez explicitní účasti → legacy fallback z aktivních členů; memberId tam je s rolemi.
        var member = detail.Ucast.SingleOrDefault(x => x.OsobaId == memberId);
        member.Should().NotBeNull();
        member!.AktivniRole.Should().NotBeEmpty("aktivní projektová i subsystémová role musí být vidět");
        member.AktivniRole.Should().Contain(r => r.Contains("ATTROLE_SYS"), "subsystémový label nese kód subsystému");
    }
}
```

Pozn.: přesnou signaturu `EnsureActiveSubsystemRoleAssignmentAsync` ověř v `IntegrationTestHelper` (existuje — používá ji subsystem-lead testování); parametry uprav dle ní (projektSubsystemId vs subsystemId).

- [ ] **Step 2: Run — FAIL** kompilací (`AktivniRole` neexistuje): `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~MeetingAttendanceRolesDataStoreTests" -v q --nologo`

- [ ] **Step 3: VM**

```csharp
public sealed class UcastViewModel
{
    public int OsobaId { get; init; }
    public required string Osoba { get; init; }
    public string? Email { get; init; }
    public string? StavUcastiKod { get; init; }
    public required string StavUcasti { get; init; }
    /// <summary>A1 (2026-07-08): aktivní role osoby v projektu (projektové + „Role (SUBSYSTÉM)").</summary>
    public IReadOnlyList<string> AktivniRole { get; init; } = [];
}
```

- [ ] **Step 4: Služba** — v `BuildJednaniDetailAsync` po načtení `project`:

```csharp
        // A1 (2026-07-08): membership rows JEDNOU — sdílí je účast (role labely),
        // legacy fallback i kandidáti modalu (dřív se BuildActiveProjectMembershipRowsAsync volal 2×).
        var membershipRows = await BuildActiveProjectMembershipRowsAsync(project.Id, ct);
        var attendance = await BuildMeetingAttendanceAsync(id, membershipRows, ct);
```
a v result: `AvailableParticipantCandidates = await BuildMeetingParticipantCandidatesAsync(project.Id, meeting.Id, includeAlreadyPresent: false, membershipRows, ct),`

Změny helperů:
```csharp
    private async Task<List<UcastViewModel>> BuildMeetingAttendanceAsync(
        int meetingId,
        IReadOnlyList<ActiveProjectMembershipRow> membershipRows,
        CancellationToken ct)
    {
        var rolesByOsoba = membershipRows.ToDictionary(x => x.OsobaId, x => x.AktivniRole);
        var attendances = await dbContext.Ucast.AsNoTracking()
            .Where(x => x.JednaniId == meetingId)
            .ToListAsync(ct);
        var stateRows = await dbContext.CiselnikStavuUcasti.AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
        var states = stateRows.ToDictionary(x => x.Id);
        var defaultState = ResolveDefaultAttendanceState(stateRows);

        if (attendances.Count == 0)
        {
            return BuildLegacyMeetingAttendance(membershipRows, defaultState);
        }

        var attendanceByPerson = attendances.ToDictionary(x => x.OsobaId);
        var participantIds = attendances.Select(x => x.OsobaId).Distinct().ToList();
        var people = participantIds.Count == 0
            ? new Dictionary<int, OsobaEntity>()
            : await dbContext.Osoby.AsNoTracking()
                .Where(x => participantIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);

        return participantIds
            .OrderBy(osobaId => BuildDisplayNameFromOsoba(people.GetValueOrDefault(osobaId)), StringComparer.CurrentCultureIgnoreCase)
            .Select(osobaId =>
            {
                var attendance = attendanceByPerson.GetValueOrDefault(osobaId);
                var attendanceState = attendance is null
                    ? defaultState
                    : states.GetValueOrDefault(attendance.StavUcastiId);

                return new UcastViewModel
                {
                    OsobaId = osobaId,
                    Osoba = BuildDisplayNameFromOsoba(people.GetValueOrDefault(osobaId)),
                    Email = people.GetValueOrDefault(osobaId)?.Email?.Trim(),
                    StavUcastiKod = attendanceState?.Kod,
                    StavUcasti = attendanceState?.Nazev ?? "-",
                    AktivniRole = rolesByOsoba.GetValueOrDefault(osobaId, [])
                };
            })
            .ToList();
    }

    private static List<UcastViewModel> BuildLegacyMeetingAttendance(
        IReadOnlyList<ActiveProjectMembershipRow> membershipRows,
        CiselnikStavuUcastiEntity? defaultState)
    {
        return membershipRows
            .Where(item => item.HasNonHostProjectRole || item.HasSubsystemRole)
            .OrderBy(item => item.Osoba, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new UcastViewModel
            {
                OsobaId = item.OsobaId,
                Osoba = item.Osoba,
                Email = item.Email,
                StavUcastiKod = defaultState?.Kod,
                StavUcasti = defaultState?.Nazev ?? "-",
                AktivniRole = item.AktivniRole
            })
            .ToList();
    }
```
Smazat starý `BuildLegacyMeetingAttendanceAsync` a `BuildDefaultAttendanceParticipantRowsAsync` (jediný konzument byl legacy path — ověřit grepem; pokud má dalšího konzumenta, ponechat a jen delegovat). Kandidáti:
```csharp
    private async Task<List<MeetingParticipantCandidateViewModel>> BuildMeetingParticipantCandidatesAsync(
        int projectId, int meetingId, bool includeAlreadyPresent, CancellationToken ct)
        => await BuildMeetingParticipantCandidatesAsync(
            projectId, meetingId, includeAlreadyPresent,
            await BuildActiveProjectMembershipRowsAsync(projectId, ct), ct);

    private async Task<List<MeetingParticipantCandidateViewModel>> BuildMeetingParticipantCandidatesAsync(
        int projectId, int meetingId, bool includeAlreadyPresent,
        IReadOnlyList<ActiveProjectMembershipRow> membershipRows, CancellationToken ct)
    {
        var alreadyPresentIds = (await dbContext.Ucast.AsNoTracking()
            .Where(x => x.JednaniId == meetingId)
            .Select(x => x.OsobaId)
            .ToListAsync(ct))
            .ToHashSet();

        return membershipRows
            .Select(group => new MeetingParticipantCandidateViewModel
            {
                OsobaId = group.OsobaId,
                Osoba = group.Osoba,
                Email = group.Email,
                AktivniRole = group.AktivniRole
            })
            .Where(x => includeAlreadyPresent || !alreadyPresentIds.Contains(x.OsobaId))
            .OrderBy(x => x.Osoba, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
```

- [ ] **Step 5: Build + run Integration test — PASS**; poté celé `MeetingTasksDataStoreTests` + `ProjectMembershipDataStoreTests` (sousedi) zelené.

### Task 2: View (grid řádkových karet) + CSS + Api render test

**Files:**
- Modify: `PmTracker.Web/Views/Jednani/Detail.cshtml` (blok `<table class="table meeting-attendance-table">…</table>`)
- Modify: `PmTracker.Web/wwwroot/css/site.css` (za `.meeting-attendance-panel` blok ~ř. 5503)
- Test: `PmTracker.Tests.Api/Controllers/MeetingAttendanceRenderTests.cs` (create)

- [ ] **Step 1: Failing Api test**

```csharp
using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>A1: panel Účast renderuje role účastníka a grid řádky (ne tabulku).</summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class MeetingAttendanceRenderTests
{
    private readonly ApiSqlFixture _fixture;
    public MeetingAttendanceRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task MeetingDetail_AttendanceShowsRoles_InGridRows()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiAttRoleOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIATTROLE");
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId); // aktivní projektová role
        var meetingId = await _fixture.EnsureMeetingAsync(projectId); // pokud helper chybí, doplnit dle ApiSqlFixture vzorů (INSERT jednani OPEN)

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Jednani/Detail/{meetingId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        html.Should().Contain("meeting-attendance-grid", "tabulku nahradil grid řádkových karet");
        html.Should().Contain("meeting-attendance-roles", "role účastníka jsou v podtextu");
        html.Should().NotContain("meeting-attendance-table");
    }
}
```
(Existenci `EnsureMeetingAsync` ověř v `ApiSqlFixture`; pokud není, přidej do fixture minimální INSERT dle vzoru `EnsureRecordAsync` — stav OPEN, dnešní datum.)

- [ ] **Step 2: Run — FAIL.**

- [ ] **Step 3: Markup** — nahradit `<table …>…</table>` uvnitř formu za:

```razor
                <div class="meeting-attendance-grid">
                @for (var i = 0; i < Model.Ucast.Count; i++)
                {
                    var ucast = Model.Ucast[i];
                    <div class="meeting-attendance-row">
                        <div class="meeting-attendance-person">
                            <strong>@ucast.Osoba</strong>
                            @if (!string.IsNullOrWhiteSpace(ucast.Email))
                            {
                                <div class="muted">@ucast.Email</div>
                            }
                            @if (ucast.AktivniRole.Count > 0)
                            {
                                <div class="muted meeting-attendance-roles">@string.Join(" · ", ucast.AktivniRole)</div>
                            }
                            <input type="hidden" name="rows[@i].OsobaId" value="@ucast.OsobaId" />
                        </div>
                        <select class="meeting-attendance-select" name="rows[@i].StavUcasti"
                                aria-label="Stav účasti — @ucast.Osoba" disabled="@(!canEditMeetingFields)">
                            @foreach (var stav in Model.StavyUcasti)
                            {
                                var selected = string.Equals(stav.Value, ucast.StavUcastiKod, StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(stav.Label, ucast.StavUcasti, StringComparison.OrdinalIgnoreCase);
                                if (selected)
                                {
                                    <option value="@stav.Value" selected="selected">@stav.Label</option>
                                }
                                else
                                {
                                    <option value="@stav.Value">@stav.Label</option>
                                }
                            }
                        </select>
                    </div>
                }
                </div>
```

- [ ] **Step 4: CSS** — za blok `.meeting-attendance-panel { … }`:

```css
/* A1 (2026-07-08): řádkové karty účasti — 2 sloupce na širokém monitoru (auto-fill),
   1 sloupec na 13"; role účastníka jako podtext. Form kontrakt rows[i] beze změny. */
.meeting-attendance-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(480px, 1fr));
    gap: 10px 24px;
}
.meeting-attendance-row {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    gap: 12px;
    padding: 8px 10px;
    border: 1px solid var(--pm-border);
    border-radius: 6px;
    background: var(--pm-surface);
}
.meeting-attendance-row .meeting-attendance-select { width: 240px; flex: 0 0 auto; }
.meeting-attendance-roles { font-size: 0.8125rem; }
@media (max-width: 640px) {
    .meeting-attendance-row { flex-direction: column; }
    .meeting-attendance-row .meeting-attendance-select { width: 100%; }
}
```
Staré `.meeting-attendance-table` selektory smazat (grep výskyty i v dark-mode sekcích — memory duplicate-rules).

- [ ] **Step 5: Run Api test — PASS**; Api POST regrese: existující testy SaveAttendance (grep `SaveAttendance` v Tests.Api) zelené; pokud neexistují, přidat POST test: 2 osoby → uložit stavy → 302 + DB stav změněn.

### Task 3: Responzivní ověření (E2E)

- [ ] **Step 1:** `MeetingModalPickerPositionScenariosTests` vzor: nový test v `PmTracker.Tests.E2E/Scenarios/MeetingAttendanceLayoutScenariosTests.cs` — otevřít detail jednání (fixture: meeting seed jako v E2E fixture — pokud fixture jednání neseeduje, vytvořit přes UI „Nové jednání" modal v testu), „Zobrazit účast", měřit: viewport 1920×1000 → aspoň 2 `.meeting-attendance-row` se stejným `top` (2 sloupce); viewport 1280×900 → všechny řádky pod sebou (unikátní top hodnoty), žádný horizontální overflow (`scrollWidth ≤ clientWidth`).
- [ ] **Step 2: Run — PASS**; screenshoty 1280/1920 do reportu.
