# 40 — Plán implementace

> **Pro vývojáře:** POVINNÝ POSTUP — implementuj **inline v hlavní session**, plán po plánu,
> blok po bloku. Kroky používají zaškrtávací syntaxi `- [ ]`.
>
> **Subagenti se na programování nepoužívají.** Smí být puštěni nanejvýš na analýzu,
> detekci chyb nebo review — nikdy na psaní kódu.

**Specifikace:** [../10-specifikace/](../10-specifikace/) — plán z ní argumentuje, čte se spolu s ním.
**Architektura:** [../20-architektura/](../20-architektura/)

> **Každý blok, který staví obrazovku, se řídí wireframem.**
> Rozvržení: [../10-specifikace/11-wireframy.md](../10-specifikace/11-wireframy.md) ·
> Průchod a texty tlačítek: [../10-specifikace/12-prochazeni.md](../10-specifikace/12-prochazeni.md).
> Názvy akcí jsou závazné — slovník ve `12-prochazeni.md` platí i pro potvrzovací hlášky.

---

## Rozdělení na plány

Etapa 1 je rozdělená na **devět plánů**. Každý plán končí stavem, který jde spustit
a ručně proklikat — ne polotovarem. Uvnitř plánu jsou bloky, uvnitř bloku kroky.

| # | Plán | Co bude fungovat po jeho dokončení |
|---|---|---|
| **P1** | [Základ a identita](P1-zaklad-a-identita.md) | Aplikace běží, přihlášený doménový uživatel se dostane na prázdný rozcestník v gov designu |
| **P2** | [Oprávnění](P2-opravneni.md) | Seed-only RBAC s datovým rozsahem, obrazovka rolí a efektivních práv |
| **P3** | [Číselníky a struktura](P3-ciselniky-a-struktura.md) | Správce založí číselník a nadefinuje jeho atributy a vazby |
| **P4** | [Hodnoty, hierarchie, vazby](P4-hodnoty.md) | Číselník má hodnoty, strom, vazby na jiné číselníky; detail se dá prohlížet |
| **P5** | [Verzování](P5-verzovani.md) | Rozpracované změny, publikování verze, dopočet historické verze, rozdíl verzí |
| **P6** | [Zámek a hromadná editace](P6-zamek-a-editace.md) | Editace v tabulce s výhradním zámkem a seznamem změn |
| **P7** | [Import JSON](P7-import-json.md) | Nahrání souboru → ověření → promítnutí do editační tabulky |
| **P8** | [Veřejné rozhraní](P8-verejne-rozhrani.md) | `/api/v1`, JSON Schema, JSON-LD nad SKOS, mezipaměť přes ETag |
| **P9** | [Průřezové](P9-prurezove.md) | Auditní log, vyhledávání přes OpenSearch, PDF a tisk, profil, wiki, verze aplikace |

### Proč tohle pořadí

- **P2 před P3.** Datový rozsah je konstrukční prvek, ne dodatek. Kdyby se oprávnění
  dodělávala až nad hotovými obrazovkami, znamenalo by to zpětně měnit každou cestu.
- **P5 před P6.** Editace ukládá **rozpracované změny**. Bez hotového verzování by neměla
  kam zapisovat a musela by se přepsat.
- **P7 po P6.** Import je druhý vstup do téže editační tabulky (rozhodnutí E2). Nemá smysl
  před tím, než tabulka existuje.
- **P8 po P5.** Veřejné rozhraní vydává **publikované** verze. Bez verzování nemá co vydávat.

Etapa 2 (konektory k ERP) je samostatná sada plánů, která se etapy 1 nedotýká.
Viz [../10-specifikace/09-etapy.md](../10-specifikace/09-etapy.md).

---

## Global Constraints

Platí pro **každý blok každého plánu**. Neopakují se v jednotlivých úlohách.

### Prostředí a stack

- **.NET 10 (LTS)**, ASP.NET Core. Hosting **IIS in-process** na Windows.
- **Microsoft SQL Server**. Přístup k datům **EF Core**, poskytovatel `Microsoft.EntityFrameworkCore.SqlServer`.
  Dialekt a pasti: [../20-architektura/07-konvence-mssql.md](../20-architektura/07-konvence-mssql.md).
