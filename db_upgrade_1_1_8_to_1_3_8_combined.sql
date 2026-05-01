-- =============================================================================
-- db_upgrade_1_1_8_to_1_3_8_combined.sql
--
-- Konsolidace 13 postupných upgrade skriptů do jednoho souboru pro DB,
-- která je aktuálně na verzi 1_1_7. Po úspěšném průběhu bude DB na 1_3_8.
--
-- Obsažené migrace (v pořadí spouštění):
--   1) 1_1_8_vyzvy                            — ServiceDesk Výzvy Fáze 1 (schéma)
--   2) 1_2_0_authz_role_scope                 — authz.roles.scope + CHECK
--   3) 1_2_1_lookup_role_authz_fk             — ciselnik_roli_*.authz_role_id + FK
--   4) 1_3_0_cleanup_orphaned_role_permissions — cleanup custom role_permissions
--   5) 1_3_1_history_and_audit_indexes        — FK indexy history + audit log
--   6) 1_3_2_authz_join_indexes               — filtrované indexy lookup→authz
--   7) 1_3_3_fakturace_cleanup                — odstranění kroku harmonogramu HS11
--   8) 1_3_4_ad_sync_settings                 — singleton tabulka ad_sync_settings
--   9) 1_3_5_external_link_harvested_at       — zaznam_externi_odkazy.last_harvested_at
--  10) 1_3_6_vyjadreni_vazba                  — zaznam_harmonogram_vyjadreni_vazba
--  11) 1_3_7_sd_sync_settings_and_fingerprint — sd_active/archive_sync_settings + fingerprint
--  12) 1_3_8_authz_per_action_redesign        — smazání 8 deprecated authz klíčů
--
-- Každá sekce si drží vlastní transakci (BEGIN/COMMIT) a vlastní idempotentní
-- guardy (IF NOT EXISTS / COL_LENGTH IS NULL / ...). Sekce jsou oddělené `GO`,
-- takže batch-dependent kód (ALTER + následný INSERT/REFERENCES na právě
-- vzniklý sloupec) funguje stejně jako v původních skriptech.
--
-- Spouštět přes sqlcmd / SSMS / SqlScriptRunner, který umí GO jako batch
-- separator. ADO.NET bez GO-parseru tento soubor spolehlivě nespustí.
-- =============================================================================

-- =============================================================================
-- [1/12] db_upgrade_1_1_8_vyzvy.sql
-- ServiceDesk Výzvy — Fáze 1: schéma
-- Spec: docs/superpowers/specs/2026-04-20-servicedesk-integrace-vyzvy-design.md
-- Plan: docs/superpowers/plans/2026-04-20-servicedesk-vyzvy-faze-1-schema-backend.md
-- =============================================================================

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1. Projekt: nová pole
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.projekty') AND name = 'misto_plneni')
BEGIN
    ALTER TABLE dbo.projekty ADD misto_plneni NVARCHAR(500) NULL;
    ALTER TABLE dbo.projekty ADD cislo_ramcove_smlouvy NVARCHAR(100) NULL;
END;

-- 2. Drop starého ciselnik_vyzvy (ověř prázdnost, safety check)
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ciselnik_vyzvy')
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.ciselnik_vyzvy)
        THROW 50000, 'ciselnik_vyzvy contains data — manual migration required', 1;

    DECLARE @fkName SYSNAME = (
        SELECT name FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID('dbo.zaznam_externi_odkazy')
          AND referenced_object_id = OBJECT_ID('dbo.ciselnik_vyzvy')
    );
    IF @fkName IS NOT NULL
        EXEC('ALTER TABLE dbo.zaznam_externi_odkazy DROP CONSTRAINT ' + @fkName);

    DROP TABLE dbo.ciselnik_vyzvy;
END;

-- 3. Rename zaznam_externi_odkazy.vyzva → vyzva_id + zaradid_do_vyzvy
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.zaznam_externi_odkazy') AND name = 'vyzva')
    EXEC sp_rename 'dbo.zaznam_externi_odkazy.vyzva', 'vyzva_id', 'COLUMN';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.zaznam_externi_odkazy') AND name = 'zaradid_do_vyzvy')
    ALTER TABLE dbo.zaznam_externi_odkazy
        ADD zaradid_do_vyzvy BIT NOT NULL CONSTRAINT df_zeo_zaradid DEFAULT 0;

-- 4. Vyzvy
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'vyzvy')
BEGIN
    CREATE TABLE dbo.vyzvy (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        projekt_id INT NOT NULL,
        kod NVARCHAR(20) NOT NULL,
        poradove_v_roce INT NOT NULL,
        rok INT NOT NULL,
        stav TINYINT NOT NULL,
        datum_zalozeni DATETIME2 NOT NULL,
        zalozil_osoba_id INT NOT NULL,
        datum_odeslani DATETIME2 NULL,
        odeslal_osoba_id INT NULL,
        misto_plneni_snapshot NVARCHAR(500) NOT NULL,
        cislo_ramcove_smlouvy_snapshot NVARCHAR(100) NOT NULL,
        CONSTRAINT fk_vyzvy_projekt FOREIGN KEY (projekt_id) REFERENCES dbo.projekty(id)
    );

    CREATE UNIQUE INDEX ux_vyzvy_smlouva_rok_poradove
        ON dbo.vyzvy (cislo_ramcove_smlouvy_snapshot, rok, poradove_v_roce);

    CREATE INDEX ix_vyzvy_projekt_datum_desc
        ON dbo.vyzvy (projekt_id, datum_zalozeni);
