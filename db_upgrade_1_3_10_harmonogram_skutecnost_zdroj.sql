-- db_upgrade_1_3_10_harmonogram_skutecnost_zdroj.sql
--
-- Plán 4 Feature C Task 1 — SkutecnostZdroj / SkutecnostRezim / PreferredExterniOdkazId
-- na tabulce zaznam_harmonogram_hodnoty (per-krok per-záznam hodnoty harmonogramu).
--
-- Pozn.: Původní plán odkazoval na tabulku "projektovy_zaznam_harmonogram", ta v tomto
-- schématu neexistuje. Skutečnost v current architektuře je ukládána jako HS0X_DELAY
-- hodnota v zaznam_harmonogram_hodnoty (odchylka plán vs. skutečnost ve dnech) —
-- viz docs/superpowers/specs/2026-04-21-servicedesk-vytezovani-vyjadreni-design.md §4.4.
-- Nové sloupce doplňují audit zdroje + switch Auto/Ručně + volbu preferred kandidáta.
--
-- Enums (byte):
--   SkutecnostZdroj   : 0=Neznamo, 1=Automat, 2=Manual, 3=Historicka
--   SkutecnostRezim   : 0=Auto, 1=Manual
--
-- Backfill: existující HS0X_DELAY řádky s nenulovou hodnotou dostávají Zdroj=Historicka.

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.zaznam_harmonogram_hodnoty') AND name = 'skutecnost_zdroj')
BEGIN
    ALTER TABLE dbo.zaznam_harmonogram_hodnoty
        ADD skutecnost_zdroj TINYINT NOT NULL CONSTRAINT DF_zhh_skutecnost_zdroj DEFAULT (0);

    -- Existující řádky s nenulovým delay jsou „Historicka" (migrovaná ruční skutečnost
    -- před zavedením auto-fill ze SD vyjádření).
    UPDATE dbo.zaznam_harmonogram_hodnoty
       SET skutecnost_zdroj = 3 -- Historicka
     WHERE hodnota_int <> 0;

    PRINT 'Added zaznam_harmonogram_hodnoty.skutecnost_zdroj (default Neznamo=0; existing delays = Historicka=3).';
END
ELSE
    PRINT 'skutecnost_zdroj column already exists, skipping.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.zaznam_harmonogram_hodnoty') AND name = 'skutecnost_rezim')
BEGIN
    ALTER TABLE dbo.zaznam_harmonogram_hodnoty
        ADD skutecnost_rezim TINYINT NOT NULL CONSTRAINT DF_zhh_skutecnost_rezim DEFAULT (0);

    PRINT 'Added zaznam_harmonogram_hodnoty.skutecnost_rezim (default Auto=0).';
END
ELSE
    PRINT 'skutecnost_rezim column already exists, skipping.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.zaznam_harmonogram_hodnoty') AND name = 'preferred_externi_odkaz_id')
BEGIN
    ALTER TABLE dbo.zaznam_harmonogram_hodnoty
        ADD preferred_externi_odkaz_id INT NULL;

    PRINT 'Added zaznam_harmonogram_hodnoty.preferred_externi_odkaz_id (user dropdown choice persistence).';
END
ELSE
    PRINT 'preferred_externi_odkaz_id column already exists, skipping.';
GO

-- Sanity check
SELECT
    COUNT(*) AS total_rows,
    SUM(CASE WHEN skutecnost_zdroj = 3 THEN 1 ELSE 0 END) AS historicka_count,
    SUM(CASE WHEN skutecnost_zdroj = 0 THEN 1 ELSE 0 END) AS neznamo_count,
    SUM(CASE WHEN skutecnost_rezim = 0 THEN 1 ELSE 0 END) AS rezim_auto,
    SUM(CASE WHEN skutecnost_rezim = 1 THEN 1 ELSE 0 END) AS rezim_manual,
    SUM(CASE WHEN preferred_externi_odkaz_id IS NOT NULL THEN 1 ELSE 0 END) AS with_preferred
FROM dbo.zaznam_harmonogram_hodnoty;
GO
