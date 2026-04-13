SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.projekt_subsystemy', 'poradi') IS NULL
BEGIN
    ALTER TABLE dbo.projekt_subsystemy
        ADD poradi int NULL;
END
GO

;WITH ordered AS (
    SELECT
        ps.id,
        poradi = ROW_NUMBER() OVER (
            PARTITION BY ps.projekt_id
            ORDER BY
                CASE WHEN ps.datum_odebrani IS NULL THEN 0 ELSE 1 END,
                ISNULL(s.[kód], N''),
                ISNULL(s.nazev, N''),
                ps.id)
    FROM dbo.projekt_subsystemy ps
    INNER JOIN dbo.subsystemy s ON s.id = ps.subsystem_id
)
UPDATE ps
SET poradi = ordered.poradi
FROM dbo.projekt_subsystemy ps
INNER JOIN ordered ON ordered.id = ps.id
WHERE ps.poradi IS NULL;
GO

IF EXISTS (
    SELECT 1
    FROM dbo.projekt_subsystemy
    WHERE poradi IS NULL
)
BEGIN
    THROW 51000, N'Nepodařilo se doplnit poradi pro všechny řádky v dbo.projekt_subsystemy.', 1;
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.projekt_subsystemy')
      AND name = N'UX_projekt_subsystemy_projekt_poradi_aktivni'
)
BEGIN
    DROP INDEX UX_projekt_subsystemy_projekt_poradi_aktivni
        ON dbo.projekt_subsystemy;
END
GO

ALTER TABLE dbo.projekt_subsystemy
    ALTER COLUMN poradi int NOT NULL;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.projekt_subsystemy')
      AND name = N'UX_projekt_subsystemy_projekt_poradi_aktivni'
)
BEGIN
    CREATE UNIQUE INDEX UX_projekt_subsystemy_projekt_poradi_aktivni
        ON dbo.projekt_subsystemy(projekt_id, poradi)
        WHERE datum_odebrani IS NULL;
END
GO
