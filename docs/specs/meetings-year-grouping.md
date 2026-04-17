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

| Rok | Výchozí stav | Zobrazení |
| --- | --- | --- |
| **Aktuální rok** (PreviewRok) | `preview` | Body je viditelný, ale zobrazen pouze **první řádek karet**. Ostatní karty jsou v DOM, mají `hidden` a `data-meeting-preview-hidden="true"`. |
| **Historické roky** | `collapsed` | Body je `hidden`, karty v DOM neuvidí ani uživatel ani screen reader. |

**Důvod**: na stránce `/Jednani/Index` je víc projektů za sebou, rozbalená historie by udělala scroll nepřehledným.

Markup:

```html
<div class="meeting-year-stack"
     data-meeting-overview="year-grouped"
     data-meeting-history-default="collapsed"
     data-meeting-preview-year="2026">
    <!-- aktuální rok state="preview", ostatní state="collapsed" -->
</div>
```

### Projektová záložka `/Projekty/Detail/{id}?tab=jednani`

| Rok | Výchozí stav | Zobrazení |
| --- | --- | --- |
| **Aktuální rok** (PreviewRok) | `preview` | Body viditelný, pouze první řádek karet (stejně jako v `/Jednani/Index`). |
| **Historické roky** | `open` | Body je viditelný, **všechny karty jsou zobrazené**. Uživatel může rok sbalit ručně kliknutím na záhlaví. |

**Důvod**: v detailu projektu jsou pouze jednání daného projektu, takže zobrazit celou historii není neúnosné a uživatel ocení plný přehled bez rozklikávání.

Markup:

```html
<div class="meeting-year-stack"
     data-meeting-overview="year-grouped"
     data-meeting-history-default="open"
     data-meeting-preview-year="2026">
    <!-- aktuální rok state="preview", ostatní state="open" -->
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
