# Datový model nad Microsoft SQL Server

> **Stav: 🟢 rozhodnuto — varianta C** (schváleno v kole 5, rozhodnutí R1).
> Varianty A a B zůstávají popsané, aby bylo doložitelné, proč se nevybraly.

## Problém k vyřešení

Uložit na relační databázi **libovolně strukturované, víceúrovňové a vzájemně provázané
číselníky** tak, aby:

1. **nový číselník byl datová operace**, ne migrace schématu (~50 číselníků výhledově),
2. **jedno rozhraní obsloužilo všechny** — ne jedna služba na číselník,
3. **verzování bylo datově úsporné** — 1000 hodnot, změna jedné, uloží se jedna,
4. **vazby mezi číselníky byly skutečné vazby**, ne texty s cizím kódem,
5. **platnost hodnoty a verze číselníku byly dvě nezávislé časové osy**.

Východiskem jsou principy **propojených otevřených dat (5★)**. Ty se obvykle realizují
grafovou databází; tady se realizují nad SQL Server.

---

## Varianta A — Trojicové úložiště

Vše je trojice *podmět – přísudek – předmět*, jak to dělá RDF.

```
ciselnik(id, kod, nazev)
predikat(id, kod, nazev, typ, cilovy_ciselnik_id)
polozka(id, ciselnik_id, uri)
tvrzeni(polozka_id, predikat_id, hodnota, objekt_polozka_id)
verze(id, ciselnik_id, cislo)
zmena(verze_id, polozka_id, predikat_id, operace, pred, po)
```

**Pro:** nejčistší naplnění principů propojených dat. Naprostá genericita — i kód a název
položky jsou jen další trojice. Vazba a atribut jsou totéž, není co rozlišovat.

**Proti:** každé čtení je otočení tabulky — z N řádků se skládá jedna položka.
Datové typy hlídá výhradně aplikace, databáze nepomůže. Filtrování a řazení podle atributu
znamená spojení navíc pro každý atribut. Kód se rychle stane nečitelným.

---

## Varianta B — Dokument v poli JSONB

Každá položka je jeden dokument. Struktura je popsaná schématem.

```
ciselnik(id, kod, nazev)
ciselnik_schema(ciselnik_id, verze, json_schema)
polozka(id, ciselnik_id, kod, data jsonb, platnost_od, platnost_do, nadrazena_id)
verze(id, ciselnik_id, cislo)
zmena(verze_id, polozka_id, json_patch)
```

**Pro:** jednoduché a rychlé čtení — položka je jeden řádek. Popis struktury pro rozhraní
vzniká přímo ze schématu. Rozdíl mezi verzemi je standardní záplata dokumentu.

**Proti:** **vazba na jiný číselník je jen text uvnitř dokumentu.** Databáze ji nehlídá,
neplatný odkaz se pozná až při čtení. Dotaz „na které cíle je navázaná tato osoba"
znamená prohledat dokumenty místo skoku přes index. Propojenost je tím nominální —
odkaz existuje, ale není to hrana grafu. Rozdíl mezi verzemi je záplata dokumentu,
takže na otázku „kdo kdy měnil atribut *stav*" se odpovídá rozborem záplat, ne dotazem.

---

## Varianta C — Kombinovaná *(doporučeno)*

Tři vrstvy podle toho, jak se která část dat chová:
**co mají všechny číselníky společné** je v typovaných sloupcích,
**co se liší** je v atributech,
**odkazy** jsou samostatné řádky s cizím klíčem.

### Definice — struktura číselníku jako data

```
ciselnik(id, kod, nazev, popis, hierarchicky, aktivni, spravce_id,
         rezim_spravy)                 -- RUCNI | EXTERNI, nikdy obojí

atribut_definice(id, ciselnik_id, kod, nazev, typ, povinny, poradi, vycet_hodnot)
    typ ∈ {text, cislo, datum, ano_ne, vycet}

vazba_definice(id, ciselnik_id, kod, nazev, cilovy_ciselnik_id, povinna, nasobnost)
```

Založení číselníku = vložení řádků sem. Žádná migrace.

### Data — publikovaný stav

```
polozka(id, ciselnik_id, kod, nazev,
        nadrazena_polozka_id,          -- hierarchie uvnitř číselníku
        platnost_od, platnost_do,      -- věcná časová osa
        aktivni, poradi)

polozka_atribut(polozka_id, atribut_definice_id,
                hodnota_text, hodnota_cislo, hodnota_datum, hodnota_ano_ne)

polozka_vazba(polozka_id, vazba_definice_id, cil_polozka_id)   -- hrana grafu, s cizím klíčem
```

