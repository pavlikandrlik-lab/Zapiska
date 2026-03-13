using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Meetings;

public interface IMeetingsCommands
{
    int SaveMeeting(SaveMeetingCommand command, CurrentUserContextViewModel currentUser);
    void DeleteMeeting(DeleteMeetingCommand command, CurrentUserContextViewModel currentUser);
    void SaveMeetingStatus(SaveMeetingStatusCommand command, CurrentUserContextViewModel currentUser);
    void SaveMeetingNote(SaveMeetingNoteCommand command, CurrentUserContextViewModel currentUser);
    void SaveAttendance(SaveAttendanceCommand command, CurrentUserContextViewModel currentUser);
    void AddMeetingParticipant(AddMeetingParticipantCommand command, CurrentUserContextViewModel currentUser);
}
