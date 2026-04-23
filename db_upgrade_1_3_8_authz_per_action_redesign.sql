-- =============================================================================
-- db_upgrade_1_3_8_authz_per_action_redesign.sql
--
-- Per-action authz redesign 2026-04-23 — smaže z authz.* tabulek 8 deprecated
-- klíčů nahrazených per-action variantami.
--
-- ROZSAH MIGRACE (BEZPEČNOSTNÍ INVARIANT — viz zadání v CLAUDE.md / memory):
--   * Smaže záznamy POUZE z:
--       - authz.role_permission_projects
--       - authz.role_permissions
--       - authz.permissions
--     a to STRIKTNĚ FILTROVANÉ podle seznamu 8 deprecated klíčů.
--   * NEDOTKNE se:
--       - authz.user_roles    (přiřazení rolí uživatelům)
--       - authz.roles         (katalog rolí)
--       - authz.permission_categories
--       - authz.audit_log
--       - žádných business tabulek (osoby, projekty, projektove_zaznamy, obsazeni_projektu,
--         jednani, vyjadreni, historie_*, apod.)
--
-- DEPRECATED KLÍČE (pre-redesign), které migrace odstraní:
--   1. records.schedule.add            → nahrazeno: proposals.schedule.create
--   2. records.comment.subsystemlead   → nahrazeno: proposals.record.create + meetings.notes.subsystemlead
--   3. team.manage                     → nahrazeno: team.member.add/.remove / team.role.assign/.deactivate
--                                                   / team.subsystem.create/.reorder/.deactivate
--                                                   / team.subsystem.role.assign/.deactivate
--                                                   / team.candidates.search
--   4. people.manage                   → nahrazeno: people.create/.edit/.delete/.ad.search/.ad.sync
--   5. ciselniky.edit                  → nahrazeno: ciselniky.row.edit / ciselniky.row.delete
--   6. settings.manage                 → nahrazeno: settings.roles.assign / .sync.configure / .sync.run / .sd.view
--   7. export.pdf                      → nahrazeno: export.pdf.projekt / .jednani / .ukol
--   8. export.word                     → nahrazeno: export.word.projekt / .jednani / .ukol
--
-- Nové per-action klíče jsou automaticky doseedovány aplikačním
-- PermissionSeederem při spuštění aplikace (idempotentní UPSERT z
-- PermissionSeedConfiguration.Actions).
--
-- Idempotence: skript lze spustit opakovaně — DELETE s NOT EXISTS predikátem
-- projde bez chyby i po úspěšné předchozí migraci.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

PRINT N'[1.3.8] Per-action authz redesign — start';

DECLARE @DeprecatedKeys TABLE (klic NVARCHAR(150) NOT NULL PRIMARY KEY);
INSERT INTO @DeprecatedKeys (klic) VALUES
    (N'records.schedule.add'),
    (N'records.comment.subsystemlead'),
    (N'team.manage'),
    (N'people.manage'),
    (N'ciselniky.edit'),
    (N'settings.manage'),
    (N'export.pdf'),
    (N'export.word');

-- Audit pre-state: kolik řádků dotčeno.
DECLARE @PermissionsCount INT = (
    SELECT COUNT(*) FROM authz.permissions p
    INNER JOIN @DeprecatedKeys d ON d.klic = p.klic
);
DECLARE @RolePermissionsCount INT = (
    SELECT COUNT(*) FROM authz.role_permissions rp
    INNER JOIN authz.permissions p ON p.id = rp.permission_id
    INNER JOIN @DeprecatedKeys d ON d.klic = p.klic
);
DECLARE @RolePermissionProjectsCount INT = (
    SELECT COUNT(*) FROM authz.role_permission_projects rpp
    INNER JOIN authz.role_permissions rp ON rp.id = rpp.role_permission_id
    INNER JOIN authz.permissions p ON p.id = rp.permission_id
    INNER JOIN @DeprecatedKeys d ON d.klic = p.klic
);

PRINT N'[1.3.8] Pre-state:';
PRINT N'[1.3.8]   authz.permissions řádků k smazání: ' + CAST(@PermissionsCount AS NVARCHAR(16));
PRINT N'[1.3.8]   authz.role_permissions řádků k smazání: ' + CAST(@RolePermissionsCount AS NVARCHAR(16));
PRINT N'[1.3.8]   authz.role_permission_projects řádků k smazání: ' + CAST(@RolePermissionProjectsCount AS NVARCHAR(16));

-- 1) role_permission_projects (scope INCLUDE → konkrétní projekty) — mazat jako první
--    kvůli FK na role_permissions.
DELETE rpp
FROM authz.role_permission_projects rpp
INNER JOIN authz.role_permissions rp ON rp.id = rpp.role_permission_id
INNER JOIN authz.permissions p ON p.id = rp.permission_id
INNER JOIN @DeprecatedKeys d ON d.klic = p.klic;

