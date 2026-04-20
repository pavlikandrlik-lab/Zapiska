# PM Tracker – Technická dokumentace 11: Visual Testing Governance (Playwright)

## 1. Účel

Tento dokument definuje **governance pro vizuální testy přes Playwright**, tj. testy, které:

- spouštějí běžící dev server aplikace,
- otevírají skutečné stránky v prohlížeči,
- provádějí interakci s UI (klik, přetažení, odeslání),
- berou screenshoty a dělají *assertions* přes DOM (`document.querySelector`, `getComputedStyle`).

Cílem je **zachytit vizuální a chování-úrovňové regresions**, které unit/integrační testy neodhalí:

- chybějící kódy ve `site.bundle.js` (moduly, které nikdo nekonkatenoval),
- CSS regresse (nedostatek specificity, překrývající pseudo-elementy z Gov Design System),
- nepropojené delegace event handlerů v `bootstrap.js`,
- chybné vygenerované HTML (view modely vs. JS atributy),
- layout pod různými rozlišeními.

Tento governance **nenahrazuje** `PmTracker.Tests.Unit` / `.Integration` / `.Api` / `.E2e` — je to *jiný úhel pohledu*: browser-level smoke, který validuje „složený" výsledek.

---

## 2. Kdy použít Playwright smoke

**Použij vždy při:**

1. **Přidání nového JS modulu** — ověř, že je v `site.bundle.js` (aplikace v prohlížeči načítá pouze bundle, ne moduly).
2. **Nové stránce / novém panelu** — zkontroluj, že render odpovídá view modelům.
3. **Velkých CSS změnách** (nové komponenty, přepis layoutu) — zachytí konflikty s Gov Design System bullet/::before pseudo-elementy, rozbitý scope.
4. **Opravě nahlášeného UI bugu** — reprodukce → fix → re-run ověří, že se chování skutečně spravilo.
5. **Před merge PRs, které přidávají/mění** views, partials, JS moduly nebo CSS v `wwwroot/css/components/`.

**NEPOUŽÍVEJ** Playwright pro:

- Logiku, která se dá testovat jednotkově (business pravidla, state machiny, validace).
- Čistě backendové API — použij `PmTracker.Tests.Api`.
- E2E testy regresí uživatelských scénářů — pro to existuje samostatný `PmTracker.Tests.E2e` projekt.

Playwright smoke je **rychlá vizuální kontrola**, ne plná E2E sada.

---

## 3. Prerekvizity

### 3.1 Dev server běží

Aplikace musí běžet lokálně. Výchozí port je **5071** (HTTP) / **7071** (HTTPS) — převzato z `Properties/launchSettings.json`.

```bash
# V samostatném terminálu:
cd PmTracker.Web
ASPNETCORE_ENVIRONMENT=Development dotnet run
```

Nebo na pozadí:

```bash
cd PmTracker.Web && dotnet run > /tmp/pmtracker-run.log 2>&1 &
```

Počkej na řádek `Now listening on: http://localhost:5071` ve výstupu.

### 3.2 Databáze běží

Používáme pmtracker-sql container na `localhost:1433` (plné schéma včetně seed dat pro projekt 1).

```bash
docker ps | grep pmtracker-sql
# Pokud neběží:
docker start pmtracker-sql
```

`PmTracker.Web/appsettings.Development.json` obsahuje connection string (není v gitu — generuje se lokálně):

```json
{
  "ConnectionStrings": {
    "PmTrackerDb": "Server=localhost,1433;Database=PmTracker;User Id=sa;Password=PmTracker!2026;TrustServerCertificate=True;MultipleActiveResultSets=True",
    "TicketingReadOnly": ""
  }
}
```

### 3.3 Dev auth bypass

V Development prostředí se použije AD-bypass přes query param `?asUser=<email>`:

```
http://localhost:5071/projekty/1/dashboard?asUser=pavel.admin@pmtracker.local
```

Tento mechanizmus přepíná na konkrétní osobu z tabulky `dbo.osoby`. Pro testy je nejjednodušší použít `pavel.admin@pmtracker.local` (má všechna oprávnění).

### 3.4 Playwright skill + Chromium

Playwright se spouští přes lokální skill (není součástí repa):

```
~/.claude/plugins/cache/playwright-skill/playwright-skill/<version>/skills/playwright-skill
```

Kdyby Chromium chyběl:

```bash
cd $PW_SKILL_DIR && npm run setup
```

---

## 4. Kánonická struktura testovacího skriptu

### 4.1 Umístění

