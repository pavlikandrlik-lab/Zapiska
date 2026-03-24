using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Dictionaries;
using PmTracker.Web.Services.Export;
using PmTracker.Web.Services.Profile;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.Settings;

namespace PmTracker.Tests.Integration.TestInfrastructure;

internal sealed class IntegrationTestDataStore(IServiceProvider services)
{
    public CurrentUserContextViewModel BuildCurrentUserContext(string? asProfile)
    {
        var resolver = services.GetRequiredService<IUserContextResolver>();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };

        if (!string.IsNullOrWhiteSpace(asProfile))
        {
            httpContext.Request.QueryString = new QueryString($"?asUser={Uri.EscapeDataString(asProfile)}");
        }

        var result = resolver.ResolveAsync(httpContext).GetAwaiter().GetResult();
        if (!result.IsSuccess || result.UserContext is null)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "User context resolution failed.");
        }

        return result.UserContext;
    }

    public IReadOnlyList<ProjektListItemViewModel> BuildProjektyList()
        => services.GetRequiredService<IProjectService>().BuildProjektyListAsync().GetAwaiter().GetResult();

    public ProjektDetailViewModel BuildProjektDetail(int id)
        => services.GetRequiredService<IProjectService>().BuildProjektDetailAsync(id).GetAwaiter().GetResult();

    public CiselnikDetailViewModel BuildCiselnikDetail(string id, CurrentUserContextViewModel currentUser)
        => services.GetRequiredService<IDictionaryService>().BuildCiselnikDetailAsync(id, currentUser).GetAwaiter().GetResult();

    public IReadOnlyList<JednaniProjektListItemViewModel> BuildJednaniOverview()
        => services.GetRequiredService<IMeetingService>().BuildJednaniOverviewAsync().GetAwaiter().GetResult();

    public JednaniDetailViewModel BuildJednaniDetail(int id)
        => services.GetRequiredService<IMeetingService>().BuildJednaniDetailAsync(id).GetAwaiter().GetResult();

    public ProfilPageViewModel BuildProfilPage(CurrentUserContextViewModel currentUser, int? projektId)
        => services.GetRequiredService<IProfileService>().BuildProfilPageAsync(currentUser, projektId).GetAwaiter().GetResult();

    public ZaznamEditViewModel BuildZaznamCreate(int projektId, int? jednaniId = null)
        => services.GetRequiredService<IRecordService>().BuildZaznamCreateAsync(projektId, jednaniId).GetAwaiter().GetResult();

    public ZaznamEditViewModel BuildZaznamEdit(int id)
        => services.GetRequiredService<IRecordService>().BuildZaznamEditAsync(id).GetAwaiter().GetResult();

    public int SaveRecord(SaveRecordCommand command, CurrentUserContextViewModel currentUser)
        => services.GetRequiredService<IRecordService>().SaveRecordAsync(command, currentUser).GetAwaiter().GetResult();

    public void DeleteRecord(DeleteRecordCommand command, CurrentUserContextViewModel currentUser)
        => services.GetRequiredService<IRecordService>().DeleteRecordAsync(command, currentUser).GetAwaiter().GetResult();

    public void AddComment(AddCommentCommand command, CurrentUserContextViewModel currentUser)
        => services.GetRequiredService<IRecordService>().AddCommentAsync(command, currentUser).GetAwaiter().GetResult();

    public void UpdateComment(UpdateCommentCommand command, CurrentUserContextViewModel currentUser)
        => services.GetRequiredService<IRecordService>().UpdateCommentAsync(command, currentUser).GetAwaiter().GetResult();

    public void DeleteComment(DeleteCommentCommand command, CurrentUserContextViewModel currentUser)
        => services.GetRequiredService<IRecordService>().DeleteCommentAsync(command, currentUser).GetAwaiter().GetResult();

    public int SaveMeeting(SaveMeetingCommand command, CurrentUserContextViewModel currentUser)
        => services.GetRequiredService<IMeetingService>().SaveMeetingAsync(command, currentUser).GetAwaiter().GetResult();

    public void DeleteMeeting(DeleteMeetingCommand command, CurrentUserContextViewModel currentUser)
        => services.GetRequiredService<IMeetingService>().DeleteMeetingAsync(command, currentUser).GetAwaiter().GetResult();

    public void AddMeetingParticipant(AddMeetingParticipantCommand command, CurrentUserContextViewModel currentUser)
        => services.GetRequiredService<IMeetingService>().AddMeetingParticipantAsync(command, currentUser).GetAwaiter().GetResult();

    public void SaveAuthzPermission(SaveAuthzPermissionCommand command, CurrentUserContextViewModel currentUser)
        => services.GetRequiredService<ISettingsService>().SaveAuthzPermissionAsync(command, currentUser).GetAwaiter().GetResult();

    public void SaveRolePermission(SaveRolePermissionCommand command, CurrentUserContextViewModel currentUser)
        => services.GetRequiredService<ISettingsService>().SaveRolePermissionAsync(command, currentUser).GetAwaiter().GetResult();

    public void DeleteRolePermission(DeleteRolePermissionCommand command, CurrentUserContextViewModel currentUser)
        => services.GetRequiredService<ISettingsService>().DeleteRolePermissionAsync(command, currentUser).GetAwaiter().GetResult();

    public void SaveCiselnikRow(SaveCiselnikRowCommand command, CurrentUserContextViewModel currentUser)
        => services.GetRequiredService<IDictionaryService>().SaveCiselnikRowAsync(command, currentUser).GetAwaiter().GetResult();

    public void DeleteCiselnikRow(DeleteCiselnikRowCommand command, CurrentUserContextViewModel currentUser)
        => services.GetRequiredService<IDictionaryService>().DeleteCiselnikRowAsync(command, currentUser).GetAwaiter().GetResult();

    public void AssignProjectRole(AssignProjectRoleCommand command, CurrentUserContextViewModel currentUser)
        => services.GetRequiredService<IProjectService>().AssignProjectRoleAsync(command, currentUser).GetAwaiter().GetResult();

    public void AssignProjectSubsystemRole(AssignProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser)
        => services.GetRequiredService<IProjectService>().AssignProjectSubsystemRoleAsync(command, currentUser).GetAwaiter().GetResult();

    public PdfExportTemplateViewModel BuildProjectPrintTemplate(int projektId, CurrentUserContextViewModel currentUser, bool autoPrint)
        => services.GetRequiredService<IExportTemplateUseCase>().BuildProjectTemplateAsync(projektId, currentUser, autoPrint).GetAwaiter().GetResult();

    public PdfExportTemplateViewModel BuildMeetingPrintTemplate(int jednaniId, CurrentUserContextViewModel currentUser, bool autoPrint)
        => services.GetRequiredService<IExportTemplateUseCase>().BuildMeetingTemplateAsync(jednaniId, currentUser, autoPrint).GetAwaiter().GetResult();

    public PdfExportTemplateViewModel BuildTaskPrintTemplate(int projektId, int zaznamId, CurrentUserContextViewModel currentUser, bool autoPrint)
        => services.GetRequiredService<IExportTemplateUseCase>().BuildTaskTemplateAsync(projektId, zaznamId, currentUser, autoPrint).GetAwaiter().GetResult();

    public NastaveniPanelViewModel BuildNastaveniPanel(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId)
        => services.GetRequiredService<ISettingsService>().BuildNastaveniPanelAsync(section, currentUser, userId, projektId).GetAwaiter().GetResult();

    public NastaveniDashboardViewModel BuildNastaveniDashboard(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId)
        => services.GetRequiredService<ISettingsService>().BuildNastaveniDashboardAsync(section, currentUser, userId, projektId).GetAwaiter().GetResult();
}

internal sealed class TestWebHostEnvironment : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "PmTracker.Tests.Integration";
    public string WebRootPath { get; set; } = Path.GetTempPath();
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = Path.GetTempPath();
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
