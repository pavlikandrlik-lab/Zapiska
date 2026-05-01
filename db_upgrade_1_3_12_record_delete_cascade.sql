-- ============================================================================
-- Migration: db_upgrade_1_3_12_record_delete_cascade.sql
-- Date:      2026-04-27
-- Purpose:   Sjednotit FK constraints na projektove_zaznamy(id) na ON DELETE CASCADE.
--
-- Background:
--   DeleteRecordAsync v RecordService.DeleteRecord.cs musel ručně mazat 13 child
--   tabulek před parent recordem, jinak SQL FK violation. Bug 2026-04-27
--   ukázal, že 2 tabulky byly zapomenuty (vyjadreni_vazby + zaznam_navrhy).
--
--   Sjednocením FK na CASCADE převádíme zodpovědnost za cleanup z aplikace na
--   SQL Server. Nová child tabulka v budoucnu stačí mít FK ON DELETE CASCADE
--   v migration skriptu — DeleteRecordAsync se nemusí měnit.
--
-- Strategy per FK (viz audit v learning-log 2026-04-27):
--   * 6× zaznam_historie_*       → CASCADE
--   * zaznam_externi_odkazy      → CASCADE
--   * zaznam_spoluprace          → CASCADE
--   * vyjadreni                  → CASCADE
--   * zaznam_priority_uzivatelu  → CASCADE
--   * zaznam_navrhy.zaznam_id    → CASCADE (proposals targeting this record)
--   * zaznam_navrhy.approved_record_id → SET NULL (zachovat audit historii proposals
--                                                  které vytvořily tento záznam)
--   * zaznam_harmonogram_hodnoty → přidat FK + CASCADE (FK doposud neexistovala)
--   * vyjadreni_vazby.zaznam_id  → již CASCADE (db_upgrade_1_3_6) — beze změny
--   * vyjadreni_vazby.externi_odkaz_id → ZACHOVAT NO ACTION (Save UPSERT pre-flight
--                                                            check chrání harvest
--                                                            history před náhodným
--                                                            smazáním vazby)
--
-- Idempotency: skript bezpečné re-run — DROP CONSTRAINT IF EXISTS pattern přes
--   sys.foreign_keys lookup. Constraints po prvním run mají explicitní jména
--   FK_<table>_<column> takže opakovaný run je no-op.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

PRINT '--- db_upgrade_1_3_12_record_delete_cascade.sql ---';
GO

-- ----------------------------------------------------------------------------
-- Helper: drop existing FK na projektove_zaznamy v dané child tabulce.
--   Předchozí FK má system-generated name (z initial seed záznamy jednání-7.sql).
-- ----------------------------------------------------------------------------

DECLARE @sql NVARCHAR(MAX);
DECLARE @fkName SYSNAME;

-- ----------------------------------------------------------------------------
-- 1. zaznam_historie_zmen_typu.zaznam_id → CASCADE
-- ----------------------------------------------------------------------------
SELECT @fkName = fk.name
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
WHERE fk.parent_object_id = OBJECT_ID(N'dbo.zaznam_historie_zmen_typu')
  AND fk.referenced_object_id = OBJECT_ID(N'dbo.projektove_zaznamy')
  AND c.name = 'zaznam_id';

IF @fkName IS NOT NULL
BEGIN
    SET @sql = N'ALTER TABLE dbo.zaznam_historie_zmen_typu DROP CONSTRAINT ' + QUOTENAME(@fkName);
    EXEC sp_executesql @sql;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_zaznam_historie_zmen_typu_zaznam' AND parent_object_id = OBJECT_ID(N'dbo.zaznam_historie_zmen_typu'))
BEGIN
    ALTER TABLE dbo.zaznam_historie_zmen_typu
        ADD CONSTRAINT FK_zaznam_historie_zmen_typu_zaznam
        FOREIGN KEY (zaznam_id) REFERENCES dbo.projektove_zaznamy(id) ON DELETE CASCADE;
    PRINT '  + FK_zaznam_historie_zmen_typu_zaznam → CASCADE';
END
ELSE
    PRINT '  = FK_zaznam_historie_zmen_typu_zaznam already CASCADE';

-- ----------------------------------------------------------------------------
-- 2. zaznam_historie_terminu.zaznam_id → CASCADE
-- ----------------------------------------------------------------------------
SET @fkName = NULL;
SELECT @fkName = fk.name
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
WHERE fk.parent_object_id = OBJECT_ID(N'dbo.zaznam_historie_terminu')
  AND fk.referenced_object_id = OBJECT_ID(N'dbo.projektove_zaznamy')
  AND c.name = 'zaznam_id';

