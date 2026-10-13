# Oprávnění zástupce vedoucího subsystému — implementační plán

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Provedení v tomto repu:** inline (superpowers:executing-plans), bez subagentů — preference uživatele.

**Goal:** Zástupce vedoucího subsystému (a stejně tak vedoucí, metodik, gestor, host, READ_ALL) může vyhledávat, vidí obsah tiketu u bubliny skutečnosti v okně jen pro čtení, v návrhu založení záznamu si otypuje tiket a formulář vyjádření dostane jen tam, kde ho server opravdu přijme.

**Architecture:** Pět samostatných oprav nad stávající seed-only RBAC architekturou. (1) Hledání hlídá jen přihlášení, rozsah výsledků dál řeší `ProjectVisibilityResolver`; klíč `search.index` zaniká i s migrací. (2) Nový GET endpoint okna tiketu jen pro čtení gatovaný stejně jako stránka záznamu (`CanAccessProject`); bublina nese režim `read`/`edit`, který určuje server — `edit` jen v editoru záznamu. (3) Endpoint jen pro typ tiketu pro editor návrhu. (4) Prezentace formuláře vyjádření se ptá stejné `CommentAuthorizationPolicy` jako server. (5) Stránka záznamu vybírá zdrojovou vazbu stejným pravidlem jako resolver editoru.

**Tech Stack:** ASP.NET Core 8 MVC + Razor, EF Core (SQL Server), ES moduly ve `wwwroot/js/modules`, xUnit + FluentAssertions + Moq (Unit), WebApplicationFactory nad SQL kontejnerem (Api), Playwright .NET (E2E).

**Spec:** analýza ze session 2026-10-12/13 (Playwright proklik pod rolí ZASTUPCE_VEDOUCIHO_SUBSYSTEMU, osoba 6 na lokální DB) + rozhodnutí uživatele 2026-10-13. Navazuje na:
- `docs/superpowers/specs/2026-09-17-vyhledavani-prestavba-design.md` §2.3 — „Jediné pravidlo: uživatel vidí záznam právě tehdy, když vidí projekt“.
- `docs/superpowers/specs/2026-04-21-servicedesk-vytezovani-vyjadreni-design.md` §5.2 — „Chat modal pro uživatele ve schéma 2 je read-only — vidí přiřazení, ale nemůže je měnit bez návrhu.“
- Rozhodnutí 2026-06-16 o komentářích vedoucího subsystému (jen vlastní subsystém, jen DRAFT jednání).

### Rozhodnutí uživatele (2026-10-13)

| # | Nález | Rozhodnutí |
|---|---|---|
| 1 | `/Search` vrací 403 všem bez globální role (policy `permission:search.index` bez `projektId` = globální kontrola) | Hledání bez klíče. `search.index` zrušit, výsledky filtruje `RecordSearchService`. |
| 2 | Klik na bublinu skutečnosti → `/Vyjadreni/Modal` 403 (klíč `vyjadreni.modal.open` mají jen editační role) | Nové okno **jen pro čtení** s obsahem tiketu, bez nového klíče — stačí právo vidět záznam. Otevírá se vždy ze stránky záznamu (i projektovému manažerovi). |
| 3 | `/ExterniOdkaz/Sync` 403 → v návrhu „Tiket se nepodařilo dohledat“ | Opravit (endpoint jen pro typ). |
| 4 | U záznamu cizího subsystému UI nabídne formulář vyjádření, server ho odmítne | Opravit (prezentace = stejná policy jako server). |
| 5 | Stránka záznamu bere u kroku 1 nejnovější vazbu, resolver editoru nejstarší | Opravit (sdílené pravidlo). |

## Global Constraints

- Autorizace výhradně přes klíče a `CurrentUserContext.CanAccessProject` — žádné porovnání kódu role (`NoHardcodedAuthzRolesTests`). V komentářích a commitech mluvit přes klíče, ne přes role.
- Role a klíče jen v `PermissionSeedConfiguration` (seed-only RBAC); zrušený klíč z DB uklízí idempotentní `db_upgrade_*.sql` podle vzoru `db_upgrade_1_4_1_drop_schedule_preview.sql`.
- Každý nový `db_upgrade_*.sql` má záznam v `db_check_applied_upgrades.sql`. Bootstrap seznam v `docs/technical/06-database-bootstrap-migrations.md` se mění jen pro skripty, které mění schéma nebo data ze `PMTracker_insert_sql` (authz klíče zakládá `PermissionSeeder` — proto tam 1_4_1 není a nebude ani 1_4_8).
- AJAX v nových/změněných JS voláních přes `appendCurrentAsUser` (`wwwroot/js/modules/navigationShared.js`).
- Api HTML testy se kotví na atributy a ASCII, ne na český text (Razor kóduje diakritiku na entity).
- Razor nevynechá `data-x="@null"` — podmíněné `data-*` atributy vykreslovat s hodnotou (`"true"`/`"false"`) nebo v `@if` větvi.
- Commity česky ve stylu `fix(oblast): …`, zakončené `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Pracuje se na větvi `fix/opravneni-zastupce-subsystemu`, po zelené verifikaci merge do lokálního `main`, push jen na pokyn.
- E2E sada má ~33 pre-existing selhání — regresi posuzovat porovnáním **jmen** proti baseline, ne počtem.
- Lokální spuštění Api/E2E testů vyžaduje běžící Colima + kontejner `pmtracker-sql`.

## Review Focus

1. **ServiceDesk nedostupný při otevření okna jen pro čtení** → okno se otevře s varováním a přiřazením kroků z lokálních vazeb, ne „HTTP 500“. Test: Task 3 `VyjadreniNahled_ServiceDeskNedostupny_UkazeVarovaniMisto500`.
2. **Podvržené id (externí vazba jiného záznamu, záznam cizího projektu)** → okno jen pro čtení vrátí 404, nic neprozradí. Testy: Task 3 `VyjadreniNahled_VazbaJinehoZaznamu_Vraci404`, `VyjadreniNahled_UzivatelBezPristupuKProjektu_Vraci404`.
3. **Záznam s PMP i PNF, krok 1** → stránka záznamu i editor ukážou totéž nejstarší datum. Testy: Task 2 `ResolveMatched_Krok1_VraciNejstarsi_IgnorujePreferred` + Api `DetailPage_Krok1SPmpIPnf_UkazeNejstarsiDatum`.
4. **Uživatel bez jakéhokoli projektu hledá** → 200 a prázdný výsledek, ne 403/500. Test: Task 1 `Suggest_UzivatelBezProjektu_VratiPrazdnyVysledek`.
5. **Zástupce v editoru návrhu harmonogramu klikne na bublinu** → vždy okno jen pro čtení (editační endpoint by navíc měnil vazby mimo schvalování návrhu). Test: Task 4 `NavrhHarmonogramu_Zastupce_BublinaVRezimuCteni`.

---

## Mapa souborů

| Soubor | Task | Odpovědnost změny |
|---|---|---|
| `PmTracker.Web/Controllers/SearchController.cs` | 1 | jen `[Authorize]` |
| `PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs` | 1 | pryč kategorie SEARCH, klíč a 12 mapování |
| `PmTracker.Web/Models/ViewModels/SecurityViewModels.cs` | 1 | pryč konstanta `SearchIndex` a definice v katalogu |
| `db_upgrade_1_4_8_drop_search_index.sql` (nový) | 1 | úklid klíče a kategorie v `authz.*` |
| `db_check_applied_upgrades.sql`, `db_upgrade_1_4_4_search_cleanup.sql` | 1 | kontrola 1_4_8; informativní výstup 1_4_4 |
| `PmTracker.Web/Services/Schedules/HarmonogramSkutecnostResolver.cs` | 2 | nová `ResolveMatched` (sdílené pravidlo výběru) |
| `PmTracker.Web/Services/ProjectService.RecordDetailPage.cs` | 2, 4 | výběr vazby přes `ResolveMatched`; vždy doplnit zdroj |
| `PmTracker.Web/Controllers/ZaznamyController.Partials.cs` | 3, 6 | akce `VyjadreniNahled`; async prezentace vyjádření |
| `PmTracker.Web/Services/ServiceDesk/VyjadreniModalViewModelBuilder.cs` | 3 | výpadek SD bez 500; typ z lokální vazby |
| `PmTracker.Web/Models/ViewModels/Vyjadreni/VyjadreniModalViewModel.cs` | 3 | `ServiceDeskNedostupny` |
| `PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml` | 3 | `data-chat-modal-mode`, varování výpadku, patička jen pro čtení |
| `PmTracker.Tests.Api/TestInfrastructure/FakeServiceDesk.cs` (nový), `ApiSqlFixture.cs` | 1, 3 | falešný SD, `ServiceDeskFactory`, `EnsureSubsystemRoleAsync`, `SeedTicketBindingAsync` |
| `PmTracker.Web/Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs` | 4 | `VyjadreniOknoUpravy` na bloku a buňce |
| `PmTracker.Web/Views/Shared/_ScheduleTable.cshtml`, `_ScheduleBlockManualCell.cshtml` | 4 | propsání režimu do `data-external-chat-mode` |
| `PmTracker.Web/Controllers/ZaznamyController.cs` | 4, 6 | editor nastaví `edit`; stránka záznamu bez podmínky |
| `PmTracker.Web/Services/IProjectService.cs`, `IRecordService.cs`, `RecordService.cs` | 4, 6 | pryč přetížení s `canOpenVyjadreni`; nová fasádní metoda komentářů |
| `PmTracker.Web/wwwroot/js/modules/vyjadreni/chatModal.js` | 4 | režim okna, `appendCurrentAsUser`, hláška 403 |
| `PmTracker.Web/Controllers/ExterniOdkazController.cs` + `Models/ViewModels/ExterniOdkaz/ExterniOdkazTypResponse.cs` (nový) | 5 | `GET /ExterniOdkaz/Typ` |
| `PmTracker.Web/wwwroot/js/modules/externiOdkaz/sync.js`, `css/components/externi-odkaz-card.css`, `Views/Projekty/_EditZaznamExternalPanel.cshtml` | 5 | typ přes nový endpoint, chyby ≠ „nenalezeno“, detail návrhu bez synchronizace |
| `PmTracker.Web/Services/ICommentService.cs`, `CommentService.cs` | 6 | `GetMeetingIdsAllowingNewCommentAsync` se sdíleným kontextem |
| `PmTracker.Web/Controllers/ProjektyController.cs`, `Models/ViewModels/Projekty/ProjektZaznamyTabViewModels.cs` | 6 | pryč mrtvé příznaky `CanAddComment` na kartě |

---

### Task 0: Větev

- [ ] **Step 1: Založit větev z čistého `main`**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
git status --short   # musí být prázdné
git checkout -b fix/opravneni-zastupce-subsystemu
```

---

### Task 1: Hledání bez klíče — zrušit `search.index`

**Files:**
- Modify: `PmTracker.Web/Controllers/SearchController.cs:10-12`
- Modify: `PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs:90, 213-217, 316, 391, 405, 464, 523, 582, 603, 616, 630, 650, 666`
- Modify: `PmTracker.Web/Models/ViewModels/SecurityViewModels.cs:123-124, 235-236`
- Create: `db_upgrade_1_4_8_drop_search_index.sql`
- Modify: `db_check_applied_upgrades.sql` (DECLARE blok za `@schedule_preview`, nový `INSERT @r` za `360`)
- Modify: `db_upgrade_1_4_4_search_cleanup.sql:17, 76, 85` (jen komentáře a informativní SELECT)
- Modify (testy): `PmTracker.Tests.Unit/Search/SearchControllerAuthzTests.cs` (přepis), `Search/SearchLegacyRemovedTests.cs:35`, `Search/ProjectVisibilityResolverTests.cs:96-100, 130`, `Authorization/ResolverSwapSafetyTests.cs:87, 117, 165`, `Authorization/SubsystemRolePermissionMatrixTests.cs:31, 58`, `Authorization/ReadAllRoleSeedTests.cs:40-41`, `Authorization/ProjectRolePermissionMatrixTests.cs:41, 82, 99`
- Create: `PmTracker.Tests.Unit/Search/SearchIndexKeyRemovalTests.cs`
- Modify: `PmTracker.Tests.Api/TestInfrastructure/ApiSqlFixture.cs` (`EnsureSubsystemRoleAsync`)
- Modify: `PmTracker.Tests.Api/Controllers/SearchEndpointsTests.cs`
- Docs: `docs/wiki/nastaveni-administrace/akce-permissions/index.md:26, 168-173`, `docs/technical/07-security-authz.md:56`, `docs/superpowers/specs/2026-09-17-vyhledavani-prestavba-design.md` §5, `docs/known-issues/authz-target-matrix.xlsx`

**Interfaces:**
- Produces: `ApiSqlFixture.EnsureSubsystemRoleAsync(int projectId, int subsystemId, int osobaId, string roleCode)` — používají Tasks 3, 4, 5, 6.

- [ ] **Step 1: Fixture — zobecnit přiřazení subsystémové role**

V `PmTracker.Tests.Api/TestInfrastructure/ApiSqlFixture.cs` nahraď tělo `EnsureSubsystemLeadAsync` (ř. 150–194) delegací a přesuň ho do nové metody:

```csharp
    /// <summary>
    /// A4 (2026-07-09): přiřadí osobě roli vedoucího subsystému v projektu (projekt_subsystemy +
    /// obsazeni_subsystemu_projektu). Potřebné pro lead-gated stránky (návrhy záznamů).
    /// </summary>
    public Task EnsureSubsystemLeadAsync(int projectId, int subsystemId, int osobaId)
        => EnsureSubsystemRoleAsync(projectId, subsystemId, osobaId, PmTracker.Web.Models.ViewModels.SubsystemRoleCodes.Lead);

    /// <summary>
    /// Přiřadí osobě libovolnou subsystémovou roli (kód z ciselnik_roli_subsystemu) v projektu.
    /// 2026-10-13: testy oprávnění zástupce vedoucího subsystému (SubsystemRoleCodes.DeputyLead).
    /// </summary>
    public async Task EnsureSubsystemRoleAsync(int projectId, int subsystemId, int osobaId, string roleCode)
    {
        await using var dbContext = CreateDbContext();

        var projectSubsystemId = await dbContext.ProjektSubsystemy
            .Where(x => x.ProjektId == projectId && x.SubsystemId == subsystemId && x.DatumOdebrani == null)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();
        if (!projectSubsystemId.HasValue)
        {
            var link = new ProjektSubsystemEntity
            {
                ProjektId = projectId,
                SubsystemId = subsystemId,
                Poradi = 1,
                DatumPrirazeni = DateTime.UtcNow
            };
            dbContext.ProjektSubsystemy.Add(link);
            await dbContext.SaveChangesAsync();
            projectSubsystemId = link.Id;
        }

        var roleId = await dbContext.CiselnikRoliSubsystemu
            .Where(x => x.Kod == roleCode)
            .Select(x => x.Id)
            .FirstAsync();

        var alreadyAssigned = await dbContext.ObsazeniSubsystemuProjektu
            .AnyAsync(x => x.ProjektSubsystemId == projectSubsystemId.Value
                && x.OsobaId == osobaId
                && x.RoleSubsystemuId == roleId
                && x.DatumOdebrani == null);
        if (!alreadyAssigned)
        {
            dbContext.ObsazeniSubsystemuProjektu.Add(new ObsazeniSubsystemuProjektuEntity
            {
                ProjektSubsystemId = projectSubsystemId.Value,
                OsobaId = osobaId,
                RoleSubsystemuId = roleId,
                DatumPrirazeni = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }
    }
```

- [ ] **Step 2: Napsat padající Api testy**

Do `PmTracker.Tests.Api/Controllers/SearchEndpointsTests.cs` přidej (`using System.Net;` a `using PmTracker.Web.Models.ViewModels;` doplň, pokud chybí):

