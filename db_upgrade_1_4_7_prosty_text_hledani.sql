-- =============================================================================
-- db_upgrade_1_4_7_prosty_text_hledani.sql
--
-- Čistý text formátovaných polí pro hledání (uživatel 2026-10-08).
--
-- KONTEXT: popis záznamu, text vyjádření a text požadavku do výzvy jsou HTML z editoru.
-- LIKE nad HTML nenajde frázi přes formátování ("pes a <b>kočka</b>") a naopak najde
-- samotné značky ("strong"). Aplikace k nim nově ukládá čistý text a hledá nad ním.
--
-- ROZSAH MIGRACE:
--   * Přidá NVARCHAR(MAX) NULL sloupce:
--       dbo.projektove_zaznamy.popis_prosty_text
--       dbo.vyjadreni.text_vyjadreni_prosty_text
--       dbo.zaznam_externi_odkazy.pozadavek_prosty_text
--   * Data NEMIGRUJE. Čistý text dopočte aplikace při prvním startu (T-SQL neumí HTML
--     spolehlivě odstranit) a dál ho plní při každém uložení.
--   * NEDOTKNE se žádné jiné tabulky ani indexu.
--
-- Idempotence: skript lze spustit opakovaně.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

PRINT N'[1.4.7] Čistý text pro hledání — start';

IF COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'pozadavek') IS NULL
BEGIN
    RAISERROR(N'Chybí sloupec dbo.zaznam_externi_odkazy.pozadavek — nejdřív spusť db_upgrade_1_4_2_externi_odkaz_pozadavek.sql.', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.projektove_zaznamy', N'popis_prosty_text') IS NULL
BEGIN
    ALTER TABLE dbo.projektove_zaznamy ADD popis_prosty_text NVARCHAR(MAX) NULL;
    PRINT N'[1.4.7] dbo.projektove_zaznamy.popis_prosty_text přidán';
END

IF COL_LENGTH(N'dbo.vyjadreni', N'text_vyjadreni_prosty_text') IS NULL
BEGIN
    ALTER TABLE dbo.vyjadreni ADD text_vyjadreni_prosty_text NVARCHAR(MAX) NULL;
    PRINT N'[1.4.7] dbo.vyjadreni.text_vyjadreni_prosty_text přidán';
END

IF COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'pozadavek_prosty_text') IS NULL
BEGIN
    ALTER TABLE dbo.zaznam_externi_odkazy ADD pozadavek_prosty_text NVARCHAR(MAX) NULL;
    PRINT N'[1.4.7] dbo.zaznam_externi_odkazy.pozadavek_prosty_text přidán';
END

COMMIT TRANSACTION;

PRINT N'[1.4.7] hotovo — čistý text dopočte aplikace při startu';
