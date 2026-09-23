# Log rozhodnutí

Zodpovězené otázky a jejich důsledky pro zadání.

---

## Kolo 1 — 2026-09-07

### A — Architektura a technologie

| # | Otázka | Rozhodnutí | Důsledek pro zadání |
|---|---|---|---|
| A1 | Runtime backendu | **ASP.NET Core, .NET 10 (LTS)** — volba ponechána na zpracovateli | Kód ze Zápisky (.NET 8) se portuje bez přepisování; rozdíly nejsou pro tento typ aplikace rušivé. Podpora do listopadu 2028. Hosting bundle pro IIS existuje. |
| A2 | Vztah React ↔ backend | **React SPA nad REST API, jeden nasazovací artefakt** | Backend servíruje hotový build SPA ze statických souborů a zároveň poskytuje API. Jeden publish, jedno místo nasazení, funguje Windows Authentication. |
| A3 | Provozní prostředí | **IIS na Windows** (jako Zápiska). Databází je po A5′ Microsoft SQL Server; instance se určuje připojovacím řetězcem v nastavení. | Instalační dokumentace popisuje obojí. |
| A4 | Offline-first | **Platí bez výjimky** | Žádné CDN, žádné externí fonty, žádné volání ven za běhu. React se buildí na stroji s internetem, do produkce jde hotový build — nikdy `node_modules`. |
| A5 | Přístup k datům a migrace | ~~EF Core + Npgsql~~ → **nahrazeno A5′ v kole 7 (SQL Server)**; migrace **ručně psanými SQL skripty** platí | Stejný model jako Zápiska: baseline skript + očíslované patch skripty aplikované v pořadí + kontrolní skript stavu instance. |
| A6 | Přihlašování | **Windows Authentication + Active Directory**, jako Zápiska | Modul přihlášení a AD se přebírá v celém rozsahu. **Odchylka:** browsing musí fungovat i pro doménového uživatele, který v aplikaci nemá žádný záznam — viz otázka N1. |

#### Polemika ke kontejnerizaci celé aplikace

Zadavatel nadhodil, zda nestavět celou aplikaci jako kontejner.

**Doporučení: aplikaci nechat na IIS, kontejnerizovat jen SQL Server** (což je stav, ke kterému rozhodnutí A3 stejně vede).

Důvody:
- **Windows Authentication a AD jsou tvrdý požadavek (A6).** Právě autentizace vůči doméně je na kontejnerizované ASP.NET aplikaci ta nejtěžší část — řeší se skupinovými spravovanými účty a přidává provozní složitost, kterou dnes IIS řeší sám.
- **Provozáci umí IIS na Windows.** Zavedení Podmanu a Linuxu pro aplikaci znamená naučit tým druhou platformu kvůli jedné aplikaci.
- **SQL Server v kontejneru je jiný případ** — jedna stavová služba s diskovým svazkem, bez vazby na doménu. Tam se kontejner vyplatí.

**Pojistka do zadání:** aplikace nesmí obsahovat kód závislý na IIS mimo samotné navázání autentizace. Pak je pozdější přesun do kontejneru otázkou konfigurace, ne přepisu.

### D — Data a vazby

| # | Rozhodnutí | Důsledek |
|---|---|---|
| D1 | **Vlastní evidence osob a rolí**, do budoucna napojení na centrální systém řízení přístupů (kombinace uživatel × role × datové rozsahy) | Sestavování efektivních práv musí být za rozhraním se dvěma implementacemi: dnes z vlastní databáze, později z centrálního systému. Datový rozsah musí být od začátku prvotřídní pojem, ne dodatek. |
| D2 | **Čtení: bez omezení pro každého příchozího.** Zápis: řádově 5–20 lidí | Čtecí cesta se navrhuje na propustnost a mezipaměť, zápisová na správnost a auditovatelnost, ne na výkon. |

### F — Frontend

| # | Rozhodnutí |
|---|---|
| F1 | **Vite + TypeScript + React**, směrování React Router, serverová data přes dotazovací knihovnu s mezipamětí. Vše lokálně instalované kvůli A4. |
| F2 | **Jen čeština.** Žádná jazyková vrstva, texty přímo v komponentách. |
| F3 | **Living style guide v aplikaci**, jako Zápiska. Dokumentace se později převede do XWiki, až bude nasazená — aplikace se tehdy upraví. |

### T — Testování

