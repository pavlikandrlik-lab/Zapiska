# Design: Fluid + adaptivní layout uživatelského přehledu (dashboard)

- **Datum:** 2026-06-09
- **Branch:** codex/senior-refactor-fase-1
- **Restore point před implementací:** commit `4af292d`
- **Skill flow:** brainstorming → (tento spec) → writing-plans → implementace

## Problém

Hlavní stránka (uživatelský přehled, `DashboardController.Index`) působí na 13"
MacBook Air řídce — hodně prázdného místa mezi prvky i uvnitř karet — a zároveň
má layoutový bug: panel **Novinky** „odjede pod obrazovku" mimo zorné pole, když
je hodně **jednání**. Rozměry jsou fixní (rem), nepřizpůsobují se velikosti
monitoru (13"–28").

### Root cause (zjištěno exploreם)

1. **100vh antipattern:** `.dashboard-shell { height: 100vh }` sedí pod sticky
   `app-header`. Reálná výška stránky = `header + 100vh` → spodní pravý panel
   (Novinky) přeteče pod fold.
2. **Class mismatch:** partial `_DashboardMeetingsList.cshtml` používá třídu
   `.dashboard-meeting-list` (jednotné č.), ale scroll pravidlo
   (`overflow-y:auto; flex:1 1 auto`) cílí `.dashboard-meetings-list` (množné č.).
   → meetings list nescrolluje uvnitř panelu a roztahuje panel, tlačí Novinky dolů.
3. **Fixní mezery/font:** `gap 0.9rem`, panel padding `1.5rem`, karta padding
   `1rem`, focus karta nese 4-políčkový meta blok (Vlastník/Subsystém/Založeno/
   Termín) → velké karty, na 13" se vejde málo, na 28" se prostor nevyužije.

### Fakta o obsahu (z dokumentace + kódu)

- Tři panely: **Na co se soustředit** (focus = „Moje priority", primární pracovní
  pohled), **Nejbližší jednání** (meetings), **Co je nového** (novinky = pasivní).
- Limity položek (`DashboardController`): focus **8**, jednání **5**, novinky **5**.
- Dashboard je read-only rozcestník; klik vede do detailu/editoru.
- Dokumentace: `docs/wiki/uzivatelsky-dashboard/` (index, moje-priority, novinky).

## Rozhodnutí (odsouhlasena uživatelem)

| Téma | Volba | Důsledek |
|---|---|---|
| Model výšky | **Hybrid** | 28" (vysoký viewport) = fix na 1 obrazovku, interní scroll panelů; 13" (nízký) = fallback na scroll stránky, panely se nesmáčknou |
| Hustota položek | **Adaptivní** | úzký panel → hustý 1–2řádek; široký → bohatá karta s meta. Řízeno **container query**. |
| Rozmístění | **Focus dominantní 2+1** | Focus velký vlevo (2fr), Jednání+Novinky vpravo nad sebou (1fr); na úzkém okně stack pod sebe (Focus → Jednání → Novinky) |
| Škálování | **Plynulé `clamp()`** | písmo i mezery rostou s šířkou viewportu, min/max hranice proti extrémům |

## Návrh řešení

### A. Layout engine (výška + scroll)

- Zrušit `height: 100vh` na `.dashboard-shell`. Místo toho navázat na výšku pod
  headerem: `min-height: calc(100dvh - var(--app-header-h))`.
  - `--app-header-h` vystavit jako CSS proměnnou. Primárně CSS odhad/clamp;
    pokud je header dynamický, doměřit přes malý JS (`ResizeObserver` na
    `.app-header`, set `--app-header-h`). Preferovat čisté CSS, JS jen když nutné.
  - `dvh` (dynamic viewport height) kvůli mobilním/proměnným lištám.
