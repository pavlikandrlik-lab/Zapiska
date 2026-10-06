-- =============================================================================
-- db_upgrade_1_4_6_richtext_unicode.sql
--
-- Rich text (popis záznamu, vyjádření, požadavek externí vazby) v Unicode
-- a bez entit pro písmena.
--
-- KONTEXT (2026-10-06):
--   Aplikace do verze 1.4.5 ukládala rich text z editoru Quill přes
--   HtmlEncoder.Default. Ten každý znak mimo ASCII převedl na entitu (ř → &#x159;),
--   takže vyhledávání (LIKE) slova s diakritikou v popisu a ve vyjádřeních nenašlo.
--   Od verze 1.4.6 aplikace kóduje jen znaky nebezpečné v HTML (& < > ").
--
--   Sloupce popis, text_vyjadreni a duvod jsou ale zastaralý ne-Unicode typ text
--   v kódové stránce collation (v produkci CP1250). Znaky mimo ni (emoji, azbuka,
--   → ≥ ✓ …) by se bez entit při uložení změnily na „?“. Skript je proto převádí
--   na NVARCHAR(MAX).
--
-- ROZSAH MIGRACE:
--   1. Typ text → NVARCHAR(MAX), se zachováním NULL/NOT NULL a collation sloupce:
--        dbo.vyjadreni.text_vyjadreni
--        dbo.projektove_zaznamy.popis
--        dbo.zaznam_historie_terminu.duvod  (prostý text, převádí se jen kvůli Unicode)
--   2. Číselné entity &#x…; zpět na znaky v text_vyjadreni, popis
--      a zaznam_externi_odkazy.pozadavek (ten je NVARCHAR(MAX) už od 1_4_2).
--      Entity znaků & < > " se převedou na pojmenované (&amp; &lt; &gt; &quot;),
--      aby z textu nevznikly HTML značky. Neplatné sekvence zůstanou beze změny.
--   * Mění se jen tyto sloupce: nic se nemaže a data změn záznamů zůstávají.
--   * authz.audit_log se nemění: jsou to JSON snímky, entity v nich nejsou
--     ve tvaru &#x a zobrazení je nepoužívá.
--
-- PŘEDPOKLADY: db_upgrade_1_4_2 (sloupec pozadavek). Spouštějte při zastavené
--   aplikaci (app pool), aby během převodu nikdo neukládal.
--
-- SPUŠTĚNÍ: pod účtem s db_owner na cílové databázi.
--   sqlcmd -S <SQL_HOST>\<INSTANCE> -E -d PM_Tracker -b -i db_upgrade_1_4_6_richtext_unicode.sql
--
-- Idempotence: skript lze spustit opakovaně. Typ se mění jen u sloupců, které jsou
--   ještě typu text, a převádějí se jen řádky, které ještě obsahují entity.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

PRINT N'[1.4.6] Rich text v Unicode — start';

IF COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'pozadavek') IS NULL
BEGIN
    ;THROW 50000, N'[1.4.6] Chybí sloupec dbo.zaznam_externi_odkazy.pozadavek — spusťte nejdřív db_upgrade_1_4_2_externi_odkaz_pozadavek.sql.', 1;
END

BEGIN TRANSACTION;

-- -----------------------------------------------------------------------------
-- 1. Typ text → NVARCHAR(MAX)
-- -----------------------------------------------------------------------------
DECLARE @sloupce TABLE (tabulka SYSNAME NOT NULL, sloupec SYSNAME NOT NULL);
INSERT @sloupce VALUES
    (N'vyjadreni', N'text_vyjadreni'),
    (N'projektove_zaznamy', N'popis'),
    (N'zaznam_historie_terminu', N'duvod');

DECLARE @tabulka SYSNAME, @sloupec SYSNAME, @alter NVARCHAR(1000);

DECLARE sloupce CURSOR LOCAL FAST_FORWARD FOR SELECT tabulka, sloupec FROM @sloupce;
OPEN sloupce;
FETCH NEXT FROM sloupce INTO @tabulka, @sloupec;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @alter = NULL;

    SELECT @alter = N'ALTER TABLE dbo.' + QUOTENAME(@tabulka)
                  + N' ALTER COLUMN ' + QUOTENAME(@sloupec)
                  + N' NVARCHAR(MAX) COLLATE ' + c.collation_name
                  + CASE WHEN c.is_nullable = 1 THEN N' NULL' ELSE N' NOT NULL' END
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(N'dbo.' + QUOTENAME(@tabulka))
      AND c.name = @sloupec
      AND TYPE_NAME(c.user_type_id) = N'text';

    IF @alter IS NULL
        PRINT N'[1.4.6] dbo.' + @tabulka + N'.' + @sloupec + N' není typu text — přeskakuji';
    ELSE
    BEGIN
        EXEC sys.sp_executesql @alter;
        PRINT N'[1.4.6] dbo.' + @tabulka + N'.' + @sloupec + N' převeden na NVARCHAR(MAX)';
    END

    FETCH NEXT FROM sloupce INTO @tabulka, @sloupec;
END
CLOSE sloupce;
DEALLOCATE sloupce;

-- -----------------------------------------------------------------------------
-- 2. Číselné entity &#x…; → znaky
-- -----------------------------------------------------------------------------
DECLARE @prace TABLE (druh TINYINT NOT NULL, id INT NOT NULL, hodnota NVARCHAR(MAX) NOT NULL);
INSERT @prace SELECT 1, id, text_vyjadreni FROM dbo.vyjadreni WHERE text_vyjadreni LIKE N'%&#x%';
INSERT @prace SELECT 2, id, popis FROM dbo.projektove_zaznamy WHERE popis LIKE N'%&#x%';
INSERT @prace SELECT 3, id, pozadavek FROM dbo.zaznam_externi_odkazy WHERE pozadavek LIKE N'%&#x%';

DECLARE @druh TINYINT, @id INT, @vstup NVARCHAR(MAX), @vystup NVARCHAR(MAX),
        @pos BIGINT, @delka BIGINT, @start BIGINT, @konec BIGINT,
        @hex NVARCHAR(6), @kod INT, @znak NVARCHAR(8),
        @prevedeno INT = 0;

DECLARE prace CURSOR LOCAL FAST_FORWARD FOR SELECT druh, id, hodnota FROM @prace;
OPEN prace;
FETCH NEXT FROM prace INTO @druh, @id, @vstup;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @vystup = N'';
    SET @pos = 1;
    SET @delka = DATALENGTH(@vstup) / 2;  -- LEN by ignoroval koncové mezery

    WHILE @pos <= @delka
    BEGIN
        SET @start = CHARINDEX(N'&#x', @vstup, @pos);
        IF @start = 0
        BEGIN
            SET @vystup = @vystup + SUBSTRING(@vstup, @pos, @delka - @pos + 1);
            BREAK;
        END

        SET @znak = NULL;
        SET @konec = CHARINDEX(N';', @vstup, @start + 3);

        -- Platná entita má mezi „&#x“ a „;“ 1 až 6 hexadecimálních číslic. Binární
        -- collation, protože pod českou by rozsah [A-F] pustil i Č, Ď, É …
        IF @konec > @start + 3 AND @konec - @start - 3 <= 6
        BEGIN
            SET @hex = SUBSTRING(@vstup, @start + 3, @konec - @start - 3);
            IF @hex COLLATE Latin1_General_BIN NOT LIKE N'%[^0-9A-Fa-f]%'
            BEGIN
                SET @kod = CONVERT(INT, TRY_CONVERT(VARBINARY(4), RIGHT('00000000' + CONVERT(VARCHAR(6), @hex), 8), 2));
                SET @znak = CASE
                    WHEN @kod = 34 THEN N'&quot;'                  -- "
                    WHEN @kod = 38 THEN N'&amp;'                   -- &
                    WHEN @kod = 60 THEN N'&lt;'                    -- <
                    WHEN @kod = 62 THEN N'&gt;'                    -- >
                    WHEN @kod BETWEEN 1 AND 55295                  -- 0x1–0xD7FF
                      OR @kod BETWEEN 57344 AND 65535              -- 0xE000–0xFFFF
                        THEN NCHAR(@kod)
                    WHEN @kod BETWEEN 65536 AND 1114111            -- mimo BMP (emoji): náhradní pár
                        THEN COALESCE(NCHAR(@kod),
                                      NCHAR(55296 + (@kod - 65536) / 1024) + NCHAR(56320 + (@kod - 65536) % 1024))
                END;
            END
        END

        IF @znak IS NULL
        BEGIN
            -- Neplatná entita: opsat „&#x“ beze změny a hledat dál za ní.
            SET @vystup = @vystup + SUBSTRING(@vstup, @pos, @start - @pos + 3);
            SET @pos = @start + 3;
        END
        ELSE
        BEGIN
            SET @vystup = @vystup + SUBSTRING(@vstup, @pos, @start - @pos) + @znak;
            SET @pos = @konec + 1;
        END
    END

    IF @druh = 1
        UPDATE dbo.vyjadreni SET text_vyjadreni = @vystup WHERE id = @id;
    ELSE IF @druh = 2
        UPDATE dbo.projektove_zaznamy SET popis = @vystup WHERE id = @id;
    ELSE
        UPDATE dbo.zaznam_externi_odkazy SET pozadavek = @vystup WHERE id = @id;

    SET @prevedeno += 1;
    FETCH NEXT FROM prace INTO @druh, @id, @vstup;
END
CLOSE prace;
DEALLOCATE prace;

PRINT N'[1.4.6] řádků s převedenými entitami: ' + CAST(@prevedeno AS NVARCHAR(10));

COMMIT TRANSACTION;

-- -----------------------------------------------------------------------------
-- 3. Kontrola výsledku
-- -----------------------------------------------------------------------------
SELECT tabulka = t.name, sloupec = c.name, typ = TYPE_NAME(c.user_type_id), c.max_length
FROM sys.columns c
JOIN sys.tables t ON t.object_id = c.object_id
WHERE (t.name = N'vyjadreni' AND c.name = N'text_vyjadreni')
   OR (t.name = N'projektove_zaznamy' AND c.name = N'popis')
   OR (t.name = N'zaznam_historie_terminu' AND c.name = N'duvod')
   OR (t.name = N'zaznam_externi_odkazy' AND c.name = N'pozadavek');

-- Očekávané: 0. Nenulový počet = neplatné sekvence &#x, které skript nechal beze změny.
SELECT zbyva_entit =
      (SELECT COUNT(*) FROM dbo.vyjadreni WHERE text_vyjadreni LIKE N'%&#x%')
    + (SELECT COUNT(*) FROM dbo.projektove_zaznamy WHERE popis LIKE N'%&#x%')
    + (SELECT COUNT(*) FROM dbo.zaznam_externi_odkazy WHERE pozadavek LIKE N'%&#x%');

PRINT N'[1.4.6] hotovo';
