using System;
using System.Collections.Generic;
using System.Linq;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Models.ViewModels;

public static class PermissionKeys
{
    // =============================================================================
    // Per-action permission keys (redesign 2026-04-23)
    // =============================================================================
    // Katalog: docs/known-issues/authz-redesign-per-action-keys.md
    // Matice role × klíč: docs/known-issues/authz-target-matrix.xlsx
    //
    // Konvence: <doména>.<entita>?.<akce>[.<scope>]
    // Staré (pre-redesign) klíče jsou označené [Obsolete] — smazány v Fázi 7 (DB migrace).
    // =============================================================================

    // Prefix helpery (ponecháno — používá se pro HasPermissionPrefix).
    public const string PeoplePrefix = "people.";
    public const string CiselnikyPrefix = "ciselniky.";
    public const string SettingsPrefix = "settings.";

    // --- 1. Projekty -----------------------------------------------------------
    public const string ProjectsReadAll = "projects.read.all";
    public const string ProjectsCreate = "projects.create";
    public const string ProjectsEdit = "projects.edit";
    public const string ProjectsDelete = "projects.delete";

    // --- 2. Záznamy ------------------------------------------------------------
    public const string RecordsCreate = "records.create";
    public const string RecordsEdit = "records.edit";
    public const string RecordsDelete = "records.delete";
    public const string RecordsScheduleEdit = "records.schedule.edit";
    public const string RecordsAssignMeeting = "records.assign.meeting";

    // --- 3. Komentáře ----------------------------------------------------------
    public const string CommentsAdd = "comments.add";
    public const string CommentsEditOwn = "comments.edit.own";
    public const string CommentsEditAny = "comments.edit.any";
    public const string CommentsDeleteOwn = "comments.delete.own";
    public const string CommentsDeleteAny = "comments.delete.any";

    // --- 4. Jednání ------------------------------------------------------------
    public const string MeetingsCreate = "meetings.create";
    public const string MeetingsEdit = "meetings.edit";
    public const string MeetingsDelete = "meetings.delete";
    public const string MeetingsStatusChange = "meetings.status.change";
    public const string MeetingsNotesEdit = "meetings.notes.edit";
    public const string MeetingsNotesSubsystemLead = "meetings.notes.subsystemlead";
    public const string MeetingsAttendanceEdit = "meetings.attendance.edit";
    public const string MeetingsParticipantAdd = "meetings.participant.add";

    // --- 5. Návrhy -------------------------------------------------------------
    public const string ProposalsRecordCreate = "proposals.record.create";
    public const string ProposalsScheduleCreate = "proposals.schedule.create";
    public const string ProposalsEditOwn = "proposals.edit.own";
    public const string ProposalsEditAny = "proposals.edit.any";
    public const string ProposalsAccept = "proposals.accept";
    public const string ProposalsReject = "proposals.reject";
    public const string ProposalsTakeover = "proposals.takeover";

    // --- 6. Vyjádření / externí odkazy ----------------------------------------
    public const string ExterniOdkazySync = "externiodkazy.sync";
    public const string VyjadreniModalOpen = "vyjadreni.modal.open";
    public const string VyjadreniRefresh = "vyjadreni.refresh";
    public const string VyjadreniVazbaCreate = "vyjadreni.vazba.create";
    public const string VyjadreniVazbaDelete = "vyjadreni.vazba.delete";
    public const string VyjadreniReharvest = "vyjadreni.reharvest";

    // --- 7. Tým projektu -------------------------------------------------------
    public const string TeamMemberAdd = "team.member.add";
    public const string TeamMemberRemove = "team.member.remove";
    public const string TeamRoleAssign = "team.role.assign";
    public const string TeamRoleDeactivate = "team.role.deactivate";
    public const string TeamSubsystemCreate = "team.subsystem.create";
    public const string TeamSubsystemReorder = "team.subsystem.reorder";
    public const string TeamSubsystemDeactivate = "team.subsystem.deactivate";
    public const string TeamSubsystemRoleAssign = "team.subsystem.role.assign";
    public const string TeamSubsystemRoleDeactivate = "team.subsystem.role.deactivate";
    public const string TeamCandidatesSearch = "team.candidates.search";

