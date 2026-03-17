using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Modules.Export.Queries;

public sealed class ExportRoleProjectionBuilder(
    PmTrackerDbContext dbContext,
    IPersonIdentityMatcher personIdentityMatcher) : IExportRoleProjectionBuilder
{
    private sealed record ProjectRoleExportRow(int OsobaId, string Osoba, string RoleKod, string RoleNazev);
    private sealed record SubsystemRoleExportRow(int OsobaId, string Osoba, string RoleNazev, string SubsystemNazev);

    public List<PdfRoleAssignmentViewModel> BuildProjectRoleRows(int projectId)
    {
        var projectRoles = BuildActiveProjectRoleAssignments(projectId)
            .Select(item => new
            {
                item.Osoba,
                KindOrder = 0,
                item.RoleNazev,
                SubsystemNazev = (string?)null,
                ViewModel = new PdfRoleAssignmentViewModel
                {
                    Osoba = item.Osoba,
                    TypRole = "Projektová",
                    Role = item.RoleNazev,
                    Subsystem = null
                }
            });

        var subsystemRoles = BuildActiveProjectSubsystemRoleAssignments(projectId)
            .Select(item => new
            {
                item.Osoba,
                KindOrder = 1,
                item.RoleNazev,
                SubsystemNazev = (string?)item.SubsystemNazev,
                ViewModel = new PdfRoleAssignmentViewModel
                {
                    Osoba = item.Osoba,
                    TypRole = "Subsystémová",
                    Role = item.RoleNazev,
                    Subsystem = item.SubsystemNazev
                }
            });

        return projectRoles
            .Concat(subsystemRoles)
            .OrderBy(item => item.Osoba, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.KindOrder)
            .ThenBy(item => item.SubsystemNazev ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.RoleNazev, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => item.ViewModel)
            .ToList();
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
                    role?.Kod ?? "-",
                    role?.Nazev ?? "-");
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
        var subsystems = dbContext.Subsystemy.AsNoTracking().ToDictionary(x => x.Id);
        var roleById = dbContext.CiselnikRoliSubsystemu.AsNoTracking().ToDictionary(x => x.Id);
        var projectSubsystemById = projectSubsystems.ToDictionary(x => x.Id);

        return assignments
            .Select(assignment =>
            {
                var person = people.GetValueOrDefault(assignment.OsobaId);
                var projectSubsystem = projectSubsystemById.GetValueOrDefault(assignment.ProjektSubsystemId);
                var subsystem = projectSubsystem is null ? null : subsystems.GetValueOrDefault(projectSubsystem.SubsystemId);
                var role = roleById.GetValueOrDefault(assignment.RoleSubsystemuId);
                return new SubsystemRoleExportRow(
                    assignment.OsobaId,
                    BuildDisplayNameFromOsoba(person),
                    role?.Nazev ?? "-",
                    subsystem?.Nazev ?? "-");
            })
            .ToList();
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
