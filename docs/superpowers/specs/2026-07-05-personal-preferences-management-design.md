# Osobní předvolby — správa po jednotlivých položkách (Design)

**Datum:** 2026-07-05
**Stav:** Návrh k odsouhlasení
**Autor:** Claude + Ing. Pavel Andrlík

## Kontext a motivace

Osobní předvolby uživatele jsou dnes výhradně **klientské** (browser `localStorage` / `cookie`); server o nich neví. Na stránce `Profil` jsou dvě samostatné karty, každá s **jedním hromadným tlačítkem**:

- **Preferovaný formát tisku** — klíč `pmtracker.print.preferredFormat` (localStorage), tlačítko *„Zrušit uloženou volbu"* (`[data-print-preference-reset]`).
- **Uložené filtry projektu** — klíče `pmtracker.projectFilters.v1.project.<id>.defaults` (localStorage) + `.state` (sessionStorage) + legacy prefixy; tlačítko *„Smazat uložené projektové filtry"* (`[data-project-filter-preferences-reset]`), které volá `clearProjectFilterPreferenceStorage()` a **smaže uložené filtry VŠECH projektů naráz**.

**Problém:** hromadné smazání je jediný způsob, jak se předvoleb zbavit. Uživatel s uloženými filtry ve více projektech ztratí všechno naráz a nemá jak vybrat, co smazat. Jak předvoleb přibývá, potřebujeme je spravovat **po jednotlivých položkách**.

**Cíl:** nahradit hromadné mazání **spravovatelným seznamem předvoleb**, kde každá uložená předvolba je samostatný řádek s vlastním malým tlačítkem „smazat". Návrh musí být **rozšiřitelný** — pozdější Sub-projekt 2 (zamykatelné rozbalené projektové menu) do něj přidá svou předvolbu bez zásahu do renderu.

### Vztah k širšímu záměru (mimo rozsah tohoto specu)

Toto je **Sub-projekt 1** z rozděleného zadání. **Sub-projekt 2** (projektové menu „3 + overflow za +" s cookie-perzistencí a zámečkem, odemykatelným v předvolbách) je **mimo rozsah** a dostane vlastní spec → plán. Předvolby se dělají první záměrně, aby se na ně dal zámeček menu později napojit.

## Rozsah

**V rozsahu:**
- Klientský **registr deskriptorů předvoleb** + render seznamu.
- Dva deskriptory: **formát tisku** (0/1 položka) a **uložené filtry projektu** (0..n, jedna položka na projekt).
- Uložení **čitelného labelu projektu** (zkratka) při ukládání filtrů, ať jde seznam vykreslit offline.
- Přestavba předvoleb na stránce `Profil` na jednu sekci **„Předvolby"** se seznamem řádků + malé „smazat" u každého.
- Odstranění dnešních hromadných tlačítek a jejich handlerů.

**Mimo rozsah:**
- Předvolba „zamčené rozbalené menu" a celé projektové menu 3+overflow (Sub-projekt 2).
- Serverové (per-účet / cross-device) předvolby — zamítnuto, zůstáváme klientští.
- Správa tématu (má vlastní přepínač v hlavičce) a přechodný UI stav (`pmtracker.filters.open`, `pmtracker.recordEditor.preference`) — do seznamu se nezařazují.
- Hromadné „Smazat vše" — vědomě vynecháno (uživatel: „to už umí prohlížeče").

## Globální omezení

- **Jen klientské úložiště** (localStorage/cookie); žádná DB migrace; funguje offline (intranet).
- **ESM moduly**: nový kód jako ESM submoduly; side-effect wiring musí být explicitně importován z `bootstrap.js` (jinak tichý fail — viz `project_bundle_sync`).
- **gov-design-system** jako styl; ikona „smazat" = gov-icon (Bootstrap icon, `type="components"`, např. `trash` / `x`). Žádné globální input styly neprosakují (scoping `:not(gov-* input)`).
- **České UI texty.**
- **Cache-busting**: JS/CSS změny se projeví až po verzování entry souboru (dev no-cache), viz `feedback_esm_module_cache_busting`.
- **Progresivní vylepšení**: seznam vykresluje JS; bez JS zůstane statická informativní hláška (viz Chybové stavy).