```csharp
    [Fact]
    public async Task Suggest_ZastupceVedoucihoSubsystemu_NajdeZaznamSvehoProjektu()
    {
        // Regrese 89bb967: permission:search.index bez projektId = globální kontrola → 403 všem
        // bez globální role. Hledání hlídá jen přihlášení, rozsah dává viditelnost projektů.
        var ownerId = await _fixture.EnsurePersonAsync("ApiSrchOwner");
        var deputyId = await _fixture.EnsurePersonAsync("ApiSrchDeputy");
        var projectId = await _fixture.EnsureProjectAsync("APISRCHDEP");
        var foreignProjectId = await _fixture.EnsureProjectAsync("APISRCHCIZI");
        var subsystemId = await _fixture.EnsureSubsystemAsync("ApiSrchSub", ownerId);
        await _fixture.EnsureSubsystemRoleAsync(projectId, subsystemId, deputyId, SubsystemRoleCodes.DeputyLead);
        var ownRecordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "Zkousnihledani vlastni");
        var foreignRecordId = await _fixture.EnsureRecordAsync(foreignProjectId, ownerId, subsystemId, "U", "Zkousnihledani cizi");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Search/Suggest?q=Zkousnihledani&asUser={deputyId}");
        var json = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, json);
        json.Should().Contain($"\"zaznamId\":{ownRecordId}", "záznam vlastního projektu se najde");
        json.Should().NotContain($"\"zaznamId\":{foreignRecordId}", "cizí projekt filtruje ProjectVisibilityResolver");
    }

    [Fact]
    public async Task Suggest_UzivatelBezProjektu_VratiPrazdnyVysledek()
    {
        var loneId = await _fixture.EnsurePersonAsync("ApiSrchLone");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Search/Suggest?q=Zkousnihledani&asUser={loneId}");
        var json = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, json);
        json.Should().Contain("\"totalCount\":0", "kdo nevidí žádný projekt, nenajde nic — ale nedostane chybu");
    }

    [Fact]
    public async Task Index_ZastupceVedoucihoSubsystemu_StrankaSeVykresli()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiSrchIdxOwner");
        var deputyId = await _fixture.EnsurePersonAsync("ApiSrchIdxDeputy");
        var projectId = await _fixture.EnsureProjectAsync("APISRCHIDX");
        var subsystemId = await _fixture.EnsureSubsystemAsync("ApiSrchIdxSub", ownerId);
        await _fixture.EnsureSubsystemRoleAsync(projectId, subsystemId, deputyId, SubsystemRoleCodes.DeputyLead);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Search?q=Zkousnihledani&asUser={deputyId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
```

- [ ] **Step 3: Ověřit, že padají**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~SearchEndpointsTests"`
Expected: 3 nové testy „Neúspěšné“ s `Expected response.StatusCode to be HttpStatusCode.OK … but found HttpStatusCode.Forbidden`.

- [ ] **Step 4: Unit testy na zrušení klíče (padající)**

Přepiš `PmTracker.Tests.Unit/Search/SearchControllerAuthzTests.cs` celý:

```csharp
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using PmTracker.Web.Controllers;

namespace PmTracker.Tests.Unit.Search;

/// <summary>
/// Hledání je dostupné každému přihlášenému (rozhodnutí 2026-10-13). Co uživatel uvidí,
/// omezuje výhradně ProjectVisibilityResolver (spec 2026-09-17 §2.3). Klíč search.index byl
/// zrušen: jako globální klíč ho držely i projektové a subsystémové role, jenže bez projektId
/// se kontroloval jen globálně a hledání vracelo 403 všem kromě globálních rolí (89bb967).
/// </summary>
public sealed class SearchControllerAuthzTests
{
    [Fact]
    public void SearchController_HlidaJenPrihlaseni()
    {
        var authorize = typeof(SearchController)
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .ToList();

        authorize.Should().ContainSingle();
        authorize[0].Policy.Should().BeNull("o rozsahu výsledků rozhoduje viditelnost projektů, ne klíč");
    }

    [Fact]
    public void Akce_NemajiVlastniPermissionPolicy()
    {
        typeof(SearchController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetCustomAttributes<AuthorizeAttribute>())
            .Should().BeEmpty();
    }
}
```

Vytvoř `PmTracker.Tests.Unit/Search/SearchIndexKeyRemovalTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Search;

/// <summary>
/// 2026-10-13: klíč search.index zaniká (hledání hlídá jen přihlášení). Seed ani katalog ho
/// nesmí vracet zpět a DB uklízí idempotentní migrace 1_4_8 podle vzoru 1_4_1.
/// </summary>
public sealed class SearchIndexKeyRemovalTests
{
    private const string MigrationFile = "db_upgrade_1_4_8_drop_search_index.sql";

    [Fact]
    public void Seed_AniKatalog_KlicNeobsahuji()
    {
        File.ReadAllText(ResolvePath("PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs"))
            .Should().NotContain("search.index").And.NotContain("\"SEARCH\"");
        File.ReadAllText(ResolvePath("PmTracker.Web/Models/ViewModels/SecurityViewModels.cs"))
            .Should().NotContain("search.index");
    }

    [Fact]
    public void Migrace_MazeKlicVazbyAOsirelouKategorii()
    {
        var sql = File.ReadAllText(ResolvePath(MigrationFile));

        sql.Should().Contain("FROM authz.role_permission_projects");
        sql.Should().Contain("FROM authz.role_permissions");
        sql.Should().Contain("DELETE FROM authz.permissions WHERE klic = N'search.index'");
        sql.Should().Contain("c.kod = N'SEARCH'");
        sql.Should().Contain("NOT EXISTS (SELECT 1 FROM authz.permissions p WHERE p.category_id = c.id)",
            "kategorie se maže jen osiřelá — guard proti cizím klíčům");
        sql.Should().Contain("BEGIN TRANSACTION").And.Contain("COMMIT TRANSACTION");
    }

    [Fact]
    public void Migrace_JeVKontrolnimSkriptu()
    {
        File.ReadAllText(ResolvePath("db_check_applied_upgrades.sql"))
            .Should().Contain("db_upgrade_1_4_8_drop_search_index");
    }
}
```

V `PmTracker.Tests.Unit/Search/SearchLegacyRemovedTests.cs:35` nahraď řádek

```csharp
        seed.Should().Contain("search.index", "vyhledávání zůstává, klíč pro jeho použití taky");
```

řádkem

```csharp
        seed.Should().NotContain("search.index", "od 2026-10-13 hledání hlídá jen přihlášení (SearchIndexKeyRemovalTests)");
```

- [ ] **Step 5: Ověřit, že padají**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~SearchControllerAuthzTests|FullyQualifiedName~SearchIndexKeyRemovalTests|FullyQualifiedName~SearchLegacyRemovedTests"`
Expected: Neúspěšné — `SearchController_HlidaJenPrihlaseni` (Policy je `permission:search.index`), `Seed_AniKatalog_KlicNeobsahuji`, obě migrační (soubor neexistuje), `Seed_JizNeobsahujeKlicSearchReindex`.

- [ ] **Step 6: Controller**

`PmTracker.Web/Controllers/SearchController.cs` ř. 10–12 nahraď:

```csharp
// Hledání smí každý přihlášený (rozhodnutí 2026-10-13). Rozsah výsledků určuje výhradně
// ProjectVisibilityResolver uvnitř RecordSearchService (spec 2026-09-17 §2.3) — kdo nevidí
// žádný projekt, dostane prázdný výsledek. Dřívější policy permission:search.index se bez
// projektId vyhodnocovala globálně a vracela 403 všem projektovým a subsystémovým rolím.
[Authorize]
```

- [ ] **Step 7: Seed a katalog**

V `PermissionSeedConfiguration.cs`:
- smaž ř. 90 `new("SEARCH", "Hledání", 80),`;
- smaž ř. 216–217 (`// ==== 14. Hledání ====` a `new("search.index", …)`) a z ř. 213 (`settings.sd.view`) odstraň koncovou čárku;
- za řádek se smazanou sekcí vlož komentář:

```csharp
        // Sekce 14 (search.index) odstraněna 2026-10-13 — hledání hlídá jen přihlášení, rozsah
        // výsledků dává ProjectVisibilityResolver; viz db_upgrade_1_4_8_drop_search_index.sql.
```

- smaž všech 12 mapování `new("<ROLE>", "search.index", ScopeMode.All, true),` (ř. 316, 391, 405, 464, 523, 582, 603, 616, 630, 650, 666 — ověř `grep -n '"search.index"'`, výsledek musí být prázdný). Pokud je smazané mapování posledním prvkem kolekce, odstraň čárku u předchozího prvku.

V `SecurityViewModels.cs` smaž ř. 123–124 (`// --- 14. Hledání …` a `public const string SearchIndex = "search.index";`) a ř. 235–236 (`// 14. Hledání` a `new(SearchIndex, …),`).

- [ ] **Step 8: Opravit testy, které klíč vyjmenovávají**

Odstraň prvek `"search.index"` (a případnou osiřelou čárku) v:
- `Authorization/ResolverSwapSafetyTests.cs:87, 117, 165`
- `Authorization/SubsystemRolePermissionMatrixTests.cs:31, 58`
- `Authorization/ProjectRolePermissionMatrixTests.cs:41, 82, 99`
- `Authorization/ReadAllRoleSeedTests.cs:40` a důvod na ř. 41 změň na `"READ_ALL má read + per-entita export + projects.read.all"`.

V `Search/ProjectVisibilityResolverTests.cs` nahraď oba výskyty `"search.index"` (ř. 96 a 130) za `PermissionKeys.SettingsView` a důvod na ř. 100 za `"settings.view není project-read klíč, sám o sobě projekt nezpřístupní"` (soubor už `PermissionKeys` používá; jinak doplň `using PmTracker.Web.Models.ViewModels;`).

- [ ] **Step 9: Migrace 1_4_8**

Vytvoř `db_upgrade_1_4_8_drop_search_index.sql`:

```sql
-- =============================================================================
-- db_upgrade_1_4_8_drop_search_index.sql
--
-- Authz úklid 2026-10-13 — smaže z authz.* tabulek zrušený klíč `search.index`
-- a jeho (jednoúčelovou) kategorii SEARCH.
--
-- KONTEXT: Hledání (SearchController) hlídá od verze s touto migrací jen přihlášení.
-- Rozsah výsledků určuje ProjectVisibilityResolver (spec 2026-09-17 §2.3). Klíč byl
-- označen jako globální, ale drželo ho i 8 projektových/subsystémových rolí; policy bez
-- projectId ho kontrolovala jen globálně → hledání vracelo 403 všem kromě globálních rolí.
--
-- POŘADÍ: spustit AŽ PO nasazení verze aplikace bez klíče. Starší binárka by klíč při
-- startu znovu doseedovala (PermissionSeeder = idempotentní UPSERT). Klíč, který v DB
-- zůstane, aplikaci nevadí — nic ho nečte; skript je jen úklid.
--
-- ROZSAH MIGRACE:
--   * Smaže záznamy POUZE z:
--       - authz.role_permission_projects  (scope INCLUDE vazby)
--       - authz.role_permissions          (vazby role -> permission)
--       - authz.permissions               (katalog: klic = 'search.index')
--       - authz.permission_categories     (kod = 'SEARCH', jen pokud osiřelá)
--   * NEDOTKNE se: authz.user_roles, authz.roles, žádných business tabulek.
--
-- Idempotence: skript lze spustit opakovaně.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

PRINT N'[1.4.8] Drop search.index — start';

-- Audit pre-state: které role klíč drží (očekávané: 12 seed rolí).
SELECT r.kod AS role_kod, p.klic
FROM authz.role_permissions rp
INNER JOIN authz.roles r ON r.id = rp.role_id
INNER JOIN authz.permissions p ON p.id = rp.permission_id
WHERE p.klic = N'search.index';

-- 1) role_permission_projects — první kvůli FK na role_permissions.
DELETE rpp
FROM authz.role_permission_projects rpp
INNER JOIN authz.role_permissions rp ON rp.id = rpp.role_permission_id
INNER JOIN authz.permissions p ON p.id = rp.permission_id
WHERE p.klic = N'search.index';

PRINT N'[1.4.8] Smazáno z authz.role_permission_projects: ' + CAST(@@ROWCOUNT AS NVARCHAR(16));

-- 2) role_permissions.
DELETE rp
FROM authz.role_permissions rp
INNER JOIN authz.permissions p ON p.id = rp.permission_id
WHERE p.klic = N'search.index';

PRINT N'[1.4.8] Smazáno z authz.role_permissions: ' + CAST(@@ROWCOUNT AS NVARCHAR(16));

-- 3) permissions.
DELETE FROM authz.permissions WHERE klic = N'search.index';

PRINT N'[1.4.8] Smazáno z authz.permissions: ' + CAST(@@ROWCOUNT AS NVARCHAR(16));

-- 4) Kategorie SEARCH — jen pokud na ni už nic neukazuje (guard proti cizím klíčům).
DELETE c
FROM authz.permission_categories c
WHERE c.kod = N'SEARCH'
  AND NOT EXISTS (SELECT 1 FROM authz.permissions p WHERE p.category_id = c.id);

PRINT N'[1.4.8] Smazáno z authz.permission_categories: ' + CAST(@@ROWCOUNT AS NVARCHAR(16));

-- Sanity check.
IF EXISTS (SELECT 1 FROM authz.permissions WHERE klic = N'search.index')
BEGIN
    ROLLBACK TRANSACTION;
    RAISERROR(N'[1.4.8] KONZISTENCE: search.index stále existuje — migrace přerušena.', 16, 1);
    RETURN;
END

PRINT N'[1.4.8] Drop search.index — dokončeno.';

COMMIT TRANSACTION;

-- ROLLBACK: vrátit C# kód (revert commitu, který klíč odstranil z
-- PermissionSeedConfiguration) — PermissionSeeder pak klíč, kategorii i granty při startu
-- aplikace znovu doseeduje. Samotný INSERT zpět bez C# revertu nedává smysl.
```

- [ ] **Step 10: Kontrolní skript a výstup 1_4_4**

V `db_check_applied_upgrades.sql` za blok `DECLARE @schedule_preview …` (končí `@c = @schedule_preview OUTPUT;`) vlož:

```sql
DECLARE @search_index INT;          -- 1_4_8: zrušený klíč search.index v authz.permissions
IF OBJECT_ID(N'authz.permissions', N'U') IS NOT NULL
    EXEC sp_executesql
        N'SELECT @c = COUNT(*) FROM authz.permissions WHERE klic = N''search.index'';',
        N'@c INT OUTPUT', @c = @search_index OUTPUT;
```

a za `INSERT @r VALUES (360, N'db_upgrade_1_4_7_prosty_text_hledani', …);` vlož:

```sql
INSERT @r VALUES (370, N'db_upgrade_1_4_8_drop_search_index',
    CASE WHEN @search_index IS NULL THEN N'NELZE OVĚŘIT (authz schéma chybí)'
         WHEN @search_index = 0 THEN N'APLIKOVÁN'
         ELSE N'CHYBÍ' END,
    N'absence zrušeného klíče search.index v authz.permissions (spouštět až po nasazení aplikace; nalezeno: '
        + ISNULL(CAST(@search_index AS NVARCHAR(10)), N'-') + N')');
```

V `db_upgrade_1_4_4_search_cleanup.sql` (beze změny DML):
- ř. 17 změň na `--     Klíč \`search.index\` tato migrace NEMAŽE (zrušil ho až db_upgrade_1_4_8_drop_search_index.sql).`
- ř. 76 změň na `-- Kategorie SEARCH se tu NEMAŽE — uklízí ji až 1_4_8 spolu se search.index.`
- ř. 85 změň na:

```sql
    SearchIndexKlic        = CASE WHEN EXISTS (SELECT 1 FROM authz.permissions WHERE klic = N'search.index') THEN N'ANO (zruší 1_4_8)' ELSE N'NE (zrušen v 1_4_8)' END;
```

(`SearchMigrationTests` hlídá jen DELETE příkazy a `permission_categories` — obojí zůstává beze změny.)

- [ ] **Step 11: Testy zelené**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~Search|FullyQualifiedName~Authorization|FullyQualifiedName~SeedSourceOfTruth|FullyQualifiedName~PerActionKeyCoverage"`
Expected: Úspěšné.
Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~SearchEndpointsTests"`
Expected: Úspěšné (včetně 3 nových).