Skripty patří do `/tmp/playwright-*.js` — **nikdy do repa**. Soubor po dokončení testu zůstane v `/tmp` (OS ho smaže přirozeným TTL) a příště napíšeš nový.

Důvod: tyto skripty jsou **ad-hoc smoke** pro konkrétní iteraci; nejsou to opakovatelné E2E testy, které by měly být verzovány. Pokud něco chceš perzistovat, patří to do `PmTracker.Tests.E2e` (skutečný test projekt).

### 4.2 Šablona

```js
const { chromium } = require('playwright');

// TARGET_URL musí být parametrizovaný, ne hard-coded — dev server může běžet
// na 5071, 5999 nebo jiné instanci.
const TARGET_URL = process.env.PW_TARGET_URL || 'http://localhost:5071';
const AS_USER = 'pavel.admin@pmtracker.local';
const PROJEKT_ID = 1;

(async () => {
  const browser = await chromium.launch({
    headless: false,  // DEFAULT: vidět, co se děje.
    slowMo: 80,       // Uvolnění pro vizuální kontrolu.
  });

  const context = await browser.newContext({
    viewport: { width: 1920, height: 1080 },  // UI je širokoúhlá.
    ignoreHTTPSErrors: true,  // dev cert je self-signed.
  });
  const page = await context.newPage();

  // Vždy zachytávat console errors/pageerrors — často jsou první signál regrese.
  const consoleErrors = [];
  page.on('console', (m) => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  page.on('pageerror', (e) => consoleErrors.push('PAGE ERROR: ' + e.message));

  try {
    // === 1. Navigace + výchozí stav ===
    await page.goto(
      `${TARGET_URL}/projekty/${PROJEKT_ID}/dashboard?asUser=${encodeURIComponent(AS_USER)}`,
      { waitUntil: 'networkidle' },
    );
    await page.waitForTimeout(1000);  // Gov web components se inicializují async.
    await page.screenshot({ path: '/tmp/screen-01-default.png', fullPage: true });

    // === 2. Interakce + assertions přes DOM ===
    // Preferuj data-* selektory (stable) před CSS třídami nebo textem.
    await page.click('[data-dashboard-tab="vyzvy"]');
    await page.waitForTimeout(2000);  // AJAX load + JS bootstrap.

    const panelCount = await page.locator('[data-vyzvy-panel]').count();
    console.log('panel count:', panelCount);

    // === 3. Validace computed style (CSS regresse) ===
    const info = await page.evaluate(() => {
      const el = document.querySelector('#pm-vyzvy-reassign-modal');
      if (!el) return { found: false };
      const cs = getComputedStyle(el);
      return {
        position: cs.position,
        zIndex: cs.zIndex,
        background: cs.backgroundColor,
      };
    });
    console.log('modal computed:', JSON.stringify(info));

    // === 4. Shrnutí ===
    console.log('Console errors:', consoleErrors.length);
    consoleErrors.slice(0, 10).forEach((e) => console.log('  ❌ ' + e.slice(0, 200)));
  } catch (err) {
    console.error('❌ TEST ERROR:', err.message);
    try { await page.screenshot({ path: '/tmp/screen-error.png', fullPage: true }); } catch (_) {}
  } finally {
    await page.waitForTimeout(1500);
    await browser.close();
  }
})();
```

### 4.3 Spuštění

```bash
cd ~/.claude/plugins/cache/playwright-skill/playwright-skill/*/skills/playwright-skill
node run.js /tmp/playwright-moje-test.js
```

Nebo přes Claude Code tool:

```
Bash: cd $PW_SKILL_DIR && node run.js /tmp/playwright-moje-test.js
```

---

## 5. Testovací matrix — co při review zkontrolovat

Pro každou změnu, která se dotkne UI, projdi následující matrix:

### 5.1 Bundle sync

- [ ] Vznikl nový soubor v `PmTracker.Web/wwwroot/js/modules/**`?
  → Ověř, že jeho funkce jsou **zkonkatenovány do `site.bundle.js`** (aplikace v prohlížeči načítá jen bundle, ne moduly).
- [ ] Přidal jsi nový event handler (click/change)?
  → Ověř, že je **propojený v `bootstrap.js`** → `handleDocumentClick` / `handleDocumentChange`.
- [ ] Přidal jsi novou inicializační funkci?
  → Ověř, že je v poli `runInitializers([ ... ])` v `bootstrapPmTrackerApp`.

**Červená vlajka:** `grep -n "mojeNovaFunkce" site.bundle.js` vrátí nulové výsledky → bundle není synchronizovaný.

### 5.2 Gov Design System interference

