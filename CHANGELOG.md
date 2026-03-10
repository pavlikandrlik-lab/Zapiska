# Changelog

Tento soubor je generován skriptem `scripts/generate-changelog.sh` z verzovaných podkladů v `docs/changelog/releases/`.

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


