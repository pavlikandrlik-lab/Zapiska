-- db_upgrade_1_3_11_projekty_infosystem.sql
--
-- Plán 5 Sprint B Task 1 — vazba projektu na Informační systém v ServiceDesku.
-- Přidává sloupec projekty.servicedesk_info_system_id (INT NULL), který nese
-- logickou referenci na intranetNEW.dbo.HOT_IS.ID (FIS / ISSP).
-- Memory: project_servicedesk_infosystem_binding — projekt ↔ IS je 1:0..1,
-- řídí filtraci NES/Výzvy/SD metrik v projektovém dashboardu (Plán 5 Sprint B).
--
-- Poznámka: Žádný FK nezavádíme — HOT_IS je v cizí DB (intranetNEW),
-- ověření existence IS probíhá aplikačně přes hardkódovaný katalog
-- PmTracker.Web.Services.ServiceDesk.SdInfoSystemy.

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.projekty') AND name = 'servicedesk_info_system_id')
BEGIN
    ALTER TABLE dbo.projekty
        ADD servicedesk_info_system_id INT NULL;
    PRINT 'Added projekty.servicedesk_info_system_id column.';
END
ELSE
    PRINT 'projekty.servicedesk_info_system_id already exists, skipping.';
GO

-- Sanity check — kolik projektů zatím nemá napojení na IS.
SELECT COUNT(*) AS total_projekty,
       SUM(CASE WHEN servicedesk_info_system_id IS NULL THEN 1 ELSE 0 END) AS bez_is_napojeni
FROM dbo.projekty;
GO
