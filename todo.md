# Požadované změny v aplikaci

Seznam je seskládaný od jednodušších úprav po složitější zásahy. Pořadí je orientační implementační backlog, ne finální release plán.

## 1. Drobné vizuální a UX opravy

- Upravit významové barvy textu pro `přípravu` a `nové informace`; modrá a červená mají být světlejší a na první pohled lépe rozeznatelné od běžného černého textu, bez změny jejich významu.
- Opravit vykreslení štítků s datumy na ose času v harmonogramu; levý krajní štítek je v pořádku, pravý se ořezává a chybí na něm přibližně jedna a půl číslice.
- Opravit nefunkční tlačítko `Rozpad` v harmonogramu tak, aby znovu správně rozbalovalo a sbalovalo detailní rozpad harmonogramu pro daný záznam.
- Ve stručném zobrazení harmonogramu `Plán / Skutečnost` neopisovat ani nevykreslovat kroky s nulovým `TrvaniDni` jako běžné indikátory; nesmí posouvat další skutečně vyplněné kroky ani vytvářet vizuální artefakty.
- V exportech `PDF` i `Word` změnit zvýraznění pozastaveného úkolu tak, aby byl světle hnědě podbarvený celý blok záznamu, nikoli jen hlavička.

## 2. Filtry, řazení a ovládání přehledů

- Filtr `Pouze aktivní úkoly` má být ve výchozím stavu aktivní.
- Do projektové záložky `Osoby (tým)` doplnit základní filtrování tabulky rolí a přístupů; pokud nebude vhodný plný filtr po sloupcích, musí být minimálně možné tabulku základně řadit podle sloupců.
- Do globální záložky `Osoby` doplnit obdobné základní filtrování, aby bylo možné dohledávat konkrétní osoby i při větším počtu záznamů.
- Při spuštění `Tisk projektu` se má aplikace pokaždé zeptat, zda použít aktuálně aktivní projektové filtry, nebo tisknout celý projekt; volba se nesmí ukládat a pokud není aktivní žádný relevantní filtr, dotaz se nemá zobrazovat.

## 3. Přehledy jednání a projektové analytické pohledy

- Přehledy jednání mají nově seskupovat kartičky do rozbalovacích lišt po kalendářních rocích; vizuál kartiček se nemá měnit, mění se pouze rozložení.
- V globální záložce `Jednání` mají být pod sebou projekty, každý projekt má mít řádek s posledními jednáními a možnost rozbalit historická jednání po rocích; roční lišty mají být výchozím stavem zabalené.
- V projektové záložce `Jednání` se má začínat přímo ročními lištami pro vybraný projekt; aktuální rok má být výchozím stavem částečně rozbalený tak, aby byl vidět první řádek jednání v daném roce.
- Přidat pro každý projekt samostatný `projektový dashboard` pro rychlou analytiku přímo v aplikaci bez potřeby SQL nebo Power BI; konkrétní grafy, statistiky a KPI budou doplněny později.

## 4. Tisk, historické zobrazení a pravidla pro snapshoty

- Doplnit číslování stránek do exportů `PDF` i `Word`.
- V zápisu jednání zobrazovat u položky `Subsystém` hodnotu ve formátu `Zkratka subsystému - Název subsystému` namísto samotného názvu subsystému.
- Upravit historický tisk jednání tak, aby se záznamy nezobrazovaly dříve, než se mají na daném jednání skutečně objevit.
- U projektů s `PouzivatIdentJednani` nesmí o zařazení záznamu do tisku rozhodovat jen `DatumZalozeni`; je nutné kontrolovat i posloupnost identifikátoru navázaného na zdrojové jednání.
- Záznam s identifikátorem přiřazeným k pozdějšímu jednání se nesmí objevit už v tisku předchozího jednání, i kdyby měl starší nebo stejné `DatumZalozeni`; poprvé se má objevit až od jednání, ke kterému je identifikátorem skutečně navázaný.

## 5. Role, oprávnění a pravidla zápisu

