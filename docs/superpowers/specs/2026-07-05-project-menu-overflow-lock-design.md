# Projektové menu — 3 + overflow se zamykatelným rozbalením (Design)

**Datum:** 2026-07-05
**Stav:** Návrh k odsouhlasení
**Autor:** Claude + Ing. Pavel Andrlík
**Navazuje na:** Sub-projekt 1 (`2026-07-05-personal-preferences-management-design.md`) — používá jeho registr předvoleb pro odemčení zámku.

## Kontext a motivace

Lišta záložek na detailu projektu ([Detail.cshtml](../../PmTracker.Web/Views/Projekty/Detail.cshtml)) dnes zobrazuje všech 6 cílů vedle sebe: Záznamy · Harmonogram · Jednání · Osoby (tým) · Návrhy · Dashboard. Pro běžnou práci šéf chce lištu zjednodušit na **3 primární** záložky a zbytek schovat za **„+"**. Menšina uživatelů (kdo pravidelně chodí do Návrhů) si má moct menu **trvale rozbalit (zamknout)**, aby neklikali dokola.

**Cíl:**
- Primární (vždy): **Záznamy · Harmonogram · Jednání**.
- Za **„+"**: **Osoby (tým) · Návrhy · Dashboard** (práva zachována).
- Volitelně **trvale rozbalené (zamčené)** = všech 6 inline; stav v cookie; odemčení jen v osobních Předvolbách.

## Rozsah

**V rozsahu:**
- Přeuspořádání tab stripu na primární skupinu + overflow „+" (popover) NEBO 6 inline dle stavu zámku.
- Cookie-řízený počáteční stav (server render, bez fliknutí).
- „+" popover (open/close, outside-click/Escape) — reuse floating-chooser patternu.
- Zámek: přepnutí do `locked` z popoveru; indikátor zámku ve stavu locked s hover hláškou.
- Odemčení přes `menuLockDescriptor` v `preferences/registry.js` (napojení na Sub-projekt 1).

**Mimo rozsah:**
- Změny autorizace (Návrhy/Dashboard práva zůstávají).
- Ostatní projektové akce vpravo (Tisk, Nový záznam, Nové jednání) — beze změny.
- Dashboard jako in-page panel (zůstává odkaz na samostatnou stránku).

## Globální omezení

- **Progressive enhancement:** záložky zůstávají `<a class="tab" data-tab href>` (server-nav funguje i bez JS; JS dělá in-page přepnutí přes `projectTabs.js`).
- **Cookie jako téma:** klientský zápis (`document.cookie … Path=/; SameSite=Lax`), **serverové čtení** (`Request.Cookies`) pro počáteční render.
- **Server = zdroj pravdy pro layout:** přepnutí zámku zapíše cookie a **reloadne** stránku; klient nereplikuje dvě rozvržení v JS (minimum client logiky). Reload je přijatelný — zamčení je vzácná akce.
- ESM modul side-effect importovaný z `bootstrap.js` (`project_bundle_sync`).
- gov-design-system: „+" a zámek = `gov-icon` (Bootstrap icons, `type="components"`, např. `plus-lg`, `lock`, `unlock`).
- České texty. gov jako inspirace, doplnit kde chybí.

## Architektura

### Dva stavy menu (řízené jednou cookie)

Cookie **`pmtracker.projectMenu.locked`** (globální, per-prohlížeč; hodnota `"1"` = zamčeno, jinak/chybí = odemčeno):

**A) Odemčeno (default):** 3 primární inline + tlačítko **„+"** v hlavní řadě. Klik „+" → **popover** s povolenými sekundárními položkami (`<a class="tab" data-tab href>`), zavírá se na outside-click/Escape.

**B) Zamčeno:** všech 6 povolených záložek **inline** (jako před úpravou); „+" se nerenderuje.