| # | Rozhodnutí |
|---|---|
| T1 | **Vitest + React Testing Library** pro komponenty. |
| T2 | **Lokální instance SQL Server** pro integrační testy (rychlejší než kontejner na test). |
| T3 | **Playwright v .NET** — jedno řešení, jeden příkaz, testovací data zakládaná stejnou datovou vrstvou. |

### G — Forma zadání

| # | Rozhodnutí |
|---|---|
| G1 | Vznikne **nový git repozitář**. Zatím se pracuje zde; hotové zadání se přenese celé. |
| G2 | Kritérium velikosti bloku potvrzeno. Počet bloků vyjde, jak vyjde — 200 byl příklad. |
| G3 | **Struktura wiki předem, texty až v blocích.** |
| G4 | **Jediná větev, žádné mergování. Vývojář commituje sám.** |

### G5 — Povinná výbava přenesená ze Zápisky

| Schopnost | Přenést |
|---|---|
| Světlý / tmavý / automatický režim vzhledu | **ano** |
| Globální vyhledávání přes OpenSearch | **ano** |
| Export do PDF a tisk | **ano** |
| Obrazovka „Efektivní práva" | **ano** |
| Uživatelský profil | **ano** |
| Import ze souboru — **JSON s předem definovanou strukturou** | **ano** |
| Historie změn | **ano** — ve dvou podobách: verzování číselníku a auditní log zásahů |
| Export do WORDu | ne |
| Uživatelský dashboard po přihlášení | ne |

#### Verzování musí být datově úsporné

Zadavatel formuloval explicitně: číselník o 1000 hodnotách, u kterého se změní jedna hodnota,
**nesmí uložit 1000 hodnot znovu**.

Cílový model: **aktuální stav číselníku je uložený jako hodnoty; historické verze se
neukládají, ale dopočítávají** zpětnou aplikací zaznamenaných změn. Změny jsou vedené
po jednotlivých hodnotách a přiřazené k verzím číselníku. Uživateli se historická verze
vygeneruje jako celek, přestože v databázi leží jen rozdíly.

---

## Kolo 2 — 2026-09-07

### V — Verzování

| # | Rozhodnutí | Důsledek pro návrh |
|---|---|---|
| V1 | **Verzi vydává člověk.** Dvě tlačítka: *Uložit změny* (bez nové verze) a *Publikovat novou verzi* | Existuje trvale uložený **rozpracovaný stav**. Konzumenti vidí jen poslední publikovanou verzi. Má přímý dopad na datový model — viz níže. |
| V2 | **Změna struktury číselníku je nová verze** | Definice atributů a vazeb je součástí verzovaného obsahu, ne metadata mimo verzi |
| V3 | **Dvojice hlavní/vedlejší** | Hlavní se zvyšuje při změně struktury, vedlejší při změně hodnot. Konzument z čísla pozná, jestli se ho změna může dotknout. |

#### Dopad V1 na datový model

Rozhodnutí zavádí druhý stav číselníku a určuje, **která větev je rychlá**:

- **`polozka` a spol. drží publikovaný stav.** Čtení konzumenty i prohlížeči je prostý dotaz
  do tabulky. To je horká cesta — čte neomezený počet uživatelů a aplikací.
- **Rozpracované změny leží v témž záznamu změn s nepřiřazenou verzí.** Editor vidí
  publikovaný stav překrytý svými rozpracovanými změnami. To je studená cesta —
  editorů je 5–20.
- **Publikování** přiřadí rozpracovaným změnám nové číslo verze a promítne je do
  publikovaného stavu. Jedna operace, jeden mechanismus.

Opačné uspořádání (tabulka drží rozpracovaný stav, publikovaná verze se dopočítává)
by znamenalo dopočítávat na každý dotaz konzumenta. Nepřipadá v úvahu.

### Z — Zdroje a rozhraní

