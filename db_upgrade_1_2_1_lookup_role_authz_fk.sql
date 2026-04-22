-- db_upgrade_1_2_1_lookup_role_authz_fk.sql
-- Authorization unification — Fáze A — Task A5
-- Propojení lookup rolí (projektové + subsystémové) s authz.roles přes FK.

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1. ciselnik_roli_projektu.authz_role_id (nullable během migrace)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.ciselnik_roli_projektu') AND name = 'authz_role_id'
)
BEGIN
    ALTER TABLE dbo.ciselnik_roli_projektu
        ADD authz_role_id INT NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID('dbo.ciselnik_roli_projektu')
      AND name = 'fk_ciselnik_roli_projektu_authz_role'
)
BEGIN
    ALTER TABLE dbo.ciselnik_roli_projektu
        ADD CONSTRAINT fk_ciselnik_roli_projektu_authz_role
            FOREIGN KEY (authz_role_id) REFERENCES authz.roles(id);
END;

-- 2. ciselnik_roli_subsystemu.authz_role_id
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.ciselnik_roli_subsystemu') AND name = 'authz_role_id'
)
BEGIN
    ALTER TABLE dbo.ciselnik_roli_subsystemu
        ADD authz_role_id INT NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID('dbo.ciselnik_roli_subsystemu')
      AND name = 'fk_ciselnik_roli_subsystemu_authz_role'
)
BEGIN
    ALTER TABLE dbo.ciselnik_roli_subsystemu
        ADD CONSTRAINT fk_ciselnik_roli_subsystemu_authz_role
            FOREIGN KEY (authz_role_id) REFERENCES authz.roles(id);
END;

COMMIT TRANSACTION;
