-- db_upgrade_1_3_15_harmonogram_indexes.sql
--
-- Plán Harmonogram refactor 2026-05-01 — round 3 review #16.
-- Přidává performance indexy pro často-queryované sloupce:
--   - zaznam_harmonogram_hodnoty (zaznam_id) — used heavily ToggleRezim, SelectCandidate,
--     ComputePlan, StageManualActualKroky. Filter pouze na ZaznamId degeneroval na
--     CLUSTERED INDEX SCAN (composite UQ_..._zaznam_typ neslouží jako leading-column index).
--   - zaznam_harmonogram_hodnoty (skutecnost_rezim) WHERE rezim = Manual — pro retract
--     queries kde sync hledá Manual rows k preskočení.

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_zaznam_harmonogram_hodnoty_zaznam_id' AND object_id = OBJECT_ID('dbo.zaznam_harmonogram_hodnoty'))
BEGIN
    CREATE INDEX IX_zaznam_harmonogram_hodnoty_zaznam_id
        ON dbo.zaznam_harmonogram_hodnoty (zaznam_id)
        INCLUDE (typ_id, hodnota_int, skutecnost_rezim, skutecnost_zdroj, preferred_externi_odkaz_id, updated_at);
    PRINT 'Created IX_zaznam_harmonogram_hodnoty_zaznam_id (covering index pro queries jen na ZaznamId).';
END
ELSE
    PRINT 'IX_zaznam_harmonogram_hodnoty_zaznam_id already exists, skipping.';
GO

-- Sanity check
SELECT
    COUNT(*) AS total_indexes
FROM sys.indexes
WHERE object_id = OBJECT_ID('dbo.zaznam_harmonogram_hodnoty');
GO
