# Úpravy inbox — Ranní review 2026-04-20

> **Pravidlo:** Pouze sbírám položky, **NEOPRAVUJI**. User pošle "oprav to" (nebo ekvivalent) → teprve pak systémově rozhoduji o společné implementaci.

**Branch:** `codex/senior-refactor-fase-1`
**HEAD při zahájení inboxu:** `ee05e3b`

## Položky

<!-- Přidávám čísluji a přidávám metadata:
     - **Kde:** URL / view / modul
     - **Co:** popis chování
     - **Očekávání:** jak se to má chovat
     - **Status:** 🆕 nová / 🔍 ověřeno / ⏸ čeká na detail
     - **Komentář:** hypotéza root cause / vazba na jiné položky
-->

### Úprava #1 — Projektová záložka Jednání: sbalit historické roky

- **Kde:** `/Projekty/Detail/{id}?tab=jednani` → view `PmTracker.Web/Views/Projekty/_ProjectMeetingsTab.cshtml`
- **Co (současný stav):** Historické roky jsou v `state="open"` (rozbalené, karty všechny viditelné). Dáno atributem `data-meeting-history-default="open"` a `isPreviewYear ? "preview" : "open"`.
- **Očekávání:** Sbalit historické roky defaultně (stejné chování jako aplikační záložka `/Jednani/Index`). Pouze aktuální rok zůstane v `preview`, historické → `collapsed`.
- **Status:** 🆕 nová — spec change
- **Komentář / dopady:**
  - Spec `docs/specs/meetings-year-grouping.md` — celý oddíl "Projektová záložka" musí být přepsán (tabulka default stavů, důvod, markup příklad).
  - Test `MeetingsYearGroupingTests.ProjectJednaniTab_ShouldDefaultHistoricalYearsToOpen` **se zamění** na `..._ShouldDefaultHistoricalYearsToCollapsed` — asserce změní z `"open"` na `"collapsed"` (atribut i ternární výraz).
  - JS `meetingOverview.js` **nepotřebuje změnu** (toggle logika je symetrická mezi stavy).
  - CSS **nepotřebuje změnu**.
  - Commit `db2560d` (Pá 17.04) původně zaváděl rozdílné chování mezi tab typy — tato úprava ho zjednoduší na "historické roky vždy collapsed" napříč celou appkou.
  - Root-cause hypotéza: pravděpodobně původní UX rozhodnutí z db2560d bylo špatné; unified collapsed chování dává smysl (konzistence).

### Úprava #2 — Globální Jednání: projektová karta defaultně skrývá historické roky za toggle

