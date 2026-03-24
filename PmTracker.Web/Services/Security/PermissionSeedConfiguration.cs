namespace PmTracker.Web.Services.Security;

public sealed record PermissionCategorySeedItem(string Kod, string Nazev, int SortOrder);
public sealed record RoleSeedItem(string Kod, string Nazev, string Popis, bool IsSystem);
public sealed record ActionSeedItem(string Klic, string Nazev, string CategoryKod, string ScopeLevel);
public sealed record RoleActionSeedItem(string RoleKod, string ActionKlic, string ScopeMode, bool IsAllowed);

public static class PermissionSeedConfiguration
{
    public static readonly IReadOnlyList<PermissionCategorySeedItem> Categories =
    [
        new("PROJECTS", "Projekty", 10),
        new("RECORDS", "Projektové záznamy", 20),
        new("MEETINGS", "Jednání", 30),
        new("MASTER", "Číselníky a osoby", 40),
        new("SETTINGS", "Nastavení", 50)
    ];

    public static readonly IReadOnlyList<RoleSeedItem> Roles =
    [
        new("SUPERADMIN", "Superadmin", "Pevná role s plnými oprávněními.", true),
        new("APP_ADMIN", "Administrátor aplikace", "Správa aplikace a základních entit.", true)
    ];

    public static readonly IReadOnlyList<ActionSeedItem> Actions =
    [
        new("projects.create", "Vytvářet projekty", "PROJECTS", "PROJECT"),
        new("projects.edit", "Upravovat projekty", "PROJECTS", "PROJECT"),
        new("projects.delete", "Mazat projekty (soft-delete)", "PROJECTS", "PROJECT"),
        new("records.edit", "Upravovat projektové záznamy", "RECORDS", "PROJECT"),
        new("records.schedule.add", "Doplňovat harmonogram úkolu", "RECORDS", "PROJECT"),
        new("records.schedule.edit", "Upravovat harmonogram úkolu", "RECORDS", "PROJECT"),
        new("records.comment.subsystemlead", "Přidávat vyjádření jako vedoucí subsystému", "RECORDS", "PROJECT"),
        new("meetings.create", "Zakládat jednání", "MEETINGS", "PROJECT"),
        new("meetings.edit", "Upravovat jednání", "MEETINGS", "PROJECT"),
        new("team.manage", "Správa týmu projektu", "PROJECTS", "PROJECT"),
        new("people.manage", "Správa osob", "MASTER", "GLOBAL"),
        new("ciselniky.edit", "Editace číselníků", "MASTER", "GLOBAL"),
        new("settings.view", "Zobrazit nastavení", "SETTINGS", "GLOBAL"),
        new("settings.manage", "Správa nastavení", "SETTINGS", "GLOBAL")
    ];

    public static readonly IReadOnlyList<RoleActionSeedItem> RoleMappings =
    [
        new("SUPERADMIN", "projects.create", "ALL", true),
        new("SUPERADMIN", "projects.edit", "ALL", true),
        new("SUPERADMIN", "projects.delete", "ALL", true),
        new("SUPERADMIN", "records.edit", "ALL", true),
        new("SUPERADMIN", "records.schedule.add", "ALL", true),
        new("SUPERADMIN", "records.schedule.edit", "ALL", true),
        new("SUPERADMIN", "records.comment.subsystemlead", "ALL", true),
        new("SUPERADMIN", "meetings.create", "ALL", true),
        new("SUPERADMIN", "meetings.edit", "ALL", true),
        new("SUPERADMIN", "team.manage", "ALL", true),
        new("SUPERADMIN", "people.manage", "ALL", true),
        new("SUPERADMIN", "ciselniky.edit", "ALL", true),
        new("SUPERADMIN", "settings.view", "ALL", true),
        new("SUPERADMIN", "settings.manage", "ALL", true),

        new("APP_ADMIN", "projects.create", "ALL", true),
        new("APP_ADMIN", "projects.edit", "ALL", true),
        new("APP_ADMIN", "records.edit", "ALL", true),
        new("APP_ADMIN", "records.schedule.add", "ALL", true),
        new("APP_ADMIN", "records.schedule.edit", "ALL", true),
        new("APP_ADMIN", "records.comment.subsystemlead", "ALL", true),
        new("APP_ADMIN", "meetings.create", "ALL", true),
        new("APP_ADMIN", "meetings.edit", "ALL", true),
        new("APP_ADMIN", "team.manage", "ALL", true),
        new("APP_ADMIN", "people.manage", "ALL", true),
        new("APP_ADMIN", "ciselniky.edit", "ALL", true),
        new("APP_ADMIN", "settings.view", "ALL", true)
    ];
}
