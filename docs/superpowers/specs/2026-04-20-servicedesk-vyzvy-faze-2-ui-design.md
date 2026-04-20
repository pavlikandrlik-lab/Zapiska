# Spec: ServiceDesk Výzvy — Fáze 2 UI

**Stav:** návrh (čeká na schválení)
**Datum:** 2026-04-20
**Autor:** Ing. Pavel Andrlík + Claude
**Scope:** UI vrstva pro use-case C (Výzvy). Backend (fáze 1) je hotový, tato fáze zobrazuje a umožňuje správu přes prohlížeč. Word export (fáze 3) a produkční propojení ServiceDesk (fáze 4) jsou mimo scope.
**Navazuje na:** [2026-04-20-servicedesk-integrace-vyzvy-design.md](2026-04-20-servicedesk-integrace-vyzvy-design.md) sekce 6 (UI), tento dokument ji zpřesňuje do implementačního detailu.

---

## 1. Kontext

Fáze 1 dodala schéma + backend `IVyzvaService` s metodami `GetBufferAsync`, `GetVyzvyAsync`, `GetVyzvaAsync`, `ZaloztVyzvuZBufferuAsync`, `ZmenitStavAsync`, `NastavitZaradidAsync`, `PrerditPnfAsync`. UI zatím neexistuje:

- Záložka „Výzvy" v projektovém dashboardu je přítomná (`ProjectDashboard/Index.cshtml`, tab `data-dashboard-tab="vyzvy"`), ale její panel `_VyzvyPanel.cshtml` zobrazuje pouze placeholder „ServiceDesk není integrovaný".
- `BuildVyzvyPanel()` v `ProjectDashboardService` vrací `IsServiceDeskIntegrated = false`.
- Formulář externí vazby (`_EditZaznamExternalPanel.cshtml`) má starý `<select>` pro výběr výzvy s hodnotou `ExterniVazba.Vyzva` (string). Entity property se přejmenovala na `VyzvaId` (int?), takže mapování je dočasně nekonzistentní.
- Číselník výzev v `/Ciselniky/…` existuje jen jako fallback — plán říká, že zůstává jen pro SuperAdmin/app_admin.

## 2. Scope fáze 2

### ✅ V scope

1. **Panel „Výzvy" na projektovém dashboardu** — přepsat z placeholderu na reálný layout dle specu 6.1.
2. **Záložka projektu „Výzvy" jako dashboard tab** — využít existující dashboard tab infrastrukturu. Žádná nová stránka.
3. **Switch „Zařadit do další výzvy"** na externí vazbě PNF (`_EditZaznamExternalPanel.cshtml`) — nahradit starý select.
4. **Editace projektu**: přidat pole `MistoPlneni` + `CisloRamcoveSmlouvy` do existujícího editoru projektu (modal / detail page).
5. **Modal „Upravit přiřazení PNF"** — drag & drop mezi bufferem a `Priprava` výzvami projektu.
6. **Fallback číselník výzev** v `/Ciselniky` — aktualizovat na novou entitu `Vyzva` (zobrazí nová pole, `Stav`, read-only snapshot). Přístup jen `SuperAdmin` + `app_admin`.
7. **HTTP endpointy** v `ProjectDashboardController` (GET panel, POST akce) nebo novém `VyzvyController` (AJAX JSON API).
8. **Client-side JS modul** pro interakci (switch toggle, akce výzvy, DnD).
9. **ACL**: `adm_proj` + `proj_man` projektu + `app_admin` + `SuperAdmin` mohou editovat. Ostatní read-only.

### ❌ Mimo scope

