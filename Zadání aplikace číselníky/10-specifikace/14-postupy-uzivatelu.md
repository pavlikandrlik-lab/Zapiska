# Postupy uživatelů krok za krokem

Kolikrát kde kliknout. Doplňuje [12-prochazeni.md](12-prochazeni.md) (co po čem následuje)
o **skutečný počet úkonů** — protože počítání kliků je zkouška návrhu, ne popis.

Značení: **[K]** kliknutí · **[P]** psaní do pole · **[V]** výběr z nabídky

---

## A. Správce zakládá nový číselník

Příklad: **Výdajové oblasti** — plochý číselník, 3 atributy, 1 vazba, 12 hodnot.

### A1. Založení — 2 kliky

```
rail → [K] + Nový číselník
       [P] kód:   vydajove-oblasti
       [P] název: Výdajové oblasti
       [P] popis: (nepovinné)
       ( ) režim správy — výchozí RUCNI, netřeba sahat
       ( ) hierarchie   — výchozí vypnutá, netřeba sahat
       [K] Založit číselník
```

Výchozí hodnoty jsou nastavené tak, aby běžný případ nevyžadoval **žádný** klik navíc.
Kdo zakládá přebíraný nebo hierarchický číselník, ty dva přepínače přepne.

### A2. Struktura — asi 4 kliky na atribut

Otevře se záložka **Struktura**.

```
[K] + Přidat atribut
    [P] kód, [P] název
    [V] typ (text / číslo / datum / ano-ne / výčet)
    [K] povinný — jen když ano
    [P] přípustné hodnoty — jen u typu výčet
```

Tři atributy ≈ **10 kliků**. Jedna vazba ≈ **4 kliky** (přidat, kód, název, výběr
cílového číselníku, povinnost).

```
[K] Uložit strukturu
```

> **Před první verzí se struktura mění volně.** Číselník se teprve skládá, přes rozhraní
> neexistuje, takže úprava struktury nevydává verzi. Pravidlo „změna struktury = hlavní
> verze" se zapíná až po vydání 1.0.

### A3. Hodnoty a vydání — 3 kliky plus psaní

```
[K] Upravit hodnoty          ← aplikace si vyžádá zámek
[K] + Přidat hodnotu         ← nebo TAB na konci posledního řádku
    [P] kód, [P] název, [P] atributy
    [V] cíl vazby z našeptávače
… opakovat pro dalších 11 hodnot …
[K] Publikovat verzi         ← vznikne 1.0, zámek se uvolní
```

**Přidání řádku klávesou TAB na konci posledního** je záměrné: u dvanácti hodnot je
dvanáct kliků na tlačítko zbytečná práce a lidé to od tabulky čekají.

### Součet

| Krok | Kliků |
|---|---|
| Založení | 2 |
| Struktura (3 atributy + 1 vazba) | ~14 |
| Hodnot 12 přes TAB | ~2 |
| Publikování | 1 |
| **Celkem** | **~19 kliků a chvíle psaní** |

### Kdy se neklikne vůbec

Číselník o tisíci hodnotách se ručně neťuká. Správce nechá PDF přepsat do souboru JSON
a nahraje ho — **režim založení vytvoří číselník včetně definice atributů a vazeb**
([06-import-json.md](06-import-json.md)).

```
[K] Nahrát soubor JSON  →  [K] Zkontrolovat  →  [K] Promítnout  →  [K] Publikovat verzi
```

**4 kliky bez ohledu na to, jestli má číselník 12 nebo 5 000 hodnot.**

> Ruční cesta je pro malé číselníky dělané od stolu. Velké chodí importem — a to je taky
> důvod, proč je importní formát soběstačný a nese i definici struktury.

---

## B. Pracovník přidá novou položku

Nejčastější úkon v aplikaci.

```
rail → [K] číselník
       [K] Upravit hodnoty          ← zámek
       [K] + Přidat hodnotu         ← nebo TAB na konci posledního řádku
           [P] kód, [P] název, [P] atributy
           [V] cíl vazby
       [K] Uložit            nebo   [K] Publikovat verzi
```

**4 kliky.** S klávesou TAB **3**.

Rozdíl mezi oběma tlačítky na konci:

