-- PM Tracker local/dev seed
-- Purpose: make app usable without AD connector (test mode).
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
