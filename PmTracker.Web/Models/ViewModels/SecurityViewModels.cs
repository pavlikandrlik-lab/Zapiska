using System.Collections.Generic;
using System.Linq;

namespace PmTracker.Web.Models.ViewModels;

public static class PermissionKeys
{
    public const string ProjectsCreate = "projects.create";
    public const string ProjectsEdit = "projects.edit";
    public const string ProjectsDelete = "projects.delete";
    public const string RecordsEdit = "records.edit";
    public const string RecordsScheduleAdd = "records.schedule.add";
    public const string RecordsScheduleEdit = "records.schedule.edit";
    public const string RecordsCommentSubsystemLead = "records.comment.subsystemlead";
    public const string MeetingsCreate = "meetings.create";
    public const string MeetingsEdit = "meetings.edit";
    public const string TeamManage = "team.manage";
    public const string PeopleManage = "people.manage";
    public const string CiselnikyEdit = "ciselniky.edit";
    public const string SettingsView = "settings.view";
    public const string SettingsManage = "settings.manage";

    private static readonly PermissionKeyDefinition[] Definitions =
    [
        new(ProjectsCreate, "Vytvářet projekty", "PROJECTS", "PROJECT", "Zakládání nových projektů."),
        new(ProjectsEdit, "Upravovat projekty", "PROJECTS", "PROJECT", "Úpravy metadat a konfigurace projektu."),
        new(ProjectsDelete, "Mazat projekty", "PROJECTS", "PROJECT", "Soft-delete projektů."),
        new(RecordsEdit, "Upravovat projektové záznamy", "RECORDS", "PROJECT", "Plná editace záznamů v projektu."),
        new(RecordsScheduleAdd, "Doplňovat harmonogram úkolu", "RECORDS", "PROJECT", "Doplnění harmonogramu: trvání pouze do dosud nulových hodnot, zpoždění bez omezení."),
        new(RecordsScheduleEdit, "Upravovat harmonogram úkolu", "RECORDS", "PROJECT", "Plná editace harmonogramu úkolu včetně dat a trvání."),
        new(RecordsCommentSubsystemLead, "Přidávat vyjádření jako vedoucí subsystému", "RECORDS", "PROJECT", "Komentáře vedoucího pouze k záznamům jeho subsystému."),
        new(MeetingsCreate, "Zakládat jednání", "MEETINGS", "PROJECT", "Tvorba a úprava metadat jednání."),
        new(MeetingsEdit, "Upravovat jednání", "MEETINGS", "PROJECT", "Změna stavu jednání, účasti a zápisu."),
        new(TeamManage, "Spravovat tým projektu", "PROJECTS", "PROJECT", "Přidávání a odstraňování členů týmu."),
        new(PeopleManage, "Spravovat osoby", "MASTER", "GLOBAL", "Ruční/AD správa osob."),
        new(CiselnikyEdit, "Editovat číselníky", "MASTER", "GLOBAL", "Správa číselníků a referenčních dat."),
        new(SettingsView, "Zobrazit nastavení", "SETTINGS", "GLOBAL", "Read-only přístup do modulu nastavení."),
        new(SettingsManage, "Spravovat nastavení", "SETTINGS", "GLOBAL", "Správa rolí, akcí a mapování oprávnění.")
    ];

    private static readonly HashSet<string> SupportedKeys = new(
        Definitions.Select(x => x.Key),
        StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<LookupOptionViewModel> BuildLookupOptions()
    {
        return Definitions
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Key,
                Label = $"{x.Key} - {x.Nazev}"
            })
            .ToList();
    }

    public static IReadOnlyList<PermissionCatalogEntryViewModel> BuildCatalog()
    {
        return Definitions
            .Select(x => new PermissionCatalogEntryViewModel
            {
                Key = x.Key,
                Nazev = x.Nazev,
                CategoryKod = x.CategoryKod,
                ScopeLevel = x.ScopeLevel,
                Popis = x.Popis
            })
            .ToList();
    }

    public static bool IsSupported(string? key)
    {
        return !string.IsNullOrWhiteSpace(key) && SupportedKeys.Contains(key.Trim());
    }

    private sealed record PermissionKeyDefinition(string Key, string Nazev, string CategoryKod, string ScopeLevel, string Popis);
}

public sealed class PermissionCatalogEntryViewModel
{
    public required string Key { get; init; }
    public required string Nazev { get; init; }
    public required string CategoryKod { get; init; }
    public required string ScopeLevel { get; init; }
    public required string Popis { get; init; }
}

public sealed class CurrentUserContextViewModel
{
    public int OsobaId { get; init; }
    public required string Jmeno { get; init; }
    public required string Prijmeni { get; init; }
    public required string DisplayName { get; init; }
    public required string Email { get; init; }
    public string? OrganizacniCelekKod { get; init; }
    public required string OrganizacniCelek { get; init; }
    public bool IsSuperAdmin { get; init; }
    public required IReadOnlyList<string> RoleKody { get; init; }
    public required IReadOnlyList<PermissionGrantViewModel> PermissionGrants { get; init; }

    public bool HasPermission(string permissionKey, int? projektId = null)
    {
        if (IsSuperAdmin)
        {
            return true;
        }

        var grants = PermissionGrants
            .Where(g => g.IsAllowed && string.Equals(g.PermissionKey, permissionKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (grants.Count == 0)
        {
            return false;
        }

        if (projektId is null)
        {
            return grants.Any(g =>
                string.Equals(g.ScopeLevel, "GLOBAL", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(g.ScopeMode, "ALL", StringComparison.OrdinalIgnoreCase));
        }

        return grants.Any(g =>
            string.Equals(g.ScopeLevel, "GLOBAL", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(g.ScopeMode, "ALL", StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(g.ScopeMode, "INCLUDE", StringComparison.OrdinalIgnoreCase) && g.ProjectIds.Contains(projektId.Value)));
    }
}

public sealed class PermissionGrantViewModel
{
    public required string PermissionKey { get; init; }
    public required string ScopeLevel { get; init; } // GLOBAL|PROJECT
    public required string ScopeMode { get; init; } // ALL|INCLUDE
    public bool IsAllowed { get; init; }
    public required IReadOnlyList<int> ProjectIds { get; init; }
}
