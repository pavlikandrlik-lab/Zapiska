SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.obsazeni_projektu', 'datum_prirazeni') IS NULL
BEGIN
    ALTER TABLE dbo.obsazeni_projektu
        ADD datum_prirazeni datetime2 NOT NULL CONSTRAINT DF_obsazeni_projektu_datum_prirazeni DEFAULT (SYSUTCDATETIME());
END
GO

IF COL_LENGTH('dbo.obsazeni_projektu', 'datum_odebrani') IS NULL
BEGIN
    ALTER TABLE dbo.obsazeni_projektu
        ADD datum_odebrani datetime2 NULL;
END
GO

IF OBJECT_ID('dbo.ciselnik_roli_subsystemu', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ciselnik_roli_subsystemu (
        id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ciselnik_roli_subsystemu PRIMARY KEY,
        kod nvarchar(255) NOT NULL,
        nazev nvarchar(255) NOT NULL,
        is_locked bit NOT NULL CONSTRAINT DF_ciselnik_roli_subsystemu_is_locked DEFAULT (0),
        CONSTRAINT UQ_ciselnik_roli_subsystemu_kod UNIQUE (kod)
    );
END
GO

IF OBJECT_ID('dbo.projekt_subsystemy', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.projekt_subsystemy (
        id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_projekt_subsystemy PRIMARY KEY,
        projekt_id int NOT NULL,
        subsystem_id int NOT NULL,
        datum_prirazeni datetime2 NOT NULL CONSTRAINT DF_projekt_subsystemy_datum_prirazeni DEFAULT (SYSUTCDATETIME()),
        datum_odebrani datetime2 NULL
    );

    ALTER TABLE dbo.projekt_subsystemy ADD CONSTRAINT FK_projekt_subsystemy_projekt FOREIGN KEY (projekt_id) REFERENCES dbo.projekty(id);
    ALTER TABLE dbo.projekt_subsystemy ADD CONSTRAINT FK_projekt_subsystemy_subsystem FOREIGN KEY (subsystem_id) REFERENCES dbo.subsystemy(id);
END
GO

IF OBJECT_ID('dbo.obsazeni_subsystemu_projektu', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.obsazeni_subsystemu_projektu (
        id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_obsazeni_subsystemu_projektu PRIMARY KEY,
        projekt_subsystem_id int NOT NULL,
        osoba_id int NOT NULL,
        role_subsystemu_id int NOT NULL,
        datum_prirazeni datetime2 NOT NULL CONSTRAINT DF_obsazeni_subsystemu_projektu_datum_prirazeni DEFAULT (SYSUTCDATETIME()),
        datum_odebrani datetime2 NULL
    );

    ALTER TABLE dbo.obsazeni_subsystemu_projektu ADD CONSTRAINT FK_obsazeni_subsystemu_projektu_projekt_subsystem FOREIGN KEY (projekt_subsystem_id) REFERENCES dbo.projekt_subsystemy(id);
    ALTER TABLE dbo.obsazeni_subsystemu_projektu ADD CONSTRAINT FK_obsazeni_subsystemu_projektu_osoba FOREIGN KEY (osoba_id) REFERENCES dbo.osoby(id);
    ALTER TABLE dbo.obsazeni_subsystemu_projektu ADD CONSTRAINT FK_obsazeni_subsystemu_projektu_role FOREIGN KEY (role_subsystemu_id) REFERENCES dbo.ciselnik_roli_subsystemu(id);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_obsazeni_projektu_projekt_osoba_role_aktivni' AND object_id = OBJECT_ID('dbo.obsazeni_projektu'))
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_obsazeni_projektu_projekt_osoba_role' AND object_id = OBJECT_ID('dbo.obsazeni_projektu'))
        DROP INDEX UX_obsazeni_projektu_projekt_osoba_role ON dbo.obsazeni_projektu;

    CREATE UNIQUE INDEX UX_obsazeni_projektu_projekt_osoba_role_aktivni
        ON dbo.obsazeni_projektu(projekt_id, osoba_id, role_id)
        WHERE datum_odebrani IS NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_projekt_subsystemy_projekt_subsystem_aktivni' AND object_id = OBJECT_ID('dbo.projekt_subsystemy'))
BEGIN
    CREATE UNIQUE INDEX UX_projekt_subsystemy_projekt_subsystem_aktivni
        ON dbo.projekt_subsystemy(projekt_id, subsystem_id)
        WHERE datum_odebrani IS NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_obsazeni_subsystemu_projektu_aktivni' AND object_id = OBJECT_ID('dbo.obsazeni_subsystemu_projektu'))
BEGIN
    CREATE UNIQUE INDEX UX_obsazeni_subsystemu_projektu_aktivni
        ON dbo.obsazeni_subsystemu_projektu(projekt_subsystem_id, osoba_id, role_subsystemu_id)
        WHERE datum_odebrani IS NULL;
END
GO

MERGE dbo.ciselnik_roli_projektu AS target
USING (VALUES
    (N'VLASTNIK_PROJEKTU', N'Vlastník projektu', 1),
    (N'HOST', N'Host', 1),
    (N'ADM_PROJ', N'Administrátor projektu', 1)
) AS source(kod, nazev, is_locked)
ON target.kod = source.kod
WHEN NOT MATCHED BY TARGET THEN
    INSERT (kod, nazev, is_locked) VALUES (source.kod, source.nazev, source.is_locked)
WHEN MATCHED THEN
    UPDATE SET target.nazev = source.nazev, target.is_locked = source.is_locked;
GO

MERGE dbo.ciselnik_roli_subsystemu AS target
USING (VALUES
    (N'VEDOUCI_SUBSYSTEMU', N'Vedoucí subsystému', 1),
    (N'ZASTUPCE_VEDOUCIHO_SUBSYSTEMU', N'Zástupce vedoucího subsystému', 1),
    (N'METODIK_SUBSYSTEMU', N'Metodik subsystému', 1)
) AS source(kod, nazev, is_locked)
ON target.kod = source.kod
WHEN NOT MATCHED BY TARGET THEN
    INSERT (kod, nazev, is_locked) VALUES (source.kod, source.nazev, source.is_locked)
WHEN MATCHED THEN
    UPDATE SET target.nazev = source.nazev, target.is_locked = source.is_locked;
GO

INSERT INTO dbo.projekt_subsystemy (projekt_id, subsystem_id)
SELECT source.projekt_id, source.subsystem_id
FROM (
    SELECT DISTINCT projekt_id, subsystem_id
    FROM dbo.projektove_zaznamy
    WHERE subsystem_id IS NOT NULL
) AS source
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.projekt_subsystemy existing
    WHERE existing.projekt_id = source.projekt_id
      AND existing.subsystem_id = source.subsystem_id
      AND existing.datum_odebrani IS NULL
);
GO

UPDATE dbo.obsazeni_projektu
SET datum_prirazeni = SYSUTCDATETIME()
WHERE datum_prirazeni IS NULL;
GO

DECLARE @leadRoleId int = (
    SELECT TOP (1) id
    FROM dbo.ciselnik_roli_subsystemu
    WHERE kod = N'VEDOUCI_SUBSYSTEMU'
    ORDER BY id
);

IF @leadRoleId IS NOT NULL AND COL_LENGTH('dbo.subsystemy', 'vedouci_osoba_id') IS NOT NULL
BEGIN
    INSERT INTO dbo.obsazeni_subsystemu_projektu (projekt_subsystem_id, osoba_id, role_subsystemu_id)
    SELECT ps.id, s.vedouci_osoba_id, @leadRoleId
    FROM dbo.projekt_subsystemy ps
    INNER JOIN dbo.subsystemy s ON s.id = ps.subsystem_id
    WHERE s.vedouci_osoba_id IS NOT NULL
      AND NOT EXISTS (
          SELECT 1
          FROM dbo.obsazeni_subsystemu_projektu existing
          WHERE existing.projekt_subsystem_id = ps.id
            AND existing.osoba_id = s.vedouci_osoba_id
            AND existing.role_subsystemu_id = @leadRoleId
            AND existing.datum_odebrani IS NULL
      );
END
GO
