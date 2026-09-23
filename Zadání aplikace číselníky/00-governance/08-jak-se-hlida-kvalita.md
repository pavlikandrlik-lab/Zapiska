# Podle čeho se hlídá, aby z toho nebyla změť

Odpověď na otázku vývojáře: *„Podle jakého standardu Zápiska jede? Když ne podle MVC,
tak podle čeho se hlídá kvalita?"*

## Nejdřív uvedení na pravou míru

**Zápiska podle MVC jede.** MVC je ale **vzor zpracování požadavku** — směrování, navázání
dat, filtry, autorizace — ne standard kvality. Neříká nic o tom, kam patří doménová logika,
jak velký smí být soubor ani kdo smí sáhnout na databázi.

Kdyby MVC bylo tím, co drží kvalitu, byla by každá MVC aplikace čistá. Není.

## Čím tedy Zápiska jede

**Vrstvenou architekturou s tenkým controllerem.** Není to značková metodika — ne
Clean Architecture, ne Onion, ne DDD. Jsou to **zapsaná pravidla vrstvení**
(`docs/architecture/backend-layering.md`):

```
1. Controller ≤ 200 řádků — jen směrování, ověření vstupu, mapování na výstup
2. Služba          — doménová logika
3. Datová vrstva   — dotazy; závisí jen na kontextu databáze
4. Soubor ≤ 500 řádků
```

A k tomu výslovný seznam **zakázaných vzorů**:

> - dotaz do databáze přímo v akci controlleru — **ne**
> - doménové pravidlo v akci — **ne**
> - uložení změn bez služby — **ne**

## Ale to podstatné: pravidla jsou spustitelná

Zapsané pravidlo, které nikdo nekontroluje, vydrží tak měsíc. Zápiska proto pravidla
**převádí na testy**:

| | |
|---|---|
| Souborů architektonických testů | **31** |
| Testovacích metod | **176** |

Co konkrétně hlídají:

| Oblast | Příklad testu |
|---|---|
| **Autorizace** | `Every_Mutating_Action_Must_Have_AuthorizationAttribute` — každá měnící akce musí mít oprávnění |
| | `Every_Mutating_Action_Should_Have_SpecificPolicy_OrBeExplicitlyAllowlisted` — a konkrétní klíč, ne obecný |
| **Zdroj pravdy** | `SeedSourceOfTruthTests` — klíč v konstantách musí být i v seedu |
| | `NoHardcodedAuthzRolesTests` — název role se nesmí objevit natvrdo v kódu |
| **Závislosti vrstev** | `ModelsFile_ShouldNotImportEntityFramework` — model nesmí znát databázi |
| **Doménová hranice** | `RecordCommands_ShouldNotContainOtherDomainTypes` — příkazy jedné domény nesmí obsahovat typy jiné |
| **Velikost** | `FileSizePolicyTests`, desítky testů `*SplitTests` hlídajících, že rozdělený soubor zůstal rozdělený |
| **Pořadí zpracování** | `MiddlewareOrderingTests` — identita se plní před autorizací |
| **Bezpečnost** | `AppSettingsCredentialLeakGuardTests` — v nastavení nesmí být skutečné heslo |
| **Offline pravidlo** | `GovFontsOfflineTests` — písma se nesmí tahat zvenčí |
| **Známé chyby** | `HarmonogramPhantomUiFixesTests`, `RecordDeleteCascadeFkTests` — jednou opravená chyba se nesmí vrátit |

To je skutečná odpověď: **kvalitu nedrží metodika, drží ji 176 testů, které čtou zdrojový
kód a padnou, když někdo pravidlo poruší.**

---

## Kde je Zápiska slabá — a co z toho Číselníky dělají jinak

Poctivé měření odhalilo díru:

| | Zápiska |
|---|---|
| `Directory.Build.props` | **žádné vynucení** — bez `TreatWarningsAsErrors`, bez `Nullable` |
| `.editorconfig` | **6 řádků** — jen kódování a konce řádků |
| Statická analýza | žádná |

Veškerá kvalita tedy stojí na **testech, které čtou zdrojový kód jako text**. Je to chytré
a funguje to, ale má to dvě slabiny:

1. **Textová shoda se dá obejít.** Test hledající `DbContext` v controlleru neuvidí totéž
   volání schované za pomocnou metodou.
2. **Chytí jen to, na co byl napsaný.** Každé nové pravidlo znamená nový test — a než ho
   někdo napíše, pravidlo neplatí.

### Co Číselníky přidávají

Architektonické testy se přebírají jako vzor **a doplňují se o vrstvu, kterou Zápiska nemá** —
o vynucení překladačem, které nejde obejít, protože nekontroluje text, ale program:

```xml
<!-- Directory.Build.props, P1 blok 1 -->
<Nullable>enable</Nullable>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
```

> `TreatWarningsAsErrors` je záměrné. Pravidlo „build končí s 0 varováními" je v Global
> Constraints — a pravidlo, které nevynucuje nástroj, se do měsíce přestane dodržovat.

Plus hlídač velikosti souborů jako **test**, ne jako skript, který někdo občas pustí
(P1, blok 1). Zápiska ho má jako shellový skript — a shellový skript se v běhu testů
sám nespustí.

---

## Shrnutí pro Číselníky

Tři vrstvy, každá chytá něco jiného:

| Vrstva | Co chytí | Kdy |
|---|---|---|
| **Překladač** | typové chyby, prázdné odkazy, varování | při každém sestavení |
| **Architektonické testy** | porušení vrstvení, chybějící oprávnění, mrtvé klíče, nabobtnalé soubory | při každém běhu testů |
| **Zapsaná pravidla** | to, co se testem vyjádřit nedá — pojmenování, tón hlášek, rozvržení | při čtení a revizi |

**Pravidlo, které lze vyjádřit testem, se vyjádří testem.** Zbytek se zapíše — a ví se,
že na něj musí dohlédnout člověk.
