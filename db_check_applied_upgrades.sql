-- =============================================================================
-- db_check_applied_upgrades.sql
--
-- Diagnostika: které db_upgrade_*.sql skripty jsou v TÉTO databázi nasazené.
-- Určeno pro produkci, kde není záznam o naposledy spuštěné migraci.
--
-- POUZE ČTE — dotazuje se na systémový katalog (sys.*) a pár SELECT COUNT
-- nad číselníky/authz. Nic nemění, žádné DDL, žádné DELETE/UPDATE.
--
-- Výstup (3 result sety):
--   1) přehled všech upgrade skriptů se stavem:
--        APLIKOVÁN    — otisk skriptu v DB nalezen
--        CHYBÍ        — otisk nenalezen, skript je potřeba spustit
--        PŘESKOČIT    — skript mění objekty, které migrace 1.4.0 ruší;
--                       při skoku rovnou na 1.4.0 není potřeba
--        NELZE OVĚŘIT — chybí objekt, nad kterým se otisk kontroluje
--   2) chybějící skripty v pořadí, v jakém je spustit
--   3) poznámky k interpretaci
--
-- Princip: neexistuje tabulka s logem migrací, proto se každý skript detekuje
-- podle svého "otisku" (tabulka / sloupec / index / constraint / data), který
-- v DB zanechal. Datové kontroly běží přes sp_executesql, aby batch nespadl
-- na compile chybě, když cílová tabulka v této verzi DB ještě/už neexistuje.
-- =============================================================================
SET NOCOUNT ON;

DECLARE @r TABLE (
    poradi  INT           NOT NULL PRIMARY KEY,
    skript  NVARCHAR(80)  NOT NULL,
    stav    NVARCHAR(60)  NOT NULL,
    detekce NVARCHAR(400) NOT NULL
);

-- ---------------------------------------------------------------------------
-- Sdílené příznaky
-- ---------------------------------------------------------------------------
DECLARE @hodnoty_existuje BIT = CASE WHEN OBJECT_ID(N'dbo.zaznam_harmonogram_hodnoty', N'U') IS NOT NULL THEN 1 ELSE 0 END;
DECLARE @krok_existuje    BIT = CASE WHEN OBJECT_ID(N'dbo.zaznam_harmonogram_krok',    N'U') IS NOT NULL THEN 1 ELSE 0 END;
-- 1.4.0 = nová tabulka kroků existuje a starý offset model je zrušený
DECLARE @v140 BIT = CASE WHEN @krok_existuje = 1 AND @hodnoty_existuje = 0 THEN 1 ELSE 0 END;

-- ---------------------------------------------------------------------------
-- Datové kontroly (dynamicky — tabulka nemusí v této verzi DB existovat)
-- ---------------------------------------------------------------------------
DECLARE @role_proj_man_gest INT;    -- 1_1_6: kódy PROJ_MAN + GEST v ciselnik_roli_projektu
IF OBJECT_ID(N'dbo.ciselnik_roli_projektu', N'U') IS NOT NULL
    EXEC sp_executesql
        N'SELECT @c = COUNT(*) FROM dbo.ciselnik_roli_projektu WHERE kod IN (N''PROJ_MAN'', N''GEST'');',
        N'@c INT OUTPUT', @c = @role_proj_man_gest OUTPUT;

DECLARE @stav_new INT;              -- 1_1_7: stav NEW v ciselnik_stavu_ukolu
IF OBJECT_ID(N'dbo.ciselnik_stavu_ukolu', N'U') IS NOT NULL
    EXEC sp_executesql
        N'SELECT @c = COUNT(*) FROM dbo.ciselnik_stavu_ukolu WHERE kod = N''NEW'';',
        N'@c INT OUTPUT', @c = @stav_new OUTPUT;

DECLARE @custom_role_mappings INT;  -- 1_3_0: mappingy custom (is_system=0) rolí
IF OBJECT_ID(N'authz.role_permissions', N'U') IS NOT NULL AND OBJECT_ID(N'authz.roles', N'U') IS NOT NULL
    EXEC sp_executesql
        N'SELECT @c = COUNT(*) FROM authz.role_permissions rp
          INNER JOIN authz.roles r ON r.id = rp.role_id WHERE r.is_system = 0;',
        N'@c INT OUTPUT', @c = @custom_role_mappings OUTPUT;

