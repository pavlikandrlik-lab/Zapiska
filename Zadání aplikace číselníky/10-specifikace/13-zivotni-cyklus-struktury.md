# Životní cyklus struktury číselníku

Kdo strukturu určuje, jak se zakládá, co se s ní smí za provozu dělat a co se stane
konzumentům. Doplňuje [01-domenovy-model.md](01-domenovy-model.md) a
[03-schopnosti.md](03-schopnosti.md) (S2).

---

## Kdo strukturu stanovuje

**Role *Správce číselníků*** (rozhodnutí E1). Je to samostatná role záměrně — odděluje
toho, kdo číselníky den co den aktualizuje, od toho, kdo je navrhuje a zakládá.

| Akce | Kdo | Rozsah |
|---|---|---|
| Založit číselník | Správce číselníků | **globální** — v okamžiku zakládání číselník ještě neexistuje, není na co vázat rozsah |
| Definovat a měnit strukturu | Správce číselníků | jen číselníky **ve svém datovém rozsahu** |
| Měnit hodnoty | Editor | jen číselníky ve svém datovém rozsahu |

Editor strukturu měnit **nemůže**, a je to úmysl: změna struktury se dotýká konzumujících
aplikací, kdežto změna hodnoty ne.

---

## Proces založení číselníku

```
1. Kód, název, popis
2. Režim správy:  RUCNI  |  EXTERNI          ← nejzávažnější volba, viz níže
3. Příznak hierarchie:  ano | ne
4. Atributy:  kód, název, typ, povinnost, pořadí, (výčet hodnot)
5. Vazby:     kód, název, cílový číselník, povinnost
6. Naplnění hodnot — ručně v tabulce, nebo nahráním souboru JSON
7. PUBLIKOVAT VERZI 1.0
```

**Do publikování první verze číselník přes rozhraní neexistuje.** Nedokončený číselník
nemá co nabízet — konzument by dostal prázdný obsah a nevěděl by, že jde o rozpracovanou
věc.

**Kód je po prvním publikování neměnný** (rozhodnutí Z4) — je součástí stálého
identifikátoru, který si konzumenti ukládají u sebe.

### Volba režimu správy je nejzávažnější

| | RUCNI | EXTERNI |
|---|---|---|
| Kdo mění hodnoty | editor v tabulce nebo importem | **výhradně konektor zdroje** |
| Ruční editace | ano | **nikdy** — zakázaná na úrovni modelu |
| Kdy vzniká verze | publikováním, ručně | během konektoru, pokud našel rozdíl |
| Oprava chyby v datech | v aplikaci | **u zdroje** |

Hybrid neexistuje (rozhodnutí E3). Číselník má **právě jednoho vlastníka dat**.
Režim jde později přepnout, ale ne obejít.

---

## Co se smí měnit za provozu

Struktura se měnit smí. **Změna vydává vlastní verzi okamžitě** a zvyšuje **hlavní**
číslo — a je **zakázaná, dokud má číselník nepublikované změny hodnot**.

> **Než vznikne verze 1.0, nic z toho neplatí.** Číselník se tehdy teprve skládá,
> přes rozhraní neexistuje a nemá koho chránit. Správce upravuje strukturu i hodnoty
> volně a **první publikování vydá 1.0 se vším najednou**. Pravidla níže se zapínají
> až po první verzi.

> Kdyby se definice měnila hned a verze se zvýšila až příštím publikováním, viděli by
> konzumenti mezitím novou strukturu se starým číslem verze. Číslo verze je přitom jediné,
> podle čeho konzument pozná, že se ho změna může dotknout — nesmí lhát ani na chvíli.

### Matice povolených změn

**Vedoucí pravidlo: změna struktury nesmí tiše znehodnotit data.**
Když ji nejde provést beze ztráty, aplikace ji **odmítne a vyjmenuje, co jí brání** —
nikdy nevrátí pouhé „nelze".