| # | Rozhodnutí | Důsledek |
|---|---|---|
| Z1 | **Autentizace rozhraní výhradně přes Active Directory.** Žádné přístupové klíče. **Kdo čte, se nesleduje. Sleduje se, kdo mění.** | Konzumující aplikace používají doménové servisní účty. Auditní log pokrývá zápisové operace, ne čtení. Odpadá správa klíčů. |
| Z2 | Vnořená dvojice kód + popis ze zdroje → **samostatný číselník a vazba na něj** | |
| Z3 | **SOAP nikdo nepotřebuje** | Rozhraní je jen REST |
| Z4 | **Otevřeno** — zadavatel žádá vysvětlení. Viz [otevrene-otazky.md](otevrene-otazky.md) | |
| Z5 | Další zdroje pravděpodobně přibudou, všechny na stejném principu: *přečti, zjisti změny, aktualizuj číselník*. **Každé napojení se ale programuje zvlášť — obecný předpis mapování se nedělá.** | **Oprava návrhu.** Předchozí verze počítala s mapováním jako s daty. Nedělá se. Konektor je kód. Sdílené zůstává vše za ním. |

#### Kde vede hranice mezi konektorem a sdílenou částí

```
konektor zdroje (kód, jeden na zdroj)          sdílené (jednou pro všechny)
─────────────────────────────────────    →     ────────────────────────────────
přečti zdroj                                   porovnej s publikovaným stavem
rozbal jeho tvar odpovědi                      sestav změny
přihlas se jeho způsobem                       vydej verzi
vrať položky v kanonickém tvaru                zapiš audit
```

Konektor implementuje jedno rozhraní a vrací položky v témž tvaru, jaký má importní
soubor JSON. Všechno za touto hranicí je společné a píše se jednou.

Zdůvodnění zadavatele: tvary odpovědí zdrojů se liší natolik, že obecný předpis mapování
by byl větší investice než napsat konektor. Souhlas — obecný mapovací jazyk je vlastní
malý produkt a živil by se jen jednou za čas.

### N — Uživatelské prostředí

| # | Rozhodnutí |
|---|---|
| N1 | Uživatel bez role **vidí vše** — hodnoty, verze i historii. Nesmí jen nic měnit. |
| N3 | **Obrazovka rozdílu mezi verzemi bude.** |

### E — Editace

| # | Rozhodnutí | Důsledek |
|---|---|---|
| E1 | **Správce číselníků je samostatná role.** Oddělí toho, kdo číselníky aktualizuje, od toho, kdo je navrhuje a zakládá. | |
| E2 | **Hromadná editace v tabulce.** Před odesláním je k dispozici **seznam změn ke kontrole** — zobrazení není podmínkou uložení. Import JSON prochází **týmž tokem**: detekce změn → promítnutí do tabulky → seznam změn → uložení. | Ruční hromadná editace a import nejsou dvě funkce, ale **jeden tok se dvěma vstupy**. Viz níže. |
| E3 | **Číselník je buď ručně spravovaný, nebo externí. Hybrid neexistuje.** Chyba v externím číselníku se opravuje u zdroje. | Číselník nese příznak režimu správy. Ruční editace externího číselníku je zakázaná na úrovni modelu, ne jen skrytím tlačítka. |

#### E2 — jeden tok, dva vstupy

```
vstup                          společný tok
──────────────────────    →    ──────────────────────────────────────────
ruční editace v tabulce        detekce změn proti publikovanému stavu
nahrání souboru JSON      →    promítnutí změn do tabulky
                               volitelný seznam změn ke kontrole
                               uložení (rozpracované) / publikování (nová verze)
```

Import není samostatná obrazovka s vlastní logikou — je to **druhý způsob, jak naplnit
tutéž tabulku**. Uživatel po nahrání souboru vidí své číselníky v tabulce se zvýrazněnými
změnami a může je ještě ručně upravit, než uloží.

Tím odpadá celá jedna paralelní cesta v kódu i v uživatelském prostředí.

---

## Kolo 3 — 2026-09-07

| # | Rozhodnutí | Důsledek |
|---|---|---|
| Z4 | **Varianta A — identifikátor je adresa aplikace. Doména FIS.** | Přesný název stroje se musí ustálit **před prvním publikováním verze**. Od té chvíle je neměnný — je součástí identity dat, ne konfigurace. Zapsat jako podmínku nasazení. |
| V4 | **Výhradní zámek na číselník.** V jeden okamžik ho upravuje nejvýše jeden člověk. Token platný 5 hodin od poslední skutečné aktivity. Druhý editor editaci neotevře a dozví se, kdo ji drží. Souběžná živá spolupráce se **zamítá**. | Vlastní návrh, viz [10-specifikace/08-zamek-editace.md](../10-specifikace/08-zamek-editace.md) |
| V5 | **Publikuje se všechno rozpracované.** Editace i publikování se vždy týkají **právě jednoho číselníku**. | Zapadá do zámku — jednotka zamčení, editace i publikování je tatáž věc |
| V6 | **Běh externího zdroje publikuje verzi rovnou.** | |
| E4 | **Prahová pojistka ano** — nadlimitní úbytek položek běh zastaví a verze se nevydá. | |

