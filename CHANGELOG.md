# Changelog

Tento soubor je generován skriptem `scripts/generate-changelog.sh` z verzovaných podkladů v `docs/changelog/releases/`.

## 0.10 - 2026-09-17

### Přidáno
- **Záznam se zamyká na dobu úprav.** Když si někdo otevře úpravu záznamu, druhému se editor neotevře a uvidí, kdo záznam právě upravuje a odkdy. Zámek se uvolní uložením nebo odchodem z editoru; když prohlížeč spadne, vyprší sám po 15 minutách nečinnosti.
- **Uložení přes cizí verzi se odmítne se jménem.** Pokud záznam mezitím uložil někdo jiný, hláška ho pojmenuje, uvede čas a nabídne tlačítko Obnovit stránku. Dřív se cizí změna tiše přepsala.

### Opraveno
- **Uložení už neblokuje automatika.** Pracovníci v pilotu naráželi na hlášku „Harmonogram byl mezitím upraven jiným uživatelem" i když harmonogram vůbec neměnili — stačilo, že mezitím doběhla synchronizace se ServiceDeskem. Kontrola, která to způsobovala, byla zrušena; souběh dvou lidí místo ní řeší zámek záznamu.
- **Ruční skutečnost u kroků 1, 3, 4, 6, 7 a 10 se konečně uloží.** V ručním režimu šlo datum vyplnit, ale server ho při uložení zahodil. Nově se uloží a krok zůstane ruční, dokud se přepínač nevrátí zpět na automatiku.

### Poznámka k nasazení
- Před nasazením je potřeba spustit `db_upgrade_1_4_5_record_edit_lock.sql` (zakládá tabulku zámku). **Bez něj aplikace nenastartuje** — kontrola při startu selže a v logu uvede název skriptu. Stav skriptů na dané databázi ukáže `db_check_applied_upgrades.sql`.


## 0.9 - 2026-09-08

### Přidáno
- Nová projektová záložka **Výzvy**: buffer nezařazených PNF, ruční číslování výzev domluvené se SVA, přesun PNF mezi výzvami tažením i z kontextového menu a uzavření výzvy proti dalším změnám.
- Tisk výzvy do Wordu a vedle něj **Náhled**, který tiskovou podobu jen otevře v nové kartě, včetně tabulek kalkulací (analýza, programové úpravy, testování, implementace) a rekapitulace ceny.
- **Text požadavku u PNF vazby**: pracovník si ho píše v editoru formátovaného textu přímo na kartě vazby (Times New Roman 12) a u nově zadaného tiketu se předvyplní jeho popisem ze ServiceDesku. Do výzvy jde právě tento text — popis tiketu se do dokumentu už nepřenáší. U vazeb založených dřív zůstává pole prázdné, dokud si ho pracovník nevyplní.
- Chyba se nově ukazuje s detaily a tlačítky Kopírovat a Uložit i mimo modaly — dosud obyčejná stránka, která spadla, nabídla jen Request ID.
- Souborový log chyb v adresáři `logs`: zapisují se jen chyby a soubory starší týdne se samy mažou.
- **Harmonogram** přešel na datumový model s deseti pevnými kroky — plán proti skutečnosti, zvýrazněný aktuální krok, projekce a znaménkové překročení termínu.
- Skutečnost kroků se plní automaticky z tiketů ServiceDesku; u vybraných kroků lze přepnout na ruční zadání, a to i hromadně přepínačem v liště záložky.
- Pravidelná i reaktivní synchronizace se ServiceDeskem, vytěžování datumů z textu vyjádření a diagnostická stránka `/SDConnector` pro kontrolu napojení.
- Panely informačních systémů: prodlené tikety a čerpání rozpočtu; panel NES na přehledu projektu s exportem do Excelu.
- Globální vyhledávání — index se zakládá databázovým skriptem při nasazení a jde ho ručně přegenerovat v Nastavení.
- Sada komponent `pm-*` nad gov design systémem (tlačítka, pole, přepínače, karty, záložky, dialogy, tooltipy, stránkování) a sjednocený datumový vstup `pm-date-field`.
- Krokový ukazatel v modalu vyjádření s přiřazením vyjádření ke kroku harmonogramu tažením.
- Návrhy změn nově přenášejí i harmonogram — vazby kroků a ručně zadané skutečnosti.
- Skutečná cena PNF z akceptované kalkulace: chip externí vazby, karta PNF i součet výzvy na záložce Výzvy ukazují cenu z kalkulace. Předpokládaná cena zůstává a do doby, než je kalkulace známá, se zobrazí s označením „(předp.)“.

