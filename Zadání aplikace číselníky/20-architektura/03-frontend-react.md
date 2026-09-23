# Frontend — React

Jediná úmyslná odchylka od Zápisky. Razor šablony se v Zápisce po delším vývoji ukázaly
jako nevhodné — nová aplikace staví frontend v Reactu.

## Co se přebírá i přes změnu technologie

- **Pravidlo offline-first** — žádné CDN, žádné externí fonty, vše lokálně.
- **gov design system web komponenty** — jsou to Custom Elements, v Reactu použitelné přímo.
  Viz [30-prevzate-moduly/05-gov-design-system.md](../30-prevzate-moduly/05-gov-design-system.md).
- **Wrapper vrstva `pm-*`** — jeden wrapper = jeden bod změny při upgradu gov DS.
  V Zápisce to jsou Razor TagHelpery, v Reactu to budou React komponenty se stejným
  rozhraním a stejnými variantami.
- **Designové tokeny** — CSS proměnné jako jediný zdroj pravdy pro barvy a odsazení,
  včetně light/dark/auto režimu.

## Známé pasti gov komponent (platí i v Reactu — jsou to Custom Elements)

Tyto chyby už jednou stály čas v Zápisce. Do zadání se přenášejí jako závazná pravidla:

- `instanceof HTMLButtonElement` **nematchuje** `gov-button`.
- Nastavení `textContent` na hostu `gov-button` rozbije slot relocation a zdvojí popisek.
- CSS `:checked` **nematchuje** custom elementy — pro `gov-form-switch` se píše `[checked]`.
- Atributy `aria-*` a `hidden` na hostu wrapperu se neuplatní — stav se řídí třídou
  na obyčejném předkovi.
- `gov-dialog` s atributem `block-close` má **disablovaný křížek** — nikdy nepoužívat.
- Zavírání plovoucích vrstev se vyhodnocuje na `mousedown`, ne na `click`.
- Modály se zavírají **jen křížkem** — klik na backdrop zavírat nesmí.
- V Playwrightu je host gov komponenty „not visible" — klikat přes dispatch události,
  čekat na třídu `hydrated`, viditelnost ověřovat počtem prvků, ne `ToBeVisible`.

## Zdroje k harvestu ze Zápisky

- `docs/architecture/` — README + 20 dokumentů komponent (buttons, fields, dialogs,
  tabs, cards, toasts, tooltips, …), každý popisuje varianty a API wrapperu
- `docs/architecture/tokens.md` — designové tokeny
- `docs/architecture/js-modules.md` — členění JS a event bus
- `PmTracker.Web/TagHelpers/` — 28 wrapperů, referenční mapování na gov komponenty
- `PmTracker.Web/Views/StyleGuide/` — living style guide, ukazuje každou komponentu
  ve všech variantách; v nové aplikaci ekvivalent (Storybook nebo vlastní stránka — F3)


---

## Sestava

| Vrstva | Volba |
|---|---|
| Sestavení | **Vite** |
| Jazyk | **TypeScript** |
| Směrování | **React Router** |
| Serverová data | Dotazovací knihovna s mezipamětí (TanStack Query) |
| Stav v prohlížeči | Vlastní stav komponent. **Žádná globální knihovna stavu** — aplikace je čtení a formuláře, ne sdílený živý stav. |
| Jazyk rozhraní | Jen čeština, texty přímo v komponentách. Žádná překladová vrstva (F2). |

## Členění zdrojů

```
ciselniky-web/
├── src/
│   ├── stranky/          jedna složka na obrazovku (O1–O13)
│   ├── komponenty/
│   │   ├── pm/           wrappery nad gov web komponentami
│   │   └── …             vlastní složené komponenty
│   ├── api/              volání /internal/…, typy odpovědí
│   ├── lib/              pomocné funkce bez vazby na UI
│   └── styly/            tokeny a globální styly
└── vite.config.ts
```

## Směrování

| Cesta | Obrazovka |
|---|---|
| `/` | O1 Seznam číselníků |
| `/ciselnik/:kod` | O2 Detail — záložky hodnoty / struktura / verze / rozdíl |
| `/ciselnik/:kod/editace` | O5 Hromadná editace *(vyžaduje zámek)* |
| `/ciselnik/:kod/import` | O6 Import JSON |
| `/ciselnik/:kod/struktura` | O7 Definice struktury |
| `/hledani` | O3 Výsledky vyhledávání |
| `/nastaveni/role`, `/nastaveni/prava`, `/nastaveni/audit` | O8–O10 |
| `/profil` | O11 |
| `/dokumentace/*` | O12 |
| `/styleguide` | O13 |

## Komponenty `pm-*`

Nad každou použitou gov komponentou vede **vlastní wrapper** — jeden bod změny při upgradu
gov design systemu. Zápiska má 28 wrapperů jako Razor TagHelpery; tady jich stačí méně,
protože aplikace je menší.

Potřebné pro etapu 1: `pm-button` · `pm-field` · `pm-select` · `pm-textarea` · `pm-checkbox`
· `pm-switch` · `pm-icon` · `pm-alert` · `pm-badge` · `pm-card` · `pm-dialog` · `pm-tabs`
· `pm-pagination` · `pm-search` · `pm-link` · `pm-loading` · `pm-toast` · `pm-tooltip`

Dokumentaci každého z nich přebíráme ze Zápisky (`docs/architecture/*.md`) — popisuje
varianty a rozhraní, které se má zachovat.

