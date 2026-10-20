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
