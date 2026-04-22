using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Web.Controllers;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Meetings;

/// <summary>
/// M-1: Verifies SaveStatus (and by analogy SaveAttendance/SaveNotes) no longer
/// swallows all exceptions — only InvalidOperationException is caught as a business
/// error; other exceptions propagate. ModelState validation is now enforced before
/// reaching the service call.
/// </summary>
public sealed class JednaniControllerExceptionHandlingTests
{
    /// <summary>
    /// When the service throws InvalidOperationException (business error), the non-AJAX
    /// path must set TempData["ErrorMessage"] and redirect back to Detail — it must NOT
    /// return a 2xx success.
    /// </summary>
    [Fact]
    public async Task SaveStatus_WhenServiceThrowsInvalidOperationException_SetsErrorTempDataAndRedirects()
    {
        // Arrange
        const string businessError = "biz error";
        var meetingService = new ThrowingMeetingService(new InvalidOperationException(businessError));
        var controller = CreateController(meetingService, projektId: 42);
        var command = new SaveMeetingStatusCommand { JednaniId = 7, Stav = "CLOSED" };

        // Act
        var result = await controller.SaveStatus(command, returnUrl: null);

        // Assert — redirect back (not success page), error surfaced in TempData
        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be(nameof(JednaniController.Detail));

        controller.TempData["ErrorMessage"].Should().Be(businessError);
    }

    /// <summary>
    /// When ModelState is invalid (non-AJAX), action must redirect without calling the
    /// service at all.
    /// </summary>
    [Fact]
    public async Task SaveStatus_WhenModelStateInvalid_RedirectsWithoutCallingService()
    {
        // Arrange
        var meetingService = new ThrowingMeetingService(new InvalidOperationException("should not be reached"));
        var controller = CreateController(meetingService, projektId: 42);
        controller.ModelState.AddModelError("Stav", "Stav je povinný.");
        var command = new SaveMeetingStatusCommand { JednaniId = 7, Stav = string.Empty };

        // Act
        var result = await controller.SaveStatus(command, returnUrl: null);

        // Assert — redirect (ModelState guard fires before service call)
        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be(nameof(JednaniController.Detail));

        // Service was never called (ThrowingMeetingService would set CallCount if reached)
        meetingService.SaveMeetingStatusCallCount.Should().Be(0);
    }

    private static JednaniController CreateController(IMeetingService meetingService, int projektId)
    {
        var httpContext = new DefaultHttpContext();

        var controller = new JednaniController(
            userContextResolver: null!,
            timeProvider: TimeProvider.System,
            loggerFactory: NullLoggerFactory.Instance,
            meetingService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        // Wire up TempData so assignments don't throw
        controller.TempData = new TempDataDictionary(httpContext, new NullTempDataProvider());

        // Inject CurrentUserContext — IsSuperAdmin with HasPermission returning true for projektId
        var userContext = new CurrentUserContextViewModel
        {
            OsobaId = 1,
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
            Authorization = new AuthorizationSnapshot(
                IsSuperAdmin: true,
                GlobalPermissions: new HashSet<string>(),
                PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>>
                {
                    [projektId] = new HashSet<string> { PermissionKeys.MeetingsEdit }
                },
                PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>())
        };

        var field = typeof(BaseController).GetField(
            "<CurrentUserContext>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        field!.SetValue(controller, userContext);

        return controller;
    }

    /// <summary>
    /// Stub meeting service that throws on SaveMeetingStatusAsync and tracks call count.
    /// Other methods return safe no-op defaults.
    /// </summary>
    private sealed class ThrowingMeetingService(Exception exceptionToThrow) : IMeetingService
    {
        public int SaveMeetingStatusCallCount { get; private set; }

        public Task<int?> GetMeetingProjectIdAsync(int meetingId, CancellationToken ct = default)
            => Task.FromResult<int?>(42);

        public Task SaveMeetingStatusAsync(SaveMeetingStatusCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
        {
            SaveMeetingStatusCallCount++;
            throw exceptionToThrow;
        }

        public Task<IReadOnlyList<JednaniProjektListItemViewModel>> BuildJednaniOverviewAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<JednaniProjektListItemViewModel>>([]);

        public Task<IReadOnlyList<JednaniProjektListItemViewModel>> BuildJednaniOverviewAsync(IReadOnlyCollection<int>? projectIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<JednaniProjektListItemViewModel>>([]);

        public Task<IReadOnlyList<JednaniListItemViewModel>> BuildJednaniListAsync(int projektId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<JednaniListItemViewModel>>([]);

        public Task<MeetingModalViewModel> BuildNewMeetingModalAsync(int projectId, DateTime localNow, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MeetingModalViewModel?> BuildEditMeetingModalAsync(int projectId, int meetingId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool?> IsMeetingEditableAsync(int projectId, int meetingId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<JednaniDetailViewModel> BuildJednaniDetailAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<JednaniUkolViewModel?> GetSingleTaskAsync(int meetingId, int zaznamId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MeetingParticipantCandidateViewModel>> BuildMeetingParticipantCandidatesAsync(int projectId, int meetingId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> SaveMeetingAsync(SaveMeetingCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteMeetingAsync(DeleteMeetingCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddMeetingParticipantAsync(AddMeetingParticipantCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveAttendanceBatchAsync(int meetingId, IEnumerable<(int OsobaId, string StavUcasti)> rows, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveMeetingNotesBatchAsync(int meetingId, IEnumerable<(int ZaznamId, string Text)> rows, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }
}