- **Textové sloupce vždy `nvarchar`, textové literály vždy s předponou `N`.** Aplikace je česky.
- Migrace schématu **ručně psanými očíslovanými SQL skripty** — nikdy generovanými.
- **Názvy tabulek a sloupců jen malá písmena bez diakritiky, oddělená podtržítkem.**
  Zápisková zátěž s diakritikou v názvech se nedědí.
  Vzor pojmenování: `db_upgrade_<verze>_<popis>.sql`. Ke každé sadě patří kontrolní skript
  stavu instance.
- Frontend **React SPA** — Vite + TypeScript, React Router, dotazovací knihovna s mezipamětí.
- Nasazovací jednotka je **jeden artefakt**: backend servíruje rozhraní i hotový build SPA.
- Přihlášení **Windows Authentication**, identity z Active Directory.

### Offline-first — bez výjimek

- Žádné CDN, žádné externí fonty, žádné volání ven za běhu.
- Všechny knihovny leží v repozitáři a servírují se z aplikace.
- `npm ci` (nikdy `npm install`) na stroji s internetem; **do produkce jde jen výstup
  sestavení, nikdy `node_modules`**.
- gov design system: `@gov-design-system-ce/components` **4.2.9**,
  `@gov-design-system-ce/styles` **4.2.7**. Ikony jsou **Bootstrap Icons 1.11.3** stažené
  samostatně — gov je nedodává.

### Kvalita

- Build končí s **0 chybami a 0 varováními**.
- **Po každém bloku spusť plnou sadu jednotkových testů a nahlas výsledek.**
  Blok není hotový, dokud sada neběží.
- Nové chování má **deterministický regresní test, který před opravou selhal**.
- Dotčená dokumentace se aktualizuje **ve stejném bloku**, ne na konci.
- Hlídač velikosti souborů běží jako test.

### Git

- **Jediná větev. Žádné feature větve, žádné PR, žádný merge ani přerovnání historie.**
- **Vývojář commituje sám** po dokončení bloku.
- Prefixy zpráv: `feat(oblast):`, `fix(oblast):`, `refactor(oblast):`, `chore(oblast):`.

### Doménová pravidla, která nesmí být porušena

- **Nic se nemaže.** Všude, kde by se čekalo mazání, je ukončení platnosti.
- **Autorizace se vyhodnocuje vždy na serveru.** Skrytí prvku v prohlížeči není kontrola.
- **Klíč vázaný na číselník potřebuje identifikátor číselníku v cestě nebo dotazu.**
- **Čtení nemá klíč oprávnění** — vidí každý, i doménový uživatel bez záznamu v aplikaci.
- **Číselník je buď ručně spravovaný, nebo externí. Hybrid neexistuje.**
- **Bázová adresa stálých identifikátorů je od prvního publikování neměnná.**
- Jazyk rozhraní **jen čeština**, texty přímo v komponentách. Žádná překladová vrstva.
- **Přístupnost: úroveň 1**, nic navíc (rozhodnutí N2).

### Pasti gov komponent — ověřené v Zápisce, neopakovat

- `instanceof HTMLButtonElement` **nematchuje** `gov-button`.
- Nastavení `textContent` na hostu `gov-button` rozbije přemístění slotu a **zdvojí popisek**.
- CSS `:checked` **nematchuje** custom elementy — pro `gov-form-switch` se píše `[checked]`.
- Atributy `aria-*` a `hidden` na hostu wrapperu se neuplatní; stav se řídí třídou na
  obyčejném předkovi.
- `gov-dialog` s `block-close` má **disablovaný křížek** — atribut nikdy nepoužívat.
- **Modály se zavírají jen křížkem.** Klik na pozadí zavírat nesmí — tažení myší z pole ven
  jinak zavře rozdělanou práci.
- Zavírání plovoucích vrstev se vyhodnocuje na `mousedown`, ne na `click`.
- V Playwrightu je host gov komponenty „neviditelný": klikat dispatchem události,
  čekat na třídu `hydrated`, viditelnost ověřovat počtem prvků.
