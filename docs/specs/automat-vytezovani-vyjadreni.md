# Specifikace — automat vytěžování vyjádření ticketů

**Stav:** rozpracováno (fáze 3 implementace)
**Závisí na:** [ticketing-integration.md](ticketing-integration.md)

## Kontext

Ticket v ticketovacím systému prochází několika **stavy** (např. *analýza → řešení → testování → předáno*).
PM Tracker potřebuje tyto stavy promítat do **skutečnosti harmonogramu** záznamu
(viz [harmonogram-plan-vs-skutecnost.md](harmonogram-plan-vs-skutecnost.md)).

Explicitní pole „stav" na ticketu **neexistuje** — stav se vyvozuje ze souslednosti textů
**vyjádření ticketu** (rich-textové zápisy pracovníků v XML). Starý ticketing nelze upravit,
proto musí PM Tracker stav vytěžovat **automatem** (parsováním textu) a poskytovat UI
pro **ruční korekci** odhadem 20–50 % případů, kde automat selže.

## Přehled

```
ticket v ticketing DB ──[TicketingDeltaSyncJob]──▶ TicketVyjadreniCache
                                                         │
                                                         ▼
                                              VytezovaniRuleEngine
                                              (sada pravidel: fráze + pozice)
                                                         │
                                                         ▼
                                              TicketVyjadreniTag (ZdrojEnum=Automat)
                                                         │
                                                         ▼
                              ┌──────[manuální korekce]──┐
                              │ adm_proj / proj_man     │
                              │ v modalu „externí vazby" │
                              └──────────────────────────┘
                                           │
                                           ▼
                              TicketVyjadreniTag (ZdrojEnum=Manual, VytvorilOsobaId=X)
                                           │
                                           ▼
                   Skutečnost harmonogramu = MIN(Datum) z tagů daného stavu
```

## Rule Engine

### Struktura pravidla

Pravidla definují, **jaký tag přiřadit vyjádření** na základě jeho textu + kontextu předchozích vyjádření.

```csharp
public sealed record VytezovaniPravidlo(
    string Kod,                      // identifikátor pravidla
    string TagKod,                   // jaký tag přiřadit
    IReadOnlyList<string> Fraze,     // hledané fráze (OR-ing)
    VytezovaniPodminka? Podminka,    // volitelné — pozice v sequenci, autor, atd.
    int Priorita,                    // vyšší priorita vyhrává při shodě
    bool Aktivni
);

public enum VytezovaniPodminka
{
    Zadna,
    PrvniVyjadreni,             // jen pokud je to první vyjádření ticketu
    NeniPoStejnemTagu,          // nepřiřadit, pokud už existuje tag (detekce re-dodávky)
    PoTagu,                     // navazuje na jiný tag (pořadí)
}
```

### Uložení pravidel

- **DB tabulka** `VytezovaniPravidla` — admin konfiguruje v UI `/Nastaveni/Vytezovani`
- Změna je **okamžitě aktivní** pro nové vyjádření (rules cache invalidate-on-write)
- **Historie změn** pravidel je auditována (FK na `Osoba`, timestamp)

### Fráze matching

- **Case-insensitive** substring match
- **Normalizace**: odstranění diakritiky, více-mezer → jedna, lowercase
- Support pro **celá slova** (pattern `\bfráze\b` regex) — admin volí per pravidlo
- **Priorita**: vyšší priorita pravidla vyhrává, pokud matchují dvě

### Omezení

Fráze jsou heuristika. Očekává se:
- **Pokrytí automatem: 50–80 %** (dle business odhadu)
- **Zbytek řeší ručně** adm_proj / proj_man přes UI

Re-dodávky a podobné edge cases (*dodavatel dodal podruhé po vrácení*) **automat nerozezná** —
spoléhá se na ruční opravu.

## Delta zpracování

**Klíčové:** automat se spouští **jen na nová vyjádření** (od posledního úspěšného sync).
Stará vyjádření se **přehodnocují jen při manuální akci** (admin spustí re-scan z UI).

Důvod:
- Výkonnost — XSL parsování XML 100 000 vyjádření = dlouhá doba
- Stabilita — existující manuální tagy se nepřepisují
- Auditovatelnost — přidané tagy mají jasnou historii

### Rozhodnutí: co dělat s existujícím tagem

