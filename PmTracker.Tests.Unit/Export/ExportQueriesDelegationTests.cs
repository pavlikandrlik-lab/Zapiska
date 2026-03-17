using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Modules.Export;
using PmTracker.Web.Modules.Export.Queries;

namespace PmTracker.Tests.Unit.Export;

public sealed class ExportQueriesDelegationTests
{
    [Fact]
    public void ProjektExists_ShouldDelegateToExportProjektExistsQueryHandler()
    {
        var existsHandler = new FakeExportProjektExistsQueryHandler { Result = true };
        var sut = CreateSut(exportProjektExistsQueryHandler: existsHandler);

        var result = sut.ProjektExists(77);

        result.Should().BeTrue();
        existsHandler.LastProjektId.Should().Be(77);
        existsHandler.InvocationCount.Should().Be(1);
    }

    [Fact]
    public void ResolveMeetingProjectId_ShouldDelegateToExportMeetingProjectIdQueryHandler()
    {
        var meetingProjectIdHandler = new FakeExportMeetingProjectIdQueryHandler { Result = 18 };
        var sut = CreateSut(exportMeetingProjectIdQueryHandler: meetingProjectIdHandler);

        var result = sut.ResolveMeetingProjectId(901);

        result.Should().Be(18);
        meetingProjectIdHandler.LastMeetingId.Should().Be(901);
        meetingProjectIdHandler.InvocationCount.Should().Be(1);
    }

    [Fact]
    public void BuildTaskPrintTemplate_ShouldDelegateToExportTemplateUseCase()
    {
        var useCase = new FakeExportTemplateUseCase { TaskResult = BuildTemplateModel(10) };
        var currentUser = BuildCurrentUser();
        var sut = CreateSut(exportTemplateUseCase: useCase);

        var result = sut.BuildTaskPrintTemplate(25, 31, currentUser, autoPrint: false);

        result.Should().BeSameAs(useCase.TaskResult);
        useCase.LastTaskProjektId.Should().Be(25);
        useCase.LastTaskZaznamId.Should().Be(31);
        useCase.LastTaskCurrentUser.Should().BeSameAs(currentUser);
        useCase.LastTaskAutoPrint.Should().BeFalse();
    }

    private static ExportQueries CreateSut(
        FakeExportProjektExistsQueryHandler? exportProjektExistsQueryHandler = null,
        FakeExportMeetingProjectIdQueryHandler? exportMeetingProjectIdQueryHandler = null,
        FakeExportTemplateUseCase? exportTemplateUseCase = null)
    {
        return new ExportQueries(
            exportProjektExistsQueryHandler ?? new FakeExportProjektExistsQueryHandler(),
            exportMeetingProjectIdQueryHandler ?? new FakeExportMeetingProjectIdQueryHandler(),
            exportTemplateUseCase ?? new FakeExportTemplateUseCase());
    }

    private static CurrentUserContextViewModel BuildCurrentUser()
    {
        return new CurrentUserContextViewModel
        {
            OsobaId = 5,
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

    private static PdfExportTemplateViewModel BuildTemplateModel(int projektId)
    {
        return new PdfExportTemplateViewModel
        {
            ProjektId = projektId,
            ProjektNazev = "Projekt",
            JednaniStav = "Otevřeno",
            Vytvoril = "Unit Tester",
            SnapshotSummary = "Snapshot",
            AppliedRuleSummary = [],
            Legenda = [],
            Zaznamy = []
        };
    }

    private sealed class FakeExportProjektExistsQueryHandler : IExportProjektExistsQueryHandler
    {
        public bool Result { get; set; }
        public int LastProjektId { get; private set; }
        public int InvocationCount { get; private set; }

        public bool Handle(int projektId)
        {
            LastProjektId = projektId;
            InvocationCount += 1;
            return Result;
        }
    }

    private sealed class FakeExportMeetingProjectIdQueryHandler : IExportMeetingProjectIdQueryHandler
    {
        public int Result { get; set; }
        public int LastMeetingId { get; private set; }
        public int InvocationCount { get; private set; }

        public int Handle(int jednaniId)
        {
            LastMeetingId = jednaniId;
            InvocationCount += 1;
            return Result;
        }
    }

    private sealed class FakeExportTemplateUseCase : IExportTemplateUseCase
    {
        public PdfExportTemplateViewModel ProjectResult { get; set; } = BuildTemplateModel(1);
        public PdfExportTemplateViewModel MeetingResult { get; set; } = BuildTemplateModel(1);
        public PdfExportTemplateViewModel TaskResult { get; set; } = BuildTemplateModel(1);

        public int LastTaskProjektId { get; private set; }
        public int LastTaskZaznamId { get; private set; }
        public CurrentUserContextViewModel? LastTaskCurrentUser { get; private set; }
        public bool LastTaskAutoPrint { get; private set; }

        public PdfExportTemplateViewModel BuildProjectTemplate(int projektId, CurrentUserContextViewModel currentUser, bool autoPrint)
            => ProjectResult;

        public PdfExportTemplateViewModel BuildMeetingTemplate(int jednaniId, CurrentUserContextViewModel currentUser, bool autoPrint)
            => MeetingResult;

        public PdfExportTemplateViewModel BuildTaskTemplate(int projektId, int zaznamId, CurrentUserContextViewModel currentUser, bool autoPrint)
        {
            LastTaskProjektId = projektId;
            LastTaskZaznamId = zaznamId;
            LastTaskCurrentUser = currentUser;
            LastTaskAutoPrint = autoPrint;
            return TaskResult;
        }
    }
}