DECLARE @hs11 INT;                  -- 1_3_3: fakturační řádky HS11 v číselníku harmonogramu
IF OBJECT_ID(N'dbo.ciselnik_harmonogram_typu', N'U') IS NOT NULL
    EXEC sp_executesql
        N'SELECT @c = COUNT(*) FROM dbo.ciselnik_harmonogram_typu
          WHERE kod LIKE N''HS11[_]%'' OR nazev LIKE N''%fakturace%'';',
        N'@c INT OUTPUT', @c = @hs11 OUTPUT;

DECLARE @deprecated_klice INT;      -- 1_3_8: 8 deprecated klíčů v authz.permissions
IF OBJECT_ID(N'authz.permissions', N'U') IS NOT NULL
    EXEC sp_executesql
        N'SELECT @c = COUNT(*) FROM authz.permissions WHERE klic IN (
            N''records.schedule.add'', N''records.comment.subsystemlead'',
            N''team.manage'', N''people.manage'', N''ciselniky.edit'',
            N''settings.manage'', N''export.pdf'', N''export.word'');',
        N'@c INT OUTPUT', @c = @deprecated_klice OUTPUT;

DECLARE @schedule_preview INT;      -- 1_4_1: mrtvý klíč schedule.preview v authz.permissions
IF OBJECT_ID(N'authz.permissions', N'U') IS NOT NULL
    EXEC sp_executesql
        N'SELECT @c = COUNT(*) FROM authz.permissions WHERE klic = N''schedule.preview'';',
        N'@c INT OUTPUT', @c = @schedule_preview OUTPUT;

DECLARE @pozadavek INT = CASE
    WHEN COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'pozadavek') IS NOT NULL THEN 1 ELSE 0 END;
    -- 1_4_2: sloupec s textem požadavku u externí vazby

DECLARE @kalkulace INT = CASE
    WHEN COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'kalkulace_cena') IS NOT NULL
     AND COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'kalkulace_id') IS NOT NULL
     AND COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'kalkulace_nacteno') IS NOT NULL
    THEN 1 ELSE 0 END;
    -- 1_4_3: snímek skutečné ceny z kalkulace u externí vazby

DECLARE @searchCleanup INT = CASE
    WHEN OBJECT_ID(N'dbo.SearchIndex', N'U') IS NULL
     AND OBJECT_ID(N'dbo.search_reindex_checkpoint', N'U') IS NULL
     AND NOT EXISTS (SELECT 1 FROM authz.permissions WHERE klic = N'search.reindex')
    THEN 1 ELSE 0 END;
    -- 1_4_4: úklid po zrušené indexové vrstvě vyhledávání

DECLARE @editZamek INT = CASE
    WHEN OBJECT_ID(N'dbo.zaznam_edit_zamek', N'U') IS NOT NULL THEN 1 ELSE 0 END;
    -- 1_4_5: tabulka zámku karty záznamu (souběžná editace)

-- ---------------------------------------------------------------------------
-- Otisky jednotlivých skriptů (v pořadí nasazování)
-- ---------------------------------------------------------------------------

INSERT @r VALUES (10, N'db_upgrade_0_4_membership_subsystems',
    CASE WHEN OBJECT_ID(N'dbo.projekt_subsystemy', N'U') IS NOT NULL
          AND COL_LENGTH(N'dbo.obsazeni_projektu', N'datum_prirazeni') IS NOT NULL
         THEN N'APLIKOVÁN' ELSE N'CHYBÍ' END,
    N'tabulka projekt_subsystemy + sloupec obsazeni_projektu.datum_prirazeni');

INSERT @r VALUES (20, N'db_upgrade_1_1_0_signed_schedule_actual',
    CASE WHEN @v140 = 1 THEN N'PŘESKOČIT (objekt zrušila 1.4.0)'
         WHEN @hodnoty_existuje = 0 THEN N'NELZE OVĚŘIT'
         WHEN NOT EXISTS (SELECT 1 FROM sys.check_constraints
                          WHERE parent_object_id = OBJECT_ID(N'dbo.zaznam_harmonogram_hodnoty')
                            AND name = N'CK_zaznam_harmonogram_hodnoty_hodnota_nonnegative')
              THEN N'APLIKOVÁN'
         ELSE N'CHYBÍ' END,
    N'absence CHECK constraintu CK_zaznam_harmonogram_hodnoty_hodnota_nonnegative');

INSERT @r VALUES (30, N'db_upgrade_1_1_1_external_link_estimated_price',
    CASE WHEN COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'predpokladana_cena') IS NOT NULL
         THEN N'APLIKOVÁN' ELSE N'CHYBÍ' END,
    N'sloupec zaznam_externi_odkazy.predpokladana_cena');