IF @fkName IS NOT NULL
BEGIN
    SET @sql = N'ALTER TABLE dbo.zaznam_historie_terminu DROP CONSTRAINT ' + QUOTENAME(@fkName);
    EXEC sp_executesql @sql;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_zaznam_historie_terminu_zaznam' AND parent_object_id = OBJECT_ID(N'dbo.zaznam_historie_terminu'))
BEGIN
    ALTER TABLE dbo.zaznam_historie_terminu
        ADD CONSTRAINT FK_zaznam_historie_terminu_zaznam
        FOREIGN KEY (zaznam_id) REFERENCES dbo.projektove_zaznamy(id) ON DELETE CASCADE;
    PRINT '  + FK_zaznam_historie_terminu_zaznam → CASCADE';
END
ELSE
    PRINT '  = FK_zaznam_historie_terminu_zaznam already CASCADE';

-- ----------------------------------------------------------------------------
-- 3. zaznam_historie_vlastnik.zaznam_id → CASCADE
-- ----------------------------------------------------------------------------
SET @fkName = NULL;
SELECT @fkName = fk.name
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
WHERE fk.parent_object_id = OBJECT_ID(N'dbo.zaznam_historie_vlastnik')
  AND fk.referenced_object_id = OBJECT_ID(N'dbo.projektove_zaznamy')
  AND c.name = 'zaznam_id';

IF @fkName IS NOT NULL
BEGIN
    SET @sql = N'ALTER TABLE dbo.zaznam_historie_vlastnik DROP CONSTRAINT ' + QUOTENAME(@fkName);
    EXEC sp_executesql @sql;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_zaznam_historie_vlastnik_zaznam' AND parent_object_id = OBJECT_ID(N'dbo.zaznam_historie_vlastnik'))
BEGIN
    ALTER TABLE dbo.zaznam_historie_vlastnik
        ADD CONSTRAINT FK_zaznam_historie_vlastnik_zaznam
        FOREIGN KEY (zaznam_id) REFERENCES dbo.projektove_zaznamy(id) ON DELETE CASCADE;
    PRINT '  + FK_zaznam_historie_vlastnik_zaznam → CASCADE';
END
ELSE
    PRINT '  = FK_zaznam_historie_vlastnik_zaznam already CASCADE';

-- ----------------------------------------------------------------------------
-- 4. zaznam_historie_subsystem.zaznam_id → CASCADE
-- ----------------------------------------------------------------------------
SET @fkName = NULL;
SELECT @fkName = fk.name
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
WHERE fk.parent_object_id = OBJECT_ID(N'dbo.zaznam_historie_subsystem')
  AND fk.referenced_object_id = OBJECT_ID(N'dbo.projektove_zaznamy')
  AND c.name = 'zaznam_id';

IF @fkName IS NOT NULL
BEGIN
    SET @sql = N'ALTER TABLE dbo.zaznam_historie_subsystem DROP CONSTRAINT ' + QUOTENAME(@fkName);
    EXEC sp_executesql @sql;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_zaznam_historie_subsystem_zaznam' AND parent_object_id = OBJECT_ID(N'dbo.zaznam_historie_subsystem'))
BEGIN
    ALTER TABLE dbo.zaznam_historie_subsystem
        ADD CONSTRAINT FK_zaznam_historie_subsystem_zaznam
        FOREIGN KEY (zaznam_id) REFERENCES dbo.projektove_zaznamy(id) ON DELETE CASCADE;
    PRINT '  + FK_zaznam_historie_subsystem_zaznam → CASCADE';
END
ELSE
    PRINT '  = FK_zaznam_historie_subsystem_zaznam already CASCADE';

-- ----------------------------------------------------------------------------
-- 5. zaznam_historie_stavu_zaznamu.zaznam_id → CASCADE
-- ----------------------------------------------------------------------------
SET @fkName = NULL;
SELECT @fkName = fk.name
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
WHERE fk.parent_object_id = OBJECT_ID(N'dbo.zaznam_historie_stavu_zaznamu')
  AND fk.referenced_object_id = OBJECT_ID(N'dbo.projektove_zaznamy')
  AND c.name = 'zaznam_id';

IF @fkName IS NOT NULL
BEGIN
    SET @sql = N'ALTER TABLE dbo.zaznam_historie_stavu_zaznamu DROP CONSTRAINT ' + QUOTENAME(@fkName);
    EXEC sp_executesql @sql;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_zaznam_historie_stavu_zaznamu_zaznam' AND parent_object_id = OBJECT_ID(N'dbo.zaznam_historie_stavu_zaznamu'))
