# Drobečková lišta v aplikačním framu — návrh

- **Datum:** 2026-07-04
- **Stav:** Návrh k implementaci (fáze 1)
- **Branch:** codex/senior-refactor-fase-1

## Kontext a cíl

Dnes má každá stránka vlastní `page-header` s tlačítkem „Zpět" a titulkem (partial `_PageHeader.cshtml`
nebo inline — např. `Projekty/Detail.cshtml` ř. 20–34: zpět `|` název + pill „Zkratka:" + stav). Zabírá
to svislé místo a navigace „nahoru" je nekonzistentní.

Cíl: **pevná drobečková lišta v aplikačním framu**, hned pod hlavním menu, která nahradí tyto per-page
hlavičky sjednocenou navigací zanoření. Inspirace GINIS / smartfp.cz. Šetří místo, sjednocuje „zpět".

## Zamčená rozhodnutí (z brainstormingu)

1. **Zdroj cesty = serverová logická hierarchie.** Každá stránka zná své předky a přispěje je; server
   je vykreslí. Přežije refresh, deep-link i zpět/vpřed v prohlížeči.
2. **Kanonické vlastnictví (jeden trail na entitu).** Jednání i záznam **vždy** kořenují pod svým
   projektem: `Projekty › ProjektXY › Jednání 12`, bez ohledu na to, odkud uživatel přišel. Top-level
   seznam Jednání je vlastní kořen jen pro ten seznam.
3. **Kořeny = sekce menu** (landing stránky): `Projekty`, `Jednání`, `Přehled`, `Osoby`, `Číselníky`,
   `Nastavení`. Kořen nemá ← ani ✕.
4. **✕ na každém entitním drobečku → rodič.** Klik na ✕ u drobečku `i` zavře tu oblast i vše pod ní a
   přejde na rodiče. Kořen sekce ✕ nemá.
5. **Šipka ← = o úroveň výš** (rodič aktuální stránky = stejné jako ✕ na posledním drobečku).
6. **Lišta = jen navigace.** Obsahuje ← + drobečky (`název | zkratka`) + ✕. Stav a akce zůstávají u
   záložek/obsahu.
7. **Styling:** gov-design-system jako základ/inspirace (přístupné `ol/li` + `aria-current`), doplnit
   z jiných prvků kde gov nestačí. Postaveno **nativně, bez třetí-strany závislosti** (žádný React/Vue
   ani komerční suite jako Smart UI / Syncfusion — ty řeší jen widget, ne doménový model cesty).

## Datový model

```csharp
public sealed record Breadcrumb(
    string Text,            // hlavní popisek (název entity / sekce)
    string? Url,            // cíl odkazu; null = aktuální (aria-current, není odkaz)
    string? MutedSuffix,    // ztlumený dovětek, renderuje se " | ZKR"
    bool IsClosable);       // true = entita (má ✕), false = kořen sekce

public sealed record BreadcrumbTrail(IReadOnlyList<Breadcrumb> Items);
```

- **Plnění:** helper `BaseController.SetBreadcrumbs(params Breadcrumb[] items)` uloží trail do
  `ViewData["Breadcrumbs"]` — stejný vzor jako stávající `ViewData["NavPermissions"]`.
- **Vykreslení:** `_Layout.cshtml` přečte `ViewData["Breadcrumbs"]` a vloží partial
  `_BreadcrumbBar.cshtml` hned pod `<nav class="app-nav">`. Když trail chybí → lišta se nevykreslí
  (opt-in rollout, viz Rozsah).
- **Rodič = předchozí drobeček.** Žádné zvláštní „close URL":
  - `✕` na drobečku `i` → odkaz na `Items[i-1].Url`.
  - `←` → `Items[Count-2].Url` (rodič aktuálního). Skryté, když je jen kořen.
- **Vše jsou `<a href>` (GET).** Základní navigace bez JS.

### Příklad — editor záznamu otevřený z jednání
```
Projekty            Url=/Projekty                IsClosable=false
Projekt Alfa        Url=/Projekty/Detail/7  Suffix=ALF  IsClosable=true   ✕→/Projekty
Jednání 12/2026     Url=/Jednani/Detail/33            IsClosable=true      ✕→/Projekty/Detail/7
Záznam #1042        Url=null (aria-current)           IsClosable=true      ✕→/Jednani/Detail/33
```
`←` = `/Jednani/Detail/33`.

## UI lišty

- **Pozice:** sticky pruh těsně pod menu, součást pevné hlavičky (drží se při scrollu obsahu).
  Kompaktní jeden řádek, gov tokeny (barvy/mezery/typografie), light + dark.
- **Struktura:** `<nav aria-label="Drobečková navigace"> <ol> <li>… </ol> </nav>`; aktuální `<li>`
  má `aria-current="page"` a není odkaz.
