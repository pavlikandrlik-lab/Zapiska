# To-Do (PM Tracker)

## Pravidla práce
- Problémy budeme přidávat postupně po jednom.
- U každého problému se nejdřív dohodneme na cílovém výstupu.
- Po potvrzení se doplní závěr a konkrétní bod do tohoto seznamu.
- Implementace začne až po uzavření celého seznamu.

## Otevřené body
- [ ] 1) Harmonogram rozpad: nulové trvání nesmí kreslit nic v řádku
  - Kontext: v rozpadu harmonogramu se u kroků s trváním `0` stále vykreslují začátky/osy/markery.
  - Požadovaný výstup: pokud má krok trvání `0`, v jeho řádku se nevykreslí žádný segment, marker ani bod.
  - Navržený závěr (k potvrzení): pro `trvani = 0` bude vizuální vrstva breakdown řádku prázdná; osa a ostatní kroky zůstanou beze změny.
- [ ] 2) Harmonogram osa: datumy nesmí přetékat mimo pravý okraj panelu
  - Kontext: štítky dat na časové ose se u pravého kraje překrývají a text leze mimo okno/panel.
  - Požadovaný výstup: každý štítek data musí zůstat uvnitř šířky osy a nesmí se překrývat s dalším štítkem.
  - Potvrzený scénář z praxe: pravý štítek (např. `31.03.`) přetéká mimo panel v detailu harmonogramu.
  - Navržený závěr (k potvrzení): pozice štítků se bude clampovat do hranic osy; pokud by poslední štítky kolidovaly, pozdější se skryjí (nebo zřídí), aby nic nevylézalo ven.
- [ ] 3) Revize pravého sloupce v editoru harmonogramu: vrátit 2 lišty (Plán / Skutečnost)
  - Kontext: současný způsob zobrazení vpravo u dílčích činností je nepřehledný.
  - Požadovaný výstup:
    - každý řádek bude mít 2 samostatné lišty pod sebou: horní `Plán`, spodní `Skutečnost`,
    - obě lišty budou používat stejnou barvu kroku z existujícího nastavení/číselníku,
    - délka lišt bude přímo odpovídat hodnotě plánu a skutečnosti (na stejné škále),
    - bez speciálního plus/minus barevného režimu a bez dodatečných šrafovaných překryvů.
  - Navržený závěr (k potvrzení): cílem je vizuálně jednoduché porovnání dvou délek (plán vs. skutečnost) v každém řádku.

- [ ] 4) Horní harmonogram: zarovnání časové osy a rozdělení na 2 lišty
  - Kontext: horní osa datumů nezačíná tam, kde začíná první segment lišty, a působí rozhozeně.
  - Požadovaný výstup:
    - časová osa začne přesně v levém bodě prvního segmentu a skončí v pravém bodě posledního segmentu,
    - horní grafika bude ve 2 řádcích: horní `Plán`, spodní `Skutečnost`,
    - oba řádky budou mít stejnou logiku barev jako dosud (jen oddělené do dvou lišt).
  - Potvrzený scénář z praxe: v rozbaleném harmonogramu neodpovídá pozice datumů (`04.03.` / `11.03.`) reálnému začátku lišt a kroků.
  - Navržený závěr (k potvrzení): osa a lišty budou mít stejný vizuální rámec/šířku, aby datumy i segmenty byly 1:1 zarovnané.

- [ ] 5) Editace záznamu: pole `Vlastník` musí být vyhledávací picker z osob projektu
  - Kontext: u pole `Vlastník` nejde prakticky vyhledat osobu stejně jako u `Spolupráce`, takže nelze pohodlně vybrat jiného vlastníka než default.
  - Požadovaný výstup:
    - `Vlastník` bude mít stejný UX pattern jako person picker ve `Spolupráce` (psaní + filtrování + výběr),
    - kandidáti budou pouze osoby, které mají v daném projektu roli (projektovou nebo subsystémovou),
    - do kandidátů se nemají přidávat osoby jen na základě globálních rolí (`SUPERADMIN`, `APP_ADMIN`) bez projektové/subsystémové role.
  - Ověření ve stejném bodě:
    - `Spolupráce` musí používat stejnou množinu kandidátů (jen osoby s projektovou/subsystémovou rolí v daném projektu).
  - Navržený závěr (k potvrzení): `Vlastník` i `Spolupráce` budou mít jednotný zdroj kandidátů “osoby s aktivní projektovou/subsystémovou rolí pro tento projekt”, a jednotné vyhledávání.

