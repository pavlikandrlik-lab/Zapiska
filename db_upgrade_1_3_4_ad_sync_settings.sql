-- =============================================================================
-- db_upgrade_1_3_4_ad_sync_settings.sql
-- Singleton settings row pro AD periodic sync job.
-- Spec: docs/superpowers/specs/2026-04-22-sync-infra-and-ad-design.md §3.1
-- Plán: docs/superpowers/plans/2026-04-22-sync-infra-and-ad.md Task 8
-- Pozn.: Očíslování 1_3_4 (plán zmiňuje 1_3_0, ale to + _1/_2/_3 jsou obsazené).
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ad_sync_settings')
BEGIN
    CREATE TABLE dbo.ad_sync_settings
    (
        id                   INT NOT NULL
            CONSTRAINT PK_ad_sync_settings PRIMARY KEY,
        is_enabled           BIT NOT NULL
            CONSTRAINT DF_ad_sync_settings_enabled DEFAULT (0),
        period_minutes       INT NOT NULL
            CONSTRAINT DF_ad_sync_settings_period DEFAULT (360),
        anchor_at            DATETIMEOFFSET NOT NULL,
        last_run_at          DATETIME2 NULL,
        last_trigger_kind    NVARCHAR(16) NULL,
        last_result_json     NVARCHAR(MAX) NULL,
        is_running           BIT NOT NULL
            CONSTRAINT DF_ad_sync_settings_running DEFAULT (0),
        run_started_at       DATETIME2 NULL,
        updated_at           DATETIME2 NOT NULL
            CONSTRAINT DF_ad_sync_settings_updated DEFAULT SYSUTCDATETIME(),
        updated_by_osoba_id  INT NULL
            CONSTRAINT FK_ad_sync_settings_osoba REFERENCES dbo.osoby(id)
    );

    PRINT N'Tabulka ad_sync_settings vytvořena.';
END
ELSE
BEGIN
    PRINT N'Tabulka ad_sync_settings už existuje, přeskakuji CREATE.';
END;
GO

-- Seed singleton row (idempotentní)
IF NOT EXISTS (SELECT 1 FROM dbo.ad_sync_settings WHERE id = 1)
BEGIN
    -- Pozn.: SYSUTCDATETIMEOFFSET() NENÍ vestavěná T-SQL funkce (ani v SQL Serveru,
    -- ani v Azure SQL Edge). Ekvivalent „UTC now jako datetimeoffset" =
    -- TODATETIMEOFFSET(SYSUTCDATETIME(), 0). Opraveno 2026-04-22.
    INSERT INTO dbo.ad_sync_settings (id, is_enabled, period_minutes, anchor_at)
    VALUES (1, 0, 360, TODATETIMEOFFSET(SYSUTCDATETIME(), 0));
    PRINT N'Seed výchozího řádku ad_sync_settings id=1 vložen.';
END
ELSE
BEGIN
    PRINT N'Seed řádek ad_sync_settings id=1 už existuje, přeskakuji INSERT.';
END;
GO