INSERT @r VALUES (40, N'db_upgrade_1_1_2_project_subsystem_order',
    CASE WHEN COL_LENGTH(N'dbo.projekt_subsystemy', N'poradi') IS NOT NULL
         THEN N'APLIKOVÁN' ELSE N'CHYBÍ' END,
    N'sloupec projekt_subsystemy.poradi');

INSERT @r VALUES (50, N'db_upgrade_1_1_3_record_proposals',
    CASE WHEN OBJECT_ID(N'dbo.zaznam_navrhy', N'U') IS NOT NULL
         THEN N'APLIKOVÁN' ELSE N'CHYBÍ' END,
    N'tabulka zaznam_navrhy');

INSERT @r VALUES (60, N'db_upgrade_1_1_4_record_priority_matrix',
    CASE WHEN OBJECT_ID(N'dbo.zaznam_priority_uzivatelu', N'U') IS NOT NULL
          AND OBJECT_ID(N'dbo.zaznam_priority_rebuild_state', N'U') IS NOT NULL
         THEN N'APLIKOVÁN' ELSE N'CHYBÍ' END,
    N'tabulky zaznam_priority_uzivatelu + zaznam_priority_rebuild_state');

-- 1_1_5 zavedla search_reindex_checkpoint, kterou 1_4_4 zase ruší. Sonda na
-- existenci tabulky by proto po 1_4_4 hlásila CHYBÍ navždy. Rozhoduje pořadí:
-- dokud 1_4_4 neproběhla, tabulka tu má být; poté tu být nesmí.
INSERT @r VALUES (70, N'db_upgrade_1_1_5_search_checkpoint',
    CASE WHEN OBJECT_ID(N'dbo.search_reindex_checkpoint', N'U') IS NOT NULL THEN N'APLIKOVÁN'
         WHEN @searchCleanup = 1 THEN N'NEAKTUÁLNÍ (zrušeno v 1_4_4)'
         ELSE N'CHYBÍ' END,
    N'tabulka search_reindex_checkpoint (ruší ji 1_4_4)');

INSERT @r VALUES (80, N'db_upgrade_1_1_6_project_roles_manager_gestor',
    CASE WHEN @role_proj_man_gest IS NULL THEN N'NELZE OVĚŘIT'
         WHEN @role_proj_man_gest = 2 THEN N'APLIKOVÁN'
         ELSE N'CHYBÍ' END,
    N'kódy PROJ_MAN + GEST v ciselnik_roli_projektu (nalezeno: '
        + ISNULL(CAST(@role_proj_man_gest AS NVARCHAR(10)), N'-') + N' z 2)');

INSERT @r VALUES (90, N'db_upgrade_1_1_7_new_task_status',
    CASE WHEN @stav_new IS NULL THEN N'NELZE OVĚŘIT'
         WHEN @stav_new >= 1 THEN N'APLIKOVÁN'
         ELSE N'CHYBÍ' END,
    N'kód NEW (Nezahájeno) v ciselnik_stavu_ukolu');

INSERT @r VALUES (100, N'db_upgrade_1_1_8_vyzvy',
    CASE WHEN OBJECT_ID(N'dbo.vyzvy', N'U') IS NOT NULL
          AND COL_LENGTH(N'dbo.projekty', N'misto_plneni') IS NOT NULL
         THEN N'APLIKOVÁN' ELSE N'CHYBÍ' END,
    N'tabulka vyzvy + sloupec projekty.misto_plneni');

INSERT @r VALUES (110, N'db_upgrade_1_2_0_authz_role_scope',
    CASE WHEN OBJECT_ID(N'authz.roles', N'U') IS NULL THEN N'NELZE OVĚŘIT (authz schéma chybí)'
         WHEN COL_LENGTH(N'authz.roles', N'scope') IS NOT NULL THEN N'APLIKOVÁN'
         ELSE N'CHYBÍ' END,
    N'sloupec authz.roles.scope');

INSERT @r VALUES (120, N'db_upgrade_1_2_1_lookup_role_authz_fk',
    CASE WHEN COL_LENGTH(N'dbo.ciselnik_roli_projektu', N'authz_role_id') IS NOT NULL
          AND COL_LENGTH(N'dbo.ciselnik_roli_subsystemu', N'authz_role_id') IS NOT NULL
         THEN N'APLIKOVÁN' ELSE N'CHYBÍ' END,
    N'sloupce ciselnik_roli_projektu.authz_role_id + ciselnik_roli_subsystemu.authz_role_id');