END;

-- 5. FK + filtered unique index
-- Pozn.: referujeme sloupec vyzva_id, který vznikl renamem v kroku 3. V rámci
-- jednoho batche parser sloupec nevidí, proto používáme dynamic SQL.
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'fk_zeo_vyzva')
BEGIN
    EXEC('ALTER TABLE dbo.zaznam_externi_odkazy
        ADD CONSTRAINT fk_zeo_vyzva
        FOREIGN KEY (vyzva_id) REFERENCES dbo.vyzvy(id) ON DELETE SET NULL');
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_zaznam_externi_odkazy_cislo_in_vyzve')
BEGIN
    EXEC('CREATE UNIQUE INDEX ux_zaznam_externi_odkazy_cislo_in_vyzve
        ON dbo.zaznam_externi_odkazy (cislo)
        WHERE vyzva_id IS NOT NULL');
END;

-- 6. Audit
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'vyzva_historie_stavu')
BEGIN
    CREATE TABLE dbo.vyzva_historie_stavu (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        vyzva_id INT NOT NULL,
        puvodni_stav TINYINT NULL,
        novy_stav TINYINT NOT NULL,
        datum_zmeny DATETIME2 NOT NULL,
        zmenil_osoba_id INT NOT NULL,
        CONSTRAINT fk_vhs_vyzva FOREIGN KEY (vyzva_id) REFERENCES dbo.vyzvy(id) ON DELETE CASCADE
    );

    CREATE INDEX ix_vyzva_historie_stavu_vyzva_id ON dbo.vyzva_historie_stavu (vyzva_id);
END;

COMMIT TRANSACTION;
GO

-- =============================================================================
-- [2/12] db_upgrade_1_2_0_authz_role_scope.sql
-- Authorization unification — Fáze A — Task A2
-- Spec: docs/superpowers/specs/2026-04-21-authorization-unification-design.md
-- Plan: docs/superpowers/plans/2026-04-21-authorization-unification.md
-- =============================================================================

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1. Přidat sloupec scope do authz.roles (default 'GLOBAL' pro existing rows).
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('authz.roles') AND name = 'scope'
)
BEGIN
    EXEC('ALTER TABLE authz.roles ADD scope NVARCHAR(16) NOT NULL CONSTRAINT df_authz_roles_scope DEFAULT ''GLOBAL''');
END;

-- 2. CHECK constraint pro povolené hodnoty.
-- EXEC obaluje DDL, aby deferred compilation vyřešila referenci na sloupec 'scope',
-- který v rámci stejného batche nemusí být ještě parser-visible (SqlScriptRunner spouští
-- skript jako jeden batch; sqlcmd/SSMS by využil implicitní GO, ale ADO.NET ne).
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID('authz.roles') AND name = 'ck_authz_roles_scope'
)
BEGIN
    EXEC('ALTER TABLE authz.roles ADD CONSTRAINT ck_authz_roles_scope CHECK (scope IN (''GLOBAL'',''PROJECT'',''SUBSYSTEM''))');
END;

COMMIT TRANSACTION;
GO

-- =============================================================================
-- [3/12] db_upgrade_1_2_1_lookup_role_authz_fk.sql
-- Authorization unification — Fáze A — Task A5
-- Propojení lookup rolí (projektové + subsystémové) s authz.roles přes FK.
-- =============================================================================

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1. ciselnik_roli_projektu.authz_role_id (nullable během migrace)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.ciselnik_roli_projektu') AND name = 'authz_role_id'
)
BEGIN
    ALTER TABLE dbo.ciselnik_roli_projektu
        ADD authz_role_id INT NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID('dbo.ciselnik_roli_projektu')
      AND name = 'fk_ciselnik_roli_projektu_authz_role'
)
BEGIN
    ALTER TABLE dbo.ciselnik_roli_projektu
        ADD CONSTRAINT fk_ciselnik_roli_projektu_authz_role
            FOREIGN KEY (authz_role_id) REFERENCES authz.roles(id);
END;

-- 2. ciselnik_roli_subsystemu.authz_role_id
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.ciselnik_roli_subsystemu') AND name = 'authz_role_id'
)
BEGIN
    ALTER TABLE dbo.ciselnik_roli_subsystemu
        ADD authz_role_id INT NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID('dbo.ciselnik_roli_subsystemu')
      AND name = 'fk_ciselnik_roli_subsystemu_authz_role'
)
BEGIN
    ALTER TABLE dbo.ciselnik_roli_subsystemu
        ADD CONSTRAINT fk_ciselnik_roli_subsystemu_authz_role
            FOREIGN KEY (authz_role_id) REFERENCES authz.roles(id);
END;

COMMIT TRANSACTION;
GO

