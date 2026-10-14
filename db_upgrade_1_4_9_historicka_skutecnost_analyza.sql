-- =============================================================================
-- db_upgrade_1_4_9_historicka_skutecnost_analyza.sql
--
-- POUZE ČTE — nic nemění. Ukáže, co udělá db_upgrade_1_4_9_historicka_skutecnost.sql:
-- pro každý krok harmonogramu s historickou skutečností (skutecnost_zdroj = 3) úkol, krok,
-- datum a akci. Spusť ho PŘED výkonným skriptem a výsledek zkontroluj. Pracovní tabulka
-- #historicke je dočasná (tempdb) a zanikne se spojením.
--
-- Pravidla (uživatel 2026-10-14: „buď to dělám ručně, nebo to dělá automat, nic mezi“):
--   * Záznam v RUČNÍM režimu (některý z kroků 1/3/4/6/7/10 má skutecnost_rezim = 1):
--       historické datum se převede na ruční (✍️), krok přejde do ručního režimu.
--   * Záznam v AUTOMATICKÉM režimu:
--       kroky 1/3/4/6/7/10 — datum se SMAŽE; aplikace ho po startu doplní automatem
--         z navázaného vyjádření, pokud nějaké existuje;
--       kroky 2/5/8/9 — převedou se na ruční (automat je nikdy nevyplňuje).
--   * Historický zdroj bez data — jen se opraví zdroj na „bez hodnoty“.
--
-- Výstup:
--   1) detail po krocích (projekt, úkol, režim, krok, historické datum, akce)
--   2) souhrn podle akce
--
-- Sloupec vyjadreni_orientacne ukazuje datum navázaného vyjádření, které by automat
-- pravděpodobně použil (krok 1 nejdřívější, ostatní nejpozdější). Je orientační: automat
-- navíc ověřuje typ tiketu v ServiceDesku a respektuje ručně zvoleného kandidáta.
-- =============================================================================
SET NOCOUNT ON;

SELECT k.id,
       k.zaznam_id,
       k.poradi,
       k.skutecnost_datum,
       CASE WHEN EXISTS (SELECT 1 FROM dbo.zaznam_harmonogram_krok m
                         WHERE m.zaznam_id = k.zaznam_id
                           AND m.poradi IN (1, 3, 4, 6, 7, 10)
                           AND m.skutecnost_rezim = 1)
            THEN 1 ELSE 0 END AS zaznam_rucne
INTO #historicke
FROM dbo.zaznam_harmonogram_krok k
WHERE k.skutecnost_zdroj = 3;

-- 1) Detail
SELECT p.zkratka AS projekt,
       z.id AS zaznam_id,
       z.cislo_viditelne AS cislo_zaznamu,
       z.nazev AS nazev_zaznamu,
       CASE h.zaznam_rucne WHEN 1 THEN N'ruční' ELSE N'automatický' END AS rezim_zaznamu,
       h.poradi AS krok,
       CASE h.poradi
            WHEN 1 THEN N'1. příprava zadání dodavateli'
            WHEN 2 THEN N'2. konzultace termínů s dodavatelem'
            WHEN 3 THEN N'3. odeslání zadání dodavateli'
            WHEN 4 THEN N'4. dodání návrhu řešení'
            WHEN 5 THEN N'5. vypořádání připomínek'
            WHEN 6 THEN N'6. odeslání požadavku na výrobu'
            WHEN 7 THEN N'7. dodání funkcionality dodavatelem'
            WHEN 8 THEN N'8. připomínkování'
            WHEN 9 THEN N'9. testování'
            WHEN 10 THEN N'10. nasazení do provozu'
       END AS nazev_kroku,
       CONVERT(varchar(10), h.skutecnost_datum, 104) AS historicke_datum,
       a.akce_kod,
       CASE a.akce_kod
            WHEN 'RUCNI' THEN N'převede na ruční (datum zůstane)'
            WHEN 'ZDROJ' THEN N'jen opraví zdroj (datum chybí)'
            WHEN 'SMAZAT' THEN CASE WHEN v.datum IS NULL
                                    THEN N'SMAŽE — vyjádření zatím není, krok zůstane prázdný'
                                    ELSE N'SMAŽE — automat doplní z vyjádření' END
       END AS akce,
       CONVERT(varchar(10), v.datum, 104) AS vyjadreni_orientacne
FROM #historicke h
CROSS APPLY (SELECT CASE WHEN h.skutecnost_datum IS NULL THEN 'ZDROJ'
                         WHEN h.zaznam_rucne = 0 AND h.poradi IN (1, 3, 4, 6, 7, 10) THEN 'SMAZAT'
                         ELSE 'RUCNI' END AS akce_kod) a
JOIN dbo.projektove_zaznamy z ON z.id = h.zaznam_id
JOIN dbo.projekty p ON p.id = z.projekt_id
OUTER APPLY (SELECT CASE WHEN h.poradi = 1 THEN MIN(vz.datum_vyjadreni) ELSE MAX(vz.datum_vyjadreni) END AS datum
             FROM dbo.zaznam_harmonogram_vyjadreni_vazba vz
             WHERE vz.zaznam_id = h.zaznam_id AND vz.poradi = h.poradi AND vz.stav = 1) v
ORDER BY p.zkratka, z.id, h.poradi;

-- 2) Souhrn
SELECT CASE WHEN h.skutecnost_datum IS NULL THEN N'jen opraví zdroj'
            WHEN h.zaznam_rucne = 0 AND h.poradi IN (1, 3, 4, 6, 7, 10) THEN N'SMAŽE (automatický režim, kroky 1/3/4/6/7/10)'
            ELSE N'převede na ruční' END AS akce,
       COUNT(*) AS kroku,
       COUNT(DISTINCT h.zaznam_id) AS ukolu
FROM #historicke h
GROUP BY CASE WHEN h.skutecnost_datum IS NULL THEN N'jen opraví zdroj'
              WHEN h.zaznam_rucne = 0 AND h.poradi IN (1, 3, 4, 6, 7, 10) THEN N'SMAŽE (automatický režim, kroky 1/3/4/6/7/10)'
              ELSE N'převede na ruční' END
ORDER BY akce;