- Word export (fáze 3 — vlastní panel v detailu výzvy bude mít disabled placeholder tlačítko „Stáhnout Word (brzy)")
- Produkční ServiceDesk connection string (fáze 4 — panel bude fungovat s `DisabledTicketingQueryService`, HOT data prázdná, pouze PM Tracker strana plná)
- PMP/NES panel (jiný use-case, fáze B ze zastřešující specifikace)
- Mobile layout — projekt cílí na širokoúhlé desktop zobrazení dle požadavku uživatele

## 3. Architektura UI

### 3.1 Strom souborů

**Nové soubory:**
```
PmTracker.Web/Controllers/
  VyzvyController.cs                    ← AJAX JSON endpointy per operace

PmTracker.Web/Models/ViewModels/Vyzvy/
  VyzvyPanelViewModel.cs                ← model pro _VyzvyPanel.cshtml
  VyzvaDetailCardViewModel.cs           ← detail view model pro render jedné výzvy
  BufferCardViewModel.cs                ← model bufferu
  VyzvyReassignModalViewModel.cs        ← model DnD modal

PmTracker.Web/Views/ProjectDashboard/
  _VyzvyPanel.cshtml                    ← REFAKTOR z placeholderu na reálný panel
  _VyzvyPanel.BufferCard.cshtml         ← partial pro buffer
  _VyzvyPanel.VyzvaCard.cshtml          ← partial pro jednu výzvu

PmTracker.Web/Views/Vyzvy/
  ReassignModal.cshtml                  ← modal s drag & drop

PmTracker.Web/wwwroot/js/modules/vyzvy/
  index.js                              ← bootstrap modulu
  panelController.js                    ← ovládání panelu (akce tlačítek)
  switchController.js                   ← switch na externí vazbě
  reassignModal.js                      ← drag & drop modal

PmTracker.Web/wwwroot/css/components/
  vyzvy-panel.css                       ← styly panelu + karet (pokud nevejde do site.css)
```

**Modifikované soubory:**
```
PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs
  BuildVyzvyPanelAsync()                ← z placeholderu na reálný builder, injekce IVyzvaService
PmTracker.Web/Controllers/ProjectDashboardController.cs
  GetVyzvyPanel()                       ← úprava — volá AsyncBuild
PmTracker.Web/Views/Projekty/_EditZaznamExternalPanel.cshtml
                                        ← nahradit select za switch + indikátor
PmTracker.Web/Views/Projekty/ProjectModal.cshtml (nebo odpovídající edit projektu view)
                                        ← přidat inputy MistoPlneni + CisloRamcoveSmlouvy
PmTracker.Web/Models/ViewModels/Projekty/ProjektEditViewModel.cs
                                        ← přidat 2 properties
PmTracker.Web/Services/ProjectService.*.cs (Commands)
                                        ← mapping 2 nových polí při update
PmTracker.Web/Models/ViewModels/Projekty/ZaznamEditViewModels.cs
  ExterniVazbaViewModel                 ← property `Vyzva (string)` → `VyzvaId (int?)` + `VyzvaKod (string?)` + `ZaradidDoVyzvy (bool)`
  ZaznamEditViewModel                   ← odstranit `Vyzvy (IReadOnlyList<string>)` (již nepoužíváme select)
PmTracker.Web/Services/Records/RecordProposalPayloadMapper.cs
                                        ← mapování nových polí
PmTracker.Web/wwwroot/js/modules/recordEditor/              (pokud existuje external sekce)
                                        ← napojení switchController
PmTracker.Web/Controllers/CiselnikyController.cs (nebo DictionaryService.Queries/Commands)
                                        ← úprava číselníku Výzvy: read-only + nová pole
PmTracker.Web/wwwroot/js/modules/projectDashboard.js nebo dashboard.js
                                        ← bootstrap modulu Vyzvy když je aktivní tab
```

### 3.2 Dataový tok

```
Uživatel → ProjectDashboard/{id}/dashboard?tab=vyzvy
   ↓
Prohlížeč načte Index.cshtml (tabs), aktivuje "vyzvy"
   ↓
AJAX GET /projekty/{id}/dashboard/vyzvy-panel
   ↓
ProjectDashboardController.GetVyzvyPanel(id)
   ↓
ProjectDashboardService.BuildVyzvyPanelAsync(id, ct)
  → IVyzvaService.GetBufferAsync(id)
  → IVyzvaService.GetVyzvyAsync(id)  (buffer + všechny výzvy projektu)
   ↓
Renderovaný partial _VyzvyPanel.cshtml
   ↓
JS modul vyzvy/panelController.js najde data-vyzvy-panel a naváže event handlery

Akce (klik "Založit výzvu", změna stavu, odebrat PNF, DnD):
   Prohlížeč → POST /vyzvy/{endpoint} s antiforgery
   → VyzvaController → IVyzvaService metoda
   → JSON response { success, errorCode?, message?, reload? }
   → JS přenačte panel (AJAX GET ...vyzvy-panel) a překreslí DOM
```

### 3.3 Role v DI

- `ProjectDashboardService` injektuje `IVyzvaService` (už zaregistrovaný)
- `VyzvyController` injektuje `IVyzvaService`, `IUserContextResolver`
- Žádné nové DI registrace — `AddVyzvyServices()` už v Program.cs

## 4. UI layout — panel Výzvy (širokoúhlé)

Podle specu sekce 6.1 (dvousloupcový), ale zjednodušeno pro **jednu stránku bez postranníku**, protože projekt má málo výzev v danou chvíli (< 10 aktivních) a 50 za rok (archiv). Místo postranníku použijeme **sekvenční layout shora dolů**:

```
┌───────────────────────────────────────────────────────────────────────┐
│ BUFFER — čeká na zařazení (3 PNF)                                     │
│ ┌─────────────────────────────────────────────────────────────────┐  │
│ │ PNF  336865  "Omezení přístupu uživatele..."  12 345,00 Kč  [x]│  │
│ │ PNF  345763  "Automatizace generování..."    25 000,00 Kč  [x]│  │
│ │ PNF  341837  "Automatizace DRD 12..."        18 500,00 Kč  [x]│  │
│ └─────────────────────────────────────────────────────────────────┘  │
│                             [Založit výzvu z bufferu →]               │
└───────────────────────────────────────────────────────────────────────┘

┌───────────────────────────────────────────────────────────────────────┐
│ VÝZVA 2/2026 [Priprava] · Ing. Andrlík · 2026-04-15           [▼]    │
│ ┌─────────────────────────────────────────────────────────────────┐  │
│ │ a) RU841-2 | 336865 | Omezení přístupu uživatele | 12 345,00   │  │
│ │ b) RU848-5 | 345763 | Automatizace generování    | 25 000,00   │  │
│ │ ─                                                 ─             │  │
│ │                                  CELKEM bez DPH:  37 345,00 Kč │  │
│ └─────────────────────────────────────────────────────────────────┘  │
│ [Stáhnout Word ✗ brzy]  [Upravit přiřazení]  [Stav: Priprava ▼]       │
└───────────────────────────────────────────────────────────────────────┘

┌───────────────────────────────────────────────────────────────────────┐
│ VÝZVA 1/2026 [Odeslano] · Ing. Andrlík · 2026-03-10           [▶]    │
│ (kolapsované — klik otevře detail)                                    │
└───────────────────────────────────────────────────────────────────────┘
```

### Pravidla layoutu

- **Buffer vždy nahoře** jako první sekce, i když prázdný („Buffer je prázdný — přepněte switch u PNF v externích vazbách")
- **Výzvy setříděné `DatumZalozeni DESC`** (nejnovější nahoře)
- **Výchozí stav každé výzvy v seznamu:**
  - `Priprava` → **rozbalené** (uživatel s tím pracuje)
  - `Odeslano`, `Zruseno` → **kolapsované** (pouze hlavička, rozbalí klikem na `▶`)
- **Badge barva dle stavu**:
  - `Priprava` → žlutá (warning token)
  - `Odeslano` → zelená (success token)
  - `Zruseno` → šedá (muted token)
- **Akční tlačítka v hlavičce každé výzvy**:
  - „Stáhnout Word" — disabled s tooltip „Bude dostupné v další fázi" (fáze 3)
  - „Upravit přiřazení" — otevře modal DnD (jen pokud je alespoň 1 `Priprava` výzva v projektu nebo buffer není prázdný)
  - „Stav" dropdown — změna stavu (povolené přechody dle `VyzvaStateMachine`)
- **U bufferu**: tlačítko „Založit výzvu z bufferu" (disabled pokud buffer prázdný NEBO projekt chybí `MistoPlneni`/`CisloRamcoveSmlouvy` — v posledním případě zobrazit varovný banner s linkem na edit projektu)

## 5. Switch na externí vazbě PNF

V `_EditZaznamExternalPanel.cshtml` u každé externí vazby kde `Typ == "PNF"`:

```
┌─────────────────────────────────────────────────┐
│ Typ: [PNF ▼]   Číslo: [336865]  Cena: [12 345]  │
│ ...ostatní pole existujícího řádku...            │
│                                                  │
│ Zařadit do další výzvy: [●━━○] Zap              │
│   Aktuálně: Zařazeno do výzvy 2/2026            │
│   (nebo "Čeká se" / žádný text)                 │
└─────────────────────────────────────────────────┘
```

### Pravidla

- **Viditelnost:** switch viditelný jen pro `Typ == "PNF"`. Pro `NES`/`PMP` se skryje (CSS `hidden`).
- **Stav switche**:
  - OFF — `ZaradidDoVyzvy = false`, `VyzvaId = null` → žádný status text
  - ON bez výzvy — `ZaradidDoVyzvy = true`, `VyzvaId = null` → status „Čeká se (buffer projektu)"
  - ON s výzvou Priprava — `VyzvaId = X`, stav `Priprava` → status „Zařazeno do výzvy X/YYYY" (link na panel výzev)
  - ON s výzvou Odeslano — `VyzvaId = X`, stav `Odeslano` → status „Zařazeno do výzvy X/YYYY (odeslána)", **switch disabled** (nelze změnit)
- **Ukládání:** switch volá POST `/vyzvy/set-zaradid` okamžitě při změně (ne při submit formuláře) — je to self-contained akce, oddělená od ostatních polí externí vazby
- **Read-only pro ne-PM role**: switch disabled

## 6. Modal „Upravit přiřazení PNF"

Dialog otevíraný tlačítkem „Upravit přiřazení". Gov-dialog komponenta (existující vzor v projektu).

### Layout

Jeden sloupec pro buffer + sloupec pro každou `Priprava` výzvu projektu. Maximum ~4 sloupce vedle sebe; pokud víc, horizontální scroll.

```
┌─────────────────────────────────────────────────────────────────┐
│ Upravit přiřazení PNF                                     [x]   │
│                                                                  │
│ ┌──────────┐ ┌──────────┐ ┌──────────┐                         │
│ │ BUFFER   │ │ 1/2026   │ │ 2/2026   │                         │
│ │          │ │ Priprava │ │ Priprava │                         │
│ ├──────────┤ ├──────────┤ ├──────────┤                         │
│ │ 336865   │ │ 341837   │ │ 345763   │                         │
│ │ Omezení..│ │ Auto DRD │ │ Auto gen │                         │
│ ├──────────┤ ├──────────┤ ├──────────┤                         │
│ │ (drop)   │ │ (drop)   │ │ (drop)   │                         │
│ └──────────┘ └──────────┘ └──────────┘                         │
│                                                                  │
│ Drop-cíle jsou jen Priprava výzvy projektu. Odeslane vyzvy     │
│ nejsou viditelné.                                              │
│                                             [Zavřít]            │
└─────────────────────────────────────────────────────────────────┘
```

- **Odeslane vyzvy se NEZOBRAZUJÍ** jako cíle (ani zdrojové) — `PrerditPnfAsync` by je odmítnul jako Locked, takže ani UI je nenabízí
- **DnD technika:** native HTML5 DnD (`draggable="true"` + `drop` events) — žádná knihovna. Projekt nemá instalovanou DnD knihovnu, a pro tento use-case (jednoduché přesouvání PNF mezi sloupci) je native API dostačující
- **Po každém drop**: POST `/vyzvy/prerdit-pnf` → při success překreslí modal (znovu načte bufferu + výzvy), při chybě zobrazí toast
- **Zavření modalu**: auto reload panelu výzev (aby se zobrazily změny)

## 7. Editace projektu — nová pole

V existujícím editoru projektu (`ProjectModal.cshtml` + detail page) přidat dvě textové inputy:

- `MistoPlneni` — textarea 2 řádky, max 500 znaků, placeholder „např. FIS (EIS): VZ 8201, Tychonova 1, 160 01 Praha 6"
- `CisloRamcoveSmlouvy` — text input, max 100 znaků, placeholder „např. 23106000271"

ACL editace: `app_admin` + `SuperAdmin` (jiní vidí jen read-only). Ostatní vidí tato pole jen ve zobrazovací části (detail projektu).

## 8. HTTP API (VyzvyController)

Všechny POST endpointy vracejí JSON:
```json
{
  "success": true | false,
  "errorCode": "BufferEmpty" | ...,        // při success=false
  "message": "...",                         // lidsky čitelná zpráva při chybě
  "reload": true                            // signál pro JS aby přenačetl panel
}
```

Endpointy:

| Metoda | URL | Tělo | Popis |
|---|---|---|---|
| GET | `/projekty/{id}/dashboard/vyzvy-panel` | — | HTML partial (existuje, přepsat) |
| POST | `/vyzvy/zalozit` | `{ projektId }` | Založí výzvu z bufferu |
| POST | `/vyzvy/zmenit-stav` | `{ vyzvaId, novyStav }` | Změna stavu |
| POST | `/vyzvy/set-zaradid` | `{ externiOdkazId, zaradit }` | Switch na ext. vazbě |
| POST | `/vyzvy/prerdit` | `{ externiOdkazId, cilovaVyzvaId? }` | DnD přesun |
| GET | `/vyzvy/reassign-modal?projektId={id}` | — | HTML partial pro DnD modal |

**Antiforgery**: všechny POSTy chráněny stejně jako existující endpoints v projektu (viz `BaseController`).

**ACL v controlleru**: pro POST endpointy ověř `CurrentUserContext.CanEditProject(projektId)` (nebo ekvivalent) + role jedna z: `proj_man`, `adm_proj`, `app_admin`, `SuperAdmin`. Jinak `Forbid()`.

## 9. CSS / design system

- Použít existující **Gov Design System komponenty** kde dostupné (`gov-button`, `gov-dialog`, `gov-badge`, `gov-switch` — pokud existuje). Pokud `gov-switch` chybí, použít standardní `<input type="checkbox" class="pm-switch">` s vlastním stylem.
- **Karty výzev**: styl podle stávajících dashboard panelů (`_NesPanel.cshtml`, `_StatisticsPanel.cshtml` jako vzor)
- **Barvy stavů**: využít existující `--token-success`, `--token-warning`, `--token-muted` z `tokens.css`
- **Typografie**: standardní (dědí se z `site.css`)
- **Nové CSS:** v `PmTracker.Web/wwwroot/css/components/vyzvy-panel.css`, importovat z `site.css` (nebo přidat do `wwwroot/css/site.css` pokud je to malé)

## 10. JavaScript moduly

### 10.1 `vyzvy/index.js` — bootstrap

```js
// Pseudokód
export function bootstrapVyzvyPanel(panelElement) {
  const projectId = panelElement.dataset.projectId;
  initPanelController(panelElement, projectId);
  initReassignModal(projectId);
}

// Napojení v projectDashboard.js: při aktivaci tabu "vyzvy" volej bootstrap
```

### 10.2 `vyzvy/panelController.js`

- Najde všechna tlačítka `[data-vyzvy-action]` a naváže klik handlery
- Akce: `zalozit`, `zmenit-stav`, `otevrit-reassign`, `stahnout-word` (disabled)
- Po akci: AJAX GET panel, nahradí DOM

### 10.3 `vyzvy/switchController.js`

- Použije se v `_EditZaznamExternalPanel.cshtml`
- Najde `[data-vyzvy-switch]`, naváže change handler
- Při změně: POST `/vyzvy/set-zaradid`, aktualizuje status text bez reloadu

### 10.4 `vyzvy/reassignModal.js`

- Otevře modal s DnD
- Implementuje HTML5 drag events
- Po drop volá POST `/vyzvy/prerdit` → reload modal content
- Při zavření modalu signal rodičovi (panelController) → reload panelu

## 11. Tests

### Unit testy (PmTracker.Tests.Unit)

- `VyzvyControllerTests` — každý endpoint: šťastná cesta, ACL forbid, validace payloadu, Service chyba → JSON mapping
- `VyzvyPanelViewModelBuilderTests` (pokud vzniká builder) — mapping z `IVyzvaService.Get*` → panel VM
- `ProjektEditCommandTests` — nová pole ukládána přes Update (pokud existuje relevantní test)

### UI testy (PmTracker.Web.Tests / Playwright — podle projektové konvence)

**Mimo scope fáze 2** — ruční smoke test stačí, automatizace až když bude Word export hotový a má smysl end-to-end scénář.

## 12. Akceptační kritéria

1. ✅ Panel Výzvy se renderuje s reálnými daty (buffer + výzvy projektu)
2. ✅ Tlačítko „Založit výzvu" funguje, vytvoří novou výzvu, přesune buffer, přenačte panel
3. ✅ Stav výzvy lze měnit dropdownem (jen povolené přechody nabídnuty)
4. ✅ Switch „Zařadit do další výzvy" se objeví jen u PNF, změna je okamžitá (AJAX) a zobrazí správný status
5. ✅ Switch je disabled pro PNF ve výzvě `Odeslano`
6. ✅ Modal „Upravit přiřazení" drag & drop funguje mezi bufferem a `Priprava` výzvami, `Odeslano` výzvy se nezobrazují
7. ✅ Edit projektu zobrazí a uloží `MistoPlneni` + `CisloRamcoveSmlouvy` pro `app_admin`/`SuperAdmin`
8. ✅ Založení výzvy zablokováno s chybovou hláškou, pokud projekt nemá `MistoPlneni` nebo `CisloRamcoveSmlouvy`
9. ✅ Fallback číselník `/Ciselniky/Vyzvy` funguje pro SuperAdmin/app_admin (zobrazuje nová pole, umožňuje edit pro opravu chyb)
10. ✅ Tlačítko „Stáhnout Word" je viditelné ale disabled s tooltipem „Bude dostupné v další fázi"
11. ✅ ACL: non-PM role vidí panel read-only (bez akčních tlačítek, switch disabled)
12. ✅ Build 0 warnings/errors, všechny existující testy projdou, nové testy projdou

## 13. Otevřené body (k doladění při implementaci)

1. **Nové samostatné CSS soubor vs. site.css** — rozhodne implementer podle velikosti stylů (heuristika: > 80 řádků = nový soubor)
2. **Gov-switch existence** — pokud existuje, použít; jinak vlastní styl. Ověří se v prvním kroku.
3. **Editor projektu — modal vs. detail page** — podíváme se, kde dnes probíhá edit (`ProjectModal.cshtml`); pole přidat tam
4. **Číselník `Vyzvy` v admin** — pokud má stávající controller komplikovanější CRUD logiku, přizpůsobit UI; jinak skrýt editaci a zobrazovat read-only audit seznam

## 14. Návrh implementace (hrubá fázovaná kostra)

**Fáze 2a — Backend UI podpora:**
1. `VyzvyController` s JSON endpoints
2. `ProjectDashboardService.BuildVyzvyPanelAsync()` refactor
3. `ProjektEditViewModel` + edit service: `MistoPlneni` + `CisloRamcoveSmlouvy`
4. `ExterniVazbaViewModel`: property `Vyzva` (string) → `VyzvaId` (int?), `VyzvaKod` (string?), `ZaradidDoVyzvy` (bool)
5. Unit testy pro controller

**Fáze 2b — HTML/CSS:**
6. `_VyzvyPanel.cshtml` přepsat
7. Partials pro buffer + kartu výzvy
8. `ReassignModal.cshtml`
9. Úprava `_EditZaznamExternalPanel.cshtml` (switch)
10. Úprava editoru projektu (nová pole)
11. CSS pro panel + karty

**Fáze 2c — JS:**
12. `vyzvy/panelController.js`
13. `vyzvy/switchController.js`
14. `vyzvy/reassignModal.js`
15. `vyzvy/index.js` bootstrap
16. Napojení v `projectDashboard.js` + `recordEditor` bootstrap
17. Update `site.bundle.js` (dle zavedené konvence)

**Fáze 2d — Číselník fallback:**
18. Update `CiselnikyController` + views pro novou entitu `Vyzva`

**Fáze 2e — Ladění + manuální smoke:**
19. Build + tests
20. Manuální test všech akceptačních kritérií na lokálu (Ticketing:Enabled=false → HOT data prázdná, ale PM Tracker strana funkční)

---

**Konec specu.** Po schválení navazuje `superpowers:writing-plans` pro detailní implementační plán.
