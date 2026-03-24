using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services;

public sealed partial class MeetingService
{
    private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;
    private const byte RecordDisplayNumberTypeMeeting = 1;

    private sealed record ActiveProjectMembershipRow(
        int OsobaId,
        string Osoba,
        string? Email,
        bool HasNonHostProjectRole,
        bool HasSubsystemRole,
        IReadOnlyList<string> AktivniRole);

    public async Task<JednaniDetailViewModel> BuildJednaniDetailAsync(int id, CancellationToken ct = default)
    {
        var meeting = await dbContext.Jednani.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException($"Jednání {id} nebylo nalezeno.");

        var project = await dbContext.Projekty.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == meeting.ProjektId, ct)
            ?? throw new InvalidOperationException($"Projekt {meeting.ProjektId} nebyl nalezen.");
        var meetings = await BuildJednaniListAsync(project.Id, ct);
        var currentMeeting = meetings.FirstOrDefault(x => x.Id == id)
            ?? throw new InvalidOperationException($"Jednání {id} nebylo nalezeno v seznamu projektu.");

        var attendance = await BuildMeetingAttendanceAsync(id, project.Id, ct);
        var taskRows = await BuildMeetingTasksAsync(id, project.Id, ct);
        var meetingStatusesRaw = await dbContext.CiselnikStavuJednani.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Kod, x.Nazev })
            .ToListAsync(ct);
        var meetingStatuses = meetingStatusesRaw
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToList();
        var attendanceStatuses = await dbContext.CiselnikStavuUcasti.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToListAsync(ct);

        var openStatusCode = meetingStatusesRaw
            .Where(x => Ci.Equals(x.Kod, "OPEN") || x.Nazev.Contains("otev", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Kod)
            .FirstOrDefault();
        var closedStatusCode = meetingStatusesRaw
            .Where(x => Ci.Equals(x.Kod, "CLOSED") || x.Nazev.Contains("uzav", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Kod)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(closedStatusCode) && meeting.UzamklOsobaId.HasValue)
        {
            closedStatusCode = currentMeeting.StavKod;
        }

        return new JednaniDetailViewModel
        {
            ProjektId = project.Id,
            ProjektNazev = project.CelyNazev,
            Jednani = currentMeeting,
            OtevrenyStavKod = openStatusCode,
            UzavrenyStavKod = closedStatusCode,
            Ucast = attendance,
            Ukoly = taskRows,
            AvailableParticipantCandidates = await BuildMeetingParticipantCandidatesAsync(project.Id, meeting.Id, ct),
            StavyJednani = meetingStatuses,
            StavyUcasti = attendanceStatuses
        };
    }

    public Task<int?> GetMeetingProjectIdAsync(int meetingId, CancellationToken ct = default)
    {
        return dbContext.Jednani.AsNoTracking()
            .Where(x => x.Id == meetingId)
            .Select(x => (int?)x.ProjektId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<MeetingParticipantCandidateViewModel>> BuildMeetingParticipantCandidatesAsync(int projectId, int meetingId, CancellationToken ct = default)
        => await BuildMeetingParticipantCandidatesAsync(projectId, meetingId, includeAlreadyPresent: false, ct);

    public async Task<JednaniUkolViewModel?> GetSingleTaskAsync(int meetingId, int zaznamId, CancellationToken ct = default)
    {
        var meeting = await dbContext.Jednani.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == meetingId, ct);
        if (meeting is null)
        {
            return null;
        }

        var stateCodesById = await dbContext.CiselnikStavuUkolu.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.Kod, ct);
        var record = await dbContext.ProjektoveZaznamy.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == zaznamId && x.ProjektId == meeting.ProjektId, ct);
        if (record is null)
        {
            return null;
        }

        var stavKod = record.StavUkoluId.HasValue && stateCodesById.TryGetValue(record.StavUkoluId.Value, out var code)
            ? code
            : null;
        if (!IsMeetingTaskVisible(stavKod))
        {
            return null;
        }

        var commentsForRecord = await dbContext.Vyjadreni.AsNoTracking()
            .Where(x => x.JednaniId == meetingId && x.ZaznamId == zaznamId)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
        var requiredPersonIds = commentsForRecord
            .Select(x => x.AutorOsobaId)
            .Distinct()
            .ToList();
        var people = requiredPersonIds.Count == 0
            ? new Dictionary<int, OsobaEntity>()
            : await dbContext.Osoby.AsNoTracking()
                .Where(x => requiredPersonIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);
        var leadEquivalentOsobaIdsBySubsystem = await BuildLeadEquivalentOsobaIdsByProjectSubsystemAsync(meeting.ProjektId, ct);
        var meetingState = await dbContext.CiselnikStavuJednani.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == meeting.StavJednaniId, ct);
        var canModify = !IsMeetingReadOnly(meeting, meetingState);

        return new JednaniUkolViewModel
        {
            ZaznamId = record.Id,
            CisloZaznamu = record.CisloZaznamu,
            CisloViditelne = ResolveVisibleRecordNumber(record),
            Popis = record.Nazev,
            Zapis = string.Empty,
            LzeUpravovatVyjadreni = canModify,
            SubsystemLeadEquivalentOsobaIds = leadEquivalentOsobaIdsBySubsystem.GetValueOrDefault(record.SubsystemId, []),
            Vyjadreni = commentsForRecord
                .Select(comment => new JednaniVyjadreniViewModel
                {
                    Id = comment.Id,
                    AutorOsobaId = comment.AutorOsobaId,
                    Autor = BuildInlinePersonLabelFromOsoba(people.GetValueOrDefault(comment.AutorOsobaId)),
                    Datum = comment.DatumVyjadreni,
                    Text = comment.TextVyjadreni,
                    LzeUpravit = canModify
                })
                .ToList()
        };
    }

    private async Task<List<UcastViewModel>> BuildMeetingAttendanceAsync(int meetingId, int projectId, CancellationToken ct)
    {
        var attendances = await dbContext.Ucast.AsNoTracking()
            .Where(x => x.JednaniId == meetingId)
            .ToListAsync(ct);
        var stateRows = await dbContext.CiselnikStavuUcasti.AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
        var states = stateRows.ToDictionary(x => x.Id);
        var defaultState = ResolveDefaultAttendanceState(stateRows);

        if (attendances.Count == 0)
        {
            return await BuildLegacyMeetingAttendanceAsync(projectId, defaultState, ct);
        }

        var attendanceByPerson = attendances.ToDictionary(x => x.OsobaId);
        var participantIds = attendances.Select(x => x.OsobaId).Distinct().ToList();
        var people = participantIds.Count == 0
            ? new Dictionary<int, OsobaEntity>()
            : await dbContext.Osoby.AsNoTracking()
                .Where(x => participantIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);

        return participantIds
            .OrderBy(osobaId => BuildDisplayNameFromOsoba(people.GetValueOrDefault(osobaId)), StringComparer.CurrentCultureIgnoreCase)
            .Select(osobaId =>
            {
                var attendance = attendanceByPerson.GetValueOrDefault(osobaId);
                var attendanceState = attendance is null
                    ? defaultState
                    : states.GetValueOrDefault(attendance.StavUcastiId);

                return new UcastViewModel
                {
                    OsobaId = osobaId,
                    Osoba = BuildDisplayNameFromOsoba(people.GetValueOrDefault(osobaId)),
                    Email = people.GetValueOrDefault(osobaId)?.Email?.Trim(),
                    StavUcastiKod = attendanceState?.Kod,
                    StavUcasti = attendanceState?.Nazev ?? "-"
                };
            })
            .ToList();
    }

    private async Task<List<UcastViewModel>> BuildLegacyMeetingAttendanceAsync(
        int projectId,
        CiselnikStavuUcastiEntity? defaultState,
        CancellationToken ct)
    {
        var participants = await BuildDefaultAttendanceParticipantRowsAsync(projectId, ct);
        if (participants.Count == 0)
        {
            return [];
        }

        return participants
            .OrderBy(item => item.Osoba, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new UcastViewModel
            {
                OsobaId = item.OsobaId,
                Osoba = item.Osoba,
                Email = item.Email,
                StavUcastiKod = defaultState?.Kod,
                StavUcasti = defaultState?.Nazev ?? "-"
            })
            .ToList();
    }

    private async Task<List<JednaniUkolViewModel>> BuildMeetingTasksAsync(int meetingId, int projectId, CancellationToken ct)
    {
        var projectRecords = await dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .ToListAsync(ct);
        var stateCodesById = await dbContext.CiselnikStavuUkolu.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.Kod, ct);
        projectRecords = projectRecords
            .Where(x =>
            {
                var stavKod = x.StavUkoluId.HasValue && stateCodesById.TryGetValue(x.StavUkoluId.Value, out var code)
                    ? code
                    : null;
                return IsMeetingTaskVisible(stavKod);
            })
            .ToList();
        projectRecords = OrderRecordsByVisibleNumber(projectRecords).ToList();
        var leadEquivalentOsobaIdsBySubsystem = await BuildLeadEquivalentOsobaIdsByProjectSubsystemAsync(projectId, ct);

        var commentRows = await dbContext.Vyjadreni.AsNoTracking()
            .Where(x => x.JednaniId == meetingId)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
        var commentsByRecord = commentRows
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var requiredPersonIds = projectRecords
            .Select(record => record.VlastnikId)
            .Concat(commentRows.Select(comment => comment.AutorOsobaId))
            .Distinct()
            .ToList();
        var people = requiredPersonIds.Count == 0
            ? new Dictionary<int, OsobaEntity>()
            : await dbContext.Osoby.AsNoTracking()
                .Where(x => requiredPersonIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);
        var meeting = await dbContext.Jednani.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == meetingId, ct);
        var meetingState = meeting is null
            ? null
            : await dbContext.CiselnikStavuJednani.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == meeting.StavJednaniId, ct);
        var canModify = !IsMeetingReadOnly(meeting, meetingState);

        return projectRecords.Select(record => new JednaniUkolViewModel
        {
            ZaznamId = record.Id,
            CisloZaznamu = record.CisloZaznamu,
            CisloViditelne = ResolveVisibleRecordNumber(record),
            Popis = record.Nazev,
            Zapis = string.Empty,
            LzeUpravovatVyjadreni = canModify,
            SubsystemLeadEquivalentOsobaIds = leadEquivalentOsobaIdsBySubsystem.GetValueOrDefault(record.SubsystemId, []),
            Vyjadreni = (commentsByRecord.TryGetValue(record.Id, out var commentsForRecord)
                    ? (IEnumerable<VyjadreniEntity>)commentsForRecord
                    : Array.Empty<VyjadreniEntity>())
                .Select(comment => new JednaniVyjadreniViewModel
                {
                    Id = comment.Id,
                    AutorOsobaId = comment.AutorOsobaId,
                    Autor = BuildInlinePersonLabelFromOsoba(people.GetValueOrDefault(comment.AutorOsobaId)),
                    Datum = comment.DatumVyjadreni,
                    Text = comment.TextVyjadreni,
                    LzeUpravit = canModify
                })
                .ToList()
        }).ToList();
    }

    private async Task<List<MeetingParticipantCandidateViewModel>> BuildMeetingParticipantCandidatesAsync(
        int projectId,
        int meetingId,
        bool includeAlreadyPresent,
        CancellationToken ct)
    {
        var alreadyPresentIds = (await dbContext.Ucast.AsNoTracking()
            .Where(x => x.JednaniId == meetingId)
            .Select(x => x.OsobaId)
            .ToListAsync(ct))
            .ToHashSet();
        var activeRoles = await BuildActiveProjectMembershipRowsAsync(projectId, ct);

        return activeRoles
            .Select(group => new MeetingParticipantCandidateViewModel
            {
                OsobaId = group.OsobaId,
                Osoba = group.Osoba,
                Email = group.Email,
                AktivniRole = group.AktivniRole
            })
            .Where(x => includeAlreadyPresent || !alreadyPresentIds.Contains(x.OsobaId))
            .OrderBy(x => x.Osoba, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private async Task<List<ActiveProjectMembershipRow>> BuildDefaultAttendanceParticipantRowsAsync(int projectId, CancellationToken ct)
    {
        var rows = await BuildActiveProjectMembershipRowsAsync(projectId, ct);
        return rows
            .Where(item => item.HasNonHostProjectRole || item.HasSubsystemRole)
            .ToList();
    }

    private async Task<List<ActiveProjectMembershipRow>> BuildActiveProjectMembershipRowsAsync(int projectId, CancellationToken ct)
    {
        var projectRoleRows = await (
            from assignment in dbContext.ObsazeniProjektu.AsNoTracking()
            join role in dbContext.CiselnikRoliProjektu.AsNoTracking() on assignment.RoleId equals role.Id
            join person in dbContext.Osoby.AsNoTracking() on assignment.OsobaId equals person.Id
            where assignment.ProjektId == projectId
                && !assignment.DatumOdebrani.HasValue
            select new
            {
                OsobaId = assignment.OsobaId,
                person.Titul,
                person.Jmeno,
                person.Prijmeni,
                person.Email,
                RoleKod = role.Kod,
                RoleNazev = role.Nazev
            })
            .ToListAsync(ct);

        var subsystemRoleRows = await (
            from assignment in dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            join projectSubsystem in dbContext.ProjektSubsystemy.AsNoTracking() on assignment.ProjektSubsystemId equals projectSubsystem.Id
            join role in dbContext.CiselnikRoliSubsystemu.AsNoTracking() on assignment.RoleSubsystemuId equals role.Id
            join person in dbContext.Osoby.AsNoTracking() on assignment.OsobaId equals person.Id
            join subsystem in dbContext.Subsystemy.AsNoTracking() on projectSubsystem.SubsystemId equals subsystem.Id
            where projectSubsystem.ProjektId == projectId
                && !projectSubsystem.DatumOdebrani.HasValue
                && !assignment.DatumOdebrani.HasValue
            select new
            {
                OsobaId = assignment.OsobaId,
                person.Titul,
                person.Jmeno,
                person.Prijmeni,
                person.Email,
                RoleNazev = role.Nazev,
                SubsystemKod = subsystem.Kod,
                SubsystemNazev = subsystem.Nazev
            })
            .ToListAsync(ct);

        var roleFragments = projectRoleRows
            .Select(item => new
            {
                item.OsobaId,
                Osoba = BuildDisplayName(item.Titul, item.Jmeno, item.Prijmeni, item.OsobaId),
                Email = item.Email?.Trim(),
                HasNonHostProjectRole = !Ci.Equals(item.RoleKod, ProjectRoleCodes.Host),
                HasSubsystemRole = false,
                RoleLabel = item.RoleNazev
            })
            .Concat(subsystemRoleRows.Select(item => new
            {
                item.OsobaId,
                Osoba = BuildDisplayName(item.Titul, item.Jmeno, item.Prijmeni, item.OsobaId),
                Email = item.Email?.Trim(),
                HasNonHostProjectRole = false,
                HasSubsystemRole = true,
                RoleLabel = BuildSubsystemRoleLabel(item.RoleNazev, item.SubsystemKod, item.SubsystemNazev)
            }))
            .ToList();

        return roleFragments
            .GroupBy(item => item.OsobaId)
            .Select(group =>
            {
                var first = group.First();
                return new ActiveProjectMembershipRow(
                    group.Key,
                    first.Osoba,
                    first.Email,
                    group.Any(item => item.HasNonHostProjectRole),
                    group.Any(item => item.HasSubsystemRole),
                    group.Select(item => item.RoleLabel)
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Distinct(Ci)
                        .OrderBy(item => item, StringComparer.CurrentCultureIgnoreCase)
                        .ToList());
            })
            .OrderBy(item => item.Osoba, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private async Task<Dictionary<int, List<int>>> BuildLeadEquivalentOsobaIdsByProjectSubsystemAsync(int projectId, CancellationToken ct)
    {
        var leadRoleIds = (await dbContext.CiselnikRoliSubsystemu.AsNoTracking()
            .Where(x => x.Kod == SubsystemRoleCodes.Lead || x.Kod == SubsystemRoleCodes.DeputyLead)
            .Select(x => x.Id)
            .ToListAsync(ct))
            .ToHashSet();
        if (leadRoleIds.Count == 0)
        {
            return [];
        }

        var activeProjectSubsystems = await dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
            .ToListAsync(ct);
        var subsystemIdByProjectSubsystemId = activeProjectSubsystems.ToDictionary(x => x.Id, x => x.SubsystemId);
        var activeProjectSubsystemIds = subsystemIdByProjectSubsystemId.Keys.ToHashSet();
        var rows = await dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .Where(x => activeProjectSubsystemIds.Contains(x.ProjektSubsystemId)
                && !x.DatumOdebrani.HasValue
                && leadRoleIds.Contains(x.RoleSubsystemuId))
            .ToListAsync(ct);

        return rows
            .GroupBy(x => subsystemIdByProjectSubsystemId[x.ProjektSubsystemId])
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x.OsobaId).Distinct().OrderBy(x => x).ToList());
    }

    private CiselnikStavuUcastiEntity? ResolveDefaultAttendanceState(IReadOnlyList<CiselnikStavuUcastiEntity> stateRows)
    {
        return stateRows.FirstOrDefault(x => Ci.Equals(x.Kod, "PRESENT"))
            ?? stateRows.FirstOrDefault(x =>
                textNormalizer.Normalize(x.Nazev).Contains("pritomen", StringComparison.OrdinalIgnoreCase))
            ?? stateRows.FirstOrDefault();
    }

    private string BuildDisplayName(string? titul, string jmeno, string prijmeni, int id)
    {
        var displayName = personIdentityMatcher.BuildPersonName(titul, jmeno, prijmeni);
        return string.IsNullOrWhiteSpace(displayName) ? $"Uživatel #{id}" : displayName;
    }

    private string BuildInlinePersonLabel(string? titul, string jmeno, string prijmeni, string? email, int id)
    {
        var displayName = BuildDisplayName(titul, jmeno, prijmeni, id);
        var normalizedEmail = textNormalizer.NormalizeEmail(email);
        return string.IsNullOrWhiteSpace(normalizedEmail)
            ? displayName
            : $"{displayName} <{normalizedEmail}>";
    }

    private string BuildDisplayNameFromOsoba(OsobaEntity? osoba)
    {
        if (osoba is null)
        {
            return "-";
        }

        return BuildDisplayName(osoba.Titul, osoba.Jmeno, osoba.Prijmeni, osoba.Id);
    }

    private string BuildInlinePersonLabelFromOsoba(OsobaEntity? osoba)
    {
        if (osoba is null)
        {
            return "-";
        }

        return BuildInlinePersonLabel(osoba.Titul, osoba.Jmeno, osoba.Prijmeni, osoba.Email, osoba.Id);
    }

    private static string ResolveVisibleRecordNumber(ProjektovyZaznamEntity record)
    {
        if (!string.IsNullOrWhiteSpace(record.CisloViditelne))
        {
            return record.CisloViditelne.Trim();
        }

        return record.CisloZaznamu.ToString(CultureInfo.InvariantCulture);
    }

    private static int ResolveVisibleNumberPartA(ProjektovyZaznamEntity record)
    {
        if (record.CisloViditelneA > 0)
        {
            return record.CisloViditelneA;
        }

        return Math.Max(0, record.CisloZaznamu);
    }

    private static int ResolveVisibleNumberPartB(ProjektovyZaznamEntity record)
    {
        if (record.CisloViditelneTyp == RecordDisplayNumberTypeMeeting)
        {
            return Math.Max(1, record.CisloViditelneB);
        }

        return 0;
    }

    private static IEnumerable<ProjektovyZaznamEntity> OrderRecordsByVisibleNumber(IEnumerable<ProjektovyZaznamEntity> rows)
        => rows
            .OrderBy(ResolveVisibleNumberPartA)
            .ThenBy(ResolveVisibleNumberPartB)
            .ThenBy(x => x.CisloZaznamu);

    private static bool IsMeetingTaskVisible(string? stavKod)
    {
        return !Ci.Equals(stavKod, "END")
            && !Ci.Equals(stavKod, "CANCELLED");
    }

    private static string BuildSubsystemRoleLabel(string roleName, string? subsystemCode, string? subsystemName)
    {
        var subsystemLabel = !string.IsNullOrWhiteSpace(subsystemCode)
            ? subsystemCode
            : subsystemName;

        return string.IsNullOrWhiteSpace(subsystemLabel)
            ? roleName
            : $"{roleName} ({subsystemLabel})";
    }

    private static bool IsMeetingReadOnly(JednaniEntity? meeting, CiselnikStavuJednaniEntity? status)
        => MeetingStatePolicy.IsReadOnly(meeting, status);
}
