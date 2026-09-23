# Wireframy

Rozvržení obrazovek. **Vzhled je daný gov design systemem** — barvy, písma ani tvary se
nevymýšlejí. Rozhoduje se zde jen o tom, **co je kde a proč**.

Doprovází [10-obrazovky.md](10-obrazovky.md), které říká *co* na obrazovce je.
Tento dokument říká *kde*.

---

## Vzory převzaté z osvědčených produktů

Nic z toho není vymyšlené od nuly. Každý vzor už někde léta funguje a lidé ho znají.

| Vzor | Odkud | Proč právě sem |
|---|---|---|
| Trvalý levý rail se seznamem a filtrem | Confluence, Notion | „Který číselník" je trvalý kontext, ne jednorázová volba. Uživatel mezi číselníky skáče. |
| Seznam → detail se záložkami | GitHub (repozitář), Azure Portal (prostředek) | Uživatel drží kontext jedné věci a přepíná **pohledy**, ne stránky |
| Editační mřížka s přilepenou hlavičkou | Airtable, Tabulky Google | Hromadná editace tisíce řádků je tabulková práce a lidé pro ni mají naučené pohyby |
| Rozdíl mezi verzemi | GitHub | Zavedený způsob, jak ukázat přidané, odebrané a změněné |
| Import ve třech krocích s náhledem | Stripe, Airtable | Nahrát → zkontrolovat → potvrdit je jediná obrana proti zničení dat překlepem |
| Pruh „upravuje X" | Confluence | Známé z redakčních systémů, čte se okamžitě |
| Role × rozsah v jedné tabulce | Keycloak, správa přístupů v Azure | Role a rozsah jsou zavedená dvojice, ne dvě obrazovky |

---

## Rámec aplikace

Platí na všech obrazovkách kromě tisku. Rozvržení počítá se **širokou obrazovkou**.

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│  Číselníky          [ 🔍  Hledat napříč číselníky…        ]      ☾    Jan Novák  │
├──────────────────────┬───────────────────────────────────────────────────────────┤
│ [ Filtrovat…      ]  │                                                           │
│                      │                                                           │
│  Cíle           312  │                                                           │
│  Ekonomické o.   18  │                        OBSAH                              │
│  Osoby          847  │                                                           │
│  Rozpočtoví k.   11  │                                                           │
│  Stavy cíle       6  │                                                           │
│  Výdajové obl.   24  │                                                           │
│                      │                                                           │
│  ──────────────────  │                                                           │
│  + Nový číselník     │                                                           │
└──────────────────────┴───────────────────────────────────────────────────────────┘
   280 px pevně           zbytek šířky
