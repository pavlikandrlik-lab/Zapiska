# Spec: Výzvy — dokončení tématu

**Stav:** návrh (čeká na schválení)
**Datum:** 2026-09-07
**Autor:** Ing. Pavel Andrlík + Claude (brainstorming)
**Navazuje na:** [2026-04-20-servicedesk-integrace-vyzvy-design.md](2026-04-20-servicedesk-integrace-vyzvy-design.md) (use-case C, fáze 1–2 hotové) a [bod 11 analýzy z 2026-06-25](../../known-issues/dashboard-and-search-analysis-2026-06-25.md)
**Scope:** přesun Výzev z dashboardu do projektového menu, ruční číslování, nový 20/80 layout s výběrem roku, přesuny PNF bez skoku obrazovky. Generování/tisk výzvy je v tomto specu **jen tlačítko bez funkce** — upřesní se samostatně jako poslední krok.

---

## 1. Kontext

Fáze 1 (schéma + backend) a fáze 2 (UI) jsou hotové a nasazené, poslední commit do Výzev je z 2026-05-01. Analýza z 2026-06-25 zaznamenala pět požadavků na přepracování, které se nikdy nepřevedly do specu. Tento dokument je spojuje s pozdějším zadáním z 2026-09-07 (umístění, oprávnění, výběr roku, chování po přesunu).

**Dnešní stav, který se mění:**

| Oblast | Dnes |
|---|---|
| Umístění | Záložka uvnitř projektového dashboardu (`ProjectDashboard/Index`, tab `vyzvy`) |
| Viditelnost | Řízena oprávněním `dashboard.vyzvy.view` |
| Číslování | Automatické, `MAX(PoradoveVRoce) + 1` |
| Zakládání | Přesune celý buffer do nové výzvy; prázdný buffer zakládání blokuje |
| Layout | Svislý stack karet: buffer nahoře, pod ním všechny výzvy všech let |
| PNF v kartě | Plochá tabulka bez vazby na projektový záznam |
| Přesuny PNF | Jen modal „Upravit přiřazení" s drag & drop |

## 2. Rozsah

### ✅ V scope

1. Přesun Výzev z dashboardu do **projektového menu** (vedle Návrhy, před Dashboard).
2. Viditelnost záložky **bez oprávnění** — vidí ji každý, kdo vidí projekt.
3. Omezení **CUD operací** na role Vlastník projektu, Gestor projektu, Projektový manažer, Administrátor projektu.
4. **Ruční číslování** výzvy místo automatického.
5. Zakládání **prázdné** výzvy (buffer se nepřesouvá, prázdný buffer neblokuje).
6. Nový **20/80 layout** s railem vlevo a obsahem vpravo.
7. **Výběr roku** v railu; buffer vždy první bez ohledu na rok.
8. PNF v pravém panelu **seskupené podle projektového záznamu**.
9. Přesuny PNF přes **drag & drop** (primární) a **kontextové menu** (rovnocenná druhá cesta), **bez skoku obrazovky**.
10. Zrušení modalu „Upravit přiřazení".
11. Tlačítko tisku výzvy — **přítomné, bez funkce**.
12. Switch "Zařadit do další výzvy" nově končí vždy v bufferu.
13. Povýšení verze aplikace 0.8 → 0.9.

### ❌ Mimo scope

- Vlastní generování / tisk výzvy (samostatné upřesnění na konci tématu).
- Word export dle fáze 3 původního specu — nahrazen bodem výše.
- Produkční připojení ServiceDesku (fáze 4, čistě konfigurace).
- Přepis wiki stránky [vyzvy/index.md](../../wiki/projekty/projektovy-dashboard/vyzvy/index.md), která popisuje neexistující funkce — vzniká jako úklidový úkol po dokončení tématu.

## 3. Umístění — přesun z dashboardu do projektového menu

### 3.1 Nová záložka

Výzvy se stávají **projektovou záložkou** `Projekty/Detail?tab=vyzvy`, ve skupině sekundárních záložek [Detail.cshtml:59-97](../../../PmTracker.Web/Views/Projekty/Detail.cshtml) v pořadí:

