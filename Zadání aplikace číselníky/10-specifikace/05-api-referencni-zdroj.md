# Rozhraní pro konzumující aplikace

Pokrývá schopnost [S6](03-schopnosti.md). **Návrh k odsouhlasení.**

## Zadání

Konzumující aplikace si vyžádá číselník a dostane jeho hodnoty, platnost, verzi a popis
struktury. Rozhraní musí obsloužit i **víceúrovňový** číselník.

> **Jedno rozhraní pro všech ~50 číselníků.** Ne jedna služba na číselník.

---

## Poznámka k pojmu „WSDL schéma"

WSDL popisuje **SOAP** webové služby. U rozhraní postaveného na REST se tatáž potřeba
pokrývá dvěma různými dokumenty a je užitečné je nezaměňovat:

| Co popisuje | Čím |
|---|---|
| **Rozhraní** — jaké adresy existují, jaké parametry berou | **OpenAPI**. Jeden dokument pro celou aplikaci. Statický — rozhraní je generické, novým číselníkem se nemění. |
| **Strukturu konkrétního číselníku** — jaké atributy mají jeho položky, jaké typy, co je povinné | **JSON Schema**. Generuje se z definice číselníku, jeden na číselník a verzi. Tohle je funkční ekvivalent WSDL pro číselník. |

Rozhodnuto (Z3): **SOAP nikdo z konzumentů nepotřebuje.** Rozhraní je výhradně REST.

## Zda existuje něco lepšího než REST

Pro tento účel ne. REST je správná volba, protože:

- data jsou **čitelná, ne měnitelná** — konzument nic nezapisuje,
- odpovědi jsou **dobře mezipaměťovatelné**, což je při neomezeném počtu čtenářů zásadní,
- konzumenti v tomto prostředí REST umí; cokoli jiného zvyšuje bariéru vstupu.

GraphQL by dával smysl, kdyby si každý konzument bral jiný výřez z velkého grafu.
Tady si bere celý číselník. Přidal by složitost bez užitku.

---

## Návrh rozhraní

Základ: `/api/v1/`

| Adresa | Vrací |
|---|---|
| `GET /ciselniky` | Seznam číselníků — kód, název, popis, aktuální verze, kdy naposledy změněn |
| `GET /ciselniky/{kod}` | Metadata číselníku a definice jeho struktury |
| `GET /ciselniky/{kod}/schema` | **JSON Schema** položek číselníku |
| `GET /ciselniky/{kod}/polozky` | Hodnoty číselníku |
| `GET /ciselniky/{kod}/polozky/{kodPolozky}` | Jedna hodnota |
| `GET /ciselniky/{kod}/verze` | Seznam verzí |
| `GET /ciselniky/{kod}/verze/{verze}/zmeny` | Změny obsažené v dané verzi, např. `…/verze/3.14/zmeny` |

### Parametry výdeje hodnot

| Parametr | Význam | Výchozí |
|---|---|---|
| `verze` | Vydá číselník tak, jak vypadal v dané verzi | aktuální |
| `platneK` | Vydá jen hodnoty věcně platné k datu | bez filtru |
| `tvar` | `plochy` nebo `strom` — jak se vrací hierarchie uvnitř číselníku | `plochy` |
| `rozbalit` | Výčet vazeb, které se mají doplnit celé místo odkazu | žádné |
| `zmenyOd` | Vrátí jen rozdíl proti uvedené verzi — pro konzumenty, kteří si drží kopii | bez filtru |
| `strana`, `velikost` | Stránkování | 1, 200 |

### Jak se řeší „víceúrovňový"

Zadání používá jedno slovo pro dvě různé věci. Rozhraní je rozlišuje:

**1. Hierarchie uvnitř číselníku** — cíl má nadřazený cíl.

- `tvar=plochy` *(výchozí)* — plochý seznam, každá položka nese kód nadřazené položky.
  Jednoduché na zpracování i na mezipaměť.
- `tvar=strom` — položky vnořené do sebe. Pro přímé vykreslení stromu.

Táž data, dva tvary. Konzument si vybere.

**2. Vazba na jiný číselník** — cíl odkazuje na manažera z číselníku osob.

Ve výchozím stavu se vrací **odkaz**, ne obsah:

```json
"manazer": { "ciselnik": "osoby", "kod": "12345",
             "uri": "{BAZE}/id/osoby/12345" }
```

