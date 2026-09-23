# Backend — vrstvení a členění řešení

## Členění řešení

```
Ciselniky.sln
├── Ciselniky.Api/            ASP.NET Core — hostuje rozhraní i hotový build SPA
│   ├── Controllers/
│   │   ├── Verejne/          /api/v1/…    pro konzumující aplikace  (KONTRAKT)
│   │   └── Vnitrni/          /internal/…  pro vlastní React frontend
│   ├── Middleware/           naplnění identity do requestu
│   ├── Filters/              ochrana proti CSRF u měnících akcí
│   └── wwwroot/              sem se kopíruje výstup sestavení SPA
├── Ciselniky.Core/           doména, služby, přístup k datům
│   ├── Domain/               entity a doménová pravidla, bez závislosti na EF
│   ├── Data/                 DbContext, konfigurace entit, dotazovací pomocníci
│   ├── Services/<oblast>/    aplikační služby
│   └── Security/             RBAC, snapshot oprávnění
├── ciselniky-web/            React SPA (Vite + TypeScript)
├── Ciselniky.Tests.Unit/
├── Ciselniky.Tests.Integration/
├── Ciselniky.Tests.Api/
└── Ciselniky.Tests.E2E/      Playwright v .NET
```

### Proč dva projekty, ne pět

`Core` je samostatná knihovna, aby šly služby testovat **bez spouštění webu** — to je jediný
důvod, pro který se projekt dělí, a ten je věcný.

Další dělení (samostatný projekt na doménu, na přístup k datům, na kontrakty) se **nedělá**.
Přineslo by mezivrstvy, které jen předávají volání dál. Zápiska má vše v jednom webovém
projektu a její obtíže nepramení z počtu projektů, ale z velikosti jednotlivých souborů.

**Kdy dělení přehodnotit:** až se objeví druhý konzument `Core` (například samostatná
služba pro běh konektorů). Do té doby ne.

---

## Vrstvení

```
Controller  →  Služba  →  Data
```

| Vrstva | Smí | Nesmí |
|---|---|---|
| **Controller** | Přijmout vstup, ověřit jeho tvar, zavolat službu, namapovat výstup, vrátit stavový kód | Obsahovat doménovou logiku, skládat dotazy, rozhodovat o oprávněních nad rámec deklarace |
| **Služba** | Doménová logika, orchestrace, transakce, kontrola oprávnění programově tam, kde deklarace nestačí | Znát HTTP, číst z requestu, vracet stavové kódy |
| **Data** | Dotazy, konfigurace entit, převody typů | Rozhodovat o doménových pravidlech |

Velké služby se dělí na `SluzbaX.Commands.cs` a `SluzbaX.Queries.cs` — dělící čára vede
mezi tím, co mění stav, a tím, co jen čte. Vzor ze Zápisky:
`Services/Dictionaries/DictionaryService.Commands.cs` a `.Queries.cs`.

---

## Oblasti služeb

| Oblast | Odpovědnost |
|---|---|
| `Ciselniky` | Číselníky, definice atributů a vazeb, položky |
| `Verze` | Rozpracované změny, publikování, dopočet historické verze, rozdíl mezi verzemi |
| `Porovnani` | **Porovnávací stroj.** Vstup: položky v kanonickém tvaru. Výstup: seznam změn proti publikovanému stavu. |
| `Import` | Načtení a ověření souboru JSON; změny předává `Porovnani` |
| `Zamek` | Získání, prodloužení a uvolnění výhradního zámku |
| `Security` | Sestavení snapshotu oprávnění, kontroly, datový rozsah |
| `Audit` | Zápis auditní stopy |
| `Documentation` | Render markdownu z repozitáře |
| `Export` | PDF a tisková podoba |
| `Search` | Indexace a dotazování přes OpenSearch |
| `Zdroje` *(etapa 2)* | Konektory a jejich běh; změny předává `Porovnani` |

> **`Porovnani` je jedna služba, ne dvě.** Import (etapa 1) i konektory (etapa 2) ji volají
> se stejným vstupem. Kdyby vznikly dvě implementace porovnávání, rozejdou se —
> a rozdíl mezi nimi by se projevil až rozdílem ve verzích, tedy nejhůř dohledatelně.

---

## Dvě rozhraní, dvě sady controllerů

| | Kdo volá | Stabilita |
|---|---|---|
| `/api/v1/…` | Konzumující aplikace | **Kontrakt.** Mění se jen povýšením verze rozhraní. Popsané v [10-specifikace/05-api-referencni-zdroj.md](../10-specifikace/05-api-referencni-zdroj.md). |
| `/internal/…` | Vlastní React frontend | Smí se měnit spolu s frontendem, nasazuje se s ním zároveň |

Oddělení je záměrné: bez něj by se veřejný kontrakt měnil pokaždé, když si frontend řekne
o jiný tvar dat. Obě sady stojí nad **týmiž službami** — duplikuje se controller, ne logika.

---

## Autorizace

Seed-only RBAC převzatý ze Zápisky — role, klíče a jejich mapování jsou v kódu a verzované
v gitu. Popis a mapa harvestu:
[30-prevzate-moduly/02-autorizace-rbac.md](../30-prevzate-moduly/02-autorizace-rbac.md).

Závazná pravidla pro tuto aplikaci:

1. **Kontrola vždy na serveru.** Skrytí tlačítka v prohlížeči není kontrola.
2. **Klíč vázaný na číselník potřebuje identifikátor číselníku v cestě nebo v dotazu.**
   Bez něj proběhne tichá globální kontrola a uživatel s dílčím rozsahem dostane odmítnutí.
   Poučení ze Zápisky, kde to stálo čas.
3. **Čtení klíč nemá.** Vidí každý (rozhodnutí N1). Klíče existují jen pro měnící akce.
4. **Sestavení efektivních práv je za rozhraním** — dnes z vlastní databáze, později
   z centrálního systému řízení přístupů (rozhodnutí D1).

---

## Průřezová pravidla

| Pravidlo | Důvod |
|---|---|
| **Hlídač velikosti souborů** v testech | Zápiska má obtíže z nabobtnalých souborů, ne z architektury. Vzor: `scripts/check-file-sizes.sh` |
| **Chyby jako `ProblemDetails`** | Jeden tvar chyby pro frontend i pro konzumenty |
| **Žádný kód závislý na IIS** mimo navázání autentizace | Pozdější přesun do kontejneru pak stojí konfiguraci, ne přepis (rozhodnutí A3) |
| **Migrace ručně psanými očíslovanými skripty** | Rozhodnutí A5; plus kontrolní skript stavu instance |