```
Záznamy | Harmonogram | Jednání | [ Osoby (tým) · Návrhy · VÝZVY · Dashboard ]
```

Záložka jede po **stejném lazy-load vzoru jako Návrhy** — `ProjektLazyTabShellViewModel` s `TabKey = "vyzvy"`, `LoadUrl` na novou akci `VyzvyTabPartial(id, rok?)` v `ProjektyController`, placeholder s textem „Načítání výzev...". Při přímém vstupu s `?tab=vyzvy` se panel renderuje rovnou (`LoadedVyzvyTab`), stejně jako to dnes dělá `LoadedNavrhyTab`.

### 3.2 Odpojení od dashboardu

Ruší se:

- Tlačítko záložky a panel `vyzvy` v [ProjectDashboard/Index.cshtml](../../../PmTracker.Web/Views/ProjectDashboard/Index.cshtml)
- Akce `GetVyzvyPanel` v [ProjectDashboardController.cs:129](../../../PmTracker.Web/Controllers/ProjectDashboardController.cs)
- Odpovídající pole ve view modelu dashboardu

`VyzvyPanelBuilder` **zůstává** a stěhuje se z `Services/ProjectDashboard/` do `Services/Vyzvy/` — obsahově je to builder výzev, ne dashboardu.

## 4. Oprávnění

### 4.1 Zobrazení — bez oprávnění

Záložku i její obsah vidí **každý, kdo se dostane na detail projektu**. Žádný permission klíč se neptá.

Klíč `dashboard.vyzvy.view` tím ztrácí smysl a **odstraňuje se** ze [PermissionSeedConfiguration.cs](../../../PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs) včetně všech 11 rolových přiřazení. Dotčené testy (`ProjectRolePermissionMatrixTests`, `PerActionKeyCoverageTests`, `ResolverSwapSafetyTests`) se upraví.

### 4.2 Změny — jen čtyři projektové role

Vytváření, úpravy a mazání smí:

| Role | Kód |
|---|---|
| Vlastník projektu | `VLASTNIK_PROJEKTU` |
| Gestor projektu | `GEST` |
| Projektový manažer | `PROJ_MAN` |
| Administrátor projektu | `ADM_PROJ` |

Klíče zůstávají beze změny (`vyzvy.create`, `vyzvy.state.change`, `vyzvy.pnf.assign`, `vyzvy.pnf.reassign`, `vyzvy.word.export`). Jediná změna v seedu je **přidání role `GEST`**, která je dnes nemá; ostatní tři je už mají.

Globální administrátoři `SUPERADMIN` a `APP_ADMIN` si klíče **ponechávají** (potvrzeno 2026-09-07) — výčet čtyř rolí výše je výčet *projektových* rolí.

### 4.3 Read-only zobrazení

Kdo nemá klíče, vidí kompletní obsah — rail, výzvy, PNF, ceny — ale bez ovládacích prvků: bez tlačítka „Nová výzva", bez měniče stavu, bez kontextového menu a bez drag & dropu.

## 5. Zakládání výzvy — ruční číslo

### 5.1 Změna služby

`ZaloztVyzvuZBufferuAsync` → **`ZalozitVyzvuAsync(projektId, poradoveVRoce, zalozilOsobaId, now, ct)`** (opravuje se i překlep v názvu).

Chování:

1. Projekt existuje, má `MistoPlneni` a `CisloRamcoveSmlouvy` — beze změny.
2. `poradoveVRoce` je v rozsahu **1–999**, jinak `InvalidVyzvaNumber`.
3. Číslo není obsazené v rámci `(CisloRamcoveSmlouvySnapshot, Rok)`, jinak `DuplicateVyzvaNumber`.
4. Vzniká **prázdná** výzva ve stavu `Priprava`. Buffer se nedotýká.
5. Zápis do `VyzvaHistorieStavu` — beze změny.

