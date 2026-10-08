-- =============================================================================
-- db_reset_prosty_text_hledani.sql
--
-- Údržbový skript (NENÍ to migrace) — vynuluje čistý text pro hledání, aby ho příští start
-- aplikace dopočetl znovu z aktuálního HTML (uživatel 2026-10-08, review I1).
--
-- KDY SPUSTIT:
--   * Po běhu binárek verze <= 1.4.6 proti DB, která už má db_upgrade_1_4_7 a běžela na ní
--     nová aplikace — starý kód dál upravuje popis/text_vyjadreni/pozadavek, ale neumí
--     vynulovat jejich čistý text, takže zůstane zastaralý (nenajde nová slova, najde stará).
--   * Po jakémkoliv SQL zásahu (budoucí db_upgrade_*.sql podobný 1_4_6, ruční oprava v SSMS),
--     který změnil popis, text_vyjadreni nebo pozadavek bez vynulování odpovídajícího
--     *_prosty_text sloupce ve stejném příkazu.
--
-- INVARIANT (viz i db_upgrade_1_4_7_prosty_text_hledani.sql a
-- docs/technical/06-database-bootstrap-migrations.md, část 5.7):
--   Každý SQL příkaz, který mění popis, text_vyjadreni nebo pozadavek, musí ve stejném
--   příkazu nastavit odpovídající *_prosty_text na NULL — čistý text pak dopočte další
--   start aplikace. Tento skript je záchranná síť pro případy, kdy se to nestalo.
--
-- CO SKRIPT DĚLÁ: vynuluje popis_prosty_text, text_vyjadreni_prosty_text a
-- pozadavek_prosty_text na NULL u všech řádků, kde nejsou už NULL. Samotné HTML
-- (popis/text_vyjadreni/pozadavek) nemění. PO SPUŠTĚNÍ RESTARTUJ APLIKACI —
-- RichTextSearchTextBackfillHostedService dopočte čistý text znovu pro všechny řádky.
--
-- Idempotence: skript lze spustit opakovaně (druhý běh nahlásí 0 ovlivněných řádků).
-- NENÍ migrace — nepatří do číslovaného seznamu v 06-database-bootstrap-migrations.md
-- ani do db_check_applied_upgrades.sql.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

PRINT N'[reset] Vynulování čistého textu pro hledání — start';

IF COL_LENGTH(N'dbo.projektove_zaznamy', N'popis_prosty_text') IS NULL
   OR COL_LENGTH(N'dbo.vyjadreni', N'text_vyjadreni_prosty_text') IS NULL
   OR COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'pozadavek_prosty_text') IS NULL
BEGIN
    RAISERROR(N'Chybí sloupce čistého textu pro hledání — nejdřív spusť db_upgrade_1_4_7_prosty_text_hledani.sql.', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;

UPDATE dbo.projektove_zaznamy SET popis_prosty_text = NULL WHERE popis_prosty_text IS NOT NULL;
PRINT N'[reset] dbo.projektove_zaznamy.popis_prosty_text vynulován u ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' řádků';

UPDATE dbo.vyjadreni SET text_vyjadreni_prosty_text = NULL WHERE text_vyjadreni_prosty_text IS NOT NULL;
PRINT N'[reset] dbo.vyjadreni.text_vyjadreni_prosty_text vynulován u ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' řádků';

UPDATE dbo.zaznam_externi_odkazy SET pozadavek_prosty_text = NULL WHERE pozadavek_prosty_text IS NOT NULL;
PRINT N'[reset] dbo.zaznam_externi_odkazy.pozadavek_prosty_text vynulován u ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' řádků';

COMMIT TRANSACTION;

PRINT N'[reset] hotovo — restartuj aplikaci, další start čistý text dopočte';
