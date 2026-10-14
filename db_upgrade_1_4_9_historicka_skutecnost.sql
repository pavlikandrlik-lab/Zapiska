-- =============================================================================
-- db_upgrade_1_4_9_historicka_skutecnost.sql
--
-- Převod historické skutečnosti harmonogramu (skutecnost_zdroj = 3) podle režimu záznamu
-- (uživatel 2026-10-14: „buď to dělám ručně, nebo to dělá automat, nic mezi“).
--
-- PŘED SPUŠTĚNÍM: spusť db_upgrade_1_4_9_historicka_skutecnost_analyza.sql a zkontroluj,
-- co se smaže a pro které úkoly. Udělej zálohu DB.
--
-- POŘADÍ: spustit PŘED startem aplikace verze 0.11 (nebo ji po skriptu restartovat).
-- Aplikace při startu doplní automatem prázdné kroky záznamů v automatickém režimu,
-- ke kterým existuje navázané vyjádření — tím se smazaná historická data nahradí daty
-- ze ServiceDesku.
--
-- Pravidla (stejná jako v analytickém skriptu):
--   * Záznam v RUČNÍM režimu (některý z kroků 1/3/4/6/7/10 má skutecnost_rezim = 1):
--       historické datum → ruční (skutecnost_zdroj = 2), krok do ručního režimu.
--   * Záznam v AUTOMATICKÉM režimu:
--       kroky 1/3/4/6/7/10 → datum se SMAŽE (skutecnost_zdroj = 0);
--       kroky 2/5/8/9 → ruční (automat je nikdy nevyplňuje).
--   * Historický zdroj bez data → skutecnost_zdroj = 0.
--
-- ROZSAH: mění jen řádky dbo.zaznam_harmonogram_krok se skutecnost_zdroj = 3.
-- Idempotence: po prvním běhu už žádný takový řádek není, další běh nic nezmění.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.zaznam_harmonogram_krok', N'U') IS NULL
    THROW 50000, N'Tabulka dbo.zaznam_harmonogram_krok neexistuje — nejdřív spusť db_upgrade_1_4_0_harmonogram_datum_model.sql.', 1;

BEGIN TRANSACTION;

PRINT N'[1.4.9] Historická skutečnost harmonogramu — start';

SELECT k.id,
       CASE WHEN k.skutecnost_datum IS NULL THEN 'ZDROJ'
            WHEN k.poradi IN (1, 3, 4, 6, 7, 10)
                 AND NOT EXISTS (SELECT 1 FROM dbo.zaznam_harmonogram_krok m
                                 WHERE m.zaznam_id = k.zaznam_id
                                   AND m.poradi IN (1, 3, 4, 6, 7, 10)
                                   AND m.skutecnost_rezim = 1)
                 THEN 'SMAZAT'
            ELSE 'RUCNI' END AS akce_kod
INTO #historicke
FROM dbo.zaznam_harmonogram_krok k WITH (UPDLOCK, HOLDLOCK)
WHERE k.skutecnost_zdroj = 3;

UPDATE k
SET skutecnost_datum = NULL,
    skutecnost_zdroj = 0,
    updated_at = SYSUTCDATETIME()
FROM dbo.zaznam_harmonogram_krok k
JOIN #historicke h ON h.id = k.id
WHERE h.akce_kod = 'SMAZAT';
PRINT N'[1.4.9] Smazáno historických dat (automatický režim, kroky 1/3/4/6/7/10): ' + CAST(@@ROWCOUNT AS NVARCHAR(10));

UPDATE k
SET skutecnost_zdroj = 2,
    skutecnost_rezim = 1,
    updated_at = SYSUTCDATETIME()
FROM dbo.zaznam_harmonogram_krok k
JOIN #historicke h ON h.id = k.id
WHERE h.akce_kod = 'RUCNI';
PRINT N'[1.4.9] Převedeno na ruční: ' + CAST(@@ROWCOUNT AS NVARCHAR(10));

UPDATE k
SET skutecnost_zdroj = 0,
    updated_at = SYSUTCDATETIME()
FROM dbo.zaznam_harmonogram_krok k
JOIN #historicke h ON h.id = k.id
WHERE h.akce_kod = 'ZDROJ';
PRINT N'[1.4.9] Opraven zdroj bez data: ' + CAST(@@ROWCOUNT AS NVARCHAR(10));

DROP TABLE #historicke;

COMMIT TRANSACTION;

PRINT N'[1.4.9] Hotovo. Prázdné kroky záznamů v automatickém režimu doplní aplikace při startu.';
