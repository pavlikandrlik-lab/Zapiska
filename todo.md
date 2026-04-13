# Požadované změny v aplikaci

Seznam je seskládaný od jednodušších úprav po složitější zásahy. Pořadí je orientační implementační backlog, ne finální release plán.

## 1. Drobné vizuální a UX opravy

H - Upravit významové barvy textu pro `přípravu` a `nové informace`; modrá a červená mají být světlejší a na první pohled lépe rozeznatelné od běžného černého textu, bez změny jejich významu.
H - Opravit vykreslení štítků s datumy na ose času v harmonogramu; levý krajní štítek je v pořádku, pravý se ořezává a chybí na něm přibližně jedna a půl číslice.
H - Opravit nefunkční tlačítko `Rozpad` v harmonogramu tak, aby znovu správně rozbalovalo a sbalovalo detailní rozpad harmonogramu pro daný záznam.
H - Ve stručném zobrazení harmonogramu `Plán / Skutečnost` neopisovat ani nevykreslovat kroky s nulovým `TrvaniDni` jako běžné indikátory; nesmí posouvat další skutečně vyplněné kroky ani vytvářet vizuální artefakty.
H - V exportech `PDF` i `Word` změnit zvýraznění pozastaveného úkolu tak, aby byl světle hnědě podbarvený celý blok záznamu, nikoli jen hlavička.

## 2. Filtry, řazení a ovládání přehledů

H - Filtr `Pouze aktivní úkoly` má být ve výchozím stavu aktivní.
H - Do projektové záložky `Osoby (tým)` doplnit základní filtrování tabulky rolí a přístupů; pokud nebude vhodný plný filtr po sloupcích, musí být minimálně možné tabulku základně řadit podle sloupců.
H - Do globální záložky `Osoby` doplnit obdobné základní filtrování, aby bylo možné dohledávat konkrétní osoby i při větším počtu záznamů.
H - Při spuštění `Tisk projektu` se má aplikace pokaždé zeptat, zda použít aktuálně aktivní projektové filtry, nebo tisknout celý projekt; volba se nesmí ukládat a pokud není aktivní žádný relevantní filtr, dotaz se nemá zobrazovat.

### Řazení subsystémů v projektu

H - Doplnit do tabulky `projekt_subsystemy` explicitní sloupec pro pořadí subsystému v projektu; pro tuto změnu bude potřeba připravit samostatný DB patch.
H - V projektové záložce `Osoby (tým)` v tabulce `Subsystémy projektu` zobrazovat a spravovat pořadí subsystémů.
H - Uživatel s projektovým oprávněním `team.manage` má mít u každého subsystému vedle akce `Deaktivovat` i akce `nahoru` a `dolů`.
H - Kliknutí na `nahoru` nebo `dolů` má vždy prohodit pořadí dvou sousedních řádků, tedy dvou subsystémů v rámci projektu.
H - Změna pořadí se má ukládat okamžitě pouhým prohozením pořadí v databázi; bez samostatného tlačítka `Uložit`.
H - Pořadí subsystémů nastavené na projektu má ovlivnit řazení subsystémů v projektových záložkách a také generování zápisu jednání.
H - Do panelu filtrů v příslušných záložkách doplnit volbu `Řadit podle` s možnostmi:
H - `abecedně vzestupně (A-Z)`
H - `abecedně sestupně (Z-A)`
H - `podle pořadí projektu vzestupně`
H - `podle pořadí projektu sestupně`
H - V zápisu jednání se ruční volba řazení nabízet nemá; tam se má vždy použít pořadí subsystémů podle nastavení projektu v záložce `Osoby (tým)`.


## 3. Přehledy jednání a projektové analytické pohledy

H - Přehledy jednání mají nově seskupovat kartičky do rozbalovacích lišt po kalendářních rocích; vizuál kartiček se nemá měnit, mění se pouze rozložení.
H - V globální záložce `Jednání` mají být pod sebou projekty, každý projekt má mít řádek s posledními jednáními a možnost rozbalit historická jednání po rocích; roční lišty mají být výchozím stavem zabalené.
H - V projektové záložce `Jednání` se má začínat přímo ročními lištami pro vybraný projekt; aktuální rok má být výchozím stavem částečně rozbalený tak, aby byl vidět první řádek jednání v daném roce.
### 3.1 Projektový dashboard
- Přidat pro každý projekt samostatný `projektový dashboard` pro rychlou analytiku přímo v aplikaci bez potřeby SQL nebo Power BI; konkrétní grafy, statistiky a KPI budou doplněny později.