INSERT @r VALUES (130, N'db_upgrade_1_3_0_cleanup_orphaned_role_permissions',
    CASE WHEN @custom_role_mappings IS NULL THEN N'NELZE OVĚŘIT (authz schéma chybí)'
         WHEN @custom_role_mappings = 0 THEN N'APLIKOVÁN (nebo nebylo co čistit)'
         ELSE N'CHYBÍ' END,
    N'absence mappingů custom rolí v authz.role_permissions (nalezeno: '
        + ISNULL(CAST(@custom_role_mappings AS NVARCHAR(10)), N'-') + N')');

INSERT @r VALUES (140, N'db_upgrade_1_3_1_history_and_audit_indexes',
    CASE WHEN EXISTS (SELECT 1 FROM sys.indexes
                      WHERE name = N'IX_zaznam_historie_vlastnik_zaznam_id'
                        AND object_id = OBJECT_ID(N'dbo.zaznam_historie_vlastnik'))
         THEN N'APLIKOVÁN' ELSE N'CHYBÍ' END,
    N'index IX_zaznam_historie_vlastnik_zaznam_id (reprezentant sady indexů)');

INSERT @r VALUES (150, N'db_upgrade_1_3_2_authz_join_indexes',
    CASE WHEN EXISTS (SELECT 1 FROM sys.indexes
                      WHERE name = N'ix_ciselnik_roli_projektu_authz_role_id'
                        AND object_id = OBJECT_ID(N'dbo.ciselnik_roli_projektu'))
         THEN N'APLIKOVÁN' ELSE N'CHYBÍ' END,
    N'index ix_ciselnik_roli_projektu_authz_role_id');

INSERT @r VALUES (160, N'db_upgrade_1_3_3_fakturace_cleanup',
    CASE WHEN @v140 = 1 THEN N'PŘESKOČIT (objekt zrušila 1.4.0)'
         WHEN @hs11 IS NULL THEN N'NELZE OVĚŘIT'
         WHEN @hs11 = 0 THEN N'APLIKOVÁN (nebo HS11 nikdy neexistoval)'
         ELSE N'CHYBÍ' END,
    N'absence řádků HS11/fakturace v ciselnik_harmonogram_typu (nalezeno: '
        + ISNULL(CAST(@hs11 AS NVARCHAR(10)), N'-') + N')');

INSERT @r VALUES (170, N'db_upgrade_1_3_4_ad_sync_settings',
    CASE WHEN OBJECT_ID(N'dbo.ad_sync_settings', N'U') IS NOT NULL
         THEN N'APLIKOVÁN' ELSE N'CHYBÍ' END,
    N'tabulka ad_sync_settings');

INSERT @r VALUES (180, N'db_upgrade_1_3_5_external_link_harvested_at',
    CASE WHEN COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'last_harvested_at') IS NOT NULL
         THEN N'APLIKOVÁN' ELSE N'CHYBÍ' END,
    N'sloupec zaznam_externi_odkazy.last_harvested_at');

INSERT @r VALUES (190, N'db_upgrade_1_3_6_vyjadreni_vazba',
    CASE WHEN OBJECT_ID(N'dbo.zaznam_harmonogram_vyjadreni_vazba', N'U') IS NOT NULL
         THEN N'APLIKOVÁN' ELSE N'CHYBÍ' END,
    N'tabulka zaznam_harmonogram_vyjadreni_vazba');

INSERT @r VALUES (200, N'db_upgrade_1_3_7_sd_sync_settings_and_fingerprint',
    CASE WHEN OBJECT_ID(N'dbo.sd_active_sync_settings', N'U') IS NOT NULL
          AND OBJECT_ID(N'dbo.sd_archive_sync_settings', N'U') IS NOT NULL
          AND COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'last_known_hot_zaznam_datum') IS NOT NULL
         THEN N'APLIKOVÁN' ELSE N'CHYBÍ' END,
    N'tabulky sd_active/archive_sync_settings + sloupec zaznam_externi_odkazy.last_known_hot_zaznam_datum');

INSERT @r VALUES (210, N'db_upgrade_1_3_8_authz_per_action_redesign',
    CASE WHEN @deprecated_klice IS NULL THEN N'NELZE OVĚŘIT (authz schéma chybí)'
         WHEN @deprecated_klice = 0 THEN N'APLIKOVÁN'
         ELSE N'CHYBÍ' END,
    N'absence 8 deprecated klíčů (team.manage, export.pdf, ...) v authz.permissions (nalezeno: '
        + ISNULL(CAST(@deprecated_klice AS NVARCHAR(10)), N'-') + N')');

