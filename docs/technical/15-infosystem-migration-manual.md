# Manuální migrace projektů na Informační systém (IS) — postup

> **Kontext:** Plán 5 Sprint B (2026-04-24) zavádí vazbu projekt → IS
> (FIS / ISSP) jako hardkódovaný katalog (`SdInfoSystemy`) + sloupec
> `projekty.servicedesk_info_system_id`. Tento dokument popisuje, jak
> admin ručně přiřadí existujícím projektům správný IS, aby se na
> projektovém dashboardu (panel **NES v prodlení**) začaly zobrazovat
> tickety.

## Předpoklady

1. DB migrace `db_upgrade_1_3_11_projekty_infosystem.sql` je nasazena
   (viz `docs/technical/06-database-bootstrap-migrations.md`).
2. Nasazen build obsahující Sprint B (Plán 5) — dropdown IS v edit
   modalu projektu.
3. Admin má globální oprávnění `projects.edit` (nebo je SUPERADMIN /
   APP_ADMIN — viz `docs/technical/07-security-authz.md`).

## Výchozí stav po migraci

Všechny existující projekty mají `servicedesk_info_system_id = NULL`
(bez napojení). Projektový dashboard → panel **NES v prodlení** zobrazí
pro takové projekty zprávu:

> Projekt není napojen na žádný Informační systém v ServiceDesku.
> Nastavte napojení na IS …

## Postup přes UI (doporučené)

1. **Přehled projektů:** otevři `/Projekty`.
2. U každého projektu klikni na akci **Upravit** → otevře se edit modal.
3. V modalu najdi pole **Informační systém (ServiceDesk)**:
   - **— bez napojení —** (výchozí) → panel NES zůstane vypnutý.
   - **FIS — Finanční informační systém** → projekty s doménou financí
     (účetnictví, rozpočet, mzdy, fakturace, VZ 8201, …).
   - **ISSP — Informační systém služebních poměrů** → projekty s doménou
     personalistiky, služebních poměrů, HR.
4. Ulož změnu. Audit trail (`ProjectAuditSnapshot`) zachytí změnu
   `ServiceDeskInfoSystemId`.

## Heuristika pro mapování

| Indikátor v názvu / zkratce projektu | Doporučený IS |
|---|---|
| FIS, UCT, MZD, VZ, SAP, fakturace, rozpočet, účetnictví | **FIS** |
| ISSP, HR, služební poměry, personalistika, SP, OKS | **ISSP** |
| Interní / bez přímé vazby na SD tickety | **— bez napojení —** |

## Batch SQL migrace (volitelné, opatrně)

> **Varování:** heuristiku vždy ověř na malém vzorku před hromadným
> updatem. SQL nezohledňuje audit trail — změny nebudou zaznamenány
> do `audit_log`. Preferuj UI přístup, kde je změna auditovaná.

```sql
-- Orientační — uprav WHERE dle skutečné datové situace.
-- Testuj nejdřív: SELECT id, zkratka, cely_nazev FROM dbo.projekty WHERE ...

UPDATE dbo.projekty
   SET servicedesk_info_system_id = 1  -- FIS
 WHERE (zkratka LIKE '%FIS%' OR cely_nazev LIKE '%FIS%' OR cely_nazev LIKE '%fakturac%')
   AND servicedesk_info_system_id IS NULL;

UPDATE dbo.projekty
   SET servicedesk_info_system_id = 2  -- ISSP
 WHERE (zkratka LIKE '%ISSP%' OR cely_nazev LIKE '%služeb%' OR cely_nazev LIKE '%personal%')
   AND servicedesk_info_system_id IS NULL;

-- Sanity po updatu
SELECT servicedesk_info_system_id AS is_id,
       COUNT(*) AS pocet_projektu
  FROM dbo.projekty
 GROUP BY servicedesk_info_system_id
 ORDER BY servicedesk_info_system_id;
```

## Ověření ID v HOT_IS před deployem

Katalog `SdInfoSystemy` používá `FisId = 1` a `IsspId = 2`. Pokud
`intranetNEW.dbo.HOT_IS.ID` má v produkci jiné hodnoty, je nutné
konstanty upravit v `PmTracker.Web/Services/ServiceDesk/SdInfoSystemy.cs`
před nasazením. Ověření:

```sql
SELECT ID, zkratka, nazev
  FROM intranetNEW.dbo.HOT_IS
 WHERE zkratka IN ('FIS', 'ISSP')
 ORDER BY ID;
```

Pokud produkční ID neodpovídají výchozím konstantám, uprav zdroj a znovu
přestav binárku — jedná se o `const int`, takže refaktor je triviální.

## Související dokumentace

- `docs/technical/12-servicedesk-schema-reference.md` — schéma HOT_IS /
  HOT_MODULY / HOT_ZAZNAMY.
- `docs/technical/13-servicedesk-connection-setup.md` — připojení k
  ServiceDesk DB (read-only).
- `docs/specs/2026-04-16-projektovy-dashboard-design.md` §98–129, §217–244
  — design panelu NES.
- Memory: `project_servicedesk_infosystem_binding.md` — terminologie
  projekt ↔ IS (1:0..1).
