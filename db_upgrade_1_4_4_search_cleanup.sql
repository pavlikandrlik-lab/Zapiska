-- =============================================================================
-- db_upgrade_1_4_4_search_cleanup.sql
--
-- Úklid po zrušené indexové vrstvě vyhledávání (spec 2026-09-17).
--
-- KONTEXT: Vyhledávání stálo na denormalizované tabulce dbo.SearchIndex plněné
--   na pozadí a na fulltextovém indexu. Produkční SQL Server nemá komponentu
--   Full-Text Search (IsFullTextInstalled = 0) a mít ji nebude; index navíc mohl
--   zestárnout, což se projevovalo jako „záznam nejde najít". Nové vyhledávání
--   čte rovnou z ostrých tabulek, takže obě tabulky i checkpoint jsou mrtvé.
--
-- ROZSAH MIGRACE:
--   * DROP dbo.SearchIndex — pokud existuje (na dev strojích ji mohla založit
--     stará runtime cesta; v produkci nevznikla, chybělo právo CREATE TABLE).
--   * DROP dbo.search_reindex_checkpoint — zavedena v db_upgrade_1_1_5.
--   * Smaže klíč `search.reindex` z authz (endpoint POST /Search/Reindex zaniká).
--     Klíč `search.index` ZŮSTÁVÁ — drží ho všech 12 rolí a vyhledávání funguje dál.
--     Kategorie SEARCH se proto NEMAŽE (na rozdíl od vzoru 1_4_1, kde osiřela).
--   * Žádná business data se nemažou.
--
-- SPUŠTĚNÍ: pod účtem s db_owner na cílové databázi.
--   sqlcmd -S <SQL_HOST>\<INSTANCE> -E -d PM_Tracker -b -i db_upgrade_1_4_4_search_cleanup.sql
--
-- Idempotence: skript lze spustit opakovaně.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

PRINT N'[1.4.4] Úklid vyhledávání — start';

-- -----------------------------------------------------------------------------
-- 1. Tabulky indexové vrstvy
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.SearchIndex', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.SearchIndex;
    PRINT N'[1.4.4] dbo.SearchIndex zrušena';
END
ELSE
    PRINT N'[1.4.4] dbo.SearchIndex neexistuje — přeskakuji';

IF OBJECT_ID(N'dbo.search_reindex_checkpoint', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.search_reindex_checkpoint;
    PRINT N'[1.4.4] dbo.search_reindex_checkpoint zrušena';
END
ELSE
    PRINT N'[1.4.4] dbo.search_reindex_checkpoint neexistuje — přeskakuji';

-- -----------------------------------------------------------------------------
-- 2. Authz úklid — klíč search.reindex
-- -----------------------------------------------------------------------------
DECLARE @permId INT = (SELECT id FROM authz.permissions WHERE klic = N'search.reindex');

IF @permId IS NULL
    PRINT N'[1.4.4] klíč search.reindex v authz není — přeskakuji';
ELSE
BEGIN
    -- Audit pre-state: které role klíč drží (očekávané: SUPERADMIN, APP_ADMIN).
    SELECT DrziKlic = r.kod
    FROM authz.role_permissions rp
    JOIN authz.roles r ON r.id = rp.role_id
    WHERE rp.permission_id = @permId;

    DELETE FROM authz.role_permission_projects
    WHERE role_permission_id IN (SELECT id FROM authz.role_permissions WHERE permission_id = @permId);

    DELETE FROM authz.role_permissions WHERE permission_id = @permId;
    DELETE FROM authz.permissions     WHERE id = @permId;

    PRINT N'[1.4.4] klíč search.reindex smazán';
END

-- Kategorie SEARCH se NEMAŽE — drží ji search.index, který zůstává.

-- -----------------------------------------------------------------------------
-- 3. Kontrola výsledku
-- -----------------------------------------------------------------------------
SELECT
    SearchIndexZrusena     = CASE WHEN OBJECT_ID(N'dbo.SearchIndex', N'U') IS NULL THEN N'ANO' ELSE N'NE' END,
    CheckpointZrusen       = CASE WHEN OBJECT_ID(N'dbo.search_reindex_checkpoint', N'U') IS NULL THEN N'ANO' ELSE N'NE' END,
    SearchReindexZrusen    = CASE WHEN NOT EXISTS (SELECT 1 FROM authz.permissions WHERE klic = N'search.reindex') THEN N'ANO' ELSE N'NE' END,
    SearchIndexKlicZustava = CASE WHEN EXISTS (SELECT 1 FROM authz.permissions WHERE klic = N'search.index') THEN N'ANO' ELSE N'CHYBA' END;

COMMIT TRANSACTION;

PRINT N'[1.4.4] hotovo';