- **Kde:** `/Jednani/Index` → view `PmTracker.Web/Views/Jednani/Index.cshtml`
- **Co (současný stav):** V kartě projektu (`.meeting-project-overview`) jsou v `meeting-year-stack` **všechny roky** v DOMu. Aktuální rok je `preview` (s rozbalovacím chevronem uvnitř year-group), historické jsou `collapsed` — ale **záhlaví historických roků je stále viditelné** (jen jejich karty jsou `hidden`). User to vnímá jako "vidím další roky" i když jsou karty schované.
- **Očekávání:** V kartě projektu vidět **pouze aktuální rok** (celý `meeting-year-group` s preview). Všechny historické year-groups jsou skryté za **novým project-level toggle tlačítkem** (typ. "Starší roky ▼" / "Zobrazit další roky"). Kliknutí na toggle → zobrazí se historické year-groups (každý stále jako collapsed s vlastním chevronem pro rozbalení jednání roku).
- **Pattern:** vnořený expand — stejný vzor jako year-toggle, ale o úroveň výš (project-level vs. year-level).
- **Status:** 🆕 nová — chybí UI primitive (project-level toggle)
- **Komentář / dopady:**
  - **View (Razor):** `Views/Jednani/Index.cshtml` — přidat `<button data-project-history-toggle>` + wrapper pro historické year-groups (např. `<div data-project-history-body hidden>`). Aktuální rok zůstane mimo wrapper (vždy viditelný).
  - **Spec:** `docs/specs/meetings-year-grouping.md` — nová sekce "Project-level history toggle" popisuje strukturu. V kombinaci s Úpravou #1 mění pohled na celý year-grouping design.
  - **JS (`meetingOverview.js`):** nová funkce `toggleProjectHistory(toggleEl)` analogická k `toggleMeetingYearGroup`. Přidat do init listeneru v `bootstrap.js` delegaci na `[data-project-history-toggle]`. Chevron swap stejnou technikou (`chevron-down` ↔ `chevron-up` swap `name` atributu gov-icon — NE CSS rotate kvůli artefaktům).
  - **CSS:** nové selektory `.meeting-project-history-toggle`, `.meeting-project-history-body` — typografie/padding konzistentní s `.meeting-year-toggle`.
  - **Tests:** `MeetingsYearGroupingTests` — nový test `GlobalJednaniIndex_ShouldHideHistoricalYearGroupsBehindProjectLevelToggle` (contains `data-project-history-toggle`, historical year-groups uvnitř `[data-project-history-body][hidden]`).
  - **Projektová záložka `/Projekty/Detail?tab=jednani`** (Úprava #1): **nedotčena tímto toggle** — protože tam je jen jeden projekt. Ale pokud Úprava #1 sbalí historické roky, bude bez project-level toggle konzistentní (každý historický rok má svůj vlastní chevron). **Rozhodnout při opravě:** chceme na projektové záložce také project-level history toggle? Pravděpodobně ne — user nespecifikoval. Default: **jen globální záložka dostává project-level toggle**, projektová záložka zachová prosté year-groups.
  - **Dopad na `PreviewRok` logic:** žádný. `MeetingYearGroupBuilder.ResolvePreviewYear` zůstává stejný; jen markup v Razor se změní (wrapper kolem non-preview year-groups).
  - Root-cause: Úprava #2 není bug opravou, ale UX vylepšením — současné collapsed yearů bylo zavedeno v db2560d; user teď chce o úroveň víc skrývat.

### Úprava #3 — Analýza: jsou všechny actions mapované na permission keys? (audit + návrh sjednocení)

- **Kde:** celá aplikace (controllers, services, autorizační pipeline)
- **Co (podezření):** User si myslí, že **ne všechny actions** (MVC endpoints, příp. i service-level operace) mají explicitně přiřazený permission key z `PermissionKeys`. Pravděpodobně se někde kontrola permissionu vynechává nebo je nahrazena jen `CanAccessProject(projectId)` apod., což je slabší než per-akční check.
- **Očekávání:** **Sjednotit** — každá action (a ideálně i každá write-command v service vrstvě) má explicitní permission key check. Chybějící check = bug (tichý security hole) nebo úmyslná výjimka dokumentovaná v kódu.
- **Typ úpravy:** 🔍 **audit + design + decision**, ne jednorázový fix. Není fixovatelné v rámci systémové opravy inboxu — vyžaduje separátní fázi.
- **Status:** 🆕 nová — vyžaduje před-fixovou analýzu
- **Rozsah analýzy potřebný před rozhodnutím:**
  1. **Inventář všech MVC actions** napříč controllery — ProjektyController (31 actions, teď 5 partials), ZaznamyController (13 actions), JednaniController, NavrhyController, NastaveniController, ExportController, CiselnikyController, AuthController, HomeController, atd. Tabulka: action → permission key (nebo "NONE").
  2. **Inventář všech permission keys** v `Services/Security/PermissionKeys.cs` — kompletní enum hodnot.
  3. **Inventář kontrol v kódu** — grep `HasPermission(PermissionKeys.*)`, `[Authorize(Policy=...)]`, `CanAccessProject(...)`, `CanDelete...`, apod. Mapovat, kde se čeho kontroluje.
  4. **Inventář services** (write paths) — `SaveRecordAsync`, `SaveProjectAsync`, `ApproveProposalAsync`, `SaveHarmonogramStepRowAsync`, atd. — mají interně permission check nebo spoléhají na controller? Double-guard by byl defense-in-depth.
  5. **Gap analysis** — kandidáti bez explicitního permission key.
  6. **Návrh sjednocení** — možné přístupy:
     - (a) **Controller-level `[Authorize(Policy="...")]` atributy** mapované na permissions (čistý ASP.NET Core pattern)
     - (b) **Konzistentní `CurrentUserContext.HasPermission(key, projectId)` uvnitř action body** (current dominantní pattern)
     - (c) **Hybrid** — mandatory attribute + runtime check pro per-project scoping
     - (d) **Service-level authorization gate** (přenést kontrolu do service layer → defense-in-depth + consistent CLI/API/MVC coverage)
  7. **Decision** — user vybere (a/b/c/d) na základě návrhu.
- **Komentář / vazby:**
  - Potenciální **security issue** — skrytý risk že nějaká action je otevřená/pod-autorizovaná.
  - Vazba na **Fázi 3D** (ProjektyController split do 5 partials) — audit může naopak těžit z už rozbitého kódu (lehčí grep per partial).
  - Vazba na **Fáze 3C/Services** — pokud se rozhodneme pro (d) service-level, ovlivní to partial class patterny (`RecordService.*`).
  - **Výstupní artefakt:** `docs/superpowers/specs/2026-04-2X-authorization-audit.md` (audit + návrh) + implementační plán `docs/superpowers/plans/2026-04-2X-authorization-unification.md` → separate fáze (ne v rámci ranní systémové opravy).
  - Není na main-line inbox fixu — **TODO po ranní opravě základních UX úprav**.

### Úprava #4 — User Dashboard: full-width + non-scrollable layout + unified expand button

- **Kde:** User dashboard (pravděpodobně `Views/Home/Index.cshtml` nebo `Views/Home/Dashboard.cshtml` — ověřit při opravě) + sekce Focus / Meetings / News
- **Co (současný stav):**
  1. Dashboard má omezenou šířku (container), stránka je scrollovatelná při větším počtu položek
  2. Sekce mají tlačítka **"Načíst více"** i **"Zobrazit více"** (duplicitní pattern, user chce sjednotit)
- **Očekávání:**
  1. **Full-screen šířka** dashboardu — bez centrálního containeru limitujícího šířku, využít celou obrazovku
  2. **Non-scrollable** (fit-on-screen) — výška dashboardu = `100vh` (nebo dostupná výška viewportu mínus header), sekce uvnitř řeší přetečení vlastními internal scroll/pagination
  3. **Všechna tlačítka "Načíst více" smazat** — zůstane pouze **"Zobrazit více"** (expand to full). Unified CTA.
- **Status:** 🆕 nová UX úprava
- **Komentář / dopady:**
  - **View:** najít dashboard view, přepnout z `.container` na full-width wrapper; upravit `max-width`/`padding` v `.dashboard-*` CSS selektorech
  - **CSS:** nový layout — CSS Grid s `grid-template-rows: auto 1fr 1fr 1fr` (header + 3 stejně vysoké sekce) nebo flex s `min-height: 0` + `overflow: auto` per sekce
  - **JS:** pagination/lazy-load logika pro "Načíst více" musí být **odstraněna** (ne disabled, smazat). Vyhledat: `data-load-more`, `loadMore`, `načíst více`
  - **Button text:** všechny instance "Načíst více" → smazat; "Zobrazit více" → ponechat (pravděpodobně otevírá modal/detail stránku)
  - **Related:** user chce později **full-width pro celou aplikaci** — to je separátní follow-up (Úprava #4b), nejprve user-dashboard jako proof of concept
  - **Architectural risk:** non-scrollable dashboard musí správně reagovat na malé viewporty (tablet/mobile). Možná mediaquery fallback na scrollable pod breakpointem.

### Úprava #5 — Dashboard News: obohatit záznamy o základní kontext

- **Kde:** News sekce user dashboardu (součást #4)
- **Co (současný stav):** News zobrazuje pouze typ události, např. "Změna úkolu" — žádný další kontext (co se změnilo, na čem, od koho).
- **Očekávání:** Po rozšíření (full-width z #4) se vejde více info. Přidat **aspoň základní info**: např. "Změna úkolu č. 42 — Pavel Andrlík změnil termín z 20.04 na 25.04 v projektu PMT". Konkrétní tvar záleží na typu události.
- **Status:** 🆕 nová — feature enhancement
- **Komentář / dopady:**
  - **Závisí na Úprava #4** — čeká na full-width layout
  - **Typy událostí:** najít generátor news (pravděpodobně service v `Services/Dashboard/` nebo `Services/Audit/`). Inventarizovat typy události + kontext dostupný z audit log / entity snapshot.
  - **ViewModel:** `DashboardNewsItemViewModel` (nebo ekvivalent) rozšířit o kontext fieldy (ProjectName, RecordTitle, ActorName, ChangeSummary, atd.)
  - **Template:** Razor partial pro news item — přidat context řádek
  - **Localization:** cs-CZ věty ve správném formátu
  - **Truncation:** pokud se delší text nevejde, ellipsis + tooltip / hover

### Úprava #6 — Action buttons → ikony

- **Kde:** detail záznamu (editor), panel vyjádření (comments)
- **Co (současný stav):** textová tlačítka:
  - **"Upravit"** (u záznamu/vyjádření)
  - **"Navrhnout termín a harmonogram"** (u záznamu)
  - **"Upravit"** + **"Smazat"** (u vyjádření/comment)
- **Očekávání:** nahradit texty **ikonami** (gov-icon) s `aria-label` + tooltip. Konkrétní ikony (návrh, ale diskutovat):
  - Upravit → `edit` / `pencil`
  - Navrhnout termín a harmonogram → `calendar-edit` / `schedule` (nejasné — vybrat dle gov-icon setu)
  - Smazat → `trash` / `delete`
- **Status:** 🆕 nová UX / vizuální úprava
- **Komentář / dopady:**
  - **Accessibility:** `aria-label` nutný (ikona sama nenese sémantiku). `<gov-button variant="plain" aria-label="Upravit">` + `<gov-icon slot="left" name="pencil">`
  - **Tooltip:** gov-design-system má tooltip provider? Ověřit. Pokud ne, `title` atribut fallback.
  - **Views dotčené:** pravděpodobně `_ZaznamCard.cshtml` / `_RecordEditorActions.cshtml` / `_VyjadreniCard.cshtml` — ověřit při opravě
  - **Risk:** ikonová tlačítka bez popisku snižují discoverability pro netrained uživatele. Tooltip + first-use hint doporučeno.
  - **Dopad na responzivní layout:** ikony jsou menší → víc se vejde, což pomáhá #4 (více info na stránku).

### Úprava #7 — Floating picker panely: výsledky vyhledávání se zobrazují ZA modalem (stacking/z-index, NE funkční bug)

- **Kde (rozšířený rozsah — všechny modaly používající person/autocomplete picker):**
  - **Modal AD vyhledávání** (původně nahlášeno)
  - **Modal "Přidat projektovou roli"** — picker vyhledávání osoby/role
  - **Modal "Přidat subsystémovou roli"** — picker vyhledávání
  - Potenciálně další: "Přidat člena týmu", "Přiřadit vlastníka" apod. — audit při opravě
  - Technické soubory: `Views/Shared/_AdPersonSearch*.cshtml`, `_AddProjectRoleModal.cshtml`, `_AssignProjectSubsystemRoleModal.cshtml` + `wwwroot/js/modules/pickers/` (adPerson.js, person.js — po 3B split)
  - Shared infrastructure: `#floating-panel-root` v `_Layout.cshtml` + `ui/print.js` `registerFloatingChooser`
- **Co (současný stav):** **Vyhledávání samo funguje** (dotaz se pošle, výsledky přijdou). Ale **floating panel s výsledky se vykreslí do globálního `#floating-panel-root` v layoutu** — což je light DOM na úrovni stránky, **vizuálně ZA gov-dialog modalem** (gov-dialog má vlastní shadow DOM stacking context s vyšším z-index než elementy v layoutu).
- **User formulace:** "výsledky se zobrazují za modalem místo pole globální vyhledávání v aplikaci" — tj. panel vyrenderuje svůj obsah v layout-level portalu (kde je i globální app search bar), ne nad modalem.
- **Očekávání:** Výsledkový panel musí být **vizuálně nad** modalem (nad gov-dialog content i backdrop) — bez ohledu na to, ve kterém modalu picker běží.
- **Status:** 🐛 visual/stacking bug (funkce OK, viditelnost KO) — **systémový** cross-modal problém
- **Root-cause poznámky (rozšířené):**
  - Fáze 2E Task 3 (`a3bba6b`) přesunula `#floating-panel-root` MIMO gov-dialog kvůli shadow DOM kolizi. Tím se vyřešila jedna třída bugů, ale vznikl tento (stacking).
  - **Systémový fix:** 2 varianty — (a) zvýšit z-index globálního `#floating-panel-root` nad gov-dialog shadow DOM z-index (může být netriviální, pokud gov-dialog používá interně `position: fixed + z-index: max-int`), (b) dynamicky **detekovat open modal** a přemístit floating-root DOVNITŘ modalu (jako child `gov-dialog`) — zpět do "inside modal" strategii pro čas, kdy je modal open. Po zavření modalu vrátit zpět.
  - Varianta (b) elegantnější, ale vyžaduje detekci open modal a insertion point switch v `ui/print.js` `registerFloatingChooser` / picker init.
  - **Scope:** fix se musí aplikovat jednou a platit pro všechny pickery (AD, person, date, time) — všechny používají sdílený `#floating-panel-root`.
- **Komentář / dopady (root-cause hypothesis):**
  - `gov-dialog` má shadow DOM s vlastním stacking contextem (pravděpodobně `z-index: 10000` nebo podobné). Floating panel picker renderuje do `#floating-panel-root` v `_Layout.cshtml` (Fáze 2E Task 3 — shadow DOM kolize s gov-dialog), který je v light DOM.
  - Pokud `#floating-panel-root` má nižší z-index než gov-dialog backdrop/content, panel se skryje za modal.
  - **Hypotéza fixu:** zvýšit z-index `#floating-panel-root` nad gov-dialog stacking context, NEBO přemístit picker portal do uvnitř gov-dialogu (přes slot) při otevřeném modálu.
  - **Vazba na Fáze 2E Task 3** — tehdy se floating-root přesouval mimo gov-dialog kvůli jinému shadow DOM problému. Tento fix může být regression z té úpravy. Commit `a3bba6b` — ověřit diff.
  - **Similar picker types:** date-picker, time-picker, person-picker — stejný fix pro všechny (pickers use shared floating-root).
  - **CSS / JS?:** může být obojí — CSS pro z-index, JS pokud je portal insertion point potřeba změnit dynamicky podle "am I inside a modal" detection.
  - **Test pokrytí:** architecture test pro z-index stacking by byl tricky. Spíše E2E Playwright smoke (pm-modal-harness rozšířit o simulovaný person-search).

### Úprava #8 — Modal "Přidat ručně": křížek (X) nezavírá, jen tlačítko Zrušit

- **Kde:** Modal pro ruční přidání osoby (pravděpodobně `Views/Ciselniky/_ManualPersonAddModal.cshtml` nebo `Views/Shared/_AddPersonManually.cshtml` — ověřit)
- **Co (současný stav):** Křížek `×` v horním rohu gov-dialogu nezavře modal. Funguje pouze tlačítko "Zrušit" uvnitř formulářových akcí.
- **Očekávání:** Křížek (gov-close event) musí modal korektně zavřít, stejně jako "Zrušit" button.
- **Status:** 🐛 reálný bug — regrese z Fáze 2E gov-dialog migrace
- **Komentář / dopady (root-cause hypothesis):**
  - Phase 2E zavedla `block-close="true"` + `block-backdrop-close="true"` — X v headeru nezavře gov-dialog sám, ale emituje `gov-close` event.
  - `bootstrap.js` má `handleGovCloseEvent` (commit `a3bba6b`) který chytne gov-close a zavolá close flow — pokud je to record-editor s dirty-check. **Pro non-record-editor modaly (jako "Přidat ručně") dirty-check neudělá nic a close flow neběží.**
  - **Hypotéza fixu:** gov-close listener musí fallbackovat na `closeModal()` když modal není record-editor (nemá `[data-record-editor]` atribut nebo ekvivalent).

### Úprava #9 — Modal "Vyhledat v AD": křížek (X) nezavírá + modal je scrollovatelný

- **Kde:** Modal AD vyhledávání — pravděpodobně `Views/Shared/_AdPersonSearchModal.cshtml` nebo součást picker
- **Co (současný stav):**
  1. **Křížek nezavírá modal** — stejný bug jako #8 (non-record-editor gov-close fallback chybí)
  2. **Modal je scrollovatelný** — content přetéká výšku modalu a generuje internal scrollbar
- **Očekávání:**
  1. Křížek funguje (stejný fix jako #8 — jeden společný handler pro všechny non-record-editor modaly)
  2. Modal **NENÍ scrollovatelný** — výška obsahu se musí přizpůsobit viewportu. Když by content přetekl, mají se skrývat méně důležité části nebo má být obsah layoutován jinak (např. list jako internal scrollable region uvnitř neposouvatelné obálky)
- **User governance pravidlo:** "modaly by neměly být scrollovatelné bez opravdu závažných důvodů" — zavést jako **general UX constraint** napříč aplikací
- **Status:** 🐛 bug (část 1) + 🟡 UX constraint (část 2)
- **Komentář / dopady:**
  - **Část 1** (zavírání křížkem) → řešitelné společně s #8 — jeden cross-cutting fix v `bootstrap.js` handleGovCloseEvent
  - **Část 2** (non-scrollable) → architectonický princip. Dopady:
    - Review **všech modalů** (i new-project, edit-meeting, new-zaznam, proposal editor, ...) — zda některé mají overflow scroll a jsou změnitelné do layout-fit pattern
    - **Record-editor modal** je výjimka — často má form delší než viewport. Pro něj ponechat overflow-y:auto uvnitř content area, ale modal-chrome (header/footer) sticky. Je to asi ten "opravdu závažný důvod" z user formulace.
    - **AD search modal** — content: search input + result list. Fix: list získá vlastní `overflow-y: auto` + `max-height: 60vh` apod., modal body jako celek zůstane non-scrollable.
  - **CSS:** `gov-dialog[data-modal-container] .modal-content` — audit současných rules. Příp. zavést `data-modal-scrollable="true"` flag pro explicitní výjimky (record-editor).
  - **Spec artefakt:** `docs/specs/modal-layout-rules.md` — nová specifikace kdy scrollable OK.

### Konsolidace bugů #7 + #8 + #9 — všechny souvisí s Fází 2E gov-dialog

Společný root-cause pattern: Fáze 2E přepnula modaly z custom overlay na gov-dialog Web Component se shadow DOM. Tři samostatné symptomy (z-index kolize #7, gov-close fallback chybí #8+#9 část 1, overflow layout #9 část 2), ale při systémové opravě vyřešit konzistentně:

1. **Close-handler unification** — jeden `handleGovCloseEvent` pro všechny modaly (record-editor s dirty-check, ostatní přes přímý `closeModal()`)
2. **Floating portal stacking** — `#floating-panel-root` z-index nad gov-dialog shadow DOM
3. **Modal overflow policy** — default `overflow: hidden`, opt-in scroll jen pro record-editor přes data-flag

### Úprava #10 — Modal úprav záznamu (record-editor): rozšířit ✅ VYŘEŠENO 2026-04-21

- **Kde:** Modal pro edit/create záznamu (`_EditZaznamForm.cshtml`) — render v `gov-dialog[data-modal-variant="record-editor"]`
- **Co (současný stav):** Modal je vizuálně úzký (~52rem), user má pocit těsného layoutu. Uživatel cítí, že se tam nevejde vše pohodlně.
- **Očekávání:** Větší šířka modalu — ideálně ~1280px (nebo širší) pro record-editor varianta.
- **User governance:** DEFERRED do 2026-04-21, pak implementováno po dokončení fluid tier layoutu.
- **Status:** ✅ **VYŘEŠENO 2026-04-21** — root cause: gov-dialog 4.2.9 čte `--max-width` (ne `--gov-dialog-max-width`), max-height hardcoded 75vh override jen přes přímý class selector.
- **Fix (commit b…):**
  1. `--gov-dialog-max-width` → `--max-width` (wide: 1100px, record-editor: 1280px)
  2. Nový selector `gov-dialog[data-modal-variant="record-editor"] .gov-dialog__dialog { max-height: 92vh }`
  3. Mobile media query: stejný pattern pro `max-height: calc(100vh - 36px)`
- **Verifikace (Playwright probe, viewport 1920×1080):** wide 840×810 → 1100×810, record-editor 840×810 → **1280×994** (92vh)
- **Architecture guard:** `GovDialogVariantWidthTests` — 3 testy (obsolete var not used, wide uses --max-width, record-editor uses --max-width + .gov-dialog__dialog max-height override)
- **Komentář / dopady — KRITICKÁ VAZBA na pre-existing CSS issue:**
  - **Pre-existing bug (zdokumentován v review):** `gov-dialog[data-modal-variant="record-editor"]` má v CSS `--gov-dialog-max-width: 1280px`, ale vizuálně modal zůstává defaultní ~52rem. **Custom property se neaplikuje** — pravděpodobně mismatch názvu CSS custom property s aktuální verzí gov-design-system (shadow DOM ho nečte).
  - Playwright harness (pm-modal-harness.html) to potvrdil: wide (1100px) i record-editor (1280px) varianty vykreslují stejně široko jako default.
  - **Implikace:** kdyby se tento CSS var fix udělal, modal by byl **okamžitě** 1280px široký bez dalších změn — což je přesně to, co user chce.
  - **Fix options:**
    1. **Zjistit správné custom property name** pro aktuální verzi gov-design-system (prohrabat gov-design-system dist CSS / shadow DOM rules)
    2. **Přeskočit CSS var** a stylovat přímo přes `::part()` selector pokud gov-dialog poskytuje ::part pro root
    3. **Přeskočit gov-dialog max-width** a aplikovat width přímo na `gov-dialog[data-modal-variant="record-editor"] { width: 1280px; max-width: 96vw; }` (může kolidovat se shadow DOM styly)
  - **Rozhodnutí odložit do ranní diskuse:** user chtěl nejdřív full-width celou app → pokud se tam nasadí max-width:none s internal layout, record-editor by možná mohl být přímo "full viewport" variant místo fixní šířky.
  - **Rozlišit od #4:** #4 je dashboard (full viewport), #10 je modal (dialog nad obsahem). Technicky jiné. Ale strategicky souvisí — oba jdou "víc místa pro obsah".
  - **Plán:** při ranní systémové opravě **NEfixovat přímo**. Místo toho: (a) ověřit root cause CSS var nefunguje, (b) navrhnout 3 varianty řešení + diskuse, (c) decide až po rozhodnutí #4 full-width scope.

---

### Úprava #11 — Broken icon references v ranní fix (pencil + calendar-clock) ✅ VYŘEŠENO

- **Kde:**
  - `PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml:82` — `<gov-icon name="pencil" type="basic">` (Upravit záznam)
  - `PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml:95` — `<gov-icon name="calendar-clock" type="basic">` (Navrhnout termín/harmonogram)
  - `PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml:75,83` — `<gov-icon name="pencil|trash" type="basic">` (edit/delete komentář)
- **Co (bývalý stav):** Icon-only tlačítka na .record-actions panelu ukazovala prázdné bílé rámečky. Network 404 na `/assets/icons/basic/pencil.svg` a `/assets/icons/basic/calendar-clock.svg`.
- **Root cause:** `type="basic"` hledal ikony ve složce `/wwwroot/assets/icons/basic/` která neexistuje. Všechny ikony žijí v `/components/`. `calendar-clock.svg` neexistoval ani v components — použit byl tedy `plus` jako ikona pro "nový návrh".
- **Status:** ✅ **VYŘEŠENO** 2026-04-20 — Playwright re-verify: 0 × 404, ikony vizuálně viditelné (`/tmp/pm-after-fix-zaznamy.png`).
- **Commity:** ještě neuncommited; bude v bundle commitu
- **Provedené změny:**
  1. `_ZaznamPartial.cshtml:82` pencil type="basic" → `type="components"`
  2. `_ZaznamPartial.cshtml:95` calendar-clock type="basic" → `plus type="components"` (calendar-clock v gov 4.2.9 neexistuje)
  3. `_ZaznamCommentsPartial.cshtml:75` pencil type="basic" → `type="components"`
  4. `_ZaznamCommentsPartial.cshtml:83` trash type="basic" → `type="components"`
  5. `DashboardActionButtonsTests.ZaznamPartial_ProposeButton_ShouldBeIconOnly` — expectace změněna z `calendar-clock` na `plus` s komentářem proč
  6. Nový `PmTracker.Tests.Unit/Layout/GovIconReferencesTests.cs` — regression guard validuje každý `<gov-icon name=X type=Y>` v Views proti existujícímu souboru v `wwwroot/assets/icons/{type}/{name}.svg`
- **Komentář:**
  - Ranní fix úprava #6 ikony zavedla s chybným `type="basic"` — nebyla architecture test pro existenci souboru. Nově doplněný `GovIconReferencesTests` to zachytí do budoucna.
  - Existující `meeting-year-chevron` používal správně `type="components"` — precedent byl, ranní fix ho ignoroval.

---

### Úprava #12 — aria-label na icon-only buttons ❌ FALSE POSITIVE

- **Původní pozorování:** Playwright detekce hlásila že icon-only `<gov-button>` nemají `aria-label` (pouze `title`).
- **Verifikace:** Raw server HTML má `aria-label="Upravit"` ✓. Gov-button web component při hydraci přesouvá `aria-label` z host elementu do vnitřního `<button class="element" aria-label="Upravit">` (viz deep-dive `/tmp/playwright-aria-check.js`). Screen readers čtou z vnitřního button → accessibility **je správná**.
- **Status:** ❌ **FALSE POSITIVE** — Úprava není nutná. Detection script se díval na špatný element.
- **Lesson learned:** `gov-button.getAttribute('aria-label')` vrací null po hydraci, ale `gov-button.querySelector('button.element').getAttribute('aria-label')` vrací skutečnou hodnotu. Future Playwright testy: audit aria na `.element` child, nebo přes `page.locator('gov-button').first().getByLabel('Upravit')`.

---

### Úprava #13 — Subsystém reorder "Dolů" jde místo dolů nahoru (gov-button name/value propagation)

- **Kde:** [_ProjectTeamTab.cshtml:145,154](PmTracker.Web/Views/Projekty/_ProjectTeamTab.cshtml#L145-L154) — reorder forms v týmové tabulce projektu
- **Co (současný stav):** Nahoru funguje, Dolů taky jde nahoru.
- **Root cause (2026-04-21, ověřeno Playwright):**
  - Markup: `<pm-button name="Direction" value="up|down" native-type="submit">` — atributy `name` a `value` se na `<gov-button>` host elementu správně zachovají
  - ALE: při hydraci gov-button přesune `name` na vnitřní `<button class="element">`, zatímco **`value` se nepropaguje** (inner button: `name="Direction" value=null type="submit"`)
  - Při submitu form pošle `Direction=` (prázdná hodnota) → ASP.NET model binding nedodrží prázdný string a použije default z property: `Direction = ProjectSubsystemReorderDirections.Up` = `"up"` → oba buttony posílají up
- **Fix:** přesunout `Direction` z `pm-button` atributu na `<input type="hidden" name="Direction" value="up|down" />` uvnitř každého formuláře. Hidden input se vždy submituje nezávisle na submit-button name/value propagation.
- **Status:** ✅ VYŘEŠENO 2026-04-21
- **Architecture guard:** regression test — `pm-button` tag nemá `native-type="submit"` s `name=` + `value=` atributy (pattern vede k bugu kvůli gov-button hydraci).

---

## Pozorování z předchozího review (nezařazená, k rozhodnutí)

Během Playwright review byly odhaleny tyto potenciální issues, které nepatří k recent refactoru, ale stojí za zvážení při systémovém fixu:

- **Modal variant width nefunguje.** `gov-dialog[data-modal-variant="wide"]` nastavuje `--gov-dialog-max-width: 1100px`, ale vizuálně zůstává default (~52rem). Pravděpodobně mismatch CSS custom property name s aktuální verzí gov-design-system (shadow DOM si ho nečte). _Status:_ 🔍 ověřeno v harness, NE regrese z 3A-3D
- **Jednani year-grouping bug (user report před 2E):** opraven 19.04. commity `63ea516` / `6acf1d1` / `1886862` — NE akce potřeba

Pokud ranní testování odhalí nové regrese/chování, zapíši je sem před začátkem systémové opravy.
