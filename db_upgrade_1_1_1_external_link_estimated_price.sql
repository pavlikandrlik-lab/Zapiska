SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.zaznam_externi_odkazy', 'predpokladana_cena') IS NULL
BEGIN
    ALTER TABLE dbo.zaznam_externi_odkazy
        ADD predpokladana_cena decimal(18,2) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID('dbo.zaznam_externi_odkazy')
      AND name = 'CK_zaznam_externi_odkazy_predpokladana_cena_nonnegative')
BEGIN
    ALTER TABLE dbo.zaznam_externi_odkazy
        ADD CONSTRAINT CK_zaznam_externi_odkazy_predpokladana_cena_nonnegative
        CHECK (predpokladana_cena IS NULL OR predpokladana_cena >= 0);
END
GO
