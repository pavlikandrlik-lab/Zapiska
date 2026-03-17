using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services.Data;

public sealed class MeetingWriteCommandsUseCase(
    PmTrackerDbContext dbContext,
    ITextNormalizer textNormalizer,
    IRecordCommentCommandsUseCase recordCommentCommandsUseCase) : IMeetingWriteCommandsUseCase
{
    private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;

    public int SaveMeeting(SaveMeetingCommand command, CurrentUserContextViewModel currentUser)
    {
        var duplicateMeetingNumberExists = dbContext.Jednani
            .AsNoTracking()
            .Any(x => x.ProjektId == command.ProjektId
                      && x.CisloJednani == command.CisloJednani
                      && (!command.Id.HasValue || x.Id != command.Id.Value));
        if (duplicateMeetingNumberExists)
        {
            throw new InvalidOperationException($"Jednání č. {command.CisloJednani} už v tomto projektu existuje.");
        }

        var statusId = ResolveMeetingStatusId(command.StavJednani);
        if (command.Id.HasValue)
        {
            var existing = dbContext.Jednani.FirstOrDefault(x => x.Id == command.Id.Value)
                ?? throw new InvalidOperationException($"Jednání {command.Id.Value} nebylo nalezeno.");
            var existingStatus = dbContext.CiselnikStavuJednani
                .AsNoTracking()
                .FirstOrDefault(x => x.Id == existing.StavJednaniId);
            if (IsMeetingReadOnly(existing, existingStatus))
            {
                throw new InvalidOperationException("Uzavřené jednání nelze upravovat. Nejprve jednání otevřete.");
            }

            existing.CisloJednani = command.CisloJednani;
            existing.DatumPlanovane = command.DatumPlanovane.Date;
            existing.CasZacatek = command.CasZacatek;
            existing.Misto = command.Misto?.Trim();
            existing.StavJednaniId = statusId;
            dbContext.SaveChanges();
            WriteAudit(currentUser.OsobaId, "jednani", existing.Id.ToString(CultureInfo.InvariantCulture), "update", null, JsonSerializer.Serialize(existing));
            return existing.Id;
        }

        var created = new JednaniEntity
        {
            ProjektId = command.ProjektId,
            CisloJednani = command.CisloJednani,
            DatumPlanovane = command.DatumPlanovane.Date,
            CasZacatek = command.CasZacatek,
            Misto = command.Misto?.Trim(),
            StavJednaniId = statusId,
            UzamklOsobaId = null
        };
        dbContext.Jednani.Add(created);
        dbContext.SaveChanges();
        CreateMeetingAttendanceSnapshot(created.Id, command.ProjektId);
        WriteAudit(currentUser.OsobaId, "jednani", created.Id.ToString(CultureInfo.InvariantCulture), "create", null, JsonSerializer.Serialize(created));
        return created.Id;
    }

    public void DeleteMeeting(DeleteMeetingCommand command, CurrentUserContextViewModel currentUser)
    {
        var meeting = dbContext.Jednani.FirstOrDefault(x => x.Id == command.JednaniId)
            ?? throw new InvalidOperationException($"Jednání {command.JednaniId} nebylo nalezeno.");

        if (meeting.ProjektId != command.ProjektId)
        {
            throw new InvalidOperationException("Jednání nepatří do zvoleného projektu.");
        }

        var attendanceRows = dbContext.Ucast
            .Where(x => x.JednaniId == command.JednaniId)
            .ToList();
        var commentRows = dbContext.Vyjadreni
            .Where(x => x.JednaniId == command.JednaniId)
            .ToList();

        var oldMeeting = JsonSerializer.Serialize(meeting);
        if (attendanceRows.Count > 0)
        {
            dbContext.Ucast.RemoveRange(attendanceRows);
        }

        if (commentRows.Count > 0)
        {
            dbContext.Vyjadreni.RemoveRange(commentRows);
        }

        if (attendanceRows.Count > 0 || commentRows.Count > 0)
        {
            // Persist dependent row deletion first to avoid FK ordering issues in providers
            // where relationships are not fully modeled in EF metadata.
            dbContext.SaveChanges();
        }

        dbContext.Jednani.Remove(meeting);
        dbContext.SaveChanges();

        var meta = JsonSerializer.Serialize(new
        {
            Meeting = meeting,
            DeletedAttendance = attendanceRows.Count,
            DeletedComments = commentRows.Count
        });
        WriteAudit(currentUser.OsobaId, "jednani", command.JednaniId.ToString(CultureInfo.InvariantCulture), "delete", oldMeeting, meta);
    }

    public void SaveAttendance(SaveAttendanceCommand command, CurrentUserContextViewModel currentUser)
    {
        var stateId = ResolveAttendanceStatusId(command.StavUcasti);
        var entity = dbContext.Ucast.FirstOrDefault(x => x.JednaniId == command.JednaniId && x.OsobaId == command.OsobaId);
        if (entity is null)
        {
            entity = new UcastEntity
            {
                JednaniId = command.JednaniId,
                OsobaId = command.OsobaId,
                StavUcastiId = stateId
            };
            dbContext.Ucast.Add(entity);
        }
        else
        {
            entity.StavUcastiId = stateId;
        }

        dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "ucast", $"{command.JednaniId}:{command.OsobaId}", "upsert", null, JsonSerializer.Serialize(entity));
    }

    public void SaveMeetingStatus(SaveMeetingStatusCommand command, CurrentUserContextViewModel currentUser)
    {
        var meeting = dbContext.Jednani.FirstOrDefault(x => x.Id == command.JednaniId)
            ?? throw new InvalidOperationException($"Jednání {command.JednaniId} nebylo nalezeno.");

        var statusId = ResolveMeetingStatusId(command.Stav);
        meeting.StavJednaniId = statusId;
        if (command.UzavritJednani)
        {
            meeting.UzamklOsobaId = currentUser.OsobaId;
        }
        else if (command.OtevritJednani)
        {
            meeting.UzamklOsobaId = null;
        }

        dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "jednani", meeting.Id.ToString(CultureInfo.InvariantCulture), "status_update", null, JsonSerializer.Serialize(meeting));
    }

    public void SaveMeetingNote(SaveMeetingNoteCommand command, CurrentUserContextViewModel currentUser)
    {
        recordCommentCommandsUseCase.AddComment(new AddCommentCommand
        {
            JednaniId = command.JednaniId,
            ZaznamId = command.ZaznamId,
            Text = command.Text
        }, currentUser);
    }

    public void AddMeetingParticipant(AddMeetingParticipantCommand command, CurrentUserContextViewModel currentUser)
    {
        if (!command.OsobaId.HasValue || command.OsobaId.Value <= 0)
        {
            throw new InvalidOperationException("Vyberte osobu z nabídky.");
        }

        var personId = command.OsobaId.Value;
        var meeting = dbContext.Jednani.AsNoTracking().FirstOrDefault(x => x.Id == command.JednaniId)
            ?? throw new InvalidOperationException($"Jednání {command.JednaniId} nebylo nalezeno.");
        if (meeting.ProjektId != command.ProjektId)
        {
            throw new InvalidOperationException("Jednání nepatří do zvoleného projektu.");
        }

        var isProjectMember = IsActiveProjectMember(command.ProjektId, personId);
        if (!isProjectMember)
        {
            throw new InvalidOperationException("Vybraná osoba není aktivním členem projektu.");
        }

        var exists = dbContext.Ucast.Any(x => x.JednaniId == command.JednaniId && x.OsobaId == personId);
        if (exists)
        {
            return;
        }

        var entity = new UcastEntity
        {
            JednaniId = command.JednaniId,
            OsobaId = personId,
            StavUcastiId = ResolveDefaultAttendanceStatusId()
        };
        dbContext.Ucast.Add(entity);
        dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "ucast", $"{command.JednaniId}:{personId}", "create", null, JsonSerializer.Serialize(entity));
    }

    private int ResolveMeetingStatusId(string value)
        => dbContext.CiselnikStavuJednani
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Stav jednání '{value}' neexistuje.");

    private int ResolveAttendanceStatusId(string value)
        => dbContext.CiselnikStavuUcasti
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Stav účasti '{value}' neexistuje.");

    private CiselnikStavuUcastiEntity? ResolveDefaultAttendanceState(IReadOnlyList<CiselnikStavuUcastiEntity> stateRows)
    {
        return stateRows.FirstOrDefault(x => Ci.Equals(x.Kod, "PRESENT"))
            ?? stateRows.FirstOrDefault(x =>
                textNormalizer.Normalize(x.Nazev).Contains("pritomen", StringComparison.OrdinalIgnoreCase))
            ?? stateRows.FirstOrDefault();
    }

    private int ResolveDefaultAttendanceStatusId()
    {
        var directStateId = dbContext.CiselnikStavuUcasti.AsNoTracking()
            .Where(x => x.Kod == "PRESENT")
            .Select(x => (int?)x.Id)
            .FirstOrDefault();
        if (directStateId.HasValue)
        {
            return directStateId.Value;
        }

        var stateRows = dbContext.CiselnikStavuUcasti.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new CiselnikStavuUcastiEntity
            {
                Id = x.Id,
                Kod = x.Kod,
                Nazev = x.Nazev
            })
            .ToList();

        return ResolveDefaultAttendanceState(stateRows)?.Id
            ?? throw new InvalidOperationException("Není nadefinován žádný stav účasti.");
    }

    private void CreateMeetingAttendanceSnapshot(int meetingId, int projectId)
    {
        var participantIds = BuildDefaultAttendanceParticipantIds(projectId);
        if (participantIds.Count == 0)
        {
            return;
        }

        var defaultStateId = ResolveDefaultAttendanceStatusId();
        var existingIds = dbContext.Ucast.AsNoTracking()
            .Where(x => x.JednaniId == meetingId)
            .Select(x => x.OsobaId)
            .ToHashSet();

        var rows = participantIds
            .Where(personId => !existingIds.Contains(personId))
            .Select(personId => new UcastEntity
            {
                JednaniId = meetingId,
                OsobaId = personId,
                StavUcastiId = defaultStateId
            })
            .ToList();
        if (rows.Count == 0)
        {
            return;
        }

        dbContext.Ucast.AddRange(rows);
        dbContext.SaveChanges();
    }

    private List<int> BuildDefaultAttendanceParticipantIds(int projectId)
    {
        var nonHostProjectRolePersonIds = (
            from assignment in dbContext.ObsazeniProjektu.AsNoTracking()
            join role in dbContext.CiselnikRoliProjektu.AsNoTracking() on assignment.RoleId equals role.Id
            where assignment.ProjektId == projectId
                && !assignment.DatumOdebrani.HasValue
            select new
            {
                assignment.OsobaId,
                role.Kod
            })
            .AsEnumerable()
            .Where(x => !Ci.Equals(x.Kod, ProjectRoleCodes.Host))
            .Select(x => x.OsobaId)
            .Distinct()
            .ToList();

        var subsystemRolePersonIds = (
            from assignment in dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            join projectSubsystem in dbContext.ProjektSubsystemy.AsNoTracking() on assignment.ProjektSubsystemId equals projectSubsystem.Id
            where projectSubsystem.ProjektId == projectId
                && !projectSubsystem.DatumOdebrani.HasValue
                && !assignment.DatumOdebrani.HasValue
            select assignment.OsobaId)
            .Distinct()
            .ToList();

        return nonHostProjectRolePersonIds
            .Concat(subsystemRolePersonIds)
            .Distinct()
            .ToList();
    }

    private bool IsActiveProjectMember(int projectId, int osobaId)
    {
        var hasProjectRole = dbContext.ObsazeniProjektu.AsNoTracking()
            .Any(x => x.ProjektId == projectId && x.OsobaId == osobaId && !x.DatumOdebrani.HasValue);
        if (hasProjectRole)
        {
            return true;
        }

        return dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .Join(
                dbContext.ProjektSubsystemy.AsNoTracking(),
                role => role.ProjektSubsystemId,
                projectSubsystem => projectSubsystem.Id,
                (role, projectSubsystem) => new { role, projectSubsystem })
            .Any(x => x.projectSubsystem.ProjektId == projectId
                && x.role.OsobaId == osobaId
                && !x.role.DatumOdebrani.HasValue
                && !x.projectSubsystem.DatumOdebrani.HasValue);
    }

    private static bool IsMeetingReadOnly(JednaniEntity? meeting, CiselnikStavuJednaniEntity? status)
        => MeetingStatePolicy.IsReadOnly(meeting, status);

    private void WriteAudit(int? actorOsobaId, string entityType, string entityId, string action, string? oldValue, string? newValue)
    {
        dbContext.AuthzAuditLog.Add(new AuthzAuditLogEntity
        {
            ActorOsobaId = actorOsobaId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedAt = DateTime.UtcNow
        });
        dbContext.SaveChanges();
    }
}