Když automat vyhodnotí nové vyjádření a mohl by přiřadit tag, který **už existuje pro ticket** od jiného vyjádření:

- **Pokud `VytezovaniPodminka.NeniPoStejnemTagu`** — nepřiřadit (re-dodávka se ignoruje, zachová se původní termín)
- **Jinak** — přiřadit a v harmonogramu vyhrává **nejranější datum** (MIN)

## UI — modal „Externí vazby → Vyjádření ticketu"

### Přístup

V editoru záznamu (`_EditZaznamForm.cshtml`) je karta **Externí vazby**. Pro adm_proj / proj_man:

- U každé vazby (ticket ID) je tlačítko **„Zobrazit vyjádření ticketu"**
- Otevře modal s timeline vyjádření (chronologicky)
- Každé vyjádření zobrazeno: datum, autor, text (řezně-sanitizovaný XML → HTML)
- **Panel tagů** vpravo: seznam dostupných tagů (kroky harmonogramu), checkboxes

### Akce

- **Přidat tag** k vyjádření — checkbox zaškrtnout → ukládá se jako `Manual`
- **Odebrat tag** — odškrtnout → soft delete + audit log
- **Přepnout zdroj** — ruční opravou `Automat` tagu vytvoříme `Manual` tag (override)
- **Zobrazit zdroj tagu** — ikonka (automat/člověk) vedle tagu + tooltip (pravidlo / osoba)

### ACL

- **Zobrazit** modal: všichni, kdo vidí projekt
- **Editovat** tagy: **jen** `adm_proj` + `proj_man` (viz role v `Security.Roles`)
- **Smazat/přidat pravidlo** v nastavení: **jen** `SuperAdmin`

## Datový model

Viz [ticketing-integration.md](ticketing-integration.md) sekce "Datový model" —
tabulky `TicketVyjadreniCache`, `TicketVyjadreniTag`.

Nové tabulky zde:

### `VytezovaniPravidla`

| Sloupec | Typ | Popis |
|---|---|---|
| `Id` | int | PK |
| `Kod` | varchar(50) | Unikátní identifikátor |
| `TagKod` | varchar(50) | Jaký tag přiřadit (FK na kroky harmonogramu) |
| `Fraze` | nvarchar(max) | Fráze (JSON array) |
| `Podminka` | tinyint | VytezovaniPodminka enum |
| `Priorita` | int | |
| `Aktivni` | bit | Soft disable bez smazání |
| `VytvorenoDne` / `UpravenoDne` | datetime2 | Audit |
| `VytvorilOsobaId` / `UpravilOsobaId` | int | Audit |

## Testy (fáze 3)

- **Unit `VytezovaniRuleEngineTests`** — happy-path, priorita, `NeniPoStejnemTagu`, diakritika
- **Integration** — DB-integrovaný test: seed pravidla, seed vyjádření, asserce přiřazených tagů
- **Regression** — fixture s reálnými vyjádřeními (anonymizované) z produkce pro ověření >50 % recall

## Otevřené otázky

| # | Otázka | Kdo rozhodne | Deadline |
|---|---|---|---|
| V1 | **Finální seznam frází** — jaké stavy, jaké fráze | Vedení (čeká na redukci kroků harmonogramu) | Před fází 3 |
| V2 | Priorita pravidel — jak ji definovat (přirozená ordinál, explicitní weight)? | Návrh — Claude | Při implementaci |
| V3 | Re-evaluation — může admin spustit re-scan všech vyjádření? (přemazání automat tagů) | Claude doporučení: ano, s confirm dialogem | Při implementaci |
| V4 | Conflict resolution — automat přiřadil tag X, manuál přiřadil Y. Co vyhrává? | Claude doporučení: manuál vždy vyhrává | Při implementaci |
| V5 | Smazání pravidla — mažou se všechny tagy vytvořené tím pravidlem? | Claude doporučení: NE, soft-disable pravidla, tagy zůstávají | Při implementaci |

## Zodpovědnost

- **Definice frází a stavů:** vedení (po schválení redukce kroků harmonogramu)
- **Návrh rule enginu a implementace:** Claude + dodavatel
- **Testovací datové sety (anonymní vyjádření z produkce):** Ing. Andrlík
- **UI modal:** Claude (dle fáze 2 — TagHelper komponenty)