`Rok` je vždy aktuální rok. Zakládání do jiného roku zadání nezmiňuje, takže se nenabízí.

Pojistkou proti souběhu je existující unique index `ux_vyzvy_smlouva_rok_poradove` ([db_upgrade_1_1_8_vyzvy.sql:60](../../../db_upgrade_1_1_8_vyzvy.sql)). Porušení se převádí na `DuplicateVyzvaNumber`, aby druhý uživatel dostal srozumitelnou hlášku místo serverové chyby.

### 5.2 Chybové kódy

Přibývají `InvalidVyzvaNumber` a `DuplicateVyzvaNumber`. `BufferEmpty` se odstraňuje — už nemá kdo ho vyvolat. Ostatní hodnoty mají explicitní čísla, takže se nic nepřečísluje.

### 5.3 Modal „Nová výzva"

Volné číselné pole s nápovědou, která čísla jsou v daném roce a smlouvě obsazená:

```
┌─ Nová výzva ─────────────────────────┐
│ Číslo výzvy:  [  47  ] / 2026        │
│                                       │
│ Rámcová smlouva: 23106000271          │
│ Obsazená čísla: 1, 2, 3, 5            │
│                                       │
│         [ Založit ]  [ Zrušit ]       │
└───────────────────────────────────────┘
```

Dropdown volných čísel z §11.2.5 analýzy se **nepoužívá**: nabízel jen prvních dvacet volných, takže číslo domluvené se SVA mimo tento rozsah by nešlo zadat. Zadání říká „jediné ověření: duplicita" — volné pole to splňuje přesně.

Po založení se **vybere nová výzva** — právě ji jdeš plnit.

Nová služební metoda `GetObsazenaCislaAsync(projektId, rok, ct)`.

### 5.4 Zrušená blokace

`VyzvyPanelBuilder.BufferZalozitPodminky` přestává blokovat prázdným bufferem. Blokace chybějícím místem plnění a číslem rámcové smlouvy zůstává i s vysvětlujícím textem.

## 6. Layout

### 6.1 Rozvržení 20/80

```
┌────────────────┬──────────────────────────────────────────────────┐
│ Rok: [2026 ▾]  │  VÝZVA 2/2026   [Příprava ▾]  Ing. A.  15.4.2026 │
│                │                          [Tisk výzvy] [Stav ▾]   │
│ ▸ BUFFER   (3) │                                                   │
│                │  RU841-2 · Rozšíření sestav                       │
│ ▪ 2/2026   (5) │     PNF 336865 · Omezení přístupu    12 345 Kč [⋯]│
│   Příprava     │     PNF 341837 · Automatizace DRD    18 500 Kč [⋯]│
│                │                                                   │
│ ▪ 1/2026   (2) │  RU848-5 · Reporting                              │
│   Odesláno     │     PNF 345763 · Automatizace gen.   25 000 Kč [⋯]│
│                │                                                   │
│ [+ Nová výzva] │                        CELKEM      55 845 Kč      │
└────────────────┴──────────────────────────────────────────────────┘
```

- **Levý rail (20 %)** — výběr roku, dlaždice, tlačítko „Nová výzva".
- **Pravý panel (80 %)** — obsah vybrané dlaždice.

Grid `grid-template-columns: 1fr 4fr`. Nosný layout drží letité flex/grid základy, `:has` a podobné novinky se použijí nejvýš jako vylepšení — cílový prohlížeč je Edge na i15.

### 6.2 Výběr roku

Select v hlavičce railu. Nabídka = roky, ve kterých projekt má výzvu, plus vždy aktuální rok. Výchozí je **aktuální rok**.

Změna roku znamená serverový round trip (jiná množina výzev) — panel se překreslí a vybere se buffer.

### 6.3 Dlaždice

- **Buffer je vždy první**, nezávisle na zvoleném roce.
- Pod ním výzvy zvoleného roku, řazené **od nejnovější k nejstarší** (`DatumZalozeni DESC`).
- Každá dlaždice nese kód, stav a počet PNF. Buffer jen počet.
- Vybraná dlaždice je zvýrazněná.