- **Hybrid přes min-height panelů:** každý panel `min-height: clamp(120px, 18vh, 220px)`.
  - Vysoký viewport (28") → grid `1fr 1fr` vyplní přesně, panely scrollují uvnitř.
  - Nízký viewport (13") → součet min-heightů + header > výška → scrolluje celá
    stránka (zvolený fallback), panely zůstanou čitelné.
- **Bugfix scroll:** sjednotit třídu meetings listu (partial i CSS na jeden název)
  a zajistit `flex:1 1 auto; min-height:0; overflow-y:auto` pro všechny tři listy.

### B. Fluid tokeny (single source of truth)

Definovat na `.dashboard-shell` (scoped), ať se vše škáluje z jednoho místa
(navazuje na konvenci `--pm-*` aliasů). Ilustrativní hodnoty k vizuálnímu doladění:

```css
.dashboard-shell {
  --d-fs-label: clamp(0.68rem, 0.60rem + 0.25vw, 0.80rem);
  --d-fs-base:  clamp(0.80rem, 0.70rem + 0.35vw, 0.98rem);
  --d-fs-title: clamp(0.92rem, 0.78rem + 0.45vw, 1.18rem);
  --d-gap-item:  clamp(0.35rem, 0.15rem + 0.45vw, 0.85rem);
  --d-pad-card:  clamp(0.5rem,  0.30rem + 0.5vw,  1.05rem);
  --d-pad-panel: clamp(0.55rem, 0.30rem + 0.6vw,  1.2rem);
  --d-radius:    clamp(0.45rem, 0.30rem + 0.3vw,  0.85rem);
}
```

Všechny dashboard rozměry (font, gap, padding, radius) přepsat na tyto proměnné.

### C. Adaptivní hustota (container queries)

Každý panel-body = container (`container-type: inline-size`). Karty reagují na
**šířku panelu**, ne celé stránky (funguje i ve stacku).

- **Focus karta:**
  - úzký panel (`@container < ~520px`): 2řádek — `PRJxx #N Název [stav] ●termín` +
    řádek meta inline (`Vlastník · Subsystém`), popis cíle ořez na 1 řádek.
  - široký panel: dnešní 3-col grid (identita | obsah+popis 2 řádky | meta grid),
    ale s fluid tokeny.
- **Jednání / Novinky karty:** úzký = 1 řádek (kód, název, datum); široký = 2 řádky
  s meta (čas, místo / actor, projekt).
- Empty state: kompaktní muted text, panel se smrskne na `min-height` (žádná velká
  prázdná karta).

### D. Scénáře (akceptační — co uživatel vidí)

1. **13" (1280×800) typický** (focus 8 / jedn 3 / nov 4): vejde se na ~1 obrazovku,
   focus hustý 2řádek, panely scrollují uvnitř na zbytek.
2. **13" edge — hodně jednání (5) + novinek (5):** každý panel scrolluje uvnitř,
   Novinky zůstávají viditelné (žádný fold). ✅ hlavní bug vyřešen.
3. **FHD 1920×1080 typický:** focus sloupec širší → bohatá 3-col karta, střední
   font, 1 obrazovka.
4. **28" QHD/4K typický:** větší font (clamp strop), karty plně rozbalené, vše bez
   scrollu, prostor využit čitelností místo prázdnem.
5. **Edge — prázdné panely:** kompaktní empty state, panely nedrží 50% výšky zbytečně.
6. **Edge — úzké okno / split-screen (< ~900px):** stack pod sebe (Focus → Jednání
   → Novinky), husté řádky, scroll stránky.

## Dotčené soubory (předběžně)

- `PmTracker.Web/wwwroot/css/site.css` — bloky `.dashboard-shell`, `.dashboard-body`,
  `.dashboard-panel-*`, `.dashboard-focus-*`, `.dashboard-meeting(s)-*`,
  `.dashboard-news-*`, media query; přidat tokeny + container queries.
- `PmTracker.Web/Views/Dashboard/_DashboardMeetingsList.cshtml` — sjednotit třídu.
- Případně `_DashboardFocusList.cshtml` / `_DashboardNewsList.cshtml` — drobné
  hooky pro container-query varianty (data-atributy/třídy), bez změny dat.
- `_Layout.cshtml` / `app-header` CSS — vystavit `--app-header-h` (případně malý JS).
- Žádná změna ve viewmodelech, controlleru ani službách (čistě prezentační vrstva).

## Mimo rozsah (YAGNI)

- Změna počtu položek (8/5/5 zůstává), filtry, řazení, nové panely.
- Změna obsahu/dat karet (jen vizuální hustota).
- Globální vyhledávání (samostatná oblast).

## Rizika

- `--app-header-h`: pokud je header výška proměnná, čistě CSS odhad může být
  nepřesný → fallback malý JS s `ResizeObserver`.
- Container queries: podpora OK v moderních prohlížečích (cílový intranet = aktuální
  Edge/Chrome). Ověřit cílovou verzi.
- Dark mode: dashboard tokeny respektovat v obou tématech (gov tokens).
- Pre-existing WIP na branchi — implementaci držet v izolovaných commitech.
```
