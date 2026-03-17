using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services.Data;

public sealed class MeetingDetailQueriesUseCase(
    PmTrackerDbContext dbContext,
    IMeetingListQueriesUseCase meetingListQueriesUseCase,
    ITextNormalizer textNormalizer,
    IPersonIdentityMatcher personIdentityMatcher) : IMeetingDetailQueriesUseCase
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

    public JednaniDetailViewModel BuildJednaniDetail(int id)
    {
        var meeting = dbContext.Jednani.AsNoTracking().FirstOrDefault(x => x.Id == id)
            ?? throw new InvalidOperationException($"Jednání {id} nebylo nalezeno.");

        var project = dbContext.Projekty.AsNoTracking().First(x => x.Id == meeting.ProjektId);
        var meetings = meetingListQueriesUseCase.BuildJednaniList(project.Id);
        var currentMeeting = meetings.First(x => x.Id == id);

        var attendance = BuildMeetingAttendance(id, project.Id);
        var taskRows = BuildMeetingTasks(id, project.Id);
        var meetingStatusesRaw = dbContext.CiselnikStavuJednani.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Kod, x.Nazev })
            .ToList();
        var meetingStatuses = meetingStatusesRaw
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToList();
        var attendanceStatuses = dbContext.CiselnikStavuUcasti.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToList();

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
            AvailableParticipantCandidates = BuildMeetingParticipantCandidates(project.Id, meeting.Id),
            StavyJednani = meetingStatuses,
            StavyUcasti = attendanceStatuses
        };
    }

    private List<UcastViewModel> BuildMeetingAttendance(int meetingId, int projectId)
    {
        var attendances = dbContext.Ucast.AsNoTracking().Where(x => x.JednaniId == meetingId).ToList();
        var stateRows = dbContext.CiselnikStavuUcasti.AsNoTracking().OrderBy(x => x.Id).ToList();
        var states = stateRows.ToDictionary(x => x.Id);
        var defaultState = ResolveDefaultAttendanceState(stateRows);

        if (attendances.Count == 0)
        {
            return BuildLegacyMeetingAttendance(projectId, defaultState);
        }

        var attendanceByPerson = attendances.ToDictionary(x => x.OsobaId);
        var participantIds = attendances.Select(x => x.OsobaId).Distinct().ToList();
        var people = dbContext.Osoby.AsNoTracking()
            .Where(x => participantIds.Contains(x.Id))
            .ToDictionary(x => x.Id);

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

    private List<UcastViewModel> BuildLegacyMeetingAttendance(
        int projectId,
        CiselnikStavuUcastiEntity? defaultState)
    {
        var participants = BuildDefaultAttendanceParticipantRows(projectId);
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

    private List<JednaniUkolViewModel> BuildMeetingTasks(int meetingId, int projectId)
    {
        var projectRecords = dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .ToList();
        var stateCodesById = dbContext.CiselnikStavuUkolu.AsNoTracking()
            .ToDictionary(x => x.Id, x => x.Kod);
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
        var leadEquivalentOsobaIdsBySubsystem = BuildLeadEquivalentOsobaIdsByProjectSubsystem(projectId);

        var commentsByRecord = dbContext.Vyjadreni.AsNoTracking()
            .Where(x => x.JednaniId == meetingId)
            .OrderBy(x => x.Id)
            .ToList()
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var people = dbContext.Osoby.AsNoTracking().ToDictionary(x => x.Id);
        var meeting = dbContext.Jednani.AsNoTracking().FirstOrDefault(x => x.Id == meetingId);
        var meetingState = meeting is null
            ? null
            : dbContext.CiselnikStavuJednani.AsNoTracking().FirstOrDefault(x => x.Id == meeting.StavJednaniId);
        var canModify = !IsMeetingReadOnly(meeting, meetingState);

        return projectRecords.Select(record => new JednaniUkolViewModel
        {
            ZaznamId = record.Id,
            CisloZaznamu = record.CisloZaznamu,
            CisloViditelne = ResolveVisibleRecordNumber(record),
            Popis = record.Nazev,
            Zapis = string.Empty,
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

    private List<MeetingParticipantCandidateViewModel> BuildMeetingParticipantCandidates(int projectId, int meetingId)
    {
        var alreadyPresentIds = dbContext.Ucast.AsNoTracking()
            .Where(x => x.JednaniId == meetingId)
            .Select(x => x.OsobaId)
            .ToHashSet();
        var activeRoles = BuildActiveProjectMembershipRows(projectId)
            .Select(group => new MeetingParticipantCandidateViewModel
            {
                OsobaId = group.OsobaId,
                Osoba = group.Osoba,
                Email = group.Email,
                AktivniRole = group.AktivniRole
            })
            .Where(x => !alreadyPresentIds.Contains(x.OsobaId))
            .OrderBy(x => x.Osoba, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return activeRoles;
    }

    private List<ActiveProjectMembershipRow> BuildDefaultAttendanceParticipantRows(int projectId)
    {
        return BuildActiveProjectMembershipRows(projectId)
            .Where(item => item.HasNonHostProjectRole || item.HasSubsystemRole)
            .ToList();
    }

    private List<ActiveProjectMembershipRow> BuildActiveProjectMembershipRows(int projectId)
    {
        var projectRoleRows = (
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
            .ToList();

        var subsystemRoleRows = (
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
            .ToList();

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

    private Dictionary<int, List<int>> BuildLeadEquivalentOsobaIdsByProjectSubsystem(int projectId)
    {
        var leadRoleIds = dbContext.CiselnikRoliSubsystemu.AsNoTracking()
            .Where(x => x.Kod == SubsystemRoleCodes.Lead || x.Kod == SubsystemRoleCodes.DeputyLead)
            .Select(x => x.Id)
            .ToHashSet();
        if (leadRoleIds.Count == 0)
        {
            return [];
        }

        var activeProjectSubsystems = dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
            .ToList();
        var activeProjectSubsystemIds = activeProjectSubsystems.Select(x => x.Id).ToHashSet();

        return dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .Where(x => activeProjectSubsystemIds.Contains(x.ProjektSubsystemId)
                && !x.DatumOdebrani.HasValue
                && leadRoleIds.Contains(x.RoleSubsystemuId))
            .ToList()
            .GroupBy(x => activeProjectSubsystems.First(ps => ps.Id == x.ProjektSubsystemId).SubsystemId)
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
