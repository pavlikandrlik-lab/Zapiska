# gov design system — stažení, offline hosting a použití

> **Stav: 🟢 návod ověřený proti reálné instalaci v Zápisce.**
> Soupis React wrapperů `pm-*` je v [../20-architektura/03-frontend-react.md](../20-architektura/03-frontend-react.md); staví je P1, P3 a P6.

## Co to je

Oficiální design systém české veřejné správy, distribuovaný jako **Web Components**
(Custom Elements postavené na Stencilu). Komponenty se používají jako běžné HTML tagy
`<gov-button>`, `<gov-message>`, `<gov-dialog>`, … a fungují v jakémkoli frameworku,
tedy i v Reactu.

## Balíčky a verze používané v Zápisce

| Balíček | Verze | Obsah |
|---|---|---|
| `@gov-design-system-ce/components` | 4.2.9 | Web Components (Stencil dist) |
| `@gov-design-system-ce/styles` | 4.2.7 | Styly a designové tokeny |

## Postup stažení (offline-first)

Aplikace běží v prostředí bez internetu, proto se knihovna **nestahuje za běhu ani přes CDN**.
Stáhne se jednou na stroji s internetem a nakopíruje do repozitáře.

```bash
# 1) Na stroji s internetem — stáhnout balíčky (bez instalace do projektu)
mkdir -p /tmp/govds && cd /tmp/govds
npm pack @gov-design-system-ce/components@4.2.9
npm pack @gov-design-system-ce/styles@4.2.7

# 2) Rozbalit
tar -xzf gov-design-system-ce-components-4.2.9.tgz     # -> package/
mv package components
tar -xzf gov-design-system-ce-styles-4.2.7.tgz
mv package styles

# 3) Zkopírovat do repozitáře aplikace
#    cíl: <web>/wwwroot/lib/gov-design-system/
cp -R components/dist/core   <web>/wwwroot/lib/gov-design-system/dist/core
cp -R styles/lib             <web>/wwwroot/lib/gov-design-system/styles/lib
```

Výsledná struktura, kterou musí repozitář obsahovat:

```
wwwroot/lib/gov-design-system/
├── dist/core/            # 91 souborů: core.esm.min.js, core.min.css, p-*.entry.js …
└── styles/lib/           # tokens.min.css, styles.css, content.css, html/…
```

### Zapojení do stránky

```html
<link rel="stylesheet" href="/lib/gov-design-system/styles/lib/tokens.min.css" />
<link rel="stylesheet" href="/lib/gov-design-system/dist/core/core.min.css" />
...
<script type="module" src="/lib/gov-design-system/dist/core/core.esm.min.js"></script>
```

> **Jen ESM.** Oficiální návod „Usage with basic HTML" zmiňuje dvojici `type="module"` +
> `nomodule`. V balíčku 4.2.9 už ale non-module varianta **neexistuje** — Stencil ji negeneruje.
> Cílem jsou moderní prohlížeče s ES moduly. Snažit se o `nomodule` fallback nemá smysl,
> zdrojový soubor není z čeho vzít.

## Ikony — pozor, nejsou součástí gov balíčku

`<gov-icon type="components">` odkazuje na SVG soubory, které **gov design system 4.x
nedistribuuje**. Ve skutečnosti jde o **Bootstrap Icons 1.11.3**, které si aplikace stahuje
sama a servíruje z `wwwroot/assets/icons/components/` (Zápiska jich má 58).

```bash
# Na stroji s internetem
npm pack bootstrap-icons@1.11.3
tar -xzf bootstrap-icons-1.11.3.tgz
# vybrané ikony zkopírovat do <web>/wwwroot/assets/icons/components/
```

Ikony se nikdy neprezentují jako „dodané gov design systemem" — je to naše doplnění.

## Upgrade na novou verzi

1. Smazat obsah `dist/core/` a nakopírovat nový.
2. Aktualizovat verze v dokumentaci (tabulka knihoven, tento návod).
3. Projít changelog gov DS a zkontrolovat atributy komponent, na které jsou navázané wrappery.
4. Spustit testy — snapshot testy wrapperů odhalí změnu generovaného HTML.
5. Projít living style guide a vizuálně ověřit.
6. Commit s prefixem `chore(gov-ds):`.

> Po upgradu vždy znovu přečíst sémantiku `p-*.entry.js` u komponent, které používáme.
> Příklad z praxe: novější verze `gov-dialog` renderuje křížek jako `disabled` při
> `block-close` — atribut se proto nesmí používat vůbec.

## Wrapper vrstva

Komponenty se nikdy nepoužívají přímo ve stránkách. Nad každou vede **vlastní wrapper `pm-*`**,
aby upgrade gov DS znamenal změnu na jednom místě. Zápiska má 28 wrapperů jako Razor
TagHelpery; Číselníky je budou mít jako React komponenty se stejným rozhraním.

Ze Zápisky se přebírá dokumentace každého wrapperu: `docs/architecture/buttons.md`,
`alerts.md`, `badges.md`, `fields.md`, `icons.md`, `selects.md`, `textareas.md`,
`checkboxes.md`, `radios.md`, `switches.md`, `links.md`, `tabs.md`, `cards.md`,
`pagination.md`, `dialogs.md`, `tooltips.md`, `toasts.md`, `skeletons.md`, `loadings.md`,
`searches.md`, `tokens.md`.

## Co se v Zápisce vědomě nepoužívá

- `gov-theme-switch` — umí jen dva stavy (světlý/tmavý), aplikace potřebuje tři
  (světlý/tmavý/podle systému) s vlastní persistencí.
- Zbytek viz `docs/specs/gov-design-system-integration.md`.

## Známé pasti

Kompletní seznam je v [20-architektura/03-frontend-react.md](../20-architektura/03-frontend-react.md).
Jsou to chyby, které v Zápisce reálně nastaly — v zadání figurují jako závazná pravidla,
ne jako doporučení.