| Změna | Prázdný číselník | S hodnotami |
|---|---|---|
| Přidat **nepovinný** atribut | ✅ | ✅ existující položky ho mají prázdný |
| Přidat **povinný** atribut | ✅ | ⛔ **nejde přímo** — přidat jako nepovinný, doplnit hodnoty, pak povýšit |
| Povýšit atribut na povinný | ✅ | ⚠️ **jen když ho mají vyplněný všechny položky**; jinak výpis těch, kterým chybí |
| Zrušit povinnost | ✅ | ✅ |
| Přejmenovat atribut *(název)* | ✅ | ✅ název je popisek pro člověka |
| Změnit **kód** atributu | ✅ | ⛔ **ne po publikování** — kód je v popisu struktury a konzumenti podle něj čtou |
| Změnit **typ** atributu | ✅ | ⚠️ **jen když jsou všechny hodnoty převeditelné**; jinak výpis nepřevoditelných |
| Rozšířit výčet hodnot | ✅ | ✅ |
| Zúžit výčet hodnot | ✅ | ⚠️ **jen když odebíranou hodnotu nikdo nepoužívá**; jinak výpis položek |
| Odebrat atribut | ✅ | ⚠️ **s výslovným potvrzením**, hláška uvádí počet dotčených položek |
| Přidat **nepovinnou** vazbu | ✅ | ✅ |
| Přidat **povinnou** vazbu | ✅ | ⛔ nejde přímo — stejný postup jako u povinného atributu |
| Odebrat vazbu | ✅ | ⚠️ s výslovným potvrzením |
| **Zapnout hierarchii** | ✅ | ✅ existující položky zůstanou na kořenové úrovni |
| **Vypnout hierarchii** | ✅ | ⚠️ **jen když žádná položka nemá nadřazenou** |
| Změnit režim RUCNI → EXTERNI | ✅ | ✅ od té chvíle ruční editace končí |
| Změnit režim EXTERNI → RUCNI | ✅ | ✅ konektor se odpojí, data zůstanou |

Legenda: ✅ projde · ⚠️ projde po kontrole, jinak srozumitelné odmítnutí · ⛔ nejde, existuje náhradní postup

### Proč „přidat povinný atribut" nejde přímo

Existující položky by ho neměly vyplněný, takže by číselník byl okamžitě po publikování
v rozporu s vlastním popisem struktury. Konzument, který si podle popisu vygeneroval kód,
by dostal prázdnou hodnotu tam, kde má být jistota.

Náhradní postup je tříkrokový a je v aplikaci **nabídnutý v hlášce**, ne ponechaný
na domyšlení:

```
1. přidej atribut jako NEPOVINNÝ   → verze 2.0
2. doplň hodnoty u všech položek   → verze 2.1
3. povyš atribut na POVINNÝ        → verze 3.0
```

---

## Přidání úrovně: z dvouúrovňového číselníku tříúrovňový

**V tomhle modelu nic takového jako „dvouúrovňový číselník" neexistuje.**

Hierarchie je odkaz položky na nadřazenou položku **téhož číselníku**.
Hloubka není nikde deklarovaná ani omezená — `hierarchicky` je jen zapnuto/vypnuto.

Přidání třetí úrovně je proto **čistě datová operace**: založíš položku, jejímž rodičem
je položka druhé úrovně. A to je celé.

| Co se změní | |
|---|---|
| Struktura číselníku | **nic** |
| Databáze | **žádná migrace** |
| Popis struktury (JSON Schema) | **nemění se** — obsahuje `nadrazenyKod`, ne počet úrovní |
| Rozhraní | **nemění se** |
| Číslo verze | **vedlejší** — jsou to přidané hodnoty, ne změna struktury |
| Konzument | `tvar=strom` vrátí o úroveň hlubší strom; `tvar=plochy` položky s `nadrazenyKod` |

