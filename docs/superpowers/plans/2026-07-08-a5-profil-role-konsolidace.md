# A5 — Profil: konsolidace rolí a práv do karet per instance role — Implementation Plan

> **For agentic workers:** Exekuce INLINE. **Commity DRŽET.**

**Goal:** Jedna sekce „Moje role a práva" s rozklikávacími kartami per instance role (Typ · Role · Projekt · Subsystém · N akcí); sekce „Odvozená práva" zaniká; Identita ukazuje počet rolí.

**Architecture (upřesnění vůči spec — menší blast radius):** Spec navrhovala rozšířit projekci grantů. Granty (`PermissionGrantViewModel` z `UserAuthorizationAuditSnapshotBuilder`) jsou ale **grupované per permission key** — identita role instance se v nich ztrácí a VM sdílí Settings/Resolver (velký dopad). Proto místo toho ProfileService dostane **dvě přímé dotazy per instance** (projektové + subsystémové role) podle join vzoru `UserContextResolver.LoadDbDrivenSubsystemRoleGrantsAsync` (ObsazeniSubsystemuProjektu × CiselnikRoliSubsystemu.AuthzRoleId × AuthzRolePermissions × AuthzPermissions; projektová analogie přes ObsazeniProjektu × CiselnikRoliProjektu.AuthzRoleId). `PermissionGrantViewModel`, audit builder ani resolver se **nemění**. Aplikační role se mapují ze stávajícího `BuildProfilRolePravaAsync`.

**Tech Stack:** C# (EF Core LINQ), Razor, Integration + Api testy.

## Global Constraints
- Pozor na SubsystemId ↔ ProjektSubsystemId mismatch (memory `project_subsystem_lead_comment_authz`) — subsystém identifikuj přes join ProjektSubsystemy→Subsystemy.
- Žádná změna výpočtu oprávnění (AuthorizationSnapshot) — čistě prezentační dotazy.
- Commity držené.

---

### Task 1: VM instancí + mapování aplikačních rolí

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/ProfilViewModels.cs`
- Modify: `PmTracker.Web/Services/Profile/ProfileService.PageQueries.cs`
- Test: `PmTracker.Tests.Integration/DataStore/ProfileRoleInstancesDataStoreTests.cs` (create)

**Interfaces:**
- Produces:
```csharp
public sealed class ProfilRoleInstanceViewModel
{
    public required string TypRole { get; init; }        // "Aplikační" | "Projektová" | "Subsystémová"
    public required string RoleKod { get; init; }
    public required string RoleNazev { get; init; }
    public string? Popis { get; init; }
    public string? ProjektZkratka { get; init; }          // null u aplikačních
    public string? ProjektNazev { get; init; }
    public string? SubsystemKod { get; init; }            // jen subsystémové
    public string? SubsystemNazev { get; init; }
    public required IReadOnlyList<ProfilRoleAkceViewModel> Akce { get; init; }
}
```
`ProfilPageViewModel`: `MojeRole : IReadOnlyList<ProfilRoleInstanceViewModel>`; **smazat** `OdvozenaPrava` + `ProfilOdvozenePravoViewModel` + `ProfilRolePravaViewModel` (nahrazeno instancí; `ProfilRoleAkceViewModel` zůstává).
- Consumes: `BuildProfilRolePravaAsync` interní dotazy (přepsat návratový typ na instance s `TypRole="Aplikační"`).

- [ ] **Step 1: Failing Integration test**

```csharp
using FluentAssertions;
using PmTracker.Tests.Integration.TestInfrastructure;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>A5 (2026-07-08): profil skládá karty per instance role (Typ/Role/Projekt/Subsystém).</summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class ProfileRoleInstancesDataStoreTests
{
    private readonly SqlIntegrationFixture _fixture;
    public ProfileRoleInstancesDataStoreTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task BuildProfilPage_GroupsRolesPerInstance_WithProjectAndSubsystem()
    {
        var db = await _fixture.CreateDatabaseAsync("profile_role_instances");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ProfInstAdmin");
        var personId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ProfInstUser");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "PROFINST");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "PROFINST_SYS", adminId);

        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, personId, ProjectRoleCodes.ProjectOwner);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, personId);

        var user = IntegrationTestHelper.BuildUser(isSuperAdmin: false, visibleProjectIds: [projectId]);
        // BuildUser vrací CurrentUserContextViewModel — nastav OsobaId=personId dle helper API
        // (pokud BuildUser OsobaId neparametrizuje, použij with-expression / init kopii dle vzoru okolních testů).
        var page = store.BuildProfilPage(user with { OsobaId = personId }, projektId: null);

        page.MojeRole.Should().Contain(x => x.TypRole == "Projektová" && x.ProjektZkratka == "PROFINST");
        var sub = page.MojeRole.SingleOrDefault(x => x.TypRole == "Subsystémová");
        sub.Should().NotBeNull();
        sub!.ProjektZkratka.Should().Be("PROFINST");
        sub.SubsystemKod.Should().Be("PROFINST_SYS");
        sub.Akce.Should().NotBeEmpty("instance nese akce z AuthzRolePermissions napojené role");
    }
}
```
(Pozn.: `CurrentUserContextViewModel` je class s `init` — pokud `with` nejde, postav objekt ručně dle `BuildUser` vzoru. Předpoklad: seed lookup role mají `AuthzRoleId` vazbu — stejný předpoklad jako produkce/resolver; pokud integr. seed vazbu nemá, test ji doplní UPDATE-em `ciselnik_roli_*`.)

- [ ] **Step 2: Run — FAIL kompilací** (`MojeRole` typ, `TypRole` neexistuje).

- [ ] **Step 3: VM změny** (kód výše v Interfaces) + `ProfilPageViewModel.MojeRole` typ + smazání `OdvozenaPrava`.

- [ ] **Step 4: ProfileService.PageQueries.cs**

`BuildProfilPageAsync`:
```csharp
        return new ProfilPageViewModel
        {
            Uzivatel = currentUser,
            MojeRole = await BuildProfilRoleInstancesAsync(currentUser.OsobaId, ct)
        };
