using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IPmTrackerDataStore
{
    CurrentUserContextViewModel BuildCurrentUserContext(string? asProfile);
    bool ProjektExists(int id);

    IReadOnlyList<ProjektListItemViewModel> BuildProjektyList();
    ProjektDetailViewModel BuildProjektDetail(int id);
    ZaznamEditViewModel BuildZaznamEdit(int id);
    ZaznamEditViewModel BuildZaznamCreate(int projektId, int? jednaniId = null);
    DeleteRecordModalViewModel BuildDeleteRecordModal(int projektId, int zaznamId);
    int GetNextCisloZaznamu(int projektId);

    IReadOnlyList<JednaniProjektListItemViewModel> BuildJednaniOverview();
    IReadOnlyList<JednaniListItemViewModel> BuildJednaniList(int projektId);
    JednaniDetailViewModel BuildJednaniDetail(int id);

    OsobyIndexViewModel BuildOsoby();
    ProfilPageViewModel BuildProfilPage(CurrentUserContextViewModel currentUser, int? projektId);

    CiselnikyDashboardViewModel BuildCiselnikyDashboard(string? id, CurrentUserContextViewModel currentUser);
    CiselnikDetailViewModel BuildCiselnikDetail(string id, CurrentUserContextViewModel currentUser);

    NastaveniDashboardViewModel BuildNastaveniDashboard(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId);
    NastaveniPanelViewModel BuildNastaveniPanel(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId);

    PdfExportTemplateViewModel BuildProjectPrintTemplate(int projektId, CurrentUserContextViewModel currentUser, bool autoPrint);
    PdfExportTemplateViewModel BuildMeetingPrintTemplate(int jednaniId, CurrentUserContextViewModel currentUser, bool autoPrint);
    PdfExportTemplateViewModel BuildTaskPrintTemplate(int projektId, int zaznamId, CurrentUserContextViewModel currentUser, bool autoPrint);

    int SaveProject(SaveProjectCommand command, CurrentUserContextViewModel currentUser);
    void SoftDeleteProject(SoftDeleteProjectCommand command, CurrentUserContextViewModel currentUser);

    int SaveRecord(SaveRecordCommand command, CurrentUserContextViewModel currentUser);
    void DeleteRecord(DeleteRecordCommand command, CurrentUserContextViewModel currentUser);
    void AssignMeetingIdentifier(AssignMeetingIdentifierCommand command, CurrentUserContextViewModel currentUser);
    void AddComment(AddCommentCommand command, CurrentUserContextViewModel currentUser);
    void UpdateComment(UpdateCommentCommand command, CurrentUserContextViewModel currentUser);
    void DeleteComment(DeleteCommentCommand command, CurrentUserContextViewModel currentUser);

    int SaveMeeting(SaveMeetingCommand command, CurrentUserContextViewModel currentUser);
    void DeleteMeeting(DeleteMeetingCommand command, CurrentUserContextViewModel currentUser);
    void SaveMeetingStatus(SaveMeetingStatusCommand command, CurrentUserContextViewModel currentUser);
    void SaveMeetingNote(SaveMeetingNoteCommand command, CurrentUserContextViewModel currentUser);
    void SaveAttendance(SaveAttendanceCommand command, CurrentUserContextViewModel currentUser);
    void AddMeetingParticipant(AddMeetingParticipantCommand command, CurrentUserContextViewModel currentUser);

    void SaveTeamMember(SaveTeamMemberCommand command, CurrentUserContextViewModel currentUser);
    void RemoveTeamMember(RemoveTeamMemberCommand command, CurrentUserContextViewModel currentUser);
    void AssignProjectRole(AssignProjectRoleCommand command, CurrentUserContextViewModel currentUser);
    void DeactivateProjectRole(DeactivateProjectRoleCommand command, CurrentUserContextViewModel currentUser);
    void AssignProjectSubsystem(AssignProjectSubsystemCommand command, CurrentUserContextViewModel currentUser);
    void DeactivateProjectSubsystem(DeactivateProjectSubsystemCommand command, CurrentUserContextViewModel currentUser);
    void AssignProjectSubsystemRole(AssignProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser);
    void DeactivateProjectSubsystemRole(DeactivateProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser);

    int SaveManualPerson(SaveManualPersonCommand command, CurrentUserContextViewModel currentUser);
    int SaveAdPerson(SaveAdPersonCommand command, CurrentUserContextViewModel currentUser);
    void DeletePerson(DeletePersonCommand command, CurrentUserContextViewModel currentUser);

    void SaveCiselnikRow(SaveCiselnikRowCommand command, CurrentUserContextViewModel currentUser);
    void DeleteCiselnikRow(DeleteCiselnikRowCommand command, CurrentUserContextViewModel currentUser);
    void SaveUserRoleAssignment(SaveUserRoleAssignmentCommand command, CurrentUserContextViewModel currentUser);
    void SaveUserRolesForUser(SaveUserRolesForUserCommand command, CurrentUserContextViewModel currentUser);
    void SaveAuthzRole(SaveAuthzRoleCommand command, CurrentUserContextViewModel currentUser);
    void ToggleAuthzRole(ToggleAuthzRoleCommand command, CurrentUserContextViewModel currentUser);
    void SaveAuthzPermission(SaveAuthzPermissionCommand command, CurrentUserContextViewModel currentUser);
    void ToggleAuthzPermission(ToggleAuthzPermissionCommand command, CurrentUserContextViewModel currentUser);
    void SaveRolePermission(SaveRolePermissionCommand command, CurrentUserContextViewModel currentUser);
    void DeleteRolePermission(DeleteRolePermissionCommand command, CurrentUserContextViewModel currentUser);
}