Parametrem `rozbalit=manazer` se odkaz nahradí celou položkou.

> Tenhle rozdíl je jádro genericity. Hierarchie je tvar výdeje, vazba je odkaz na jiný
> zdroj. Kdyby se obojí řešilo vnořováním, každý číselník by potřeboval vlastní tvar
> odpovědi — a tím i vlastní službu.

### Tvar odpovědi

```json
{
  "ciselnik": "cile",
  "nazev": "Rozpočtové cíle",
  "verze": "3.14",
  "verzeVydana": "2026-08-30T10:12:00+02:00",
  "uri": "{BAZE}/id/cile",
  "pocet": 312,
  "polozky": [
    {
      "kod": "C-2026-001",
      "nazev": "Rozvoj infrastruktury",
      "uri": "{BAZE}/id/cile/C-2026-001",
      "platnostOd": "2026-01-01",
      "platnostDo": null,
      "nadrazenyKod": null,
      "atributy": { "cisloCile": "001", "stav": "A", "popis": "..." },
      "vazby": {
        "manazer": { "ciselnik": "osoby", "kod": "12345", "uri": "..." },
        "ekonomickyOrgan": { "ciselnik": "ekonomicke-organy", "kod": "EO-7", "uri": "..." }
      }
    }
  ]
}
```

Stálé části obálky (`ciselnik`, `verze`, `polozky`) jsou u všech číselníků totožné.
Proměnná je jen část `atributy` a `vazby` — a tu popisuje JSON Schema.

### Bázová adresa identifikátorů

Rozhodnuto (Z4): **identifikátor je adresa aplikace v doméně FIS.**

> **Přesný tvar adresy se musí ustálit před prvním publikováním verze.**
> Od té chvíle je **neměnný** — je součástí identity dat, ne konfigurace. Konzumenti si
> identifikátory ukládají u sebe a změna by jim je rozbila.
> Zapsat mezi podmínky nasazení.

V dalším textu se bázová adresa píše jako `{BAZE}`.

### Propojená otevřená data

Každý číselník i každá položka má **stálý identifikátor URI**. Ten se nemění při změně
názvu ani při vydání nové verze — to je celý smysl stálého identifikátoru.

Rozhraní podporuje volbu formátu podle hlavičky `Accept`:

| Hlavička | Odpověď |
|---|---|
| `application/json` *(výchozí)* | Tvar výše. Konzument nemusí o propojených datech nic vědět. |
| `application/ld+json` | Táž data doplněná o `@context` postavený na **SKOS** — strojově srozumitelná jako propojená data podle mezinárodního standardu |

Slovník výstupu, mapování našich pojmů na SKOS a pravidlo pro tři různé druhy odkazu:
[20-architektura/06-propojena-data-skos.md](../20-architektura/06-propojena-data-skos.md).

> **Závazné pravidlo:** `skos:broader` se používá **výhradně pro hierarchii uvnitř jednoho
> číselníku**. Vazba na jiný číselník dostává **vlastní doménový predikát**, nikdy
> `skos:broader`. Je to častá chyba a v hotovém výstupu se špatně opravuje.

Takto se principy 5★ naplní, aniž by se jimi zatížil běžný konzument.

### Číslo verze

Verze je **dvojice hlavní.vedlejší**, například `3.14`.

- **hlavní** se zvýší, když se změnila **struktura** číselníku — konzument by se měl podívat,
  jestli se ho to dotýká,
- **vedlejší** se zvýší, když se změnily jen **hodnoty** — struktura drží, konzument nemusí dělat nic.

Parametr `verze` přijímá plný tvar (`verze=3.14`).

### Kdo smí číst

**Výhradně přes Active Directory.** Žádné přístupové klíče, žádné vlastní tokeny.
Konzumující aplikace se hlásí doménovým servisním účtem.

**Čtení se nesleduje.** Auditní log pokrývá zápisové operace — kdo co změnil.
Kdo si číselník přečetl, aplikace neeviduje a evidovat nemá.

### Mezipaměť

Čte neomezený počet uživatelů a aplikací. Odpověď proto nese `ETag` odvozený od verze
číselníku. Konzument, který se ptá znovu, dostane `304` a nic se nepřenáší.

Nová verze číselníku = nový `ETag` = konzument si stáhne aktuální data.
