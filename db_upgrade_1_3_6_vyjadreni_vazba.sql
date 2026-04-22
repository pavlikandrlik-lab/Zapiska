-- db_upgrade_1_3_6_vyjadreni_vazba.sql
-- Idempotent: creates zaznam_harmonogram_vyjadreni_vazba table for Plán C
-- (chat modal + vyjádření harvest). Maps one HOT_VYJADRENI to one harmonogram
-- step (KrokKey) per projektový záznam. Source = 1 (Auto) or 2 (Manual).
-- Stav = 1 (Active), 2 (Superseded), 3 (Deleted) — append-only with history.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'zaznam_harmonogram_vyjadreni_vazba')
BEGIN
    CREATE TABLE dbo.zaznam_harmonogram_vyjadreni_vazba
    (
        id                  INT IDENTITY PRIMARY KEY,
        zaznam_id           INT NOT NULL
            CONSTRAINT FK_zhvv_zaznam REFERENCES dbo.projektove_zaznamy(id) ON DELETE CASCADE,
        krok_key            UNIQUEIDENTIFIER NOT NULL,
        externi_odkaz_id    INT NOT NULL
            CONSTRAINT FK_zhvv_externi_odkaz REFERENCES dbo.zaznam_externi_odkazy(id) ON DELETE NO ACTION,
        hot_vyjadreni_id    BIGINT NOT NULL,
        datum_vyjadreni     DATETIME2 NOT NULL,
        source              TINYINT NOT NULL,
        stav                TINYINT NOT NULL,
        created_at          DATETIME2 NOT NULL CONSTRAINT DF_zhvv_created_at DEFAULT SYSUTCDATETIME(),
        created_by_osoba_id INT NULL
            CONSTRAINT FK_zhvv_osoba_created REFERENCES dbo.osoby(id),
        deleted_at          DATETIME2 NULL,
        deleted_by_osoba_id INT NULL
            CONSTRAINT FK_zhvv_osoba_deleted REFERENCES dbo.osoby(id)
    );

    CREATE INDEX ix_zhvv_zaznam_krok_stav
        ON dbo.zaznam_harmonogram_vyjadreni_vazba(zaznam_id, krok_key, stav);
    CREATE INDEX ix_zhvv_externi_odkaz
        ON dbo.zaznam_harmonogram_vyjadreni_vazba(externi_odkaz_id);

    PRINT N'Tabulka zaznam_harmonogram_vyjadreni_vazba vytvořena.';
END
ELSE
BEGIN
    PRINT N'Tabulka zaznam_harmonogram_vyjadreni_vazba už existuje.';
END;
GO
