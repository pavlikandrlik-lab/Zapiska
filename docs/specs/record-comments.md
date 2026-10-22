# Specifikace — vyjádření (komentáře) v záznamu (record-comments)

Dokumentuje chování panelu „Vyjádření" v detailu projektového záznamu
(karta v záložce Záznamy, detail jednání, stránka záznamu — jeden sdílený partial).

Poslední změna: spec [2026-10-22 vyjádření v jednom kompaktním bloku](../superpowers/specs/2026-10-22-vyjadreni-kompaktni-blok-design.md)
(blok, bublina s autorem a datem, ikony při najetí; R10 počet, R11 poloha stránkování).

---

## Kontext

Každý projektový záznam může mít více **vyjádření** (komentářů). Vyjádření
vznikají v rámci jednání; každé vyjádření má autora, datum a vazbu na jednání
(číslo + datum).

Panel je implementován v [PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml](../../PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml).
JS chování v [PmTracker.Web/wwwroot/js/modules/comments.js](../../PmTracker.Web/wwwroot/js/modules/comments.js).
CSS v [PmTracker.Web/wwwroot/css/site.css](../../PmTracker.Web/wwwroot/css/site.css) (selektory `.record-comments-*`, `.record-comments .comment*`).
Počet načtených vyjádření řídí `ProjectService.LazyQueries.cs` (`RecordCommentsInitialCount`, `RecordCommentsLoadStep`).

---

## Řazení a stránkování

Panel otevře **posledních 10 vyjádření** (`RecordCommentsInitialCount`). Uživatel může:

1. Přepnout **směr řazení** (tlačítko `comment-sort-toggle`, texty „Řazení:
   jednání vzestupně" / „sestupně"). Směr se ukládá do localStorage
   (`pmtracker.comments.sortDirection`).
2. **Načíst další** vyjádření po 5 (`LoadStep`, tlačítko s atributem
   `data-record-comments-load-more`; žádá `limit = načtené + 5`).
3. **Zobrazit všechna** vyjádření (tlačítko s atributem
   `data-record-comments-load-all`; žádá `loadAll=true`).

Po načtení všech tlačítka zmizí. Obnovení panelu po uložení / smazání drží
počet načtených (`recordRefresh.js` posílá `limit: loadedCount`).

### Poloha a texty tlačítek stránkování

| Směr řazení | Poloha | Text load-more | Text load-all |
| --- | --- | --- | --- |
| **DESC** (nejnovější nahoře) | v hlavičce **vlevo od řazení** | **„Další (5)"** | **„Zobrazit vše"** |
| **ASC** (nejstarší nahoře) | v hlavičce **vlevo od řazení** | **„Předchozí (5)"** | **„Zobrazit vše"** |

Poloha je v obou směrech stejná (uživatel 2026-10-22, R11); dřív JS blok
přesouval pod seznam (DESC) nebo pod řazení (ASC). V závorce je počet, který
přibude (`LoadStep`).

### DOM struktura

Root má třídu `record-comments`, atributy `data-comment-sort-section` a
`data-comment-sort-direction="asc"|"desc"`.

Hlavička:
```html
<div class="record-comments-header">
    <h4>Vyjádření</h4>
    <div class="record-comments-tools">                 <!-- jen když je co ukázat -->
        <div class="comment-pagination-actions">        <!-- jen když jsou další -->
            <pm-button data-record-comments-load-more>
                <span data-comment-pagination-label="desc">Další (5)</span>
                <span data-comment-pagination-label="asc">Předchozí (5)</span>
            </pm-button>
            <pm-button data-record-comments-load-all>Zobrazit vše</pm-button>
        </div>
        <pm-button class="comment-sort-toggle" data-comment-sort-toggle>…</pm-button>  <!-- 2+ vyjádření -->
    </div>
</div>
```

Text tlačítka podle směru přepíná **CSS** (`[data-comment-sort-direction]
[data-comment-pagination-label]`), ne JS — `textContent` na gov-button
duplikoval obsah.

---

## Vzhled seznamu (spec 2026-10-22)

```html
<div class="comment-list" data-comment-list>              <!-- jeden rámeček celého bloku -->
    <div class="comment" data-comment-item data-comment-id data-comment-meeting data-comment-date>
        <div class="comment-side">                        <!-- float vpravo -->
            <div class="comment-row-actions" data-comment-row-actions>tužka, koš</div>  <!-- jen kdo smí, ne u uzavřeného jednání -->
            <span class="chip-wrap">
                <span class="comment-meeting-chip" tabindex="0" aria-describedby="vyjadreni-meta-{Id}">Jednání č. N (datum)</span>
                <span id="vyjadreni-meta-{Id}" class="chip-tooltip chip-tooltip--end" role="tooltip" data-comment-meta>autor, Zapsáno datum</span>
            </span>
        </div>
        <div class="comment-text richtext-render" data-comment-text>…</div>
        <form class="comment-edit-form" data-comment-edit-form hidden>…</form>
    </div>
</div>
```

- Řádky nemají vlastní rámeček; pod myší / při fokusu je řádek jemně šedý
  (`--background-neutral-subtle`) a ukážou se ikony Upravit / Smazat.
- Autor a datum jsou jen v bublině štítku jednání (myš i Tab).
- Ikony se nevykreslí u vyjádření k uzavřenému jednání; server úpravu i
  smazání odmítne (`CommentService.EnsureMeetingAllowsCommentChangesAsync`).

---

## Pokrytí testy

- Unit: [CommentPaginationMarkupTests.cs](../../PmTracker.Tests.Unit/Projects/CommentPaginationMarkupTests.cs)
  (labely podle směru, poloha v hlavičce, žádný přesun v JS/CSS),
  [CompactCommentsBlockCssTests.cs](../../PmTracker.Tests.Unit/Layout/CompactCommentsBlockCssTests.cs) (pravidla bloku).
- Api: `RecordEditorControllerTests.RecordCommentsPartial_*` (10 / limit / vše),
  `CompactCommentsBlockRenderTests`, `ClosedRecordCommentsRenderTests`, `ClosedMeetingCommentChangesTests`.
- E2E: `RecordRichTextScenariosTests.RecordComments_ShouldLoadTenThenExpandWhilePreservingSortOnLoadedSubset`,
  `CompactCommentsBlockScenariosTests`.

---

## Pravidla pro úpravy

1. **Texty tlačítek stránkování** jsou dva `<span data-comment-pagination-label="asc|desc">`;
   při změně uprav view i tabulku výše.
2. **Nikdy nenastavuj text přes `textContent` / `innerText`** na gov-button
   (pm-button) — hydratace duplikuje obsah.
3. **Nové tlačítko stránkování** patří do `.comment-pagination-actions` v hlavičce.
4. **Žádný bundle**: `_Layout` načítá `site.js` jako ES modul; nový modul
   s vedlejšími efekty musí být importovaný v `bootstrap.js`.
