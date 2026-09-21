-- =============================================================================
-- db_check_search_collation.sql
--
-- Ověří, že vyhledávání najde česká slova napsaná bez diakritiky.
-- Spusť na PRODUKČNÍ databázi (stačí právo SELECT, nic nemění).
--
--   sqlcmd -S <SQL_HOST>\<INSTANCE> -E -d PM_Tracker -i db_check_search_collation.sql
--
-- Očekávaný výsledek: ve sloupci Verdikt samé OK.
-- Kdyby se objevilo CHYBA, aplikace by česká slova s háčkem nenašla a je potřeba
-- to nahlásit — viz SearchQueryText.AccentInsensitiveCollation.
-- =============================================================================
SET NOCOUNT ON;

PRINT N'Collation databáze: ' + CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS NVARCHAR(128));
PRINT N'Verze SQL Serveru:  ' + CAST(SERVERPROPERTY('ProductVersion') AS NVARCHAR(64));
PRINT N'';

DECLARE @p TABLE (poradi INT, pismeno NVARCHAR(20), text NVARCHAR(80), dotaz NVARCHAR(40));
INSERT @p VALUES
 (1, N'ř', N'Řízení projektu',  N'rizeni'),
 (2, N'č', N'Číslo jednání',    N'cislo'),
 (3, N'š', N'Šedý pruh',        N'sedy'),
 (4, N'ž', N'Žlutý stav',       N'zluty'),
 (5, N'ě', N'Tělo zprávy',      N'telo'),
 (6, N'ď', N'Ďábelský',         N'dabelsky'),
 (7, N'ů', N'Půlnoc',           N'pulnoc'),
 (8, N'á', N'Zálohování dat',   N'zalohovani'),
 (9, N'ý', N'Výzva',            N'vyzva');

SELECT
    Pismeno = pismeno,
    Text    = text,
    Dotaz   = dotaz,
    Verdikt = CASE WHEN text COLLATE Latin1_General_CI_AI LIKE N'%' + dotaz + N'%'
                   THEN N'OK' ELSE N'CHYBA - nenajde' END,
    -- Pro srovnání: proč se nepoužívá česká collation.
    Czech_CI_AI = CASE WHEN text COLLATE Czech_CI_AI LIKE N'%' + dotaz + N'%'
                       THEN N'najde' ELSE N'nenajde' END
FROM @p
ORDER BY poradi;

-- Totéž na skutečných datech, včetně non-Unicode sloupců popis a text_vyjadreni.
-- Ukáže, že COLLATE funguje přímo na sloupcích typu text a že vrácený text
-- zůstává s diakritikou.
SELECT TOP 5
    Zdroj        = N'projektove_zaznamy.nazev',
    Nalezeno     = nazev
FROM dbo.projektove_zaznamy
WHERE nazev COLLATE Latin1_General_CI_AI LIKE N'%a%'
UNION ALL
SELECT TOP 5
    N'projektove_zaznamy.popis (text)',
    CAST(popis AS NVARCHAR(200))
FROM dbo.projektove_zaznamy
WHERE popis IS NOT NULL
  AND popis COLLATE Latin1_General_CI_AI LIKE N'%a%'
UNION ALL
SELECT TOP 5
    N'vyjadreni.text_vyjadreni (text)',
    CAST(text_vyjadreni AS NVARCHAR(200))
FROM dbo.vyjadreni
WHERE text_vyjadreni IS NOT NULL
  AND text_vyjadreni COLLATE Latin1_General_CI_AI LIKE N'%a%';