### Etapizace — napojení ERP až později

Zadavatel rozhodl: **číselníky z velkého ERP se v aplikaci na začátku nebudou.**
ERP se bude napojovat postupně, jeden číselník po druhém.

To mění rozsah první etapy. Viz [10-specifikace/09-etapy.md](../10-specifikace/09-etapy.md).

**Užitečné zjištění:** import JSON v první etapě postaví přesně ten porovnávací a verzovací
stroj, který pak konektory jen využijí. Etapa 2 tedy nepřidává mechaniku, jen zdroj dat.

---

## Kolo 4 — posouzení externího doporučení (Gemini), 2026-09-07

| Podnět | Verdikt |
|---|---|
| **SKOS jako standardní slovník číselníků** | **Přijato.** Slovník výstupu rozhraní při `application/ld+json`. Cena je jeden `@context`, přínos je rozpoznatelný standard. |
| `skos:broader` mezi položkami dvou různých číselníků | **Odmítnuto — je to chyba.** `broader` je pro hierarchii uvnitř schématu. Doménová vazba dostává vlastní predikát. Zapsáno jako závazné pravidlo. |
| Tabulky `concept_schemes` / `concepts` / `relations` jako model úložiště | **Odmítnuto jako úplný model.** Nenese atributy ani verzování — tedy vlastní obsah a většinu naší složitosti. Část o oddělené tabulce vztahů se ale **shoduje** s naší `polozka_vazba`. |
| Grafová databáze | **Odmítnuto.** Doporučení samo dochází k témuž. |
| R2RML / Ontop, koncový bod SPARQL | **Odmítnuto.** Další provozovaná služba v uzavřené síti kvůli jazyku, který žádný konzument nechce. Dá se doplnit kdykoli později. |
| Vnitřní model jako trojice | **Odmítnuto** — naše varianta A, zamítnutá už dřív. |

Rozbor: [20-architektura/06-propojena-data-skos.md](../20-architektura/06-propojena-data-skos.md).

---

## Kontrolní průchod před plánem implementace — 2026-09-07

Mechanická kontrola (mrtvé odkazy, stavové značky, zastaralé odkazy na otázky, mrtvé klíče,
terminologie) plus čtení všech dokumentů proti sobě.

### Opraveno rovnou — 12 nálezů

| # | Nález | Oprava |
|---|---|---|
| 1 | **10 souborů mělo zastaralou hlavičku** „čeká na rozhodnutí otázek A1–A6 / T1–T3 / G1–G4 / F1–F3" — všechny zodpovězené v kole 1 | Hlavičky přepsány na skutečný stav |
| 2 | **5 odkazů na „otevřenou otázku"**, která je zodpovězená (N1, Z1, Z2, Z3, Z4) | Nahrazeno rozhodnutím včetně důvodu |
| 3 | **`otevrene-otazky.md` tvrdil opak reality** — vedl kolo 3 jako otevřené, ačkoli je zodpovězené | Přepsán na tři skutečně otevřené položky |
| 4 | **Mrtvý klíč `zdroje.mapping.edit`** — mapování se po Z5 nekonfiguruje, konektor je kód. Klíč neměl co chránit. | Smazán |
| 5 | **Chybný klíč `verze.compare`** — porovnání verzí je čtení, a čtení podle N1 klíč nemá | Smazán |
| 6 | **Klíče zdrojů neoznačené etapou** | Doplněno *(etapa 2)* |
| 7 | **Nekonzistentní cesta k verzi v rozhraní** — `…/verze/{n}/zmeny`, ale verze je dvojice | `…/verze/{verze}/zmeny`, příklad `…/verze/3.14/zmeny` |
| 8–10 | **Terminologie „aktuální stav"** na 3 místech, kde po V1 existují stavy dva a slovo je dvojznačné | „publikovaný stav" |
| 11 | **Cíle 3 a 4 a schopnosti S3, S4 nebyly označené jako etapa 2** — čtenář musel nabýt dojmu, že jsou v první dodávce | Doplněny odkazy na etapizaci |
| 12 | **Zastaralá tabulka stavů** v přehledu architektury a v hlavním rozcestníku | Srovnáno se skutečností |

