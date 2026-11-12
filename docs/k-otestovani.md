# K otestování — hromadné ruční ověření

Průběžný seznam změn, které jsou v lokálním `main`, prošly automatickými testy a čekají na ruční ověření (i15 / Edge, IIS). Po otestování položku odškrtni nebo smaž.

Nasazení: kód je jen v lokálním `main` (bez pushe). Žádná změna DB schématu, pokud není u položky uvedeno jinak.

---

## 1. Pohled jako (superadmin vidí aplikaci očima jiného uživatele)

**Hotovo 2026-10-20:** merge `062e371`, plán `docs/superpowers/plans/2026-10-20-pohled-jako.md`, popis v `docs/authorization.md` (sekce „Pohled jako“).

**Co to dělá:**
- Superadmin má v menu účtu (vpravo nahoře) položku **Zobrazit jako…**. Otevře modal s výběrem osoby.
- Aplikace se pak chová jako pro vybranou osobu: role, práva, viditelné projekty, menu i „Moje práva“.
- Pod hlavičkou je žlutá lišta „Prohlížíte aplikaci jako X, jen pro čtení. Váš účet: Y“ s tlačítkem **Zrušit pohled jako**.
- Režim je **jen pro čtení**: žádné uložení neprojde, zámek karty se nezakládá ani neprodlužuje.
- Pohled končí zrušením nebo zavřením prohlížeče a platí ve všech záložkách.
- Zahájení a ukončení se zapisuje do `authz.audit_log` (`entity_type = 'pohled_jako'`).
- Vedlejší oprava: položka v rozbalovacím menu, která otevírá modal, menu zavře (dřív zůstávalo otevřené nad dialogem).

**Otestovat (přihlášen jako superadmin):**
- [ ] Menu účtu → je tam „Zobrazit jako…“. Běžný uživatel bez superadmina ji **nevidí**.
- [ ] Klik na „Zobrazit jako…“ → menu se zavře, otevře se modal „Zobrazit aplikaci jako jiný uživatel“.
- [ ] V modalu vyhledat osobu (jméno, e-mail, login), vybrat, „Zobrazit jako“ → přesměruje na Přehled a ukáže lištu.
- [ ] Sám sebe vybrat nejde: modal ukáže chybu u pole.
- [ ] Hlavička vpravo ukazuje jméno cílové osoby a hlavní menu jen její sekce (např. bez Nastavení a Číselníků).
- [ ] Projekty: vidíš jen projekty cílové osoby. Profil → „Moje práva“ ukazuje její role.
- [ ] Projděte detail projektu (všechny záložky), záznam, editor záznamu, Jednání, Hledání: vše se načte, nic nespadne.
- [ ] Editor záznamu → „Uložit“: červená hláška „V režimu ‚Zobrazit jako‘ nelze nic ukládat…“ (ne „Relace vypršela“, ne „Obnovit stránku“).
- [ ] Jiný modal s uložením (např. nové jednání, člen týmu, vyjádření): uložení odmítne se stejnou hláškou.
- [ ] Záznam, který právě edituje někdo jiný: v pohledu se ukáže stránka „záznam upravuje X“, stejně jako by ji viděla cílová osoba. Druhému uživateli se zámek nezmění.
- [ ] Tisk / PDF exportu v pohledu funguje (je to čtení).
- [ ] „Zrušit pohled jako“ → zůstaneš na stejné stránce, lišta zmizí, vidíš zase vše jako admin.
- [ ] Nová záložka během pohledu je taky v pohledu. Po zavření a otevření prohlížeče už pohled neběží.
- [ ] Vzhled lišty na i15 / Edge: světlý i tmavý motiv, úzké okno (tlačítko spadne pod text, nic nepřetéká).
- [ ] V DB: `SELECT * FROM authz.audit_log WHERE entity_type = N'pohled_jako' ORDER BY id DESC` → záznamy `view_as_start` / `view_as_stop`, actor = ty, entity_id = cílová osoba.

**Známá omezení (vědomě):**
- Stránka, kam cílová osoba nesmí (prázdná 403), lištu nemá. Stačí se vrátit zpět nebo na Přehled.
- Některé starší akce mimo modaly (chat vyjádření, synchronizace tiketu, přetažení vazby) při pokusu o změnu ukážou obecnou chybu místo konkrétní hlášky. Zápis je i tak zablokovaný.
- Vyhledávání osob v modalu nemusí najít jméno, když hledáš bez diakritiky (převzatý existující dotaz).

---

## 2. Přehled — nadpisy bloků

**Hotovo 2026-10-20:** merge `45dd77d`, spec `docs/superpowers/specs/2026-10-20-prehled-nadpisy-bloku-design.md`, plán `docs/superpowers/plans/2026-10-20-prehled-nadpisy-bloku.md`.

**Co to dělá:**
- Tři bloky Přehledu mají kompaktní nadpis: „Na co se soustředit“, „Nejbližší jednání“, „Co je nového“. Texty jsou stejné jako titulky stránek „Zobrazit více“ a berou se z jednoho místa v kódu.
- Nadpis zabere 32 px. Počet viditelných karet záznamů se nezměnil (lokálně 1400×900 = 5 karet, 1920×1080 = 6 karet, stejně jako dřív).
- Nadpis je vidět hned, i během načítání (nad šedým skeletonem) a i když se obsah bloku nenačte.
- Pro čtečky obrazovky má stránka skrytý nadpis „Přehled“ a každý blok je pojmenovaný svým nadpisem.

**Otestovat:**
- [ ] Přehled na i15 / Edge: tři nadpisy, malé tučné písmo, každý na jednom řádku.
- [ ] Levý blok: vejde se stejný počet karet záznamů jako dřív. Seznam roluje a nadpis zůstává nahoře.
- [ ] Pravý sloupec: jednání a novinky se dělí výškou jako dřív, nic se neořezává. Bez jednání: nadpis + „Nejsou naplánovaná žádná blízká jednání.“, novinky dostanou zbytek.
- [ ] Při načítání stránky je nadpis vidět už nad šedým skeletonem.
- [ ] Tmavý motiv a úzké okno: nadpisy čitelné, nic nepřetéká.
- [ ] „Zobrazit více“ u bloku → titulek záložky i drobečková navigace mají stejný název jako nadpis bloku.
- [ ] V režimu „Zobrazit jako“ jsou nadpisy pod žlutou lištou a Přehled se načte celý.

