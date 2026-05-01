-- db_upgrade_1_3_13_proposal_supersede.sql
--
-- Plán Harmonogram refactor 2026-05-01 — DESIGN-7-D.
-- Přidává sloupec superseded_by_proposal_id na zaznam_navrhy pro auto-supersede vlastního
-- starého návrhu při novém submitu (max 1 Pending per zaznam+typNavrhu invariant).
-- Plus rozšiřuje CK_zaznam_navrhy_stav o nový state code 'SUPERSEDED'.

SET NOCOUNT ON;
GO

-- 1) Sloupec superseded_by_proposal_id (nullable FK na zaznam_navrhy.id, bez DB FK constraint
--    aby self-reference history nezpůsobila cyklický cascade)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.zaznam_navrhy') AND name = 'superseded_by_proposal_id')
BEGIN
    ALTER TABLE dbo.zaznam_navrhy
        ADD superseded_by_proposal_id INT NULL;
    PRINT 'Added zaznam_navrhy.superseded_by_proposal_id.';
END
ELSE
    PRINT 'superseded_by_proposal_id column already exists, skipping.';
GO

-- 2) Rozšířit CK_zaznam_navrhy_stav o SUPERSEDED state code
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_zaznam_navrhy_stav' AND parent_object_id = OBJECT_ID('dbo.zaznam_navrhy'))
BEGIN
    ALTER TABLE dbo.zaznam_navrhy DROP CONSTRAINT CK_zaznam_navrhy_stav;
    PRINT 'Dropped old CK_zaznam_navrhy_stav (without SUPERSEDED).';
END
GO

ALTER TABLE dbo.zaznam_navrhy
    ADD CONSTRAINT CK_zaznam_navrhy_stav
    CHECK (stav IN ('PENDING', 'APPROVED', 'REJECTED', 'SUPERSEDED'));
PRINT 'Created new CK_zaznam_navrhy_stav with SUPERSEDED.';
GO

-- 3) Index pro non-schedule pending lookups (UX_zaznam_navrhy_pending_schedule_per_record
--    už existuje pro SCHEDULE_PLAN_CHANGE max-1-pending invariant; tento index pokryje
--    obecnější queries za Pending napříč všemi typy).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_zaznam_navrhy_pending_per_zaznam_typ' AND object_id = OBJECT_ID('dbo.zaznam_navrhy'))
BEGIN
    CREATE INDEX IX_zaznam_navrhy_pending_per_zaznam_typ
        ON dbo.zaznam_navrhy (zaznam_id, typ_navrhu)
        WHERE stav = 'PENDING';
    PRINT 'Created index IX_zaznam_navrhy_pending_per_zaznam_typ for max-1-pending check napříč typy.';
END
ELSE
    PRINT 'Index IX_zaznam_navrhy_pending_per_zaznam_typ already exists, skipping.';
GO

-- Sanity check
SELECT
    COUNT(*) AS total_proposals,
    SUM(CASE WHEN stav = 'PENDING' THEN 1 ELSE 0 END) AS pending_count,
    SUM(CASE WHEN stav = 'APPROVED' THEN 1 ELSE 0 END) AS approved_count,
    SUM(CASE WHEN stav = 'REJECTED' THEN 1 ELSE 0 END) AS rejected_count,
    SUM(CASE WHEN stav = 'SUPERSEDED' THEN 1 ELSE 0 END) AS superseded_count,
    SUM(CASE WHEN superseded_by_proposal_id IS NOT NULL THEN 1 ELSE 0 END) AS with_supersedes_link
FROM dbo.zaznam_navrhy;
GO
