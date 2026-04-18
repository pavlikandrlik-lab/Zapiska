# Upgrade gov-design-system

Aplikace používá gov-design-system lokálně hostované v `PmTracker.Web/wwwroot/lib/gov-design-system/`.

Aktuální verze:
- `@gov-design-system-ce/components` **4.2.9**
- `@gov-design-system-ce/styles` **4.2.7**

## Postup upgrade na novou verzi

### 1. Stáhnout nové soubory

V adresáři `PmTracker.Web/wwwroot/lib/gov-design-system/dist/core/` smazat staré soubory
a stáhnout všechny `p-*.js` + `core.esm.min.js` + `core.min.css` (viz `docs/specs/offline-deployment.md`).

### 2. Aktualizovat verze v kódu

- `docs/specs/offline-deployment.md` — tabulka aktuálních knihoven
- `docs/architecture/upgrade-gov-ds.md` — tento soubor
- `docs/specs/global-search.md` — verzní tabulka

### 3. Prověřit breaking changes

Otevři `CHANGELOG.md` gov DS (https://github.com/gov-design-system-ce/...).

Pro `pm-*` TagHelpery stačí zkontrolovat:
- **gov-button** atributy (color, type, size) — pokud se změní enum, upravit `PmButtonTagHelper`
- **gov-message** (variant/color) — upravit `PmAlertTagHelper`
- **gov-tag** — `PmBadgeTagHelper`
- **gov-form-control/input/message** — `PmFieldTagHelper`

Jeden zdroj pravdy pro každý mapping → jeden soubor k revizi.

### 4. Spustit testy

```
dotnet test PmTracker.Tests.Unit
dotnet test PmTracker.Tests.E2E
```

Vizuální regression: snapshot testy TagHelperu odhalí změnu generovaného HTML.

### 5. Ověřit `/StyleGuide`

Otevři stránku, projdi všechny sekce. Vizuál musí být stále konzistentní.

### 6. Commit

Message prefix `chore(gov-ds):` + popis změny verzí a důvod upgradu.
