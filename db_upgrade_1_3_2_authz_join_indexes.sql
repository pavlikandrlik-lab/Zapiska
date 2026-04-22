-- db_upgrade_1_3_2_authz_join_indexes.sql
-- H-3 — chybějící indexy na authz join sloupcích v lookup tabulkách
--
-- Účel: AuthorizationSnapshotBuilder joinuje při každém build() snapshotu
--       přes CiselnikRoliProjektu.authz_role_id a CiselnikRoliSubsystemu.authz_role_id.
--       Bez indexu jde o table-scan přes lookup tabulky při každém auth checku.
--
-- Filtrovaný index (WHERE authz_role_id IS NOT NULL) — většina lookup řádků má
-- authz_role_id = NULL (neautorizační role), filtrovaný index šetří místo.
--
-- Idempotentní: IF NOT EXISTS guard zajišťuje bezpečné opakované spuštění.
-- Spouštět po nasazení kódu s HasIndex() konfiguracemi.

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'ix_ciselnik_roli_projektu_authz_role_id'
      AND object_id = OBJECT_ID(N'dbo.ciselnik_roli_projektu')
)
BEGIN
    CREATE NONCLUSTERED INDEX ix_ciselnik_roli_projektu_authz_role_id
        ON dbo.ciselnik_roli_projektu (authz_role_id)
        WHERE authz_role_id IS NOT NULL;
    PRINT '[db_upgrade_1_3_2] Created ix_ciselnik_roli_projektu_authz_role_id';
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'ix_ciselnik_roli_subsystemu_authz_role_id'
      AND object_id = OBJECT_ID(N'dbo.ciselnik_roli_subsystemu')
)
BEGIN
    CREATE NONCLUSTERED INDEX ix_ciselnik_roli_subsystemu_authz_role_id
        ON dbo.ciselnik_roli_subsystemu (authz_role_id)
        WHERE authz_role_id IS NOT NULL;
    PRINT '[db_upgrade_1_3_2] Created ix_ciselnik_roli_subsystemu_authz_role_id';
END

PRINT '[db_upgrade_1_3_2] Hotovo.';

COMMIT TRANSACTION;