### Nálezy, které opravit nejdu — potřebují rozhodnutí

| # | Nález |
|---|---|
| R1 | **Varianta C datového modelu nebyla výslovně odsouhlasena.** Stojí na ní všechno další. |
| N2 | **Otázka přístupnosti se ztratila** mezi koly 2 a 3. Nikdo na ni neodpověděl, odkaz na ni v nefunkčních požadavcích zůstal. |

### Ověřeno

- **70 vnitřních odkazů, 0 mrtvých.**
- Žádná zbylá zastaralá hlavička ani odkaz na zodpovězenou otázku.
- Jediný zbývající odkaz na otevřenou otázku (N2) je správně — otázka skutečně otevřená je.

---

## Kolo 5 — 2026-09-07

| # | Rozhodnutí |
|---|---|
| **R1** | **Datový model — varianta C (kombinovaná) schválena.** Tři vrstvy: společné typované sloupce, atributy řízené definicí, vazby jako řádky s cizím klíčem. Verzování rozdílově, historická verze se dopočítá z publikovaného stavu. |
| **N2** | **Přístupnost — úroveň 1, nic navíc.** Cílová skupina uživatelů nemá omezení, která by to vyžadovala. Bez auditu, bez prohlášení o přístupnosti, bez zvláštních akceptačních kritérií v blocích. Komponenty gov design systemu si nesou to, co dodává jejich autor; vlastní části aplikace se v tomto ohledu neřeší. |

> Poznámka pro budoucího čtenáře: rozhodnutí N2 je vratné, ale dodatečné doplnění by u tabulky
> hromadné editace znamenalo její přepis, ne doplnění. Zapsáno, aby bylo dohledatelné proč.

---

## Kolo 6 — 2026-09-07

| # | Rozhodnutí |
|---|---|
| **O1** | **První správce aplikace vzniká řádkem v databázi, ne instalátorem.** Tabulka `superadmini` s doménovým loginem se plní ručním SQL skriptem `db/db_seed_prvni_superadmin.sql` při zakládání databáze — přesně jako to má Zápiska (`authz.superadmins`). |

**Oprava předchozího návrhu.** Mluvil jsem o „instalačním skriptu", což je u této aplikace
nesmysl: nasazení je kopírování souborů do složky na IIS a žádný instalátor neexistuje.
Databáze se ale stejně zakládá ručním spuštěním SQL skriptů (A5), takže první superadmin
je jen další skript v téže posloupnosti — ne nový mechanismus.

**Superadmin není role.** Je to nouzový klíč, který zkratuje každou kontrolu oprávnění
a stojí mimo tabulky rolí. Kdyby na nich závisel, nefungoval by právě tehdy, když je
potřeba. Běžná správa se dělá rolí *Správce aplikace*, která má normální klíče.

---

## Nález při přípravě P3 — 2026-09-07

**Rozpor v datovém modelu.** Stálo tam, že identifikátory URI se odvozují od kódu, a zároveň
že „změna kódu nemění identitu věci". To je pravda jen uvnitř aplikace. Pro konzumenta,
který si identifikátor uložil, by změna kódu identifikátor rozbila — a to je přesně to,
co rozhodnutí Z4 zakazuje.

**Doplněno pravidlo:** *kód číselníku i kód položky jsou po prvním publikování verze
neměnné.* Do prvního publikování se mění volně, protože číselník tehdy pro konzumenty
neexistuje. Vynucuje se v kódu a hlídá testem (P3, blok 2).

---

## Nález při přípravě P5 — 2026-09-08

**Rozpor ve vnitřním kontraktu.** Stálo tam, že stránkované čtení hodnot vrací publikovaný
stav *překrytý rozpracovanými změnami*. To nejde skloubit se stránkováním: rozpracovaně
přidaná položka v tabulce publikovaného stavu neexistuje, takže by se musel dopočítávat
i počet a pořadí napříč dvěma zdroji.

**Upřesněno na dvě adresy:**

| Cesta | Co vrací | Teplota |
|---|---|---|
| `…/polozky` | publikovaný stav, stránkovaný, s příznakem `maRozpracovaneZmeny` | horká — čte každý |
| `…/polozky/rozpracovane` | publikovaný stav s překryvem, **bez stránkování** | studená — editor a výslovné zobrazení |

