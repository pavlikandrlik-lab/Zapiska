-- db_upgrade_1_3_14_delay_nullable.sql
--
-- Plán Harmonogram refactor 2026-05-01 — DESIGN-10-A.
-- ALTER zaznam_harmonogram_hodnoty.hodnota_int z NOT NULL na NULL.
-- Backfill: DELAY řádky (je_zpozdeni = 1) se Zdroj=Neznamo a HodnotaInt=0
--   = "krok ještě nenastal, default insert state" → NULL.
-- Řádky s Manual/Automat/Historicka keep — legitimní hodnoty, i 0 znamená
-- "vše šlo dle plánu" (krok dokončen včas).
-- DURATION řádky (je_zpozdeni = 0) se NEMĚNÍ — plán s 0 trváním je legitimní stav.

SET NOCOUNT ON;
GO

-- 1) ALTER NOT NULL → NULL
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.zaznam_harmonogram_hodnoty')
      AND name = 'hodnota_int'
      AND is_nullable = 0
)
BEGIN
    ALTER TABLE dbo.zaznam_harmonogram_hodnoty
        ALTER COLUMN hodnota_int INT NULL;
    PRINT 'Altered zaznam_harmonogram_hodnoty.hodnota_int to nullable.';
END
ELSE
    PRINT 'hodnota_int already nullable, skipping ALTER.';
GO

-- 2) Backfill: jen DELAY řádky s Zdroj=Neznamo a HodnotaInt=0 → NULL
IF EXISTS (
    SELECT 1
    FROM dbo.zaznam_harmonogram_hodnoty zhh
    INNER JOIN dbo.ciselnik_harmonogram_typu cht ON cht.id = zhh.typ_id
    WHERE cht.je_zpozdeni = 1
      AND zhh.skutecnost_zdroj = 0  -- Neznamo
      AND zhh.hodnota_int = 0
)
BEGIN
    UPDATE zhh
    SET zhh.hodnota_int = NULL
    FROM dbo.zaznam_harmonogram_hodnoty zhh
    INNER JOIN dbo.ciselnik_harmonogram_typu cht ON cht.id = zhh.typ_id
    WHERE cht.je_zpozdeni = 1
      AND zhh.skutecnost_zdroj = 0
      AND zhh.hodnota_int = 0;
    PRINT CONCAT('Backfilled ', @@ROWCOUNT, ' DELAY rows from 0 -> NULL (krok nenastal).');
END
ELSE
    PRINT 'No DELAY rows matching backfill criteria, skipping.';
GO

-- Sanity check
SELECT
    COUNT(*) AS total_delay_rows,
    SUM(CASE WHEN zhh.hodnota_int IS NULL THEN 1 ELSE 0 END) AS null_delays_krok_nenastal,
    SUM(CASE WHEN zhh.hodnota_int = 0 AND zhh.skutecnost_zdroj <> 0 THEN 1 ELSE 0 END) AS legit_zero_delays_dle_planu,
    SUM(CASE WHEN zhh.hodnota_int <> 0 THEN 1 ELSE 0 END) AS nonzero_delays_odchylka
FROM dbo.zaznam_harmonogram_hodnoty zhh
INNER JOIN dbo.ciselnik_harmonogram_typu cht ON cht.id = zhh.typ_id
WHERE cht.je_zpozdeni = 1;
GO
