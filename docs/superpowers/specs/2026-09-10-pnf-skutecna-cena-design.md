# Výzvy: oprava bufferu, skutečná cena z kalkulace a tisk podle finálního vzoru

**Datum:** 2026-09-10
**Stav:** SCHVÁLENO — odpovědi uživatele z 2026-09-10 jsou zapracované jako rozhodnutí.
**Plán:** [2026-09-10-vyzva-cena-vzor.md](../plans/2026-09-10-vyzva-cena-vzor.md)

Tři části, implementují se v tomto pořadí:

- **Část 0** — oprava vad: PNF se po uložení záznamu vrací do bufferu a související vady.
- **Část A** — skutečná cena z akceptované kalkulace v aplikaci.
- **Část B** — tisk výzvy podle finálního vzoru.

Část 0 jde první, protože dnes tiše rozbíjí i odeslané výzvy.

---

# Část 0 — Oprava: PNF se po uložení záznamu vrací do bufferu

## 0.1 Vada 1: uložení záznamu vytáhne PNF z výzvy

Při uložení záznamu se `VyzvaId` každé existující vazby přepíše hodnotou odvozenou z textového
pole `Vyzva` (kód výzvy) — `ResolveVyzvaIdAsync(link.Vyzva)` v
[RecordService.SaveRecord.cs](../../../PmTracker.Web/Services/RecordService.SaveRecord.cs).
Formulář to pole od 2026-04-20 neposílá: commity `c998313` („switch místo select") a `e464038`
(„Vyzva (string) → VyzvaId + VyzvaKod + ZaradidDoVyzvy") změnily formulář, server dál četl pole,
které přestalo existovat. `link.Vyzva` je vždy `null`, takže `VyzvaId` spadne na `null`.
`ZaradidDoVyzvy` zůstane `true` a vazba odpovídá definici bufferu
(`ZaradidDoVyzvy && VyzvaId == null`, [VyzvaQueries.cs](../../../PmTracker.Web/Services/Vyzvy/VyzvaQueries.cs)).

Rozsah:

- Stane se to při **každém** uložení záznamu v editoru, u všech jeho PNF ve výzvě. Změna ceny
  je jen chvíle, kdy si toho uživatel všiml.
- **Obchází zámek odeslané výzvy.** `NastavitZaradidAsync` i `PrerditPnfAsync` odeslanou výzvu
  odmítnou, UPSERT ji nekontroluje.
- Netýká se uložení jen záložky Harmonogram (UPSERT nevolá) ani schválení návrhu založení (vazby
  jsou nové).

Proč to testy nechytily: žádný test neověřuje, že uložení záznamu zachová `VyzvaId`.

## 0.2 Vada 2: nová PNF se před prvním uložením do bufferu nedostane

- Přepínač „Zařadit" u nové vazby má `data-externi-odkaz-id=""` a `handleSwitch` hned končí
  ([switchController.js](../../../PmTracker.Web/wwwroot/js/modules/vyzvy/switchController.js)) —
  neodejde požadavek a skryté pole zůstane `false`.
- Větev UPSERTu pro novou vazbu `ZaradidDoVyzvy` nenastavuje.

Přepínač se vizuálně přepne, po uložení je vypnutý. Uživatel musí záznam uložit, znovu otevřít
a přepnout znovu.

## 0.3 Vada 3: schválení návrhu založení ztratí text požadavku

Nalezeno při přípravě plánu. `RecordProposalPayloadMapper.BuildSaveCommand` kopíruje z payloadu
vazby bez `Pozadavek` a `ZaradidDoVyzvy`. Detail návrhu text zobrazí (mapper pro zobrazení ho má
od bloku 5 plánu 2026-09-08), ale po schválení se do záznamu neuloží. Sourozenec toho bloku —
doplnilo se jen jedno ze dvou míst.

## 0.4 Rozhodnutí

- **R0.1** Zařazení do výzvy vlastní `VyzvaService` (přepínač, přesun, přechody stavů). Uložení
  záznamu u existující vazby na `VyzvaId` ani `ZaradidDoVyzvy` **nesahá** — stejný vzor jako čtyři
  datumy dodávky, které vlastní harvest.
- **R0.2** Nová vazba přebírá `ZaradidDoVyzvy` z formuláře, a to jen když je PNF a uživatel má
  `vyzvy.pnf.assign` na projektu záznamu — stejné oprávnění, jaké hlídá endpoint přepínače.
  `VyzvaId` zůstává `null`, PNF skončí v bufferu. Do bufferu tak jdou jen nově zaškrtnuté PNF
  (zadání uživatele).
- **R0.3** Přepínač u nové vazby jen přepne skryté pole a ukáže „Po uložení půjde do bufferu".
  AJAX volá až u uložené vazby.
- **R0.4** Mrtvé pole `SaveRecordExterniVazbaCommand.Vyzva` mizí z příkazu, validace, UPSERTu
  i mapperu; `ResolveVyzvaIdAsync` se maže. Staré návrhy s klíčem `Vyzva` v JSON se načtou dál —
  System.Text.Json neznámé klíče ignoruje.
- **R0.5** `BuildSaveCommand` kopíruje `Pozadavek` i `ZaradidDoVyzvy`.

## 0.5 Testy

- **Integration:** uložení záznamu zachová `VyzvaId` i `ZaradidDoVyzvy` — rozpracovaná i odeslaná
  výzva, se změnou předpokládané ceny (scénář z hlášení). Ověřit červeně-zeleně.
- **Pin:** UPSERT u existující vazby nepřiřazuje `VyzvaId` ani `ZaradidDoVyzvy`; nová vazba bere
  `ZaradidDoVyzvy` s kontrolou oprávnění. Novou vazbu Integration sada přes uložení vytvořit
  nedokáže (ServiceDesk je vypnutý a validace nový tiket odmítne), proto pin.
- **JS pin:** přepínač u nové vazby nevolá `set-zaradid`.
- **Unit:** mapper návrhu kopíruje `Pozadavek` a `ZaradidDoVyzvy`.

---

# Část A — Skutečná cena z kalkulace

## A1. Zadání (uživatel 2026-09-10)

- **Předpokládaná cena** u PNF zůstává, jak je, a nikdy se nemaže. Poslouží v dalším rozvoji pro
  generování připravovaného rozvoje. Je to jen číslo — s DPH ani bez se u ní neuvádí.
- Jakmile má PNF **akceptovanou kalkulaci**, aplikace ukáže **skutečnou cenu** místo předpokládané.
- Předpokládaná cena se nedostane **do PDF ani do Wordu**.

## A2. Kde se cena zobrazuje a co se mění

| # | Místo | Soubor | Změna |
|---|---|---|---|
| 1 | Chip na kartě záznamu v seznamu | `_ZaznamDetailPartial.cshtml` přes `_ZaznamPartial` | skutečná, jinak předpokládaná „(předp.)" |
| 2 | Chip na stránce záznamu | týž partial přes `ZaznamDetailPage.cshtml` | totéž |
| 3 | Chip v líně načteném detailu karty | týž partial, `ZaznamyController.Partials.cs` | totéž |
| 4 | Karta PNF na záložce Výzvy | `_VyzvyPane.cshtml` | totéž |
| 5 | Patička výzvy na záložce Výzvy | `_VyzvyPane.cshtml`, `VyzvyPanelBuilder` | součet skutečných, viz A3 R7 |
| 6 | Export záznamů do PDF/Wordu | `ExportProjectionBuilders.FormatExternalLinkDisplay` | **jen skutečná**; bez ní žádná cena |
| 7 | Editor externí vazby | `_EditZaznamExternalPanel.cshtml` | beze změny |
| 8 | Detail návrhu | `RecordProposalPayloadMapper` | beze změny |

Chip má dvě varianty — odkaz `chip-link` a prostý `span`. Změna platí pro obě a pro tooltip.

Dnes patička ve Výzvách sčítá předpokládané ceny a tištěná výzva skutečné — u téže výzvy dvě
různá „celkem". Převod bodu 5 to srovná.

## A3. Rozhodnutí

- **R1** Předpokládaná cena, její sloupec, editor i ukládání se nemění. Skutečná cena se ukládá
  vedle.
- **R2** Skutečná cena = `HOT_KALKULACE.cena` — celková částka včetně licence, bez DPH (uživatel:
  „chip může ukazovat i cenu licence, součet, celkovou cenu"). Kalkulace se vybírá stejně jako pro
  výzvu: zákazový seznam stavů akceptace, při shodě vyšší `id`.
- **R3** DPH je 21 % napevno v kódu (`VyzvaExportViewModel.DphSazba`). Beze změny.
- **R4 Snímek vedle harvestu, ne v něm.** Skutečná cena se ukládá jako snímek do
  `zaznam_externi_odkazy`. Plní ho samostatná služba `IKalkulaceSnapshotService`, **ne**
  `PerTicketMetadataSyncService` — ta běží až za rychlou cestou fingerprintu v
  `VyjadreniHarvestService.HarvestWithFingerprintAsync`. Akceptace kalkulace mění `HOT_KALKULACE`,
  ne fingerprint tiketu, takže by se snímek nikdy neobnovil. Zapojení:
  - `HarvestSingleTicketAsync` (reaktivní) — pro jednu vazbu, **před** rozhodnutím podle
    fingerprintu,
  - `HarvestScopeAsync` (periodický) — dávkově pro celý rozsah po skončení smyčky.
- **R5 Obnova po uložení záznamu je zadarmo.** `SaveRecord` už po commitu plánuje harvest každé
  vazby záznamu (`ScheduleHarvestAsync`). Snímek tedy obnoví reaktivní harvest vlastního záznamu
  bez nového spouštění. Náklad: dva malé dotazy do ServiceDesku na PNF vazbu (`pid` z
  `HOT_ZAZNAMY`, kalkulace z `HOT_KALKULACE`). Tím je splněná podmínka uživatele „jen pro svůj
  záznam a jen pokud je to levné".
- **R6 Pravidla snímku:**
  - jen PNF;
  - tiket v `HOT_ZAZNAMY` nenalezen (vypnutý ServiceDesk nebo smazaný tiket) → snímek beze změny;
  - tiket nalezen, akceptovaná kalkulace není → snímek se **vynuluje** (kalkulace mohla být
    odvolána);
  - jinak se zapíše `cena`, `id` kalkulace a čas.
- **R7 Zobrazení:**
  - skutečná cena bez přípony, předpokládaná s příponou „(předp.)";
  - tooltip chipu ukáže obě, pokud jsou známé;
  - patička výzvy: „CELKEM" = součet skutečných, u PNF bez skutečné se dopočte předpokládaná
    a pod součtem stojí „z toho N PNF jen s předpokládanou cenou";
  - atribut `data-cena-zdroj="kalkulace|predpokladana"` kvůli testům.
- **R8** Uložení záznamu snímek nepíše — hlídá pin.

## A4. Data a migrace

Skript `db_upgrade_1_4_3_externi_odkaz_kalkulace.sql`, idempotentní. Zapsat do
`docs/technical/06-database-bootstrap-migrations.md` (jinak spadne Integration sada) i do
`db_check_applied_upgrades.sql`.

| Sloupec | Typ | Význam |
|---|---|---|
| `kalkulace_cena` | `DECIMAL(18,2) NULL` | `HOT_KALKULACE.cena` vybrané kalkulace |
| `kalkulace_id` | `BIGINT NULL` | `HOT_KALKULACE.id` — ze které kalkulace cena je |
| `kalkulace_nacteno` | `DATETIME2 NULL` | kdy se snímek naposledy **změnil** |

Stávající řádky zůstanou `NULL` a doplní je první běh synchronizace.

Čas se zapisuje jen při změně ceny nebo kalkulace. Kdyby se psal při každém běhu, periodická
synchronizace by pokaždé přepsala řádky všech PNF v databázi.

## A5. Testy

- **Unit, snímek:** akceptovaná kalkulace zapíše cenu a id; tiket bez kalkulace snímek vynuluje;
  nenalezený tiket snímek nechá; ne-PNF vazba se nečte.
- **Unit, harvest:** reaktivní cesta zapíše snímek i při shodném fingerprintu.
- **Pin:** UPSERT nesahá na `Kalkulace*`.
- **Api render:** chip se skutečnou i předpokládanou cenou (obě varianty), karta PNF ve Výzvách,
  patička se smíšeným součtem. Kotvit na `data-cena-zdroj`, ne na český text.
- **Unit, export záznamů:** skutečná cena se vypíše, předpokládaná nikdy.

---

# Část B — Tisk výzvy podle finálního vzoru

**Předloha:** `2027xxxx_N_8201_Vyzva_c_x_2027_EIS.docx` v kořeni repa, gitignorovaná (`*.docx`
a explicitní `*_Vyzva_*.docx`). Rozbor 2026-09-10 z XML dokumentu včetně 13 komentářů autora vzoru.
Nahrazuje předlohu `20260206_…_2_2026_EIS.docx` (spec 2026-09-07 §9).

**Zásada (uživatel):** struktury ve výzvě se drž přesně — až na chyby vzoru, které se opravují
(B9). Word i náhled staví z téže projekce `VyzvaExportViewModel`.

## B1. Stránka a písmo

- A4, okraje 2,5 cm, záhlaví a zápatí 1,25 cm od okraje.
- Times New Roman všude. **Tělo 12 bodů**, **tabulky 10 bodů**. V sekci 2 jsou 10bodové i řádek
  „Číslo úkolu VP EIS" a štítky „Individuální úpravy" / „Licenční rozšíření". Hlavička úřadu:
  1. řádek 16 b. tučně, 2. řádek tučně, 3. řádek 10 b.
- Uzavírá otevřený bod plánu 2026-09-08: dnešní export sází tělo v 10 bodech a to je chyba.

## B2. Záhlaví a zápatí

- **Word:** „Příloha č.1 k Čj. MO" je záhlaví stránky, zarovnané vpravo, číslo jednací prázdné
  k doplnění. Zápatí nese číslo stránky (pole `PAGE`) na střed, 10 b.
- **Náhled PDF:** renderer má pro všechny exporty sdílené prázdné záhlaví a sdílené zápatí
  „Strana X z Y". Náhled proto nese „Příloha č.1 k Čj. MO" jako první řádek, číslo stránky dává
  sdílené zápatí. Rozšiřovat renderer jen kvůli náhledu se nevyplatí — odevzdává se Word.

## B3. Úvodní část

| Prvek | Výsledek |
|---|---|
| „Čj. … V Praze dne" | jen popisky, hodnoty prázdné |
| Nadpis „Výzva k poskytnutí plnění č. … pro …" | zarovnání do bloku |
| Úvodní odstavec | jako vzor, včetně „zastoupena ředitelem odboru … Ing. Petrem ZÁBORCEM"; název zákona opravený na „o zadávání veřejných zakázek" |
| „k poskytnutí plnění" | tučně, na střed |
| Věta s VZ | název „Technické zhodnocení APV a DZ" a pořadové číslo tučně |

## B4. Sekce 1 — Popis předmětu dílčí VZ

Tabulka Poř. č. | Č. úkolu VP | Název požadavku | Č. HTL:

- **Poř. č. římsky s tečkou** — „I.", „II.", … (komentář autora).
- **Č. úkolu VP s prefixem „RU"** — „RU867-5" (komentář autora). Prefix před `CisloViditelne`
  záznamu (`{jednání}-{pořadí}` nebo `{číslo}`). Pojistka: když už hodnota „RU" začíná, znovu se
  nepřidá. Bez čísla úkolu zůstane buňka prázdná.
- Č. HTL = číslo PNF.

Pod tabulkou:

1. Pevný odstavec doslova ze vzoru: „Podrobné návrhy požadavků jsou součástí příslušného protokolu
   (HotLine – uvedená v tabulce shora) a specifikace. Byly analyzovány dodavatelem a jejich užitnost
   je posuzována zadavatelem, vedením projektu EIS, Řídícím výborem FIS, případně dalšími odborníky.
   Požadavky jsou posuzovány jednotlivými vedoucími subsystémů FIS/ISSP a příslušnými metodiky.
   Požadavky jsou schváleny vedením projektu FIS/ISSP (VP EIS). Čísla úkolů jsou uvedena také
   v souhrnné tabulce shora. Dále je uvedena stručná anotace požadavků."
2. Tučně „Stručné popisy požadavků:".
3. Pro každý požadavek v pořadí tabulky:
   - nadpis = název požadavku, tučně 12 b., číslovaný seznam Wordu římsky,
   - „Číslo úkolu VP EIS: RU867-5.",
   - text požadavku z vazby (rich text včetně seznamů),
   - „Bližší podrobnosti jsou uvedeny v PNF 358310.".

**„Vazba na PMP" se ve výzvě neuvádí nikde** (uživatel: jediný výskyt ve vzoru je chyba).

## B5. Sekce 2 — Počet člověkohodin

Pro každý požadavek nadpis (název, tučně 12 b., číslovaný seznam římsky) a „Číslo úkolu VP EIS:
RU….“ (10 b.). Pak tabulky podle kalkulace:

| Kalkulace má | Vytiskne se |
|---|---|
| jen činnosti A–D | „Individuální úpravy" + tabulka činností |
| jen licenci | „Licenční rozšíření" + tabulka licencí |
| obojí | nejdřív činnosti, pak licence |
| žádná akceptovaná kalkulace | **žádná tabulka** |

„Má činnosti" = součet `cena_a` až `cena_i` > 0 (nulový řádek uvnitř vyplněné tabulky zůstává).
„Má licenci" = `cena_l` > 0.

**Tabulka činností:** hlavička beze změny; řádek součtu tři prázdné buňky, „CELKEM" ve 4. buňce,
pak tři částky.

**Tabulka licencí:** Kód činnosti | Název činnosti | POL | PPOL | Cena v Kč bez DPH | DPH v Kč |
Cena v Kč s DPH.

- řádky z `rozpad_licence` — HTML tabulka `<tr><td>název</td><td>cena</td></tr>`; kód je pořadí
  1, 2, …; DPH dopočtem 21 %;
- **POL a PPOL vždy prázdné** — v databázi pro ně sloupec není, uživatel je doplňuje ručně;
- součet „CELKEM licenční rozšíření" přes 4 sloučené sloupce, pak tři částky;
- `cena_l` je kontrolní součet. Když se s rozpadem rozejde, tiskne se rozpad a rozdíl se zaloguje
  jako varování. Když rozpad chybí nebo nejde přečíst, jeden řádek „Licenční rozšíření" s `cena_l`.

## B6. Sekce 3 — Celková cena

- **„Individuální úpravy:"** — jen požadavky s činnostmi, Poř. č. převzaté ze sekce 1
  (nepřečíslovává se), před součty prázdný řádek, součty „Celkem | bez DPH", „| DPH 21 %",
  „| s DPH".
- **„Licenční rozšíření:"** — jen požadavky s licencí, stejný tvar. Tiskne se vždy, i bez licencí.
- **Souhrnná tabulka** — beze změny (Úprava APV a DZ celkem, Licenční rozšíření APV a DZ celkem,
  CELKEM).

## B7. Sekce 4 až 7

- **4** Identifikační údaje — beze změny.
- **5** „Termín pro splnění dílčí veřejné zakázky do:" a místo pro datum prázdné (komentář autora
  „Nechat volné"). Místo plnění tučně z pole projektu (snapshot výzvy), beze změny obsahu.
- **6** Lhůta — beze změny.
- **7** Podpisy: „Za nabyvatele:" / „Za dodavatele:", tečkované řádky, pak **„Ing. Petr ZÁBOREC"
  / „ředitel"** a **„Ing. Břetislav MOC" / „předseda správní rady"** — uživatel tato dvě jména
  ponechává. Jiná jména ani hodnoty aplikace nedoplňuje.

## B8. Číslování, seznamy a zalomení

- Nadpisy sekcí 1.–7.: číslovaný seznam Wordu arabsky.
- Nadpisy požadavků v sekci 1 a v sekci 2: dvě samostatné řady římsky, obě od I.
- Seznamy v textu požadavku: skutečné seznamy Wordu (odrážky i číslované), ne znak „•".
  Každý číslovaný seznam začíná od 1.
- Zalomení stránky před sekcemi 2, 3 a 4. Nadpis požadavku drží s tabulkou (`keepNext`), řádek
  tabulky se nedělí (`cantSplit`). Ruční zalomení z konkrétní výzvy se nepřebírají.
- Čísla jednotně `N2` s oddělovačem tisíců.

## B9. Chyby vzoru, které se opravují (uživatel: „oprav ty chyby")

| Ve vzoru | Správně |
|---|---|
| „o zadávání veřejných zakázkách" | „o zadávání veřejných zakázek" |
| „poř. č." v hlavičce licencí | „Poř. č." |
| „DPH 21%" | „DPH 21 %" |
| „Vazba na PMP č. …" u jednoho požadavku | vždy „Bližší podrobnosti jsou uvedeny v PNF …" |
| čísla bez oddělovače tisíců | `N2` |

## B10. Dopad na kód

- `VyzvaExportBuilder`: římské pořadí, prefix „RU", příznaky činností a licence, položky licence
  z `rozpad_licence`, bez vazby na PMP.
- `RozpadLicence` v projekci — dnes se v builderu zahazuje.
- `RichTextHtmlParser`: odstavec nese druh seznamu a identitu seznamu; export záznamu se nemění.
- `OpenXmlVyzvaExportService`: stránka, styly, záhlaví a zápatí, část `numbering`, nové pořadí
  sekcí, tabulka licencí.
- `VyzvaTemplate.cshtml` a `pdf-export.css`: totéž pro náhled.
- `OpenXmlWordElements.CreateRunProperties`: prvky běhu v pořadí podle schématu Office
  (`b`, `i`, `strike`, `color` před `sz`, `u` za ním). Word dnešní pořadí snese, validátor ne.
  Oprava je čisté přeřazení — pro export záznamu, který pomocníka sdílí, chováním neutrální.
