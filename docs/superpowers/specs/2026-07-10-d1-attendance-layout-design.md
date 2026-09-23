# D1 — Účast na detailu jednání: inline řádky, column-major sloupce, číslování (spec)

**Datum:** 2026-07-10 · **Stav:** rozhodnutí uživatele zapracována (1: jméno·role·e-mail, 2: číslo uvnitř karty) · **Náročnost:** malá

## Kontext

A1 grid (2026-07-08) skládá v kartě účastníka jméno / e-mail / role pod sebe →
karta ~3 řádky, na 27" z většiny prázdná. Dvousloupcové řazení je row-major.
Uživatel chce (2026-07-10): texty v jedné řádce se zalomením až při nedostatku
místa, sloupce plněné shora dolů a decentní číslo řádku uvnitř karty vlevo.

**Závazná lekce z i15 (feedback_i15_edge_css_compat):** nosná logika jen
z letitých CSS základů — flex-wrap, grid s explicitními řádky, CSS counters,
media query. Žádné `:has`, intrinsic keywords, multi-column balancování ani
container queries v nosné roli. Finální vizuální verdikt dává uživatel na i15.

## Chování

### 1. Inline tok v kartě
Obsah karty: `[č.] Jméno · role1 · role2 · e-mail … [stav dropdown]`
- Pořadí: **jméno (tučně) → role (muted, oddělené „·") → e-mail (nejvíc muted,
  menší)** — rozhodnutí uživatele.
- Jedna řádka, `flex-wrap` zalomí přirozeně až při nedostatku šířky (vzor
  dashboard dlaždice jednání, C2). Baseline zarovnání.
- Stav dropdown zůstává vpravo (šířka 240 px), na řádku s prvním řádkem textu;
  mobilní fallback ≤640 px beze změny (sloupec, select 100 %).

### 2. Column-major dvousloupec
- Server (Razor) spočítá `rows = ceil(pocet/2)` a předá přes inline CSS
  proměnnou (`style="--attendance-rows: N"`).
- Široký viewport (media query, práh ekvivalent dnešního auto-fill ~2×680 px):
  `display:grid; grid-auto-flow: column; grid-template-rows: repeat(var(--attendance-rows), auto); grid-auto-columns: 1fr;`
  → levý sloupec 1..N shora dolů, pravý N+1.. shora dolů. Deterministické,
  žádné browser balancování.
- Úzký viewport: jeden sloupec (dnešní chování), stejné pořadí.
- DOM pořadí (= B6 řazení: role skupiny → příjmení → jméno) se NEMĚNÍ;
  mění se jen vizuální tok. Form kontrakt `rows[i].OsobaId/StavUcasti`
  beze změny → backend nedotčen.

### 3. Číslování řádků
- Čisté CSS: `counter-reset` na gridu, `counter-increment` + `::before`
  na kartě. Číslo uvnitř karty vlevo (rozhodnutí uživatele): malé, muted
  (`var(--pm-text-muted)`, ~0.75rem), formát `1.`, pevná minimální šířka
  kvůli zarovnání dvouciferných, `user-select: none`.
- Číslo = pořadí v seznamu (B6 řazení); s column-major tokem čte 1..N dolů
  vlevo, N+1.. dolů vpravo. Jen vizuální orientace — nepropisuje se do
  tisku/exportu ani do dat.

## Dotčené soubory

- `PmTracker.Web/Views/Jednani/Detail.cshtml` — vnitřek `.meeting-attendance-row`
  (divy → spany v inline pořadí jméno·role·e-mail), `--attendance-rows` na gridu.
- `PmTracker.Web/wwwroot/css/site.css` — blok ~5550: grid column-major (media
  query), inline flex-wrap karty, counter ::before; mobilní fallback zachovat.

## Testy

- **Unit (CSS pin):** grid má `grid-auto-flow: column` + `repeat(var(--attendance-rows`
  v media bloku; karta `flex-wrap: wrap`; counter pravidla existují; zákaz
  `:has`/`max-content` v attendance bloku (i15 pojistka).
- **Api render:** pořadí v kartě jméno→role→e-mail (regex přes spany);
  `--attendance-rows: N` odpovídá ⌈count/2⌉ (seed se známým počtem osob);
  hidden inputy `rows[i]` v nezměněném pořadí.
- **Playwright (živě, dev):** 1470×956 → 1 sloupec, karty 1–2 řádky, čísla 1..n
  shora dolů; 2560×1440 → 2 sloupce, jednořádkové karty, levý sloupec 1..⌈n/2⌉,
  pravý zbytek; screenshoty pro finální posouzení uživatelem na i15.

## Mimo scope

Data a řazení účasti (B6), SaveAttendance flow, tisk/export jednání,
modal Přidat osobu.
