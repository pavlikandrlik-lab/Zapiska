# B1 — Dashboard: dlaždice v panelech se dole neřežou (scroll-snap + fade)

**Datum:** 2026-07-09 · **Stav:** schváleno uživatelem (analýza 2026-07-09)

## Problém a kontext okolí (reprodukováno)
Listy panelů Přehledu (`.dashboard-focus-list`, `.dashboard-meeting-list`, `.dashboard-news-list`, site.css ~7536) mají `overflow-y: auto` a řežou obsah **kdekoli dojde místo** — screenshot s naplněnými panely: 5. dlaždice uříznutá v půli borderu. Na 13" s reálnými daty scrollují oba panely → dole je vždy „useknutá" dlaždice a nic nesignalizuje pokračování. (Kompaktní dlaždice z A2 jsou nasazené a fungují — 92 px; tohle je poslední vrstva problému.)

Repro poznámka: focus panel plní priority matice (`zaznam_priority_uzivatelu`; jen úkoly ve stavu RUN/OPEN; bootstrap rebuild jen když `last_full_rebuild_status='NEVER'`).

## Řešení (CSS-only, sdílené list třídy)
1. **Scroll-snap:** na listech `scroll-snap-type: y proximity`; na dlaždicích (`.dashboard-focus-item`, `.dashboard-meeting-item`, `.dashboard-news-item`) `scroll-snap-align: start`. Po doscrollování jsou viditelné dlaždice celé; proximity (ne mandatory) nechává plynulý scroll bez „lepení".
2. **Fade afordance:** spodní hrana listu s fade maskou, jen když je co scrollovat — čisté CSS:
   `mask-image: linear-gradient(to bottom, black calc(100% - 28px), transparent)` na listu; aby fade nebyl na konci scrollu rušivý, použít variantu `background-attachment: local` overlay trik NEBO jednodušeji ponechat konstantní mask (fade i na konci je přijatelný — decision: **konstantní mask**, žádný JS).
3. Žádné změny výšek, tokenů ani markupu.

## Dotčené soubory
- `PmTracker.Web/wwwroot/css/site.css` — blok listů (~7536) + 3 dlaždicové třídy.

## Akceptační kritéria (Playwright, 1280×832, plné panely)
- Po `scrollTop=0` i po scrollu na snap pozici není žádná dlaždice proříznutá hranou listu (bottom dlaždice ≤ bottom listu, nebo celá pod hranou).
- Fade mask přítomna na scrollovatelném listu (computed mask-image ≠ none).
- Scroll stále funguje (scrollHeight > clientHeight ⇒ lze doscrollovat na poslední dlaždici celou).

## Testy
- Unit source-assertion: listy mají `scroll-snap-type`, dlaždice `scroll-snap-align`, mask-image pravidlo existuje.
- Playwright měření dle kritérií + screenshot pro ruční odsouhlasení (fade vzhled).

## Mimo scope
Výšky panelů/tokenů; JS scroll indikátory; subpages (nescrollují interně).