Výchozí výběr po načtení panelu je **buffer** — je první v seznamu a je to místo, kde práce začíná.

### 6.4 Pravý panel

Vykreslují se **všechny panely najednou**, JS přepíná viditelnost. Nestojí to nic navíc: `GetVyzvyAsync` už dnes načítá všechny výzvy se všemi položkami a panel je všechny renderuje. Odpadá tím další endpoint i čekání při kliknutí na dlaždici.

Obsah panelu výzvy:

- **Hlavička** — kód, stav, kdo a kdy založil, datum odeslání, tlačítka „Tisk výzvy" a měnič stavu.
- **Tělo** — PNF seskupené podle projektového záznamu.
- **Patička** — součet předpokládaných cen.

Panel bufferu má stejné tělo bez hlavičky a bez součtu.

Prázdná výzva zobrazí „Výzva zatím neobsahuje žádné PNF." Prázdný buffer „Buffer je prázdný — přepněte switch u PNF v externích vazbách."

## 7. Data panelu

### 7.1 Seskupení podle záznamu

Mezi výzvu a položky vstupuje skupina:

```csharp
VyzvyPanelZaznamSkupinaViewModel
    int ZaznamId
    string? CisloViditelne
    string? Nazev
    IReadOnlyList<VyzvyPanelPolozkaViewModel> Polozky
```

Buffer i výzva sdílí stejný tvar, takže tělo pravého panelu je **jeden partial pro obě**.

Jeden projektový záznam se může objevit pod více výzvami — každé jeho PNF může být v jiné výzvě. Jedno PNF je vždy nejvýš v jedné výzvě; hlídá to filtered unique index `ux_zaznam_externi_odkazy_cislo_in_vyzve`.

### 7.2 Řazení

Skupiny se řadí sdílenou utilitou [RecordDisplayOrdering](../../../PmTracker.Web/Services/Common/RecordDisplayOrdering.cs) — `CategoryOrder → PartA → PartB → CisloZaznamu` — aby pořadí záznamů odpovídalo záložce Záznamy i tisku. Uvnitř skupiny se PNF řadí podle `ZaznamExterniOdkaz.Id` ASC (stabilní pořadí přidání), jako dnes.

`VyzvyPanelBuilder` musí navíc načíst název záznamu a řadicí klíče; dnes bere jen `CisloViditelne`.

### 7.3 Filtr roku ve službě

`GetVyzvyAsync(projektId, rok, ct)` dostává rok. Přibývá `GetRokyAsync(projektId, ct)` pro nabídku selectu. `GetBufferAsync` se nemění — buffer je bez roku.

## 8. Přesuny PNF

### 8.1 Dvě cesty ke stejné akci

**Drag & drop** je primární: PNF řádky jsou `draggable`, drop cíle jsou dlaždice v railu. Vizuální zpětná vazba třídou na cílové dlaždici.

**Kontextové menu `⋯`** u každého PNF je rovnocenná druhá cesta — „Přesunout do bufferu" a „Přesunout do výzvy N/RRRR" pro každou nezamčenou výzvu. Není to náhradní řešení pro případ, že by drag & drop nevyšel; je to přístupná cesta pro ovládání klávesnicí a jistota, když je rail odscrollovaný jinam.

Menu stojí na existující sdílené floating vrstvě [ui/floating.js](../../../PmTracker.Web/wwwroot/js/modules/ui/floating.js) (`mountFloatingPanel`, `closeAllFloatingPanels`), stejně jako menu na kartě záznamu. Získává tím zdarma mousedown-origin guard proti falešnému zavření při tažení myší, Escape a zavírání ostatních otevřených panelů. Vlastní menu se nepíše.

Obě cesty končí na stávajícím `POST /vyzvy/prerdit`.

### 8.2 Obrazovka nesmí uskočit

Po přesunu **zůstává vybraná ta dlaždice, na které uživatel je**. Pohled se nikdy nepřepne na cílovou výzvu.

