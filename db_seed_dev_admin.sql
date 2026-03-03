-- PM Tracker local/dev seed
-- Purpose: add demo/business dictionaries, local admin and sample data on top of production baseline.
-- Idempotent.

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

BEGIN TRY
    BEGIN TRAN;

    -- Organization + unit
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_organizace WHERE kod = N'MO')
        INSERT INTO dbo.ciselnik_organizace(kod, nazev, is_locked) VALUES (N'MO', N'Ministerstvo obrany', 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_organizace WHERE kod = N'DOD')
        INSERT INTO dbo.ciselnik_organizace(kod, nazev, is_locked) VALUES (N'DOD', N'Dodavatel', 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_organizacni_celky WHERE kod = N'OI')
        INSERT INTO dbo.ciselnik_organizacni_celky(kod, nazev, is_locked) VALUES (N'OI', N'Odbor informatiky', 1);

    DECLARE @organizaceId int = (SELECT TOP (1) id FROM dbo.ciselnik_organizace WHERE kod = N'MO' ORDER BY id);
    DECLARE @orgCelekId int = (SELECT TOP (1) id FROM dbo.ciselnik_organizacni_celky WHERE kod = N'OI' ORDER BY id);

    -- Admin user for development (without AD)
    IF NOT EXISTS (SELECT 1 FROM dbo.osoby WHERE jmeno = N'Pavel' AND prijmeni = N'Admin')
    BEGIN
        INSERT INTO dbo.osoby(jmeno, prijmeni, titul, email, organizacni_celek_id, organizace_id, Guid_AD)
        VALUES (N'Pavel', N'Admin', NULL, N'pavel.admin@pmtracker.local', @orgCelekId, @organizaceId, NULL);
    END

    DECLARE @osobaId int = (SELECT TOP (1) id FROM dbo.osoby WHERE jmeno = N'Pavel' AND prijmeni = N'Admin' ORDER BY id);
    UPDATE dbo.osoby
    SET email = COALESCE(NULLIF(email, N''), N'pavel.admin@pmtracker.local')
    WHERE id = @osobaId;

    -- Project roles
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_roli_projektu WHERE kod = N'ANALYTIK')
        INSERT INTO dbo.ciselnik_roli_projektu(kod, nazev, is_locked) VALUES (N'ANALYTIK', N'Analytik', 0);
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_roli_projektu WHERE kod = N'DEV')
        INSERT INTO dbo.ciselnik_roli_projektu(kod, nazev, is_locked) VALUES (N'DEV', N'Developer', 0);

    -- Task dictionaries
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_kategorii_zaznamu WHERE kod = N'UKOL')
        INSERT INTO dbo.ciselnik_kategorii_zaznamu(kod, nazev, is_locked) VALUES (N'UKOL', N'Úkol', 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_kategorii_zaznamu WHERE kod = N'INFO')
        INSERT INTO dbo.ciselnik_kategorii_zaznamu(kod, nazev, is_locked) VALUES (N'INFO', N'Informace', 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_kategorii_zaznamu WHERE kod = N'ROZHODNUTI')
        INSERT INTO dbo.ciselnik_kategorii_zaznamu(kod, nazev, is_locked) VALUES (N'ROZHODNUTI', N'Rozhodnutí', 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_typu_ukolu WHERE kod = N'RU')
        INSERT INTO dbo.ciselnik_typu_ukolu(kod, nazev, is_locked) VALUES (N'RU', N'Hlavní úkol rozvoje', 0);
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_typu_ukolu WHERE kod = N'A')
        INSERT INTO dbo.ciselnik_typu_ukolu(kod, nazev, is_locked) VALUES (N'A', N'Akce', 0);
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_typu_ukolu WHERE kod = N'mP')
        INSERT INTO dbo.ciselnik_typu_ukolu(kod, nazev, is_locked) VALUES (N'mP', N'Miniprojekt', 0);

    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_stavu_ukolu WHERE kod = N'OPEN')
        INSERT INTO dbo.ciselnik_stavu_ukolu(kod, nazev, is_final, is_locked) VALUES (N'OPEN', N'Rozpracováno', 0, 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_stavu_ukolu WHERE kod = N'WAIT')
        INSERT INTO dbo.ciselnik_stavu_ukolu(kod, nazev, is_final, is_locked) VALUES (N'WAIT', N'Čeká se', 0, 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_stavu_ukolu WHERE kod = N'DONE')
        INSERT INTO dbo.ciselnik_stavu_ukolu(kod, nazev, is_final, is_locked) VALUES (N'DONE', N'Hotovo', 1, 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_stavu_ukolu WHERE kod = N'CANCEL')
        INSERT INTO dbo.ciselnik_stavu_ukolu(kod, nazev, is_final, is_locked) VALUES (N'CANCEL', N'Zrušeno', 1, 1);

    -- External links + vyzvy
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_typu_externich_odkazu WHERE kod = N'PMP')
        INSERT INTO dbo.ciselnik_typu_externich_odkazu(kod, nazev, is_locked) VALUES (N'PMP', N'Požadavek metodické podpory', 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_typu_externich_odkazu WHERE kod = N'PNF')
        INSERT INTO dbo.ciselnik_typu_externich_odkazu(kod, nazev, is_locked) VALUES (N'PNF', N'Požadavek nové funkcionality', 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_typu_externich_odkazu WHERE kod = N'NES')
        INSERT INTO dbo.ciselnik_typu_externich_odkazu(kod, nazev, is_locked) VALUES (N'NES', N'Nesrovnalost', 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_vyzvy WHERE kod = N'DV05')
        INSERT INTO dbo.ciselnik_vyzvy(kod, nazev, rok, is_locked) VALUES (N'DV05', N'Dílčí výzva 05', '2025-01-01', 0);
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_vyzvy WHERE kod = N'DV06')
        INSERT INTO dbo.ciselnik_vyzvy(kod, nazev, rok, is_locked) VALUES (N'DV06', N'Dílčí výzva 06', '2026-01-01', 0);
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_vyzvy WHERE kod = N'DV07')
        INSERT INTO dbo.ciselnik_vyzvy(kod, nazev, rok, is_locked) VALUES (N'DV07', N'Dílčí výzva 07', '2027-01-01', 0);

    -- Meetings dictionaries
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_stavu_jednani WHERE kod = N'DRAFT')
        INSERT INTO dbo.ciselnik_stavu_jednani(kod, nazev, is_locked) VALUES (N'DRAFT', N'Příprava', 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_stavu_jednani WHERE kod = N'OPEN')
        INSERT INTO dbo.ciselnik_stavu_jednani(kod, nazev, is_locked) VALUES (N'OPEN', N'Otevřeno pro zápis', 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_stavu_jednani WHERE kod = N'CLOSED')
        INSERT INTO dbo.ciselnik_stavu_jednani(kod, nazev, is_locked) VALUES (N'CLOSED', N'Uzavřeno', 1);

    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_stavu_ucasti WHERE kod = N'PRESENT')
        INSERT INTO dbo.ciselnik_stavu_ucasti(kod, nazev, is_locked) VALUES (N'PRESENT', N'Přítomen', 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_stavu_ucasti WHERE kod = N'EXCUSED')
        INSERT INTO dbo.ciselnik_stavu_ucasti(kod, nazev, is_locked) VALUES (N'EXCUSED', N'Omluven', 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_stavu_ucasti WHERE kod = N'ABSENT')
        INSERT INTO dbo.ciselnik_stavu_ucasti(kod, nazev, is_locked) VALUES (N'ABSENT', N'Nepřítomen', 1);

    -- Harmonogram baseline for local/dev/test
    IF NOT EXISTS (SELECT 1 FROM dbo.harmonogram_sablony)
    BEGIN
        INSERT INTO dbo.harmonogram_sablony(delay_barva_hex, is_aktivni, created_by)
        VALUES (N'#DC2626', 1, NULL);
    END

    DECLARE @sablonaVerze int = (
        SELECT TOP (1) verze
        FROM dbo.harmonogram_sablony
        WHERE is_aktivni = 1
        ORDER BY verze DESC
    );

    IF @sablonaVerze IS NULL
    BEGIN
        SELECT TOP (1) @sablonaVerze = verze
        FROM dbo.harmonogram_sablony
        ORDER BY verze DESC;

        UPDATE dbo.harmonogram_sablony
        SET is_aktivni = CASE WHEN verze = @sablonaVerze THEN 1 ELSE 0 END;
    END

    ;WITH source_data AS (
        SELECT
            @sablonaVerze AS sablona_verze,
            v.kod,
            v.nazev,
            v.hodnota,
            v.krok_poradi,
            v.je_zpozdeni,
            v.barva_hex,
            v.krok_key
        FROM (VALUES
            (N'HS01_DURATION', N'1. příprava zadání dodavateli', 1, 1, 0, N'#EF4444', CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8301')),
            (N'HS02_DURATION', N'2. konzultace termínů s dodavatelem před vytvořením zadání', 2, 2, 0, N'#F97316', CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8302')),
            (N'HS03_DURATION', N'3. odeslání zadání dodavateli', 3, 3, 0, N'#F59E0B', CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8303')),
            (N'HS04_DURATION', N'4. dodání návrhu řešení', 4, 4, 0, N'#84CC16', CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8304')),
            (N'HS05_DURATION', N'5. vypořádání připomínek', 5, 5, 0, N'#22C55E', CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8305')),
            (N'HS06_DURATION', N'6. odeslání požadavku na výrobu', 6, 6, 0, N'#14B8A6', CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8306')),
            (N'HS07_DURATION', N'7. dodání funkcionality dodavatelem', 7, 7, 0, N'#06B6D4', CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8307')),
            (N'HS08_DURATION', N'8. připomínkování', 8, 8, 0, N'#3B82F6', CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8308')),
            (N'HS09_DURATION', N'9. testování', 9, 9, 0, N'#6366F1', CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8309')),
            (N'HS10_DURATION', N'10. nasazení do provozu', 10, 10, 0, N'#8B5CF6', CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8310')),
            (N'HS11_DURATION', N'11. fakturace', 11, 11, 0, N'#D946EF', CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8311')),
            (N'HS01_DELAY', N'1. příprava zadání dodavateli - zpoždění', 101, 1, 1, NULL, CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8301')),
            (N'HS02_DELAY', N'2. konzultace termínů s dodavatelem před vytvořením zadání - zpoždění', 102, 2, 1, NULL, CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8302')),
            (N'HS03_DELAY', N'3. odeslání zadání dodavateli - zpoždění', 103, 3, 1, NULL, CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8303')),
            (N'HS04_DELAY', N'4. dodání návrhu řešení - zpoždění', 104, 4, 1, NULL, CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8304')),
            (N'HS05_DELAY', N'5. vypořádání připomínek - zpoždění', 105, 5, 1, NULL, CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8305')),
            (N'HS06_DELAY', N'6. odeslání požadavku na výrobu - zpoždění', 106, 6, 1, NULL, CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8306')),
            (N'HS07_DELAY', N'7. dodání funkcionality dodavatelem - zpoždění', 107, 7, 1, NULL, CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8307')),
            (N'HS08_DELAY', N'8. připomínkování - zpoždění', 108, 8, 1, NULL, CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8308')),
            (N'HS09_DELAY', N'9. testování - zpoždění', 109, 9, 1, NULL, CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8309')),
            (N'HS10_DELAY', N'10. nasazení do provozu - zpoždění', 110, 10, 1, NULL, CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8310')),
            (N'HS11_DELAY', N'11. fakturace - zpoždění', 111, 11, 1, NULL, CONVERT(uniqueidentifier, '9A114F1B-8AA3-44C8-8F8E-E3A9ED4F8311'))
        ) v(kod, nazev, hodnota, krok_poradi, je_zpozdeni, barva_hex, krok_key)
    )
    MERGE dbo.ciselnik_harmonogram_typu AS target
    USING source_data AS source
    ON target.sablona_verze = source.sablona_verze AND target.kod = source.kod
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (kod, nazev, hodnota, is_locked, sablona_verze, krok_key, krok_poradi, je_zpozdeni, barva_hex)
        VALUES (source.kod, source.nazev, source.hodnota, 1, source.sablona_verze, source.krok_key, source.krok_poradi, source.je_zpozdeni, source.barva_hex)
    WHEN MATCHED THEN
        UPDATE SET
            target.nazev = source.nazev,
            target.hodnota = source.hodnota,
            target.is_locked = 1,
            target.krok_key = source.krok_key,
            target.krok_poradi = source.krok_poradi,
            target.je_zpozdeni = source.je_zpozdeni,
            target.barva_hex = source.barva_hex;

    -- Subsystems
    IF NOT EXISTS (SELECT 1 FROM dbo.subsystemy WHERE [kód] = N'INTEGRACE')
        INSERT INTO dbo.subsystemy([kód], nazev) VALUES (N'INTEGRACE', N'Integrace');
    IF NOT EXISTS (SELECT 1 FROM dbo.subsystemy WHERE [kód] = N'JADRO')
        INSERT INTO dbo.subsystemy([kód], nazev) VALUES (N'JADRO', N'Jádro');

    -- Demo project
    DECLARE @stavRunId int = (SELECT TOP (1) id FROM dbo.ciselnik_stavu_projektu WHERE kod = N'RUN' ORDER BY id);
    IF NOT EXISTS (SELECT 1 FROM dbo.projekty WHERE zkratka = N'PMT')
        INSERT INTO dbo.projekty(zkratka, cely_nazev, stav_id) VALUES (N'PMT', N'PM Tracker', @stavRunId);

    DECLARE @projektId int = (SELECT TOP (1) id FROM dbo.projekty WHERE zkratka = N'PMT' ORDER BY id);

    -- Authz: superadmin + APP_ADMIN role mapping
    IF NOT EXISTS (SELECT 1 FROM authz.superadmins WHERE osoba_id = @osobaId)
        INSERT INTO authz.superadmins(osoba_id, poznamka, created_by) VALUES (@osobaId, N'Lokální seed admin bez AD', @osobaId);

    DECLARE @appAdminRoleId int = (SELECT TOP (1) id FROM authz.roles WHERE kod = N'APP_ADMIN');
    IF @appAdminRoleId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM authz.user_roles WHERE osoba_id = @osobaId AND role_id = @appAdminRoleId)
            INSERT INTO authz.user_roles(osoba_id, role_id, is_active) VALUES (@osobaId, @appAdminRoleId, 1);
    END

    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK;
    THROW;
END CATCH;
