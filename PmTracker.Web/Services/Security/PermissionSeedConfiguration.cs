// =============================================================================
// GLOSSARY — autorizační model PM Trackeru
// =============================================================================
//
// Tento soubor je JEDNÍM ze DVOU autoritativních zdrojů autorizace:
//   (1) PermissionSeedConfiguration (tento soubor) — katalog rolí, permission
//       keys, a mappingů mezi nimi. Verzovaný v gitu, review v PR.
//   (2) ObsazeniProjektu + ObsazeniSubsystemuProjektu — DB tabulky, kam admin
//       v UI přiřazuje konkrétní osoby do projektových/subsystémových rolí.
//
// Manuální UI kompozice rolí z permission keys (tab "Akce/Role/Uživatel-Role"
// v Nastavení) je zrušena — role jsou nyní definované POUZE v seedu tohoto
// souboru. Důvod: audit trail v gitu, review four-eyes, žádný drift mezi
// seedem a runtime. Nové role = PR do tohoto souboru + deploy.
// Viz spec §3 a plán 2026-04-22-authz-phases-b-to-f.md.
//
// TŘI PODOBNÉ POJMY — NEZAMĚŇOVAT:
//
//   RoleScope            — úroveň ROLE. Určuje, JAK se role přiřazuje:
//                           - Global     → přes authz.user_roles (přímo)
//                           - Project    → přes ObsazeniProjektu (na projekt)
//                           - Subsystem  → přes ObsazeniSubsystemuProjektu (na subsystém)
//                         Příklad: VLASTNIK_PROJEKTU má RoleScope.Project.
//
//   PermissionScopeLevel — úroveň permission KEY. Určuje, zda key potřebuje
//                          projektový kontext při kontrole:
//                           - Global   → nepotřebuje projektId (např. people.manage)
//                           - Project  → potřebuje projektId (např. projects.edit)
//                         Příklad: records.edit má PermissionScopeLevel.Project.
//
//   ScopeMode            — šířka grantu v role→permission MAPPINGU:
//                           - All       → všechny entity v rozsahu role (99 % mappingů)
//                           - Include   → jen vyjmenované entity (role_permission_projects)
//                           - Own       → jen entity vlastněné osobou (Phase C, comments)
//                           - Subsystem → jen entity v subsystému osoby (Phase C)
//                         Příklad: (ADM_PROJ, records.edit, All) = smí editovat
//                         VŠECHNY záznamy na projektech, kde má ADM_PROJ.
//
// Matice platných kombinací (zjednodušeně):
//
//   RoleScope  │ PermissionScopeLevel │ ScopeMode typicky
//   ───────────┼──────────────────────┼──────────────────
//   Global     │ Global, Project      │ All, Include
//   Project    │ Project              │ All, Own
//   Subsystem  │ Project              │ All, Subsystem, Own
//
// Resolver (UserContextResolver) interpretuje kombinaci Scope × ScopeMode tak,
// aby vrátil správnou množinu projektId/subsystemId, kde grant platí.
// =============================================================================

namespace PmTracker.Web.Services.Security;