- [ ] **Step 12: Dokumentace**

- `docs/wiki/nastaveni-administrace/akce-permissions/index.md`: ř. 26 → `## Kompletní katalog (72 klíčů ve 13 kategoriích)`; sekci `### 14. Hledání (SEARCH) — 2 klíče` (ř. 168–173) nahraď:

```markdown
> Sekce 14 (`SEARCH` / `search.index`, `search.reindex`) odstraněna — `search.reindex` zanikl
> 2026-09 s indexovou vrstvou (`db_upgrade_1_4_4_search_cleanup.sql`), `search.index` 2026-10-13
> (`db_upgrade_1_4_8_drop_search_index.sql`). Hledání smí každý přihlášený; výsledky omezuje
> viditelnost projektů — kdo nevidí projekt, nenajde ani jeho záznamy.
```

- `docs/technical/07-security-authz.md:56` → `- **Ostatní:** \`externiodkazy.sync\` (hledání nemá klíč — rozsah výsledků dává viditelnost projektů)`.
- `docs/superpowers/specs/2026-09-17-vyhledavani-prestavba-design.md` za tabulku §5 doplň:

```markdown
> **Dodatek 2026-10-13:** zrušen i klíč `search.index` (`db_upgrade_1_4_8_drop_search_index.sql`).
> Policy `permission:search.index` se bez `projektId` vyhodnocovala globálně a hledání vracelo
> 403 všem projektovým a subsystémovým rolím. Pravidlo §2.3 (viditelnost projektu) je jediné.
```

- `docs/known-issues/authz-target-matrix.xlsx`: v listech „Funkce × Permission (target)“ (BZ2, CA2) a „Role × Permission (target)“ (BY2, BZ2) dopiš k názvu klíče ` — ZRUŠEN` (search.index 2026-10-13, search.reindex 2026-09). Úprava jen hlaviček, ne sloupců (zachová vzorce a formátování):

```bash
python3 - <<'EOF'
import openpyxl
p = "docs/known-issues/authz-target-matrix.xlsx"
wb = openpyxl.load_workbook(p)
for sheet, cells in {"Funkce × Permission (target)": ["BZ2", "CA2"], "Role × Permission (target)": ["BY2", "BZ2"]}.items():
    ws = wb[sheet]
    for c in cells:
        assert str(ws[c].value).startswith("search."), (sheet, c, ws[c].value)
        ws[c].value = f"{ws[c].value} — ZRUŠEN"
wb.save(p)
EOF
```

- [ ] **Step 13: Commit**

```bash
git add PmTracker.Web/Controllers/SearchController.cs PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs \
  PmTracker.Web/Models/ViewModels/SecurityViewModels.cs db_upgrade_1_4_8_drop_search_index.sql db_check_applied_upgrades.sql \
  db_upgrade_1_4_4_search_cleanup.sql PmTracker.Tests.Unit PmTracker.Tests.Api docs
git commit -m "fix(hledani): hledání smí každý přihlášený, klíč search.index zrušen

Policy permission:search.index se bez projektId vyhodnocovala globálně, takže
hledání vracelo 403 všem bez globálního klíče (regrese 89bb967). Rozsah výsledků
určuje ProjectVisibilityResolver (spec 2026-09-17 §2.3). Úklid DB:
db_upgrade_1_4_8_drop_search_index.sql — spouštět až po nasazení.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Sdílené pravidlo výběru zdrojové vazby (bod 5)

**Files:**
- Modify: `PmTracker.Web/Services/Schedules/HarmonogramSkutecnostResolver.cs:60-126`
- Modify: `PmTracker.Web/Services/ProjectService.RecordDetailPage.cs:80-135`
- Test: `PmTracker.Tests.Unit/Schedule/HarmonogramSkutecnostResolverTests.cs`
- Test: `PmTracker.Tests.Api/Controllers/RecordDetailPageRenderTests.cs`
- Modify: `PmTracker.Tests.Api/TestInfrastructure/ApiSqlFixture.cs` (`SeedTicketBindingAsync`)

**Interfaces:**
- Produces: `HarmonogramSkutecnostResolver.ResolveMatched(int krokPoradi, IEnumerable<BindingKandidat> matched, int? preferredExterniOdkazId) : ResolvedSkutecnost` — vazby už jsou pro daný krok (bez filtru predikátem).
- Produces: `ApiSqlFixture.SeedTicketBindingAsync(int recordId, string cislo, string typKod, int poradi, long hotVyjadreniId, DateTime datum) : Task<int>` (vrací id externí vazby) — používají Tasks 3, 4.

- [ ] **Step 1: Padající unit testy resolveru**

Do `HarmonogramSkutecnostResolverTests.cs` přidej:

```csharp
    [Fact]
    public void ResolveMatched_Krok1_VraciNejstarsi_IgnorujePreferred()
    {
        // Stránka záznamu nemá typ tiketu (bez dotazu do SD) — vazby už jsou pro krok 1.
        // Pravidlo musí být totožné s Resolve: krok 1 = MIN napříč tikety, preferred neplatí.
        var pmp = new BindingKandidat(100, "", "", "", D2);
        var pnf = new BindingKandidat(200, "", "", "", D1);

        var r = HarmonogramSkutecnostResolver.ResolveMatched(1, new[] { pmp, pnf }, preferredExterniOdkazId: 100);

        r.Datum.Should().Be(D1);
        r.VybranyExterniOdkazId.Should().Be(200);
    }

    [Fact]
    public void ResolveMatched_OstatniKrok_PreferredVyhravaJinakNejnovejsi()
    {
        var a = new BindingKandidat(100, "", "", "", D1);
        var b = new BindingKandidat(200, "", "", "", D3);

        HarmonogramSkutecnostResolver.ResolveMatched(7, new[] { a, b }, preferredExterniOdkazId: 100)
            .VybranyExterniOdkazId.Should().Be(100);
        HarmonogramSkutecnostResolver.ResolveMatched(7, new[] { a, b }, preferredExterniOdkazId: null)
            .VybranyExterniOdkazId.Should().Be(200);
    }
```

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~HarmonogramSkutecnostResolverTests"`
Expected: build error `'HarmonogramSkutecnostResolver' does not contain a definition for 'ResolveMatched'`.

- [ ] **Step 2: `ResolveMatched` v resolveru**

V `HarmonogramSkutecnostResolver.cs` nahraď začátek `Resolve` (filtr + řazení) delegací a zbytek těla přesuň do nové metody:

```csharp
    public static ResolvedSkutecnost Resolve(
        int krokPoradi,
        IReadOnlyList<BindingKandidat> allBindingsForZaznam,
        int? preferredExterniOdkazId)
        => ResolveMatched(
            krokPoradi,
            allBindingsForZaznam.Where(b => HarmonogramKrokDatumMapping.GetPredikatKey(b.TypZaznamu, krokPoradi) == b.PredikatKey),
            preferredExterniOdkazId);

    /// <summary>
    /// Výběr vítězné vazby nad kandidáty, kteří už k <paramref name="krokPoradi"/> patří
    /// (bez filtru predikátem podle typu tiketu). Pro volající, kteří mají uložené vazby
    /// s pořadím z harvestu a nechtějí kvůli typu tiketu volat ServiceDesk — stránka záznamu.
    /// Pravidlo je totožné s <see cref="Resolve"/>: krok 1 = MIN bez preferred, ostatní
    /// = preferred, jinak MAX.
    /// </summary>
    public static ResolvedSkutecnost ResolveMatched(
        int krokPoradi,
        IEnumerable<BindingKandidat> matched,
        int? preferredExterniOdkazId)
    {
        // Spec 2026-04-28 §2: Krok 1 „příprava zadání" agreguje napříč externími vazbami
        // jako MIN (nejdřívější datum založení tiketu). Ostatní kroky 3, 4, 6, 7, 10
        // používají MAX (nejpozdější datum vyjádření) — výchozí stav z C-Q2.
        var kandidati = (krokPoradi == 1
                ? matched.OrderBy(b => b.Datum)
                : matched.OrderByDescending(b => b.Datum))
            .ThenBy(b => b.ExterniOdkazId) // deterministické tie-break při shodném datu
            .ToList();

        if (kandidati.Count == 0)
        {
            return new ResolvedSkutecnost(
                Datum: null,
                VybranyExterniOdkazId: null,
                Kandidati: kandidati,
                PreferredFallbackApplied: false);
        }

        // FIX 2026-05-04: Krok 1 = MIN agregace přes všechny synthetic K1 bindings (spec
        // 2026-04-28 §2 „nejdřívější datum založení tiketu"). Preferred koncepce neplatí —
        // agregace je deterministická a bere nejstarší datum napříč externími vazbami.
        // Signalizujeme PreferredFallbackApplied když preferred ≠ MIN, aby sync clear-nul
        // stale preferred z krok řádku a další iterace byla čistá.
        if (krokPoradi == 1)
        {
            var min = kandidati[0]; // OrderBy(Datum) ASC výše → kandidati[0] = nejstarší
            return new ResolvedSkutecnost(
                Datum: min.Datum,
                VybranyExterniOdkazId: min.ExterniOdkazId,
                Kandidati: kandidati,
                PreferredFallbackApplied:
                    preferredExterniOdkazId.HasValue
                    && preferredExterniOdkazId.Value != min.ExterniOdkazId);
        }

        if (preferredExterniOdkazId.HasValue)
        {
            var preferred = kandidati.FirstOrDefault(k => k.ExterniOdkazId == preferredExterniOdkazId.Value);
            if (preferred is not null)
            {
                return new ResolvedSkutecnost(
                    Datum: preferred.Datum,
                    VybranyExterniOdkazId: preferred.ExterniOdkazId,
                    Kandidati: kandidati,
                    PreferredFallbackApplied: false);
            }
            // Preferred není mezi kandidáty → fallback na MAX + signál pro caller (clear preferred).
            var fallback = kandidati[0];
            return new ResolvedSkutecnost(
                Datum: fallback.Datum,
                VybranyExterniOdkazId: fallback.ExterniOdkazId,
                Kandidati: kandidati,
                PreferredFallbackApplied: true);
        }

        var max = kandidati[0];
        return new ResolvedSkutecnost(
            Datum: max.Datum,
            VybranyExterniOdkazId: max.ExterniOdkazId,
            Kandidati: kandidati,
            PreferredFallbackApplied: false);
    }
```

(Tělo je přesun původního kódu z `Resolve` — logika se nemění, `Resolve` jen předfiltruje predikátem a deleguje. Původní dlouhý komentář u kroku 1 o PMP→PNF ponech v plném znění.)

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~HarmonogramSkutecnostResolverTests"`
Expected: Úspěšné (staré i nové).

- [ ] **Step 3: Fixture — vazba s řízeným tiketem**

Do `ApiSqlFixture.cs` za `SeedHarvestedActualAsync` přidej:

```csharp
    /// <summary>
    /// Externí vazba s konkrétním číslem a typem tiketu + aktivní vazba vyjádření na krok.
    /// Na rozdíl od <see cref="SeedHarvestedActualAsync"/> řídí test číslo, typ, id vyjádření
    /// i datum — potřebné pro falešný ServiceDesk a pro výběr vazby u více tiketů (2026-10-13).
    /// Krok musí existovat (SeedDatumScheduleAsync).
    /// </summary>
    public async Task<int> SeedTicketBindingAsync(int recordId, string cislo, string typKod, int poradi, long hotVyjadreniId, DateTime datum)
    {
        await using var dbContext = CreateDbContext();

        var typId = await dbContext.CiselnikTypuExternichOdkazu.Where(x => x.Kod == typKod).Select(x => x.Id).FirstAsync();
        var odkaz = await dbContext.ZaznamExterniOdkazy
            .FirstOrDefaultAsync(x => x.ZaznamId == recordId && x.Cislo == cislo);
        if (odkaz is null)
        {
            odkaz = new ZaznamExterniOdkazEntity { ZaznamId = recordId, TypOdkazuId = typId, Cislo = cislo };
            dbContext.ZaznamExterniOdkazy.Add(odkaz);
            await dbContext.SaveChangesAsync();
        }

        var hasBinding = await dbContext.VyjadreniVazby.AnyAsync(x => x.ZaznamId == recordId
            && x.ExterniOdkazId == odkaz.Id && x.Poradi == (byte)poradi && x.Stav == (byte)VazbaStav.Active);
        if (!hasBinding)
        {
            dbContext.VyjadreniVazby.Add(new ZaznamHarmonogramVyjadreniVazbaEntity
            {
                ZaznamId = recordId,
                Poradi = (byte)poradi,
                ExterniOdkazId = odkaz.Id,
                HotVyjadreniId = hotVyjadreniId,
                DatumVyjadreni = datum,
                Source = (byte)VazbaSource.Auto,
                Stav = (byte)VazbaStav.Active,
                CreatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        var krok = await dbContext.ZaznamHarmonogramKroky.FirstAsync(x => x.ZaznamId == recordId && x.Poradi == (byte)poradi);
        krok.SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Automat;
        krok.SkutecnostRezim = (byte)SkutecnostRezimEnum.Auto;
        await dbContext.SaveChangesAsync();

        return odkaz.Id;
    }
```

- [ ] **Step 4: Padající Api test stránky záznamu**

Do `RecordDetailPageRenderTests.cs`:

```csharp
    [Fact]
    public async Task DetailPage_Krok1SPmpIPnf_UkazeNejstarsiDatum()
    {
        // Krok 1 = MIN napříč tikety (spec 2026-04-28 §2). Stránka záznamu dřív brala
        // nejnovější vazbu, takže ukazovala jiné datum než editor a uložená skutečnost.
        var (_, recordId) = await SeedTaskWithScheduleAsync("ApiRecPage12");
        var starsi = DateTime.UtcNow.Date.AddDays(-20);
        var novejsi = DateTime.UtcNow.Date.AddDays(-10);
        await _fixture.SeedTicketBindingAsync(recordId, "812001", "PMP", poradi: 1, hotVyjadreniId: 0, datum: starsi);
        await _fixture.SeedTicketBindingAsync(recordId, "812002", "PNF", poradi: 1, hotVyjadreniId: 0, datum: novejsi);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        var rowStart = html.IndexOf("data-step-index=\"1\"", StringComparison.Ordinal);
        rowStart.Should().BeGreaterThan(0);
        var rowHtml = html[rowStart..html.IndexOf("</tr>", rowStart, StringComparison.Ordinal)];

        rowHtml.Should().Contain(starsi.ToString("dd.MM.yyyy"), "krok 1 bere nejstarší datum (PMP)");
        rowHtml.Should().NotContain(novejsi.ToString("dd.MM.yyyy"), "ne nejnovější (PNF)");
    }
```

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~DetailPage_Krok1SPmpIPnf_UkazeNejstarsiDatum"`
Expected: Neúspěšné — řádek obsahuje novější datum.

- [ ] **Step 5: `AttachSourceVyjadreniAsync` přes `ResolveMatched`**

V `ProjectService.RecordDetailPage.cs` nahraď blok `var byPoradi = vazby.GroupBy(…)…` až `.ToList();` na konci metody a uprav XML komentář metody (poslední věta: „Výběr vazby = stejné pravidlo jako editor (`HarmonogramSkutecnostResolver.ResolveMatched`): krok 1 nejstarší, ostatní preferovaná, jinak nejnovější.“):

```csharp
        var winnerByPoradi = vazby
            .GroupBy(v => v.Poradi)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var resolved = HarmonogramSkutecnostResolver.ResolveMatched(
                        g.Key,
                        g.Select(v => new BindingKandidat(v.ExterniOdkazId, "", "", "", v.DatumVyjadreni, v.HotVyjadreniId)),
                        preferredByPoradi.TryGetValue(g.Key, out var preferred) ? preferred : null);
                    return resolved.Kandidati.First(k => k.ExterniOdkazId == resolved.VybranyExterniOdkazId);
                });

        return kroky
            .Select(k =>
            {
                if (winnerByPoradi.TryGetValue((byte)k.KrokIndex, out var winner))
                {
                    k.SourceExterniOdkazId = winner.ExterniOdkazId;
                    k.SourceVyjadreniId = winner.HotVyjadreniId > 0 ? winner.HotVyjadreniId : null;
                    k.SourceVyjadreniDatum = winner.Datum;
                }
                return k;
            })
            .ToList();
```

