using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Modules.Meetings.Commands;

namespace PmTracker.Web.Modules.Meetings;

public sealed class MeetingsCommands(
    ISaveMeetingCommandHandler saveMeetingCommandHandler,
    IDeleteMeetingCommandHandler deleteMeetingCommandHandler,
    ISaveMeetingStatusCommandHandler saveMeetingStatusCommandHandler,
    ISaveMeetingNoteCommandHandler saveMeetingNoteCommandHandler,
    ISaveAttendanceCommandHandler saveAttendanceCommandHandler,
    IAddMeetingParticipantCommandHandler addMeetingParticipantCommandHandler) : IMeetingsCommands
{
    public int SaveMeeting(SaveMeetingCommand command, CurrentUserContextViewModel currentUser)
        => saveMeetingCommandHandler.Handle(command, currentUser);

    public void DeleteMeeting(DeleteMeetingCommand command, CurrentUserContextViewModel currentUser)
        => deleteMeetingCommandHandler.Handle(command, currentUser);

    public void SaveMeetingStatus(SaveMeetingStatusCommand command, CurrentUserContextViewModel currentUser)
        => saveMeetingStatusCommandHandler.Handle(command, currentUser);

    public void SaveMeetingNote(SaveMeetingNoteCommand command, CurrentUserContextViewModel currentUser)
        => saveMeetingNoteCommandHandler.Handle(command, currentUser);

    public void SaveAttendance(SaveAttendanceCommand command, CurrentUserContextViewModel currentUser)
        => saveAttendanceCommandHandler.Handle(command, currentUser);

    public void AddMeetingParticipant(AddMeetingParticipantCommand command, CurrentUserContextViewModel currentUser)
        => addMeetingParticipantCommandHandler.Handle(command, currentUser);
}