- **Prvky:** `←` ikon-tlačítko (odkaz) · drobečky oddělené `›` · u entitních drobečků `✕` ikon-odkaz
  vpravo od textu (`aria-label="Zavřít <název>"`). Ikony = Bootstrap Icons (`arrow-left`, `x`) —
  konzistentní se zbytkem appky.
- **`název | zkratka`:** `Text` = název, `MutedSuffix` = zkratka renderovaná jako ztlumené ` | ZKR`.
- **Klik na TEXT** = přejít na tu úroveň. **Klik na ✕** = zavřít → rodič.
- **Top-level seznam** (např. Projekty/Index): trail jen kořen (`Projekty`), bez ← a bez ✕.
- **Stav projektu** (badge) se přesune vlevo do řádku záložek (na `Projekty/Detail`); samostatný
  page-header mizí.
- **Přetečení:** dlouhý název ořízne `text-overflow: ellipsis` (+ `title`); na úzké šířce lišta
  zkolabuje na `← + aktuální drobeček`. Řešeno CSS (flex + `min-width:0`), bez JS.
- **A11y:** landmark `nav`, `ol/li`, `aria-current`, popisky ✕; ← i ✕ jsou fokusovatelné odkazy.

## Chování

- `←`, `✕`, text = obyčejné `<a href>` → GET navigace. **Očekává se nula nového JS** (i přetečení je
  CSS-only).
- **Dirty-check (editor):** na page-level editoru záznamu s neuloženými změnami zachytí tyto odkazy
  stávající `maybeGuardOutboundNavigation` v `bootstrap.js` (komentář ř. 389–398 už „breadcrumbs"
  jmenuje) → app-level potvrzovací dialog. Bez úprav JS.
- **Modálová varianta editoru:** lišta patří spodní stránce; modal ji nemění.

## Rozsah — fáze 1 (opt-in)

Lišta se vykreslí jen tam, kde akce zavolá `SetBreadcrumbs(...)`. Ostatní sekce zůstanou beze změny
(žádné dvojité hlavičky) až do fáze 2.

| Stránka | Trail | Změna hlavičky |
|---|---|---|
| `Projekty/Index` | `Projekty` (kořen) | beze změny obsahu |
| `Projekty/Detail` | `Projekty › Nazev \| ZKR` | smazat page-header (ř. 20–34); stav → řádek záložek |
| `Jednani/Detail` | `Projekty › Nazev \| ZKR › Jednání <label>` | nahradit `_PageHeader` |
| `Zaznamy` Edit/Create (full-page `EditZaznamPage`) | `Projekty › Nazev \| ZKR › (Jednání <label> ›)? Záznam #id / „Nový záznam"` dle `uiContext`/`meetingId` | page-header „Zpět" pryč; Save/Zrušit lišta editoru zůstává |

Poznámky k implementaci:
- `JednaniDetailViewModel` a editor VM musí vystavit `ProjektId` + název + zkratku (doplnit, pokud
  chybí) a u editoru meeting label pro mezidrobeček.
- Label jednání = existující zobrazovací popisek (číslo/datum). Aktuální drobeček záznamu = `Záznam #<id>`
  (edit) / `Nový záznam` (create).
- Záložky projektu (harmonogram/jednani/tym/navrhy) **nepřidávají** drobeček — jsou uvnitř projektu,
  aktuální drobeček zůstává projekt.

### Fáze 2 (mimo tento spec)
Rollout lišty na `Jednání` (seznam), `Přehled`, `Osoby`, `Číselníky`, `Nastavení`, `Dokumentace` —
už mechanicky podle stejného modelu. Každá sekce vlastní plán.

## Testy

- **Unit:** `BreadcrumbTrail`/builder — rodič = předchozí; ✕ url = předchozí; kořen bez ✕; aktuální bez
  Url.
- **Api-render** (WebApplicationFactory + `?asUser=`): GET `Projekty/Detail`, `Jednani/Detail`,
  `Zaznamy/Edit`, `Zaznamy/Create` → ověřit vykreslené drobečky, `href`, ✕ cíle, `aria-current`,
  `název | zkratka`.
- **E2E** (Playwright, dev/SQL): ✕ na projektu → seznam Projekty; ← na jednání → projekt; ✕ na
  editoru s dirty formulářem → objeví se close-guard (ne tichá navigace).
- **Unit CSS-invariant** (volitelně): lišta má sticky pozici pod menu (`File.ReadAllText` site.css).

## Non-goals / YAGNI

- Žádný klientský zásobník otevřených objektů (jen serverová kanonická cesta).
- Žádné akce/stav v liště (jen navigace).
- Žádná třetí-strany komponenta.
- Fáze 2 sekce nejsou součástí této implementace.