Realizace: po úspěšném POSTu se překreslí rail a panely, přičemž se **zachová vybraná dlaždice i pozice odscrollování**. Aktualizuje se seznam PNF v aktuálním panelu a počty na dlaždicích.

Zvažovanou alternativou bylo přesunout řádek v DOM čistě na klientu bez dotazu na server. Zamítnuto: vložení PNF do správné skupiny cílového panelu na správné místo by vyžadovalo zopakovat serverové řazení záznamů v JavaScriptu, tedy druhou kopii logiky, která se rozejde.

Při chybě se nepřesouvá nic a zobrazí se hláška.

### 8.3 Zamčené výzvy

Pravidla zůstávají v platnosti beze změny: s obsahem výzvy ve stavu **Odesláno** ani **Zrušeno** nelze hýbat.

- Dlaždice zamčené výzvy **nepřijme drop**.
- PNF v zamčené výzvě **nemá kontextové menu**.
- Zamčená výzva se nenabízí jako cíl v menu jiných PNF.
- Serverové guardy (`VyzvaIsLocked`, cross-project kontrola) v [VyzvaService.Assignment.cs](../../../PmTracker.Web/Services/Vyzvy/VyzvaService.Assignment.cs) zůstávají — UI je jen první linie.

Zrušená výzva je navíc **vždy prázdná**: přechod do `Zruseno` v [VyzvaService.Transitions.cs](../../../PmTracker.Web/Services/Vyzvy/VyzvaService.Transitions.cs) vrací všechna její PNF do bufferu. Nemá tedy obsah, se kterým by šlo hýbat, a serverová kontrola cíle (`cil.Stav != Priprava`) ji zároveň nepustí jako cíl přesunu.

### 8.4 Switch na externí vazbě míří vždy do bufferu

Dnes `NastavitZaradidAsync` při zapnutí switche "Zařadit do další výzvy" PNF **rovnou přiřadí do nejstarší rozpracované výzvy** a do bufferu ho pošle jen tehdy, když žádná rozpracovaná výzva neexistuje. To odpovídalo světu, kde se výzva zakládala nasypáním celého bufferu.

Nově výzva vzniká prázdná a plní se výhradně vědomým přesunem, takže by automatické přiřazení plnilo výzvu za zády uživatele a buffer by zůstával prázdný. **Switch proto nově vždy končí v bufferu** (`VyzvaId = null`, `ZaradidDoVyzvy = true`); zařazení do konkrétní výzvy je vždy explicitní akce tažením nebo přes menu.

Vypnutí switche se nemění: PNF vypadne z bufferu i z rozpracované výzvy, u odeslané výzvy zůstává switch zamčený.

### 8.5 Zrušený modal

Modal „Upravit přiřazení" se ruší celý — kontextové menu a drag & drop pokrývají totéž a modal by byl třetí cesta ke stejné akci. Odpadá s ním i jeho nedořešené zavírání přes backdrop.

## 9. Tisk výzvy

Tlačítko **„Tisk výzvy"** v hlavičce panelu vygeneruje výzvu jako dokument podle
standardního formuláře resortu. Předlohou je reálná výzva
`20260206_N_8201_Vyzva_c_2_2026_EIS.docx` (rozbor 2026-09-07).

### 9.1 Kde to žije

Výzva se přidává jako čtvrtá entita do [ExportController](../../../PmTracker.Web/Controllers/ExportController.cs)
vedle projektu, jednání a úkolu:

```
GET /Export/Vyzva/{vyzvaId}/Tisk?projektId={id}   → PDF (inline)
GET /Export/Vyzva/{vyzvaId}/Word?projektId={id}   → .docx
```

`projektId` v query je povinná součást autorizačního kontextu, ne ozdoba: bez něj by
project-scoped policy udělala tichý globální check a per-projektovým rolím vrátila 403.
Guard proti spoofingu je stejný jako u jednání — je-li `projektId` zadané, musí odpovídat
skutečnému projektu výzvy, jinak `NotFound`.