(`using PmTracker.Web.Services.Schedules;` už v souboru je.)

- [ ] **Step 6: Testy zelené**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~RecordDetailPageRenderTests"` → Úspěšné.
Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~Schedule"` → Úspěšné.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Services/Schedules/HarmonogramSkutecnostResolver.cs PmTracker.Web/Services/ProjectService.RecordDetailPage.cs \
  PmTracker.Tests.Unit/Schedule/HarmonogramSkutecnostResolverTests.cs PmTracker.Tests.Api
git commit -m "fix(harmonogram): stránka záznamu vybírá zdrojovou vazbu stejně jako editor

U kroku 1 se dvěma tikety (PMP i PNF) brala nejnovější vazbu místo nejstarší,
takže ukazovala jiné datum než editor a uložená skutečnost. Pravidlo výběru je
nově sdílené: HarmonogramSkutecnostResolver.ResolveMatched.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Okno tiketu jen pro čtení — endpoint (bod 2, server)

**Files:**
- Create: `PmTracker.Tests.Api/TestInfrastructure/FakeServiceDesk.cs`
- Modify: `PmTracker.Tests.Api/TestInfrastructure/ApiSqlFixture.cs` (`ServiceDesk`, `ServiceDeskFactory`, dispose)
- Create: `PmTracker.Tests.Api/Controllers/VyjadreniNahledEndpointTests.cs`
- Modify: `PmTracker.Web/Controllers/ZaznamyController.Partials.cs` (nová akce)
- Modify: `PmTracker.Web/Services/ServiceDesk/VyjadreniModalViewModelBuilder.cs:48-63, 99-104, 143, 186-197`
- Modify: `PmTracker.Web/Models/ViewModels/Vyjadreni/VyjadreniModalViewModel.cs`
- Modify: `PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml:13-19, 70-75, 234-243`

**Interfaces:**
- Consumes: `EnsureSubsystemRoleAsync` (Task 1), `SeedTicketBindingAsync` (Task 2).
- Produces: `GET /Zaznamy/VyjadreniNahled?externiOdkazId={int}&zaznamId={int}` → `PartialView _ChatModal` s `CanEdit=false`; 404 bez přístupu / nesouhlasící ids; 400 pro ids ≤ 0.
- Produces: kořen okna `data-chat-modal-mode="read"|"edit"`; varování výpadku `[data-chat-modal-sd-offline]`; patička `[data-chat-modal-readonly-hint]`.
- Produces: `FakeServiceDesk` (implementuje `ITicketingQueryService` i `IVyjadreniQueryService`; `Tikety`, `Vyjadreni`, `OfflineCisla`) a `ApiSqlFixture.ServiceDeskFactory` (`WebApplicationFactory<Program>`) — používají Tasks 4, 5.

- [ ] **Step 1: Falešný ServiceDesk**

Vytvoř `PmTracker.Tests.Api/TestInfrastructure/FakeServiceDesk.cs`:

```csharp
using PmTracker.ServiceDesk.Contracts;

namespace PmTracker.Tests.Api.TestInfrastructure;

/// <summary>
/// ServiceDesk pro Api testy, které potřebují obsah tiketu (okno vyjádření, kandidáti
/// harmonogramu). Sdílený pro kolekci — každý test si registruje vlastní čísla tiketů.
/// Číslo v <see cref="OfflineCisla"/> simuluje výpadek SD (výjimka jako u SqlClient).
/// </summary>
public sealed class FakeServiceDesk : ITicketingQueryService, IVyjadreniQueryService
{
    public Dictionary<string, HotZaznamDto> Tikety { get; } = new();
    public Dictionary<string, List<HotVyjadreniDto>> Vyjadreni { get; } = new();
    public HashSet<string> OfflineCisla { get; } = new();

    private void ThrowIfOffline(IEnumerable<string> cisla)
    {
        if (cisla.Any(OfflineCisla.Contains))
        {
            throw new InvalidOperationException("ServiceDesk je v testu nedostupný.");
        }
    }

    public Task<HotZaznamDto?> GetZaznamAsync(string cislo, CancellationToken ct)
    {
        ThrowIfOffline([cislo]);
        return Task.FromResult(Tikety.GetValueOrDefault(cislo));
    }

    public Task<IReadOnlyDictionary<string, HotZaznamDto>> GetZaznamyAsync(IReadOnlyCollection<string> cisla, CancellationToken ct)
    {
        ThrowIfOffline(cisla);
        IReadOnlyDictionary<string, HotZaznamDto> map = cisla.Where(Tikety.ContainsKey).ToDictionary(c => c, c => Tikety[c]);
        return Task.FromResult(map);
    }

    public Task<HotKalkulaceDto?> GetAkceptovanouKalkulaciAsync(string cislo, CancellationToken ct)
        => Task.FromResult<HotKalkulaceDto?>(null);

    public Task<IReadOnlyDictionary<string, HotKalkulaceDto>> GetAkceptovaneKalkulaceAsync(IReadOnlyCollection<string> cisla, CancellationToken ct)
        => Task.FromResult<IReadOnlyDictionary<string, HotKalkulaceDto>>(new Dictionary<string, HotKalkulaceDto>());

    public Task<IReadOnlyList<HotVyjadreniDto>> GetVyjadreniForTicketAsync(string cislo6, DateTime? sinceUtc, CancellationToken ct)
    {
        ThrowIfOffline([cislo6]);
        return Task.FromResult<IReadOnlyList<HotVyjadreniDto>>(Vyjadreni.GetValueOrDefault(cislo6) ?? []);
    }

    public Task<IReadOnlyDictionary<string, HotZaznamFingerprintDto>> GetHotZaznamFingerprintsAsync(IReadOnlyCollection<string> cisla6, CancellationToken ct)
    {
        ThrowIfOffline(cisla6);
        IReadOnlyDictionary<string, HotZaznamFingerprintDto> map = cisla6
            .Where(Tikety.ContainsKey)
            .ToDictionary(c => c, c => new HotZaznamFingerprintDto(c, DateTime.Today.AddDays(-30), "Řešení", Tikety[c].TypZaznamu));
        return Task.FromResult(map);
    }

    public Task<IReadOnlyDictionary<string, VyjadreniSecondaryFingerprintDto>> GetVyjadreniSecondaryFingerprintsAsync(IReadOnlyCollection<string> cisla6, CancellationToken ct)
    {
        ThrowIfOffline(cisla6);
        IReadOnlyDictionary<string, VyjadreniSecondaryFingerprintDto> map = cisla6
            .Where(c => Vyjadreni.TryGetValue(c, out var list) && list.Count > 0)
            .ToDictionary(c => c, c => new VyjadreniSecondaryFingerprintDto(c, Vyjadreni[c].Max(v => v.Id), Vyjadreni[c].Count));
        return Task.FromResult(map);
    }
}
```

- [ ] **Step 2: Factory se ServiceDeskem ve fixture**

V `ApiSqlFixture.cs` doplň usingy `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.AspNetCore.TestHost`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.DependencyInjection.Extensions`, `PmTracker.ServiceDesk.Contracts` a pod `Factory`:

```csharp
    /// <summary>Falešný ServiceDesk sdílený kolekcí (viz <see cref="ServiceDeskFactory"/>).</summary>
    public FakeServiceDesk ServiceDesk { get; } = new();

    private WebApplicationFactory<Program>? _serviceDeskFactory;

    /// <summary>
    /// Aplikace se zapnutým (falešným) ServiceDeskem. Hlavní <see cref="Factory"/> má SD vypnutý
    /// (DisabledTicketingQueryService) a řada testů na tom stojí — proto samostatná instance.
    /// </summary>
    public WebApplicationFactory<Program> ServiceDeskFactory => _serviceDeskFactory ??= Factory.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITicketingQueryService>();
            services.RemoveAll<IVyjadreniQueryService>();
            services.AddSingleton<ITicketingQueryService>(ServiceDesk);
            services.AddSingleton<IVyjadreniQueryService>(ServiceDesk);
        }));
