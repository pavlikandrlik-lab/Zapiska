# A8 — Aplikační dialog při šipce zpět + ověření breadcrumb návratu (rozpracovaný editor)

**Datum:** 2026-07-08 · **Stav:** schváleno uživatelem (analýza 2026-07-08; doplněk: pohlídat i návrat z breadcrumb lišty)

## Problém a kontext okolí

Stránkové editory (editace/založení záznamu, založení/schválení návrhu — všechny `EditZaznamPage.cshtml` s `form[data-record-editor-form][data-record-editor-presentation="page"]`) mají dirty-guard:
- Tlačítko „Zrušit a vrátit se" → aplikační dialog (`promptRecordEditorDiscard`).
- **In-app odkazy** (menu, logo, breadcrumbs) → `maybeGuardOutboundNavigation` (bootstrap.js:461): každý `<a href>` klik při dirty formu → preventDefault + aplikační dialog → `window.location.assign` až po potvrzení. Vynechává `#`/`mailto:`/`download`/`target≠_self`.
- **Nativní `beforeunload` byl záměrně odstraněn** (`handleWindowBeforeUnload` je prázdný) — proto dřívější kolize „aplikační + browser dialog naráz" už nehrozí.
- **Šipka zpět prohlížeče: žádná ochrana** — navigace není click, guard se nespustí (user report ✓).

Breadcrumb lišta: ← (`.app-breadcrumb-back`), odkazy a ✕ (`.app-breadcrumb-close`) jsou obyčejné `<a href>` bez `target` → **měl** by je krýt existující guard. Bod od usera „pohlídat i dotaz z breadcrumb lišty" = ověřit reálné chování a pokrýt testem; pokud kdekoli neguarduje (např. handler větev s časnějším `return`), opravit. Pozn.: guard běží až PO print/attendance/... větvích delegovaného handleru — breadcrumb odkazy žádnou z nich nematchují, pořadí je OK.

## Řešení — pushState-trap pro šipku zpět

1. **Aktivace trapu:** modul `recordEditor` při prvním „zašpinění" formuláře (existující dirty tracking `isRecordEditorFormDirty`) zavolá `history.pushState({ pmEditorTrap: true }, "", location.href)` — vytvoří sentinel entry (URL se nemění).
2. **popstate handler:** šipka zpět → popstate ve stejném dokumentu (žádný browser dialog, beforeunload je prázdný):
   - form dirty & ne-navigating → zobrazit `promptRecordEditorDiscard`:
     - „Zůstat" → `history.pushState(sentinel)` znovu (trap obnoven),
     - „Odejít" → nastavit `recordEditorNavigating=true` a `history.back()` (odejde na skutečnou předchozí stránku).
   - form čistý → rovnou `history.back()` (uživatel projde bez dotazu).
3. **Úklid:** po uložení (submit success redirect) a po potvrzeném odchodu se trap nesmí obnovovat; případná zbývající sentinel entry je neškodná (stejná URL), ale při programové navigaci po uložení preferovat `location.replace`, ať v historii nezůstane dvojice. Ověřit interakci s `syncTabQuery`/`replaceState` používaným jinde (projectTabs) — editor je samostatná stránka, kolize se nečeká, ale testem pokrýt návrat na detail projektu.
4. **Breadcrumb větev:** E2E ověřit ←/odkaz/✕ z breadcrumbs při dirty formu → dialog; při čistém formu → přímý odchod. Opravit, jen pokud test odhalí díru.

## Poctivé limity (zdokumentovat i v kódu)
- Trap kryje 1 krok zpět; delší podržení tlačítka zpět (výběr z historie) může trap přeskočit — best effort, bez nativního beforeunload nelze víc.
- Zavření tabu/okna ochráněno není (záměr — nativní dialog nechceme).
- BFCache: sentinel je same-document, obnovení stránky trap resetuje spolu s dirty stavem (form je po loadu čistý) — konzistentní.

## Dotčené soubory
- `PmTracker.Web/wwwroot/js/modules/recordEditor/form.js` (dirty tracking hook + trap lifecycle)
- `PmTracker.Web/wwwroot/js/modules/bootstrap.js` (registrace popstate handleru vedle stávajících listenerů)
- Testy E2E + Unit source-assertion

## Akceptační kritéria
- Dirty editor + šipka zpět → aplikační dialog; „Zůstat" = zůstane (obnovený trap funguje i na druhý pokus); „Odejít" = předchozí stránka. Žádný nativní browser dialog.
- Čistý editor + šipka zpět → normální odchod bez dialogu.
- Dirty editor + breadcrumb ←/odkaz/✕ → aplikační dialog (stejné volby).
- Uložení záznamu → následná šipka zpět NEvrací na editor-sentinel (žádné „mrtvé" kliknutí).
- Platí pro všechny stránkové editory (záznam, návrh, schválení návrhu).

## Testy
- E2E (Playwright): scénáře z kritérií (dirty→back→zůstat→back→odejít; breadcrumb varianty; po uložení).
- Unit source-assertion: popstate handler registrován; trap se aktivuje z dirty hooku, ne na load.

## Mimo scope
Ochrana zavření tabu; modaly (nejsou stránky); vícekrokový history výběr.