### Změněno
- **Oprávnění** jsou přepsaná na klíče podle jednotlivých akcí: každá mutující akce má vlastní klíč a kontrolu provádí jednotná politika nad celou aplikací. Role mají rozsah globální, projektový nebo subsystémový a vyhodnocují se z databáze.
- Subsystémové role mají nově právo na export a tisk — kdo smí číst, smí i tisknout.
- Široké stránky (Projekty, Přehled, Jednání, Nastavení, Číselníky, Vyhledávání, Profil) využijí celou šířku obrazovky.
- Karty záznamů mají barevnou čáru podle kategorie: Úkol, Informace a Rozhodnutí; úkol po termínu je červený, hotový zelený.
- Z výpisů zmizely generické nadpisy stránek, které jen opakovaly název z navigace.
- Externí vazby: číslo tiketu se ověřuje na šest číslic, pole jsou gov komponenty a čtyři datumy dodávky se plní z vytěžování, takže se ručně nepřepisují.
- Z harmonogramu byl odstraněn krok fakturace; NES je od harmonogramu odpojený a plní jen čtyři datumy na kartě externí vazby.
- Tmavý režim jede na návrhových tokenech — opravena čitelnost přehledu, karet vyjádření a panelů.
- Tisk výzvy podle finálního vzoru: stručné popisy požadavků v první sekci, římské číslování, číslo úkolu s prefixem RU, tabulky podle obsahu kalkulace včetně licenčního rozšíření z rozpisu kalkulace, ve Wordu záhlaví s přílohou, číslo stránky a písmo Times New Roman 12.
- Export záznamů do PDF a Wordu uvádí u externí vazby jen skutečnou cenu z kalkulace, předpokládanou už ne.

### Opraveno
- Vyhledávání nefungovalo vůbec: aplikace si tabulku zakládala sama za běhu, jenže účet, pod kterým běží, na to nemá v databázi právo. Založení se přesunulo do instalačního skriptu `db_upgrade_1_4_4_search_index.sql`, který spouští správce databáze. Hláška v logu nově píše konkrétní důvod a příkaz, který jde rovnou zkopírovat a spustit — dosud nabízela šablonu k doplnění, na které spuštění skončilo chybou.
- Hledání nezáleží na diakritice ani velikosti písmen: „zalohovani“ najde „Zálohování“. Víceslovný dotaz vyžaduje všechna slova a shoda v názvu se řadí výš než výskyt v textu. Znaky `%` a `_` se berou doslova, ne jako zástupné.
- Tisk výzvy končil chybou 500: tři sloupce kalkulací ze ServiceDesku byly v aplikaci vedené jako čísla nebo datum, ačkoli v databázi nesou text. Nepoužívané sloupce se přestaly načítat a při více akceptovaných kalkulacích na jedno PNF se bere ta s vyšším id.
- Ceny ve výzvě mohly tiše chybět: kalkulace se považovala za akceptovanou jen se stavem „Akceptováno“, ačkoli ServiceDesk používá i další stavy (například „Fakturovat“ u kalkulace potvrzené projektovým manažerem). Nově se vylučují jen rozpracované a odmítnuté stavy — „Návrh“, „Neakceptováno“ a „Akceptovat ?“ — a na zápisu s mezerou či velikostí písmen nezáleží.
- Bezpečnost: XSS a únik informací v modalu vyjádření a v SD konektoru, neoprávněný přístup k cizím záznamům, kontrola vlastnictví před vytěžováním, ochrana tiskových odkazů proti podvržení a ochrana exportu PDF proti odeslání z cizí stránky.
- Autorizace: role pro čtení všech projektů skutečně zpřístupní všechny projekty a hledání kandidátů do týmu funguje i držitelům projektových rolí.
- Ručně zadané skutečnosti kroků se skutečně ukládají; přepnutí kroku na ruční režim funguje i tam, kde pro něj ještě neexistoval záznam.
- Mazání záznamu ani komentáře už neselže kvůli osiřelým vazbám na historii.
- Vytěžování: správné určení data plánovaného dodání, rozlišení kroků podle typu tiketu a odolnost proti soubežnému zpracování téhož tiketu.
- Modal vyjádření drží pozici odscrollování po přiřazení kroku a otevírá se v šířce, kde je na obsah vidět.
- Uložení záznamu vracelo PNF z výzvy do bufferu — a to i z odeslané, uzamčené výzvy.
- Nově přidanou PNF šlo zařadit do bufferu až po uložení a novém otevření záznamu.
- Schválení návrhu založení záznamu ztrácelo u PNF text požadavku.


