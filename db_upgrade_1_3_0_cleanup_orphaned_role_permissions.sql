-- db_upgrade_1_3_0_cleanup_orphaned_role_permissions.sql
-- Authorization unification — Fáze C — Task C7
--
-- Účel: vyčistit authz.role_permissions od řádků, které vznikly přes starou
--       UI kompozici rolí (tab "Akce/Role/Uživatel-Role" v Nastavení). Po Fázi F
--       je seed v `PermissionSeedConfiguration.cs` jediným zdrojem pravdy pro role
--       a jejich permission mappingy.
--
-- Strategy:
--   1. Smazat authz.role_permission_projects rows navázané na custom (is_system=0) role
--   2. Smazat authz.role_permissions rows pro custom role
--   3. Deaktivovat (not smazat — kvůli auditu) custom role samotné
--
-- Idempotentní: run více-krát bezpečně. Pokud rows není, nic se nestane.
-- POZOR: spustit AŽ PO nasazení kódu s novou seed matricí (Task C2).
--        Před spuštěním doporučuji DB backup.

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1. Vyčistit authz.role_permission_projects (INCLUDE scope mode per-project grants)
--    pro non-system role (vzniklé přes starou UI "přidej grant na konkrétní projekt").
DELETE FROM authz.role_permission_projects
WHERE role_permission_id IN (
    SELECT rp.id
    FROM authz.role_permissions rp
    INNER JOIN authz.roles r ON r.id = rp.role_id
    WHERE r.is_system = 0
);

-- 2. Smazat mappings pro custom (non-system) role — po Fázi F se tyto role
--    nebudou upravovat přes UI, takže jejich mappings jsou orphans.
DELETE FROM authz.role_permissions
WHERE role_id IN (
    SELECT id FROM authz.roles WHERE is_system = 0
);

-- 3. Deaktivovat custom role. Nemažeme je — zachováváme pro audit,
--    ať budoucí historik ví, že kdysi existovaly a kdo je vytvořil
--    (viz authz_audit_log pokud existuje).
UPDATE authz.roles
SET is_active = 0
WHERE is_system = 0 AND is_active = 1;

-- 4. Log pro audit.
PRINT '[db_upgrade_1_3_0] Cleanup orphaned role_permissions dokončen.';

COMMIT TRANSACTION;