Tlačítko nese `data-print-trigger` s oběma URL, takže se otevře **tentýž chooser formátu**
jako u ostatních tisků ([ui/print.js](../../../PmTracker.Web/wwwroot/js/modules/ui/print.js)).
Vlastní dialog se nepíše.

**Oprávnění:** jediný klíč `vyzvy.word.export` pro obě cesty. V seedu už existuje a je
přiřazený právě šesti rolím z §4.2 (SUPERADMIN, APP_ADMIN, VLASTNIK_PROJEKTU, ADM_PROJ,
PROJ_MAN, GEST). Zakládat druhý klíč jen kvůli PDF by znamenalo šest nových řádků seedu
a migraci bez jakéhokoli rozdílu v chování. Popisek klíče se upraví, ať neříká jen „Word".

### 9.2 Jedna projekce, dva renderery

Word i PDF se staví ze **stejné projekce** `VyzvaExportViewModel` — stejný princip jako
u ostatních exportů, aby se obsah obou formátů nemohl rozejít.

Sdílí se infrastruktura, ne model: PDF jde přes `IViewRenderer` → `IPdfRenderer` (Chromium),
Word přes OpenXML s pomocnými prvky z
[OpenXmlWordElements](../../../PmTracker.Web/Services/Export/OpenXmlWordElements.cs).
Stávající `PdfExportTemplateViewModel` se **nepoužije** — je stavěný pro jednání a seznam
záznamů, kdežto výzva má jinou strukturu (právní preambule, kapitoly per PNF, kalkulační
tabulky, součty s DPH, podpisová doložka). Ohýbat ho by poškodilo obě strany.

### 9.3 Struktura dokumentu

| # | Část | Obsah |
|---|---|---|
| — | Záhlaví stránky | `Příloha č.1 k Čj. …` |
| — | Adresa zadavatele | statický blok resortu |
| — | Nadpis | `Výzva k poskytnutí plnění č. {kód} pro {IS}` |
| — | Preambule | odkaz na § 134 ZZVZ a rámcovou dohodu |
| 1 | Popis předmětu dílčí VZ | tabulka Poř. č. \| Č. úkolu VP \| Název požadavku \| Č. HTL |
| 2 | Počet člověkohodin | kapitola za každé PNF: název, popis, vazba na PMP, kalkulační tabulka A–D |
| 3 | Celková cena | souhrn individuálních úprav, licenční rozšíření, celkový součet |
| 4 | Identifikační údaje nabyvatele | statický blok |
| 5 | Termín a místo plnění | termín + místo plnění (viz §9.4) |
| 6 | Lhůta pro potvrzení | statický text (5 dnů dle čl. IV) |
| 7 | Podpisová doložka | dvě jména a funkce |

### 9.4 Mapování dat

| V dokumentu | Zdroj |
|---|---|
| Číslo výzvy | `VyzvaEntity.Kod` |
| IS v nadpisu | první token místa plnění (`FIS (EIS): VZ 8201` → `FIS`) |
| Číslo rámcové dohody | `CisloRamcoveSmlouvySnapshot` |
| Místo plnění | výzva v Přípravě: `ProjektEntity.MistoPlneni` (aktuální); jinak `MistoPlneniSnapshot`, uložený při opuštění Přípravy (uživatel 2026-10-07, `VyzvaMistoPlneni`) |
| Poř. č. | `a`, `b`, `c`, … dle pořadí v seznamu |
| Č. úkolu VP | `ProjektovyZaznam.CisloViditelne` |
| Název požadavku | `ProjektovyZaznam.Nazev` |
| Č. HTL | `ZaznamExterniOdkaz.Cislo` (PNF) |
| Popis kapitoly | `HOT_ZAZNAMY.popis` |
| Vazba na PMP č. | PMP externí vazba téhož záznamu |

Pořadí požadavků řídí sdílené `RecordDisplayOrdering` — stejné jako v záložce Záznamy
a v panelu Výzev, aby uživatel nepotkal třetí pořadí.

### 9.5 Kalkulace

