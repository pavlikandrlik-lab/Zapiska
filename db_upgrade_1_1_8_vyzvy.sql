-- db_upgrade_1_1_8_vyzvy.sql
-- ServiceDesk Výzvy — Fáze 1: schéma
-- Spec: docs/superpowers/specs/2026-04-20-servicedesk-integrace-vyzvy-design.md
-- Plan: docs/superpowers/plans/2026-04-20-servicedesk-vyzvy-faze-1-schema-backend.md

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1. Projekt: nová pole
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.projekty') AND name = 'misto_plneni')
BEGIN
    ALTER TABLE dbo.projekty ADD misto_plneni NVARCHAR(500) NULL;
    ALTER TABLE dbo.projekty ADD cislo_ramcove_smlouvy NVARCHAR(100) NULL;
END;

-- 2. Drop starého ciselnik_vyzvy (ověř prázdnost, safety check)
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ciselnik_vyzvy')
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.ciselnik_vyzvy)
        THROW 50000, 'ciselnik_vyzvy contains data — manual migration required', 1;

    DECLARE @fkName SYSNAME = (
        SELECT name FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID('dbo.zaznam_externi_odkazy')
          AND referenced_object_id = OBJECT_ID('dbo.ciselnik_vyzvy')
    );
    IF @fkName IS NOT NULL
        EXEC('ALTER TABLE dbo.zaznam_externi_odkazy DROP CONSTRAINT ' + @fkName);

    DROP TABLE dbo.ciselnik_vyzvy;
END;

-- 3. Rename zaznam_externi_odkazy.vyzva → vyzva_id + zaradid_do_vyzvy
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.zaznam_externi_odkazy') AND name = 'vyzva')
    EXEC sp_rename 'dbo.zaznam_externi_odkazy.vyzva', 'vyzva_id', 'COLUMN';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.zaznam_externi_odkazy') AND name = 'zaradid_do_vyzvy')
    ALTER TABLE dbo.zaznam_externi_odkazy
        ADD zaradid_do_vyzvy BIT NOT NULL CONSTRAINT df_zeo_zaradid DEFAULT 0;

-- 4. Vyzvy
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'vyzvy')
BEGIN
    CREATE TABLE dbo.vyzvy (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        projekt_id INT NOT NULL,
        kod NVARCHAR(20) NOT NULL,
        poradove_v_roce INT NOT NULL,
        rok INT NOT NULL,
        stav TINYINT NOT NULL,
        datum_zalozeni DATETIME2 NOT NULL,
        zalozil_osoba_id INT NOT NULL,
        datum_odeslani DATETIME2 NULL,
        odeslal_osoba_id INT NULL,
        misto_plneni_snapshot NVARCHAR(500) NOT NULL,
        cislo_ramcove_smlouvy_snapshot NVARCHAR(100) NOT NULL,
        CONSTRAINT fk_vyzvy_projekt FOREIGN KEY (projekt_id) REFERENCES dbo.projekty(id)
    );

    CREATE UNIQUE INDEX ux_vyzvy_smlouva_rok_poradove
        ON dbo.vyzvy (cislo_ramcove_smlouvy_snapshot, rok, poradove_v_roce);

    CREATE INDEX ix_vyzvy_projekt_datum_desc
        ON dbo.vyzvy (projekt_id, datum_zalozeni);
END;

-- 5. FK + filtered unique index
-- Pozn.: referujeme sloupec vyzva_id, který vznikl renamem v kroku 3. V rámci
-- jednoho batche parser sloupec nevidí, proto používáme dynamic SQL.
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'fk_zeo_vyzva')
BEGIN
    EXEC('ALTER TABLE dbo.zaznam_externi_odkazy
        ADD CONSTRAINT fk_zeo_vyzva
        FOREIGN KEY (vyzva_id) REFERENCES dbo.vyzvy(id) ON DELETE SET NULL');
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_zaznam_externi_odkazy_cislo_in_vyzve')
BEGIN
    EXEC('CREATE UNIQUE INDEX ux_zaznam_externi_odkazy_cislo_in_vyzve
        ON dbo.zaznam_externi_odkazy (cislo)
        WHERE vyzva_id IS NOT NULL');
END;

-- 6. Audit
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'vyzva_historie_stavu')
BEGIN
    CREATE TABLE dbo.vyzva_historie_stavu (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        vyzva_id INT NOT NULL,
        puvodni_stav TINYINT NULL,
        novy_stav TINYINT NOT NULL,
        datum_zmeny DATETIME2 NOT NULL,
        zmenil_osoba_id INT NOT NULL,
        CONSTRAINT fk_vhs_vyzva FOREIGN KEY (vyzva_id) REFERENCES dbo.vyzvy(id) ON DELETE CASCADE
    );

    CREATE INDEX ix_vyzva_historie_stavu_vyzva_id ON dbo.vyzva_historie_stavu (vyzva_id);
END;

COMMIT TRANSACTION;
