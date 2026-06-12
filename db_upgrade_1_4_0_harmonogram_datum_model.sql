-- db_upgrade_1_4_0_harmonogram_datum_model.sql
-- Harmonogram: prechod offset -> datum model + 10 fixnich kroku (zruseni sablon/verzovani).
-- Data harmonogramu jsou zahoditelna -> DROP/CREATE bez backfillu; po nasazeni znovu vytezit.
-- POZADI: spustit az po nasazeni kodu Faze 7 (smazani starych EF entit), jinak EF startup mismatch.
SET XACT_ABORT ON;
BEGIN TRAN;

-- 1) Nova tabulka kroku (10 radku na zaznam: plan_datum + skutecnost_datum + audit)
IF OBJECT_ID('dbo.zaznam_harmonogram_krok','U') IS NULL
BEGIN
    CREATE TABLE dbo.zaznam_harmonogram_krok (
        id INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_zaznam_harmonogram_krok PRIMARY KEY,
        zaznam_id INT NOT NULL,
        poradi TINYINT NOT NULL,
        plan_datum DATE NULL,
        skutecnost_datum DATE NULL,
        skutecnost_zdroj TINYINT NOT NULL CONSTRAINT DF_zhk_zdroj DEFAULT(0),
        skutecnost_rezim TINYINT NOT NULL CONSTRAINT DF_zhk_rezim DEFAULT(0),
        preferred_externi_odkaz_id INT NULL,
        updated_at DATETIME2 NOT NULL CONSTRAINT DF_zhk_updated DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT FK_zhk_zaznam FOREIGN KEY (zaznam_id)
            REFERENCES dbo.projektove_zaznamy(id) ON DELETE CASCADE,
        CONSTRAINT UQ_zaznam_harmonogram_krok_zaznam_poradi UNIQUE (zaznam_id, poradi)
    );
END

-- 2) Vazby vytezeni: krok_key (uniqueidentifier) -> poradi (tinyint).
--    Data zahoditelna: stare vazby smazat, harvest je vytvori znovu pres poradi.
IF COL_LENGTH('dbo.zaznam_harmonogram_vyjadreni_vazba','poradi') IS NULL
    ALTER TABLE dbo.zaznam_harmonogram_vyjadreni_vazba ADD poradi TINYINT NOT NULL CONSTRAINT DF_vazba_poradi DEFAULT(0);
DELETE FROM dbo.zaznam_harmonogram_vyjadreni_vazba;
-- index ix_zhvv_zaznam_krok_stav obsahuje krok_key -> dropnout pred DROP COLUMN
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_zhvv_zaznam_krok_stav'
           AND object_id = OBJECT_ID('dbo.zaznam_harmonogram_vyjadreni_vazba'))
    DROP INDEX ix_zhvv_zaznam_krok_stav ON dbo.zaznam_harmonogram_vyjadreni_vazba;
IF COL_LENGTH('dbo.zaznam_harmonogram_vyjadreni_vazba','krok_key') IS NOT NULL
    ALTER TABLE dbo.zaznam_harmonogram_vyjadreni_vazba DROP COLUMN krok_key;
-- novy index pres poradi (zrcadli EF mapping ve Fazi 5)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_zhvv_zaznam_poradi_stav'
               AND object_id = OBJECT_ID('dbo.zaznam_harmonogram_vyjadreni_vazba'))
    CREATE INDEX ix_zhvv_zaznam_poradi_stav
        ON dbo.zaznam_harmonogram_vyjadreni_vazba (zaznam_id, poradi, stav);

-- 3) Drop stary offset model + sloupec verze na zaznamu + FK na sablony
IF OBJECT_ID('dbo.zaznam_harmonogram_hodnoty','U') IS NOT NULL
    DROP TABLE dbo.zaznam_harmonogram_hodnoty;

DECLARE @fk SYSNAME = (
    SELECT fk.name FROM sys.foreign_keys fk
    WHERE fk.parent_object_id = OBJECT_ID('dbo.projektove_zaznamy')
      AND fk.referenced_object_id = OBJECT_ID('dbo.harmonogram_sablony'));
IF @fk IS NOT NULL
    EXEC('ALTER TABLE dbo.projektove_zaznamy DROP CONSTRAINT ' + @fk);

IF COL_LENGTH('dbo.projektove_zaznamy','harmonogram_sablona_verze') IS NOT NULL
    ALTER TABLE dbo.projektove_zaznamy DROP COLUMN harmonogram_sablona_verze;

IF OBJECT_ID('dbo.ciselnik_harmonogram_typu','U') IS NOT NULL
    DROP TABLE dbo.ciselnik_harmonogram_typu;
IF OBJECT_ID('dbo.harmonogram_sablony','U') IS NOT NULL
    DROP TABLE dbo.harmonogram_sablony;

COMMIT;
