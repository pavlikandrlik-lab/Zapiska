using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Modules.Meetings;
using PmTracker.Web.Modules.Meetings.Queries;

namespace PmTracker.Tests.Unit.Meetings;

public sealed class MeetingsQueriesDelegationTests
{
    [Fact]
    public void ProjektExists_ShouldDelegateToProjektExistsQueryHandler()
    {
        var existsHandler = new FakeProjektExistsQueryHandler { Result = true };
        var sut = CreateSut(projektExistsQueryHandler: existsHandler);

        var result = sut.ProjektExists(12);

        result.Should().BeTrue();
        existsHandler.LastId.Should().Be(12);
        existsHandler.InvocationCount.Should().Be(1);
    }

    [Fact]
    public void BuildJednaniDetail_ShouldDelegateToBuildJednaniDetailQueryHandler()
    {
        var expected = CreateJednaniDetail(5);
        var detailHandler = new FakeBuildJednaniDetailQueryHandler { Result = expected };
        var sut = CreateSut(buildJednaniDetailQueryHandler: detailHandler);

        var result = sut.BuildJednaniDetail(5);

        result.Should().BeSameAs(expected);
        detailHandler.LastId.Should().Be(5);
        detailHandler.InvocationCount.Should().Be(1);
    }

    private static MeetingsQueries CreateSut(
        FakeProjektExistsQueryHandler? projektExistsQueryHandler = null,
        FakeBuildProjektDetailQueryHandler? buildProjektDetailQueryHandler = null,
        FakeBuildJednaniOverviewQueryHandler? buildJednaniOverviewQueryHandler = null,
        FakeBuildJednaniDetailQueryHandler? buildJednaniDetailQueryHandler = null)
    {
        return new MeetingsQueries(
            projektExistsQueryHandler ?? new FakeProjektExistsQueryHandler(),
            buildProjektDetailQueryHandler ?? new FakeBuildProjektDetailQueryHandler(),
            buildJednaniOverviewQueryHandler ?? new FakeBuildJednaniOverviewQueryHandler(),
            buildJednaniDetailQueryHandler ?? new FakeBuildJednaniDetailQueryHandler());
    }

    private static ProjektDetailViewModel CreateProjektDetail(int id)
    {
        return new ProjektDetailViewModel
        {
            Projekt = new ProjektHeaderViewModel
            {
                Id = id,
                Zkratka = "P",
                Nazev = "Projekt",
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

    private static JednaniDetailViewModel CreateJednaniDetail(int id)
    {
        return new JednaniDetailViewModel
        {
            ProjektId = 8,
            ProjektNazev = "Projekt",
            Jednani = new JednaniListItemViewModel
            {
                Id = id,
                CisloJednani = 9,
                Datum = new DateTime(2026, 1, 1),
                CasZacatek = new TimeOnly(9, 0),
                Misto = "Místnost",
                Stav = "Otevřeno",
                StavKod = "OPEN"
            },
            Ucast = [],
            Ukoly = [],
            AvailableParticipantCandidates = [],
            StavyJednani = [],
            StavyUcasti = []
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

    private sealed class FakeBuildProjektDetailQueryHandler : IBuildProjektDetailQueryHandler
    {
        public ProjektDetailViewModel Handle(int id) => CreateProjektDetail(id);
    }

    private sealed class FakeBuildJednaniOverviewQueryHandler : IBuildJednaniOverviewQueryHandler
    {
        public IReadOnlyList<JednaniProjektListItemViewModel> Handle() => [];
    }

    private sealed class FakeBuildJednaniDetailQueryHandler : IBuildJednaniDetailQueryHandler
    {
        public JednaniDetailViewModel Result { get; set; } = CreateJednaniDetail(0);
        public int LastId { get; private set; }
        public int InvocationCount { get; private set; }

        public JednaniDetailViewModel Handle(int id)
        {
            LastId = id;
            InvocationCount += 1;
            return Result;
        }
    }
}