BEGIN
    ALTER TABLE dbo.zaznam_historie_stavu_zaznamu
        ADD CONSTRAINT FK_zaznam_historie_stavu_zaznamu_zaznam
        FOREIGN KEY (zaznam_id) REFERENCES dbo.projektove_zaznamy(id) ON DELETE CASCADE;
    PRINT '  + FK_zaznam_historie_stavu_zaznamu_zaznam → CASCADE';
END
ELSE
    PRINT '  = FK_zaznam_historie_stavu_zaznamu_zaznam already CASCADE';

-- ----------------------------------------------------------------------------
-- 6. zaznam_historie_stavu_projektu.zaznam_id → CASCADE
-- ----------------------------------------------------------------------------
SET @fkName = NULL;
SELECT @fkName = fk.name
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
WHERE fk.parent_object_id = OBJECT_ID(N'dbo.zaznam_historie_stavu_projektu')
  AND fk.referenced_object_id = OBJECT_ID(N'dbo.projektove_zaznamy')
  AND c.name = 'zaznam_id';

IF @fkName IS NOT NULL
BEGIN
    SET @sql = N'ALTER TABLE dbo.zaznam_historie_stavu_projektu DROP CONSTRAINT ' + QUOTENAME(@fkName);
    EXEC sp_executesql @sql;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_zaznam_historie_stavu_projektu_zaznam' AND parent_object_id = OBJECT_ID(N'dbo.zaznam_historie_stavu_projektu'))
BEGIN
    ALTER TABLE dbo.zaznam_historie_stavu_projektu
        ADD CONSTRAINT FK_zaznam_historie_stavu_projektu_zaznam
        FOREIGN KEY (zaznam_id) REFERENCES dbo.projektove_zaznamy(id) ON DELETE CASCADE;
    PRINT '  + FK_zaznam_historie_stavu_projektu_zaznam → CASCADE';
END
ELSE
    PRINT '  = FK_zaznam_historie_stavu_projektu_zaznam already CASCADE';

-- ----------------------------------------------------------------------------
-- 7. zaznam_externi_odkazy.zaznam_id → CASCADE
-- ----------------------------------------------------------------------------
SET @fkName = NULL;
SELECT @fkName = fk.name
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
WHERE fk.parent_object_id = OBJECT_ID(N'dbo.zaznam_externi_odkazy')
  AND fk.referenced_object_id = OBJECT_ID(N'dbo.projektove_zaznamy')
  AND c.name = 'zaznam_id';

IF @fkName IS NOT NULL
BEGIN
    SET @sql = N'ALTER TABLE dbo.zaznam_externi_odkazy DROP CONSTRAINT ' + QUOTENAME(@fkName);
    EXEC sp_executesql @sql;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_zaznam_externi_odkazy_zaznam' AND parent_object_id = OBJECT_ID(N'dbo.zaznam_externi_odkazy'))
BEGIN
    ALTER TABLE dbo.zaznam_externi_odkazy
        ADD CONSTRAINT FK_zaznam_externi_odkazy_zaznam
        FOREIGN KEY (zaznam_id) REFERENCES dbo.projektove_zaznamy(id) ON DELETE CASCADE;
    PRINT '  + FK_zaznam_externi_odkazy_zaznam → CASCADE';
END
ELSE
    PRINT '  = FK_zaznam_externi_odkazy_zaznam already CASCADE';

-- ----------------------------------------------------------------------------
-- 8. zaznam_spoluprace.zaznam_id → CASCADE
-- ----------------------------------------------------------------------------
SET @fkName = NULL;
SELECT @fkName = fk.name
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
WHERE fk.parent_object_id = OBJECT_ID(N'dbo.zaznam_spoluprace')
  AND fk.referenced_object_id = OBJECT_ID(N'dbo.projektove_zaznamy')
  AND c.name = 'zaznam_id';

IF @fkName IS NOT NULL
BEGIN
    SET @sql = N'ALTER TABLE dbo.zaznam_spoluprace DROP CONSTRAINT ' + QUOTENAME(@fkName);
    EXEC sp_executesql @sql;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_zaznam_spoluprace_zaznam' AND parent_object_id = OBJECT_ID(N'dbo.zaznam_spoluprace'))
BEGIN
    ALTER TABLE dbo.zaznam_spoluprace
        ADD CONSTRAINT FK_zaznam_spoluprace_zaznam
        FOREIGN KEY (zaznam_id) REFERENCES dbo.projektove_zaznamy(id) ON DELETE CASCADE;
    PRINT '  + FK_zaznam_spoluprace_zaznam → CASCADE';
