# 30 — Převzaté moduly ze Zápisky

Moduly ověřené provozem Zápisky, které se do zadání aplikace Číselníky vloží
**jako kompletní kód**, ne jako popis. Cílem je, aby se už jednou vyřešené věci
nevymýšlely a neprogramovaly znovu.

| Modul | Co se přebírá | Stav |
|---|---|---|
| [01-autentizace-ad.md](01-autentizace-ad.md) | Přihlášení uživatele, mapování na AD, atributy z AD, synchronizace | 🟢 |
| [02-autorizace-rbac.md](02-autorizace-rbac.md) | Seed-only RBAC: klíče, role, scope, snapshot, policy handler | 🟢 |
| [03-auditni-log.md](03-auditni-log.md) | Auditní záznamy změn, historizace | 🟢 |
| [04-dokumentace-a-wiki.md](04-dokumentace-a-wiki.md) | Wiki knihovna renderovaná v aplikaci, known issues, changelog | 🟢 |
| [05-gov-design-system.md](05-gov-design-system.md) | Stažení, offline hosting, wrappery, ikony, tokeny | 🟢 |
| [06-testovaci-infrastruktura.md](06-testovaci-infrastruktura.md) | Fixtures, testovací autentizace, Playwright setup, skripty | 🟢 |
| [07-jak-se-prebira-kod.md](07-jak-se-prebira-kod.md) | **Kde vede šev mezi Razorem a Reactem** — změřeno, modul po modulu | 🟢 |

## Pravidlo pro tuto sekci

Každý dokument obsahuje:
1. **Co modul dělá** — stručně, aby vývojář věděl, co dostal.
2. **Kompletní zdrojový kód** — vložený, ne odkazovaný.
3. **Co je nutné upravit** pro Číselníky (jiná databáze, jiný frontend, jiná doména).
4. **Testy, které modul drží** — také jako kompletní kód.

> Vkládání kódu proběhne až po zodpovězení architektonických otázek. Kód psaný pro
> SQL Server a Razor by se do zadání vkládat neměl, dokud není jasné, co přesně
> se mění při přechodu na SQL Server a React.
