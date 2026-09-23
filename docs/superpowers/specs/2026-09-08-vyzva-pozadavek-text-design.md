# Výzva: jen Word s náhledem a vlastní text požadavku u PNF

**Datum:** 2026-09-08
**Stav:** k odsouhlasení
**Navazuje na:** `docs/superpowers/specs/2026-09-07-vyzvy-dokonceni-design.md` (§9 tisk výzvy)

## 1. Proč

Výzva se do spisové služby zadává jako dokument Wordu. Volba mezi PDF a Wordem tedy
nemá koho obsloužit — pracovník vždycky potřebuje Word. Náhled se přesto hodí: umožní
podívat se na aktuální podobu výzvy, aniž by se musel stahovat soubor.

Druhá a podstatnější změna se týká textu, který ve výzvě popisuje jednotlivé požadavky.
Dnes se bere z popisu tiketu (`HOT_ZAZNAMY.popis`) — tedy z textu, který psal zadavatel
tiketu pro potřeby ServiceDesku, ne pro veřejnou zakázku. Pracovník, který výzvu sestavuje,
ho nemá jak upravit. Nově bude text psát sám, přímo u konkrétní PNF vazby, a bude ho moct
formulovat tak, jak to zakázka potřebuje.

## 2. Cíl

1. Tisk výzvy je jen Word, bez výběru formátu. Vedle něj tlačítko **Náhled**, které otevře
   tiskovou podobu výzvy v nové kartě a nevyvolá dialog tisku.
2. Karta externí vazby typu PNF nese pod stávajícími údaji rich text editor s textem
   požadavku. Tento text se použije do výzvy.

## 3. Rozhodnutí

| # | Rozhodnutí | Proč |
|---|---|---|
| R1 | Nový sloupec `zaznam_externi_odkazy.pozadavek`, `NVARCHAR(MAX) NULL` | Text patří k vazbě, ne k záznamu — jeden záznam může mít víc PNF a každé svůj požadavek |
| R2 | Stávající vazby zůstávají `NULL`, žádná migrace dat | Zadání uživatele: „pro stávající PNF nechci nic dopisovat" |
| R3 | Prázdná hodnota znamená ve výzvě žádný text; **žádný fallback na popis tiketu** | Celý smysl změny je brát text odjinud než z tiketu. Skrytý fallback by do dokumentu vracel text, který pracovník nikdy neviděl |
| R4 | `NULL` a prázdný řetězec se nerozlišují | Bez fallbacku by ten rozdíl nic neřídil. Nestavíme, co nemá spotřebitele |
| R5 | Předvyplnění jen u **nově zadávané** vazby, na klientovi při dohledání tiketu | Pracovník text uvidí a může ho upravit ještě před uložením |
| R6 | Editor i dokument sázejí text v Times New Roman 12 | Co je v editoru, to musí být ve výzvě — jinak pracovníka čeká překvapení ve spisové službě |
| R7 | Převod rich textu do Wordu se vytáhne ze `OpenXmlWordExportService` do sdílené komponenty | Výzva potřebuje totéž co tisk záznamu; druhá kopie by se rozešla |
| R8 | Harvest ze ServiceDesku se pole nikdy nedotkne | Po prvním vyplnění je jediným zdrojem pravdy pracovník, ne tiket |

### Důsledek R2 + R3, který je potřeba vidět

U výzvy sestavené ze **stávajících** PNF vazeb se do dokumentu nevytiskne u požadavků
žádný popisný text, dokud ho pracovník nevyplní. Oproti dnešku je to úbytek obsahu.
Vyplývá to přímo ze zadání a je to vědomá cena za to, že v dokumentu je jen text,
který pracovník schválil. Kdyby to vadilo, alternativou je fallback na popis tiketu
u hodnot `NULL` — pak by ale bylo potřeba rozlišovat `NULL` od prázdna (viz R4).

## 4. Část A — tisk jen do Wordu

### 4.1 Co se mění

Patička panelu výzvy (`PmTracker.Web/Views/Projekty/_VyzvyPane.cshtml`) dnes nese jedno
tlačítko se sdíleným výběrem formátu (`data-print-trigger`). Nahradí ho dvě samostatná,
v pořadí zleva doprava **Náhled**, pak **Tisk výzvy**:

- **Náhled** — hned vlevo vedle tisku. Otevře `GET /Export/Vyzva/{vyzvaId}/Tisk` v nové
  kartě. Endpoint zůstává beze změny: vrací PDF s `Content-Disposition: inline`, takže
  se zobrazí v prohlížeči a dialog tisku se nevyvolá. Při nedostupném Chromiu spadne
  na HTML podobu, jak to dělá dnes.