## Architektura

### Přehled

```
Profil (server render)                 preferences/ (ESM, klient)
┌───────────────────────────┐          ┌────────────────────────────┐
│ sekce „Předvolby"         │          │ registry.js                │
│  [data-preferences-list]  │◀────────▶│  [ printFormatDescriptor,  │
│  (prázdný kontejner)      │  render  │    projectFiltersDescriptor]│
│  [data-preferences-empty] │          ├────────────────────────────┤
└───────────────────────────┘          │ render.js                  │
                                        │  čte registry → řádky      │
        localStorage / cookie ◀─────────│  wire „smazat" + status    │
                                        └────────────────────────────┘
```

Server vykreslí jen **prázdný kontejner** a prázdný stav. Obsah seznamu **plní JS** z registru, protože obsah předvoleb zná jen prohlížeč.

### Komponenty a jejich hranice

**1. Deskriptor předvolby (kontrakt)**
Každý typ předvolby implementuje jednotné rozhraní. Deskriptor je jediné místo, které ví, jak se jeho předvolba ukládá, popisuje a maže.

```
Descriptor = {
  id: string,                     // stabilní, např. "printFormat" | "projectFilters"
  list(): PreferenceItem[]        // 0..n aktuálně uložených položek
}

PreferenceItem = {
  itemKey: string,                // stabilní identita řádku (pro data-atribut a re-render)
  label: string,                  // co uživatel vidí
  valueText?: string,             // volitelná hodnota (např. „PDF")
  remove(): void                  // smaže právě tuto položku ze storage
}
```

- **`printFormatDescriptor`** — `list()` vrátí 0/1 položku: pokud je `pmtracker.print.preferredFormat` nastaven, `{ label: "Preferovaný formát tisku", valueText: "PDF"|"Word", remove: smaž klíč }`.
- **`projectFiltersDescriptor`** — `list()` vyjmenuje `localStorage` klíče odpovídající `pmtracker.projectFilters.v1.project.<id>.defaults`, z každého vytáhne `<id>` a vrátí `{ itemKey: id, label: "Filtry projektu «zkratka»" || "Projekt #<id>", remove: smaž .defaults + .state + .label + legacy klíče toho projektu }`.

**2. `registry.js`** — jen pole deskriptorů `[printFormatDescriptor, projectFiltersDescriptor]`. Rozšíření = přidání dalšího deskriptoru (Sub-projekt 2 sem přidá `menuLockDescriptor`).

**3. `render.js`** — přečte registr, `flatMap` přes `descriptor.list()`, vykreslí řádky do `[data-preferences-list]`. Prázdno → zobrazí `[data-preferences-empty]`. Deleguje kliky na `[data-preference-remove]` (event delegation), zavolá odpovídající `item.remove()`, re-renderuje a zapíše `aria-live` status. Registruje se side-effectem z `bootstrap.js` a spouští při načtení stránky Profil.

**4. Rozšíření `filters/projectFilter.js`**
- `saveProjectFilterDefaults(scope)` navíc zapíše **companion klíč** `pmtracker.projectFilters.v1.project.<id>.label = <zkratka>`. Tvar `.defaults`/`.state` (objekt stavu) se **nemění** → restore cesta zůstává netknutá.
- Nová exportovaná funkce `removeProjectFilterPreference(projectId)` — smaže `.defaults` + `.state` + `.label` + legacy klíče právě jednoho projektu. Použije ji `projectFiltersDescriptor`.
- Zkratka projektu se k JS dostane novým atributem **`data-project-zkratka`** na filter-shellu (`_ProjectFilterShell.cshtml`), plněným z projektového VM (obdoba dřívějšího `data-project-id`). Starší uložené předvolby zkratku nemají → fallback „Projekt #<id>".