public sealed record PermissionCategorySeedItem(string Kod, string Nazev, int SortOrder);
public sealed record RoleSeedItem(string Kod, string Nazev, string Popis, bool IsSystem, RoleScope Scope);
public sealed record ActionSeedItem(string Klic, string Nazev, string CategoryKod, PermissionScopeLevel ScopeLevel);
public sealed record RoleActionSeedItem(string RoleKod, string ActionKlic, ScopeMode ScopeMode, bool IsAllowed);

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
        // Globální role
        new("SUPERADMIN", "Superadmin", "Pevná role s plnými oprávněními.", true, RoleScope.Global),
        new("APP_ADMIN", "Administrátor aplikace", "Správa aplikace a základních entit.", true, RoleScope.Global),
        // Fáze C — Task C2: READ_ALL role pro management visibility
        new("READ_ALL", "Read-all (management visibility)", "Read-only přístup ke všem projektům a jejich datům.", true, RoleScope.Global),

        // Projektové role (použité v ciselnik_roli_projektu)
        new("VLASTNIK_PROJEKTU", "Vlastník projektu", "Plný vlastník projektu.", true, RoleScope.Project),
        new("ADM_PROJ", "Projektový admin", "Silný projektový admin bez práva měnit metadata.", true, RoleScope.Project),
        new("PROJ_MAN", "Projektový manažer", "Projektový manažer bez úprav metadat.", true, RoleScope.Project),
        new("HOST", "Host", "Read-only host projektu.", true, RoleScope.Project),
        new("GEST", "Gestor", "Gestor s komentovacími právy.", true, RoleScope.Project),

        // Subsystémové role (použité v ciselnik_roli_subsystemu)
        new("VEDOUCI_SUBSYSTEMU", "Vedoucí subsystému", "Vedoucí subsystému projektu.", true, RoleScope.Subsystem),
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "Zástupce vedoucího subsystému", "Zástupce vedoucího, stejná práva jako vedoucí.", true, RoleScope.Subsystem),
        new("METODIK_SUBSYSTEMU", "Metodik subsystému", "Metodik s komentovacími právy.", true, RoleScope.Subsystem)
    ];

    public static readonly IReadOnlyList<ActionSeedItem> Actions =
    [
        new("projects.create", "Vytvářet projekty", "PROJECTS", PermissionScopeLevel.Project),
        new("projects.edit", "Upravovat projekty", "PROJECTS", PermissionScopeLevel.Project),
        new("projects.delete", "Mazat projekty (soft-delete)", "PROJECTS", PermissionScopeLevel.Project),
        new("records.edit", "Upravovat projektové záznamy", "RECORDS", PermissionScopeLevel.Project),
        new("records.schedule.add", "Doplňovat harmonogram úkolu", "RECORDS", PermissionScopeLevel.Project),
        new("records.schedule.edit", "Upravovat harmonogram úkolu", "RECORDS", PermissionScopeLevel.Project),
        new("records.comment.subsystemlead", "Přidávat vyjádření jako vedoucí subsystému", "RECORDS", PermissionScopeLevel.Project),
        new("meetings.create", "Zakládat jednání", "MEETINGS", PermissionScopeLevel.Project),
        new("meetings.edit", "Upravovat jednání", "MEETINGS", PermissionScopeLevel.Project),
        new("team.manage", "Správa týmu projektu", "PROJECTS", PermissionScopeLevel.Project),
        new("people.manage", "Správa osob", "MASTER", PermissionScopeLevel.Global),
        new("ciselniky.edit", "Editace číselníků", "MASTER", PermissionScopeLevel.Global),
        new("settings.view", "Zobrazit nastavení", "SETTINGS", PermissionScopeLevel.Global),
        new("settings.manage", "Správa nastavení", "SETTINGS", PermissionScopeLevel.Global),
        // Fáze C — Task C1: nové permission keys
        new("dashboard.view", "Zobrazit projektový dashboard", "PROJECTS", PermissionScopeLevel.Project),
        new("export.pdf", "Exportovat projekt do PDF", "PROJECTS", PermissionScopeLevel.Project),
        new("export.word", "Exportovat projekt do Word", "PROJECTS", PermissionScopeLevel.Project),
        new("comments.add", "Přidávat komentáře", "RECORDS", PermissionScopeLevel.Project),
        new("comments.edit.own", "Upravovat vlastní komentáře", "RECORDS", PermissionScopeLevel.Project),
        new("comments.delete.own", "Mazat vlastní komentáře", "RECORDS", PermissionScopeLevel.Project),
        new("search.reindex", "Spustit reindex vyhledávání", "SETTINGS", PermissionScopeLevel.Global),
        new("projects.read.all", "Číst všechny projekty", "PROJECTS", PermissionScopeLevel.Global)
    ];

    public static readonly IReadOnlyList<RoleActionSeedItem> RoleMappings =
    [
        new("SUPERADMIN", "projects.create", ScopeMode.All, true),
        new("SUPERADMIN", "projects.edit", ScopeMode.All, true),
        new("SUPERADMIN", "projects.delete", ScopeMode.All, true),
        new("SUPERADMIN", "records.edit", ScopeMode.All, true),
        new("SUPERADMIN", "records.schedule.add", ScopeMode.All, true),
        new("SUPERADMIN", "records.schedule.edit", ScopeMode.All, true),
        new("SUPERADMIN", "records.comment.subsystemlead", ScopeMode.All, true),
        new("SUPERADMIN", "meetings.create", ScopeMode.All, true),
        new("SUPERADMIN", "meetings.edit", ScopeMode.All, true),
        new("SUPERADMIN", "team.manage", ScopeMode.All, true),
        new("SUPERADMIN", "people.manage", ScopeMode.All, true),
        new("SUPERADMIN", "ciselniky.edit", ScopeMode.All, true),
        new("SUPERADMIN", "settings.view", ScopeMode.All, true),
        new("SUPERADMIN", "settings.manage", ScopeMode.All, true),
        // Fáze C — Task C2: SUPERADMIN nové keys
        new("SUPERADMIN", "dashboard.view", ScopeMode.All, true),
        new("SUPERADMIN", "export.pdf", ScopeMode.All, true),
        new("SUPERADMIN", "export.word", ScopeMode.All, true),
        new("SUPERADMIN", "comments.add", ScopeMode.All, true),
        new("SUPERADMIN", "comments.edit.own", ScopeMode.All, true),
        new("SUPERADMIN", "comments.delete.own", ScopeMode.All, true),
        new("SUPERADMIN", "search.reindex", ScopeMode.All, true),
        new("SUPERADMIN", "projects.read.all", ScopeMode.All, true),

        new("APP_ADMIN", "projects.create", ScopeMode.All, true),
        new("APP_ADMIN", "projects.edit", ScopeMode.All, true),
        new("APP_ADMIN", "records.edit", ScopeMode.All, true),
        new("APP_ADMIN", "records.schedule.add", ScopeMode.All, true),
        new("APP_ADMIN", "records.schedule.edit", ScopeMode.All, true),
        new("APP_ADMIN", "records.comment.subsystemlead", ScopeMode.All, true),
        new("APP_ADMIN", "meetings.create", ScopeMode.All, true),
        new("APP_ADMIN", "meetings.edit", ScopeMode.All, true),
        new("APP_ADMIN", "team.manage", ScopeMode.All, true),
        new("APP_ADMIN", "people.manage", ScopeMode.All, true),
        new("APP_ADMIN", "ciselniky.edit", ScopeMode.All, true),
        new("APP_ADMIN", "settings.view", ScopeMode.All, true),
        // Fáze C — Task C2: APP_ADMIN administrativní nové keys
        new("APP_ADMIN", "dashboard.view", ScopeMode.All, true),
        new("APP_ADMIN", "search.reindex", ScopeMode.All, true),
        new("APP_ADMIN", "projects.read.all", ScopeMode.All, true),

        // Fáze C — Task C2: READ_ALL mappings
        new("READ_ALL", "projects.read.all", ScopeMode.All, true),
        new("READ_ALL", "dashboard.view", ScopeMode.All, true),
        new("READ_ALL", "export.pdf", ScopeMode.All, true),
        new("READ_ALL", "export.word", ScopeMode.All, true),

        // Authorization unification — Fáze A — Task A8
        // Projektové role mappings (jen existující permission keys;
        // dashboard/export/comments se přidají ve Fázi C)
        new("VLASTNIK_PROJEKTU", "projects.edit", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "records.edit", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "records.schedule.add", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "records.schedule.edit", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "records.comment.subsystemlead", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "meetings.create", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "meetings.edit", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "team.manage", ScopeMode.All, true),
        // VLASTNIK_PROJEKTU — Fáze C keys:
        new("VLASTNIK_PROJEKTU", "dashboard.view", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "export.pdf", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "export.word", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "comments.add", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "comments.edit.own", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "comments.delete.own", ScopeMode.All, true),

        new("ADM_PROJ", "records.edit", ScopeMode.All, true),
        new("ADM_PROJ", "records.schedule.add", ScopeMode.All, true),
        new("ADM_PROJ", "records.schedule.edit", ScopeMode.All, true),
        new("ADM_PROJ", "records.comment.subsystemlead", ScopeMode.All, true),
        new("ADM_PROJ", "meetings.create", ScopeMode.All, true),
        new("ADM_PROJ", "meetings.edit", ScopeMode.All, true),
        new("ADM_PROJ", "team.manage", ScopeMode.All, true),
        // ADM_PROJ — Fáze C keys:
        new("ADM_PROJ", "dashboard.view", ScopeMode.All, true),
        new("ADM_PROJ", "export.pdf", ScopeMode.All, true),
        new("ADM_PROJ", "export.word", ScopeMode.All, true),
        new("ADM_PROJ", "comments.add", ScopeMode.All, true),
        new("ADM_PROJ", "comments.edit.own", ScopeMode.All, true),
        new("ADM_PROJ", "comments.delete.own", ScopeMode.All, true),

        new("PROJ_MAN", "records.edit", ScopeMode.All, true),
        new("PROJ_MAN", "records.schedule.add", ScopeMode.All, true),
        new("PROJ_MAN", "records.schedule.edit", ScopeMode.All, true),
        new("PROJ_MAN", "records.comment.subsystemlead", ScopeMode.All, true),
        new("PROJ_MAN", "meetings.create", ScopeMode.All, true),
        new("PROJ_MAN", "meetings.edit", ScopeMode.All, true),
        new("PROJ_MAN", "team.manage", ScopeMode.All, true),
        // PROJ_MAN — Fáze C keys:
        new("PROJ_MAN", "dashboard.view", ScopeMode.All, true),
        new("PROJ_MAN", "export.pdf", ScopeMode.All, true),
        new("PROJ_MAN", "export.word", ScopeMode.All, true),
        new("PROJ_MAN", "comments.add", ScopeMode.All, true),
        new("PROJ_MAN", "comments.edit.own", ScopeMode.All, true),
        new("PROJ_MAN", "comments.delete.own", ScopeMode.All, true),

        new("GEST", "records.comment.subsystemlead", ScopeMode.All, true),
        // GEST — Fáze C (comments + read):
        new("GEST", "dashboard.view", ScopeMode.All, true),
        new("GEST", "export.pdf", ScopeMode.All, true),
        new("GEST", "export.word", ScopeMode.All, true),
        new("GEST", "comments.add", ScopeMode.All, true),
        new("GEST", "comments.edit.own", ScopeMode.All, true),
        new("GEST", "comments.delete.own", ScopeMode.All, true),

        // HOST — Fáze C (read-only):
        new("HOST", "dashboard.view", ScopeMode.All, true),
        new("HOST", "export.pdf", ScopeMode.All, true),
        new("HOST", "export.word", ScopeMode.All, true),

        // Authorization unification — Fáze A — Task A9
        // Subsystémové role mappings
        new("VEDOUCI_SUBSYSTEMU", "records.comment.subsystemlead", ScopeMode.All, true),
        // VEDOUCI_SUBSYSTEMU — Fáze C (comments):
        new("VEDOUCI_SUBSYSTEMU", "comments.add", ScopeMode.All, true),
        new("VEDOUCI_SUBSYSTEMU", "comments.edit.own", ScopeMode.All, true),
        new("VEDOUCI_SUBSYSTEMU", "comments.delete.own", ScopeMode.All, true),

        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "records.comment.subsystemlead", ScopeMode.All, true),
        // ZASTUPCE_VEDOUCIHO_SUBSYSTEMU — Fáze C:
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "comments.add", ScopeMode.All, true),
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "comments.edit.own", ScopeMode.All, true),
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "comments.delete.own", ScopeMode.All, true),

        // METODIK_SUBSYSTEMU — Fáze C:
        new("METODIK_SUBSYSTEMU", "comments.add", ScopeMode.All, true),
        new("METODIK_SUBSYSTEMU", "comments.edit.own", ScopeMode.All, true),
        new("METODIK_SUBSYSTEMU", "comments.delete.own", ScopeMode.All, true)
    ];
}