**Známá omezení (vědomě):** žádná.

---

## 3. Tisk jednání a „Znovu vytěžit“ pro projektové role

**Hotovo 2026-10-20:** merge `14c0703`, plán `docs/superpowers/plans/2026-10-20-tisk-jednani-projektove-role.md`.

**Co se opravilo:**
- PM, vedoucí subsystému a další role bez globální role (vlastník, admin projektu, gestor, host, zástupce, metodik) dostávali u tisku a Wordu jednání ze **seznamu Jednání** a ze **záložky Jednání v projektu** „Přístup odepřen“. Z detailu jednání to fungovalo už od července. Odkazy teď nesou projekt. Kombinace „PM + vedoucí subsystému“ nebyla příčinou: stejně dopadl každý bez globální role, superadmin a správce aplikace chybu neviděli.
- Vlastník, admin projektu a PM dostávali „Přístup odepřen“ u **„Znovu vytěžit“** v okně vyjádření tiketu. Opraveno. Server navíc ověřuje, že tiket patří do projektu: dřív šlo podvrženým projektem vytěžit tiket cizího projektu.
- Nový automatický test hlídá všechny odkazy a formuláře na akce s projektovým oprávněním (dnes 37 odkazů a 14 formulářů), aby žádný neztratil projekt.

**Ověřeno lokálně v prohlížeči:** za Ing. Janu Testerovou (PM + vedoucí subsystému v projektu PMT) přes „Zobrazit jako“ vrátil tisk PDF i Word ze seznamu Jednání, ze záložky projektu i z detailu jednání soubor. „Znovu vytěžit“ za ni proběhlo. Uživatel bez role v projektu tisk dál neotevře.

**Otestovat (přihlášen jako PM nebo vedoucí subsystému bez admin role, nebo superadmin přes „Zobrazit jako“):**
- [ ] Jednání → karta jednání → tiskárna → PDF: otevře se tisk, ne „Přístup odepřen“.
- [ ] Totéž → WORD: stáhne se dokument.
- [ ] Detail projektu → záložka Jednání → tisk PDF i Word.
- [ ] Detail jednání → tisk PDF i Word (fungovalo i dřív, nesmí se rozbít).
- [ ] Jednání ze starších let (rozbalit historii) → tisk funguje.
- [ ] Uživatel ve více projektech: tisk jednání z každého projektu funguje.
- [ ] Jako PM, vlastník nebo admin projektu (ne přes „Zobrazit jako“, ten zápisy blokuje): záznam s externím odkazem → okno vyjádření → „Znovu vytěžit“ → proběhne, ne „Vytěžování selhalo (HTTP 403)“.
- [ ] Uživatel bez role v projektu tisk jednání toho projektu dál neotevře.

**Známá omezení (vědomě):** staré záložky nebo přímé URL tisku bez `projektId` fungují jen globálním uživatelům, stejně jako dřív.

---

## 4. Detail jednání se záznamy

**Hotovo 2026-10-21:** merge `1152bc5`, plán `docs/superpowers/plans/2026-10-21-detail-jednani-zaznamy.md`.

**Co to dělá:**
- **Jednání v přípravě nebo otevřené:** nahoře úzký pruh s datem, místem, stavem a akcemi (Uložit stav / Uzavřít jednání, Tisk, Nový záznam, Upravit jednání, Smazat). Pod ním je účast na jeden řádek, rozbalitelná, a pod ní celá stránka Záznamy (filtry + karty). Je to stejná komponenta jako záložka Záznamy v projektu, ne kopie.
- **Vyjádření** na kartě má jako „Vazba na jednání“ předvybrané toto jednání (u přípravy přípravné vyjádření). Jiné jednání jde vybrat. Předvolba zůstane i po uložení, úpravě a smazání vyjádření.
- **Tužka** otevře editor „z jednání“. Po změně stavu má „Změna stavu platí k“ předvybrané toto jednání, pokud ho pravidla ze 13. 10. nabízejí. Jinak platí výchozí volba jako dřív. Uložení i „Zrušit a vrátit se“ vede zpět do jednání.
- **Nový záznam** z pruhu založí záznam k tomuto jednání.
- **Filtry** v jednání jsou jen pro toto jednání a jen do zavření prohlížeče. Nejde je uložit jako výchozí a záložku Záznamy projektu neovlivní.
- **Uzavřené jednání:** jen pruh se stavem (a „Otevřít jednání“ pro toho, kdo smí) a pod ním zápis, stejný jako PDF tisk.
- Kdo stav jednání mění, vidí ho ve výběru. Ostatní ho vidí jako štítek.

**Ověřeno lokálně v prohlížeči za všechny role** (dočasné testovací osoby, po průchodu smazané; záznam vlastního a cizího subsystému; jednání otevřené, v přípravě a uzavřené):

| Role | Pruh: stav, Nový záznam, Upravit, Smazat, tužka | Vyjádření v otevřeném jednání (vlastní / cizí subsystém) | Vyjádření v přípravě (vlastní / cizí) | Uzavřené jednání |
|---|---|---|---|---|
| Superadmin, Administrátor aplikace | ano | předvybráno / předvybráno, uloženo | předvybráno / předvybráno | zápis + Otevřít jednání |
| Vlastník, Admin projektu, PM, PM + vedoucí | ano | předvybráno / předvybráno, uloženo | předvybráno / předvybráno | zápis + Otevřít jednání |
| Gestor, Metodik subsystému | ne | předvybráno / předvybráno, uloženo | předvybráno / předvybráno | zápis |
| Vedoucí, Zástupce vedoucího subsystému | ne | jen příprava, nic nepředvybráno / bez formuláře | předvybráno, uloženo / bez formuláře | zápis |
| Host, Read-all | ne | bez formuláře | bez formuláře | zápis |
| Bez role v projektu | stránka neexistuje (404) | – | – | – |