**5. Server: `Profil/Index.cshtml`**
Dnešní dvě karty („Preferovaný formát tisku" + „Uložené filtry projektu") nahradí **jedna karta „Předvolby"**:
```
<section class="card">
  <h2>Předvolby</h2>
  <p class="muted">Uloženo pouze v tomto prohlížeči na tomto počítači.</p>
  <ul data-preferences-list class="preferences-list"></ul>
  <p data-preferences-empty class="muted" hidden>Žádné uložené předvolby.</p>
  <p class="profile-preference-status" data-preferences-status aria-live="polite"></p>
</section>
```
Řádek (vykreslený JS):
```
<li class="preference-row">
  <span class="preference-row-label">Filtry projektu ABC</span>
  <button type="button" class="preference-row-remove" data-preference-remove
          data-preference-descriptor="projectFilters" data-preference-item="42"
          aria-label="Smazat předvolbu: Filtry projektu ABC">
    <gov-icon size="s" name="trash" type="components" aria-hidden="true"></gov-icon>
  </button>
</li>
```

### Odstranění staré cesty
- Zrušit tlačítka `[data-print-preference-reset]` a `[data-project-filter-preferences-reset]` z `Profil/Index.cshtml`.
- Zrušit jejich handlery v `bootstrap.js`. `clearProjectFilterPreferenceStorage()` po odstranění jediného volajícího zůstane bez použití → **odstranit** (per-item mazání ji nahrazuje). Před smazáním ověřit grepem, že nemá jiného volajícího.
- `refreshPrintPreferenceUi()` v `print.js` aktualizuje `[data-print-preference-current]`/`-reset`; po odstranění karty tam prostě nic nenajde (no-op). Aktuální hodnotu formátu tisku nově zobrazuje řádek v seznamu předvoleb.

## Datový tok

1. **Načtení Profilu** → `render.js` proběhne → pro každý deskriptor `list()` přečte storage → vykreslí řádky (nebo prázdný stav).
2. **Uložení filtru** (jinde, v projektu) → `saveProjectFilterDefaults` zapíše `.defaults` + nově `.label`. Příště se objeví jako řádek na Profilu.
3. **Smazání řádku** → klik na `[data-preference-remove]` → `render.js` najde deskriptor+položku → `item.remove()` smaže odpovídající storage klíče → re-render → status „Předvolba odstraněna."

## Chybové stavy

- **localStorage/cookie zakázané nebo plné** → veškeré čtení/zápis v `try/catch`; degradace bez pádu (seznam vykreslí, co jde; prázdný stav jako fallback).
- **Nevalidní JSON v klíči** → deskriptor položku přeskočí (chová se jako neexistující).
- **Bez JS** → server ukáže prázdný kontejner + statickou hlášku, že se předvolby spravují po zapnutí JS; žádná chyba. (Interní intranet appka reálně JS vždy má.)

## Testovací strategie

- **Unit (source-assertion, `PmTracker.Tests.Unit`):**
  - `registry.js` deklaruje `printFormat` i `projectFilters` deskriptor.
  - `Profil/Index.cshtml` obsahuje `[data-preferences-list]` + `[data-preferences-empty]` a **neobsahuje** `data-project-filter-preferences-reset` ani `data-print-preference-reset`.
  - `projectFilter.js` exportuje `removeProjectFilterPreference` a `saveProjectFilterDefaults` zapisuje `.label` companion klíč; bulk `clearProjectFilterPreferenceStorage` odstraněn.
  - `_ProjectFilterShell.cshtml` renderuje `data-project-zkratka`.
- **E2E (`PmTracker.Tests.E2E`, Playwright):** ulož filtry dvou projektů → otevři Profil → 2 řádky → smaž jeden → druhý zůstane; formát tisku jako řádek → smaž. Pozn.: E2E JS-init má pre-existing potíže (`project_pre_existing_test_failures`); nespoléhat na E2E jako jediný důkaz, těžiště na source-assertion + ruční ověření uživatelem.
- **Regrese (TDD):** deterministický test, který selže před opravou — Unit assertion, že Profil už neobsahuje hromadné reset tlačítko, napsat jako první (červená → zelená).

## Otevřené otázky

Žádné — rozsah (tisk + per-projekt filtry), vynechání „Smazat vše" i ukládání labelu při save odsouhlaseny uživatelem 2026-07-05.