INSERT @r VALUES (220, N'db_upgrade_1_3_10_harmonogram_skutecnost_zdroj',
    CASE WHEN @v140 = 1 THEN N'PŘESKOČIT (objekt zrušila 1.4.0)'
         WHEN @hodnoty_existuje = 0 THEN N'NELZE OVĚŘIT'
         WHEN COL_LENGTH(N'dbo.zaznam_harmonogram_hodnoty', N'skutecnost_zdroj') IS NOT NULL
              THEN N'APLIKOVÁN'
         ELSE N'CHYBÍ' END,
    N'sloupec zaznam_harmonogram_hodnoty.skutecnost_zdroj');

INSERT @r VALUES (230, N'db_upgrade_1_3_11_projekty_infosystem',
    CASE WHEN COL_LENGTH(N'dbo.projekty', N'servicedesk_info_system_id') IS NOT NULL
         THEN N'APLIKOVÁN' ELSE N'CHYBÍ' END,
    N'sloupec projekty.servicedesk_info_system_id');

INSERT @r VALUES (240, N'db_upgrade_1_3_12_record_delete_cascade',
    CASE WHEN EXISTS (SELECT 1 FROM sys.foreign_keys
                      WHERE name = N'FK_zaznam_historie_zmen_typu_zaznam'
                        AND parent_object_id = OBJECT_ID(N'dbo.zaznam_historie_zmen_typu')
                        AND delete_referential_action = 1)
         THEN N'APLIKOVÁN' ELSE N'CHYBÍ' END,
    N'FK FK_zaznam_historie_zmen_typu_zaznam s ON DELETE CASCADE (reprezentant sady FK)');

INSERT @r VALUES (250, N'db_upgrade_1_3_13_proposal_supersede',
    CASE WHEN COL_LENGTH(N'dbo.zaznam_navrhy', N'superseded_by_proposal_id') IS NOT NULL
         THEN N'APLIKOVÁN' ELSE N'CHYBÍ' END,
    N'sloupec zaznam_navrhy.superseded_by_proposal_id');

INSERT @r VALUES (260, N'db_upgrade_1_3_14_delay_nullable',
    CASE WHEN @v140 = 1 THEN N'PŘESKOČIT (objekt zrušila 1.4.0)'
         WHEN @hodnoty_existuje = 0 THEN N'NELZE OVĚŘIT'
         WHEN EXISTS (SELECT 1 FROM sys.columns
                      WHERE object_id = OBJECT_ID(N'dbo.zaznam_harmonogram_hodnoty')
                        AND name = N'hodnota_int' AND is_nullable = 1)
              THEN N'APLIKOVÁN'
         ELSE N'CHYBÍ' END,
    N'zaznam_harmonogram_hodnoty.hodnota_int je NULLable');

INSERT @r VALUES (270, N'db_upgrade_1_3_15_harmonogram_indexes',
    CASE WHEN @v140 = 1 THEN N'PŘESKOČIT (objekt zrušila 1.4.0)'
         WHEN @hodnoty_existuje = 0 THEN N'NELZE OVĚŘIT'
         WHEN EXISTS (SELECT 1 FROM sys.indexes
                      WHERE name = N'IX_zaznam_harmonogram_hodnoty_zaznam_id'
                        AND object_id = OBJECT_ID(N'dbo.zaznam_harmonogram_hodnoty'))
              THEN N'APLIKOVÁN'
         ELSE N'CHYBÍ' END,
    N'index IX_zaznam_harmonogram_hodnoty_zaznam_id');

INSERT @r VALUES (280, N'db_upgrade_1_3_16_fix_harmonogram_hodnoty_cascade',
    CASE WHEN @v140 = 1 THEN N'PŘESKOČIT (objekt zrušila 1.4.0)'
         WHEN @hodnoty_existuje = 0 THEN N'NELZE OVĚŘIT'
         WHEN EXISTS (SELECT 1 FROM sys.foreign_keys
                      WHERE name = N'FK_zaznam_harmonogram_hodnoty_zaznam'
                        AND parent_object_id = OBJECT_ID(N'dbo.zaznam_harmonogram_hodnoty')
                        AND delete_referential_action = 1)
              THEN N'APLIKOVÁN'
         ELSE N'CHYBÍ' END,
    N'FK FK_zaznam_harmonogram_hodnoty_zaznam s ON DELETE CASCADE');

