# Globální vyhledávání — specifikace

## Kontext

Globální vyhledávání je umístěno v hlavičce aplikace (`_Layout.cshtml`). Umožňuje uživatelům
rychle najít záznamy a vyjádření across projektů. Implementace je dvojí:

1. **DB Suggest** (`IDbSuggestService`) — přímý LIKE search v SQL, funguje vždy bez závislosti
   na OpenSearch/SearchOptions.Enabled. Výsledky se zobrazují v dropdownu pod polem.
2. **OpenSearch/FullText** (`IGlobalSearchService`) — fulltextové vyhledávání na stránce
   `/Search/Index`, aktivní pouze pokud `PmTracker:Search:Enabled = true`.

## UI struktura

### Markup (`_Layout.cshtml` řádky 46–83)

Vyhledávací pole využívá **skutečné Web Components** z `@gov-design-system-ce/components`
hostované **LOKÁLNĚ** v `PmTracker.Web/wwwroot/lib/gov-design-system/` (aplikace MUSÍ fungovat
offline — viz `docs/specs/offline-deployment.md`). Vlastní HTML divy simulující gov-form-search
jsou **zakázány** — používejte pouze custom elementy.

```html
<form data-global-search role="search" asp-controller="Search" asp-action="Index" class="app-search">
  <gov-form-control size="m" class="gov-form-control" type="input">
    <div class="gov-form-control__holder">
      <gov-form-group>
        <gov-form-search color="primary" size="m">
          <gov-form-input slot="input" size="m" name="q" placeholder="Hledání">
            <span class="element">
              <input type="text"
                     id="app-global-search-input"
                     name="q"
                     placeholder="Hledání"
                     aria-label="Globální vyhledávání"
                     autocomplete="off" />
            </span>
          </gov-form-input>
          <gov-button slot="button-erase" size="s" color="primary" type="base">
            <gov-icon slot="icon-start" name="x" type="components"></gov-icon>
          </gov-button>
          <gov-button slot="button" color="primary" size="s" type="solid">
            Hledat
          </gov-button>
        </gov-form-search>
      </gov-form-group>
    </div>
  </gov-form-control>
  <div data-global-search-dropdown class="app-search-dropdown" hidden
       id="global-search-listbox" role="listbox"></div>
</form>
```

### Lokální hosting (offline-first)

Aplikace běží v prostředí **bez přístupu na internet**, proto jsou VŠECHNY gov-design-system
soubory uloženy lokálně ve složce `PmTracker.Web/wwwroot/lib/gov-design-system/`. Použití CDN
(`cdn.jsdelivr.net` apod.) je **zakázáno** — viz `docs/specs/offline-deployment.md`.

| Typ        | Lokální cesta (pod `wwwroot/`)                                            | Verze  |
|------------|---------------------------------------------------------------------------|--------|
| CSS tokeny | `~/lib/gov-design-system/styles/lib/tokens.min.css`                       | 4.2.7  |
| CSS komp.  | `~/lib/gov-design-system/dist/core/core.min.css`                          | 4.2.9  |
| JS loader  | `~/lib/gov-design-system/dist/core/core.esm.min.js`                       | 4.2.9  |
| JS chunks  | `~/lib/gov-design-system/dist/core/p-*.js` (89 souborů, ~1.4 MB)          | 4.2.9  |

**Pozor:** `core.esm.min.js` je pouze loader — dynamicky načítá ~88 chunked JS souborů (`p-*.js`)
jako relativní `import()`y. Musí být všechny ve stejné složce `/dist/core/`, jinak se komponenty
nezaregistrují a tagy `<gov-form-search>` zůstanou jako inert HTML.

### Aktualizace verze gov-design-system

1. Stáhni nové soubory z jsdelivr podle vzoru (viz `docs/specs/offline-deployment.md`).
2. Uprav `Layout_MaLocalniGovAssety_VRepozitari` test pokud se změní struktura.
3. Ověř, že `.gitignore` **nevylučuje** složku `PmTracker.Web/wwwroot/lib/gov-design-system/`.

V Razor `.cshtml` se `@` musí escapovat jako `@@` (Razor syntaxe).

### JS selektor pro input

`global-search.js` hledá input přes `form[data-global-search] input[name="q"]`. Input je v light DOM
uvnitř `<span class="element">` slotovaném do `<gov-form-input>` — selektor funguje bez změny.
Submit zachycuje event `submit` na formu — `<gov-button slot="button" type="solid">` emituje klik,
který spouští form submit standardním způsobem.

Erase button (`slot="button-erase"`) má vestavěné chování Web Component — nemaže ho JS.

## Data flow

```
Uživatel píše (≥2 znaky)
  → JS debounce 300ms
  → AbortController zruší předchozí fetch
  → GET /Search/Suggest?q={q}  (Accept: application/json)
  → SearchController.Suggest
  → IDbSuggestService.SuggestAsync(q, currentUser, limit=10)
      → EF Core LIKE %q% na ProjektoveZaznamy (Nazev, Cil, Popis)
      → EF Core LIKE %q% na Vyjadreni (TextVyjadreni) přes JOIN Jednani → Projekt
      → ACL filtr: VisibleProjectIds nebo IsSuperAdmin=true = vše
  → JSON: { hits: [{ type, title, snippet, url, projektNazev }] }
  → JS renderHits → dlaždice v dropdownu
```