    // --- 8. Osoby --------------------------------------------------------------
    public const string PeopleCreate = "people.create";
    public const string PeopleEdit = "people.edit";
    public const string PeopleDelete = "people.delete";
    public const string PeopleAdSearch = "people.ad.search";
    public const string PeopleAdSync = "people.ad.sync";

    // --- 9. Číselníky ----------------------------------------------------------
    public const string CiselnikyRowEdit = "ciselniky.row.edit";
    public const string CiselnikyRowDelete = "ciselniky.row.delete";

    // --- 10. Výzvy -------------------------------------------------------------
    public const string VyzvyCreate = "vyzvy.create";
    public const string VyzvyStateChange = "vyzvy.state.change";
    public const string VyzvyPnfAssign = "vyzvy.pnf.assign";
    public const string VyzvyPnfReassign = "vyzvy.pnf.reassign";
    public const string VyzvyWordExport = "vyzvy.word.export";

    // --- 11. Dashboard projektu -----------------------------------------------
    public const string DashboardView = "dashboard.view";
    public const string DashboardRecordsView = "dashboard.records.view";
    public const string DashboardNesView = "dashboard.nes.view";
    public const string DashboardStatisticsView = "dashboard.statistics.view";
    public const string DashboardVyzvyView = "dashboard.vyzvy.view";

    // --- 12. Export ------------------------------------------------------------
    public const string ExportPdfProjekt = "export.pdf.projekt";
    public const string ExportPdfJednani = "export.pdf.jednani";
    public const string ExportPdfUkol = "export.pdf.ukol";
    public const string ExportWordProjekt = "export.word.projekt";
    public const string ExportWordJednani = "export.word.jednani";
    public const string ExportWordUkol = "export.word.ukol";

    // --- 13. Nastavení ---------------------------------------------------------
    public const string SettingsView = "settings.view";
    public const string SettingsRolesAssign = "settings.roles.assign";
    public const string SettingsSyncConfigure = "settings.sync.configure";
    public const string SettingsSyncRun = "settings.sync.run";
    public const string SettingsSdView = "settings.sd.view";

    // --- 14. Hledání -----------------------------------------------------------
    public const string SearchIndex = "search.index";
    public const string SearchReindex = "search.reindex";

    // --- 15. Harmonogram preview ----------------------------------------------
    public const string SchedulePreview = "schedule.preview";

