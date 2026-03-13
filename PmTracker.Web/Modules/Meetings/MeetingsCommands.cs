using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Modules.Meetings;

public sealed class MeetingsCommands(IPmTrackerDataStore dataStore) : IMeetingsCommands
{
    public int SaveMeeting(SaveMeetingCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.SaveMeeting(command, currentUser);

    public void DeleteMeeting(DeleteMeetingCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.DeleteMeeting(command, currentUser);

    public void SaveMeetingStatus(SaveMeetingStatusCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.SaveMeetingStatus(command, currentUser);

    public void SaveMeetingNote(SaveMeetingNoteCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.SaveMeetingNote(command, currentUser);

    public void SaveAttendance(SaveAttendanceCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.SaveAttendance(command, currentUser);

    public void AddMeetingParticipant(AddMeetingParticipantCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.AddMeetingParticipant(command, currentUser);
}