- **Tisk výzvy** — `GET /Export/Vyzva/{vyzvaId}/Word`, beze změny. Stahuje dokument Wordu.

Obě tlačítka se renderují jen u výzvy (ne na bufferu) a jen s oprávněním
`vyzvy.word.export`, tedy stejně jako dnes.

### 4.2 Co se nemění

Sdílený výběr formátu (`wwwroot/js/modules/ui/print.js`) zůstává beze změny — používá ho
dalších pět míst (tisk projektu, záznamu, úkolu, jednání). Ruší se jen jeho použití
u výzvy.

### 4.3 Riziko k ověření při implementaci

`pm-button` strhává atributy z hostu (viz `feedback_pm_button_strips_host_attributes`).
Je potřeba ověřit, že `href` a `target="_blank"` na `pm-button` fungují; pokud ne,
použije se prostý `<a>` se stylem tlačítka. Rozhodne se podle chování, ne odhadem.

## 5. Část B — text požadavku u PNF vazby

### 5.1 Datový model

```
zaznam_externi_odkazy.pozadavek  NVARCHAR(MAX)  NULL
```

Entita `ZaznamExterniOdkazEntity` (`PmTracker.Web/Models/Entities/`) dostane
`public string? Pozadavek { get; set; }`, mapování v `RecordEntityConfiguration`
řádek k ostatním: `builder.Property(x => x.Pozadavek).HasColumnName("pozadavek");`

Ukládá se sanitizované HTML, stejně jako `popis` u záznamu po přechodu na rich text.

### 5.2 SQL migrace

Nový skript `db_upgrade_1_4_2_externi_odkaz_pozadavek.sql` podle zavedené konvence
(offline nasazení, EF Migrations je záměrně prázdná — viz
`project_offline_deployment_sql_migrations`). Skript je idempotentní:

```sql
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.zaznam_externi_odkazy')
                 AND name = 'pozadavek')
BEGIN
    ALTER TABLE dbo.zaznam_externi_odkazy ADD pozadavek NVARCHAR(MAX) NULL;
END
```

Žádný `UPDATE` — stávající řádky zůstávají `NULL` (R2).

### 5.3 Editor v kartě vazby

Do `PmTracker.Web/Views/Projekty/_EditZaznamExternalPanel.cshtml`, **pod** blok čtyř
read-only datumů, přibude blok s editorem. Renderuje se jen pro vazby typu **PNF**
a jen když je záznam editovatelný.

Editor jede na existující infrastruktuře — `textarea[data-rich-text="true"]`, kterou
`wwwroot/js/modules/recordEditor/richtext.js` povýší na Quill. Tím se přebírá i hotová
lišta (tučné, kurzíva, podtržení, odkaz, seznamy, odsazení) a synchronizace zpět do
textarey. Žádný druhý Quill setup nevzniká.

Písmo Times New Roman 12 se nastaví CSS pravidlem zúženým na tenhle editor
(`.external-row .ql-editor`), ne globálně — ostatní editory v aplikaci si drží
aplikační písmo.

### 5.4 Předvyplnění u nové vazby

`ExterniOdkazSyncResponse` (`PmTracker.Web/Controllers/ExterniOdkazController.cs`) dnes
vrací `Strucne`, ale ne `Popis`. Doplní se o `Popis` jako poziční parametr **na konci**
s výchozí hodnotou — stejný postup, jakým se do `HotZaznamDto` přidávalo `Pid`, aby se
nerozbila stávající volání.

`wwwroot/js/modules/externiOdkaz/sync.js` po dohledání tiketu vloží popis do editoru,
a to **jen když je pole prázdné** a jde o typ PNF. Rozepsaný text tím nikdy nepřepíše.

Stávající vazby se nepředvyplňují (R2) — pracovník je vyplní podle potřeby.

### 5.5 Ukládání

Pole je součástí formuláře editoru záznamu, ukládá se tedy spolu se záznamem přes
`RecordService.SaveRecord`. Platí zavedený UPSERT vzor podle `Id` (viz
`feedback_replace_upsert_for_audit_fk`), takže se nic nemaže a neporušují se FK
z audit tabulek.

Před uložením projde text `IRichTextContentService.NormalizeForStorage`, při renderu
`ToSafeHtml`. O tom, jestli se do dokumentu vůbec něco vypíše, rozhoduje
`HasVisibleText` — prázdný odstavec z Quillu (`<p><br></p>`) se tak nevytiskne.

### 5.6 Harvest

`PerTicketMetadataSyncService` ani re-harvest se pole nedotýkají (R8). Doplní se na to
regresní test, protože právě tudy by se přepis vloudil nejsnáz.

