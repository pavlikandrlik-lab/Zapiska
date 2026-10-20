# Přehled — nadpisy bloků

**Stav:** návrh k odsouhlasení (2026-10-20)
**Stránka:** osobní Přehled `/Dashboard` (`Views/Dashboard/Index.cshtml`). Projektový dashboard (`/ProjectDashboard`, přestavba podle [2026-10-15](2026-10-15-dashboard-prestavba-prodleni-statistiky-design.md)) se netýká.

---

## 1. Cíl

Každý ze tří bloků Přehledu dostane krátký nadpis, aby bylo na první pohled jasné, co blok obsahuje. Nadpis smí zabrat co nejméně místa: na kartách záznamů se nesmí nic ztratit.

## 2. Výchozí stav

- Přehled má tři bloky: levý sloupec se záznamy, vpravo jednání a pod nimi novinky. Ani jeden nemá nadpis.
- Nadpisy (nadpis, podnadpis, počet) byly odstraněny 2026-06-08 kvůli úspoře místa. V `site.css` po nich zůstaly jen komentáře `.dashboard-panel-header → odstraněno 2026-06-08`. Spolu s nimi zmizel i nadpis stránky `.dashboard-shell-header` (h1), takže stránka dnes nemá žádný nadpis.
- Detailní stránky bloků už názvy mají (`DashboardService`, `PageTitle`): **Na co se soustředit**, **Nejbližší jednání**, **Co je nového**.
- Obsah bloků se načítá líně (`data-dashboard-panel-url`). Do té doby blok ukazuje skeleton.

## 3. Rozhodnutí (odsouhlaseno v konverzaci 2026-10-20)

| Otázka | Rozhodnutí |
|---|---|
| Podoba hlavičky | **Jen nadpis.** Bez počtu položek, bez podnadpisu, nadpis není odkaz. |
| Texty | Stejné jako titulky detailních stránek: „Na co se soustředit“, „Nejbližší jednání“, „Co je nového“. |
| Velikost | Kompaktní, viz §4.3. Ověřeno měřením: počet viditelných karet se nemění. |

## 4. Návrh

### 4.1 Jeden zdroj textů

Nová statická třída `DashboardBlockTitles` (konstanty `Focus`, `Meetings`, `News`) v `Services/Dashboard/`. Používá ji:
- `DashboardService`: `PageTitle` detailních stránek Focus, Meetings a News (dnes jsou tam řetězce natvrdo),
- `Views/Dashboard/Index.cshtml`: nadpisy bloků.

Texty se tak nemůžou rozejít mezi nadpisem bloku, tabem prohlížeče a drobečkovou navigací.

### 4.2 Markup

Nadpis je v kostře stránky (`Index.cshtml`), **ne** v líně načítaném partialu. Je tedy vidět hned, i během načítání a i když se obsah bloku nenačte.

```html
<section class="dashboard-panel dashboard-section--focus"
         data-dashboard-panel="focus"
         data-dashboard-panel-url="…"
         aria-labelledby="dashboard-focus-heading">
    <h2 class="dashboard-panel-heading" id="dashboard-focus-heading">Na co se soustředit</h2>
    <div class="dashboard-panel-placeholder" data-dashboard-panel-placeholder>…</div>
    <div data-dashboard-panel-content></div>
</section>
```

Stejně pro jednání (`dashboard-meetings-heading`) a novinky (`dashboard-news-heading`).

Hierarchie nadpisů: na začátek `dashboard-shell` přibude `<h1 class="app-sr-only">Přehled</h1>`, který je vidět jen pro čtečky obrazovky. Bez něj by stránka měla `h2` bez `h1`. Třída `app-sr-only` v aplikaci zatím není, proto se do `site.css` přidá standardní vzor (`position:absolute; width:1px; height:1px; overflow:hidden; clip:rect(0 0 0 0); white-space:nowrap`).

### 4.3 Vzhled

```css
.dashboard-panel-heading {
    flex: 0 0 auto;
    margin: 0;
    padding: 0.5rem var(--d-pad-card) 0.25rem;
    font-size: 0.875rem;
    line-height: 1.25rem;
    font-weight: 700;
    color: var(--text-primary);
}
```

