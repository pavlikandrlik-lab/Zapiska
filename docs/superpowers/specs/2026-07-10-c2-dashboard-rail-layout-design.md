# C2 — Pravý sloup dashboardu: dělení 50/50 + responzivní dlaždice jednání (spec)

**Datum:** 2026-07-10 · **Stav:** čeká na schválení · **Náročnost:** malá–střední (CSS + markup dlaždice)

## Kontext

Obsahová logika panelů je v pořádku (ověřeno reprodukcí: jednání s minulým
datem se korektně nezobrazuje; limit 5 nejbližších + „Zobrazit více" uživateli
vyhovuje — beze změny). Problémy jsou čistě layoutové.

## Problém 1 — dělení výšky pravého sloupce (~30/70 místo 50/50)

`.dashboard-rail` panely s obsahem mají `flex: 1 1 auto` (site.css ~7543).
Flex-basis `auto` = výchozí velikost podle obsahu → panel s vyšším obsahem
(novinky) trvale zabírá víc; jednání se 3 dlaždicemi dostane okno pro ~2.

### Chování (zadání uživatele, upřesněno 2026-07-10)

- Skutečný cíl: **dlaždice jednání viditelné bez rolování** (uživatel: 3
  jednání, ale okno jen pro 2 i na 27" monitoru). Novinky nesmí mít víc
  absolutního prostoru na úkor jednání, dokud jednání svůj prostor
  potřebují.
- Pravidlo: **50/50 je strop, ne vynucený poměr** — panel jednání dostane
  tolik, kolik jeho obsah potřebuje, až do poloviny sloupce; zbytek si
  vezmou novinky. Když jeden panel potřebuje méně (málo/žádný obsah),
  druhý si volný prostor vezme.
- Souhra s Problémem 2: po zhuštění dlaždic (1 řádka na 27") bude obsah
  jednání výrazně nižší, takže typicky zabere *méně* než polovinu a strop
  ani nenarazí — strop zůstává jako strukturální pojistka proti vytlačení
  (dnes prohrává kvůli flex-basis auto, ne kvůli záměrnému poměru;
  ověřeno v git historii — commit c313327 řešil jen empty-stav).

### Návrh (CSS-only)

Obsahové panely: `flex: 1 1 0` (basis 0 → rovný dělicí základ)
+ `max-height: max-content` (panel nikdy neroste nad vlastní obsah —
slack automaticky přeteče druhému panelu). Tím vzniknou přesně oba
požadované režimy: oba plné → 50/50; jeden malý → obsah + zbytek druhému.
Stávající `:has(...)` empty-detekce se stane redundantní → smazat, pokud
`max-height: max-content` pokryje i empty stav (ověřit v Chromium/Safari;
kdyby ne, `:has` větev zůstává jako fallback pro prázdný panel).
Scroll uvnitř listů, B1 fade/snap i `@media (max-width:1024px)` stack
zůstávají beze změny.

## Problém 2 — dlaždice jednání: pevné 3 řádky, na 27" z většiny prázdná

`_DashboardMeetingsList.cshtml`: dlaždice = 3 blokové řádky pod sebou
(kód+badge / „Jednání č. X" / datum·čas·projekt) bez ohledu na šířku.

### Chování (zadání uživatele)

Počet řádek **responzivně podle obsahu a šířky** — na 27" jedna řádka,
na 13" dvě, delší obsah klidně víc. Týká se **jen dlaždic jednání**
(focus/news dlaždice beze změny).

### Návrh

Vnitřek dlaždice = jeden flex kontejner s `flex-wrap: wrap` a řízeným
pořadím: `[PMT] [badge stavu] Jednání č. 905 · 18.07.2026 09:00 · název
projektu`. Přirozený wrap podle dostupné šířky — žádné breakpointy;
container query (`dashpanel` už existuje na `.dashboard-panel-body`)
jen kdyby bylo potřeba doladit typografii úzkého panelu. Sémantika
zůstává (strong kód, badge, muted meta). Stejný partial používá i
stránka `/dashboard/meetings` → změna se propíše konzistentně (žádoucí,
tam je dlaždice ještě širší).

## Testy

- **Unit (CSS pin):** rail panely `flex-basis: 0` + `max-height: max-content`;
  dlaždice jednání `flex-wrap: wrap`; focus/news dlaždice beze změny.
- **Api render:** markup dlaždice — nové pořadí prvků, žádný ztracený údaj
  (kód, stav, číslo, datum, čas, projekt).
- **Playwright (živě):** 1440×900 i 2560×1440 — (a) **primární akceptace:
  s 3 jednáními a plnými novinkami jsou všechny 3 dlaždice jednání
  viditelné bez scrollu v panelu** (na obou viewportech); (b) panel jednání
  nikdy nepřesáhne ~50 % sloupce, pokud novinky mají obsah; (c) prázdné
  novinky → jednání zabírá téměř celý sloup; (d) dlaždice na širokém
  viewportu 1 řádka, na 13" 2 řádky; screenshoty pro ruční kontrolu.

## Mimo scope

Obsahová logika panelů (limit, scoping, novinky) — nechává se být
(rozhodnutí uživatele 2026-07-10). Membership scoping případně později
samostatně.
