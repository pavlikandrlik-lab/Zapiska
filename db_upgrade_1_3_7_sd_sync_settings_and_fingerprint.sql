-- =============================================================================
-- db_upgrade_1_3_7_sd_sync_settings_and_fingerprint.sql
--
-- ServiceDesk sync konfigurace + fingerprint detekce pro zaznam_externi_odkazy.
-- Navazuje na db_upgrade_1_3_4_ad_sync_settings.sql (shared sync infra).
--
-- Spec: docs/superpowers/specs/2026-04-22-sync-infra-and-ad-design.md §4, §5
-- Plán: docs/superpowers/plans/2026-04-22-sd-sync-revise.md Task 1 / Task 5 / Task 6
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- 1) sd_active_sync_settings (singleton row id=1, HOT_ZAZNAMY.stav <> 'archiv')
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'sd_active_sync_settings')
BEGIN
    CREATE TABLE dbo.sd_active_sync_settings (
        id                   INT NOT NULL CONSTRAINT PK_sd_active_sync_settings PRIMARY KEY,
        is_enabled           BIT NOT NULL CONSTRAINT DF_sd_active_sync_enabled DEFAULT (0),
        period_minutes       INT NOT NULL CONSTRAINT DF_sd_active_sync_period DEFAULT (60),
        anchor_at            DATETIMEOFFSET NOT NULL,
        last_run_at          DATETIME2 NULL,
        last_trigger_kind    NVARCHAR(16) NULL,
        last_result_json     NVARCHAR(MAX) NULL,
        is_running           BIT NOT NULL CONSTRAINT DF_sd_active_sync_running DEFAULT (0),
        run_started_at       DATETIME2 NULL,
        updated_at           DATETIME2 NOT NULL CONSTRAINT DF_sd_active_sync_updated DEFAULT SYSUTCDATETIME(),
        updated_by_osoba_id  INT NULL
            CONSTRAINT FK_sd_active_sync_osoba REFERENCES dbo.osoby(id)
    );
    PRINT N'Tabulka sd_active_sync_settings vytvořena.';
END
ELSE
BEGIN
    PRINT N'Tabulka sd_active_sync_settings už existuje, přeskakuji CREATE.';
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.sd_active_sync_settings WHERE id = 1)
BEGIN
    INSERT INTO dbo.sd_active_sync_settings (id, is_enabled, period_minutes, anchor_at)
    VALUES (1, 0, 60, CAST('2026-01-01T00:00:00+00:00' AS DATETIMEOFFSET));
    PRINT N'Seed výchozího řádku sd_active_sync_settings id=1 vložen.';
END
ELSE
BEGIN
    PRINT N'Seed řádek sd_active_sync_settings id=1 už existuje, přeskakuji INSERT.';
END;
GO

-- 2) sd_archive_sync_settings (singleton row id=1, HOT_ZAZNAMY.stav = 'archiv')
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'sd_archive_sync_settings')
BEGIN
    CREATE TABLE dbo.sd_archive_sync_settings (
        id                   INT NOT NULL CONSTRAINT PK_sd_archive_sync_settings PRIMARY KEY,
        is_enabled           BIT NOT NULL CONSTRAINT DF_sd_archive_sync_enabled DEFAULT (0),
        period_minutes       INT NOT NULL CONSTRAINT DF_sd_archive_sync_period DEFAULT (1440),
        anchor_at            DATETIMEOFFSET NOT NULL,
        last_run_at          DATETIME2 NULL,
        last_trigger_kind    NVARCHAR(16) NULL,
        last_result_json     NVARCHAR(MAX) NULL,
        is_running           BIT NOT NULL CONSTRAINT DF_sd_archive_sync_running DEFAULT (0),
        run_started_at       DATETIME2 NULL,
        updated_at           DATETIME2 NOT NULL CONSTRAINT DF_sd_archive_sync_updated DEFAULT SYSUTCDATETIME(),
        updated_by_osoba_id  INT NULL
            CONSTRAINT FK_sd_archive_sync_osoba REFERENCES dbo.osoby(id)
    );
    PRINT N'Tabulka sd_archive_sync_settings vytvořena.';
END
ELSE
BEGIN
    PRINT N'Tabulka sd_archive_sync_settings už existuje, přeskakuji CREATE.';
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.sd_archive_sync_settings WHERE id = 1)
BEGIN
    INSERT INTO dbo.sd_archive_sync_settings (id, is_enabled, period_minutes, anchor_at)
    VALUES (1, 0, 1440, CAST('2026-01-01T04:00:00+00:00' AS DATETIMEOFFSET));
    PRINT N'Seed výchozího řádku sd_archive_sync_settings id=1 vložen.';
END
ELSE
BEGIN
    PRINT N'Seed řádek sd_archive_sync_settings id=1 už existuje, přeskakuji INSERT.';
END;
GO

-- 3) Fingerprint sloupce na zaznam_externi_odkazy (spec §5.2)
IF COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'last_known_hot_zaznam_datum') IS NULL
BEGIN
    ALTER TABLE dbo.zaznam_externi_odkazy
        ADD last_known_hot_zaznam_datum DATETIME2 NULL;
    PRINT N'Sloupec last_known_hot_zaznam_datum přidán.';
END
ELSE
BEGIN
    PRINT N'Sloupec last_known_hot_zaznam_datum už existuje.';
END;
GO

IF COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'last_known_max_vyjadreni_id') IS NULL
BEGIN
    ALTER TABLE dbo.zaznam_externi_odkazy
        ADD last_known_max_vyjadreni_id BIGINT NULL;
    PRINT N'Sloupec last_known_max_vyjadreni_id přidán.';
END
ELSE
BEGIN
    PRINT N'Sloupec last_known_max_vyjadreni_id už existuje.';
END;
GO

IF COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'last_known_vyjadreni_count') IS NULL
BEGIN
    ALTER TABLE dbo.zaznam_externi_odkazy
        ADD last_known_vyjadreni_count INT NULL;
    PRINT N'Sloupec last_known_vyjadreni_count přidán.';
END
ELSE
BEGIN
    PRINT N'Sloupec last_known_vyjadreni_count už existuje.';
END;
GO

PRINT N'db_upgrade_1_3_7 dokončen.';
