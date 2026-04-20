# Fallback číselník výzev — poznámka

**Status:** fáze 2 UI hotová — primární správa výzev probíhá přes **projektový dashboard → záložka Výzvy**.

**Fallback číselník** pro SuperAdmin / app_admin opravy chyb:
- Používá existující generický `CiselnikyController` + `DictionaryService` infrastrukturu.
- Zakládá/edituje `VyzvaEntity` s placeholder hodnotami (ProjektId, snapshots).
- Plné přepracování fallback číselníku (např. interaktivní formulář s volbou projektu) je mimo scope fáze 2 — možné rozšíření ve fázi 4.

**Pro běžné uživatele:** Nepoužívat. Použít projektový dashboard.
