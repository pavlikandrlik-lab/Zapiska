-- db_upgrade_1_2_0_authz_role_scope.sql
-- Authorization unification — Fáze A — Task A2
-- Spec: docs/superpowers/specs/2026-04-21-authorization-unification-design.md
-- Plan: docs/superpowers/plans/2026-04-21-authorization-unification.md

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1. Přidat sloupec scope do authz.roles (default 'GLOBAL' pro existing rows).
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('authz.roles') AND name = 'scope'
)
BEGIN
    ALTER TABLE authz.roles
        ADD scope NVARCHAR(16) NOT NULL CONSTRAINT df_authz_roles_scope DEFAULT 'GLOBAL';
END;

-- 2. CHECK constraint pro povolené hodnoty.
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID('authz.roles') AND name = 'ck_authz_roles_scope'
)
BEGIN
    ALTER TABLE authz.roles
        ADD CONSTRAINT ck_authz_roles_scope
            CHECK (scope IN ('GLOBAL','PROJECT','SUBSYSTEM'));
END;

COMMIT TRANSACTION;
