using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Modules.Export.Queries;

public sealed class ExportAttendanceProjectionBuilder(
    PmTrackerDbContext dbContext,
    ITextNormalizer textNormalizer,
    IPersonIdentityMatcher personIdentityMatcher) : IExportAttendanceProjectionBuilder
{
    private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;

    private sealed record ProjectRoleExportRow(int OsobaId, string Osoba, string RoleKod);
    private sealed record SubsystemRoleExportRow(int OsobaId, string Osoba);

    public List<PdfAttendanceGroupViewModel> BuildAttendanceGroups(int meetingId, int projectId)
    {
        var attendanceRows = dbContext.Ucast.AsNoTracking()
            .Where(x => x.JednaniId == meetingId)
            .ToList();

        if (attendanceRows.Count == 0)
        {
            return BuildLegacyAttendanceGroups(projectId);
        }

        var attendanceStates = dbContext.CiselnikStavuUcasti.AsNoTracking().ToDictionary(x => x.Id);
        var people = dbContext.Osoby.AsNoTracking().ToDictionary(x => x.Id);

        return attendanceRows
            .GroupBy(x => x.StavUcastiId)
            .Select(group => new
            {
                State = attendanceStates.GetValueOrDefault(group.Key),
                Names = group
                    .Select(item => BuildDisplayNameFromOsoba(people.GetValueOrDefault(item.OsobaId)))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(Ci)
                    .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList()
            })
            .Where(group => group.Names.Count > 0)
            .OrderBy(group => AttendancePrintOrder(group.State))
            .ThenBy(group => group.State?.Nazev ?? "Bez stavu", StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new PdfAttendanceGroupViewModel
            {
                Stav = group.State?.Nazev ?? "Bez stavu",
                Osoby = group.Names
            })
            .ToList();
    }

    private List<PdfAttendanceGroupViewModel> BuildLegacyAttendanceGroups(int projectId)
    {
        var participantNamesById = new Dictionary<int, string>();
        foreach (var assignment in BuildActiveProjectRoleAssignments(projectId))
        {
            if (!Ci.Equals(assignment.RoleKod, ProjectRoleCodes.Host))
            {
                participantNamesById[assignment.OsobaId] = assignment.Osoba;
            }
        }

        foreach (var assignment in BuildActiveProjectSubsystemRoleAssignments(projectId))
        {
            participantNamesById[assignment.OsobaId] = assignment.Osoba;
        }

        if (participantNamesById.Count == 0)
        {
            return [];
        }

        var defaultState = ResolveDefaultAttendanceState(dbContext.CiselnikStavuUcasti.AsNoTracking().OrderBy(x => x.Id).ToList());

        return
        [
            new PdfAttendanceGroupViewModel
            {
                Stav = defaultState?.Nazev ?? "-",
                Osoby = participantNamesById.Values
                    .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList()
            }
        ];
    }

    private List<ProjectRoleExportRow> BuildActiveProjectRoleAssignments(int projectId)
    {
        var assignments = dbContext.ObsazeniProjektu.AsNoTracking()
            .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
            .ToList();
        var roles = dbContext.CiselnikRoliProjektu.AsNoTracking().ToDictionary(x => x.Id);
        var people = dbContext.Osoby.AsNoTracking().ToDictionary(x => x.Id);

        return assignments
            .Select(assignment =>
            {
                var person = people.GetValueOrDefault(assignment.OsobaId);
                var role = roles.GetValueOrDefault(assignment.RoleId);
                return new ProjectRoleExportRow(
                    assignment.OsobaId,
                    BuildDisplayNameFromOsoba(person),
                    role?.Kod ?? "-");
            })
            .ToList();
    }

    private List<SubsystemRoleExportRow> BuildActiveProjectSubsystemRoleAssignments(int projectId)
    {
        var projectSubsystems = dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
            .ToList();
        var projectSubsystemIds = projectSubsystems.Select(x => x.Id).ToHashSet();
        var assignments = dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .Where(x => projectSubsystemIds.Contains(x.ProjektSubsystemId) && !x.DatumOdebrani.HasValue)
            .ToList();
        var people = dbContext.Osoby.AsNoTracking().ToDictionary(x => x.Id);

        return assignments
            .Select(assignment => new SubsystemRoleExportRow(
                assignment.OsobaId,
                BuildDisplayNameFromOsoba(people.GetValueOrDefault(assignment.OsobaId))))
            .ToList();
    }

    private CiselnikStavuUcastiEntity? ResolveDefaultAttendanceState(IReadOnlyList<CiselnikStavuUcastiEntity> stateRows)
    {
        return stateRows.FirstOrDefault(x => Ci.Equals(x.Kod, "PRESENT"))
            ?? stateRows.FirstOrDefault(x =>
                textNormalizer.Normalize(x.Nazev).Contains("pritomen", StringComparison.OrdinalIgnoreCase))
            ?? stateRows.FirstOrDefault();
    }

    private int AttendancePrintOrder(CiselnikStavuUcastiEntity? state)
    {
        if (state is null)
        {
            return 100;
        }

        if (Ci.Equals(state.Kod, "PRESENT"))
        {
            return 1;
        }

        if (Ci.Equals(state.Kod, "ONLINE"))
        {
            return 2;
        }

        if (Ci.Equals(state.Kod, "EXCUSED"))
        {
            return 3;
        }

        if (Ci.Equals(state.Kod, "MISSING") || Ci.Equals(state.Kod, "ABSENT"))
        {
            return 4;
        }

        var normalizedName = textNormalizer.Normalize(state.Nazev);
        if (normalizedName.Contains("videokonference", StringComparison.OrdinalIgnoreCase)
            || normalizedName.Contains("online", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (normalizedName.Contains("neomluven", StringComparison.OrdinalIgnoreCase)
            || normalizedName.Contains("nepritomen", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (normalizedName.Contains("omluven", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (normalizedName.Contains("pritomen", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 100;
    }

    private string BuildDisplayNameFromOsoba(OsobaEntity? osoba)
    {
        if (osoba is null)
        {
            return "-";
        }

        var displayName = personIdentityMatcher.BuildPersonName(osoba.Titul, osoba.Jmeno, osoba.Prijmeni);
        return string.IsNullOrWhiteSpace(displayName)
            ? $"Uživatel #{osoba.Id}"
            : displayName;
    }
}