    // =========================================================================
    // Definitions: bijekce s PermissionSeedConfiguration.Actions.
    // F7 2026-04-23: 8 deprecated klíčů smazáno (records.schedule.add,
    // records.comment.subsystemlead, team.manage, people.manage, ciselniky.edit,
    // settings.manage, export.pdf, export.word) — viz db_upgrade_1_3_8_authz_per_action_redesign.sql.
    // =========================================================================
    private static readonly PermissionKeyDefinition[] Definitions =
    [
        // 1. Projekty
        new(ProjectsReadAll, "Číst všechny projekty", "PROJECTS", "GLOBAL", "Management visibility — read-only přístup ke všem projektům."),
        new(ProjectsCreate, "Vytvářet projekty", "PROJECTS", "GLOBAL", "Zakládání nových projektů."),
        new(ProjectsEdit, "Upravovat projekty", "PROJECTS", "PROJECT", "Úpravy metadat a konfigurace projektu."),
        new(ProjectsDelete, "Mazat projekty (soft-delete)", "PROJECTS", "PROJECT", "Soft-delete projektu; reverzibilní."),

        // 2. Záznamy
        new(RecordsCreate, "Vytvářet záznamy", "RECORDS", "PROJECT", "Založit nový projektový záznam."),
        new(RecordsEdit, "Upravovat záznamy", "RECORDS", "PROJECT", "Upravit metadata záznamu."),
        new(RecordsDelete, "Mazat záznamy", "RECORDS", "PROJECT", "Smazat záznam."),
        new(RecordsScheduleEdit, "Upravovat harmonogram úkolu", "RECORDS", "PROJECT", "Editace všech slotů harmonogramu úkolu."),
        new(RecordsAssignMeeting, "Přiřadit identifikátor jednání", "RECORDS", "PROJECT", "Doplnit identifikátor jednání k záznamu."),

        // 3. Komentáře
        new(CommentsAdd, "Přidávat komentáře", "COMMENTS", "PROJECT", "Vkládání nových komentářů k záznamům."),
        new(CommentsEditOwn, "Upravovat vlastní komentáře", "COMMENTS", "PROJECT", "Úprava komentářů, které osoba sama vložila."),
        new(CommentsEditAny, "Upravovat cizí komentáře (admin)", "COMMENTS", "PROJECT", "Admin úprava libovolného komentáře."),
        new(CommentsDeleteOwn, "Mazat vlastní komentáře", "COMMENTS", "PROJECT", "Smazání komentářů, které osoba sama vložila."),
        new(CommentsDeleteAny, "Mazat cizí komentáře (admin)", "COMMENTS", "PROJECT", "Admin smazání libovolného komentáře."),

        // 4. Jednání
        new(MeetingsCreate, "Zakládat jednání", "MEETINGS", "PROJECT", "Nové jednání v projektu."),
        new(MeetingsEdit, "Upravovat jednání", "MEETINGS", "PROJECT", "Změna metadat jednání."),
        new(MeetingsDelete, "Mazat jednání", "MEETINGS", "PROJECT", "Smazat jednání."),
        new(MeetingsStatusChange, "Měnit stav jednání", "MEETINGS", "PROJECT", "Změna stavu jednání (DRAFT/OPEN/CLOSED)."),
        new(MeetingsNotesEdit, "Upravovat zápis jednání", "MEETINGS", "PROJECT", "Editace poznámek k jednání."),
        new(MeetingsNotesSubsystemLead, "Zápis za vedoucího subsystému", "MEETINGS", "PROJECT", "Přidat zápis/vyjádření za vedoucího subsystému."),
        new(MeetingsAttendanceEdit, "Upravovat docházku", "MEETINGS", "PROJECT", "Záznamy účasti na jednání."),
        new(MeetingsParticipantAdd, "Přidat účastníka jednání", "MEETINGS", "PROJECT", "Registrace účastníka jednání."),

        // 5. Návrhy
        new(ProposalsRecordCreate, "Navrhnout nový záznam", "PROPOSALS", "PROJECT", "Vytvoření návrhu nového záznamu."),
        new(ProposalsScheduleCreate, "Navrhnout úpravu harmonogramu", "PROPOSALS", "PROJECT", "Vytvoření návrhu úpravy harmonogramu."),
        new(ProposalsEditOwn, "Upravit vlastní návrh", "PROPOSALS", "PROJECT", "Úprava návrhu před rozhodnutím (autor)."),
        new(ProposalsEditAny, "Upravit cizí návrh (admin)", "PROPOSALS", "PROJECT", "Admin úprava libovolného otevřeného návrhu."),
        new(ProposalsAccept, "Schválit návrh", "PROPOSALS", "PROJECT", "Schválení návrhu."),
        new(ProposalsReject, "Zamítnout návrh", "PROPOSALS", "PROJECT", "Zamítnutí návrhu."),
        new(ProposalsTakeover, "Převzít návrh", "PROPOSALS", "PROJECT", "Zamítnutí návrhu + převzetí vytvoření záznamu."),

        // 6. Vyjádření / externí odkazy
        new(ExterniOdkazySync, "Synchronizovat externí odkaz", "EXTERNI", "PROJECT", "Ruční synchronizace externího odkazu se ServiceDeskem."),
        new(VyjadreniModalOpen, "Otevřít vyjádření (modal)", "EXTERNI", "PROJECT", "Otevření chat modalu s vyjádřeními."),
        new(VyjadreniRefresh, "Obnovit vyjádření", "EXTERNI", "PROJECT", "Refresh vyjádření z externího zdroje."),
        new(VyjadreniVazbaCreate, "Vytvořit vazbu vyjádření", "EXTERNI", "PROJECT", "Vytvoření vazby na krok harmonogramu."),
        new(VyjadreniVazbaDelete, "Smazat vazbu vyjádření", "EXTERNI", "PROJECT", "Smazání vazby na krok harmonogramu."),
        new(VyjadreniReharvest, "ReHarvest vyjádření (admin)", "EXTERNI", "PROJECT", "Admin akce — znovunačíst vyjádření per ticket."),

        // 7. Tým
        new(TeamMemberAdd, "Přidat člena týmu", "TEAM", "PROJECT", "Přidání osoby do týmu projektu."),
        new(TeamMemberRemove, "Odebrat člena týmu", "TEAM", "PROJECT", "Odebrání osoby z týmu projektu."),
        new(TeamRoleAssign, "Přiřadit projektovou roli", "TEAM", "PROJECT", "Přiřazení role na úrovni projektu."),
        new(TeamRoleDeactivate, "Deaktivovat projektovou roli", "TEAM", "PROJECT", "Deaktivace role na úrovni projektu."),
        new(TeamSubsystemCreate, "Vytvořit subsystém projektu", "TEAM", "PROJECT", "Přiřadit subsystém k projektu."),
        new(TeamSubsystemReorder, "Přeřadit subsystémy", "TEAM", "PROJECT", "Změna pořadí subsystémů projektu."),
        new(TeamSubsystemDeactivate, "Deaktivovat subsystém", "TEAM", "PROJECT", "Deaktivace subsystému projektu."),
        new(TeamSubsystemRoleAssign, "Přiřadit roli subsystému", "TEAM", "PROJECT", "Přiřazení role na úrovni subsystému."),
        new(TeamSubsystemRoleDeactivate, "Deaktivovat roli subsystému", "TEAM", "PROJECT", "Deaktivace role subsystému."),
        new(TeamCandidatesSearch, "Hledat kandidáty do týmu", "TEAM", "PROJECT", "Fulltextové vyhledávání osob do týmu."),

        // 8. Osoby
        new(PeopleCreate, "Přidat osobu", "PEOPLE", "GLOBAL", "Založení nové osoby."),
        new(PeopleEdit, "Upravit osobu", "PEOPLE", "GLOBAL", "Úprava osoby a jejích atributů."),
        new(PeopleDelete, "Smazat osobu", "PEOPLE", "GLOBAL", "Odstranění osoby."),
        new(PeopleAdSearch, "Hledat v Active Directory", "PEOPLE", "GLOBAL", "Vyhledávání osob v AD."),
        new(PeopleAdSync, "Synchronizovat osobu z AD", "PEOPLE", "GLOBAL", "Synchronizace osoby ze záznamu v AD."),

        // 9. Číselníky
        new(CiselnikyRowEdit, "Upravit řádek číselníku", "CISELNIKY", "GLOBAL", "Úprava / uložení řádku číselníku."),
        new(CiselnikyRowDelete, "Smazat řádek číselníku", "CISELNIKY", "GLOBAL", "Smazání řádku číselníku."),

        // 10. Výzvy
        new(VyzvyCreate, "Založit výzvu", "VYZVY", "PROJECT", "Založení výzvy z bufferu."),
        new(VyzvyStateChange, "Změnit stav výzvy", "VYZVY", "PROJECT", "Změna stavu výzvy."),
        new(VyzvyPnfAssign, "Zařadit PNF", "VYZVY", "PROJECT", "Zařadit / vyřadit PNF do bufferu."),
        new(VyzvyPnfReassign, "Přeřadit PNF", "VYZVY", "PROJECT", "Přeřadit PNF mezi výzvami."),
        new(VyzvyWordExport, "Word export výzvy", "VYZVY", "PROJECT", "Stáhnout Word export výzvy (budoucí feature)."),

        // 11. Dashboard
        new(DashboardView, "Otevřít projektový dashboard", "DASHBOARD", "PROJECT", "Vstup na dashboard projektu."),
        new(DashboardRecordsView, "Záložka Záznamy", "DASHBOARD", "PROJECT", "Panel Záznamy v dashboardu."),
        new(DashboardNesView, "Záložka NES v prodlení", "DASHBOARD", "PROJECT", "Panel NES v prodlení."),
        new(DashboardStatisticsView, "Záložka Statistiky", "DASHBOARD", "PROJECT", "Panel Statistiky."),
        new(DashboardVyzvyView, "Záložka Výzvy", "DASHBOARD", "PROJECT", "Panel Výzvy v dashboardu."),

        // 12. Export
        new(ExportPdfProjekt, "PDF export projektu", "EXPORT", "PROJECT", "Tisk projektu do PDF."),
        new(ExportPdfJednani, "PDF export jednání", "EXPORT", "PROJECT", "Tisk jednání do PDF."),
        new(ExportPdfUkol, "PDF export úkolu", "EXPORT", "PROJECT", "Tisk úkolu do PDF."),
        new(ExportWordProjekt, "Word export projektu", "EXPORT", "PROJECT", "Word export projektu."),
        new(ExportWordJednani, "Word export jednání", "EXPORT", "PROJECT", "Word export jednání."),
        new(ExportWordUkol, "Word export úkolu", "EXPORT", "PROJECT", "Word export úkolu."),

        // 13. Nastavení
        new(SettingsView, "Zobrazit nastavení", "SETTINGS", "GLOBAL", "Vstup do sekce Nastavení."),
        new(SettingsRolesAssign, "Přiřadit globální roli", "SETTINGS", "GLOBAL", "Přiřazení globální role uživateli."),
        new(SettingsSyncConfigure, "Konfigurace sync jobu", "SETTINGS", "GLOBAL", "Úprava konfigurace synchronizačního jobu."),
        new(SettingsSyncRun, "Spustit sync job", "SETTINGS", "GLOBAL", "Manuální spuštění synchronizačního jobu."),
        new(SettingsSdView, "SD konektor (admin přehled)", "SETTINGS", "GLOBAL", "Otevřít SD konektor s admin přehledem."),

        // 14. Hledání
        new(SearchIndex, "Fulltext hledání", "SEARCH", "GLOBAL", "Globální fulltext hledání."),
        new(SearchReindex, "Spustit reindex", "SEARCH", "GLOBAL", "Administrátorská akce: full reindex FTS."),

        // 15. Harmonogram preview
        new(SchedulePreview, "Náhledový přepočet harmonogramu", "SCHEDULE", "PROJECT", "Stateless kalkulace pro editor úkolu."),

        // =====================================================================
        // DEPRECATED (pre-redesign). Mapping test vynucuje existenci i v seedu
        // dokud F7 (DB migrace) staré klíče neodstraní.
        // =====================================================================
    ];

