# Specifikace — vyjádření (komentáře) v záznamu (record-comments)

Dokumentuje chování panelu „Vyjádření" v detailu projektového záznamu.

---

## Kontext

Každý projektový záznam může mít více **vyjádření** (komentářů). Vyjádření
vznikají nejčastěji v rámci jednání; každé vyjádření má autora, datum a
(volitelně) vazbu na jednání (číslo + datum).

Panel je implementován v [PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml](../../PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml).
JS chování v [PmTracker.Web/wwwroot/js/modules/comments.js](../../PmTracker.Web/wwwroot/js/modules/comments.js).
CSS v [PmTracker.Web/wwwroot/css/site.css](../../PmTracker.Web/wwwroot/css/site.css) (selektory `.record-comments-*`, `.comment-*`).

---

## Řazení a stránkování

Panel zobrazuje **5 vyjádření** jako stránku (`LoadStep`). Uživatel může:

1. Přepnout **směr řazení** (tlačítko `comment-sort-toggle`, texty „Řazení:
   jednání vzestupně" / „sestupně"). Směr se ukládá do localStorage
   (`pmtracker.comments.sortDirection`).
2. **Načíst další** vyjádření po 5 (tlačítko s atributem
   `data-record-comments-load-more`).
3. **Zobrazit všechny** vyjádření (tlačítko s atributem
   `data-record-comments-load-all`).

### Poloha a texty pagination tlačítek podle směru

| Směr řazení | Pozice sort toggle | Pozice pagination tlačítek | Text load-more | Text load-all |
| --- | --- | --- | --- | --- |
| **DESC** (nejnovější nahoře, starší dole) | vpravo v headeru | **POD listem**, vpravo | **„Další (5)"** | **„Zobrazit vše"** |
| **ASC** (nejstarší nahoře, nejnovější dole) | vpravo v headeru | **POD sort toggle** v headeru, vpravo (sloupcově) | **„Zobrazit předchozí (5)"** | **„Zobrazit vše"** |

Obě varianty obsahují v závorce **počet záznamů, které se při kliknutí dají načíst** (`LoadStep`, default 5), aby měl uživatel jasno, kolik jich přibude.

### Důvod

- **DESC**: další načtené záznamy přibydou **dole** (další stránka = starší).
  Tlačítka jsou fyzicky tam, kde vznikají nové řádky.
- **ASC**: další načtené záznamy přibydou **nahoře** (starší jsou nahoře).
  Tlačítka by na tom konci měla být → jsou v headeru nad listem. Zároveň
  v ASC nazýváme další načtení „předchozí vyjádření" (starší vůči
  nejnovějším, která jsou dole).

### DOM struktura

V obou směrech má root section třídu `record-comments` s atributem
`data-comment-sort-section` a `data-comment-sort-direction="asc"|"desc"`.

Header:
```html
<div class="record-comments-header [record-comments-header--stacked-right]">
    <h4>Vyjádření</h4>
    <button class="comment-sort-toggle" data-comment-sort-toggle>…</button>
    <!-- V ASC JS sem přesune .comment-pagination-actions -->
</div>
```

- Třída `record-comments-header--stacked-right` se přidává JS v ASC režimu.
- V ASC jde pagination pod sort toggle (flex-basis: 100%; margin-top: 6px).

Seznam:
```html
<div class="comment-list" data-comment-list>
    <div class="comment" data-comment-item>…</div>
    …
</div>
```

Pagination (jen jeden blok v DOM, JS ho přesouvá):
```html
<div class="comment-pagination-actions [comment-pagination-actions--in-header]">
    <button data-record-comments-load-more
            data-label-desc="Další (5)"
            data-label-asc="Zobrazit předchozí (5)">
        Další (5)
    </button>
    <button data-record-comments-load-all
            data-label-desc="Zobrazit vše"
            data-label-asc="Zobrazit vše">
        Zobrazit vše
    </button>
</div>
```

Atributy `data-label-desc` a `data-label-asc` řídí text tlačítka podle
aktuálního směru. JS v `applyCommentSort` přepne textContent.

---

## Zarovnání

Pagination tlačítka jsou **vždy zarovnaná vpravo** (`justify-content: flex-end`).
V ASC režimu má blok `flex-basis: 100%` a `margin-left: auto`, aby obešel
sort toggle a zobrazil se POD ním plnou šířkou s obsahem zarovnaným vpravo.

---

## Layout jednotlivého komentáře

Každý komentář (`.comment`) má dva sloupce:

```html
<div class="comment">
    <div class="comment-header">
        <div class="comment-text richtext-render">… obsah …</div>
        <div class="comment-metadata">
            <div class="comment-info">
                <strong class="comment-author">Jan Novák</strong>
                <div class="comment-subline">
                    <span>15.04.2026</span>
                    <span class="comment-meeting-chip">· Jednání č. 14 (12.04.2026)</span>
                </div>
            </div>
            <!-- Pouze pro autora / editora: -->
            <div class="comment-actions-inline">…edit + delete…</div>
        </div>
    </div>
</div>
```

- Text vpravo posazené metadata (autor, datum, jednání, ikony akcí).
- Avatar byl odebrán v commitu 7270f1e (zjednodušení layoutu).

---

## Pokrytí testy

- Unit: [PmTracker.Tests.Unit/Projects/CommentPaginationMarkupTests.cs](../../PmTracker.Tests.Unit/Projects/CommentPaginationMarkupTests.cs) —
  ověřuje `data-label-desc` / `data-label-asc` atributy, třídy, existenci CSS pravidel.

---

## Pravidla pro úpravy

1. **Při změně textů tlačítek** aktualizuj `data-label-desc` / `data-label-asc`
   ve view a taky tabulku v této specifikaci.
2. **Nikdy nedělej vlastní text přes `innerText` nebo `{ text: … }`** v kódu
   pro pagination tlačítka — vždy jen přes dat-label atributy, aby se respektoval směr.
3. **Nové pagination tlačítko** musí mít oba `data-label-*` atributy a musí
   respektovat `.comment-pagination-actions--in-header` třídu pro ASC layout.
4. **Synchronizace bundle**: pokud měníš logiku `comments.js`, uprav **i**
   `PmTracker.Web/wwwroot/js/site.bundle.js`. Aplikace v prohlížeči načítá
   pouze bundle (`_Layout.cshtml` má jen `<script src="~/js/site.bundle.js">`),
   moduly slouží jen pro vývoj. Pokud bundle obsahuje zastaralou funkci
   `applyCommentSort` bez přesunu pagination a přepínání textů, pozorované
   chování je „tlačítka zůstávají dole a text se nemění" — i když modul je v pořádku.
