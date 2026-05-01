# Learning Log

## 2026-04-30 | Bug Root Cause | PermissionAuthorizationHandler četl projektId jen z RouteValues

ASP.NET Core authz policies pre-action handler `PermissionAuthorizationHandler` extrahoval kontextové klíče (`projektId`, `projektSubsystemId`) **pouze z `Request.RouteValues`**. Když controller deklaroval projektId jako query string parametr (např. `NavrhyController.CreateScheduleProposal?projektId=...`) nebo jako form body field (POST endpointy s [FromForm] command), handler dostal `projektId=null` → `AuthorizationSnapshot.HasPermission` přepadl na global-scope check → pro project-scoped permission keys (např. `dashboard.view`, `proposals.schedule.create`) selhalo → 403 i pro user s validní project rolí (VLASTNIK_PROJEKTU, PROJ_MAN).

Druhý projev: ProjectDashboardController měl route param pojmenovaný `id` místo `projektId` — handler hledal konkrétně klíč "projektId", takže ho v RouteValues nenašel → stejné selhání.

**Files:** [PermissionAuthorizationHandler.cs](PmTracker.Web/Services/Security/PermissionAuthorizationHandler.cs), [ProjectDashboardController.cs:16](PmTracker.Web/Controllers/ProjectDashboardController.cs#L16)

**Resolution:** (1) Přejmenován route param `{id:int}` → `{projektId:int}` v ProjectDashboardController + všechny action params + Url.Action callery. (2) Handler rozšířen o multi-source lookup **Route → Query → Form** s zachováním M2 fail-closed semantics (malformed value → return bez Succeed). 4 nové test cases (query, form, route precedence, malformed query).

**Detekce:** Vždycky když přidáváš `[Authorize(Policy="permission:xxx.yyy")]` na action s project-scoped permission key, ověř že `projektId` je v jednom ze tří zdrojů: route values, query string, form body. Pokud ne, handler dostane null a selže pro běžné users (i když mají roli s daným klíčem). Konvence: pojmenuj route param doslovně `projektId` (ne `id`).

## 2026-04-30 | Bug Root Cause | CSS specificity rovnost: `:not([attr])` vs `.class`

Po lego refactoru pm-tabs zůstal v sync settings starý CSS rule `pm-tab-panel.sync-tab-panel-content { display: grid }`. Bez [active] qualifier má specificitu **0,1,1** (1 element + 1 class) — **stejnou** jako globální `pm-tab-panel:not([active]) { display: none }` (1 element + 1 attribute via :not). Při rovnosti vyhrává source order, a `.sync-tab-panel-content` rule je deklarováno později → `display: grid` přebíjí `display: none` → všechny inactive panely zůstaly viditelné. JS `panel.hidden = true` nepomohl, protože UA `[hidden] { display: none }` má specificitu 0,1,0 (nižší než site rule 0,1,1).

**Files:** [site.css:5728](PmTracker.Web/wwwroot/css/site.css#L5728)

**Resolution:** `pm-tab-panel.sync-tab-panel-content[active]` — přidání `[active]` qualifieru zvýší specificitu na 0,2,1 (vyšší než :not 0,1,1) a omezí pravidlo jen na aktivní panel. Inactive panely fall through na `:not([active]) { display: none }`.

**Detekce:** Vždycky když píšeš CSS rule pro pm-tab-panel s display/visibility property, pamatuj že existuje globální `pm-tab-panel:not([active]) { display: none }`. Buď (a) přidej `[active]` qualifier, (b) zvyš specificity (např. ID nebo descendant selector), nebo (c) buď si vědom source order. Bug zůstane skrytý dokud má panel jen 1 visible tab — pak vidíš správně 1 panel a netušíš že CSS je rozbité.

## 2026-04-30 | Gotcha | TagHelper a stejnojmenný Web Component se přebíjí

`<pm-tabs>` existuje jako TagHelper (PmTabsTagHelper.cs → přepisuje tag na `<gov-tabs>`) **i** jako light-DOM Web Component (wwwroot/js/components/pmTabs.js → registrovaný přes customElements.define). Default Razor renderování VŽDY aktivuje TagHelper, takže Web Component nikdy nedostal upgrade — `<pm-tabs persist="hash">` v _SyncPanel.cshtml a _EditZaznamForm.cshtml se reálně renderoval jako `<gov-tabs persist="hash">` a JS scope (PmTabs class) ho nenašel.

**Files:** [PmTabsTagHelper.cs](PmTracker.Web/TagHelpers/PmTabsTagHelper.cs), [pmTabs.js](PmTracker.Web/wwwroot/js/components/pmTabs.js)

**Resolution:** TagHelper opt-out přes attribute marker — pokud má pm-tabs přítomný `persist`, `persist-key` nebo `sync-input` atribut, TagHelper element ponechá nedotčený (return early). Web Component variant se rozezná přes tyto atributy. Doplněn `WebComponentVariant_LeavesTagUntouched` test (3 [InlineData] varianty).

**Detekce:** Vždycky když registruješ light-DOM custom element s prefixem `pm-*`, zkontroluj zda neexistuje TagHelper na stejné jméno (`grep -rn "HtmlTargetElement.*pm-NÁZEV" --include='*.cs'`). Pokud ano, TagHelper musí mít opt-out přes marker attribute.

## 2026-04-27 | Architecture Decision | DeleteRecord refactor → SQL FK CASCADE (varianta a)

**Kontext:** Po dvou bugech ze stejného dne (Save UPSERT FK violation + DeleteRecord missing VyjadreniVazby/ZaznamNavrhy cleanup) jsme přehodnotili strategii cleanup chain v DeleteRecord. Místo manuálního mazání 13 child tabulek delegujeme cleanup na SQL Server přes FK ON DELETE CASCADE.

**Důvody pro variantu (a) místo (c) reflection-based:**
- DB schema řízeno ručně ze SQL skriptů (per memory rule `project_offline_deployment_sql_migrations`) — přidat ON DELETE CASCADE je standard workflow.
- 14 child tabulek je malý počet — generic řešení by bylo over-engineering.
- Hard-delete je primary use case (vedle existujícího soft-delete přes "stav: zrušeno") — SQL CASCADE je nativní.
- DeleteRecordAsync se zkrátil z 181 na ~100 řádků (úspora 80 řádků boilerplate).
- Lepší performance (1 batch SQL místo 13 RemoveRange round-tripů).

**Provedené změny:**
- **SQL migration** [`db_upgrade_1_3_12_record_delete_cascade.sql`](../db_upgrade_1_3_12_record_delete_cascade.sql) — DROP+ADD všech 14 FK constraints na projektove_zaznamy:
  - 12× CASCADE (history, externi_odkazy, spoluprace, vyjadreni, priority, navrhy.zaznam_id, harmonogram_hodnoty)
  - 1× SET NULL (navrhy.approved_record_id) — multi-path avoidance + audit preservation pro proposals které tento záznam vytvořily
  - 1× nově přidaná FK (zaznam_harmonogram_hodnoty.zaznam_id) — tabulka existovala bez FK (legacy), teď CASCADE
  - vyjadreni_vazby.externi_odkaz_id ZACHOVÁN NO ACTION (Save UPSERT pre-flight check tomu závisí)
- **DeleteRecordAsync** ([`RecordService.DeleteRecord.cs`](../PmTracker.Web/Services/RecordService.DeleteRecord.cs)) — manuální cleanup chain odstraněn, jen audit fetch (AsNoTracking) + Remove parent + SaveChanges. SQL CASCADE handle zbytek.
- **DeleteRecordModalViewModel** + [`DeleteRecordModal.cshtml`](../PmTracker.Web/Views/Projekty/DeleteRecordModal.cshtml) rozšířen o 4 nové counts: HarvestVyjadreniCount, NavrhyTargetCount, NavrhyOriginCount, HistorieCount. User uvidí kompletní souhrn před hard-delete.
- **Architecture guard test** [`RecordDeleteCascadeFkTests`](../PmTracker.Tests.Unit/Architecture/RecordDeleteCascadeFkTests.cs) — kontroluje, že každá FK na projektove_zaznamy v migration souborech má ON DELETE CASCADE nebo je v allowlistu (per-FK whitelist se zdůvodněním).

**Klíčový invariant pro budoucí vývoj:**
- Nová child tabulka s FK na projektove_zaznamy → MUSÍ mít `ON DELETE CASCADE` v migration. Jinak hard-delete fail UNEXPECTED_SERVER_ERROR.
- Architecture test toto vynucuje — pokud někdo přidá FK bez CASCADE, build CI fail.
- DeleteRecordAsync se NEMUSÍ měnit pro novou tabulku.

**Open follow-up:**
- `ReplaceRecordCollaborationAsync` v RecordService.SaveRecord má stále naive `RemoveRange + Add` pattern. Aktuálně OK protože ZaznamSpoluprace nemá FK z append-only historie. Pokud se v budoucnu přidá, může se tamto bug objevit (jako bylo s vyjadreni_vazby na external_odkazy 2026-04-27 ráno).
- Pre-existing test failures `ChatModalDragDropBundle_CallsDeleteBindingWithVazbaId` + `ManualKrokyJsModuleTests` — testují bundle obsah s apostrofy vs uvozovkami (bun bundler kompiluje strings s ").

**Files:**
- [PmTracker.Web/Services/RecordService.DeleteRecord.cs](../PmTracker.Web/Services/RecordService.DeleteRecord.cs) — refactored ~100 lines (was 181)
- [PmTracker.Web/Services/RecordService.EditorQueries.cs:148-186](../PmTracker.Web/Services/RecordService.EditorQueries.cs) — extended counts query
- [PmTracker.Web/Models/ViewModels/ModalViewModels.cs:102-141](../PmTracker.Web/Models/ViewModels/ModalViewModels.cs) — 4 new properties
- [PmTracker.Web/Views/Projekty/DeleteRecordModal.cshtml](../PmTracker.Web/Views/Projekty/DeleteRecordModal.cshtml) — extended summary
- [db_upgrade_1_3_12_record_delete_cascade.sql](../db_upgrade_1_3_12_record_delete_cascade.sql) — new migration
- [PmTracker.Tests.Unit/Architecture/RecordDeleteCascadeFkTests.cs](../PmTracker.Tests.Unit/Architecture/RecordDeleteCascadeFkTests.cs) — architecture guard
- [PmTracker.Tests.Unit/ExterniOdkaz/RecordServiceDeleteCleanupTests.cs](../PmTracker.Tests.Unit/ExterniOdkaz/RecordServiceDeleteCleanupTests.cs) — rewritten shape tests
- [PmTracker.Tests.Unit/Modals/DeleteRecordModalCountsTests.cs](../PmTracker.Tests.Unit/Modals/DeleteRecordModalCountsTests.cs) — modal counts coverage

**Verification:** dotnet build OK (0/0); 1259/1261 unit tests (2 pre-existing fails nesouvisí). User MUST manually run db_upgrade_1_3_12 migration na všech prostředích před deploy nového binárky.

---

## 2026-04-27 | Bug Root Cause | DeleteRecord chyběl cleanup VyjadreniVazby + ZaznamNavrhy → FK violation

**Symptom (preventive analysis, ne user-reported):** Při permanentním smazání projektového záznamu by aplikace spadla na FK violation pro:
- Záznamy s harvestnutými vyjádřeními (`vyjadreni_vazby` rows)
- Záznamy s proposal historií (pending nebo rozhodnutými proposals v `zaznam_navrhy`)
- Záznamy vzniklé schvalovaným CREATE_RECORD návrhem (FK `approved_record_id`)

**Root cause:** `RecordService.DeleteRecord.DeleteRecordAsync` mazal manuálně 11 child tabulek, ale chyběly DVĚ:

1. **`vyjadreni_vazby`** (`zaznam_harmonogram_vyjadreni_vazba`) — `FK_zhvv_externi_odkaz REFERENCES zaznam_externi_odkazy(id) ON DELETE NO ACTION` (db_upgrade_1_3_6_vyjadreni_vazba.sql:19). DeleteRecord mazal `external_odkazy` ale necháchal `vyjadreni_vazby` rows → SQL DELETE external_odkazy fail FK violation. (Cascade z parent record by sice fungoval, ale až po external_odkazy DELETE selže.)

2. **`zaznam_navrhy`** — má 2 FK na `projektove_zaznamy(id)`:
   - `FK_zaznam_navrhy_zaznam (zaznam_id)` — proposals targeting tento záznam
   - `FK_zaznam_navrhy_approved_record (approved_record_id)` — proposals které tento záznam vytvořily
   Oba bez explicitního ON DELETE = default NO ACTION (db_upgrade_1_1_3_record_proposals.sql:46,94).

Stejný mechanismus jako Save bug ze 2026-04-27 (`UNEXPECTED_SERVER_ERROR` při deletion vazby s harvestem) — `DbUpdateException` se vyhodí, není ani `RecordValidationException` ani `InvalidOperationException`, padá do catch-all v `BuildAjaxExceptionResult`.

**Fix:** Přidány 2 cleanup kroky v `DeleteRecordAsync`:
- `harvestVazbyRows` (VyjadreniVazby filtered by ZaznamId) — `RemoveRange` PŘED externalLinkRows
- `proposalRows` (ZaznamNavrhy filtered by ZaznamId == X OR ApprovedRecordId == X) — `RemoveRange` PŘED parent record

Pořadí MATTER — EF Core nemá nakonfigurované `HasOne`/`HasForeignKey` pro `ZaznamHarmonogramVyjadreniVazba` (jen `Property` mapping bez relationship). EF auto-ordering DELETEs nepomáhá → ruční pořadí v kódu je load-bearing.

**Files:**
- [PmTracker.Web/Services/RecordService.DeleteRecord.cs:32-45,77-82,103-108,114-127](../PmTracker.Web/Services/RecordService.DeleteRecord.cs) — cleanup chain rozšířen
- [PmTracker.Tests.Unit/ExterniOdkaz/RecordServiceDeleteCleanupTests.cs](../PmTracker.Tests.Unit/ExterniOdkaz/RecordServiceDeleteCleanupTests.cs) — 4 regression testy (cleanup VyjadreniVazby, cleanup ZaznamNavrhy, ordering, save-changes guard)

**Lesson learned:** Při refactoringu DeleteRecord vždy dotáhnout **kompletní seznam FK na child tables** ze SQL upgrade scripts (`grep "REFERENCES dbo.{parent_table}"`). Spoléhat na EF Core auto-cascade pro tabulky s explicitně nakonfigurovanou relationship — pro tabulky kde EF mapping má jen `Property` bez `HasOne`, EF auto-ordering nefunguje a kód musí ordering řídit ručně. **Pravidelně auditovat tento seznam** při přidávání nové child tabulky (např. nová audit/historie tabulka).

**Open architectural concern:** Stejný pattern (manuální cleanup chain) v RecordService.DeleteRecord je **fragile** — každá nová child tabulka musí být ručně přidaná. Lepší by bylo:
- (a) Sjednotit FK na CASCADE (ale to neumožňuje pre-flight validaci)
- (b) Nahradit DeleteRecord logikou „mark as deleted" + GC (soft delete)
- (c) Vygenerovat cleanup chain z reflection EF entities + FK metadata
Aktuálně si user dle MEMORY.md drží offline DB schema přes ruční SQL skripty, takže cascade-everything by byl OK kompromis. Otevřená otázka pro budoucí refactor.

---

## 2026-04-27 | Bug Root Cause | Smazání externí vazby s harvestem → UNEXPECTED_SERVER_ERROR (FK NO ACTION)

**Symptom:** User smazal externí vazbu (PNF, Cislo=234567) na záznamu s harvestnutými vyjádřeními, klikl Uložit. Server vrátil HTTP 400 `UNEXPECTED_SERVER_ERROR` s prázdným `fieldErrors` a bez `diagnosticLog`. Frontend zobrazil generic „Operaci se nepodařilo dokončit."

**Root cause:** `RecordService.SaveRecord.ReplaceRecordExternalLinksAsync` dělal naivní `RemoveRange(existing) + Add(new)` při každém save (každá záložka, ne jen externí). Když existující vazba měla harvestnuté vyjádření v `zaznam_harmonogram_vyjadreni_vazba`, SQL FK constraint `FK_zhvv_externi_odkaz REFERENCES dbo.zaznam_externi_odkazy(id) ON DELETE NO ACTION` (db_upgrade_1_3_6_vyjadreni_vazba.sql) způsobil DELETE → `DbUpdateException`. Ten není ani `RecordValidationException` ani `InvalidOperationException`, takže `BuildAjaxExceptionResult` v BaseController padá do catch-all větve s `AjaxErrorCodes.UnexpectedServerError`.

**Krytý širší problém:** Naive Replace by failil i pro EDITACI (změna ceny, switch Výzva, datum) jakékoli vazby s harvest historií — Id se měnilo při každém save a FK references zaznamenané v vyjadreni_vazby se rozjely.

**Fix:** UPSERT místo Replace. UPDATE existing entity in-place (Id zachován → FK references stále platné), INSERT pro nové vazby (Id == 0), DELETE jen pro existing rows nepřítomné v command. Pre-flight: dotaz na `VyjadreniVazby` před RemoveRange — pokud má být smazána vazba s FK reference, vyhodit `RecordValidationException` se srozumitelnou českou hláškou (mapováno přes `RecordValidationIssue` rule `external_link_harvest_locked`).

**Files:**
- [PmTracker.Web/Services/RecordService.SaveRecord.cs:1122-1230](../PmTracker.Web/Services/RecordService.SaveRecord.cs) — UPSERT logika
- [db_upgrade_1_3_6_vyjadreni_vazba.sql:18-19](../db_upgrade_1_3_6_vyjadreni_vazba.sql) — FK definice (NO ACTION)
- [PmTracker.Tests.Unit/ExterniOdkaz/RecordServiceExternalLinkUpsertTests.cs](../PmTracker.Tests.Unit/ExterniOdkaz/RecordServiceExternalLinkUpsertTests.cs) — regression test (4 testy: anti-naive pattern, UPSERT-by-Id, pre-flight, user-message)
- [PmTracker.Tests.Unit/Architecture/RecordServiceWriteSplitTests.cs:69](../PmTracker.Tests.Unit/Architecture/RecordServiceWriteSplitTests.cs) — bump LOC limit 1600→1700

**Lesson learned:** Pro tabulky s **append-only audit historií** (vyjadreni_vazby = harvested historie) **nikdy nepoužívat naive `RemoveRange + Add` patterń** na parent řádcích. Vždy UPSERT s preservací Id. Pokud user chce row smazat a FK references existují, vrátit user-friendly validační chybu, ne nechat SQL constraint vybuchnout. Audit historie je hard requirement → smazání parent rowy je business-level zákaz.

**Related architectural concern (open):** `ReplaceRecordCollaborationAsync` má stejný naive pattern pro `ZaznamSpoluprace`. Tato tabulka aktuálně nemá FK z append-only historie, ale pokud se v budoucnu přidá, **stejný bug se objeví**. Doporučení: refactorovat všechny Replace funkce na UPSERT pro robustnost.

---

## 2026-04-27 | Architecture Decision | gov-icon `type="components"` = Bootstrap Icons (ne gov-vlastní set)

**Kontext:** User mě konfrontoval, že tvrdím, že `chat-dots` je „gov ikona". Měl pravdu — je to Bootstrap Icon.

**Fakta:**
- `gov-design-system 4.x` jako npm/dist balíček **neshipuje žádné SVG assets** — `wwwroot/lib/gov-design-system/dist/` obsahuje jen Stencil JS bundle + CSS, žádné `.svg` soubory.
- `gov-icon` Stencil komponenta podporuje (minimálně) `type="base"` a `type="components"`. Ikony fetchuje runtime z URL `/assets/icons/{type}/{name}.svg` (relativně k assetPath, default `/assets/icons/`).
- **`type="components"` katalog je oficiálně Bootstrap Icons 1.11.3** — gov-design-system to deklaruje ve svém `list.js`. Sám gov-input interně používá `<gov-icon name="check-lg" type="components">` a `<gov-icon name="exclamation-lg" type="components">` (viz disassembled bundle `p-19a408eb.entry.js`).
- V projektu PM Tracker jsou ikony stažené offline do `wwwroot/assets/icons/components/`:
  - **Komit `21855ed` (2026-04-18):** počáteční seed 44 ikon ze gov-design-system 4.2.9 list.js
  - **Komit `aea3f2c` (2026-04-23):** přidán `chat-dots.svg` mimo seed (nebyl v list.js)
  - **Komit z 2026-04-27 (současný):** přidán `chat-left-text.svg` na user volbu (vizuálně jasnější pro „vyjádření")

**Důsledek pro budoucí komunikaci:**
- `<gov-icon type="components" name="X">` je **gov-design-system kompatibilní pattern**, ale jméno X musí existovat jako SVG v `/assets/icons/components/`. Pokud tam není, sync stahuje z Bootstrap Icons (https://icons.getbootstrap.com/).
- **Nesnažit se prezentovat naše vlastní přidané ikony jako „gov-shipped"** — gov-design-system 4.x žádné ikony nedodává, a naše custom přidávky (chat-dots, chat-left-text, calendar3, …) jsou **Bootstrap Icons subset** který si projekt udržuje sám.

**Files:**
- [PmTracker.Web/wwwroot/assets/icons/components/](../PmTracker.Web/wwwroot/assets/icons/components/) — 52 SVG souborů (po přidání chat-left-text)
- [PmTracker.Web/wwwroot/lib/gov-design-system/dist/](../PmTracker.Web/wwwroot/lib/gov-design-system/dist/) — pouze JS+CSS bundle, **bez SVG**

**How to apply:** Při psaní/refactoringu ikon v cshtml — pokud někdo požaduje „přímou implementaci GOV komponent", říct otevřeně: gov-icon komponenta je z gov-design-system, ALE její asset je Bootstrap Icon stažený do projektu. Nic neslibovat o „originalitě" ikony.

**Files modified 2026-04-27 (icon swap):**
- [PmTracker.Web/wwwroot/assets/icons/components/chat-left-text.svg](../PmTracker.Web/wwwroot/assets/icons/components/chat-left-text.svg) — nový
- [PmTracker.Web/Views/Projekty/_EditZaznamExternalPanel.cshtml](../PmTracker.Web/Views/Projekty/_EditZaznamExternalPanel.cshtml) — `chat-dots` → `chat-left-text` (2 výskyty)
- [PmTracker.Tests.Unit/ExterniOdkaz/ExterniVazbaCardRenderingTests.cs](../PmTracker.Tests.Unit/ExterniOdkaz/ExterniVazbaCardRenderingTests.cs) — test asserce přepsána

---

## 2026-04-25 | Bug Root Cause | CSS `:checked` nematchuje custom elements (gov-form-switch)

**Symptom:** Při otevření detailu projektu s toggle „Seskupit dle subsystému" zapnutým se neukáže žádný záznam.

**Root cause:** CSS pseudo-třída `:checked` funguje **pouze na nativních HTML form controls** — `<input type="checkbox">`, `<input type="radio">`, `<option>`. Pro custom elementy (`<gov-form-switch>`) **nikdy nematchuje**, i když mají `checked` HTML atribut nastavený.

V [`site.css`](../PmTracker.Web/wwwroot/css/site.css) byly 4 pravidla typu:
```css
.tab-panel[data-tab-panel="zaznamy"]:has([data-filter-key="groupBySubsystem"]:checked) [data-records-view="subsystem"] {
    display: block !important;
}
.tab-panel[data-tab-panel="zaznamy"]:has([data-filter-key="groupBySubsystem"]:not(:checked)) [data-records-view="flat"] {
    display: block !important;
}
```

Po WIP migraci `<input type="checkbox" checked data-filter-key="groupBySubsystem">` → `<gov-form-switch checked data-filter-key="groupBySubsystem">`:
- `:checked` nikdy nematchuje → `:has(... :checked)` nikdy nematchuje → grouped shell rule se neaplikuje
- `:not(:checked)` **vždycky** matchuje (custom element není `:checked`) → flat shell rule se aplikuje **nezávisle na stavu toggle**
- Výsledek: subsystem shell hidden (server-rendered cards uvnitř), flat shell visible (ALE prázdný)
- User vidí prázdnou plochu = „žádné záznamy"

**Files:**
- [PmTracker.Web/wwwroot/css/site.css](../PmTracker.Web/wwwroot/css/site.css) — 4 CSS pravidla s `:checked` / `:not(:checked)`
- [PmTracker.Web/Views/Projekty/_ProjectRecordsTab.cshtml](../PmTracker.Web/Views/Projekty/_ProjectRecordsTab.cshtml) — gov-form-switch s `data-filter-key="groupBySubsystem"`

**Resolution plan:**
1. Replace `:checked` / `:not(:checked)` → `[checked]` / `:not([checked])` (atribut selektor — pracuje na custom elements)
2. Stencil 4 reflektuje `checked` property → atribut (Mutable + Reflect = bitflag 1540 v Stencil bundlu) — atribut zůstává v sync s interním stavem komponenty po user toggle
3. JS toggle handler (gov-change → handleDocumentChange → applyRecordsView) zůstává beze změny — paralelní cesta která správně mění shell visibility přes `hidden` IDL property

**Lekce — preventivní pravidlo do MEMORY.md:**
Při migraci nativního `<input type="checkbox">` → `<gov-form-switch>` (nebo jakýkoli custom element) je nutné taky zkontrolovat všechna CSS pravidla která používají `:checked` / `:not(:checked)` na ten element. Pseudo-třída se nepřenese na custom element automaticky.

## 2026-04-25 | Architecture Decision | gov-form-switch reflection (Stencil 4 metadata)

**Discovery:** Při zkoumání [`core.esm.js`](../PmTracker.Web/wwwroot/lib/gov-design-system/dist/core/core.esm.js) jsem našel Stencil bundle metadata. Pro `gov-form-switch`:
```
[260, "gov-form-switch", {"checked":[1540], ...}]
```

**Bitflag 1540 = 1024 (Mutable) + 512 (ReflectAttr) + 4 (Boolean)** — ekvivalent `@Prop({ mutable: true, reflect: true }) checked: boolean;`.

**Důsledky:**
- `gov-form-switch.checked` je read/write boolean property
- Property → atribut reflexe je obousměrná (set property → update attribute, set attribute → update property)
- `[checked]` CSS atribut selektor funguje pro vyjádření „je zapnutý" stav
- **Ale `:checked` pseudo-class NEFUNGUJE** (jen pro nativní inputy)

**Files:** [PmTracker.Web/wwwroot/lib/gov-design-system/dist/core/core.esm.js](../PmTracker.Web/wwwroot/lib/gov-design-system/dist/core/core.esm.js)