Gov Design System core.css nastavuje globální pravidla, která mohou přebít tvé custom styly:

- **`ul > li::before { content: "●" }`** — přidává bullet pseudo-element absolutně na všechny `ul li`. `list-style: none` **to nevypne**. Je potřeba vypnout `::before { content: none }` nebo použít `.gov-list--plain`.
- **`input, select, textarea`** globální styly v `site.css` mohou narušit Gov Web Components (`gov-form-input`) — scope přes `:not(gov-* input)`.
- **Gov buttons (`gov-button`)** — `instanceof HTMLButtonElement` na ně nematchne. Použij `isButtonLike` / `setButtonDisabled` helpery.

**Červená vlajka:** screenshot ukazuje bullet před text tam, kde ho nemá být → gov `::before` pseudo tě našel.

### 5.3 Modal / overlay

- [ ] Je modal `position: fixed; inset: 0`?
- [ ] Má backdrop (`background: rgba(0,0,0,0.x)`)?
- [ ] Má vysoký `z-index` (alespoň 2000+)?
- [ ] Zavírá se přes Escape, klik mimo, × button?

**Červená vlajka:** modal se renderuje pod footerem místo centrovaného overlay → chybí CSS pro jeho outer container.

### 5.4 Konzistentní layout při širokoúhlém rozlišení

Viewport nastav na **1920×1080** (cílové rozlišení produkce). UI má být navrženo širokoúhle; nezjišťuj mobilní layout, pokud to není explicitně v zadání.

### 5.5 Console errors

Každý test **musí** monitorovat `console` + `pageerror` a ohlásit jejich počet. **Jediný tolerovaný error** je obvykle chybějící favicon (404) — vše ostatní je regrese.

---

## 6. Dev auth bypass + interceptor patterns

### 6.1 Jednoduchý přidat `?asUser=` na první URL

```js
await page.goto(`${TARGET_URL}/path?asUser=${encodeURIComponent(AS_USER)}`);
```

Všechny následující AJAX requesty v rámci session přijmou cookie a authentication zůstane.

### 6.2 Route interceptor pro automatický přídavek `?asUser=`

Pokud aplikace dělá interní přesměrování nebo ztrácí `asUser`, nastav globální interceptor:

```js
await context.route('**/*', async (route) => {
  const req = route.request();
  const url = new URL(req.url());
  if (url.host.startsWith('localhost')
      && !url.searchParams.has('asUser')
      && !url.pathname.startsWith('/lib/')
      && !url.pathname.startsWith('/css/')
      && !url.pathname.startsWith('/js/')
      && !url.pathname.startsWith('/images/')
      && !url.pathname.startsWith('/fonts/')) {
    url.searchParams.set('asUser', AS_USER);
    await route.continue({ url: url.toString() });
  } else {
    await route.continue();
  }
});
```

---

## 7. Selectors — co preferovat

**Priorita (nejstabilnější → nejkřehčí):**

1. **`[data-*]` atributy** specifické pro testy / bootstrap (`data-vyzvy-panel`, `data-dashboard-tab`). ← **PREFER TOTO**
2. **ID** (`#pm-vyzvy-reassign-modal`).
3. **Strukturální CSS třídy** (`.vyzvy-card`, `.modal-body`).
4. **Textový obsah** (`filter({ hasText: /Upravit/ })`) — jen když není jiná možnost, lámou se lokalizací.

**NIKDY:**

- XPath absolutní (`//div[3]/span[1]`).
- Nth-of-type (`li:nth-of-type(2)`).

Pokud v kódu nevidíš `data-*` atribut, který by ti usnadnil test, přidej ho do template — je to součást testování.

---

## 8. Assertions přes computed style

Screenshot je **důkaz pro člověka**, ale pro automatický pass/fail musíš validovat přímo DOM. Vzor:

```js
const info = await page.evaluate(() => {
  const el = document.querySelector('.vyzvy-stav-menu-list');
  if (!el) return { found: false };
  const cs = getComputedStyle(el);
  return {
    found: true,
    display: cs.display,
    listStyle: cs.listStyleType,
    zIndex: cs.zIndex,
    // Detail, který ukáže regresii Gov bullet pseudo:
    liBefore: (function () {
      const li = el.querySelector('li');
      if (!li) return null;
      const before = getComputedStyle(li, '::before');
      return before.content;
    })(),
  };
});
```

Tyto hodnoty porovnej proti očekávání — pokud `info.liBefore` vrátí `"●"`, víš, že Gov core.css tě našel.

---

## 9. Screenshoty — kdy a kolik

**Vždy** alespoň jeden per pain-point:

- výchozí stav (landing),
- po hlavní interakci,
- finální stav (otevřený modal, menu, apod.).

**Jméno**: `/tmp/<prefix>-<cislo>-<nazev>.png` (např. `verify-03-stav-menu.png`).

**Rozlišení**: `fullPage: true` zachytí celou stránku včetně footeru — lepší než viewport crop.

**Při chybě**: vždy vyfoť screenshot do `/tmp/<prefix>-error.png` v catch bloku.

---

## 10. Červené vlajky v testu, které musí bloknout merge

- Console errors > 1 (favicon 404 je OK).
- Selektor vrací `.count() === 0` tam, kde má být prvek → bundle/view misalignment.
- Computed style `display: "none"` tam, kde má být `block`.
- Screenshot ukazuje překrytí elementů (z-index konflikt).
- Screenshot ukazuje bullet / marker, tam kde neměl být (Gov interference).
- `data-*` ready atribut chybí (`data-vyzvy-bootstrapped`, `data-project-dashboard-ready`) → JS bootstrap se nespustil.

---

## 11. Typické případy, které tento typ testu už odhalil

### 11.1 `projectDashboard.js` chyběl v `site.bundle.js`

Symptom: klik na záložku „Výzvy" nic nedělal — panel se nenačetl.
Kauza: modul `wwwroot/js/modules/projectDashboard.js` existoval, ale jeho funkce nebyly v bundlu. Prohlížeč bundle načítá, ne moduly.
Oprava: inline funkcí `initProjectDashboardShell`, `handleProjectDashboardClick`, `handleProjectDashboardChange` do `site.bundle.js` + propojení v `handleDocumentClick` / `handleDocumentChange`.

### 11.2 Reassign modal se renderoval pod footerem

Symptom: klik na „Upravit přiřazení PNF" otevřel modal jako flow element pod obsahem.
Kauza: chyběla CSS třída `.pm-modal` (`position: fixed`, `inset: 0`, backdrop).
Oprava: přidat `.pm-modal` styl do `vyzvy-panel.css`.

### 11.3 Bullet tečky před `<li>` v custom ul menu

Symptom: ve stav menu a v buffer kartě se zobrazovala modrá tečka vlevo před každým řádkem, i přes `list-style: none` v CSS.
Kauza: Gov Design System `core.css` má globální pravidlo `ul > li::before { position: absolute; content: "●" }`. `list-style: none` nevypíná pseudo-elementy.
Oprava: `.vyzvy-polozky > li::before { content: none !important }` a obdobně pro menu.

**Lesson learned:** každé přidání custom `ul/li` mimo Gov komponenty musí explicitně vypnout `::before` pseudo-element.

---

## 12. Limity

Playwright smoke je **rychlá vizuální kontrola**, ne regresní sada. Konkrétně:

- **Není deterministický** — timing, AJAX rychlost, DOM ready mohou flapovat. Používej `waitForTimeout`/`waitForSelector` místo fixed sleep všude možně.
- **Nechrání proti pomalému načítání** — pro to mají být BenchmarkDotNet / perf testy.
- **Nevaliduje celé user-flow přes více stránek a rolí** — pro to je `PmTracker.Tests.E2e`.
- **Runs on dev database** — stav DB může ovlivnit testy (proto tolerujeme `asUser=pavel.admin` a projekt id 1).

Pro **stabilní repeatable testy** přemigruj scénář do `PmTracker.Tests.E2e` projektu.

---

## 13. Checklist před merge PRs s UI změnami

- [ ] Build zelený.
- [ ] Unit/Integration testy zelené (`dotnet test`).
- [ ] `site.bundle.js` obsahuje všechny nové funkce z modulů.
- [ ] Při lokálním spuštění (`dotnet run` + open browser) nová funkcionalita vizuálně funguje.
- [ ] Playwright smoke (dle této governance) proběhl **bez console errors** a zachytil očekávané stavy.
- [ ] Screenshoty proběhly a zachytily nový stav UI.
- [ ] `data-*` atributy byly přidány tam, kde testy potřebují stabilní selektor.
- [ ] CSS komponent explicitně vypnul `::before { content: none }` pro custom `<ul>/<li>` struktury (Gov interference).

---

## 14. Související dokumenty

- `docs/technical/09-testing-quality.md` — celková testing strategie.
- `docs/architecture/js-modules.md` — jak funguje bundle + moduly.
- `docs/technical/07-security-authz.md` — dev auth bypass a `?asUser=`.

---

**Verze**: 1.0 (2026-04-20) — první draft, založen na praxi z Fáze 2 UI.