- [ ] 6) Harmonogram v projektu (bez rozpadu): `Skutečnost` má být barevně podle číselníku, ne červeně šrafovaná
  - Kontext: na hlavní kartě projektu v sekci `Harmonogram` má řádek `Skutečnost` teď červené šrafování, což zhoršuje rychlé porovnání s řádkem `Plán`.
  - Požadovaný výstup:
    - v základním (nerozbaleném) zobrazení budou řádky `Plán` i `Skutečnost` používat stejný barevný jazyk podle číselníku kroků,
    - `Skutečnost` nebude v tomto zobrazení červeně šrafovaná.
  - Důležitá hranice:
    - šrafování a červené zvýraznění zůstane zachované v rozpadu/detailu, kde je žádoucí zvýraznit rozdíly.
  - Navržený závěr (k potvrzení): sjednotit vizualizaci `Plán`/`Skutečnost` na hlavní kartě pro rychlé porovnání, ale detailní rozpad ponechat s kontrastním (šrafovaným) stylem.

- [ ] 7) Rozbalený harmonogram: zjednodušit legendu a markery na časové ose
  - Kontext: v legendě je nyní `Plán`, `Skutečnost`, `Dnes`, `Termín`; marker `Termín` je pro uživatele nejasný a nesedí vizuálně s grafem.
  - Požadovaný výstup:
    - na časové ose zůstane jasně viditelný marker `Dnes`,
    - marker `Termín` se z grafické osy odstraní (nebo se nebude vykreslovat jako samostatná svislá čára),
    - legenda bude odpovídat tomu, co je skutečně vykreslené.
  - Poznámka: textová informace `Termín úkolu` v metadatech karty může zůstat, jde o odstranění/omezení grafického markeru.
  - Navržený závěr (k potvrzení): v rozbaleném harmonogramu ponechat orientační vertikálu `Dnes`, odstranit vertikálu `Termín` a vyčistit legendu, aby nepůsobila matoucím dojmem.

- [ ] 8) Detail projektu / Harmonogram: odstranit horní globální legendu kroků
  - Kontext: horní legenda nad filtry (`Legenda: Plán / Skutečnost / ... + Krok 1..11`) je zbytečně hlučná a zabírá místo.
  - Požadovaný výstup:
    - globální legenda nad filtrem v záložce `Harmonogram` se nebude zobrazovat,
    - orientace v barvách zůstane přes rozbalený harmonogram (lokální legenda/detail v kartě).
  - Navržený závěr (k potvrzení): odstranit pouze horní globální legendu na úrovni panelu, nikoli povinné lokální informace uvnitř rozbalené karty.

- [ ] 9) Editace projektu: checkbox nahradit toggle přepínačem (sjednocení s filtry)
  - Kontext: pole `Používat identifikátor záznamů podle jednání` je v editaci projektu jako klasický checkbox, zatímco jinde v aplikaci se používá toggle/switch.
  - Požadovaný výstup:
    - vizuálně i interakčně použít stejný switch/toggle pattern jako ve filtrech (`gov-switch`),
    - zachovat stejnou datovou logiku i význam pole (jen UI změna),
    - zachovat přístupnost: klik labelu, ovládání klávesnicí, focus ring.
  - Navržený závěr (k potvrzení): checkbox v editaci projektu bude nahrazen jednotným toggle komponentem; nevidím validní argument, proč ho v tomto konkrétním případě nechat odlišný.

- [ ] 10) Přehled práv: zobrazovat i oprávnění odvozená z projektových a subsystémových rolí
  - Kontext: v profilu `Moje role a práva` jsou teď vidět hlavně aplikační role; chybí odvozená práva vzniklá z rolí v projektech/subsystémech.
  - Požadovaný výstup:
    - v profilu uživatele zobrazit kromě aplikačních rolí i odvozená práva z projektových/subsystémových rolí,
    - ve `Nastavení > Efektivní práva` totéž: jasně ukázat i odvozené granty z projektových/subsystémových rolí,
    - výpis má být srozumitelný i pro více projektů (uživatel nemusí ručně proklikávat projekty).
  - Požadovaný rozsah detailu:
    - u odvozeného práva uvést zdroj (projekt, typ role, případně subsystém),
    - odlišit aplikační grant vs. odvozený grant.
  - Navržený závěr (k potvrzení): práva se budou dopočítávat při čtení (bez duplicitního ukládání navíc), a UI je zobrazí jako sjednocený přehled se zdrojem oprávnění.

- [ ] 11) Modal Editace záznamu: datepicker u externích vazeb se nesmí překrývat se sticky akcemi
  - Kontext: kalendář (`Datum objednání`, `Plán dodání`, případně další date pole) se v modalu překrývá s prvky layoutu a se spodním action barem (`Smazat natrvalo / Zrušit / Uložit`).
  - Požadovaný výstup:
    - otevřený datepicker se vykreslí čitelně bez kolizí s okolními prvky,
    - datepicker nebude zasahovat přes sticky footer akce ani nebude odříznutý scrollem kontejneru,
    - chování bude stabilní i při scrollu modalu.
  - Navržený závěr (k potvrzení): upravit layering/positioning datepickeru v modalu (z-index + anchor + overflow pravidla), aby picker vždy seděl nad obsahem formuláře, ale nezasahoval do akčního footeru.

