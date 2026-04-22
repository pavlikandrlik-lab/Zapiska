-- =============================================================================
-- db_upgrade_1_3_5_external_link_harvested_at.sql
-- Přidá sloupec last_harvested_at do zaznam_externi_odkazy pro tracking
-- posledního auto-harvestu ze ServiceDesku.
-- Plán B — Externí vazba v2 + auto-sync ServiceDesk (Task 1).
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF COL_LENGTH('dbo.zaznam_externi_odkazy', 'last_harvested_at') IS NULL
BEGIN
    ALTER TABLE dbo.zaznam_externi_odkazy
        ADD last_harvested_at DATETIME2 NULL;

    PRINT N'Sloupec last_harvested_at přidán.';
END
ELSE
BEGIN
    PRINT N'Sloupec last_harvested_at už existuje, přeskakuji.';
END
GO
