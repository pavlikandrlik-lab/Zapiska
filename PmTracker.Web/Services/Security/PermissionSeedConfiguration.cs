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
//                           - Global   → nepotřebuje projektId (např. people.*)
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
//
// =============================================================================
// REDESIGN 2026-04-23 — per-action permission keys
// =============================================================================
//
// Katalog klíčů: docs/known-issues/authz-redesign-per-action-keys.md
// Matice role × klíč: docs/known-issues/authz-target-matrix.xlsx
//
// Seed obsahuje:
//   * 76 per-action klíčů (nový cílový model)
//   * 8 deprecated klíčů (pre-redesign): records.schedule.add,
//     records.comment.subsystemlead, team.manage, people.manage,
//     ciselniky.edit, settings.manage, export.pdf, export.word.
//     Zachovány pro kompatibilitu během Fází 1–6 migrace; smažou se
//     v Fázi 7 + DB migraci.
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
        new("COMMENTS", "Komentáře", 25),
        new("MEETINGS", "Jednání", 30),
        new("PROPOSALS", "Návrhy", 35),
        new("EXTERNI", "Externí odkazy a vyjádření", 40),
        new("TEAM", "Tým projektu", 45),
        new("PEOPLE", "Osoby", 50),
        new("CISELNIKY", "Číselníky", 55),
        new("VYZVY", "Výzvy", 60),
        new("DASHBOARD", "Dashboard", 65),
        new("EXPORT", "Export", 70),
        new("SETTINGS", "Nastavení", 75),
        new("SEARCH", "Hledání", 80),
        new("SCHEDULE", "Harmonogram preview", 85),
        // Ponecháno pro deprecated klíče (MASTER sjednoceno pod PEOPLE/CISELNIKY).
        new("MASTER", "Master data (deprecated)", 95)
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
        // ==== 1. Projekty ====
        new("projects.read.all", "Číst všechny projekty", "PROJECTS", PermissionScopeLevel.Global),
        new("projects.create", "Vytvářet projekty", "PROJECTS", PermissionScopeLevel.Global),
        new("projects.edit", "Upravovat projekty", "PROJECTS", PermissionScopeLevel.Project),
        new("projects.delete", "Mazat projekty (soft-delete)", "PROJECTS", PermissionScopeLevel.Project),

        // ==== 2. Záznamy ====
        new("records.create", "Vytvářet záznamy", "RECORDS", PermissionScopeLevel.Project),
        new("records.edit", "Upravovat záznamy", "RECORDS", PermissionScopeLevel.Project),
        new("records.delete", "Mazat záznamy", "RECORDS", PermissionScopeLevel.Project),
        new("records.schedule.edit", "Upravovat harmonogram úkolu", "RECORDS", PermissionScopeLevel.Project),
        new("records.assign.meeting", "Přiřadit identifikátor jednání", "RECORDS", PermissionScopeLevel.Project),

        // ==== 3. Komentáře ====
        new("comments.add", "Přidávat komentáře", "COMMENTS", PermissionScopeLevel.Project),
        new("comments.edit.own", "Upravovat vlastní komentáře", "COMMENTS", PermissionScopeLevel.Project),
        new("comments.edit.any", "Upravovat cizí komentáře (admin)", "COMMENTS", PermissionScopeLevel.Project),
        new("comments.delete.own", "Mazat vlastní komentáře", "COMMENTS", PermissionScopeLevel.Project),
        new("comments.delete.any", "Mazat cizí komentáře (admin)", "COMMENTS", PermissionScopeLevel.Project),

        // ==== 4. Jednání ====
        new("meetings.create", "Zakládat jednání", "MEETINGS", PermissionScopeLevel.Project),
        new("meetings.edit", "Upravovat jednání", "MEETINGS", PermissionScopeLevel.Project),
        new("meetings.delete", "Mazat jednání", "MEETINGS", PermissionScopeLevel.Project),
        new("meetings.status.change", "Měnit stav jednání", "MEETINGS", PermissionScopeLevel.Project),
        new("meetings.notes.edit", "Upravovat zápis jednání", "MEETINGS", PermissionScopeLevel.Project),
        new("meetings.notes.subsystemlead", "Zápis za vedoucího subsystému", "MEETINGS", PermissionScopeLevel.Project),
        new("meetings.attendance.edit", "Upravovat docházku", "MEETINGS", PermissionScopeLevel.Project),
        new("meetings.participant.add", "Přidat účastníka jednání", "MEETINGS", PermissionScopeLevel.Project),

        // ==== 5. Návrhy ====
        new("proposals.record.create", "Navrhnout nový záznam", "PROPOSALS", PermissionScopeLevel.Project),
        new("proposals.schedule.create", "Navrhnout úpravu harmonogramu", "PROPOSALS", PermissionScopeLevel.Project),
        new("proposals.edit.own", "Upravit vlastní návrh", "PROPOSALS", PermissionScopeLevel.Project),
        new("proposals.edit.any", "Upravit cizí návrh (admin)", "PROPOSALS", PermissionScopeLevel.Project),
        new("proposals.accept", "Schválit návrh", "PROPOSALS", PermissionScopeLevel.Project),
        new("proposals.reject", "Zamítnout návrh", "PROPOSALS", PermissionScopeLevel.Project),
        new("proposals.takeover", "Převzít návrh", "PROPOSALS", PermissionScopeLevel.Project),

        // ==== 6. Vyjádření / externí odkazy ====
        new("externiodkazy.sync", "Synchronizovat externí odkaz", "EXTERNI", PermissionScopeLevel.Project),
        new("vyjadreni.modal.open", "Otevřít vyjádření (modal)", "EXTERNI", PermissionScopeLevel.Project),
        new("vyjadreni.refresh", "Obnovit vyjádření", "EXTERNI", PermissionScopeLevel.Project),
        new("vyjadreni.vazba.create", "Vytvořit vazbu vyjádření", "EXTERNI", PermissionScopeLevel.Project),
        new("vyjadreni.vazba.delete", "Smazat vazbu vyjádření", "EXTERNI", PermissionScopeLevel.Project),
        new("vyjadreni.reharvest", "ReHarvest vyjádření (admin)", "EXTERNI", PermissionScopeLevel.Project),

        // ==== 7. Tým projektu ====
        new("team.member.add", "Přidat člena týmu", "TEAM", PermissionScopeLevel.Project),
        new("team.member.remove", "Odebrat člena týmu", "TEAM", PermissionScopeLevel.Project),
        new("team.role.assign", "Přiřadit projektovou roli", "TEAM", PermissionScopeLevel.Project),
        new("team.role.deactivate", "Deaktivovat projektovou roli", "TEAM", PermissionScopeLevel.Project),
        new("team.subsystem.create", "Vytvořit subsystém projektu", "TEAM", PermissionScopeLevel.Project),
        new("team.subsystem.reorder", "Přeřadit subsystémy", "TEAM", PermissionScopeLevel.Project),
        new("team.subsystem.deactivate", "Deaktivovat subsystém", "TEAM", PermissionScopeLevel.Project),
        new("team.subsystem.role.assign", "Přiřadit roli subsystému", "TEAM", PermissionScopeLevel.Project),
        new("team.subsystem.role.deactivate", "Deaktivovat roli subsystému", "TEAM", PermissionScopeLevel.Project),
        new("team.candidates.search", "Hledat kandidáty do týmu", "TEAM", PermissionScopeLevel.Project),

        // ==== 8. Osoby ====
        new("people.create", "Přidat osobu", "PEOPLE", PermissionScopeLevel.Global),
        new("people.edit", "Upravit osobu", "PEOPLE", PermissionScopeLevel.Global),
        new("people.delete", "Smazat osobu", "PEOPLE", PermissionScopeLevel.Global),
        new("people.ad.search", "Hledat v Active Directory", "PEOPLE", PermissionScopeLevel.Global),
        new("people.ad.sync", "Synchronizovat osobu z AD", "PEOPLE", PermissionScopeLevel.Global),

        // ==== 9. Číselníky ====
        new("ciselniky.row.edit", "Upravit řádek číselníku", "CISELNIKY", PermissionScopeLevel.Global),
        new("ciselniky.row.delete", "Smazat řádek číselníku", "CISELNIKY", PermissionScopeLevel.Global),

        // ==== 10. Výzvy ====
        new("vyzvy.create", "Založit výzvu", "VYZVY", PermissionScopeLevel.Project),
        new("vyzvy.state.change", "Změnit stav výzvy", "VYZVY", PermissionScopeLevel.Project),
        new("vyzvy.pnf.assign", "Zařadit PNF", "VYZVY", PermissionScopeLevel.Project),
        new("vyzvy.pnf.reassign", "Přeřadit PNF", "VYZVY", PermissionScopeLevel.Project),
        new("vyzvy.word.export", "Word export výzvy", "VYZVY", PermissionScopeLevel.Project),

        // ==== 11. Dashboard ====
        new("dashboard.view", "Otevřít projektový dashboard", "DASHBOARD", PermissionScopeLevel.Project),
        new("dashboard.records.view", "Záložka Záznamy", "DASHBOARD", PermissionScopeLevel.Project),
        new("dashboard.nes.view", "Záložka NES v prodlení", "DASHBOARD", PermissionScopeLevel.Project),
        new("dashboard.statistics.view", "Záložka Statistiky", "DASHBOARD", PermissionScopeLevel.Project),
        new("dashboard.vyzvy.view", "Záložka Výzvy", "DASHBOARD", PermissionScopeLevel.Project),

        // ==== 12. Export ====
        new("export.pdf.projekt", "PDF export projektu", "EXPORT", PermissionScopeLevel.Project),
        new("export.pdf.jednani", "PDF export jednání", "EXPORT", PermissionScopeLevel.Project),
        new("export.pdf.ukol", "PDF export úkolu", "EXPORT", PermissionScopeLevel.Project),
        new("export.word.projekt", "Word export projektu", "EXPORT", PermissionScopeLevel.Project),
        new("export.word.jednani", "Word export jednání", "EXPORT", PermissionScopeLevel.Project),
        new("export.word.ukol", "Word export úkolu", "EXPORT", PermissionScopeLevel.Project),

        // ==== 13. Nastavení ====
        new("settings.view", "Zobrazit nastavení", "SETTINGS", PermissionScopeLevel.Global),
        new("settings.roles.assign", "Přiřadit globální roli", "SETTINGS", PermissionScopeLevel.Global),
        new("settings.sync.configure", "Konfigurace sync jobu", "SETTINGS", PermissionScopeLevel.Global),
        new("settings.sync.run", "Spustit sync job", "SETTINGS", PermissionScopeLevel.Global),
        new("settings.sd.view", "SD konektor (admin přehled)", "SETTINGS", PermissionScopeLevel.Global),

        // ==== 14. Hledání ====
        new("search.index", "Fulltext hledání", "SEARCH", PermissionScopeLevel.Global),
        new("search.reindex", "Spustit reindex", "SEARCH", PermissionScopeLevel.Global),

        // ==== 15. Harmonogram preview ====
        new("schedule.preview", "Náhledový přepočet harmonogramu", "SCHEDULE", PermissionScopeLevel.Project),

        // ==== DEPRECATED — ponecháno pro kompatibilitu během Fází 1–6 ====
        new("records.schedule.add", "Doplňovat harmonogram úkolu (deprecated)", "RECORDS", PermissionScopeLevel.Project),
        new("records.comment.subsystemlead", "Vyjádření vedoucího subsystému (deprecated)", "RECORDS", PermissionScopeLevel.Project),
        new("team.manage", "Správa týmu (deprecated)", "TEAM", PermissionScopeLevel.Project),
        new("people.manage", "Správa osob (deprecated)", "PEOPLE", PermissionScopeLevel.Global),
        new("ciselniky.edit", "Editace číselníků (deprecated)", "CISELNIKY", PermissionScopeLevel.Global),
        new("settings.manage", "Správa nastavení (deprecated)", "SETTINGS", PermissionScopeLevel.Global),
        new("export.pdf", "Export PDF (deprecated)", "EXPORT", PermissionScopeLevel.Project),
        new("export.word", "Export Word (deprecated)", "EXPORT", PermissionScopeLevel.Project)
    ];

    public static readonly IReadOnlyList<RoleActionSeedItem> RoleMappings = BuildRoleMappings();

    private static IReadOnlyList<RoleActionSeedItem> BuildRoleMappings()
    {
        var list = new List<RoleActionSeedItem>(capacity: 500);
        list.AddRange(BuildPerActionMappings());
        list.AddRange(BuildDeprecatedCompatibilityMappings());
        return list;
    }

    // =========================================================================
    // Per-action cílové mappingy (76 klíčů × 11 rolí — transpozice Excel matice).
    // Zdroj pravdy: docs/known-issues/authz-target-matrix.xlsx
    //               (sheet "Role × Permission (target)")
    // =========================================================================
    private static IEnumerable<RoleActionSeedItem> BuildPerActionMappings() =>
    [
        // --- SUPERADMIN (76/76) ---
        new("SUPERADMIN", "projects.read.all", ScopeMode.All, true),
        new("SUPERADMIN", "projects.create", ScopeMode.All, true),
        new("SUPERADMIN", "projects.edit", ScopeMode.All, true),
        new("SUPERADMIN", "projects.delete", ScopeMode.All, true),
        new("SUPERADMIN", "records.create", ScopeMode.All, true),
        new("SUPERADMIN", "records.edit", ScopeMode.All, true),
        new("SUPERADMIN", "records.delete", ScopeMode.All, true),
        new("SUPERADMIN", "records.schedule.edit", ScopeMode.All, true),
        new("SUPERADMIN", "records.assign.meeting", ScopeMode.All, true),
        new("SUPERADMIN", "comments.add", ScopeMode.All, true),
        new("SUPERADMIN", "comments.edit.own", ScopeMode.All, true),
        new("SUPERADMIN", "comments.edit.any", ScopeMode.All, true),
        new("SUPERADMIN", "comments.delete.own", ScopeMode.All, true),
        new("SUPERADMIN", "comments.delete.any", ScopeMode.All, true),
        new("SUPERADMIN", "meetings.create", ScopeMode.All, true),
        new("SUPERADMIN", "meetings.edit", ScopeMode.All, true),
        new("SUPERADMIN", "meetings.delete", ScopeMode.All, true),
        new("SUPERADMIN", "meetings.status.change", ScopeMode.All, true),
        new("SUPERADMIN", "meetings.notes.edit", ScopeMode.All, true),
        new("SUPERADMIN", "meetings.notes.subsystemlead", ScopeMode.All, true),
        new("SUPERADMIN", "meetings.attendance.edit", ScopeMode.All, true),
        new("SUPERADMIN", "meetings.participant.add", ScopeMode.All, true),
        new("SUPERADMIN", "proposals.record.create", ScopeMode.All, true),
        new("SUPERADMIN", "proposals.schedule.create", ScopeMode.All, true),
        new("SUPERADMIN", "proposals.edit.own", ScopeMode.All, true),
        new("SUPERADMIN", "proposals.edit.any", ScopeMode.All, true),
        new("SUPERADMIN", "proposals.accept", ScopeMode.All, true),
        new("SUPERADMIN", "proposals.reject", ScopeMode.All, true),
        new("SUPERADMIN", "proposals.takeover", ScopeMode.All, true),
        new("SUPERADMIN", "externiodkazy.sync", ScopeMode.All, true),
        new("SUPERADMIN", "vyjadreni.modal.open", ScopeMode.All, true),
        new("SUPERADMIN", "vyjadreni.refresh", ScopeMode.All, true),
        new("SUPERADMIN", "vyjadreni.vazba.create", ScopeMode.All, true),
        new("SUPERADMIN", "vyjadreni.vazba.delete", ScopeMode.All, true),
        new("SUPERADMIN", "vyjadreni.reharvest", ScopeMode.All, true),
        new("SUPERADMIN", "team.member.add", ScopeMode.All, true),
        new("SUPERADMIN", "team.member.remove", ScopeMode.All, true),
        new("SUPERADMIN", "team.role.assign", ScopeMode.All, true),
        new("SUPERADMIN", "team.role.deactivate", ScopeMode.All, true),
        new("SUPERADMIN", "team.subsystem.create", ScopeMode.All, true),
        new("SUPERADMIN", "team.subsystem.reorder", ScopeMode.All, true),
        new("SUPERADMIN", "team.subsystem.deactivate", ScopeMode.All, true),
        new("SUPERADMIN", "team.subsystem.role.assign", ScopeMode.All, true),
        new("SUPERADMIN", "team.subsystem.role.deactivate", ScopeMode.All, true),
        new("SUPERADMIN", "team.candidates.search", ScopeMode.All, true),
        new("SUPERADMIN", "people.create", ScopeMode.All, true),
        new("SUPERADMIN", "people.edit", ScopeMode.All, true),
        new("SUPERADMIN", "people.delete", ScopeMode.All, true),
        new("SUPERADMIN", "people.ad.search", ScopeMode.All, true),
        new("SUPERADMIN", "people.ad.sync", ScopeMode.All, true),
        new("SUPERADMIN", "ciselniky.row.edit", ScopeMode.All, true),
        new("SUPERADMIN", "ciselniky.row.delete", ScopeMode.All, true),
        new("SUPERADMIN", "vyzvy.create", ScopeMode.All, true),
        new("SUPERADMIN", "vyzvy.state.change", ScopeMode.All, true),
        new("SUPERADMIN", "vyzvy.pnf.assign", ScopeMode.All, true),
        new("SUPERADMIN", "vyzvy.pnf.reassign", ScopeMode.All, true),
        new("SUPERADMIN", "vyzvy.word.export", ScopeMode.All, true),
        new("SUPERADMIN", "dashboard.view", ScopeMode.All, true),
        new("SUPERADMIN", "dashboard.records.view", ScopeMode.All, true),
        new("SUPERADMIN", "dashboard.nes.view", ScopeMode.All, true),
        new("SUPERADMIN", "dashboard.statistics.view", ScopeMode.All, true),
        new("SUPERADMIN", "dashboard.vyzvy.view", ScopeMode.All, true),
        new("SUPERADMIN", "export.pdf.projekt", ScopeMode.All, true),
        new("SUPERADMIN", "export.pdf.jednani", ScopeMode.All, true),
        new("SUPERADMIN", "export.pdf.ukol", ScopeMode.All, true),
        new("SUPERADMIN", "export.word.projekt", ScopeMode.All, true),
        new("SUPERADMIN", "export.word.jednani", ScopeMode.All, true),
        new("SUPERADMIN", "export.word.ukol", ScopeMode.All, true),
        new("SUPERADMIN", "settings.view", ScopeMode.All, true),
        new("SUPERADMIN", "settings.roles.assign", ScopeMode.All, true),
        new("SUPERADMIN", "settings.sync.configure", ScopeMode.All, true),
        new("SUPERADMIN", "settings.sync.run", ScopeMode.All, true),
        new("SUPERADMIN", "settings.sd.view", ScopeMode.All, true),
        new("SUPERADMIN", "search.index", ScopeMode.All, true),
        new("SUPERADMIN", "search.reindex", ScopeMode.All, true),
        new("SUPERADMIN", "schedule.preview", ScopeMode.All, true),

        // --- APP_ADMIN (76/76 — = SUPERADMIN na úrovni permission modelu) ---
        new("APP_ADMIN", "projects.read.all", ScopeMode.All, true),
        new("APP_ADMIN", "projects.create", ScopeMode.All, true),
        new("APP_ADMIN", "projects.edit", ScopeMode.All, true),
        new("APP_ADMIN", "projects.delete", ScopeMode.All, true),
        new("APP_ADMIN", "records.create", ScopeMode.All, true),
        new("APP_ADMIN", "records.edit", ScopeMode.All, true),
        new("APP_ADMIN", "records.delete", ScopeMode.All, true),
        new("APP_ADMIN", "records.schedule.edit", ScopeMode.All, true),
        new("APP_ADMIN", "records.assign.meeting", ScopeMode.All, true),
        new("APP_ADMIN", "comments.add", ScopeMode.All, true),
        new("APP_ADMIN", "comments.edit.own", ScopeMode.All, true),
        new("APP_ADMIN", "comments.edit.any", ScopeMode.All, true),
        new("APP_ADMIN", "comments.delete.own", ScopeMode.All, true),
        new("APP_ADMIN", "comments.delete.any", ScopeMode.All, true),
        new("APP_ADMIN", "meetings.create", ScopeMode.All, true),
        new("APP_ADMIN", "meetings.edit", ScopeMode.All, true),
        new("APP_ADMIN", "meetings.delete", ScopeMode.All, true),
        new("APP_ADMIN", "meetings.status.change", ScopeMode.All, true),
        new("APP_ADMIN", "meetings.notes.edit", ScopeMode.All, true),
        new("APP_ADMIN", "meetings.notes.subsystemlead", ScopeMode.All, true),
        new("APP_ADMIN", "meetings.attendance.edit", ScopeMode.All, true),
        new("APP_ADMIN", "meetings.participant.add", ScopeMode.All, true),
        new("APP_ADMIN", "proposals.record.create", ScopeMode.All, true),
        new("APP_ADMIN", "proposals.schedule.create", ScopeMode.All, true),
        new("APP_ADMIN", "proposals.edit.own", ScopeMode.All, true),
        new("APP_ADMIN", "proposals.edit.any", ScopeMode.All, true),
        new("APP_ADMIN", "proposals.accept", ScopeMode.All, true),
        new("APP_ADMIN", "proposals.reject", ScopeMode.All, true),
        new("APP_ADMIN", "proposals.takeover", ScopeMode.All, true),
        new("APP_ADMIN", "externiodkazy.sync", ScopeMode.All, true),
        new("APP_ADMIN", "vyjadreni.modal.open", ScopeMode.All, true),
        new("APP_ADMIN", "vyjadreni.refresh", ScopeMode.All, true),
        new("APP_ADMIN", "vyjadreni.vazba.create", ScopeMode.All, true),
        new("APP_ADMIN", "vyjadreni.vazba.delete", ScopeMode.All, true),
        new("APP_ADMIN", "vyjadreni.reharvest", ScopeMode.All, true),
        new("APP_ADMIN", "team.member.add", ScopeMode.All, true),
        new("APP_ADMIN", "team.member.remove", ScopeMode.All, true),
        new("APP_ADMIN", "team.role.assign", ScopeMode.All, true),
        new("APP_ADMIN", "team.role.deactivate", ScopeMode.All, true),
        new("APP_ADMIN", "team.subsystem.create", ScopeMode.All, true),
        new("APP_ADMIN", "team.subsystem.reorder", ScopeMode.All, true),
        new("APP_ADMIN", "team.subsystem.deactivate", ScopeMode.All, true),
        new("APP_ADMIN", "team.subsystem.role.assign", ScopeMode.All, true),
        new("APP_ADMIN", "team.subsystem.role.deactivate", ScopeMode.All, true),
        new("APP_ADMIN", "team.candidates.search", ScopeMode.All, true),
        new("APP_ADMIN", "people.create", ScopeMode.All, true),
        new("APP_ADMIN", "people.edit", ScopeMode.All, true),
        new("APP_ADMIN", "people.delete", ScopeMode.All, true),
        new("APP_ADMIN", "people.ad.search", ScopeMode.All, true),
        new("APP_ADMIN", "people.ad.sync", ScopeMode.All, true),
        new("APP_ADMIN", "ciselniky.row.edit", ScopeMode.All, true),
        new("APP_ADMIN", "ciselniky.row.delete", ScopeMode.All, true),
        new("APP_ADMIN", "vyzvy.create", ScopeMode.All, true),
        new("APP_ADMIN", "vyzvy.state.change", ScopeMode.All, true),
        new("APP_ADMIN", "vyzvy.pnf.assign", ScopeMode.All, true),
        new("APP_ADMIN", "vyzvy.pnf.reassign", ScopeMode.All, true),
        new("APP_ADMIN", "vyzvy.word.export", ScopeMode.All, true),
        new("APP_ADMIN", "dashboard.view", ScopeMode.All, true),
        new("APP_ADMIN", "dashboard.records.view", ScopeMode.All, true),
        new("APP_ADMIN", "dashboard.nes.view", ScopeMode.All, true),
        new("APP_ADMIN", "dashboard.statistics.view", ScopeMode.All, true),
        new("APP_ADMIN", "dashboard.vyzvy.view", ScopeMode.All, true),
        new("APP_ADMIN", "export.pdf.projekt", ScopeMode.All, true),
        new("APP_ADMIN", "export.pdf.jednani", ScopeMode.All, true),
        new("APP_ADMIN", "export.pdf.ukol", ScopeMode.All, true),
        new("APP_ADMIN", "export.word.projekt", ScopeMode.All, true),
        new("APP_ADMIN", "export.word.jednani", ScopeMode.All, true),
        new("APP_ADMIN", "export.word.ukol", ScopeMode.All, true),
        new("APP_ADMIN", "settings.view", ScopeMode.All, true),
        new("APP_ADMIN", "settings.roles.assign", ScopeMode.All, true),
        new("APP_ADMIN", "settings.sync.configure", ScopeMode.All, true),
        new("APP_ADMIN", "settings.sync.run", ScopeMode.All, true),
        new("APP_ADMIN", "settings.sd.view", ScopeMode.All, true),
        new("APP_ADMIN", "search.index", ScopeMode.All, true),
        new("APP_ADMIN", "search.reindex", ScopeMode.All, true),
        new("APP_ADMIN", "schedule.preview", ScopeMode.All, true),

        // --- READ_ALL (13 klíčů — management visibility) ---
        new("READ_ALL", "projects.read.all", ScopeMode.All, true),
        new("READ_ALL", "dashboard.view", ScopeMode.All, true),
        new("READ_ALL", "dashboard.records.view", ScopeMode.All, true),
        new("READ_ALL", "dashboard.nes.view", ScopeMode.All, true),
        new("READ_ALL", "dashboard.statistics.view", ScopeMode.All, true),
        new("READ_ALL", "dashboard.vyzvy.view", ScopeMode.All, true),
        new("READ_ALL", "export.pdf.projekt", ScopeMode.All, true),
        new("READ_ALL", "export.pdf.jednani", ScopeMode.All, true),
        new("READ_ALL", "export.pdf.ukol", ScopeMode.All, true),
        new("READ_ALL", "export.word.projekt", ScopeMode.All, true),
        new("READ_ALL", "export.word.jednani", ScopeMode.All, true),
        new("READ_ALL", "export.word.ukol", ScopeMode.All, true),
        new("READ_ALL", "search.index", ScopeMode.All, true),

        // --- VLASTNIK_PROJEKTU (59 klíčů — plný vlastník projektového obsahu; bez project metadata) ---
        new("VLASTNIK_PROJEKTU", "records.create", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "records.edit", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "records.delete", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "records.schedule.edit", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "records.assign.meeting", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "comments.add", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "comments.edit.own", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "comments.edit.any", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "comments.delete.own", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "comments.delete.any", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "meetings.create", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "meetings.edit", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "meetings.delete", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "meetings.status.change", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "meetings.notes.edit", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "meetings.notes.subsystemlead", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "meetings.attendance.edit", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "meetings.participant.add", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "proposals.record.create", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "proposals.schedule.create", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "proposals.edit.own", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "proposals.edit.any", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "proposals.accept", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "proposals.reject", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "proposals.takeover", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "externiodkazy.sync", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "vyjadreni.modal.open", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "vyjadreni.refresh", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "vyjadreni.vazba.create", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "vyjadreni.vazba.delete", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "vyjadreni.reharvest", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "team.member.add", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "team.member.remove", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "team.role.assign", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "team.role.deactivate", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "team.subsystem.create", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "team.subsystem.reorder", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "team.subsystem.deactivate", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "team.subsystem.role.assign", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "team.subsystem.role.deactivate", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "team.candidates.search", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "vyzvy.create", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "vyzvy.state.change", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "vyzvy.pnf.assign", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "vyzvy.pnf.reassign", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "vyzvy.word.export", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "dashboard.view", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "dashboard.records.view", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "dashboard.nes.view", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "dashboard.statistics.view", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "dashboard.vyzvy.view", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "export.pdf.projekt", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "export.pdf.jednani", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "export.pdf.ukol", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "export.word.projekt", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "export.word.jednani", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "export.word.ukol", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "search.index", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "schedule.preview", ScopeMode.All, true),

        // --- ADM_PROJ (59 klíčů — = VLASTNIK_PROJEKTU pro obsah projektu) ---
        new("ADM_PROJ", "records.create", ScopeMode.All, true),
        new("ADM_PROJ", "records.edit", ScopeMode.All, true),
        new("ADM_PROJ", "records.delete", ScopeMode.All, true),
        new("ADM_PROJ", "records.schedule.edit", ScopeMode.All, true),
        new("ADM_PROJ", "records.assign.meeting", ScopeMode.All, true),
        new("ADM_PROJ", "comments.add", ScopeMode.All, true),
        new("ADM_PROJ", "comments.edit.own", ScopeMode.All, true),
        new("ADM_PROJ", "comments.edit.any", ScopeMode.All, true),
        new("ADM_PROJ", "comments.delete.own", ScopeMode.All, true),
        new("ADM_PROJ", "comments.delete.any", ScopeMode.All, true),
        new("ADM_PROJ", "meetings.create", ScopeMode.All, true),
        new("ADM_PROJ", "meetings.edit", ScopeMode.All, true),
        new("ADM_PROJ", "meetings.delete", ScopeMode.All, true),
        new("ADM_PROJ", "meetings.status.change", ScopeMode.All, true),
        new("ADM_PROJ", "meetings.notes.edit", ScopeMode.All, true),
        new("ADM_PROJ", "meetings.notes.subsystemlead", ScopeMode.All, true),
        new("ADM_PROJ", "meetings.attendance.edit", ScopeMode.All, true),
        new("ADM_PROJ", "meetings.participant.add", ScopeMode.All, true),
        new("ADM_PROJ", "proposals.record.create", ScopeMode.All, true),
        new("ADM_PROJ", "proposals.schedule.create", ScopeMode.All, true),
        new("ADM_PROJ", "proposals.edit.own", ScopeMode.All, true),
        new("ADM_PROJ", "proposals.edit.any", ScopeMode.All, true),
        new("ADM_PROJ", "proposals.accept", ScopeMode.All, true),
        new("ADM_PROJ", "proposals.reject", ScopeMode.All, true),
        new("ADM_PROJ", "proposals.takeover", ScopeMode.All, true),
        new("ADM_PROJ", "externiodkazy.sync", ScopeMode.All, true),
        new("ADM_PROJ", "vyjadreni.modal.open", ScopeMode.All, true),
        new("ADM_PROJ", "vyjadreni.refresh", ScopeMode.All, true),
        new("ADM_PROJ", "vyjadreni.vazba.create", ScopeMode.All, true),
        new("ADM_PROJ", "vyjadreni.vazba.delete", ScopeMode.All, true),
        new("ADM_PROJ", "vyjadreni.reharvest", ScopeMode.All, true),
        new("ADM_PROJ", "team.member.add", ScopeMode.All, true),
        new("ADM_PROJ", "team.member.remove", ScopeMode.All, true),
        new("ADM_PROJ", "team.role.assign", ScopeMode.All, true),
        new("ADM_PROJ", "team.role.deactivate", ScopeMode.All, true),
        new("ADM_PROJ", "team.subsystem.create", ScopeMode.All, true),
        new("ADM_PROJ", "team.subsystem.reorder", ScopeMode.All, true),
        new("ADM_PROJ", "team.subsystem.deactivate", ScopeMode.All, true),
        new("ADM_PROJ", "team.subsystem.role.assign", ScopeMode.All, true),
        new("ADM_PROJ", "team.subsystem.role.deactivate", ScopeMode.All, true),
        new("ADM_PROJ", "team.candidates.search", ScopeMode.All, true),
        new("ADM_PROJ", "vyzvy.create", ScopeMode.All, true),
        new("ADM_PROJ", "vyzvy.state.change", ScopeMode.All, true),
        new("ADM_PROJ", "vyzvy.pnf.assign", ScopeMode.All, true),
        new("ADM_PROJ", "vyzvy.pnf.reassign", ScopeMode.All, true),
        new("ADM_PROJ", "vyzvy.word.export", ScopeMode.All, true),
        new("ADM_PROJ", "dashboard.view", ScopeMode.All, true),
        new("ADM_PROJ", "dashboard.records.view", ScopeMode.All, true),
        new("ADM_PROJ", "dashboard.nes.view", ScopeMode.All, true),
        new("ADM_PROJ", "dashboard.statistics.view", ScopeMode.All, true),
        new("ADM_PROJ", "dashboard.vyzvy.view", ScopeMode.All, true),
        new("ADM_PROJ", "export.pdf.projekt", ScopeMode.All, true),
        new("ADM_PROJ", "export.pdf.jednani", ScopeMode.All, true),
        new("ADM_PROJ", "export.pdf.ukol", ScopeMode.All, true),
        new("ADM_PROJ", "export.word.projekt", ScopeMode.All, true),
        new("ADM_PROJ", "export.word.jednani", ScopeMode.All, true),
        new("ADM_PROJ", "export.word.ukol", ScopeMode.All, true),
        new("ADM_PROJ", "search.index", ScopeMode.All, true),
        new("ADM_PROJ", "schedule.preview", ScopeMode.All, true),

        // --- PROJ_MAN (59 klíčů — = ADM_PROJ pro obsah projektu) ---
        new("PROJ_MAN", "records.create", ScopeMode.All, true),
        new("PROJ_MAN", "records.edit", ScopeMode.All, true),
        new("PROJ_MAN", "records.delete", ScopeMode.All, true),
        new("PROJ_MAN", "records.schedule.edit", ScopeMode.All, true),
        new("PROJ_MAN", "records.assign.meeting", ScopeMode.All, true),
        new("PROJ_MAN", "comments.add", ScopeMode.All, true),
        new("PROJ_MAN", "comments.edit.own", ScopeMode.All, true),
        new("PROJ_MAN", "comments.edit.any", ScopeMode.All, true),
        new("PROJ_MAN", "comments.delete.own", ScopeMode.All, true),
        new("PROJ_MAN", "comments.delete.any", ScopeMode.All, true),
        new("PROJ_MAN", "meetings.create", ScopeMode.All, true),
        new("PROJ_MAN", "meetings.edit", ScopeMode.All, true),
        new("PROJ_MAN", "meetings.delete", ScopeMode.All, true),
        new("PROJ_MAN", "meetings.status.change", ScopeMode.All, true),
        new("PROJ_MAN", "meetings.notes.edit", ScopeMode.All, true),
        new("PROJ_MAN", "meetings.notes.subsystemlead", ScopeMode.All, true),
        new("PROJ_MAN", "meetings.attendance.edit", ScopeMode.All, true),
        new("PROJ_MAN", "meetings.participant.add", ScopeMode.All, true),
        new("PROJ_MAN", "proposals.record.create", ScopeMode.All, true),
        new("PROJ_MAN", "proposals.schedule.create", ScopeMode.All, true),
        new("PROJ_MAN", "proposals.edit.own", ScopeMode.All, true),
        new("PROJ_MAN", "proposals.edit.any", ScopeMode.All, true),
        new("PROJ_MAN", "proposals.accept", ScopeMode.All, true),
        new("PROJ_MAN", "proposals.reject", ScopeMode.All, true),
        new("PROJ_MAN", "proposals.takeover", ScopeMode.All, true),
        new("PROJ_MAN", "externiodkazy.sync", ScopeMode.All, true),
        new("PROJ_MAN", "vyjadreni.modal.open", ScopeMode.All, true),
        new("PROJ_MAN", "vyjadreni.refresh", ScopeMode.All, true),
        new("PROJ_MAN", "vyjadreni.vazba.create", ScopeMode.All, true),
        new("PROJ_MAN", "vyjadreni.vazba.delete", ScopeMode.All, true),
        new("PROJ_MAN", "vyjadreni.reharvest", ScopeMode.All, true),
        new("PROJ_MAN", "team.member.add", ScopeMode.All, true),
        new("PROJ_MAN", "team.member.remove", ScopeMode.All, true),
        new("PROJ_MAN", "team.role.assign", ScopeMode.All, true),
        new("PROJ_MAN", "team.role.deactivate", ScopeMode.All, true),
        new("PROJ_MAN", "team.subsystem.create", ScopeMode.All, true),
        new("PROJ_MAN", "team.subsystem.reorder", ScopeMode.All, true),
        new("PROJ_MAN", "team.subsystem.deactivate", ScopeMode.All, true),
        new("PROJ_MAN", "team.subsystem.role.assign", ScopeMode.All, true),
        new("PROJ_MAN", "team.subsystem.role.deactivate", ScopeMode.All, true),
        new("PROJ_MAN", "team.candidates.search", ScopeMode.All, true),
        new("PROJ_MAN", "vyzvy.create", ScopeMode.All, true),
        new("PROJ_MAN", "vyzvy.state.change", ScopeMode.All, true),
        new("PROJ_MAN", "vyzvy.pnf.assign", ScopeMode.All, true),
        new("PROJ_MAN", "vyzvy.pnf.reassign", ScopeMode.All, true),
        new("PROJ_MAN", "vyzvy.word.export", ScopeMode.All, true),
        new("PROJ_MAN", "dashboard.view", ScopeMode.All, true),
        new("PROJ_MAN", "dashboard.records.view", ScopeMode.All, true),
        new("PROJ_MAN", "dashboard.nes.view", ScopeMode.All, true),
        new("PROJ_MAN", "dashboard.statistics.view", ScopeMode.All, true),
        new("PROJ_MAN", "dashboard.vyzvy.view", ScopeMode.All, true),
        new("PROJ_MAN", "export.pdf.projekt", ScopeMode.All, true),
        new("PROJ_MAN", "export.pdf.jednani", ScopeMode.All, true),
        new("PROJ_MAN", "export.pdf.ukol", ScopeMode.All, true),
        new("PROJ_MAN", "export.word.projekt", ScopeMode.All, true),
        new("PROJ_MAN", "export.word.jednani", ScopeMode.All, true),
        new("PROJ_MAN", "export.word.ukol", ScopeMode.All, true),
        new("PROJ_MAN", "search.index", ScopeMode.All, true),
        new("PROJ_MAN", "schedule.preview", ScopeMode.All, true),

        // --- GEST (15 klíčů — komentátor + read-only) ---
        new("GEST", "comments.add", ScopeMode.All, true),
        new("GEST", "comments.edit.own", ScopeMode.All, true),
        new("GEST", "comments.delete.own", ScopeMode.All, true),
        new("GEST", "dashboard.view", ScopeMode.All, true),
        new("GEST", "dashboard.records.view", ScopeMode.All, true),
        new("GEST", "dashboard.nes.view", ScopeMode.All, true),
        new("GEST", "dashboard.statistics.view", ScopeMode.All, true),
        new("GEST", "dashboard.vyzvy.view", ScopeMode.All, true),
        new("GEST", "export.pdf.projekt", ScopeMode.All, true),
        new("GEST", "export.pdf.jednani", ScopeMode.All, true),
        new("GEST", "export.pdf.ukol", ScopeMode.All, true),
        new("GEST", "export.word.projekt", ScopeMode.All, true),
        new("GEST", "export.word.jednani", ScopeMode.All, true),
        new("GEST", "export.word.ukol", ScopeMode.All, true),
        new("GEST", "search.index", ScopeMode.All, true),

        // --- HOST (12 klíčů — read-only pozorovatel) ---
        new("HOST", "dashboard.view", ScopeMode.All, true),
        new("HOST", "dashboard.records.view", ScopeMode.All, true),
        new("HOST", "dashboard.nes.view", ScopeMode.All, true),
        new("HOST", "dashboard.statistics.view", ScopeMode.All, true),
        new("HOST", "dashboard.vyzvy.view", ScopeMode.All, true),
        new("HOST", "export.pdf.projekt", ScopeMode.All, true),
        new("HOST", "export.pdf.jednani", ScopeMode.All, true),
        new("HOST", "export.pdf.ukol", ScopeMode.All, true),
        new("HOST", "export.word.projekt", ScopeMode.All, true),
        new("HOST", "export.word.jednani", ScopeMode.All, true),
        new("HOST", "export.word.ukol", ScopeMode.All, true),
        new("HOST", "search.index", ScopeMode.All, true),

        // --- VEDOUCI_SUBSYSTEMU (14 klíčů — subsystémový lead) ---
        new("VEDOUCI_SUBSYSTEMU", "comments.add", ScopeMode.All, true),
        new("VEDOUCI_SUBSYSTEMU", "comments.edit.own", ScopeMode.All, true),
        new("VEDOUCI_SUBSYSTEMU", "comments.delete.own", ScopeMode.All, true),
        new("VEDOUCI_SUBSYSTEMU", "meetings.notes.subsystemlead", ScopeMode.All, true),
        new("VEDOUCI_SUBSYSTEMU", "proposals.record.create", ScopeMode.All, true),
        new("VEDOUCI_SUBSYSTEMU", "proposals.schedule.create", ScopeMode.All, true),
        new("VEDOUCI_SUBSYSTEMU", "proposals.edit.own", ScopeMode.All, true),
        new("VEDOUCI_SUBSYSTEMU", "dashboard.view", ScopeMode.All, true),
        new("VEDOUCI_SUBSYSTEMU", "dashboard.records.view", ScopeMode.All, true),
        new("VEDOUCI_SUBSYSTEMU", "dashboard.nes.view", ScopeMode.All, true),
        new("VEDOUCI_SUBSYSTEMU", "dashboard.statistics.view", ScopeMode.All, true),
        new("VEDOUCI_SUBSYSTEMU", "dashboard.vyzvy.view", ScopeMode.All, true),
        new("VEDOUCI_SUBSYSTEMU", "search.index", ScopeMode.All, true),
        new("VEDOUCI_SUBSYSTEMU", "schedule.preview", ScopeMode.All, true),

        // --- ZASTUPCE_VEDOUCIHO_SUBSYSTEMU (14 klíčů — = VEDOUCI) ---
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "comments.add", ScopeMode.All, true),
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "comments.edit.own", ScopeMode.All, true),
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "comments.delete.own", ScopeMode.All, true),
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "meetings.notes.subsystemlead", ScopeMode.All, true),
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "proposals.record.create", ScopeMode.All, true),
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "proposals.schedule.create", ScopeMode.All, true),
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "proposals.edit.own", ScopeMode.All, true),
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "dashboard.view", ScopeMode.All, true),
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "dashboard.records.view", ScopeMode.All, true),
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "dashboard.nes.view", ScopeMode.All, true),
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "dashboard.statistics.view", ScopeMode.All, true),
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "dashboard.vyzvy.view", ScopeMode.All, true),
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "search.index", ScopeMode.All, true),
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "schedule.preview", ScopeMode.All, true),

        // --- METODIK_SUBSYSTEMU (9 klíčů — komentátor + read) ---
        new("METODIK_SUBSYSTEMU", "comments.add", ScopeMode.All, true),
        new("METODIK_SUBSYSTEMU", "comments.edit.own", ScopeMode.All, true),
        new("METODIK_SUBSYSTEMU", "comments.delete.own", ScopeMode.All, true),
        new("METODIK_SUBSYSTEMU", "dashboard.view", ScopeMode.All, true),
        new("METODIK_SUBSYSTEMU", "dashboard.records.view", ScopeMode.All, true),
        new("METODIK_SUBSYSTEMU", "dashboard.nes.view", ScopeMode.All, true),
        new("METODIK_SUBSYSTEMU", "dashboard.statistics.view", ScopeMode.All, true),
        new("METODIK_SUBSYSTEMU", "dashboard.vyzvy.view", ScopeMode.All, true),
        new("METODIK_SUBSYSTEMU", "search.index", ScopeMode.All, true)
    ];

    // =========================================================================
    // DEPRECATED mappings — zachovány pro kompat. v kódu během F1–F6.
    // V F7 (DB migrace) se smažou společně s Actions entries.
    // Každý deprecated klíč má stejnou sadu rolí jako před redesignem.
    // =========================================================================
    private static IEnumerable<RoleActionSeedItem> BuildDeprecatedCompatibilityMappings() =>
    [
        // records.schedule.add
        new("SUPERADMIN", "records.schedule.add", ScopeMode.All, true),
        new("APP_ADMIN", "records.schedule.add", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "records.schedule.add", ScopeMode.All, true),
        new("ADM_PROJ", "records.schedule.add", ScopeMode.All, true),
        new("PROJ_MAN", "records.schedule.add", ScopeMode.All, true),

        // records.comment.subsystemlead
        new("SUPERADMIN", "records.comment.subsystemlead", ScopeMode.All, true),
        new("APP_ADMIN", "records.comment.subsystemlead", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "records.comment.subsystemlead", ScopeMode.All, true),
        new("ADM_PROJ", "records.comment.subsystemlead", ScopeMode.All, true),
        new("PROJ_MAN", "records.comment.subsystemlead", ScopeMode.All, true),
        new("GEST", "records.comment.subsystemlead", ScopeMode.All, true),
        new("VEDOUCI_SUBSYSTEMU", "records.comment.subsystemlead", ScopeMode.All, true),
        new("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU", "records.comment.subsystemlead", ScopeMode.All, true),

        // team.manage
        new("SUPERADMIN", "team.manage", ScopeMode.All, true),
        new("APP_ADMIN", "team.manage", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "team.manage", ScopeMode.All, true),
        new("ADM_PROJ", "team.manage", ScopeMode.All, true),
        new("PROJ_MAN", "team.manage", ScopeMode.All, true),

        // people.manage
        new("SUPERADMIN", "people.manage", ScopeMode.All, true),
        new("APP_ADMIN", "people.manage", ScopeMode.All, true),

        // ciselniky.edit
        new("SUPERADMIN", "ciselniky.edit", ScopeMode.All, true),
        new("APP_ADMIN", "ciselniky.edit", ScopeMode.All, true),

        // settings.manage — APP_ADMIN = SUPERADMIN po per-action redesignu
        new("SUPERADMIN", "settings.manage", ScopeMode.All, true),
        new("APP_ADMIN", "settings.manage", ScopeMode.All, true),

        // export.pdf — APP_ADMIN = SUPERADMIN (kompletní přístup k exportu)
        new("SUPERADMIN", "export.pdf", ScopeMode.All, true),
        new("APP_ADMIN", "export.pdf", ScopeMode.All, true),
        new("READ_ALL", "export.pdf", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "export.pdf", ScopeMode.All, true),
        new("ADM_PROJ", "export.pdf", ScopeMode.All, true),
        new("PROJ_MAN", "export.pdf", ScopeMode.All, true),
        new("GEST", "export.pdf", ScopeMode.All, true),
        new("HOST", "export.pdf", ScopeMode.All, true),

        // export.word — APP_ADMIN = SUPERADMIN
        new("SUPERADMIN", "export.word", ScopeMode.All, true),
        new("APP_ADMIN", "export.word", ScopeMode.All, true),
        new("READ_ALL", "export.word", ScopeMode.All, true),
        new("VLASTNIK_PROJEKTU", "export.word", ScopeMode.All, true),
        new("ADM_PROJ", "export.word", ScopeMode.All, true),
        new("PROJ_MAN", "export.word", ScopeMode.All, true),
        new("GEST", "export.word", ScopeMode.All, true),
        new("HOST", "export.word", ScopeMode.All, true)
    ];
}
