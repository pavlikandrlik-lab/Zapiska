# B6 — Účast: skupiny dle projektových rolí, uvnitř příjmení+jméno — Implementation Plan

> **For agentic workers:** Exekuce INLINE. **Commity DRŽET.**

**Goal:** Pořadí účastníků i kandidátů: Vlastník(1) → Gestor(2) → PM(3) → Admin(4) → zbytek(5); uvnitř skupiny příjmení → jméno. Jen pořadí, žádné vizuální nadpisy.

**Architecture:** `ActiveProjectMembershipRow` + `int GroupPriority` (min přes projektové RoleKod; subsystem-only fragmenty → 5). Jediné řadicí trio `GroupPriority → Prijmeni → Jmeno` na: rows (kandidáti+legacy) a normal-attendance klíči (lookup rows, fallback 5).

**Tech Stack:** C# LINQ, Integration testy.

## Global Constraints
- Kódy: `VLASTNIK_PROJEKTU`=1, `GEST`=2, `PROJ_MAN`=3, `ADM_PROJ`=4, jinak 5 (RoleCatalogKeys/ciselnik).
- Nahrazuje včerejší surname-only test (přepsat, ne duplikovat).
- Commity držené.

---

### Task 1: Integration failing test (RED)

**Files:**
- Modify: `PmTracker.Tests.Integration/DataStore/MeetingAttendanceRolesDataStoreTests.cs` — test `BuildJednaniDetail_OrdersUcastAndCandidates_BySurnameThenGivenName` PŘEPSAT na skupinový

**Interfaces:**
- Consumes: `IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(db, projectId, osobaId, roleCode)` (roleCode string — kódy výše), `EnsureActiveSubsystemRoleAssignmentAsync(db, projectId, subsystemId, osobaId, SubsystemRoleCodes.Lead)`, `ProjectRoleCodes` konstanty (ověř: obsahuje ProjectOwner/Gestor/ProjectManager/ProjectAdmin? grep — jinak použij string literály kódů).

- [ ] **Step 1: Přepsat test**

```csharp
    /// <summary>B6 (2026-07-09): pořadí = role skupina (Vlastník→Gestor→PM→Admin→zbytek),
    /// uvnitř skupiny příjmení→jméno. Nahrazuje surname-only řazení z 2026-07-09 rána.</summary>
    [Fact]
    public async Task BuildJednaniDetail_OrdersUcastAndCandidates_ByRoleGroupThenSurname()
    {
        var db = await _fixture.CreateDatabaseAsync("meeting_attendance_role_groups");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var seedAdmin = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "GrpSeedAdmin");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "GRPORD");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "GRPORD_SYS", seedAdmin);

        async Task<int> PersonAsync(string marker, string jmeno, string prijmeni)
        {
            var id = await IntegrationTestHelper.EnsurePersonAsync(dbContext, marker);
            var p = await dbContext.Osoby.SingleAsync(x => x.Id == id);
            p.Jmeno = jmeno; p.Prijmeni = prijmeni;
            await dbContext.SaveChangesAsync();
            return id;
        }

        // Křestní/příjmení volená PROTI skupinovému pořadí (skupina musí přebít abecedu):
        var vlastnik = await PersonAsync("GrpVlastnik", "Zdenek", "Žlutý");      // skupina 1
        var pm       = await PersonAsync("GrpPm",       "Cyril",  "Adamec");     // skupina 3
        var admin2   = await PersonAsync("GrpAdminB",   "Bora",   "Bílý");       // skupina 4
        var admin1   = await PersonAsync("GrpAdminA",   "Alan",   "Adam");       // skupina 4 (před Bílým)
        var subOnly  = await PersonAsync("GrpSubOnly",  "Aida",   "Aaron");      // skupina 5 (jen subsystém)

        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, vlastnik, "VLASTNIK_PROJEKTU");
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, pm, "PROJ_MAN");
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, admin1, "ADM_PROJ");
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, admin2, "ADM_PROJ");
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, subOnly, SubsystemRoleCodes.Lead);

        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9722);
        var detail = store.BuildJednaniDetail(meetingId);

        var expected = new[] { vlastnik, pm, admin1, admin2, subOnly };
        detail.Ucast.Select(x => x.OsobaId).Where(id => expected.Contains(id))
            .Should().ContainInOrder(expected,
                "skupina (Vlastník→PM→Admin→zbytek) přebíjí příjmení; uvnitř Admin skupiny Adam < Bílý");
        detail.AvailableParticipantCandidates.Select(x => x.OsobaId).Where(id => expected.Contains(id))
            .Should().ContainInOrder(expected, "kandidáti drží stejné řazení");

        // Varianta s EXPLICITNÍ účastí (Ucast řádky) — normal path musí řadit stejně.
        var presentStateId = await dbContext.CiselnikStavuUcasti
            .OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        foreach (var osobaId in new[] { subOnly, admin2, vlastnik, admin1, pm }) // schválně rozházené
        {
            dbContext.Ucast.Add(new UcastEntity
            {
                JednaniId = meetingId,
                OsobaId = osobaId,
                StavUcastiId = presentStateId
            });
        }
        await dbContext.SaveChangesAsync();

        var detailWithUcast = store.BuildJednaniDetail(meetingId);
        detailWithUcast.Ucast.Select(x => x.OsobaId).Where(id => expected.Contains(id))
            .Should().ContainInOrder(expected, "explicitní účast řadí stejným triem (priorita z membership lookup)");
    }
```
(Gestor záměrně vynechán — mezera ve skupinách je legální; test tak nekříží s dostupností GEST kódu v integr. seedu. Pokud `EnsureActiveProjectRoleAssignmentAsync` role kód v ciselniku nenajde a hodí, seed roli doplň dle vzoru helperu — STOP jen když chybí mechanismus.)