END
ELSE
    PRINT '  = FK_zaznam_spoluprace_zaznam already CASCADE';

-- ----------------------------------------------------------------------------
-- 9. vyjadreni.zaznam_id → CASCADE
-- ----------------------------------------------------------------------------
SET @fkName = NULL;
SELECT @fkName = fk.name
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
WHERE fk.parent_object_id = OBJECT_ID(N'dbo.vyjadreni')
  AND fk.referenced_object_id = OBJECT_ID(N'dbo.projektove_zaznamy')
  AND c.name = 'zaznam_id';

IF @fkName IS NOT NULL
BEGIN
    SET @sql = N'ALTER TABLE dbo.vyjadreni DROP CONSTRAINT ' + QUOTENAME(@fkName);
    EXEC sp_executesql @sql;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_vyjadreni_zaznam' AND parent_object_id = OBJECT_ID(N'dbo.vyjadreni'))
BEGIN
    ALTER TABLE dbo.vyjadreni
        ADD CONSTRAINT FK_vyjadreni_zaznam
        FOREIGN KEY (zaznam_id) REFERENCES dbo.projektove_zaznamy(id) ON DELETE CASCADE;
    PRINT '  + FK_vyjadreni_zaznam → CASCADE';
END
ELSE
    PRINT '  = FK_vyjadreni_zaznam already CASCADE';

-- ----------------------------------------------------------------------------
-- 10. zaznam_priority_uzivatelu.zaznam_id → CASCADE
-- ----------------------------------------------------------------------------
SET @fkName = NULL;
SELECT @fkName = fk.name
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
WHERE fk.parent_object_id = OBJECT_ID(N'dbo.zaznam_priority_uzivatelu')
  AND fk.referenced_object_id = OBJECT_ID(N'dbo.projektove_zaznamy')
  AND c.name = 'zaznam_id';

IF @fkName IS NOT NULL
BEGIN
    SET @sql = N'ALTER TABLE dbo.zaznam_priority_uzivatelu DROP CONSTRAINT ' + QUOTENAME(@fkName);
    EXEC sp_executesql @sql;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_zaznam_priority_uzivatelu_zaznam' AND parent_object_id = OBJECT_ID(N'dbo.zaznam_priority_uzivatelu'))
BEGIN
    ALTER TABLE dbo.zaznam_priority_uzivatelu
        ADD CONSTRAINT FK_zaznam_priority_uzivatelu_zaznam
        FOREIGN KEY (zaznam_id) REFERENCES dbo.projektove_zaznamy(id) ON DELETE CASCADE;
    PRINT '  + FK_zaznam_priority_uzivatelu_zaznam → CASCADE';
END
ELSE
    PRINT '  = FK_zaznam_priority_uzivatelu_zaznam already CASCADE';

-- ----------------------------------------------------------------------------
-- 11. zaznam_navrhy.zaznam_id → CASCADE (primary path: proposals targeting this record)
-- ----------------------------------------------------------------------------
SET @fkName = NULL;
SELECT @fkName = fk.name
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
WHERE fk.parent_object_id = OBJECT_ID(N'dbo.zaznam_navrhy')
  AND fk.referenced_object_id = OBJECT_ID(N'dbo.projektove_zaznamy')
  AND c.name = 'zaznam_id';

IF @fkName IS NOT NULL
BEGIN
    SET @sql = N'ALTER TABLE dbo.zaznam_navrhy DROP CONSTRAINT ' + QUOTENAME(@fkName);
    EXEC sp_executesql @sql;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_zaznam_navrhy_zaznam' AND parent_object_id = OBJECT_ID(N'dbo.zaznam_navrhy'))
BEGIN
    ALTER TABLE dbo.zaznam_navrhy
        ADD CONSTRAINT FK_zaznam_navrhy_zaznam
        FOREIGN KEY (zaznam_id) REFERENCES dbo.projektove_zaznamy(id) ON DELETE CASCADE;
    PRINT '  + FK_zaznam_navrhy_zaznam → CASCADE';
END
ELSE
    PRINT '  = FK_zaznam_navrhy_zaznam already CASCADE';

-- ----------------------------------------------------------------------------
-- 12. zaznam_navrhy.approved_record_id → SET NULL (multi-path avoidance + audit preservation)
-- ----------------------------------------------------------------------------
SET @fkName = NULL;
SELECT @fkName = fk.name
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
WHERE fk.parent_object_id = OBJECT_ID(N'dbo.zaznam_navrhy')
  AND fk.referenced_object_id = OBJECT_ID(N'dbo.projektove_zaznamy')
  AND c.name = 'approved_record_id';