```
Nová kompozice:
```csharp
    private async Task<IReadOnlyList<ProfilRoleInstanceViewModel>> BuildProfilRoleInstancesAsync(int osobaId, CancellationToken ct)
    {
        var app = await BuildAppRoleInstancesAsync(osobaId, ct);          // dnešní BuildProfilRolePravaAsync přemapovaný
        var project = await BuildProjectRoleInstancesAsync(osobaId, ct);  // nový dotaz
        var subsystem = await BuildSubsystemRoleInstancesAsync(osobaId, ct); // nový dotaz
        return app
            .Concat(project)
            .Concat(subsystem)
            .ToList(); // pořadí: Aplikační → Projektová → Subsystémová (řazení uvnitř builderů)
    }
```
`BuildAppRoleInstancesAsync` = tělo dnešního `BuildProfilRolePravaAsync` s výstupem `ProfilRoleInstanceViewModel { TypRole = "Aplikační", ProjektZkratka = null, … }` (Akce mapping beze změny).

`BuildProjectRoleInstancesAsync` (join vzor resolveru, per instance = (role, projekt)):
```csharp
    private async Task<IReadOnlyList<ProfilRoleInstanceViewModel>> BuildProjectRoleInstancesAsync(int osobaId, CancellationToken ct)
    {
        var rows = await (
            from assignment in dbContext.ObsazeniProjektu.AsNoTracking()
            where assignment.OsobaId == osobaId && !assignment.DatumOdebrani.HasValue
            join lookupRole in dbContext.CiselnikRoliProjektu.AsNoTracking() on assignment.RoleId equals lookupRole.Id
            join projekt in dbContext.Projekty.AsNoTracking() on assignment.ProjektId equals projekt.Id
            join authzRole in dbContext.AuthzRoles.AsNoTracking() on lookupRole.AuthzRoleId equals authzRole.Id
            where authzRole.IsActive
            join rp in dbContext.AuthzRolePermissions.AsNoTracking() on authzRole.Id equals rp.RoleId
            join permission in dbContext.AuthzPermissions.AsNoTracking() on rp.PermissionId equals permission.Id
            where permission.IsActive
            select new
            {
                RoleKod = lookupRole.Kod,
                RoleNazev = lookupRole.Nazev,
                ProjektZkratka = projekt.Zkratka,
                ProjektNazev = projekt.CelyNazev,
                permission.Klic,
                PermissionNazev = permission.Nazev,
                rp.IsAllowed
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => new { r.RoleKod, r.RoleNazev, r.ProjektZkratka, r.ProjektNazev })
            .OrderBy(g => g.Key.ProjektZkratka, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(g => g.Key.RoleNazev, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new ProfilRoleInstanceViewModel
            {
                TypRole = "Projektová",
                RoleKod = g.Key.RoleKod,
                RoleNazev = g.Key.RoleNazev,
                ProjektZkratka = g.Key.ProjektZkratka,
                ProjektNazev = g.Key.ProjektNazev,
                Akce = g
                    .GroupBy(x => x.Klic)
                    .OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase)
                    .Select(x => new ProfilRoleAkceViewModel
                    {
                        PermissionKlic = x.Key,
                        PermissionNazev = x.First().PermissionNazev,
                        IsAllowed = x.Any(y => y.IsAllowed),
                        ScopeSummary = g.Key.ProjektZkratka
                    })
                    .ToList()
            })
            .ToList();
    }
```
`BuildSubsystemRoleInstancesAsync` analogicky s joiny `ObsazeniSubsystemuProjektu × CiselnikRoliSubsystemu × ProjektSubsystemy (!DatumOdebrani) × Projekty × Subsystemy × AuthzRoles×RolePermissions×Permissions`, klíč grupy `(RoleKod, RoleNazev, ProjektZkratka, ProjektNazev, SubsystemKod, SubsystemNazev)`, `TypRole="Subsystémová"`, `ScopeSummary = $"{ProjektZkratka} / {SubsystemKod}"`.

Smazat: `BuildProfilOdvozenaPravaAsync`, `BuildGrantSourceSummary`; z `ProfileService` ctor odstranit `IUserAuthorizationAuditSnapshotBuilder` (grep — jediné použití bylo tady; DI registrace zůstává pro Settings).

- [ ] **Step 5: Build + Integration test — PASS.** Pokud `lookupRole.AuthzRoleId` je nullable → přidat `where lookupRole.AuthzRoleId != null` (vzor resolveru).

### Task 2: View + Identita

**Files:**
- Modify: `PmTracker.Web/Views/Profil/Index.cshtml` (sekce ř. 26–27 Identita; 77–149 karty; 151–198 smazat)
- Modify: `PmTracker.Web/wwwroot/css/site.css` (summary meta)
- Test: `PmTracker.Tests.Api/Controllers/ProfilePageRenderTests.cs` (create/extend — grep, existuje-li)

- [ ] **Step 1: Failing Api test** — profil stránka: obsahuje `Typ role` značení (např. `data-role-typ="Subsystémová"`), NEobsahuje `Odvozená práva z projektu a subsystému`; Identita obsahuje `rolí` s číslem (regex `\d+ rol`).

```csharp
    [Fact]
    public async Task Profile_ShowsRoleInstanceCards_NoDerivedSection()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Profil?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK, html);
        html.Should().Contain("data-role-typ=");
        html.Should().NotContain("Odvozená práva z projektu a subsystému");
    }
