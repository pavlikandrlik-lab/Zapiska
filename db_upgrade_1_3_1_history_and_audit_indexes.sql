-- db_upgrade_1_3_1_history_and_audit_indexes.sql
-- Bug #7 — chybějící FK indexy na history + audit tabulkách
--
-- Účel: přidat nonclustered indexy na zaznam_id pro všechny history tabulky
--       a složený index (entity_type, entity_id) + created_at pro audit log.
--       Tyto tabulky jsou filtrovány přes recordIds.Contains(x.ZaznamId) v
--       ProjectService.RecordCards, ExportProjectionBuilders a dalších službách —
--       bez indexu jde o full scan při každém načtení karet projektu.
--
-- Idempotentní: IF NOT EXISTS guard zajišťuje bezpečné opakované spuštění.
-- Spouštět po nasazení kódu s HasIndex() konfiguracemi.

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- ============================================================
-- History tabulky — index na zaznam_id
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_zaznam_historie_vlastnik_zaznam_id'
      AND object_id = OBJECT_ID('dbo.zaznam_historie_vlastnik')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_zaznam_historie_vlastnik_zaznam_id
        ON dbo.zaznam_historie_vlastnik (zaznam_id);
    PRINT '[db_upgrade_1_3_1] Created IX_zaznam_historie_vlastnik_zaznam_id';
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_zaznam_historie_terminu_zaznam_id'
      AND object_id = OBJECT_ID('dbo.zaznam_historie_terminu')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_zaznam_historie_terminu_zaznam_id
        ON dbo.zaznam_historie_terminu (zaznam_id);
    PRINT '[db_upgrade_1_3_1] Created IX_zaznam_historie_terminu_zaznam_id';
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_zaznam_historie_subsystem_zaznam_id'
      AND object_id = OBJECT_ID('dbo.zaznam_historie_subsystem')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_zaznam_historie_subsystem_zaznam_id
        ON dbo.zaznam_historie_subsystem (zaznam_id);
    PRINT '[db_upgrade_1_3_1] Created IX_zaznam_historie_subsystem_zaznam_id';
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_zaznam_historie_zmen_typu_zaznam_id'
      AND object_id = OBJECT_ID('dbo.zaznam_historie_zmen_typu')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_zaznam_historie_zmen_typu_zaznam_id
        ON dbo.zaznam_historie_zmen_typu (zaznam_id);
    PRINT '[db_upgrade_1_3_1] Created IX_zaznam_historie_zmen_typu_zaznam_id';
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_zaznam_historie_stavu_zaznamu_zaznam_id'
      AND object_id = OBJECT_ID('dbo.zaznam_historie_stavu_zaznamu')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_zaznam_historie_stavu_zaznamu_zaznam_id
        ON dbo.zaznam_historie_stavu_zaznamu (zaznam_id);
    PRINT '[db_upgrade_1_3_1] Created IX_zaznam_historie_stavu_zaznamu_zaznam_id';
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_zaznam_historie_stavu_projektu_zaznam_id'
      AND object_id = OBJECT_ID('dbo.zaznam_historie_stavu_projektu')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_zaznam_historie_stavu_projektu_zaznam_id
        ON dbo.zaznam_historie_stavu_projektu (zaznam_id);
    PRINT '[db_upgrade_1_3_1] Created IX_zaznam_historie_stavu_projektu_zaznam_id';
END

-- zaznam_externi_odkazy — zaznam_id (FK, dotazováno přes recordIds.Contains)
-- (existující index ux_zaznam_externi_odkazy_cislo_in_vyzve pokrývá pouze cislo+vyzva_id)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_zaznam_externi_odkazy_zaznam_id'
      AND object_id = OBJECT_ID('dbo.zaznam_externi_odkazy')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_zaznam_externi_odkazy_zaznam_id
        ON dbo.zaznam_externi_odkazy (zaznam_id);
    PRINT '[db_upgrade_1_3_1] Created IX_zaznam_externi_odkazy_zaznam_id';
END

-- zaznam_spoluprace — PK je (zaznam_id, osoba_id), zaznam_id je leading column → PK index
-- slouží filtraci; žádný extra index potřeba není.
PRINT '[db_upgrade_1_3_1] zaznam_spoluprace: PK (zaznam_id, osoba_id) pokrývá Contains dotaz — přeskočeno.';

-- ============================================================
-- Audit log — složený index (entity_type, entity_id) + created_at
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_authz_audit_log_entity_type_entity_id'
      AND object_id = OBJECT_ID('authz.audit_log')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_authz_audit_log_entity_type_entity_id
        ON authz.audit_log (entity_type, entity_id);
    PRINT '[db_upgrade_1_3_1] Created IX_authz_audit_log_entity_type_entity_id';
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_authz_audit_log_created_at'
      AND object_id = OBJECT_ID('authz.audit_log')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_authz_audit_log_created_at
        ON authz.audit_log (created_at);
    PRINT '[db_upgrade_1_3_1] Created IX_authz_audit_log_created_at';
END

PRINT '[db_upgrade_1_3_1] Hotovo.';

COMMIT TRANSACTION;