INSERT @r VALUES (290, N'db_upgrade_1_4_0_harmonogram_datum_model',
    CASE WHEN @v140 = 1 THEN N'APLIKOVÁN'
         WHEN @krok_existuje = 1 AND @hodnoty_existuje = 1
              THEN N'ČÁSTEČNĚ?! (krok i hodnoty existují — prověřit ručně)'
         ELSE N'CHYBÍ' END,
    N'tabulka zaznam_harmonogram_krok existuje + zaznam_harmonogram_hodnoty zrušena');

INSERT @r VALUES (300, N'db_upgrade_1_4_1_drop_schedule_preview',
    CASE WHEN @schedule_preview IS NULL THEN N'NELZE OVĚŘIT (authz schéma chybí)'
         WHEN @schedule_preview = 0 THEN N'APLIKOVÁN'
         ELSE N'CHYBÍ' END,
    N'absence mrtvého klíče schedule.preview v authz.permissions (nalezeno: '
        + ISNULL(CAST(@schedule_preview AS NVARCHAR(10)), N'-') + N')');

INSERT @r VALUES (310, N'db_upgrade_1_4_2_externi_odkaz_pozadavek',
    CASE WHEN @pozadavek = 1 THEN N'APLIKOVÁN' ELSE N'CHYBÍ' END,
    N'sloupec zaznam_externi_odkazy.pozadavek (text požadavku do výzvy)');

INSERT @r VALUES (320, N'db_upgrade_1_4_3_externi_odkaz_kalkulace',
    CASE WHEN @kalkulace = 1 THEN N'APLIKOVÁN' ELSE N'CHYBÍ' END,
    N'sloupce zaznam_externi_odkazy.kalkulace_cena/_id/_nacteno (snímek skutečné ceny)');

INSERT @r VALUES (330, N'db_upgrade_1_4_4_search_cleanup',
    CASE WHEN @searchCleanup = 1 THEN N'APLIKOVÁN' ELSE N'CHYBÍ' END,
    N'zrušené tabulky SearchIndex + search_reindex_checkpoint a klíč search.reindex');

INSERT @r VALUES (340, N'db_upgrade_1_4_5_record_edit_lock',
    CASE WHEN @editZamek = 1 THEN N'APLIKOVÁN' ELSE N'CHYBÍ' END,
    N'tabulka dbo.zaznam_edit_zamek (bez ní se editor neotevře nikomu — fail-soft vypne zámek)');

-- ---------------------------------------------------------------------------
-- Výstup
-- ---------------------------------------------------------------------------

-- 1) Kompletní přehled
SELECT poradi, skript, stav, detekce
FROM @r
ORDER BY poradi;

-- 2) Co spustit (chybějící skripty v pořadí nasazování)
SELECT ROW_NUMBER() OVER (ORDER BY poradi) AS krok,
       skript + N'.sql' AS spustit_v_tomto_poradi
FROM @r
WHERE stav = N'CHYBÍ'
ORDER BY poradi;

-- 3) Poznámky k interpretaci
SELECT poznamka FROM (VALUES
    (1, N'Skripty spouštěj vzestupně podle sloupce poradi — všechny jsou idempotentní (guardy IF EXISTS / COL_LENGTH), opakované spuštění nevadí.'),
    (2, N'Pokud chybí celý blok 1_1_8 až 1_3_8, lze místo 12 jednotlivých skriptů spustit db_upgrade_1_1_8_to_1_3_8_combined.sql (vyžaduje sqlcmd/SSMS kvůli GO).'),
    (3, N'Stav PŘESKOČIT = skript mění tabulky, které migrace 1.4.0 ruší (zaznam_harmonogram_hodnoty, ciselnik_harmonogram_typu). Při skoku rovnou na 1.4.0 je spouštět nemusíš; při postupném nasazování je spusť v pořadí.'),
    (4, N'db_upgrade_1_4_0 vyžaduje již existující tabulku zaznam_harmonogram_vyjadreni_vazba (z 1_3_6) a spouští se AŽ PO nasazení nové verze aplikace (Fáze 7 — smazané staré EF entity).'),
    (5, N'Před spuštěním upgrade skriptů udělej zálohu DB. 1_3_3 a 1_4_0 jsou destruktivní (mažou data harmonogramu — dle designu zahoditelná, po nasazení se znovu vytěží).'),
    (6, N'Nové authz per-action klíče (po 1_3_8) doseeduje aplikace při startu (PermissionSeeder) — skript je nevkládá.')
) AS n(id, poznamka)
ORDER BY id;
