# PM Tracker - Technická dokumentace 06: Databáze, bootstrap a migrace

## 1. Účel
Dokument popisuje standardní databázový lifecycle: inicializaci baseline, aplikaci upgrade patchů, validaci konzistence a provozní rollback strategii.

## 2. Publikum a role
- DB administrátor: spouštění SQL skriptů, backup/restore, patching.
- Ops administrátor: koordinace deployment pořadí app vs DB.
- Vývojář: aktualizace baseline a upgrade skriptů.

## 3. Závislosti a předpoklady
- SQL Server instance dostupná z deployment hostu.
- Účet s oprávněním na CREATE/ALTER v cílové DB.
- Před upgrade existuje poslední ověřená backup DB.

## 4. Vstupy a výstupy
### Vstupy
- SQL skripty v rootu repozitáře.
- SQL klient (`sqlcmd` nebo SSMS).

### Výstupy
- Databáze ve validním schema + seed stavu odpovídajícím release.

## 5. Detailní postup
### 5.1 Pořadí spuštění skriptů
1. `PMTracker_insert_sql`
2. `db_upgrade_0_4_membership_subsystems.sql`
3. `db_upgrade_1_1_0_signed_schedule_actual.sql`
4. `db_upgrade_1_1_1_external_link_estimated_price.sql`
5. `db_upgrade_1_1_2_project_subsystem_order.sql`
6. `db_upgrade_1_1_3_record_proposals.sql`
7. `db_upgrade_1_1_4_record_priority_matrix.sql`
8. `db_upgrade_1_1_5_search_checkpoint.sql`
9. `db_upgrade_1_1_6_project_roles_manager_gestor.sql`
10. `db_upgrade_1_1_7_new_task_status.sql`
11. `db_upgrade_1_1_8_vyzvy.sql`
12. `db_upgrade_1_2_0_authz_role_scope.sql`
13. `db_upgrade_1_2_1_lookup_role_authz_fk.sql`
14. `db_upgrade_1_3_0_cleanup_orphaned_role_permissions.sql`
15. `db_upgrade_1_3_1_history_and_audit_indexes.sql`
16. `db_upgrade_1_3_2_authz_join_indexes.sql`
17. `db_upgrade_1_3_3_fakturace_cleanup.sql`
18. `db_upgrade_1_3_4_ad_sync_settings.sql`
19. `db_upgrade_1_3_5_external_link_harvested_at.sql`
20. `db_upgrade_1_3_6_vyjadreni_vazba.sql`
21. `db_upgrade_1_3_7_sd_sync_settings_and_fingerprint.sql`
22. `db_upgrade_1_3_8_authz_per_action_redesign.sql`
23. `db_upgrade_1_3_10_harmonogram_skutecnost_zdroj.sql`
24. `db_upgrade_1_3_11_projekty_infosystem.sql`
25. `db_upgrade_1_3_12_record_delete_cascade.sql`
26. `db_upgrade_1_3_13_proposal_supersede.sql`
27. `db_upgrade_1_3_14_delay_nullable.sql`
28. `db_upgrade_1_3_15_harmonogram_indexes.sql`
29. `db_upgrade_1_3_16_fix_harmonogram_hodnoty_cascade.sql`
30. `db_seed_dev_admin.sql` (pouze neprodukční prostředí)

### 5.2 Fresh install
```powershell
$SQL_INSTANCE = "<SQL_SERVER>"
$DB_NAME = "PmTracker"
$SQL_DIR = "C:\deploy\pmtracker\sql"

sqlcmd -S $SQL_INSTANCE -E -Q "IF DB_ID('$DB_NAME') IS NULL CREATE DATABASE [$DB_NAME];"
sqlcmd -S $SQL_INSTANCE -E -d $DB_NAME -b -i "$SQL_DIR\PMTracker_insert_sql"
```