## Rozsah vyhledávání

| Entita                    | Prohledávaná pole                   | Entity property          |
|---------------------------|-------------------------------------|--------------------------|
| `ProjektovyZaznamEntity`  | Název záznamu                        | `Nazev`                  |
| `ProjektovyZaznamEntity`  | Cíl záznamu                          | `Cil` (nullable)         |
| `ProjektovyZaznamEntity`  | Popis záznamu                        | `Popis` (nullable)       |
| `VyjadreniEntity`         | Text vyjádření                       | `TextVyjadreni`          |

Záznamy v dropdownu: max 10 celkem (split zhruba 50/50 záznamy vs. vyjádření).

## ACL pravidla

- **SuperAdmin** (`IsSuperAdmin = true`): vidí záznamy ze všech projektů.
- **Ostatní uživatelé**: vidí pouze záznamy projektů v `VisibleProjectIds`
  (naplněno při přihlášení z `ObsazeniProjektu`).
- ACL je vynucen přes EF Core WHERE klauzuli (ne post-filter) — výsledky jsou bezpečné.

## Klávesové zkratky

| Klávesa    | Akce                                              |
|------------|---------------------------------------------------|
| `Escape`   | Zavře dropdown                                    |
| `ArrowDown`| Přesune fokus na další dlaždici (cyklicky)        |
| `ArrowUp`  | Přesune fokus na předchozí dlaždici (cyklicky)    |
| `Enter`    | Naviguje na URL aktivní dlaždice                  |

## Dark mode

Dark mode pro `gov-form-search`, `gov-form-input` a `gov-button` řídí **gov-design-system** pomocí
vlastních CSS tokenů. Vlastní overrides pro `gov-*` selektory v `site.css` jsou **zakázány**
— konflikují s Web Component stylesheetem.

`site.css` obsahuje dark mode override pouze pro naše vlastní komponenty:
- `.app-search-dropdown` (dropdown s výsledky — není gov komponenta)
- `.global-search-item__badge--zaznam` a `--vyjadreni` (badge výsledků)

## Pravidla pro CSS

1. **Nikdy** nepřidávejte vlastní CSS pro `gov-*` selektory — Web Components si styl řídí samy.
2. Pro layoutové overrides (width, flex) používejte nadřazený selektor `.app-search gov-form-control`.
3. Vlastní dark overrides patří pouze na `.app-search-dropdown` a `.global-search-item*`.

## Pokrytí testy

### Unit testy (`PmTracker.Tests.Unit/Search/`)

| Soubor                               | Co testuje                                                          |
|--------------------------------------|---------------------------------------------------------------------|
| `GlobalSearchMarkupTests.cs`         | Web Component tagy, sloty, CDN URLs, aria, erase button            |
| `GlobalSearchDarkModeCssTests.cs`    | Absence gov-* dark overrides; přítomnost dropdown a badge overrides |
| `SuggestEndpointExistsTests.cs`      | Reflection: SearchController.Suggest existuje + sig.               |

### Integration/API testy (`PmTracker.Tests.Api/Controllers/`)

| Soubor                               | Co testuje                                           |
|--------------------------------------|------------------------------------------------------|
| `SuggestEndpointTests.cs`            | HTTP GET /Search/Suggest — 200, prázdné pole, hit    |

### E2E testy (`PmTracker.Tests.E2E/Scenarios/`)

| Soubor                                      | Co testuje                               |
|---------------------------------------------|------------------------------------------|
| `GlobalSearchDynamicResultsTests.cs`        | Zobrazení dropdownu, Escape, klik mimo   |

## Synchronizace bundle

`global-search.js` je **standalone skript** (ne součást `site.bundle.js`). Načítá se bezpodmínečně
přes `<script src="~/js/global-search.js">` v `_Layout.cshtml`.

## Rizika

| Riziko                    | Dopad                                  | Mitigace                                                          |
|---------------------------|----------------------------------------|-------------------------------------------------------------------|
| CDN nedostupná            | Search field se nezobrazí správně      | Přidat npm/node a bundlovat lokálně, nebo SRI integrity hash      |
| CORS / CSP blokuje CDN    | Komponenty se nenačtou                 | Přidat cdn.jsdelivr.net do Content-Security-Policy                |
| Upgrade verze CDN         | Breaking change v komponentách         | Pinovat přesnou verzi (4.2.9 / 4.2.7), testovat před upgradem    |
| Shadow DOM a JS selectory | input[name="q"] nemusí být viditelný  | Input je v light DOM (slotovaný) — selektor funguje               |

## Pravidla pro úpravy

1. Pokud měníte `global-search.js`: soubor je standalone — bundle se nemění.
2. Pokud přidáváte nové entity do vyhledávání: upravte `DbSuggestService.cs`.
3. ACL filtr (VisibleProjectIds) musí být vždy aplikován v EF Core dotazu, ne post-filter.
4. Debounce je 300ms, AbortController ruší předchozí fetch — neměňte bez regresního testu.
5. Pokud CDN přestane být dostupná v produkci: nainstalujte npm/node a bundlujte lokálně.
