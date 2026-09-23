# Tisk jednání pro projektové a subsystémové role — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Umožnit tisk/export jednání každému, kdo smí projekt číst — opravit 403 pro projektové role (ADM_PROJ ap.) a doplnit export subsystémovým rolím.

**Architecture:** Deklarativní `[Authorize(Policy = "permission:export.*")]` zůstává (architektonické pravidlo — viz Global Constraints). Autorizační handler čte `projektId` z query stringu; tiskové URL jednání ho dnes nenese, takže handler padá do globální kontroly a projektové role dostanou 403. Fix = view přidá `projektId` do URL (autorizace) + controller ověří shodu předaného `projektId` se skutečným projektem jednání (anti-spoof) + seed doplní export klíče subsystémovým rolím.

**Tech Stack:** ASP.NET Core MVC (Razor), EF Core, SQL Server. Testy: xUnit + FluentAssertions; Api testy přes `ApiSqlFixture` (reálná SQL DB, impersonace přes `?asUser={osobaId}`).

## Global Constraints

- **Export autorizace MUSÍ zůstat deklarativní** `[Authorize(Policy = "permission:export.*")]`. Žádná body-level `CurrentUserContext.HasPermission(PermissionKeys.ExportPdf…)` kontrola — vynucuje `PolicyAttributeMigrationTests.ExportController_ShouldUsePerEntityAuthorizePolicyAttributes`.
- **Nemazat `[Authorize(Policy)]` atributy** u žádné export akce — vynucují `ExportAuthzTests` + `PolicyAttributeMigrationTests`.
- **Seed → prod DB:** `PermissionSeeder.SeedAsync` běží při startu (`Program.cs:62`) a je **idempotentní upsert** (přidá chybějící `AuthzRolePermissions` řádek, existující updatuje; nikdy nemaže). Přidané mapování se doseeduje i do existující prod DB při příštím startu. Offline deployment = žádná EF migrace.
- **Handler čte `projektId` z Route → Query → Form** (`PermissionAuthorizationHandler`). Route `Jednani/{jednaniId:int}/Tisk` nemá `projektId` → musí přijít v query.
- **`ScopeMode.All`** pro projektové/subsystémové role = grant platí pro projekty/subsystémy, kde osoba roli drží (materializuje se do `PerProjectPermissions` / `PerSubsystemPermissions`, ne do `GlobalPermissions`).
- **Baseline:** dle `memory/project_pre_existing_test_failures.md` existují orthogonální pre-existing selhání (F4 authz / meetings / SD). Neřešit je v rámci tohoto plánu — porovnávat proti baseline před změnou.

---

### Task 1: Tiskové odkazy jednání nesou `projektId` (opraví reportovaný 403)

Skutečný user-facing bug: tlačítko „Tisk" v detailu jednání generuje URL bez `projektId`, takže projektové role dostanou 403. Tlačítko není nijak gatované (vždy se renderuje).

**Files:**
- Modify: `PmTracker.Web/Views/Jednani/Detail.cshtml:10-11`
- Test: `PmTracker.Tests.Api/Controllers/ExportControllerTests.cs` (nový `[Fact]`)

**Interfaces:**
- Consumes: `Model.ProjektId` (na `JednaniDetailViewModel`, už používáno na `Detail.cshtml:44`); route `GET /Jednani/Detail/{id}?asUser={osobaId}` (vrací 200 HTML pro `AdminOsobaId`); `ExportControllerTests._fixture`, `_meetingSequence`, `Interlocked`.
- Produces: rendered `data-print-pdf-url` / `data-print-word-url` obsahující `?projektId={ProjektId}`.

- [ ] **Step 1: Napsat failing test**

Do `ExportControllerTests.cs` (nad `private async Task<ExportScenarioData> CreateExportScenarioAsync()`):

```csharp
[Fact]
public async Task MeetingDetail_PrintLinks_ShouldCarryProjektId_ForPerProjectAuthz()
{
    var projectId = await _fixture.EnsureProjectAsync("APIPRINTLINK");
    var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", Interlocked.Increment(ref _meetingSequence));

    using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
    var response = await client.GetAsync($"/Jednani/Detail/{meetingId}?asUser={_fixture.AdminOsobaId}");
    var html = await response.Content.ReadAsStringAsync();

    response.StatusCode.Should().Be(HttpStatusCode.OK, html);
    html.Should().Contain($"/Export/Jednani/{meetingId}/Tisk?projektId={projectId}");
    html.Should().Contain($"/Export/Jednani/{meetingId}/Word?projektId={projectId}");
}
```