Navíc prošlo:
- Tužka ze staršího otevřeného jednání předvybrala to jednání, ne nejnovější, a změna stavu se uložila k jeho datu.
- Server odmítl podvržené akce: editor a Nový záznam z jednání pro gestora, hosta, vedoucího, zástupce a read-all (403); vyjádření vedoucího k otevřenému jednání nebo k cizímu subsystému (odmítnuto).
- „Zobrazit jako“ ukázal stránku jen pro čtení.
- Filtry jsou oddělené od projektu i od ostatních jednání.
- Tmavý motiv i šířka 375 px bez vodorovného posuvníku.
- V konzoli prohlížeče žádné chyby.

**Otestovat (PM nebo admin projektu, ne superadmin; vedoucí subsystému; host):**
- [ ] Otevřené jednání: pruh nahoře na jeden řádek, účast rozbalitelná, záznamy pod ní. Totéž u jednání v přípravě.
- [ ] Vyjádření na kartě: předvybrané toto jednání. Uložení ho zapíše k tomuto jednání a výběr zůstane.
- [ ] Jiné jednání jde ve výběru zvolit.
- [ ] Tužka → změnit stav → „Změna stavu platí k“ = toto jednání → Uložit → zpět v detailu jednání. V tisku jednání má záznam nový stav.
- [ ] Nový záznam z pruhu → založí se k tomuto jednání.
- [ ] Filtry v jednání neovlivní záložku Záznamy projektu a naopak. Po zavření prohlížeče jsou výchozí.
- [ ] Uzavřené jednání: jen pruh a zápis. Zápis odpovídá PDF tisku, stránka má jeden posuvník.
- [ ] Vedoucí subsystému: v přípravě píše vyjádření ke svému subsystému (předvybráno), v otevřeném jednání ne.
- [ ] Host: vidí záznamy i zápis, nic nemění.
- [ ] Záložka Záznamy v detailu projektu funguje jako dřív (tužka, vyjádření bez předvolby, uložení filtrů jako výchozích).
- [ ] i15 / Edge: světlý i tmavý motiv, úzké okno.

**Známá omezení (vědomě):**
- Žádné značky „má / nemá vyjádření k tomuto jednání“ (rozhodnutí R7, udělá se později jinak).
- Zápis uzavřeného jednání má vzhled tisku (bílý papír i v tmavém motivu).
- Předvolba jde změnit (rozhodnutí 2026-10-21: pevná volba ne, kvůli kolizním stavům).

---

## 5. Vyjádření v jednom kompaktním bloku

**Hotovo 2026-10-22:** merge `a74ec4d`, plán `docs/superpowers/plans/2026-10-22-vyjadreni-kompaktni-blok.md`, spec `docs/superpowers/specs/2026-10-22-vyjadreni-kompaktni-blok-design.md`.

**Co to dělá** (karta v záložce Záznamy, detail jednání i stránka záznamu — jedna sdílená komponenta):
- Všechna zobrazená vyjádření jsou v **jednom ohraničeném bloku**. Řádky nemají vlastní rámeček. Řádek pod myší (nebo s fokusem klávesnice) se podbarví **jemně šedou**.
- **Autor a datum** už nejsou trvale nad vyjádřením. Ukáže je **bublina při najetí na štítek jednání** vpravo (i klávesou Tab nebo klepnutím).
- **Upravit / Smazat** (tužka, koš) se objeví jako ikony **při najetí na řádek**, vlevo od štítku. Text se neposune.
- U vyjádření k **uzavřenému jednání** se ikony neobjeví ani při najetí. Podvrženou úpravu nebo smazání server odmítne (tak to bylo i dřív, nově to hlídá test).
- Panel ukáže **posledních 10 vyjádření** (dřív 5). „**Další (5)**“ (při vzestupném řazení „**Předchozí (5)**“) a „**Zobrazit vše**“ jsou v hlavičce **vlevo vedle řazení**, v obou směrech řazení na stejném místě.
- 5 jednořádkových vyjádření zabere 190 px (dřív 600 px).

**Ověřeno lokálně v prohlížeči za všechny role** (13 dočasných osob, po průchodu smazané; 12 vyjádření různých autorů k jednání v přípravě, otevřenému a uzavřenému; 3 místa):

| Role | Blok, bublina s autorem a datem, šedé podbarvení | Ikony Upravit / Smazat při najetí |
|---|---|---|
| Superadmin, Administrátor aplikace, Vlastník, Admin projektu, PM, PM + vedoucí | ano (12/12) | u všech vyjádření k přípravě a otevřenému jednání, **u uzavřeného ne** |
| Vedoucí, Zástupce vedoucího, Gestor, Metodik | ano (12/12) | jen u **vlastního** vyjádření k jednání **v přípravě** |
| Host, Read-all | ano (12/12) | nikde |
| Bez role v projektu | stránka neexistuje (404) | – |

Navíc prošlo:
- U všech rolí a míst: nejdřív 10 vyjádření, tlačítka v hlavičce vlevo od řazení vzestupně i sestupně, „Zobrazit vše“ → 12 a tlačítka zmizí.
- Kliknutím: PM po „Další“ upravil vyjádření (panel dál ukazuje 12) a jedno smazal (potvrzovací dotaz). Vedoucí a zástupce upravili své vyjádření v přípravě.
- Podvržená úprava vyjádření k uzavřenému jednání: odmítnuto („Vyjádření u uzavřeného jednání nelze upravovat ani mazat…“).
- Tmavý motiv: šedé podbarvení je vidět. Šířka 375 px: bez vodorovného posuvníku, tlačítka v hlavičce se zalomí. Tab na štítek ukáže bublinu.
- V konzoli prohlížeče žádné chyby.

