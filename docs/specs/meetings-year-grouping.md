# Specifikace — seskupení jednání podle let (meeting-year-grouping)

Dokumentuje požadované chování sekce jednání seskupené po letech. Týká se dvou míst v aplikaci:

1. **Aplikační záložka „Jednání"** — `/Jednani/Index`
   - Soubor view: [PmTracker.Web/Views/Jednani/Index.cshtml](../../PmTracker.Web/Views/Jednani/Index.cshtml)
2. **Projektová záložka „Jednání"** — `/Projekty/Detail/{id}?tab=jednani`
   - Soubor view: [PmTracker.Web/Views/Projekty/_ProjectMeetingsTab.cshtml](../../PmTracker.Web/Views/Projekty/_ProjectMeetingsTab.cshtml)

Obě místa používají stejnou šablonu DOM (`.meeting-year-stack` → `.meeting-year-group` → `.meeting-year-body`). Rozdíl je v **výchozím stavu historických let**.

---

## Výchozí chování

### Aplikační záložka `/Jednani/Index`

#### Struktura projekt-karty

V každé projekt-kartě se zobrazuje:

1. **Hlavička projektu** (`<header class="meeting-header">`) — název + `+N starších roků` counter + chevron. Celá hlavička je **klikatelný toggle** pro rozpad/sbalení historických roků (`data-project-history-toggle`).
2. **Aktuální rok** (`PreviewRok`) — **vždy viditelný** year-group v `state="preview"` (první řádek karet).
3. **Historické roky** — schované ve wrapperu `[data-project-history-body][hidden]`. Po kliknutí na hlavičku projektu se zobrazí jako year-groups v `state="collapsed"` (každý rok má vlastní year-chevron pro rozbalení jednání).

#### Viditelnost

| Element | Default | Po kliknutí na header |
| --- | --- | --- |
| Hlavička projektu | viditelná, chevron-down | chevron-up |
| Aktuální rok year-group | `state="preview"` | nezměněno |
| Historické year-groups | `hidden` (skryté) | viditelné, každý `state="collapsed"` |

**Důvod**: projekt-karta drží jen aktuální dění; starší historie je tichá, ale jedno kliknutí ji ukáže. Each year-group je pak individuálně collapsed (další click-through pro jednání).

#### Markup

```html
<section class="card meeting-project-overview" data-project-card>
    <header class="meeting-header"
            role="button"
            data-project-history-toggle
            aria-controls="project-history-42"
            aria-expanded="false"
            tabindex="0">
        <h2>Název projektu</h2>
        <span class="meeting-project-history-count" aria-hidden="true">+3 starších roků</span>
        <gov-icon class="meeting-project-chevron" name="chevron-down" ...></gov-icon>
    </header>

    <!-- Aktuální rok vždy viditelný -->
    <div class="meeting-year-stack" data-meeting-overview="year-grouped"
         data-meeting-history-default="collapsed"
         data-meeting-preview-year="2026">
        <section class="meeting-year-group" data-meeting-year-state="preview">...</section>
    </div>

    <!-- Historické roky za toggle -->
    <div class="meeting-project-history-body"
         id="project-history-42"
         data-project-history-body
         hidden>
        <div class="meeting-year-stack" ...>
            <section class="meeting-year-group" data-meeting-year-state="collapsed">...</section>
            <!-- další historické year-groups -->
        </div>
    </div>
</section>
```

### Projektová záložka `/Projekty/Detail/{id}?tab=jednani`

| Rok | Výchozí stav | Zobrazení |
| --- | --- | --- |
| **Aktuální rok** (PreviewRok) | `preview` | Body viditelný, pouze první řádek karet (stejné jako `/Jednani/Index`). |
| **Historické roky** | `collapsed` | Body je `hidden`, karty nejsou v DOMu viditelné. Uživatel musí rok ručně rozkliknout year-chevronem. |

**Důvod**: konzistence s aplikační záložkou — user 2026-04-20 si vyžádal sjednocené chování, aby na historické jednání bylo vždy nutné explicitní kliknutí (proti přehlcení dlouhou historií).

Markup:

```html
<div class="meeting-year-stack"
     data-meeting-overview="year-grouped"
     data-meeting-history-default="collapsed"
     data-meeting-preview-year="2026">
    <!-- aktuální rok state="preview", ostatní state="collapsed" -->
</div>
```

---

## Stavy `data-meeting-year-state`

Atribut `data-meeting-year-state` nastavený na `<section class="meeting-year-group">` řídí viditelnost:

| Stav | Body `hidden` | Karty | Šipka | `aria-expanded` |
| --- | --- | --- | --- | --- |
| `collapsed` | ano | skryté (v DOM, `hidden`) | ↓ dolů (rotate 45°) | `false` |
| `preview` + skryté další karty | ne | pouze první řádek, ostatní `hidden` + `data-meeting-preview-hidden` | ↓ dolů | `false` (je co rozbalit) |
| `preview` + celý rok se vešel na 1. řádek | ne | všechny | ↑ nahoru | `true` |
| `open` | ne | všechny viditelné | ↑ nahoru | `true` |

**Toggle chování** (klik na `.meeting-year-toggle`):

- `collapsed` → `open`
- `preview` + `data-meeting-year-has-hidden="true"` → `open` (uživatel uvidí vše)
- `preview` bez skrytých karet → `collapsed`
- `open` → `collapsed`

Logika je v [PmTracker.Web/wwwroot/js/modules/meetingOverview.js](../../PmTracker.Web/wwwroot/js/modules/meetingOverview.js).

---

## Šipka (meeting-year-chevron)

- Default: `rotate(45deg)` = ↓ (dolů, rozbalit)
- `open`: `rotate(-135deg)` = ↑ (nahoru, sbalit)
- `preview` bez skrytých karet: `rotate(-135deg)` = ↑ (nahoru, sbalit)
- `preview` se skrytými kartami: `rotate(45deg)` = ↓ (dolů, je co rozbalit)

CSS: [PmTracker.Web/wwwroot/css/site.css](../../PmTracker.Web/wwwroot/css/site.css), selektor `.meeting-year-group[data-meeting-year-state="open"] .meeting-year-chevron` apod.

---

## Pravidla pro úpravy

1. **Nikdy neměň chování jednoho místa, aniž bys ověřil druhé.** Obě šablony musí zůstat konzistentní v DOM struktuře, ale jsou plně konfigurovatelné přes `data-meeting-history-default`.
2. **Výchozí stav pro historické roky** se generuje v serverové šabloně (`isPreviewYear ? "preview" : "collapsed"|"open"`). Při změně chování aktualizuj tento dokument i testy.
3. **JS meetingOverview** nesmí resetovat stav nastavený serverem — smí ho jen měnit po kliknutí uživatele.
4. **Kritéria pro `preview`**: první vizuální řádek karet dle `offsetTop`. Když se celý rok vejde na první řádek, `data-meeting-year-has-hidden` NEBUDE nastaven a šipka bude nahoru.

---

## Pokrytí testy

- [PmTracker.Tests.Unit/Layout/MeetingsYearGroupingTests.cs](../../PmTracker.Tests.Unit/Layout/MeetingsYearGroupingTests.cs) — kontroluje `data-meeting-history-default` v obou šablonách a výchozí `data-meeting-year-state` pro historické roky.