-- =============================================================================
-- [4/12] db_upgrade_1_3_0_cleanup_orphaned_role_permissions.sql
-- Authorization unification — Fáze C — Task C7
--
-- Účel: vyčistit authz.role_permissions od řádků, které vznikly přes starou
--       UI kompozici rolí (tab "Akce/Role/Uživatel-Role" v Nastavení). Po Fázi F
--       je seed v `PermissionSeedConfiguration.cs` jediným zdrojem pravdy pro role
--       a jejich permission mappingy.
--
-- Strategy:
--   1. Smazat authz.role_permission_projects rows navázané na custom (is_system=0) role
--   2. Smazat authz.role_permissions rows pro custom role
--   3. Deaktivovat (not smazat — kvůli auditu) custom role samotné
--
-- Idempotentní: run více-krát bezpečně. Pokud rows není, nic se nestane.
-- POZOR: spustit AŽ PO nasazení kódu s novou seed matricí (Task C2).
--        Před spuštěním doporučuji DB backup.
-- =============================================================================

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1. Vyčistit authz.role_permission_projects (INCLUDE scope mode per-project grants)
--    pro non-system role (vzniklé přes starou UI "přidej grant na konkrétní projekt").
DELETE FROM authz.role_permission_projects
WHERE role_permission_id IN (
    SELECT rp.id
    FROM authz.role_permissions rp
    INNER JOIN authz.roles r ON r.id = rp.role_id
    WHERE r.is_system = 0
);

-- 2. Smazat mappings pro custom (non-system) role — po Fázi F se tyto role
--    nebudou upravovat přes UI, takže jejich mappings jsou orphans.
DELETE FROM authz.role_permissions
WHERE role_id IN (
    SELECT id FROM authz.roles WHERE is_system = 0
);

-- 3. Deaktivovat custom role. Nemažeme je — zachováváme pro audit,
--    ať budoucí historik ví, že kdysi existovaly a kdo je vytvořil
--    (viz authz_audit_log pokud existuje).
UPDATE authz.roles
SET is_active = 0
WHERE is_system = 0 AND is_active = 1;

-- 4. Log pro audit.
PRINT '[db_upgrade_1_3_0] Cleanup orphaned role_permissions dokončen.';

COMMIT TRANSACTION;
GO

-- =============================================================================
-- [5/12] db_upgrade_1_3_1_history_and_audit_indexes.sql
-- Bug #7 — chybějící FK indexy na history + audit tabulkách
--
-- Účel: přidat nonclustered indexy na zaznam_id pro všechny history tabulky
--       a složený index (entity_type, entity_id) + created_at pro audit log.
--       Tyto tabulky jsou filtrovány přes recordIds.Contains(x.ZaznamId) v
--       ProjectService.RecordCards, ExportProjectionBuilders a dalších službách —
--       bez indexu jde o full scan při každém načtení karet projektu.
--
-- Idempotentní: IF NOT EXISTS guard zajišťuje bezpečné opakované spuštění.
-- Spouštět po nasazení kódu s HasIndex() konfiguracemi.
-- =============================================================================

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- ============================================================
-- History tabulky — index na zaznam_id
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_zaznam_historie_vlastnik_zaznam_id'
      AND object_id = OBJECT_ID('dbo.zaznam_historie_vlastnik')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_zaznam_historie_vlastnik_zaznam_id
        ON dbo.zaznam_historie_vlastnik (zaznam_id);
    PRINT '[db_upgrade_1_3_1] Created IX_zaznam_historie_vlastnik_zaznam_id';
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_zaznam_historie_terminu_zaznam_id'
      AND object_id = OBJECT_ID('dbo.zaznam_historie_terminu')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_zaznam_historie_terminu_zaznam_id
        ON dbo.zaznam_historie_terminu (zaznam_id);
    PRINT '[db_upgrade_1_3_1] Created IX_zaznam_historie_terminu_zaznam_id';
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_zaznam_historie_subsystem_zaznam_id'
      AND object_id = OBJECT_ID('dbo.zaznam_historie_subsystem')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_zaznam_historie_subsystem_zaznam_id
        ON dbo.zaznam_historie_subsystem (zaznam_id);
    PRINT '[db_upgrade_1_3_1] Created IX_zaznam_historie_subsystem_zaznam_id';
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_zaznam_historie_zmen_typu_zaznam_id'
      AND object_id = OBJECT_ID('dbo.zaznam_historie_zmen_typu')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_zaznam_historie_zmen_typu_zaznam_id
        ON dbo.zaznam_historie_zmen_typu (zaznam_id);
    PRINT '[db_upgrade_1_3_1] Created IX_zaznam_historie_zmen_typu_zaznam_id';
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_zaznam_historie_stavu_zaznamu_zaznam_id'
      AND object_id = OBJECT_ID('dbo.zaznam_historie_stavu_zaznamu')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_zaznam_historie_stavu_zaznamu_zaznam_id
        ON dbo.zaznam_historie_stavu_zaznamu (zaznam_id);
    PRINT '[db_upgrade_1_3_1] Created IX_zaznam_historie_stavu_zaznamu_zaznam_id';
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_zaznam_historie_stavu_projektu_zaznam_id'
      AND object_id = OBJECT_ID('dbo.zaznam_historie_stavu_projektu')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_zaznam_historie_stavu_projektu_zaznam_id
        ON dbo.zaznam_historie_stavu_projektu (zaznam_id);
    PRINT '[db_upgrade_1_3_1] Created IX_zaznam_historie_stavu_projektu_zaznam_id';
