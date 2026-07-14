-- =============================================================================
-- db_upgrade_1_4_1_drop_schedule_preview.sql
--
-- Authz úklid 2026-07-14 — smaže z authz.* tabulek mrtvý klíč `schedule.preview`
-- a jeho (jednoúčelovou) kategorii SCHEDULE.
--
-- KONTEXT: Klíč hlídal ScheduleController.Recalc (stateless přepočet harmonogramu),
-- který byl smazán při harmonogram datum-model migraci (Fáze 7a, commit 52d6607).
-- Live-preview editoru je client-side (block.js); serverové schedule endpointy
-- (HarmonogramController.SelectCandidate/PreviewSync) kontrolují records.schedule.edit.
-- Klíč není referencován žádným [Authorize(Policy)] ani HasPermission checkem.
--
-- ROZSAH MIGRACE:
--   * Smaže záznamy POUZE z:
--       - authz.role_permission_projects  (scope INCLUDE vazby)
--       - authz.role_permissions          (vazby role -> permission)
--       - authz.permissions               (katalog: klic = 'schedule.preview')
--       - authz.permission_categories     (kod = 'SCHEDULE', jen pokud osiřelá)
--   * NEDOTKNE se: authz.user_roles, authz.roles, žádných business tabulek.
--
-- Idempotence: skript lze spustit opakovaně.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

PRINT N'[1.4.1] Drop schedule.preview — start';

-- Audit pre-state: které role klíč drží (očekávané: 7 seed rolí; případné CUSTOM
-- role zkontrolovat ručně — po smazání ztratí tento grant, což je záměr, klíč nic nehlídá).
SELECT r.kod AS role_kod, p.klic
FROM authz.role_permissions rp
INNER JOIN authz.roles r ON r.id = rp.role_id
INNER JOIN authz.permissions p ON p.id = rp.permission_id
WHERE p.klic = N'schedule.preview';

-- 1) role_permission_projects — první kvůli FK na role_permissions.
DELETE rpp
FROM authz.role_permission_projects rpp
INNER JOIN authz.role_permissions rp ON rp.id = rpp.role_permission_id
INNER JOIN authz.permissions p ON p.id = rp.permission_id
WHERE p.klic = N'schedule.preview';

PRINT N'[1.4.1] Smazáno z authz.role_permission_projects: ' + CAST(@@ROWCOUNT AS NVARCHAR(16));

-- 2) role_permissions.
DELETE rp
FROM authz.role_permissions rp
INNER JOIN authz.permissions p ON p.id = rp.permission_id
WHERE p.klic = N'schedule.preview';

PRINT N'[1.4.1] Smazáno z authz.role_permissions: ' + CAST(@@ROWCOUNT AS NVARCHAR(16));

-- 3) permissions.
DELETE FROM authz.permissions WHERE klic = N'schedule.preview';

PRINT N'[1.4.1] Smazáno z authz.permissions: ' + CAST(@@ROWCOUNT AS NVARCHAR(16));

-- 4) Kategorie SCHEDULE — jen pokud na ni už nic neukazuje (guard proti cizím klíčům).
DELETE c
FROM authz.permission_categories c
WHERE c.kod = N'SCHEDULE'
  AND NOT EXISTS (SELECT 1 FROM authz.permissions p WHERE p.category_id = c.id);

PRINT N'[1.4.1] Smazáno z authz.permission_categories: ' + CAST(@@ROWCOUNT AS NVARCHAR(16));

-- Sanity check.
IF EXISTS (SELECT 1 FROM authz.permissions WHERE klic = N'schedule.preview')
BEGIN
    ROLLBACK TRANSACTION;
    RAISERROR(N'[1.4.1] KONZISTENCE: schedule.preview stále existuje — migrace přerušena.', 16, 1);
    RETURN;
END

PRINT N'[1.4.1] Drop schedule.preview — dokončeno.';

COMMIT TRANSACTION;

-- ROLLBACK: nejprve vrátit C# kód (revert commitu, který klíč odstranil z
-- PermissionSeedConfiguration) — PermissionSeeder pak klíč i granty při startu
-- aplikace znovu doseeduje (idempotentní UPSERT). Samotný INSERT zpět bez C#
-- revertu nedává smysl.
