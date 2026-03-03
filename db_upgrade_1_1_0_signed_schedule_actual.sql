IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.zaznam_harmonogram_hodnoty')
      AND name = N'CK_zaznam_harmonogram_hodnoty_hodnota_nonnegative'
)
BEGIN
    ALTER TABLE dbo.zaznam_harmonogram_hodnoty
        DROP CONSTRAINT CK_zaznam_harmonogram_hodnoty_hodnota_nonnegative;
END
GO