Kalkulační tabulka každé kapitoly se plní z `HOT_KALKULACE`. Napojení už v aplikaci
existuje (`GetAkceptovaneKalkulaceAsync` vrací nejvyšší verzi se stavem `Akceptováno`),
ale **dosud ho nikdo nevolal** — tisk výzvy je jeho první konzument. Pro vyjádření
zůstávají kalkulace nadále nezpracované.

Klíčem není šestimístné číslo PNF, ale GINIS PID: cesta vede
`ZaznamExterniOdkaz.Cislo` → `HOT_ZAZNAMY.pid` → `HOT_KALKULACE.pid`. `HotZaznamDto`
proto musí nově vystavit `Pid` — v entitě je, v DTO chybí.

Řádky tabulky jsou **napevno** a odpovídají sloupcům v databázi:

| Kód | Název činnosti | Rozsah | Sazba | Cena |
|---|---|---|---|---|
| A | Analýza | `pracnost_a` | `sazba_a` | `cena_a` |
| B | Programové úpravy | `pracnost_p` | `sazba_p` | `cena_p` |
| C | Testování | `pracnost_t` | `sazba_t` | `cena_t` |
| D | Implementace | `pracnost_i` | `sazba_i` | `cena_i` |

Pracnost je v hodinách, sazba v Kč za hodinu, cena je jejich součin. **Všechny ceny
v databázi jsou bez DPH.** Sloupce DPH a s DPH dopočítává tisk sazbou **21 %**, kterou
formulář sám uvádí v záhlaví. Licenční rozšíření jede z `pocet_l`, `sazba_l`, `cena_l`.

Když je ServiceDesk vypnutý (`DisabledTicketingQueryService`) nebo kalkulace k PNF
neexistuje, tabulka se vykreslí prázdná se strukturou A–D. Dokument se kvůli chybějícím
kalkulacím nesmí rozbít.

### 9.6 Údaje, které aplikace nemá

Číslo jednací, název veřejné zakázky, termín plnění a jména podepsaných v aplikaci
nejsou a **nezavádějí se**. Tisk je vyplní zástupnými `X` ve tvaru daného pole:

```
Příloha č.1 k Čj. MO XXXXXX/XXXX-XXXX
Čj. XXXXXXXXXX                V Praze dne XX. XX. XXXX
veřejné zakázky „XXXXXXXXXXXXXXXXXXXX" pořadové číslo {NN}/{RRRR}
Termín pro splnění dílčí veřejné zakázky do: XX. XX. XXXX
Za nabyvatele: XXXXXXXXXXXXX        Za dodavatele: XXXXXXXXXXXXX
```

Uživatel si je přepíše ve Wordu při zadávání výzvy do spisové služby. Až se ukáže, že to
vadí, doplní se jako pole na výzvě — teď by to byla předčasná složitost.

### 9.7 Prázdná výzva

Prázdná výzva se **vytiskne** s prázdnou tabulkou požadavků. Odmítat tisk by uživateli
bralo možnost připravit si dokument dopředu; výzva navíc nově vzniká prázdná vždy (§5.2).

## 10. Verze aplikace

Posledním krokem tématu je povýšení verze **0.8 → 0.9** — pilotní verze před ostrým vydáním 1.0.

Verze má jediný zdroj pravdy: `<AppVersion>` v [PmTracker.Web.csproj:6](../../../PmTracker.Web/PmTracker.Web.csproj). `Version` i `InformationalVersion` se z ní odvozují a do patičky ji promítá `IApplicationVersionProvider`. Mění se tedy jedno místo.

Bump se dělá **až po dokončení a ověření všech ostatních změn**, aby verze 0.9 označovala hotový celek.

## 11. Co se odstraňuje