## 0.8 - 2026-03-10

### Přidáno
- Pole `Popis` a `Vyjádření` používají rich text editor s omezeným formátováním (`bold`, `italic`, `underline`, `odkaz`, `odsazení`) a auto-grow chováním.
- Serverová vrstva pro rich text zpracování (`normalizace`, `sanitizace`, `legacy převod plain text -> safe HTML`) je sjednocená do samostatné služby.
- PDF a WORD exporty zachovávají formátování vyjádření včetně klikatelných odkazů.

### Změněno
- Render `Popis` a `Vyjádření` v UI je převeden na bezpečný HTML výstup po sanitizaci.
- Výpočet délky komentářů pro export používá čistou textovou délku bez HTML tagů.
- Uživatel bez oprávnění přidávat vyjádření nově v kartě záznamu nevidí formulář pro zadání vyjádření ani výběr jednání.

### Opraveno
- Opraveno znovu-inicializování rich text editoru po AJAX refreshi panelů detailu projektu.
- Opraveno CSS Quill kontejneru, aby neblokoval kliknutí na tlačítko `Uložit`.


## 0.7 - 2026-03-05

### Přidáno
- Projektové záznamy mají nové pole `Cíl` (`dbo.projektove_zaznamy.cil`, max 85 znaků) napojené end-to-end přes editor, backend, PDF a WORD export.
- Startup SQL validace kontroluje existenci sloupce `dbo.projektove_zaznamy.cil` při startu aplikace.

### Změněno
- V záložce `Záznamy` se na kartě záznamu v kompaktním (sbaleném) pohledu zobrazuje místo popisu hodnota `Cíl`.
- `Popis` se na kartě záznamu přesunul do rozbalené části dlaždice.
- V editoru záznamu (modal/stránka) je nové pole `Cíl` mezi `Název` a `Popis`.
- `Popis` v editoru je vizuálně zmenšen o jeden řádek, aby se snížila potřeba scrollování v modalu.
- V PDF i WORD exportu je `Cíl` doplněn mezi hlavičku záznamu a `Popis`, formátovaný kurzívou ve stejné velikosti písma jako popis.

### Opraveno
- Ukládání záznamu ořezává (`Trim`) i pole `Cíl`, stejně jako ostatní textová pole, a bezpečně pracuje s prázdnou hodnotou.


## 0.6 - 2026-03-04

### Přidáno
- Profil obsahuje novou sekci `Odvozená práva z projektu a subsystému` s přehledem implicitních grantů a jejich zdroje.
- V `Nastavení -> Efektivní práva` je nově sloupec `Zdroj`, který ukazuje původ oprávnění.