### 5.7 Cesta do výzvy

`VyzvaExportBuilder` (`PmTracker.Web/Services/Vyzvy/VyzvaExportBuilder.cs`) dnes plní
`Popis = h?.Popis` z tiketu. Nově bude číst `pozadavek` z externí vazby. Ve view modelu
se pole přejmenuje z `Popis` na `PozadavekHtml`, aby z názvu bylo poznat, že nese HTML.

**PDF/náhled** — `Views/Export/VyzvaTemplate.cshtml` vypisuje dnes `<p>@p.Popis</p>`,
což text HTML-kóduje. Nově `@Html.Raw(...)` nad hodnotou prohnanou `ToSafeHtml`;
bezpečnost drží sanitizace na serveru, ne kódování při výpisu. Styl `.vyzva-pozadavek`
v `wwwroot/css/pdf-export.css` nastaví Times New Roman 12.

**Word** — `OpenXmlVyzvaExportService` místo `Odstavec(body, p.Popis!)` použije sdílený
zapisovač rich textu (viz §6) s vynuceným písmem Times New Roman a velikostí 12.

## 6. Sdílený převod rich textu do Wordu

Převod HTML → OpenXML dnes žije jako **privátní** metody v
`PmTracker.Web/Services/Export/OpenXmlWordExportService.Records.cs`
(`AppendHtmlParagraphs`, `ParseRichHtml`, `AppendInlineTokens`, `NormalizeHtmlForXml`
a struktury `HtmlInlineToken` / `HtmlParagraphModel` / `HtmlStyleState`).

Vytáhne se do samostatné komponenty `Services/Export/RichTextWordWriter.cs`, kterou
budou používat oba exporty. Komponenta dostane volitelné písmo a velikost, aby si výzva
mohla vynutit Times New Roman 12, zatímco tisk záznamu si nechá dosavadní vzhled.

Jde o refaktor s rizikem regrese v tisku záznamu. Musí být chováním neutrální a jistí
ho existující testy `PmTracker.Tests.Unit/Export/OpenXmlWordExportServiceTests.cs`
a `Architecture/OpenXmlWordExportSplitTests.cs` — ty musí zůstat zelené beze změny.

## 7. Co se nemění

- Endpointy tisku výzvy, jejich oprávnění (`vyzvy.word.export`) ani anti-spoof guard.
- Sdílený výběr formátu pro ostatní tisky.
- Čtyři datumy dodávky na kartě vazby zůstávají read-only z harvestu.
- Kalkulace ve výzvě (tabulky A–D) — beze změny.
- Pole se renderuje jen u PNF; ostatní typy vazeb kartu nemění.

## 8. Testovací strategie

| Vrstva | Co se ověřuje |
|---|---|
| Unit — builder | Do výzvy jde `pozadavek` z vazby, ne popis tiketu; prázdná hodnota nevytiskne nic |
| Unit — Word | Rich text se převede na formátované odstavce; text výzvy má Times New Roman 12 |
| Unit — sanitizace | Skript v textu se do dokumentu ani do náhledu nedostane |
| Unit — harvest | Re-harvest hodnotu `pozadavek` nepřepíše |
| Api — karta vazby | U PNF se editor renderuje, u jiného typu ne; jen pro editovatelný záznam |
| Api — panel výzvy | Patička nese Náhled i Tisk v tomto pořadí a nemá `data-print-trigger` |
| Integration | Uložení záznamu text zachová a nerozbije FK z audit tabulek (UPSERT) |

Assertace na český text v Api testech se kotví na atributy a ASCII — Razor kóduje
diakritiku na číselné entity (viz `feedback_razor_encodes_diacritics_in_html_tests`).

## 9. Rizika a otevřené body

1. **Nevyřešená chyba 500 při tisku výzvy.** Zůstává otevřená z 2026-09-08; příčinu má
   ukázat nově zavedený souborový log. Tahle specifikace se jí nedotýká a implementace
   by neměla začít dřív, než bude příčina známá — mohla by být právě v exportní cestě,
   kterou tu měníme.
2. **Refaktor sdíleného zapisovače Wordu** zasahuje do funkčního tisku záznamu.
3. **Chování `pm-button` s `href`/`target`** je potřeba ověřit, ne předpokládat (§4.3).
4. **Konzistence písma** — Times New Roman 12 v editoru je jen vizuální nápověda; Quill
   ukládá strukturu, ne písmo. Shodu vzhledu drží CSS editoru a nastavení zapisovače
   Wordu, tedy dvě místa, která se mohou rozejít. Hlídá to test na velikost a písmo.