PRINT N'[1.3.8] Smazáno z authz.role_permission_projects: ' + CAST(@@ROWCOUNT AS NVARCHAR(16));

-- 2) role_permissions (vazba role → permission) — pak je FK volná pro permissions.
DELETE rp
FROM authz.role_permissions rp
INNER JOIN authz.permissions p ON p.id = rp.permission_id
INNER JOIN @DeprecatedKeys d ON d.klic = p.klic;

PRINT N'[1.3.8] Smazáno z authz.role_permissions: ' + CAST(@@ROWCOUNT AS NVARCHAR(16));

-- 3) permissions (katalog akcí) — nakonec, když už nejsou FK závislosti.
DELETE p
FROM authz.permissions p
INNER JOIN @DeprecatedKeys d ON d.klic = p.klic;

PRINT N'[1.3.8] Smazáno z authz.permissions: ' + CAST(@@ROWCOUNT AS NVARCHAR(16));

-- Post-state sanity check: 0 řádků pro deprecated klíče.
DECLARE @RemainingPermissions INT = (
    SELECT COUNT(*) FROM authz.permissions p
    INNER JOIN @DeprecatedKeys d ON d.klic = p.klic
);
IF @RemainingPermissions <> 0
BEGIN
    ROLLBACK TRANSACTION;
    RAISERROR(N'[1.3.8] KONZISTENCE: v authz.permissions zbylo %d řádků pro deprecated klíče — migrace přerušena.', 16, 1, @RemainingPermissions);
    RETURN;
END

PRINT N'[1.3.8] Per-action authz redesign — dokončeno.';
PRINT N'[1.3.8] Nové per-action klíče doseedují PermissionSeederem při startu aplikace.';

COMMIT TRANSACTION;

-- =============================================================================
-- ROLLBACK (pokud migraci potřebuješ vrátit):
--
-- 1) Aplikační PermissionSeeder znovu nevytváří deprecated klíče (jsou smazány
--    z PermissionSeedConfiguration.Actions ve fázi F7). Nejprve je nutné vrátit
--    C# kód (revert commit F7) — bez toho by rollback SQL jenom znovu vložil
--    řádky, které aplikace při startu nebude znát.
--
-- 2) Poté obnov deprecated katalog:
--
-- BEGIN TRANSACTION;
-- DECLARE @CatRecId INT = (SELECT id FROM authz.permission_categories WHERE kod = N'RECORDS');
-- DECLARE @CatTeamId INT = (SELECT id FROM authz.permission_categories WHERE kod = N'TEAM');
-- DECLARE @CatPeopleId INT = (SELECT id FROM authz.permission_categories WHERE kod = N'PEOPLE');
-- DECLARE @CatCisId INT = (SELECT id FROM authz.permission_categories WHERE kod = N'CISELNIKY');
-- DECLARE @CatSetId INT = (SELECT id FROM authz.permission_categories WHERE kod = N'SETTINGS');
-- DECLARE @CatExpId INT = (SELECT id FROM authz.permission_categories WHERE kod = N'EXPORT');
--
-- INSERT INTO authz.permissions (klic, nazev, category_id, scope_level, is_active, is_system) VALUES
--   (N'records.schedule.add', N'Doplňovat harmonogram úkolu (deprecated)', @CatRecId, N'PROJECT', 1, 1),
--   (N'records.comment.subsystemlead', N'Vyjádření vedoucího subsystému (deprecated)', @CatRecId, N'PROJECT', 1, 1),
--   (N'team.manage', N'Správa týmu (deprecated)', @CatTeamId, N'PROJECT', 1, 1),
--   (N'people.manage', N'Správa osob (deprecated)', @CatPeopleId, N'GLOBAL', 1, 1),
--   (N'ciselniky.edit', N'Editace číselníků (deprecated)', @CatCisId, N'GLOBAL', 1, 1),
--   (N'settings.manage', N'Správa nastavení (deprecated)', @CatSetId, N'GLOBAL', 1, 1),
--   (N'export.pdf', N'Export PDF (deprecated)', @CatExpId, N'PROJECT', 1, 1),
--   (N'export.word', N'Export Word (deprecated)', @CatExpId, N'PROJECT', 1, 1);
-- -- RoleMappings dohraje PermissionSeeder po revertovaní C# kódu.
-- COMMIT TRANSACTION;
--
-- POZNÁMKA: user_roles / osoby / projekty / záznamy / historie NEBYLY dotčeny
-- touto migrací, takže rollback obnovuje pouze permission katalog — uživatelské
-- přiřazení rolí zůstávají v celistvém stavu.
-- =============================================================================
