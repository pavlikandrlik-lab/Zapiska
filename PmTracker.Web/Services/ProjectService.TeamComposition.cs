using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services;

public sealed partial class ProjectService
{
    private const int ProjectMemberSearchResultLimit = 15;
    private const int ProjectMemberSearchPrefilterLimit = 40;

    private sealed record ActiveProjectMembershipRow(
        int OsobaId,
        string Osoba,
        string? Email,
        string? Organizace,
        string? OrganizacniCelek,
        bool HasProjectRole,
        bool HasNonHostProjectRole,
        bool HasHostRole,
        bool HasSubsystemRole,
        IReadOnlyList<string> AktivniRole);

    public async Task SaveTeamMemberAsync(SaveTeamMemberCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        if (!command.OsobaId.HasValue || command.OsobaId.Value <= 0)
        {
            throw new InvalidOperationException("Vyberte osobu z nabídky.");
        }

        var osobaId = command.OsobaId.Value;
        var roleId = await ResolveProjectRoleIdAsync(command.Role, ct)
            ?? throw new InvalidOperationException($"Role '{command.Role}' nebyla nalezena.");

        var existing = await dbContext.ObsazeniProjektu
            .FirstOrDefaultAsync(x => x.ProjektId == command.ProjektId && x.OsobaId == osobaId, ct);
        if (existing is null)
        {
            dbContext.ObsazeniProjektu.Add(new ObsazeniProjektuEntity
            {
                ProjektId = command.ProjektId,
                OsobaId = osobaId,
                RoleId = roleId
            });
        }
        else
        {
            existing.RoleId = roleId;
        }

        await dbContext.SaveChangesAsync(ct);
        await WriteAuditAsync(currentUser.OsobaId, "obsazeni_projektu", $"{command.ProjektId}:{osobaId}", "upsert", null, JsonSerializer.Serialize(command), ct);
    }

    public async Task RemoveTeamMemberAsync(RemoveTeamMemberCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var rows = await dbContext.ObsazeniProjektu
            .Where(x => x.ProjektId == command.ProjektId && x.OsobaId == command.OsobaId)
            .ToListAsync(ct);
        if (rows.Count == 0)
        {
            return;
        }

        dbContext.ObsazeniProjektu.RemoveRange(rows);
        await dbContext.SaveChangesAsync(ct);
        await WriteAuditAsync(currentUser.OsobaId, "obsazeni_projektu", $"{command.ProjektId}:{command.OsobaId}", "delete", null, null, ct);
    }