```

a v `DisposeAsync` před `Factory.Dispose();` přidej `_serviceDeskFactory?.Dispose();`.

- [ ] **Step 3: Padající Api testy endpointu**

Vytvoř `PmTracker.Tests.Api/Controllers/VyjadreniNahledEndpointTests.cs`:

```csharp
using System.Net;
using FluentAssertions;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Okno tiketu jen pro čtení (2026-10-13, spec 2026-04-21 §5.2): obsah tiketu u bubliny
/// skutečnosti vidí každý, kdo vidí záznam — bez klíče. Úpravy přiřazení kroků zůstávají
/// v editačním okně za vyjadreni.modal.open.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class VyjadreniNahledEndpointTests
{
    private readonly ApiSqlFixture _fixture;

    public VyjadreniNahledEndpointTests(ApiSqlFixture fixture) => _fixture = fixture;

    private sealed record Scenario(int ProjectId, int RecordId, int OdkazId, int DeputyId, int OutsiderId, string Cislo);

    private async Task<Scenario> SeedAsync(string marker, string cislo)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var deputyId = await _fixture.EnsurePersonAsync($"{marker}Deputy");
        var outsiderId = await _fixture.EnsurePersonAsync($"{marker}Outsider");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        await _fixture.EnsureSubsystemRoleAsync(projectId, subsystemId, deputyId, SubsystemRoleCodes.DeputyLead);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} zaznam");
        await _fixture.SeedDatumScheduleAsync(recordId, DateTime.UtcNow.AddDays(-30), new HashSet<int> { 1 });
        var odkazId = await _fixture.SeedTicketBindingAsync(recordId, cislo, "PMP", poradi: 3, hotVyjadreniId: 7001, datum: new DateTime(2026, 4, 1));

        _fixture.ServiceDesk.Tikety[cislo] = new HotZaznamDto(cislo, "PMP  ", "Strucne testu", "Popis tiketu", "uzivatel");
        _fixture.ServiceDesk.Vyjadreni[cislo] =
        [
            new HotVyjadreniDto(7001, "D", $"PID-{cislo}", new DateTime(2026, 4, 1, 10, 0, 0), "dodavatel", "Zaznam byl predan dodavateli k reseni.", null, 1),
            new HotVyjadreniDto(7002, "D", $"PID-{cislo}", new DateTime(2026, 4, 2, 10, 0, 0), "dodavatel", "Doplnujici informace.", null, 1)
        ];
        return new Scenario(projectId, recordId, odkazId, deputyId, outsiderId, cislo);
    }

    private async Task<(HttpStatusCode Status, string Html)> GetNahledAsync(int odkazId, int recordId, int asUser)
    {
        using var client = _fixture.ServiceDeskFactory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/VyjadreniNahled?externiOdkazId={odkazId}&zaznamId={recordId}&asUser={asUser}");
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task VyjadreniNahled_Zastupce_VidiObsahTiketuAPrirazeniBezUprav()
    {
        var s = await SeedAsync("ApiNahled1", "813001");

        var (status, html) = await GetNahledAsync(s.OdkazId, s.RecordId, s.DeputyId);

        status.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-chat-modal-mode=\"read\"");
        html.Should().Contain("data-vyjadreni-id=\"7001\"").And.Contain("data-vyjadreni-id=\"7002\"");
        html.Should().Contain("Krok 3", "přiřazení vyjádření ke kroku je vidět jako štítek");
        html.Should().Contain("data-chat-modal-readonly-hint");
        html.Should().NotContain("data-bubble-step-selector", "výběr kroku je jen v editačním okně");
        html.Should().NotContain("data-reharvest-form");
        html.Should().NotContain("data-clear-bubble-binding");
        html.Should().NotContain("data-clear-binding");
    }

    [Fact]
    public async Task VyjadreniNahled_UzivatelBezPristupuKProjektu_Vraci404()
    {
        var s = await SeedAsync("ApiNahled2", "813002");

        var (status, _) = await GetNahledAsync(s.OdkazId, s.RecordId, s.OutsiderId);

        status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task VyjadreniNahled_VazbaJinehoZaznamu_Vraci404()
    {
        var a = await SeedAsync("ApiNahled3", "813003");
        var b = await SeedAsync("ApiNahled4", "813004");

        // Vazba záznamu A s id záznamu B (B uživatel vidí) — nesmí prozradit tiket A.
        var (status, _) = await GetNahledAsync(a.OdkazId, b.RecordId, b.DeputyId);

        status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task VyjadreniNahled_ServiceDeskNedostupny_UkazeVarovaniMisto500()
    {
        var s = await SeedAsync("ApiNahled5", "813005");
        _fixture.ServiceDesk.OfflineCisla.Add(s.Cislo);

        var (status, html) = await GetNahledAsync(s.OdkazId, s.RecordId, s.DeputyId);

        status.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-chat-modal-sd-offline");
        html.Should().Contain("data-chat-modal-mode=\"read\"");
    }

    [Fact]
    public async Task EditacniOkno_ZastupceBezKlice_ZustavaZakazane()
    {
        var s = await SeedAsync("ApiNahled6", "813006");

        using var client = _fixture.ServiceDeskFactory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Vyjadreni/Modal?externiOdkazId={s.OdkazId}&zaznamId={s.RecordId}&asUser={s.DeputyId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "úpravy přiřazení drží vyjadreni.modal.open");
    }
}
```

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~VyjadreniNahledEndpointTests"`
Expected: Neúspěšné všechny kromě `EditacniOkno_…` (endpoint neexistuje → 404 tam, kde se čeká 200; 404 testy mohou projít náhodou — po implementaci musí projít ze správného důvodu, viz Step 7).

- [ ] **Step 4: View model**

Do `VyjadreniModalViewModel` přidej:

```csharp
    /// <summary>
    /// ServiceDesk při sestavení okna neodpověděl (2026-10-13). Okno pak ukáže varování
    /// a přiřazení kroků z lokálních vazeb místo „HTTP 500".
    /// </summary>
    public bool ServiceDeskNedostupny { get; set; }
```

- [ ] **Step 5: Builder — výpadek SD a typ z lokální vazby**

V `VyjadreniModalViewModelBuilder.cs`:
1. Do polí a konstruktoru přidej `ILogger<VyjadreniModalViewModelBuilder> logger` (`private readonly ILogger<VyjadreniModalViewModelBuilder> _logger;`, přiřazení `_logger = logger;`). Builder se vytváří jen přes DI.
2. Řádky `var tiket = await _ticketing.GetZaznamAsync(…);` až `vm.TiketModul = tiket?.Modul;` nahraď:

```csharp
        // Obsah tiketu se čte živě ze ServiceDesku. Při výpadku nespadnout na HTTP 500 —
        // okno ukáže varování a přiřazení kroků z lokálních vazeb (typ z lokální externí vazby,
        // aby stepper věděl, které kroky k tiketu patří).
        HotZaznamDto? tiket = null;
        IReadOnlyList<HotVyjadreniDto> list = Array.Empty<HotVyjadreniDto>();
        try
        {
            tiket = await _ticketing.GetZaznamAsync(eo.Cislo, ct).ConfigureAwait(false);
            list = await _vyjadreni.GetVyjadreniForTicketAsync(eo.Cislo, sinceUtc: null, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Okno vyjádření: ServiceDesk nedostupný pro tiket {Cislo}.", eo.Cislo);
            vm.ServiceDeskNedostupny = true;
            vm.EmptyMessage = "ServiceDesk je teď nedostupný, obsah tiketu nelze načíst. Přiřazení kroků odpovídá poslednímu vytěžení.";
        }

        var lokalniTyp = await _db.CiselnikTypuExternichOdkazu.AsNoTracking()
            .Where(t => t.Id == eo.TypOdkazuId)
            .Select(t => t.Kod)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        vm.TiketStrucne = tiket?.Strucne;
        vm.TiketTyp = tiket?.TypZaznamu ?? lokalniTyp;
        vm.TiketUzivatel = tiket?.Uzivatel;
        vm.TiketSubsystem = tiket?.Subsystem;
        vm.TiketModul = tiket?.Modul;
```

3. Smaž pozdější řádek `var list = await _vyjadreni.GetVyjadreniForTicketAsync(eo.Cislo, sinceUtc: null, ct).ConfigureAwait(false);`.
4. Závěrečný blok `if (bubliny.All(b => b.IsTicketIntro)) {…} else {…}` obal tak, aby varování výpadku nepřepsal:

```csharp
        if (!vm.ServiceDeskNedostupny)
        {
            // Reálná vyjádření (mimo synthetic intro) — pokud žádná, ukázat empty message.
            if (bubliny.All(b => b.IsTicketIntro))
            {
                vm.EmptyMessage = "Žádná vyjádření zatím nejsou dostupná (buď ještě nebyla vytěžena, nebo ServiceDesk je offline).";
            }
            else
            {
                // Spec 2026-04-29-modal-vyjadreni-dropdown §3 — per-bubble option computation
                // (1:1 + chronologie validation). Také Assigned* properties pro UI badge / dropdown.
                ComputePerBubbleStepOptions(vm, activeBindings);
            }
        }
```

(Doplň `using Microsoft.Extensions.Logging;` a `using PmTracker.ServiceDesk.Contracts;`, pokud chybí.)

- [ ] **Step 6: View `_ChatModal.cshtml`**

1. Na kořen `<div class="pm-chat-modal" data-chat-modal-root …>` přidej atribut `data-chat-modal-mode="@(canEdit ? "edit" : "read")"`.
2. Blok `@if (!string.IsNullOrWhiteSpace(Model.EmptyMessage)) { <gov-alert variant="info" …> }` nahraď:

```cshtml
    @if (Model.ServiceDeskNedostupny)
    {
        <gov-alert variant="warning" role="status" data-chat-modal-sd-offline>
            @Model.EmptyMessage
        </gov-alert>
    }
    else if (!string.IsNullOrWhiteSpace(Model.EmptyMessage))
    {
        <gov-alert variant="info" role="status">
            @Model.EmptyMessage
        </gov-alert>
    }
```

3. Za blok `@if (canEdit && !isNes) { <footer …> }` přidej:

```cshtml
    @if (!canEdit)
    {
        @* 2026-10-13 (spec 2026-04-21 §5.2): okno jen pro čtení — obsah tiketu a přiřazení kroků
           bez možnosti je měnit. Úpravy přiřazení patří do editoru záznamu. *@
        <footer class="pm-chat-modal__footer" data-chat-modal-readonly-hint>
            <small class="pm-chat-modal__hint">Jen pro čtení. Přiřazení vyjádření ke krokům se upravuje v editoru záznamu.</small>
        </footer>
    }
```

- [ ] **Step 7: Akce `VyjadreniNahled`**

V `ZaznamyController.Partials.cs` (usingy: `Microsoft.EntityFrameworkCore`, `PmTracker.Web.Services.ServiceDesk`) za `RecordCommentsPartial` přidej:

```csharp
    /// <summary>
    /// Okno tiketu jen pro čtení (2026-10-13, spec 2026-04-21 §5.2): obsah tiketu ze ServiceDesku
    /// a přiřazení vyjádření ke krokům, bez možnosti je měnit. Guard = přístup k projektu
    /// záznamu, stejně jako stránka záznamu — žádný klíč. projektId se nebere od klienta, ale
    /// odvozuje z vazby, takže podvržené id vrátí 404. Bez harvestu: GET nic nezapisuje.
    /// Builder přes [FromServices], aby se neměnil konstruktor controlleru.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> VyjadreniNahled(
        int externiOdkazId,
        int zaznamId,
        [FromServices] IVyjadreniModalViewModelBuilder builder,
        CancellationToken ct = default)
    {
        if (externiOdkazId <= 0 || zaznamId <= 0)
        {
            return BadRequest();
        }

        var projektId = await (from eo in _db.ZaznamExterniOdkazy.AsNoTracking()
                               join z in _db.ProjektoveZaznamy.AsNoTracking() on eo.ZaznamId equals z.Id
                               where eo.Id == externiOdkazId && eo.ZaznamId == zaznamId
                               select (int?)z.ProjektId)
                              .FirstOrDefaultAsync(ct);
        if (projektId is null || !CurrentUserContext.CanAccessProject(projektId.Value))
        {
            return NotFound();
        }

        var vm = await builder.BuildAsync(externiOdkazId, zaznamId, canEdit: false, canAddAddon: false, ct);
        if (vm is null)
        {
            return NotFound();
        }

        return PartialView("~/Views/Vyjadreni/_ChatModal.cshtml", vm);
    }
```

- [ ] **Step 8: Testy zelené (a 404 ze správného důvodu)**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~VyjadreniNahledEndpointTests|FullyQualifiedName~Vyjadreni"`
Expected: Úspěšné. Pro kontrolu 404 testů dočasně zakomentuj podmínku `CanAccessProject` → `VyjadreniNahled_UzivatelBezPristupuKProjektu_Vraci404` musí spadnout; podmínku vrať.
Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~Vyjadreni|FullyQualifiedName~ChatModal|FullyQualifiedName~Architecture"`
Expected: Úspěšné (`AuthorizationPolicyEnforcementTests` se GET akce netýká).

- [ ] **Step 9: Commit**

```bash
git add PmTracker.Web/Controllers/ZaznamyController.Partials.cs PmTracker.Web/Services/ServiceDesk/VyjadreniModalViewModelBuilder.cs \
  PmTracker.Web/Models/ViewModels/Vyjadreni PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml PmTracker.Tests.Api
git commit -m "feat(vyjadreni): okno tiketu jen pro čtení pro každého, kdo vidí záznam

GET /Zaznamy/VyjadreniNahled ukáže obsah tiketu a přiřazení kroků bez možnosti
je měnit (spec 2026-04-21 §5.2). Guard = přístup k projektu jako stránka záznamu,
žádný nový klíč; projektId se odvozuje z vazby. Výpadek ServiceDesku už okno
neshodí na HTTP 500 — ukáže varování a lokální přiřazení.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Režim bubliny, stránka záznamu bez podmínky, JS (bod 2, UI)

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs` (`HarmonogramBlockViewModel`, `ScheduleBlockManualCellViewModel:239-251`)
- Modify: `PmTracker.Web/Views/Shared/_ScheduleTable.cshtml:137-143, 150-156, 163-169`
- Modify: `PmTracker.Web/Views/Shared/_ScheduleBlockManualCell.cshtml:137-148, 224-235`
- Modify: `PmTracker.Web/Controllers/ZaznamyController.cs:66-76, 270-275`
- Modify: `PmTracker.Web/Services/ProjectService.RecordDetailPage.cs:11-53`
- Modify: `PmTracker.Web/Services/IProjectService.cs:21`, `IRecordService.cs:12`, `RecordService.cs:70-71`
- Modify (test fakes): `PmTracker.Tests.Unit/Projects/ProjektyControllerBehaviorTests.cs:490`, `PmTracker.Tests.Unit/Controllers/ProjektyControllerTeamAuthzTests.cs:229`
- Modify: `PmTracker.Tests.Unit/Projects/RecordScheduleToggleJsTests.cs:22-43`
- Modify: `PmTracker.Web/wwwroot/js/modules/vyjadreni/chatModal.js`
- Create: `PmTracker.Tests.Api/Controllers/VyjadreniOknoRezimRenderTests.cs`
- Create: `PmTracker.Tests.E2E/Scenarios/TicketReadOnlyWindowScenariosTests.cs`
- Docs: `docs/wiki/integrace/servicedesk/chat-vyjadreni.md:16-24`, `docs/wiki/nastaveni-administrace/akce-permissions/index.md:89`

**Interfaces:**
- Consumes: endpoint a markup z Task 3; `ServiceDeskFactory`, `SeedTicketBindingAsync`, `EnsureSubsystemRoleAsync`.
- Produces: `HarmonogramBlockViewModel.VyjadreniOknoUpravy : bool` (init, výchozí `false` = čtení); `ScheduleBlockManualCellViewModel.VyjadreniOknoUpravy : bool`; tlačítko bubliny nese `data-external-chat-mode="read"|"edit"`; `IProjectService/IRecordService.BuildRecordDetailPageAsync(int projectId, int recordId, CancellationToken ct = default)` (přetížení s `canOpenVyjadreni` zaniká); `window.pmChatModal.open(externiOdkazId, zaznamId, mode)` (`mode` volitelný, výchozí `edit`).

- [ ] **Step 1: Padající Api render testy**

Vytvoř `PmTracker.Tests.Api/Controllers/VyjadreniOknoRezimRenderTests.cs`:

```csharp
using System.Net;
using FluentAssertions;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Režim okna u bubliny skutečnosti určuje server (2026-10-13): „edit" jen v editoru záznamu
/// držiteli vyjadreni.modal.open, všude jinde (stránka záznamu, editor i detail návrhu)
/// „read". Návrhy ho mít musí — editační okno by měnilo vazby mimo schvalování (spec §5.2).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class VyjadreniOknoRezimRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public VyjadreniOknoRezimRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<(int ProjectId, int RecordId, int DeputyId)> SeedAsync(string marker, string cislo)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var deputyId = await _fixture.EnsurePersonAsync($"{marker}Deputy");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        await _fixture.EnsureSubsystemRoleAsync(projectId, subsystemId, deputyId, SubsystemRoleCodes.DeputyLead);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} zaznam");
        await _fixture.SeedDatumScheduleAsync(recordId, DateTime.UtcNow.AddDays(-30), new HashSet<int> { 1 });
        await _fixture.SeedTicketBindingAsync(recordId, cislo, "PMP", poradi: 3, hotVyjadreniId: 7101, datum: DateTime.UtcNow.Date.AddDays(-7));
        _fixture.ServiceDesk.Tikety[cislo] = new HotZaznamDto(cislo, "PMP", "Strucne", "Popis");
        return (projectId, recordId, deputyId);
    }

    private static string ScheduleTable(string html)
    {
        var start = html.IndexOf("schedule-table-wrap", StringComparison.Ordinal);
        start.Should().BeGreaterThan(0, "stránka renderuje tabulku harmonogramu");
        return html[start..html.IndexOf("</table>", start, StringComparison.Ordinal)];
    }

    [Fact]
    public async Task StrankaZaznamu_Zastupce_VidiBublinuVRezimuCteni()
    {
        var (_, recordId, deputyId) = await SeedAsync("ApiRezim1", "814001");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Detail/{recordId}?asUser={deputyId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        var table = ScheduleTable(html);
        table.Should().Contain("data-external-chat-open", "bublinu vidí každý, kdo vidí záznam");
        table.Should().Contain("data-external-chat-mode=\"read\"");
        table.Should().NotContain("data-external-chat-mode=\"edit\"");
    }

    [Fact]
    public async Task StrankaZaznamu_Admin_TakeJenCteni()
    {
        var (_, recordId, _) = await SeedAsync("ApiRezim2", "814002");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var html = await (await client.GetAsync($"/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}")).Content.ReadAsStringAsync();

        ScheduleTable(html).Should().Contain("data-external-chat-mode=\"read\"", "stránka záznamu je celá jen pro čtení");
    }

    [Fact]
    public async Task NavrhHarmonogramu_Zastupce_BublinaVRezimuCteni()
    {
        var (projectId, recordId, deputyId) = await SeedAsync("ApiRezim3", "814003");

        using var client = _fixture.ServiceDeskFactory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Navrhy/CreateScheduleProposal?projektId={projectId}&zaznamId={recordId}&asUser={deputyId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-external-chat-open", "kandidát z (falešného) SD dá kroku zdroj");
        html.Should().Contain("data-external-chat-mode=\"read\"");
        html.Should().NotContain("data-external-chat-mode=\"edit\"");
    }

    [Fact]
    public async Task EditorZaznamu_DrzitelKliceOkna_BublinaVRezimuUprav()
    {
        var (projectId, recordId, _) = await SeedAsync("ApiRezim4", "814004");

        using var client = _fixture.ServiceDeskFactory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Edit/{recordId}?projektId={projectId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-external-chat-mode=\"edit\"");
    }
}
```

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~VyjadreniOknoRezimRenderTests"`
Expected: Neúspěšné všechny čtyři (zástupce bublinu na stránce nevidí; atribut `data-external-chat-mode` neexistuje).

- [ ] **Step 2: View modely**

V `ProjektHarmonogramTabViewModels.cs` do `HarmonogramBlockViewModel` (za `CanEditManualActual`) přidej:

```csharp
    /// <summary>
    /// Bublina skutečnosti otevře editační okno (přiřazování kroků, klíč vyjadreni.modal.open).
    /// Výchozí false = okno jen pro čtení (GET /Zaznamy/VyjadreniNahled), které smí každý, kdo
    /// záznam vidí. true nastavuje výhradně editor záznamu (ZaznamyController.PrepareRecordEditorModel);
    /// návrhy a stránka záznamu zůstávají jen pro čtení (spec 2026-04-21 §5.2).
    /// </summary>
    public bool VyjadreniOknoUpravy { get; init; }
```

Do `ScheduleBlockManualCellViewModel` přidej:

```csharp
    /// <summary>Viz <see cref="HarmonogramBlockViewModel.VyjadreniOknoUpravy"/>.</summary>
    public bool VyjadreniOknoUpravy { get; init; }
```

- [ ] **Step 3: Propsání do buňky a tlačítka**

V `_ScheduleTable.cshtml` ve všech třech `new ScheduleBlockManualCellViewModel { … }` (ř. 137, 150, 163) přidej řádek `VyjadreniOknoUpravy = Model.VyjadreniOknoUpravy,` (za `IsLocked = isKrokLocked` doplň čárku).

V `_ScheduleBlockManualCell.cshtml` do obou `<pm-button … data-external-chat-open="true" …>` (ř. 139 a 227) přidej atribut:

```cshtml
                       data-external-chat-mode="@(Model.VyjadreniOknoUpravy ? "edit" : "read")"
```

- [ ] **Step 4: Editor nastaví „edit“**

V `ZaznamyController.cs` v `PrepareRecordEditorModel` rozšiř závěrečné `with`:

```csharp
        model.HarmonogramBlok = model.HarmonogramBlok with
        {
            Permissions = schedulePermissions,
            CanEditManualActual = schedulePermissions.CanEditManualActual,
            // Editační okno vyjádření (přiřazování kroků) jen v editoru a jen s klíčem okna.
            VyjadreniOknoUpravy = CurrentUserContext.HasPermission(PermissionKeys.VyjadreniModalOpen, model.ProjektId)
        };
```

- [ ] **Step 5: Stránka záznamu — zdroj vždy, bez `canOpenVyjadreni`**

V `ZaznamyController.Detail` smaž komentář a řádek `var canOpenVyjadreni = …` (ř. 70–74) a volání změň na `await _recordService.BuildRecordDetailPageAsync(projektId.Value, id, ct);`.

V `ProjectService.RecordDetailPage.cs` smaž delegující přetížení (ř. 18–19) i `<param name="canOpenVyjadreni">` a sjednoť na jednu metodu `public async Task<ZaznamDetailPageViewModel?> BuildRecordDetailPageAsync(int projectId, int recordId, CancellationToken ct = default)`. Ve `scheduleTable` nahraď ternár `Kroky = canOpenVyjadreni ? … : …` a doplň režim:

```csharp
                CanEditManualActual = false,
                // Odkaz na zdrojový tiket vidí každý, kdo vidí záznam — okno jen pro čtení
                // (2026-10-13). Výběr vazby = stejné pravidlo jako editor (ResolveMatched).
                VyjadreniOknoUpravy = false,
                Kroky = await AttachSourceVyjadreniAsync(recordId, schedule.HarmonogramBlok.Kroky, ct)
```

V `IProjectService.cs` smaž ř. 21, v `IRecordService.cs` ř. 12, v `RecordService.cs` ř. 70–71 (přetížení s `bool canOpenVyjadreni`). V test fakes `ProjektyControllerBehaviorTests.cs:490` a `ProjektyControllerTeamAuthzTests.cs:229` smaž stejné přetížení.

- [ ] **Step 6: Strážní unit test místo starého pravidla**

V `RecordScheduleToggleJsTests.cs` nahraď test `SourceVyjadreniLink_IsGatedByModalOpenPermission` (ř. 22–43):

```csharp
    /// <summary>
    /// 2026-10-13: odkaz „odkud skutečnost pochází" vidí každý, kdo vidí záznam — otevře okno
    /// tiketu jen pro čtení (bez klíče). Editační okno nabízí jen editor záznamu.
    /// </summary>
    [Fact]
    public void SourceVyjadreniLink_JeVzdyJenProCteni()
    {
        var controller = File.ReadAllText(Path.Combine(RepoRoot(), "PmTracker.Web/Controllers/ZaznamyController.cs"));
        controller.Should().NotContain("canOpenVyjadreni", "stránka záznamu už odkaz podle klíče neskrývá");
        controller.Should().Contain("VyjadreniOknoUpravy = CurrentUserContext.HasPermission(PermissionKeys.VyjadreniModalOpen",
            "režim úprav nastavuje jen editor");

        var composition = File.ReadAllText(Path.Combine(RepoRoot(), "PmTracker.Web/Services/ProjectService.RecordDetailPage.cs"));
        composition.Should().Contain("Kroky = await AttachSourceVyjadreniAsync", "zdroj se doplňuje vždy");
        composition.Should().Contain("VyjadreniOknoUpravy = false");

        var cell = File.ReadAllText(Path.Combine(RepoRoot(), "PmTracker.Web/Views/Shared/_ScheduleBlockManualCell.cshtml"));
        cell.Should().Contain("data-external-chat-mode=");
    }
```

- [ ] **Step 7: `chatModal.js` — režim, identita, hláška 403**

1. Na začátek souboru (před IIFE) vlož `import { appendCurrentAsUser } from "../navigationShared.js";` a hlavičkový komentář uprav na: „Fetchuje partial okna vyjádření — `/Zaznamy/VyjadreniNahled` (jen pro čtení) nebo `/Vyjadreni/Modal` (úpravy) — vloží do gov-dialog …“.
2. Za `let currentCtx = null;` přidej:

```js
  // Režim okna určuje server atributem data-external-chat-mode na bublině (2026-10-13):
  // "read" = obsah tiketu bez úprav (stačí vidět záznam), "edit" = přiřazování kroků
  // (klíč vyjadreni.modal.open). Bez atributu "edit" — karty externí vazby v editoru a
  // SD konektor (inspect.js volá open(id)) se chovají jako dřív.
  const MODE_READ = 'read';
  const MODE_EDIT = 'edit';

  function modalUrl(ctx) {
    const query = 'externiOdkazId=' + encodeURIComponent(ctx.externiOdkazId)
      + '&zaznamId=' + encodeURIComponent(ctx.zaznamId);
    return appendCurrentAsUser(ctx.mode === MODE_READ
      ? '/Zaznamy/VyjadreniNahled?' + query
      : '/Vyjadreni/Modal?' + query);
  }
```

3. `open`:

```js
  async function open(externiOdkazId, zaznamId, mode) {
    currentCtx = {
      externiOdkazId: String(externiOdkazId),
      zaznamId: String(zaznamId),
      mode: mode === MODE_READ ? MODE_READ : MODE_EDIT
    };
```

4. V `loadInto` nahraď sestavení `url` a `fetch` + `!resp.ok` větev:

```js
      const resp = await fetch(modalUrl(currentCtx), { credentials: 'same-origin', headers: { 'Accept': 'text/html' } });
      if (!resp.ok) {
        container.innerHTML = resp.status === 403
          ? '<gov-alert variant="error">K úpravě přiřazení vyjádření nemáte oprávnění.</gov-alert>'
          : `<gov-alert variant="error">Nepodařilo se načíst vyjádření (HTTP ${resp.status}).</gov-alert>`;
        return;
      }
```

5. V `onClick` poslední řádek `open(externiOdkazId, zaznamId);` změň na `open(externiOdkazId, zaznamId, btn.getAttribute('data-external-chat-mode'));`.

- [ ] **Step 8: Testy zelené**

Run: `dotnet build PmTracker.sln` → bez chyb.
Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~RecordScheduleToggleJsTests|FullyQualifiedName~ProjektyController|FullyQualifiedName~ChatModal|FullyQualifiedName~JsBundle"` → Úspěšné.
Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~VyjadreniOknoRezimRenderTests|FullyQualifiedName~RecordDetailPageRenderTests"` → Úspěšné. Existující `DetailPage_TableView_LinksToSourceVyjadreni` zůstane zelený (odkaz dál existuje).

- [ ] **Step 9: E2E — zástupce klikne na bublinu na stránce záznamu**

Vytvoř `PmTracker.Tests.E2E/Scenarios/TicketReadOnlyWindowScenariosTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// 2026-10-13: zástupce vedoucího subsystému (bez vyjadreni.modal.open) klikne na stránce
/// záznamu na bublinu vytěžené skutečnosti a otevře se okno tiketu jen pro čtení — dřív
/// „Nepodařilo se načíst vyjádření" (403). SD je v E2E vypnutý, okno proto ukáže prázdný
/// obsah; ověřuje se režim, odpověď 200 a absence ovládání.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class TicketReadOnlyWindowScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public TicketReadOnlyWindowScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private async Task<(int RecordId, int DeputyId)> SeedAsync()
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @kat INT = (SELECT TOP (1) id FROM dbo.ciselnik_kategorii_zaznamu
                WHERE kod = N'U' OR nazev LIKE N'%kol%' ORDER BY CASE WHEN kod = N'U' THEN 0 ELSE 1 END, id);
            DECLARE @stav INT = (SELECT TOP (1) id FROM dbo.ciselnik_stavu_ukolu ORDER BY id);
            DECLARE @sub INT = (SELECT TOP (1) id FROM dbo.subsystemy ORDER BY id);

            IF NOT EXISTS (SELECT 1 FROM dbo.projekt_subsystemy WHERE projekt_id = @p AND subsystem_id = @sub AND datum_odebrani IS NULL)
                INSERT INTO dbo.projekt_subsystemy (projekt_id, subsystem_id, datum_prirazeni) VALUES (@p, @sub, SYSUTCDATETIME());
            DECLARE @ps INT = (SELECT TOP (1) id FROM dbo.projekt_subsystemy
                WHERE projekt_id = @p AND subsystem_id = @sub AND datum_odebrani IS NULL ORDER BY id);

            DECLARE @deputy INT = (SELECT TOP (1) id FROM dbo.osoby WHERE email = N'e2e.zastupce@test.local');
            IF @deputy IS NULL
            BEGIN
                INSERT INTO dbo.osoby (jmeno, prijmeni, email, organizacni_celek_id, organizace_id, location_locked)
                SELECT N'E2E', N'Zastupce', N'e2e.zastupce@test.local', organizacni_celek_id, organizace_id, 0
                FROM dbo.osoby WHERE id = @admin;
                SET @deputy = CAST(SCOPE_IDENTITY() AS INT);
            END
            DECLARE @role INT = (SELECT id FROM dbo.ciselnik_roli_subsystemu WHERE kod = N'ZASTUPCE_VEDOUCIHO_SUBSYSTEMU');
            IF NOT EXISTS (SELECT 1 FROM dbo.obsazeni_subsystemu_projektu
                WHERE projekt_subsystem_id = @ps AND osoba_id = @deputy AND role_subsystemu_id = @role AND datum_odebrani IS NULL)
                INSERT INTO dbo.obsazeni_subsystemu_projektu (projekt_subsystem_id, osoba_id, role_subsystemu_id, datum_prirazeni)
                VALUES (@ps, @deputy, @role, SYSUTCDATETIME());

            DECLARE @rid INT = (SELECT TOP (1) id FROM dbo.projektove_zaznamy WHERE projekt_id = @p AND nazev = @marker ORDER BY id);
            IF @rid IS NULL
            BEGIN
                DECLARE @num INT = (SELECT ISNULL(MAX(cislo_zaznamu), 0) + 1 FROM dbo.projektove_zaznamy WHERE projekt_id = @p);
                INSERT INTO dbo.projektove_zaznamy
                    (projekt_id, kategorie_id, stav_ukolu_id, cislo_zaznamu, nazev, cil, popis,
                     vlastnik_id, datum_zalozeni, datum_ukonceni, subsystem_id)
                VALUES (@p, @kat, @stav, @num, @marker, @marker, @marker,
                        @admin, DATEADD(day, -30, CONVERT(DATE, GETDATE())), DATEADD(day, 45, CONVERT(DATE, GETDATE())), @sub);
                SET @rid = CAST(SCOPE_IDENTITY() AS INT);
            END

            DELETE FROM dbo.zaznam_harmonogram_vyjadreni_vazba WHERE zaznam_id = @rid;
            DELETE FROM dbo.zaznam_externi_odkazy WHERE zaznam_id = @rid;
            DELETE FROM dbo.zaznam_harmonogram_krok WHERE zaznam_id = @rid;
            DECLARE @start DATE = DATEADD(day, -30, CONVERT(DATE, GETDATE()));
            DECLARE @i INT = 1;
            WHILE @i <= 10
            BEGIN
                -- Krok 3 = skutečnost vytěžená automatem (zdroj 1, režim Auto 0).
                INSERT INTO dbo.zaznam_harmonogram_krok
                    (zaznam_id, poradi, plan_datum, skutecnost_datum, skutecnost_zdroj, skutecnost_rezim, updated_at)
                VALUES (@rid, @i, DATEADD(day, @i * 7, @start),
                        CASE WHEN @i = 3 THEN DATEADD(day, 20, @start) ELSE NULL END,
                        CASE WHEN @i = 3 THEN 1 ELSE 0 END, 0, SYSUTCDATETIME());
                SET @i = @i + 1;
            END

            DECLARE @typ INT = (SELECT id FROM dbo.ciselnik_typu_externich_odkazu WHERE kod = N'PMP');
            INSERT INTO dbo.zaznam_externi_odkazy (zaznam_id, typ_odkazu_id, cislo, zaradid_do_vyzvy) VALUES (@rid, @typ, N'815001', 0);
            DECLARE @eo INT = CAST(SCOPE_IDENTITY() AS INT);
            INSERT INTO dbo.zaznam_harmonogram_vyjadreni_vazba
                (zaznam_id, externi_odkaz_id, hot_vyjadreni_id, datum_vyjadreni, source, stav, created_at, poradi)
            VALUES (@rid, @eo, 7201, DATEADD(day, 20, @start), 1, 1, SYSUTCDATETIME(), 3);

            SELECT @rid AS rid, @deputy AS deputy;
            """;
        command.Parameters.AddWithValue("@p", _fixture.ProjectId);
        command.Parameters.AddWithValue("@admin", _fixture.AdminOsobaId);
        command.Parameters.AddWithValue("@marker", "E2E okno tiketu jen pro cteni");
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue("seed vrací id záznamu a zástupce");
        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    private static async Task DispatchClickAsync(ILocator locator)
    {
        await locator.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
        await locator.First.DispatchEventAsync("click");
    }

    [Fact]
    public async Task Zastupce_KlikNaBublinu_OtevreOknoJenProCteni()
    {
        var (recordId, deputyId) = await SeedAsync();
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Zaznamy/Detail/{recordId}?asUser={deputyId}");

        await DispatchClickAsync(page.Locator("[data-schedule-view-toggle='table']"));
        var bubble = page.Locator(".schedule-table-wrap [data-external-chat-open][data-external-chat-mode='read']");
        await Assertions.Expect(bubble).ToHaveCountAsync(1);

        var responseTask = page.WaitForResponseAsync(r => r.Url.Contains("/Zaznamy/VyjadreniNahled"));
        await DispatchClickAsync(bubble);
        var response = await responseTask;
        response.Status.Should().Be(200, "okno jen pro čtení nepotřebuje klíč vyjadreni.modal.open");
        response.Url.Should().Contain($"asUser={deputyId}", "AJAX nese identitu přes appendCurrentAsUser");

        await Assertions.Expect(page.Locator("[data-chat-modal-root][data-chat-modal-mode='read']")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("[data-chat-modal-root] [data-bubble-step-selector]")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("[data-chat-modal-root] [data-reharvest-form]")).ToHaveCountAsync(0);

        await page.Context.CloseAsync();
    }
}
```

Run: `dotnet test PmTracker.Tests.E2E --filter "FullyQualifiedName~TicketReadOnlyWindowScenariosTests"`
Expected: Úspěšné.

- [ ] **Step 10: Dokumentace**

- `docs/wiki/integrace/servicedesk/chat-vyjadreni.md`, sekce „Kde najít“ (ř. 16–24) nahraď:

```markdown
## Kde najít

Okno se otevírá kliknutím na bublinu 💬 u vytěžené skutečnosti kroku harmonogramu (tabulka
na stránce záznamu, editor záznamu, editor a detail návrhu) a v editoru záznamu tlačítkem
💬 na kartě externí vazby.

Okno má dva režimy:

- **Jen pro čtení** — obsah tiketu ze ServiceDesku a přiřazení vyjádření ke krokům (štítek
  „Krok N“), bez možnosti je měnit. Otevře ho každý, kdo vidí záznam; žádný klíč není
  potřeba. Takto se okno otevírá vždy ze stránky záznamu a z návrhů.
- **Úpravy** — přiřazování a odpojování kroků, znovuvytěžení. Jen v editoru záznamu a jen
  s klíčem `vyjadreni.modal.open`; jednotlivé akce hlídají `vyjadreni.vazba.create`,
  `vyjadreni.vazba.delete` a `vyjadreni.reharvest`.

Když ServiceDesk neodpovídá, okno se otevře s varováním a ukáže přiřazení kroků podle
posledního vytěžení.
```

- `docs/wiki/nastaveni-administrace/akce-permissions/index.md:89` → `| \`vyjadreni.modal.open\` | PROJECT | Editační okno vyjádření v editoru záznamu (přiřazování kroků). Okno jen pro čtení klíč nepotřebuje. |`

- [ ] **Step 11: Commit**

```bash
git add PmTracker.Web PmTracker.Tests.Unit PmTracker.Tests.Api PmTracker.Tests.E2E docs/wiki
git commit -m "fix(vyjadreni): bublina skutečnosti otevírá okno tiketu jen pro čtení

Režim okna určuje server: úpravy jen v editoru záznamu s vyjadreni.modal.open,
stránka záznamu a návrhy vždy jen pro čtení (spec 2026-04-21 §5.2). Stránka
záznamu už odkaz na zdrojový tiket neskrývá podle klíče. Zástupce vedoucího
subsystému dřív dostal po kliknutí na bublinu „Nepodařilo se načíst“ (403).

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Typ tiketu v návrhu bez `externiodkazy.sync` (bod 3)

**Files:**
- Create: `PmTracker.Web/Models/ViewModels/ExterniOdkaz/ExterniOdkazTypResponse.cs`
- Modify: `PmTracker.Web/Controllers/ExterniOdkazController.cs` (nová akce, XML komentář třídy)
- Modify: `PmTracker.Web/wwwroot/js/modules/externiOdkaz/sync.js:120-192, 318-326`
- Modify: `PmTracker.Web/wwwroot/css/components/externi-odkaz-card.css` (za ř. 169)
- Modify: `PmTracker.Web/Views/Projekty/_EditZaznamExternalPanel.cshtml:9`
- Modify: `PmTracker.Tests.E2E/Scenarios/RecordProposalExternalTypeScenariosTests.cs:28-37`
- Test: `PmTracker.Tests.Api/Controllers/ExterniOdkazTypTests.cs` (nový), `PmTracker.Tests.Unit/ExterniOdkaz/ExterniOdkazSyncJsTests.cs`

**Interfaces:**
- Consumes: `ServiceDeskFactory`, `EnsureSubsystemRoleAsync`.
- Produces: `GET /ExterniOdkaz/Typ?cislo={6 číslic}&projektId={int}` → `200 {nalezeno, cislo, typ, strucne, popis}` | `400` | `403`. Smí kdo má v projektu `externiodkazy.sync` NEBO `proposals.record.create`.
- Produces: řádek externí vazby nese `data-sync-error="forbidden"|"unavailable"` (místo falešného `data-not-found`); panel nese `data-external-readonly="true"|"false"`.

- [ ] **Step 1: Padající Api testy**

Vytvoř `PmTracker.Tests.Api/Controllers/ExterniOdkazTypTests.cs`:

```csharp
using System.Net;
using FluentAssertions;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Editor návrhu založení potřebuje jen typ tiketu (2026-10-13). Dřív volal /ExterniOdkaz/Sync
/// (klíč externiodkazy.sync), takže zástupci vedoucího subsystému hlásil u existujícího
/// tiketu „Tiket se nepodařilo dohledat".
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ExterniOdkazTypTests
{
    private readonly ApiSqlFixture _fixture;

    public ExterniOdkazTypTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<(int ProjectId, int DeputyId, int OutsiderId)> SeedAsync(string marker)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var deputyId = await _fixture.EnsurePersonAsync($"{marker}Deputy");
        var outsiderId = await _fixture.EnsurePersonAsync($"{marker}Outsider");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        await _fixture.EnsureSubsystemRoleAsync(projectId, subsystemId, deputyId, SubsystemRoleCodes.DeputyLead);
        return (projectId, deputyId, outsiderId);
    }

    [Fact]
    public async Task Typ_ZastupceSNavrhemZalozeni_DostaneNormalizovanyTyp()
    {
        var (projectId, deputyId, _) = await SeedAsync("ApiTyp1");
        _fixture.ServiceDesk.Tikety["816001"] = new HotZaznamDto("816001", "PNF  ", "Strucne", "Popis tiketu");

        using var client = _fixture.ServiceDeskFactory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/ExterniOdkaz/Typ?cislo=816001&projektId={projectId}&asUser={deputyId}");
        var json = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, json);
        json.Should().Contain("\"nalezeno\":true").And.Contain("\"typ\":\"PNF\"", "CHAR(5) padding se ořízne");
        json.Should().Contain("\"popis\":\"Popis tiketu\"", "PNF předvyplňuje text požadavku");
        json.Should().NotContain("vyjadreni", "endpoint nevrací obsah tiketu ani harvest");
    }

    [Fact]
    public async Task Typ_BezKlicu_Vraci403()
    {
        var (projectId, _, outsiderId) = await SeedAsync("ApiTyp2");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/ExterniOdkaz/Typ?cislo=816002&projektId={projectId}&asUser={outsiderId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Typ_NeplatneCislo_Vraci400()
    {
        var (projectId, deputyId, _) = await SeedAsync("ApiTyp3");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/ExterniOdkaz/Typ?cislo=12ab&projektId={projectId}&asUser={deputyId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ExterniOdkazTypTests"`
Expected: Neúspěšné (endpoint neexistuje → 404/405).

- [ ] **Step 2: Response a akce**

Vytvoř `PmTracker.Web/Models/ViewModels/ExterniOdkaz/ExterniOdkazTypResponse.cs`:

```csharp
namespace PmTracker.Web.Models.ViewModels.ExterniOdkaz;

/// <summary>
/// Odpověď GET /ExterniOdkaz/Typ (2026-10-13) — jen metadata tiketu pro otypování externí
/// vazby v editoru návrhu: typ (NES/PMP/PNF, normalizovaný), stručně a popis (PNF jím
/// předvyplní text požadavku). Žádná vyjádření ani vytěžené datumy.
/// </summary>
public sealed record ExterniOdkazTypResponse(bool Nalezeno, string Cislo, string? Typ, string? Strucne, string? Popis);
```

Do `ExterniOdkazController.cs` (using `PmTracker.Web.Services.ServiceDesk` pro `VyjadreniHarvestService`) za `Sync` přidej:

```csharp
    /// <summary>
    /// Jen typ tiketu pro editor návrhu založení (2026-10-13). Smí, kdo má v projektu
    /// externiodkazy.sync NEBO proposals.record.create — dva [Authorize(Policy)] atributy by se
    /// vyhodnotily jako AND, proto OR v těle. GET bez vedlejších efektů (žádný harvest).
    /// </summary>
    [HttpGet("Typ")]
    public async Task<IActionResult> Typ([FromQuery] string? cislo, [FromQuery] int projektId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cislo) || !SixDigits.IsMatch(cislo))
        {
            return BadRequest(new { error = "Číslo tiketu musí být přesně 6 cifer." });
        }

        var osobaId = _currentUser.OsobaId;
        if (osobaId is null)
        {
            return Forbid();
        }

        var smi = await _authz.HasPermissionAsync(osobaId.Value, PermissionKeys.ExterniOdkazySync, projektId, null, ct)
            || await _authz.HasPermissionAsync(osobaId.Value, PermissionKeys.ProposalsRecordCreate, projektId, null, ct);
        if (!smi)
        {
            return Forbid();
        }

        var dto = await _ticketing.GetZaznamAsync(cislo, ct);
        var typ = VyjadreniHarvestService.NormalizeTypZaznamu(dto?.TypZaznamu);
        return Ok(new ExterniOdkazTypResponse(
            Nalezeno: dto is not null,
            Cislo: cislo,
            Typ: typ.Length == 0 ? null : typ,
            Strucne: dto?.Strucne,
            Popis: dto?.Popis));
    }
```

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ExterniOdkazTypTests"` → Úspěšné.

- [ ] **Step 3: Padající JS strážní test**

Do `ExterniOdkazSyncJsTests.cs` přidej:

```csharp
    [Fact]
    public void Sync_TypVNavrhuBezHarvestu_ChybyNejsouNenalezeno_DetailNavrhuNesynchronizuje()
    {
        var sync = Js("externiOdkaz/sync.js");

        sync.Should().Contain("'/ExterniOdkaz/Typ?cislo='", "editor návrhu potřebuje jen typ — bez klíče externiodkazy.sync");
        sync.Should().Contain("appendCurrentAsUser", "AJAX nese identitu stejně jako ostatní moduly");
        sync.Should().Contain("resp.status === 403", "chybějící oprávnění se nesmí hlásit jako nenalezený tiket");
        sync.Should().Contain("data-sync-error");
        sync.Should().Contain("[data-external-readonly=\"true\"]", "detail návrhu je jen pro čtení, nesynchronizuje se");
    }
```

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ExterniOdkazSyncJsTests"` → Neúspěšné.

- [ ] **Step 4: `sync.js`**

1. Na začátek souboru (před IIFE): `import { appendCurrentAsUser } from "../navigationShared.js";`
2. Za `getProjektId` přidej:

```js
  function buildSyncForm(cislo, projektId) {
    const form = new FormData();
    form.append('cislo', cislo);
    form.append('projektId', projektId);
    form.append('__RequestVerificationToken', getCsrfToken());
    return form;
  }

  // 2026-10-13: ověření tiketu selhalo jinak než „nenalezeno" — chybí oprávnění (403) nebo
  // ServiceDesk neodpověděl. Dřív obojí končilo hláškou „Tiket se nepodařilo dohledat".
  function setSyncError(row, kind) {
    row.removeAttribute('data-not-found');
    if (kind) {
      row.setAttribute('data-sync-error', kind);
    } else {
      row.removeAttribute('data-sync-error');
    }
  }
```

3. V `syncCislo` nahraď blok od `const form = new FormData();` po `const data = await resp.json();`:

```js
    try {
      // typeOnly (editor návrhu): jen typ tiketu — GET /ExterniOdkaz/Typ smí i držitel
      // proposals.record.create. Plný sync (vyjádření + 4 datumy) jen v editoru záznamu.
      const resp = typeOnly
        ? await fetch(appendCurrentAsUser('/ExterniOdkaz/Typ?cislo=' + encodeURIComponent(cislo)
            + '&projektId=' + encodeURIComponent(projektId)), {
            credentials: 'same-origin',
            headers: { 'Accept': 'application/json' },
          })
        : await fetch('/ExterniOdkaz/Sync', {
            method: 'POST',
            body: buildSyncForm(cislo, projektId),
            credentials: 'same-origin',
          });
      if (resp.status === 403) {
        setDatesVisible(row, false);
        setSyncError(row, 'forbidden');
        return;
      }
      if (!resp.ok) throw new Error('HTTP ' + resp.status);
      const data = await resp.json();
      setSyncError(row, null);
```

4. V `catch (err)` větvi nahraď `row.setAttribute('data-not-found', 'true');` za `setSyncError(row, 'unavailable');`.
5. V neplatném čísle na začátku `syncCislo` (větev `if (!/^\d{6}$/.test(cislo))`) za `row.removeAttribute('data-not-found');` přidej `row.removeAttribute('data-sync-error');`.
6. `syncPrefilledInputs`:

```js
  function syncPrefilledInputs() {
    document.querySelectorAll('[data-external-cislo]').forEach(function (input) {
      // Detail návrhu (rozhodnutí) je jen pro čtení: typ i datumy vykreslil server. Synchronizace
      // by nic nezměnila a uživateli bez klíče by jen označila tikety jako chybné (2026-10-13).
      if (input.closest('[data-external-readonly="true"]')) return;
      if (input.value && /^\d{6}$/.test(input.value.trim())) {
```

(Zbytek funkce beze změny. Větev `if (!typeOnly)` zůstává — hlídá ji `NavrhyRedesignTests`.)

- [ ] **Step 5: Panel a CSS**

`_EditZaznamExternalPanel.cshtml:9` → `<div data-external-links-editor data-external-readonly="@(externalReadonly ? "true" : "false")">`.

Do `externi-odkaz-card.css` za blok `.external-row[data-not-found]::after { … }`:

```css
/* 2026-10-13: ověření tiketu selhalo jinak než „nenalezeno" — chybějící oprávnění (403)
   nebo výpadek ServiceDesku se nesmí tvářit jako neexistující tiket. */
.external-row[data-sync-error] {
  background: var(--pm-warning-subtle, #fef3c7);
  border-color: var(--pm-warning, #f59e0b);
}

.external-row[data-sync-error]::after {
  grid-column: 1 / -1;
  grid-row: 3;
  color: var(--pm-warning-strong, #92400e);
  font-size: 0.8125rem;
  padding-top: 0.5rem;
}

.external-row[data-sync-error="forbidden"]::after {
  content: "K ověření tiketu nemáte oprávnění.";
}

.external-row[data-sync-error="unavailable"]::after {
  content: "Tiket se teď nepodařilo ověřit, ServiceDesk neodpovídá.";
}
```

- [ ] **Step 6: E2E stub na nový endpoint**

V `RecordProposalExternalTypeScenariosTests.cs` doplň `using System.Text.RegularExpressions;` a stub (ř. 27–37) nahraď:

```csharp
        // Stub typu tiketu: editor návrhu volá GET /ExterniOdkaz/Typ (jen typ, bez harvestu).
        await page.RouteAsync(new Regex(@"/ExterniOdkaz/Typ\?"), async route =>
        {
            await route.FulfillAsync(new()
            {
                Status = 200,
                ContentType = "application/json",
                Body = "{\"nalezeno\":true,\"cislo\":\"334565\",\"typ\":\"PMP\",\"strucne\":\"Stub\",\"popis\":null}"
            });
        });
```

(a uprav komentář třídy: „…SD typ je stubovaný přes route interception na /ExterniOdkaz/Typ.“)

- [ ] **Step 7: Testy zelené**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ExterniOdkaz|FullyQualifiedName~NavrhyRedesignTests|FullyQualifiedName~Architecture"` → Úspěšné.
Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ExterniOdkaz"` → Úspěšné.
Run: `dotnet test PmTracker.Tests.E2E --filter "FullyQualifiedName~RecordProposalExternalTypeScenariosTests"` → Úspěšné.

- [ ] **Step 8: Commit**

```bash
git add PmTracker.Web PmTracker.Tests.Unit PmTracker.Tests.Api PmTracker.Tests.E2E
git commit -m "fix(navrhy): typ tiketu v návrhu založení i bez externiodkazy.sync

Editor návrhu volá GET /ExterniOdkaz/Typ (externiodkazy.sync NEBO
proposals.record.create) místo plného syncu, detail návrhu se už nesynchronizuje
a chybějící oprávnění či výpadek SD se nehlásí jako nenalezený tiket.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Formulář vyjádření podle `CommentAuthorizationPolicy` (bod 4)

**Files:**
- Modify: `PmTracker.Web/Services/ICommentService.cs`, `PmTracker.Web/Services/CommentService.cs:21-56, 305-311`
- Modify: `PmTracker.Web/Services/IRecordService.cs`, `PmTracker.Web/Services/RecordService.cs`
- Modify: `PmTracker.Web/Controllers/ZaznamyController.Partials.cs:60-83, 85-117, 118-140`
- Modify: `PmTracker.Web/Controllers/ZaznamyController.cs:75-106` (Detail + `PrepareRecordDetailPagePresentation`)
- Modify: `PmTracker.Web/Controllers/ProjektyController.cs:277-279`
- Modify: `PmTracker.Web/Models/ViewModels/Projekty/ProjektZaznamyTabViewModels.cs:90, 178`
- Test: `PmTracker.Tests.Api/Controllers/RecordCommentsFormAuthzRenderTests.cs` (nový)

**Interfaces:**
- Consumes: `EnsureSubsystemRoleAsync`.
- Produces: `ICommentService.GetMeetingIdsAllowingNewCommentAsync(int zaznamId, IReadOnlyCollection<JednaniOptionViewModel> meetings, CurrentUserContextViewModel currentUser, CancellationToken ct = default) : Task<IReadOnlySet<int>>` + stejnojmenná fasáda v `IRecordService`.

- [ ] **Step 1: Padající Api testy**

Vytvoř `PmTracker.Tests.Api/Controllers/RecordCommentsFormAuthzRenderTests.cs`:

```csharp
using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Formulář vyjádření nabídne jen jednání, kde by AddComment prošel (2026-10-13) — stejná
/// CommentAuthorizationPolicy jako server. Zástupce vedoucího subsystému: jen vlastní
/// subsystém a jen DRAFT jednání (rozhodnutí 2026-06-16). Dřív u cizího subsystému editor
/// ukázal se všemi jednáními a server uložení odmítl.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class RecordCommentsFormAuthzRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public RecordCommentsFormAuthzRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private sealed record Scenario(int ProjectId, int DeputyId, int PmId, int OwnRecordId, int ForeignRecordId, int DraftId, int OpenId);

    private async Task<Scenario> SeedAsync(string marker, int meetingBase)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var deputyId = await _fixture.EnsurePersonAsync($"{marker}Deputy");
        var pmId = await _fixture.EnsurePersonAsync($"{marker}Pm");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        var ownSubsystemId = await _fixture.EnsureSubsystemAsync($"{marker}A", ownerId);
        var foreignSubsystemId = await _fixture.EnsureSubsystemAsync($"{marker}B", ownerId);
        await _fixture.EnsureSubsystemRoleAsync(projectId, ownSubsystemId, deputyId, SubsystemRoleCodes.DeputyLead);
        await _fixture.EnsureProjectRoleAssignmentAsync(projectId, pmId, "PROJ_MAN");
        var ownRecordId = await _fixture.EnsureRecordAsync(projectId, ownerId, ownSubsystemId, "U", $"{marker} vlastni");
        var foreignRecordId = await _fixture.EnsureRecordAsync(projectId, ownerId, foreignSubsystemId, "U", $"{marker} cizi");
        var draftId = await _fixture.CreateMeetingAsync(projectId, "DRAFT", meetingBase);
        var openId = await _fixture.CreateMeetingAsync(projectId, "OPEN", meetingBase + 1);
        return new Scenario(projectId, deputyId, pmId, ownRecordId, foreignRecordId, draftId, openId);
    }

    private async Task<string> CommentsHtmlAsync(int projectId, int recordId, int asUser)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/RecordCommentsPartial?projektId={projectId}&zaznamId={recordId}&asUser={asUser}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return html;
    }

    [Fact]
    public async Task Zastupce_CiziSubsystem_FormularNenabidne()
    {
        var s = await SeedAsync("ApiCmt1", 9971);

        var html = await CommentsHtmlAsync(s.ProjectId, s.ForeignRecordId, s.DeputyId);

        html.Should().NotContain("action=\"/Zaznamy/AddComment\"", "server by uložení u cizího subsystému odmítl");
    }

    [Fact]
    public async Task Zastupce_VlastniSubsystem_NabidneJenJednaniVPriprave()
    {
        var s = await SeedAsync("ApiCmt2", 9973);

        var html = await CommentsHtmlAsync(s.ProjectId, s.OwnRecordId, s.DeputyId);

        html.Should().Contain("action=\"/Zaznamy/AddComment\"");
        html.Should().Contain($"<option value=\"{s.DraftId}\"");
        html.Should().NotContain($"<option value=\"{s.OpenId}\"", "otevřené jednání patří projektovým rolím");
    }

    [Fact]
    public async Task ProjektovyManazer_CiziSubsystem_NabidneObeJednani()
    {
        var s = await SeedAsync("ApiCmt3", 9975);

        var html = await CommentsHtmlAsync(s.ProjectId, s.ForeignRecordId, s.PmId);

        html.Should().Contain($"<option value=\"{s.DraftId}\"").And.Contain($"<option value=\"{s.OpenId}\"");
    }
}
```

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~RecordCommentsFormAuthzRenderTests"`
Expected: `Zastupce_CiziSubsystem_FormularNenabidne` Neúspěšné (formulář se vykreslí); ostatní dva projdou už teď — hlídají, že oprava nerozbije platné scénáře.

- [ ] **Step 2: Služba — sdílený kontext**

`ICommentService.cs` (using `PmTracker.Web.Models.ViewModels`):

```csharp
    /// <summary>
    /// Ke kterým z nabízených jednání smí uživatel k záznamu přidat vyjádření. Stejné vstupy
    /// i stejná ICommentAuthorizationPolicy.CanAddComment jako AddCommentAsync — UI tak
    /// nenabídne jednání, které by server při uložení odmítl (2026-10-13).
    /// </summary>
    Task<IReadOnlySet<int>> GetMeetingIdsAllowingNewCommentAsync(
        int zaznamId,
        IReadOnlyCollection<JednaniOptionViewModel> meetings,
        CurrentUserContextViewModel currentUser,
        CancellationToken ct = default);
```

`CommentService.cs` — v `AddCommentAsync` nahraď ř. 41–46 (výpočet `leadEquivalentBySubsystem`, `subsystemLeadEquivalentOsobaIds`, `isSubsystemLeadEquivalentInProject`):

```csharp
        var addContext = await ResolveAddCommentContextAsync(record.ProjektId, record.SubsystemId, currentUser.OsobaId, ct);
```

a ve volání `CanAddComment` použij `addContext.SubsystemLeadEquivalentOsobaIds` a `addContext.IsSubsystemLeadEquivalentInProject`. Na konec třídy (před `ResolveLeadEquivalentOsobaIdsAsync`) přidej:

```csharp
    public async Task<IReadOnlySet<int>> GetMeetingIdsAllowingNewCommentAsync(
        int zaznamId,
        IReadOnlyCollection<JednaniOptionViewModel> meetings,
        CurrentUserContextViewModel currentUser,
        CancellationToken ct = default)
    {
        if (meetings.Count == 0)
        {
            return new HashSet<int>();
        }

        var record = await dbContext.ProjektoveZaznamy
            .AsNoTracking()
            .Where(x => x.Id == zaznamId)
            .Select(x => new { x.ProjektId, x.SubsystemId })
            .FirstOrDefaultAsync(ct);
        if (record is null)
        {
            return new HashSet<int>();
        }

        var addContext = await ResolveAddCommentContextAsync(record.ProjektId, record.SubsystemId, currentUser.OsobaId, ct);
        return meetings
            .Where(meeting => commentAuthorizationPolicy.CanAddComment(
                currentUser,
                record.ProjektId,
                addContext.SubsystemLeadEquivalentOsobaIds,
                addContext.IsSubsystemLeadEquivalentInProject,
                meeting.IsDraft))
            .Select(meeting => meeting.Id)
            .ToHashSet();
    }

    /// <summary>
    /// Vstupy CommentAuthorizationPolicy.CanAddComment — jediné místo výpočtu, sdílené uložením
    /// i nabídkou jednání ve formuláři. Je osoba vedoucím/zástupcem NĚKTERÉHO subsystému projektu?
    /// Pak podléhá subsystémovému omezení (jen vlastní subsystém v DRAFT), nehledě na zděděný comments.add.
    /// </summary>
    private async Task<AddCommentContext> ResolveAddCommentContextAsync(int projektId, int subsystemId, int osobaId, CancellationToken ct)
    {
        var leadEquivalentBySubsystem = await ResolveLeadEquivalentOsobaIdsBySubsystemAsync(projektId, ct);
        return new AddCommentContext(
            leadEquivalentBySubsystem.GetValueOrDefault(subsystemId, []),
            leadEquivalentBySubsystem.Values.Any(ids => ids.Contains(osobaId)));
    }

    private sealed record AddCommentContext(IReadOnlyList<int> SubsystemLeadEquivalentOsobaIds, bool IsSubsystemLeadEquivalentInProject);
```

`IRecordService.cs` — za `DeleteCommentAsync` přidej stejnou signaturu jako v `ICommentService`; `RecordService.cs`:

```csharp
    public Task<IReadOnlySet<int>> GetMeetingIdsAllowingNewCommentAsync(
        int zaznamId, IReadOnlyCollection<JednaniOptionViewModel> meetings, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
        => commentService.GetMeetingIdsAllowingNewCommentAsync(zaznamId, meetings, currentUser, ct);
```

- [ ] **Step 3: Prezentace přes službu**

V `ZaznamyController.Partials.cs` nahraď `PrepareRecordCommentsPresentation` (ř. 118–140):

```csharp
    private async Task PrepareRecordCommentsPresentationAsync(ZaznamCommentsPanelViewModel model, ZaznamCardSummaryViewModel summary, CancellationToken ct)
    {
        var canEditRecord = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, model.ProjektId);
        var canCommentAsSubsystemLead = CurrentUserContext.HasPermission(PermissionKeys.MeetingsNotesSubsystemLead, model.ProjektId)
            && summary.AktualniSubsystemLeadEquivalentOsobaIds.Contains(CurrentUserContext.OsobaId);

        // Nabídka jednání = přesně ta, kde by AddComment prošel (sdílená CommentAuthorizationPolicy,
        // 2026-10-13). Dřív se tu ptalo jen na comments.add v projektu — subsystémový grant ho dává
        // vedoucímu/zástupci pro celý projekt, takže u cizího subsystému viděl editor, který server odmítl.
        var allowedMeetingIds = await _recordService.GetMeetingIdsAllowingNewCommentAsync(
            summary.Id, model.OtevrenaJednani, CurrentUserContext, ct);
        model.OtevrenaJednani = model.OtevrenaJednani
            .Where(item => allowedMeetingIds.Contains(item.Id))
            .ToList();

        model.CurrentUserOsobaId = CurrentUserContext.OsobaId;
        model.CanEditRecord = canEditRecord;
        model.CanCommentAsSubsystemLeader = canCommentAsSubsystemLead;
        model.CanAddComment = model.OtevrenaJednani.Count > 0;
    }
```

V `RecordCommentsPartial` nahraď volání `PrepareRecordCommentsPresentation(model, record.Summary);` za `await PrepareRecordCommentsPresentationAsync(model, record.Summary, ct);`.

V `ZaznamyController.cs` změň `private void PrepareRecordDetailPagePresentation(ZaznamDetailPageViewModel model, string? returnUrl)` na `private async Task PrepareRecordDetailPagePresentationAsync(ZaznamDetailPageViewModel model, string? returnUrl, CancellationToken ct)`, uvnitř `await PrepareRecordCommentsPresentationAsync(model.Comments, model.Summary, ct);` a v `Detail` volání na `await PrepareRecordDetailPagePresentationAsync(model, returnUrl, ct);`.

- [ ] **Step 4: Mrtvé příznaky `CanAddComment` na kartě**

Příznak karty nikde nevykresluje žádný view (vykresluje se jen `ZaznamCommentsPanelViewModel.CanAddComment`) a kóduje staré, chybné pravidlo:
- `ZaznamyController.Partials.cs` v `PrepareRecordCardShellPresentation` smaž `var canAddGeneralComment = …` (ř. 96) a `summary.CanAddComment = …` (ř. 107).
- `ProjektyController.cs:277-279` smaž přiřazení `summary.CanAddComment = …`.
- `ProjektZaznamyTabViewModels.cs` smaž `public bool CanAddComment { get; set; }` v `ZaznamCardSummaryViewModel` (ř. 90) a v `ZaznamCardViewModel` (ř. 178).

Ověř: `grep -rn "CanAddComment" PmTracker.Web --include=*.cs --include=*.cshtml` → zbývá jen `ZaznamCommentsPanelViewModel`, `_ZaznamCommentsPartial.cshtml`, `ZaznamyController.Partials.cs`, `CommentService.cs` a `CommentAuthorizationPolicy`.

- [ ] **Step 5: Testy zelené**

Run: `dotnet build PmTracker.sln` → bez chyb.
Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~RecordCommentsFormAuthzRenderTests|FullyQualifiedName~RecordDetailPageRenderTests|FullyQualifiedName~AjaxControllersTests"` → Úspěšné.
Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~Comment|FullyQualifiedName~ZaznamyController|FullyQualifiedName~ProjektyController"` → Úspěšné.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web PmTracker.Tests.Api
git commit -m "fix(vyjadreni): formulář vyjádření jen tam, kde ho server přijme

Nabídka jednání ve formuláři se počítá stejnou CommentAuthorizationPolicy a ze
stejných vstupů jako uložení. Zástupce vedoucího subsystému už u záznamu cizího
subsystému nevidí editor, který server odmítne. Odstraněny nepoužívané příznaky
CanAddComment na kartě se starým pravidlem.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Ověření, nasazovací poznámka, merge

- [ ] **Step 1: Celé Unit a Api sady**

Run: `dotnet test PmTracker.Tests.Unit` → Úspěšné (stav 2026-10-08: zelené; každé selhání = regrese).
Run: `dotnet test PmTracker.Tests.Api` → Úspěšné.

- [ ] **Step 2: E2E proti baseline**

```bash
SCR=/private/tmp/claude-501/-Users-Pavel-Andrlik-Documents-PM-Tracker/e2e-zastupce
mkdir -p "$SCR"
git worktree add "$SCR/baseline" main
(cd "$SCR/baseline" && dotnet test PmTracker.Tests.E2E > "$SCR/baseline.txt" 2>&1)
dotnet test PmTracker.Tests.E2E > "$SCR/branch.txt" 2>&1
python3 - "$SCR/baseline.txt" "$SCR/branch.txt" <<'EOF'
import re, sys
def failed(p):
    return {re.sub(r"\s*\[[^\]]*\]\s*$", "", m.group(1)).strip()
            for m in re.finditer(r"Neúspěšné\s+(\S+)", open(p, encoding="utf-8").read())}
base, branch = failed(sys.argv[1]), failed(sys.argv[2])
print("NOVÁ SELHÁNÍ:", sorted(branch - base) or "žádná")
print("OPRAVENO:", sorted(base - branch) or "nic")
EOF
git worktree remove "$SCR/baseline"
```

Expected: `NOVÁ SELHÁNÍ: žádná`.

- [ ] **Step 3: Lokální dev DB a ruční proklik pod zástupcem**

Na lokální DB (záloha z analýzy: `/var/opt/mssql/data/PmTracker_pred_zastupce_audit_20261012.bak`; testovací osoba 6 = ZASTUPCE_VEDOUCIHO_SUBSYSTEMU, záznam 300 s PMP+PNF, SD DB `intranetNEW_test`):

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
ASPNETCORE_ENVIRONMENT=Development \
ConnectionStrings__PmTrackerDb='Server=localhost,1433;Database=PmTracker;User Id=sa;Password=PmTracker!2026;TrustServerCertificate=True;Encrypt=True' \
ConnectionStrings__TicketingReadOnly='Server=localhost,1433;Database=intranetNEW_test;User Id=sa;Password=PmTracker!2026;TrustServerCertificate=True;Encrypt=True' \
Ticketing__Enabled=true PmTracker__Data__Provider=SqlServer \
dotnet run --project PmTracker.Web --urls http://localhost:5071 --no-launch-profile
# po startu (nová verze už klíč nedoseeduje):
/opt/homebrew/bin/sqlcmd -S localhost,1433 -U sa -P 'PmTracker!2026' -Ndisable -d PmTracker -b -i db_upgrade_1_4_8_drop_search_index.sql
/opt/homebrew/bin/sqlcmd -S localhost,1433 -U sa -P 'PmTracker!2026' -Ndisable -d PmTracker -W -s'|' -i db_check_applied_upgrades.sql | grep 1_4_8
```

Expected: `db_upgrade_1_4_8_drop_search_index|APLIKOVÁN`. Pak spusť Playwright skripty z analýzy (`/tmp/playwright-test-zastupce-crawl.js`, `-actions.js`, `-final.js`) a ověř: žádná odpověď ≥ 400; `/Search/Suggest` a `/Search` 200; bublina na stránce záznamu 300 otevře `/Zaznamy/VyjadreniNahled` (200, štítky „Krok N“, bez výběru kroku); v editoru návrhu bublina otevře okno jen pro čtení; v návrhu nového záznamu tiket 123456 dostane typ PMP; u záznamu 99011 (cizí subsystém) není formulář vyjádření.

- [ ] **Step 4: Text do poznámek k vydání (pro uživatele, vloží při releasu)**

```markdown
### Opraveno
- **Hledání funguje všem.** Od verze 0.10 vracelo „Nedostatečná oprávnění“ každému bez globální role (projektoví manažeři, vedoucí a zástupci subsystémů, gestoři…). Hledání nově smí každý přihlášený a najde jen záznamy projektů, které vidí.
- **Obsah tiketu u skutečnosti kroku vidí každý, kdo vidí záznam.** Bublina u vytěžené skutečnosti otevře okno jen pro čtení s komunikací v tiketu a přiřazením ke krokům. Měnit přiřazení jde dál jen v editoru záznamu.
- **Návrh nového záznamu otypuje tiket i vedoucím a zástupcům subsystémů** — dřív hlásil u existujícího tiketu „Tiket se nepodařilo dohledat“.
- **Formulář vyjádření se ukáže jen tam, kde ho jde uložit.** Vedoucí a zástupci subsystému ho u záznamů cizích subsystémů už neuvidí.
- Stránka záznamu ukazuje u kroku 1 se dvěma tikety stejné datum jako editor.

### Poznámka k nasazení
- Po nasazení nové verze spusť `db_upgrade_1_4_8_drop_search_index.sql` (úklid zrušeného klíče `search.index`). Skript je idempotentní; dokud neproběhne, klíč v databázi jen zbytečně leží. **Nespouštět před nasazením** — starší verze by klíč při startu znovu založila.
```

- [ ] **Step 5: Merge do lokálního `main`**

```bash
git checkout main
git merge --no-ff fix/opravneni-zastupce-subsystemu -m "Merge: oprávnění zástupce vedoucího subsystému"
git log --oneline -8
```

(Push jen na pokyn uživatele. Ruční ověření na i15/serveru dělá uživatel.)

---

## Pracnost (Opus 5, inline)

| Task | Odhad |
|---|---|
| 1 Hledání bez klíče | 1–1,5 h |
| 2 Sdílené pravidlo výběru vazby | 0,5–0,75 h |
| 3 Okno jen pro čtení — endpoint + falešný SD | 1,25–1,5 h |
| 4 Režim bubliny + JS + E2E | 1,25–1,5 h |
| 5 Typ tiketu v návrhu | 1 h |
| 6 Formulář vyjádření | 1 h |
| 7 Ověření (bez čekání na E2E ~2× 11 min) | 0,75 h |
| **Celkem** | **~7–8 h** |

Ruční ověření uživatelem (proklik pod zástupcem na i15, nasazení + 1_4_8 na obou instancích) není v odhadu.
