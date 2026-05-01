-- sd_diagnostika_hot_zaznamy.sql
--
-- Diagnostika ServiceDesk integrace — proč /SDConnector/Inspect padá s HTTP 500
-- pro existující ticket, ale neexistující správně odhlásí jako „neexistuje".
--
-- Hypotéza: SqlTicketingQueryService.GetZaznamAsync materializuje plnou
-- HotZaznamEntity (PmTracker.ServiceDesk.Sql/Entities/HotZaznamEntity.cs).
-- Pokud má reálná HOT_ZAZNAMY tabulka NULL v non-nullable property
-- (Radek/Id/Schvaleno/PriznakZamceni), EF Core hodí SqlNullValueException.
--
-- Spusť POSTUPNĚ. Pošli výstupy 1+2 zpět — z toho identifikuju konkrétní
-- code fix (změna non-nullable na nullable v entitě).
--
-- Jak používat:
--   1) Otevři v SSMS, přepni context na databázi se SD kopií.
--   2) Spusť dotazy 1, 2, 3 postupně (F5 nad každou sekcí).
--   3) Pošli mi výstupy.

SET NOCOUNT ON;

-- =====================================================================
-- 1) Schema kontrola — které sloupce reálně máš + jejich nullability/typy
-- =====================================================================
-- Očekávané non-nullable (musí být IS_NULLABLE = 'NO'):
--   radek            (BIGINT  / INT      / TINYINT)
--   id               (NVARCHAR / VARCHAR)
--   schvaleno        (BIT)
--   priznak_zamceni  (TINYINT)
-- Pokud má kterýkoli z nich IS_NULLABLE = 'YES', spusť dotaz 2 ať zjistíme
-- jestli reálně někde NULL je.

SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH
FROM   INFORMATION_SCHEMA.COLUMNS
WHERE  TABLE_NAME   = 'HOT_ZAZNAMY'
  AND  TABLE_SCHEMA = 'dbo'
ORDER  BY COLUMN_NAME;

-- =====================================================================
-- 2) Datová kontrola — jsou někde reálně NULL hodnoty v non-nullable
-- =====================================================================
-- Pokud má kterýkoli sloupec count > 0, máme jasného viníka HTTP 500.

SELECT
    SUM(CASE WHEN radek           IS NULL THEN 1 ELSE 0 END) AS radek_null,
    SUM(CASE WHEN id              IS NULL THEN 1 ELSE 0 END) AS id_null,
    SUM(CASE WHEN schvaleno       IS NULL THEN 1 ELSE 0 END) AS schvaleno_null,
    SUM(CASE WHEN priznak_zamceni IS NULL THEN 1 ELSE 0 END) AS priznak_zamceni_null,
    COUNT(*) AS total_rows
FROM dbo.HOT_ZAZNAMY;

-- =====================================================================
-- 3) Vyber existující ticket pro test — TOP 5 nejnovějších s plnou hlavičkou
-- =====================================================================
-- Vezmi některé číslo `id` ze sloupce a dej do /SDConnector/Inspect.
-- Pokud ticket existuje a Inspect spadne s 500 → potvrzená hypotéza.

SELECT TOP 5
    id, datum, stav, typ_zaznamu, schvaleno, priznak_zamceni, radek
FROM   dbo.HOT_ZAZNAMY
WHERE  datum IS NOT NULL
ORDER  BY datum DESC;

-- =====================================================================
-- 4) (volitelné) Bonus check — sloupce co aplikace používá pro
--                              chat modal (HOT_VYJADRENI)
-- =====================================================================
-- HotVyjadreniEntity má pouze Id (long) jako non-nullable.
-- Tady je riziko menší, ale rozumné ověřit.

SELECT
    SUM(CASE WHEN id IS NULL THEN 1 ELSE 0 END) AS vyj_id_null,
    COUNT(*) AS vyj_total_rows
FROM dbo.HOT_VYJADRENI;
