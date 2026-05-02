-- db_upgrade_1_3_16_fix_harmonogram_hodnoty_cascade.sql
--
-- FIX 2026-05-02 — `zaznam_harmonogram_hodnoty.zaznam_id` FK měla být v migraci
-- 1_3_12 přidaná s `ON DELETE CASCADE`, ale když ALTER selhal kvůli SQL 1785
-- "multiple cascade paths" v jiné batch (FK_zaznam_navrhy_approved_record SET NULL),
-- subsequent operations v stejném scriptu skončily v inconsistent state — FK
-- byla přidaná, ale `delete_referential_action_desc = NO_ACTION`.
--
-- Důsledek: DELETE záznamu (POST /Zaznamy/DeleteRecord) padal s FK violation
-- místo úspěšného CASCADE cleanup do `zaznam_harmonogram_hodnoty`. UI vidělo
-- UNEXPECTED_SERVER_ERROR (Bug 1, traceId 4000095a-…).
--
-- Tato migrace forcefully přepíše FK na CASCADE, idempotentně (skipne pokud už OK).

SET NOCOUNT ON;
GO

DECLARE @currentAction NVARCHAR(60);
SELECT @currentAction = delete_referential_action_desc
FROM sys.foreign_keys
WHERE name = N'FK_zaznam_harmonogram_hodnoty_zaznam'
  AND parent_object_id = OBJECT_ID(N'dbo.zaznam_harmonogram_hodnoty');

IF @currentAction IS NULL
BEGIN
    PRINT 'FK_zaznam_harmonogram_hodnoty_zaznam neexistuje — přidávám s CASCADE.';
    -- Cleanup orphan rows pre-flight (FK by jinak fail).
    DELETE FROM dbo.zaznam_harmonogram_hodnoty
    WHERE zaznam_id NOT IN (SELECT id FROM dbo.projektove_zaznamy);

    ALTER TABLE dbo.zaznam_harmonogram_hodnoty
        ADD CONSTRAINT FK_zaznam_harmonogram_hodnoty_zaznam
        FOREIGN KEY (zaznam_id) REFERENCES dbo.projektove_zaznamy(id) ON DELETE CASCADE;
    PRINT '  + FK_zaznam_harmonogram_hodnoty_zaznam → CASCADE (added).';
END
ELSE IF @currentAction = N'CASCADE'
BEGIN
    PRINT 'FK_zaznam_harmonogram_hodnoty_zaznam už CASCADE, přeskakuji.';
END
ELSE
BEGIN
    PRINT 'FK_zaznam_harmonogram_hodnoty_zaznam má ' + @currentAction + ' — přepisuji na CASCADE.';
    ALTER TABLE dbo.zaznam_harmonogram_hodnoty
        DROP CONSTRAINT FK_zaznam_harmonogram_hodnoty_zaznam;

    -- Cleanup orphan rows (možná vznikly mezi DROP a ADD).
    DELETE FROM dbo.zaznam_harmonogram_hodnoty
    WHERE zaznam_id NOT IN (SELECT id FROM dbo.projektove_zaznamy);

    ALTER TABLE dbo.zaznam_harmonogram_hodnoty
        ADD CONSTRAINT FK_zaznam_harmonogram_hodnoty_zaznam
        FOREIGN KEY (zaznam_id) REFERENCES dbo.projektove_zaznamy(id) ON DELETE CASCADE;
    PRINT '  + FK_zaznam_harmonogram_hodnoty_zaznam → CASCADE (replaced).';
END
GO

-- Sanity check
SELECT
    fk.name                                  AS constraint_name,
    OBJECT_NAME(fk.parent_object_id)         AS child_table,
    fk.delete_referential_action_desc        AS on_delete
FROM sys.foreign_keys fk
WHERE fk.name = N'FK_zaznam_harmonogram_hodnoty_zaznam';
GO
