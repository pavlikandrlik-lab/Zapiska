-- =============================================================================
-- db_upgrade_1_4_3_externi_odkaz_kalkulace.sql
--
-- Snímek skutečné ceny PNF z akceptované kalkulace (spec 2026-09-10 část A).
--
-- KONTEXT: Aplikace ukazuje skutečnou cenu z HOT_KALKULACE místo předpokládané. Aby se
-- nečetla cizí databáze při každém vykreslení karet, harvest ji ukládá jako snímek u vazby.
--
-- ROZSAH MIGRACE:
--   * Přidá do dbo.zaznam_externi_odkazy sloupce kalkulace_cena, kalkulace_id,
--     kalkulace_nacteno — všechny NULL.
--   * Žádná data se nemigrují; doplní je první běh synchronizace se ServiceDeskem.
--   * Předpokládaná cena (predpokladana_cena) se nemění.
--
-- Idempotence: skript lze spustit opakovaně.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

PRINT N'[1.4.3] zaznam_externi_odkazy.kalkulace_* — start';

IF OBJECT_ID(N'dbo.zaznam_externi_odkazy', N'U') IS NULL
BEGIN
    RAISERROR(N'Tabulka dbo.zaznam_externi_odkazy neexistuje — spusť dřívější upgrade skripty.', 16, 1);
    RETURN;
END

IF COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'kalkulace_cena') IS NULL
    ALTER TABLE dbo.zaznam_externi_odkazy ADD kalkulace_cena DECIMAL(18,2) NULL;

IF COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'kalkulace_id') IS NULL
    ALTER TABLE dbo.zaznam_externi_odkazy ADD kalkulace_id BIGINT NULL;

IF COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'kalkulace_nacteno') IS NULL
    ALTER TABLE dbo.zaznam_externi_odkazy ADD kalkulace_nacteno DATETIME2 NULL;

PRINT N'[1.4.3] hotovo';