END

-- zaznam_externi_odkazy — zaznam_id (FK, dotazováno přes recordIds.Contains)
-- (existující index ux_zaznam_externi_odkazy_cislo_in_vyzve pokrývá pouze cislo+vyzva_id)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_zaznam_externi_odkazy_zaznam_id'
      AND object_id = OBJECT_ID('dbo.zaznam_externi_odkazy')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_zaznam_externi_odkazy_zaznam_id
        ON dbo.zaznam_externi_odkazy (zaznam_id);
    PRINT '[db_upgrade_1_3_1] Created IX_zaznam_externi_odkazy_zaznam_id';
END

-- zaznam_spoluprace — PK je (zaznam_id, osoba_id), zaznam_id je leading column → PK index
-- slouží filtraci; žádný extra index potřeba není.
PRINT '[db_upgrade_1_3_1] zaznam_spoluprace: PK (zaznam_id, osoba_id) pokrývá Contains dotaz — přeskočeno.';

-- ============================================================
-- Audit log — složený index (entity_type, entity_id) + created_at
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_authz_audit_log_entity_type_entity_id'
      AND object_id = OBJECT_ID('authz.audit_log')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_authz_audit_log_entity_type_entity_id
        ON authz.audit_log (entity_type, entity_id);
    PRINT '[db_upgrade_1_3_1] Created IX_authz_audit_log_entity_type_entity_id';
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_authz_audit_log_created_at'
      AND object_id = OBJECT_ID('authz.audit_log')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_authz_audit_log_created_at
        ON authz.audit_log (created_at);
    PRINT '[db_upgrade_1_3_1] Created IX_authz_audit_log_created_at';
END

PRINT '[db_upgrade_1_3_1] Hotovo.';

COMMIT TRANSACTION;
GO

-- =============================================================================
-- [6/12] db_upgrade_1_3_2_authz_join_indexes.sql
-- H-3 — chybějící indexy na authz join sloupcích v lookup tabulkách
--
-- Účel: AuthorizationSnapshotBuilder joinuje při každém build() snapshotu
--       přes CiselnikRoliProjektu.authz_role_id a CiselnikRoliSubsystemu.authz_role_id.
--       Bez indexu jde o table-scan přes lookup tabulky při každém auth checku.
--
-- Filtrovaný index (WHERE authz_role_id IS NOT NULL) — většina lookup řádků má
-- authz_role_id = NULL (neautorizační role), filtrovaný index šetří místo.
--
-- Idempotentní: IF NOT EXISTS guard zajišťuje bezpečné opakované spuštění.
-- Spouštět po nasazení kódu s HasIndex() konfiguracemi.
-- =============================================================================

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'ix_ciselnik_roli_projektu_authz_role_id'
      AND object_id = OBJECT_ID(N'dbo.ciselnik_roli_projektu')
)
BEGIN
    CREATE NONCLUSTERED INDEX ix_ciselnik_roli_projektu_authz_role_id
        ON dbo.ciselnik_roli_projektu (authz_role_id)
        WHERE authz_role_id IS NOT NULL;
    PRINT '[db_upgrade_1_3_2] Created ix_ciselnik_roli_projektu_authz_role_id';
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'ix_ciselnik_roli_subsystemu_authz_role_id'
      AND object_id = OBJECT_ID(N'dbo.ciselnik_roli_subsystemu')
)
BEGIN
    CREATE NONCLUSTERED INDEX ix_ciselnik_roli_subsystemu_authz_role_id
        ON dbo.ciselnik_roli_subsystemu (authz_role_id)
        WHERE authz_role_id IS NOT NULL;
    PRINT '[db_upgrade_1_3_2] Created ix_ciselnik_roli_subsystemu_authz_role_id';
END

PRINT '[db_upgrade_1_3_2] Hotovo.';

COMMIT TRANSACTION;
GO

-- =============================================================================
-- [7/12] db_upgrade_1_3_3_fakturace_cleanup.sql
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

-- =============================================================================
-- [8/12] db_upgrade_1_3_4_ad_sync_settings.sql
-- Singleton settings row pro AD periodic sync job.
-- Spec: docs/superpowers/specs/2026-04-22-sync-infra-and-ad-design.md §3.1
-- Plán: docs/superpowers/plans/2026-04-22-sync-infra-and-ad.md Task 8
-- Pozn.: Očíslování 1_3_4 (plán zmiňuje 1_3_0, ale to + _1/_2/_3 jsou obsazené).
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ad_sync_settings')
BEGIN
    CREATE TABLE dbo.ad_sync_settings
    (
        id                   INT NOT NULL
            CONSTRAINT PK_ad_sync_settings PRIMARY KEY,
        is_enabled           BIT NOT NULL
            CONSTRAINT DF_ad_sync_settings_enabled DEFAULT (0),
        period_minutes       INT NOT NULL
            CONSTRAINT DF_ad_sync_settings_period DEFAULT (360),
        anchor_at            DATETIMEOFFSET NOT NULL,
        last_run_at          DATETIME2 NULL,
        last_trigger_kind    NVARCHAR(16) NULL,
        last_result_json     NVARCHAR(MAX) NULL,
        is_running           BIT NOT NULL
            CONSTRAINT DF_ad_sync_settings_running DEFAULT (0),
        run_started_at       DATETIME2 NULL,
        updated_at           DATETIME2 NOT NULL
            CONSTRAINT DF_ad_sync_settings_updated DEFAULT SYSUTCDATETIME(),
        updated_by_osoba_id  INT NULL
            CONSTRAINT FK_ad_sync_settings_osoba REFERENCES dbo.osoby(id)
    );

    PRINT N'Tabulka ad_sync_settings vytvořena.';
