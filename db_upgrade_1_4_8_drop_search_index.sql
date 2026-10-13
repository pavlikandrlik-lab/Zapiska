-- =============================================================================
-- db_upgrade_1_4_8_drop_search_index.sql
--
-- Authz úklid 2026-10-13 — smaže z authz.* tabulek zrušený klíč `search.index`
-- a jeho (jednoúčelovou) kategorii SEARCH.
--
-- KONTEXT: Hledání (SearchController) hlídá od verze s touto migrací jen přihlášení.
-- Rozsah výsledků určuje ProjectVisibilityResolver (spec 2026-09-17 §2.3). Klíč byl
-- označen jako globální, ale drželo ho i 8 projektových/subsystémových rolí; policy bez
-- projectId ho kontrolovala jen globálně → hledání vracelo 403 všem kromě globálních rolí.
--
-- POŘADÍ: spustit AŽ PO nasazení verze aplikace bez klíče. Starší binárka by klíč při
-- startu znovu doseedovala (PermissionSeeder = idempotentní UPSERT). Klíč, který v DB
-- zůstane, aplikaci nevadí — nic ho nečte; skript je jen úklid.
--
-- ROZSAH MIGRACE:
--   * Smaže záznamy POUZE z:
--       - authz.role_permission_projects  (scope INCLUDE vazby)
--       - authz.role_permissions          (vazby role -> permission)
--       - authz.permissions               (katalog: klic = 'search.index')
--       - authz.permission_categories     (kod = 'SEARCH', jen pokud osiřelá)
--   * NEDOTKNE se: authz.user_roles, authz.roles, žádných business tabulek.
--
-- Idempotence: skript lze spustit opakovaně.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

PRINT N'[1.4.8] Drop search.index — start';

-- Audit pre-state: které role klíč drží (očekávané: 11 seed rolí).
SELECT r.kod AS role_kod, p.klic
FROM authz.role_permissions rp
INNER JOIN authz.roles r ON r.id = rp.role_id
INNER JOIN authz.permissions p ON p.id = rp.permission_id
WHERE p.klic = N'search.index';

-- 1) role_permission_projects — první kvůli FK na role_permissions.
DELETE rpp
FROM authz.role_permission_projects rpp
INNER JOIN authz.role_permissions rp ON rp.id = rpp.role_permission_id
INNER JOIN authz.permissions p ON p.id = rp.permission_id
WHERE p.klic = N'search.index';

PRINT N'[1.4.8] Smazáno z authz.role_permission_projects: ' + CAST(@@ROWCOUNT AS NVARCHAR(16));

-- 2) role_permissions.
DELETE rp
FROM authz.role_permissions rp
INNER JOIN authz.permissions p ON p.id = rp.permission_id
WHERE p.klic = N'search.index';

PRINT N'[1.4.8] Smazáno z authz.role_permissions: ' + CAST(@@ROWCOUNT AS NVARCHAR(16));

-- 3) permissions.
DELETE FROM authz.permissions WHERE klic = N'search.index';

PRINT N'[1.4.8] Smazáno z authz.permissions: ' + CAST(@@ROWCOUNT AS NVARCHAR(16));

-- 4) Kategorie SEARCH — jen pokud na ni už nic neukazuje (guard proti cizím klíčům).
DELETE c
FROM authz.permission_categories c
WHERE c.kod = N'SEARCH'
  AND NOT EXISTS (SELECT 1 FROM authz.permissions p WHERE p.category_id = c.id);

PRINT N'[1.4.8] Smazáno z authz.permission_categories: ' + CAST(@@ROWCOUNT AS NVARCHAR(16));

-- Sanity check.
IF EXISTS (SELECT 1 FROM authz.permissions WHERE klic = N'search.index')
BEGIN
    ROLLBACK TRANSACTION;
    RAISERROR(N'[1.4.8] KONZISTENCE: search.index stále existuje — migrace přerušena.', 16, 1);
    RETURN;
END

PRINT N'[1.4.8] Drop search.index — dokončeno.';

COMMIT TRANSACTION;

-- ROLLBACK: vrátit C# kód (revert commitu, který klíč odstranil z
-- PermissionSeedConfiguration) — PermissionSeeder pak klíč, kategorii i granty při startu
-- aplikace znovu doseeduje. Samotný INSERT zpět bez C# revertu nedává smysl.