IF @fkName IS NOT NULL
BEGIN
    SET @sql = N'ALTER TABLE dbo.zaznam_navrhy DROP CONSTRAINT ' + QUOTENAME(@fkName);
    EXEC sp_executesql @sql;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_zaznam_navrhy_approved_record' AND parent_object_id = OBJECT_ID(N'dbo.zaznam_navrhy'))
BEGIN
    ALTER TABLE dbo.zaznam_navrhy
        ADD CONSTRAINT FK_zaznam_navrhy_approved_record
        FOREIGN KEY (approved_record_id) REFERENCES dbo.projektove_zaznamy(id) ON DELETE SET NULL;
    PRINT '  + FK_zaznam_navrhy_approved_record → SET NULL';
END
ELSE
    PRINT '  = FK_zaznam_navrhy_approved_record already SET NULL';

-- ----------------------------------------------------------------------------
-- 13. zaznam_harmonogram_hodnoty.zaznam_id → ADD FK + CASCADE (FK doposud chybí)
-- ----------------------------------------------------------------------------
-- Tabulka existuje od počátku, ale CREATE TABLE byl udělán bez explicitní FK
-- na projektove_zaznamy. EF Core entity má ZaznamId property, ale fyzicky
-- v DB neexistuje constraint → orphan rows pre-cleanup byly možné. Po této
-- migraci CASCADE zajistí čistou DB i bez aplikační logiky.
SET @fkName = NULL;
SELECT @fkName = fk.name
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
WHERE fk.parent_object_id = OBJECT_ID(N'dbo.zaznam_harmonogram_hodnoty')
  AND fk.referenced_object_id = OBJECT_ID(N'dbo.projektove_zaznamy')
  AND c.name = 'zaznam_id';

IF @fkName IS NOT NULL
BEGIN
    SET @sql = N'ALTER TABLE dbo.zaznam_harmonogram_hodnoty DROP CONSTRAINT ' + QUOTENAME(@fkName);
    EXEC sp_executesql @sql;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_zaznam_harmonogram_hodnoty_zaznam' AND parent_object_id = OBJECT_ID(N'dbo.zaznam_harmonogram_hodnoty'))
BEGIN
    -- Pre-flight: orphan rows by zablokovaly přidání FK. Smazat orphans.
    DELETE FROM dbo.zaznam_harmonogram_hodnoty
    WHERE zaznam_id NOT IN (SELECT id FROM dbo.projektove_zaznamy);

    ALTER TABLE dbo.zaznam_harmonogram_hodnoty
        ADD CONSTRAINT FK_zaznam_harmonogram_hodnoty_zaznam
        FOREIGN KEY (zaznam_id) REFERENCES dbo.projektove_zaznamy(id) ON DELETE CASCADE;
    PRINT '  + FK_zaznam_harmonogram_hodnoty_zaznam → CASCADE (newly added)';
END
ELSE
    PRINT '  = FK_zaznam_harmonogram_hodnoty_zaznam already CASCADE';

GO

-- ----------------------------------------------------------------------------
-- Sanity check — vypsat aktuální stav všech FK na projektove_zaznamy.
-- ----------------------------------------------------------------------------
PRINT '';
PRINT '=== Aktuální FK na projektove_zaznamy po migraci ===';

SELECT
    fk.name                                  AS constraint_name,
    OBJECT_NAME(fk.parent_object_id)         AS child_table,
    cp.name                                  AS child_column,
    fk.delete_referential_action_desc        AS on_delete,
    fk.update_referential_action_desc        AS on_update
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns cp ON cp.object_id = fkc.parent_object_id AND cp.column_id = fkc.parent_column_id
WHERE fk.referenced_object_id = OBJECT_ID(N'dbo.projektove_zaznamy')
ORDER BY OBJECT_NAME(fk.parent_object_id), cp.name;

GO

PRINT '';
PRINT '=== Aktuální FK na zaznam_externi_odkazy (vyjadreni_vazby.externi_odkaz_id MUSÍ být NO ACTION!) ===';

SELECT
    fk.name                                  AS constraint_name,
    OBJECT_NAME(fk.parent_object_id)         AS child_table,
    cp.name                                  AS child_column,
    fk.delete_referential_action_desc        AS on_delete
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns cp ON cp.object_id = fkc.parent_object_id AND cp.column_id = fkc.parent_column_id
WHERE fk.referenced_object_id = OBJECT_ID(N'dbo.zaznam_externi_odkazy')
ORDER BY OBJECT_NAME(fk.parent_object_id), cp.name;

GO

PRINT '';
PRINT '--- db_upgrade_1_3_12_record_delete_cascade.sql DONE ---';
GO