END
ELSE
BEGIN
    PRINT N'Tabulka ad_sync_settings už existuje, přeskakuji CREATE.';
END;
GO

-- Seed singleton row (idempotentní)
IF NOT EXISTS (SELECT 1 FROM dbo.ad_sync_settings WHERE id = 1)
BEGIN
    -- Pozn.: SYSUTCDATETIMEOFFSET() NENÍ vestavěná T-SQL funkce (ani v SQL Serveru,
    -- ani v Azure SQL Edge). Ekvivalent „UTC now jako datetimeoffset" =
    -- TODATETIMEOFFSET(SYSUTCDATETIME(), 0). Opraveno 2026-04-22.
    INSERT INTO dbo.ad_sync_settings (id, is_enabled, period_minutes, anchor_at)
    VALUES (1, 0, 360, TODATETIMEOFFSET(SYSUTCDATETIME(), 0));
    PRINT N'Seed výchozího řádku ad_sync_settings id=1 vložen.';
END
ELSE
BEGIN
    PRINT N'Seed řádek ad_sync_settings id=1 už existuje, přeskakuji INSERT.';
END;
GO

-- =============================================================================
-- [9/12] db_upgrade_1_3_5_external_link_harvested_at.sql
-- Přidá sloupec last_harvested_at do zaznam_externi_odkazy pro tracking
-- posledního auto-harvestu ze ServiceDesku.
-- Plán B — Externí vazba v2 + auto-sync ServiceDesk (Task 1).
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF COL_LENGTH('dbo.zaznam_externi_odkazy', 'last_harvested_at') IS NULL
BEGIN
    ALTER TABLE dbo.zaznam_externi_odkazy
        ADD last_harvested_at DATETIME2 NULL;

    PRINT N'Sloupec last_harvested_at přidán.';
END
ELSE
BEGIN
    PRINT N'Sloupec last_harvested_at už existuje, přeskakuji.';
END
GO

-- =============================================================================
-- [10/12] db_upgrade_1_3_6_vyjadreni_vazba.sql
-- Idempotent: creates zaznam_harmonogram_vyjadreni_vazba table for Plán C
-- (chat modal + vyjádření harvest). Maps one HOT_VYJADRENI to one harmonogram
-- step (KrokKey) per projektový záznam. Source = 1 (Auto) or 2 (Manual).
-- Stav = 1 (Active), 2 (Superseded), 3 (Deleted) — append-only with history.
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'zaznam_harmonogram_vyjadreni_vazba')
BEGIN
    CREATE TABLE dbo.zaznam_harmonogram_vyjadreni_vazba
    (
        id                  INT IDENTITY PRIMARY KEY,
        zaznam_id           INT NOT NULL
            CONSTRAINT FK_zhvv_zaznam REFERENCES dbo.projektove_zaznamy(id) ON DELETE CASCADE,
        krok_key            UNIQUEIDENTIFIER NOT NULL,
        externi_odkaz_id    INT NOT NULL
            CONSTRAINT FK_zhvv_externi_odkaz REFERENCES dbo.zaznam_externi_odkazy(id) ON DELETE NO ACTION,
        hot_vyjadreni_id    BIGINT NOT NULL,
        datum_vyjadreni     DATETIME2 NOT NULL,
        source              TINYINT NOT NULL,
        stav                TINYINT NOT NULL,
        created_at          DATETIME2 NOT NULL CONSTRAINT DF_zhvv_created_at DEFAULT SYSUTCDATETIME(),
        created_by_osoba_id INT NULL
            CONSTRAINT FK_zhvv_osoba_created REFERENCES dbo.osoby(id),
        deleted_at          DATETIME2 NULL,
        deleted_by_osoba_id INT NULL
            CONSTRAINT FK_zhvv_osoba_deleted REFERENCES dbo.osoby(id)
    );

    CREATE INDEX ix_zhvv_zaznam_krok_stav
        ON dbo.zaznam_harmonogram_vyjadreni_vazba(zaznam_id, krok_key, stav);
    CREATE INDEX ix_zhvv_externi_odkaz
        ON dbo.zaznam_harmonogram_vyjadreni_vazba(externi_odkaz_id);

    PRINT N'Tabulka zaznam_harmonogram_vyjadreni_vazba vytvořena.';
END
ELSE
BEGIN
    PRINT N'Tabulka zaznam_harmonogram_vyjadreni_vazba už existuje.';
END;
GO