- [ ] 12) Datepicker UI: vertikálně vycentrovat šipky/caret v hlavičce měsíce a roku
  - Kontext: v datepickeru jsou názvy měsíce/roku vizuálně vycentrované, ale caret šipky u selectů sedí nízko u spodního okraje.
  - Požadovaný výstup:
    - caret/šipka u výběru měsíce i roku bude vertikálně uprostřed políčka,
    - stejná vizuální úprava bude platit konzistentně ve všech datepickerech aplikace.
  - Navržený závěr (k potvrzení): jde o čistě kosmetickou CSS úpravu datepicker headru (bez změny logiky komponenty).

- [ ] 13) Externí vazby v editoru: přidat vertikální mezeru nad tlačítko `Přidat externí vazbu`
  - Kontext: tlačítko `Přidat externí vazbu` je vizuálně nalepené na horní vstupní pole bez potřebného odsazení.
  - Požadovaný výstup:
    - mezi horním řádkem polí a tlačítkem bude malý, konzistentní vertikální odstup.
  - Navržený závěr (k potvrzení): čistě kosmetická úprava spacingu (margin/padding) v sekci externích vazeb, bez změny funkce tlačítka.

- [ ] 14) Hard delete záznamu: odstranit checkbox „Opravdu chci záznam smazat natrvalo“
  - Kontext: současný flow má zbytečný krok navíc (checkbox), uživatel potvrzuje mazání víckrát, než je potřeba.
  - Požadovaný výstup:
    - zachovat vstup přes `Upravit` -> `Smazat natrvalo` -> potvrzovací modal,
    - v potvrzovacím modalu ponechat varování a souhrn dopadů (počty návazností),
    - odstranit checkbox potvrzení,
    - `Potvrdit trvalé smazání` bude dostupné rovnou bez checkboxu.
  - Navržený závěr (k potvrzení): zkrátit flow hard delete na 3 kroky bez checkbox gate, ale zachovat samostatný potvrzovací modal s jasným textem o nevratnosti.

## Uzavřené body
- _Zatím bez položek._

## Implementační plán (návrh)

### Release cíl této iterace
- Cílová verze: **`0.6`**
- Podmínka release: změny implementované + kompletní test gate zelená + manuální smoke bez blockeru.
- `to-do.md` se smaže až jako **poslední krok** po potvrzení, že release je hotový.

### Fáze 0: Stabilizační baseline před změnami
- Cíl: mít jistotu, že se nic nerozbije mimo scope.
- Kroky:
  - spustit baseline sadu testů (`build`, `unit`, `api`, `integration`, `e2e`),
  - uložit baseline výsledky (které testy jsou dnes green/red),
  - teprve potom začít s úpravami.

### Fáze 1: Editace záznamu – výběr osob a hard delete UX
- Pokrývá body: **5**, **14**
- Změny:
  - `Vlastník` přepnout na stejný person-picker pattern jako `Spolupráce`,
  - sjednotit datový zdroj kandidátů pro `Vlastník` i `Spolupráce`: pouze osoby s aktivní projektovou/subsystémovou rolí v aktuálním projektu,
  - v hard-delete modalu odstranit checkbox potvrzení a zkrátit flow na přímé potvrzení tlačítkem.
- Pravděpodobně dotčené soubory:
  - `PmTracker.Web/Services/Data/SqlServerDataStore.cs`
  - `PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml`
  - `PmTracker.Web/wwwroot/js/site.js`
  - `PmTracker.Web/Views/Projekty/DeleteRecordModal.cshtml`
  - `PmTracker.Web/Models/ViewModels/CommandViewModels.cs` (pokud bude potřeba uvolnit validaci checkboxu)
  - `PmTracker.Web/Controllers/ZaznamyController.cs` (pokud je checkbox kontrolován server-side)
- Testy:
  - unit/integration na kandidátní osoby pro owner/collaboration,
  - API test na hard delete bez checkboxu,
  - E2E smoke: vyhledání ownera, změna ownera, hard delete modal flow.

### Fáze 2: Datepicker + externí vazby (modal rendering a spacing)
- Pokrývá body: **11**, **12**, **13**
- Změny:
  - opravit layering/positioning datepickeru v modalu (kolize se sticky footer akcemi),
  - vertikálně vycentrovat caret šipky v month/year headeru datepickeru,
  - doplnit vertikální spacing nad tlačítko `Přidat externí vazbu`.
- Pravděpodobně dotčené soubory:
  - `PmTracker.Web/wwwroot/css/site.css`
  - `PmTracker.Web/wwwroot/js/site.js`
  - `PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml`