```

- [ ] **Step 2: View** — karta (nahrazuje foreach v sekci `#moje-prava`):

```razor
        @foreach (var role in Model.MojeRole)
        {
            <details class="profile-role-card" data-role-typ="@role.TypRole">
                <summary class="profile-role-summary">
                    <span class="profile-role-summary-main">
                        <span class="profile-role-typ badge">@role.TypRole</span>
                        <span class="profile-role-name">@role.RoleNazev</span>
                        <code>@role.RoleKod</code>
                        @if (!string.IsNullOrWhiteSpace(role.ProjektZkratka))
                        {
                            <span class="profile-role-scope">Projekt: <strong>@role.ProjektZkratka</strong></span>
                        }
                        @if (!string.IsNullOrWhiteSpace(role.SubsystemKod))
                        {
                            <span class="profile-role-scope">Subsystém: <strong>@role.SubsystemKod</strong></span>
                        }
                    </span>
                    <span class="profile-role-summary-meta">@role.Akce.Count akcí</span>
                </summary>
                @* tělo: stávající tabulka Akce/Název/Přístup/Rozsah beze změny (role.Akce) *@
```
Sekci „Odvozená práva…" (ř. 151–198) smazat. Identita:
```razor
                <dt>Role</dt>
                <dd><a href="#moje-prava">@Model.MojeRole.Count rolí</a></dd>
```
CSS: `.profile-role-typ { … }`, `.profile-role-scope { color: var(--pm-text-muted); font-weight: 400; }` — do bloku profile-role stylů (grep `.profile-role-summary`).

- [ ] **Step 3: Run Api — PASS**; celé Unit+Api regrese (grep testy s `OdvozenaPrava` — smazat/aktualizovat: `grep -rn "OdvozenaPrava" PmTracker.Tests.*`).

- [ ] **Step 4: Vizuální kontrola** — Playwright screenshot `/Profil?asUser=1` (admin: aplikační role; dev seed user s projektovou rolí, je-li) do reportu.