## 4. Tisk, historické zobrazení a pravidla pro snapshoty

H - Upravit historický tisk jednání tak, aby se záznamy nezobrazovaly dříve, než se mají na daném jednání skutečně objevit.
H - U projektů s `PouzivatIdentJednani` nesmí o zařazení záznamu do tisku rozhodovat jen `DatumZalozeni`; je nutné kontrolovat i posloupnost identifikátoru navázaného na zdrojové jednání.
H - Záznam s identifikátorem přiřazeným k pozdějšímu jednání se nesmí objevit už v tisku předchozího jednání, i kdyby měl starší nebo stejné `DatumZalozeni`; poprvé se má objevit až od jednání, ke kterému je identifikátorem skutečně navázaný.

## 5. Role, oprávnění a pravidla zápisu

H - Přidat základní projektovou roli `Projektový manažer` s kódem `PROJ_MAN`, vytvořenou už při seedování databáze v číselníku projektových rolí. Role je v databázi nyní doplněna, ale není s ní nikde počítáno v aplikaci. Potřeba je tedy upravit základní seed databáze, patch databáze není potřeba. Dále je potřeba doplnit potřebné kusy kódu do aplikace, aby se s rolí mohlo pracovat dle dalšího zadání.
H - Projektová role `Projektový manažer` má mít stejná implicitní oprávnění jako projektová role `Administrátor projektu`.
H - Projektová role `Projektový manažer` má být po seedování výchozím stavem zamknutá.
H - Upravit pravidla pro přidávání vyjádření podle stavu jednání:
H - Ve stavu `DRAFT` mohou vyjádření přidávat `Vedoucí subsystému` a `Zástupce vedoucího subsystému`, pokud jsou pro daný záznam relevantní podle subsystému.
H - Ve stavu `OPEN` se jejich speciální oprávnění neuplatní a zapisování se řídí už jen standardním oprávněním pro daný projekt.
H - Ve stavu `OPEN` tedy mohou vyjádření přidávat například `Projektový manažer`, `Administrátor projektu`, `APP_ADMIN`, `SUPERADMIN` nebo jiný uživatel se standardně povolenou potřebnou akcí pro daný projekt.
H - Pokud jednání není uzavřené ani zamčené, musí být umožněna úprava a smazání vyjádření uživatelům s projektovou rolí `Projektový manažer`, `Administrátor projektu`, dále `APP_ADMIN`, `SUPERADMIN` a každému dalšímu uživateli se standardním oprávněním `records.edit` v odpovídajícím rozsahu.
H - Oprávnění pro úpravu a smazání vyjádření se v tomto režimu musí vztahovat jak na vlastní, tak na cizí vyjádření.
H - Cílový scénář je možnost přeformulovat nebo smazat vyjádření, které ve stavu `DRAFT` přidal `Vedoucí subsystému` nebo `Zástupce vedoucího subsystému`, aniž by bylo nutné zakládat nové vyjádření místo úpravy původního.

## 6. Návrhové a schvalovací workflow

### Návrh založení záznamu

