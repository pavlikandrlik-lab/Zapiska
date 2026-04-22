-- =============================================================================
-- db_upgrade_1_3_3_fakturace_cleanup.sql
-- Odstraňuje krok harmonogramu #11 „fakturace" z existujících instalací.
-- Spec: docs/superpowers/specs/2026-04-21-servicedesk-vytezovani-vyjadreni-design.md §11
-- Destruktivní skript — spouštět s vědomím, že se smažou data.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

-- 1. Zjistit všechny Id řádků označených jako fakturace
DECLARE @FakturaceTypy TABLE (Id INT NOT NULL PRIMARY KEY);

INSERT @FakturaceTypy (Id)
SELECT Id
FROM dbo.ciselnik_harmonogram_typu
WHERE kod LIKE N'HS11[_]%'
   OR nazev LIKE N'%fakturace%';

-- 2. Report před smazáním
DECLARE @TypyCount INT = (SELECT COUNT(*) FROM @FakturaceTypy);
DECLARE @HodnotyCount INT = (
    SELECT COUNT(*)
    FROM dbo.zaznam_harmonogram_hodnoty
    WHERE typ_id IN (SELECT Id FROM @FakturaceTypy)
);

PRINT N'Fakturace typy ke smazání: ' + CAST(@TypyCount AS nvarchar(10));
PRINT N'Fakturace hodnoty ke smazání: ' + CAST(@HodnotyCount AS nvarchar(10));

-- 3. Smazat hodnoty (skutečnost + plán) pro fakturační typy
DELETE FROM dbo.zaznam_harmonogram_hodnoty
WHERE typ_id IN (SELECT Id FROM @FakturaceTypy);

-- 4. Smazat pending návrhy s referencí na HS11 (edge case)
--    payload_json je nvarchar(max), obsahuje TypId nebo kód kroku
DECLARE @NavrhyCount INT;
SELECT @NavrhyCount = COUNT(*)
FROM dbo.zaznam_navrhy
WHERE stav = N'PENDING'
  AND (payload_json LIKE N'%HS11[_]%' OR payload_json LIKE N'%fakturace%');

IF @NavrhyCount > 0
BEGIN
    PRINT N'Pending návrhy s referencí na fakturaci: ' + CAST(@NavrhyCount AS nvarchar(10));
    DELETE FROM dbo.zaznam_navrhy
    WHERE stav = N'PENDING'
      AND (payload_json LIKE N'%HS11[_]%' OR payload_json LIKE N'%fakturace%');
END;

-- 5. Smazat řádky ze číselníku
DELETE FROM dbo.ciselnik_harmonogram_typu
WHERE Id IN (SELECT Id FROM @FakturaceTypy);

-- 6. Verifikace
DECLARE @ZbyvajiTypy INT = (
    SELECT COUNT(*)
    FROM dbo.ciselnik_harmonogram_typu
    WHERE kod LIKE N'HS11[_]%' OR nazev LIKE N'%fakturace%'
);
DECLARE @ZbyvajiHodnoty INT = (
    SELECT COUNT(*)
    FROM dbo.zaznam_harmonogram_hodnoty zhh
    INNER JOIN dbo.ciselnik_harmonogram_typu cht ON cht.Id = zhh.typ_id
    WHERE cht.kod LIKE N'HS11[_]%'
);

IF @ZbyvajiTypy > 0 OR @ZbyvajiHodnoty > 0
BEGIN
    RAISERROR(N'Fakturace cleanup selhal: typy=%d, hodnoty=%d', 16, 1, @ZbyvajiTypy, @ZbyvajiHodnoty);
    ROLLBACK TRANSACTION;
    RETURN;
END;

PRINT N'Fakturace cleanup OK. Typy smazáno: ' + CAST(@TypyCount AS nvarchar(10))
    + N', hodnoty smazáno: ' + CAST(@HodnotyCount AS nvarchar(10))
    + N', návrhy smazáno: ' + CAST(@NavrhyCount AS nvarchar(10));

COMMIT TRANSACTION;
GO