### 5.3 Upgrade existující DB
```powershell
sqlcmd -S $SQL_INSTANCE -E -d $DB_NAME -b -i "$SQL_DIR\db_upgrade_1_1_0_signed_schedule_actual.sql"
sqlcmd -S $SQL_INSTANCE -E -d $DB_NAME -b -i "$SQL_DIR\db_upgrade_1_1_1_external_link_estimated_price.sql"
```

### 5.4 Povinné baseline kontroly
```sql
SELECT kod FROM dbo.ciselnik_stavu_projektu WHERE kod IN ('PLAN','RUN','DONE','DELETED');
SELECT kod FROM dbo.ciselnik_roli_projektu WHERE kod IN ('VLASTNIK_PROJEKTU','HOST','ADM_PROJ','PROJ_MAN');
SELECT kod FROM dbo.ciselnik_roli_subsystemu WHERE kod IN ('VEDOUCI_SUBSYSTEMU','ZASTUPCE_VEDOUCIHO_SUBSYSTEMU','METODIK_SUBSYSTEMU');
SELECT id, kod FROM dbo.ciselnik_organizace WHERE kod='MO';
SELECT COUNT(*) AS osoby_count FROM dbo.osoby;
SELECT COUNT(*) AS superadmins_count FROM authz.superadmins;
```

### 5.5 První superadmin
- Produkční baseline nevkládá automaticky uživatele ani superadmina.
- Nejprve vlož záznam do `dbo.osoby` s validním `Guid_AD`.
- Následně vlož `osoba_id` do `authz.superadmins`.
- AD není automaticky synchronizováno, proto je první založení osoby vždy ruční provozní krok.

### 5.6 Minimální business tabulky pro provozní baseline
- `dbo.ciselnik_kategorii_zaznamu`
- `dbo.ciselnik_stavu_ukolu`
- `dbo.ciselnik_typu_ukolu`
- `dbo.subsystemy`
- `dbo.ciselnik_stavu_jednani`
- `dbo.ciselnik_stavu_ucasti`
- `dbo.harmonogram_sablony`
- `dbo.ciselnik_harmonogram_typu`
- `dbo.ciselnik_typu_externich_odkazu`
- `dbo.vyzvy`
- `dbo.vyzva_historie_stavu`
- `dbo.ciselnik_organizace`
- `dbo.ciselnik_organizacni_celky`

## 6. Verifikace
- SQL skripty skončí `exit code 0` bez `RAISERROR`.
- Povinné číselníky a authz tabulky existují.
- Aplikace po DB změně startuje a obslouží `/Projekty` bez runtime chyby.

## 7. Rollback
- Před změnou vždy provést full backup DB.
- Pokud patch selže a není idempotentně opravitelný, proveď restore poslední ověřené backup.
- Po restore znovu ověř baseline query a spustitelnost aplikace.

## 8. Troubleshooting
- Chyba na unique constraint při bootstrapu:
  - cílová DB není čistá; použij fresh DB nebo migrační postup.
- Chybí authz tabulky:
  - ověř, že byl spuštěn správný baseline skript.
- Negativní hodnoty harmonogramu selhávají:
  - ověř, že je aplikován patch `db_upgrade_1_1_0_signed_schedule_actual.sql`.

## 9. Audit a traceability
- Baseline script: `/Users/Pavel.Andrlik/Documents/PM Tracker/PMTracker_insert_sql`
- Upgrade scripts: `/Users/Pavel.Andrlik/Documents/PM Tracker/db_upgrade_1_1_0_signed_schedule_actual.sql`, `/Users/Pavel.Andrlik/Documents/PM Tracker/db_upgrade_1_1_1_external_link_estimated_price.sql`
- Dev seed script: `/Users/Pavel.Andrlik/Documents/PM Tracker/db_seed_dev_admin.sql`
- Test bootstrap reader: `/Users/Pavel.Andrlik/Documents/PM Tracker/tests/Common/RepositoryPaths.cs`