- Výška řádku nadpisu je **32 px** (pevně 0,875 rem písma, ne `clamp`, takže se nemění s šířkou okna).
- Blok je flex sloupec s `overflow: hidden`. Nadpis je pevný (`flex: 0 0 auto`), obsah pod ním si bere zbytek a seznam dál roluje uvnitř bloku. Nadpis při rolování zůstává nahoře.
- Barva jen z gov tokenu (`--text-primary`), tmavý motiv se přepne sám. Žádné `:has`, `max-content` ani intrinsic klíčová slova (Edge na i15).
- Dělení pravého sloupce se nemění: jednání podle obsahu se stropem 50 %, novinky zbytek. Prázdný blok ukáže nadpis a hlášku (např. „Nejsou naplánovaná žádná blízká jednání.“).
- Úzký displej (blokové skládání pod sebe): nadpis je nad každým blokem, bez dalších změn.

### 4.4 Ověřené místo na karty

Měřeno na lokální dev DB 2026-10-20 (nadpis vložený do stránky v prohlížeči, kód beze změny):

| Okno | Bez nadpisu | S nadpisem 0,875 rem | Varianta 1 rem (zamítnuta) |
|---|---|---|---|
| 1400×900 | 5 celých karet, seznam 681 px | **5 karet**, 649 px (nadpis 32 px) | 5 karet, 641 px (nadpis 40 px) |
| 1920×1080 | 6 celých karet, seznam 851 px | **6 karet**, 819 px | 6 karet, 811 px |

Náhled: [assets/2026-10-20-prehled-nadpisy-nahled.png](assets/2026-10-20-prehled-nadpisy-nahled.png)

## 5. Testy

- **Api render test** (`PmTracker.Tests.Api`, nový `DashboardBlockHeadingsRenderTests`), kotvený na atributy, ne na český text:
  - `/Dashboard` obsahuje tři `h2` s id `dashboard-focus-heading`, `dashboard-meetings-heading` a `dashboard-news-heading` a každý blok má odpovídající `aria-labelledby`,
  - text nadpisu = `HtmlEncoder.Default.Encode(DashboardBlockTitles.X)` (Razor kóduje diakritiku),
  - `/Dashboard` obsahuje skrytý `h1` se třídou `app-sr-only`.
- **Unit** (`DashboardService`): `PageTitle` detailních stránek = `DashboardBlockTitles.*`.
- **Regrese:** stávající E2E testy layoutu Přehledu (rail, jednání neořezaná, rolování seznamů) zůstanou zelené. Porovnává se proti baseline jmen, ne počtem.
- **Ruční průchod (Playwright, lokální app):**
  - 1400×900 a 1920×1080: stejný počet celých karet jako před změnou (§4.4),
  - tmavý motiv, šířka 375 px: nadpisy čitelné, bez vodorovného posunu,
  - stav během načítání: nadpis je vidět nad skeletonem,
  - prázdná jednání: nadpis a hláška, novinky dostanou zbytek výšky.
- Po dokončení zápis do `docs/k-otestovani.md`.

## 6. Mimo rozsah

- Počty položek v hlavičce, podnadpisy, nadpis jako odkaz (zamítnuto v §3).
- Detailní stránky (`/Dashboard/Focus`, `/Meetings`, `/News`): mají titulek a drobečkovou navigaci, beze změny.
- Projektový dashboard.
- Úklid nepoužívané třídy `sr-only` v `modals.js` (chybový titulek modalu nemá CSS pravidlo). Jde o samostatnou drobnost, nová třída `app-sr-only` ji neřeší.

## 7. Dotčené soubory

| Soubor | Změna |
|---|---|
| `PmTracker.Web/Services/Dashboard/DashboardBlockTitles.cs` | nový, konstanty textů |
| `PmTracker.Web/Services/Dashboard/DashboardService.cs` | `PageTitle` z konstant |
| `PmTracker.Web/Views/Dashboard/Index.cshtml` | `h1` (skrytý), tři `h2`, `aria-labelledby` |
| `PmTracker.Web/wwwroot/css/site.css` | `.dashboard-panel-heading`, `.app-sr-only`, úprava komentáře o odstranění hlaviček |
| `PmTracker.Tests.Api/Controllers/DashboardBlockHeadingsRenderTests.cs` | nový render test |
| `PmTracker.Tests.Unit/...` | test `PageTitle` (do existujícího testu `DashboardService`, pokud existuje) |
| `docs/k-otestovani.md` | nová sekce |