`polozka.id` je **stálá vnitřní identita** — přežije změnu kódu i názvu.

### Nouzový klíč

```
superadmini(osoba_id PRIMARY KEY REFERENCES osoby(id),
            poznamka, zalozeno_kdy, zalozil_id)
```

Kdo je zde uveden, projde **každou** kontrolou oprávnění bez ohledu na role a rozsahy.
Záměrně stojí **mimo tabulky rolí** — kdyby na nich závisel, nefungoval by právě ve chvíli,
kdy je potřeba, tedy když jsou data o oprávněních porušená.

Plní se ručním SQL skriptem při zakládání databáze; je to i způsob, jak se do čerstvě
nasazené aplikace vůbec dostane první správce.

### Zámek editace

```
zamek_ciselniku(ciselnik_id PRIMARY KEY,   -- databáze fyzicky nedovolí dva zámky
                osoba_id, ziskan_kdy, posledni_aktivita_kdy, platnost_do)
```

Jeden číselník upravuje v jeden okamžik nejvýše jeden člověk. Zámek patří **osobě**,
ne oknu prohlížeče — proto načtení stránky znovu nikoho neblokuje proti jeho vlastní práci.
Úplný návrh: [10-specifikace/08-zamek-editace.md](../10-specifikace/08-zamek-editace.md).

### Verze a změny

```
ciselnik_verze(id, ciselnik_id, cislo_hlavni, cislo_vedlejsi,
               vydana_kdy, vydal_id, poznamka)

zmena(id, ciselnik_id, verze_id,       -- verze_id NULL = rozpracovaná, dosud nepublikovaná
      polozka_id,
      druh,                            -- POLOZKA | ATRIBUT | VAZBA | DEFINICE
      atribut_definice_id, vazba_definice_id,
      operace,                         -- PRIDANO | ZMENENO | ODEBRANO
      hodnota_pred, hodnota_po,
      kdo_id, kdy)
```

**Číslo verze je dvojice.** Hlavní se zvyšuje při změně struktury číselníku,
vedlejší při změně hodnot. Konzument z čísla pozná, jestli se ho změna může dotknout:
změna vedlejšího čísla se ho dotknout nemůže, změna hlavního ano.

**Uložený je publikovaný stav a záznamy změn. Historická verze se dopočítá.**

Tisíc hodnot, změna jedné → **jeden řádek v `zmena`**. Přesně to, co zadání žádá.

### Jak se dopočítá historická verze

```
vezmi publikovaný stav
pro verzi V od nejnovější sestupně až po V+1:
    aplikuj změny verze V obráceně
       PRIDANO  → odeber
       ODEBRANO → vrať zpět
       ZMENENO  → nastav hodnotu_pred
```

Výsledek je číselník tak, jak vypadal ve verzi V, sestavený jako celek — přestože
v databázi leží jen rozdíly.

> Kdyby počet verzí u jednoho číselníku narostl natolik, že dopočet zpomalí, řeší se to
> **občasným uloženým otiskem** každých K verzí, od kterého se pak počítá.
> **Nestaví se teď** — přidání je čistě přírůstkové a nic nemění.

### Rozpracovaný stav a publikování

Verzi vydává člověk (rozhodnutí V1). Číselník tedy má dva stavy a je zásadní,
**který z nich leží v tabulkách**.

| Stav | Kde je | Kdo ho vidí |
|---|---|---|
| **Publikovaný** | přímo v `polozka`, `polozka_atribut`, `polozka_vazba` | konzumenti rozhraní a všichni čtenáři — prostý dotaz, žádný dopočet |
| **Rozpracovaný** | `zmena` s `verze_id IS NULL` | editoři daného číselníku, jako překryv nad publikovaným stavem |

```
Uložit změny        → zapíše řádky do zmena s verze_id NULL
                      publikovaný stav se nemění, konzumenti nic nepoznají

Publikovat verzi    → založí ciselnik_verze
                      přiřadí jí všechny rozpracované změny
                      promítne je do polozka / polozka_atribut / polozka_vazba
                      zvýší hlavní číslo, pokud mezi změnami je DEFINICE, jinak vedlejší
```

**Proč takhle a ne obráceně.** Kdyby v tabulkách ležel rozpracovaný stav a publikovaná
verze se dopočítávala, dopočet by běžel při **každém** dotazu konzumenta. Čte neomezený
počet uživatelů a aplikací, edituje 5–20 lidí. Dopočet patří na tu stranu, kde je málo lidí.

