# Upgrade gov-design-system

Aplikace používá DS gov.cz lokálně v `PmTracker.Web/wwwroot/assets/gov/` jako **1:1 kopii
předsestaveného kitu** `DesignSystem-FIS-v1.0.0/assets/gov` (bez `icons/`). Soubory DS se
nikdy needitují (README kitu, pravidlo 1); odchylky jsou v `docs/known-issues/ds-fis-odchylky.md`.

Aktuální verze: `@gov-design-system-ce/{styles,templates,components,fonts}` **4.7.0**.

## Postup upgrade na novou verzi

### 1. Nahradit kopii DS

Sestav novou verzi kitu (`node build.mjs` v kitu) a přepiš složky
`components/`, `styles/`, `fonts/`, `templates/` v `wwwroot/assets/gov/` (celé, ne po souborech —
staré `p-*.js` smazat). `icons/` ani `ds-fis/` se nekopírují.

> **Pozn. pro non-module prohlížeče**
> Oficiální návod pro "Usage with basic HTML" na `designsystem.gov.cz` zmiňuje
> dvojici `<script type="module" src="core.esm.js">` + `<script nomodule src="core.js">`.
> V npm packagu `@gov-design-system-ce/components` je však **jen ESM varianta** —
> non-module `core.js` Stencil v novějších verzích negeneruje. Doporučení v dokumentaci
> je zastaralé (platilo pro starší verze gov DS). Cílíme proto výhradně na moderní
> prohlížeče s ES modules (Chrome 61+, Firefox 60+, Safari 11+, Edge 16+).
> Nemá smysl se snažit o `nomodule` fallback — zdrojový soubor neexistuje.

### 2. Ikony

Nové ikony sady kitu (`icons/components/`) doplň do `wwwroot/assets/icons/components/`,
**existující nepřepisuj**. `GovAssets470Tests.AplikacniIkony_JsouNadmnozinouKitu` obsahuje
seznam sady — aktualizuj ho.

### 3. Aktualizovat verze

- tento soubor, `docs/specs/offline-deployment.md` (tabulka + ověřovací příkazy),
- řádek verze v patičce `_Layout.cshtml` („DS gov.cz X.Y.Z") a `LayoutGovFooterRenderTests`.

### 4. Prověřit breaking changes

Otevři `CHANGELOG.md` gov DS (https://github.com/gov-design-system-ce/...).

Pro `pm-*` TagHelpery stačí zkontrolovat:
- **gov-button** atributy (color, type, size) — pokud se změní enum, upravit `PmButtonTagHelper`
- **gov-message** (variant/color) — upravit `PmAlertTagHelper`
- **gov-tag** — `PmBadgeTagHelper`
- **gov-form-control/input/message** — `PmFieldTagHelper`

Jeden zdroj pravdy pro každý mapping → jeden soubor k revizi.

### 5. Spustit testy

```
dotnet test PmTracker.Tests.Unit
dotnet test PmTracker.Tests.Api
dotnet test PmTracker.Tests.E2E
```

Vizuální regression: snapshot testy TagHelperu odhalí změnu generovaného HTML.

### 6. Ověřit `/StyleGuide`

Otevři stránku, projdi všechny sekce. Vizuál musí být stále konzistentní.

### 7. Commit

Message prefix `chore(gov-ds):` + popis změny verzí a důvod upgradu.
