-- ============================================================================
-- Migration: db_upgrade_1_3_13_externi_odkaz_cascade.sql
-- Date:      2026-04-28
-- Purpose:   Změnit FK_zhvv_externi_odkaz z ON DELETE NO ACTION na ON DELETE CASCADE.
--
-- Background:
--   Bug 2026-04-28: User nemohl smazat externí vazbu ze záznamu, protože Save
--   pre-flight check (RecordService.SaveRecord.cs ReplaceRecordExternalLinksAsync)
--   blokoval DELETE jakékoli vazby s navázanými řádky v vyjadreni_vazby
--   s hláškou "external_link_harvest_locked".
--
--   Před 2026-04-27 to bylo mírnější — blokace fungovala jen pro vazby s text-fráze
--   bindings (K3/K4/K6/K7/K10). Po implementaci synthetic K1 binding (2026-04-28
--   plán nes-vyjadreni-a-4-datumy) má KAŽDÁ harvestnutá PMP/PNF vazba alespoň jeden
--   binding (K1 z HOT_ZAZNAMY.datum), takže blokace nyní fires univerzálně.
--
--   User požadavek: delete externí vazby je běžná operace a nesmí selhávat.
--   Audit hodnota bindings je nízká — HotVyjadreniId odkazuje na append-only
--   HOT_VYJADRENI v legacy DB, která existuje navždy a kterou nikdy nemažeme.
--   Binding row je jen user assignment "tato vyjadreni je navázána na tento krok"
--   — pokud externí vazba zmizí, binding ztrácí význam.
--
-- Strategy:
--   FK_zhvv_externi_odkaz: ON DELETE NO ACTION → ON DELETE CASCADE
--   Aplikace: odstranit pre-flight harvest_locked check v ReplaceRecordExternalLinksAsync.
--
-- Idempotency: skript bezpečný re-run. DROP CONSTRAINT IF EXISTS přes sys.foreign_keys
-- lookup, ADD CONSTRAINT jen pokud neexistuje.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

PRINT '--- db_upgrade_1_3_13_externi_odkaz_cascade.sql ---';
GO

-- ----------------------------------------------------------------------------
-- vyjadreni_vazby.externi_odkaz_id → CASCADE
-- ----------------------------------------------------------------------------
DECLARE @fkName SYSNAME;

SELECT @fkName = fk.name
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
WHERE fk.parent_object_id = OBJECT_ID(N'dbo.zaznam_harmonogram_vyjadreni_vazba')
  AND fk.referenced_object_id = OBJECT_ID(N'dbo.zaznam_externi_odkazy')
  AND c.name = 'externi_odkaz_id';

IF @fkName IS NOT NULL
BEGIN
    DECLARE @drop NVARCHAR(MAX) = N'ALTER TABLE dbo.zaznam_harmonogram_vyjadreni_vazba DROP CONSTRAINT ' + QUOTENAME(@fkName) + N';';
    EXEC sp_executesql @drop;
    PRINT '  Dropped FK ' + @fkName + ' on zaznam_harmonogram_vyjadreni_vazba.externi_odkaz_id.';
END
ELSE
    PRINT '  No existing FK on zaznam_harmonogram_vyjadreni_vazba.externi_odkaz_id (fresh schema).';

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_zhvv_externi_odkaz'
      AND parent_object_id = OBJECT_ID(N'dbo.zaznam_harmonogram_vyjadreni_vazba')
)
BEGIN
    ALTER TABLE dbo.zaznam_harmonogram_vyjadreni_vazba
        ADD CONSTRAINT FK_zhvv_externi_odkaz
            FOREIGN KEY (externi_odkaz_id)
            REFERENCES dbo.zaznam_externi_odkazy(id)
            ON DELETE CASCADE;
    PRINT '  Added FK_zhvv_externi_odkaz with ON DELETE CASCADE.';
END
ELSE
    PRINT '  FK_zhvv_externi_odkaz already exists, skipping ADD.';
GO

-- ----------------------------------------------------------------------------
-- Sanity check: zobrazit aktuální delete action
-- ----------------------------------------------------------------------------
SELECT
    fk.name AS fk_name,
    OBJECT_NAME(fk.parent_object_id) AS parent_table,
    OBJECT_NAME(fk.referenced_object_id) AS referenced_table,
    fk.delete_referential_action_desc AS on_delete
FROM sys.foreign_keys fk
WHERE fk.name = N'FK_zhvv_externi_odkaz';
GO

PRINT '--- Migration db_upgrade_1_3_13 complete ---';
GO