- [ ] **Step 2: Spustit test — musí selhat**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ExportControllerTests.MeetingDetail_PrintLinks_ShouldCarryProjektId_ForPerProjectAuthz"`
Expected: FAIL — URL neobsahuje `projektId=` (dnes `…/Tisk?autoPrint=True`).

- [ ] **Step 3: Přidat `projektId` do URL v Detail.cshtml**

Nahradit řádky 10-11:

```csharp
    var meetingPrintUrl = Url.Action("JednaniTisk", "Export", new { jednaniId = Model.Jednani.Id, projektId = Model.ProjektId, autoPrint = true }) ?? $"/Export/Jednani/{Model.Jednani.Id}/Tisk?projektId={Model.ProjektId}&autoPrint=true";
    var meetingWordUrl = Url.Action("JednaniWord", "Export", new { jednaniId = Model.Jednani.Id, projektId = Model.ProjektId }) ?? $"/Export/Jednani/{Model.Jednani.Id}/Word?projektId={Model.ProjektId}";
```

- [ ] **Step 4: Spustit test — musí projít**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ExportControllerTests.MeetingDetail_PrintLinks_ShouldCarryProjektId_ForPerProjectAuthz"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Views/Jednani/Detail.cshtml PmTracker.Tests.Api/Controllers/ExportControllerTests.cs
git commit -m "fix(export): tiskové odkazy jednání nesou projektId (per-projekt authz)"
```

---

### Task 2: Anti-spoof guard + `projektId` param na Jednani export akcích, thread do redirectů

Autorizace už funguje díky Tasku 1 (handler čte `projektId` z query). Task 2 přidává obrannou vrstvu: akce ověří, že předaný `projektId` odpovídá skutečnému projektu jednání (jinak nelze autorizovat proti mnou spravovanému projektu a tisknout cizí jednání). Kompat redirecty `Dialog`/`Pdf` musí `projektId` protáhnout.

**Files:**
- Modify: `PmTracker.Web/Controllers/ExportController.cs` — `JednaniTisk` (106-124), `JednaniWord` (126-144), `Dialog` (176-189), `Pdf` (191-202)
- Test: `PmTracker.Tests.Api/Controllers/ExportControllerTests.cs` (nový `[Fact]` + úprava 2 existujících redirect testů)

**Interfaces:**
- Consumes: `_meetingService.GetMeetingProjectIdAsync(jednaniId, ct)` → `int?`; `EnsureProjectReadableAsync(int, ct)` → `IActionResult?`; `PdfExportRequestViewModel.ProjektId` / `.JednaniId`.
- Produces: `JednaniTisk(int jednaniId, int projektId, bool autoPrint, ct)`, `JednaniWord(int jednaniId, int projektId, ct)`; redirect URL `/Export/Jednani/{id}/Tisk?projektId={pid}&autoPrint=…`.

- [ ] **Step 1: Napsat failing test (anti-spoof)**

Do `ExportControllerTests.cs`:

```csharp
[Fact]
public async Task JednaniTisk_ShouldReturnNotFound_WhenProjektIdDoesNotMatchMeeting()
{
    var projectA = await _fixture.EnsureProjectAsync("APISPOOFA");
    var projectB = await _fixture.EnsureProjectAsync("APISPOOFB");
    var meetingInA = await _fixture.CreateMeetingAsync(projectA, "OPEN", Interlocked.Increment(ref _meetingSequence));

    using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
    // AdminOsobaId má globální export → policy projde pro projektId=B; guard musí odmítnout B != A.
    var response = await client.GetAsync($"/Export/Jednani/{meetingInA}/Tisk?projektId={projectB}&asUser={_fixture.AdminOsobaId}");

    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}
```

- [ ] **Step 2: Spustit test — musí selhat**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ExportControllerTests.JednaniTisk_ShouldReturnNotFound_WhenProjektIdDoesNotMatchMeeting"`
Expected: FAIL — dnes vrací 200 (žádný guard; `projektId` z query se ignoruje, tělo pracuje se skutečným projektem A).

- [ ] **Step 3: Přidat `projektId` param + guard do `JednaniTisk` a `JednaniWord`**

`JednaniTisk` (106-124) — nová signatura + guard:

```csharp
    [HttpGet("Jednani/{jednaniId:int}/Tisk")]
    [Authorize(Policy = "permission:export.pdf.jednani")]
    public async Task<IActionResult> JednaniTisk(int jednaniId, int projektId, bool autoPrint = true, CancellationToken ct = default)
    {
        var meetingProjectId = await _meetingService.GetMeetingProjectIdAsync(jednaniId, ct);
        if (meetingProjectId is null || meetingProjectId.Value != projektId)
        {
            return NotFound();
        }

        var accessCheck = await EnsureProjectReadableAsync(projektId, ct);
        if (accessCheck is not null)
        {
            return accessCheck;
        }

        var model = await _exportTemplateUseCase.BuildMeetingTemplateAsync(jednaniId, CurrentUserContext, autoPrint, ct);
        return View("~/Views/Export/PdfTemplate.cshtml", model);
    }
```