- Testy:
  - E2E: otevření datepickeru v externích vazbách bez překryvu tlačítek,
  - E2E: klikatelnost a správné zavření/scroll chování v modalu,
  - vizuální smoke na různé výšce modalu.

### Fáze 3: Harmonogram – osa, legenda, plan/skutečnost vizualizace
- Pokrývá body: **1**, **2**, **3**, **4**, **6**, **7**, **8**
- Změny (po částech, ale jako jeden harmonogram balík):
  - odstranit horní globální legendu v panelu harmonogramu,
  - v rozbaleném harmonogramu zjednodušit legendu na to, co je reálně vykreslené (ponechat marker `Dnes`, odstranit marker `Termín`),
  - zarovnat časovou osu 1:1 na šířku kreslicí oblasti (bez přetékání pravých štítků),
  - přidat clamp + anti-overlap logiku štítků dat,
  - u kroků s `trvání = 0` nevykreslovat segment/marker v řádku rozpadu,
  - v hlavní (nerozbalené) kartě sjednotit barvení `Plán` a `Skutečnost` podle číselníku kroků (bez červeného šrafování),
  - v editoru harmonogramu vpravo vrátit 2 samostatné lišty (`Plán` nahoře, `Skutečnost` dole) se stejnou barevnou logikou.
- Pravděpodobně dotčené soubory:
  - `PmTracker.Web/Views/Projekty/Detail.cshtml`
  - `PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml`
  - `PmTracker.Web/wwwroot/js/site.js`
  - `PmTracker.Web/wwwroot/css/site.css`
  - případně `PmTracker.Web/Models/ViewModels/ProjektyViewModels.cs` (pokud bude potřeba upřesnit data pro renderer)
- Testy:
  - E2E harmonogram scénáře (axis alignment, label overflow, expand/collapse),
  - API test renderu detailu harmonogramu (legenda/markery),
  - případně unit test pomocných výpočtů pozice štítků (pokud bude vytaženo do helperu).

### Fáze 4: Přehled práv (profil + efektivní práva)
- Pokrývá bod: **10**
- Změny:
  - rozšířit výpočet práv v profilu a v `Nastavení > Efektivní práva` o odvozené granty z projektových/subsystémových rolí,
  - u každého práva zobrazit zdroj (aplikační role vs. projekt/subsystém role + projekt),
  - nic neukládat navíc, dopočítávat při čtení.
- Pravděpodobně dotčené soubory:
  - `PmTracker.Web/Services/Data/SqlServerDataStore.cs`
  - `PmTracker.Web/Models/ViewModels/ProfilViewModels.cs`
  - `PmTracker.Web/Models/ViewModels/NastaveniViewModels.cs`
  - `PmTracker.Web/Views/Profil/Index.cshtml`
  - `PmTracker.Web/Views/Nastaveni/_DetailPanel.cshtml`
- Testy:
  - API testy profilu a efektivních práv s uživatelem, který má kombinaci aplikační + projektové + subsystémové role,
  - integration test na správný výpočet zdrojů grantů.

### Fáze 5: Editace projektu – toggle sjednocení
- Pokrývá bod: **9**
- Změny:
  - checkbox `Používat identifikátor záznamů podle jednání` převést na `gov-switch` toggle,
  - beze změny business logiky a datového ukládání.
- Pravděpodobně dotčené soubory:
  - `PmTracker.Web/Views/Projekty/EditProjectModal.cshtml` (nebo odpovídající view)
  - `PmTracker.Web/wwwroot/css/site.css` (pokud bude třeba jen drobný spacing)
- Testy:
  - E2E smoke: otevřít editaci projektu, přepnout toggle, uložit, znovu otevřít a ověřit perzistenci stavu.

### Fáze 6: Finální ověření a uzavření
- Spustit kompletní sadu:
  - `dotnet build`
  - `dotnet test PmTracker.Tests.Unit`
  - `dotnet test PmTracker.Tests.Api`
  - `dotnet test PmTracker.Tests.Integration`
  - `./scripts/test-e2e.sh`
- Provést ruční smoke pro vizuální body (harmonogram/datepickery/modaly) podle screenshot scénářů.
- Po schválení přesunout body z `Otevřené` do `Uzavřené`.

### Fáze 7: Release 0.6 + changelog + úklid
- Sjednotit verzi napříč řešením na `0.6`:
  - `Directory.Build.props`: `Version=0.6`, `AssemblyVersion=0.6.0.0`, `FileVersion=0.6.0.0`.
- Dopsat release sekci do `CHANGELOG.md`:
  - `## 0.6 - YYYY-MM-DD`
  - `Přidáno`, `Změněno`, `Opraveno`.
- Spustit ještě jednou finální regression gate (build + testy + e2e) nad release kandidátem.
- Teprve po úspěšném průchodu a potvrzení:
  - odstranit soubor `to-do.md`.