Kdo to udělá: **editor**, ne správce. Je to přidání hodnot.

> Kdyby hloubka byla součástí struktury, znamenalo by prohloubení změnu schématu,
> novou hlavní verzi a zásah do rozhraní. Tenhle jediný příklad je nejlepší doklad,
> že model varianty C je postavený správně.

**Jediná výjimka:** byl-li číselník založen s vypnutou hierarchií, je její zapnutí
změnou struktury → hlavní verze. Poté už je prohlubování opět jen data.

---

## Případ NIPEZ: přebíraný číselník, který si chci detailizovat

Zadaný příklad: *„budu mít NIPEZ, ale nebude pro mě dost detailní a budu chtít
ještě detailizovat pro interní potřeby."*

Tady narazíme na rozhodnutí E3 a je dobře, že narazíme.

### Co nejde

NIPEZ se přebírá zvenčí, je tedy v režimu **EXTERNI**. **Nelze do něj přidat vlastní
položky.** Kdyby to šlo:

- příští běh konektoru by je smazal, protože ve zdroji nejsou,
- nebo by konektor musel rozlišovat „naše" a „cizí" položky — a číselník by přestal mít
  jednoho vlastníka. Nikdo by pak nevěděl, co je autoritativní.

### Co jde a jak

**Samostatný číselník navázaný vazbou.**

```
┌──────────────────────┐          ┌────────────────────────────┐
│  nipez               │          │  nipez-detail              │
│  režim: EXTERNI      │◄─────────┤  režim: RUCNI              │
│  přebírán ze zdroje  │  vazba   │  hierarchický sám v sobě   │
│  needituje se        │ povinná  │  spravujeme si ho my       │
└──────────────────────┘          └────────────────────────────┘
```

Definice `nipez-detail`:

| | |
|---|---|
| Režim správy | RUCNI |
| Hierarchie | ano — vlastní vnitřní členění může jít do libovolné hloubky |
| Vazba `nadrazenaPolozkaNipez` | **povinná**, cílový číselník `nipez` |
| Vlastní atributy | co si potřebujeme doplnit |

**Co z toho má konzument:**

- kdo chce jen NIPEZ → čte `nipez`, nic se pro něj nemění,
- kdo chce i náš detail → čte `nipez-detail` a z vazby ví, pod kterou položku NIPEZ patří,
- `?rozbalit=nadrazenaPolozkaNipez` mu rovnou doplní i tu nadřazenou položku.

**Co z toho máme my:** NIPEZ se dál aktualizuje ze zdroje beze srážek s naší prací,
a náš detail má vlastní verzování a vlastní historii.

> Je to tentýž postup, jaký specifikace předepisuje obecně: *potřeba doplnit
> k přebíraným datům vlastní údaj se řeší samostatným číselníkem navázaným vazbou,
> nikdy zápisem do přebíraných dat.*

---

## Jak na změnu struktury reagují ostatní části

| Část | Reakce |
|---|---|
| **Databáze** | Žádná. Struktura je data — mění se řádky v `atribut_definice` a `vazba_definice`, ne schéma. |
| **Popis struktury (JSON Schema)** | Přegeneruje se z definice. Vzniká z ní, ne vedle ní. |
| **Číslo verze** | Hlavní se zvýší. Konzument z něj pozná, že se ho změna může dotknout. |
| **Mezipaměť rozhraní** | Značka odvozená od verze se změní, konzumenti si stáhnou nová data. |
| **Editační tabulka** | Sloupce se skládají z definice, takže se objeví samy. |
| **Import** | Ověření se řídí novou definicí. Starý soubor s odebraným atributem selže s hláškou, která ten atribut jmenuje. |
| **Vyhledávání** | Index se obnoví po publikování verze. |
| **Auditní log** | Změna struktury je zaznamenaná — kdo, kdy, co. |

**Nikde není krok, který by musel udělat programátor.** To je celý smysl schopnosti S2.
