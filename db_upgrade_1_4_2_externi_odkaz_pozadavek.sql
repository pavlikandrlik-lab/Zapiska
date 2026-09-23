-- =============================================================================
-- db_upgrade_1_4_2_externi_odkaz_pozadavek.sql
--
-- Text požadavku u externí vazby (spec 2026-09-08-vyzva-pozadavek-text-design §5.2).
--
-- KONTEXT: Do výzvy se dosud tiskl popis tiketu z HOT_ZAZNAMY. Nově si text píše
-- pracovník sám u konkrétní PNF vazby v rich text editoru a do výzvy jde jen ten.
-- Ukládá se jako sanitizované HTML, proto NVARCHAR(MAX).
--
-- ROZSAH MIGRACE:
--   * Přidá sloupec dbo.zaznam_externi_odkazy.pozadavek NVARCHAR(MAX) NULL.
--   * Žádná data se nemigrují ani nedoplňují — stávající řádky zůstávají NULL
--     zcela záměrně (zadání uživatele 2026-09-08): popis tiketu se nikam nepřepisuje
--     a pracovník si vazby vyplní podle potřeby.
--   * NEDOTKNE se žádné jiné tabulky ani indexu.
--
-- Idempotence: skript lze spustit opakovaně.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

PRINT N'[1.4.2] zaznam_externi_odkazy.pozadavek — start';

IF OBJECT_ID(N'dbo.zaznam_externi_odkazy', N'U') IS NULL
BEGIN
    RAISERROR(N'Tabulka dbo.zaznam_externi_odkazy neexistuje — spusť dřívější upgrade skripty.', 16, 1);
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.zaznam_externi_odkazy')
                 AND name = N'pozadavek')
BEGIN
    ALTER TABLE dbo.zaznam_externi_odkazy ADD pozadavek NVARCHAR(MAX) NULL;
    PRINT N'[1.4.2] zaznam_externi_odkazy.pozadavek přidán';
END
ELSE
BEGIN
    PRINT N'[1.4.2] zaznam_externi_odkazy.pozadavek už existuje — přeskočeno';
END

PRINT N'[1.4.2] hotovo';
