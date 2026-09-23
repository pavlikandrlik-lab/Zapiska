# Auditní log

> **Stav: 🟢 mapa harvestu hotová; auditní log staví P9.**

## Co modul v Zápisce dělá

- Zaznamenává změny nad sledovanými entitami do append-only historie.
- Uchovává „snapshot" stavu, aby šlo dohledat, jak záznam vypadal v čase.
- Slouží jako podklad pro obrazovku efektivních práv i pro dohledání, kdo co změnil.

## Zdroje k harvestu ze Zápisky

| Soubor | Obsah |
|---|---|
| `PmTracker.Web/Services/Audit/AuditModels.cs` | Tvar auditního záznamu |
| `PmTracker.Web/Services/Audit/AuditSnapshots.cs` | Snapshoty stavu entit |
| `PmTracker.Tests.Unit/Audit/` | Testy |
| `db_upgrade_1_3_1_history_and_audit_indexes.sql` | Indexy nad auditními tabulkami |

## Poučení, které musí být v zadání explicitně

Auditní tabulky jsou **append-only a mají do sebe cizí klíče**. Ukládací tok proto nesmí
používat vzor „smaž vše a založ znovu" — padá na porušení referenční integrity.
Správný vzor je UPSERT podle identifikátoru plus kontrola vazeb před smazáním
a srozumitelná chyba místo výjimky z databáze.
