using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Search;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// Vyhledávání naostro proti reálné databázi. Collation ani EXISTS nejde ověřit
/// in-memory, takže tohle je jediné místo, kde se dotaz opravdu testuje.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class RecordSearchServiceTests
{
    private readonly SqlIntegrationFixture _fixture;

    public RecordSearchServiceTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    private static CurrentUserContextViewModel User(
        bool isSuperAdmin = false,
        int[]? visibleProjectIds = null,
        string[]? globalPermissions = null) => new()
    {
        OsobaId = 1,
        Jmeno = "Jan",
        Prijmeni = "Novák",
        DisplayName = "Jan Novák",
        Email = "jan.novak@example.cz",
        OrganizacniCelek = "MO",
        IsSuperAdmin = isSuperAdmin,
        RoleKody = Array.Empty<string>(),
        VisibleProjectIds = visibleProjectIds ?? Array.Empty<int>(),
        DeletedProjectIds = Array.Empty<int>(),
        Authorization = new AuthorizationSnapshot(
            IsSuperAdmin: isSuperAdmin,
            GlobalPermissions: new HashSet<string>(globalPermissions ?? Array.Empty<string>()),
            PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>>(),
            PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>())
    };

    private static RecordSearchService CreateService(string connectionString)
    {
        var db = IntegrationTestHelper.CreateDbContext(connectionString);
        return new RecordSearchService(db, new ProjectVisibilityResolver(),
            NullLogger<RecordSearchService>.Instance);
    }

    /// <summary>
    /// Bez tohohle by test diakritiky mohl projít i kdyby COLLATE v dotazu chybělo.
    /// Produkce má Czech_CI_AS, kontejner SQL_Latin1_General_CP1_CI_AS — obojí _AS.
    /// </summary>
    private static async Task AssertAccentSensitiveCollationAsync(string connectionString)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT CONVERT(nvarchar(128), DATABASEPROPERTYEX(DB_NAME(), 'Collation'));";
        ((string)(await cmd.ExecuteScalarAsync())!).Should().EndWith("_AS");
    }

    [Fact]
    public async Task Hledani_IgnorujeDiakritiku()
    {
        var db = await _fixture.CreateDatabaseAsync("search_diakritika");
        await AssertAccentSensitiveCollationAsync(db.ConnectionString);
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        await seed.AddRecordAsync(projektId: seed.ProjektId, nazev: "Zálohování serveru");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("zalohovani", User(isSuperAdmin: true), 7, default);

        result.TotalCount.Should().Be(1);
        result.Categories.Should().ContainSingle()
            .Which.Items[0].Nazev.Should().Be("Zálohování serveru");
    }

    [Fact]
    public async Task Hledani_PodPrahemTriZnaku_NehledaVubec()
    {
        var db = await _fixture.CreateDatabaseAsync("search_prah");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        await seed.AddRecordAsync(seed.ProjektId, "Záloha");

        var service = CreateService(db.ConnectionString);

        (await service.SearchAsync("za", User(isSuperAdmin: true), 7, default))
            .TotalCount.Should().Be(0);
        (await service.SearchAsync("zal", User(isSuperAdmin: true), 7, default))
            .TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Hledani_VyzadujeVsechnaSlova()
    {
        var db = await _fixture.CreateDatabaseAsync("search_vsechna_slova");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        await seed.AddRecordAsync(seed.ProjektId, "Zálohování serveru");
        await seed.AddRecordAsync(seed.ProjektId, "Zálohování databáze");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("zálohování serveru", User(isSuperAdmin: true), 7, default);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Hledani_ZastupneZnakyJsouDoslovne()
    {
        var db = await _fixture.CreateDatabaseAsync("search_zastupne");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        await seed.AddRecordAsync(seed.ProjektId, "Čerpání 50 % rozpočtu");
        await seed.AddRecordAsync(seed.ProjektId, "Úplně nesouvisející");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("50 %", User(isSuperAdmin: true), 7, default);

        result.TotalCount.Should().Be(1, "neescapované % by vrátilo obě položky");
    }

    [Fact]
    public async Task Hledani_NajdeZaznamPodleTextuVyjadreni_AVratiCisloJednani()
    {
        var db = await _fixture.CreateDatabaseAsync("search_vyjadreni");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        var zaznamId = await seed.AddRecordAsync(seed.ProjektId, "Nesouvisející název");
        var jednaniId = await seed.AddMeetingAsync(seed.ProjektId, cisloJednani: 8201);
        await seed.AddStatementAsync(zaznamId, jednaniId, "Dnes proběhla záloha dat a byla ověřena");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("záloha", User(isSuperAdmin: true), 7, default);

        var item = result.Categories.Single().Items.Single();
        item.ZaznamId.Should().Be(zaznamId, "jednotkou výsledku je záznam, ne vyjádření");
        item.MatchKind.Should().Be(SearchMatchKind.Vyjadreni);
        item.CisloJednani.Should().Be(8201);
        item.Snippet!.Match.Should().Be("záloha");
        item.Snippet.Before.Should().Be("Dnes proběhla ");
        item.Snippet.After.Should().Be(" dat a");
    }

    [Fact]
    public async Task Hledani_NajdeZaznamPodleCislaExterniVazby()
    {
        var db = await _fixture.CreateDatabaseAsync("search_externi");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        var zaznamId = await seed.AddRecordAsync(seed.ProjektId, "Nesouvisející název");
        await seed.AddExternalLinkAsync(zaznamId, cislo: "123456");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("123456", User(isSuperAdmin: true), 7, default);

        var item = result.Categories.Single().Items.Single();
        item.ZaznamId.Should().Be(zaznamId);
        item.MatchKind.Should().Be(SearchMatchKind.ExterniOdkaz);
        item.Snippet!.Match.Should().Be("123456");
    }

    [Fact]
    public async Task Hledani_NajdeZaznamPodleCislaZaznamu()
    {
        var db = await _fixture.CreateDatabaseAsync("search_cislo_zaznamu");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        await seed.AddRecordAsync(seed.ProjektId, "Nesouvisející název", cisloViditelne: "RU 123");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("RU 123", User(isSuperAdmin: true), 7, default);

        result.Categories.Single().Items.Single().MatchKind.Should().Be(SearchMatchKind.Nazev);
    }

    [Fact]
    public async Task Hledani_VraciZkratkuSubsystemu()
    {
        var db = await _fixture.CreateDatabaseAsync("search_subsystem");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        await seed.AddRecordAsync(seed.ProjektId, "Zálohování serveru");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("zálohování", User(isSuperAdmin: true), 7, default);

        result.Categories.Single().Items.Single().SubsystemKod.Should().Be(seed.SubsystemKod);
    }

    // ---- Autorizace: spec §2.3 --------------------------------------------

    [Fact]
    public async Task Autorizace_ClenProjektu_VidiJenSvujProjekt()
    {
        var db = await _fixture.CreateDatabaseAsync("search_authz_clen");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        var druhyProjekt = await seed.AddProjectAsync("DRUHY");
        await seed.AddRecordAsync(seed.ProjektId, "Záloha v mém projektu");
        await seed.AddRecordAsync(druhyProjekt, "Záloha v cizím projektu");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("záloha", User(visibleProjectIds: [seed.ProjektId]), 7, default);

        result.Categories.Single().Items.Should().ContainSingle()
            .Which.Nazev.Should().Be("Záloha v mém projektu");
    }

    [Fact]
    public async Task Autorizace_UzivatelBezProjektu_NevidiNic()
    {
        var db = await _fixture.CreateDatabaseAsync("search_authz_bez_projektu");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        await seed.AddRecordAsync(seed.ProjektId, "Záloha serveru");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("záloha", User(), 7, default);

        result.TotalCount.Should().Be(0,
            "prázdná množina projektů musí znamenat žádné výsledky, ne žádný filtr");
    }

    [Fact]
    public async Task Autorizace_Superadmin_VidiVse()
    {
        var db = await _fixture.CreateDatabaseAsync("search_authz_superadmin");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        var druhyProjekt = await seed.AddProjectAsync("DRUHY");
        await seed.AddRecordAsync(seed.ProjektId, "Záloha jedna");
        await seed.AddRecordAsync(druhyProjekt, "Záloha dvě");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("záloha", User(isSuperAdmin: true), 7, default);

        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Autorizace_AppAdminSGlobalnimReadAll_VidiVse()
    {
        // Uživatel výslovně žádal, aby nové chování nerozbilo app-admina.
        var db = await _fixture.CreateDatabaseAsync("search_authz_appadmin");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        var druhyProjekt = await seed.AddProjectAsync("DRUHY");
        await seed.AddRecordAsync(seed.ProjektId, "Záloha jedna");
        await seed.AddRecordAsync(druhyProjekt, "Záloha dvě");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("záloha",
            User(globalPermissions: [PermissionKeys.ProjectsReadAll]), 7, default);

        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Autorizace_VyjadreniZCizihoProjektu_ZaznamNeodhali()
    {
        // Shoda padne ve vyjádření záznamu z projektu, který uživatel nevidí.
        var db = await _fixture.CreateDatabaseAsync("search_authz_vyjadreni");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        var cizi = await seed.AddProjectAsync("CIZI");
        var cizinZaznam = await seed.AddRecordAsync(cizi, "Cizí záznam");
        var jednani = await seed.AddMeetingAsync(cizi, 9001);
        await seed.AddStatementAsync(cizinZaznam, jednani, "Tady je tajná záloha");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("záloha", User(visibleProjectIds: [seed.ProjektId]), 7, default);

        result.TotalCount.Should().Be(0);
    }

    // ---- Limit a řazení ----------------------------------------------------

    [Fact]
    public async Task Hledani_RespektujeLimit()
    {
        var db = await _fixture.CreateDatabaseAsync("search_limit");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        for (var i = 1; i <= 10; i++)
        {
            await seed.AddRecordAsync(seed.ProjektId, $"Záloha {i:D2}");
        }

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("záloha", User(isSuperAdmin: true), 7, default);

        result.TotalCount.Should().Be(7);
    }

    [Fact]
    public async Task Hledani_RadiVzestupnePodleNazvu()
    {
        var db = await _fixture.CreateDatabaseAsync("search_razeni");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        await seed.AddRecordAsync(seed.ProjektId, "Záloha C");
        await seed.AddRecordAsync(seed.ProjektId, "Záloha A");
        await seed.AddRecordAsync(seed.ProjektId, "Záloha B");

        var service = CreateService(db.ConnectionString);
        var result = await service.SearchAsync("záloha", User(isSuperAdmin: true), 7, default);

        result.Categories.Single().Items.Select(i => i.Nazev)
            .Should().Equal("Záloha A", "Záloha B", "Záloha C");
    }
}