```

**Rozhodnutí a jejich důvody:**

- **Rail je trvalý, ne rozbalovací.** V číselníkové aplikaci je „který číselník" kontext,
  ve kterém uživatel žije. Skrývat ho za tlačítko znamená nutit ho ke kliku navíc pokaždé.
- **Číslo vedle názvu je počet hodnot**, ne pořadí. Je to údaj, který uživatel potřebuje
  ještě před otevřením — pozná podle něj, jestli ho čeká krátký seznam, nebo tisícovka řádků.
- **`+ Nový číselník` je pod čarou, ne nahoře.** Zakládání je vzácná akce správce;
  hledání je častá akce každého. Nahoru patří to častější.
- **Rail nemá ikony u položek.** Padesát číselníků nemá padesát smysluplných ikon a
  vymyšlené ikony jen zabírají místo, které potřebuje název.

---

## O1 — Seznam číselníků

Obsahová část při vstupu. Rail ukazuje názvy; tabulka ukáže i to, co se do railu nevejde.

```
│  Číselníky                                                                       │
│  Referenční zdroj číselníků. Vidí je každý, upravovat je smí přidělený správce.   │
│                                                                                  │
│  [ Filtrovat…            ]   Režim: [ Vše ▾ ]   Stav: [ Aktivní ▾ ]              │
│                                                                                  │
│  KÓD              NÁZEV                    HODNOT   VERZE   REŽIM     PUBLIKOVÁN │
│  ───────────────────────────────────────────────────────────────────────────────│
│  cile             Rozpočtové cíle             312    3.14   Ručně     30. 8. 2026│
│  ekonomicke-org   Ekonomické orgány            18    1.02   Ručně     12. 6. 2026│
│  osoby            Osoby                       847    5.41   Ručně      2. 9. 2026│
│  stavy-cile       Stavy cíle                    6    1.00   Ručně     14. 3. 2026│
│  vydajove-oblasti Výdajové oblasti              24    2.07   Ručně     28. 8. 2026│
│                                                                                  │
│                                                        ‹ 1 2 3 ›   50 na stránku │
```

**Prázdný stav** — první spuštění:

```
│                                                                                  │
│                      Zatím není založen žádný číselník.                          │
│                      Založte první a nadefinujte, jaké údaje ponese.             │
│                                                                                  │
│                             [ Založit číselník ]                                 │
```

Prázdná obrazovka je výzva k akci, ne oznámení o prázdnotě. Tlačítko se ukáže jen tomu,
kdo na to má právo — ostatní vidí jen první větu.

---

## O2 — Detail číselníku, záložka Hodnoty

```
│  ‹ Číselníky                                                                     │
│                                                                                  │
│  Rozpočtové cíle                              [ Upravit hodnoty ] [ Tisk ]       │
│  cile · verze 3.14 · publikováno 30. 8. 2026 · spravováno ručně                  │
│                                                                                  │
│  ┌ Hodnoty ┬ Struktura ┬ Verze ┬ Rozdíl verzí ┐                                  │
│  ┴─────────┴───────────┴───────┴──────────────┴───────────────────────────────── │
│                                                                                  │
│  [ Hledat v hodnotách… ]   Platné k: [ 7. 9. 2026 ]   Zobrazit: (•) Seznam ( ) Strom│
│                                                                                  │
│  KÓD           NÁZEV                  ČÍSLO   STAV  MANAŽER      PLATNOST        │
│  ───────────────────────────────────────────────────────────────────────────────│
│  C-2026-001    Rozvoj infrastruktury   001    A     Jan Novák →  od 1. 1. 2026   │
│  C-2026-002    Digitalizace agend      002    A     Eva Dvořáková→ od 1. 1. 2026 │
│  C-2025-014    Obnova vozového parku   014    U     Jan Novák →  1.1.25–31.12.25 │
│                                                                                  │
│                                                        ‹ 1 2 … 7 ›  50 na stránku│
```

Ve stromovém zobrazení se mění jen tělo tabulky:

```
│  KÓD           NÁZEV                                  ČÍSLO   STAV               │
│  ───────────────────────────────────────────────────────────────────────────────│
│  ▾ C-2026-001  Rozvoj infrastruktury                   001    A                  │
│      C-2026-001-A  Silnice II. třídy                   001-A  A                  │
│      C-2026-001-B  Mosty                               001-B  A                  │
│  ▸ C-2026-002  Digitalizace agend                      002    A                  │
```

**Rozhodnutí:**

- **Záložky, ne podstránky.** Hodnoty, struktura, verze a rozdíl jsou čtyři pohledy na
  jednu věc. Uživatel mezi nimi přepíná často a nechce ztrácet kontext.
- **`Platné k` je předvyplněné dneškem.** Nejčastější otázka je „co platí teď".
  Kdo se ptá na jiné datum, přepíše ho.
- **Vazba je odkaz s šipkou** (`Jan Novák →`). Prokliknutím se přejde na položku v číselníku
  osob. Je to hrana grafu a chová se jako odkaz, protože to odkaz je.
- **Hlavička nese verzi a režim správy.** Bez nich uživatel neví, jestli se dívá na aktuální
  data a jestli je vůbec smí měnit.
- **Je-li číselník externí**, tlačítko `Upravit hodnoty` tam **není vůbec** — ne zašedlé.
  Zašedlé tlačítko slibuje, že to jednou půjde. U externího číselníku to nepůjde nikdy.

---

## O2 — záložka Verze

```
│  VERZE   VYDÁNO           VYDAL           ZMĚN   POZNÁMKA                        │
│  ───────────────────────────────────────────────────────────────────────────────│
│  3.14    30. 8. 2026      Jan Novák          1   Oprava názvu cíle 014           │
│  3.13    12. 8. 2026      Jan Novák         37   Doplnění cílů pro rok 2027      │
│  3.12     4. 8. 2026      Eva Dvořáková       2                                  │
│  ───────────────────────────────────────────────────────────────────────────────│
│  3.00    14. 3. 2026      Jan Novák          8   Nový atribut Rozpočtový cíl     │
```

Vodorovná čára odděluje **změny hlavního čísla** — tam se měnila struktura a konzumující
aplikace se toho mohly dotknout. Je to jediná informace, kterou v tomhle seznamu někdo
opravdu hledá.

---

## O2 — záložka Rozdíl verzí

```
│  Porovnat verzi [ 3.12 ▾ ]  s verzí [ 3.14 ▾ ]              [ Porovnat ]         │
│                                                                                  │
│  39 změn · 2 přidané · 36 změněných · 1 vyřazená                                 │
│                                                                                  │
│  + C-2027-001   Rozvoj infrastruktury 2027            přidána ve verzi 3.13      │
│  + C-2027-002   Digitalizace agend 2027               přidána ve verzi 3.13      │
│                                                                                  │
│  ~ C-2025-014   Obnova vozového parku                 změněna ve verzi 3.14      │
│      Název      „Obnova vozidel"  →  „Obnova vozového parku"                     │
│      Stav       „A"  →  „U"                                                      │
│                                                                                  │
│  − C-2024-009   Zrušený cíl                           vyřazena ve verzi 3.13     │
│      Platnost do  —  →  31. 12. 2024                                             │
```

**Rozhodnutí:**

- **Seskupeno po položkách, ne po atributech.** Uživatel se ptá „co se stalo s tímhle cílem",
  ne „kde všude se změnil stav".
- **Znaménka `+ ~ −` vedle barvy.** Znaménko je čitelné i vytištěné načerno.
- **Vyřazení se ukazuje jako změna platnosti**, protože přesně to se stalo.
  Nic se nemaže; předstírat mazání by lhalo o tom, co je v datech.

---

## O5 — Hromadná editace hodnot

Nejsložitější obrazovka aplikace. Vzor je tabulkový editor, protože hromadná editace
tisíce řádků je tabulková práce.

```
│  ‹ Rozpočtové cíle                                                               │
│                                                                                  │
│  Upravujete Rozpočtové cíle                                     verze 3.14       │
│  ───────────────────────────────────────────────────────────────────────────────│
│  [ Hledat… ]   [ + Přidat hodnotu ]   [ Nahrát soubor JSON ]                     │
│                                                                                  │
│    KÓD           NÁZEV                 NADŘAZENÁ      STAV  MANAŽER      PLATNOST│
│  ┌──────────────────────────────────────────────────────────────────────────────│
│  │ C-2026-001    Rozvoj infrastruktury  [—         ▾] [A▾] [Jan Novák ▾] 1.1.2026│
│  │   ↳ +Podřízená                                                                │
│  │ C-2026-001-A  Silnice II. třídy      [C-2026-001▾] [A▾] [Jan Novák ▾] 1.1.2026│
│ ~│ C-2025-014    Obnova vozového parku  [—         ▾] [U▾] [Jan Novák ▾] 1.1.2025│
│ +│ C-2027-001    Rozvoj infrastr. 2027  [—         ▾] [A▾] [          ▾] 1.1.2027│
│ −│ C-2024-009    Zrušený cíl            [—         ▾] [N▾] [Eva D.    ▾] 1.1.2024│
│  └──────────────────────────────────────────────────────────────────────────────│
│                                                                                  │
├──────────────────────────────────────────────────────────────────────────────────┤
│  3 neuložené změny            [ Zobrazit změny ]  [ Uložit ]  [ Publikovat verzi ]│
└──────────────────────────────────────────────────────────────────────────────────┘
```

**Rozhodnutí:**

- **Hlavička tabulky i spodní lišta jsou přilepené.** U tisícovky řádků musí být pořád
  vidět, co je který sloupec a kolik změn čeká.
- **Sloupec značek vlevo** (`+ ~ −`) je užší než jméno a přesto nese nejdůležitější údaj.
  Barva řádku ho doprovází, ale nenese sama.
- **Vyřazení nemá koš.** `−` znamená ukončenou platnost. Ikona koše by slibovala mazání,
  které aplikace nedělá.
- **Vazba se vybírá ze seznamu, ne píše z hlavy.** Ručně psaný kód cizí položky je
  pozvánka k překlepu, který projde až do konzumujících aplikací.
- **Sloupec `NADŘAZENÁ` se zobrazuje jen u hierarchického číselníku.** Vybírá se
  našeptávačem z položek téhož číselníku; nabídka vylučuje položku samotnou i její potomky,
  aby nešlo vyrobit cyklus.
- **Akce `+ Podřízená` na řádku** založí položku s předvyplněnou nadřazenou.
  Je to hlavní cesta, jak číselník prohloubit o další úroveň — jedno kliknutí místo
  vyhledávání rodiče v našeptávači.
- **Spodní lišta pojmenovává akce slovesem, které se opakuje i v potvrzení.**
  `Publikovat verzi` → hlášení `Verze 3.15 publikována`. Kdo klikl na *Publikovat*,
  má se dozvědět, že bylo *publikováno* — ne „operace proběhla úspěšně".

### Zamčeno někým jiným

```
│  Rozpočtové cíle                                                                 │
│  ┌──────────────────────────────────────────────────────────────────────────────│
│  │  Číselník upravuje Eva Dvořáková od 14:32. Otevřít pro úpravy zatím nejde.    │
│  │  Zámek se sám uvolní po pěti hodinách bez její činnosti.                      │
│  └──────────────────────────────────────────────────────────────────────────────│
│                                                                                  │
│  … obsah v režimu čtení …                                                        │
```

Hláška říká **kdo, od kdy a co s tím** — tedy i to, že se to samo vyřeší.
Bez poslední věty by uživatel nevěděl, jestli má čekat minutu, nebo psát správci.

Správce vidí navíc `[ Odebrat zámek ]`. Odebrání se zapisuje do auditu.

---

## O6 — Import ze souboru JSON

Tři kroky. Import **nikdy nejde rovnou do databáze** — končí v editační tabulce.

```
│   ①  Nahrát soubor   ──   ②  Zkontrolovat   ──   ③  Promítnout do tabulky        │
│   ═══════════════════      ─────────────────      ───────────────────────        │
│                                                                                  │
│              Přetáhněte soubor JSON sem, nebo jej vyberte.                        │
│                                                                                  │
│                              [ Vybrat soubor ]                                   │
│                                                                                  │
│              Očekávaný tvar souboru: Formát importu (nápověda)                    │
```

Krok 2 při nálezu chyb:

```
│   ①  Nahrát soubor   ──   ②  Zkontrolovat   ──   ③  Promítnout do tabulky        │
│                            ═════════════════                                     │
│                                                                                  │
│  Soubor obsahuje 4 chyby. Opravte je a nahrajte soubor znovu.                     │
│                                                                                  │
│  POLOŽKA      ATRIBUT        CO JE ŠPATNĚ                                        │
│  ───────────────────────────────────────────────────────────────────────────────│
│  5011         cisloPolozky   Kód 5011 se v souboru opakuje.                      │
│  5012         trida          Třída „9" v číselníku Rozpočtové třídy neexistuje.  │
│  5013         —              Nadřazená položka „501" v souboru ani číselníku není.│
│  5014         castka         „dvě stě" není číslo.                               │
│                                                                                  │
│                                              [ Nahrát jiný soubor ]              │
```

Krok 2 bez chyb:

```
│  Soubor je v pořádku. Obsahuje 1 043 hodnot.                                     │
│                                                                                  │
│      12 přidaných        1 029 beze změny        2 změněné        0 vyřazených   │
│                                                                                  │
│                          [ Promítnout do tabulky ]                               │
```

**Rozhodnutí:**

- **Vypíší se všechny chyby najednou**, ne první a konec. Kdo přepisuje tisíc řádků
  z vyhlášky, potřebuje vidět všechno naráz.
- **Chyba říká, co je špatně, ne že se něco nezdařilo.** „Třída 9 v číselníku
  Rozpočtové třídy neexistuje" je návod. „Ověření selhalo" není.
- **Krok 3 nekončí uložením, ale otevřením editační tabulky** se zvýrazněnými změnami.
  Uživatel může ještě sáhnout do dat, než uloží.

---

## O7 — Definice struktury číselníku

```
│  Struktura · Rozpočtové cíle                                    [ Přidat atribut ]│
│                                                                                  │
│  Změna struktury zvýší hlavní číslo verze. Konzumující aplikace se jí mohou       │
│  dotknout — po publikování si mají znovu načíst popis struktury.                  │
│                                                                                  │
│  ATRIBUTY                                                                        │
│  KÓD           NÁZEV              TYP      POVINNÝ                               │
│  ───────────────────────────────────────────────────────────────────────────────│
│  cisloCile     Číslo cíle         text     ano                                   │
│  stav          Stav               výčet    ano        A, U, N                    │
│  popis         Popis              text     ne                                    │
│                                                                                  │
│  VAZBY                                                          [ Přidat vazbu ] │
│  KÓD               NÁZEV             CÍLOVÝ ČÍSELNÍK      POVINNÁ                │
│  ───────────────────────────────────────────────────────────────────────────────│
│  manazer           Manažer cíle      Osoby                ano                    │
│  ekonomickyOrgan   Ekonomický orgán  Ekonomické orgány    ne                     │
```

Varování o dopadu je **nad tabulkou, ne v potvrzovacím okně**. Kdo sem přišel, má vědět,
do čeho jde, dřív než něco změní.

---

## O8 — Role uživatelů

```
│  Role uživatelů                                              [ Přidat přiřazení ]│
│                                                                                  │
│  [ Hledat osobu… ]                                                               │
│                                                                                  │
│  OSOBA             ROLE                 DATOVÝ ROZSAH                            │
│  ───────────────────────────────────────────────────────────────────────────────│
│  Jan Novák         Editor               Cíle, Výdajové oblasti                   │
│  Eva Dvořáková     Editor               všechny číselníky                        │
│  Petr Svoboda      Správce číselníků    Cíle                                     │
│  Marie Horáková    Správce aplikace     —                                        │
```

Role a rozsah jsou **v jednom řádku**, protože se přidělují společně a odděleně nedávají
smysl. „Editor" bez rozsahu neznamená nic.

---

## O9 — Efektivní práva

```
│  Efektivní práva · Jan Novák                                                     │
│                                                                                  │
│  AKCE                    ROZSAH                        ZDROJ                     │
│  ───────────────────────────────────────────────────────────────────────────────│
│  hodnoty.edit            Cíle, Výdajové oblasti        role Editor               │
│  verze.publish           Cíle, Výdajové oblasti        role Editor               │
│  ciselniky.create        —                             nemá                      │
```

Sloupec **Zdroj** je celý smysl obrazovky. Odpovídá na otázku „proč tohle smí" —
a až se práva začnou brát z centrálního systému řízení přístupů, přibude jen další
hodnota v tomto sloupci a obrazovka se nemění.

---

## Tisk a PDF

Bez railu, bez horní lišty, bez ovládacích prvků. V hlavičce **kód číselníku, název,
verze a datum vydání** — bez nich je vytištěný číselník nedatovatelný papír.
V patičce číslo stránky a stálý identifikátor číselníku.
