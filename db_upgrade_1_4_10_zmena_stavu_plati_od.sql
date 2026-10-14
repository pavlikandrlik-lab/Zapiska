-- =============================================================================
-- db_upgrade_1_4_10_zmena_stavu_plati_od.sql
--
-- Historie stavů úkolu: datum, ke kterému změna platí, a vazba na jednání.
--
-- KONTEXT (spec docs/superpowers/specs/2026-10-13-zmena-stavu-k-jednani-design.md):
--   Změna stavu dosud platila k okamžiku uložení. Zápis z jednání zpracovaný o pár dní
--   později pak v tisku toho jednání ukazoval stav, který k datu jednání ještě neplatil.
--   Uživatel nově volí, ke kterému datu změna platí: dnes, nebo datum jednání.
--
-- SÉMANTIKA:
--   * plati_od (DATE, NOT NULL) — místní datum, ke kterému změna platí. Jediné datum,
--     které čte tisk jednání i dashboard.
--   * jednani_id (INT, NULL) — jednání, ke kterému změna patří. Aplikace přes něj
--     při přeplánování jednání přepíše plati_od. Pro výpočty se nečte.
--   * datum_zmeny zůstává beze změny: okamžik uložení v UTC (audit).
--
-- ROZSAH MIGRACE:
--   * Přidá oba sloupce, cizí klíč na dbo.jednani (NO ACTION — vazbu ruší aplikace
--     při mazání jednání) a index pro přeplánování a mazání jednání.
--   * Existujícím řádkům doplní plati_od převodem datum_zmeny z UTC na místní datum
--     (Central Europe Standard Time). Opraví tím i posun o den u změn uložených
--     mezi 0:00 a 2:00 místního času.
--   * plati_od dostane výchozí hodnotu (dnešní datum serveru), aby stará verze aplikace
--     po spuštění skriptu dál mohla ukládat změny stavu.
--
-- POŽADAVEK: SQL Server 2016 nebo novější (AT TIME ZONE). Na starší verzi skript
--   skončí chybou a nic nezmění.
--
-- SPUŠTĚNÍ: pod účtem s db_owner na cílové databázi, PŘED startem nové verze aplikace
--   (bez sloupců aplikace nenastartuje — ohlásí název tohoto skriptu).
--   sqlcmd -S <SQL_HOST>\<INSTANCE> -E -d PM_Tracker -b -i db_upgrade_1_4_10_zmena_stavu_plati_od.sql
--
-- Idempotence: skript lze spustit opakovaně.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

PRINT N'[1.4.10] Změna stavu platná k datu — start';

IF CAST(SERVERPROPERTY('ProductMajorVersion') AS INT) < 13
BEGIN
    RAISERROR(N'Skript vyžaduje SQL Server 2016 nebo novější (AT TIME ZONE). Databáze zůstala beze změny.', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.zaznam_historie_stavu_zaznamu', N'plati_od') IS NULL
BEGIN
    ALTER TABLE dbo.zaznam_historie_stavu_zaznamu ADD plati_od DATE NULL;
    PRINT N'[1.4.10] sloupec plati_od přidán';
END

-- Dynamicky: sloupec mohl vzniknout až v tomto batchi, statický příkaz by neprošel překladem.
EXEC sp_executesql N'
UPDATE dbo.zaznam_historie_stavu_zaznamu
SET plati_od = CAST((datum_zmeny AT TIME ZONE ''UTC'') AT TIME ZONE ''Central Europe Standard Time'' AS date)
WHERE plati_od IS NULL;';

IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.zaznam_historie_stavu_zaznamu')
             AND name = N'plati_od'
             AND is_nullable = 1)
BEGIN
    EXEC sp_executesql N'ALTER TABLE dbo.zaznam_historie_stavu_zaznamu ALTER COLUMN plati_od DATE NOT NULL;';
    PRINT N'[1.4.10] plati_od doplněn u existujících řádků a nastaven jako NOT NULL';
END

-- Výchozí hodnota drží v chodu starou verzi aplikace mezi spuštěním skriptu a nasazením nové
-- (i při návratu na starou verzi): stará plati_od nezná a bez výchozí hodnoty by každá změna
-- stavu spadla na NOT NULL. Nová verze plati_od vždy posílá, výchozí hodnotu nepoužije.
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints
               WHERE parent_object_id = OBJECT_ID(N'dbo.zaznam_historie_stavu_zaznamu')
                 AND name = N'DF_zaznam_historie_stavu_zaznamu_plati_od')
BEGIN
    EXEC sp_executesql N'
    ALTER TABLE dbo.zaznam_historie_stavu_zaznamu
        ADD CONSTRAINT DF_zaznam_historie_stavu_zaznamu_plati_od
        DEFAULT (CONVERT(date, SYSDATETIME())) FOR plati_od;';
    PRINT N'[1.4.10] výchozí hodnota plati_od (dnešní datum serveru) přidána';
END

IF COL_LENGTH(N'dbo.zaznam_historie_stavu_zaznamu', N'jednani_id') IS NULL
BEGIN
    ALTER TABLE dbo.zaznam_historie_stavu_zaznamu ADD jednani_id INT NULL;
    PRINT N'[1.4.10] sloupec jednani_id přidán';
END

IF OBJECT_ID(N'dbo.FK_zaznam_historie_stavu_zaznamu_jednani', N'F') IS NULL
BEGIN
    EXEC sp_executesql N'
    ALTER TABLE dbo.zaznam_historie_stavu_zaznamu
        ADD CONSTRAINT FK_zaznam_historie_stavu_zaznamu_jednani
        FOREIGN KEY (jednani_id) REFERENCES dbo.jednani (id);';
    PRINT N'[1.4.10] cizí klíč na dbo.jednani přidán';
END

-- Obyčejný (ne filtrovaný) index: filtrovaný vyžaduje QUOTED_IDENTIFIER ON, který sqlcmd bez -I nezapíná.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'dbo.zaznam_historie_stavu_zaznamu')
                 AND name = N'IX_zaznam_historie_stavu_zaznamu_jednani_id')
BEGIN
    EXEC sp_executesql N'
    CREATE INDEX IX_zaznam_historie_stavu_zaznamu_jednani_id
        ON dbo.zaznam_historie_stavu_zaznamu (jednani_id);';
    PRINT N'[1.4.10] index jednani_id přidán';
END

COMMIT TRANSACTION;

PRINT N'[1.4.10] hotovo';