**Otestovat (PM nebo admin projektu, ne superadmin; vedoucí subsystému; host):**
- [ ] Záložka Záznamy → karta s vyjádřeními: jeden rámeček kolem všech vyjádření, řádek pod myší šedý.
- [ ] Najetí na štítek „Jednání č. …“: bublina se jménem autora a „Zapsáno dd.mm.rrrr“. U posledního vyjádření se bublina neořízne.
- [ ] Najetí na řádek: tužka a koš vlevo od štítku. Tužka otevře editor přes celou šířku, Zrušit / Uložit funguje. Koš se zeptá a smaže.
- [ ] Vyjádření k uzavřenému jednání: ani při najetí žádné ikony.
- [ ] Záznam s více než 10 vyjádřeními: vidět 10, vpravo nahoře „Další (5)“ / „Předchozí (5)“ a „Zobrazit vše“ vedle řazení. Po přepnutí řazení zůstanou na místě.
- [ ] Totéž v detailu otevřeného jednání a na stránce záznamu.
- [ ] Vedoucí subsystému: ikony jen u svého vyjádření k jednání v přípravě.
- [ ] Host: bubliny ano, ikony nikde.
- [ ] Odkaz z vyhledávání na starší vyjádření (mimo prvních 10): karta ho načte, posune se k němu a podsvítí.
- [ ] i15 / Edge: světlý i tmavý motiv, úzké okno.

**Známá omezení (vědomě):**
- Autor a datum nejsou vidět na první pohled, jen v bublině (zadání). Tisk a PDF je dál obsahují.
- Ikony při najetí na chvíli zakryjí konec prvního řádku textu (hlavně na úzkém okně).
- ~~Nesoulad UI × server u vlastních vyjádření~~ — vyřešeno 2026-11-11, viz §10. (Průběžná poznámka „smazání projde“ u Gestora a Metodika v přípravě byla chybná: server smazání odmítal stejně jako úpravu — ověřeno testem před změnou.)
- Stránka záznamu má na 375 px vodorovný posuvník kvůli tlačítkům v hlavičce stránky. Bylo to tak i dřív a s vyjádřeními to nesouvisí.

## 6. Záznam — hlavička vlevo, detaily v tabulce, procházení šipkami, mazání data plánu

**Hotovo 2026-10-22:** merge `4ca299f`, plán `docs/superpowers/plans/2026-10-22-zaznam-hlavicka-detaily-sipky-plan.md`, spec `docs/superpowers/specs/2026-10-22-zaznam-hlavicka-detaily-sipky-plan-design.md`.

**Co to dělá:**
- **A – hlavička karty zarovnaná vlevo.** Šipka rozbalení (›) je v levém okraji karty. Štítky, nadpis, detaily i vyjádření začínají na jedné svislé čáře (dřív hlavička odskočila o 21 px).
- **B – detaily v tabulce.** Karta i stránka záznamu mají dvě dvojice na řádek: Vlastník | Subsystém, Termín | Typ úkolu, Založeno. Přeškrtnutá historie zůstává u hodnoty. Bez typu úkolu jeho buňky chybí. Na úzkém okně (pod 640 px) je jedna dvojice na řádek.
  - Řádek „Termín · Založeno“ v hlavičce rozbalené karty zmizel, protože obojí je v tabulce. Na sbalené kartě a v pohledu Harmonogram (tabulka je tam skrytá) zůstává.
  - Stránka záznamu už nemá samostatný řádek „Založeno“.
- **C – záznamy jednání a procházení šipkami.**
  - Detail otevřeného nebo připravovaného jednání ukáže **jen záznamy, které do jednání patří**, tedy stejné jako zápis (tisk). Filtry fungují uvnitř nich. Když do jednání nepatří nic: „Do tohoto jednání zatím nepatří žádný záznam.“
  - V menu › na kartě je **první položka „Otevřít na celou obrazovku“**. Otevře stránku záznamu ve stejné kartě prohlížeče. „Otevřít na nové kartě“ zůstává.
  - Stránka záznamu otevřená z menu má nahoře **‹ Předchozí · N / M · Další ›** a funguje i **klávesami ← →**. Z detailu jednání se prochází záznamy jednání v pořadí zápisu, ze záložky Záznamy aktivní záznamy projektu ve stejném pořadí. Filtry ani řazení na stránce se nepřebírají. Na krajích se nepřetáčí.
  - Klávesy nereagují při psaní (pole, výběr, editor vyjádření), v otevřeném okně ani s Alt/Ctrl/Shift/Cmd.
  - Drobečková lišta vede zpět tam, odkud jsem přišel. Úprava a návrh změny harmonogramu se vrátí na stránku i se šipkami.
- **D – datum plánu jde smazat.** Pole plánu v harmonogramu má **✕**, kdo plán smí upravit (editor záznamu, návrh změny harmonogramu). Krok bez plánu má trvání **„–“**, další krok se počítá od posledního kroku s plánem.
  - Návrh změny harmonogramu nese i odstranění plánu. Detail návrhu ukáže „Původní plán: X | Navržený: bez plánu“, schválení plán smaže.
  - Navíc: trvání kroku se v editoru přepočítá hned po změně data i po ✕ (dřív se nepřepočítávalo vůbec). Vrátila se i kontrola pořadí: plán dřív než plán předchozího kroku pole označí a uložení zastaví (server takové uložení odmítal i dřív).

