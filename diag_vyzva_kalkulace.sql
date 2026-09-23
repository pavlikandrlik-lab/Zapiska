-- =====================================================================
-- Diagnostika: proč tisk výzvy nezobrazí kalkulaci u některých PNF
-- Replikuje přesně výběr, který dělá aplikace
-- (VyzvaExportBuilder -> SqlTicketingQueryService.GetAkceptovaneKalkulaceAsync)
-- =====================================================================

-- ČÁST 1 — spustit v DB PM_Tracker: která čísla PNF výzva obsahuje
SELECT v.kod, eo.cislo, eo.zaznam_id, eo.predpokladana_cena
FROM dbo.zaznam_externi_odkazy eo
JOIN dbo.vyzvy v ON v.id = eo.vyzva_id
WHERE v.kod = N'<KOD_VYZVY>'          -- doplnit kód výzvy
ORDER BY eo.cislo;


-- ČÁST 2 — spustit v DB ServiceDesku (intranetNEW)
DECLARE @cisla TABLE (cislo INT PRIMARY KEY);
INSERT INTO @cisla (cislo) VALUES (347306), (352735);   -- sem čísla z části 1

;WITH z AS (
    SELECT c.cislo,
           COUNT(hz.radek) OVER (PARTITION BY c.cislo) AS pocet_radku_hot_zaznamy,
           hz.typ_zaznamu, hz.pid
    FROM @cisla c
    LEFT JOIN dbo.HOT_ZAZNAMY hz ON hz.id = c.cislo
),
k AS (
    SELECT z.cislo, z.pocet_radku_hot_zaznamy, z.typ_zaznamu,
           N'[' + z.pid + N']'  AS zaznam_pid,  DATALENGTH(z.pid)  AS zaznam_pid_bytes,
           hk.id                AS kalk_id,
           N'[' + hk.pid + N']' AS kalk_pid,    DATALENGTH(hk.pid) AS kalk_pid_bytes,
           N'[' + hk.akceptace + N']' AS akceptace,
           hk.cena_a, hk.cena_p, hk.cena_t, hk.cena_i, hk.cena_l, hk.cena,
           -- aplikace páruje v C# ordinálně: záleží na velikosti písmen i koncových mezerách
           CASE WHEN (hk.pid + N'|') COLLATE Latin1_General_BIN2
                   = (z.pid  + N'|') COLLATE Latin1_General_BIN2 THEN 1 ELSE 0 END AS pid_presna_shoda,
           -- zákazový seznam jako v aplikaci; prázdná akceptace = neakceptováno
           CASE WHEN NULLIF(LTRIM(RTRIM(hk.akceptace)), N'') IS NULL THEN 0
                WHEN LOWER(REPLACE(REPLACE(REPLACE(hk.akceptace, N' ', N''), NCHAR(9), N''), NCHAR(160), N''))
                     IN (N'návrh', N'neakceptováno', N'akceptovat?') THEN 0
                ELSE 1 END AS akceptace_ok
    FROM z
    LEFT JOIN dbo.HOT_KALKULACE hk ON hk.pid = z.pid   -- SQL párování: case-insensitive, ignoruje koncové mezery
)
SELECT *,
       CASE WHEN pid_presna_shoda = 1 AND akceptace_ok = 1 THEN 1 ELSE 0 END AS aplikace_ji_muze_vzit,
       ROW_NUMBER() OVER (PARTITION BY cislo
                          ORDER BY CASE WHEN pid_presna_shoda = 1 AND akceptace_ok = 1 THEN 0 ELSE 1 END,
                                   kalk_id DESC) AS poradi_vyberu   -- 1 + aplikace_ji_muze_vzit=1 => tuhle vytiskne
FROM k
ORDER BY cislo, poradi_vyberu;


-- ČÁST 3 — když část 2 u PNF kalkulaci nenajde, ale v SD ji vidíš:
-- zjisti, ke kterému tiketu (pid) kalkulace ve skutečnosti patří
SELECT hk.id, N'[' + hk.pid + N']' AS pid, hk.akceptace, hk.cena,
       hz.id AS tiket, hz.typ_zaznamu
FROM dbo.HOT_KALKULACE hk
LEFT JOIN dbo.HOT_ZAZNAMY hz ON hz.pid = hk.pid
WHERE hk.id = <ID_KALKULACE_KTEROU_JSI_VIDEL>;