- Přidat základní projektovou roli `Projektový manažer` s kódem `PROJ_MAN`, vytvořenou už při seedování databáze v číselníku projektových rolí.
- Projektová role `Projektový manažer` má mít stejná implicitní oprávnění jako projektová role `Administrátor projektu`.
- Projektová role `Projektový manažer` má být po seedování výchozím stavem zamknutá.
- Upravit pravidla pro přidávání vyjádření podle stavu jednání:
- Ve stavu `DRAFT` mohou vyjádření přidávat `Vedoucí subsystému` a `Zástupce vedoucího subsystému`, pokud jsou pro daný záznam relevantní podle subsystému.
- Ve stavu `OPEN` se jejich speciální oprávnění neuplatní a zapisování se řídí už jen standardním oprávněním pro daný projekt.
- Ve stavu `OPEN` tedy mohou vyjádření přidávat například `Projektový manažer`, `Administrátor projektu`, `APP_ADMIN`, `SUPERADMIN` nebo jiný uživatel se standardně povolenou potřebnou akcí pro daný projekt.
- Pokud jednání není uzavřené ani zamčené, musí být umožněna úprava a smazání vyjádření uživatelům s projektovou rolí `Projektový manažer`, `Administrátor projektu`, dále `APP_ADMIN`, `SUPERADMIN` a každému dalšímu uživateli se standardním oprávněním `records.edit` v odpovídajícím rozsahu.
- Oprávnění pro úpravu a smazání vyjádření se v tomto režimu musí vztahovat jak na vlastní, tak na cizí vyjádření.
- Cílový scénář je možnost přeformulovat nebo smazat vyjádření, které ve stavu `DRAFT` přidal `Vedoucí subsystému` nebo `Zástupce vedoucího subsystému`, aniž by bylo nutné zakládat nové vyjádření místo úpravy původního.

## 6. Návrhové a schvalovací workflow

### Návrh založení záznamu

- Přidat samostatný workflow pro `návrh založení záznamu`; návrh se před schválením nesmí ukládat do provozní tabulky `projektove_zaznamy` ani do navázaných provozních dat.
- Návrh mohou vytvářet pouze uživatelé s rolí `Vedoucí subsystému` nebo `Zástupce vedoucího subsystému` pro relevantní subsystem.
- O návrhu mohou rozhodovat pouze uživatelé s projektovou rolí `Projektový manažer` nebo `Administrátor projektu`.
- Workflow má být jednoduchý: návrh lze pouze `schválit` nebo `zamítnout`, bez vyjádření k návrhu, bez vrácení k doplnění a bez dalších mezikroků.
- Pro navrhovatele se má použít stejné UI jako pro běžné založení záznamu; pouze primární akce se změní z `Založit záznam` na `Odeslat návrh`.
- Návrhy se mají ukládat do nové samostatné tabulky oddělené od provozních dat.
- Vlastní data návrhu se mají ukládat jako serializovaný `JSON` payload odpovídající datům editoru záznamu.
- Nová tabulka má evidovat minimálně stav návrhu, datum a autora podání návrhu, datum a uživatele rozhodnutí a serializovaná data návrhu.
- Stav návrhu má pokrývat minimálně hodnoty `PENDING`, `APPROVED` a `REJECTED`.
- Před rozhodnutím musí být možné načíst data návrhu do stejného formuláře jako při založení záznamu, upravit je podle potřeby schvalovatele a teprve poté schválit nebo zamítnout.
- Při schválení návrhu se má skutečný záznam vytvořit až z finálně upravených dat; při zamítnutí se žádný provozní záznam nevytvoří.

### Návrh změny termínu ukončení a plánové části harmonogramu

- Přidat samostatný workflow pro `návrh změny termínu ukončení záznamu` a `návrh změny plánové části harmonogramu`.
- Workflow se má vztahovat pouze na `Termín ukončení` a plánovou část harmonogramu; skutečnost harmonogramu se do návrhu nesmí zahrnovat ani jím měnit.
- Pokud pro záznam existuje nevyřízený návrh změny termínu nebo plánové části harmonogramu, musí být odpovídající pole v běžném editoru uzamčená až do vyřízení návrhu.
- Navrhovat mohou stejné role jako u návrhu založení záznamu, tedy `Vedoucí subsystému` a `Zástupce vedoucího subsystému`.
- Schvalovat nebo zamítat mohou stejné role jako u návrhu založení záznamu, tedy `Projektový manažer` a `Administrátor projektu`.
- Workflow má být stejně jednoduchý: pouze `schválit` nebo `zamítnout`, bez vracení k doplnění a bez dalších mezikroků.
- Při schválení se mají do provozních dat propsat pouze schválené změny `Termínu ukončení` a plánové části harmonogramu; skutečnost harmonogramu musí zůstat beze změny.

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