-- =============================================================================
-- [11/12] db_upgrade_1_3_7_sd_sync_settings_and_fingerprint.sql
--
-- ServiceDesk sync konfigurace + fingerprint detekce pro zaznam_externi_odkazy.
-- Navazuje na db_upgrade_1_3_4_ad_sync_settings.sql (shared sync infra).
--
-- Spec: docs/superpowers/specs/2026-04-22-sync-infra-and-ad-design.md §4, §5
-- Plán: docs/superpowers/plans/2026-04-22-sd-sync-revise.md Task 1 / Task 5 / Task 6
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- 1) sd_active_sync_settings (singleton row id=1, HOT_ZAZNAMY.stav <> 'archiv')
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'sd_active_sync_settings')
BEGIN
    CREATE TABLE dbo.sd_active_sync_settings (
        id                   INT NOT NULL CONSTRAINT PK_sd_active_sync_settings PRIMARY KEY,
        is_enabled           BIT NOT NULL CONSTRAINT DF_sd_active_sync_enabled DEFAULT (0),
        period_minutes       INT NOT NULL CONSTRAINT DF_sd_active_sync_period DEFAULT (60),
        anchor_at            DATETIMEOFFSET NOT NULL,
        last_run_at          DATETIME2 NULL,
        last_trigger_kind    NVARCHAR(16) NULL,
        last_result_json     NVARCHAR(MAX) NULL,
        is_running           BIT NOT NULL CONSTRAINT DF_sd_active_sync_running DEFAULT (0),
        run_started_at       DATETIME2 NULL,
        updated_at           DATETIME2 NOT NULL CONSTRAINT DF_sd_active_sync_updated DEFAULT SYSUTCDATETIME(),
        updated_by_osoba_id  INT NULL
            CONSTRAINT FK_sd_active_sync_osoba REFERENCES dbo.osoby(id)
    );
    PRINT N'Tabulka sd_active_sync_settings vytvořena.';
END
ELSE
BEGIN
    PRINT N'Tabulka sd_active_sync_settings už existuje, přeskakuji CREATE.';
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.sd_active_sync_settings WHERE id = 1)
BEGIN
    INSERT INTO dbo.sd_active_sync_settings (id, is_enabled, period_minutes, anchor_at)
    VALUES (1, 0, 60, CAST('2026-01-01T00:00:00+00:00' AS DATETIMEOFFSET));
    PRINT N'Seed výchozího řádku sd_active_sync_settings id=1 vložen.';
END
ELSE
BEGIN
    PRINT N'Seed řádek sd_active_sync_settings id=1 už existuje, přeskakuji INSERT.';
END;
GO

-- 2) sd_archive_sync_settings (singleton row id=1, HOT_ZAZNAMY.stav = 'archiv')
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'sd_archive_sync_settings')
BEGIN
    CREATE TABLE dbo.sd_archive_sync_settings (
        id                   INT NOT NULL CONSTRAINT PK_sd_archive_sync_settings PRIMARY KEY,
        is_enabled           BIT NOT NULL CONSTRAINT DF_sd_archive_sync_enabled DEFAULT (0),
        period_minutes       INT NOT NULL CONSTRAINT DF_sd_archive_sync_period DEFAULT (1440),
        anchor_at            DATETIMEOFFSET NOT NULL,
        last_run_at          DATETIME2 NULL,
        last_trigger_kind    NVARCHAR(16) NULL,
        last_result_json     NVARCHAR(MAX) NULL,
        is_running           BIT NOT NULL CONSTRAINT DF_sd_archive_sync_running DEFAULT (0),
        run_started_at       DATETIME2 NULL,
        updated_at           DATETIME2 NOT NULL CONSTRAINT DF_sd_archive_sync_updated DEFAULT SYSUTCDATETIME(),
        updated_by_osoba_id  INT NULL
            CONSTRAINT FK_sd_archive_sync_osoba REFERENCES dbo.osoby(id)
    );
    PRINT N'Tabulka sd_archive_sync_settings vytvořena.';
END
ELSE
BEGIN
    PRINT N'Tabulka sd_archive_sync_settings už existuje, přeskakuji CREATE.';
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.sd_archive_sync_settings WHERE id = 1)
BEGIN
    INSERT INTO dbo.sd_archive_sync_settings (id, is_enabled, period_minutes, anchor_at)
    VALUES (1, 0, 1440, CAST('2026-01-01T04:00:00+00:00' AS DATETIMEOFFSET));
    PRINT N'Seed výchozího řádku sd_archive_sync_settings id=1 vložen.';
END
ELSE
BEGIN
    PRINT N'Seed řádek sd_archive_sync_settings id=1 už existuje, přeskakuji INSERT.';
END;
GO

-- 3) Fingerprint sloupce na zaznam_externi_odkazy (spec §5.2)
IF COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'last_known_hot_zaznam_datum') IS NULL
BEGIN
    ALTER TABLE dbo.zaznam_externi_odkazy
        ADD last_known_hot_zaznam_datum DATETIME2 NULL;
    PRINT N'Sloupec last_known_hot_zaznam_datum přidán.';
END
ELSE
BEGIN
    PRINT N'Sloupec last_known_hot_zaznam_datum už existuje.';
END;
GO

IF COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'last_known_max_vyjadreni_id') IS NULL
BEGIN
    ALTER TABLE dbo.zaznam_externi_odkazy
        ADD last_known_max_vyjadreni_id BIGINT NULL;
    PRINT N'Sloupec last_known_max_vyjadreni_id přidán.';