Editor si číselník stejně načítá celý do tabulky, takže mu nestránkovaná cesta nevadí.
Horká cesta zůstává prostým dotazem do tabulky.

---

## Nález při přípravě P6 — 2026-09-08

**Kdy vzniká verze při změně struktury.** Rozhodnutí V2 říká, že změna struktury je nová
verze. Dosud ale nebylo řečeno, jestli se struktura mění jako rozpracovaná změna,
nebo rovnou.

Kdyby se definice měnila hned a verze se zvýšila až příštím publikováním, viděli by
konzumenti mezitím **novou strukturu se starým číslem verze**. To je pro referenční
zdroj nepřijatelné — číslo verze je jediné, podle čeho konzument pozná, že se ho změna
může dotknout.

**Rozhodnuto: změna struktury vydává vlastní verzi okamžitě**, a je **zakázaná, dokud
má číselník nepublikované změny hodnot**.

| Důsledek | |
|---|---|
| Struktura nemá rozpracovaný stav | Obrazovka O7 nemá dvojici *Uložit* / *Publikovat*, jen *Uložit strukturu* |
| Změna struktury je vždy čistá verze | V balíku není nic než změny druhu `DEFINICE` |
| Konzument nikdy neuvidí nesoulad | Nová struktura přijde současně s novým hlavním číslem |
| Kdo má rozdělané hodnoty, dostane srozumitelnou hlášku | „Číselník má nepublikované změny hodnot. Nejdřív je publikujte." |

Uzavírá dluh D11.

---

## Nález při dotazu vývojáře — 2026-09-08

**Otázka:** Zápiska nemá React a React komunikuje přes rozhraní — jak se dá její kód použít?

**Změřeno nad repozitářem Zápisky:** ~70 % kódu je vůči prezentační vrstvě netečné.
Ve složce `Services/` sahá na MVC **jediný soubor z 278**. Rozbor a čísla:
[30-prevzate-moduly/07-jak-se-prebira-kod.md](../30-prevzate-moduly/07-jak-se-prebira-kod.md).

Šev je v controlleru doslova jeden řádek: `return View(model)` → `return Ok(model)`.
Autorizace se nemění vůbec — atribut se vyhodnotí dřív, než akce vrátí cokoli.

**Z měření vyplynul nález pro P9: sazba PDF.**

Zápiska ho dělá `Razor → HTML → Chromium → PDF`. Renderer HTML→PDF se přebírá beze změny;
nepřenosný je jen krok, který HTML vyrábí.

*Doporučení:* ponechat Razor **výhradně pro tiskové šablony** a vynutit architektonickým
testem, že `.cshtml` nesmí být mimo `Views/Tisk/`. Důvod je escapování HTML — ruční
skládání z uživatelských dat je bezpečnostní riziko na každém opomenutém místě.
Rozhodne se v P9.

**Doplněno dřív, do frontendové architektury a P1:** vývojová proxy ve Vite.
Bez ní běží frontend na jiném portu, což je cizí původ, a prohlížeč k němu vyjednávání
o Windows přihlášení sám neposílá — vypadalo by to jako rozbitá autorizace.

---

## Kolo 7 — 2026-09-08

| # | Rozhodnutí |
|---|---|
| **A5′** | **Databáze je Microsoft SQL Server, ne SQL Server.** Ruší se část rozhodnutí A5; zbytek (EF Core, ručně psané očíslované migrační skripty) platí beze změny. |

### Co to mění a co ne

**Nemění se nic věcného.** Datový model varianty C, verzování, zámek, oprávnění, veřejné
rozhraní ani obrazovky nejsou na typu databáze závislé. Mění se **dialekt a poskytovatel**.

**Tři místa, kde se to nedá přeložit slovo za slovem** — viz
[20-architektura/07-konvence-mssql.md](../20-architektura/07-konvence-mssql.md):

1. **Získání zámku.** SQL Server to uměl jedním `INSERT … ON CONFLICT DO UPDATE … WHERE`.
   SQL Server tuhle podmíněnou variantu nemá; atomicita se drží zámkem rozsahu v transakci.
2. **Částečný jedinečný index nad rozpracovanými změnami.** V SQL Server potřeboval
   `COALESCE`, protože tam se `NULL` nerovná `NULL`. **V SQL Serveru se rovná** —
   index je proto jednodušší a `COALESCE` mizí.
3. **Seed skripty.** Bloky `DO $$ … $$` a proměnné `\set` se přepisují do T-SQL.

