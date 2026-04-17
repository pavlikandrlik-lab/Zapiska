-- ==============================================================================
-- PM Tracker upgrade 1.1.6 — doplnění rolí PROJ_MAN a GEST
--
-- Důvod: SqlStartupValidatorHostedService vyžaduje role PROJ_MAN (projektový
-- manažer) a GEST (gestor projektu) v číselníku ciselnik_roli_projektu, ale
-- předchozí bootstrap skripty (db_upgrade_0_4_membership_subsystems.sql)
-- tyto kódy neobsahovaly. Aplikace na startu selhávala s:
--   "V DB chybí povinné kódy v ciselnik_roli_projektu: GEST"
--
-- Idempotentní — pouští se opakovaně bez chyby.
-- ==============================================================================

SET NOCOUNT ON;
GO

MERGE dbo.ciselnik_roli_projektu AS target
USING (VALUES
    (N'PROJ_MAN', N'Projektový manažer', 1),
    (N'GEST',     N'Gestor projektu',    1)
) AS source(kod, nazev, is_locked)
ON target.kod = source.kod
WHEN NOT MATCHED BY TARGET THEN
    INSERT (kod, nazev, is_locked) VALUES (source.kod, source.nazev, source.is_locked)
WHEN MATCHED THEN
    UPDATE SET target.nazev = source.nazev, target.is_locked = source.is_locked;
GO
