using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Audit;

namespace PmTracker.Web.Services;

public sealed partial class MeetingService
{

    public async Task<int> SaveMeetingAsync(SaveMeetingCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var duplicateMeetingNumberExists = await dbContext.Jednani
            .AsNoTracking()
            .AnyAsync(x => x.ProjektId == command.ProjektId
                           && x.CisloJednani == command.CisloJednani
                           && (!command.Id.HasValue || x.Id != command.Id.Value), ct);
        if (duplicateMeetingNumberExists)
        {
            throw new InvalidOperationException($"Jednání č. {command.CisloJednani} už v tomto projektu existuje.");
        }

        var statusId = await ResolveMeetingStatusIdAsync(command.StavJednani, ct);
        if (command.Id.HasValue)
        {
            var existing = await dbContext.Jednani
                .FirstOrDefaultAsync(x => x.Id == command.Id.Value, ct)
                ?? throw new InvalidOperationException($"Jednání {command.Id.Value} nebylo nalezeno.");
            var existingStatus = await dbContext.CiselnikStavuJednani
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == existing.StavJednaniId, ct);
            if (IsMeetingReadOnly(existing, existingStatus))
            {
                throw new InvalidOperationException("Uzavřené jednání nelze upravovat. Nejprve jednání otevřete.");
            }

            var old = MeetingAuditSnapshot.FromEntity(existing);
            existing.CisloJednani = command.CisloJednani;
            existing.DatumPlanovane = command.DatumPlanovane.Date;
            existing.CasZacatek = command.CasZacatek;
            existing.Misto = command.Misto?.Trim();
            existing.StavJednaniId = statusId;
            await dbContext.SaveChangesAsync(ct);
            auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                AuditActionType.Update,
                AuditEntityType.Meeting,
                existing.Id.ToString(CultureInfo.InvariantCulture),
                old,
                MeetingAuditSnapshot.FromEntity(existing)));
            await dbContext.SaveChangesAsync(ct);
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
        await dbContext.SaveChangesAsync(ct);
        await CreateMeetingAttendanceSnapshotAsync(created.Id, command.ProjektId, currentUser.OsobaId, ct);
        auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Create,
            AuditEntityType.Meeting,
            created.Id.ToString(CultureInfo.InvariantCulture),
            null,
            MeetingAuditSnapshot.FromEntity(created)));
        await dbContext.SaveChangesAsync(ct);
        return created.Id;
    }

    public async Task DeleteMeetingAsync(DeleteMeetingCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var meeting = await dbContext.Jednani
            .FirstOrDefaultAsync(x => x.Id == command.JednaniId, ct)
            ?? throw new InvalidOperationException($"Jednání {command.JednaniId} nebylo nalezeno.");

        if (meeting.ProjektId != command.ProjektId)
        {
            throw new InvalidOperationException("Jednání nepatří do zvoleného projektu.");
        }

        var attendanceRows = await dbContext.Ucast
            .Where(x => x.JednaniId == command.JednaniId)
            .ToListAsync(ct);
        var commentRows = await dbContext.Vyjadreni
            .Where(x => x.JednaniId == command.JednaniId)
            .ToListAsync(ct);

        var oldMeeting = MeetingAuditSnapshot.FromEntity(meeting);
        var oldAttendanceSnapshots = attendanceRows.Select(AttendanceAuditSnapshot.FromEntity).ToList();
        var oldCommentSnapshots = commentRows.Select(CommentAuditSnapshot.FromEntity).ToList();
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
            await dbContext.SaveChangesAsync(ct);
        }

        dbContext.Jednani.Remove(meeting);
        await dbContext.SaveChangesAsync(ct);

        foreach (var oldAttendanceSnapshot in oldAttendanceSnapshots)
        {
            auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                AuditActionType.Delete,
                AuditEntityType.Attendance,
                $"{oldAttendanceSnapshot.JednaniId}:{oldAttendanceSnapshot.OsobaId}",
                oldAttendanceSnapshot,
                null));
        }

        foreach (var oldCommentSnapshot in oldCommentSnapshots)
        {
            auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                AuditActionType.Delete,
                AuditEntityType.Comment,
                oldCommentSnapshot.Id.ToString(CultureInfo.InvariantCulture),
                oldCommentSnapshot,
                null));
        }

        auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Delete,
            AuditEntityType.Meeting,
            command.JednaniId.ToString(CultureInfo.InvariantCulture),
            oldMeeting,
            null));
        await dbContext.SaveChangesAsync(ct);
    }

    public Task SaveAttendanceAsync(SaveAttendanceCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
        => SaveAttendanceBatchAsync(
            command.JednaniId,
            [(command.OsobaId, command.StavUcasti)],
            currentUser,
            ct);

    public async Task SaveAttendanceBatchAsync(int meetingId, IEnumerable<(int OsobaId, string StavUcasti)> rows, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var normalizedRows = rows
            .Where(x => x.OsobaId > 0 && !string.IsNullOrWhiteSpace(x.StavUcasti))
            .Select(x => (x.OsobaId, StavUcasti: x.StavUcasti.Trim()))
            .ToList();
        if (normalizedRows.Count == 0)
        {
            return;
        }

        var meetingExists = await dbContext.Jednani.AsNoTracking()
            .AnyAsync(x => x.Id == meetingId, ct);
        if (!meetingExists)
        {
            throw new InvalidOperationException($"Jednání {meetingId} nebylo nalezeno.");
        }

        var stateRows = await dbContext.CiselnikStavuUcasti.AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
        var stateIdByValue = BuildAttendanceStateIdMap(stateRows);
        var participantIds = normalizedRows.Select(x => x.OsobaId).Distinct().ToList();
        var existingRows = await dbContext.Ucast
            .Where(x => x.JednaniId == meetingId && participantIds.Contains(x.OsobaId))
            .ToDictionaryAsync(x => x.OsobaId, ct);

        foreach (var row in normalizedRows)
        {
            if (!stateIdByValue.TryGetValue(row.StavUcasti, out var stateId))
            {
                throw new InvalidOperationException($"Stav účasti '{row.StavUcasti}' neexistuje.");
            }

            if (!existingRows.TryGetValue(row.OsobaId, out var entity))
            {
                entity = new UcastEntity
                {
                    JednaniId = meetingId,
                    OsobaId = row.OsobaId,
                    StavUcastiId = stateId
                };
                dbContext.Ucast.Add(entity);
                existingRows[row.OsobaId] = entity;
                await dbContext.SaveChangesAsync(ct);
                auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                    AuditActionType.Create,
                    AuditEntityType.Attendance,
                    $"{meetingId}:{row.OsobaId}",
                    null,
                    AttendanceAuditSnapshot.FromEntity(entity)));
            }
            else
            {
                var old = AttendanceAuditSnapshot.FromEntity(entity);
                entity.StavUcastiId = stateId;
                auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                    AuditActionType.Update,
                    AuditEntityType.Attendance,
                    $"{meetingId}:{row.OsobaId}",
                    old,
                    AttendanceAuditSnapshot.FromEntity(entity)));
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }

    public async Task SaveMeetingStatusAsync(SaveMeetingStatusCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var meeting = await dbContext.Jednani
            .FirstOrDefaultAsync(x => x.Id == command.JednaniId, ct)
            ?? throw new InvalidOperationException($"Jednání {command.JednaniId} nebylo nalezeno.");

        var old = MeetingAuditSnapshot.FromEntity(meeting);
        var statusId = await ResolveMeetingStatusIdAsync(command.Stav, ct);
        meeting.StavJednaniId = statusId;
        if (command.UzavritJednani)
        {
            meeting.UzamklOsobaId = currentUser.OsobaId;
        }
        else if (command.OtevritJednani)
        {
            meeting.UzamklOsobaId = null;
        }

        await dbContext.SaveChangesAsync(ct);
        auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Update,
            AuditEntityType.Meeting,
            meeting.Id.ToString(CultureInfo.InvariantCulture),
            old,
            MeetingAuditSnapshot.FromEntity(meeting)));
        await dbContext.SaveChangesAsync(ct);
    }

    public Task SaveMeetingNoteAsync(SaveMeetingNoteCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
        => commentService.SaveMeetingNoteAsync(command, currentUser, ct);

    public Task SaveMeetingNotesBatchAsync(int meetingId, IEnumerable<(int ZaznamId, string Text)> rows, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
        => commentService.SaveMeetingNotesBatchAsync(meetingId, rows, currentUser, ct);

    public async Task AddMeetingParticipantAsync(AddMeetingParticipantCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        if (!command.OsobaId.HasValue || command.OsobaId.Value <= 0)
        {
            throw new InvalidOperationException("Vyberte osobu z nabídky.");
        }

        var personId = command.OsobaId.Value;
        var meeting = await dbContext.Jednani.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == command.JednaniId, ct)
            ?? throw new InvalidOperationException($"Jednání {command.JednaniId} nebylo nalezeno.");
        if (meeting.ProjektId != command.ProjektId)
        {
            throw new InvalidOperationException("Jednání nepatří do zvoleného projektu.");
        }

        var isProjectMember = await IsActiveProjectMemberAsync(command.ProjektId, personId, ct);
        if (!isProjectMember)
        {
            throw new InvalidOperationException("Vybraná osoba není aktivním členem projektu.");
        }

        var exists = await dbContext.Ucast.AsNoTracking()
            .AnyAsync(x => x.JednaniId == command.JednaniId && x.OsobaId == personId, ct);
        if (exists)
        {
            return;
        }

        var entity = new UcastEntity
        {
            JednaniId = command.JednaniId,
            OsobaId = personId,
            StavUcastiId = await ResolveDefaultAttendanceStatusIdAsync(ct)
        };
        dbContext.Ucast.Add(entity);
        await dbContext.SaveChangesAsync(ct);
        auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Create,
            AuditEntityType.Attendance,
            $"{command.JednaniId}:{personId}",
            null,
            AttendanceAuditSnapshot.FromEntity(entity)));
        await dbContext.SaveChangesAsync(ct);
    }

    private Task<int?> ResolveMeetingStatusIdQueryAsync(string value, CancellationToken ct)
        => dbContext.CiselnikStavuJednani
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);

    private async Task<int> ResolveMeetingStatusIdAsync(string value, CancellationToken ct)
        => await ResolveMeetingStatusIdQueryAsync(value, ct)
            ?? throw new InvalidOperationException($"Stav jednání '{value}' neexistuje.");

    private async Task<int> ResolveDefaultAttendanceStatusIdAsync(CancellationToken ct)
    {
        var directStateId = await dbContext.CiselnikStavuUcasti.AsNoTracking()
            .Where(x => x.Kod == "PRESENT")
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);
        if (directStateId.HasValue)
        {
            return directStateId.Value;
        }

        var stateRows = await dbContext.CiselnikStavuUcasti.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new CiselnikStavuUcastiEntity
            {
                Id = x.Id,
                Kod = x.Kod,
                Nazev = x.Nazev
            })
            .ToListAsync(ct);

        return ResolveDefaultAttendanceState(stateRows)?.Id
            ?? throw new InvalidOperationException("Není nadefinován žádný stav účasti.");
    }

    private async Task CreateMeetingAttendanceSnapshotAsync(int meetingId, int projectId, int? actorOsobaId, CancellationToken ct)
    {
        var participantIds = await BuildDefaultAttendanceParticipantIdsAsync(projectId, ct);
        if (participantIds.Count == 0)
        {
            return;
        }

        var defaultStateId = await ResolveDefaultAttendanceStatusIdAsync(ct);
        var existingIds = (await dbContext.Ucast.AsNoTracking()
            .Where(x => x.JednaniId == meetingId)
            .Select(x => x.OsobaId)
            .ToListAsync(ct))
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
        await dbContext.SaveChangesAsync(ct);
        foreach (var row in rows)
        {
            auditWriteService.Add(actorOsobaId, new AuditWriteEntry(
                AuditActionType.Create,
                AuditEntityType.Attendance,
                $"{row.JednaniId}:{row.OsobaId}",
                null,
                AttendanceAuditSnapshot.FromEntity(row)));
        }

        await dbContext.SaveChangesAsync(ct);
    }

    private async Task<List<int>> BuildDefaultAttendanceParticipantIdsAsync(int projectId, CancellationToken ct)
    {
        var nonHostProjectRoleRows = await (
            from assignment in dbContext.ObsazeniProjektu.AsNoTracking()
            join role in dbContext.CiselnikRoliProjektu.AsNoTracking() on assignment.RoleId equals role.Id
            where assignment.ProjektId == projectId
                && !assignment.DatumOdebrani.HasValue
            select new
            {
                assignment.OsobaId,
                role.Kod
            })
            .ToListAsync(ct);

        var subsystemRolePersonIds = await (
            from assignment in dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            join projectSubsystem in dbContext.ProjektSubsystemy.AsNoTracking() on assignment.ProjektSubsystemId equals projectSubsystem.Id
            where projectSubsystem.ProjektId == projectId
                && !projectSubsystem.DatumOdebrani.HasValue
                && !assignment.DatumOdebrani.HasValue
            select assignment.OsobaId)
            .Distinct()
            .ToListAsync(ct);

        return nonHostProjectRoleRows
            .Where(x => !Ci.Equals(x.Kod, ProjectRoleCodes.Host))
            .Select(x => x.OsobaId)
            .Concat(subsystemRolePersonIds)
            .Distinct()
            .ToList();
    }

    private async Task<bool> IsActiveProjectMemberAsync(int projectId, int osobaId, CancellationToken ct)
    {
        var hasProjectRole = await dbContext.ObsazeniProjektu.AsNoTracking()
            .AnyAsync(x => x.ProjektId == projectId && x.OsobaId == osobaId && !x.DatumOdebrani.HasValue, ct);
        if (hasProjectRole)
        {
            return true;
        }

        return await dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .Join(
                dbContext.ProjektSubsystemy.AsNoTracking(),
                role => role.ProjektSubsystemId,
                projectSubsystem => projectSubsystem.Id,
                (role, projectSubsystem) => new { role, projectSubsystem })
            .AnyAsync(x => x.projectSubsystem.ProjektId == projectId
                && x.role.OsobaId == osobaId
                && !x.role.DatumOdebrani.HasValue
                && !x.projectSubsystem.DatumOdebrani.HasValue, ct);
    }

    private static Dictionary<string, int> BuildAttendanceStateIdMap(IEnumerable<CiselnikStavuUcastiEntity> stateRows)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in stateRows)
        {
            if (!string.IsNullOrWhiteSpace(row.Kod) && !result.ContainsKey(row.Kod))
            {
                result[row.Kod] = row.Id;
            }

            if (!string.IsNullOrWhiteSpace(row.Nazev) && !result.ContainsKey(row.Nazev))
            {
                result[row.Nazev] = row.Id;
            }
        }

        return result;
    }

}