    private static readonly HashSet<string> SupportedKeys = new(
        Definitions.Select(x => x.Key),
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ProjectReadGrantKeys = new(
    [
        // Nový model — read-grantující klíče (pokud osoba má kterýkoli z nich,
        // smí vidět projekt).
        ProjectsEdit,
        ProjectsDelete,
        RecordsCreate,
        RecordsEdit,
        RecordsDelete,
        RecordsScheduleEdit,
        RecordsAssignMeeting,
        CommentsAdd,
        CommentsEditOwn,
        CommentsEditAny,
        CommentsDeleteOwn,
        CommentsDeleteAny,
        MeetingsCreate,
        MeetingsEdit,
        MeetingsDelete,
        MeetingsStatusChange,
        MeetingsNotesEdit,
        MeetingsNotesSubsystemLead,
        MeetingsAttendanceEdit,
        MeetingsParticipantAdd,
        ProposalsRecordCreate,
        ProposalsScheduleCreate,
        ProposalsEditOwn,
        ProposalsEditAny,
        ProposalsAccept,
        ProposalsReject,
        ProposalsTakeover,
        ExterniOdkazySync,
        VyjadreniModalOpen,
        VyjadreniRefresh,
        VyjadreniVazbaCreate,
        VyjadreniVazbaDelete,
        VyjadreniReharvest,
        TeamMemberAdd,
        TeamMemberRemove,
        TeamRoleAssign,
        TeamRoleDeactivate,
        TeamSubsystemCreate,
        TeamSubsystemReorder,
        TeamSubsystemDeactivate,
        TeamSubsystemRoleAssign,
        TeamSubsystemRoleDeactivate,
        TeamCandidatesSearch,
        VyzvyCreate,
        VyzvyStateChange,
        VyzvyPnfAssign,
        VyzvyPnfReassign,
        VyzvyWordExport,
        DashboardView,
        DashboardRecordsView,
        DashboardNesView,
        DashboardStatisticsView,
        DashboardVyzvyView,
        ExportPdfProjekt,
        ExportPdfJednani,
        ExportPdfUkol,
        ExportWordProjekt,
        ExportWordJednani,
        ExportWordUkol,
        SchedulePreview
    ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ProjectWriteKeysBlockedForDeletedProjects = new(
    [
        // Nový model — všechny write klíče projektového scope
        ProjectsEdit,
        ProjectsDelete,
        RecordsCreate,
        RecordsEdit,
        RecordsDelete,
        RecordsScheduleEdit,
        RecordsAssignMeeting,
        CommentsAdd,
        CommentsEditOwn,
        CommentsEditAny,
        CommentsDeleteOwn,
        CommentsDeleteAny,
        MeetingsCreate,
        MeetingsEdit,
        MeetingsDelete,
        MeetingsStatusChange,
        MeetingsNotesEdit,
        MeetingsNotesSubsystemLead,
        MeetingsAttendanceEdit,
        MeetingsParticipantAdd,
        ProposalsRecordCreate,
        ProposalsScheduleCreate,
        ProposalsEditOwn,
        ProposalsEditAny,
        ProposalsAccept,
        ProposalsReject,
        ProposalsTakeover,
        ExterniOdkazySync,
        VyjadreniRefresh,
        VyjadreniVazbaCreate,
        VyjadreniVazbaDelete,
        VyjadreniReharvest,
        TeamMemberAdd,
        TeamMemberRemove,
        TeamRoleAssign,
        TeamRoleDeactivate,
        TeamSubsystemCreate,
        TeamSubsystemReorder,
        TeamSubsystemDeactivate,
        TeamSubsystemRoleAssign,
        TeamSubsystemRoleDeactivate,
        VyzvyCreate,
        VyzvyStateChange,
        VyzvyPnfAssign,
        VyzvyPnfReassign
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

    /// <summary>
    /// True, pokud osoba má klíč z PŘÍMÉ projektové role (nebo globálně), tj. NE pouze
    /// jako zděděný subsystémový grant. Pro rozlišení „projektová role vs. vedoucí subsystému".
    /// </summary>
    public bool HasDirectProjectPermission(string permissionKey, int projektId)
    {
        if (IsProjectReadOnly(projektId) && PermissionKeys.IsBlockedForDeletedProject(permissionKey))
        {
            return false;
        }

        var authz = Authorization ?? throw new InvalidOperationException(
            "AuthorizationSnapshot musí být vyplněn pro tento request.");

        return authz.HasDirectProjectPermission(permissionKey, projektId);
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
