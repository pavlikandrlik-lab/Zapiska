# ⚠️ Superseded — viz `authz-redesign-per-action-keys.md`

Tento dokument byl nahrazen kompletním redesignem permission modelu na per-action klíče:

**→ [authz-redesign-per-action-keys.md](authz-redesign-per-action-keys.md)**

Dílčí úlohy A (komentáře), B (`records.schedule.add` odstranění), C (`records.propose.*`) a D (`Schedule.Recalc`), které tento dokument obsahoval, jsou součástí nového zadání v rámci jednotného redesignu. Tento dokument se **nesmaže**, slouží jako historický záznam předchozí iterace rozhodování (2026-04-23 dopoledne → odpoledne změněno na kompletní redesign).

**Rozhodnutí změny:** Místo dílčích oprav přepsat model permissions na principu „každá mutující akce = vlastní klíč, role se skládá z klíčů". Odůvodnění v novém dokumentu, sekce „Filozofie".