END
ELSE
BEGIN
    PRINT N'Sloupec last_known_max_vyjadreni_id už existuje.';
END;
GO

IF COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'last_known_vyjadreni_count') IS NULL
BEGIN
    ALTER TABLE dbo.zaznam_externi_odkazy
        ADD last_known_vyjadreni_count INT NULL;
    PRINT N'Sloupec last_known_vyjadreni_count přidán.';
END
ELSE
BEGIN
    PRINT N'Sloupec last_known_vyjadreni_count už existuje.';
END;
GO

PRINT N'db_upgrade_1_3_7 dokončen.';
GO

-- =============================================================================
-- [12/12] db_upgrade_1_3_8_authz_per_action_redesign.sql
--
-- Per-action authz redesign 2026-04-23 — smaže z authz.* tabulek 8 deprecated
-- klíčů nahrazených per-action variantami.
--
-- ROZSAH MIGRACE (BEZPEČNOSTNÍ INVARIANT — viz zadání v CLAUDE.md / memory):
--   * Smaže záznamy POUZE z:
--       - authz.role_permission_projects
--       - authz.role_permissions
--       - authz.permissions
--     a to STRIKTNĚ FILTROVANÉ podle seznamu 8 deprecated klíčů.
--   * NEDOTKNE se:
--       - authz.user_roles    (přiřazení rolí uživatelům)
--       - authz.roles         (katalog rolí)
--       - authz.permission_categories
--       - authz.audit_log
--       - žádných business tabulek (osoby, projekty, projektove_zaznamy, obsazeni_projektu,
--         jednani, vyjadreni, historie_*, apod.)
--
-- DEPRECATED KLÍČE (pre-redesign), které migrace odstraní:
--   1. records.schedule.add            → nahrazeno: proposals.schedule.create
--   2. records.comment.subsystemlead   → nahrazeno: proposals.record.create + meetings.notes.subsystemlead
--   3. team.manage                     → nahrazeno: team.member.add/.remove / team.role.assign/.deactivate
--                                                   / team.subsystem.create/.reorder/.deactivate
--                                                   / team.subsystem.role.assign/.deactivate
--                                                   / team.candidates.search
--   4. people.manage                   → nahrazeno: people.create/.edit/.delete/.ad.search/.ad.sync
--   5. ciselniky.edit                  → nahrazeno: ciselniky.row.edit / ciselniky.row.delete
--   6. settings.manage                 → nahrazeno: settings.roles.assign / .sync.configure / .sync.run / .sd.view
--   7. export.pdf                      → nahrazeno: export.pdf.projekt / .jednani / .ukol
--   8. export.word                     → nahrazeno: export.word.projekt / .jednani / .ukol
--
-- Nové per-action klíče jsou automaticky doseedovány aplikačním
-- PermissionSeederem při spuštění aplikace (idempotentní UPSERT z
-- PermissionSeedConfiguration.Actions).
--
-- Idempotence: skript lze spustit opakovaně — DELETE s NOT EXISTS predikátem
-- projde bez chyby i po úspěšné předchozí migraci.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

PRINT N'[1.3.8] Per-action authz redesign — start';

DECLARE @DeprecatedKeys TABLE (klic NVARCHAR(150) NOT NULL PRIMARY KEY);
INSERT INTO @DeprecatedKeys (klic) VALUES
    (N'records.schedule.add'),
    (N'records.comment.subsystemlead'),
    (N'team.manage'),
    (N'people.manage'),
    (N'ciselniky.edit'),
    (N'settings.manage'),
    (N'export.pdf'),
    (N'export.word');

-- Audit pre-state: kolik řádků dotčeno.
DECLARE @PermissionsCount INT = (
    SELECT COUNT(*) FROM authz.permissions p
    INNER JOIN @DeprecatedKeys d ON d.klic = p.klic
);
DECLARE @RolePermissionsCount INT = (
    SELECT COUNT(*) FROM authz.role_permissions rp
    INNER JOIN authz.permissions p ON p.id = rp.permission_id
    INNER JOIN @DeprecatedKeys d ON d.klic = p.klic
);
DECLARE @RolePermissionProjectsCount INT = (
    SELECT COUNT(*) FROM authz.role_permission_projects rpp
    INNER JOIN authz.role_permissions rp ON rp.id = rpp.role_permission_id
    INNER JOIN authz.permissions p ON p.id = rp.permission_id
    INNER JOIN @DeprecatedKeys d ON d.klic = p.klic
);

PRINT N'[1.3.8] Pre-state:';
PRINT N'[1.3.8]   authz.permissions řádků k smazání: ' + CAST(@PermissionsCount AS NVARCHAR(16));
PRINT N'[1.3.8]   authz.role_permissions řádků k smazání: ' + CAST(@RolePermissionsCount AS NVARCHAR(16));
PRINT N'[1.3.8]   authz.role_permission_projects řádků k smazání: ' + CAST(@RolePermissionProjectsCount AS NVARCHAR(16));