| | Co se stane |
|---|---|
| `Uložit` | Změna je uložená jako rozpracovaná. **Konzumenti nic nepoznají.** Lze zavřít prohlížeč a vrátit se zítra. |
| `Publikovat verzi` | Vznikne nová verze, konzumenti dostanou nová data, zámek se uvolní. |

---

## C. Pracovník prohloubí číselník o další úroveň

Zadaný příklad: číselník má dvě úrovně a je potřeba třetí.

### Co se přitom neděje

| | |
|---|---|
| Změna struktury | **ne** |
| Migrace databáze | **ne** |
| Změna popisu struktury pro konzumenty | **ne** |
| Změna rozhraní | **ne** |
| Hlavní číslo verze | **ne** — zvýší se jen vedlejší |

**Hloubka není nikde deklarovaná ani omezená.** Přidání úrovně je proto **přidání hodnoty**,
a dělá ho **pracovník, ne správce**.

### Postup — 2 kliky

```
[K] Upravit hodnoty
    najdi řádek položky druhé úrovně
[K] ↳ + Podřízená             ← na řádku; nadřazená se předvyplní
    [P] kód, [P] název
[K] Publikovat verzi
```

Akce `+ Podřízená` na řádku je hlavní cesta právě proto, že **předvyplní nadřazenou
položku**. Odpadá tím hledání rodiče v našeptávači i možnost splést se.

### Přesun existující položky pod jinou

Sloupec **Nadřazená** je u hierarchického číselníku vždy vidět a mění se našeptávačem:

```
[K] buňka ve sloupci Nadřazená
    [P] dvě písmena → nabídka
[V] nadřazená položka
```

Nabídka **vylučuje položku samotnou i její potomky**, takže cyklus nejde vyrobit ani omylem.

### Zapnutí hierarchie u číselníku, který ji neměl

Tohle už **je** změna struktury a dělá ji **správce**:

```
[K] záložka Struktura  →  [K] přepínač Hierarchický  →  [K] Uložit strukturu
```

Vydá se nová **hlavní** verze. Existující položky zůstanou na kořenové úrovni.
Od té chvíle je prohlubování zase jen práce s hodnotami.

---

## D. Případ NIPEZ: detailizace přebíraného číselníku

Zadaný příklad: *NIPEZ přebíráme zvenčí, ale nestačí nám podrobností.*

**Do přebíraného číselníku vlastní položky přidat nelze** (rozhodnutí E3) — příští běh
zdroje by je smazal. Postup je jiný a dělá ho **správce jednou**:

```
[K] + Nový číselník
    [P] kód: nipez-detail, [P] název
    [K] hierarchický: ano
    [K] Založit číselník
[K] Struktura → [K] + Přidat vazbu
    [P] kód: nadrazena-nipez, [P] název: Nadřazená položka NIPEZ
    [V] cílový číselník: nipez
    [K] povinná
[K] Uložit strukturu
```

**~9 kliků, jednou.** Od té chvíle pracovník přidává vlastní podrobnější položky
postupem B a u každé vybere, pod kterou položku NIPEZ patří.

Podrobné zdůvodnění: [13-zivotni-cyklus-struktury.md](13-zivotni-cyklus-struktury.md).

---

## Kde je návrh nejtěžší — a co s tím

| Úkon | Náročnost | Proč to tak je |
|---|---|---|
| Přidat hodnotu | 3–4 kliky | Nejčastější úkon, proto nejlevnější |
| Prohloubit o úroveň | 2 kliky | Datová operace, ne strukturální |
| Založit malý číselník | ~19 kliků | Jednorázové |
| Založit velký číselník | 4 kliky | Importem, nezávisle na počtu hodnot |
| **Definovat strukturu atribut po atributu** | **~4 kliky na atribut** | **Nejtěžší část celého rozhraní** |

Poslední řádek je poctivé přiznání: u číselníku s deseti atributy je definice struktury
čtyřicet kliků. **Zlevnit se dá jedině importem** — soubor JSON nese definici s sebou,
takže se struktura naklikat vůbec nemusí.

> Proto importní formát nese definici atributů a vazeb, ne jen hodnoty. Nebyl to rozmar
> — je to jediná cesta, jak se u velkých číselníků vyhnout ruční definici.