- [ ] **Step 2: Run — FAIL** (současné pořadí: příjmení globálně → Aaron první).
`dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~ByRoleGroupThenSurname" --nologo -v q`

### Task 2: GroupPriority (GREEN)

**Files:**
- Modify: `PmTracker.Web/Services/MeetingService.DetailQueries.cs`

**Interfaces:**
- Produces: `ActiveProjectMembershipRow(..., int GroupPriority, ...)`; `static int ResolveProjectRoleGroupPriority(string? roleKod)`.

- [ ] **Step 1: Record + resolver**

```csharp
    private sealed record ActiveProjectMembershipRow(
        int OsobaId,
        string Osoba,
        string Prijmeni,
        string Jmeno,
        string? Email,
        bool HasNonHostProjectRole,
        bool HasSubsystemRole,
        int GroupPriority,
        IReadOnlyList<string> AktivniRole);

    /// <summary>B6 (2026-07-09): pořadí role-skupin v účasti jednání (menší = dřív).</summary>
    private static int ResolveProjectRoleGroupPriority(string? roleKod) => roleKod switch
    {
        _ when Ci.Equals(roleKod, "VLASTNIK_PROJEKTU") => 1,
        _ when Ci.Equals(roleKod, "GEST") => 2,
        _ when Ci.Equals(roleKod, "PROJ_MAN") => 3,
        _ when Ci.Equals(roleKod, "ADM_PROJ") => 4,
        _ => 5
    };
```

- [ ] **Step 2: Fragmenty + group** — v `BuildActiveProjectMembershipRowsAsync`: do projektového fragment selectu přidat `GroupPriority = ResolveProjectRoleGroupPriority(item.RoleKod)`, do subsystémového `GroupPriority = 5`; v group-by:

```csharp
                return new ActiveProjectMembershipRow(
                    group.Key,
                    first.Osoba,
                    first.Prijmeni,
                    first.Jmeno,
                    first.Email,
                    group.Any(item => item.HasNonHostProjectRole),
                    group.Any(item => item.HasSubsystemRole),
                    group.Min(item => item.GroupPriority),
                    group.Select(item => item.RoleLabel) /* … beze změny … */);
            })
            // B6: skupina dle role → příjmení → jméno.
            .OrderBy(item => item.GroupPriority)
            .ThenBy(item => item.Prijmeni, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Jmeno, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
```

- [ ] **Step 3: Legacy fallback** (`BuildLegacyMeetingAttendance`) — OrderBy trojice:
```csharp
            .OrderBy(item => item.GroupPriority)
            .ThenBy(item => item.Prijmeni, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Jmeno, StringComparer.CurrentCultureIgnoreCase)
```

- [ ] **Step 4: Normal attendance** — v `BuildMeetingAttendanceAsync` doplnit lookup priority:
```csharp
        var priorityByOsoba = membershipRows.ToDictionary(x => x.OsobaId, x => x.GroupPriority);
```
a řazení participantIds:
```csharp
        return participantIds
            .OrderBy(osobaId => priorityByOsoba.GetValueOrDefault(osobaId, 5))
            .ThenBy(osobaId => people.GetValueOrDefault(osobaId)?.Prijmeni ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(osobaId => people.GetValueOrDefault(osobaId)?.Jmeno ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
```
(Kandidáti bez re-sortu konzumují rows — beze změny.)

- [ ] **Step 5: Build + test — PASS**; regrese `MeetingAttendanceRolesDataStoreTests` celé + `MeetingTasksDataStoreTests` + Api `MeetingAttendanceRenderTests|JednaniControllerTests`.

- [ ] **Step 6: Živě (dev):** /Jednani/Detail/2 → pořadí: Pavel Admin + Jana Testerová (PM, dle příjmení Admin→Testerová), pak Marie Analytická + Karel Vývojář (zbytek, dle příjmení). Screenshot do reportu.
