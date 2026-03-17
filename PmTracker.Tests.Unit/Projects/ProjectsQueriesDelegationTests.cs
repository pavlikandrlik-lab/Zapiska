using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Modules.Projects;
using PmTracker.Web.Modules.Projects.Queries;

namespace PmTracker.Tests.Unit.Projects;

public sealed class ProjectsQueriesDelegationTests
{
    [Fact]
    public void ProjektExists_ShouldDelegateToProjektExistsQueryHandler()
    {
        var existsHandler = new FakeProjektExistsQueryHandler { Result = true };
        var sut = CreateSut(existsHandler: existsHandler);

        var result = sut.ProjektExists(55);

        result.Should().BeTrue();
        existsHandler.LastId.Should().Be(55);
        existsHandler.InvocationCount.Should().Be(1);
    }

    [Fact]
    public void BuildProjektyList_ShouldDelegateToBuildProjektyListQueryHandler()
    {
        var expected = new List<ProjektListItemViewModel>
        {
            new()
            {
                Id = 1,
                Zkratka = "P1",
                Nazev = "Projekt 1",
                StavKod = "OPEN",
                Stav = "Otevřený",
                CanEdit = true,
                CanDelete = false
            }
        };
        var listHandler = new FakeBuildProjektyListQueryHandler { Result = expected };
        var sut = CreateSut(buildProjektyListQueryHandler: listHandler);

        var result = sut.BuildProjektyList();

        result.Should().BeSameAs(expected);
        listHandler.InvocationCount.Should().Be(1);
    }

    [Fact]
    public void BuildProjektDetail_ShouldDelegateToBuildProjektDetailQueryHandler()
    {
        var expected = CreateProjektDetailModel(5);
        var detailHandler = new FakeBuildProjektDetailQueryHandler { Result = expected };
        var sut = CreateSut(buildProjektDetailQueryHandler: detailHandler);

        var result = sut.BuildProjektDetail(5);

        result.Should().BeSameAs(expected);
        detailHandler.LastId.Should().Be(5);
        detailHandler.InvocationCount.Should().Be(1);
    }

    [Fact]
    public void BuildProjectStatusOptions_ShouldDelegateToBuildProjectStatusOptionsQueryHandler()
    {
        var expected = new List<LookupOptionViewModel>
        {
            new() { Value = "PLAN", Label = "Plán" }
        };
        var statusHandler = new FakeBuildProjectStatusOptionsQueryHandler { Result = expected };
        var currentUser = BuildCurrentUser();
        var sut = CreateSut(buildProjectStatusOptionsQueryHandler: statusHandler);

        var result = sut.BuildProjectStatusOptions(currentUser);

        result.Should().BeSameAs(expected);
        statusHandler.LastUser.Should().BeSameAs(currentUser);
        statusHandler.InvocationCount.Should().Be(1);
    }

    private static ProjectsQueries CreateSut(
        FakeProjektExistsQueryHandler? existsHandler = null,
        FakeBuildProjektyListQueryHandler? buildProjektyListQueryHandler = null,
        FakeBuildProjektDetailQueryHandler? buildProjektDetailQueryHandler = null,
        FakeBuildProjectStatusOptionsQueryHandler? buildProjectStatusOptionsQueryHandler = null)
    {
        return new ProjectsQueries(
            existsHandler ?? new FakeProjektExistsQueryHandler(),
            buildProjektyListQueryHandler ?? new FakeBuildProjektyListQueryHandler(),
            buildProjektDetailQueryHandler ?? new FakeBuildProjektDetailQueryHandler(),
            buildProjectStatusOptionsQueryHandler ?? new FakeBuildProjectStatusOptionsQueryHandler());
    }

    private static CurrentUserContextViewModel BuildCurrentUser()
    {
        return new CurrentUserContextViewModel
        {
            OsobaId = 10,
            Jmeno = "Unit",
            Prijmeni = "Tester",
            DisplayName = "Unit Tester",
            Email = "unit@test.local",
            OrganizacniCelek = "Test",
            OrganizacniCelekKod = "TEST",
            IsSuperAdmin = true,
            RoleKody = [],
            VisibleProjectIds = [],
            DeletedProjectIds = [],
            PermissionGrants = []
        };
    }

    private static ProjektDetailViewModel CreateProjektDetailModel(int id)
    {
        return new ProjektDetailViewModel
        {
            Projekt = new ProjektHeaderViewModel
            {
                Id = id,
                Zkratka = $"P{id}",
                Nazev = $"Projekt {id}",
                Stav = "Aktivní"
            },
            SkupinySubsystemu = [],
            Zaznamy = [],
            Jednani = [],
            AktivniRole = [],
            HistorieRoli = [],
            AktivniSubsystemyProjektu = [],
            DostupneOsobyProRole = [],
            DostupneProjektoveSubsystemy = [],
            HarmonogramUkoly = [],
            OtevrenaJednani = [],
            Filtry = new ProjektFiltryViewModel
            {
                Subsystemy = [],
                Kategorie = [],
                StavyUkolu = [],
                TypyUkolu = [],
                Vlastnici = []
            }
        };
    }

    private sealed class FakeProjektExistsQueryHandler : IProjektExistsQueryHandler
    {
        public bool Result { get; set; }
        public int LastId { get; private set; }
        public int InvocationCount { get; private set; }

        public bool Handle(int id)
        {
            LastId = id;
            InvocationCount += 1;
            return Result;
        }
    }

    private sealed class FakeBuildProjektyListQueryHandler : IBuildProjektyListQueryHandler
    {
        public IReadOnlyList<ProjektListItemViewModel> Result { get; set; } = [];
        public int InvocationCount { get; private set; }

        public IReadOnlyList<ProjektListItemViewModel> Handle()
        {
            InvocationCount += 1;
            return Result;
        }
    }

    private sealed class FakeBuildProjektDetailQueryHandler : IBuildProjektDetailQueryHandler
    {
        public ProjektDetailViewModel Result { get; set; } = CreateProjektDetailModel(0);
        public int LastId { get; private set; }
        public int InvocationCount { get; private set; }

        public ProjektDetailViewModel Handle(int id)
        {
            LastId = id;
            InvocationCount += 1;
            return Result;
        }
    }

    private sealed class FakeBuildProjectStatusOptionsQueryHandler : IBuildProjectStatusOptionsQueryHandler
    {
        public IReadOnlyList<LookupOptionViewModel> Result { get; set; } = [];
        public CurrentUserContextViewModel? LastUser { get; private set; }
        public int InvocationCount { get; private set; }

        public IReadOnlyList<LookupOptionViewModel> Handle(CurrentUserContextViewModel currentUser)
        {
            LastUser = currentUser;
            InvocationCount += 1;
            return Result;
        }
    }
}