- **Statické soubory se servírují s výslovným `charset=utf-8`.** Bez toho se na cílových
  stanicích rozsype čeština.
- Nosné rozvržení stojí na letitých základech; novější vlastnosti stylů jen jako
  nepovinné vylepšení. Cílový prohlížeč je Edge na kancelářských sestavách.

---

## Přenášené dluhy

Zapisuje se průběžně při exekuci, aby se nic neztratilo při zkrácení kontextu.
Každá položka má přiřazený plán, který ji uzavře.

| # | Dluh | Vznikl v | Uzavře | Stav |
|---|---|---|---|---|
| D1 | Tabulka `ciselniky` má jen kód, název a příznak aktivity. | P2 | P3 | ✅ uzavřeno v P3 bloku 1 |
| D2 | Zkušební koncový bod `internal/ciselniky/{kod}/zkouska` je dočasný. | P2 | P3 | ✅ uzavřeno v P3 bloku 5 |
| D3 | Klíče pro zdroje (`zdroje.configure`, `zdroje.run`) v katalogu nejsou. | P2 | **etapa 2** | otevřeno |
| D4 | `CiselnikSluzba.BylPublikovanAsync` vrací natvrdo `false`. | P3 | P5 | ✅ uzavřeno v P5 bloku 4 |
| D5 | Odebrání atributu maže bez okolků, protože hodnoty neexistují. | P3 | P4 | ✅ uzavřeno v P4 bloku 5 |
| D6 | Bázová adresa identifikátorů natvrdo. | P3 | P8 | ✅ uzavřeno v P8 bloku 1 |
| D7 | Zápisová cesta pro hodnoty v P4 není — jde jen o čtení. | P4 | P5 | ✅ uzavřeno v P5 bloku 2 |
| D8 | `HierarchieKontrola.OverBezCykluAsync` je napsaná, ale nikdo ji nevolá. | P4 | P5 | ✅ uzavřeno v P5 bloku 2 |
| D9 | Strop stránky natvrdo v controlleru. | P4 | P8 | ✅ uzavřeno v P8 bloku 1 |
| D10 | Publikování zámek neuvolňuje. | P5 | P6 | ✅ uzavřeno v P6 bloku 3 |
| D11 | Změny druhu `DEFINICE` nikdo nezapisuje. | P5 | P6 | ✅ uzavřeno v P6 bloku 4 |
| D14 | Silové odebrání zámku se nikam nezapisuje. | P6 | P9 | ✅ uzavřeno v P9 bloku 1 |
| D20 | Cesta k prohlížeči pro sazbu PDF se při startu neověřuje — chybná se projeví až prvním tiskem. | P9 | *otevřeno* | otevřeno |
| D21 | Editační mřížka vykresluje všechny řádky najednou. Podle měření případně doplnit vykreslování jen viditelného okna. | P6 | *podle měření* | otevřeno |
| D15 | Délka platnosti zámku je konstanta v kódu. | P6 | P8 | ✅ uzavřeno v P8 bloku 1 |
| D16 | Buňka vazby načítá všechny položky cílového číselníku. | P6 | P7 | ✅ uzavřeno v P7 bloku 7 |
| D17 | Strop velikosti souboru a mez našeptávače natvrdo. | P7 | P8 | ✅ uzavřeno v P8 bloku 1 |
| D19 | Rozbalení vazby dělá dotaz na cílový číselník pro každou rozbalovanou vazbu. Při více vazbách zvážit načtení najednou. | P8 | *otevřeno* | otevřeno |
| D18 | Ověření velkého souboru běží celé v paměti. Dokud nevznikne soubor o statisících položek, neřešit. | P7 | *otevřeno* | otevřeno |
| D12 | Uložený otisk každých K verzí se nestaví. Až dopočet zpomalí, přidat — přírůstková změna. | P5 | *otevřeno* | otevřeno |
| D13 | Historická verze vydává hodnoty se soudobou definicí struktury, ne s tehdejší. Rozhodnout, zda je to přijatelné. | P5 | *otevřeno* | otevřeno |