### Vedlejší přínos: přebírání kódu ze Zápisky se zjednodušuje

Zápiska běží na SQL Serveru. Rozhodnutí tím **odstraňuje jedinou nefunkční vrstvu**
v mapě převzatých modulů: datová vrstva se dosud měla přepisovat na jiný dialekt,
teď se přebírá stejně jako zbytek. Odhad ~1 000 řádků, které se nemusí překládat.

### Umístění instance

Určuje se **připojovacím řetězcem v nastavení** (`appsettings.json`). Není to rozhodnutí
návrhu — aplikaci je jedno, kde instance stojí.

---

## Námitka vývojáře — 2026-09-08

**Námitka:** *„Strukturu Zápisky nejde použít, protože Zápiska je MVC a Číselníky budou
kvůli Reactu jinak."*

**Verdikt: námitka platí na tenkou vrstvu nahoře, neplatí na nic pod ní.**

MVC v ASP.NET Core je způsob zpracování požadavku, ne „server vykresluje HTML".
Rozhraní vracející JSON je součástí téhož zásobníku; `[ApiController]` i controller
vracející pohled dědí z téhož `ControllerBase`. **Číselníky budou také MVC — jen nepoužijí V.**

**Doloženo v repozitáři Zápisky:** 24 míst vrací JSON, v 9 controllerech, s vyhrazenou částí
`BaseController.Ajax.cs` (chybové kódy, chyby po polích) a testovací sadou
`AjaxControllersTests`. **Zápiska už obě podoby provozuje vedle sebe nad stejnými službami.**

Námitka má pravdu ve třech bodech — Razor pohledy, view modely tvarované pro vykreslení
a orchestrace pohledu v controlleru se nepřenášejí. To je právě těch 30 %, které měření
už dřív odlišilo.

Rozbor: [30-prevzate-moduly/07-jak-se-prebira-kod.md](../30-prevzate-moduly/07-jak-se-prebira-kod.md).

---

## Kontrolní průchod před předáním kolegům — 2026-09-08

### Opraveno — 8 nálezů

| # | Nález | Oprava |
|---|---|---|
| 1 | **Wireframe O5 slibuje sloupec `Nadřazená` a akci `+ Podřízená`, které P6 nestaví** | Doplněno do P6 včetně ochrany proti cyklu v nabídce a pěti testů |
| 2 | **Postupy slibují přidání řádku klávesou TAB**, P6 to nemá | Doplněno do P6 |
| 3 | **Obrazovka O13 (living style guide) není v žádném plánu** | Nový blok 7 v P9 včetně testu, že style guide nezestárne |
| 4 | Datový model se hlásil jako „čeká na odsouhlasení", ačkoli R1 padlo v kole 5 | Stav srovnán |
| 5 | Pět dokumentů `30-prevzate-moduly` tvrdilo „kód se vloží při psaní plánu" — plány jsou napsané | Stav srovnán |
| 6 | Dva governance dokumenty byly skutečně skelet | Dopsány (vývojový cyklus, testovací strategie) |
| 7 | Čtyři governance dokumenty se hlásily jako skelet, ač obsah měly | Hlavičky odstraněny |
| 8 | **Dva zbytky po Podmanu** — ten patřil k PostgreSQL, ne k SQL Serveru | Nahrazeno odkazem na nastavení |

### Ověřeno

| Kontrola | Výsledek |
|---|---|
| Mrtvé odkazy | **0 ze 161** |
| Číslování kroků v 69 blocích | **souvislé** |
| Pokrytí obrazovek O1–O13 plánem | **úplné** |
| Placeholdery | **žádné** |
| Rozpracované stavové značky | **žádné** |
| Zbytky po PostgreSQL | **žádné** |
| Terminologická konzistence | odchylky prověřeny, všechny legitimní |

### Uzavření otevřených otázek — 8. 9. 2026

Zadavatel stáhl z řešení všechny tři zbývající otázky:

| Otázka | Uzavření |
|---|---|
| Odkud se bere číselník osob | **Neřeší se.** Číselník osob je ručně spravovaný jako každý jiný. |
| Kde poběží databázová instance | Připojovací řetězec v `appsettings.json`. Není to otázka návrhu. |
| Zálohování a obnova | Nastaví si provoz podle vlastní praxe. Aplikace nic nevyžaduje. |

**Zadání je tím uzavřené.**
