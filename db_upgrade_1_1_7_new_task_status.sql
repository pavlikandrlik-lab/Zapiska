/* ==========================================================
   Upgrade 1.1.7 — přidat stav úkolu "Nezahájeno" (kod NEW)
   ==========================================================

   Doplňuje třetí baseline stav pro životní cyklus úkolu:
     NEW     → Nezahájeno   (is_final = 0, výchozí pro nové úkoly)
     OPEN    → Rozpracováno (existující baseline)
     DONE    → Hotovo       (existující baseline, is_final = 1)

   Idempotentní přes MERGE — bezpečné spouštět opakovaně.
   Neovlivní existující záznamy; nový stav je k dispozici
   v číselníku pro výběr při vytváření / editaci záznamu.
*/

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

MERGE dbo.ciselnik_stavu_ukolu AS target
USING (VALUES
    (N'NEW', N'Nezahájeno', 0, 1)
) AS source(kod, nazev, is_final, is_locked)
ON target.kod = source.kod
WHEN NOT MATCHED BY TARGET THEN
    INSERT (kod, nazev, is_final, is_locked)
    VALUES (source.kod, source.nazev, source.is_final, source.is_locked)
WHEN MATCHED THEN
    UPDATE SET
        target.nazev = source.nazev,
        target.is_final = source.is_final,
        target.is_locked = source.is_locked;
GO