    private async Task<List<ActiveProjectMembershipRow>> BuildActiveProjectMembershipRowsAsync(int projectId, CancellationToken ct)
    {
        var activeProjectRoleAssignments = await BuildActiveProjectRoleAssignmentsAsync(projectId, ct);
        var activeSubsystemRoleAssignments = await BuildActiveProjectSubsystemRoleAssignmentsAsync(projectId, ct);

        var roleFragments = activeProjectRoleAssignments
            .Select(item => new
            {
                item.OsobaId,
                item.Osoba,
                item.Email,
                item.Organizace,
                item.OrganizacniCelek,
                HasProjectRole = true,
                HasNonHostProjectRole = !Ci.Equals(item.RoleKod, ProjectRoleCodes.Host),
                HasHostRole = Ci.Equals(item.RoleKod, ProjectRoleCodes.Host),
                HasSubsystemRole = false,
                RoleLabel = item.RoleNazev
            })
            .Concat(activeSubsystemRoleAssignments.Select(item => new
            {
                item.OsobaId,
                item.Osoba,
                item.Email,
                Organizace = (string?)null,
                OrganizacniCelek = (string?)null,
                HasProjectRole = false,
                HasNonHostProjectRole = false,
                HasHostRole = false,
                HasSubsystemRole = true,
                RoleLabel = BuildSubsystemRoleLabel(item.RoleNazev, item.SubsystemKod, item.SubsystemNazev)
            }))
            .ToList();

        if (roleFragments.Count == 0)
        {
            return [];
        }

        var personIds = roleFragments.Select(item => item.OsobaId).Distinct().ToList();
        var people = await dbContext.Osoby.AsNoTracking()
            .Where(x => personIds.Contains(x.Id))
            .ToListAsync(ct);
        var peopleById = people.ToDictionary(x => x.Id);
        var organizationsById = await LoadOrganizationsByPeopleAsync(people, ct);
        var orgUnitsById = await LoadOrgUnitsByPeopleAsync(people, ct);

        return roleFragments
            .GroupBy(item => item.OsobaId)
            .Select(group =>
            {
                var first = group.First();
                var person = peopleById.GetValueOrDefault(group.Key);
                return new ActiveProjectMembershipRow(
                    group.Key,
                    first.Osoba,
                    first.Email,
                    first.Organizace ?? (person is null ? null : organizationsById.GetValueOrDefault(person.OrganizaceId)?.Nazev),
                    first.OrganizacniCelek ?? (person?.OrganizacniCelekId is int orgUnitId ? orgUnitsById.GetValueOrDefault(orgUnitId)?.Nazev : null),
                    group.Any(item => item.HasProjectRole),
                    group.Any(item => item.HasNonHostProjectRole),
                    group.Any(item => item.HasHostRole),
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

    private async Task<List<ProjectRoleGridRowViewModel>> BuildUnifiedActiveProjectRoleRowsAsync(int projectId, CancellationToken ct)
    {
        var activeProjectRoleAssignments = await BuildActiveProjectRoleAssignmentsAsync(projectId, ct);
        var activeSubsystemRoleAssignments = await BuildActiveProjectSubsystemRoleAssignmentsAsync(projectId, ct);

        return activeProjectRoleAssignments
            .Select(item => new ProjectRoleGridRowViewModel
            {
                AssignmentId = item.ProjektRoleId,
                AssignmentKind = "PROJECT",
                OsobaId = item.OsobaId,
                Osoba = item.Osoba,
                Email = item.Email,
                RoleKod = item.RoleKod,
                RoleNazev = item.RoleNazev,
                RoleTypeLabel = "Projektová",
                SubsystemKod = null,
                SubsystemNazev = null,
                Organizace = item.Organizace,
                OrganizacniCelek = item.OrganizacniCelek,
                DatumPrirazeni = item.DatumPrirazeni
            })
            .Concat(activeSubsystemRoleAssignments.Select(item =>
                new ProjectRoleGridRowViewModel
                {
                    AssignmentId = item.ProjektSubsystemRoleId,
                    AssignmentKind = "SUBSYSTEM",
                    OsobaId = item.OsobaId,
                    Osoba = item.Osoba,
                    Email = item.Email,
                    RoleKod = item.RoleKod,
                    RoleNazev = item.RoleNazev,
                    RoleTypeLabel = "Subsystémová",
                    SubsystemKod = item.SubsystemKod,
                    SubsystemNazev = item.SubsystemNazev,
                    Organizace = item.Organizace,
                    OrganizacniCelek = item.OrganizacniCelek,
                    DatumPrirazeni = item.DatumPrirazeni
                }))
            .OrderBy(item => item.Osoba, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => Ci.Equals(item.AssignmentKind, "PROJECT") ? 0 : 1)
            .ThenBy(item => item.SubsystemNazev ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.RoleNazev, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private async Task<List<ProjectRoleHistoryGridRowViewModel>> BuildUnifiedProjectRoleHistoryRowsAsync(int projectId, CancellationToken ct)
    {
        var projectRoleHistory = await BuildProjectRoleHistoryAsync(projectId, ct);
        var projectSubsystemRoleHistory = await BuildProjectSubsystemRoleHistoryAsync(projectId, ct);

        return projectRoleHistory
            .Select(item => new ProjectRoleHistoryGridRowViewModel
            {
                AssignmentId = item.ProjektRoleId,
                AssignmentKind = "PROJECT",
                OsobaId = item.OsobaId,
                Osoba = item.Osoba,
                RoleKod = item.RoleKod,
                RoleNazev = item.RoleNazev,
                RoleTypeLabel = "Projektová",
                SubsystemKod = null,
                SubsystemNazev = null,
                DatumPrirazeni = item.DatumPrirazeni,
                DatumOdebrani = item.DatumOdebrani
            })
            .Concat(projectSubsystemRoleHistory.Select(item => new ProjectRoleHistoryGridRowViewModel
            {
                AssignmentId = item.ProjektSubsystemRoleId,
                AssignmentKind = "SUBSYSTEM",
                OsobaId = item.OsobaId,
                Osoba = item.Osoba,
                RoleKod = item.RoleKod,
                RoleNazev = item.RoleNazev,
                RoleTypeLabel = "Subsystémová",
                SubsystemKod = item.SubsystemKod,
                SubsystemNazev = item.SubsystemNazev,
                DatumPrirazeni = item.DatumPrirazeni,
                DatumOdebrani = item.DatumOdebrani
            }))
            .OrderByDescending(item => item.DatumOdebrani)
            .ThenBy(item => item.Osoba, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => Ci.Equals(item.AssignmentKind, "PROJECT") ? 0 : 1)
            .ThenBy(item => item.SubsystemNazev ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.RoleNazev, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private async Task<List<ProjectRoleAssignmentViewModel>> BuildActiveProjectRoleAssignmentsAsync(int projectId, CancellationToken ct)
    {
        var assignments = await dbContext.ObsazeniProjektu.AsNoTracking()
            .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
            .ToListAsync(ct);
        var roleIds = assignments.Select(x => x.RoleId).Distinct().ToArray();
        var roles = roleIds.Length == 0
            ? new Dictionary<int, CiselnikRoliProjektuEntity>()
            : await dbContext.CiselnikRoliProjektu.AsNoTracking()
                .Where(x => roleIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);
        var people = await LoadPeopleByIdsAsync(assignments.Select(x => x.OsobaId).Distinct(), ct);
        var organizations = await LoadOrganizationsByPeopleAsync(people.Values, ct);
        var orgUnits = await LoadOrgUnitsByPeopleAsync(people.Values, ct);

        return assignments
            .Select(assignment =>
            {
                var person = people.GetValueOrDefault(assignment.OsobaId);
                var role = roles.GetValueOrDefault(assignment.RoleId);
                return new ProjectRoleAssignmentViewModel
                {
                    ProjektRoleId = assignment.Id,
                    OsobaId = assignment.OsobaId,
                    Osoba = BuildDisplayNameFromOsoba(person),
                    Email = person?.Email?.Trim(),
                    RoleKod = role?.Kod ?? "-",
                    RoleNazev = role?.Nazev ?? "-",
                    Organizace = person is null ? null : organizations.GetValueOrDefault(person.OrganizaceId)?.Nazev,
                    OrganizacniCelek = person?.OrganizacniCelekId is int cel ? orgUnits.GetValueOrDefault(cel)?.Nazev : null,
                    DatumPrirazeni = assignment.DatumPrirazeni
                };
            })
            .OrderBy(x => x.RoleNazev, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.Osoba, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private async Task<List<ProjectRoleHistoryItemViewModel>> BuildProjectRoleHistoryAsync(int projectId, CancellationToken ct)
    {
        var assignments = await dbContext.ObsazeniProjektu.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .ToListAsync(ct);
        var roleIds = assignments.Select(x => x.RoleId).Distinct().ToArray();
        var roles = roleIds.Length == 0
            ? new Dictionary<int, CiselnikRoliProjektuEntity>()
            : await dbContext.CiselnikRoliProjektu.AsNoTracking()
                .Where(x => roleIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);
        var people = await LoadPeopleByIdsAsync(assignments.Select(x => x.OsobaId).Distinct(), ct);

        return assignments
            .Where(x => x.DatumOdebrani.HasValue)
            .Select(assignment => new ProjectRoleHistoryItemViewModel
            {
                ProjektRoleId = assignment.Id,
                OsobaId = assignment.OsobaId,
                Osoba = BuildDisplayNameFromOsoba(people.GetValueOrDefault(assignment.OsobaId)),
                RoleKod = roles.GetValueOrDefault(assignment.RoleId)?.Kod ?? "-",
                RoleNazev = roles.GetValueOrDefault(assignment.RoleId)?.Nazev ?? "-",
                DatumPrirazeni = assignment.DatumPrirazeni,
                DatumOdebrani = assignment.DatumOdebrani
            })
            .OrderByDescending(x => x.DatumOdebrani)
            .ThenBy(x => x.RoleNazev, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private async Task<List<ProjectSubsystemViewModel>> BuildActiveProjectSubsystemsAsync(int projectId, CancellationToken ct)
    {
        var items = await ProjectSubsystemOrderingQuery.LoadActiveRowsAsync(dbContext, projectId, ct: ct);

        return items
            .Select(item => new ProjectSubsystemViewModel
            {
                ProjektSubsystemId = item.ProjektSubsystemId,
                SubsystemId = item.SubsystemId,
                Poradi = item.Poradi,
                Kod = item.Kod,
                Nazev = item.Nazev,
                DatumPrirazeni = item.DatumPrirazeni
            })
            .ToList();
    }

    private async Task<List<ProjectTeamSubsystemRowViewModel>> BuildProjectTeamSubsystemRowsAsync(int projectId, CancellationToken ct)
    {
        var subsystems = await BuildActiveProjectSubsystemsAsync(projectId, ct);
        return subsystems
            .Select((item, index) => new ProjectTeamSubsystemRowViewModel
            {
                ProjektSubsystemId = item.ProjektSubsystemId,
                SubsystemId = item.SubsystemId,
                Poradi = item.Poradi,
                Kod = item.Kod,
                Nazev = item.Nazev,
                DatumPrirazeni = item.DatumPrirazeni,
                CanMoveUp = index > 0,
                CanMoveDown = index < subsystems.Count - 1
            })
            .ToList();
    }

    private async Task<List<ProjectSubsystemRoleAssignmentViewModel>> BuildActiveProjectSubsystemRoleAssignmentsAsync(int projectId, CancellationToken ct)
    {
        var projectSubsystems = await dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
            .ToListAsync(ct);
        var projectSubsystemIds = projectSubsystems.Select(x => x.Id).ToHashSet();
        var assignments = await dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .Where(x => projectSubsystemIds.Contains(x.ProjektSubsystemId) && !x.DatumOdebrani.HasValue)
            .ToListAsync(ct);
        var people = await LoadPeopleByIdsAsync(assignments.Select(x => x.OsobaId).Distinct(), ct);
        var organizations = await LoadOrganizationsByPeopleAsync(people.Values, ct);
        var orgUnits = await LoadOrgUnitsByPeopleAsync(people.Values, ct);
        var subsystemIds = projectSubsystems.Select(x => x.SubsystemId).Distinct().ToArray();
        var subsystems = subsystemIds.Length == 0
            ? new Dictionary<int, SubsystemEntity>()
            : await dbContext.Subsystemy.AsNoTracking()
                .Where(x => subsystemIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);
        var roleIds = assignments.Select(x => x.RoleSubsystemuId).Distinct().ToArray();
        var roleById = roleIds.Length == 0
            ? new Dictionary<int, CiselnikRoleSubsystemuEntity>()
            : await dbContext.CiselnikRoliSubsystemu.AsNoTracking()
                .Where(x => roleIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);
        var psById = projectSubsystems.ToDictionary(x => x.Id);

        return assignments
            .Select(assignment =>
            {
                var person = people.GetValueOrDefault(assignment.OsobaId);
                var projektSubsystem = psById.GetValueOrDefault(assignment.ProjektSubsystemId);
                var subsystem = projektSubsystem is null ? null : subsystems.GetValueOrDefault(projektSubsystem.SubsystemId);
                var role = roleById.GetValueOrDefault(assignment.RoleSubsystemuId);
                return new ProjectSubsystemRoleAssignmentViewModel
                {
                    ProjektSubsystemRoleId = assignment.Id,
                    ProjektSubsystemId = assignment.ProjektSubsystemId,
                    OsobaId = assignment.OsobaId,
                    Osoba = BuildDisplayNameFromOsoba(person),
                    Email = person?.Email?.Trim(),
                    SubsystemKod = subsystem?.Kod ?? "-",
                    SubsystemNazev = subsystem?.Nazev ?? "-",
                    RoleKod = role?.Kod ?? "-",
                    RoleNazev = role?.Nazev ?? "-",
                    Organizace = person is null ? null : organizations.GetValueOrDefault(person.OrganizaceId)?.Nazev,
                    OrganizacniCelek = person?.OrganizacniCelekId is int cel ? orgUnits.GetValueOrDefault(cel)?.Nazev : null,
                    DatumPrirazeni = assignment.DatumPrirazeni
                };
            })
            .OrderBy(x => x.SubsystemNazev, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.RoleNazev, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.Osoba, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private async Task<List<ProjectSubsystemRoleHistoryItemViewModel>> BuildProjectSubsystemRoleHistoryAsync(int projectId, CancellationToken ct)
    {
        var projectSubsystems = await dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .ToListAsync(ct);
        var projectSubsystemIds = projectSubsystems.Select(x => x.Id).ToHashSet();
        var assignments = await dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .Where(x => projectSubsystemIds.Contains(x.ProjektSubsystemId) && x.DatumOdebrani.HasValue)
            .ToListAsync(ct);
        var people = await LoadPeopleByIdsAsync(assignments.Select(x => x.OsobaId).Distinct(), ct);
        var subsystemIds = projectSubsystems.Select(x => x.SubsystemId).Distinct().ToArray();
        var subsystems = subsystemIds.Length == 0
            ? new Dictionary<int, SubsystemEntity>()
            : await dbContext.Subsystemy.AsNoTracking()
                .Where(x => subsystemIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);
        var roleIds = assignments.Select(x => x.RoleSubsystemuId).Distinct().ToArray();
        var roleById = roleIds.Length == 0
            ? new Dictionary<int, CiselnikRoleSubsystemuEntity>()
            : await dbContext.CiselnikRoliSubsystemu.AsNoTracking()
                .Where(x => roleIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);
        var psById = projectSubsystems.ToDictionary(x => x.Id);

        return assignments
            .Select(assignment =>
            {
                var projektSubsystem = psById.GetValueOrDefault(assignment.ProjektSubsystemId);
                var subsystem = projektSubsystem is null ? null : subsystems.GetValueOrDefault(projektSubsystem.SubsystemId);
                return new ProjectSubsystemRoleHistoryItemViewModel
                {
                    ProjektSubsystemRoleId = assignment.Id,
                    OsobaId = assignment.OsobaId,
                    Osoba = BuildDisplayNameFromOsoba(people.GetValueOrDefault(assignment.OsobaId)),
                    SubsystemKod = subsystem?.Kod ?? "-",
                    SubsystemNazev = subsystem?.Nazev ?? "-",
                    RoleKod = roleById.GetValueOrDefault(assignment.RoleSubsystemuId)?.Kod ?? "-",
                    RoleNazev = roleById.GetValueOrDefault(assignment.RoleSubsystemuId)?.Nazev ?? "-",
                    DatumPrirazeni = assignment.DatumPrirazeni,
                    DatumOdebrani = assignment.DatumOdebrani
                };
            })
            .OrderByDescending(x => x.DatumOdebrani)
            .ToList();
    }

    public async Task<IReadOnlyList<PersonPickerEntryViewModel>> SearchProjectMemberCandidatesAsync(string query, CancellationToken ct = default)
    {
        var trimmedQuery = query?.Trim() ?? string.Empty;
        if (trimmedQuery.Length < 2)
        {
            return [];
        }

        var searchTerms = trimmedQuery
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
        if (searchTerms.Length == 0)
        {
            return [];
        }

        var normalizedSearch = textNormalizer.Normalize(trimmedQuery);
        IQueryable<OsobaEntity> peopleQuery = dbContext.Osoby.AsNoTracking();
        foreach (var term in searchTerms)
        {
            var likePattern = $"%{term}%";
            peopleQuery = peopleQuery.Where(x =>
                EF.Functions.Like(x.Jmeno, likePattern)
                || EF.Functions.Like(x.Prijmeni, likePattern)
                || EF.Functions.Like((x.Email ?? string.Empty), likePattern)
                || EF.Functions.Like((x.AdLogin ?? string.Empty), likePattern));
        }

        var candidates = await peopleQuery
            .OrderBy(x => x.Prijmeni)
            .ThenBy(x => x.Jmeno)
            .Take(ProjectMemberSearchPrefilterLimit)
            .Select(x => new
            {
                x.Id,
                x.Titul,
                x.Jmeno,
                x.Prijmeni,
                x.Email,
                x.AdLogin,
                x.OrganizaceId,
                x.OrganizacniCelekId
            })
            .ToListAsync(ct);
        if (candidates.Count == 0)
        {
            return [];
        }

        var organizationIds = candidates
            .Select(x => x.OrganizaceId)
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        var orgUnitIds = candidates
            .Where(x => x.OrganizacniCelekId.HasValue)
            .Select(x => x.OrganizacniCelekId!.Value)
            .Distinct()
            .ToArray();

        var organizationsById = organizationIds.Length == 0
            ? new Dictionary<int, string>()
            : await dbContext.CiselnikOrganizace.AsNoTracking()
                .Where(x => organizationIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Nazev, ct);
        var orgUnitsById = orgUnitIds.Length == 0
            ? new Dictionary<int, string>()
            : await dbContext.CiselnikOrganizacniCelky.AsNoTracking()
                .Where(x => orgUnitIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Nazev, ct);

        return candidates
            .Select(candidate =>
            {
                var organization = organizationsById.GetValueOrDefault(candidate.OrganizaceId);
                var organizationalUnit = candidate.OrganizacniCelekId.HasValue
                    ? orgUnitsById.GetValueOrDefault(candidate.OrganizacniCelekId.Value)
                    : null;
                return new
                {
                    Score = ScoreProjectMemberCandidate(
                        candidate.Titul,
                        candidate.Jmeno,
                        candidate.Prijmeni,
                        candidate.Email,
                        candidate.AdLogin,
                        organization,
                        organizationalUnit,
                        normalizedSearch),
                    Entry = new PersonPickerEntryViewModel
                    {
                        Id = candidate.Id,
                        Label = BuildDisplayName(candidate.Titul, candidate.Jmeno, candidate.Prijmeni, candidate.Id),
                        Email = textNormalizer.NormalizeEmail(candidate.Email),
                        Organizace = organization,
                        OrganizacniCelek = organizationalUnit
                    }
                };
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Entry.Label, StringComparer.CurrentCultureIgnoreCase)
            .Take(ProjectMemberSearchResultLimit)
            .Select(x => x.Entry)
            .ToList();
    }

    public async Task<ProjectTeamModalOptionsViewModel> BuildProjectTeamModalOptionsAsync(int id, CancellationToken ct = default)
    {
        return new ProjectTeamModalOptionsViewModel
        {
            RoleProjektu = await BuildProjectRoleOptionsAsync(ct),
            RoleSubsystemu = await BuildSubsystemRoleOptionsAsync(ct),
            DostupneProjektoveSubsystemy = await BuildProjectSubsystemOptionsAsync(id, ct),
            DostupneSubsystemy = await BuildAvailableSubsystemOptionsAsync(ct)
        };
    }

    private async Task<List<ProjectMemberCandidateViewModel>> BuildProjectMemberCandidatesAsync(CancellationToken ct)
    {
        var organizations = (await dbContext.CiselnikOrganizace.AsNoTracking().ToListAsync(ct)).ToDictionary(x => x.Id);
        var orgUnits = (await dbContext.CiselnikOrganizacniCelky.AsNoTracking().ToListAsync(ct)).ToDictionary(x => x.Id);
        var people = await dbContext.Osoby.AsNoTracking()
            .OrderBy(x => x.Prijmeni)
            .ThenBy(x => x.Jmeno)
            .ToListAsync(ct);

        return people
            .Select(person => new ProjectMemberCandidateViewModel
            {
                OsobaId = person.Id,
                Osoba = BuildDisplayName(person.Titul, person.Jmeno, person.Prijmeni, person.Id),
                Email = person.Email?.Trim(),
                Organizace = organizations.GetValueOrDefault(person.OrganizaceId)?.Nazev,
                OrganizacniCelek = person.OrganizacniCelekId.HasValue ? orgUnits.GetValueOrDefault(person.OrganizacniCelekId.Value)?.Nazev : null
            })
            .ToList();
    }

    private async Task<List<SpolupracovnikOptionViewModel>> BuildRecordOwnerCandidatesAsync(int projectId, int? selectedOwnerId, CancellationToken ct)
    {
        var rows = (await BuildActiveProjectMembershipRowsAsync(projectId, ct))
            .Select(item => new SpolupracovnikOptionViewModel
            {
                OsobaId = item.OsobaId,
                Osoba = item.Osoba,
                Email = item.Email,
                Organizace = item.Organizace,
                OrganizacniCelek = item.OrganizacniCelek
            })
            .ToList();

        if (selectedOwnerId.HasValue && rows.All(item => item.OsobaId != selectedOwnerId.Value))
        {
            var selectedOwner = await dbContext.Osoby.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == selectedOwnerId.Value, ct);
            if (selectedOwner is not null)
            {
                var organizations = await LoadOrganizationsByPeopleAsync([selectedOwner], ct);
                var orgUnits = await LoadOrgUnitsByPeopleAsync([selectedOwner], ct);
                rows.Add(new SpolupracovnikOptionViewModel
                {
                    OsobaId = selectedOwner.Id,
                    Osoba = BuildDisplayName(selectedOwner.Titul, selectedOwner.Jmeno, selectedOwner.Prijmeni, selectedOwner.Id),
                    Email = selectedOwner.Email?.Trim(),
                    Organizace = organizations.GetValueOrDefault(selectedOwner.OrganizaceId)?.Nazev,
                    OrganizacniCelek = selectedOwner.OrganizacniCelekId.HasValue
                        ? orgUnits.GetValueOrDefault(selectedOwner.OrganizacniCelekId.Value)?.Nazev
                        : null
                });
            }
        }

        return rows
            .DistinctBy(item => item.OsobaId)
            .OrderBy(item => item.Osoba, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private int ScoreProjectMemberCandidate(
        string? titul,
        string jmeno,
        string prijmeni,
        string? email,
        string? adLogin,
        string? organizace,
        string? organizacniCelek,
        string normalizedSearch)
    {
        if (string.IsNullOrWhiteSpace(normalizedSearch))
        {
            return 0;
        }

        var score = 0;

        if (personIdentityMatcher.NameEquals(titul, jmeno, prijmeni, normalizedSearch))
        {
            score += 1000;
        }
        else if (personIdentityMatcher.NameContains(titul, jmeno, prijmeni, normalizedSearch))
        {
            score += 500;
        }

        if (personIdentityMatcher.EmailEquals(email, normalizedSearch))
        {
            score += 900;
        }
        else if (personIdentityMatcher.EmailContains(email, normalizedSearch))
        {
            score += 450;
        }

        var normalizedLogin = textNormalizer.Normalize(adLogin ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(normalizedLogin))
        {
            if (string.Equals(normalizedLogin, normalizedSearch, StringComparison.OrdinalIgnoreCase))
            {
                score += 850;
            }
            else if (normalizedLogin.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || normalizedSearch.Contains(normalizedLogin, StringComparison.OrdinalIgnoreCase))
            {
                score += 425;
            }
        }

        if (ContainsNormalizedText(organizace, normalizedSearch))
        {
            score += 125;
        }

        if (ContainsNormalizedText(organizacniCelek, normalizedSearch))
        {
            score += 125;
        }

        return score;
    }

    private bool ContainsNormalizedText(string? value, string normalizedSearch)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(normalizedSearch))
        {
            return false;
        }

        var normalizedValue = textNormalizer.Normalize(value);
        return normalizedValue.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
            || normalizedSearch.Contains(normalizedValue, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<ProjectSubsystemOptionViewModel>> BuildProjectSubsystemOptionsAsync(int projectId, CancellationToken ct)
    {
        return (await BuildActiveProjectSubsystemsAsync(projectId, ct))
            .OrderBy(item => item.Kod, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Nazev, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new ProjectSubsystemOptionViewModel
            {
                ProjektSubsystemId = item.ProjektSubsystemId,
                SubsystemId = item.SubsystemId,
                Kod = item.Kod,
                Nazev = item.Nazev,
                Label = string.IsNullOrWhiteSpace(item.Kod) ? item.Nazev : $"{item.Kod} - {item.Nazev}"
            })
            .ToList();
    }

    private Task<List<LookupOptionViewModel>> BuildProjectRoleOptionsAsync(CancellationToken ct)
        => dbContext.CiselnikRoliProjektu.AsNoTracking()
            .OrderBy(x => x.Nazev)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToListAsync(ct);

    private Task<List<LookupOptionViewModel>> BuildSubsystemRoleOptionsAsync(CancellationToken ct)
        => dbContext.CiselnikRoliSubsystemu.AsNoTracking()
            .OrderBy(x => x.Nazev)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToListAsync(ct);

    private Task<List<LookupOptionViewModel>> BuildAvailableSubsystemOptionsAsync(CancellationToken ct)
        => dbContext.Subsystemy.AsNoTracking()
            .OrderBy(x => x.Kod)
            .ThenBy(x => x.Nazev)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = string.IsNullOrWhiteSpace(x.Kod) ? x.Nazev : $"{x.Kod} - {x.Nazev}"
            })
            .ToListAsync(ct);

    private async Task<Dictionary<int, int>> BuildDefaultOwnerOsobaIdsByProjectSubsystemAsync(int projectId, CancellationToken ct)
    {
        var leadRoleId = await dbContext.CiselnikRoliSubsystemu.AsNoTracking()
            .Where(x => x.Kod == SubsystemRoleCodes.Lead)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);
        if (!leadRoleId.HasValue)
        {
            return [];
        }

        var activeProjectSubsystems = await dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
            .ToListAsync(ct);
        var activeProjectSubsystemIds = activeProjectSubsystems.Select(x => x.Id).ToHashSet();
        var assignments = await dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .Where(x => activeProjectSubsystemIds.Contains(x.ProjektSubsystemId)
                && !x.DatumOdebrani.HasValue
                && x.RoleSubsystemuId == leadRoleId.Value)
            .ToListAsync(ct);

        return assignments
            .GroupBy(x => activeProjectSubsystems.First(ps => ps.Id == x.ProjektSubsystemId).SubsystemId)
            .ToDictionary(group => group.Key, group => group.Select(x => x.OsobaId).First());
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

    private Task<int?> ResolveProjectRoleIdAsync(string value, CancellationToken ct)
        => dbContext.CiselnikRoliProjektu
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);
}