`JednaniWord` (126-144) — stejný vzor:

```csharp
    [HttpGet("Jednani/{jednaniId:int}/Word")]
    [Authorize(Policy = "permission:export.word.jednani")]
    public async Task<IActionResult> JednaniWord(int jednaniId, int projektId, CancellationToken ct = default)
    {
        var meetingProjectId = await _meetingService.GetMeetingProjectIdAsync(jednaniId, ct);
        if (meetingProjectId is null || meetingProjectId.Value != projektId)
        {
            return NotFound();
        }

        var accessCheck = await EnsureProjectReadableAsync(projektId, ct);
        if (accessCheck is not null)
        {
            return accessCheck;
        }

        var model = await _exportTemplateUseCase.BuildMeetingTemplateAsync(jednaniId, CurrentUserContext, autoPrint: false, ct);
        return BuildWordResult(model);
    }
```

- [ ] **Step 4: Protáhnout `projektId` do redirectů `Dialog` a `Pdf`**

V `Dialog` (183-186) nahradit řádek redirectu na jednání:

```csharp
        if (jednaniId.HasValue)
        {
            return RedirectToAction(nameof(JednaniTisk), new { jednaniId = jednaniId.Value, projektId, autoPrint });
        }
```

V `Pdf` (196-199) nahradit řádek redirectu na jednání:

```csharp
        if (request.JednaniId.HasValue)
        {
            return RedirectToAction(nameof(JednaniTisk), new { jednaniId = request.JednaniId.Value, projektId = request.ProjektId, autoPrint = true });
        }
```

- [ ] **Step 5: Upravit 2 existující redirect testy (nově obsahují projektId)**

V `ExportControllerTests.cs`:

`Dialog_ShouldRedirectToMeetingPrint_WhenMeetingIdIsProvided` — nahradit assert (byl `?autoprint=false`):

```csharp
        GetLocation(response).ToLowerInvariant().Should().Be($"/export/jednani/{data.MeetingId}/tisk?projektid={data.ProjectId}&autoprint=false");
```

`Pdf_ShouldRedirectToMeetingPrint_WhenMeetingIdIsProvided` — nahradit assert (byl `?autoprint=true`):

```csharp
        GetLocation(response).ToLowerInvariant().Should().Be($"/export/jednani/{data.MeetingId}/tisk?projektid={data.ProjectId}&autoprint=true");
```