### Změněno
- Pole `Vlastník` i `Spolupráce` v editoru záznamu používají stejný kandidátní seznam osob: pouze aktivní členové projektu (projektové nebo subsystémové role) s fallbackem pro legacy vybraného vlastníka.
- Hard delete záznamu nevyžaduje potvrzovací checkbox; potvrzení probíhá přímo tlačítkem v modalu.
- Harmonogram v detailu projektu už nezobrazuje horní globální legendu a kompaktní řádek `Skutečnost` používá stejné barvy kroků jako `Plán`.
- Rozbalený harmonogram zobrazuje pouze marker `Dnes` (marker `Termín` byl odebrán) a osa je zarovnaná na stejnou šířku jako kreslicí tracky.
- Pravý sloupec `Plán / Skutečnost` v editoru harmonogramu má znovu dvě samostatné lišty na řádek se stejnou barvou kroku.
- Přepínač `Používat identifikátor záznamů podle jednání` v modalu editace projektu je sjednocen na `gov-switch`.

### Opraveno
- Datepicker v modalu editoru záznamu nezasahuje do sticky action baru a lépe se umisťuje při scrollu.
- Štítky časové osy jsou clampované do šířky panelu a při kolizi se přebytečné štítky skryjí, takže nepřetékají mimo panel.
- Krok s trváním `0` v rozbaleném harmonogramu nevykresluje segmenty ani markery.
- Datepicker hlavička (měsíc/rok) má vertikálně centrované caret šipky.
- Tlačítko `Přidat externí vazbu` má přidaný vertikální odstup od horních polí.


## 0.5 - 2026-03-04

### Přidáno
- Přehled projektů obsahuje malý filtr `Skrýt hotové` a `Skrýt smazané` s uložením volby do tohoto prohlížeče.

### Změněno
- Filtry v sekci `Záznamy` používají pro `Mé záznamy` a `Aktivní úkoly` sjednocený GOV switch styl.
- Sekce `Účast` v detailu jednání je výchozí sbalená do kompaktního souhrnu a lze ji rozbalit jen při potřebě úprav.
- Ovládací prvky `Uložit stav` a `Uzavřít jednání` jsou v detailu jednání srovnané do jednoho akčního bloku.

### Opraveno
- Uložení nového záznamu v modalu i na samostatné stránce už nepadá na `400 Bad Request`, pokud v databázi chyběla aktivní harmonogramová šablona.
- První uložení záznamu na čerstvé databázi automaticky vytvoří chybějící aktivní harmonogramovou šablonu a její výchozí kroky.


## 0.4 - 2026-03-03

### Přidáno
- Profil obsahuje samostatnou volbu pro zrušení uloženého výchozího otevření editoru záznamu.
- Harmonogram zobrazuje rozpad kroků přímo v projektovém detailu bez nutnosti přechodu do samostatné GANTT záložky.

### Změněno
- Otevření editoru záznamu používá jednu výchozí volbu uloženou v tomto prohlížeči; chooser se ukáže jen pokud volba ještě není nastavená.
- Dialog volby otevření editoru používá negativní checkbox `Neukládat pro tentokrát jako výchozí volbu`.
- Samostatná záložka `GANTT` byla sloučena do záložky `Harmonogram`.
- Harmonogram byl zjednodušen na filtrování podle subsystému a zvýrazňuje plán jako základní vrstvu a skutečnost jako překryv.
- Indikátor aktuálního subsystému v přehledu záznamů je nově boční rail s pohyblivou bublinou místo plovoucího boxu.

### Opraveno
- Tlačítka pro otevření editoru záznamu už nepoužívají split-button se šipkou.
- Uložené výchozí otevření editoru záznamu lze vyčistit z profilu bez zásahu do ostatních preferencí.


## 0.3 - 2026-03-02

### Přidáno
- Aplikace zobrazuje svou verzi ve footeru.
- Detail projektu má nové klientské filtry `Jen mé záznamy` a `Jen mé úkoly` pro panely Záznamy, Harmonogram a GANTT.
- Aktivní projektové filtry se zobrazují jako odebíratelné čipy.
- Uživatel si může uložit výchozí projektové filtry do local storage a smazat je v profilu.

### Změněno
- Přepínač `Seskupit dle subsystému` byl přesunut z hlavičky panelu do sekce filtrů a sjednocen do gov switch stylu.
- Viditelnost hlavních navigačních záložek `Osoby`, `Číselníky` a `Nastavení` se nově řídí prefixy oprávnění `people.*`, `ciselniky.*` a `settings.*`.


