using System.Collections.Generic;
using System.Linq;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Models.ViewModels;

public static class PermissionKeys
{
    public const string PeoplePrefix = "people.";
    public const string CiselnikyPrefix = "ciselniky.";
    public const string SettingsPrefix = "settings.";
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
    public const string DashboardView = "dashboard.view";
    public const string ExportPdf = "export.pdf";
    public const string ExportWord = "export.word";
    public const string CommentsAdd = "comments.add";
    public const string CommentsEditOwn = "comments.edit.own";
    public const string CommentsDeleteOwn = "comments.delete.own";
    public const string SearchReindex = "search.reindex";
    public const string ProjectsReadAll = "projects.read.all";

    private static readonly PermissionKeyDefinition[] Definitions =
    [
        new(ProjectsCreate, "Vytvářet projekty", "PROJECTS", "PROJECT", "Zakládání nových projektů."),
        new(ProjectsEdit, "Upravovat projekty", "PROJECTS", "PROJECT", "Úpravy metadat a konfigurace projektu."),
        new(ProjectsDelete, "Mazat projekty", "PROJECTS", "PROJECT", "Soft-delete projektů."),
        new(RecordsEdit, "Upravovat projektové záznamy", "RECORDS", "PROJECT", "Plná editace záznamů v projektu."),
        new(RecordsScheduleAdd, "Doplňovat harmonogram úkolu", "RECORDS", "PROJECT", "Doplnění harmonogramu: trvání pouze do dosud nulových hodnot, skutečnost bez omezení."),
        new(RecordsScheduleEdit, "Upravovat harmonogram úkolu", "RECORDS", "PROJECT", "Plná editace harmonogramu úkolu včetně dat, trvání a skutečnosti."),
        new(RecordsCommentSubsystemLead, "Přidávat vyjádření jako vedoucí subsystému", "RECORDS", "PROJECT", "Komentáře vedoucího pouze k záznamům jeho subsystému."),
        new(MeetingsCreate, "Zakládat jednání", "MEETINGS", "PROJECT", "Tvorba a úprava metadat jednání."),
        new(MeetingsEdit, "Upravovat jednání", "MEETINGS", "PROJECT", "Změna stavu jednání, účasti a zápisu."),
        new(TeamManage, "Spravovat tým projektu", "PROJECTS", "PROJECT", "Přidávání a odstraňování členů týmu."),
        new(PeopleManage, "Spravovat osoby", "MASTER", "GLOBAL", "Ruční/AD správa osob."),
        new(CiselnikyEdit, "Editovat číselníky", "MASTER", "GLOBAL", "Správa číselníků a referenčních dat."),
        new(SettingsView, "Zobrazit nastavení", "SETTINGS", "GLOBAL", "Read-only přístup do modulu nastavení."),
        new(SettingsManage, "Spravovat nastavení", "SETTINGS", "GLOBAL", "Správa rolí, akcí a mapování oprávnění."),
        new(DashboardView, "Zobrazit projektový dashboard", "PROJECTS", "PROJECT", "Read-only přístup k projektovému dashboardu."),
        new(ExportPdf, "Exportovat projekt do PDF", "PROJECTS", "PROJECT", "Generování PDF exportu projektu."),
        new(ExportWord, "Exportovat projekt do Word", "PROJECTS", "PROJECT", "Generování Word exportu projektu."),
        new(CommentsAdd, "Přidávat komentáře", "RECORDS", "PROJECT", "Vkládání nových komentářů k záznamům."),
        new(CommentsEditOwn, "Upravovat vlastní komentáře", "RECORDS", "PROJECT", "Úprava komentářů, které osoba sama vložila."),
        new(CommentsDeleteOwn, "Mazat vlastní komentáře", "RECORDS", "PROJECT", "Smazání komentářů, které osoba sama vložila."),
        new(SearchReindex, "Spustit reindex vyhledávání", "SETTINGS", "GLOBAL", "Administrátorská akce: full reindex FTS."),
        new(ProjectsReadAll, "Číst všechny projekty", "PROJECTS", "GLOBAL", "Read-only přístup ke všem projektům (management visibility).")
    ];

