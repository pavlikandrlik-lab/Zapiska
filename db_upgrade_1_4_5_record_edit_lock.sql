-- =============================================================================
-- db_upgrade_1_4_5_record_edit_lock.sql
--
-- Tabulka dbo.zaznam_edit_zamek — pesimistický zámek karty záznamu.
--
-- KONTEXT (pilot 2026-09, spec docs/superpowers/specs/2026-09-17-record-edit-concurrency-design.md):
--   Dva uživatelé si mohli otevřít úpravu téhož záznamu a ten druhý svým uložením
--   tiše přepsal práci prvního. Řešením je advisory zámek: editor se druhému
--   uživateli vůbec neotevře a uvidí, kdo záznam upravuje.
--
-- SÉMANTIKA:
--   * Jeden řádek na záznam (PK = zaznam_id).
--   * Zámek je APLIKAČNÍ, ne databázový — žádný sp_getapplock, ten by nepřežil request.
--   * Vlastnictví se vyhodnocuje proti heartbeat_at: zámek starší než TTL (15 minut
--     bez projevu života) přebírá další uživatel. Žádný úklidový job neexistuje,
--     expirace se řeší při získávání zámku.
--   * Řádky mizí s mazaným záznamem (ON DELETE CASCADE) — zámek nemá bez záznamu smysl.
--
-- ROZSAH MIGRACE:
--   * Zakládá dbo.zaznam_edit_zamek (pokud chybí). Nic nemigruje, nic nemaže.
--
-- SPUŠTĚNÍ: pod účtem s db_owner na cílové databázi.
--   sqlcmd -S <SQL_HOST>\<INSTANCE> -E -d PM_Tracker -b -i db_upgrade_1_4_5_record_edit_lock.sql
--
-- Idempotence: skript lze spustit opakovaně.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

PRINT N'[1.4.5] zaznam_edit_zamek — start';

IF OBJECT_ID(N'dbo.zaznam_edit_zamek', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.zaznam_edit_zamek
    (
        zaznam_id    INT           NOT NULL,
        osoba_id     INT           NOT NULL,
        ziskano_at   DATETIME2(3)  NOT NULL,
        heartbeat_at DATETIME2(3)  NOT NULL,
        CONSTRAINT PK_zaznam_edit_zamek PRIMARY KEY CLUSTERED (zaznam_id),
        CONSTRAINT FK_zaznam_edit_zamek_zaznam FOREIGN KEY (zaznam_id)
            REFERENCES dbo.projektove_zaznamy (id) ON DELETE CASCADE,
        CONSTRAINT FK_zaznam_edit_zamek_osoba FOREIGN KEY (osoba_id)
            REFERENCES dbo.osoby (id)
    );

    PRINT N'[1.4.5] tabulka dbo.zaznam_edit_zamek vytvořena';
END
ELSE
BEGIN
    PRINT N'[1.4.5] tabulka dbo.zaznam_edit_zamek už existuje — přeskočeno';
END

PRINT N'[1.4.5] hotovo';
