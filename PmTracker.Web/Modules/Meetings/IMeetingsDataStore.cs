using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Meetings;

public interface IMeetingsDataStore
{
    bool ProjektExists(int id);

    IReadOnlyList<JednaniProjektListItemViewModel> BuildJednaniOverview();

    JednaniDetailViewModel BuildJednaniDetail(int id);

    ProjektDetailViewModel BuildProjektDetail(int id);

    int SaveMeeting(SaveMeetingCommand command, CurrentUserContextViewModel currentUser);

    void DeleteMeeting(DeleteMeetingCommand command, CurrentUserContextViewModel currentUser);

    void SaveMeetingStatus(SaveMeetingStatusCommand command, CurrentUserContextViewModel currentUser);

    void SaveAttendance(SaveAttendanceCommand command, CurrentUserContextViewModel currentUser);

    void SaveMeetingNote(SaveMeetingNoteCommand command, CurrentUserContextViewModel currentUser);

    void AddMeetingParticipant(AddMeetingParticipantCommand command, CurrentUserContextViewModel currentUser);
}