-- 1) role_permission_projects (scope INCLUDE → konkrétní projekty) — mazat jako první
--    kvůli FK na role_permissions.
DELETE rpp
FROM authz.role_permission_projects rpp
INNER JOIN authz.role_permissions rp ON rp.id = rpp.role_permission_id
INNER JOIN authz.permissions p ON p.id = rp.permission_id
INNER JOIN @DeprecatedKeys d ON d.klic = p.klic;

PRINT N'[1.3.8] Smazáno z authz.role_permission_projects: ' + CAST(@@ROWCOUNT AS NVARCHAR(16));

-- 2) role_permissions (vazba role → permission) — pak je FK volná pro permissions.
DELETE rp
FROM authz.role_permissions rp
INNER JOIN authz.permissions p ON p.id = rp.permission_id
INNER JOIN @DeprecatedKeys d ON d.klic = p.klic;

PRINT N'[1.3.8] Smazáno z authz.role_permissions: ' + CAST(@@ROWCOUNT AS NVARCHAR(16));

-- 3) permissions (katalog akcí) — nakonec, když už nejsou FK závislosti.
DELETE p
FROM authz.permissions p
INNER JOIN @DeprecatedKeys d ON d.klic = p.klic;

PRINT N'[1.3.8] Smazáno z authz.permissions: ' + CAST(@@ROWCOUNT AS NVARCHAR(16));

-- Post-state sanity check: 0 řádků pro deprecated klíče.
DECLARE @RemainingPermissions INT = (
    SELECT COUNT(*) FROM authz.permissions p
    INNER JOIN @DeprecatedKeys d ON d.klic = p.klic
);
IF @RemainingPermissions <> 0
BEGIN
    ROLLBACK TRANSACTION;
    RAISERROR(N'[1.3.8] KONZISTENCE: v authz.permissions zbylo %d řádků pro deprecated klíče — migrace přerušena.', 16, 1, @RemainingPermissions);
    RETURN;
END

PRINT N'[1.3.8] Per-action authz redesign — dokončeno.';
PRINT N'[1.3.8] Nové per-action klíče doseedují PermissionSeederem při startu aplikace.';

COMMIT TRANSACTION;
GO

PRINT N'==============================================================================';
PRINT N'db_upgrade_1_1_8_to_1_3_8_combined.sql DOKONČEN';
PRINT N'DB verze po úspěšném dokončení: 1_3_8';
PRINT N'==============================================================================';
GO

-- =============================================================================
-- ROLLBACK pro [12/12] (pokud migraci potřebuješ vrátit) — z původního 1_3_8:
--
-- 1) Aplikační PermissionSeeder znovu nevytváří deprecated klíče (jsou smazány
--    z PermissionSeedConfiguration.Actions ve fázi F7). Nejprve je nutné vrátit
--    C# kód (revert commit F7) — bez toho by rollback SQL jenom znovu vložil
--    řádky, které aplikace při startu nebude znát.
--
-- 2) Poté obnov deprecated katalog:
--
-- BEGIN TRANSACTION;
-- DECLARE @CatRecId INT = (SELECT id FROM authz.permission_categories WHERE kod = N'RECORDS');
-- DECLARE @CatTeamId INT = (SELECT id FROM authz.permission_categories WHERE kod = N'TEAM');
-- DECLARE @CatPeopleId INT = (SELECT id FROM authz.permission_categories WHERE kod = N'PEOPLE');
-- DECLARE @CatCisId INT = (SELECT id FROM authz.permission_categories WHERE kod = N'CISELNIKY');
-- DECLARE @CatSetId INT = (SELECT id FROM authz.permission_categories WHERE kod = N'SETTINGS');
-- DECLARE @CatExpId INT = (SELECT id FROM authz.permission_categories WHERE kod = N'EXPORT');
--
-- INSERT INTO authz.permissions (klic, nazev, category_id, scope_level, is_active, is_system) VALUES
--   (N'records.schedule.add', N'Doplňovat harmonogram úkolu (deprecated)', @CatRecId, N'PROJECT', 1, 1),
--   (N'records.comment.subsystemlead', N'Vyjádření vedoucího subsystému (deprecated)', @CatRecId, N'PROJECT', 1, 1),
--   (N'team.manage', N'Správa týmu (deprecated)', @CatTeamId, N'PROJECT', 1, 1),
--   (N'people.manage', N'Správa osob (deprecated)', @CatPeopleId, N'GLOBAL', 1, 1),
--   (N'ciselniky.edit', N'Editace číselníků (deprecated)', @CatCisId, N'GLOBAL', 1, 1),
--   (N'settings.manage', N'Správa nastavení (deprecated)', @CatSetId, N'GLOBAL', 1, 1),
--   (N'export.pdf', N'Export PDF (deprecated)', @CatExpId, N'PROJECT', 1, 1),
--   (N'export.word', N'Export Word (deprecated)', @CatExpId, N'PROJECT', 1, 1);
-- -- RoleMappings dohraje PermissionSeeder po revertovaní C# kódu.
-- COMMIT TRANSACTION;
--
-- POZNÁMKA: user_roles / osoby / projekty / záznamy / historie NEBYLY dotčeny
-- touto migrací, takže rollback obnovuje pouze permission katalog — uživatelské
-- přiřazení rolí zůstávají v celistvém stavu.
-- =============================================================================