**Zámeček = symetrický přepínač ve spodní řadě menu („spodní index"), vpravo, v OBOU stavech** (2026-07-05 upřesnění):
- Odemčeno → ikona **otevřený zámek**; hover ukáže **zavřený** (afordance „kliknutím zamknu"); klik = zapiš cookie `1` + `location.reload()` → varianta B.
- Zamčeno → ikona **zavřený zámek**; hover ukáže **otevřený**; klik = smaž cookie + `location.reload()` → varianta A.
- `title` vysvětluje akci (např. „Zamknout rozbalené menu" / „Odemknout — sbalit menu; jde i v Nastavení ▸ Předvolby").
- Přepínač je malý a vpravo dole, aby nedělal vizuální smog.

Sekundární položky jsou v **obou** stavech gateované právy: Návrhy jen `CanViewProposals`, Dashboard jen `CanViewDashboard`; Osoby (tým) vždy → overflow má vždy ≥1 položku.

### Dva zdroje zápisu cookie

Cookie se přepíná ze **dvou míst** (sdílejí `preferences/cookie.js`, stejná sémantika):
1. **Zámeček ve spodní řadě menu** — obousměrně (zamknout i odemknout) + reload.
2. **Osobní Předvolby** (`menuLockDescriptor`) — smazání řádku = odemknout (delete cookie).

### Serverový render (bez fliknutí)

`ProjektyController.Detail` přečte cookie a nastaví do modelu `bool ProjectMenuLocked`. `Detail.cshtml` podle něj vyrenderuje variantu A nebo B. Aktivní stav sekundární záložky: server zná `Model.ActiveTab`; když ∈ {`tym`, `navrhy`} a stav = odemčeno, „+" dostane `active` třídu a odpovídající položka v popoveru se označí `aria-current`.

### Odemčení v Předvolbách (Sub-projekt 1 hook)

Do `preferences/registry.js` přidat `menuLockDescriptor`: pokud cookie == `"1"`, vrátí 1 položku „Trvale rozbalené projektové menu" s `remove()` = smazání cookie (`Max-Age=0`). Řádek se objeví v seznamu Předvoleb; smazáním se menu při příštím otevření projektu vrátí na sbalené. Žádná změna renderu SP1 — jen nový deskriptor.

## Komponenty a soubory

- **Modify** `Detail.cshtml` — rozdělit tab strip; přidat overflow `+`/popover (varianta A) a inline-6 + zámek (varianta B) dle `Model.ProjectMenuLocked`.
- **Modify** `ProjektDetailViewModel` (`ProjektDetailViewModels.cs`) — `public bool ProjectMenuLocked { get; set; }`.
- **Modify** `ProjektyController.Detail` — `model.ProjectMenuLocked = Request.Cookies["pmtracker.projectMenu.locked"] == "1";`.
- **Create** `PmTracker.Web/wwwroot/js/modules/projectMenu.js` — `initProjectMenuOverflow()`: open/close popoveru (outside-click/Escape) + zámeček-přepínač `[data-project-menu-lock-toggle]` (obousměrně: čte aktuální stav z cookie, zapíše/smaže + `location.reload()`). Hover icon-swap řeší CSS. Side-effect wiring z `bootstrap.js`.
- **Create** `PmTracker.Web/wwwroot/js/modules/preferences/cookie.js` — mini helper `readCookie(name)`, `writeCookie(name, value, maxAgeSeconds)`, `deleteCookie(name)` (SameSite=Lax, Path=/). Sdílí projectMenu.js i menuLockDescriptor.
- **Modify** `preferences/registry.js` — přidat `menuLockDescriptor` (import z `./cookie.js`).
- **Modify** `bootstrap.js` — import + init `initProjectMenuOverflow`.
- **Modify** `site.css` — overflow tlačítko, popover menu, spodní řada se zámkem (right-aligned) + hover icon-swap (dvě `gov-icon` lock/unlock, `:hover` prohodí viditelnost).

## Datový tok

1. **Render projektu:** controller přečte cookie → `ProjectMenuLocked` → view varianta A/B.
2. **Klik „+":** JS otevře popover (A). Outside-click/Escape zavře.
3. **Klik zámeček (spodní řada menu):** toggle — odemčeno → `writeCookie("pmtracker.projectMenu.locked","1", 1 rok)`; zamčeno → `deleteCookie(...)`. Poté `location.reload()` → server vyrenderuje druhou variantu.
4. **Odemčení (Profil ▸ Předvolby):** klik smazat na řádku → `deleteCookie(...)` → řádek zmizí; menu se sbalí při příštím otevření projektu.

## Stavy a chybové situace

- **Bez JS:** varianta A ukáže „+" jako `<a>`/`<details>`-less tlačítko bez funkce, ale všechny záložky mají server-nav `href` — sekundární cíle jsou dostupné přes přímé URL (`?tab=…`). Zamykání je JS-only (přijatelné). Varianta B (locked) funguje plně i bez JS (jen odkazy).
- **Cookie zakázané:** zápis v `try/catch`; degradace = menu prostě zůstane odemčené (zámek se „nechytne", žádný pád).
- **Uživatel bez práv na sekundární:** overflow vždy obsahuje aspoň Osoby (tým); „+" má smysl.

## Testovací strategie

- **Api render (WebApplicationFactory, cookie header — jako `Layout_ShouldRenderServerThemeAttributesFromCookie`):**
  - Bez cookie → detail obsahuje overflow „+" (`data-project-menu-toggle`) a NEobsahuje 6. inline sekundární záložku mimo popover.
  - S `Cookie: pmtracker.projectMenu.locked=1` → detail obsahuje inline sekundární záložky + zámeček-přepínač (`data-project-menu-lock-toggle`) a NEobsahuje „+".
  - Zámeček-přepínač je přítomen v obou stavech (odemčeno i zamčeno).
  - Práva: bez `CanViewProposals` → Návrhy chybí i v popoveru.
- **Unit (source-assertion):** `registry.js` deklaruje `menuLockDescriptor`; `projectMenu.js` má `initProjectMenuOverflow` + outside-click/Escape; `bootstrap.js` ho wiruje.
- **E2E (Playwright):** odemčeno → klik „+" → popover; klik zámek → reload → 6 inline; Profil ▸ Předvolby → řádek „Trvale rozbalené…" → smazat → menu sbalené. (Pozn.: E2E JS-init env caveat — těžiště na Api render + ruční ověření.)
- **TDD:** červený Api render test (overflow „+" ve výchozím stavu) první.

## Otevřené otázky

Žádné — všechny designové otázky zodpovězeny uživatelem 2026-07-05:
1. Bez zámku po reloadu sbaleno; trvalé jen `locked`.
2. Zamčeno = 6 inline; malý zámeček vpravo dole u 6. záložky.
3. Zámeček ve spodní úrovni menu („spodní index").
4. **Upřesnění:** zámeček je **symetrický přepínač v obou stavech** (odemknout i zamknout přímo z menu), s **hover icon-swap** (ukáže opačnou ikonu). Odemčení jde **i** z osobních Předvoleb → cookie se zapisuje **ze dvou zdrojů** (menu + Předvolby, sdílejí `preferences/cookie.js`).

---

## Revize 2026-07-07 — Option 2 „Vždy ukázat aktivní tab" (nahrazuje popover overflow)

User zpětná vazba: (a) menu se centrovalo (badge stavu recykloval `.project-status-inline{margin-left:auto}`
→ dva auto-marginy → wrong); (b) „+" popover s klik-rozbal-klik-shrň + outside-close byl otravný; ve
sbaleném stavu se po přechodu na Osoby/Návrhy ztratilo podtržení aktivního tabu (byl schovaný v popoveru).

**Zvolený model (Option 2):**
- Přepnutí tabu je **plná navigace** (server round-trip) — persistence rozbalení pro podtržení netřeba.
- **Aktivní sekundární tab je VŽDY inline** (server renderuje `.tab-secondary.active`), takže podtržení
  je vidět i ve sbaleném stavu. „+" odkrývá jen **neaktivní** sekundární inline „do boku".
- „+" = **jednosměrné** rozbalení (`.is-expanded` třída, JS), **bez** outside-close/Escape. Reload/navigace
  ho přirozeně sbalí (aktivní tab zůstává). „+" se renderuje jen v odemčeném stavu.
- **Zámeček** = trvalé rozbalení (roční cookie), jako **spodní index** vpravo dole u posledního tabu;
  viditelný až po rozbalení (CSS). Zamčeno = server rovnou `.is-expanded`, „+" se nerenderuje.

Markup: `.tab-secondary-group[data-project-menu-group]` (Detail.cshtml). CSS: `.tab-secondary-group`,
`:not(.is-expanded) .tab-secondary:not(.active){display:none}`. JS: `projectMenu.js` (`is-expanded`, bez Escape).
Popover (`.tab-overflow-menu`, `data-project-menu-popover`, `.tabs-lockrow`) **odstraněn**.