    private static readonly HashSet<string> SupportedKeys = new(
        Definitions.Select(x => x.Key),
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ProjectReadGrantKeys = new(
    [
        ProjectsEdit,
        ProjectsDelete,
        RecordsEdit,
        RecordsScheduleAdd,
        RecordsScheduleEdit,
        RecordsCommentSubsystemLead,
        MeetingsCreate,
        MeetingsEdit,
        TeamManage,
        DashboardView,
        ExportPdf,
        ExportWord,
        CommentsAdd,
        CommentsEditOwn,
        CommentsDeleteOwn
    ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ProjectWriteKeysBlockedForDeletedProjects = new(
    [
        ProjectsEdit,
        ProjectsDelete,
        RecordsEdit,
        RecordsScheduleAdd,
        RecordsScheduleEdit,
        RecordsCommentSubsystemLead,
        MeetingsCreate,
        MeetingsEdit,
        TeamManage
    ],
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

    public static bool GrantsProjectRead(string? key)
    {
        return !string.IsNullOrWhiteSpace(key) && ProjectReadGrantKeys.Contains(key.Trim());
    }

    public static bool IsBlockedForDeletedProject(string? key)
    {
        return !string.IsNullOrWhiteSpace(key) && ProjectWriteKeysBlockedForDeletedProjects.Contains(key.Trim());
    }

    /// <summary>Všechny definice oprávnění. Používá se pro registraci ASP.NET Core policies.</summary>
    public static IReadOnlyList<PermissionKeyDefinition> AllDefinitions => Definitions;

    public sealed record PermissionKeyDefinition(string Key, string Nazev, string CategoryKod, string ScopeLevel, string Popis);
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
    public required IReadOnlyList<int> VisibleProjectIds { get; init; }
    public required IReadOnlyList<int> DeletedProjectIds { get; init; }
    /// <summary>
    /// Kanonická in-memory autorizační projekce pro tento request (Fáze D).
    /// Null pouze pro unauthenticated requests; vždy vyplněno pro autentizované uživatele.
    /// </summary>
    public AuthorizationSnapshot? Authorization { get; init; }

    public bool IsProjectReadOnly(int projektId)
    {
        return projektId > 0 && DeletedProjectIds.Contains(projektId);
    }

    public bool CanAccessProject(int projektId)
    {
        if (IsSuperAdmin) return true;
        if (projektId <= 0) return false;
        if (VisibleProjectIds.Contains(projektId)) return true;

        var authz = Authorization;
        if (authz is null) return false;

        // Any project-read key granted globally covers all projects.
        if (authz.GlobalPermissions.Any(PermissionKeys.GrantsProjectRead)) return true;

        // Project-read key granted specifically for this project.
        if (authz.PerProjectPermissions.TryGetValue(projektId, out var projectKeys)
            && projectKeys.Any(PermissionKeys.GrantsProjectRead))
        {
            return true;
        }

        return false;
    }

    public bool HasPermission(string permissionKey, int? projektId = null)
    {
        if (projektId.HasValue && IsProjectReadOnly(projektId.Value) && PermissionKeys.IsBlockedForDeletedProject(permissionKey))
        {
            return false;
        }

        var authz = Authorization ?? throw new InvalidOperationException(
            "AuthorizationSnapshot musí být vyplněn pro tento request.");

        return authz.HasPermission(permissionKey, projektId);
    }

    public bool HasPermissionPrefix(string permissionPrefix)
    {
        if (IsSuperAdmin) return true;
        if (string.IsNullOrWhiteSpace(permissionPrefix)) return false;

        var authz = Authorization ?? throw new InvalidOperationException(
            "AuthorizationSnapshot musí být vyplněn pro tento request.");

        var prefix = permissionPrefix.Trim();
        if (!prefix.EndsWith(".", StringComparison.Ordinal))
        {
            prefix += ".";
        }

        bool MatchesPrefix(string key) =>
            key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

        if (authz.GlobalPermissions.Any(MatchesPrefix)) return true;
        if (authz.PerProjectPermissions.Values.Any(set => set.Any(MatchesPrefix))) return true;
        if (authz.PerSubsystemPermissions.Values.Any(set => set.Any(MatchesPrefix))) return true;
        return false;
    }
}

public sealed class PermissionGrantViewModel
{
    public required string PermissionKey { get; init; }
    public required string ScopeLevel { get; init; } // GLOBAL|PROJECT
    public required string ScopeMode { get; init; } // ALL|INCLUDE
    public bool IsAllowed { get; init; }
    public required IReadOnlyList<int> ProjectIds { get; init; }
    public string? SourceType { get; init; } // APP_ROLE|PROJECT_ROLE|SUBSYSTEM_ROLE
    public string? SourceRoleCode { get; init; }
    public int? SourceProjectId { get; init; }
}