| Co | Kde |
|---|---|
| `VyzvaCodeGenerator.DalsiPoradoveVRoce()` | `Services/Vyzvy/VyzvaCodeGenerator.cs` |
| Chybový kód `BufferEmpty` | `Services/Vyzvy/VyzvaErrors.cs` |
| Blokace zakládání prázdným bufferem | `VyzvyPanelBuilder` |
| Modal přiřazení — view, JS, VM, endpoint | `Views/Vyzvy/ReassignModal.cshtml`, `js/modules/vyzvy/reassignModal.js`, `ReassignModalViewModel`, `GET /vyzvy/reassign-modal` |
| Karty starého layoutu | `_VyzvyPanel.BufferCard.cshtml`, `_VyzvyPanel.VyzvaCard.cshtml` |
| Klíč `dashboard.vyzvy.view` + 11 přiřazení | `PermissionSeedConfiguration.cs` |
| Záložka a panel Výzvy na dashboardu | `ProjectDashboard/Index.cshtml`, `ProjectDashboardController.GetVyzvyPanel` |

## 12. Testy

**Jednotkové:**

- Zakládání s ručním číslem: platné číslo, duplicita, číslo mimo rozsah 1–999, prázdný buffer projde, výzva vznikne prázdná.
- `VyzvyPanelBuilder`: seskupení PNF podle záznamu, řazení skupin dle `RecordDisplayOrdering`, filtr roku, buffer mimo filtr roku, počty na dlaždicích.
- Povolené cíle přesunu: zamčená výzva se nenabízí, cizí projekt se odmítne.
- Controller: mapování nových chybových kódů do JSON.

**Upravit:** `VyzvaServiceFoundingTests`, `VyzvyPanelBuilderTests`, `VyzvyControllerTests`, `VyzvaCodeGeneratorTests`, plus tři authz testy kvůli odstraněnému klíči a přidané roli `GEST`.

**Ruční ověření** (dělá uživatel): záložka na správném místě v menu, člen projektu bez práv vidí obsah bez ovládání, přesun PNF neuskočí z aktuálního pohledu, zamčenou výzvou nejde hýbat.

## 13. Otevřené body

1. **Formulář tisku výzvy** — upřesní se na konci tématu. Tento spec končí u tlačítka bez funkce.
2. **Prázdná výzva při tisku** — viz §9.
3. **Wiki stránka Výzvy** popisuje neexistující funkce (kódy „VZ-001", pole dodavatel, stavy „Schválena / V realizaci", oprávnění `vyzvy.edit`/`approve`/`delete`). Přepis až po dokončení funkčních změn, aby se nepsal dvakrát.

## 14. Akceptační kritéria

1. Záložka Výzvy je v projektovém menu mezi Návrhy a Dashboard; na dashboardu už není.
2. Záložku otevře každý, kdo vidí projekt, bez ohledu na oprávnění.
3. Vlastník projektu, Gestor projektu, Projektový manažer a Administrátor projektu mohou zakládat, měnit a přesouvat; ostatní vidí obsah bez ovládacích prvků.
4. Novou výzvu lze založit s ručně zadaným číslem; duplicitní číslo a číslo mimo 1–999 se odmítne se srozumitelnou hláškou.
5. Výzva vzniká prázdná a lze ji založit i při prázdném bufferu.
6. Panel má rozvržení 20/80 s railem vlevo.
7. Výběr roku filtruje výzvy; buffer je první v seznamu bez ohledu na rok.
8. Výzvy jsou v railu řazené od nejnovější k nejstarší.
9. PNF jsou v pravém panelu seskupené podle projektového záznamu.
10. PNF lze přesunout tažením na dlaždici i přes kontextové menu.
11. Po přesunu zůstává vybraná stejná dlaždice a pozice odscrollování; aktualizuje se seznam PNF a počty.
12. S obsahem výzvy ve stavu Odesláno ani Zrušeno nelze hýbat žádnou z cest.
13. Tlačítko „Tisk výzvy" je v hlavičce výzvy a zatím nedělá nic.
14. Zapnutí switche u PNF ho vždy pošle do bufferu, nikdy rovnou do výzvy.
15. Verze aplikace v patičce hlásí 0.9.
16. Build bez chyb a varování, všechny testy procházejí.

---

**Konec specu.** Po schválení navazuje `superpowers:writing-plans` s implementačním plánem.