## Vlastní komponenty, které gov nedodává

| Komponenta | Poznámka |
|---|---|
| **Tabulka hromadné editace** | Nejsložitější kus aplikace. Editace v buňkách, zvýraznění přidaných / změněných / vyřazených řádků, výběr cílové položky u vazeb. |
| **Strom hierarchie** | Rozbalování, přesun položky pod jinou |
| **Seznam změn** | Přehled rozpracovaných změn před uložením |
| **Rozdíl verzí** | Dvě verze vedle sebe |

Nosné rozvržení stojí na vlastním CSS gridu — gov design system nemá pro tento typ obrazovek
vhodnou strukturu. **Všechny ovládací prvky uvnitř jsou ale gov komponenty.**
Tentýž přístup se osvědčil v Zápisce.

## Jak React mluví s backendem

**React běží v prohlížeči a s databází nemluví nikdy.** Umí jedinou věc — poslat HTTP
požadavek. Všechen přístup k datům i všechna autorizace jsou na serveru.

```
prohlížeč ── HTTP ──▶ IIS ──▶ ASP.NET Core ──▶ služba ──▶ SQL Server
   React            Windows      middleware
   fetch()           Auth        naplní identitu
```

### Proč Windows Authentication funguje i pro SPA

Protože je to **jeden nasazovací artefakt** (rozhodnutí A2): prohlížečová aplikace
i rozhraní jsou na **téže adrese**. Volání `fetch('/internal/…')` je proto obyčejný
požadavek na tentýž původ, IIS ho autentizuje stejně jako kterýkoli jiný, a middleware
z něj naplní identitu osoby. Pro server není rozdíl mezi požadavkem z React aplikace
a požadavkem z adresního řádku.

`fetch` posílá přihlašovací údaje u požadavků na tentýž původ **sám** — výchozí hodnota
`credentials` je `same-origin`. Přesto se uvádí výslovně, aby bylo v kódu vidět,
že se s tím počítá:

```ts
const odpoved = await fetch(`/internal/ciselniky/${kod}`, { credentials: 'same-origin' })
```

### Vývojový režim: jediný původ i tady

> **Tohle je past, na kterou se dá snadno narazit.** Vývojový server Vite běží na vlastním
> portu. Kdyby prohlížeč volal backend přímo na jiný port, byl by to **cizí původ** —
> a k cizímu původu prohlížeč vyjednávání o Windows přihlášení sám neposílá.
> Vypadalo by to jako rozbitá autorizace, přitom by šlo o původ.

Řešení je proxy ve vývojovém serveru, aby prohlížeč viděl **jediný původ**:

```ts
// vite.config.ts
export default defineConfig({
  server: {
    proxy: {
      '/internal': { target: 'http://localhost:5080', changeOrigin: false },
      '/api':      { target: 'http://localhost:5080', changeOrigin: false },
      '/zdravi':   { target: 'http://localhost:5080', changeOrigin: false },
    },
  },
})
```

`changeOrigin: false` je podstatné — hlavička `Host` musí zůstat původní,
jinak se vyjednávání o přihlášení rozejde s tím, na co je server nastavený.

V produkci žádná proxy není: backend servíruje hotový build i rozhraní z jedné adresy.

### Autorizace se v prohlížeči nekontroluje

Frontend si při načtení vyžádá `GET /internal/ja` a dostane **seznam svých efektivních
práv** včetně datového rozsahu. Používá ho k tomu, aby nenabízel, co server odmítne.

> **Není to kontrola oprávnění, je to nápověda pro rozhraní.** Server kontroluje vždy
> znovu, při každém požadavku. Kdyby se spoléhalo na prohlížeč, stačilo by ke zvýšení
> práv otevřít vývojářské nástroje.

## Sestavení a nasazení

```bash
# na stroji s internetem
npm ci                      # nikdy `npm install` — zamčené verze
npm run build               # → dist/

# výstup se kopíruje do Ciselniky.Api/wwwroot/ a jde do publikovaného balíku
```

**Do produkce jde hotový výstup sestavení, nikdy `node_modules`** (rozhodnutí A4).

### Kde je Node potřeba a kde ne

| Kde | Co tam musí být | Node |
|---|---|---|
| **Klientská stanice** | jen prohlížeč | **ne** |
| **Server (IIS)** | .NET hosting bundle | **ne** |
| **Vývojářský a build stroj** | .NET SDK + Node | **ano** |

Sestavením vzniknou **statické soubory** — HTML, JS, CSS. Kopírují se do `wwwroot`
a IIS je servíruje stejně jako obrázky. **Server o Node vůbec neví.**

Stanice ho nepotřebuje z principu: Node je serverový běhový modul pro JavaScript,
prohlížeč má vlastní.

> **Past:** cíl `BuildSpa` v `csproj` spouští `npm ci` před publikováním. Publish se
> proto dělá **na build stroji, nikdy na serveru** — tam by spadl na chybějícím npm.
> Na server jde hotová složka.

> Offline pravidlo se tím neporušuje. **Týká se běhu, ne sestavení.** V uzavřené síti
> nikdy nic nevolá ven; build proběhne jinde a dovnitř se nese jen výsledek.
Aplikace se nasazuje jako **jeden artefakt** — backend servíruje rozhraní i SPA.

## Přístupnost

**Úroveň 1, nic navíc** (rozhodnutí N2). Komponenty gov design systemu si nesou to,
co dodává jejich autor; vlastní části se v tomto ohledu neřeší.