H - Přidat samostatný workflow pro `návrh založení záznamu`; návrh se před schválením nesmí ukládat do provozní tabulky `projektove_zaznamy` ani do navázaných provozních dat.
H - Návrh mohou vytvářet pouze uživatelé s rolí `Vedoucí subsystému` nebo `Zástupce vedoucího subsystému` pro relevantní subsystem.
H - O návrhu mohou rozhodovat pouze uživatelé s projektovou rolí `Projektový manažer` nebo `Administrátor projektu`.
H - Workflow má být jednoduchý: návrh lze pouze `schválit` nebo `zamítnout`, bez vyjádření k návrhu, bez vrácení k doplnění a bez dalších mezikroků.
H - Pro navrhovatele se má použít stejné UI jako pro běžné založení záznamu; pouze primární akce se změní z `Založit záznam` na `Odeslat návrh`.
H - Návrhy se mají ukládat do nové samostatné tabulky oddělené od provozních dat.
H - Vlastní data návrhu se mají ukládat jako serializovaný `JSON` payload odpovídající datům editoru záznamu.
H - Nová tabulka má evidovat minimálně stav návrhu, datum a autora podání návrhu, datum a uživatele rozhodnutí a serializovaná data návrhu.
H - Stav návrhu má pokrývat minimálně hodnoty `PENDING`, `APPROVED` a `REJECTED`.
H - Před rozhodnutím musí být možné načíst data návrhu do stejného formuláře jako při založení záznamu, upravit je podle potřeby schvalovatele a teprve poté schválit nebo zamítnout.
H - Při schválení návrhu se má skutečný záznam vytvořit až z finálně upravených dat; při zamítnutí se žádný provozní záznam nevytvoří.

### Návrh změny termínu ukončení a plánové části harmonogramu

H - Přidat samostatný workflow pro `návrh změny termínu ukončení záznamu` a `návrh změny plánové části harmonogramu`.
H - Workflow se má vztahovat pouze na `Termín ukončení` a plánovou část harmonogramu; skutečnost harmonogramu se do návrhu nesmí zahrnovat ani jím měnit.
H - Pokud pro záznam existuje nevyřízený návrh změny termínu nebo plánové části harmonogramu, musí být odpovídající pole v běžném editoru uzamčená až do vyřízení návrhu.
H - Navrhovat mohou stejné role jako u návrhu založení záznamu, tedy `Vedoucí subsystému` a `Zástupce vedoucího subsystému`.
H - Schvalovat nebo zamítat mohou stejné role jako u návrhu založení záznamu, tedy `Projektový manažer` a `Administrátor projektu`.
H - Workflow má být stejně jednoduchý: pouze `schválit` nebo `zamítnout`, bez vracení k doplnění a bez dalších mezikroků.
H - Při schválení se mají do provozních dat propsat pouze schválené změny `Termínu ukončení` a plánové části harmonogramu; skutečnost harmonogramu musí zůstat beze změny.

## 7. Vyhledávání, audit a systémové chování aplikace

- Doplnit do aplikace globální vyhledávání.
- Výsledky vyhledávání se mají filtrovat podle oprávnění a scope aktuálního uživatele; preferovaná varianta je hledat nad širším indexem a teprve nad výsledky aplikovat autorizaci.
- Vyhledávací engine má být postavený na `Elasticsearch` nebo `OpenSearch`; finální volba technologie se může rozhodnout později.
- Vyhledávání má být sémantické, ale zároveň praktické, předvídatelné a nepřehnaně „kreativní“.
- Zachovat a dotáhnout audit změn v celé aplikaci pro všechny akce, které mění data.
- Každá write akce musí auditně zaznamenat minimálně kdo akci provedl, kdy byla provedena, jaký typ akce proběhl, nad jakou entitou nebo záznamem proběhla a jaká data byla po změně uložena; pokud to dává smysl, audit má ukládat i stav před změnou.
- Stávající auditní řešení se má projít a ověřit na úplnost tak, aby žádná akce měnící data nezůstala mimo auditní log.

## 8. Pozdější a technicky náročnější fáze

### Integrace skutečnosti harmonogramu z ticketovacího systému

- V pozdější fázi přidat integraci pro dotažení `skutečnosti harmonogramu` z externího ticketovacího systému.
- Pro návrh pravidel integrace a parsování budou později dodány ukázkové tickety a ukázky komunikace z ticketovacího systému.
- Na základě dodaných ukázek a explicitně zadaných pravidel implementovat parser, který bude z textu ticketů a související komunikace rozpoznávat relevantní termíny pro promítnutí do `skutečnosti harmonogramu`.
- Detailní mapování zdrojových textů, pravidel parsování a převodu do harmonogramu bude doplněno až ve chvíli, kdy budou k dispozici konkrétní příklady a rozhodovací pravidla.

## 9. Uživatelský dashboard