- [ ] **Step 6: Spustit relevantní testy — musí projít**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ExportControllerTests"`
Expected: PASS — včetně nového anti-spoof testu, upravených redirect testů a existujícího `ReadEndpoints_ShouldReturnExpectedContentTypes` (globální admin bez projektId → 200 dál platí).

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Controllers/ExportController.cs PmTracker.Tests.Api/Controllers/ExportControllerTests.cs
git commit -m "fix(export): anti-spoof guard + projektId na Jednani tisku/wordu + redirect threading"
```

---

### Task 3: Subsystémové role dostanou export (read = print)

Subsystémoví vedoucí (VEDOUCI_SUBSYSTEMU, ZASTUPCE_VEDOUCIHO_SUBSYSTEMU, METODIK_SUBSYSTEMU) smí projekt číst, ale v redesignu 2026-04-23 byli z exportu vynecháni. Doplnit 6 export klíčů každé roli. Přes MEDIUM-1 propagaci se dostanou do `PerProjectPermissions` → tisk projektu i jednání funguje.

**Files:**
- Modify: `PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs` (subsystémové bloky ~631-672 + počty v komentářích)
- Modify: `PmTracker.Tests.Unit/Authorization/SubsystemRolePermissionMatrixTests.cs` (očekávaná pole)
- Create: `docs/known-issues/2026-07-14-subsystem-export-print.md` (changelog odchylky od target matice)
- Test: `PmTracker.Tests.Api/Controllers/ExportControllerTests.cs` (nový `[Fact]`)

**Interfaces:**
- Consumes: `RoleActionSeedItem(string RoleKod, string ActionKlic, ScopeMode, bool IsAllowed)` (record používaný v `RoleMappings`); `_fixture.EnsureSubsystemLeadAsync(projectId, subsystemId, osobaId)` (přiřadí VEDOUCI_SUBSYSTEMU).
- Produces: 18 nových `RoleMappings` řádků; aktualizovaná matice testů.

- [ ] **Step 1: Napsat failing Api test (subsystémový vedoucí tiskne jednání)**

Do `ExportControllerTests.cs`:

```csharp
[Fact]
public async Task JednaniTisk_ShouldSucceed_ForSubsystemLead_WithProjektId()
{
    var leadId = await _fixture.EnsurePersonAsync("ApiExportSubLead");
    var projectId = await _fixture.EnsureProjectAsync("APISUBEXP");
    var subsystemId = await _fixture.EnsureSubsystemAsync("APISUBEXPS", leadId);
    await _fixture.EnsureSubsystemLeadAsync(projectId, subsystemId, leadId);
    var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", Interlocked.Increment(ref _meetingSequence));

    using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
    var response = await client.GetAsync($"/Export/Jednani/{meetingId}/Tisk?projektId={projectId}&asUser={leadId}");

    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

- [ ] **Step 2: Spustit test — musí selhat**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ExportControllerTests.JednaniTisk_ShouldSucceed_ForSubsystemLead_WithProjektId"`
Expected: FAIL — 403 (subsystémová role dnes nemá `export.pdf.jednani`, policy short-circuit).

- [ ] **Step 3: Aktualizovat matici v unit testu (RED před seedem)**

V `SubsystemRolePermissionMatrixTests.cs` do `SubsystemLeadTargetKeys` (pole na řádcích 22-31) přidat 6 klíčů:

```csharp
        "export.pdf.jednani", "export.pdf.projekt", "export.pdf.ukol",
        "export.word.jednani", "export.word.projekt", "export.word.ukol",
```

A do očekávaného pole v `METODIK_SUBSYSTEMU_ShouldHaveComments` (řádky 50-56) přidat stejných 6 klíčů.

- [ ] **Step 4: Spustit unit matici — musí selhat**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~SubsystemRolePermissionMatrixTests"`
Expected: FAIL — seed zatím export klíče nemá.

- [ ] **Step 5: Doplnit export klíče do seedu**

V `PermissionSeedConfiguration.cs` za poslední klíč každé subsystémové role přidat 6 řádků. Za `new("VEDOUCI_SUBSYSTEMU", "schedule.preview", ScopeMode.All, true),` (~645):

```csharp
        new("VEDOUCI_SUBSYSTEMU", "export.pdf.projekt", ScopeMode.All, true),
        new("VEDOUCI_SUBSYSTEMU", "export.pdf.jednani", ScopeMode.All, true),
        new("VEDOUCI_SUBSYSTEMU", "export.pdf.ukol", ScopeMode.All, true),
        new("VEDOUCI_SUBSYSTEMU", "export.word.projekt", ScopeMode.All, true),
        new("VEDOUCI_SUBSYSTEMU", "export.word.jednani", ScopeMode.All, true),
        new("VEDOUCI_SUBSYSTEMU", "export.word.ukol", ScopeMode.All, true),
```

Za `new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "schedule.preview", ScopeMode.All, true),`:

```csharp
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "export.pdf.projekt", ScopeMode.All, true),
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "export.pdf.jednani", ScopeMode.All, true),
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "export.pdf.ukol", ScopeMode.All, true),
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "export.word.projekt", ScopeMode.All, true),
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "export.word.jednani", ScopeMode.All, true),
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "export.word.ukol", ScopeMode.All, true),
```

Za `new("METODIK_SUBSYSTEMU", "search.index", ScopeMode.All, true)` (přidej čárku za předchozí řádek):

```csharp
        new("METODIK_SUBSYSTEMU", "export.pdf.projekt", ScopeMode.All, true),
        new("METODIK_SUBSYSTEMU", "export.pdf.jednani", ScopeMode.All, true),
        new("METODIK_SUBSYSTEMU", "export.pdf.ukol", ScopeMode.All, true),
        new("METODIK_SUBSYSTEMU", "export.word.projekt", ScopeMode.All, true),
        new("METODIK_SUBSYSTEMU", "export.word.jednani", ScopeMode.All, true),
        new("METODIK_SUBSYSTEMU", "export.word.ukol", ScopeMode.All, true),
```

Aktualizovat počty v komentářích: `VEDOUCI_SUBSYSTEMU (14 klíčů…` → `(20 klíčů…`, `ZASTUPCE… (14 klíčů…` → `(20 klíčů…`, `METODIK_SUBSYSTEMU (9 klíčů…` → `(15 klíčů…`.

- [ ] **Step 6: Spustit matici + Api test — musí projít**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~SubsystemRolePermissionMatrixTests"`
Expected: PASS.
Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ExportControllerTests.JednaniTisk_ShouldSucceed_ForSubsystemLead_WithProjektId"`
Expected: PASS.

- [ ] **Step 7: Changelog odchylky od target matice**

Vytvořit `docs/known-issues/2026-07-14-subsystem-export-print.md`:

```markdown
# Subsystémové role: doplněn export/tisk (2026-07-14)

Redesign 2026-04-23 (authz-target-matrix.xlsx) vynechal export z rolí
VEDOUCI_SUBSYSTEMU / ZASTUPCE_VEDOUCIHO_SUBSYSTEMU / METODIK_SUBSYSTEMU.
Rozhodnutí vlastníka: kdo smí projekt číst, smí tisknout. Doplněno 6 export
klíčů (export.pdf.{projekt,jednani,ukol} + export.word.{…}) každé roli.

Zdroj pravdy v kódu: PermissionSeedConfiguration.RoleMappings +
SubsystemRolePermissionMatrixTests. Binární authz-target-matrix.xlsx je nutné
ručně srovnat při příští revizi matice.
```

- [ ] **Step 8: Commit**

```bash
git add PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs PmTracker.Tests.Unit/Authorization/SubsystemRolePermissionMatrixTests.cs PmTracker.Tests.Api/Controllers/ExportControllerTests.cs docs/known-issues/2026-07-14-subsystem-export-print.md
git commit -m "feat(authz): subsystémové role dostávají export/tisk (read = print)"
```

---

### Task 4: Regresní ověření celé autorizační sady

Doplněné seed granty mohou rozbít jiné maticové/coverage testy. Ověřit a případně srovnat.

**Files:**
- Ověřit (bez očekávané změny, případně srovnat počty): `PmTracker.Tests.Unit/Authorization/*`

- [ ] **Step 1: Build**

Run: `dotnet build PmTracker.Web`
Expected: 0 errors.

- [ ] **Step 2: Spustit celou Authorization unit sadu**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~Authorization"`
Expected: PASS. Pokud selže test počítající CELKOVÝ počet `RoleMappings` nebo per-role počty (např. `ExtendedRoleMatrixTests`, `RoleSeedCoverageTests`, `SeedSourceOfTruthTests`, `PerActionKeyCoverageTests`), srovnat očekávané hodnoty o +18 (celkem) resp. +6 (per subsystémová role). NEupravovat asserty, které testují SÉMANTIKU (jen počty).

- [ ] **Step 3: Spustit dotčené Api + Unit export/policy testy**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ExportControllerTests"`
Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ExportAuthz|FullyQualifiedName~PolicyAttributeMigration"`
Expected: PASS (atributy zachovány, žádná body kontrola → tyto testy nedotčeny).

- [ ] **Step 4: Porovnat proti baseline pre-existing selhání**

Zkontrolovat, že jakékoli zbylé červené testy odpovídají `memory/project_pre_existing_test_failures.md` (F4 authz / meetings / SD) a NEsouvisí s exportem/tiskem. Pokud ano → nechat, nejsou součástí tohoto plánu.

- [ ] **Step 5: Commit (jen pokud Step 2 vyžadoval srovnání počtů)**

```bash
git add PmTracker.Tests.Unit/Authorization/
git commit -m "test(authz): srovnat počty klíčů po doplnění subsystémového exportu"
```

---

## Self-Review

**Spec coverage:**
- Bug 1 (projektové role, reportovaný 403) → Task 1 (view link) + Task 2 (anti-spoof/param). ✓
- Bug 2 (subsystémové role bez exportu) → Task 3. ✓
- Word export zahrnut → Task 1 (word URL), Task 2 (`JednaniWord` param), Task 3 (word klíče v seedu). ✓
- Zachování deklarativní policy → Global Constraints + Task 2 nechává atributy. ✓
- Seed → prod DB (idempotent upsert) → Global Constraints. ✓
- Dokumentace odchylky → Task 3 Step 7. ✓

**Placeholder scan:** Žádné TBD/TODO; každý krok má konkrétní kód/příkaz/očekávaný výstup. ✓

**Type consistency:** `GetMeetingProjectIdAsync` → `int?` (guard přes `.HasValue`/`.Value`), `EnsureProjectReadableAsync` → `IActionResult?`, `RoleActionSeedItem(RoleKod, ActionKlic, ScopeMode, IsAllowed)`, `EnsureSubsystemLeadAsync(projectId, subsystemId, osobaId)`, `_meetingSequence`/`Interlocked.Increment` — vše ověřeno proti zdroji. ✓