Opakovaná úprava téže hodnoty před publikováním **nezakládá druhý řádek** — existující
rozpracovaná změna se přepíše a `hodnota_pred` zůstává z prvního zásahu. Jinak by verze
obsahovala mezikroky, které nikdy nikdo neviděl.

### Režim správy číselníku

`ciselnik.rezim_spravy` je **RUCNI**, nebo **EXTERNI**. Hybrid neexistuje (rozhodnutí E3).

| Režim | Kdo mění hodnoty | Jak vzniká verze |
|---|---|---|
| RUCNI | editor v tabulce nebo nahráním souboru JSON | publikováním, ručně |
| EXTERNI | výhradně konektor zdroje | během zdroje, pokud našel rozdíl |

U externího číselníku je ruční editace zakázaná **na úrovni modelu**, ne skrytím tlačítka.
Chyba v datech se opravuje u zdroje. Potřeba doplnit vlastní údaje se řeší samostatným
číselníkem navázaným vazbou — nikdy zápisem do přebíraných dat.

### Proč tahle varianta

| Požadavek | Jak ho C plní |
|---|---|
| Nový číselník bez migrace | Struktura je v `atribut_definice` a `vazba_definice` — data |
| Jedno rozhraní | Obálka odpovědi čte společné sloupce, proměnná část se skládá z atributů a vazeb |
| Úsporné verzování | Změna jednoho atributu = jeden řádek `zmena` |
| Skutečné vazby | `polozka_vazba` má cizí klíč. Je to hrana grafu, ne text. |
| Dotaz oběma směry | „na které cíle je navázaná tato osoba" je skok přes index, ne prohledávání |
| Dvě časové osy | `platnost_od/do` na položce, `ciselnik_verze` nad číselníkem |
| Historie po atributech | „kdo kdy měnil atribut *stav*" je běžný dotaz, ne rozbor záplat |
| Rozsah oprávnění | `ciselnik_id` je na položce i na definici — kontrola práv je jednoduchá |

Společné sloupce nejsou kompromis. Kód, název, nadřazená položka, platnost a příznak
aktivity **mají opravdu všechny číselníky** — a jsou to zároveň sloupce, podle kterých
se filtruje, řadí a staví strom. V typovaných sloupcích s indexy je 90 % dotazů obyčejné SQL.

### Co C stojí

Zápis se dotýká tří tabulek místo jedné a záznam změny potřebuje rozlišovač druhu.
Je to víc návrhu předem než u varianty B. To je cena za skutečný graf a za dohledatelnou
historii po atributech — obojí je v zadání požadované.

### Podvarianta, která se zvažovala a zamítla

Nahradit `polozka_atribut` sloupcem `atributy jsonb` přímo na položce.
Zápis by byl jednodušší.

**Zamítnuto:** rozdíl mezi verzemi by pak byl záplata dokumentu, ne změna konkrétního
atributu. Záznam změny by nemohl odkazovat na `atribut_definice_id` cizím klíčem
a historie po atributech — výslovný požadavek zadání — by se z ní nedala dotazovat.

---

## Co z modelu vyplývá dál

- **Definice je jediný zdroj pravdy** pro formulář v prohlížeči, validaci při ukládání,
  ověření importu i pro popis struktury vydávaný rozhraním. Vzniká z ní, ne vedle ní.
- **Identifikátory URI se skládají z bázové adresy, `ciselnik.kod` a `polozka.kod`.**
  Bázová adresa je adresa aplikace v doméně FIS (rozhodnutí Z4).

  Z toho plyne pravidlo, které se **musí vynutit v kódu**:

  > **Kód číselníku i kód položky jsou po prvním publikování verze neměnné.**

  Do prvního publikování se mění volně — číselník tehdy pro konzumenty neexistuje
  ([10-specifikace/12-prochazeni.md](../10-specifikace/12-prochazeni.md), W5). Po něm už
  konzumenti drží identifikátor u sebe a jeho změna by jim ho rozbila.

  Vnitřně je identitou `polozka.id`, takže přejmenování názvu ani změna atributů historii
  nerozbijí. Kód je ale součástí identifikátoru, a proto nepodléhá téže volnosti.
- **Auditní log je nad rámec verzování.** Verze říká *co* se změnilo v číselníku;
  auditní log říká *kdo, kdy a odkud* zasáhl do aplikace — včetně změn oprávnění,
  definic a nastavení zdrojů.