**Ověřeno lokálně:**
- Automatické testy: Unit 2272, Api 532, JS 56 – vše zelené; Integration 203/204 (jediné selhání je známé `ProposalRejectAndTakeOver…`); E2E 123 testů, 32 selhání = stejných 32 jmen jako `main` (nová žádná), 6 nových prošlo.
- Tisk zápisu je stejný jako před změnou. Jednání 906 i 902 mají na `main` i na větvi stejných 14 záznamů ve stejném pořadí.
- **V prohlížeči za všechny role** (13 dočasných osob, po průchodu smazané; jednání 906 a 902 projektu PMT, záznam #901-1 s dočasným plánem):

| Role | Detail jednání = zápis | Menu → celá obrazovka → ← → → zpět | Záložka Záznamy → aktivní | ✕ u plánu |
|---|---|---|---|---|
| Superadmin, Administrátor aplikace, Vlastník, Admin projektu, PM, PM + vedoucí | ano (14 = 14) | ano, „2 / 14“, sousedé podle zápisu | ano, „2 / 14“; uzavřený záznam bez šipek | v editoru (i v návrhu) |
| Vedoucí, Zástupce vedoucího | ano | ano | ano | jen v návrhu změny harmonogramu; editor odepřen |
| Gestor, Metodik, Host, Read-all | ano | ano | ano | nikde (editor i návrh odepřeny) |
| Bez role v projektu | 404 | – | – | – |

Navíc prošlo:
- PM prošel šipkou → celý seznam jednání 906: pořadí stránek = pořadí zápisu, na posledním → nic neudělá.
- Hlavička a tabulka na šířce 1400 i 375 px: odskok 0 px, na 375 px jedna dvojice na řádek, karta bez vodorovného posuvníku.
- Kliknutím: PM v editoru ✕ u kroku 3 → „–“, další krok „14 dnů“ → uložit → v DB `NULL`. Vedoucí v návrhu ✕ u kroku 4 → odeslat → PM v detailu návrhu vidí „Navržený: bez plánu“ a prázdné pole → Schválit → v DB `NULL`.
- Tmavý motiv: tabulka, navigace i šipka v okraji v pořádku. V konzoli prohlížeče žádné chyby.
- Odepřený editor nebo návrh vrací v lokálním dev běhu 500 („No DefaultForbidScheme“). V produkci s Windows Auth je to 403, dev artefakt.

**Otestovat (PM nebo admin projektu, ne superadmin; vedoucí subsystému; host):**
- [ ] Záložka Záznamy: rozbalená karta má štítky, nadpis, tabulku detailů i vyjádření zarovnané na jedné čáře; šipka › je v levém okraji. Sbalená karta ukazuje „Termín · Založeno“.
- [ ] Tabulka detailů: Vlastník | Subsystém, Termín | Typ úkolu, Založeno. Záznam s historií (změněný termín nebo vlastník): přeškrtnuté staré hodnoty. Informace nebo Rozhodnutí bez typu úkolu: buňky typu chybí.
- [ ] Přepínač Harmonogram na kartě: řádek „Termín · Založeno“ v hlavičce je vidět.
- [ ] Stránka záznamu: tabulka detailů místo samostatného „Založeno“.
- [ ] Detail otevřeného jednání: stejné záznamy jako v jeho zápisu (Tisk). Uzavřený záznam, který byl uzavřený už k minulému jednání, tam není.
- [ ] Menu › → „Otevřít na celou obrazovku“ (první položka) → stránka ve stejné kartě, „N / M“. Šipky ← → i tlačítka přepínají záznamy v pořadí zápisu, na krajích je tlačítko neaktivní. Drobečková lišta vrátí do jednání.
- [ ] Totéž ze záložky Záznamy: prochází se jen aktivní záznamy. Uzavřený záznam otevřený z menu šipky nemá.
- [ ] Na stránce záznamu psát do vyjádření a používat ← →: kurzor se posouvá, stránka se nepřepne.
- [ ] „Upravit“ na stránce se šipkami → uložit nebo zrušit → návrat na stránku i se šipkami.
- [ ] Editor záznamu → Harmonogram: ✕ u plánu kroku → pole prázdné, trvání „–“, další krok se přepočítá → uložit → plán kroku je prázdný.
- [ ] Editor: plán kroku dřív než plán předchozího kroku → pole označené, uložení se zastaví s hláškou.
- [ ] Vedoucí subsystému: návrh změny harmonogramu → ✕ u plánu → odeslat → PM v detailu návrhu vidí „Navržený: bez plánu“ → Schválit → plán prázdný.
- [ ] i15 / Edge: světlý i tmavý motiv, úzké okno (tabulka po jedné dvojici).

**Známá omezení (vědomě):**
- Detail jednání ukazuje **méně záznamů než dřív**: jen ty, které patří do zápisu (záměr R6).
- Šipky **nerespektují filtry ani řazení** na stránce (R7, R8). Prochází se celý seznam jednání nebo všechny aktivní záznamy.
- **Graf harmonogramu v editoru** se po změně data ani po ✕ nepřekreslí až do uložení. Tak to bylo i dřív, graf v editoru na datum nereaguje. Karta, stránka záznamu i záložka Harmonogram krok bez plánu nekreslí.
- Kontrola pořadí plánů v editoru je znovu zapnutá. Kdo dřív uložil nechronologický plán, dostal chybu až ze serveru, teď ji uvidí hned v editoru.
- Stránka záznamu má na 375 px vodorovný posuvník kvůli tlačítkům v hlavičce stránky. Bylo to tak i dřív (viz sekce 5), touto změnou se to nezhoršilo.

## 7. Harmonogram — stav u kroku bez plánu, živý přepočet editoru

**Hotovo 2026-10-24:** merge `ac6c98b`, plán `docs/superpowers/plans/2026-10-24-harmonogram-stav-bez-planu-plan.md`, spec `docs/superpowers/specs/2026-10-24-harmonogram-stav-bez-planu-design.md`.

**Co to dělá:**
- **Stav nad harmonogramem u kroku bez plánu.** Když aktuální krok nemá vlastní plánové datum, skluz se počítá k **celkovému termínu záznamu** a text to řekne: „Aktuální krok: 5. vyporadani pripominek · plán není zadán · celkový termín: zbývá 12 dní“ (po termínu „skluz +10 dní“, v den termínu „celkový termín je dnes“). Dřív se počítalo k dopočtenému datu (datum založení nebo plán předchozího kroku) a vycházely nesmysly, např. „skluz +108 dní“.
  - Když aktuální krok plán má, počítá se dál k němu („skluz +5 dní“, „v předstihu 3 dny“, „dle plánu“), i kdyby dřívější kroky plán neměly.
  - Štítek **Stíháme/Nestíháme** a barva stavu se řídí stejným číslem, u záznamu bez plánu tedy podle celkového termínu.
- **Skloňování:** „1 den“, „2–4 dny“, „5 dní“; „zbývá 1 den“, „zbývají 3 dny“. Platí i pro dosavadní texty (dřív „skluz +1 dní“).
- **Plán dokončení / Plán:** když nemá plán žádný krok, „není zadán“ a v editoru Trvání „–“ (dřív datum založení a „0 dnů“).
- **Rozpad:** krok se skutečností, ale bez plánu má šedý štítek **„bez plánu“** místo „+N dnů“. Odchylka u kroků s plánem se skloňuje („+1 den“, „+3 dny“, „+5 dnů“, „-2 dny“).
- **Editor se přepočítává hned.** Výběr data i ✕ u plánu nebo skutečnosti, i změna termínu, hned přepočítá Stav, Plán dokončení, Trvání i graf. Tím je vyřešené známé omezení ze sekce 6 („graf v editoru se nepřekreslí až do uložení“).
- **Kalendář hlídá pořadí plánů:** vypne dny před plánem nejbližšího dřívějšího a po plánu nejbližšího pozdějšího kroku s plánem; kroky bez plánu přeskakuje, stejný den je povolený. Je to pravidlo, které server kontroloval při uložení i dřív; kalendář ho měl hlídat od Fáze 5, ale kvůli chybě ve skriptu nikdy nehlídal.

**Ověřeno lokálně:**
- Automatické testy: Unit 2314, Api 536, JS 78 – vše zelené; Integration 203/204 (jediné selhání je známé `ProposalRejectAndTakeOver…`); E2E 125 testů, 32 selhání = stejných 32 jmen jako `main` (nová žádná), 2 nové prošly. Server a editor počítají stav ze stejné sady 19 případů (`tests/js/schedule/__fixtures__/status-cases.json`), včetně přechodu na zimní čas.
- **V prohlížeči za všechny role** (13 dočasných osob, po průchodu smazané; záznam #901-1 projektu PMT s dočasnou skutečností kroků 1/3/4 z automatu, bez plánu, termín 05.11.2026; dev DB po úklidu shodná se zálohou):

| Role | Stránka záznamu a záložka Harmonogram | Editor | Návrh změny harmonogramu |
|---|---|---|---|
| Superadmin, Administrátor aplikace, Vlastník, Admin projektu, PM, PM + vedoucí | „… · plán není zadán · celkový termín: zbývá 12 dní“, Plán „není zadán“, Rozpad „bez plánu“, Stíháme | stejný stav, „není zadán“, Trvání „–“ | – |
| Vedoucí, Zástupce vedoucího | totéž | odepřen | stejný stav; po výběru plánu kroku 5 hned „v předstihu 6 dní“ |
| Gestor, Metodik, Host, Read-all | totéž | odepřen | – |
| Bez role v projektu | 404 | – | – |

Navíc prošlo:
- PM v editoru: plán kroku 5 (30.10.) → hned „v předstihu 6 dní“, Plán dokončení 30.10.2026, Trvání 114 dnů; plán kroku 6 → meze kalendáře 4/5/6 správně; ✕ u kroku 5 → zpět „plán není zadán · celkový termín: zbývá 12 dní“; termín v Základních údajích na 20.10. → hned „celkový termín: skluz +4 dny“. Nic se neukládalo.
- Vedoucí v návrhu ✕ u plánu aktuálního kroku → odeslat → PM v detailu návrhu vidí „plán není zadán · celkový termín: zbývá 12 dní“.
- Záznam 300 (plán i skutečnost): stav k plánu, Rozpad „+4 dny, +9 dnů, 0 dnů, +19 dnů, -21 dnů, -17 dnů“. Plán kroku 3 přehozený před krok 2 (jen v editoru, bez uložení) → krok 2 nemá v kalendáři žádný povolený den, bez chyby.
- Světlý i tmavý motiv, 1400 i 375 px: editor bez vodorovného posuvníku, „bez plánu“ šedě. V konzoli prohlížeče žádné chyby.
- Odepřený editor vrací v lokálním dev běhu 500 („No DefaultForbidScheme“). V produkci s Windows Auth je to 403, dev artefakt.

**Otestovat (PM nebo admin projektu, ne superadmin; vedoucí subsystému; host):**
- [ ] Záznam **bez plánu s napojenou externí vazbou** (skutečnost doplnil automat): stav „Aktuální krok: … · plán není zadán · celkový termín: zbývá N dní“ (nebo „skluz +N dní“ po termínu), Plán „není zadán“, štítek podle termínu. Na kartě (přepínač Harmonogram), na stránce záznamu i v záložce Harmonogram.
- [ ] Rozpad u takového záznamu: kroky se skutečností mají šedé „bez plánu“.
- [ ] Záznam s plánem: stav jako dřív, jen se skloňuje („skluz +1 den“, „v předstihu 3 dny“).
- [ ] Editor → Harmonogram: ✕ u plánu aktuálního kroku → stav se **hned** přepne na „plán není zadán · celkový termín…“; vybrat datum → hned zpět na skluz k plánu. Graf se překreslí hned.
- [ ] Editor: změnit termín v Základních údajích → stav k celkovému termínu se přepočítá.
- [ ] Kalendář plánu: dny před plánem předchozího kroku s plánem jsou vypnuté (šedé, „Mimo povolené pořadí“), kroky bez plánu se přeskakují.
- [ ] Vedoucí: návrh změny harmonogramu → ✕ u plánu aktuálního kroku → PM v detailu návrhu vidí „plán není zadán · celkový termín…“.
- [ ] Nový záznam (Úkol) v editoru: stav „Aktuální krok: 1. … · plán není zadán · celkový termín: zbývá N dní“.
- [ ] i15 / Edge: světlý i tmavý motiv.

**Známá omezení (vědomě):**
- ~~**Dashboard** počítá problémové kroky postaru~~ — opraveno 2026-11-10, viz sekce 8.
- Detail návrhu ze serveru vykreslí souhrn současného záznamu; prohlížeč ho hned po načtení přepočítá z navržených hodnot (tak to bylo i dřív).
- Záložka Harmonogram u záznamu se skutečností bez plánu dál ukazuje poznámku „Záznam zatím nemá zadány kroky harmonogramu“ (stávající poznámka, podmínka „trvání 0“). Rozhodnutí 2026-11-10: zatím nechat.
- Editor na 375 px: přepínač „Automatické vyplňování harmonogramu“ překrývá záložky (od 2026-05-03). Rozhodnutí 2026-11-10: neřešit, mobilní šířka není pro editor relevantní.

## 8. Dashboard — kroky bez plánu; úklid editoru harmonogramu

**Hotovo 2026-11-10:** merge `656f634`. Bez samostatné spec (odložené drobnosti ze sekce 7, rozhodnutí 2026-11-10); pravidla dopsaná do `docs/specs/harmonogram-plan-vs-skutecnost.md` (kapitola Dashboard projektu).

**Co to dělá:**
- **Dashboard projektu → záložka Záznamy** (problémové záznamy) hodnotí jen kroky s **vlastním plánem**. Dřív se u kroku bez plánu dosazoval konec předchozího plánu nebo datum založení, takže:
  - záznam bez plánu měl všech 10 kroků „Čeká (125 dní)“ s „plánem“ = datum založení (v dev DB záznamy 901-1, 901-2, 901-17);
  - krok se skutečností z automatu a bez plánu vycházel jako „V prodlení“ (např. +81 dní);
  - kroky bez plánu za krokem s plánem se hlásily jako „Blíží se termín“ k datu toho plánu.
- **Krok splněný přesně v den plánu** už nehlásí „Čeká na skutečnost“ (dřív ano: skutečnost = plán se nedala odlišit od nevyplněné).
- **Sloupec „Skutečný termín“** v rozbaleném detailu ukazuje jen vyplněnou skutečnost. Dřív měl krok „Blíží se termín“ ve sloupci datum plánu.
- **Pořadí v Přehledu:** nejbližší plánovaný milník se bere jen ze skutečných plánů (záznam bez plánu měl v den založení milník „dnes“).
- **Editor harmonogramu — úklid:** z `block.js`, `form.js` a `select-candidate.js` odstraněno 449 řádků kódu z offsetového modelu (pole trvání a odchylky ve dnech, tlačítka +/−, Rozpad v editoru, řádek „Skutečné dokončení“), které nic nedělaly. Chování editoru se nemění.
- Nový záznam v editoru ukazuje „plán není zadán · celkový termín: zbývá N dní“ — potvrzeno jako záměr, zapsáno ve spec harmonogramu.

**Ověřeno lokálně:**
- Automatické testy: Unit 2329, Api 536, JS 78 – vše zelené. E2E 125 testů: na větvi i na `main` (`085f877`) stejných 32 selhání podle jmen, nová žádná.
- **V prohlížeči za všechny role** (13 dočasných osob, po průchodu smazané; dev DB po úklidu shodná se zálohou — počty řádků všech tabulek, kroky harmonogramu, zámky, matice priorit):

| Role | Dashboard → Záznamy | Editor záznamu 300 | Návrh změny harmonogramu |
|---|---|---|---|
| Superadmin, Administrátor aplikace, Vlastník, Admin projektu, PM, PM + vedoucí | ano, stejný výsledek | ✕ u plánu kroku 8 → hned „plán není zadán · celkový termín: zbývá 51 dní“, krok 8 trvání „–“, krok 9 „61 dnů“, meze kroku 9 od plánu kroku 7 | PM + vedoucí: stejně |
| Vedoucí, Zástupce vedoucího | ano | odepřen | stejný přepočet jako v editoru |
| Gestor, Metodik, Host, Read-all | ano | odepřen | odepřen (Read-all nezkoušen) |
| Bez role v projektu | odepřen | odepřen | – |

Navíc:
- Srovnání `main` × větev (PM): dashboard na `main` ukazuje 901-1, 901-2, 901-17 s „Čeká (125 dní)“ u všech 10 kroků, na větvi „Žádné záznamy k zobrazení“.
- Dočasně (pak vráceno) záznam 901-1: plán kroku 1 = 1. 10. bez skutečnosti, krok 2 plán i skutečnost 15. 10., plán kroku 3 = 14. 11. Větev: „Čeká na skutečnost +40 dní“, v detailu krok 1 „Čeká (40 dní)“ a krok 3 bez skutečného termínu; krok 2 ani kroky 4–10 se nehlásí. `main`: navíc krok 2 „Čeká (26 dní)“ a kroky 3–10 se „skutečným termínem“ 14.11.2026.
- Editor PM na `main` i na větvi: shodné souhrny, meze kalendáře i přepnutí kategorie Úkol → Informace → Úkol. V konzoli žádné chyby.
- Odepřené stránky vrací v lokálním dev běhu 500 („No DefaultForbidScheme“), v produkci s Windows Auth 403.

**Otestovat (PM nebo admin projektu, ne superadmin):**
- [ ] Dashboard projektu → Záznamy: záznam bez plánu (i se skutečností z automatu) se neukazuje.
- [ ] Záznam s propadlým plánem kroku bez skutečnosti: „Čeká na skutečnost“ se správným počtem dní; v detailu bez skutečného termínu.
- [ ] Krok splněný v den plánu se v detailu neobjeví.
- [ ] Krok s plánem v příštích 7 dnech: „Blíží se termín“, sloupec Skutečný termín prázdný.
- [ ] Editor → Harmonogram: výběr data, ✕ u plánu, změna termínu — souhrn a graf se přepočítají jako dřív; přepnutí kategorie a zpět bez chyby.
- [ ] Vedoucí subsystému: návrh změny harmonogramu — přepočet jako dřív.

**Známá omezení (vědomě):**
- Záznam **bez plánu** se mezi problémovými neobjeví, ani když mu propadl celkový termín: panel hlídá plán kroků. Stav nad harmonogramem ho přitom ukazuje jako „celkový termín: skluz +N dní“ / Nestíháme. Pokud ho tam chceš, je to nová kategorie.
- Krok bez skutečnosti **před** posledním splněným krokem (mezera) se dál hlásí jako „Čeká na skutečnost“, i když ho stav harmonogramu přeskakuje. Beze změny.
- Texty dashboardu se neskloňují („+1 dní“, „Čeká (1 dní)“). Vzhled dashboardu se nemění do jeho přepracování.


---

## 9. Testy — převzetí návrhu, zastaralé E2E testy editoru a harmonogramu

**Hotovo 2026-11-11:** merge `d91cde9`. Mění se jen testy a dokumentace, aplikace ne.

**Co se změnilo:**
- **Převzetí návrhu záznamu (rozhodnutí 2026-11-11, varianta B):** převzít návrh do formuláře smí jen ten, kdo smí návrh schválit (`proposals.accept` + `proposals.takeover`). Kód to tak dělal, padal jen Integration test, který dal manažerovi jen `proposals.takeover`. Test dostal i `proposals.accept`; nový test hlídá, že bez práva schválit se převzetí odmítne a návrh dál čeká.
- **E2E harmonogram (`HarmonogramUnifiedScenariosTests`):** 7 testů padalo, protože otevíraly editor v modálu, který od 2026-05-01 neexistuje (editor je stránka). Šest převedeno na stránku editoru a vlastní projekt s úkolem s plánem (dřív při prázdném projektu mlčky procházely); hlídají totéž: osu s popisky a značkami DNES / TERMÍN, rozpad, mini-Gantt Plán / Skutečnost, přepínání záložek, počet dnů vpravo od kalendáře, založení úkolu s plánem.
- **Smazané testy zrušeného UI:**
  - `Harmonogram_ShouldRenderOnlySubsystemFilter…` — harmonogram má od sjednocení filtrů sdílený filtr projektu (opak testu);
  - `RecordEditModalCloseScenariosTests` (5) a `RecordEditModalSilentRegressionTests` (1) — modál editoru; 3 testy procházely naprázdno. Totéž chování na stránce editoru hlídá `RecordEditorHistoryBackScenariosTests`.

**Ověřeno lokálně:** Integration převzetí 2/2 (nový test bez kontroly `proposals.accept` spadne — ověřeno dočasnou úpravou kódu). E2E celá sada: 118 testů, 22 selhání — všechna z dosavadních 32 známých jmen, nové žádné; z baseline zmizelo 10 (7 harmonogram, 3 modál).

**Otestovat ručně:** nic — aplikace se nemění.

**Známá omezení:** zbylá známá selhání E2E (filtry projektů, nastavení rolí, plovoucí panely, PhaseD, tlačítko „Nový záznam“ v dalších testech) se týkají jiných oblastí a tato změna je neřeší.

---

## 10. Vyjádření — kdo smí přidat, upravit a smazat

**Hotovo 2026-11-12:** merge `e755f268`. Spec `docs/superpowers/specs/2026-11-10-vyjadreni-uprava-podle-role-design.md`, pravidla v `docs/specs/record-comments.md`.

**Co se mění pro uživatele:**

| Role | Dřív | Nově |
|---|---|---|
| Gestor | vlastní v přípravě: tužka ano, uložení odmítnuto; v otevřeném jednání tužka chyběla | vlastní v přípravě i v otevřeném: tužka i koš, uložení projde |
| Metodik | přidával v přípravě i otevřeném, u všech subsystémů | přidá, upraví, smaže jen v přípravě a jen u svého subsystému |
| Vedoucí, Zástupce | vlastní v otevřeném: tužka chyběla, server by povolil | tužka chybí a server odmítne (pro uživatele beze změny) |
| Admini, Vlastník, Admin projektu, PM, Host, Read-all | — | beze změny |

- Ikony tužka / koš teď počítá server stejným pravidlem jako uložení — co se ukáže, projde; co se neukáže, server odmítne.
- Starší vyjádření metodika u cizího subsystému nebo v otevřeném jednání zůstávají; upravit je může PM / admin.
- Smazán nepoužívaný blok úkolu v detailu jednání (`_TaskItemPartial`), detail jednání tím ušetří jeden dotaz do DB. Vzhled se nemění.

**Ověřeno lokálně:**
- Automatické testy: Unit 2344, Api 545, Integration 204, JS 78 — vše zelené. E2E 118 testů: na větvi stejných 22 selhání podle jmen jako na `main` (`d91cde9`), nová žádná.
- Před změnou test potvrdil, že server Gestorovi smazání vlastního vyjádření v přípravě odmítal (dřívější poznámka v §5 byla chybná).
- **V prohlížeči za všech 13 rolí** (dočasné osoby, 26 vyjádření na záznamu 901-1 v přípravě / otevřeném / uzavřeném jednání a 1 u cizího subsystému 902-1; stránka záznamu i detail jednání v přípravě): ikony u každého vyjádření a nabídka jednání ve formuláři přesně podle tabulky výše — 681 kontrol, jediná odchylka je dev 500 u podvrhu hosta (níže).
  - Kliknutím: Gestor upravil vlastní v otevřeném a smazal vlastní v přípravě; Metodik přidal a upravil vlastní v přípravě; Vedoucí smazal vlastní v přípravě; Zástupce upravil vlastní v přípravě; PM upravil cizí (hosta).
  - Podvržené požadavky tam, kde ikony nejsou (Metodik v otevřeném a u cizího subsystému, Vedoucí a Zástupce v otevřeném, Gestor na cizím, PM v uzavřeném, Host): všechny odmítnuté, v DB beze změny. U hosta v dev běhu 500 „No DefaultForbidScheme“ (v produkci 403).
  - Bez chyb v konzoli. Dev DB po úklidu shodná se zálohou (počty řádků všech tabulek).

**Otestovat (asi 20 min; jednání v přípravě, otevřené a uzavřené):**
- [ ] Gestor: vlastní vyjádření v přípravě i v otevřeném jednání — tužka i koš, úprava a smazání projdou; u cizího vyjádření ikony nejsou.
- [ ] Metodik: na záznamu svého subsystému nabídka jednání jen „v přípravě“, přidání, úprava a smazání vlastního projdou; v otevřeném jednání a u záznamu jiného subsystému formulář ani ikony nejsou.
- [ ] Vedoucí / zástupce: vlastní v přípravě ikony ano, v otevřeném ne.
- [ ] PM nebo admin projektu: ikony u všech vyjádření kromě uzavřeného jednání.
- [ ] Host: žádné ikony, žádný formulář.
- [ ] Detail jednání → rozbalit záznam → vyjádření: totéž jako na stránce záznamu; po uložení se panel obnoví a předvybrané jednání zůstane.

**Známá omezení (vědomě):**
- Když se jednání přepne do jiného stavu v jiném okně, ikony se změní až po obnovení panelu (jako dřív).
- Redesign klíčů oprávnění (`docs/known-issues/authz-ui-serverside-mismatch.md`) je mimo rozsah.
